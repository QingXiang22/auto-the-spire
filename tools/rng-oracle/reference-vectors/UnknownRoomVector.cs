using System.Text.Json;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal static class UnknownRoomVector
{
    internal static async Task<object> Capture(JsonElement referenceLock, Dictionary<string, string> hashes)
    {
        // An additional native normal path. No controlled setup, injected room, reseed or compensation draw.
        const string seed = "unknown-normal-354";
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()]);
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var opening = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(opening); await opening.Enter(run);
        var neow = (Neow)opening.Event; await neow.ChooseOption(neow.CurrentOptions.Single(o => o.Key == "GoldenPearl"));
        await opening.Exit(run); run.PopCurrentRoom();
        var first = run.CurrentMapPoint!.Children.Single(n => n.coord.col == 3 && n.coord.row == 1);
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
        if (!combat.Engine.Won) throw new InvalidOperationException("Native Unknown path did not win its real first combat.");
        await combat.ResolveOutcomeAsync();
        await combat.Rewards!.Gold.Skip(); await combat.Rewards.Card.Skip(); if (combat.Rewards.Potion is { } potion) await potion.Skip();
        await combat.Exit(run); run.PopCurrentRoom();
        var next = run.CurrentMapPoint!.Children.Single(n => n.coord.col == 3 && n.coord.row == 2);
        run.AddVisitedMapCoord(next.coord); var actual = RoomFactory.ResolveRoomType(run, next);
        if (actual != RoomType.Monster) throw new InvalidOperationException("Native first Unknown no longer resolves to Monster.");
        var second = (CombatRoom)RoomFactory.CreateRoom(run, actual); run.PushRoom(second); await second.Enter(run);
        string Id(string entry) => "base-spire:" + entry.ToLowerInvariant().Replace('_', '-');
        var random = run.Rng.UnknownMapPoint.ToSerializable();
        return new
        {
            format = "normal-unknown-room-vector@1", reference_lock = referenceLock, source_hash_encoding = "utf8-lf", source_sha256 = hashes,
            seed, neow_relic = "base-spire:golden-pearl", first = new[] { 3, 1 }, unknown = new[] { 3, 2 },
            room = actual.ToString().ToLowerInvariant(), encounter = Id(second.Encounter!.IdEntry), hp = player.Creature.CurrentHp, gold = player.Gold, total_floor = run.TotalFloor,
            monsters = second.Engine.State.SpawnedEnemies.Select(e => new { id = Id(e.Monster!.Id.Entry), hp = e.CurrentHp, move = e.Monster.NextMove!.Id }).ToArray(),
            odds = new[] { run.Odds.UnknownMapPoint.MonsterOdds, run.Odds.UnknownMapPoint.EliteOdds, run.Odds.UnknownMapPoint.TreasureOdds, run.Odds.UnknownMapPoint.ShopOdds }.Select(BitConverter.SingleToUInt32Bits).ToArray(),
            random = new { random.counter, random.state0, random.state1, random.state2, random.state3 }
        };
    }
}
