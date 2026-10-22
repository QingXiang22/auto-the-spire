using System.Collections;
using System.Reflection;
using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class EventSelectionComparison
{
    private sealed record Eligibility(string Id, bool Natural, bool Allowed);
    private sealed record Boundary(string Selected, long Cursor, string[] Visited);
    private sealed record Result(Eligibility[] Eligibility, Boundary[] Boundaries, bool RandomUnchanged, bool QueriesReadOnly);
    private static readonly string[] Modes = ["default", "gold43", "gold44", "gold100", "gold148", "gold149", "gold150", "hp9", "hp10", "hp56", "hp57", "floor6", "floor7",
        "no-deck", "one-card", "eternal-deck", "empty-bag", "two-potions", "egg", "byrdpip", "paels-legion"];
    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use event-selection-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        var package = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        using var fixture = new Fixture(package.Root); var observed = DslPackage.Load(Path.Combine(fixture.Root, "package.json"));
        var cases = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" })
        foreach (var mode in Modes)
        {
            var left = Dsl(observed, seed, mode); var right = Native(seed, mode);
            var same = NormalPreflight.Json(left) == NormalPreflight.Json(right); passed &= same;
            if (left.Eligibility.Length != 31 || right.Eligibility.Length != 31 || left.Boundaries.Length != 41 || right.Boundaries.Length != 41
                || !left.RandomUnchanged || !right.RandomUnchanged || !left.QueriesReadOnly || !right.QueriesReadOnly)
                throw new InvalidOperationException("Event selection omitted full eligibility, source passes or read-only checks.");
            cases.Add(new { id = seed + ":" + mode, status = same ? "passed" : "diverged", left, right });
        }
        var report = new { schema = "dsl-migration/event-selection@1", scope = "controlled-component", reference, package_digest = package.Digest,
            fixture_digest = observed.Digest, status = passed ? "passed" : "diverged", independent_verified = passed, normal_run_verified = false,
            controlled_setup = "Both sides build their complete native three-act opening queues and starting Ironclad assets. First-act event eligibility is evaluated for all 31 source entries. Resource, deck, pet, potion, bag and floor variants are controlled inputs, not implemented event effects. The real selector executes 40 times per case through unvisited priority and exhausted fallback; cursor and final visited identities are compared at every boundary. All run/player RNG words and cursors are captured before queries/selection and required unchanged on each side. No pool reduction, reseed, compensation draw or reference rewrite.", cases };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }
    private static Result Dsl(DslPackage package, string seed, string mode)
    {
        var s = package.CreateSession(1, ["player"], seed); s.Start();
        void Submit(string name, params Value[] args)
        {
            s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), s.Participant(0), args);
            if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Event fixture stopped: " + s.Fault);
        }
        Submit("EventFixtureInit");
        InstanceId One(string name) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + name).Id).Single();
        long Number(InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field)).AsInt();
        string Text(InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field)).AsString();
        void Set(InstanceId id, string type, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
        var run = One("Run"); var player = One("Player"); var audit = One("EventFixture");
        if (mode.StartsWith("gold", StringComparison.Ordinal)) Set(player, "Player", "Gold", Value.Of(long.Parse(mode[4..])));
        if (mode.StartsWith("hp", StringComparison.Ordinal)) Set(player, "Player", "Hp", Value.Of(long.Parse(mode[2..])));
        if (mode.StartsWith("floor", StringComparison.Ordinal)) Set(run, "Run", "TotalFloor", Value.Of(long.Parse(mode[5..])));
        if (mode is "no-deck" or "one-card") Submit("EventFixtureTrimDeck", Value.Of(mode == "no-deck" ? 0 : 1));
        if (mode == "eternal-deck") Set(audit, "EventFixture", "Eternal", Value.Of(true));
        if (mode == "empty-bag") Submit("EventFixtureEmptyBag");
        if (mode == "two-potions") { Submit("EventFixturePotion"); Submit("EventFixturePotion"); }
        if (mode == "egg") Submit("EventFixtureEgg");
        if (mode is "byrdpip" or "paels-legion") Submit("EventFixturePet", Value.Of("base-spire:" + mode));
        var random = JsonSerializer.Serialize(s.Random.Capture()); var state = s.State.StateHash();
        s.LegalActions(s.Participant(0)); s.Observe(s.Participant(0), "base-spire:PlayerView");
        var readOnly = state == s.State.StateHash() && random == JsonSerializer.Serialize(s.Random.Capture());
        var definitions = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:OpeningDefinition").Id)
            .Where(d => Text(d, "OpeningDefinition", "Category") == "event" && Number(d, "OpeningDefinition", "Act") == 0)
            .Select(d => Text(d, "OpeningDefinition", "DefinitionId")).Order(StringComparer.Ordinal).ToArray();
        var eligibility = definitions.Select(id =>
        {
            Submit("EventFixtureQuery", Value.Of(id));
            bool Flag(string field) => s.State.GetField(audit, s.RuleSet.TypeByQualifiedId("base-spire:EventFixture").FieldIndex(field)).AsBool();
            return new Eligibility(id, Flag("Natural"), Flag("Allowed"));
        }).ToArray();
        var boundaries = new List<Boundary> { Capture() };
        for (var i = 0; i < 40; i++) { Submit("EventFixturePull"); boundaries.Add(Capture()); }
        return new(eligibility, boundaries.ToArray(), random == JsonSerializer.Serialize(s.Random.Capture()), readOnly);
        Boundary Capture() => new(Text(run, "Run", "SelectedNormalEvent"), Number(run, "Run", "NormalEventIndex"),
            s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:NormalEventHistory").Id).Select(h => Text(h, "NormalEventHistory", "DefinitionId")).Order(StringComparer.Ordinal).ToArray());
    }
    private static Result Native(string seed, string mode)
    {
#if REFERENCE_SIM
        RunReplayDemo.EnsureModelsRegistered();
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()]);
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var room = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(room); room.Enter(run).GetAwaiter().GetResult();
        if (mode.StartsWith("gold", StringComparison.Ordinal)) player.Gold = int.Parse(mode[4..]);
        if (mode.StartsWith("hp", StringComparison.Ordinal)) player.Creature.SetCurrentHpInternal(int.Parse(mode[2..]));
        if (mode.StartsWith("floor", StringComparison.Ordinal))
            for (var floor = 1; floor < int.Parse(mode[5..]); floor++) run.AddVisitedMapCoord(new MapCoord(0, floor));
        if (mode is "no-deck" or "one-card") foreach (var card in player.Deck.Cards.Skip(mode == "no-deck" ? 0 : 1).ToArray()) player.Deck.RemoveInternal(card);
        if (mode == "eternal-deck")
            foreach (var card in player.Deck.Cards) typeof(CardModel).GetMethod("AddKeywordInternal", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(card, [CardKeyword.Eternal]);
        if (mode == "empty-bag")
            foreach (var list in ((IDictionary)typeof(RelicGrabBag).GetField("_buckets", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(player.RelicGrabBag)!).Values) ((IList)list!).Clear();
        if (mode == "two-potions") for (var i = 0; i < 2; i++) player.AddPotionInternal(ModelDb.Potion<Sts2Sim.Core.Models.Potions.FirePotion>());
        if (mode == "egg") { var egg = (CardModel)ModelDb.Card<Sts2Sim.Core.Models.Cards.ByrdonisEgg>().MutableClone(); egg.AssignOwner(player); player.Deck.AddInternal(egg); }
        if (mode is "byrdpip" or "paels-legion")
        {
            RelicModel relic = mode == "byrdpip" ? ModelDb.Relic<Sts2Sim.Core.Models.Relics.Byrdpip>() : ModelDb.Relic<Sts2Sim.Core.Models.Relics.PaelsLegion>();
            relic = (RelicModel)relic.MutableClone(); relic.AssignOwner(player); player.AddRelicInternal(relic);
        }
        string Random() => NormalPreflight.Json(new { run = run.Rng.ToSerializable(), player = player.PlayerRng.ToSerializable() });
        var random = Random(); var before = Capture();
        var eligibility = new Overgrowth().EffectiveEventPool.Select(type => (EventModel)ModelDb.Get(type))
            .Select(e => new Eligibility(NormalReferenceRun.Id(e.Id), e.CanAppearNaturally, e.IsAllowed(run))).OrderBy(e => e.Id, StringComparer.Ordinal).ToArray();
        var readOnly = NormalPreflight.Json(before) == NormalPreflight.Json(Capture()) && random == Random();
        var boundaries = new List<Boundary> { before }; var selected = "none";
        for (var i = 0; i < 40; i++) { selected = NormalReferenceRun.Id(ModelDb.GetId(run.PullNextEvent())); boundaries.Add(Capture(selected)); }
        return new(eligibility, boundaries.ToArray(), random == Random(), readOnly);
        Boundary Capture(string selected = "none") => new(selected, (int)Private("_eventsVisited"),
            ((IEnumerable<ModelId>)Private("_visitedEventIds")).Select(NormalReferenceRun.Id).Order(StringComparer.Ordinal).ToArray());
        object Private(string name) => typeof(RunState).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(run)!;
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }
    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "event-selection-comparison-" + Guid.NewGuid());
        internal Fixture(string original)
        {
            foreach (var source in Directory.GetFiles(original, "*", SearchOption.AllDirectories))
            { var target = Path.Combine(Root, Path.GetRelativePath(original, source)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target); }
            var entry = Path.Combine(Root, "base.spire");
            File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"event-fixture.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "event-fixture.spire"), """
                using namespace base-spire;
                dsl_version = "core/0.3"
                entity EventFixture { fields = { Allowed = { type = Bool default = false } Natural = { type = Bool default = false } Eternal = { type = Bool default = false } } }
                action EventFixtureInit { role = player locals = { run = Binding visit = Binding audit = Binding } body = {
                  call = { procedure = InitializeNormalIronclad args = { owner = first(type(Player)) } }
                  let = { into = run value = first(type(Run)) }
                  set = { target = run field = Run.TotalFloor value = 1 }
                  set = { target = first(type(Player)) field = Player.CurrentRun value = run }
                  call = { procedure = InitializeSourceOpeningPools args = { run = run } }
                  create = { type = NeowVisit scope = entityScope(run) into = visit fields = { Run = run } }
                  call = { procedure = GenerateSourceNeowOptions args = { visit = visit } }
                  set = { target = run field = Run.NormalEventIndex value = 1 }
                  create = { type = EventFixture scope = frameScope() into = audit }
                } }
                action EventFixtureQuery { role = player parameters = { id = String(length = 64) } body = {
                  set = { target = first(type(EventFixture)) field = EventFixture.Allowed value = pureCall(NormalEventIsAllowed, first(type(Run)), id) }
                  set = { target = first(type(EventFixture)) field = EventFixture.Natural value = pureCall(NormalEventCanAppearNaturally, id) }
                } }
                action EventFixturePull { role = player locals = { result = String(length = 64) } body = { call = { procedure = PullNextNormalEvent args = { run = first(type(Run)) } into = result } } }
                action EventFixtureTrimDeck { role = player parameters = { keep = Int } body = { forEach = { query = skip(collection(RunDeck), keep) item = card body = { destroy = { target = card } } } } }
                action EventFixtureEmptyBag { role = player body = {
                  forEach = { query = where(type(OpeningList), bag, equal(field(bag, OpeningList.Category), "player-bag")) item = bag body = {
                    forEach = { query = relation(OpeningItems, bag) item = entry body = { destroy = { target = entry } } }
                  } }
                } }
                action EventFixturePotion { role = player locals = { potion = Binding } body = {
                  call = { procedure = CreatePotionInstance args = { owner = first(type(Player)) definition = "base-spire:fire-potion" } into = potion }
                  call = { procedure = ObtainPotion args = { owner = first(type(Player)) potion = potion } }
                } }
                action EventFixtureEgg { role = player locals = { card = Binding } body = { create = { type = Card scope = frameScope() into = card collection = RunDeck fields = { DefinitionId = "base-spire:byrdonis-egg" } } } }
                action EventFixturePet { role = player parameters = { id = String(length = 64) } locals = { relic = Binding } body = { create = { type = Relic scope = frameScope() into = relic collection = Relics fields = { DefinitionId = id Holder = first(type(Player)) } } } }
                procedure EventFixtureEternal { pure = true parameters = { value = Bool card = Ref(Card) keyword = String(length = 16) } result = Bool body = {
                  if = { condition = and(equal(keyword, "eternal"), field(first(type(EventFixture)), EventFixture.Eternal)) then = { return = { value = true } } }
                  return = { value = value }
                } }
                subscription EventFixtureEternal { event = ModifyCardKeyword reaction = EventFixtureEternal }
                """);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
