using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第六批：**批次**（`damage x3` / `block x2`）。
///
/// 判据：批次是内容声明（降级成通用 IR 的 `Repeat`），不是运行时巧合；批次只允许出现在可重复的效果步骤上；
/// 批次不消费随机（随机只由 `damage_random` 消费）；冻结作者文本不受影响。
/// </summary>
internal static class SliceFrontendBatchChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck {
          strike-ironclad x4
          test-flurry
          test-bulwark
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
        card test-flurry {
          name = TestFlurry  kind = Attack  cost = 0
          normal   { amount = 2  steps = [target, damage x3] }
          upgraded { amount = 3  steps = [target, damage x3] }
        }
        card test-bulwark {
          name = TestBulwark  kind = Skill  cost = 0
          normal   { amount = 1  steps = [block x4] }
          upgraded { amount = 2  steps = [block x3] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 批次：`xN` 被解析为批次，不可重复的步骤写批次会被拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "batch");
            var flurry = model.Cards.Single(card => card.Id == "base-spire:test-flurry");
            Check.Sequence(["target", "damage"], flurry.StepKinds, "步骤名");
            Check.Sequence([1, 3], flurry.NormalSteps.Select(step => step.Count), "批次");
            Check.Equal("damage x3", flurry.NormalSteps[1].ToString(), "步骤文本");

            var bulwark = model.Cards.Single(card => card.Id == "base-spire:test-bulwark");
            Check.Sequence([4], bulwark.NormalSteps.Select(step => step.Count), "普通变体批次");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("steps = [target, damage x3]", "steps = [target x2, damage]"),
                             "unsupported_combination"),
                         (ScenarioText.Replace("steps = [block x4]", "steps = [block x0]"), "invalid_value"),
                         (ScenarioText.Replace("steps = [block x4]", "steps = [block x]"), "invalid_value"),
                         (ScenarioText.Replace("steps = [block x4]", "steps = [block x2 y3]"), "invalid_value"),
                         (ScenarioText.Replace("steps = [block x4]", "steps = [damage_all x3]"),
                             "unsupported_combination"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "batch");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Contains("steps[", StringComparison.Ordinal),
                        $"路径应指向步骤：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 批次：`damage x3` 在同一目标上打三次、数值取声明值 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "batch");
            foreach (var (upgraded, amount, values) in new[]
                     {
                         (false, 2L, ((int, int)?)null),
                         (true, 3L, (3, 0)),
                     })
            {
                var session = Build(model);
                var label = upgraded ? "升级" : "普通";
                var card = SliceFixtures.PutInHand(session, "base-spire:test-flurry", upgraded, values);
                var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                var before = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");

                SliceFixtures.PlayAt(session, card, front);

                Check.Equal(before - amount * 3,
                    SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                    $"{label}：3 次 × {amount} 全部命中同一目标");
            }

            // 对照：同一张牌去掉批次（等差值）只打一次 —— 证明差额来自批次而不是别处。
            var singleHit = SliceAuthorFrontend.Parse(
                ScenarioText.Replace("steps = [target, damage x3]", "steps = [target, damage]"), "batch");
            var plain = Build(singleHit);
            var plainCard = SliceFixtures.PutInHand(plain, "base-spire:test-flurry", upgraded: false);
            var plainFront = SliceFixtures.Cultist(plain, SliceManifest.CalcifiedCultistId);
            var plainBefore = SliceFixtures.Number(plain, plainFront, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(plain, plainCard, plainFront);
            Check.Equal(plainBefore - 2, SliceFixtures.Number(plain, plainFront, SliceFixtures.MonsterTypeId, "Hp"),
                "无批次时只打一次（差值是批次造成的）");
        });

        Check.Run("B-05c 批次：`block x4` 加四次格挡，且批次不消费随机流 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "batch");
            var session = Build(model);
            var card = SliceFixtures.PutInHand(session, "base-spire:test-bulwark", upgraded: false);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var blockBefore = SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Block");
            var drawsBefore = session.Random.TotalDraws;

            SliceFixtures.PlayAt(session, card, front);

            Check.Equal(blockBefore + 4, SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Block"), "4 次 × 1 格挡");
            Check.Equal(drawsBefore, session.Random.TotalDraws, "批次本身不消费任何随机（Repeat 不是随机点）");
            Check.False(session.Random.Streams.Contains(SliceManifest.RandomTargetStream),
                "批次不创建随机目标流");
        });

        Check.Run("B-05c 批次：hook 里也能写批次（`on_exhaust` 的 damage_all/block）[真实]", () =>
        {
            const string hookBatch = """
                package base-spire { version = combat-slice-v1 }
                deck { test-ember }
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
                card test-ember {
                  name = TestEmber  kind = Attack  cost = 0
                  normal {
                    amount = 2
                    steps = [target, damage, exhaust_self]
                    hook on_exhaust { steps = [block x3] }
                  }
                  upgraded {
                    amount = 2
                    steps = [target, damage, exhaust_self]
                    hook on_exhaust { steps = [block x3] }
                  }
                }
                """;
            var model = SliceAuthorFrontend.Parse(hookBatch, "hook-batch");
            var ember = model.Cards.Single(card => card.Id == "base-spire:test-ember");
            Check.Sequence([3], ember.NormalHooks.OnExhaust.Select(step => step.Count), "hook 里的批次");

            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false,
                authorModel: model).Build());
            var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
            session.Start();

            var card = SliceFixtures.PutInHand(session, "base-spire:test-ember", upgraded: false);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var blockBefore = SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Block");
            SliceFixtures.PlayAt(session, card, front);
            Check.Equal(blockBefore + 6, SliceFixtures.Number(session, SliceFixtures.Player(session),
                SliceFixtures.PlayerTypeId, "Block"), "hook 里的 block x3 按 amount 2 加三次");
        });

        Check.Run("B-05c 批次：冻结作者文本没有批次（默认路径不受影响）", () =>
        {
            var model = SliceAuthorFrontend.Load();
            // 只对切片那 4 张卡断言（B-06a 的 TwinStrike 来源就是 `repeat count = 2`，批次不是 1）。
            Check.True(SliceManifest.Cards.All(entry =>
                    model.Cards.Single(card => card.Id == entry.ContentId).NormalSteps.All(step => step.Count == 1)),
                "切片那 4 张卡的每个步骤批次都是 1");
            // 只对切片那 4 张卡断言（B-06a 的 Beckon 在来源里带 turnEndInHand hook）。
            Check.True(SliceManifest.Cards.All(entry =>
                    model.Cards.Single(card => card.Id == entry.ContentId).NormalHooks.IsEmpty),
                "切片那 4 张卡在冻结文本里没有 hook");
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
