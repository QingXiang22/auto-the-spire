using System.Text;
using System.Text.RegularExpressions;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// compute 资产的编译期句柄：资产符号 + 卡上的排序键字段 + 输入/输出字段。声明在
/// <c>Compilation/CardCompilation.Draw</c>（排序接缝），执行分派在纯计算后端。
/// </summary>
public sealed record SourceStableShuffleSeam(
    ComputeAssetSymbol Asset,
    FieldSymbol CardKey,
    FieldSymbol Keys,
    FieldSymbol Step,
    FieldSymbol Index);

/// <summary>
/// ENV-T06-source-stable-shuffle-20261012：源 <c>StableShuffle</c> 缺失的**预排序**（纯计算部分）。
///
/// 源（只读 <c>sts2/</c>）：<c>src/Core/Extensions/ListExtensions.cs:22-31</c>
/// <code>
/// List&lt;T&gt; list2 = list.ToList();
/// list2.Sort();                        // Comparer&lt;CardModel&gt;.Default → CardModel.CompareTo
/// for (int i = 0; i &lt; list.Count; i++) list[i] = list2[i];
/// return list.UnstableShuffle(rng);    // 降序 Fisher–Yates（base 侧由 ShuffleCollection 消费同一条流）
/// </code>
/// 排序键（<c>src/Core/Models/CardModel.cs:2242-2263</c> → <c>AbstractModel.CompareTo:87-97</c> →
/// <c>ModelId.CompareTo:42-50</c>）：先 <c>Category</c> 再 <c>Entry</c>（都是 <c>StringComparison.Ordinal</c>），
/// 最后 <c>CurrentUpgradeLevel</c>。卡牌的 <c>Category</c> 恒为 <c>CARD</c>
/// （<c>ModelDb.GetCategoryType</c> 对卡类归结到 <c>CardModel</c>，<c>SlugifyCategory</c> 去掉 <c>_MODEL</c> 后缀），
/// 所以**卡与卡之间只比 Entry**；<c>Entry</c> = <c>ModelDb.GetEntry</c> = <c>StringHelper.Slugify(类型名)</c>。
/// 本项目的内容 id 是源类名的 kebab 转写，但两者**不是**恒等互逆（连续大写结尾等内容 id 会丢边界），
/// 因此排序键由**声明名**算出并作为卡实例的隐藏元数据保存（见 <c>CardSchema.SourceOrderKey</c>）。
///
/// **为什么不能直接用 IR 的稳定 <c>Sort</c> 查询**：源 <c>List&lt;T&gt;.Sort()</c> 是内省排序
/// （&lt;16 张走插入排序，更大时分区后再插入排序），同键实例在 ≥16 张时**不保证**保持原相对次序。
/// 实测 4000 组重复实例输入里 3005 组与稳定排序结果不同（<c>.audit/dsh-source-stable-shuffle-20261012/probe-output.txt</c>）。
/// 本模块复刻的是**置换**：对下标数组用同一个比较器调用 <c>List&lt;int&gt;.Sort(Comparison&lt;int&gt;)</c>。
/// 内省排序只依赖两两比较的符号，同一 .NET 运行时下与源逐对同号，因此给出的元素置换逐位一致。
///
/// 纯计算：不接触 RNG / Session / State；输入是"按牌区当前顺序拼好的键快照"。
/// </summary>
public static class SourceStableShuffle
{
    /// <summary>计算资产名（进规则身份）。</summary>
    public const string AssetName = "SourceStableShuffleOrder";

    /// <summary>资产条目名（后端按它分派）。</summary>
    public const string Entry = "stable-shuffle-order";

    /// <summary>源定位（进规则身份）。</summary>
    public const string SourcePath = "sts2/src/Core/Extensions/ListExtensions.cs";

    /// <summary>源定位摘要（进规则身份；改排序语义必须换版本）。</summary>
    public const string SourceHash =
        "sts2:ListExtensions.StableShuffle=List<T>.Sort(Comparer<CardModel>.Default);"
        + "CardModel.CompareTo=AbstractModel.Id(ModelId.Category,ModelId.Entry,Ordinal)+CurrentUpgradeLevel;"
        + "StringHelper.Slugify@source-stable-shuffle-v1";

    /// <summary>单卡排序键（<c>ENTRY@LEVEL</c>）的最大长度。</summary>
    public const int MaxKeyLength = 64;

    /// <summary>键快照 blob 的最大长度（每卡一个键 + 分隔符）。</summary>
    public const int MaxBlobLength = 32768;

    /// <summary>键里"源 Entry"与"升级等级"的分隔符。Entry 只含 <c>[A-Z0-9_]</c>，不会撞上它。</summary>
    public const char LevelSeparator = '@';

    /// <summary>blob 里键与键之间的分隔符。</summary>
    public const string KeySeparator = ";";

    /// <summary>源比较中"升级等级"的两位取值（源是 int，本包卡形态只有普通/升级）。</summary>
    public const int NormalLevel = 0;

    /// <summary>升级形态的源等级（源 <c>CurrentUpgradeLevel</c> 的首级）。</summary>
    public const int UpgradedLevel = 1;

    // `StringHelper.Slugify` 用的三条正则（与 `VanillaRandomLayout` 的 CamelCase 同一条）。
    private static readonly Regex CamelCase = new(@"([A-Za-z0-9]|\G(?!^))([A-Z])", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex NonSlug = new(@"[^A-Z0-9_]", RegexOptions.Compiled);

    /// <summary>
    /// <c>StringHelper.Slugify</c>（<c>sts2/src/Core/Helpers/StringHelper.cs:23-28</c>）：
    /// CamelCase 拆分 → 大写并把空白折成 <c>_</c> → 去掉 <c>[^A-Z0-9_]</c>。这是源的 <c>ModelId.Entry</c>。
    /// </summary>
    public static string Slugify(string sourceName)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        var split = CamelCase.Replace(sourceName.Trim(), "$1_$2");
        return NonSlug.Replace(Whitespace.Replace(split.ToUpperInvariant(), "_"), string.Empty);
    }

