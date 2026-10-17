using BaseSpire.Slice.Domain.Run;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

// RelicCmd and Player.MeltRelicInternal are the source contracts. Formal relic definitions,
// controlled command consumers; these checks do not claim normal-run or independent evidence.
internal static class RelicLifecycleChecks
{
    public static void Run()
    {
        Check.Run("Relics: replacement preserves position, resets state and removes before obtaining", () =>
        {
            var s = Build(); Submit(s, "Seed");
            var before = Owned(s).ToArray(); Submit(s, "CountMiddle"); Submit(s, "ReplaceMiddle");
            var after = Owned(s);
            Check.Equal(before[0], after[0], "left neighbour stable");
            Check.Equal(before[2], after[2], "right neighbour stable");
            Check.True(before[1] != after[1], "replacement has fresh identity");
            Check.Equal(0L, RelicNumber(s, after[1], "Counter"), "new instance starts at zero");
            Check.Equal("base-spire:old-coin", RelicValue(s, after[1], "DefinitionId").AsString(), "target definition");
            Check.Equal(8L, RunValue(s, "RemovedCounter").AsInt(), "remove callback can still read old counter");
            Check.False(RunValue(s, "RemovalOwned").AsBool(), "remove callback sees detached instance");
            Check.Equal(11121L, RunValue(s, "Trace").AsInt(), "three obtains, removal, obtain");
            Check.Equal(90L, RunValue(s, "MaxHp").AsInt(), "replacement never reverses Pear permanent gain");
        });
        Check.Run("Relics: removing a captured first instance does not remove its neighbour", () =>
        {
            var s = Build(); Submit(s, "Seed"); var middle = Owned(s)[1];
            Submit(s, "RemoveFirst");
            Check.Equal(middle, Owned(s)[0], "query argument captured before removing head");
            Check.Equal(2, Owned(s).Count, "exactly one removed");
            Check.Equal(300L, RunValue(s, "Gold").AsInt(), "OldCoin removal does not refund gain");
        });
        Check.Run("Relics: wax melting keeps identity, order and counter, but disables active queries", () =>
        {
            var s = Build(); Submit(s, "Wax"); var original = Owned(s).Single();
            Submit(s, "CountFirst"); Submit(s, "MeltFirst"); Submit(s, "Query");
            Check.Equal(original, Owned(s).Single(), "melted instance is still held");
            Check.Equal(7L, RelicNumber(s, original, "Counter"), "counter retained");
            Check.True(RelicValue(s, original, "IsMelted").AsBool(), "melted flag");
            Check.True(RunValue(s, "RemovalOwned").AsBool(), "melt callback observes membership");
            Check.False(RunValue(s, "HasBlood").AsBool(), "active predicate excludes melted instance");
            Check.Equal(12L, RunValue(s, "Trace").AsInt(), "obtained and removed callbacks exactly once");
        });
        Check.Run("Relics: normal and already melted relics fail the wax guard", () =>
        {
            var normal = Build(); Submit(normal, "Seed"); Submit(normal, "MeltFirst", fault: true);
            Check.True(normal.Fault!.ToString()!.Contains("unmelted wax relic"), "named ordinary-relic failure");
            var wax = Build(); Submit(wax, "Wax"); Submit(wax, "MeltFirst"); Submit(wax, "MeltFirst", fault: true);
            Check.Equal(12L, RunValue(wax, "Trace").AsInt(), "failed second melt never dispatches callback");
        });
        Check.Run("Relics: counters are isolated and reject inactive instances", () =>
        {
            var s = Build(); Submit(s, "Seed"); Submit(s, "CountMiddle");
            Check.Sequence(new long[] { 0, 8, 0 }, Owned(s).Select(r => RelicNumber(s, r, "Counter")), "per-instance values");
            var wax = Build(); Submit(wax, "Wax"); Submit(wax, "MeltFirst"); Submit(wax, "CountFirst", fault: true);
            Check.Equal(0L, RelicNumber(wax, Owned(wax).Single(), "Counter"), "inactive counter unchanged");
        });
        RunCounterIsolation();
        Check.Run("Relics: unknown replacement fails before removing old instance", () =>
        {
            var s = Build(); Submit(s, "Seed"); var before = Owned(s).ToArray();
            Submit(s, "ReplaceUnknown", fault: true);
            Check.Sequence(before, Owned(s), "old inventory intact");
            Check.Equal(111L, RunValue(s, "Trace").AsInt(), "no remove/obtain callbacks");
        });
        Check.Run("Relics: unsupported trigger/effect combinations fail instead of masquerading as healing", () =>
        {
            var wrong = SpireRelics.ById(SpireRelics.BurningBloodId) with
            { Effects = [new(RelicTriggers.AfterCombatVictory, RelicEffectKinds.GainGold, 6)] };
            Check.True(SpireRelics.UnsupportedReason(wrong).Length > 0, "victory gold not yet implemented");
            Check.False(RunRelics.CanObtain(wrong), "unsupported definition not advertised as obtainable");
        });
    }

    public static void RunCounterIsolation()
    {
        Check.Run("Relics: identical definitions still have independent instance counters", () =>
        {
            var s = Build(); Submit(s, "DuplicateCoins"); Submit(s, "CountFirst");
            Check.Sequence(new long[] { 7, 0 }, Owned(s).Select(r => RelicNumber(s, r, "Counter")), "no definition-level counter state");
            Check.Equal(600L, RunValue(s, "Gold").AsInt(), "two explicit obtains each run their own effect");
        });
    }

