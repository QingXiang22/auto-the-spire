using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    // Controlled integration of the pre-generated elite queue: the room type is forced so the
    // check does not need a full reference route through an elite node.
    private static void RunActOneEliteVisitChecks(DslPackage package)
    {
        using var copy = new Copy(package.Root);
        copy.Include("act-one-elite-probes.spire", """
            action SetEliteMapPointProbe { role = player locals = { node = Binding } body = {
              let = { into = node value = first(where(collection(MapNodes), n, field(n, MapNode.Selectable))) }
              set = { target = node field = MapNode.Type value = enum(MapPointType, Elite) }
            } }
            action KillEliteProbe { role = player body = {
              call = { procedure = DamageEnemy args = { target = first(type(Enemy)) amount = 999 } }
            } }
            """);
        var compiled = copy.Load();
        using var opening = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-opening-vectors.json")!);
        var s = compiled.CreateSession(777, ["player"], "777"); s.Start(); Healthy(s);
        Submit(s, "StartNormalRun");
        s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Instances: [s.PendingDecision.Candidates.Single(o => TextField(s, o, "NeowOption", "RelicId") == "base-spire:booming-conch")]));
        s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
        var run = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
        var normalCursor = IntField(s, run, "Run", "NormalEncounterIndex");
        var gold = IntField(s, Player(s), "Player", "Gold");
        Submit(s, "SetEliteMapPointProbe");
        var choose = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
        var node = choose.Bindings[0][0].AsRef();
        s.Submit(choose.Definition, s.Participant(0), choose.Bindings[0]); Healthy(s);
        var visit = Members(s, "RunVisits").Single();
        var expected = opening.RootElement.GetProperty("vectors").EnumerateArray().Single(v => v.GetProperty("seed").GetString() == "777")
            .GetProperty("plans").EnumerateArray().Single(p => p.GetProperty("act").GetInt64() == 0
                && p.GetProperty("room").GetString() == "Elite" && p.GetProperty("position").GetInt64() == 0);
        var encounter = expected.GetProperty("id").GetString()!;
        Equal(encounter, TextField(s, visit, "RunVisit", "EncounterId"), "the elite room consumes the pre-generated elite queue");
        Equal("elite", TextField(s, visit, "RunVisit", "RoomType"), "the actual room type is elite, not only the map icon");
        Equal(s.State.GetField(node, s.RuleSet.TypeByQualifiedId("base-spire:MapNode").FieldIndex("Type")).AsEnumOrdinal(),
            s.State.GetField(visit, s.RuleSet.TypeByQualifiedId("base-spire:RunVisit").FieldIndex("MapType")).AsEnumOrdinal(), "the map keeps its original node type");
        Equal(1L, IntField(s, run, "Run", "EliteEncounterIndex"), "the elite cursor advances");
        Equal(normalCursor, IntField(s, run, "Run", "NormalEncounterIndex"), "an elite visit does not consume the normal queue");
        Equal(1, Targets(s).Count, "single source formation");
        Equal(encounter[..^"-elite".Length], TargetId(s), "the elite encounter spawns its source monster");
        True(EnemyHp(s) is >= 61 and <= 127, "A0 act-one elite HP range");
        Submit(s, "KillEliteProbe");
        Equal("rewards", TextField(s, Player(s), "Player", "Room"), "elite victory opens rewards");
        var set = s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("CurrentRewardSet")).AsRef();
        Equal("elite", TextField(s, set, "CombatRewardSet", "Source"), "rewards keep the actual elite source");
        var amount = Field(s, "GoldRewardAmount");
        True(amount is >= 35 and <= 45, "elite gold range");
        True(s.State.GetField(set, s.RuleSet.TypeByQualifiedId("base-spire:CombatRewardSet").FieldIndex("RelicPending")).AsBool(), "elite relic reward pending");
        Equal(3, Members(s, "CardRewardOptions").Count, "three elite card options");
        Submit(s, "ClaimDevelopmentGoldReward");
        Equal(gold + amount, IntField(s, Player(s), "Player", "Gold"), "elite gold is claimed exactly once");
        var relic = TextField(s, set, "CombatRewardSet", "RelicId");
        if (s.LegalActions(s.Participant(0)).Any(a => a.Definition.QualifiedId == "base-spire:ClaimCombatRelicReward"))
        {
            Submit(s, "ClaimCombatRelicReward");
            True(Members(s, "Relics").Any(r => TextField(s, r, "Relic", "DefinitionId") == relic), "the implemented elite relic joins the run");
        }
        Submit(s, "CloseDevelopmentRewards");
        Equal("map", TextField(s, Player(s), "Player", "Room"), "closing elite rewards returns to the map");
        Equal("map", TextField(s, run, "Run", "Phase"), "the run phase returns to the map");
        Equal(1L, IntField(s, run, "Run", "EliteEncounterIndex"), "closing does not reroll the elite queue");
        Console.WriteLine("ok   ENV-T05 normal elite room: source queue, cursors, rewards and map return");
    }
}
