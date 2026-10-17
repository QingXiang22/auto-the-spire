using System.Collections.Immutable;
using CoreTheSpire;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Effects;

/// <summary>Compile-time domain program. It carries no Session or mutable gameplay state.</summary>
public sealed record CardEffectProgram(ImmutableArray<CardEffect> Effects)
{
    public static CardEffectProgram Empty { get; } = new([]);
    public bool NeedsSelectedTarget => Effects.Any(effect => effect switch
    {
        PrimitiveCardEffect leaf => leaf.Target == CardEffectTarget.Selected
            || leaf.Amount.NeedsSelectedTarget,
        ConditionalCardEffect branch => branch.Condition.NeedsSelectedTarget
            || branch.Then.NeedsSelectedTarget || branch.Else.NeedsSelectedTarget,
        RepeatCardEffect repeat => repeat.Count > 0 && repeat.Body.NeedsSelectedTarget,
        DynamicRepeatCardEffect repeat => repeat.Count.NeedsSelectedTarget || repeat.Body.NeedsSelectedTarget,
        GenerateCardsEffect generate => generate.Count.NeedsSelectedTarget,
        AutoPlayCardEffect auto => auto.Count.NeedsSelectedTarget,
        ForgeCardEffect forge => forge.Amount.NeedsSelectedTarget,
        OrbCardEffect orb => orb.Amount.NeedsSelectedTarget,
        OstyCardEffect osty => osty.Operation == "ostyAttack" || osty.Amount.NeedsSelectedTarget,
        // ENV-T05：`withCount` 只是词法快照，不引入新的目标来源；`forEachCard` 只绑定**牌**，
        // 但它的 body 仍可能引用敌人目标（当前内容没有，保留递归以免漏判）。
        WithCountCardEffect withCount => withCount.Count.NeedsSelectedTarget || withCount.Body.NeedsSelectedTarget,
        ForEachCardEffect each => each.Body.NeedsSelectedTarget,
        SelectCardsEffect select => select.OnSelected.NeedsSelectedTarget,
        RandomCardEffect random => random.OnSelected.NeedsSelectedTarget,
        _ => false,
    });
}

public abstract record CardEffect(DslSourceLocation Source);
public sealed record PrimitiveCardEffect(CardEffectOperation Operation, CardEffectTarget Target,
    CardEffectAmount Amount, string PowerId, DslSourceLocation Source) : CardEffect(Source);
public sealed record ConditionalCardEffect(CardEffectCondition Condition, CardEffectProgram Then,
    CardEffectProgram Else, DslSourceLocation Source) : CardEffect(Source);
public sealed record RepeatCardEffect(int Count, CardEffectProgram Body, DslSourceLocation Source) : CardEffect(Source);

/// <summary>
/// Repetition whose count is a live, typed amount expression. The count is read once when the loop is
/// entered; every effect inside the body still reads live state at its own execution position.
/// </summary>
public sealed record DynamicRepeatCardEffect(CardEffectAmount Count, CardEffectProgram Body,
    DslSourceLocation Source) : CardEffect(Source);

public sealed record UpgradeSelectedCardEffect(DslSourceLocation Source) : CardEffect(Source);
public sealed record CloneCardEffect(CardEffectPile To, bool Selected, DslSourceLocation Source) : CardEffect(Source);
public sealed record ModifyPlayedCardCostEffect(long Delta, DslSourceLocation Source) : CardEffect(Source);
public sealed record CreateDefinedCardEffect(string CardId, DslSourceLocation Source) : CardEffect(Source);
public sealed record RewritePlayedCardEffect(string CardId, DslSourceLocation Source) : CardEffect(Source);
public sealed record ModifyCardVariableEffect(string Variable, long Delta, DslSourceLocation Source) : CardEffect(Source);

/// <summary>
/// ENV-T05 `withCount`：**词法快照**。进入时求值 <see cref="Count"/> 一次，把它绑定到
/// <see cref="Name"/>；<see cref="Body"/> 里的 `savedCount { name }` 读这个绑定。
/// 源语义（Stoke / FiendFire）：先量手牌，再消耗手牌，最后按**量到的**张数结算——
/// 因此这里的快照必须早于消耗，且不随后续牌区变化重算。
/// </summary>
public sealed record WithCountCardEffect(string Name, CardEffectAmount Count, CardEffectProgram Body,
    DslSourceLocation Source) : CardEffect(Source);

