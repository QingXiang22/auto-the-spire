using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第二小批）：**HP 损失（`lose_hp`）原语 + `Beckon` 状态牌**。
///
/// 来源对照：`ContentSource/status-cards.spire:25-60` —— Beckon 可打出（`keywords = {}`）、
/// `cost = 1`、`play = {}`，但在 `turnEndInHand` 里 `loseHp { amount = 6 target = context.owner
/// properties = { move unpowered unblockable } }`。
///
/// 判据：语法/编译/加载/行为四态；**HP 损失不是伤害**——它**绕过格挡**（`unblockable`）、
/// **不走修正链**（`unpowered`）；扣到 0 时的失败判定与 `DamagePlayer` **同一套**（终局次序一致）。
/// </summary>
internal static class SliceFrontendBeckonChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  beckon x2 }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick  damage = multiply-incoming:1.5 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 200  tough-hp = 201  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 200  tough-hp = 201  first-move-ritual = 5  strike-damage = 0
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
        card beckon {
          name = Beckon  kind = Status  cost = 1
          normal   { amount = 6  steps = []  hook turn_end_in_hand { steps = [lose_hp] } }
          upgraded { amount = 6  steps = []  hook turn_end_in_hand { steps = [lose_hp] } }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a HP 损失：`lose_hp` 可在端末 hook 里声明，且它不进消耗区也不消失 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "beckon");
            var beckon = model.Cards.Single(card => card.Id == "base-spire:beckon");
            Check.Equal("Status", beckon.Kind, "kind = Status");
            Check.Sequence([AuthorSteps.LoseHp], beckon.NormalHooks.TurnEndInHand.Select(step => step.Kind),
                "turn_end_in_hand 里是 lose_hp");
            Check.Equal(6, beckon.NormalAmount, "损失数值 6（来源 loseHp.amount）");

            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var beckonCard = SliceFixtures.PutInHand(session, "base-spire:beckon", upgraded: false);
            var hpBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            var blockBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");

            EndTurn(session);

            Check.Equal(hpBefore - 6, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "端末扣 6 点 HP");
            Check.Equal(blockBefore, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "**绕过格挡**：格挡没有被消耗（unblockable）");
            Check.True(session.State.IsMember(SliceFixtures.Collection(session, "base-spire:DiscardPile"), beckonCard),
                "Beckon 没有 ethereal：端末后进弃牌堆");
        });

        Check.Run("B-06a HP 损失：绕过格挡（先给 10 格挡，仍然扣 6）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "beckon");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var type = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            session.State.RunAtomic(() => session.State.SetField(player, type.FieldIndex("Block"), Value.Of(10L)));

            SliceFixtures.PutInHand(session, "base-spire:beckon", upgraded: false);
            var hpBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");

            EndTurn(session);

            // 反证就在这一行：若格挡吸收了这次损失，10 点格挡会吃掉 6 点、HP 一点不掉。
            Check.Equal(hpBefore - 6, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "HP 照扣 6（格挡没有吸收）");
            // 端末之后进入**新的玩家回合**，格挡按规则清零；因此这里的 0 是回合开始的重置，不是被这次损失吃掉的
            // （对比：伤害路径的格挡吸收由既有的伤害检查覆盖）。
            Check.Equal(0L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "新玩家回合开始时格挡清零（与本次 HP 损失无关）");
        });

        Check.Run("B-06a HP 损失：不走修正链（玩家有力量/易伤也仍是 6）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "beckon");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            Attach(session, player, SliceManifest.StrengthId, 5);
            Attach(session, player, SliceManifest.VulnerableId, 3);

            SliceFixtures.PutInHand(session, "base-spire:beckon", upgraded: false);
            var hpBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");

            EndTurn(session);

            Check.Equal(hpBefore - 6, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "力量/易伤都不影响 HP 损失（unpowered）");
        });

        Check.Run("B-06a HP 损失：扣到 0 触发**失败**终局（与伤害同一次序）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "beckon");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var type = session.RuleSet.TypeByQualifiedId(SliceFixtures.PlayerTypeId);
            // 把玩家压到刚好 6 点，再让 Beckon 在端末扣掉它。
            session.State.RunAtomic(() => session.State.SetField(player, type.FieldIndex("Hp"), Value.Of(6L)));

            SliceFixtures.PutInHand(session, "base-spire:beckon", upgraded: false);

            EndTurn(session);

            Check.Equal(0L, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"), "HP 归零");
            Check.Equal(SessionStatus.Terminal, session.Status, "会话进入终局");
            Check.Equal("base-spire:Defeat", session.Terminal!.Definition.QualifiedId, "终局是失败");
            // 终局字段次序取自声明（Defeat 的第一个字段是 Turn），值来自玩家当前回合数——与 DamagePlayer 同源。
            Check.Equal(1L, session.Terminal!.Fields[0].AsInt(), "终局回合 = 玩家回合数");
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

    private static void EndTurn(Session session)
    {
        var endTurn = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
        session.Submit(endTurn.Definition, SliceManifest.Player, []);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId,
                [session.PendingDecision!.Candidates[0]]));
        }
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
