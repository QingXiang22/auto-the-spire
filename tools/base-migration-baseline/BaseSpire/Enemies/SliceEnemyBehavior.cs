namespace BaseSpire.Slice;

/// <summary>
/// **敌人行动选择的作者模型**（BASE-02 批 1）。
///
/// 它对应冻结源码 `sts2/src/Core/MonsterMoves/MonsterMoveStateMachine/` 的真实机制，不是本项目自造的近似：
/// 每只怪一份**状态机**（`MonsterMoveStateMachine`），每次选取按当前状态求**下一个**状态，
/// 直到落到一个 `MoveState`（`MonsterMoveStateMachine.cs:54-80` 的 `do … while (!IsMove)`）。状态有三类：
/// <list type="bullet">
/// <item><see cref="AuthorMoveState"/> = `MoveState`：一个具名行动 + 固定后继（`MoveState.cs:67-70`；源里由
///       `FollowUpStateId`/`FollowUpState` 给出）；</item>
/// <item><see cref="AuthorRandomState"/> = `RandomBranchState`：加权随机分支（`RandomBranchState.cs:115-167`）；</item>
/// <item><see cref="AuthorOrderedState"/> = `ConditionalBranchState`：**有序**条件分支，取第一个成立项，
///       不消耗随机数（`ConditionalBranchState.cs:44-54`）。</item>
/// </list>
///
/// 作者语法（`ContentSource/enemies*.spire` 的 `behavior = …`）到本模型的映射，逐条与源码对照：
/// <list type="bullet">
/// <item>`loop(x)` → x 的后继是 x（`SnappingJaxfruit.cs:30-33`）；</item>
/// <item>`cycle(a,b,c)` → a→b、b→c、c→a（`FuzzyWurmCrawler.cs:35-38`：INHALE→ACID_GOOP→FIRST_ACID_GOOP）；</item>
/// <item>`startWith(a, then=B)` → 初始状态 a，a 的后继是 B 的入口；开局那次选取因此**不掷**（`Mawler.cs:35,43`）；</item>
/// <item>`startWithChoice(C, then=B)` → 初始状态是分支 C（不是行动），C 的候选行动与 B 里的行动都以 B 的入口为后继
///       （`Flyconid.cs:34-49`：初始为 `INITIAL` 分支，三个行动的后继都是 `RAND`）；</item>
/// <item>`graph(start=X, edges=[edge(from, to)])` → 显式给出每个行动的固定/随机后继（`Inklet.cs:69-83`）；</item>
/// <item>`chooseWithLimits(candidate(…))` → 加权分支，候选上可带 `weight`/`cooldown`/`maxConsecutive`/
///       `maxUsesPerCombat`/`when`；</item>
/// <item>`chooseWithoutRepeat(…)` → 每个候选 `CannotRepeat`、权重 1（`SoulNexus` 的写法）；</item>
/// <item>`chooseByCondition(candidate(m, when=…))` → 有序条件分支（`FrogKnight.cs` 的半血分支）。</item>
/// </list>
///
/// 未支持项（不静默近似）：`maxUsesPerCombat &gt; 1`、具名条件 `condition(Name)`、`candidate` 指向非行动状态、
/// 行动没有后继——全部在**解析期**记为未支持并附原因，敌人整条不进包。
/// </summary>
public static class EnemyRepeatKinds
{
    /// <summary>`MoveRepeatType.CanRepeatForever`：没有连续上限。</summary>
    public const string Forever = "CanRepeatForever";

    /// <summary>`MoveRepeatType.CanRepeatXTimes`：日志尾部连续出现次数 ≥ n 时权重 0。</summary>
    public const string AtMost = "CanRepeatXTimes";

    /// <summary>`MoveRepeatType.CannotRepeat`：等价 n = 1（`RandomBranchState.cs:144`）。</summary>
    public const string NoRepeat = "CannotRepeat";

    /// <summary>`MoveRepeatType.UseOnlyOnce`：曾出现在日志里就权重 0。</summary>
    public const string Once = "UseOnlyOnce";
}