/// <summary>
/// ENV-T05 `forEachCard`：按 <see cref="From"/> 牌区 + <see cref="Filter"/> 逐张绑定"当前牌"，
/// 对每张跑一次 <see cref="Body"/>。迭代集合在进入时快照（引擎 <c>ForEachInst</c> 语义），
/// 所以 body 里把牌移出牌区不会打乱遍历。
/// </summary>
public sealed record ForEachCardEffect(CardEffectPile From, CardEffectCardFilter Filter, CardEffectProgram Body,
    DslSourceLocation Source) : CardEffect(Source);

/// <summary>ENV-T05 `moveSelectedCard`：把 forEachCard 绑定的当前牌从一张牌区移到另一张。</summary>
public sealed record MoveSelectedCardEffect(CardEffectPile From, CardEffectPile To, DslSourceLocation Source)
    : CardEffect(Source)
{
    public bool Manual { get; init; }
    public bool Top { get; init; }
}

public sealed record SelectCardsEffect(CardEffectPile From, CardEffectCardFilter Filter, int Count,
    CardEffectProgram OnSelected, DslSourceLocation Source) : CardEffect(Source)
{
    public bool AutoSelect { get; init; }
    public int? MinimumCount { get; init; }
    public string Prompt { get; init; } = "Choose cards.";
}

public sealed record RandomCardEffect(CardEffectPile From, CardEffectCardFilter Filter,
    CardEffectProgram OnSelected, DslSourceLocation Source) : CardEffect(Source);

/// <summary>ENV-T05 `transformSelectedCard`：把 forEachCard 绑定的当前牌变成另一个定义（可选升级形态）。</summary>
public sealed record TransformSelectedCardEffect(string CardId, CardEffectPile From, bool Upgraded, DslSourceLocation Source)
    : CardEffect(Source);

/// <summary>
/// ENV-T05 `drawUntil { type = attack }`（Pillage）：连续抽牌，**只要抽到的是该类型就继续抽**
/// ——源语义（<c>CardPileCmd.Draw</c> 的 do-while）是继续条件，不是"抽到该类型为止"。
/// 停止于：抽到的牌不是该类型 / 牌堆抽空（重洗后仍空）/ 手牌已满。
/// </summary>
public sealed record DrawUntilCardEffect(string ContinueKind, DslSourceLocation Source) : CardEffect(Source);

public sealed record AutoPlayCardEffect(CardEffectAmount Count, bool ForceExhaust, DslSourceLocation Source) : CardEffect(Source);

/// <summary>
/// Explicit-pool combat generation. Distinct sampling currently accepts literal count=1.
/// FreeThisTurn expires on first play or turn end; full character pools remain a separate content dependency.
/// </summary>
public sealed record GenerateCardsEffect(CardEffectAmount Count, CardEffectCardFilter Filter, bool Upgraded,
    bool Distinct, bool FreeThisTurn, DslSourceLocation Source) : CardEffect(Source)
{
    public ImmutableArray<string> Pool { get; init; } = [];
    public string CharacterPool { get; init; } = "";
    public bool Choose { get; init; }
    public bool AllowCancel { get; init; }
}

public sealed record ModifyPowerVariableEffect(string PowerId, string Variable, long Delta, DslSourceLocation Source) : CardEffect(Source);
public sealed record ForgeCardEffect(string CardId, string Variable, CardEffectAmount Amount, DslSourceLocation Source) : CardEffect(Source);
public sealed record OrbCardEffect(string Operation, string Kind, CardEffectAmount Amount, bool Dequeue, DslSourceLocation Source) : CardEffect(Source);
public sealed record ResolveDoomCardEffect(DslSourceLocation Source) : CardEffect(Source);
public sealed record OstyCardEffect(string Operation, CardEffectAmount Amount, DslSourceLocation Source) : CardEffect(Source);
public sealed record ExhaustAttackAndGrowEffect(string Variable, DslSourceLocation Source) : CardEffect(Source);
public sealed record AutoPlaySelfEffect(CardEffectPile From, DslSourceLocation Source) : CardEffect(Source);

public enum CardEffectPile { Hand, Draw, Discard, Exhaust, InPlay }
public enum CardEffectCardFilter { Any, Attack, NonAttack, Upgradeable }

public enum CardEffectOperation { Damage, GainBlock, GainEnergy, Draw, LoseHp, Heal, ApplyPower, GainMaxHp, GainStars, LoseStars, SetStars }
public enum CardEffectTarget { Owner, Selected, AllEnemies, RandomEnemy, LowestHp }
public enum CardAmountSource { Literal, Hand, Discard, Draw, Exhaust, Played, Block, Variable, TargetPower, PaidX, CardsWithTag, PlayerHpLossCount, SavedCount, PaidStarX }

