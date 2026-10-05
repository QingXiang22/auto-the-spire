using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

/// <summary>
/// Two test fixtures that share one generic runtime (K-07). Neither package is a real game: both are deliberately
/// small test scenarios that exist to prove the runtime has no game-specific branch.
///
/// <list type="bullet">
/// <item><b>grid-wargame</b>: a board scope, units with a movement resource, a shared turn register, a moved-event
/// listener, an area that shares a membership group, a recruit action that can overflow a declared capacity, a
/// victory terminal result, and deliberately bad actions (unbounded recursion, division by zero, an event cascade)
/// used by the budget and fault checks.</item>
/// <item><b>micro-board</b>: cards with a public name/value, an owner-private holder and secret, an engine-hidden
/// legacy field, a pack-maintained "known" flag, an order-hidden hand, an identity-hidden deck, a public area, two
/// projections, and a discard action that suspends on a decision.</item>
/// </list>
/// </summary>
internal static class Fixtures
{
    public const string Grid = "grid-wargame";

    public const string Micro = "micro-board";

    public static readonly ParticipantId White = new(1);

    public static readonly ParticipantId Black = new(2);

    public static readonly IReadOnlyList<string> Roles = ["white", "black"];

    /// <summary>Links packages in the given order.</summary>
    public static RuleSet Link(params EnginePackageBuilder[] builders)
    {
        var builder = new RuleSetBuilder();
        foreach (var package in builders) builder.Add(package.Build());
        return builder.Build();
    }

    public static RuleSet BuildBoth() => Link(GridPackage(), MicroPackage());

    private static (FieldSymbol Field, Expr Value) F(FieldSymbol field, Expr value) => (field, value);

    // -- grid-wargame -------------------------------------------------------------------------------------------

