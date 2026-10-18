using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCombatEndChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        Case("CP45: killing the last enemy ends the combat in victory and closes the turn", () =>
        {
            var s = Start(package); Submit(s, "Bludgeon.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(8L, EnemyHp(s), "the surviving target keeps the combat open"); Equal("none", Outcome(s), "the combat is not over");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(5L));
            Submit(s, "Bludgeon.AddToHand"); Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(0L, EnemyHp(s), "the last target died"); Equal("victory", Outcome(s), "the combat is won");
            True(!Active(s), "the combat is closed"); True(!Legal(s, "PlayAttackFromHand"), "no further plays are offered");
            True(!Legal(s, "BeginDevelopmentTurn"), "no further turns are offered");
            True(!Legal(s, "BeginDevelopmentCombat"), "rewards must close before the next combat");
            Submit(s, "CloseDevelopmentRewards");
            True(Legal(s, "BeginDevelopmentCombat"), "a new development combat may start after rewards close");
        });
        Case("CP46: reaching zero HP ends the combat in defeat even without enemies", () =>
        {
            var s = Start(package); Submit(s, "Bloodletting.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Hp", Value.Of(2L));
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(0L, Hp(s), "the HP floor is zero"); Equal("defeat", Outcome(s), "the combat is lost");
            True(!Active(s), "the combat is closed"); True(!Legal(s, "PlayCardFromHand"), "no further plays are offered");
        });
        Case("CP47: a new development combat resets the previous outcome", () =>
        {
            var s = Start(package); Submit(s, "Bloodletting.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Hp", Value.Of(2L)); Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal("defeat", Outcome(s), "first combat lost");
            Set(s, Player(s), "Player", "Hp", Value.Of(80L));
            Submit(s, "BeginDevelopmentCombat");
            Equal("none", Outcome(s), "the outcome is cleared for the new combat"); True(Active(s), "the new combat runs");
            Submit(s, "BeginDevelopmentTurn"); Equal(1L, Turn(s), "the new combat starts at turn one");
        });

        var all = new List<string>(witnesses);
        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:bludgeon"] = all.ToArray() };
    }

    private static string Outcome(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("CombatOutcome")).AsString();
}
