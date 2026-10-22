namespace CoreTheSpire.Engine;

public enum Binary64Op { FromNumber, Add, Subtract, Multiply, Divide, Sqrt, Log, Sin, RoundToInt, Less, LessOrEqual }

/// <summary>Pure binary64 arithmetic, encoded as signed IEEE754 bits in an Int slot.</summary>
public sealed class Binary64Expr(Binary64Op op, params Expr[] arguments) : Expr
{
    public Binary64Op Op { get; } = op;
    public IReadOnlyList<Expr> Arguments { get; } = Array.AsReadOnly(arguments.ToArray());
    public override string Primitive => "expr.binary64";
    public override IReadOnlyList<IrNode> Children => Arguments;
}

public static class Binary64
{
    public static long Bits(double value) => double.IsFinite(value) ? BitConverter.DoubleToInt64Bits(value) :
        throw Faults.Range("binary64 result must be finite");

    public static double Read(long bits)
    {
        var value = BitConverter.Int64BitsToDouble(bits);
        if (!double.IsFinite(value)) throw Faults.Range("binary64 operand must be finite");
        return value;
    }

    public static long RoundToInt(double value)
    {
        var rounded = Math.Round(value, MidpointRounding.ToEven);
        if (!double.IsFinite(rounded) || rounded < long.MinValue || rounded >= 9223372036854775808d)
            throw Faults.Range("rounded binary64 value is outside Int range");
        return checked((long)rounded);
    }
}
