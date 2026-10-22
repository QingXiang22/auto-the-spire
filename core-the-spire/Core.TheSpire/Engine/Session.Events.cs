namespace CoreTheSpire.Engine;

public sealed partial class Session
{
    private void Emit(EventDefinition eventType, Value[] payload, ScopeId inheritedScope)
    {
        var depth = 0;
        foreach (var item in _stack)
        {
            if (item is DispatchActivation) depth++;
        }

        if (depth + 1 > Budget.MaxEventDepth)
        {
            throw Faults.Budget(
                $"event '{eventType.QualifiedId}' would exceed the configured event depth {Budget.MaxEventDepth}");
        }

        var listeners = SnapshotListeners(eventType, payload, inheritedScope);
        _stack.Add(new DispatchActivation(eventType, listeners, payload, inheritedScope));
        if (Debug is not null)
            Trace(SessionDebugKind.EventEmit, eventType.QualifiedId,
                $"scope={inheritedScope}; listeners={listeners.Length}; payload=[{string.Join(',', payload)}]", payload: payload);
    }

    private ListenerActivation[] SnapshotListeners(EventDefinition eventType, Value[] payload, ScopeId scope)
    {
        var result = new List<ListenerActivation>();
        foreach (var definition in RuleSet.Subscriptions.Where(s => s.EventType.Id == eventType.Id))
        {
            CountInstruction();
            if (definition.Instances is null) { result.Add(new(definition, Value.None, 0)); continue; }
            var args = payload.Append(Value.None).ToArray();
            var context = new CallActivation(definition.EvaluationProcedure(), scope, -1, null, args);
            foreach (var instance in EvaluateQuery(context, definition.Instances).ToArray())
            {
                CountInstruction();
                context.Slots[payload.Length] = Value.OfRef(instance);
                var order = definition.InstanceOrder is null ? 0 : Eval(context, definition.InstanceOrder).AsInt();
                result.Add(new(definition, Value.OfRef(instance), order));
            }
        }
        return result.OrderBy(s => s.Definition.Priority).ThenBy(s => s.Order)
            .ThenBy(s => s.Definition.DeclarationOrder).ToArray();
    }

    private bool PrepareListener(ListenerActivation activation, Value[] payload, ScopeId inheritedScope, out Value[] arguments, out ScopeId scope)
    {
        var listener = activation.Definition;
        scope = inheritedScope;
        arguments = new Value[listener.Reaction.Parameters.Count];
        for (var i = 0; i < arguments.Length; i++) arguments[i] = i < payload.Length ? payload[i] : activation.Instance;
        if (listener.Instances is not null && !State.Exists(activation.Instance.AsRef())) return false;
        if (listener.ScopePayloadIndex >= 0)
        {
            var bound = payload[listener.ScopePayloadIndex];
            if (bound.Kind != ValueKind.Ref || !State.Exists(bound.AsRef())) return false;
            scope = State.ScopeOf(bound.AsRef());
        }
        if (!State.ScopeExists(scope)) return false;
        var context = new CallActivation(listener.EvaluationProcedure(), scope, -1, null, arguments);
        if (listener.Instances is not null && !EvaluateQuery(context, listener.Instances).Contains(activation.Instance.AsRef())) return false;
        return listener.Recheck is null || Eval(context, listener.Recheck).AsBool();
    }

    private void StartListener(DispatchActivation dispatch, ListenerActivation activation)
    {
        var listener = activation.Definition;
        var reaction = listener.Reaction;
        if (!PrepareListener(activation, dispatch.Payload, dispatch.InheritedScope, out var arguments, out var scope)) return;

        PushCall(reaction, arguments, scope, -1, null);
        if (Debug is not null)
            Trace(SessionDebugKind.ListenerEnter, reaction.QualifiedId,
                $"event={dispatch.EventType.QualifiedId}; priority={listener.Priority}");
    }

    private Value BuildAnswerValue(DecisionRequest request, DecisionAnswer answer)
    {
        switch (request.Kind)
        {
            case DecisionResponseKind.EntityRef:
            {
                var instances = answer.Instances ?? [];
                if (instances.Count != 1)
                    throw Faults.Of(EngineFaultCode.InvalidDecisionResponse, "an entity decision takes exactly one instance");
                RequireCandidate(request, instances[0]);
                return Value.OfRef(instances[0]);
            }

            case DecisionResponseKind.EntityRefList:
            {
                var instances = answer.Instances ?? [];
                if (instances.Count < request.Minimum || instances.Count > request.Maximum)
                {
                    throw Faults.Of(EngineFaultCode.InvalidDecisionResponse,
                        $"decision takes between {request.Minimum} and {request.Maximum} instances but " +
                        $"{instances.Count} were supplied");
                }

                var seen = new HashSet<InstanceId>();
                foreach (var instance in instances)
                {
                    RequireCandidate(request, instance);
                    if (!seen.Add(instance))
                        throw Faults.Of(EngineFaultCode.InvalidDecisionResponse, $"{instance} was selected twice");
                }

                return Value.OfRefs(instances);
            }

            case DecisionResponseKind.Int:
                if (answer.Number < request.Minimum || answer.Number > request.Maximum)
                {
                    throw Faults.Of(EngineFaultCode.InvalidDecisionResponse,
                        $"decision number {answer.Number} is outside [{request.Minimum}, {request.Maximum}]");
                }

                return Value.Of(answer.Number);

            case DecisionResponseKind.Confirm:
                if (!answer.Flag) throw Faults.Of(EngineFaultCode.InvalidDecisionResponse, "confirmation requires an affirmative answer");
                return Value.Of(true);
            case DecisionResponseKind.Bool:
                return Value.Of(answer.Flag);

            case DecisionResponseKind.Enum:
                if (answer.EnumOrdinal < request.Minimum || answer.EnumOrdinal > request.Maximum)
                    throw Faults.Of(EngineFaultCode.InvalidDecisionResponse,
                        $"enum ordinal {answer.EnumOrdinal} is outside [{request.Minimum}, {request.Maximum}]");
                return Value.OfEnum(answer.EnumOrdinal);

            case DecisionResponseKind.Participant:
                RoleOf(answer.Participant);
                return Value.OfParticipant(answer.Participant);

            default:
                throw Faults.Of(EngineFaultCode.InvalidDecisionResponse,
                    $"unsupported decision kind '{request.Kind}'");
        }
    }

    private static void RequireCandidate(DecisionRequest request, InstanceId instance)
    {
        if (!request.Candidates.Contains(instance))
        {
            throw Faults.Of(EngineFaultCode.InvalidDecisionResponse,
                $"{instance} is not a candidate of decision {request.RequestId}");
        }
    }

}
