using System.Text;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;

namespace NetTheSpire.Tests;

/// <summary>
/// 跨端一致检查：C# 契约层对 Python 参考实现（<c>tools/contract-check</c>）逐字节复算。
/// 期望值全部来自 Python 侧产出的对照清单（<c>fixtures/cross-end-*.json</c>）与
/// golden 向量，C# 端只读同一份输入，不复制参考资产、不另造协议形状。
/// 覆盖：L0（原始文档解析 + 规范字节）、L1（schema 驱动结构判定）、
/// golden（规范字节/decimal/动作语义键/身份摘要）、L4 的 seed 原语。
/// </summary>
internal static class CrossEndChecks
{
    /// <summary>
    /// 角色 → wire.schema.json 的 <c>$defs</c> 指针。单一出处是 Python 参考实现
    /// <c>tools/contract-check/check_contracts.py:56 DOC_ROLES</c>；C# 端镜像同一张表，
    /// 并用「用例角色必须全部有映射」兜底（新增角色会直接报错，而不是静默跳过 L1）。
    /// </summary>
    private static readonly Dictionary<string, string> DocRolePointers = new(StringComparer.Ordinal)
    {
        ["catalog"] = "#/$defs/PublicCatalog",
        ["snapshot"] = "#/$defs/PublicSnapshot",
        ["request"] = "#/$defs/Request",
        ["response"] = "#/$defs/Response",
        ["capabilities"] = "#/$defs/Capabilities",
        ["observe_result"] = "#/$defs/ObserveResult",
        ["attach_result"] = "#/$defs/AttachResult",
        ["close_result"] = "#/$defs/CloseResult",
        ["start_result"] = "#/$defs/StartResult",
        ["request_status_result"] = "#/$defs/RequestStatusResult",
        ["describe_result"] = "#/$defs/DescribeResult",
        ["error"] = "#/$defs/Error",
        ["identity"] = "#/$defs/Identity",
        ["start_payload"] = "#/$defs/StartPayload",
    };

    public static List<(string Name, Action Body)> Build()
    {
        var checks = new List<(string Name, Action Body)>();
        Fixtures fixtures;
        try
        {
            fixtures = Fixtures.Load();
        }
        catch (Exception exception)
        {
            checks.Add((
                "fixtures: 装载跨端对照资产",
                () => throw new CheckFailedException($"装载失败 {exception.GetType().Name}: {exception.Message}")));
            return checks;
        }

        List<CasePlanItem> plan;
        try
        {
            plan = BuildPlan(fixtures);
        }
        catch (Exception exception)
        {
            checks.Add((
                "manifest: 用例与参考夹具同源",
                () => throw new CheckFailedException($"{exception.GetType().Name}: {exception.Message}")));
            return checks;
        }

        checks.Add(("manifest: 用例与参考夹具同源", () => CheckManifest(fixtures, plan)));
        foreach (var item in plan)
        {
            var captured = item;
            checks.Add(($"case {captured.Id}: canonical 字节", () => CheckCanonical(fixtures, captured)));
            checks.Add(($"case {captured.Id}: L1 结构判定", () => CheckL1(fixtures, captured)));
            if (captured.RawDocuments.Count > 0)
                checks.Add(($"case {captured.Id}: L0 原始文档解析", () => CheckRaw(fixtures, captured)));
        }

        checks.Add(("L4/I-76 seed 原语（跨端用例驱动）", () => CheckSeedPrimitive(plan)));
        checks.Add(("L0/I-91 文本解析故障规则号（跨端用例驱动）", () => CheckL0RuleIds(plan)));
        checks.Add(("L3/I-21 非规范 decimal 故障规则号（跨端用例驱动）", () => CheckDecimalRuleIds(plan)));
        AddGolden(fixtures, checks);
        return checks;
    }

    // ------------------------------------------------------------------ 故障原语 rule id 的两端一致

