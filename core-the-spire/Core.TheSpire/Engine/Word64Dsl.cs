using System.IO.Hashing;
using System.Text;

namespace CoreTheSpire.Engine;

public enum Word64Op { HashUtf8, Add }

/// <summary>Unsigned words encoded as signed Int bits; arithmetic is explicit modulo 2^64.</summary>
public sealed class Word64Expr(Word64Op op, params Expr[] arguments) : Expr
{
    public Word64Op Op { get; } = op;
    public IReadOnlyList<Expr> Arguments { get; } = Array.AsReadOnly(arguments.ToArray());
    public override string Primitive => "expr.word64";
    public override IReadOnlyList<IrNode> Children => Arguments;
}

public static class Word64
{
    public static long HashUtf8(string text) => unchecked((long)XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(text)));
    public static long Add(long left, long right) => unchecked(left + right);
}

public enum TextOp { StartsWith, Substring }

public sealed class TextExpr(TextOp op, Expr text, Expr argument) : Expr
{
    public TextOp Op { get; } = op;
    public Expr Text { get; } = text;
    public Expr Argument { get; } = argument;
    public override string Primitive => "expr.text";
    public override IReadOnlyList<IrNode> Children => [Text, Argument];
}
