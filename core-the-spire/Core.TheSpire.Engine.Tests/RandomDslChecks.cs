using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class RandomDslChecks
{
    public static void Run()
    {
        Check.Run("RNG DSL float sum matches source LINQ double accumulation before one float rounding", () =>
        {
            var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile("""
                using namespace float-sum; dsl_version = "core/0.3"
                entity Item { fields = { Bits = { type = Int } } }
                procedure Setup { locals = { item = Binding } body = {
                  if = { condition = not(equal(aggregate(type(Item), SumFloat32, i, field(i, Item.Bits)), float32(0))) then = { fail = { message = "empty sum" } } }
                  create = { type = Item scope = frameScope() into = item fields = { Bits = float32(16777216) } }
                  repeat = { count = 2 body = { create = { type = Item scope = frameScope() into = item fields = { Bits = float32(1) } } } }
                  if = { condition = not(equal(aggregate(type(Item), SumFloat32, i, field(i, Item.Bits)), float32(16777218))) then = { fail = { message = "sum rounded at every add" } } }
                } } start = [Setup]
                """)).Build();
            var s = new Session(rules, 1, ["player"]); s.Start();
            Check.Equal(SessionStatus.Running, s.Status, "source sum retains both small weights");
            Check.Equal(0L, s.Random.TotalDraws, "pure sum never samples");
            Check.True(16777218f == new[] { 16777216f, 1f, 1f }.Sum(), "independent BCL Sum(float) result");
        });
        Check.Run("RNG DSL ranged binary32 scales the double before rounding and includes bounds in identity", () =>
        {
            const string source = """
                using namespace ranged; dsl_version = "core/0.3"
                entity Probe { fields = { Result = { type = Int } } }
                procedure Setup { locals = { probe = Binding } body = {
                  seedStream = { stream = "source" seed = 12345 }
                  create = { type = Probe scope = frameScope() into = probe }
                } }
                action Draw { role = player locals = { roll = Int } body = {
                  sampleFloat32 = { stream = "source" min = float32(-0.3) max = float32(3.7) into = roll }
                  set = { target = first(type(Probe)) field = Probe.Result value = roll }
                } }
                start = [Setup]
                """;
            RuleSet Rules(string text) => new RuleSetBuilder().Add(EngineDslCompiler.Compile(text)).Build();
            var rules = Rules(source);
            var s = new Session(rules, 1, ["player"], randomAlgorithmId: RandomService.VanillaAlgorithmId,
                randomDrawProfileId: RandomService.VanillaDrawProfileId, debug: new());
            s.Start();
            // Independent Python source oracle, seed 12345; indices 4,7,8 detect premature float rounding.
            long[] expected = [1076573955, 1046574956, 1080256996, 3185206742, 1073076871, 3196304185,
                1051566008, 1063393938, 1067398887, 1079372477, 1077212094, 1072473345];
            var probe = s.State.AllInstances(rules.TypeByQualifiedId("ranged:Probe").Id).Single();
            foreach (var bits in expected)
            {
                s.Submit(s.LegalActions(s.Participant(0)).Single().Definition, s.Participant(0), []);
                Check.Equal(bits, s.State.GetField(probe, 0).AsInt(), "source ranged float bits");
                var sample = s.Debug!.Records.Last(r => r.Random is not null).Random!.Float32!;
                Check.Equal(3197737370L, sample.MinimumBits, "evaluated -0.3 binary32 bound");
                Check.Equal(1080872141L, sample.MaximumBits, "evaluated 3.7 binary32 bound");
                Check.Equal(bits, sample.ResultBits, "diagnostic result matches independent oracle");
            }
            Check.Equal(12L, s.Random.DrawCount("source"), "one raw word for each float");
            var other = new Session(Rules(source.Replace("float32(3.7)", "float32(7.7)")), 1, ["player"],
                randomAlgorithmId: RandomService.VanillaAlgorithmId, randomDrawProfileId: RandomService.VanillaDrawProfileId, debug: new());
            other.Start(); other.Submit(other.LegalActions(other.Participant(0)).Single().Definition, other.Participant(0), []);
            var original = s.Debug!.Records.First(r => r.Random is not null).Random!;
            var changed = other.Debug!.Records.Single(r => r.Random is not null).Random!;
            Check.Equal(original.Result, changed.Result, "same raw double for different evaluated ranges");
            Check.True(original.Float32!.MaximumBits != changed.Float32!.MaximumBits
                && original.Float32.ResultBits != changed.Float32.ResultBits, "float metadata distinguishes actual range and scaled result");
            Check.True(rules.Identity != Rules(source.Replace("float32(3.7)", "float32(3.8)")).Identity, "range affects rules identity");
            Check.True(rules.Identity != Rules(source.Replace("float32(-0.3)", "float32(-0.4)")).Identity, "minimum affects rules identity");
        });
        Check.Run("RNG DSL ranged binary32 validates before sampling and equal bounds still consume", () =>
        {
            foreach (var (min, max, valid) in new[] { ("float32(2)", "float32(2)", true),
                ("float32(2)", "float32(1)", false), ("2143289344", "float32(1)", false),
                ("4286578687", "2139095039", false), ("-1", "float32(1)", false) })
            {
                var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile($$"""
                    using namespace bounds; dsl_version = "core/0.3"
                    procedure Setup { locals = { v = Int } body = {
                      sampleFloat32 = { stream = "s" min = {{min}} max = {{max}} into = v }
                      if = { condition = not(equal(v, float32(2))) then = { fail = { message = "equal bounds" } } }
                      if = { condition = not(equal(float32Subtract(float32(0.5), float32(0.25)), float32(0.25))) then = { fail = { message = "subtract" } } }
                    } } start = [Setup]
                    """)).Build();
                var s = new Session(rules, 1, ["player"], debug: new());
                if (valid) s.Start();
                else Check.Fault(EngineFaultCode.ValueOutOfRange, s.Start, "invalid range before RNG");
                Check.Equal(valid ? 1L : 0L, s.Random.TotalDraws, "invalid input never samples");
                var samples = s.Debug!.Records.Where(r => r.Random is not null).ToArray();
                Check.Equal(valid ? 1 : 0, samples.Length, "only successful float draws are recorded");
                if (valid) Check.Equal(new Float32SampleDetails(1073741824, 1073741824, 1073741824),
                    samples.Single().Random!.Float32!, "equal bounds retain their real result and consumption");
            }
        });
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
            foreach (var expression in new[] { "hash32(1)", "float32Add(1, true)", "float32Subtract(1, true)", "float32(true)" })
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
