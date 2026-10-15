#if REFERENCE_SIM
using System.Globalization;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Differential;

/// <summary>
/// **参考侧运行层入口**（ENV-T04 源随机包）：用一个**同一原始字符串 game seed** 真的建一局并走到
/// 「首次洗牌 + 抽起手」这个共同稳定边界，然后产出与 base 侧同构的规范化维度。
///
/// <para>
/// 它执行的是**参考自己的**运行/洗牌逻辑（`RunState` 的房间预生成、`RelicGrabBag`、遭遇取用、
/// `CombatRoom.Prepare` 出生、`CombatEngine.StartCombatAsync` 的 `Rng.Shuffle` 与起手抽取），
/// 不复制 base 记录、也不替参考"补"任何一步：base 侧的初始化缺口照原样留着，
/// 差异由比较器报出来。
/// </para>
/// <para>
/// **唯一被替换的东西**是具名流的种子派生：<see cref="RngSourceCompat.Apply"/> 用参考自己的
/// <c>RunRngSet.MockRng</c> 把 12 条 run 级具名流换成按只读 <c>sts2/</c> 源码独立算出的源版本种子。
/// 参考原生默认构造（`usesSemanticKeys=false` 的顺序路径）、`MegaRandom`、`Shuffle` 语义全部保持原样。
/// 参考身份（启用方式/范围/原始 seed/各流映射/代码摘要/实际源文件摘要）写进记录的
/// <see cref="CaseRecord.ReferenceIdentity"/>。
/// </para>
/// </summary>
internal static class ReferenceRunEntry
{
    public const string Name = "reference:sts2-sim";
    public const string Version = "env-t04-run-entry@1";

    /// <summary>规范化的"被触及即比较"流集合判据，与 base 侧 `RandomService.Diagnostics()` 一致。</summary>
    private const int TouchedDrawsThreshold = 1;

    public static CaseRecord Run(FixtureCase fixture)
    {
        var notes = new List<string>
        {
            "参考侧执行自己的运行层与首次洗牌：RunState(string seed) → Player.CreateForNewRun → "
                + "RoomFactory.CreateRoom(Monster) → room.Enter()（遭遇取用/怪物出生/StartCombat 洗牌与起手都在里面）。",
            "本 case 的参考侧只跑「开局到首次洗牌/起手」这一段，不驱动整场战斗，也不复制 base 结果。",
            "streams 维度按「被触及的流」收录：两侧同一判据（抽取计数 > 0），流名用显式源流映射对齐"
                + "（ENV §8），未映射到源流的引擎私有流不进入比较。",
            "指纹是流状态的**规范化**表达（同一公式的 4 状态字混合），不是参考的原生读数；"
                + "两侧同一公式才可比。",
            "参考身份里的 Profile 只替换具名流种子派生；MegaRandom/抽样/游戏效果未改。",
        };

        var boundaries = new List<BoundaryRecord>();
        if (fixture.Script.Count > 0)
        {
            return new CaseRecord(fixture.Id, Name, Version, fixture.RngProfile, "invalid_fixture",
                "运行层参考入口只支持空脚本的「开局到首次洗牌」边界，不接受脚本步骤", false, notes, boundaries);
        }

        if (string.IsNullOrWhiteSpace(fixture.GameSeed))
        {
            return new CaseRecord(fixture.Id, Name, Version, fixture.RngProfile, "invalid_fixture",
                "运行层参考入口需要每局字符串 game_seed", false, notes, boundaries);
        }

        var gameSeed = fixture.GameSeed!;
        CombatState combat;
        Player player;
        RunRngSet rng;
        IReadOnlyList<(string Type, string Stream, uint Seed)> applied;
        try
        {
            (combat, player, rng, applied) = Build(gameSeed);
        }
        catch (Exception failure)
        {
            return new CaseRecord(fixture.Id, Name, Version, fixture.RngProfile, "faulted",
                $"{failure.GetType().Name}: {failure.Message}", false, notes, boundaries);
        }

        boundaries.Add(new BoundaryRecord(
            0,
            "start",
            $"参考正式开局（RunState(\"{gameSeed}\") + Overgrowth + 首个普通战斗 Enter）",
            ReferenceBackend.LegalActions(combat, player),
            [],
            ReferenceBackend.Facts(combat, player, exactPileOrder: fixture.RngProfile == RngProfiles.VanillaCompatible),
            Streams(rng, player),
            null));

        return new CaseRecord(fixture.Id, Name, Version, fixture.RngProfile, "ok", null, false, notes, boundaries)
        {
            SessionStatus = "Running(combat-started)",
            ReferenceIdentity = Identity(gameSeed, applied),
        };
    }

    // -- 参考侧自己的建局与首次洗牌 -------------------------------------------------------------------------

