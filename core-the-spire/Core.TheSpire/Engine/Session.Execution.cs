namespace CoreTheSpire.Engine;

public sealed partial class Session
{
    // -- scheduling ---------------------------------------------------------------------------------------------

    private void Drain() => DrainTo(0);

    private void DrainTo(int targetDepth)
    {
        _drainDepth++;
        try
        {
            while (_stack.Count > targetDepth && Status == SessionStatus.Running)
            {
                var top = _stack[^1];
                if (top is StartupActivation startup)
                {
                    if (startup.Next >= startup.Procedures.Count)
                        _stack.RemoveAt(_stack.Count - 1);
                    else
                        PushCall(startup.Procedures[startup.Next++], [], State.RootScope, -1, null);
                    continue;
                }
                if (top is DispatchActivation dispatch)
                {
                    if (dispatch.Next >= dispatch.Listeners.Length)
                    {
                        _stack.RemoveAt(_stack.Count - 1);
                        continue;
                    }

                    var listener = dispatch.Listeners[dispatch.Next++];
                    StartListener(dispatch, listener);
                    continue;
                }

                var call = (CallActivation)top;
                var outcome = StepFrame(call);
                if (outcome != Outcome.Completed) continue;
                _stack.RemoveAt(_stack.Count - 1);
                Trace(SessionDebugKind.ProcedureExit, call.Procedure.QualifiedId, call.ReturnValue.ToString());
                CompleteCall(call);
            }
        }
        finally
        {
            _drainDepth--;
        }
    }

    private void CompleteCall(CallActivation call)
    {
        if (call.Caller is null || call.ResultSlot < 0) return;
        call.Caller.Slots[call.ResultSlot] = call.ReturnValue;
    }

    private Outcome StepFrame(CallActivation call)
    {
        var cursor = call.Cursors[^1];
        if (cursor.Pc >= cursor.Block.Count)
        {
            if (cursor.RepeatRemaining > 0)
            {
                cursor.RepeatRemaining--;
                cursor.Pc = 0;
                CountInstruction();
                return Outcome.Continue;
            }

            if (cursor.Items is not null && cursor.ItemSlot >= 0)
            {
                cursor.ItemIndex++;
                if (cursor.ItemIndex < cursor.Items.Count)
                {
                    call.Slots[cursor.ItemSlot] = Value.OfRef(cursor.Items[cursor.ItemIndex]);
                    cursor.Pc = 0;
                    return Outcome.Continue;
                }
            }

            call.Cursors.RemoveAt(call.Cursors.Count - 1);
            return call.Cursors.Count == 0 ? Outcome.Completed : Outcome.Continue;
        }

        var instruction = cursor.Block[cursor.Pc++];
        _executingSource = instruction.SourceLocation;
        Trace(SessionDebugKind.Instruction, call.Procedure.QualifiedId, instruction.Primitive, instruction.SourceLocation);
        CountInstruction();
        return Execute(call, instruction);
    }

    private void CountInstruction()
    {
        _instructions++;
        if (_instructions > Budget.MaxInstructions)
        {
            throw Faults.Budget(
                $"session exceeded its instruction budget of {Budget.MaxInstructions} in one command");
        }
    }

    private void PushCall(ProcedureDefinition procedure, IReadOnlyList<Value> arguments, ScopeId scope,
        int resultSlot, CallActivation? caller)
    {
        var slots = new Value[procedure.Parameters.Count + procedure.Locals.Count];
        for (var index = 0; index < procedure.Parameters.Count; index++)
            slots[index] = index < arguments.Count ? arguments[index] : procedure.Parameters[index].DefaultValue;
        for (var index = 0; index < procedure.Locals.Count; index++)
            slots[procedure.Parameters.Count + index] = procedure.Locals[index].DefaultValue;
        var activation = new CallActivation(procedure, scope, resultSlot, caller, slots);
        _stack.Add(activation);
        if (Debug is not null)
            Trace(SessionDebugKind.ProcedureEnter, procedure.QualifiedId,
                $"scope={scope}; arguments=[{string.Join(',', arguments)}]", procedure.Body.SourceLocation);
        var depth = 0;
        foreach (var item in _stack)
        {
            if (item is CallActivation) depth++;
        }

        if (depth > Budget.MaxCallDepth)
            throw Faults.Budget($"call depth {depth} exceeds the configured maximum {Budget.MaxCallDepth}");
    }

}
