using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunEncounterChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        Case("CP79: the source weak encounter spawns exactly its one monster with the source minimum HP", () =>
        {
            var s = Start(package); Submit(s, "FuzzyWurmCrawlerWeak.Spawn");
            Equal(1, Targets(s).Count, "one monster"); Equal("base-spire:fuzzy-wurm-crawler", TargetId(s), "the source monster");
            Equal(55L, EnemyHp(s), "the encounter spawns the minimum initial HP"); Equal(55L, s.State.GetField(FirstTarget(s), EnemyField(s, "MaxHp")).AsInt(), "max HP matches");
            Equal("attack", EnemyIntent(s), "the first intent is public"); Equal(0L, EnemyMove(s), "it starts on its first move");
        });
        Case("CP80: the encounter monster takes its rotation and can be defeated like any other enemy", () =>
        {
            var s = Start(package); Submit(s, "FuzzyWurmCrawlerWeak.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(76L, Hp(s), "the encounter monster attacked for 4");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(5L));
            Submit(s, "Bludgeon.AddToHand"); Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal("victory", Outcome(s), "killing the encounter monster wins the development combat");
        });

        Case("CP84: the raider formation spawns three distinct raiders with their own source HP", () =>
        {
            var s = Start(package); Submit(s, "RubyRaidersNormal.Spawn");
            var spawned = Targets(s);
            Equal(3, spawned.Count, "three slots");
            var ids = spawned.Select(target => s.State.GetField(target, EnemyField(s, "DefinitionId")).AsString()).ToArray();
            Equal(3, ids.Distinct(StringComparer.Ordinal).Count(), "the three raiders are distinct: " + string.Join(",", ids));
            var expected = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["base-spire:axe-ruby-raider"] = 20, ["base-spire:assassin-ruby-raider"] = 18, ["base-spire:brute-ruby-raider"] = 30,
                ["base-spire:crossbow-ruby-raider"] = 18, ["base-spire:tracker-ruby-raider"] = 21,
            };
            foreach (var target in spawned)
            {
                var id = s.State.GetField(target, EnemyField(s, "DefinitionId")).AsString();
                True(expected.ContainsKey(id), "each slot is a source raider candidate: " + id);
                Equal(expected[id], s.State.GetField(target, EnemyField(s, "Hp")).AsInt(), "HP follows the candidate's source minimum for " + id);
            }
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EnemyPoolEntry").Id).Count, "the temporary pool is cleared");
        });
        Case("CP85: the same seed repeats the same formation while another seed still yields a valid one", () =>
        {
            string Formation(DslPackage rules, ulong seed)
            {
                var session = rules.CreateSession(seed, ["player"]); session.Start(); Healthy(session);
                Submit(session, "RubyRaidersNormal.Spawn");
                return string.Join(",", Targets(session).Select(target => session.State.GetField(target, EnemyField(session, "DefinitionId")).AsString()));
            }
            var first = Formation(package, 20261018); var again = Formation(package, 20261018);
            Equal(first, again, "the same seed repeats the formation");
            var other = Formation(package, 777);
            Equal(3, other.Split(',').Distinct(StringComparer.Ordinal).Count(), "another seed still yields three distinct raiders");
        });

        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:fuzzy-wurm-crawler-weak"] = witnesses.ToArray(), ["base-spire:ruby-raiders-normal"] = witnesses.ToArray() };
    }
}
