using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCharonsAshesChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        using var probes = new Copy(package.Root);
        probes.Include("charons-ashes-probes.spire", """
            action ExhaustCard {
              role = player
              parameters = { card = Ref(Card) }
              candidates = { parameter = card query = collection(Hand) }
              body = {
                call = { procedure = ExhaustCombatCard args = { owner = first(type(Player)) card = card } }
              }
            }
            action ObtainSecondAshes {
              role = player
              body = {
                call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:charons-ashes" } }
              }
            }
            action MakeOtherOwner {
              role = player
              locals = { other = Binding }
              body = {
                create = { type = Player scope = frameScope() into = other fields = { Owner = participant(2) } }
                call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:charons-ashes" } }
              }
            }
            procedure SkipThird {
              pure = true parameters = { value = Bool target = Ref(Enemy) } result = Bool
              body = { return = { value = and(value, not(equal(target, first(skip(type(Enemy), 2))))) } }
            }
            subscription SkipThird { event = ShouldAllowHittingEnemy reaction = SkipThird }
            action PrepareDamageModifiers { role = player body = {
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = 10 } }
              call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:vulnerable-power" stacks = 3 } }
            } }
            action ExhaustOtherCard {
              role = player parameters = { card = Ref(Card) }
              candidates = { parameter = card query = collection(Hand) }
              body = { call = { procedure = ExhaustCombatCard args = { owner = first(skip(type(Player), 1)) card = card } } }
            }
            """);
        var configured = probes.Load();

        Case("RELIC CharonsAshes: an owned exhaustion damages every hittable enemy once and exhausts after the move", () =>
        {
            var s = Start(configured);
            Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "CharonsAshes.Obtain"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            foreach (var enemy in Targets(s)) Set(s, enemy, "Enemy", "Hp", Value.Of(100L));
            Submit(s, "PrepareDamageModifiers");
            var card = IcAdd(s, "DefendIronclad"); Submit(s, "ExhaustCard", card);
            True(Members(s, "ExhaustPile").Contains(card), "CardExhausted sees the card in ExhaustPile");
            Equal(97L, IntField(s, Targets(s)[0], "Enemy", "Hp"), "first hittable enemy takes three unpowered damage");
            Equal(97L, IntField(s, Targets(s)[1], "Enemy", "Hp"), "second hittable enemy takes three unpowered damage");
            Equal(100L, IntField(s, Targets(s)[2], "Enemy", "Hp"), "living but unhittable enemy is skipped");
        });

        Case("RELIC CharonsAshes: Fiend Fire nested exhausts trigger each event, then final-kill damage settles victory", () =>
        {
            var s = Start(configured);
            Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget"); Submit(s, "CharonsAshes.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            foreach (var enemy in Targets(s)) Set(s, enemy, "Enemy", "Hp", Value.Of(9L));
            Set(s, Player(s), "Player", "Energy", Value.Of(20L));
            IcAdd(s, "StrikeIronclad"); IcAdd(s, "DefendIronclad"); var fiend = IcAdd(s, "FiendFire");
            IcPlay(s, fiend);
            Equal(3, Members(s, "ExhaustPile").Count, "two original hand cards and Fiend Fire exhaust exactly once each");
            Equal(0L, IntField(s, Targets(s)[0], "Enemy", "Hp"), "first target receives nested and parent exhaust damage");
            Equal(0L, IntField(s, Targets(s)[1], "Enemy", "Hp"), "second target receives nested and parent exhaust damage");
            Equal("victory", Outcome(s), "final exhaustion damage completes victory");
        });

        Case("RELIC CharonsAshes: removed, melted, other-owner and empty enemy scopes do not trigger", () =>
        {
            foreach (var inactive in new[] { "removed", "melted" })
            {
                var s = Start(configured); Submit(s, "AddDevelopmentTarget"); Submit(s, "CharonsAshes.Obtain");
                var relic = Members(s, "Relics").Single();
                if (inactive == "removed") Submit(s, "RemoveOwnedRelic", relic);
                else Set(s, relic, "Relic", "IsMelted", Value.Of(true));
                Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(100L));
                var card = IcAdd(s, "DefendIronclad"); Submit(s, "ExhaustCard", card);
                Equal(100L, EnemyHp(s), inactive + " CharonsAshes does not react");
            }

            var other = Start(configured); Submit(other, "AddDevelopmentTarget"); Submit(other, "MakeOtherOwner");
            Submit(other, "BeginDevelopmentCombat"); Submit(other, "BeginDevelopmentTurn");
            Set(other, FirstTarget(other), "Enemy", "Hp", Value.Of(100L));
            var ownCard = IcAdd(other, "DefendIronclad"); Submit(other, "ExhaustCard", ownCard);
            Equal(100L, EnemyHp(other), "another player's relic cannot react to this owner's exhaustion");
            Submit(other, "RemoveOwnedRelic", Members(other, "Relics").Single());
            Submit(other, "CharonsAshes.Obtain");
            var otherCard = IcAdd(other, "DefendIronclad");
            Set(other, otherCard, "Card", "Owner", Value.OfParticipant(new ParticipantId(2)));
            Submit(other, "ExhaustOtherCard", otherCard);
            Equal(100L, EnemyHp(other), "this owner's relic cannot react to another player's card exhaustion");

            var empty = Start(configured); Submit(empty, "CharonsAshes.Obtain"); Submit(empty, "BeginDevelopmentCombat"); Submit(empty, "BeginDevelopmentTurn");
            var emptyCard = IcAdd(empty, "DefendIronclad"); Submit(empty, "ExhaustCard", emptyCard);
            True(Members(empty, "ExhaustPile").Contains(emptyCard), "empty enemy scope still completes exhaustion");

            var excluded = Start(configured); Submit(excluded, "CharonsAshes.Obtain");
            Submit(excluded, "AddDevelopmentTarget"); Submit(excluded, "AddDevelopmentTarget"); Submit(excluded, "AddDevelopmentTarget");
            Submit(excluded, "BeginDevelopmentCombat"); Submit(excluded, "BeginDevelopmentTurn");
            Set(excluded, Targets(excluded)[0], "Enemy", "Hp", Value.Of(0L));
            Set(excluded, Targets(excluded)[1], "Enemy", "Hp", Value.Of(0L));
            Submit(excluded, "ExhaustCard", IcAdd(excluded, "DefendIronclad"));
            Equal(40L, IntField(excluded, Targets(excluded)[2], "Enemy", "Hp"), "only living target denies hitting");
            Equal(0, Members(excluded, "DamageResults").Count, "dead and unhittable targets produce no damage results");
        });

        Case("RELIC CharonsAshes: each active instance reacts independently without consuming randomness", () =>
        {
            var s = Start(configured); Submit(s, "AddDevelopmentTarget"); Submit(s, "CharonsAshes.Obtain"); Submit(s, "ObtainSecondAshes");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(100L));
            var draws = s.Random.TotalDraws; var card = IcAdd(s, "DefendIronclad"); Submit(s, "ExhaustCard", card);
            Equal(94L, EnemyHp(s), "both active instances react to the same event");
            Equal(draws, s.Random.TotalDraws, "CharonsAshes does not consume randomness");
        });

        Case("RELIC CharonsAshes: damage callbacks observe exhausted cards and a committed unpowered batch before final settlement", () =>
        {
            using var ordered = new Copy(package.Root);
            ordered.Include("observe-ashes.spire", """
                procedure ObserveAshDamage { parameters = { result = Ref(DamageResult) } body = {
                  if = { condition = not(isNone(field(result, DamageResult.Batch))) then = {
                    if = { condition = not(and(equal(count(collection(ExhaustPile)), 1), equal(count(collection(InPlay)), 0))) then = { fail = { message = "damage notified before exhaust movement" } } }
                    if = { condition = exists(where(type(Enemy), e, greater(field(e, Enemy.Hp), 0))) then = { fail = { message = "damage notified before every target committed" } } }
                    if = { condition = or(field(result, DamageResult.Powered), or(not(isNone(field(result, DamageResult.Card))), not(isNone(field(result, DamageResult.Attack))))) then = { fail = { message = "exhaust damage inherited an attack or card" } } }
                    if = { condition = not(field(first(type(Player)), Player.CombatActive)) then = { fail = { message = "combat closed inside damage callbacks" } } }
                  } }
                } }
                subscription ObserveAshDamage { event = DamageResolved reaction = ObserveAshDamage }
                procedure AfterAshes { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = not(field(owner, Player.CombatActive)) then = { fail = { message = "combat closed before remaining exhaust listeners" } } }
                  set = { target = owner field = Player.Gold value = add(field(owner, Player.Gold), 1) }
                } }
                subscription AfterAshes { event = CardExhausted reaction = AfterAshes priority = 101 }
                """);
            var s = Start(ordered.Load()); Submit(s, "CharonsAshes.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            foreach (var enemy in Targets(s)) Set(s, enemy, "Enemy", "Hp", Value.Of(3L));
            IcPlay(s, IcAdd(s, "Impervious"));
            Equal(100L, Field(s, "Gold"), "late exhaust listener completes before victory");
            Equal(2, Members(s, "DamageResults").Count, "one result per target");
            Equal(30L, Block(s), "card effect resolves before its exhaustion");
            Equal("victory", Outcome(s), "victory follows the entire played-card boundary");
        });

        Case("RELIC CharonsAshes: reentrant exhaustion finishes the child event before the parent and ordinary movement is silent", () =>
        {
            using var nested = new Copy(package.Root);
            nested.Include("nested-ashes.spire", """
                procedure ExhaustAnother { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = equal(field(card, Card.DefinitionId), "base-spire:impervious") then = {
                    call = { procedure = ExhaustCombatCard args = { owner = owner card = first(collection(Hand)) } }
                  } }
                } }
                subscription ExhaustAnother { event = CardExhausted reaction = ExhaustAnother priority = 99 }
                """);
            var s = Start(nested.Load()); Submit(s, "CharonsAshes.Obtain"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var child = IcAdd(s, "DefendIronclad"); IcPlay(s, IcAdd(s, "Impervious"));
            Equal(34L, EnemyHp(s), "child and parent exhaust events each cause exactly three damage");
            True(Members(s, "ExhaustPile").Contains(child), "child exhaustion completed");
            IcPlay(s, IcAdd(s, "DefendIronclad"));
            Equal(34L, EnemyHp(s), "normal play-result movement cannot trigger exhaust damage");
            Equal(2, Members(s, "DamageResults").Count, "only two exhaustion events create damage");
        });

        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:charons-ashes"] = witnesses.ToArray() };
    }
}