    private static Session Build()
    {
        var p = new EnginePackageBuilder("relic-checks", "0.1.0");
        var area = p.ScopeType("Area"); var runType = p.EntityType("Run");
        var hp = runType.AddField("Hp", FieldTypeSchema.Int(0, 999));
        var maxHp = runType.AddField("MaxHp", FieldTypeSchema.Int(1, 999));
        var gold = runType.AddField("Gold", FieldTypeSchema.Int(0, 99999));
        var trace = runType.AddField("Trace", FieldTypeSchema.Int(0, 99999999));
        var removedCounter = runType.AddField("RemovedCounter", FieldTypeSchema.Int(0, 999999));
        var removalOwned = runType.AddField("RemovalOwned", FieldTypeSchema.Bool());
        var hasBlood = runType.AddField("HasBlood", FieldTypeSchema.Bool());
        var runs = p.Collection("Runs", runType);
        var schema = RelicSchema.Declare(p);
        var bags = new RunInitializationWiring(p, runType);
        var defeat = p.TerminalResult("Defeat"); var turn = defeat.AddField("Turn", FieldTypeSchema.Int(0, 999));
        var resources = new RunResources(hp, maxHp, gold, defeat, turn);
        Expr Owned(IrScope s, Expr relic) => s.GreaterOrEqual(s.CollectionIndex(schema.Owned, relic), s.Const(0L));
        Inst Record(IrScope s, Expr run, long digit) => s.SetField(run, trace, s.Add(s.Multiply(s.Field(run, trace), s.Const(10L)), s.Const(digit)));
        var relics = new RunRelics(schema, bags, resources, SpireRelics.All)
        {
            AfterObtained = (s, run, _) => [Record(s, run, 1)],
            AfterRemoved = (s, run, relic) => [s.SetField(run, removedCounter, s.Field(relic, schema.Counter)),
                s.SetField(run, removalOwned, Owned(s, relic)), Record(s, run, 2)],
        };
        Expr Run(IrScope s) => s.First(s.Collection(runs));
        Expr First(IrScope s) => s.First(s.Collection(schema.Owned));
        Expr Middle(IrScope s) => s.First(s.Where(s.Collection(schema.Owned), r => s.Equal(s.Field(r, schema.Definition), s.Const("base-spire:pear"))));
        void Action(string name, Func<IrScope, Inst[]> steps)
        { var body = p.ActionBody(p.Action(name)); body.Role(SliceManifest.PlayerRole); body.Body(steps(body)); }
        var setup = p.Procedure("Setup"); var scope = setup.Local("area", FieldTypeSchema.Binding());
        var slot = setup.Local("run", FieldTypeSchema.RefTo(runType));
        setup.Body(setup.CreateScope(area, scope), setup.CreateEntity(runType, setup.LocalRef(scope), slot,
            [(hp, setup.Const(50L)), (maxHp, setup.Const(80L)), (gold, setup.Const(0L)), (trace, setup.Const(0L)),
             (removedCounter, setup.Const(0L)), (removalOwned, setup.Const(false)), (hasBlood, setup.Const(false))], runs));
        p.OnSessionStart(setup.Symbol);
        Action("Seed", s => [.. relics.Obtain(s, Run(s), s.Const("base-spire:old-coin")),
            .. relics.Obtain(s, Run(s), s.Const("base-spire:pear")), .. relics.Obtain(s, Run(s), s.Const(SpireRelics.BurningBloodId))]);
        Action("Wax", s => relics.Obtain(s, Run(s), s.Const(SpireRelics.BurningBloodId), isWax: true));
        Action("DuplicateCoins", s => [.. relics.Obtain(s, Run(s), s.Const("base-spire:old-coin")),
            .. relics.Obtain(s, Run(s), s.Const("base-spire:old-coin"))]);
        Action("CountFirst", s => relics.SetCounter(s, First(s), s.Const(7L)));
        Action("CountMiddle", s => [.. relics.SetCounter(s, Middle(s), s.Const(5L)), .. relics.AddCounter(s, Middle(s), s.Const(3L))]);
        Action("ReplaceMiddle", s => relics.Replace(s, Run(s), Middle(s), s.Const("base-spire:old-coin")));
        Action("ReplaceUnknown", s => relics.Replace(s, Run(s), First(s), s.Const("base-spire:unknown-relic")));
        Action("RemoveFirst", s => relics.Remove(s, Run(s), First(s)));
        Action("MeltFirst", s => relics.Melt(s, Run(s), First(s)));
        Action("Query", s => [s.SetField(Run(s), hasBlood, relics.HasActive(s, s.Const(SpireRelics.BurningBloodId)))]);
        var builder = new RuleSetBuilder(); builder.Add(p.Build());
        var session = new Session(builder.Build(), 42, [SliceManifest.PlayerRole]); session.Start(); return session;
    }

    private static void Submit(Session s, string name, bool fault = false)
    {
        void Execute() => s.Submit(s.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == "relic-checks:" + name).Definition,
            SliceManifest.Player, []);
        if (fault) Check.Fault(EngineFaultCode.SessionStateInvalid, Execute, name);
        else Execute();
        Check.Equal(fault, s.Status == SessionStatus.Faulted, $"{name}: {s.Fault}");
    }
    private static IReadOnlyList<InstanceId> Owned(Session s) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "relic-checks:Relics").Id);
    private static Value RelicValue(Session s, InstanceId relic, string field) => s.State.GetField(relic, s.RuleSet.TypeByQualifiedId("relic-checks:Relic").FieldIndex(field));
    private static long RelicNumber(Session s, InstanceId relic, string field) => RelicValue(s, relic, field).AsInt();
    private static Value RunValue(Session s, string field)
    { var type = s.RuleSet.TypeByQualifiedId("relic-checks:Run"); return s.State.GetField(s.State.AllInstances(type.Id).Single(), type.FieldIndex(field)); }
}