    public static EnginePackageBuilder GridPackage(string version = "1.0.0")
    {
        var package = new EnginePackageBuilder(Grid, version);

        var board = package.ScopeType("Board");
        var unit = package.EntityType("Unit");
        var unitOwner = unit.AddField("Owner", FieldTypeSchema.Participant());
        var unitColumn = unit.AddField("Column", FieldTypeSchema.Int(0, 7));
        var unitRow = unit.AddField("Row", FieldTypeSchema.Int(0, 7));
        var unitStrength = unit.AddField("Strength", FieldTypeSchema.Int(0, 100));
        // An explicit optional reference: "this unit may have no target" is a deliberate declaration, not the old
        // implicit default. A required reference (the default since R-CORE) is a construction obligation.
        var unitTarget = unit.AddField("Target", FieldTypeSchema.RefTo(unit), optional: true);
        var unitNote = unit.AddField("Note", FieldTypeSchema.String(32), visibility: FieldVisibility.Hidden);

        var units = package.Collection("Units", unit);
        units.Capacity = 2;
        units.MembershipGroup = "board";
        var fallen = package.Collection("Fallen", unit);
        fallen.Ordered = false;
        fallen.MembershipGroup = "board";
        var controls = package.Relation("Controls", unit);
        controls.Many = false;

        var game = package.EntityType("Game");
        var gameCurrent = game.AddField("Current", FieldTypeSchema.Participant());
        var gameMoves = game.AddField("Moves", FieldTypeSchema.Int(0, 10_000));

        var ping = package.Event("Ping");
        var moved = package.Event("Moved");
        var movedUnit = moved.AddPayload("Unit", FieldTypeSchema.RefTo(unit));

        var victory = package.TerminalResult("Victory");
        var victoryWinner = victory.AddField("Winner", FieldTypeSchema.Participant());

        // Session start: the board scope, the turn register and one unit per side.
        var setup = package.Procedure("Setup");
        var boardLocal = setup.Local("board", FieldTypeSchema.Binding());
        var gameLocal = setup.Local("game", FieldTypeSchema.Binding());
        var whiteUnit = setup.Local("whiteUnit", FieldTypeSchema.Binding());
        var blackUnit = setup.Local("blackUnit", FieldTypeSchema.Binding());
        setup.Body(
            setup.CreateScope(board, boardLocal),
            setup.CreateEntity(game, setup.LocalRef(boardLocal), gameLocal, new[]
            {
                F(gameCurrent, setup.Const(Value.OfParticipant(White))),
                F(gameMoves, setup.Const(0L)),
            }),
            setup.CreateEntity(unit, setup.LocalRef(boardLocal), whiteUnit, new[]
            {
                F(unitOwner, setup.Const(Value.OfParticipant(White))),
                F(unitColumn, setup.Const(0L)),
                F(unitRow, setup.Const(0L)),
                F(unitStrength, setup.Const(5L)),
                F(unitNote, setup.Const("white")),
            }, units),
            setup.CreateEntity(unit, setup.LocalRef(boardLocal), blackUnit, new[]
            {
                F(unitOwner, setup.Const(Value.OfParticipant(Black))),
                F(unitColumn, setup.Const(7L)),
                F(unitRow, setup.Const(0L)),
                F(unitStrength, setup.Const(5L)),
                F(unitNote, setup.Const("black")),
            }, units));
        package.OnSessionStart(setup.Symbol);

        // A listener whose parameter is bound positionally from the event payload.
        var onMoved = package.Procedure("OnMoved");
        var movedParameter = onMoved.Param("Unit", FieldTypeSchema.RefTo(unit));
        var counter = onMoved.First(onMoved.Type(game));
        onMoved.Body(
            onMoved.SetField(onMoved.Param(movedParameter), unitNote, onMoved.Const("moved")),
            onMoved.If(
                onMoved.Not(onMoved.IsNone(counter)),
                onMoved.SetField(counter, gameMoves,
                    onMoved.Add(onMoved.Field(counter, gameMoves), onMoved.Const(1L)))));
        package.Subscribe(moved, onMoved.Symbol);

        // A self-emitting listener: exists only to prove the event-cascade budget faults explicitly.
        var onPing = package.Procedure("OnPing");
        onPing.Body(onPing.Emit(ping));
        package.Subscribe(ping, onPing.Symbol);

        // White's move: validated against its own candidates, spends one strength, emits, may win, passes the turn.
        var move = package.Action("Move");
        var moveUnit = move.Parameter("Unit", FieldTypeSchema.RefTo(unit));
        var moveColumn = move.Parameter("Column", FieldTypeSchema.Int(0, 7));
        var moveRow = move.Parameter("Row", FieldTypeSchema.Int(0, 7));
        var moveBody = package.ActionBody(move);
        var moveGame = moveBody.First(moveBody.Type(game));
        var moveUnitRef = moveBody.Param(moveUnit.DeclarationIndex);
        moveBody.Role("white");
        moveBody.Candidates("Unit", moveBody.Where(moveBody.Collection(units),
            item => moveBody.Equal(moveBody.Field(item, unitOwner), moveBody.Const(Value.OfParticipant(White)))));
        moveBody.Requires(moveBody.And(
            moveBody.Equal(moveBody.Field(moveGame, gameCurrent), moveBody.Const(Value.OfParticipant(White))),
            moveBody.GreaterOrEqual(moveBody.Field(moveUnitRef, unitStrength), moveBody.Const(1L))));
        moveBody.Body(
            moveBody.SetField(moveUnitRef, unitColumn, moveBody.Param(moveColumn.DeclarationIndex)),
            moveBody.SetField(moveUnitRef, unitRow, moveBody.Param(moveRow.DeclarationIndex)),
            moveBody.SetField(moveUnitRef, unitStrength,
                moveBody.Subtract(moveBody.Field(moveUnitRef, unitStrength), moveBody.Const(1L))),
            moveBody.Emit(moved, F(movedUnit, moveUnitRef)),
            moveBody.If(
                moveBody.Equal(moveBody.Field(moveUnitRef, unitColumn), moveBody.Const(3L)),
                moveBody.SetTerminal(victory, F(victoryWinner, moveBody.Const(Value.OfParticipant(White))))),
            moveBody.SetField(moveGame, gameCurrent, moveBody.Const(Value.OfParticipant(Black))));

        // Black's mirror action keeps the turn register honest without needing a dynamic actor expression.
        var moveBlack = package.Action("MoveBlack");
        var blackUnitParameter = moveBlack.Parameter("Unit", FieldTypeSchema.RefTo(unit));
        var blackColumn = moveBlack.Parameter("Column", FieldTypeSchema.Int(0, 7));
        var blackRow = moveBlack.Parameter("Row", FieldTypeSchema.Int(0, 7));
        var blackBody = package.ActionBody(moveBlack);
        var blackGame = blackBody.First(blackBody.Type(game));
        var blackUnitRef = blackBody.Param(blackUnitParameter.DeclarationIndex);
        blackBody.Role("black");
        blackBody.Candidates("Unit", blackBody.Where(blackBody.Collection(units),
            item => blackBody.Equal(blackBody.Field(item, unitOwner), blackBody.Const(Value.OfParticipant(Black)))));
        blackBody.Requires(blackBody.Equal(blackBody.Field(blackGame, gameCurrent),
            blackBody.Const(Value.OfParticipant(Black))));
        blackBody.Body(
            blackBody.SetField(blackUnitRef, unitColumn, blackBody.Param(blackColumn.DeclarationIndex)),
            blackBody.SetField(blackUnitRef, unitRow, blackBody.Param(blackRow.DeclarationIndex)),
            blackBody.Emit(moved, F(movedUnit, blackUnitRef)),
            blackBody.SetField(blackGame, gameCurrent, blackBody.Const(Value.OfParticipant(White))));

        // Recruiting overflows the declared capacity of "Units"; the fault must roll the creation back.
        var recruit = package.Action("Recruit");
        var recruitBody = package.ActionBody(recruit);
        var recruitGame = recruitBody.First(recruitBody.Type(game));
        var recruitSlot = recruitBody.Local("recruit", FieldTypeSchema.Binding());
        recruitBody.Role("white");
        recruitBody.Body(
            recruitBody.CreateEntity(unit, new FrameScopeExpr(), recruitSlot, new[]
            {
                F(unitOwner, recruitBody.Const(Value.OfParticipant(White))),
                F(unitColumn, recruitBody.Const(0L)),
                F(unitRow, recruitBody.Const(0L)),
                F(unitStrength, recruitBody.Const(5L)),
                F(unitNote, recruitBody.Const("recruit")),
            }, units),
            recruitBody.SetField(recruitGame, gameMoves,
                recruitBody.Add(recruitBody.Field(recruitGame, gameMoves), recruitBody.Const(1L))));

        // A fifty-iteration loop: iteration count must not consume call depth.
        var drill = package.Action("Drill");
        var drillBody = package.ActionBody(drill);
        var drillGame = drillBody.First(drillBody.Type(game));
        drillBody.Role("white");
        drillBody.Body(drillBody.Repeat(drillBody.Const(50L),
            drillBody.SetField(drillGame, gameMoves,
                drillBody.Add(drillBody.Field(drillGame, gameMoves), drillBody.Const(1L)))));

        // Fault actions.
        var recurseProcedure = package.Procedure("Recurse");
        recurseProcedure.Body(recurseProcedure.Call(recurseProcedure.Symbol));
        var recurse = package.Action("RecurseAction");
        var recurseBody = package.ActionBody(recurse);
        recurseBody.Role("white");
        recurseBody.Body(recurseBody.Call(recurseProcedure.Symbol));

        var divide = package.Action("DivideAction");
        var divideBody = package.ActionBody(divide);
        var divideGame = divideBody.First(divideBody.Type(game));
        divideBody.Role("white");
        divideBody.Body(divideBody.SetField(divideGame, gameMoves,
            divideBody.Divide(divideBody.Field(divideGame, gameMoves), divideBody.Const(0L))));

        var cascade = package.Action("CascadeAction");
        var cascadeBody = package.ActionBody(cascade);
        cascadeBody.Role("white");
        cascadeBody.Body(cascadeBody.Emit(ping));

        return package;
    }

