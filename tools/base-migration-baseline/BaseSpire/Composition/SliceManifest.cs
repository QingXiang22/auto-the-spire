using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>One card definition of the controlled combat slice, with the source it was frozen from.</summary>
public sealed record SliceCard(
    string ContentId,
    string Name,
    string Kind,
    int Cost,
    int NormalAmount,
    int UpgradedAmount,
    string AppliedPowerId,
    int NormalPowerStacks,
    int UpgradedPowerStacks,
    string SourcePath,
    int SourceLine)
{
    /// <summary>
    /// 升级形态的费用（**可空**）：来源里有些牌升级后更便宜（例如 Entrench 2 → 1）。
    /// 为空表示与 <see cref="Cost"/> 相同——切片那 4 张冻结卡就是这样，因此冻结数据一字未改。
    /// </summary>
    public int? UpgradedCost { get; init; }

    /// <summary>升级形态的**有效**费用（没声明就用普通费用）。</summary>
    public int EffectiveUpgradedCost => UpgradedCost ?? Cost;
}

/// <summary>One power of the slice, with the source it was frozen from.</summary>
public sealed record SlicePower(
    string ContentId,
    string Name,
    string Role,
    string Effect,
    string SourcePath,
    int SourceLine)
{
    /// <summary>能力的**触发点**（声明式触发；空＝没有触发点）。来源 `ironclad-powers.spire` 的 `trigger`。</summary>
    public string Trigger { get; init; } = string.Empty;

    /// <summary>
    /// `trigger` 是回合末衰减时的**哪一侧**回合末："enemy" / "player" / 空（来源
    /// `AfterSideTurnEnd(side == Enemy)` 等）。空 = 没有声明回合末触发点。
    /// </summary>
    public string TurnEndSide { get; init; } = string.Empty;

    /// <summary>只有这一类牌触发（空＝不限）。来源的 `whenCardType`。</summary>
    public string WhenCardType { get; init; } = string.Empty;

    /// <summary>触发效果（来源的 `effect = gainBlock(1, perPowerStack=…)`）。</summary>
    public string TriggerEffect { get; init; } = string.Empty;

    /// <summary>持有该能力时**不能获得能量**（来源 `preventEnergyGain = true`，例如 NoEnergyGainPower）。</summary>
    public bool PreventEnergyGain { get; init; }

    /// <summary>
    /// W-06：拥有者回合开始时格挡**不清零**（来源 `ironclad-powers.spire` 的 BarricadePower
    /// `preventsBlockClear = true`）。与 `PreventEnergyGain` 一样是**声明驱动**的：只有声明的能力
    /// 会进运行时的判定链，没声明的能力不会凭空生效。
    /// </summary>
    public bool PreventsBlockClear { get; init; }

    /// <summary>W-06：**牌规则**——持有该能力时把这一**类**牌的费用改写成 `CardRuleCost`（-1＝不改）。</summary>
    public string CardRuleType { get; init; } = string.Empty;

    public int CardRuleCost { get; init; } = -1;

    /// <summary>`exhaust` 表示这类牌打出后进消耗区而不是弃牌堆（空＝按牌自己的行为）。</summary>
    public string CardRuleResultPile { get; init; } = string.Empty;

    /// <summary>打出这类牌时把这个能力**消耗掉**（一次性牌规则，例如 FreeAttackPower）。</summary>
    public bool CardRuleConsumeOnPlay { get; init; }
}