    /// <summary>
    /// L0 严格文本解析的规则号：Python 权威为 <c>tools/contract-check/wire_json.py:37/41/100</c> 的
    /// <c>I-91</c>（JSON 常量、浮点字面量），清单里 <c>neg-number-value-json-float</c> 期望同一规则号。
    /// 该检查存在的原因：rule id 曾在 C# 侧被写成 <c>I-87</c>（与「动作/终端字段清单一致」的目录规则重载），
    /// 而跨端对照只比字节与 L1 文本、不比 rule id，所以漂移没有被发现。
    /// </summary>
    private static void CheckL0RuleIds(IReadOnlyList<CasePlanItem> plan)
    {
        var raw = plan.FirstOrDefault(item => item.RawDocuments.Count > 0)
            ?? throw new CheckFailedException("清单里没有任何原始文档（L0）用例");
        Check.True(raw.RuleIds.Contains("I-91"),
            $"L0 用例 {raw.Id} 的清单规则号应为 I-91（Python wire_json.py），得到 [{string.Join("/", raw.RuleIds)}]");

        Check.Fault(() => WireParser.ParseStrict("{\"a\": NaN}", "probe"), "NaN 必须按 L0 拒绝", "I-91");
        Check.Fault(() => WireParser.ParseStrict("{\"a\": Infinity}", "probe"), "Infinity 必须按 L0 拒绝", "I-91");
        Check.Fault(() => WireParser.ParseStrict("{\"a\": 1.5}", "probe"), "浮点字面量必须按 L0 拒绝", "I-91");
        Check.True(WireParser.ParseStrict("{\"a\": 1}", "probe") is WireObject, "结构整数仍然被接受");
    }

    /// <summary>
    /// decimal 语义的规则号：Python 权威为 <c>tools/contract-check/wire_layers.py:385</c> 的
    /// <c>I-21</c>；清单里的非规范 decimal 用例期望同一规则号。<c>I-23</c> 只保留给结构整数
    /// （<c>wire_json.py:113/115</c>），不再用于 decimal。
    /// </summary>
    private static void CheckDecimalRuleIds(IReadOnlyList<CasePlanItem> plan)
    {
        var decimalCases = plan.Where(item => item.RuleIds.Contains("I-21")).ToList();
        Check.True(decimalCases.Count >= 6, $"清单里的非规范 decimal 用例只有 {decimalCases.Count} 个");

        Check.Fault(
            () => new WireReader(
                    WireValue.Object(
                        ("field_id", WireValue.Str("probe")),
                        ("state", WireValue.Str("known")),
                        ("value", WireValue.Str("1.0"))),
                    "probe")
                .RequireDecimalString("value"),
            "尾随零的 decimal 必须被拒绝",
            "I-21");
        Check.Equal("0", new WireReader(
            WireValue.Object(
                ("field_id", WireValue.Str("probe")),
                ("state", WireValue.Str("known")),
                ("value", WireValue.Str("0"))),
            "probe").RequireDecimalString("value"), "规范 decimal 仍然被接受");
    }

    // ------------------------------------------------------------------ 清单与用例装载

    private sealed class CasePlanItem
    {
        public required string Id { get; init; }

        public required string Expect { get; init; }

        public required List<string> Layers { get; init; }

        public required List<string> RuleIds { get; init; }

        public required List<string> ManifestL1Findings { get; init; }

        public required List<string> ManifestRoles { get; init; }

        public required List<(string Role, WireValue Document)> Documents { get; init; }

        public required List<(string Role, string Raw)> RawDocuments { get; init; }
    }

