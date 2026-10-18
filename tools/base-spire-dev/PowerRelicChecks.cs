using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunPowerChecks(DslPackage package, Action<string, Action> check)
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
        const string probes = """
            action GrantStrength { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" kind = "damage-additive" stacks = 3 } } } }
            action GrantDexterity { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:dexterity-power" kind = "block-additive" stacks = 2 } } } }
            """;

        Case("CP26: vulnerable multiplies later powered damage and its stacks stay until something removes them", () =>
        {
            var s = Start(package); Submit(s, "Bash.AddToHand"); Submit(s, "Strike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:bash"));
            Equal(32L, EnemyHp(s), "Bash hits before its own vulnerable lands"); Equal(2L, PowerStacks(s, "base-spire:vulnerable-power"), "two vulnerable stacks on the target");
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(23L, EnemyHp(s), "a following strike deals 6 x 1.5 rounded down"); Equal(2L, PowerStacks(s, "base-spire:vulnerable-power"), "no turn-end tick-down is migrated");
        });
        Case("CP27: strength is additive for powered card damage only", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("strength-probe.spire", probes);
            var s = Start(copy.Load()); Submit(s, "Strike.AddToHand"); Submit(s, "MercuryHourglass.Obtain"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "GrantStrength"); Submit(s, "BeginDevelopmentTurn");
            Equal(37L, EnemyHp(s), "the turn-start relic dealt its unpowered 3 first");
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(28L, EnemyHp(s), "the card attack gets 6 + 3 strength");
        });
        Case("CP28: dexterity is additive for powered card block only", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("dexterity-probe.spire", probes);
            var s = Start(copy.Load()); Submit(s, "Defend.AddToHand"); Submit(s, "Anchor.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "GrantDexterity"); Submit(s, "BeginDevelopmentTurn");
            Equal(10L, Block(s), "the unpowered relic block ignores dexterity");
            Set(s, Player(s), "Player", "Block", Value.Of(0L));
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(7L, Block(s), "the card block gets 5 + 2 dexterity");
        });
        Case("CP29: applying the same power again merges stacks instead of creating a second instance", () =>
        {
            var s = Start(package); Submit(s, "Uppercut.AddToHand"); Submit(s, "Uppercut.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            foreach (var card in Hand(s).ToArray())
            {
                Set(s, Player(s), "Player", "Energy", Value.Of(3L));
                Submit(s, "PlayAttackFromHand", card);
            }
            Equal(2L, PowerStacks(s, "base-spire:weak-power"), "weak merged into one instance"); Equal(2L, PowerStacks(s, "base-spire:vulnerable-power"), "vulnerable merged into one instance");
            Equal(2, PowersOf(s), "two power instances for two definitions");
        });
        Case("CP30: shockwave debuffs every development target and exhausts", () =>
        {
            var s = Start(package); Submit(s, "Shockwave.AddToHand"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            var targets = Targets(s);
            Equal(3L, PowerStacksFor(s, "base-spire:weak-power", targets[0]), "first target weakened");
            Equal(3L, PowerStacksFor(s, "base-spire:vulnerable-power", targets[1]), "second target vulnerable");
            Equal(1, Members(s, "ExhaustPile").Count, "shockwave exhausted");
        });

        Case("CP35: debuff stacks tick down at the end of the turn and the instance disappears at zero", () =>
        {
            var s = Start(package); Submit(s, "Bash.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(2L, PowerStacks(s, "base-spire:vulnerable-power"), "two stacks applied");
            Submit(s, "EndDevelopmentTurn"); Equal(1L, PowerStacks(s, "base-spire:vulnerable-power"), "one stack ticked down");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(0L, PowerStacks(s, "base-spire:vulnerable-power"), "the last stack is gone"); Equal(0, PowersOf(s), "the zero-stack instance was removed");
            Submit(s, "BeginDevelopmentTurn"); Equal(3L, Energy(s), "the turn cycle still works after the power was removed");
        });
        Case("CP36: a power card applies its effect and then leaves play instead of reaching a pile", () =>
        {
            var s = Start(package); Submit(s, "Inflame.AddToHand"); Submit(s, "Strike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:inflame"));
            Equal(2L, PowerStacks(s, "base-spire:strength-power"), "the power card granted its strength");
            Equal(1, Members(s, "RemovedFromCombat").Count, "the power card left play"); Equal(3, Members(s, "DiscardPile").Count, "it is not in the discard pile");
            Equal(0, Members(s, "ExhaustPile").Count, "it is not exhausted");
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(32L, EnemyHp(s), "the granted strength applies to the next attack");
        });

        Own("base-spire:bash", "CP31 Bash: cost 2 for 8 damage plus 2 vulnerable, 10 and 3 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Bash.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(32L, EnemyHp(s), "normal Bash deals 8"); Equal(2L, PowerStacks(s, "base-spire:vulnerable-power"), "and applies 2 vulnerable");
            Submit(s, "Bash.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(17L, EnemyHp(s), "upgraded Bash deals 10 x 1.5 against the vulnerable target"); Equal(5L, PowerStacks(s, "base-spire:vulnerable-power"), "and adds 3 more vulnerable");
        });
        Own("base-spire:uppercut", "CP32 Uppercut: cost 2 for 13 damage plus weak and vulnerable, 2 of each when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Uppercut.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(27L, EnemyHp(s), "normal Uppercut deals 13"); Equal(1L, PowerStacks(s, "base-spire:weak-power"), "one weak"); Equal(1L, PowerStacks(s, "base-spire:vulnerable-power"), "one vulnerable");
            Submit(s, "Uppercut.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(8L, EnemyHp(s), "upgraded Uppercut deals 13 x 1.5"); Equal(3L, PowerStacks(s, "base-spire:weak-power"), "weak stacks merge to three");
        });
        Own("base-spire:shockwave", "CP33 Shockwave: cost 2, exhaust, 3 weak and vulnerable to all enemies, 5 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Shockwave.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(3L, PowerStacks(s, "base-spire:weak-power"), "normal Shockwave applies three"); Equal(3L, PowerStacks(s, "base-spire:vulnerable-power"), "of both debuffs");
            Submit(s, "Shockwave.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(8L, PowerStacks(s, "base-spire:weak-power"), "upgraded Shockwave adds five more"); Equal(2, Members(s, "ExhaustPile").Count, "both copies exhausted");
        });

        Own("base-spire:inflame", "CP37 Inflame: cost 1 power card for 2 strength, 3 when upgraded, leaving play", () =>
        {
            var s = Start(package); Submit(s, "Inflame.AddToHand"); Submit(s, "Strike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:inflame"));
            Equal(2L, PowerStacks(s, "base-spire:strength-power"), "normal Inflame grants two strength"); Equal(2L, Energy(s), "one energy spent");
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(32L, EnemyHp(s), "6 + 2 strength"); Equal(1, Members(s, "RemovedFromCombat").Count, "one power card left play");
            Submit(s, "Inflame.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(5L, PowerStacks(s, "base-spire:strength-power"), "upgraded Inflame merges three more strength"); Equal(2, Members(s, "RemovedFromCombat").Count, "both copies left play");
        });

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }

    private static long PowerStacks(Session s, string definition) => PowerStacksFor(s, definition, null);
    private static long PowerStacksFor(Session s, string definition, InstanceId? holder)
    {
        var type = s.RuleSet.TypeByQualifiedId("base-spire:Power");
        foreach (var power in s.State.AllInstances(type.Id))
        {
            if (s.State.GetField(power, type.FieldIndex("DefinitionId")).AsString() != definition) continue;
            if (holder is { } expected)
            {
                var bound = s.State.GetField(power, type.FieldIndex("HolderEnemy"));
                if (bound.Kind != ValueKind.Ref || bound.AsRef() != expected) continue;
            }
            return s.State.GetField(power, type.FieldIndex("Stacks")).AsInt();
        }
        return 0L;
    }
    private static int PowersOf(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Power").Id).Count;
}