/// <summary>One enemy of the slice, with the source it was frozen from.</summary>
public sealed record SliceEnemy(
    string ContentId,
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
    string SourcePath,
    int SourceLine)
{
    /// <summary>
    /// 出生时挂上的能力 `[(能力 id, 层数)]`（来源 `startingPowers = [power(Slow, 1), …]`）：
    /// 出生期施加点与战斗中的施加走同一条路径（`PowerCmd.Apply`）。
    /// </summary>
    public IReadOnlyList<(string PowerId, int Stacks)> StartingPowers { get; init; } = [];

    /// <summary>
    /// 每层给**拥有者自己**的 powered 获得格挡**加**多少点（`DexterityPower.ModifyBlockAdditive`）。
    /// 加性阶段在乘性阶段之前，最后只截断一次。0 = 无。
    /// </summary>
    public int BlockGainAdditivePerStack { get; init; }
    /// <summary>
    /// Spawn health used by the slice: the normal range minimum, so the scenario is deterministic. The vanilla
    /// in-range roll needs the recovered RNG call order and is not implemented.
    /// </summary>
    public int SliceHp => NormalMinHp;

    /// <summary>
    /// 出生格挡（来源 `enemy … startingBlock = N`）。0 = 无。
    /// 原版获得格挡还要过 Dexterity/Frail 等修正阶段，本包当前按**未修正**加。
    /// </summary>
    public int StartingBlock { get; init; }

    /// <summary>
    /// 该敌人的行动序列**循环起点**（`behavior` 的降级结果）；-1 = "最后一个行动永远重复"。
    /// 运行时只用于意图推导，行动本身已由 IR 展开。
    /// </summary>
    public int RepeatFrom { get; init; } = -1;

    /// <summary>声明里有 `variants(default=…, deadly=…)`：降级取 default（进阶 0），进阶 &gt; 0 必须建局前拒绝。</summary>
    public bool HasAscensionVariants { get; init; }

    /// <summary>
    /// 正式 `.spire` 源的**行动状态机**（非空时运行时按状态机选取行动；此时 <see cref="RepeatFrom"/> 不参与）。
    /// </summary>
    public AuthorEnemyBehavior? Behavior { get; init; }
}

/// <summary>
/// Frozen source manifest of <c>base-spire:combat-slice-v1</c>.
///
/// Every number the slice package uses comes from this file, so a value can never be changed in one place and
/// silently diverge in another, and every value carries the file and line it was read from. Values whose exact
/// vanilla rule is not yet recovered are marked <see cref="TestScenario"/> and must not be reported as vanilla
/// behaviour.
/// </summary>
public static class SliceManifest
{
    public const string PackageId = "base-spire";

    public const string Version = "combat-slice-v1";

    /// <summary>Participant role of the single player in the slice. Participants are numbered in role order.</summary>
    public const string PlayerRole = "player";

    public static readonly ParticipantId Player = new(1);

    public const string StrikeId = "base-spire:strike-ironclad";

    public const string DefendId = "base-spire:defend-ironclad";

    public const string BashId = "base-spire:bash";

    public const string HeadbuttId = "base-spire:headbutt";

    public const string StrengthId = "base-spire:strength";

    public const string VulnerableId = "base-spire:vulnerable";

    public const string WeakId = "base-spire:weak";

    public const string DampCultistId = "base-spire:damp-cultist";

    public const string CalcifiedCultistId = "base-spire:calcified-cultist";

    public const string RitualId = "base-spire:ritual";

    /// <summary>Named random stream the package uses to reshuffle the discard pile into the draw pile.</summary>
    public const string DrawStream = "base-spire:combat-draw";

    /// <summary>
    /// Named random stream the package uses to sample a **random target** (`SamplePick`).
    ///
    /// 消费点有来源：每次 `damage_random` 恰好消费这条流**一次**（`RandomService.DrawCount` 可核对），
    /// 且候选顺序不是插入顺序 —— 引擎的 `DrawPick` 在采样前按公开语义键排序候选
    /// （RandomService.cs:125），因此同一批候选即使集合成员顺序不同，选中的也是同一个实例。
    /// 抽牌堆的重洗用另一条流（<see cref="DrawStream" />），两者互不影响。
    /// </summary>
    public const string RandomTargetStream = "base-spire:combat-random-target";

    /// <summary>
    /// W-06：**随机抽一张手牌**用的命名流（`randomMoveCard { from = hand, to = exhaust }`）。
    /// 与"随机目标"分开：两条流的消费次数互不影响，重放/重编码时也不会互相挪动。
    /// </summary>
    public const string RandomCardStream = "base-spire:combat-random-card";

    /// <summary>
    /// ENV-T05：**战斗内生成牌**用的命名流（`generateCards`，源 `RunState.Rng.CombatCardGeneration`）。
    /// 与抽牌重洗、随机目标、随机手牌三条流分开：生成消费的次数不会挪动其他流的位置。
    /// </summary>
    public const string CardGenerationStream = "base-spire:combat-card-generation";

    /// <summary>Move names the slice exposes as monster intents. Authority for the cycle:
    /// sts2/src/Core/Models/Monsters/DampCultist.cs:69-74 (<c>INCANTATION_MOVE</c> then <c>DARK_STRIKE_MOVE</c>).</summary>
    public const string IncantationMove = "Incantation";

    public const string DarkStrikeMove = "DarkStrike";

    /// <summary>Pile name the pack writes into its known-card facts.</summary>
    public const string DrawPileName = "draw";

