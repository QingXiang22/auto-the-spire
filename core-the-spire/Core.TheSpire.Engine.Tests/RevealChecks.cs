using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

/// <summary>
/// H-01 checks for viewer-authorized reveals (the K-06 contract extension the known-position conflict needed).
///
/// The fixture is a hidden area holding four cards that all share one content definition, plus one fact entity per
/// claim. A fact names exactly one viewer and references exactly one card, so the interesting failures are all about
/// leaking through the wrong viewer, the wrong field, the pile's internal order or the live index.
/// </summary>
internal static class RevealChecks
{
    private const string Board = "reveal-board:";
    private const string CardId = Board + "Card";
    private const string FactId = Board + "Known";

    public static void Run()
    {
        Check.Run("H-01 the revealed subset of a hidden area is per viewer", () =>
        {
            var session = Start();
            var cards = Cards(session);
            var facts = Facts(session);

            var white = session.Observe(Fixtures.White, Board + "WhiteView");
            var black = session.Observe(Fixtures.Black, Board + "BlackView");

            Check.Sequence([cards[0], cards[1]], Revealed(white), "white is authorized for its own two facts");
            Check.Sequence([cards[2]], Revealed(black), "black is authorized for its own fact only");

            // The area keeps hiding its identity and its order; only the authorized members are named.
            var draw = white.Collections.Single(c => c.QualifiedId == Board + "Draw");
            Check.False(draw.IdentityVisible, "the hidden area still reports an incomplete membership");
            Check.Equal(0, draw.Members.Count, "the full membership is never listed");
            Check.Equal(4, draw.Size, "the true size stays public");

            var whiteCards = VisibleIds(white, CardId);
            Check.Sequence([cards[0], cards[1]], whiteCards, "white identifies only its own cards");
            var blackCards = VisibleIds(black, CardId);
            Check.Sequence([cards[2]], blackCards, "black identifies only its own card");

            // The two projections declare the same reveal; only the fact's viewer field decides, so no global flag
            // can open an instance to everyone.
            Check.True(facts.All(f => session.State.RelationTargets(
                session.RuleSet.Relations.Single(r => r.QualifiedId == Board + "Shows").Id, f).Count == 1),
                "every fact still owns exactly one reveal edge");
        });

        Check.Run("H-01 a reveal never widens field permissions", () =>
        {
            var session = Start();
            var cards = Cards(session);
            var white = session.Observe(Fixtures.White, Board + "WhiteView");

            // Card A belongs to black, so white is not its owner: the reveal lets white identify the instance but not
            // read the owner-private fields or the engine-hidden field.
            var revealed = white.Instances.Single(i => i.Id == cards[0]);
            Check.Equal("copy", revealed.Fields.Single(f => f.Name == "DefinitionId").Value.AsString(),
                "the public content fact is visible");
            Check.DoesNotContain(revealed.Fields, f => f.Name == "Holder", "owner-private holder stays hidden");
            Check.DoesNotContain(revealed.Fields, f => f.Name == "Secret", "owner-private secret stays hidden");
            Check.DoesNotContain(revealed.Fields, f => f.Name == "EngineHidden", "engine-hidden field stays hidden");

            // Card B belongs to white: ownership and reveal are independent and both apply.
            var own = white.Instances.Single(i => i.Id == cards[1]);
            Check.Contains(own.Fields, f => f.Name == "Secret", "ownership still grants owner-private fields");
            Check.DoesNotContain(own.Fields, f => f.Name == "EngineHidden", "reveal does not unlock hidden fields");
        });

        Check.Run("H-01 duplicate same-definition instances stay distinct", () =>
        {
            var session = Start();
            var cards = Cards(session);
            var white = session.Observe(Fixtures.White, Board + "WhiteView");

            // All four cards carry the same content definition; two of them are revealed to white.
            Check.True(cards.All(c => Text(session, c, "DefinitionId") == "copy"),
                "the fixture really contains four instances of one definition");
            Check.Sequence([cards[0], cards[1]], Revealed(white), "each instance keeps its own identity");
            Check.True(cards[0] != cards[1], "the two revealed instances are not the same instance");
            Check.Equal(2, white.Instances.Count(i => i.Id == cards[0] || i.Id == cards[1]),
                "no instance is listed twice");
        });

        Check.Run("H-01 a hidden-area permutation leaves the revealed subset and its order unchanged", () =>
        {
            var session = Start();
            var cards = Cards(session);
            var draw = session.RuleSet.Collections.Single(c => c.QualifiedId == Board + "Draw").Id;

            // The area is ordered, so the state exposes its internal order to the check (never to a viewer).
            Check.Sequence([cards[1], cards[0], cards[2], cards[3]], session.State.CollectionMembers(draw),
                "the fixture really holds a different internal order");

            var before = Describe(session.Observe(Fixtures.White, Board + "WhiteView"));
            var beforeRevealed = Revealed(session.Observe(Fixtures.White, Board + "WhiteView"));
            Check.Sequence([cards[0], cards[1]], beforeRevealed,
                "revealed members are ordered by identity, not by the pile's internal order");

            session.Submit(Action(session, Board + "SendToBack"), Fixtures.White, [Value.OfRef(cards[0])]);
            Check.Sequence([cards[1], cards[2], cards[3], cards[0]], session.State.CollectionMembers(draw),
                "the permutation really changed the internal order");

            var after = Describe(session.Observe(Fixtures.White, Board + "WhiteView"));
            Check.Equal(before, after, "the public view is identical after a hidden permutation");
            Check.Sequence(beforeRevealed, Revealed(session.Observe(Fixtures.White, Board + "WhiteView")),
                "the revealed subset keeps its identity order");
        });

        Check.Run("H-01 revoking a fact revokes the identity", () =>
        {
            var session = Start();
            var cards = Cards(session);
            var facts = Facts(session);

            session.Submit(Action(session, Board + "Revoke"), Fixtures.White, [Value.OfRef(facts[0])]);

            var white = session.Observe(Fixtures.White, Board + "WhiteView");
            Check.Sequence([cards[1]], Revealed(white), "the revoked claim no longer authorizes its card");
            Check.Sequence([cards[1]], VisibleIds(white, CardId), "the revoked card is not identifiable");
            Check.Equal(2, facts.Count(f => session.State.AllInstances(EntityType(session.RuleSet, FactId).Id)
                .Contains(f)), "the dropped fact is gone from the live state");
            var draw = white.Collections.Single(c => c.QualifiedId == Board + "Draw");
            Check.Equal(4, draw.Size, "revocation does not shrink the hidden area");
            Check.Sequence([cards[1]], Revealed(white), "the remaining claim still works");
        });

        Check.Run("H-01 a fact states the known position and the engine never recomputes it", () =>
        {
            var session = Start();
            var cards = Cards(session);
            var facts = Facts(session);
            var draw = session.RuleSet.Collections.Single(c => c.QualifiedId == Board + "Draw").Id;

            session.Submit(Action(session, Board + "Restate"), Fixtures.White, [Value.OfRef(facts[1]), Value.Of(9L)]);
            var white = session.Observe(Fixtures.White, Board + "WhiteView");

            var restated = white.Instances.Single(i => i.Id == facts[1]);
            Check.Equal(9L, restated.Fields.Single(f => f.Name == "Position").Value.AsInt(),
                "the pack's stated position is what the viewer reads");
            var live = session.State.CollectionIndexOf(draw, cards[1]);
            Check.True(live != 9, "the stated position is not the live index");

            // Moving the card does not silently rewrite the knowledge: only the pack may restate it.
            session.Submit(Action(session, Board + "SendToBack"), Fixtures.White, [Value.OfRef(cards[1])]);
            var moved = session.Observe(Fixtures.White, Board + "WhiteView").Instances.Single(i => i.Id == facts[1]);
            Check.Equal(9L, moved.Fields.Single(f => f.Name == "Position").Value.AsInt(),
                "the engine does not substitute the live index for the stated position");
        });

        Check.Run("H-01 a projection without a reveal keeps hiding the area", () =>
        {
            var rules = Fixtures.Link(Package(declareReveal: false));
            var session = new Session(rules, 7UL, Fixtures.Roles);
            session.Start();

            var white = session.Observe(Fixtures.White, Board + "WhiteView");
            Check.Equal(0, Revealed(white).Count, "nothing is authorized without a reveal declaration");
            Check.Equal(0, VisibleIds(white, CardId).Count, "the hidden area's cards stay unidentifiable");
            // The default hiding rule is intact: no reference and no relation target names a hidden instance.
            Check.DoesNotContain(white.Instances, i => i.TypeId == FactId && i.Fields.Any(f => f.Name == "Card"),
                "a reference to an unidentifiable instance is not published");
            Check.Equal(0, white.Relations.Count, "no relation edge leaks a hidden instance");
        });

        Check.Run("H-01 references and relation edges only name identifiable instances", () =>
        {
            var session = Start();
            var cards = Cards(session);
            var facts = Facts(session);
            var white = session.Observe(Fixtures.White, Board + "WhiteView");

            // White may read its own claims' references, but only because the reveal makes those cards identifiable.
            var own = white.Instances.Single(i => i.Id == facts[0]);
            Check.Equal(cards[0], own.Fields.Single(f => f.Name == "Card").Value.AsRef(),
                "an authorized reference is published");
            // Black's claim stays in the view (its type is projected) but its reference is withheld, and the reveal
            // relation edge that would name card C is filtered out.
            var other = white.Instances.Single(i => i.Id == facts[2]);
            Check.DoesNotContain(other.Fields, f => f.Name == "Card", "another viewer's reference is withheld");
            Check.Sequence([cards[0], cards[1]],
                white.Relations.Where(r => r.QualifiedId == Board + "Shows")
                    .SelectMany(r => r.Targets).OrderBy(t => t.Value),
                "only authorized reveal targets survive the projection");
        });

        Check.Run("H-01 the reveal declaration is part of the rule set identity", () =>
        {
            var withReveal = Fixtures.Link(Package(true)).Identity;
            var without = Fixtures.Link(Package(false)).Identity;
            Check.True(withReveal != without, "changing the reveal declaration changes the identity");
        });

        Check.Run("H-01 a reveal authority outside the projection is rejected", () =>
        {
            var probe = new EnginePackageBuilder("reveal-probe-authority", "1.0.0");
            var thing = probe.EntityType("Thing");
            thing.AddField("Value", FieldTypeSchema.Int(0, 7));
            var known = probe.EntityType("Known");
            known.OwnerFieldName = "Viewer";
            known.AddField("Viewer", FieldTypeSchema.Participant());
            var shows = probe.Relation("Shows", thing);
            var view = probe.Projection("View");
            view.ParticipantRole = "white";
            view.Type(thing);
            view.Reveal(known, shows);
            Check.Fault(EngineFaultCode.SchemaInvalid, () => Fixtures.Link(probe),
                "an authority type outside the projection is rejected");
        });

        Check.Run("H-01 a reveal authority without a viewer field is rejected", () =>
        {
            var probe = new EnginePackageBuilder("reveal-probe-no-viewer", "1.0.0");
            var thing = probe.EntityType("Thing");
            thing.AddField("Value", FieldTypeSchema.Int(0, 7));
            var known = probe.EntityType("Known");
            known.AddField("Note", FieldTypeSchema.String(8));
            var shows = probe.Relation("Shows", thing);
            var view = probe.Projection("View");
            view.ParticipantRole = "white";
            view.Type(thing);
            view.Type(known);
            view.Reveal(known, shows);
            Check.Fault(EngineFaultCode.SchemaInvalid, () => Fixtures.Link(probe),
                "an authority type with no viewer field cannot authorize anyone");
        });

        Check.Run("H-01 a reveal whose viewer field is not a participant is rejected", () =>
        {
            var probe = new EnginePackageBuilder("reveal-probe-bad-viewer", "1.0.0");
            var thing = probe.EntityType("Thing");
            thing.AddField("Value", FieldTypeSchema.Int(0, 7));
            var known = probe.EntityType("Known");
            known.OwnerFieldName = "Viewer";
            known.AddField("Viewer", FieldTypeSchema.Int(0, 7));
            var shows = probe.Relation("Shows", thing);
            var view = probe.Projection("View");
            view.ParticipantRole = "white";
            view.Type(thing);
            view.Type(known);
            view.Reveal(known, shows);
            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(probe),
                "only a participant field can name the authorized viewer");
        });

