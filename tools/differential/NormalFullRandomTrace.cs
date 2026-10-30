using System.Globalization;
using System.Text.Json;

namespace Differential;

internal sealed record NormalFullRandomCall(string Stream, long Before, long After, string Operation,
    string Minimum, string Maximum, string Result, string Context, string Semantics, string[] BeforeWords, string[] AfterWords);
internal sealed record NormalFullRandomHistory(NormalRandomRoot Roots, NormalFullRandomCall[] Calls);

internal static class NormalFullRandomTrace
{
    internal static NormalFullRandomHistory Capture(IReadOnlyDictionary<string, string> values)
    {
        T Read<T>(string path) => values.TryGetValue(path, out var value) ? JsonSerializer.Deserialize<T>(value)!
            : throw NormalRandomRoots.Missing("required full-trace evidence: " + path);
        var states = NormalRandomStateTrace.Canonical(Read<NormalRandomStateStep[]>("random.step_states"));
        var roots = NormalRandomRoots.Validate(Read<NormalRandomRoot>("random.roots"), states);
        var scalar = Read<NormalScalarCall[]>("random.scalar_calls").ToDictionary(c => (c.Stream, c.Before));
        var floats = Read<NormalFloat32Call[]>("random.float32_calls").ToDictionary(c => (c.Stream, c.Before));
        var sources = new Dictionary<(string Stream, long Before), (string Context, string Semantics)>();
        void Add<T>(string stream, long before, string context, T value)
        {
            if (!sources.TryAdd((stream, before), (context, NormalPreflight.Json(value))))
                throw NormalRandomRoots.Missing("overlapping full-trace source classification");
        }
        var initial = Read<NormalInitializationCandidates>("random.initialization_candidates");
        foreach (var c in initial.Shuffles) Add(NormalInitializationTrace.Stream, c.Before, "initial-shuffle", c);
        foreach (var c in initial.Allocations) Add(NormalInitializationTrace.Stream, c.Before, "ancient-allocation", c);
        foreach (var c in initial.Picks) Add(NormalInitializationTrace.Stream, c.Before, "initial-pick", c);
        var attempts = Read<NormalOpeningAttempt[]>("random.weighted_attempts").ToDictionary(a => a.Before);
        var weighted = Read<NormalWeightedCall[]>("random.weighted_candidates");
        if (attempts.Count != weighted.Length) throw NormalRandomRoots.Missing("full trace weighted rejection coverage");
        foreach (var c in weighted)
        {
            if (!attempts.TryGetValue(c.Before, out var attempt)) throw NormalRandomRoots.Missing("missing actual weighted acceptance/rejection");
            Add(c.Stream, c.Before, "weighted-encounter", new { Draw = c, Attempt = attempt });
        }
        foreach (var c in Read<NormalNeowCandidates>("random.neow_candidates").Calls) Add(NormalNeowTrace.Stream, c.Before, "neow/" + c.Kind, c);
        foreach (var c in Read<NormalMapCandidates>("random.map_candidates").Calls) Add(NormalMapCandidatesTrace.Stream, c.Before, "map/" + c.Kind, c);
        foreach (var c in Read<NormalShuffleCall[]>("random.shuffle_candidates")) Add("base-spire:run:shuffle", c.Before, "combat-shuffle", c);
        foreach (var c in Read<NormalRewardCandidateCall[]>("random.reward_candidates")) Add(NormalRewardCandidateTrace.Stream, c.Before, "reward/" + c.Kind, c);
        var combat = Read<NormalCombatCandidates>("random.combat_candidates");
        foreach (var c in combat.Picks) Add(c.Kind == "hp" ? "base-spire:run:niche" : "base-spire:encounter:monsters", c.Before, "combat/" + c.Kind, c);
        foreach (var c in combat.Moves) Add("base-spire:run:monster_ai", c.Before, "combat/weighted-move", c);
        var calls = new List<NormalFullRandomCall>();
        foreach (var state in states)
        {
            var key = (state.Stream, state.Before); var integer = scalar.TryGetValue(key, out var s); var floating = floats.TryGetValue(key, out var f);
            if (integer == floating || integer && s!.After != state.After || floating && f!.After != state.After)
                throw NormalRandomRoots.Missing("full trace does not have exactly one scalar result per actual draw");
            var operation = integer ? s!.Kind : "binary32";
            var min = integer ? s!.Minimum : f!.MinimumBits.ToString(CultureInfo.InvariantCulture);
            var max = integer ? s!.Maximum : f!.MaximumBits.ToString(CultureInfo.InvariantCulture);
            var result = integer ? s!.Result : f!.ResultBits.ToString(CultureInfo.InvariantCulture);
            if (!sources.TryGetValue(key, out var source))
            {
                // The remaining native reward operations are continuous chance/rarity/
                // upgrade draws and a bounded integer gold roll, with no object pool.
                if (state.Stream != NormalRewardCandidateTrace.Stream || operation is not ("integer" or "binary32") || operation == "integer" && state.Before != 1)
                    throw NormalRandomRoots.Missing("unclassified full random call: " + state.Stream + "/" + state.Before);
                source = (operation == "integer" ? "reward/gold" : "reward/chance-rarity-upgrade", "null");
            }
            calls.Add(new(state.Stream, state.Before, state.After, operation, min, max, result,
                source.Context, source.Semantics, state.BeforeWords, state.AfterWords));
        }
        var keys = states.Select(s => (s.Stream, s.Before)).ToHashSet();
        if (scalar.Keys.Concat(floats.Keys).Any(k => !keys.Contains(k)) || sources.Keys.Any(k => !keys.Contains(k)))
            throw NormalRandomRoots.Missing("source or scalar evidence has no actual random call");
        return new(roots, calls.ToArray());
    }
}
