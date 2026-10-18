using CoreTheSpire.Engine;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Host;

/// <summary>
/// 文本规则包（core DSL）到 Host 公开接缝的<b>通用</b>会话提供方：同一份代码服务任意规则包，
/// 规则包新增公开字段/合法动作时本类不新增登记行（见 design.md §7.3 第 2/4 项）。
/// </summary>
/// <remarks>
/// 只做三件提供方必须做的事：把规则包编译出的 <see cref="RuleSet"/> 交给 Host、把每局游戏 seed
/// 字符串映射成会话种子、给出已配置但<b>尚未 Start</b> 的独立会话。不重算玩法、不猜原版布局、
/// 不解析地图/奖励、不在公开身份里放每局 seed。
/// </remarks>
internal sealed class TextRuleSource : ISessionSource
{
    private readonly RuleSet _rules;
    private readonly string _projectionId;
    private readonly string _participantRole;
    private readonly string _defaultGameSeed;
    private readonly DslPackage? _package;

    public TextRuleSource(
        RuleSet rules,
        PublicSnapshotProfile profile,
        string scenarioProfile,
        string projectionId,
        string participantRole,
        string defaultGameSeed, DslPackage? package = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioProfile);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantRole);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultGameSeed);

        _rules = rules;
        Profile = profile;
        ScenarioProfile = scenarioProfile;
        _projectionId = projectionId;
        _participantRole = participantRole;
        _defaultGameSeed = defaultGameSeed;
        _package = package;

        // Report the profile selected by the package; ordinary text-rule scenarios keep the default.
        RngProfileId = package is { RandomProfile: not "engine-default" } ? RandomService.VanillaDrawProfileId : RandomService.DefaultDrawProfileId;
    }

    public RuleSet Rules => _rules;

    public PublicSnapshotProfile Profile { get; }

    public string ScenarioProfile { get; }

    public string RngProfileId { get; }

    public ProvidedSession CreateSession(string? requestedSeed)
    {
        var requested = requestedSeed ?? _defaultGameSeed;
        if (string.IsNullOrEmpty(requested))
        {
            throw new ArgumentException("每局游戏种子必须是非空字符串（0.2 的 seed 是游戏字符串，不是数值）", nameof(requestedSeed));
        }

        // 「已配置、尚未 Start」：建会话阶段只种随机流，不执行开局程序、不消费任何随机数。
        var session = _package?.CreateSession(SessionSeed(requested), [_participantRole], requested) ?? new Session(
            _rules,
            SessionSeed(requested),
            [_participantRole],
            randomAlgorithmId: RandomService.DefaultAlgorithmId,
            randomDrawProfileId: RandomService.DefaultDrawProfileId, gameSeed: requested);

        return new ProvidedSession(
            session,
            session.Participant(0),
            _participantRole,
            _projectionId,
            RequestedSeed: requested,
            AcceptedSeed: requested);
    }

    /// <summary>
    /// 0.2 的每局 seed 是游戏字符串，由提供方（而不是 net 协议层）映射成会话种子。
    /// </summary>
    /// <remarks>
    /// 文本规则提供方没有原版的存档/布局派生，因此用稳定的 FNV-1a 64 折叠整串 UTF-8 字节：
    /// 同一字符串必得同一会话种子（同 Host 多 seed 可复现），不同字符串高概率不同；原样使用，
    /// 不做 trim、不做大小写归一、不解释成数值。映射结果只决定本局的随机流，不进入任何公开身份。
    /// </remarks>
    internal static ulong SessionSeed(string gameSeed)
    {
        ArgumentException.ThrowIfNullOrEmpty(gameSeed);
        var bytes = System.Text.Encoding.UTF8.GetBytes(gameSeed);
        var hash = 14695981039346656037UL;
        foreach (var octet in bytes)
        {
            hash ^= octet;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}
