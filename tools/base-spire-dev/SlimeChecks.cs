using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunSlimeChecks(DslPackage package, Action<string, Action> check)
    {
        using var copy = new Copy(package.Root);
        copy.Include("slimes-probe.spire", """
            action SlimesProbe { role = player body = {
              call = { procedure = InitializeNormalIronclad args = { owner = first(type(Player)) } }
              call = { procedure = SpawnSourceSlimesWeak args = { totalFloor = 2 } }
              call = { procedure = BeginDevelopmentCombat args = { owner = first(type(Player)) } }
              call = { procedure = BeginDevelopmentTurn args = { owner = first(type(Player)) } }
            } }
            """);
        var rules = copy.Load(); var witnesses = new List<string>();
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.slimes-vectors.json")!);
        Equal(10, data.RootElement.GetProperty("vectors").GetArrayLength(), "all original source seeds retained");
        foreach (var vector in data.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var seed = vector.GetProperty("seed").GetString()!;
            var name = "SLIMES native composition, HP, three turns, status generation and AI cursor: " + JsonSerializer.Serialize(seed);
            check(name, () =>
            {
                var s = rules.CreateSession(1, ["player"], seed); s.Start(); Submit(s, "SlimesProbe");
                var enemies = Targets(s); var expected = vector.GetProperty("monsters").EnumerateArray().ToArray();
                Equal(expected.Length, enemies.Count, "full formation");
                for (var i = 0; i < enemies.Count; i++)
                {
                    Equal(expected[i].GetProperty("id").GetString(), TextField(s, enemies[i], "Enemy", "DefinitionId"), "native spawn order");
                    Equal(expected[i].GetProperty("hp").GetInt64(), IntField(s, enemies[i], "Enemy", "Hp"), "native HP");
                }
                Equal(vector.GetProperty("composition_draws").GetInt64(), s.Random.DrawCount("base-spire:encounter:monsters"), "singleton composition still consumes");
                Equal(vector.GetProperty("niche_draws").GetInt64(), s.Random.DrawCount("base-spire:run:niche"), "native HP cursor");
                var turn = 0;
                foreach (var state in vector.GetProperty("states").EnumerateArray())
                {
                    if (turn++ > 0) { Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); }
                    Equal(state.GetProperty("hp").GetInt64(), Hp(s), "actual attacks match reference");
                    Equal(state.GetProperty("monster_ai_draws").GetInt64(), s.Random.DrawCount(MonsterAi), "fixed and random transitions preserve cursor");
                    True(enemies.Select(e => TextField(s, e, "Enemy", "MoveId")).SequenceEqual(state.GetProperty("moves").EnumerateArray().Select(m => m.GetString())), "actual saved moves match reference");
                    Equal(state.GetProperty("slimed").GetInt32(), s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Count(c => CardDefinition(s, c) == "base-spire:slimed"), "actual generated status count");
                }
            });
            witnesses.Add(name);
        }
        return new() { ["base-spire:leaf-slime-s"] = witnesses.ToArray(), ["base-spire:leaf-slime-m"] = witnesses.ToArray(), ["base-spire:slimes-weak"] = witnesses.ToArray() };
    }
}
