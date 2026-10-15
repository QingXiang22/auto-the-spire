using System.Text.RegularExpressions;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// ENV-01：STS 的**字符串 seed → 随机流种子**布局，照抄只读的 <c>sts2/</c> 源码。
///
/// 分工：core 提供通用、可显式选择的随机算法/映射/流种子入口（<see cref="RandomService.SeedStream"/>），
/// "字符串 seed 怎么变成哪些流、每个流用什么种子"是 **STS 领域规则**，因此落在 base 这边。
///
/// 源位置：
/// <list type="bullet">
/// <item><c>sts2/src/Core/Runs/RunRngSet.cs:104-119</c>：`Seed = (uint)GetDeterministicHashCode(seed)`，
///       每个 `RunRngType` 用 `new Rng(Seed, SnakeCase(枚举名))`；</item>
/// <item><c>sts2/src/Core/Random/Rng.cs:52-55</c>：`Rng(seed, name)` = `seed + (uint)GetDeterministicHashCode(name)`；</item>
/// <item><c>sts2/src/Core/Entities/Players/Player.cs:324-328</c>：`PlayerRng = PlayerRngSet((uint)(hash(seed) + 槽位))`；</item>
/// <item><c>sts2/src/Core/Random/PlayerRngSet.cs:33-47</c>：玩家级流同上；</item>
/// <item><c>sts2/src/Core/Random/Rng.cs:47-50</c>：内容级 `(uint)(runSeed + 槽位 + hash(内容 entry)) + mixin`；</item>
/// <item><c>sts2/src/Core/Helpers/StringHelper.cs</c>：`GetDeterministicHashCode`（int 循环）与 `SnakeCase`。</item>
/// </list>
///
/// **不要照抄参考实现**：`reference/sts2-sim` 的 `RunRngSet` 已经把 run seed 换成了 ulong + XxHash64
/// （`Runs/RunRngSet.cs:36,80`），与这份冻结基线不是同一版本——这是登记在案的版本偏离，见工具 README。
/// </summary>
public static class VanillaRandomLayout
{
    /// <summary>与 <see cref="VanillaRandomLayout"/> 配套的随机 Profile（core 侧的一对常量）。</summary>
    public const string AlgorithmId = RandomService.VanillaAlgorithmId;

    public const string DrawProfileId = RandomService.VanillaDrawProfileId;

    /// <summary>Game-owned policy; pass as Session.randomProfile or RandomService.profile.</summary>
    public static IRandomProfile Profile { get; } = new VanillaRandomProfile();

    /// <summary>`Entities/Rngs/RunRngType.cs` 的成员顺序（`Enum.GetValues` 就是按这个顺序）。</summary>
    public static readonly IReadOnlyList<string> RunStreamTypes =
    [
        "UpFront", "Shuffle", "UnknownMapPoint", "CombatCardGeneration",
        "CombatPotionGeneration", "CombatCardSelection", "CombatEnergyCosts",
        "CombatTargets", "MonsterAi", "Niche", "CombatOrbs", "TreasureRoomRelics",
    ];

    /// <summary>`Entities/Rngs/PlayerRngType.cs` 的成员。</summary>
    public static readonly IReadOnlyList<string> PlayerStreamTypes = ["Rewards", "Shops", "Transformations"];

    // `StringHelper.SnakeCase` 用的就是这条正则（与 Slugify 共用）：`\G` 表示"紧接上一次匹配结束的位置"，
    // 连续大写会各自断开（ABC → A_B_C）。
    private static readonly Regex CamelCase = new(@"([A-Za-z0-9]|\G(?!^))([A-Z])", RegexOptions.Compiled);