    /// <summary>Selection prompt of Headbutt. Text follows the recovered string
    /// (sts2/localization/eng/cards.json:531); the legacy DSL wording differs and has no rule effect.</summary>
    public const string HeadbuttPrompt = "Choose a card to put on top of your Draw Pile.";

    /// <summary>W-06：`chooseCard { from = hand, to = exhaust }` 的提问文案（BurningPact / Brand）。</summary>
    public const string ExhaustChoicePrompt = "Choose a card to exhaust.";

    /// <summary>W-06：`upgradeSelectedCard` 的提问文案（Armaments 的普通形态）。</summary>
    public const string UpgradeChoicePrompt = "Choose a card to upgrade.";

    /// <summary>ENV-T09.2：商店删牌服务的提问文案（源 `CardSelectCmd.FromDeckForRemoval` 从长期牌组里选实例）。</summary>
    public const string RemoveCardChoicePrompt = "Choose a card to remove.";

    // -- engine-independent battle constants ----------------------------------------------------------------

    /// <summary>Ironclad starting maximum health. Source: BaseSpirePackage.cs:208.</summary>
    public const int IroncladMaxHp = 80;

    /// <summary>Energy at the start of a player turn. Source: State.cs:318 (<c>3 + powers</c>).</summary>
    public const int EnergyPerTurn = 3;

    /// <summary>Maximum hand size. Source: State.cs:1091.</summary>
    public const int MaxHandSize = 10;

    /// <summary>
    /// 药水栏的槽位数（= 声明的容量上限，不是"当前装了几瓶"）。
    /// Authority: <c>reference/sts2-sim/src/Sts2Sim.Core/Entities/Players/Player.cs:23</c>
    /// （<c>InitialMaxPotionSlotCount = 3</c>，:137-139 初始化三个空槽）与
    /// <c>reference/sts2-sim/tests/Sts2Sim.Core.Tests/Commands/PotionCmdTests.cs:51-56</c>
    /// （<c>NewPlayer_HasThreeEmptyPotionSlots</c> / <c>MaxPotionCount == 3</c>）。
    /// 增槽来源（药水腰带等）属原版遗物范围，本切片未实现，因此这里是**初始**槽数。
    /// </summary>
    public const int PotionSlotCount = 3;

    /// <summary>
    /// 受控场景的**房间数**（W-05：整局由连续战斗房间组成）。
    /// 这是切片自己的受控取值（不是从原版恢复的房间表）：真实整局的房间/地图由整局 manifest 决定，
    /// 本切片固定两间房，用来把"战斗结束 ≠ 整局结束"和"第二间房由持久 HP/长期牌组初始化"变成可判定的行为。
    /// </summary>
    public const int RunRoomCount = 2;

    /// <summary>
    /// Cards drawn at the start of a player turn. <b>Test scenario value</b>: the recovered turn-start sequence in
    /// Environment.cs:760-772 sets the turn counter, energy and block but does not itself draw, so the draw rule is
    /// not yet sourced. It is a controlled slice value, not a vanilla claim.
    /// </summary>
    public const int DrawPerTurn = 5;

    /// <summary>Starting deck after upgrades: five Strikes, four Defends and one Bash. Source: BaseSpirePackage.cs:210-211.</summary>
    public static IReadOnlyList<string> StarterDeck() =>
    [
        StrikeId, StrikeId, StrikeId, StrikeId, StrikeId,
        DefendId, DefendId, DefendId, DefendId,
        BashId,
    ];

    /// <summary>
    /// Incoming damage multiplier while the target has Vulnerable. Authority:
    /// sts2/src/Core/Models/Powers/VulnerablePower.cs:22 (<c>DamageIncrease = 1.5m</c>, a flat multiplier — equal
    /// to the recovered 150 percent) and :59-65 (stacks are a duration that ticks down, not a magnitude).
    ///
    /// The slice applies this as a fixed-point factor, never as a percent division, so the only integer conversion
    /// in the damage chain stays the single one at the end (Creature.cs:449).
    ///
    /// Note: the legacy backend carries a separate <c>VulnerableMultiplierPercentPerStack</c> field (Content.cs:53)
    /// for relic/power adjustments of that multiplier. That is a different mechanism and must not be read as
    /// "50 percent per stack".
    /// </summary>
    public const decimal VulnerableDamageMultiplier = 1.5m;

