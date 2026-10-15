using System.Numerics;

namespace NetTheSpire.Contracts;

/// <summary>
/// wire v0.1 的值模型。规范字节来自这个模型，而不是任意 .NET serializer：
/// 结构整数保留十进制数字（JSON 整数），精确数值始终是规范字符串，浮点字面量
/// 只可能出现在宽松解析里并且在规范化时被拒绝。
/// 规范出处：docs/design/net-the-spire/contract-artifacts.md §2。
/// </summary>
public enum WireKind
{
    Null,
    Boolean,
    Integer,
    FloatLiteral,
    String,
    Array,
    Object,
}

public abstract class WireValue
{
    public abstract WireKind Kind { get; }

    public virtual string TypeName => Kind switch
    {
        WireKind.Null => "null",
        WireKind.Boolean => "boolean",
        WireKind.Integer => "integer",
        WireKind.FloatLiteral => "number",
        WireKind.String => "string",
        WireKind.Array => "array",
        WireKind.Object => "object",
        _ => "unknown",
    };

    public static readonly WireNull Null = WireNull.Instance;

    public static WireValue Bool(bool value) => value ? WireBool.True : WireBool.False;

    public static WireInteger Integer(string digits) => new(digits);

    public static WireInteger Integer(long value) =>
        new(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static WireInteger Integer(BigInteger value) =>
        new(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static WireString Str(string value) => new(value);

    public static WireArray Array(params WireValue[] items) => new(items);

    public static WireArray Array(IEnumerable<WireValue> items) => new(items);

    public static WireObject Object(params (string Key, WireValue Value)[] members) =>
        new(members.Select(member => new KeyValuePair<string, WireValue>(member.Key, member.Value)));
}

public sealed class WireNull : WireValue
{
    public static readonly WireNull Instance = new();

    private WireNull() { }

    public override WireKind Kind => WireKind.Null;
}

public sealed class WireBool : WireValue
{
    public static readonly WireBool True = new(true);
    public static readonly WireBool False = new(false);

    private WireBool(bool value) => Value = value;

    public bool Value { get; }

    public override WireKind Kind => WireKind.Boolean;
}

public sealed class WireInteger : WireValue
{
    public WireInteger(string digits)
    {
        if (string.IsNullOrEmpty(digits))
            throw new ArgumentException("结构整数不能为空", nameof(digits));
        Digits = Normalize(digits);
    }

    /// <summary>最短十进制数字：无前导零、无 <c>-0</c>（与 Python <c>str(int)</c> 一致）。</summary>
    public string Digits { get; }

    public override WireKind Kind => WireKind.Integer;

    public BigInteger ToBigInteger() => BigInteger.Parse(Digits, System.Globalization.CultureInfo.InvariantCulture);

    public bool TryToLong(out long value) => long.TryParse(Digits, out value);

    private static string Normalize(string digits)
    {
        var negative = digits.StartsWith('-');
        var body = negative ? digits.TrimStart('-') : digits;
        body = body.TrimStart('0');
        if (body.Length == 0) return "0";
        return negative ? "-" + body : body;
    }
}

/// <summary>JSON 浮点字面量：wire 不含它，规范化必然拒绝（contract-artifacts §2 第 21 行）。</summary>
public sealed class WireFloatLiteral : WireValue
{
    public WireFloatLiteral(string raw) => Raw = raw;

    public string Raw { get; }

    public override WireKind Kind => WireKind.FloatLiteral;
}

public sealed class WireString : WireValue
{
    public WireString(string value) => Value = value;

    public string Value { get; }

    public override WireKind Kind => WireKind.String;
}

public sealed class WireArray : WireValue
{
    private readonly List<WireValue> _items;

    public WireArray(IEnumerable<WireValue> items) => _items = items.ToList();

    public IReadOnlyList<WireValue> Items => _items;

    public override WireKind Kind => WireKind.Array;
}

/// <summary>JSON 对象：保留成员顺序（规范化时按键的 UTF-8 字节序重排）。</summary>
public sealed class WireObject : WireValue
{
    private readonly List<KeyValuePair<string, WireValue>> _members;

    public WireObject(IEnumerable<KeyValuePair<string, WireValue>> members) => _members = members.ToList();

    public IReadOnlyList<KeyValuePair<string, WireValue>> Members => _members;

    public IEnumerable<string> Keys => _members.Select(member => member.Key);

    public override WireKind Kind => WireKind.Object;

    public int Count => _members.Count;

    public bool ContainsKey(string key) => _members.Any(member => member.Key == key);

    public bool TryGet(string key, out WireValue value)
    {
        for (var index = _members.Count - 1; index >= 0; index--)
        {
            if (_members[index].Key == key)
            {
                value = _members[index].Value;
                return true;
            }
        }

        value = WireValue.Null;
        return false;
    }

    public WireValue? Get(string key) => TryGet(key, out var value) ? value : null;

    public WireObject With(params (string Key, WireValue Value)[] members) =>
        new(_members.Concat(members.Select(member => new KeyValuePair<string, WireValue>(member.Key, member.Value))));
}
