using CoreTheSpire;

namespace BaseSpire.Slice;

/// <summary>
/// W-06：**正式 0.0.1 能力（power）源 → 通用 Engine** 的作者前端。
///
/// 与卡牌前端（<see cref="SpireCardSource"/>）同一套做法：复用通用解析器 `DslSourceParser` 的 AST，
/// 只做 **AST → 共享作者模型**（<see cref="AuthorPower"/>）的降级，随后由切片运行时（Engine IR）执行；
/// 四个状态分开报：声明 / 解析 / 降级 / 未支持（逐条原因）。
///
/// 显式范围（本批次）：切片运行时**已经能执行**的能力语义
/// <list type="bullet">
/// <item>`role = X`（角色）+ `owner-turn-end = none|tick|convert-to-strength|remove`；</item>
/// <item>`outgoingDamageMultiplier = M` → `damage = multiply-outgoing:M`；</item>
/// <item>`preventEnergyGain = true` / `removeAfterOwnerTurnEnd = true`；</item>
/// <item>`trigger = afterCardExhausted|afterCardPlayed` + `whenCardType` +
///       `effect = gainBlock(N, perPowerStack=自己)` → `trigger-effect = gain-block-per-stack:N`；</item>
/// <item>`decrementOnTrigger = true` → `owner-turn-end = tick`；</item>
/// <item>`preventsBlockClear = true` → 拥有者回合开始时格挡不清零（`PreventsBlockClear`）；</item>
/// <item>`skipFirstWhenAppliedToPlayer` / `isDebuff` / `removeWhenApplierDies` 由运行时的既有规则覆盖
///       （施加到玩家身上的负面能力会跳过第一次衰减），因此不产生额外字段。</item>
/// </list>
/// 其它构造（`cardRule` / `events` / `incomingDamageModifiers` /
/// `removeAfterOpponentTurnEnd` / `effect` 的其它调用形态 …）**记为未支持并附原因**。
///
/// **角色数值表**：个别角色的数值不在正式声明里（例如 Vulnerable 的 x1.5 在已恢复的
/// `sts2/src/Core/Models/Powers/VulnerablePower.cs` 里），因此这里用一张**显式引用来源**的小表补上；
/// 表里没有的角色若缺少数值就记为未支持（不猜）。
/// </summary>
public static class SpirePowerSource
{
    public const string ResourcePrefix = "BaseSpire.ContentSource.";

    public const string ResourceSuffix = "-powers.spire";

