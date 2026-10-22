using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class UnknownRoomComparison
{
    private const string Stream = "base-spire:run:unknown_map_point";
    private sealed record Boundary(string At, string Room, long[] Odds, long Draws, ulong[] State, long? FloatBits);
    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use unknown-room-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        var package = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        using var fixture = new Fixture(package.Root);
        var observed = DslPackage.Load(Path.Combine(fixture.Root, "package.json"));
        var results = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" })
        foreach (var mode in new[] { "normal", "exclude-shop", "double", "event-only", "no-event" })
        {
            var left = Dsl(observed, seed, mode); var right = Native(seed, mode);
            var same = NormalPreflight.Json(left) == NormalPreflight.Json(right); passed &= same;
            if (left.Length != 20 || left[^1].Draws != 18 || left.Count(b => b.FloatBits.HasValue) != 18)
                throw new InvalidOperationException("Unknown room comparison omitted source draws/reset boundaries.");
            results.Add(new { id = seed + ":" + mode, status = same ? "passed" : "diverged", left, right });
        }
        var report = new { schema = "dsl-migration/unknown-rooms@1", scope = "controlled-component", reference,
            package_digest = package.Digest, fixture_digest = observed.Digest, status = passed ? "passed" : "diverged",
            independent_verified = passed, normal_run_verified = false,
            controlled_setup = "Real production RollUnknownRoom versus locked UnknownMapPointOdds.Roll. Each case draws 18 times and resets current odds to the actual base after draw 12 without resetting RNG. Shop blacklist and pure IOddsHooks modes are controlled component inputs, not migrated relic claims. Actual NextFloat(0,1) parameters and final binary32 result metadata are checked on both sides. No reseed, compensation draw, pool reduction or room outcome injection.",
            cases = results };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }
    private static Boundary[] Dsl(DslPackage package, string seed, string mode)
    {
        var s = package.CreateSession(1, ["player"], seed, new SessionDebugOptions(MaxRecords: 50_000)); s.Start();
        void Submit(string name)
        {
            var action = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
            s.Submit(action.Definition, s.Participant(0), []);
            if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Unknown-room DSL stopped: " + s.Fault);
        }
        var auditType = s.RuleSet.TypeByQualifiedId("base-spire:UnknownFixture");
        Submit("FixtureInit"); var audit = s.State.AllInstances(auditType.Id).Single();
        s.State.RunAtomic(() => s.State.SetField(audit, auditType.FieldIndex("Mode"), Value.Of(mode)));
        var boundaries = new List<Boundary> { Snapshot("start", false) };
        for (var draw = 1; draw <= 18; draw++)
        {
            Submit("FixtureRoll"); boundaries.Add(Snapshot("draw:" + draw, true));
            if (draw == 12) { Submit("FixtureReset"); boundaries.Add(Snapshot("reset", false)); }
        }
        return boundaries.ToArray();
        Boundary Snapshot(string at, bool drew)
        {
            if (s.Debug!.DroppedRecords != 0) throw new InvalidOperationException("Unknown room diagnostics dropped records.");
            var random = s.Random.Capture(); var state = random.Streams.SingleOrDefault(r => r.Name == Stream);
            var words = state?.BackendState ?? new Xoshiro256StarStar(random.ExplicitSeeds[Stream], false).Capture();
            long? bits = null;
            if (drew)
            {
                var call = s.Debug.Capture().Last(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == Stream).Random!;
                if (call.Operation != "double" || call.CandidatesTruncated || call.After != call.Before + 1
                    || call.Float32 is not { MinimumBits: 0, MaximumBits: 1065353216 })
                    throw new InvalidOperationException("Unexpected unknown room draw operation.");
                bits = call.Float32.ResultBits;
            }
            var odds = s.RuleSet.TypeByQualifiedId("base-spire:UnknownRoomOdds");
            return new(at, s.State.GetField(audit, auditType.FieldIndex("Last")).AsString(),
                s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:UnknownRoomOddsOrder").Id)
                    .Select(o => s.State.GetField(o, odds.FieldIndex("Current")).AsInt()).ToArray(), s.Random.DrawCount(Stream), words.ToArray(), bits);
        }
    }
    private static Boundary[] Native(string seed, string mode)
    {
#if REFERENCE_SIM
        var run = new RunState(seed, new Overgrowth()); var rng = run.Rng.UnknownMapPoint;
        var odds = new UnknownMapPointOdds(rng, new Hooks(mode));
        var hook = typeof(Rng).GetField("_drawObserver", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var previous = RngDiagnostics.DrawObserver; long? bits = null; var room = "none";
        if (previous is not null || hook.GetValue(rng) is not null) throw new InvalidOperationException("Unknown room RNG observer already owned.");
        RngDiagnostics.DrawObserver = (source, op, min, max, arity) =>
        {
            if (ReferenceEquals(source, rng) && (op != "NextFloat" || min != 0 || max != 1 || arity != 2))
                throw new InvalidOperationException("Unexpected native unknown room draw parameters.");
        };
        hook.SetValue(rng, (Action<int, string, string>)((_, _, result) => bits = BitConverter.SingleToUInt32Bits(float.Parse(result, CultureInfo.InvariantCulture))));
        try
        {
            var boundaries = new List<Boundary> { Snapshot("start", false) };
            for (var draw = 1; draw <= 18; draw++)
            {
                room = odds.Roll(mode == "exclude-shop" ? [RoomType.Shop] : []).ToString().ToLowerInvariant();
                boundaries.Add(Snapshot("draw:" + draw, true));
                if (draw == 12) { odds.ResetToBase(); boundaries.Add(Snapshot("reset", false)); }
            }
            return boundaries.ToArray();
            Boundary Snapshot(string at, bool drew)
            {
                var state = rng.ToSerializable();
                return new(at, room, new[] { odds.MonsterOdds, odds.EliteOdds, odds.TreasureOdds, odds.ShopOdds }.Select(v => (long)BitConverter.SingleToUInt32Bits(v)).ToArray(),
                    state.counter, [state.state0, state.state1, state.state2, state.state3], drew ? bits : null);
            }
        }
        finally { hook.SetValue(rng, null); RngDiagnostics.DrawObserver = previous; }
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }
#if REFERENCE_SIM
    private sealed class Hooks(string mode) : IOddsHooks
    {
        public bool ShouldForcePotionReward(RoomType _) => false;
        public IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> rooms) => mode switch
        {
            "event-only" => new HashSet<RoomType> { RoomType.Event },
            "no-event" => rooms.Where(r => r != RoomType.Event).ToHashSet(),
            _ => rooms,
        };
        public float ModifyOddsIncreaseForUnrolledRoomType(RoomType _, float increase) => mode == "double" ? increase + increase : increase;
    }
