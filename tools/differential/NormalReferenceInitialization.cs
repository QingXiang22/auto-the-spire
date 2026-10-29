#if REFERENCE_SIM
using System.Collections;
using System.Globalization;
using System.Reflection;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Differential;

// Reconstruct diagnostic copies using this side's own source pools and actual draws.
// Check every final bag/queue against native state; never invoke a picker or RNG here.
internal sealed class NormalReferenceInitialization
{
    private readonly ActDefinition[] acts;
    private readonly RunState run;
    private readonly NormalOpeningList[] bagSources;
    private readonly NormalOpeningList[] actualBags;
    private readonly string[] sharedAncients;
    internal object Sources => new
    {
        bags = bagSources, actual_bags = actualBags, shared_ancients = sharedAncients,
        acts = acts.Select(a => new { act = a.Index, events = a.EffectiveEventPool.Select(TypeId).ToArray(),
            bosses = a.BossEncounterCandidates.Select(e => EncounterId(e.IdEntry)).ToArray(), ancients = a.AncientPool.Select(TypeId).ToArray() }).ToArray()
    };
    internal NormalReferenceInitialization(ActDefinition[] acts, RunState run, Player player)
    {
        this.acts = acts; this.run = run;
        var source = SharedRelicPool.Instance.AllRelics.ToArray();
        RelicRarity[] bucketed = [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop];
        bagSources = source.GroupBy(r => r.Rarity).Select(g => new NormalOpeningList("shared-bag/" + g.Key, g.Select(r => NormalReferenceRun.Id(r.Id)).ToArray()))
            .Concat(source.Concat(player.Character.RelicPool.AllRelics).Where(r => bucketed.Contains(r.Rarity)).GroupBy(r => r.Rarity)
                .Select(g => new NormalOpeningList("player-bag/" + g.Key, g.Select(r => NormalReferenceRun.Id(r.Id)).ToArray()))).ToArray();
        var actual = new List<NormalOpeningList>();
        foreach (var (name, bag) in new[] { ("shared-bag", run.SharedRelicGrabBag!), ("player-bag", player.RelicGrabBag) })
            foreach (DictionaryEntry pair in (IDictionary)typeof(RelicGrabBag).GetField("_buckets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bag)!)
                actual.Add(new(name + "/" + pair.Key, ((IEnumerable<RelicModel>)pair.Value!).Select(r => NormalReferenceRun.Id(r.Id)).ToArray()));
        actualBags = actual.ToArray();
        var visited = (Dictionary<int, Type>)typeof(RunState).GetField("_visitedAncientTypes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!;
        if (visited.Count != 0 || acts.Length != 3) throw NormalInitializationTrace.Missing("unsupported native opening profile or previsited Ancient");
        sharedAncients = SharedAncientPool.All.Where(t => !visited.ContainsValue(t)).Select(TypeId).ToArray();
    }
    internal NormalInitializationCandidates Capture(NormalInitializationDraw[] draws, NormalWeightedCall[] weighted)
    {
        var index = 0; var shuffles = new List<NormalOpeningShuffle>(); var allocations = new List<NormalAncientAllocation>(); var picks = new List<NormalOpeningPick>();
        var weightedCursors = new List<long>(); var plans = Plans(run);
        NormalInitializationDraw Next(string operation, int arity)
        {
            if (index >= draws.Length) throw NormalInitializationTrace.Missing("missing native initialization draw");
            var draw = draws[index++];
            if (draw.Before != index - 1 || draw.After != index || draw.Operation != operation || draw.Arity != arity)
                throw NormalInitializationTrace.Missing("native initialization operation, order or cursor");
            return draw;
        }
        (NormalInitializationDraw Draw, int Selected) Integer(int max, int arity)
        {
            var draw = Next("NextInt", arity); var selected = int.Parse(draw.Result, CultureInfo.InvariantCulture);
            if (draw.Minimum != 0 || draw.Maximum != max || selected < 0 || selected >= max)
                throw NormalInitializationTrace.Missing("native initialization integer bounds or result");
            return (draw, selected);
        }
        List<string> Shuffle(string key, IEnumerable<string> source)
        {
            var list = source.ToList();
            for (var i = list.Count - 1; i > 0; i--)
            {
                var (draw, selected) = Integer(i + 1, 1);
                shuffles.Add(new(key, draw.Before, draw.After, selected, list.Take(i + 1).ToArray()));
                (list[i], list[selected]) = (list[selected], list[i]);
            }
            return list;
        }
        foreach (var source in bagSources)
        {
            var shuffled = Shuffle(source.Key, source.Ids);
            if (!actualBags.Single(b => b.Key == source.Key).Ids.SequenceEqual(shuffled)) throw NormalInitializationTrace.Missing("native initial relic bag differs from its actual draws");
        }
        var remaining = Shuffle("shared-ancient", sharedAncients); var subsets = new Dictionary<int, string[]> { [0] = [] };
        for (var act = 1; act < acts.Length; act++)
        {
            var (draw, take) = Integer(remaining.Count + 1, 1);
            allocations.Add(new(act, draw.Before, draw.After, take, remaining.ToArray()));
            subsets.Add(act, remaining.Take(take).ToArray()); remaining.RemoveRange(0, take);
        }
        foreach (var act in acts)
        {
            var events = Shuffle("event/" + act.Index, act.EffectiveEventPool.Select(TypeId));
            if (!events.SequenceEqual(plans.Where(p => p.Act == act.Index && p.Room == "event").OrderBy(p => p.Position).Select(p => p.Id)))
                throw NormalInitializationTrace.Missing("native event shuffle differs from actual generated events");
            while (index < draws.Length && draws[index].Operation == "NextDouble")
            {
                var draw = Next("NextDouble", 0);
                if (draw.Minimum != 0 || draw.Maximum != 0) throw NormalInitializationTrace.Missing("native weighted opening parameters");
                weightedCursors.Add(draw.Before);
            }
            foreach (var (room, candidates) in new[] { ("Boss", act.BossEncounterCandidates.Select(e => EncounterId(e.IdEntry)).ToArray()),
                ("ancient", act.AncientPool.Select(TypeId).Concat(subsets[act.Index]).ToArray()) })
            {
                var (draw, selected) = Integer(candidates.Length, 2);
                picks.Add(new(act.Index, room, draw.Before, draw.After, selected, candidates));
            }
        }
        if (index != draws.Length || !weightedCursors.SequenceEqual(weighted.Select(c => c.Before)))
            throw NormalInitializationTrace.Missing("unclassified or omitted native initialization draws");
        NormalInitializationTrace.ValidatePicks(picks, plans);
        return new(shuffles.ToArray(), allocations.ToArray(), picks.ToArray());
    }
    internal static NormalOpeningQueueEntry[] Plans(RunState run)
    {
        var generated = (Array)typeof(RunState).GetField("_generatedRooms", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(run)!;
        var result = new List<NormalOpeningQueueEntry>();
        for (var act = 0; act < generated.Length; act++)
        {
            var state = generated.GetValue(act)!;
            object Get(string property) => state.GetType().GetProperty(property)!.GetValue(state)!;
            result.AddRange(((IEnumerable<Type>)Get("Events")).Select((t, index) => new NormalOpeningQueueEntry(act, "event", index, TypeId(t))));
            result.Add(new(act, "Boss", 0, EncounterId(((EncounterDefinition)Get("Boss")).IdEntry)));
            result.Add(new(act, "ancient", 0, TypeId((Type)Get("Ancient"))));
        }
        return result.ToArray();
    }
    private static string EncounterId(string entry) => "base-spire:" + entry.ToLowerInvariant().Replace('_', '-');
    private static string TypeId(Type type) => NormalReferenceRun.Id(ModelDb.GetId(type));
}
#endif