    /// <summary>
    /// Outgoing damage multiplier while the attacker has Weak. Authority:
    /// sts2/src/Core/Models/Powers/WeakPower.cs:22 (<c>DamageDecrease = 0.75m</c>) and :24-33 (it applies only to
    /// the owner's powered attacks). Equal to the legacy <c>outgoingDamageMultiplier = 0.75</c>
    /// (Powers/Content/shared-powers.spire:29).
    /// </summary>
    public const decimal WeakDamageMultiplier = 0.75m;

    /// <summary>Strength adds this much per stack, and only to the attacker's own powered attack.
    /// Authority: sts2/src/Core/Models/Powers/StrengthPower.cs:15-26 (<c>Amount</c>, guarded by
    /// <c>IsPoweredAttack()</c>), matching State.cs:585.</summary>
    public const int StrengthDamagePerStack = 1;

    /// <summary>
    /// Damage order recovered from the source: additive stage (Strength) first, then the multiplicative stage
    /// (Weak x0.75 on the attacker, Vulnerable x1.5 on the target), then the cap stage, with a single lower bound of
    /// zero, and the decimal-to-int conversion exactly once at the very end. Authority:
    /// sts2/src/Core/Hooks/Hook.cs:2515-2555 and :1565 for the stages, and
    /// sts2/src/Core/Entities/Creatures/Creature.cs:449
    /// (<c>int num = (int)Math.Min(amount, 999999999m);</c>) for the single truncation point.
    ///
    /// Implementation decision (R-DAMAGE): the slice's damage chain runs in the engine's fixed-point domain
    /// (<see cref="FieldTypeSchema.Fixed"/> plus <see cref="NumericProfile"/>) and converts to an integer exactly
    /// once with <see cref="FixedRounding.Truncate"/>. No stage rounds to an integer in between, so the planner's
    /// counterexample (base 5, Weak x0.75, Vulnerable x1.5) keeps its exact 5.625 and yields 5. Authority for the
    /// rounding rule: <c>(int)</c> on a non-negative decimal truncates, equal to <c>decimal.Truncate</c>.
    ///
    /// Known discrepancy: the legacy backend floors three times (State.cs:598, :609 and :627) and converts before
    /// block and hp-loss hooks, which differs by one for e.g. base 5 with Weak and Vulnerable
    /// (source 5, legacy 4). Authority and the worked counterexample:
    /// docs/review/tofix/r-damage-source-evidence-2026-10-01.md. The legacy triple floor is deliberately left
    /// alone: changing it is a behaviour change, not a bug fix, for a frozen backend.
    ///
    /// Not covered by the slice: the cap stage (no <c>ModifyDamageCap</c> equivalent), the <c>Unpowered</c> gate,
    /// Shrink x0.7 and the <c>ModifyHpLost</c> hooks. See docs/review/tofix/r-damage-impl-2026-10-01.md.
    /// </summary>
    public const string DamageOrder = "additive (Strength) -> multiplicative (Weak/Vulnerable) -> cap -> truncate once";

    /// <summary>Ritual stacks the DampCultist gains with its first move (Incantation).
    /// Authority: sts2/src/Core/Models/Monsters/DampCultist.cs:34,83 (normal 5, deadly 6).</summary>
    public const int RitualStacksNormal = 5;

    public const int RitualStacksDeadly = 6;

    /// <summary>Attack damage of CalcifiedCultist and DampCultist.
    /// Authority: sts2/src/Core/Models/Monsters/DampCultist.cs:32 and the base-spire move table
    /// (enemies.spire:234 for the Calcified Cultist).</summary>
    public const int DarkStrikeDamageNormal = 1;

    public const int DarkStrikeDamageDeadly = 3;

    /// <summary>Ritual grants its owner Strength equal to its stacks at that owner's turn end, skipping the turn it was
    /// applied. Authority: sts2/src/Core/Models/Powers/RitualPower.cs:45-57.</summary>
    public const string RitualRule =
        "owner turn end: gain Strength equal to the Ritual stacks, skipped once on the application turn";

    /// <summary>
    /// DampCultist spawn health range. Authority: sts2/src/Core/Models/Monsters/DampCultist.cs:28,30
    /// (<c>AscensionHelper.GetValueIfAscension(level, ascensionValue, fallbackValue)</c>): normal 51..53,
    /// ToughEnemies 52..54.
    ///
    /// The legacy DSL content has these two ranges swapped (enemies.spire:222) and both the legacy test and the
    /// source audit document pin the swapped values. This manifest follows the source; the conflict is reported in
    /// docs/review/tofix/damp-cultist-hp-range-inversion-2026-10-01.md.
    /// </summary>
    public const int DampCultistNormalMinHp = 51;

