using System.Globalization;
using NetTheSpire.Contracts;

namespace NetTheSpire.Tests;

/// <summary>
/// 参考夹具的文档叠加（<c>check_contracts.resolve_document</c> 的 C# 对应物）：
/// <c>$base</c> / <c>$set</c> / <c>$append</c> / <c>$insert</c> / <c>$move</c> / <c>$del</c>
/// 与嵌套 <c>"doc:&lt;name&gt;"</c> 引用。逐条对应 <c>tools/contract-check/check_contracts.py</c>
/// 第 98–174 行：叠加顺序（set → append → insert → move → del）、对象键的 dict 赋值语义、
/// 数组下标语义都与 Python 一致，以便两侧对同一用例得到同一份文档。
/// 这是同一份夹具的读取方式，不是第二套协议形状。
/// </summary>
internal static class DocumentComposer
{
    public static WireValue Resolve(WireValue spec, IReadOnlyDictionary<string, WireValue> bases)
    {
        if (spec is WireObject overlay && overlay.Get("$base") is WireString baseName)
        {
            if (!bases.TryGetValue(baseName.Value, out var document))
                throw new CheckFailedException($"$base 引用了不存在的基文档: {baseName.Value}");

            foreach (var (path, value) in Pairs(overlay, "$set"))
                document = Replace(document, Tokens(path), 0, value, path);
            foreach (var (path, value) in Pairs(overlay, "$append"))
                document = UpdateContainer(document, Tokens(path), 0, container => Append(container, path, value), path);
            foreach (var (path, insertSpec) in Pairs(overlay, "$insert"))
                document = UpdateContainer(document, Tokens(path), 0, container => Insert(container, path, insertSpec), path);
            foreach (var (path, moveSpec) in Pairs(overlay, "$move"))
                document = UpdateContainer(document, Tokens(path), 0, container => Move(container, path, moveSpec), path);
            if (overlay.Get("$del") is WireArray deletions)
            {
                foreach (var item in deletions.Items)
                {
                    if (item is not WireString path)
                        throw new CheckFailedException("$del 的元素必须是路径字符串");
                    document = Delete(document, Tokens(path.Value), 0);
                }
            }

            return ResolveNestedReferences(document, bases);
        }

        // Python 参考实现对字符串基文档不做嵌套引用解析（check_contracts.py 第 172 行），此处一致。
        if (spec is WireString reference && reference.Value.StartsWith("doc:", StringComparison.Ordinal))
        {
            if (!bases.TryGetValue(reference.Value[4..], out var document))
                throw new CheckFailedException($"doc: 引用了不存在的基文档: {reference.Value}");
            return document;
        }

        return ResolveNestedReferences(spec, bases);
    }

    private static IEnumerable<(string Path, WireValue Value)> Pairs(WireObject overlay, string key)
    {
        if (overlay.Get(key) is not WireObject table) yield break;
        foreach (var member in table.Members) yield return (member.Key, member.Value);
    }

    /// <summary>$set：最后一个路径段是被替换的键/下标，其余路径必须是已存在的容器链。</summary>
    private static WireValue Replace(WireValue node, string[] tokens, int index, WireValue value, string path)
    {
        var token = tokens[index];
        if (index == tokens.Length - 1) return WithChild(node, token, value, path);
        var child = Replace(Child(node, token, path), tokens, index + 1, value, path);
        return WithChild(node, token, child, path);
    }

    /// <summary>$del：与 Python 的 <c>_del_path</c> 一致，对象缺失键是静默 no-op。</summary>
    private static WireValue Delete(WireValue node, string[] tokens, int index)
    {
        var token = tokens[index];
        if (index == tokens.Length - 1) return WithoutChild(node, token);
        var child = Delete(Child(node, token, string.Join('.', tokens)), tokens, index + 1);
        return WithChild(node, token, child, string.Join('.', tokens));
    }

    /// <summary>$append / $insert / $move：最后一个路径段指向被修改的数组本身。</summary>
    private static WireValue UpdateContainer(
        WireValue node,
        string[] tokens,
        int index,
        Func<WireValue, WireValue> update,
        string path)
    {
        var token = tokens[index];
        var child = Child(node, token, path);
        var updated = index == tokens.Length - 1 ? update(child) : UpdateContainer(child, tokens, index + 1, update, path);
        return WithChild(node, token, updated, path);
    }

