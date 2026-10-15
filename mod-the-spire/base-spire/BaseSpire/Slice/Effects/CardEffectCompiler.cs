using CoreTheSpire;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Effects;

/// <summary>Only effect-level dependencies. Lifecycle/terminal/room settlement are intentionally absent.</summary>
public sealed record CardEffectBindings(
    Expr Owner, Expr SelectedTarget, Query Enemies, int RandomTargetSlot,
    FieldSymbol OwnerHp, FieldSymbol OwnerMaxHp, FieldSymbol EnemyHp,
    ProcedureSymbol Damage, ProcedureSymbol ApplyPower, ProcedureSymbol Draw, ProcedureSymbol LoseHp,
    Func<CardEffectAmount, Expr> ReadAmount,
    Func<CardEffectCondition, Expr> ReadCondition,
    Func<Expr, Inst[]> GainBlock,
    Func<Expr, Inst[]> GainEnergy,
    Func<string, long, Inst> ModifyVariable,
    // ENV-T05（withCount / forEachCard 一族）：
    //  - Pile：牌区枚举 → 该牌区的集合查询；
    //  - IsAttackCard：牌实例表达式 → "它是攻击牌"的谓词（`filter = attack|nonAttack`）；
    //  - MoveCard / TransformCard：对 `forEachCard` 绑定的**当前牌**做移动 / 变形。
    Func<CardEffectPile, Query> Pile,
    Func<Expr, Expr> IsAttackCard,
    Func<CardEffectPile, CardEffectPile, Expr, Inst[]> MoveCard,
    Func<CardEffectPile, string, bool, Expr, Inst[]> TransformCard,
    bool Unpowered = false)
{
    // ENV-T05（生成 / 抽牌族）：两者都需要包侧的运行时知识（牌堆重洗、角色牌池候选），
    // 因此用 init 属性绑定；缺省实现显式抛错，避免"忘了接线还静默通过"。
    public Func<string, string, long, Inst[]> ModifyPowerVariable { get; init; } = (_, _, _) => throw new InvalidOperationException("Power variable binding missing");
    public Func<string, Inst[]> ExhaustAttackAndGrow { get; init; } = _ => throw new InvalidOperationException("Card damage growth binding missing");
    public Func<CardEffectPile, Inst[]> AutoPlaySelf { get; init; } = _ => throw new InvalidOperationException("Auto play source binding missing");
    public Func<string, Inst[]> DrawUntilKind { get; init; } = kind =>
        throw new InvalidOperationException($"drawUntil {kind}: no card pile binding was wired");

    public Func<Expr, GenerateCardsEffect, Inst[]> GenerateCards { get; init; } =
        (_, _) => throw new InvalidOperationException("generateCards: no card pool binding was wired");

    public Func<Expr, bool, Inst[]> AutoPlay { get; init; } = (_, _) =>
        throw new InvalidOperationException("autoPlay: no shared play pipeline was wired");

    public Expr? PlayedCard { get; init; }
    public Func<CardEffectOperation, Expr, Inst[]> Stars { get; init; } = (_, _) => throw new InvalidOperationException("stars binding missing");
    public Func<ForgeCardEffect, Expr, Inst[]> Forge { get; init; } = (_, _) => throw new InvalidOperationException("forge binding missing");
    public Func<OrbCardEffect, Expr, Inst[]> Orbs { get; init; } = (_, _) => throw new InvalidOperationException("orbs binding missing");
    public Func<Inst[]> ResolveDoom { get; init; } = () => throw new InvalidOperationException("doom binding missing");
    public Func<OstyCardEffect, Expr, Inst[]> Osty { get; init; } = (_, _) => throw new InvalidOperationException("Osty binding missing");
    public Func<Expr, Expr> CanUpgrade { get; init; } = _ => throw new InvalidOperationException("upgrade qualification binding missing");
    public Func<Expr, Inst[]> UpgradeCard { get; init; } = _ => throw new InvalidOperationException("upgrade binding missing");
    public Func<Expr, CardEffectPile, Inst[]> CloneCard { get; init; } = (_, _) => throw new InvalidOperationException("clone binding missing");
    public Func<MoveSelectedCardEffect, Expr, Inst[]> MoveChosenCard { get; init; } = (_, _) => throw new InvalidOperationException("chosen move binding missing");
    public Func<long, Inst[]> ModifyPlayedCost { get; init; } = _ => throw new InvalidOperationException("cost binding missing");
    public Func<string, Inst[]> CreateDefinedCard { get; init; } = _ => throw new InvalidOperationException("creation binding missing");
    public Func<string, Inst[]> RewritePlayedCard { get; init; } = _ => throw new InvalidOperationException("definition binding missing");
    public Func<SelectCardsEffect, Func<Expr, Inst[]>, Inst[]> SelectCards { get; init; } = (_, _) =>
        throw new InvalidOperationException("chooseCard: no card selection binding was wired");
}

