using System.Globalization;
using CoreTheSpire;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// 正式 0.0.1 **敌人（enemy）源 → 通用 Engine** 的作者前端。
///
/// 与卡牌前端（<see cref="SpireCardSource"/>）、能力前端（<see cref="SpirePowerSource"/>）同一套做法：
/// 复用通用解析器 <see cref="DslSourceParser"/> 的 AST，只做 **AST → 共享作者模型**
/// （<see cref="AuthorEnemy"/>）的降级，随后由切片运行时（Engine IR）执行；
/// 五个状态分开报：声明 / 解析 / 降级 / 未支持（逐条原因）。
///
/// 为什么需要它：正式构建此前**没有敌人源**——敌人只能来自冻结的 `.author` 原型文本（两个 Cultist），
/// 这正是"正式路径回退原型"的缺口。本前端让 `ContentSource/enemies*.spire` 的 `enemy` 声明
/// 成为正式敌人的唯一来源。
///
/// 显式范围（本批次）：切片运行时**已经能执行**的行动语义
/// <list type="bullet">
/// <item>`attack(<数值|variants.default>, target=player)`（powered 攻击）；</item>
/// <item>`gainBlock(<数值>)`（本人获得格挡；原版的 Dexterity/Frail 修正阶段尚未接入，见构建描述）；</item>
/// <item>`applyPower(<能力名>, <层数>, target=self|player)`（能力名按**正式能力源自己的规范 id** 解析）；</item>
/// <item>`sequence(…)` / `repeat(N, …)` / `action(effect=…)` 的结构展开；</item>
/// <item>`behavior`：`loop` / `cycle` / `startWith` / `startWithChoice` / `graph` /
///       `chooseWithLimits` / `chooseWithoutRepeat` / `chooseByCondition`，解析成与冻结源码
///       `MonsterMoveStateMachine` 同构的状态图（见 <see cref="AuthorEnemyBehavior"/>），
///       状态、行动历史与已选意图都放在 **Engine 状态**里，由同一份 IR 执行。</item>
/// </list>
///
/// 其它构造（`addCards` / `summonEnemy` / `startingPowers`、以及 `candidate` 上的
/// `maxUsesPerCombat &gt; 1`、具名条件 `condition(Name)` 等）**逐条记为未支持并附原因**，
/// 引用未加载能力的敌人同样记为未支持（不产生"悬空引用"或"空效果"敌人）。
/// </summary>
public static class SpireEnemySource
{
    public const string ResourcePrefix = "BaseSpire.ContentSource.";

    /// <summary>`enemies*.spire`：敌人声明所在的全部正式源文件（`ironclad-cards.spire` 一类不含敌人）。</summary>
    public const string ResourcePattern = "enemies";

    /// <summary>敌人行动程序里允许的 step 种类（运行时 `StartEnemyTurn` 逐条实现）。</summary>
    public static class Steps
    {
        public const string Damage = "damage";

        public const string GainBlock = "gain_block";

        /// <summary>`apply_power_self:&lt;能力 id&gt;:&lt;层数&gt;`。</summary>
        public const string ApplyPowerSelf = "apply_power_self";

        /// <summary>`apply_power_player:&lt;能力 id&gt;:&lt;层数&gt;`（作用于玩家）。</summary>
        public const string ApplyPowerPlayer = "apply_power_player";

        /// <summary>`add_cards:&lt;卡 id&gt;:&lt;张数&gt;`（尚未实现）。</summary>
        public const string AddCards = "add_cards";
    }

    /// <summary>一个敌人的**解析结果**（字段齐备、尚未降级）。</summary>
    public sealed record Declaration(
        string Name,
        int Line,
        int NormalMinHp,
        int NormalMaxHp,
        int ToughMinHp,
        int ToughMaxHp,
        int StartingBlock,
        /// <summary>出生时挂上的能力 `[(能力 id, 层数)]`（来源 `startingPowers = [power(Slow, 1), …]`）。</summary>
        IReadOnlyList<(string PowerId, int Stacks)> StartingPowers,
        IReadOnlyList<AuthorMove> Moves,
        AuthorEnemyBehavior Behavior,
        bool HasAscensionVariants,
        string SourcePath);

    /// <summary>一个源文件的降级结果（声明 / 解析 / 降级 / 未支持分开报）。</summary>
    public sealed record Source(
        string Name,
        IReadOnlyList<(string Name, int Line)> Declared,
        IReadOnlyList<Declaration> Parsed,
        IReadOnlyList<AuthorEnemy> Enemies,
        IReadOnlyList<(string Name, string Reason)> Unsupported)
    {
        public int DeclaredCount => Declared.Count;

        public int ParsedCount => Parsed.Count;

        public int LoweredCount => Enemies.Count;

        public string Summary =>
            $"{Name}: 声明 {DeclaredCount} / 解析 {ParsedCount} / 降级 {LoweredCount} / 未支持 {Unsupported.Count}";
    }

