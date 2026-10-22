using System.Globalization;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using System.Reflection;
using Sts2Sim.Core.Content;
#endif

namespace Differential;

internal sealed record NormalWeightedEntry(long Act, string Room, bool Weak, string Id, long WeightBits, string[] Tags);
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
                || record.Snapshot is not { Complete: true, ReferenceDepth: >= 1, RelationsCaptured: true } snapshot || roots.Count != draw.CandidateCount || roots.Count == 0)
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
                    bool.Parse(Scalar(definition, "Weak")), Scalar(definition, "DefinitionId"), BitConverter.DoubleToInt64Bits(1d),
                    snapshot.Relations.Where(r => r.Owner == definition.Instance && r.Relation == "base-spire:OpeningTags").SelectMany(r => r.Targets)
                        .Select(id => entities.TryGetValue(id.Value, out var tag) && tag.Type == "base-spire:OpeningTag" ? Scalar(tag, "Name")
                            : throw Missing("historical compatibility tag target")).Order(StringComparer.Ordinal).ToArray());
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
    private readonly List<NormalOpeningAttempt> attempts = [];
    private readonly List<object> commits = [];
    private NativeAttempt? pending;
    private sealed record NativeAttempt(ActDefinition Act, string Room, object State, object Bag,
        (EncounterDefinition Item, double Weight)[] Entries, EncounterDefinition[] FullPool, EncounterDefinition? LastPicked,
        EncounterDefinition? LastMonster, EncounterDefinition? Previous, long Before, NormalWeightedSnapshot Snapshot, int Selected = -1);
    private static readonly FieldInfo Entries = typeof(GrabBag<EncounterDefinition>).GetField("_entries", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo Total = typeof(GrabBag<EncounterDefinition>).GetField("_totalWeight", BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal NormalWeightedSnapshot Capture(long before)
    {
        if (pending is not null) throw NormalWeightedTrace.Missing("previous native opening attempt not completed");
        var bags = new List<(ActDefinition Act, string Room, object Bag, object State)>();
        foreach (var act in acts)
            foreach (var field in new[] { "_weakMonsterEncounterState", "_regularMonsterEncounterState", "_eliteEncounterState" })
            {
                var state = typeof(ActDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(act);
                if (state is null) continue;
                bags.Add((act, field == "_eliteEncounterState" ? "Elite" : "Monster", state.GetType().GetProperty("Bag")!.GetValue(state)!, state));
            }
        var added = bags.Where(b => !seen.Contains(b.Bag)).ToArray();
        if (added.Length > 1 || added.Length == 0 && active is null) throw NormalWeightedTrace.Missing("ambiguous active native bag");
        if (added.Length == 1) { active = added[0].Bag; seen.Add(active); }
        var current = bags.Single(b => ReferenceEquals(b.Bag, active));
        var raw = Items(current.Bag);
        var entries = raw.Select(e =>
            new NormalWeightedEntry(current.Act.Index, current.Room, e.Item.IsWeak,
                "base-spire:" + e.Item.IdEntry.ToLowerInvariant().Replace('_', '-'), BitConverter.DoubleToInt64Bits(e.Weight),
                e.Item.Tags.Where(t => t != EncounterTag.None).Select(t => t.ToString()).Order(StringComparer.Ordinal).ToArray())).ToArray();
        if (entries.Length == 0) throw NormalWeightedTrace.Missing("empty active native bag");
        var snapshot = new NormalWeightedSnapshot(entries, BitConverter.DoubleToInt64Bits((double)Total.GetValue(current.Bag)!));
        var last = LastPicked(current.State); var lastMonster = LastMonster(current.Act);
        var previous = current.Room == "Monster" ? lastMonster ?? last : last;
        var pool = current.Room == "Elite" ? current.Act.EliteEncounterCandidates : current.Act.MonsterEncounterCandidates;
        var fullPool = pool.Where(e => current.Room == "Elite" || e.IsWeak == raw[0].Item.IsWeak).ToArray();
        pending = new(current.Act, current.Room, current.State, current.Bag, raw, fullPool, last, lastMonster, previous, before, snapshot);
        return snapshot;
    }
    internal void Result(double unit)
    {
        var p = pending ?? throw NormalWeightedTrace.Missing("native weighted result without its actual bag");
        pending = p with { Selected = NormalWeightedTrace.FromUnit("base-spire:run:up_front", p.Before, p.Before + 1, p.Snapshot, unit).Selected };
    }
    internal void FinishPending()
    {
        if (pending is not { } p) return;
        if (p.Selected < 0) throw NormalWeightedTrace.Missing("native opening result is unfinished");
        var after = Items(p.Bag); var selected = p.Entries[p.Selected].Item;
        var remaining = p.Entries.Where((_, i) => i != p.Selected).ToArray();
        var unchanged = Same(after, p.Entries);
        // The next picker may already have refilled an emptied bag before its draw
        // callback. Read that real pool and require the full refill, not a guessed index.
        var refilled = p.Entries.Length == 1 && Same(after, p.FullPool.Select(e => (e, 1d)).ToArray());
        if (unchanged && refilled) throw NormalWeightedTrace.Missing("ambiguous single-entry native refill");
        var accepted = Same(after, remaining) || refilled;
        if (!accepted && !unchanged || !ReferenceEquals(LastPicked(p.State), accepted ? selected : p.LastPicked)
            || !ReferenceEquals(LastMonster(p.Act), accepted && p.Room == "Monster" ? selected : p.LastMonster))
            throw NormalWeightedTrace.Missing("native opening accept/reject changed unexpected bag or previous state");
        if (BitConverter.DoubleToInt64Bits(after.Sum(e => e.Weight)) != BitConverter.DoubleToInt64Bits((double)Total.GetValue(p.Bag)!))
            throw NormalWeightedTrace.Missing("native opening committed total weight");
        var mask = p.Entries.Select(e => !ReferenceEquals(e.Item, p.Previous)
            && (p.Previous is null || !e.Item.Tags.Any(t => t != EncounterTag.None && p.Previous.Tags.Contains(t)))).ToArray();
        attempts.Add(new(p.Before, p.Previous is null ? null : "base-spire:" + p.Previous.IdEntry.ToLowerInvariant().Replace('_', '-'), mask.Any(v => v), mask, accepted));
        commits.Add(new { before = p.Before, accepted, refilled, after = after.Select(e => new
            { id = e.Item.IdEntry, weight_bits = BitConverter.DoubleToInt64Bits(e.Weight) }).ToArray(),
            total_bits = BitConverter.DoubleToInt64Bits((double)Total.GetValue(p.Bag)!),
            last_picked = LastPicked(p.State)?.IdEntry, last_monster = LastMonster(p.Act)?.IdEntry });
        pending = null;
    }
    internal NormalOpeningAttempt[] Attempts() { FinishPending(); return attempts.ToArray(); }
    internal object[] Commits() { FinishPending(); return commits.ToArray(); }
    private static (EncounterDefinition Item, double Weight)[] Items(object bag) =>
        ((IEnumerable<(EncounterDefinition Item, double Weight)>)Entries.GetValue(bag)!).ToArray();
    private static EncounterDefinition? LastPicked(object state) => (EncounterDefinition?)state.GetType().GetProperty("LastPicked")!.GetValue(state);
    private static EncounterDefinition? LastMonster(ActDefinition act) =>
        (EncounterDefinition?)typeof(ActDefinition).GetField("_lastMonsterEncounter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(act);
    private static bool Same((EncounterDefinition Item, double Weight)[] left, (EncounterDefinition Item, double Weight)[] right) =>
        left.Length == right.Length && left.Zip(right).All(p => ReferenceEquals(p.First.Item, p.Second.Item)
            && BitConverter.DoubleToInt64Bits(p.First.Weight) == BitConverter.DoubleToInt64Bits(p.Second.Weight));
}
#endif
