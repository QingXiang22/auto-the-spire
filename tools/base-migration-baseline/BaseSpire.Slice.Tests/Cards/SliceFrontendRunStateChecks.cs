using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-07a：整局状态（第一/第二小批的检查）。
///
/// 本文件先只做**能立刻判定的事**：`run { … }` 声明是否被解析、`Run` 单例是否真的被创建并进了 `RunStates` 集合。
/// 这两条成立之后，"结算过程取不到实例"才可能是**过程内的查法**问题；不成立就是创建根本没发生。
///
/// **历史注记**：这里曾经记着"`lose_hp` 造成的失败终局不写回整局"的已知缺陷。该缺陷已经修好，
/// `LosePlayerHp`/`DamagePlayer` 两条路径现在都在设终局**之前**调 `SettleTail`，所以下面
/// "B-07a 写回：失败（HP 损失路径）也写回一次"是**通过检查**（`Run.Hp == 0`、`SettledRoom == CurrentRoom`）。
/// 胜利侧的余效时序见 `SliceFrontendSettlementChecks`（ENV-T02）。
/// </summary>
internal static class SliceFrontendRunStateChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x8  test-selfharm  test-generate }
        run {
          hp = 73
          max-hp = 91
          gold = 123
        }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 120  tough-hp = 121  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 120  tough-hp = 121  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 6  steps = [target, damage] }
        }
        card test-selfharm {
          name = TestSelfHarm  kind = Skill  cost = 0
          normal   { amount = 1  steps = [lose_hp:1] }
          upgraded { amount = 3  steps = [lose_hp:1] }
        }
        card test-generate {
          name = TestGenerate  kind = Skill  cost = 0
          normal   { amount = 0  steps = [generate:strike-ironclad] }
          upgraded { amount = 0  steps = [generate:strike-ironclad] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-07a 整局：`run { … }` 声明被解析（未知键被拒）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            Check.Equal(73, model.Run.Hp, "hp");
            Check.Equal(91, model.Run.MaxHp, "max-hp");
            Check.Equal(123, model.Run.Gold, "gold");

            try
            {
                SliceAuthorFrontend.Parse(ScenarioText.Replace("gold = 123", "gold = 123  relics = 2"), "run");
                Check.True(false, "未知键必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("unknown_key", failure.Code, "错误码");
                Check.True(failure.Path.Contains("relics", StringComparison.Ordinal), $"路径：{failure.Path}");
            }
        });

        Check.Run("B-07a 整局：`Run` 单例在开局被创建，且带着声明的数值 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);

            var runStates = session.RuleSet.Collections.SingleOrDefault(
                collection => collection.QualifiedId == "base-spire:RunStates");
            Check.True(runStates is not null, "包里有 RunStates 集合");
            var members = session.State.CollectionMembers(runStates!.Id).ToList();
            Check.Equal(1, members.Count, "开局恰好一个 Run 单例");

            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var run = members[0];
            Check.Equal(73L, session.State.GetField(run, runType.FieldIndex("Hp")).AsInt(), "Hp = 声明值");            Check.Equal(91L, session.State.GetField(run, runType.FieldIndex("MaxHp")).AsInt(), "MaxHp = 声明值");
            Check.Equal(123L, session.State.GetField(run, runType.FieldIndex("Gold")).AsInt(), "Gold = 声明值");
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            Check.False(session.State.GetField(SliceFixtures.Player(session),
                    playerType.FieldIndex("BattleSettled")).AsBool(),
                "这场战斗尚未结算（幂等键在战斗作用域的玩家上）");
        });

        Check.Run("B-07a 写回：胜利后整局 HP = 玩家收尾 HP，且 BattleSettled 置位 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var monsterType = session.RuleSet.TypeByQualifiedId(SliceFixtures.MonsterTypeId);
            var run = RunInstance(session);
            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);

            // 把**两个**敌人都压到 5 点（只有全部死亡才算胜利），再打一张 6 点的打击 → 胜利（战斗结束即写回）。
            var other = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            session.State.RunAtomic(() =>
            {
                session.State.SetField(target, monsterType.FieldIndex("Hp"), Value.Of(5L));
                session.State.SetField(other, monsterType.FieldIndex("Hp"), Value.Of(5L));
            });
            // 单目标攻击一次只杀一个，因此打两张：先杀前排、再杀后排（全部死亡才算胜利）。
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike, target);
            var second = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, second, other);

            Check.Equal(SessionStatus.Running, session.Status,
                "清房后会话继续（战斗终止 ≠ 整局终止；W-05）");
            var finalHp = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            Check.Equal(finalHp, session.State.GetField(run, runType.FieldIndex("Hp")).AsInt(),
                "整局 HP 写回成战斗收尾 HP");
            Check.Equal(1L, session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(),
                "当前房间号 = 1");
            Check.Equal(session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(),
                session.State.GetField(run, runType.FieldIndex("SettledRoom")).AsInt(),
                "这间房的守卫置位（SettledRoom == CurrentRoom == 1 ⇒ 写回恰好一次）");
        });

        Check.Run("B-07a 写回：只死一个敌人时**不**结算（战斗未结束）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var monsterType = session.RuleSet.TypeByQualifiedId(SliceFixtures.MonsterTypeId);
            var run = RunInstance(session);
            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            var player = SliceFixtures.Player(session);

            session.State.RunAtomic(() => session.State.SetField(target, monsterType.FieldIndex("Hp"), Value.Of(5L)));
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike, target);

            Check.Equal(SessionStatus.Running, session.Status, "还有一个敌人存活 ⇒ 战斗继续");
            Check.Equal(73L, session.State.GetField(run, runType.FieldIndex("Hp")).AsInt(),
                "整局 HP **未**被写回（战斗未结束）");
            Check.Equal(session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(),
                session.State.GetField(run, runType.FieldIndex("SettledRoom")).AsInt() + 1L,
                "这间房尚未结算（SettledRoom = CurrentRoom - 1，仍可继续到结束再结算）");
        });

        Check.Run("B-07a 写回：失败（HP 损失路径）也写回一次 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var run = RunInstance(session);
            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);

            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Hp"), Value.Of(1L)));
            var selfHarm = SliceFixtures.PutInHand(session, "base-spire:test-selfharm", upgraded: false);
            SliceFixtures.PlayAt(session, selfHarm, player);

            Check.Equal(SessionStatus.Terminal, session.Status, "会话进入终局");
            Check.Equal("base-spire:Defeat", session.Terminal!.Definition.QualifiedId, "失败");
            Check.Equal(0L, session.State.GetField(run, runType.FieldIndex("Hp")).AsInt(), "整局 HP 写回 0");
            Check.Equal(session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(),
                session.State.GetField(run, runType.FieldIndex("SettledRoom")).AsInt(), "守卫置位（按房间）");
        });
        Check.Run("W-05 长期牌组：按声明逐张建立，且与战斗实例是**两批独立实例** [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);

            var longTerm = SliceFixtures.Collection(session, "base-spire:LongTermDeck");
            var expected = model.Deck.Sum(entry => entry.Count);
            var longMembers = session.State.CollectionMembers(longTerm).ToList();
            Check.Equal(expected, longMembers.Count, "长期牌组张数 = 声明卡组张数");

            // 战斗牌区（抽牌堆/手牌/弃牌堆）里的实例**都不在**长期牌组里。
            var battleInstances = new List<InstanceId>();
            foreach (var pile in new[] { "base-spire:DrawPile", "base-spire:Hand", "base-spire:DiscardPile" })
            {
                battleInstances.AddRange(session.State.CollectionMembers(SliceFixtures.Collection(session, pile)));
            }

            Check.True(battleInstances.Count > 0, "战斗牌区里有牌");
            Check.False(battleInstances.Intersect(longMembers).Any(),
                "临时战斗实例与长期实例不重叠（战斗临时牌不误入长期组）");

            // 逐实例身份：把一张**战斗**牌置为升级态，同定义的**长期**实例仍必须是未升级。
            var battleCard = battleInstances[0];
            var definition = session.State.GetField(battleCard, cardType.FieldIndex("DefinitionId")).AsString();
            session.State.RunAtomic(() => session.State.SetField(battleCard,
                cardType.FieldIndex("Upgraded"), Value.Of(true)));
            var longCounterpart = longMembers.First(held =>
                session.State.GetField(held, cardType.FieldIndex("DefinitionId")).AsString() == definition);
            Check.True(session.State.GetField(battleCard, cardType.FieldIndex("Upgraded")).AsBool(), "战斗牌已升级");
            Check.False(session.State.GetField(longCounterpart, cardType.FieldIndex("Upgraded")).AsBool(),
                "同定义的长期实例**未**被升级（同定义实例分别升级/移除成立）");
        });
        Check.Run("W-05 长期牌组：战斗里**生成**的牌不进长期组（D5③）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var longTerm = SliceFixtures.Collection(session, "base-spire:LongTermDeck");
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var beforeLongTerm = session.State.CollectionMembers(longTerm).ToHashSet();
            var strikesBefore = CountDefinition(session, "base-spire:Hand", SliceManifest.StrikeId);

            var generator = SliceFixtures.PutInHand(session, "base-spire:test-generate", upgraded: false);
            SliceFixtures.PlayAt(session, generator, player);

            var afterLongTerm = session.State.CollectionMembers(longTerm).ToHashSet();
            var strikesAfter = CountDefinition(session, "base-spire:Hand", SliceManifest.StrikeId);
            Check.Equal(strikesBefore + 1, strikesAfter, "打出生成牌后手牌里多了一张打击");

            var generated = session.State.CollectionMembers(hand)
                .Where(held => session.State.GetField(held, cardType.FieldIndex("DefinitionId")).AsString()
                    == SliceManifest.StrikeId)
                .ToList();
            Check.False(generated.Any(held => afterLongTerm.Contains(held)),
                "生成出来的实例**不在**长期牌组里");
            Check.Equal(beforeLongTerm.Count, afterLongTerm.Count, "长期牌组张数不变");
            Check.True(beforeLongTerm.SetEquals(afterLongTerm), "长期牌组成员不变");
        });
        Check.Run("B-07a 整局：程序化路径（无作者文本）不建 Run 实体与集合（包形状不变）", () =>
        {
            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build().Build());
            var rules = builder.Build();
            Check.False(rules.Types.Any(type => type.QualifiedId == "base-spire:Run"), "没有 Run 类型");
            Check.False(rules.Collections.Any(collection => collection.QualifiedId == "base-spire:RunStates"),
                "没有 RunStates 集合");
        });

        Check.Run("W-05 两房间：HP 从整局延续，第二间房重建敌人与牌区 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var run = RunInstance(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);

            // 第一间房：先把玩家压到 41 点（模拟战斗消耗），再清场。
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Hp"),
                Value.Of(41L)));
            ClearRoom(session);

            Check.Equal(SessionStatus.Running, session.Status, "清房后会话继续");
            Check.Equal(41L, session.State.GetField(run, runType.FieldIndex("Hp")).AsInt(),
                "整局 HP 写回 41");

            EnterNextRoom(session);
            Check.Equal(2L, session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(),
                "进入第二间房");
            Check.Equal(41L, SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Hp"), "第二间房的玩家 HP 来自整局（41），不是声明初值 73");
            Check.Equal(0L, SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Block"), "格挡清零");
            Check.Equal(1L, SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Turn"), "第二间房从第 1 回合开始");
            Check.False(session.State.GetField(SliceFixtures.Player(session),
                playerType.FieldIndex("BattleSettled")).AsBool(), "第二间房的结算守卫已重置");
            Check.Equal(5, session.State.CollectionMembers(
                SliceFixtures.Collection(session, "base-spire:Hand")).Count(), "第二间房抽了 5 张起手牌");
            Check.Equal(2, session.State.CollectionMembers(
                SliceFixtures.Collection(session, "base-spire:Monsters")).Count(), "第二间房重建了两个敌人");
            Check.Equal(120L, SliceFixtures.Number(session,
                SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId),
                SliceFixtures.MonsterTypeId, "Hp"), "第二间房的敌人血量回到定义值");
        });

        Check.Run("W-05 两房间：长期牌组里的升级会进入第二间房（同定义实例分别升级）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var run = RunInstance(session);
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var longTerm = SliceFixtures.Collection(session, "base-spire:LongTermDeck");

            ClearRoom(session);
            // **提供方侧**（不是公开动作）在两个**同定义**的长期实例上做不同的升级：
            // 一个是 9 点升级打击，另一个是 12 点（模拟"分别升级"这条验收要素）。
            var longStrikes = session.State.CollectionMembers(longTerm)
                .Where(card => session.State.GetField(card, cardType.FieldIndex("DefinitionId")).AsString()
                    == SliceManifest.StrikeId)
                .Take(2).ToList();
            Check.Equal(2, longStrikes.Count, "长期牌组里至少有两个同定义打击");
            session.State.RunAtomic(() =>
            {
                session.State.SetField(longStrikes[0], cardType.FieldIndex("Upgraded"), Value.Of(true));
                session.State.SetField(longStrikes[0], cardType.FieldIndex("Amount"), Value.Of(9L));
                session.State.SetField(longStrikes[1], cardType.FieldIndex("Upgraded"), Value.Of(true));
                session.State.SetField(longStrikes[1], cardType.FieldIndex("Amount"), Value.Of(12L));
            });

            EnterNextRoom(session);

            var battleAmounts = session.State.CollectionMembers(
                    SliceFixtures.Collection(session, "base-spire:DrawPile"))
                .Concat(session.State.CollectionMembers(SliceFixtures.Collection(session, "base-spire:Hand")))
                .Where(card => session.State.GetField(card, cardType.FieldIndex("DefinitionId")).AsString()
                    == SliceManifest.StrikeId)
                .Select(card => session.State.GetField(card, cardType.FieldIndex("Amount")).AsInt())
                .ToList();
            Check.True(battleAmounts.Contains(9L) && battleAmounts.Contains(12L),
                $"第二间房的战斗牌里应同时出现 9 点与 12 点的打击：{string.Join(",", battleAmounts)}");
            Check.True(battleAmounts.Count(amount => amount == 6L) >= 1,
                "未升级的打击仍是 6 点（两个同定义实例分别升级，互不牵连）");
            Check.Equal(2L, session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(),
                "已经进入第二间房");
        });

        Check.Run("W-05 两房间：临时生成的牌不污染长期组，也不进入第二间房 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var longTerm = SliceFixtures.Collection(session, "base-spire:LongTermDeck");
            var before = session.State.CollectionMembers(longTerm).ToHashSet();

            var generator = SliceFixtures.PutInHand(session, "base-spire:test-generate", upgraded: false);
            SliceFixtures.PlayAt(session, generator, player);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var generated = session.State.CollectionMembers(hand).Where(card =>
                session.State.GetField(card, cardType.FieldIndex("DefinitionId")).AsString()
                == SliceManifest.StrikeId).ToList();
            Check.True(generated.Count > 0, "战斗里确实生成过打击");

            ClearRoom(session);
            EnterNextRoom(session);

            var after = session.State.CollectionMembers(longTerm).ToHashSet();
            Check.True(before.SetEquals(after), "长期牌组成员完全不变（临时牌没有混进去）");
            var battleCards = new List<InstanceId>();
            foreach (var pile in new[] { "base-spire:DrawPile", "base-spire:Hand", "base-spire:DiscardPile" })
            {
                battleCards.AddRange(session.State.CollectionMembers(SliceFixtures.Collection(session, pile)));
            }

            Check.False(generated.Any(card => battleCards.Contains(card)),
                "第一间房生成的牌不会出现在第二间房的牌区（旧房间已清理）");
        });

        Check.Run("W-05 两房间：结算按房间幂等（第二间房可再结算一次）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var run = RunInstance(session);
            var enterRoomId = "base-spire:EnterNextRoom";

            Check.False(IsLegal(session, enterRoomId),
                "房间尚未结算时 EnterNextRoom **不合法**（动作层把关，不靠调用方自觉）");
            ClearRoom(session);
            Check.True(IsLegal(session, enterRoomId), "房间结算后 EnterNextRoom 合法");
            Check.Equal(1L, session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(), "第 1 间房");
            Check.Equal(1L, session.State.GetField(run, runType.FieldIndex("SettledRoom")).AsInt(),
                "第 1 间房已结算");

            EnterNextRoom(session);
            Check.Equal(2L, session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(), "第 2 间房");
            Check.Equal(1L, session.State.GetField(run, runType.FieldIndex("SettledRoom")).AsInt(),
                "第 2 间房尚未结算（房间号已递增 ⇒ 可以再结算一次）");
            Check.False(IsLegal(session, enterRoomId), "第 2 间房尚未结算 ⇒ 不能再进下一间房");

            // 第二间房：把玩家压到 55 点再清场 ⇒ 整局 HP 应更新为 55（按房间键**再结算一次**）。
            var player = SliceFixtures.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("Hp"),
                Value.Of(55L)));
            ClearRoom(session);
            Check.Equal(55L, session.State.GetField(run, runType.FieldIndex("Hp")).AsInt(),
                "第二间房结算后整局 HP = 55（按房间键可再结算一次，而不是被全局布尔挡住）");
            Check.Equal(2L, session.State.GetField(run, runType.FieldIndex("SettledRoom")).AsInt(),
                "SettledRoom = 2");
            Check.Equal(SessionStatus.Terminal, session.Status, "最后一间房清完 ⇒ 整局胜利终局");
            Check.Equal("base-spire:Victory", session.Terminal!.Definition.QualifiedId, "整局胜利");
        });
        Check.Run("W-05 房间奖励：升级长期实例（每间房一次，且升级形态进入下一间房）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var longTerm = SliceFixtures.Collection(session, "base-spire:LongTermDeck");
            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var run = RunInstance(session);
            const string upgradeId = "base-spire:UpgradeLongTermCard";

            Check.False(IsLegal(session, upgradeId), "未清房时不能拿奖励（动作层把关）");
            ClearRoom(session);
            Check.True(IsLegal(session, upgradeId), "清房后可以拿奖励");

            // 选一张长期 `test-selfharm`（升级形态的数值是 3，与普通形态的 1 不同 ⇒ 可判别）。
            var target = session.State.CollectionMembers(longTerm).Single(cardInstance =>
                session.State.GetField(cardInstance, cardType.FieldIndex("DefinitionId")).AsString()
                == "base-spire:test-selfharm");
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == upgradeId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(target)]);

            Check.True(session.State.GetField(target, cardType.FieldIndex("Upgraded")).AsBool(),
                "长期实例被升级（Upgraded=true）");
            Check.Equal(3L, session.State.GetField(target, cardType.FieldIndex("Amount")).AsInt(),
                "升级写入升级变体的数值（3），不是普通形态的 1");
            Check.Equal(1L, session.State.GetField(run, runType.FieldIndex("RewardTaken")).AsInt(),
                "本间房已拿奖励（RewardTaken = 1）");
            Check.False(IsLegal(session, upgradeId), "同一间房**不能**拿第二次奖励");

            // 升级进入第二间房：拷贝出来的战斗牌是升级形态（在清房之前检查——最后一间房清完整局就结束了）。
            EnterNextRoom(session);
            Check.Equal(1L, session.State.GetField(run, runType.FieldIndex("RewardTaken")).AsInt(),
                "奖励键是**按房间**的：RewardTaken=1 而 CurrentRoom=2 ⇒ 第二间房本可再拿一次");
            Check.False(IsLegal(session, upgradeId), "第二间房尚未结算 ⇒ 不能拿奖励");
            var battleCopies = new List<InstanceId>();
            foreach (var pile in new[] { "base-spire:DrawPile", "base-spire:Hand", "base-spire:DiscardPile" })
            {
                battleCopies.AddRange(session.State.CollectionMembers(SliceFixtures.Collection(session, pile)));
            }

            var upgradedCopies = battleCopies.Where(cardInstance =>
                session.State.GetField(cardInstance, cardType.FieldIndex("DefinitionId")).AsString()
                == "base-spire:test-selfharm"
                && session.State.GetField(cardInstance, cardType.FieldIndex("Upgraded")).AsBool()).ToList();
            Check.Equal(1, upgradedCopies.Count, "第二间房里恰好有一张升级过的 test-selfharm");
            Check.Equal(3L, session.State.GetField(upgradedCopies[0], cardType.FieldIndex("Amount")).AsInt(),
                "第二间房的升级拷贝带着升级数值（3）");

            // 最后一间房清完 ⇒ 整局胜利终局（此时不再有任何动作，包括奖励）。
            ClearRoom(session);
            Check.Equal(SessionStatus.Terminal, session.Status, "清完最后一间房 ⇒ 整局终局");
            Check.Equal("base-spire:Victory", session.Terminal!.Definition.QualifiedId, "整局胜利");
            Check.False(IsLegal(session, upgradeId), "整局结束后不再提供奖励动作");
        });

        Check.Run("W-05 房间奖励：跳过奖励不影响进入下一间房（奖励与结算分开）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "run");
            var session = Build(model);
            var runType = session.RuleSet.TypeByQualifiedId("base-spire:Run");
            var run = RunInstance(session);

            ClearRoom(session);
            Check.True(IsLegal(session, "base-spire:EnterNextRoom"),
                "没拿奖励也可以进入下一间房（奖励是可选的）");
            Check.Equal(0L, session.State.GetField(run, runType.FieldIndex("RewardTaken")).AsInt(),
                "RewardTaken 仍为 0");

            EnterNextRoom(session);
            Check.Equal(2L, session.State.GetField(run, runType.FieldIndex("CurrentRoom")).AsInt(),
                "已进入第二间房");
            Check.False(IsLegal(session, "base-spire:EnterNextRoom"), "第二间房尚未结算 ⇒ 不能再进");
            Check.False(IsLegal(session, "base-spire:UpgradeLongTermCard"), "第二间房尚未结算 ⇒ 不能拿奖励");
        });
    }
    private static bool IsLegal(Session session, string actionQualifiedId) =>
        session.LegalActions(SliceManifest.Player)
            .Any(candidate => candidate.Definition.QualifiedId == actionQualifiedId);

    /// <summary>把两个敌人压到 5 点再用打击清场（受控场景的"清房"动作）。</summary>
    private static void ClearRoom(Session session)
    {
        var monsterType = session.RuleSet.TypeByQualifiedId(SliceFixtures.MonsterTypeId);
        var monsters = session.State.CollectionMembers(
            SliceFixtures.Collection(session, "base-spire:Monsters")).ToList();
        session.State.RunAtomic(() =>
        {
            foreach (var monster in monsters)
                session.State.SetField(monster, monsterType.FieldIndex("Hp"), Value.Of(5L));
        });
        foreach (var monster in monsters)
        {
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike, monster);
        }
    }

    /// <summary>执行 `EnterNextRoom`（作者包里只有结算完成的房间才合法）。</summary>
    private static void EnterNextRoom(Session session)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .SingleOrDefault(candidate => candidate.Definition.QualifiedId == "base-spire:EnterNextRoom")
            ?? throw new CheckFailedException("EnterNextRoom 当前不合法（房间尚未结算？）");
        session.Submit(action.Definition, SliceManifest.Player, []);
    }

    /// <summary>某个牌区里指定定义 id 的实例数（按 DefinitionId 字段统计）。</summary>
    private static int CountDefinition(Session session, string areaId, string definition)
    {
        var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
        return session.State.CollectionMembers(SliceFixtures.Collection(session, areaId))
            .Count(held => session.State.GetField(held, cardType.FieldIndex("DefinitionId")).AsString() == definition);
    }

    private static InstanceId RunInstance(Session session)
    {
        var collection = session.RuleSet.Collections.Single(entry => entry.QualifiedId == "base-spire:RunStates");
        return session.State.CollectionMembers(collection.Id).First();
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
