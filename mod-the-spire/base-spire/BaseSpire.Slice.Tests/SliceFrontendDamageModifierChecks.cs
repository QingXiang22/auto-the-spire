using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第十二批：**能力的伤害修正**作者化（Vulnerable/Weak/Strength 由声明驱动，而不是按 id 比较）。
///
/// 关键判据：作者路径必须**逐值复现已冻结的伤害语义**（R-DAMAGE 证据）：
/// 加性阶段 → 乘性阶段（先攻击者侧 Weak，再承受者侧 Vulnerable）→ 下界 → **唯一一次**截断。
/// 特别是那个反例：5 点基础 + Weak ×0.75 + Vulnerable ×1.5 = 5.625 → 截断成 **5**（旧后端会得到 4）。
/// </summary>
internal static class SliceFrontendDamageModifierChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x6  test-jab }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power weak { name = Weak  role = Weak  effect = "x"  owner-turn-end = tick  damage = multiply-outgoing:0.75 }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick  damage = multiply-incoming:1.5 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength  damage = none }
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
          normal   { amount = 5  steps = [target, damage] }
          upgraded { amount = 5  steps = [target, damage] }
        }
        card test-jab {
          name = TestJab  kind = Attack  cost = 0
          normal   { amount = 0  steps = [target, damage] }
          upgraded { amount = 0  steps = [target, damage] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 伤害修正：解析 damage 声明的四种形态，非法值被拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "damage");
            Check.Equal(PowerDamage.AddPerStack,
                model.Powers.Single(power => power.Id == SliceManifest.StrengthId).DamageModifier, "力量：加性");
            Check.Equal("1",
                model.Powers.Single(power => power.Id == SliceManifest.StrengthId).DamageModifierValue, "每层 1");
            Check.Equal(PowerDamage.MultiplyOutgoing,
                model.Powers.Single(power => power.Id == SliceManifest.WeakId).DamageModifier, "虚弱：攻击者侧乘");
            Check.Equal("0.75",
                model.Powers.Single(power => power.Id == SliceManifest.WeakId).DamageModifierValue, "×0.75");
            Check.Equal("1.5",
                model.Powers.Single(power => power.Id == SliceManifest.VulnerableId).DamageModifierValue, "×1.5");
            Check.Equal(PowerDamage.None,
                model.Powers.Single(power => power.Id == SliceManifest.RitualId).DamageModifier,
                "未声明的能力不参与伤害修正（默认 none）");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("damage = add-per-stack:1", "damage = add-per-stack:-1"),
                             "invalid_value"),
                         (ScenarioText.Replace("damage = multiply-outgoing:0.75", "damage = multiply-outgoing:0.333"),
                             "invalid_value"),
                         (ScenarioText.Replace("damage = multiply-incoming:1.5", "damage = multiplier:1.5"),
                             "invalid_value"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "damage");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Contains("damage", StringComparison.Ordinal), $"路径：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 伤害修正：作者路径复现 R-DAMAGE 反例（5 × 0.75 × 1.5 = 5.625 → 5）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "damage");

            // 场景：攻击者带 Weak，目标带 Vulnerable，基础 5（= 声明的 amount）。
            static long Damage(SliceAuthorModel model, int weakStacks, int vulnerableStacks, int strengthStacks)
            {
                var session = Build(model);
                var attacker = SliceFixtures.Player(session);
                var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                if (weakStacks > 0) Attach(session, attacker, SliceManifest.WeakId, weakStacks);
                if (strengthStacks > 0) Attach(session, attacker, SliceManifest.StrengthId, strengthStacks);
                if (vulnerableStacks > 0) Attach(session, target, SliceManifest.VulnerableId, vulnerableStacks);

                var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
                var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
                SliceFixtures.PlayAt(session, strike, target);
                return before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            }

            Check.Equal(5L, Damage(model, 0, 0, 0), "基础 5");
            Check.Equal(3L, Damage(model, 1, 0, 0), "Weak 单乘：5 × 0.75 = 3.75 → **截断**成 3（不是四舍五入）");
            Check.Equal(7L, Damage(model, 0, 1, 0), "Vulnerable 单乘：5 × 1.5 = 7.5 → 截断 7");
            Check.Equal(5L, Damage(model, 1, 1, 0),
                "**反例**：5 × 0.75 × 1.5 = 5.625 → 只截断一次得 5（中间不取整）");
            Check.Equal(8L, Damage(model, 0, 0, 3), "加性阶段：5 + 3 层力量 = 8");
            Check.Equal(9L, Damage(model, 1, 1, 3), "5 + 3 = 8，8 × 0.75 × 1.5 = 9（恰好整数，无需截断）");
        });

        Check.Run("B-05c 伤害修正：冻结作者文本声明了三个修正，且与程序化路径一致 [真实]", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            Check.Equal(PowerDamage.AddPerStack,
                frozen.Powers.Single(power => power.Id == SliceManifest.StrengthId).DamageModifier, "冻结：力量加性");
            Check.Equal(PowerDamage.MultiplyOutgoing,
                frozen.Powers.Single(power => power.Id == SliceManifest.WeakId).DamageModifier, "冻结：虚弱乘 0.75");
            Check.Equal(PowerDamage.MultiplyIncoming,
                frozen.Powers.Single(power => power.Id == SliceManifest.VulnerableId).DamageModifier,
                "冻结：易伤乘 1.5");

            // 两条路径在同一场景下给出同样的伤害（作者路径按声明，程序化路径按 id）。
            static long DamageWith(Session session, string attackerPower, string targetPower)
            {
                var attacker = SliceFixtures.Player(session);
                var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                if (attackerPower.Length > 0) Attach(session, attacker, attackerPower, 2);
                if (targetPower.Length > 0) Attach(session, target, targetPower, 2);
                var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
                var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
                SliceFixtures.PlayAt(session, strike, target);
                return before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            }

            var authored = Build(frozen);
            var programmatic = Build(null);
            Check.Equal(
                DamageWith(programmatic, SliceManifest.WeakId, SliceManifest.VulnerableId),
                DamageWith(authored, SliceManifest.WeakId, SliceManifest.VulnerableId),
                "Weak + Vulnerable 两条路径同值");
            Check.Equal(
                DamageWith(programmatic, SliceManifest.StrengthId, string.Empty),
                DamageWith(authored, SliceManifest.StrengthId, string.Empty),
                "Strength 两条路径同值");
        });
    }

    private static void Attach(Session session, InstanceId owner, string powerId, int stacks)
    {
        var rules = session.RuleSet;
        var powerType = rules.TypeByQualifiedId("base-spire:Power");
        var relation = rules.Relations.Single(entry => entry.QualifiedId == "base-spire:Powers");
        var power = session.State.CreateInstance(powerType.Id, session.State.ScopeOf(owner));
        session.State.RunAtomic(() =>
        {
            session.State.SetField(power, powerType.FieldIndex("DefinitionId"), Value.Of(powerId));
            session.State.SetField(power, powerType.FieldIndex("Stacks"), Value.Of((long)stacks));
            session.State.SetField(power, powerType.FieldIndex("Ticks"), Value.Of(true));
            session.State.SetField(power, powerType.FieldIndex("SkipNextTick"), Value.Of(true));
            session.State.AddRelation(relation.Id, owner, power);
        });
    }

    private static Session Build(SliceAuthorModel? model)
    {
        var builder = new RuleSetBuilder();
        builder.Add(model is null
            ? CombatSlicePackage.Build().Build()
            : CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: model).Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start();
        return session;
    }
}
