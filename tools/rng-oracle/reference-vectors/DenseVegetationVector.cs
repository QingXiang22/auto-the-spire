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

internal static class DenseVegetationVector
{
    internal static async Task<object> Capture(JsonElement referenceLock, Dictionary<string, string> hashes)
    {
        var branches = new List<object>();
        foreach (var branch in new[] { "trudge", "fight-skip", "fight-claim" })
        {
            var run = new RunState("dense-normal-30", [new Overgrowth(), new Hive(), new Glory()]);
            var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
            object Random(EventModel? e = null) => new { run = run.Rng.ToSerializable(), player = player.PlayerRng.ToSerializable(), event_rng = e?.Rng.ToSerializable() };
            object Snapshot(EventModel e) => new { hp = player.Creature.CurrentHp, gold = player.Gold, finished = e.IsFinished, awaiting = e.IsAwaitingForcedCombat, options = e.CurrentOptions.Select(o => o.Key).ToArray(), random = Random(e) };
            run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
            var opening = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(opening); await opening.Enter(run);
            await opening.Event.ChooseOption(opening.Event.CurrentOptions.Single(o => o.Key == "BoomingConch"));
            await opening.Exit(run); run.PopCurrentRoom();
            async Task Fight(CombatRoom combat)
            {
                for (var step = 0; combat.Engine.IsInProgress && step < 200; step++)
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
                if (!combat.Engine.Won) throw new InvalidOperationException("Native Dense path did not win its real combat.");
            }
            var first = run.CurrentMapPoint!.Children.Single(n => n.coord.col == 1 && n.coord.row == 1);
            run.AddVisitedMapCoord(first.coord);
            var firstCombat = (CombatRoom)RoomFactory.CreateRoom(run, RoomFactory.ResolveRoomType(run, first)); run.PushRoom(firstCombat); await firstCombat.Enter(run);
            await Fight(firstCombat); await firstCombat.ResolveOutcomeAsync();
            await firstCombat.Rewards!.Gold.Skip(); await firstCombat.Rewards.Card.Skip(); if (firstCombat.Rewards.Potion is { } potion) await potion.Skip();
            await firstCombat.Exit(run); run.PopCurrentRoom();
            var next = run.CurrentMapPoint!.Children.Single(n => n.coord.col == 0 && n.coord.row == 2);
            run.AddVisitedMapCoord(next.coord); var roomType = RoomFactory.ResolveRoomType(run, next);
            if (roomType != RoomType.Event) throw new InvalidOperationException("Native Dense path must naturally roll Event.");
            var room = (EventRoom)RoomFactory.CreateRoom(run, roomType); run.PushRoom(room); await room.Enter(run);
            if (room.Event is not DenseVegetation) throw new InvalidOperationException("Native Dense path selected another event.");
            var entered = Snapshot(room.Event);
            await room.Event.ChooseOption(room.Event.CurrentOptions.Single(o => o.Key == (branch == "trudge" ? "TRUDGE_ON" : "REST")));
            var selected = Snapshot(room.Event); object? combatStart = null; object? reward = null;
            if (branch != "trudge")
            {
                await room.Event.ChooseOption(room.Event.CurrentOptions.Single(o => o.Key == "FIGHT"));
                var child = (CombatRoom)typeof(EventRoom).GetMethod("CreatePendingForcedCombatRoom", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(room, null)!;
                run.PushRoom(child); await child.Enter(run);
                combatStart = new { state = Snapshot(room.Event), monsters = child.Engine.State.SpawnedEnemies.Select(e => new { id = Id(e.Monster!.Id), hp = e.MaxHp, move = e.Monster.NextMove!.Id }).ToArray() };
                await Fight(child); await child.ResolveOutcomeAsync();
                var rewards = child.Rewards!;
                reward = new { state = Snapshot(room.Event), gold = rewards.Gold.Amount, cards = rewards.Card.Options.Select(c => new { id = Id(c.Id), upgraded = c.IsUpgraded }).ToArray(), potion = rewards.Potion?.Potion is { } p ? Id(p.Id) : null };
                if (branch == "fight-claim")
                {
                    await rewards.Gold.Take(); await rewards.Card.SelectOption(rewards.Card.Options[0]); if (rewards.Potion is { } pot) await pot.Take();
                }
                else { await rewards.Gold.Skip(); await rewards.Card.Skip(); if (rewards.Potion is { } pot) await pot.Skip(); }
                await child.Exit(run); run.PopCurrentRoom();
            }
            await room.Exit(run); run.PopCurrentRoom();
            branches.Add(new { branch, entered, selected, combat_start = combatStart, reward, final = Snapshot(room.Event), total_floor = run.TotalFloor, cursor = (int)typeof(RunState).GetField("_eventsVisited", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(run)!,
                deck = player.Deck.Cards.Select(c => new { id = Id(c.Id), upgraded = c.IsUpgraded }).ToArray(),
                successors = next.Children.Select(n => new[] { n.coord.col, n.coord.row }).OrderBy(n => n[0]).ThenBy(n => n[1]).ToArray() });
        }
        // SerializableRng stores fields; freeze them before the outer writer serializes this value.
        return JsonSerializer.SerializeToElement(new { format = "normal-dense-vegetation-vector@1", reference_lock = referenceLock, source_hash_encoding = "utf8-lf", source_sha256 = hashes,
            seed = "dense-normal-30", neow_relic = "base-spire:booming-conch", first = new[] { 1, 1 }, unknown = new[] { 0, 2 }, branches }, new JsonSerializerOptions { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    }
    private static string Id(ModelId id) => "base-spire:" + id.Entry.ToLowerInvariant().Replace('_', '-');
}
