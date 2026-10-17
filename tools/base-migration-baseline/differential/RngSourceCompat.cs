using System.Globalization;
using System.Text.RegularExpressions;

namespace Differential;

/// <summary>
/// **窄源版本兼容 Profile**（ENV-T04 / ENV 约束 §10 与「ENV-T04初始洗牌的派生对齐边界（2026-10-08）」）。
///
/// <para>
/// 为什么需要它：`reference/sts2-sim` 的检出领先 `sts2/` 冻结基线，两侧的**流种子派生**不是同一版本。
/// 负责人已核对源码，确认允许替换的只有三类派生差异：
/// </para>
/// <list type="number">
/// <item>完整游戏 seed 的 **hash32**（`sts2/src/Core/Runs/RunRngSet.cs:107`：
///       `Seed = (uint)StringHelper.GetDeterministicHashCode(seed)`，31 位循环）。参考同位置改为
///       `Seed = StringHelper.GetDeterministicHashCode(seed)`，而该函数在参考检出里返回 **XxHash64**；</item>
/// <item>命名流的 **hash32 + uint 加法回绕**（`sts2/src/Core/Random/Rng.cs:52-55`：
///       `Rng(seed, name) : this(seed + (uint)GetDeterministicHashCode(name))`）。参考是 ulong 相加；</item>
/// <item>**`old` 前缀解释**：`sts2/` 的 `RunRngSet` **没有**该分支，整串参与哈希；参考会去掉 `"old"` 再走旧哈希
///       （`reference/.../Runs/RunRngSet.cs:72-81`）。</item>
/// </list>
/// <para>
/// **不允许**替换的（本类一律不碰）：参考原生默认构造（`RunRngSet(string)` 仍是
/// `usesSemanticKeys=false` 的**顺序**路径）、`MegaRandom`（xoshiro256** + Splitmix64 播种）、抽样映射、
/// 卡牌/敌人/地图/奖励效果。参考侧**不得**调用 base 的 `VanillaRandomLayout` 来制造一致——
/// 本类是按只读 `sts2/` 源码**独立重写**的一份派生，并由
/// <c>tools/rng-oracle/vanilla_rng_oracle.py --source-compat</c> 的独立向量逐值核对。
/// </para>
/// <para>
/// 启用方式与范围：只有显式调用 <see cref="Apply"/> 的 case 才生效，且只覆盖
/// <see cref="RunStreamTypes"/> 这 12 条 run 级具名流；不进入任何生产规则，
/// 也不进入未声明该 Profile 的 case。原始字符串 seed、有效派生值、各流映射与代码摘要都要进入参考身份。
/// </para>
/// </summary>
internal static class RngSourceCompat
{
    /// <summary>Profile 的稳定 id。进记录与参考身份；改了派生就必须换它。</summary>
    public const string ProfileId = "source-compat-sts2-frozen@1";

    /// <summary>本 Profile 允许替换的派生差异（逐条对应 ENV 边界裁决）。</summary>
    public static readonly IReadOnlyList<string> Replaces =
    [
        "run seed hash32（完整游戏字符串，31 位循环）",
        "命名流 hash32 + uint 加法回绕",
        "old 前缀解释（sts2/ 无该分支：整串参与哈希）",
    ];

    /// <summary>本 Profile 明确不动的东西（越界即无效）。</summary>
    public static readonly IReadOnlyList<string> NotReplaced =
    [
        "RunRngSet 默认顺序构造（usesSemanticKeys=false，不是语义重键）",
        "MegaRandom xoshiro256** 与 Splitmix64 播种",
        "抽样映射（NextInt/NextDouble/NextUnsignedInt/Shuffle 等）",
        "卡牌/敌人/地图/奖励效果",
    ];

    /// <summary>suite 里 `rng-initial-shuffle` 声明的 game_seed：两侧必须用**同一个原始字符串**。</summary>
    public const string CaseGameSeed = "ENV-T04-RNG-SHUFFLE-001";

