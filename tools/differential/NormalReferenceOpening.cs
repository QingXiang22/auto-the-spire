#if REFERENCE_SIM
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class NormalReferenceOpening
{
    public static NormalPreflight.Opening Capture(string seed)
    {
#if REFERENCE_SIM
        RunReplayDemo.EnsureModelsRegistered();
        // Native sequential constructors only. No controlled fixture, reseeding, injected encounter,
        // rewards, or changes to the locked reference. Match RunDriver's real Ancient entry order.
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()]);
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var room = (EventRoom)RoomFactory.CreateAncientEventRoom(run); run.PushRoom(room);
        room.Enter(run).GetAwaiter().GetResult();
        if (room.Event is not Neow neow || neow.CurrentOptions.Count != 3)
            throw new InvalidOperationException("Native reference did not reach its normal Neow decision.");
        string Id(ModelId id) => "base-spire:" + id.Entry.ToLowerInvariant().Replace('_', '-');
        string TypeId(string name) => Id(ModelDb.GetId(typeof(Neow).Assembly.GetType("Sts2Sim.Core.Models.Relics." + name)
            ?? throw new InvalidOperationException("Unknown native Neow relic option: " + name)));
        var seen = Enumerable.Range(0, run.Map.GetRowCount()).SelectMany(run.Map.GetPointsInRow)
            .Append(run.Map.StartingMapPoint).Append(run.Map.BossMapPoint).Distinct().ToArray();
        string Coord(Sts2Sim.Core.Map.MapPoint node) => node.coord.col + "," + node.coord.row;
        var facts = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["player.hp"] = player.Creature.CurrentHp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["player.max_hp"] = player.Creature.MaxHp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["player.gold"] = player.Gold.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["deck"] = NormalPreflight.Json(player.Deck.Cards.Select(c => Id(c.Id)).Order(StringComparer.Ordinal)),
            ["relics"] = NormalPreflight.Json(player.Relics.Select(r => Id(r.Id)).Order(StringComparer.Ordinal)),
            ["neow.options"] = NormalPreflight.Json(neow.CurrentOptions.Select(o => TypeId(o.Key))),
            ["map"] = NormalPreflight.Json(seen.OrderBy(Coord, StringComparer.Ordinal).Select(n => new
            {
                coordinate = Coord(n), type = n.PointType.ToString(),
                children = n.Children.Select(Coord).Order(StringComparer.Ordinal).ToArray()
            }))
        };
        return new("reference:sts2-sim", NormalPreflight.Hash(typeof(RunState).Assembly.Location), facts,
            new { raw_seed = seed, native_root_seed = run.Rng.Seed, source = "native sequential RunState/Player/RoomFactory/AbstractRoom.Enter",
                up_front_draws = run.Rng.UpFront.Counter, possible_neow_relics = neow.AllPossibleOptions.Select(r => Id(r.Id)).ToArray(),
                normal_compatibility_alignment = false });
#else
        throw new NotSupportedException("unsupported_reference_adapter: build with the clean locked reference checkout.");
#endif
    }
}
