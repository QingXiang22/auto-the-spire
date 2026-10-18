using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunRestSiteChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        Case("CP112: the campfire heals thirty percent of maximum HP once per visit", () =>
        {
            var s = Start(package); Submit(s, "EnterDevelopmentRestSite");
            Equal("rest-site", Room(s), "the rest site is the current room");
            Set(s, Player(s), "Player", "Hp", Value.Of(40L));
            Submit(s, "RestAtDevelopmentCampfire");
            Equal(64L, Hp(s), "forty plus the 24 point thirty percent heal"); True(RoomUsed(s), "the option is spent");
            True(!Legal(s, "RestAtDevelopmentCampfire"), "a used campfire offers no rest candidate");
            Submit(s, "LeaveDevelopmentRoom"); Equal("none", Room(s), "leaving clears the room");
            Submit(s, "EnterDevelopmentRestSite"); Set(s, Player(s), "Player", "Hp", Value.Of(40L));
            Submit(s, "RestAtDevelopmentCampfire"); Equal(64L, Hp(s), "a new visit can rest again");
        });
        Case("CP113: the campfire heal floors the percentage and caps at maximum HP", () =>
        {
            var s = Start(package); Submit(s, "EnterDevelopmentRestSite");
            Set(s, Player(s), "Player", "MaxHp", Value.Of(87L)); Set(s, Player(s), "Player", "Hp", Value.Of(10L));
            Submit(s, "RestAtDevelopmentCampfire");
            Equal(36L, Hp(s), "87 x 0.3 = 26.1 floored to 26, so 10 + 26");
            var capped = Start(package); Submit(capped, "EnterDevelopmentRestSite"); Set(capped, Player(capped), "Player", "Hp", Value.Of(79L));
            Submit(capped, "RestAtDevelopmentCampfire"); Equal(80L, Hp(capped), "the heal stops at maximum HP");
        });
        Case("CP114: resting requires a rest site and a battle room clears it", () =>
        {
            var s = Start(package);
            True(!Legal(s, "RestAtDevelopmentCampfire"), "no rest candidate outside a rest site");
            Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:RestAtDevelopmentCampfire"), s.Participant(0), []));
            Equal(SessionStatus.Running, s.Status, "the action guard rejects it without faulting the session"); Equal(80L, Hp(s), "and nothing is healed");
            using var copy = new Copy(package.Root);
            copy.Include("rest-probe.spire", """
                action ForceRest { role = player body = { call = { procedure = RestAtDevelopmentCampfire args = { owner = first(type(Player)) } } } }
                """);
            var forced = Start(copy.Load());
            Throws<EngineFaultException>(() => forced.Submit(forced.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ForceRest"), forced.Participant(0), []));
            Equal(SessionStatus.Faulted, forced.Status, "the procedure itself also refuses outside a rest site");
            var battle = Start(package); Submit(battle, "EnterDevelopmentRestSite"); Submit(battle, "BeginDevelopmentCombat");
            Equal("battle", Room(battle), "starting a battle replaces the room"); Equal("none", Room(Start(package)), "a fresh session starts outside any room");
            Submit(battle, "EndDevelopmentCombat"); Equal("none", Room(battle), "ending the battle leaves the room");
        });

        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:rest-site"] = witnesses.ToArray() };
    }

    private static string Room(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("Room")).AsString();
    private static bool RoomUsed(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("RoomUsed")).AsBool();
}
