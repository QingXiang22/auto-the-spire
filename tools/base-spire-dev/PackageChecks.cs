using System.Text.Json;
using CoreTheSpire;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int Run(string manifest, string? reportDirectory = null)
    {
        var package = DslPackage.Load(manifest);
        var passed = 0;
        var scenarios = new List<string>();
        void Case(string name, Action body) { body(); passed++; scenarios.Add(name); Console.WriteLine("ok   " + name); }
        Case("DB-A06/A07: real shuffle, ordinary draw, empty piles, repeat shuffle and removal", () =>
        {
            var s = Start(package); Submit(s, "TheAbacus.Obtain");
            Equal(0L, Block(s), "obtaining does not grant block");
            Submit(s, "Draw"); Equal(6L, Block(s), "source: six block per completed shuffle");
            Equal(1, Members(s, "Hand").Count, "one drawn"); Equal(2, Members(s, "DrawPile").Count, "remaining draw cards");
            Submit(s, "Draw"); Submit(s, "Draw"); Submit(s, "Draw");
            Equal(6L, Block(s), "nonempty draw pile and then both empty do not trigger");
            Submit(s, "DiscardHand"); Submit(s, "Draw"); Equal(12L, Block(s), "next actual shuffle triggers again");
            Submit(s, "RemoveOwnedRelic", Members(s, "Relics").Single());
            Submit(s, "DiscardHand"); Submit(s, "Draw"); Submit(s, "Draw"); Submit(s, "DiscardHand"); Submit(s, "Draw");
            Equal(12L, Block(s), "removed listener does not grant block");
            var bare = Start(package); Submit(bare, "Draw"); Equal(0L, Block(bare), "no relic means no block");
        });
        Case("DB-A07: melted instances and blocked draw do not trigger or consume shuffle randomness", () =>
        {
            var s = Start(package); Submit(s, "TheAbacus.Obtain");
            Set(s, Members(s, "Relics").Single(), "Relic", "IsMelted", Value.Of(true));
            Submit(s, "Draw"); Equal(0L, Block(s), "melted instance is inactive");
            var blocked = Start(package); Submit(blocked, "TheAbacus.Obtain");
            Set(blocked, Player(blocked), "Player", "DrawBlocked", Value.Of(true));
            var before = blocked.Random.DrawCount("base-spire:development-shuffle"); Submit(blocked, "Draw");
            Equal(before, blocked.Random.DrawCount("base-spire:development-shuffle"), "blocked draw consumes no shuffle random samples");
            Equal(3, Members(blocked, "DiscardPile").Count, "blocked draw leaves discard intact"); Equal(0L, Block(blocked), "no block");
        });
        Case("DB-A06: shuffle listeners complete before AfterCardDrawn", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("order-check.spire", """
                procedure CheckDrawOrder {
                  parameters = { owner = Ref(Player) card = Ref(Card) }
                  body = { if = { condition = not(equal(field(owner, Player.Block), 6)) then = { fail = { message = "draw happened before shuffle reward" } } } }
                }
                subscription CheckOrder { event = AfterCardDrawn reaction = CheckDrawOrder }
                """);
            var s = Start(copy.Load()); Submit(s, "TheAbacus.Obtain"); Submit(s, "Draw"); Equal(6L, Block(s), "ordered effect");
        });
        Case("DB-A07: full hand prevents both reshuffle and relic trigger", () =>
        {
            using var copy = new Copy(package.Root);
            var setup = copy.Path("run/setup-dev.spire"); File.WriteAllText(setup, File.ReadAllText(setup).Replace("count = 3", "count = 12", StringComparison.Ordinal));
            var s = Start(copy.Load()); Submit(s, "TheAbacus.Obtain");
            var hand = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id;
            var discard = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:DiscardPile").Id;
            s.State.RunAtomic(() => { foreach (var card in Members(s, "DiscardPile").Take(10).ToArray()) { s.State.RemoveFromCollection(discard, card); s.State.AddToCollection(hand, card); } });
            Submit(s, "Draw"); Equal(10, Members(s, "Hand").Count, "full hand unchanged");
            Equal(2, Members(s, "DiscardPile").Count, "discard is not shuffled"); Equal(0L, Block(s), "no trigger without shuffle");
        });
        Case("DB-A07: listeners isolate holders and instance state", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("owners.spire", """
                procedure SecondOwner { locals = { owner = Binding } body = {
                  create = { type = Player scope = frameScope() into = owner }
                  call = { procedure = ObtainRelic args = { owner = owner definition = "base-spire:the-abacus" } }
                } }
                action AddSecondOwner { role = player body = { call = { procedure = SecondOwner } } }
                """);
            var s = Start(copy.Load()); Submit(s, "TheAbacus.Obtain"); Submit(s, "AddSecondOwner");
            var second = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Last();
            Submit(s, "Draw"); Equal(6L, Block(s), "only shuffler gains block");
            Equal(0L, s.State.GetField(second, s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("Block")).AsInt(), "other holder untouched");
        });
        Case("DB-A08: another configured relic reuses DSL without a C# effect branch", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("configured.spire", "use ConfiguredProbe { template = AfterShuffleBlockRelic args = { definition = \"development:probe\" amount = 8 } }");
            var variant = copy.Load(); var s = Start(variant); Submit(s, "ConfiguredProbe.Obtain"); Submit(s, "Draw");
            Equal(8L, Block(s), "configured amount"); True(variant.Rules.Identity != package.Rules.Identity, "new semantics change identity");
        });
        Case("DB-A01/A09: package rejects executable files, JSON programs, unknown and duplicate fields", () =>
        {
            RejectCopy(package.Root, c => File.WriteAllText(c.Path("hidden.cs"), "class Hidden {}"));
            RejectCopy(package.Root, c => c.EditJson("package.json", root => root["effects"] = new System.Text.Json.Nodes.JsonArray()));
            RejectCopy(package.Root, c => c.EditJson("relics/catalog.json", root => root["unknown"] = 1));
            RejectCopy(package.Root, c => File.WriteAllText(c.Path("package.json"), File.ReadAllText(c.Path("package.json")).Replace("{", "{\"format\":\"duplicate\",", StringComparison.Ordinal)));
        });
        Case("DB-A03/A04: package rejects escaping references, missing and unreferenced DSL files", () =>
        {
            RejectCopy(package.Root, c => c.EditJson("package.json", root => root["entry"] = "../outside.spire"));
            RejectCopy(package.Root, c => File.WriteAllText(c.Path("unused.spire"), "using namespace base-spire; dsl_version = \"core/0.3\""));
            RejectCopy(package.Root, c => File.WriteAllText(c.Path("base.spire"), File.ReadAllText(c.Path("base.spire")).Replace("combat/index.spire", "../outside.spire", StringComparison.Ordinal)));
        });
        Case("DB-R05: content definitions reject rule bodies, unknown arguments and executable expressions", () =>
        {
            const string content = "relics/shared/the-abacus.spire";
            RejectCopy(package.Root, c => File.AppendAllText(c.Path(content), "\nprocedure Injected { body = {} }"));
            using var unknown = new Copy(package.Root);
            File.WriteAllText(unknown.Path(content), File.ReadAllText(unknown.Path(content)).Replace("amount = 6", "unlisted = 6", StringComparison.Ordinal));
            Throws<DslSourceException>(() => unknown.Load());
            using var expression = new Copy(package.Root);
            File.WriteAllText(expression.Path(content), File.ReadAllText(expression.Path(content)).Replace("amount = 6", "amount = add(3, 3)", StringComparison.Ordinal));
            Throws<DslSourceException>(() => expression.Load());
        });
        Case("DB-A05/A10: old scopes are rejected and source definitions exactly match supported relics", () =>
        {
            Throws<NotSupportedException>(() => DslPackage.Load(manifest, "base-spire-singleplayer-dev-map-v1"));
            using var rows = JsonDocument.Parse(File.ReadAllText(Path.Combine(package.Root, "relics/catalog.json")));
            var relics = rows.RootElement.GetProperty("entries"); Equal(297, relics.GetArrayLength(), "independent frozen source denominator");
            var migrated = relics.EnumerateArray().Where(row => row.GetProperty("definition").ValueKind == JsonValueKind.String)
                .Select(row => row.GetProperty("id").GetString()).Order(StringComparer.Ordinal);
            Equal("base-spire:anchor,base-spire:bag-of-marbles,base-spire:blood-vial,base-spire:bronze-scales,base-spire:fake-anchor,base-spire:gremlin-horn,base-spire:happy-flower,base-spire:horn-cleat,base-spire:lantern,base-spire:meat-on-the-bone,base-spire:mercury-hourglass,base-spire:oddly-smooth-stone,base-spire:orichalcum,base-spire:pendulum,base-spire:strawberry,base-spire:the-abacus,base-spire:vajra", string.Join(",", migrated), "actual migrated identities, not a reduced source denominator");
        });
        Case("DB-R10: same package has stable identities and sessions do not share state", () =>
        {
            var again = DslPackage.Load(manifest); Equal(package.Digest, again.Digest, "package identity"); Equal(package.Rules.Identity, again.Rules.Identity, "rules identity");
            var one = Start(package); var two = Start(package); Submit(one, "TheAbacus.Obtain"); Submit(one, "Draw");
            Equal(0L, Block(two), "independent session state"); Equal(3, Members(two, "DiscardPile").Count, "independent piles");
        });
        var witnesses = RunCombatStartChecks(package, Case);
        void Merge(Dictionary<string, string[]> rows, Dictionary<string, string[]> into)
        {
            foreach (var row in rows)
                into[row.Key] = into.TryGetValue(row.Key, out var existing) ? existing.Concat(row.Value).ToArray() : row.Value;
        }
        Merge(RunCombatTurnChecks(package, Case), witnesses);
        Merge(RunDamageChecks(package, Case), witnesses);
        var cards = RunCardPlayChecks(package, Case);
        Merge(RunPowerChecks(package, Case), cards);
        Merge(RunCardBatchChecks(package, Case), cards);
        Merge(RunCombatEndChecks(package, Case), cards);
        Merge(RunBronzeScalesChecks(package, Case), witnesses);
        Merge(RunFlameBarrierChecks(package, Case), cards);
        Merge(RunRelicBatch5Checks(package, Case), witnesses);
        Merge(RunRelicBatch6Checks(package, Case), witnesses);
        var potions = RunPotionChecks(package, Case);
        var rooms = RunRestSiteChecks(package, Case);
        var rewards = RunRewardChecks(package, Case);
        Merge(RunCardBatch3Checks(package, Case), cards);
        Merge(RunCardBatch4Checks(package, Case), cards);
        var enemies = RunEnemyTurnChecks(package, Case);
        var encounters = RunEncounterChecks(package, Case);
        witnesses.Add("base-spire:the-abacus", scenarios.Take(5).ToArray());
        Console.WriteLine($"All {passed} pure DSL package checks passed. This is a mechanism-development scope, not full-game parity.");
        if (reportDirectory is not null)
        {
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(System.IO.Path.Combine(reportDirectory, "package-behavior.json"), JsonSerializer.Serialize(new
            {
                schema = "engine-dsl/package-behavior@1", package_digest = package.Digest, ruleset = package.Rules.Identity,
                scope = package.Scope, scenarios, independent_verified = false,
                content = witnesses.Select(row => new { id = row.Key, passed_scenarios = row.Value }).ToArray(),
                cards = cards.Select(row => new { id = row.Key, passed_scenarios = row.Value }).ToArray(),
                enemies = enemies.Select(row => new { id = row.Key, passed_scenarios = row.Value }).ToArray(),
                encounters = encounters.Select(row => new { id = row.Key, passed_scenarios = row.Value }).ToArray(),
                potions = potions.Select(row => new { id = row.Key, passed_scenarios = row.Value }).ToArray(),
                rooms = rooms.Select(row => new { id = row.Key, passed_scenarios = row.Value }).ToArray(),
                rewards = rewards.Select(row => new { id = row.Key, passed_scenarios = row.Value }).ToArray(),
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return 0;
    }

    private static Session Start(DslPackage package) { var s = new Session(package.Rules, 20261018, ["player"]); s.Start(); Healthy(s); return s; }
    private static InstanceId Player(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Player").Id).First();
    private static long Block(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("Block")).AsInt();
    private static IReadOnlyList<InstanceId> Members(Session s, string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
    private static void Set(Session s, InstanceId entity, string type, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(entity, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
    private static void Submit(Session s, string name, params InstanceId[] instances)
    {
        var action = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
        s.Submit(action.Definition, s.Participant(0), instances.Select(Value.OfRef).ToArray()); Healthy(s);
    }
    private static void Healthy(Session s) { if (s.Status == SessionStatus.Faulted) throw new InvalidOperationException("Session fault: " + s.Fault); }
    private static void Equal<T>(T expected, T actual, string why) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{why}: expected {expected}, actual {actual}"); }
    private static void True(bool test, string why) { if (!test) throw new InvalidOperationException(why); }
    private static void Throws<T>(Action body) where T : Exception { try { body(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}"); }
    private static void RejectCopy(string root, Action<Copy> mutate) { using var copy = new Copy(root); mutate(copy); Throws<FormatException>(() => copy.Load()); }
    private sealed class Copy : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "base-dsl-check-" + Guid.NewGuid());
        public Copy(string root) { foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) { var target = Path(System.IO.Path.GetRelativePath(root, path)); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!); File.Copy(path, target); } }
        public string Path(string relative) => System.IO.Path.Combine(directory, relative);
        public DslPackage Load() => DslPackage.Load(Path("package.json"));
        public void EditJson(string path, Action<System.Text.Json.Nodes.JsonObject> edit) { var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path(path)))!.AsObject(); edit(json); File.WriteAllText(Path(path), json.ToJsonString()); }
        public void Include(string path, string body)
        {
            var entry = Path("base.spire"); File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", $"\"{path}\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path(path), "using namespace base-spire;\ndsl_version = \"core/0.3\"\n" + body);
        }
        public void Dispose() => Directory.Delete(directory, true);
    }
}