    /// <summary>
    /// `StringHelper.GetDeterministicHashCode`（`StringHelper.cs:GetDeterministicHashCode`）。
    /// C# 的 `int` 在 unchecked 下按 32 位回绕；调用方最后把它转成 `uint`，所以返回值按 32 位无符号给出。
    /// </summary>
    public static uint DeterministicHashCode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        unchecked
        {
            var left = 352654597;
            var right = left;
            for (var index = 0; index < text.Length; index += 2)
            {
                left = ((left << 5) + left) ^ text[index];
                if (index == text.Length - 1)
                {
                    break;
                }

                right = ((right << 5) + right) ^ text[index + 1];
            }

            return (uint)(left + right * 1566083941);
        }
    }

    /// <summary>`StringHelper.SnakeCase`：CamelCase 拆分后转小写（流名字就是枚举名的 SnakeCase）。</summary>
    public static string SnakeCase(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return CamelCase.Replace(text.Trim(), "$1_$2").ToLowerInvariant();
    }

    // `StringHelper.SpecialCharRegex` = `[^A-Z0-9_]`（Slugify 的最后一步）。
    private static readonly Regex SpecialChar = new(@"[^A-Z0-9_]", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// `StringHelper.Slugify`（`sts2/src/Core/Helpers/StringHelper.cs:74-79`）：CamelCase 拆分 → 大写 →
    /// 空白转 `_` → 去掉 `[^A-Z0-9_]`。源的 `ModelDb.GetEntry(type)` 就是 `Slugify(type.Name)`，
    /// 因此事件/遭遇一类内容级流的种子哈希用的是**这个**形态（`SunkenStatue` → `SUNKEN_STATUE`），
    /// 与内容 id 的短横线形态不是同一条转换。
    /// </summary>
    public static string Slugify(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var camel = CamelCase.Replace(text.Trim(), "$1_$2");
        return SpecialChar.Replace(Whitespace.Replace(camel.ToUpperInvariant(), "_"), string.Empty);
    }

    /// <summary>
    /// `SeedHelper.CanonicalizeSeed`（`sts2/src/Core/Helpers/SeedHelper.cs:32-39`）：
    /// 大写、`O`→`0`、`I`→`1`、去首尾空白。**不加前缀**，也不做数值转换。
    ///
    /// 应用范围按来源：只有大厅建局与每日挑战在**哈希之前**做这步；`--seed` / bootstrap / 测试路径
    /// 直接拿原始字符串去哈希。本项目的公开入口（Host 的 `--game-seed`）属于"玩家输入的字符串 seed"，
    /// 因此按大厅路径标准化，并把**原始请求**与**实际接受**两个值都记进会话证据。
    /// </summary>
    public static string CanonicalizeSeed(string seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return seed.ToUpperInvariant().Replace('O', '0').Replace('I', '1').Trim();
    }

    /// <summary>
    /// 引擎侧的流名 ↔ 原版流类型的**显式映射**（参考侧用不同的诊断名时必须靠它对齐，不能靠猜）。
    ///
    /// 一个原版流对应**一个**引擎流（不能一个来源流派两个引擎流：那会让同一条流有两个计数器）。
    /// 因此"所有洗牌"都落在 `base-spire:combat-draw` 上——原版也只有 `Shuffle` 这一条流负责洗牌，
    /// 开局洗战斗牌堆与战斗中重洗共享同一条流的计数。
    /// </summary>
    public static string EngineStreamName(string runStreamType) => runStreamType switch
    {
        "Shuffle" => SliceManifest.DrawStream,
        "CombatTargets" => SliceManifest.RandomTargetStream,
        "CombatCardSelection" => SliceManifest.RandomCardStream,
        _ => $"base-spire:run:{SnakeCase(runStreamType)}",
    };

    /// <summary>玩家级流的引擎流名（`base-spire:player:&lt;snake&gt;`）。</summary>
    public static string EnginePlayerStreamName(string playerStreamType) =>
        $"base-spire:player:{SnakeCase(playerStreamType)}";

    /// <summary>
    /// 把这一局的**全部原版线流**按原版布局显式种进随机服务（必须在任何抽数之前调用）。
    /// 返回实际种下的 (原版流类型, 引擎流名, 种子) 列表，供构建描述与会话证据核对。
    /// </summary>
    public static IReadOnlyList<(string Type, string Stream, uint Seed)> SeedAllStreams(
        RandomService service, string canonicalSeed, int playerSlot = 0)
    {
        ArgumentNullException.ThrowIfNull(service);
        var seeded = new List<(string, string, uint)>();
        foreach (var (type, _, seed) in RunStreams(canonicalSeed))
        {
            var stream = EngineStreamName(type);
            service.SeedStream(stream, seed);
            seeded.Add((type, stream, seed));
        }

        foreach (var (type, _, seed) in PlayerStreams(canonicalSeed, playerSlot))
        {
            var stream = EnginePlayerStreamName(type);
            service.SeedStream(stream, seed);
            seeded.Add((type, stream, seed));
        }

        return seeded;
    }

    /// <summary>`RunRngSet.cs:107`：字符串 seed → 这一局的 uint seed。</summary>
    public static uint RunSeed(string stringSeed) => DeterministicHashCode(stringSeed);

    /// <summary>`Rng.cs:52-55`：`new Rng(setSeed, name)` 的等价形式（uint 加法回绕）。</summary>
    public static uint StreamSeed(uint setSeed, string streamType)
    {
        ArgumentNullException.ThrowIfNull(streamType);
        return unchecked(setSeed + DeterministicHashCode(SnakeCase(streamType)));
    }

    /// <summary>`Player.cs:326`：玩家级 seed = 字符串 seed 的哈希 + 玩家槽位。</summary>
    public static uint PlayerSeed(string stringSeed, int slotIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);
        return unchecked(DeterministicHashCode(stringSeed) + (uint)slotIndex);
    }

    /// <summary>`Rng.cs:47-50`：内容级种子 = run seed + 槽位 + 内容 entry 的哈希（+ 可选 mixin）。</summary>
    public static uint ContentSeed(uint runSeed, int slotIndex, string contentEntry, uint mixin = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);
        ArgumentNullException.ThrowIfNull(contentEntry);
        return unchecked(runSeed + (uint)slotIndex + DeterministicHashCode(contentEntry) + mixin);
    }

    /// <summary>
    /// 内容 id → 源的 **ModelId.Entry**（`ModelDb.GetEntry` = `StringHelper.Slugify(type.Name)` ⇒ 大写下划线）。
    /// 本项目的遭遇 id 由源类名转写而来（`base-spire:slimes-weak` ↔ `SlimesWeak` ⇒ `SLIMES_WEAK`），
    /// 因此这里按同一条规则还原；它进遭遇派生种子的哈希，必须与源一致。
    /// </summary>
    public static string SourceEntry(string contentId)
    {
        ArgumentNullException.ThrowIfNull(contentId);
        var name = contentId[(contentId.IndexOf(':') + 1)..];
        return name.Replace('-', '_').ToUpperInvariant();
    }

    /// <summary>
    /// 遭遇的**派生流**引擎名：`base-spire:encounter:&lt;ENTRY&gt;:&lt;floor&gt;`。
    /// 一条遭遇在一个楼层上一条流（源里 `EncounterModel._rng = new Rng(派生种子)`，见
    /// `EncounterModel.cs:266-270`）；流名只是引擎侧的键，**种子**才是与源对齐的东西。
    /// </summary>
    public static string EncounterStreamName(string sourceEntry, int floor)
    {
        ArgumentNullException.ThrowIfNull(sourceEntry);
        ArgumentOutOfRangeException.ThrowIfNegative(floor);
        return $"base-spire:encounter:{sourceEntry}:{floor}";
    }

    /// <summary>
    /// `EncounterModel.GenerateMonstersWithSlots`（`EncounterModel.cs:268`）：遭遇派生种子 =
    /// `(uint)((int)runSeed + TotalFloor + hash(entry))`。本项目的楼层取**房间号**
    /// （地图未接入时的显式替代，见构建描述的限制）。
    /// </summary>
    public static uint EncounterSeed(uint runSeed, int floor, string sourceEntry) =>
        ContentSeed(runSeed, floor, sourceEntry);

    /// <summary>这一局的全部 run 级流（类型名 + SnakeCase 流名 + 种子）。</summary>
    public static IReadOnlyList<(string Type, string Stream, uint Seed)> RunStreams(string stringSeed)
    {
        var seed = RunSeed(stringSeed);
        return [.. RunStreamTypes.Select(type => (type, SnakeCase(type), StreamSeed(seed, type)))];
    }

    /// <summary>某个玩家槽位的全部玩家级流。</summary>
    public static IReadOnlyList<(string Type, string Stream, uint Seed)> PlayerStreams(string stringSeed,
        int slotIndex)
    {
        var seed = PlayerSeed(stringSeed, slotIndex);
        return [.. PlayerStreamTypes.Select(type => (type, SnakeCase(type), StreamSeed(seed, type)))];
    }

    /// <summary>
    /// T05：幕地图流的**引擎名**：`base-spire:run:act_{n}_map`（n 从 1 起）。它按局生成、
    /// 不属于原版 12+3 条固定流，因此**不**进 `RunStreamTypes`，由地图子包单独登记。
    /// </summary>
    public static string ActMapStreamName(int act)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(act, 1);
        return EngineStreamName(ActMapStreamType(act));
    }

    /// <summary>T05：地图流的原版流类型名（`EngineStreamName` 的输入；`SnakeCase` 下恒等）。</summary>
    public static string ActMapStreamType(int act)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(act, 1);
        return $"act_{act}_map";
    }

    /// <summary>
    /// ENV-T09-unknown-room-modules-20261012：事件级流的**源条目名** = `ModelDb.GetEntry(model)` =
    /// `Slugify(类名)`（`Models/EventModel.cs:BeginEvent` 的种子哈希用的就是它）。
    /// </summary>
    public static string SourceEventEntry(string sourceName) => Slugify(sourceName);

    /// <summary>
    /// 事件级流的引擎名：`base-spire:event:&lt;SLUG&gt;`（与地图流、遭遇派生流一样是内容派生流，
    /// 不属于原版 12+3 条固定流，因此不进 <see cref="RunStreamTypes"/>）。
    /// </summary>
    public static string EventStreamName(string sourceEntry) =>
        $"base-spire:event:{sourceEntry}";

    /// <summary>
    /// 事件级流的种子：源 `EventModel.BeginEvent` 的
    /// `(uint)((uint)((int)RunState.Rng.Seed + (IsShared ? 0 : slot)) + hash(Id.Entry))`。
    /// 32 位加法可结合，因此与 <see cref="ContentSeed"/>（run seed + 槽位 + 条目哈希 + mixin）
    /// 逐位相同；单人槽位恒为 0，共享事件同样取 0。
    /// </summary>
    public static uint EventSeed(uint runSeed, string sourceEntry, int playerSlot = 0) =>
        ContentSeed(runSeed, playerSlot, sourceEntry);

    /// <summary>
    /// T05：地图流种子 = `unchecked(runSeed + DeterministicHashCode("act_{n}_map"))`，
    /// 与源 `Rng` 的"基线种子 + 名字 hash"同一推导（见 `StreamSeed`）。
    /// </summary>
    public static uint ActMapSeed(uint runSeed, int act) => StreamSeed(runSeed, ActMapStreamType(act));

    /// <summary>
    /// 用原版布局把一条流显式种进 <see cref="RandomService"/>（服务必须已经是原版兼容 Profile）。
    /// 引擎侧的流名字由内容包自己定，所以这里由调用方把"自己的流名"对到"原版的流类型"。
    /// </summary>
    public static void SeedStream(RandomService service, string engineStreamName, string stringSeed,
        string runStreamType)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (!service.IsVanillaCompatible)
        {
            throw new InvalidOperationException(
                $"随机服务不是原版兼容 Profile（{service.AlgorithmId} / {service.DrawProfileId}）："
                + $"不能用原版布局种流，否则是静默分歧");
        }

        service.SeedStream(engineStreamName, StreamSeed(RunSeed(stringSeed), runStreamType));
    }
}
