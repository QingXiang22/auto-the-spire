using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunRelicBatch6Checks(DslPackage package, Action<string, Action> check)
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

        Case("CP97: the end-of-turn block relic only fires when the owner ends the turn without block", () =>
        {
            var s = Start(package); Submit(s, "Orichalcum.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Block", Value.Of(4L));
            Submit(s, "EndDevelopmentTurn");
            Equal(4L, Block(s), "block was held, so nothing was granted");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(6L, Block(s), "a turn ended without block gains the unpowered six");
        });
        Case("CP98: the max-HP pickup relic raises both maximum and current HP", () =>
        {
            var s = Start(package); Submit(s, "Strawberry.Obtain");
            Equal(87L, Field(s, "MaxHp"), "max HP raised by seven"); Equal(87L, Hp(s), "current HP healed by the same amount");
        });
        Case("CP99: the combat-end heal relic only heals when HP is at or below the threshold", () =>
        {
            string Finish(long startHp)
            {
                var s = Start(package); Submit(s, "MeatOnTheBone.Obtain"); Submit(s, "AddDevelopmentTarget");
                Submit(s, "BeginDevelopmentCombat");
                Set(s, Player(s), "Player", "Hp", Value.Of(startHp));
                Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1L)); Set(s, FirstTarget(s), "Enemy", "Block", Value.Of(0L));
                Submit(s, "BeginDevelopmentTurn"); Submit(s, "Strike.AddToHand"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
                return Hp(s).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            Equal("52", Finish(40), "at exactly half HP the twelve point heal applies");
            Equal("41", Finish(41), "above half HP nothing is healed");
        });

        Own("base-spire:orichalcum", "CP100 Orichalcum: six unpowered block at the end of an empty-block turn", () =>
        {
            var s = Start(package); Submit(s, "Orichalcum.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(6L, Block(s), "six block after the turn ends");
        });
        Own("base-spire:pendulum", "CP101 Pendulum: one card every three owner turn starts", () =>
        {
            var s = Start(package); Submit(s, "Pendulum.Obtain"); Submit(s, "BeginDevelopmentCombat");
            var hands = new List<int>();
            for (var turn = 0; turn < 3; turn++)
            {
                Submit(s, "BeginDevelopmentTurn"); hands.Add(Members(s, "Hand").Count); Submit(s, "EndDevelopmentTurn");
            }
            Equal(0, hands[0], "turn one draws nothing"); Equal(0, hands[1], "turn two draws nothing"); Equal(1, hands[2], "turn three draws one");
            Equal(0L, Counter(s), "the counter wraps at three");
        });
        Own("base-spire:strawberry", "CP102 Strawberry: plus seven maximum HP on pickup", () =>
        {
            var s = Start(package); Submit(s, "Strawberry.Obtain");
            Equal(87L, Field(s, "MaxHp"), "max HP"); Equal(87L, Hp(s), "current HP");
            Submit(s, "BeginDevelopmentCombat"); Equal(87L, Hp(s), "the increase survives the next combat start");
        });
        Own("base-spire:meat-on-the-bone", "CP103 Meat on the Bone: twelve HP healed after a low-HP victory", () =>
        {
            var s = Start(package); Submit(s, "MeatOnTheBone.Obtain"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Set(s, Player(s), "Player", "Hp", Value.Of(20L));
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1L));
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "Strike.AddToHand"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal("victory", Outcome(s), "the kill ends the combat"); Equal(32L, Hp(s), "twelve HP healed");
        });

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }
}
