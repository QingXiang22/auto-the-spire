using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunEffectListenerChecks(DslPackage package, Action<string, Action> check)
    {
        check("HOOK relic completion follows acquisition order rather than content declaration order", () =>
        {
            foreach (var meatFirst in new[] { false, true })
            {
                var s = Start(package);
                foreach (var name in meatFirst ? new[] { "MeatOnTheBone", "BlackBlood" } : new[] { "BlackBlood", "MeatOnTheBone" }) Submit(s, name + ".Obtain");
                Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                Set(s, Player(s), "Player", "Hp", Value.Of(35L)); Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1L));
                IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(meatFirst ? 59L : 47L, Hp(s), "earlier healing determines later threshold qualification");
            }
        });
        check("HOOK removing or melting a snapshotted relic cancels its later notification and private state is cleaned", () =>
        {
            foreach (var melt in new[] { false, true })
            {
                using var copy = new Copy(package.Root);
                copy.Include("cancel-listener.spire", """
                    procedure Cancel { parameters = { owner = Ref(Player) } body = {
                      MUTATION
                    } }
                    subscription Cancel { event = AfterSideTurnStarted reaction = Cancel priority = 99 }
                    """.Replace("MUTATION", melt
                        ? "set = { target = first(collection(Relics)) field = Relic.IsMelted value = true }"
                        : "call = { procedure = RemoveRelic args = { instance = first(collection(Relics)) } }"));
                var s = Start(copy.Load()); Submit(s, "Lantern.Obtain"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                Equal(3L, Energy(s), "cancelled Lantern cannot gain energy");
            }
            var state = Start(package); Submit(state, "ArtOfWar.Obtain");
            var type = state.RuleSet.TypeByQualifiedId("base-spire:ArtOfWar.State");
            Equal(1, state.State.AllInstances(type.Id).Count, "ability state exists only on acquisition");
            Submit(state, "RemoveOwnedRelic", Members(state, "Relics").Single()); Equal(0, state.State.AllInstances(type.Id).Count, "removal destroys attached private state");
        });
        check("HOOK Power notifications follow instance order and preserve nested exhaust/draw effects", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("observe-power-order.spire", """
                procedure ObserveDrawBlock { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  set = { target = owner field = Player.Gold value = field(owner, Player.Block) }
                } }
                subscription ObserveDrawBlock { event = AfterCardDrawn reaction = ObserveDrawBlock }
                """);
            foreach (var drawFirst in new[] { false, true })
            {
                var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                Set(s, Player(s), "Player", "Energy", Value.Of(30L));
                foreach (var name in drawFirst ? new[] { "DarkEmbrace", "FeelNoPain" } : new[] { "FeelNoPain", "DarkEmbrace" }) IcPlay(s, IcAdd(s, name));
                IcPlay(s, IcAdd(s, "Hellraiser")); var top = IcAdd(s, "StrikeIronclad"); IcMove(s, top, "Hand", "DrawPile");
                Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1L));
                IcPlay(s, IcAdd(s, "Impervious"));
                Equal("victory", Outcome(s), "DarkEmbrace draws and Hellraiser automatically attacks");
                Equal(33L, Block(s), "exhaust reactions finish despite nested final kill");
                Equal(drawFirst ? 30L : 33L, Field(s, "Gold"), "draw callback sees block according to actual Power acquisition order");
            }
        });
        check("HOOK modifier guards are read-only and Thrash preview uses the same Power/relic chain", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("readonly-modifiers.spire", """
                action ModifierPreview { role = player requires = and(equal(foldEvent(ModifyAttackAdditive, 6, first(type(Player)), first(collection(Hand)), none), 11), and(equal(foldEvent(ModifyMaxEnergy, 3, first(type(Player))), 3), equal(foldEvent(ModifyCardCost, 2, first(type(Player)), first(collection(Hand))), 2))) body = { } }
                """);
            var s = Start(copy.Load()); Submit(s, "StrikeDummy.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Energy", Value.Of(30L)); IcPlay(s, IcAdd(s, "Inflame")); IcAdd(s, "StrikeIronclad");
            var hash = s.State.StateHash(); var version = s.StateVersion; var draws = s.Random.TotalDraws;
            for (var i = 0; i < 3; i++) { True(Legal(s, "ModifierPreview"), "pure modifiers available"); s.Observe(s.Participant(0), "base-spire:PlayerView"); }
            Equal(hash, s.State.StateHash(), "query state hash unchanged"); Equal(version, s.StateVersion, "query version unchanged"); Equal(draws, s.Random.TotalDraws, "query random draws unchanged");
            IcPlay(s, IcAdd(s, "Thrash"));
            var thrash = Members(s, "DiscardPile").Single(c => CardDefinition(s, c) == "base-spire:thrash");
            Equal(11L, IntField(s, thrash, "Card", "DamageGrowth"), "absorbed Strike uses 6 base plus 2 Strength plus 3 relic bonus");
        });
    }
}
