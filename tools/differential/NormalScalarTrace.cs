using System.Globalization;
using CoreTheSpire.Engine;

namespace Differential;

internal sealed record NormalScalarCall(string Stream, long Before, long After, string Kind, string Minimum, string Maximum, string Result);
internal sealed record NormalUnmappedScalar(string Stream, long Before, long After, string Reason);
internal sealed record NormalScalarProjection(NormalScalarCall[] Calls, NormalUnmappedScalar[] Unmapped);

internal static class NormalScalarTrace
{
    internal static NormalScalarProjection Dsl(IReadOnlyList<SessionDebugRecord> records, long dropped)
    {
        if (dropped != 0) throw Missing("dropped scalar random records");
        var calls = new List<NormalScalarCall>(); var unmapped = new List<NormalUnmappedScalar>();
        foreach (var record in records.Where(r => r.Kind == SessionDebugKind.RandomCall))
        {
            if (record.TextTruncated || string.IsNullOrEmpty(record.Subject) || record.Random is not { } value || value.CandidatesTruncated)
                throw Missing("scalar random parameters, stream or candidate prefix");
            var stream = record.Subject;
            if (value.Operation == "double")
            {
                if (value.Float32 is null) throw Missing("unclassified raw double");
                continue; // Independently compared in random.float32_calls.
            }
            if (stream == "base-spire:run:niche")
            { unmapped.Add(new(stream, value.Before, value.After, "monster HP offset normalization")); continue; }
            if (value.Operation is not ("int" or "pick" or "shuffle")) throw Missing("scalar operation " + value.Operation);
            if (value.Operation == "pick" && stream == "base-spire:run:up_front")
            {
                var roots = record.Snapshot?.Entities.Where(e => value.Candidates!.Contains(e.Instance)).ToArray()
                    ?? throw Missing("initialization candidate shape");
                if (roots.Length != value.CandidateCount) throw Missing("initialization candidate roots");
                if (roots.All(e => e.Type == "base-spire:OpeningListItem"))
                { unmapped.Add(new(stream, value.Before, value.After, "opening weighted bag normalization")); continue; }
                if (roots.Any(e => e.Type != "base-spire:OpeningDefinition")) throw Missing("unclassified initialization pick");
            }
            var min = long.Parse(value.Minimum, CultureInfo.InvariantCulture); var max = long.Parse(value.Maximum, CultureInfo.InvariantCulture);
            var result = long.Parse(value.Result, CultureInfo.InvariantCulture);
            if (stream == "base-spire:run:act_1_map" && value.Operation == "int" && min == 0 && max == 9007199254740992
                && record.Source?.Source.Replace('\\', '/').EndsWith("/run/random.spire", StringComparison.Ordinal) == true)
                calls.Add(UnitDouble(stream, value.Before, value.After, result / 9007199254740992d));
            else calls.Add(Integer(stream, value.Before, value.After, min, max, result));
        }
        return new(Canonical(calls), unmapped.OrderBy(c => c.Stream, StringComparer.Ordinal).ThenBy(c => c.Before).ToArray());
    }

    internal static NormalScalarCall Integer(string stream, long before, long after, long min, long max, long result)
    {
        if (min >= max || result < min || result >= max) throw Missing("invalid integer scalar parameters or result");
        return new(stream, before, after, "integer", min.ToString(CultureInfo.InvariantCulture), max.ToString(CultureInfo.InvariantCulture), result.ToString(CultureInfo.InvariantCulture));
    }

    internal static NormalScalarCall UnitDouble(string stream, long before, long after, double result)
    {
        if (!double.IsFinite(result) || result < 0 || result >= 1) throw Missing("invalid unit double result");
        return new(stream, before, after, "binary64", "0", "1", BitConverter.DoubleToUInt64Bits(result).ToString(CultureInfo.InvariantCulture));
    }

    internal static NormalScalarCall[] Canonical(IEnumerable<NormalScalarCall> source)
    {
        var calls = source.ToArray();
        foreach (var group in calls.GroupBy(c => c.Stream))
        {
            long previous = -1;
            foreach (var call in group)
            {
                if (call.Before <= previous || call.After != call.Before + 1) throw Missing("duplicate, reordered or invalid scalar cursor");
                previous = call.Before;
            }
        }
        return calls.OrderBy(c => c.Stream, StringComparer.Ordinal).ThenBy(c => c.Before).ToArray();
    }

    private static NotSupportedException Missing(string detail) => new("missing_dimension: " + detail);
}
