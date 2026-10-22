#if REFERENCE_SIM
using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Differential;

internal sealed record NormalRunConfiguration(string Character, long Ascension, long Players, bool AllEpochs, bool AllColorless,
    bool FullCharacterPool, long BaseDraw);

internal static class NormalReferenceConfiguration
{
    internal static NormalRunConfiguration Capture(RunState run, Player player)
    {
        var level = Read(typeof(Sts2Sim.Core.Entities.Ascension.AscensionManager), "_level", run.Ascension);
        var draw = Read(typeof(CombatEngine), "BaseHandDrawCount", null);
        if (level is not int ascension || draw is not decimal count || count < 0 || count != decimal.Truncate(count))
            throw new NotSupportedException("missing_dimension: native normal configuration numeric shape");
        return new(NormalReferenceRun.Id(player.Character.Id), ascension, run.Players.Count,
            Read(typeof(PlayerUnlockState), "_revealedEpochIds", player.UnlockState) is null,
            ModelDb.All<CardModel>().Where(c => c.IsColorless).All(player.UnlockState.IsColorlessCardUnlocked),
            player.Character.CardPool.GetUnlockedCards(player.UnlockState, run.Players.Count > 1).Select(c => c.Id)
                .SequenceEqual(player.Character.CardPool.GetUnlockedCards(PlayerUnlockState.AllUnlocked(), false).Select(c => c.Id)), checked((long)count));
    }
    internal static string Profile(NormalRunConfiguration configuration) => configuration is
        { Character: "base-spire:ironclad", Ascension: 0, Players: 1, AllEpochs: true, AllColorless: true, FullCharacterPool: true }
        ? "ironclad-a0-fully-unlocked-singleplayer" : throw new NotSupportedException("missing_dimension: native normal profile");

    private static object? Read(Type type, string name, object? owner) => (type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        ?? throw new NotSupportedException("missing_dimension: native normal configuration " + name)).GetValue(owner);
}
#endif
