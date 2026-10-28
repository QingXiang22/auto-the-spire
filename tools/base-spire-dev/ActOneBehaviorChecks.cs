using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunActOneBehaviorChecks(DslPackage package, Action<string, Action> check)
    {
        long EnemyHpAt(Session s, InstanceId enemy) => s.State.GetField(enemy, EnemyField(s, "Hp")).AsInt();
        long PowerStacksAt(Session s, InstanceId enemy, string definition) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Power").Id)
            .Where(p => { var holder = s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("HolderEnemy")); return !holder.IsNone && holder.AsRef() == enemy
                && s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("DefinitionId")).AsString() == definition; })
            .Select(p => s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("Stacks")).AsInt()).SingleOrDefault();
        long PlayerPowerStacks(Session s, string definition) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Power").Id)
            .Where(p => { var holder = s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("HolderPlayer")); return !holder.IsNone && holder.AsRef() == Player(s)
                && s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("DefinitionId")).AsString() == definition; })
            .Select(p => s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("Stacks")).AsInt()).SingleOrDefault();

        check("ACT1 ShrinkPower reduces the player's next powered attack to 70 percent", () =>
        {
            var s = Start(package); Submit(s, "ShrinkerBeetleWeak.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(-1L, PlayerPowerStacks(s, "base-spire:shrink-power"), "permanent -1 Shrink");
            var hp = EnemyHpAt(s, FirstTarget(s)); Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            var card = IcAdd(s, "StrikeIronclad");
            IcPlay(s, card);
            Equal(hp - 4L, EnemyHpAt(s, FirstTarget(s)), "6 * 0.70 floors to 4");
        });
        check("ACT1 SlipperyPower caps one unblocked hit at 1 HP and consumes its stack", () =>
        {
            var s = Start(package); Submit(s, "Inklet.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal(1L, PowerStacksAt(s, FirstTarget(s), "base-spire:slippery-power"), "one source stack");
            var hp = EnemyHpAt(s, FirstTarget(s)); Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(hp - 1L, EnemyHpAt(s, FirstTarget(s)), "hit capped at 1");
            Equal(0L, PowerStacksAt(s, FirstTarget(s), "base-spire:slippery-power"), "stack consumed");
        });
        check("ACT1 ConstrictPower damages the player at the next player side end", () =>
        {
            var s = Start(package); Submit(s, "SlitheringStrangler.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var hp = Hp(s); Submit(s, "EndDevelopmentTurn");
            Equal(3L, PlayerPowerStacks(s, "base-spire:constrict-power"), "initial Constrict 3");
            var afterFirst = Hp(s); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            True(afterFirst - Hp(s) >= 3L, "Constrict adds its own damage at the next player side end");
        });
        check("ACT1 TangledPower raises attack costs until the owner's side turn ends", () =>
        {
            var s = Start(package); Submit(s, "VineShambler.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(1L, PlayerPowerStacks(s, "base-spire:tangled-power"), "Tangled 1 from Grasping Vines");
            var strike = IcAdd(s, "StrikeIronclad"); Equal(2L, CardCost(s, strike), "attack costs one more while Entangled");
            Submit(s, "EndDevelopmentTurn");
            Equal(0L, PlayerPowerStacks(s, "base-spire:tangled-power"), "Tangled expires at player side end");
        });
        check("ACT1 Fogmog summons EyeWithTeeth and revives it once on the next enemy turn", () =>
        {
            var s = Start(package); Submit(s, "FogmogNormal.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal("ILLUSION_MOVE", s.State.GetField(FirstTarget(s), EnemyField(s, "MoveId")).AsString(), "initial Illusion");
            Submit(s, "EndDevelopmentTurn");
            var eye = Targets(s).Single(e => s.State.GetField(e, EnemyField(s, "DefinitionId")).AsString() == "base-spire:eye-with-teeth");
            Equal(6L, EnemyHpAt(s, eye), "summoned at 6 HP");
            Submit(s, "BeginDevelopmentTurn");
            Set(s, eye, "Enemy", "Hp", Value.Of(1L));
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            var strike = IcAdd(s, "StrikeIronclad");
            Submit(s, "PlayTargetedCard", strike);
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Instances: [eye])); Healthy(s);
            True(s.State.GetField(eye, EnemyField(s, "Reviving")).AsBool(), "zero-HP illusion waits to revive");
            True(Targets(s).Contains(eye), "illusion remains on the side");
            Equal("none", Outcome(s), "Fogmog keeps the combat alive");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(6L, EnemyHpAt(s, eye), "revive restores full HP");
            True(!s.State.GetField(eye, EnemyField(s, "Reviving")).AsBool(), "revive consumed");
        });
    }
}
