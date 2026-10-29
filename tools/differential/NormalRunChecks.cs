using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;

namespace Differential;

internal static class NormalRunChecks
{
    public static void Run(string repository)
    {
        NormalReportChecks.Run();
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
        CheckPublicCompleteness();
#if REFERENCE_SIM
        CheckNeowCallbackAvailability();
        CheckReferenceDiagnostics();
        CheckPotionDiscard(repository);
#endif
        Console.WriteLine("Normal comparison checks passed: all dimensions/first differences, zero execution, incomplete policy/return, and real Host diagnostic non-interference.");
    }
#if REFERENCE_SIM
    private static void CheckNeowCallbackAvailability()
    {
        Sts2Sim.Core.Demo.RunReplayDemo.EnsureModelsRegistered();
        var enabled = new Sts2Sim.Core.Events.EventOption("WingedBoots", () => Task.CompletedTask);
        var locked = new Sts2Sim.Core.Events.EventOption("WingedBoots", null);
        Require(NormalReferenceNeow.Read(enabled, 0).EffectSupported && !NormalReferenceNeow.Read(locked, 0).EffectSupported,
            "native option availability reads the real callback, including the disabled control; it is not a true constant");
        var rejected = false;
        try { NormalReferenceNeow.Read(new("UnknownUnregisteredRelic", () => Task.CompletedTask), 0); }
        catch (NotSupportedException) { rejected = true; }
        Require(rejected, "unknown native callback target cannot acquire a fabricated support value");
    }

