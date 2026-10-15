namespace NetTheSpire.Contracts;

/// <summary>
/// 身份与摘要输入白名单。唯一规范出处：contract-artifacts §2 第 23–31 行。
/// 排除仅供人读的 description、路径与诊断时间；种子/进程身份不在内容身份中。
/// </summary>
public static class WireIdentity
{
    private static readonly string[] DeclarationTables =
    [
        "entity_kinds",
        "content_types",
        "fields",
        "relation_types",
        "pile_types",
        "sequence_types",
        "group_types",
        "action_types",
        "terminal_reasons",
    ];

    /// <summary>目录身份中按 ID 集合排序的键。</summary>
    private static readonly HashSet<string> IdSetKeys =
    [
        "kind_ids",
        "member_kind_ids",
        "owner_kind_ids",
        "element_kind_ids",
        "source_kind_ids",
        "target_kind_ids",
        "required_fields",
        "optional_fields",
        "visibility_check_ids",
    ];

    /// <summary>明确“公开有序”而不排序的数组（contract-artifacts §2 第 27 行）。</summary>
    private static readonly HashSet<string> OrderedByDeclaration = ["category_ids"];

    private static readonly HashSet<string> ForbiddenRulesetKeys =
    [
        "seed", "seeds", "run_seed", "timestamp", "started_at", "finished_at", "pid", "process_id",
    ];

    private static readonly HashSet<string> ForbiddenBackendKeys =
    [
        "pid", "process_id", "limits", "elapsed_ms", "timing", "started_at", "max_concurrency",
    ];

    private static readonly string[] RulesetRequiredKeys =
    [
        "engine_versions", "ir_version", "package_order", "package_digests", "rule_config",
        "numeric_config", "prng_profile",
    ];

    private static readonly string[] BackendRequiredKeys =
    [
        "backend_kind", "projection_semantics_version", "public_identity_version",
        "action_mapping_version", "field_availability", "supported_mechanism_ids",
    ];

    /// <summary>catalog_id 摘要输入：protocol_version + 各声明表除 description 外的全部语义键。</summary>
    public static WireValue CatalogIdentityInput(WireObject catalog)
    {
        var declarations = catalog.Get("declarations") as WireObject ?? new WireObject([]);
        var tables = new List<KeyValuePair<string, WireValue>>();
        foreach (var table in DeclarationTables)
        {
            var items = declarations.Get(table) as WireArray ?? new WireArray([]);
            var stripped = items.Items
                .Select(CanonicalizeDeclaration)
                .OrderBy(CanonicalSortKey, Comparer<byte[]>.Create(CompareBytes))
                .ToList();
            tables.Add(new KeyValuePair<string, WireValue>(table, new WireArray(stripped)));
        }

        return WireValue.Object(
            ("canonical_version", WireValue.Str(WireCanonical.CanonicalVersion)),
            ("protocol_version", catalog.Get("protocol_version") ?? WireValue.Null),
            ("declarations", new WireObject(tables)));
    }

    public static string CatalogDigest(WireObject catalog) => WireCanonical.Digest(CatalogIdentityInput(catalog));

    public static WireValue RulesetIdentityInput(WireObject descriptor)
    {
        RejectKeys(descriptor, ForbiddenRulesetKeys, "ruleset_id");
        var members = new List<KeyValuePair<string, WireValue>>
        {
            new("canonical_version", WireValue.Str(WireCanonical.CanonicalVersion)),
        };
        foreach (var key in RulesetRequiredKeys)
        {
            if (!descriptor.TryGet(key, out var value))
                throw new WireFaultException("wire-identity", $"ruleset_id 输入缺少键: {key}");
            members.Add(new KeyValuePair<string, WireValue>(key, value));
        }

        return new WireObject(members);
    }

    public static string RulesetDigest(WireObject descriptor) => WireCanonical.Digest(RulesetIdentityInput(descriptor));

