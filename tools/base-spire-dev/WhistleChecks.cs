using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunWhistleChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        Case("MOVE Whistle: damage resolves before stun, exhausts, skips one enemy turn and resumes the saved successor", () =>
        {
            var s = Start(package);
            Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "Whistle.AddToHand");
            var whistle = Hand(s).Single();
            Equal(3L, CardCost(s, whistle), "source cost");
            Equal(33L, IntField(s, whistle, "Card", "BaseDamage"), "source damage");
            Submit(s, "UpgradeCardInHand", whistle);
            Equal(44L, IntField(s, whistle, "Card", "BaseDamage"), "upgraded source damage");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var draws = s.Random.TotalDraws;
            Submit(s, "PlayAttackFromHand", whistle);
            Equal(11L, EnemyHp(s), "damage resolves before the stun");
            Equal("STUNNED", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "stun replaces the saved action");
            Equal("stun", MoveText(s, FirstTarget(s), "Enemy", "Intent"), "stun intent is public");
            True(Members(s, "ExhaustPile").Contains(whistle), "Whistle exhausts after resolution");
            Equal(draws, s.Random.TotalDraws, "stun does not consume randomness");
            Submit(s, "EndDevelopmentTurn");
            Equal(80L, Hp(s), "the stunned enemy skips its turn");
            Equal("INHALE", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "the move resumes from the saved successor");
            Equal("buff", MoveText(s, FirstTarget(s), "Enemy", "Intent"), "the resumed intent is public");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(7L, PowerStacksFor(s, "base-spire:strength-power", FirstTarget(s)), "the resumed move executes once");
        });

        Case("MOVE Whistle: two graph instances isolate the forced skip and the other enemy still acts", () =>
        {
            var s = Start(package);
            Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "FuzzyWurmCrawler.Spawn");
            var enemies = Targets(s).ToArray();
            Submit(s, "Whistle.AddToHand"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var whistle = Hand(s).Single();
            Submit(s, "PlayAttackFromHand", whistle);
            Equal("STUNNED", MoveText(s, enemies[0], "Enemy", "MoveId"), "only the targeted instance is stunned");
            Equal("FIRST_ACID_GOOP", MoveText(s, enemies[1], "Enemy", "MoveId"), "the other instance is unchanged before its turn");
            Submit(s, "EndDevelopmentTurn");
            Equal(76L, Hp(s), "the other enemy acts while the targeted one skips");
            Equal("INHALE", MoveText(s, enemies[0], "Enemy", "MoveId"), "targeted instance resumes independently");
            Equal("INHALE", MoveText(s, enemies[1], "Enemy", "MoveId"), "other instance advances normally");
        });

        return new Dictionary<string, string[]> { ["base-spire:whistle"] = witnesses.ToArray() };
    }
}
