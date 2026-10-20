using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunNormalRunOnly(DslPackage package)
    {
        var session = StartNormal(package);
        Submit(session, "StartNormalRun");
        Equal(SessionStatus.AwaitingDecision, session.Status, "normal entry pauses for Neow");
        Equal("base-spire:ironclad", TextField(session, Player(session), "Player", "CharacterId"), "normal entry initializes Ironclad");
        True(session.PendingDecision is { Kind: DecisionResponseKind.Bool }, "Neow decision is a boolean choice");
        Equal(0L, session.Random.DrawCount("base-spire:run:act_1_map"), "map stream waits until Neow is resolved");

        session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, Flag: true));
        Healthy(session);
        var run = session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
        var nodes = Members(session, "MapNodes");
        Equal(4, nodes.Count, "first act map materializes four connected nodes");
        Equal(1L, session.Random.DrawCount("base-spire:run:act_1_map"), "map generation consumes its named stream once");
        Equal("map", TextField(session, Player(session), "Player", "Room"), "Neow returns to map phase");
        var choose = session.LegalActions(session.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
        Equal(1, choose.Bindings.Count, "only the first legal map child is offered");

        session.Submit(choose.Definition, session.Participant(0), choose.Bindings[0]);
        Healthy(session);
        Equal(SessionStatus.Running, session.Status, "first map visit remains active in combat");
        Equal(3, session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).Count(), "normal encounter spawns the real Ruby Raiders formation");
        Equal("combat", TextField(session, run, "Run", "Phase"), "visit phase records combat");
        Console.WriteLine("ok   ENV-T05: normal Neow, map generation, legal child and real first encounter");
        return 0;
    }

    private static Session StartNormal(DslPackage package)
    {
        var session = package.CreateSession(20261021, ["player"]);
        session.Start();
        Healthy(session);
        return session;
    }

    private static string TextField(Session session, InstanceId entity, string type, string field)
    {
        var value = session.State.GetField(entity, session.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        return value.AsString();
    }
}
