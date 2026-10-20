using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace NetTheSpire.Contracts;

/// <summary>
/// JSON Schema 2020-12 最小子集校验器，覆盖 <c>docs/contracts/net/0.2/wire.schema.json</c>
/// 与 <c>coverage.schema.json</c> 实际使用的关键字。与 Python 参考实现
/// <c>tools/contract-check/jsonschema_min.py</c> 逐分支对应：C# 端不另造协议形状，
/// 而是直接读同一份 schema 文件。
/// 支持：$ref（本地 #/$defs/*）、type、enum、const、required、properties、
/// additionalProperties、propertyNames、items、minItems、maxItems、uniqueItems、
/// minLength、maxLength、pattern、minimum、maximum、allOf、anyOf、oneOf、not、if/then/else。
/// </summary>
public sealed class LiteSchema
{
    private readonly WireObject _root;

    public LiteSchema(WireObject root) => _root = root;

    public WireObject Root => _root;

    public static LiteSchema FromJson(string text) =>
        new((WireObject)WireParser.ParseLenient(text, "schema"));

    public WireValue Resolve(string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
            throw new WireFaultException("schema-ref", $"只支持本地 $ref: {reference}");
        WireValue node = _root;
        foreach (var rawToken in reference[2..].Split('/'))
        {
            var token = rawToken.Replace("~1", "/").Replace("~0", "~");
            node = node switch
            {
                WireObject obj when obj.TryGet(token, out var member) => member,
                WireArray array => array.Items[int.Parse(token, CultureInfo.InvariantCulture)],
                _ => throw new WireFaultException("schema-ref", $"无法解析 $ref: {reference}"),
            };
        }

        return node;
    }

    /// <summary>返回错误消息列表；空列表表示通过（与 jsonschema_min.validate 同语义）。</summary>
    public List<string> Validate(WireValue instance, WireValue schema, WireValue? root = null, string path = "$")
    {
        var errors = new List<string>();
        Walk(instance, schema, root ?? _root, path, errors);
        return errors;
    }

    /// <summary>按 <c>$defs</c> 名字校验（供逐角色用例复用）。</summary>
    public List<string> ValidateDefinition(string definitionName, WireValue instance) =>
        Validate(instance, Resolve($"#/$defs/{definitionName}"));

