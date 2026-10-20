using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class DebugChecks
{
    public static void Run()
    {
        RandomDebugChecks.Run();
        Check.Run("DEBUG enabling trace leaves identity, state, random and public choices unchanged", () =>
        {
            var package = EngineDslCompiler.Compile("""
                using namespace random;
                dsl_version = "core/0.3" start = [Setup]
                entity Number { fields = { Value = { type = Int default = 0 } } }
                procedure Setup { locals = { number = Binding } body = {
                    create = { type = Number scope = frameScope() into = number }
                } }
                action Roll { locals = { value = Int } body = {
                    sampleInt = { stream = "dice" min = 0 max = 100 into = value }
                    set = { target = first(type(Number)) field = Number.Value value = value }
                } }
                """);
            var rules = new RuleSetBuilder().Add(package).Build();
            var normal = new Session(rules, 42, ["player"]);
            var debug = new Session(rules, 42, ["player"], debug: new SessionDebugOptions(500, true));
            normal.Start(); debug.Start();
            Check.True(normal.Debug is null, "disabled by default");
            Check.Equal(normal.Identity, debug.Identity, "identity unchanged");
            var roll = rules.Actions.Single();
            normal.Submit(roll, normal.Participant(0), []); debug.Submit(roll, debug.Participant(0), []);
            Check.Equal(normal.State.StateHash(), debug.State.StateHash(), "state unchanged");
            Check.Equal(normal.Random.TotalDraws, debug.Random.TotalDraws, "random unchanged");
            Check.Sequence(normal.Random.Diagnostics(), debug.Random.Diagnostics(), "random states unchanged");
            Check.Sequence(normal.LegalActions(normal.Participant(0)).Select(action => action.Definition.QualifiedId),
                debug.LegalActions(debug.Participant(0)).Select(action => action.Definition.QualifiedId), "legal choices unchanged");
            Check.Contains(debug.Debug!.Records, record => record.Kind == SessionDebugKind.RandomCall, "random consumption recorded");
        });

        Check.Run("DEBUG event, listener, decision and continuation preserve source and execution order", () =>
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "counter.spire");
            var session = new Session(EngineDslCompiler.LoadFiles(path), 42, ["player"],
                debug: new SessionDebugOptions(1000, true));
            var player = session.Participant(0);
            session.Start();
            session.Submit(session.LegalActions(player).Single().Definition, player, []);
            var request = session.PendingDecision!;
            session.Answer(new DecisionAnswer(request.RequestId, Instances: [request.Candidates[0]]));
            var records = session.Debug!.Capture();
            var requested = records.Single(record => record.Kind == SessionDebugKind.DecisionRequested);
            var answered = records.Single(record => record.Kind == SessionDebugKind.DecisionAnswered);
            var emitted = records.Single(record => record.Kind == SessionDebugKind.EventEmit);
            var listener = records.Single(record => record.Kind == SessionDebugKind.ListenerEnter);
            Check.True(requested.Sequence < answered.Sequence && answered.Sequence < emitted.Sequence &&
                emitted.Sequence < listener.Sequence, "real execution order");
            Check.Equal(path, requested.Source!.Value.Source, "decision source file");
            Check.True(requested.Source.Value.Line > 1, "decision source line");
            var write = records.Where(record => record.Kind == SessionDebugKind.Instruction && record.Detail == "inst.setField");
            Check.Equal(3, write.Count(), "before-decision write, score write, listener write; no replay");
        });

        Check.Run("DEBUG bounded trace retains first failure even after overflow and cannot invoke a sink in-session", () =>
        {
            var package = EngineDslCompiler.Compile("""
                using namespace broken;
                dsl_version = "core/0.3" start = [Setup]
                procedure Setup { body = { fail = { message = "failed rule" } } }
                """, "failure.spire");
            var session = new Session(new RuleSetBuilder().Add(package).Build(), 1, ["player"],
                debug: new SessionDebugOptions(MaxRecords: 1, IncludeInstructions: true, MaxTextLength: 16));
            Check.Throws<EngineFaultException>(session.Start, "explicit fault");
            Check.Equal(SessionStatus.Faulted, session.Status, "session faulted");
            Check.Equal(1, session.Debug!.Records.Count, "bounded record count");
            Check.True(session.Debug.DroppedRecords > 0, "overflow explicit");
            Check.Equal(SessionDebugKind.Fault, session.Debug.FirstFault!.Kind, "fault retained separately");
            Check.Equal("failure.spire", session.Debug.FirstFault.Source!.Value.Source, "first fault source");
            Check.Throws<NotSupportedException>(() => ((IList<SessionDebugRecord>)session.Debug.Records).Clear(), "readonly trace");
        });

        Check.Run("RANDOM a supplied versioned profile uses arbitrary stream state and resumes exact consumption", () =>
        {
            var rules = new RuleSetBuilder().Add(new EnginePackageBuilder("custom-random").Build()).Build();
            var session = new Session(rules, 1, ["player"], randomProfile: new CounterProfile());
            session.Random.SeedStream("custom", 7);
            Check.Equal(7L, session.Random.DrawInt("custom", 0, 10), "custom mapping and explicit seed");
            var saved = session.Random.Capture();
            Check.Equal(1, saved.Streams.Single().BackendState!.Count, "not restricted to xoshiro's four words");
            Check.Equal(8L, session.Random.DrawInt("custom", 0, 10), "next custom value");
            session.Random.Restore(saved);
            Check.Equal(8L, session.Random.DrawInt("custom", 0, 10), "restore uses supplied generator");
            Check.Equal(2L, session.Random.TotalDraws, "consumption restored");
            Check.True(session.Identity.Contains("test/counter@1") && session.Identity.Contains("test/modulo@1"), "profile enters identity");
            Check.Fault(EngineFaultCode.SnapshotInvalid, () => new RandomService(1).Restore(saved), "cross-profile snapshot rejected");
        });
    }

    private sealed class CounterProfile : IRandomProfile
    {
        public string AlgorithmId => "test/counter@1";
        public string DrawProfileId => "test/modulo@1";
        public IRandomGenerator Create(ulong seed) => new CounterGenerator(seed);
        public long DrawBounded(Func<ulong> next, long minInclusive, long maxExclusive) =>
            minInclusive + (long)(next() % (ulong)(maxExclusive - minInclusive));
        public double DrawDouble(Func<ulong> next) => (next() >> 11) * (1.0 / 9007199254740992.0);
        public bool DrawBool(Func<ulong> next) => (next() & 1) != 0;
    }
    private sealed class CounterGenerator(ulong state) : IRandomGenerator
    {
        private ulong _state = state;
        public ulong Next() => _state++;
        public IReadOnlyList<ulong> Capture() => [_state];
        public void Restore(IReadOnlyList<ulong> words) => _state = words.Single();
    }
}
