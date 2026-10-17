using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>作者表示里的一个效果步骤。词表就是切片运行时真正能执行的动作。</summary>
public static class AuthorSteps
{
    public const string Target = "target";
    public const string Damage = "damage";

    /// <summary>目标域：对**全部**敌人各造成一次声明数值的伤害（不选目标）。</summary>
    public const string DamageAll = "damage_all";

    public const string Block = "block";
    public const string ApplyPower = "apply_power";

    /// <summary>目标域：对**全部**敌人各施加一次声明的能力与层数（不选目标）。</summary>
    public const string ApplyPowerAll = "apply_power_all";

    /// <summary>施加给**自己**（来源 `target = context.owner`，例如 Inflame 给自己加力量）。</summary>
    public const string ApplyPowerSelf = "apply_power_self";

    /// <summary>
    /// `apply_power_self_from_target:&lt;power-id&gt;`：给自己施加卡牌级 `power`，**层数取"所选目标身上**
    /// 该能力当前的层数"（来源 `applyScaledPower { base = 0  perCount = 1  count = { targetPowerAmount { … } } }`，
    /// 例如 Dominate）。因为要在**它所在的步骤位置**读目标状态，所以它是一条步骤，而不是"数值来源"。
    /// 当前只支持来源里 `base = 0, perCount = 1` 这一子集。
    /// </summary>
    public const string ApplyPowerSelfFromTarget = "apply_power_self_from_target";

    /// <summary>
    /// `apply_power_target_from_target:&lt;被计数的能力 id&gt;`：给**所选目标**施加卡牌级 `power`，    /// 层数取**目标自己**身上该能力当前的层数（来源
    /// `applyScaledPower { power = P  count = { targetPowerAmount = { power = P } }  target = context.target }`，
    /// 例如 MoltenFist：把目标的易伤翻倍）。
    /// 与 `apply_power_self_from_target` 的区别只在于**施加给谁**（自己 vs 目标）。
    /// </summary>
    public const string ApplyPowerTargetFromTarget = "apply_power_target_from_target";
    public const string TopFromDiscard = "top_from_discard";

    /// <summary>打出后进入**消耗区**（而不是弃牌堆），并在进入时触发 `on_exhaust`。</summary>
    public const string ExhaustSelf = "exhaust_self";

    /// <summary>**生成**：`generate:&lt;card-id&gt;` 把手牌里多出一张该定义的**新实例**（普通形态）。</summary>
    public const string GeneratePrefix = "generate:";

    /// <summary>
    /// `modify_cost:&lt;n&gt;`：给**这张牌自己**加一个临时费用修正（来源 `modifySelectedCardCost`：
    /// `operation = add`、`expires = { turnEnd }`）。参数可正可负；有效费用 = max(0, 费用 + 修正)。
    /// </summary>
    public const string ModifyCost = "modify_cost";

    /// <summary>**获得能量**：给玩家加"这张牌的 Amount"点能量（来源 `modifyEnergy`）。</summary>
    public const string GainEnergy = "gain_energy";

    /// <summary>
    /// **随机消耗一张手牌**（来源 `randomMoveCard { from = hand, to = exhaust }`，例如 TrueGrit / Cinder）：
    /// 用**命名随机流**（`base-spire:combat-random-card`）从当前手牌里抽一张送进消耗区，
    /// 并触发消耗相关的声明式链；手牌为空时什么也不做。
    /// </summary>
    public const string ExhaustRandomFromHand = "exhaust_random_from_hand";

    /// <summary>**抽牌**：从抽牌堆抽声明数值的牌（抽牌堆空时按命名流重洗弃牌堆）。</summary>
    public const string Draw = "draw";

    /// <summary>
    /// **治疗**：`heal:&lt;n&gt;`（来源 `heal { amount = n }`，例如 NotYet）——
    /// 给玩家回 n 点生命，**不超过**最大生命。
    /// </summary>
    public const string Heal = "heal";

    /// <summary>
    /// **击杀奖励**：`on_fatal_gain_max_hp:&lt;n&gt;`（来源 `attack { onFatal = { gainMaxHp = { amount = n } } }`，
    /// 例如 Feed）——紧跟在那次攻击的伤害之后：**若目标已死**，最大生命与当前生命各 +n。
    /// </summary>
    public const string OnFatalGainMaxHp = "on_fatal_gain_max_hp";

    /// <summary>
    /// **选择一张手牌消耗**（来源 `chooseCard { from = hand, to = exhaust }`，例如 BurningPact / Brand）：
    /// 向玩家提问（候选＝当前手牌），把选中的那张送进消耗区并触发消耗相关的声明式链。
    /// 手牌为空时不提问（不产生空决策）。
    /// </summary>
    public const string ExhaustFromHand = "exhaust_from_hand";

    /// <summary>带**字面量数值**参数的前缀（写成 `<步骤>:<数值>`；数值不参与命名空间限定）。</summary>
    public static readonly IReadOnlyList<string> LiteralParameterized =
        [LoseHp + ":", Draw + ":", ApplyPower + ":", ApplyPowerAll + ":", ApplyPowerSelf + ":", ModifyCost + ":"];

    /// <summary>**HP 损失**（不是伤害）：对**玩家**扣声明数值的 HP，绕过格挡与修正链；归零即失败。</summary>
    public const string LoseHp = "lose_hp";

    /// <summary>**升级**：把手牌里的每张牌升级（已是升级形态的保持不变）。</summary>
    public const string UpgradeHand = "upgrade_hand";

    /// <summary>
    /// **选择一张手牌升级**（来源 `chooseCard { from = hand, to = none, filter = upgradeable,
    /// onSelected = { upgradeSelectedCard } }`，例如 Armaments 的普通形态）：
    /// 只把**可升级**（尚未升级）的手牌作为候选；没有候选时不提问。
    /// </summary>
    public const string UpgradeChooseFromHand = "upgrade_choose_from_hand";

    /// <summary>**变形**：`becomes:&lt;card-id&gt;` 让这张牌打出后变成另一个定义（普通形态、数值重置）。</summary>
    public const string BecomesPrefix = "becomes:";

    /// <summary>
    /// ENV-T05（Anger）：**战斗克隆** —— 来源 `cloneCard { pile = discard }`，在**弃牌堆**里留下一个
    /// **新的战斗实例**。源是**本张正在打出的牌**：克隆复制它的定义（`DefinitionId`）、升级、可表达的实例
    /// 字段与实例变量，并记录"以哪张牌为源"（`Card.CloneOf`，隐藏引用）；它与源实例**互相独立**，
    /// 之后对任一方的改写都不影响另一方。
    /// 与 `generate:` 的区别（原版 `CardModel.CreateClone` vs 重新生成普通定义）：克隆保留源实例**打出时**
    /// 的变体与实例状态，不回到初始普通定义；与 `StartNextRoom` 的长期→战斗副本也不同：克隆**不设长期来源**
    /// （`LongTermSource` 留空），因此它不会被带进下一场战斗、也不会写回长期牌组。
    /// </summary>
    public const string CloneCardToDiscard = "clone_card_to_discard";

    public static readonly IReadOnlyList<string> Parameterized =
        [GeneratePrefix, BecomesPrefix, ApplyPowerSelfFromTarget + ":"];

    /// <summary>目标域：从**全部敌人**里按命名随机流均匀抽一个作为目标（候选顺序由引擎按公开语义键排序）。</summary>
    public const string DamageRandom = "damage_random";

    /// <summary>目标域（**条件选择**）：自动选**当前生命最低**的敌人并造成伤害，不向玩家提问。</summary>
    public const string DamageLowest = "damage_lowest";

    /// <summary>
    /// **条件选择**：只把弃牌堆里的**攻击牌**作为候选，选一张置顶（`top_from_discard` 的受限版本）。
    /// 候选集由 `Where(...)` 过滤，空候选时同样不提问。
    /// </summary>
    public const string TopAttackFromDiscard = "top_attack_from_discard";

    public static IReadOnlyList<string> StepKinds(IReadOnlyList<AuthorStep> steps) =>
        [.. steps.Select(step => step.Kind)];

    /// <summary>
    /// **多选**：从弃牌堆里挑**最多 N 张**（N 就是批次）攻击牌，按回答顺序逐一置顶。
    /// 多选结果是引用列表，运行时会用 `list[slot]` 遍历它（每个引用恰好移动一次）。
    /// </summary>
    public const string TopAttacksFromDiscard = "top_attacks_from_discard";

    public static readonly IReadOnlyList<string> All =
        [Target, Damage, DamageAll, DamageRandom, DamageLowest, Block, ApplyPower, TopFromDiscard,
            TopAttackFromDiscard, TopAttacksFromDiscard, ExhaustSelf, CloneCardToDiscard, UpgradeHand, LoseHp, Draw,
            ApplyPowerAll, ApplyPowerSelf, GainEnergy, ModifyCost];

    /// <summary>支持的 hook 名。两个都是"当前运行时会真的触发"的：`on_exhaust` 由 `exhaust_self` 触发，
    /// `turn_end_in_hand` 由回合结束时仍在手牌的牌触发。</summary>
    public const string HookOnExhaust = "on_exhaust";
    public const string HookOnDiscard = "on_discard";

    public const string HookTurnEndInHand = "turn_end_in_hand";

    /// <summary>该牌进入战斗时触发（切片里就是开局创建这张牌之后）。</summary>
    public const string HookOnEnterCombat = "on_enter_combat";

    /// <summary>这张牌被打出**之前**触发（在它自己的效果之前）。</summary>
    public const string HookBeforeCardPlayed = "before_card_played";

    /// <summary>这张牌被打出**之后**触发（在它自己的效果之后、进结果区之前）。</summary>
    public const string HookAfterCardPlayed = "after_card_played";

    public static readonly IReadOnlyList<string> Hooks =
        // Legacy .author syntax has no discard-hook field; the formal .spire reader registers it separately.
        [HookOnExhaust, HookTurnEndInHand, HookOnEnterCombat, HookBeforeCardPlayed, HookAfterCardPlayed];

    /// <summary>hook 里允许的步骤：目前只有不选目标、不进消耗区的两个；其余需要先补运行时能力。</summary>
    public static readonly IReadOnlyList<string> HookSteps =
        [DamageAll, Block, ApplyPower, ApplyPowerAll, ApplyPowerSelf, LoseHp];

    /// <summary>
    /// hook 里为什么不能写某些步骤——**逐条给出理由**，而不是笼统地说"未支持"。
    /// 这些是**设计边界**（hook 的时点不允许挂起决策、也不改变实例身份），不是尚未实现的缺口。
    /// </summary>
    /// <summary>
    /// 某个步骤是否属于 <paramref name="canonical"/>：带字面量参数的形式（`apply_power:1`）也算。
    /// 用于"同一目标域只能出现一种""需要 power/stacks"这类校验。
    /// </summary>
    public static bool IsStep(string step, string canonical) =>
        string.Equals(step, canonical, StringComparison.Ordinal)
        || step.StartsWith(canonical + ":", StringComparison.Ordinal);

    public static string HookStepReason(string step) => step switch
    {
        Target => "hook 的时点不允许挂起决策；需要选目标请写进这张牌的效果步骤",
        Damage => "hook 里没有可选的敌人目标；要对全体敌人造成伤害请用 damage_all",
        TopFromDiscard => "hook 的时点不允许挂起决策；置顶选择请写进牌的效果步骤",
        TopAttackFromDiscard => "hook 的时点不允许挂起决策；置顶选择请写进牌的效果步骤",
        TopAttacksFromDiscard => "hook 的时点不允许挂起决策；多选置顶请写进牌的效果步骤",
        ExhaustSelf => "hook 是在这张牌**已经**被消耗之后才触发的，再声明消耗自己没有意义",
        UpgradeHand => "hook 里不做手牌升级（它是可观察的批量改写，适合写在牌的效果步骤里）",
        CloneCardToDiscard => "hook 里不复制实例（复制会产生新的实例身份，适合写在牌的效果步骤里）",
        _ when step.StartsWith(GeneratePrefix, StringComparison.Ordinal)
            => "hook 里不生成新实例（生成会改变实例身份，适合写在牌的效果步骤里）",
        _ when step.StartsWith(BecomesPrefix, StringComparison.Ordinal)
            => "hook 里不改变实例身份（变形适合写在牌的效果步骤里）",
        _ => "该步骤不在 hook 的允许面内",
    };

    /// <summary>不允许写批次的步骤。</summary>
    public static readonly IReadOnlyList<string> NonRepeatable =
        [Target, DamageAll, DamageRandom, ExhaustSelf, ApplyPowerAll, CloneCardToDiscard];

    public static string NonRepeatableReason(string step) => step switch
    {
        Target => "选 N 次目标的语义不明（要多次命中请给效果步骤加批次）",
        DamageAll => "它本身就是全员各一次，再加批次仍然只表示全员",
        DamageRandom => "抽 N 次目标的语义不明（要多次命中请给 damage 加批次）",
        ApplyPowerAll => "它本身就是给全员各一次，再加批次只表示层数翻倍（应直接写层数）",
        ExhaustSelf => "一张牌只会被消耗一次",
        CloneCardToDiscard => "一张牌打出一次只留下一个同源克隆（重复出牌请用新的克隆实例）",
        _ => "该步骤不可重复",
    };
}

/// <summary>
/// 效果的**数值来源**：`literal`（用声明的 `amount`）或从**当前状态**读取（打出那一刻结算，不是声明常量）：
/// `hand-count` / `discard-count` / `draw-count`。这是切片里"实例变量"的最小形态——数值在解析时才确定。
/// </summary>
public static class AmountSource
{
    public const string Literal = "literal";
    public const string HandCount = "hand-count";
    public const string DiscardCount = "discard-count";
    public const string DrawCount = "draw-count";

    /// <summary>`played-count:<Kind>`：本回合**已打出**的该类牌数量（来源 `cardPlaysThisTurn { type = … phase = finished }`）。</summary>
    public const string PlayedCountPrefix = "played-count:";

    /// <summary>`exhaust-count`：消耗区里的牌数（来源 `pileCount { pile = exhaust }`，例如 AshenStrike）。</summary>
    public const string ExhaustCount = "exhaust-count";

    /// <summary>`block-count`：读**自己**当前的格挡值（来源 `gainBlockFromCurrentBlock`，例如 Entrench）。</summary>
    public const string BlockCount = "block-count";

    /// <summary>`variable:&lt;name&gt;`：读这张牌自己的实例变量（可变、随实例走）。</summary>
    /// <summary>`hand-count:<Kind>`：手牌里**该类**牌的数量（来源 `pileCount { pile = hand  type = attack }`）。</summary>
    public const string HandCountPrefix = "hand-count:";