    // -- micro-board --------------------------------------------------------------------------------------------

    public static EnginePackageBuilder MicroPackage()
    {
        var package = new EnginePackageBuilder(Micro, "1.0.0");

        var card = package.EntityType("Card");
        card.OwnerFieldName = "Holder";
        var cardName = card.AddField("Name", FieldTypeSchema.String(32));
        var cardHolder = card.AddField("Holder", FieldTypeSchema.Participant(),
            visibility: FieldVisibility.OwnerPrivate);
        var cardValue = card.AddField("Value", FieldTypeSchema.Int(0, 99));
        var cardSecret = card.AddField("Secret", FieldTypeSchema.Int(0, 99),
            visibility: FieldVisibility.OwnerPrivate);
        var cardLegacy = card.AddField("Legacy", FieldTypeSchema.String(16), visibility: FieldVisibility.Hidden);
        var cardKnown = card.AddField("Known", FieldTypeSchema.Bool());

        var hand = package.Collection("Hand", card);
        hand.MembershipGroup = "zone";
        hand.OrderVisible = false;
        var deck = package.Collection("Deck", card);
        deck.MembershipGroup = "zone";
        deck.IdentityVisible = false;
        deck.OrderVisible = false;
        var arena = package.Collection("Arena", card);
        var discards = package.Collection("Discards", card);

        var ledger = package.EntityType("Ledger");
        var ledgerDiscards = ledger.AddField("Discards", FieldTypeSchema.Int(0, 1000));

        var projection = package.Projection("PlayerView");
        projection.ParticipantRole = "white";
        projection.Type(card).OwnerField = "Holder";

        var audit = package.Projection("AuditView");
        audit.ParticipantRole = "black";
        audit.Type(card).IncludeHiddenFields = true;

        package.TerminalResult("Draw");

        var compute = package.ComputeAsset("Score");
        compute.BackendId = "test";
        compute.SourcePath = "fixtures/score.lua";
        compute.SourceHash = "0000000000000000";
        compute.Entry = "score";
        var computeValue = compute.Input("Value", FieldTypeSchema.Int(0, 99));
        var computeDoubled = compute.Output("Doubled", FieldTypeSchema.Int(0, 999));

        var setup = package.Procedure("Setup");
        var ledgerLocal = setup.Local("ledger", FieldTypeSchema.Binding());
        var first = setup.Local("first", FieldTypeSchema.Binding());
        var second = setup.Local("second", FieldTypeSchema.Binding());
        var foreign = setup.Local("foreign", FieldTypeSchema.Binding());
        var covered = setup.Local("covered", FieldTypeSchema.Binding());
        var doomed = setup.Local("doomed", FieldTypeSchema.Binding());
        setup.Body(
            setup.CreateEntity(ledger, new FrameScopeExpr(), ledgerLocal, new[]
            {
                F(ledgerDiscards, setup.Const(0L)),
            }),
            Card(setup, first, "Strike", White, 6, 41, "legacy-a", hand, cardName, cardHolder, cardValue, cardSecret,
                cardLegacy),
            Card(setup, second, "Defend", White, 5, 42, "legacy-b", hand, cardName, cardHolder, cardValue, cardSecret,
                cardLegacy),
            Card(setup, foreign, "Bash", Black, 9, 43, "legacy-c", hand, cardName, cardHolder, cardValue, cardSecret,
                cardLegacy),
            Card(setup, covered, "Covered", Black, 3, 44, "legacy-d", deck, cardName, cardHolder, cardValue, cardSecret,
                cardLegacy),
            Card(setup, doomed, "Doomed", White, 1, 45, "legacy-e", arena, cardName, cardHolder, cardValue, cardSecret,
                cardLegacy),
            setup.SetField(setup.LocalRef(first), cardKnown, setup.Const(true)),
            setup.AddToCollection(arena, setup.LocalRef(first)),
            setup.Destroy(setup.LocalRef(doomed)));
        package.OnSessionStart(setup.Symbol);

        // Suspends on a decision after an already resolved effect, so resumption can be checked for double counting.
        var discard = package.Action("Discard");
        var discardBody = package.ActionBody(discard);
        var discardLedger = discardBody.First(discardBody.Type(ledger));
        var chosen = discardBody.Local("chosen", FieldTypeSchema.Binding());
        discardBody.Role("white");
        discardBody.Body(
            discardBody.SetField(discardLedger, ledgerDiscards,
                discardBody.Add(discardBody.Field(discardLedger, ledgerDiscards), discardBody.Const(1L))),
            discardBody.Decide(discardBody.Const(Value.OfParticipant(White)), DecisionResponseKind.EntityRef,
                "Choose a card to discard", chosen, discardBody.Collection(hand), discardBody.Const(1L),
                discardBody.Const(1L)),
            discardBody.RemoveFromCollection(hand, discardBody.LocalRef(chosen)),
            discardBody.AddToCollection(discards, discardBody.LocalRef(chosen)));

        // Moves every hand card into the discard pile: proves a loop iterates a snapshot, not the live collection.
        var clearHand = package.Action("ClearHand");
        var clearBody = package.ActionBody(clearHand);
        clearBody.Role("white");
        clearBody.Body(
            clearBody.ForEach(clearBody.Collection(hand), cardInHand => new[]
            {
                clearBody.RemoveFromCollection(hand, cardInHand),
                clearBody.AddToCollection(discards, cardInHand),
            }));

        // Compute stays behind a declared seam: the host must install a backend.
        var score = package.Action("Score");
        var scoreCard = score.Parameter("Card", FieldTypeSchema.RefTo(card));
        var scoreBody = package.ActionBody(score);
        var scoreResult = scoreBody.Local("doubled", FieldTypeSchema.Int(0, 999));
        scoreBody.Role("white");
        scoreBody.Candidates("Card", scoreBody.Where(scoreBody.Collection(hand),
            item => scoreBody.Equal(scoreBody.Field(item, cardHolder), scoreBody.Const(Value.OfParticipant(White)))));
        scoreBody.Requires(scoreBody.Equal(scoreBody.Field(scoreBody.Param(scoreCard.DeclarationIndex), cardHolder),
            scoreBody.Const(Value.OfParticipant(White))));
        scoreBody.Body(
            scoreBody.Compute(compute,
                new[] { (computeValue, scoreBody.Field(scoreBody.Param(scoreCard.DeclarationIndex), cardValue)) },
                new[] { (computeDoubled, scoreResult) }),
            scoreBody.SetField(scoreBody.Param(scoreCard.DeclarationIndex), cardValue,
                scoreBody.LocalRef(scoreResult)));

        return package;
    }

    private static Inst Card(IrScope scope, int slot, string name, ParticipantId holder, long value, long secret,
        string legacy, CollectionSymbol collection, FieldSymbol nameField, FieldSymbol holderField,
        FieldSymbol valueField, FieldSymbol secretField, FieldSymbol legacyField) =>
        scope.CreateEntity(collection.ElementType, new FrameScopeExpr(), slot, new[]
        {
            F(nameField, scope.Const(name)),
            F(holderField, scope.Const(Value.OfParticipant(holder))),
            F(valueField, scope.Const(value)),
            F(secretField, scope.Const(secret)),
            F(legacyField, scope.Const(legacy)),
        }, collection);
}
