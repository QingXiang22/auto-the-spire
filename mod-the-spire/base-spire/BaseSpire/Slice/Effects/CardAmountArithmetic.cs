using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Effects;

internal static class CardAmountArithmetic
{
    public static Expr Compile(IrScope scope, string operation, Expr[] arguments) => operation switch
    {
        "add" => scope.Add(arguments[0], arguments[1]),
        "subtract" => scope.Subtract(arguments[0], arguments[1]),
        "multiply" => scope.Multiply(arguments[0], arguments[1]),
        "divide" => scope.Divide(arguments[0], arguments[1]),
        "minimum" => scope.Minimum(arguments[0], arguments[1]),
        "maximum" => scope.Maximum(arguments[0], arguments[1]),
        _ => throw Faults.Schema($"Unknown amount operator '{operation}'"),
    };
}