    /// <summary>
    /// `target-power-count:&lt;power-id&gt;`：**所选目标**身上该能力的层数
    /// （来源 `count = { targetPowerAmount = { power = … } }`，例如 Dominate：
    /// 按目标身上的易伤层数给自己加力量）。
    /// </summary>
    public const string TargetPowerCountPrefix = "target-power-count:";

    /// <summary>
    /// `cards-with-tag:&lt;tag-id&gt;`：玩家**本次战斗全部牌堆**里带该标签的牌实例数
    /// （来源 `count = { cardsWithTag = { tag = Strike } }`，例如 PerfectedStrike；原版读
    /// `PlayerCombatState.AllCards`，含正在结算的这张牌自己）。
    /// </summary>
    public const string CardsWithTagPrefix = "cards-with-tag:";

    /// <summary>
    /// `player-hp-loss-count`：本次战斗里玩家**实际失去生命的事件次数**（来源
    /// `count = { playerHpLossCount = {} }`，例如 TearAsunder）。直接失去生命与未被完全挡下的
    /// 攻击各记一次；完全被格挡挡下的攻击不计。战斗级（不随回合清零）。
    /// </summary>
    public const string PlayerHpLossCount = "player-hp-loss-count";

    /// <summary>
    /// ENV-T05：`count = { energyX = {} }` —— **已付的 X**（X 费卡在付款时捕获的数值，
    /// 不是"当前能量"，也不随之后的能量增减改变）。作者对照：Cascade 的
    /// `count = { scaled = { base = 0 perCount = 1 count = { energyX = {} } } }`。
    /// </summary>
    public const string EnergyX = "energy-x";

    /// <summary>
    /// ENV-T05：`count = { savedCount = { name = handSize } }` —— 读 **`withCount` 进入时留下的词法快照**，
    /// 不是进入读取点时的实时牌区。作者对照：Stoke / FiendFire 先量手牌（`withCount { name = "handSize"
    /// count = { pileCount = { pile = hand } } }`），再消耗手牌，最后按**量到的**张数结算。
    /// </summary>
    public const string SavedCountPrefix = "saved-count:";

    public const string VariablePrefix = "variable:";

    public static readonly IReadOnlyList<string> All =
        [Literal, HandCount, DiscardCount, DrawCount, ExhaustCount, BlockCount, VariablePrefix];
}

/// <summary>
/// 条件效果（来源 `if = { condition = { … }  then = { … }  else = { … } }`）支持的**条件**。
/// 只列切片真的能判定的谓词；未实现的谓词显式拒绝。
/// </summary>
public static class AuthorCondition
{
    /// <summary>本回合玩家**失去过生命**（来源 `playerLostHpThisTurn`）。</summary>
    public const string PlayerLostHpThisTurn = "player-lost-hp-this-turn";

    /// <summary>本回合**消耗过牌**（来源 `cardExhaustedThisTurn`）。</summary>
    public const string CardExhaustedThisTurn = "card-exhausted-this-turn";

    /// <summary>
    /// `target-has-power:&lt;power-id&gt;`：**所选目标**身上有该能力且层数 &gt; 0
    /// （来源 `hasPower { power = … target = context.target }`）。
    /// </summary>
    public const string TargetHasPowerPrefix = "target-has-power:";

    /// <summary>
    /// `target-alive`：**所选目标**仍然存活（来源 `if { condition = { targetAlive = {} } }`）。
    /// 范围限制：只对敌人目标有意义（前端按目标形状拒绝别的用法）；判定读目标的 `Hp`。
    /// </summary>
    public const string TargetAlive = "target-alive";

    /// <summary>
    /// `pile-count-at-least:&lt;pile&gt;:&lt;n&gt;`：某个牌区的张数 ≥ n
    /// （来源 `pileCountAtLeast { pile = exhaust count = 3 }`）。
    /// </summary>
    public const string PileCountAtLeastPrefix = "pile-count-at-least:";

    public static readonly IReadOnlyList<string> All =
        [PlayerLostHpThisTurn, CardExhaustedThisTurn, TargetHasPowerPrefix, PileCountAtLeastPrefix,
            TargetAlive];
}

/// <summary>
/// 条件效果：一个条件 + 两组步骤（then/else）。
///
/// ENV-02：分支里还可以有**嵌套条件段**（MoltenFist：`if targetAlive then { if hasPower(Vulnerable) then … }`）。
/// 嵌套用**递归的同一类型**表达，因此深度不限；执行顺序与卡片级一致——先该分支的固定步骤，再嵌套条件段。
/// </summary>
public sealed record AuthorConditional(
    string Condition,
    IReadOnlyList<AuthorStep> Then,
    IReadOnlyList<AuthorStep> Else,
    AuthorConditional? ThenConditional = null,
    AuthorConditional? ElseConditional = null)
{
    public override string ToString() =>
        $"if({Condition}) then[{Then.Count}] else[{Else.Count}]"
        + (ThenConditional is null ? string.Empty : $" then-if({ThenConditional})")
        + (ElseConditional is null ? string.Empty : $" else-if({ElseConditional})");
}

/// <summary>一个效果步骤：`Kind` 来自词表，`Count` 是批次数（`damage x3` → `Count = 3`）。</summary>
public sealed record AuthorStep(string Kind, int Count)
{
    public override string ToString() => Count == 1 ? Kind : $"{Kind} x{Count}";
}

/// <summary>一个升级变体上的 hook 集合：**逐变体显式声明**，没有声明就是没有（不继承另一个变体）。</summary>
public sealed record AuthorHooks(
    IReadOnlyList<AuthorStep> OnExhaust,
    IReadOnlyList<AuthorStep> TurnEndInHand,
    IReadOnlyList<AuthorStep> OnEnterCombat,
    IReadOnlyList<AuthorStep> BeforeCardPlayed,
    IReadOnlyList<AuthorStep> AfterCardPlayed)
{
    public static readonly AuthorHooks None = new([], [], [], [], []);

    public System.Collections.Immutable.ImmutableDictionary<string, Effects.CardEffectProgram>? Programs { get; init; }
    public Effects.CardEffectProgram? ProgramFor(string hook) => Programs?.GetValueOrDefault(hook, Effects.CardEffectProgram.Empty);
    public bool Has(string hook) => ProgramFor(hook) is { } program ? program.Effects.Length > 0 : For(hook).Count > 0;

    public bool IsEmpty => (Programs is null || Programs.Values.All(p => p.Effects.IsEmpty))
        && OnExhaust.Count == 0 && TurnEndInHand.Count == 0 && OnEnterCombat.Count == 0
        && BeforeCardPlayed.Count == 0 && AfterCardPlayed.Count == 0;

    public IReadOnlyList<AuthorStep> For(string hook) => hook switch
    {
        AuthorSteps.HookOnExhaust => OnExhaust,
        AuthorSteps.HookOnDiscard => [],
        AuthorSteps.HookTurnEndInHand => TurnEndInHand,
        AuthorSteps.HookOnEnterCombat => OnEnterCombat,
        AuthorSteps.HookBeforeCardPlayed => BeforeCardPlayed,
        AuthorSteps.HookAfterCardPlayed => AfterCardPlayed,
        "after_auto_post_play" => [],
        _ => throw new SliceAuthorError("unknown_hook", hook, $"未知 hook '{hook}'"),
    };
}

public sealed record AuthorCard(
    string Id,
    string Name,
    string Kind,
    int Cost,
    bool IsXCost,
    int NormalCost,
    int UpgradedCost,
    int NormalAmount,
    int UpgradedAmount,
    string PowerId,
    int NormalStacks,
    int UpgradedStacks,
    IReadOnlyList<AuthorStep> NormalSteps,
    IReadOnlyList<AuthorStep> UpgradedSteps,
    string NormalAmountSource,
    string UpgradedAmountSource,
    int NormalAmountBase,
    int UpgradedAmountBase,
    int NormalAmountPerCount,
    int UpgradedAmountPerCount,
    bool NormalRetain,
    bool UpgradedRetain,
    bool NormalUnpowered,
    bool UpgradedUnpowered,
    AuthorConditional? NormalConditional,
    AuthorConditional? UpgradedConditional,
    bool NormalUnplayable,
    bool UpgradedUnplayable,
    bool NormalEthereal,
    bool UpgradedEthereal,
    IReadOnlyDictionary<string, int> NormalVariables,
    IReadOnlyDictionary<string, int> UpgradedVariables,
    string NormalGrowVariable,
    string UpgradedGrowVariable,
    int NormalGrowAmount,
    int UpgradedGrowAmount,
    AuthorHooks NormalHooks,
    AuthorHooks UpgradedHooks,
    string SourcePath,
    int SourceLine)
{
    public Effects.CardEffectAmount? NormalDamageValue { get; init; }
    public Effects.CardEffectAmount? UpgradedDamageValue { get; init; }
    public bool? NormalXCost { get; init; }
    public int NormalStarCost { get; init; }
    public bool NormalStarXCost { get; init; }
    public bool UpgradedStarXCost { get; init; }
    public int UpgradedStarCost { get; init; }
    public bool? UpgradedXCost { get; init; }
    public bool CostReductionPerAttack { get; init; }
    public bool CanGenerateInCombat { get; init; } = true;
    public bool NormalInnate { get; init; }
    public bool UpgradedInnate { get; init; }
    public string DamageVariable { get; init; } = "";

    /// <summary>Formal source programs. Null identifies the legacy diagnostic adapter, never an empty effect.</summary>
    public Effects.CardEffectProgram? NormalProgram { get; init; }
    public Effects.CardEffectProgram? UpgradedProgram { get; init; }
    public Effects.CardResultPolicy NormalResult { get; init; }
    public Effects.CardResultPolicy UpgradedResult { get; init; }

    /// <summary>
    /// ENV-T05：**奖励池注册元数据**（来源 `.spire` 的 `normal.rarity`；`.author` 路径不声明时为
    /// 空串）。奖励稀有度 roll 的候选集合按这个字段分桶；它不属于效果语义，故不影响任何既有降级。
    /// </summary>
    public string Rarity { get; init; } = string.Empty;

    /// <summary>升级形态声明的稀有度（源侧应与普通形态一致；不一致时奖励池构建期具名失败）。</summary>
    public string UpgradedRarity { get; init; } = string.Empty;

    /// <summary>卡级 `pools = { … }` 的池名（源级形式）。空＝未声明池。</summary>
    public IReadOnlyList<string> Pools { get; init; } = [];

    /// <summary>卡级 `upgradeable`（false＝升级 roll 不会改这张牌）。</summary>
    public bool Upgradeable { get; init; } = true;

    /// <summary>卡级 `multiplayerOnly`（true＝单机池不含它）。</summary>
    public bool MultiplayerOnly { get; init; }

    /// <summary>
    /// ENV-T05：卡级 `tags = { Strike }` 的**规范 id**（已补包前缀，如 `base-spire:strike`）。
    /// 标签是卡牌**定义**的属性，全部实例共享；供 `cardsWithTag` 的牌区计数使用。
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>该卡是否会在**普通**形态把自己消耗掉（决定结果区是消耗区还是弃牌堆）。</summary>
    public bool NormalExhaustsSelf =>
        AuthorSteps.StepKinds(NormalSteps).Contains(AuthorSteps.ExhaustSelf, StringComparer.Ordinal);

    /// <summary>升级形态是否消耗自己（逐变体独立：升级变体可以不同）。</summary>
    public bool UpgradedExhaustsSelf =>
        AuthorSteps.StepKinds(UpgradedSteps).Contains(AuthorSteps.ExhaustSelf, StringComparer.Ordinal);

    /// <summary>普通形态的步骤名（供校验与检查按词表比对）。</summary>
    public IReadOnlyList<string> StepKinds => AuthorSteps.StepKinds(NormalSteps);
}

/// <summary>
/// 能力在**所属者回合末**的行为程序（内容声明，不是按 id 硬编码）：
/// `none` 不随时间变化；`tick` 每次减 1（首次跳过）；`convert-to &lt;power&gt;` 转成另一种能力（层数相同）。
/// </summary>
public sealed record AuthorPower(
    string Id,
    string Name,
    string Role,
    string Effect,
    string OwnerTurnEnd,
    string DamageModifier,
    string DamageModifierValue,
    /// <summary>
    /// `afterEnemyTurnEnd` / `afterPlayerTurnEnd` 声明的**哪一侧**回合末："enemy" / "player" / 空。
    /// 来源：Vulnerable/Weak/Frail 都在 `AfterSideTurnEnd(side == Enemy)` 递减，NoDraw 在拥有者回合末。
    /// </summary>
    string TurnEndSide,
    /// <summary>
    /// ENV-02：受击修正的**来源条件**——只有当**攻击者**身上有这条能力（层数 &gt; 0）时该修正才生效。
    ///
    /// 来源：`ColossusPower.ModifyDamageMultiplicative` 里除了 `target == Owner`、`props.IsPoweredAttack()`
    /// 之外，还要求 `dealer.HasPower&lt;VulnerablePower&gt;()`——即"打我的那个人身上有易伤"。
    /// 空串＝没有这个条件（原有行为不变）。
    /// </summary>
    string DamageModifierSourcePower,
    /// <summary>
    /// ENV-02：每层给拥有者**加多少最大能量**（来源 `PyrePower.ModifyMaxEnergy` 的
    /// `amount + Amount`，即每层 +1）。0＝没有这个效果。
    /// </summary>
    int MaxEnergyPerStack,
    /// <summary>
    /// ENV-02：拥有者**作为攻击者**时，把受击方的**易伤倍率**每层提高多少个百分点
    /// （来源 `CrueltyPower.ModifyVulnerableMultiplier` 的 `amount + Amount / 100m`）。0＝无该效果。
    /// </summary>
    int VulnerableMultiplierPercentPerStack,
    /// <summary>
    /// ENV-02：**临时力量**能力——持有期间按层数给拥有者 `+1` / `−1`（每个符号各占一位）力量，
    /// 能力被移除时回补相反的量。来源 `TemporaryStrengthPower`（`BeforeApplied` 施加 `Sign × amount`，
    /// `AfterSideTurnEnd` 移除自身并施加 `−Sign × Amount`）；`ManglePower` 的 `IsPositive = false` ⇒ −1。
    /// 0＝不是临时力量能力。
    /// </summary>
    int TemporaryStrengthPerStack,
    string DamageCap,
    string Trigger,
    string WhenCardType,
    string TriggerEffect,
    bool PreventEnergyGain,
    /// <summary>W-06：拥有者回合开始时格挡**不清零**（来源 BarricadePower 的 `preventsBlockClear`）。</summary>
    bool PreventsBlockClear,
    /// <summary>
    /// W-06：**牌规则**（来源 `cardRule = cardRule(type=skill, cost=0, resultPile=exhaust)`）。
    /// 持有该能力时，这一**类**牌的费用/结果区/消耗行为被改写；空类型＝没有牌规则。
    /// </summary>
    string CardRuleType,
    /// <summary>`cardRule(cost=N)`：这类牌的费用（-1＝不改费用）。只允许**降低**费用（见运行时判定）。</summary>
    int CardRuleCost,
    /// <summary>`cardRule(resultPile=exhaust)`：这类牌打出后进哪个牌区（空＝按牌自己的行为）。</summary>
    string CardRuleResultPile,
    /// <summary>`cardRule(consumeOnPlay=true)`：打出这类牌时把该能力消耗掉（一次性）。</summary>
    bool CardRuleConsumeOnPlay,
    string SourcePath,
    int SourceLine,
    /// <summary>
    /// ENV-02：**获得格挡时的乘数**（来源 `blockGainModifiers = [modifier(multiplier=2, require=[move],
    /// priorEntriesPerStack=1, playerOnly=true)]`，例如 UnmovablePower）。空串＝没有该修正。
    /// 乘数用字符串保存，与 `PowerDamage` 的写法一致（固定点两位小数）。
    /// </summary>
    string BlockGainMultiplier = "",
    /// <summary>
    /// 每层能力**豁免多少次**本回合已有的"由牌获得格挡"（来源 `priorEntriesPerStack`）：
    /// 本回合已有的这类次数（不含本次出牌）< 层数 × 该值时才乘。
    /// </summary>
    int BlockGainPriorEntriesPerStack = 0,
    /// <summary>
    /// 每层给**拥有者自己**的 powered 获得格挡**加**多少点（来源 `DexterityPower.ModifyBlockAdditive`：
    /// `block + Amount`）。加性阶段在乘性阶段之前，最后只截断一次。0 = 无。
    /// </summary>
    int BlockGainAdditivePerStack = 0)
{
    public IReadOnlyList<PowerRule> Rules { get; init; } = [];
    public bool NegativeStacksAreInfinite { get; init; }
}

