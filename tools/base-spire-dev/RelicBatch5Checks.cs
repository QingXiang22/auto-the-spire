using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunRelicBatch5Checks(DslPackage package, Action<string, Action> check)
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

        Case("CP91: a combat-start power relic applies its power to the owner once per combat", () =>
        {
            var s = Start(package); Submit(s, "Vajra.Obtain"); Submit(s, "OddlySmoothStone.Obtain");
            Submit(s, "BeginDevelopmentCombat");
            Equal(1L, PowerStacks(s, "base-spire:strength-power"), "one strength"); Equal(1L, PowerStacks(s, "base-spire:dexterity-power"), "one dexterity");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat");
            Equal(1L, PowerStacks(s, "base-spire:strength-power"), "a new combat does not stack the same relic twice");
        });
        Case("CP92: a first-player-side enemy-power relic debuffs every hittable enemy", () =>
        {
            var s = Start(package); Submit(s, "BagOfMarbles.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat");
            Equal(0, PowersOf(s), "BagOfMarbles has not reached BeforeSideTurnStart yet"); Submit(s, "BeginDevelopmentTurn");
            foreach (var target in Targets(s)) Equal(1L, PowerStacksFor(s, "base-spire:vulnerable-power", target), "each target is vulnerable");
            Equal(2, PowersOf(s), "one power instance per enemy, not per relic");
        });

        Own("base-spire:vajra", "CP93 Vajra: one strength at combat start", () =>
        {
            var s = Start(package); Submit(s, "Vajra.Obtain"); Submit(s, "Strike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(33L, EnemyHp(s), "the strike deals 6 + 1 strength");
        });
        Own("base-spire:oddly-smooth-stone", "CP94 Oddly Smooth Stone: one dexterity at combat start", () =>
        {
            var s = Start(package); Submit(s, "OddlySmoothStone.Obtain"); Submit(s, "Defend.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(6L, Block(s), "Defend gives 5 + 1 dexterity");
        });
        Own("base-spire:bag-of-marbles", "CP95 Bag of Marbles: one vulnerable on every enemy at first player side start", () =>
        {
            var s = Start(package); Submit(s, "BagOfMarbles.Obtain"); Submit(s, "Strike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(31L, EnemyHp(s), "the strike deals 6 x 1.5 against the vulnerable target");
        });
        Own("base-spire:gremlin-horn", "CP96 Gremlin Horn: one energy and one card when a monster dies", () =>
        {
            var s = Start(package); Submit(s, "GremlinHorn.Obtain"); Submit(s, "Strike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(5L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(3L, Energy(s), "one energy spent, one energy gained"); Equal(0, Members(s, "Hand").Count, "ending Draw refuses the final-kill draw");
            Equal("victory", Outcome(s), "the kill still ends the combat");
            var live = Start(package); Submit(live, "GremlinHorn.Obtain"); Submit(live, "Strike.AddToHand"); Submit(live, "AddDevelopmentTarget"); Submit(live, "AddDevelopmentTarget");
            Submit(live, "BeginDevelopmentCombat"); Submit(live, "BeginDevelopmentTurn"); Set(live, FirstTarget(live), "Enemy", "Hp", Value.Of(5L)); Submit(live, "PlayAttackFromHand", Hand(live).Single());
            Equal(3L, Energy(live), "nonfinal death also grants one energy"); Equal(1, Hand(live).Count, "nonfinal death still draws one real card"); Equal("none", Outcome(live), "remaining enemy keeps combat live");
        });

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }
}
