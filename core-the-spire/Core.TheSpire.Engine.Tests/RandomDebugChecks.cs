using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class RandomDebugChecks
{
    public static void Run()
    {
        var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile("""
            using namespace random-debug;
            dsl_version = "core/0.3" start = [Setup]
            entity Item { fields = { Number = { type = Int default = 0 } } }
            collection Items { element = Item ordered = true }
            procedure Setup { locals = { item = Binding } body = {
                seedStream = { stream = "test" seed = 12345 }
                repeat = { count = 3 body = { create = { type = Item scope = frameScope() into = item collection = Items } } }
            } }
            action Roll { locals = { value = Int picked = Binding } body = {
                sampleInt = { stream = "test" min = 0 max = 100 into = value }
                samplePick = { stream = "test" candidates = collection(Items) into = picked }
                shuffle = { collection = Items stream = "test" }
                sampleFloat32 = { stream = "test" into = value }
            } }
            action Invalid { locals = { value = Int } body = {
                sampleInt = { stream = "test" min = 1 max = 1 into = value }
            } }
            action Choose { locals = { value = Int answer = Bool } body = {
                sampleInt = { stream = "test" min = 0 max = 100 into = value }
                decide = { participant = participant(1) kind = Bool prompt = "continue" into = answer }
                sampleInt = { stream = "test" min = 0 max = 100 into = value }
            } }
            """, "random-debug.spire")).Build();
        Session New(SessionDebugOptions? options) => new(rules, 1, ["player"], debug: options,
            randomAlgorithmId: RandomService.VanillaAlgorithmId, randomDrawProfileId: RandomService.VanillaDrawProfileId);
        void Roll(Session s) => s.Submit(rules.Actions.Single(a => a.QualifiedId == "random-debug:Roll"), s.Participant(0), []);

        Check.Run("DEBUG random calls preserve actual parameters, results, candidate order and DSL source", () =>
        {
            var s = New(new()); s.Start();
            var items = s.State.CollectionMembers(rules.Collections.Single().Id).ToArray();
            Roll(s);
            var records = s.Debug!.Capture().Where(r => r.Kind == SessionDebugKind.RandomCall).ToArray();
            var calls = records.Select(r => r.Random!).ToArray();
            Check.Sequence(new[] { "int", "pick", "shuffle", "shuffle", "double" }, calls.Select(c => c.Operation), "individual API calls, including every swap");
            // Independent source vector: tools/rng-oracle/vanilla_rng_oracle.py, Rng(12345), bounds 100/3/3/2.
            Check.Sequence(new[] { "74", "0", "2", "0", "0.5551828553264562" }, calls.Select(c => c.Result), "actual sampled results");
            Check.Sequence(new[] { "100", "3", "3", "2", "1" }, calls.Select(c => c.Maximum), "bounds survive recording");
            Check.Sequence(items, calls[1].Candidates!, "pick candidates copied before sampling");
            Check.Sequence(items, calls[2].Candidates!, "first shuffle candidates before swap");
            Check.Sequence(items.Take(2), calls[3].Candidates!, "active shuffle prefix");
            for (var i = 0; i < calls.Length; i++)
            {
                Check.Equal((long)i, calls[i].Before, "per-stream cursor before");
                Check.Equal(i + 1L, calls[i].After, "per-stream cursor after");
                Check.Equal(calls[i].Before, calls[i].TotalBefore, "total cursor before");
                Check.Equal(calls[i].After, calls[i].TotalAfter, "total cursor after");
                Check.Equal("random-debug.spire", records[i].Source!.Value.Source, "source survives into each draw");
            }
            Check.Throws<NotSupportedException>(() => ((IList<InstanceId>)calls[1].Candidates!).Clear(), "immutable candidate snapshot");
            var before = s.Random.TotalDraws;
            Check.Equal(items[0], s.Random.DrawPick("single", [items[0]]), "singleton still sampled");
            Check.Equal(before + 1, s.Random.TotalDraws, "singleton consumes the source draw");
            var singleton = s.Debug.Records.Last().Random!;
            Check.Equal("1", singleton.Maximum, "singleton bound retained");
            Check.Sequence(new[] { items[0] }, singleton.Candidates!, "singleton candidate retained");
        });

        Check.Run("DEBUG pull frequency and independent sessions cannot change state or random calls", () =>
        {
            var disabled = New(null); var frequent = New(new()); var sparse = New(new());
            disabled.Start(); frequent.Start(); sparse.Start();
            for (var i = 0; i < 3; i++)
            {
                _ = frequent.Debug!.Capture(); _ = frequent.Random.Diagnostics();
                Roll(disabled); Roll(frequent); Roll(sparse);
                _ = frequent.Debug.Capture();
                Check.Equal(disabled.State.StateHash(), frequent.State.StateHash(), "trace does not change state");
                Check.Sequence(disabled.Random.Diagnostics(), frequent.Random.Diagnostics(), "trace does not change RNG");
                Check.Sequence(frequent.Random.Diagnostics(), sparse.Random.Diagnostics(), "pull frequency does not change RNG");
            }
            Check.Equal(frequent.Debug!.Records.Count, sparse.Debug!.Records.Count, "pulling does not consume records");
            var old = sparse.Debug.Capture(); Roll(frequent);
            Check.Sequence(old, sparse.Debug.Capture(), "another session cannot append here");
            Check.Equal(15L, sparse.Random.TotalDraws, "another session cannot advance this stream");
            Check.Equal(15, old.Count(r => r.Kind == SessionDebugKind.RandomCall), "all individual calls retained");
        });

        Check.Run("DEBUG truncated candidates and capacity loss are explicit without changing gameplay", () =>
        {
            var limited = New(new(MaxCandidates: 1)); var overflow = New(new(MaxRecords: 1)); var normal = New(null);
            limited.Start(); overflow.Start(); normal.Start(); Roll(limited); Roll(overflow); Roll(normal);
            var pick = limited.Debug!.Records.Single(r => r.Random?.Operation == "pick").Random!;
            Check.True(pick.CandidatesTruncated, "candidate loss explicit");
            Check.Equal(3, pick.CandidateCount, "full count retained"); Check.Equal(1, pick.Candidates!.Count, "candidate storage bounded");
            Check.Equal(1, overflow.Debug!.Records.Count, "shared record budget enforced");
            Check.True(overflow.Debug.DroppedRecords > 0, "overflow explicit");
            Check.Equal(normal.State.StateHash(), overflow.State.StateHash(), "overflow does not change state");
            Check.Sequence(normal.Random.Diagnostics(), limited.Random.Diagnostics(), "candidate limit does not change RNG");
            var bad = New(new()); bad.Start(); var before = bad.Random.Capture();
            Check.Fault(EngineFaultCode.ValueOutOfRange, () => bad.Submit(rules.Actions.Single(a => a.QualifiedId == "random-debug:Invalid"), bad.Participant(0), []), "invalid range rejected");
            Check.Equal(before.TotalDraws, bad.Random.TotalDraws, "invalid range consumes nothing");
            Check.DoesNotContain(bad.Debug!.Records, r => r.Kind == SessionDebugKind.RandomCall, "invalid range is not a successful draw");
        });

        Check.Run("DEBUG random trace survives decision suspension without replaying or consuming invalid answers", () =>
        {
            var s = New(new()); s.Start();
            s.Submit(rules.Actions.Single(a => a.QualifiedId == "random-debug:Choose"), s.Participant(0), []);
            var pending = s.PendingDecision!; var state = s.State.StateHash();
            var first = s.Debug!.Records.Single(r => r.Random is not null);
            Check.Equal(1L, s.Random.TotalDraws, "one draw before suspension");
            Check.Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(pending.RequestId + 1, Flag: true)), "stale answer rejected");
            Check.Equal(state, s.State.StateHash(), "invalid answer preserves state");
            Check.Equal(1L, s.Random.TotalDraws, "invalid answer does not draw");
            _ = s.Debug.Capture();
            s.Answer(new DecisionAnswer(pending.RequestId, Flag: true));
            var calls = s.Debug.Capture().Where(r => r.Random is not null).ToArray();
            Check.Equal(2, calls.Length, "continuation records one further draw");
            Check.Equal(first, calls[0], "pre-decision record not replaced or replayed");
            Check.Equal(1L, calls[1].Random!.Before, "continuation uses next cursor");
            Check.True(first.Sequence < calls[1].Sequence, "trace order survives suspension");
            Check.Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(pending.RequestId, Flag: true)), "duplicate answer rejected");
            Check.Equal(2L, s.Random.TotalDraws, "duplicate answer does not draw");
        });
    }
}
