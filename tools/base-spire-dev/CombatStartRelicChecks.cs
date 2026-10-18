using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCombatStartChecks(DslPackage package, Action<string, Action> check)
    {
        var variants = new[] { (Symbol: "Anchor", Amount: 10L, Id: "base-spire:anchor"), (Symbol: "FakeAnchor", Amount: 4L, Id: "base-spire:fake-anchor") };
        foreach (var variant in variants)
            check($"RC04 {variant.Symbol}: source block on start, never on obtain or end, repeats per combat", () =>
            {
                var s = Start(package); Submit(s, variant.Symbol + ".Obtain"); Equal(0L, Block(s), "obtain is not combat start");
                Set(s, Player(s), "Player", "Block", Value.Of(77L));
                Submit(s, "BeginDevelopmentCombat"); Equal(variant.Amount, Block(s), "clear old block before source reward"); True(Active(s), "active before completion");
                Submit(s, "EndDevelopmentCombat"); Equal(variant.Amount, Block(s), "end grants no reward"); True(!Active(s), "ended");
                Submit(s, "BeginDevelopmentCombat"); Equal(variant.Amount, Block(s), "same held instance triggers once in each new combat");
                Equal(1, Members(s, "Relics").Count, "no recreation of the held instance");
            });
        const string validity = "RC04: absent, removed and melted relics do not trigger; obtain during active waits for next start";
        check(validity, () =>
        {
            var bare = Start(package); Submit(bare, "BeginDevelopmentCombat"); Equal(0L, Block(bare), "no relic");
            foreach (var variant in variants)
            {
                var removed = Start(package); Submit(removed, variant.Symbol + ".Obtain"); Submit(removed, "RemoveOwnedRelic", Members(removed, "Relics").Single());
                Submit(removed, "BeginDevelopmentCombat"); Equal(0L, Block(removed), "removed instance");
                var melted = Start(package); Submit(melted, variant.Symbol + ".Obtain"); Set(melted, Members(melted, "Relics").Single(), "Relic", "IsMelted", Value.Of(true));
                Submit(melted, "BeginDevelopmentCombat"); Equal(0L, Block(melted), "melted instance");
                var late = Start(package); Submit(late, "BeginDevelopmentCombat"); Submit(late, variant.Symbol + ".Obtain"); Equal(0L, Block(late), "late obtain does not replay start");
                Submit(late, "EndDevelopmentCombat"); Submit(late, "BeginDevelopmentCombat"); Equal(variant.Amount, Block(late), "late obtain participates next time");
            }
        });
        const string owners = "RC04: combined start relics filter the real holder and preserve other owners";
        check(owners, () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("start-owners.spire", """
                procedure MakeOtherOwner { locals = { other = Binding } body = {
                  create = { type = Player scope = frameScope() into = other }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:anchor" } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:fake-anchor" } }
                } }
                action MakeOtherOwner { role = player body = { call = { procedure = MakeOtherOwner } } }
                action BeginOtherOwner { role = player body = { call = { procedure = BeginDevelopmentCombat args = { owner = first(skip(type(Player), 1)) } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "Anchor.Obtain"); Submit(s, "FakeAnchor.Obtain"); Submit(s, "MakeOtherOwner");
            var other = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Last();
            Submit(s, "BeginDevelopmentCombat"); Equal(14L, Block(s), "own inventory only, not 28"); Equal(0L, PlayerBlock(s, other), "other holder unchanged");
            Submit(s, "BeginOtherOwner"); Equal(14L, PlayerBlock(s, other), "other holder gets exactly its rewards"); Equal(14L, Block(s), "first holder not triggered again");
        });
        check("RC02: the start event sees active combat and cleared block before listeners grant block", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("start-order.spire", """
                procedure InspectStart { parameters = { owner = Ref(Player) } body = {
                  if = { condition = or(not(field(owner, Player.CombatActive)), not(equal(field(owner, Player.Block), 0))) then = { fail = { message = "start state was not prepared" } } }
                } }
                subscription InspectStart { event = BeforeCombatStart reaction = InspectStart }
                """);
            var s = Start(copy.Load()); Submit(s, "Anchor.Obtain"); Set(s, Player(s), "Player", "Block", Value.Of(99L));
            Submit(s, "BeginDevelopmentCombat"); Equal(10L, Block(s), "listener reward survives startup");
        });
        check("RC01/03: repeated transitions are rejected by action guards and by the DSL procedures", () =>
        {
            var s = Start(package); Submit(s, "Anchor.Obtain");
            True(!Legal(s, "EndDevelopmentCombat"), "no end candidate while idle");
            Submit(s, "BeginDevelopmentCombat"); True(!Legal(s, "BeginDevelopmentCombat"), "no begin candidate while active");
            var before = s.StateVersion;
            var begin = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:BeginDevelopmentCombat");
            Throws<EngineFaultException>(() => s.Submit(begin, s.Participant(0), [])); Healthy(s);
            Equal(before, s.StateVersion, "rejected action has no state changes"); Equal(10L, Block(s), "no duplicate reward");
            using var copy = new Copy(package.Root);
            copy.Include("transition-probes.spire", """
                action RepeatBeginProbe { role = player body = { call = { procedure = BeginDevelopmentCombat args = { owner = first(type(Player)) } } } }
                action RepeatEndProbe { role = player body = { call = { procedure = EndDevelopmentCombat args = { owner = first(type(Player)) } } } }
                """);
            var repeated = Start(copy.Load()); Submit(repeated, "Anchor.Obtain"); Submit(repeated, "BeginDevelopmentCombat");
            Throws<EngineFaultException>(() => repeated.Submit(repeated.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:RepeatBeginProbe"), repeated.Participant(0), []));
            Equal(SessionStatus.Faulted, repeated.Status, "procedure guard rejects duplicate start"); Equal(10L, Block(repeated), "procedure rejection before clearing or rewarding");
            True(repeated.Fault!.Message.Contains("already active", StringComparison.Ordinal), "specific start guard failure");
            var idle = Start(copy.Load());
            Throws<EngineFaultException>(() => idle.Submit(idle.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:RepeatEndProbe"), idle.Participant(0), []));
            Equal(SessionStatus.Faulted, idle.Status, "procedure guard rejects end while idle"); True(!Active(idle), "idle state remains idle");
            True(idle.Fault!.Message.Contains("not active", StringComparison.Ordinal), "specific end guard failure");
        });
        const string combined = "RC06: combat start and real reshuffle rewards compose without cross-triggering";
        check(combined, () =>
        {
            var pair = Start(package); Submit(pair, "TheAbacus.Obtain"); Submit(pair, "Anchor.Obtain");
            Submit(pair, "BeginDevelopmentCombat"); Equal(10L, Block(pair), "Anchor start reward");
            Submit(pair, "Draw"); Equal(16L, Block(pair), "Anchor plus actual TheAbacus reshuffle");
            var s = Start(package); Submit(s, "TheAbacus.Obtain"); Submit(s, "Anchor.Obtain"); Submit(s, "FakeAnchor.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Equal(14L, Block(s), "only start rewards");
            Submit(s, "Draw"); Equal(20L, Block(s), "only actual shuffle adds six"); Submit(s, "Draw"); Equal(20L, Block(s), "ordinary draw has no reward");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Equal(14L, Block(s), "next start clears previous combat block");
        });
        return variants.ToDictionary(v => v.Id, v => new[] { $"RC04 {v.Symbol}: source block on start, never on obtain or end, repeats per combat", validity, owners, combined });
    }

    private static bool Active(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("CombatActive")).AsBool();
    private static long PlayerBlock(Session s, InstanceId player) => s.State.GetField(player, s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("Block")).AsInt();
    private static bool Legal(Session s, string action) => s.LegalActions(s.Participant(0)).Any(a => a.Definition.QualifiedId == "base-spire:" + action);
}
