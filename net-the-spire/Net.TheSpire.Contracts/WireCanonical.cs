using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NetTheSpire.Contracts;

/// <summary>
/// 规范字节、decimal 规范形式与摘要。唯一规范出处：
/// docs/areas/net/spec/contract-artifacts.md §2 第 19/21/23 行。
/// 实现刻意不依赖任何 serializer 的默认行为：键序、转义集合、整数格式都逐项固定。
/// </summary>
public static class WireCanonical
{
    /// <summary>规范算法版本；按 §2 第 33 行作为一等键进入各身份摘要输入。</summary>
    public const string CanonicalVersion = "net-wire-canon-v1";

    /// <summary>结构整数上界 2^53-1（protocol.md §2 第 73 行）。</summary>
    public const long SafeIntegerMax = 9007199254740991L;

    /// <summary>seed 上界 2^64-1（protocol.md §4 第 185 行）。</summary>

    /// <summary>键按 UTF-8 字节序比较（不是 .NET 默认的序数/文化比较）。</summary>
    public static int CompareByUtf8Bytes(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        var length = Math.Min(leftBytes.Length, rightBytes.Length);
        for (var index = 0; index < length; index++)
        {
            var difference = leftBytes[index] - rightBytes[index];
            if (difference != 0) return difference;
        }

        return leftBytes.Length - rightBytes.Length;
    }

    public static string Text(WireValue value)
    {
        var builder = new StringBuilder();
        Write(value, builder);
        return builder.ToString();
    }

    /// <summary>无 BOM 的 UTF-8 紧凑 JSON 字节。</summary>
    public static byte[] Bytes(WireValue value) => new UTF8Encoding(false).GetBytes(Text(value));

    public static string DigestBytes(byte[] payload) => "sha256:" + Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

    public static string Digest(WireValue value) => DigestBytes(Bytes(value));

    /// <summary>对规范文本计算的摘要（等价于 <see cref="Digest"/>，便于对照 Python 侧 digest_bytes）。</summary>
    public static string DigestText(string text) => DigestBytes(new UTF8Encoding(false).GetBytes(text));

    private static void Write(WireValue value, StringBuilder builder)
    {
        switch (value)
        {
            case WireNull:
                builder.Append("null");
                return;
            case WireBool boolean:
                builder.Append(boolean.Value ? "true" : "false");
                return;
            case WireInteger integer:
                builder.Append(integer.Digits);
                return;
            case WireFloatLiteral literal:
                throw new WireFaultException(
                    "wire-float", $"wire 不含浮点字面量；精确数值必须是规范字符串（得到 {literal.Raw}）");
            case WireString text:
                WriteString(text.Value, builder);
                return;
            case WireArray array:
            {
                builder.Append('[');
                for (var index = 0; index < array.Items.Count; index++)
                {
                    if (index > 0) builder.Append(',');
                    Write(array.Items[index], builder);
                }

                builder.Append(']');
                return;
            }
            case WireObject obj:
            {
                builder.Append('{');
                var keys = obj.Members.Select(member => member.Key).ToList();
                keys.Sort(CompareByUtf8Bytes);
                for (var index = 0; index < keys.Count; index++)
                {
                    if (index > 0) builder.Append(',');
                    var key = keys[index];
                    WriteString(key, builder);
                    builder.Append(':');
                    obj.TryGet(key, out var member);
                    Write(member, builder);
                }

                builder.Append('}');
                return;
            }
            default:
                throw new WireFaultException("wire-type", $"不支持的值类型: {value.GetType().Name}");
        }
    }

    private static void WriteString(string text, StringBuilder builder)
    {
        builder.Append('"');
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            switch (current)
            {
                case '"': builder.Append("\\\""); continue;
                case '\\': builder.Append("\\\\"); continue;
                case '\b': builder.Append("\\b"); continue;
                case '\t': builder.Append("\\t"); continue;
                case '\n': builder.Append("\\n"); continue;
                case '\f': builder.Append("\\f"); continue;
                case '\r': builder.Append("\\r"); continue;
            }

            if (current < 0x20)
            {
                builder.Append("\\u").Append(((int)current).ToString("x4", CultureInfo.InvariantCulture));
                continue;
            }

            if (char.IsSurrogate(current))
            {
                var paired = char.IsHighSurrogate(current) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]);
                if (!paired)
                    throw new WireFaultException("wire-unicode", $"不合法 Unicode（孤立代理项 U+{(int)current:X4}）");
                builder.Append(current).Append(text[index + 1]);
                index++;
                continue;
            }

            // 保留原 Unicode，不做归一化，不转义 "/"（§2 第 21 行）。
            builder.Append(current);
        }

        builder.Append('"');
    }
}