/// <summary>
/// 能力的**伤害修正**声明（按所属者/承受者与加减乘区分）：
/// `none` 不参与；`add-per-stack:&lt;int&gt;` 攻击者每层加 N；`multiply-outgoing:&lt;decimal&gt;` 攻击者侧乘；
/// `multiply-incoming:&lt;decimal&gt;` 承受者侧乘。乘数必须是固定点可表示的两位小数。
/// </summary>
public static class PowerDamage
{

    public const string None = "none";
    public const string AddPerStack = "add-per-stack";
    public const string MultiplyOutgoing = "multiply-outgoing";
    public const string MultiplyIncoming = "multiply-incoming";

    public static readonly IReadOnlyList<string> All =
        [None, AddPerStack, MultiplyOutgoing, MultiplyIncoming];
}

/// <summary>
/// 能力的**触发点**（声明式）：来源 `ironclad-powers.spire` 里的 `trigger = afterCardExhausted` 一类。
/// 只有切片**真的会触发**的时点才能声明（否则会出现"能力在、效果不在"）。
/// </summary>
public static class AuthorPowerTrigger
{
    /// <summary>一张牌被消耗之后（来源 `trigger = afterCardExhausted`）。</summary>
    public const string AfterCardExhausted = "after_card_exhausted";

    /// <summary>一张牌被打出之后（来源 `trigger = afterCardPlayed`；可配 `when-card-type`）。</summary>
    public const string AfterCardPlayed = "after_card_played";

    /// <summary>
    /// 拥有者**受到 powered 伤害之后**（来源 `events = [event(type=damage, owner=target,
    /// require=[move], exclude=[unpowered], positiveAmount=false, …)]`，例如 FlameBarrierPower 的反击）。
    /// 当前运行时只对**玩家持有**的能力展开这条链（敌人侧的同类能力记为未支持）。
    /// </summary>
    public const string AfterDamageTaken = "after_damage_taken";

    /// <summary>
    /// **玩家回合开始**（来源 `trigger = afterPlayerTurnStart`，例如 DemonFormPower：
    /// `effect = grantPower(Strength, sourceStacks)`＝每回合开始按自身层数获得某个能力）。
    /// </summary>
    public const string AfterPlayerTurnStart = "after_player_turn_start";

    /// <summary>
    /// 拥有者**获得格挡之后**（来源 `events = [event(type=block, owner=source,
    /// effect=randomAttack(...))]`，例如 JuggernautPower：每次获得格挡就随机攻击一个敌人）。
    /// 当前运行时只对**玩家获得格挡**展开这条链（切片里只有玩家会获得格挡）。
    /// </summary>
    public const string AfterBlockGained = "after_block_gained";

    /// <summary>
    /// `after_power_applied:&lt;能力 id&gt;`：**施加了指定能力之后**（来源
    /// `events = [event(type=power, owner=source, power=Vulnerable, ownerTurnOnly=false,
    /// effect=draw(scaled(0, 1, count=targetPowerAmount(自己, target=self))))]`，例如 ViciousPower：
    /// "每当你施加易伤，抽 1 张牌"）。
    ///
    /// 已知范围限制：切片的施加点不区分"谁施加的"，因此这条链只在**施加对象是敌人**时展开
    /// （= 玩家施加的），这也是当前内容里唯一的形态；`ownerTurnOnly=false` 在本内容里不改变结果
    ///（玩家不会在敌人回合施加易伤）。
    /// </summary>
    public const string AfterPowerAppliedPrefix = "after_power_applied:";
}

/// <summary>触发效果的词表（对应来源的 `effect = gainBlock(1, perPowerStack=…)`）。</summary>
public static class PowerTriggerEffect
{
    /// <summary>`gain-block-per-stack:&lt;n&gt;`：每层能力给 n 点格挡（格挡给了能力持有者自己）。</summary>
    public const string GainBlockPerStackPrefix = "gain-block-per-stack:";

    /// <summary>
    /// `retaliate-per-stack:&lt;n&gt;`：每层能力对**伤害来源**反击 n 点（无修正伤害）。
    /// 来源 `effect = scaledAttack(0, 1, count=targetPowerAmount(自己, target=self), properties=[unpowered])`。
    /// </summary>
    public const string RetaliatePerStackPrefix = "retaliate-per-stack:";

    /// <summary>
    /// `draw-per-stack:&lt;n&gt;`：按自身层数抽 `n × 层数` 张牌（来源
    /// `effect=draw(scaled(0, n, count=targetPowerAmount(自己, target=self)))`，例如 ViciousPower）。
    /// </summary>
    public const string DrawPerStackPrefix = "draw-per-stack:";

    /// <summary>
    /// `gain-power-per-stack:&lt;n&gt;:&lt;power-id&gt;`：给持有者自己施加 `n × 自身层数` 层 `<power-id>`。
    /// 来源 `effect = grantPower(&lt;PowerName&gt;, sourceStacks)`（DemonFormPower：每回合开始按自身层数加力量）。
    /// </summary>
    public const string GainPowerPerStackPrefix = "gain-power-per-stack:";

    /// <summary>
    /// `random-attack-per-stack:&lt;n&gt;`：按**命名随机流**从存活敌人里抽一个，造成 `n × 自身层数` 点
    /// **无修正**伤害。来源 `effect = randomAttack(scaled(0, n, count=targetPowerAmount(自己, target=self)),
    /// properties=[unpowered])`（JuggernautPower：每次获得格挡随机打一下）。
    /// </summary>
    public const string RandomAttackPerStackPrefix = "random-attack-per-stack:";

    /// <summary>`gain-block:&lt;n&gt;`：给持有者固定 n 点格挡（来源 `gainBlock(n, target=self)`，不按层数）。</summary>
    public const string GainBlockPrefix = "gain-block:";

    /// <summary>`remove-self`：把声明这条效果的**能力自己**移除（来源 `removePower(自己)`，一次性能力）。</summary>
    public const string RemoveSelf = "remove-self";

    /// <summary>
    /// `decrement-self`：把**自身**层数减 1，减到 0 就移除（来源
    /// `trigger(event=afterOwnerTurnStart, effect=decrementPower(自己))`，例如 PlatingPower）。
    /// 与 `remove-self` 取实例的写法相同，只把 Destroy 换成"减 1 再判 0"。
    /// </summary>
    public const string DecrementSelf = "decrement-self";

    /// <summary>
    /// 一次触发里的**多个效果**用 `|` 连接（来源 `effect = sequence(A, B)`，例如 SelfFormingClayPower：
    /// `sequence(gainBlock(3, target=self), removePower(自己))`）。执行顺序就是声明顺序。
    /// </summary>
    public const char SequenceSeparator = '|';
}

/// <summary>能力回合末行为的三种形态（与 <see cref="AuthorPower.OwnerTurnEnd" /> 对应）。</summary>
public static class PowerTurnEnd
{
    public const string None = "none";
    public const string Tick = "tick";
    /// <summary>回合末把自身层数转成 Strength（切片里唯一的转换方向，因此不需要引用别的能力）。</summary>
    public const string ConvertToStrength = "convert-to-strength";

    /// <summary>
    /// **对手**回合末把整个能力移除（来源 `removeAfterOpponentTurnEnd = true`，例如 FlameBarrierPower）：
    /// 玩家持有的这类能力在**敌人**回合结束时移除（因此它覆盖敌人的整个回合）。
    /// </summary>
    public const string RemoveAtOpponentTurnEnd = "remove-at-opponent-turn-end";

    /// <summary>回合末把**整个能力**移除（来源 `removeAfterPlayerTurnEnd = true`，例如 RagePower）。</summary>
    public const string Remove = "remove";

    /// <summary>
    /// 回合末按自身层数给拥有者 `n × 层数` 点格挡（来源 `trigger(event=afterOwnerTurnEnd,
    /// effect=gainBlock(n, perPowerStack=自身))`，例如 PlatingPower）。
    /// 与 `convert-to-strength` 同款：一个"回合末转换"种类，不引入新状态。
    /// </summary>
    public const string GainBlockPerStackPrefix = "gain-block-per-stack:";

    /// <summary>
    /// 回合末对**拥有者自己**造成 `base + perStack × 层数` 点 **unpowered** 伤害（来源
    /// `ConstrictPower.AfterSideTurnEnd`：`participants.Contains(Owner)` 时对自己造成 `Amount` 点
    /// `ValueProp.Unpowered` 伤害）。格式：`damage-owner-unpowered:base:perStack`。
    /// </summary>
    public const string DamageOwnerUnpoweredPrefix = "damage-owner-unpowered:";
}

/// <summary>敌人行动程序里的一个动作：`ritual = &lt;int|first-ritual&gt;` 或 `damage = &lt;int|strike&gt;`。</summary>
public sealed record AuthorMoveStep(string Kind, string Value)
{
    public override string ToString() => $"{Kind} = {Value}";
}

/// <summary>一个具名行动（`move dark-strike { damage = strike }`）。</summary>
public sealed record AuthorMove(string Name, IReadOnlyList<AuthorMoveStep> Steps, bool Unpowered = false)
{
    public bool IsEmpty => Steps.Count == 0;
}

/// <summary>
/// 一个敌人的**行动序列**：先按 <see cref="Sequence"/> 各走一次，走完后从 <see cref="RepeatFrom"/> 开始循环。
///
/// 这条模型覆盖原版敌人行为里最常见的三种形状（来源 `ContentSource/enemies*.spire` 的 `behavior`）：
/// <list type="bullet">
/// <item><c>loop(x)</c> → 序列 [x]，RepeatFrom 0；</item>
/// <item><c>cycle(a,b,c)</c> → 序列 [a,b,c]，RepeatFrom 0；</item>
/// <item><c>startWith(a, then=loop(x))</c> / <c>startWith(a, then=cycle(b,c))</c> →
///       序列 [a,x] / [a,b,c]，RepeatFrom 1。</item>
/// </list>
/// `RepeatFrom = -1` 表示"沿用旧语义：最后一个行动永远重复"（冻结 `.author` 文本的写法）。
/// 带权重/冷却/图结构的 `chooseWithLimits` / `graph` / `startWithChoice` **不在**这条模型里，
/// 由 <see cref="SpireEnemySource"/> 逐条记为未支持。
/// </summary>
public sealed record AuthorEnemy(
    string Id,
    string Name,
    int NormalMinHp,
    int NormalMaxHp,
    int ToughMinHp,
    int ToughMaxHp,
    int FirstMoveRitual,
    int StrikeDamageNormal,
    int StrikeDamageDeadly,
    string FirstMove,
    string RepeatMove,
    IReadOnlyList<AuthorMove> Moves,
    IReadOnlyList<string> Cycle,
    string SourcePath,
    int SourceLine)
{
    /// <summary>循环起点下标；-1 = 最后一个行动永远重复（冻结 `.author` 语义）。</summary>
    public int RepeatFrom { get; init; } = -1;

    /// <summary>
    /// 出生格挡（来源 `startingBlock = N`）。0 = 无。
    /// 注意：原版敌人获得格挡还要过 Dexterity/Frail 等修正阶段，本包当前按**未修正**加，
    /// 该差异由构建描述列出（见 `BaseSpireBuildDescription.Limitations`）。
    /// </summary>
    public int StartingBlock { get; init; }

    /// <summary>
    /// 出生时挂上的能力 `[(能力 id, 层数)]`（来源 `startingPowers = [power(Slow, 1), …]`）。
    /// 出生期施加点与战斗中的施加走**同一条**路径（`PowerCmd.Apply`），因此这里只带数据、不写效果。
    /// </summary>
    public IReadOnlyList<(string PowerId, int Stacks)> StartingPowers { get; init; } = [];

    /// <summary>
    /// 声明里出现了 `variants(default=…, deadly=…)` 这类**进阶变体**。降级时取 `default`（进阶 0），
    /// 因此进阶 &gt; 0 的会话必须在建局前拒绝，而不是静默用进阶 0 的数值。
    /// </summary>
    public bool HasAscensionVariants { get; init; }

    /// <summary>
    /// **行动状态机**（正式 `.spire` 源经 <see cref="SpireEnemySource"/> 解析得到）。
    /// 非空时运行时用状态机选取行动（权重/冷却/连续上限/有序条件/图转移），
    /// <see cref="Cycle"/>/<see cref="RepeatFrom"/> 只描述冻结 `.author` 文本的旧形状。
    /// </summary>
    public AuthorEnemyBehavior? Behavior { get; init; }
}

public sealed record AuthorDeckEntry(string CardId, int Count, bool Upgraded = false);