/// <summary>Translates immutable domain effects to the existing Engine IR; never executes game state.</summary>
public static class CardEffectCompiler
{
    public static Inst[] Compile(IrScope scope, CardEffectProgram program, CardEffectBindings bindings,
        Expr? selectedCard = null)
        => CompileInScope(scope, program, bindings, EffectCompileScope.Empty with { SelectedCard = selectedCard });

    private static Inst[] CompileInScope(IrScope scope, CardEffectProgram program, CardEffectBindings bindings,
        EffectCompileScope environment)
    {
        var instructions = new List<Inst>();
        foreach (var effect in program.Effects)
        {
            var first = instructions.Count;
            switch (effect)
            {
                case RepeatCardEffect repeat:
                    instructions.Add(scope.Repeat(scope.Const((long)repeat.Count), CompileInScope(scope, repeat.Body, bindings, environment)));
                    break;
                // ENV-T05：动态次数。次数表达式在**进入循环时求值一次**（`Repeat` 指令只跑一次，
                // 内部自己循环），循环体的每条效果仍在自己的执行位置读活状态。
                case DynamicRepeatCardEffect dynamicRepeat:
                    instructions.Add(scope.Repeat(ReadAmount(scope, bindings, environment, dynamicRepeat.Count, dynamicRepeat.Source),
                        CompileInScope(scope, dynamicRepeat.Body, bindings, environment)));
                    break;
                case ConditionalCardEffect branch:
                    instructions.Add(scope.IfElse(bindings.ReadCondition(branch.Condition),
                        CompileInScope(scope, branch.Then, bindings, environment), CompileInScope(scope, branch.Else, bindings, environment)));
                    break;
                case UpgradeSelectedCardEffect upgrade:
                    instructions.AddRange(bindings.UpgradeCard(Current(environment.SelectedCard, upgrade.Source))); break;
                case CloneCardEffect clone:
                    instructions.AddRange(bindings.CloneCard(Current(clone.Selected ? environment.SelectedCard : bindings.PlayedCard, clone.Source), clone.To)); break;
                case ModifyPlayedCardCostEffect cost:
                    instructions.AddRange(bindings.ModifyPlayedCost(cost.Delta)); break;
                case CreateDefinedCardEffect create:
                    instructions.AddRange(bindings.CreateDefinedCard(create.CardId)); break;
                case RewritePlayedCardEffect rewrite:
                    instructions.AddRange(bindings.RewritePlayedCard(rewrite.CardId)); break;
                case ModifyPowerVariableEffect powerVariable:
                    instructions.AddRange(bindings.ModifyPowerVariable(powerVariable.PowerId, powerVariable.Variable, powerVariable.Delta)); break;
                case ResolveDoomCardEffect:
                    instructions.AddRange(bindings.ResolveDoom()); break;
                case OstyCardEffect osty:
                    instructions.AddRange(bindings.Osty(osty, ReadAmount(scope, bindings, environment, osty.Amount, osty.Source))); break;
                case ForgeCardEffect forge:
                    instructions.AddRange(bindings.Forge(forge, ReadAmount(scope, bindings, environment, forge.Amount, forge.Source))); break;
                case OrbCardEffect orb:
                    instructions.AddRange(bindings.Orbs(orb, ReadAmount(scope, bindings, environment, orb.Amount, orb.Source))); break;
                case ExhaustAttackAndGrowEffect grow:
                    instructions.AddRange(bindings.ExhaustAttackAndGrow(grow.Variable)); break;
                case AutoPlaySelfEffect self:
                    instructions.AddRange(bindings.AutoPlaySelf(self.From)); break;
                case ModifyCardVariableEffect variable:
                    instructions.Add(bindings.ModifyVariable(variable.Variable, variable.Delta));
                    break;
                // ENV-T05 `withCount`：先按**声明顺序**登记快照（求值一次），再编译 body；
                // body 里的 `savedCount { name }` 读的就是这个已登记的绑定，不随后续牌区变化重算。
                case WithCountCardEffect withCount:
                    var value = ReadAmount(scope, bindings, environment, withCount.Count, withCount.Source);
                    var slot = scope.Local("savedCount", FieldTypeSchema.Int(0, 9999));
                    instructions.Add(scope.SetLocal(slot, value));
                    instructions.AddRange(CompileInScope(scope, withCount.Body, bindings,
                        environment.BindCount(withCount.Name, scope.LocalRef(slot))));
                    break;
                // ENV-T05 `forEachCard`：迭代集合在 `ForEachInst` 进入时快照（引擎语义），
                // 所以 body 里把当前牌移出牌区不会打乱遍历；body 的"当前牌"就是这一轮的绑定值。
                case ForEachCardEffect each:
                    instructions.Add(scope.ForEach(Filtered(scope, each, bindings),
                        item => CompileInScope(scope, each.Body, bindings, environment.BindCard(item))));
                    break;
                case MoveSelectedCardEffect move:
                    var moving = Current(environment.SelectedCard, move.Source);
                    instructions.AddRange(move.Manual ? bindings.MoveChosenCard(move, moving) : bindings.MoveCard(move.From, move.To, moving));
                    break;
                case SelectCardsEffect choice:
                    instructions.AddRange(bindings.SelectCards(choice,
                        card => CompileInScope(scope, choice.OnSelected, bindings, environment.BindCard(card))));
                    break;
                case RandomCardEffect random:
                    var candidates = Filtered(scope, random.From, random.Filter, bindings);
                    var selected = scope.Local("randomSelectedCard", FieldTypeSchema.Binding());
                    instructions.Add(scope.If(scope.Greater(scope.Count(candidates), scope.Const(0L)),
                        [scope.SamplePick(SliceManifest.RandomCardStream, scope.Sort(candidates, card => card), selected),
                         .. CompileInScope(scope, random.OnSelected, bindings, environment.BindCard(scope.LocalRef(selected)))]));
                    break;
                case TransformSelectedCardEffect transform:
                    instructions.AddRange(bindings.TransformCard(transform.From, transform.CardId, transform.Upgraded,
                        Current(environment.SelectedCard, transform.Source)));
                    break;
                // ENV-T05 `drawUntil` / `generateCards`：包侧循环/牌池过程，这里只转发参数。
                case DrawUntilCardEffect drawUntil:
                    instructions.AddRange(bindings.DrawUntilKind(drawUntil.ContinueKind));
                    break;
                case AutoPlayCardEffect auto:
                    instructions.AddRange(bindings.AutoPlay(ReadAmount(scope, bindings, environment, auto.Count, auto.Source), auto.ForceExhaust));
                    break;
                case GenerateCardsEffect generate:
                    instructions.AddRange(bindings.GenerateCards(ReadAmount(scope, bindings, environment, generate.Count, generate.Source), generate));
                    break;
                case PrimitiveCardEffect primitive:
                    instructions.AddRange(CompilePrimitive(scope, primitive, bindings, environment));
                    break;
                default:
                    throw new InvalidOperationException($"{effect.Source}: unsupported effect {effect.GetType().Name}");
            }
            for (var i = first; i < instructions.Count; i++) instructions[i].At(effect.Source);
        }
        return [.. instructions];
    }

