using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;

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
        var item = new NormalCase("fault-injection", "blocked_compare", null, prefix, pairs, null, true)
        { ProtocolChecks = pairs.Select(_ => new NormalProtocolBoundary("synthetic", new(true, true, true, true, "synthetic-gate-check"))).ToArray() };
        // Synthetic records exercise the evidence gate only. They never count as gameplay passes.
        Require(NormalRunComparison.Certified(item), "complete synthetic evidence gate");
        Require(!NormalRunComparison.Certified(item with { ProtocolChecks = [] }), "missing protocol replay evidence");
        foreach (var failed in new[] { new NormalProtocolCheck(false, true, true, true, "synthetic"),
            new(true, false, true, true, "synthetic"), new(true, true, false, true, "synthetic"), new(true, true, true, false, "synthetic") })
            Require(!NormalRunComparison.Certified(item with { ProtocolChecks = [.. item.ProtocolChecks[..^1], item.ProtocolChecks[^1] with { Evidence = failed }] }), "failed protocol invariant");
        Require(!NormalRunComparison.Certified(item with { Id = "m01-targeted-claim" }), "targeted path requires a real target decision");
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
        NormalSnapshotChecks.Run(repository);
#if REFERENCE_SIM
        CheckReferenceDiagnostics();
        CheckPotionDiscard(repository);
#endif
        Console.WriteLine("Normal comparison checks passed: all dimensions/first differences, zero execution, incomplete policy/return, and real Host diagnostic non-interference.");
    }
