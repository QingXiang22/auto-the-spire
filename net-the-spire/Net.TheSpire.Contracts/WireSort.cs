namespace NetTheSpire.Contracts;

/// <summary>
/// 公开数组排序与动作语义键。唯一规范出处：contract-artifacts §3 第 37/39 行、
/// protocol.md §2 第 102 行。所有排序只看公开值，<c>candidate_id</c> 不参与。
/// </summary>
public static class WireSort
{
    private static readonly byte[] Missing = [0x00];

    private static byte[] Text(WireValue? value) => value switch
    {
        null => Missing,
        WireNull => Missing,
        WireString text => System.Text.Encoding.UTF8.GetBytes(text.Value),
        _ => System.Text.Encoding.UTF8.GetBytes(WireCanonical.Text(value)),
    };

    private static int CompareBytes(byte[] left, byte[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        for (var index = 0; index < length; index++)
        {
            var difference = left[index] - right[index];
            if (difference != 0) return difference;
        }

        return left.Length - right.Length;
    }

    private static int CompareField(WireObject value, string key, byte[] other) =>
        CompareBytes(Text(value.Get(key)), other);

    /// <summary>实体按公开 instance_id 排序。</summary>
    public static int CompareEntity(WireObject left, WireObject right) =>
        CompareField(left, "instance_id", Text(right.Get("instance_id")));

    /// <summary>字段按 field_id 排序。</summary>
    public static int CompareField(WireObject left, WireObject right) =>
        CompareField(left, "field_id", Text(right.Get("field_id")));

    /// <summary>relations 按 type_id/source_ref/target_ref 排序；同键时无 position 在前，再按 position 升序。</summary>
    public static int CompareRelation(WireObject left, WireObject right)
    {
        var result = CompareField(left, "type_id", Text(right.Get("type_id")));
        if (result != 0) return result;
        result = CompareField(left, "source_ref", Text(right.Get("source_ref")));
        if (result != 0) return result;
        result = CompareField(left, "target_ref", Text(right.Get("target_ref")));
        if (result != 0) return result;

        var leftPosition = left.Get("position");
        var rightPosition = right.Get("position");
        var leftHas = leftPosition is not null and not WireNull;
        var rightHas = rightPosition is not null and not WireNull;
        if (leftHas != rightHas) return leftHas ? 1 : -1;
        if (!leftHas) return 0;
        return AsInteger(leftPosition!).CompareTo(AsInteger(rightPosition!));
    }

    private static System.Numerics.BigInteger AsInteger(WireValue value) => value switch
    {
        WireInteger integer => integer.ToBigInteger(),
        WireFloatLiteral literal => System.Numerics.BigInteger.Parse(
            literal.Raw.Split('.', 'e', 'E')[0], System.Globalization.CultureInfo.InvariantCulture),
        _ => System.Numerics.BigInteger.Zero,
    };

    /// <summary>piles 按 pile_ref 排序。</summary>
    public static int ComparePile(WireObject left, WireObject right) =>
        CompareField(left, "pile_ref", Text(right.Get("pile_ref")));

    /// <summary>sequences/groups 按 type_id/owner_ref 排序。</summary>
    public static int CompareTypeOwner(WireObject left, WireObject right)
    {
        var result = CompareField(left, "type_id", Text(right.Get("type_id")));
        return result != 0 ? result : CompareField(left, "owner_ref", Text(right.Get("owner_ref")));
    }

    /// <summary>known_positions 按 position/card_ref 排序（无 position 用 -1）。</summary>
    public static int CompareCardPosition(WireObject left, WireObject right)
    {
        var leftPosition = left.Get("position");
        var rightPosition = right.Get("position");
        long leftValue = -1, rightValue = -1;
        if (leftPosition is WireInteger leftInteger && leftInteger.TryToLong(out var parsedLeft)) leftValue = parsedLeft;
        if (rightPosition is WireInteger rightInteger && rightInteger.TryToLong(out var parsedRight)) rightValue = parsedRight;
        var result = leftValue.CompareTo(rightValue);
        return result != 0 ? result : CompareField(left, "card_ref", Text(right.Get("card_ref")));
    }

    /// <summary>groups.members 按 entity_ref 排序。</summary>
    public static int CompareGroupMember(WireObject left, WireObject right) =>
        CompareField(left, "entity_ref", Text(right.Get("entity_ref")));

    /// <summary>
    /// 动作语义键：规范序列化的 <c>{action_type, fields}</c>，fields 按 field_id。
    /// candidate_id 不参与排序/特征。
    /// </summary>
    public static byte[] ActionSemanticKey(WireObject action)
    {
        var fields = action.Get("fields") as WireArray ?? new WireArray([]);
        var ordered = fields.Items
            .OfType<WireObject>()
            .OrderBy(field => Text(field.Get("field_id")), Comparer<byte[]>.Create(CompareBytes))
            .ToList();
        return WireCanonical.Bytes(WireValue.Object(
            ("action_type", action.Get("action_type") ?? WireValue.Null),
            ("fields", new WireArray(ordered))));
    }

    public static int CompareActionSemanticKey(WireObject left, WireObject right) =>
        CompareBytes(ActionSemanticKey(left), ActionSemanticKey(right));

    /// <summary>按列表顺序比较两个数组是否已按给定比较器升序排列（严格，不允许相等）。</summary>
    public static bool IsStrictlySorted(IReadOnlyList<WireObject> items, Comparison<WireObject> comparison)
    {
        for (var index = 1; index < items.Count; index++)
        {
            if (comparison(items[index - 1], items[index]) >= 0) return false;
        }

        return true;
    }
}