/// <summary>
/// 一条药水声明。`use` 的效果步骤**复用牌的词表**（因此自动继承全部既有步骤校验与运行时语义）；
/// `usage` 当前只支持 `combat-only`，`target` 支持 `enemy` / `self`。
/// </summary>
public sealed record AuthorPotion(
    string Id,
    string Name,
    string Rarity,
    string Usage,
    string Target,
    int Amount,
    string AmountSource,
    string PowerId,
    int PowerStacks,
    IReadOnlyList<AuthorStep> Steps,
    bool Unpowered,
    string SourcePath,
    int SourceLine)
{
    public Effects.CardEffectProgram? Program { get; init; }
}

/// <summary>药水栏里的一条（与卡组的 `xN` 写法一致）。</summary>
public sealed record AuthorBeltEntry(string PotionId, int Count);

public static class PotionRarities
{
    public static readonly IReadOnlyList<string> All = ["common", "uncommon", "rare"];
}

public static class PotionUsages
{
    /// <summary>当前切片只支持战斗内使用（战斗外使用属 run 流程，见范围说明）。</summary>
    public const string CombatOnly = "combat-only";

    public static readonly IReadOnlyList<string> All = [CombatOnly];
}

public static class PotionTargets
{
    public const string Enemy = "enemy";
    public const string Self = "self";

    public static readonly IReadOnlyList<string> All = [Enemy, Self];
}

/// <summary>
/// 整局（run）级的**初始状态声明**（B-07a 第一小批）：长期 HP/最大 HP/金币。
/// 长期牌组沿用既有的 `deck { … }` 声明（它就是"这一局的牌组"），战斗里的临时牌不进它。
/// </summary>
public sealed record AuthorRun(int Hp, int MaxHp, int Gold);

public sealed record SliceAuthorModel(
    string PackageId,
    string Version,
    IReadOnlyList<AuthorDeckEntry> Deck,
    IReadOnlyList<AuthorCard> Cards,
    IReadOnlyList<AuthorPower> Powers,
    IReadOnlyList<AuthorEnemy> Enemies,
    IReadOnlyList<AuthorPotion> Potions,
    IReadOnlyList<AuthorBeltEntry> Belt,
    AuthorRun Run)
{
    public IReadOnlyList<PotionDefinition> PotionPool { get; init; } = [];
    public int BaseOrbSlots { get; init; }
}

/// <summary>作者前端的编译期错误：一律带**路径**（`card bash.normal.steps[1]`），不静默忽略。</summary>
public sealed class SliceAuthorError(string code, string path, string message)
    : Exception($"[{code}] {path}: {message}")
{
    public string Code { get; } = code;

    public string Path { get; } = path;
}

/// <summary>
/// B-05b 的作者前端：把切片内容的文本表示解析成声明模型，并降级为程序化包本来就消费的记录
/// （<see cref="SliceCard"/> / <see cref="SlicePower"/> / <see cref="SliceEnemy"/>），同时保留显式的
/// **效果步骤列表**供包构建器生成指令序列。
///
/// 边界：只做词法/语法/声明校验与降级，**不实现规则**，也不引用 Legacy 类型——编译目标是
/// <c>CoreTheSpire.Engine</c> 的包构建器。
///
/// 语法（`//` 或 `#` 起为注释）：
/// <code>
/// package base-spire { version = combat-slice-v1 }
/// deck { strike-ironclad x5  bash }
/// power strength { name = Strength  role = Strength  effect = "..."  source = base-spire/...  line = 2 }
/// enemy damp-cultist { name = DampCultist  hp = 51  tough-hp = 52  first-move-ritual = 5
///                      strike-damage = 1  strike-damage-deadly = 3  first-move = Incantation
///                      repeat-move = DarkStrike  source = base-spire/...  line = 221 }
/// card bash { name = Bash  kind = Attack  cost = 2  source = base-spire/...  line = 1459
///             normal   { amount = 8  power = vulnerable  stacks = 2  steps = [target, damage, apply_power] }
///             upgraded { amount = 10 power = vulnerable  stacks = 3  steps = [target, damage, apply_power] } }
/// </code>
/// </summary>
public static class SliceAuthorFrontend
{
    public const string ResourceName = "BaseSpire.SliceAuthor.slice-content.author";

    private static readonly HashSet<string> CardKeys =
        new(["name", "kind", "cost", "source", "line", "normal", "upgraded"], StringComparer.Ordinal);

    private static readonly HashSet<string> VariantKeys =
        new(["amount", "amount-source", "amount-base", "amount-per-count", "power", "stacks", "steps", "retain",
            "grow", "grow-amount", "cost", "properties",
            "unplayable", "ethereal"], StringComparer.Ordinal);

    private static readonly HashSet<string> PotionKeys =
        new(["name", "rarity", "usage", "target", "source", "line"], StringComparer.Ordinal);

    private static readonly HashSet<string> PotionUseKeys =
        new(["amount", "amount-source", "power", "stacks", "steps", "properties"], StringComparer.Ordinal);

    private static readonly HashSet<string> PowerKeys =
        new(["name", "role", "effect", "owner-turn-end", "damage", "damage-cap", "trigger", "when-card-type",
            "trigger-effect", "prevent-energy-gain", "damage-source-power", "max-energy-per-stack",
            "vulnerable-multiplier-percent-per-stack", "temporary-strength-per-stack",
            "block-gain-multiplier", "block-gain-prior-entries-per-stack",
            "source", "line"],
            StringComparer.Ordinal);

    private static readonly HashSet<string> RunKeys =
        new(["hp", "max-hp", "gold"], StringComparer.Ordinal);

    private static readonly HashSet<string> EnemyKeys =
        new(["name", "hp", "tough-hp", "first-move-ritual", "strike-damage", "strike-damage-deadly",
            "first-move", "repeat-move", "cycle", "source", "line"], StringComparer.Ordinal);

    /// <summary>敌人行动程序允许的动作：`ritual`（给自己上 Ritual）与 `damage`（打玩家）。</summary>
    private static readonly IReadOnlyList<string> MoveStepKinds = ["ritual", "damage"];

