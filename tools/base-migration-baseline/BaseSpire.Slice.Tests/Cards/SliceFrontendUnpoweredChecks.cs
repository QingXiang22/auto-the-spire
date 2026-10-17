using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第二十一批：**出手方属性门控（`unpowered`）**。
///
/// 来源：`IsPoweredAttack()` = 攻击带 `Move` 且**不带** `Unpowered`（`ValuePropExtensions.cs:5-12`），
/// Strength / Weak（出手方）与 Vulnerable（受击方）三者都以它为必要条件。
/// 判据：声明 `properties = [unpowered]` 的攻击**完全跳过**这三项修正；未声明的照旧；
/// 牌、药水、敌人行动三个来源都能声明；非法属性被拒绝。
/// </summary>
internal static class SliceFrontendUnpoweredChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x8  test-blunt }
        potions { blunt-potion }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power weak { name = Weak  role = Weak  effect = "x"  owner-turn-end = tick  damage = multiply-outgoing:0.75 }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick  damage = multiply-incoming:1.5 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 200  tough-hp = 201  first-move-ritual = 4  strike-damage = 7
          strike-damage-deadly = 7  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          move blunt-strike { damage = strike  properties = [unpowered] }
          cycle = [incantation, dark-strike, blunt-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 200  tough-hp = 201  first-move-ritual = 5  strike-damage = 3
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
        card test-blunt {
          name = TestBlunt  kind = Attack  cost = 0
          normal   { amount = 5  properties = [unpowered]  steps = [target, damage] }
          upgraded { amount = 5  properties = [unpowered]  steps = [target, damage] }
        }
        potion blunt-potion {
          name = BluntPotion  rarity = common  usage = combat-only  target = enemy
          use { amount = 5  properties = [unpowered]  steps = [target, damage] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 属性门控：解析 properties，未知属性被拒绝（带路径）", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "unpowered");
            Check.True(model.Cards.Single(card => card.Id == "base-spire:test-blunt").NormalUnpowered,
                "牌的普通形态声明 unpowered");
            Check.False(model.Cards.Single(card => card.Id == SliceManifest.StrikeId).NormalUnpowered,
                "未声明的牌是 powered");
            Check.True(model.Potions.Single(potion => potion.Id == "base-spire:blunt-potion").Unpowered,
                "药水声明 unpowered");
            Check.True(model.Enemies.Single(enemy => enemy.Id == SliceManifest.CalcifiedCultistId)
                .Moves.Single(move => move.Name == "blunt-strike").Unpowered, "敌人行动声明 unpowered");

            foreach (var (text, code) in new[]
                     {
                         (ScenarioText.Replace("properties = [unpowered]", "properties = [ghostly]"), "invalid_value"),
                         (ScenarioText.Replace("properties = [unpowered]", "properties = unpowered"), "syntax"),
                     })
            {
                try
                {
                    SliceAuthorFrontend.Parse(text, "unpowered");
                    Check.True(false, $"必须被拒绝（{code}）");
                }
                catch (SliceAuthorError failure)
                {
                    Check.Equal(code, failure.Code, $"错误码（{code}）");
                    Check.True(failure.Path.Contains("properties", StringComparison.Ordinal), $"路径：{failure.Path}");
                }
            }
        });

        Check.Run("B-05c 属性门控：unpowered 攻击跳过 Strength/Weak/Vulnerable，powered 照旧 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "unpowered");

            static long Damage(SliceAuthorModel model, string cardId, bool strength, bool weak, bool vulnerable)
            {
                var session = Build(model);
                var attacker = SliceFixtures.Player(session);
                var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                if (strength) Attach(session, attacker, SliceManifest.StrengthId, 3);
                if (weak) Attach(session, attacker, SliceManifest.WeakId, 1);
                if (vulnerable) Attach(session, target, SliceManifest.VulnerableId, 1);
                var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
                var card = SliceFixtures.PutInHand(session, cardId, upgraded: false);
                SliceFixtures.PlayAt(session, card, target);
                return before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            }

            // powered（打击）：三种修正都生效。
            Check.Equal(5L, Damage(model, SliceManifest.StrikeId, false, false, false), "powered 基础 5");
            Check.Equal(8L, Damage(model, SliceManifest.StrikeId, true, false, false), "powered + 3 力量 = 8");
            Check.Equal(3L, Damage(model, SliceManifest.StrikeId, false, true, false), "powered + 虚弱 = 5 × 0.75 → 3");
            Check.Equal(7L, Damage(model, SliceManifest.StrikeId, false, false, true), "powered + 易伤 = 7.5 → 7");

            // unpowered（test-blunt）：三种修正**全部被跳过**，五种局面都是 5。
            Check.Equal(5L, Damage(model, "base-spire:test-blunt", false, false, false), "unpowered 基础 5");
            Check.Equal(5L, Damage(model, "base-spire:test-blunt", true, false, false),
                "unpowered 忽略**出手方力量**");
            Check.Equal(5L, Damage(model, "base-spire:test-blunt", false, true, false),
                "unpowered 忽略**出手方虚弱**");
            Check.Equal(5L, Damage(model, "base-spire:test-blunt", false, false, true),
                "unpowered 忽略**受击方易伤**");
            Check.Equal(5L, Damage(model, "base-spire:test-blunt", true, true, true),
                "unpowered 在三种修正同时存在时仍是 5");
        });

        Check.Run("B-05c 属性门控：unpowered 药水同样跳过修正 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "unpowered");
            var session = Build(model);
            var attacker = SliceFixtures.Player(session);
            var target = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            Attach(session, attacker, SliceManifest.StrengthId, 3);
            Attach(session, target, SliceManifest.VulnerableId, 1);

            var before = SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp");
            var belt = SliceFixtures.Collection(session, "base-spire:PotionBelt");
            var potionType = session.RuleSet.TypeByQualifiedId("base-spire:Potion");
            var potion = session.State.CollectionMembers(belt).Single();
            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == "base-spire:UsePotion");
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(potion)]);
            while (session.Status == SessionStatus.AwaitingDecision)
            {
                session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [target]));
            }

            Check.Equal(5L, before - SliceFixtures.Number(session, target, SliceFixtures.MonsterTypeId, "Hp"),
                "药水的 unpowered 攻击跳过力量与易伤（仍是 5）");
            _ = potionType;
        });

        Check.Run("B-05c 属性门控：冻结作者文本没有 unpowered（默认路径行为不变）", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            Check.True(frozen.Cards.All(card => !card.NormalUnpowered && !card.UpgradedUnpowered),
                "冻结文本的牌都是 powered");
            Check.True(frozen.Enemies.SelectMany(enemy => enemy.Moves).All(move => !move.Unpowered),
                "冻结文本的敌人行动都是 powered");
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
