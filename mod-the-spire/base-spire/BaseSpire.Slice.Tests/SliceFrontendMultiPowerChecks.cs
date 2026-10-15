using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第十一小批）：**多能力同牌**（同一张牌、同一个能力、两个收受者、两种层数）+ `FightMe`。
///
/// 具体反例（来源 `ContentSource/ironclad-cards.spire:3321`）：
/// <code>
/// play = { repeat { count = 2  effect = { attack { damage = 5 } } }
///          applyPower { power = Strength  stacks = 3  target = context.owner }   // 升级 4
///          applyPower { power = Strength  stacks = 1  target = context.target } }
/// </code>
/// 作者模型原先每个变体只有**一组** `power`/`stacks`，无法表达"给自己 3 层、给目标 1 层"。
///
/// 采用的表示（沿用既有的"字面量参数步骤"写法，不新增一套语法）：
/// <list type="bullet">
/// <item>`apply_power_self:&lt;n&gt;` / `apply_power:&lt;n&gt;` / `apply_power_all:&lt;n&gt;` —— 参数是**层数**；</item>
/// <item>能力 id 仍是**卡牌级** `power`（与 `stacks` 一样，只有"没带字面量的施加步骤"才要求卡牌级 `stacks`）。</item>
/// </list>
/// 步骤次序按来源（先两段伤害，再给自己、再给目标）：因此这张牌自己的伤害**不**吃它给的 Strength。
/// </summary>
internal static class SliceFrontendMultiPowerChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  fight-me }
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
        card fight-me {
          name = FightMe  kind = Attack  cost = 2
          source = base-spire/ContentSource/ironclad-cards.spire
          line = 3321
          normal   { amount = 5  power = strength  steps = [target, damage x2, apply_power_self:3, apply_power:1] }
          upgraded { amount = 6  power = strength  steps = [target, damage x2, apply_power_self:4, apply_power:1] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 多能力同牌：解析逐步骤层数；没有字面量时才要求卡牌级 stacks", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "fightme");
            var card = model.Cards.Single(entry => entry.Id == "base-spire:fight-me");
            Check.Equal(3321, card.SourceLine, "来源行");
            Check.Sequence(
                [AuthorSteps.Target, AuthorSteps.Damage, AuthorSteps.ApplyPowerSelf + ":3", AuthorSteps.ApplyPower + ":1"],
                card.NormalSteps.Select(step => step.Kind), "步骤与次序（先伤害，再给自己、再给目标）");
            Check.Equal(2, card.NormalSteps.Single(step => step.Kind == AuthorSteps.Damage).Count, "伤害两段");
            Check.Equal(5, card.NormalAmount, "普通每段 5");
            Check.Equal(6, card.UpgradedAmount, "升级每段 6");
            Check.Equal(0, card.NormalStacks, "卡牌级 stacks 未声明（全部施加步骤都带了字面量）");
            Check.Equal(SliceManifest.StrengthId, card.PowerId, "能力 id 仍是卡牌级");

            // 升级形态的逐步骤层数
            Check.Equal(AuthorSteps.ApplyPowerSelf + ":4", card.UpgradedSteps[2].Kind, "升级给自己 4 层");
            Check.Equal(AuthorSteps.ApplyPower + ":1", card.UpgradedSteps[3].Kind, "给目标仍是 1 层");

            // 没有字面量、也没有卡牌级 stacks ⇒ 必须拒绝（不能猜 0 层）
            try
            {
                SliceAuthorFrontend.Parse(
                    ScenarioText.Replace("apply_power_self:3", "apply_power_self"), "fightme");
                Check.True(false, "缺 stacks 必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("missing_key", failure.Code, "错误码");
            }

            // 越过 999 的字面量层数必须拒绝
            try
            {
                SliceAuthorFrontend.Parse(
                    ScenarioText.Replace("apply_power_self:3", "apply_power_self:1000"), "fightme");
                Check.True(false, "越界层数必须被拒绝");
            }
            catch (SliceAuthorError failure)
            {
                Check.Equal("invalid_value", failure.Code, "错误码");
            }
        });

        Check.Run("B-06a 多能力同牌：FightMe 打两段 5 点，给自己 +3、给目标 +1 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "fightme");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            var card = SliceFixtures.PutInHand(session, "base-spire:fight-me", upgraded: false);
            SliceFixtures.PlayAt(session, card, target);

            Check.Equal(10L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "两段 5 = 10（这张牌自己的伤害不吃它给的 Strength）");
            Check.Equal(3L, Stacks(session, player, SliceManifest.StrengthId), "玩家 +3 力量");
            Check.Equal(1L, Stacks(session, target, SliceManifest.StrengthId), "目标 +1 力量（**不同收受者**）");

            // 随后一张打击：玩家 3 力量 → 6 + 3 = 9；目标身上的力量不影响我方伤害。
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            var second = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(9L, second - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "下一张打击 6 + 3 = 9");
        });

        Check.Run("B-06a 多能力同牌：升级形态两段 6、给自己 +4、目标 +1 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "fightme");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            var card = SliceFixtures.PutInHand(session, "base-spire:fight-me", upgraded: true,
                (Amount: 6, PowerStacks: 0));
            SliceFixtures.PlayAt(session, card, target);

            Check.Equal(12L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "两段 6 = 12");
            Check.Equal(4L, Stacks(session, player, SliceManifest.StrengthId), "玩家 +4 力量");
            Check.Equal(1L, Stacks(session, target, SliceManifest.StrengthId), "目标 +1 力量");
        });

        Check.Run("B-06a 多能力同牌：冻结作者文本声明了 FightMe（来源行与两形态逐步骤层数）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var card = frozen.Cards.Single(entry => entry.Id == "base-spire:fight-me");
            Check.Equal(3321, card.SourceLine, "来源行");
            Check.Equal(5, card.NormalAmount, "普通伤害 5");
            Check.Equal(6, card.UpgradedAmount, "升级伤害 6");
            Check.Equal(AuthorSteps.ApplyPowerSelf + ":3", card.NormalSteps[2].Kind, "普通给自己 3");
            Check.Equal(AuthorSteps.ApplyPowerSelf + ":4", card.UpgradedSteps[2].Kind, "升级给自己 4");
            Check.Equal(AuthorSteps.ApplyPower + ":1", card.UpgradedSteps[3].Kind, "两形态给目标都是 1");
        });
    }

    private static long Stacks(Session session, InstanceId owner, string powerId)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        foreach (var power in session.State.RelationTargets(relation.Id, owner))
        {
            if (session.State.GetField(power, powerType.FieldIndex("DefinitionId")).AsString() == powerId)
            {
                return session.State.GetField(power, powerType.FieldIndex("Stacks")).AsInt();
            }
        }

        return 0;
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
