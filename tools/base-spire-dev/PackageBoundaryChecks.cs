using CoreTheSpire;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunPackageOnly(DslPackage package)
    {
        var count = 0;
        void Case(string name, Action body) { body(); count++; Console.WriteLine("ok   " + name); }
        using var minimal = new MinimalPackage();
        Case("PACKAGE minimal package loads without production content", () =>
        {
            var loaded = minimal.Load();
            Equal("minimal", loaded.Id, "minimal package identity");
            Equal("1.0", loaded.Version, "minimal package version");
            Start(loaded);
        });
        Case("DB-A01/A09: package rejects executable files, JSON programs, unknown and duplicate fields", () =>
        {
            RejectCopy(minimal.Root, c => File.WriteAllText(c.Path("hidden.cs"), "class Hidden {}"));
            RejectCopy(minimal.Root, c => c.EditJson("package.json", root => root["effects"] = new System.Text.Json.Nodes.JsonArray()));
            RejectCopy(minimal.Root, c => c.EditJson("cards/catalog.json", root => root["unknown"] = 1));
            RejectCopy(minimal.Root, c => File.WriteAllText(c.Path("package.json"), "{\"format\":\"duplicate\"," + File.ReadAllText(c.Path("package.json")).TrimStart()[1..]));
        });
        Case("package instruction budget validates configuration and is enforced by its sessions", () =>
        {
            foreach (var invalid in new[] { "0", "-1", "1.5", "9223372036854775808", "true", "null", "\"100\"" })
                RejectCopy(minimal.Root, c => c.EditJson("package.json", root => root["instruction_budget"] = System.Text.Json.Nodes.JsonNode.Parse(invalid)));
            Equal(SessionBudget.Default.MaxInstructions, minimal.Load().CreateSession(1, ["player"]).Budget.MaxInstructions, "omitted budget uses the engine default");
            using var limited = new MinimalPackage(instructionBudget: 1);
            var session = limited.Load().CreateSession(1, ["player"]);
            Throws<EngineFaultException>(session.Start);
            Equal(EngineFaultCode.BudgetExceeded, session.Fault!.Code, "configured limit is enforced during actual execution");
        });
        Case("DB-A03/A04: package rejects escaping references, missing and unreferenced DSL files", () =>
        {
            RejectCopy(minimal.Root, c => c.EditJson("package.json", root => root["entry"] = "../outside.spire"));
            RejectCopy(minimal.Root, c => File.WriteAllText(c.Path("unused.spire"), "using namespace minimal; dsl_version = \"core/0.3\""));
            RejectCopy(minimal.Root, c => File.WriteAllText(c.Path("base.spire"), File.ReadAllText(c.Path("base.spire")).Replace("\"content/probe.spire\"", "\"../outside.spire\"", StringComparison.Ordinal)));
        });
        Case("DB-R05: content modules accept owned declarations and reject package properties, unknown arguments and executable expressions", () =>
        {
            const string content = "content/probe.spire";
            using var owned = new Copy(minimal.Root);
            File.AppendAllText(owned.Path(content), "\nprocedure OwnedEffect { body = {} }");
            Start(owned.Load());
            RejectCopy(minimal.Root, c => File.AppendAllText(c.Path(content), "\nstart = []"));
            using var unknown = new Copy(minimal.Root);
            File.WriteAllText(unknown.Path(content), File.ReadAllText(unknown.Path(content)).Replace("amount = 6", "unlisted = 6", StringComparison.Ordinal));
            Throws<DslSourceException>(() => unknown.Load());
            using var expression = new Copy(minimal.Root);
            File.WriteAllText(expression.Path(content), File.ReadAllText(expression.Path(content)).Replace("amount = 6", "amount = add(3, 3)", StringComparison.Ordinal));
            Throws<DslSourceException>(() => expression.Load());
        });
        Case("LIFECYCLE runtime catalogs reject source records, null definitions and invalid mappings", () =>
        {
            foreach (var format in new[] { "engine-dsl/source-catalog@1", "engine-dsl/source-data@1" })
                RejectCopy(minimal.Root, c => c.EditJson("cards/catalog.json", j => j["format"] = format));
            RejectCopy(minimal.Root, c => c.EditJson("cards/catalog.json", j => j["source_baseline"] = "old-progress"));
            RejectCopy(minimal.Root, c => c.EditJson("cards/catalog.json", j => j["entries"] = "claimed complete"));
            RejectCopy(minimal.Root, c => c.EditJson("cards/catalog.json", j => j["entries"] = new System.Text.Json.Nodes.JsonArray { new System.Text.Json.Nodes.JsonObject { ["id"] = "minimal:probe", ["definition"] = null } }));
            RejectCopy(minimal.Root, c => c.EditJson("cards/catalog.json", j => j["entries"] = new System.Text.Json.Nodes.JsonArray { new System.Text.Json.Nodes.JsonObject { ["id"] = "minimal:probe", ["definition"] = "public/metadata.json" } }));
            RejectCopy(minimal.Root, c => c.EditJson("cards/catalog.json", j => j["entries"] = new System.Text.Json.Nodes.JsonArray { new System.Text.Json.Nodes.JsonObject { ["id"] = "minimal:probe", ["definition"] = "content/probe.spire" }, new System.Text.Json.Nodes.JsonObject { ["id"] = "minimal:probe", ["definition"] = "content/probe.spire" } }));
        });
        Case("LIFECYCLE registration rejects unknown and missing source mappings", () =>
        {
            using var unknown = new Copy(package.Root);
            unknown.EditJson("cards/catalog.json", j => j["entries"]![0]!["id"] = "base-spire:not-a-source-card");
            Throws<FormatException>(() => ContentRegistration.Check(unknown.Load()));
            using var missing = new Copy(package.Root);
            missing.EditJson("cards/catalog.json", j => j["entries"]!.AsArray().RemoveAt(0));
            Throws<FormatException>(() => ContentRegistration.Check(missing.Load()));
        });
        Console.WriteLine($"All {count} package boundary checks passed on minimal package data plus registration mapping; content behavior is not executed here.");
        return 0;
    }

    private sealed class MinimalPackage : IDisposable
    {
        private readonly long? instructionBudget;
        internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "base-minimal-package-" + Guid.NewGuid());

        internal MinimalPackage(long? instructionBudget = null)
        {
            this.instructionBudget = instructionBudget;
            Directory.CreateDirectory(Path("content"));
            Directory.CreateDirectory(Path("cards"));
            Directory.CreateDirectory(Path("public"));
            Write("base.spire", """
                using namespace minimal;
                dsl_version = "core/0.3"
                version = "1.0"
                includes = ["content/probe.spire"]
                start = [ProbeStart]
                procedure ProbeStart { locals = { probe = Int other = Int } body = { let = { into = probe value = 1 } let = { into = other value = 2 } } }
                template ProbeTemplate { parameters = { amount = { type = Int } } body = { } }
                """);
            Write("content/probe.spire", """
                using namespace minimal;
                dsl_version = "core/0.3"
                use ProbeUse { template = ProbeTemplate args = { amount = 6 } }
                """);
            Write("cards/catalog.json", """{"format":"engine-dsl/content-catalog@1","kind":"card","entries":[]}""");
            Write("public/metadata.json", "{}");
            Write("package.json", instructionBudget is null ? """
                {
                  "format": "engine-dsl/package@1",
                  "id": "minimal",
                  "version": "1.0",
                  "language": "core/0.3",
                  "entry": "base.spire",
                  "public_metadata": "public/metadata.json",
                  "default_scope": "minimal",
                  "supported_scopes": ["minimal"],
                  "unsupported_scopes": {},
                  "catalogs": ["cards/catalog.json"],
                  "random_profile": "engine-default"
                }
                """ : $$"""
                {
                  "format": "engine-dsl/package@1",
                  "id": "minimal",
                  "version": "1.0",
                  "language": "core/0.3",
                  "entry": "base.spire",
                  "public_metadata": "public/metadata.json",
                  "default_scope": "minimal",
                  "supported_scopes": ["minimal"],
                  "unsupported_scopes": {},
                  "catalogs": ["cards/catalog.json"],
                  "random_profile": "engine-default",
                  "instruction_budget": {{instructionBudget}}
                }
                """);
        }

        internal string Path(string relative) => System.IO.Path.Combine(Root, relative);
        internal DslPackage Load() => DslPackage.Load(Path("package.json"));
        private void Write(string relative, string text) => File.WriteAllText(Path(relative), text.TrimStart() + "\n");
        public void Dispose()
        {
            var path = System.IO.Path.GetFullPath(Root);
            if (System.IO.Path.GetDirectoryName(path) != System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()))
                || !System.IO.Path.GetFileName(path).StartsWith("base-minimal-package-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected minimal package cleanup path.");
            Directory.Delete(path, true);
        }
    }
}
