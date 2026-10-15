using CoreTheSpire.Engine;
using NetTheSpire.Projection;

namespace NetTheSpire.Control;

/// <summary>
/// 规则提供方（base/mod 的正式工厂）交出的**已配置、尚未 Start** 的会话句柄。
///
/// "尚未 Start"是契约的一部分：随机流已按原版布局种好、一个随机数都没消费、也没有任何开局程序执行过。
/// 控制入口（<see cref="ControlSession.Start"/>）调用 <c>Session.Start()</c> **恰好一次**，工厂不代启动。
/// </summary>
/// <param name="Session">已配置的引擎会话（状态、RNG 游标、调度帧都属于这一局，不与别的会话共享）。</param>
/// <param name="Participant">本控制者在该会话里的参与者。</param>
/// <param name="ParticipantRole">参与者角色（必须与所选投影声明一致）。</param>
/// <param name="ProjectionId">所选公开投影。</param>
/// <param name="RequestedSeed">请求方给出的原始 seed（未标准化，留证据）。</param>
/// <param name="AcceptedSeed">实际接受的**游戏 seed 字符串**（成功响应要回报的实际值；不是内部数值种子）。</param>
public sealed record ProvidedSession(
    Session Session,
    ParticipantId Participant,
    string ParticipantRole,
    string ProjectionId,
    string RequestedSeed,
    string AcceptedSeed);

/// <summary>
/// 正式公开出口的会话来源：把"规则集 + 公开 Profile + 场景 + 开一局"这三件事收敛到一个提供方，
/// 由 Host 组装（Host 是唯一允许同时看到 base 与 net 的组合点）。
///
/// net 只通过这个接缝拿会话：**不自己 new Session、不重算初始化、不重算玩法**（ENV 实现约束第 3 节）。
/// </summary>
public interface ISessionSource
{
    /// <summary>只读规则集（会话状态不在这里）。</summary>
    RuleSet Rules { get; }

    /// <summary>公开 Profile：目录/字段/动作/身份的唯一来源。</summary>
    PublicSnapshotProfile Profile { get; }

    /// <summary>本后端实际支持的场景 profile id（<c>describe</c> 与 <c>start</c> 必须用同一个）。</summary>
    string ScenarioProfile { get; }

    /// <summary>
    /// <c>execution_guarantees.rng_profile_id</c> 要报告的**实际**随机映射 Profile
    /// （protocol.md §7：不得声明一个与真实开局不同的 Profile）。
    /// </summary>
    string RngProfileId { get; }

    /// <summary><c>limits.entities</c>：本构建可能发布的最大实体数（消费者按它分配缓冲）。</summary>
    long EntityLimit => 256;

    /// <summary><c>limits.legal_actions</c>：一个稳定边界最多发布的候选数。</summary>
    long LegalActionLimit => 256;

    /// <summary>
    /// 为一次 <c>start</c> 建一局。参数为 <c>null</c> 表示请求没有带 seed：
    /// 由提供方按自己的显式默认值或源规则生成，并在返回的 <see cref="ProvidedSession.AcceptedSeed"/> 里给出实际值。
    /// 每次调用都必须返回**独立**的会话（相同 config/seed 可重放，但两局互不污染）。
    /// </summary>
    ProvidedSession CreateSession(string? requestedSeed);
}
