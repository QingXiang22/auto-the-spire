using System.Globalization;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using System.Reflection;
using Sts2Sim.Core.Content;
#endif

namespace Differential;

internal sealed record NormalWeightedEntry(long Act, string Room, bool Weak, string Id, long WeightBits);
internal sealed record NormalWeightedSnapshot(NormalWeightedEntry[] Entries, long TotalBits);
internal sealed record NormalWeightedCall(string Stream, long Before, long After, NormalWeightedSnapshot Candidates, int Selected);

internal static class NormalWeightedTrace
{
    internal static NormalWeightedCall[] Dsl(IReadOnlyList<SessionDebugRecord> records, long dropped)
    {
        if (dropped != 0) throw Missing("dropped weighted candidate records");
        var calls = new List<NormalWeightedCall>();
        foreach (var record in records.Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == "base-spire:run:up_front" && r.Random?.Operation == "pick"))
        {
            if (record.TextTruncated || record.Random is not { Candidates: { } roots, CandidatesTruncated: false } draw
                || record.Snapshot is not { Complete: true, ReferenceDepth: >= 1 } snapshot || roots.Count != draw.CandidateCount || roots.Count == 0)
                throw Missing("incomplete opening candidate definitions");
            var entities = snapshot.Entities.ToDictionary(e => e.Instance.Value);
            var items = roots.Select(id => entities.TryGetValue(id.Value, out var entity) ? entity : throw Missing("missing historical candidate root")).ToArray();
            if (items.All(e => e.Type == "base-spire:OpeningDefinition")) continue; // Direct boss/Ancient picks have their own remaining mapping.
            if (items.Any(e => e.Type != "base-spire:OpeningListItem")) throw Missing("unclassified weighted candidate type");
            long previous = -1;
            var entries = items.Select(item =>
            {
                var position = long.Parse(Scalar(item, "Position"), CultureInfo.InvariantCulture);
                if (position <= previous) throw Missing("unordered historical weighted candidates");
                previous = position;
                var reference = item.Fields.Single(f => f.Name == "Definition").Value;
                if (reference.Kind != ValueKind.Ref || !entities.TryGetValue(int.Parse(reference.Scalar!, CultureInfo.InvariantCulture), out var definition)
                    || definition.Type != "base-spire:OpeningDefinition" || Scalar(definition, "Category") != "encounter")
                    throw Missing("historical weighted definition reference");
                return new NormalWeightedEntry(long.Parse(Scalar(definition, "Act"), CultureInfo.InvariantCulture), Scalar(definition, "Room"),
                    bool.Parse(Scalar(definition, "Weak")), Scalar(definition, "DefinitionId"), BitConverter.DoubleToInt64Bits(1d));
            }).ToArray();
            if (draw.Minimum != "0" || draw.Maximum != roots.Count.ToString(CultureInfo.InvariantCulture)) throw Missing("weighted discrete bounds");
            calls.Add(Call(record.Subject!, draw.Before, draw.After, new(entries, BitConverter.DoubleToInt64Bits(entries.Length)),
                int.Parse(draw.Result, CultureInfo.InvariantCulture)));
        }
        return Canonical(calls);
    }

    internal static NormalWeightedCall FromUnit(string stream, long before, long after, NormalWeightedSnapshot candidates, double unit)
    {
        if (!double.IsFinite(unit) || unit < 0 || unit >= 1) throw Missing("invalid native weighted unit result");
        var roll = unit * BitConverter.Int64BitsToDouble(candidates.TotalBits);
        double cumulative = 0;
        for (var i = 0; i < candidates.Entries.Length; i++)
        {
            cumulative += BitConverter.Int64BitsToDouble(candidates.Entries[i].WeightBits);
            if (roll < cumulative) return Call(stream, before, after, candidates, i);
        }
        throw Missing("native weighted result outside actual candidates");
    }

    private static NormalWeightedCall Call(string stream, long before, long after, NormalWeightedSnapshot candidates, int selected)
    {
        if (selected < 0 || selected >= candidates.Entries.Length || after != before + 1) throw Missing("weighted selected index or cursor");
        double total = 0;
        foreach (var entry in candidates.Entries)
        {
            var weight = BitConverter.Int64BitsToDouble(entry.WeightBits);
            if (!double.IsFinite(weight) || weight <= 0) throw Missing("invalid weighted candidate weight");
            total += weight;
        }
        if (BitConverter.DoubleToInt64Bits(total) != candidates.TotalBits) throw Missing("weighted total differs from copied entries");
        return new(stream, before, after, candidates, selected);
    }

    internal static NormalWeightedCall[] Canonical(IEnumerable<NormalWeightedCall> source)
    {
        var calls = source.ToArray(); long previous = -1;
        foreach (var call in calls)
        {
            Call(call.Stream, call.Before, call.After, call.Candidates, call.Selected);
            if (call.Stream != "base-spire:run:up_front" || call.Before <= previous) throw Missing("duplicate or reordered weighted cursor");
            previous = call.Before;
        }
        return calls;
    }

    private static string Scalar(SessionDebugEntitySnapshot entity, string name) => entity.Fields.Single(f => f.Name == name).Value is
        { Truncated: false, Scalar: { } scalar } ? scalar : throw Missing("historical weighted field " + name);
    internal static NotSupportedException Missing(string detail) => new("missing_dimension: " + detail);
}

#if REFERENCE_SIM
// Copy the actual live bag at the RNG callback. ActDefinition retains the same bag objects
// through synchronous upfront generation; a new pool creates exactly one new bag state.
internal sealed class NormalReferenceWeightedBags(ActDefinition[] acts)
{
    private readonly HashSet<object> seen = [];
    private object? active;
    private static readonly FieldInfo Entries = typeof(GrabBag<EncounterDefinition>).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Total = typeof(GrabBag<EncounterDefinition>).GetField("_totalWeight", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal NormalWeightedSnapshot Capture()
    {
        var bags = new List<(ActDefinition Act, string Room, object Bag)>();
        foreach (var act in acts)
            foreach (var field in new[] { "_weakMonsterEncounterState", "_regularMonsterEncounterState", "_eliteEncounterState" })
            {
                var state = typeof(ActDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(act);
                if (state is null) continue;
                bags.Add((act, field == "_eliteEncounterState" ? "Elite" : "Monster", state.GetType().GetProperty("Bag")!.GetValue(state)!));
            }
        var added = bags.Where(b => !seen.Contains(b.Bag)).ToArray();
        if (added.Length > 1 || added.Length == 0 && active is null) throw NormalWeightedTrace.Missing("ambiguous active native bag");
        if (added.Length == 1) { active = added[0].Bag; seen.Add(active); }
        var current = bags.Single(b => ReferenceEquals(b.Bag, active));
        var entries = ((IEnumerable<(EncounterDefinition Item, double Weight)>)Entries.GetValue(current.Bag)!).Select(e =>
            new NormalWeightedEntry(current.Act.Index, current.Room, e.Item.IsWeak,
                "base-spire:" + e.Item.IdEntry.ToLowerInvariant().Replace('_', '-'), BitConverter.DoubleToInt64Bits(e.Weight))).ToArray();
        if (entries.Length == 0) throw NormalWeightedTrace.Missing("empty active native bag");
        return new(entries, BitConverter.DoubleToInt64Bits((double)Total.GetValue(current.Bag)!));
    }
}
#endif
