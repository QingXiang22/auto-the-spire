using System.Reflection;
#if REFERENCE_SIM
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
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
    private readonly Dictionary<PotionModel, string> potionOrigins = [];
    private readonly Dictionary<CardModel, string> rewardCardOrigins = [];
    private readonly HashSet<int> generatedActions = [];
    private int actionIndex = -1;
    private readonly List<object> events = [];
    private readonly List<NormalObservedEvent> observedEvents = [];
    private readonly NormalReferenceEventCoverage eventCoverage = new();
    private readonly List<NormalSettlement> settlement = [];
    private bool cardEffectFinished;
    private readonly NormalReferenceRandom random;
    private readonly bool diagnostics;
    private CombatRoom? combat;
    private NormalReferenceRewards? rewardObservation;
    private NormalRewardCandidateCall[] rewardCandidates = [];
    private object? rewardCandidateSources;
    private NormalInitializationCandidates? initialization;
    private object? initializationSources;
    private NormalNeowCandidates? neowCandidates;
    private object? neowSources;
    private CardModel? targetedCard;
    private PlayerCombatState? lastCombatState;
    private string[] neowOptions = [];
    private NormalNeowOption[] neowPublicOptions = [];
    public string Phase { get; private set; } = "neow";
    private Neow Neow => (Neow)opening.Event;
    internal static string Id(ModelId id) => "base-spire:" + id.Entry.ToLowerInvariant().Replace('_', '-');
    private static string Relic(string name) => Id(ModelDb.GetId(typeof(Neow).Assembly.GetType("Sts2Sim.Core.Models.Relics." + name)!));
    public NormalReferenceRun(string seed, bool diagnostics = true)
    {
        this.diagnostics = diagnostics;
        RunReplayDemo.EnsureModelsRegistered();
        NormalReferencePotionCapabilities.Prime();
        random = new NormalReferenceRandom(diagnostics);
        try
        {
            Sts2Sim.Core.Content.ActDefinition[] acts = [new Overgrowth(), new Hive(), new Glory()];
            random.WatchOpening(acts);
            run = new RunState(seed, acts);
            player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
            random.Name(run, player);
            var initializationObserver = diagnostics ? new NormalReferenceInitialization(acts, run, player) : null;
            foreach (var card in player.Deck.Cards) permanent.Add(card, "permanent:" + permanent.Count.ToString("D2"));
            if (diagnostics) random.WatchShuffle(player, Card);
            run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
            opening = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(opening);
            if (initializationObserver is not null)
            {
                initializationSources = initializationObserver.Sources;
                initialization = initializationObserver.Capture(random.InitializationDraws(), random.WeightedCalls());
            }
        }
        catch { random.Dispose(); throw; }
    }
    public void Start()
    {
        opening.Enter(run).GetAwaiter().GetResult();
        random.Name(Neow.Rng, "base-spire:event:neow");
        neowOptions = Neow.CurrentOptions.Select(o => Relic(o.Key)).ToArray();
        neowPublicOptions = Neow.CurrentOptions.Select((o, index) => NormalReferenceNeow.Read(o, index)).ToArray();
        if (diagnostics)
        {
            var observer = new NormalReferenceNeowCandidates(run);
            neowSources = observer.Sources;
            neowCandidates = observer.Capture(random.NeowDraws(), neowOptions);
        }
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
    private NormalEnemyView Enemy(Creature creature)
    {
        var monster = creature.Monster!;
        var move = monster.NextMove ?? throw new NotSupportedException("missing_dimension: native next move");
        var index = Array.IndexOf(monster.MoveStateMachine!.States.Values.OfType<MoveState>().ToArray(), move);
        if (index < 0 || move.Intents.Count != 1) throw new NotSupportedException("missing_dimension: native registered move index or compound intent");
        var intent = move.Intents.Single().IntentType switch
        {
            IntentType.Attack => "attack", IntentType.StatusCard => "status",
            _ => throw new NotSupportedException("missing_dimension: native first-encounter intent")
        };
        return new(Creature(creature), Id(monster.Id), creature.CurrentHp, creature.MaxHp, creature.Block, move.Id, index, intent, combat!.Engine.State.EscapedCreatures.Contains(creature));
    }
    private NormalPublicPower PublicPower(PowerModel power) => power is VulnerablePower
        ? new(Id(power.Id), Creature(power.Owner), power.Amount, power.SkipNextDurationTick, false)
        : throw new NotSupportedException("missing_dimension: native power turn-end expiry outside Vulnerable duration semantics");
    private string Potion(PotionModel potion) => potionOrigins.TryGetValue(potion, out var origin) ? origin + ":" + Id(potion.Id)
        : throw new NotSupportedException("missing_dimension: reference potion acquisition origin");
    private NormalAction[] PotionDiscards() => player.CanUseOrRemovePotions ? player.PotionSlots.Where(p => p is not null)
        .Select(p => new NormalAction("discard-potion", [Potion(p!)])).ToArray() : [];
    private object? EventArgument(object? arg) => arg switch
    {
        null => null, CardModel c => Card(c), Creature c => Creature(c),
        PowerModel p => new { id = Id(p.Id), holder = Creature(p.Owner), amount = p.Amount },
        DamageResult d => new { receiver = Creature(d.Receiver), d.BlockedDamage, d.UnblockedDamage, d.OverkillDamage, d.WasTargetKilled, props = d.Props.ToString() },
        AbstractModel m => Id(m.Id), string or int or decimal or bool => arg,
        CombatSide side => side.ToString(),
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
                if (diagnostics)
                {
                    var hpStart = random.CallCount;
                    Action<CombatRoom, CombatState> beforeSetup = (room, state) =>
                    {
                        if (room.Encounter?.IdEntry != "SLIMES_WEAK") throw new NotSupportedException("missing_dimension: native initial HP source outside SlimesWeak");
                        random.CaptureHpOrigins(state, hpStart);
                    };
                    typeof(CombatRoom).GetMethod("ConfigureBeforeSetupDiagnostic", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(combat, [beforeSetup]);
                    var observerType = typeof(CombatRoom).Assembly.GetType("Sts2Sim.Core.Combat.ICombatObserver")!;
                    var observer = DispatchProxy.Create(observerType, typeof(NormalReferenceObserver));
                    ((NormalReferenceObserver)observer).Record = (name, args) =>
                    {
                        var projectedBefore = observedEvents.Count;
                        events.Add(new { name, arguments = args.Select(EventArgument).ToArray() });
                        if (name == "CardPlayStarted") cardEffectFinished = false;
                        if (name == "CardPlayFinished") cardEffectFinished = true;
                        if (name == "DamageResolved" && args[1] is DamageResult result)
                            observedEvents.Add(new("damage", Damage: new(args[0] is Creature dealer ? Creature(dealer) : null,
                                Creature(result.Receiver), result.BlockedDamage.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
                                result.UnblockedDamage.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
                                result.OverkillDamage.ToString("G29", System.Globalization.CultureInfo.InvariantCulture), result.WasTargetKilled)));
                        else if (name is "CardDrawn" or "CardPlayStarted" or "CardPlayFinished")
                            observedEvents.Add(new(name switch { "CardDrawn" => "draw", "CardPlayStarted" => "before-play", _ => "after-play" }, Card((CardModel)args[0]!)));
                        else if (name == "PowerAmountChanged" && args[0] is PowerModel power)
                            observedEvents.Add(new("power-amount-changed", Power: new(Id(power.Id), Creature(power.Owner),
                                power.Amount.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
                                ((decimal)args[1]!).ToString("G29", System.Globalization.CultureInfo.InvariantCulture))));
                        else if (name == "PlayerTurnStarted") observedEvents.Add(new("player-turn-started"));
                        else if (name is "CombatStarted" or "PlayerTurnPrepared" or "EnemyTurnStarting" or "PlayerTurnEnded")
                            observedEvents.Add(new(name switch { "CombatStarted" => "combat-started", "PlayerTurnPrepared" => "player-turn-prepared",
                                "EnemyTurnStarting" => "enemy-turn-starting", _ => "player-turn-ended" }));
                        else if (name == "EnemyMoveStarted") observedEvents.Add(new("enemy-move-started", Actor: Creature((Creature)args[0]!), Move: (string)args[1]!));
                        else if (name == "EnemyMoveFinished") observedEvents.Add(new("enemy-move-finished", Actor: Creature((Creature)args[0]!), Move: (string)args[1]!));
                        eventCoverage.Record(name, args, observedEvents.Count == projectedBefore + 1);
                    };
                    typeof(CombatRoom).GetMethod("ConfigureObserver", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(combat, [observer]);
                }
                await combat.Enter(run);
                lastCombatState = player.PlayerCombatState;
                random.NameEncounter(unchecked(run.Rng.Seed + (ulong)run.TotalFloor + StringHelper.GetDeterministicHashCode(combat.Encounter!.IdEntry)));
                Phase = "combat"; break;
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
                var beforeDeck = player.Deck.Cards.ToHashSet();
                await combat.Rewards.Card.SelectOption(chosen);
                var acquired = player.Deck.Cards.Where(c => !beforeDeck.Contains(c)).ToArray();
                // Native SelectOption clones its chosen offer before AddToDeck. This registered
                // one-card acquisition has one new object and an actual SelectedOption source;
                // never re-pair a permanent card by its name or position after the operation.
                if (acquired.Length != 1 || !ReferenceEquals(combat.Rewards.Card.SelectedOption, chosen))
                    throw new NotSupportedException("missing_dimension: unique native reward acquisition origin");
                permanent.Add(acquired[0], rewardCardOrigins[combat.Rewards.Card.SelectedOption]);
                break;
            case "skip-card": await combat!.Rewards!.Card.Skip(); break;
            case "claim-potion": await combat!.Rewards!.Potion!.Take(); break;
            case "skip-potion": await combat!.Rewards!.Potion!.Skip(); break;
            case "discard-potion": await PotionCmd.Discard(player.PotionSlots.Single(p => p is not null && Potion(p) == action.Arguments[0])!); break;
            case "close-rewards":
                if (!combat!.Rewards!.Gold.IsResolved) await combat.Rewards.Gold.Skip();
                if (!combat.Rewards.Card.IsResolved) await combat.Rewards.Card.Skip();
                if (combat.Rewards.Potion is { IsResolved: false } potion) await potion.Skip();
                await combat.Exit(run); run.PopCurrentRoom(); Phase = "map"; break;
            default: throw new NotSupportedException(action.Kind);
        }
        if (diagnostics) random.VerifyShuffle();
    }
    private async Task Resolve()
    {
        if (!combat!.Engine.IsInProgress)
        {
            if (!combat.Engine.Won) throw new InvalidOperationException("Normal reference lost the combat.");
            if (diagnostics)
            {
                if (!cardEffectFinished || combat.GeneratedRewards.Count != 0)
                    throw new NotSupportedException("missing_dimension: native final card must finish before rewards are generated");
                settlement.Add(new("effects-finished", player.Creature.CurrentHp, player.Creature.MaxHp, player.Gold, false));
            }
            var rewardStart = random.CallCount;
            var rewardSources = diagnostics ? new NormalReferenceRewardCandidates(player) : null;
            rewardCandidateSources = rewardSources?.Sources;
            await combat.ResolveOutcomeAsync(); Phase = "rewards";
            rewardObservation = new(run, combat, random.StreamName);
            if (rewardSources is not null) rewardCandidates = rewardSources.Capture(random.RewardDraws(rewardStart), combat.Rewards!);
            if (diagnostics) settlement.Add(new("rewards-published", player.Creature.CurrentHp, player.Creature.MaxHp, player.Gold, combat.GeneratedRewards.Count > 0));
            if (combat.Rewards!.Potion?.Potion is { } potion)
                potionOrigins.Add(potion, $"generated:action:{actionIndex}:potion-reward:0");
            foreach (var (rewardCard, index) in combat.Rewards.Card.Options.Select((card, index) => (card, index)))
                rewardCardOrigins.Add(rewardCard, $"generated:action:{actionIndex}:card-reward:{index}");
        }
    }
    private NormalAction[] Actions()
    {
        if (Phase == "neow") return Neow.IsFinished ? [new("leave-neow", [])] : Neow.CurrentOptions.Select(o => new NormalAction("neow", [Relic(o.Key)])).ToArray();
        if (Phase == "map") return run.CurrentMapPoint!.Children.Where(n => !run.VisitedMapCoords.Contains(n.coord))
            .Select(n => new NormalAction("map", [n.coord.col + "," + n.coord.row])).Concat(PotionDiscards()).ToArray();
        var result = PotionDiscards().ToList();
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
        var configuration = NormalReferenceConfiguration.Capture(run, player);
        var restContext = NormalReferenceRestState.Capture(run);
        var rest = NormalReferenceRestState.Normalize(restContext);
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
        Put("public.piles", new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay", "RemovedFromCombat", "RunDeck" }.Select(name =>
        {
            var cards = name switch
            {
                "Hand" => state?.Hand.Cards.ToArray() ?? [], "DrawPile" => state?.DrawPile.Cards.ToArray() ?? [],
                "DiscardPile" => state?.DiscardPile.Cards.ToArray() ?? [], "ExhaustPile" => state?.ExhaustPile.Cards.ToArray() ?? [],
                "InPlay" => state?.PlayPile.Cards.ToArray() ?? [], "RunDeck" => player.Deck.Cards.ToArray(),
                "RemovedFromCombat" => Array.Empty<CardModel>(), _ => throw new NotSupportedException(name)
            };
            return new { name, total = (long)cards.Length, complete = name != "DrawPile",
                members = name == "DrawPile" ? Array.Empty<string>() : cards.Select(Card).Order(StringComparer.Ordinal).ToArray(),
                positions = Array.Empty<object>() };
        }).ToArray());
        var map = run.Map.GetAllMapPoints().Append(run.Map.StartingMapPoint).Append(run.Map.BossMapPoint).ToArray();
        if (map.Distinct().Count() != map.Length || map.Select(n => n.coord).Distinct().Count() != map.Length)
            throw new NotSupportedException("missing_dimension: ambiguous native map publication");
        string Coord(Sts2Sim.Core.Map.MapPoint point) => point.coord.col + "," + point.coord.row;
        Put("public.map", map.Select((n, index) => new NormalMapNode(index, Coord(n), n.PointType.ToString(), run.VisitedMapCoords.Contains(n.coord),
            Phase is "neow" or "map" && run.CurrentMapPoint!.Children.Contains(n) && !run.VisitedMapCoords.Contains(n.coord),
            n.Children.Select(Coord).Order(StringComparer.Ordinal).ToArray())).OrderBy(n => n.Coordinate, StringComparer.Ordinal).ToArray());
        Put("public.run", new NormalRunView(Phase, run.CurrentActIndex, run.CurrentMapCoord!.Value.row, run.TotalFloor,
            Coord(run.CurrentMapPoint!), map.Length, Phase == "neow", NormalReferenceConfiguration.Profile(configuration), Id(ModelDb.GetId(run.CurrentAncientEventType))));
        var active = combat?.Engine.IsInProgress ?? false;
        Put("public.player", new NormalPlayerView(player.Creature.CurrentHp, player.Creature.MaxHp, player.Gold, Id(player.Character.Id), player.MaxEnergy,
            active, combat is null || active ? "none" : combat.Engine.Won ? "victory" : "defeat", Phase == "combat" ? "battle" : Phase,
            active && lastCombatState?.Phase == PlayerTurnPhase.Play, lastCombatState?.Energy ?? 0, lastCombatState?.TurnNumber ?? 0, player.Creature.Block, true, configuration.BaseDraw,
            rest.RoomUsed, rest.PendingForge, combat?.Engine.State.EscapedCreatures.Count > 0));
        // Compare the active combat's counters. The native room retains its completed
        // history during rewards; DSL clears that history before publishing rewards.
        var history = combat?.Engine.State.DamageHistory.Entries.Where(e => ReferenceEquals(e.Receiver, player.Creature) && e.Result.UnblockedDamage > 0).ToArray() ?? [];
        // Before the first combat there is no native ICombatState. Read the actual
        // run listener set and its native methods rather than inventing a combat state.
        Put("public.draw_blocked", combat is not null
            ? !Sts2Sim.Core.Hooks.Hook.ShouldDraw(combat.Engine.State, player, false, out _)
            : !run.IterateHookListeners(null).All(model => model.ShouldDraw(player, false)));
        Put("public.player_history", new NormalPlayerHistory(active ? state!.CardsPlayedThisCombat : 0, active ? history.Length : 0,
            active && history.Any(e => e.HappenedThisTurn(combat!.Engine.State)), active && combat!.Engine.State.CountCardsExhaustedThisTurn(player) > 0,
            combat?.Engine.State.CurrentSide == CombatSide.Enemy, player.Deck.Type == PileType.Deck));
        Put("public.enemies", combat is not null && Phase != "map" ? combat.Engine.State.SpawnedEnemies.Select(Enemy).OrderBy(e => e.Origin, StringComparer.Ordinal).ToArray() : []);
        Put("public.powers", (combat is not null && Phase != "map" ? combat.Engine.State.SpawnedEnemies.Prepend(player.Creature).SelectMany(c => c.Powers) : [])
            .Select(PublicPower)
            .OrderBy(p => p.Holder, StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray());
        Put("public.visits", combat is null ? Array.Empty<NormalVisitView>() : [new NormalVisitView(combat.Id ?? throw new NotSupportedException("Native room has no visit identity"),
            Coord(run.CurrentMapPoint!), run.CurrentMapPoint!.PointType.ToString(), combat.RoomType.ToString().ToLowerInvariant(), "base-spire:" + combat.Encounter!.IdEntry.ToLowerInvariant().Replace('_', '-'),
            Phase == "map" ? "closed" : Phase, Phase == "map" ? "closed" : "active", combat.Won)]);
        Put("public.neow", new NormalNeowView(Phase != "neow" ? "closed" : Neow.IsFinished ? "finished" : "pending",
            player.Relics.Select(r => Id(r.Id)).SingleOrDefault(neowOptions.Contains) ?? "none", neowPublicOptions));
        var offer = Phase == "rewards" ? combat!.Rewards : null;
        Put("public.reward_sets", rewardObservation?.Capture(Potion) ?? []);
        Put("public.reward_offer", new NormalRewardView(offer is not null, offer is not null && !offer.Gold.IsResolved,
            offer is not null && !offer.Gold.IsResolved ? offer.Gold.Amount : 0, offer is not null && !offer.Card.IsResolved,
            offer?.Potion is { IsResolved: false }, offer is not null && !offer.Card.IsResolved
                ? offer.Card.Options.Select(c => new NormalCardOffer(Id(c.Id), c.IsUpgraded, c.EnergyCost, c.Rarity.ToString().ToLowerInvariant()))
                    .OrderBy(c => c.Id, StringComparer.Ordinal).ThenBy(c => c.Upgraded).ToArray() : [],
            offer?.Potion is { IsResolved: false, Potion: {} offeredPotion } ? [Potion(offeredPotion)] : []));
        NormalPotionView PublicPotion(PotionModel p, long slot) => new(Potion(p), slot, p.Usage switch
            {
                Sts2Sim.Core.Entities.Potions.PotionUsage.CombatOnly => "combat", Sts2Sim.Core.Entities.Potions.PotionUsage.AnyTime => "anytime",
                Sts2Sim.Core.Entities.Potions.PotionUsage.Automatic => "automatic", _ => throw new NotSupportedException("Unmapped native potion usage")
            }, p.TargetType switch { TargetType.Self => "self", TargetType.AnyPlayer when run.Players.Count == 1 => "self",
                TargetType.AnyEnemy => "enemy", TargetType.None => "none",
                _ => throw new NotSupportedException("Unmapped native potion target") }, NormalReferencePotionCapabilities.Read(p).Supported);
        var visiblePotions = player.PotionSlots.OfType<PotionModel>().Concat(offer?.Potion is { IsResolved: false, Potion: {} pendingPotion } ? new[] { pendingPotion } : []).Distinct().ToArray();
        Put("public.potions", visiblePotions.Select(p => PublicPotion(p, Array.IndexOf(player.PotionSlots.ToArray(), p))).OrderBy(p => p.Origin, StringComparer.Ordinal).ToArray());
        Put("public.potion_inventory", player.PotionSlots.Select((p, index) => (p, index)).Where(p => p.p is not null)
            .Select(p => PublicPotion(p.p!, p.index)).OrderBy(p => p.Origin, StringComparer.Ordinal).ToArray());
        Put("public.relic_inventory", player.Relics.Select(r => new NormalRelicView(Id(r.Id), r.IsMelted)).OrderBy(r => r.Id, StringComparer.Ordinal).ToArray());
        var visibleCards = player.Deck.Cards.Select(c => (scope: "deck", card: c))
            .Concat(state?.AllPiles.Where(p => p.Type != PileType.Draw).SelectMany(p => p.Cards).Select(c => (scope: "combat", card: c)) ?? [])
            .Concat(offer is not null && !offer.Card.IsResolved ? offer.Card.Options.Select(c => (scope: "reward", card: c)) : []);
        Put("public.cards", visibleCards.Select(pair =>
        {
            var c = pair.card;
            if (c.CostsXEnergy) throw new NotSupportedException("missing_dimension: native captured energy X for public card");
            var origin = pair.scope == "reward" ? rewardCardOrigins[c] + ":" + Id(c.Id) + (c.IsUpgraded ? "+" : "") : Card(c);
            var damage = c is Sts2Sim.Core.Models.Cards.ICardDamageVariableProvider provider && provider.TryGetThrashDamageVariable(out var value) ? value : 0m;
            if (damage != decimal.Truncate(damage)) throw new NotSupportedException("missing_dimension: noninteger public card damage");
            return new NormalCardView(pair.scope, origin, Id(c.Id), Id(c.Id), c.Id.Entry, c.Type.ToString().ToLowerInvariant(), c.Rarity.ToString().ToLowerInvariant(),
                c.LocalEnergyCost, c.CanonicalEnergyCostValue, (long)damage, c.IsUpgraded, c.MaxUpgradeLevel > 0, c.TargetType == TargetType.AnyEnemy,
                c.GetKeywordsWithSources(KeywordSources.Local).Contains(CardKeyword.Exhaust), c.HasKeyword(CardKeyword.Innate), c.Type == CardType.Power,
                c.Tags.Contains(CardTag.Strike), c.CostsXEnergy, 0);
        }).OrderBy(c => c.Scope, StringComparer.Ordinal).ThenBy(c => c.Origin, StringComparer.Ordinal).ToArray());
        Put("public.ownership", NormalPublicTopology.NativeOwnership());
        Put("public.map_members", map.Select(Coord).Order(StringComparer.Ordinal).ToArray());
        Put("public.map_sequence", map.Select(Coord).ToArray());
        Put("public.map_parents", map.SelectMany(n => n.parents.Select(parent => Coord(n) + "->" + Coord(parent))).Order(StringComparer.Ordinal).ToArray());
        Put("public.visit_links", new NormalVisitLinks(combat?.Id, combat?.Id ?? 0, Phase == "rewards" ? combat?.Id : null,
            combat is null ? [] : [combat.Id + (combat.GeneratedRewards.Count == 0 ? ":none" : ":reward:" + combat.Id)]));
        Put("public.relation_types", new[] { "net:relation:base-spire:MapChildren", "net:relation:base-spire:MapParents", "net:relation:base-spire:NeowOptions" });
        // DTO-only view entities stand for the native player's seven piles and the native run.
        // Native rooms and their generated reward sets remain retained after returning to the map.
        Put("public.entity_counts", new Dictionary<string, int>
        {
            ["Player"] = run.Players.Count, ["Run"] = 1, ["NeowVisit"] = 1, ["NeowOption"] = neowOptions.Length,
            ["MapNode"] = map.Length, ["RunVisit"] = combat is null ? 0 : 1, ["CombatRewardSet"] = combat?.GeneratedRewards.Count ?? 0,
            ["Card"] = visibleCards.Count(), ["Relic"] = player.Relics.Count,
            ["Potion"] = player.PotionSlots.Count(p => p is not null) + (offer?.Potion is { IsResolved: false, Potion: not null } ? 1 : 0),
            ["Enemy"] = combat is not null && Phase != "map" ? combat.Engine.State.SpawnedEnemies.Count : 0,
            ["Power"] = combat is not null && Phase != "map" ? combat.Engine.State.SpawnedEnemies.Prepend(player.Creature).Sum(c => c.Powers.Count) : 0,
        }.Concat(NormalPublicTopology.Piles.Select(p => new KeyValuePair<string, int>(p + "View", 1)))
            .Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ":" + p.Value).ToArray());
        if (Phase == "combat")
        {
            Put("facts.player.block", player.Creature.Block); Put("facts.player.energy", state!.Energy); Put("facts.player.turn", state.TurnNumber);
            Put("facts.enemies", combat!.Engine.State.SpawnedEnemies.Select(e => new { id = Id(e.Monster!.Id), hp = e.CurrentHp, max_hp = e.MaxHp, block = e.Block,
                move = e.Monster.NextMove!.Id }).ToArray());
            Put("facts.powers", combat.Engine.State.SpawnedEnemies.Prepend(player.Creature).SelectMany(c => c.Powers).Select(p => new { id = Id(p.Id), holder = Creature(p.Owner), amount = (int)p.Amount })
                .OrderBy(p => p.holder).ThenBy(p => p.id).ToArray());
        }
        Put("actions.candidates", Actions().Select(a => a.Key).Distinct().Order(StringComparer.Ordinal).ToArray());
        var scalars = diagnostics ? random.ScalarCalls() : null;
        if (scalars is not null) Put("random.scalar_calls", scalars.Calls);
        if (diagnostics) Put("random.weighted_candidates", random.WeightedCalls());
        if (diagnostics) Put("random.reward_candidates", rewardCandidates);
        if (diagnostics) Put("random.initialization_candidates", initialization);
        if (diagnostics) Put("random.neow_candidates", neowCandidates);
        if (diagnostics)
        {
            var attempts = random.OpeningAttempts(); var queues = NormalOpeningQueues.Reference(run);
            NormalOpeningQueues.Validate(random.WeightedCalls(), attempts, queues);
            Put("random.weighted_attempts", attempts); Put("random.opening_queues", queues);
        }
        if (diagnostics) { Put("random.step_states", random.StepStates()); Put("random.shuffle_candidates", random.ShuffleCalls()); Put("random.stream_states", random.ConsumedStates()); Put("random.float32_calls", random.Float32Calls()); Put("events.observed_order", observedEvents.ToArray()); Put("lifecycle.settlement", settlement.ToArray()); }
        Put("decisions.current", targetedCard is not null ? new { min = 1, max = 1,
            candidates = CombatTargetCandidates.ForCard(combat!.Engine.State, player, targetedCard.TargetType).Select(Creature).ToArray() }
            : Phase == "neow" ? Neow.IsFinished ? new { min = 0, max = 0, candidates = new[] { "true" } } :
            new { min = 1, max = 1, candidates = Neow.CurrentOptions.Select(o => Relic(o.Key)).Order(StringComparer.Ordinal).ToArray() } : null);
        var coverage = eventCoverage.Capture();
        var publicComplete = NormalPublicSnapshot.Add(v, [], Actions().Length > 0 ? "decision" : "unmapped-outcome");
        string[] missing = [.. (publicComplete ? Array.Empty<string>() : new[] { "public: complete cross-backend projection" }), "random: full initialization candidates/normalization", "events: cross-backend event projection"];
        if (coverage.Unmapped.Length > 0) missing = [.. missing, $"events: {coverage.Unmapped.Length} native notifications remain unmapped"];
        return new(kind, v, missing,
            new { backend = "reference:sts2-sim", build = NormalPreflight.Hash(typeof(RunState).Assembly.Location), events = events.ToArray(), random = random.Capture(),
                native_player_history = new { active, retained_plays = lastCombatState?.CardsPlayedThisCombat, retained_hp_losses = history.Length },
                native_configuration = configuration,
                native_neow_option_callbacks = neowPublicOptions,
                native_potion_capability_probes = visiblePotions.Select(NormalReferencePotionCapabilities.Read).Distinct().ToArray(),
                native_rest_context = restContext,
                native_event_coverage = coverage,
                native_reward_policies = rewardObservation?.Evidence,
                native_reward_candidate_sources = rewardCandidateSources,
                native_initialization_sources = initializationSources,
                native_neow_sources = neowSources,
                unmapped_scalar_calls = scalars?.Unmapped,
                unmapped_weighted_semantics = Array.Empty<string>(),
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
    private sealed record Draw(string Operation, double Minimum, double Maximum, int Arity, Sts2Sim.Core.Saves.SerializableRng State)
    { public NormalWeightedSnapshot? Weighted { get; init; } }
    private sealed record Call(Rng Rng, string Operation, string Result, int Before, int After, Draw Parameters,
        Sts2Sim.Core.Saves.SerializableRng StateAfter);
    private readonly Dictionary<Rng, Draw> pending = [];
    private readonly List<Call> calls = [];
    private Player? shufflePlayer;
    private Func<CardModel, string>? shuffleIdentity;
    private List<CardModel>? shuffled;
    private int shuffleExpected;
    private bool firstShuffle = true;
    private readonly List<NormalShuffleCall> shuffleCalls = [];
    internal void WatchShuffle(Player player, Func<CardModel, string> identity) { shufflePlayer = player; shuffleIdentity = identity; }
    private void ObserveShuffle(int cursor, Draw parameters, string result)
    {
        if (shufflePlayer?.PlayerCombatState is not { } state || shuffleIdentity is null
            || parameters is not { Operation: "NextInt", Arity: 1, Minimum: 0 })
            throw NormalShuffleTrace.Missing("unsupported native shuffle call");
        var count = checked((int)parameters.Maximum);
        if (shuffled is null)
        {
            shuffled = state.DiscardPile.Cards.Concat(state.DrawPile.Cards).ToList();
            if (!firstShuffle) shuffled.Sort();
            firstShuffle = false; shuffleExpected = shuffled.Count;
        }
        if (count != shuffleExpected || count < 2) throw NormalShuffleTrace.Missing("native shuffle candidate count");
        var selected = int.Parse(result, System.Globalization.CultureInfo.InvariantCulture);
        if (selected < 0 || selected >= count) throw NormalShuffleTrace.Missing("native shuffle index");
        shuffleCalls.Add(new(cursor - 1L, cursor, selected, shuffled.Take(count).Select(shuffleIdentity).ToArray()));
        (shuffled[count - 1], shuffled[selected]) = (shuffled[selected], shuffled[count - 1]);
        shuffleExpected--;
    }
    internal void VerifyShuffle()
    {
        if (shuffled is null) return;
        var state = shufflePlayer!.PlayerCombatState!;
        var seen = state.Hand.Cards.Concat(state.DrawPile.Cards).Where(shuffled.Contains).ToArray();
        if (shuffleExpected != 1 || !seen.SequenceEqual(shuffled)) throw NormalShuffleTrace.Missing("native shuffle result does not match actual piles");
        shuffled = null;
    }
    internal NormalShuffleCall[] ShuffleCalls()
    {
        var stream = names.Single(pair => pair.Value == "base-spire:run:shuffle").Key;
        if (dropped != 0 || shuffled is not null || stream.Counter != shuffleCalls.Count) throw NormalShuffleTrace.Missing("incomplete native shuffle observation");
        return NormalShuffleTrace.Canonical(shuffleCalls);
    }
    private readonly Dictionary<Call, NormalScalarCall> hpCalls = [];
    private NormalHpOrigin[] hpOrigins = [];
    internal int CallCount => calls.Count;
    internal NormalInitializationDraw[] NeowDraws()
    {
        if (dropped != 0 || pending.Count != 0) throw NormalNeowTrace.Missing("incomplete native Neow diagnostics");
        return calls.Where(c => Name(c.Rng) == NormalNeowTrace.Stream).Select(c => new NormalInitializationDraw(
            c.Parameters.Operation, c.Before, c.After, c.Parameters.Minimum, c.Parameters.Maximum, c.Parameters.Arity, c.Result)).ToArray();
    }
    internal NormalInitializationDraw[] InitializationDraws()
    {
        if (dropped != 0 || pending.Count != 0) throw NormalInitializationTrace.Missing("incomplete native initialization diagnostics");
        return calls.Where(c => Name(c.Rng) == NormalInitializationTrace.Stream).Select(c => new NormalInitializationDraw(
            c.Parameters.Operation, c.Before, c.After, c.Parameters.Minimum, c.Parameters.Maximum, c.Parameters.Arity, c.Result)).ToArray();
    }
    internal NormalRewardDraw[] RewardDraws(int start)
    {
        if (dropped != 0 || pending.Count != 0) throw NormalRewardCandidateTrace.Missing("incomplete native reward diagnostics");
        return calls.Skip(start).Select(c => Name(c.Rng) == NormalRewardCandidateTrace.Stream
            ? new NormalRewardDraw(c.Parameters.Operation, c.Before, c.After, c.Parameters.Minimum, c.Parameters.Maximum, c.Parameters.Arity, c.Result)
            : throw NormalRewardCandidateTrace.Missing("unclassified stream during native reward generation")).ToArray();
    }
    internal void CaptureHpOrigins(CombatState state, int start)
    {
        if (hpOrigins.Length != 0 || state.SpawnedEnemies.Any(e => e.CurrentHp != e.MaxHp))
            throw new NotSupportedException("missing_dimension: HP origin capture must precede setup effects");
        var draws = calls.Skip(start).Where(c => Name(c.Rng) == "base-spire:run:niche").ToArray();
        if (draws.Any(c => c.Parameters is not { Operation: "NextInt", Arity: 2 }))
            throw new NotSupportedException("missing_dimension: non-integer HP initialization draw");
        hpOrigins = state.SpawnedEnemies.Select((e, i) => new NormalHpOrigin("enemy:" + i + ":" + NormalReferenceRun.Id(e.Monster!.Id),
            e.Monster.MinInitialHp, checked((long)e.Monster.MaxInitialHp + 1), e.MaxHp)).ToArray();
        var normalized = NormalHpTrace.Normalize(hpOrigins, draws.Select(c => NormalScalarTrace.Integer(Name(c.Rng), c.Before, c.After,
            checked((long)c.Parameters.Minimum), checked((long)c.Parameters.Maximum), long.Parse(c.Result, System.Globalization.CultureInfo.InvariantCulture))).ToArray());
        for (var i = 0; i < draws.Length; i++) hpCalls.Add(draws[i], normalized[i]);
    }
    private long dropped;
    private readonly RngDrawObserver? previous;
    private readonly bool enabled;
    private NormalReferenceWeightedBags? opening;
    internal void WatchOpening(Sts2Sim.Core.Content.ActDefinition[] acts)
    { if (enabled) opening = new(acts); }
    public NormalReferenceRandom(bool enabled)
    {
        this.enabled = enabled;
        previous = RngDiagnostics.DrawObserver;
        if (previous is not null) throw new NotSupportedException("Reference global RNG diagnostic already owned.");
        if (enabled) RngDiagnostics.DrawObserver = Before;
    }
    private void Before(Rng rng, string operation, double minimum, double maximum, int arity)
    {
        opening?.FinishPending();
        // Upstream advances Counter before this callback, and the PRNG word afterwards.
        // Capture both real states; this hook never draws, replays or replaces a generator.
        var state = rng.ToSerializable(); state.counter--;
        var weighted = operation == "NextDouble" && Name(rng) == "base-spire:run:up_front"
            ? (opening ?? throw NormalWeightedTrace.Missing("native opening bag observer")).Capture(state.counter) : null;
        if (!pending.TryAdd(rng, new(operation, minimum, maximum, arity, state) { Weighted = weighted }))
            throw new NotSupportedException("Reference RNG call is missing its post-result notification.");
        if (attached.Contains(rng)) return;
        if (hook.GetValue(rng) is not null) throw new NotSupportedException("Reference result observer already owned.");
        Action<int, string, string> observer = (cursor, op, result) =>
        {
            var parameters = pending[rng]; pending.Remove(rng);
            if (calls.Count == 20_000) { dropped++; return; }
            calls.Add(new(rng, op, result, cursor - 1, cursor, parameters, rng.ToSerializable()));
            if (Name(rng) == "base-spire:run:shuffle") ObserveShuffle(cursor, parameters, result);
            if (parameters.Weighted is not null) opening!.Result(double.Parse(result, System.Globalization.CultureInfo.InvariantCulture));
        };
        hook.SetValue(rng, observer); attached.Add(rng);
    }
    private string Name(Rng rng) => names.GetValueOrDefault(rng) ?? (rng.DiagnosticStreamName is { } name && name.StartsWith("named.", StringComparison.Ordinal)
        ? "base-spire:run:" + name[6..] : rng.DiagnosticStreamName) ?? "unidentified:" + attached.IndexOf(rng);
    internal string StreamName(Rng rng) => Name(rng);
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
        var sequence = names.Count(p => p.Value.StartsWith("base-spire:encounter:monsters", StringComparison.Ordinal));
        Name(candidates[0], sequence == 0 ? "base-spire:encounter:monsters" : "base-spire:encounter:monsters:" + sequence);
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
        hp_initialization = hpOrigins,
        opening_commits = opening?.Commits(),
        calls = calls.Select(c => new { stream = Name(c.Rng), c.Operation, c.Result, c.Before, c.After, c.Parameters, c.StateAfter }).ToArray(),
        streams = names.Keys.Concat(attached).Distinct().Select(r => new { name = Name(r), state = r.ToSerializable() }).ToArray() };
    internal NormalRandomStateStep[] StepStates()
    {
        if (dropped != 0 || pending.Count != 0) throw new NotSupportedException("missing_dimension: incomplete native per-call states");
        return NormalRandomStateTrace.Canonical(calls.Select(c =>
        {
            var before = c.Parameters.State; var after = c.StateAfter;
            if (before.counter != c.Before || after.counter != c.After)
                throw new NotSupportedException("missing_dimension: native per-call state cursor");
            return new NormalRandomStateStep(Name(c.Rng), c.Before, c.After,
                NormalRandomStateTrace.Words(before.state0, before.state1, before.state2, before.state3),
                NormalRandomStateTrace.Words(after.state0, after.state1, after.state2, after.state3));
        }));
    }
    internal NormalFloat32Call[] Float32Calls()
    {
        if (dropped != 0 || pending.Count != 0) throw new NotSupportedException("missing_dimension: incomplete native random diagnostics");
        return NormalFloat32Trace.Canonical(calls.Where(c => c.Parameters.Operation == "NextFloat").Select(c =>
        {
            if (c.Parameters.Arity != 2 || c.Parameters.Minimum != (float)c.Parameters.Minimum || c.Parameters.Maximum != (float)c.Parameters.Maximum)
                throw new NotSupportedException("missing_dimension: native float parameter shape");
            return new NormalFloat32Call(Name(c.Rng), c.Before, c.After,
                BitConverter.SingleToUInt32Bits((float)c.Parameters.Minimum), BitConverter.SingleToUInt32Bits((float)c.Parameters.Maximum),
                BitConverter.SingleToUInt32Bits(float.Parse(c.Result, System.Globalization.CultureInfo.InvariantCulture)));
        }));
    }
    internal NormalScalarProjection ScalarCalls()
    {
        if (dropped != 0 || pending.Count != 0) throw new NotSupportedException("missing_dimension: incomplete native scalar diagnostics");
        var result = new List<NormalScalarCall>(); var unmapped = new List<NormalUnmappedScalar>();
        foreach (var call in calls)
        {
            var stream = Name(call.Rng); var p = call.Parameters;
            if (p.Operation == "NextFloat") continue;
            if (stream == "base-spire:run:niche" && p.Operation == "NextInt")
            {
                if (!hpCalls.TryGetValue(call, out var hp)) throw new NotSupportedException("missing_dimension: unattributed native niche HP draw");
                result.Add(hp); continue;
            }
            if (p.Operation == "NextInt" && p.Arity is 1 or 2)
                result.Add(NormalScalarTrace.Integer(stream, call.Before, call.After, Integral(p.Minimum), Integral(p.Maximum), long.Parse(call.Result, System.Globalization.CultureInfo.InvariantCulture)));
            else if (p.Operation == "NextBool" && p.Arity == 0)
                result.Add(NormalScalarTrace.Integer(stream, call.Before, call.After, 0, 2, bool.Parse(call.Result) ? 0 : 1));
            else if (p.Operation == "NextDouble" && p.Arity == 0)
            {
                if (stream == "base-spire:run:up_front" && p.Weighted is null) throw new NotSupportedException("missing_dimension: native weighted scalar attribution");
                if (stream is not ("base-spire:run:act_1_map" or "base-spire:run:up_front")) throw new NotSupportedException("missing_dimension: unclassified native double source");
                result.Add(NormalScalarTrace.UnitDouble(stream, call.Before, call.After, double.Parse(call.Result, System.Globalization.CultureInfo.InvariantCulture)));
            }
            else throw new NotSupportedException("missing_dimension: native scalar operation " + p.Operation);
        }
        return new(NormalScalarTrace.Canonical(result), unmapped.OrderBy(c => c.Stream, StringComparer.Ordinal).ThenBy(c => c.Before).ToArray());
        static long Integral(double value) => double.IsFinite(value) && value == Math.Truncate(value) && value >= int.MinValue && value <= int.MaxValue
            ? (long)value : throw new NotSupportedException("missing_dimension: non-integral native integer bound");
    }
    internal NormalWeightedCall[] WeightedCalls()
    {
        if (dropped != 0 || pending.Count != 0) throw NormalWeightedTrace.Missing("incomplete native weighted diagnostics");
        return NormalWeightedTrace.Canonical(calls.Where(c => c.Parameters.Operation == "NextDouble" && Name(c.Rng) == "base-spire:run:up_front").Select(c =>
        {
            if (c.Parameters.Arity != 0 || c.Parameters.Weighted is not { } candidates) throw NormalWeightedTrace.Missing("actual native weighted candidates");
            return NormalWeightedTrace.FromUnit(Name(c.Rng), c.Before, c.After, candidates,
                double.Parse(c.Result, System.Globalization.CultureInfo.InvariantCulture));
        }));
    }
    internal NormalOpeningAttempt[] OpeningAttempts() => (opening ?? throw NormalWeightedTrace.Missing("native opening attempt observer")).Attempts();
    // The factory-local generator is retained only by the enabled observer. Compare the
    // same run/player/event objects on both sides; per-call diagnostics retain its words.
    public string KnownState() => System.Text.Json.JsonSerializer.Serialize(names.Where(p => !p.Value.StartsWith("base-spire:encounter:monsters", StringComparison.Ordinal)).OrderBy(p => p.Value, StringComparer.Ordinal)
        .Select(p => new { name = p.Value, state = p.Key.ToSerializable() }), new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
    public void Dispose()
    {
        foreach (var rng in attached) hook.SetValue(rng, null);
        if (enabled) RngDiagnostics.DrawObserver = previous;
    }
}
#endif
