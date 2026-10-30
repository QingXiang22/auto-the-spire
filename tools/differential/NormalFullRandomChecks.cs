using System.Text.Json;
using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalFullRandomChecks
{
    internal static void Run(IReadOnlyList<SessionDebugRecord> records, RandomSnapshot snapshot, string[][] encounterSources, NormalDslMoveBranch[] branches, NormalBoundary left, NormalBoundary right)
    {
        var actual = JsonSerializer.Deserialize<NormalFullRandomHistory>(left.Values["random.trace"])!;
        Require(left.Values["random.trace"] == right.Values["random.trace"] && left.Values["random.roots"] == right.Values["random.roots"],
            "complete source semantics, seed derivations, scalar results and both actual states agree");
        Require(actual.Calls.Length == 736 && actual.Roots.Seeds.Length == 18 && actual.Calls.Length == snapshot.TotalDraws
            && actual.Calls.Select(c => (c.Stream, c.Before)).Distinct().Count() == actual.Calls.Length,
            "one full record for every original actual draw, retaining unused named seeds");
        Require(actual.Calls.Count(c => c.Context == "combat/encounter") == 3 && actual.Calls.Count(c => c.Context == "combat/hp") == 3
            && actual.Calls.Count(c => c.Context == "combat/weighted-move") == 3 && actual.Calls.Count(c => c.Context.StartsWith("map/", StringComparison.Ordinal)) == 264,
            "encounter singleton, creation HP, actual weighted moves and all map phases retain semantic candidates");
        Require(left.MissingDimensions.All(d => !d.StartsWith("random:", StringComparison.Ordinal)) && right.MissingDimensions.All(d => !d.StartsWith("random:", StringComparison.Ordinal)),
            "random is complete while independent event certification remains separate");
        var values = new SortedDictionary<string, string>(left.Values, StringComparer.Ordinal);
        void Bad(string path, object changed)
        {
            var copy = new SortedDictionary<string, string>(values, StringComparer.Ordinal) { [path] = NormalPreflight.Json(changed) };
            Reject(() => NormalFullRandomTrace.Capture(copy));
        }
        foreach (var path in new[] { "random.roots", "random.step_states", "random.scalar_calls", "random.float32_calls", "random.initialization_candidates",
            "random.weighted_candidates", "random.weighted_attempts", "random.neow_candidates", "random.map_candidates", "random.shuffle_candidates", "random.reward_candidates", "random.combat_candidates" })
        {
            var copy = new SortedDictionary<string, string>(values, StringComparer.Ordinal); copy.Remove(path); Reject(() => NormalFullRandomTrace.Capture(copy));
        }
        var map = JsonSerializer.Deserialize<NormalMapCandidates>(values["random.map_candidates"])!;
        Bad("random.map_candidates", map with { Calls = map.Calls[..^1] });
        Bad("random.reward_candidates", JsonSerializer.Deserialize<NormalRewardCandidateCall[]>(values["random.reward_candidates"])![..^1]);
        Bad("random.weighted_candidates", JsonSerializer.Deserialize<NormalWeightedCall[]>(values["random.weighted_candidates"])![..^1]);
        Bad("random.shuffle_candidates", JsonSerializer.Deserialize<NormalShuffleCall[]>(values["random.shuffle_candidates"])![..^1]);
        var combat = JsonSerializer.Deserialize<NormalCombatCandidates>(values["random.combat_candidates"])!;
        Require(combat.Picks.Count(p => p.Kind == "encounter" && p.Candidates.Length == 1) == 1, "actual encounter singleton retains its draw and sole source");
        foreach (var name in new[] { "CreateSourceEncounterEnemy", "CreateEncounterEnemy", "RollEncounterEnemyHp", "ResolveEnemyMoveNode", "SaveEnemyMove" })
        {
            var record = records.First(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:" + name);
            Reject(() => NormalDslCombatCandidates.Capture(records.Select(r => r == record ? r with { Snapshot = null } : r).ToArray(), 0, encounterSources, branches));
            Reject(() => NormalDslCombatCandidates.Capture(records.Select(r => r == record ? r with { TextTruncated = true } : r).ToArray(), 0, encounterSources, branches));
        }
        Reject(() => NormalDslCombatCandidates.Capture(records, 1, encounterSources, branches));
        var birth = records.First(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:CreateEncounterEnemy");
        Reject(() => NormalDslCombatCandidates.Capture(records.Select(r => r == birth ? r with { Snapshot = r.Snapshot! with
        { Values = [r.Snapshot!.Values[0], r.Snapshot.Values[1] with { Scalar = "999" }] } } : r).ToArray(), 0, encounterSources, branches));
        Bad("random.combat_candidates", combat with { Moves = combat.Moves[..^1] });
        Bad("random.combat_candidates", combat with { Picks = combat.Picks[..^1] });
        var neow = JsonSerializer.Deserialize<NormalNeowCandidates>(values["random.neow_candidates"])!;
        Bad("random.neow_candidates", neow with { Calls = neow.Calls[..^1] });
        var initial = JsonSerializer.Deserialize<NormalInitializationCandidates>(values["random.initialization_candidates"])!;
        Bad("random.initialization_candidates", initial with { Allocations = initial.Allocations[..^1] });
        var steps = JsonSerializer.Deserialize<NormalRandomStateStep[]>(values["random.step_states"])!;
        Bad("random.step_states", steps[..^1]);
        var roots = actual.Roots;
        var used = roots.Seeds.First(s => steps.Any(step => step.Stream == s.Stream));
        Bad("random.roots", roots with { Seeds = roots.Seeds.Where(s => s != used).ToArray() });
        Bad("random.roots", roots with { Seeds = [.. roots.Seeds, used] });
        var broken = roots.Seeds.Select(s => s != used ? s : s with { InitialWords = ["0000000000000000", .. s.InitialWords[1..]] }).ToArray();
        Bad("random.roots", roots with { Seeds = broken });
        foreach (var seed in snapshot.ExplicitSeeds)
        {
            var changed = snapshot.ExplicitSeeds.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal); changed[seed.Key] ^= 1UL;
            Reject(() => NormalRandomRoots.Dsl(roots.Input, snapshot with { ExplicitSeeds = changed }, steps, "SLIMES_WEAK", 1));
        }
        var altered = actual.Calls.ToArray(); altered[0] = altered[0] with { Semantics = "[]" };
        var changedValues = new SortedDictionary<string, string>(values, StringComparer.Ordinal) { ["random.trace"] = NormalPreflight.Json(actual with { Calls = altered }) };
        Require(NormalRunComparison.First(0, left, left with { Values = changedValues })?.Path == "random.trace", "changed full source evidence is visible even when scalar draws match");
        var changedMove = combat.Moves[0] with { Candidates = combat.Moves[0].Candidates.Reverse().ToArray() };
        var changedCombat = new SortedDictionary<string, string>(values, StringComparer.Ordinal)
            { ["random.combat_candidates"] = NormalPreflight.Json(combat with { Moves = [changedMove, .. combat.Moves[1..]] }) };
        Require(NormalPreflight.Json(NormalFullRandomTrace.Capture(changedCombat)) != values["random.trace"], "move source order remains visible even with identical numeric draws");
        Console.WriteLine("Normal full random trace: all 736 calls, 18 actual named seed derivations, ordered candidates, complete parameters and before/after states agree.");
    }
    private static void Reject(Action action)
    { var rejected = false; try { action(); } catch (NotSupportedException) { rejected = true; } Require(rejected, "missing or contradictory full random evidence must fail"); }
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("Normal full random check failed: " + reason); }
}
