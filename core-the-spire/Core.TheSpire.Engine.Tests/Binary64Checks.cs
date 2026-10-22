using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class Binary64Checks
{
    private static RuleSet Rules(string expression) => new RuleSetBuilder().Add(EngineDslCompiler.Compile($$"""
        using namespace binary64-test; dsl_version = "core/0.3"
        entity Probe { fields = { Result = { type = Int } } }
        procedure Read { pure = true result = Int body = { return = { value = {{expression}} } } }
        procedure Setup { locals = { item = Binding } body = {
          create = { type = Probe scope = frameScope() into = item fields = { Result = pureCall(Read) } }
        } } start = [Setup]
        """)).Build();

    public static void Run()
    {
        Check.Run("binary64 text and typed IR preserve precision, signed bits and pure evaluation", () =>
        {
            foreach (var (expression, expected) in new (string, long)[]
            {
                ("float64Add(float64(16777216), float64(1))", 4715268810125344768L),
                ("float64Subtract(float64(1), float64(2))", -4616189618054758400L),
                ("float64Multiply(float64(0.5), float64(8))", 4616189618054758400L),
                ("float64Divide(float64(1), float64(4))", 4598175219545276416L),
                ("float64Sqrt(float64(4))", 4611686018427387904L),
                ("float64Log(float64(1))", 0L),
                ("float64Sin(float64(0))", 0L),
                ("float64RoundToInt(float64(2.5))", 2L),
                ("float64RoundToInt(float64(3.5))", 4L),
                ("float64RoundToInt(float64(-2.5))", -2L),
                ("float64RoundToInt(float64(-3.5))", -4L),
                ("float64RoundToInt(float64(-9223372036854775808))", long.MinValue),
            })
            {
                var rules = Rules(expression);
                var session = new Session(rules, 1, ["player"]); session.Start();
                Check.Equal(SessionStatus.Running, session.Status, expression);
                var probe = session.State.AllInstances(rules.TypeByQualifiedId("binary64-test:Probe").Id).Single();
                Check.Equal(expected, session.State.GetField(probe, 0).AsInt(), expression);
                Check.Equal(0L, session.Random.TotalDraws, "pure arithmetic consumes no randomness");
            }
            var text = Rules("float64(2)");
            Check.True(text.Identity != Rules("float64Sqrt(float64(4))").Identity, "operator affects rule identity");
            Check.True(text.Identity != Rules("float64(3)").Identity, "operand affects rule identity");
            Check.True(Binary64.Read(-4616189618054758400L) == -1d, "signed bit transport");
            Check.True(Binary64.Read(Binary64.Bits(-0d)) == 0d, "finite signed zero");
        });
        Check.Run("binary64 rejects nonfinite operands, undefined results and integer overflow", () =>
        {
            foreach (var expression in new[]
            {
                "float64Sqrt(float64(-1))", "float64Log(float64(0))",
                "float64Divide(float64(1), float64(0))", "float64Add(9218868437227405312, float64(1))",
                "float64Sin(9221120237041090560)",
                "float64RoundToInt(float64(9223372036854775807))",
            })
            {
                var session = new Session(Rules(expression), 1, ["player"]);
                Check.Fault(EngineFaultCode.ValueOutOfRange, session.Start, expression);
                Check.Equal(0L, session.Random.TotalDraws, "invalid arithmetic consumes no RNG");
            }
            Check.Throws<Exception>(() => Rules("float64(true)"), "numeric input required");
            Check.Throws<Exception>(() => Rules("float64Sin(1.5)"), "bits require Int");
            Check.Throws<Exception>(() => Rules("float64Sqrt(float64(4), float64(9))"), "extra operand rejected");
            Check.Throws<Exception>(() => Rules("float64Add(float64(1))"), "missing operand rejected");
        });
    }
}