    /// <summary>`sts2/src/Core/Entities/Rngs/RunRngType.cs` 的成员顺序（`Enum.GetValues` 的顺序）。</summary>
    public static readonly IReadOnlyList<string> RunStreamTypes =
    [
        "UpFront", "Shuffle", "UnknownMapPoint", "CombatCardGeneration",
        "CombatPotionGeneration", "CombatCardSelection", "CombatEnergyCosts",
        "CombatTargets", "MonsterAi", "Niche", "CombatOrbs", "TreasureRoomRelics",
    ];

    /// <summary>`sts2/src/Core/Entities/Rngs/PlayerRngType.cs` 的成员。</summary>
    public static readonly IReadOnlyList<string> PlayerStreamTypes = ["Rewards", "Shops", "Transformations"];

    /// <summary>
    /// `sts2/src/Core/Models/Characters/Ironclad.cs:43-55` 的起始牌组声明顺序（5 Strike / 4 Defend / 1 Bash）。
    /// 用它做端到端的首次洗牌向量：牌序错了，洗牌结果就对不上。
    /// </summary>
    public static readonly IReadOnlyList<string> IroncladStartingDeck =
    [
        "strike-ironclad", "strike-ironclad", "strike-ironclad", "strike-ironclad", "strike-ironclad",
        "defend-ironclad", "defend-ironclad", "defend-ironclad", "defend-ironclad", "bash",
    ];

    // `StringHelper.SnakeCase` 用的正则（与 `Slugify` 共用）：`\G` 表示"紧接上一次匹配结束的位置"，
    // 连续大写会各自断开（ABC → A_B_C）。这里逐字照抄只读源，不自行改写语义。
    private static readonly Regex CamelCase = new(@"([A-Za-z0-9]|\G(?!^))([A-Z])", RegexOptions.Compiled);

    /// <summary>
    /// `sts2/src/Core/Helpers/StringHelper.cs:GetDeterministicHashCode`：31 位循环，返回 int；
    /// 调用方 `(uint)` 转换，因此这里按 32 位无符号给出。
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

    /// <summary>`RunRngSet.cs:107`：字符串 seed → 本局 run seed（uint）。</summary>
    public static uint RunSeed(string stringSeed) => DeterministicHashCode(stringSeed);

    /// <summary>`Rng.cs:52-55`：`new Rng(setSeed, name)` 的等价形式（**uint 加法回绕**）。</summary>
    public static uint StreamSeed(uint setSeed, string streamType)
    {
        ArgumentNullException.ThrowIfNull(streamType);
        return unchecked(setSeed + DeterministicHashCode(SnakeCase(streamType)));
    }