    /// <summary>`forEachCard` 的候选集合：牌区 + 可选的攻击/非攻击筛选。</summary>
    private static Query Filtered(IrScope scope, ForEachCardEffect each, CardEffectBindings bindings)
        => Filtered(scope, each.From, each.Filter, bindings);

    private static Query Filtered(IrScope scope, CardEffectPile from, CardEffectCardFilter filter, CardEffectBindings bindings)
    {
        var cards = bindings.Pile(from);
        return filter switch
        {
            CardEffectCardFilter.Any => cards,
            CardEffectCardFilter.Attack => scope.Where(cards, held => bindings.IsAttackCard(held)),
            CardEffectCardFilter.NonAttack => scope.Where(cards, held => scope.Not(bindings.IsAttackCard(held))),
            CardEffectCardFilter.Upgradeable => scope.Where(cards, bindings.CanUpgrade),
            _ => throw new InvalidOperationException($"unsupported card filter {filter}"),
        };
    }

    /// <summary>
    /// `moveSelectedCard` / `transformSelectedCard` 只对 `forEachCard` 绑定的"当前牌"有意义；
    /// 没有外层绑定时显式拒绝，而不是随便挑一张牌或悄悄跳过。
    /// </summary>
    private static Expr Current(Expr? selectedCard, DslSourceLocation source) => selectedCard
        ?? throw new InvalidOperationException(
            $"{source}: this effect requires an enclosing forEachCard to bind the current card");