    private static List<CasePlanItem> BuildPlan(Fixtures fixtures)
    {
        var sourceCases = new Dictionary<string, WireObject>(StringComparer.Ordinal);
        foreach (var source in fixtures.SourceCases)
            sourceCases[Str(source, "case_id")] = source;

        var plan = new List<CasePlanItem>();
        foreach (var manifestCase in Items(fixtures.CasesManifest, "cases"))
        {
            var id = Str(manifestCase, "id");
            if (!sourceCases.TryGetValue(id, out var source))
                throw new CheckFailedException($"清单用例 '{id}' 在 tools/contract-check/fixtures/cases 中不存在");

            var documents = new List<(string, WireValue)>();
            if (source.Get("documents") is WireObject specs)
            {
                foreach (var member in specs.Members)
                    documents.Add((member.Key, DocumentComposer.Resolve(member.Value, fixtures.BaseDocuments)));
            }

            var rawDocuments = new List<(string, string)>();
            if (source.Get("raw_documents") is WireObject raws)
            {
                foreach (var member in raws.Members)
                {
                    if (member.Value is not WireString raw)
                        throw new CheckFailedException($"{id}.raw_documents.{member.Key} 不是字符串");
                    rawDocuments.Add((member.Key, raw.Value));
                }
            }

            plan.Add(new CasePlanItem
            {
                Id = id,
                Expect = Str(manifestCase, "expect"),
                Layers = Strings(manifestCase, "layers"),
                RuleIds = Strings(manifestCase, "rule_ids"),
                ManifestL1Findings = Strings(manifestCase, "l1_findings"),
                ManifestRoles = Strings(manifestCase, "roles"),
                Documents = documents,
                RawDocuments = rawDocuments,
            });
        }

        return plan;
    }

    private static void CheckManifest(Fixtures fixtures, IReadOnlyList<CasePlanItem> plan)
    {
        Check.Equal(
            WireCanonical.CanonicalVersion,
            Str(fixtures.CasesManifest, "canonical_version"),
            "用例清单的规范算法版本与 C# 实现不一致");
        Check.Equal(
            WireCanonical.CanonicalVersion,
            Str(fixtures.CanonicalManifest, "canonical_version"),
            "规范清单的规范算法版本与 C# 实现不一致");

        // 同源不变式：清单里的每个用例、每个角色都要能在参考夹具里找到，反之亦然。
        Check.Equal(fixtures.SourceCases.Count, plan.Count, "用例数与参考夹具 cases/*.json 不一致");
        var expectedDocuments = fixtures.BaseDocuments.Count + plan.Sum(item => item.Documents.Count);
        var actualDocuments = Items(fixtures.CanonicalManifest, "documents").Count;
        Check.Equal(expectedDocuments, actualDocuments, "规范清单的文档条目数与参考夹具不一致");
        Check.Equal(
            plan.Sum(item => item.RawDocuments.Count),
            Items(fixtures.CanonicalManifest, "raw_documents").Count,
            "规范清单的原始文档条目数与参考夹具不一致");
        Check.True(
            plan.All(item => item.Documents.Count + item.RawDocuments.Count > 0),
            "存在既没有 documents 也没有 raw_documents 的用例");

        foreach (var item in plan)
        {
            foreach (var role in item.Documents.Select(document => document.Role))
            {
                Check.True(
                    role == "coverage" || DocRolePointers.ContainsKey(role),
                    $"用例 {item.Id} 的角色 '{role}' 没有 $defs 映射（DOC_ROLES 已变化？）");
            }
        }
    }

    // ------------------------------------------------------------------ 逐用例：canonical 字节

    private static void CheckCanonical(Fixtures fixtures, CasePlanItem item)
    {
        var problems = new List<string>();
        foreach (var (role, document) in item.Documents)
        {
            var key = $"{item.Id}/{role}";
            if (!fixtures.CanonicalById.TryGetValue(key, out var entry) || entry is not WireObject expected)
            {
                problems.Add($"{role}: 清单缺少规范条目");
                continue;
            }

            if (expected.ContainsKey("canonical_error"))
            {
                try
                {
                    _ = WireCanonical.Text(document);
                    problems.Add($"{role}: Python 侧预期规范化失败，C# 侧却成功");
                }
                catch (WireFaultException)
                {
                    // 两侧一致：都拒绝。
                }

                continue;
            }

            string text;
            byte[] payload;
            try
            {
                text = WireCanonical.Text(document);
                payload = new UTF8Encoding(false).GetBytes(text);
            }
            catch (WireFaultException fault)
            {
                problems.Add(
                    $"{role}: C# 规范化失败 {fault.RuleId}: {fault.Message}；Python 侧摘要 {Str(expected, "sha256")}");
                continue;
            }

            var expectedDigest = Str(expected, "sha256");
            var digest = WireCanonical.DigestBytes(payload);
            if (digest != expectedDigest)
            {
                problems.Add($"{role}: 摘要 {digest} != Python {expectedDigest}");
                continue;
            }

            if (expected.Get("text") is WireString inline && text != inline.Value)
                problems.Add($"{role}: 摘要相同但规范文本不同（清单内不一致）");
            else if (expected.Get("text_prefix") is WireString prefix &&
                     !text.StartsWith(prefix.Value, StringComparison.Ordinal))
                problems.Add($"{role}: 规范文本前缀与 Python 不一致");

            if (expected.Get("byte_length") is WireInteger length &&
                length.TryToLong(out var expectedLength) && expectedLength != payload.Length)
                problems.Add($"{role}: 规范字节长度 {payload.Length} != Python {expectedLength}");
        }

        Check.True(problems.Count == 0, string.Join("; ", problems));
    }

