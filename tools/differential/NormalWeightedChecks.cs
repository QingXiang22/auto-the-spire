using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalWeightedChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, NormalBoundary boundary)
    {
        var actual = NormalWeightedTrace.Dsl(records, 0);
        Require(actual.Length == 94 && actual.SelectMany(c => c.Candidates.Entries).Select(e => e.Act).Distinct().Order().SequenceEqual(new long[] { 0, 1, 2 })
            && actual.SelectMany(c => c.Candidates.Entries).Select(e => (e.Act, e.Room, e.Weak)).Distinct().Count() == 9,
            "all 94 actual weighted attempts include all three acts and weak, normal and elite bags");
        foreach (var injected in new[] { actual[..^1], actual.Concat(actual.Take(1)).ToArray(), actual.Reverse().ToArray(),
            new[] { actual[0] with { Selected = (actual[0].Selected + 1) % actual[0].Candidates.Entries.Length } }.Concat(actual.Skip(1)).ToArray(),
            new[] { actual[0] with { Before = actual[0].Before + 1 } }.Concat(actual.Skip(1)).ToArray(),
            new[] { actual[0] with { Candidates = actual[0].Candidates with { Entries = actual[0].Candidates.Entries.Reverse().ToArray() } } }.Concat(actual.Skip(1)).ToArray() })
        {
            var values = new SortedDictionary<string, string>(boundary.Values, StringComparer.Ordinal)
                { ["random.weighted_candidates"] = NormalPreflight.Json(injected) };
            Require(NormalRunComparison.First(0, boundary, boundary with { Values = values })?.Path == "random.weighted_candidates",
                "missing, repeated, reordered, wrong result, cursor and candidate order produce weighted first differences");
        }
        var index = records.ToList().FindIndex(r => r.Random?.Before == actual[0].Before && r.Subject == actual[0].Stream);
        var record = records[index]; var snapshot = record.Snapshot!;
        Require(actual[0].Candidates.Entries[0].Tags.SequenceEqual(new[] { "Crawler" }), "first actual FuzzyWurmCrawler candidate retains its source compatibility tag");
        Require(actual.SelectMany(c => c.Candidates.Entries).First(e => e.Id == "base-spire:flyconid-normal").Tags.SequenceEqual(new[] { "Mushroom", "Slimes" }),
            "actual Flyconid candidates retain both source compatibility tags");
        var edge = snapshot.Relations.First(r => r.Relation == "base-spire:OpeningTags");
        var tag = snapshot.Entities.Single(e => e.Instance == edge.Targets[0]);
        var tagged = record with { Snapshot = snapshot with { Entities = snapshot.Entities.Select(e => e != tag ? e : e with
            { Fields = e.Fields.Select(f => f.Name != "Name" ? f : f with { Value = f.Value with { Scalar = "Changed" } }).ToArray() }).ToArray() } };
        var tagMutation = records.ToArray(); tagMutation[index] = tagged;
        Require(NormalPreflight.Json(actual) != NormalPreflight.Json(NormalWeightedTrace.Dsl(tagMutation, 0)), "historical tag values participate in weighted evidence");
        var multiIndex = records.ToList().FindIndex(r => r.Random?.Operation == "pick" && r.Snapshot!.Entities.Any(e => e.Type == "base-spire:OpeningListItem")
            && r.Snapshot.Relations.Any(e => e.Relation == "base-spire:OpeningTags" && e.Targets.Count > 1));
        Require(multiIndex >= 0, "actual candidate snapshot contains a multi-tag relation");
        var multi = records[multiIndex];
        var multiEdge = multi.Snapshot!.Relations.First(e => e.Relation == "base-spire:OpeningTags" && e.Targets.Count > 1);
        tagMutation = records.ToArray();
        tagMutation[multiIndex] = multi with { Snapshot = multi.Snapshot with { Relations = multi.Snapshot.Relations.Select(r => r != multiEdge ? r : r with { Targets = r.Targets.Reverse().ToArray() }).ToArray() } };
        Require(NormalPreflight.Json(actual) == NormalPreflight.Json(NormalWeightedTrace.Dsl(tagMutation, 0)), "compatibility tag membership is unordered while candidate order remains significant");
        Reject(record with { Snapshot = snapshot with { RelationsCaptured = false } });
        Reject(record with { Snapshot = snapshot with { Relations = [] } });
        Reject(record with { Snapshot = snapshot with { Entities = snapshot.Entities.Where(e => e != tag).ToArray() } });
        var definition = snapshot.Entities.First(e => e.Type == "base-spire:OpeningDefinition");
        var changed = record with { Snapshot = snapshot with { Entities = snapshot.Entities.Select(e => e != definition ? e : e with
            { Fields = e.Fields.Select(f => f.Name != "DefinitionId" ? f : f with { Value = f.Value with { Scalar = "base-spire:changed" } }).ToArray() }).ToArray() } };
        var mutation = records.ToArray(); mutation[index] = changed;
        Require(NormalPreflight.Json(actual) != NormalPreflight.Json(NormalWeightedTrace.Dsl(mutation, 0)),
            "the actual historical definition, rather than its final state, determines candidate identity");
        Reject(record with { Snapshot = snapshot with { Entities = snapshot.Entities.Where(e => e != definition).ToArray() } });
        Reject(record with { Snapshot = snapshot with { ReferenceDepth = 0 } });
        Reject(record with { Snapshot = snapshot with { EntitiesTruncated = true } });
        Reject(record with { TextTruncated = true });
        Reject(record with { Random = record.Random! with { CandidatesTruncated = true } });
        Reject(record with { Random = record.Random! with { Maximum = "999" } });
        Reject(record with { Random = record.Random! with { Result = "999" } });
        Reject(record with { Random = record.Random! with { After = record.Random.After + 1 } });
        Reject(record, 1);
        var copied = actual[0].Candidates;
        var weights = copied.Entries.Select((e, i) => i == 0 ? e with { WeightBits = BitConverter.DoubleToInt64Bits(2d) } : e).ToArray();
        var changedWeights = actual.ToArray();
        changedWeights[0] = actual[0] with { Candidates = copied with { Entries = weights, TotalBits = BitConverter.DoubleToInt64Bits(weights.Length + 1d) } };
        var weightedValues = new SortedDictionary<string, string>(boundary.Values, StringComparer.Ordinal)
            { ["random.weighted_candidates"] = NormalPreflight.Json(changedWeights) };
        Require(NormalRunComparison.First(0, boundary, boundary with { Values = weightedValues })?.Path == "random.weighted_candidates",
            "changed actual weight and total report a weighted first difference");
        void Reject(SessionDebugRecord bad, long dropped = 0)
        {
            var invalid = records.ToArray(); invalid[index] = bad;
            var rejected = false;
            try { NormalWeightedTrace.Dsl(invalid, dropped); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "missing, truncated or invalid weighted historical data fails");
        }
    }
    private static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Normal weighted check failed: " + name); }
}