    private static Expr ReadAmount(IrScope scope, CardEffectBindings bindings, EffectCompileScope environment,
        CardEffectAmount amount, DslSourceLocation source) => amount.Operator is not null
        ? CardAmountArithmetic.Compile(scope, amount.Operator,
            amount.Operands.Select(a => ReadAmount(scope, bindings, environment, a, source)).ToArray())
        : amount.Source == CardAmountSource.SavedCount
        ? scope.Add(scope.Const(amount.Constant), scope.Multiply(scope.Const(amount.Multiplier),
            environment.Count(amount.Reference, source)))
        : bindings.ReadAmount(amount);

    private static Inst[] CompilePrimitive(IrScope scope, PrimitiveCardEffect effect, CardEffectBindings b, EffectCompileScope environment)
    {
        var amount = ReadAmount(scope, b, environment, effect.Amount, effect.Source);
        switch (effect.Operation)
        {
            case CardEffectOperation.Damage:
                return Targets(scope, effect.Target, b, target =>
                    [scope.Call(b.Damage, b.Owner, target, amount, scope.Const(!b.Unpowered))]);
            case CardEffectOperation.ApplyPower:
                return Targets(scope, effect.Target, b, target =>
                    [scope.Call(b.ApplyPower, target, scope.Const(effect.PowerId), amount, scope.Const(true), scope.Const(false))]);
            case CardEffectOperation.GainBlock:
                return b.GainBlock(amount);
            case CardEffectOperation.GainEnergy:
                return b.GainEnergy(amount);
            case CardEffectOperation.GainStars or CardEffectOperation.LoseStars or CardEffectOperation.SetStars:
                return b.Stars(effect.Operation, amount);
            case CardEffectOperation.Draw:
                return [scope.Call(b.Draw, amount)];
            case CardEffectOperation.LoseHp:
                return [scope.Call(b.LoseHp, amount)];
            case CardEffectOperation.Heal:
                return [scope.SetField(b.Owner, b.OwnerHp,
                    scope.Minimum(scope.Field(b.Owner, b.OwnerMaxHp), scope.Add(scope.Field(b.Owner, b.OwnerHp), amount)))];
            case CardEffectOperation.GainMaxHp:
                var gained = scope.Local("gainedMaxHp", FieldTypeSchema.Int(-99999, 99999));
                return [scope.SetLocal(gained, amount),
                    scope.If(scope.Less(scope.LocalRef(gained), scope.Const(0L)), scope.Fail("gainMaxHp requires nonnegative amount")),
                    scope.SetField(b.Owner, b.OwnerMaxHp, scope.Add(scope.Field(b.Owner, b.OwnerMaxHp), scope.LocalRef(gained))),
                    scope.If(scope.Greater(scope.Field(b.Owner, b.OwnerHp), scope.Const(0L)),
                        scope.SetField(b.Owner, b.OwnerHp,
                            scope.Minimum(scope.Field(b.Owner, b.OwnerMaxHp), scope.Add(scope.Field(b.Owner, b.OwnerHp), scope.LocalRef(gained)))))];
            default:
                throw new InvalidOperationException($"{effect.Source}: unsupported operation {effect.Operation}");
        }
    }

    private static Inst[] Targets(IrScope scope, CardEffectTarget target, CardEffectBindings b,
        Func<Expr, Inst[]> emit) => target switch
    {
        CardEffectTarget.Owner => emit(b.Owner),
        CardEffectTarget.Selected => emit(b.SelectedTarget),
        CardEffectTarget.AllEnemies => [scope.ForEach(b.Enemies, each => emit(each))],
        CardEffectTarget.RandomEnemy =>
            [scope.If(scope.Greater(scope.Count(b.Enemies), scope.Const(0L)),
                [scope.SamplePick(SliceManifest.RandomTargetStream, scope.Sort(b.Enemies, each => each), b.RandomTargetSlot),
                 .. emit(scope.LocalRef(b.RandomTargetSlot))])],
        CardEffectTarget.LowestHp => emit(scope.First(scope.Sort(b.Enemies, each => scope.Field(each, b.EnemyHp)))),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
}
