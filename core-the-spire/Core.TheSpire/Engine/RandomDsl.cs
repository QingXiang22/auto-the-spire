namespace CoreTheSpire.Engine;

public enum RandomValueOp { GameSeed, Hash32, Float32, Float32Add, Float32Minimum, Float32Less, Float32LessOrEqual }

public sealed class RandomValueExpr(RandomValueOp op, params Expr[] arguments) : Expr
{
    public RandomValueOp Op { get; } = op;
    public IReadOnlyList<Expr> Arguments { get; } = Array.AsReadOnly(arguments.ToArray());
    public override string Primitive => "expr.randomValue";
    public override IReadOnlyList<IrNode> Children => Arguments;
}

public sealed class SeedStreamInst(Expr stream, Expr seed) : Inst
{
    public Expr Stream { get; } = stream;
    public Expr Seed { get; } = seed;
    public override string Primitive => "inst.seedStream";
    public override IReadOnlyList<IrNode> Children => [Stream, Seed];
}

/// <summary>IEEE754 binary32 encoded as unsigned bits in an Int slot; never decimal-quantized.</summary>
public sealed class SampleFloat32Inst(Expr stream, int resultSlot) : Inst
{
    public Expr Stream { get; } = stream;
    public int ResultSlot { get; } = resultSlot;
    public override string Primitive => "inst.sampleFloat32";
    public override IReadOnlyList<IrNode> Children => [Stream];
}

public static class Binary32
{
    public static long Bits(float value) => float.IsFinite(value) ? BitConverter.SingleToUInt32Bits(value) :
        throw Faults.Range("binary32 result must be finite");
    public static float Read(long bits)
    {
        if (bits < 0 || bits > uint.MaxValue) throw Faults.Range("binary32 bits must be an unsigned 32-bit value");
        var value = BitConverter.UInt32BitsToSingle((uint)bits);
        if (!float.IsFinite(value)) throw Faults.Range("binary32 operand must be finite");
        return value;
    }
    public static long Hash32(string text)
    {
        unchecked
        {
            var left = 352654597; var right = left;
            for (var index = 0; index < text.Length; index += 2)
            {
                left = ((left << 5) + left) ^ text[index];
                if (index == text.Length - 1) break;
                right = ((right << 5) + right) ^ text[index + 1];
            }
            return (uint)(left + right * 1566083941);
        }
    }
}
