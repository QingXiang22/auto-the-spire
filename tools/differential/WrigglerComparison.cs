using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class WrigglerComparison
{
    private const string Niche = "base-spire:run:niche";
    private sealed record HpCall(long Minimum, long Maximum, long Result, long Before, long After);
    private sealed record Monster(long Hp, long MaxHp, long Strength, string Move, string Intent);
    private sealed record Card(long Cost, string Kind, bool CanUpgrade, bool Upgraded);
    private sealed record Boundary(string At, Monster[] Monsters, long Hp, long Block, long Energy, long Plays,
        bool Active, string Outcome, Card[] Hand, Card[] Draw, Card[] Discard, Card[] Exhaust, Card[] Play,
        long HpDraws, ulong[] HpState, long AiDraws, long ShuffleDraws);
    private sealed record Trace(Boundary[] States, HpCall[] Calls);

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output"))
            throw new FormatException("Use wriggler-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        var original = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        using var fixture = new Fixture(original.Root);
        var package = DslPackage.Load(Path.Combine(fixture.Root, "package.json"));
        var results = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" })
        {
            foreach (var mode in new[] { "four", "stunned", "hp-exhausted" }) Compare(seed, mode);
            foreach (var mode in new[] { "block", "unpowered", "death", "retain", "ethereal", "auto", "force-auto", "cost", "nonhand" }) Compare(seed, mode);
        }
        var report = new { schema = "dsl-migration/wriggler@1", scope = "controlled-component", reference,
            package_digest = original.Digest, fixture_digest = package.Digest, status = passed ? "passed" : "diverged",
            independent_verified = passed, normal_run_verified = false, parent_event_verified = false,
            controlled_setup = "Empty permanent decks on both sides; real combat start, draw/shuffle, turns and autoplay commands. Wriggler cases start at 1000 HP; Infection cases add real cards after combat start and explicitly set block, death HP or keywords/cost modifiers. Test-only DSL actions call the unchanged production procedures. No RNG reseed, compensation draw, forced move, injected outcome or parent-event certification.",
            cases = results };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;

        void Compare(string seed, string mode)
        {
            var left = Dsl(package, seed, mode); var right = Native(seed, mode);
            var same = NormalPreflight.Json(left) == NormalPreflight.Json(right); passed &= same;
            var wriggler = IsWriggler(mode);
            if (left.States.Length != (wriggler ? 4 : 2) || left.Calls.Length != (mode == "hp-exhausted" ? 8 : mode is "four" or "stunned" ? 4 : 1)
                || left.States.Any(s => s.Plays != 0 || s.AiDraws != 0) || (mode == "death" && left.States[^1].Outcome != "defeat"))
                throw new InvalidOperationException("Missing real component boundaries: " + seed + ":" + mode);
            results.Add(new { id = seed + ":" + mode, status = same ? "passed" : "diverged", left, right });
        }
    }

    private static bool IsWriggler(string mode) => mode is "four" or "stunned" or "hp-exhausted";
    private static Trace Dsl(DslPackage package, string seed, string mode)
    {
        var s = package.CreateSession(1, ["player"], seed, new SessionDebugOptions(MaxRecords: 100_000)); s.Start();
        InstanceId[] Instances(string type) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId id, string type, string name) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(name));
        void Set(InstanceId id, string type, string name, Value value) => s.State.RunAtomic(() => s.State.SetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(name), value));
        var player = Instances("Player").Single();
        void Submit(string name)
        {
            var action = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
            s.Submit(action.Definition, s.Participant(0), []);
            if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Component DSL stopped: " + s.Fault);
        }
        Set(player, "Player", "UsesRunDeck", Value.Of(true)); Set(player, "Player", "AutoDrawPerTurn", Value.Of(5L));
        if (IsWriggler(mode))
        {
            Set(player, "Player", "MaxHp", Value.Of(1000L)); Set(player, "Player", "Hp", Value.Of(1000L));
            Submit(mode == "stunned" ? "FixtureStunned" : "Wriggler.Spawn"); if (mode == "hp-exhausted") Submit("Wriggler.Spawn");
        }
        else Submit("BattleFriendV1.Spawn");
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        if (!IsWriggler(mode))
        {
            Submit("Infection.AddToHand"); if (mode is "block" or "death") Submit("Infection.AddToHand");
            if (mode == "death") { Submit("Defend.AddToHand"); Set(player, "Player", "Hp", Value.Of(2L)); }
            if (mode is "block" or "unpowered") Set(player, "Player", "Block", Value.Of(mode == "block" ? 4L : 0L));
            if (mode == "unpowered") Submit("FixturePowers");
            if (mode is "retain" or "ethereal") Set(Instances("Card").Single(), "Card", "Label", Value.Of(mode));
            if (mode == "cost") Submit("FixtureCosts");
            if (mode == "nonhand") Submit("FixtureDiscard");
        }
        var states = new List<Boundary> { Snapshot("start") };
        if (mode is "auto" or "force-auto") Submit(mode == "auto" ? "FixtureAuto" : "FixtureForceAuto");
        else
        {
            for (var turn = 1; turn <= (IsWriggler(mode) ? 3 : 1); turn++)
            {
                Submit("EndDevelopmentTurn"); if (Field(player, "Player", "CombatActive").AsBool()) Submit("BeginDevelopmentTurn");
                states.Add(Snapshot("turn:" + turn));
            }
        }
        if (mode is "auto" or "force-auto") states.Add(Snapshot("auto"));
        if (s.Debug!.DroppedRecords != 0) throw new InvalidOperationException("Component RNG diagnostics truncated.");
        var calls = s.Debug.Capture().Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == Niche).Select(r =>
        {
            var c = r.Random ?? throw new InvalidOperationException("Missing HP call details.");
            if (c.Operation != "int" || c.CandidatesTruncated) throw new InvalidOperationException("Unexpected HP operation.");
            return new HpCall(long.Parse(c.Minimum, CultureInfo.InvariantCulture), long.Parse(c.Maximum, CultureInfo.InvariantCulture), long.Parse(c.Result, CultureInfo.InvariantCulture), c.Before, c.After);
        }).ToArray();
        return new(states.ToArray(), calls);

        Boundary Snapshot(string at)
        {
            Card[] Pile(string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id)
                .Select(c => new Card(Field(c, "Card", "Cost").AsInt(), Field(c, "Card", "Kind").AsString(), Field(c, "Card", "CanUpgrade").AsBool(), Field(c, "Card", "Upgraded").AsBool())).ToArray();
            var rng = s.Random.Capture().Streams.Single(r => r.Name == Niche);
            return new(at, Instances("Enemy").Select(e => new Monster(Field(e, "Enemy", "Hp").AsInt(), Field(e, "Enemy", "MaxHp").AsInt(),
                Instances("Power").Where(p => Field(p, "Power", "HolderEnemy").Kind == ValueKind.Ref && Field(p, "Power", "HolderEnemy").AsRef() == e)
                    .Where(p => Field(p, "Power", "DefinitionId").AsString() == "base-spire:strength-power").Sum(p => Field(p, "Power", "Stacks").AsInt()),
                Field(e, "Enemy", "MoveId").AsString(), Field(e, "Enemy", "Intent").AsString())).ToArray(),
                Field(player, "Player", "Hp").AsInt(), Field(player, "Player", "Block").AsInt(), Field(player, "Player", "Energy").AsInt(),
                Field(player, "Player", "PlaySequence").AsInt(), Field(player, "Player", "CombatActive").AsBool(), Field(player, "Player", "CombatOutcome").AsString(),
                Pile("Hand"), Pile("DrawPile"), Pile("DiscardPile"), Pile("ExhaustPile"), Pile("InPlay"),
                rng.Draws, [rng.S0, rng.S1, rng.S2, rng.S3], s.Random.DrawCount("base-spire:run:monster_ai"), s.Random.DrawCount("base-spire:run:shuffle"));
        }
    }

    private static Trace Native(string seed, string mode)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered();
        var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        if (IsWriggler(mode)) { player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000); }
        var combat = new CombatState(run); var rng = run.Rng.Niche;
        var calls = new List<HpCall>(); (long Min, long Max) bounds = default;
        var hook = typeof(Rng).GetField("_drawObserver", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var previous = RngDiagnostics.DrawObserver;
        if (previous is not null || hook.GetValue(rng) is not null) throw new InvalidOperationException("RNG observer already owned.");
        RngDiagnostics.DrawObserver = (source, op, min, max, arity) =>
        {
            if (!ReferenceEquals(source, rng)) return;
            if (op != "NextInt" || arity is not (1 or 2)) throw new InvalidOperationException("Unexpected native HP draw.");
            bounds = ((long)min, (long)max);
        };
        hook.SetValue(rng, (Action<int, string, string>)((cursor, op, result) => calls.Add(new(bounds.Min, bounds.Max, long.Parse(result, CultureInfo.InvariantCulture), cursor - 1, cursor))));
        try
        {
            if (IsWriggler(mode))
                for (var i = 0; i < (mode == "hp-exhausted" ? 8 : 4); i++)
                {
                    var model = (Wriggler)ModelDb.Monster<Wriggler>().MutableClone(); model.StartStunned = mode == "stunned";
                    combat.AddMonster(model, CombatSide.Enemy, "wriggler" + (i % 4 + 1));
                }
            else combat.AddMonster((MonsterModel)ModelDb.Monster<BattleFriendV1>().MutableClone(), CombatSide.Enemy);
            var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult();
            if (!IsWriggler(mode))
            {
                var infection = Add<Infection>(); if (mode is "block" or "death") Add<Infection>();
                if (mode == "death") { Add<DefendIronclad>(); player.Creature.SetCurrentHpInternal(2); }
                if (mode == "block") player.Creature.GainBlockInternal(4);
                if (mode == "unpowered")
                {
                    PowerCmd.Apply<StrengthPower>(combat, player.Creature, 20, player.Creature, null).GetAwaiter().GetResult();
                    PowerCmd.Apply<WeakPower>(combat, player.Creature, 2, player.Creature, null).GetAwaiter().GetResult();
                    PowerCmd.Apply<VulnerablePower>(combat, player.Creature, 2, player.Creature, null).GetAwaiter().GetResult();
                }
                if (mode is "retain" or "ethereal") typeof(CardModel).GetMethod("AddKeywordInternal", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(infection, [mode == "retain" ? CardKeyword.Retain : CardKeyword.Ethereal]);
                if (mode == "cost") { infection.SetTemporaryCostOverrideThisCombat(0); infection.AddEnergyCostThisTurn(2); }
                if (mode == "nonhand") CardPileCmd.Add(infection, PileType.Discard);
            }
            var states = new List<Boundary> { Snapshot("start") };
            if (mode is "auto" or "force-auto")
            {
                var card = player.PlayerCombatState!.Hand.Cards.Single();
                typeof(CardModel).GetProperty(nameof(CardModel.ExhaustOnNextPlay))!.SetValue(card, mode == "force-auto");
                AutoPlayCmd.FromCards(combat, player, [card]).GetAwaiter().GetResult(); states.Add(Snapshot("auto"));
            }
            else for (var turn = 1; turn <= (IsWriggler(mode) ? 3 : 1); turn++)
            {
                engine.EndPlayerTurnAsync().GetAwaiter().GetResult(); states.Add(Snapshot("turn:" + turn));
            }
            return new(states.ToArray(), calls.ToArray());

            CardModel Add<T>() where T : CardModel
            {
                var card = (CardModel)ModelDb.Card<T>().MutableClone(); card.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(card); return card;
            }
            Boundary Snapshot(string at)
            {
                Card[] Pile(PileType type) => CardPileCmd.Get(type, player)!.Cards.Select(c => new Card(c.EnergyCost, c.Type.ToString().ToLowerInvariant(), c.IsUpgradable, c.IsUpgraded)).ToArray();
                var state = rng.ToSerializable(); var p = player.PlayerCombatState!;
                return new(at, combat.SpawnedEnemies.Select(e => new Monster(e.CurrentHp, e.MaxHp, (long)(e.GetPower<StrengthPower>()?.Amount ?? 0),
                    e.Monster!.NextMove!.Id, string.Join(",", e.Monster.NextMove.Intents.Select(i => i.IntentType.ToString().ToLowerInvariant() is var kind && kind == "statuscard" ? "status" : kind)))).ToArray(),
                    player.Creature.CurrentHp, (long)player.Creature.Block, p.Energy, p.CardsPlayedThisCombat, engine.IsInProgress, engine.IsInProgress ? "none" : engine.Won ? "victory" : "defeat",
                    Pile(PileType.Hand), Pile(PileType.Draw), Pile(PileType.Discard), Pile(PileType.Exhaust), Pile(PileType.Play),
                    state.counter, [state.state0, state.state1, state.state2, state.state3], run.Rng.MonsterAi.Counter + combat.SpawnedEnemies.Sum(e => (long)e.Monster!.Rng.Counter), run.Rng.Shuffle.Counter);
            }
        }
        finally { hook.SetValue(rng, null); RngDiagnostics.DrawObserver = previous; }
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "wriggler-comparison-" + Guid.NewGuid());
        internal Fixture(string original)
        {
            foreach (var source in Directory.GetFiles(original, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(Root, Path.GetRelativePath(original, source)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            var entry = Path.Combine(Root, "base.spire");
            File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"comparison-fixture.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "comparison-fixture.spire"), """
                using namespace base-spire;
                dsl_version = "core/0.3"
                action FixtureStunned { role = player locals = { enemy = Binding } body = {
                  call = { procedure = Wriggler.Create args = { slot = "wriggler1" startStunned = true } into = enemy }
                  call = { procedure = Wriggler.Create args = { slot = "wriggler2" startStunned = true } into = enemy }
                  call = { procedure = Wriggler.Create args = { slot = "wriggler3" startStunned = true } into = enemy }
                  call = { procedure = Wriggler.Create args = { slot = "wriggler4" startStunned = true } into = enemy }
                } }
                action FixtureAuto { role = player body = { call = { procedure = AutoPlayCard args = { owner = first(type(Player)) card = first(collection(Hand)) forceExhaust = false } } } }
                action FixtureForceAuto { role = player body = { call = { procedure = AutoPlayCard args = { owner = first(type(Player)) card = first(collection(Hand)) forceExhaust = true } } } }
                action FixtureDiscard { role = player body = { call = { procedure = DiscardCombatCard args = { owner = first(type(Player)) card = first(collection(Hand)) } } } }
                action FixtureCosts { role = player body = {
                  call = { procedure = AddCardCostModifier args = { card = first(collection(Hand)) source = "test" operation = "set" amount = 0 lifetime = "combat" } }
                  call = { procedure = AddCardCostModifier args = { card = first(collection(Hand)) source = "test" operation = "add" amount = 2 lifetime = "turn" } }
                } }
                action FixturePowers { role = player body = {
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = 20 } }
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:weak-power" stacks = 2 } }
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:vulnerable-power" stacks = 2 } }
                } }
                procedure FixtureKeyword { pure = true parameters = { value = Bool card = Ref(Card) keyword = String(length = 16) } result = Bool body = {
                  return = { value = or(value, equal(field(card, Card.Label), keyword)) }
                } }
                subscription FixtureKeyword { event = ModifyCardKeyword reaction = FixtureKeyword }
                """);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}
