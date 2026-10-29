using System.Globalization;
using CoreTheSpire.Engine;

namespace Differential;

internal sealed record NormalShuffleCall(long Before, long After, int Selected, string[] Candidates);

internal static class NormalShuffleTrace
{
    internal static NormalShuffleCall[] Dsl(IReadOnlyList<SessionDebugRecord> records, long dropped, IReadOnlyDictionary<InstanceId, string> identities)
    {
        if (dropped != 0) throw Missing("dropped shuffle records");
        var calls = new List<NormalShuffleCall>();
        foreach (var record in records.Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == "base-spire:run:shuffle"))
        {
            if (record.TextTruncated || record.Random is not { Operation: "shuffle", Candidates: { } roots, CandidatesTruncated: false } draw
                || record.Snapshot is not { Complete: true } snapshot || roots.Count != draw.CandidateCount || roots.Count < 2
                || draw.Minimum != "0" || draw.Maximum != roots.Count.ToString(CultureInfo.InvariantCulture))
                throw Missing("incomplete shuffle candidate snapshot");
            var entities = snapshot.Entities.ToDictionary(e => e.Instance);
            var candidates = roots.Select(id =>
            {
                if (!entities.TryGetValue(id, out var card) || card.Type != "base-spire:Card" || !identities.TryGetValue(id, out var identity))
                    throw Missing("unidentified historical shuffle card");
                var definition = card.Fields.Single(f => f.Name == "DefinitionId").Value;
                var upgraded = card.Fields.Single(f => f.Name == "Upgraded").Value;
                if (definition.Truncated || upgraded.Truncated || definition.Scalar is null || upgraded.Scalar is not ("true" or "false")
                    || !identity.EndsWith(":" + definition.Scalar + (upgraded.Scalar == "true" ? "+" : ""), StringComparison.Ordinal))
                    throw Missing("historical card identity changed or truncated");
                return identity;
            }).ToArray();
            calls.Add(new(draw.Before, draw.After, int.Parse(draw.Result, CultureInfo.InvariantCulture), candidates));
        }
        return Canonical(calls);
    }

    internal static NormalShuffleCall[] Canonical(IEnumerable<NormalShuffleCall> source)
    {
        var calls = source.ToArray();
        for (var i = 0; i < calls.Length; i++)
        {
            var c = calls[i];
            if (c.Before != i || c.After != i + 1 || c.Candidates.Length < 2 || c.Selected < 0 || c.Selected >= c.Candidates.Length
                || c.Candidates.Any(string.IsNullOrWhiteSpace) || c.Candidates.Distinct(StringComparer.Ordinal).Count() != c.Candidates.Length)
                throw Missing("invalid or discontinuous shuffle candidates");
            if (c.Candidates.Length > 2)
            {
                var expected = c.Candidates.ToArray();
                (expected[^1], expected[c.Selected]) = (expected[c.Selected], expected[^1]);
                if (i + 1 == calls.Length || !expected[..^1].SequenceEqual(calls[i + 1].Candidates))
                    throw Missing("shuffle candidates do not follow the actual preceding swap");
            }
        }
        return calls;
    }

    internal static NotSupportedException Missing(string reason) => new("missing_dimension: " + reason);
}
