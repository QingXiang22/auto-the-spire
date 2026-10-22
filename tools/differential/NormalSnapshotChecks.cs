using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalSnapshotChecks
{
    internal static bool Complete(IReadOnlyList<SessionDebugRecord> records, long dropped) => dropped == 0
        && records.Any(r => r.Kind == SessionDebugKind.EventEmit) && records.Any(r => r.Kind == SessionDebugKind.RandomCall)
        && records.Where(r => r.Kind is SessionDebugKind.EventEmit or SessionDebugKind.RandomCall)
            .All(r => !r.TextTruncated && r.Snapshot is { Complete: true } && r.Random is not { CandidatesTruncated: true }
                && r.Random is not { Operation: "double", Float32: null } && CandidateReferencesComplete(r));

    private static bool CandidateReferencesComplete(SessionDebugRecord record)
    {
        if (record.Random?.Candidates is not { } candidates) return true;
        if (record.Snapshot is not { ReferenceDepth: 1, RelationsCaptured: true } snapshot) return false;
        foreach (var id in candidates)
        {
            var root = snapshot.Entities.SingleOrDefault(e => e.Instance == id);
            if (root is null) return false;
            foreach (var field in root.Fields)
            {
                var value = field.Value;
                if (value.Kind == ValueKind.Ref && !Copied(long.Parse(value.Scalar!))) return false;
                if (value.Kind == ValueKind.RefList && !value.Items!.All(Copied)) return false;
            }
        }
        if (snapshot.Relations.Any(r => !Copied(r.Owner.Value) || r.Targets.Any(id => !Copied(id.Value)))) return false;
        return true;
        bool Copied(long id) => id == 0 || snapshot.Entities.Any(e => e.Instance.Value == id && !e.Missing);
    }

    internal static void Run(string repository)
    {
        var directory = Path.Combine(repository, ".artifacts/differential/diagnostic-checks"); Directory.CreateDirectory(directory);
        using var observed = new NormalDslRun(repository, Path.Combine(directory, "snapshot-observed.ndjson"), captureValues: true);
        using var plain = new NormalDslRun(repository, Path.Combine(directory, "snapshot-plain.ndjson"), debug: false);
        observed.Start("777"); plain.Start("777");
        observed.Step(observed.Action("StartNormalRun")); plain.Step(plain.Action("StartNormalRun")); Check();
        foreach (var action in new[] { new NormalAction("neow", ["base-spire:booming-conch"]), new("leave-neow", []) }) Apply(action);
        Apply(observed.Semantic(observed.Action("ChooseMapPoint")));
        var moves = 0;
        while (observed.Phase == "combat" && moves++ < 100)
            Apply(observed.Semantic(new[] { "PlayAttackFromHand", "PlayCardFromHand", "EndDevelopmentTurn" }
                .Select(observed.Find).First(a => a is not null)!));
        Require(observed.Phase == "rewards", "the real normal run reaches its reward boundary");
        var records = observed.DebugRecords;
        Require(observed.HasCompleteDebug && Complete(records, 0), "all actual event/candidate snapshots are available and untruncated");
        var floats = records.Where(r => r.Random?.Float32 is not null).ToArray();
        Require(floats.Any(r => r.Random!.Float32 is { MinimumBits: 0, MaximumBits: 1073741824 }), "real enemy sampling retains the [0,2) range");
        Require(floats.Any(r => r.Subject!.Contains("rewards") && r.Random!.Float32 is { MinimumBits: 0, MaximumBits: 1065353216 }), "real reward sampling retains the [0,1) range");
        CheckFloat32Projection(records);
        var relatedIndex = records.ToList().FindIndex(r => r.Random?.Candidates is not null
            && r.Snapshot!.Relations.Any(edge => edge.Relation == "base-spire:OpeningTags"));
        Require(relatedIndex >= 0, "real normal initialization has historical candidate tag relations");
        var related = records[relatedIndex]; var relations = related.Snapshot!.Relations;
        var tag = relations.First(edge => edge.Relation == "base-spire:OpeningTags").Targets[0];
        Require(related.Snapshot.Entities.Single(e => e.Instance == tag) is { Type: "base-spire:OpeningTag" } copiedTag
            && copiedTag.Fields.Single(f => f.Name == "Name").Value.Scalar is { Length: > 0 }, "actual historical tag target fields are available");
        foreach (var bad in new[] { related.Snapshot with { RelationsCaptured = false }, related.Snapshot with { Relations = [] },
            related.Snapshot with { Entities = related.Snapshot.Entities.Where(e => e.Instance != tag).ToArray() },
            related.Snapshot with { Relations = relations.Select((edge, i) => i != 0 ? edge : edge with { Targets = [] }).ToArray() } })
        {
            var relationInjected = records.ToArray(); relationInjected[relatedIndex] = related with { Snapshot = bad };
            Require(!Complete(relationInjected, 0), "missing historical relation request, group, target or member cannot pass");
        }
        var expiredCandidates = records.Where(r => r.Random?.Candidates is not null)
            .SelectMany(r => r.Random!.Candidates!).Where(id => !observed.IsLive(id)).Distinct().ToArray();
        CheckPhaseProjection(records);
        Require(expiredCandidates.Length > 0, "this path actually destroys temporary initialization candidates");
        var opening = records.First(r => r.Random?.Candidates is not null && r.Snapshot!.Entities.Any(e => e.Type == "base-spire:OpeningListItem"));
        var item = opening.Snapshot!.Entities.First(e => e.Type == "base-spire:OpeningListItem");
        var definition = long.Parse(item.Fields.Single(f => f.Name == "Definition").Value.Scalar!);
        var copiedDefinition = opening.Snapshot.Entities.Single(e => e.Instance.Value == definition);
        Require(!observed.IsLive(item.Instance) && copiedDefinition.Type == "base-spire:OpeningDefinition"
            && copiedDefinition.Fields.Any(f => f.Name == "DefinitionId" && f.Value.Kind == ValueKind.String && f.Value.Scalar!.StartsWith("base-spire:", StringComparison.Ordinal)),
            "destroyed initialization candidate retains its actual indirect content definition");
        var injected = records.ToArray(); var openingIndex = records.ToList().IndexOf(opening);
        injected[openingIndex] = opening with { Snapshot = opening.Snapshot with { ReferenceDepth = 0 } };
        Require(!Complete(injected, 0), "missing requested candidate reference depth cannot pass");
        injected[openingIndex] = opening with { Snapshot = opening.Snapshot with { Entities = opening.Snapshot.Entities.Where(e => e != copiedDefinition).ToArray() } };
        Require(!Complete(injected, 0), "missing indirect candidate definition cannot pass");
        foreach (var record in records.Where(r => r.Random?.Candidates is not null))
            Require(record.Random!.Candidates!.All(id => record.Snapshot!.Entities.Any(e => e.Instance == id)), "every ordered candidate has its own copied entity");
        var damage = records.Where(r => r.Kind == SessionDebugKind.EventEmit && r.Subject == "base-spire:DamageResolved").ToArray();
        Require(damage.Length > 0, "the normal path produces real damage events");
        foreach (var record in damage)
        {
            var entity = record.Snapshot!.Entities.Single();
            Require(entity.Type == "base-spire:DamageResult" && !observed.IsLive(entity.Instance), "damage data survives real destruction");
            foreach (var field in new[] { "DealerPlayer", "DealerEnemy", "Player", "Enemy", "Blocked", "Unblocked", "Overkill", "Killed" })
                Require(entity.Fields.Any(f => f.Name == field), "historical damage field " + field);
        }
        Require(damage[0].Snapshot!.Entities.Single().Fields.Single(f => f.Name == "Unblocked").Value is { Kind: ValueKind.Int, Scalar: "8" }, "first Bash damage copied at emission");
        Require(damage[^1].Snapshot!.Entities.Single().Fields.Single(f => f.Name == "Killed").Value is { Kind: ValueKind.Bool, Scalar: "true" }, "final kill retained");
        var index = records.ToList().FindIndex(r => r.Kind == SessionDebugKind.RandomCall);
        var missing = records.ToArray(); missing[index] = missing[index] with { Snapshot = null };
        Require(!Complete(missing, 0), "missing snapshot fails the consumer check");
        missing[index] = records[index] with { Snapshot = records[index].Snapshot! with { EntitiesTruncated = true } };
        Require(!Complete(missing, 0) && !Complete(records, 1) && !Complete([], 0), "truncation, dropped records and zero execution cannot pass");
        missing[index] = records[index] with { TextTruncated = true };
        Require(!Complete(missing, 0), "truncated stream/event labels cannot pass");
        var floatIndex = records.ToList().FindIndex(r => r.Random?.Float32 is not null);
        missing = records.ToArray();
        missing[floatIndex] = records[floatIndex] with { Random = records[floatIndex].Random! with { Float32 = null } };
        Require(!Complete(missing, 0), "missing actual float range and result cannot pass");
        File.WriteAllText(Path.Combine(directory, "historical-snapshots.json"), NormalPreflight.Json(new
        {
            schema = "normal-run/historical-snapshots@1", seed = "777", independent_verified = false,
            production_manifest_sha256 = NormalPreflight.Hash(Path.Combine(repository, "mod-the-spire/base-spire/package.json")),
            expired_candidates = expiredCandidates.Length, damage_events = damage.Length,
            records = records.Where(r => r.Random?.Candidates is not null || damage.Contains(r)).ToArray()
        }));
        Console.WriteLine($"Normal historical snapshots: {expiredCandidates.Length} expired candidates and {damage.Length} destroyed damage payloads retained; diagnostic on/off gameplay, RNG and full public snapshots match.");

        void Apply(NormalAction action) { observed.Apply(action); plain.Apply(action); Check(); }
        void Check()
        {
            Require(observed.StateHash == plain.StateHash && observed.RandomState == plain.RandomState
                && observed.PublicState == plain.PublicState, "copying leaves every real normal boundary unchanged");
        }
    }

    private static void CheckPhaseProjection(IReadOnlyList<SessionDebugRecord> records)
    {
        var starts = records.Where(r => r.Kind == SessionDebugKind.EventEmit && r.Subject == "base-spire:EnemyTurnStarted")
            .ToDictionary(r => r.Sequence, r =>
            {
                var enemy = r.Snapshot!.Entities.Single(e => e.Type == "base-spire:Enemy");
                return new NormalObservedEvent("enemy-move-started", Actor: enemy.Instance.ToString(),
                    Move: enemy.Fields.Single(f => f.Name == "MoveId").Value.Scalar!);
            });
        var actual = NormalPhaseTrace.Merge(records, starts);
        Require(actual.Count(e => e.Kind == "enemy-move-finished") == starts.Count && starts.Count > 0, "every actual enemy move completes once with its original actor and move");
        var prepared = records.First(r => r.Kind == SessionDebugKind.ProcedureExit && r.Subject == "base-spire:DrawDevelopmentTurnHand");
        Require(!actual.SequenceEqual(NormalPhaseTrace.Merge(records.Where(r => r != prepared).ToArray(), starts)), "missing actual draw-completion phase changes evidence");
        Require(!actual.SequenceEqual(NormalPhaseTrace.Merge(records.SelectMany(r => r == prepared ? new[] { r, r } : new[] { r }).ToArray(), starts)), "duplicate actual phase changes evidence");
        foreach (var subject in new[] { "base-spire:RunEnemyTurn", "base-spire:PerformEnemyMove" })
        {
            var finish = records.First(r => r.Kind == SessionDebugKind.ProcedureExit && r.Subject == subject);
            Reject(records.Where(r => r != finish).ToArray(), "missing " + subject + " completion");
        }
        Reject(records.Select(r => r != prepared ? r : r with { TextTruncated = true }).ToArray(), "truncated phase");
        void Reject(IReadOnlyList<SessionDebugRecord> injected, string name)
        {
            var rejected = false;
            try { NormalPhaseTrace.Merge(injected, starts); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "phase comparison rejects " + name);
        }
    }

    private static void CheckFloat32Projection(IReadOnlyList<SessionDebugRecord> records)
    {
        var actual = NormalFloat32Trace.Dsl(records, 0);
        Require(actual.Length > 1 && actual.Select(c => c.Stream).Distinct().Count() > 1, "normal combat has real float draws in independent streams");
        Require(actual.SequenceEqual(NormalFloat32Trace.Dsl(records.OrderBy(r => r.Subject, StringComparer.Ordinal).ToArray(), 0)),
            "independent streams do not impose a cross-stream execution order");
        var index = records.ToList().FindIndex(r => r.Random?.Float32 is not null);
        var record = records[index]; var value = record.Random!.Float32!;
        foreach (var changed in new[] { record with { Random = record.Random with { Float32 = value with { MaximumBits = 1077936128 } } },
            record with { Random = record.Random with { Float32 = value with { ResultBits = value.ResultBits + 1 } } } })
        {
            var injected = records.ToArray(); injected[index] = changed;
            Require(!actual.SequenceEqual(NormalFloat32Trace.Dsl(injected, 0)), "same raw double cannot hide changed actual float bounds or result");
        }
        Require(!actual.SequenceEqual(NormalFloat32Trace.Dsl(records.Where((_, i) => i != index).ToArray(), 0)), "a missing actual float call changes compared evidence");
        Reject(records, 1, "dropped records");
        var missing = records.ToArray(); missing[index] = record with { Random = record.Random with { Float32 = null } };
        Reject(missing, 0, "missing evaluated bounds");
        missing[index] = record with { TextTruncated = true }; Reject(missing, 0, "truncated stream");
        missing[index] = record with { Random = record.Random with { After = record.Random.After + 1 } }; Reject(missing, 0, "invalid cursor");
        Reject([.. records, record], 0, "duplicate cursor");
        Reject(records.Reverse().ToArray(), 0, "within-stream reordering");
        void Reject(IReadOnlyList<SessionDebugRecord> injected, long dropped, string name)
        {
            var rejected = false;
            try { NormalFloat32Trace.Dsl(injected, dropped); } catch (NotSupportedException) { rejected = true; }
            Require(rejected, "float comparison rejects " + name);
        }
    }

    private static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Normal snapshot check failed: " + name); }
}
