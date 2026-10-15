#if REFERENCE_SIM
using System.Globalization;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;
namespace Differential;

/// <summary>
/// **参考侧随机流的只读观察器**（ENV-T04 参考流边界包）。
///
/// <para>
/// 作用域：安装时保存既有 <see cref="RngDiagnostics.DrawObserver"/>，<see cref="Dispose"/> 时原样恢复；
/// 观察回调只做计数与身份登记，**不**消费随机数、**不**改参考的抽样算法/模型/种子。
/// </para>
/// <para>
/// 两类流分开标识，绝不合并：
/// </para>
/// <list type="bullet">
/// <item>**已映射具名流**：<c>RunRngSet</c> 的 12 条 run 级流与 <c>PlayerRngSet</c> 的 3 条玩家级流，
///       按只读 <c>sts2/</c> 源码推导与 base 引擎流名逐条显式映射（见 <c>RngSourceCompat</c>）。
///       它们的 Counter 是"自播种以来的原生调用数"，与 base 同名引擎流是**同一可比单位**
///       （<c>Comparable=true</c>，且只在与 base 的"被触及"判据相同——计数器有增量——时收录）；</item>
/// <item>**动态/临时 RNG**：内容级、遭遇级与其它运行期构造的 RNG，保留**原始**诊断名与独立槽位
///       （<c>observed:N:&lt;raw&gt;</c>）。即使某个动态 RNG 的 seed 与具名流相同也不据此猜同一流——
///       身份按对象，不按 seed。它们只作证据，不参与按生成器次数的比较（<c>Comparable=false</c>）。</item>
/// </list>
/// <para>
/// 逐边界读数在**边界时刻**从实际 Rng 状态取 <c>Counter</c> 与状态指纹；登记表持有对象引用，因此一个在
/// 边界之后被丢弃的临时 RNG 也保留它最后一次被观察到的真实状态。每个边界只报"自上一个边界以来被触及"的
/// 流，初始化与受控 Restore 前后的读数另列，不拿累计末态填所有边界。
/// </para>
/// </summary>
internal sealed class ReferenceRandomProbe : IDisposable
{
    private readonly RngDrawObserver? previous;
    private readonly Dictionary<Rng, Observed> observed = new();
    private readonly Dictionary<Rng, long> namedBaseline = new();
    private readonly Dictionary<Rng, long> dynamicBaseline = new();

    /// <summary>具名流的**原始**诊断名 → 显式映射后的 base 引擎流名。</summary>
    private readonly Dictionary<string, string> mappedNames = new(StringComparer.Ordinal);

    private readonly List<ReferenceStreamBoundary> boundaries = [];
    private RunRngSet? run;
    private Player? player;
    private IReadOnlyList<ReferenceStreamSnapshot>? initialization;
    private IReadOnlyList<ReferenceStreamSnapshot>? restoreBefore;
    private IReadOnlyList<ReferenceStreamSnapshot>? restoreAfter;

    /// <summary>所有被观察到的原生调用总数（初始消费与脚本消费靠前后差值分列，与旧行为一致）。</summary>
    public long Count { get; private set; }

    public ReferenceRandomProbe()
    {
        previous = RngDiagnostics.DrawObserver;
        RngDiagnostics.DrawObserver = Observe;
        foreach (var type in RngSourceCompat.RunStreamTypes)
        {
            mappedNames[RngSourceCompat.DiagnosticStreamName(type)] = RngSourceCompat.EngineStreamName(type);
        }

        foreach (var (engine, name) in RngSourceCompat.PlayerStreamNames())
        {
            mappedNames[name] = engine;
        }
    }

    /// <summary>建局之后登记参考自己的 run/player 具名流派生，并把具名流基线对齐到当前计数器。</summary>
    public void Attach(RunRngSet runSet, Player newPlayer)
    {
        run = runSet;
        player = newPlayer;
        Baseline();
    }

    /// <summary>建局完成、脚本开始之前的状态读数（初始化证据，不写入任何脚本边界）。</summary>
    public void MarkInitialization() => initialization = Cumulative("initial");

    /// <summary>
    /// 受控注入后的工具侧游标对齐：把**这一条** run 级具名流按源版本 Profile 重新装回种子，
    /// 前后各取一次真实读数，不做静默对齐。
    ///
    /// <para>
    /// 注意 <c>RunRngSet.MockRng</c> 是**换对象**（<c>_rngs[type] = new Rng(seed)</c>），不是原地重置：
    /// 所以对齐之后必须把基线重挂到**新**对象上，否则新对象会被当成"从未见过"而在每个边界重复出现
    /// （这正是 2026-10-12 调试中抓到的假象）。重挂不掩盖任何读数——这一条流被归零本身已单独记录。
    /// </para>
    /// </summary>
    public (long Before, long After) Restore(
        string runStreamType, string stringSeed, Func<RunRngSet, string, string, (long Before, long After)> restore)
    {
        restoreBefore = Cumulative("restore-before");
        var result = restore(run!, runStreamType, stringSeed);
        Baseline();
        restoreAfter = Cumulative("restore-after");
        return result;
    }