    private static WireValue Append(WireValue container, string path, WireValue value)
    {
        if (container is not WireArray array)
            throw new CheckFailedException($"$append 目标不是数组: {path}");
        return new WireArray(array.Items.Append(value));
    }

    private static WireValue Insert(WireValue container, string path, WireValue spec)
    {
        if (spec is not WireArray pair || pair.Items.Count != 2 ||
            pair.Items[0] is not WireInteger index || !index.TryToLong(out var position))
            throw new CheckFailedException($"$insert 的取值必须是 [下标, 值]: {path}");
        if (container is not WireArray array)
            throw new CheckFailedException($"$insert 目标不是数组: {path}");
        var items = array.Items.ToList();
        items.Insert((int)position, pair.Items[1]);
        return new WireArray(items);
    }

    private static WireValue Move(WireValue container, string path, WireValue spec)
    {
        if (spec is not WireArray pair || pair.Items.Count != 2 ||
            pair.Items[0] is not WireInteger source || pair.Items[1] is not WireInteger target ||
            !source.TryToLong(out var from) || !target.TryToLong(out var to))
            throw new CheckFailedException($"$move 的取值必须是 [源下标, 目标下标]: {path}");
        if (container is not WireArray array)
            throw new CheckFailedException($"$move 目标不是数组: {path}");
        var items = array.Items.ToList();
        var moved = items[(int)from];
        items.RemoveAt((int)from);
        items.Insert((int)to, moved);
        return new WireArray(items);
    }

    private static WireValue Child(WireValue node, string token, string path) => node switch
    {
        WireObject obj when obj.TryGet(token, out var value) => value,
        WireArray array => array.Items[TokenIndex(token, array, path)],
        _ => throw new CheckFailedException($"路径 '{path}' 在 '{token}' 处无法穿越（{node.TypeName}）"),
    };

    private static WireValue WithChild(WireValue node, string token, WireValue value, string path)
    {
        if (node is WireObject obj)
        {
            // Python dict 赋值语义：已存在的键保持原位置，新键追加在末尾。
            var members = obj.Members.ToList();
            for (var index = 0; index < members.Count; index++)
            {
                if (members[index].Key != token) continue;
                members[index] = new KeyValuePair<string, WireValue>(token, value);
                return new WireObject(members);
            }

            members.Add(new KeyValuePair<string, WireValue>(token, value));
            return new WireObject(members);
        }

        if (node is WireArray array)
        {
            var items = array.Items.ToList();
            items[TokenIndex(token, array, path)] = value;
            return new WireArray(items);
        }

        throw new CheckFailedException($"$set 目标 '{path}' 不是对象或数组（{node.TypeName}）");
    }

    private static WireValue WithoutChild(WireValue node, string token)
    {
        // Python 的 _del_path 对对象缺失键是静默 no-op（pop(key, None)）。
        if (node is WireObject obj)
            return obj.ContainsKey(token)
                ? new WireObject(obj.Members.Where(member => member.Key != token))
                : node;
        if (node is WireArray array)
        {
            var items = array.Items.ToList();
            items.RemoveAt(TokenIndex(token, array, string.Empty));
            return new WireArray(items);
        }

        throw new CheckFailedException($"$del 目标不是对象或数组（{node.TypeName}）");
    }

    private static int TokenIndex(string token, WireArray array, string path)
    {
        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
            index < 0 || index >= array.Items.Count)
            throw new CheckFailedException($"数组下标越界: '{token}'（路径 '{path}'，长度 {array.Items.Count}）");
        return index;
    }

    private static string[] Tokens(string path)
    {
        var tokens = path.Split('.');
        if (tokens.Length == 0 || tokens.Any(string.IsNullOrEmpty))
            throw new CheckFailedException($"非法路径: '{path}'");
        return tokens;
    }

    private static WireValue ResolveNestedReferences(WireValue node, IReadOnlyDictionary<string, WireValue> bases) => node switch
    {
        WireString text when text.Value.StartsWith("doc:", StringComparison.Ordinal) =>
            bases.TryGetValue(text.Value[4..], out var document)
                ? document
                : throw new CheckFailedException($"doc: 引用了不存在的基文档: {text.Value}"),
        WireObject obj => new WireObject(obj.Members.Select(member =>
            new KeyValuePair<string, WireValue>(member.Key, ResolveNestedReferences(member.Value, bases)))),
        WireArray array => new WireArray(array.Items.Select(item => ResolveNestedReferences(item, bases))),
        _ => node,
    };
}
