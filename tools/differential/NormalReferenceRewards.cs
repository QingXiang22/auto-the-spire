#if REFERENCE_SIM
using System.Reflection;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Differential;

internal sealed record NormalRewardPolicyEvidence(long Act, string Character, long Players, string[] UnlockedPool, string[] FullPool,
    bool DefaultEncounter, bool SemanticKeys, string[] Modifiers, string Stream);

// Capture the actual generation context once. Resolution and room-exit state are
// read again at each boundary; the native room keeps its generated sets on exit.
internal sealed class NormalReferenceRewards
{
    private readonly CombatRoom room;
    private readonly (RewardsSet Reward, NormalRewardSetView Context)[] sets;
    internal NormalRewardPolicyEvidence[] Evidence { get; }
    internal NormalReferenceRewards(RunState run, CombatRoom room, Func<Sts2Sim.Core.Random.Rng, string> streamName)
    {
        this.room = room;
        if (room.GeneratedRewards.Count == 0) throw new NotSupportedException("missing_dimension: native generated reward sets");
        string[] hooks = ["ModifyRewards", "ShouldForcePotionReward", "ModifyCardRewardCreationOptions", "ModifyCardRewardCreationOptionsLate",
            "TryModifyCardRewardOptions", "TryModifyCardRewardOptionsLate", "TryModifyCardRewardOptionLate", "BeforeCombatRewardOffered"];
        var modifiers = run.IterateHookListeners(null).SelectMany(listener => listener.GetType().GetMethods()
            .Where(m => hooks.Contains(m.Name) && m.DeclaringType != typeof(AbstractModel)).Select(m => listener.GetType().Name + "." + m.Name))
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        Evidence = room.GeneratedRewards.Select(reward => new NormalRewardPolicyEvidence(run.CurrentActIndex,
            NormalReferenceRun.Id(reward.Player.Character.Id), run.Players.Count,
            reward.Player.Character.CardPool.GetUnlockedCards(reward.Player.UnlockState, run.Players.Count > 1).Select(c => NormalReferenceRun.Id(c.Id)).ToArray(),
            reward.Player.Character.CardPool.GetUnlockedCards(PlayerUnlockState.AllUnlocked(), false).Select(c => NormalReferenceRun.Id(c.Id)).ToArray(),
            Raw(reward.Card, "_creationOptions") is null && Raw(reward.Card, "_explicitOptions") is null,
            reward.Player.PlayerRng.UsesSemanticKeys, modifiers, streamName(reward.Player.PlayerRng.Rewards))).ToArray();
        var policies = Evidence.Select(Classify).ToArray();
        sets = room.GeneratedRewards.Select((reward, index) => (reward, new NormalRewardSetView(
            room.Id ?? throw new NotSupportedException("missing_dimension: native reward visit"), room.RoomType.ToString().ToLowerInvariant(),
            NormalReferenceRun.Id(reward.Player.Character.Id), run.CurrentActIndex, Read<int>(run.Ascension, "_level"),
            Read<int>(reward.Card, "_optionCount"), Read<CardRarityOddsType>(reward.Card, "_oddsType") switch
            {
                CardRarityOddsType.RegularEncounter => "regular-encounter",
                _ => throw new NotSupportedException("missing_dimension: native reward rarity policy")
            }, Read<bool>(reward.Card, "_isPopulated"), false, null, policies[index], false))).ToArray();
    }
    internal static NormalRewardPolicyView Classify(NormalRewardPolicyEvidence evidence)
    {
        if (evidence is not { Act: 0, Character: "base-spire:ironclad", Players: 1, DefaultEncounter: true, SemanticKeys: false }
            || evidence.UnlockedPool.Length == 0 || !evidence.UnlockedPool.SequenceEqual(evidence.FullPool) || evidence.Modifiers.Length != 0)
            throw new NotSupportedException("missing_dimension: native reward policy outside the default, unmodified, fully unlocked first-act encounter");
        return new("all-unlocked-ironclad", "act-one", "none", evidence.Stream);
    }
    internal NormalRewardSetView[] Capture(Func<PotionModel, string> potion) => sets.Select(s => s.Context with
    {
        Generated = Read<bool>(s.Reward.Card, "_isPopulated"), Closed = Read<bool>(room, "_exitCompleted"),
        PotionPending = s.Reward.Potion is { IsResolved: false, Potion: not null },
        Potion = s.Reward.Potion is { IsResolved: false, Potion: { } pending } ? potion(pending) : null
    }).OrderBy(s => s.Visit).ToArray();

    private static object? Raw(object owner, string name) => (owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new NotSupportedException("missing_dimension: native reward metadata " + name)).GetValue(owner);
    private static T Read<T>(object owner, string name) => Raw(owner, name) is T value
        ? value : throw new NotSupportedException("missing_dimension: native reward metadata " + name);
}
#endif
