using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

/// <summary>
/// Executable checks for the generic runtime (K-01 to K-07). Every check is one <see cref="Check.Run"/> entry so the
/// suite reports how many checks actually executed, and a failure names the check and the expected value.
/// </summary>
internal static class EngineChecks
{
    private const string Grid = "grid-wargame:";
    private const string Micro = "micro-board:";
    private const string UnitId = Grid + "Unit";
    private const string GameId = Grid + "Game";
    private const string CardId = Micro + "Card";
    private const string LedgerId = Micro + "Ledger";

    public static int RunAll()
    {
        DefinitionChecks();
        StorageChecks();
        SchedulerChecks();
        DecisionChecks();
        RandomChecks();
        VanillaRandomChecks.Run();
        RandomDslChecks.Run();
        ProjectionChecks();
        RevealChecks.Run();
        RequiredRefChecks.Run();
        CounterexampleChecks.Run();
        AuthoringChecks.Run();
        RuleLibraryChecks.Run();
        ListenerChecks.Run();
        SourceFragmentChecks.Run();
        DispatchChecks.Run();
        ContractChecks.Run();
        AuthorRuntimeV04Checks.Run();
        DebugChecks.Run();
        return Check.Passed;
    }

    // -- K-01: declaration and link checks ----------------------------------------------------------------------

