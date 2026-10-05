namespace CoreTheSpire;

public enum CardCostOperation { Set, Add }
[Flags]
public enum CardCostExpiration { TurnEnd = 1, Played = 2, CombatEnd = 4 }

public sealed record CardCostModifier(int Amount, CardCostOperation Operation,
    CardCostExpiration Expires, bool ReduceOnly = false)
{
    internal void Validate()
    {
        if (!Enum.IsDefined(Operation) || Expires == 0
            || (Expires & ~(CardCostExpiration.TurnEnd | CardCostExpiration.Played | CardCostExpiration.CombatEnd)) != 0)
            throw new InvalidOperationException("A card cost modifier needs a supported operation and expiration.");
        if (Operation == CardCostOperation.Set) ArgumentOutOfRangeException.ThrowIfNegative(Amount);
    }

    internal int Apply(int current)
    {
        var modified = Operation == CardCostOperation.Set ? Amount : checked(current + Amount);
        return ReduceOnly ? Math.Min(current, modified) : modified;
    }
}

internal sealed record ExpireCardCostEffect(string InstanceId, CardCostExpiration Boundary) : EffectNode;
