using System.Globalization;
using CoreTheSpire.Engine;

namespace Differential;

internal static class DslBackend
{
    public static BackendRecord Run(string repository, CombatFixture fixture)
    {
        var package = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        if (fixture.Reshuffle && package.RandomProfile != RandomService.VanillaDrawProfileId)
            throw new NotSupportedException("Original reshuffle diagnostics require the declared vanilla raw-word profile.");
        var session = package.CreateSession((ulong)fixture.Seed, ["player"], fixture.GameSeed,
            fixture.Reshuffle ? new SessionDebugOptions(MaxRecords: 20_000) : null);
        session.Start();
        var player = Instances("Player").Single();
        var slots = new Dictionary<InstanceId, string>();

        // Original controlled-slice deck: five Strikes, four Defends, one Bash. Use current DSL definitions.
        foreach (var card in Instances("Card")) session.State.RunAtomic(() => session.State.DestroyInstance(card));
        var declarations = fixture.Reshuffle ? fixture.InitialCards : Enumerable.Repeat("base-spire:strike-ironclad", 5)
            .Concat(Enumerable.Repeat("base-spire:defend-ironclad", 4)).Append("base-spire:bash")
            .Select((name, i) => new OrderedCard("start:" + i.ToString("D2"), name, false)).ToArray();
        foreach (var card in declarations)
        {
            var name = card.Card switch { "base-spire:strike-ironclad" => "Strike", "base-spire:defend-ironclad" => "Defend", "base-spire:bash" => "Bash", _ => throw new FormatException("Unknown initial card.") };
            Submit(name + ".AddToHand");
            slots.Add(Members("Hand")[^1], card.Slot);
        }
        foreach (var card in Members("Hand").ToArray()) Move(card, "Hand", "DrawPile");
        session.State.RunAtomic(() =>
        {
            for (var index = 0; index < fixture.EnemyHp.Length; index++)
            {
                // Fixture HP targets have no matching DSL monster consumers yet. Their missing
                // turn effects remain real differences in the original reshuffle comparison.
                var enemy = session.State.CreateInstance(Type("Enemy").Id, session.State.RootScope);
                Set(enemy, "Enemy", "DefinitionId", Value.Of(index == 0 ? "base-spire:calcified-cultist" : "base-spire:damp-cultist"));
                Set(enemy, "Enemy", "Hp", Value.Of((long)fixture.EnemyHp[index]));
                Set(enemy, "Enemy", "MaxHp", Value.Of((long)fixture.EnemyHp[index]));
            }
        });
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        var strike = fixture.Reshuffle ? default : Members("DrawPile").First(c => Field(c, "Card", "DefinitionId").AsString() == fixture.Card);
        if (!fixture.Reshuffle) Move(strike, "DrawPile", "Hand");
        session.State.RunAtomic(() =>
        {
            Set(player, "Player", "Hp", Value.Of((long)fixture.PlayerHp));
            Set(player, "Player", "MaxHp", Value.Of((long)fixture.PlayerHp));
            Set(player, "Player", "Energy", Value.Of((long)fixture.Energy));
            if (fixture.Reshuffle) Set(player, "Player", "AutoDrawPerTurn", Value.Of(5L));
        });

        var beforeRandom = session.Random.TotalDraws;
        var startCounts = session.Random.Diagnostics().ToDictionary(s => s.Name, s => s.Draws);
        var start = Snapshot("start", []);
        if (fixture.Reshuffle)
        {
            var boundaries = new List<Boundary> { start };
            for (var step = 0; step < 3; step++)
            {
                Submit("EndDevelopmentTurn"); Submit("BeginDevelopmentTurn");
                boundaries.Add(Snapshot($"step:{step}:end_turn", []));
            }
            return new("dsl:base-spire", package.Digest + ":" + package.Rules.Identity,
                Field(player, "Player", "CombatActive").AsBool() ? "running" : session.Status.ToString(), beforeRandom,
                session.Random.TotalDraws - beforeRandom, fixture.RequiredDimensions, boundaries.ToArray());
        }
        Submit("PlayTargetedCard", strike);
        var request = session.PendingDecision ?? throw new InvalidOperationException("Target decision was not published.");
        if (session.Status != SessionStatus.AwaitingDecision) throw new InvalidOperationException("Target choice did not suspend the DSL.");
        var enemies = Instances("Enemy");
        var chosen = enemies[int.Parse(fixture.Target.AsSpan("enemy:".Length), CultureInfo.InvariantCulture)];
        var decision = new DecisionSnapshot(request.Candidates.Select(c => "enemy:" + Array.IndexOf(enemies, c)).ToArray(),
            request.Minimum, request.Maximum, fixture.Target);
        session.Answer(new DecisionAnswer(request.RequestId, [chosen]));
        Healthy();
        var end = Snapshot("step:0:play_card", [decision]);
        return new("dsl:base-spire", package.Digest + ":" + package.Rules.Identity,
            session.Status == SessionStatus.Running && Field(player, "Player", "CombatActive").AsBool() ? "running" : session.Status.ToString(),
            beforeRandom, session.Random.TotalDraws - beforeRandom, CombatFixture.Dimensions, [start, end]);

        EntityTypeDefinition Type(string name) => session.RuleSet.TypeByQualifiedId("base-spire:" + name);
        InstanceId[] Instances(string name) => session.State.AllInstances(Type(name).Id).ToArray();
        int Collection(string name) => session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id;
        IReadOnlyList<InstanceId> Members(string name) => session.State.CollectionMembers(Collection(name));
        Value Field(InstanceId id, string type, string name) => session.State.GetField(id, Type(type).FieldIndex(name));
        void Set(InstanceId id, string type, string name, Value value) => session.State.SetField(id, Type(type).FieldIndex(name), value);
        void Move(InstanceId card, string from, string to) => session.State.RunAtomic(() =>
        {
            session.State.RemoveFromCollection(Collection(from), card); session.State.AddToCollection(Collection(to), card);
        });
        void Healthy()
        {
            if (session.Status == SessionStatus.Faulted) throw new InvalidOperationException("DSL fault: " + session.Fault);
        }
        void Submit(string name, params InstanceId[] values)
        {
            var action = session.LegalActions(session.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
            session.Submit(action.Definition, session.Participant(0), values.Select(Value.OfRef).ToArray()); Healthy();
        }
        Boundary Snapshot(string kind, DecisionSnapshot[] decisions)
        {
            var facts = new SortedDictionary<string, string>(StringComparer.Ordinal);
            void Number(string path, long value) => facts.Add(path, value.ToString(CultureInfo.InvariantCulture));
            foreach (var (key, field) in new[] { ("hp", "Hp"), ("max_hp", "MaxHp"), ("block", "Block"), ("energy", "Energy") })
                Number("player." + key, Field(player, "Player", field).AsInt());
            foreach (var enemy in Instances("Enemy"))
            {
                var prefix = "enemy." + Field(enemy, "Enemy", "DefinitionId").AsString();
                Number(prefix + ".hp", Field(enemy, "Enemy", "Hp").AsInt());
                Number(prefix + ".max_hp", Field(enemy, "Enemy", "MaxHp").AsInt());
                Number(prefix + ".block", Field(enemy, "Enemy", "Block").AsInt());
                facts.Add(prefix + ".alive", Field(enemy, "Enemy", "Hp").AsInt() > 0 ? "true" : "false");
                Powers(enemy, "HolderEnemy", prefix);
            }
            Powers(player, "HolderPlayer", "player");
            foreach (var (collection, key) in new[] { ("DrawPile", "draw"), ("Hand", "hand"), ("DiscardPile", "discard"), ("ExhaustPile", "exhaust") })
            {
                var cards = Members(collection).Select(c => Field(c, "Card", "DefinitionId").AsString()
                    + (Field(c, "Card", "Upgraded").AsBool() ? "+" : "")).ToArray();
                Number("pile." + key + ".size", cards.Length);
                facts.Add("pile." + key + ".sorted", string.Join(",", cards.Order(StringComparer.Ordinal)));
                // Preserve the original no-random case: draw/discard membership, hand/exhaust order.
                if (fixture.Reshuffle || key is "hand" or "exhaust") facts.Add("pile." + key + ".order", string.Join(",", cards));
                if (fixture.Reshuffle) facts.Add("pile." + key + ".slots", string.Join(",", Members(collection).Select(c => slots[c])));
            }
            var legal = session.LegalActions(session.Participant(0));
            var play = legal.Where(a => a.Definition.QualifiedId is "base-spire:PlayTargetedCard" or "base-spire:PlayCardFromHand")
                .SelectMany(a => a.Bindings.Select(binding => binding[a.Definition.CandidateParameterIndex].AsRef())).ToHashSet();
            var playable = Members("Hand").Select((card, index) => (card, index)).Where(pair => play.Contains(pair.card))
                .Select(pair => "card:hand:" + pair.index).ToArray();
            var actions = new List<ActionOption>();
            if (playable.Length > 0) actions.Add(new("base-spire:PlayCard", playable, playable.Length));
            if (legal.Any(a => a.Definition.QualifiedId == "base-spire:EndDevelopmentTurn")) actions.Add(new("base-spire:EndTurn", [], 1));
            return new(kind, actions.ToArray(), decisions, facts) { Random = fixture.Reshuffle ? RandomSnapshot() : null };

            void Powers(InstanceId holder, string field, string prefix)
            {
                var powers = Members("Powers").Where(p => Field(p, "Power", field).Kind == ValueKind.Ref
                    && Field(p, "Power", field).AsRef() == holder).ToArray();
                Number("power." + prefix + ".count", powers.Length);
                for (var index = 0; index < powers.Length; index++)
                {
                    facts.Add($"power.{prefix}.{index}.id", Field(powers[index], "Power", "DefinitionId").AsString());
                    Number($"power.{prefix}.{index}.stacks", Field(powers[index], "Power", "Stacks").AsInt());
                }
            }
        }

        RandomTrace RandomSnapshot()
        {
            var debug = session.Debug!;
            var records = debug.Capture().Where(r => r.Kind == SessionDebugKind.RandomCall).ToArray();
            var calls = records.Select(r =>
            {
                var draw = r.Random ?? throw new InvalidOperationException("Random trace payload missing.");
                return new RandomDraw(r.Subject!, draw.Operation, draw.Minimum, draw.Maximum, draw.Result, draw.Before, draw.After,
                    draw.Candidates?.Select(c => slots[c]).ToArray(), r.Source?.ToString());
            }).ToArray();
            var snapshot = session.Random.Capture();
            var positions = snapshot.ExplicitSeeds.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
            {
                var stream = snapshot.Streams.SingleOrDefault(s => s.Name == p.Key);
                var words = stream?.BackendState ?? new Xoshiro256StarStar(p.Value, false).Capture();
                return new StreamPosition(p.Key, startCounts.GetValueOrDefault(p.Key), stream?.Draws ?? 0,
                    string.Join(",", words.Select(w => w.ToString("x16", CultureInfo.InvariantCulture))));
            }).ToArray();
            return new(calls, debug.DroppedRecords, records.Any(r => r.Random!.CandidatesTruncated), positions);
        }
    }
}
