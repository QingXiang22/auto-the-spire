using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Differential;

internal sealed record RandomDraw(string Stream, string Operation, string Minimum, string Maximum, string Result,
    long Before, long After, string[]? Candidates, string? Source)
{
    public string? CandidateDigest => Candidates is null ? null :
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Candidates))));
}
internal sealed record StreamPosition(string Stream, long Start, long Cursor, string State);
internal sealed record RandomTrace(RandomDraw[] Calls, long Dropped, bool Truncated, StreamPosition[] Positions);
internal sealed record RandomDifference(string Path, string? Left, string? Right);

internal static class RandomComparison
{
    // Explicit reference/run/player name mapping for the original controlled fixture.
    private static readonly string[] Streams = ["base-spire:player:rewards", "base-spire:player:shops", "base-spire:player:transformations",
        .. new[] { "up_front", "shuffle", "unknown_map_point", "combat_card_generation", "combat_potion_generation",
            "combat_card_selection", "combat_energy_costs", "combat_targets", "monster_ai", "niche", "combat_orbs", "treasure_room_relics" }
            .Select(n => "base-spire:run:" + n)];

    public static bool Complete(RandomTrace? trace)
    {
        if (trace is null || trace.Dropped != 0 || trace.Truncated || trace.Positions.Length == 0
            || trace.Positions.Any(p => string.IsNullOrWhiteSpace(p.Stream) || string.IsNullOrWhiteSpace(p.State))
            || trace.Positions.Select(p => p.Stream).Distinct().Count() != trace.Positions.Length) return false;
        if (!trace.Positions.Select(p => p.Stream).Order(StringComparer.Ordinal).SequenceEqual(Streams.Order(StringComparer.Ordinal))) return false;
        foreach (var group in trace.Calls.GroupBy(c => c.Stream))
        {
            long? previous = trace.Positions.SingleOrDefault(p => p.Stream == group.Key)?.Start;
            foreach (var call in group)
            {
                if (call.After != call.Before + 1 || (previous is not null && previous != call.Before)
                    || string.IsNullOrWhiteSpace(call.Operation) || string.IsNullOrWhiteSpace(call.Result)
                    || string.IsNullOrWhiteSpace(call.Minimum) || string.IsNullOrWhiteSpace(call.Maximum)
                    || (call.Operation == "shuffle" && (call.Candidates is null
                        || call.Candidates.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) != call.Maximum))) return false;
                previous = call.After;
            }
            if (trace.Positions.SingleOrDefault(p => p.Stream == group.Key)?.Cursor != previous) return false;
        }
        // Every consumed word must have a record; equal cursors alone cannot certify an empty trace.
        return trace.Positions.All(p => p.Start >= 0 && p.Cursor >= p.Start
            && trace.Calls.LongCount(c => c.Stream == p.Stream) == p.Cursor - p.Start);
    }

    public static RandomDifference? First(RandomTrace left, RandomTrace right)
    {
        if (!Complete(left) || !Complete(right)) return new("trace.missing", "complete=" + Complete(left), "complete=" + Complete(right));
        for (var i = 0; i < Math.Min(left.Calls.Length, right.Calls.Length); i++)
        {
            var a = left.Calls[i]; var b = right.Calls[i];
            foreach (var (path, x, y) in new (string, string?, string?)[]
            {
                ("stream", a.Stream, b.Stream), ("operation", a.Operation, b.Operation),
                ("candidates", a.CandidateDigest, b.CandidateDigest), ("minimum", a.Minimum, b.Minimum),
                ("maximum", a.Maximum, b.Maximum), ("before", a.Before.ToString(), b.Before.ToString()),
                ("after", a.After.ToString(), b.After.ToString()), ("result", a.Result, b.Result),
            })
                if (x != y)
                {
                    if (path == "candidates" && a.Candidates is not null && b.Candidates is not null)
                    {
                        var n = Enumerable.Range(0, Math.Min(a.Candidates.Length, b.Candidates.Length))
                            .FirstOrDefault(j => a.Candidates[j] != b.Candidates[j], -1);
                        return new($"random[{i}].candidates[{(n < 0 ? Math.Min(a.Candidates.Length, b.Candidates.Length) : n)}]",
                            JsonSerializer.Serialize(a.Candidates), JsonSerializer.Serialize(b.Candidates));
                    }
                    return new($"random[{i}].{path}", x, y);
                }
        }
        if (left.Calls.Length != right.Calls.Length)
            return new("random.count", left.Calls.Length.ToString(), right.Calls.Length.ToString());
        var names = left.Positions.Select(p => p.Stream).Union(right.Positions.Select(p => p.Stream)).Order(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var a = left.Positions.SingleOrDefault(p => p.Stream == name); var b = right.Positions.SingleOrDefault(p => p.Stream == name);
            if (a is null || b is null) return new($"streams[{name}]", JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
            foreach (var (field, x, y) in new[] { ("start", a.Start.ToString(), b.Start.ToString()),
                ("cursor", a.Cursor.ToString(), b.Cursor.ToString()), ("state", a.State, b.State) })
                if (x != y) return new($"streams[{name}].{field}", x, y);
        }
        return null;
    }
}
