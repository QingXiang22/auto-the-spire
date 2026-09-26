using CoreTheSpire;

namespace BaseSpire;

internal static class IroncladCards
{
    public static void Register(ContentRegistry registry)
    {
        registry.Register(new CardDefinition(BaseSpireIds.Anger, 0, CardType.Attack,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":6},{"op":"addCardsToPile","card":"base-spire:anger","pile":"discard","target":"self","inheritUpgradeFromPlayedCard":true}]}"""),
            Rarity: CardRarity.Common,
            UpgradedEffect: EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":8},{"op":"addCardsToPile","card":"base-spire:anger","pile":"discard","target":"self","inheritUpgradeFromPlayedCard":true}]}""")));
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
        registry.Register(new CardDefinition(BaseSpireIds.Bludgeon, 3, CardType.Attack,
            EffectDsl.Parse("""{"op":"attack","amount":32}"""), Rarity: CardRarity.Uncommon,
            UpgradedEffect: EffectDsl.Parse("""{"op":"attack","amount":42}""")));
        registry.Register(new CardDefinition(BaseSpireIds.Slimed, 1, CardType.Status,
            new DrawCardsEffect(1), Exhaust: true, Target: TargetSelector.Self, Rarity: CardRarity.Status));
        registry.Register(new CardDefinition(BaseSpireIds.DemonForm, 3, CardType.Power,
            EffectDsl.Parse("""{"op":"applyPower","power":"base-spire:demon-form-power","amount":2,"target":"self"}"""),
            Target: TargetSelector.Self, Rarity: CardRarity.Rare,
            UpgradedEffect: EffectDsl.Parse("""{"op":"applyPower","power":"base-spire:demon-form-power","amount":3,"target":"self"}""")));
        registry.Register(new CardDefinition(BaseSpireIds.Headbutt, 1, CardType.Attack,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":9},{"op":"chooseCardFromPile","from":"discard","to":"draw","prompt":"Choose a card from discard","destinationPosition":"top"}]}"""),
            Rarity: CardRarity.Common,
            UpgradedEffect: EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":12},{"op":"chooseCardFromPile","from":"discard","to":"draw","prompt":"Choose a card from discard","destinationPosition":"top"}]}""")));
        registry.Register(new CardDefinition(BaseSpireIds.IronWave, 1, CardType.Attack,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"gainBlock","amount":5},{"op":"attack","amount":5}]}"""),
            Rarity: CardRarity.Common,
            UpgradedEffect: EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"gainBlock","amount":7},{"op":"attack","amount":7}]}""")));
        registry.Register(new CardDefinition(BaseSpireIds.Inflame, 1, CardType.Power,
            EffectDsl.Parse("""{"op":"applyPower","power":"base-spire:strength","amount":2,"target":"self"}"""),
            Target: TargetSelector.Self, Rarity: CardRarity.Uncommon,
            UpgradedEffect: EffectDsl.Parse("""{"op":"applyPower","power":"base-spire:strength","amount":3,"target":"self"}""")));
        registry.Register(new CardDefinition(BaseSpireIds.PommelStrike, 1, CardType.Attack,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":9},{"op":"drawCards","amount":1}]}"""),
            Rarity: CardRarity.Common,
            UpgradedEffect: EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":10},{"op":"drawCards","amount":2}]}""")));
        registry.Register(new CardDefinition(BaseSpireIds.ShrugItOff, 1, CardType.Skill,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"gainBlock","amount":8},{"op":"drawCards","amount":1}]}"""),
            Target: TargetSelector.Self, Rarity: CardRarity.Common,
            UpgradedEffect: EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"gainBlock","amount":11},{"op":"drawCards","amount":1}]}""")));
        registry.Register(new CardDefinition(BaseSpireIds.TwinStrike, 1, CardType.Attack,
            EffectDsl.Parse("""{"op":"repeat","count":2,"effect":{"op":"attack","amount":5}}"""),
            Rarity: CardRarity.Common,
            UpgradedEffect: EffectDsl.Parse("""{"op":"repeat","count":2,"effect":{"op":"attack","amount":7}}""")));
        registry.Register(new CardDefinition(BaseSpireIds.Uppercut, 2, CardType.Attack,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":13},{"op":"applyPower","power":"base-spire:weak","amount":1,"target":"firstEnemy"},{"op":"applyPower","power":"base-spire:vulnerable","amount":1,"target":"firstEnemy"}]}"""),
            Rarity: CardRarity.Uncommon,
            UpgradedEffect: EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":13},{"op":"applyPower","power":"base-spire:weak","amount":2,"target":"firstEnemy"},{"op":"applyPower","power":"base-spire:vulnerable","amount":2,"target":"firstEnemy"}]}""")));
        registry.Register(new CardDefinition(BaseSpireIds.Thunderclap, 1, CardType.Attack,
            EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":4,"target":"allOpponents"},{"op":"applyPower","power":"base-spire:vulnerable","amount":1,"target":"allOpponents"}]}"""),
            Target: TargetSelector.AllOpponents, Rarity: CardRarity.Common,
            UpgradedEffect: EffectDsl.Parse("""{"op":"sequence","effects":[{"op":"attack","amount":7,"target":"allOpponents"},{"op":"applyPower","power":"base-spire:vulnerable","amount":1,"target":"allOpponents"}]}""")));
        registry.Register(new CardPoolDefinition(BaseSpireIds.IroncladCardPool,
            [BaseSpireIds.Anger, BaseSpireIds.Bash, BaseSpireIds.Bludgeon, BaseSpireIds.DefendIronclad,
                BaseSpireIds.DemonForm, BaseSpireIds.Headbutt, BaseSpireIds.Inflame,
                BaseSpireIds.IronWave, BaseSpireIds.PommelStrike, BaseSpireIds.ShrugItOff,
                BaseSpireIds.StrikeIronclad, BaseSpireIds.Thunderclap, BaseSpireIds.TwinStrike,
                BaseSpireIds.Uppercut]));
    }
}
