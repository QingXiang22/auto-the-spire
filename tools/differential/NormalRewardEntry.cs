using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace Differential;

/// <summary>Normal first combat/reward prefix. Only public legal submissions mutate base state.</summary>
internal static partial class NormalRewardEntry
{
    internal interface Side
    {
        string Phase { get; }
        IReadOnlyList<ActionOption> Actions();
        IReadOnlyList<FactSnapshot> Facts();
        IReadOnlyList<StreamSnapshot> Streams(int index, string kind);
        IReadOnlyList<DecisionSnapshot> Execute(string action, string? token, IReadOnlyList<DecisionSnapshot>? expectedDecisions = null);
        DiagnosticSnapshot? Diagnostic();
    }

    // Visible-candidate policy produces one semantic plan; reference independently executes that same plan.
    private static void Drive(Side side, List<BoundaryRecord> records, bool skip, List<NormalSemanticAction> plan, int combatCount, bool discardAcquiredPotion, bool usePotions, string? eventAction = null, bool continueAfterEvent = false, string? capacityPolicy = null)
    {
        void Add(string kind, IReadOnlyList<DecisionSnapshot> decisions) => records.Add(new(
            records.Count, kind, kind, side.Actions(), decisions, side.Facts(),
            side.Streams(records.Count, kind), side.Diagnostic()));
        Add("start", []);
        int closedCombats = 0;
        bool enteringEvent = false, finishedEvent = false, eventCompleted = false;
        for (var i = 0; i < 200; i++)
        {
            var phase = side.Phase;
            if (phase == "travel-ready")
            {
                var settled = side.Facts().ToDictionary(f => f.Path, f => f.Value);
                if (settled["run.hp"] == "0") throw new CheckFailed("Normal prefix reached death before required reward closure");
                closedCombats = int.Parse(settled["run.room"]);
                if (finishedEvent)
                {
                    if (!continueAfterEvent) return;
                    finishedEvent = false; enteringEvent = false; eventCompleted = true;
                }
                if (closedCombats == combatCount && (eventAction is null || eventCompleted)) return;
                if (eventAction is not null && !eventCompleted && closedCombats == 2) enteringEvent = true;
            }
            var actions = side.Actions();
            if (actions.Count == 0) throw new CheckFailed("Normal prefix ended before the required reward closure; last phase=" + phase
                + ", HP=" + side.Facts().Single(f => f.Path == "run.hp").Value);
            string action; string? token = null;
            if (phase is "map" or "travel-ready")
            {
                action = "base-spire:ChooseMapPoint";
                var options = actions.Single(a => a.Action == action).Candidates;
                var facts = side.Facts().ToDictionary(f => f.Path, f => f.Value);
                var next = options.Order(StringComparer.Ordinal).Where(t => facts.GetValueOrDefault("map." + t + ".type") == (enteringEvent ? "type:1" : "monster"));
                if (capacityPolicy is not null && eventCompleted && !enteringEvent)
                    next = options.Where(t => facts.GetValueOrDefault("map." + t + ".type") is "monster" or "type:6")
                        .OrderBy(t => facts.GetValueOrDefault("map." + t + ".type") == "monster" ? 0 : 1)
                        .ThenBy(t => t, StringComparer.Ordinal);
                token = next.FirstOrDefault()
                    ?? throw new CheckFailed("Normal prefix has no legal next " + (enteringEvent ? "Unknown" : "Monster") + " map point");
            }
            else if (phase == "event" && eventAction is not null)
            {
                action = eventAction;
                if (!side.Facts().Any(f => f.Path == "run.event" && f.Value == "aromaofchaos"))
                    throw new CheckFailed("Normal event policy requires the actual AromaOfChaos source identity");
                finishedEvent = true;
            }
            else if (phase == "rewards")
            {
                if (actions.Any(a => a.Action == "base-spire:TakeRewardCard"))
                {
                    action = skip ? "base-spire:SkipRewardCard" : "base-spire:TakeRewardCard";
                    if (!skip) token = "reward:card:0";
                }
                else if (actions.Any(a => a.Action == "base-spire:SkipRewardPotion"))
                {
                    if (capacityPolicy is not null && !actions.Any(a => a.Action == "base-spire:TakeRewardPotion"))
                    {
                        action = capacityPolicy == "skip" ? "base-spire:SkipRewardPotion" : "base-spire:DiscardPotion";
                        if (action == "base-spire:DiscardPotion")
                            token = actions.Single(a => a.Action == action).Candidates.Order(StringComparer.Ordinal).ElementAt(1);
                    }
                    else action = skip ? "base-spire:SkipRewardPotion" : "base-spire:TakeRewardPotion";
                }
                else if (discardAcquiredPotion && actions.FirstOrDefault(a => a.Action == "base-spire:DiscardPotion") is { } discard)
                {
                    action = discard.Action;
                    token = discard.Candidates.Order(StringComparer.Ordinal).First();
                }
                else if (actions.Any(a => a.Action == "base-spire:SkipRewardRelic")) action = "base-spire:SkipRewardRelic";
                else action = "base-spire:TakeRewardGold";
            }
            else if (phase == "combat")
            {
                if (usePotions && actions.FirstOrDefault(a => a.Action == "base-spire:UsePotion") is { } use)
                {
                    action = use.Action; token = use.Candidates.Order(StringComparer.Ordinal).First();
                }
                else
                {
                var play = actions.FirstOrDefault(a => a.Action == "base-spire:PlayCard");
                var facts = side.Facts().ToDictionary(f => f.Path, f => f.Value);
                token = play?.Candidates.OrderBy(t => capacityPolicy is not null && facts.GetValueOrDefault("visible." + t + ".definition") == "base-spire:flame-barrier" ? -2
                    : capacityPolicy is not null && facts.GetValueOrDefault("visible." + t + ".definition") == "base-spire:headbutt"
                        && facts.GetValueOrDefault("pile.discard.sorted", "").Contains("base-spire:flame-barrier", StringComparison.Ordinal) ? -2
                    : continueAfterEvent && eventCompleted && facts.GetValueOrDefault("visible." + t + ".kind") == "power" ? -1
                    : facts.GetValueOrDefault("visible." + t + ".definition") switch
                    { "base-spire:bash" => 0, "base-spire:strike-ironclad" => 1, "base-spire:defend-ironclad" => 2, _ => 3 })
                    .ThenBy(t => t, StringComparer.Ordinal).FirstOrDefault();
                action = token is null ? "base-spire:EndTurn" : "base-spire:PlayCard";
                }
            }
            else throw new CheckFailed("Cannot continue normal prefix phase=" + phase);
            var beforeVisible = VisibleCandidates(side.Facts());
            var decisions = side.Execute(action, token);
            plan.Add(new(action, token, actions, beforeVisible, decisions));
            Add("step:" + i + ":" + action + (token is null ? "" : ":" + token), decisions);
        }
        throw new CheckFailed("Normal prefix exceeded 200 visible actions (not a defeat)");
    }

