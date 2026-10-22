using System.Globalization;
using CoreTheSpire.Engine;

namespace Differential;

internal sealed record NormalOpeningAttempt(long Before, string? Previous, bool UsePredicate, bool[] CompatibleCandidates, bool Accepted);

// Read actual pure-procedure results and the actual picker return. The debug text
// format is deliberately parsed strictly; a changed or truncated format fails.
internal static class NormalOpeningAttempts
{
    internal static bool Relevant(SessionDebugRecord record) => record.Kind is SessionDebugKind.ProcedureEnter or SessionDebugKind.ProcedureExit
        && record.Subject is "base-spire:PickOpeningEncounter" or "base-spire:OpeningEncountersCompatible";

    internal static NormalOpeningAttempt[] Dsl(IReadOnlyList<SessionDebugRecord> records, long dropped)
    {
        if (dropped != 0) throw Missing("dropped opening attempt records");
        var weighted = NormalWeightedTrace.Dsl(records, dropped).ToDictionary(c => c.Before);
        var definitions = new Dictionary<int, string>(); var result = new List<NormalOpeningAttempt>();
        var picking = false; var previous = 0; var usePredicate = false; bool? compatible = null;
        int? checking = null; var checks = new List<(int Definition, bool Compatible)>(); bool[] mask = [];
        (NormalWeightedCall Call, int Selected)? pending = null;
        foreach (var record in records)
        {
            if (Relevant(record))
            {
                if (record.TextTruncated) throw Missing("truncated opening procedure");
                if (record.Subject == "base-spire:PickOpeningEncounter")
                {
                    if (record.Kind == SessionDebugKind.ProcedureEnter)
                    {
                        if (picking) throw Missing("nested opening picker");
                        var args = Arguments(record); Ref(args[0]); previous = Ref(args[1]);
                        picking = true; usePredicate = false; checks.Clear(); mask = [];
                    }
                    else
                    {
                        if (!picking || checking is not null || pending is not { } draw || Ref(record.Detail!) != draw.Selected)
                            throw Missing("opening picker return differs from its actual selected candidate");
                        Finish(true); picking = false;
                    }
                }
                else
                {
                    if (!picking) throw Missing("compatibility check outside its opening picker");
                    if (record.Kind == SessionDebugKind.ProcedureEnter)
                    {
                        var args = Arguments(record);
                        if (checking is not null) throw Missing("nested compatibility check");
                        checking = Ref(args[0]);
                        if (Ref(args[1]) != previous) throw Missing("compatibility call changed its previous context");
                        if (pending is { } draw)
                        {
                            var selected = draw.Call.Candidates.Entries[draw.Call.Selected].Id;
                            if (!definitions.TryGetValue(Ref(args[0]), out var id) || id != selected)
                                throw Missing("compatibility call did not test the selected definition");
                        }
                    }
                    else
                    {
                        var value = record.Detail switch { "true" => true, "false" => false, _ => throw Missing("typed compatibility result") };
                        var definition = checking ?? throw Missing("compatibility result without its arguments"); checking = null;
                        if (pending is null) { usePredicate |= value; checks.Add((definition, value)); }
                        else if (compatible is not null) throw Missing("duplicate post-draw compatibility result");
                        else compatible = value;
                    }
                }
            }
            if (record.Random is not { Operation: "pick" } random || record.Subject != "base-spire:run:up_front"
                || !weighted.TryGetValue(random.Before, out var call)) continue;
            if (!picking) throw Missing("weighted draw outside its actual picker");
            if (checking is not null) throw Missing("unfinished compatibility check before draw");
            if (pending is not null) Finish(false);
            foreach (var entity in record.Snapshot!.Entities.Where(e => e.Type == "base-spire:OpeningDefinition"))
            {
                var id = entity.Fields.Single(f => f.Name == "DefinitionId").Value.Scalar!;
                if (definitions.TryGetValue(entity.Instance.Value, out var old) && old != id) throw Missing("mutable opening definition identity");
                definitions[entity.Instance.Value] = id;
            }
            if (mask.Length == 0)
            {
                if (!checks.Select(c => definitions.TryGetValue(c.Definition, out var id) ? id : throw Missing("precheck definition identity"))
                    .SequenceEqual(call.Candidates.Entries.Select(e => e.Id)))
                    throw Missing("compatibility precheck omitted, repeated or reordered candidates");
                mask = checks.Select(c => c.Compatible).ToArray();
            }
            pending = (call, random.Candidates![call.Selected].Value); compatible = null;
        }
        if (picking || pending is not null || result.Count != weighted.Count) throw Missing("unfinished or missing opening attempts");
        return result.ToArray();

        void Finish(bool accepted)
        {
            var draw = pending ?? throw Missing("opening return without an actual draw");
            if (accepted != (!usePredicate || compatible == true) || usePredicate && compatible is null)
                throw Missing("actual picker outcome contradicts its compatibility result");
            var id = previous == 0 ? null : definitions.TryGetValue(previous, out var value) ? value : throw Missing("historical previous definition");
            result.Add(new(draw.Call.Before, id, usePredicate, mask, accepted)); pending = null; compatible = null;
        }
    }

    private static string[] Arguments(SessionDebugRecord record)
    {
        var detail = record.Detail ?? throw Missing("opening procedure arguments");
        var start = detail.IndexOf("; arguments=[", StringComparison.Ordinal);
        if (start < 0 || !detail.EndsWith(']')) throw Missing("opening argument format");
        var args = detail[(start + 13)..^1].Split(',');
        return args.Length == 2 ? args : throw Missing("opening argument arity");
    }
    private static int Ref(string value) => value == "<none>" ? 0
        : value.StartsWith('#') && int.TryParse(value.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id : throw Missing("opening reference format");
    private static NotSupportedException Missing(string message) => new("missing_dimension: " + message);
}