    /// <summary>`Player.cs:324-328`：玩家级 seed = 字符串 seed 的 hash32 + 槽位（uint 回绕）。</summary>
    public static uint PlayerSeed(string stringSeed, int slotIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slotIndex);
        return unchecked(DeterministicHashCode(stringSeed) + (uint)slotIndex);
    }

    /// <summary>
    /// 原版 `RunRngType` → 引擎流名的**显式映射**（ENV §8：参考诊断名不同时必须靠显式映射对齐）。
    ///
    /// <para>
    /// 映射必须落在 base **真实使用**的引擎流名上，否则比较器会把"同一条流的两个名字"读成两侧各缺一条。
    /// 已在 base `SliceManifest` 里具名的四条直接引用该常量，编译期绑死，不允许各写一份字符串：
    /// 2026-10-12 修的就是这里——原来自写的 <c>base-spire:random-target</c> /
    /// <c>base-spire:random-card</c> 与 base 实际的 <c>base-spire:combat-random-target</c> /
    /// <c>base-spire:combat-random-card</c> 不是同一个键（只在真的消费了那两条流时才会显形）。
    /// </para>
    /// <para>
    /// 其余 run 级流各占一条 <c>base-spire:run:&lt;snake&gt;</c>。已知两处**不是漏映射**：
    /// <c>act_&lt;n&gt;_map</c> 与 `MonsterAi` 在源里由内容自己 `new Rng(...)` 构造（不是 RunRngSet 成员），
    /// 因此它们走动态/临时 RNG 路径并保留原始名，不在这里硬塞一个同名键。
    /// </para>
    /// </summary>
    public static string EngineStreamName(string runStreamType) => runStreamType switch
    {
        "Shuffle" => BaseSpire.Slice.SliceManifest.DrawStream,
        "CombatTargets" => BaseSpire.Slice.SliceManifest.RandomTargetStream,
        "CombatCardSelection" => BaseSpire.Slice.SliceManifest.RandomCardStream,
        "CombatCardGeneration" => BaseSpire.Slice.SliceManifest.CardGenerationStream,
        _ => $"base-spire:run:{SnakeCase(runStreamType)}",
    };

    /// <summary>
    /// 同一张表里**尚未核对到引擎常量**的键名（base 的 `SliceManifest` 只具名了上表引用的四条）。
    ///
    /// <para>
    /// 它们今天不会显形：没有 case 消费这些流。但一旦被消费，参考侧会投影到
    /// <c>base-spire:run:&lt;snake&gt;</c>，而 base 若用别的引擎常量名，比较器就会把"同一条流"
    /// 读成两侧各缺一条。这里只**如实登记待核对**，不去猜 base 的名字、也不改 base 常量
    /// （引擎流名是 base 主责的既定命名）。核对依据是 base 侧实际 `SeedStream`/消费点，不是源枚举名。
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> UnverifiedKeyNames =
    [
        "UnknownMapPoint → 合成 base-spire:run:unknown_map_point（base 侧消费点未核对）",
        "CombatPotionGeneration → 合成 base-spire:run:combat_potion_generation（base 侧消费点未核对）",
        "CombatEnergyCosts → 合成 base-spire:run:combat_energy_costs（base 侧消费点未核对）",
        "MonsterAi → 合成 base-spire:run:monster_ai（base 侧消费点未核对；源里怪物 RNG 不走 RunRngSet）",
        "CombatOrbs → 合成 base-spire:run:combat_orbs（base 侧消费点未核对）",
        "TreasureRoomRelics → 合成 base-spire:run:treasure_room_relics（base 侧消费点未核对）",
    ];

    /// <summary>`new Rng(seed, name)` 在观察器下留下的**原始**诊断名（`Rng.cs:52` 的 <c>"named." + name</c>）。</summary>
    public static string DiagnosticStreamName(string streamType) => "named." + SnakeCase(streamType);

    /// <summary>玩家级流的显式映射：引擎流名 ↔ 原始诊断名（两边都按 <c>SnakeCase</c> 派生）。</summary>
    public static IReadOnlyList<(string EngineStream, string Diagnostic)> PlayerStreamNames() =>
    [
        .. PlayerStreamTypes.Select(type =>
            ($"base-spire:player:{SnakeCase(type)}", DiagnosticStreamName(type))),
    ];

    /// <summary>这一局的 12 条 run 级具名流（原版类型名 + 引擎流名 + 源版本派生种子）。</summary>
    public static IReadOnlyList<(string Type, string Stream, uint Seed)> RunStreams(string stringSeed)
    {
        var runSeed = RunSeed(stringSeed);
        return [.. RunStreamTypes.Select(type => (type, EngineStreamName(type), StreamSeed(runSeed, type)))];
    }

    /// <summary>玩家槽位的 3 条玩家级流（原版类型名 + 派生种子）。</summary>
    public static IReadOnlyList<(string Type, uint Seed)> PlayerStreams(string stringSeed, int slotIndex)
    {
        var playerSeed = PlayerSeed(stringSeed, slotIndex);
        return [.. PlayerStreamTypes.Select(type => (type, StreamSeed(playerSeed, type)))];
    }

    /// <summary>
    /// 独立重写的 Fisher-Yates 洗牌（`Rng.cs:308-320`：`for (i = n-1; i &gt; 0; i--) { j = NextInt(i+1); swap }`），
    /// 用自己的 xoshiro256** 走一遍，用来核对外部给出的洗牌结果。返回洗好的列表与抽取次数。
    /// </summary>
    public static (IReadOnlyList<string> Deck, int Draws) ShuffleDeck(uint streamSeed, IReadOnlyList<string> deck)
    {
        var generator = new Xoshiro256StarStar(streamSeed);
        var items = deck.ToList();
        var draws = 0;
        for (var index = items.Count - 1; index > 0; index--)
        {
            // `MegaRandom.Next(maxValue)` = `(int)(NextDouble() * maxValue)`（双精度截断，不是拒绝采样）。
            var other = (int)(generator.NextDouble() * (index + 1));
            draws++;
            (items[index], items[other]) = (items[other], items[index]);
        }

        return (items, draws);
    }

    /// <summary>流状态的**规范化指纹**：与 core `RandomService.RandomStream.Fingerprint()` 同一公式。</summary>
    public static ulong Fingerprint(ulong s0, ulong s1, ulong s2, ulong s3) =>
        Mix(s0 ^ RotateLeft(s1, 13) ^ RotateLeft(s2, 29) ^ RotateLeft(s3, 47));

    public static string FingerprintText(ulong s0, ulong s1, ulong s2, ulong s3) =>
        "0x" + Fingerprint(s0, s1, s2, s3).ToString("x16", CultureInfo.InvariantCulture);

    /// <summary>
    /// 把底层生成器状态按**字数**往回对齐：返回使源兼容模型的状态等于给定 4 个字所需的最小抽数，
    /// 预算内对不上返回 -1。用于把参考侧「API 调用计数」换算成与 base 同单位的**底层 64 位字数**。
    /// </summary>
    public static long WordsForState(ulong seed, ulong s0, ulong s1, ulong s2, ulong s3, int maxWords)
    {
        var generator = new Xoshiro256StarStar(seed);
        for (var step = 0; step <= maxWords; step++)
        {
            if (generator.Matches(s0, s1, s2, s3)) { return step; }
            generator.NextULong();
        }

        return -1;
    }

    // -- core `RandomService` 的两个小工具（同一公式，用于规范化指纹） --------------------------------

    internal static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    internal static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));

    /// <summary>
    /// `sts2/src/Core/Random/MegaRandom.cs` 的**独立重写**（只覆盖本 Profile 需要的入口）：
    /// 用它复算洗牌，与参考的 `Rng` 互证——两边都错才会一起错。
    /// </summary>
    internal sealed class Xoshiro256StarStar
    {
        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        public Xoshiro256StarStar(ulong seed)
        {
            _s0 = Splitmix64(ref seed);
            _s1 = Splitmix64(ref seed);
            _s2 = Splitmix64(ref seed);
            _s3 = Splitmix64(ref seed);
        }

        private static ulong Splitmix64(ref ulong x)
        {
            var value = x += 0x9E3779B97F4A7C15UL;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }

        public ulong NextULong()
        {
            var s0 = _s0;
            var s1 = _s1;
            var s2 = _s2;
            var s3 = _s3;
            var result = RotateLeft(s1 * 5, 7) * 9;
            var temporary = s1 << 17;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= temporary;
            s3 = RotateLeft(s3, 45);
            _s0 = s0;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            return result;
        }

        public double NextDouble() => (NextULong() >> 11) * 1.1102230246251565E-16;

        /// <summary>状态是否等于给定 4 个字（只读比较，供把 API 调用数换算成底层字数用）。</summary>
        public bool Matches(ulong s0, ulong s1, ulong s2, ulong s3) =>
            _s0 == s0 && _s1 == s1 && _s2 == s2 && _s3 == s3;
    }

