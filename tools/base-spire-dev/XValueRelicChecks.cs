using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunXValueRelicChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Own(string name, Action body) { check(name, body); witnesses.Add(name); }
        Session Combat(DslPackage? variant = null, bool obtain = true)
        {
            var s = Start(variant ?? package);
            if (obtain) Submit(s, "ChemicalX.Obtain");
            Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            foreach (var c in Members(s, "DiscardPile").ToArray()) s.State.RunAtomic(() => s.State.DestroyInstance(c));
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L));
            return s;
        }
        InstanceId Top(Session s, string name)
        {
            var c = IcAdd(s, name); IcMove(s, c, "Hand", "DrawPile"); return c;
        }
        Own("CX Whirlwind: zero and positive captured X, both upgrades, every enemy and payment remain distinct from effect X", () =>
        {
            foreach (var upgraded in new[] { false, true })
                foreach (var energy in new[] { 0L, 3L })
                    foreach (var obtain in new[] { false, true })
                    {
                        var s = Combat(obtain: obtain); Submit(s, "AddDevelopmentTarget");
                        var enemies = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).ToArray();
                        Set(s, enemies[1], "Enemy", "Hp", Value.Of(1000L));
                        var c = IcAdd(s, "Whirlwind", upgraded); Set(s, Player(s), "Player", "Energy", Value.Of(energy));
                        IcPlay(s, c);
                        var expected = 1000L - (energy + (obtain ? 2 : 0)) * (upgraded ? 8 : 5);
                        foreach (var enemy in enemies) Equal(expected, IntField(s, enemy, "Enemy", "Hp"), "all enemies take modified X hits");
                        Equal(0L, Energy(s), "only original energy paid"); Equal(energy, IntField(s, c, "Card", "PaidX"), "captured X never includes the relic bonus");
                    }
        });
        Own("CX Cascade: effect X plus upgrade controls automatic card count without extra payment", () =>
        {
            foreach (var upgraded in new[] { false, true })
                foreach (var energy in new[] { 0L, 2L })
                {
                    var s = Combat(); var c = IcAdd(s, "Cascade", upgraded);
                    for (var i = 0; i < 6; i++) Top(s, "DefendIronclad");
                    Set(s, Player(s), "Player", "Energy", Value.Of(energy)); IcPlay(s, c);
                    var count = energy + 2 + (upgraded ? 1 : 0);
                    Equal(5L * count, Block(s), "exact number of automatic Defends"); Equal(6L - count, (long)Members(s, "DrawPile").Count, "unplayed cards remain");
                    Equal(0L, Energy(s), "automatic children are free"); Equal(energy, IntField(s, c, "Card", "PaidX"), "upgrade and relic never change capture");
                }
        });
        Own("CX nested autoplay: Havoc captures after parent payment and Cascade children use their own current energy", () =>
        {
            foreach (var upgraded in new[] { false, true })
            {
                var s = Combat(); var havoc = IcAdd(s, "Havoc", upgraded); var x = Top(s, "Whirlwind");
                Set(s, Player(s), "Player", "Energy", Value.Of(3L)); IcPlay(s, havoc);
                var capture = upgraded ? 3L : 2L;
                Equal(1000L - (capture + 2) * 5, EnemyHp(s), "child effect X has its own capture plus two");
                Equal(capture, Energy(s), "autoplay never pays child energy"); Equal(capture, IntField(s, x, "Card", "PaidX"), "child raw capture");
                True(Members(s, "ExhaustPile").Contains(x), "Havoc still forces exhaust");
            }
            var nested = Combat(); var cascade = IcAdd(nested, "Cascade"); Top(nested, "Bloodletting"); var child = Top(nested, "Whirlwind"); Top(nested, "DefendIronclad");
            Set(nested, Player(nested), "Player", "Energy", Value.Of(0L)); IcPlay(nested, cascade);
            Equal(980L, EnemyHp(nested), "child captures Bloodletting's two energy and gets two additional hits");
            Equal(2L, Energy(nested), "child is free"); Equal(0L, IntField(nested, cascade, "Card", "PaidX"), "parent capture stays zero");
            Equal(2L, IntField(nested, child, "Card", "PaidX"), "child has independent capture"); Equal(1, Members(nested, "DrawPile").Count, "parent count frozen before child energy gain");
        });
        Own("CX repeated attack: pay once, reuse raw capture, apply bonus once to each repetition", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "OneTwoPunch")); var x = IcAdd(s, "Whirlwind");
            Set(s, Player(s), "Player", "Energy", Value.Of(3L)); IcPlay(s, x);
            Equal(950L, EnemyHp(s), "two sets of five hits"); Equal(3L, IntField(s, x, "Card", "PaidX"), "no accumulated +2 on second play");
            Equal(0L, Energy(s), "no second payment"); Equal(1, Members(s, "DiscardPile").Count(c => c == x), "single result-pile move");
        });
        check("CX repetitions re-evaluate active listeners after the first effect removes ChemicalX", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("remove-after-x.spire", """
                procedure RemoveAfterX { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = field(card, Card.IsX) then = {
                    forEach = { query = where(collection(Relics), r, equal(field(r, Relic.DefinitionId), "base-spire:chemical-x")) item = r body = {
                      call = { procedure = RemoveRelic args = { instance = r } }
                    } }
                  } }
                } }
                subscription RemoveAfterX { event = AfterCardPlayed reaction = RemoveAfterX priority = 101 }
                """);
            var s = Combat(copy.Load()); IcPlay(s, IcAdd(s, "OneTwoPunch")); var x = IcAdd(s, "Whirlwind");
            Set(s, Player(s), "Player", "Energy", Value.Of(3L)); IcPlay(s, x);
            Equal(960L, EnemyHp(s), "five hits before removal then three after"); Equal(0, Members(s, "Relics").Count, "removed between repetitions");
            Equal(3L, IntField(s, x, "Card", "PaidX"), "capture survives removal");
        });
        check("CX removed and melted instances stop contributing; ordinary attacks never consume an X bonus", () =>
        {
            foreach (var melt in new[] { false, true })
            {
                var s = Combat(); var relic = Members(s, "Relics").Single();
                if (melt) Set(s, relic, "Relic", "IsMelted", Value.Of(true)); else Submit(s, "RemoveOwnedRelic", relic);
                var c = IcAdd(s, "Whirlwind"); Set(s, Player(s), "Player", "Energy", Value.Of(3L)); IcPlay(s, c);
                Equal(985L, EnemyHp(s), "inactive relic no longer contributes");
            }
            var nonX = Combat(); IcPlay(nonX, IcAdd(nonX, "StrikeIronclad")); Equal(994L, EnemyHp(nonX), "ordinary attack unchanged"); Equal(2L, Energy(nonX), "ordinary cost unchanged");
        });
        check("CX instance and holder isolation: direct acquisition stacks per active instance and other holders do not contribute", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("x-owners.spire", """
                action OtherOwner { role = player locals = { other = Binding } body = {
                  create = { type = Player scope = frameScope() into = other }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:chemical-x" } }
                } }
                action AnotherX { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:chemical-x" } } } }
                """);
            var s = Combat(copy.Load(), obtain: false); Submit(s, "OtherOwner");
            var a = IcAdd(s, "Whirlwind"); Set(s, Player(s), "Player", "Energy", Value.Of(0L)); IcPlay(s, a); Equal(1000L, EnemyHp(s), "other holder cannot add X");
            Submit(s, "ChemicalX.Obtain"); Submit(s, "AnotherX"); var b = IcAdd(s, "Whirlwind"); IcPlay(s, b); Equal(980L, EnemyHp(s), "two actual owned listeners each add two");
            Submit(s, "RemoveOwnedRelic", Members(s, "Relics")[1]); var c = IcAdd(s, "Whirlwind"); IcPlay(s, c); Equal(970L, EnemyHp(s), "only surviving owned instance contributes");
        });
        check("CX pure query: repeated previews leave state, version, randomness and captured X unchanged", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("x-preview.spire", """
                action PreviewX { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) }
                  requires = equal(pureCall(ResolveEnergyXValue, first(type(Player)), card), 5) body = { } }
                action PreviewNonX { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) }
                  requires = equal(pureCall(ResolveEnergyXValue, first(type(Player)), card), 0) body = { } }
                """);
            var s = Combat(copy.Load()); var x = IcAdd(s, "Whirlwind"); Set(s, x, "Card", "PaidX", Value.Of(3L));
            var hash = s.State.StateHash(); var version = s.StateVersion; var draws = s.Random.TotalDraws;
            for (var i = 0; i < 3; i++) { True(Legal(s, "PreviewX"), "same pure effect X each time"); s.Observe(s.Participant(0), "base-spire:PlayerView"); }
            Equal(hash, s.State.StateHash(), "queries never mutate state"); Equal(version, s.StateVersion, "queries never mutate version"); Equal(draws, s.Random.TotalDraws, "queries never consume random");
            IcMove(s, x, "Hand", "DrawPile"); IcAdd(s, "StrikeIronclad"); True(Legal(s, "PreviewNonX"), "non-X query has neutral result without dispatching modifiers");
        });
        Own("CX Cascade decision: pause and answer resume the original X count without replaying payment or pre-choice effects", () =>
        {
            var s = Combat(); var cascade = IcAdd(s, "Cascade"); var chosen = IcAdd(s, "StrikeIronclad"); IcAdd(s, "Anger");
            Top(s, "Brand"); Top(s, "DefendIronclad"); Top(s, "DefendIronclad"); Top(s, "Anger");
            Set(s, Player(s), "Player", "Energy", Value.Of(1L)); IcPlay(s, cascade);
            var decision = s.PendingDecision!; True(decision.Candidates.Contains(chosen), "nested Brand publishes a real hand choice");
            Equal(0L, Energy(s), "parent payment before pause"); Equal(79L, Hp(s), "Brand HP cost before pause"); Equal(1L, IntField(s, cascade, "Card", "PaidX"), "raw capture remains visible at pause");
            s.Answer(new DecisionAnswer(decision.RequestId, [chosen])); Healthy(s);
            Equal(10L, Block(s), "remaining two automatic effects run once"); Equal(79L, Hp(s), "resume never repeats Brand HP cost");
            Equal(0L, Energy(s), "resume never repeats payment"); Equal(1, Members(s, "DrawPile").Count, "exact captured X plus two cards consumed");
            Equal(4, Members(s, "CardPlays").Count, "parent and exactly three children");
        });
        Own("CX manual X still spends all energy with free-card, FreeAttack and Corruption modifiers", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("free-x.spire", """
                action Free { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = {
                  call = { procedure = AddCardCostModifier args = { card = card source = "probe:free" operation = "set" amount = 0 lifetime = "play" } }
                } }
                """);
            var s = Combat(copy.Load()); IcPlay(s, IcAdd(s, "Unrelenting")); var x = IcAdd(s, "Whirlwind");
            Submit(s, "Free", x); Set(s, Player(s), "Player", "Energy", Value.Of(2L)); IcPlay(s, x);
            Equal(966L, EnemyHp(s), "Unrelenting fourteen then four X hits"); Equal(0L, Energy(s), "free ordinary attacks do not waive X payment");
            var skill = Combat(); IcPlay(skill, IcAdd(skill, "Corruption")); var cascade = IcAdd(skill, "Cascade");
            for (var i = 0; i < 5; i++) Top(skill, "DefendIronclad");
            Set(skill, Player(skill), "Player", "Energy", Value.Of(2L)); IcPlay(skill, cascade);
            Equal(20L, Block(skill), "X skill plus two effect cards"); Equal(0L, Energy(skill), "Corruption does not waive X payment");
            True(Members(skill, "ExhaustPile").Contains(cascade), "Corruption still exhausts the X skill");
        });
        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:chemical-x"] = witnesses.ToArray() };
    }
}
