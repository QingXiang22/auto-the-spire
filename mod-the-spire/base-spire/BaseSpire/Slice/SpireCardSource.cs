using CoreTheSpire;
using BaseSpire.Slice.Effects;
using System.Collections.Immutable;

namespace BaseSpire.Slice;

/// <summary>
/// Formal card DSL frontend: the shared parser produces an AST, then this frontend preserves
/// ordered per-effect programs and card metadata in AuthorCard. CardEffectCompiler lowers those
/// programs to the existing Engine IR through CombatSlicePackage's bindings.
/// The diagnostic .author path retains its legacy step adapter; formal programs do not read
/// shared display parameters. Unsupported leaves, targets and references remain explicit errors.
/// Supported syntax is defined by the leaf and condition readers, not by parser acceptance alone.
/// </summary>
public static partial class SpireCardSource
{
    /// <summary>正式内容源的嵌入资源前缀/后缀（与 `BaseSpirePackage` 的枚举口径一致）。</summary>
    public const string ResourcePrefix = "BaseSpire.ContentSource.";

    public const string ResourceSuffix = "-cards.spire";

    private static readonly IReadOnlyList<string> SupportedKeywords =
        ["exhaust", "ethereal", "unplayable", "retain", "innate"];

    /// <summary>一个效果原语（`play` 块里的一个成员）在本批次支持的字段。</summary>
    public sealed record Primitive(
        string Name,
        int Amount,
        int Count,
        string Target,
        string PowerId,
        int Stacks,
        string AmountSource,
        int AmountBase,
        int AmountPerCount,
        /// <summary>直接对应某个**作者模型步骤**的原语（目前只有 `chooseCard`）：步骤名写在这里。</summary>
        string StepKind = "");

    /// <summary>一个变体（`normal` / `upgraded`）在本批次支持的字段。</summary>
    public sealed record Variant(
        string Type,
        string Rarity,
        int Cost,
        bool IsXCost,
        string TargetKind,
        IReadOnlyList<string> Keywords,
        IReadOnlyList<AuthorStep> Steps,
        int Amount,
        string AmountSource,
        int AmountBase,
        int AmountPerCount,
        string PowerId,
        int Stacks,
        AuthorHooks Hooks,
        /// <summary>旧条件摘要，仅供诊断；正式 Program 保留条件在效果序列中的实际位置。</summary>
        AuthorConditional? Conditional,
        /// <summary>
        /// ENV-02：本变体的**卡牌实例变量**初值（来源卡级
        /// `variables = { variable = { name = X default = N [upgraded = M] } }`，例如 Rampage 的 `ExtraDamage`）。
        /// 与 `.author` 路径共用同一份运行时机制（`Variable_&lt;name&gt;` 字段）。
        /// </summary>
        IReadOnlyDictionary<string, int> Variables,
        /// <summary>ENV-02：打出后**成长**的变量名（来源 `modifyCardVariable { variable = X }`；空＝不成长）。</summary>
        string GrowVariable,
        /// <summary>ENV-02：每次打出给成长变量加多少（来源 `modifyCardVariable { amount = N }`）。</summary>
        int GrowAmount)
    {
        public CardEffectAmount? DamageValue { get; init; }
        public CardEffectProgram Program { get; init; } = CardEffectProgram.Empty;
        public int StarCost { get; init; }
        public bool IsStarXCost { get; init; }
    }

    /// <summary>`damageValue = 7` / `{ scaled = { base, perCount, count = {…} } }` 的读数。</summary>
    private sealed record ScaledValue(string Source, int Base, int PerCount, bool IsScaled);

    public sealed record Declaration(
        string Name,
        string Id,
        int Line,
        bool Upgradeable,
        Variant Normal,
        Variant Upgraded)
    {
        public bool CostReductionPerAttack { get; init; }
        public bool CanGenerateInCombat { get; init; } = true;
        public string DamageVariable { get; init; } = "";

        /// <summary>
        /// ENV-T05：卡级 `pools = { IroncladCardPool }` 的池名（**源级**形式，与
        /// `sts2/src/Core/Models/CardPools/*CardPool.cs` 的类型名一致）。
        /// 这些名字决定这张牌进哪个池；奖励牌池的**成员资格**必须从冻结源清单与这里逐项对账，
        /// 不能按 `.spire` 文件的声明顺序或 id 排序猜。
        /// </summary>
        public IReadOnlyList<string> Pools { get; init; } = [];

        /// <summary>卡级 `multiplayerOnly = true`：源侧多人独占卡，单机池不含它。</summary>
        public bool MultiplayerOnly { get; init; }

        /// <summary>
        /// ENV-T05：卡级 `tags = { Strike }`。**作者写的名字原样保存**（可能已带包前缀，
        /// 例如 `"base-spire:strike"`）；补包前缀与链接校验在 `Lower` 里统一做。
        /// 标签是卡牌**定义**的属性，因此一张牌的全部实例共享它。
        /// </summary>
        public IReadOnlyList<string> Tags { get; init; } = [];
    }

    /// <summary>一份卡牌源的降级结果：**四态分开报**（声明 / 解析 / 降级 / 未支持原因）。</summary>
    public sealed record Source(
        string Name,
        IReadOnlyList<(string Name, int Line)> Declared,
        IReadOnlyList<Declaration> Parsed,
        IReadOnlyList<AuthorCard> Cards,
        IReadOnlyList<(string Name, string Reason)> Unsupported)
    {
        public int DeclaredCount => Declared.Count;

        public int ParsedCount => Parsed.Count;

        public int LoweredCount => Cards.Count;

        /// <summary>声明里的卡名（源级形式）。</summary>
        public IReadOnlyList<string> DeclaredNames => [.. Declared.Select(entry => entry.Name)];

        /// <summary>降级出来的 id（**源级**形式，不带包前缀）。</summary>
        public IReadOnlyList<string> LoweredIds =>
            [.. Cards.Select(card => card.Id.Contains(':') ? card.Id[(card.Id.IndexOf(':') + 1)..] : card.Id)];

        public string Summary =>
            $"{Name}: 声明 {DeclaredCount} / 解析 {ParsedCount} / 降级 {LoweredCount} / 未支持 {Unsupported.Count}";
    }

