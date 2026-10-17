using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第十四批：**状态来源的数值**（切片里"实例变量"的最小形态）——
/// 效果的数值不再是声明常量，而是在**打出那一刻**从当前状态读取。
///
/// 作者文本：`amount-source = literal | hand-count | discard-count | draw-count`。
/// 判据：解析→IR→加载→行为；同一张牌在不同手牌数下打出必须造成**不同**伤害（证明求值在打出时，
/// 而不是在构筑时把数字烤进去）；与固定 amount 不能共存；冻结作者文本不受影响。
/// </summary>
internal static class SliceFrontendAmountSourceChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x10  test-tally  test-reclaim }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 90  tough-hp = 91  first-move-ritual = 4  strike-damage = 8
          strike-damage-deadly = 10  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 90  tough-hp = 91  first-move-ritual = 5  strike-damage = 1
          strike-damage-deadly = 3  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 1  steps = [target, damage] }
          upgraded { amount = 1  steps = [target, damage] }
        }
        card test-tally {
          name = TestTally  kind = Attack  cost = 0
          normal   { amount-source = hand-count   steps = [target, damage] }
          upgraded { amount-source = hand-count   steps = [target, damage] }
        }
        card test-reclaim {
          name = TestReclaim  kind = Attack  cost = 0
          normal   { amount-source = discard-count  steps = [target, damage] }
          upgraded { amount-source = discard-count  steps = [target, damage] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 状态来源：解析 amount-source，与固定 amount 冲突时拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "amount-source");
            var tally = model.Cards.Single(card => card.Id == "base-spire:test-tally");
            Check.Equal(AmountSource.HandCount, tally.NormalAmountSource, "手牌数来源");
            Check.Equal(0, tally.NormalAmount, "状态来源时声明数值为 0（不参与）");
            Check.Equal(AmountSource.Literal,
                model.Cards.Single(card => card.Id == SliceManifest.StrikeId).NormalAmountSource,
                "未声明时是 literal");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("amount-source = hand-count   steps",
                             "amount = 3  amount-source = hand-count   steps"), "unsupported_combination"),
                         (ScenarioText.Replace("amount-source = hand-count", "amount-source = deck-count"),
                             "invalid_value"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "amount-source");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Contains("test-tally", StringComparison.Ordinal),
                        $"路径：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 状态来源：`hand-count` 在打出那一刻读取手牌数（同一张牌两种结果）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "amount-source");

            static long Damage(SliceAuthorModel model, int extraCards)
            {
                var session = Build(model);
                var hand = SliceFixtures.Collection(session, "base-spire:Hand");
                var draw = SliceFixtures.Collection(session, "base-spire:DrawPile");
                var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

                // 用手牌堆→手牌的移动来改变手牌数（PutInHand 会复用已在手里的牌，不能用来加牌）。
                for (var index = 0; index < extraCards; index++)
                {
                    SliceFixtures.Relocate(session, SliceManifest.StrikeId, draw, hand);
                }

                var tally = SliceFixtures.PutInHand(session, "base-spire:test-tally", upgraded: false);
                var handBefore = session.State.CollectionMembers(hand).Count;
                var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
                SliceFixtures.PlayAt(session, tally, target);
                var dealt = before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
                // 这张牌在效果解析**之前**就已经离开手牌，因此伤害 = 打出前手牌数 − 1。
                Check.Equal((long)(handBefore - 1), dealt, $"伤害等于打出那一刻的手牌数（{handBefore - 1}）");
                return dealt;
            }

            var small = Damage(model, 0);
            var large = Damage(model, 4);
            Check.True(large > small, $"手牌更多时伤害更大（{small} → {large}）");
            Check.Equal(small + 4, large, "多出的 4 张手牌正好多造成 4 点");
            Check.True(small >= 1, $"伤害至少 1（实际 {small}）");
        });

        Check.Run("B-05c 状态来源：`discard-count` 读弃牌堆，冻结作者文本不受影响", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "amount-source");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            // 先制造 3 张弃牌。
            for (var index = 0; index < 3; index++)
            {
                SliceFixtures.Relocate(session, SliceManifest.StrikeId, hand, discard);
            }

            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            var reclaim = SliceFixtures.PutInHand(session, "base-spire:test-reclaim", upgraded: false);
            SliceFixtures.PlayAt(session, reclaim, target);
            Check.Equal(3L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "伤害等于打出时的弃牌堆张数（3）");

            // 切片那 4 张卡没有状态来源（B-06a 起可以有：例如 Rampage 的来源是实例变量）。
            var frozen = SliceAuthorFrontend.Load();
            Check.True(SliceManifest.Cards.All(entry =>
                {
                    var card = frozen.Cards.Single(candidate => candidate.Id == entry.ContentId);
                    return card.NormalAmountSource == AmountSource.Literal
                        && card.UpgradedAmountSource == AmountSource.Literal;
                }),
                "切片那 4 张卡全部是字面量来源");
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