    private static void DefinitionChecks()
    {
        Check.Run("K-01 a package cannot declare the same name twice", () =>
        {
            var package = new EnginePackageBuilder("dup");
            package.EntityType("Thing");
            package.EntityType("Thing");
            Check.Fault(EngineFaultCode.DuplicateDeclaration, () => Fixtures.Link(package), "duplicate declaration");
        });

        Check.Run("K-01 redeclaring another package's id needs an explicit override", () =>
        {
            var origin = new EnginePackageBuilder("base");
            origin.EntityType("Thing");
            var intruder = new EnginePackageBuilder("mod");
            intruder.EntityType("base:Thing");
            Check.Fault(EngineFaultCode.UnauthorizedOverride, () => Fixtures.Link(origin, intruder), "override");
        });

        Check.Run("K-01 an explicit override replaces the declaration", () =>
        {
            var origin = new EnginePackageBuilder("base");
            origin.EntityType("Thing").AddField("A", FieldTypeSchema.Int());
            var replacement = new EnginePackageBuilder("mod");
            replacement.Overrides.Add("base:Thing");
            replacement.EntityType("base:Thing").AddField("B", FieldTypeSchema.Int());

            var rules = Fixtures.Link(origin, replacement);
            var thing = rules.TypeByQualifiedId("base:Thing");
            Check.Equal(1, thing.PackageIndex, "winning package");
            Check.Equal(1, thing.Fields.Count, "winning field count");
            Check.Equal("B", thing.Fields[0].Name, "winning field");
        });

        Check.Run("K-01 extension merges fields only for an extensible type", () =>
        {
            var origin = new EnginePackageBuilder("base");
            var extensible = origin.EntityType("Thing");
            extensible.AddField("A", FieldTypeSchema.Int());
            extensible.ExtensibleFields = true;
            var extender = new EnginePackageBuilder("mod");
            extender.Extends.Add("base:Thing");
            extender.EntityType("base:Thing").AddField("B", FieldTypeSchema.Int());
            var rules = Fixtures.Link(origin, extender);
            var thing = rules.TypeByQualifiedId("base:Thing");
            Check.Equal(2, thing.Fields.Count, "merged field count");
            Check.Equal("B", thing.Fields[1].Name, "appended field");

            var closed = new EnginePackageBuilder("closed");
            closed.EntityType("Thing").AddField("A", FieldTypeSchema.Int());
            var illegal = new EnginePackageBuilder("extender");
            illegal.Extends.Add("closed:Thing");
            illegal.EntityType("closed:Thing").AddField("B", FieldTypeSchema.Int());
            Check.Fault(EngineFaultCode.SchemaInvalid, () => Fixtures.Link(closed, illegal), "closed type");
        });

        Check.Run("K-01 missing references, dependency order and missing dependencies are rejected", () =>
        {
            var dangling = new EnginePackageBuilder("dangling");
            var external = dangling.External("other:Wing");
            var procedure = dangling.Procedure("P");
            procedure.Body(new CallInst(dangling.Ref(external), []));
            Check.Fault(EngineFaultCode.ReferenceUnresolved, () => Fixtures.Link(dangling), "missing external");

            var dependency = new EnginePackageBuilder("dep");
            dependency.EntityType("T");
            var user = new EnginePackageBuilder("user");
            user.Dependencies.Add("dep");
            user.EntityType("U");
            Check.Fault(EngineFaultCode.DependencyCycle, () => Fixtures.Link(user, dependency), "dependency order");

            var missing = new EnginePackageBuilder("missing");
            missing.Dependencies.Add("nope");
            missing.EntityType("V");
            Check.Fault(EngineFaultCode.MissingDependency, () => Fixtures.Link(missing), "missing dependency");
        });

        Check.Run("K-01 a pure procedure cannot reach a side effect", () =>
        {
            var direct = new EnginePackageBuilder("pure-direct");
            var random = direct.Procedure("Random");
            random.Pure();
            random.Body(random.SampleInt("stream", random.Const(0L), random.Const(4L),
                random.Local("x", FieldTypeSchema.Int())));
            Check.Fault(EngineFaultCode.PurityViolation, () => Fixtures.Link(direct), "pure randomness");

            var indirect = new EnginePackageBuilder("pure-indirect");
            var impure = indirect.Procedure("Impure");
            impure.Body(impure.SampleInt("stream", impure.Const(0L), impure.Const(4L),
                impure.Local("y", FieldTypeSchema.Int())));
            var pure = indirect.Procedure("Pure");
            pure.Pure();
            pure.Body(pure.Call(impure.Symbol));
            Check.Fault(EngineFaultCode.PurityViolation, () => Fixtures.Link(indirect), "pure calls impure");
        });

        Check.Run("K-01 type mismatches, untyped arithmetic and slot overruns are rejected", () =>
        {
            var mismatch = new EnginePackageBuilder("types");
            var procedure = mismatch.Procedure("P");
            var slot = procedure.Local("x", FieldTypeSchema.Int());
            procedure.Body(procedure.SetLocal(slot, procedure.Const("text")));
            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(mismatch), "string into an int slot");

            var arithmetic = new EnginePackageBuilder("arith");
            var arithProcedure = arithmetic.Procedure("P");
            var arithSlot = arithProcedure.Local("x", FieldTypeSchema.Int());
            arithProcedure.Body(arithProcedure.SetLocal(arithSlot,
                arithProcedure.Add(arithProcedure.Const("a"), arithProcedure.Const("b"))));
            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(arithmetic), "arithmetic on strings");

            var overrun = new EnginePackageBuilder("slots");
            var overrunProcedure = overrun.Procedure("P");
            overrunProcedure.Body(overrunProcedure.SetLocal(9, overrunProcedure.Const(1L)));
            Check.Fault(EngineFaultCode.TypeError, () => Fixtures.Link(overrun), "slot overrun");
        });

        Check.Run("K-01 scope parents resolve forward and cycles are rejected", () =>
        {
            var package = new EnginePackageBuilder("scopes");
            var child = package.ScopeType("Child");
            var root = package.ScopeType("Root");
            child.Parent = root;
            var rules = Fixtures.Link(package);
            var childDefinition = rules.ScopeTypes.Single(scope => scope.QualifiedId == "scopes:Child");
            var rootDefinition = rules.ScopeTypes.Single(scope => scope.QualifiedId == "scopes:Root");
            Check.Equal(rootDefinition.Id, childDefinition.ParentScopeTypeId, "forward parent resolution");

            var cyclic = new EnginePackageBuilder("cyclic");
            var first = cyclic.ScopeType("First");
            var second = cyclic.ScopeType("Second");
            first.Parent = second;
            second.Parent = first;
            Check.Fault(EngineFaultCode.DependencyCycle, () => Fixtures.Link(cyclic), "scope parent cycle");
        });

        Check.Run("K-01 the rule set identity follows every semantic dimension", () =>
        {
            var baseline = IdentityFor(IdentityMutation.None);
            Check.Equal(baseline, IdentityFor(IdentityMutation.None), "the identity is stable for equal input");

            var dimensions = 0;
            foreach (var mutation in Enum.GetValues<IdentityMutation>())
            {
                if (mutation == IdentityMutation.None) continue;
                Check.False(baseline == IdentityFor(mutation),
                    $"the identity must change when {mutation} changes");
                dimensions++;
            }

            Check.True(dimensions >= 20, $"probed {dimensions} semantic dimensions");
        });
    }

    /// <summary>One semantic dimension the rule set identity must be sensitive to.</summary>
    private enum IdentityMutation
    {
        None,
        FieldBounds,
        FieldVisibility,
        FieldOptional,
        FieldDefault,
        FieldTarget,
        CollectionCapacity,
        CollectionMembershipGroup,
        CollectionOrder,
        CollectionIdentityVisible,
        RelationMany,
        RelationPublic,
        ScopeParent,
        EnumMembers,
        EventPayloadType,
        TerminalFieldType,
        ProcedureParameterType,
        ProcedureResultType,
        ProcedurePurity,
        ActionRole,
        ActionCandidateQuery,
        ActionChoices,
        ProjectionType,
        ProjectionHidden,
        SubscriptionPriority,
        SubscriptionScopePayload,
        PackageVersion,
    }

    /// <summary>
    /// Builds a package that differs from the baseline in exactly one semantic dimension and returns the linked
    /// identity. Every variant must link successfully, so a dimension that cannot be expressed would fail loudly.
    /// </summary>
    private static string IdentityFor(IdentityMutation mutation)
    {
        var package = new EnginePackageBuilder("identity",
            mutation == IdentityMutation.PackageVersion ? "1.0.1" : "1.0.0");

        var box = package.ScopeType("Box");
        var inner = package.ScopeType("Inner");
        if (mutation == IdentityMutation.ScopeParent) inner.Parent = box;

        var kind = package.Enum("Kind");
        kind.Member("A");
        if (mutation == IdentityMutation.EnumMembers) kind.Member("B");

        var thing = package.EntityType("Thing");
        var size = thing.AddField("Size", mutation == IdentityMutation.FieldBounds
            ? FieldTypeSchema.Int(0, 9)
            : FieldTypeSchema.Int(0, 7));
        thing.AddField("Note", FieldTypeSchema.String(16),
            mutation == IdentityMutation.FieldDefault ? Value.Of("x") : null,
            mutation == IdentityMutation.FieldVisibility ? FieldVisibility.Hidden : FieldVisibility.Public,
            // Entity gameplay fields are Persistent today (an execution-only entity field is rejected at link time),
            // so the lifetime is not a probeable dimension any more.
            FieldLifetime.Persistent,
            mutation == IdentityMutation.FieldOptional);
        var other = package.EntityType("Other");
        other.AddField("Value", FieldTypeSchema.Int(0, 7));
        thing.AddField("Link",
            FieldTypeSchema.RefTo(mutation == IdentityMutation.FieldTarget ? other : thing));

        var items = package.Collection("Items", thing);
        items.Capacity = mutation == IdentityMutation.CollectionCapacity ? 2 : 1;
        items.MembershipGroup = mutation == IdentityMutation.CollectionMembershipGroup ? "group" : null;
        items.Ordered = mutation != IdentityMutation.CollectionOrder;
        items.IdentityVisible = mutation != IdentityMutation.CollectionIdentityVisible;

        var pair = package.Relation("Pair", thing);
        pair.Many = mutation != IdentityMutation.RelationMany;
        pair.Public = mutation != IdentityMutation.RelationPublic;

        var pinged = package.Event("Pinged");
        pinged.AddPayload("Item", FieldTypeSchema.RefTo(
            mutation == IdentityMutation.EventPayloadType ? other : thing));
        if (mutation == IdentityMutation.SubscriptionScopePayload)
            pinged.AddPayload("Extra", FieldTypeSchema.RefTo(other));

        var done = package.TerminalResult("Done");
        done.AddField("Count", mutation == IdentityMutation.TerminalFieldType
            ? FieldTypeSchema.String(8)
            : FieldTypeSchema.Int(0, 7));

        var bump = package.Procedure("Bump");
        bump.Param("Value", mutation == IdentityMutation.ProcedureParameterType
            ? FieldTypeSchema.Bool()
            : FieldTypeSchema.Int(0, 7));
        if (mutation == IdentityMutation.ProcedureResultType) bump.Result(FieldTypeSchema.Int(0, 7));
        if (mutation == IdentityMutation.ProcedurePurity) bump.Pure();
        var bumpLocal = bump.Local("local", FieldTypeSchema.Int(0, 7));
        bump.Body(bump.SetLocal(bumpLocal, bump.Const(0L)));

        var spare = package.Collection("Spare", thing);
        var touch = package.Action("Touch");
        var touchItem = touch.Parameter("Item", FieldTypeSchema.RefTo(thing));
        var touchBody = package.ActionBody(touch);
        touchBody.Role(mutation == IdentityMutation.ActionRole ? "other" : "player");
        touchBody.Candidates("Item", touchBody.Collection(
            mutation == IdentityMutation.ActionCandidateQuery ? spare : items));
        if (mutation == IdentityMutation.ActionChoices)
            touchBody.Choose("Item", touchBody.Const(Value.OfRef(InstanceId.None)));
        touchBody.Body(touchBody.SetField(touchBody.Param(touchItem.DeclarationIndex), size,
            touchBody.Const(1L)));

        var view = package.Projection("View");
        view.ParticipantRole = "player";
        view.Type(thing, mutation != IdentityMutation.ProjectionType).IncludeHiddenFields =
            mutation == IdentityMutation.ProjectionHidden;

        var react = package.Procedure("React");
        var reactItem = react.Param("Item", FieldTypeSchema.RefTo(thing));
        react.Body(react.SetField(react.Param(reactItem), size, react.Const(1L)));
        // Changing the payload's target type would make this reaction invalid, so that variant has no subscription;
        // the subscription dimensions are probed by the two Subscription mutations instead.
        if (mutation != IdentityMutation.EventPayloadType)
        {
            package.Subscribe(pinged, react.Symbol, mutation == IdentityMutation.SubscriptionPriority ? 5 : 0,
                null, mutation == IdentityMutation.SubscriptionScopePayload ? "Extra" : null);
        }

        return Fixtures.Link(package).Identity;
    }

    // -- K-02: storage ------------------------------------------------------------------------------------------

    private static void StorageChecks()
    {
        Check.Run("K-02 two sessions never share storage", () =>
        {
            var rules = Fixtures.BuildBoth();
            var left = new Session(rules, 99UL, Fixtures.Roles);
            var right = new Session(rules, 99UL, Fixtures.Roles);
            left.Start();
            right.Start();
            var untouched = right.State.StateHash();

            var move = Action(rules, Grid + "Move");
            var unit = left.LegalActions(Fixtures.White).Single(a => a.Definition.QualifiedId == move.QualifiedId)
                .Candidates[0];
            left.Submit(move, Fixtures.White, [Value.OfRef(unit), Value.Of(1L), Value.Of(0L)]);

            Check.False(left.State.StateHash() == untouched, "the acting session changed");
            Check.Equal(untouched, right.State.StateHash(), "the other session is untouched");
        });

        Check.Run("K-02 membership groups, capacity, order and relations are enforced", () =>
        {
            var rules = Fixtures.BuildBoth();
            var state = new SessionState(rules);
            var unitType = EntityType(rules, UnitId);
            var units = Collection(rules, Grid + "Units").Id;
            var fallen = Collection(rules, Grid + "Fallen").Id;
            var controls = Relation(rules, Grid + "Controls").Id;

            var first = state.CreateInstance(unitType.Id, state.RootScope);
            var second = state.CreateInstance(unitType.Id, state.RootScope);
            var third = state.CreateInstance(unitType.Id, state.RootScope);
            var fourth = state.CreateInstance(unitType.Id, state.RootScope);

            state.AddToCollection(units, first);
            state.AddToCollection(units, second);
            Check.Fault(EngineFaultCode.CapacityExceeded, () => state.AddToCollection(units, third), "capacity");
            Check.Fault(EngineFaultCode.MembershipConflict, () => state.AddToCollection(fallen, first),
                "membership group");
            Check.Equal(2, state.CollectionMembers(units).Count, "membership rolled back");

            state.AddRelation(controls, first, second);
            Check.Fault(EngineFaultCode.MembershipConflict, () => state.AddRelation(controls, first, third),
                "single-valued relation");
            Check.Sequence([second], state.RelationTargets(controls, first), "relation target");
            Check.Fault(EngineFaultCode.TypeError, () => state.AddRelation(controls, first,
                state.CreateInstance(EntityType(rules, Micro + "Card").Id, state.RootScope)), "relation target type");

            state.AddToCollection(fallen, fourth);
            state.AddToCollection(fallen, third);
            var members = state.CollectionMembers(fallen);
            Check.True(members[0] == third && members[1] == fourth,
                "an unordered collection is read in ascending identity order");
            Check.Fault(EngineFaultCode.SchemaInvalid,
                () => state.ShuffleCollection(fallen, new RandomService(1UL), "shuffle"), "shuffling an unordered collection");
        });

        Check.Run("K-02 field types, bounds, references and the destroy policy are enforced", () =>
        {
            var rules = Fixtures.BuildBoth();
            var state = new SessionState(rules);
            var unitType = EntityType(rules, UnitId);
            var target = unitType.FieldIndex("Target");
            var owner = state.CreateInstance(unitType.Id, state.RootScope);
            var other = state.CreateInstance(unitType.Id, state.RootScope);

            Check.Fault(EngineFaultCode.InvalidReference,
                () => state.SetField(other, target, Value.OfRef(new InstanceId(999))), "dangling reference");
            Check.Fault(EngineFaultCode.ValueOutOfRange,
                () => state.SetField(other, unitType.FieldIndex("Strength"), Value.Of(1000L)), "int bounds");
            Check.Fault(EngineFaultCode.TypeError,
                () => state.SetField(other, unitType.FieldIndex("Strength"), Value.Of("x")), "field type");

            state.SetField(other, target, Value.OfRef(owner));
            state.DestroyInstance(owner);
            Check.Equal(ValueKind.None, state.GetField(other, target).Kind,
                "destroying clears an inbound optional reference");
            Check.False(state.Exists(owner), "the destroyed instance is gone");
            Check.False(state.CreateInstance(unitType.Id, state.RootScope) == owner,
                "instance identities are never reused");
        });

        Check.Run("K-02 one failed command leaves no partial write", () =>
        {
            var rules = Fixtures.BuildBoth();
            var state = new SessionState(rules);
            var unitType = EntityType(rules, UnitId);
            var units = Collection(rules, Grid + "Units").Id;
            var before = state.StateHash();
            var version = state.Version;
            var count = state.InstanceCount;

            Check.Fault(EngineFaultCode.ValueOutOfRange, () => state.RunAtomic(() =>
            {
                var created = state.CreateInstance(unitType.Id, state.RootScope);
                state.AddToCollection(units, created);
                state.SetField(created, unitType.FieldIndex("Strength"), Value.Of(500L));
            }), "atomic rollback");

            Check.Equal(before, state.StateHash(), "state rolled back");
            Check.Equal(version, state.Version, "version rolled back");
            Check.Equal(count, state.InstanceCount, "instances rolled back");
        });

        Check.Run("K-02 a seeded shuffle is reproducible", () =>
        {
            var rules = Fixtures.BuildBoth();
            var left = Shuffled(rules);
            var right = Shuffled(rules);
            Check.Equal(left, right, "same seed, same order");
        });
    }

    private static string Shuffled(RuleSet rules)
    {
        var state = new SessionState(rules);
        var unitType = EntityType(rules, UnitId);
        var units = Collection(rules, Grid + "Units").Id;
        for (var index = 0; index < 2; index++)
            state.AddToCollection(units, state.CreateInstance(unitType.Id, state.RootScope));
        state.ShuffleCollection(units, new RandomService(7UL), "combat");
        return string.Join(",", state.CollectionMembers(units).Select(member => member.Value));
    }

    // -- K-03: scheduler ----------------------------------------------------------------------------------------

    private static void SchedulerChecks()
    {
        Check.Run("K-03 loop iterations do not consume call depth", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 5UL, Fixtures.Roles, new SessionBudget(MaxCallDepth: 4));
            session.Start();
            session.Submit(Action(rules, Grid + "Drill"), Fixtures.White, []);
            Check.Equal(50L, Moves(session), "fifty iterations applied");
            Check.Equal(SessionStatus.Running, session.Status, "session still running");
        });

        Check.Run("K-03 unbounded recursion faults on the call-depth budget", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 5UL, Fixtures.Roles, new SessionBudget(MaxCallDepth: 6));
            session.Start();
            Check.Fault(EngineFaultCode.BudgetExceeded,
                () => session.Submit(Action(rules, Grid + "RecurseAction"), Fixtures.White, []), "call depth budget");
            Check.Equal(SessionStatus.Faulted, session.Status, "session faulted");
            Check.Fault(EngineFaultCode.SessionStateInvalid,
                () => session.Submit(Action(rules, Grid + "Drill"), Fixtures.White, []), "no command after a fault");
        });

        Check.Run("K-03 an event cascade faults on the event-depth budget", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 5UL, Fixtures.Roles, new SessionBudget(MaxEventDepth: 4));
            session.Start();
            Check.Fault(EngineFaultCode.BudgetExceeded,
                () => session.Submit(Action(rules, Grid + "CascadeAction"), Fixtures.White, []), "event depth budget");
            Check.Equal(SessionStatus.Faulted, session.Status, "session faulted");
        });

        Check.Run("K-03 a listener binds its payload parameter and runs before the emitter continues", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 11UL, Fixtures.Roles);
            session.Start();
            var unit = Units(session).Single(u => Owner(session, u) == Fixtures.White);
            session.Submit(Action(rules, Grid + "Move"), Fixtures.White,
                [Value.OfRef(unit), Value.Of(1L), Value.Of(2L)]);

            Check.Equal("moved", Text(session, unit, UnitId, "Note"), "payload bound to the reaction parameter");
            Check.Equal(1L, Moves(session), "the listener ran once");
            Check.Equal(1L, Number(session, unit, UnitId, "Column"), "the emitter's own effects applied");
        });

        Check.Run("K-03 the scheduler rejects re-entrancy from an extension", () =>
        {
            var rules = Fixtures.BuildBoth();
            var backend = new ReentrantBackend();
            var session = new Session(rules, 3UL, Fixtures.Roles, null, backend);
            session.Start();
            backend.Session = session;
            backend.Action = Action(rules, Micro + "Score");
            backend.Participant = Fixtures.White;
            var card = HandCard(session, "Strike");
            Check.Fault(EngineFaultCode.SessionStateInvalid,
                () => session.Submit(backend.Action, Fixtures.White, [Value.OfRef(card)]), "re-entrant command");
        });

        Check.Run("K-03 an installed compute backend produces typed outputs", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 3UL, Fixtures.Roles, null, new DoublingBackend());
            session.Start();
            var card = HandCard(session, "Strike");
            session.Submit(Action(rules, Micro + "Score"), Fixtures.White, [Value.OfRef(card)]);
            Check.Equal(12L, Number(session, card, CardId, "Value"), "compute output committed");
        });

        Check.Run("K-03 a loop over a collection iterates a snapshot, not the live list", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 13UL, Fixtures.Roles);
            session.Start();
            var hand = Collection(rules, Micro + "Hand").Id;
            var discards = Collection(rules, Micro + "Discards").Id;
            var before = session.State.CollectionMembers(hand).Count;
            Check.True(before > 1, "the fixture hand has more than one card");

            session.Submit(Action(rules, Micro + "ClearHand"), Fixtures.White, []);

            Check.Equal(0, session.State.CollectionMembers(hand).Count, "every card left the hand");
            Check.Equal(before, session.State.CollectionMembers(discards).Count, "every card reached the discard pile");
        });

        Check.Run("K-03 an execution fault rolls back and stops the session", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 3UL, Fixtures.Roles);
            session.Start();
            var before = session.State.StateHash();
            Check.Fault(EngineFaultCode.DivisionByZero,
                () => session.Submit(Action(rules, Grid + "DivideAction"), Fixtures.White, []), "division by zero");
            Check.Equal(before, session.State.StateHash(), "no partial write");
            Check.Equal(SessionStatus.Faulted, session.Status, "session faulted");
        });

        Check.Run("K-03 a collection capacity overflow faults and rolls back", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 3UL, Fixtures.Roles);
            session.Start();
            var before = session.State.StateHash();
            var count = session.State.InstanceCount;
            Check.Fault(EngineFaultCode.CapacityExceeded,
                () => session.Submit(Action(rules, Grid + "Recruit"), Fixtures.White, []), "capacity overflow");
            Check.Equal(before, session.State.StateHash(), "state rolled back");
            Check.Equal(count, session.State.InstanceCount, "no orphan instance");
        });
    }

    // -- K-04: actions and decisions ----------------------------------------------------------------------------

    private static void DecisionChecks()
    {
        Check.Run("K-04 role and guard gate legality", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 17UL, Fixtures.Roles);
            session.Start();
            var white = session.LegalActions(Fixtures.White);
            Check.Contains(white, a => a.Definition.QualifiedId == Grid + "Move", "white may move");
            Check.DoesNotContain(white, a => a.Definition.QualifiedId == Grid + "MoveBlack", "role gate");
            Check.DoesNotContain(session.LegalActions(Fixtures.Black),
                a => a.Definition.QualifiedId == Grid + "MoveBlack", "guard gate before the turn passes");

            var unit = white.Single(a => a.Definition.QualifiedId == Grid + "Move").Candidates[0];
            session.Submit(Action(rules, Grid + "Move"), Fixtures.White,
                [Value.OfRef(unit), Value.Of(1L), Value.Of(0L)]);
            Check.Contains(session.LegalActions(Fixtures.Black),
                a => a.Definition.QualifiedId == Grid + "MoveBlack", "black may move after the turn passes");
        });

        Check.Run("K-04 an illegal or unauthorised submission changes nothing", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 17UL, Fixtures.Roles);
            session.Start();
            var move = Action(rules, Grid + "Move");
            var units = Units(session);
            var blackUnit = units.Single(u => Owner(session, u) == Fixtures.Black);
            var hash = session.State.StateHash();
            var draws = session.Random.TotalDraws;

            Check.Fault(EngineFaultCode.UnauthorizedAction,
                () => session.Submit(move, Fixtures.Black, [Value.OfRef(blackUnit), Value.Of(1L), Value.Of(0L)]),
                "role authorisation");
            Check.Fault(EngineFaultCode.IllegalAction,
                () => session.Submit(move, Fixtures.White, [Value.OfRef(blackUnit), Value.Of(1L), Value.Of(0L)]),
                "candidate enforcement");

            Check.Equal(hash, session.State.StateHash(), "state untouched");
            Check.Equal(draws, session.Random.TotalDraws, "randomness untouched");
            Check.Equal(SessionStatus.Running, session.Status, "the session is still usable");
            Check.Contains(session.LegalActions(Fixtures.White),
                a => a.Definition.QualifiedId == Grid + "Move", "the action is still legal");
        });

        Check.Run("K-04 a decision suspends, validates and resumes exactly once", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 23UL, Fixtures.Roles);
            session.Start();
            var ledger = One(session, LedgerId);
            var hand = Collection(rules, Micro + "Hand").Id;
            var discards = Collection(rules, Micro + "Discards").Id;

            session.Submit(Action(rules, Micro + "Discard"), Fixtures.White, []);
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "suspended");
            Check.Equal(1L, Number(session, ledger, LedgerId, "Discards"),
                "the effect before the decision already resolved");
            var pending = session.PendingDecision!;
            Check.Equal(3, pending.Candidates.Count, "hand candidates");
            var handCount = session.State.CollectionMembers(hand).Count;
            var deckCard = session.State.CollectionMembers(Collection(rules, Micro + "Deck").Id)[0];

            Check.Fault(EngineFaultCode.StaleDecision,
                () => session.Answer(new DecisionAnswer(pending.RequestId + 5, [pending.Candidates[0]])),
                "stale request id");
            Check.Fault(EngineFaultCode.InvalidDecisionResponse,
                () => session.Answer(new DecisionAnswer(pending.RequestId, [deckCard])), "non-candidate instance");
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "rejections do not resume the session");

            var chosen = pending.Candidates[0];
            session.Answer(new DecisionAnswer(pending.RequestId, [chosen]));
            Check.Equal(SessionStatus.Running, session.Status, "resumed");
            Check.Equal(1L, Number(session, ledger, LedgerId, "Discards"),
                "the resolved effect is not replayed on resume");
            Check.Equal(handCount - 1, session.State.CollectionMembers(hand).Count, "the card left the hand");
            Check.True(session.State.IsMember(discards, chosen), "the card reached the discard pile");
            Check.Fault(EngineFaultCode.IllegalAction,
                () => session.Answer(new DecisionAnswer(pending.RequestId, [chosen])), "repeated answer");
            Check.Equal(SessionStatus.Running, session.Status, "a rejected answer does not fault the session");
        });

        Check.Run("K-04 a decision bound to an older state version is rejected", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 23UL, Fixtures.Roles);
            session.Start();
            session.Submit(Action(rules, Micro + "Discard"), Fixtures.White, []);
            var pending = session.PendingDecision!;
            var ledgerType = EntityType(rules, LedgerId);
            session.State.SetField(One(session, LedgerId), ledgerType.FieldIndex("Discards"), Value.Of(7L));

            Check.Fault(EngineFaultCode.StaleDecision,
                () => session.Answer(new DecisionAnswer(pending.RequestId, [pending.Candidates[0]])),
                "version binding");
            Check.Equal(SessionStatus.AwaitingDecision, session.Status, "still awaiting the same decision");
        });

        Check.Run("K-04 driving the grid to its terminal result stops the session", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 31UL, Fixtures.Roles);
            session.Start();
            var move = Action(rules, Grid + "Move");
            var moveBlack = Action(rules, Grid + "MoveBlack");

            for (var step = 0; step < 3; step++)
            {
                var white = session.LegalActions(Fixtures.White)
                    .Single(a => a.Definition.QualifiedId == move.QualifiedId);
                session.Submit(move, Fixtures.White,
                    [Value.OfRef(white.Candidates[0]), Value.Of(step + 1L), Value.Of(0L)]);
                if (session.Status == SessionStatus.Terminal) break;
                var black = session.LegalActions(Fixtures.Black)
                    .Single(a => a.Definition.QualifiedId == moveBlack.QualifiedId);
                session.Submit(moveBlack, Fixtures.Black,
                    [Value.OfRef(black.Candidates[0]), Value.Of(6L - step), Value.Of(0L)]);
            }

            Check.Equal(SessionStatus.Terminal, session.Status, "terminal status");
            Check.Equal(Grid + "Victory", session.Terminal?.Definition.QualifiedId, "declared terminal result");
            Check.Equal(ValueKind.Participant, session.Terminal!.Fields[0].Kind, "terminal result field");
            Check.Fault(EngineFaultCode.TerminalReached,
                () => session.Submit(move, Fixtures.White, [Value.OfRef(Units(session)[0]), Value.Of(2L), Value.Of(0L)]),
                "no command after a terminal result");
            Check.Equal(5L, Moves(session), "the listener ran for every move");
        });
    }

    // -- K-05: randomness and numerics --------------------------------------------------------------------------

    private static void RandomChecks()
    {
        Check.Run("K-05 named streams are reproducible, independent and read-only to queries", () =>
        {
            var left = new RandomService(42UL);
            var right = new RandomService(42UL);
            var leftValues = Enumerable.Range(0, 8).Select(_ => left.DrawInt("combat", 0, 1000)).ToArray();
            var rightValues = Enumerable.Range(0, 8).Select(_ => right.DrawInt("combat", 0, 1000)).ToArray();
            Check.Sequence(leftValues, rightValues, "same seed replays");
            Check.True(leftValues.Distinct().Count() > 1, "draws vary");
            Check.True(leftValues.All(value => value is >= 0 and < 1000), "draws stay inside the range");

            var named = new RandomService(42UL);
            Check.False(named.DrawUInt64("combat") == named.DrawUInt64("reward"),
                "stream names derive different states");

            var interleaved = new RandomService(7UL);
            var plain = new RandomService(7UL);
            var reference = plain.DrawInt("a", 0, 100);
            interleaved.DrawInt("other", 0, 100);
            Check.Equal(reference, interleaved.DrawInt("a", 0, 100), "streams do not influence each other");

            var counts = new RandomService(1UL);
            counts.DrawInt("x", 0, 10);
            var before = counts.DrawCount("x");
            _ = counts.Diagnostics();
            _ = counts.Capture();
            _ = counts.DrawCount("missing");
            Check.Equal(before, counts.DrawCount("x"), "diagnostics never consume randomness");

            var snapshot = counts.Capture();
            var replayed = counts.DrawInt("x", 0, 10);
            counts.Restore(snapshot);
            Check.Equal(replayed, counts.DrawInt("x", 0, 10), "restore replays the same draw");

            Check.Fault(EngineFaultCode.ValueOutOfRange, () => counts.DrawInt("x", 5, 5), "empty range");
            Check.Fault(EngineFaultCode.SchemaInvalid, () => counts.DrawInt(string.Empty, 0, 5), "stream name");
        });

        Check.Run("K-05 inst.samplePick preserves query order; packages may explicitly sort", () =>
        {
            static InstanceId Run(ulong seed, bool reverseMembers, bool explicitSort = false)
            {
                var package = new EnginePackageBuilder("sample-pick", "1.0.0");
                var unit = package.EntityType("Unit");
                var tag = unit.AddField("Tag", FieldTypeSchema.Int(0, 99));
                var boardScope = package.ScopeType("BoardScope");
                var units = package.Collection("Units", unit);
                var chosen = package.Collection("Chosen", unit);

                var place = package.Action("Place");
                var placeBody = package.ActionBody(place);
                placeBody.Role("board");
                var scopeLocal = placeBody.Local("board", FieldTypeSchema.Binding());
                var created = placeBody.Local("unit", FieldTypeSchema.Binding());
                var steps = new List<Inst> { placeBody.CreateScope(boardScope, scopeLocal) };
                for (var index = 0; index < 3; index++)
                {
                    steps.Add(placeBody.CreateEntity(unit, placeBody.LocalRef(scopeLocal), created,
                        [(tag, placeBody.Const((long)index))], units));
                }

                placeBody.Body(steps.ToArray());

                var pick = package.Action("Pick");
                var pickBody = package.ActionBody(pick);
                pickBody.Role("board");
                var slot = pickBody.Local("picked", FieldTypeSchema.Binding());
                pickBody.Body(
                    pickBody.SamplePick("fixture-stream", explicitSort
                        ? pickBody.Sort(pickBody.Collection(units), item => pickBody.Field(item, tag))
                        : pickBody.Collection(units), slot),
                    pickBody.RemoveFromCollection(units, pickBody.LocalRef(slot)),
                    pickBody.AddToCollection(chosen, pickBody.LocalRef(slot)));

                var builder = new RuleSetBuilder();
                builder.Add(package.Build());
                var rules = builder.Build();
                var unitsId = rules.Collections.Single(entry => entry.QualifiedId == "sample-pick:Units").Id;
                var chosenId = rules.Collections.Single(entry => entry.QualifiedId == "sample-pick:Chosen").Id;

                var session = new Session(rules, seed, ["board"]);
                session.Start();
                var participant = new ParticipantId(1);
                var placeAction = session.LegalActions(participant)
                    .Single(action => action.Definition.QualifiedId == "sample-pick:Place");
                session.Submit(placeAction.Definition, participant, []);
                Check.Equal(3, session.State.CollectionMembers(unitsId).Count, "三个单位都进了集合");

                if (reverseMembers)
                {
                    var members = session.State.CollectionMembers(unitsId);
                    session.State.RunAtomic(() =>
                    {
                        foreach (var member in members) session.State.RemoveFromCollection(unitsId, member);
                        foreach (var member in members.Reverse()) session.State.AddToCollection(unitsId, member);
                    });
                }

                var pickAction = session.LegalActions(participant)
                    .Single(action => action.Definition.QualifiedId == "sample-pick:Pick");
                session.Submit(pickAction.Definition, participant, []);
                return session.State.CollectionMembers(chosenId).Single();
            }

            var changed = false;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var straight = Run(seed, reverseMembers: false);
                var reversed = Run(seed, reverseMembers: true);
                Check.Equal(4L, straight.Value + reversed.Value, "same index selects the mirrored member");
                changed |= straight != reversed;
                Check.Equal(Run(seed, false, true), Run(seed, true, true), "explicit sort restores package-defined order");
            }
            Check.True(changed, "reversing candidate order must be observable for a non-middle draw");

            // 对照：同一集合顺序下换种子可以采到另一个实例（采样确实在消费命名流）。
            var seeds = Enumerable.Range(1, 12).Select(value => Run((ulong)value, reverseMembers: false)).ToList();
            Check.True(seeds.Distinct().Count() > 1, $"不同种子必须能采到不同实例（实际 {seeds.Distinct().Count()} 种）");
        });
        Check.Run("K-05 the numeric profile owns rounding, overflow and division", () =>
        {
            Check.Throws<ArgumentOutOfRangeException>(
                () => new NumericProfile(13, MidpointRounding.ToEven, true), "fixed scale bound");
            var profile = new NumericProfile(2, MidpointRounding.ToEven, true);
            Check.Equal(1.00m, profile.Quantize(1.004m), "round half to even at the declared scale");
            Check.Fault(EngineFaultCode.ArithmeticOverflow, () => profile.AddInt(long.MaxValue, 1L), "checked overflow");
            Check.Fault(EngineFaultCode.ArithmeticOverflow, () => profile.AddFixed(decimal.MaxValue, 1m),
                "fixed-point overflow");
            Check.Fault(EngineFaultCode.ArithmeticOverflow, () => profile.DivideInt(long.MinValue, -1L),
                "division overflow");
            Check.Fault(EngineFaultCode.DivisionByZero, () => profile.DivideInt(1L, 0L), "integer division by zero");
            Check.Fault(EngineFaultCode.DivisionByZero, () => profile.DivideFixed(1m, 0m), "fixed division by zero");
        });
    }

    // -- K-06: projection ---------------------------------------------------------------------------------------

    private static void ProjectionChecks()
    {
        Check.Run("K-06 projections hide private, hidden, unordered and identity-hidden data", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 41UL, Fixtures.Roles);
            session.Start();

            var player = session.Observe(Fixtures.White, Micro + "PlayerView");
            Check.Equal(Micro + "PlayerView", player.ProjectionId, "projection id");
            Check.DoesNotContain(player.Instances, i => i.TypeId == LedgerId,
                "a type outside the projection stays invisible");
            // The deck's identity is hidden, so a card that is only in the deck is not projected at all.
            Check.Equal(3, player.Instances.Count, "visible card instances");
            var deckCollection = player.Collections.Single(c => c.QualifiedId == Micro + "Deck");
            Check.Equal(0, deckCollection.Members.Count, "hidden deck members are not listed");
            Check.True(deckCollection.Size == 1, "the deck still reports its size");

            var own = player.Instances.Single(i => Text(i, "Name") == "Strike");
            Check.Contains(own.Fields, f => f.Name == "Secret", "the owner sees own-private fields");
            Check.Contains(own.Fields, f => f.Name == "Known" && f.Value.AsBool(), "pack-declared known flag");
            Check.DoesNotContain(own.Fields, f => f.Name == "Legacy", "an engine-hidden field stays hidden");
            Check.True(own.Fields.All(f => f.Name != "Legacy"), "hidden field not present");

            var foreign = player.Instances.Single(i => Text(i, "Name") == "Bash");
            Check.DoesNotContain(foreign.Fields, f => f.Name == "Holder", "owner-private owner field");
            Check.DoesNotContain(foreign.Fields, f => f.Name == "Secret", "owner-private secret");

            var hand = player.Collections.Single(c => c.QualifiedId == Micro + "Hand");
            Check.True(hand.Size == 3, "hand size is public");
            Check.False(hand.OrderVisible, "hand order is hidden");
            var deck = player.Collections.Single(c => c.QualifiedId == Micro + "Deck");
            Check.True(deck.Size == 1, "deck size is public");
            Check.Equal(0, deck.Members.Count, "deck identity is hidden");

            var audit = session.Observe(Fixtures.Black, Micro + "AuditView");
            var audited = audit.Instances.Single(i => Text(i, "Name") == "Strike");
            Check.Contains(audited.Fields, f => f.Name == "Legacy", "the audit projection reveals hidden fields");
            Check.DoesNotContain(audited.Fields, f => f.Name == "Secret", "owner-private stays with the owner");

            Check.Fault(EngineFaultCode.NoProjection,
                () => session.Observe(Fixtures.White, Micro + "AuditView"), "projection role");
        });

        Check.Run("K-06 repeated observation and legal querying change nothing", () =>
        {
            var rules = Fixtures.BuildBoth();
            var session = new Session(rules, 41UL, Fixtures.Roles);
            session.Start();
            var hash = session.State.StateHash();
            var version = session.StateVersion;
            var draws = session.Random.TotalDraws;

            var first = Describe(session.Observe(Fixtures.White, Micro + "PlayerView"));
            _ = session.LegalActions(Fixtures.White);
            _ = session.LegalActions(Fixtures.Black);
            var second = Describe(session.Observe(Fixtures.White, Micro + "PlayerView"));

            Check.Equal(first, second, "views are stable");
            Check.Equal(hash, session.State.StateHash(), "state untouched");
            Check.Equal(version, session.StateVersion, "version untouched");
            Check.Equal(draws, session.Random.TotalDraws, "randomness untouched");
        });
    }

    private static string Describe(ParticipantView view) =>
        string.Join(";", view.Instances.Select(instance =>
            $"{instance.TypeId}#{instance.Id.Value}[{string.Join(",", instance.Fields.Select(f => f.Name + "=" + f.Value))}]"))
        + "|" + string.Join(";", view.Collections.Select(collection =>
            $"{collection.QualifiedId}:{collection.Size}:{string.Join(",", collection.Members.Select(m => m.Value))}"))
        + "|" + string.Join(";", view.Relations.Select(relation =>
            $"{relation.QualifiedId}({relation.Owner.Value})->{string.Join(",", relation.Targets.Select(t => t.Value))}"));

    // -- shared helpers -----------------------------------------------------------------------------------------

    private static EntityTypeDefinition EntityType(RuleSet rules, string qualifiedId) =>
        rules.TypeByQualifiedId(qualifiedId);

    private static CollectionDefinition Collection(RuleSet rules, string qualifiedId) =>
        rules.Collections.Single(collection => collection.QualifiedId == qualifiedId);

    private static RelationDefinition Relation(RuleSet rules, string qualifiedId) =>
        rules.Relations.Single(relation => relation.QualifiedId == qualifiedId);

    private static ActionDefinition Action(RuleSet rules, string qualifiedId) =>
        rules.Actions.Single(action => action.QualifiedId == qualifiedId);

    private static IReadOnlyList<InstanceId> Units(Session session) =>
        session.State.AllInstances(EntityType(session.RuleSet, UnitId).Id);

    private static ParticipantId Owner(Session session, InstanceId unit) =>
        session.State.GetField(unit, EntityType(session.RuleSet, UnitId).FieldIndex("Owner")).AsParticipant();

    private static InstanceId One(Session session, string qualifiedId) =>
        session.State.AllInstances(EntityType(session.RuleSet, qualifiedId).Id).Single();

    private static InstanceId HandCard(Session session, string name)
    {
        var type = EntityType(session.RuleSet, CardId);
        var hand = Collection(session.RuleSet, Micro + "Hand").Id;
        return session.State.CollectionMembers(hand)
            .Single(card => session.State.GetField(card, type.FieldIndex("Name")).AsString() == name);
    }

    private static long Number(Session session, InstanceId instance, string qualifiedId, string field)
    {
        var type = EntityType(session.RuleSet, qualifiedId);
        return session.State.GetField(instance, type.FieldIndex(field)).AsInt();
    }

    private static string Text(Session session, InstanceId instance, string qualifiedId, string field)
    {
        var type = EntityType(session.RuleSet, qualifiedId);
        return session.State.GetField(instance, type.FieldIndex(field)).AsString();
    }

    private static string Text(ProjectedInstance instance, string field) =>
        instance.Fields.Single(f => f.Name == field).Value.AsString();

    private static long Moves(Session session) => Number(session, One(session, GameId), GameId, "Moves");

    private sealed class ReentrantBackend : IComputeBackend
    {
        public string Id => "test";

        public Session? Session { get; set; }

        public ActionDefinition? Action { get; set; }

        public ParticipantId Participant { get; set; }

        public void Execute(ComputeAssetDefinition asset, IReadOnlyList<Value> inputs, Value[] outputs)
        {
            Session!.Submit(Action!, Participant, []);
            outputs[0] = inputs[0];
        }
    }

    private sealed class DoublingBackend : IComputeBackend
    {
        public string Id => "test";

        public void Execute(ComputeAssetDefinition asset, IReadOnlyList<Value> inputs, Value[] outputs) =>
            outputs[0] = Value.Of(inputs[0].AsInt() * 2);
    }
}
