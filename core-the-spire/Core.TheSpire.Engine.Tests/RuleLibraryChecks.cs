using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class RuleLibraryChecks
{
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Simple", "game.spire");
    private static string LibrarySource => File.ReadAllText(Path.Combine(Path.GetDirectoryName(Fixture)!, "rules", "score-race.spire"));
    private static EnginePackage Library(string? source = null) => EngineDslCompiler.Compile(source ?? LibrarySource, "rules/score-race.spire");
    private static RuleSet Game(string source, EnginePackage? library = null)
    {
        library ??= Library();
        var game = EngineDslCompiler.Compile(source.Replace("imports = [\"rules/score-race.spire\"]", "dependencies = [counters]"),
            "game.spire", [library]);
        return new RuleSetBuilder().Add(library).Add(game).Build();
    }

    public static void Run()
    {
        Check.Run("LIBRARY author config imports real DSL rules and enumerates direct choices", () =>
        {
            var session = new Session(EngineDslCompiler.LoadFiles(Fixture), 42, ["player"]);
            session.Start();
            var action = session.LegalActions(session.Participant(0)).Single();
            Check.Sequence(new long[] { 1, 2 }, action.Bindings.Select(binding => binding.Single().AsInt()), "configured choices");
            Submit(session, "tutorial:Game.Add", 1);
            Check.Equal(SessionStatus.Running, session.Status, "direct choice does not suspend");
            Check.True(session.PendingDecision is null, "no extra choice request");
            Check.Equal(2L, Score(session, "tutorial:Game.PlayerView"), "selected +2");
            Submit(session, "tutorial:Game.Add", 0);
            Check.Equal(SessionStatus.Terminal, session.Status, "reached target");
            Check.Equal(3L, session.Terminal!.Fields.Single().AsInt(), "terminal score");
        });

        Check.Run("LIBRARY editing only template arguments changes legal choices and time to terminal", () =>
        {
            var source = File.ReadAllText(Fixture).Replace("target = 3", "target = 5").Replace("increments = [1, 2]", "increments = [2, 4]");
            var rules = Game(source);
            Check.True(rules.Identity != Game(File.ReadAllText(Fixture)).Identity, "parameters change semantic identity");
            var session = new Session(rules, 42, ["player"]); session.Start();
            Check.Sequence(new long[] { 2, 4 }, session.LegalActions(session.Participant(0)).Single().Bindings.Select(binding => binding.Single().AsInt()), "edited choices");
            Submit(session, "tutorial:Game.Add", 1);
            Check.Equal(SessionStatus.Running, session.Status, "4 has not reached edited goal 5");
            Submit(session, "tutorial:Game.Add", 0);
            Check.Equal(6L, session.Terminal!.Fields.Single().AsInt(), "second choice finishes at 6");
        });

        Check.Run("LIBRARY repeated instances isolate types and bind helpers in the library namespace", () =>
        {
            var rules = Game("""
                using namespace tutorial;
                dsl_version = "core/0.2" dependencies = [counters]
                entity Counter { fields = { Score = { type = Int default = 999 } } }
                procedure BelowTarget {
                    parameters = { score = Int target = Int } pure = true result = Bool
                    body = { return = { value = false } }
                }
                use Left { template = counters:ScoreRace args = { target = 10 } }
                use Right { template = counters:ScoreRace args = { target = 10 increments = [3] } }
                """);
            var session = new Session(rules, 1, ["player"]); session.Start();
            Check.Equal(2, session.LegalActions(session.Participant(0)).Count, "library helper not captured by false local helper");
            Submit(session, "tutorial:Left.Add", 1);
            Check.Equal(2L, Score(session, "tutorial:Left.PlayerView"), "left changes");
            Check.Equal(0L, Score(session, "tutorial:Right.PlayerView"), "right isolated");
            Submit(session, "tutorial:Right.Add", 0);
            Check.Equal(3L, Score(session, "tutorial:Right.PlayerView"), "right configured increment");
            Check.Equal(2L, Score(session, "tutorial:Left.PlayerView"), "left preserved");
        });

        Check.Run("LIBRARY nested templates compose transitive DSL dependencies with isolated names", () =>
        {
            var library = Library();
            var pair = EngineDslCompiler.Compile("""
                using namespace pairs;
                dsl_version = "core/0.2" dependencies = [counters]
                template Pair {
                    parameters = { goal = { type = Int default = 10 } }
                    body = {
                        use Left { template = counters:ScoreRace args = { target = arg(goal) } }
                        use Right { template = counters:ScoreRace args = { target = arg(goal) } }
                    }
                }
                """, "pair.spire", [library]);
            var game = EngineDslCompiler.Compile("""
                using namespace composed;
                dsl_version = "core/0.2" dependencies = [pairs]
                use Game { template = pairs:Pair args = { goal = 20 } }
                """, "composed.spire", [library, pair]);
            var session = new Session(new RuleSetBuilder().Add(library).Add(pair).Add(game).Build(), 1, ["player"]);
            session.Start(); Submit(session, "composed:Game.Left.Add", 1);
            Check.Equal(2L, Score(session, "composed:Game.Left.PlayerView"), "nested left changes");
            Check.Equal(0L, Score(session, "composed:Game.Right.PlayerView"), "nested right isolated");
        });

        Check.Run("LIBRARY canonical exports ignore paths/comments and include unused template semantics", () =>
        {
            var first = Library();
            var same = EngineDslCompiler.Compile(LibrarySource + "\n# another comment\n", "elsewhere/library.spire");
            Check.Equal(first.ContentHash, same.ContentHash, "path/comment do not change export identity");
            var changed = Library(LibrarySource.Replace("default = 3", "default = 5"));
            Check.True(first.ContentHash != changed.ContentHash, "template default contributes to export identity");
            Check.True(new RuleSetBuilder().Add(first).Build().Identity != new RuleSetBuilder().Add(changed).Build().Identity,
                "even unused export changes package identity");
            Check.Equal(Game(File.ReadAllText(Fixture), first).Identity, Game(File.ReadAllText(Fixture), same).Identity, "reusing independent exports is stable");
        });

        Check.Run("LIBRARY invalid arguments and expanded rules report author or library locations", () =>
        {
            var source = File.ReadAllText(Fixture);
            foreach (var invalid in new[] {
                source.Replace("target = 3", "target = true"),
                source.Replace("target = 3", "target = 30000"),
                source.Replace("target = 3", "unknown = 3"),
                source.Replace("increments = [1, 2]", "increments = [0]"),
                source.Replace("increments = [1, 2]", "increments = []"),
                source.Replace("ScoreRace", "Missing") })
            {
                var error = Check.Throws<DslSourceException>(() => Game(invalid), "invalid author input");
                Check.Equal("game.spire", error.Location.Source, "author locator");
                Check.True(error.Location.Line > 1, "author line");
            }
            Check.Throws<DslSourceException>(() => Game(source.Replace("target = 3", ""),
                Library(LibrarySource.Replace("default = 3", ""))), "required argument");
            var unknown = Check.Throws<DslSourceException>(() => Library(LibrarySource.Replace("arg(target)", "arg(missing)")), "unknown placeholder even when unused");
            Check.Equal("rules/score-race.spire", unknown.Location.Source, "library locator");
            var binding = Check.Throws<DslSourceException>(() => Game(source,
                Library(LibrarySource.Replace("Counter.Score", "Counter.Missing"))), "expanded binding");
            Check.True(binding.Message.Contains("use 'Game' at game.spire"), "expanded diagnostic mentions use site");
            var recursive = """
                using namespace bad;
                dsl_version = "core/0.2"
                template Loop { body = { use Again { template = Loop } } }
                use Game { template = Loop }
                """;
            Check.Throws<DslSourceException>(() => EngineDslCompiler.Compile(recursive), "recursive expansion");
            Check.Throws<DslSourceException>(() => EngineDslCompiler.Compile(recursive.Replace("core/0.2", "core/0.1")), "version gate");
        });

        Check.Run("LIBRARY imports resolve relative paths, deduplicate files and reject cycles/missing files", () =>
        {
            var directory = Directory.CreateTempSubdirectory("core-dsl-library-");
            var root = Path.GetFullPath(directory.FullName);
            var libraryPath = Path.Combine(root, "library.spire");
            var gamePath = Path.Combine(root, "game.spire");
            try
            {
                File.WriteAllText(libraryPath, LibrarySource);
                File.WriteAllText(gamePath, File.ReadAllText(Fixture).Replace("rules/score-race.spire", "library.spire"));
                var rules = EngineDslCompiler.LoadFiles(gamePath, libraryPath, gamePath);
                Check.Equal(2, rules.Packages.Count, "same file loaded once");
                File.WriteAllText(libraryPath, "using namespace counters; dsl_version = \"core/0.2\" imports = [\"game.spire\"]");
                Check.Throws<DslSourceException>(() => EngineDslCompiler.LoadFiles(gamePath), "import cycle");
                File.WriteAllText(gamePath, "using namespace tutorial; dsl_version = \"core/0.2\" imports = [\"missing.spire\"]");
                var missing = Check.Throws<DslSourceException>(() => EngineDslCompiler.LoadFiles(gamePath), "missing import");
                Check.Equal(gamePath, missing.Location.Source, "import source locator");
            }
            finally
            {
                // Only the two files created above, then the empty directory; no recursive cleanup.
                File.Delete(libraryPath); File.Delete(gamePath); Directory.Delete(root);
            }
        });
    }

    private static void Submit(Session session, string actionId, int index)
    {
        var action = session.LegalActions(session.Participant(0)).Single(action => action.Definition.QualifiedId == actionId);
        session.Submit(action.Definition, session.Participant(0), action.Bindings[index]);
    }
    private static long Score(Session session, string projection) => session.Observe(session.Participant(0), projection)
        .Instances.Single().Fields.Single(field => field.Name == "Score").Value.AsInt();
}
