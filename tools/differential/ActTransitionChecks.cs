#if REFERENCE_SIM
using System.Collections;
using System.Reflection;
using System.Text.Json;
using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Runs;

namespace Differential;

/// <summary>Controlled transition witnesses with independent native default-plan generation; not a won Run.</summary>
internal static class ActTransitionChecks
{
    public static int Run(string output)
    {
        Directory.CreateDirectory(output);
        var evidence = new List<object>();
        try
        {
            CombatReplayDemo.EnsureModelsRegistered();
            var factory = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.DevRun));
            foreach (var seed in new[] { "ENV-T04-RNG-SHUFFLE-001", "ACT1-BASIC-10", "ENV-T03-SEED-L" })
            {
                using var random = new ReferenceRandomProbe();
                var native = new RunState(seed, new ActDefinition[] { new Overgrowth(), new Hive(), new Glory() });
                var player = Player.CreateForNewRun(Sts2Sim.Core.Models.ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), native);
                native.AddPlayer(player); _ = native.CurrentAncientEventType;
                random.Attach(native.Rng, player);
                var run = factory.CreateSession(seed); run.Start(); var s = run.Session;
                var owner = Members(s, "RunStates").Single();
                var generated = (IEnumerable)typeof(RunState).GetField("_generatedRooms", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(native)!;
                var beforeUpFront = s.Random.DrawCount(VanillaRandomLayout.EngineStreamName("UpFront"));
                Require(beforeUpFront == native.Rng.UpFront.Counter, $"{seed}: UpFront consumption {beforeUpFront} != {native.Rng.UpFront.Counter}");
                // Controlled inventory witness, never represented as a normal acquisition/Run victory.
                s.State.RunAtomic(() =>
                {
                    var potionType = s.RuleSet.TypeByQualifiedId("base-spire:Potion");
                    var belt = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:PotionBelt").Id;
                    foreach (var (identity, slot) in new[] { ("base-spire:block-potion", 0L), ("base-spire:strength-potion", 2L) })
                    {
                        var potion = s.State.CreateInstance(potionType.Id, s.State.ScopeOf(owner));
                        Set(s, potion, "DefinitionId", Value.Of(identity)); Set(s, potion, "SlotIndex", Value.Of(slot));
                        s.State.AddToCollection(belt, potion);
                    }
                });
                File.WriteAllText(Path.Combine(output, "initial-diagnostic.json"), JsonSerializer.Serialize(new {
                    seed, baseDraws = beforeUpFront, referenceDraws = native.Rng.UpFront.Counter,
                    nativeRooms = generated.Cast<object>().Select(g => new {
                        normal = ((IEnumerable)g.GetType().GetProperty("NormalEncounters")!.GetValue(g)!).Cast<EncounterDefinition>().Select(e=>e.IdEntry),
                        elite = ((IEnumerable)g.GetType().GetProperty("EliteEncounters")!.GetValue(g)!).Cast<EncounterDefinition>().Select(e=>e.IdEntry),
                        events = ((IEnumerable)g.GetType().GetProperty("Events")!.GetValue(g)!).Cast<Type>().Select(e=>e.Name),
                        boss = ((EncounterDefinition)g.GetType().GetProperty("Boss")!.GetValue(g)!).Name,
                        ancient = ((Type)g.GetType().GetProperty("Ancient")!.GetValue(g)!).Name }),
                    actual = new[] { "RunActMetadata", "RunActNormalQueue", "RunActEliteQueue", "RunActEventQueue", "RunActSharedAncients" }
                        .ToDictionary(name => name, name => Members(s,name).Select(e=>new {act=Number(s,e,"ActIndex"), index=Number(s,e,"Index"), id=Text(s,e,"SourceId"), boss=Text(s,e,"Boss"), ancient=Text(s,e,"Ancient")}))
                }, new JsonSerializerOptions { WriteIndented = true }));
                var actEvidence = new List<object>();
                var nativeActs = generated.Cast<object>().ToArray();
                for (var act = 0; act < 3; act++)
                {
                    var expected = nativeActs[act];
                    object Read(string name) => expected.GetType().GetProperty(name)!.GetValue(expected)!;
                    string[] NativeIds(string name) => ((IEnumerable)Read(name)).Cast<EncounterDefinition>()
                        .Select(e => "base-spire:" + e.IdEntry.ToLowerInvariant().Replace('_', '-')).ToArray();
                    string[] Archived(string collection) => Members(s, collection).Where(e => Number(s, e, "ActIndex") == act)
                        .OrderBy(e => Number(s, e, "Index")).Select(e => Text(s, e, "SourceId")).ToArray();
                    Require(Archived("RunActNormalQueue").SequenceEqual(NativeIds("NormalEncounters")), $"{seed}: act {act} normal queue differs");
                    Require(Archived("RunActEliteQueue").SequenceEqual(NativeIds("EliteEncounters")), $"{seed}: act {act} elite queue differs");
                    var eventNames = ((IEnumerable)Read("Events")).Cast<Type>().Select(t => t.Name).ToArray();
                    Require(Archived("RunActEventQueue").SequenceEqual(eventNames), $"{seed}: act {act} event queue differs");
                    var meta = Members(s, "RunActMetadata").Single(e => Number(s, e, "ActIndex") == act);
                    var boss = "base-spire:" + ((EncounterDefinition)Read("Boss")).IdEntry.ToLowerInvariant().Replace('_', '-');
                    Require(Text(s, meta, "Boss") == boss, $"{seed}: act {act} boss differs");
                    Require(Text(s, meta, "Ancient") == ((Type)Read("Ancient")).Name, $"{seed}: act {act} ancient differs");
                    Require(Number(s, owner, "CurrentActIndex") == act, "one transition must advance exactly one act");
                    Require(Text(s, owner, "GeneratedBoss") == boss, "active boss is the pre-generated source identity");
                    Require(Members(s, "EncounterQueue").Select(e => Text(s, e, "EncounterId")).SequenceEqual(NativeIds("NormalEncounters")), "active normal queue copies the original inventory");
                    CompareMap(s, native);
                    var mapStream = VanillaRandomLayout.ActMapStreamName(act + 1);
                    var nativeMap = random.Streams().Single(stream => stream.Stream.EndsWith($":named.act_{act + 1}_map", StringComparison.Ordinal));
                    var actualMap = EngineSnapshot.Streams(s).Single(stream => stream.Stream == mapStream);
                    Require(nativeMap.Draws == actualMap.Draws && nativeMap.Fingerprint == actualMap.Fingerprint,
                        "map generation consumes the exact native stream and leaves the same RNG state");
                    var oldNodes = Members(s, "MapNodes").ToArray();
                    var held = new[] { "LongTermDeck", "Relics", "PotionBelt" }.ToDictionary(name => name, name => Members(s, name).ToArray());
                    var gold = Number(s, owner, "Gold"); var health = Number(s, owner, "Hp");
                    var maxHealth = Number(s, owner, "MaxHp");
                    var archiveHash = ArchiveSignature(s);
                    var action = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ContinueToNextAct");
                    var stateHash = s.State.StateHash(); var draws = s.Random.TotalDraws;
                    try { s.Submit(action, SliceManifest.Player, []); throw new CheckFailed("premature act transition was accepted"); }
                    catch (EngineFaultException e) when (e.Code == EngineFaultCode.IllegalAction) { }
                    Require(s.State.StateHash() == stateHash && s.Random.TotalDraws == draws, "rejected transition must be inert");
                    s.State.RunAtomic(() => { Set(s, owner, "ActCompleted", Value.Of(true)); Set(s, owner, "MapPhase", Value.Of(3L)); });
                    var pendingRewards = s.State.StateHash();
                    try { s.Submit(action, SliceManifest.Player, []); throw new CheckFailed("transition bypassed unresolved rewards"); }
                    catch (EngineFaultException e) when (e.Code == EngineFaultCode.IllegalAction) { }
                    Require(s.State.StateHash() == pendingRewards && s.Random.TotalDraws == draws, "unresolved rewards reject transition without effects");
                    actEvidence.Add(new { act, boss, ancient = Text(s, meta, "Ancient"), normal = Archived("RunActNormalQueue"),
                        elite = Archived("RunActEliteQueue"), events = eventNames, nodes = oldNodes.Length,
                        mapDraws = actualMap.Draws, nativeMapDraws = nativeMap.Draws, mapFingerprint = actualMap.Fingerprint,
                        nativeMapFingerprint = nativeMap.Fingerprint, rejectedBeforeCompletion = true, rejectedBeforeRewardClosure = true });
                    if (act == 2)
                    {
                        ArrangeCompleted(s, owner);
                        try { s.Submit(action, SliceManifest.Player, []); throw new CheckFailed("final event boundary silently completed a Run"); }
                        catch (EngineFaultException e) when (e.Message.Contains("TheArchitect", StringComparison.Ordinal)) { }
                        Require(s.Status != SessionStatus.Terminal, "three act endings must not invent a global Victory");
                        break;
                    }
                    // Explicit controlled boundary, shared by both backends. No pretend first-act victory.
                    var visited = oldNodes.Take(3).ToArray();
                    s.State.RunAtomic(() => { foreach (var node in visited) Set(s, node, "Visited", Value.Of(true)); });
                    foreach (var point in native.Map.GetAllMapPoints().Take(3)) native.AddVisitedMapCoord(point.coord);
                    var remnants = ArrangeCombatRemnants(s, owner);
                    ArrangeCompleted(s, owner);
                    s.Submit(action, SliceManifest.Player, []); native.AdvanceToNextAct();
                    Require(s.Status == SessionStatus.Running, $"transition fault: {s.Fault}");
                    Require(Number(s, owner, "TotalFloor") == native.TotalFloor, "completed-act floor history survives map replacement");
                    Require(Number(s, owner, "CurrentRoom") == 7 && Number(s, owner, "SettledRoom") == 7, "global settlement keys are not reused/reset");
                    Require(Number(s, owner, "Gold") == gold && Number(s, owner, "Hp") == health
                        && Number(s, owner, "MaxHp") == maxHealth, "run resources survive");
                    Require(Number(s, owner, "NormalEncountersVisited") == 0 && Number(s, owner, "EliteEncountersVisited") == 0
                        && Number(s, owner, "EventsVisited") == 0, "act-local visit counters reset");
                    Require(remnants.All(id => !s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id)
                        .Concat(s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Monster").Id))
                        .Concat(s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Power").Id)).Contains(id)), "dead monsters, powers and temporary combat cards must not survive");
                    var battlePlayer = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Single();
                    Require(Number(s, battlePlayer, "Stars") == 0 && Number(s, battlePlayer, "Block") == 0
                        && Number(s, battlePlayer, "HpLossCount") == 0 && Number(s, battlePlayer, "Energy") == 0,
                        "battle-local resources cannot leak into the next act's map");
                    foreach (var (collection, ids) in held) Require(Members(s, collection).SequenceEqual(ids), collection + " real instances must survive");
                    Require(Members(s, "PotionBelt").Select(p => Number(s, p, "SlotIndex")).SequenceEqual(new[] { 0L, 2L }), "potion slot gaps survive");
                    Require(oldNodes.All(n => !s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:MapNode").Id).Contains(n)), "old map entities must be destroyed");
                    Require(ArchiveSignature(s) == archiveHash, "transition cannot regenerate or mutate pre-generated rooms");
                    Require(s.Random.DrawCount(VanillaRandomLayout.EngineStreamName("UpFront")) == beforeUpFront, "transition does not consume UpFront again");
                    var travel = s.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
                    Require(travel.Bindings.Count == 1 && Number(s, travel.Bindings[0][0].AsRef(), "Row") == 0, "next-act ancient must be entered before normal successors");
                }
                Require(beforeUpFront == native.Rng.UpFront.Counter, "full pre-generation UpFront consumption differs");
                evidence.Add(new { seed, controlledTransition = true, normalVictoryEvidence = false, upFront = beforeUpFront, acts = actEvidence,
                    build = run.Build.PublicBuildId, reference = Repo.GitHead("reference/sts2-sim").ToString() });
            }
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { exit_code = 0, evidence }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Default-plan generation and controlled cross-act boundaries independently matched for 3 seeds / 3 acts.");
            return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { exit_code = 1, error = e.ToString(), evidence }, new JsonSerializerOptions { WriteIndented = true }));
            Console.Error.WriteLine(e); return 1;
        }
    }

    private static void CompareMap(Session s, RunState run)
    {
        var points = run.Map.GetAllMapPoints().Append(run.Map.StartingMapPoint).Append(run.Map.BossMapPoint).Distinct().ToArray();
        string Native(Sts2Sim.Core.Map.MapPoint p) => $"{p.coord.col},{p.coord.row}:{(int)p.PointType}";
        string Base(InstanceId p) => $"{Number(s, p, "Col")},{Number(s, p, "Row")}:{Get(s, p, "Type").AsEnumOrdinal()}";
        var nodes = Members(s, "MapNodes");
        Require(nodes.Select(Base).Order(StringComparer.Ordinal).SequenceEqual(points.Select(Native).Order(StringComparer.Ordinal)), "native map cells/types differ");
        var relation = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren");
        var edges = nodes.SelectMany(p => s.State.RelationTargets(relation.Id, p).Select(c => Base(p) + "->" + Base(c))).Order(StringComparer.Ordinal);
        Require(edges.SequenceEqual(points.SelectMany(p => p.Children.Select(c => Native(p) + "->" + Native(c))).Order(StringComparer.Ordinal)), "native map edges differ");
    }
    private static void ArrangeCompleted(Session s, InstanceId run) => s.State.RunAtomic(() =>
    { Set(s, run, "ActCompleted", Value.Of(true)); Set(s, run, "MapPhase", Value.Of(6L)); });
    private static InstanceId[] ArrangeCombatRemnants(Session s, InstanceId run)
    {
        var ids = new List<InstanceId>();
        s.State.RunAtomic(() =>
        {
            Set(s, run, "CurrentRoom", Value.Of(7L)); Set(s, run, "SettledRoom", Value.Of(7L));
            Set(s, run, "NormalEncountersVisited", Value.Of(4L)); Set(s, run, "EliteEncountersVisited", Value.Of(2L));
            Set(s, run, "EventsVisited", Value.Of(3L));
            var player = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Single();
            Set(s, player, "Stars", Value.Of(4L)); Set(s, player, "Block", Value.Of(17L));
            Set(s, player, "HpLossCount", Value.Of(3L)); Set(s, player, "Energy", Value.Of(2L));
            var monster = s.State.CreateInstance(s.RuleSet.TypeByQualifiedId("base-spire:Monster").Id, s.State.ScopeOf(player));
            Set(s, monster, "DefinitionId", Value.Of("base-spire:inklet")); Set(s, monster, "Hp", Value.Of(0L)); ids.Add(monster);
            var powers = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:Powers");
            foreach (var owner in new[] { player, monster })
            {
                var power = s.State.CreateInstance(s.RuleSet.TypeByQualifiedId("base-spire:Power").Id, s.State.ScopeOf(player));
                Set(s, power, "DefinitionId", Value.Of("base-spire:strength")); Set(s, power, "Stacks", Value.Of(2L));
                s.State.AddRelation(powers.Id, owner, power); ids.Add(power);
            }
            var card = s.State.CreateInstance(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id, s.State.ScopeOf(player));
            Set(s, card, "DefinitionId", Value.Of("base-spire:strike-ironclad"));
            s.State.AddToCollection(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id, card); ids.Add(card);
        });
        return ids.ToArray();
    }
    private static string ArchiveSignature(Session s) => string.Join("|", new[] { "RunActMetadata", "RunActNormalQueue", "RunActEliteQueue", "RunActEventQueue", "RunActSharedAncients" }
        .SelectMany(name => Members(s, name).Select(id => name + ":" + id + ":" + Text(s, id, "SourceId"))));
    private static IReadOnlyList<InstanceId> Members(Session s, string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
    private static Value Get(Session s, InstanceId e, string name) => s.State.GetField(e, s.RuleSet.Type(s.State.TypeIdOf(e)).FieldIndex(name));
    private static long Number(Session s, InstanceId e, string name) => Get(s, e, name).AsInt();
    private static string Text(Session s, InstanceId e, string name) => Get(s, e, name).AsString();
    private static void Set(Session s, InstanceId e, string name, Value value) => s.State.SetField(e, s.RuleSet.Type(s.State.TypeIdOf(e)).FieldIndex(name), value);
    private static void Require(bool condition, string message) { if (!condition) throw new CheckFailed(message); }
}
#endif
