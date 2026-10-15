using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第三小批）：**第一组基础牌（来源逐条对照）+ 胜利终局的生命周期场景**。
///
/// 三张卡都来自 `ContentSource/ironclad-cards.spire`，且都能用**既有**步骤能力表达：
/// <list type="bullet">
/// <item><c>IronWave</c>（:2103）：格挡 5 + 伤害 5（升级 7/7）——两者共用同一数值字段，正好与来源一致；</item>
/// <item><c>TwinStrike</c>（:2438）：`repeat count = 2` 的 5 点攻击（升级 7），用既有的**批次数**表达；</item>
/// <item><c>Breakthrough</c>（:1549）：对全体 9 点（升级 13）+ **自损 1 点**——用新增的字面量步骤 `lose_hp:1` 表达
/// （同一张牌上"伤害取卡牌数值、自损取固定值"是来源事实）。</item>
/// </list>
/// 另含**胜利**场景：清空全部敌人时终局是 `Victory`，且终局回合取自玩家回合数。
/// </summary>
internal static class SliceFrontendBasicCardChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  iron-wave  twin-strike  breakthrough }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick  damage = multiply-incoming:1.5 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 40  tough-hp = 41  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 40  tough-hp = 41  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 9  steps = [target, damage] }
        }
        card iron-wave {
          name = IronWave  kind = Attack  cost = 1
          source = base-spire/ContentSource/ironclad-cards.spire
          line = 2103
          normal   { amount = 5  steps = [target, block, damage] }
          upgraded { amount = 7  steps = [target, block, damage] }
        }
        card twin-strike {
          name = TwinStrike  kind = Attack  cost = 1
          source = base-spire/ContentSource/ironclad-cards.spire
          line = 2438
          normal   { amount = 5  steps = [target, damage x2] }
          upgraded { amount = 7  steps = [target, damage x2] }
        }
        card breakthrough {
          name = Breakthrough  kind = Attack  cost = 1
          source = base-spire/ContentSource/ironclad-cards.spire
          line = 1549
          normal   { amount = 9  steps = [lose_hp:1, damage_all] }
          upgraded { amount = 13  steps = [lose_hp:1, damage_all] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 基础牌：三张卡的来源数值与步骤逐条对照（含升级形态）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "basic");

            var ironWave = model.Cards.Single(card => card.Id == "base-spire:iron-wave");
            Check.Equal(2103, ironWave.SourceLine, "来源行");
            Check.Sequence([AuthorSteps.Target, AuthorSteps.Block, AuthorSteps.Damage], ironWave.NormalSteps.Select(step => step.Kind),
                "先格挡后打（来源 play 的次序）");
            Check.Equal(5, ironWave.NormalAmount, "普通 5/5");
            Check.Equal(7, ironWave.UpgradedAmount, "升级 7/7");

            var twin = model.Cards.Single(card => card.Id == "base-spire:twin-strike");
            Check.Equal(2438, twin.SourceLine, "来源行");
            Check.Equal(2, twin.NormalSteps.Single(step => step.Kind == AuthorSteps.Damage).Count,
                "repeat count = 2（目标步骤仍是一次）");
            Check.Equal(5, twin.NormalAmount, "每击 5");
            Check.Equal(7, twin.UpgradedAmount, "升级每击 7");

            var breakthrough = model.Cards.Single(card => card.Id == "base-spire:breakthrough");
            Check.Equal(1549, breakthrough.SourceLine, "来源行");
            Check.Sequence([AuthorSteps.LoseHp + ":1", AuthorSteps.DamageAll],
                breakthrough.NormalSteps.Select(step => step.Kind), "自损 1 点在前、全体伤害在后");
            Check.Equal(9, breakthrough.NormalAmount, "对全体 9");
            Check.Equal(13, breakthrough.UpgradedAmount, "升级对全体 13");

            // 非法字面量必须被拒绝（不能悄悄当成卡牌数值）。
            try
            {
                SliceAuthorFrontend.Parse(ScenarioText.Replace("lose_hp:1", "lose_hp:x"), "basic");
                Check.True(false, "非法字面量必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("invalid_value", failure.Code, "错误码");
                Check.True(failure.Path.Contains("steps", StringComparison.Ordinal), $"路径：{failure.Path}");
            }
        });

        Check.Run("B-06a 基础牌：IronWave 同时给格挡与伤害；TwinStrike 打两次且力量**每击**都生效 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "basic");

            // IronWave：5 点伤害 + 5 点格挡（同一个数值字段）。
            var ironWaveSession = Build(model);
            var ironPlayer = SliceFixtures.Player(ironWaveSession);
            var ironTarget = SliceFixtures.Cultist(ironWaveSession, SliceManifest.CalcifiedCultistId);
            var hpBefore = SliceFixtures.Number(ironWaveSession, ironTarget, SliceFixtures.MonsterTypeId, "Hp");
            var blockBefore = SliceFixtures.Number(ironWaveSession, ironPlayer, SliceFixtures.PlayerTypeId, "Block");
            var ironWave = SliceFixtures.PutInHand(ironWaveSession, "base-spire:iron-wave", upgraded: false);
            SliceFixtures.PlayAt(ironWaveSession, ironWave, ironTarget);
            Check.Equal(5L, hpBefore - SliceFixtures.Number(ironWaveSession, ironTarget, SliceFixtures.MonsterTypeId, "Hp"),
                "伤害 5");
            Check.Equal(blockBefore + 5,
                SliceFixtures.Number(ironWaveSession, ironPlayer, SliceFixtures.PlayerTypeId, "Block"), "格挡 +5");

            // TwinStrike：两次 5 点；再加 3 力量则每击 8 → 共 16（证明力量**逐击**生效而不是只算一次）。
            long TwinDamage(bool withStrength, bool upgraded)
            {
                var session = Build(model);
                var player = SliceFixtures.Player(session);
                if (withStrength)
                {
                    Attach(session, player, SliceManifest.StrengthId, 3);
                }

                var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
                var card = SliceFixtures.PutInHand(session, "base-spire:twin-strike", upgraded,
                    upgraded ? (Amount: 7, PowerStacks: 0) : null);
                SliceFixtures.PlayAt(session, card, target);
                return before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            }

            Check.Equal(10L, TwinDamage(false, false), "普通两次 5 = 10");
            Check.Equal(14L, TwinDamage(false, true), "升级两次 7 = 14");
            Check.Equal(16L, TwinDamage(true, false), "3 力量下两次 (5+3) = 16");
        });

        Check.Run("B-06a 基础牌：Breakthrough 对**每个**敌人各打一次，播放者自损固定 1 点 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "basic");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var playerHpBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            var frontBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            var backBefore = SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");

            var card = SliceFixtures.PutInHand(session, "base-spire:breakthrough", upgraded: false);
            // 全体伤害不需要选目标：提交后不应出现目标决策。
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
            Check.Equal(SessionStatus.Running, session.Status, "damage_all 不请求目标选择");

            Check.Equal(9L, frontBefore - SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "前排 -9");
            Check.Equal(9L, backBefore - SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                "后排 -9（两个都打到）");
            Check.Equal(playerHpBefore - 1,
                SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "自损是**固定的 1 点**，不是卡牌数值 9");
        });

        Check.Run("B-06a 胜利终局：清空全部敌人 → 最后一间房才 Victory，且终局回合取自当前玩家回合 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "basic");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var monsterType = session.RuleSet.TypeByQualifiedId(SliceFixtures.MonsterTypeId);
            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var runStates = SliceFixtures.Collection(session, "base-spire:RunStates");
            var run = session.State.CollectionMembers(runStates).Single();

            // 把两个敌人都压到 5 点，再用升级的 Breakthrough（13 点全体）一次清场。
            session.State.RunAtomic(() =>
            {
                session.State.SetField(front, monsterType.FieldIndex("Hp"), Value.Of(5L));
                session.State.SetField(back, monsterType.FieldIndex("Hp"), Value.Of(5L));
            });

            var card = SliceFixtures.PutInHand(session, "base-spire:breakthrough", upgraded: true,
                (Amount: 13, PowerStacks: 0));
            SliceFixtures.PlayAt(session, card, front);

            Check.Equal(0L, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"), "前排归零");
            Check.Equal(0L, SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"), "后排归零");
            // W-05：**战斗终止 ≠ 整局终止**——还有下一间房时，清房只让这间房结算，会话继续可操作。
            Check.Equal(SessionStatus.Running, session.Status, "清掉第一间房后会话继续（整局未结束）");
            Check.Equal(1L, session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(),
                "当前房间号 = 1");
            Check.Equal(1L, session.State.GetField(run, runType.FieldIndex("SettledRoom")).AsInt(),
                "第一间房已结算");

            // 进入第二间房：敌人重建、玩家从整局状态重置、战斗牌由长期牌组重新铺开。
            var enter = session.LegalActions(SliceManifest.Player)
                .Single(action => action.Definition.QualifiedId == "base-spire:EnterNextRoom");
            session.Submit(enter.Definition, SliceManifest.Player, []);
            Check.Equal(2L, session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(),
                "当前房间号 = 2");
            Check.Equal(40L, SliceFixtures.Number(session, SliceFixtures.Cultist(session,
                SliceManifest.CalcifiedCultistId), SliceFixtures.MonsterTypeId, "Hp"),
                "第二间房的敌人血量回到定义值（新实例）");

            // 第二间房：先结束一回合（回合数变 2），再清场 ⇒ 终局回合应为 **2**。
            var endTurn = session.LegalActions(SliceManifest.Player)
                .Single(action => action.Definition.QualifiedId == "base-spire:EndTurn");
            session.Submit(endTurn.Definition, SliceManifest.Player, []);
            var front2 = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back2 = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            session.State.RunAtomic(() =>
            {
                session.State.SetField(front2, monsterType.FieldIndex("Hp"), Value.Of(5L));
                session.State.SetField(back2, monsterType.FieldIndex("Hp"), Value.Of(5L));
            });
            var card2 = SliceFixtures.PutInHand(session, "base-spire:breakthrough", upgraded: true,
                (Amount: 13, PowerStacks: 0));
            SliceFixtures.PlayAt(session, card2, front2);

            Check.Equal(SessionStatus.Terminal, session.Status, "最后一间房清完 ⇒ 会话进入终局");
            Check.Equal("base-spire:Victory", session.Terminal!.Definition.QualifiedId, "终局是胜利");
            Check.Equal(2L, session.Terminal!.Fields[0].AsInt(), "终局回合 = 当前玩家回合数（第二间房的第 2 回合）");
            _ = player;
        });
    }

    private static void Attach(Session session, InstanceId owner, string powerId, int stacks)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        var power = session.State.CreateInstance(powerType.Id, session.State.ScopeOf(owner));
        session.State.RunAtomic(() =>
        {
            session.State.SetField(power, powerType.FieldIndex("DefinitionId"), Value.Of(powerId));
            session.State.SetField(power, powerType.FieldIndex("Stacks"), Value.Of((long)stacks));
            session.State.SetField(power, powerType.FieldIndex("Ticks"), Value.Of(true));
            session.State.SetField(power, powerType.FieldIndex("SkipNextTick"), Value.Of(true));
            session.State.AddRelation(relation.Id, owner, power);
        });
    }

    private static Session Build(SliceAuthorModel model)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: model)
            .Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }
}
