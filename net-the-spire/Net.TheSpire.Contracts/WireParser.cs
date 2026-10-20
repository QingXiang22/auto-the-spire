using System.Globalization;
using System.Text;

namespace NetTheSpire.Contracts;

/// <summary>带稳定 rule id 的契约故障，供分层检查复用（与 Python 侧 ParseError/CanonicalError 对应）。</summary>
public class WireFaultException : Exception
{
    public WireFaultException(string ruleId, string message) : base(message) => RuleId = ruleId;

    public string RuleId { get; }
}

/// <summary>
/// wire v0.2 的 L0 解析层（唯一规范出处：protocol.md §6 第 217 行、§2 第 73 行、
/// contract-artifacts §1 第 11 行）。与 tools/contract-check/wire_json.py 逐条对应：
/// 拒绝重复键、NaN/Infinity、浮点字面量、BOM、非 UTF-8 与孤立代理项。
/// </summary>
public static class WireParser
{
    /// <summary>严格解析：wire 文档必须能被规范化，浮点字面量在解析阶段即拒绝。</summary>
    public static WireValue ParseStrict(string text, string what = "document")
        => Parse(text, what, allowFloat: false, checkUnicode: true);

    /// <summary>
    /// 宽松解析：只拒绝重复键、BOM、非法 UTF-8 与 JSON 语法错误；浮点字面量保留为
    /// <see cref="WireFloatLiteral"/>、孤立代理项保留在字符串里，交由规范化阶段报告
    /// （fixtures/golden 的 canonical_reject 向量依赖这一区别，与 Python 的 json.load 一致）。
    /// </summary>
    public static WireValue ParseLenient(string text, string what = "document")
        => Parse(text, what, allowFloat: true, checkUnicode: false);

    public static WireValue ParseUtf8Strict(byte[] payload, string what = "document")
    {
        if (payload.Length >= 3 && payload[0] == 0xEF && payload[1] == 0xBB && payload[2] == 0xBF)
            throw new WireFaultException("L0-BOM", $"{what}: UTF-8 文本不得带 BOM");
        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(payload);
        }
        catch (DecoderFallbackException exception)
        {
            throw new WireFaultException("L0-UTF8", $"{what}: 不是合法 UTF-8: {exception.Message}");
        }