    public static WireValue BackendProfileIdentityInput(WireObject descriptor)
    {
        RejectKeys(descriptor, ForbiddenBackendKeys, "backend_profile_id");
        var members = new List<KeyValuePair<string, WireValue>>
        {
            new("canonical_version", WireValue.Str(WireCanonical.CanonicalVersion)),
        };
        foreach (var key in BackendRequiredKeys)
        {
            if (!descriptor.TryGet(key, out var value))
                throw new WireFaultException("wire-identity", $"backend_profile_id 输入缺少键: {key}");
            members.Add(new KeyValuePair<string, WireValue>(key, value));
        }

        return new WireObject(members);
    }

    public static string BackendProfileDigest(WireObject descriptor) =>
        WireCanonical.Digest(BackendProfileIdentityInput(descriptor));

    /// <summary>请求载荷指纹：operation 与完整 payload；request_id 不参与。</summary>
    public static string RequestPayloadFingerprint(string operation, WireValue payload) =>
        WireCanonical.Digest(WireValue.Object(
            ("canonical_version", WireValue.Str(WireCanonical.CanonicalVersion)),
            ("operation", WireValue.Str(operation)),
            ("payload", payload)));

    /// <summary>快照存储摘要：完整规范公开快照（含身份与控制元数据）。</summary>
    public static string SnapshotStorageDigest(WireValue snapshot) => WireCanonical.Digest(snapshot);

    private static byte[] CanonicalSortKey(WireValue item) =>
        item is WireObject obj && obj.Get("id") is WireString id
            ? System.Text.Encoding.UTF8.GetBytes(id.Value)
            : System.Text.Encoding.UTF8.GetBytes(WireCanonical.Text(item));

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

    /// <summary>递归去掉 description、排序 ID 集合；category_ids 保持声明顺序。</summary>
    private static WireValue CanonicalizeDeclaration(WireValue node)
    {
        switch (node)
        {
            case WireObject obj:
            {
                var members = new List<KeyValuePair<string, WireValue>>();
                foreach (var member in obj.Members)
                {
                    if (member.Key == "description") continue;
                    if (OrderedByDeclaration.Contains(member.Key))
                    {
                        members.Add(member);
                        continue;
                    }

                    if (IdSetKeys.Contains(member.Key) && member.Value is WireArray array)
                    {
                        var sorted = array.Items
                            .OrderBy(SortableBytes, Comparer<byte[]>.Create(CompareBytes))
                            .ToList();
                        members.Add(new KeyValuePair<string, WireValue>(member.Key, new WireArray(sorted)));
                        continue;
                    }

                    if (member.Key == "applies_to" && member.Value is WireObject appliesTo)
                    {
                        var inner = appliesTo.Members.Select(innerMember =>
                            new KeyValuePair<string, WireValue>(
                                innerMember.Key,
                                innerMember.Value is WireArray list
                                    ? new WireArray(list.Items
                                        .OrderBy(SortableBytes, Comparer<byte[]>.Create(CompareBytes))
                                        .ToList())
                                    : innerMember.Value)).ToList();
                        members.Add(new KeyValuePair<string, WireValue>(member.Key, new WireObject(inner)));
                        continue;
                    }

                    members.Add(new KeyValuePair<string, WireValue>(member.Key, CanonicalizeDeclaration(member.Value)));
                }

                return new WireObject(members);
            }
            case WireArray array:
                return new WireArray(array.Items.Select(CanonicalizeDeclaration));
            default:
                return node;
        }
    }

    private static byte[] SortableBytes(WireValue item) =>
        item is WireString text
            ? System.Text.Encoding.UTF8.GetBytes(text.Value)
            : System.Text.Encoding.UTF8.GetBytes(WireCanonical.Text(item));

    private static void RejectKeys(WireValue node, HashSet<string> forbidden, string what)
    {
        if (node is not WireObject obj) return;
        foreach (var member in obj.Members)
        {
            if (forbidden.Contains(member.Key))
                throw new WireFaultException("wire-identity", $"{what}: 身份输入不得包含 '{member.Key}'");
            // 与 Python 参考实现一致：只递归对象成员，不遍历数组元素。
            if (member.Value is WireObject) RejectKeys(member.Value, forbidden, what);
        }
    }
}