    private static (CombatState Combat, Player Player, RunRngSet Rng,
        IReadOnlyList<(string Type, string Stream, uint Seed)> Applied) Build(string gameSeed)
    {
        CombatReplayDemo.EnsureModelsRegistered();

        // 1) 参考自己的建局：字符串 seed → RunRngSet（默认**顺序**路径，不是语义重键）。
        var run = new RunState(gameSeed, new Overgrowth());

        // 2) 唯一被替换的一步：按源版本把 12 条 run 级具名流的种子换掉（在**任何抽取之前**）。
        var applied = RngSourceCompat.Apply(run.Rng, gameSeed);

        // 3) 参考自己的玩家与遗物袋构造（遗物袋走 UpFront，是首次洗牌之前的真实前置消费）。
        var player = Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), run);
        run.AddPlayer(player);

        // 4) 进入第一个普通战斗房间：遭遇取用、怪物出生与 StartCombat 的洗牌/起手都在 room.Enter 里。
        var room = (CombatRoom)RoomFactory.CreateRoom(run, RoomType.Monster);
        run.PushRoom(room);
        room.Enter(run).GetAwaiter().GetResult();

        return (room.Engine.State, player, run.Rng, applied);
    }

    // -- 规范化维度 -----------------------------------------------------------------------------------------

    /// <summary>
    /// 参考侧的随机流读数：把**被触及**的具名流（抽取计数 &gt; 0）按显式映射投影成引擎流名，
    /// 读它自己的 counter 与底层生成器状态，再算规范化指纹。观察不消费随机数。
    /// </summary>
    private static IReadOnlyList<StreamSnapshot> Streams(RunRngSet rng, Player player)
    {
        var snapshots = new List<StreamSnapshot>();
        foreach (var type in Enum.GetValues<RunRngType>())
        {
            var stream = rng.GetRng(type);
            Add(snapshots, RngSourceCompat.EngineStreamName(type.ToString()), stream);
        }

        foreach (var type in Enum.GetValues<PlayerRngType>())
        {
            var stream = player.PlayerRng!.GetRng(type);
            Add(snapshots, $"base-spire:player:{RngSourceCompat.SnakeCase(type.ToString())}", stream);
        }

        return [.. snapshots.OrderBy(item => item.Stream, StringComparer.Ordinal)];
    }

    private static void Add(List<StreamSnapshot> snapshots, string streamName, Sts2Sim.Core.Random.Rng stream)
    {
        if (stream.Counter < TouchedDrawsThreshold)
        {
            return;
        }

        var state = stream.ToSerializable();
        snapshots.Add(new StreamSnapshot(streamName, stream.Counter,
            RngSourceCompat.FingerprintText(state.state0, state.state1, state.state2, state.state3)));
    }

    // -- 参考身份 -------------------------------------------------------------------------------------------

    internal static ReferenceIdentity Identity(
        string gameSeed, IReadOnlyList<(string Type, string Stream, uint Seed)> applied)
    {
        var (head, headSource) = Repo.GitHead("reference/sts2-sim");
        var profileCode = Repo.DigestFiles(["tools/differential/RngSourceCompat.cs"]);
        return new ReferenceIdentity(
            Name,
            RngSourceCompat.ProfileId,
            "显式：只有声明该 Profile 的 case 调用 RngSourceCompat.Apply；经参考自己的 RunRngSet.MockRng 逐流装载",
            "仅 12 条 run 级具名流；参考默认顺序路径（usesSemanticKeys=false）；玩家级/内容级派生不改",
            gameSeed,
            RngSourceCompat.RunSeed(gameSeed),
            [.. applied.Select(item => new ReferenceStreamSeed(item.Type, item.Stream, item.Seed))],
            RngSourceCompat.Replaces,
            RngSourceCompat.NotReplaced,
            profileCode.Count > 0 && profileCode[0].Exists ? profileCode[0].Sha256 : null,
            head,
            headSource,
            SourceFiles());
    }

    /// <summary>
    /// 实际源文件摘要：优先复用负责人本轮登记的
    /// <c>.audit/dsh-supervisor/review-2026-10-08-1639/rng-source-inputs.json</c>（path + sha256），
    /// 并用工作区**当前**文件重算一遍；两者不一致时如实记录（HEAD 不能替代实际摘要）。
    /// </summary>
    private static IReadOnlyList<ReferenceFileDigest> SourceFiles()
    {
        var relative = ".audit/dsh-supervisor/review-2026-10-08-1639/rng-source-inputs.json";
        var full = Path.Combine(Repo.Root(), relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full))
        {
            return [];
        }

        List<ReferenceFileDigest> digests = [];
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(full));
        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString() ?? string.Empty;
            var registered = entry.GetProperty("sha256").GetString() ?? string.Empty;
            var current = Repo.DigestFiles([path]);
            var actual = current.Count > 0 && current[0].Exists ? current[0].Sha256 : "<missing>";
            digests.Add(new ReferenceFileDigest(path,
                actual == registered ? actual : $"{actual}（登记={registered}，不一致）"));
        }

        return digests;
    }
}
#endif