    public static CaseRecord RunBase(FixtureCase fixture)
    {
        List<BoundaryRecord> records = [];
        List<NormalSemanticAction> plan = [];
        BaseSpireRunSession? run = null; long before = 0;
        string status = "ok"; string? detail = null;
        try
        {
            Validate(fixture);
            run = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0,
                fixture.BaseScope == BaseScopes.DevRun ? BaseSpireScope.DevRun : BaseSpireScope.DevMap) with
                { RandomLayoutId = RunRandomLayout.V109 }).CreateSession(fixture.GameSeed!);
            run.Start(); before = run.Session.Random.TotalDraws;
            if (IsActRun(fixture)) DriveAct(new BaseSide(run.Session, capacityStrategy: true, runLifecycle: true), records, plan,
                fixture.Script.Single().Choose == "defensive");
            else Drive(new BaseSide(run.Session, fixture.Script.Single().Action == "normal_reward_use_cancel", CapacityPolicy(fixture) is not null), records, fixture.Script.Single().Action == "normal_reward_skip", plan, fixture.NormalCombatCount,
                fixture.Script.Single().Action == "normal_reward_take_discard", fixture.Script.Single().Action is "normal_reward_use_first" or "normal_reward_use_cancel",
                EventAction(fixture), fixture.Script.Single().Action.EndsWith("_next_combat", StringComparison.Ordinal) || CapacityPolicy(fixture) is not null,
                CapacityPolicy(fixture));
            if (fixture.Script.Single().Action is "normal_reward_use_first" or "normal_reward_use_cancel"
                && !plan.Any(a => a.Action == "base-spire:UsePotion")) throw new CheckFailed("Required real potion use did not occur");
            RequirePotionAcquisition(fixture, plan, records);
            RequirePotionDiscard(fixture, plan, records);
            RequireEvent(fixture, plan);
            if (!IsActRun(fixture)) RequireCombatClosure(fixture, records);
            RequirePostEventCarryover(fixture, plan, records);
            RequireCapacity(fixture, plan, records);
            if (!IsActRun(fixture) && !records.Last().Facts.Any(f => f.Path == "run.phase" && f.Value == "travel-ready"))
                throw new CheckFailed("First victory/reward closure not reached");
        }
        catch (Exception e) { status = "faulted"; detail = e.GetType().Name + ": " + e.Message; }
        return new(fixture.Id, BaseBackend.FormalName, IsActRun(fixture) ? "normal-run@1" : "normal-reward-prefix@1", fixture.RngProfile, status, detail,
            run is not null && run.Session.Random.TotalDraws != before,
            [$"Normal {fixture.BaseScope} factory; Start once; no initial-state writes. Action selection uses published definitions, HP, piles and map types only.",
             IsActRun(fixture) ? "Continuous Run: stops only at actual defeat, first-act completion or an explicit fault; no combat-count cutoff."
                : $"Stop after {fixture.NormalCombatCount} normal combat reward closures; full legal successor candidates retained."], records)
        { BaseSource = fixture.BaseSource, BuildId = run?.Build.PublicBuildId, SessionStatus = run?.Session.Status.ToString(),
          SessionIdentity = run?.Session.Identity, InitializationRandomCount = before, SemanticActions = plan,
          Terminal = run?.Session.Terminal?.Definition.QualifiedId };
    }

    private static IReadOnlyList<FactSnapshot> VisibleCandidates(IReadOnlyList<FactSnapshot> facts) =>
        facts.Where(f => f.Path.StartsWith("visible.card:", StringComparison.Ordinal)
            || f.Path.StartsWith("visible.enemy:", StringComparison.Ordinal)
            || f.Path == "reward.cards.order").OrderBy(f => f.Path, StringComparer.Ordinal).ToArray();

    private static void RequirePotionAcquisition(FixtureCase fixture, IReadOnlyList<NormalSemanticAction> plan, IReadOnlyList<BoundaryRecord> records)
    {
        if (fixture.RequirePotionAcquisition && (!plan.Any(a => a.Action == "base-spire:TakeRewardPotion")
            || !records.Last().Facts.Any(f => f.Path == "potion.inventory" && f.Value.Length > 0)))
            throw new CheckFailed("Required normal potion acquisition did not occur or persist to reward closure");
    }

    private static void RequirePotionDiscard(FixtureCase fixture, IReadOnlyList<NormalSemanticAction> plan, IReadOnlyList<BoundaryRecord> records)
    {
        if (fixture.Script.Single().Action != "normal_reward_take_discard") return;
        var take = plan.ToList().FindIndex(a => a.Action == "base-spire:TakeRewardPotion");
        var discard = plan.ToList().FindIndex(a => a.Action == "base-spire:DiscardPotion");
        if (take < 0 || discard <= take
            || !records[discard].Facts.Any(f => f.Path == "potion.inventory" && f.Value.Length > 0)
            || !records.Last().Facts.Any(f => f.Path == "potion.inventory" && f.Value.Length == 0))
            throw new CheckFailed("Required normal potion take/discard sequence did not occur");
    }

    private static string? EventAction(FixtureCase fixture) => fixture.Script.Single().Action switch
    {
        "normal_aroma_upgrade" or "normal_aroma_upgrade_next_combat" or "normal_capacity_skip" or "normal_capacity_discard" => "base-spire:AromaOfChaosMaintainControl",
        "normal_aroma_transform" or "normal_aroma_transform_next_combat" => "base-spire:AromaOfChaosLetGo",
        _ => null,
    };

    private static string? CapacityPolicy(FixtureCase fixture) => fixture.Script.Single().Action switch
    {
        "normal_capacity_skip" => "skip",
        "normal_capacity_discard" => "discard",
        _ => null,
    };

    private static void RequireCapacity(FixtureCase fixture, IReadOnlyList<NormalSemanticAction> plan, IReadOnlyList<BoundaryRecord> records)
    {
        if (CapacityPolicy(fixture) is not { } policy) return;
        Dictionary<string, string> Facts(int index) => records[index].Facts.ToDictionary(f => f.Path, f => f.Value);
        var roomTypes = Enumerable.Range(0, records.Count).Select(Facts)
            .Where(f => f.GetValueOrDefault("run.phase") == "combat")
            .DistinctBy(f => f["run.room"]).Select(f => f["run.room_type"]).ToArray();
        if (!roomTypes.SequenceEqual(new[] { "monster", "monster", "monster", "monster", "monster", "elite" }))
            throw new CheckFailed("Capacity route must retain five actual Monster rooms and one actual Elite room");
        var relicSkip = Enumerable.Range(0, plan.Count).SingleOrDefault(i => plan[i].Action == "base-spire:SkipRewardRelic", -1);
        if (relicSkip < 0 || Facts(relicSkip).GetValueOrDefault("run.room_type") != "elite"
            || Facts(relicSkip).GetValueOrDefault("reward.relic.present") != "true"
            || Facts(relicSkip).GetValueOrDefault("reward.relic.resolved") != "false"
            || string.IsNullOrEmpty(Facts(relicSkip).GetValueOrDefault("reward.relic.definition"))
            || Facts(relicSkip + 1).GetValueOrDefault("reward.relic.resolved") != "true"
            || Facts(relicSkip)["relic.inventory.order"] != Facts(relicSkip + 1)["relic.inventory.order"]
            || Facts(records.Count - 1)["run.phase"] != "travel-ready"
            || !records.Last().Actions.Any(a => a.Action == "base-spire:ChooseMapPoint"))
            throw new CheckFailed("Real Elite relic reward must be generated, explicitly skipped and fully closed before travel");
        var index = Enumerable.Range(0, plan.Count).Where(i =>
        {
            if (plan[i].Action != (policy == "skip" ? "base-spire:SkipRewardPotion" : "base-spire:DiscardPotion")) return false;
            var b = records[i];
            var f = b.Facts.ToDictionary(f => f.Path, f => f.Value);
            return f.GetValueOrDefault("run.phase") == "rewards" && f.GetValueOrDefault("reward.potion.present") == "true"
                && f.GetValueOrDefault("reward.potion.resolved") == "false"
                && f["potion.inventory"].Split(',', StringSplitOptions.RemoveEmptyEntries).Length == 3
                && b.Actions.All(a => a.Action != "base-spire:TakeRewardPotion");
        }).FirstOrDefault(-1);
        if (index < 0 || index >= plan.Count) throw new CheckFailed("Required real full-belt potion reward was not reached");
        var before = Facts(index); var after = Facts(index + 1);
        var original = before["potion.inventory.origins"].Split(',');
        bool SameRandom(int left, int right) => records[left].Streams.OrderBy(s => s.Stream, StringComparer.Ordinal)
            .SequenceEqual(records[right].Streams.OrderBy(s => s.Stream, StringComparer.Ordinal));
        if (policy == "skip")
        {
            if (plan[index].Action != "base-spire:SkipRewardPotion" || after["reward.potion.resolved"] != "true"
                || after["potion.inventory.origins"] != before["potion.inventory.origins"] || !SameRandom(index, index + 1))
                throw new CheckFailed("Full-belt skip did not preserve all held instances and random state");
            return;
        }
        if (plan[index].Action != "base-spire:DiscardPotion" || plan[index].Token != "potion:1"
            || after["reward.potion.origin"] != before["reward.potion.origin"] || after["reward.potion.resolved"] != "false"
            || !after["potion.inventory.origins"].Split(',').SequenceEqual([original[0], original[2]])
            || !SameRandom(index, index + 1) || index + 1 >= plan.Count || plan[index + 1].Action != "base-spire:TakeRewardPotion")
            throw new CheckFailed("Full-belt discard must retain the exact reward and leave other slots in place");
        var taken = Facts(index + 2);
        var expected = new[] { original[0], "1:" + before["reward.potion.origin"], original[2] };
        if (!taken["potion.inventory.origins"].Split(',').SequenceEqual(expected)
            || taken["reward.potion.resolved"] != "true" || !SameRandom(index + 1, index + 2))
            throw new CheckFailed("Refill must acquire the same reward into the free middle slot without shifting or random draws");
    }

    private static void RequireEvent(FixtureCase fixture, IReadOnlyList<NormalSemanticAction> plan)
    {
        if (EventAction(fixture) is { } action && !plan.Any(a => a.Action == action))
            throw new CheckFailed("Required actual event choice did not occur");
        if (fixture.BaseScope == BaseScopes.DevRun && fixture.Script.Single().Choose == "tablet"
            && plan.Count(a => a.Action == "base-spire:TabletOfTruthDecipher") != 5)
            throw new CheckFailed("Required five normal TabletOfTruth pages did not occur");
    }

    private static void RequireCombatClosure(FixtureCase fixture, IReadOnlyList<BoundaryRecord> records)
    {
        if (!records.Last().Facts.Any(f => f.Path == "run.room" && f.Value == fixture.NormalCombatCount.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            throw new CheckFailed("Required number of normal combat closures was not reached");
    }

    private static void RequirePostEventCarryover(FixtureCase fixture, IReadOnlyList<NormalSemanticAction> plan, IReadOnlyList<BoundaryRecord> records)
    {
        if (!fixture.Script.Single().Action.EndsWith("_next_combat", StringComparison.Ordinal)) return;
        var eventIndex = plan.ToList().FindIndex(p => p.Action == EventAction(fixture));
        if (eventIndex < 0) throw new CheckFailed("Required event did not occur before third combat");
        Dictionary<string, string> Facts(int index) => records[index].Facts.ToDictionary(f => f.Path, f => f.Value);
        var afterEvent = Facts(eventIndex + 1);
        var origins = afterEvent["deck.origins.order"].Split(',');
        var cards = afterEvent["deck.order"].Split(',');
        var target = EventAction(fixture)!.EndsWith("MaintainControl", StringComparison.Ordinal) ? origins[0] : origins[^1];
        var third = records.ToList().FindIndex(b => b.Facts.Any(f => f.Path == "run.room" && f.Value == "3")
            && b.Facts.Any(f => f.Path == "run.phase" && f.Value == "combat"));
        if (third < 0) throw new CheckFailed("Post-event third combat was not entered");
        var initial = Facts(third);
        var copies = new[] { "hand", "draw", "discard", "exhaust", "play" }.SelectMany(pile =>
            initial["pile." + pile + ".origins"].Split(',', StringSplitOptions.RemoveEmptyEntries)).ToArray();
        if (!copies.Order(StringComparer.Ordinal).SequenceEqual(origins.Order(StringComparer.Ordinal)))
            throw new CheckFailed("Third combat must contain exactly one copy of every current permanent instance");
        var played = plan.Select((p, i) => (p, i)).Skip(third).Any(step => step.p.Action == "base-spire:PlayCard"
            && Facts(step.i)["pile.hand.origins"].Split(',')[int.Parse(step.p.Token!.Split(':')[^1])] == target);
        if (!played) throw new CheckFailed("Required event-modified permanent card was not actually played in third combat");
        var final = Facts(records.Count - 1);
        var finalOrigins = final["deck.origins.order"].Split(',');
        var finalCards = final["deck.order"].Split(',');
        foreach (var (origin, index) in origins.Select((origin, index) => (origin, index)))
        {
            var position = Array.IndexOf(finalOrigins, origin);
            if (position < 0 || finalCards[position] != cards[index])
                throw new CheckFailed("Combat changed or removed persistent instance " + origin);
        }
    }

    private static void RequireSameActionBoundary(Side side, NormalSemanticAction step)
    {
        var selected = side.Actions().SingleOrDefault(a => a.Action == step.Action);
        if (selected is null || (step.Token is not null && !selected.Candidates.Contains(step.Token, StringComparer.Ordinal)))
            throw new CheckFailed("Common semantic action or candidate is not legal: " + step.Action + "/" + step.Token);
        string Key(ActionOption action) => action.Action + "|" + action.Bindings + "|"
            + string.Join(",", action.Candidates.Order(StringComparer.Ordinal));
        if (!side.Actions().Select(Key).Order(StringComparer.Ordinal)
            .SequenceEqual(step.BeforeActions.Select(Key).Order(StringComparer.Ordinal))
            || !VisibleCandidates(side.Facts()).SequenceEqual(step.BeforeVisible))
            throw new CheckFailed("Common semantic action has different legal candidates or visible instance definitions before " + step.Action);
    }

    private static void Validate(FixtureCase fixture)
    {
        if (fixture.BaseSource != BaseSources.FormalFactory
            || fixture.BaseScope != BaseScopes.DevMap && !(fixture.BaseScope == BaseScopes.DevRun && IsActRun(fixture))
            || string.IsNullOrWhiteSpace(fixture.GameSeed)
            || fixture.Script.Count != 1 || fixture.Script[0].Action is not ("normal_act1" or "normal_reward_take" or "normal_reward_skip" or "normal_reward_take_discard" or "normal_reward_use_first" or "normal_reward_use_cancel" or "normal_aroma_upgrade" or "normal_aroma_transform" or "normal_aroma_upgrade_next_combat" or "normal_aroma_transform_next_combat" or "normal_capacity_skip" or "normal_capacity_discard")
            || fixture.Setup != new FixtureSetup() || fixture.NormalCombatCount is < 1 or > 6
            || (fixture.Script[0].Action.EndsWith("_next_combat", StringComparison.Ordinal) && fixture.NormalCombatCount != 3))
            throw new CheckFailed("normal-reward requires formal dev-map, seed, empty setup and one registered policy action");
    }

    private sealed class BaseSide(Session session, bool cancelOffer = false, bool capacityStrategy = false, bool runLifecycle = false) : Side
    {
        private readonly DeckOrigins<InstanceId> deckOrigins = new();
        private readonly DeckOrigins<InstanceId> potionOrigins = new();
        private InstanceId Run => Members("RunStates").Single();
        private Value Field(InstanceId id, string field) => session.State.GetField(id, session.RuleSet.Type(session.State.TypeIdOf(id)).FieldIndex(field));
        private IReadOnlyList<InstanceId> Members(string name) => session.State.CollectionMembers(session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
        private string Card(InstanceId id) => Field(id, "DefinitionId").AsString() + (Field(id, "Upgraded").AsBool() ? "+" : "");
        public string Phase => runLifecycle && session.Status == SessionStatus.Terminal ? "defeat"
            : Field(Run, "MapPhase").AsInt() switch { 0 => "map", 1 => "combat", 2 => "travel-ready", 3 => "rewards",
                4 when runLifecycle => "rest", 5 when runLifecycle => "treasure", 6 when runLifecycle => "act-completed",
                7 when runLifecycle => "shop", 8 => "event", _ => "unsupported" };
        private string Token(InstanceId id)
        {
            var offers = session.RuleSet.Collections.SingleOrDefault(c => c.QualifiedId == "base-spire:GeneratedCardOffers");
            if (offers is not null && session.State.CollectionMembers(offers.Id).Contains(id)) return "offer:" + Card(id);
            foreach (var (collection, prefix) in new[] { ("MapNodes", "map:"), ("RewardCards", "reward:card:"), ("PotionBelt", "potion:"), ("LongTermDeck", "deck:") })
            {
                var members = Members(collection); var i = members.ToList().IndexOf(id);
                if (i >= 0) return collection == "MapNodes" ? $"map:{Field(id, "Col").AsInt()},{Field(id, "Row").AsInt()}" : prefix + i;
            }
            return EngineSnapshot.SlotMap(session).Single(p => p.Value == id).Key;
        }
        public IReadOnlyList<ActionOption> Actions() => session.LegalActions(SliceManifest.Player)
            .Select(a => new ActionOption(a.Definition.QualifiedId,
                a.Definition.CandidateParameterIndex < 0 ? [] : a.Bindings.Select(b => Token(b[a.Definition.CandidateParameterIndex].AsRef())).Distinct().ToArray(), a.Bindings.Count))
            .OrderBy(a => a.Action, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<FactSnapshot> Facts()
        {
            List<FactSnapshot> facts = [];
            void F(string path, object value) => facts.Add(new(path, Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!.ToLowerInvariant()));
            F("run.phase", Phase); F("run.gold", Field(Run, "Gold").AsInt()); F("run.hp", Field(Run, "Hp").AsInt()); F("run.max_hp", Field(Run, "MaxHp").AsInt());
            F("run.room_type", session.RuleSet.Enums.Single(e => e.QualifiedId == "base-spire:RoomType").NameOf(Field(Run,"ActualRoomType").AsEnumOrdinal()));
            F("relic.inventory.order", string.Join(",", Members("Relics").Select(r => Field(r, "DefinitionId").AsString())));
            if (Phase == "treasure")
            {
                F("treasure.opened", Field(Run, "TreasureOpened").AsBool());
                F("treasure.gold", Field(Run, "TreasureGold").AsInt());
                F("treasure.relic.definition", Field(Run, "TreasureRelicId").AsString());
                F("treasure.relic.rarity", Field(Run, "TreasureRelicRarity").AsString());
            }
            if (Phase == "event") F("run.event", Field(Run, "CurrentEventId").AsString());
            F("run.room", Field(Run, "CurrentRoom").AsInt());
            F("run.map_floor", Field(Run,"MapFloor").AsInt());
            var current = Field(Run,"CurrentNode");
            F("run.map_coord", current.Kind == ValueKind.Ref ? Token(current.AsRef()) : "none");
            var deck = Members("LongTermDeck"); F("deck.order", string.Join(",", deck.Select(Card)));
            F("deck.origins.order", deckOrigins.Capture(deck));
            if (Phase == "event" && Field(Run, "CurrentEventId").AsString().Equals("SlipperyBridge", StringComparison.OrdinalIgnoreCase))
            {
                var preview = Field(Run, "SlipperyBridgeCard").AsRef();
                F("event.slippery.card", Card(preview));
                F("event.slippery.origin", deckOrigins.Of(preview));
                F("event.slippery.hp_loss", 3L + Field(Run, "SlipperyBridgeHoldOns").AsInt());
            }
            F("deck.instance_count", deck.Distinct().Count());
            for (var i = 0; i < deck.Count; i++) F("deck.instance:" + i, Card(deck[i]));
            F("potion.capacity", session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:PotionBelt").Capacity!);
            potionOrigins.Capture(Members("RewardPotions")); potionOrigins.Capture(Members("PotionBelt"));
            F("potion.inventory.origins", string.Join(",", Members("PotionBelt").Select(p => Field(p,"SlotIndex").AsInt() + ":" + potionOrigins.Of(p))));
            F("potion.inventory", string.Join(",", Members("PotionBelt").Select(p => Field(p,"SlotIndex").AsInt() + ":" + Field(p,"DefinitionId").AsString())));
            foreach (var node in Members("MapNodes"))
            {
                F("map." + Token(node) + ".type", Field(node,"Type").AsEnumOrdinal() == 5 ? "Monster" : "type:" + Field(node,"Type").AsEnumOrdinal());
                var relation = session.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren");
                foreach (var child in session.State.RelationTargets(relation.Id,node)) F("map.edge." + Token(node) + "->" + Token(child),true);
            }
            if (Phase == "combat" || Phase == "defeat" && session.RuleSet.Enums.Single(e => e.QualifiedId == "base-spire:RoomType")
                .NameOf(Field(Run, "ActualRoomType").AsEnumOrdinal()) is "Monster" or "Elite" or "Boss")
            {
                facts.AddRange(EngineSnapshot.Facts(session, true));
                foreach (var enemy in Members("Monsters")) F("visible." + Token(enemy) + ".definition", Field(enemy,"DefinitionId").AsString());
                foreach (var card in Members("Hand")) F("visible." + Token(card) + ".definition", Field(card,"DefinitionId").AsString());
                foreach (var card in Members("Hand")) F("visible." + Token(card) + ".cost",
                    Math.Max(0, Field(card, "Cost").AsInt() + Field(card, "CostModifier").AsInt()));
                foreach (var card in Members("Hand")) F("visible." + Token(card) + ".kind",
                    session.RuleSet.Enums.Single(e => e.QualifiedId == "base-spire:CardKind").NameOf(Field(card, "Kind").AsEnumOrdinal()));
                foreach (var (collection, key) in new[] { ("Hand", "hand"), ("DrawPile", "draw"), ("DiscardPile", "discard"), ("ExhaustPile", "exhaust"), ("InPlay", "play") })
                    F("pile." + key + ".origins", string.Join(",", Members(collection).Select(card =>
                    {
                        var source = Field(card, "LongTermSource");
                        return source.Kind == ValueKind.Ref ? deckOrigins.Of(source.AsRef()) : "temporary";
                    })));
            }
            if (Phase == "rewards")
            {
                F("reward.gold", Field(Run,"RewardGold").AsInt());
                F("reward.gold.resolved", Field(Run,"RewardGoldTaken").AsInt() == Field(Run,"CurrentRoom").AsInt());
                F("reward.card.resolved", Field(Run,"RewardTaken").AsInt() == Field(Run,"CurrentRoom").AsInt());
                F("reward.cards.order", string.Join(",", Members("RewardCards").Select(Card)));
                for (var i = 0; i < Members("RewardCards").Count; i++)
                {
                    F("reward.card:"+i+".rarity", Field(Run,"RewardCardRarity"+i).AsInt());
                    F("reward.card:"+i+".upgraded", Field(Members("RewardCards")[i],"Upgraded").AsBool());
                }
                F("reward.potion.present", Field(Run,"RewardPotionDropped").AsBool());
                F("reward.potion.resolved", !Field(Run,"RewardPotionDropped").AsBool()
                    || Field(Run,"RewardPotionResolved").AsInt() == Field(Run,"CurrentRoom").AsInt());
                F("reward.potion.definition", string.Join(",",Members("RewardPotions").Select(p => Field(p,"DefinitionId").AsString())));
                F("reward.potion.origin", potionOrigins.Capture(Members("RewardPotions")));
                F("reward.relic.present", Field(Run,"RewardRelicPresent").AsBool());
                F("reward.relic.resolved", !Field(Run,"RewardRelicPresent").AsBool() || Field(Run,"RewardRelicResolved").AsBool());
                F("reward.relic.definition", Field(Run,"RewardRelicPresent").AsBool() ? Field(Run,"RewardRelicId").AsString() : "");
                F("reward.relic.rarity", Field(Run,"RewardRelicPresent").AsBool() ? Field(Run,"RewardRelicRarity").AsString() : "");
            }
            return Records.Facts(facts);
        }
        public IReadOnlyList<StreamSnapshot> Streams(int index,string kind) => EngineSnapshot.Streams(session);
        public DiagnosticSnapshot Diagnostic() => EngineSnapshot.Capture(session);
        public IReadOnlyList<DecisionSnapshot> Execute(string action, string? token, IReadOnlyList<DecisionSnapshot>? expectedDecisions = null)
        {
            var legal = session.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == action);
            var binding = token is null ? legal.Bindings.Single() : legal.Bindings.First(b => Token(b[legal.Definition.CandidateParameterIndex].AsRef()) == token);
            try { session.Submit(legal.Definition, SliceManifest.Player, binding); }
            catch (EngineFaultException fault) when (action == "base-spire:ChooseMapPoint"
                && fault.Message.Contains("selected event has no registered executable module", StringComparison.Ordinal))
            {
                var selected = Members("RunVisitedEvents").Last();
                throw new CheckFailed(fault.Message + "; actual selected source event=" + Field(selected, "SourceId").AsString());
            }
            List<DecisionSnapshot> decisions = [];
            while (session.Status == SessionStatus.AwaitingDecision)
            {
                var d = session.PendingDecision!;
                var selected = d.Candidates.First(); // Registered target policy: first local legal target, never hidden state.
                if (capacityStrategy)
                {
                    var monster = session.RuleSet.TypeByQualifiedId("base-spire:Monster").Id;
                    var card = session.RuleSet.TypeByQualifiedId("base-spire:Card").Id;
                    if (d.Candidates.All(c => session.State.TypeIdOf(c) == monster))
                        selected = d.Candidates.OrderBy(c => Field(c, "Hp").AsInt()).First();
                    else if (d.Candidates.All(c => session.State.TypeIdOf(c) == card))
                        selected = d.Candidates.FirstOrDefault(c => Field(c, "DefinitionId").AsString() ==
                            (action == "base-spire:AromaOfChaosMaintainControl" ? "base-spire:bash" : "base-spire:flame-barrier"), selected);
                }
                bool cancel = cancelOffer && d.Minimum == 0;
                decisions.Add(new(d.Kind.ToString(), d.Prompt, d.Candidates.Select(Token).ToArray(), d.Minimum, d.Maximum, cancel ? "cancel" : Token(selected)));
                session.Answer(new DecisionAnswer(d.RequestId,cancel ? [] : [selected]));
            }
            if (session.Status == SessionStatus.Faulted) throw new CheckFailed(session.Fault?.Message ?? "base fault");
            return decisions;
        }
    }

    /// <summary>Independent creation-order correspondence, retained across removal; never rematches by card definition.</summary>
    private sealed class DeckOrigins<T> where T : notnull
    {
        private readonly Dictionary<T, int> origins = new();
        public string Of(T card) => origins.TryGetValue(card, out var origin) ? "origin:" + origin
            : throw new CheckFailed("Combat copy refers to an unregistered permanent card origin");
        public string Capture(IEnumerable<T> cards) => string.Join(",", cards.Select(card =>
        {
            if (!origins.TryGetValue(card, out var origin)) origins.Add(card, origin = origins.Count);
            return "origin:" + origin;
        }));
    }
}
