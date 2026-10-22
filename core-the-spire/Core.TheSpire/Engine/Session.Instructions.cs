namespace CoreTheSpire.Engine;

public sealed partial class Session
{
    private Outcome Execute(CallActivation call, Inst instruction)
    {
        var signature = instruction.Signature;
        var consumedRandom = signature.Has(EngineEffect.ConsumesRandom);
        RandomSnapshot? snapshot = consumedRandom ? Random.Capture() : null;
        if (consumedRandom) Debug?.BeginRandom(this, instruction.SourceLocation);
        try
        {
            var outcome = State.RunAtomic<Outcome>(() => ExecuteCore(call, instruction));
            return outcome;
        }
        catch (EngineFaultException)
        {
            if (snapshot is not null) Random.Restore(snapshot);
            throw;
        }
        finally
        {
            if (consumedRandom) Debug?.EndRandom();
        }
    }

    private Outcome ExecuteCore(CallActivation call, Inst instruction)
    {
        switch (instruction)
        {
            case SetLocalInst setLocal:
                call.Slots[setLocal.Slot] = Eval(call, setLocal.Value);
                return Outcome.Continue;

            case SetFieldInst setField:
            {
                var field = ResolveEntityField(call, setField.FieldRef);
                State.SetField(RequireReference(Eval(call, setField.Target), "field target"), field.Index,
                    Eval(call, setField.Value));
                return Outcome.Continue;
            }

            case CreateEntityInst create:
            {
                var type = Resolve<TypeResolution>(call, create.TypeRef).Type;
                var scope = RequireScope(Eval(call, create.Scope));
                var instance = State.CreateInstance(type.Id, scope);
                foreach (var init in create.Fields)
                {
                    var field = ResolveFieldRef(call, init.FieldRef);
                    State.SetField(instance, field.Index, Eval(call, init.Value));
                }

                if (create.AddToCollectionRef >= 0)
                {
                    State.AddToCollection(Resolve<CollectionResolution>(call, create.AddToCollectionRef).Collection.Id,
                        instance, create.Position, EvalIndex(call, create.PositionIndex));
                }

                call.Slots[create.ResultSlot] = Value.OfRef(instance);
                return Outcome.Continue;
            }

            case CreateScopeInst createScope:
            {
                var scopeType = Resolve<ScopeResolution>(call, createScope.ScopeTypeRef).ScopeType;
                var parent = createScope.Parent is null ? call.Scope : RequireScope(Eval(call, createScope.Parent));
                var scope = State.CreateScope(scopeType.Id, parent);
                call.Slots[createScope.ResultSlot] = Value.OfScope(scope);
                return Outcome.Continue;
            }

            case DestroyEntityInst destroy:
            {
                var target = Eval(call, destroy.Target);
                if (destroy.ScopeSubtree) State.DestroyScope(RequireScope(target));
                else State.DestroyInstance(RequireReference(target, "destruction target"));
                return Outcome.Continue;
            }

            case AddToCollectionInst add:
            {
                var collection = Resolve<CollectionResolution>(call, add.CollectionRef).Collection;
                State.AddToCollection(collection.Id, RequireReference(Eval(call, add.Item), "collection item"),
                    add.Position, EvalIndex(call, add.PositionIndex));
                return Outcome.Continue;
            }

            case RemoveFromCollectionInst remove:
            {
                var collection = Resolve<CollectionResolution>(call, remove.CollectionRef).Collection;
                State.RemoveFromCollection(collection.Id,
                    RequireReference(Eval(call, remove.Item), "collection item"));
                return Outcome.Continue;
            }

            case MoveInCollectionInst move:
            {
                var collection = Resolve<CollectionResolution>(call, move.CollectionRef).Collection;
                State.MoveInCollection(collection.Id, RequireReference(Eval(call, move.Item), "collection item"),
                    move.Position, EvalIndex(call, move.PositionIndex));
                return Outcome.Continue;
            }

            case ShuffleCollectionInst shuffle:
            {
                var collection = Resolve<CollectionResolution>(call, shuffle.CollectionRef).Collection;
                State.ShuffleCollection(collection.Id, Random, RequireStream(Eval(call, shuffle.Stream)));
                return Outcome.Continue;
            }

            case SetRelationInst setRelation:
            {
                var relation = Resolve<RelationResolution>(call, setRelation.RelationRef).Relation;
                var owner = RequireReference(Eval(call, setRelation.Owner), "relation owner");
                var target = RequireReference(Eval(call, setRelation.Target), "relation target");
                if (setRelation.Add) State.AddRelation(relation.Id, owner, target);
                else State.RemoveRelation(relation.Id, owner, target);
                return Outcome.Continue;
            }

            case ClearRelationInst clearRelation:
            {
                var relation = Resolve<RelationResolution>(call, clearRelation.RelationRef).Relation;
                State.ClearRelation(relation.Id, RequireReference(Eval(call, clearRelation.Owner), "relation owner"));
                return Outcome.Continue;
            }

            case SeedStreamInst seedStream:
            {
                var seed = Eval(call, seedStream.Seed).AsInt();
                if (!seedStream.Word64 && seed < 0) throw Faults.Range("stream seed must be nonnegative");
                Random.SeedStream(RequireStream(Eval(call, seedStream.Stream)), unchecked((ulong)seed));
                return Outcome.Continue;
            }
            case SampleFloat32Inst sampleFloat:
            {
                var min = sampleFloat.Minimum is null ? 0f : Binary32.Read(Eval(call, sampleFloat.Minimum).AsInt());
                var max = sampleFloat.Maximum is null ? 1f : Binary32.Read(Eval(call, sampleFloat.Maximum).AsInt());
                if (min > max) throw Faults.Range("binary32 sampling minimum exceeds maximum");
                var width = max - min;
                if (!float.IsFinite(width)) throw Faults.Range("binary32 sampling width must be finite");
                // Source NextFloat scales the double first, then rounds once; equal bounds still consume.
                var raw = Random.DrawDouble(RequireStream(Eval(call, sampleFloat.Stream)));
                call.Slots[sampleFloat.ResultSlot] = Value.Of(Binary32.Bits((float)(raw * (double)width + (double)min)));
                return Outcome.Continue;
            }

            case SampleIntInst sampleInt:
            {
                var stream = RequireStream(Eval(call, sampleInt.Stream));
                var min = Eval(call, sampleInt.Min).AsInt();
                var max = Eval(call, sampleInt.Max).AsInt();
                if (min >= max)
                    throw Faults.Range($"sampling range [{min}, {max}) is empty");
                call.Slots[sampleInt.ResultSlot] = Value.Of(Random.DrawInt(stream, min, max));
                return Outcome.Continue;
            }

            case SamplePickInst samplePick:
            {
                var stream = RequireStream(Eval(call, samplePick.Stream));
                var candidates = EvaluateQuery(call, samplePick.Candidates);
                if (candidates.Count == 0) throw Faults.Range("cannot sample from an empty candidate query");
                // The package owns query order; do not substitute instance identity or display order.
                call.Slots[samplePick.ResultSlot] = Value.OfRef(Random.DrawPick(stream, candidates));
                return Outcome.Continue;
            }

            case EmitEventInst emit:
            {
                var eventType = Resolve<EventResolution>(call, emit.EventRef).Event;
                var payload = new Value[eventType.Payload.Count];
                for (var index = 0; index < payload.Length; index++) payload[index] = eventType.Payload[index].DefaultValue;
                foreach (var init in emit.Payload)
                {
                    var field = ResolveFieldRef(call, init.FieldRef);
                    payload[field.Index] = Eval(call, init.Value);
                }

                Emit(eventType, payload, call.Scope);
                return Outcome.Continue;
            }

            case RequestDecisionInst decision:
            {
                var participant = Eval(call, decision.Participant).AsParticipant();
                var candidates = decision.Candidates is null
                    ? (IReadOnlyList<InstanceId>)[]
                    : EvaluateQuery(call, decision.Candidates);
                var minimum = decision.Minimum is null ? 0L : Eval(call, decision.Minimum).AsInt();
                var maximum = decision.Maximum is null
                    ? Math.Max(minimum, candidates.Count)
                    : Eval(call, decision.Maximum).AsInt();
                if (decision.Kind == DecisionResponseKind.Enum)
                {
                    var parameters = call.Procedure.Parameters;
                    var field = decision.ResultSlot < parameters.Count
                        ? parameters[decision.ResultSlot]
                        : call.Procedure.Locals[decision.ResultSlot - parameters.Count];
                    var lastMember = RuleSet.Enums[field.EnumTypeId].Members.Count - 1;
                    if (decision.Maximum is null) maximum = lastMember;
                    if (minimum < 0 || maximum > lastMember)
                        throw Faults.Range($"enum decision range [{minimum}, {maximum}] is outside [0, {lastMember}]");
                }
                if (maximum < minimum)
                    throw Faults.Range($"decision range [{minimum}, {maximum}] is inverted");

                // A decision request is the first stable boundary a host can act on: everything the state holds must
                // already be constructed, or the request would publish a half-built instance.
                State.RequireStableState($"decision request '{decision.Prompt}'");

                var request = new DecisionRequest(_nextRequestId++, State.Version, participant, decision.Kind,
                    decision.Prompt, candidates, minimum, maximum);
                _pending = new Pending(request, call, decision.ResultSlot);
                Status = SessionStatus.AwaitingDecision;
                Trace(SessionDebugKind.DecisionRequested, request.RequestId.ToString(),
                    $"participant={participant}; kind={decision.Kind}; candidates=[{string.Join(',', candidates)}]",
                    instruction.SourceLocation);
                return Outcome.Suspended;
            }

            case ComputeInst compute:
            {
                var asset = Resolve<ComputeResolution>(call, compute.ComputeRef).Asset;
                var backend = ComputeBackend
                    ?? throw Faults.Of(EngineFaultCode.Unsupported,
                        $"compute asset '{asset.QualifiedId}' needs a compute backend but none is installed");
                if (!string.Equals(backend.Id, asset.BackendId, StringComparison.Ordinal))
                {
                    throw Faults.Of(EngineFaultCode.Unsupported,
                        $"compute asset '{asset.QualifiedId}' declares backend '{asset.BackendId}' but " +
                        $"'{backend.Id}' is installed");
                }

                var inputs = new Value[asset.Inputs.Count];
                for (var index = 0; index < inputs.Length; index++) inputs[index] = asset.Inputs[index].DefaultValue;
                foreach (var input in compute.Inputs)
                    inputs[IndexOf(asset.Inputs, input.Name)] = Eval(call, input.Value);

                var outputs = new Value[asset.Outputs.Count];
                backend.Execute(asset, inputs, outputs);
                foreach (var output in compute.Outputs)
                    call.Slots[output.Slot] = outputs[IndexOf(asset.Outputs, output.Name)];
                return Outcome.Continue;
            }

            case CallInst invoke:
            {
                var procedure = Resolve<ProcedureResolution>(call, invoke.ProcedureRef).Procedure;
                var arguments = new Value[invoke.Arguments.Count];
                for (var index = 0; index < arguments.Length; index++) arguments[index] = Eval(call, invoke.Arguments[index]);
                PushCall(procedure, arguments, call.Scope, invoke.ResultSlot, call);
                return Outcome.Continue;
            }

            case IfInst conditional:
            {
                var condition = Eval(call, conditional.Condition).AsBool();
                var branch = condition ? conditional.Then : conditional.Else;
                if (branch is not null) call.Cursors.Add(new BlockCursor(branch));
                return Outcome.Continue;
            }

            case RepeatInst repeat:
            {
                var count = Eval(call, repeat.Count).AsInt();
                if (count > 0) call.Cursors.Add(new BlockCursor(repeat.Body) { RepeatRemaining = count - 1 });
                return Outcome.Continue;
            }

            case ForEachInst forEach:
            {
                var items = EvaluateQuery(call, forEach.Query);
                if (items.Count == 0) return Outcome.Continue;
                call.Slots[forEach.ItemSlot] = Value.OfRef(items[0]);
                call.Cursors.Add(new BlockCursor(forEach.Body)
                {
                    Items = items,
                    ItemIndex = 0,
                    ItemSlot = forEach.ItemSlot,
                });
                return Outcome.Continue;
            }

            case ReturnInst returnInst:
            {
                call.ReturnValue = returnInst.Value is null ? Value.None : Eval(call, returnInst.Value);
                return Outcome.Completed;
            }

            case SetTerminalInst terminal:
            {
                var definition = Resolve<TerminalResolution>(call, terminal.TerminalRef).Terminal;
                var fields = new Value[definition.Fields.Count];
                for (var index = 0; index < fields.Length; index++) fields[index] = definition.Fields[index].DefaultValue;
                foreach (var init in terminal.Fields)
                {
                    var field = ResolveFieldRef(call, init.FieldRef);
                    fields[field.Index] = Eval(call, init.Value);
                }

                // The terminal result is the last stable boundary: it may not publish a half-built instance.
                State.RequireStableState($"terminal result '{definition.QualifiedId}'");

                Terminal = new TerminalOutcome(definition, fields);
                Status = SessionStatus.Terminal;
                Trace(SessionDebugKind.Terminal, definition.QualifiedId, string.Join(',', fields), instruction.SourceLocation);
                return Outcome.Completed;
            }

            case FailInst fail:
                throw Faults.Of(EngineFaultCode.SessionStateInvalid,
                    $"rule '{call.Procedure.QualifiedId}' failed: {fail.Message}");

            default:
                throw Faults.Of(EngineFaultCode.Unsupported,
                    $"instruction '{instruction.Primitive}' is not implemented by the scheduler");
        }
    }

}
