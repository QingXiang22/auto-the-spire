using System.Numerics;
using CoreTheSpire.Engine;

namespace Differential;

// The DSL does not expose per-draw states. Invert its actual boundary state and
// verify every reconstructed step against the independently recorded raw word.
internal sealed record NormalRandomStateStep(string Stream, long Before, long After, string[] BeforeWords, string[] AfterWords);

internal static class NormalRandomStateTrace
{
    internal static NormalRandomStateStep[] Dsl(IReadOnlyList<SessionDebugRecord> records, long dropped, RandomSnapshot snapshot)
    {
        if (dropped != 0 || snapshot.AlgorithmId != RandomService.VanillaAlgorithmId || snapshot.DrawProfileId != RandomService.VanillaDrawProfileId)
            throw Missing("incomplete diagnostics or unsupported random profile");
        var draws = records.Where(r => r.Kind == SessionDebugKind.RandomCall).ToArray();
        if (draws.LongLength != snapshot.TotalDraws) throw Missing("random record count does not cover all actual words");
        for (var i = 0; i < draws.Length; i++)
        {
            var record = draws[i]; var call = record.Random;
            if (record.TextTruncated || string.IsNullOrWhiteSpace(record.Subject) || call is null || call.CandidatesTruncated
                || call.TotalBefore != i || call.TotalAfter != i + 1 || call.After != call.Before + 1 || call.SingleRawWord is null)
                throw Missing("truncated, missing, multi-word or discontinuous actual draw");
        }
        if (snapshot.Streams.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count() != snapshot.Streams.Count)
            throw Missing("duplicate stream identity");
        var streams = snapshot.Streams.ToDictionary(s => s.Name, StringComparer.Ordinal);
        var result = new List<NormalRandomStateStep>(draws.Length);
        foreach (var group in draws.GroupBy(r => r.Subject!, StringComparer.Ordinal))
        {
            if (!streams.TryGetValue(group.Key, out var stream) || stream.BackendState is not { Count: 4 } backend
                || !backend.SequenceEqual(new[] { stream.S0, stream.S1, stream.S2, stream.S3 }))
                throw Missing("missing or incompatible actual stream state");
            var state = backend.ToArray(); var cursor = stream.Draws;
            foreach (var record in group.Reverse())
            {
                var call = record.Random!;
                if (call.After != cursor) throw Missing("non-contiguous per-stream cursor");
                var previous = Invert(state);
                if (Output(previous) != call.SingleRawWord) throw Missing("reconstructed state disagrees with actual raw word");
                result.Add(new(group.Key, call.Before, call.After, Words(previous), Words(state)));
                state = previous; cursor = call.Before;
            }
            if (cursor != 0) throw Missing("random history does not start at the stream's first draw");
        }
        if (snapshot.Streams.Sum(s => s.Draws) != snapshot.TotalDraws) throw Missing("unobserved stream draws");
        return Canonical(result.AsEnumerable().Reverse());
    }

    internal static NormalRandomStateStep[] Canonical(IEnumerable<NormalRandomStateStep> source)
    {
        var result = source.ToArray();
        foreach (var group in result.GroupBy(s => s.Stream, StringComparer.Ordinal))
        {
            long cursor = 0; string[]? previous = null;
            foreach (var step in group)
            {
                if (string.IsNullOrWhiteSpace(step.Stream) || step.Stream.StartsWith("unidentified:", StringComparison.Ordinal)
                    || step.Before != cursor || step.After != cursor + 1 || step.BeforeWords.Length != 4 || step.AfterWords.Length != 4
                    || step.BeforeWords.Any(w => !IsWord(w)) || step.AfterWords.Any(w => !IsWord(w))
                    || (previous is not null && !previous.SequenceEqual(step.BeforeWords)))
                    throw Missing("invalid or discontinuous per-stream state trace");
                previous = step.AfterWords; cursor = step.After;
            }
        }
        return result.OrderBy(s => s.Stream, StringComparer.Ordinal).ThenBy(s => s.Before).ToArray();
    }

    private static bool IsWord(string value) => value is { Length: 16 } && ulong.TryParse(value, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out _);

    internal static string[] Words(params ulong[] state) => state.Select(w => w.ToString("x16", System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    private static ulong[] Invert(IReadOnlyList<ulong> state)
    {
        var (x, y, z, w) = (state[0], state[1], state[2], state[3]);
        var q = BitOperations.RotateRight(w, 45); var a = x ^ q; var t = y ^ z;
        var b = t ^ (t << 17) ^ (t << 34) ^ (t << 51);
        return [a, b, y ^ b ^ a, q ^ b];
    }
    private static ulong Output(IReadOnlyList<ulong> state) => unchecked(BitOperations.RotateLeft(state[1] * 5UL, 7) * 9UL);
    private static NotSupportedException Missing(string reason) => new("missing_dimension: " + reason);
}
