using System.Globalization;
using System.Text;

namespace NetTheSpire.Contracts;

/// <summary>
/// Python <c>repr()</c> 的 JSON 值子集。存在的唯一理由：L1 结构诊断文本必须与
/// Python 参考实现 <c>tools/contract-check/jsonschema_min.py</c> 的 f-string 逐字相同
/// （它用 <c>!r</c> 打印 const/enum/pattern），C# 端不因此产生第二套消息形状。
/// 只覆盖 wire 值：None/True/False/整数/字符串/数组/对象；浮点字面量按原文输出。
/// </summary>
public static class PythonRepr
{
    /// <summary>f-string 里 <c>{value}</c> 的语义：字符串不加引号，其余用 repr。</summary>
    public static string Declared(WireValue value) =>
        value is WireString text ? text.Value : Of(value);

    public static string Of(WireValue value)
    {
        switch (value)
        {
            case WireNull:
                return "None";
            case WireBool boolean:
                return boolean.Value ? "True" : "False";
            case WireInteger integer:
                return integer.Digits;
            case WireFloatLiteral literal:
                return literal.Raw;
            case WireString text:
                return String(text.Value);
            case WireArray array:
                return "[" + string.Join(", ", array.Items.Select(Of)) + "]";
            case WireObject obj:
                return "{" + string.Join(", ", obj.Members.Select(member => $"{String(member.Key)}: {Of(member.Value)}")) + "}";
            default:
                throw new WireFaultException("python-repr", $"不支持的值类型: {value.GetType().Name}");
        }
    }

    /// <summary>Python 3 的 str repr：默认单引号，含单引号且不含双引号时改用双引号。</summary>
    private static string String(string text)
    {
        var quote = text.Contains('\'') && !text.Contains('"') ? '"' : '\'';
        var builder = new StringBuilder();
        builder.Append(quote);
        foreach (var current in text)
        {
            switch (current)
            {
                case '\\': builder.Append("\\\\"); continue;
                case '\n': builder.Append("\\n"); continue;
                case '\r': builder.Append("\\r"); continue;
                case '\t': builder.Append("\\t"); continue;
            }

            if (current == quote)
            {
                builder.Append('\\').Append(current);
                continue;
            }

            if (char.IsControl(current))
            {
                builder.Append("\\x").Append(((int)current).ToString("x2", CultureInfo.InvariantCulture));
                continue;
            }

            builder.Append(current);
        }

        builder.Append(quote);
        return builder.ToString();
    }
}
