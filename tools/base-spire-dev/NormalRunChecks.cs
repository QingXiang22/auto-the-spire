using CoreTheSpire.Engine;
using System.Text.Json;

internal static partial class PackageChecks
{
    public static int RunNormalRunOnly(DslPackage package)
    {
        RunNormalOpeningChecks(package);
        RunSourceOpeningChecks(package);
        RunUnsupportedNormalOpeningChecks(package);
        RunMapGaussianChecks(package);
        RunSourceMapChecks(package);
        return 0;
    }

    private static void RunNormalOpeningChecks(DslPackage package)
    {
        var session = package.CreateSession(777, ["player"], "777"); session.Start(); Healthy(session);
        var modified = package.CreateSession(777, ["player"], "777"); modified.Start();
        Submit(modified, "Draw");
        True(!modified.LegalActions(modified.Participant(0)).Any(a => a.Definition.QualifiedId == "base-spire:StartNormalRun"), "a modified development state cannot be reset into a normal opening");
        Submit(session, "StartNormalRun");
        Equal(SessionStatus.AwaitingDecision, session.Status, "normal entry pauses for real Neow options");
        Equal("base-spire:ironclad", TextField(session, Player(session), "Player", "CharacterId"), "normal entry initializes Ironclad");
        Equal(DecisionResponseKind.EntityRef, session.PendingDecision!.Kind, "Neow offers relic instances");
        Equal(3, session.PendingDecision.Candidates.Count, "source Neow has three options");
        var request = session.PendingDecision;
        var conch = request.Candidates.Single(o => TextField(session, o, "NeowOption", "RelicId") == "base-spire:booming-conch");
        var state = session.State.StateHash(); var draws = session.Random.TotalDraws;
        Throws<EngineFaultException>(() => session.Answer(new DecisionAnswer(request.RequestId, Instances: [Player(session)])));
        Equal(state, session.State.StateHash(), "invalid choice preserves state"); Equal(draws, session.Random.TotalDraws, "invalid choice preserves random streams");
        session.Answer(new DecisionAnswer(request.RequestId, Instances: [conch])); Healthy(session);
        Equal(DecisionResponseKind.Confirm, session.PendingDecision!.Kind, "Neow waits for leave confirmation after acquisition");
        Equal(2, Members(session, "Relics").Count, "the selected real relic joins the starter relic");
        Throws<EngineFaultException>(() => session.Answer(new DecisionAnswer(request.RequestId, Instances: [conch])));
        session.Answer(new DecisionAnswer(session.PendingDecision.RequestId, Flag: true)); Healthy(session);
        var run = session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
        var nodes = Members(session, "MapNodes");
        Equal(56, nodes.Count, "seed 777 source map includes the ancient and boss");
        Equal(270L, session.Random.DrawCount("base-spire:run:act_1_map"), "normal entry uses the complete source map stream");
        Equal("map", TextField(session, Player(session), "Player", "Room"), "leaving Neow returns to the real map");
        var choose = session.LegalActions(session.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
        var boss = nodes.Single(n => IntField(session, n, "MapNode", "Row") == 16);
        state = session.State.StateHash(); draws = session.Random.TotalDraws;
        Throws<EngineFaultException>(() => session.Submit(choose.Definition, session.Participant(0), [Value.OfRef(boss)]));
        Equal(state, session.State.StateHash(), "illegal map point changes no state"); Equal(draws, session.Random.TotalDraws, "illegal map point draws no RNG");
        session.Submit(choose.Definition, session.Participant(0), choose.Bindings[0]); Healthy(session);
        Equal(1, session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).Count(), "first source weak encounter has one Fuzzy Wurm");
        Equal(55L, EnemyHp(session), "seed 777 source Niche HP sample");
        Equal(1L, session.Random.DrawCount("base-spire:run:niche"), "one actual spawn HP draw");
        Equal("combat", TextField(session, run, "Run", "Phase"), "visit phase records real combat");
        Equal(1L, IntField(session, Player(session), "Player", "TurnNumber"), "normal entry starts the first turn");
        Equal(3L, IntField(session, Player(session), "Player", "Energy"), "Conch does not grant elite-only energy in a Monster room");
        Equal(5, Hand(session).Count, "normal first turn draws five real combat copies");
        Equal(10, Members(session, "RunDeck").Count, "permanent deck keeps its ten instances");
        var visit = Members(session, "RunVisits").Single();
        Equal("base-spire:fuzzy-wurm-crawler-weak", TextField(session, visit, "RunVisit", "EncounterId"), "normal visit consumes the source queue");
        True(Hand(session).All(card => !Members(session, "RunDeck").Contains(card)), "combat copies and permanent instances are distinct");
        state = session.State.StateHash(); draws = session.Random.TotalDraws;
        Throws<EngineFaultException>(() => session.Submit(choose.Definition, session.Participant(0), choose.Bindings[0]));
        Equal(state, session.State.StateHash(), "repeated visit is rejected without mutation"); Equal(draws, session.Random.TotalDraws, "repeated visit consumes no RNG");
        Console.WriteLine("ok   ENV-T05 normal public actions: real Neow, source map and queue, HP draw and first-turn combat copies");
    }

