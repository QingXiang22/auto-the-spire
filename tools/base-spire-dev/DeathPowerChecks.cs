using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunDeathPowerChecks(DslPackage package)
    {
        var natural = NormalFirstCombat(package);
        var initialEnemies = natural.State.AllInstances(natural.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).ToArray();
        var survivors = initialEnemies.Skip(1).Select(e => IntField(natural, e, "Enemy", "Hp")).ToArray();
        var draws = natural.Random.TotalDraws;
        Submit(natural, "PlayAttackFromHand", Hand(natural).Single(c => CardDefinition(natural, c) == "base-spire:bash"));
        Equal(1, Members(natural, "Powers").Count, "normal Bash creates real Vulnerable before death");
        Submit(natural, "PlayAttackFromHand", Hand(natural).First(c => CardDefinition(natural, c) == "base-spire:strike-ironclad"));
        Equal(0L, IntField(natural, initialEnemies[0], "Enemy", "Hp"), "normal second card really kills LeafSlimeS");
        Equal(0, Members(natural, "Powers").Count, "normal death removes Vulnerable immediately");
        Equal("none", Outcome(natural), "nonfinal death keeps combat active");
        Equal(draws, natural.Random.TotalDraws, "death cleanup spends no random draws");
        True(initialEnemies.Skip(1).Select(e => IntField(natural, e, "Enemy", "Hp")).SequenceEqual(survivors), "other enemy instances unchanged");
        var loaded = natural.State.CollectionMembers(natural.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:PowerLibrary").Id)
            .Select(p => TextField(natural, p, "PowerBlueprint", "DefinitionId")).ToHashSet(StringComparer.Ordinal);
        foreach (var name in new[] { "die-for-you", "adaptable", "steam-eruption", "minion", "painful-stabs", "illusion", "reattach" })
            True(!loaded.Contains("base-spire:" + name + "-power"), "source death-retention/veto consumer remains unsupported: " + name);
        Console.WriteLine("ok   CB07 normal SlimesWeak Bash/Strike: death clears real Vulnerable without changing survivors or RNG");

        using var copy = new Copy(package.Root);
        copy.Include("death-audit.spire", """
            entity DeathAudit { fields = { Deaths = { type = Int default = 0 } Removed = { type = Int default = 0 } } }
            procedure InitializeDeathAudit { locals = { audit = Binding } body = {
              create = { type = DeathAudit scope = entityScope(first(type(Player))) into = audit }
              call = { procedure = ApplyEnemyPower args = { target = first(where(type(Enemy), e, equal(field(e, Enemy.DefinitionId), "base-spire:twig-slime-m"))) definition = "base-spire:strength-power" stacks = 5 } }
            } }
            action InitializeDeathAudit { role = player body = { call = { procedure = InitializeDeathAudit } } }
            procedure InspectDeath { parameters = { target = Ref(Enemy) } body = {
              if = { condition = not(exists(where(collection(Powers), p, equal(field(p, Power.HolderEnemy), target)))) then = { fail = { message = "Death notification lost its original Power too early" } } }
              if = { condition = field(target, Enemy.DeathResolved) then = { fail = { message = "Death detached before its notification completed" } } }
              set = { target = first(type(DeathAudit)) field = DeathAudit.Deaths value = add(field(first(type(DeathAudit)), DeathAudit.Deaths), 1) }
            } }
            subscription InspectDeath { event = EnemyDied reaction = InspectDeath }
            procedure InspectRemoval { parameters = { power = Ref(Power) } body = {
              if = { condition = greaterOrEqual(collectionIndex(Powers, power), 0) then = { fail = { message = "Power removal notification precedes collection removal" } } }
              if = { condition = not(field(field(power, Power.HolderEnemy), Enemy.DeathResolved)) then = { fail = { message = "Power removal precedes source detach" } } }
              if = { condition = not(equal(field(first(type(DeathAudit)), DeathAudit.Deaths), 1)) then = { fail = { message = "Power removal precedes death notification" } } }
              call = { procedure = ApplyEnemyPower args = { target = field(power, Power.HolderEnemy) definition = "base-spire:strength-power" stacks = 9 } }
              set = { target = first(type(DeathAudit)) field = DeathAudit.Removed value = add(field(first(type(DeathAudit)), DeathAudit.Removed), 1) }
            } }
            subscription InspectRemoval { event = PowerRemoved reaction = InspectRemoval }
            action RepeatDeathCleanup { role = player body = { call = { procedure = RemoveEnemyPowersOnDeath args = { target = first(type(Enemy)) } } } }
            """);
        var compiled = copy.Load();
        var audited = NormalFirstCombat(compiled); Submit(audited, "InitializeDeathAudit");
        Submit(audited, "PlayAttackFromHand", Hand(audited).Single(c => CardDefinition(audited, c) == "base-spire:bash"));
        Submit(audited, "PlayAttackFromHand", Hand(audited).First(c => CardDefinition(audited, c) == "base-spire:strike-ironclad"));
        var audit = audited.State.AllInstances(audited.RuleSet.TypeByQualifiedId("base-spire:DeathAudit").Id).Single();
        Equal(1L, IntField(audited, audit, "DeathAudit", "Deaths"), "one death notification");
        Equal(1L, IntField(audited, audit, "DeathAudit", "Removed"), "one removal notification");
        Equal(1, Members(audited, "Powers").Count, "removal callback cannot reapply Power to a detached enemy");
        Equal(5L, IntField(audited, Members(audited, "Powers").Single(), "Power", "Stacks"), "survivor retains its own Power");
        var beforeRepeat = audited.Random.TotalDraws; Submit(audited, "RepeatDeathCleanup");
        Equal(1L, IntField(audited, audit, "DeathAudit", "Removed"), "repeat cleanup emits no second removal");
        Equal(beforeRepeat, audited.Random.TotalDraws, "repeat cleanup spends no RNG");
        var live = NormalFirstCombat(compiled); Submit(live, "InitializeDeathAudit");
        var beforeInvalid = live.Random.TotalDraws;
        Throws<EngineFaultException>(() => live.Submit(live.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:RepeatDeathCleanup"), live.Participant(0), []));
        True(!live.State.GetField(FirstTarget(live), live.RuleSet.TypeByQualifiedId("base-spire:Enemy").FieldIndex("DeathResolved")).AsBool(), "invalid live-target cleanup cannot detach the enemy");
        Equal(1, Members(live, "Powers").Count, "invalid live-target cleanup cannot remove the surviving Power");
        Equal(beforeInvalid, live.Random.TotalDraws, "invalid live-target cleanup spends no RNG");
        Console.WriteLine("ok   CB07 controlled death notifications: original Power, detach, full removal, survivor isolation, rejected reapplication and repeat cleanup");

        var fatalBash = NormalFirstCombat(package);
        Set(fatalBash, FirstTarget(fatalBash), "Enemy", "Hp", Value.Of(8));
        Submit(fatalBash, "PlayAttackFromHand", Hand(fatalBash).Single(c => CardDefinition(fatalBash, c) == "base-spire:bash"));
        Equal(0, Members(fatalBash, "Powers").Count, "fatal Bash cannot add Vulnerable after target detach");
        Equal("none", Outcome(fatalBash), "fatal Bash leaves the other source enemies active");
        Console.WriteLine("ok   CB08 controlled fatal Bash rejects post-death Vulnerable while other enemies remain");
    }
}
