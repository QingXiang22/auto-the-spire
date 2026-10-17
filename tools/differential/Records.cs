using System.Text.Json;
using System.Text.Json.Serialization;

namespace Differential;

/// <summary>
/// 一条**规范化事实**：路径 → 值。两个后端的对象图完全不同，所以比较的不是各自的完整状态，
/// 而是双方用**同一套路径**表达的、有规则含义的可追踪事实（ENV §6.2）。
/// 路径前缀决定差异种类：<c>action.</c> / <c>decision.</c> / <c>pile.</c> / <c>rng.</c> / 其它视为字段。
/// </summary>
internal sealed record FactSnapshot(string Path, string Value);

/// <summary>一条字段读数（仅用于 base 侧完整内部诊断，不参与跨后端比较）。</summary>
internal sealed record FieldSnapshot(string Field, string Value);

/// <summary>语义槽位的实例状态（仅用于 base 侧完整内部诊断）。</summary>
internal sealed record InstanceSnapshot(string Slot, string Type, string Definition, IReadOnlyList<FieldSnapshot> Fields);

/// <summary>有序牌区成员（仅用于 base 侧完整内部诊断）。</summary>
internal sealed record CollectionSnapshot(string Collection, IReadOnlyList<string> Members);

/// <summary>一条随机流的调用计数与状态指纹（观察不消费随机数，所以这里只读）。</summary>
internal sealed record StreamSnapshot(string Stream, long Draws, string Fingerprint);

/// <summary>
/// 一条参考侧随机流在某个边界的**实际读数**（不是累计脚本末态）：原始诊断流名、稳定槽位、
/// 该流自己的 <c>Rng.Counter</c>（native-counter-increments 单位）与按两后端同一公式算出的状态指纹。
///
/// <para>
/// <see cref="Slot"/> 是观察到的对象身份槽位（具名流为 <c>named:&lt;raw&gt;</c>，动态/临时 RNG 为
/// <c>observed:&lt;序号&gt;</c>），<see cref="Stream"/> 是**原始流名**：具名流由 <c>RngSourceCompat</c>
/// 的显式映射投影成 base 引擎流名，动态/临时 RNG 保留自己的原始诊断名。两者都必须保留——
/// 只留投影名会把不同对象合并成一条流，只留原始名又无法与 base 对齐。
/// </para>
/// <para>
/// <see cref="Comparable"/> 只有在"这条流已被显式映射到 base 引擎流、且两侧计数单位可比
/// （同一生成器自播种以来的调用数）"时才为 true；动态/临时 RNG 与未映射流一律 false，
/// 它们仍是证据但不参与按生成器次数的比较。<see cref="Status"/> 标出这条读数是哪个相位的
/// （<c>initial</c> / <c>restore-before</c> / <c>restore-after</c> / <c>boundary[n]</c>）。
/// </para>
/// </summary>
internal sealed record ReferenceStreamSnapshot(
    string Slot,
    string Stream,
    string Origin,
    string Status,
    long Counter,
    string Fingerprint,
    bool Comparable);

/// <summary>
/// 参考侧随机流的**逐边界**证据：每条稳定边界上每条被触及的流的真实 Counter 与状态指纹，
/// 外加建局产生（初始化）与受控注入后 <c>RngSourceCompat.RestoreStream</c> 前后的独立记录。
///
/// <para>
/// 初始化与受控恢复不是"边界"，不能拿它们的末态去填所有边界：这里分列保存。
/// <c>Counter</c> 与 <c>Fingerprint</c> 都只从实际 Rng 状态读，缺失就不写（不补 0）。
/// </para>
/// </summary>
internal sealed record ReferenceStreamEvidence(
    string Unit,
    string Observation,
    IReadOnlyList<ReferenceStreamSnapshot> Initialization,
    IReadOnlyList<ReferenceStreamSnapshot> RestoreBefore,
    IReadOnlyList<ReferenceStreamSnapshot> RestoreAfter,
    IReadOnlyList<ReferenceStreamBoundary> Boundaries);

/// <summary>一条稳定边界上的参考侧随机流读数。</summary>
internal sealed record ReferenceStreamBoundary(
    int Index,
    string Kind,
    IReadOnlyList<ReferenceStreamSnapshot> Streams);


