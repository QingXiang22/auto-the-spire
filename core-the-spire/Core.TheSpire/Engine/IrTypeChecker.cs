namespace CoreTheSpire.Engine;

/// <summary>
/// Link-time IR definition check (K-01). This is a <em>definition</em> check, not a runtime check: it reads the linked
/// tables and never mutates engine state. It is the only place that rejects a rule set which is internally
/// inconsistent, so a rejected rule set must never reach the scheduler.
///
/// It enforces:
/// <list type="bullet">
/// <item>every reference slot resolves to the declaration kind the instruction expects (missing reference);</item>
/// <item>every expression has a statically known type and the operands of an operator agree;</item>
/// <item>frame slots (parameters, locals, query/decision/compute bindings) exist and receive assignable values;</item>
/// <item>field initialisers belong to the owner the instruction declares and are not repeated;</item>
/// <item>query item bindings are in scope only inside their predicate, key, aggregate value or loop body;</item>
/// <item>a pure procedure cannot reach a side effect (state write, randomness, event, decision, impure call);</item>
/// <item>compute inputs/outputs, static compute budgets, projection roles and scope-type parenting are consistent;</item>
/// <item>every entity gameplay field is <see cref="FieldLifetime.Persistent"/> (execution-only data belongs in a frame
/// slot, never in an instance field).</item>
/// </list>
///
/// Deliberately <em>not</em> checked here (they are runtime concerns): whether a query has any candidates, whether a
/// collection is at capacity, whether a decision response is stale, and whether an action guard holds.
/// </summary>
internal static class IrTypeChecker
{
    public static void Check(LinkedTables tables, Resolution[] resolutions, IReadOnlyList<EnginePackage> packages) =>
        new Checker(tables, resolutions, packages).Run();

    /// <summary>Effects a pure procedure may never reach, directly or through a call.</summary>
    private static readonly EngineEffect[] SideEffects =
    [
        EngineEffect.WritesState,
        EngineEffect.ConsumesRandom,
        EngineEffect.EmitsEvent,
        EngineEffect.RequestsDecision,
        EngineEffect.EndsSession,
    ];

    private sealed class Checker
    {
        private readonly LinkedTables _tables;
        private readonly Resolution[] _resolutions;
        private readonly IReadOnlyList<EnginePackage> _packages;

        public Checker(LinkedTables tables, Resolution[] resolutions, IReadOnlyList<EnginePackage> packages)
        {
            _tables = tables;
            _resolutions = resolutions;
            _packages = packages;
        }

        public void Run()
        {
            CheckTables();
            foreach (var procedure in _tables.Procedures) CheckProcedure(procedure);
            foreach (var action in _tables.Actions) CheckAction(action);
            foreach (var subscription in _tables.Subscriptions) CheckSubscription(subscription);
            foreach (var asset in _tables.ComputeAssets) CheckComputeAsset(asset);
            foreach (var projection in _tables.Projections) CheckProjection(projection);
        }

        // -- table-level checks ---------------------------------------------------------------------------------

        private void CheckTables()
        {
            foreach (var type in _tables.Types)
            {
                RequireUniqueNames(type.QualifiedId, type.Fields);
                RequirePersistentGameplayFields(type);
            }

            foreach (var eventType in _tables.Events) RequireUniqueNames(eventType.QualifiedId, eventType.Payload);
            foreach (var terminal in _tables.Terminals) RequireUniqueNames(terminal.QualifiedId, terminal.Fields);
            foreach (var asset in _tables.ComputeAssets)
            {
                RequireUniqueNames(asset.QualifiedId + " inputs", asset.Inputs);
                RequireUniqueNames(asset.QualifiedId + " outputs", asset.Outputs);
            }

            for (var id = 0; id < _tables.ScopeTypes.Count; id++)
            {
                var scope = _tables.ScopeTypes[id];
                if (scope.Id != id) throw Faults.Schema($"scope type table is not dense at {id}");
                if (scope.ParentScopeTypeId == id)
                    throw Faults.Of(EngineFaultCode.DependencyCycle,
                        $"scope type '{scope.QualifiedId}' declares itself as its parent");

                var seen = new HashSet<int> { id };
                var parent = scope.ParentScopeTypeId;
                var depth = 0;
                while (parent >= 0)
                {
                    if (!seen.Add(parent))
                        throw Faults.Of(EngineFaultCode.DependencyCycle,
                            $"scope type parent chain of '{scope.QualifiedId}' contains a cycle");
                    if (++depth > _tables.ScopeTypes.Count)
                        throw Faults.Of(EngineFaultCode.DependencyCycle,
                            $"scope type parent chain of '{scope.QualifiedId}' is not finite");
                    parent = _tables.ScopeTypes[parent].ParentScopeTypeId;
                }
            }

            foreach (var collection in _tables.Collections)
            {
                if (collection.ElementTypeId < 0 || collection.ElementTypeId >= _tables.Types.Count)
                    throw Faults.Unresolved($"collection '{collection.QualifiedId}' has no element type in the rule set");
            }

            foreach (var relation in _tables.Relations)
            {
                if (relation.TargetTypeId < 0 || relation.TargetTypeId >= _tables.Types.Count)
                    throw Faults.Unresolved($"relation '{relation.QualifiedId}' has no target type in the rule set");
            }
        }

