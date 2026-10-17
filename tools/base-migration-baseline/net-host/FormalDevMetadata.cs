using BaseSpire.SinglePlayer;
using NetTheSpire.Projection;

namespace NetTheSpire.Host;

/// <summary>
/// **唯一的 base → net 元数据转换点**（ENV-T03 / design.md §7.2）。
///
/// 背景：内核 <c>RuleSet</c> 的通用声明能给出实体/字段/枚举/引用/动作/参数，但**四类游戏含义**
/// 推不出来——内容 ID 目录、集合用途与位置/归属、已知位置角色、终局胜负解释。这四类由规则提供方
/// （base 的正式工厂）以**不可变**形式给出（无每局 seed、无可变状态、同 config 稳定、外部不可篡改），
/// 由 Host 转成 net 的 <see cref="PublicBuildMetadata"/> 交给 <see cref="DerivedPublicProfile.Build"/>。
///
/// 边界（不得越界）：
/// <list type="bullet">
/// <item>这里**只做转换**：不复制逐项字段/Action 清单（那些仍由引擎声明与所选投影派生），
///       不猜名字用途、不替 base 决定某个集合该不该发布；</item>
/// <item>未知名字/缺条目由 <see cref="DerivedPublicProfile.Build"/> 逐条对照真实声明**显式失败**，
///       不在这里兜底；</item>
/// <item>base 不得引用 net：转换方向只能是 base → net，且只发生在这一个文件里；</item>
/// <item>base 侧出现 net 没有对应表达的用途时**显式失败**（不静默降级成"不发布"）。</item>
/// </list>
///
/// 静态构建身份用工厂的 <c>PublicBuildId</c>：它已经把范围/源基线/规则集/内容/随机布局/**语义元数据摘要**
/// 折在一起，且**不含每局 seed**（每局 seed 只进会话句柄与证据）。
/// </summary>
internal static class FormalDevMetadata
{
    /// <summary>取工厂随构建给出的不可变公开语义元数据，并转换成 net 的公开元数据形状。</summary>
    public static PublicBuildMetadata FromBase(BaseSpireRuntimeFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var semantics = factory.PublicSemantics;

        var content = new Dictionary<string, PublicContentRule>(StringComparer.Ordinal);
        foreach (var (typeId, catalog) in semantics.Content)
        {
            content[typeId] = new PublicContentRule(catalog.FieldName, [.. catalog.ContentIds]);
        }

        var collections = new Dictionary<string, PublicCollectionRule>(StringComparer.Ordinal);
        foreach (var (collectionId, declared) in semantics.Collections)
        {
            collections[collectionId] = declared.Use switch
            {
                BasePublicCollectionUse.Pile => PublicCollectionRule.Pile(
                    declared.PileKindEngineTypeId ?? throw new InvalidOperationException(
                        $"base 集合 '{collectionId}' 声明为 pile 但没有牌堆种类"),
                    ToNet(declared.Position)),
                BasePublicCollectionUse.Group => PublicCollectionRule.Group(
                    declared.OwnerKindEngineTypeId ?? throw new InvalidOperationException(
                        $"base 集合 '{collectionId}' 声明为 group 但没有 owner 种类")),
                // sequence 与 group 同一形状：owner 是**公开种类**，运行时按它在快照里唯一解析 owner_ref
                // （不写死每局句柄，也不偷偷退回"不发布"）。元素的公开顺序就是集合的真实顺序。
                BasePublicCollectionUse.Sequence => PublicCollectionRule.Sequence(
                    declared.OwnerKindEngineTypeId ?? throw new InvalidOperationException(
                        $"base 集合 '{collectionId}' 声明为 sequence 但没有 owner 种类")),
                BasePublicCollectionUse.NotPublished => PublicCollectionRule.NotPublished(
                    declared.SkipRuleId ?? throw new InvalidOperationException(
                        $"base 集合 '{collectionId}' 声明为不发布但没有规则号"),
                    declared.SkipReason ?? throw new InvalidOperationException(
                        $"base 集合 '{collectionId}' 声明为不发布但没有理由")),
                _ => throw new NotSupportedException(
                    $"base 集合用途 '{declared.Use}'（集合 {collectionId}）在本版公开协议里没有对应表达："
                    + "不静默降级成不发布"),
            };
        }

        var terminals = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (terminalId, outcome) in semantics.TerminalOutcomes)
        {
            terminals[terminalId] = outcome.WireName();
        }

        var knownPositions = new Dictionary<string, PublicKnownPositionRole>(StringComparer.Ordinal);
        foreach (var (factTypeId, role) in semantics.KnownPositions)
        {
            knownPositions[factTypeId] = new PublicKnownPositionRole(
                role.FactTypeId,
                role.CardFieldName,
                role.PileFieldName,
                role.PositionFieldName,
                new Dictionary<string, string>(role.PileCollectionByToken, StringComparer.Ordinal));
        }

        return PublicBuildMetadata.Create(
            factory.Describe().PublicBuildId,
            content,
            collections,
            terminals,
            knownPositions);
    }

    /// <summary>base 的位置语义（Scope/Origin/First/Max）→ net 的公开位置语义（Unit/Origin/Min/Max）。</summary>
    private static PublicPositionSchema? ToNet(BasePublicPositionSemantics? position) =>
        position is null
            ? null
            : new PublicPositionSchema(position.Scope, position.Origin, position.First, position.Max);
}