    private static void CheckPotionDiscard(string repository)
    {
        var directory = Path.Combine(repository, ".artifacts/differential/diagnostic-checks");
        using var left = new NormalDslRun(repository, Path.Combine(directory, "potion-discard.ndjson"));
        using var right = new NormalReferenceRun("777");
        left.Start("777"); left.Step(left.Action("StartNormalRun")); right.Start();
        var histories = new List<NormalPlayerHistory>();
        Apply(new("neow", ["base-spire:booming-conch"])); Apply(new("leave-neow", []));
        Apply(left.Semantic(left.Action("ChooseMapPoint")));
        for (var i = 0; left.Phase == "combat" && i < 100; i++)
            Apply(left.Semantic(new[] { "PlayAttackFromHand", "PlayCardFromHand", "EndTurn" }.Select(left.Find).First(a => a is not null)!));
        Require(left.Phase == "rewards", "potion mapping check reaches real rewards");
        var victory = left.Capture("victory");
        var generatedReward = System.Text.Json.JsonSerializer.Deserialize<NormalRewardSetView[]>(victory.Values["public.reward_sets"])!.Single();
        Require(generatedReward is { Source: "monster", Character: "base-spire:ironclad", Act: 0, Ascension: 0, CardCount: 3,
            RarityPolicy: "regular-encounter", Generated: true, Closed: false, Potion: not null,
            Policies: { PoolEligibility: "all-unlocked-ironclad", UpgradePolicy: "act-one", ModifiersPolicy: "none", RandomStream: "base-spire:player:rewards" } },
            "real reward generation metadata, policies and pending potion");
        Require(histories.Any(h => h.Plays > 0) && histories.Any(h => h.HpLosses > 0)
            && histories[^1] is { Plays: 0, HpLosses: 0, LostHpThisTurn: false, ExhaustedThisTurn: false },
            "real combat increments play and HP-loss counters before victory clears active history");
        using (var diagnostic = System.Text.Json.JsonDocument.Parse(NormalPreflight.Json(right.Capture("victory").Diagnostic)))
        {
            var retained = diagnostic.RootElement.GetProperty("native_player_history");
            var configuration = System.Text.Json.JsonSerializer.Deserialize<NormalRunConfiguration>(diagnostic.RootElement.GetProperty("native_configuration"))!;
            var rest = System.Text.Json.JsonSerializer.Deserialize<NormalRestContext>(diagnostic.RootElement.GetProperty("native_rest_context"))!;
            Require(rest.VisitedTypes.SequenceEqual(new[] { "Ancient", "Monster" }) && rest.CurrentRoom == "Monster"
                && NormalReferenceRestState.Normalize(rest) is { RoomUsed: false, PendingForge: false },
                "actual first-fight history proves that no rest-site decision has occurred");
            foreach (var invalid in new[] { rest with { Act = 1 }, rest with { TotalFloor = 3 }, rest with { RoomCount = 2 },
                rest with { CurrentRoom = "RestSite" }, rest with { VisitedTypes = ["Ancient", "RestSite"] },
                rest with { VisitedTypes = ["Ancient", "Unknown"] } })
            {
                var rejected = false;
                try { NormalReferenceRestState.Normalize(invalid); } catch (NotSupportedException) { rejected = true; }
                Require(rejected, "rest-state absence cannot be assumed after unsupported room or history");
            }
            Require(configuration.BaseDraw == 5 && NormalReferenceConfiguration.Profile(configuration) == "ironclad-a0-fully-unlocked-singleplayer",
                "actual native unlock state and base draw configuration determine the declared profile");
            foreach (var invalid in new[] { configuration with { Ascension = 1 }, configuration with { Players = 2 },
                configuration with { AllEpochs = false }, configuration with { AllColorless = false }, configuration with { FullCharacterPool = false } })
            {
                var rejected = false;
                try { NormalReferenceConfiguration.Profile(invalid); } catch (NotSupportedException) { rejected = true; }
                Require(rejected, "profile does not claim unsupported or partially unlocked configuration");
            }
            Require(!retained.GetProperty("active").GetBoolean() && retained.GetProperty("retained_plays").GetInt32() > 0
                && retained.GetProperty("retained_hp_losses").GetInt32() > 0,
                "normalization retains the actual completed native history in diagnostics");
            var policy = System.Text.Json.JsonSerializer.Deserialize<NormalRewardPolicyEvidence[]>(diagnostic.RootElement.GetProperty("native_reward_policies"))!.Single();
            Require(policy.UnlockedPool.Length == 85 && NormalReferenceRewards.Classify(policy) == generatedReward.Policies,
                "policy names have actual full native pool and default-path evidence");
            foreach (var invalid in new[] { policy with { Act = 1 }, policy with { Players = 2 }, policy with { DefaultEncounter = false },
                policy with { SemanticKeys = true }, policy with { UnlockedPool = policy.UnlockedPool[..^1] }, policy with { Modifiers = ["changed-hook"] } })
            {
                var rejected = false;
                try { NormalReferenceRewards.Classify(invalid); } catch (NotSupportedException) { rejected = true; }
                Require(rejected, "policy classification rejects unsupported context instead of filling expected labels");
            }
        }
        NormalScalarChecks.Run(left.DebugRecords, victory, right.Capture("victory"));
        NormalRandomStateChecks.Run(left.DebugRecords, left.RandomSnapshot, victory, right.Capture("victory"));
        NormalRewardCandidateChecks.Run(left.DebugRecords, victory, right.Capture("victory"));
        var shuffleCalls = System.Text.Json.JsonSerializer.Deserialize<NormalShuffleCall[]>(victory.Values["random.shuffle_candidates"])!;
        Require(shuffleCalls.Length == 19 && victory.Values["random.shuffle_candidates"] == right.Capture("victory").Values["random.shuffle_candidates"],
            "both actual shuffles retain all 19 ordered candidate lists");
        var alteredShuffle = shuffleCalls.ToArray();
        alteredShuffle[0] = alteredShuffle[0] with { Candidates = alteredShuffle[0].Candidates.Reverse().ToArray() };
        foreach (var injected in new[] { shuffleCalls[..^1], shuffleCalls.Reverse().ToArray(), alteredShuffle,
            new[] { shuffleCalls[0] with { Selected = (shuffleCalls[0].Selected + 1) % shuffleCalls[0].Candidates.Length } }.Concat(shuffleCalls.Skip(1)).ToArray() })
        {
            var changed = new SortedDictionary<string, string>(victory.Values, StringComparer.Ordinal)
                { ["random.shuffle_candidates"] = NormalPreflight.Json(injected) };
            Require(NormalRunComparison.First(0, victory, victory with { Values = changed })?.Path == "random.shuffle_candidates",
                "missing, reordered or altered shuffle candidate calls must report a difference");
            var rejected = false;
            try { NormalShuffleTrace.Canonical(injected); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "incomplete shuffles and candidates inconsistent with actual swaps must fail normalization");
        }
        foreach (var invalid in new[] { shuffleCalls[0] with { Candidates = [shuffleCalls[0].Candidates[0], shuffleCalls[0].Candidates[0]] },
            shuffleCalls[0] with { Selected = -1 }, shuffleCalls[0] with { Selected = shuffleCalls[0].Candidates.Length },
            shuffleCalls[0] with { After = 2 } })
        {
            var rejected = false;
            try { NormalShuffleTrace.Canonical([invalid, .. shuffleCalls.Skip(1)]); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "duplicate card origins, invalid indices and discontinuous shuffle cursors must fail");
        }
        NormalWeightedChecks.Run(left.DebugRecords, victory);
        NormalOpeningAttemptChecks.Run(left.DebugRecords, victory);
        CheckNativeEventCoverage(right.Capture("victory"));
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
        Apply(new("claim-potion", []));
        Require(RewardSet() == generatedReward with { Potion = null, PotionPending = false }, "claim clears only the pending reward potion link");
        Apply(new("close-rewards", []));
        Require(RewardSet() == generatedReward with { Potion = null, PotionPending = false, Closed = true }, "room exit closes and retains the same generated reward context");
        CheckPublicProjection(left.Snapshot);
        var discard = left.Semantic(left.Action("DiscardPotion"));
        Require(discard.Arguments.Single().Contains(":potion-reward:0:", StringComparison.Ordinal), "held potion retains its actual reward creation origin");
        Apply(discard);
        Require(left.Find("DiscardPotion") is null && left.Capture("discarded").Values["permanent.potions"] == "[]", "the same acquired potion is removed by the real public action");
        Require(left.CheckRequestReplay().Passed, "discard request cache and stale rejection preserve state and RNG");
        NormalRewardSetView RewardSet() => System.Text.Json.JsonSerializer.Deserialize<NormalRewardSetView[]>(left.Capture("reward-check").Values["public.reward_sets"])!.Single();
        void Apply(NormalAction action)
        {
            left.Apply(action); right.Apply(action);
            var actual = left.Capture("potion-check");
            histories.Add(System.Text.Json.JsonSerializer.Deserialize<NormalPlayerHistory>(actual.Values["public.player_history"])!);
            var difference = NormalRunComparison.First(0, actual, right.Capture("potion-check"));
            Require(difference is null,
                "real acquisition and discard match reference candidates, assets, events and RNG: " + NormalPreflight.Json(difference));
        }
    }