/// <summary>一个候选的重复限制（来源 `MoveRepeatType` + `maxTimes`）。</summary>
public sealed record EnemyRepeatLimit(string Kind, int Times = 0)
{
    public static readonly EnemyRepeatLimit Forever = new(EnemyRepeatKinds.Forever);

    public static readonly EnemyRepeatLimit NoRepeat = new(EnemyRepeatKinds.NoRepeat);

    public static readonly EnemyRepeatLimit Once = new(EnemyRepeatKinds.Once);

    /// <summary>`CanRepeatXTimes(n)`；n ≤ 1 时就是 `CannotRepeat`。</summary>
    public static EnemyRepeatLimit AtMost(int times) =>
        times <= 1 ? NoRepeat : new EnemyRepeatLimit(EnemyRepeatKinds.AtMost, times);

    /// <summary>该限制在原版日志里比较的**尾部连续次数下限**；`CanRepeatForever` 返回 0。</summary>
    public int StreakLimit => Kind switch
    {
        EnemyRepeatKinds.NoRepeat => 1,
        EnemyRepeatKinds.AtMost => Math.Max(1, Times),
        _ => 0,
    };

    public override string ToString() =>
        Kind == EnemyRepeatKinds.AtMost ? $"{Kind}({Times})" : Kind;
}

/// <summary>
/// 加权分支里的一个候选：`candidate(move, weight=, cooldown=, maxConsecutive=, maxUsesPerCombat=, when=)`。
///
/// 对应源码 `RandomBranchState.AddBranch(state, cooldown, repeatType, weight)` /
/// `AddBranch(state, cooldown, maxRepeats, weight)`：**权重默认 1**，`cooldown`/`maxConsecutive` 都是**额外**
/// 的门槛（`maxConsecutive` 对应 `CannotRepeat`/`CanRepeatXTimes`，`cooldown` 对应 `cooldown` 字段）。
///
/// ENV-T01：权重是**源 float32**（`AddBranch` 的 `weightLambda : Func&lt;float&gt;`，可以是 `1f/12f`、`0.75f`
/// 这类分数），不再是整数近似。作者文本里的十进制字面量按 **round-to-nearest 转 float32**
/// （等价于 C# 的 `0.4f`；`1f/12f` 用九位十进制 `0.083333336` 就能精确往返）。
///
/// 被门槛置 0 的候选在源实现里就是 `0f`，加法/减法上都是精确 no-op，所以"同一行动按 `when`
/// 拆成多行"的成对写法（见 `enemies.spire` 的 `twoTailedRat`：可召援 `1f/12f`、不可召援 `1f`）
/// 与源码的单行 lambda 逐位等价，声明顺序里的 0 权重行不会改变命中项。
/// **注意**：那条声明属于 `TwoTailedRat`，它当前**未进包**（行动用到 `summonEnemy` 与具名条件等
/// 未实现原语）——这里引用的是**写法**，不是"该敌人已可运行"；分数权重的运行时证据见
/// `SliceEnemyStateMachineChecks`（受控文本 + 真实内容候选行的解析检查）。
/// </summary>
public sealed record AuthorMoveCandidate(
    string Move,
    float Weight,
    int Cooldown,
    EnemyRepeatLimit Repeat,
    int MaxUsesPerCombat,
    AuthorEnemyCondition? When);

/// <summary>条件分支/候选上的条件（`when=…`）。只登记**已实现**的形状，其余在解析期记为未支持。</summary>
public abstract record AuthorEnemyCondition
{
    /// <summary>条件读到的公开事实名（进证据与描述）。</summary>
    public abstract string Describe();
}

/// <summary>`metric(EnemyHp, LessThan, n)`：自身当前生命 &lt; n（来源 `FrogKnight` 的半血分支）。</summary>
public sealed record EnemyHpBelowCondition(int Amount) : AuthorEnemyCondition
{
    public override string Describe() => $"EnemyHp<{Amount}";
}

/// <summary>`metric(Turn, AtLeast, n)`：玩家回合数 ≥ n（来源 `FrogKnight` 的恒真兜底分支）。</summary>
public sealed record TurnAtLeastCondition(int Turn) : AuthorEnemyCondition
{
    public override string Describe() => $"Turn>={Turn}";
}

