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
            case Word64Expr word:
                return Value.Of(word.Op switch
                {
                    Word64Op.HashUtf8 => Word64.HashUtf8(Eval(call, word.Arguments[0]).AsString()),
                    Word64Op.Add => Word64.Add(Eval(call, word.Arguments[0]).AsInt(), Eval(call, word.Arguments[1]).AsInt()),
                    _ => throw Faults.Schema("Unknown word64 operation"),
                });
            case TextExpr text:
            {
                var value = Eval(call, text.Text).AsString();
                var argument = Eval(call, text.Argument);
                if (text.Op == TextOp.StartsWith) return Value.Of(value.StartsWith(argument.AsString(), StringComparison.Ordinal));
                if (text.Op != TextOp.Substring) throw Faults.Schema("Unknown text operation");
                var start = argument.AsInt();
                if (start < 0 || start > value.Length) throw Faults.Range("substring start is outside the UTF-16 text");
                return Value.Of(value[(int)start..]);
            }
            case RandomValueExpr randomValue:
            {
                Value Arg(int index) => Eval(call, randomValue.Arguments[index]);
                float Float(int index) => Binary32.Read(Arg(index).AsInt());
                return randomValue.Op switch
                {
                    RandomValueOp.GameSeed => Value.Of(GameSeed),
                    RandomValueOp.Hash32 => Value.Of(Binary32.Hash32(Arg(0).AsString())),
                    RandomValueOp.Float32 => Value.Of(Binary32.Bits(Arg(0).Kind == ValueKind.Fixed ? (float)Arg(0).AsDecimal() : (float)Arg(0).AsInt())),
                    RandomValueOp.Float32Add => Value.Of(Binary32.Bits(Float(0) + Float(1))),
                    RandomValueOp.Float32Multiply => Value.Of(Binary32.Bits(Float(0) * Float(1))),
                    RandomValueOp.Float32RoundToInt => Value.Of(Binary64.RoundToInt(Float(0))),
                    RandomValueOp.Float32Subtract => Value.Of(Binary32.Bits(Float(0) - Float(1))),
                    RandomValueOp.Float32Minimum => Value.Of(Binary32.Bits(MathF.Min(Float(0), Float(1)))),
                    RandomValueOp.Float32Less => Value.Of(Float(0) < Float(1)),
                    RandomValueOp.Float32LessOrEqual => Value.Of(Float(0) <= Float(1)),
                    _ => throw Faults.Schema("Unknown random value operation"),
                };
            }

            case Binary64Expr binary64:
            {
                Value Arg(int index) => Eval(call, binary64.Arguments[index]);
                double Number(int index) => Binary64.Read(Arg(index).AsInt());
                return binary64.Op switch
                {
                    Binary64Op.FromNumber => Value.Of(Binary64.Bits(Arg(0).Kind == ValueKind.Fixed ? (double)Arg(0).AsDecimal() : (double)Arg(0).AsInt())),
                    Binary64Op.Add => Value.Of(Binary64.Bits(Number(0) + Number(1))),
                    Binary64Op.Subtract => Value.Of(Binary64.Bits(Number(0) - Number(1))),
                    Binary64Op.Multiply => Value.Of(Binary64.Bits(Number(0) * Number(1))),
                    Binary64Op.Divide => Value.Of(Binary64.Bits(Number(0) / Number(1))),
                    Binary64Op.Sqrt => Value.Of(Binary64.Bits(Math.Sqrt(Number(0)))),
                    Binary64Op.Log => Value.Of(Binary64.Bits(Math.Log(Number(0)))),
                    Binary64Op.Sin => Value.Of(Binary64.Bits(Math.Sin(Number(0)))),
                    Binary64Op.RoundToInt => Value.Of(Binary64.RoundToInt(Number(0))),
                    Binary64Op.Less => Value.Of(Number(0) < Number(1)),
                    Binary64Op.LessOrEqual => Value.Of(Number(0) <= Number(1)),
                    _ => throw Faults.Schema("Unknown binary64 operation"),
                };
            }

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
            case FoldEventExpr fold:
            {
                var eventType = Resolve<EventResolution>(call, fold.EventRef).Event;
                var arguments = fold.Arguments.Select(arg => Eval(call, arg)).ToArray();
                foreach (var activation in SnapshotListeners(eventType, arguments, call.Scope))
                {
                    if (!PrepareListener(activation, arguments, call.Scope, out var bound, out var scope)) continue;
                    var result = InvokePure(call, activation.Definition.Reaction, bound, scope);
                    if (result.Kind != arguments[0].Kind)
                        throw Faults.TypeError($"foldEvent listener '{activation.Definition.Reaction.QualifiedId}' returned {result.Kind} instead of {arguments[0].Kind}");
                    arguments[0] = result;
                }
                return arguments[0];
            }

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

    private Value InvokePure(CallActivation caller, ProcedureDefinition procedure, Value[] arguments, ScopeId? scope = null)
    {
        var depth = _stack.Count;
        PushCall(procedure, arguments, scope ?? caller.Scope, -1, null);
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
        if (aggregate.Op == AggregateOp.SumFloat32)
        {
            // Enumerable.Sum(float) accumulates in double and rounds once on return.
            double total = 0;
            foreach (var item in items)
            {
                call.Slots[aggregate.ItemSlot] = Value.OfRef(item);
                total += Binary32.Read(Eval(call, aggregate.ItemValue!).AsInt());
            }
            return Value.Of(Binary32.Bits((float)total));
        }
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
            ValueKind.None => 0,
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
