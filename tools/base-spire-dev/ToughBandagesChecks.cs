using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunToughBandagesChecks(DslPackage package, Action<string, Action> check)
    {
        const string id = "base-spire:tough-bandages";
        var names = new List<string>();
        void Case(string name, Action body) { check(name, body); names.Add(name); }
        using var copy = new Copy(package.Root);
        copy.Include("bandages-probes.spire", """
            action DiscardOne { role = player parameters = { card = Ref(Card) }
              candidates = { parameter = card query = collection(Hand) } body = {
                call = { procedure = DiscardCombatCard args = { owner = first(type(Player)) card = card } }
            } }
            action MoveOne { role = player parameters = { card = Ref(Card) }
              candidates = { parameter = card query = collection(Hand) } body = {
                call = { procedure = MoveCombatCard args = { owner = first(type(Player)) card = card to = "discard" reason = "probe" } }
            } }
            action BandageModifiers { role = player body = {
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:dexterity-power" stacks = 20 } }
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:frail-power" stacks = 20 } }
            } }
            action OtherBandages { role = player locals = { other = Binding } body = {
              create = { type = Player scope = frameScope() into = other fields = { Owner = participant(2) } }
              call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:tough-bandages" } }
            } }
            action DiscardOther { role = player parameters = { card = Ref(Card) }
              candidates = { parameter = card query = collection(Hand) } body = {
                call = { procedure = DiscardCombatCard args = { owner = first(skip(type(Player), 1)) card = card } }
            } }
            action SecondBandages { role = player body = {
              call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:tough-bandages" } }
            } }
            """);
        var variant = copy.Load();
        Session Combat(bool obtain = true)
        {
            var s = Start(variant); if (obtain) Submit(s, "ToughBandages.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); return s;
        }
        Case("BANDAGES active discard grants three unpowered block per card and respects the cap", () =>
        {
            var s = Combat(); Submit(s, "BandageModifiers");
            var one = IcAdd(s, "StrikeIronclad"); var two = IcAdd(s, "DefendIronclad");
            var draws = s.Random.TotalDraws; Submit(s, "DiscardHand");
            Equal(6L, Block(s), "two active discards, unaffected by Dexterity or Frail");
            True(Members(s, "DiscardPile").Contains(one) && Members(s, "DiscardPile").Contains(two), "both cards actually moved");
            Equal(draws, s.Random.TotalDraws, "discard reaction consumes no random values");
            Set(s, Player(s), "Player", "Block", Value.Of(998L));
            Submit(s, "DiscardOne", IcAdd(s, "StrikeIronclad")); Equal(999L, Block(s), "shared block cap");
        });
        Case("BANDAGES normal movement, played-card movement and end-turn flush do not trigger", () =>
        {
            var s = Combat(); Submit(s, "MoveOne", IcAdd(s, "StrikeIronclad"));
            Equal(0L, Block(s), "ordinary movement to discard is silent");
            IcPlay(s, IcAdd(s, "DefendIronclad")); Equal(5L, Block(s), "only the card's block, no discard reward");
            IcAdd(s, "StrikeIronclad"); IcAdd(s, "DefendIronclad");
            Submit(s, "EndDevelopmentTurn"); Equal(5L, Block(s), "flush moves cards without active-discard rewards");
            Equal(0, Hand(s).Count, "flush actually empties hand");
            Submit(s, "BeginDevelopmentTurn"); Equal(0L, Block(s), "no deferred or repeated reward");
        });
        Case("BANDAGES inactive instances, other holders and the enemy phase are isolated", () =>
        {
            foreach (var mode in new[] { "absent", "removed", "melted", "enemy", "outside" })
            {
                var s = Combat(mode != "absent");
                if (mode == "removed") Submit(s, "RemoveOwnedRelic", Members(s, "Relics").Single());
                if (mode == "melted") Set(s, Members(s, "Relics").Single(), "Relic", "IsMelted", Value.Of(true));
                if (mode == "enemy") Set(s, Player(s), "Player", "EnemyPhase", Value.Of(true));
                if (mode == "outside") Submit(s, "EndDevelopmentCombat");
                Submit(s, "DiscardOne", IcAdd(s, "StrikeIronclad")); Equal(0L, Block(s), mode + " does not trigger");
            }
            var other = Combat(false); Submit(other, "OtherBandages");
            Submit(other, "DiscardOne", IcAdd(other, "StrikeIronclad")); Equal(0L, Block(other), "another holder's relic does not reward our card");
            Submit(other, "RemoveOwnedRelic", Members(other, "Relics").Single()); Submit(other, "ToughBandages.Obtain");
            var card = IcAdd(other, "StrikeIronclad");
            Set(other, card, "Card", "Owner", Value.OfParticipant(new ParticipantId(2)));
            var holder = other.State.AllInstances(other.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Last();
            Set(other, holder, "Player", "CombatActive", Value.Of(true));
            Submit(other, "DiscardOther", card); Equal(0L, Block(other), "our relic does not reward another holder's card");
        });
        Case("BANDAGES duplicate instances react once each and development obtain checks its own holder", () =>
        {
            var s = Combat(false); Submit(s, "OtherBandages"); Submit(s, "ToughBandages.Obtain");
            True(!Legal(s, "ToughBandages.Obtain"), "own held definition prevents duplicate development obtain");
            Submit(s, "SecondBandages"); Submit(s, "DiscardOne", IcAdd(s, "StrikeIronclad"));
            Equal(6L, Block(s), "two own instances contribute independently; other holder contributes nothing");
        });
        Case("BANDAGES discard callbacks see committed movement and nested events finish before the parent", () =>
        {
            using var ordered = new Copy(package.Root);
            ordered.Include("bandages-order.spire", """
                procedure NestedDiscard { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = not(equal(pureCall(CombatCardPile, card), "discard")) then = { fail = { message = "discard notified before movement" } } }
                  if = { condition = equal(field(card, Card.DefinitionId), "base-spire:strike-ironclad") then = {
                    call = { procedure = DiscardCombatCard args = { owner = owner card = first(collection(Hand)) } }
                    if = { condition = not(equal(field(owner, Player.Block), 3)) then = { fail = { message = "child reward did not finish before parent" } } }
                  } }
                } }
                subscription NestedDiscard { event = CardDiscarded reaction = NestedDiscard priority = -1 }
                procedure ObserveBandages { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = equal(field(card, Card.DefinitionId), "base-spire:defend-ironclad") then = {
                    if = { condition = not(equal(field(owner, Player.Block), 3)) then = { fail = { message = "child reward missing or duplicated" } } }
                  } else = {
                    if = { condition = not(equal(field(owner, Player.Block), 6)) then = { fail = { message = "parent reward missing or duplicated" } } }
                  } }
                  set = { target = owner field = Player.Gold value = add(field(owner, Player.Gold), 1) }
                } }
                subscription ObserveBandages { event = CardDiscarded reaction = ObserveBandages priority = 101 }
                action DiscardFirst { role = player body = { call = { procedure = DiscardCombatCard args = { owner = first(type(Player)) card = first(collection(Hand)) } } } }
                """);
            var s = Start(ordered.Load()); Submit(s, "ToughBandages.Obtain"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var parent = IcAdd(s, "StrikeIronclad"); var child = IcAdd(s, "DefendIronclad"); Submit(s, "DiscardFirst");
            Equal(6L, Block(s), "one reward for each nested discard"); Equal(101L, Field(s, "Gold"), "both late callbacks completed");
            True(Members(s, "DiscardPile").Contains(parent) && Members(s, "DiscardPile").Contains(child), "both actual cards in discard");
        });
        return new Dictionary<string, string[]>(StringComparer.Ordinal) { [id] = names.ToArray() };
    }
}
