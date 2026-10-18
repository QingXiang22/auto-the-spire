using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunPotionChecks(DslPackage package, Action<string, Action> check)
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

        Case("CP104: a potion is consumed when used and the inventory has three slots", () =>
        {
            var s = Start(package); Submit(s, "BlockPotion.Obtain"); Submit(s, "EnergyPotion.Obtain"); Submit(s, "SwiftPotion.Obtain");
            Equal(3, Members(s, "Potions").Count, "three potions fit");
            Throws<EngineFaultException>(() => Submit(s, "BlockPotion.Obtain"));
            Equal(SessionStatus.Faulted, s.Status, "a fourth potion overflows the three-slot inventory");
            var clean = Start(package); Submit(clean, "EnergyPotion.Obtain"); Submit(clean, "BeginDevelopmentCombat"); Submit(clean, "BeginDevelopmentTurn");
            Submit(clean, "UsePotion", Members(clean, "Potions").Single());
            Equal(0, Members(clean, "Potions").Count, "the used potion left the inventory"); Equal(5L, Energy(clean), "the effect resolved before consumption");
        });

        Own("base-spire:fire-potion", "CP105 Fire Potion: unpowered 20 damage to one enemy", () =>
        {
            var s = Start(package); Submit(s, "FirePotion.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat");
            Submit(s, "UsePotionOnTarget", Members(s, "Potions").Single());
            Equal(20L, EnemyHp(s), "twenty unpowered damage"); Equal(0, Members(s, "Potions").Count, "consumed");
        });
        Own("base-spire:block-potion", "CP106 Block Potion: unpowered twelve block", () =>
        {
            var s = Start(package); Submit(s, "BlockPotion.Obtain"); Submit(s, "BeginDevelopmentCombat");
            Submit(s, "UsePotion", Members(s, "Potions").Single());
            Equal(12L, Block(s), "twelve block");
        });
        Own("base-spire:energy-potion", "CP107 Energy Potion: two energy", () =>
        {
            var s = Start(package); Submit(s, "EnergyPotion.Obtain"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "UsePotion", Members(s, "Potions").Single());
            Equal(5L, Energy(s), "three plus two");
        });
        Own("base-spire:swift-potion", "CP108 Swift Potion: three cards drawn", () =>
        {
            var s = Start(package); Submit(s, "SwiftPotion.Obtain"); Submit(s, "BeginDevelopmentCombat");
            Submit(s, "UsePotion", Members(s, "Potions").Single());
            Equal(3, Members(s, "Hand").Count, "three development cards drawn after a reshuffle");
        });
        Own("base-spire:fruit-juice", "CP109 Fruit Juice: five maximum HP raised and healed", () =>
        {
            var s = Start(package); Submit(s, "FruitJuice.Obtain");
            Submit(s, "UsePotion", Members(s, "Potions").Single());
            Equal(85L, Field(s, "MaxHp"), "max HP plus five"); Equal(85L, Hp(s), "current HP healed by the same amount");
        });
        Own("base-spire:blood-potion", "CP110 Blood Potion: heals twenty percent of maximum HP", () =>
        {
            var s = Start(package); Submit(s, "BloodPotion.Obtain");
            Set(s, Player(s), "Player", "Hp", Value.Of(40L));
            Submit(s, "UsePotion", Members(s, "Potions").Single());
            Equal(56L, Hp(s), "forty plus sixteen");
        });

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }
}
