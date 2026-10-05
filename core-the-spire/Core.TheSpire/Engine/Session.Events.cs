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

        var listeners = RuleSet.Subscriptions
            .Where(subscription => subscription.EventType.Id == eventType.Id)
            .OrderBy(subscription => subscription.Priority)
            .ThenBy(subscription => subscription.DeclarationOrder)
            .ToArray();
        _stack.Add(new DispatchActivation(eventType, listeners, payload, inheritedScope));
        if (Debug is not null)
            Trace(SessionDebugKind.EventEmit, eventType.QualifiedId,
                $"scope={inheritedScope}; listeners={listeners.Length}; payload=[{string.Join(',', payload)}]");
    }

    private void StartListener(DispatchActivation dispatch, SubscriptionDefinition listener)
    {
        var reaction = listener.Reaction;
        var scope = dispatch.InheritedScope;
        if (listener.ScopePayloadIndex >= 0)
        {
            var bound = dispatch.Payload[listener.ScopePayloadIndex];
            if (bound.Kind != ValueKind.Ref) return;
            if (!State.Exists(bound.AsRef())) return;
            scope = State.ScopeOf(bound.AsRef());
        }
        else if (!State.ScopeExists(scope))
        {
            return;
        }

        var arguments = new Value[reaction.Parameters.Count];
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = index < dispatch.Payload.Length
                ? dispatch.Payload[index]
                : reaction.Parameters[index].DefaultValue;
        }

        if (listener.Recheck is not null)
        {
            // Resolve predicate references in its author package, then run the reaction in its own package.
            var context = reaction with { PackageIndex = listener.PackageIndex, RefBase = listener.RefBase };
            var probe = new CallActivation(context, scope, -1, null, arguments);
            if (!Eval(probe, listener.Recheck).AsBool()) return;
        }

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

            case DecisionResponseKind.Bool or DecisionResponseKind.Confirm:
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