    /// <summary>单卡排序键：源 <c>ModelId.Entry</c> + 源 <c>CurrentUpgradeLevel</c>。</summary>
    public static string Key(string sourceEntry, bool upgraded) =>
        sourceEntry + LevelSeparator + (upgraded ? '1' : '0');

    /// <summary>解析排序键。缺分隔符的内容 id 形态会被拒绝：调用方必须先过 <see cref="Slugify"/>。</summary>
    public static (string Entry, int Level) Parse(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var separator = key.LastIndexOf(LevelSeparator);
        if (separator < 0)
        {
            throw new ArgumentException($"'{key}' is not a source sort key (missing '{LevelSeparator}')", nameof(key));
        }

        return (key[..separator], int.Parse(key[(separator + 1)..], System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>源 <c>CardModel.CompareTo</c> 的符号：Entry 序数比较，再升级等级。</summary>
    public static int Compare((string Entry, int Level) left, (string Entry, int Level) right)
    {
        var byEntry = string.CompareOrdinal(left.Entry, right.Entry);
        return byEntry != 0 ? byEntry : left.Level.CompareTo(right.Level);
    }

    /// <summary>内容 id → 源 Entry 的**回退**口径（<c>SpireCardSource.ToId</c> 的逆向：短 id、<c>-</c>→<c>_</c>、大写）。</summary>
    public static string FallbackEntry(string contentId)
    {
        ArgumentNullException.ThrowIfNull(contentId);
        var separator = contentId.IndexOf(':');
        var name = separator < 0 ? contentId : contentId[(separator + 1)..];
        return name.Replace('-', '_').ToUpperInvariant();
    }

    /// <summary>把一组排序键拼成快照 blob（空集合 = 空串）。</summary>
    public static string Encode(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var builder = new StringBuilder();
        foreach (var key in keys)
        {
            builder.Append(key).Append(KeySeparator);
        }

        return builder.ToString();
    }

    /// <summary>解析快照 blob。</summary>
    public static IReadOnlyList<string> Decode(string blob)
    {
        ArgumentNullException.ThrowIfNull(blob);
        return blob.Length == 0
            ? []
            : [.. blob.Split(KeySeparator, StringSplitOptions.RemoveEmptyEntries)];
    }

    /// <summary>
    /// 源 <c>List&lt;T&gt;.Sort()</c> 给出的**目标顺序**：返回值是"结果第 k 位来自输入的第几个元素"。
    /// 排序对象是下标（比较结果与源逐对同号），因此同键实例的次序与源的内省排序一致。
    /// </summary>
    public static int[] Order(IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var parsed = new (string Entry, int Level)[keys.Count];
        for (var position = 0; position < keys.Count; position++)
        {
            parsed[position] = Parse(keys[position]);
        }

        var order = new List<int>(keys.Count);
        for (var position = 0; position < keys.Count; position++)
        {
            order.Add(position);
        }

        order.Sort((left, right) => Compare(parsed[left], parsed[right]));
        return [.. order];
    }

    /// <summary>
    /// IR 每一步要问的量：目标顺序第 <paramref name="step"/> 个元素**当前**在集合里的下标。
    ///
    /// IR 的不变量（按目标顺序逐个移到末尾）：集合 = [尚未放置的（保持原相对次序）] ++ [已放置的（目标顺序）]。
    /// 因此该下标就是"目标顺序第 step 个元素在未放置集合里的名次"。
    /// </summary>
    public static int NextIndex(string blob, int step)
    {
        var keys = Decode(blob);
        if (step < 0 || step >= keys.Count)
        {
            return -1;
        }

        var order = Order(keys);
        var wanted = order[step];
        var rank = 0;
        for (var position = step; position < order.Length; position++)
        {
            if (order[position] < wanted)
            {
                rank++;
            }
        }

        return rank;
    }

    /// <summary>
    /// 声明资产（排序接缝调用一次）。后端与加权分桶共用同一个 <c>BackendId</c>（引擎每会话只装一个后端），
    /// 由后端按 <c>asset.Entry</c> 分派。
    /// </summary>
    public static SourceStableShuffleSeam Declare(EnginePackageBuilder package, FieldSymbol cardKey)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(cardKey);
        var asset = package.ComputeAsset(AssetName);
        asset.BackendId = SourceWeightedPickBackend.BackendId;
        asset.SourcePath = SourcePath;
        asset.SourceHash = SourceHash;
        asset.Entry = Entry;
        return new SourceStableShuffleSeam(asset, cardKey,
            asset.Input("Keys", FieldTypeSchema.String(MaxBlobLength)),
            asset.Input("Step", FieldTypeSchema.Int(0, 9999)),
            asset.Output("Index", FieldTypeSchema.Int(0, 9999)));
    }

    /// <summary>后端分派：本资产的条目返回 true，并写出"当前下标"。</summary>
    public static bool TryExecute(string entry, IReadOnlyList<Value> inputs, Value[] outputs)
    {
        if (!string.Equals(entry, Entry, StringComparison.Ordinal))
        {
            return false;
        }

        if (inputs.Count < 2 || outputs.Length < 1)
        {
            throw Faults.Schema($"compute asset '{AssetName}' needs (Keys, Step) inputs and one output");
        }

        outputs[0] = Value.Of((long)NextIndex(inputs[0].AsString(), checked((int)inputs[1].AsInt())));
        return true;
    }
}
