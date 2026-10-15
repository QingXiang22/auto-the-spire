using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第十五小批）：**缩放数值来源**（`base + perCount × 来源计数`）+ `AshenStrike`。
///
/// 来源（`ContentSource/ironclad-cards.spire:3057`）：
/// <code>
/// damageValue      = scaled { base = 6  perCount = 3  count = { pileCount { pile = exhaust } } }
/// upgradedDamageValue = scaled { base = 6  perCount = 4  count = { pileCount { pile = exhaust } } }
/// </code>
/// 即"伤害 = 6 + 3 ×（消耗区牌数）"，升级只改 perCount。为此新增两件事：
/// <list type="bullet">
/// <item>数值来源 `exhaust-count`（消耗区牌数；来源里 `pile = exhaust` 是显式的）；</item>
/// <item>变体键 `amount-base` / `amount-per-count`（默认 0 / 1 ⇒ 既有内容**行为不变**）。</item>
/// </list>
///
/// 另记一个**未实现**的来源形态：`PerfectedStrike` 用 `count = { cardsWithTag { tag = Strike } }`，
/// 而 `cardsWithTag` **没有牌区限定**（源码里只有这一处用法，`sts2/` 也没有实现），
/// 因此"数哪些牌区"无法确定——按纪律**不猜、不声明**（见报告）。
/// </summary>
internal static class SliceFrontendScaledAmountChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x10  ashen-strike  test-exhauster x3 }
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
        card test-exhauster {
          name = TestExhauster  kind = Skill  cost = 0
          normal   { amount = 0  steps = [exhaust_self] }
          upgraded { amount = 0  steps = [exhaust_self] }
        }
        card ashen-strike {
          name = AshenStrike  kind = Attack  cost = 1
          source = base-spire/ContentSource/ironclad-cards.spire
          line = 3057
          normal   { amount-source = exhaust-count  amount-base = 6  amount-per-count = 3  steps = [target, damage] }
          upgraded { amount-source = exhaust-count  amount-base = 6  amount-per-count = 4  steps = [target, damage] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 缩放来源：解析 `exhaust-count` 与 `amount-base`/`amount-per-count`；越界被拒", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "scaled");
            var card = model.Cards.Single(entry => entry.Id == "base-spire:ashen-strike");
            Check.Equal(3057, card.SourceLine, "来源行");
            Check.Equal(AmountSource.ExhaustCount, card.NormalAmountSource, "来源是消耗区牌数");
            Check.Equal(6, card.NormalAmountBase, "base 6");
            Check.Equal(3, card.NormalAmountPerCount, "perCount 3");
            Check.Equal(4, card.UpgradedAmountPerCount, "升级 perCount 4");
            Check.Equal(0, card.NormalAmount, "带来源时不再有字面量 amount");
            Check.Sequence([AuthorSteps.Target, AuthorSteps.Damage], card.NormalSteps.Select(step => step.Kind),
                "步骤照常");

            // 既有内容的行为不变：没有声明缩放参数的牌，perCount 默认 1、base 默认 0。
            var frozen = SliceAuthorFrontend.Load();
            Check.True(SliceManifest.Cards.All(entry =>
                {
                    var manifestCard = frozen.Cards.Single(candidate => candidate.Id == entry.ContentId);
                    return manifestCard.NormalAmountBase == 0 && manifestCard.NormalAmountPerCount == 1;
                }),
                "切片那 4 张卡的缩放参数是默认值（行为不变）");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("amount-base = 6", "amount-base = -1"), "invalid_value"),
                         (ScenarioText.Replace("amount-per-count = 3", "amount-per-count = 1000"), "invalid_value"),
                         (ScenarioText.Replace("exhaust-count", "exhaust-count:Attack"), "invalid_value"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "scaled");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）：{failure.Path}");
                }
            }
        });

        Check.Run("B-06a 缩放来源：AshenStrike 伤害 = 6 + 3 ×（消耗区牌数）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "scaled");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            // 消耗区为空：伤害就是 base 6。
            var card = SliceFixtures.PutInHand(session, "base-spire:ashen-strike", upgraded: false);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, card, target);
            Check.Equal(6L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "消耗区 0 张 → 6");
        });

        Check.Run("B-06a 缩放来源：消耗区有 2 张时伤害 12（6 + 3×2）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "scaled");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var player = SliceFixtures.Player(session);

            // 先消耗两张牌进消耗区。
            for (var i = 0; i < 2; i++)
            {
                var exhauster = SliceFixtures.PutInHand(session, "base-spire:test-exhauster", upgraded: false);
                SliceFixtures.PlayAt(session, exhauster, player);
            }

            var card = SliceFixtures.PutInHand(session, "base-spire:ashen-strike", upgraded: false);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, card, target);
            Check.Equal(12L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "消耗区 2 张 → 6 + 3×2 = 12");
        });

        Check.Run("B-06a 缩放来源：升级只改 perCount（2 张时 6 + 4×2 = 14）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "scaled");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var player = SliceFixtures.Player(session);

            for (var i = 0; i < 2; i++)
            {
                var exhauster = SliceFixtures.PutInHand(session, "base-spire:test-exhauster", upgraded: false);
                SliceFixtures.PlayAt(session, exhauster, player);
            }

            var card = SliceFixtures.PutInHand(session, "base-spire:ashen-strike", upgraded: true,
                (Amount: 0, PowerStacks: 0));
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, card, target);
            Check.Equal(14L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "升级：6 + 4×2 = 14");
        });

        Check.Run("B-06a 缩放来源：冻结作者文本声明了 AshenStrike 的两形态缩放参数", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var card = frozen.Cards.Single(entry => entry.Id == "base-spire:ashen-strike");
            Check.Equal(3057, card.SourceLine, "来源行");
            Check.Equal(6, card.NormalAmountBase, "普通 base 6");
            Check.Equal(3, card.NormalAmountPerCount, "普通 perCount 3");
            Check.Equal(4, card.UpgradedAmountPerCount, "升级 perCount 4");
            Check.Equal(AmountSource.ExhaustCount, card.UpgradedAmountSource, "两形态都数消耗区");
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
