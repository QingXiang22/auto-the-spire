using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第九小批）：**能力的声明式触发点** + `FeelNoPain`。
///
/// 来源（两处，缺一不可）：
/// <list type="bullet">
/// <item>`Powers/Content/ironclad-powers.spire:28` —— `power FeelNoPain { trigger = afterCardExhausted
///   effect = gainBlock(1, perPowerStack=FeelNoPain) }`；</item>
/// <item>`Cards/Content/ironclad-cards.spire:1976` —— 卡 `FeelNoPain`：`cost = 1`、给自己上 3 层（升级 4 层）。</item>
/// </list>
///
/// 本批的做法：**先补触发点**（`trigger` / `when-card-type` / `trigger-effect` 三个声明键，落在切片**真的会触发**
/// 的时点：一张牌被消耗之后），再声明"用得到它"的内容。未实现的触发点会被显式拒绝——
/// 不允许"声明了但永远不触发"。
/// </summary>
internal static class SliceFrontendPowerTriggerChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  feel-no-pain  test-exhauster }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        power feel-no-pain {
          name = FeelNoPain
          role = FeelNoPain
          effect = "after a card is exhausted: gain 1 Block per stack"
          source = base-spire/Powers/Content/ironclad-powers.spire
          line = 28
          trigger = after_card_exhausted
          trigger-effect = gain-block-per-stack:1
        }
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
        card feel-no-pain {
          name = FeelNoPain  kind = Power  cost = 1
          source = base-spire/Cards/Content/ironclad-cards.spire
          line = 1976
          normal   { amount = 0  power = feel-no-pain  stacks = 3  steps = [apply_power_self] }
          upgraded { amount = 0  power = feel-no-pain  stacks = 4  steps = [apply_power_self] }
        }
        card test-exhauster {
          name = TestExhauster  kind = Skill  cost = 0
          normal   { amount = 0  steps = [exhaust_self] }
          upgraded { amount = 0  steps = [exhaust_self] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 触发点：解析 trigger/when-card-type/trigger-effect；未实现的时点被拒绝", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "trigger");
            var power = model.Powers.Single(entry => entry.Id == "base-spire:feel-no-pain");
            Check.Equal(AuthorPowerTrigger.AfterCardExhausted, power.Trigger, "触发点");
            Check.Equal(string.Empty, power.WhenCardType, "不限牌类");
            Check.Equal(PowerTriggerEffect.GainBlockPerStackPrefix + "1", power.TriggerEffect, "触发效果");
            Check.Equal(28, power.SourceLine, "能力来源行");

            var card = model.Cards.Single(entry => entry.Id == "base-spire:feel-no-pain");
            Check.Equal(1976, card.SourceLine, "卡来源行");
            Check.Equal(3, card.NormalStacks, "普通 3 层");
            Check.Equal(4, card.UpgradedStacks, "升级 4 层");

            foreach (var (text, code) in new[]
                     {
                         // 未实现的触发点必须被拒绝（不能"声明了但永远不触发"）
                         (ScenarioText.Replace("trigger = after_card_exhausted", "trigger = after_card_drawn"),
                             "invalid_value"),
                         // trigger-effect 单独出现没有意义（缺前置 ⇒ missing_prerequisite）。
                         // 注意：把 key 注释掉而不是删整行——原始字符串字面量里的换行是 CRLF，
                         // 用 "\n" 去匹配整行会静默失配（变异没生效，检查就成了假绿）。
                         (ScenarioText.Replace("trigger = after_card_exhausted", "# trigger removed"),
                             "missing_prerequisite"),
                         // 触发效果词表之外
                         (ScenarioText.Replace("gain-block-per-stack:1", "draw-a-card"), "invalid_value"),
                         // 层数越界
                         (ScenarioText.Replace("gain-block-per-stack:1", "gain-block-per-stack:1000"), "invalid_value"),
                         // 没有 trigger 却写 when-card-type
                         (ScenarioText.Replace("trigger = after_card_exhausted", "# trigger removed")
                             .Replace("trigger-effect = gain-block-per-stack:1", "when-card-type = Attack"),
                             "missing_prerequisite"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "trigger");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）：{failure.Path}");
                }
            }
        });

        Check.Run("B-06a 触发点：有 FeelNoPain 时消耗一张牌 → 每层 +1 格挡 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "trigger");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var power = SliceFixtures.PutInHand(session, "base-spire:feel-no-pain", upgraded: false);
            SliceFixtures.PlayAt(session, power, player);

            var blockBefore = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
            var exhauster = SliceFixtures.PutInHand(session, "base-spire:test-exhauster", upgraded: false);
            SliceFixtures.PlayAt(session, exhauster, player);

            Check.Equal(blockBefore + 3, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "消耗一张牌 → +3 格挡（3 层 × 每层 1 点）");
        });

        Check.Run("B-06a 触发点：没有该能力时不触发；不消耗的牌也不触发 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "trigger");

            // 没有 FeelNoPain：消耗一张牌不加格挡。
            var without = Build(model);
            var playerWithout = SliceFixtures.Player(without);
            var exhauster = SliceFixtures.PutInHand(without, "base-spire:test-exhauster", upgraded: false);
            var before = SliceFixtures.Number(without, playerWithout, SliceFixtures.PlayerTypeId, "Block");
            SliceFixtures.PlayAt(without, exhauster, playerWithout);
            Check.Equal(before, SliceFixtures.Number(without, playerWithout, SliceFixtures.PlayerTypeId, "Block"),
                "没有 FeelNoPain 时不触发");

            // 有 FeelNoPain：打一张**不消耗**的牌不加格挡。
            var with = Build(model);
            var playerWith = SliceFixtures.Player(with);
            var power = SliceFixtures.PutInHand(with, "base-spire:feel-no-pain", upgraded: false);
            SliceFixtures.PlayAt(with, power, playerWith);
            var blockAfterPower = SliceFixtures.Number(with, playerWith, SliceFixtures.PlayerTypeId, "Block");
            var strike = SliceFixtures.PutInHand(with, SliceManifest.StrikeId, upgraded: false);
            var target = SliceFixtures.Cultist(with, SliceManifest.CalcifiedCultistId);
            SliceFixtures.PlayAt(with, strike, target);
            Check.Equal(blockAfterPower,
                SliceFixtures.Number(with, playerWith, SliceFixtures.PlayerTypeId, "Block"),
                "打出不消耗的牌不触发");
        });

        Check.Run("B-06a 触发点：升级形态（4 层）给 +4；端末因 ethereal 被消耗也触发 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "trigger");
            var session = Build(model);
            var player = SliceFixtures.Player(session);
            var power = SliceFixtures.PutInHand(session, "base-spire:feel-no-pain", upgraded: true,
                (Amount: 0, PowerStacks: 4));
            SliceFixtures.PlayAt(session, power, player);

            var before = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block");
            var exhauster = SliceFixtures.PutInHand(session, "base-spire:test-exhauster", upgraded: false);
            SliceFixtures.PlayAt(session, exhauster, player);
            Check.Equal(before + 4, SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Block"),
                "4 层 → +4 格挡");
        });

        Check.Run("B-06a 触发点：冻结作者文本声明了 FeelNoPain 的触发点与触发效果", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var power = frozen.Powers.Single(entry => entry.Id == "base-spire:feel-no-pain");
            Check.Equal(AuthorPowerTrigger.AfterCardExhausted, power.Trigger, "触发点");
            Check.Equal(PowerTriggerEffect.GainBlockPerStackPrefix + "1", power.TriggerEffect, "每层 1 点格挡");
            Check.Equal(28, power.SourceLine, "能力来源行");
            var card = frozen.Cards.Single(entry => entry.Id == "base-spire:feel-no-pain");
            Check.Equal(3, card.NormalStacks, "普通 3 层");
            Check.Equal(4, card.UpgradedStacks, "升级 4 层");
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
