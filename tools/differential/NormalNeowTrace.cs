using System.Globalization;
using CoreTheSpire.Engine;

namespace Differential;

internal sealed record NormalNeowCall(string Kind, long Before, long After, int Selected, string[] Candidates);
internal sealed record NormalNeowCandidates(string[] BasePositives, string[] Positives, NormalNeowCall[] Calls, string[] Options);
internal sealed record NormalNeowSource(int Reference, string Id, string Group, long InitialPosition, long Position, bool Eligible, bool AddedByPair = false);
internal sealed record NormalNeowEvidence(NormalNeowCandidates Candidates, NormalNeowSource[] Sources);

internal static class NormalNeowTrace
{
    internal const string Stream = "base-spire:event:neow";
    internal const string OptionProcedure = "base-spire:AddNeowOption";
    private static readonly string[] Procedures = ["GenerateSourceNeowOptions", "AddNeowPoolEntry", "AddNeowPair", "NeowSourceBool", "AddNeowOption"];
    internal static bool Relevant(SessionDebugRecord r) => r.Kind is SessionDebugKind.ProcedureEnter or SessionDebugKind.ProcedureExit
        && Procedures.Any(p => r.Subject == "base-spire:" + p);
    private sealed class Pair(string[] candidates, long position)
    {
        internal string[] Candidates { get; } = candidates;
        internal long Position { get; } = position;
        internal NormalNeowCall? Call;
        internal bool Added;
        internal bool Returned;
        internal bool Started;
    }
    internal static NormalNeowEvidence Dsl(IReadOnlyList<SessionDebugRecord> records, long dropped, string[] options)
    {
        if (dropped != 0) throw Missing("dropped Neow history");
        var offers = records.Where(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == OptionProcedure).ToArray();
        if (offers.Length != 3 || offers.Any(r => r.TextTruncated || r.Snapshot is not { Complete: true, RelationsCaptured: true, ValueCount: 3 }))
            throw Missing("missing complete Neow option argument/relation snapshots");
        var visit = RefValue(offers[0].Snapshot!.Values[0]);
        var pool = Pool(offers[0].Snapshot!, visit);
        var byId = pool.ToDictionary(e => e.Id, StringComparer.Ordinal); var byRef = pool.ToDictionary(e => e.Reference);
        for (var i = 0; i < offers.Length; i++)
        {
            var snapshot = offers[i].Snapshot!;
            if (RefValue(snapshot.Values[0]) != visit || snapshot.Values[2] is not { Kind: ValueKind.Int, Scalar: { } position }
                || position != i.ToString(CultureInfo.InvariantCulture) || !Pool(snapshot, visit).SequenceEqual(pool)
                || !byRef.ContainsKey(RefValue(snapshot.Values[1]))) throw Missing("Neow option arguments or transient pool changed");
        }
        var sources = new List<NormalNeowSource>(); var calls = new List<NormalNeowCall>();
        var active = false; var finished = false; Pair? pair = null;
        string? curse = null; List<string>? shuffled = null; string[] positives = []; var index = 0; var offered = 0; long cursor = 0;
        foreach (var record in records)
        {
            if (Relevant(record))
            {
                if (record.TextTruncated) throw Missing("truncated Neow procedure");
                var entering = record.Kind == SessionDebugKind.ProcedureEnter;
                switch (record.Subject)
                {
                    case "base-spire:GenerateSourceNeowOptions":
                        if (entering)
                        {
                            if (active || finished || Reference(Arguments(record, 1)[0]) != visit) throw Missing("repeated or foreign Neow generation");
                            active = true;
                        }
                        else { if (!active || pair is not null) throw Missing("unfinished Neow generation"); active = false; finished = true; }
                        break;
                    case "base-spire:AddNeowPair" when active:
                        if (entering)
                        {
                            var args = Arguments(record, 4);
                            if (pair is not null || curse is null || shuffled is not null || Reference(args[0]) != visit) throw Missing("invalid Neow pair context");
                            pair = new([String(args[1]), String(args[2])], long.Parse(args[3], CultureInfo.InvariantCulture));
                        }
                        else { if (pair is not { Added: true, Returned: true }) throw Missing("Neow pair was not drawn, returned and added"); pair = null; }
                        break;
                    case "base-spire:NeowSourceBool" when active:
                        if (entering)
                        {
                            if (pair is null || pair.Started || pair.Call is not null) throw Missing("invalid Neow boolean call context");
                            pair.Started = true;
                        }
                        else
                        {
                            if (pair is not { Started: true, Returned: false, Call: { } boolean } || record.Detail != (boolean.Selected == 0 ? "true" : "false"))
                                throw Missing("Neow boolean return disagrees with actual draw");
                            pair.Returned = true;
                        }
                        break;
                    case OptionProcedure when entering:
                        if (!active || pair is not null || shuffled is null || index != 0 || offered >= offers.Length || record != offers[offered++])
                            throw Missing("Neow option outside its completed shuffle");
                        break;
                    case "base-spire:AddNeowPoolEntry" when active && entering:
                    {
                        var args = Arguments(record, 4); var id = String(args[1]); var group = String(args[2]);
                        var position = long.Parse(args[3], CultureInfo.InvariantCulture);
                        if (Reference(args[0]) != visit || !byId.TryGetValue(id, out var entry) || entry.Group != group || sources.Any(s => s.Id == id)
                            || shuffled is not null) throw Missing("Neow entry differs from actual transient source");
                        if (pair is not null)
                        {
                            if (!pair.Returned || pair.Call is not { } choice || pair.Added || group != "positive" || position != pair.Position
                                || id != choice.Candidates[choice.Selected]) throw Missing("Neow pair added the wrong actual relic");
                            pair.Added = true;
                        }
                        sources.Add(entry with { InitialPosition = position, AddedByPair = pair is not null });
                        break;
                    }
                }
            }
            if (record.Kind != SessionDebugKind.RandomCall || record.Subject != Stream) continue;
            if (!active || record.TextTruncated || record.Random is not { CandidatesTruncated: false } draw || draw.Before != cursor || draw.After != cursor + 1)
                throw Missing("incomplete, reordered or unscoped Neow draw");
            cursor = draw.After; var selected = int.Parse(draw.Result, CultureInfo.InvariantCulture);
            if (curse is null)
            {
                if (draw.Operation != "pick" || draw.Candidates is not { Count: > 0 } roots || roots.Count != draw.CandidateCount
                    || record.Snapshot is not { Complete: true } snapshot) throw Missing("missing Neow curse candidates");
                var entities = snapshot.Entities.ToDictionary(e => e.Instance.Value);
                var candidates = roots.Select(r => byRef.TryGetValue(r.Value, out var entry) && entry.Group == "curse" && entry.Eligible
                    && entities.TryGetValue(r.Value, out var entity) && Field(entity, "RelicId") == entry.Id && Field(entity, "Group") == "curse"
                    && Field(entity, "Eligible") == "true" && Field(entity, "Visit") == visit.ToString(CultureInfo.InvariantCulture)
                    && Field(entity, "Position") == entry.Position.ToString(CultureInfo.InvariantCulture)
                    ? entry.Id : throw Missing("corrupt historical Neow curse source")).ToArray();
                if (!candidates.SequenceEqual(sources.Where(s => s.Group == "curse").Select(s => s.Id))) throw Missing("incomplete or reordered curse source");
                Bounds(draw, candidates.Length, selected); curse = candidates[selected]; calls.Add(new("curse", draw.Before, draw.After, selected, candidates));
            }
            else if (pair is not null)
            {
                if (!pair.Started || pair.Returned || pair.Call is not null || draw.Operation != "int") throw Missing("extra or non-integer Neow pair draw");
                Bounds(draw, 2, selected); calls.Add(pair.Call = new("pair", draw.Before, draw.After, selected, pair.Candidates));
            }
            else
            {
                if (draw.Operation != "int") throw Missing("unclassified Neow draw");
                if (shuffled is null)
                {
                    var eligible = sources.Where(s => s.Group == "positive" && s.Eligible).OrderBy(s => s.InitialPosition).ToArray();
                    if (eligible.Select(s => s.InitialPosition).Distinct().Count() != eligible.Length) throw Missing("ambiguous Neow source positions");
                    positives = eligible.Select(s => s.Id).ToArray(); shuffled = positives.ToList(); index = shuffled.Count - 1;
                }
                if (index < 1) throw Missing("extra Neow shuffle draw");
                Bounds(draw, index + 1, selected);
                calls.Add(new("shuffle", draw.Before, draw.After, selected, shuffled.Take(index + 1).ToArray()));
                (shuffled[index], shuffled[selected]) = (shuffled[selected], shuffled[index]); index--;
            }
        }
        if (!finished || active || pair is not null || shuffled is null || index != 0 || offered != 3 || sources.Count != pool.Length || curse is null)
            throw Missing("unfinished or incomplete Neow candidate history");
        var actual = pool.Where(s => s.Group == "positive" && s.Eligible).OrderBy(s => s.Position).ToArray();
        if (!actual.Select(s => s.Position).SequenceEqual(Enumerable.Range(0, actual.Length).Select(i => (long)i))
            || !actual.Select(s => s.Id).SequenceEqual(shuffled)) throw Missing("shuffle reconstruction differs from actual full Neow pool");
        var expected = shuffled.Take(2).Append(curse).ToArray();
        if (options.Length != 3 || !expected.SequenceEqual(options) || !offers.Select(r => byRef[RefValue(r.Snapshot!.Values[1])].Id).SequenceEqual(options))
            throw Missing("actual Neow options disagree with source draws");
        return new(new(sources.Where(s => s.Group == "positive" && !s.AddedByPair).Select(s => s.Id).ToArray(), positives, calls.ToArray(), options), sources.ToArray());
    }
    private static NormalNeowSource[] Pool(SessionDebugSnapshot snapshot, int visit)
    {
        var relation = snapshot.Relations.SingleOrDefault(r => r.Owner.Value == visit && r.Relation == "base-spire:NeowPool");
        if (relation is null || relation.Truncated || relation.Targets.Count == 0) throw Missing("missing transient Neow pool relation");
        var entities = snapshot.Entities.ToDictionary(e => e.Instance);
        return relation.Targets.Select(id => entities.TryGetValue(id, out var e) && e.Type == "base-spire:NeowPoolEntry"
            && Field(e, "Visit") == visit.ToString(CultureInfo.InvariantCulture)
            ? new NormalNeowSource(id.Value, Field(e, "RelicId"), Field(e, "Group"), -1, long.Parse(Field(e, "Position"), CultureInfo.InvariantCulture), bool.Parse(Field(e, "Eligible")))
            : throw Missing("missing Neow pool member")).ToArray();
    }
    private static void Bounds(RandomCallDetails draw, int count, int selected)
    { if (draw.Minimum != "0" || draw.Maximum != count.ToString(CultureInfo.InvariantCulture) || selected < 0 || selected >= count) throw Missing("invalid Neow range or index"); }
    private static int RefValue(SessionDebugValueSnapshot v) => v is { Kind: ValueKind.Ref, Truncated: false, Scalar: { } s }
        ? int.Parse(s, CultureInfo.InvariantCulture) : throw Missing("missing typed Neow reference");
    private static string Field(SessionDebugEntitySnapshot e, string name) => e.Fields.Single(f => f.Name == name).Value is { Truncated: false, Scalar: { } s }
        ? s : throw Missing("missing historical Neow field");
    private static string[] Arguments(SessionDebugRecord record, int count)
    {
        var text = record.Detail ?? throw Missing("missing Neow arguments"); var start = text.IndexOf("; arguments=[", StringComparison.Ordinal);
        if (start < 0 || !text.EndsWith(']')) throw Missing("invalid Neow argument format");
        var args = text[(start + 13)..^1].Split(','); return args.Length == count ? args : throw Missing("invalid Neow argument count");
    }
    private static string String(string s) => s.Length >= 2 && s[0] == '"' && s[^1] == '"' && !s[1..^1].Contains('"')
        ? s[1..^1] : throw Missing("invalid Neow string argument");
    private static int Reference(string s) => s.StartsWith('#') && int.TryParse(s.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw Missing("invalid Neow reference");
    internal static NotSupportedException Missing(string detail) => new("missing_dimension: " + detail);
}