#endif
    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "unknown-room-comparison-" + Guid.NewGuid());
        internal Fixture(string original)
        {
            foreach (var source in Directory.GetFiles(original, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(Root, Path.GetRelativePath(original, source)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            var entry = Path.Combine(Root, "base.spire");
            File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"unknown-fixture.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "unknown-fixture.spire"), """
                using namespace base-spire;
                dsl_version = "core/0.3"
                entity UnknownFixture { fields = { Mode = { type = String(length = 16) default = "normal" } Last = { type = String(length = 16) default = "none" } } }
                action FixtureInit { role = player locals = { audit = Binding } body = { create = { type = UnknownFixture scope = frameScope() into = audit } call = { procedure = InitializeUnknownRoomOdds args = { run = first(type(Run)) } } } }
                action FixtureRoll { role = player locals = { room = String(length = 16) } body = {
                  call = { procedure = RollUnknownRoom args = { run = first(type(Run)) excludeShop = equal(field(first(type(UnknownFixture)), UnknownFixture.Mode), "exclude-shop") } into = room }
                  set = { target = first(type(UnknownFixture)) field = UnknownFixture.Last value = room }
                } }
                action FixtureReset { role = player body = { call = { procedure = ResetUnknownRoomOdds args = { run = first(type(Run)) } } } }
                procedure FixtureAllowed { pure = true parameters = { value = Bool run = Ref(Run) kind = String(length = 16) } result = Bool body = {
                  if = { condition = equal(field(first(type(UnknownFixture)), UnknownFixture.Mode), "event-only") then = { return = { value = equal(kind, "event") } } }
                  if = { condition = equal(field(first(type(UnknownFixture)), UnknownFixture.Mode), "no-event") then = { return = { value = and(value, not(equal(kind, "event"))) } } }
                  return = { value = value }
                } }
                subscription FixtureAllowed { event = ModifyUnknownRoomAllowed reaction = FixtureAllowed }
                procedure FixtureIncrease { pure = true parameters = { value = Int run = Ref(Run) kind = String(length = 16) } result = Int body = {
                  if = { condition = equal(field(first(type(UnknownFixture)), UnknownFixture.Mode), "double") then = { return = { value = float32Add(value, value) } } }
                  return = { value = value }
                } }
                subscription FixtureIncrease { event = ModifyUnknownRoomOddsIncrease reaction = FixtureIncrease }
                """);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
