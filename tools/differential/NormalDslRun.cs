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
    private readonly Dictionary<InstanceId, string> slots = [];
    private readonly Dictionary<InstanceId, string> identities = [];
    private readonly Dictionary<InstanceId, string> generated = [];
    private readonly HashSet<int> generatedActions = [];
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
    public NormalDslRun(string repository, string path, bool debug = true)
    {
        var factory = typeof(TextRuleProviderSpec).Assembly.GetType("NetTheSpire.Host.TextRuleScenario")!;
        var setup = factory.GetMethod("CreatePackage")!.Invoke(null,
            [Path.Combine(repository, "mod-the-spire/base-spire/package.json"), null, 4096, null,
                debug ? new SessionDebugOptions(MaxRecords: 200_000, MaxTextLength: 8192, MaxCandidates: 4096) : null])!;
        var configured = (ControlHost)setup.GetType().GetProperty("Host")!.GetValue(setup)!;
        // A fixed namespace is scoped to each isolated tool host. It makes on/off diagnostic
        // snapshots byte-comparable; protocol handles still never cross live sessions.
        host = new ControlHost(configured.SourceForTests, runToken: "normal-compare");
        log = new StreamWriter(path, false, new UTF8Encoding(false));
    }
    internal string RandomState => NormalPreflight.Json(Engine.Random.Capture());
    internal string PublicState => WireCanonical.Text(Snapshot.ToWire());
    internal bool HasCompleteDebug => Engine.Debug is { DroppedRecords: 0 };
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
        foreach (var card in Members("RunDeck")) if (!slots.ContainsKey(card)) slots.Add(card, "permanent:" + slots.Count.ToString("D2"));
        var combatCards = new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay", "RemovedFromCombat" }.SelectMany(Members).Distinct().ToArray();
        var fresh = combatCards.Where(c => !slots.ContainsKey(c) && Field(c, "Card", "PermanentSource").IsNone && !generated.ContainsKey(c)).ToArray();
        if (fresh.Length > 1 || (fresh.Length == 1 && !generatedActions.Add(actionIndex)))
            throw new NotSupportedException("missing_dimension: multiple generated cards in one common action require native creation-origin observation.");
        if (fresh.Length == 1) generated.Add(fresh[0], "generated:action:" + actionIndex);
        foreach (var card in Members("RunDeck").Concat(combatCards).Distinct())
        {
            var source = Field(card, "Card", "PermanentSource");
            var slot = slots.GetValueOrDefault(card) ?? (!source.IsNone ? slots[source.AsRef()] : generated[card]);
            identities[card] = slot + ":" + Field(card, "Card", "DefinitionId").AsString() + (Field(card, "Card", "Upgraded").AsBool() ? "+" : "");
        }
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
        // Public evidence remains the actual protocol snapshot, separate from tool-only hidden piles.
        var trace = Engine.Debug;
        if (trace is not null) Put("random.stream_states", Engine.Random.Capture().Streams.OrderBy(s => s.Name, StringComparer.Ordinal)
            .Select(s => new { stream = s.Name, draws = s.Draws, words = new[] { s.S0, s.S1, s.S2, s.S3 }.Select(w => w.ToString("x16")).ToArray() }).ToArray());
        return new(kind, v, ["public: complete cross-backend projection", "random: full initialization candidates/normalization", "events: cross-backend event projection",
                "lifecycle: final-kill effects"],
            new { backend = "dsl:public-host", public_snapshot = WireCanonical.Text(Snapshot.ToWire()),
                debug = trace?.Capture().Where(r => r.Kind is SessionDebugKind.RandomCall or SessionDebugKind.EventEmit or SessionDebugKind.ListenerEnter
                    or SessionDebugKind.DecisionRequested or SessionDebugKind.DecisionAnswered or SessionDebugKind.Fault).ToArray(), dropped = trace?.DroppedRecords,
                streams = Engine.Random.Capture(), instance_mapping = identities.Select(p => new { instance = p.Key.Value, identity = p.Value }).ToArray() });
    }
    public void Dispose() => log.Dispose();
}
