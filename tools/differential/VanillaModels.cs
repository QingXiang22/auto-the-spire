using System.Text.RegularExpressions;

namespace Differential;

/// <summary>
/// 原版分母里的一条内容（身份取自**原版源码类型**，不是本项目的内容 id）。
/// <para>
/// 身份规则照抄原版：<c>ModelDb.GetId(type)</c> = <c>new ModelId(GetCategory(type), GetEntry(type))</c>；
/// <c>GetCategory</c> 取"基类链上第一个直接派生自 <c>AbstractModel</c> 的类型名"，再走
/// <c>ModelId.SlugifyCategory</c>（<c>StringHelper.Slugify</c> 转 UPPER_SNAKE 后去掉 <c>_MODEL</c> 后缀）；
/// <c>GetEntry</c> = <c>StringHelper.Slugify(type.Name)</c>。
/// 因此一条原版内容的 id 形如 <c>CARD.ABRASIVE</c>（见 <c>sts2/src/Core/Models/ModelDb.cs:GetId</c>、
/// <c>ModelId.cs:SlugifyCategory</c>、<c>Helpers/StringHelper.cs:Slugify</c>）。
/// </para>
/// </summary>
internal sealed record VanillaEntry(
    string Category,
    string Entry,
    string TypeName,
    string File,
    int Line,
    string Scope,
    string? Reason,
    string? Evidence,
    string? Note)
{
    public string VanillaId => $"{Category}.{Entry}";

    /// <summary>本项目内容 id 的候选 kebab 形式（卡牌/能力的正式源链接用的就是这条规则）。</summary>
    public string Kebab => VanillaModels.Kebab(Entry);
}

/// <summary>抽取结果：文件/类型计数与逐条内容（含排除项——排除项同样登记，不静默丢弃）。</summary>
internal sealed record VanillaInventory(
    string Root,
    bool Exists,
    int FilesScanned,
    int TypesParsed,
    IReadOnlyList<VanillaEntry> Entries,
    IReadOnlyList<string> Warnings);

internal static class VanillaModels
{
    public const string RelativeRoot = "sts2/src/Core/Models";

    private static readonly Regex TypeDeclaration = new(
        @"^[ \t]*(?<mods>(?:(?:public|internal|private|protected|abstract|sealed|static|partial|readonly|file|unsafe|new)[ \t]+)*)"
        + @"(?<kind>class|record|struct|interface|enum)[ \t]+(?:(?:class|struct)[ \t]+)?"
        + @"(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<rest>[^\r\n]*)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex FirstIdentifier = new(@"[A-Za-z_][A-Za-z0-9_\.]*", RegexOptions.Compiled);

    // StringHelper.Slugify 的三个正则（原样照抄，保证 id 派生与原版一致）。
    private static readonly Regex CamelCase = new(@"([A-Za-z0-9]|\G(?!^))([A-Z])", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex SpecialChar = new(@"[^A-Z0-9_]", RegexOptions.Compiled);

    /// <summary>五个标准角色（<c>ModelDb.cs:123-130</c> 的 AllCharacters 固定五项）。</summary>
    private static readonly HashSet<string> StandardCharacters =
        new(["Ironclad", "Silent", "Regent", "Necrobinder", "Defect"], StringComparer.Ordinal);

    /// <summary>四幕（<c>sts2/src/Core/Models/Acts/</c> 里的标准四幕；Deprecated 不在此列）。</summary>
    private static readonly HashSet<string> StandardActs =
        new(["Overgrowth", "Underdocks", "Hive", "Glory"], StringComparer.Ordinal);

    public static VanillaInventory Extract(string root)
    {
        var full = Path.Combine(root, RelativeRoot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(full))
        {
            return new VanillaInventory(RelativeRoot, false, 0, 0, [], ["原版恢复源码目录不存在：" + RelativeRoot]);
        }

        var warnings = new List<string>();
        var files = Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar)
                .Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal)
            .ToList();

        var parsed = new List<ParsedType>();
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException failure)
            {
                warnings.Add($"{relative}: {failure.Message}");
                continue;
            }

