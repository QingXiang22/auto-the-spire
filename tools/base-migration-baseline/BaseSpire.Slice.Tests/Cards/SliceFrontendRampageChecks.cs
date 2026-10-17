using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第六小批）：**按量累加的实例变量（`grow-amount`）** + `Rampage`。
///
/// 来源：`Cards/Content/ironclad-cards.spire:3852`
/// <list type="bullet">
/// <item>`variables { variable ExtraDamage  default = 0  preserveOnDowngrade = true }`；</item>
/// <item>`scaledAttack { base = 9  perCount = 1  count = { cardVariable ExtraDamage } }`；</item>
/// <item>`modifyCardVariable { variable = ExtraDamage  amount = 5 }`（升级形态 **9**）。</item>
/// </list>
///
/// 编码决定（已在报告里写明）：把 `base = 9` 落到变量的**初值**，把 `amount = 5/9` 落到 `grow-amount`。
/// "9 + 1×计数（计数每次 +5）"与"初值 9、每次 +5"在可观察行为上等价。
/// 来源的 `preserveOnDowngrade` 在切片里**没有降级路径**，因此**未建模**（不是"支持了"）。
/// </summary>
internal static class SliceFrontendRampageChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  rampage }
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
          upgraded { amount = 9  steps = [target, damage] }
        }
        card rampage {
          name = Rampage  kind = Attack  cost = 1
          source = base-spire/Cards/Content/ironclad-cards.spire
          line = 3852
          normal   { amount-source = variable:extra-damage  variable { extra-damage = 9 }  grow = extra-damage  grow-amount = 5   steps = [target, damage] }
          upgraded { amount-source = variable:extra-damage  variable { extra-damage = 9 }  grow = extra-damage  grow-amount = 9   steps = [target, damage] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 累加变量：解析 `grow-amount`；缺 grow / 越界 / 非法值被拒绝", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "rampage");
            var rampage = model.Cards.Single(card => card.Id == "base-spire:rampage");
            Check.Equal(3852, rampage.SourceLine, "来源行");
            Check.Equal(AmountSource.VariablePrefix + "extra-damage", rampage.NormalAmountSource, "数值来源");
            Check.Equal("extra-damage", rampage.NormalGrowVariable, "写回的变量");
            Check.Equal(5, rampage.NormalGrowAmount, "普通每次 +5（来源 modifyCardVariable.amount）");
            Check.Equal(9, rampage.UpgradedGrowAmount, "升级每次 +9");
            Check.Equal(9, rampage.NormalVariables["extra-damage"], "初值 = 来源的 base 9");
            Check.Equal(0, rampage.NormalAmount, "带来源时不再有字面量 amount");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("grow = extra-damage  grow-amount = 5", "grow-amount = 5"),
                             "missing_prerequisite"),
                         (ScenarioText.Replace("grow-amount = 5", "grow-amount = ten"), "invalid_value"),
                         (ScenarioText.Replace("grow-amount = 5", "grow-amount = 1000"), "invalid_value"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "rampage");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                }
            }
        });

        Check.Run("B-06a 累加变量：Rampage 第一次 9、第二次 14（读的是写回后的值）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "rampage");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var rampage = SliceFixtures.PutInHand(session, "base-spire:rampage", upgraded: false);

            var first = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, rampage, target);
            Check.Equal(9L, first - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "第一次造成初值 9");

            Replay(session, rampage);

            var second = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, rampage, target);
            Check.Equal(14L, second - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "第二次造成 9 + 5 = 14（变量按 grow-amount 写回）");
        });

        Check.Run("B-06a 累加变量：升级形态每次 +9（第二次 18）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "rampage");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var rampage = SliceFixtures.PutInHand(session, "base-spire:rampage", upgraded: true,
                (Amount: 9, PowerStacks: 0));

            var first = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, rampage, target);
            Check.Equal(9L, first - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "第一次仍是 9（初值不随升级变）");

            Replay(session, rampage);

            var second = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, rampage, target);
            Check.Equal(18L, second - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "升级第二次 9 + 9 = 18");
        });

        Check.Run("B-06a 累加变量：冻结作者文本声明了 Rampage（来源行、来源与写回量）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var rampage = frozen.Cards.Single(card => card.Id == "base-spire:rampage");
            Check.Equal(3852, rampage.SourceLine, "来源行");
            Check.Equal(5, rampage.NormalGrowAmount, "普通 +5");
            Check.Equal(9, rampage.UpgradedGrowAmount, "升级 +9");
            Check.Equal(AmountSource.VariablePrefix + "extra-damage", rampage.UpgradedAmountSource, "升级同样读变量");
        });
    }

    /// <summary>把已经打出的同一张实例放回手牌，并清掉本回合的出牌记录（回合簿记，不是变量语义）。</summary>
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
