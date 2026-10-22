using System.Reflection;
#if REFERENCE_SIM
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

// DispatchProxy implements the locked reference's internal, read-only combat observer.
// No source rewrite, replacement engine, state injection or rule callbacks are installed.
public class NormalReferenceObserver : DispatchProxy
{
    internal Action<string, object?[]> Record { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    { Record(method!.Name, args ?? []); return null; }
}

internal sealed class NormalReferenceRun : IDisposable
{
#if REFERENCE_SIM
    private readonly RunState run;
    private readonly Player player;
    private readonly EventRoom opening;
    private readonly Dictionary<CardModel, string> permanent = [];
    private readonly Dictionary<CardModel, string> generated = [];
    private readonly HashSet<int> generatedActions = [];
    private int actionIndex = -1;
    private readonly List<object> events = [];
    private readonly NormalReferenceRandom random;
    private readonly bool diagnostics;
    private CombatRoom? combat;
    private CardModel? targetedCard;
    public string Phase { get; private set; } = "neow";
    private Neow Neow => (Neow)opening.Event;
    internal static string Id(ModelId id) => "base-spire:" + id.Entry.ToLowerInvariant().Replace('_', '-');
    private static string Relic(string name) => Id(ModelDb.GetId(typeof(Neow).Assembly.GetType("Sts2Sim.Core.Models.Relics." + name)!));
    public NormalReferenceRun(string seed, bool diagnostics = true)
    {
        this.diagnostics = diagnostics;
        random = new NormalReferenceRandom(diagnostics);
        try
        {
            RunReplayDemo.EnsureModelsRegistered();
            run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()]);
            player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
            random.Name(run, player);
            foreach (var card in player.Deck.Cards) permanent.Add(card, "permanent:" + permanent.Count.ToString("D2"));
            run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
            opening = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(opening);
        }
        catch { random.Dispose(); throw; }
    }
    public void Start()
    {
        opening.Enter(run).GetAwaiter().GetResult();
        random.Name(Neow.Rng, "base-spire:event:neow");
    }
    private string Card(CardModel card)
    {
        var source = card.DeckVersion ?? card;
        var slot = permanent.GetValueOrDefault(source);
        if (slot is null)
        {
            if (!generated.TryGetValue(card, out slot))
            {
                if (!generatedActions.Add(actionIndex)) throw new NotSupportedException("missing_dimension: multiple generated cards in one common action require native creation-origin observation.");
                generated.Add(card, slot = "generated:action:" + actionIndex);
            }
        }
        return slot + ":" + Id(card.Id) + (card.IsUpgraded ? "+" : "");
    }
    private string Creature(Creature creature) => ReferenceEquals(creature, player.Creature) ? "player" :
        "enemy:" + combat!.Engine.State.SpawnedEnemies.ToList().IndexOf(creature);
    private object? EventArgument(object? arg) => arg switch
    {
        null => null, CardModel c => Card(c), Creature c => Creature(c),
        PowerModel p => new { id = Id(p.Id), holder = Creature(p.Owner), amount = p.Amount },
        DamageResult d => new { receiver = Creature(d.Receiver), d.BlockedDamage, d.UnblockedDamage, d.OverkillDamage, d.WasTargetKilled, props = d.Props.ToString() },
        AbstractModel m => Id(m.Id), string or int or decimal or bool => arg,
        _ => arg.GetType().FullName
    };
    public void Apply(NormalAction action) => ApplyAsync(action).GetAwaiter().GetResult();
    private async Task ApplyAsync(NormalAction action)
    {
        if (!Actions().Any(a => a.Key == action.Key)) throw new InvalidOperationException("Reference rejects common action: " + action.Key);
        actionIndex++;
        switch (action.Kind)
        {
            case "neow":
                await Neow.ChooseOption(Neow.CurrentOptions.Single(o => Relic(o.Key) == action.Arguments[0])); break;
            case "leave-neow":
                if (!Neow.IsFinished) throw new InvalidOperationException("Native Neow has not finished.");
                await opening.Exit(run); run.PopCurrentRoom(); Phase = "map"; break;
            case "map":
                var node = run.CurrentMapPoint!.Children.Single(n => n.coord.col + "," + n.coord.row == action.Arguments[0]);
                if (node.PointType.ToString() != "Monster") throw new NotSupportedException("Normal first encounter requires a Monster map point.");
                run.AddVisitedMapCoord(node.coord);
                combat = (CombatRoom)RoomFactory.CreateRoom(run, RoomType.Monster); run.PushRoom(combat);
                random.NameEncounter(unchecked(run.Rng.Seed + (ulong)run.TotalFloor + StringHelper.GetDeterministicHashCode(combat.Encounter!.IdEntry)));
                if (diagnostics)
                {
                    var observerType = typeof(CombatRoom).Assembly.GetType("Sts2Sim.Core.Combat.ICombatObserver")!;
                    var observer = DispatchProxy.Create(observerType, typeof(NormalReferenceObserver));
                    ((NormalReferenceObserver)observer).Record = (name, args) => events.Add(new { name, arguments = args.Select(EventArgument).ToArray() });
                    typeof(CombatRoom).GetMethod("ConfigureObserver", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(combat, [observer]);
                }
                await combat.Enter(run); Phase = "combat"; break;
            case "targeted-play":
                targetedCard = player.PlayerCombatState!.Hand.Cards.Single(c => Card(c) == action.Arguments[0]);
                break;
            case "choose-target":
                var selectedTarget = CombatTargetCandidates.ForCard(combat!.Engine.State, player, targetedCard!.TargetType)
                    .Single(c => Creature(c) == action.Arguments[0]);
                var selectedCard = targetedCard; targetedCard = null;
                await combat.Engine.PlayCardAsync(player, selectedCard, selectedTarget);
                combat.Engine.CheckWinCondition(); await Resolve(); break;
            case "play":
                var card = player.PlayerCombatState!.Hand.Cards.Single(c => Card(c) == action.Arguments[0]);
                var target = card.TargetType == TargetType.AnyEnemy ? combat!.Engine.State.Enemies.First(e => e.IsAlive) : null;
                await combat!.Engine.PlayCardAsync(player, card, target);
                // This is the native RunDriver action order, not a comparison-only compensation.
                combat.Engine.CheckWinCondition(); await Resolve(); break;
            case "end-turn": await combat!.Engine.EndPlayerTurnAsync(); await Resolve(); break;
            case "claim-gold": await combat!.Rewards!.Gold.Take(); break;
            case "claim-card":
                var chosen = combat!.Rewards!.Card.Options.Single(c => Id(c.Id) == action.Arguments[0]);
                await combat.Rewards.Card.SelectOption(chosen);
                foreach (var acquired in player.Deck.Cards) if (!permanent.ContainsKey(acquired)) permanent.Add(acquired, "permanent:" + permanent.Count.ToString("D2"));
                break;
            case "skip-card": await combat!.Rewards!.Card.Skip(); break;
            case "claim-potion": await combat!.Rewards!.Potion!.Take(); break;
            case "skip-potion": await combat!.Rewards!.Potion!.Skip(); break;
            case "close-rewards":
                if (!combat!.Rewards!.Gold.IsResolved) await combat.Rewards.Gold.Skip();
                if (!combat.Rewards.Card.IsResolved) await combat.Rewards.Card.Skip();
                if (combat.Rewards.Potion is { IsResolved: false } potion) await potion.Skip();
                await combat.Exit(run); run.PopCurrentRoom(); Phase = "map"; break;
            default: throw new NotSupportedException(action.Kind);
        }
    }
    private async Task Resolve()
    {
        if (!combat!.Engine.IsInProgress)
        {
            if (!combat.Engine.Won) throw new InvalidOperationException("Normal reference lost the combat.");
            await combat.ResolveOutcomeAsync(); Phase = "rewards";
        }
    }
    private NormalAction[] Actions()
    {
        if (Phase == "neow") return Neow.IsFinished ? [new("leave-neow", [])] : Neow.CurrentOptions.Select(o => new NormalAction("neow", [Relic(o.Key)])).ToArray();
        if (Phase == "map") return run.CurrentMapPoint!.Children.Where(n => !run.VisitedMapCoords.Contains(n.coord)).Select(n => new NormalAction("map", [n.coord.col + "," + n.coord.row])).ToArray();
        var result = new List<NormalAction>();
        if (Phase == "combat")
        {
            if (targetedCard is not null)
                return CombatTargetCandidates.ForCard(combat!.Engine.State, player, targetedCard.TargetType)
                    .Select(c => new NormalAction("choose-target", [Creature(c)])).ToArray();
            foreach (var card in player.PlayerCombatState!.Hand.Cards.Where(c => c.CanPlay(out _)))
            {
                if (card.TargetType == TargetType.AnyEnemy)
                {
                    result.Add(new("play", [Card(card), "first-legal-target"]));
                    result.Add(new("targeted-play", [Card(card)]));
                }
                else if (card.TargetType is TargetType.Self or TargetType.AllEnemies or TargetType.None) result.Add(new("play", [Card(card)]));
                else throw new NotSupportedException("Unextracted target constraint: " + card.TargetType);
            }
            result.Add(new("end-turn", [])); return result.ToArray();
        }
        var rewards = combat!.Rewards!;
        if (!rewards.Gold.IsResolved) result.Add(new("claim-gold", []));
        if (!rewards.Card.IsResolved)
        {
            result.AddRange(rewards.Card.Options.Select(c => new NormalAction("claim-card", [Id(c.Id)])));
            result.Add(new("skip-card", []));
        }
        if (rewards.Potion is { IsResolved: false } p)
        {
            if (p.CanTake) result.Add(new("claim-potion", [])); result.Add(new("skip-potion", []));
        }
        result.Add(new("close-rewards", [])); return result.ToArray();
    }
    public NormalBoundary Capture(string kind)
    {
        var v = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Put(string key, object? value) => v.Add(key, NormalPreflight.Json(value));
        Put("facts.player.hp", player.Creature.CurrentHp); Put("facts.player.max_hp", player.Creature.MaxHp); Put("facts.player.gold", player.Gold);
        Put("visits.phase", Phase); Put("visits.act", run.CurrentActIndex); Put("visits.total_floor", run.TotalFloor);
        Put("visits.coordinates", run.VisitedMapCoords.Select(c => c.col + "," + c.row).Order(StringComparer.Ordinal).ToArray());
        Put("permanent.deck", player.Deck.Cards.Select(Card).ToArray());
        Put("permanent.relics", player.Relics.Select(r => Id(r.Id)).ToArray());
        Put("permanent.potions", player.PotionSlots.Where(p => p is not null).Select(p => Id(p!.Id)).ToArray());
        var state = player.PlayerCombatState;
        foreach (var (name, pile) in new[] { ("Hand", state?.Hand), ("DrawPile", state?.DrawPile), ("DiscardPile", state?.DiscardPile), ("ExhaustPile", state?.ExhaustPile), ("InPlay", state?.PlayPile) })
            Put("piles." + name, pile?.Cards.Select(Card).ToArray() ?? []);
        Put("piles.RemovedFromCombat", Array.Empty<string>());
        if (Phase == "combat")
        {
            Put("facts.player.block", player.Creature.Block); Put("facts.player.energy", state!.Energy); Put("facts.player.turn", state.TurnNumber);
            Put("facts.enemies", combat!.Engine.State.SpawnedEnemies.Select(e => new { id = Id(e.Monster!.Id), hp = e.CurrentHp, max_hp = e.MaxHp, block = e.Block,
                move = e.Monster.NextMove!.Id }).ToArray());
            Put("facts.powers", combat.Engine.State.SpawnedEnemies.Prepend(player.Creature).SelectMany(c => c.Powers).Select(p => new { id = Id(p.Id), holder = Creature(p.Owner), amount = (int)p.Amount })
                .OrderBy(p => p.holder).ThenBy(p => p.id).ToArray());
        }
        Put("actions.candidates", Actions().Select(a => a.Key).Distinct().Order(StringComparer.Ordinal).ToArray());
        if (diagnostics) Put("random.stream_states", random.ConsumedStates());
        Put("decisions.current", targetedCard is not null ? new { min = 1, max = 1,
            candidates = CombatTargetCandidates.ForCard(combat!.Engine.State, player, targetedCard.TargetType).Select(Creature).ToArray() }
            : Phase == "neow" ? Neow.IsFinished ? new { min = 0, max = 0, candidates = new[] { "true" } } :
            new { min = 1, max = 1, candidates = Neow.CurrentOptions.Select(o => Relic(o.Key)).Order(StringComparer.Ordinal).ToArray() } : null);
        return new(kind, v, ["public: complete cross-backend projection", "random: full initialization candidates/normalization", "events: cross-backend event projection",
                "decisions: targeted-play constraints", "lifecycle: final-kill effects and duplicate/stale submissions"],
            new { backend = "reference:sts2-sim", build = NormalPreflight.Hash(typeof(RunState).Assembly.Location), events = events.ToArray(), random = random.Capture(),
                encounter = combat?.Encounter?.IdEntry, native_root_seed = run.Rng.Seed, normal_state_injection = false });
    }
    public void Dispose() => random.Dispose();
    internal string KnownRandomState => random.KnownState();
#else
    public string Phase => throw new NotSupportedException("unsupported_reference_adapter");
    public NormalReferenceRun(string seed, bool diagnostics = true) => throw new NotSupportedException("unsupported_reference_adapter: clean locked reference required");
    public void Start() => throw new NotSupportedException("unsupported_reference_adapter");
    public void Apply(NormalAction action) => throw new NotSupportedException("unsupported_reference_adapter");
    public NormalBoundary Capture(string kind) => throw new NotSupportedException("unsupported_reference_adapter");
    public void Dispose() { }
#endif
}