            foreach (Match match in TypeDeclaration.Matches(text))
            {
                parsed.Add(new ParsedType(
                    match.Groups["name"].Value,
                    BaseName(match.Groups["rest"].Value),
                    match.Groups["mods"].Value.Contains("abstract", StringComparison.Ordinal),
                    relative,
                    1 + text.Take(match.Index).Count(character => character == '\n')));
            }
        }

        var byName = new Dictionary<string, ParsedType>(StringComparer.Ordinal);
        foreach (var declaration in parsed)
        {
            byName.TryAdd(declaration.Name, declaration);
        }

        var entries = new List<VanillaEntry>();
        foreach (var declaration in parsed)
        {
            var categoryType = ResolveCategoryType(declaration, byName);
            var category = categoryType is null ? "UNCATEGORIZED" : SlugifyCategory(categoryType);
            var (scope, reason, evidence, note) = ScopeOf(category, declaration, categoryType);
            entries.Add(new VanillaEntry(category, Slugify(declaration.Name), declaration.Name,
                declaration.File, declaration.Line, scope, reason, evidence, note));
        }

        // 同一类型可能在 partial/多文件里出现多次：按 (category, entry) 去重，保留首个声明位置。
        var deduplicated = entries
            .GroupBy(item => item.VanillaId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Entry, StringComparer.Ordinal)
            .ToList();

        return new VanillaInventory(RelativeRoot, true, files.Count, parsed.Count, deduplicated, warnings);
    }

    private sealed record ParsedType(string Name, string? BaseName, bool IsAbstract, string File, int Line);

    /// <summary>基类链上"直接派生自 AbstractModel"的那个类型（原版 <c>ModelDb.GetCategoryType</c>）。</summary>
    private static string? ResolveCategoryType(ParsedType declaration, IReadOnlyDictionary<string, ParsedType> byName)
    {
        var current = declaration;
        for (var depth = 0; depth < 64; depth++)
        {
            if (current.BaseName is null)
            {
                return null;
            }

            if (current.BaseName == "AbstractModel")
            {
                return current.Name;
            }

            if (!byName.TryGetValue(current.BaseName, out var next))
            {
                return null;
            }

            current = next;
        }

        return null;
    }

    /// <summary>从声明行里取基类名（取第一个标识符；泛型参数与命名空间限定都已处理）。</summary>
    private static string? BaseName(string rest)
    {
        // `where` 子句里也有冒号（`where T : class`），先截掉，避免把约束误当基类。
        var where = rest.IndexOf(" where ", StringComparison.Ordinal);
        if (where >= 0)
        {
            rest = rest[..where];
        }

        var depth = 0;
        var colon = -1;
        for (var index = 0; index < rest.Length; index++)
        {
            var character = rest[index];
            if (character == '(')
            {
                depth++;
            }
            else if (character == ')')
            {
                depth--;
            }
            else if (character == '{')
            {
                break;
            }
            else if (character == ':' && depth == 0)
            {
                colon = index;
                break;
            }
        }

        if (colon < 0)
        {
            return null;
        }

        var match = FirstIdentifier.Match(rest[(colon + 1)..]);
        if (!match.Success)
        {
            return null;
        }

        var name = match.Value;
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
    }

    /// <summary>范围判定：只有**有来源依据**的才排除；拿不准的留在分母里并标 <c>pending_source_check</c>。</summary>
    private static (string Scope, string? Reason, string? Evidence, string? Note) ScopeOf(
        string category, ParsedType declaration, string? categoryType)
    {
        if (declaration.File.Contains("/Mocks/", StringComparison.Ordinal)
            || declaration.Name.StartsWith("Mock", StringComparison.Ordinal))
        {
            return ("excluded", "mock_test_fixture",
                "sts2/src/Core/Models/**/Mocks/**（测试替身夹具）",
                "注意：ModelDb.AllPowers/AllPotions 用反射/池聚合，仍会把 mock power/potion 列进来——"
                + "这里的排除是范围决定，不是源码不可达结论；已列入下一批核对项。");
        }

        if (declaration.IsAbstract)
        {
            return ("excluded", "abstract_type_not_instantiable", "声明带 abstract 修饰符", null);
        }

        if (categoryType is null)
        {
            return ("excluded", "not_abstract_model_subtype", "基类链未到达 AbstractModel", null);
        }

        if (declaration.Name.StartsWith("Deprecated", StringComparison.Ordinal))
        {
            return ("pending_source_check", null, null,
                "废弃内容：是否在单人正常流程可达尚未核对，先保留在分母里，不无依据排除。");
        }

        return category switch
        {
            "BADGE" or "ACHIEVEMENT" => ("excluded", "achievement_or_badge_tracking",
                "docs/design/environment-completion-and-differential-v1.md §3（排除账号解锁/成就追踪）", null),
            "EXCEPTION" => ("excluded", "not_content_model", "异常类型，不是内容模型", null),
            "MODIFIER" => ("pending_source_check", null,
                "sts2/src/Core/Models/Modifiers/Draft.cs:11-14（重写牌组 + GenerateNeowOption）",
                "看起来是 run 修正/Neow 选项，但'是否属于标准单人正常流程'尚未按源码核对；"
                + "先保留在分母里，不无依据排除。"),
            "SINGLETON" => ("excluded", "multiplayer_only",
                "sts2/src/Core/Models/Singleton/MultiplayerScalingModel.cs（类名即 multiplayer 缩放）",
                "依据是类名与放置目录，已列入下一批核对项。"),
            "CHARACTER" => StandardCharacters.Contains(declaration.Name)
                ? ("in_scope", null, "sts2/src/Core/Models/ModelDb.cs:123-130（AllCharacters 固定五项）", null)
                : ("pending_source_check", null, null,
                    "不在 ModelDb.AllCharacters 里：是否单人正常可达待核对，先保留在分母里。"),
            "ACT" => StandardActs.Contains(declaration.Name)
                ? ("in_scope", null, "sts2/src/Core/Models/Acts/**（标准四幕）", null)
                : ("pending_source_check", null, null, "非标准幕：可达性待核对，先保留在分母里。"),
            _ => ("in_scope", null, null, null),
        };
    }

    /// <summary>原版 <c>StringHelper.Slugify</c>（CamelCase → UPPER_SNAKE，丢掉非 [A-Z0-9_]）。</summary>
    public static string Slugify(string text)
    {
        var camel = CamelCase.Replace(text.Trim(), "$1_$2");
        var upper = Whitespace.Replace(camel.ToUpperInvariant(), "_");
        return SpecialChar.Replace(upper, "");
    }

    /// <summary>原版 <c>ModelId.SlugifyCategory</c>（去掉 <c>_MODEL</c> 后缀）。</summary>
    public static string SlugifyCategory(string category)
    {
        var slug = Slugify(category);
        return slug.EndsWith("_MODEL", StringComparison.Ordinal) ? slug[..^"_MODEL".Length] : slug;
    }

    /// <summary>UPPER_SNAKE → kebab（本项目内容 id 用的形式，与 <c>SpireCardSource.ToId</c> 同规则）。</summary>
    public static string Kebab(string entry) => entry.ToLowerInvariant().Replace('_', '-');
}
