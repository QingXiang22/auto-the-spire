using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Effects;

/// <summary>Compile-time references to authoritative Engine state, with no runtime state or lifecycle capabilities.</summary>
public sealed record CardEffectStateBindings(
    CollectionSymbol Hand, CollectionSymbol Draw, CollectionSymbol Discard, CollectionSymbol Exhaust,
    EnumTypeSymbol CardKind, FieldSymbol Kind,
    FieldSymbol Block, FieldSymbol LostHpThisTurn, FieldSymbol ExhaustedCardThisTurn, FieldSymbol EnemyHp,
    IReadOnlyDictionary<string, FieldSymbol> Variables, ProcedureSymbol PowerStacks)
{
    /// <summary>
    /// ENV-T05：「在场区」——正在结算中的牌所在的牌区。原版 <c>PlayerCombatState.AllPiles</c>
    /// 的第 5 个牌区就是 <c>PlayPile</c>，它是**牌区**（与手牌/抽牌堆/弃牌堆/消耗堆互斥），
    /// 出牌历史独立于牌区：牌离场后历史仍保留，用历史数全场牌会重复。
    /// </summary>
    public CollectionSymbol InPlay { get; init; } = null!;
    /// <summary>
    /// ENV-T05：玩家本**次战斗**累计"实际失去生命"的事件次数（TearAsunder 的次数来源）。
    /// 战斗级——不随回合清零（与 `LostHpThisTurn` 区分）。
    /// </summary>
    public FieldSymbol HpLossCount { get; init; } = null!;

    /// <summary>
    /// ENV-T05：判断一个牌实例是否带某标签。标签是**卡牌定义**的属性，因此由工厂按
    /// "该标签的成员定义 id 集合"编译成实例上的定义比较；变换/生成出的实例按新定义重新判定。
    /// </summary>
    public Func<string, Expr, Expr> HasTag { get; init; } = (_, _) => throw Faults.Schema(
        "Combat slice cannot compile 'cards-with-tag': tag membership was not wired into this package");

    public BaseSpire.Slice.Orchestration.CardPlayHistory History { get; init; } = null!;

}

/// <summary>Compiles live reads at each effect position; queries are not evaluated or cached here.</summary>
public sealed class CardEffectExpressions(IrScope scope, CardEffectStateBindings state,
    Expr card, Expr owner, Expr target, Expr? paidX)
{
    public Expr? PaidStarX { get; init; }
    public Expr Amount(CardEffectAmount amount)
    {
        if (amount.Operator is not null) return CardAmountArithmetic.Compile(scope, amount.Operator, amount.Operands.Select(Amount).ToArray());
        if (amount.Source == CardAmountSource.Literal) return scope.Const(amount.Constant);
        var value = amount.Source switch
        {
            CardAmountSource.Hand => amount.Reference.Length == 0 ? scope.Count(scope.Collection(state.Hand))
                : scope.Count(scope.Where(scope.Collection(state.Hand), held => scope.Equal(
                    scope.Field(held, state.Kind), scope.EnumConst(state.CardKind, amount.Reference)))),
            CardAmountSource.Discard => scope.Count(scope.Collection(state.Discard)),
            CardAmountSource.Draw => scope.Count(scope.Collection(state.Draw)),
            CardAmountSource.Exhaust => scope.Count(scope.Collection(state.Exhaust)),
            CardAmountSource.Block => scope.Field(owner, state.Block),
            CardAmountSource.Variable => scope.Field(card, state.Variables[amount.Reference]),
            CardAmountSource.TargetPower => scope.CallPure(state.PowerStacks, target, scope.Const(amount.Reference)),
            CardAmountSource.PaidX => paidX ?? throw Unsupported("X", "effect needs a captured payment context"),
            CardAmountSource.PaidStarX => PaidStarX ?? throw Unsupported("StarX", "effect needs a captured star payment context"),
            CardAmountSource.PlayerHpLossCount => scope.Field(owner, state.HpLossCount),
            // Stoke / FiendFire：`savedCount { name }` 读 `withCount` 的词法快照（不是实时牌区）。
            CardAmountSource.SavedCount => throw Unsupported("savedCount", "lexical counts belong to the effect compiler"),
            // PerfectedStrike（原版 `PlayerCombatState.AllCards`）：本次战斗全部牌区的带标签实例数。
            CardAmountSource.CardsWithTag => TaggedCount(amount.Reference),
            CardAmountSource.Played => state.History.FinishedCount(scope, state.CardKind, amount.Reference),
            _ => throw Unsupported(amount.Source.ToString(), "unknown effect amount source"),
        };
        return scope.Add(scope.Const(amount.Constant), scope.Multiply(scope.Const(amount.Multiplier), value));
    }

    public Expr Condition(CardEffectCondition condition) => condition.Kind switch
    {
        CardConditionKind.TargetAlive => scope.Greater(scope.Field(target, state.EnemyHp), scope.Const(0L)),
        CardConditionKind.TargetHasPower => scope.Greater(scope.CallPure(state.PowerStacks, target,
            scope.Const(condition.Reference)), scope.Const(0L)),
        CardConditionKind.PlayerLostHpThisTurn => scope.Field(owner, state.LostHpThisTurn),
        CardConditionKind.CardExhaustedThisTurn => scope.Field(owner, state.ExhaustedCardThisTurn),
        CardConditionKind.PileCountAtLeast => scope.GreaterOrEqual(scope.Count(Pile(condition.Reference)), scope.Const(condition.Threshold)),
        _ => throw Unsupported(condition.Kind.ToString(), "unknown effect condition"),
    };

    private Expr TaggedCount(string tag)
    {
        var total = TaggedIn(state.Hand, tag);
        foreach (var pile in new[] { state.Draw, state.Discard, state.Exhaust, state.InPlay })
            total = scope.Add(total, TaggedIn(pile, tag));
        return total;
    }

    private Expr TaggedIn(CollectionSymbol pile, string tag)
        => scope.Count(scope.Where(scope.Collection(pile), held => state.HasTag(tag, held)));

    private Query Pile(string name) => scope.Collection(name switch    {
        "hand" => state.Hand, "draw" => state.Draw, "discard" => state.Discard, "exhaust" => state.Exhaust,
        _ => throw Unsupported(name, "unknown effect pile"),
    });

    private static EngineFaultException Unsupported(string step, string why) => Faults.Schema(
        $"Combat slice cannot compile '{step}': {why}");
}
