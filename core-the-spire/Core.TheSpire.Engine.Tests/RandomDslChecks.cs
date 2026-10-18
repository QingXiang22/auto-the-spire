using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class RandomDslChecks
{
    public static void Run()
    {
        Check.Run("RNG DSL typed seed/float operations execute without decimal quantization", () =>
        {
            var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile("""
                using namespace random-test; dsl_version = "core/0.3"
                procedure Setup { locals = { roll = Int } body = {
                  if = { condition = not(equal(hash32(gameSeed()), 3980066918)) then = { fail = { message = "UTF16 hash mismatch" } } }
                  seedStream = { stream = "source" seed = 12345 }
                  sampleFloat32 = { stream = "source" into = roll }
                  if = { condition = not(float32Less(roll, float32(1))) then = { fail = { message = "float bound" } } }
                  if = { condition = equal(float32Add(float32(0.03), float32(-0.05)), float32(-0.02)) then = { fail = { message = "binary32 addition incorrectly became exact decimal" } } }
                } }
                start = [Setup]
                """)).Build();
            var s = new Session(rules, 1, ["player"], randomAlgorithmId: RandomService.VanillaAlgorithmId, randomDrawProfileId: RandomService.VanillaDrawProfileId, gameSeed: "A😀B");
            s.Start(); Check.Equal(1L, s.Random.DrawCount("source"), "one float consumes one raw word");
            Check.True(s.Identity != new Session(rules, 1, ["player"], randomAlgorithmId: RandomService.VanillaAlgorithmId, randomDrawProfileId: RandomService.VanillaDrawProfileId, gameSeed: "other").Identity, "immutable textual seed participates in session identity");
        });
        Check.Run("RNG snapshots preserve unconsumed stream seeds and restore exact positions", () =>
        {
            var a = new RandomService(1, RandomService.VanillaAlgorithmId, RandomService.VanillaDrawProfileId);
            a.SeedStream("later", 12345); var snapshot = a.Capture();
            var expected = a.DrawInt("later", 0, 100);
            var b = new RandomService(1, RandomService.VanillaAlgorithmId, RandomService.VanillaDrawProfileId);
            b.SeedStream("later", 7); b.Restore(snapshot);
            Check.Equal(expected, b.DrawInt("later", 0, 100), "unconsumed explicit seed restored");
            Check.Equal(a.Capture().Streams.Single().StateFingerprintForTest(), b.Capture().Streams.Single().StateFingerprintForTest(), "backend position");
            Check.Fault(EngineFaultCode.Unsupported, () => b.SeedStream("later", 1), "cannot reseed consumed stream");
            var untouched = new RandomService(1); untouched.SeedStream("one", 1);
            Check.Fault(EngineFaultCode.Unsupported, () => untouched.SeedStream("one", 2), "duplicate declaration rejected before first draw");
        });
        Check.Run("RNG vanilla bool follows NextInt(2)==0 and default mapping remains unchanged", () =>
        {
            foreach (var vector in VanillaRandomVectors.All)
            {
                var source = new RandomService(1, RandomService.VanillaAlgorithmId, RandomService.VanillaDrawProfileId); source.SeedStream("v", vector.Seed);
                foreach (var word in vector.RawFirst8) Check.Equal((word >> 63) == 0, source.DrawBool("v"), "source high bit polarity");
            }
        });
        Check.Run("RNG DSL rejects side effects from pure procedures and wrong argument types", () =>
        {
            foreach (var body in new[] { "seedStream = { stream = \"x\" seed = 1 }", "sampleFloat32 = { stream = \"x\" into = v }" })
                Check.Throws<EngineFaultException>(() => new RuleSetBuilder().Add(EngineDslCompiler.Compile("using namespace bad; dsl_version = \"core/0.3\" procedure P { pure = true locals = { v = Int } body = { " + body + " } }")).Build(), "pure random instruction rejected");
            foreach (var expression in new[] { "hash32(1)", "float32Add(1, true)", "float32(true)" })
                Check.Throws<EngineFaultException>(() => new RuleSetBuilder().Add(EngineDslCompiler.Compile("using namespace bad; dsl_version = \"core/0.3\" procedure P { locals = { v = Int } body = { let = { into = v value = " + expression + " } } }")).Build(), "wrong operand type rejected");
        });
        Check.Run("RNG DSL invalid seed and empty sampling range fail before consuming random words", () =>
        {
            foreach (var body in new[] { "seedStream = { stream = \"source\" seed = -1 }", "sampleInt = { stream = \"source\" min = 1 max = 1 into = value }" })
            {
                var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile("using namespace invalid; dsl_version = \"core/0.3\" procedure Setup { locals = { value = Int } body = { " + body + " } } start = [Setup]")).Build();
                var session = new Session(rules, 1, ["player"]);
                Check.Fault(EngineFaultCode.ValueOutOfRange, session.Start, "invalid random operation");
                Check.Equal(0L, session.Random.TotalDraws, "no random consumption");
                Check.Equal(0, session.Random.Capture().ExplicitSeeds.Count, "no partial stream configuration");
            }
        });
    }
    private static string StateFingerprintForTest(this RandomStreamSnapshot s) => string.Join(",", s.BackendState!);
}
