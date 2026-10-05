namespace CoreTheSpire.Engine;

public sealed partial class Session
{
    // -- expression evaluation ----------------------------------------------------------------------------------

    private Value Eval(CallActivation call, Expr expression)
    {
        switch (expression)
        {
            case ConstExpr constant:
                return constant.Value;

            case LocalExpr local:
                return call.Slots[local.Slot];

            case ParamExpr parameter:
                return call.Slots[parameter.Slot];

            case FrameScopeExpr:
                return Value.OfScope(call.Scope);

            case EntityScopeExpr entityScope:
            {
                var entity = RequireReference(Eval(call, entityScope.Entity), "scope lookup");
                return Value.OfScope(State.ScopeOf(entity));
            }

            case FieldExpr field:
            {
                var resolution = ResolveEntityField(call, field.FieldRef);
                var target = RequireReference(Eval(call, field.Target), $"field '{resolution.Field.Name}' target");
                return State.GetField(target, resolution.Index);
            }

            case ArithExpr arith:
                return Arithmetic(arith.Op, Eval(call, arith.Left), Eval(call, arith.Right));

            case CompareExpr compare:
                return Value.Of(Compare(compare.Op, Eval(call, compare.Left), Eval(call, compare.Right)));

            case LogicExpr logic:
            {
                var left = Eval(call, logic.Left).AsBool();
                if (logic.IsAnd && !left) return Value.Of(false);
                if (!logic.IsAnd && left) return Value.Of(true);
                return Value.Of(Eval(call, logic.Right).AsBool());
            }

            case NotExpr not:
                return Value.Of(!Eval(call, not.Operand).AsBool());

            case ConvertExpr convert:
            {
                var operand = Eval(call, convert.Operand);
                return convert.Op switch
                {
                    ConvertOp.IntToFixed => Value.Of(RuleSet.NumericProfile.IntToFixed(operand.AsInt())),
                    ConvertOp.FixedToIntTruncate => Value.Of(
                        RuleSet.NumericProfile.FixedToInt(operand.AsDecimal(), FixedRounding.Truncate)),
                    ConvertOp.FixedToIntFloor => Value.Of(
                        RuleSet.NumericProfile.FixedToInt(operand.AsDecimal(), FixedRounding.Floor)),
                    ConvertOp.FixedToIntCeiling => Value.Of(
                        RuleSet.NumericProfile.FixedToInt(operand.AsDecimal(), FixedRounding.Ceiling)),
                    ConvertOp.FixedToIntRound => Value.Of(
                        RuleSet.NumericProfile.FixedToInt(operand.AsDecimal(), FixedRounding.Round)),
                    _ => throw Faults.Of(EngineFaultCode.Unsupported, $"conversion '{convert.Op}' is not implemented"),
                };
            }

            case EnumConstExpr enumConstant:
            {
                var enumType = Resolve<EnumTypeResolution>(call, enumConstant.EnumRef).Enum;
                return Value.OfEnum(enumType.OrdinalOf(enumConstant.Member));
            }

            case CountExpr count:
                return Value.Of((long)EvaluateQuery(call, count.Source).Count);

            case CollectionIndexExpr collectionIndex:
            {
                var collection = Resolve<CollectionResolution>(call, collectionIndex.CollectionRef).Collection;
                var item = RequireReference(Eval(call, collectionIndex.Item), "collection index");
                return Value.Of((long)State.CollectionIndexOf(collection.Id, item));
            }

            case ExistsExpr exists:
                return Value.Of(EvaluateQuery(call, exists.Source).Count > 0);

            case FirstExpr first:
            {
                var items = EvaluateQuery(call, first.Source);
                return items.Count == 0 ? Value.None : Value.OfRef(items[0]);
            }

            case IsNoneExpr isNone:
            {
                var operand = Eval(call, isNone.Operand);
                return Value.Of(operand.Kind == ValueKind.None || (operand.Kind == ValueKind.Ref && operand.AsRef().IsNone));
            }

            case CallPureExpr pure:
            {
                var procedure = Resolve<ProcedureResolution>(call, pure.ProcedureRef).Procedure;
                var arguments = new Value[pure.Arguments.Count];
                for (var index = 0; index < arguments.Length; index++) arguments[index] = Eval(call, pure.Arguments[index]);
                return InvokePure(call, procedure, arguments);
            }

            case AggregateExpr aggregate:
                return Aggregate(call, aggregate);

            case StringConcatExpr concat:
                return Value.Of(string.Concat(concat.Parts.Select(part => Eval(call, part) switch
                {
                    { Kind: ValueKind.String } text => text.AsString(),
                    { Kind: ValueKind.Int } number => number.AsInt().ToString(System.Globalization.CultureInfo.InvariantCulture),
                    var other => throw Faults.TypeError($"cannot concatenate a {other.Kind} value"),
                })));

            default:
                throw Faults.Of(EngineFaultCode.Unsupported,
                    $"expression '{expression.Primitive}' is not implemented by the scheduler");
        }
    }