    /// <summary>角色 → 伤害修正（数值不在正式声明里的角色；每项都引用来源）。</summary>
    private static readonly IReadOnlyDictionary<string, (string Modifier, string Value, string Source)> RoleDamage =
        new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            ["Strength"] = ("add-per-stack", "1", "sts2/src/Core/Models/Powers/StrengthPower.cs"),
            ["Vulnerable"] = ("multiply-incoming", "1.5", "sts2/src/Core/Models/Powers/VulnerablePower.cs"),
            ["Shrink"] = ("multiply-outgoing", "0.7", "sts2/src/Core/Models/Powers/ShrinkPower.cs"),
        };

    /// <summary>
    /// **规范 id 表**：正式能力名 → 内容 id。正式源用名字声明、运行时用 id，
    /// 两者并不总能互相推导（`NoEnergyGainPower`→`no-energy-gain`、`RagePower`→`rage-power`），
    /// 因此这张表是显式的；新内容（例如 BarricadePower）在这里登记 id 并注明来源。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> KnownIds =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Strength"] = "base-spire:strength",
            ["Vulnerable"] = "base-spire:vulnerable",
            ["Weak"] = "base-spire:weak",
            ["Shrink"] = "base-spire:shrink",
            ["NoDraw"] = "base-spire:no-draw",
            ["NoEnergyGainPower"] = "base-spire:no-energy-gain",
            ["FeelNoPain"] = "base-spire:feel-no-pain",
            ["RagePower"] = "base-spire:rage-power",
            // 新内容（不在冻结 id 表里）：Barricade 的卡与能力同名 id。
            ["BarricadePower"] = "base-spire:barricade",
            ["TankPower"] = "base-spire:tank-power",
            ["FlameBarrierPower"] = "base-spire:flame-barrier",
            ["DemonFormPower"] = "base-spire:demon-form",
            ["CorruptionPower"] = "base-spire:corruption-power",
            ["FreeAttackPower"] = "base-spire:free-attack-power",
            ["JuggernautPower"] = "base-spire:juggernaut-power",
            ["SelfFormingClayPower"] = "base-spire:self-forming-clay",
            // ENV-02：Colossus 的减伤能力（卡 `ironclad-cards.spire:584` 引用它）。
            ["ViciousPower"] = "base-spire:vicious-power",
            ["UnmovablePower"] = "base-spire:unmovable-power",
            ["PlatingPower"] = "base-spire:plating-power",
            ["ManglePower"] = "base-spire:mangle-power",
            ["SetupStrikePower"] = "base-spire:setup-strike-power",
            ["CrueltyPower"] = "base-spire:cruelty-power",
            ["ColossusPower"] = "base-spire:colossus-power",
            // 敌人侧 debuff：Frail（获得格挡 ×0.75、对手回合末递减）。
            ["Frail"] = "base-spire:frail",
            // 敌人侧 debuff：Constrict（拥有者回合末对自己造成 unpowered 伤害 = 层数；施加者死亡时移除）。
            ["ConstrictPower"] = "base-spire:constrict-power",
            ["Poison"] = "base-spire:poison",
            ["Accelerant"] = "base-spire:accelerant",
            ["ThornsPower"] = "base-spire:thorns-power",
        };

    /// <summary>一个能力声明在本批次支持的字段。</summary>
    public sealed record Declaration(
        string Name,
        string Id,
        int Line,
        string Role,
        string OwnerTurnEnd,
        /// <summary>`afterEnemyTurnEnd` / `afterPlayerTurnEnd` 声明的**哪一侧**回合末（"enemy"/"player"/空）。</summary>
        string TurnEndSide,
        string DamageModifier,
        string DamageModifierValue,
        string Trigger,
        string WhenCardType,
        string TriggerEffect,
        bool PreventEnergyGain,
        bool PreventsBlockClear,
        /// <summary>ENV-02：受击修正的来源条件（`sourcePower=<能力名>` 解析后的 id；空＝无此条件）。</summary>
        string DamageModifierSourcePower,
        /// <summary>ENV-02：每层给拥有者加多少最大能量（`maxEnergyPerStack`；0＝无）。</summary>
        int MaxEnergyPerStack,
        /// <summary>ENV-02：拥有者作为攻击者时把易伤倍率每层提高多少个百分点（`vulnerableMultiplierPercentPerStack`）。</summary>
        int VulnerableMultiplierPercentPerStack,
        /// <summary>ENV-02：临时力量能力每层给拥有者 `±1` 力量（`isTemporary` + `onStackChange`/`onExpire`；0＝无）。
        /// **位置必须与构造调用一致**：这些字段都是 `int`，插错位置会静默对调（本轮踩过一次）。</summary>
        int TemporaryStrengthPerStack,
        string CardRuleType,
        int CardRuleCost,
        string CardRuleResultPile,
        bool CardRuleConsumeOnPlay,
        /// <summary>ENV-02：获得格挡时的乘数（`blockGainModifiers` 的 `multiplier`；空＝无该修正）。</summary>
        string BlockGainMultiplier,
        /// <summary>ENV-02：每层豁免多少次本回合已有的"由牌获得格挡"（`priorEntriesPerStack`）。</summary>
        int BlockGainPriorEntriesPerStack,
        /// <summary>每层给拥有者自己的 powered 获得格挡加多少点（`DexterityPower.ModifyBlockAdditive`）。</summary>
        int BlockGainAdditivePerStack,
        string Effect,
        string Source)
    {
        public IReadOnlyList<PowerRule> Rules { get; init; } = [];
        public bool NegativeStacksAreInfinite { get; init; }
    }

    /// <summary>一份能力源的降级结果（四态）。</summary>
    public sealed record Source(
        string Name,
        IReadOnlyList<(string Name, int Line)> Declared,
        IReadOnlyList<Declaration> Parsed,
        IReadOnlyList<AuthorPower> Powers,
        IReadOnlyList<(string Name, string Reason)> Unsupported)
    {
        public int DeclaredCount => Declared.Count;

        public int ParsedCount => Parsed.Count;

        public int LoweredCount => Powers.Count;

        public string Summary =>
            $"{Name}: 声明 {DeclaredCount} / 解析 {ParsedCount} / 降级 {LoweredCount} / 未支持 {Unsupported.Count}";
    }

    /// <summary>本程序集里全部 `*-powers.spire` 正式能力源资源名。</summary>
    public static IReadOnlyList<string> PowerResources()
    {
        var assembly = typeof(SpirePowerSource).Assembly;
        return
        [
            .. assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                    && name.EndsWith(ResourceSuffix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>All registered power declarations, including powers colocated with enemy content.</summary>
    public static IReadOnlyList<string> PowerDeclarationResources() =>
        [.. PowerResources().Concat(SpireEnemySource.EnemyResources())
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];

    public static Source LoadResource(string resourceName,
        IReadOnlyDictionary<string, string>? ids = null)
    {
        var assembly = typeof(SpirePowerSource).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"缺少正式能力资源 '{resourceName}'");
        using var reader = new StreamReader(stream);
        var sourcePath = $"base-spire/{resourceName[ResourcePrefix.Length..]}";
        return Parse(reader.ReadToEnd(), sourcePath, ids);
    }

    public static Source Parse(string text, string sourceName,
        IReadOnlyDictionary<string, string>? ids = null) =>
        Lower(DslSourceParser.Parse(text, sourceName), sourceName, ids);

    /// <summary>
    /// 把正式 AST 降级成共享作者模型的能力声明。
    ///
    /// `ids` 是**规范 id 表**（正式能力名 → 内容 id）。正式源用 `power &lt;名字&gt;` 声明，
    /// 而运行时用的是内容 id，两者**并不总是** kebab-case 一致（`NoEnergyGainPower` → `no-energy-gain`、
    /// `RagePower` → `rage-power`），因此 id 由显式表给出；表里没有时回退到 kebab-case，
    /// 并**保留**该回退事实（调用方可以据此判断哪些 id 还没进规范表）。
    /// </summary>
    public static Source Lower(DslPackageNode package, string sourceName,
        IReadOnlyDictionary<string, string>? ids = null)
    {
        var nodes = package.Members.OfType<DslDeclarationNode>()
            .Where(node => node.Kind == "power")
            .ToList();
        var declared = nodes.Select(node => (node.Name, node.Location.Line)).ToList();
        var parsed = new List<Declaration>();
        var lowered = new List<AuthorPower>();
        var unsupported = new List<(string, string)>();
        foreach (var node in nodes)
        {
            var problems = new List<string>();
            var properties = node.Members.OfType<DslPropertyNode>().ToList();
            var declaration = ReadPower(node, properties, sourceName, ref problems);
            if (declaration is null)
            {
                unsupported.Add((node.Name, string.Join("；", problems)));
                continue;
            }

            // id 优先级：调用方给的表 → 本文件的**规范 id 表** → kebab 回退（回退会保留在 id 里，便于发现未登记项）。
            var id = ids is not null && ids.TryGetValue(node.Name, out var known)
                ? known
                : package.Name == "base-spire" && KnownIds.TryGetValue(node.Name, out var canonical)
                    ? canonical
                    : $"{package.Name}:{SpireCardSource.ToId(node.Name)}";
            id = id.Contains(':') ? id : $"{package.Name}:{id}";
            declaration = declaration with { Id = id };
            declaration = declaration with { Rules = declaration.Rules.Select(rule =>
            {
                if (rule.Reference.Length == 0) return rule;
                if (ids is not null && ids.TryGetValue(rule.Reference, out var linked)) return rule with { Reference = linked };
                if (package.Name == "base-spire" && KnownIds.TryGetValue(rule.Reference, out linked)) return rule with { Reference = linked };
                problems.Add($"{rule.Source}: unknown power reference {rule.Reference}");
                return rule;
            }).ToArray() };
            if (!HasReusableBehavior(declaration))
                problems.Add("运行时未实现声明的角色/触发器/效果组合：不注册部分生效的能力");

            parsed.Add(declaration);
            if (problems.Count > 0)
            {
                unsupported.Add((node.Name, string.Join("；", problems)));
                continue;
            }

            lowered.Add(Lower(declaration));
        }

        return new Source(sourceName, declared, parsed, lowered, unsupported);
    }

    // New content may reuse fully supported semantic patterns without adding an ID to the legacy audit table.
    // A role-only declaration still needs a implemented role; never register unknown roles as empty powers.
    private static bool HasReusableBehavior(Declaration power)
    {
        if (power.Rules.Count > 0 && power.Role.Length == 0 && power.Trigger.Length == 0) return true;
        if (power.Role.Length > 0 && power.Role is not ("Strength" or "Vulnerable" or "Shrink" or "Dexterity" or "NoDraw" or "Focus" or "Doom" or "Poison" or "Accelerant" or "Artifact")) return false;
        bool Prefix(string prefix) => power.TriggerEffect.StartsWith(prefix, StringComparison.Ordinal);
        if (power.Trigger is AuthorPowerTrigger.AfterCardExhausted or AuthorPowerTrigger.AfterCardPlayed)
            return Prefix(PowerTriggerEffect.GainBlockPerStackPrefix)
                && power.WhenCardType is "" or "Attack" or "Skill" or "Power" or "Status";
        if (power.Trigger == AuthorPowerTrigger.AfterPlayerTurnStart)
            return power.TriggerEffect.Split(PowerTriggerEffect.SequenceSeparator).All(effect =>
                effect.StartsWith(PowerTriggerEffect.GainPowerPerStackPrefix, StringComparison.Ordinal)
                || effect.StartsWith(PowerTriggerEffect.GainBlockPrefix, StringComparison.Ordinal)
                || effect is PowerTriggerEffect.RemoveSelf or PowerTriggerEffect.DecrementSelf);
        if (power.Trigger == AuthorPowerTrigger.AfterBlockGained) return Prefix(PowerTriggerEffect.RandomAttackPerStackPrefix);
        if (power.Trigger == AuthorPowerTrigger.AfterDamageTaken) return Prefix(PowerTriggerEffect.RetaliatePerStackPrefix);
        if (power.Trigger.StartsWith(AuthorPowerTrigger.AfterPowerAppliedPrefix, StringComparison.Ordinal))
            return Prefix(PowerTriggerEffect.DrawPerStackPrefix);
        if (power.Trigger.Length > 0) return false;
        return power.PreventEnergyGain || power.PreventsBlockClear || power.MaxEnergyPerStack != 0
            || power.CardRuleType is "Attack" or "Skill" or "Power" or "Status"
            || power.DamageModifier.Length > 0 || power.BlockGainMultiplier.Length > 0
            || power.BlockGainAdditivePerStack != 0 || power.Role is "NoDraw" or "Focus" or "Doom" or "Poison" or "Accelerant" or "Artifact"
            || power.TemporaryStrengthPerStack != 0 || power.VulnerableMultiplierPercentPerStack != 0
            || power.OwnerTurnEnd is PowerTurnEnd.Tick or PowerTurnEnd.Remove or PowerTurnEnd.RemoveAtOpponentTurnEnd or PowerTurnEnd.ConvertToStrength
            || power.OwnerTurnEnd.StartsWith(PowerTriggerEffect.GainPowerPerStackPrefix, StringComparison.Ordinal)
            || power.OwnerTurnEnd.StartsWith(PowerTurnEnd.GainBlockPerStackPrefix, StringComparison.Ordinal)
            || power.OwnerTurnEnd.StartsWith(PowerTurnEnd.DamageOwnerUnpoweredPrefix, StringComparison.Ordinal);
    }

    private static Declaration? ReadPower(DslDeclarationNode node, IReadOnlyList<DslPropertyNode> properties,
        string sourcePath, ref List<string> problems)
    {
        IReadOnlyList<PowerRule> rules = [];
        var role = string.Empty;
        var ownerTurnEnd = string.Empty;
        var damageModifier = string.Empty;
        var damageValue = string.Empty;
        var trigger = string.Empty;
        var turnEndSide = string.Empty;
        var whenCardType = string.Empty;
        var triggerEffect = string.Empty;
        var preventEnergyGain = false;
        var negativeStacksAreInfinite = false;
        var modifierSourcePower = string.Empty;
        var blockGainMultiplier = string.Empty;
        var blockGainPriorEntries = 0;
        // `role = Dexterity`：每层给拥有者自己的 powered 获得格挡 **+1**（来源
        // `DexterityPower.ModifyBlockAdditive`：`block + Amount`）。加性阶段在乘性阶段之前。
        var blockGainAdditive = 0;
        var maxEnergyPerStack = 0;
        var vulnerablePercentPerStack = 0;
        var isTemporary = false;
        var stackChangeSign = 0;
        var expireSign = 0;
        var preventsBlockClear = false;
        var cardRuleType = string.Empty;
        var cardRuleCost = -1;
        var cardRuleResultPile = string.Empty;
        var cardRuleConsumeOnPlay = false;
        var effectText = string.Empty;
        foreach (var property in properties)
        {
            switch (property.Name)
            {
                case "rules":
                    rules = PowerRuleSource.Read(property.Value, problems);
                    break;
                case "role":
                    role = NameOf(property.Value);
                    break;
                case "preventEnergyGain":
                    preventEnergyGain = property.Value is DslBooleanNode { Value: true };
                    break;
                case "preventsBlockClear":
                    preventsBlockClear = property.Value is DslBooleanNode { Value: true };
                    break;
                case "isTemporary":
                    // ENV-02：`isTemporary = true`（来源 TemporaryStrengthPower）——记号位，真正的语义由
                    // `onStackChange`/`onExpire` 两个方向共同决定（下面解析），这里只做"确实是 true"的校验。
                    if (property.Value is not DslBooleanNode { Value: true })
                    {
                        problems.Add($"{node.Name}.isTemporary 只支持 true");
                    }

                    isTemporary = true;
                    break;
                case "onStackChange":
                    // `grantPower(Strength, stackDelta)` ⇒ 持有期间 +1/层；`negativeStackDelta` ⇒ −1/层。
                    stackChangeSign = StrengthGrantSign(property.Value, node.Name, "onStackChange",
                        positive: "stackDelta", negative: "negativeStackDelta", ref problems);
                    break;
                case "onExpire":
                    // 到期回补的是**相反**方向：`negativeSourceStacks` ⇒ 持有期间 +1/层；`sourceStacks` ⇒ −1/层。
                    expireSign = StrengthGrantSign(property.Value, node.Name, "onExpire",
                        positive: "negativeSourceStacks", negative: "sourceStacks", ref problems);
                    break;
                case "vulnerableMultiplierPercentPerStack":
                    // ENV-02：`vulnerableMultiplierPercentPerStack = 1` → 拥有者作为攻击者时易伤倍率 +1%/层
                    //（来源 CrueltyPower.ModifyVulnerableMultiplier）。
                    vulnerablePercentPerStack = IntOf(property.Value);
                    break;
                case "maxEnergyPerStack":
                    // ENV-02：`maxEnergyPerStack = 1` → 每层给拥有者 +1 最大能量（来源 PyrePower.ModifyMaxEnergy）。
                    if (IntOf(property.Value) is var energy && energy >= 0)
                    {
                        maxEnergyPerStack = energy;
                    }
                    else
                    {
                        problems.Add($"{node.Name}.maxEnergyPerStack 取值无效");
                    }

                    break;
                case "removeAfterOwnerTurnEnd":
                case "removeAfterPlayerTurnEnd":
                    if (property.Value is DslBooleanNode { Value: true })
                    {
                        ownerTurnEnd = "remove";
                    }

                    break;
                case "removeAfterOpponentTurnEnd":
                    if (property.Value is DslBooleanNode { Value: true })
                    {
                        // W-06：**对手**回合末移除（FlameBarrierPower）——玩家持有的这类能力在敌人回合末才消失，
                        // 因此它覆盖敌人的整个回合。
                        ownerTurnEnd = "remove-at-opponent-turn-end";
                    }

                    break;
                case "events":
                    ReadEvents(property.Value, node.Name, ref trigger, ref triggerEffect, ref problems);
                    break;
                case "cardRule":
                    ReadCardRule(property.Value, node.Name, ref cardRuleType, ref cardRuleCost,
                        ref cardRuleResultPile, ref cardRuleConsumeOnPlay, ref problems);
                    break;
                case "negativeStacksAreInfinite":
                    if (property.Value is DslBooleanNode declaredInfinite) negativeStacksAreInfinite = declaredInfinite.Value;
                    else problems.Add($"{node.Name}.negativeStacksAreInfinite requires a boolean");
                    break;
                case "decrementOnTrigger":
                    if (property.Value is DslBooleanNode { Value: true })
                    {
                        ownerTurnEnd = "tick";
                    }

                    break;
                case "skipFirstWhenAppliedToPlayer":
                case "skipFirstWhenAppliedToEnemy":
                case "isDebuff":
                case "removeWhenApplierDies":
                    // 运行时既有规则：施加到玩家身上的负面能力跳过第一次衰减；施加者死亡时的移除由
                    // 施加者生命周期处理。两者都不需要额外字段。
                    //
                    // 已知范围（显式记账）：「跳过第一次衰减」的判定落在**施加点**（玩家负面能力与
                    // `Ritual` 步骤各自传 skip），因此 `skipFirstWhenAppliedToEnemy` 目前只被**识别**、
                    // 不改变出生期施加的行为——后果是"出生自带的 tick 型能力在敌人第一个回合末就会递减一次"
                    // （`SliceConstrictChecks` 的 startingPowers 检查把这条行为钉住了）。
                    break;
                case "outgoingDamageMultiplier":
                    damageModifier = "multiply-outgoing";
                    damageValue = NameOf(property.Value);
                    if (ownerTurnEnd.Length == 0)
                    {
                        ownerTurnEnd = "tick";
                    }

                    break;
                case "incomingDamageModifiers":
                    // `incomingDamageModifiers = [modifier(multiplier=2, require=[move], exclude=[unpowered])]`
                    // → 受击伤害 ×multiplier。运行时既有 `multiply-incoming` 正是"受击方侧乘、只作用于
                    // powered 攻击"（与 require=[move]/exclude=[unpowered] 同义），因此是**忠实映射**。
                    // `sourcePower=<能力名>` 额外要求**攻击者**身上有该能力（ColossusPower 的易伤条件）。
                    ReadIncomingModifier(property.Value, node.Name, ref damageModifier, ref damageValue,
                        ref modifierSourcePower, ref problems);
                    break;
                case "blockGainModifiers":
                    // ENV-02：`blockGainModifiers = [modifier(multiplier=2, require=[move],
                    // priorEntriesPerStack=1, playerOnly=true)]`（UnmovablePower）
                    // → "本回合由牌获得格挡的前 N 次翻倍"。
                    ReadBlockGainModifier(property.Value, node.Name, ref blockGainMultiplier,
                        ref blockGainPriorEntries, ref problems);
                    break;
                case "blockGainMultiplier":
                    // 标量形态：`blockGainMultiplier = 0.75`（FrailPower）→ 无条件乘 0.75。
                    ReadScalarBlockGainMultiplier(property.Value, node.Name, ref blockGainMultiplier,
                        ref blockGainPriorEntries, ref problems);
                    break;
                case "trigger":
                    var (kind, value) = TriggerOf(NameOf(property.Value), ref problems);
                    if (kind == "event")
                    {
                        trigger = value;
                    }
                    else if (kind == "turnEndTick")
                    {
                        // 回合末衰减：与冻结 `.author` 的 `owner-turn-end = tick` 同一语义。
                        // **哪一侧的回合末**必须保留：`afterEnemyTurnEnd`（Vulnerable/Weak/Frail）在**敌人**侧
                        // 回合末递减，`afterPlayerTurnEnd`（NoDraw）在**玩家**侧回合末递减。
                        turnEndSide = NameOf(property.Value) == "afterPlayerTurnEnd" ? "player" : "enemy";
                        if (ownerTurnEnd.Length == 0)
                        {
                            ownerTurnEnd = value;
                        }
                    }

                    break;
                case "whenCardType":
                    whenCardType = Capitalize(NameOf(property.Value));
                    break;
                case "effect":
                    effectText = Describe(property.Value);
                    if (!ReadSingleEffect(property.Value, node.Name, ref triggerEffect, ref problems))
                    {
                        problems.Add($"effect '{effectText}' 未支持（本批次只支持 "
                            + "gainBlock(N, perPowerStack=自己)、grantPower(<能力>, sourceStacks)、"
                            + "decrementPower(自己) 与 sequence(...)）");
                    }

                    break;
                case "on":
                    // ENV-02：`on = [trigger(event=…, effect=…), …]`（多触发点声明形状）。
                    ReadOn(property.Value, node.Name, ref trigger, ref triggerEffect, ref ownerTurnEnd, ref problems);
                    break;
                default:
                    problems.Add($"能力字段 '{property.Name}' 未支持");
                    break;
            }
        }

        // ENV-02：临时力量能力必须**三件齐备且方向自洽**，否则整条记为未支持：
        // ① `isTemporary = true`；② `onStackChange` 与 `onExpire` 给出同一符号（一个正向一个反向）；
        // ③ 声明了移除时点（`removeAfterOwnerTurnEnd`/`removeAfterPlayerTurnEnd`）——**回补发生在移除处**，
        //    没有移除就没有回补点，此时按"给了永久力量"实现会与来源不符，因此宁可记为未支持。
        var temporaryStrength = 0;
        if (isTemporary || stackChangeSign != 0 || expireSign != 0)
        {
            var hasRemoval = ownerTurnEnd.Length > 0;
            if (!isTemporary)
            {
                problems.Add($"{node.Name} 声明了 onStackChange/onExpire 却没有 isTemporary = true");
            }
            else if (stackChangeSign == 0 || expireSign == 0)
            {
                problems.Add($"{node.Name} 是临时力量能力，但 onStackChange/onExpire 不完整或形态未支持");
            }
            else if (stackChangeSign != expireSign)
            {
                problems.Add($"{node.Name} 的 onStackChange 与 onExpire 方向不一致"
                    + $"（{stackChangeSign} vs {expireSign}）：无法确定持有期间给的是正还是负力量");
            }
            else if (!hasRemoval)
            {
                problems.Add($"{node.Name} 是临时力量能力但没有声明移除时点"
                    + "（回补发生在移除处）：本批次记为未支持，避免降级成'永久力量'");
            }
            else
            {
                temporaryStrength = stackChangeSign;
            }
        }

        // "声明了但没有任何可识别行为"必须被拒绝（否则会降级出一个不生效的空能力）。
        // 新机制落地后要把它的字段加进来，否则能力会被这条守卫挡住——PyrePower 就是这样被挡住的。
        if (problems.Count == 0 && rules.Count == 0 && role.Length == 0 && trigger.Length == 0 && !preventEnergyGain
            && !preventsBlockClear && maxEnergyPerStack == 0 && vulnerablePercentPerStack == 0
            && temporaryStrength == 0
            && blockGainMultiplier.Length == 0
            && damageModifier.Length == 0
            && cardRuleType.Length == 0
            // 回合末行为（tick / convert-to-strength / gain-block-per-stack / damage-owner-unpowered …）
            // 也是行为：它们只在拥有者回合末展开，没有别的字段可依赖。
            && ownerTurnEnd.Length == 0)
        {
            problems.Add("既没有 role，也没有 trigger/owner-turn-end/preventEnergyGain/伤害修正：无法确定行为");
        }

        // 角色：`Dexterity` 是**格挡加性**角色（每层 +1），不是伤害修正角色。
        if (string.Equals(role, "Dexterity", StringComparison.Ordinal))
        {
            blockGainAdditive = 1;
            effectText = effectText.Length == 0
                ? "Dexterity: 每层给拥有者自己的 powered 获得格挡 +1（DexterityPower.ModifyBlockAdditive）"
                : effectText;
        }

        // 角色数值表：正式声明里没有数值的角色，从**显式引用来源**的小表里取。
        if (damageModifier.Length == 0 && role.Length > 0
            && RoleDamage.TryGetValue(role, out var entry))
        {
            damageModifier = entry.Modifier;
            damageValue = entry.Value;
            effectText = effectText.Length == 0
                ? $"{role}: {entry.Modifier} {entry.Value}（数值来自 {entry.Source}）"
                : effectText;
        }

        return new Declaration(node.Name, SpireCardSource.ToId(node.Name), node.Location.Line, role,
            ownerTurnEnd.Length == 0 ? "none" : ownerTurnEnd, turnEndSide, damageModifier, damageValue, trigger,
            whenCardType, triggerEffect, preventEnergyGain, preventsBlockClear, modifierSourcePower,
            maxEnergyPerStack, vulnerablePercentPerStack, temporaryStrength, cardRuleType,
            cardRuleCost, cardRuleResultPile, cardRuleConsumeOnPlay, blockGainMultiplier,
            blockGainPriorEntries, blockGainAdditive, effectText, sourcePath)
            { Rules = rules, NegativeStacksAreInfinite = negativeStacksAreInfinite };
    }

    /// <summary>
    /// 正式能力名 → 规范 id（**大小写不敏感**；表里没有时返回 false，调用方据此把"没登记的能力"记为未支持）。
    /// `extraIds` 允许调用方补充额外的 id 表。
    /// </summary>
    public static bool TryResolveId(string name, out string id,
        IReadOnlyDictionary<string, string>? extraIds = null)
    {
        if (extraIds is not null)
        {
            foreach (var entry in extraIds)
            {
                if (string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    id = entry.Value;
                    return true;
                }
            }
        }

        foreach (var entry in KnownIds)
        {
            if (string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                id = entry.Value;
                return true;
            }
        }

        id = string.Empty;
        return false;
    }

    /// <summary>
    /// 触发器分两类：
    /// * **时长类**（回合末衰减/移除）→ 映射到运行时的 `owner-turn-end`（`tick`/`remove`），
    ///   与冻结 `.author` 的 Vulnerable/Weak/NoDraw 编码一致；
    /// * **事件类**（打出牌/消耗牌之后）→ 运行时的 `trigger`。
    /// </summary>
    /// <summary>
    /// `effect = grantPower(&lt;PowerName&gt;, sourceStacks)` → `gain-power-per-stack:1:&lt;power-id&gt;`。
    /// 被施加的能力 id 在**降级时**就解析好（正式源用名字，运行时用 id）。
    /// 其它形态（`grantPower(x, 常量)`、`sequence(...)` 等）记为未支持。
    /// </summary>
    private static bool ReadGrantPowerEffect(DslValueNode value, string powerName, ref string triggerEffect,
        ref List<string> problems)
    {
        if (value is not DslCallNode call || call.Name != "grantPower")
        {
            return false;
        }

        var positional = call.Arguments.Where(item => item.Name is null).Select(item => NameOf(item.Value))
            .ToList();
        var grantedName = positional.Count > 0 ? positional[0] : string.Empty;
        var byOwnStacks = positional.Count > 1 && positional[1] == "sourceStacks";
        foreach (var argument in call.Arguments)
        {
            if (argument.Name == "perPowerStack")
            {
                byOwnStacks = NameOf(argument.Value) == "sourceStacks";
            }
        }

        if (grantedName.Length == 0 || !byOwnStacks)
        {
            problems.Add($"{powerName}.effect grantPower 形态未支持（只支持 grantPower(<能力>, sourceStacks)）");
            return true;
        }

        if (!KnownIds.TryGetValue(grantedName, out var grantedId))
        {
            problems.Add($"grantPower 引用的能力 '{grantedName}' 不在规范 id 表里，无法链接");
            return true;
        }

        triggerEffect = $"gain-power-per-stack:1:{grantedId}";
        return true;
    }

    /// <summary>
    /// `effect = sequence(A, B, …)` → 用 `|` 连接的各效果（**声明顺序即执行顺序**）。
    /// 本批次支持序列里的：`gainBlock(n, target=self)` → `gain-block:n`、
    /// `removePower(自己)` → `remove-self`、以及 `grantPower(<能力>, sourceStacks)`。
    /// </summary>
    private static bool ReadSequenceEffect(DslValueNode value, string powerName, ref string triggerEffect,
        ref List<string> problems)
    {
        if (value is not DslCallNode call || call.Name != "sequence")
        {
            return false;
        }

        var parts = new List<string>();
        foreach (var argument in call.Arguments)
        {
            if (argument.Value is not DslCallNode step)
            {
                problems.Add($"{powerName}.effect sequence 的成员不是调用");
                return true;
            }

            switch (step.Name)
            {
                case "gainBlock":
                {
                    // 只支持"固定数值、目标是自己"的形态（按层数的形态走单效果路径）。
                    var amount = step.Arguments.FirstOrDefault(item => item.Name is null) is { } positional
                        ? IntOf(positional.Value)
                        : 0;
                    var targetsSelf = step.Arguments.Any(item => item.Name == "target"
                        && NameOf(item.Value) == "self");
                    var perStack = step.Arguments.Any(item => item.Name == "perPowerStack");
                    if (perStack || (amount <= 0 && !targetsSelf))
                    {
                        problems.Add($"{powerName}.effect sequence 里的 gainBlock 形态未支持"
                            + "（只支持固定数值 gainBlock(n, target=self)）");
                        return true;
                    }

                    parts.Add($"{PowerTriggerEffect.GainBlockPrefix}{amount}");
                    break;
                }
                case "removePower":
                {
                    var targetIsSelf = step.Arguments.Any(item => NameOf(item.Value) == powerName);
                    if (!targetIsSelf)
                    {
                        problems.Add($"{powerName}.effect sequence 里的 removePower 目标未支持（只支持自己）");
                        return true;
                    }

                    parts.Add(PowerTriggerEffect.RemoveSelf);
                    break;
                }
                case "decrementPower":
                {
                    // ENV-02：`sequence(…, decrementPower(自己))`（与单效果路径同一语义）。
                    var targetIsSelf = step.Arguments.Any(item => NameOf(item.Value) == powerName);
                    if (!targetIsSelf)
                    {
                        problems.Add($"{powerName}.effect sequence 里的 decrementPower 目标未支持（只支持自己）");
                        return true;
                    }

                    parts.Add(PowerTriggerEffect.DecrementSelf);
                    break;
                }
                case "grantPower":
                {
                    var before = triggerEffect;
                    triggerEffect = string.Empty;
                    _ = ReadGrantPowerEffect(step, powerName, ref triggerEffect, ref problems);
                    if (triggerEffect.Length == 0)
                    {
                        triggerEffect = before;
                        return true;
                    }

                    parts.Add(triggerEffect);
                    triggerEffect = before;
                    break;
                }
                default:
                    problems.Add($"{powerName}.effect sequence 里的 '{step.Name}' 未支持");
                    return true;
            }
        }

        if (parts.Count == 0)
        {
            problems.Add($"{powerName}.effect sequence 为空");
            return true;
        }

        triggerEffect = string.Join(PowerTriggerEffect.SequenceSeparator, parts);
        return true;
    }

    private static (string Kind, string Value) TriggerOf(string raw, ref List<string> problems) => raw switch
    {
        "afterCardExhausted" => ("event", "after_card_exhausted"),
        "afterCardPlayed" => ("event", "after_card_played"),
        "afterPlayerTurnStart" or "afterOwnerTurnStart" => ("event", "after_player_turn_start"),
        "afterEnemyTurnEnd" or "afterPlayerTurnEnd" => ("turnEndTick", "tick"),
        _ => ("unsupported", Unsupported(raw, ref problems)),
    };

    /// <summary>
    /// `cardRule = cardRule(type=skill, cost=0, resultPile=exhaust)`（CorruptionPower）/
    /// `cardRule(type=attack, cost=0, consumeOnPlay=true)`（FreeAttackPower）
    /// → 牌规则字段（按**牌类型**改写费用 / 结果区 / 打出后消耗该能力）。
    ///
    /// 限制（显式）：只接受**降低**费用的规则（guard 是表达式，不能先算局部变量）；
    /// `extraPlays` / `consumeOnPlayCount`（OneTwoPunch）需要"额外打出"机制，本批次记为未支持。
    /// </summary>
    private static void ReadCardRule(DslValueNode value, string powerName, ref string cardRuleType,
        ref int cardRuleCost, ref string cardRuleResultPile, ref bool cardRuleConsumeOnPlay,
        ref List<string> problems)
    {
        if (value is not DslCallNode call || call.Name != "cardRule")
        {
            problems.Add($"{powerName}.cardRule 未支持（只支持 cardRule(type=…, …)）");
            return;
        }

        var cardKind = string.Empty;
        var baseCost = -1;
        foreach (var argument in call.Arguments)
        {
            switch (argument.Name)
            {
                case "type":
                    cardKind = NameOf(argument.Value) switch
                    {
                        "attack" => "Attack",
                        "skill" => "Skill",
                        "power" => "Power",
                        "status" => "Status",
                        var other => other,
                    };
                    break;
                case "cost":
                    baseCost = IntOf(argument.Value);
                    break;
                case "resultPile":
                    cardRuleResultPile = NameOf(argument.Value);
                    if (cardRuleResultPile != "exhaust")
                    {
                        problems.Add($"{powerName}.cardRule 的 resultPile '{cardRuleResultPile}' 未支持"
                            + "（只支持 exhaust）");
                    }

                    break;
                case "consumeOnPlay":
                    cardRuleConsumeOnPlay = argument.Value is DslBooleanNode { Value: true };
                    break;
                case "extraPlays":
                case "consumeOnPlayCount":
                    problems.Add($"{powerName}.cardRule 的 '{argument.Name}' 未支持（需要「额外打出」机制）");
                    break;
                default:
                    problems.Add($"{powerName}.cardRule 参数 '{argument.Name}' 未支持");
                    break;
            }
        }

        if (cardKind.Length == 0)
        {
            problems.Add($"{powerName}.cardRule 缺少 type");
            return;
        }

        cardRuleType = cardKind;
        cardRuleCost = baseCost;
        // 费用语义按"**降低**费用"使用：付款取 min(基础, 规则)，guard 用"按基础够或按规则够"。
        // 提高费用的规则不在本批次支持范围内（语料里也没有），但其存在不会让 checks 通过错误行为。
    }

    /// <summary>
    /// `events = [event(type=damage, owner=target, require=[move], exclude=[unpowered],
    /// positiveAmount=false, effect=scaledAttack(0, 1, count=targetPowerAmount(自己, target=self),
    /// target=target, properties=[unpowered]))]`
    /// → `trigger = after_damage_taken` + `trigger-effect = retaliate-per-stack:perCount`。
    ///
    /// 本批次只接受这一形态（"受到 powered 正数伤害后按自身层数反击"）：事件类型、owner、
    /// require/exclude、数值来源与目标都要逐一匹配，否则记为未支持（不猜语义）。
    /// </summary>
    private static void ReadEvents(DslValueNode value, string powerName, ref string trigger,
        ref string triggerEffect, ref List<string> problems)
    {
        if (value is not DslListNode list || list.Items.Count != 1 || list.Items[0] is not DslCallNode eventCall)
        {
            problems.Add($"{powerName}.events 未支持（本批次只支持单个 event(...)）");
            return;
        }

        if (eventCall.Name != "event")
        {
            problems.Add($"{powerName}.events 的调用 '{eventCall.Name}' 未支持（只支持 event）");
            return;
        }

        DslCallNode? effect = null;
        var blockEvent = false;
        // ENV-02：`type=power` 的事件（ViciousPower）——被监视的能力 id。
        var powerEvent = false;
        var watchedPowerId = string.Empty;
        foreach (var argument in eventCall.Arguments)
        {
            switch (argument.Name)
            {
                case "type":
                    if (NameOf(argument.Value) == "block")
                    {
                        blockEvent = true;
                        break;
                    }

                    if (NameOf(argument.Value) == "power")
                    {
                        // ENV-02：`type=power`（ViciousPower）——"施加了指定能力之后"。
                        // 形态：`event(type=power, owner=source, power=<能力名>, ownerTurnOnly=<bool>,
                        // effect=draw(scaled(0, n, count=targetPowerAmount(<自己>, target=self))))`。
                        powerEvent = true;
                        break;
                    }

                    if (NameOf(argument.Value) != "damage")
                    {
                        problems.Add($"{powerName}.events 的事件类型 '{NameOf(argument.Value)}' 未支持"
                            + "（只支持 damage / block / power）");
                        return;
                    }

                    break;
                case "power" when powerEvent:
                    // 被监视的能力名 → 规范 id（与其它能力引用同一套解析；必须是**已登记**的限定 id）。
                    if (!TryResolveId(NameOf(argument.Value), out var watchedId) || !watchedId.Contains(':'))
                    {
                        problems.Add($"{powerName}.events 的 power '{NameOf(argument.Value)}' "
                            + "无法链接到规范 id 表里已登记的能力");
                        return;
                    }

                    watchedPowerId = watchedId;
                    break;
                case "ownerTurnOnly" when powerEvent:
                    // 当前内容里玩家不会在敌人回合施加易伤，因此该开关不改变结果；显式接受并记录。
                    break;
                case "owner":
                    if (NameOf(argument.Value) is not ("target" or "source"))
                    {
                        problems.Add($"{powerName}.events 的 owner '{NameOf(argument.Value)}' 未支持"
                            + "（只支持 target / source）");
                        return;
                    }

                    break;
                case "require":
                    if (!NamesIn(argument.Value).SequenceEqual(["move"], StringComparer.Ordinal))
                    {
                        problems.Add($"{powerName}.events 的 require 未支持（只支持 [move]）");
                        return;
                    }

                    break;
                case "exclude":
                    if (!NamesIn(argument.Value).SequenceEqual(["unpowered"], StringComparer.Ordinal))
                    {
                        problems.Add($"{powerName}.events 的 exclude 未支持（只支持 [unpowered]）");
                        return;
                    }

                    break;
                case "positiveAmount":
                    if (argument.Value is not DslBooleanNode { Value: false })
                    {
                        problems.Add($"{powerName}.events 的 positiveAmount 未支持（只支持 false）");
                        return;
                    }

                    break;
                case "effect":
                    effect = argument.Value as DslCallNode;
                    break;
                default:
                    problems.Add($"{powerName}.events 参数 '{argument.Name}' 未支持");
                    return;
            }
        }

        if (effect is null)
        {
            problems.Add($"{powerName}.events 缺少 effect");
            return;
        }

        if (powerEvent)
        {
            // ENV-02：`type=power, owner=source, power=<能力>, effect=draw(scaled(0, n,
            // count=targetPowerAmount(自己, target=self)))` → "施加该能力后按自身层数抽 n×层数 张牌"。
            if (watchedPowerId.Length == 0)
            {
                problems.Add($"{powerName}.events 的 power 事件缺少 power=<能力名>");
                return;
            }

            if (effect.Name != "draw")
            {
                problems.Add($"{powerName}.events 的 power 事件 effect '{effect.Name}' 未支持（只支持 draw）");
                return;
            }

            var drawShape = ReadScaledAttackShape(effect, powerName, ref problems);
            if (drawShape is null)
            {
                return;
            }

            var (drawBase, drawPer, drawSelf, _, _) = drawShape.Value;
            if (drawBase != 0 || drawPer <= 0 || !drawSelf)
            {
                problems.Add($"{powerName}.events 的 draw 形态未支持（只支持 "
                    + "scaled(0, n, count=targetPowerAmount(自己, target=self))）");
                return;
            }

            trigger = $"{AuthorPowerTrigger.AfterPowerAppliedPrefix}{watchedPowerId}";
            triggerEffect = $"{PowerTriggerEffect.DrawPerStackPrefix}{drawPer}";
            return;
        }

        if (blockEvent)
        {
            // `type=block, owner=source, effect=randomAttack(scaled(0, n, count=targetPowerAmount(自己, self)),
            // properties=[unpowered])` → 每次获得格挡随机打一个敌人（JuggernautPower）。
            if (effect.Name != "randomAttack")
            {
                problems.Add($"{powerName}.events 的 block 事件 effect '{effect.Name}' 未支持（只支持 randomAttack）");
                return;
            }

            var blockShape = ReadScaledAttackShape(effect, powerName, ref problems);
            if (blockShape is null)
            {
                return;
            }

            var (blockBase, blockPer, blockSelf, blockUnpowered, _) = blockShape.Value;
            if (blockBase != 0 || blockPer <= 0 || !blockSelf || !blockUnpowered)
            {
                problems.Add($"{powerName}.events 的 randomAttack 形态未支持（只支持 "
                    + "scaled(0, n, count=targetPowerAmount(自己, target=self)) + properties=[unpowered]）");
                return;
            }

            trigger = "after_block_gained";
            triggerEffect = $"random-attack-per-stack:{blockPer}";
            return;
        }

        if (effect.Name != "scaledAttack")
        {
            problems.Add($"{powerName}.events 的 effect 未支持（只支持 scaledAttack(...) / randomAttack(...)）");
            return;
        }

        var shape = ReadScaledAttackShape(effect, powerName, ref problems);
        if (shape is null)
        {
            return;
        }

        var (baseValue, perCount, countIsSelf, unpowered, targetIsEventTarget) = shape.Value;
        if (baseValue != 0 || perCount <= 0 || !countIsSelf || !unpowered || !targetIsEventTarget)
        {
            problems.Add($"{powerName}.events 的 effect 形态未支持（本批次只支持 "
                + "scaledAttack(0, n, count=targetPowerAmount(自己, target=self), target=target, "
                + "properties=[unpowered])）");
            return;
        }

        trigger = "after_damage_taken";
        triggerEffect = $"retaliate-per-stack:{perCount}";
    }

    /// <summary>
    /// 读 `scaledAttack(base, perCount, count=targetPowerAmount(自己, target=self), …, properties=[unpowered])`
    /// 与 `randomAttack(scaled(base, perCount, count=targetPowerAmount(自己, target=self)), properties=[unpowered])`
    /// 两种形态（后者把 scaled(...) 放在第一个位置参数里）。
    /// </summary>
    private static (int Base, int PerCount, bool CountIsSelf, bool Unpowered, bool TargetIsEventTarget)?
        ReadScaledAttackShape(DslCallNode effect, string powerName, ref List<string> problems)
    {
        DslCallNode? scaling = effect.Name switch
        {
            "scaledAttack" => effect,
            "randomAttack" => effect.Arguments.Where(item => item.Name is null)
                .Select(item => item.Value as DslCallNode).FirstOrDefault(call => call?.Name == "scaled"),
            // ENV-02：`draw(scaled(0, n, count=targetPowerAmount(自己, target=self)))`（ViciousPower）
            // ——与 randomAttack 同一形态：缩放量写在**内层** `scaled` 调用里。
            "draw" => effect.Arguments.Where(item => item.Name is null)
                .Select(item => item.Value as DslCallNode).FirstOrDefault(call => call?.Name == "scaled"),
            _ => null,
        };
        if (scaling is null)
        {
            problems.Add($"{powerName}.events 的 effect '{effect.Name}' 未支持"
                + "（只支持 scaledAttack(...) / randomAttack(scaled(...)) / draw(scaled(...))）");
            return null;
        }

        var baseValue = 0;
        var perCount = 0;
        var countIsSelf = false;
        var unpowered = false;
        var targetIsEventTarget = effect.Name == "randomAttack";   // randomAttack 自带随机目标
        var positional = 0;
        foreach (var argument in scaling.Arguments)
        {
            if (argument.Name is null)
            {
                if (positional == 0)
                {
                    baseValue = IntOf(argument.Value);
                }
                else if (positional == 1)
                {
                    perCount = IntOf(argument.Value);
                }

                positional++;
                continue;
            }

            switch (argument.Name)
            {
                case "count":
                    countIsSelf = argument.Value is DslCallNode countCall
                        && countCall.Name == "targetPowerAmount"
                        && countCall.Arguments.Any(item => item.Value is DslNameNode { Name: var name }
                            && name == powerName)
                        && countCall.Arguments.Any(item => item.Name == "target"
                            && NameOf(item.Value) == "self");
                    break;
                case "target":
                    targetIsEventTarget = NameOf(argument.Value) == "target";
                    break;
                case "properties":
                    unpowered = NamesIn(argument.Value).Contains("unpowered", StringComparer.Ordinal);
                    break;
                default:
                    problems.Add($"{powerName}.events 的 effect 参数 '{argument.Name}' 未支持");
                    return null;
            }
        }

        // `randomAttack(...)` 的 properties 在**外层**调用上（scaled 里面没有）。
        foreach (var argument in effect.Arguments.Where(item => item.Name == "properties"))
        {
            unpowered |= NamesIn(argument.Value).Contains("unpowered", StringComparer.Ordinal);
        }

        return (baseValue, perCount, countIsSelf, unpowered, targetIsEventTarget);
    }

    /// <summary>
    /// ENV-02：从 `grantPower(Strength, &lt;值源&gt;)` 里读出"每层给多少力量"的符号。
    /// `positive` 是表示 +1 的值源名，`negative` 是表示 −1 的；别的形态返回 0 并记未支持。
    /// 只接受力量与这两个值源：别的组合（例如给别的能力、别的来源）本批次记为未支持。
    /// </summary>
    private static int StrengthGrantSign(DslValueNode value, string powerName, string field,
        string positive, string negative, ref List<string> problems)
    {
        if (value is not DslCallNode { Name: "grantPower" } call)
        {
            problems.Add($"{powerName}.{field} 未支持（只支持 grantPower(Strength, {positive}|{negative})）");
            return 0;
        }

        // 声明形式是 `grantPower(<能力名>, <值源>)`：**两个位置参数**（第一个是能力、第二个是值源），
        // 不写成命名参数。这里按顺序取，别把它当成"命名 source"。
        var positionals = call.Arguments.Where(argument => argument.Name is null)
            .Select(argument => NameOf(argument.Value)).ToList();
        var power = positionals.Count > 0 ? positionals[0] : string.Empty;
        var source = positionals.Count > 1 ? positionals[1] : string.Empty;

        if (!string.Equals(power, "Strength", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"{powerName}.{field} 给的不是 Strength（{power}）：本批次只支持临时力量");
            return 0;
        }

        if (string.Equals(source, positive, StringComparison.Ordinal))
        {
            return 1;
        }

        if (string.Equals(source, negative, StringComparison.Ordinal))
        {
            return -1;
        }

        problems.Add($"{powerName}.{field} 的值源 '{source}' 未支持（只支持 {positive}/{negative}）");
        return 0;
    }

    /// <summary>把一个效果节点读成 `trigger-effect`（四种形态；顺序即优先级）。单效果与 `sequence` 共用。</summary>
    private static bool ReadSingleEffect(DslValueNode value, string powerName, ref string triggerEffect,
        ref List<string> problems) =>
        ReadGainBlockEffect(value, powerName, ref triggerEffect, ref problems)
        || ReadGrantPowerEffect(value, powerName, ref triggerEffect, ref problems)
        || ReadSequenceEffect(value, powerName, ref triggerEffect, ref problems)
        || ReadDecrementPowerEffect(value, powerName, ref triggerEffect, ref problems);

    /// <summary>
    /// ENV-02：`decrementPower(&lt;能力名&gt;)` → `decrement-self`，**只接受指向能力自己**
    ///（来源 `trigger(event=afterOwnerTurnStart, effect=decrementPower(PlatingPower))`）。
    /// 别的目标需要"改别人的层数"语义，本批次记为未支持。
    /// </summary>
    private static bool ReadDecrementPowerEffect(DslValueNode value, string powerName, ref string triggerEffect,
        ref List<string> problems)
    {
        if (value is not DslCallNode call || call.Name != "decrementPower")
        {
            return false;
        }

        var target = call.Arguments.Count == 1 ? NameOf(call.Arguments[0].Value) : string.Empty;
        if (!string.Equals(target, powerName, StringComparison.Ordinal))
        {
            problems.Add($"{powerName}.effect decrementPower 的目标不是自己（{target}）：只支持减自己");
            return true;
        }

        triggerEffect = AppendEffect(triggerEffect, PowerTriggerEffect.DecrementSelf);
        return true;
    }

    /// <summary>把一条效果接到已有的 `trigger-effect` 后面（与 `sequence` 用同一个分隔符）。</summary>
    private static string AppendEffect(string existing, string effect) =>
        existing.Length == 0 ? effect : $"{existing}{PowerTriggerEffect.SequenceSeparator}{effect}";

    /// <summary>
    /// ENV-02：`on = [trigger(event=…, effect=…), …]`（多触发点声明形状，例如 PlatingPower）。
    ///
    /// **本批次只接受两个触发点**，因为运行时正好有两个对应槽位：
    /// <list type="bullet">
    /// <item>`afterOwnerTurnStart` → `trigger`（事件触发；效果链走既有的效果读取器）；</item>
    /// <item>`afterOwnerTurnEnd` → `owner-turn-end`：grantPower(&lt;能力&gt;, sourceStacks)、按层数格挡或 unpowered 自伤。</item>
    /// </list>
    /// 别的触发点、别的组合一律**记为未支持并点名**（不静默忽略，也不假装支持多触发点的通用形状）。
    /// 已知范围限制：`afterOwnerTurnStart` 在运行时只对**玩家持有**的能力展开（既有站点如此），
    /// 因此敌人持有的同类能力尚未覆盖。
    /// </summary>
    private static void ReadOn(DslValueNode value, string powerName, ref string trigger, ref string triggerEffect,
        ref string ownerTurnEnd, ref List<string> problems)
    {
        if (value is not DslListNode list)
        {
            problems.Add($"{powerName}.on 未支持（只支持 trigger(...) 列表）");
            return;
        }

        foreach (var item in list.Items)
        {
            if (item is not DslCallNode { Name: "trigger" } call)
            {
                problems.Add($"{powerName}.on 的成员不是 trigger(...)：{Describe(item)}");
                continue;
            }

            var eventName = string.Empty;
            DslValueNode? effectNode = null;
            foreach (var argument in call.Arguments)
            {
                if (argument.Name == "event")
                {
                    eventName = NameOf(argument.Value);
                }
                else if (argument.Name == "effect")
                {
                    effectNode = argument.Value;
                }
            }

            switch (eventName)
            {
                case "afterOwnerTurnStart":
                    trigger = "after_player_turn_start";
                    if (effectNode is not null
                        && !ReadSingleEffect(effectNode, powerName, ref triggerEffect, ref problems))
                    {
                        problems.Add($"{powerName}.on afterOwnerTurnStart 的 effect 未支持：{Describe(effectNode)}");
                    }

                    break;
                case "afterOwnerTurnEnd":
                    // Own-side end: grant by current stacks without consuming or skipping the source power.
                    if (effectNode is not null
                        && (ReadGrantPowerEffect(effectNode, powerName, ref ownerTurnEnd, ref problems)
                            || ReadTurnEndBlockEffect(effectNode, powerName, ref ownerTurnEnd, ref problems)
                            || ReadTurnEndDamageEffect(effectNode, powerName, ref ownerTurnEnd, ref problems)))
                    {
                        break;
                    }

                    problems.Add($"{powerName}.on afterOwnerTurnEnd 的 effect 未支持（只支持 "
                        + "grantPower(<能力>, sourceStacks)、gainBlock(n, perPowerStack=自己) 或 scaledAttack(...properties=[unpowered])）："
                        + $"{Describe(effectNode)}");
                    break;
                default:
                    problems.Add($"{powerName}.on 的触发点 '{eventName}' 未支持"
                        + "（本批次只接受 afterOwnerTurnStart / afterOwnerTurnEnd）");
                    break;
            }
        }
    }

    /// <summary>回合末的 `gainBlock(n, perPowerStack=自己)` → `owner-turn-end = gain-block-per-stack:n`。</summary>
    private static bool ReadTurnEndBlockEffect(DslValueNode value, string powerName, ref string ownerTurnEnd,
        ref List<string> problems)
    {
        if (value is not DslCallNode call || call.Name != "gainBlock")
        {
            return false;
        }

        var amount = 0;
        var perStackSelf = false;
        foreach (var argument in call.Arguments)
        {
            if (argument.Name is null && argument.Value is DslNumberNode number
                && int.TryParse(number.Literal, out var parsed))
            {
                amount = parsed;
            }
            else if (argument.Name == "perPowerStack" && NameOf(argument.Value) == powerName)
            {
                perStackSelf = true;
            }
        }

        if (!perStackSelf)
        {
            problems.Add($"{powerName}.on afterOwnerTurnEnd 的 gainBlock 未按自身层数（perPowerStack≠自己）");
            return true;
        }

        ownerTurnEnd = $"{PowerTurnEnd.GainBlockPerStackPrefix}{amount}";
        return true;
    }

    /// <summary>
    /// 回合末的**自伤**形态（来源 `ConstrictPower.AfterSideTurnEnd`：`participants.Contains(Owner)` 时
    /// 对拥有者造成 `Amount` 点 `ValueProp.Unpowered` 伤害）：
    /// <c>scaledAttack(base, perStack, count=targetPowerAmount(自己, target=self), target=self,
    /// properties=[unpowered])</c> ⇒ `owner-turn-end = damage-owner-unpowered:base:perStack`
    /// （伤害 = base + perStack × 自身层数，**不消化**出手/受击修正）。
    ///
    /// 只接受这一形态：别的 base/perStack/count/target/properties 组合一律记未支持并点名。
    /// </summary>
    private static bool ReadTurnEndDamageEffect(DslValueNode value, string powerName, ref string ownerTurnEnd,
        ref List<string> problems)
    {
        if (value is not DslCallNode { Name: "scaledAttack" } call)
        {
            return false;
        }

        int? baseAmount = null;
        int? perStack = null;
        var countSelf = false;
        var targetSelf = false;
        var unpowered = false;
        var positional = 0;
        foreach (var argument in call.Arguments)
        {
            if (argument.Name is null && argument.Value is DslNumberNode number
                && int.TryParse(number.Literal, out var parsed))
            {
                if (positional == 0)
                {
                    baseAmount = parsed;
                }
                else if (positional == 1)
                {
                    perStack = parsed;
                }

                positional++;
            }
            else if (argument.Name == "count" && argument.Value is DslCallNode { Name: "targetPowerAmount" } count)
            {
                countSelf = count.Arguments.Any(item => item.Name == "target" && NameOf(item.Value) == "self");
            }
            else if (argument.Name == "target" && NameOf(argument.Value) == "self")
            {
                targetSelf = true;
            }
            else if (argument.Name == "properties" && argument.Value is DslListNode properties)
            {
                unpowered = properties.Items.Any(item => NameOf(item) == "unpowered");
            }
        }

        if (baseAmount is null || perStack is null || !countSelf || !targetSelf || !unpowered)
        {
            problems.Add($"{powerName}.on afterOwnerTurnEnd 的自伤形态未支持（只支持 "
                + "scaledAttack(base, perStack, count=targetPowerAmount(自己, target=self), target=self, "
                + "properties=[unpowered])）");
            return true;
        }

        ownerTurnEnd = $"{PowerTurnEnd.DamageOwnerUnpoweredPrefix}{baseAmount}:{perStack}";
        return true;
    }

    /// <summary>
    /// ENV-02：`blockGainModifiers = [modifier(multiplier=M, require=[move], priorEntriesPerStack=N,
    /// playerOnly=true)]` → "本回合由**牌**获得格挡的前 `层数 × N` 次乘 M"（来源 `UnmovablePower.
    /// ModifyBlockMultiplicative`：只看同回合、同目标、`IsCardOrMonsterMove()` 且**不含本次出牌**的
    /// `BlockGainedEntry` 数量，`num >= Amount` 时为 1、否则为 2）。
    ///
    /// 只接受这一形态：多个 modifier、别的 require/exclude、`playerOnly != true` 都记为未支持
    /// （切片目前只在"卡牌驱动的获得格挡"这一处应用该修正，且只对玩家）。
    /// </summary>
    private static void ReadBlockGainModifier(DslValueNode value, string powerName, ref string multiplier,
        ref int priorEntries, ref List<string> problems)
    {
        if (value is not DslListNode list || list.Items.Count != 1 || list.Items[0] is not DslCallNode call
            || call.Name != "modifier")
        {
            problems.Add($"{powerName}.blockGainModifiers 未支持（只支持单个 modifier(...)）");
            return;
        }

        string? parsedMultiplier = null;
        var require = new List<string>();
        var exclude = new List<string>();
        var playerOnly = false;
        var prior = 0;
        var priorSeen = false;
        foreach (var argument in call.Arguments)
        {
            switch (argument.Name)
            {
                case "multiplier":
                    parsedMultiplier = NameOf(argument.Value);
                    break;
                case "require":
                    require.AddRange(NamesIn(argument.Value));
                    break;
                case "exclude":
                    exclude.AddRange(NamesIn(argument.Value));
                    break;
                case "priorEntriesPerStack":
                    prior = IntOf(argument.Value);
                    priorSeen = true;
                    break;
                case "playerOnly":
                    playerOnly = argument.Value is DslBooleanNode { Value: true };
                    break;
                default:
                    problems.Add($"{powerName}.blockGainModifiers 参数 '{argument.Name}' 未支持");
                    break;
            }
        }

        if (require.Count != 1 || require[0] != "move" || exclude.Count != 0 || !playerOnly)
        {
            problems.Add($"{powerName}.blockGainModifiers 的 require/exclude/playerOnly 未支持"
                + $"（require=[{string.Join(",", require)}] exclude=[{string.Join(",", exclude)}]"
                + $" playerOnly={playerOnly}）：只支持 require=[move] + playerOnly=true");
            return;
        }

        if (!priorSeen || prior <= 0)
        {
            problems.Add($"{powerName}.blockGainModifiers 的 priorEntriesPerStack 未支持（只支持正整数）");
            return;
        }

        if (parsedMultiplier is null || !decimal.TryParse(parsedMultiplier,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed <= 0m)
        {
            problems.Add($"{powerName}.blockGainModifiers 的 multiplier 不是正小数（{parsedMultiplier}）");
            return;
        }

        // 乘数原样保留（可以是 2、也可以是 0.75）：运行时用**定点**乘、只在最后截断一次，
        // 与源 `decimal` 的 `ModifyBlockMultiplicative` 一致（见 `CombatSlicePackage.BlockGainSteps`）。
        multiplier = parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        priorEntries = prior;
    }

    /// <summary>
    /// `blockGainMultiplier = M`（标量形态，来源 `FrailPower.ModifyBlockMultiplicative`）：
    /// 拥有者就是获得格挡者时、对 **powered 的牌或怪物行动格挡**乘 M，**没有次数上限**。
    /// 返回 true 并写回 `multiplier`（原样保留小数）与 `priorEntries = 0`（0 = 无上限）。
    /// </summary>
    private static bool ReadScalarBlockGainMultiplier(DslValueNode value, string powerName,
        ref string multiplier, ref int priorEntries, ref List<string> problems)
    {
        var text = value switch
        {
            DslNumberNode number => number.Literal,
            _ => string.Empty,
        };
        if (text.Length == 0 || !decimal.TryParse(text, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed <= 0m)
        {
            problems.Add($"{powerName}.blockGainMultiplier 不是正小数");
            return false;
        }

        multiplier = parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        priorEntries = 0;
        return true;
    }

    /// <summary>
    /// `incomingDamageModifiers = [modifier(multiplier=M, require=[move], exclude=[unpowered])]`
    /// → `multiply-incoming:M`。只接受这一形态：多个 modifier、别的 require/exclude 组合都记为未支持
    /// （运行时的 `multiply-incoming` 语义就是"受击方侧乘、只作用于 powered 攻击"）。
    /// </summary>
    private static void ReadIncomingModifier(DslValueNode value, string powerName, ref string damageModifier,
        ref string damageValue, ref string sourcePower, ref List<string> problems)
    {
        if (value is not DslListNode list || list.Items.Count != 1 || list.Items[0] is not DslCallNode call)
        {
            problems.Add($"{powerName}.incomingDamageModifiers 未支持（本批次只支持单个 modifier(...)）");
            return;
        }

        if (call.Name != "modifier")
        {
            problems.Add($"{powerName}.incomingDamageModifiers 的调用 '{call.Name}' 未支持（只支持 modifier）");
            return;
        }

        string? multiplier = null;
        var require = new List<string>();
        var exclude = new List<string>();
        foreach (var argument in call.Arguments)
        {
            switch (argument.Name)
            {
                case "multiplier":
                    multiplier = NameOf(argument.Value);
                    break;
                case "require":
                    require.AddRange(NamesIn(argument.Value));
                    break;
                case "exclude":
                    exclude.AddRange(NamesIn(argument.Value));
                    break;
                // ENV-02：`sourcePower=<能力名>` —— 只有当**攻击者**身上有这条能力时修正才生效
                //（来源 `ColossusPower.ModifyDamageMultiplicative` 的 `dealer.HasPower<VulnerablePower>()`）。
                case "sourcePower":
                    var name = NameOf(argument.Value);
                    if (!TryResolveId(name, out var resolved))
                    {
                        problems.Add($"{powerName}.incomingDamageModifiers 的 sourcePower '{name}' 无法链接到已声明能力");
                        break;
                    }

                    sourcePower = resolved;
                    break;
                default:
                    problems.Add($"{powerName}.incomingDamageModifiers 参数 '{argument.Name}' 未支持");
                    break;
            }
        }

        var requireOk = require.Count == 0 || (require.Count == 1 && require[0] == "move");
        var excludeOk = exclude.Count == 0 || (exclude.Count == 1 && exclude[0] == "unpowered");
        if (!requireOk || !excludeOk)
        {
            problems.Add($"{powerName}.incomingDamageModifiers 的 require/exclude 未支持"
                + $"（require=[{string.Join(",", require)}] exclude=[{string.Join(",", exclude)}]）");
            return;
        }

        if (multiplier is null || !decimal.TryParse(multiplier,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed <= 0m)
        {
            problems.Add($"{powerName}.incomingDamageModifiers 的 multiplier 不是正小数（{multiplier}）");
            return;
        }

        damageModifier = "multiply-incoming";
        damageValue = parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        // sourcePower 已在上面解析成限定 id 并写进 ref 参数（没写＝没有来源条件）。
    }

    private static int IntOf(DslValueNode value) =>
        value is DslNumberNode number && int.TryParse(number.Literal, out var parsed) ? parsed : 0;

    private static IEnumerable<string> NamesIn(DslValueNode value)
    {
        if (value is DslListNode list)
        {
            foreach (var item in list.Items)
            {
                yield return NameOf(item);
            }

            yield break;
        }

        yield return NameOf(value);
    }

    private static string Unsupported(string raw, ref List<string> problems)
    {
        problems.Add($"触发器 '{raw}' 未支持（本批次只有 afterCardExhausted/afterCardPlayed）");
        return string.Empty;
    }

    /// <summary>`effect = gainBlock(N, perPowerStack=自己)` → `gain-block-per-stack:N`。</summary>
    private static bool ReadGainBlockEffect(DslValueNode value, string powerName, ref string triggerEffect,
        ref List<string> problems)
    {
        if (value is not DslCallNode call || call.Name != "gainBlock")
        {
            return false;
        }

        var amount = 0;
        var perStackSelf = false;
        foreach (var argument in call.Arguments)
        {
            if (argument.Name is null && argument.Value is DslNumberNode number
                && int.TryParse(number.Literal, out var parsed))
            {
                amount = parsed;
            }
            else if (argument.Name == "perPowerStack" && NameOf(argument.Value) == powerName)
            {
                perStackSelf = true;
            }
        }

        if (!perStackSelf)
        {
            problems.Add($"effect gainBlock 的 perPowerStack 不是自己（{powerName}）：本批次只支持按自身层数");
            return true;
        }

        triggerEffect = $"gain-block-per-stack:{amount}";
        return true;
    }

    private static string Describe(DslValueNode value) => value switch
    {
        DslCallNode call => call.Name + "(" + string.Join(",", call.Arguments.Select(argument =>
            (argument.Name is null ? string.Empty : argument.Name + "=") + Describe(argument.Value))) + ")",
        DslNumberNode number => number.Literal,
        DslNameNode name => name.Name,
        DslBooleanNode boolean => boolean.Value ? "true" : "false",
        DslStringNode text => text.Value,
        DslListNode list => "[" + string.Join(",", list.Items.Select(Describe)) + "]",
        DslBlockNode block => "{" + string.Join(",", block.Members.OfType<DslPropertyNode>()
            .Select(member => member.Name + "=" + Describe(member.Value))) + "}",
        _ => "?",
    };

    private static string NameOf(DslValueNode value) => value switch
    {
        DslNameNode name => name.Name,
        DslNumberNode number => number.Literal,
        DslBooleanNode boolean => boolean.Value ? "true" : "false",
        DslStringNode text => text.Value,
        _ => "?",
    };

    private static string Capitalize(string raw) =>
        raw.Length == 0 ? raw : char.ToUpperInvariant(raw[0]) + raw[1..];

    private static AuthorPower Lower(Declaration declaration)
    {
        // `sourcePower` 由 ReadIncomingModifier 解析成 id；`KnownIds` 给的是限定 id，kebab 回退是不限定的，
        // 这里按能力自己的包前缀补齐（与 `Id` 同一套规则，不另立规则）。
        var colon = declaration.Id.IndexOf(':');
        var prefix = colon > 0 ? declaration.Id[..colon] : string.Empty;
        var sourcePower = declaration.DamageModifierSourcePower.Length == 0
            || declaration.DamageModifierSourcePower.Contains(':')
            || prefix.Length == 0
                ? declaration.DamageModifierSourcePower
                : $"{prefix}:{declaration.DamageModifierSourcePower}";

        return new AuthorPower(
            Id: declaration.Id,
            Name: declaration.Name,
            Role: declaration.Role.Length == 0 ? declaration.Name : declaration.Role,
            Effect: declaration.Effect,
            OwnerTurnEnd: declaration.OwnerTurnEnd,
            DamageModifier: declaration.DamageModifier,
            DamageModifierValue: declaration.DamageModifierValue,
            DamageModifierSourcePower: sourcePower,
            MaxEnergyPerStack: declaration.MaxEnergyPerStack,
            VulnerableMultiplierPercentPerStack: declaration.VulnerableMultiplierPercentPerStack,
            TemporaryStrengthPerStack: declaration.TemporaryStrengthPerStack,
            DamageCap: string.Empty,
            Trigger: declaration.Trigger,
            TurnEndSide: declaration.TurnEndSide,
            WhenCardType: declaration.WhenCardType,
            TriggerEffect: declaration.TriggerEffect,
            PreventEnergyGain: declaration.PreventEnergyGain,
            PreventsBlockClear: declaration.PreventsBlockClear,
            CardRuleType: declaration.CardRuleType,
            CardRuleCost: declaration.CardRuleCost,
            CardRuleResultPile: declaration.CardRuleResultPile,
            CardRuleConsumeOnPlay: declaration.CardRuleConsumeOnPlay,
            BlockGainMultiplier: declaration.BlockGainMultiplier,
            BlockGainPriorEntriesPerStack: declaration.BlockGainPriorEntriesPerStack,
            BlockGainAdditivePerStack: declaration.BlockGainAdditivePerStack,
            SourcePath: declaration.Source,
            SourceLine: declaration.Line)
            { Rules = declaration.Rules, NegativeStacksAreInfinite = declaration.NegativeStacksAreInfinite };
    }
}