    private static void CheckNativeEventCoverage(NormalBoundary boundary)
    {
        using var diagnostic = System.Text.Json.JsonDocument.Parse(NormalPreflight.Json(boundary.Diagnostic));
        var coverage = System.Text.Json.JsonSerializer.Deserialize<NormalNativeEventCoverage>(diagnostic.RootElement.GetProperty("native_event_coverage"))!;
        Require(coverage.Total == 773 && coverage.Mapped == 77 && coverage.Listeners.Length == 696
            && coverage.Unmapped.Length == 0 && coverage.Listeners.All(l => l.DefaultNoOp),
            "all 773 real notifications are accounted for: 77 projected and 696 default virtual-method entries/exits");
        Require(coverage.Listeners.Select(l => l.Hook).Distinct().Count() == 4
            && coverage.Listeners.Select(l => l.Side).Distinct().Order().SequenceEqual(new[] { "Enemy", "Player" }),
            "real listener evidence preserves all four hook phases and both actual side values");
        var model = Sts2Sim.Core.Models.ModelDb.Relic<Sts2Sim.Core.Models.Relics.BurningBlood>();
        object?[] Args(bool starting) => ["AfterSideTurnEnd", model, Sts2Sim.Core.Combat.CombatSide.Player, starting];
        foreach (var mode in new[] { "finish-first", "missing-finish", "nested-start", "wrong-side" })
        {
            var audit = new NormalReferenceEventCoverage(); var rejected = false;
            try
            {
                if (mode == "finish-first") audit.Record("SideTurnEndListener", Args(false), false);
                else
                {
                    audit.Record("SideTurnEndListener", Args(true), false);
                    if (mode == "nested-start") audit.Record("SideTurnEndListener", Args(true), false);
                    if (mode == "wrong-side") audit.Record("SideTurnEndListener",
                        ["AfterSideTurnEnd", model, Sts2Sim.Core.Combat.CombatSide.Enemy, false], false);
                    audit.Capture();
                }
            }
            catch (NotSupportedException) { rejected = true; }
            Require(rejected, "missing, nested, wrong-side and unmatched listener pairs cannot be classified");
        }
        var overridden = new NormalReferenceEventCoverage();
        var power = Sts2Sim.Core.Models.ModelDb.Power<Sts2Sim.Core.Models.Powers.VulnerablePower>();
        foreach (var starting in new[] { true, false })
            overridden.Record("SideTurnEndListener", ["AfterSideTurnEnd", power, Sts2Sim.Core.Combat.CombatSide.Player, starting], false);
        overridden.Record("FutureUnmappedObserver", [], false);
        Require(overridden.Capture() is { Unmapped.Length: 3 } unknown && unknown.Listeners.All(l => !l.DefaultNoOp),
            "overridden hooks stay unmapped even on a side where their body has no effect; unknown observers stay visible");
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
            var action = observed.Semantic(new[] { "PlayAttackFromHand", "PlayCardFromHand", "EndTurn" }
                .Select(observed.Find).First(a => a is not null)!);
            observed.Apply(action); plain.Apply(action); Check();
        }
        void Check()
        {
            CheckPublicProjection(observed.Snapshot);
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
        Changed(snapshot with { Sequences = snapshot.Sequences.Select(s => s != sequence ? s : s with { Elements = s.Elements.Reverse().ToArray() }).ToArray() }, "public.map_sequence");
        Changed(Replace(First("MapNode"), "Index", WireValue.Str("999")), "public.map");
        Reject(snapshot with { Entities = snapshot.Entities.Select(e => e != First("MapNode") ? e : e with
            { Fields = e.Fields.Where(f => !f.FieldId.EndsWith(".Index", StringComparison.Ordinal)).ToArray() }).ToArray() }, "missing public map index");
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
                Changed(Replace(First("CombatRewardSet"), "VisitId", WireValue.Str("999")), "public.reward_sets");
        }
        Changed(Replace(First("MapNode"), "Visited", WireValue.Bool(!NormalPublicObservation.Flag(First("MapNode"), "Visited"))), "public.map");
        var edge = snapshot.Relations.First(r => r.TypeId.EndsWith(":MapChildren", StringComparison.Ordinal));
        Changed(snapshot with { Relations = snapshot.Relations.Where(r => r != edge).ToArray() }, "public.map");
        var parent = snapshot.Relations.First(r => r.TypeId.EndsWith(":MapParents", StringComparison.Ordinal));
        Changed(snapshot with { Relations = snapshot.Relations.Where(r => r != parent).ToArray() }, "public.map_parents");
        Changed(Replace(First("Run"), "TotalFloor", WireValue.Str("999")), "public.run");
        Changed(Replace(First("Run"), "Profile", WireValue.Str("corrupted-profile")), "public.run");
        Changed(Replace(First("NeowOption"), "Position", WireValue.Str("999")), "public.neow");
        Changed(Replace(First("NeowOption"), "EffectSupported", WireValue.Bool(!NormalPublicObservation.Flag(First("NeowOption"), "EffectSupported"))), "public.neow");
        Reject(snapshot with { Entities = snapshot.Entities.Select(e => e != First("NeowOption") ? e : e with
            { Fields = e.Fields.Where(f => !f.FieldId.EndsWith(".EffectSupported", StringComparison.Ordinal)).ToArray() }).ToArray() }, "missing Neow option effect availability");
        Changed(Replace(First("Player"), "GoldRewardAmount", WireValue.Str("999")), "public.reward_offer");
        if (snapshot.Entities.Any(e => e.KindId.EndsWith(":CombatRewardSet", StringComparison.Ordinal)))
        {
            var rewardSet = First("CombatRewardSet");
            foreach (var name in new[] { "Source", "CharacterId", "ActIndex", "Ascension", "CardCount", "RarityPolicy", "PoolEligibility", "UpgradePolicy", "ModifiersPolicy", "RandomStream" })
                Changed(Replace(rewardSet, name, WireValue.Str("999")), "public.reward_sets");
            foreach (var name in new[] { "Generated", "Closed" })
                Changed(Replace(rewardSet, name, WireValue.Bool(!NormalPublicObservation.Flag(rewardSet, name))), "public.reward_sets");
            if (rewardSet.Fields.Any(f => f.FieldId.EndsWith(".Potion", StringComparison.Ordinal)))
                Changed(snapshot with { Entities = snapshot.Entities.Select(e => e != rewardSet ? e : e with
                    { Fields = e.Fields.Where(f => !f.FieldId.EndsWith(".Potion", StringComparison.Ordinal)).ToArray() }).ToArray() }, "public.reward_sets");
        }
        Changed(Replace(First("Player"), "Hp", WireValue.Str("999")), "public.player");
        Changed(Replace(First("Player"), "AutoDrawPerTurn", WireValue.Str("999")), "public.player");
        foreach (var name in new[] { "RoomUsed", "PendingForge", "CombatTimedOut" })
            Changed(Replace(First("Player"), name, WireValue.Bool(!NormalPublicObservation.Flag(First("Player"), name))), "public.player");
        if (snapshot.Entities.Any(e => e.KindId.EndsWith(":Power", StringComparison.Ordinal)))
        {
            var power = First("Power");
            Changed(Replace(power, "ExpiresAtTurnEnd", WireValue.Bool(!NormalPublicObservation.Flag(power, "ExpiresAtTurnEnd"))), "public.powers");
        }
        if (snapshot.Entities.Any(e => e.KindId.EndsWith(":Enemy", StringComparison.Ordinal)))
        {
            var enemy = First("Enemy");
            Changed(Replace(enemy, "MoveIndex", WireValue.Str("999")), "public.enemies");
            Changed(Replace(enemy, "Escaped", WireValue.Bool(!NormalPublicObservation.Flag(enemy, "Escaped"))), "public.enemies");
            Changed(Replace(enemy, "Intent", WireValue.Str("corrupted-intent")), "public.enemies");
        }
        foreach (var name in new[] { "PlaySequence", "HpLossCount" })
            Changed(Replace(First("Player"), name, WireValue.Str("999")), "public.player_history");
        foreach (var name in new[] { "LostHpThisTurn", "ExhaustedCardThisTurn", "EnemyPhase", "UsesRunDeck" })
            Changed(Replace(First("Player"), name, WireValue.Bool(!NormalPublicObservation.Flag(First("Player"), name))), "public.player_history");
        Changed(Replace(First("Player"), "DrawBlocked", WireValue.Bool(!NormalPublicObservation.Flag(First("Player"), "DrawBlocked"))), "public.draw_blocked");
        var drawPlayer = First("Player");
        Reject(snapshot with { Entities = snapshot.Entities.Select(e => e != drawPlayer ? e : e with { Fields = e.Fields.Where(f => !f.FieldId.EndsWith("Player.DrawBlocked", StringComparison.Ordinal)).ToArray() }).ToArray() }, "missing public DrawBlocked");
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
        var wrongNamespace = snapshot with { Entities = snapshot.Entities.Select(e => e != node ? e : e with
        { Fields = e.Fields.Select(f => f != field ? f : f with { FieldId = "net:field:other-mod:MapNode.Visited" }).ToArray() }).ToArray() };
        Require(NormalPublicObservation.UnmappedFields(wrongNamespace).Contains("net:field:other-mod:MapNode.Visited"), "a matching field suffix in another namespace is not mapped");
        Reject(wrongNamespace, "foreign field namespace");
        Reject(snapshot with { Entities = snapshot.Entities.Select(e => e != run ? e : e with { Fields = [.. e.Fields, field] }).ToArray() }, "known field attached to the wrong entity kind");
        Reject(snapshot with { Entities = snapshot.Entities.Select(e => e != run ? e : e with { ContentState = "unknown" }).ToArray() }, "changed non-content entity visibility");
        Reject(snapshot with { Entities = snapshot.Entities.Select(e => e != run ? e : e with { KindId = "net:kind:other-mod:Run" }).ToArray() }, "foreign entity kind namespace");
        // Every currently published field must influence the semantic result or be rejected.
        // One example per qualified field avoids repeating the same check for every map node.
        var omitted = new List<string>();
        foreach (var pair in snapshot.Entities.SelectMany(e => e.Fields.Select(f => (entity: e, field: f))).GroupBy(p => p.field.FieldId).Select(g => g.First()))
        {
            var changedValue = pair.field.Value switch
            {
                WireBool b => WireValue.Bool(!b.Value),
                WireString s when long.TryParse(s.Value, out var n) => WireValue.Str((n == long.MaxValue ? n - 1 : n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                WireString s => WireValue.Str(s.Value + "-corrupted"),
                _ => throw new InvalidOperationException("Unexpected normal public value shape.")
            };
            var changedSnapshot = snapshot with { Entities = snapshot.Entities.Select(e => e != pair.entity ? e : e with
                { Fields = e.Fields.Select(f => f != pair.field ? f : f with { Value = changedValue }).ToArray() }).ToArray() };
            var detected = false;
            try { detected = NormalRunComparison.First(0, original, original with { Values = Project(changedSnapshot) }) is not null; }
            catch (Exception error) when (error is NotSupportedException or InvalidOperationException or KeyNotFoundException or FormatException) { detected = true; }
            if (!detected) omitted.Add(pair.field.FieldId);
        }
        Require(omitted.Count == 0, "published fields cannot disappear from comparison: " + string.Join(", ", omitted));
        var expected = new IdentityDto(snapshot.ProtocolVersion, snapshot.RulesetId, snapshot.CatalogId, snapshot.BackendProfileId);
        NormalPublicSnapshot.ValidateEnvelope(snapshot, expected, snapshot.SessionId);
        foreach (var corrupted in new[] { snapshot with { RulesetId = "wrong" }, snapshot with { CatalogId = "wrong" },
            snapshot with { BackendProfileId = "wrong" }, snapshot with { ProtocolVersion = "wrong" }, snapshot with { SessionId = "wrong" },
            snapshot with { ObservationId = -1 }, snapshot with { Status = "terminal" } })
        {
            var failed = false;
            try { NormalPublicSnapshot.ValidateEnvelope(corrupted, expected, snapshot.SessionId); }
            catch (NotSupportedException) { failed = true; }
            Require(failed, "public envelope is checked against the actual handshake/session");
        }
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
    private static void CheckPublicCompleteness()
    {
        var paths = NormalRunComparison.RequiredPaths.Where(p => p.StartsWith("public.", StringComparison.Ordinal) && p != "public.snapshot")
            .Concat(new[] { "actions.candidates", "decisions.current" }).ToArray();
        SortedDictionary<string, string> Values() => new(paths.ToDictionary(p => p, _ => "[]"), StringComparer.Ordinal);
        var complete = Values();
        Require(NormalPublicSnapshot.Add(complete, [], "decision") && complete.ContainsKey("public.snapshot"), "complete public semantic surfaces form a snapshot");
        foreach (var path in paths)
        {
            var missing = Values(); missing.Remove(path);
            Require(!NormalPublicSnapshot.Add(missing, [], "decision") && !missing.ContainsKey("public.snapshot"), "missing public component cannot pass: " + path);
        }
        Require(!NormalPublicSnapshot.Add(Values(), ["MapNode.FutureField"], "decision"), "unknown fields block complete public snapshot");
    }
    private static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Normal comparison check failed: " + name); }
}
