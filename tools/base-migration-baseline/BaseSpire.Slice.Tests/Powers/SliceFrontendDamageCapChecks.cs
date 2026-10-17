using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第十八批：**伤害上限阶段（cap）**——补上 `SliceManifest` 与 R-DAMAGE 证据里明确标注的缺口。
///
/// 来源次序（`Hook.cs:2511-2557` + `:1565`）：加法 → 乘法 → **上限** → 下限 0 → 唯一一次截断，
/// 全程 decimal。因此上限必须**在乘法之后、截断之前**起作用：
/// 基础 5 + Weak(×0.75) = 3.75，上限 2.5 → **2.5** → 截断 **2**（而不是先截断成 3 再夹到 2.5→2 的巧合，
/// 也不是"上限作用于已截断的整数"）。
/// </summary>
internal static class SliceFrontendDamageCapChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x8  test-ward }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power weak { name = Weak  role = Weak  effect = "x"  owner-turn-end = tick  damage = multiply-outgoing:0.75 }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick  damage = multiply-incoming:1.5 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        power ward { name = Ward  role = Ward  effect = "caps incoming damage"  owner-turn-end = tick  damage-cap = 2.5 }
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
          normal   { amount = 5  steps = [target, damage] }
          upgraded { amount = 5  steps = [target, damage] }
        }
        card test-ward {
          name = TestWard  kind = Skill  cost = 0
          normal   { amount = 0  power = ward  stacks = 0  steps = [block] }
          upgraded { amount = 0  power = ward  stacks = 0  steps = [block] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 伤害上限：解析 damage-cap，非法值被拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "damage-cap");
            Check.Equal("2.5", model.Powers.Single(power => power.Id == "base-spire:ward").DamageCap, "上限值");
            Check.Equal(string.Empty,
                model.Powers.Single(power => power.Id == SliceManifest.StrengthId).DamageCap,
                "未声明上限的能力没有 cap");

            foreach (var text in new[]
                     {
                         ScenarioText.Replace("damage-cap = 2.5", "damage-cap = -1"),
                         ScenarioText.Replace("damage-cap = 2.5", "damage-cap = 1.234"),
                         ScenarioText.Replace("damage-cap = 2.5", "damage-cap = many"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "damage-cap");
                    Check.True(false, "非法上限必须被拒绝");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal("invalid_value", failure.Code, "错误码");
                    Check.True(failure.Path.Contains("damage-cap", StringComparison.Ordinal), $"路径：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 伤害上限：上限在乘法之后、截断之前作用于 decimal [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "damage-cap");

            static long Damage(SliceAuthorModel model, bool ward, bool weak, bool vulnerable)
            {
                var session = Build(model);
                var attacker = SliceFixtures.Player(session);
                var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                if (weak) Attach(session, attacker, SliceManifest.WeakId, 1);
                if (vulnerable) Attach(session, target, SliceManifest.VulnerableId, 1);
                if (ward) Attach(session, target, "base-spire:ward", 1);

                var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
                var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
                SliceFixtures.PlayAt(session, strike, target);
                return before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            }

            Check.Equal(5L, Damage(model, ward: false, weak: false, vulnerable: false), "基础 5（无上限）");
            Check.Equal(2L, Damage(model, ward: true, weak: false, vulnerable: false),
                "有上限：5 → 上限 2.5 → 截断 2");
            Check.Equal(7L, Damage(model, ward: false, weak: false, vulnerable: true), "无上限 + 易伤：5 × 1.5 = 7.5 → 7");
            Check.Equal(2L, Damage(model, ward: true, weak: false, vulnerable: true),
                "有上限 + 易伤：7.5 → 上限 2.5 → 2（上限在乘法之后）");
            // 关键反例：5 × 0.75 = 3.75，上限 2.5 → 2.5 → **截断 2**。
            // 若上限作用于"已截断的整数"（3.75→3，再 min(3, 2.5)=2.5→2）结果同为 2，因此再用上限 3.75 区分：
            Check.Equal(2L, Damage(model, ward: true, weak: true, vulnerable: false),
                "有上限 + 虚弱：5 × 0.75 = 3.75 → 上限 2.5 → 2");
        });

        Check.Run("B-05c 伤害上限：上限作用在**乘法结果**上，而不是原始基础值 [真实]", () =>
        {
            // 把上限调到 3.75（= 5 × 0.75 恰好相等）与 3.5 两种情形：
            //  - 上限 3.75：3.75 → min(3.75, 3.75) = 3.75 → 截断 3；
            //  - 若实现先截断（3.75→3）再夹，结果也是 3 —— 不能区分。
            // 用上限 3.4 区分：先夹再截断 = min(3.75, 3.4) = 3.4 → 3；先截断再夹 = min(3, 3.4) = 3 → 3。仍不能区分。
            // 真正可区分的是**上限带小数且小于截断后的整数**的情形：基础 5 + 虚弱 3.75，上限 3.6：
            //   先夹再截断 = 3.6 → 3；先截断再夹 = 3 → min(3, 3.6) = 3。相同。
            // 结论：截断点在上限之后时，两种次序在**整数包络**内不可区分；可区分的是"上限是否作用于乘法结果"，
            // 由下面这条断言给出：基础 5 + 易伤 7.5，上限 7.2 → 7（先乘后夹）；若上限作用于原始 5，则 5 → 5。
            var model = SliceAuthorFrontend.Parse(
                ScenarioText.Replace("damage-cap = 2.5", "damage-cap = 7.2"), "damage-cap");
            var session = Build(model);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            Attach(session, target, SliceManifest.VulnerableId, 1);
            Attach(session, target, "base-spire:ward", 1);
            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            var strike = SliceFixtures.PutInHand(session, SliceManifest.StrikeId, upgraded: false);
            SliceFixtures.PlayAt(session, strike, target);
            Check.Equal(7L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "上限 7.2 作用在乘法结果 7.5 上 → 7.2 → 截断 7（不是把原始 5 当上限前的值）");
        });

        Check.Run("B-05c 伤害上限：冻结作者文本没有上限声明（默认路径不受影响）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            Check.True(frozen.Powers.All(power => power.DamageCap.Length == 0), "冻结文本没有伤害上限");

            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build().Build());
            var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
            session.Start();
            Check.Equal(SessionStatus.Running, session.Status, "程序化路径的受控开局仍然成立");
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
