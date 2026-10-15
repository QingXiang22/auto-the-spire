namespace NetTheSpire.Contracts.Dto;

/// <summary>
/// DTO 严格解码助手：拒绝未知键（protocol.md §6 第 217 行）、缺失必需键、
/// 类型/枚举/范围错配，并给出带 rule id 的稳定故障。
/// </summary>
public sealed class WireReader
{
    private readonly WireObject _obj;

    public WireReader(WireValue value, string what)
    {
        What = what;
        if (value is not WireObject obj)
            throw new WireFaultException("I-66", $"{what}: 期望 object，得到 {value.TypeName}");
        _obj = obj;
    }

    public string What { get; }

    public WireObject Raw => _obj;

    /// <summary>拒绝本 DTO 未声明的键。</summary>
    public WireReader Known(params string[] keys)
    {
        foreach (var member in _obj.Members)
        {
            if (!keys.Contains(member.Key, StringComparer.Ordinal))
                throw new WireFaultException("I-66", $"{What}: 未知键 '{member.Key}'");
        }

        return this;
    }

    public WireValue Require(string key)
    {
        if (!_obj.TryGet(key, out var value))
            throw new WireFaultException("I-66", $"{What}: 缺少必需键 '{key}'");
        return value;
    }

    public WireValue? Optional(string key) => _obj.Get(key);

    public string RequireNonEmptyString(string key)
    {
        var value = Require(key);
        if (value is not WireString text || text.Value.Length == 0)
            throw new WireFaultException("I-66", $"{What}.{key}: 期望非空字符串");
        return text.Value;
    }

    public string? OptionalNonEmptyString(string key)
        => Optional(key) is null ? null : RequireNonEmptyString(key);

    public string RequireString(string key)
    {
        if (Require(key) is not WireString text)
            throw new WireFaultException("I-66", $"{What}.{key}: 期望字符串");
        return text.Value;
    }

    public string RequireDecimalString(string key)
    {
        var value = Require(key);
        if (!WireDecimal.IsCanonicalDecimal(value))
            throw new WireFaultException("I-21", $"{What}.{key}: 非规范 decimal 或非字符串值");
        return ((WireString)value).Value;
    }

    public long RequireSafeInteger(string key) => WireDecimal.SafeInteger(Require(key), $"{What}.{key}");

    public bool RequireBoolean(string key)
    {
        if (Require(key) is not WireBool boolean)
            throw new WireFaultException("I-66", $"{What}.{key}: 期望 boolean");
        return boolean.Value;
    }

    public WireObject RequireObject(string key)
    {
        if (Require(key) is not WireObject obj)
            throw new WireFaultException("I-66", $"{What}.{key}: 期望 object");
        return obj;
    }

    public WireArray RequireArray(string key)
    {
        if (Require(key) is not WireArray array)
            throw new WireFaultException("I-66", $"{What}.{key}: 期望 array");
        return array;
    }

    public IReadOnlyList<string> RequireIdList(string key) =>
        RequireArray(key).Items.Select(item => item is WireString text && text.Value.Length > 0
            ? text.Value
            : throw new WireFaultException("I-66", $"{What}.{key}: 列表元素必须是非空字符串")).ToList();

    public IReadOnlyList<string> RequireStringList(string key) =>
        RequireArray(key).Items.Select(item => item is WireString text
            ? text.Value
            : throw new WireFaultException("I-66", $"{What}.{key}: 列表元素必须是字符串")).ToList();

    public IReadOnlyList<string>? OptionalStringList(string key) =>
        Optional(key) is null ? null : RequireStringList(key);

    public bool Has(string key) => _obj.ContainsKey(key);
}

/// <summary>DTO 编码助手：把 C# 值写回 wire 值。</summary>
public static class WireBuild
{
    public static WireValue IdList(IReadOnlyList<string> values) =>
        new WireArray(values.Select(value => (WireValue)new WireString(value)));

    public static WireValue StringList(IReadOnlyList<string> values) =>
        new WireArray(values.Select(value => (WireValue)new WireString(value)));

    public static WireValue NullableString(string? value) =>
        value is null ? WireValue.Null : new WireString(value);

    public static WireValue NullableInteger(long? value) =>
        value is null ? WireValue.Null : WireValue.Integer(value.Value);
}