/// <summary>
/// decimal 规范形式（contract-artifacts §2 第 19 行、protocol.md §2 第 73 行）。
/// </summary>
public static class WireDecimal
{
    /// <summary>源精确值的接受语法（编码器输入，允许符号/前导零/指数）。</summary>
    private static readonly System.Text.RegularExpressions.Regex SourcePattern = new(
        @"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>规范 decimal：整数部分为 0 或无前导零；小数部分最后一位非零。</summary>
    private static readonly System.Text.RegularExpressions.Regex CanonicalPattern = new(
        @"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]*[1-9])?$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>number_format=integer 的规范形式（无小数部分）。</summary>
    private static readonly System.Text.RegularExpressions.Regex IntegerPattern = new(
        @"^-?(?:0|[1-9][0-9]*)$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>wire 解码判定：字符串是否已是规范十进制。非规范输入不默默重写。</summary>
    public static bool IsCanonicalDecimal(WireValue? value)
        => value is WireString text && IsCanonicalDecimal(text.Value);

    public static bool IsCanonicalDecimal(string? text)
    {
        if (text is null || text == "-0") return false;
        return CanonicalPattern.IsMatch(text);
    }

    public static bool IsIntegerDecimal(WireValue? value)
        => value is WireString text && IntegerPattern.IsMatch(text.Value);

    public static string RequireCanonicalDecimal(WireValue value, string what)
    {
        if (!IsCanonicalDecimal(value))
            throw new WireFaultException("wire-decimal", $"{what}: 非规范 decimal {Describe(value)}");
        return ((WireString)value).Value;
    }

    /// <summary>
    /// 编码器：把源精确值规范化为 wire 十进制字符串。接受整数与十进制字符串；
    /// 拒绝 bool 与浮点（会丢失源精度）；零一律输出 "0"（含 -0、-0.000）。
    /// </summary>
    public static string Normalize(WireValue source)
    {
        string text = source switch
        {
            WireBool => throw new WireFaultException("wire-decimal", "boolean 不是 decimal"),
            WireInteger integer => integer.Digits,
            WireFloatLiteral => throw new WireFaultException("wire-decimal", "不接受浮点源值，会丢失源精度"),
            WireString str => str.Value,
            _ => throw new WireFaultException(
                "wire-decimal", $"不支持的 decimal 源类型: {source.GetType().Name}"),
        };

        if (!SourcePattern.IsMatch(text))
            throw new WireFaultException("wire-decimal", $"拒绝非 decimal 源值: '{text}'");

        var negative = text.StartsWith('-');
        var body = text[0] is '+' or '-' ? text[1..] : text;
        var exponentIndex = body.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex < 0 ? body : body[..exponentIndex];
        var exponent = exponentIndex < 0 ? 0 : int.Parse(body[(exponentIndex + 1)..], CultureInfo.InvariantCulture);

        var dotIndex = mantissa.IndexOf('.');
        var intPart = dotIndex < 0 ? mantissa : mantissa[..dotIndex];
        var fracPart = dotIndex < 0 ? string.Empty : mantissa[(dotIndex + 1)..];
        var digits = intPart + fracPart;
        var point = intPart.Length + exponent;

        string plain;
        if (point <= 0) plain = "0." + new string('0', -point) + digits;
        else if (point >= digits.Length) plain = digits + new string('0', point - digits.Length);
        else plain = digits[..point] + "." + digits[point..];

        if (plain.Contains('.')) plain = plain.TrimEnd('0').TrimEnd('.');
        var head = plain;
        var tail = string.Empty;
        var separator = plain.IndexOf('.');
        if (separator >= 0)
        {
            head = plain[..separator];
            tail = plain[(separator + 1)..];
        }

        head = head.TrimStart('0');
        if (head.Length == 0) head = "0";
        var output = tail.Length == 0 ? head : $"{head}.{tail}";
        if (output == "0") return "0";
        return negative ? "-" + output : output;
    }

    public static string Normalize(string source) => Normalize(new WireString(source));

    public static string Normalize(long source) => Normalize(WireValue.Integer(source));

    /// <summary>结构整数：0..2^53-1 的 JSON 整数（不是 number 字段的字符串）。</summary>
    public static long SafeInteger(WireValue value, string what)
    {
        if (value is not WireInteger integer)
            throw new WireFaultException("I-23", $"{what}: 结构整数必须是 JSON 整数（得到 {value.TypeName}）");
        if (!integer.TryToLong(out var number) || number < 0 || number > WireCanonical.SafeIntegerMax)
            throw new WireFaultException(
                "I-23", $"{what}: 结构整数超出 0..{WireCanonical.SafeIntegerMax}: {integer.Digits}");
        return number;
    }

    /// <summary>非空游戏字符串；原样返回，不解析数值或标准化。</summary>
    public static string Seed(WireValue value, string what = "seed")
    {
        if (value is not WireString text || text.Value.Length == 0)
            throw new WireFaultException("I-76", $"{what}: seed must be a non-empty game string");
        return text.Value;
    }

    private static string Describe(WireValue value) => value is WireString text ? $"'{text.Value}'" : value.TypeName;
}
