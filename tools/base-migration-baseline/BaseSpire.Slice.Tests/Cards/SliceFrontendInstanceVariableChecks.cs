using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第十五批：**可变实例量**——效果把值写回这张牌、后续效果（乃至下一次打出）读到新值。
/// 这是 B-05a 记的 `variables` + `instance.VariableOffsets` 在切片里的最小等价物。
///
/// 判据：解析→IR→加载→行为；同一**实例**重复打出时读到的是被写回后的值；不同实例互不影响；
/// 冻结作者文本不受影响。
/// </summary>
internal static class SliceFrontendInstanceVariableChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x10  test-charge x2 }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 120  tough-hp = 121  first-move-ritual = 4  strike-damage = 8
          strike-damage-deadly = 10  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 120  tough-hp = 121  first-move-ritual = 5  strike-damage = 1
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
        card test-charge {
          name = TestCharge  kind = Attack  cost = 0
          normal   { amount-source = variable:charge  variable { charge = 2 }  grow = charge  steps = [target, damage] }
          upgraded { amount-source = variable:charge  variable { charge = 5 }  grow = charge  steps = [target, damage] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 实例变量：解析 variable/grow，悬空引用与不可达声明被拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "instance-variable");
            var charge = model.Cards.Single(card => card.Id == "base-spire:test-charge");
            Check.Equal(2, charge.NormalVariables["charge"], "普通形态初值 2");
            Check.Equal(5, charge.UpgradedVariables["charge"], "升级形态初值 5");
            Check.Equal("charge", charge.NormalGrowVariable, "写回目标");
            Check.Equal("variable:charge", charge.NormalAmountSource, "数值来源是实例变量");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("grow = charge", "grow = missing"), "dangling_reference"),
                         (ScenarioText.Replace("variable:charge", "variable:missing"), "dangling_reference"),
                         // 声明了变量却没人读写
                         (ScenarioText.Replace("amount-source = variable:charge  variable { charge = 2 }  grow = charge",
                             "amount = 2  variable { charge = 2 }"), "unreachable_variable"),
                         (ScenarioText.Replace("variable { charge = 2 }", "variable { charge = two }"),
                             "invalid_value"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "instance-variable");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Contains("charge", StringComparison.Ordinal), $"路径：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 实例变量：同一实例重复打出读到被写回后的值 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "instance-variable");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);

            var charge = SliceFixtures.PutInHand(session, "base-spire:test-charge", upgraded: false);
            var type = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var variableField = type.FieldIndex("Variable_charge");
            Check.Equal(2L, session.State.GetField(charge, variableField).AsInt(), "开局该实例的变量＝声明初值 2");

            // 第一次打出：伤害＝2，随后变量写回成 3。
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, charge, target);
            Check.Equal(2L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "第一次打出造成声明的初值 2");
            Check.Equal(3L, session.State.GetField(charge, variableField).AsInt(),
                "打完之后变量被写回成 3（grow = charge）");

            // 把这**同一个实例**放回手牌再打一次：伤害必须是 3。
            // 为了让检查聚焦在"变量写回"上，这里同时清掉本回合的出牌记录（那是回合簿记，不是变量语义）。
            var playedThisTurn = SliceFixtures.Collection(session, "base-spire:PlayedThisTurn");
            session.State.RunAtomic(() =>
            {
                session.State.RemoveFromCollection(discard, charge);
                session.State.AddToCollection(hand, charge);
                session.State.RemoveFromCollection(playedThisTurn, charge);
            });
            var secondBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, charge, target);
            Check.Equal(3L, secondBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "第二次打出读到写回后的 3（不是又按初值 2）");
            Check.Equal(4L, session.State.GetField(charge, variableField).AsInt(), "再写回成 4");
        });

        Check.Run("B-05c 实例变量：不同实例的值互不影响 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "instance-variable");
            var session = Build(model);
            var hand = SliceFixtures.Collection(session, "base-spire:Hand");
            var discard = SliceFixtures.Collection(session, "base-spire:DiscardPile");
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var type = session.RuleSet.TypeByQualifiedId(SliceFixtures.CardTypeId);
            var variableField = type.FieldIndex("Variable_charge");

            // 两张同定义的牌都在手里：只打其中一张。
            var first = SliceFixtures.PutInHand(session, "base-spire:test-charge", upgraded: false);
            // 第二张必须从抽牌堆**移动**过来：PutInHand 会复用已在手里的那一张（同一个实例）。
            var draw = SliceFixtures.Collection(session, "base-spire:DrawPile");
            var second = SliceFixtures.Relocate(session, "base-spire:test-charge", draw, hand);
            Check.True(first != second, "场景里有两张同定义的牌（两个实例）");

            SliceFixtures.PlayAt(session, first, target);

            Check.Equal(3L, session.State.GetField(first, variableField).AsInt(), "被打出的那张写回成 3");
            Check.Equal(2L, session.State.GetField(second, variableField).AsInt(),
                "另一张仍是初值 2（实例变量随实例走）");
            _ = discard;
            _ = hand;
        });

        Check.Run("B-05c 实例变量：冻结作者文本没有实例变量（默认路径不受影响）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            // 切片那 4 张卡没有实例变量（B-06a 起可以有：例如 Rampage 用变量承载每次累加的伤害）。
            Check.True(SliceManifest.Cards.All(entry =>
                {
                    var card = frozen.Cards.Single(candidate => candidate.Id == entry.ContentId);
                    return card.NormalVariables.Count == 0 && card.UpgradedVariables.Count == 0;
                }),
                "切片那 4 张卡没有实例变量");

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