/// <summary>会合取：全部成立。</summary>
public sealed record AllCondition(IReadOnlyList<AuthorEnemyCondition> Parts) : AuthorEnemyCondition
{
    public override string Describe() => "all(" + string.Join(",", Parts.Select(part => part.Describe())) + ")";
}

/// <summary>析取：任一成立。</summary>
public sealed record AnyCondition(IReadOnlyList<AuthorEnemyCondition> Parts) : AuthorEnemyCondition
{
    public override string Describe() => "any(" + string.Join(",", Parts.Select(part => part.Describe())) + ")";
}

/// <summary>取反。</summary>
public sealed record NotCondition(AuthorEnemyCondition Inner) : AuthorEnemyCondition
{
    public override string Describe() => "not(" + Inner.Describe() + ")";
}

/// <summary>状态机里一个状态（对应 `MonsterState` 的一个具体子类）。</summary>
public abstract record AuthorEnemyState(string Id);

/// <summary>`MoveState`：一个具名行动；<see cref="FollowUp"/> 是它的后继状态 id（自己 = 循环）。</summary>
public sealed record AuthorMoveState(string Id, string Move, string FollowUp) : AuthorEnemyState(Id);

/// <summary>`RandomBranchState`：按权重选择候选（每次选取消耗**恰好一次** `monster_ai` 抽取）。</summary>
public sealed record AuthorRandomState(string Id, IReadOnlyList<AuthorMoveCandidate> Candidates)
    : AuthorEnemyState(Id);

/// <summary>`ConditionalBranchState`：按声明顺序取第一个成立项（不消耗随机数）。</summary>
public sealed record AuthorOrderedState(string Id, IReadOnlyList<(string Move, AuthorEnemyCondition When)> Candidates)
    : AuthorEnemyState(Id);

/// <summary>
/// 一只怪的完整状态机：状态表 + 初始状态。`InitialState` 可以是行动（开局那次选取不掷）或分支
/// （开局那次选取就掷，`Flyconid`/`TwoTailedRat` 的形状）。
/// </summary>
public sealed record AuthorEnemyBehavior(IReadOnlyList<AuthorEnemyState> States, string InitialState)
{
    public IEnumerable<AuthorMoveState> MoveStates => States.OfType<AuthorMoveState>();

    public AuthorEnemyState? Initial => States.FirstOrDefault(state => state.Id == InitialState);

    /// <summary>初始状态是行动时它的行动名；否则空串（开局由分支选出）。</summary>
    public string InitialMove => Initial is AuthorMoveState move ? move.Move : string.Empty;

    public bool InitialIsMove => Initial is AuthorMoveState;

    /// <summary>全部分支候选用到的冷却窗口上限（决定需要保留多少条历史）。</summary>
    public int MaxCooldown => States.OfType<AuthorRandomState>()
        .SelectMany(state => state.Candidates)
        .Select(candidate => candidate.Cooldown)
        .DefaultIfEmpty(0)
        .Max();

    /// <summary>`UseOnlyOnce` 候选指向的行动（需要在 Monster 上记"用过"）。</summary>
    public IEnumerable<string> OnceOnlyMoves => States.OfType<AuthorRandomState>()
        .SelectMany(state => state.Candidates)
        .Where(candidate => candidate.Repeat.Kind == EnemyRepeatKinds.Once || candidate.MaxUsesPerCombat == 1)
        .Select(candidate => candidate.Move)
        .Distinct(StringComparer.Ordinal);

    /// <summary>状态机里出现的全部行动名（用于"行动必须声明过"的核对）。</summary>
    public IEnumerable<string> ReferencedMoves => States.SelectMany(state => state switch
    {
        AuthorMoveState move => [move.Move],
        AuthorRandomState random => random.Candidates.Select(candidate => candidate.Move),
        AuthorOrderedState ordered => ordered.Candidates.Select(candidate => candidate.Move),
        _ => [],
    }).Distinct(StringComparer.Ordinal);
}
