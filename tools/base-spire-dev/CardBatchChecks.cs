using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCardBatchChecks(DslPackage package, Action<string, Action> check)
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

        Case("CP39: an all-enemies attack resolves before the debuff it applies to the same enemies", () =>
        {
            var s = Start(package); Submit(s, "Thunderclap.AddToHand"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            var targets = Targets(s);
            Equal(36L, s.State.GetField(targets[0], EnemyField(s, "Hp")).AsInt(), "first target took only the base 4");
            Equal(36L, s.State.GetField(targets[1], EnemyField(s, "Hp")).AsInt(), "second target took only the base 4");
            Equal(1L, PowerStacksFor(s, "base-spire:vulnerable-power", targets[1]), "the debuff landed after the damage");
            Submit(s, "Thunderclap.AddToHand"); Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(30L, s.State.GetField(targets[0], EnemyField(s, "Hp")).AsInt(), "the next thunderclap deals 4 x 1.5 to a vulnerable target");
        });

        Own("base-spire:taunt", "CP40 Taunt: cost 1 for 7 block plus 1 vulnerable, 8 and 2 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Taunt.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(7L, Block(s), "normal Taunt gains 7 block"); Equal(1L, PowerStacks(s, "base-spire:vulnerable-power"), "and one vulnerable");
            Submit(s, "Taunt.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(15L, Block(s), "upgraded Taunt gains 8 more block"); Equal(3L, PowerStacks(s, "base-spire:vulnerable-power"), "and two more vulnerable");
        });
        Own("base-spire:tremble", "CP41 Tremble: cost 1 for 3 vulnerable and it exhausts", () =>
        {
            var s = Start(package); Submit(s, "Tremble.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(3L, PowerStacks(s, "base-spire:vulnerable-power"), "normal Tremble applies three"); Equal(1, Members(s, "ExhaustPile").Count, "and exhausts");
            Submit(s, "Tremble.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(7L, PowerStacks(s, "base-spire:vulnerable-power"), "upgraded Tremble adds four"); Equal(2, Members(s, "ExhaustPile").Count, "both copies exhausted");
        });
        Own("base-spire:thunderclap", "CP42 Thunderclap: cost 1 for 4 damage and 1 vulnerable to every enemy, 7 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Thunderclap.AddToHand"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            foreach (var target in Targets(s)) Equal(36L, s.State.GetField(target, EnemyField(s, "Hp")).AsInt(), "every target took 4");
            Submit(s, "Thunderclap.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            foreach (var target in Targets(s)) Equal(26L, s.State.GetField(target, EnemyField(s, "Hp")).AsInt(), "upgraded thunderclap deals 7 x 1.5 rounded down to each vulnerable target");
        });
        Own("base-spire:blood-wall", "CP43 Blood Wall: cost 2 for 2 HP loss then 16 block, 20 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "BloodWall.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(78L, Hp(s), "two HP lost"); Equal(16L, Block(s), "sixteen block gained"); Equal(1L, Energy(s), "two energy spent");
            Submit(s, "BloodWall.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(2L)); Set(s, Player(s), "Player", "Block", Value.Of(0L));
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(76L, Hp(s), "the upgrade does not change the HP loss"); Equal(20L, Block(s), "upgraded Blood Wall gains twenty block");
        });

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }
}
