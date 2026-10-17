using CoreTheSpire;

namespace BaseSpire.Slice.Effects;

/// <summary>Validates contextual bindings before a source card can be registered.</summary>
public static class CardEffectProgramValidator
{
    public static IReadOnlyList<string> Validate(CardEffectProgram program, bool starX = false)
    {
        var problems = new List<string>();
        Visit(program, new HashSet<string>(StringComparer.Ordinal), false);
        return problems;

        void Amount(CardEffectAmount amount, DslSourceLocation source, HashSet<string> names)
        {
            if (amount.Source == CardAmountSource.PaidStarX && !starX)
                problems.Add($"{source}: PaidStarX requires a starCost=X play context");
            foreach (var operand in amount.Operands) Amount(operand, source, names);
            if (amount.Source == CardAmountSource.SavedCount && !names.Contains(amount.Reference))
                problems.Add($"{source}: savedCount '{amount.Reference}' has no enclosing withCount");
        }
        void Visit(CardEffectProgram body, HashSet<string> names, bool cardBound)
        {
            foreach (var effect in body.Effects)
            {
                switch (effect)
                {
                    case PrimitiveCardEffect leaf: Amount(leaf.Amount, leaf.Source, names); break;
                    case ForgeCardEffect forge: Amount(forge.Amount, forge.Source, names); break;
                    case OrbCardEffect orb: Amount(orb.Amount, orb.Source, names); break;
                    case OstyCardEffect osty: Amount(osty.Amount, osty.Source, names); break;
                    case GenerateCardsEffect generate: Amount(generate.Count, generate.Source, names); break;
                    case AutoPlayCardEffect auto: Amount(auto.Count, auto.Source, names); break;
                    case DynamicRepeatCardEffect repeat:
                        Amount(repeat.Count, repeat.Source, names); Visit(repeat.Body, names, cardBound); break;
                    case RepeatCardEffect repeat: Visit(repeat.Body, names, cardBound); break;
                    case ConditionalCardEffect branch:
                        Visit(branch.Then, names, cardBound); Visit(branch.Else, names, cardBound); break;
                    case WithCountCardEffect count:
                        Amount(count.Count, count.Source, names);
                        var child = new HashSet<string>(names, StringComparer.Ordinal) { count.Name };
                        Visit(count.Body, child, cardBound); break;
                    case ForEachCardEffect each: Visit(each.Body, names, true); break;
                    case SelectCardsEffect select: Visit(select.OnSelected, names, true); break;
                    case RandomCardEffect random: Visit(random.OnSelected, names, true); break;
                    case MoveSelectedCardEffect or TransformSelectedCardEffect or UpgradeSelectedCardEffect or GrantSelectedSlyEffect when !cardBound:
                        problems.Add($"{effect.Source}: this effect requires an enclosing card selection or forEachCard"); break;
                    case CloneCardEffect { Selected: true } when !cardBound:
                        problems.Add($"{effect.Source}: selected clone requires an enclosing card selection or forEachCard"); break;
                }
            }
        }
    }
}