/// <summary>一个当前合法动作：动作 id + 候选实例（按语义槽位） + 可绑定的参数组合数。</summary>
internal sealed record ActionOption(string Action, IReadOnlyList<string> Candidates, int Bindings);

internal sealed record NormalSemanticAction(string Action, string? Token,
    IReadOnlyList<ActionOption> BeforeActions, IReadOnlyList<FactSnapshot> BeforeVisible,
    IReadOnlyList<DecisionSnapshot> Decisions);

/// <summary>
/// 一个**待回答的逻辑决策点**：候选（按语义槽位）、最少/最多可选数量、以及最终选择。
/// <paramref name="Kind"/> 与 <paramref name="Prompt"/> 是各后端自己的表述，只写进记录供人读，**不参与比较**
/// （两个后端的提示文本与决策枚举本来就不是一回事；ENV §6.2 允许"单独规范化核对"）。
/// </summary>
internal sealed record DecisionSnapshot(
    string Kind,
    string Prompt,
    IReadOnlyList<string> Candidates,
    long Minimum,
    long Maximum,
    string? Chosen);

/// <summary>
/// base 侧的**完整内部诊断**：只在工具里使用，含完整牌区顺序、实例字段、状态哈希。
/// 它**不参与跨后端比较**（对象图不同），保留它是为了定位首差时能看清自己这一侧到底发生了什么。
/// </summary>
internal sealed record DiagnosticSnapshot(
    long StateVersion,
    int InstanceCount,
    string StateHash,
    IReadOnlyList<CollectionSnapshot> Collections,
    IReadOnlyList<InstanceSnapshot> Instances);

/// <summary>一个共同逻辑边界上的两侧记录。</summary>
internal sealed record BoundaryRecord(
    int Index,
    string Kind,
    string Label,
    IReadOnlyList<ActionOption> Actions,
    IReadOnlyList<DecisionSnapshot> Decisions,
    IReadOnlyList<FactSnapshot> Facts,
    IReadOnlyList<StreamSnapshot> Streams,
    DiagnosticSnapshot? Diagnostic);

/// <summary>
/// 工厂接线检查的实测结果（ENV-T04：正式构建/会话必须由唯一工厂产出）。
/// 它是**工具/生产集成**证据，不是产品内容覆盖。
/// <para>
/// `FactoryDidNotStart` 覆盖契约里"工厂不代启动"这一半：工厂返回的会话必须零随机消费、零玩家实例。
/// "只 Start 一次"是调用方的义务，工具按构造只调用一次（<see cref="StartOnceSucceeded"/>），
/// 不假装引擎会拒绝第二次 Start。
/// </para>
/// </summary>
internal sealed record FactoryWiringSnapshot(
    string Scope,
    string Character,
    int Ascension,
    string SourceBaseline,
    string BuildId,
    bool UnstartedSessionHasNoDraws,
    bool UnstartedSessionHasNoPlayerInstance,
    string RandomAlgorithmId,
    string RandomDrawProfileId,
    bool ComputeBackendInstalled,
    bool StartOnceSucceeded,
    string SessionStatusAfterStart,
    IReadOnlyList<string> Rejections);

/// <summary>参考身份里的一条流：原版流类型名 → 引擎流名 → **源版本派生的**种子。</summary>
internal sealed record ReferenceStreamSeed(string SourceType, string EngineStream, ulong Seed);

/// <summary>参考身份里的一份实际源文件摘要（不拿 HEAD 代替）。</summary>
internal sealed record ReferenceFileDigest(string Path, string Sha256);

/// <summary>
/// **参考身份**（ENV 约束 §10）：只有参考侧在启用源版本兼容 Profile 时才有值。
/// 记录启用方式/范围、原始字符串 seed、有效派生、各流映射、Profile 代码摘要、参考 HEAD 与**实际源文件摘要**。
/// 它不进 base 记录，也不参与跨后端比较——它是"两侧到底比的是什么版本"的证据。
/// </summary>
internal sealed record ReferenceIdentity(
    string Adapter,
    string ProfileId,
    string Enablement,
    string Scope,
    string StringSeed,
    ulong SourceRunSeed,
    IReadOnlyList<ReferenceStreamSeed> Streams,
    IReadOnlyList<string> Replaces,
    IReadOnlyList<string> NotReplaced,
    string? ProfileCodeSha256,
    string? ReferenceHead,
    string? ReferenceHeadSource,
    IReadOnlyList<ReferenceFileDigest> SourceFiles);

