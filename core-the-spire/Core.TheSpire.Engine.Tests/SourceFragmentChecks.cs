using CoreTheSpire;
using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class SourceFragmentChecks
{
    public static void Run()
    {
        using var files = new Sources();
        Check.Run("DSL includes preserve flat-source identity and reject duplicate declarations", () =>
        {
            const string model = "entity Unit { fields = { Hp = { type = Int default = 3 } } }";
            files.Write("entry.spire", "includes = [\"one.spire\"]");
            files.Write("one.spire", model);
            var flat = new RuleSetBuilder().Add(EngineDslCompiler.Compile("using namespace fragments; dsl_version = \"core/0.3\" " + model)).Build();
            Check.Equal(flat.Identity, EngineDslCompiler.LoadFiles(files.Path("entry.spire")).Identity, "file layout does not change rule semantics");
            files.Write("two.spire", model);
            files.Write("entry.spire", "includes = [\"one.spire\", \"two.spire\"]");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.LoadFiles(files.Path("entry.spire")), "different files cannot silently override a symbol");
        });
        Check.Run("DSL includes bind cross-file types, templates and a single explicit startup", () =>
        {
            files.Write("entry.spire", "includes = [\"rules.spire\", \"types.spire\", \"types.spire\"] start = [Setup]");
            files.Write("types.spire", "entity Counter { fields = { Amount = { type = Int default = 0 } } }");
            files.Write("rules.spire", "procedure Setup { locals = { c = Binding } body = { create = { type = Counter scope = frameScope() into = c fields = { Amount = 7 } } } }");
            var rules = EngineDslCompiler.LoadFiles(files.Path("entry.spire"));
            var session = new Session(rules, 42, ["player"]); session.Start();
            var type = rules.TypeByQualifiedId("fragments:Counter");
            var instances = session.State.AllInstances(type.Id);
            Check.Equal(1, instances.Count, "duplicate inclusion neither redeclares nor starts twice");
            Check.Equal(7L, session.State.GetField(instances[0], type.FieldIndex("Amount")).AsInt(), "forward-bound field initialization");
        });
        Check.Run("DSL includes reject cycles while deduplicating a diamond", () =>
        {
            files.Write("entry.spire", "includes = [\"left.spire\", \"right.spire\"]");
            files.Write("left.spire", "includes = [\"types.spire\"]");
            files.Write("right.spire", "includes = [\"types.spire\"]");
            EngineDslCompiler.LoadFiles(files.Path("entry.spire"));
            files.Write("types.spire", "includes = [\"entry.spire\"]");
            var error = Check.Throws<DslSourceException>(() => EngineDslCompiler.LoadFiles(files.Path("entry.spire")), "cycle is rejected");
            Check.True(error.Message.Contains("Cyclic", StringComparison.Ordinal), "explicit cycle diagnostic");
        });
        Check.Run("DSL includes reject foreign namespaces and fragment package properties", () =>
        {
            files.Write("entry.spire", "includes = [\"types.spire\"]");
            File.WriteAllText(files.Path("types.spire"), "using namespace foreign; dsl_version = \"core/0.3\" entity Thing {}");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.LoadFiles(files.Path("entry.spire")), "foreign namespace");
            files.Write("types.spire", "start = []");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.LoadFiles(files.Path("entry.spire")), "fragment cannot hide package startup");
        });
        Check.Run("DSL includes preserve failing fragment source locations and reject missing files", () =>
        {
            files.Write("entry.spire", "includes = [\"types.spire\"]");
            files.Write("types.spire", "procedure Broken { body = { call = { procedure = Missing } } }");
            var error = Check.Throws<DslSourceException>(() => EngineDslCompiler.LoadFiles(files.Path("entry.spire")), "missing symbol");
            Check.Equal(files.Path("types.spire"), error.Location.Source, "diagnostic points to the authored fragment");
            files.Write("entry.spire", "includes = [\"missing.spire\"]");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.LoadFiles(files.Path("entry.spire")), "missing fragment");
        });
        Check.Run("DSL Compile and BindInto never resolve includes through implicit filesystem IO", () =>
        {
            const string source = "using namespace fragments; dsl_version = \"core/0.3\" includes = [\"types.spire\"]";
            Check.Throws<DslSourceException>(() => EngineDslCompiler.Compile(source), "Compile rejects includes");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.BindInto(source, new EnginePackageBuilder("fragments")), "BindInto rejects includes");
        });
    }

    private sealed class Sources : IDisposable
    {
        private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "engine-dsl-fragments-" + Guid.NewGuid());
        public Sources() => Directory.CreateDirectory(directory);
        public string Path(string name) => System.IO.Path.Combine(directory, name);
        public void Write(string name, string body) => File.WriteAllText(Path(name), "using namespace fragments;\ndsl_version = \"core/0.3\"\n" + body);
        public void Dispose() => Directory.Delete(directory, true);
    }
}
