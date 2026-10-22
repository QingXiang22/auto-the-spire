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

internal static class SapphireSeedVector
{
    internal static async Task<object> Capture(JsonElement referenceLock, Dictionary<string, string> hashes)
    {
        var branches = new List<object>();
        var randomJson = new JsonSerializerOptions { IncludeFields = true };
        foreach (var eat in new[] { true, false })
        {
            var run = new RunState("sapphire-normal-79", [new Overgrowth(), new Hive(), new Glory()]);
            var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
            run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
            var opening = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(opening); await opening.Enter(run);
            var neow = (Neow)opening.Event; await neow.ChooseOption(neow.CurrentOptions.Single(o => o.Key == "BoomingConch"));
            await opening.Exit(run); run.PopCurrentRoom();
            var first = run.CurrentMapPoint!.Children.Single(n => n.coord.col == 0 && n.coord.row == 1);
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
            if (!combat.Engine.Won) throw new InvalidOperationException("Native Sapphire path did not win its real first combat.");
            await combat.ResolveOutcomeAsync();
            await combat.Rewards!.Gold.Skip(); await combat.Rewards.Card.Skip(); if (combat.Rewards.Potion is { } potion) await potion.Skip();
            await combat.Exit(run); run.PopCurrentRoom();
            var next = run.CurrentMapPoint!.Children.Single(n => n.coord.col == 0 && n.coord.row == 2);
            run.AddVisitedMapCoord(next.coord); var roomType = RoomFactory.ResolveRoomType(run, next);
            if (roomType != RoomType.Event) throw new InvalidOperationException("Native Sapphire path must naturally roll Event.");
            var room = (EventRoom)RoomFactory.CreateRoom(run, roomType); run.PushRoom(room); await room.Enter(run);
            if (room.Event is not SapphireSeed) throw new InvalidOperationException("Native Sapphire path selected another event.");
            var beforeHp = player.Creature.CurrentHp;
            var candidates = player.Deck.Cards.Select((c, i) => (c, i)).Where(x => x.c.IsUpgradable).Select(x => x.i).ToArray();
            var randomBefore = JsonSerializer.Serialize(new { run = run.Rng.ToSerializable(), player = player.PlayerRng.ToSerializable() }, randomJson);
            await room.Event.ChooseOption(room.Event.CurrentOptions.Single(o => o.Key == (eat ? "EAT" : "PLANT")));
            if (!room.Event.IsFinished) throw new InvalidOperationException("Native Sapphire branch did not finish.");
            await room.Exit(run); run.PopCurrentRoom();
            var random = run.Rng.UnknownMapPoint.ToSerializable();
            branches.Add(new
            {
                branch = eat ? "eat" : "plant", before_hp = beforeHp, hp = player.Creature.CurrentHp, gold = player.Gold, total_floor = run.TotalFloor,
                cursor = (int)typeof(RunState).GetField("_eventsVisited", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(run)!,
                upgrade_candidates = eat ? candidates : [], selected_index = eat ? candidates.Last() : 0,
                deck = player.Deck.Cards.Select(c => new { id = Id(c.Id), upgraded = c.IsUpgraded, enchantments = c.Enchantments.Select(e => new { id = Id(e.Id), amount = e.Magnitude }).ToArray() }).ToArray(),
                successors = next.Children.Select(n => new[] { n.coord.col, n.coord.row }).OrderBy(n => n[0]).ThenBy(n => n[1]).ToArray(),
                random_unchanged = randomBefore == JsonSerializer.Serialize(new { run = run.Rng.ToSerializable(), player = player.PlayerRng.ToSerializable() }, randomJson),
                random = new { random.counter, random.state0, random.state1, random.state2, random.state3 }
            });
        }
        return new { format = "normal-sapphire-seed-vector@1", reference_lock = referenceLock, source_hash_encoding = "utf8-lf", source_sha256 = hashes,
            seed = "sapphire-normal-79", neow_relic = "base-spire:booming-conch", first = new[] { 0, 1 }, unknown = new[] { 0, 2 }, branches };
    }
    private static string Id(ModelId id) => "base-spire:" + id.Entry.ToLowerInvariant().Replace('_', '-');
}