#if REFERENCE_SIM
    private static void CheckPotionDiscard(string repository)
    {
        var directory = Path.Combine(repository, ".artifacts/differential/diagnostic-checks");
        using var left = new NormalDslRun(repository, Path.Combine(directory, "potion-discard.ndjson"));
        using var right = new NormalReferenceRun("777");
        left.Start("777"); left.Step(left.Action("StartNormalRun")); right.Start();
        Apply(new("neow", ["base-spire:booming-conch"])); Apply(new("leave-neow", []));
        Apply(left.Semantic(left.Action("ChooseMapPoint")));
        for (var i = 0; left.Phase == "combat" && i < 100; i++)
            Apply(left.Semantic(new[] { "PlayAttackFromHand", "PlayCardFromHand", "EndDevelopmentTurn" }.Select(left.Find).First(a => a is not null)!));
        Require(left.Phase == "rewards", "potion mapping check reaches real rewards");
        var victory = left.Capture("victory");
        NormalScalarChecks.Run(left.DebugRecords, victory, right.Capture("victory"));
        NormalWeightedChecks.Run(left.DebugRecords, victory);
        var floatCalls = System.Text.Json.JsonSerializer.Deserialize<NormalFloat32Call[]>(victory.Values["random.float32_calls"])!;
        Require(floatCalls.Length == 11 && floatCalls.Count(c => c.Stream == "base-spire:run:monster_ai") == 3
            && floatCalls.Count(c => c.Stream == "base-spire:player:rewards") == 8,
            "normal fixed path compares every actual native float parameter and result");
        foreach (var injected in new[] { floatCalls[..^1], floatCalls.Concat(floatCalls.Take(1)).ToArray(),
            new[] { floatCalls[0] with { MaximumBits = 1077936128 } }.Concat(floatCalls.Skip(1)).ToArray(),
            new[] { floatCalls[0] with { ResultBits = floatCalls[0].ResultBits + 1 } }.Concat(floatCalls.Skip(1)).ToArray(),
            new[] { floatCalls[0] with { Before = floatCalls[0].Before + 1 } }.Concat(floatCalls.Skip(1)).ToArray() })
        {
            var changed = new SortedDictionary<string, string>(victory.Values, StringComparer.Ordinal)
                { ["random.float32_calls"] = NormalPreflight.Json(injected) };
            Require(NormalRunComparison.First(0, victory, victory with { Values = changed })?.Path == "random.float32_calls",
                "missing, duplicate, changed range, final bits and cursor report a float first difference");
        }
        CheckPublicProjection(left.Snapshot);
        var actualEvents = System.Text.Json.JsonSerializer.Deserialize<NormalObservedEvent[]>(victory.Values["events.observed_order"])!;
        foreach (var (phase, count) in new[] { ("combat-started", 1), ("player-turn-prepared", 4), ("enemy-turn-starting", 3),
            ("enemy-move-finished", 5), ("player-turn-ended", 3) })
        {
            Require(actualEvents.Count(e => e.Kind == phase) == count, "normal path has all actual " + phase + " phases");
            var first = Array.FindIndex(actualEvents, e => e.Kind == phase);
            var changed = new SortedDictionary<string, string>(victory.Values, StringComparer.Ordinal)
                { ["events.observed_order"] = NormalPreflight.Json(actualEvents.Where((_, i) => i != first).ToArray()) };
            Require(NormalRunComparison.First(0, victory, victory with { Values = changed })?.Path == "events.observed_order",
                "missing actual phase reports an event first difference: " + phase);
        }
        using (var document = System.Text.Json.JsonDocument.Parse(victory.Values["lifecycle.settlement"]))
        {
            var stages = document.RootElement.EnumerateArray().ToArray();
            Require(stages.Length == 2 && stages[0].GetProperty("Phase").GetString() == "effects-finished"
                && stages[1].GetProperty("Phase").GetString() == "rewards-published"
                && !stages[0].GetProperty("RewardsGenerated").GetBoolean() && stages[1].GetProperty("RewardsGenerated").GetBoolean(),
                "real last kill finishes its outer card before publishing rewards");
            var hp = stages[0].GetProperty("Hp").GetInt64();
            Require(hp < stages[0].GetProperty("MaxHp").GetInt64()
                && stages[1].GetProperty("Hp").GetInt64() == Math.Min(hp + 6, stages[0].GetProperty("MaxHp").GetInt64())
                && stages[0].GetProperty("Gold").GetInt64() == stages[1].GetProperty("Gold").GetInt64(),
                "normal BurningBlood heals six only after card effects, before reward claiming");
            foreach (var invalid in new[] { stages.Reverse().ToArray(), stages[..1], stages.Concat(stages.Take(1)).ToArray() })
            {
                var changed = new SortedDictionary<string, string>(victory.Values, StringComparer.Ordinal)
                    { ["lifecycle.settlement"] = NormalPreflight.Json(invalid) };
                Require(NormalRunComparison.First(0, victory, victory with { Values = changed })?.Path == "lifecycle.settlement",
                    "wrong order, missing and repeated actual settlement stages are detected");
            }
        }
        Apply(new("claim-potion", [])); Apply(new("close-rewards", []));
        CheckPublicProjection(left.Snapshot);
        var discard = left.Semantic(left.Action("DiscardPotion"));
        Require(discard.Arguments.Single().Contains(":potion-reward:0:", StringComparison.Ordinal), "held potion retains its actual reward creation origin");
        Apply(discard);
        Require(left.Find("DiscardPotion") is null && left.Capture("discarded").Values["permanent.potions"] == "[]", "the same acquired potion is removed by the real public action");
        Require(left.CheckRequestReplay().Passed, "discard request cache and stale rejection preserve state and RNG");
        void Apply(NormalAction action)
        {
            left.Apply(action); right.Apply(action);
            Require(NormalRunComparison.First(0, left.Capture("potion-check"), right.Capture("potion-check")) is null,
                "real acquisition and discard match reference candidates, assets, events and RNG");
        }
    }

    private static void CheckReferenceDiagnostics()
    {
        var enabled = Replay(true); var disabled = Replay(false);
        Require(enabled.SequenceEqual(disabled), "reference combat/RNG observers leave every measured boundary and named stream unchanged");
        string[] Replay(bool diagnostics)
        {
            using var reference = new NormalReferenceRun("777", diagnostics); reference.Start();
            var result = new List<string>(); Capture();
            foreach (var action in new[] { new NormalAction("neow", ["base-spire:booming-conch"]), new("leave-neow", []), new("map", ["0,1"]),
                new("play", ["permanent:09:base-spire:bash", "first-legal-target"]), new("play", ["permanent:02:base-spire:strike-ironclad", "first-legal-target"]), new("end-turn", []) })
            { reference.Apply(action); Capture(); }
            return result.ToArray();
            void Capture()
            {
                result.Add(NormalPreflight.Json(reference.Capture("check").Values.Where(p => !p.Key.StartsWith("random.", StringComparison.Ordinal) && !p.Key.StartsWith("events.", StringComparison.Ordinal) && !p.Key.StartsWith("lifecycle.", StringComparison.Ordinal))));
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
        CheckPublicProjection(observed.Snapshot);
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
            var gameplay = a with { Values = new(a.Values.Where(p => !p.Key.StartsWith("random.", StringComparison.Ordinal) && !p.Key.StartsWith("events.", StringComparison.Ordinal) && !p.Key.StartsWith("lifecycle.", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal) };
            Require(NormalRunComparison.First(0, gameplay, b) is null, "diagnostics leave normal Host facts/actions/piles unchanged");
            Require(observed.RandomState == plain.RandomState, "diagnostics leave every stream/state/cursor unchanged");
            Require(observed.HasCompleteDebug && !plain.HasDebug, "debug is present only when requested and has no dropped records");
            Require(observed.PublicState == plain.PublicState, "diagnostics leave the complete public snapshot unchanged");
            using var actualEvents = System.Text.Json.JsonDocument.Parse(a.Values["events.observed_order"]);
            var array = actualEvents.RootElement.EnumerateArray().ToArray();
            if (array.Length > 1)
            {
                foreach (var injected in new[] { array.Reverse().ToArray(), array[..^1], array.Concat(array.Take(1)).ToArray() })
                {
                    var changed = new SortedDictionary<string, string>(a.Values, StringComparer.Ordinal)
                    { ["events.observed_order"] = NormalPreflight.Json(injected) };
                    Require(NormalRunComparison.First(0, a, a with { Values = changed })?.Path == "events.observed_order",
                        "missing, extra and reordered actual semantic events are detected");
                }
            }
        }
    }
    private static void CheckPublicProjection(PublicSnapshotDto snapshot)
    {
        SortedDictionary<string, string> Project(PublicSnapshotDto value)
        {
            var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
            NormalPublicObservation.Add(value, result, WireCanonical.Text); return result;
        }
        var original = new NormalBoundary("public-check", Project(snapshot), [], new { fault_injection = true });
        void Changed(PublicSnapshotDto changed, string path)
        {
            Require(NormalRunComparison.First(0, original, original with { Values = Project(changed) })?.Path == path,
                "actual public snapshot corruption is detected at " + path);
        }
        PublicSnapshotDto Replace(EntityDto entity, string field, WireValue value) => snapshot with
        {
            Entities = snapshot.Entities.Select(e => e != entity ? e : e with
            {
                Fields = e.Fields.Select(f => f.FieldId.EndsWith("." + field, StringComparison.Ordinal) ? f with { Value = value } : f).ToArray()
            }).ToArray()
        };
        EntityDto First(string kind) => snapshot.Entities.First(e => e.KindId.EndsWith(":" + kind, StringComparison.Ordinal));
        var run = First("Run");
        var pileOwner = First("HandView");
        Changed(Replace(pileOwner, "Player", WireValue.Str(run.InstanceId)), "public.ownership");
        var sequence = snapshot.Sequences.Single(s => s.TypeId.EndsWith(":MapNodes", StringComparison.Ordinal));
        Changed(snapshot with { Sequences = snapshot.Sequences.Select(s => s != sequence ? s : s with { OwnerRef = First("Player").InstanceId }).ToArray() }, "public.ownership");
        Reject(snapshot with { Sequences = snapshot.Sequences.Select(s => s != sequence ? s : s with { Elements = s.Elements.Skip(1).ToArray() }).ToArray() }, "missing map sequence member");
        Reject(snapshot with { Sequences = snapshot.Sequences.Select(s => s != sequence ? s : s with { Elements = [.. s.Elements, s.Elements[0]] }).ToArray() }, "duplicate map sequence member");
        var group = snapshot.Groups.First();
        Reject(snapshot with { Groups = snapshot.Groups.Select(g => g != group ? g : g with { OwnerRef = run.InstanceId }).ToArray() }, "incorrect inventory owner");
        Reject(snapshot with { Entities = [.. snapshot.Entities, First("Relic") with { InstanceId = "orphan-relic" }] }, "orphan visible relic");
        if (snapshot.Entities.Any(e => e.KindId.EndsWith(":RunVisit", StringComparison.Ordinal)))
        {
            var visit = First("RunVisit");
            var withoutCurrent = snapshot with { Entities = snapshot.Entities.Select(e => e != run ? e : e with
                { Fields = e.Fields.Where(f => !f.FieldId.EndsWith(".CurrentVisit", StringComparison.Ordinal)).ToArray() }).ToArray() };
            Changed(withoutCurrent, "public.visit_links");
            Changed(Replace(run, "VisitSequence", WireValue.Str("999")), "public.visit_links");
            if (visit.Fields.Any(f => f.FieldId.EndsWith(".Rewards", StringComparison.Ordinal)))
                Changed(Replace(First("CombatRewardSet"), "VisitId", WireValue.Str("999")), "public.visit_links");
        }
        Changed(Replace(First("MapNode"), "Visited", WireValue.Bool(!NormalPublicObservation.Flag(First("MapNode"), "Visited"))), "public.map");
        var edge = snapshot.Relations.First(r => r.TypeId.EndsWith(":MapChildren", StringComparison.Ordinal));
        Changed(snapshot with { Relations = snapshot.Relations.Where(r => r != edge).ToArray() }, "public.map");
        var parent = snapshot.Relations.First(r => r.TypeId.EndsWith(":MapParents", StringComparison.Ordinal));
        Changed(snapshot with { Relations = snapshot.Relations.Where(r => r != parent).ToArray() }, "public.map_parents");
        Changed(Replace(First("Run"), "TotalFloor", WireValue.Str("999")), "public.run");
        Changed(Replace(First("NeowOption"), "Position", WireValue.Str("999")), "public.neow");
        Changed(Replace(First("Player"), "GoldRewardAmount", WireValue.Str("999")), "public.reward_offer");
        Changed(Replace(First("Player"), "Hp", WireValue.Str("999")), "public.player");
        Changed(Replace(First("Card"), "Cost", WireValue.Str("999")), "public.cards");
        Changed(Replace(First("Relic"), "IsMelted", WireValue.Bool(!NormalPublicObservation.Flag(First("Relic"), "IsMelted"))), "public.relic_inventory");
        var cards = snapshot.Groups.Single(g => g.TypeId.EndsWith(":CardRewardOptions", StringComparison.Ordinal));
        if (cards.Members.Count > 0)
            Changed(snapshot with { Groups = snapshot.Groups.Select(g => g != cards ? g : g with { Members = [] }).ToArray(),
                Entities = snapshot.Entities.Where(e => !cards.Members.Any(m => m.EntityRef == e.InstanceId)).ToArray() }, "public.cards");
        var potions = snapshot.Groups.Single(g => g.TypeId.EndsWith(":Potions", StringComparison.Ordinal));
        if (potions.Members.Count > 0)
        {
            var held = snapshot.Entities.Single(e => e.InstanceId == potions.Members[0].EntityRef);
            Changed(Replace(held, "Slot", WireValue.Str("999")), "public.potion_inventory");
        }
        var node = First("MapNode"); var field = node.Fields.First(f => f.FieldId.EndsWith(".Visited", StringComparison.Ordinal));
        var unknown = snapshot with { Entities = snapshot.Entities.Select(e => e != node ? e : e with
        { Fields = e.Fields.Select(f => f != field ? f : f with { State = "unknown", Value = null }).ToArray() }).ToArray() };
        var rejected = false;
        try { Project(unknown); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, "unknown public field is not replaced by a hidden Engine value");
        var extended = snapshot with { Entities = snapshot.Entities.Select(e => e != node ? e : e with
        { Fields = [.. e.Fields, field with { FieldId = "net:field:base-spire:MapNode.FutureField", Value = WireValue.Bool(true) }] }).ToArray() };
        Require(NormalPublicObservation.UnmappedFields(extended).Contains("MapNode.FutureField"), "new public fields remain explicitly unverified until mapped");
        var relation = snapshot.Relations.First();
        Reject(snapshot with { Relations = [.. snapshot.Relations, relation with { SourceRef = First("Player").InstanceId }] }, "relation endpoint has wrong kind");
        Reject(snapshot with { Relations = snapshot.Relations.Select(r => r != relation ? r : r with { Position = 0 }).ToArray() }, "unordered relation has unexpected position");
        Changed(snapshot with { Relations = [.. snapshot.Relations, relation with { TypeId = "net:relation:base-spire:FutureRelation" }] }, "public.relation_types");
        void Reject(PublicSnapshotDto value, string name)
        {
            var failed = false;
            try { Project(value); } catch (NotSupportedException) { failed = true; }
            Require(failed, "actual public topology corruption fails: " + name);
        }
    }
    private static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Normal comparison check failed: " + name); }
}