    // ------------------------------------------------------------------ 逐用例：L1 结构判定

    private static void CheckL1(Fixtures fixtures, CasePlanItem item)
    {
        var roles = item.Documents.Select(document => document.Role).OrderBy(role => role, StringComparer.Ordinal).ToList();
        var manifestRoles = item.ManifestRoles.OrderBy(role => role, StringComparer.Ordinal).ToList();
        Check.Sequence(manifestRoles, roles, "用例角色集合与清单不一致");

        if (!item.Layers.Contains("L1"))
        {
            Check.True(item.ManifestL1Findings.Count == 0, "清单未声明 L1，却记录了 L1 判定");
            return;
        }

        var findings = new List<string>();
        foreach (var (role, document) in item.Documents)
        {
            if (role == "coverage")
            {
                if (fixtures.CoverageSchema is { } coverageSchema)
                {
                    foreach (var message in coverageSchema.Validate(document, coverageSchema.Root))
                        findings.Add(role + StripRoot(message));
                }

                continue;
            }

            if (!DocRolePointers.TryGetValue(role, out var pointer))
                throw new CheckFailedException($"角色 '{role}' 没有 $defs 映射");
            var target = fixtures.WireSchema.Resolve(pointer);
            foreach (var message in fixtures.WireSchema.Validate(document, target))
                findings.Add(role + StripRoot(message));
        }

        Check.Sequence(item.ManifestL1Findings, findings, "L1 判定与 Python 参考不一致");
    }

    /// <summary>与 <c>check_contracts.py</c> 一致：把路径根 '$' 换成角色名前缀。</summary>
    private static string StripRoot(string message) => message.StartsWith('$') ? message[1..] : message;

    // ------------------------------------------------------------------ 逐用例：L0 原始文档

    private static void CheckRaw(Fixtures fixtures, CasePlanItem item)
    {
        foreach (var (role, raw) in item.RawDocuments)
        {
            var key = $"{item.Id}/{role}";
            if (!fixtures.RawById.TryGetValue(key, out var entry) || entry is not WireObject expected)
                throw new CheckFailedException($"清单缺少原始文档条目 {key}");

            var expectedError = expected.Get("parse_error") as WireObject;
            WireFaultException? fault = null;
            try
            {
                var parsed = WireParser.ParseStrict(raw, role);
                if (expectedError is not null)
                    throw new CheckFailedException(
                        $"raw {role}: Python 预期 {Str(expectedError, "rule_id")}，C# 侧解析成功");

                var digest = WireCanonical.Digest(parsed);
                Check.Equal(Str(expected, "sha256"), digest, $"raw {role}: 规范摘要与 Python 不一致");
                continue;
            }
            catch (WireFaultException thrown)
            {
                fault = thrown;
            }

            if (expectedError is null)
                throw new CheckFailedException(
                    $"raw {role}: C# 解析失败 {fault.RuleId}: {fault.Message}，Python 侧接受");
            Check.Equal(Str(expectedError, "rule_id"), fault.RuleId, $"raw {role}: L0 规则号与 Python 不一致");
        }
    }

    // ------------------------------------------------------------------ L4：seed 原语

