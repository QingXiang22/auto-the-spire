using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第十六小批）：**临时费用修正**（`modify_cost:&lt;n&gt;`）+ **本回合已打出计数**（`played-count:&lt;Kind&gt;`）。
///
/// 来源事实（`Cards/Content/ironclad-cards.spire:167` 的 `Stomp`）：
/// <code>
/// beforeCardPlayed = { whenCardType = attack  effect = { modifySelectedCardCost = { modifier =
///     { costModifier = { amount = -1  operation = add  expires = { turnEnd } } } } } }
/// </code>
/// 即"每打出一张攻击牌，这张牌的费用 −1，直到回合末"。本批先补**能力**并**用夹具牌验证**：
/// <list type="bullet">
/// <item>卡牌字段 `CostModifier`：有效费用 = `max(0, Cost + CostModifier)`（guard 与实际扣费都用它）；</item>
/// <item>步骤 `modify_cost:&lt;n&gt;`：给这张牌自己叠加修正（可负）；</item>
/// <item>数值来源 `played-count:&lt;Kind&gt;`：本回合已打出的该类牌数量；</item>
/// <item>回合末（`expires = { turnEnd }`）把**手牌**上的临时修正清零。</item>
/// </list>
/// **尚未声明 `Stomp` 本身**：它还要两件本批没做的东西——`beforeCardPlayed` hook 上的
/// `whenCardType` 过滤、以及 `onEnterCombat` 里 `selectedCardIsClone` 分支（涉及复制/克隆语义）。
/// 在那两件补齐前声明它会**夸大覆盖**，因此记为明确未完成项（见报告 §5）。
/// </summary>
internal static class SliceFrontendCostModifierChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x8  test-drum x2  test-cheap }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 300  tough-hp = 301  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 300  tough-hp = 301  first-move-ritual = 5  strike-damage = 0
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
        card test-drum {
          name = TestDrum  kind = Skill  cost = 1
          normal   { amount = 0  steps = [modify_cost:-1] }
          upgraded { amount = 0  steps = [modify_cost:-1] }
        }
        card test-cheap {
          name = TestCheap  kind = Skill  cost = 1
          normal   { amount = 0  steps = [modify_cost:-9] }
          upgraded { amount = 0  steps = [modify_cost:-9] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 费用修正：解析 `modify_cost:<n>`（可负）与 `played-count:<Kind>`；越界被拒", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "cost");
            var drum = model.Cards.Single(entry => entry.Id == "base-spire:test-drum");
            Check.Sequence([AuthorSteps.ModifyCost + ":-1"], drum.NormalSteps.Select(step => step.Kind),
                "步骤带负参数");

            // `played-count:<Kind>` 的来源解析
            var scaled = SliceAuthorFrontend.Parse(
                ScenarioText.Replace("amount = 0  steps = [modify_cost:-1]",
                    "amount-source = played-count:Attack  steps = [target, damage]"), "cost");
            Check.Equal(AmountSource.PlayedCountPrefix + "Attack",
                scaled.Cards.Single(entry => entry.Id == "base-spire:test-drum").NormalAmountSource, "已打出计数来源");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("modify_cost:-1", "modify_cost:-10"), "invalid_value"),
                         (ScenarioText.Replace("modify_cost:-1", "modify_cost:x"), "invalid_value"),
                         (ScenarioText.Replace("amount = 0  steps = [modify_cost:-1]",
                             "amount-source = played-count:Spell  steps = [target, damage]"), "invalid_value"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "cost");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）：{failure.Path}");
                }
            }
        });

        Check.Run("B-06a 费用修正：修正**逐实例**且会改变**下一次**实际扣费 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "cost");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);

            var other = SliceFixtures.PutInHand(session, "base-spire:test-cheap", upgraded: false);
            var drum = SliceFixtures.PutInHand(session, "base-spire:test-drum", upgraded: false);
            Check.Equal(1L, session.State.GetField(drum, cardType.FieldIndex("Cost")).AsInt(), "基础费用 1");
            Check.Equal(0L, session.State.GetField(drum, cardType.FieldIndex("CostModifier")).AsInt(), "初始修正 0");

            // 第一次打出：费用按基础值扣（效果步骤在**这次**付款之后才跑，这正是来源里
            // `beforeCardPlayed` 与 `play = {...}` 的区别——本批只补了后者）。
            var energyBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            SliceFixtures.PlayAt(session, drum, player);
            Check.Equal(energyBefore - 1, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "第一次按基础费用 1 扣");
            Check.Equal(-1L, session.State.GetField(drum, cardType.FieldIndex("CostModifier")).AsInt(),
                "效果跑完后它身上留下 −1（回合末才清零）");

            // 同一实例再打一次（夹具把它放回手牌）：这次有效费用 = max(0, 1 + (−1)) = 0。
            Replay(session, drum);
            var modifierAfterReplay = session.State.GetField(drum, cardType.FieldIndex("CostModifier")).AsInt();
            var costAfterReplay = session.State.GetField(drum, cardType.FieldIndex("Cost")).AsInt();
            var beforeSecond = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            Check.Equal(-1L, modifierAfterReplay, $"放回手牌后修正仍在；Cost={costAfterReplay}");
            SliceFixtures.PlayAt(session, drum, player);
            Check.Equal(beforeSecond, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "第二次不再扣费（有效费用 0）");

            // 修正是逐实例的：另一张牌没有被改。
            Check.Equal(0L, session.State.GetField(other, cardType.FieldIndex("CostModifier")).AsInt(),
                "另一张牌不受影响");
        });

        Check.Run("B-06a 费用修正：修正不会让有效费用变负（扣费下限 0）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "cost");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var cheap = SliceFixtures.PutInHand(session, "base-spire:test-cheap", upgraded: false);

            // 直接写入 −9（模拟"被别的效果减费"）：有效费用 = max(0, 1 + (−9)) = 0，能量一点不扣。
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            session.State.RunAtomic(() => session.State.SetField(cheap, cardType.FieldIndex("CostModifier"),
                Value.Of(-9L)));
            var energyBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy");
            SliceFixtures.PlayAt(session, cheap, player);
            Check.Equal(energyBefore, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Energy"),
                "有效费用夹到 0：不扣能量");
        });

        Check.Run("B-06a 费用修正：回合末清零（`expires = { turnEnd }`）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "cost");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var cardType = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);

            // 手里放两张鼓：打一张（自己 −1），另一张留手里看它是否被清零。
            var drum = SliceFixtures.PutInHand(session, "base-spire:test-drum", upgraded: false);
            var handDrum = SliceFixtures.PutInHand(session, "base-spire:test-drum", upgraded: false);
            SliceFixtures.PlayAt(session, drum, player);
            Check.Equal(-1L, session.State.GetField(drum, cardType.FieldIndex("CostModifier")).AsInt(), "打出后 −1");

            // 让留在手里的那张先获得修正（直接写字段，模拟"被别的效果减费"），再端末。
            session.State.RunAtomic(() => session.State.SetField(handDrum, cardType.FieldIndex("CostModifier"),
                Value.Of(-1L)));
            EndTurn(session);
            Check.Equal(0L, session.State.GetField(handDrum, cardType.FieldIndex("CostModifier")).AsInt(),
                "端末清零（来源 expires = { turnEnd }）");
        });

        Check.Run("B-06a 费用修正：`played-count:Attack` 数的是本回合已打出的攻击牌 [真实]", () =>
        {
            var scenario = ScenarioText.Replace(
                """
        card test-drum {
          name = TestDrum  kind = Skill  cost = 1
          normal   { amount = 0  steps = [modify_cost:-1] }
          upgraded { amount = 0  steps = [modify_cost:-1] }
        }
        """,
                """
        card test-drum {
          name = TestDrum  kind = Attack  cost = 0
          normal   { amount-source = played-count:Attack  amount-base = 1  steps = [target, damage] }
          upgraded { amount-source = played-count:Attack  amount-base = 1  steps = [target, damage] }
        }
        """);
            var model = SliceAuthorFrontend.Parse(scenario, "cost");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            // 第一次打出：此时本回合已完成的攻击牌数为 0 → 伤害 = base 1。
            var first = SliceFixtures.PutInHand(session, "base-spire:test-drum", upgraded: false);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, first, target);
            Check.Equal(1L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "0 张已完成 → 1");

            // 第二次打出：上一张已完成 → 伤害 = base 1 + 1×1 = 2。
            var second = SliceFixtures.PutInHand(session, "base-spire:test-drum", upgraded: false);
            var beforeSecond = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, second, target);
            Check.Equal(2L, beforeSecond - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "1 张已完成 → 2（读的是本回合已打出的攻击牌数）");
        });
    }

    /// <summary>把已经打出的同一实例放回手牌，并清掉本回合的出牌记录（回合簿记，不是费用语义）。</summary>
    private static void Replay(Session session, InstanceId card)
    {
        var hand = SliceFixtures.Collection(session, "base-spire:Hand");
        var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
        var playedThisTurn = SliceFixtures.Collection(session, "base-spire:PlayedThisTurn");
        session.State.RunAtomic(() =>
        {
            if (session.State.IsMember(discard, card))
            {
                session.State.RemoveFromCollection(discard, card);
            }

            session.State.AddToCollection(hand, card);
            session.State.RemoveFromCollection(playedThisTurn, card);
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
