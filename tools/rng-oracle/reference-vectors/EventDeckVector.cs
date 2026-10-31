using System.Text.Json;
using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal static class EventDeckVector
{
    internal static async Task<object> Capture(JsonElement referenceLock, Dictionary<string, string> hashes)
    {
        var rows = new List<object>();
        foreach (var morphic in new[] { false, true })
        foreach (var firstOption in new[] { true, false })
        {
            var seed = morphic ? "morphic-normal-14" : "event-normal-11";
            var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()]);
            var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
            run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
            var opening = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(opening); await opening.Enter(run);
            var neow = (Neow)opening.Event; await neow.ChooseOption(neow.CurrentOptions.Single(o => o.Key == "BoomingConch"));
            await opening.Exit(run); run.PopCurrentRoom();
            var first = run.CurrentMapPoint!.Children.Single(n => n.coord.col == (morphic ? 4 : 2) && n.coord.row == 1);
            run.AddVisitedMapCoord(first.coord);
            var combat = (CombatRoom)RoomFactory.CreateRoom(run, RoomFactory.ResolveRoomType(run, first)); run.PushRoom(combat); await combat.Enter(run);
            for (var action = 0; combat.Engine.IsInProgress && action < 100; action++)
            {
                var cards = player.PlayerCombatState!.Hand.Cards.Where(c => c.CanPlay(out _)).ToArray();
                var card = cards.FirstOrDefault(c => c.Type == CardType.Attack) ?? cards.FirstOrDefault();
                if (card is null) await combat.Engine.EndPlayerTurnAsync();
                else { await combat.Engine.PlayCardAsync(player, card, card.TargetType == TargetType.AnyEnemy ? combat.Engine.State.Enemies.First(e => e.IsAlive) : null); combat.Engine.CheckWinCondition(); }
            }
            if (!combat.Engine.Won) throw new InvalidOperationException("Registered event-deck normal prefix did not win.");
            await combat.ResolveOutcomeAsync();
            if (morphic) await combat.Rewards!.Gold.Take(); else await combat.Rewards!.Gold.Skip();
            await combat.Rewards.Card.Skip(); if (combat.Rewards.Potion is {} potion) await potion.Skip();
            await combat.Exit(run); run.PopCurrentRoom();
            var node = run.CurrentMapPoint!.Children.Single(n => n.coord.col == (morphic ? 3 : 1) && n.coord.row == 2);
            run.AddVisitedMapCoord(node.coord); var roomType = RoomFactory.ResolveRoomType(run, node);
            if (roomType != RoomType.Event) throw new InvalidOperationException("Registered normal prefix no longer naturally rolls Event.");
            var room = (EventRoom)RoomFactory.CreateRoom(run, roomType); run.PushRoom(room); await room.Enter(run);
            if (morphic ? room.Event is not MorphicGrove : room.Event is not AromaOfChaos)
                throw new InvalidOperationException("Registered normal prefix selected another event.");
            var deck = player.Deck.Cards.ToArray(); var selector = new Selection(deck, morphic, firstOption);
            typeof(RunState).GetMethod("ConfigureCardSelectionSource", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(run, [selector]);
            object Card(CardModel c) => new { original_index = Array.IndexOf(deck, c), id = Id(c), upgraded = c.IsUpgraded };
            object RngState() { var s = room.Event.Rng.ToSerializable(); return new { s.counter, s.state0, s.state1, s.state2, s.state3 }; }
            var before = new { hp = player.Creature.CurrentHp, max_hp = player.Creature.MaxHp, gold = player.Gold,
                total_floor = run.TotalFloor, deck = deck.Select(Card).ToArray(), event_rng = RngState() };
            string OtherRandom() => JsonSerializer.Serialize(new { run = run.Rng.ToSerializable(), player = player.PlayerRng.ToSerializable() }, new JsonSerializerOptions { IncludeFields = true });
            var otherRandom = OtherRandom();
            var branch = morphic ? firstOption ? "GROUP" : "LONER" : firstOption ? "LET_GO" : "MAINTAIN_CONTROL";
            await room.Event.ChooseOption(room.Event.CurrentOptions.Single(o => o.Key == branch));
            if (!room.Event.IsFinished) throw new InvalidOperationException("Registered event branch did not finish.");
            var after = new { hp = player.Creature.CurrentHp, max_hp = player.Creature.MaxHp, gold = player.Gold,
                deck = player.Deck.Cards.Select(Card).ToArray(), event_rng = RngState(), other_random_unchanged = otherRandom == OtherRandom() };
            await room.Exit(run); run.PopCurrentRoom();
            rows.Add(new { seed, definition = Id(room.Event), branch,
                event_seed = unchecked(run.Rng.Seed + StringHelper.GetDeterministicHashCode(room.Event.Id.Entry)),
                first = new[] { first.coord.col, first.coord.row },
                unknown = new[] { node.coord.col, node.coord.row }, claim_gold = morphic, before, selections = selector.Requests, after,
                successors = node.Children.Select(n => new[] { n.coord.col, n.coord.row }).OrderBy(c => c[0]).ThenBy(c => c[1]).ToArray() });
        }
        return new { format = "normal-event-deck-vector@1", reference_lock = referenceLock, source_hash_encoding = "utf8-lf", source_sha256 = hashes, branches = rows };
    }
    private static string Id(AbstractModel model) => "base-spire:" + model.Id.Entry.ToLowerInvariant().Replace('_', '-');
    private sealed class Selection(CardModel[] deck, bool morphic, bool transform) : ICardSelectionDecisionSource
    {
        internal List<object> Requests { get; } = [];
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            // A real source choice, frozen before the DSL implementation. The
            // ordered pair deliberately tests two-card order and append semantics.
            CardModel[] selected = morphic ? [request.Candidates[^1], request.Candidates[0]] : [request.Candidates[0]];
            Requests.Add(new { min = request.MinCount, max = request.MaxCount, gold = request.Player.Gold,
                candidates = request.Candidates.Select(c => Array.IndexOf(deck, c)).ToArray(), selected = selected.Select(c => Array.IndexOf(deck, c)).ToArray(),
                pools = selected.Where(_ => transform)
                    .Select(c => new { original = Array.IndexOf(deck, c), ids = CardFactory.GetDefaultTransformationOptions(c, false).Select(Id).ToArray() }).ToArray() });
            return Task.FromResult<IReadOnlyList<CardModel>>(selected);
        }
    }
}
