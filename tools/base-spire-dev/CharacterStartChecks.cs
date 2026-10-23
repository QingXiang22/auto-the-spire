using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCharacterStartChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }
        Session Character() { var s = Start(package); Submit(s, "Ironclad.Initialize"); return s; }
        void Begin(Session s) { Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); }

        Case("IS01: source Ironclad resources and ordered deck have distinct permanent identities", () =>
        {
            var s = Character(); var deck = Members(s, "RunDeck");
            Equal(80L, Hp(s), "source current HP"); Equal(80L, Field(s, "MaxHp"), "source max HP"); Equal(99L, Field(s, "Gold"), "source gold");
            Equal(10, deck.Distinct().Count(), "ten independent permanent instances");
            var expected = Enumerable.Repeat("base-spire:strike-ironclad", 5).Concat(Enumerable.Repeat("base-spire:defend-ironclad", 4)).Append("base-spire:bash");
            True(deck.Select(c => CardDefinition(s, c)).SequenceEqual(expected), "source order 5 Strike, 4 Defend, Bash");
            True(deck.All(c => !CardUpgraded(s, c) && CardCost(s, c) == (CardDefinition(s, c) == "base-spire:bash" ? 2 : 1)), "actual definitions initialized metadata");
            Equal(10, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Count, "placeholders destroyed");
            Equal(0, Members(s, "DiscardPile").Count, "no placeholder combat deck");
            Equal("base-spire:burning-blood", s.State.GetField(Members(s, "Relics").Single(), s.RuleSet.TypeByQualifiedId("base-spire:Relic").FieldIndex("DefinitionId")).AsString(), "source starter relic");
            True(!Legal(s, "Ironclad.Initialize"), "no repeated initialization");
        });
        Case("IS02: initialization refuses existing progress and direct repeated calls", () =>
        {
            foreach (var action in new[] { "Draw", "Strike.Obtain", "Anger.AddToHand", "TheAbacus.Obtain", "AddDevelopmentTarget", "EnterDevelopmentRestSite", "BeginDevelopmentCombat" })
            { var s = Start(package); Submit(s, action); True(!Legal(s, "Ironclad.Initialize"), "existing progress: " + action); }
            var hurt = Start(package); Set(hurt, Player(hurt), "Player", "Hp", Value.Of(79L)); True(!Legal(hurt, "Ironclad.Initialize"), "cannot heal via initialization");
            using var copy = new Copy(package.Root);
            copy.Include("force-init.spire", "action ForceInit { role = player body = { call = { procedure = Ironclad.Initialize args = { owner = first(type(Player)) } } } }");
            var direct = Start(copy.Load()); Submit(direct, "Ironclad.Initialize");
            ForceRejected(direct, "Init");
            Equal(10, Members(direct, "RunDeck").Count, "no duplicate permanent cards"); Equal(1, Members(direct, "Relics").Count, "no duplicate starter relic");
        });
        Case("IS03: initial shuffle is distinct from reshuffle and hand draw precedes player-turn hooks", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("start-order.spire", """
                procedure InspectStartDraw { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = not(and(equal(field(owner, Player.Block), 0), equal(field(owner, Player.Energy), 3))) then = { fail = { message = "initial shuffle triggered Abacus or draw before energy" } } }
                } }
                subscription InspectStartDraw { event = AfterCardDrawn reaction = InspectStartDraw }
                procedure InspectStartHand { parameters = { owner = Ref(Player) } body = {
                  if = { condition = not(equal(count(collection(Hand)), 5)) then = { fail = { message = "turn hook before hand draw" } } }
                } }
                subscription InspectStartHand { event = AfterPlayerTurnStart reaction = InspectStartHand }
                """);
            var s = Start(copy.Load()); Submit(s, "Ironclad.Initialize"); Submit(s, "TheAbacus.Obtain");
            var deck = Members(s, "RunDeck").ToArray(); Begin(s);
            Equal(5, Hand(s).Count, "automatic opening five"); Equal(5, Members(s, "DrawPile").Count, "remaining five");
            Equal(0L, Block(s), "initial shuffle never triggers AfterShuffle"); True(s.Random.DrawCount("base-spire:run:shuffle") > 0, "real random stream consumed");
            True(Members(s, "RunDeck").SequenceEqual(deck), "permanent order unchanged");
            True(Hand(s).Concat(Members(s, "DrawPile")).All(c => deck.Contains(PermanentSource(s, c)) && !deck.Contains(c)), "separate combat copies with exact sources");
        });
        Case("IS04: same seed reproduces the opening and next combat rebuilds fresh copies", () =>
        {
            var one = Character(); var two = Character(); Begin(one); Begin(two);
            True(Hand(one).Select(c => CardDefinition(one, c)).SequenceEqual(Hand(two).Select(c => CardDefinition(two, c))), "same seed hand");
            var permanent = Members(one, "RunDeck").ToArray(); var old = Hand(one).Concat(Members(one, "DrawPile")).ToArray();
            Set(one, old[0], "Card", "DamageGrowth", Value.Of(9L));
            Set(one, Player(one), "Player", "Hp", Value.Of(70L));
            Submit(one, "EndDevelopmentCombat"); Submit(one, "BeginDevelopmentCombat"); Submit(one, "BeginDevelopmentTurn");
            True(old.All(c => !one.State.Exists(c)), "previous combat copies destroyed"); True(Members(one, "RunDeck").SequenceEqual(permanent), "same permanent identities");
            True(Hand(one).Concat(Members(one, "DrawPile")).All(c => IntField(one, c, "Card", "DamageGrowth") == 0), "temporary growth did not persist");
            Equal(70L, Hp(one), "manual ending did not heal or reset HP"); Equal(1, Members(one, "Relics").Count, "same held starter relic");
        });
        Case("IS05: NoDraw allows turn hand while full hand prevents another shuffle", () =>
        {
            using var copy = DrawPermissionCopy(package);
            var blocked = Start(copy.Load()); Submit(blocked, "Ironclad.Initialize"); Submit(blocked, "BeginDevelopmentCombat"); Submit(blocked, "BlockEffectDraw");
            var before = blocked.Random.DrawCount("base-spire:run:shuffle"); Submit(blocked, "BeginDevelopmentTurn");
            Equal(5, Hand(blocked).Count, "NoDraw permits automatic turn hand"); Equal(5, Members(blocked, "DrawPile").Count, "actual automatic draw");
            Equal(before, blocked.Random.DrawCount("base-spire:run:shuffle"), "no unnecessary reshuffle");
            var full = Character(); Submit(full, "BeginDevelopmentCombat"); for (var i = 0; i < 10; i++) Submit(full, "Draw");
            Submit(full, "BeginDevelopmentTurn"); Equal(10, Hand(full).Count, "full hand remains ten");
            var turns = Character(); Begin(turns); Submit(turns, "EndDevelopmentTurn"); Submit(turns, "BeginDevelopmentTurn");
            Equal(5, Hand(turns).Count, "next turn draws five too"); Equal(5, Members(turns, "DiscardPile").Count, "first hand discarded");
        });
        Case("IS06: first-turn Innate priority increases draw count past five and stops at ten", () =>
        {
            foreach (var total in new[] { 6, 12 })
            {
                var s = Character();
                for (var i = 10; i < total; i++) Submit(s, "Strike.Obtain");
                var deck = Members(s, "RunDeck").ToArray();
                foreach (var c in deck.Take(total)) Set(s, c, "Card", "Innate", Value.Of(true));
                Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat");
                var priority = Members(s, "DrawPile").Where(c => s.State.GetField(c, CardField(s, "Innate")).AsBool()).Reverse().Take(10).ToArray();
                Submit(s, "BeginDevelopmentTurn"); Equal(Math.Min(total, 10), Hand(s).Count, "Innate count with hand cap");
                True(Hand(s).SequenceEqual(priority), "each Innate move-to-top follows source traversal order");
                True(Hand(s).All(c => s.State.GetField(c, CardField(s, "Innate")).AsBool()), "all opening draws are Innate when enough exist");
            }
        });
        Case("IS07: BurningBlood heals six after an actual last kill and clamps at max HP", () =>
        {
            foreach (var hp in new[] { 60L, 78L })
            {
                var s = Character(); Begin(s); Set(s, Player(s), "Player", "Hp", Value.Of(hp));
                Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1L));
                var attack = Hand(s).First(c => CardDefinition(s, c) is "base-spire:strike-ironclad" or "base-spire:bash");
                Submit(s, "PlayAttackFromHand", attack); Equal("victory", Outcome(s), "actual kill victory");
                Equal(Math.Min(80, hp + 6), Hp(s), "source six heal capped");
                Submit(s, "CloseDevelopmentRewards"); Equal(Math.Min(80, hp + 6), Hp(s), "reward close cannot heal twice");
            }
        });
        Case("IS08: inactive and removed BurningBlood do not heal, defeat cannot revive", () =>
        {
            foreach (var removed in new[] { false, true })
            {
                var s = Character(); var relic = Members(s, "Relics").Single();
                if (removed) Submit(s, "RemoveOwnedRelic", relic); else Set(s, relic, "Relic", "IsMelted", Value.Of(true));
                Begin(s); Set(s, Player(s), "Player", "Hp", Value.Of(60L)); Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1L));
                Submit(s, "PlayAttackFromHand", Hand(s).First(c => CardDefinition(s, c) is "base-spire:strike-ironclad" or "base-spire:bash"));
                Equal(60L, Hp(s), "inactive starter relic");
            }
            var dead = Character(); Begin(dead); Set(dead, Player(dead), "Player", "Hp", Value.Of(2L));
            IcPlay(dead, IcAdd(dead, "Bloodletting")); Equal("defeat", Outcome(dead), "actual card self-damage defeat"); Equal(0L, Hp(dead), "starter relic cannot revive");
        });
        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:burning-blood"] = witnesses.ToArray() };
    }
}
