using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunBronzeScalesChecks(DslPackage package, Action<string, Action> check)
    {
        var witness = new List<string>();
        void Case(string name, Action body) { check(name, body); witness.Add(name); }

        Case("CP86: Bronze Scales grants three thorns at combat start that retaliate unpowered", () =>
        {
            var s = Start(package); Submit(s, "BronzeScales.Obtain"); Submit(s, "TwigSlimeS.Spawn");
            Submit(s, "BeginDevelopmentCombat");
            Equal(3L, PowerStacksFor(s, "base-spire:thorns-power", null), "three thorns at combat start");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(76L, Hp(s), "the tackle hit for 4"); Equal(4L, EnemyHp(s), "the thorns dealt their unpowered 3 back first");
        });
        Case("CP87: thorns damage ignores strength", () =>
        {
            var s = Start(package); Submit(s, "BronzeScales.Obtain"); Submit(s, "Inflame.AddToHand"); Submit(s, "TwigSlimeS.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(2L, PowerStacksFor(s, "base-spire:strength-power", null), "Inflame granted strength");
            Submit(s, "EndDevelopmentTurn");
            Equal(4L, EnemyHp(s), "thorns still deal exactly 3 because they are unpowered");
        });
        Case("CP88: a melted or removed Bronze Scales stops retaliating", () =>
        {
            var removed = Start(package); Submit(removed, "BronzeScales.Obtain"); Submit(removed, "RemoveOwnedRelic", Members(removed, "Relics").Single());
            Submit(removed, "TwigSlimeS.Spawn"); Submit(removed, "BeginDevelopmentCombat"); Submit(removed, "BeginDevelopmentTurn"); Submit(removed, "EndDevelopmentTurn");
            Equal(7L, EnemyHp(removed), "no thorns after removal");
        });

        Case("CP111: the turn-end damage-back expires after the enemy phase while relic thorns persist", () =>
        {
            var flame = Start(package); Submit(flame, "FlameBarrier.AddToHand"); Submit(flame, "TwigSlimeS.Spawn");
            Submit(flame, "BeginDevelopmentCombat"); Submit(flame, "BeginDevelopmentTurn"); Submit(flame, "PlayCardFromHand", Hand(flame).Single());
            Submit(flame, "EndDevelopmentTurn");
            Equal(3L, EnemyHp(flame), "the turn-end thorns retaliated once"); Equal(0L, PowerStacksFor(flame, "base-spire:thorns-power", null), "and then expired");
            Submit(flame, "BeginDevelopmentTurn"); Submit(flame, "EndDevelopmentTurn");
            Equal(3L, EnemyHp(flame), "the next attack takes no thorns"); Equal(76L, Hp(flame), "the first hit was blocked and the second one landed");
            var scales = Start(package); Submit(scales, "BronzeScales.Obtain"); Submit(scales, "TwigSlimeS.Spawn");
            Submit(scales, "BeginDevelopmentCombat"); Submit(scales, "BeginDevelopmentTurn"); Submit(scales, "EndDevelopmentTurn");
            Submit(scales, "BeginDevelopmentTurn"); Submit(scales, "EndDevelopmentTurn");
            Equal(1L, EnemyHp(scales), "the relic thorns retaliate on every attack"); Equal(3L, PowerStacksFor(scales, "base-spire:thorns-power", null), "and stay in place");
        });

        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:bronze-scales"] = witness.ToArray() };
    }

    private static Dictionary<string, string[]> RunFlameBarrierChecks(DslPackage package, Action<string, Action> check)
    {
        var witness = new List<string>();
        void Case(string name, Action body) { check(name, body); witness.Add(name); }

        Case("CP89: Flame Barrier gains block and four damage back, and retaliates before the hit lands", () =>
        {
            var s = Start(package); Submit(s, "FlameBarrier.AddToHand"); Submit(s, "TwigSlimeS.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(12L, Block(s), "twelve block"); Equal(4L, PowerStacksFor(s, "base-spire:thorns-power", null), "four damage back"); Equal(1L, Energy(s), "two energy spent");
            Submit(s, "EndDevelopmentTurn");
            Equal(3L, EnemyHp(s), "the thorns hit the slime for 4 before the tackle"); Equal(80L, Hp(s), "the twelve block absorbed the 4 damage"); Equal(8L, Block(s), "eight block left");
        });
        Case("CP90: the upgraded Flame Barrier blocks more and returns more", () =>
        {
            var s = Start(package); Submit(s, "FlameBarrier.AddToHand"); Submit(s, "TwigSlimeS.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(16L, Block(s), "upgraded block"); Equal(6L, PowerStacksFor(s, "base-spire:thorns-power", null), "upgraded damage back");
            Submit(s, "EndDevelopmentTurn");
            Equal(1L, EnemyHp(s), "the slime took 6 thorns"); Equal(80L, Hp(s), "the sixteen block absorbed the 4 damage");
        });

        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:flame-barrier"] = witness.ToArray() };
    }
}
