namespace Differential;

internal static class NormalRunChecks
{
    public static void Run(string repository)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in NormalRunComparison.RequiredPaths) values.Add(key, "present");
        var boundary = new NormalBoundary("neow-ready", values, [], new { fault_injection = true });
        var pairs = Enumerable.Range(0, 6).Select(i => new NormalPair(boundary with { Kind = i == 5 ? "map-return" : "neow-ready" },
            boundary with { Kind = i == 5 ? "map-return" : "neow-ready" })).ToArray();
        NormalAction[] prefix = [new("neow", ["relic"]), new("leave-neow", []), new("map", ["0,1"]), new("play", ["card"]), new("close-rewards", [])];
        var item = new NormalCase("fault-injection", "blocked_compare", null, prefix, pairs, null, true);
        // Synthetic records exercise the evidence gate only. They never count as gameplay passes.
        Require(NormalRunComparison.Certified(item), "complete synthetic evidence gate");
        foreach (var key in NormalRunComparison.RequiredPaths)
        {
            var missing = new SortedDictionary<string, string>(values, StringComparer.Ordinal); missing.Remove(key);
            var b = boundary with { Values = missing };
            Require(!NormalRunComparison.Certified(item with { Boundaries = [.. pairs[..^1], new(b, b)] }), "both sides omit " + key);
        }
        foreach (var dimension in NormalRunComparison.Dimensions)
        {
            var missing = boundary with { MissingDimensions = [dimension] };
            Require(!NormalRunComparison.Certified(item with { Boundaries = [.. pairs[..^1], new(missing, missing)] }), "explicit unverified " + dimension);
            var key = NormalRunComparison.RequiredPaths.First(p => p.StartsWith(dimension + ".", StringComparison.Ordinal));
            var changed = new SortedDictionary<string, string>(values, StringComparer.Ordinal) { [key] = "changed" };
            Require(NormalRunComparison.First(2, boundary, boundary with { Values = changed }) is { Boundary: 2 } d && d.Path == key,
                dimension + " first difference");
        }
        Require(!NormalRunComparison.Certified(item with { Boundaries = [] }), "zero execution");
        Require(!NormalRunComparison.Certified(item with { Reason = "unsupported" }), "unsupported/exception");
        Require(!NormalRunComparison.Certified(item with { ReturnedToMap = false }), "missing map return");
        Require(!NormalRunComparison.Certified(item with { Prefix = [] }), "missing common decisions");
        Require(!NormalRunComparison.Certified(item with { FirstDifference = new(0, "start", "facts", "a", "b") }), "real difference");
        CheckHostDiagnostics(repository);
#if REFERENCE_SIM
        CheckReferenceDiagnostics();
#endif
        Console.WriteLine("Normal comparison checks passed: all dimensions/first differences, zero execution, incomplete policy/return, and real Host diagnostic non-interference.");
    }
#if REFERENCE_SIM
    private static void CheckReferenceDiagnostics()
    {
        var enabled = Replay(true); var disabled = Replay(false);
        Require(enabled.SequenceEqual(disabled), "reference combat/RNG observers leave every measured boundary and named stream unchanged");
        string[] Replay(bool diagnostics)
        {
            using var reference = new NormalReferenceRun("777", diagnostics); reference.Start();
            var result = new List<string>(); Capture();
            foreach (var action in new[] { new NormalAction("neow", ["base-spire:booming-conch"]), new("leave-neow", []), new("map", ["0,1"]),
                new("play", ["permanent:09:base-spire:bash", "first-legal-target"]), new("play", ["permanent:02:base-spire:strike-ironclad", "first-legal-target"]) })
            { reference.Apply(action); Capture(); }
            return result.ToArray();
            void Capture()
            {
                result.Add(NormalPreflight.Json(reference.Capture("check").Values));
                result.Add(reference.KnownRandomState);
            }
        }
    }
#endif
    private static void CheckHostDiagnostics(string repository)
    {
        var directory = Path.Combine(repository, ".artifacts/differential/diagnostic-checks"); Directory.CreateDirectory(directory);
        using var observed = new NormalDslRun(repository, Path.Combine(directory, "observed.ndjson"));
        using var plain = new NormalDslRun(repository, Path.Combine(directory, "plain.ndjson"), debug: false);
        observed.Start("777"); plain.Start("777");
        observed.Step(observed.Action("StartNormalRun")); plain.Step(plain.Action("StartNormalRun"));
        Check();
        foreach (var action in new[] { new NormalAction("neow", ["base-spire:booming-conch"]), new("leave-neow", []) })
        { observed.Apply(action); plain.Apply(action); Check(); }
        var map = observed.Semantic(observed.Action("ChooseMapPoint")); observed.Apply(map); plain.Apply(map); Check();
        for (var i = 0; i < 3; i++)
        {
            var action = observed.Semantic(new[] { "PlayAttackFromHand", "PlayCardFromHand", "EndDevelopmentTurn" }
                .Select(observed.Find).First(a => a is not null)!);
            observed.Apply(action); plain.Apply(action); Check();
        }
        void Check()
        {
            var a = observed.Capture("check"); var b = plain.Capture("check");
            Require(NormalRunComparison.First(0, a, b) is null, "diagnostics leave normal Host facts/actions/piles unchanged");
            Require(observed.RandomState == plain.RandomState, "diagnostics leave every stream/state/cursor unchanged");
            Require(observed.HasCompleteDebug && !plain.HasDebug, "debug is present only when requested and has no dropped records");
            Require(observed.PublicState == plain.PublicState, "diagnostics leave the complete public snapshot unchanged");
        }
    }
    private static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Normal comparison check failed: " + name); }
}
