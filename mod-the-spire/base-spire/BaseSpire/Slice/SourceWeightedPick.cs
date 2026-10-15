using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// ENV-T01：源 `RandomBranchState.GetNextState` 的**加权分桶**纯计算，逐位复刻
/// `sts2/src/Core/MonsterMoves/MonsterMoveStateMachine/RandomBranchState.cs:115-128`：
/// <code>
/// float max = States.Sum(x => GetStateWeight(x, owner));
/// float num = rng.NextFloat(max);
/// foreach (StateWeight state in States) { num -= GetStateWeight(state, owner); if (num &lt;= 0f) return state.stateId; }
/// throw new InvalidOperationException("No valid state found in RandomBranchState " + Id + "!");
/// </code>
/// 三处最容易写成"近似"的地方，这里都按源来：
/// <list type="number">
/// <item>`States.Sum(...)` 是 LINQ `IEnumerable&lt;float&gt;.Sum`：.NET 用 **double 累加器**、末尾**一次**转 float32。
///       它和"逐项 float32 相加"不是同一次算；后端调用**同一个重载**，保证逐位一致。</item>
/// <item>`rng.NextFloat(max)` = `(float)(NextDouble() * (double)max + 0.0)`（`Rng.cs:156-164`，`min` 为 0）：
///       乘法在 double 里做、**只转换一次**到 float32。</item>
/// <item>命中判据是 `&lt;= 0f`（不是 `&lt;`），并按**声明顺序**逐项做 float32 减法。</item>
/// </list>
///
/// 输入是"随机数的一次抽取结果"，不是 Session：`SampleInt(stream, 0, 2^53)` 恰好取到源 `NextDouble()` 的
/// 53 位值（`bits = raw &gt;&gt; 11`），因此纯计算不接触 RNG、也不消耗随机数。权重以 **float32 位型**
/// （`Int`，`BitConverter.SingleToInt32Bits`）传入，不是十进制近似；门槛不满足的候选由 IR 传 `0`（即 `0f`）。
/// </summary>
public static class SourceWeightedPick
{
    /// <summary>计算资产名（进规则身份）。</summary>
    public const string AssetName = "SourceWeightedPick";

    /// <summary>一次 `SampleInt(0, 2^53)` 的跨度；`bits = raw &gt;&gt; 11` 恰好落在 `[0, 2^53)`。</summary>
    public const long Span = 1L << 53;

    /// <summary>`NextDouble()` 的 53 位值到 `[0,1)` 的换算（`2^-53`，二进制精确）。</summary>
    public const double Unit = 1.0 / (double)Span;

    /// <summary>权重输入（float32 位型）的字段范围。</summary>
    public const long WeightBitsMax = uint.MaxValue;

    /// <summary>
    /// 源语义一次分桶：给定 53 位抽取值 <paramref name="bits"/> 与**按声明顺序**的权重，
    /// 返回命中的下标；没有任何项命中时返回 `-1`（源在这个位置抛 `InvalidOperationException`）。
    ///
    /// 逐行照抄 `RandomBranchState.cs:115-128`：`Sum` → `NextFloat(total)` → 逐项 `-=` / `&lt;= 0f`。
    /// 后端 <see cref="SourceWeightedPickBackend"/> 调用它，判别检查也直接对它做**表驱动**验证
    /// （期望值按源语义手算，不复用本函数的实现）。
    /// </summary>
    public static long Pick(long bits, ReadOnlySpan<float> weights)
    {
        // 源 `RandomBranchState.cs:117` 的 `States.Sum(...)`：LINQ `IEnumerable<float>.Sum`
        // 用 double 累加器、末尾一次转 float32（**不是**逐项 float32 相加）。
        // 这里刻意调用同一个 LINQ 重载，而不是手写累加，保证与源逐位一致。
        var total = weights.ToArray().Sum();

        // 源 `Rng.cs:156-164` 的 `NextFloat(max)`（`min = 0`）：`(float)(NextDouble() * (double)max)`。
        // 乘法在 double 里做、只向 float32 转换一次；`bits * Unit` 就是源 `NextDouble()`。
        var number = (float)(bits * Unit * (double)total);

        // 源 `RandomBranchState.cs:119-124`：按声明顺序做 float32 减法、`<= 0f` 命中。
        for (var index = 0; index < weights.Length; index++)
        {
            number -= weights[index];
            if (number <= 0f)
            {
                return index;
            }
        }

        return -1;
    }
}

/// <summary>
/// <see cref="SourceWeightedPick"/> 的宿主后端（ENV-T01）。
///
/// `BackendId` 是**算法 + 数值语义的版本化身份**：它进 `RuleSet.Identity`（冻结契约
/// `docs/contracts/core-engine-baseline.md:57`"规则身份绑定…编译器/扩展/**后端**与数值语义"），
/// 因此本后端参与 `RulesetDigest`，最终进入 `PublicBuildId`——"算法资产/backend/数值身份进入构建身份"。
///
/// 宿主必须把实例注入 `Session`（`new Session(…, computeBackend: SourceWeightedPickBackend.Instance)`）；
/// 未安装后端时引擎在**执行**该资产时报 `Unsupported`（链接期只检查声明）。
/// </summary>
public sealed class SourceWeightedPickBackend : IComputeBackend
{
    /// <summary>后端标识（进规则身份；改算法或数值语义必须换版本号）。</summary>
    public const string BackendId = "base-spire-source-float32@1";

    /// <summary>无状态单例：后端不得重入 Session，也不持有任何会话数据。</summary>
    public static SourceWeightedPickBackend Instance { get; } = new();

    private SourceWeightedPickBackend()
    {
    }

    public string Id => BackendId;

    /// <summary>
    /// 输入：`Bits`（`[0, 2^53)`）、`Count`（有效候选数）、`W0…`（各候选的 float32 位型）。
    /// 输出：命中的候选下标，或 `-1`（源在这里抛 `InvalidOperationException`，由 IR 显式失败）。
    /// </summary>
    public void Execute(ComputeAssetDefinition asset, IReadOnlyList<Value> inputs, Value[] outputs)
    {
        // ENV-T06-source-stable-shuffle-20261012：源 `StableShuffle` 预排序是本包第二条纯计算资产。
        // 引擎每会话只装一个 backend，且它的 BackendId 与本地图/奖励/商店资产相同，
        // 因此在这里按 `asset.Entry` 分派（`BaseSpireComputeBackend` 的 default 分支逐字转发到本方法，
        // 两条会话路径一并生效）。该资产不消耗随机数、不接触 Session/State。
        if (SourceStableShuffle.TryExecute(asset.Entry, inputs, outputs))
        {
            return;
        }

        var bits = inputs[0].AsInt();
        var count = inputs[1].AsInt();
        if (count <= 0 || count + 2 > inputs.Count || bits < 0 || bits >= SourceWeightedPick.Span)
        {
            outputs[0] = Value.Of(-1L);
            return;
        }

        var weights = new float[count];
        for (var index = 0; index < count; index++)
        {
            weights[index] = BitConverter.Int32BitsToSingle((int)inputs[index + 2].AsInt());
        }

        outputs[0] = Value.Of(SourceWeightedPick.Pick(bits, weights));
    }
}
