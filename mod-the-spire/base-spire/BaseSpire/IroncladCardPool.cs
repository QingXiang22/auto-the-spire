using CoreTheSpire;

namespace BaseSpire;

internal static class IroncladCardPool
{
    public static void Register(ContentRegistry registry, IReadOnlyList<CardDefinition> cards) =>
        registry.Register(new CardPoolDefinition(BaseSpireIds.IroncladCardPool,
            cards.Where(card => card.Rarity is CardRarity.Basic or CardRarity.Common
                    or CardRarity.Uncommon or CardRarity.Rare or CardRarity.Ancient)
                .Select(card => card.Id).ToArray()));
}
