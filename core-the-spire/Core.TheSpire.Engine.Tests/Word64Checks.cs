using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class Word64Checks
{
    public static void Run()
    {
        Check.Run("WORD64 UTF8 hash, explicit wrapping arithmetic and high-bit stream seed preserve all bits", () =>
        {
            const string text = """
                using namespace words; dsl_version = "core/0.3"
                entity Probe { fields = { Hash = { type = Int } } }
                procedure Setup { locals = { probe = Binding } body = {
                  create = { type = Probe scope = frameScope() into = probe fields = { Hash = hash64(gameSeed()) } }
                  if = { condition = not(equal(uint64Add(-1, 1), 0)) then = { fail = { message = "unsigned overflow" } } }
                  if = { condition = not(equal(uint64Add(9223372036854775807, 1), -9223372036854775808)) then = { fail = { message = "signed boundary" } } }
                  if = { condition = startsWith("Old", "old") then = { fail = { message = "prefix must be ordinal" } } }
                  if = { condition = not(and(startsWith("old😀", "old"), equal(substring("old😀", 3), "😀"))) then = { fail = { message = "UTF16 substring" } } }
                  if = { condition = not(equal(substring("old", 3), "")) then = { fail = { message = "empty suffix" } } }
                  seedStream64 = { stream = "full-word" seed = -1 }
                } }
                start = [Setup]
                """;
            var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile(text)).Build();
            foreach (var vector in Word64Vectors.All)
            {
                var session = new Session(rules, 1, ["player"], gameSeed: vector.Text); session.Start();
                Check.Equal(SessionStatus.Running, session.Status, "typed word expressions execute");
                var probe = session.State.AllInstances(rules.TypeByQualifiedId("words:Probe").Id).Single();
                Check.Equal(vector.Hash, unchecked((ulong)session.State.GetField(probe, 0).AsInt()), "native source XxHash64 UTF8 vector");
                Check.Equal(ulong.MaxValue, session.Random.Capture().ExplicitSeeds["full-word"], "complete unsigned seed");
                Check.Equal(0L, session.Random.TotalDraws, "pure expressions and seed declarations consume no words");
            }
            Check.Equal(0xef46db3751d8e999UL, unchecked((ulong)Word64.HashUtf8("")), "standard xxHash64 empty golden vector");
            Check.Equal(0x44bc2cf5ad770999UL, unchecked((ulong)Word64.HashUtf8("abc")), "standard xxHash64 abc golden vector");
            RuleSet Compile(string source) => new RuleSetBuilder().Add(EngineDslCompiler.Compile(source)).Build();
            Check.True(rules.Identity != Compile(text.Replace("seedStream64", "seedStream")).Identity, "seed interpretation participates in rules identity");
            Check.True(rules.Identity != Compile(text.Replace("hash64(gameSeed())", "uint64Add(hash64(gameSeed()), 0)")).Identity, "word operation participates in identity");
        });
        Check.Run("WORD64 malformed operands and pure writes are rejected while invalid slices never sample", () =>
        {
            foreach (var expression in new[] { "hash64(1)", "uint64Add(1,true)", "substring(1,0)", "substring(\"x\",true)", "startsWith(\"x\",1)" })
                Check.Throws<EngineFaultException>(() => new RuleSetBuilder().Add(EngineDslCompiler.Compile(
                    "using namespace invalid; dsl_version = \"core/0.3\" procedure Setup { body = { if = { condition = equal(" + expression + ", " + expression + ") then = {} } } }")).Build(), "wrong scalar operands");
            foreach (var start in new[] { "-1", "2", "9223372036854775807" })
            {
                var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile(
                    "using namespace invalid; dsl_version = \"core/0.3\" procedure Setup { body = { seedStream64 = { stream = substring(\"x\"," + start + ") seed = -1 } } } start = [Setup]")).Build();
                var session = new Session(rules, 1, ["player"]);
                Check.Fault(EngineFaultCode.ValueOutOfRange, session.Start, "invalid UTF16 slice");
                Check.Equal(0, session.Random.Capture().ExplicitSeeds.Count, "invalid stream expression changes no seed");
                Check.Equal(0L, session.Random.TotalDraws, "invalid slice consumes no words");
            }
            Check.Throws<EngineFaultException>(() => new RuleSetBuilder().Add(EngineDslCompiler.Compile(
                "using namespace invalid; dsl_version = \"core/0.3\" procedure P { pure = true body = { seedStream64 = { stream = \"x\" seed = -1 } } }")).Build(), "pure procedures cannot configure streams");
        });
    }
}
