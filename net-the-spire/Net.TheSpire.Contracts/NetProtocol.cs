namespace NetTheSpire.Contracts;

/// <summary>当前公开协议；发行前只支持当前版本，旧输入明确拒绝。</summary>
public static class NetProtocol
{
    public const string V02 = "0.2";

    public static void RequireKnown(string? version, string what)
    {
        if (!string.Equals(version, V02, StringComparison.Ordinal))
            throw new WireFaultException("I-66", $"{what}: protocol_version must be '{V02}', got '{version}'");
    }
}
