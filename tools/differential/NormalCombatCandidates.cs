using CoreTheSpire.Engine;

namespace Differential;

internal sealed record NormalCombatPick(string Kind, string Owner, long Before, long After, int Selected, string[] Candidates);
internal sealed record NormalMoveEntry(string Id, long WeightBits, int MaxRepeats, int Cooldown, bool Once);
internal sealed record NormalMoveCall(string Owner, string Node, long Before, long After, int Selected, NormalMoveEntry[] Candidates);
internal sealed record NormalCombatCandidates(NormalCombatPick[] Picks, NormalMoveCall[] Moves);

internal static class NormalCombatCandidateTrace
{
    internal static int Select(NormalMoveEntry[] candidates, float result)
    {
        for (var i = 0; i < candidates.Length; i++) { result -= Binary32.Read(candidates[i].WeightBits); if (result <= 0) return i; }
        throw Missing("weighted move result outside actual source");
    }
    internal static NotSupportedException Missing(string reason) => new("missing_dimension: " + reason);
}
