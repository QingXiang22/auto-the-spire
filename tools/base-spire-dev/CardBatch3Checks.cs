using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCardBatch3Checks(DslPackage package, Action<string, Action> check)
    {
        var witness = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var shared = new List<string>();
        void Case(string name, Action body) { check(name, body); shared.Add(name); }
        void Own(string id, string name, Action body)
        {
            check(name, body);
            if (!witness.TryGetValue(id, out var list)) witness[id] = list = [];
            list.Add(name);
        }

        Case("CP49: the lost-HP flag only lasts for the turn it was set in", () =>
        {
            var s = Start(package); Submit(s, "Bloodletting.AddToHand"); Submit(s, "Spite.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:bloodletting"));
            True(FieldBool(s, "LostHpThisTurn"), "losing HP sets the turn flag");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(30L, EnemyHp(s), "the flagged hit count applies twice in the same turn");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            True(!FieldBool(s, "LostHpThisTurn"), "the next turn clears the flag");
            Submit(s, "Spite.AddToHand"); Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:spite"));
            Equal(25L, EnemyHp(s), "without the flag Spite hits once");
        });
        Case("CP50: the exhausted-card flag is only set by an actual exhaust", () =>
        {
            var s = Start(package); Submit(s, "EvilEye.AddToHand"); Submit(s, "Tremble.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:evil-eye"));
            Equal(8L, Block(s), "no exhaust yet means a single block gain");
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:tremble"));
            True(FieldBool(s, "ExhaustedCardThisTurn"), "the exhausting card set the flag");
            Submit(s, "EvilEye.AddToHand"); Set(s, Player(s), "Player", "Energy", Value.Of(3L)); Set(s, Player(s), "Player", "Block", Value.Of(0L));
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:evil-eye"));
            Equal(16L, Block(s), "the flag doubles the block");
        });

        Own("base-spire:spite", "CP51 Spite: cost 0 for 5 damage, twice after HP loss and three times upgraded", () =>
        {
            var s = Start(package); Submit(s, "Spite.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(35L, EnemyHp(s), "no HP loss yet means one hit");
            Set(s, Player(s), "Player", "Hp", Value.Of(70L)); Set(s, Player(s), "Player", "LostHpThisTurn", Value.Of(true));
            Submit(s, "Spite.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(20L, EnemyHp(s), "the upgraded card hits three times when the flag is set");
        });
        Own("base-spire:evil-eye", "CP52 Evil Eye: cost 1 for 8 block, 16 after an exhaust and 11 or 22 upgraded", () =>
        {
            var s = Start(package); Submit(s, "EvilEye.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(8L, Block(s), "normal Evil Eye gains eight block");
            Submit(s, "EvilEye.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L)); Set(s, Player(s), "Player", "Block", Value.Of(0L)); Set(s, Player(s), "Player", "ExhaustedCardThisTurn", Value.Of(true));
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(22L, Block(s), "upgraded Evil Eye gains eleven block twice after an exhaust");
        });
        Own("base-spire:break", "CP53 Break: cost 1 for 20 damage plus 5 vulnerable, 30 and 7 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Break.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(20L, EnemyHp(s), "normal Break deals 20"); Equal(5L, PowerStacks(s, "base-spire:vulnerable-power"), "and five vulnerable");
            Submit(s, "Break.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(0L, EnemyHp(s), "upgraded Break deals 30 x 1.5 against the vulnerable target, flooring at zero"); Equal(12L, PowerStacks(s, "base-spire:vulnerable-power"), "vulnerable stacks add up");
        });
        Own("base-spire:howl-from-beyond", "CP54 Howl From Beyond: cost 3 for 16 damage to all enemies, 21 upgraded; the exhaust hover tip is not a keyword", () =>
        {
            var s = Start(package); Submit(s, "HowlFromBeyond.AddToHand"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            foreach (var target in Targets(s)) Equal(24L, s.State.GetField(target, EnemyField(s, "Hp")).AsInt(), "every target took 16");
            Equal(0, Members(s, "ExhaustPile").Count, "no innate exhaust keyword"); Equal(0L, Energy(s), "three energy spent");
            Submit(s, "HowlFromBeyond.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            foreach (var target in Targets(s)) Equal(3L, s.State.GetField(target, EnemyField(s, "Hp")).AsInt(), "the upgraded card deals 21 to each target");
        });
        Own("base-spire:hemokinesis", "CP55 Hemokinesis: cost 1, lose 2 HP then deal 15 damage, 20 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Hemokinesis.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(78L, Hp(s), "two HP lost before the attack"); Equal(25L, EnemyHp(s), "normal Hemokinesis deals 15");
            Submit(s, "Hemokinesis.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(76L, Hp(s), "the upgrade does not change the HP loss"); Equal(5L, EnemyHp(s), "upgraded Hemokinesis deals 20");
        });

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }

    private static bool FieldBool(Session s, string name) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex(name)).AsBool();
}
