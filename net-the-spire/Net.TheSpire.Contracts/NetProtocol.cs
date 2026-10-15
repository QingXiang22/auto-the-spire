namespace NetTheSpire.Contracts;

/// <summary>
/// 协议版本常量与判别。
///
/// 唯一规范出处：<c>docs/design/net-the-spire/protocol.md</c> 第 1～6 节（0.1 语义）与
/// 第 7 节（ENV 正式环境的 0.2 扩展）。两个版本**并存**：0.1 保留旧切片/作者场景，0.2 只对正式单人构建开放。
///
/// 版本差异只有协议第 7 节列出的那些：<c>protocol_version</c> 常量、<c>start.seed</c> 的形状、
/// <c>start</c> 成功结果多一个实际 seed、以及 0.2 新增的 <c>string</c> 值类型。
/// 结构（目录/快照/错误/生命周期）在版本间不改变；版本常量各自精确相等，不自动降级。
/// </summary>
public static class NetProtocol
{
    public const string V01 = "0.1";

    /// <summary>正式 ENV 扩展：非空游戏字符串 seed、start 结果带实际 seed、新增 string 值类型。</summary>
    public const string V02 = "0.2";

    public static IReadOnlyList<string> Known { get; } = [V01, V02];

    /// <summary>版本必须精确相等；未知版本直接拒绝（不猜、不按内容降级）。</summary>
    public static void RequireKnown(string? version, string what)
    {
        if (version is null || !Known.Contains(version, StringComparer.Ordinal))
        {
            throw new WireFaultException("I-66",
                $"{what}: 未知 protocol_version '{version}'（本实现只支持 {string.Join('/', Known)}）");
        }
    }

    /// <summary>该版本是否允许 <c>start.seed</c> 是非数字的游戏字符串。</summary>
    public static bool UsesGameSeedString(string version) => string.Equals(version, V02, StringComparison.Ordinal);
}
