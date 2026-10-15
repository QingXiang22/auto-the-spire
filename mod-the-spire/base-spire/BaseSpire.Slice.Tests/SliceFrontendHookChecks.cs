using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第二批：hook 的作者语法与运行时——`on_exhaust`（打出即消耗）与 `turn_end_in_hand`（回合结束时仍在手牌）。
///
/// 三条判据（对应开工清单 §14 B-05c 行）：
/// <list type="number">
/// <item>**升级空 hooks 不隐式继承**：升级变体没声明 hook 就是没有，哪怕普通变体声明了；</item>
/// <item>`on_exhaust` 接受自身与外部消耗，不要求 `exhaust_self`；</item>
/// <item>hook 的可执行步骤面与出牌时**共用同一套降级**，且超出运行时能力的步骤在编译期拒绝。</item>
/// </list>
/// </summary>
internal static class SliceFrontendHookChecks
{
    /// <summary>测试场景：一张消耗牌（带 on_exhaust）、一张留手牌（带 turn_end_in_hand）、一张对照牌（无 hook）。</summary>
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck {
          strike-ironclad x4
          defend-ironclad x4
          test-ember
          test-sentinel
        }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x" }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 44  tough-hp = 45  first-move-ritual = 4  strike-damage = 8
          strike-damage-deadly = 10  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 51  tough-hp = 52  first-move-ritual = 5  strike-damage = 1
          strike-damage-deadly = 3  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 1
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 9  steps = [target, damage] }
        }
        card defend-ironclad {
          name = DefendIronclad  kind = Skill  cost = 1
          normal   { amount = 5  steps = [block] }
          upgraded { amount = 8  steps = [block] }
        }
        card test-ember {
          name = TestEmber  kind = Attack  cost = 0
          normal {
            amount = 3
            steps = [target, damage, exhaust_self]
            hook on_exhaust { steps = [block] }
          }
          upgraded {
            amount = 5
            steps = [target, damage, exhaust_self]
          }
        }
        card test-sentinel {
          name = TestSentinel  kind = Skill  cost = 0
          normal {
            amount = 2
            steps = [block]
            hook turn_end_in_hand { steps = [damage_all] }
          }
          upgraded {
            amount = 4
            steps = [block]
            hook turn_end_in_hand { steps = [damage_all] }
          }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c hooks: 语法接受外部消耗 hook，并拒绝未知/超能力的 hook 声明（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "hooks");
            var ember = model.Cards.Single(card => card.Id == "base-spire:test-ember");
            Check.Sequence([AuthorSteps.Block], ember.NormalHooks.OnExhaust.Select(s => s.Kind), "普通变体声明了 on_exhaust");
            Check.True(ember.UpgradedHooks.IsEmpty, "升级变体没有声明任何 hook（不隐式继承）");
            Check.True(ember.NormalExhaustsSelf, "ember 会消耗自己");

            var sentinel = model.Cards.Single(card => card.Id == "base-spire:test-sentinel");
            Check.Sequence([AuthorSteps.DamageAll], sentinel.NormalHooks.TurnEndInHand.Select(s => s.Kind), "普通变体声明了 turn_end_in_hand");
            Check.Sequence([AuthorSteps.DamageAll], sentinel.UpgradedHooks.TurnEndInHand.Select(s => s.Kind), "升级变体也声明了");

            var externallyExhaustible = SliceAuthorFrontend.Parse(
                ScenarioText.Replace("steps = [target, damage, exhaust_self]", "steps = [target, damage]"), "hooks");
            Check.False(externallyExhaustible.Cards.Single(c => c.Id == "base-spire:test-ember").NormalExhaustsSelf,
                "外部消耗钩子不要求自己消耗");

            // 未知 hook 名。
            try
            {
                SliceAuthorFrontend.Parse(ScenarioText.Replace("hook turn_end_in_hand", "hook on_draw"), "hooks");
                Check.True(false, "未知 hook 必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("unknown_hook", failure.Code, "错误码");
            }

            // hook 里超出运行时能力的步骤（选择/消耗自己）。
            try
            {
                SliceAuthorFrontend.Parse(
                    ScenarioText.Replace("hook on_exhaust { steps = [block] }",
                        "hook on_exhaust { steps = [top_from_discard] }"), "hooks");
                Check.True(false, "hook 里的超能力步骤必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("unsupported_capability", failure.Code, "错误码");
            }
        });

        Check.Run("B-05c hooks: 普通变体打出即消耗并触发 on_exhaust [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "hooks");
            var session = Build(model);
            var card = SliceFixtures.PutInHand(session, "base-spire:test-ember", upgraded: false);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var before = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            var blockBefore = SliceFixtures.Number(session, SliceFixtures.Player(session), SliceFixtures.PlayerTypeId,
                "Block");

            SliceFixtures.PlayAt(session, card, front);

            Check.True(session.State.IsMember(SliceFixtures.Collection(session, "base-spire:ExhaustPile"), card),
                "消耗牌进消耗区");
            Check.False(session.State.IsMember(SliceFixtures.Collection(session, "base-spire:DiscardPile"), card),
                "消耗牌不进弃牌堆");
            Check.Equal(before - 3, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "damage 步骤按声明数值生效");
            Check.Equal(blockBefore + 3, SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Block"), "on_exhaust 生效（数值来自同一张卡的 amount）");
        });

        Check.Run("B-05c hooks: 升级变体未声明 hook 时不触发（**不隐式继承**）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "hooks");
            var session = Build(model);
            var card = SliceFixtures.PutInHand(session, "base-spire:test-ember", upgraded: true, (5, 0));
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var before = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            var blockBefore = SliceFixtures.Number(session, SliceFixtures.Player(session), SliceFixtures.PlayerTypeId,
                "Block");

            SliceFixtures.PlayAt(session, card, front);

            Check.True(session.State.IsMember(SliceFixtures.Collection(session, "base-spire:ExhaustPile"), card),
                "升级形态仍然消耗自己");
            Check.Equal(before - 5, SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "升级数值 5 生效");
            Check.Equal(blockBefore, SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Block"),
                "升级变体没有 on_exhaust，**不得**继承普通变体的 hook");
        });

        Check.Run("B-05c hooks: turn_end_in_hand 在回合结束按声明数值触发，然后牌照常进弃牌堆 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "hooks");
            foreach (var (upgraded, amount) in new[] { (false, 2L), (true, 4L) })
            {
                var session = Build(model);
                var label = upgraded ? "升级" : "普通";
                var values = upgraded ? ((int, int)?)(4, 0) : null;
                var card = SliceFixtures.PutInHand(session, "base-spire:test-sentinel", upgraded, values);
                var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
                var frontBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
                var backBefore = SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");

                var endTurn = session.LegalActions(SliceManifest.Player)
                    .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
                session.Submit(endTurn.Definition, SliceManifest.Player, []);
                while (session.Status == SessionStatus.AwaitingDecision)
                {
                    session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId,
                        [session.PendingDecision!.Candidates[0]]));
                }

                // 伤害是耐久的（格挡会在下回合开始被清掉，因此不适合当观测量）。
                Check.Equal(frontBefore - amount,
                    SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                    $"{label} sentinel 的 turn_end_in_hand 对前排造成声明的 {amount}");
                Check.Equal(backBefore - amount,
                    SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                    $"{label} sentinel 的 turn_end_in_hand 对后排造成声明的 {amount}");
                Check.False(session.State.IsMember(SliceFixtures.Collection(session, "base-spire:Hand"), card),
                    $"{label} 触发后牌照常离开手牌（进弃牌堆）");
                // 不能断言"在弃牌堆"：结束回合的抽牌可能把弃牌堆洗回抽牌堆。可判别的是**没有进消耗区**。
                Check.False(session.State.IsMember(SliceFixtures.Collection(session, "base-spire:ExhaustPile"), card),
                    $"{label} 没有 exhaust_self 步骤，因此不得进消耗区");
            }

            // 本 hook 的**不隐式继承**：只给普通变体声明 hook，升级变体必须保持"没有"。
            const string normalOnly = """
                package base-spire { version = combat-slice-v1 }
                deck { test-echo }
                card test-echo {
                  name = TestEcho  kind = Skill  cost = 0
                  normal   { amount = 2  steps = [block]  hook turn_end_in_hand { steps = [damage_all] } }
                  upgraded { amount = 4  steps = [block] }
                }
                """;
            var dropped = SliceAuthorFrontend.Parse(normalOnly, "hooks");
            var echoed = dropped.Cards.Single(card => card.Id == "base-spire:test-echo");
            Check.Sequence([AuthorSteps.DamageAll], echoed.NormalHooks.TurnEndInHand.Select(s => s.Kind), "普通变体仍有 hook");
            Check.True(echoed.UpgradedHooks.IsEmpty,
                "升级变体没声明 hook 就是没有（前端不得把普通变体的 hook 抄过去）");
        });
        Check.Run("B-05c hooks: 冻结作者文本没有被本批改动", () =>
        {
            var model = SliceAuthorFrontend.Load();
            Check.Equal(SliceManifest.Cards.Count + SliceFixtures.AuthoredBeyondSlice.Count, model.Cards.Count,
                "冻结文本的卡牌数（切片 + B-06a 已迁内容）");
            // 只对**切片的那 4 张卡**断言"没有 hook"：B-06a 引入的 Beckon 在来源里就带 turnEndInHand hook。
            foreach (var entry in SliceManifest.Cards)
            {
                var card = model.Cards.Single(candidate => candidate.Id == entry.ContentId);
                Check.True(card.NormalHooks.IsEmpty && card.UpgradedHooks.IsEmpty,
                    $"{card.Id} 在冻结文本里没有 hook");
                Check.False(card.NormalExhaustsSelf, $"{card.Id} 不消耗自己");
            }

            // 程序化路径（默认）不受影响：battle 仍能建立并跑通（由既有 25 项检查覆盖）。
            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build().Build());
            var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
            session.Start();
            Check.Equal(SessionStatus.Running, session.Status, "程序化路径的受控开局仍然成立");
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
