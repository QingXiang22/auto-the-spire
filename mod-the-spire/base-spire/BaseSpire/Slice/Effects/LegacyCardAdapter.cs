using CoreTheSpire;

namespace BaseSpire.Slice.Effects;

/// <summary>Input-only translation of diagnostic .author recipes; the dispatcher has no legacy execution path.</summary>
internal static class LegacyCardAdapter
{
    public static CardEffectProgram Play(AuthorCard card, bool upgraded, DslSourceLocation source)
    {
        var amount = CardEffectAmount.FromAuthor(upgraded ? card.UpgradedAmountSource : card.NormalAmountSource,
            upgraded ? card.UpgradedAmountBase : card.NormalAmountBase, upgraded ? card.UpgradedAmountPerCount : card.NormalAmountPerCount);
        if (amount.Source == CardAmountSource.Literal)
            amount = card.IsXCost ? new CardEffectAmount(CardAmountSource.PaidX, 0, 1)
                : CardEffectAmount.Literal(upgraded ? card.UpgradedAmount : card.NormalAmount);
        var live = amount.Source != CardAmountSource.Literal;
        var captured = live ? new CardEffectAmount(CardAmountSource.SavedCount, 0, 1, "legacyAmount") : amount;
        var effects = Steps(card.PowerId, upgraded ? card.UpgradedStacks : card.NormalStacks, upgraded ? card.UpgradedSteps : card.NormalSteps, captured, source).Effects.ToList();
        if ((upgraded ? card.UpgradedConditional : card.NormalConditional) is { } conditional)
            effects.Add(Branch(card, upgraded, conditional, captured, source));
        var name = upgraded ? card.UpgradedGrowVariable : card.NormalGrowVariable;
        if (name.Length > 0) effects.Add(new ModifyCardVariableEffect(name, upgraded ? card.UpgradedGrowAmount : card.NormalGrowAmount, source));
        var program = new CardEffectProgram([.. effects]);
        return live ? new([new WithCountCardEffect("legacyAmount", amount, program, source)]) : program;
    }

    public static CardEffectProgram Hook(AuthorCard card, bool upgraded, IReadOnlyList<AuthorStep> steps, DslSourceLocation source) =>
        Steps(card.PowerId, upgraded ? card.UpgradedStacks : card.NormalStacks, steps, CardEffectAmount.Literal(upgraded ? card.UpgradedAmount : card.NormalAmount), source);

    public static CardEffectProgram Potion(AuthorPotion potion)
    {
        if (potion.Program is not null) return potion.Program;
        var program = Steps(potion.PowerId, potion.PowerStacks, potion.Steps, CardEffectAmount.Literal(potion.Amount),
            new(potion.SourcePath, potion.SourceLine, 1));
        // Diagnostic self-target potions used apply_power for their declared target, not an enemy.
        return potion.Target == PotionTargets.Self
            ? new([.. program.Effects.Select(effect => effect is PrimitiveCardEffect { Operation: CardEffectOperation.ApplyPower } power
                ? power with { Target = CardEffectTarget.Owner } : effect)])
            : program;
    }

    private static CardEffect Branch(AuthorCard card, bool upgraded, AuthorConditional branch, CardEffectAmount amount, DslSourceLocation source)
    {
        CardEffectProgram Arm(IReadOnlyList<AuthorStep> steps, AuthorConditional? nested) =>
            new([.. Steps(card.PowerId, upgraded ? card.UpgradedStacks : card.NormalStacks, steps, amount, source).Effects,
                .. nested is null ? Array.Empty<CardEffect>() : [Branch(card, upgraded, nested, amount, source)]]);
        return new ConditionalCardEffect(CardEffectCondition.FromAuthor(branch.Condition),
            Arm(branch.Then, branch.ThenConditional), Arm(branch.Else, branch.ElseConditional), source);
    }