    private static void RunUnsupportedNormalOpeningChecks(DslPackage package)
    {
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-opening-vectors.json")!);
        var vector = data.RootElement.GetProperty("regression_vectors").EnumerateArray().Single();
        Equal("20261021", vector.GetProperty("seed").GetString(), "the original normal-opening seed is retained");
        var s = package.CreateSession(20261021, ["player"]); s.Start(); Submit(s, "StartNormalRun");
        var request = s.PendingDecision!;
        var actual = request.Candidates.Select(o => TextField(s, o, "NeowOption", "RelicId"));
        True(actual.SequenceEqual(vector.GetProperty("neow_options").EnumerateArray().Select(id => id.GetString())), "original seed exposes actual source choices instead of a dummy Bool");
        Equal(vector.GetProperty("up_front_draws").GetInt64(), s.Random.DrawCount("base-spire:run:up_front"), "original seed consumes its full source opening");
        var state = s.State.StateHash(); var draws = s.Random.TotalDraws;
        Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request.RequestId, Instances: [request.Candidates[0]])));
        True(s.Fault!.Message.Contains("unsupported_capability", StringComparison.Ordinal), "original seed's unimplemented effect remains explicit");
        Equal(state, s.State.StateHash(), "unsupported real choice grants no fake relic and changes no visit");
        Equal(draws, s.Random.TotalDraws, "unsupported real choice does not reroll");
        Console.WriteLine("ok   ENV-T05 original seed 20261021: real source Neow options and explicit unsupported effect, no fake normal run");
    }

    private static void RunSourceOpeningChecks(DslPackage package)
    {
        using var copy = new Copy(package.Root);
        copy.Include("opening-probes.spire", """
            action BuildOpeningPools { role = player body = { call = { procedure = InitializeSourceOpeningPools args = { run = first(type(Run)) } } } }
            action ResolveNeowProbe { role = player parameters = { option = Ref(NeowOption) } body = {
              call = { procedure = ResolveSourceNeow args = { owner = first(type(Player)) run = first(type(Run)) visit = field(option, NeowOption.Visit) option = option } }
            } }
            action BuildNeowOffers { role = player locals = { visit = Binding } body = {
              create = { type = NeowVisit scope = entityScope(first(type(Run))) into = visit fields = { Run = first(type(Run)) } }
              call = { procedure = GenerateSourceNeowOptions args = { visit = visit } }
            } }
            """);
        var compiled = copy.Load();
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-opening-vectors.json")!);
        Equal(10, data.RootElement.GetProperty("vectors").GetArrayLength(), "all required opening seeds remain present");
        foreach (var vector in data.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var seed = vector.GetProperty("seed").GetString()!;
            var s = compiled.CreateSession(1, ["player"], seed); s.Start(); Healthy(s);
            Submit(s, "BuildOpeningPools");
            Equal(vector.GetProperty("up_front_draws").GetInt64(), s.Random.DrawCount("base-spire:run:up_front"), "source opening rejection and shuffle cursor: " + seed);
            var definitions = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:OpeningDefinition").Id);
            var sourceDefinitions = data.RootElement.GetProperty("definitions").EnumerateArray().ToArray();
            Equal(sourceDefinitions.Length, definitions.Count, "full source pools remain, including unselected content");
            for (var i = 0; i < definitions.Count; i++)
            {
                var expected = sourceDefinitions[i]; var actual = definitions[i];
                foreach (var (json, field) in new[] { ("id", "DefinitionId"), ("category", "Category"), ("room", "Room"), ("rarity", "Rarity") })
                    Equal(expected.GetProperty(json).GetString(), TextField(s, actual, "OpeningDefinition", field), "source pool order and eligibility: " + field);
                Equal(expected.GetProperty("act").GetInt64(), IntField(s, actual, "OpeningDefinition", "Act"), "source act membership");
                Equal(expected.GetProperty("weak").GetBoolean(), s.State.GetField(actual, s.RuleSet.TypeByQualifiedId("base-spire:OpeningDefinition").FieldIndex("Weak")).AsBool(), "source weak encounter eligibility");
                var tags = s.State.RelationTargets(s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:OpeningTags").Id, actual).Select(tag => TextField(s, tag, "OpeningTag", "Name"));
                True(tags.SequenceEqual(expected.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString())), "source encounter exclusion tags");
            }
            var plans = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:RunRoomPlan").Id);
            Equal(vector.GetProperty("plans").GetArrayLength(), plans.Count, "all three acts keep their full source queues");
            foreach (var expected in vector.GetProperty("plans").EnumerateArray())
            {
                var plan = plans.Single(p => IntField(s, p, "RunRoomPlan", "Act") == expected.GetProperty("act").GetInt64()
                    && IntField(s, p, "RunRoomPlan", "Position") == expected.GetProperty("position").GetInt64()
                    && TextField(s, p, "RunRoomPlan", "Room") == expected.GetProperty("room").GetString());
                var definition = s.State.GetField(plan, s.RuleSet.TypeByQualifiedId("base-spire:RunRoomPlan").FieldIndex("Definition")).AsRef();
                Equal(expected.GetProperty("id").GetString(), TextField(s, definition, "OpeningDefinition", "DefinitionId"), "source ordered room queue");
            }
            var lists = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:OpeningList").Id);
            var itemsRelation = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:OpeningItems").Id;
            foreach (var expected in vector.GetProperty("bags").EnumerateArray())
            {
                var bag = lists.Single(l => TextField(s, l, "OpeningList", "Category") == expected.GetProperty("category").GetString()
                    && TextField(s, l, "OpeningList", "Rarity") == expected.GetProperty("rarity").GetString());
                var ids = s.State.RelationTargets(itemsRelation, bag).OrderBy(i => IntField(s, i, "OpeningListItem", "Position"))
                    .Select(i => s.State.GetField(i, s.RuleSet.TypeByQualifiedId("base-spire:OpeningListItem").FieldIndex("Definition")).AsRef())
                    .Select(d => TextField(s, d, "OpeningDefinition", "DefinitionId")).ToArray();
                True(ids.SequenceEqual(expected.GetProperty("ids").EnumerateArray().Select(id => id.GetString())), "both source relic bag orders");
            }
            Submit(s, "BuildNeowOffers");
            var options = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:NeowOption").Id).OrderBy(o => IntField(s, o, "NeowOption", "Position"));
            True(options.Select(o => TextField(s, o, "NeowOption", "RelicId")).SequenceEqual(vector.GetProperty("neow_options").EnumerateArray().Select(id => id.GetString())), "full source Neow options and exclusions");
            Equal(vector.GetProperty("neow_draws").GetInt64(), s.Random.DrawCount("base-spire:event:neow"), "source Neow cursor");
            var unsupported = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:NeowOption").Id)
                .First(o => !s.State.GetField(o, s.RuleSet.TypeByQualifiedId("base-spire:NeowOption").FieldIndex("EffectSupported")).AsBool());
            Set(s, Player(s), "Player", "CurrentRun", Value.OfRef(s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single()));
            var state = s.State.StateHash(); var draws = s.Random.TotalDraws;
            Throws<EngineFaultException>(() => Submit(s, "ResolveNeowProbe", unsupported));
            True(s.Fault!.Message.Contains("unsupported_capability", StringComparison.Ordinal), "unmigrated Neow choices report their boundary");
            Equal(state, s.State.StateHash(), "unsupported Neow grants no empty relic or visit mutation");
            Equal(draws, s.Random.TotalDraws, "unsupported Neow consumes no additional randomness");
            Console.WriteLine("ok   ENV-T05 source opening: bags, all act queues, rejections and Neow offers: " + seed);
        }
    }

    private static void RunSourceMapChecks(DslPackage package)
    {
        using var copy = new Copy(package.Root);
        copy.Include("map-probes.spire", """
            action PrepareRoomProbe { role = player body = {
              call = { procedure = InitializeNormalIronclad args = { owner = first(type(Player)) } }
              call = { procedure = InitializeSourceOpeningPools args = { run = first(type(Run)) } }
              set = { target = first(type(Player)) field = Player.CurrentRun value = first(type(Run)) }
            } }
            action BuildSourceMap { role = player body = {
              call = { procedure = GenerateSourceActOneMap args = { owner = first(type(Player)) run = first(type(Run)) } }
            } }
            """);
        var compiled = copy.Load();
        using var vectors = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.act-one-map-vectors.json")!);
        Equal(10, vectors.RootElement.GetProperty("vectors").GetArrayLength(), "all required map seeds remain present");
        foreach (var vector in vectors.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var seed = vector.GetProperty("seed").GetString()!;
            var s = compiled.CreateSession(1, ["player"], seed, new SessionDebugOptions(MaxRecords: 100000));
            s.Start(); Healthy(s);
            if (seed == "1234") Submit(s, "PrepareRoomProbe");
            try { Submit(s, "BuildSourceMap"); }
            catch
            {
                Console.Error.WriteLine($"map {seed}: draws={s.Random.DrawCount("base-spire:run:act_1_map")}; " + string.Join(", ", new[] { "MapNode", "MapWork", "MapSegment", "MapSegmentKey" }.Select(name => name + "=" + s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + name).Id).Count())));
                Console.Error.WriteLine(s.Debug!.Records.LastOrDefault());
                throw;
            }
            Equal(vector.GetProperty("draws").GetInt64(), s.Random.DrawCount("base-spire:run:act_1_map"), "source map cursor: " + seed);
            var nodes = Members(s, "MapNodes");
            var expected = vector.GetProperty("cells").EnumerateArray().ToArray();
            Equal(expected.Length, nodes.Count, "source map node count: " + seed);
            var childRelation = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren");
            var parentRelation = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapParents");
            foreach (var cell in expected)
            {
                var node = nodes.Single(n => IntField(s, n, "MapNode", "Col") == cell.GetProperty("col").GetInt64() && IntField(s, n, "MapNode", "Row") == cell.GetProperty("row").GetInt64());
                Equal(cell.GetProperty("type").GetInt64(), IntField(s, node, "MapNode", "Kind"), "source point type");
                long Mask(int relation) => s.State.RelationTargets(relation, node).Sum(n => 1L << (int)IntField(s, n, "MapNode", "Col"));
                Equal(cell.GetProperty("children").GetInt64(), Mask(childRelation.Id), "source children");
                Equal(cell.GetProperty("parents").GetInt64(), Mask(parentRelation.Id), "source parents");
            }
            foreach (var name in new[] { "MapWork", "MapWorkGroup", "MapSegment", "MapSegmentKey" })
                Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + name).Id).Count(), "map scratch state is removed");
            if (seed == "1234")
            {
                var choose = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
                var state = s.State.StateHash(); var draws = s.Random.TotalDraws;
                Throws<EngineFaultException>(() => s.Submit(choose.Definition, s.Participant(0), choose.Bindings[0]));
                True(s.Fault!.Message.Contains("unsupported_capability", StringComparison.Ordinal), "source queue keeps its unmigrated first encounter");
                Equal(state, s.State.StateHash(), "unsupported encounter leaves the current node and visit untouched");
                Equal(draws, s.Random.TotalDraws, "unsupported encounter draws no spawn randomness");
            }
            Console.WriteLine("ok   ENV-T05 map source port: final cells, types, edges and cursor: " + seed);
        }
    }

    private static void RunMapGaussianChecks(DslPackage package)
    {
        using var copy = new Copy(package.Root);
        copy.Include("gaussian-probes.spire", """
            entity GaussianProbe { fields = { Result = { type = Int } } }
            procedure SetupGaussianProbe { locals = { probe = Binding } body = {
              create = { type = GaussianProbe scope = frameScope() into = probe }
              seedStream = { stream = "map-probe" seed = pureCall(SourceStreamSeed, pureCall(SourceRunSeed), "act_1_map") }
            } }
            action RestCount { role = player locals = { value = Int } body = {
              call = { procedure = SourceGaussianInt args = { stream = "map-probe" mean = 7 deviation = 1 min = 6 max = 7 } into = value }
              set = { target = first(type(GaussianProbe)) field = GaussianProbe.Result value = value }
            } }
            action UnknownCount { role = player locals = { value = Int } body = {
              call = { procedure = SourceGaussianInt args = { stream = "map-probe" mean = 12 deviation = 1 min = 10 max = 14 } into = value }
              set = { target = first(type(GaussianProbe)) field = GaussianProbe.Result value = value }
            } }
            """);
        File.WriteAllText(copy.Path("base.spire"), File.ReadAllText(copy.Path("base.spire")).Replace("start = [", "start = [SetupGaussianProbe, ", StringComparison.Ordinal));
        using var vectors = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.map-gaussian-vectors.json")!);
        var compiled = copy.Load();
        Equal(10, vectors.RootElement.GetProperty("vectors").GetArrayLength(), "all required map seeds remain present");
        foreach (var vector in vectors.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var session = compiled.CreateSession(1, ["player"], vector.GetProperty("seed").GetString());
            session.Start(); Healthy(session);
            var probe = session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:GaussianProbe").Id).Single();
            foreach (var call in vector.GetProperty("calls").EnumerateArray())
            {
                Equal(call.GetProperty("before").GetInt64(), session.Random.DrawCount("map-probe"), "source cursor before Gaussian");
                Submit(session, call.GetProperty("mean").GetInt32() == 7 ? "RestCount" : "UnknownCount");
                Equal(call.GetProperty("result").GetInt64(), IntField(session, probe, "GaussianProbe", "Result"), "source room count");
                Equal(call.GetProperty("after").GetInt64(), session.Random.DrawCount("map-probe"), "source rejection attempts consume two words each");
            }
        }
        Console.WriteLine("ok   ENV-T05: source Gaussian room counts and rejection cursors for ten fixed seeds");
    }

    private static string TextField(Session session, InstanceId entity, string type, string field)
    {
        var value = session.State.GetField(entity, session.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        return value.AsString();
    }
}
