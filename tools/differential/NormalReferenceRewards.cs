#if REFERENCE_SIM
using System.Reflection;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Differential;

// Capture the actual generation context once. Resolution and room-exit state are
// read again at each boundary; the native room keeps its generated sets on exit.
internal sealed class NormalReferenceRewards
{
    private readonly CombatRoom room;
    private readonly (RewardsSet Reward, NormalRewardSetView Context)[] sets;
    internal NormalReferenceRewards(RunState run, CombatRoom room)
    {
        this.room = room;
        if (room.GeneratedRewards.Count == 0) throw new NotSupportedException("missing_dimension: native generated reward sets");
        sets = room.GeneratedRewards.Select(reward => (reward, new NormalRewardSetView(
            room.Id ?? throw new NotSupportedException("missing_dimension: native reward visit"), room.RoomType.ToString().ToLowerInvariant(),
            NormalReferenceRun.Id(reward.Player.Character.Id), run.CurrentActIndex, Read<int>(run.Ascension, "_level"),
            Read<int>(reward.Card, "_optionCount"), Read<CardRarityOddsType>(reward.Card, "_oddsType") switch
            {
                CardRarityOddsType.RegularEncounter => "regular-encounter",
                _ => throw new NotSupportedException("missing_dimension: native reward rarity policy")
            }, Read<bool>(reward.Card, "_isPopulated"), false, null))).ToArray();
    }
    internal NormalRewardSetView[] Capture(Func<PotionModel, string> potion) => sets.Select(s => s.Context with
    {
        Generated = Read<bool>(s.Reward.Card, "_isPopulated"), Closed = Read<bool>(room, "_exitCompleted"),
        Potion = s.Reward.Potion is { IsResolved: false, Potion: { } pending } ? potion(pending) : null
    }).OrderBy(s => s.Visit).ToArray();

    private static T Read<T>(object owner, string name) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(owner) is T value
        ? value : throw new NotSupportedException("missing_dimension: native reward metadata " + name);
}
#endif
