using System.Collections;
using System.Reflection;
using System.Text.Json;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal static class EventSelectionVector
{
    internal static async Task<object> Capture(JsonElement referenceLock, Dictionary<string, string> hashes)
    {
        const string seed = "event-normal-11";
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()]);
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var opening = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(opening); await opening.Enter(run);
        var neow = (Neow)opening.Event; await neow.ChooseOption(neow.CurrentOptions.Single(o => o.Key == "BoomingConch"));
        await opening.Exit(run); run.PopCurrentRoom();
        var first = run.CurrentMapPoint!.Children.Single(n => n.coord.col == 2 && n.coord.row == 1);
        run.AddVisitedMapCoord(first.coord);
        var combat = (CombatRoom)RoomFactory.CreateRoom(run, RoomFactory.ResolveRoomType(run, first)); run.PushRoom(combat); await combat.Enter(run);
        for (var actions = 0; combat.Engine.IsInProgress && actions < 100; actions++)
        {
            var cards = player.PlayerCombatState!.Hand.Cards.Where(c => c.CanPlay(out _)).ToArray();
            var card = cards.FirstOrDefault(c => c.Type == CardType.Attack) ?? cards.FirstOrDefault();
            if (card is null) await combat.Engine.EndPlayerTurnAsync();
            else
            {
                await combat.Engine.PlayCardAsync(player, card, card.TargetType == TargetType.AnyEnemy ? combat.Engine.State.Enemies.First(e => e.IsAlive) : null);
                combat.Engine.CheckWinCondition();
            }
        }
        if (!combat.Engine.Won) throw new InvalidOperationException("Native Event path did not win its real first combat.");
        await combat.ResolveOutcomeAsync();
        await combat.Rewards!.Gold.Skip(); await combat.Rewards.Card.Skip(); if (combat.Rewards.Potion is { } potion) await potion.Skip();
        await combat.Exit(run); run.PopCurrentRoom();
        var next = run.CurrentMapPoint!.Children.Single(n => n.coord.col == 1 && n.coord.row == 2);
        var floorBefore = run.TotalFloor;
        run.AddVisitedMapCoord(next.coord); var actual = RoomFactory.ResolveRoomType(run, next);
        if (actual != RoomType.Event) throw new InvalidOperationException("Native first Unknown no longer resolves to Event.");
        // CreateRoom invokes the real pre-history selector. Do not enter or claim migrated event effects.
        RoomFactory.CreateRoom(run, actual);
        object Private(string name) => typeof(RunState).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(run)!;
        string Id(ModelId id) => "base-spire:" + id.Entry.ToLowerInvariant().Replace('_', '-');
        var visited = ((IEnumerable<ModelId>)Private("_visitedEventIds")).Select(Id).Order(StringComparer.Ordinal).ToArray();
        var random = run.Rng.UnknownMapPoint.ToSerializable();
        return new
        {
            format = "normal-event-selection-vector@1", reference_lock = referenceLock, source_hash_encoding = "utf8-lf", source_sha256 = hashes,
            seed, neow_relic = "base-spire:booming-conch", first = new[] { 2, 1 }, unknown = new[] { 1, 2 },
            room = actual.ToString().ToLowerInvariant(), selected = visited.Single(), cursor = (int)Private("_eventsVisited"), visited,
            hp = player.Creature.CurrentHp, gold = player.Gold, selection_floor = floorBefore,
            odds = new[] { run.Odds.UnknownMapPoint.MonsterOdds, run.Odds.UnknownMapPoint.EliteOdds, run.Odds.UnknownMapPoint.TreasureOdds, run.Odds.UnknownMapPoint.ShopOdds }.Select(BitConverter.SingleToUInt32Bits).ToArray(),
            random = new { random.counter, random.state0, random.state1, random.state2, random.state3 }
        };
    }
}