        return ParseStrict(text, what);
    }

    private static WireValue Parse(string text, string what, bool allowFloat, bool checkUnicode)
    {
        if (text.StartsWith('\uFEFF'))
            throw new WireFaultException("L0-BOM", $"{what}: UTF-8 文本不得带 BOM");

        var parser = new JsonReader(text, what, allowFloat);
        var value = parser.ReadValue();
        parser.SkipWhitespace();
        if (!parser.AtEnd)
            throw new WireFaultException(
                "L0-JSON", $"{what}: JSON 解析失败: 容器结束后仍有多余内容（位置 {parser.Position}）");
        if (checkUnicode) CheckUnicode(value, what);
        return value;
    }

    /// <summary>拒绝孤立代理项等不合法 Unicode（contract-artifacts §2 第 21 行）。</summary>
    public static void CheckUnicode(WireValue node, string what = "document")
    {
        foreach (var (path, text) in Strings(node))
        {
            for (var index = 0; index < text.Length; index++)
            {
                var codePoint = text[index];
                if (char.IsSurrogate(codePoint) &&
                    !(char.IsHighSurrogate(codePoint) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])))
                {
                    throw new WireFaultException("L0-UNICODE", $"{what}: {path} 含非法 Unicode U+{(int)codePoint:X4}");
                }

                if (char.IsHighSurrogate(codePoint)) index++;
            }
        }
    }

    private static IEnumerable<(string Path, string Text)> Strings(WireValue node, string path = "$")
    {
        switch (node)
        {
            case WireString text:
                yield return (path, text.Value);
                break;
            case WireObject obj:
                foreach (var member in obj.Members)
                {
                    yield return ($"{path}.<key>", member.Key);
                    foreach (var item in Strings(member.Value, $"{path}.{member.Key}")) yield return item;
                }

                break;
            case WireArray array:
                for (var index = 0; index < array.Items.Count; index++)
                {
                    foreach (var item in Strings(array.Items[index], $"{path}[{index}]")) yield return item;
                }

                break;
        }
    }

    private sealed class JsonReader(string text, string what, bool allowFloat)
    {
        private int _index;

        public int Position => _index;

        public bool AtEnd => _index >= text.Length;

        public WireValue ReadValue()
        {
            SkipWhitespace();
            if (AtEnd) throw Fail("意外的输入结束");
            var current = text[_index];
            return current switch
            {
                '{' => ReadObject(),
                '[' => ReadArray(),
                '"' => new WireString(ReadString()),
                't' => ReadLiteral("true", WireValue.Bool(true)),
                'f' => ReadLiteral("false", WireValue.Bool(false)),
                'n' => ReadLiteral("null", WireValue.Null),
                'N' => throw new WireFaultException("I-91", $"{what}: JSON 常量 NaN 不是 wire 值"),
                'I' => throw new WireFaultException("I-91", $"{what}: JSON 常量 Infinity 不是 wire 值"),
                '-' => ReadNumber(),
                _ => char.IsDigit(current) ? ReadNumber() : throw Fail($"意外的字符 '{current}'"),
            };
        }

        public void SkipWhitespace()
        {
            while (!AtEnd && text[_index] is ' ' or '\t' or '\n' or '\r') _index++;
        }

        private WireValue ReadLiteral(string literal, WireValue value)
        {
            if (_index + literal.Length > text.Length || text.Substring(_index, literal.Length) != literal)
                throw Fail($"非法字面量，期望 {literal}");
            _index += literal.Length;
            return value;
        }

        private WireValue ReadNumber()
        {
            var start = _index;
            if (!AtEnd && text[_index] == '-') _index++;
            if (AtEnd || !char.IsDigit(text[_index])) throw Fail("非法数字：缺少整数位");
            if (text[_index] == '0')
            {
                _index++;
            }
            else
            {
                while (!AtEnd && char.IsDigit(text[_index])) _index++;
            }

            var isFloat = false;
            if (!AtEnd && text[_index] == '.')
            {
                isFloat = true;
                _index++;
                if (AtEnd || !char.IsDigit(text[_index])) throw Fail("非法数字：小数点后缺少数字");
                while (!AtEnd && char.IsDigit(text[_index])) _index++;
            }

            if (!AtEnd && (text[_index] == 'e' || text[_index] == 'E'))
            {
                isFloat = true;
                _index++;
                if (!AtEnd && (text[_index] == '+' || text[_index] == '-')) _index++;
                if (AtEnd || !char.IsDigit(text[_index])) throw Fail("非法数字：指数缺少数字");
                while (!AtEnd && char.IsDigit(text[_index])) _index++;
            }

            var raw = text[start.._index];
            if (!isFloat) return new WireInteger(raw);
            if (!allowFloat) throw new WireFaultException("I-91", $"{what}: wire 不含浮点字面量: {raw}");
            return new WireFloatLiteral(raw);
        }

        private WireValue ReadObject()
        {
            _index++; // '{'
            var members = new List<KeyValuePair<string, WireValue>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            SkipWhitespace();
            if (!AtEnd && text[_index] == '}')
            {
                _index++;
                return new WireObject(members);
            }

            while (true)
            {
                SkipWhitespace();
                if (AtEnd || text[_index] != '"') throw Fail("对象的键必须是字符串");
                var key = ReadString();
                if (!seen.Add(key)) throw new WireFaultException("I-24", $"重复 JSON 键: '{key}'");
                SkipWhitespace();
                if (AtEnd || text[_index] != ':') throw Fail("对象的键后缺少 ':'");
                _index++;
                members.Add(new KeyValuePair<string, WireValue>(key, ReadValue()));
                SkipWhitespace();
                if (AtEnd) throw Fail("对象未闭合");
                if (text[_index] == ',')
                {
                    _index++;
                    continue;
                }

                if (text[_index] == '}')
                {
                    _index++;
                    return new WireObject(members);
                }

                throw Fail($"对象中出现意外字符 '{text[_index]}'");
            }
        }

        private WireValue ReadArray()
        {
            _index++; // '['
            var items = new List<WireValue>();
            SkipWhitespace();
            if (!AtEnd && text[_index] == ']')
            {
                _index++;
                return new WireArray(items);
            }

            while (true)
            {
                items.Add(ReadValue());
                SkipWhitespace();
                if (AtEnd) throw Fail("数组未闭合");
                if (text[_index] == ',')
                {
                    _index++;
                    continue;
                }

                if (text[_index] == ']')
                {
                    _index++;
                    return new WireArray(items);
                }

                throw Fail($"数组中出现意外字符 '{text[_index]}'");
            }
        }

        private string ReadString()
        {
            _index++; // '"'
            var builder = new StringBuilder();
            while (true)
            {
                if (AtEnd) throw Fail("字符串未闭合");
                var current = text[_index];
                if (current == '"')
                {
                    _index++;
                    return builder.ToString();
                }

                if (current == '\\')
                {
                    _index++;
                    if (AtEnd) throw Fail("转义序列未完成");
                    var escape = text[_index++];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u': builder.Append(ReadUnicodeEscape()); break;
                        default: throw Fail($"非法转义 '\\{escape}'");
                    }

                    continue;
                }

                if (current < 0x20) throw Fail($"字符串中出现未转义的控制字符 U+{(int)current:X4}");
                builder.Append(current);
                _index++;
            }
        }

        private string ReadUnicodeEscape()
        {
            var code = ReadHex4();
            if (char.IsHighSurrogate(code))
            {
                // 成对的 UTF-16 转义合并为一个码点；孤立代理项保留给 L0-UNICODE 检查报告。
                if (_index + 1 < text.Length && text[_index] == '\\' && text[_index + 1] == 'u')
                {
                    var saved = _index;
                    _index += 2;
                    var low = ReadHex4();
                    if (char.IsLowSurrogate(low)) return new string([code, low]);
                    _index = saved;
                }

                return code.ToString();
            }

            return code.ToString();
        }

        private char ReadHex4()
        {
            if (_index + 4 > text.Length) throw Fail("\\u 转义不足 4 位");
            var hex = text.Substring(_index, 4);
            if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                throw Fail($"非法 \\u 转义 '{hex}'");
            _index += 4;
            return (char)code;
        }

        private WireFaultException Fail(string message) =>
            new("L0-JSON", $"{what}: JSON 解析失败: {message}（位置 {_index}）");
    }
}
