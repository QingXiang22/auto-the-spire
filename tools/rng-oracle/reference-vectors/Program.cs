using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Models.PotionPools;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

if (args.Length != 2) throw new ArgumentException("Use ReferenceVectors REPOSITORY OUTPUT_DIRECTORY");
var root = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
using var locked = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reference/baselines/sts2-sim.json")));
string Git(params string[] arguments)
{
    var info = new ProcessStartInfo("git") { WorkingDirectory = Path.Combine(root, "reference/sts2-sim"), RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) info.ArgumentList.Add(argument);
    using var process = Process.Start(info)!;
    var result = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException(process.StandardError.ReadToEnd());
    return result;
}
if (Git("rev-parse", "HEAD") != locked.RootElement.GetProperty("commit").GetString()
    || Git("rev-parse", "HEAD^{tree}") != locked.RootElement.GetProperty("tree").GetString()
    || Git("status", "--porcelain").Length != 0)
    throw new InvalidOperationException("Reference checkout must be clean and match the committed lock.");
RunReplayDemo.EnsureModelsRegistered();
var json = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, json) + "\n");
string Id(string entry) => "base-spire:" + entry.ToLowerInvariant().Replace('_', '-');
string TypeId(Type type) => Id(ModelDb.GetId(type).Entry);
object Private(object instance, string field) => instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
ActDefinition[] Acts() => [new Overgrowth(), new Hive(), new Glory()];
object[] Definitions()
{
    var rows = new List<object>();
    void Add(string category, int act, string name, string id, string room = "none", string rarity = "none", bool weak = false, string[]? tags = null)
        => rows.Add(new { category, act, name, id, room, rarity, weak, tags = tags ?? [] });
    foreach (var (category, relics) in new[] { ("shared-relic", SharedRelicPool.Instance.AllRelics), ("ironclad-relic", new IroncladRelicPool().AllRelics) })
        foreach (var relic in relics) Add(category, -1, relic.GetType().Name, Id(relic.Id.Entry), rarity: relic.Rarity.ToString());
    foreach (var act in Acts())
    {
        foreach (var type in act.EffectiveEventPool) Add("event", act.Index, type.Name, TypeId(type));
        foreach (var (room, encounters) in new[] { ("Monster", act.MonsterEncounterCandidates), ("Elite", act.EliteEncounterCandidates), ("Boss", act.BossEncounterCandidates) })
            foreach (var encounter in encounters) Add("encounter", act.Index, encounter.Name, Id(encounter.IdEntry), room: room, weak: encounter.IsWeak, tags: encounter.Tags.Select(t => t.ToString()).ToArray());
        foreach (var type in act.AncientPool) Add("ancient", act.Index, type.Name, TypeId(type));
    }
    foreach (var type in SharedAncientPool.All) Add("shared-ancient", -1, type.Name, TypeId(type));
    return rows.ToArray();
}
var definitions = Definitions();
var seeds = new[] { "20261018", "777", "1234", "99", "ABC", "abc", "BASE-DSL-STDIO-00", "斩杀尖塔2", "A😀B", "" };
var openings = new List<object>(); var maps = new List<object>(); var gaussians = new List<object>(); var combats = new List<object>(); var slimes = new List<object>();
foreach (var seed in seeds.Append("20261021"))
{
    var run = new RunState(seed, Acts());
    var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
    // Snapshot both freshly shuffled bags before normal room entry removes starting/selected relics.
    var bags = new List<object>();
    foreach (var (category, bag) in new[] { ("shared-bag", run.SharedRelicGrabBag!), ("player-bag", player.RelicGrabBag) })
        foreach (DictionaryEntry pair in (IDictionary)Private(bag, "_buckets"))
            bags.Add(new { category, rarity = pair.Key.ToString(), ids = ((IEnumerable<RelicModel>)pair.Value!).Select(r => Id(r.Id.Entry)).ToArray() });
    run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
    var room = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(room); await room.Enter(run);
    var neow = (Neow)room.Event;
    var plans = new List<object>(); var generated = (IEnumerable)Private(run, "_generatedRooms");
    var actIndex = 0;
    foreach (var state in generated)
    {
        object Get(string property) => state!.GetType().GetProperty(property)!.GetValue(state)!;
        var index = 0;
        foreach (Type type in (IEnumerable)Get("Events")) plans.Add(new { act = actIndex, room = "event", position = index++, id = TypeId(type) });
        foreach (var (kind, property) in new[] { ("Monster", "NormalEncounters"), ("Elite", "EliteEncounters") })
        {
            index = 0;
            foreach (EncounterDefinition encounter in (IEnumerable)Get(property)) plans.Add(new { act = actIndex, room = kind, position = index++, id = Id(encounter.IdEntry) });
        }
        plans.Add(new { act = actIndex, room = "Boss", position = 0, id = Id(((EncounterDefinition)Get("Boss")).IdEntry) });
        plans.Add(new { act = actIndex, room = "ancient", position = 0, id = TypeId((Type)Get("Ancient")) });
        actIndex++;
    }
    openings.Add(new { seed, root_seed = run.Rng.Seed, up_front_draws = run.Rng.UpFront.Counter, bags, plans,
        neow_options = neow.CurrentOptions.Select(o => TypeId(typeof(Neow).Assembly.GetType("Sts2Sim.Core.Models.Relics." + o.Key)!)).ToArray(),
        neow_draws = ((Rng)typeof(EventModel).GetProperty("Rng", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(neow)!).Counter });
    var mapRng = new Rng(run.Rng.Seed, "act_1_map"); var act = new Overgrowth();
    var counts = act.GetMapPointTypes(mapRng, run.Ascension);
    var map = new Sts2Sim.Core.Map.StandardActMap(mapRng, act.BaseNumberOfRooms, counts, hasSecondBoss: false);
    var points = Enumerable.Range(0, map.GetRowCount()).SelectMany(map.GetPointsInRow).Append(map.StartingMapPoint).Append(map.BossMapPoint).Distinct().ToArray();
    var cells = points.OrderBy(p => p.coord.row).ThenBy(p => p.coord.col)
        .Select(p => new { row = p.coord.row, col = p.coord.col, type = (int)p.PointType,
            children = p.Children.Sum(c => 1L << c.coord.col), parents = points.Where(candidate => candidate.Children.Contains(p)).Sum(c => 1L << c.coord.col) }).ToArray();
    maps.Add(new { seed, draws = mapRng.Counter, cells });
    var gaussianRng = new Rng(run.Rng.Seed, "act_1_map"); var gaussianCalls = new List<object>();
    foreach (var (mean, deviation, min, max) in Enumerable.Repeat(new[] { (7, 1, 6, 7), (12, 1, 10, 14) }, 4).SelectMany(x => x))
    {
        var before = gaussianRng.Counter;
        var result = gaussianRng.NextGaussianInt(mean, deviation, min, max);
        gaussianCalls.Add(new { mean, deviation, min, max, before, after = gaussianRng.Counter, result });
    }
    gaussians.Add(new { seed, calls = gaussianCalls });
    if (seed != "20261021")
    {
        var controlled = new RunState(seed, Acts());
        var fighter = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), controlled); controlled.AddPlayer(fighter);
        var definition = new Overgrowth().MonsterEncounterCandidates.Single(e => e.Name == "SlimesWeak");
        var composition = new Rng(unchecked(controlled.Rng.Seed + 2 + StringHelper.GetDeterministicHashCode(definition.IdEntry)));
        var battle = new CombatRoom(() => definition.CreateMonsters(composition));
        controlled.PushRoom(battle); await battle.Enter(controlled);
        var states = new List<object>();
        object Snapshot() => new { hp = fighter.Creature.CurrentHp, monster_ai_draws = controlled.Rng.MonsterAi.Counter,
            moves = battle.Engine.State.Enemies.Select(c => c.Monster!.NextMove!.Id).ToArray(),
            slimed = fighter.PlayerCombatState!.AllPiles.SelectMany(p => p.Cards).Count(c => c is Sts2Sim.Core.Models.Cards.Slimed) };
        states.Add(Snapshot());
        var monsters = battle.Engine.State.Enemies.Select(c => new { id = Id(c.Monster!.Id.Entry), hp = c.CurrentHp }).ToArray();
        for (var turn = 0; turn < 3; turn++) { await battle.Engine.EndPlayerTurnAsync(); states.Add(Snapshot()); }
        slimes.Add(new { seed, total_floor = 2, monsters, composition_draws = composition.Counter, niche_draws = controlled.Rng.Niche.Counter, states });
    }
    if (seed == "777")
    {
        await neow.ChooseOption(neow.CurrentOptions.Single(o => o.Key == "BoomingConch"));
        await room.Exit(run);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.Children.OrderBy(p => p.coord.col).First().coord);
        var combat = (CombatRoom)RoomFactory.CreateRoom(run, RoomType.Monster); run.PushRoom(combat); await combat.Enter(run);
        combats.Add(new { seed, encounter = Id(combat.Encounter!.IdEntry), total_floor = run.TotalFloor,
            monsters = combat.Engine.State.Enemies.Select(c => new { id = Id(c.Monster!.Id.Entry), hp = c.CurrentHp, move = c.Monster.NextMove!.Id }),
            hand = player.PlayerCombatState!.Hand.Cards.Select(c => Id(c.Id.Entry)),
            draw_pile = player.PlayerCombatState.DrawPile.Cards.Select(c => Id(c.Id.Entry)),
            niche_draws = run.Rng.Niche.Counter, monster_ai_draws = run.Rng.MonsterAi.Counter });
    }
}
var hashes = Directory.GetFiles(Path.Combine(root, "reference/sts2-sim/src/Sts2Sim.Core"), "*.cs", SearchOption.AllDirectories)
    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
    .Order(StringComparer.Ordinal).ToDictionary(p => Path.GetRelativePath(root, p).Replace('\\', '/'), p => Convert.ToHexStringLower(SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(p)).Replace("\r\n", "\n")))));
