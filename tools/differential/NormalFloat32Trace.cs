using CoreTheSpire.Engine;

namespace Differential;

internal sealed record NormalFloat32Call(string Stream, long Before, long After, long MinimumBits, long MaximumBits, long ResultBits);

internal static class NormalFloat32Trace
{
    internal static NormalFloat32Call[] Dsl(IReadOnlyList<SessionDebugRecord> records, long dropped)
    {
        if (dropped != 0) throw new NotSupportedException("missing_dimension: dropped float random diagnostics");
        var calls = records.Where(r => r.Kind == SessionDebugKind.RandomCall && r.Random?.Operation == "double").Select(r =>
        {
            if (r.TextTruncated || string.IsNullOrEmpty(r.Subject) || r.Random?.Float32 is not { } value)
                throw new NotSupportedException("missing_dimension: actual float stream, range or result");
            return new NormalFloat32Call(r.Subject, r.Random.Before, r.Random.After, value.MinimumBits, value.MaximumBits, value.ResultBits);
        });
        return Canonical(calls);
    }

    internal static NormalFloat32Call[] Canonical(IEnumerable<NormalFloat32Call> source)
    {
        var calls = source.ToArray();
        foreach (var group in calls.GroupBy(c => c.Stream))
        {
            long previous = -1;
            foreach (var call in group)
            {
                if (call.Before <= previous || call.After != call.Before + 1)
                    throw new NotSupportedException("missing_dimension: duplicate, reordered or invalid float random cursor");
                previous = call.Before;
                var minimum = Binary32.Read(call.MinimumBits); var maximum = Binary32.Read(call.MaximumBits);
                var result = Binary32.Read(call.ResultBits);
                if (minimum > maximum || !float.IsFinite(maximum - minimum) || result < minimum || result > maximum)
                    throw new NotSupportedException("missing_dimension: invalid binary32 random parameters or result");
            }
        }
        // Separate streams can initialize in different global orders; preserve each stream's real cursors.
        return calls.OrderBy(c => c.Stream, StringComparer.Ordinal).ThenBy(c => c.Before).ToArray();
    }
}
