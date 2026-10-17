using BaseSpire.SinglePlayer;
using CoreTheSpire.Engine;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Host;

/// <summary>
/// **正式 dev 场景的会话来源**（ENV-T03）：把 base 的唯一正式工厂 <see cref="BaseSpireRuntimeFactory"/>
/// 接到公开出口的 <see cref="ISessionSource"/> 接缝上。
///
/// 组合点只有 Host：base 不认识 net（不引用 net 程序集/DTO），net 也不认识 base 的规则语义；
/// 唯一同时看到两侧的地方就是这里。因此本类只做三件事：
/// <list type="number">
/// <item>把工厂的**只读规则集**与**已派生好的公开 Profile** 交出去（不另建规则集、不重算目录）；</item>
/// <item>把 <c>start</c> 的每局 seed 原样交给工厂，并把工厂返回的**实际接受值**带回去
///       （net 不 trim、不改大小写、不数值强转——标准化由 base 按源执行）；</item>
/// <item>如实报告本后端实际使用的场景 id 与随机映射 Profile（protocol.md §7：不得声明一个与实际开局不同的
///       RNG Profile）。</item>
/// </list>
///
/// 禁止事项（ENV 实现约束第 3 节）：不在这里 <c>new Session</c>、不重算开局/洗牌/随机布局、
/// 不在 <c>Start</c> 之后补造状态；工厂返回的句柄必须保持「已配置、尚未 Start」直到
/// <see cref="ControlSession"/> 启动它**恰好一次**。
/// </summary>
internal sealed class BaseSpireFormalSource : ISessionSource
{
    private readonly BaseSpireRuntimeFactory _factory;
    private readonly string _defaultGameSeed;

    /// <param name="factory">已构造好的正式工厂（构造时已完成不支持配置的拒绝）。</param>
    /// <param name="profile">由所选规则集与 base 元数据派生出来的公开 Profile（本类不参与派生）。</param>
    /// <param name="scenarioProfile">
    /// 本后端实际支持的场景 id。正式构建用它自己的**范围身份**（<see cref="BaseSpireScope"/>），
    /// 不接受旧切片 id 冒充正式构建（protocol.md §7）。
    /// </param>
    /// <param name="defaultGameSeed">
    /// <c>start</c> 未带 seed 时使用的 **Host 显式默认游戏 seed**（protocol.md §7 允许"Host 的显式默认值"）。
    /// 它是字符串游戏 seed，不是旧的数值种子；实际接受值仍以工厂返回的
    /// <see cref="ProvidedSession.AcceptedSeed"/> 为准，并在 <c>start</c> 结果里回报。
    /// </param>
    public BaseSpireFormalSource(
        BaseSpireRuntimeFactory factory,
        PublicSnapshotProfile profile,
        string scenarioProfile,
        string defaultGameSeed)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultGameSeed);
        ScenarioProfile = scenarioProfile;
        _defaultGameSeed = defaultGameSeed;
    }

    /// <summary>只读规则集：直接取工厂那一份（会话状态不在这里）。</summary>
    public RuleSet Rules => _factory.Rules;

    /// <summary>公开 Profile：目录/字段/动作/身份的唯一来源（派生结果由构造方传入）。</summary>
    public PublicSnapshotProfile Profile { get; }

    /// <summary>本后端实际支持、且 <c>describe</c> 与 <c>start</c> 一致使用的场景 id。</summary>
    public string ScenarioProfile { get; }

    /// <summary>
    /// <c>execution_guarantees.rng_profile_id</c>：报告**实际**随机映射 Profile（来自工厂的构建描述，
    /// 不是 net 自己编的常量）。
    /// </summary>
    public string RngProfileId => _factory.Describe().RandomDrawProfileId;

    /// <summary>
    /// 交给工厂开一局。请求没带 seed 时使用 Host 的显式默认值；任何失败都在**创建之前**抛出，
    /// 由 <see cref="ControlHost"/> 转成拒绝响应，**不消耗游戏 RNG**（工厂本身也不 Start）。
    /// </summary>
    public ProvidedSession CreateSession(string? requestedSeed)
    {
        // 请求带 seed ⇒ 原样使用（base 负责标准化，本层不 trim/不改写）；
        // 请求不带 ⇒ Host 的显式默认值。两种情况都在结果里回报实际接受值。
        var requested = requestedSeed ?? _defaultGameSeed;
        var run = _factory.CreateSession(requested);
        return new ProvidedSession(
            run.Session,
            run.Participant,
            run.ParticipantRole,
            run.ProjectionId,
            // 未带 seed 时，原始值记 Host 实际使用的默认值（证据用）；实际接受值始终是 AcceptedSeed。
            RequestedSeed: requested,
            AcceptedSeed: run.AcceptedSeed);
    }
}