    /// <summary>
    /// L4 语义层在 C# 侧只落了 seed 原语（<see cref="WireDecimal.Seed"/>）。用例筛选：
    /// 声明了 <c>request</c> 文档、payload 含 seed，且声明的规则号不超出 I-66/I-76
    /// （排除 I-11「能力不支持时不得携带 seed」这类非 seed 取值问题）。
    /// </summary>
    private static void CheckSeedPrimitive(IReadOnlyList<CasePlanItem> plan)
    {
        var problems = new List<string>();
        var compared = 0;
        foreach (var item in plan)
        {
            if (!item.RuleIds.All(rule => rule is "I-66" or "I-76")) continue;
            var request = item.Documents.FirstOrDefault(document => document.Role == "request");
            if (request.Document is not WireObject requestDocument) continue;
            if (requestDocument.Get("payload") is not WireObject payload) continue;
            if (!payload.TryGet("seed", out var seed)) continue;

            compared++;
            var accepted = true;
            try
            {
                _ = WireDecimal.Seed(seed);
            }
            catch (WireFaultException)
            {
                accepted = false;
            }

            if (accepted != (item.Expect == "accept"))
                problems.Add($"{item.Id}: seed 原语{(accepted ? "接受" : "拒绝")}，清单期望 {item.Expect}");
        }

        Check.True(compared >= 12, $"seed 相关对照片例只有 {compared} 个（清单与参考夹具不同源？）");
        Check.True(problems.Count == 0, string.Join("; ", problems));
    }

    // ------------------------------------------------------------------ golden 向量

    private static void AddGolden(Fixtures fixtures, List<(string Name, Action Body)> checks)
    {
        foreach (var vector in Items(fixtures.Golden, "canonical"))
        {
            var captured = vector;
            checks.Add(($"golden.canonical:{Str(captured, "id")}", () => CheckGoldenCanonical(captured)));
        }

        foreach (var vector in Items(fixtures.Golden, "canonical_reject"))
        {
            var captured = vector;
            checks.Add(($"golden.canonical-reject:{Str(captured, "id")}", () => CheckGoldenCanonicalReject(captured)));
        }

        foreach (var vector in Items(fixtures.Golden, "decimal"))
        {
            var captured = vector;
            checks.Add(($"golden.decimal:{Str(captured, "id")}", () => CheckGoldenDecimal(captured)));
        }

        foreach (var text in Strings(fixtures.Golden, "decimal_accept"))
        {
            var captured = text;
            checks.Add((
                $"golden.decimal-accept:{captured}",
                () => Check.True(WireDecimal.IsCanonicalDecimal(captured), $"应接受规范 decimal '{captured}'")));
        }

        foreach (var text in Strings(fixtures.Golden, "decimal_reject"))
        {
            var captured = text;
            checks.Add((
                $"golden.decimal-reject:{captured}",
                () => Check.False(WireDecimal.IsCanonicalDecimal(captured), $"应拒绝非规范 decimal '{captured}'")));
        }

        foreach (var vector in Items(fixtures.Golden, "action_key"))
        {
            var captured = vector;
            checks.Add(($"golden.action-key:{Str(captured, "id")}", () => CheckGoldenActionKey(captured)));
        }

        foreach (var vector in Items(fixtures.Golden, "identity"))
        {
            var captured = vector;
            checks.Add(($"golden.identity:{Str(captured, "id")}", () => CheckGoldenIdentity(captured)));
        }
    }

    private static void CheckGoldenCanonical(WireObject vector)
    {
        var document = vector.Get("document")
            ?? throw new CheckFailedException("向量缺少 document");
        Check.Equal(Str(vector, "expect_text"), WireCanonical.Text(document), "规范文本与独立工具不一致");
        Check.Equal(
            Str(vector, "expect_sha256"),
            WireCanonical.Digest(document),
            $"规范摘要与独立工具不一致（{vector.Get("digest_tool")}）");
    }