    /// <summary>
    /// 一个稳定边界的参考侧流读数：自上一个边界以来真正被触及的具名流 + 期间出现/存活的动态 RNG。
    /// 返回与 base 侧同形状的 <see cref="StreamSnapshot"/>，直接进 <c>BoundaryRecord.Streams</c>。
    ///
    /// <para>
    /// <see cref="StreamSnapshot"/> 只有一个流名字段，动态 RNG 的槽位身份只能写进名字里
    /// （<c>observed:&lt;槽位&gt;:&lt;原始名&gt;</c>）：这样比较器与维度结算都能按 <see cref="Records.ObservedStreamPrefix"/>
    /// 把不可比的槽位滤掉，同时原始名与槽位在记录里完整保留，不与具名流合并。
    /// </para>
    /// </summary>
    public IReadOnlyList<StreamSnapshot> AtBoundary(int index, string kind)
    {
        var snapshots = Snapshots($"boundary[{index}]");
        boundaries.Add(new ReferenceStreamBoundary(index, kind, snapshots));
        return
        [
            .. snapshots.Select(item => new StreamSnapshot(
                item.Comparable ? item.Stream : item.Slot + ":" + item.Stream,
                item.Counter,
                item.Fingerprint)),
        ];
    }

    /// <summary>完整的参考侧流证据（逐边界 + 初始化 + Restore 前后），写进参考记录。</summary>
    public ReferenceStreamEvidence Evidence() => new(
        "native-counter-increments",
        "RngDiagnostics.DrawObserver 逐次观察所有推进 Counter 的原生调用；逐边界在边界时刻从实际 Rng 状态"
            + "读取 Counter 与状态指纹。具名流按 source-compat-sts2-frozen@1 显式映射到 base 引擎流名，"
            + "动态/临时 RNG 保留原始诊断名与独立 observed 槽位；不按 seed 猜同一流、不合并槽位、不补 0。",
        initialization ?? [],
        restoreBefore ?? [],
        restoreAfter ?? [],
        [.. boundaries]);

    /// <summary>
    /// 脚本前后的**累计**读数（<c>script_random</c> 的既有两列）：报"到目前为止被触及"的全部流，
    /// 包含初始化期消费与工具侧归零的那一条。这里**不**做增量过滤——边界读数才是增量视图，
    /// 把 <c>script_random</c> 也改成增量会让 before/after 差丢失初始化消费。
    /// </summary>
    public IReadOnlyList<StreamSnapshot> Streams() =>
        [.. Cumulative("script-random").Select(item =>
            new StreamSnapshot(
                item.Comparable ? item.Stream : item.Slot + ":" + item.Stream,
                item.Counter,
                item.Fingerprint))];

    /// <summary>具名流的**增量**读数：自上次基线以来计数器有变化的流（与 base 同一"被触及"判据）。</summary>
    private IReadOnlyList<ReferenceStreamSnapshot> Snapshots(string status)
    {
        var snapshots = new List<ReferenceStreamSnapshot>();
        foreach (var type in RngSourceCompat.RunStreamTypes)
        {
            AddNamed(snapshots, status, "mapped-run-stream",
                RngSourceCompat.EngineStreamName(type), RngSourceCompat.DiagnosticStreamName(type),
                run?.GetRng(ParseRun(type)), includeUntouched: false);
        }

        foreach (var (engine, name) in RngSourceCompat.PlayerStreamNames())
        {
            AddNamed(snapshots, status, "mapped-player-stream", engine, name, PlayerStream(name),
                includeUntouched: false);
        }

        // 动态/临时 RNG：只报**自上一个读数以来又有推进**的槽位。它们在初始化期就被创建，
        // 把不动的累计值在每个边界重抄一遍就成了"拿一个末态填所有边界"；推进过的才带信息。
        foreach (var (rng, entry) in observed)
        {
            if (entry.Mapped)
            {
                continue;
            }

            if (dynamicBaseline.TryGetValue(rng, out var calls) && calls == entry.Calls)
            {
                continue;
            }

            dynamicBaseline[rng] = entry.Calls;
            snapshots.Add(Dynamic(rng, entry, status));
        }

        return Sorted(snapshots);
    }

