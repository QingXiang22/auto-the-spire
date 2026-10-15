using BaseSpire.SinglePlayer;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Host;

/// <summary>
/// **正式 dev 场景**的组合（ENV-T03）：base 唯一正式工厂 + 派生公开 Profile + 正式控制宿主。
///
/// 这是 Host 的第二个场景，与旧的受控切片场景（<c>CombatSlicePackage</c> + <c>SlicePublicProfile</c>，
/// 协议 0.1，数字 seed）并存：旧场景与既有 stdio 检查不变，正式场景走
/// <see cref="ControlHost(ISessionSource,int,string?)"/> 这条「规则提供方建会话、控制入口只启动一次」的路。
///
/// 三件事必须在这里对齐、且都来自**所选 source**（不能各写一份常量）：
/// <list type="number">
/// <item>场景 id = 构建的范围身份（<see cref="RunConfiguration.ScopeId"/>）；</item>
/// <item>随机映射 Profile = 构建描述里的实际值（<c>describe</c> 与真实开局必须一致）；</item>
/// <item>规则集/公开 Profile = 同一个工厂的两份只读产物。</item>
/// </list>
/// </summary>
internal static class FormalDevScenario
{
    /// <summary>
    /// 正式 Host 命令行可见的**范围名 → 范围 id**。名字只是选择入口，<c>ScopeId</c> 才是构建身份
    /// （它进 ruleset/公开构建身份，协议 §7）；这里不另抄 id，直接引用 base 的常量。
    /// </summary>
    public static readonly IReadOnlyList<(string Name, string ScopeId)> SelectableScopes =
    [
        ("dev", BaseSpireScope.Dev),
        ("dev-map", BaseSpireScope.DevMap),
    ];

    /// <summary>命令行没给 <c>--scope</c> 时的默认范围名：与既有正式入口逐字相同（dev）。</summary>
    public const string DefaultScopeName = "dev";

    /// <summary>范围名 → 范围 id；未知名字返回 <c>null</c>，由调用方**显式拒绝**，不静默回退到 dev。</summary>
    public static string? ScopeIdForName(string name) =>
        SelectableScopes.FirstOrDefault(scope => string.Equals(scope.Name, name, StringComparison.Ordinal)) is
            { ScopeId: { } scopeId }
            ? scopeId
            : null;

    /// <summary>
    /// 默认的正式场景：Ironclad、进阶 0、<paramref name="scopeId"/> 范围。
    /// 无参调用等价于旧的 <c>new(RunConfiguration.Ironclad())</c> ⇒ 默认入口的身份/行为逐字不变。
    /// </summary>
    public static BaseSpireRuntimeFactory CreateFactory(string scopeId = BaseSpireScope.Dev) =>
        new(RunConfiguration.Ironclad(0, scopeId));

    /// <summary>
    /// 组装正式控制宿主。元数据由调用方传入，转换点在 <see cref="FormalDevMetadata"/>（唯一的 base→net 转换）。
    /// </summary>
    public static ControlHost Create(
        BaseSpireRuntimeFactory factory,
        PublicBuildMetadata metadata,
        string defaultGameSeed,
        int ledgerCapacity = 4096)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(metadata);

        var rules = factory.Rules;
        var semantics = factory.PublicSemantics;
        // 投影与角色由**提供方元数据**声明（它构造时已与规则集里的投影闭合），Host 不猜、不另抄常量。
        if (!string.Equals(semantics.ProjectionId, BaseSpireRuntimeFactory.ProjectionId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"base 元数据声明的投影 '{semantics.ProjectionId}' 与本 Host 选择的 "
                + $"'{BaseSpireRuntimeFactory.ProjectionId}' 不一致：不能对另一个投影发布目录");
        }

        var profile = DerivedPublicProfile.Build(
            rules,
            semantics.ProjectionId,
            semantics.ParticipantRole,
            metadata,
            NetProtocol.V02);
        var source = new BaseSpireFormalSource(
            factory,
            profile,
            factory.Configuration.ScopeId,
            defaultGameSeed);
        return new ControlHost(source, ledgerCapacity);
    }
}