    private static void CheckGoldenCanonicalReject(WireObject vector)
    {
        var document = vector.Get("document")
            ?? throw new CheckFailedException("向量缺少 document");
        Check.Fault(() => _ = WireCanonical.Text(document), $"向量 {Str(vector, "id")} 应被规范化拒绝");
    }

    private static void CheckGoldenDecimal(WireObject vector) =>
        Check.Equal(
            Str(vector, "expect"),
            WireDecimal.Normalize(vector.Get("source") ?? throw new CheckFailedException("向量缺少 source")),
            $"decimal 规范化与 Python 参考不一致（源 {PythonRepr.Of(vector.Get("source")!)}）");

    private static void CheckGoldenActionKey(WireObject vector)
    {
        var actions = (vector.Get("actions") as WireArray ?? new WireArray([]))
            .Items
            .Select(item => item as WireObject ?? throw new CheckFailedException("action 不是对象"))
            .ToList();
        Check.True(actions.Count > 0, "向量缺少 actions");
        var keys = actions.Select(action => Convert.ToHexString(WireSort.ActionSemanticKey(action))).ToList();

        if (vector.ContainsKey("expect_equal"))
            Check.True(keys.Distinct(StringComparer.Ordinal).Count() == 1, $"动作语义键应相等: [{string.Join(",", keys)}]");
        if (vector.ContainsKey("expect_distinct"))
            Check.True(
                keys.Distinct(StringComparer.Ordinal).Count() == keys.Count,
                $"动作语义键本应互不相同: [{string.Join(",", keys)}]");
        if (vector.Get("expect_key_text") is WireString expected)
            Check.Equal(
                expected.Value,
                Encoding.UTF8.GetString(WireSort.ActionSemanticKey(actions[0])),
                "动作语义键文本与 Python 参考不一致");
    }

    private static void CheckGoldenIdentity(WireObject vector)
    {
        var kind = Str(vector, "kind");
        if (vector.ContainsKey("expect_stable"))
        {
            var first = IdentityDigest(vector, kind);
            var second = IdentityDigest(vector, kind);
            Check.True(
                first == second && first.StartsWith("sha256:", StringComparison.Ordinal),
                $"{kind} 身份摘要应可复现: {first}");
            return;
        }

        Check.Equal(Str(vector, "expect"), IdentityDigest(vector, kind), $"{kind} 身份摘要与 Python 参考不一致");
    }

    private static string IdentityDigest(WireObject vector, string kind) => kind switch
    {
        "catalog" => WireIdentity.CatalogDigest(IdentityInput(vector)),
        "ruleset" => WireIdentity.RulesetDigest(IdentityInput(vector)),
        "backend_profile" => WireIdentity.BackendProfileDigest(IdentityInput(vector)),
        "snapshot" => WireIdentity.SnapshotStorageDigest(
            vector.Get("input") ?? throw new CheckFailedException("identity 向量缺少 input")),
        "request" => WireIdentity.RequestPayloadFingerprint(
            Str(vector, "operation"),
            vector.Get("payload") ?? throw new CheckFailedException("request 向量缺少 payload")),
        _ => throw new CheckFailedException($"未知身份类型 '{kind}'"),
    };

    private static WireObject IdentityInput(WireObject vector) =>
        vector.Get("input") as WireObject ?? throw new CheckFailedException("identity 向量的 input 不是对象");

    // ------------------------------------------------------------------ 取值助手

    private static string Str(WireObject obj, string key) =>
        obj.Get(key) as WireString is { } text ? text.Value : throw new CheckFailedException($"字段 '{key}' 不是字符串");

    private static List<string> Strings(WireObject obj, string key)
    {
        var values = new List<string>();
        foreach (var item in RawItems(obj, key))
        {
            if (item is not WireString text) throw new CheckFailedException($"字段 '{key}' 的元素不是字符串");
            values.Add(text.Value);
        }

        return values;
    }

    private static List<WireObject> Items(WireObject obj, string key) => RawItems(obj, key).OfType<WireObject>().ToList();

    private static List<WireValue> RawItems(WireObject obj, string key) =>
        (obj.Get(key) as WireArray ?? new WireArray([])).Items.ToList();
}