    public static SliceAuthorModel Load()
    {
        using var stream = typeof(SliceAuthorFrontend).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new SliceAuthorError("missing_resource", ResourceName,
                "作者文本没有作为嵌入资源打包（检查 BaseSpire.csproj 的 EmbeddedResource）");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd(), "base-spire/Slice/Content/slice-content.author");
    }

    // -- 解析 -----------------------------------------------------------------------------------------------

    public static SliceAuthorModel Parse(string text, string sourcePath)
    {
        var authorParser = new AuthorParser(Tokenize(text), sourcePath);
        return authorParser.ParseModel();
    }

    private static List<AuthorToken> Tokenize(string text)
    {
        var tokens = new List<AuthorToken>();
        var line = 1;
        var index = 0;
        while (index < text.Length)
        {
            var current = text[index];
            if (current == '\n')
            {
                line++;
                index++;
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                while (index < text.Length && text[index] != '\n') index++;
                continue;
            }

            if (current == '#')
            {
                while (index < text.Length && text[index] != '\n') index++;
                continue;
            }

            if ("{}[]=,".Contains(current))
            {
                tokens.Add(new AuthorToken(current.ToString(), line));
                index++;
                continue;
            }

            if (current == '"')
            {
                var start = ++index;
                while (index < text.Length && text[index] != '"') index++;
                if (index >= text.Length)
                {
                    throw new SliceAuthorError("syntax", $"{line}", "字符串没有闭合的引号");
                }

                tokens.Add(new AuthorToken(text[start..index], line));
                index++;
                continue;
            }

            var wordStart = index;
            while (index < text.Length && !char.IsWhiteSpace(text[index]) && !"{}[]=,".Contains(text[index])) index++;
            tokens.Add(new AuthorToken(text[wordStart..index], line));
        }

        return tokens;
    }

    private sealed record AuthorToken(string Text, int Line);

    private sealed record Pairs(List<(string Key, string Value, int Line)> Items,
        Dictionary<string, Pairs> Blocks);

    private sealed class AuthorParser(List<AuthorToken> tokens, string sourcePath)
    {
        private int _position;

        public SliceAuthorModel ParseModel()
        {
            var packageId = "base-spire";
            var version = "combat-slice-v1";
            var deck = new List<AuthorDeckEntry>();
            var cards = new List<AuthorCard>();
            var powers = new List<AuthorPower>();
            var enemies = new List<AuthorEnemy>();
            var potions = new List<AuthorPotion>();
            var belt = new List<AuthorBeltEntry>();
            // 整局初始状态：默认与切片玩家一致（80/80/99）；作者可显式声明 `run { … }`。
            var run = new AuthorRun(80, 80, 99);

            while (_position < tokens.Count)
            {
                var keyword = Next();
                switch (keyword.Text)
                {
                    case "package":
                    {
                        ExpectOptionalWord(); // package id 可省略
                        var body = ReadPairs("package");
                        foreach (var (key, value, _) in body.Items)
                        {
                            switch (key)
                            {
                                case "id": packageId = value; break;
                                case "version": version = value; break;
                                default: throw UnknownKey("package", key, keyword.Line);
                            }
                        }

                        break;
                    }

                    case "deck":
                    {
                        Expect("{", "deck");
                        while (Peek()?.Text != "}")
                        {
                            var id = Next();
                            var count = 1;
                            if (Peek()?.Text is { } maybeCount && maybeCount.StartsWith('x')
                                && int.TryParse(maybeCount[1..], out var parsed))
                            {
                                count = parsed;
                                _position++;
                            }

                            if (count <= 0)
                            {
                                throw new SliceAuthorError("invalid_value", $"deck[{id.Line}]",
                                    $"'{id.Text}' 的份数必须为正");
                            }

                            deck.Add(new AuthorDeckEntry(id.Text, count));
                        }

                        Expect("}", "deck");
                        break;
                    }

                    case "power":
                    {
                        var id = Next().Text;
                        var body = ReadPairs($"power {id}");
                        foreach (var (key, _, line) in body.Items)
                        {
                            if (!PowerKeys.Contains(key)) throw UnknownKey($"power {id}", key, line);
                        }

                        var turnEnd = ParsePowerTurnEnd(Get(body, "owner-turn-end"), $"power {id}");
                        var (modifier, modifierValue) = ParsePowerDamage(Get(body, "damage"), $"power {id}");
                        // ENV-02：受击修正的来源条件（"打我的那个人身上有 X"）。
                        // `.author` 里用 `damage-source-power = vulnerable`；正式源里由 `sourcePower=` 映射进来。
                        var modifierSourcePower = Get(body, "damage-source-power") ?? string.Empty;
                        // ENV-02：`max-energy-per-stack = 1`（来源 PyrePower.ModifyMaxEnergy）：
                        // 每层给拥有者 +N 最大能量，在回合开始的能量重置处体现。
                        var maxEnergyPerStack = Get(body, "max-energy-per-stack") is { } energyText
                            ? ParseInt(energyText, $"power {id}.max-energy-per-stack")
                            : 0;
                        // ENV-02：`vulnerable-multiplier-percent-per-stack = 1`（来源 CrueltyPower）：
                        // 拥有者作为攻击者时把易伤倍率每层提高 N 个百分点。
                        var vulnerablePercentPerStack =
                            Get(body, "vulnerable-multiplier-percent-per-stack") is { } percentText
                                ? ParseInt(percentText, $"power {id}.vulnerable-multiplier-percent-per-stack")
                                : 0;
                        // ENV-02：临时力量能力（`.author` 侧用 `temporary-strength-per-stack = ±1`）。
                        var temporaryStrengthPerStack =
                            Get(body, "temporary-strength-per-stack") is { } strengthText
                                ? ParseInt(strengthText, $"power {id}.temporary-strength-per-stack")
                                : 0;
                        // ENV-02：获得格挡时的乘数（`.author` 侧：`block-gain-multiplier` + 豁免次数）。
                        var blockGainMultiplier = Get(body, "block-gain-multiplier") ?? string.Empty;
                        var blockGainPriorEntries =
                            Get(body, "block-gain-prior-entries-per-stack") is { } priorText
                                ? ParseInt(priorText, $"power {id}.block-gain-prior-entries-per-stack")
                                : 0;
                        var damageCap = ParseDamageCap(Get(body, "damage-cap"), $"power {id}");
                        var trigger = ParsePowerTrigger(Get(body, "trigger"), $"power {id}");
                        var whenCardType = ParseWhenCardType(Get(body, "when-card-type"), trigger, $"power {id}");
                        var triggerEffect = ParseTriggerEffect(Get(body, "trigger-effect"), trigger, $"power {id}");
                        var preventEnergyGain = ParseBoolean(body, "prevent-energy-gain", $"power {id}");
                        // W-06：`prevents-block-clear`（来源 `ironclad-powers.spire` 的 BarricadePower
                        // `preventsBlockClear = true`）：拥有者回合开始时格挡**不清零**。
                        var preventsBlockClear = ParseBoolean(body, "prevents-block-clear", $"power {id}");
                        // W-06：`cardRule = cardRule(type=skill, cost=0, resultPile=exhaust)`（CorruptionPower 等）。
                        var cardRuleType = Get(body, "card-rule-type") ?? string.Empty;
                        var cardRuleCost = Get(body, "card-rule-cost") is { } costText
                            ? ParseInt(costText, $"power {id}.card-rule-cost")
                            : -1;
                        var cardRuleResultPile = Get(body, "card-rule-result-pile") ?? string.Empty;
                        var cardRuleConsumeOnPlay = ParseBoolean(body, "card-rule-consume-on-play", $"power {id}");
                        powers.Add(new AuthorPower(id,
                            Require(body, "name", $"power {id}"),
                            Require(body, "role", $"power {id}"),
                            Require(body, "effect", $"power {id}"),
                            turnEnd,
                            modifier,
                            modifierValue,
                            // 冻结 `.author` 文本没有"哪一侧回合末"的区分：按旧语义（拥有者自己那一侧）处理。
                            string.Empty,
                            modifierSourcePower,
                            maxEnergyPerStack,
                            vulnerablePercentPerStack,
                            temporaryStrengthPerStack,
                            damageCap,
                            trigger,
                            whenCardType,
                            triggerEffect,
                            preventEnergyGain,
                            preventsBlockClear,
                            cardRuleType,
                            cardRuleCost,
                            cardRuleResultPile,
                            cardRuleConsumeOnPlay,
                            Get(body, "source") ?? sourcePath,
                            ParseInt(Get(body, "line") ?? "0", $"power {id}.line"),
                            BlockGainMultiplier: blockGainMultiplier,
                            BlockGainPriorEntriesPerStack: blockGainPriorEntries));
                        break;
                    }

                    case "enemy":
                    {
                        var id = Next().Text;
                        var body = ReadPairs($"enemy {id}");
                        foreach (var (key, _, line) in body.Items)
                        {
                            if (!EnemyKeys.Contains(key)) throw UnknownKey($"enemy {id}", key, line);
                        }

                        var hp = RequireInt(body, "hp", $"enemy {id}");
                        var toughHp = RequireInt(body, "tough-hp", $"enemy {id}");
                        var moves = ParseMoves(body, $"enemy {id}");
                        var cycle = ParseCycle(Get(body, "cycle"), moves, $"enemy {id}");
                        enemies.Add(new AuthorEnemy(id,
                            Require(body, "name", $"enemy {id}"),
                            hp, hp, toughHp, toughHp,
                            RequireInt(body, "first-move-ritual", $"enemy {id}"),
                            RequireInt(body, "strike-damage", $"enemy {id}"),
                            RequireInt(body, "strike-damage-deadly", $"enemy {id}"),
                            Require(body, "first-move", $"enemy {id}"),
                            Require(body, "repeat-move", $"enemy {id}"),
                            moves,
                            cycle,
                            Get(body, "source") ?? sourcePath,
                            ParseInt(Get(body, "line") ?? "0", $"enemy {id}.line"))
                        {
                            // `.author` 的 `xforever` 只能标在最后一个行动上 ⇒ 循环起点就是最后一格。
                            RepeatFrom = cycle.Count - 1,
                        });
                        break;
                    }

                    case "card":
                    {
                        cards.Add(ReadCard(Next().Text, keyword.Line));
                        break;
                    }

                    case "potions":
                    {
                        Expect("{", "potions");
                        while (Peek()?.Text != "}")
                        {
                            var id = Next();
                            var count = 1;
                            if (Peek()?.Text is { } maybeCount && maybeCount.StartsWith('x')
                                && int.TryParse(maybeCount[1..], out var parsed))
                            {
                                count = parsed;
                                _position++;
                            }

                            if (count <= 0)
                            {
                                throw new SliceAuthorError("invalid_value", $"potions[{id.Line}]",
                                    $"'{id.Text}' 的份数必须为正");
                            }

                            belt.Add(new AuthorBeltEntry(id.Text, count));
                        }

                        Expect("}", "potions");
                        break;
                    }

                    case "potion":
                    {
                        potions.Add(ReadPotion(Next().Text, keyword.Line));
                        break;
                    }

                    // B-07a：整局级的初始状态（长期 HP/最大 HP/金币）。
                    case "run":
                    {
                        var body = ReadPairs("run");
                        foreach (var (key, _, line) in body.Items)
                        {
                            if (!RunKeys.Contains(key)) throw UnknownKey("run", key, line);
                        }

                        run = new AuthorRun(
                            RequireInt(body, "hp", "run"),
                            RequireInt(body, "max-hp", "run"),
                            RequireInt(body, "gold", "run"));
                        break;
                    }

                    default:
                        throw new SliceAuthorError("unknown_block", $"{sourcePath}:{keyword.Line}",
                            $"未知块 '{keyword.Text}'（可用：package/deck/power/enemy/card）");
                }
            }

            // 裸名字按 `package { id = ... }` 限定到包命名空间（`strike-ironclad` → `base-spire:strike-ironclad`）；
            // 已经带命名空间的 id 原样保留。作者文本因此可以写得短，但落库的身份始终是完整内容 ID。
            string Qualify(string id) => id.Contains(':') ? id : $"{packageId}:{id}";
            var qualifiedDeck = deck
                .Select(entry => new AuthorDeckEntry(Qualify(entry.CardId), entry.Count)).ToList();
            // 步骤里的 `generate:<id>` / `becomes:<id>` / `apply_power_self_from_target:<id>` 参数同样要按包命名空间限定；
            // `apply_power:<id>:<层数>` 一类只限定 **id 部分**（层数是数字，不参与限定）。
            IReadOnlyList<AuthorStep> QualifySteps(IReadOnlyList<AuthorStep> source) =>
                [.. source.Select(step =>
                {
                    var twoPart = new[]
                        {
                            AuthorSteps.ApplyPower, AuthorSteps.ApplyPowerAll, AuthorSteps.ApplyPowerSelf,
                        }
                        .FirstOrDefault(candidate => step.Kind.StartsWith(candidate + ":", StringComparison.Ordinal)
                            && step.Kind.LastIndexOf(':') > candidate.Length);
                    if (twoPart is not null)
                    {
                        var lastColon = step.Kind.LastIndexOf(':');
                        var id = step.Kind[(twoPart.Length + 1)..lastColon];
                        var stacks = step.Kind[(lastColon + 1)..];
                        return step with { Kind = $"{twoPart}:{Qualify(id)}:{stacks}" };
                    }

                    var prefix = AuthorSteps.Parameterized.FirstOrDefault(candidate =>
                        step.Kind.StartsWith(candidate, StringComparison.Ordinal));
                    return prefix is null || step.Kind.Length == prefix.Length
                        ? step
                        : step with { Kind = prefix + Qualify(step.Kind[prefix.Length..]) };
                })];

            var qualifiedCards = cards.Select(card => card with
            {
                Id = Qualify(card.Id),
                PowerId = card.PowerId.Length == 0 ? string.Empty : Qualify(card.PowerId),
                NormalSteps = QualifySteps(card.NormalSteps),
                UpgradedSteps = QualifySteps(card.UpgradedSteps),
            }).ToList();
            var qualifiedPowers = powers.Select(power => power with { Id = Qualify(power.Id) }).ToList();
            var qualifiedEnemies = enemies.Select(enemy => enemy with { Id = Qualify(enemy.Id) }).ToList();
            var qualifiedPotions = potions.Select(potion => potion with
            {
                Id = Qualify(potion.Id),
                PowerId = potion.PowerId.Length == 0 ? string.Empty : Qualify(potion.PowerId),
                Steps = QualifySteps(potion.Steps),
            }).ToList();
            var qualifiedBelt = belt.Select(entry => new AuthorBeltEntry(Qualify(entry.PotionId), entry.Count))
                .ToList();

            var model = new SliceAuthorModel(packageId, version, qualifiedDeck, qualifiedCards, qualifiedPowers,
                qualifiedEnemies, qualifiedPotions, qualifiedBelt, run);
            Validate(model, sourcePath);
            return model;
        }

        /// <summary>`potion &lt;id&gt; { name = … rarity = … usage = … target = … use { … } }`。</summary>
        private AuthorPotion ReadPotion(string id, int blockLine)
        {
            var body = ReadPairs($"potion {id}");
            foreach (var (key, _, line) in body.Items)
            {
                if (!PotionKeys.Contains(key)) throw UnknownKey($"potion {id}", key, line);
            }

            var rarity = Require(body, "rarity", $"potion {id}");
            if (!PotionRarities.All.Contains(rarity, StringComparer.Ordinal))
            {
                throw new SliceAuthorError("invalid_value", $"potion {id}.rarity",
                    $"未知稀有度 '{rarity}'（可用：{string.Join("/", PotionRarities.All)}）");
            }

            var usage = Require(body, "usage", $"potion {id}");
            if (!PotionUsages.All.Contains(usage, StringComparer.Ordinal))
            {
                throw new SliceAuthorError("invalid_value", $"potion {id}.usage",
                    $"未知用法 '{usage}'（当前只支持 {PotionUsages.CombatOnly}；战斗外使用属 run 流程）");
            }

            var target = Require(body, "target", $"potion {id}");
            if (!PotionTargets.All.Contains(target, StringComparer.Ordinal))
            {
                throw new SliceAuthorError("invalid_value", $"potion {id}.target",
                    $"未知目标 '{target}'（可用：{string.Join("/", PotionTargets.All)}）");
            }

            if (!body.Blocks.TryGetValue("use", out var use))
            {
                throw new SliceAuthorError("missing_key", $"potion {id}.use", "药水必须声明 use 块");
            }

            foreach (var (key, _, line) in use.Items)
            {
                if (!PotionUseKeys.Contains(key))
                {
                    throw new SliceAuthorError("unknown_key", $"potion {id}.use.{key}:{line}",
                        $"use 里只支持 {string.Join("/", PotionUseKeys)}");
                }
            }

            var steps = ParseSteps(use, $"potion {id}.use");
            var kinds = AuthorSteps.StepKinds(steps);
            if (kinds.Contains(AuthorSteps.Damage, StringComparer.Ordinal)
                && target != PotionTargets.Enemy)
            {
                throw new SliceAuthorError("unsupported_combination", $"potion {id}.use.steps",
                    "声明了 damage 就必须 target = enemy（需要敌人目标）");
            }

            if ((kinds.Contains(AuthorSteps.ApplyPower, StringComparer.Ordinal)
                 || kinds.Contains(AuthorSteps.ApplyPowerAll, StringComparer.Ordinal)
                 || kinds.Contains(AuthorSteps.ApplyPowerSelf, StringComparer.Ordinal))
                && Get(use, "power") is null)
            {
                throw new SliceAuthorError("missing_key", $"potion {id}.use.power",
                    "声明了 apply_power/apply_power_all 就必须给出 power id");
            }

            return new AuthorPotion(id,
                Require(body, "name", $"potion {id}"),
                rarity,
                usage,
                target,
                AmountValue(use, $"potion {id}.use"),
                ParseAmountSource(use, $"potion {id}.use"),
                Get(use, "power") is { } powerId ? powerId : string.Empty,
                ParseInt(Get(use, "stacks") ?? "0", $"potion {id}.use.stacks"),
                steps,
                ParseUnpowered(use, $"potion {id}.use"),
                Get(body, "source") ?? sourcePath,
                ParseInt(Get(body, "line") ?? blockLine.ToString(), $"potion {id}.line"));
        }

        private AuthorCard ReadCard(string id, int blockLine)
        {
            var body = ReadPairs($"card {id}");
            foreach (var (key, _, line) in body.Items)
            {
                if (!CardKeys.Contains(key)) throw UnknownKey($"card {id}", key, line);
            }

            var kind = Require(body, "kind", $"card {id}");
            if (kind is not ("Attack" or "Skill" or "Power" or "Status"))
            {
                throw new SliceAuthorError("invalid_value", $"card {id}.kind",
                    $"kind 必须是 Attack/Skill/Power/Status，得到 '{kind}'");
            }

            if (!body.Blocks.TryGetValue("normal", out var normal) || !body.Blocks.TryGetValue("upgraded", out var upgraded))
            {
                throw new SliceAuthorError("missing_key", $"card {id}",
                    "必须同时声明 normal 与 upgraded 变体");
            }

            foreach (var (variantName, variant) in new[] { ("normal", normal), ("upgraded", upgraded) })
            {
                foreach (var (key, _, line) in variant.Items)
                {
                    if (!VariantKeys.Contains(key)) throw UnknownKey($"card {id}.{variantName}", key, line);
                }
            }

            // 条件效果与固定步骤表**二选一**（来源里 `if` 块就是整个 play）。
            var normalConditional = ParseConditional(normal, $"card {id}.normal");
            var upgradedConditional = ParseConditional(upgraded, $"card {id}.upgraded");
            if (normalConditional is not null && Get(normal, "steps") is not null)
            {
                throw new SliceAuthorError("unsupported_combination", $"card {id}.normal",
                    "同时声明了 steps 与 if：一张牌的 play 只能有一种形态（不猜、不合并）");
            }

            if (upgradedConditional is not null && Get(upgraded, "steps") is not null)
            {
                throw new SliceAuthorError("unsupported_combination", $"card {id}.upgraded",
                    "同时声明了 steps 与 if：一张牌的 play 只能有一种形态（不猜、不合并）");
            }

            var steps = normalConditional is null
                ? ParseSteps(normal, $"card {id}.normal")
                : [];
            var upgradedSteps = upgradedConditional is null
                ? ParseSteps(upgraded, $"card {id}.upgraded")
                : [];
            var normalHooks = ParseHooks(normal, $"card {id}.normal");
            var upgradedHooks = ParseHooks(upgraded, $"card {id}.upgraded");
            // 外部效果也可以消耗这张牌；on_exhaust 不要求牌自身含 exhaust_self。
            // 逐变体独立：升级形态可以有自己的步骤序列与批次（运行时按 (定义, 升级) 分派）。

            var power = Get(normal, "power") ?? Get(upgraded, "power") ?? string.Empty;
            // 施加能力的三种目标域（单个目标 / 全体 / 自己）以及"按目标层数给自己"都要求给出 power。
            var stepKindsForPower = AuthorSteps.StepKinds(steps);
            var appliesAPower = stepKindsForPower.Any(kind =>
                AuthorSteps.IsStep(kind, AuthorSteps.ApplyPower)
                || AuthorSteps.IsStep(kind, AuthorSteps.ApplyPowerAll)
                || AuthorSteps.IsStep(kind, AuthorSteps.ApplyPowerSelf)
                || kind.StartsWith(AuthorSteps.ApplyPowerSelfFromTarget + ":", StringComparison.Ordinal));
            if (appliesAPower)
            {
                if (power.Length == 0)
                {
                    throw new SliceAuthorError("missing_key", $"card {id}.normal.power",
                        "声明了 apply_power/apply_power_all 就必须给出 power id");
                }
            }

            // 卡牌级 `stacks` 只有在"有施加步骤**没带**字面量层数"时才是必需的。
            var needsCardStacks = stepKindsForPower.Any(kind =>
                (AuthorSteps.IsStep(kind, AuthorSteps.ApplyPower)
                 || AuthorSteps.IsStep(kind, AuthorSteps.ApplyPowerAll)
                 || AuthorSteps.IsStep(kind, AuthorSteps.ApplyPowerSelf))
                && !kind.Contains(':', StringComparison.Ordinal));
            if (needsCardStacks && Get(normal, "stacks") is null)
            {
                throw new SliceAuthorError("missing_key", $"card {id}.normal.stacks",
                    "有施加步骤没有字面量层数时就必须在卡牌级给出 stacks");
            }

            var stepKinds = AuthorSteps.StepKinds(steps);
            var needsTarget = stepKinds.Contains(AuthorSteps.Damage, StringComparer.Ordinal)
                || stepKinds.Contains(AuthorSteps.ApplyPower, StringComparer.Ordinal)
                || stepKinds.Any(kind => kind.StartsWith(AuthorSteps.ApplyPowerSelfFromTarget + ":",
                    StringComparison.Ordinal));
            if (needsTarget && !stepKinds.Contains(AuthorSteps.Target, StringComparer.Ordinal))
            {
                throw new SliceAuthorError("missing_prerequisite", $"card {id}.normal.steps",
                    "damage/apply_power/apply_power_self_from_target 之前必须先声明 target");
            }



            // X 费：`cost = X`（与项目 DSL 约定一致）。运行时不读 Card.Cost，而是在打出时捕获剩余能量。
            var rawCost = Require(body, "cost", $"card {id}");
            var isXCost = string.Equals(rawCost, "X", StringComparison.OrdinalIgnoreCase);
            var cost = isXCost ? 0 : ParseInt(rawCost, $"card {id}.cost");

            if (isXCost)
            {
                foreach (var (variantName, variant) in new[] { ("normal", normal), ("upgraded", upgraded) })
                {
                    if (Get(variant, "amount") is { } declared && ParseInt(declared, $"card {id}.{variantName}.amount") != 0)
                    {
                        throw new SliceAuthorError("unsupported_combination", $"card {id}.{variantName}.amount",
                            "X 费牌的 amount 由打出时捕获的能量决定；声明固定值会与捕获冲突（请写 amount = 0）");
                    }
                }
            }

            return new AuthorCard(id,
                Require(body, "name", $"card {id}"),
                kind,
                cost,
                isXCost,
                VariantCost(normal, cost, $"card {id}.normal"),
                VariantCost(upgraded, cost, $"card {id}.upgraded"),
                AmountValue(normal, $"card {id}.normal"),
                AmountValue(upgraded, $"card {id}.upgraded"),
                power,
                ParseInt(Get(normal, "stacks") ?? "0", $"card {id}.normal.stacks"),
                ParseInt(Get(upgraded, "stacks") ?? "0", $"card {id}.upgraded.stacks"),
                steps,
                upgradedSteps,
                ParseAmountSource(normal, $"card {id}.normal"),
                ParseAmountSource(upgraded, $"card {id}.upgraded"),
                ScaledPart(normal, "amount-base", $"card {id}.normal"),
                ScaledPart(upgraded, "amount-base", $"card {id}.upgraded"),
                ScaledPart(normal, "amount-per-count", $"card {id}.normal", fallback: 1),
                ScaledPart(upgraded, "amount-per-count", $"card {id}.upgraded", fallback: 1),
                ParseRetain(normal, $"card {id}.normal"),
                ParseRetain(upgraded, $"card {id}.upgraded"),
                ParseUnpowered(normal, $"card {id}.normal"),
                ParseUnpowered(upgraded, $"card {id}.upgraded"),
                normalConditional,
                upgradedConditional,
                ParseBoolean(normal, "unplayable", $"card {id}.normal"),
                ParseBoolean(upgraded, "unplayable", $"card {id}.upgraded"),
                ParseBoolean(normal, "ethereal", $"card {id}.normal"),
                ParseBoolean(upgraded, "ethereal", $"card {id}.upgraded"),
                ParseVariables(normal, $"card {id}.normal", out var normalGrow, out var normalGrowAmount),
                ParseVariables(upgraded, $"card {id}.upgraded", out var upgradedGrow, out var upgradedGrowAmount),
                normalGrow,
                upgradedGrow,
                normalGrowAmount,
                upgradedGrowAmount,
                normalHooks,
                upgradedHooks,
                Get(body, "source") ?? sourcePath,
                ParseInt(Get(body, "line") ?? blockLine.ToString(), $"card {id}.line"));
        }

        /// <summary>解析变体里的 `hook &lt;name&gt; { steps = [...] }` 块；**逐变体独立**，不做继承。</summary>
        private static AuthorHooks ParseHooks(Pairs variant, string where)
        {
            var onExhaust = new List<AuthorStep>();
            var turnEndInHand = new List<AuthorStep>();
            var onEnterCombat = new List<AuthorStep>();
            var beforeCardPlayed = new List<AuthorStep>();
            var afterCardPlayed = new List<AuthorStep>();
            foreach (var (blockName, block) in variant.Blocks)
            {
                // `variable { ... }` 是数据块（实例变量初值），不是 hook。
                // 已知的嵌套块：实例变量与**条件效果**（来源 `if { condition then else }`）。
                if (blockName is "variable" or "if")
                {
                    continue;
                }

                if (!blockName.StartsWith("hook ", StringComparison.Ordinal))
                {
                    throw new SliceAuthorError("unknown_block", $"{where}.{blockName}",
                        $"未知嵌套块 '{blockName}'（可用：variable / if / hook {string.Join(" / hook ", AuthorSteps.Hooks)}）");
                }

                var hook = blockName["hook ".Length..];
                if (!AuthorSteps.Hooks.Contains(hook, StringComparer.Ordinal))
                {
                    throw new SliceAuthorError("unknown_hook", $"{where}.{blockName}",
                        $"未支持的 hook '{hook}'（可用：{string.Join("/", AuthorSteps.Hooks)}）");
                }

                foreach (var (key, _, line) in block.Items)
                {
                    if (!string.Equals(key, "steps", StringComparison.Ordinal))
                    {
                        throw new SliceAuthorError("unknown_key", $"{where}.{blockName}.{key}:{line}",
                            $"hook 里只支持 'steps'");
                    }
                }

                var hookSteps = ParseSteps(block, $"{where}.{blockName}");
                for (var index = 0; index < hookSteps.Count; index++)
                {
                    if (!AuthorSteps.HookSteps.Contains(hookSteps[index].Kind, StringComparer.Ordinal))
                    {
                        throw new SliceAuthorError("unsupported_capability", $"{where}.{blockName}.steps[{index}]",
                            $"hook 里不能写步骤 '{hookSteps[index].Kind}'：{AuthorSteps.HookStepReason(hookSteps[index].Kind)}" +
                            $"（hook 允许：{string.Join(" / ", AuthorSteps.HookSteps)}）");
                    }
                }

                switch (hook)
                {
                    case AuthorSteps.HookOnExhaust: onExhaust.AddRange(hookSteps); break;
                    case AuthorSteps.HookTurnEndInHand: turnEndInHand.AddRange(hookSteps); break;
                    case AuthorSteps.HookOnEnterCombat: onEnterCombat.AddRange(hookSteps); break;
                    case AuthorSteps.HookBeforeCardPlayed: beforeCardPlayed.AddRange(hookSteps); break;
                    case AuthorSteps.HookAfterCardPlayed: afterCardPlayed.AddRange(hookSteps); break;
                }
            }

            return new AuthorHooks(onExhaust, turnEndInHand, onEnterCombat, beforeCardPlayed, afterCardPlayed);
        }

        private static bool ParseRetainOrFalse(Pairs variant, string where) => ParseRetain(variant, where);

        /// <summary>
        /// `variable { name = &lt;初始值&gt; ... }`：这张牌的**实例变量**（每个实例各有一份），
        /// 可以被 `amount-source = variable:&lt;name&gt;` 读到、被 `grow = &lt;name&gt;` 加一。
        /// </summary>
        private static IReadOnlyDictionary<string, int> ParseVariables(Pairs variant, string where,
            out string growVariable, out int growAmount)
        {
            var variables = new Dictionary<string, int>(StringComparer.Ordinal);
            if (variant.Blocks.TryGetValue("variable", out var block))
            {
                foreach (var (name, value, line) in block.Items)
                {
                    if (!int.TryParse(value, out var initial))
                    {
                        throw new SliceAuthorError("invalid_value", $"{where}.variable.{name}:{line}",
                            $"变量初值必须是整数，得到 '{value}'");
                    }

                    variables[name] = initial;
                }
            }

            growVariable = Get(variant, "grow") ?? string.Empty;
            // `grow-amount`：每次打出后给该变量加多少（默认 1）。来源里 `modifyCardVariable { amount = N }`
            // 就是这条事实（例如 Rampage 普通 +5 / 升级 +9）。
            var rawGrowAmount = Get(variant, "grow-amount");
            if (rawGrowAmount is not null && growVariable.Length == 0)
            {
                throw new SliceAuthorError("missing_prerequisite", $"{where}.grow-amount",
                    "grow-amount 只有在同时声明 grow 时才有意义（它加的是那条变量）");
            }
            growAmount = rawGrowAmount is null ? 1 : ParseRanged(rawGrowAmount, 0, 999, $"{where}.grow-amount");
            if (growVariable.Length > 0 && !variables.ContainsKey(growVariable))
            {
                throw new SliceAuthorError("dangling_reference", $"{where}.grow",
                    $"grow 引用了没有声明的实例变量 '{growVariable}'");
            }

            // 数值来源引用变量时也必须在同一个变体里声明。
            var source = Get(variant, "amount-source") ?? AmountSource.Literal;
            if (source.StartsWith(AmountSource.VariablePrefix, StringComparison.Ordinal))
            {
                var name = source[AmountSource.VariablePrefix.Length..];
                if (!variables.ContainsKey(name))
                {
                    throw new SliceAuthorError("dangling_reference", $"{where}.amount-source",
                        $"引用了没有声明的实例变量 '{name}'");
                }
            }

            // 没有任何来源用到变量时，声明变量属于不可达内容。
            var used = growVariable.Length > 0
                || source.StartsWith(AmountSource.VariablePrefix, StringComparison.Ordinal);
            if (variables.Count > 0 && !used)
            {
                throw new SliceAuthorError("unreachable_variable", $"{where}.variable",
                    "声明了实例变量但没有任何地方读写它（不许留不可达声明）");
            }

            return variables;
        }

        /// <summary>`amount-source = literal | hand-count | discard-count | draw-count | variable:&lt;name&gt;`。</summary>
        private static string ParseAmountSource(Pairs variant, string where)
        {
            var raw = Get(variant, "amount-source");
            if (raw is null) return AmountSource.Literal;
            var isFixed = AmountSource.All.Contains(raw, StringComparer.Ordinal);
            var isVariable = raw.StartsWith(AmountSource.VariablePrefix, StringComparison.Ordinal)
                && raw.Length > AmountSource.VariablePrefix.Length;
            // `hand-count:<Kind>`：按牌类过滤的手牌计数（来源 pileCount 的 type 字段）。
            var filteredHandKind = raw.StartsWith(AmountSource.HandCountPrefix, StringComparison.Ordinal)
                ? raw[AmountSource.HandCountPrefix.Length..]
                : null;
            var filteredPlayedKind = raw.StartsWith(AmountSource.PlayedCountPrefix, StringComparison.Ordinal)
                ? raw[AmountSource.PlayedCountPrefix.Length..]
                : null;
            if ((filteredHandKind ?? filteredPlayedKind) is not null
                && (filteredHandKind ?? filteredPlayedKind) is not ("Attack" or "Skill" or "Power" or "Status"))
            {
                throw new SliceAuthorError("invalid_value", $"{where}.amount-source",
                    $"hand-count:<Kind> 只支持 Attack/Skill/Power/Status，得到 '{filteredHandKind}'");
            }

            if (!isFixed && !isVariable && filteredHandKind is null && filteredPlayedKind is null)
            {
                throw new SliceAuthorError("invalid_value", $"{where}.amount-source",
                    $"只支持 literal / hand-count / hand-count:<Kind> / discard-count / draw-count / exhaust-count" +
                    $" / played-count:<Kind> / block-count / variable:<name>，得到 '{raw}'");
            }

            if (!string.Equals(raw, AmountSource.Literal, StringComparison.Ordinal)
                && Get(variant, "amount") is not null)
            {
                throw new SliceAuthorError("unsupported_combination", $"{where}.amount",
                    $"声明了 amount-source = {raw} 就不能再写固定 amount：两个数值来源会冲突");
            }

            return raw;
        }

        /// <summary>`amount`：字面量来源时必填；状态来源时必须不写。</summary>
        private static int AmountValue(Pairs variant, string where)
        {
            var source = Get(variant, "amount-source");
            if (source is not null && !string.Equals(source, AmountSource.Literal, StringComparison.Ordinal))
            {
                return 0;
            }

            return RequireInt(variant, "amount", where);
        }

        /// <summary>
        /// `damage-cap = &lt;decimal&gt;`：**伤害上限阶段**（来源：`Hook.cs:2539-2555`，在乘法之后、下限之前，
        /// 全程 decimal，取各上限中的最小值）。作用于**承受者**持有该能力时。
        /// </summary>
        private static string ParseDamageCap(string? raw, string where)
        {
            if (raw is null) return string.Empty;
            if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var cap) || cap < 0m)
            {
                throw new SliceAuthorError("invalid_value", $"{where}.damage-cap",
                    $"伤害上限必须是非负小数，得到 '{raw}'");
            }

            if (decimal.Round(cap, 2) != cap)
            {
                throw new SliceAuthorError("invalid_value", $"{where}.damage-cap",
                    $"伤害上限必须能被两位小数精确表示，得到 '{raw}'");
            }

            return cap.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>`damage = none | add-per-stack:&lt;int&gt; | multiply-outgoing|incoming:&lt;decimal&gt;`。</summary>
        private static (string Kind, string Value) ParsePowerDamage(string? raw, string where)
        {
            if (raw is null) return (PowerDamage.None, string.Empty);
            var parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length == 1 && string.Equals(parts[0], PowerDamage.None, StringComparison.Ordinal))
            {
                return (PowerDamage.None, string.Empty);
            }

            if (parts.Length != 2 || !PowerDamage.All.Contains(parts[0], StringComparer.Ordinal))
            {
                throw new SliceAuthorError("invalid_value", $"{where}.damage",
                    $"只支持 none / add-per-stack:<int> / multiply-outgoing:<2位小数> / multiply-incoming:<2位小数>，" +
                    $"得到 '{raw}'");
            }

            var kind = parts[0];
            var value = parts[1];
            if (string.Equals(kind, PowerDamage.AddPerStack, StringComparison.Ordinal))
            {
                if (!int.TryParse(value, out var perStack) || perStack < 0)
                {
                    throw new SliceAuthorError("invalid_value", $"{where}.damage",
                        $"add-per-stack 的值必须是非负整数，得到 '{value}'");
                }

                return (kind, perStack.ToString());
            }

            // 乘数必须能被固定点（两位小数）精确表示，否则会在引擎里悄悄失精度。
            if (!decimal.TryParse(value, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var multiplier)
                || multiplier <= 0m)
            {
                throw new SliceAuthorError("invalid_value", $"{where}.damage",
                    $"{kind} 的值必须是正小数，得到 '{value}'");
            }

            if (decimal.Round(multiplier, 2) != multiplier)
            {
                throw new SliceAuthorError("invalid_value", $"{where}.damage",
                    $"{kind} 的乘数必须能被两位小数精确表示，得到 '{value}'");
            }

            return (kind, multiplier.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>`owner-turn-end = none | tick | convert-to &lt;power&gt;`。</summary>
        /// <summary>
        /// `trigger = &lt;时点&gt;`：能力的**触发点**。当前只支持切片真的会触发的时点；
        /// 未实现的时点必须显式拒绝（不允许"声明了但永远不触发"）。
        /// </summary>
        /// <summary>
        /// `if { condition = &lt;谓词&gt;  then = [步骤…]  else = [步骤…] }`：**条件效果**（来源 `if` 块）。
        /// 与 `steps` 二选一：一张牌要么是一条固定步骤表，要么是一个条件效果（不猜、不合并）。
        /// </summary>
        private static AuthorConditional? ParseConditional(Pairs variant, string where)
        {
            if (!variant.Blocks.TryGetValue("if", out var block))
            {
                return null;
            }

            var condition = Require(block, "condition", $"{where}.if");
            if (!AuthorCondition.All.Contains(condition, StringComparer.Ordinal))
            {
                throw new SliceAuthorError("invalid_value", $"{where}.if.condition",
                    $"只支持 {string.Join(" / ", AuthorCondition.All)}，得到 '{condition}'");
            }

            var then = ParseStepList(Require(block, "then", $"{where}.if"), $"{where}.if.then");
            var otherwise = ParseStepList(Require(block, "else", $"{where}.if"), $"{where}.if.else");
            return new AuthorConditional(condition, then, otherwise);
        }

        private static string ParsePowerTrigger(string? raw, string where)
        {
            if (raw is null) return string.Empty;
            if (string.Equals(raw, AuthorPowerTrigger.AfterCardExhausted, StringComparison.Ordinal))
            {
                return AuthorPowerTrigger.AfterCardExhausted;
            }

            if (string.Equals(raw, AuthorPowerTrigger.AfterCardPlayed, StringComparison.Ordinal))
            {
                return AuthorPowerTrigger.AfterCardPlayed;
            }

            throw new SliceAuthorError("invalid_value", $"{where}.trigger",
                $"只支持 {AuthorPowerTrigger.AfterCardExhausted} / {AuthorPowerTrigger.AfterCardPlayed}" +
                $"（切片真的会触发的时点），得到 '{raw}'");
        }

        /// <summary>`when-card-type = Attack|Skill|Power|Status`：只有这一类牌触发（来源 `whenCardType`）。</summary>
        private static string ParseWhenCardType(string? raw, string trigger, string where)
        {
            if (raw is null) return string.Empty;
            if (trigger.Length == 0)
            {
                throw new SliceAuthorError("missing_prerequisite", $"{where}.when-card-type",
                    "when-card-type 只有在同时声明 trigger 时才有意义");
            }

            if (raw is not ("Attack" or "Skill" or "Power" or "Status"))
            {
                throw new SliceAuthorError("invalid_value", $"{where}.when-card-type",
                    $"只能是 Attack/Skill/Power/Status，得到 '{raw}'");
            }

            return raw;
        }

        /// <summary>`trigger-effect = gain-block-per-stack:&lt;n&gt;`：触发时给能力持有者多少格挡（按层数）。</summary>
        private static string ParseTriggerEffect(string? raw, string trigger, string where)
        {
            if (raw is null) return string.Empty;
            if (trigger.Length == 0)
            {
                throw new SliceAuthorError("missing_prerequisite", $"{where}.trigger-effect",
                    "trigger-effect 只有在同时声明 trigger 时才有意义");
            }

            if (!raw.StartsWith(PowerTriggerEffect.GainBlockPerStackPrefix, StringComparison.Ordinal))
            {
                throw new SliceAuthorError("invalid_value", $"{where}.trigger-effect",
                    $"只支持 gain-block-per-stack:<n>，得到 '{raw}'");
            }

            var amount = raw[PowerTriggerEffect.GainBlockPerStackPrefix.Length..];
            ParseRanged(amount, 0, 999, $"{where}.trigger-effect");
            return raw;
        }

        /// <summary>
        /// `amount-base` / `amount-per-count`：来源 `scaled { base = b  perCount = p  count = { … } }`
        /// 里的 b 与 p（数值 = base + perCount × 来源计数）。默认 base = 0、perCount = 1（即原来的"数值就是来源"）。
        /// </summary>
        private static int ScaledPart(Pairs variant, string key, string where, int fallback = 0) =>
            Get(variant, key) is { } raw ? ParseRanged(raw, 0, 999, $"{where}.{key}") : fallback;

        private static string ParsePowerTurnEnd(string? raw, string where)
        {
            if (raw is null) return PowerTurnEnd.None;
            if (string.Equals(raw, PowerTurnEnd.None, StringComparison.Ordinal)) return PowerTurnEnd.None;
            if (string.Equals(raw, PowerTurnEnd.Tick, StringComparison.Ordinal)) return PowerTurnEnd.Tick;

            if (string.Equals(raw, PowerTurnEnd.ConvertToStrength, StringComparison.Ordinal))
            {
                return PowerTurnEnd.ConvertToStrength;
            }

            if (string.Equals(raw, PowerTurnEnd.Remove, StringComparison.Ordinal))
            {
                return PowerTurnEnd.Remove;
            }

            if (string.Equals(raw, PowerTurnEnd.RemoveAtOpponentTurnEnd, StringComparison.Ordinal))
            {
                return PowerTurnEnd.RemoveAtOpponentTurnEnd;
            }

            throw new SliceAuthorError("invalid_value", $"{where}.owner-turn-end",
                $"只支持 none / tick / convert-to-strength / remove，得到 '{raw}'");
        }

        /// <summary>
        /// 变体费用：变体块里的 `cost` 覆盖卡牌级 `cost`（来源里有些牌升级后更便宜，例如 Entrench 2 → 1）；
        /// 没写就用卡牌级的值。
        /// </summary>
        private static int VariantCost(Pairs variant, int cardCost, string where) =>
            Get(variant, "cost") is { } raw ? ParseRanged(raw, 0, 9, $"{where}.cost") : cardCost;

        /// <summary>`unplayable = true|false`：这张牌不能被主动打出（状态牌的关键词之一）。</summary>
        private static bool ParseBoolean(Pairs variant, string key, string where)
        {
            var raw = Get(variant, key);
            return raw switch
            {
                null => false,
                "true" => true,
                "false" => false,
                _ => throw new SliceAuthorError("invalid_value", $"{where}.{key}",
                    $"{key} 只能是 true 或 false，得到 '{raw}'"),
            };
        }

        /// <summary>
        /// `properties = [unpowered]`：这次攻击**不受**出手方修正（Strength/Weak）与受击方易伤影响。
        /// 来源：`IsPoweredAttack()` = 有 Move 且非 Unpowered。空列表＝正常（powered）。
        /// </summary>
        private static bool ParseUnpowered(Pairs pairs, string where)
        {
            var raw = Get(pairs, "properties");
            if (raw is null) return false;
            if (!raw.StartsWith('[') || !raw.EndsWith(']'))
            {
                throw new SliceAuthorError("syntax", $"{where}.properties", $"属性表必须是 [a, b] 形式，得到 '{raw}'");
            }

            var items = raw[1..^1]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            foreach (var item in items)
            {
                if (!string.Equals(item, "unpowered", StringComparison.Ordinal))
                {
                    throw new SliceAuthorError("invalid_value", $"{where}.properties",
                        $"未知属性 '{item}'（当前只支持 unpowered）");
                }
            }

            return items.Count > 0;
        }

        /// <summary>`retain = true|false`：回合结束时这张牌**留在手牌**而不是进弃牌堆（逐变体）。</summary>
        private static bool ParseRetain(Pairs variant, string where)
        {
            var raw = Get(variant, "retain");
            if (raw is null) return false;
            return raw switch
            {
                "true" => true,
                "false" => false,
                _ => throw new SliceAuthorError("invalid_value", $"{where}.retain",
                    $"retain 只能是 true 或 false，得到 '{raw}'"),
            };
        }

        /// <summary>`cycle = [a, b xforever]`：前面的行动各一次，**最后一个行动永远重复**。</summary>
        private static IReadOnlyList<string> ParseCycle(string? raw, IReadOnlyList<AuthorMove> moves, string where)
        {
            if (raw is null)
            {
                if (moves.Count > 0)
                {
                    throw new SliceAuthorError("missing_key", $"{where}.cycle",
                        "声明了行动却没有 cycle：行动程序必须有明确的循环");
                }

                return [];
            }

            if (!raw.StartsWith('[') || !raw.EndsWith(']'))
            {
                throw new SliceAuthorError("syntax", $"{where}.cycle", $"循环必须是 [a, b] 形式，得到 '{raw}'");
            }

            var entries = raw[1..^1]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            if (entries.Count == 0)
            {
                throw new SliceAuthorError("invalid_value", $"{where}.cycle", "循环不能为空");
            }

            var cycle = new List<string>();
            for (var index = 0; index < entries.Count; index++)
            {
                var parts = entries[index].Split(' ',
                    StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var name = parts[0];
                if (!moves.Any(move => string.Equals(move.Name, name, StringComparison.Ordinal)))
                {
                    throw new SliceAuthorError("dangling_reference", $"{where}.cycle[{index}]",
                        $"循环引用了没有声明的行动 '{name}'");
                }

                var repeats = parts.Length >= 2 && string.Equals(parts[1], "xforever", StringComparison.Ordinal);
                if (parts.Length >= 2 && !repeats)
                {
                    throw new SliceAuthorError("invalid_value", $"{where}.cycle[{index}]",
                        $"'{entries[index]}' 的重复写法只能是 'xforever'");
                }

                if (repeats && index != entries.Count - 1)
                {
                    throw new SliceAuthorError("unsupported_combination", $"{where}.cycle[{index}]",
                        "只有**最后一个**行动可以标 xforever：运行时是「先各一次，最后一个永远重复」");
                }

                cycle.Add(name);
            }

            foreach (var move in moves)
            {
                if (!cycle.Contains(move.Name, StringComparer.Ordinal))
                {
                    throw new SliceAuthorError("unreachable_move", $"{where}.move {move.Name}",
                        "该行动不在循环里：永远不会被执行（不许留不可达声明）");
                }
            }

            return cycle;
        }

        /// <summary>`move name { ritual = first-ritual  damage = strike }` 块。</summary>
        private static IReadOnlyList<AuthorMove> ParseMoves(Pairs enemy, string where)
        {
            var moves = new List<AuthorMove>();
            foreach (var (blockName, block) in enemy.Blocks)
            {
                if (!blockName.StartsWith("move ", StringComparison.Ordinal))
                {
                    throw new SliceAuthorError("unknown_block", $"{where}.{blockName}",
                        $"未知嵌套块 '{blockName}'（可用：move &lt;name&gt;）");
                }

                var name = blockName["move ".Length..];
                var steps = new List<AuthorMoveStep>();
                var unpowered = ParseUnpowered(block, $"move {name}");
                foreach (var (kind, value, line) in block.Items)
                {
                    // `properties = [...]` 是这次攻击的属性声明，不是动作。
                    if (string.Equals(kind, "properties", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!MoveStepKinds.Contains(kind, StringComparer.Ordinal))
                    {
                        throw new SliceAuthorError("unknown_key", $"{where}.move {name}.{kind}:{line}",
                            $"未知动作 '{kind}'（可用：{string.Join("/", MoveStepKinds)}）");
                    }

                    steps.Add(new AuthorMoveStep(kind, value));
                }

                if (steps.Count == 0)
                {
                    throw new SliceAuthorError("missing_key", $"{where}.move {name}",
                        "行动必须至少有一个动作");
                }

                moves.Add(new AuthorMove(name, steps, unpowered));
            }

            if (moves.Select(move => move.Name).Distinct(StringComparer.Ordinal).Count() != moves.Count)
            {
                throw new SliceAuthorError("duplicate_identity", $"{where}.moves", "行动名不得重复");
            }

            return moves;
        }

        /// <summary>
        /// 解析 `[a, b x3]`：逗号分隔；步骤名后可跟 `xN` 表示**批次**（与卡组 `x5` 同一写法）。
        /// 批次只允许出现在可重复的效果步骤上：`target`（选 N 次目标语义不明）、`damage_all`（已经是全体）、
        /// `damage_random`（抽 N 次语义不明）、`exhaust_self`（消耗一次即可）上都拒绝。
        /// </summary>
        private static IReadOnlyList<AuthorStep> ParseSteps(Pairs variant, string where) =>
            ParseStepList(Require(variant, "steps", where), $"{where}.steps");

        /// <summary>
        /// 解析一个 `[a, b x3]` 形式的**步骤表**：`steps` 键与条件效果的 `then`/`else` 共用这一套词表与校验。
        /// </summary>
        private static IReadOnlyList<AuthorStep> ParseStepList(string raw, string where)
        {
            if (!raw.StartsWith('[') || !raw.EndsWith(']'))
            {
                throw new SliceAuthorError("syntax", where, $"步骤表必须是 [a, b] 形式，得到 '{raw}'");
            }

            var tokens = raw[1..^1]
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            if (tokens.Count == 0)
            {
                // `steps = []` 是**显式的空效果**（状态牌就是没有效果）：允许。
                // 缺整个 `steps` 键仍然拒绝（`Require` 报 missing_key）——"没写"不等于"写了空"。
                return [];
            }

            var steps = new List<AuthorStep>();
            for (var index = 0; index < tokens.Count; index++)
            {
                var parts = tokens[index].Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                var kind = parts[0];
                // 带参数的步骤：`generate:<card-id>` / `becomes:<card-id>`（参数写在冒号后，仍是单个词）。
                // `apply_power:<power-id>:<n>` / `apply_power_self:<power-id>:<n>` / `apply_power_all:<power-id>:<n>`：
                // 一步之内同时给出**能力 id**与**层数**（来源里同一张牌可以给不同收受者上**不同**能力，
                // 例如 Dominate：给目标 Vulnerable、给自己 Strength）。id 参与命名空间限定。
                var applyPrefix = new[] { AuthorSteps.ApplyPower, AuthorSteps.ApplyPowerAll, AuthorSteps.ApplyPowerSelf }
                    .FirstOrDefault(candidate => kind.StartsWith(candidate + ":", StringComparison.Ordinal)
                        && kind.LastIndexOf(':') > candidate.Length);
                if (applyPrefix is not null)
                {
                    var lastColon = kind.LastIndexOf(':');
                    var powerId = kind[(applyPrefix.Length + 1)..lastColon];
                    var stacksText = kind[(lastColon + 1)..];
                    if (powerId.Length == 0)
                    {
                        throw new SliceAuthorError("invalid_value", $"{where}.steps[{index}]",
                            $"'{applyPrefix}:<power-id>:<层数>' 缺少能力 id");
                    }

                    var stacks = ParseRanged(stacksText, 0, 999, $"{where}.steps[{index}]");
                    steps.Add(new AuthorStep($"{applyPrefix}:{powerId}:{stacks}", 1));
                    continue;
                }
                // 带**字面量数值**的参数化步骤：`lose_hp:<n>`、`draw:<n>`（同一张牌上不同步骤要用不同数值，
                // 例如 Breakthrough = 对全体 9 点 + 自损 1 点；PommelStrike = 伤害 9 + 抽 1 张，
                // 升级后伤害 10 + 抽 **2** 张——抽牌数与伤害数是两个独立事实）。参数不参与命名空间限定。
                var literalStep = AuthorSteps.LiteralParameterized.FirstOrDefault(prefix =>
                    kind.StartsWith(prefix, StringComparison.Ordinal));
                if (literalStep is not null)
                {
                    var literal = kind[literalStep.Length..];
                    var allowsNegative = literalStep == AuthorSteps.ModifyCost + ":";
                    if (!int.TryParse(literal, out var literalAmount)
                        || (allowsNegative ? literalAmount is < -9 or > 9 : literalAmount is < 0 or > 999))
                    {
                        throw new SliceAuthorError("invalid_value", $"{where}.steps[{index}]",
                            $"'{literalStep}<数值>' 需要 0..999 的整数字面量，得到 '{literal}'");
                    }

                    steps.Add(new AuthorStep($"{literalStep}{literalAmount}", 1));
                    continue;
                }

                var parameterized = AuthorSteps.Parameterized.FirstOrDefault(prefix =>
                    kind.StartsWith(prefix, StringComparison.Ordinal));
                if (parameterized is not null)
                {
                    if (kind.Length == parameterized.Length)
                    {
                        throw new SliceAuthorError("invalid_value", $"{where}.steps[{index}]",
                            $"'{parameterized}<card-id>' 缺少卡牌 id");
                    }

                    steps.Add(new AuthorStep(kind, 1));
                    continue;
                }

                if (!AuthorSteps.All.Contains(kind, StringComparer.Ordinal))
                {
                    throw new SliceAuthorError("unknown_step", $"{where}.steps[{index}]",
                        $"未支持的步骤 '{kind}'（可用：{string.Join("/", AuthorSteps.All)}）");
                }

                var count = 1;
                if (parts.Length == 2)
                {
                    var suffix = parts[1];
                    if (!suffix.StartsWith('x') || !int.TryParse(suffix[1..], out count) || count < 1)
                    {
                        throw new SliceAuthorError("invalid_value", $"{where}.steps[{index}]",
                            $"批次写法是 '{kind} xN'（N ≥ 1），得到 '{tokens[index]}'");
                    }
                }
                else if (parts.Length > 2)
                {
                    throw new SliceAuthorError("invalid_value", $"{where}.steps[{index}]",
                        $"步骤写法只能是 '名字' 或 '名字 xN'，得到 '{tokens[index]}'");
                }

                if (count > 1 && AuthorSteps.NonRepeatable.Contains(kind, StringComparer.Ordinal))
                {
                    throw new SliceAuthorError("unsupported_combination", $"{where}.steps[{index}]",
                        $"'{kind}' 不允许批次：{AuthorSteps.NonRepeatableReason(kind)}");
                }

                steps.Add(new AuthorStep(kind, count));
            }

            var kinds = steps.Select(step => step.Kind).ToList();
            if (kinds.Contains(AuthorSteps.Damage, StringComparer.Ordinal)
                && kinds.Contains(AuthorSteps.DamageAll, StringComparer.Ordinal))
            {
                throw new SliceAuthorError("unsupported_combination", $"{where}.steps",
                    "damage 与 damage_all 同时出现：目标域不明确，必须二选一");
            }

            // 施加能力的目标域也只能有一种：给单个目标、或给全体（不能同时声明两者，否则同一能力落在谁身上不明确）。
            if (kinds.Contains(AuthorSteps.ApplyPower, StringComparer.Ordinal)
                && kinds.Contains(AuthorSteps.ApplyPowerAll, StringComparer.Ordinal))
            {
                throw new SliceAuthorError("unsupported_combination", $"{where}.steps",
                    "apply_power 与 apply_power_all 同时出现：施加能力的目标域不明确，必须二选一");
            }

            // 目标的**确定方式**只能有一种：显式选择、全体各一次、或按随机流抽一个。
            var targetingModes = new[] { AuthorSteps.Target, AuthorSteps.DamageAll, AuthorSteps.DamageRandom,
                AuthorSteps.DamageLowest }
                .Count(step => kinds.Contains(step, StringComparer.Ordinal));
            if (targetingModes > 1)
            {
                throw new SliceAuthorError("unsupported_combination", $"{where}.steps",
                    "target / damage_all / damage_random 只能出现一种：目标的确定方式必须唯一");
            }

            return steps;
        }

        /// <summary>读 `{ k = v ... }`；`name { ... }` 形式的嵌套块收进 <see cref="Pairs.Blocks"/>。</summary>
        private Pairs ReadPairs(string where)
        {
            var items = new List<(string Key, string Value, int Line)>();
            var blocks = new Dictionary<string, Pairs>(StringComparer.Ordinal);
            Expect("{", where);
            while (Peek()?.Text != "}")
            {
                var key = Next();
                var name = key.Text;
                // 两词块头（`hook on_exhaust {`）：下一个词既不是 `{` 也不是 `=` 时并入块名。
                if (Peek()?.Text is { } following
                    && following != "{" && following != "=" && following != "[")
                {
                    name = name + " " + Next().Text;
                }

                if (Peek()?.Text == "{")
                {
                    blocks[name] = ReadPairs($"{where}.{name}");
                    continue;
                }

                Expect("=", $"{where}.{name}");
                var value = ReadValue($"{where}.{name}");
                if (items.Any(item => string.Equals(item.Key, name, StringComparison.Ordinal)))
                {
                    throw new SliceAuthorError("duplicate_key", $"{where}.{name}:{key.Line}",
                        $"键 '{name}' 重复声明");
                }

                items.Add((name, value, key.Line));
            }

            Expect("}", where);
            return new Pairs(items, blocks);
        }

        private string ReadValue(string where)
        {
            var AuthorToken = Next();
            if (AuthorToken.Text != "[") return AuthorToken.Text;

            // 逗号**分隔项**，项内多个词用空格连起来（`damage x3` 是一个项，不是两个）。
            var items = new List<string>();
            var current = new List<string>();
            while (Peek()?.Text != "]")
            {
                var item = Next();
                if (item.Text == ",")
                {
                    if (current.Count > 0) items.Add(string.Join(" ", current));
                    current.Clear();
                    continue;
                }

                current.Add(item.Text);
            }

            if (current.Count > 0) items.Add(string.Join(" ", current));
            Expect("]", where);
            return "[" + string.Join(",", items) + "]";
        }

        private AuthorToken? Peek() => _position < tokens.Count ? tokens[_position] : null;

        private AuthorToken Next() => _position < tokens.Count
            ? tokens[_position++]
            : throw new SliceAuthorError("syntax", $"{sourcePath}", "文本意外结束");

        private void Expect(string text, string where)
        {
            var AuthorToken = Next();
            if (AuthorToken.Text != text)
            {
                throw new SliceAuthorError("syntax", $"{sourcePath}:{AuthorToken.Line}",
                    $"{where} 处期望 '{text}'，得到 '{AuthorToken.Text}'");
            }
        }

        private void ExpectOptionalWord()
        {
            if (Peek()?.Text is { } text && text != "{") _position++;
        }

        private static string Require(Pairs pairs, string key, string where) =>
            Get(pairs, key) ?? throw new SliceAuthorError("missing_key", $"{where}.{key}",
                $"缺少必需键 '{key}'");

        private static string? Get(Pairs pairs, string key) =>
            pairs.Items.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal)).Value is { Length: > 0 } value
                ? value
                : null;

        private static int RequireInt(Pairs pairs, string key, string where) =>
            ParseInt(Require(pairs, key, where), $"{where}.{key}");

        /// <summary>带范围检查的整数解析（越界**不予截断**，直接拒绝）。</summary>
        private static int ParseRanged(string text, int min, int max, string where)
        {
            var value = ParseInt(text, where);
            if (value < min || value > max)
            {
                throw new SliceAuthorError("invalid_value", where, $"数值必须在 {min}..{max}，得到 {value}");
            }

            return value;
        }

        private static int ParseInt(string text, string where) =>
            int.TryParse(text, out var value)
                ? value
                : throw new SliceAuthorError("invalid_value", where, $"'{text}' 不是整数");

        private static SliceAuthorError UnknownKey(string where, string key, int line) =>
            new("unknown_key", $"{where}.{key}:{line}", $"未知键 '{key}'（不被静默忽略：先补语法再声明）");
    }

    // -- 校验 -----------------------------------------------------------------------------------------------

    private static void Validate(SliceAuthorModel model, string sourcePath)
    {
        if (model.Cards.Count == 0)
        {
            throw new SliceAuthorError("missing_key", "cards", "至少要声明一张卡");
        }

        if (model.Cards.Select(card => card.Id).Distinct(StringComparer.Ordinal).Count() != model.Cards.Count)
        {
            throw new SliceAuthorError("duplicate_identity", "cards", "卡牌 ID 不得重复");
        }

        if (model.Enemies.Select(enemy => enemy.Id).Distinct(StringComparer.Ordinal).Count() != model.Enemies.Count)
        {
            throw new SliceAuthorError("duplicate_identity", "enemies", "敌人 ID 不得重复");
        }

        foreach (var entry in model.Deck)
        {
            if (!model.Cards.Any(card => string.Equals(card.Id, entry.CardId, StringComparison.Ordinal)))
            {
                throw new SliceAuthorError("dangling_reference", $"deck[{entry.CardId}]", "卡组引用了没有声明的卡");
            }
        }

        // 敌人的 `ritual` 行动本身只会上层数；"回合末转成力量"是那个能力的**声明**行为。
        // 因此用了 ritual 行动却没有声明这条行为时，行动会静默地什么也不产生 —— 这里显式拒绝。
        var usesRitualMove = model.Enemies
            .SelectMany(enemy => enemy.Moves)
            .SelectMany(move => move.Steps)
            .Any(step => string.Equals(step.Kind, "ritual", StringComparison.Ordinal));
        if (usesRitualMove && !model.Powers.Any(power =>
                string.Equals(power.OwnerTurnEnd, PowerTurnEnd.ConvertToStrength, StringComparison.Ordinal)))
        {
            throw new SliceAuthorError("missing_key", "powers",
                "敌人行动里用了 ritual，但没有任何能力声明 owner-turn-end = convert-to-strength：" +
                "那样 Ritual 永远不会转成力量");
        }

        foreach (var entry in model.Belt)
        {
            if (!model.Potions.Any(potion => string.Equals(potion.Id, entry.PotionId, StringComparison.Ordinal)))
            {
                throw new SliceAuthorError("dangling_reference", $"potions[{entry.PotionId}]",
                    "药水栏引用了没有声明的药水");
            }
        }

        // 声明了药水就必须放进药水栏：否则它永远不会被用到（不许留不可达声明）。
        foreach (var potion in model.Potions)
        {
            if (!model.Belt.Any(entry => string.Equals(entry.PotionId, potion.Id, StringComparison.Ordinal)))
            {
                throw new SliceAuthorError("unreachable_potion", $"potion {potion.Id}",
                    "声明了药水但药水栏里没有它：这瓶药水永远不会被用到");
            }
        }

        // `apply_power_self_from_target:<power-id>` 引用的能力必须真的被声明（否则运行时读不到层数）。
        foreach (var card in model.Cards)
        {
            foreach (var step in card.NormalSteps.Concat(card.UpgradedSteps)
                         .Where(step => step.Kind.StartsWith(
                             AuthorSteps.ApplyPowerSelfFromTarget + ":", StringComparison.Ordinal)))
            {
                var referenced = step.Kind[(AuthorSteps.ApplyPowerSelfFromTarget.Length + 1)..];
                if (!model.Powers.Any(power => string.Equals(power.Id, referenced, StringComparison.Ordinal)))
                {
                    throw new SliceAuthorError("dangling_reference", $"card {card.Id}.steps",
                        $"apply_power_self_from_target 引用了没有声明的能力 '{referenced}'");
                }
            }
        }
        foreach (var enemy in model.Enemies)
        {
            // 行动程序是**必需**事实：运行时按声明的循环执行，缺了它这个敌人就无法行动
            // （这里显式拒绝，而不是让运行时悄悄用某个内建默认值）。
            if (enemy.Moves.Count == 0 || enemy.Cycle.Count == 0)
            {
                throw new SliceAuthorError("missing_key", $"enemy {enemy.Id}.cycle",
                    "每个敌人都必须声明行动（move …）与循环（cycle = […]）");
            }
        }

        foreach (var card in model.Cards)
        {
            if (card.PowerId.Length > 0
                && !model.Powers.Any(power => string.Equals(power.Id, card.PowerId, StringComparison.Ordinal)))
            {
                throw new SliceAuthorError("dangling_reference", $"card {card.Id}.power",
                    $"'{card.PowerId}' 没有在 powers 里声明");
            }
        }

        _ = sourcePath;
    }

    // -- 降级到程序化包消费的记录 ---------------------------------------------------------------------------

    public static SliceCard ToSliceCard(AuthorCard card) => new(
        card.Id, card.Name, card.Kind, card.NormalCost, card.NormalAmount, card.UpgradedAmount, card.PowerId,
        card.NormalStacks, card.UpgradedStacks, card.SourcePath, card.SourceLine)
    {
        // 升级形态的费用：与普通不同时显式带上（来源 Entrench 2 → 1）。
        UpgradedCost = card.UpgradedCost == card.NormalCost ? null : card.UpgradedCost,
    };

    public static SlicePower ToSlicePower(AuthorPower power) => new(
        power.Id, power.Name, power.Role, power.Effect, power.SourcePath, power.SourceLine)
    {
        Trigger = power.Trigger,
        TurnEndSide = power.TurnEndSide,
        WhenCardType = power.WhenCardType,
        TriggerEffect = power.TriggerEffect,
        PreventEnergyGain = power.PreventEnergyGain,
        PreventsBlockClear = power.PreventsBlockClear,
        CardRuleType = power.CardRuleType,
        CardRuleCost = power.CardRuleCost,
        CardRuleResultPile = power.CardRuleResultPile,
        CardRuleConsumeOnPlay = power.CardRuleConsumeOnPlay,
    };

    public static SliceEnemy ToSliceEnemy(AuthorEnemy enemy) => new(
        enemy.Id, enemy.Name, enemy.NormalMinHp, enemy.NormalMaxHp, enemy.ToughMinHp, enemy.ToughMaxHp,
        enemy.FirstMoveRitual, enemy.StrikeDamageNormal, enemy.StrikeDamageDeadly,
        enemy.FirstMove,
        enemy.RepeatMove,
        enemy.SourcePath, enemy.SourceLine)
    {
        StartingBlock = enemy.StartingBlock,
        StartingPowers = enemy.StartingPowers,
        RepeatFrom = enemy.RepeatFrom,
        HasAscensionVariants = enemy.HasAscensionVariants,
        Behavior = enemy.Behavior,
    };

    public static IReadOnlyList<string> ToStarterDeck(SliceAuthorModel model) =>
        [.. model.Deck.SelectMany(entry => Enumerable.Repeat(entry.CardId, entry.Count))];

    private static string Lower(string text) =>
        text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];
}
