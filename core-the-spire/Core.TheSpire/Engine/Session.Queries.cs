namespace CoreTheSpire.Engine;

public sealed partial class Session
{
    // -- query evaluation ---------------------------------------------------------------------------------------

    private IReadOnlyList<InstanceId> EvaluateQuery(CallActivation call, Query query)
    {
        switch (query)
        {
            case TypeQuery type:
            {
                var definition = Resolve<TypeResolution>(call, type.TypeRef).Type;
                var scopeTypeId = type.ScopeTypeRef < 0
                    ? -1
                    : Resolve<ScopeResolution>(call, type.ScopeTypeRef).ScopeType.Id;
                var all = State.AllInstances(definition.Id);
                if (scopeTypeId < 0) return all;
                return all.Where(instance => State.ScopeTypeId(State.ScopeOf(instance)) == scopeTypeId).ToArray();
            }

            case RefListQuery refList:
            {
                // 引用列表槽里的引用，按**存入顺序**遍历：多选的消费顺序就是玩家/程序的回答顺序，
                // 不做额外排序（要稳定顺序请在消费端显式排序）。
                var value = call.Slots[refList.ListSlot];
                if (value.Kind != ValueKind.RefList)
                {
                    throw Faults.Schema(
                        $"'{refList.Primitive}' needs a list-of-ref slot but slot {refList.ListSlot} holds " +
                        $"'{value.Kind}'");
                }

                return value.AsRefList();
            }

            case CollectionQuery collection:
            {
                var definition = Resolve<CollectionResolution>(call, collection.CollectionRef).Collection;
                var members = State.CollectionMembers(definition.Id);
                if (collection.ScopeTypeRef < 0) return members;
                var scopeTypeId = Resolve<ScopeResolution>(call, collection.ScopeTypeRef).ScopeType.Id;
                return members.Where(instance => State.ScopeTypeId(State.ScopeOf(instance)) == scopeTypeId).ToArray();
            }

            case RelationQuery relation:
            {
                var definition = Resolve<RelationResolution>(call, relation.RelationRef).Relation;
                var owner = RequireReference(Eval(call, relation.Owner), "relation owner");
                return State.RelationTargets(definition.Id, owner);
            }

            case FilterQuery filter:
            {
                var source = EvaluateQuery(call, filter.Source);
                var kept = new List<InstanceId>(source.Count);
                foreach (var item in source)
                {
                    call.Slots[filter.ItemSlot] = Value.OfRef(item);
                    if (Eval(call, filter.Predicate).AsBool()) kept.Add(item);
                }

                return kept;
            }

            case SortQuery sort:
            {
                var source = EvaluateQuery(call, sort.Source);
                var keys = new (InstanceId Item, Value Key)[source.Count];
                for (var index = 0; index < source.Count; index++)
                {
                    call.Slots[sort.ItemSlot] = Value.OfRef(source[index]);
                    keys[index] = (source[index], Eval(call, sort.Key));
                }

                var ordered = keys.OrderBy(pair => pair.Key, ValueComparer.Instance)
                    .AsEnumerable();
                if (sort.Descending) ordered = ordered.Reverse();
                return ordered.Select(pair => pair.Item).ToArray();
            }

            case TakeQuery take:
            {
                var source = EvaluateQuery(call, take.Source);
                var count = (int)Math.Clamp(Eval(call, take.Count).AsInt(), 0L, source.Count);
                return source.Take(count).ToArray();
            }

            case SkipQuery skip:
            {
                var source = EvaluateQuery(call, skip.Source);
                var count = (int)Math.Clamp(Eval(call, skip.Count).AsInt(), 0L, source.Count);
                return source.Skip(count).ToArray();
            }

            default:
                throw Faults.Of(EngineFaultCode.Unsupported,
                    $"query '{query.Primitive}' is not implemented by the scheduler");
        }
    }

}
