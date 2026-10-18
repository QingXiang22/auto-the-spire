using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunPermanentDeckChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        Case("PD01: obtaining permanent cards creates distinct instances before the acquisition hook", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("obtain-order.spire", """
                procedure InspectObtained {
                  parameters = { owner = Ref(Player) card = Ref(Card) }
                  body = { if = {
                    condition = or(less(collectionIndex(RunDeck, card), 0), not(field(owner, Player.UsesRunDeck)))
                    then = { fail = { message = "acquisition hook ran before permanent ownership" } }
                  } }
                }
                subscription InspectObtained { event = CardObtained reaction = InspectObtained }
                """);
            var s = Start(copy.Load()); Submit(s, "Strike.Obtain"); Submit(s, "Strike.Obtain");
            var cards = Members(s, "RunDeck");
            Equal(2, cards.Count, "two permanent cards"); True(cards[0] != cards[1], "same definition retains distinct identities");
            True(cards.All(card => CardDefinition(s, card) == "base-spire:strike-ironclad"), "actual definition identity");
            Equal(3, Members(s, "DiscardPile").Count, "permanent acquisition did not put cards into a combat pile");
            True(cards.All(card => s.State.GetField(card, CardField(s, "PermanentSource")).Kind == ValueKind.None), "permanent cards are not combat copies");
            Submit(s, "BeginDevelopmentCombat");
            True(!Legal(s, "Strike.Obtain"), "permanent acquisition is not offered during combat");
        });
        Case("PD02: smith requires upgradable permanent cards, never the development hand", () =>
        {
            var empty = Start(package); Submit(empty, "Strike.AddToHand"); Submit(empty, "EnterDevelopmentRestSite");
            True(!Legal(empty, "ForgeAtDevelopmentCampfire"), "a hand card cannot substitute for a permanent card");
            True(Legal(empty, "RestAtDevelopmentCampfire"), "rest remains available with an empty permanent deck");
            var upgraded = Start(package); Submit(upgraded, "Strike.Obtain");
            Set(upgraded, Members(upgraded, "RunDeck").Single(), "Card", "Upgraded", Value.Of(true));
            Submit(upgraded, "EnterDevelopmentRestSite");
            True(!Legal(upgraded, "ForgeAtDevelopmentCampfire"), "an already upgraded permanent deck cannot be smithed");
            var placeholder = Start(package); Submit(placeholder, "Draw");
            True(!Legal(placeholder, "UpgradeCardInHand"), "unsupported placeholder cards have no upgrade behavior");
        });
        Case("PD03: selecting the second identical permanent card upgrades only that instance", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("smith-order.spire", """
                procedure InspectSmith {
                  parameters = { owner = Ref(Player) }
                  body = { if = {
                    condition = not(and(field(owner, Player.RoomUsed), exists(where(collection(RunDeck), card, field(card, Card.Upgraded)))))
                    then = { fail = { message = "smith hook ran before the successful upgrade" } }
                  } }
                }
                subscription InspectSmith { event = AfterRestSiteSmith reaction = InspectSmith }
                """);
            var s = Start(copy.Load()); Submit(s, "Strike.Obtain"); Submit(s, "Strike.Obtain");
            var deck = Members(s, "RunDeck").ToArray();
            Submit(s, "EnterDevelopmentRestSite"); Submit(s, "ForgeAtDevelopmentCampfire");
            Equal(SessionStatus.AwaitingDecision, s.Status, "smith actually pauses");
            Equal(0L, s.PendingDecision!.Minimum, "selection may be cancelled"); Equal(1L, s.PendingDecision.Maximum, "one card maximum");
            True(s.PendingDecision.Candidates.SequenceEqual(deck), "candidate order and identities come from RunDeck");
            True(!RoomUsed(s) && PendingForge(s), "the option is unspent while waiting");
            var request = s.PendingDecision.RequestId;
            s.Answer(new DecisionAnswer(request, [deck[1]])); Healthy(s);
            True(!CardUpgraded(s, deck[0]) && CardUpgraded(s, deck[1]), "only the selected permanent instance upgraded");
            True(RoomUsed(s) && !PendingForge(s), "successful smith spends the option and clears pending state");
            True(!Legal(s, "ForgeAtDevelopmentCampfire") && !Legal(s, "RestAtDevelopmentCampfire"), "no second successful option");
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request, [deck[0]])));
            True(!CardUpgraded(s, deck[0]), "repeated answer cannot upgrade another instance");
        });
        Case("PD04: cancellation leaves the option available and invalid or stale answers preserve the choice", () =>
        {
            var s = Start(package); Submit(s, "Strike.Obtain"); Submit(s, "Strike.AddToHand");
            var permanent = Members(s, "RunDeck").Single(); var temporary = Hand(s).Single();
            Submit(s, "EnterDevelopmentRestSite"); Submit(s, "ForgeAtDevelopmentCampfire");
            var request = s.PendingDecision!.RequestId;
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request, [temporary])));
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request, [permanent, permanent])));
            Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:BeginDevelopmentCombat"), s.Participant(0), []));
            Equal(SessionStatus.AwaitingDecision, s.Status, "illegal answer and room bypass preserve the live choice");
            Equal(request, s.PendingDecision!.RequestId, "the original decision remains live");
            s.Answer(new DecisionAnswer(request, [])); Healthy(s);
            True(!RoomUsed(s) && !PendingForge(s) && !CardUpgraded(s, permanent), "cancellation consumes neither upgrade nor room option");
            Submit(s, "ForgeAtDevelopmentCampfire"); var next = s.PendingDecision!.RequestId;
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request, [permanent])));
            Equal(next, s.PendingDecision!.RequestId, "a stale answer does not replace the next choice");
            s.Answer(new DecisionAnswer(next, [])); Healthy(s);
            Submit(s, "RestAtDevelopmentCampfire"); True(RoomUsed(s), "the player can rest after cancellation");
        });
        Case("PD05: room re-entry and combat cannot reset an unfinished or spent campfire", () =>
        {
            var s = Start(package); Submit(s, "Strike.Obtain"); Submit(s, "EnterDevelopmentRestSite");
            foreach (var action in new[] { "EnterDevelopmentRestSite", "BeginDevelopmentCombat", "LeaveDevelopmentRoom", "Strike.Obtain" })
                RejectRewardAction(s, action);
            Submit(s, "RestAtDevelopmentCampfire");
            RejectRewardAction(s, "EnterDevelopmentRestSite"); RejectRewardAction(s, "BeginDevelopmentCombat");
            Submit(s, "LeaveDevelopmentRoom"); Equal("none", Room(s), "successful visit closes explicitly");
            Submit(s, "EnterDevelopmentRestSite"); True(!RoomUsed(s), "a genuinely new visit has a fresh option");
        });
        Case("PD06: smith upgrades survive two combat copies and temporary upgrades never write back", () =>
        {
            var s = Start(package); Submit(s, "Strike.Obtain"); Submit(s, "Strike.Obtain");
            var originals = Members(s, "RunDeck").ToArray();
            Submit(s, "EnterDevelopmentRestSite"); Submit(s, "ForgeAtDevelopmentCampfire");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [originals[1]])); Healthy(s);
            Submit(s, "LeaveDevelopmentRoom"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat");
            var firstCopies = Members(s, "DrawPile").ToArray();
            True(firstCopies.SequenceEqual(firstCopies.Distinct()) && firstCopies.All(card => !originals.Contains(card)), "fresh combat identities");
            Equal(originals[0], PermanentSource(s, firstCopies[0]), "first copy keeps its source identity");
            Equal(originals[1], PermanentSource(s, firstCopies[1]), "second copy keeps its source identity");
            Submit(s, "Draw"); Submit(s, "Draw"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", firstCopies[1]); Equal(31L, EnemyHp(s), "the permanent upgrade deals nine damage in combat");
            Submit(s, "UpgradeCardInHand", firstCopies[0]);
            True(!CardUpgraded(s, originals[0]) && CardUpgraded(s, firstCopies[0]), "temporary upgrade stays in the combat copy");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat");
            var secondCopies = Members(s, "DrawPile").ToArray();
            True(firstCopies.All(card => !s.State.Exists(card)), "the old combat copies were destroyed");
            True(!CardUpgraded(s, secondCopies[0]) && CardUpgraded(s, secondCopies[1]), "next combat inherits only permanent upgrades");
            True(Members(s, "RunDeck").SequenceEqual(originals), "permanent identities survived both combats");
            Submit(s, "Draw"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(25L, EnemyHp(s), "the unupgraded permanent card deals six in the next combat");
        });
        Case("PD07: definition cost upgrades survive copying and generated or removed cards do not enter the next deck", () =>
        {
            var s = Start(package); Submit(s, "BodySlam.Obtain"); var original = Members(s, "RunDeck").Single();
            Submit(s, "EnterDevelopmentRestSite"); Submit(s, "ForgeAtDevelopmentCampfire");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [original])); Healthy(s);
            Equal(0L, CardCost(s, original), "Body Slam's permanent cost upgrade applied");
            Submit(s, "LeaveDevelopmentRoom"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat");
            Equal(0L, CardCost(s, Members(s, "DrawPile").Single()), "copied cost retains the upgrade");
            Submit(s, "BeginDevelopmentTurn");
            Submit(s, "Impervious.AddToHand"); Submit(s, "PlayCardFromHand", Hand(s).Single());
            Submit(s, "Inflame.AddToHand"); Submit(s, "PlayCardFromHand", Hand(s).Single());
            Submit(s, "Anger.AddToHand"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(1, Members(s, "ExhaustPile").Count, "temporary exhaust skill removed from play");
            Equal(1, Members(s, "RemovedFromCombat").Count, "temporary Power card removed from play");
            True(Members(s, "DiscardPile").All(card => s.State.GetField(card, CardField(s, "CanUpgrade")).AsBool()), "Anger's generated copy preserves upgrade metadata");
            var old = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Where(card => card != original).ToArray();
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat");
            True(old.All(card => !s.State.Exists(card)), "all previous generated and removed cards were cleaned up");
            Equal(1, Members(s, "DrawPile").Count, "only the permanent deck was copied");
            True(new[] { "Hand", "DiscardPile", "ExhaustPile", "RemovedFromCombat", "InPlay" }.All(pile => Members(s, pile).Count == 0), "no previous combat pile survived");
        });
        Case("PD08: procedure guards reject direct room bypass, empty smith and repeated upgrades", () =>
        {
            using var copy = new Copy(package.Root);
            foreach (var name in new[] { "EnterDevelopmentRestSite", "BeginDevelopmentCombat", "LeaveDevelopmentRoom", "ForgeAtDevelopmentCampfire", "Strike.Obtain" })
                copy.Include(name + "-direct.spire", "action Force" + name + " { role = player body = { call = { procedure = " + name + " args = { owner = first(type(Player)) } } } }");
            copy.Include("upgrade-direct.spire", """
                action ForceUpgrade {
                  role = player body = { call = { procedure = UpgradeCard args = { card = first(collection(RunDeck)) } } }
                }
                """);
            var probe = copy.Load();
            foreach (var name in new[] { "EnterDevelopmentRestSite", "BeginDevelopmentCombat", "LeaveDevelopmentRoom", "ForgeAtDevelopmentCampfire", "Strike.Obtain" })
            {
                var s = Start(probe); Submit(s, "EnterDevelopmentRestSite"); ForceRejected(s, name);
            }
            var upgraded = Start(probe); Submit(upgraded, "Strike.Obtain");
            Set(upgraded, Members(upgraded, "RunDeck").Single(), "Card", "Upgraded", Value.Of(true));
            ForceRejected(upgraded, "Upgrade");
        });

        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:rest-site-smith"] = witnesses.ToArray() };
    }

    private static bool PendingForge(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("PendingForge")).AsBool();
    private static InstanceId PermanentSource(Session s, InstanceId card) => s.State.GetField(card, CardField(s, "PermanentSource")).AsRef();
}