    public const int DampCultistNormalMaxHp = 53;

    public const int DampCultistToughMinHp = 52;

    public const int DampCultistToughMaxHp = 54;

    /// <summary>
    /// The slice spawns the DampCultist at the range minimum so the scenario is deterministic. The vanilla in-range
    /// roll needs the recovered RNG call order and is a B-02 item, so the slice does not claim the vanilla opening
    /// health, only the declared range.
    /// </summary>
    public const int DampCultistSliceHp = DampCultistNormalMinHp;

    public static IReadOnlyList<SliceCard> Cards { get; } =
    [
        new(StrikeId, "StrikeIronclad", "Attack", 1, 6, 9, string.Empty, 0, 0,
            "mod-the-spire/base-spire/BaseSpire/Cards/Content/ironclad-cards.spire", 1388),
        new(DefendId, "DefendIronclad", "Skill", 1, 5, 8, string.Empty, 0, 0,
            "mod-the-spire/base-spire/BaseSpire/Cards/Content/ironclad-cards.spire", 1424),
        new(BashId, "Bash", "Attack", 2, 8, 10, VulnerableId, 2, 3,
            "mod-the-spire/base-spire/BaseSpire/Cards/Content/ironclad-cards.spire", 1459),
        new(HeadbuttId, "Headbutt", "Attack", 1, 9, 12, string.Empty, 0, 0,
            "mod-the-spire/base-spire/BaseSpire/Cards/Content/ironclad-cards.spire", 2015),
    ];

    public static IReadOnlyList<SlicePower> Powers { get; } =
    [
        new(StrengthId, "Strength", "Strength", "adds its stacks to each attack hit",
            "mod-the-spire/base-spire/BaseSpire/Powers/Content/shared-powers.spire", 2),
        new(VulnerableId, "Vulnerable", "Vulnerable",
            "incoming powered damage x1.5 (stacks are a duration, not a multiplier), decremented after the enemy "
            + "turn end, skipped on the turn it is applied",
            "mod-the-spire/base-spire/BaseSpire/Powers/Content/shared-powers.spire", 17),
        new(WeakId, "Weak", "Weak",
            "its owner's powered damage x0.75 (stacks are a duration, not a multiplier), decremented after the enemy "
            + "turn end, skipped on the turn it is applied",
            "mod-the-spire/base-spire/BaseSpire/Powers/Content/shared-powers.spire", 25),
    ];

    public static IReadOnlyList<SliceEnemy> Enemies { get; } =
    [
        // Encounter order follows the source: sts2/src/Core/Models/Encounters/CultistsNormal.cs:11-12,23-24 puts the
        // Calcified Cultist in front of the Damp Cultist, matching Enemies/Content/base-spire.encounters.json:170-172.
        new(CalcifiedCultistId, "CalcifiedCultist", 38, 41, 39, 42, 2, 9, 11,
            "calcifiedCultistIncantation: applyPower(Ritual, 2, self), Buff intent",
            "calcifiedCultistDarkStrike: attack(9 default / 11 deadly, player), repeated forever",
            "mod-the-spire/base-spire/BaseSpire/Enemies/Content/enemies.spire", 230),
        new(DampCultistId, "DampCultist", DampCultistNormalMinHp, DampCultistNormalMaxHp,
            DampCultistToughMinHp, DampCultistToughMaxHp, RitualStacksNormal, DarkStrikeDamageNormal,
            DarkStrikeDamageDeadly,
            "dampCultistIncantation: applyPower(Ritual, 5 default / 6 deadly, self), Buff intent",
            "dampCultistDarkStrike: attack(1 default / 3 deadly, player), repeated forever",
            "mod-the-spire/base-spire/BaseSpire/Enemies/Content/enemies.spire", 221),
    ];

    public static SliceCard Card(string contentId) =>
        Cards.FirstOrDefault(card => string.Equals(card.ContentId, contentId, StringComparison.Ordinal))
        ?? throw Faults.Schema($"the slice manifest has no card '{contentId}'");

    public static SliceEnemy Enemy(string contentId) =>
        Enemies.FirstOrDefault(enemy => string.Equals(enemy.ContentId, contentId, StringComparison.Ordinal))
        ?? throw Faults.Schema($"the slice manifest has no enemy '{contentId}'");
}
