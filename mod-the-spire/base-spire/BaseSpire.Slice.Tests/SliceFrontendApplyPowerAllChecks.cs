using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-06a 基础内容批（第五小批）：**对全体施加能力（`apply_power_all`）** + `Thunderclap`。
///
/// 来源：`ContentSource/ironclad-cards.spire:2535` ——
/// `attack { damage = 4 (升级 7) target = allEnemies }` + `applyPower { power = Vulnerable stacks = 1
/// target = allEnemies }`。注意**伤害数与层数是两个独立事实**（4/7 与 1/1），因此层数走 `stacks` 字段。
///
/// 纪律与 `damage_all` 对齐：给全体施加**不提问**（没有可挂起的选择），且 `apply_power` 与 `apply_power_all`
/// 互斥（同一能力落在单个还是全体身上必须唯一）。
/// </summary>
internal static class SliceFrontendApplyPowerAllChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x12  thunderclap }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick  damage = multiply-incoming:1.5 }
        power weak { name = Weak  role = Weak  effect = "x"  owner-turn-end = tick  damage = multiply-outgoing:0.75 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 200  tough-hp = 201  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 200  tough-hp = 201  first-move-ritual = 5  strike-damage = 0
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
        card thunderclap {
          name = Thunderclap  kind = Attack  cost = 1
          source = base-spire/ContentSource/ironclad-cards.spire
          line = 2535
          normal   { amount = 4  power = vulnerable  stacks = 1  steps = [damage_all, apply_power_all] }
          upgraded { amount = 7  power = vulnerable  stacks = 1  steps = [damage_all, apply_power_all] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-06a 对全体施加能力：解析、与 apply_power 互斥、缺 power/stacks 被拒", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "aoe-debuff");
            var thunder = model.Cards.Single(card => card.Id == "base-spire:thunderclap");
            Check.Equal(2535, thunder.SourceLine, "来源行");
            Check.Sequence([AuthorSteps.DamageAll, AuthorSteps.ApplyPowerAll],
                thunder.NormalSteps.Select(step => step.Kind), "先打全体再给全体上状态（来源次序）");
            Check.Equal(4, thunder.NormalAmount, "普通伤害 4");
            Check.Equal(7, thunder.UpgradedAmount, "升级伤害 7");
            Check.Equal(1, thunder.NormalStacks, "层数 1（与伤害数独立）");
            Check.Equal(SliceManifest.VulnerableId, thunder.PowerId, "能力 id");

            foreach (var (text, code) in new[]
                     {
                         // 单个目标与全体同时出现：目标域不明确
                         (ScenarioText.Replace("steps = [damage_all, apply_power_all]",
                             "steps = [target, damage, apply_power, apply_power_all]"), "unsupported_combination"),
                         // apply_power_all 也要给出 power
                         (ScenarioText.Replace("power = vulnerable  stacks = 1  steps = [damage_all, apply_power_all]",
                             "stacks = 1  steps = [damage_all, apply_power_all]"), "missing_key"),
                         // 也要给出 stacks
                         (ScenarioText.Replace("power = vulnerable  stacks = 1  steps = [damage_all, apply_power_all]",
                             "power = vulnerable  steps = [damage_all, apply_power_all]"), "missing_key"),
                         // 批次：给全员各一次，再批次只表示层数翻倍
                         (ScenarioText.Replace("apply_power_all]", "apply_power_all x2]"), "unsupported_combination"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "aoe-debuff");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                }
            }
        });

        Check.Run("B-06a 对全体施加能力：Thunderclap 给**每个**敌人各上 1 层易伤，且不提问 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "aoe-debuff");
            var session = Build(model);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var frontBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            var backBefore = SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp");

            var card = SliceFixtures.PutInHand(session, "base-spire:thunderclap", upgraded: false);
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == SliceFixtures.PlayCardActionId);
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
            Check.Equal(SessionStatus.Running, session.Status, "给全体上状态**不请求目标选择**");

            Check.Equal(4L, frontBefore - SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "前排 -4");
            Check.Equal(4L, backBefore - SliceFixtures.Number(session, back, SliceFixtures.MonsterTypeId, "Hp"),
                "后排 -4");
            Check.Equal(1L, Stacks(session, front, SliceManifest.VulnerableId), "前排获得 1 层易伤");
            Check.Equal(1L, Stacks(session, back, SliceManifest.VulnerableId), "后排也获得 1 层易伤");

            // 易伤随后真的生效：再打一张打击，伤害 6 → 9（×1.5）。
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            var hpBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");
            SliceFixtures.PlayAt(session, strike, front);
            Check.Equal(9L, hpBefore - SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "易伤让打击从 6 变 9");
        });

        Check.Run("B-06a 对全体施加能力：升级形态打 7 并仍然各上 1 层 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "aoe-debuff");
            var session = Build(model);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var frontBefore = SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp");

            var card = SliceFixtures.PutInHand(session, "base-spire:thunderclap", upgraded: true,
                (Amount: 7, PowerStacks: 1));
            SliceFixtures.PlayAt(session, card, front);

            Check.Equal(7L, frontBefore - SliceFixtures.Number(session, front, SliceFixtures.MonsterTypeId, "Hp"),
                "升级打 7");
            Check.Equal(1L, Stacks(session, back, SliceManifest.VulnerableId), "后排仍然各上 1 层");
        });

        Check.Run("B-06a 对全体施加能力：冻结作者文本声明了 Thunderclap（来源行与两形态）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var thunder = frozen.Cards.Single(card => card.Id == "base-spire:thunderclap");
            Check.Equal(2535, thunder.SourceLine, "来源行");
            Check.Equal(AuthorSteps.ApplyPowerAll, thunder.UpgradedSteps.Last().Kind, "升级形态同样是给全体");
            Check.Equal(7, thunder.UpgradedAmount, "升级伤害 7");
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
