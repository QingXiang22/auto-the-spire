using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private const string RewardStream = "base-spire:development-reward";

    private static Dictionary<string, string[]> RunRewardChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        Case("CP115: victory offers a repeatable 10..20 gold roll without crediting the player", () =>
        {
            foreach (var seed in new ulong[] { 20261018, 777, 1234, 99 })
            {
                Session Offered()
                {
                    var s = new Session(package.Rules, seed, ["player"]); s.Start(); Healthy(s);
                    WinRewardCombat(s);
                    Equal(99L, Field(s, "Gold"), "an offered reward is not yet owned");
                    Equal("rewards", Room(s), "the reward screen opens after victory");
                    True(RewardPending(s), "the gold entry is pending");
                    True(Field(s, "GoldRewardAmount") is >= 10 and <= 20, "source Monster-room range");
                    True(s.Random.DrawCount(RewardStream) > 0, "generation consumes reward RNG");
                    return s;
                }
                var one = Offered(); var two = Offered();
                Equal(Field(one, "GoldRewardAmount"), Field(two, "GoldRewardAmount"), "same seed repeats the amount");
                Equal(one.Random.DrawCount(RewardStream), two.Random.DrawCount(RewardStream), "same seed repeats the RNG cursor");
            }
        });
        Case("CP116: defeat creates no reward and consumes no reward randomness", () =>
        {
            var s = Start(package); Submit(s, "Bloodletting.AddToHand"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Hp", Value.Of(2L));
            var before = s.Random.DrawCount(RewardStream);
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal("defeat", Outcome(s), "the combat is lost"); Equal(99L, Field(s, "Gold"), "no gold on defeat");
            True(!RewardPending(s), "no reward entry on defeat"); Equal(0L, Field(s, "GoldRewardAmount"), "no saved amount");
            Equal(before, s.Random.DrawCount(RewardStream), "defeat does not roll rewards");
            True(!Legal(s, "ClaimDevelopmentGoldReward") && !Legal(s, "CloseDevelopmentRewards"), "no reward actions on defeat");
        });
        Case("RW03: claiming credits the saved amount once and closing never rerolls", () =>
        {
            var s = Start(package); WinRewardCombat(s);
            var amount = Field(s, "GoldRewardAmount"); var cursor = s.Random.DrawCount(RewardStream);
            Submit(s, "ClaimDevelopmentGoldReward");
            Equal(99L + amount, Field(s, "Gold"), "only the saved amount is credited");
            True(!RewardPending(s), "entry is spent"); Equal(0L, Field(s, "GoldRewardAmount"), "amount cleared");
            Equal("rewards", Room(s), "claiming does not close the screen");
            RejectRewardAction(s, "ClaimDevelopmentGoldReward");
            Equal(99L + amount, Field(s, "Gold"), "repeated claim gives no gold");
            Submit(s, "CloseDevelopmentRewards");
            Equal("none", Room(s), "explicit close leaves rewards");
            RejectRewardAction(s, "ClaimDevelopmentGoldReward"); RejectRewardAction(s, "CloseDevelopmentRewards");
            Equal(99L + amount, Field(s, "Gold"), "closing or stale claims cannot duplicate gold");
            Equal(cursor, s.Random.DrawCount(RewardStream), "only generation draws randomness");
            var capped = Start(package); WinRewardCombat(capped);
            Set(capped, Player(capped), "Player", "Gold", Value.Of(99998L));
            Submit(capped, "ClaimDevelopmentGoldReward"); Equal(99999L, Field(capped, "Gold"), "existing development currency cap");
        });
        Case("RW04: skipping discards the entry and a later combat offers a fresh reward", () =>
        {
            var s = Start(package); WinRewardCombat(s);
            var cursor = s.Random.DrawCount(RewardStream);
            Submit(s, "CloseDevelopmentRewards");
            Equal(99L, Field(s, "Gold"), "skipping does not credit gold");
            True(!RewardPending(s), "skipped entry cleared"); Equal(0L, Field(s, "GoldRewardAmount"), "skipped amount cleared");
            Equal(cursor, s.Random.DrawCount(RewardStream), "skipping does not reroll");
            WinRewardCombat(s);
            True(RewardPending(s), "next victory opens a new entry");
            True(s.Random.DrawCount(RewardStream) > cursor, "new victory generates a new roll");
            var amount = Field(s, "GoldRewardAmount"); Submit(s, "ClaimDevelopmentGoldReward");
            Equal(99L + amount, Field(s, "Gold"), "skipped gold was not carried into the next claim");
        });
        Case("RW05: open rewards block room transitions at both action and procedure boundaries", () =>
        {
            var transitions = new[] { "BeginDevelopmentCombat", "EnterDevelopmentRestSite", "LeaveDevelopmentRoom" };
            var s = Start(package); WinRewardCombat(s);
            var amount = Field(s, "GoldRewardAmount"); var cursor = s.Random.DrawCount(RewardStream);
            foreach (var action in transitions) RejectRewardAction(s, action);
            Equal("rewards", Room(s), "invalid transitions keep the reward screen");
            Equal(amount, Field(s, "GoldRewardAmount"), "invalid transitions keep the offer");
            Equal(cursor, s.Random.DrawCount(RewardStream), "invalid transitions consume no randomness");
            Submit(s, "ClaimDevelopmentGoldReward");
            foreach (var action in transitions) True(!Legal(s, action), "even claimed rewards must be explicitly closed");
            Submit(s, "CloseDevelopmentRewards");
            True(Legal(s, "BeginDevelopmentCombat") && Legal(s, "EnterDevelopmentRestSite"), "closing restores room entry");
            True(!Legal(s, "LeaveDevelopmentRoom"), "there is no room to leave after rewards close");

            using var copy = new Copy(package.Root);
            foreach (var procedure in transitions.Append("ClaimDevelopmentGoldReward").Append("CloseDevelopmentRewards"))
                copy.Include(procedure + "-probe.spire", "action Force" + procedure + " { role = player body = { call = { procedure = " + procedure + " args = { owner = first(type(Player)) } } } }");
            var probe = copy.Load();
            foreach (var procedure in transitions)
            {
                var forced = Start(probe); WinRewardCombat(forced);
                ForceRejected(forced, procedure);
            }
            foreach (var procedure in new[] { "ClaimDevelopmentGoldReward", "CloseDevelopmentRewards" })
                ForceRejected(Start(probe), procedure);
        });

        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:combat-gold"] = witnesses.ToArray() };
    }

    private static void WinRewardCombat(Session s)
    {
        Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat");
        var target = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id)
            .Single(enemy => s.State.GetField(enemy, s.RuleSet.TypeByQualifiedId("base-spire:Enemy").FieldIndex("Hp")).AsInt() > 0);
        Set(s, target, "Enemy", "Hp", Value.Of(1L));
        Submit(s, "BeginDevelopmentTurn"); Submit(s, "Strike.AddToHand"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
        Equal("victory", Outcome(s), "the actual card kill produces victory");
    }

    private static bool RewardPending(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("GoldRewardPending")).AsBool();
    private static void RejectRewardAction(Session s, string name)
    {
        True(!Legal(s, name), "no candidate for " + name);
        Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), s.Participant(0), []));
        Equal(SessionStatus.Running, s.Status, "the invalid action does not fault the session");
    }
    private static void ForceRejected(Session s, string name)
    {
        Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:Force" + name), s.Participant(0), []));
        Equal(SessionStatus.Faulted, s.Status, "the procedure rejects bypassing " + name);
    }
}
