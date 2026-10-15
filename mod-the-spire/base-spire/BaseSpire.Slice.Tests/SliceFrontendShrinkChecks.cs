using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第二十二批：**出手倍率型能力（`Shrink` 语义）**——补齐 R-DAMAGE 证据里"出手倍率"这一未覆盖区，
/// 并让 `BeetleJuice` 药水可以被表达。
///
/// 来源事实：`sts2/src/Core/Models/Powers/ShrinkPower.cs` ——
/// <c>damageDecrease = 30m</c>，<c>ModifyDamageMultiplicative</c> 返回 <c>(100 - 30) / 100 = 0.70m</c>：
/// **按层数无关的固定 ×0.70**（层数是持续时间），且同样以 <c>IsPoweredAttack()</c> 为必要条件；
/// 它是 <c>PowerType.Debuff</c>，按 `PowerCmd.Decrement` 递减（与 Weak/Vulnerable 同一套时长语义）。
/// </summary>
internal static class SliceFrontendShrinkChecks
{
    private const string ScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x8 }
        potions { beetle-juice }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power weak { name = Weak  role = Weak  effect = "x"  owner-turn-end = tick  damage = multiply-outgoing:0.75 }
        power vulnerable { name = Vulnerable  role = Vulnerable  effect = "x"  owner-turn-end = tick  damage = multiply-incoming:1.5 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        power shrink { name = Shrink  role = Shrink  effect = "x"  owner-turn-end = tick  damage = multiply-outgoing:0.7 }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 200  tough-hp = 201  first-move-ritual = 4  strike-damage = 10
          strike-damage-deadly = 10  first-move = Incantation  repeat-move = DarkStrike
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
          normal   { amount = 10  steps = [target, damage] }
          upgraded { amount = 10  steps = [target, damage] }
        }
        potion beetle-juice {
          name = BeetleJuice  rarity = rare  usage = combat-only  target = enemy
          use { amount = 0  power = shrink  stacks = 4  steps = [target, apply_power] }
        }
        """;

    public static void Run()
    {
        Check.Run("B-05c 出手倍率：Shrink 按来源声明为固定 ×0.70（不是按层数）[真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "shrink");
            var shrink = model.Powers.Single(power => power.Id == "base-spire:shrink");
            Check.Equal(PowerDamage.MultiplyOutgoing, shrink.DamageModifier, "出手方乘性");
            Check.Equal("0.7", shrink.DamageModifierValue, "来源值 (100-30)/100 = 0.70");
            Check.Equal(PowerTurnEnd.Tick, shrink.OwnerTurnEnd, "Debuff：按时长递减");

            // 层数只影响持续时间：1 层与 4 层的**伤害倍率相同**。
            static long EnemyHit(SliceAuthorModel model, int shrinkStacks)
            {
                var session = Build(model);
                var monster = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
                Attach(session, monster, "base-spire:shrink", shrinkStacks);
                var player = SliceFixtures.Player(session);
                var before = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
                EndTurn(session);   // 第一个敌人回合：念咒
                EndTurn(session);   // 第二个敌人回合：暗击（10 点）
                return before - SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            }

            Check.Equal(10L, EnemyHit(model, 0), "没有 Shrink：10 点");
            Check.Equal(7L, EnemyHit(model, 1), "1 层 Shrink：10 × 0.70 = 7");
            Check.Equal(7L, EnemyHit(model, 4), "4 层 Shrink：仍然是 7（层数不是倍率）");
        });

        Check.Run("B-05c 出手倍率：Shrink 与 Weak 叠乘、且被 unpowered 跳过 [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "shrink");
            var session = Build(model);
            var monster = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var player = SliceFixtures.Player(session);
            Attach(session, monster, "base-spire:shrink", 1);
            Attach(session, monster, SliceManifest.WeakId, 1);

            var before = SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp");
            EndTurn(session);
            EndTurn(session);
            // 10 × 0.75 × 0.70 = 5.25 → 截断 5（阶段之间不取整）。
            Check.Equal(5L, before - SliceFixtures.Number(session, player, SliceFixtures.PlayerTypeId, "Hp"),
                "Shrink 与 Weak 叠乘：10 × 0.75 × 0.70 = 5.25 → 5");
        });

        Check.Run("B-05c 出手倍率：BeetleJuice 药水给所选敌人上 4 层 Shrink [真实]", () =>
        {
            var model = SliceAuthorFrontend.Parse(ScenarioText, "shrink");
            var session = Build(model);
            var front = SliceFixtures.Cultist(session, SliceManifest.CalcifiedCultistId);
            var back = SliceFixtures.Cultist(session, SliceManifest.DampCultistId);
            var belt = SliceFixtures.Collection(session, "base-spire:PotionBelt");
            var potion = session.State.CollectionMembers(belt).Single();

            var action = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == "base-spire:UsePotion");
            session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(potion)]);
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "敌人目标药水先提问");
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [front]));

            Check.Equal(4L, Stacks(session, front, "base-spire:shrink"), "所选敌人获得 4 层 Shrink");
            Check.Equal(0L, Stacks(session, back, "base-spire:shrink"), "另一个敌人没有");
            Check.Equal(0, session.State.CollectionMembers(belt).Count, "药水被消耗");
        });

        Check.Run("B-05c 出手倍率：冻结作者文本声明了 Shrink 与 BeetleJuice", () =>
        {
            var frozen = SliceAuthorFrontend.Load();
            var shrink = frozen.Powers.Single(power => power.Id == "base-spire:shrink");
            Check.Equal("0.7", shrink.DamageModifierValue, "冻结文本的 Shrink 值");
            Check.Equal(4, frozen.Potions.Single(potion => potion.Id == "base-spire:beetle-juice").PowerStacks,
                "BeetleJuice 上 4 层");
            Check.Equal(PotionTargets.Enemy,
                frozen.Potions.Single(potion => potion.Id == "base-spire:beetle-juice").Target, "敌人目标");
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

    private static void EndTurn(Session session)
    {
        var endTurn = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == "base-spire:EndTurn");
        session.Submit(endTurn.Definition, SliceManifest.Player, []);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId,
                [session.PendingDecision!.Candidates[0]]));
        }
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
