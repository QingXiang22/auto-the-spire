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
        IReadOnlyList<DecisionSnapshot> Execute(string action, string? token);
        DiagnosticSnapshot? Diagnostic();
    }

    // Versioned visible-candidate policy, evaluated independently on each side. No hidden draw order.
    private static void Drive(Side side, List<BoundaryRecord> records, bool skip)
    {
        void Add(string kind, IReadOnlyList<DecisionSnapshot> decisions) => records.Add(new(
            records.Count, kind, kind, side.Actions(), decisions, side.Facts(),
            side.Streams(records.Count, kind), side.Diagnostic()));
        Add("start", []);
        for (var i = 0; i < 200; i++)
        {
            var phase = side.Phase;
            if (phase == "travel-ready") return;
            var actions = side.Actions();
            string action; string? token = null;
            if (phase == "map")
            {
                action = "base-spire:ChooseMapPoint";
                var options = actions.Single(a => a.Action == action).Candidates;
                var facts = side.Facts().ToDictionary(f => f.Path, f => f.Value);
                token = options.Order(StringComparer.Ordinal).FirstOrDefault(t => facts.GetValueOrDefault("map." + t + ".type") == "monster")
                    ?? throw new CheckFailed("Normal prefix has no legal first Monster map point");
            }
            else if (phase == "rewards")
            {
                if (actions.Any(a => a.Action == "base-spire:TakeRewardCard"))
                {
                    action = skip ? "base-spire:SkipRewardCard" : "base-spire:TakeRewardCard";
                    if (!skip) token = "reward:card:0";
                }
                else if (actions.Any(a => a.Action == "base-spire:SkipRewardPotion"))
                    action = skip ? "base-spire:SkipRewardPotion" : "base-spire:TakeRewardPotion";
                else action = "base-spire:TakeRewardGold";
            }
            else if (phase == "combat")
            {
                var play = actions.FirstOrDefault(a => a.Action == "base-spire:PlayCard");
                var facts = side.Facts().ToDictionary(f => f.Path, f => f.Value);
                token = play?.Candidates.OrderBy(t => facts.GetValueOrDefault("visible." + t + ".definition") switch
                { "base-spire:bash" => 0, "base-spire:strike-ironclad" => 1, "base-spire:defend-ironclad" => 2, _ => 3 })
                    .ThenBy(t => t, StringComparer.Ordinal).FirstOrDefault();
                action = token is null ? "base-spire:EndTurn" : "base-spire:PlayCard";
            }
            else throw new CheckFailed("Cannot continue normal prefix phase=" + phase);
            var decisions = side.Execute(action, token);
            Add("step:" + i + ":" + action + (token is null ? "" : ":" + token), decisions);
        }
        throw new CheckFailed("Normal prefix exceeded 200 visible actions (not a defeat)");
    }

    public static CaseRecord RunBase(FixtureCase fixture)
    {
        List<BoundaryRecord> records = [];
        BaseSpireRunSession? run = null; long before = 0;
        string status = "ok"; string? detail = null;
        try
        {
            Validate(fixture);
            run = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.DevMap)).CreateSession(fixture.GameSeed!);
            run.Start(); before = run.Session.Random.TotalDraws;
            Drive(new BaseSide(run.Session), records, fixture.Script.Single().Action == "normal_reward_skip");
            if (!records.Last().Facts.Any(f => f.Path == "run.phase" && f.Value == "travel-ready"))
                throw new CheckFailed("First victory/reward closure not reached");
        }
        catch (Exception e) { status = "faulted"; detail = e.GetType().Name + ": " + e.Message; }
        return new(fixture.Id, BaseBackend.FormalName, "normal-reward-prefix@1", fixture.RngProfile, status, detail,
            run is not null && run.Session.Random.TotalDraws != before,
            ["Normal dev-map factory; Start once; no initial-state writes. Policy visible-bash-strike-defend@1; first legal Monster coordinate; rewards card then potion then gold.",
             "Stop at reward closure/travel-ready; full legal successor candidates retained. No next combat claimed."], records)
        { BaseSource = fixture.BaseSource, BuildId = run?.Build.PublicBuildId, SessionStatus = run?.Session.Status.ToString(),
          SessionIdentity = run?.Session.Identity, InitializationRandomCount = before };
    }

    private static void Validate(FixtureCase fixture)
    {
        if (fixture.BaseSource != BaseSources.FormalFactory || fixture.BaseScope != BaseScopes.DevMap || string.IsNullOrWhiteSpace(fixture.GameSeed)
            || fixture.Script.Count != 1 || fixture.Script[0].Action is not ("normal_reward_take" or "normal_reward_skip")
            || fixture.Setup != new FixtureSetup())
            throw new CheckFailed("normal-reward requires formal dev-map, seed, empty setup and one registered policy action");
    }

    private sealed class BaseSide(Session session) : Side
    {
        private InstanceId Run => Members("RunStates").Single();
        private Value Field(InstanceId id, string field) => session.State.GetField(id, session.RuleSet.Type(session.State.TypeIdOf(id)).FieldIndex(field));
        private IReadOnlyList<InstanceId> Members(string name) => session.State.CollectionMembers(session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
        private string Card(InstanceId id) => Field(id, "DefinitionId").AsString() + (Field(id, "Upgraded").AsBool() ? "+" : "");
        public string Phase => Field(Run, "MapPhase").AsInt() switch { 0 => "map", 1 => "combat", 2 => "travel-ready", 3 => "rewards", _ => "unsupported" };
        private string Token(InstanceId id)
        {
            foreach (var (collection, prefix) in new[] { ("MapNodes", "map:"), ("RewardCards", "reward:card:"), ("PotionBelt", "potion:") })
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
            F("run.room", Field(Run, "CurrentRoom").AsInt());
            F("run.map_floor", Field(Run,"MapFloor").AsInt());
            var current = Field(Run,"CurrentNode");
            F("run.map_coord", current.Kind == ValueKind.Ref ? Token(current.AsRef()) : "none");
            var deck = Members("LongTermDeck"); F("deck.order", string.Join(",", deck.Select(Card)));
            F("deck.instance_count", deck.Distinct().Count());
            for (var i = 0; i < deck.Count; i++) F("deck.instance:" + i, Card(deck[i]));
            F("potion.capacity", session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:PotionBelt").Capacity!);
            F("potion.inventory", string.Join(",", Members("PotionBelt").Select(p => Field(p,"SlotIndex").AsInt() + ":" + Field(p,"DefinitionId").AsString())));
            foreach (var node in Members("MapNodes"))
            {
                F("map." + Token(node) + ".type", Field(node,"Type").AsEnumOrdinal() == 5 ? "Monster" : "type:" + Field(node,"Type").AsEnumOrdinal());
                var relation = session.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren");
                foreach (var child in session.State.RelationTargets(relation.Id,node)) F("map.edge." + Token(node) + "->" + Token(child),true);
            }
            if (Phase == "combat")
            {
                facts.AddRange(EngineSnapshot.Facts(session, true));
                foreach (var card in Members("Hand")) F("visible." + Token(card) + ".definition", Field(card,"DefinitionId").AsString());
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
                F("reward.potion.resolved", Field(Run,"RewardPotionResolved").AsInt() == Field(Run,"CurrentRoom").AsInt());
                F("reward.potion.definition", string.Join(",",Members("RewardPotions").Select(p => Field(p,"DefinitionId").AsString())));
            }
            return Records.Facts(facts);
        }
        public IReadOnlyList<StreamSnapshot> Streams(int index,string kind) => EngineSnapshot.Streams(session);
        public DiagnosticSnapshot Diagnostic() => EngineSnapshot.Capture(session);
        public IReadOnlyList<DecisionSnapshot> Execute(string action, string? token)
        {
            var legal = session.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == action);
            var binding = token is null ? legal.Bindings.Single() : legal.Bindings.First(b => Token(b[legal.Definition.CandidateParameterIndex].AsRef()) == token);
            session.Submit(legal.Definition, SliceManifest.Player, binding);
            List<DecisionSnapshot> decisions = [];
            while (session.Status == SessionStatus.AwaitingDecision)
            {
                var d = session.PendingDecision!;
                var selected = d.Candidates.First(); // Registered target policy: first local legal target, never hidden state.
                decisions.Add(EngineSnapshot.DescribeDecision(session,SliceManifest.Player)! with { Chosen = Token(selected) });
                session.Answer(new DecisionAnswer(d.RequestId,[selected]));
            }
            if (session.Status == SessionStatus.Faulted) throw new CheckFailed(session.Fault?.Message ?? "base fault");
            return decisions;
        }
    }
}
