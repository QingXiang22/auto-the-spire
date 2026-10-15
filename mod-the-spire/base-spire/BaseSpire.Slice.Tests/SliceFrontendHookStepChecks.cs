using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第十七批：**hook 里更广的步骤面**——`apply_power` 现在可以在 hook 里用（目标＝玩家自己）。
///
/// 其余步骤仍然拒绝，但**逐条给出理由**（是设计边界，不是"未支持"）：
/// hook 的时点不允许挂起决策、也不改变实例身份。
/// </summary>
internal static class SliceFrontendHookStepChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x10  test-ember }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick  damage = multiply-incoming:1.5 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 120  tough-hp = 121  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 120  tough-hp = 121  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 3  steps = [target, damage] }
          upgraded { amount = 3  steps = [target, damage] }
        }
        card test-ember {
          name = TestEmber  kind = Attack  cost = 0
          normal {
            amount = 1
            power = strength
            stacks = 2
            steps = [target, damage, exhaust_self]
            hook on_exhaust { steps = [apply_power] }
          }
          upgraded {
            amount = 1
            power = strength
            stacks = 2
            steps = [target, damage, exhaust_self]
            hook on_exhaust { steps = [apply_power] }
          }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c hook 步骤面：apply_power 可在 hook 里用；其余步骤被拒时给出**具体理由**", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "hook-steps");
            var ember = model.Cards.Single(card => card.Id == "base-spire:test-ember");
            Check.Sequence([AuthorSteps.ApplyPower], ember.NormalHooks.OnExhaust.Select(step => step.Kind),
                "on_exhaust 里的 apply_power 被接受");
            Check.Sequence([AuthorSteps.DamageAll, AuthorSteps.Block, AuthorSteps.ApplyPower, AuthorSteps.ApplyPowerAll,
                    AuthorSteps.ApplyPowerSelf, AuthorSteps.LoseHp],
                AuthorSteps.HookSteps, "hook 允许面（B-06a 起含 lose_hp 与两种全体/自身施加）");

            // 被拒的步骤必须给出**各自**的理由（不是笼统的"未支持"）。
            (string Step, string ReasonFragment)[] rejected =
            [
                ("target", "不允许挂起决策"),
                ("damage", "没有可选的敌人目标"),
                ("top_from_discard", "不允许挂起决策"),
                ("exhaust_self", "已经**被消耗之后"),
                ("upgrade_hand", "不做手牌升级"),
                ("generate:strike-ironclad", "不生成新实例"),
                ("becomes:strike-ironclad", "不改变实例身份"),
            ];

            foreach (var (step, fragment) in rejected)
            {
                var text = ScenarioText.Replace("hook on_exhaust { steps = [apply_power] }",
                    $"hook on_exhaust {{ steps = [{step}] }}");
                try
                {
                    SliceAuthorFrontend.Parse(text, "hook-steps");
                    Check.True(false, $"hook 里的 '{step}' 必须被拒绝");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal("unsupported_capability", failure.Code, $"错误码（{step}）");
                    Check.True(failure.Message.Contains(fragment, StringComparison.Ordinal),
                        $"'{step}' 的理由里应包含「{fragment}」，实际：{failure.Message}");
                    Check.True(failure.Path.Contains("on_exhaust", StringComparison.Ordinal),
                        $"路径应指向该 hook：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c hook 步骤面：hook 里的 apply_power 把能力加在**玩家自己**身上 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "hook-steps");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var player = SliceFixtures.Player(session);

            var ember = SliceFixtures.PutInHand(session, "base-spire:test-ember", upgraded: false);
            SliceFixtures.PlayAt(session, ember, target);

            Check.Equal(2L, Stacks(session, player, SliceManifest.StrengthId),
                "消耗触发 on_exhaust：玩家自己获得 2 层力量");
            Check.Equal(0L, Stacks(session, target, SliceManifest.StrengthId),
                "敌人身上没有这份力量（目标是 self，不是当前打出的目标）");

            // 力量立刻生效：下一张打击造成 3 + 2 = 5。
            var hpBefore = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(5L, hpBefore - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "力量立刻参与伤害计算（3 + 2）");
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