        private static void RequireUniqueNames(string owner, IReadOnlyList<FieldDefinition> fields)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (!seen.Add(field.Name))
                    throw Faults.Of(EngineFaultCode.DuplicateDeclaration,
                        $"'{owner}' declares the field name '{field.Name}' more than once");
            }
        }

        /// <summary>
        /// An entity gameplay field must be <see cref="FieldLifetime.Persistent"/> today. An execution-only field
        /// would be live data of a live instance that no snapshot, no state hash and no projection accounts for, so it
        /// is rejected instead of silently disappearing from observation. Temporary values belong in a frame slot: a
        /// procedure parameter or local, a query/decision/compute binding, an event payload, a terminal result field
        /// or a compute input/output — all of which are execution-only <em>by construction</em> and stay legal.
        /// </summary>
        private static void RequirePersistentGameplayFields(EntityTypeDefinition type)
        {
            foreach (var field in type.Fields)
            {
                if (field.Lifetime != FieldLifetime.ExecutionOnly) continue;
                throw Faults.Of(EngineFaultCode.Unsupported,
                    $"entity type '{type.QualifiedId}' declares execution-only field '{field.Name}': entity gameplay " +
                    "fields must be Persistent; keep temporary values in a frame slot (procedure parameter or local, " +
                    "query/decision/compute binding, event payload, terminal result field or compute input/output)");
            }
        }

        // -- procedures -----------------------------------------------------------------------------------------

        private void CheckProcedure(ProcedureDefinition procedure)
        {
            var frame = new Frame(procedure);
            CheckBlock(frame, procedure.Body, procedure.QualifiedId);
        }

        private void CheckAction(ActionDefinition action)
        {
            var frame = new Frame(action.Program);
            var where = action.QualifiedId;
            var domains = new HashSet<int>();
            if (action.CandidateParameterIndex >= 0) domains.Add(action.CandidateParameterIndex);
            foreach (var index in action.Choices.Keys) domains.Add(index);

            if (action.CandidateQuery is not null)
            {
                var element = CheckQuery(frame, action.CandidateQuery, where);
                if (action.CandidateParameterIndex < 0 || action.CandidateParameterIndex >= action.Program.Parameters.Count)
                    throw Faults.Unresolved($"{where}: candidate parameter index is out of range");
                var parameter = action.Program.Parameters[action.CandidateParameterIndex];
                var slot = SlotOf(frame, parameter, where);
                if (slot.Kind == FieldTypeKind.Any) frame.Bindings[action.CandidateParameterIndex] = element;
                else Require(element, slot, where, $"candidate parameter '{parameter.Name}'");
                RequireDomainedParameters(action.CandidateQuery, domains, where, "candidate query");
            }
            else if (action.CandidateParameterIndex >= 0)
            {
                throw Faults.Schema($"{where}: candidate parameter is declared without a candidate query");
            }

            foreach (var (index, values) in action.Choices)
            {
                if (index < 0 || index >= action.Program.Parameters.Count)
                    throw Faults.Unresolved($"{where}: choice index {index} is out of range");
                var parameter = action.Program.Parameters[index];
                var expected = SlotOf(frame, parameter, where);
                foreach (var value in values)
                {
                    Require(CheckExpr(frame, value, where), expected, where, $"choice for '{parameter.Name}'");
                    RequireDomainedParameters(value, domains, where, $"choice for '{parameter.Name}'");
                }
            }

            if (action.Guard is null) return;
            Require(CheckExpr(frame, action.Guard, where), Ty.Bool, where, "action guard");
            RequireDomainedParameters(action.Guard, domains, where, "action guard");
        }

        /// <summary>
        /// An action's guard, candidate query and choices are evaluated before the host supplies any argument, so they
        /// may only read parameters the engine can enumerate (a candidate query or declared choices). A reference to
        /// any other parameter would make availability depend on host input.
        /// </summary>
        private static void RequireDomainedParameters(IrNode node, HashSet<int> domains, string where, string what)
        {
            foreach (var slot in ParameterSlots(node))
            {
                if (domains.Contains(slot)) continue;
                throw Faults.Schema(
                    $"{where}: {what} reads parameter {slot}, which declares neither a candidate query nor choices; " +
                    "availability must be decidable without host input");
            }
        }

        private static IEnumerable<int> ParameterSlots(IrNode node)
        {
            if (node is ParamExpr parameter) yield return parameter.Slot;
            foreach (var child in node.Children)
            {
                foreach (var slot in ParameterSlots(child)) yield return slot;
            }
        }

        private void CheckSubscription(SubscriptionDefinition subscription)
        {
            var reaction = subscription.Reaction;
            if (reaction.Parameters.Count > subscription.EventType.Payload.Count)
            {
                throw Faults.TypeError(
                    $"subscription {reaction.QualifiedId}: the reaction declares {reaction.Parameters.Count} " +
                    $"parameters but event '{subscription.EventType.QualifiedId}' carries " +
                    $"{subscription.EventType.Payload.Count} payload values");
            }

            for (var index = 0; index < reaction.Parameters.Count; index++)
            {
                Require(TyOf(subscription.EventType.Payload[index]), TyOf(reaction.Parameters[index]),
                    $"subscription {reaction.QualifiedId}",
                    $"payload '{subscription.EventType.Payload[index].Name}' bound to parameter " +
                    $"'{reaction.Parameters[index].Name}'");
            }

            if (subscription.ScopePayloadIndex >= 0)
            {
                var binding = subscription.EventType.Payload[subscription.ScopePayloadIndex];
                if (binding.Kind != FieldTypeKind.Ref)
                {
                    throw Faults.TypeError(
                        $"subscription {reaction.QualifiedId}: scope payload '{binding.Name}' of " +
                        $"'{subscription.EventType.QualifiedId}' is {binding.Kind}, not an entity reference");
                }
            }

            if (subscription.Recheck is null) return;
            // The predicate is authored by the subscribing package, while its slots are the reaction parameters.
            var frame = new Frame(subscription.Reaction with
            {
                PackageIndex = subscription.PackageIndex,
                RefBase = subscription.RefBase,
            });
            var where = $"subscription {subscription.Reaction.QualifiedId}";
            Require(CheckExpr(frame, subscription.Recheck, where), Ty.Bool, where, "recheck predicate");
        }

        private void CheckComputeAsset(ComputeAssetDefinition asset)
        {
            var where = asset.QualifiedId;
            if (string.IsNullOrWhiteSpace(asset.BackendId))
                throw Faults.Schema($"{where}: compute asset declares no backend id");
            if (string.IsNullOrWhiteSpace(asset.SourcePath))
                throw Faults.Schema($"{where}: compute asset declares no source path");
            if (string.IsNullOrWhiteSpace(asset.SourceHash))
                throw Faults.Schema($"{where}: compute asset declares no source hash; the source must participate in identity");
            if (string.IsNullOrWhiteSpace(asset.Entry))
                throw Faults.Schema($"{where}: compute asset declares no entry point");

            var limits = asset.Limits;
            if (limits.MaxInputValues <= 0) throw Faults.Schema($"{where}: MaxInputValues must be positive");
            if (limits.MaxOutputValues <= 0) throw Faults.Schema($"{where}: MaxOutputValues must be positive");
            if (limits.MaxInstructions <= 0) throw Faults.Schema($"{where}: MaxInstructions must be positive");
            if (limits.MaxHeapBytes <= 0) throw Faults.Schema($"{where}: MaxHeapBytes must be positive");
            if (limits.MaxCallDepth <= 0) throw Faults.Schema($"{where}: MaxCallDepth must be positive");
            if (asset.Inputs.Count > limits.MaxInputValues)
                throw Faults.Of(EngineFaultCode.ComputeLimitExceeded,
                    $"{where}: {asset.Inputs.Count} inputs exceed the declared budget of {limits.MaxInputValues}");
            if (asset.Outputs.Count > limits.MaxOutputValues)
                throw Faults.Of(EngineFaultCode.ComputeLimitExceeded,
                    $"{where}: {asset.Outputs.Count} outputs exceed the declared budget of {limits.MaxOutputValues}");
            if (asset.Inputs.Count == 0 && asset.Outputs.Count == 0)
                throw Faults.Schema($"{where}: compute asset declares neither inputs nor outputs");
        }

        private void CheckProjection(ProjectionDefinition projection)
        {
            var where = projection.QualifiedId;
            if (string.IsNullOrWhiteSpace(projection.ParticipantRole))
                throw Faults.Schema($"{where}: projection declares no participant role");
            if (projection.Types.Count == 0)
                throw Faults.Schema($"{where}: projection declares no entity type");

            foreach (var (typeId, type) in projection.Types)
            {
                if (typeId != type.Type.Id)
                    throw Faults.Schema($"{where}: projection entry key {typeId} does not match type id {type.Type.Id}");
                if (!type.Include) continue;
                if (type.OwnerFieldIndex >= 0)
                {
                    var owner = type.Type.Fields[type.OwnerFieldIndex];
                    if (owner.Kind != FieldTypeKind.Participant)
                    {
                        throw Faults.TypeError(
                            $"{where}: owner field '{owner.Name}' of '{type.Type.QualifiedId}' is " +
                            $"{owner.Kind}, not a participant");
                    }
                }
            }

            foreach (var reveal in projection.Reveals)
            {
                if (!projection.Types.TryGetValue(reveal.AuthorityTypeId, out var authority) || !authority.Include)
                {
                    throw Faults.Schema(
                        $"{where}: reveal authority type {reveal.AuthorityTypeId} is not an included projection entry");
                }

                if (reveal.ViewerFieldIndex < 0 || reveal.ViewerFieldIndex >= authority.Type.Fields.Count)
                {
                    throw Faults.Schema(
                        $"{where}: reveal viewer field index {reveal.ViewerFieldIndex} is out of range for " +
                        $"'{authority.Type.QualifiedId}'");
                }

                var viewer = authority.Type.Fields[reveal.ViewerFieldIndex];
                if (viewer.Kind != FieldTypeKind.Participant)
                {
                    throw Faults.TypeError(
                        $"{where}: reveal viewer field '{viewer.Name}' of '{authority.Type.QualifiedId}' is " +
                        $"{viewer.Kind}, not a participant");
                }
            }
        }

        // -- instructions ---------------------------------------------------------------------------------------

        private void CheckBlock(Frame frame, Block block, string where)
        {
            for (var i = 0; i < block.Count; i++) CheckInst(frame, block[i], $"{where}[{i}]");
        }

        private void CheckInst(Frame frame, Inst inst, string where)
        {
            WithSource(inst, () => { CheckInstCore(frame, inst, where); return true; });
        }

        private static T WithSource<T>(IrNode node, Func<T> check)
        {
            try { return check(); }
            catch (EngineFaultException error) when (error.Fault.Context is null && node.SourceLocation is not null)
            {
                throw new EngineFaultException(error.Fault with { Context = node.SourceLocation.Value.ToString() }, error);
            }
        }

        private void CheckInstCore(Frame frame, Inst inst, string where)
        {
            if (frame.IsPure) RequirePure(frame, inst, where);
            _ = inst.Signature;

            switch (inst)
            {
                case SetLocalInst setLocal:
                {
                    var expected = SlotOf(frame, setLocal.Slot, where);
                    Require(CheckExpr(frame, setLocal.Value, where), expected, where,
                        $"frame slot {setLocal.Slot}");
                    break;
                }

                case SetFieldInst setField:
                {
                    var field = RequireRef<FieldResolution>(frame, setField.FieldRef, where, "field");
                    RequireEntityField(field, where);
                    RequireEntityTarget(CheckExpr(frame, setField.Target, where), field.OwnerId, where,
                        $"field '{field.Field.Name}'");
                    Require(CheckExpr(frame, setField.Value, where), TyOf(field.Field), where,
                        $"field '{field.Field.Name}'");
                    break;
                }

                case CreateEntityInst create:
                {
                    var type = RequireRef<TypeResolution>(frame, create.TypeRef, where, "entity type");
                    RequireScope(CheckExpr(frame, create.Scope, where), where, "entity scope");
                    CheckFieldInits(frame, create.Fields, FieldOwnerKind.Entity, type.Type.Id, where,
                        $"type '{type.Type.QualifiedId}'");
                    Bind(frame, create.ResultSlot, Ty.RefTo(type.Type.Id), where);
                    if (create.AddToCollectionRef >= 0)
                    {
                        var collection = RequireRef<CollectionResolution>(frame, create.AddToCollectionRef, where,
                            "collection");
                        RequireCollectionElement(collection.Collection, Ty.RefTo(type.Type.Id), where);
                    }

                    CheckInsertPosition(frame, create.Position, create.PositionIndex, where);
                    break;
                }

                case CreateScopeInst createScope:
                {
                    var scopeType = RequireRef<ScopeResolution>(frame, createScope.ScopeTypeRef, where, "scope type");
                    if (scopeType.ScopeType.ParentScopeTypeId < 0 && createScope.Parent is not null)
                    {
                        throw Faults.Schema(
                            $"{where}: scope type '{scopeType.ScopeType.QualifiedId}' is a root scope type but a " +
                            "parent scope expression was supplied");
                    }

                    if (createScope.Parent is not null) RequireScope(CheckExpr(frame, createScope.Parent, where), where, "parent scope");
                    Bind(frame, createScope.ResultSlot, Ty.Scope, where);
                    break;
                }

                case DestroyEntityInst destroy:
                {
                    var target = CheckExpr(frame, destroy.Target, where);
                    if (destroy.ScopeSubtree) RequireScope(target, where, "scope destruction target");
                    else RequireEntityReference(target, where, "destruction target");
                    break;
                }

                case AddToCollectionInst add:
                {
                    var collection = RequireRef<CollectionResolution>(frame, add.CollectionRef, where, "collection");
                    RequireCollectionElement(collection.Collection, CheckExpr(frame, add.Item, where), where);
                    CheckInsertPosition(frame, add.Position, add.PositionIndex, where);
                    break;
                }

                case RemoveFromCollectionInst remove:
                {
                    var collection = RequireRef<CollectionResolution>(frame, remove.CollectionRef, where, "collection");
                    RequireCollectionElement(collection.Collection, CheckExpr(frame, remove.Item, where), where);
                    break;
                }

                case MoveInCollectionInst move:
                {
                    var collection = RequireRef<CollectionResolution>(frame, move.CollectionRef, where, "collection");
                    RequireCollectionElement(collection.Collection, CheckExpr(frame, move.Item, where), where);
                    CheckInsertPosition(frame, move.Position, move.PositionIndex, where);
                    if (!collection.Collection.Ordered)
                    {
                        throw Faults.Schema(
                            $"{where}: collection '{collection.Collection.QualifiedId}' is unordered; " +
                            "moving inside it has no defined meaning");
                    }

                    break;
                }

                case ShuffleCollectionInst shuffle:
                {
                    var collection = RequireRef<CollectionResolution>(frame, shuffle.CollectionRef, where, "collection");
                    if (!collection.Collection.Ordered)
                    {
                        throw Faults.Schema(
                            $"{where}: collection '{collection.Collection.QualifiedId}' is unordered; " +
                            "shuffling it has no defined meaning");
                    }

                    RequireStream(CheckExpr(frame, shuffle.Stream, where), where);
                    break;
                }

                case SetRelationInst setRelation:
                {
                    var relation = RequireRef<RelationResolution>(frame, setRelation.RelationRef, where, "relation");
                    RequireEntityReference(CheckExpr(frame, setRelation.Owner, where), where, "relation owner");
                    var target = CheckExpr(frame, setRelation.Target, where);
                    if (target.Kind != FieldTypeKind.Ref && target.Kind != FieldTypeKind.Any)
                    {
                        throw Faults.TypeError(
                            $"{where}: relation '{relation.Relation.QualifiedId}' needs an entity reference target " +
                            $"but found {Describe(target)}");
                    }

                    if (target.TargetId >= 0 && target.TargetId != relation.Relation.TargetTypeId)
                    {
                        throw Faults.TypeError(
                            $"{where}: relation '{relation.Relation.QualifiedId}' targets " +
                            $"'{_tables.Types[relation.Relation.TargetTypeId].QualifiedId}' but the value is " +
                            $"'{_tables.Types[target.TargetId].QualifiedId}'");
                    }

                    break;
                }

                case ClearRelationInst clearRelation:
                {
                    _ = RequireRef<RelationResolution>(frame, clearRelation.RelationRef, where, "relation");
                    RequireEntityReference(CheckExpr(frame, clearRelation.Owner, where), where, "relation owner");
                    break;
                }

                case SampleIntInst sampleInt:
                {
                    RequireStream(CheckExpr(frame, sampleInt.Stream, where), where);
                    Require(CheckExpr(frame, sampleInt.Min, where), Ty.Int, where, "sampling lower bound");
                    Require(CheckExpr(frame, sampleInt.Max, where), Ty.Int, where, "sampling upper bound");
                    Bind(frame, sampleInt.ResultSlot, Ty.Int, where);
                    break;
                }

                case SamplePickInst samplePick:
                {
                    RequireStream(CheckExpr(frame, samplePick.Stream, where), where);
                    var element = CheckQuery(frame, samplePick.Candidates, where);
                    Bind(frame, samplePick.ResultSlot, element, where);
                    break;
                }

                case EmitEventInst emit:
                {
                    var eventType = RequireRef<EventResolution>(frame, emit.EventRef, where, "event");
                    CheckFieldInits(frame, emit.Payload, FieldOwnerKind.EventPayload, eventType.Event.Id, where,
                        $"event '{eventType.Event.QualifiedId}'");
                    break;
                }

                case RequestDecisionInst decision:
                {
                    Require(CheckExpr(frame, decision.Participant, where), Ty.Participant, where, "decision participant");
                    var element = decision.Candidates is null ? (Ty?)null : CheckQuery(frame, decision.Candidates, where);
                    if (decision.Minimum is not null)
                        Require(CheckExpr(frame, decision.Minimum, where), Ty.Int, where, "decision minimum");
                    if (decision.Maximum is not null)
                        Require(CheckExpr(frame, decision.Maximum, where), Ty.Int, where, "decision maximum");
                    if (decision.Minimum is not null && decision.Maximum is not null && decision.Kind == DecisionResponseKind.Confirm)
                        throw Faults.Schema($"{where}: a confirm decision takes no minimum or maximum");
                    var result = DecisionResultType(decision.Kind, element, where);
                    if (decision.Kind == DecisionResponseKind.Enum)
                    {
                        // The existing typed result slot supplies the enum identity; no second type
                        // declaration on the instruction or an unbounded Binding result is needed.
                        result = DeclaredSlot(frame, decision.ResultSlot, where);
                        if (result.Kind != FieldTypeKind.Enum || result.EnumId < 0)
                            throw Faults.TypeError($"{where}: an enum decision needs a declared Enum result slot");
                        if (_tables.Enums[result.EnumId].Members.Count == 0)
                            throw Faults.Schema($"{where}: an enum decision cannot use an empty enum");
                    }
                    Bind(frame, decision.ResultSlot, result, where);
                    break;
                }

                case ComputeInst compute:
                {
                    var asset = RequireRef<ComputeResolution>(frame, compute.ComputeRef, where, "compute asset");
                    CheckComputeCall(frame, asset.Asset, compute, where);
                    break;
                }

                case CallInst call:
                {
                    var procedure = RequireRef<ProcedureResolution>(frame, call.ProcedureRef, where, "procedure");
                    CheckCall(frame, procedure.Procedure, call, where);
                    break;
                }

                case IfInst conditional:
                {
                    Require(CheckExpr(frame, conditional.Condition, where), Ty.Bool, where, "if condition");
                    CheckBlock(frame, conditional.Then, where + ".then");
                    if (conditional.Else is not null) CheckBlock(frame, conditional.Else, where + ".else");
                    break;
                }

                case RepeatInst repeat:
                {
                    Require(CheckExpr(frame, repeat.Count, where), Ty.Int, where, "repeat count");
                    CheckBlock(frame, repeat.Body, where + ".body");
                    break;
                }

                case ForEachInst forEach:
                {
                    var element = CheckQuery(frame, forEach.Query, where);
                    using (frame.Bind(forEach.ItemSlot, element))
                        CheckBlock(frame, forEach.Body, where + ".loop");
                    break;
                }

                case ReturnInst returnInst:
                {
                    var result = frame.Procedure.ResultType;
                    if (result is null)
                    {
                        if (returnInst.Value is not null)
                        {
                            throw Faults.TypeError(
                                $"{where}: '{frame.Procedure.QualifiedId}' declares no result type but returns a value");
                        }

                        break;
                    }

                    if (returnInst.Value is null)
                    {
                        throw Faults.TypeError(
                            $"{where}: '{frame.Procedure.QualifiedId}' must return {Describe(TyOf(result))}");
                    }

                    Require(CheckExpr(frame, returnInst.Value, where), TyOf(result), where, "return value");
                    break;
                }

                case SetTerminalInst terminal:
                {
                    var result = RequireRef<TerminalResolution>(frame, terminal.TerminalRef, where, "terminal result");
                    CheckFieldInits(frame, terminal.Fields, FieldOwnerKind.TerminalField, result.Terminal.Id, where,
                        $"terminal result '{result.Terminal.QualifiedId}'");
                    break;
                }

                case FailInst:
                    break;

                default:
                    throw Faults.Schema($"{where}: unsupported instruction '{inst.Primitive}'");
            }
        }

        private static void RequirePure(Frame frame, Inst inst, string where)
        {
            var signature = inst.Signature;
            foreach (var effect in SideEffects)
            {
                if (signature.Has(effect))
                {
                    throw Faults.Purity(
                        $"{where}: pure procedure '{frame.Procedure.QualifiedId}' uses '{inst.Primitive}' " +
                        $"which reaches the side effect {effect}");
                }
            }
        }

        private void CheckCall(Frame frame, ProcedureDefinition target, CallInst call, string where)
        {
            if (frame.IsPure && !target.IsPure)
            {
                throw Faults.Purity(
                    $"{where}: pure procedure '{frame.Procedure.QualifiedId}' calls impure procedure " +
                    $"'{target.QualifiedId}'");
            }

            RequireArity(call.Arguments.Count, target.Parameters.Count, where, target.QualifiedId);
            for (var i = 0; i < call.Arguments.Count; i++)
            {
                Require(CheckExpr(frame, call.Arguments[i], where), TyOf(target.Parameters[i]), where,
                    $"argument {i} of '{target.QualifiedId}'");
            }

            if (target.ResultType is null)
            {
                if (call.ResultSlot >= 0)
                {
                    throw Faults.TypeError(
                        $"{where}: procedure '{target.QualifiedId}' returns nothing but a result slot was requested");
                }

                return;
            }

            if (call.ResultSlot < 0)
                throw Faults.TypeError($"{where}: procedure '{target.QualifiedId}' returns a value that is discarded");

            Bind(frame, call.ResultSlot, TyOf(target.ResultType), where);
        }

        private void CheckComputeCall(Frame frame, ComputeAssetDefinition asset, ComputeInst compute, string where)
        {
            var provided = new HashSet<int>();
            foreach (var input in compute.Inputs)
            {
                var index = IndexOfField(asset.Inputs, input.Name, where, "input of " + asset.QualifiedId);
                if (!provided.Add(index))
                    throw Faults.Of(EngineFaultCode.DuplicateDeclaration,
                        $"{where}: compute input '{input.Name}' is provided twice");
                Require(CheckExpr(frame, input.Value, where), TyOf(asset.Inputs[index]), where,
                    $"compute input '{input.Name}'");
            }

            var bound = new HashSet<int>();
            foreach (var output in compute.Outputs)
            {
                var index = IndexOfField(asset.Outputs, output.Name, where, "output of " + asset.QualifiedId);
                if (!bound.Add(index))
                    throw Faults.Of(EngineFaultCode.DuplicateDeclaration,
                        $"{where}: compute output '{output.Name}' is bound twice");
                Bind(frame, output.Slot, TyOf(asset.Outputs[index]), where);
            }

            foreach (var output in asset.Outputs)
            {
                if (!bound.Contains(output.Index))
                    throw Faults.Schema($"{where}: compute output '{output.Name}' is not bound to a frame slot");
            }
        }

        private int IndexOfField(IReadOnlyList<FieldDefinition> fields, string name, string where, string what)
        {
            for (var i = 0; i < fields.Count; i++)
            {
                if (string.Equals(fields[i].Name, name, StringComparison.Ordinal)) return i;
            }

            throw Faults.Unresolved($"{where}: unknown {what} '{name}'");
        }

        private static void RequireArity(int actual, int expected, string where, string owner)
        {
            if (actual != expected)
                throw Faults.TypeError($"{where}: '{owner}' takes {expected} arguments but {actual} were supplied");
        }

        private void CheckInsertPosition(Frame frame, InsertPosition position, Expr? index, string where)
        {
            if (position == InsertPosition.AtIndex)
            {
                if (index is null)
                    throw Faults.Schema($"{where}: insert position {position} requires an index expression");
                Require(CheckExpr(frame, index, where), Ty.Int, where, "insert index");
                return;
            }

            if (index is not null)
                throw Faults.Schema($"{where}: insert position {position} does not take an index expression");
        }

        private void CheckFieldInits(Frame frame, IReadOnlyList<FieldInit> inits, FieldOwnerKind ownerKind,
            int ownerId, string where, string owner)
        {
            var seen = new HashSet<int>();
            foreach (var init in inits)
            {
                var field = RequireRef<FieldResolution>(frame, init.FieldRef, where, "field");
                if (field.OwnerKind != ownerKind || field.OwnerId != ownerId)
                {
                    throw Faults.TypeError(
                        $"{where}: field '{field.Field.Name}' does not belong to {owner} " +
                        $"(it is a {field.OwnerKind} field)");
                }

                if (!seen.Add(field.Index))
                    throw Faults.Of(EngineFaultCode.DuplicateDeclaration,
                        $"{where}: field '{field.Field.Name}' of {owner} is initialised twice");
                Require(CheckExpr(frame, init.Value, where), TyOf(field.Field), where, $"field '{field.Field.Name}'");
            }
        }

        // -- expressions ----------------------------------------------------------------------------------------

        private Ty CheckExpr(Frame frame, Expr expr, string where)
            => WithSource(expr, () => CheckExprCore(frame, expr, where));

        private Ty CheckExprCore(Frame frame, Expr expr, string where)
        {
            _ = expr.Signature;
            switch (expr)
            {
                case ConstExpr constant:
                    return TyOfValue(constant.Value);

                case FrameScopeExpr:
                    return Ty.Scope;

                case EntityScopeExpr entityScope:
                {
                    var entity = CheckExpr(frame, entityScope.Entity, where);
                    RequireEntityReference(entity, where, "scope lookup");
                    return Ty.Scope;
                }

                case LocalExpr local:
                    return SlotOf(frame, local.Slot, where);

                case ParamExpr parameter:
                    return SlotOf(frame, parameter.Slot, where);

                case FieldExpr field:
                {
                    var resolution = RequireRef<FieldResolution>(frame, field.FieldRef, where, "field");
                    RequireEntityField(resolution, where);
                    RequireEntityTarget(CheckExpr(frame, field.Target, where), resolution.OwnerId, where,
                        $"field '{resolution.Field.Name}'");
                    return TyOf(resolution.Field);
                }

                case ArithExpr arith:
                {
                    var left = CheckExpr(frame, arith.Left, where);
                    var right = CheckExpr(frame, arith.Right, where);
                    if (left.Kind == FieldTypeKind.Any || right.Kind == FieldTypeKind.Any)
                        throw Faults.TypeError($"{where}: arithmetic on an untyped binding slot");
                    if (left.Kind != right.Kind)
                    {
                        throw Faults.TypeError(
                            $"{where}: {arith.Op} mixes {Describe(left)} and {Describe(right)}");
                    }

                    if (left.Kind is not (FieldTypeKind.Int or FieldTypeKind.Fixed))
                    {
                        throw Faults.TypeError(
                            $"{where}: {arith.Op} needs numeric operands but found {Describe(left)}");
                    }

                    return left.Kind == FieldTypeKind.Int ? Ty.Int : Ty.Fixed;
                }

                case CompareExpr compare:
                {
                    var left = CheckExpr(frame, compare.Left, where);
                    var right = CheckExpr(frame, compare.Right, where);
                    if (left.Kind != FieldTypeKind.Any && right.Kind != FieldTypeKind.Any && !Comparable(left, right))
                    {
                        throw Faults.TypeError(
                            $"{where}: {compare.Op} compares {Describe(left)} with {Describe(right)}");
                    }

                    return Ty.Bool;
                }

                case LogicExpr logic:
                {
                    Require(CheckExpr(frame, logic.Left, where), Ty.Bool, where, logic.IsAnd ? "and" : "or");
                    Require(CheckExpr(frame, logic.Right, where), Ty.Bool, where, logic.IsAnd ? "and" : "or");
                    return Ty.Bool;
                }

                case NotExpr not:
                    Require(CheckExpr(frame, not.Operand, where), Ty.Bool, where, "not");
                    return Ty.Bool;

                case ConvertExpr convert:
                {
                    var operand = CheckExpr(frame, convert.Operand, where);
                    if (convert.Op == ConvertOp.IntToFixed)
                    {
                        Require(operand, Ty.Int, where, "int to fixed conversion");
                        return Ty.Fixed;
                    }

                    Require(operand, Ty.Fixed, where, "fixed to int conversion");
                    return Ty.Int;
                }

                case EnumConstExpr enumConstant:
                {
                    var enumType = RequireRef<EnumTypeResolution>(frame, enumConstant.EnumRef, where, "enum type");
                    _ = enumType.Enum.OrdinalOf(enumConstant.Member);
                    return new Ty(new FieldTypeSchema(FieldTypeKind.Enum), false, enumType.Enum.Id, -1, -1, -1);
                }

                case CountExpr count:
                    _ = CheckQuery(frame, count.Source, where);
                    return Ty.Int;

                case CollectionIndexExpr collectionIndex:
                {
                    _ = RequireRef<CollectionResolution>(frame, collectionIndex.CollectionRef, where, "collection");
                    RequireEntityReference(CheckExpr(frame, collectionIndex.Item, where), where, "collection index");
                    return Ty.Int;
                }

                case ExistsExpr exists:
                    _ = CheckQuery(frame, exists.Source, where);
                    return Ty.Bool;

                case FirstExpr first:
                    return CheckQuery(frame, first.Source, where);

                case IsNoneExpr isNone:
                {
                    var operand = CheckExpr(frame, isNone.Operand, where);
                    if (operand.Kind is not (FieldTypeKind.Ref or FieldTypeKind.Any))
                    {
                        throw Faults.TypeError(
                            $"{where}: isNone needs a reference but found {Describe(operand)}");
                    }

                    return Ty.Bool;
                }

                case CallPureExpr call:
                {
                    var procedure = RequireRef<ProcedureResolution>(frame, call.ProcedureRef, where, "procedure");
                    if (!procedure.Procedure.IsPure)
                    {
                        throw Faults.Purity(
                            $"{where}: expression calls '{procedure.Procedure.QualifiedId}' which is not pure");
                    }

                    RequireArity(call.Arguments.Count, procedure.Procedure.Parameters.Count, where,
                        procedure.Procedure.QualifiedId);
                    for (var i = 0; i < call.Arguments.Count; i++)
                    {
                        Require(CheckExpr(frame, call.Arguments[i], where), TyOf(procedure.Procedure.Parameters[i]),
                            where, $"argument {i} of '{procedure.Procedure.QualifiedId}'");
                    }

                    return procedure.Procedure.ResultType is null ? Ty.Void : TyOf(procedure.Procedure.ResultType);
                }

                case AggregateExpr aggregate:
                {
                    var element = CheckQuery(frame, aggregate.Source, where);
                    if (aggregate.Op == AggregateOp.Count)
                    {
                        if (aggregate.ItemValue is not null)
                            throw Faults.Schema($"{where}: a count aggregate takes no item value");
                        return Ty.Int;
                    }

                    if (aggregate.ItemValue is null)
                        throw Faults.Schema($"{where}: aggregate {aggregate.Op} needs an item value expression");
                    using (frame.Bind(aggregate.ItemSlot, element))
                    {
                        var value = CheckExpr(frame, aggregate.ItemValue, where);
                        if (value.Kind is not (FieldTypeKind.Int or FieldTypeKind.Fixed))
                        {
                            throw Faults.TypeError(
                                $"{where}: aggregate {aggregate.Op} needs a numeric item value but found " +
                                Describe(value));
                        }

                        return value.Kind == FieldTypeKind.Int ? Ty.Int : Ty.Fixed;
                    }
                }

                case StringConcatExpr concat:
                {
                    foreach (var part in concat.Parts)
                    {
                        var value = CheckExpr(frame, part, where);
                        if (value.Kind is not (FieldTypeKind.String or FieldTypeKind.Int or FieldTypeKind.Any))
                        {
                            throw Faults.TypeError(
                                $"{where}: string concatenation needs string or int parts but found {Describe(value)}");
                        }
                    }

                    return Ty.String;
                }

                default:
                    throw Faults.Schema($"{where}: unsupported expression '{expr.Primitive}'");
            }
        }

        private bool Comparable(Ty left, Ty right)
        {
            if (left.Kind != right.Kind) return false;
            return left.Kind switch
            {
                FieldTypeKind.Enum => left.EnumId < 0 || right.EnumId < 0 || left.EnumId == right.EnumId,
                FieldTypeKind.Ref => left.TargetId < 0 || right.TargetId < 0 || left.TargetId == right.TargetId,
                FieldTypeKind.Bool or FieldTypeKind.Int or FieldTypeKind.Fixed or FieldTypeKind.String
                    or FieldTypeKind.Participant => true,
                FieldTypeKind.List => false,
                _ => false,
            };
        }

        private Ty CheckQuery(Frame frame, Query query, string where)
            => WithSource(query, () => CheckQueryCore(frame, query, where));

        private Ty CheckQueryCore(Frame frame, Query query, string where)
        {
            switch (query)
            {
                case TypeQuery type:
                {
                    var resolution = RequireRef<TypeResolution>(frame, type.TypeRef, where, "entity type");
                    if (type.ScopeTypeRef >= 0)
                        _ = RequireRef<ScopeResolution>(frame, type.ScopeTypeRef, where, "scope type");
                    return Ty.RefTo(resolution.Type.Id);
                }

                case RefListQuery refList:
                {
                    // 槽位必须真的装着一个引用列表（ListOf(Ref)），元素类型就是引用本身。
                    // 槽位元素类型（TargetId）取自槽位自身的类型：决策结果在绑定时已带上它。
                    var slotType = SlotOf(frame, refList.ListSlot, where);
                    if (slotType.Shape?.Kind != FieldTypeKind.List
                        || slotType.Shape.Element?.Kind != FieldTypeKind.Ref)
                    {
                        throw Faults.Schema(
                            $"{where}: '{refList.Primitive}' needs a list-of-ref slot but slot " +
                            $"{refList.ListSlot} holds '{slotType.Shape?.Kind.ToString() ?? "untyped"}'");
                    }

                    return Ty.RefTo(slotType.ElementTargetId);
                }

                case CollectionQuery collection:
                {
                    var resolution = RequireRef<CollectionResolution>(frame, collection.CollectionRef, where, "collection");
                    if (collection.ScopeTypeRef >= 0)
                        _ = RequireRef<ScopeResolution>(frame, collection.ScopeTypeRef, where, "scope type");
                    return Ty.RefTo(resolution.Collection.ElementTypeId);
                }

                case RelationQuery relation:
                {
                    var resolution = RequireRef<RelationResolution>(frame, relation.RelationRef, where, "relation");
                    RequireEntityReference(CheckExpr(frame, relation.Owner, where), where, "relation owner");
                    return Ty.RefTo(resolution.Relation.TargetTypeId);
                }

                case FilterQuery filter:
                {
                    var element = CheckQuery(frame, filter.Source, where);
                    using (frame.Bind(filter.ItemSlot, element))
                        Require(CheckExpr(frame, filter.Predicate, where), Ty.Bool, where, "query predicate");
                    return element;
                }

                case SortQuery sort:
                {
                    var element = CheckQuery(frame, sort.Source, where);
                    using (frame.Bind(sort.ItemSlot, element))
                    {
                        var key = CheckExpr(frame, sort.Key, where);
                        if (key.Kind is FieldTypeKind.List or FieldTypeKind.Any)
                        {
                            throw Faults.TypeError(
                                $"{where}: sort key must be a scalar but found {Describe(key)}");
                        }
                    }

                    return element;
                }

                case TakeQuery take:
                    _ = CheckQuery(frame, take.Source, where);
                    Require(CheckExpr(frame, take.Count, where), Ty.Int, where, "take count");
                    return CheckQuery(frame, take.Source, where);

                case SkipQuery skip:
                    _ = CheckQuery(frame, skip.Source, where);
                    Require(CheckExpr(frame, skip.Count, where), Ty.Int, where, "skip count");
                    return CheckQuery(frame, skip.Source, where);

                default:
                    throw Faults.Schema($"{where}: unsupported query '{query.Primitive}'");
            }
        }

        private Ty DecisionResultType(DecisionResponseKind kind, Ty? element, string where) => kind switch
        {
            DecisionResponseKind.EntityRef => element ?? Ty.Of(new FieldTypeSchema(FieldTypeKind.Ref)),
            DecisionResponseKind.EntityRefList => new Ty(
                FieldTypeSchema.ListOf(new FieldTypeSchema(FieldTypeKind.Ref)), false, -1, -1, -1,
                element?.TargetId ?? -1),
            DecisionResponseKind.Int => Ty.Int,
            DecisionResponseKind.Bool or DecisionResponseKind.Confirm => Ty.Bool,
            DecisionResponseKind.Enum => Ty.Of(new FieldTypeSchema(FieldTypeKind.Enum)),
            DecisionResponseKind.Participant => Ty.Participant,
            _ => throw Faults.Schema($"{where}: unsupported decision response kind '{kind}'"),
        };

        // -- slot and reference helpers -------------------------------------------------------------------------

        private void Bind(Frame frame, int slot, Ty value, string where)
        {
            var declared = DeclaredSlot(frame, slot, where);
            if (declared.Kind == FieldTypeKind.Any)
            {
                frame.Bindings[slot] = value;
                return;
            }

            Require(value, declared, where, $"frame slot {slot}");
        }

        private Ty SlotOf(Frame frame, int slot, string where)
        {
            if (frame.Bindings.TryGetValue(slot, out var binding)) return binding;
            return DeclaredSlot(frame, slot, where);
        }

        private Ty SlotOf(Frame frame, FieldDefinition parameter, string where)
        {
            _ = where;
            return TyOf(parameter);
        }

        private Ty DeclaredSlot(Frame frame, int slot, string where)
        {
            if (slot < 0) throw Faults.TypeError($"{where}: frame slot {slot} is negative");
            var parameters = frame.Procedure.Parameters;
            if (slot < parameters.Count) return TyOf(parameters[slot]);
            var index = slot - parameters.Count;
            if (index < frame.Procedure.Locals.Count) return TyOf(frame.Procedure.Locals[index]);
            throw Faults.TypeError(
                $"{where}: frame slot {slot} is out of range for '{frame.Procedure.QualifiedId}' " +
                $"({parameters.Count} parameters, {frame.Procedure.Locals.Count} locals)");
        }

        private T RequireRef<T>(Frame frame, int slot, string where, string what) where T : Resolution
        {
            var packageIndex = frame.Procedure.PackageIndex;
            var package = _packages[packageIndex];
            var refBase = _tables.RefBases[package.PackageId];
            if (slot < 0 || slot >= package.RefTargets.Count)
            {
                throw Faults.Unresolved(
                    $"{where}: reference slot {slot} is outside package '{package.PackageId}' " +
                    $"(0..{package.RefTargets.Count - 1})");
            }

            var resolution = _resolutions[refBase + slot];
            if (resolution is null)
                throw Faults.Unresolved($"{where}: reference slot {slot} of '{package.PackageId}' is unresolved");
            if (resolution is not T typed)
            {
                throw Faults.TypeError(
                    $"{where}: expected a {what} reference but slot {slot} resolves to " +
                    $"'{resolution.GetType().Name}'");
            }

            return typed;
        }

        private static void RequireEntityField(FieldResolution field, string where)
        {
            if (field.OwnerKind != FieldOwnerKind.Entity)
            {
                throw Faults.TypeError(
                    $"{where}: field '{field.Field.Name}' is a {field.OwnerKind} field; only entity fields are " +
                    "addressable as 'target.field'");
            }
        }

        private void RequireEntityTarget(Ty target, int typeId, string where, string what)
        {
            RequireEntityReference(target, where, what);
            if (target.TargetId >= 0 && target.TargetId != typeId)
            {
                throw Faults.TypeError(
                    $"{where}: {what} belongs to '{_tables.Types[typeId].QualifiedId}' but the target is " +
                    $"'{_tables.Types[target.TargetId].QualifiedId}'");
            }
        }

        private void RequireEntityReference(Ty target, string where, string what)
        {
            if (target.Kind is FieldTypeKind.Ref or FieldTypeKind.Any) return;
            throw Faults.TypeError($"{where}: {what} needs an entity reference but found {Describe(target)}");
        }

        private void RequireCollectionElement(CollectionDefinition collection, Ty item, string where)
        {
            if (item.Kind == FieldTypeKind.Any) return;
            if (item.Kind != FieldTypeKind.Ref)
            {
                throw Faults.TypeError(
                    $"{where}: collection '{collection.QualifiedId}' holds entity references but found {Describe(item)}");
            }

            if (item.TargetId >= 0 && item.TargetId != collection.ElementTypeId)
            {
                throw Faults.TypeError(
                    $"{where}: collection '{collection.QualifiedId}' holds " +
                    $"'{_tables.Types[collection.ElementTypeId].QualifiedId}' but the value is " +
                    $"'{_tables.Types[item.TargetId].QualifiedId}'");
            }
        }

        private void RequireScope(Ty scope, string where, string what)
        {
            if (scope.IsScope) return;
            throw Faults.TypeError($"{where}: {what} needs a scope but found {Describe(scope)}");
        }

        private void RequireStream(Ty stream, string where)
        {
            if (stream.Kind is FieldTypeKind.String or FieldTypeKind.Ref or FieldTypeKind.Any) return;
            throw Faults.TypeError($"{where}: a random stream must be named but found {Describe(stream)}");
        }

        private void Require(Ty actual, Ty expected, string where, string what)
        {
            if (Assignable(expected, actual)) return;
            throw Faults.TypeError($"{where}: {what} expects {Describe(expected)} but found {Describe(actual)}");
        }

        private bool Assignable(Ty target, Ty value)
        {
            if (target.IsScope) return value.IsScope;
            if (target.Shape is null) return false;
            if (target.Kind == FieldTypeKind.Any || value.Kind == FieldTypeKind.Any) return true;
            if (value.Shape is null) return false;
            if (target.Kind != value.Kind) return false;
            return target.Kind switch
            {
                FieldTypeKind.Enum => target.EnumId < 0 || value.EnumId < 0 || target.EnumId == value.EnumId,
                FieldTypeKind.Ref => target.TargetId < 0 || value.TargetId < 0 || target.TargetId == value.TargetId,
                FieldTypeKind.List => ListAssignable(target, value),
                _ => true,
            };
        }

        private static bool ListAssignable(Ty target, Ty value)
        {
            var targetElement = target.Shape?.Element;
            var valueElement = value.Shape?.Element;
            if (targetElement is null || valueElement is null) return true;
            if (targetElement.Kind != valueElement.Kind) return false;
            if (targetElement.Kind == FieldTypeKind.Ref)
                return target.ElementTargetId < 0 || value.ElementTargetId < 0 ||
                       target.ElementTargetId == value.ElementTargetId;
            if (targetElement.Kind == FieldTypeKind.Enum)
                return target.ElementEnumId < 0 || value.ElementEnumId < 0 ||
                       target.ElementEnumId == value.ElementEnumId;
            return true;
        }

        // -- types ----------------------------------------------------------------------------------------------

        private Ty TyOf(FieldDefinition field) =>
            new(field.Shape, false, field.EnumTypeId, field.TargetTypeId, field.ElementEnumTypeId,
                field.ElementTargetTypeId);

        private Ty TyOf(FieldTypeSchema shape)
        {
            var enumId = shape.Enum is null ? -1 : EnumId(shape.Enum);
            var targetId = shape.Target is null ? -1 : TypeId(shape.Target);
            var elementEnumId = shape.Element?.Enum is null ? -1 : EnumId(shape.Element.Enum);
            var elementTargetId = shape.Element?.Target is null ? -1 : TypeId(shape.Element.Target);
            return new Ty(shape, false, enumId, targetId, elementEnumId, elementTargetId);
        }

        private int EnumId(EnumTypeSymbol symbol)
        {
            if (_tables.EnumIds.TryGetValue(symbol.QualifiedId, out var id)) return id;
            throw Faults.Unresolved($"enum '{symbol.QualifiedId}' is not part of the rule set");
        }

        private int TypeId(EntityTypeSymbol symbol)
        {
            if (_tables.TypeIds.TryGetValue(symbol.QualifiedId, out var id)) return id;
            throw Faults.Unresolved($"entity type '{symbol.QualifiedId}' is not part of the rule set");
        }

        private static Ty TyOfValue(Value value) => value.Kind switch
        {
            ValueKind.None => Ty.Untyped,
            ValueKind.Bool => Ty.Bool,
            ValueKind.Int => Ty.Int,
            ValueKind.Fixed => Ty.Fixed,
            ValueKind.Enum => Ty.Of(new FieldTypeSchema(FieldTypeKind.Enum)),
            ValueKind.String => Ty.String,
            ValueKind.Ref => Ty.Of(new FieldTypeSchema(FieldTypeKind.Ref)),
            ValueKind.Participant => Ty.Participant,
            ValueKind.Scope => Ty.Scope,
            ValueKind.RefList => new Ty(
                FieldTypeSchema.ListOf(new FieldTypeSchema(FieldTypeKind.Ref)), false, -1, -1, -1, -1),
            ValueKind.IntList => Ty.Of(FieldTypeSchema.IntList()),
            _ => Ty.Untyped,
        };

        private string Describe(Ty type)
        {
            if (type.IsScope) return "scope";
            if (type.Shape is null) return "no value";
            var name = type.Kind switch
            {
                FieldTypeKind.Bool => "bool",
                FieldTypeKind.Int => "int",
                FieldTypeKind.Fixed => "fixed",
                FieldTypeKind.Enum => "enum",
                FieldTypeKind.String => "string",
                FieldTypeKind.Ref => "ref",
                FieldTypeKind.Participant => "participant",
                FieldTypeKind.List => "list",
                FieldTypeKind.Any => "binding",
                _ => type.Kind.ToString(),
            };

            if (type.Kind == FieldTypeKind.Ref && type.TargetId >= 0)
                name += "<" + _tables.Types[type.TargetId].QualifiedId + ">";
            else if (type.Kind == FieldTypeKind.Enum && type.EnumId >= 0)
                name += "<" + _tables.Enums[type.EnumId].QualifiedId + ">";
            else if (type.Kind == FieldTypeKind.List && type.Shape.Element is { } element)
                name += "<" + element.Kind.ToString().ToLowerInvariant() + ">";

            return name;
        }
    }

    /// <summary>Execution frame being checked: one linked procedure plus the currently visible binding slots.</summary>
    private sealed class Frame
    {
        public Frame(ProcedureDefinition procedure)
        {
            Procedure = procedure;
        }

        public ProcedureDefinition Procedure { get; }

        /// <summary>Slot refinements for binding slots (query items, decision results, compute outputs).</summary>
        public Dictionary<int, Ty> Bindings { get; } = [];

        public bool IsPure => Procedure.IsPure;

        /// <summary>Binds a slot for the duration of a <c>using</c> scope and restores the previous binding.</summary>
        public Scope Bind(int slot, Ty type) => new(this, slot, type);

        public readonly struct Scope : IDisposable
        {
            private readonly Frame _frame;
            private readonly int _slot;
            private readonly Ty? _previous;

            public Scope(Frame frame, int slot, Ty type)
            {
                _frame = frame;
                _slot = slot;
                _previous = frame.Bindings.TryGetValue(slot, out var previous) ? previous : null;
                frame.Bindings[slot] = type;
            }

            public void Dispose()
            {
                if (_previous.HasValue) _frame.Bindings[_slot] = _previous.Value;
                else _frame.Bindings.Remove(_slot);
            }
        }
    }

    /// <summary>Static type of an IR expression. <see cref="Shape"/> is null for scopes and valueless expressions.</summary>
    private readonly record struct Ty(
        FieldTypeSchema? Shape,
        bool IsScope,
        int EnumId,
        int TargetId,
        int ElementEnumId,
        int ElementTargetId)
    {
        public static readonly Ty Scope = new(null, true, -1, -1, -1, -1);

        public static readonly Ty Void = new(null, false, -1, -1, -1, -1);

        /// <summary>An untyped binding slot or <c>none</c>: assignable to anything, comparable with anything.</summary>
        public static readonly Ty Untyped = new(new FieldTypeSchema(FieldTypeKind.Any), false, -1, -1, -1, -1);

        public static readonly Ty Bool = Of(FieldTypeSchema.Bool());

        public static readonly Ty Int = Of(FieldTypeSchema.Int());

        public static readonly Ty Fixed = Of(FieldTypeSchema.Fixed());

        public static readonly Ty String = Of(FieldTypeSchema.String());

        public static readonly Ty Participant = Of(FieldTypeSchema.Participant());

        public static Ty Of(FieldTypeSchema shape) => new(shape, false, -1, -1, -1, -1);

        public static Ty RefTo(int typeId) =>
            new(new FieldTypeSchema(FieldTypeKind.Ref), false, -1, typeId, -1, -1);

        public FieldTypeKind Kind => Shape?.Kind ?? FieldTypeKind.Any;
    }
}
