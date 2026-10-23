using System.Text.Json;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal static class NeowEffectVector
{
    internal static async Task<object> Capture(JsonElement referenceLock, Dictionary<string, string> hashes)
    {
        var openings = new List<object>();
        foreach (var choice in new[] { "WingedBoots", "CursedPearl" })
        {
            var run = new RunState("777", [new Overgrowth(), new Hive(), new Glory()]);
            var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
            run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
            var room = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(room); await room.Enter(run);
            var options = room.Event.CurrentOptions.Select(o => o.Key).ToArray();
            object State() => new { hp = player.Creature.CurrentHp, gold = player.Gold,
                deck = player.Deck.Cards.Select(c => new { id = Id(c.Id), upgraded = c.IsUpgraded, upgradable = c.IsUpgradable,
                    cost = c.CanonicalEnergyCostValue, keywords = c.Keywords.Select(k => k.ToString().ToLowerInvariant()).Order().ToArray() }).ToArray(),
                hand = player.PlayerCombatState?.Hand.Cards.Select(c => Id(c.Id)).ToArray(),
                rng = new { up_front = run.Rng.UpFront.ToSerializable(), niche = run.Rng.Niche.ToSerializable(), shuffle = run.Rng.Shuffle.ToSerializable(), rewards = player.PlayerRng.Rewards.ToSerializable(), neow = room.Event.Rng.ToSerializable() } };
            await room.Event.ChooseOption(room.Event.CurrentOptions.Single(o => o.Key == choice));
            var obtained = State(); await room.Exit(run); run.PopCurrentRoom();
            var legal = MapTravel.GetTravelablePointsFrom(run, run.CurrentMapPoint!).OrderBy(p => p.coord.col).ToArray();
            var first = legal.First(); run.AddVisitedMapCoord(first.coord);
            var combat = (CombatRoom)RoomFactory.CreateRoom(run, RoomFactory.ResolveRoomType(run, first)); run.PushRoom(combat); await combat.Enter(run);
            openings.Add(new { choice, seed = "777", options, obtained, first = Coord(first), legal = legal.Select(Coord).ToArray(), started = State() });
        }
        var travelRun = new RunState("777", new Overgrowth());
        var traveler = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), travelRun); travelRun.AddPlayer(traveler);
        await RelicCmd.Obtain(ModelDb.Relic<WingedBoots>(), traveler);
        var boots = traveler.Relics.OfType<WingedBoots>().Single();
        travelRun.AddVisitedMapCoord(travelRun.Map.StartingMapPoint.coord);
        var travel = new List<object>();
        for (var i = 0; i < 10; i++)
        {
            var previous = travelRun.CurrentMapPoint!;
            var legal = MapTravel.GetTravelablePointsFrom(travelRun, previous).OrderBy(p => p.coord.col).ToArray();
            var next = legal.FirstOrDefault(p => !previous.Children.Contains(p)) ?? legal.First();
            travelRun.AddVisitedMapCoord(next.coord);
            var room = new RestSiteRoom(); travelRun.PushRoom(room); await room.Enter(travelRun);
            travel.Add(new { previous = Coord(previous), legal = legal.Select(Coord).ToArray(), next = Coord(next), used = boots.TimesUsed, free = boots.ShouldAllowFreeTravel(), after = MapTravel.GetTravelablePointsFrom(travelRun, next).Select(Coord).OrderBy(c => c[0]).ToArray() });
            await room.Exit(travelRun); travelRun.PopCurrentRoom();
            if (boots.IsUsedUp) break;
        }
        if (boots.TimesUsed != 3) throw new InvalidOperationException("Travel component must exercise all three source charges.");
        var flex = new List<object>();
        foreach (var seed in new[] { "777", "effects-component" })
        foreach (var baseline in new[] { -3, 2 })
        {
            var run = new RunState(seed, new Overgrowth());
            var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
            var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<BattleFriendV2>().MutableClone()); run.PushRoom(room); await room.Enter(run);
            await PowerCmd.Apply<StrengthPower>(room.Engine.State, player.Creature, baseline, null, null);
            object State() => new { powers = player.Creature.Powers.Select(p => new { id = Id(p.Id), amount = p.Amount }).ToArray(), target_hp = room.Engine.State.SpawnedEnemies.Single().CurrentHp,
                potion_count = player.PotionSlots.Count(p => p is not null), shuffle = run.Rng.Shuffle.ToSerializable(), rewards = player.PlayerRng.Rewards.ToSerializable() };
            var steps = new List<object>();
            for (var n = 0; n < 2; n++)
            {
                var potion = (PotionModel)ModelDb.Potion<FlexPotion>().MutableClone();
                if (!await PotionCmd.TryToProcure(potion, player)) throw new InvalidOperationException("Controlled Flex slot must be available.");
                await PotionCmd.Use(potion, player, player.Creature); steps.Add(State());
            }
            await PowerCmd.ModifyAmount(room.Engine.State, player.Creature.Powers.OfType<FlexPotionPower>().Single(), -3, null, null);
            steps.Add(State());
            var attack = player.PlayerCombatState!.Hand.Cards.First(c => c.Type == CardType.Attack && c.CanPlay(out _));
            await room.Engine.PlayCardAsync(player, attack, room.Engine.State.SpawnedEnemies.Single()); steps.Add(State());
            await room.Engine.EndPlayerTurnAsync(); steps.Add(State());
            flex.Add(new { seed, baseline, attack = Id(attack.Id), steps });
        }
        return JsonSerializer.SerializeToElement(new { format = "neow-effects-vector@1", reference_lock = referenceLock, source_hash_encoding = "utf8-lf", source_sha256 = hashes,
            controlled_setup = "Openings are real seed777 Neow and first combat. Travel uses the real generated map and actual RestSiteRoom entry hooks without choosing a rest effect. Flex uses a direct real BattleFriendV2 combat, controlled initial Strength and direct real potion procurement; no normal-map claim for these components.", openings, travel, flex },
            new JsonSerializerOptions { IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
    }
    private static int[] Coord(MapPoint p) => [p.coord.col, p.coord.row];
    private static string Id(ModelId id) => "base-spire:" + id.Entry.ToLowerInvariant().Replace('_', '-');
}