    private static CardEffectProgram Steps(string powerId, int powerStacks, IReadOnlyList<AuthorStep> steps,
        CardEffectAmount amount, DslSourceLocation source)
    {
        var result = new List<CardEffect>();
        foreach (var step in steps)
        {
            var text = step.Kind;
            if (text is AuthorSteps.Target or AuthorSteps.ExhaustSelf) continue;
            var separator = text.IndexOf(':');
            var kind = separator < 0 ? text : text[..separator];
            var argument = separator < 0 ? "" : text[(separator + 1)..];
            CardEffect leaf;
            if (kind is AuthorSteps.Damage or AuthorSteps.DamageAll or AuthorSteps.DamageRandom or AuthorSteps.DamageLowest)
                leaf = new PrimitiveCardEffect(CardEffectOperation.Damage, kind switch
                {
                    AuthorSteps.DamageAll => CardEffectTarget.AllEnemies,
                    AuthorSteps.DamageRandom => CardEffectTarget.RandomEnemy,
                    AuthorSteps.DamageLowest => CardEffectTarget.LowestHp,
                    _ => CardEffectTarget.Selected,
                }, amount, "", source);
            else if (kind is AuthorSteps.ApplyPower or AuthorSteps.ApplyPowerSelf or AuthorSteps.ApplyPowerAll
                or AuthorSteps.ApplyPowerSelfFromTarget or AuthorSteps.ApplyPowerTargetFromTarget)
            {
                var power = powerId;
                var stacks = CardEffectAmount.Literal(powerStacks);
                if (kind is AuthorSteps.ApplyPowerSelfFromTarget or AuthorSteps.ApplyPowerTargetFromTarget)
                    stacks = new(CardAmountSource.TargetPower, 0, 1, argument);
                else if (argument.Length > 0)
                {
                    var last = argument.LastIndexOf(':');
                    if (last >= 0) { power = argument[..last]; argument = argument[(last + 1)..]; }
                    stacks = CardEffectAmount.Literal(long.Parse(argument));
                }
                leaf = new PrimitiveCardEffect(CardEffectOperation.ApplyPower,
                    kind is AuthorSteps.ApplyPowerSelf or AuthorSteps.ApplyPowerSelfFromTarget ? CardEffectTarget.Owner
                    : kind == AuthorSteps.ApplyPowerAll ? CardEffectTarget.AllEnemies : CardEffectTarget.Selected, stacks, power, source);
            }
            else if (kind is AuthorSteps.Block or AuthorSteps.GainEnergy or AuthorSteps.Draw or AuthorSteps.LoseHp or AuthorSteps.Heal or AuthorSteps.OnFatalGainMaxHp)
                leaf = new PrimitiveCardEffect(kind switch
                {
                    AuthorSteps.Block => CardEffectOperation.GainBlock, AuthorSteps.GainEnergy => CardEffectOperation.GainEnergy,
                    AuthorSteps.Draw => CardEffectOperation.Draw, AuthorSteps.LoseHp => CardEffectOperation.LoseHp,
                    AuthorSteps.Heal => CardEffectOperation.Heal, _ => CardEffectOperation.GainMaxHp,
                }, CardEffectTarget.Owner, argument.Length == 0 ? amount : CardEffectAmount.Literal(long.Parse(argument)), "", source);
            else leaf = Command(step, source);
            if (kind == AuthorSteps.OnFatalGainMaxHp)
                leaf = CardEffectPrimitives.OnFatal(((PrimitiveCardEffect)leaf).Amount, source);
            result.Add(step.Count > 1 && text != AuthorSteps.TopAttacksFromDiscard
                ? new RepeatCardEffect(step.Count, new([leaf]), source) : leaf);
        }
        return new([.. result]);
    }

    public static CardEffect Command(AuthorStep step, DslSourceLocation source)
    {
        var separator = step.Kind.IndexOf(':');
        var name = separator < 0 ? step.Kind : step.Kind[..separator];
        var argument = separator < 0 ? "" : step.Kind[(separator + 1)..];
        return name switch
        {
            AuthorSteps.TopFromDiscard => CardEffectPrimitives.ChooseMove(CardEffectPile.Discard, CardEffectPile.Draw, 1, CardEffectCardFilter.Any, true, false, source),
            AuthorSteps.TopAttackFromDiscard => CardEffectPrimitives.ChooseMove(CardEffectPile.Discard, CardEffectPile.Draw, 1, CardEffectCardFilter.Attack, true, false, source),
            AuthorSteps.TopAttacksFromDiscard => CardEffectPrimitives.ChooseMove(CardEffectPile.Discard, CardEffectPile.Draw, step.Count, CardEffectCardFilter.Attack, true, false, source) with { MinimumCount = 1 },
            AuthorSteps.ExhaustFromHand => CardEffectPrimitives.ChooseMove(CardEffectPile.Hand, CardEffectPile.Exhaust, 1, CardEffectCardFilter.Any, false, false, source),
            AuthorSteps.ExhaustRandomFromHand => new RandomCardEffect(CardEffectPile.Hand, CardEffectCardFilter.Any,
                new([new MoveSelectedCardEffect(CardEffectPile.Hand, CardEffectPile.Exhaust, source)]), source),
            AuthorSteps.UpgradeHand => new ForEachCardEffect(CardEffectPile.Hand, CardEffectCardFilter.Any,
                new([new UpgradeSelectedCardEffect(source)]), source),
            AuthorSteps.UpgradeChooseFromHand => new SelectCardsEffect(CardEffectPile.Hand, CardEffectCardFilter.Upgradeable, 1,
                new([new UpgradeSelectedCardEffect(source)]), source) { Prompt = "Choose a card to upgrade." },
            AuthorSteps.CloneCardToDiscard => new CloneCardEffect(CardEffectPile.Discard, false, source),
            AuthorSteps.ModifyCost => new ModifyPlayedCardCostEffect(long.Parse(argument), source),
            "generate" => new CreateDefinedCardEffect(argument, source),
            "becomes" => new RewritePlayedCardEffect(argument, source),
            _ => throw new InvalidOperationException($"{source}: diagnostic step '{step.Kind}' has no typed adapter"),
        };
    }
}
