using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCardBatch4Checks(DslPackage package, Action<string, Action> check)
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

        Case("CP66: an all-enemies multi-hit card resolves every hit on every target", () =>
        {
            var s = Start(package); Submit(s, "Conflagration.AddToHand"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            foreach (var target in Targets(s)) Equal(32L, s.State.GetField(target, EnemyField(s, "Hp")).AsInt(), "four hits of two damage each");
            Submit(s, "Conflagration.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            foreach (var target in Targets(s)) Equal(22L, s.State.GetField(target, EnemyField(s, "Hp")).AsInt(), "the upgraded card adds a fifth hit");
        });
        Case("CP67: the target's vulnerable stacks decide Dismantle's hit count and multiply its damage", () =>
        {
            var s = Start(package); Submit(s, "Bash.AddToHand"); Submit(s, "Dismantle.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:bash"));
            Equal(32L, EnemyHp(s), "bash hit for 8 and applied vulnerable");
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(8L, EnemyHp(s), "two hits of 8 x 1.5 rounded down");
            Submit(s, "Dismantle.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(0L, EnemyHp(s), "the upgraded two hits floor the target at zero");
        });
        Case("CP68: Molten Fist doubles the vulnerable it found after dealing its own damage", () =>
        {
            var s = Start(package); Submit(s, "Bash.AddToHand"); Submit(s, "MoltenFist.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:bash"));
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(17L, EnemyHp(s), "10 x 1.5 rounded down against the vulnerable target"); Equal(4L, PowerStacks(s, "base-spire:vulnerable-power"), "the existing two stacks were doubled");
            Equal(1, Members(s, "ExhaustPile").Count, "Molten Fist exhausts");
        });

        Own("base-spire:breakthrough", "CP69 Breakthrough: cost 1, lose 1 HP then 9 damage to all enemies, 13 upgraded", () =>
        {
            var s = Start(package); Submit(s, "Breakthrough.AddToHand"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(79L, Hp(s), "one HP lost before the attack"); Equal(31L, EnemyHp(s), "every target took 9");
            Submit(s, "Breakthrough.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(78L, Hp(s), "the upgrade does not change the HP loss"); Equal(18L, EnemyHp(s), "upgraded Breakthrough deals 13");
        });
        Own("base-spire:conflagration", "CP70 Conflagration: cost 1 for four hits of 2 to all enemies, five when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Conflagration.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(32L, EnemyHp(s), "four hits of two"); Equal(2L, Energy(s), "one energy spent");
        });
        Own("base-spire:dismantle", "CP71 Dismantle: cost 1 for 8 damage, twice against a vulnerable target, 10 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Dismantle.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(32L, EnemyHp(s), "without vulnerable it hits once for 8");
        });
        Own("base-spire:molten-fist", "CP72 Molten Fist: cost 1 for 10 damage, 14 upgraded, doubling vulnerable, and it exhausts", () =>
        {
            var s = Start(package); Submit(s, "MoltenFist.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(30L, EnemyHp(s), "no vulnerable to amplify means plain damage"); Equal(0L, PowerStacks(s, "base-spire:vulnerable-power"), "nothing applied");
            Submit(s, "MoltenFist.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(16L, EnemyHp(s), "upgraded Molten Fist deals 14"); Equal(2, Members(s, "ExhaustPile").Count, "both copies exhausted");
        });

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }
}
