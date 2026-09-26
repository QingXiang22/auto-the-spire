using CoreTheSpire;

namespace BaseSpire;

internal static class IroncladCards
{
    public static void Register(ContentRegistry registry)
    {
        registry.Register(new CardDefinition(BaseSpireIds.StrikeIronclad, 1, CardType.Attack,
            EffectDsl.Parse("""{"op":"attack","amount":6}"""), Rarity: CardRarity.Basic,
            UpgradedEffect: EffectDsl.Parse("""{"op":"attack","amount":9}""")));
        registry.Register(new CardDefinition(BaseSpireIds.DefendIronclad, 1, CardType.Skill,
            EffectDsl.Parse("""{"op":"gainBlock","amount":5}"""), Target: TargetSelector.Self,
            Rarity: CardRarity.Basic,
            UpgradedEffect: EffectDsl.Parse("""{"op":"gainBlock","amount":8}""")));
        registry.Register(new CardDefinition(BaseSpireIds.Bash, 2, CardType.Attack,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":8},{"op":"applyPower","power":"base-spire:vulnerable","amount":2,"target":"firstEnemy"}]}"""),
            Rarity: CardRarity.Basic,
            UpgradedEffect: EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":10},{"op":"applyPower","power":"base-spire:vulnerable","amount":3,"target":"firstEnemy"}]}""")));
        registry.Register(new CardDefinition(BaseSpireIds.Slimed, 1, CardType.Status,
            new DrawCardsEffect(1), Exhaust: true, Target: TargetSelector.Self, Rarity: CardRarity.Status));
        registry.Register(new CardDefinition(BaseSpireIds.IronWave, 1, CardType.Attack,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"gainBlock","amount":5},{"op":"attack","amount":5}]}"""),
            Rarity: CardRarity.Common,
            UpgradedEffect: EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"gainBlock","amount":7},{"op":"attack","amount":7}]}""")));
        registry.Register(new CardDefinition(BaseSpireIds.Inflame, 1, CardType.Power,
            EffectDsl.Parse("""{"op":"applyPower","power":"base-spire:strength","amount":2,"target":"self"}"""),
            Target: TargetSelector.Self, Rarity: CardRarity.Uncommon,
            UpgradedEffect: EffectDsl.Parse("""{"op":"applyPower","power":"base-spire:strength","amount":3,"target":"self"}""")));
        registry.Register(new CardPoolDefinition(BaseSpireIds.IroncladCardPool,
            [BaseSpireIds.StrikeIronclad, BaseSpireIds.DefendIronclad, BaseSpireIds.Bash,
                BaseSpireIds.IronWave, BaseSpireIds.Inflame]));
    }
}