#if REFERENCE_SIM
    /// <summary>
    /// 把源版本派生**逐流**装进一个**参考原生**的 <c>RunRngSet</c>：用参考自己的 `MockRng`
    /// （其注释即 "ONLY USE THIS FOR TESTING"，写的是裸 `new Rng(seed)`）。
    ///
    /// <para>
    /// 关键：我们**不**构造新的 RNG 类型、不替换 `MegaRandom`、不重键、也不调用 base 的派生实现——
    /// 只把每条具名流的种子换成按只读 `sts2/` 源码独立算出的值。`RunRngSet` 的默认顺序语义
    /// （`usesSemanticKeys=false`）、`Rng.Shuffle`、`MegaRandom` 全部保持参考原生行为。
    /// </para>
    /// <para>
    /// 返回实际装上的 (原版流类型, 引擎流名, 种子) 列表，供参考身份与独立向量核对。
    /// </para>
    /// </summary>
    public static IReadOnlyList<(string Type, string Stream, uint Seed)> Apply(
        Sts2Sim.Core.Runs.RunRngSet rng, string stringSeed)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (rng.UsesSemanticKeys)
        {
            // 语义重键是明确禁止用于保真对齐的路径（ENV 边界：默认顺序才是保真路径）。
            throw new InvalidOperationException(
                $"源版本兼容 Profile 只允许装在参考的默认顺序路径上，但该 RunRngSet 是语义重键模式");
        }

        var applied = new List<(string, string, uint)>();
        foreach (var (type, stream, seed) in RunStreams(stringSeed))
        {
            var rngType = Enum.Parse<Sts2Sim.Core.Entities.Rngs.RunRngType>(type);
            if (!rng.UsesSemanticKeys && rng.GetRng(rngType).Counter != 0)
            {
                throw new InvalidOperationException($"流 '{type}' 已经被抽过，不能换种子（那是静默分歧）");
            }

            rng.MockRng(rngType, seed);
            applied.Add((type, stream, seed));
        }

        return applied;
    }

    /// <summary>
    /// **受控状态注入后的工具侧随机游标对齐**（ENV-T04 重洗比较缺口）。
    ///
    /// <para>
    /// 受控 case 两侧都由夹具声明"精确牌区顺序"。base 的受控切片按声明顺序发牌，**不**消费开局洗牌；
    /// 参考侧的战斗开始是自己的原生流程（<c>Player.PopulateCombatState → DrawPile.RandomizeOrderInternal</c>
    /// → 不稳定 Fisher-Yates），必然从 <c>Shuffle</c> 流抽一次开局洗牌。适配器随后用
    /// <see cref="ReferenceBackend"/> 的 <c>ApplyOrdered</c> 把牌区覆盖成声明顺序，但那次抽掉的
    /// 随机数不会回来，于是**脚本开始前**参考的 <c>Shuffle</c> 游标比 base 多一次开局洗牌的偏移，
    /// 重洗比较会在这个纯工具侧偏移上分叉。
    /// </para>
    /// <para>
    /// 本方法只把**这一条** run 级流按源版本 Profile 的种子重新装回（游标归零），对齐成与 base
    /// 受控注入相同的随机状态；不碰其他流、不替换参考原生 <c>MegaRandom</c>/抽样/洗牌算法、
    /// 不改任何一个 case 的 seed/维度/Profile 声明。调用点与前后游标会写进参考记录，不做静默对齐。
    /// </para>
    /// </summary>
    public static (long Before, long After) RestoreStream(
        Sts2Sim.Core.Runs.RunRngSet rng, string runStreamType, string stringSeed)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (rng.UsesSemanticKeys)
        {
            throw new InvalidOperationException(
                $"源版本兼容 Profile 只允许装在参考的默认顺序路径上，但该 RunRngSet 是语义重键模式");
        }

        var rngType = Enum.Parse<Sts2Sim.Core.Entities.Rngs.RunRngType>(runStreamType);
        var before = rng.GetRng(rngType).Counter;
        rng.MockRng(rngType, StreamSeed(RunSeed(stringSeed), runStreamType));
        return (before, rng.GetRng(rngType).Counter);
    }
#endif
}