        Check.Run("H-01 a reveal whose target type is not included is rejected", () =>
        {
            var probe = new EnginePackageBuilder("reveal-probe-bad-target", "1.0.0");
            var thing = probe.EntityType("Thing");
            thing.AddField("Value", FieldTypeSchema.Int(0, 7));
            var secret = probe.EntityType("Secret");
            secret.AddField("Value", FieldTypeSchema.Int(0, 7));
            var known = probe.EntityType("Known");
            known.OwnerFieldName = "Viewer";
            known.AddField("Viewer", FieldTypeSchema.Participant());
            var shows = probe.Relation("Shows", secret);
            var view = probe.Projection("View");
            view.ParticipantRole = "white";
            view.Type(thing);
            view.Type(known);
            view.Reveal(known, shows);
            Check.Fault(EngineFaultCode.SchemaInvalid, () => Fixtures.Link(probe),
                "revealing a type the viewer cannot see is rejected");
        });
    }

    private static Session Start()
    {
        var session = new Session(Fixtures.Link(Package(true)), 7UL, Fixtures.Roles);
        session.Start();
        return session;
    }

    private static IReadOnlyList<InstanceId> Cards(Session session) =>
        session.State.AllInstances(EntityType(session.RuleSet, CardId).Id);

    private static IReadOnlyList<InstanceId> Facts(Session session) =>
        session.State.AllInstances(EntityType(session.RuleSet, FactId).Id);

    private static IReadOnlyList<InstanceId> Revealed(ParticipantView view) =>
        view.Collections.Single(c => c.QualifiedId == Board + "Draw").RevealedMembers;

    private static IReadOnlyList<InstanceId> VisibleIds(ParticipantView view, string typeId) =>
        view.Instances.Where(i => i.TypeId == typeId).Select(i => i.Id).ToArray();

    private static string Describe(ParticipantView view) =>
        string.Join(";", view.Instances.Select(instance =>
            $"{instance.TypeId}#{instance.Id.Value}[{string.Join(",", instance.Fields.Select(f => f.Name + "=" + f.Value))}]"))
        + "|" + string.Join(";", view.Collections.Select(collection =>
            $"{collection.QualifiedId}:{collection.Size}:{string.Join(",", collection.RevealedMembers.Select(m => m.Value))}"));

    private static ActionDefinition Action(Session session, string qualifiedId) =>
        session.RuleSet.Actions.Single(action => action.QualifiedId == qualifiedId);

    private static EntityTypeDefinition EntityType(RuleSet rules, string qualifiedId) =>
        rules.TypeByQualifiedId(qualifiedId);

    private static string Text(Session session, InstanceId instance, string field)
    {
        var type = EntityType(session.RuleSet, CardId);
        return session.State.GetField(instance, type.FieldIndex(field)).AsString();
    }

    /// <summary>
    /// The fixture: one hidden ordered area with four instances of one definition, one claim entity per viewer, and a
    /// reveal relation from a claim to the instance it claims.
    /// </summary>
    private static EnginePackageBuilder Package(bool declareReveal)
    {
        var package = new EnginePackageBuilder("reveal-board", "1.0.0");

        var card = package.EntityType("Card");
        card.OwnerFieldName = "Holder";
        var cardDefinition = card.AddField("DefinitionId", FieldTypeSchema.String(32));
        var cardFace = card.AddField("Face", FieldTypeSchema.Int(0, 99));
        var cardHolder = card.AddField("Holder", FieldTypeSchema.Participant(),
            visibility: FieldVisibility.OwnerPrivate);
        var cardSecret = card.AddField("Secret", FieldTypeSchema.Int(0, 99),
            visibility: FieldVisibility.OwnerPrivate);
        var cardHidden = card.AddField("EngineHidden", FieldTypeSchema.String(8),
            visibility: FieldVisibility.Hidden);

        var fact = package.EntityType("Known");
        fact.OwnerFieldName = "Viewer";
        var factViewer = fact.AddField("Viewer", FieldTypeSchema.Participant(),
            visibility: FieldVisibility.OwnerPrivate);
        var factCard = fact.AddField("Card", FieldTypeSchema.RefTo(card), visibility: FieldVisibility.OwnerPrivate);
        var factPile = fact.AddField("Pile", FieldTypeSchema.String(16), visibility: FieldVisibility.OwnerPrivate);
        var factPosition = fact.AddField("Position", FieldTypeSchema.Int(-1, 99),
            visibility: FieldVisibility.OwnerPrivate);

        var draw = package.Collection("Draw", card);
        draw.MembershipGroup = "pile";
        draw.IdentityVisible = false;
        draw.OrderVisible = false;
        var known = package.Collection("Known", fact);

        var shows = package.Relation("Shows", card);

        var white = package.Projection("WhiteView");
        white.ParticipantRole = "white";
        white.Type(card);
        white.Type(fact);
        var black = package.Projection("BlackView");
        black.ParticipantRole = "black";
        black.Type(card);
        black.Type(fact);
        if (declareReveal)
        {
            white.Reveal(fact, shows);
            black.Reveal(fact, shows);
        }

        package.TerminalResult("Done");

        var setup = package.Procedure("Setup");
        var a = setup.Local("a", FieldTypeSchema.Binding());
        var b = setup.Local("b", FieldTypeSchema.Binding());
        var c = setup.Local("c", FieldTypeSchema.Binding());
        var d = setup.Local("d", FieldTypeSchema.Binding());
        var first = setup.Local("first", FieldTypeSchema.Binding());
        var second = setup.Local("second", FieldTypeSchema.Binding());
        var third = setup.Local("third", FieldTypeSchema.Binding());
        setup.Body(
            Card(setup, a, 11, 51, Fixtures.Black, draw, cardDefinition, cardFace, cardHolder, cardSecret, cardHidden),
            Card(setup, b, 12, 52, Fixtures.White, draw, cardDefinition, cardFace, cardHolder, cardSecret, cardHidden),
            Card(setup, c, 13, 53, Fixtures.Black, draw, cardDefinition, cardFace, cardHolder, cardSecret, cardHidden),
            Card(setup, d, 14, 54, Fixtures.White, draw, cardDefinition, cardFace, cardHolder, cardSecret, cardHidden),
            // The area's internal order is not its creation order, so a leak of the internal order is visible.
            setup.MoveInCollection(draw, setup.LocalRef(b), InsertPosition.Start),
            Fact(setup, first, Fixtures.White, a, 3, known, factViewer, factCard, factPile, factPosition),
            Fact(setup, second, Fixtures.White, b, 1, known, factViewer, factCard, factPile, factPosition),
            Fact(setup, third, Fixtures.Black, c, 0, known, factViewer, factCard, factPile, factPosition),
            setup.AddRelation(shows, setup.LocalRef(first), setup.LocalRef(a)),
            setup.AddRelation(shows, setup.LocalRef(second), setup.LocalRef(b)),
            setup.AddRelation(shows, setup.LocalRef(third), setup.LocalRef(c)));
        package.OnSessionStart(setup.Symbol);

        // Revoking a claim: the pack drops the fact together with its reveal edge, so no field is left pointing at an
        // instance the viewer may no longer identify.
        var revoke = package.Action("Revoke");
        var revokeFact = revoke.Parameter("Fact", FieldTypeSchema.RefTo(fact));
        var revokeBody = package.ActionBody(revoke);
        revokeBody.Role("white");
        revokeBody.Body(
            revokeBody.RemoveRelation(shows, revokeBody.Param(revokeFact.DeclarationIndex),
                revokeBody.Field(revokeBody.Param(revokeFact.DeclarationIndex), factCard)),
            revokeBody.Destroy(revokeBody.Param(revokeFact.DeclarationIndex)));

        // Restating what the viewer knows: only the pack may change a stated position.
        var restate = package.Action("Restate");
        var restateFact = restate.Parameter("Fact", FieldTypeSchema.RefTo(fact));
        var restatePosition = restate.Parameter("Position", FieldTypeSchema.Int(-1, 99));
        var restateBody = package.ActionBody(restate);
        restateBody.Role("white");
        restateBody.Body(restateBody.SetField(restateBody.Param(restateFact.DeclarationIndex), factPosition,
            restateBody.Param(restatePosition.DeclarationIndex)));

        // Permutes the hidden area without touching any claim.
        var sendToBack = package.Action("SendToBack");
        var sendCard = sendToBack.Parameter("Card", FieldTypeSchema.RefTo(card));
        var sendBody = package.ActionBody(sendToBack);
        sendBody.Role("white");
        sendBody.Body(sendBody.MoveInCollection(draw, sendBody.Param(sendCard.DeclarationIndex), InsertPosition.End));

        return package;
    }

    private static Inst Card(IrScope scope, int slot, long face, long secret, ParticipantId holder,
        CollectionSymbol draw, FieldSymbol definitionField, FieldSymbol faceField, FieldSymbol holderField,
        FieldSymbol secretField, FieldSymbol hiddenField) =>
        scope.CreateEntity(draw.ElementType, new FrameScopeExpr(), slot, new[]
        {
            (definitionField, scope.Const("copy")),
            (faceField, scope.Const(face)),
            (holderField, scope.Const(Value.OfParticipant(holder))),
            (secretField, scope.Const(secret)),
            (hiddenField, scope.Const("hidden")),
        }, draw);

    private static Inst Fact(IrScope scope, int slot, ParticipantId viewer, int cardSlot, long position,
        CollectionSymbol known, FieldSymbol viewerField, FieldSymbol cardField, FieldSymbol pileField,
        FieldSymbol positionField) =>
        scope.CreateEntity(known.ElementType, new FrameScopeExpr(), slot, new[]
        {
            (viewerField, scope.Const(Value.OfParticipant(viewer))),
            (cardField, scope.LocalRef(cardSlot)),
            (pileField, scope.Const("draw")),
            (positionField, scope.Const(position)),
        }, known);
}
