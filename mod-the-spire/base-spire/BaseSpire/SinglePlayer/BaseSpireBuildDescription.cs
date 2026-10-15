using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.SinglePlayer;

/// <summary>
/// **只读的规则与构建描述**：一个正式构建"到底是什么"，供公开目录/Host/对拍工具与证据共用。
///
/// 边界（ENV/BASE-01）：这里**不含**每局 seed，也不含会随会话变化的状态；同一配置下它必须逐字节相同。
/// `RulesetId` 是**静态规则身份**（来自 Engine `RuleSet.Identity` 的摘要，含内容/链接/编译/数值/随机
/// Profile），不是 `Session.Identity`（后者含 seed 指纹，不能当作公开 ruleset_id）。
/// </summary>
public sealed record BaseSpireBuildDescription(
    string PackageId,
    string PackageVersion,
    string ScopeId,
    string SourceBaselineId,
    string CharacterId,
    string CharacterName,
    int Ascension,
    string UnlockPolicy,
    string Mode,
    string RandomAlgorithmId,
    string RandomDrawProfileId,
    /// <summary>公开的静态规则身份（构建产物 + 内容的稳定摘要）。</summary>
    string RulesetId,
    int CardCount,
    int PowerCount,
    int EnemyCount,
    int DeckSize,
    int StartingHp,
    int StartingGold,
    IReadOnlyList<string> StartingRelics,
    int WeakEncounterCount,
    int NormalEncounterCount,
    /// <summary>当前范围覆盖的战斗房间数（= 遭遇池长度）。</summary>
    int RoomCount,
    IReadOnlyList<string> SupportedEncounters,
    IReadOnlyList<string> StreamNames,
    IReadOnlyList<(string Item, string Reason)> ExcludedEncounters,
    IReadOnlyList<(string Item, string Reason)> UnsupportedContent,
    IReadOnlyList<string> Limitations,
    string EncountersDigest,
    string ActsDigest,
    /// <summary>R3：实际加载内容的聚合摘要（全部 `.spire` + 两份 JSON，按资源名稳定排序）。</summary>
    string ContentDigest,
    /// <summary>R3：实际加载的每个源资源的内容摘要（可逐条核对"声称的来源"）。</summary>
    IReadOnlyList<(string Resource, string Digest)> SourceDigests,
    /// <summary>R3：源基线的证据路径（file:line），来自注册表而不是临时拼的字符串。</summary>
    IReadOnlyList<string> SourceEvidencePaths,
    /// <summary>R3：随机**布局**身份——流名映射 + Profile/算法 id 的摘要（同 seed 行为不同的布局会换它）。</summary>
    string StreamLayoutDigest,
    /// <summary>
    /// ENV-T03：**公开语义元数据**的摘要（内容目录 / 集合用途 / 已知位置角色 / 终局解释）。
    /// 见 <see cref="BasePublicSemantics"/>；它不含每局 seed，也不含可变状态。
    /// </summary>
    string SemanticsDigest,
    /// <summary>
    /// 公开构建身份：把**所有会改变行为**的配置维度合到一个 id（范围 / 源基线 / 规则集 / 随机算法与映射 /
    /// 流布局 / 内容摘要 / 公开语义元数据）。同一 id 不得对应行为不同的运行配置。
    /// </summary>
    string PublicBuildId)
{
    /// <summary>人读摘要（Host 的 stderr 与交接证据直接用这一段）。</summary>
    public string Summary =>
        $"{ScopeId} build={PublicBuildId} ruleset={RulesetId} source={SourceBaselineId} "
        + $"content={ContentDigest} semantics={SemanticsDigest} cards={CardCount} powers={PowerCount} "
        + $"enemies={EnemyCount} deck={DeckSize} rooms={RoomCount} encounters={SupportedEncounters.Count} "
        + $"unsupported={UnsupportedContent.Count} excluded={ExcludedEncounters.Count}";
}

/// <summary>
/// 工厂产出的**已配置、尚未 Start** 的独立会话句柄。
///
/// "尚未 Start"是契约的一部分：随机流已经按原版布局种好，但**一个随机数都还没消费**、
/// 也没有任何开局程序执行过。调用方（Host / 对拍 BaseBackend）在准备好接收首次决策时**调用一次**
/// `Session.Start()`；工厂不会替它启动，控制入口也不会启动第二次。
/// </summary>
public sealed record BaseSpireRunSession(
    RunConfiguration Configuration,
    BaseSpireBuildDescription Build,
    /// <summary>请求方给出的原始 seed（未经标准化，留证据）。</summary>
    string RequestedSeed,
    /// <summary>实际接受的 seed（按源规则标准化；哈希用的是它）。</summary>
    string AcceptedSeed,
    /// <summary>本局的数值 run seed（`uint(hash32(acceptedSeed))`）。</summary>
    uint RunSeed,
    Session Session,
    ParticipantId Participant,
    string ParticipantRole,
    string ProjectionId,
    /// <summary>实际种下的 (原版流类型, 引擎流名, 种子)：构建描述/工具/证据共用同一张表。</summary>
    IReadOnlyList<(string Type, string Stream, uint Seed)> SeededStreams)
{
    /// <summary>
    /// **遭遇派生流**（类型标为 `Encounter`）：每个带编队的遭遇在每个楼层一条，种子按源
    /// `(uint)(runSeed + TotalFloor + hash(entry))` 显式种好。与 <see cref="SeededStreams"/>（原版 12+3 条流）
    /// 分开登记：前者是原版**流布局**，后者是"遭遇自己的 Rng"（`EncounterModel.cs:266-270`）。
    /// </summary>
    public IReadOnlyList<(string Type, string Stream, uint Seed)> EncounterStreams { get; init; } = [];

    /// <summary>本局的**会话身份**（含 seed 指纹）：属证据，不作公开静态 ruleset_id。</summary>
    public string SessionIdentity => Session.Identity;

    /// <summary>启动一次。重复调用会让开局程序执行第二次——调用方必须保证只调用一次。</summary>
    public void Start() => Session.Start();
}