    /// <summary>本程序集里全部 `enemies*.spire` 正式敌人源资源名（排序保证可复现）。</summary>
    public static IReadOnlyList<string> EnemyResources()
    {
        var assembly = typeof(SpireEnemySource).Assembly;
        return
        [
            .. assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                    && name.EndsWith(".spire", StringComparison.Ordinal)
                    && name[ResourcePrefix.Length..].StartsWith(ResourcePattern, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <param name="powerIds">正式能力名 → 内容 id（来自**已加载**的能力源）。引用不存在的能力 ⇒ 该敌人未支持。</param>
    /// <param name="cardIds">正式卡名 → 内容 id（来自**已加载**的卡牌源）。`addCards` 用；引用不存在的卡 ⇒ 该敌人未支持。</param>
    public static Source LoadResource(string resourceName,
        IReadOnlyDictionary<string, string>? powerIds = null,
        IReadOnlyDictionary<string, string>? cardIds = null)
    {
        var assembly = typeof(SpireEnemySource).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"缺少正式敌人资源 '{resourceName}'");
        using var reader = new StreamReader(stream);
        var sourcePath = $"base-spire/{resourceName[ResourcePrefix.Length..]}";
        return Parse(reader.ReadToEnd(), sourcePath, powerIds, cardIds);
    }

    public static Source Parse(string text, string sourceName,
        IReadOnlyDictionary<string, string>? powerIds = null,
        IReadOnlyDictionary<string, string>? cardIds = null) =>
        Lower(DslSourceParser.Parse(text, sourceName), sourceName, powerIds, cardIds);

    public static Source Lower(DslPackageNode package, string sourceName,
        IReadOnlyDictionary<string, string>? powerIds = null,
        IReadOnlyDictionary<string, string>? cardIds = null)
    {
        var nodes = package.Members.OfType<DslDeclarationNode>()
            .Where(node => node.Kind == "enemy")
            .ToList();
        var declared = nodes.Select(node => (node.Name, node.Location.Line)).ToList();
        var parsed = new List<Declaration>();
        var lowered = new List<AuthorEnemy>();
        var unsupported = new List<(string, string)>();
        foreach (var node in nodes)
        {
            var problems = new List<string>();
            var declaration = ReadEnemy(node, sourceName, powerIds, cardIds, ref problems);
            if (declaration is null)
            {
                unsupported.Add((node.Name, string.Join("；", problems)));
                continue;
            }

            parsed.Add(declaration);
            if (problems.Count > 0)
            {
                unsupported.Add((node.Name, string.Join("；", problems)));
                continue;
            }

            lowered.Add(Lower(declaration, ToId(node.Name), package.Name));
        }

        return new Source(sourceName, declared, parsed, lowered, unsupported);
    }

    /// <summary>
    /// `startingPowers = [power(<能力>, <层数>), …]`（出生期施加点）。
    ///
    /// 每个成员必须是 `power(名字, 层数)`；名字经"已加载能力"表解析（**不产生悬空引用**），
    /// 层数必须为正整数。别的写法逐条记未支持并点名。
    /// </summary>
    private static void ReadStartingPowers(DslValueNode value, string enemyName,
        IReadOnlyDictionary<string, string>? powerIds, List<(string PowerId, int Stacks)> into,
        ref List<string> problems)
    {
        if (value is not DslListNode list)
        {
            problems.Add($"{enemyName}.startingPowers 未支持（只支持 [power(<能力>, <层数>), …]）");
            return;
        }

        foreach (var item in list.Items)
        {
            if (item is not DslCallNode { Name: "power" } call)
            {
                problems.Add($"{enemyName}.startingPowers 的成员不是 power(...)：{Describe(item)}");
                continue;
            }

            var powerName = string.Empty;
            var stacks = 0;
            foreach (var argument in call.Arguments)
            {
                if (argument.Name is null && powerName.Length == 0 && NameOf(argument.Value).Length > 0)
                {
                    powerName = NameOf(argument.Value);
                }
                else if (argument.Name is null && argument.Value is DslNumberNode number
                    && int.TryParse(number.Literal, out var parsed))
                {
                    stacks = parsed;
                }
                else if (argument.Name is null && argument.Value is DslCallNode { Name: "variants" } variants)
                {
                    var fallback = variants.Arguments.FirstOrDefault(entry => entry.Name == "default")
                        ?? variants.Arguments.FirstOrDefault();
                    stacks = fallback is null ? 0 : IntOf(fallback.Value, 0);
                }
            }

            if (powerName.Length == 0 || stacks <= 0)
            {
                problems.Add($"{enemyName}.startingPowers 的 power(...) 形状未支持"
                    + $"（需要 power(<能力>, <正整数层数>)）：{Describe(item)}");
                continue;
            }

            if (powerIds is null || !powerIds.TryGetValue(powerName, out var powerId))
            {
                problems.Add($"{enemyName}.startingPowers 引用的能力 '{powerName}' 不在**已加载**的能力源里"
                    + "（不产生悬空引用）");
                continue;
            }

            into.Add((powerId, stacks));
        }
    }

    /// <summary>正式源码里的 CamelCase 敌人名 → 内容 id 的末段（`DampCultist` → `damp-cultist`）。</summary>
    public static string ToId(string name) => SpireCardSource.ToId(name);

    // -- 解析 -----------------------------------------------------------------------------------------------

    private static Declaration? ReadEnemy(DslDeclarationNode node, string sourcePath,
        IReadOnlyDictionary<string, string>? powerIds, IReadOnlyDictionary<string, string>? cardIds,
        ref List<string> problems)
    {
        var normalMin = 0;
        var normalMax = 0;
        var toughMin = 0;
        var toughMax = 0;
        var startingBlock = 0;
        var startingPowers = new List<(string PowerId, int Stacks)>();
        var variants = false;
        IReadOnlyList<AuthorMove>? moves = null;
        DslValueNode? behavior = null;
        foreach (var property in node.Members.OfType<DslPropertyNode>())
        {
            switch (property.Name)
            {
                case "hp":
                    ReadHpRange(property.Value, node.Name, ref normalMin, ref normalMax, ref toughMin, ref toughMax,
                        ref variants, ref problems);
                    break;
                case "moves":
                    moves = ReadActions(property.Value, node.Name, powerIds, cardIds, ref problems);
                    break;
                case "behavior":
                    behavior = property.Value;
                    break;
                case "startingBlock":
                    startingBlock = IntOf(property.Value, -1);
                    if (startingBlock < 0)
                    {
                        problems.Add($"{node.Name}.startingBlock 不是非负整数");
                    }

                    break;
                case "startingPowers":
                    // `startingPowers = [power(Slow, 1), …]`：出生时挂上的能力（来源 `MonsterModel` 的
                    // 出生期施加点：`PowerCmd.Apply`，与战斗中出现的能力同一条施加路径）。
                    ReadStartingPowers(property.Value, node.Name, powerIds, startingPowers, ref problems);
                    break;
                default:
                    problems.Add($"敌人字段 '{property.Name}' 未支持");
                    break;
            }
        }

        if (moves is null)
        {
            problems.Add($"{node.Name} 没有 moves 声明");
            return null;
        }

        if (normalMax <= 0 || toughMax <= 0)
        {
            problems.Add($"{node.Name}.hp 缺失或不是正数范围");
            return null;
        }

        if (behavior is null)
        {
            problems.Add($"{node.Name} 没有 behavior 声明（没有行为就不知道敌人下一步做什么）");
            return null;
        }

        var machine = ReadBehavior(behavior, node.Name, moves, ref problems);
        if (machine is null)
        {
            return null;
        }

        if (problems.Count > 0)
        {
            return null;
        }

        return new Declaration(node.Name, node.Location.Line, normalMin, normalMax, toughMin, toughMax, startingBlock,
            startingPowers, moves, machine, variants, sourcePath);
    }

    /// <summary>`variants(default=range(a,b), tough=range(c,d))` 与不带 variants 的形态。</summary>
    private static void ReadHpRange(DslValueNode value, string name, ref int normalMin, ref int normalMax,
        ref int toughMin, ref int toughMax, ref bool variants, ref List<string> problems)
    {
        switch (value)
        {
            case DslCallNode { Name: "variants" } call:
            {
                variants = true;
                var fallback = call.Arguments.FirstOrDefault(argument => argument.Name == "default");
                var tough = call.Arguments.FirstOrDefault(argument => argument.Name == "tough");
                if (fallback is null)
                {
                    problems.Add($"{name}.hp 的 variants 没有 default 分支（进阶 0 的数值无从取得）");
                    return;
                }

                (normalMin, normalMax) = RangeOf(fallback.Value, name, "hp.default", ref problems);
                (toughMin, toughMax) = tough is null
                    ? (normalMin, normalMax)
                    : RangeOf(tough.Value, name, "hp.tough", ref problems);
                return;
            }

            case DslCallNode { Name: "range" }:
                (normalMin, normalMax) = RangeOf(value, name, "hp", ref problems);
                toughMin = normalMin;
                toughMax = normalMax;
                return;

            case DslNumberNode:
                normalMin = IntOf(value, 0);
                normalMax = normalMin;
                toughMin = normalMin;
                toughMax = normalMin;
                return;

            default:
                problems.Add($"{name}.hp 形态未支持（只支持 variants(default=range(..)) / range(..) / 整数）");
                return;
        }
    }

    private static (int Min, int Max) RangeOf(DslValueNode value, string name, string where, ref List<string> problems)
    {
        if (value is not DslCallNode { Name: "range" } range || range.Arguments.Count != 2)
        {
            problems.Add($"{name}.{where} 不是 range(min, max)");
            return (0, 0);
        }

        var min = IntOf(range.Arguments[0].Value, int.MinValue);
        var max = IntOf(range.Arguments[1].Value, int.MinValue);
        if (min == int.MinValue || max == int.MinValue || min > max)
        {
            problems.Add($"{name}.{where} 的范围无效（{min}..{max}）");
        }

        return (min, max);
    }

    /// <summary>`moves = actions(name = &lt;效果&gt;, …)`。</summary>
    private static IReadOnlyList<AuthorMove> ReadActions(DslValueNode value, string name,
        IReadOnlyDictionary<string, string>? powerIds, IReadOnlyDictionary<string, string>? cardIds,
        ref List<string> problems)
    {
        var moves = new List<AuthorMove>();
        if (value is not DslCallNode { Name: "actions" } actions)
        {
            problems.Add($"{name}.moves 不是 actions(…)");
            return moves;
        }

        foreach (var argument in actions.Arguments)
        {
            if (argument.Name is null)
            {
                problems.Add($"{name}.moves 里有位置参数（行动必须写成 名字 = 效果）");
                continue;
            }

            var steps = new List<AuthorMoveStep>();
            var placeholder = new List<string>();
            ReadEffect(argument.Value, $"{name}.moves.{argument.Name}", powerIds, cardIds, steps, placeholder);
            problems.AddRange(placeholder);
            moves.Add(new AuthorMove(argument.Name, steps));
        }

        return moves;
    }

    /// <summary>
    /// 把一个效果表达式展开成**扁平 step 列表**：`sequence`/`repeat`/`action` 在这里被展开，
    /// 因此运行时只需要"顺序执行 step"这一种形状。
    /// </summary>
    private static void ReadEffect(DslValueNode value, string where, IReadOnlyDictionary<string, string>? powerIds,
        IReadOnlyDictionary<string, string>? cardIds, List<AuthorMoveStep> steps, List<string> problems)
    {
        switch (value)
        {
            case DslCallNode { Name: "sequence" } sequence:
                foreach (var argument in sequence.Arguments)
                {
                    ReadEffect(argument.Value, where, powerIds, cardIds, steps, problems);
                }

                return;

            case DslCallNode { Name: "repeat" } repeat when repeat.Arguments.Count == 2:
            {
                // `repeat(variants(default=7, deadly=8), attack(3))` 也是合法写法（进阶 0 取 default）。
                var count = AmountOf(repeat.Arguments[0].Value, where, "repeat 次数", problems);
                if (count is not > 0)
                {
                    return;
                }

                for (var index = 0; index < count.Value; index++)
                {
                    ReadEffect(repeat.Arguments[1].Value, where, powerIds, cardIds, steps, problems);
                }

                return;
            }

            case DslCallNode { Name: "action" } action:
            {
                var effect = action.Arguments.FirstOrDefault(argument => argument.Name == "effect");
                if (effect is null)
                {
                    problems.Add($"{where} 的 action 没有 effect");
                    return;
                }

                ReadEffect(effect.Value, where, powerIds, cardIds, steps, problems);
                return;
            }

            case DslCallNode { Name: "attack" } attack:
            {
                var target = TargetOf(attack, where, problems);
                if (target != "player")
                {
                    problems.Add($"{where} 的 attack 目标 '{target}' 未支持（只支持 target=player）");
                    return;
                }

                var amount = AmountOf(attack.Arguments.FirstOrDefault(argument => argument.Name is null)?.Value,
                    where, "attack", problems);
                if (amount is { } damage)
                {
                    steps.Add(new AuthorMoveStep(Steps.Damage, damage.ToString()));
                }

                return;
            }

            case DslCallNode { Name: "gainBlock" } block:
            {
                var amount = AmountOf(block.Arguments.FirstOrDefault()?.Value, where, "gainBlock", problems);
                if (amount is { } gained)
                {
                    steps.Add(new AuthorMoveStep(Steps.GainBlock, gained.ToString()));
                }

                return;
            }

            case DslCallNode { Name: "applyPower" } apply when apply.Arguments.Count >= 2:
            {
                var power = NameOf(apply.Arguments[0].Value);
                var stacks = AmountOf(apply.Arguments[1].Value, where, "applyPower", problems);
                var target = TargetOf(apply, where, problems);
                if (power.Length == 0 || stacks is null)
                {
                    return;
                }

                if (powerIds is null || !powerIds.TryGetValue(power, out var powerId))
                {
                    problems.Add($"{where} 引用的能力 '{power}' 不在**已加载**的能力源里（不产生悬空引用）");
                    return;
                }

                if (target is not ("self" or "player"))
                {
                    problems.Add($"{where} 的 applyPower 目标 '{target}' 未支持（只支持 self / player）");
                    return;
                }

                steps.Add(new AuthorMoveStep(
                    target == "self" ? Steps.ApplyPowerSelf : Steps.ApplyPowerPlayer,
                    $"{powerId}:{stacks.Value}"));
                return;
            }

            // `addCards(Dazed, count=3, pile=discard, target=player, position=random)`：
            // 往**玩家**牌区放状态牌（来源 `CardPileCmd.AddToCombatAndPreview<T>(targets, pileType, count, creator, position)`，
            // `Chomper.cs:82` 等）。编码成 `add_cards:<卡 id>:<张数>:<牌区>:<位置>`。
            case DslCallNode { Name: "addCards" } addCards:
            {
                var cardName = addCards.Arguments.Count > 0 && addCards.Arguments[0].Name is null
                    ? NameOf(addCards.Arguments[0].Value)
                    : string.Empty;
                if (cardName.Length == 0)
                {
                    problems.Add($"{where} 的 addCards 需要 addCards(<卡名>, …)");
                    return;
                }

                if (cardIds is null || !cardIds.TryGetValue(cardName, out var cardId))
                {
                    problems.Add($"{where} 的 addCards 引用的卡 '{cardName}' 不在**已加载**的卡牌源里"
                        + "（不产生悬空引用）");
                    return;
                }

                var count = 1;
                var pile = "discard";
                var position = "bottom";
                foreach (var argument in addCards.Arguments.Skip(1))
                {
                    switch (argument.Name)
                    {
                        case "count":
                            count = IntOf(argument.Value, -1);
                            if (count <= 0)
                            {
                                problems.Add($"{where} 的 addCards 张数不是正整数");
                                return;
                            }

                            break;
                        case "pile":
                            pile = NameOf(argument.Value);
                            if (pile is not ("hand" or "draw" or "discard"))
                            {
                                problems.Add($"{where} 的 addCards 牌区 '{pile}' 未支持"
                                    + "（只支持 hand / draw / discard）");
                                return;
                            }

                            break;
                        case "position":
                            position = NameOf(argument.Value);
                            if (position is not ("bottom" or "top" or "random"))
                            {
                                problems.Add($"{where} 的 addCards 位置 '{position}' 未支持"
                                    + "（只支持 bottom / top / random）");
                                return;
                            }

                            break;
                        case "target":
                            if (TargetOf(addCards, where, problems) != "player")
                            {
                                problems.Add($"{where} 的 addCards 只支持 target=player");
                                return;
                            }

                            break;
                        default:
                            problems.Add($"{where} 的 addCards 字段 '{argument.Name}' 未支持");
                            return;
                    }
                }

                steps.Add(new AuthorMoveStep(Steps.AddCards, $"{cardId}:{count}:{pile}:{position}"));
                return;
            }

            case DslCallNode { Name: "summonEnemy" }:
                problems.Add($"{where} 的 summonEnemy 未支持：战斗中生成新敌人属事件/召唤机制族");
                return;

            default:
                problems.Add($"{where} 的效果 '{Describe(value)}' 未支持");
                return;
        }
    }

    private static string TargetOf(DslCallNode call, string where, List<string> problems)
    {
        var target = call.Arguments.FirstOrDefault(argument => argument.Name == "target");
        if (target is null)
        {
            return "player";
        }

        var name = NameOf(target.Value);
        if (name.Length == 0)
        {
            problems.Add($"{where} 的 target 不是名字");
        }

        return name;
    }

    /// <summary>数值形态：整数字面量或 `variants(default=…)`（进阶 0 取 default）。</summary>
    private static int? AmountOf(DslValueNode? value, string where, string field, List<string> problems)
    {
        if (value is null)
        {
            problems.Add($"{where} 的 {field} 缺少数值");
            return null;
        }

        if (value is DslNumberNode)
        {
            var literal = IntOf(value, int.MinValue);
            if (literal == int.MinValue)
            {
                problems.Add($"{where} 的 {field} 不是整数");
                return null;
            }

            return literal;
        }

        if (value is DslCallNode { Name: "variants" } variants)
        {
            var fallback = variants.Arguments.FirstOrDefault(argument => argument.Name == "default")
                ?? variants.Arguments.FirstOrDefault();
            if (fallback is null)
            {
                problems.Add($"{where} 的 {field} variants 没有 default 数值");
                return null;
            }

            return AmountOf(fallback.Value, where, field, problems);
        }

        problems.Add($"{where} 的 {field} 数值形态未支持（只支持整数或 variants.default）");
        return null;
    }

    /// <summary>
    /// `behavior` → **状态图**（与源码 `MonsterMoveStateMachine` 同构）。
    ///
    /// 支持 `loop` / `cycle` / `startWith` / `startWithChoice` / `graph` / `chooseWithLimits` /
    /// `chooseWithoutRepeat` / `chooseByCondition`；任一形态不认识、候选指向非行动、行动没有后继，
    /// 都返回 null 并附**具体原因**（该敌人整条不进包，不产生近似行为）。
    /// </summary>
    private static AuthorEnemyBehavior? ReadBehavior(DslValueNode value, string name,
        IReadOnlyList<AuthorMove> moves, ref List<string> problems)
    {
        var builder = new BehaviorBuilder(name, moves);
        var initial = builder.Build(value, null);
        var behavior = initial is null ? null : builder.Finish(initial);
        problems.AddRange(builder.Problems);
        return behavior is not null && builder.Problems.Count == 0 ? behavior : null;
    }

    /// <summary>
    /// 状态图构造器。语义逐条对照冻结源码，不"顺手近似"：
    /// <list type="bullet">
    /// <item>`then=&lt;行为&gt;` 是**后续池**：该行为里的行动完成后回到这个池的入口（`Flyconid.cs:36-38`
    ///       把三个行动的后继都设成同一个 `RAND` 分支）；</item>
    /// <item>`graph` 的每个行动后继由它自己的 `edge` 给出，缺后继即声明错误（源码会在转移时抛错）；</item>
    /// <item>`candidate` 只能是**已声明的行动**（分支不能指向分支——源码允许，但正式内容没有这种形状，
    ///       遇到时明确记为未支持）；</item>
    /// <item>权重默认 1、冷却默认 0、重复限制默认 `CanRepeatForever`，与
    ///       `RandomBranchState.AddBranch` 的重载一一对应。</item>
    /// </list>
    /// </summary>
    private sealed class BehaviorBuilder(string enemyName, IReadOnlyList<AuthorMove> moves)
    {
        private readonly List<AuthorEnemyState> _states = [];
        private readonly Dictionary<string, string> _followUp = new(StringComparer.Ordinal);
        private int _nextBranch;

        public List<string> Problems { get; } = [];

        public string? Build(DslValueNode value, string? defaultFollowUp) => value switch
        {
            DslNameNode name => Move(name.Name, defaultFollowUp),
            DslCallNode { Name: "loop" } loop => Loop(loop),
            DslCallNode { Name: "cycle" } cycle => Cycle(cycle),
            DslCallNode { Name: "startWith" } start => StartWith(start, defaultFollowUp),
            DslCallNode { Name: "startWithChoice" } choice => StartWithChoice(choice, defaultFollowUp),
            DslCallNode { Name: "graph" } graph => Graph(graph),
            // 裸分支（`behavior = chooseWithLimits(…)`，`TwoTailedRat` 的形状）：初始状态就是分支，
            // 池里的行动完成后回到池入口（源码里这些行动的 `FollowUpState` 都指向那个分支）。
            DslCallNode { Name: "chooseWithLimits" or "chooseWithoutRepeat" or "chooseByCondition" } branch =>
                BranchEntry(branch, selfFollowUp: true),
            _ => Fail($"behavior 形态 '{Describe(value)}' 未支持：只支持 loop / cycle / startWith / startWithChoice / "
                + "graph / chooseWithLimits / chooseWithoutRepeat / chooseByCondition"),
        };

        /// <summary>收尾：每个行动都必须有后继，否则原版会在转移时抛错（不能静默当成循环）。</summary>
        public AuthorEnemyBehavior? Finish(string initial)
        {
            foreach (var state in _states.OfType<AuthorMoveState>())
            {
                if (!_followUp.ContainsKey(state.Id))
                {
                    Problems.Add($"{enemyName}.behavior 的行动 '{state.Move}' 没有后继："
                        + "原版 `MoveState.FollowUpStateId` 为空时会在转移时抛错，这里按声明错误拒绝");
                }
            }

            if (Problems.Count > 0)
            {
                return null;
            }

            var finalized = _states
                .Select(state => state is AuthorMoveState move
                    ? move with { FollowUp = _followUp[move.Id] }
                    : state)
                .ToList();
            return new AuthorEnemyBehavior(finalized, initial);
        }

        private string? Loop(DslCallNode loop)
        {
            if (loop.Arguments.Count != 1)
            {
                return Fail("behavior 的 loop 需要 loop(<行动>)");
            }

            // `loop(<分支>)`：初始状态就是分支，池内行动完成后回到池入口（`LeafSlimeS`/`TwigSlimeM` 的写法）。
            if (loop.Arguments[0].Value is DslCallNode { Name: "chooseWithLimits" or "chooseWithoutRepeat"
                or "chooseByCondition" } branch)
            {
                return BranchEntry(branch, selfFollowUp: true);
            }

            var name = NameOf(loop.Arguments[0].Value);
            if (name.Length == 0)
            {
                return Fail("behavior 的 loop 需要 loop(<行动>)");
            }

            var state = Move(name, name);
            if (state is not null)
            {
                _followUp[name] = name;
            }

            return state;
        }

        private string? Cycle(DslCallNode cycle)
        {
            var names = new List<string>();
            foreach (var argument in cycle.Arguments)
            {
                var name = NameOf(argument.Value);
                if (name.Length == 0)
                {
                    return Fail("behavior 的 cycle 里有非行动名");
                }

                names.Add(name);
            }

            if (names.Count == 0)
            {
                return Fail("behavior 的 cycle 是空的");
            }

            if (names.Any(name => Move(name, null) is null))
            {
                return null;
            }

            for (var index = 0; index < names.Count; index++)
            {
                _followUp[names[index]] = names[(index + 1) % names.Count];
            }

            return names[0];
        }

        private string? StartWith(DslCallNode start, string? defaultFollowUp)
        {
            var first = start.Arguments.FirstOrDefault(argument => argument.Name is null)?.Value;
            var then = start.Arguments.FirstOrDefault(argument => argument.Name == "then")?.Value;
            var head = first is null ? string.Empty : NameOf(first);
            if (head.Length == 0 || then is null)
            {
                return Fail("behavior 的 startWith 需要 startWith(<行动>, then=<行为>)");
            }

            var tail = Build(then, defaultFollowUp);
            if (tail is null || Move(head, null) is null)
            {
                return null;
            }

            _followUp[head] = tail;
            return head;
        }

        /// <summary>
        /// `startWithChoice(choice, then=B)`：初始状态是**分支**（不是行动），开局那次选取就掷点；
        /// `choice` 的候选与 `B` 里行动的后继都指向 `B` 的入口（`Flyconid.cs:34-49` 的形状）。
        /// </summary>
        private string? StartWithChoice(DslCallNode call, string? defaultFollowUp)
        {
            var choice = call.Arguments.FirstOrDefault(argument => argument.Name is null)?.Value;
            var then = call.Arguments.FirstOrDefault(argument => argument.Name == "then")?.Value;
            if (choice is null || then is null)
            {
                return Fail("behavior 的 startWithChoice 需要 startWithChoice(<分支>, then=<行为>)");
            }

            // `then` 是后续池：它是分支时，池内行动完成后回到池入口（`Build` 对分支就是"自己指向自己"）。
            var thenEntry = Build(then, defaultFollowUp);
            return thenEntry is null ? null : BranchEntry(choice, selfFollowUp: false, candidateFollowUp: thenEntry);
        }

        /// <summary>`graph(start=X, edges=[edge(from, toOrBranch)])`。</summary>
        private string? Graph(DslCallNode graph)
        {
            var start = graph.Arguments.FirstOrDefault(argument => argument.Name == "start")?.Value;
            var edges = graph.Arguments.FirstOrDefault(argument => argument.Name == "edges")?.Value;
            var startName = start is null ? string.Empty : NameOf(start);
            if (startName.Length == 0 || edges is not DslListNode list)
            {
                return Fail("behavior 的 graph 需要 graph(start=<行动>, edges=[edge(<行动>, <后继>), …])");
            }

            if (Move(startName, null) is null)
            {
                return null;
            }

            foreach (var item in list.Items)
            {
                if (item is not DslCallNode { Name: "edge" } edge || edge.Arguments.Count < 2)
                {
                    return Fail("graph 的 edges 里每一项都必须是 edge(<行动>, <后继>)");
                }

                var from = NameOf(edge.Arguments[0].Value);
                if (from.Length == 0 || Move(from, null) is null)
                {
                    return null;
                }

                var target = edge.Arguments[1].Value;
                var entry = target is DslCallNode { Name: "chooseWithLimits" or "chooseWithoutRepeat"
                    or "chooseByCondition" }
                    ? BranchEntry(target, selfFollowUp: false)
                    : Move(NameOf(target), null);
                if (entry is null)
                {
                    return null;
                }

                _followUp[from] = entry;
            }

            return startName;
        }

        private string? RandomBranch(DslCallNode call, string? candidateFollowUp)
        {
            // `repeat=preventImmediate`：整条分支的默认重复限制（候选自己的声明优先）。
            var repeat = call.Arguments.FirstOrDefault(argument => argument.Name == "repeat") is { } repeatArgument
                ? NameOf(repeatArgument.Value) switch
                {
                    "preventImmediate" => EnemyRepeatLimit.NoRepeat,
                    "allow" => EnemyRepeatLimit.Forever,
                    _ => null,
                }
                : EnemyRepeatLimit.Forever;
            if (repeat is null)
            {
                return Fail("chooseWithLimits 的 repeat 只支持 preventImmediate / allow");
            }

            var candidates = new List<AuthorMoveCandidate>();
            foreach (var argument in call.Arguments.Where(argument => argument.Name != "repeat"))
            {
                var candidate = ReadCandidate(argument.Value, repeat, ref candidates);
                if (!candidate)
                {
                    return null;
                }
            }

            if (candidates.Count == 0)
            {
                return Fail("chooseWithLimits 至少要有一个 candidate(...)");
            }

            var followUp = candidateFollowUp;
            if (followUp is not null && candidates.Any(entry => Move(entry.Move, followUp) is null))
            {
                return null;
            }

            var id = $"branch{_nextBranch++}";
            _states.Add(new AuthorRandomState(id, candidates));
            return id;
        }

        private string? NoRepeatBranch(DslCallNode call, string? defaultFollowUp)
        {
            var candidates = new List<AuthorMoveCandidate>();
            foreach (var argument in call.Arguments)
            {
                var name = NameOf(argument.Value);
                if (name.Length == 0)
                {
                    return Fail("chooseWithoutRepeat 的参数必须都是已声明的行动名");
                }

                if (Move(name, defaultFollowUp) is null)
                {
                    return null;
                }

                candidates.Add(new AuthorMoveCandidate(name, 1, 0, EnemyRepeatLimit.NoRepeat, 0, null));
            }

            if (candidates.Count == 0)
            {
                return Fail("chooseWithoutRepeat 至少要有一个候选");
            }

            var id = $"branch{_nextBranch++}";
            _states.Add(new AuthorRandomState(id, candidates));
            return id;
        }

        /// <summary>`chooseByCondition(candidate(move, when=…), …)`：有序、无随机数（`ConditionalBranchState`）。</summary>
        private string? OrderedBranch(DslCallNode call)
        {
            var candidates = new List<(string Move, AuthorEnemyCondition When)>();
            foreach (var argument in call.Arguments)
            {
                if (argument.Value is not DslCallNode { Name: "candidate" } candidate)
                {
                    return Fail("chooseByCondition 的参数必须是 candidate(<行动>, when=<条件>)");
                }

                var name = candidate.Arguments.Count > 0 ? NameOf(candidate.Arguments[0].Value) : string.Empty;
                var when = candidate.Arguments.FirstOrDefault(entry => entry.Name == "when")?.Value;
                if (name.Length == 0 || when is null)
                {
                    return Fail("chooseByCondition 的每个候选都需要 <行动> 和 when=<条件>");
                }

                if (candidate.Arguments.Any(entry => entry.Name is "weight" or "cooldown" or "maxConsecutive"
                        or "maxUsesPerCombat"))
                {
                    return Fail("chooseByCondition 的候选不能带 weight/cooldown/maxConsecutive/maxUsesPerCombat："
                        + "有序条件分支按第一个成立项选择，与权重无关");
                }

                if (ReadCondition(when) is not { } condition || Move(name, null) is null)
                {
                    return null;
                }

                candidates.Add((name, condition));
            }

            if (candidates.Count == 0)
            {
                return Fail("chooseByCondition 至少要有一个候选");
            }

            var id = $"branch{_nextBranch++}";
            _states.Add(new AuthorOrderedState(id, candidates));
            return id;
        }

        private bool ReadCandidate(DslValueNode value, EnemyRepeatLimit branchRepeat,
            ref List<AuthorMoveCandidate> candidates)
        {
            if (value is not DslCallNode { Name: "candidate" } candidate || candidate.Arguments.Count == 0)
            {
                Problems.Add("chooseWithLimits 的参数必须是 candidate(<行动>, …)");
                return false;
            }

            var name = NameOf(candidate.Arguments[0].Value);
            if (name.Length == 0)
            {
                Problems.Add("candidate 的第一个参数必须是行动名");
                return false;
            }

            var weight = 1f;
            var cooldown = 0;
            int? maxConsecutive = null;
            var maxUsesPerCombat = 0;
            AuthorEnemyCondition? when = null;
            foreach (var argument in candidate.Arguments.Skip(1))
            {
                if (argument.Name is null)
                {
                    Problems.Add($"candidate({name}) 的位置参数只允许行动名");
                    return false;
                }

                switch (argument.Name)
                {
                    case "weight":
                        // ENV-T01：源权重是 float32（`AddBranch(…, Func<float>)`），作者文本写十进制字面量，
                        // 按 round-to-nearest 转成 float32（`0.083333336` 精确等于 `1f/12f`）。
                        weight = SingleOf(argument.Value, float.NaN);
                        if (float.IsNaN(weight) || weight < 0f)
                        {
                            Problems.Add($"candidate({name}).weight 不是非负数字");
                            return false;
                        }

                        break;
                    case "cooldown":
                        cooldown = IntOf(argument.Value, -1);
                        if (cooldown < 0)
                        {
                            Problems.Add($"candidate({name}).cooldown 不是非负整数");
                            return false;
                        }

                        break;
                    case "maxConsecutive":
                        maxConsecutive = IntOf(argument.Value, -1);
                        if (maxConsecutive <= 0)
                        {
                            Problems.Add($"candidate({name}).maxConsecutive 必须是正整数");
                            return false;
                        }

                        break;
                    case "maxUsesPerCombat":
                        maxUsesPerCombat = IntOf(argument.Value, -1);
                        if (maxUsesPerCombat <= 0)
                        {
                            Problems.Add($"candidate({name}).maxUsesPerCombat 必须是正整数");
                            return false;
                        }

                        break;
                    case "when":
                        when = ReadCondition(argument.Value);
                        if (when is null)
                        {
                            return false;
                        }

                        break;
                    default:
                        Problems.Add($"candidate({name}) 的字段 '{argument.Name}' 未支持");
                        return false;
                }
            }

            EnemyRepeatLimit repeat;
            if (maxUsesPerCombat > 0)
            {
                if (maxUsesPerCombat > 1)
                {
                    // 源码里 `UseOnlyOnce` 只等于"用过就 0"，`CanRepeatXTimes` 是**连续**上限，
                    // 没有"整场最多 n 次"的对应物（RandomBranchState.cs:130-167）。
                    Problems.Add($"candidate({name}).maxUsesPerCombat={maxUsesPerCombat} 未支持："
                        + "原版只有 UseOnlyOnce（= 1 次）与连续上限 CanRepeatXTimes(n)，没有整场次数上限");
                    return false;
                }

                repeat = EnemyRepeatLimit.Once;
            }
            else
            {
                repeat = maxConsecutive is { } limit ? EnemyRepeatLimit.AtMost(limit) : branchRepeat;
            }

            if (Move(name, null) is null)
            {
                return false;
            }

            candidates.Add(new AuthorMoveCandidate(name, weight, cooldown, repeat, maxUsesPerCombat, when));
            return true;
        }

        /// <summary>
        /// `when=…`。只实现**有明确来源**的形状：`metric(EnemyHp, LessThan, n)`（`FrogKnight` 半血分支）与
        /// `metric(Turn, AtLeast, n)`（`FrogKnight` 的恒真兜底），以及 all/any/not 组合。
        /// 其它（具名 `condition(Name)`、`allyAction`、战斗状态等）明确记为未支持。
        /// </summary>
        private AuthorEnemyCondition? ReadCondition(DslValueNode value)
        {
            switch (value)
            {
                case DslCallNode { Name: "metric" } metric when metric.Arguments.Count == 3:
                {
                    var fact = NameOf(metric.Arguments[0].Value);
                    var comparison = NameOf(metric.Arguments[1].Value);
                    var amount = AmountOf(metric.Arguments[2].Value, enemyName, $"metric({fact},{comparison})", Problems);
                    if (amount is null)
                    {
                        return null;
                    }

                    return (fact, comparison) switch
                    {
                        ("EnemyHp", "LessThan") => new EnemyHpBelowCondition(amount.Value),
                        ("Turn", "AtLeast") => new TurnAtLeastCondition(amount.Value),
                        _ => FailCondition($"条件 metric({fact}, {comparison}, …) 未支持："
                            + "当前只实现 metric(EnemyHp, LessThan, n) 与 metric(Turn, AtLeast, n)"),
                    };
                }

                case DslCallNode { Name: "not" } not when not.Arguments.Count == 1:
                    return ReadCondition(not.Arguments[0].Value) is { } inner ? new NotCondition(inner) : null;

                case DslCallNode { Name: "all" or "any" } group:
                {
                    var parts = new List<AuthorEnemyCondition>();
                    foreach (var argument in group.Arguments)
                    {
                        if (ReadCondition(argument.Value) is not { } part)
                        {
                            return null;
                        }

                        parts.Add(part);
                    }

                    return parts.Count == 0
                        ? FailCondition($"条件 {group.Name}(…) 不能为空")
                        : group.Name == "all"
                            ? new AllCondition(parts)
                            : new AnyCondition(parts);
                }

                case DslCallNode { Name: "condition" } named:
                    return FailCondition(
                        "具名条件 condition("
                        + (named.Arguments.Count > 0 ? NameOf(named.Arguments[0].Value) : string.Empty)
                        + ") 未支持：敌人的 `conditions = [condition(Name, 谓词)]` 声明表尚未接入");

                default:
                    return FailCondition($"条件形态 '{Describe(value)}' 未支持");
            }
        }

        private AuthorEnemyCondition? FailCondition(string reason)
        {
            Problems.Add($"{enemyName}.{reason}");
            return null;
        }

        /// <summary>
        /// 创建一个分支状态并返回它的入口 id。`selfFollowUp`：池内行动完成后回到**池入口**（源码把
        /// `then=` 池里的每个行动后继都设成同一个分支，例如 `Flyconid.cs:36-38`、`Mawler.cs:35`）；
        /// 否则用显式给出的候选后继（`startWithChoice` 的开场池指向后续池）。
        /// </summary>
        private string? BranchEntry(DslValueNode value, bool selfFollowUp, string? candidateFollowUp = null)
        {
            var entry = value switch
            {
                DslCallNode { Name: "chooseWithLimits" } limits => RandomBranch(limits, null),
                DslCallNode { Name: "chooseWithoutRepeat" } noRepeat => NoRepeatBranch(noRepeat, null),
                DslCallNode { Name: "chooseByCondition" } ordered => OrderedBranch(ordered),
                _ => Fail($"'{Describe(value)}' 不是分支形态"),
            };
            if (entry is null)
            {
                return null;
            }

            var followUp = selfFollowUp ? entry : candidateFollowUp;
            if (followUp is not null)
            {
                var candidates = _states.SingleOrDefault(state => state.Id == entry) switch
                {
                    AuthorRandomState random => random.Candidates.Select(candidate => candidate.Move),
                    AuthorOrderedState ordered => ordered.Candidates.Select(candidate => candidate.Move),
                    _ => [],
                };
                foreach (var move in candidates)
                {
                    _followUp[move] = followUp;
                }
            }

            return entry;
        }

        private string? Move(string name, string? followUp)
        {
            if (name.Length == 0)
            {
                return Fail("需要一个行动名");
            }

            if (!moves.Any(move => string.Equals(move.Name, name, StringComparison.Ordinal)))
            {
                return Fail($"behavior 引用了没有声明的行动 '{name}'");
            }

            if (_states.OfType<AuthorMoveState>().All(state => state.Id != name))
            {
                _states.Add(new AuthorMoveState(name, name, string.Empty));
            }

            if (followUp is not null)
            {
                if (_followUp.TryGetValue(name, out var existing) && !string.Equals(existing, followUp, StringComparison.Ordinal))
                {
                    return Fail($"行动 '{name}' 被声明了多个不同后继（'{existing}' / '{followUp}'）");
                }

                _followUp[name] = followUp;
            }

            return name;
        }

        private string? Fail(string reason)
        {
            Problems.Add($"{enemyName}.{reason}");
            return null;
        }
    }

    private static AuthorEnemy Lower(Declaration declaration, string id, string packageName)
    {
        var qualified = id.Contains(':') ? id : $"{packageName}:{id}";
        // 正式 `.spire` 敌人一律走**状态机**（`Behavior`）：初始意图是初始状态本身（`MonsterMoveStateMachine`
        // 构造时进日志），因此 `FirstMove` 就是状态机的初始行动；`Cycle`/`RepeatFrom` 只留给冻结 `.author` 文本。
        return new AuthorEnemy(
            qualified,
            declaration.Name,
            declaration.NormalMinHp,
            declaration.NormalMaxHp,
            declaration.ToughMinHp,
            declaration.ToughMaxHp,
            0,
            0,
            0,
            declaration.Behavior.InitialMove,
            string.Empty,
            declaration.Moves,
            [],
            declaration.SourcePath,
            declaration.Line)
        {
            RepeatFrom = 0,
            StartingBlock = declaration.StartingBlock,
            StartingPowers = declaration.StartingPowers,
            HasAscensionVariants = declaration.HasAscensionVariants,
            Behavior = declaration.Behavior,
        };
    }

    // -- 小工具 ---------------------------------------------------------------------------------------------

    private static int IntOf(DslValueNode value, int fallback) =>
        value is DslNumberNode number && int.TryParse(number.Literal, out var parsed) ? parsed : fallback;

    /// <summary>
    /// ENV-T01：作者文本里的**十进制权重**字面量 → 源 float32。
    /// 用 `InvariantCulture`（DSL 字面量固定用 `.` 当小数点），按 round-to-nearest 转换；
    /// 不是数字、或不能表示成 float32 的极端字面量回落到 `fallback`。
    /// </summary>
    private static float SingleOf(DslValueNode value, float fallback) =>
        value is DslNumberNode number
        && float.TryParse(number.Literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string NameOf(DslValueNode value) => value switch
    {
        DslNameNode name => name.Name,
        DslStringNode text => text.Value,
        _ => string.Empty,
    };

    private static string Describe(DslValueNode value) => value switch
    {
        DslNameNode name => name.Name,
        DslStringNode text => text.Value,
        DslCallNode call => $"{call.Name}(…)",
        DslNumberNode number => number.Literal,
        _ => value.GetType().Name,
    };
}