/// <summary>
/// 一个 case 在一个后端上的完整记录。
/// <para>
/// <c>RngProfile</c> 是**声明**；实际是否"没有消费新随机数"由 <c>rng_consumed_by_script</c> 这一实测事实给出，
/// 判定用实测值而不是声明值（声明与实测不符时以实测为准）。
/// </para>
/// </summary>
internal sealed record CaseRecord(
    string CaseId,
    string Backend,
    string BackendVersion,
    string RngProfile,
    string Status,
    string? Detail,
    bool RngConsumedByScript,
    IReadOnlyList<string> Notes,
    IReadOnlyList<BoundaryRecord> Boundaries)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public EngineTraceSnapshot? EngineTrace { get; init; }
    public InitialStateProof? InitialState { get; init; }
    public RandomMeasurement? ScriptRandom { get; init; }
    public long? InitializationRandomCount { get; init; }

    /// <summary>base 侧会话来源（<see cref="BaseSources"/>）；参考侧为空。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BaseSource { get; init; }

    /// <summary>声明的终局结果 id（会话到达 <c>Terminal</c> 时非空）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Terminal { get; init; }

    /// <summary>脚本结束时的会话状态（<c>Running</c> / <c>AwaitingDecision</c> / <c>Terminal</c> / <c>Faulted</c>）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionStatus { get; init; }

    /// <summary>只读规则/构建身份（不含每局 seed）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BuildId { get; init; }

    /// <summary>会话身份（含 seed 指纹）：只作证据，不作公开 ruleset_id。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionIdentity { get; init; }

    /// <summary>唯一工厂接线检查的实测结果（只在 wiring case 上有值）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FactoryWiringSnapshot? FactoryWiring { get; init; }

    /// <summary>参考身份（只在启用源版本兼容 Profile 的参考记录上有值）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReferenceIdentity? ReferenceIdentity { get; init; }

    /// <summary>参考侧逐边界随机流证据（只在参考后端产出的记录上有值；base 侧为 null）。</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReferenceStreamEvidence? ReferenceStreams { get; init; }
    public IReadOnlyList<NormalSemanticAction>? SemanticActions { get; init; }
}

internal sealed record EngineTraceEntry(long Sequence, string Kind, string? Subject, string? Detail,
    string? Source, long StateVersion, string Status, long Instructions);
internal sealed record EngineTraceSnapshot(IReadOnlyList<EngineTraceEntry> Records,
    long DroppedRecords, EngineTraceEntry? FirstFault);

internal static class Records
{
    /// <summary>
    /// 动态/临时 RNG 的槽位前缀。参考侧把运行期构造的 RNG（地图流、遭遇流、怪物 AI 等未映射到
    /// base 引擎流的对象）按原始诊断名 + <c>observed:N</c> 槽位保留；它们**没有**稳定的跨后端键，
    /// 因此比较器与维度结算都按这个前缀把它们与具名流分开——既不合并槽位，也不拿它们当可比流。
    /// </summary>
    public const string ObservedStreamPrefix = "observed:";

    /// <summary>这条流读数是否是两侧可比单位的**具名流**（动态/临时 RNG 一律 false）。</summary>
    public static bool IsNamedStream(StreamSnapshot stream) =>
        !stream.Stream.StartsWith(ObservedStreamPrefix, StringComparison.Ordinal);

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string ToJson(CaseRecord record) => JsonSerializer.Serialize(record, Json);

    public static CaseRecord FromJson(string json) =>
        JsonSerializer.Deserialize<CaseRecord>(json, Json)
        ?? throw new InvalidOperationException("记录为空");

    /// <summary>事实表：路径重复即编程错误（比较会因为"谁最后写"而不确定）。</summary>
    public static IReadOnlyList<FactSnapshot> Facts(IEnumerable<FactSnapshot> facts)
    {
        var list = facts.OrderBy(fact => fact.Path, StringComparer.Ordinal).ToList();
        for (var index = 1; index < list.Count; index++)
        {
            if (list[index].Path == list[index - 1].Path)
            {
                throw new InvalidOperationException($"事实路径重复：{list[index].Path}");
            }
        }

        return list;
    }
}