    private Value InvokePure(CallActivation caller, ProcedureDefinition procedure, Value[] arguments)
    {
        var depth = _stack.Count;
        PushCall(procedure, arguments, caller.Scope, -1, null);
        var activation = (CallActivation)_stack[^1];
        DrainTo(depth);
        if (_stack.Count > depth)
        {
            throw Faults.Of(EngineFaultCode.SessionStateInvalid,
                $"pure procedure '{procedure.QualifiedId}' did not complete; pure procedures cannot suspend");
        }

        return activation.ReturnValue;
    }

    private Value Aggregate(CallActivation call, AggregateExpr aggregate)
    {
        var items = EvaluateQuery(call, aggregate.Source);
        if (aggregate.Op == AggregateOp.Count) return Value.Of((long)items.Count);
        if (items.Count == 0) return Value.Of(0L);
        Value? accumulator = null;
        foreach (var item in items)
        {
            call.Slots[aggregate.ItemSlot] = Value.OfRef(item);
            var value = Eval(call, aggregate.ItemValue!);
            if (accumulator is null)
            {
                accumulator = aggregate.Op == AggregateOp.Sum
                    ? value.Kind == ValueKind.Fixed ? Value.Of(0m) : Value.Of(0L)
                    : value;
            }

            accumulator = Arithmetic(
                aggregate.Op switch
                {
                    AggregateOp.Sum => ArithOp.Add,
                    AggregateOp.Minimum => ArithOp.Minimum,
                    AggregateOp.Maximum => ArithOp.Maximum,
                    _ => throw Faults.Of(EngineFaultCode.Unsupported, $"aggregate '{aggregate.Op}' is not implemented"),
                },
                accumulator.Value, value);
        }

        return accumulator ?? Value.Of(0L);
    }

    private Value Arithmetic(ArithOp op, Value left, Value right)
    {
        var profile = RuleSet.NumericProfile;
        if (left.Kind == ValueKind.Int && right.Kind == ValueKind.Int)
        {
            var a = left.AsInt();
            var b = right.AsInt();
            return Value.Of(op switch
            {
                ArithOp.Add => profile.AddInt(a, b),
                ArithOp.Subtract => profile.SubtractInt(a, b),
                ArithOp.Multiply => profile.MultiplyInt(a, b),
                ArithOp.Divide => profile.DivideInt(a, b),
                ArithOp.Modulo => profile.ModuloInt(a, b),
                ArithOp.Minimum => Math.Min(a, b),
                ArithOp.Maximum => Math.Max(a, b),
                _ => throw Faults.Of(EngineFaultCode.Unsupported, $"arithmetic '{op}' is not implemented"),
            });
        }

        if (left.Kind == ValueKind.Fixed && right.Kind == ValueKind.Fixed)
        {
            var a = left.AsDecimal();
            var b = right.AsDecimal();
            return Value.Of(op switch
            {
                ArithOp.Add => profile.AddFixed(a, b),
                ArithOp.Subtract => profile.SubtractFixed(a, b),
                ArithOp.Multiply => profile.MultiplyFixed(a, b),
                ArithOp.Divide => profile.DivideFixed(a, b),
                ArithOp.Minimum => Math.Min(a, b),
                ArithOp.Maximum => Math.Max(a, b),
                _ => throw Faults.Of(EngineFaultCode.Unsupported, $"fixed-point arithmetic '{op}' is not implemented"),
            });
        }

        throw Faults.TypeError($"arithmetic mixes {left.Kind} with {right.Kind}");
    }

    private static bool Compare(CompareOp op, Value left, Value right)
    {
        if (left.Kind != right.Kind)
        {
            if (left.Kind == ValueKind.None || right.Kind == ValueKind.None)
                return op == CompareOp.Equal ? (left.Kind == right.Kind) : (left.Kind != right.Kind);
            throw Faults.TypeError($"comparison mixes {left.Kind} with {right.Kind}");
        }

        var ordering = left.Kind switch
        {
            ValueKind.Bool => left.AsBool().CompareTo(right.AsBool()),
            ValueKind.Int => left.AsInt().CompareTo(right.AsInt()),
            ValueKind.Fixed => left.AsDecimal().CompareTo(right.AsDecimal()),
            ValueKind.Enum => left.AsEnumOrdinal().CompareTo(right.AsEnumOrdinal()),
            ValueKind.String => string.CompareOrdinal(left.AsString(), right.AsString()),
            ValueKind.Ref => left.AsRef().CompareTo(right.AsRef()),
            ValueKind.Participant => left.AsParticipant().CompareTo(right.AsParticipant()),
            ValueKind.Scope => left.AsScope().CompareTo(right.AsScope()),
            _ => throw Faults.Of(EngineFaultCode.Unsupported, $"comparison of {left.Kind} values is not implemented"),
        };

        return op switch
        {
            CompareOp.Equal => ordering == 0,
            CompareOp.NotEqual => ordering != 0,
            CompareOp.Less => ordering < 0,
            CompareOp.LessOrEqual => ordering <= 0,
            CompareOp.Greater => ordering > 0,
            CompareOp.GreaterOrEqual => ordering >= 0,
            _ => throw Faults.Of(EngineFaultCode.Unsupported, $"comparison '{op}' is not implemented"),
        };
    }

}
