using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class DebugSnapshotChecks
{
    private const string Source = """
        using namespace debug-copy;
        dsl_version = "core/0.3" start = [Setup]
        entity Item { fields = {
            Number = { type = Int default = 7 }
            Text = { type = String default = "original" }
            Numbers = { type = List(Int, length = 16) }
        } }
        collection Items { element = Item ordered = true }
        procedure Setup { locals = { item = Binding } body = {
            repeat = { count = 3 body = { create = { type = Item scope = frameScope() into = item collection = Items } } }
        } }
        event Before { payload = { item = Ref(Item) label = String } }
        event After { payload = { item = Ref(Item) } }
        procedure Rewrite { parameters = { item = Ref(Item) label = String } body = {
            set = { target = item field = Item.Number value = 99 }
            emit = { event = After payload = { item = item } }
        } }
        subscription Rewrite { event = Before reaction = Rewrite }
        action Run { locals = { picked = Binding } body = {
            samplePick = { stream = "test" candidates = collection(Items) into = picked }
            emit = { event = Before payload = { item = picked label = "payload" } }
            destroy = { target = picked }
        } }
        """;

    public static void Run()
    {
        var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile(Source, "debug-copy.spire")).Build();
        Session New(SessionDebugOptions? options) => new(rules, 42, ["player"], debug: options);
        void Start(Session s)
        {
            s.Start();
            foreach (var instance in s.State.AllInstances()) s.State.SetField(instance, rules.Types.Single().FieldIndex("Numbers"), Value.OfInts(1, 2, 3));
        }
        void Submit(Session s) => s.Submit(rules.Actions.Single(), s.Participant(0), []);
        SessionDebugRecord Event(Session s, string name) => s.Debug!.Records.Single(r => r.Subject == "debug-copy:" + name && r.Kind == SessionDebugKind.EventEmit);
        SessionDebugRecord Pick(Session s) => s.Debug!.Records.Single(r => r.Random is not null);

        Check.Run("DEBUG copied event and candidate fields survive listener mutation and entity destruction", () =>
        {
            var s = New(new(CaptureValues: true)); Start(s);
            var candidates = s.State.CollectionMembers(rules.Collections.Single().Id).ToArray();
            Submit(s);
            var before = Event(s, "Before").Snapshot!; var after = Event(s, "After").Snapshot!;
            Check.True(before.Complete && after.Complete, "complete copies at both emit points");
            Check.Equal(ValueKind.Ref, before.Values[0].Kind, "payload retains its value kind");
            Check.Equal("payload", before.Values[1].Scalar, "typed string payload");
            var entity = before.Entities.Single();
            Check.Equal(entity.Instance.Value.ToString(), before.Values[0].Scalar, "payload and entity are associated");
            Check.False(s.State.Exists(entity.Instance), "payload really is destroyed by the stable boundary");
            Check.Equal("debug-copy:Item", entity.Type, "type survives destruction");
            Check.Equal("7", entity.Fields.Single(f => f.Name == "Number").Value.Scalar, "copy precedes listener execution");
            Check.Equal("99", after.Entities.Single().Fields.Single(f => f.Name == "Number").Value.Scalar, "nested event sees actual changed value");
            Check.Sequence(new long[] { 1, 2, 3 }, entity.Fields.Single(f => f.Name == "Numbers").Value.Items!, "typed list copied");
            var random = Pick(s);
            Check.Sequence(candidates, random.Random!.Candidates!, "sample order retained");
            Check.Sequence(candidates, random.Snapshot!.Entities.Select(e => e.Instance), "candidate copies retain original order");
            Check.True(random.Snapshot.Complete, "candidate fields remain complete after selected entity dies");
            Check.True(random.Snapshot.Entities.All(e => e.Fields.Single(f => f.Name == "Number").Value.Scalar == "7"), "all candidate fields reflect sampling time");
            Check.Throws<NotSupportedException>(() => ((IList<SessionDebugValueSnapshot>)before.Values).Clear(), "payload copies are readonly");
            Check.Throws<NotSupportedException>(() => ((IList<SessionDebugEntitySnapshot>)before.Entities).Clear(), "entity copies are readonly");
            Check.Throws<NotSupportedException>(() => ((IList<SessionDebugFieldSnapshot>)entity.Fields).Clear(), "field copies are readonly");
            Check.Throws<NotSupportedException>(() => ((IList<long>)entity.Fields.Single(f => f.Name == "Numbers").Value.Items!)[0] = 9, "list copies are readonly");
        });

        Check.Run("DEBUG copied data reports entity, value, list, string and candidate limits", () =>
        {
            foreach (var options in new[] { new SessionDebugOptions(CaptureValues: true, MaxSnapshotEntities: 1),
                new(CaptureValues: true, MaxSnapshotValues: 1), new(CaptureValues: true, MaxSnapshotValues: 5),
                new(CaptureValues: true, MaxTextLength: 4), new(CaptureValues: true, MaxCandidates: 1) })
            {
                var s = New(options); Start(s); Submit(s);
                var snapshot = Pick(s).Snapshot!;
                Check.False(snapshot.Complete, "truncated random copy cannot claim completeness");
                Check.True(snapshot.Truncated, "loss is explicit");
                Check.True(snapshot.Entities.Count <= options.MaxSnapshotEntities, "entity budget");
                if (options.MaxSnapshotValues == 1)
                {
                    var payload = Event(s, "Before").Snapshot!;
                    Check.Equal(2, payload.ValueCount, "original payload count survives");
                    Check.Equal(1, payload.Values.Count, "payload budget applied before copying");
                    Check.True(payload.Truncated, "payload value loss explicit");
                }
                if (options.MaxSnapshotValues == 5)
                {
                    var list = snapshot.Entities[0].Fields.Single(f => f.Name == "Numbers").Value;
                    Check.Equal(3, list.OriginalLength, "original list length");
                    Check.Sequence(new long[] { 1, 2 }, list.Items!, "shared value budget bounds list elements");
                    Check.True(list.Truncated, "list loss explicit");
                }
                if (options.MaxTextLength == 4)
                {
                    var text = snapshot.Entities[0].Fields.Single(f => f.Name == "Text").Value;
                    Check.Equal("orig", text.Scalar, "text copy bounded"); Check.Equal(8, text.OriginalLength, "original text length");
                    Check.True(text.Truncated, "text loss explicit");
                    Check.True(s.Debug!.Records.Any(r => r.TextTruncated), "subject/detail text loss is explicit too");
                }
                if (options.MaxCandidates == 1) Check.True(Pick(s).Random!.CandidatesTruncated, "original candidate loss retained");
            }
            foreach (var options in new[] { new SessionDebugOptions(MaxSnapshotEntities: 0), new(MaxSnapshotValues: 0) })
                Check.Throws<EngineFaultException>(() => New(options), "invalid snapshot budget rejected");
        });

        Check.Run("DEBUG candidate references retain one bounded historical layer without expanding events", () =>
        {
            var linked = new RuleSetBuilder().Add(EngineDslCompiler.Compile("""
                using namespace linked-copy; dsl_version = "core/0.3"
                entity Node { fields = {
                  Name = { type = String default = "original" }
                  Next = { type = Ref(Node) optional = true }
                  Links = { type = List(Ref(Node), length = 4) }
                } }
                collection Roots { element = Node ordered = true }
                event Seen { payload = { node = Ref(Node) } }
                action Run { locals = { picked = Binding definition = Binding } body = {
                  samplePick = { stream = "test" candidates = collection(Roots) into = picked }
                  emit = { event = Seen payload = { node = picked } }
                  let = { into = definition value = field(picked, Node.Next) }
                  set = { target = definition field = Node.Name value = "changed" }
                  forEach = { query = collection(Roots) item = root body = { destroy = { target = root } } }
                  destroy = { target = definition }
                } }
                """)).Build();
            (Session Session, InstanceId[] Roots, InstanceId Definition, InstanceId Deep) Create(SessionDebugOptions? options)
            {
                var session = new Session(linked, 42, ["player"], debug: options); session.Start();
                var type = linked.Types.Single();
                var roots = Enumerable.Range(0, 2).Select(_ => session.State.CreateInstance(type.Id, session.State.RootScope)).ToArray();
                var definition = session.State.CreateInstance(type.Id, session.State.RootScope);
                var deep = session.State.CreateInstance(type.Id, session.State.RootScope);
                foreach (var root in roots)
                {
                    session.State.SetField(root, type.FieldIndex("Next"), Value.OfRef(definition));
                    session.State.AddToCollection(linked.Collections.Single().Id, root);
                }
                session.State.SetField(roots[0], type.FieldIndex("Links"), Value.OfRefs(definition, definition));
                session.State.SetField(definition, type.FieldIndex("Next"), Value.OfRef(deep));
                session.State.SetField(definition, type.FieldIndex("Links"), Value.OfRefs(roots[0]));
                return (session, roots, definition, deep);
            }
            var plain = Create(null); plain.Session.Submit(linked.Actions.Single(), plain.Session.Participant(0), []);
            foreach (var options in new[] { new SessionDebugOptions(CaptureValues: true),
                new(CaptureValues: true, CaptureCandidateReferences: true),
                new(CaptureValues: true, CaptureCandidateReferences: true, MaxSnapshotEntities: 2),
                new(CaptureValues: true, CaptureCandidateReferences: true, MaxSnapshotValues: 10),
                new(CaptureValues: true, CaptureCandidateReferences: true, MaxCandidates: 1),
                new(CaptureCandidateReferences: true), new(CaptureValues: true, CaptureCandidateReferences: true, MaxRecords: 1) })
            {
                var sample = Create(options); var s = sample.Session;
                s.Submit(linked.Actions.Single(), s.Participant(0), []);
                Check.Equal(plain.Session.Identity, s.Identity, "reference copying preserves identity");
                Check.Equal(plain.Session.State.StateHash(), s.State.StateHash(), "reference copying preserves real changes and destruction");
                Check.Sequence(plain.Session.Random.Diagnostics(), s.Random.Diagnostics(), "reference copying preserves draws");
                if (!options.CaptureValues || options.MaxRecords == 1)
                { Check.True(s.Debug!.Records.All(r => r.Snapshot is null), "disabled values or record exhaustion creates no snapshots"); continue; }
                var snapshot = s.Debug!.Records.Single(r => r.Random is not null).Snapshot!;
                Check.Sequence(sample.Roots.Take(options.MaxCandidates), snapshot.Entities.Take(Math.Min(2, options.MaxCandidates)).Select(e => e.Instance), "root order has priority over references");
                Check.Equal(options.CaptureCandidateReferences ? 1 : 0, snapshot.ReferenceDepth, "requested reference boundary explicit");
                if (!options.CaptureCandidateReferences)
                    Check.Equal(2, snapshot.Entities.Count, "old calls copy only root candidates");
                else if (options.MaxSnapshotEntities == 2 || options.MaxSnapshotValues == 10 || options.MaxCandidates == 1)
                    Check.False(snapshot.Complete, "entity, value or root prefix loss remains explicit");
                else
                {
                    Check.True(snapshot.Complete, "one layer copied completely");
                    Check.Equal(3, snapshot.Entities.Count, "shared and cyclic references deduplicate");
                    var definition = snapshot.Entities.Single(e => e.Instance == sample.Definition);
                    Check.Equal("original", definition.Fields.Single(f => f.Name == "Name").Value.Scalar, "indirect value survives its later rewrite and destruction");
                    Check.False(s.State.Exists(sample.Definition), "definition actually destroyed");
                    Check.DoesNotContain(snapshot.Entities, e => e.Instance == sample.Deep, "second reference layer is not traversed");
                    var before = s.Debug.Capture(); _ = s.Debug.Capture();
                    Check.Sequence(before, s.Debug.Capture(), "pulls cannot mutate indirect snapshots");
                    var emitted = s.Debug.Records.Single(r => r.Kind == SessionDebugKind.EventEmit).Snapshot!;
                    Check.Equal(0, emitted.ReferenceDepth, "event rule remains direct payload references");
                    Check.Equal(1, emitted.Entities.Count, "candidate option never expands event graphs");
                }
            }
        });

        Check.Run("DEBUG value copying is opt-in, pull-only and isolated without changing gameplay or RNG", () =>
        {
            var sessions = new[] { New(null), New(new()), New(new(CaptureValues: true)), New(new(CaptureValues: true)) };
            foreach (var s in sessions) Start(s);
            for (var i = 0; i < 2; i++)
            {
                _ = sessions[2].Debug!.Capture();
                foreach (var s in sessions) Submit(s);
                foreach (var s in sessions.Skip(1))
                {
                    Check.Equal(sessions[0].Identity, s.Identity, "debug configuration does not change identity");
                    Check.Equal(sessions[0].State.StateHash(), s.State.StateHash(), "state is unchanged");
                    Check.Sequence(sessions[0].Random.Diagnostics(), s.Random.Diagnostics(), "RNG is unchanged");
                    Check.Sequence(sessions[0].LegalActions(sessions[0].Participant(0)).Select(a => a.Definition.QualifiedId),
                        s.LegalActions(s.Participant(0)).Select(a => a.Definition.QualifiedId), "choices unchanged");
                }
            }
            Check.True(sessions[1].Debug!.Records.All(r => r.Snapshot is null), "existing debug calls do not enable value capture");
            Check.Equal(sessions[2].Debug!.Records.Count, sessions[3].Debug!.Records.Count, "pull frequency adds no records");
            var saved = sessions[3].Debug!.Capture(); Submit(sessions[2]);
            Check.Sequence(saved, sessions[3].Debug!.Capture(), "another session cannot change these snapshots");
            var overflow = New(new(MaxRecords: 1, CaptureValues: true)); Start(overflow); Submit(overflow);
            Check.Equal(1, overflow.Debug!.Records.Count, "record budget enforced");
            Check.True(overflow.Debug.DroppedRecords > 0, "overflow remains explicit");
            Check.True(overflow.Debug.Records.All(r => r.Snapshot is null), "no snapshots allocated past record capacity");
            var missing = New(new(CaptureValues: true)); Start(missing);
            missing.Random.DrawPick("outside", [new InstanceId(int.MaxValue)]);
            var absent = missing.Debug!.Records.Last().Snapshot!;
            Check.True(absent.Entities.Single().Missing && !absent.Complete, "direct random API reports unavailable entity without changing draw behavior");
        });
    }
}