    private void Walk(WireValue instance, WireValue schema, WireValue root, string path, List<string> errors)
    {
        if (schema is WireBool boolean)
        {
            if (!boolean.Value) errors.Add($"{path}: 被 false schema 拒绝");
            return;
        }

        if (schema is not WireObject obj) return;
        if (obj.Count == 0) return;

        if (obj.Get("$ref") is WireString reference)
        {
            Walk(instance, Resolve(reference.Value), root, path, errors);
            // 2020-12 允许 $ref 的兄弟关键字；本资产未使用，忽略以免误判。
        }

        if (obj.Get("type") is { } declared)
        {
            var names = declared is WireArray array
                ? array.Items.OfType<WireString>().Select(item => item.Value).ToList()
                : [(declared as WireString)?.Value ?? WireCanonical.Text(declared)];
            if (!names.Any(name => MatchesType(instance, name)))
                errors.Add($"{path}: 期望 {PythonRepr.Declared(declared)}，得到 {instance.TypeName}");
        }

        if (obj.TryGet("const", out var constant) && !WireEquals(instance, constant))
            errors.Add($"{path}: 期望常量 {PythonRepr.Of(constant)}");
        if (obj.Get("enum") is WireArray allowed && !allowed.Items.Any(item => WireEquals(instance, item)))
            errors.Add($"{path}: {PythonRepr.Of(instance)} 不在枚举 {PythonRepr.Of(allowed)}");

        if (obj.Get("allOf") is WireArray allOf)
        {
            foreach (var sub in allOf.Items) Walk(instance, sub, root, path, errors);
        }

        if (obj.TryGet("not", out var notSchema))
        {
            var subErrors = new List<string>();
            Walk(instance, notSchema, root, path, subErrors);
            if (subErrors.Count == 0) errors.Add($"{path}: 命中禁止分支 not");
        }

        if (obj.Get("anyOf") is WireArray anyOf)
        {
            var matched = false;
            foreach (var sub in anyOf.Items)
            {
                var subErrors = new List<string>();
                Walk(instance, sub, root, path, subErrors);
                if (subErrors.Count == 0)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched) errors.Add($"{path}: 不匹配任何 anyOf 分支");
        }

        if (obj.Get("oneOf") is WireArray oneOf)
        {
            var matches = 0;
            foreach (var sub in oneOf.Items)
            {
                var subErrors = new List<string>();
                Walk(instance, sub, root, path, subErrors);
                if (subErrors.Count == 0) matches++;
            }

            if (matches != 1) errors.Add($"{path}: oneOf 期望恰好一个分支匹配，实际 {matches}");
        }

        if (obj.TryGet("if", out var ifSchema))
        {
            var conditionErrors = new List<string>();
            Walk(instance, ifSchema, root, path, conditionErrors);
            var branch = conditionErrors.Count == 0 ? "then" : "else";
            if (obj.TryGet(branch, out var branchSchema)) Walk(instance, branchSchema, root, path, errors);
        }

        if (obj.Get("required") is WireArray required && instance is WireObject requiredTarget)
        {
            foreach (var key in required.Items.OfType<WireString>())
            {
                if (!requiredTarget.ContainsKey(key.Value)) errors.Add($"{path}: 缺少必需键 '{key.Value}'");
            }
        }

        if (instance is WireObject target)
        {
            var properties = obj.Get("properties") as WireObject ?? new WireObject([]);
            foreach (var member in properties.Members)
            {
                if (target.TryGet(member.Key, out var value))
                    Walk(value, member.Value, root, $"{path}.{member.Key}", errors);
            }

            if (obj.TryGet("additionalProperties", out var additional))
            {
                foreach (var member in target.Members)
                {
                    if (properties.ContainsKey(member.Key)) continue;
                    if (additional is WireBool allowedFlag)
                    {
                        if (!allowedFlag.Value)
                            errors.Add($"{path}: 未知键 '{member.Key}'（additionalProperties=false）");
                    }
                    else if (additional is WireObject)
                    {
                        Walk(member.Value, additional, root, $"{path}.{member.Key}", errors);
                    }
                }
            }

            if (obj.TryGet("propertyNames", out var propertyNames))
            {
                foreach (var member in target.Members)
                    Walk(
                        new WireString(member.Key),
                        propertyNames,
                        root,
                        $"{path}.<key {PythonRepr.Of(new WireString(member.Key))}>",
                        errors);
            }
        }

        if (instance is WireArray instanceArray)
        {
            if (obj.TryGet("items", out var items))
            {
                for (var index = 0; index < instanceArray.Items.Count; index++)
                    Walk(instanceArray.Items[index], items, root, $"{path}[{index}]", errors);
            }

            if (obj.Get("minItems") is WireInteger minItems && minItems.TryToLong(out var minimum) &&
                instanceArray.Items.Count < minimum)
                errors.Add($"{path}: 元素数 {instanceArray.Items.Count} 小于 minItems {minimum}");
            if (obj.Get("maxItems") is WireInteger maxItems && maxItems.TryToLong(out var maximum) &&
                instanceArray.Items.Count > maximum)
                errors.Add($"{path}: 元素数 {instanceArray.Items.Count} 大于 maxItems {maximum}");
            if (obj.Get("uniqueItems") is WireBool unique && unique.Value && !UniqueValues(instanceArray.Items))
                errors.Add($"{path}: uniqueItems 违反（存在重复元素）");
        }

        if (instance is WireString instanceString)
        {
            if (obj.Get("minLength") is WireInteger minLength && minLength.TryToLong(out var minimumLength) &&
                instanceString.Value.Length < minimumLength)
                errors.Add($"{path}: 长度小于 minLength {minimumLength}");
            if (obj.Get("maxLength") is WireInteger maxLength && maxLength.TryToLong(out var maximumLength) &&
                instanceString.Value.Length > maximumLength)
                errors.Add($"{path}: 长度大于 maxLength {maximumLength}");
            if (obj.Get("pattern") is WireString pattern && !Regex.IsMatch(instanceString.Value, pattern.Value))
                errors.Add($"{path}: 不匹配 pattern {PythonRepr.Of(pattern)}");
        }

        if (instance is WireInteger or WireFloatLiteral)
        {
            var instanceNumber = AsNumber(instance);
            if (obj.Get("minimum") is { } minimumNode && instanceNumber < AsNumber(minimumNode))
                errors.Add($"{path}: {Describe(instance)} 小于 minimum {PythonRepr.Declared(minimumNode)}");
            if (obj.Get("maximum") is { } maximumNode && instanceNumber > AsNumber(maximumNode))
                errors.Add($"{path}: {Describe(instance)} 大于 maximum {PythonRepr.Declared(maximumNode)}");
            if (obj.Get("exclusiveMinimum") is { } exclusiveMinimum && instanceNumber <= AsNumber(exclusiveMinimum))
                errors.Add(
                    $"{path}: {Describe(instance)} 不大于 exclusiveMinimum {PythonRepr.Declared(exclusiveMinimum)}");
            if (obj.Get("exclusiveMaximum") is { } exclusiveMaximum && instanceNumber >= AsNumber(exclusiveMaximum))
                errors.Add(
                    $"{path}: {Describe(instance)} 不小于 exclusiveMaximum {PythonRepr.Declared(exclusiveMaximum)}");
        }
    }

    private static bool MatchesType(WireValue instance, string name)
    {
        if (name == "number") return instance.Kind is WireKind.Integer or WireKind.FloatLiteral;
        if (name == "integer") return instance.Kind == WireKind.Integer;
        return instance.TypeName == name;
    }

    private static bool WireEquals(WireValue left, WireValue right)
    {
        if (left.Kind != right.Kind) return false;
        if (left.Kind == WireKind.FloatLiteral) return ((WireFloatLiteral)left).Raw == ((WireFloatLiteral)right).Raw;
        try
        {
            return WireCanonical.Text(left) == WireCanonical.Text(right);
        }
        catch (WireFaultException)
        {
            return false;
        }
    }

    private static bool UniqueValues(IReadOnlyList<WireValue> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            string key;
            try
            {
                key = WireCanonical.Text(item);
            }
            catch (WireFaultException)
            {
                key = Describe(item);
            }

            if (!seen.Add(key)) return false;
        }

        return true;
    }

    private static BigInteger AsNumber(WireValue value) => value switch
    {
        WireInteger integer => integer.ToBigInteger(),
        WireFloatLiteral literal => BigInteger.Parse(
            literal.Raw.Split('.', 'e', 'E')[0], CultureInfo.InvariantCulture),
        _ => BigInteger.Zero,
    };

    private static string Describe(WireValue value) => value switch
    {
        WireString text => $"'{text.Value}'",
        WireNull => "null",
        WireFloatLiteral literal => literal.Raw,
        _ => WireCanonical.Text(value),
    };
}