    /// <summary>正式源码里的 CamelCase 卡名 → 冻结清单使用的 kebab-case id。</summary>
    public static string ToId(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            var ch = name[index];
            if (char.IsUpper(ch) && index > 0)
            {
                var previous = name[index - 1];
                var next = index + 1 < name.Length ? name[index + 1] : '\0';
                // 只在"小写→大写"或"大写→大写+小写"的边界插连字符（StrikeIronclad → strike-ironclad）。
                if (char.IsLower(previous) || (char.IsUpper(previous) && char.IsLower(next)))
                {
                    builder.Append('-');
                }
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    /// <summary>本程序集里全部 `*-cards.spire` 正式卡牌源资源名（排序保证可复现）。</summary>
    public static IReadOnlyList<string> CardResources()
    {
        var assembly = typeof(SpireCardSource).Assembly;
        return
        [
            .. assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                    && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>从嵌入资源加载并降级（正式内容的**真实加载**入口）。</summary>
    public static Source LoadResource(string resourceName,
        IReadOnlyDictionary<string, string>? powers = null)
    {
        var assembly = typeof(SpireCardSource).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"缺少正式内容资源 '{resourceName}'");
        using var reader = new StreamReader(stream);
        var sourcePath = $"base-spire/{resourceName[ResourcePrefix.Length..]}";
        return Parse(reader.ReadToEnd(), sourcePath, powers);
    }

    public static Source Parse(string text, string sourceName,
        IReadOnlyDictionary<string, string>? powers = null) =>
        Lower(DslSourceParser.Parse(text, sourceName), sourceName, powers);

    /// <summary>
    /// 把正式 AST 降级成共享作者模型的卡声明。
    ///
    /// `powers` 是**链接**用的"正式能力名 → 已声明能力 id"表（例：`NoEnergyGainPower` →
    /// `base-spire:no-energy-gain`）：正式源里的 `power = X` 引用的是**能力名**，而运行时用的是 id，
    /// 两者的命名并不总是 kebab-case 一致（`NoEnergyGainPower` 的 id 是 `no-energy-gain`）。
    /// 链接不上时**记为未支持**，不猜一个 id。
    /// </summary>
    public static Source Lower(DslPackageNode package, string sourceName,
        IReadOnlyDictionary<string, string>? powers = null)
    {
        var versionNode = package.Members.OfType<DslPropertyNode>().SingleOrDefault(p => p.Name == "dsl_version");
        var version = versionNode is null ? "base/0.0.1" : NameOf(versionNode.Value);
        if (version is not ("base/0.0.1" or "base/0.0.2"))
            throw new DslSourceException(versionNode!.Location, $"unsupported card language '{version}'");
        var nodes = package.Members.OfType<DslDeclarationNode>()
            .Where(node => node.Kind == "card")
            .Select(node => CardSourceExpansion.Expand(node, version))
            .ToList();
        var declared = nodes.Select(node => (node.Name, node.Location.Line)).ToList();
        var parsed = new List<Declaration>();
        var lowered = new List<AuthorCard>();
        var unsupported = new List<(string, string)>();
        // ENV-T05：两遍走。第一遍只读声明，第二遍才降级——`cardsWithTag` 的引用要对整个源
        // 声明过的标签集合校验，因此集合必须先齐。
        var readProblems = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var problems = new List<string>();
            var declaration = ReadCard(node, ref problems);
            readProblems[node.Name] = problems;
            if (declaration is not null) parsed.Add(declaration);
        }

        var tagIds = new HashSet<string>(
            parsed.SelectMany(declaration => declaration.Tags).Select(raw => NamespaceTag(raw, package.Name)),
            StringComparer.Ordinal);
        // ENV-T05：`transformSelectedCard { card = GiantRock }` 的卡引用必须指向本包真的声明过的卡；
        // 与卡 id 同口径（`{packageId}:{ToId(name)}`）。卡表必须先齐，所以同样放在第二遍之前。
        var cardIds = new HashSet<string>(
            parsed.Select(declaration => declaration.Id.Contains(':') ? declaration.Id : $"{package.Name}:{declaration.Id}"),
            StringComparer.Ordinal);
        foreach (var declaration in parsed)
        {
            // 链接问题**始终**收集：读取阶段的问题（例如缺关键词）不该把"能力引用连不上"这类
            // 独立缺口吞掉——未支持原因要把两边的发现都摆出来，便于逐条对账。
            var problems = new List<string>(readProblems[declaration.Name]);
            var linkProblems = new List<string>();
            var card = Lower(declaration, package.Name, sourceName, powers, tagIds, cardIds, linkProblems);
            problems.AddRange(linkProblems);
            if (problems.Count > 0)
            {
                unsupported.Add((declaration.Name, string.Join("；", problems)));
                continue;
            }

            lowered.Add(card);
        }

        return new Source(sourceName, declared, parsed, lowered, unsupported);
    }

    /// <summary>
    /// ENV-T05：作者写的标签名 → 规范 id。已带包前缀的（`"base-spire:strike"`）原样保留，
    /// 局部名按当前包补前缀——与卡 id 的 `{packageId}:{ToId(name)}` 同一口径。
    /// </summary>
    private static string NamespaceTag(string raw, string packageId)
        => raw.Contains(':', StringComparison.Ordinal) ? raw : $"{packageId}:{ToId(raw)}";

    // -- AST 读取 -----------------------------------------------------------------------------------

    private static Declaration? ReadCard(DslDeclarationNode node, ref List<string> problems)
    {
        var upgradeable = true;
        DslBlockNode? normal = null;
        DslBlockNode? upgraded = null;
        ScaledValue? normalDamage = null;
        ScaledValue? upgradedDamage = null;
        var canGenerateInCombat = true;
        var costReductionPerAttack = false;
        var damageVariable = "";
        // ENV-T05：卡池成员资格与多人独占标记必须**读出来**（奖励牌池的排序与成员依据）。
        IReadOnlyList<string> pools = [];
        IReadOnlyList<string> tags = [];
        var multiplayerOnly = false;
        // ENV-02：卡级实例变量声明（`variables = { variable = { name/default/upgraded } }`）。
        IReadOnlyList<(string Name, int Normal, int Upgraded)> cardVariables = [];
        foreach (var member in node.Members.OfType<DslPropertyNode>())
        {
            switch (member.Name)
            {
                case "costReductionPerAttack":
                    costReductionPerAttack = member.Value is DslBooleanNode { Value: true };
                    break;
                case "damageVariable":
                    damageVariable = ToId(NameOf(member.Value));
                    break;
                case "upgradeable":
                    upgradeable = member.Value is not DslBooleanNode { Value: false };
                    break;
                case "multiplayerOnly":
                    multiplayerOnly = NameOf(member.Value) != "false";
                    break;
                case "pools":
                    // ENV-T05：池声明参与奖励牌池的成员对账（此前被静默丢弃）。
                    pools = [.. NamesOf(member.Value)];
                    break;
                case "tags":
                    // ENV-T05：标签参与 `count = { cardsWithTag = { tag = … } }` 的牌区计数（PerfectedStrike），
                    // 因此必须读出来。**原样保存作者写的名字**，包前缀由 `Lower` 统一补
                    // （与卡 id 的 `base-spire:` 前缀同一口径）。
                    tags = [.. NamesOf(member.Value)];
                    break;
                case "damageValue":
                    normalDamage = ScaledOf(member.Value, "damageValue", ref problems);
                    break;
                case "upgradedDamageValue":
                    upgradedDamage = ScaledOf(member.Value, "upgradedDamageValue", ref problems);
                    break;
                case "normal":
                    normal = member.Value as DslBlockNode;
                    break;
                case "upgraded":
                    upgraded = member.Value as DslBlockNode;
                    break;
                case "variables":
                    // ENV-02：`variables = { variable = { name = X default = N [upgraded = M]
                    // preserveOnDowngrade = true } }`（Rampage/Thrash）。
                    // `preserveOnDowngrade` 是 UI 的降级保留语义，不影响运行时可观察行为。
                    cardVariables = ParseCardVariables(member.Value, ref problems);
                    break;
                case "canGenerateInCombat":
                    // W-06：`canGenerateInCombat = false`（NotYet/Feed）是**卡牌生成**约束
                    // （"这张牌不参与战斗中的随机生成"），不改变打出时的行为；切片里没有战斗内生成路径，
                    // 因此这里显式记录为"不影响打出行为"而不是当成未支持。
                    canGenerateInCombat = NameOf(member.Value) != "false";
                    break;
                default:
                    problems.Add($"卡级属性 '{member.Name}' 未支持");
                    break;
            }
        }

        if (normal is null || upgraded is null)
        {
            problems.Add("缺少 normal/upgraded 块之一");
            return null;
        }

        var normalVariant = ReadVariant(normal, "normal", normalDamage, cardVariables, ref problems);
        var upgradedVariant = ReadVariant(upgraded, "upgraded", upgradedDamage, cardVariables, ref problems);
        return new Declaration(node.Name, ToId(node.Name), node.Location.Line, upgradeable,
            normalVariant, upgradedVariant)
        {
            CostReductionPerAttack = costReductionPerAttack,
            CanGenerateInCombat = canGenerateInCombat,
            DamageVariable = damageVariable,
            Pools = pools,
            MultiplayerOnly = multiplayerOnly,
            Tags = tags,
        };
    }

    /// <summary>
    /// 数值来源：标量（字面量）或 `{ scaled = { base, perCount, count = {…} } }`。
    /// 支持的 `count` 形态与冻结 `.author` 的 `amount-source` 词表对齐：
    /// `block` → `block-count`、`pileCount { pile = exhaust }` → `exhaust-count`、
    /// `pileCount { pile = hand, type = attack }` → `hand-count:Attack`。
    /// 其它形态（`targetPowerAmount` / `cardsWithTag` / `energyX` / `variable`）**记为未支持**。
    /// </summary>
    private static ScaledValue? ScaledOf(DslValueNode value, string where, ref List<string> problems)
    {
        if (value is DslNumberNode number && int.TryParse(number.Literal, out var literal))
        {
            return new ScaledValue("literal", literal, 0, false);
        }

        if (value is not DslBlockNode block)
        {
            problems.Add($"{where} 不是标量或 scaled 块");
            return null;
        }

        var scaled = block.Members.OfType<DslPropertyNode>()
            .FirstOrDefault(member => member.Name == "scaled")?.Value as DslBlockNode;
        if (scaled is null)
        {
            problems.Add($"{where} 不是 `scaled` 块（本批次只支持标量与 scaled）");
            return null;
        }

        var baseValue = 0;
        var perCount = 0;
        var source = string.Empty;
        foreach (var member in scaled.Members.OfType<DslPropertyNode>())
        {
            switch (member.Name)
            {
                case "base":
                    baseValue = IntOf(member.Value);
                    break;
                case "perCount":
                    perCount = IntOf(member.Value);
                    break;
                case "count":
                    source = CountSourceOf(member.Value, where, ref problems);
                    break;
                default:
                    problems.Add($"{where}.scaled 字段 '{member.Name}' 未支持");
                    break;
            }
        }

        if (source.Length == 0)
        {
            problems.Add($"{where}.scaled 缺少可识别的 count 来源");
            return null;
        }

        return new ScaledValue(source, baseValue, perCount, true);
    }

    /// <summary>`count = { block {…} | pileCount { pile = … [type = …] } }` → `amount-source` 文案。</summary>
    private static string CountSourceOf(DslValueNode value, string where, ref List<string> problems)
    {
        if (value is not DslBlockNode count)
        {
            problems.Add($"{where}.scaled.count 不是块");
            return string.Empty;
        }

        var kind = count.Members.OfType<DslPropertyNode>().FirstOrDefault();
        if (kind is null)
        {
            problems.Add($"{where}.scaled.count 为空");
            return string.Empty;
        }

        switch (kind.Name)
        {
            case "block":
                return "block-count";
            // ENV-02：`count = { cardVariable = { variable = X } }`（Rampage）
            // → 既有取值来源 `variable:X`（读**这张牌自己**的实例变量；是否声明由调用方校验）。
            case "cardVariable" when kind.Value is DslBlockNode variableBlock:
            {
                var variableName = variableBlock.Members.OfType<DslPropertyNode>()
                    .FirstOrDefault(field => field.Name == "variable") is { } variableField
                        ? NameOf(variableField.Value)
                        : string.Empty;
                if (variableName.Length == 0)
                {
                    problems.Add($"{where}.scaled.count.cardVariable 缺少 variable");
                    return string.Empty;
                }

                return $"{AmountSource.VariablePrefix}{SpireCardSource.ToId(variableName)}";
            }
            // W-06：`count = { targetPowerAmount = { power = X } }`（Dominate）——所选目标身上 X 的层数。
            case "targetPowerAmount" when kind.Value is DslBlockNode counted:
            {
                var countedName = counted.Members.OfType<DslPropertyNode>()
                    .FirstOrDefault(field => field.Name == "power") is { } powerField
                        ? NameOf(powerField.Value)
                        : string.Empty;
                if (countedName.Length == 0)
                {
                    problems.Add($"{where}.scaled.count.targetPowerAmount 缺少 power");
                    return string.Empty;
                }

                if (!SpirePowerSource.TryResolveId(countedName, out var countedId))
                {
                    problems.Add($"{where}.scaled.count.targetPowerAmount 的能力 '{countedName}' 无法链接");
                    return string.Empty;
                }

                return $"{AmountSource.TargetPowerCountPrefix}{countedId}";
            }
            case "pileCount" when kind.Value is DslBlockNode pile:
            {
                var pileName = string.Empty;
                var typeName = string.Empty;
                foreach (var field in pile.Members.OfType<DslPropertyNode>())
                {
                    switch (field.Name)
                    {
                        case "pile":
                            pileName = NameOf(field.Value);
                            break;
                        case "type":
                            typeName = NameOf(field.Value);
                            break;
                        default:
                            problems.Add($"{where}.scaled.count.pileCount 字段 '{field.Name}' 未支持");
                            break;
                    }
                }

                return (pileName, typeName) switch
                {
                    ("exhaust", "") => "exhaust-count",
                    // ENV-T05：`count = { pileCount = { pile = hand } }`（Stoke / FiendFire 的
                    // `withCount` 快照）= 整手张数。原版依据：两张卡都读 `Hand.Cards.Count`
                    // 作为「消耗整手」的张数，不含类型筛选。
                    ("hand", "") => "hand-count",
                    ("hand", "attack") => "hand-count:Attack",
                    _ => Unsupported($"{where}.scaled.count.pileCount ({pileName}/{typeName})", ref problems),
                };
            }
            // ENV-T05：`count = { cardsWithTag = { tag = Strike } }`（PerfectedStrike）
            // → `cards-with-tag:<标签名>`。**标签名原样带出**：包前缀由 `Lower` 补
            // （这里还不知道包名），与卡 id 的口径一致。
            case "cardsWithTag" when kind.Value is DslBlockNode tagged:
            {
                var tagName = tagged.Members.OfType<DslPropertyNode>()
                    .FirstOrDefault(field => field.Name == "tag") is { } tagField
                        ? NameOf(tagField.Value)
                        : string.Empty;
                if (tagName.Length == 0)
                {
                    problems.Add($"{where}.scaled.count.cardsWithTag 缺少 tag");
                    return string.Empty;
                }

                return $"{AmountSource.CardsWithTagPrefix}{tagName}";
            }
            // ENV-T05：`count = { playerHpLossCount = {} }`（TearAsunder）——战斗级历史计数。
            case "playerHpLossCount" when kind.Value is DslBlockNode:
                return AmountSource.PlayerHpLossCount;
            case "cardPlaysThisTurn" when kind.Value is DslBlockNode history:
            {
                var fields = history.Members.OfType<DslPropertyNode>().ToArray();
                foreach (var field in fields.Where(f => f.Name is not ("type" or "phase")))
                    problems.Add($"{field.Location}: unsupported cardPlaysThisTurn field '{field.Name}'");
                var type = fields.FirstOrDefault(f => f.Name == "type");
                var phase = fields.FirstOrDefault(f => f.Name == "phase");
                if (type is not null && phase is not null && NameOf(phase.Value) == "finished"
                    && NameOf(type.Value) is "attack" or "skill" or "power" or "status")
                {
                    var name = NameOf(type.Value);
                    return AmountSource.PlayedCountPrefix + char.ToUpperInvariant(name[0]) + name[1..];
                }
                return Unsupported($"{where}.cardPlaysThisTurn requires type and phase=finished", ref problems);
            }
            // ENV-T05：`count = { energyX = {} }` —— **已付的 X**（Cascade 的自动出牌张数）。
            // `cost = X` 本身**不再隐式改写数值叶**：X 只经作者显式写出的
            // `repeatForX`（次数＝已付 X）或这里的 `energyX` 进入效果。
            // 依据：Whirlwind 的源语义是「每次 5/8 点、命中 X 次」，X 只作用于次数。
            case "energyX" when kind.Value is DslBlockNode:
                return AmountSource.EnergyX;
            // ENV-T05：`count = { savedCount = { name = handSize } }`（Stoke / FiendFire）——
            // 读 `withCount` 进入时留下的词法快照。名字**原样**带出，词法绑定由
            // `WithCountCardEffect` 在编译期解析；名字拼错/不在作用域内会在链接时显式报错。
            case "savedCount" when kind.Value is DslBlockNode saved:
            {
                var savedName = saved.Members.OfType<DslPropertyNode>()
                    .FirstOrDefault(field => field.Name == "name") is { } nameField
                        ? NameOf(nameField.Value)
                        : string.Empty;
                if (savedName.Length == 0)
                {
                    problems.Add($"{where}.scaled.count.savedCount 缺少 name");
                    return string.Empty;
                }

                return $"{AmountSource.SavedCountPrefix}{savedName}";
            }
            default:
                return Unsupported($"{where}.scaled.count.{kind.Name}", ref problems);
        }

        static string Unsupported(string what, ref List<string> problems)
        {
            problems.Add($"数值来源 '{what}' 未支持（本批次：block / pileCount / pileCount hand type=attack / "
                + "cardsWithTag / playerHpLossCount / energyX / savedCount）");
            return string.Empty;
        }
    }

    private static int IntOf(DslValueNode value) =>
        value is DslNumberNode number && int.TryParse(number.Literal, out var parsed) ? parsed : 0;

    /// <summary>
    /// ENV-02：卡级 `variables` → 每个变量的 `(名字, 普通初值, 升级初值)`。
    /// 形状：`variables = { variable = { name = X default = N [upgraded = M] preserveOnDowngrade = true } }`
    /// （来源 `ironclad-cards.spire` 的 Rampage/Thrash）。
    /// 未给 `upgraded` 时升级形态沿用 `default`。
    /// </summary>
    private static IReadOnlyList<(string Name, int Normal, int Upgraded)> ParseCardVariables(DslValueNode value,
        ref List<string> problems)
    {
        var declared = new List<(string, int, int)>();
        if (value is not DslBlockNode block)
        {
            problems.Add("卡级 variables 不是块");
            return declared;
        }

        foreach (var member in block.Members.OfType<DslPropertyNode>())
        {
            if (member.Name != "variable" || member.Value is not DslBlockNode one)
            {
                problems.Add($"卡级 variables 的成员 '{member.Name}' 未支持（只支持 variable = {{ … }}）");
                continue;
            }

            var name = string.Empty;
            var initial = 0;
            var upgradedInitial = int.MinValue;
            foreach (var field in one.Members.OfType<DslPropertyNode>())
            {
                switch (field.Name)
                {
                    case "name":
                        name = NameOf(field.Value);
                        break;
                    case "default":
                        initial = IntOf(field.Value);
                        break;
                    case "upgraded":
                        upgradedInitial = IntOf(field.Value);
                        break;
                    case "preserveOnDowngrade":
                        // UI 的"降级时保留数值"语义：不影响运行时可观察行为，显式接受而不是当未支持。
                        break;
                    default:
                        problems.Add($"variables.variable 的字段 '{field.Name}' 未支持");
                        break;
                }
            }

            if (name.Length == 0)
            {
                problems.Add("variables.variable 缺少 name");
                continue;
            }

            // 变量名统一按 **kebab** 规范化：运行时字段是 `Variable_<name>`，而 .author 路径与
            // net 公开档案用的都是 kebab（`net:field:base-spire:Card.Variable_extra-damage`）。
            // 两条路径必须落在同一个字段名上，否则同一张牌的公开映射会分叉。
            declared.Add((SpireCardSource.ToId(name), initial,
                upgradedInitial == int.MinValue ? initial : upgradedInitial));
        }

        return declared;
    }

    private static Variant ReadVariant(DslBlockNode block, string variant, ScaledValue? cardDamage,
        IReadOnlyList<(string Name, int Normal, int Upgraded)> cardVariables,
        ref List<string> problems)
    {
        // 目标域**先**读一遍（不依赖成员顺序）：条件分支在"省略 target"时要按它解释。
        var targetMember = block.Members.OfType<DslPropertyNode>()
            .FirstOrDefault(member => member.Name == "target");
        var target = targetMember is null ? "none" : TargetOf(targetMember.Value, variant, ref problems);

        var type = "?";
        var rarity = "?";
        var cost = 0;
        var isXCost = false;
        var starCost = 0;
        var starXCost = false;
        var keywords = new List<string>();
        var primitives = new List<Primitive>();
        var steps = new List<AuthorStep>();
        var hooks = AuthorHooks.None;
        AuthorConditional? conditional = null;
        var program = CardEffectProgram.Empty;
        var conditionalAmount = 0;
        ScaledValue? conditionalScaling = null;
        // ENV-02：条件分支自己的原语（用于补齐卡级 `power`/`stacks`：运行时的 `apply_power` 读的是卡级字段）。
        IReadOnlyList<Primitive> conditionalPrimitives = [];
        var amount = 0;
        foreach (var member in block.Members.OfType<DslPropertyNode>())
        {
            switch (member.Name)
            {
                case "type":
                    type = NameOf(member.Value);
                    break;
                case "rarity":
                    rarity = NameOf(member.Value);
                    break;
                case "cost":
                    if (member.Value is DslNumberNode number && int.TryParse(number.Literal, out var parsedCost))
                    {
                        cost = parsedCost;
                    }
                    else if (NameOf(member.Value) is "X" or "x")
                    {
                        // X 费：`IsXCost`（运行时在出牌时把剩余能量写进这张牌的 Amount）。
                        isXCost = true;
                        cost = 0;
                    }
                    else
                    {
                        problems.Add($"{variant}.cost 不是整数字面量或 X");
                    }

                    break;
                case "target":
                    // 已在预读阶段处理（不依赖成员顺序）。
                    break;
                case "starCost":
                    if (NameOf(member.Value) is "X" or "x") starXCost = true;
                    else if (member.Value is DslNumberNode starNumber && int.TryParse(starNumber.Literal, out var parsedStars)
                        && parsedStars >= 0 && parsedStars <= 99999)
                        starCost = parsedStars;
                    else problems.Add($"{member.Location}: starCost requires an integer between 0 and 99999");
                    break;
                case "keywords":
                    keywords.AddRange(NamesOf(member.Value));
                    break;
                case "play":
                {
                    var effectiveX = block.Members.OfType<DslPropertyNode>().LastOrDefault(p => p.Name == "cost")
                        is { } costProperty && string.Equals(NameOf(costProperty.Value), "X", StringComparison.OrdinalIgnoreCase);
                    program = ReadEffectProgram(member.Value, variant + ".play", target, cardDamage, effectiveX,
                        ref problems, out primitives);
                    // Legacy summaries are retained for inspection only. The program is the executable authority.
                    var ifs = (member.Value as DslBlockNode)?.Members.OfType<DslPropertyNode>()
                        .Where(p => p.Name == "if").ToList() ?? [];
                    if (ifs.Count == 1)
                    {
                        var summaryProblems = new List<string>();
                        conditional = ReadConditional(ifs[0].Value, variant, target, ref summaryProblems,
                            out conditionalAmount, out conditionalScaling, out conditionalPrimitives);
                    }
                    break;
                }
                case "hooks":
                    hooks = ReadHooks(member.Value, variant, ref problems);
                    break;
                default:
                    problems.Add($"{variant} 字段 '{member.Name}' 未支持");
                    break;
            }
        }

        if (target == "none" && program.NeedsSelectedTarget)
            problems.Add($"{variant}.play: no-target card cannot read a selected target");

        // 目标步骤放在最前（与冻结 `.author` 编码一致：`[target, block, damage]`）；
        // 只有确实需要**选择**目标时才插入（`target = allEnemies` / `selectSelf` 都不需要提问）。
        // 原语**省略 `target`** 时按**卡牌自己的目标域**解释（正式源里 Tank 的 `applyPower` 就没写 target，
        // 而它的卡级目标是 `selectSelf`）：selectSelf ⇒ 打给自己（owner）、否则 ⇒ 打给所选目标。
        // `draw` / `loseHp` / `modifyEnergy` **不消费目标**，因此保持空（`loseHp` 的语义永远是"扣自己的 HP"）。
        primitives = [.. primitives.Select(primitive => primitive.Target.Length == 0
            && primitive.Name is not ("draw" or "loseHp" or "modifyEnergy")
            ? primitive with { Target = target == "self" ? "owner" : "target" }
            : primitive)];
        var needsTarget = primitives.Any(entry =>
            (entry.Name is "attack" or "scaledAttack" or "attackUsingBlock") && entry.Target != "allEnemies"
            || (entry.Name == "applyPower" && entry.Target == "target"));
        if (needsTarget)
        {
            steps.Add(new AuthorStep(AuthorSteps.Target, 1));
        }

        var powers = primitives.Where(entry => entry.Name == "applyPower").ToList();
        steps.AddRange(StepsFrom(primitives, powers.Count, ref problems));

        // 数值来源：原语自带 `scaled`/`base`+`perCount` 时以它为准（例：`scaledAttack`），
        // 否则回落到卡级 `damageValue`/`upgradedDamageValue`（例：`attackUsingBlock` 读 `damageValue`）。
        // **但 `applyScaledPower` 的来源不算数**：它由**步骤自己**在那一刻读取（那时目标已绑定），
        // 若把它当成卡级来源，就会在**选目标之前**求值（Dominate 曾因此报 owner None）。
        var scaling = primitives.FirstOrDefault(entry => entry.AmountSource.Length > 0
            && !(entry.Name == "applyPower"
                && entry.AmountSource.StartsWith(AmountSource.TargetPowerCountPrefix, StringComparison.Ordinal)))
            is { } scaledPrimitive
            ? new ScaledValue(scaledPrimitive.AmountSource, scaledPrimitive.AmountBase, scaledPrimitive.AmountPerCount, true)
            : cardDamage;
        if (scaling is null && primitives.Any(entry => entry.Name == "attackUsingBlock"))
        {
            // `attackUsingBlock` 但没有卡级 `damageValue`：按"伤害 = 当前格挡"读（base 0 / perCount 1）。
            scaling = new ScaledValue("block-count", 0, 1, true);
        }

        // 条件效果的变体：`play` 里没有"固定步骤"时，用分支的原语补齐
        // 变体级 `amount`/数值来源，否则条件分支读到的 Amount 会是 0（Spite 曾因此打出 0 点）。
        //
        // ENV-02：**有固定步骤时以步骤为准**。Mangle 是"攻击 + 尾部条件段"，若仍用分支的值覆盖，
        // 攻击会读到 0（实测 `normalAmount=0` ⇒ 整张卡变成 0 伤害、分支的能力也拿不到 id/层数）。
        if (conditional is not null && primitives.Count == 0)
        {
            amount = conditionalAmount;
            scaling ??= conditionalScaling;
        }

        var amountSource = scaling?.Source ?? "literal";
        var amountBase = scaling?.Base ?? 0;
        var amountPerCount = scaling?.PerCount ?? 0;

        // 变体级的 `amount` 是运行时 `damage`/`block` 步骤读的那个数（冻结 `.author` 的口径）：
        // 有攻击时取攻击数值（Bash 8 / IronWave 5 / Breakthrough 9），否则取格挡数值（Defend 5）。
        // 纯条件卡（没有固定步骤）走上面的分支补齐，不在这里覆盖。
        if (conditional is null || primitives.Count > 0)
        {
            amount = PrimaryAmountOf(primitives);
        }

        if (amountSource == "literal" && scaling is { IsScaled: false })
        {
            amount = scaling.Base;
        }

        // 只有一个 applyPower 时用**变体级** `power`/`stacks`（与冻结 `.author` 逐字一致）。
        //
        // ENV-02：运行时的 `apply_power` 步骤读的是**卡级** `power`/`stacks` 字段，因此当 applyPower
        // 只出现在**条件分支**里（Mangle：`if { targetAlive } then { applyPower(ManglePower, 10) }`）时，
        // 卡级字段必须由分支的原语补齐，否则步骤会施加一个空 id（实测：能力始终不出现）。
        // 这些字段仅为旧摘要；正式程序中的每次施加拥有独立的引用与层数。
        var stepPowers = primitives.Where(entry => entry.Name == "applyPower").ToList();
        var branchPowers = conditionalPrimitives.Where(entry => entry.Name == "applyPower").ToList();
        var allPowers = stepPowers.Concat(branchPowers).ToList();
        var powerId = allPowers.Count == 1 ? allPowers[0].PowerId : string.Empty;
        var stacks = allPowers.Count == 1 ? allPowers[0].Stacks : 0;

        // W-06：`applyScaledPower`（Dominate）——运行时读卡级 `power` 字段作为**被施加**的能力，
        // 步骤参数给**被计数**的能力。因此有两个 applyPower 原语时，卡级 `power` 取**标量那一个**。
        if (primitives.FirstOrDefault(entry => entry.Name == "applyPower"
            && entry.AmountSource.StartsWith(AmountSource.TargetPowerCountPrefix, StringComparison.Ordinal))
            is { } scaledApply)
        {
            powerId = scaledApply.PowerId;
            stacks = 0;
        }

        foreach (var keyword in keywords)
        {
            if (!SupportedKeywords.Contains(keyword, StringComparer.Ordinal))
            {
                problems.Add($"关键词 '{keyword}' 未支持");
            }
        }

        if (keywords.Contains("exhaust", StringComparer.Ordinal))
        {
            // `keywords = { exhaust }` ⇔ 打出后进消耗区（与 `.author` 的 `exhaust_self` 步骤同一语义）。
            steps.Add(new AuthorStep(AuthorSteps.ExhaustSelf, 1));
        }

        // onExhaust may be caused by another card or by a result-pile rule.

        // ENV-02：本变体的实例变量初值（升级形态取 `upgraded`，没写就沿用 `default`）。
        var variantVariables = cardVariables.ToDictionary(
            entry => entry.Name,
            entry => variant == "upgraded" ? entry.Upgraded : entry.Normal,
            StringComparer.Ordinal);
        // ENV-02：`modifyCardVariable = { variable = X, amount = N }` → 打出后给该变量 +N
        //（既有机制：`AuthorCard.NormalGrowVariable/NormalGrowAmount` + 运行时 `Grow(...)`）。
        var growVariable = string.Empty;
        var growAmount = 0;
        if (block.Members.OfType<DslPropertyNode>().FirstOrDefault(member => member.Name == "play")
                is { Value: DslBlockNode growFromBlock })
        {
            foreach (var modifier in growFromBlock.Members.OfType<DslPropertyNode>()
                         .Where(member => member.Name == "modifyCardVariable"))
            {
                if (modifier.Value is not DslBlockNode modifyBlock)
                {
                    problems.Add($"{variant}.play.modifyCardVariable 不是块");
                    continue;
                }

                var growName = modifyBlock.Members.OfType<DslPropertyNode>()
                    .FirstOrDefault(field => field.Name == "variable") is { } variableField
                        ? SpireCardSource.ToId(NameOf(variableField.Value))
                        : string.Empty;
                var delta = modifyBlock.Members.OfType<DslPropertyNode>()
                    .FirstOrDefault(field => field.Name == "amount") is { } amountField
                        ? IntOf(amountField.Value)
                        : 0;
                if (growName.Length == 0)
                {
                    problems.Add($"{variant}.play.modifyCardVariable 缺少 variable");
                    continue;
                }

                if (!variantVariables.ContainsKey(growName))
                {
                    problems.Add($"{variant}.play.modifyCardVariable 的变量 '{growName}' 未在卡级 variables 里声明");
                    continue;
                }

                growVariable = growName;
                growAmount = delta;
            }
        }

        // ENV-02：`cardVariable` 取值来源必须指向**已声明**的变量，否则运行时会读到不存在的字段。
        if (amountSource.StartsWith(AmountSource.VariablePrefix, StringComparison.Ordinal)
            && !variantVariables.ContainsKey(amountSource[AmountSource.VariablePrefix.Length..]))
        {
            problems.Add($"{variant}.play 引用了未声明的实例变量"
                + $" '{amountSource[AmountSource.VariablePrefix.Length..]}'");
        }

        problems.AddRange(CardEffectProgramValidator.Validate(program, starXCost));
        ValidateEffectVariables(program, variantVariables, ref problems);
        foreach (var hook in hooks.Programs?.Values ?? [])
        {
            problems.AddRange(CardEffectProgramValidator.Validate(hook));
            ValidateEffectVariables(hook, variantVariables, ref problems);
        }
        CardEffectAmount? DamageIn(CardEffectProgram body)
        {
            foreach (var effect in body.Effects)
            {
                var value = effect switch
                {
                    PrimitiveCardEffect { Operation: CardEffectOperation.Damage } hit => hit.Amount,
                    RepeatCardEffect repeat => DamageIn(repeat.Body),
                    DynamicRepeatCardEffect repeat => DamageIn(repeat.Body),
                    _ => null,
                };
                if (value is not null) return value;
            }
            return null;
        }
        var damageValue = cardDamage is null ? DamageIn(program)
            : CardEffectAmount.FromAuthor(cardDamage.Source, cardDamage.Base, cardDamage.PerCount);
        return new Variant(type, rarity, cost, isXCost, target, keywords, steps, amount, amountSource,
            amountBase, amountPerCount, powerId, stacks, hooks, conditional, variantVariables,
            growVariable, growAmount) { Program = program, DamageValue = damageValue, StarCost = starCost, IsStarXCost = starXCost };
    }

    /// <summary>
    /// 读 `if = { condition = { <条件> = { … } } then = { … } [else = { … }] }` → <see cref="AuthorConditional"/>。
    ///
    /// 条件映射（正式名 → 运行时谓词）：
    /// `playerLostHpThisTurn` → `player-lost-hp-this-turn`、
    /// `cardExhaustedThisTurn` → `card-exhausted-this-turn`、
    /// `hasPower { power = X target = context.target }` → `target-has-power:<能力 id>`、
    /// `pileCountAtLeast { pile = P count = N }` → `pile-count-at-least:<P>:<N>`。
    /// 其它条件（`targetAlive` / `selectedCardIsClone` / …）**记为未支持**（运行时判不出来就不声明）。
    /// </summary>
    private static AuthorConditional? ReadConditional(DslValueNode value, string variant,
        string cardTarget, ref List<string> problems, out int amount, out ScaledValue? scaling,
        out IReadOnlyList<Primitive> branchPrimitives)
    {
        amount = 0;
        scaling = null;
        branchPrimitives = [];
        if (value is not DslBlockNode block)
        {
            problems.Add($"{variant}.play.if 不是块");
            return null;
        }

        DslPropertyNode? conditionNode = null;
        DslBlockNode? thenBlock = null;
        DslBlockNode? elseBlock = null;
        foreach (var member in block.Members.OfType<DslPropertyNode>())
        {
            switch (member.Name)
            {
                case "condition":
                    conditionNode = member;
                    break;
                case "then":
                    thenBlock = member.Value as DslBlockNode;
                    break;
                case "else":
                    elseBlock = member.Value as DslBlockNode;
                    break;
                default:
                    problems.Add($"{variant}.play.if 字段 '{member.Name}' 未支持");
                    break;
            }
        }

        if (conditionNode is null || thenBlock is null)
        {
            problems.Add($"{variant}.play.if 缺少 condition 或 then");
            return null;
        }

        var condition = ConditionOf(conditionNode.Value, variant, cardTarget, ref problems);
        if (condition.Length == 0)
        {
            return null;
        }

        // ENV-02：分支块里可以再有一个**尾部条件段**（MoltenFist：`if targetAlive then { if hasPower … }`）。
        // 与卡片级同一规则：`if` 必须是该块里**唯一且最后**的成员，剩下的成员才是这一支的固定步骤。
        // 嵌套子树报告的原语要**一路带上来**：卡级 `power`/`stacks` 只由这里的列表补齐，
        // 丢掉嵌套的原语会让卡级 `power` 为空（MoltenFist 曾因此施加一个空 id）。
        var (thenOwn, thenNested, thenNestedPrimitives) = SplitTrailingConditional(thenBlock,
            $"{variant}.play.if.then", cardTarget, ref problems);
        var (elseOwn, elseNested, elseNestedPrimitives) = elseBlock is null
            ? ([], (AuthorConditional?)null, (IReadOnlyList<Primitive>)[])
            : SplitTrailingConditional(elseBlock, $"{variant}.play.if.else", cardTarget, ref problems);

        // 变体级 `amount`/数值来源要覆盖**条件分支里的原语**（含嵌套分支）：否则分支读到的 Amount 会是 0
        // （条件卡片的 `play` 里没有"固定步骤"，原语都在 then/else 里）。
        var withConditional = thenOwn.Concat(elseOwn)
            .Concat(thenNestedPrimitives)
            .Concat(elseNestedPrimitives)
            .ToList();
        amount = PrimaryAmountOf(withConditional);
        scaling = ScalingOf(withConditional);

        var thenSteps = ConditionalBranch(thenOwn, cardTarget, $"{variant}.play.if.then", ref problems);
        // 分支原语（then ∪ else ∪ 嵌套子树）回报给调用方：用来补齐**卡级** `power`/`stacks`。
        branchPrimitives = withConditional;
        var elseSteps = elseOwn.Count == 0
            ? []
            : ConditionalBranch(elseOwn, cardTarget, $"{variant}.play.if.else", ref problems);
        return new AuthorConditional(condition, [.. thenSteps], [.. elseSteps], thenNested, elseNested);
    }

    /// <summary>
    /// 把一个分支块拆成"自己的固定步骤原语" + "尾部嵌套条件段" + "嵌套子树里的全部原语（含更深层）"。
    /// `if` 必须是该块里**唯一且最后**的成员（与卡片级同一规则），否则记未支持。
    /// </summary>
    private static (IReadOnlyList<Primitive> Own, AuthorConditional? Nested, IReadOnlyList<Primitive> NestedPrimitives)
        SplitTrailingConditional(DslBlockNode block, string where, string cardTarget, ref List<string> problems)
    {
        var members = block.Members.OfType<DslPropertyNode>().ToList();
        var ifs = members.Where(member => member.Name == "if").ToList();
        if (ifs.Count == 0)
        {
            return (PrimitivesOf(block, where, ref problems), null, []);
        }

        if (ifs.Count > 1 || !ReferenceEquals(ifs[0], members[^1]))
        {
            problems.Add($"{where} 的 if 只支持**一个且位于最后**");
            return ([], null, []);
        }

        var own = members.Count > 1
            ? PrimitivesOf(block, where, ref problems, allowIf: true)
            : [];
        var nested = ReadConditional(ifs[0].Value, where, cardTarget, ref problems,
            out _, out _, out var nestedPrimitives);
        return (own, nested, nestedPrimitives);
    }

    /// <summary>条件分支的步骤：先按卡牌目标域补齐省略的 `target`，需要提问时把 `target` 步骤插在最前。</summary>
    private static IReadOnlyList<AuthorStep> ConditionalBranch(IReadOnlyList<Primitive> primitives,
        string cardTarget, string where, ref List<string> problems)
    {
        var (prepared, needsTarget) = PrepareSteps(primitives, cardTarget, ref problems);
        return needsTarget ? [new AuthorStep(AuthorSteps.Target, 1), .. prepared] : prepared;
    }

    /// <summary>变体级 `amount`：有攻击取攻击数值，否则取格挡数值（冻结 `.author` 的口径）。</summary>
    private static int PrimaryAmountOf(IReadOnlyList<Primitive> primitives) =>
        primitives.FirstOrDefault(entry =>
            entry.Name is "attack" or "scaledAttack" or "attackUsingBlock" or "randomAttack")?.Amount
        ?? primitives.FirstOrDefault(entry => entry.Name is "gainBlock" or "gainBlockFromCurrentBlock")?.Amount
        ?? 0;

    /// <summary>变体级数值来源：原语自带 scaled 时以它为准。</summary>
    private static ScaledValue? ScalingOf(IReadOnlyList<Primitive> primitives) =>
        primitives.FirstOrDefault(entry => entry.AmountSource.Length > 0) is { } scaled
            ? new ScaledValue(scaled.AmountSource, scaled.AmountBase, scaled.AmountPerCount, true)
            : null;

    /// <summary>
    /// `randomMoveCard = { from = hand, to = exhaust }` → `exhaust_random_from_hand`。
    /// 其它组合（别的源/目的牌区）记为未支持。
    /// </summary>
    private static Primitive? RandomMoveCardStep(DslValueNode value, string where, ref List<string> problems)
    {
        if (value is not DslBlockNode block)
        {
            problems.Add($"{where} 不是块");
            return null;
        }

        var from = string.Empty;
        var to = string.Empty;
        foreach (var field in block.Members.OfType<DslPropertyNode>())
        {
            switch (field.Name)
            {
                case "from":
                    from = NameOf(field.Value);
                    break;
                case "to":
                    to = NameOf(field.Value);
                    break;
                default:
                    problems.Add($"{where} 字段 '{field.Name}' 未支持");
                    break;
            }
        }

        if (from != "hand" || to != "exhaust")
        {
            problems.Add($"{where} 形态未支持（只支持 from=hand, to=exhaust）");
            return null;
        }

        return new Primitive("choose-card", 0, 1, string.Empty, string.Empty, 0, string.Empty, 0, 0,
            AuthorSteps.ExhaustRandomFromHand);
    }

    /// <summary>
    /// `chooseCard = { from = …, to = … }` → 作者模型步骤。本批次支持两种形态：
    /// `from = discard, to = draw, destinationPosition = top` → `top_from_discard`；
    /// `from = hand, to = exhaust` → `exhaust_from_hand`。其它形态记为未支持。
    /// </summary>
    private static Primitive? ChooseCardStep(DslValueNode value, string where, ref List<string> problems)
    {
        if (value is not DslBlockNode block)
        {
            problems.Add($"{where} 不是块");
            return null;
        }

        var from = string.Empty;
        var to = string.Empty;
        var destinationPosition = string.Empty;
        var filter = string.Empty;
        var onSelected = string.Empty;
        foreach (var field in block.Members.OfType<DslPropertyNode>())
        {
            switch (field.Name)
            {
                case "from":
                    from = NameOf(field.Value);
                    break;
                case "to":
                    to = NameOf(field.Value);
                    break;
                case "destinationPosition":
                    destinationPosition = NameOf(field.Value);
                    break;
                case "filter":
                    filter = NameOf(field.Value);
                    break;
                case "onSelected":
                    // `onSelected = { upgradeSelectedCard = {} }` → 取里面唯一的调用名。
                    onSelected = field.Value is DslBlockNode onBlock
                        ? onBlock.Members.OfType<DslPropertyNode>().FirstOrDefault()?.Name ?? string.Empty
                        : string.Empty;
                    break;
                case "prompt":
                    break;
                default:
                    problems.Add($"{where} 字段 '{field.Name}' 未支持");
                    break;
            }
        }

        // W-06：`to = none` + `onSelected = upgradeSelectedCard` + `filter = upgradeable` ⇒ 选择并升级一张手牌。
        if (to == "none" && onSelected == "upgradeSelectedCard")
        {
            if (filter != "upgradeable")
            {
                problems.Add($"{where} 的 filter '{filter}' 未支持（只支持 upgradeable）");
                return null;
            }

            if (from != "hand")
            {
                problems.Add($"{where} 的 from '{from}' 未支持（升级只支持 from=hand）");
                return null;
            }

            return new Primitive("choose-card", 0, 1, string.Empty, string.Empty, 0, string.Empty, 0, 0,
                AuthorSteps.UpgradeChooseFromHand);
        }

        var step = (from, to, destinationPosition) switch
        {
            ("discard", "draw", "top") => AuthorSteps.TopFromDiscard,
            ("hand", "exhaust", _) => AuthorSteps.ExhaustFromHand,
            _ => string.Empty,
        };
        if (step.Length == 0)
        {
            problems.Add($"{where} 形态未支持（只支持 from=discard/to=draw/top 与 from=hand/to=exhaust）");
            return null;
        }

        return new Primitive("choose-card", 0, 1, string.Empty, string.Empty, 0, string.Empty, 0, 0, step);
    }

    /// <summary>
    /// ENV-T05：`cloneCard = { pile = discard }` → `clone_card_to_discard`。
    ///
    /// 这是原版 `CardModel.CreateClone`（`sts2/src/Core/Models/CardModel.cs`）在切片里的**受限叶**：源固定是
    /// **正在打出的这张牌**（不写 `source`，也不按 Anger 的 id 硬编码行为），目的地只支持弃牌堆。
    /// 数值、目标、批次都不属于这个叶的语义，因此任何未建模的字段/牌区都**带源位置**明确失败——不做字符串
    /// 泛化，也不静默降级成别的步骤。
    /// </summary>
    private static Primitive? CloneCardStep(DslValueNode value, string where, ref List<string> problems)
    {
        if (value is not DslBlockNode block)
        {
            problems.Add($"{where} 不是块（只支持 cloneCard = {{ pile = discard }}）");
            return null;
        }

        var pile = string.Empty;
        var pileSeen = false;
        foreach (var member in block.Members)
        {
            if (member is not DslPropertyNode field)
            {
                problems.Add($"{where} {member.Location}: 嵌套声明未支持（只支持 pile = discard）");
                continue;
            }
            if (field.Name != "pile")
            {
                problems.Add($"{where} 字段 '{field.Name}' 未支持（只支持 pile = discard）");
                continue;
            }

            if (pileSeen)
            {
                problems.Add($"{where} {field.Location}: 重复 pile（只允许一个目的牌区）");
                continue;
            }
            pile = NameOf(field.Value);
            pileSeen = true;
        }

        if (!pileSeen)
        {
            problems.Add($"{where} 缺少 pile（只支持 cloneCard = {{ pile = discard }}）");
            return null;
        }

        if (pile != "discard")
        {
            problems.Add($"{where}.pile 的牌区 '{pile}' 未支持（只支持 discard）");
            return null;
        }

        return new Primitive("clone-card", 0, 1, string.Empty, string.Empty, 0, string.Empty, 0, 0,
            AuthorSteps.CloneCardToDiscard);
    }

    /// <summary>
    /// 条件块 → 运行时谓词字符串（判不出来的条件记为未支持）。</summary>
    private static string ConditionOf(DslValueNode value, string variant, string cardTarget,
        ref List<string> problems)
    {
        if (value is not DslBlockNode block)
        {
            problems.Add($"{variant}.play.if.condition 不是块");
            return string.Empty;
        }

        var condition = block.Members.OfType<DslPropertyNode>().FirstOrDefault();
        if (condition is null)
        {
            problems.Add($"{variant}.play.if.condition 为空");
            return string.Empty;
        }

        switch (condition.Name)
        {
            case "playerLostHpThisTurn":
                return AuthorCondition.PlayerLostHpThisTurn;
            case "cardExhaustedThisTurn":
                return AuthorCondition.CardExhaustedThisTurn;
            case "targetAlive":
                // ENV-02（第二次尝试）：`targetAlive = {}`——只接受**单个敌人目标**。
                if (!cardTarget.StartsWith("one-opponent", StringComparison.Ordinal))
                {
                    problems.Add($"{variant}.play.if.condition.targetAlive 只支持单个敌人目标"
                        + $"（这张牌的目标是 '{cardTarget}'）");
                    return string.Empty;
                }

                return AuthorCondition.TargetAlive;
            case "hasPower":
            {
                if (condition.Value is not DslBlockNode powerBlock)
                {
                    problems.Add($"{variant}.play.if.condition.hasPower 不是块");
                    return string.Empty;
                }

                var powerName = string.Empty;
                var target = string.Empty;
                foreach (var field in powerBlock.Members.OfType<DslPropertyNode>())
                {
                    switch (field.Name)
                    {
                        case "power":
                            powerName = NameOf(field.Value);
                            break;
                        case "target":
                            target = NameOf(field.Value);
                            break;
                        default:
                            problems.Add($"{variant}.play.if.condition.hasPower 字段 '{field.Name}' 未支持");
                            break;
                    }
                }

                if (target != "context.target")
                {
                    problems.Add($"{variant}.play.if.condition.hasPower 的目标 '{target}' 未支持"
                        + "（只支持 context.target）");
                    return string.Empty;
                }

                if (!SpirePowerSource.TryResolveId(powerName, out var powerId))
                {
                    problems.Add($"{variant}.play.if.condition.hasPower 的能力 '{powerName}' 无法链接");
                    return string.Empty;
                }

                return $"{AuthorCondition.TargetHasPowerPrefix}{powerId}";
            }
            case "pileCountAtLeast":
            {
                if (condition.Value is not DslBlockNode pileBlock)
                {
                    problems.Add($"{variant}.play.if.condition.pileCountAtLeast 不是块");
                    return string.Empty;
                }

                var pile = string.Empty;
                var count = 0;
                foreach (var field in pileBlock.Members.OfType<DslPropertyNode>())
                {
                    switch (field.Name)
                    {
                        case "pile":
                            pile = NameOf(field.Value);
                            break;
                        case "count":
                            count = IntOf(field.Value);
                            break;
                        default:
                            problems.Add($"{variant}.play.if.condition.pileCountAtLeast 字段 '{field.Name}' 未支持");
                            break;
                    }
                }

                if (pile is not ("hand" or "draw" or "discard" or "exhaust"))
                {
                    problems.Add($"{variant}.play.if.condition.pileCountAtLeast 的牌区 '{pile}' 未支持");
                    return string.Empty;
                }

                return $"{AuthorCondition.PileCountAtLeastPrefix}{pile}:{count}";
            }
            default:
                problems.Add($"{variant}.play.if.condition.{condition.Name} 未支持"
                    + "（运行时判不出这个条件）");
                return string.Empty;
        }
    }

    /// <summary>
    /// `repeat = { count = N effect = { … } }`（`then`/`else` 里也常见）：把内层原语降级成**批次**
    /// （原语的 `Count` 乘以 N）——运行时已有批次机制（`damage x2` 在同一目标上打两次）。
    /// </summary>
    private static IReadOnlyList<Primitive> RepeatOf(DslValueNode value, string where,
        ref List<string> problems)
    {
        if (value is not DslBlockNode block)
        {
            problems.Add($"{where} 不是块");
            return [];
        }

        var count = 0;
        DslValueNode? effect = null;
        foreach (var field in block.Members.OfType<DslPropertyNode>())
        {
            switch (field.Name)
            {
                case "count":
                    count = IntOf(field.Value);
                    break;
                case "effect":
                    effect = field.Value;
                    break;
                default:
                    problems.Add($"{where} 字段 '{field.Name}' 未支持");
                    break;
            }
        }

        if (effect is null || count <= 0)
        {
            problems.Add($"{where} 缺少有效的 count/effect");
            return [];
        }

        var inner = PrimitivesOf(effect, where, ref problems);
        return [.. inner.Select(primitive => primitive with { Count = primitive.Count * count })];
    }

    /// <summary>
    /// 原语列表 → 步骤列表的**统一准备**（`play` 与条件分支共用）：
    /// ① 省略 `target` 的原语按**卡牌目标域**解释（`selectSelf` ⇒ owner，否则 ⇒ 所选目标；
    /// `draw`/`loseHp`/`modifyEnergy` 不消费目标，保持空）；
    /// ② 若有用到"所选目标"的原语，调用方需要在**最前**插入 `target` 步骤（那一步才会提问并绑定目标）；
    /// ③ 再降级成步骤。
    /// </summary>
    private static (IReadOnlyList<AuthorStep> Steps, bool NeedsTarget) PrepareSteps(
        IReadOnlyList<Primitive> primitives, string cardTarget, ref List<string> problems)
    {
        var normalized = primitives.Select(primitive => primitive.Target.Length == 0
            && primitive.Name is not ("draw" or "loseHp" or "modifyEnergy")
            ? primitive with { Target = cardTarget == "self" ? "owner" : "target" }
            : primitive).ToList();
        var needsTarget = normalized.Any(entry =>
            (entry.Name is "attack" or "scaledAttack" or "attackUsingBlock") && entry.Target != "allEnemies"
            || (entry.Name == "applyPower" && entry.Target == "target"));
        var steps = StepsFrom(normalized,
            normalized.Count(entry => entry.Name == "applyPower"), ref problems);
        return (steps, needsTarget);
    }

    /// `attack`/`scaledAttack`/`attackUsingBlock` → `damage`（`target = allEnemies` 时是 `damage_all`）、
    /// `gainBlock`/`gainBlockFromCurrentBlock` → `block`、`modifyEnergy` → `gain_energy`、
    /// `draw`/`loseHp` → 字面量参数步骤、`applyPower` → 按目标域的 `apply_power*`。
    /// </summary>
    private static IReadOnlyList<AuthorStep> StepsFrom(IReadOnlyList<Primitive> primitives, int powerCount,
        ref List<string> problems)
    {
        var steps = new List<AuthorStep>();
        foreach (var primitive in primitives)
        {
            switch (primitive.Name)
            {
                case "attack":
                case "scaledAttack":
                case "attackUsingBlock":
                    steps.Add(primitive.Target == "allEnemies"
                        ? new AuthorStep(AuthorSteps.DamageAll, 1)
                        : new AuthorStep(AuthorSteps.Damage, Math.Max(1, primitive.Count)));
                    break;
                case "randomAttack":
                    // W-06：`randomAttack { damage = N }`（SwordBoomerang）——目标在**效果执行时**由
                    // 命名随机流当场抽取（复用既有的 `damage_random` 步骤），因此不需要目标选择步骤。
                    steps.Add(new AuthorStep(AuthorSteps.DamageRandom, Math.Max(1, primitive.Count)));
                    break;
                case "gainBlock":
                case "gainBlockFromCurrentBlock":
                    steps.Add(new AuthorStep(AuthorSteps.Block, Math.Max(1, primitive.Count)));
                    break;
                case "modifyEnergy":
                    // `modifyEnergy` 给玩家加"这张牌的 Amount"点能量（冻结 `.author` 的 `gain_energy`）。
                    steps.Add(new AuthorStep(AuthorSteps.GainEnergy, 1));
                    break;
                case "draw":
                    // 与 `.author` 的字面量参数形态一致（运行时按 `draw:<n>` 降级）。
                    steps.Add(new AuthorStep($"{AuthorSteps.Draw}:{Math.Max(0, primitive.Amount)}", 1));
                    break;
                case "loseHp":
                    if (primitive.Target is not ("owner" or ""))
                    {
                        problems.Add($"loseHp 的目标域 '{primitive.Target}' 未支持（只支持自己）");
                        break;
                    }

                    steps.Add(new AuthorStep($"{AuthorSteps.LoseHp}:{Math.Max(0, primitive.Amount)}", 1));
                    break;
                case "heal":
                    // W-06：`heal { amount = n }`（NotYet）——回血不超过最大生命。
                    steps.Add(new AuthorStep($"{AuthorSteps.Heal}:{Math.Max(0, primitive.Amount)}", 1));
                    break;
                case "on-fatal-gain-max-hp":
                    // W-06：`attack { onFatal = { gainMaxHp = { amount = n } } }`（Feed）。
                    steps.Add(new AuthorStep(
                        $"{AuthorSteps.OnFatalGainMaxHp}:{Math.Max(0, primitive.Amount)}", 1));
                    break;
                case "applyPower":
                    // W-06：`applyScaledPower`（Dominate）——**层数取所选目标身上某个能力的层数**：
                    // 降级成既有步骤 `apply_power_self_from_target:<被计数的能力 id>`（运行时会在它所在的
                    // 位置读目标状态，因此"先给目标上状态、再按目标状态给自己"是**声明出来的次序**）。
                    if (primitive.AmountSource.StartsWith(AuthorCondition.TargetHasPowerPrefix,
                            StringComparison.Ordinal)
                        || primitive.AmountSource.StartsWith(AmountSource.TargetPowerCountPrefix,
                            StringComparison.Ordinal))
                    {
                        // ENV-02：**施加给谁**由声明的目标决定——Dominate 是给自己（`target = self`），
                        // MoltenFist 是给所选目标（`target = context.target`，把目标的易伤翻倍）。
                        // 早先无条件降级成 `_self_from_target`，会把 MoltenFist 的效果**施加到玩家身上**。
                        var counted = primitive.AmountSource.StartsWith(AmountSource.TargetPowerCountPrefix,
                            StringComparison.Ordinal)
                            ? primitive.AmountSource[AmountSource.TargetPowerCountPrefix.Length..]
                            : primitive.AmountSource;
                        var scaledKind = primitive.Target == "owner"
                            ? AuthorSteps.ApplyPowerSelfFromTarget
                            : primitive.Target == "allEnemies"
                                ? string.Empty
                                : AuthorSteps.ApplyPowerTargetFromTarget;
                        if (scaledKind.Length == 0)
                        {
                            problems.Add($"applyScaledPower 目标域 '{primitive.Target}' 未支持"
                                + "（只支持 self / 单个目标）");
                            break;
                        }

                        steps.Add(new AuthorStep($"{scaledKind}:{counted}", 1));
                        break;
                    }

                    steps.Add(new AuthorStep(powerCount == 1
                        ? primitive.Target switch
                        {
                            "owner" => AuthorSteps.ApplyPowerSelf,
                            "allEnemies" => AuthorSteps.ApplyPowerAll,
                            _ => AuthorSteps.ApplyPower,
                        }
                        : primitive.Target switch
                        {
                            "owner" => $"{AuthorSteps.ApplyPowerSelf}:{ToId(primitive.PowerId)}:{primitive.Stacks}",
                            "allEnemies" => $"{AuthorSteps.ApplyPowerAll}:{ToId(primitive.PowerId)}:{primitive.Stacks}",
                            _ => $"{AuthorSteps.ApplyPower}:{ToId(primitive.PowerId)}:{primitive.Stacks}",
                        }, 1));
                    break;
                case "choose-card":
                    // `chooseCard` 直接对应一个作者模型步骤（`top_from_discard` / `exhaust_from_hand`）。
                    steps.Add(new AuthorStep(primitive.StepKind, 1));
                    break;
                case "clone-card":
                    // ENV-T05：`cloneCard` 同样直接对应一个作者模型步骤（`clone_card_to_discard`）。
                    // 它不携带数值，也没有目标域；批次由外层 `repeat` 在效果程序里表达。
                    steps.Add(new AuthorStep(primitive.StepKind, 1));
                    break;
            }
        }

        return steps;
    }

    /// <summary>
    /// 读 `hooks` 块：**钩子名 → 该钩子的原语块**。支持运行时真的会触发的五种钩子
    /// （`onExhaust` / `turnEndInHand` / `onEnterCombat` / `beforeCardPlayed` / `afterCardPlayed`）；
    /// 其它钩子名（例如 `afterAutoPostPlay`）记为未支持。
    /// 需要**选择目标**的钩子在本批次不支持（钩子没有目标选择这一步）。
    /// </summary>
    private static AuthorHooks ReadHooks(DslValueNode value, string variant, ref List<string> problems)
    {
        if (value is not DslBlockNode hooks)
        {
            problems.Add($"{value.Location}: {variant}.hooks must be a block");
            return AuthorHooks.None;
        }

        var byKind = new Dictionary<string, List<AuthorStep>>(StringComparer.Ordinal);
        var programs = ImmutableDictionary.CreateBuilder<string, CardEffectProgram>(StringComparer.Ordinal);
        foreach (var member in hooks.Members.OfType<DslPropertyNode>())
        {
            var kind = member.Name switch
            {
                "onExhaust" => AuthorSteps.HookOnExhaust,
                "onDiscard" => AuthorSteps.HookOnDiscard,
                "turnEndInHand" => AuthorSteps.HookTurnEndInHand,
                "onEnterCombat" => AuthorSteps.HookOnEnterCombat,
                "beforeCardPlayed" => AuthorSteps.HookBeforeCardPlayed,
                "afterCardPlayed" => AuthorSteps.HookAfterCardPlayed,
                "afterAutoPostPlay" => "after_auto_post_play",
                _ => string.Empty,
            };
            if (kind.Length == 0)
            {
                problems.Add($"{variant}.hooks 的钩子 '{member.Name}' 未支持");
                continue;
            }

            var program = ReadEffectProgram(member.Value, $"{variant}.hooks.{member.Name}", "self", null, false,
                ref problems, out var primitives);
            ValidateHookProgram(program, ref problems);
            if (program.NeedsSelectedTarget)
            {
                problems.Add($"{variant}.hooks.{member.Name} 需要选择目标（钩子没有目标选择这一步）");
                continue;
            }

            var summaryProblems = new List<string>();
            var steps = StepsFrom(primitives,
                primitives.Count(entry => entry.Name == "applyPower"), ref summaryProblems);
            byKind[kind] = [.. steps];
            programs[kind] = program;
        }

        IReadOnlyList<AuthorStep> Steps(string kind) =>
            byKind.TryGetValue(kind, out var found) ? found : [];

        return new AuthorHooks(
            Steps(AuthorSteps.HookOnExhaust),
            Steps(AuthorSteps.HookTurnEndInHand),
            Steps(AuthorSteps.HookOnEnterCombat),
            Steps(AuthorSteps.HookBeforeCardPlayed),
            Steps(AuthorSteps.HookAfterCardPlayed)) { Programs = programs.ToImmutable() };
    }

    private static IReadOnlyList<Primitive> PrimitivesOf(DslValueNode value, string variant,
        ref List<string> problems, bool allowIf = false)
    {
        var primitives = new List<Primitive>();
        if (value is not DslBlockNode play)
        {
            problems.Add($"{variant}.play 不是块");
            return primitives;
        }

        foreach (var member in play.Members.OfType<DslPropertyNode>())
        {
            // `if` 由调用方按"尾部条件段"处理（见 `play` 分支）；`modifyCardVariable` 由**变体级**处理
            //（它不是步骤，而是"打出后成长"的卡级事实）——只有调用方确认已处理时才允许跳过。
            if (member.Name is "if" or "modifyCardVariable")
            {
                if (!allowIf)
                {
                    problems.Add($"{variant}.play 的 {member.Name} 未支持（此处只支持尾部条件段/卡级成长）");
                }

                continue;
            }

            if (member.Name == "repeat")
            {
                primitives.AddRange(RepeatOf(member.Value, $"{variant}.play.repeat", ref problems));
                continue;
            }

            // W-06：`upgradeCards { pile = hand }`（Armaments 的升级形态）——升级整手牌。
            if (member.Name == "upgradeCards")            {
                var pile = member.Value is DslBlockNode upgradeBlock
                    ? upgradeBlock.Members.OfType<DslPropertyNode>()
                        .FirstOrDefault(field => field.Name == "pile") is { } pileField
                            ? NameOf(pileField.Value)
                            : "hand"
                    : "hand";
                if (pile == "hand")
                {
                    primitives.Add(new Primitive("choose-card", 0, 1, string.Empty, string.Empty, 0,
                        string.Empty, 0, 0, AuthorSteps.UpgradeHand));
                }
                else
                {
                    problems.Add($"{variant}.play.upgradeCards 的牌区 '{pile}' 未支持（只支持 hand）");
                }

                continue;
            }

            // 用命名流随机抽一张手牌消耗（不需要玩家决策）。
            if (member.Name == "randomMoveCard")
            {
                var step = RandomMoveCardStep(member.Value, $"{variant}.play.randomMoveCard", ref problems);
                if (step is not null)
                {
                    primitives.Add(step);
                }

                continue;
            }

            // W-06：`chooseCard` —— 选择一张牌。本批次支持两种"运行时真的能执行"的形态：
            //   * `from = discard, to = draw, destinationPosition = top` → `top_from_discard`（Headbutt）；
            //   * `from = hand, to = exhaust` → `exhaust_from_hand`（BurningPact / Brand）。
            // 其它形态（`to = none` + `onSelected` 升级手牌、`from = hand` 的随机移动等）记为未支持。
            if (member.Name == "chooseCard")
            {
                var step = ChooseCardStep(member.Value, $"{variant}.play.chooseCard", ref problems);
                if (step is not null)
                {
                    primitives.Add(step);
                }

                continue;
            }

            // ENV-T05：`cloneCard { pile = discard }` —— 打出后在**弃牌堆**留下一个本张牌的**战斗克隆**。
            // 这是原版 `CardModel.CreateClone` 的受限叶：只支持"源＝正在打出的这张牌、目的地＝discard"，
            // 别的牌区或额外字段都要带源位置明确失败（见 `CloneCardStep`），不泛化未知字符串。
            if (member.Name == "cloneCard")
            {
                var step = CloneCardStep(member.Value, $"{variant}.play.cloneCard", ref problems);
                if (step is not null)
                {
                    primitives.Add(step);
                }

                continue;
            }

            var supported = member.Name is "attack" or "gainBlock" or "draw" or "loseHp" or "applyPower"
                or "scaledAttack" or "attackUsingBlock" or "gainBlockFromCurrentBlock" or "modifyEnergy"
                or "randomAttack" or "heal" or "applyScaledPower";
            if (!supported)
            {
                problems.Add($"{variant}.play 原语 '{member.Name}' 未支持（本批次只有 "
                    + "attack/randomAttack/gainBlock/draw/loseHp/heal/applyPower/applyScaledPower/scaledAttack/"
                    + "attackUsingBlock/"
                    + "gainBlockFromCurrentBlock/modifyEnergy）");
                continue;
            }

            var amountField = member.Name switch
            {
                "attack" => "damage",
                // W-06：`randomAttack { damage = N }` 与 `attack` 同形（数值写在 `damage` 上）。
                "randomAttack" => "damage",
                "applyPower" => "stacks",
                // 这些原语的数值来自**卡级/原语级 scaled**（或"当前格挡"），没有字面量 amount 字段。
                "scaledAttack" or "gainBlockFromCurrentBlock" or "attackUsingBlock" or "modifyEnergy"
                    or "applyScaledPower" => string.Empty,
                _ => "amount",
            };
            var amount = 0;
            var count = 1;
            var target = member.Name switch
            {
                "gainBlockFromCurrentBlock" => "owner",
                _ => string.Empty,
            };
            var powerId = string.Empty;
            var amountSource = string.Empty;
            var amountBase = 0;
            var amountPerCount = 0;
            var found = amountField.Length == 0;
            if (member.Value is DslBlockNode body)
            {
                foreach (var field in body.Members.OfType<DslPropertyNode>())
                {
                    switch (field.Name)
                    {
                        case var _ when amountField.Length > 0 && field.Name == amountField:
                            if (field.Value is DslNumberNode number
                                && int.TryParse(number.Literal, out var parsed))
                            {
                                amount = parsed;
                                found = true;
                            }

                            break;
                        case "damage" when member.Name == "scaledAttack":
                            // `scaledAttack = { damage = N }` 形态（若有）按字面量读。
                            if (field.Value is DslNumberNode damageNode
                                && int.TryParse(damageNode.Literal, out var parsedDamage))
                            {
                                amount = parsedDamage;
                                found = true;
                            }

                            break;
                        case "base":
                            amountBase = IntOf(field.Value);
                            found = true;
                            break;
                        case "perCount":
                            amountPerCount = IntOf(field.Value);
                            break;
                        case "count" when member.Name is "scaledAttack" or "applyScaledPower":
                            amountSource = CountSourceOf(field.Value, $"{variant}.play.{member.Name}",
                                ref problems);
                            break;
                        case "count":
                            if (field.Value is DslNumberNode countNode
                                && int.TryParse(countNode.Literal, out var parsedCount))
                            {
                                count = parsedCount;
                            }

                            break;
                        case "target":
                            target = TargetFieldOf(field.Value);
                            break;
                        case "amount" when member.Name == "modifyEnergy":
                            // `modifyEnergy { amount = { scaled = … } }`：能量数值本身也是"按数量"的。
                            var scaledEnergy = ScaledOf(field.Value, $"{variant}.play.modifyEnergy.amount",
                                ref problems);
                            amountSource = scaledEnergy?.Source ?? amountSource;
                            amountBase = scaledEnergy?.Base ?? amountBase;
                            amountPerCount = scaledEnergy?.PerCount ?? amountPerCount;
                            found = scaledEnergy is not null;
                            break;
                        case "power":
                            powerId = NameOf(field.Value);
                            break;
                        case "properties":
                            // `loseHp` 的 `{ move unpowered unblockable }`：与切片的 `lose_hp` 步骤同一语义。
                            break;
                        case "onFatal" when member.Name == "attack":
                            // W-06：击杀奖励（Feed）——在下面的循环之后作为**独立原语**追加，
                            // 这里只是"已识别"。
                            break;
                        default:
                            problems.Add($"{variant}.play.{member.Name} 字段 '{field.Name}' 未支持");
                            break;
                    }
                }
            }

            if (member.Name is "scaledAttack" && amountSource.Length == 0 && amountBase == 0)
            {
                problems.Add($"{variant}.play.scaledAttack 缺少可识别的数值来源");
            }

            if (member.Name is "gainBlockFromCurrentBlock" && amountSource.Length == 0)
            {
                // `gainBlockFromCurrentBlock`：格挡量 = 当前格挡（冻结 `.author` 的 `amount-source = block-count`，
                // 默认 base 0 / perCount 1）。
                amountSource = "block-count";
                amountBase = 0;
                amountPerCount = 1;
            }

            if (!found)
            {
                problems.Add($"{variant}.play.{member.Name} 缺少整数字面量 '{amountField}'");
            }

            if (member.Name == "applyPower" && powerId.Length == 0)
            {
                problems.Add($"{variant}.play.applyPower 缺少 'power'");
            }

            // `applyScaledPower` 在运行时与 `applyPower` 同族（卡级 power ＋ 步骤参数），按 applyPower 发射。
            primitives.Add(new Primitive(member.Name == "applyScaledPower" ? "applyPower" : member.Name,
                amount, count, target, powerId, amount, amountSource, amountBase, amountPerCount));

            // W-06：`attack { damage = N, onFatal = { gainMaxHp = { amount = M } } }`（Feed）——
            // 击杀奖励是**紧跟这次攻击之后**的独立步骤，因此这里再补一条原语。
            if (member.Name == "attack" && member.Value is DslBlockNode attackBody
                && attackBody.Members.OfType<DslPropertyNode>()
                    .FirstOrDefault(field => field.Name == "onFatal") is { } onFatal)
            {
                var gain = 0;
                var recognised = onFatal.Value is DslBlockNode onFatalBody
                    && onFatalBody.Members.OfType<DslPropertyNode>()
                        .FirstOrDefault(field => field.Name == "gainMaxHp") is { } gainMaxHp
                    && gainMaxHp.Value is DslBlockNode gainBody
                    && gainBody.Members.OfType<DslPropertyNode>()
                        .FirstOrDefault(field => field.Name == "amount") is { } amountNode
                    && int.TryParse(NameOf(amountNode.Value), out gain);
                if (!recognised)
                {
                    problems.Add($"{variant}.play.attack.onFatal 形态未支持"
                        + "（只支持 onFatal = { gainMaxHp = { amount = n } }）");
                }
                else
                {
                    primitives.Add(new Primitive("on-fatal-gain-max-hp", gain, 1, string.Empty, string.Empty,
                        gain, string.Empty, 0, 0));
                }
            }
        }

        // 块里出现的裸名字（`play = { someCall() }` 之类）也要报出来，不静默丢弃。
        foreach (var member in play.Members.Where(item => item is not DslPropertyNode))
        {
            problems.Add($"{variant}.play 含非属性成员（{member.GetType().Name}）");
        }

        return primitives;
    }

    /// <summary>原语内部的 `target = allEnemies | context.target | context.owner`。</summary>
    private static string TargetFieldOf(DslValueNode value)
    {
        var name = NameOf(value);
        return name switch
        {
            "context.target" => "target",
            "context.owner" => "owner",
            "allEnemies" => "allEnemies",
            _ => $"unsupported:{name}",
        };
    }

    private static string TargetOf(DslValueNode value, string variant, ref List<string> problems)
    {
        if (value is not DslBlockNode target)
        {
            problems.Add($"{variant}.target 不是块");
            return "unsupported";
        }

        var selector = target.Members.OfType<DslPropertyNode>().FirstOrDefault();
        if (selector is null)
        {
            problems.Add($"{variant}.target 目标块里没有选择器");
            return "unsupported";
        }

        if (selector.Name == "selectNone")
        {
            if (selector.Value is not DslBlockNode { Members.Count: 0 } || target.Members.Count != 1)
                problems.Add($"{variant}.target selectNone requires an empty selector block");
            return "none";
        }

        if (selector.Name == "selectSelf")
        {
            return "self";
        }

        if (selector.Name == "selectAll")
        {
            return "all";
        }

        if (selector.Name == "selectRandom")
        {
            // W-06：`target = { selectRandom = {} }`（SwordBoomerang）——**打出时不提问**，
            // 目标由效果里的随机攻击步骤（`damage_random`，走命名随机流）当场抽取，
            // 因此这里按"不选目标"处理（与 `selectAll` 同一类：播放时不需要目标步骤）。
            return "all";
        }

        if (selector.Name != "selectOne" || selector.Value is not DslBlockNode body)
        {
            problems.Add($"{variant}.target 选择器 '{selector.Name}' 未支持");
            return $"unsupported:{selector.Name}";
        }

        var from = string.Empty;
        var where = string.Empty;
        foreach (var field in body.Members.OfType<DslPropertyNode>())
        {
            switch (field.Name)
            {
                case "from":
                    from = NameOf(field.Value);
                    break;
                case "where":
                    where = NameOf(field.Value);
                    break;
                default:
                    problems.Add($"{variant}.target 字段 '{field.Name}' 未支持");
                    break;
            }
        }

        return (from, where) switch
        {
            ("context.opponents", "alive") => "one-opponent-alive",
            ("context.opponents", "") => "one-opponent",
            _ => $"unsupported:{from}:{where}",
        };
    }

    private static string NameOf(DslValueNode value) => value switch
    {
        DslNameNode name => name.Name,
        DslNumberNode number => number.Literal,
        DslStringNode text => text.Value,
        DslBooleanNode boolean => boolean.Value ? "true" : "false",
        _ => "?",
    };

    private static IEnumerable<string> NamesOf(DslValueNode value)
    {
        if (value is not DslBlockNode block)
        {
            yield break;
        }

        foreach (var member in block.Members.OfType<DslPropertyNode>())
        {
            yield return member.Name;
        }
    }

    // -- 降级 ---------------------------------------------------------------------------------------

    private static AuthorCard Lower(Declaration declaration, string packageId, string sourcePath,
        IReadOnlyDictionary<string, string>? powers, IReadOnlySet<string>? declaredTags,
        IReadOnlySet<string>? declaredCards, List<string> linkProblems)
    {
        var normal = declaration.Normal;
        var upgraded = declaration.Upgraded;

        // **链接**：正式能力名 → 已声明能力 id；链接不上就记为未支持（不猜 id）。
        // 大小写**不敏感**：正式源里同一个能力有 `Vulnerable` 与 `vulnerable` 两种写法
        // （Bash 用大写、Uppercut 用小写），按大小写敏感查表会把后者误判成"链接失败"。
        string ResolvePower(string raw)
        {
            if (raw.Length == 0)
            {
                return string.Empty;
            }

            if (powers is not null && powers.TryGetValue(raw, out var linked))
            {
                return linked;
            }

            if (powers is not null)
            {
                var canonical = powers.Values.FirstOrDefault(id => string.Equals(id, raw, StringComparison.OrdinalIgnoreCase));
                if (canonical is not null) return canonical;
                foreach (var entry in powers)
                {
                    if (string.Equals(entry.Key, raw, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }

            linkProblems.Add($"能力引用 '{raw}' 无法链接到已声明能力（能力名 → id 表里没有）");
            return string.Empty;
        }

        string ResolveEffectPower(string raw, DslSourceLocation location)
        {
            var before = linkProblems.Count;
            var linked = ResolvePower(raw);
            for (var i = before; i < linkProblems.Count; i++) linkProblems[i] = $"{location}: {linkProblems[i]}";
            return linked;
        }

        // ENV-T05：标签引用必须指向本源真的声明过的标签；链接不上记为未支持（不猜、不当成 0）。
        string ResolveTag(string raw, DslSourceLocation location)
        {
            var id = NamespaceTag(raw, packageId);
            if (declaredTags is null || declaredTags.Contains(id)) return id;
            linkProblems.Add($"{location}: 标签引用 '{raw}' 无法链接到已声明标签（本源没有任何卡声明它）");
            return string.Empty;
        }

        // ENV-T05：`transformSelectedCard { card = GiantRock }` 的卡引用同样要指向本源真的声明过的卡
        // （PrimalForce → GiantRock）。链接不上记为未支持，不在运行时猜一个定义出来。
        string ResolveCard(string raw, DslSourceLocation location)
        {
            var id = raw.Contains(':', StringComparison.Ordinal) ? raw : $"{packageId}:{raw}";
            if (declaredCards is null || declaredCards.Contains(id)) return id;
            linkProblems.Add($"{location}: 卡引用 '{raw}' 无法链接到本包已声明的卡（声明表里没有）");
            return string.Empty;
        }

        // ENV-T05：`cards-with-tag:<作者写的标签名>` 的包前缀在这里补
        // （卡级 `damageValue` 与 `play` 里的 `scaledAttack` 共用同一个来源文案）。
        string ResolveAmountSource(string raw, int line)
        {
            if (!raw.StartsWith(AmountSource.CardsWithTagPrefix, StringComparison.Ordinal)) return raw;
            var id = ResolveTag(raw[AmountSource.CardsWithTagPrefix.Length..], new DslSourceLocation(sourcePath, line, 1));
            return id.Length == 0 ? raw : $"{AmountSource.CardsWithTagPrefix}{id}";
        }

        // 步骤里带的能力名（`apply_power:<名字>:<层数>`）同样要链接。
        IReadOnlyList<AuthorStep> LinkSteps(IReadOnlyList<AuthorStep> steps)
        {
            var linked = new List<AuthorStep>(steps.Count);
            foreach (var step in steps)
            {
                var parts = step.Kind.Split(':');
                if (parts.Length == 3 && parts[0] is AuthorSteps.ApplyPower
                    or AuthorSteps.ApplyPowerSelf or AuthorSteps.ApplyPowerAll)
                {
                    var id = ResolvePower(parts[1]);
                    linked.Add(id.Length == 0
                        ? step
                        : new AuthorStep($"{parts[0]}:{id}:{parts[2]}", step.Count));
                    continue;
                }

                linked.Add(step);
            }

            return linked;
        }

        return new AuthorCard(
            // 与 `.author` 路径同一口径：卡 id 带包前缀（`base-spire:strike-ironclad`）。
            Id: declaration.Id.Contains(':') ? declaration.Id : $"{packageId}:{declaration.Id}",
            Name: declaration.Name,
            Kind: KindOf(normal.Type),
            Cost: normal.Cost,
            IsXCost: normal.IsXCost,
            NormalCost: normal.Cost,
            UpgradedCost: upgraded.Cost,
            NormalAmount: normal.Amount,
            UpgradedAmount: upgraded.Amount,
            PowerId: ResolvePower(normal.PowerId),
            NormalStacks: normal.Stacks,
            UpgradedStacks: upgraded.Stacks,
            NormalSteps: LinkSteps(normal.Steps),
            UpgradedSteps: LinkSteps(upgraded.Steps),
            NormalAmountSource: ResolveAmountSource(normal.AmountSource, declaration.Line),
            UpgradedAmountSource: ResolveAmountSource(upgraded.AmountSource, declaration.Line),
            NormalAmountBase: normal.AmountBase,
            UpgradedAmountBase: upgraded.AmountBase,
            NormalAmountPerCount: normal.AmountPerCount,
            UpgradedAmountPerCount: upgraded.AmountPerCount,
            NormalRetain: normal.Keywords.Contains("retain", StringComparer.Ordinal),
            UpgradedRetain: upgraded.Keywords.Contains("retain", StringComparer.Ordinal),
            NormalUnpowered: false,
            UpgradedUnpowered: false,
            NormalConditional: normal.Conditional,
            UpgradedConditional: upgraded.Conditional,
            NormalUnplayable: normal.Keywords.Contains("unplayable", StringComparer.Ordinal),
            UpgradedUnplayable: upgraded.Keywords.Contains("unplayable", StringComparer.Ordinal),
            NormalEthereal: normal.Keywords.Contains("ethereal", StringComparer.Ordinal),
            UpgradedEthereal: upgraded.Keywords.Contains("ethereal", StringComparer.Ordinal),
            NormalVariables: new Dictionary<string, int>(normal.Variables, StringComparer.Ordinal),
            UpgradedVariables: new Dictionary<string, int>(upgraded.Variables, StringComparer.Ordinal),
            NormalGrowVariable: normal.GrowVariable,
            UpgradedGrowVariable: upgraded.GrowVariable,
            NormalGrowAmount: normal.GrowAmount,
            UpgradedGrowAmount: upgraded.GrowAmount,
            NormalHooks: LinkHookPrograms(normal.Hooks, ResolveEffectPower, ResolveTag, ResolveCard),
            UpgradedHooks: LinkHookPrograms(upgraded.Hooks, ResolveEffectPower, ResolveTag, ResolveCard),
            SourcePath: sourcePath,
            SourceLine: declaration.Line)
        {
            // ENV-T05：奖励牌池需要的注册/筛选元数据（**非效果语义**，故以 init 属性追加，
            // 不改 40 个位置参数，也就不动任何既有调用点）。
            NormalXCost = normal.IsXCost,
            NormalStarCost = normal.StarCost,
            NormalStarXCost = normal.IsStarXCost,
            UpgradedStarXCost = upgraded.IsStarXCost,
            UpgradedStarCost = upgraded.StarCost,
            UpgradedXCost = upgraded.IsXCost,
            NormalDamageValue = normal.DamageValue is null ? null : LinkAmount(normal.DamageValue,
                new DslSourceLocation(sourcePath, declaration.Line, 1), ResolveEffectPower, ResolveTag),
            UpgradedDamageValue = upgraded.DamageValue is null ? null : LinkAmount(upgraded.DamageValue,
                new DslSourceLocation(sourcePath, declaration.Line, 1), ResolveEffectPower, ResolveTag),
            NormalProgram = LinkEffectProgram(normal.Program, ResolveEffectPower, ResolveTag, ResolveCard),
            UpgradedProgram = LinkEffectProgram(upgraded.Program, ResolveEffectPower, ResolveTag, ResolveCard),
            NormalResult = normal.Keywords.Contains("exhaust") ? CardResultPolicy.Exhaust : CardResultPolicy.Discard,
            UpgradedResult = upgraded.Keywords.Contains("exhaust") ? CardResultPolicy.Exhaust : CardResultPolicy.Discard,
            Rarity = normal.Rarity,
            UpgradedRarity = upgraded.Rarity,
            Pools = declaration.Pools,
            Upgradeable = declaration.Upgradeable,
            CanGenerateInCombat = declaration.CanGenerateInCombat,
            CostReductionPerAttack = declaration.CostReductionPerAttack,
            DamageVariable = declaration.DamageVariable,
            NormalInnate = normal.Keywords.Contains("innate", StringComparer.Ordinal),
            UpgradedInnate = upgraded.Keywords.Contains("innate", StringComparer.Ordinal),
            MultiplayerOnly = declaration.MultiplayerOnly,
            Tags = [.. declaration.Tags.Select(raw => NamespaceTag(raw, packageId))],
        };
    }

    private static string KindOf(string type) => type switch
    {
        "attack" => "Attack",
        "skill" => "Skill",
        "power" => "Power",
        "status" => "Status",
        _ => type,
    };
}