#if REFERENCE_SIM
internal sealed class NormalReferenceRandom : IDisposable
{
    private readonly FieldInfo hook = typeof(Rng).GetField("_drawObserver", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private readonly Dictionary<Rng, string> names = [];
    private readonly List<Rng> attached = [];
    private sealed record Draw(string Operation, double Minimum, double Maximum, int Arity, Sts2Sim.Core.Saves.SerializableRng State);
    private sealed record Call(Rng Rng, string Operation, string Result, int Before, int After, Draw Parameters,
        Sts2Sim.Core.Saves.SerializableRng StateAfter);
    private readonly Dictionary<Rng, Draw> pending = [];
    private readonly List<Call> calls = [];
    private long dropped;
    private readonly RngDrawObserver? previous;
    private readonly bool enabled;
    public NormalReferenceRandom(bool enabled)
    {
        this.enabled = enabled;
        previous = RngDiagnostics.DrawObserver;
        if (previous is not null) throw new NotSupportedException("Reference global RNG diagnostic already owned.");
        if (enabled) RngDiagnostics.DrawObserver = Before;
    }
    private void Before(Rng rng, string operation, double minimum, double maximum, int arity)
    {
        // Upstream advances Counter before this callback, and the PRNG word afterwards.
        // Capture both real states; this hook never draws, replays or replaces a generator.
        var state = rng.ToSerializable(); state.counter--;
        if (!pending.TryAdd(rng, new(operation, minimum, maximum, arity, state)))
            throw new NotSupportedException("Reference RNG call is missing its post-result notification.");
        if (attached.Contains(rng)) return;
        if (hook.GetValue(rng) is not null) throw new NotSupportedException("Reference result observer already owned.");
        Action<int, string, string> observer = (cursor, op, result) =>
        {
            var parameters = pending[rng]; pending.Remove(rng);
            if (calls.Count == 20_000) { dropped++; return; }
            calls.Add(new(rng, op, result, cursor - 1, cursor, parameters, rng.ToSerializable()));
        };
        hook.SetValue(rng, observer); attached.Add(rng);
    }
    private string Name(Rng rng) => names.GetValueOrDefault(rng) ?? (rng.DiagnosticStreamName is { } name && name.StartsWith("named.", StringComparison.Ordinal)
        ? "base-spire:run:" + name[6..] : rng.DiagnosticStreamName) ?? "unidentified:" + attached.IndexOf(rng);
    public void Name(Rng rng, string name)
    {
        if (names.TryGetValue(rng, out var previous) && previous != name) throw new NotSupportedException("Conflicting reference RNG identity.");
        names[rng] = name;
    }
    public void NameEncounter(ulong seed)
    {
        if (!enabled) return;
        // The factory's private RNG is identified by its actual pre-draw state and the
        // documented EncounterMonsterSeed formula. Only this diagnostic object is constructed;
        // no generator used by the native run is reset, reseeded or drawn here.
        var expected = new Rng(seed).ToSerializable();
        var candidates = calls.GroupBy(c => c.Rng).Where(g => !names.ContainsKey(g.Key) && g.Key.DiagnosticStreamName is null)
            .Where(g => g.First().Parameters.State == expected).Select(g => g.Key).ToArray();
        if (candidates.Length != 1) throw new NotSupportedException("Encounter RNG cannot be identified uniquely from its actual initial state.");
        Name(candidates[0], "base-spire:encounter:monsters");
    }
    public object ConsumedStates() => attached.OrderBy(Name, StringComparer.Ordinal).Select(r =>
    {
        var s = r.ToSerializable();
        return new { stream = Name(r), draws = (long)s.counter,
            words = new[] { s.state0, s.state1, s.state2, s.state3 }.Select(w => w.ToString("x16")).ToArray() };
    }).ToArray();
    public void Name(RunState run, Player player)
    {
        foreach (var type in Enum.GetValues<RunRngType>()) names.Add(run.Rng.GetRng(type), "base-spire:run:" + StringHelper.SnakeCase(type.ToString()));
        foreach (var type in Enum.GetValues<PlayerRngType>()) names.Add(player.PlayerRng.GetRng(type), "base-spire:player:" + StringHelper.SnakeCase(type.ToString()));
    }
    public object Capture() => new { dropped, unfinished = pending.Count,
        calls = calls.Select(c => new { stream = Name(c.Rng), c.Operation, c.Result, c.Before, c.After, c.Parameters, c.StateAfter }).ToArray(),
        streams = names.Keys.Concat(attached).Distinct().Select(r => new { name = Name(r), state = r.ToSerializable() }).ToArray() };
    public string KnownState() => System.Text.Json.JsonSerializer.Serialize(names.OrderBy(p => p.Value, StringComparer.Ordinal)
        .Select(p => new { name = p.Value, state = p.Key.ToSerializable() }), new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
    public void Dispose()
    {
        foreach (var rng in attached) hook.SetValue(rng, null);
        if (enabled) RngDiagnostics.DrawObserver = previous;
    }
}
#endif