    /// <summary>当前状态的**全量**读数（初始化、受控 Restore 前后、script_random 用）：不做增量过滤。</summary>
    private IReadOnlyList<ReferenceStreamSnapshot> Cumulative(string status)
    {
        var snapshots = new List<ReferenceStreamSnapshot>();
        foreach (var type in RngSourceCompat.RunStreamTypes)
        {
            AddNamed(snapshots, status, "mapped-run-stream",
                RngSourceCompat.EngineStreamName(type), RngSourceCompat.DiagnosticStreamName(type),
                run?.GetRng(ParseRun(type)), includeUntouched: true);
        }

        foreach (var (engine, name) in RngSourceCompat.PlayerStreamNames())
        {
            AddNamed(snapshots, status, "mapped-player-stream", engine, name, PlayerStream(name),
                includeUntouched: true);
        }

        foreach (var (rng, entry) in observed)
        {
            if (!entry.Mapped)
            {
                dynamicBaseline[rng] = entry.Calls;
                snapshots.Add(Dynamic(rng, entry, status));
            }
        }

        return Sorted(snapshots);
    }

    private static ReferenceStreamSnapshot Dynamic(Rng rng, Observed entry, string status)
    {
        var state = rng.ToSerializable();
        return new ReferenceStreamSnapshot(
            Records.ObservedStreamPrefix + entry.Ordinal, entry.Name, entry.Origin, status, rng.Counter,
            RngSourceCompat.FingerprintText(state.state0, state.state1, state.state2, state.state3),
            false);
    }

    private static IReadOnlyList<ReferenceStreamSnapshot> Sorted(List<ReferenceStreamSnapshot> snapshots) =>
    [
        .. snapshots
            .OrderBy(item => item.Stream, StringComparer.Ordinal)
            .ThenBy(item => item.Slot, StringComparer.Ordinal),
    ];

    private void AddNamed(List<ReferenceStreamSnapshot> snapshots, string status, string origin,
        string engineName, string diagnosticName, Rng? stream, bool includeUntouched)
    {
        if (stream is null)
        {
            return;
        }

        var touched = !namedBaseline.TryGetValue(stream, out var baseline) || stream.Counter != baseline;
        if (!touched && !includeUntouched)
        {
            return;
        }

        var state = stream.ToSerializable();
        snapshots.Add(new ReferenceStreamSnapshot(
            "named:" + diagnosticName, engineName, origin, status, stream.Counter,
            RngSourceCompat.FingerprintText(state.state0, state.state1, state.state2, state.state3),
            true));
    }

    /// <summary>把具名流的比较基线对齐到当前计数器：之后只有真的被抽过的流才会出现在读数里。</summary>
    private void Baseline()
    {
        namedBaseline.Clear();
        foreach (var type in RngSourceCompat.RunStreamTypes)
        {
            if (run?.GetRng(ParseRun(type)) is { } stream)
            {
                namedBaseline[stream] = stream.Counter;
            }
        }

        foreach (var (_, name) in RngSourceCompat.PlayerStreamNames())
        {
            if (PlayerStream(name) is { } stream)
            {
                namedBaseline[stream] = stream.Counter;
            }
        }
    }

    private Rng? PlayerStream(string diagnosticName) =>
        diagnosticName.StartsWith("named.", StringComparison.Ordinal)
        && Enum.TryParse<Sts2Sim.Core.Entities.Rngs.PlayerRngType>(
            diagnosticName["named.".Length..], ignoreCase: true, out var type)
            ? player?.PlayerRng?.GetRng(type)
            : null;

    private static Sts2Sim.Core.Entities.Rngs.RunRngType ParseRun(string type) =>
        Enum.Parse<Sts2Sim.Core.Entities.Rngs.RunRngType>(type);

    private void Observe(Rng rng, string op, double min, double max, int arity)
    {
        if (!observed.TryGetValue(rng, out var entry))
        {
            var raw = rng.DiagnosticStreamName;
            var mapped = raw is not null && mappedNames.TryGetValue(raw, out var engine);
            entry = new Observed(
                observed.Count.ToString(CultureInfo.InvariantCulture),
                raw is null ? "unnamed" : mapped ? "mapped:" + raw : "dynamic:" + raw,
                mapped,
                mapped && raw is not null ? mappedNames[raw] : raw ?? "unnamed",
                0);
        }

        observed[rng] = entry with { Calls = entry.Calls + 1 };
        Count++;
        previous?.Invoke(rng, op, min, max, arity);
    }

    public void Dispose() => RngDiagnostics.DrawObserver = previous;

    private sealed record Observed(string Ordinal, string Origin, bool Mapped, string Name, long Calls);
}
#endif