/// <summary>Constant + Multiplier * live source value, evaluated at this effect's execution position.</summary>
public sealed record CardEffectAmount(CardAmountSource Source, long Constant, long Multiplier = 0, string Reference = "")
{
    public string? Operator { get; init; }
    public ImmutableArray<CardEffectAmount> Operands { get; init; } = [];
    public bool NeedsSelectedTarget => Source == CardAmountSource.TargetPower || Operands.Any(a => a.NeedsSelectedTarget);
    public static CardEffectAmount Literal(long value) => new(CardAmountSource.Literal, value);

    internal static CardEffectAmount FromAuthor(string source, long constant, long multiplier) => source switch
    {
        "" or AmountSource.Literal => Literal(constant),
        AmountSource.HandCount => new(CardAmountSource.Hand, constant, multiplier),
        AmountSource.DiscardCount => new(CardAmountSource.Discard, constant, multiplier),
        AmountSource.DrawCount => new(CardAmountSource.Draw, constant, multiplier),
        AmountSource.ExhaustCount => new(CardAmountSource.Exhaust, constant, multiplier),
        AmountSource.BlockCount => new(CardAmountSource.Block, constant, multiplier),
        AmountSource.PlayerHpLossCount => new(CardAmountSource.PlayerHpLossCount, constant, multiplier),
        AmountSource.EnergyX => new(CardAmountSource.PaidX, constant, multiplier),
        _ when source.StartsWith(AmountSource.CardsWithTagPrefix, StringComparison.Ordinal)
            => new(CardAmountSource.CardsWithTag, constant, multiplier, source[AmountSource.CardsWithTagPrefix.Length..]),
        // ENV-T05：`savedCount { name = handSize }` 读的是 `withCount` 进入时留下的**词法快照**，
        // 不是进入读取点时的实时牌区。见 WithCountCardEffect。
        _ when source.StartsWith(AmountSource.SavedCountPrefix, StringComparison.Ordinal)
            => new(CardAmountSource.SavedCount, constant, multiplier, source[AmountSource.SavedCountPrefix.Length..]),
        _ when source.StartsWith(AmountSource.HandCountPrefix, StringComparison.Ordinal)
            => new(CardAmountSource.Hand, constant, multiplier, source[AmountSource.HandCountPrefix.Length..]),
        _ when source.StartsWith(AmountSource.PlayedCountPrefix, StringComparison.Ordinal)
            => new(CardAmountSource.Played, constant, multiplier, source[AmountSource.PlayedCountPrefix.Length..]),
        _ when source.StartsWith(AmountSource.VariablePrefix, StringComparison.Ordinal)
            => new(CardAmountSource.Variable, constant, multiplier, source[AmountSource.VariablePrefix.Length..]),
        _ when source.StartsWith(AmountSource.TargetPowerCountPrefix, StringComparison.Ordinal)
            => new(CardAmountSource.TargetPower, constant, multiplier, source[AmountSource.TargetPowerCountPrefix.Length..]),
        _ => throw new ArgumentException($"Unsupported amount source '{source}'", nameof(source)),
    };
}

public enum CardConditionKind { TargetAlive, TargetHasPower, PlayerLostHpThisTurn, CardExhaustedThisTurn, PileCountAtLeast }
public sealed record CardEffectCondition(CardConditionKind Kind, string Reference = "", long Threshold = 0)
{
    public bool NeedsSelectedTarget => Kind is CardConditionKind.TargetAlive or CardConditionKind.TargetHasPower;

    internal static CardEffectCondition FromAuthor(string condition)
    {
        if (condition.StartsWith(AuthorCondition.TargetHasPowerPrefix, StringComparison.Ordinal))
            return new(CardConditionKind.TargetHasPower, condition[AuthorCondition.TargetHasPowerPrefix.Length..]);
        if (condition.StartsWith(AuthorCondition.PileCountAtLeastPrefix, StringComparison.Ordinal))
        {
            var parts = condition[AuthorCondition.PileCountAtLeastPrefix.Length..].Split(':');
            return new(CardConditionKind.PileCountAtLeast, parts[0], long.Parse(parts[1]));
        }
        return condition switch
        {
            AuthorCondition.TargetAlive => new(CardConditionKind.TargetAlive),
            AuthorCondition.PlayerLostHpThisTurn => new(CardConditionKind.PlayerLostHpThisTurn),
            AuthorCondition.CardExhaustedThisTurn => new(CardConditionKind.CardExhaustedThisTurn),
            _ => throw new ArgumentException($"Unsupported condition '{condition}'", nameof(condition)),
        };
    }
}
