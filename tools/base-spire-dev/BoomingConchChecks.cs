using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunBoomingConchChecks(DslPackage package, Action<string, Action> check)
    {
        using var copy = new Copy(package.Root);
        copy.Include("conch-probes.spire", """
            procedure ConchRoom { parameters = { room = String(length = 24) } locals = { run = Binding node = Binding visit = Binding } body = {
              let = { into = run value = first(type(Run)) }
              create = { type = MapNode scope = entityScope(run) into = node fields = { Run = run Index = 0 Col = 3 Row = 1 Type = enum(MapPointType, Elite) } }
              create = { type = RunVisit scope = entityScope(run) into = visit fields = { Run = run StableId = 1 Node = node MapType = enum(MapPointType, Elite) RoomType = room } }
              set = { target = run field = Run.CurrentVisit value = visit }
              set = { target = first(type(Player)) field = Player.CurrentRun value = run }
            } }
            action ConchElite { role = player body = { call = { procedure = ConchRoom args = { room = "elite" } } } }
            action ConchMonster { role = player body = { call = { procedure = ConchRoom args = { room = "monster" } } } }
            action ConchBegin { role = player body = { call = { procedure = BeginDevelopmentCombat args = { owner = first(type(Player)) } } } }
            action ConchObtain { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:booming-conch" } } } }
            action ConchOther { role = player locals = { other = Binding } body = {
              create = { type = Player scope = frameScope() into = other }
              call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:booming-conch" } }
            } }
            """);
        var compiled = copy.Load();
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }
        Case("RELIC BoomingConch: real Elite first turn draws two extra cards and grants one energy, later turns do not", () =>
        {
            var s = Start(compiled); Submit(s, "Ironclad.Initialize"); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "ConchElite"); Submit(s, "ConchObtain");
            Submit(s, "ConchBegin"); Submit(s, "BeginDevelopmentTurn");
            Equal(7, Hand(s).Count, "source Elite opening hand"); Equal(4L, IntField(s, Player(s), "Player", "Energy"), "source Elite opening energy");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(5, Hand(s).Count, "turn two returns to normal draw"); Equal(3L, IntField(s, Player(s), "Player", "Energy"), "turn two returns to normal energy");
        });
        Case("RELIC BoomingConch: Monster room, other holder and melted instances do not grant Elite benefits", () =>
        {
            foreach (var inactive in new[] { "monster", "other", "melted" })
            {
                var s = Start(compiled); Submit(s, "Ironclad.Initialize"); Submit(s, inactive == "monster" ? "ConchMonster" : "ConchElite");
                Submit(s, inactive == "other" ? "ConchOther" : "ConchObtain");
                if (inactive == "melted")
                {
                    var relic = Members(s, "Relics").Single(r => TextField(s, r, "Relic", "DefinitionId") == "base-spire:booming-conch");
                    Set(s, relic, "Relic", "IsMelted", Value.Of(true));
                }
                Submit(s, "ConchBegin"); Submit(s, "BeginDevelopmentTurn");
                Equal(5, Hand(s).Count, "inactive source draw: " + inactive); Equal(3L, IntField(s, Player(s), "Player", "Energy"), "inactive source energy: " + inactive);
            }
        });
        Case("RELIC BoomingConch: two owned instances each modify the same Elite turn and pure observation draws no RNG", () =>
        {
            var s = Start(compiled); Submit(s, "Ironclad.Initialize"); Submit(s, "ConchElite"); Submit(s, "ConchObtain"); Submit(s, "ConchObtain");
            Submit(s, "ConchBegin"); Submit(s, "BeginDevelopmentTurn");
            Equal(9, Hand(s).Count, "two independent draw modifiers"); Equal(5L, IntField(s, Player(s), "Player", "Energy"), "two independent energy reactions");
            var state = s.State.StateHash(); var draws = s.Random.TotalDraws;
            s.LegalActions(s.Participant(0)); s.LegalActions(s.Participant(0));
            Equal(state, s.State.StateHash(), "query remains read-only"); Equal(draws, s.Random.TotalDraws, "query consumes no randomness");
        });
        return new Dictionary<string, string[]> { ["base-spire:booming-conch"] = witnesses.ToArray() };
    }
}
