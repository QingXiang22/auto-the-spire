using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第十六批：**生成 / 升级 / 变形**（都在战斗中改动"实例身份或实例字段"）。
///
/// 作者文本：
/// <list type="bullet">
/// <item>`generate:&lt;card-id&gt;`：在手牌里造一个该定义的**新实例**（普通形态、含实例变量初值）；</item>
/// <item>`upgrade_hand`：把手牌里每张牌置为升级形态并换成它自己的升级数值；</item>
/// <item>`becomes:&lt;card-id&gt;`：这张牌打出后**变成**另一个定义（普通形态、数值重置）。</item>
/// </list>
/// 判据：解析→IR→加载→行为；生成的实例是**新实例**（不是复用）；升级不改未升级之外的字段语义；
/// 变形后定义与数值都换成目标卡；冻结作者文本不受影响。
/// </summary>
internal static class SliceFrontendGenerationChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x8  test-forge  test-drill  test-shift }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 90  tough-hp = 91  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 90  tough-hp = 91  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 3  steps = [target, damage] }
          upgraded { amount = 7  steps = [target, damage] }
        }
        card test-forge {
          name = TestForge  kind = Skill  cost = 0
          normal   { amount = 0  steps = [generate:strike-ironclad] }
          upgraded { amount = 0  steps = [generate:strike-ironclad] }
        }
        card test-drill {
          name = TestDrill  kind = Skill  cost = 0
          normal   { amount = 0  steps = [upgrade_hand] }
          upgraded { amount = 0  steps = [upgrade_hand] }
        }
        card test-shift {
          name = TestShift  kind = Attack  cost = 0
          normal   { amount = 1  steps = [target, damage, becomes:strike-ironclad] }
          upgraded { amount = 1  steps = [target, damage, becomes:strike-ironclad] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 生成/升级/变形：解析三种步骤，缺参数的写法被拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "generation");
            var forge = model.Cards.Single(card => card.Id == "base-spire:test-forge");
            Check.Sequence(["generate:base-spire:strike-ironclad"], forge.StepKinds, "生成步骤（参数按包命名空间限定）");
            Check.Sequence([AuthorSteps.UpgradeHand],
                model.Cards.Single(card => card.Id == "base-spire:test-drill").StepKinds, "升级步骤");
            Check.Sequence(["target", "damage", "becomes:base-spire:strike-ironclad"],
                model.Cards.Single(card => card.Id == "base-spire:test-shift").StepKinds, "变形步骤");

            foreach (var text in new[]
                     {
                         ScenarioText.Replace("generate:strike-ironclad", "generate:"),
                         ScenarioText.Replace("becomes:strike-ironclad", "becomes:"),
                         ScenarioText.Replace("upgrade_hand", "upgrade_x"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "generation");
                    Check.True(false, "缺参数/未知步骤必须被拒绝");
                }
                catch (SliceAuthorError failure)
                {
                    Check.True(failure.Code is "invalid_value" or "unknown_step",
                        $"错误码：{failure.Code}");
                    Check.True(failure.Path.Contains("steps[", StringComparison.Ordinal), $"路径：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 生成：手牌里多出一个**新实例**，它可以被正常打出 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "generation");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var type = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);

            var before = session.State.CollectionMembers(hand)
                .Where(card => session.State.GetField(card, type.FieldIndex("DefinitionId")).AsString()
                    == SliceManifest.StrikeId)
                .ToList();
            var forge = SliceFixtures.PutInHand(session, "base-spire:test-forge", upgraded: false);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            SliceFixtures.PlayAt(session, forge, target);

            var after = session.State.CollectionMembers(hand)
                .Where(card => session.State.GetField(card, type.FieldIndex("DefinitionId")).AsString()
                    == SliceManifest.StrikeId)
                .ToList();
            Check.Equal(before.Count + 1, after.Count,
                $"手牌里的该定义多了一张；before={before.Count} after={after.Count} " +
                $"hand=[{string.Join(",", session.State.CollectionMembers(hand).Select(card =>
                    session.State.GetField(card, type.FieldIndex("DefinitionId")).AsString()))}]");
            Check.False(before.Contains(after[^1]) && after[^1] == before[^1],
                "多出来的是新实例（不是把原来那张挪过来）");

            // 新实例可以被正常打出：造成它声明的 3 点。
            var generated = after.Except(before).Single();
            var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, generated, target);
            Check.Equal(3L, hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "生成的牌按普通形态的声明造成 3 点");
        });

        Check.Run("B-05c 升级：手牌里的牌全部变成升级形态并换成升级数值 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "generation");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var type = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            var drill = SliceFixtures.PutInHand(session, "base-spire:test-drill", upgraded: false);
            var heldBefore = session.State.CollectionMembers(hand).ToList();
            Check.True(heldBefore.All(card =>
                    session.State.GetField(card, type.FieldIndex("Upgraded")).AsBool() == false),
                "升级前手里的牌都不是升级形态");

            SliceFixtures.PlayAt(session, drill, target);

            var held = session.State.CollectionMembers(hand).ToList();
            Check.True(held.Count > 0, "升级后手里还有牌（新回合抽到的）");
            Check.True(held.All(card => session.State.GetField(card, type.FieldIndex("Upgraded")).AsBool()),
                "手牌里的牌全部是升级形态");

            // 数值也换成升级值：拿一张打击，打出后应造成升级的 7 点。
            var upgradedStrike = session.State.CollectionMembers(hand).First(card =>
                session.State.GetField(card, type.FieldIndex("DefinitionId")).AsString() == SliceManifest.StrikeId);
            var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, upgradedStrike, target);
            Check.Equal(7L, hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "升级后的打击造成 7 点（数值也跟着换了）");
        });

        Check.Run("B-05c 变形：打出后这张牌变成目标定义、数值重置为普通形态 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "generation");
            var session = Build(model);
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
            var type = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            var shift = SliceFixtures.PutInHand(session, "base-spire:test-shift", upgraded: false);
            var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, shift, target);

            Check.Equal(1L, hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "变形前先按自己的声明造成 1 点");
            Check.True(session.State.IsMember(discard, shift), "打完进入弃牌堆（仍是同一个实例）");
            Check.Equal(SliceManifest.StrikeId,
                session.State.GetField(shift, type.FieldIndex("DefinitionId")).AsString(), "实例定义已变成打击");
            Check.Equal(3L, session.State.GetField(shift, type.FieldIndex("Amount")).AsInt(),
                "数值重置为目标定义的普通形态值 3");
            Check.False(session.State.GetField(shift, type.FieldIndex("Upgraded")).AsBool(), "形态回到普通");
        });

        Check.Run("B-05c 生成/升级/变形：冻结作者文本没有这三种步骤（默认路径不受影响）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            Check.False(frozen.Cards.Any(card => card.StepKinds.Any(step =>
                    step == AuthorSteps.UpgradeHand
                    || step.StartsWith(AuthorSteps.GeneratePrefix, StringComparison.Ordinal)
                    || step.StartsWith(AuthorSteps.BecomesPrefix, StringComparison.Ordinal))),
                "冻结文本里没有生成/升级/变形步骤");
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
