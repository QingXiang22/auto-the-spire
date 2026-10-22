using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunWhistleChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        using var copy = new Copy(package.Root);
        copy.Include("whistle-probes.spire", """
            action StunFirst { role = player body = { call = { procedure = StunEnemy args = { target = first(type(Enemy)) } } } }
            action SelectFirst { role = player body = { call = { procedure = SelectNextEnemyMove args = { target = first(type(Enemy)) } } } }
            action RemoveFirst { role = player body = { call = { procedure = RemoveCombatEnemy args = { target = first(type(Enemy)) } } } }
            """);
        var rules = copy.Load();
        void Turn(Session s) { Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); }
        void Saved(Session s, string move) => Equal(move, MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "saved public action");

        Case("MOVE Whistle: damage resolves before stun, exhausts, skips once and restores the selected first move itself", () =>
        {
            var s = Start(package);
            Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "Whistle.AddToHand");
            var whistle = Hand(s).Single();
            Equal(2L, CardCost(s, whistle), "locked source cost");
            Equal(33L, IntField(s, whistle, "Card", "BaseDamage"), "source damage");
            Submit(s, "UpgradeCardInHand", whistle);
            Equal(44L, IntField(s, whistle, "Card", "BaseDamage"), "upgraded source damage");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var draws = s.Random.TotalDraws;
            Submit(s, "PlayAttackFromHand", whistle);
            Equal(1L, Energy(s), "actual payment is two energy");
            Equal(11L, EnemyHp(s), "damage resolves before the stun");
            Equal("STUNNED", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "stun replaces the saved action");
            Equal("stun", MoveText(s, FirstTarget(s), "Enemy", "Intent"), "stun intent is public");
            True(Members(s, "ExhaustPile").Contains(whistle), "Whistle exhausts after resolution");
            Equal(draws, s.Random.TotalDraws, "stun does not consume randomness");
            Submit(s, "EndDevelopmentTurn");
            Equal(80L, Hp(s), "the stunned enemy skips its turn");
            Equal("FIRST_ACID_GOOP", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "first selected move resumes without skipping");
            Equal("attack", MoveText(s, FirstTarget(s), "Enemy", "Intent"), "the resumed intent is public");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(76L, Hp(s), "saved first attack really executes after the skip");
            Equal(0L, PowerStacksFor(s, "base-spire:strength-power", FirstTarget(s)), "inhale has not executed early");
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
            Equal("FIRST_ACID_GOOP", MoveText(s, enemies[0], "Enemy", "MoveId"), "targeted instance resumes independently");
            Equal("INHALE", MoveText(s, enemies[1], "Enemy", "MoveId"), "other instance advances normally");
        });

        Case("MOVE Whistle: after an actual attack stun restores the pending buff and preserves queries, history and RNG", () =>
        {
            var s = Start(rules); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Turn(s); Saved(s, "INHALE"); var state = MoveState(s, FirstTarget(s));
            var sequence = IntField(s, state, "EnemyMoveState", "Sequence"); var draws = s.Random.TotalDraws;
            Submit(s, "StunFirst"); Submit(s, "SelectFirst"); Submit(s, "StunFirst"); Saved(s, "STUNNED");
            Equal(sequence, IntField(s, state, "EnemyMoveState", "Sequence"), "forced transient does not enter source history");
            var hash = s.State.StateHash(); var version = s.StateVersion;
            for (var i = 0; i < 3; i++) { s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0)); }
            Equal(hash, s.State.StateHash(), "query does not mutate stun"); Equal(version, s.StateVersion, "query version");
            Turn(s); Saved(s, "INHALE"); Equal(76L, Hp(s), "stun skips the buff turn");
            Equal(sequence + 1, IntField(s, state, "EnemyMoveState", "Sequence"), "resume is logged once as a selected real move");
            Turn(s); Equal(7L, PowerStacksFor(s, "base-spire:strength-power", FirstTarget(s)), "pending buff executes exactly once");
            Saved(s, "ACID_GOOP"); Equal(draws, s.Random.TotalDraws, "fixed graph and stun do not draw");
        });

        Case("MOVE Whistle: random selected move resumes without reroll, preserving the actual branch history", () =>
        {
            var s = Start(rules); Submit(s, "TwigSlimeM.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Turn(s); Saved(s, "POKEY_POUNCE_MOVE"); var draws = s.Random.DrawCount(MonsterAi);
            Submit(s, "StunFirst"); Turn(s); Saved(s, "POKEY_POUNCE_MOVE");
            Equal(draws, s.Random.DrawCount(MonsterAi), "no random selection while resuming saved attack");
            Equal(2L, IntField(s, MoveState(s, FirstTarget(s)), "EnemyMoveState", "Consecutive"), "resume counts as another source selected attack");
            Turn(s); Equal(69L, Hp(s), "real resumed attack deals eleven"); Saved(s, "STICKY_SHOT_MOVE");
            Equal(draws + 1, s.Random.DrawCount(MonsterAi), "next normal branch consumes its one source draw");
        });

        Case("MOVE Whistle: lethal damage never stuns a dead target or loses card resolution", () =>
        {
            var s = Start(package); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "FuzzyWurmCrawler.Spawn");
            var target = FirstTarget(s); Set(s, target, "Enemy", "Hp", Value.Of(33L));
            var card = IcAdd(s, "Whistle"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); IcPlay(s, card);
            Equal(0L, IntField(s, target, "Enemy", "Hp"), "target died from real attack");
            Equal("FIRST_ACID_GOOP", MoveText(s, target, "Enemy", "MoveId"), "dead target is not forced into stun");
            True(Members(s, "ExhaustPile").Contains(card), "card completes and exhausts while another enemy remains");
            Turn(s); Equal(76L, Hp(s), "only survivor acts");
        });

        Case("MOVE Whistle: combat reset and full instance removal discard transient stun state", () =>
        {
            var s = Start(rules); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "StunFirst"); Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Saved(s, "FIRST_ACID_GOOP");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "StunFirst"); Submit(s, "EndDevelopmentCombat"); Submit(s, "RemoveFirst");
            foreach (var name in new[] { "EnemyMoveState", "EnemyMoveHistory" })
                Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + name).Id).Count(), "no stale state after full removal");
            Submit(s, "FuzzyWurmCrawler.Spawn"); Saved(s, "FIRST_ACID_GOOP");
        });

        Case("MOVE Whistle: unmigrated live move graph fails explicitly instead of ignoring stun", () =>
        {
            var s = Start(rules); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat");
            Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:StunFirst"), s.Participant(0), []));
            True(s.Fault!.Message.Contains("unsupported_capability", StringComparison.Ordinal), "unsupported action-control boundary");
        });

        return new Dictionary<string, string[]> { ["base-spire:whistle"] = witnesses.ToArray() };
    }
}
