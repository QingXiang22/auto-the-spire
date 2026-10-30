using System.Globalization;
using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalDslMapCandidates
{
    internal static readonly string[] Captures = ["GenerateSourceActOneMap", "SourceGaussianInt", "SelectSourceMapStart", "NextSourceMapPoint",
        "AddMapWork", "ShuffleMapWork", "DestroyMapWork", "RepairSourceMapKind", "PruneSourceMapSegment", "BreakSourceMapSegment"];
    internal static bool Relevant(SessionDebugRecord r) => r.Kind is SessionDebugKind.ProcedureEnter or SessionDebugKind.ProcedureExit
        && (Captures.Contains(r.Subject?.Replace("base-spire:", "", StringComparison.Ordinal)) || r.Subject == "base-spire:AssignSourceMapKinds");
    private sealed record Item(int Reference, string Id, int Position);
    internal static NormalMapCandidates Capture(IReadOnlyList<SessionDebugRecord> records, long dropped, int rests, int unknowns)
    {
        if (dropped != 0) throw NormalMapCandidatesTrace.Missing("dropped map records");
        var calls = new List<NormalMapCall>(); var gaussians = new List<NormalMapGaussian>(); var nodes = new Dictionary<int, string>();
        var pruning = new List<SessionDebugRecord>(); var segments = new Dictionary<int, Item[]>();
        var active = false; var finished = false; string? offsets = null; string? start = null; string? repair = null; var assign = false; var round = 0;
        (int Mean, int Deviation, int Min, int Max, long Before)? gaussian = null;
        (int Group, string Kind, string Context, Item[] Source, List<Item> Current, int Index)? work = null;
        long cursor = 0;
        foreach (var record in records)
        {
            if (record.Kind == SessionDebugKind.ProcedureEnter && record.Subject == "base-spire:GenerateSourceActOneMap")
            {
                if (active || finished) throw NormalMapCandidatesTrace.Missing("repeated map generation"); active = true;
            }
            if (!active) continue;
            if (Relevant(record))
            {
                if (record.TextTruncated) throw NormalMapCandidatesTrace.Missing("truncated map procedure");
                var entering = record.Kind == SessionDebugKind.ProcedureEnter;
                switch (record.Subject)
                {
                    case "base-spire:GenerateSourceActOneMap" when !entering:
                        if (work is not null || gaussian is not null || pruning.Count != 0) throw NormalMapCandidatesTrace.Missing("unfinished map work");
                        active = false; finished = true; break;
                    case "base-spire:SourceGaussianInt":
                        if (entering)
                        {
                            var s = Snapshot(record, 5);
                            // Five evaluated parameters: stream, mean, deviation, min, max.
                            if (gaussian is not null || Scalar(s.Values[0]) != NormalMapCandidatesTrace.Stream) throw NormalMapCandidatesTrace.Missing("foreign Gaussian context");
                            gaussian = (Integer(s.Values[1]), Integer(s.Values[2]), Integer(s.Values[3]), Integer(s.Values[4]), cursor);
                        }
                        else
                        {
                            if (gaussian is not { } g || !int.TryParse(record.Detail, out var result)) throw NormalMapCandidatesTrace.Missing("missing actual Gaussian return");
                            gaussians.Add(new(g.Mean, g.Deviation, g.Min, g.Max, result, g.Before, cursor)); gaussian = null;
                        }
                        break;
                    case "base-spire:SelectSourceMapStart":
                        if (entering) start = Scalar(Snapshot(record, 2).Values[1]) == "true" ? "distinct" : "any";
                        else start = null;
                        break;
                    case "base-spire:NextSourceMapPoint":
                        if (entering) { var s = Snapshot(record, 2); offsets = Node(s, Integer(s.Values[1])); }
                        else offsets = null;
                        break;
                    case "base-spire:AssignSourceMapKinds": assign = entering; break;
                    case "base-spire:RepairSourceMapKind":
                        repair = entering ? Scalar(Snapshot(record, 3).Values[1]) : null; break;
                    case "base-spire:AddMapWork" when entering:
                    {
                        var s = Snapshot(record, 3); if (s.Values[1].Kind == ValueKind.Ref) nodes[Integer(s.Values[1])] = Node(s, Integer(s.Values[1]));
                        break;
                    }
                    case "base-spire:ShuffleMapWork" when entering:
                    {
                        if (work is not null || pruning.Count != 0) throw NormalMapCandidatesTrace.Missing("nested map shuffle");
                        var s = Snapshot(record, 1); var group = Integer(s.Values[0]); var source = Work(s, group, nodes);
                        if (!source.OrderBy(i => i.Position).Select(i => i.Position).SequenceEqual(Enumerable.Range(0, source.Length)))
                            throw NormalMapCandidatesTrace.Missing("noncontiguous initial map work positions");
                        var kind = offsets is not null ? "offsets" : assign ? "assign" : repair is not null ? "repair" : throw NormalMapCandidatesTrace.Missing("unknown map work consumer");
                        var context = offsets ?? (assign ? (round++).ToString(CultureInfo.InvariantCulture) : repair!);
                        work = (group, kind, context, source, source.OrderBy(i => i.Position).ToList(), source.Length - 1); break;
                    }
                    case "base-spire:DestroyMapWork" when entering:
                    {
                        var s = Snapshot(record, 1); var group = Integer(s.Values[0]); if (work is not { } w || w.Group != group) break;
                        var actual = Work(s, group, nodes);
                        if (w.Index > 0 || !actual.Select(i => i.Reference).SequenceEqual(w.Source.Select(i => i.Reference))
                            || !actual.OrderBy(i => i.Position).Select(i => (i.Reference, i.Id)).SequenceEqual(w.Current.Select(i => (i.Reference, i.Id)))
                            || !actual.OrderBy(i => i.Position).Select(i => i.Position).SequenceEqual(Enumerable.Range(0, actual.Length)))
                            throw NormalMapCandidatesTrace.Missing("map work shuffle differs from actual full transient result");
                        work = null; break;
                    }
                    case "base-spire:PruneSourceMapSegment" or "base-spire:BreakSourceMapSegment" when entering:
                    {
                        var s = Snapshot(record, 1); var segment = Entity(s, Integer(s.Values[0])); var key = Integer(Field(segment, "Key"));
                        var name = Scalar(Field(Entity(s, key), "Key"));
                        var pool = Relation(s, key, "MapKeySegments").Select(id =>
                        {
                            var e = Entity(s, id); if (Integer(Field(e, "Key")) != key) throw NormalMapCandidatesTrace.Missing("foreign map segment group");
                            return new Item(id, Segment(Integer64(Field(e, "Columns")), Integer(Field(e, "StartRow")), Integer(Field(e, "Length"))), Integer(Field(e, "Position")));
                        }).ToArray();
                        if (segments.TryGetValue(key, out var previous))
                        { if (!previous.SequenceEqual(pool) || pruning.Count != 0) throw NormalMapCandidatesTrace.Missing("changed map segment source or extra draws"); break; }
                        var current = pool.ToList(); var index = current.Count - 1;
                        if (pruning.Count != index) throw NormalMapCandidatesTrace.Missing("missing map segment shuffle calls");
                        foreach (var draw in pruning)
                        {
                            var selected = Pick(draw, index + 1);
                            calls.Add(new("prune", name, draw.Random!.Before, draw.Random.After, selected, current.Take(index + 1).Select(i => i.Id).ToArray()));
                            (current[index], current[selected]) = (current[selected], current[index]); index--;
                        }
                        if (!pool.OrderBy(i => i.Position).Select(i => i.Position).SequenceEqual(Enumerable.Range(0, pool.Length))
                            || !pool.OrderBy(i => i.Position).Select(i => i.Reference).SequenceEqual(current.Select(i => i.Reference)))
                            throw NormalMapCandidatesTrace.Missing("map segment shuffle differs from full historical group");
                        segments.Add(key, pool); pruning.Clear(); break;
                    }
                }
            }
            if (record.Kind != SessionDebugKind.RandomCall || record.Subject != NormalMapCandidatesTrace.Stream) continue;
            if (record.TextTruncated || record.Random is not { CandidatesTruncated: false } d || d.Before != cursor || d.After != ++cursor)
                throw NormalMapCandidatesTrace.Missing("incomplete map random history");
            if (gaussian is { } g2)
            {
                if (d.Operation != "int" || d.Minimum != "0" || d.Maximum != "9007199254740992") throw NormalMapCandidatesTrace.Missing("invalid map Gaussian draw");
                calls.Add(new("gaussian", $"{g2.Mean}/{g2.Deviation}/{g2.Min}/{g2.Max}", d.Before, d.After, long.Parse(d.Result, CultureInfo.InvariantCulture), []));
            }
            else if (start is not null) calls.Add(new("start", start, d.Before, d.After, Pick(record, 7), Enumerable.Range(0, 7).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray()));
            else if (work is { } w)
            {
                if (w.Index < 1) throw NormalMapCandidatesTrace.Missing("extra map work shuffle draw");
                var selected = Pick(record, w.Index + 1);
                calls.Add(new(w.Kind, w.Context, d.Before, d.After, selected, w.Current.Take(w.Index + 1).Select(i => i.Id).ToArray()));
                (w.Current[w.Index], w.Current[selected]) = (w.Current[selected], w.Current[w.Index]); work = w with { Index = w.Index - 1 };
            }
            else pruning.Add(record);
        }
        if (!finished || active || gaussians.Count != 2 || gaussians[0].Result != rests || gaussians[1].Result != unknowns
            || records.Count(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == NormalMapCandidatesTrace.Stream) != calls.Count)
            throw NormalMapCandidatesTrace.Missing("missing map generation or actual count evidence");
        return NormalMapCandidatesTrace.Validate(new(calls.ToArray(), gaussians.ToArray()));
    }
    private static Item[] Work(SessionDebugSnapshot s, int group, Dictionary<int, string> nodes) => Relation(s, group, "MapWorkItems").Select(id =>
    {
        var e = Entity(s, id); if (Integer(Field(e, "Group")) != group) throw NormalMapCandidatesTrace.Missing("foreign map work item");
        var node = Field(e, "Node"); var value = node.Kind == ValueKind.None ? Scalar(Field(e, "Value"))
            : nodes.TryGetValue(Integer(node), out var coordinate) ? coordinate : throw NormalMapCandidatesTrace.Missing("missing actual map node creation evidence");
        return new Item(id, value, Integer(Field(e, "Position")));
    }).ToArray();
    private static int Pick(SessionDebugRecord record, int max)
    {
        var d = record.Random!;
        if (d.Operation != "int" || d.Minimum != "0" || d.Maximum != max.ToString(CultureInfo.InvariantCulture)
            || !int.TryParse(d.Result, out var selected) || selected < 0 || selected >= max) throw NormalMapCandidatesTrace.Missing("map integer range or index");
        return selected;
    }
    private static string Node(SessionDebugSnapshot s, int id)
    { var e = Entity(s, id); return NormalMapCandidatesTrace.Coordinate(Integer(Field(e, "Col")), Integer(Field(e, "Row"))); }
    private static string Segment(long columns, int row, int length)
    {
        var nodes = new string[length]; for (var i = length - 1; i >= 0; i--) { nodes[i] = NormalMapCandidatesTrace.Coordinate(columns % 8, row + i); columns /= 8; }
        return string.Join('>', nodes);
    }
    private static SessionDebugSnapshot Snapshot(SessionDebugRecord record, int count) => record.Snapshot is { Complete: true, RelationsCaptured: true } s && s.ValueCount == count
        ? s : throw NormalMapCandidatesTrace.Missing("missing complete map procedure argument/relation snapshot: " + record.Subject);
    private static SessionDebugEntitySnapshot Entity(SessionDebugSnapshot s, int id) => s.Entities.SingleOrDefault(e => e.Instance.Value == id)
        ?? throw NormalMapCandidatesTrace.Missing("missing historical map entity");
    private static SessionDebugValueSnapshot Field(SessionDebugEntitySnapshot e, string field) => e.Fields.Single(f => f.Name == field).Value;
    private static string Scalar(SessionDebugValueSnapshot v) => !v.Truncated && v.Scalar is { } value ? value : throw NormalMapCandidatesTrace.Missing("missing map scalar");
    private static long Integer64(SessionDebugValueSnapshot v) => long.Parse(Scalar(v), CultureInfo.InvariantCulture);
    private static int Integer(SessionDebugValueSnapshot v) => checked((int)Integer64(v));
    private static int[] Relation(SessionDebugSnapshot s, int owner, string name) => s.Relations.SingleOrDefault(r => r.Owner.Value == owner && r.Relation == "base-spire:" + name)
        is { Truncated: false } r ? r.Targets.Select(i => i.Value).ToArray() : [];
}