Dictionary<string, string> Subset(params string[] paths) => hashes.Where(pair => paths.Any(path => pair.Key.StartsWith("reference/sts2-sim/src/Sts2Sim.Core/" + path, StringComparison.Ordinal))).ToDictionary();
var openingHashes = Subset("Content/Acts/", "Models/Events/Neow.cs", "Models/EventModel.cs", "Models/RelicPools/", "Runs/RunState", "Runs/RunRngSet.cs", "Random/", "Helpers/StringHelper.cs", "Helpers/ListExtensions.cs");
var mapHashes = Subset("Content/Acts/Overgrowth.cs", "Map/", "Random/", "Helpers/StringHelper.cs");
Write("normal-opening-vectors.json", new { format = "source-normal-opening-vectors@2", profile = "ironclad-a0-fully-unlocked-singleplayer-fixed-acts", reference_lock = locked.RootElement.Clone(), source_hash_encoding = "utf8-lf", source_sha256 = openingHashes,
    definitions, vectors = openings.Take(10), regression_vectors = openings.Skip(10) });
Write("act-one-map-vectors.json", new { format = "source-map-vectors@2", reference_lock = locked.RootElement.Clone(), source_hash_encoding = "utf8-lf", source_sha256 = mapHashes, vectors = maps.Take(10) });
Write("map-gaussian-vectors.json", new { format = "source-map-gaussian-vectors@2", reference_lock = locked.RootElement.Clone(), source_hash_encoding = "utf8-lf", source_sha256 = mapHashes, vectors = gaussians.Take(10) });
Write("normal-combat-vectors.json", new { reference_lock = locked.RootElement.Clone(), source_hash_encoding = "utf8-lf", vectors = combats });
Write("slimes-vectors.json", new { reference_lock = locked.RootElement.Clone(), source_hash_encoding = "utf8-lf", source_sha256 = Subset("Content/Acts/Overgrowth.cs", "Rooms/RoomFactory.cs", "Models/Monsters/LeafSlime", "Models/Monsters/TwigSlime", "Models/MonsterModel.cs", "MonsterMoves/", "Random/", "Entities/Creatures/Creature.cs", "Combat/CombatState.cs"), vectors = slimes });
Write("normal-unknown-room-vector.json", await UnknownRoomVector.Capture(locked.RootElement.Clone(), Subset("Content/Acts/", "Rooms/", "Odds/UnknownMapPointOdds.cs", "Runs/RunState", "Runs/RunRngSet.cs", "Random/", "Map/", "Models/Events/Neow.cs", "Models/Relics/GoldenPearl.cs", "Models/Monsters/FuzzyWurmCrawler.cs")));
Write("normal-event-selection-vector.json", await EventSelectionVector.Capture(locked.RootElement.Clone(), Subset("Content/Acts/", "Rooms/", "Odds/UnknownMapPointOdds.cs", "Runs/RunState", "Runs/RunRngSet.cs", "Random/", "Map/", "Models/Events/", "Models/EventModel.cs", "Models/Relics/BoomingConch.cs", "Models/Relics/Byrdpip.cs", "Models/Relics/PaelsLegion.cs", "Entities/Cards/", "Entities/Players/")));
Write("metadata.json", new { reference_lock = locked.RootElement.Clone(), source_hash_encoding = "utf8-lf", definitions,
    cards = new IroncladCardPool().AllCards.Select(c => new { id = Id(c.Id.Entry), name = c.GetType().Name, rarity = c.Rarity.ToString().ToLowerInvariant(), multiplayer_only = c.IsMultiplayerOnly }),
    potions = new IroncladPotionPool().AllPotions.Concat(SharedPotionPool.Instance.AllPotions).Select(p => new { id = Id(p.Id.Entry), name = p.GetType().Name, rarity = p.Rarity.ToString().ToLowerInvariant(), usage = p.Usage.ToString() }),
    neow = new { curse = ((Type[])typeof(Neow).GetField("CurseTypes", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!).Select(t => new { name = t.Name, id = TypeId(t) }),
        positive = ((Type[])typeof(Neow).GetField("PositiveTypes", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!).Select(t => new { name = t.Name, id = TypeId(t) }) } });
Write("ironclad-source-metadata.json", new { schema = "base-spire/ironclad-source-metadata@2", reference_lock = locked.RootElement.Clone(), source_hash_encoding = "utf8-lf",
    entries = new IroncladCardPool().AllCards.Append(ModelDb.Card<Sts2Sim.Core.Models.Cards.GiantRock>()).Select(c =>
    {
        var upgraded = (CardModel)c.MutableClone(); upgraded.Upgrade();
        return new { name = c.GetType().Name, id = Id(c.Id.Entry),
            source_sha256 = hashes["reference/sts2-sim/src/Sts2Sim.Core/Models/Cards/" + c.GetType().Name + ".cs"],
            multiplayer_only = c.IsMultiplayerOnly, cost = c.EnergyCost, upgraded_cost = upgraded.EnergyCost,
            kind = c.Type.ToString().ToLowerInvariant(), targeted = c.TargetType == TargetType.AnyEnemy,
            strike_tag = c.Tags.Contains(CardTag.Strike), is_x = c.CostsXEnergy, exhausts = c.HasKeyword(CardKeyword.Exhaust),
            upgraded_innate = upgraded.HasKeyword(CardKeyword.Innate), rarity = c.Rarity.ToString().ToLowerInvariant() };
    }) });
Write("word64-vectors.json", new { vectors = seeds.Concat(new[] { "old", "old777", "oldold777", "Old777", " seed ", new string('x', 1025) })
    .Select(seed => new { seed, hash = StringHelper.GetDeterministicHashCode(seed), root = new RunRngSet(seed).Seed,
        stream = unchecked(new RunRngSet(seed).Seed + StringHelper.GetDeterministicHashCode("up_front")) }) });
Console.WriteLine("Native locked reference vectors written: " + output);
