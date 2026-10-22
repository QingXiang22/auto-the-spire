using System.Reflection;
using System.Text;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Host;

namespace Differential;

internal sealed class NormalDslRun : IDisposable
{
    private readonly ControlHost host;
    private readonly StreamWriter log;
    private readonly string logPath;
    private readonly Dictionary<InstanceId, string> slots = [];
    private readonly Dictionary<InstanceId, string> identities = [];
    private readonly Dictionary<InstanceId, string> generated = [];
    private readonly Dictionary<InstanceId, string> potionOrigins = [];
    private readonly Dictionary<InstanceId, string> rewardCardOrigins = [];
    private readonly HashSet<int> generatedActions = [];
    private NormalSettlement[]? settlement;
    private int actionIndex = -1;
    private int number;
    private RequestDto? lastStep;
    private ResponseDto? lastStepResponse;
    private string sessionId = "";
    public PublicSnapshotDto Snapshot { get; private set; } = null!;
    private Session Engine => host.GetSessionForTests(sessionId).EngineForTests;
    public string Phase => Field(Instances("Run").Single(), "Run", "Phase").AsString();
    private IReadOnlyDictionary<InstanceId, string> Refs
    {
        get
        {
            // Read the actual Host projector's binding table. Never derive handles from creation IDs
            // or construct a second projection with a different allocation history.
            var session = host.GetSessionForTests(sessionId);
            var projector = typeof(ControlSession).GetField("_projector", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
            return (IReadOnlyDictionary<InstanceId, string>)projector.GetType().GetField("_publicRefs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(projector)!;
        }
    }
    public NormalDslRun(string repository, string path, bool debug = true, bool captureValues = true)
    {
        var factory = typeof(TextRuleProviderSpec).Assembly.GetType("NetTheSpire.Host.TextRuleScenario")!;
        var setup = factory.GetMethod("CreatePackage")!.Invoke(null,
            [Path.Combine(repository, "mod-the-spire/base-spire/package.json"), null, 4096, null,
                debug ? new SessionDebugOptions(MaxRecords: 200_000, MaxTextLength: 8192, MaxCandidates: 4096, CaptureValues: captureValues) : null])!;
        var configured = (ControlHost)setup.GetType().GetProperty("Host")!.GetValue(setup)!;
        // A fixed namespace is scoped to each isolated tool host. It makes on/off diagnostic
        // snapshots byte-comparable; protocol handles still never cross live sessions.
        host = new ControlHost(configured.SourceForTests, runToken: "normal-compare");
        logPath = path;
        log = new StreamWriter(path, false, new UTF8Encoding(false));
    }
    internal string RandomState => NormalPreflight.Json(Engine.Random.Capture());
    internal string PublicState => WireCanonical.Text(Snapshot.ToWire());
    internal bool HasCompleteDebug => Engine.Debug is { DroppedRecords: 0 };
    internal IReadOnlyList<SessionDebugRecord> DebugRecords => Engine.Debug?.Capture() ?? [];
    internal bool IsLive(InstanceId instance) => Engine.State.Exists(instance);
    internal string StateHash => Engine.State.StateHash();
    internal bool HasDebug => Engine.Debug is not null;
    private RequestDto Request(Operation op, params (string, WireValue)[] fields) =>
        new("normal-" + ++number, op, new WireObject(fields.Select(p => new KeyValuePair<string, WireValue>(p.Item1, p.Item2))));
    private ResponseDto Exchange(RequestDto request)
    {
        log.WriteLine(WireCanonical.Text(request.ToWire()));
        // Exercise the protocol codec as well as the real production dispatcher.
        var response = host.Dispatch(RequestDto.FromWire(WireParser.ParseUtf8Strict(Encoding.UTF8.GetBytes(WireCanonical.Text(request.ToWire())), "normal-compare")));
        log.WriteLine(WireCanonical.Text(response.ToWire())); log.Flush();
        return response;
    }
    private ResponseDto Send(Operation op, params (string, WireValue)[] fields)
    {
        var response = Exchange(Request(op, fields));
        if (!response.Ok) throw new InvalidOperationException("Public Host: " + response.Error?.Message);
        return response;
    }
    private static WireValue Get(ResponseDto r, string f) => ((WireObject)r.Result!).Get(f)!;
    public void Start(string seed)
    {
        var describe = DescribeResultDto.FromWire(Send(Operation.Describe).Result!);
        var r = Send(Operation.Start, ("seed", WireValue.Str(seed)), ("expected_identity", describe.Identity.ToWire()),
            ("scenario_profile", WireValue.Str(describe.Capabilities.ScenarioProfiles.Single().Id)));
        sessionId = ((WireString)Get(r, "session_id")).Value;
        Snapshot = PublicSnapshotDto.FromWire(Get(r, "snapshot"), NetProtocol.V02);
    }
    public LegalActionDto? Find(string name) => Snapshot.LegalActions.FirstOrDefault(a => a.ActionType == "net:action:base-spire:" + name);
    public LegalActionDto Action(string name) => Find(name) ?? throw new InvalidOperationException("Missing public candidate: " + name);
    public void Step(LegalActionDto action)
    {
        lastStep = Request(Operation.Step, ("session_id", WireValue.Str(sessionId)), ("expected_observation_id", WireValue.Integer(Snapshot.ObservationId)), ("candidate_id", WireValue.Str(action.CandidateId)));
        lastStepResponse = Exchange(lastStep);
        if (!lastStepResponse.Ok) throw new InvalidOperationException("Public Host: " + lastStepResponse.Error?.Message);
        Snapshot = PublicSnapshotDto.FromWire(Get(Send(Operation.Observe, ("session_id", WireValue.Str(sessionId))), "snapshot"), NetProtocol.V02);
        Remember();
    }
    internal NormalProtocolCheck CheckRequestReplay()
    {
        if (lastStep is null || lastStepResponse is null) throw new InvalidOperationException("No real step to replay.");
        var before = Signature();
        var replay = Exchange(lastStep);
        if (WireCanonical.Text(replay.ToWire()) != WireCanonical.Text(lastStepResponse.ToWire()))
            throw new InvalidOperationException("The same public request did not return its cached result.");
        Unchanged();
        var stale = new RequestDto("normal-" + ++number, lastStep.Operation, lastStep.Payload);
        var rejected = Exchange(stale);
        if (rejected.Ok || rejected.Error?.Code != "stale_observation")
            throw new InvalidOperationException("A fresh request with the previous observation was not rejected as stale.");
        Unchanged();
        if (WireCanonical.Text(Exchange(stale).ToWire()) != WireCanonical.Text(rejected.ToWire()))
            throw new InvalidOperationException("A rejected request did not retain its cached rejection.");
        Unchanged();
        return new(true, true, true, true, "dsl:public-host");
        string Signature() => Engine.State.StateHash() + RandomState + NormalPreflight.Json(Engine.PendingDecision)
            + Engine.StateVersion + Engine.Status + PublicState;
        void Unchanged()
        {
            Snapshot = PublicSnapshotDto.FromWire(Get(Send(Operation.Observe, ("session_id", WireValue.Str(sessionId))), "snapshot"), NetProtocol.V02);
            if (Signature() != before) throw new InvalidOperationException("Duplicate/stale request changed gameplay, RNG, pending decision or public observation.");
        }
    }
    private InstanceId[] Instances(string type) => Engine.State.AllInstances(Engine.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
    private InstanceId[] Members(string name) => Engine.State.CollectionMembers(Engine.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id).ToArray();
    private Value Field(InstanceId id, string type, string name) => Engine.State.GetField(id, Engine.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(name));
    private void Remember()
    {
        var cardOffers = Members("CardRewardOptions");
        for (var i = 0; i < cardOffers.Length; i++)
            if (!rewardCardOrigins.ContainsKey(cardOffers[i])) rewardCardOrigins.Add(cardOffers[i], $"generated:action:{actionIndex}:card-reward:{i}");
        foreach (var card in Members("RunDeck")) if (!slots.ContainsKey(card)) slots.Add(card, rewardCardOrigins.GetValueOrDefault(card) ?? "permanent:" + slots.Count.ToString("D2"));
        var combatCards = new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay", "RemovedFromCombat" }.SelectMany(Members).Distinct().ToArray();
        var fresh = combatCards.Where(c => !slots.ContainsKey(c) && Field(c, "Card", "PermanentSource").IsNone && !generated.ContainsKey(c)).ToArray();
        if (fresh.Length > 1 || (fresh.Length == 1 && !generatedActions.Add(actionIndex)))
            throw new NotSupportedException("missing_dimension: multiple generated cards in one common action require native creation-origin observation.");
        if (fresh.Length == 1) generated.Add(fresh[0], "generated:action:" + actionIndex);
        foreach (var card in Members("RunDeck").Concat(combatCards).Concat(cardOffers).Distinct())
        {
            var source = Field(card, "Card", "PermanentSource");
            var slot = slots.GetValueOrDefault(card) ?? (!source.IsNone ? slots[source.AsRef()] : rewardCardOrigins.GetValueOrDefault(card) ?? generated[card]);
            identities[card] = slot + ":" + Field(card, "Card", "DefinitionId").AsString() + (Field(card, "Card", "Upgraded").AsBool() ? "+" : "");
        }
        identities[Instances("Player").Single()] = "player";
        var offers = Members("PotionRewardOptions");
        for (var i = 0; i < offers.Length; i++)
            if (!potionOrigins.ContainsKey(offers[i])) potionOrigins.Add(offers[i], $"generated:action:{actionIndex}:potion-reward:{i}");
        foreach (var potion in offers.Concat(Members("Potions")))
            identities[potion] = potionOrigins.TryGetValue(potion, out var origin)
                ? origin + ":" + Field(potion, "Potion", "DefinitionId").AsString()
                : throw new NotSupportedException("missing_dimension: potion acquisition origin");
        var enemies = Instances("Enemy");
        for (var i = 0; i < enemies.Length; i++) identities[enemies[i]] = "enemy:" + i;
        foreach (var node in Instances("MapNode")) identities[node] = Field(node, "MapNode", "Col").AsInt() + "," + Field(node, "MapNode", "Row").AsInt();
        foreach (var option in Instances("NeowOption")) identities[option] = Field(option, "NeowOption", "RelicId").AsString();
    }
    private string Binding(WireValue value)
    {
        if (value is WireString s)
        {
            var id = Refs.SingleOrDefault(p => p.Value == s.Value).Key;
            return id.IsNone ? s.Value : identities.TryGetValue(id, out var identity) ? identity : throw new NotSupportedException("Unmapped public entity binding.");
        }
        return WireCanonical.Text(value);
    }
    public NormalAction Semantic(LegalActionDto action)
    {
        if (action.ActionType == "net:action:net.answer_decision" && Phase == "combat")
            return new("choose-target", [Binding(action.Fields.Single().Value!)]);
        if (action.ActionType == "net:action:net.answer_decision" && Phase == "neow")
            return action.Fields.Single().Value is WireBool ? new("leave-neow", []) : new("neow", [Binding(action.Fields.Single().Value!)]);
        var name = action.ActionType.Split(':')[^1];
        var kind = name switch
        {
            "PlayAttackFromHand" or "PlayCardFromHand" => "play", "PlayTargetedCard" => "targeted-play", "EndDevelopmentTurn" => "end-turn",
            "ChooseMapPoint" => "map", "ClaimDevelopmentCardReward" => "claim-card",
            "DiscardPotion" => "discard-potion",
            "ClaimDevelopmentPotionReward" => "claim-potion", "SkipDevelopmentPotionReward" => "skip-potion",
            "ClaimDevelopmentGoldReward" => "claim-gold", "SkipDevelopmentCardReward" => "skip-card",
            "CloseDevelopmentRewards" => "close-rewards", _ => throw new NotSupportedException("Unmapped normal action: " + name)
        };
        var arguments = kind == "claim-card" ? action.Fields.Select(f => Snapshot.Entities.Single(e => e.InstanceId == ((WireString)f.Value!).Value).ContentId!).ToArray()
            : action.Fields.Select(f => Binding(f.Value!)).ToArray();
        if (name == "PlayAttackFromHand") arguments = [.. arguments, "first-legal-target"];
        return new(kind, arguments);
    }
    public void Apply(NormalAction action)
    {
        actionIndex++;
        LegalActionDto candidate;
        if (action.Kind == "neow") candidate = Snapshot.LegalActions.Single(a => a.Fields.Any(f => Binding(f.Value!) == action.Arguments[0]));
        else if (action.Kind == "leave-neow") candidate = Snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireBool { Value: true }));
        else candidate = Snapshot.LegalActions.Where(a => !a.ActionType.EndsWith(":PlayCardFromHand", StringComparison.Ordinal)
                || !Snapshot.LegalActions.Any(c => c.ActionType.EndsWith(":PlayAttackFromHand", StringComparison.Ordinal) && c.Fields.SequenceEqual(a.Fields)))
            .First(a => TrySemantic(a)?.Key == action.Key);
        Step(candidate);
        // The source driver performs player cleanup, enemies and the next player start in one turn
        // operation. Consume the DSL's explicit continuation before comparing that common boundary.
        if (action.Kind == "end-turn" && Find("BeginDevelopmentTurn") is { } begin) Step(begin);
    }
    private NormalAction? TrySemantic(LegalActionDto a) { try { return Semantic(a); } catch (NotSupportedException) { return null; } }
    public NormalBoundary Capture(string kind)
    {
        Remember(); var v = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Put(string key, object? value) => v.Add(key, NormalPreflight.Json(value));
        var player = Instances("Player").Single();
        foreach (var (key, field) in new[] { ("hp", "Hp"), ("max_hp", "MaxHp"), ("gold", "Gold") })
            Put("facts.player." + key, Field(player, "Player", field).AsInt());
        var run = Instances("Run").Single();
        Put("visits.phase", Phase); Put("visits.act", Field(run, "Run", "ActIndex").AsInt());
        Put("visits.total_floor", Field(run, "Run", "TotalFloor").AsInt());
        Put("visits.coordinates", Instances("MapNode").Where(n => Field(n, "MapNode", "Visited").AsBool()).Select(n => identities[n]).Order(StringComparer.Ordinal).ToArray());
        Put("permanent.deck", Members("RunDeck").Select(c => identities[c]).ToArray());
        Put("permanent.relics", Instances("Relic").Select(c => Field(c, "Relic", "DefinitionId").AsString()).ToArray());
        Put("permanent.potions", Members("Potions").Select(c => Field(c, "Potion", "DefinitionId").AsString()).ToArray());
        foreach (var pile in new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay", "RemovedFromCombat" })
            Put("piles." + pile, Members(pile).Select(c => identities[c]).ToArray());
        if (Phase == "combat")
        {
            foreach (var (key, field) in new[] { ("block", "Block"), ("energy", "Energy"), ("turn", "TurnNumber") })
                Put("facts.player." + key, Field(player, "Player", field).AsInt());
            var enemies = Instances("Enemy");
            Put("facts.enemies", enemies.Select(e => new { id = Field(e, "Enemy", "DefinitionId").AsString(),
                hp = Field(e, "Enemy", "Hp").AsInt(), max_hp = Field(e, "Enemy", "MaxHp").AsInt(), block = Field(e, "Enemy", "Block").AsInt(),
                move = Field(e, "Enemy", "MoveId").AsString() }).ToArray());
            Put("facts.powers", Instances("Power").Select(p => new { id = Field(p, "Power", "DefinitionId").AsString(),
                holder = Field(p, "Power", "HolderEnemy").IsNone ? "player" : identities[Field(p, "Power", "HolderEnemy").AsRef()],
                amount = Field(p, "Power", "Stacks").AsInt() }).OrderBy(p => p.holder).ThenBy(p => p.id).ToArray());
        }
        var actions = Snapshot.LegalActions.Select(TrySemantic).Where(a => a is not null).Select(a => a!.Key).Distinct().Order(StringComparer.Ordinal).ToArray();
        var unmapped = Snapshot.LegalActions.Where(a => TrySemantic(a) is null).ToArray();
        Put("actions.candidates", actions);
        var decision = Engine.PendingDecision;
        Put("decisions.current", decision is null ? null : new { min = decision.Minimum, max = decision.Maximum,
            candidates = Phase == "combat" ? decision.Candidates.Select(c => identities[c]).ToArray()
                : Snapshot.LegalActions.Select(a => Binding(a.Fields.Single().Value!)).Order(StringComparer.Ordinal).ToArray() });
        Put("public.piles", new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay", "RemovedFromCombat", "RunDeck" }.Select(name =>
        {
            var pile = Snapshot.Piles.Single(p => Snapshot.Entities.Single(e => e.InstanceId == p.PileRef).KindId == "net:kind:base-spire:" + name + "View");
            return new { name, total = pile.TotalCount, complete = pile.MembersComplete,
                members = pile.VisibleMembers.Select(id => Binding(WireValue.Str(id))).Order(StringComparer.Ordinal).ToArray(),
                positions = pile.KnownPositions.Select(p => new { card = Binding(WireValue.Str(p.CardRef)), p.Position }).ToArray() };
        }).ToArray());
        NormalPublicObservation.Add(Snapshot, v, Binding);
        // Public evidence remains the actual protocol snapshot, separate from tool-only hidden piles.
        var trace = Engine.Debug;
        if (trace is not null) Put("events.observed_order", ObservedEvents());
        if (trace is not null) Put("lifecycle.settlement", Settlement());
        if (trace is not null) Put("random.float32_calls", NormalFloat32Trace.Dsl(trace.Capture(), trace.DroppedRecords));
        if (trace is not null) Put("random.stream_states", Engine.Random.Capture().Streams.OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => new { stream = s.Name, draws = s.Draws, words = new[] { s.S0, s.S1, s.S2, s.S3 }.Select(w => w.ToString("x16")).ToArray() }).ToArray());
        string[] missing = ["public: complete cross-backend projection", "random: full initialization candidates/normalization", "events: cross-backend event projection"];
        if (unmapped.Length > 0) missing = [.. missing, $"actions: {unmapped.Length} public candidates have no normal reference mapping"];
        return new(kind, v, missing,
            new { backend = "dsl:public-host", public_snapshot = WireCanonical.Text(Snapshot.ToWire()),
                unmapped_public_actions = unmapped,
                unmapped_public_fields = NormalPublicObservation.UnmappedFields(Snapshot),
                debug = trace?.Capture().Where(r => r.Kind is SessionDebugKind.RandomCall or SessionDebugKind.EventEmit or SessionDebugKind.ListenerEnter
                    or SessionDebugKind.DecisionRequested or SessionDebugKind.DecisionAnswered or SessionDebugKind.Fault).Select(r => r with { Snapshot = null }).ToArray(), dropped = trace?.DroppedRecords,
                streams = Engine.Random.Capture(), instance_mapping = identities.Select(p => new { instance = p.Key.Value, identity = p.Value }).ToArray() });
    }
    private NormalSettlement[] Settlement()
    {
        if (settlement is not null) return settlement;
        var records = DebugRecords;
        var ended = records.Where(r => r.Kind == SessionDebugKind.EventEmit && r.Subject == "base-spire:CombatEnded").ToArray();
        if (ended.Length == 0) return [];
        if (ended.Length != 1 || ended[0].Snapshot is not { Complete: true } snapshot || ended[0].TextTruncated
            || snapshot.Values[1].Scalar != "true")
            throw new NotSupportedException("missing_dimension: normal victory settlement snapshot");
        var owner = snapshot.Entities.Single(e => e.Type == "base-spire:Player");
        SessionDebugValueSnapshot F(string name) => owner.Fields.Single(f => f.Name == name).Value;
        var finished = records.Last(r => r.Kind == SessionDebugKind.EventEmit && r.Subject == "base-spire:AfterCardPlayed");
        var kill = records.Last(r => r.Kind == SessionDebugKind.EventEmit && r.Subject == "base-spire:DamageResolved");
        if (kill.Snapshot is not { Complete: true } damage || !damage.Entities.Single().Fields.Any(f => f.Name == "Killed" && f.Value.Scalar == "true")
            || !(kill.Sequence < ended[0].Sequence && finished.Sequence < ended[0].Sequence)
            || new[] { "AttackDepth", "DamageDepth", "EffectDepth", "PlayDepth" }.Any(n => F(n).Scalar != "0")
            || F("CurrentRewardSet").Kind != ValueKind.None || F("GoldRewardPending").Scalar != "false" || F("CardRewardPending").Scalar != "false")
            throw new NotSupportedException("missing_dimension: final card effects must finish before victory settlement and rewards");
        var player = Instances("Player").Single();
        var rewards = Field(player, "Player", "CurrentRewardSet");
        if (Phase != "rewards" || rewards.IsNone || !Field(rewards.AsRef(), "CombatRewardSet", "Generated").AsBool())
            throw new NotSupportedException("missing_dimension: first settled victory boundary must publish generated rewards");
        settlement = [new("effects-finished", int.Parse(F("Hp").Scalar!), int.Parse(F("MaxHp").Scalar!), int.Parse(F("Gold").Scalar!), false),
            new("rewards-published", Field(player, "Player", "Hp").AsInt(), Field(player, "Player", "MaxHp").AsInt(), Field(player, "Player", "Gold").AsInt(), true)];
        return settlement;
    }
    private NormalObservedEvent[] ObservedEvents()
    {
        if (!HasCompleteDebug) throw new NotSupportedException("missing_dimension: dropped event diagnostics");
        return DebugRecords.Where(r => r.Kind == SessionDebugKind.EventEmit && r.Subject is
            "base-spire:AfterCardDrawn" or "base-spire:BeforeCardPlayed" or "base-spire:AfterCardPlayed" or "base-spire:DamageResolved"
            or "base-spire:PowerApplied" or "base-spire:EnemyTurnStarted" or "base-spire:AfterPlayerTurnStartLate")
            .Select(r =>
            {
                if (r.TextTruncated || r.Snapshot is not { Complete: true }) throw new NotSupportedException("missing_dimension: event snapshot");
                var snapshot = r.Snapshot;
                if (r.Subject == "base-spire:AfterPlayerTurnStartLate") return new NormalObservedEvent("player-turn-started");
                if (r.Subject == "base-spire:EnemyTurnStarted")
                {
                    var enemy = snapshot.Entities.Single(e => e.Type == "base-spire:Enemy");
                    return new NormalObservedEvent("enemy-move-started", Actor: identities[enemy.Instance],
                        Move: enemy.Fields.Single(f => f.Name == "MoveId").Value.Scalar!);
                }
                if (r.Subject == "base-spire:PowerApplied")
                {
                    var power = snapshot.Entities.Single(e => e.Type == "base-spire:Power");
                    SessionDebugValueSnapshot F(string name) => power.Fields.Single(f => f.Name == name).Value;
                    var holder = new[] { F("HolderPlayer"), F("HolderEnemy") }.Single(v => v.Kind == ValueKind.Ref);
                    return new NormalObservedEvent("power-amount-changed", Power: new(F("DefinitionId").Scalar!,
                        identities[new InstanceId(int.Parse(holder.Scalar!))], F("Stacks").Scalar!, snapshot.Values[1].Scalar!));
                }
                if (r.Subject == "base-spire:DamageResolved")
                {
                    var entity = snapshot.Entities.Single(e => e.Type == "base-spire:DamageResult");
                    SessionDebugValueSnapshot F(string name) => entity.Fields.Single(f => f.Name == name).Value;
                    string? Ref(string name) => F(name) is { Kind: ValueKind.Ref, Scalar: not "0" } value
                        ? identities[new InstanceId(int.Parse(value.Scalar!))] : null;
                    string Number(string name) => F(name) is { Kind: ValueKind.Int, Scalar: {} value } ? value
                        : throw new NotSupportedException("missing_dimension: typed damage amount");
                    var killed = F("Killed");
                    if (killed.Kind != ValueKind.Bool) throw new NotSupportedException("missing_dimension: typed kill flag");
                    return new NormalObservedEvent("damage", Damage: new(Ref("DealerPlayer") ?? Ref("DealerEnemy"),
                        Ref("Player") ?? Ref("Enemy") ?? throw new NotSupportedException("missing_dimension: damage receiver"),
                        Number("Blocked"), Number("Unblocked"), Number("Overkill"), killed.Scalar == "true"));
                }
                var card = snapshot.Entities.Single(e => e.Type == "base-spire:Card");
                var identity = identities[card.Instance];
                var origin = identity[..identity.IndexOf(":base-spire:", StringComparison.Ordinal)];
                var content = card.Fields.Single(f => f.Name == "DefinitionId").Value.Scalar!;
                var upgraded = card.Fields.Single(f => f.Name == "Upgraded").Value.Scalar == "true";
                return new NormalObservedEvent(r.Subject switch { "base-spire:AfterCardDrawn" => "draw",
                    "base-spire:BeforeCardPlayed" => "before-play", _ => "after-play" }, origin + ":" + content + (upgraded ? "+" : ""));
            }).ToArray();
    }
    public void Dispose()
    {
        log.Dispose();
        if (sessionId.Length > 0 && HasDebug)
            File.WriteAllText(logPath + ".trace.json", NormalPreflight.Json(DebugRecords.Where(r => r.Snapshot is not null).ToArray()));
    }
}
