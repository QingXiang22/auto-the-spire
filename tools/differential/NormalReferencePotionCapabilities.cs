#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;

namespace Differential;

internal sealed record NormalPotionCapabilityEvidence(string Id, bool Procured, bool Consumed, long StrengthBefore, long StrengthAfter,
    long TemporaryAmount, long StrengthAfterTurn, bool TemporaryRemoved)
{
    public bool Supported => Procured && Consumed && StrengthAfter - StrengthBefore == 5 && TemporaryAmount == 5
        && StrengthAfterTurn == StrengthBefore && TemporaryRemoved;
}

internal static class NormalReferencePotionCapabilities
{
    private static readonly Lazy<NormalPotionCapabilityEvidence> Flex = new(ProbeFlex);
    // Run before attaching normal-run RNG observers. This independent controlled execution
    // never mutates a compared Run or supplies its RNG/results to the normal execution.
    internal static void Prime() => _ = Flex.Value;
    internal static NormalPotionCapabilityEvidence Read(PotionModel potion) => potion is FlexPotion
        ? Flex.Value : throw new NotSupportedException("missing_dimension: no independently executed native potion capability for " + potion.Id);

    private static NormalPotionCapabilityEvidence ProbeFlex()
    {
        var run = new RunState("native-flex-capability", new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000);
        var state = new CombatState(run); state.AddMonster((MonsterModel)ModelDb.Monster<FuzzyWurmCrawler>().MutableClone(), CombatSide.Enemy);
        var engine = new CombatEngine(state); engine.StartCombatAsync().GetAwaiter().GetResult();
        var potion = (PotionModel)ModelDb.Potion<FlexPotion>().MutableClone();
        var procured = PotionCmd.TryToProcure(potion, player).GetAwaiter().GetResult();
        var before = player.Creature.GetPower<StrengthPower>()?.Amount ?? 0;
        PotionCmd.Use(potion, player, player.Creature).GetAwaiter().GetResult();
        var after = player.Creature.GetPower<StrengthPower>()?.Amount ?? 0;
        var temporary = player.Creature.GetPower<FlexPotionPower>()?.Amount ?? 0;
        var consumed = !player.PotionSlots.Contains(potion);
        engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
        var evidence = new NormalPotionCapabilityEvidence(NormalReferenceRun.Id(potion.Id), procured, consumed, before, after, temporary,
            player.Creature.GetPower<StrengthPower>()?.Amount ?? 0, player.Creature.GetPower<FlexPotionPower>() is null);
        if (!evidence.Supported) throw new NotSupportedException("missing_dimension: native FlexPotion capability probe did not execute its promised effect/lifetime");
        return evidence;
    }
}
#endif
