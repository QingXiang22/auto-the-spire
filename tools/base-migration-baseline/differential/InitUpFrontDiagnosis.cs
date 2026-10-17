#if REFERENCE_SIM
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Differential;

/// <summary>
/// ENV-T04 初始化首差的**只读**诊断（`init-upfront`）：同一个原始字符串 game seed 下，把参考自己
/// 的开局初始化按阶段切开，逐段读 <c>Rng.UpFront</c> 的 `Counter`，并用一段**观测副本**重放
/// <c>RunState.GenerateAllActRooms / GenerateActRooms</c> 的原文，把「277 次额外消费」分解到具体阶段。
///
/// <para>
/// 观测纪律：本入口不改参考玩法、不改 base 玩法、不注入状态、不强设敌人、不补抽、不删池。
/// 唯一新增的东西是 (a) 阶段之间的 `Counter` 读数，(b) 一个 `CloneExact()` 出来的独立副本
/// （`Rng.cs:93-99`：深拷贝生成器状态，副本上的抽取不影响真实流）。池内容只枚举、只读。
/// </para>
/// <para>
/// 本入口不裁决对错：它只给「源/参考阶段 → 抽取次数 → 期望 vs 实测」的可核对数字，供负责人做窄裁定。
/// </para>
/// </summary>
internal static class ReferenceInitDiagnosis
{
    public const string Version = "env-t04-init-diagnosis@1";

    /// <summary>引擎侧流名（与 base 记录、`VanillaRandomLayout` 一致）。</summary>
    public const string UpFrontStreamName = "base-spire:run:up_front";

    /// <summary>`RunState.cs:32` 的 private const，逐字照抄用于重放（不在参考侧新增公开 API）。</summary>
    private const int EliteEncounterPoolSize = 15;

    /// <summary>`RelicGrabBag.cs:19-22` 的默认分桶稀有度，逐字照抄用于独立复算期望洗牌次数。</summary>
    private static readonly RelicRarity[] BucketedRarities =
        [RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop];

    public static int Run(string? gameSeed, string outputDirectory, string? recordsDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameSeed))
        {
            Console.Error.WriteLine("init-upfront 需要一个非空 --seed <原始字符串 game seed>（每局字符串种子，不是 uint）。");
            return 2;
        }

        Directory.CreateDirectory(outputDirectory);
        CombatReplayDemo.EnsureModelsRegistered();

        var stages = new List<(string Name, int Counter, int Delta, string Note)>();
        var previous = 0;
        void Stage(string name, int counter, string note)
        {
            stages.Add((name, counter, counter - previous, note));
            previous = counter;
        }

        // ---- 真实参考开局：与 `ReferenceRunEntry.Build` 的 1)~4) 逐步等价，只在每步之间读 Counter ----
        var run = new RunState(gameSeed, new Overgrowth());
        Stage("run_constructed", run.Rng.UpFront.Counter,
            "new RunState(gameSeed, new Overgrowth())：构造只生成地图（独立 act_N_map 派生流），不消费 UpFront");

        var applied = RngSourceCompat.Apply(run.Rng, gameSeed);
        Stage("profile_applied", run.Rng.UpFront.Counter,
            "RngSourceCompat.Apply：唯一替换步，只换 12 条 run 级具名流种子，不改抽样语义");

        // 观测副本：counter=0 的独立深拷贝；之后所有重放只在副本上抽取。
        var probe = run.Rng.UpFront.CloneExact();
        if (probe.Counter != 0)
        {
            Console.Error.WriteLine($"诊断自身前置条件不成立：UpFront 观测副本开始时 counter={probe.Counter}（期望 0）。");
            return 1;
        }

        var character = ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>();
        var player = Player.CreateForNewRun(character, run);
        Stage("player_created", run.Rng.UpFront.Counter,
            "Player.CreateForNewRun：起始遗物直接授予（不抽），消费来自 CreatePlayerRelicGrabBag 的两次稀有度桶洗牌");

        run.AddPlayer(player);
        Stage("player_added", run.Rng.UpFront.Counter, "run.AddPlayer(player)：本身不消费，留作界定位");

        var room = (CombatRoom)RoomFactory.CreateRoom(run, RoomType.Monster);
        Stage("room_created", run.Rng.UpFront.Counter,
            "RoomFactory.CreateRoom(Monster)：惰性房间；此处若已涨，说明整幕预生成在遭遇取用时触发");

        run.PushRoom(room);
        Stage("room_pushed", run.Rng.UpFront.Counter, "run.PushRoom(room)");

        room.Enter(run).GetAwaiter().GetResult();
        Stage("room_entered", run.Rng.UpFront.Counter,
            "room.Enter：整幕预生成（若前述未触发）+ 遭遇取用/怪物出生/StartCombat 洗牌与起手");

        var observedTotal = run.Rng.UpFront.Counter;

        // ---- 阶段重放：`RunState.cs:271-353` 原文，只在副本上执行 ----
        var replay = new List<(string Name, int Draws, int Counter, string Detail)>();
        var replayPrevious = probe.Counter;
        void ReplayStage(string name, string detail)
        {
            var now = probe.Counter;
            replay.Add((name, now - replayPrevious, now, detail));
            replayPrevious = now;
        }

        var sharedRelics = SharedRelicPool.Instance.AllRelics.ToList();
        var characterRelics = character.RelicPool.AllRelics.ToList();

        _ = new RelicGrabBag(probe, SharedRelicPool.Instance.AllRelics, includeAllRarities: true);
        ReplayStage("shared_relic_bag", "run_setup/relic_bag/shared（includeAllRarities: true ⇒ 全部稀有度分桶各洗一次）");

        _ = new RelicGrabBag(probe, SharedRelicPool.Instance.AllRelics.Concat(character.RelicPool.AllRelics));
        ReplayStage("character_relic_bag", "run_setup/relic_bag/character=IRONCLAD（共享池 ∪ 角色池，只分 4 个默认桶）");

        _ = SharedAncientPool.All.ToList().UnstableShuffle(probe);
        ReplayStage("shared_ancients_shuffle", "run_setup/shared_ancients/shuffle");

        var replayAct = new Overgrowth();
        var events = replayAct.EffectiveEventPool.ToList();
        events.UnstableShuffle(probe);
        ReplayStage("act0_events_shuffle",
            $"run_setup/act=0/events/shuffle（EffectiveEventPool = 本幕事件池 ∪ 共享事件池 = {events.Count} 条）");

        var normalPicks = new List<string>();
        for (var index = 0; index < replayAct.BaseNumberOfRooms; index++)
        {
            var pick = replayAct.PickEncounter(RoomType.Monster, probe);
            normalPicks.Add($"{index}:{pick.IdEntry}");
        }

        ReplayStage("act0_normal_slots",
            $"run_setup/act=0/encounter/normal/slot=0..{replayAct.BaseNumberOfRooms - 1}（前 {replayAct.NumberOfWeakEncounters} 槽走弱池，其余走常规池；每槽 1 次成功抽取，被拒绝的尝试同样消费）");

        for (var index = 0; index < EliteEncounterPoolSize; index++)
        {
            _ = replayAct.PickEncounter(RoomType.Elite, probe);
        }

        ReplayStage("act0_elite_slots",
            $"run_setup/act=0/encounter/elite/slot=0..{EliteEncounterPoolSize - 1}（EliteEncounterPoolSize=15，无 predicate）");

        var bossPick = replayAct.PickEncounter(RoomType.Boss, probe);
        ReplayStage("act0_boss", $"run_setup/act=0/encounter/boss/slot=0（NextItem(BossEncounters) = {bossPick.IdEntry}）");

        var ancientPick = probe.NextItem(replayAct.AncientPool.Concat(Array.Empty<Type>()));
        ReplayStage("act0_ancient",
            $"run_setup/act=0/ancient/slot=0（候选 {replayAct.AncientPool.Count} 个；NextItem 即使单候选也消费）= {ancientPick?.Name ?? "<null>"}");

        var replayTotal = probe.Counter;

        // ---- 独立复算：两个遗物袋的洗牌次数应当等于 Σ(桶大小-1) ----
        var expectedShared = ExpectedBucketDraws(sharedRelics, allRarities: true);
        var expectedCharacter = ExpectedBucketDraws(
            SharedRelicPool.Instance.AllRelics.Concat(character.RelicPool.AllRelics), allRarities: false);

        // ---- 两侧既有记录里的 up_front 抽取次数与首个战斗事实（只读生成物，不重跑）----
        var baseRecordPath = Path.Combine(recordsDirectory ?? Path.Combine("artifacts", "environment", "tests", "regression", "records"), "base", "rng-initial-shuffle.json");
        var referenceRecordPath = Path.Combine(recordsDirectory ?? Path.Combine("artifacts", "environment", "tests", "regression", "records"), "reference", "rng-initial-shuffle.json");
        var baseRecord = ReadJson(baseRecordPath);
        var referenceRecord = ReadJson(referenceRecordPath);

        // ---- JSON ----
        var stageArray = new JsonArray();
        foreach (var (name, counter, delta, note) in stages)
        {
            stageArray.Add(new JsonObject
            {
                ["stage"] = name,
                ["counter"] = counter,
                ["delta"] = delta,
                ["note"] = note,
            });
        }

        var replayArray = new JsonArray();
        foreach (var (name, draws, counter, detail) in replay)
        {
            replayArray.Add(new JsonObject
            {
                ["stage"] = name,
                ["draws"] = draws,
                ["counter"] = counter,
                ["detail"] = detail,
            });
        }

        var relicBuckets = new JsonArray();
        foreach (var group in sharedRelics.GroupBy(relic => relic.Rarity).OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
        {
            relicBuckets.Add(new JsonObject
            {
                ["rarity"] = group.Key.ToString(),
                ["shared_pool"] = group.Count(),
                ["shared_shuffle_draws"] = Math.Max(0, group.Count() - 1),
            });
        }

        var picksArray = new JsonArray();
        foreach (var pick in normalPicks)
        {
            picksArray.Add(pick);
        }

        var tagsArray = new JsonArray();
        foreach (var tag in room.Encounter?.Tags ?? [])
        {
            tagsArray.Add(tag.ToString());
        }

        var slotsArray = new JsonArray();
        foreach (var slot in room.Encounter?.Slots ?? [])
        {
            slotsArray.Add(slot);
        }

        var document = new JsonObject
        {
            ["tool"] = "tools/differential init-upfront",
            ["version"] = Version,
            ["generated_at"] = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            ["game_seed"] = gameSeed,
            ["run_seed_u32"] = RngSourceCompat.RunSeed(gameSeed),
            ["up_front_stream_seed_u32"] = RngSourceCompat.StreamSeed(RngSourceCompat.RunSeed(gameSeed), "UpFront"),
            ["applied_stream_count"] = applied.Count,
            ["observed"] = new JsonObject
            {
                ["up_front_total_draws"] = observedTotal,
                ["stages"] = stageArray,
            },
            ["replay"] = new JsonObject
            {
                ["isolation"] = "run.Rng.UpFront.CloneExact()：深拷贝生成器状态，副本抽取不影响真实流",
                ["source"] = "RunState.cs:271-353 GenerateAllActRooms/GenerateActRooms 原文（act 用同初始状态的新 Overgrowth 实例）",
                ["total_draws"] = replayTotal,
                ["matches_observed"] = replayTotal == observedTotal,
                ["stages"] = replayArray,
            },
            ["pools"] = new JsonObject
            {
                ["shared_relic_pool_size"] = sharedRelics.Count,
                ["character_relic_pool_size"] = characterRelics.Count,
                ["relic_buckets"] = relicBuckets,
                ["expected_shared_relic_shuffle_draws"] = expectedShared,
                ["expected_character_relic_shuffle_draws"] = expectedCharacter,
                ["shared_ancient_pool_size"] = SharedAncientPool.All.Count,
                ["act0_event_pool_size"] = events.Count,
                ["act0_monster_candidates"] = replayAct.MonsterEncounterCandidates.Count,
                ["act0_elite_candidates"] = replayAct.EliteEncounterCandidates.Count,
                ["act0_boss_candidates"] = replayAct.BossEncounterCandidates.Count,
                ["act0_base_number_of_rooms"] = replayAct.BaseNumberOfRooms,
                ["act0_number_of_weak_encounters"] = replayAct.NumberOfWeakEncounters,
                ["act0_ancient_candidates"] = replayAct.AncientPool.Count,
                ["elite_encounter_pool_size"] = EliteEncounterPoolSize,
            },
            ["reference_first_room"] = new JsonObject
            {
                ["encounter_id_entry"] = room.Encounter?.IdEntry,
                ["encounter_name"] = room.Encounter?.Name ?? room.EncounterName,
                ["is_weak"] = room.Encounter?.IsWeak,
                ["tags"] = new JsonArray([.. (room.Encounter?.Tags ?? []).Select(tag => (JsonNode)tag.ToString())]),
                ["slots"] = new JsonArray([.. (room.Encounter?.Slots ?? []).Select(slot => (JsonNode)slot)]),
                ["normal_slot_sequence"] = picksArray,
            },
            ["records"] = new JsonObject
            {
                ["base_path"] = baseRecordPath,
                ["reference_path"] = referenceRecordPath,
                ["base_up_front_draws"] = StreamDraws(baseRecord, UpFrontStreamName),
                ["reference_up_front_draws"] = StreamDraws(referenceRecord, UpFrontStreamName),
                ["base_enemy_facts"] = EnemyFacts(baseRecord),
                ["reference_enemy_facts"] = EnemyFacts(referenceRecord),
            },
        };

        var outputPath = Path.Combine(outputDirectory, "init-upfront-diagnosis.json");
        File.WriteAllText(outputPath, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // ---- 控制台摘要（数字优先，供报告直接引用）----
        Console.WriteLine($"参考初始化 up_front 实测总抽取 = {observedTotal}（game_seed=\"{gameSeed}\"）");
        foreach (var (name, counter, delta, _) in stages)
        {
            Console.WriteLine($"  [counter {counter,4}] +{delta,4}  {name}");
        }

        Console.WriteLine($"阶段重放总抽取 = {replayTotal}（与实测 {(replayTotal == observedTotal ? "一致" : "不一致")}）");
        foreach (var (name, draws, counter, _) in replay)
        {
            Console.WriteLine($"  [counter {counter,4}] +{draws,4}  {name}");
        }

        Console.WriteLine($"遗物袋洗牌期望：共享 {expectedShared} / 角色 {expectedCharacter}");
        Console.WriteLine($"记录里的 up_front 抽取：base {StreamDraws(baseRecord, UpFrontStreamName)} / reference {StreamDraws(referenceRecord, UpFrontStreamName)}");
        Console.WriteLine($"参考首个普通战斗遭遇 = {room.Encounter?.IdEntry}（槽位 {string.Join(",", room.Encounter?.Slots ?? [])}）");
        Console.WriteLine($"诊断写入 {outputPath}");

        if (replayTotal != observedTotal)
        {
            Console.Error.WriteLine(
                $"诊断不成立：重放总抽取 {replayTotal} ≠ 实测 {observedTotal}——阶段分解尚未覆盖全部消费点，不得据此下结论。");
            return 1;
        }

        return StartupTrace.Run(gameSeed, outputDirectory) ? 0 : 1;
    }

    /// <summary>Σ(桶大小 − 1)：`RelicGrabBag.cs:31-43` 每个稀有度桶洗一次，`Rng.Shuffle` 消费 n−1 次。</summary>
    private static int ExpectedBucketDraws(IEnumerable<RelicModel> pool, bool allRarities) =>
        pool.Where(relic => allRarities || BucketedRarities.Contains(relic.Rarity))
            .GroupBy(relic => relic.Rarity)
            .Sum(group => Math.Max(0, group.Count() - 1));

    private static JsonNode? ReadJson(string path) =>
        File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null;

    private static IEnumerable<JsonNode> Walk(JsonNode? node)
    {
        if (node is null)
        {
            yield break;
        }

        yield return node;
        switch (node)
        {
            case JsonObject obj:
                foreach (var pair in obj)
                {
                    foreach (var child in Walk(pair.Value))
                    {
                        yield return child;
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    foreach (var child in Walk(item))
                    {
                        yield return child;
                    }
                }

                break;
        }
    }

    /// <summary>记录里某个流的抽取次数（记录结构由 CaseRecord 序列化决定，这里按字段名容错查找）。</summary>
    private static int? StreamDraws(JsonNode? root, string streamName)
    {
        foreach (var node in Walk(root).OfType<JsonObject>())
        {
            if (node["stream"] is JsonValue value
                && value.TryGetValue<string>(out var name)
                && name == streamName
                && node["draws"] is JsonValue draws
                && draws.TryGetValue<int>(out var count))
            {
                return count;
            }
        }

        return null;
    }

    /// <summary>记录里 `enemy.*` 事实（id.hp / id.alive 一类）读成扁平对象，用于对齐首个战斗。</summary>
    private static JsonObject EnemyFacts(JsonNode? root)
    {
        var facts = new JsonObject();
        foreach (var node in Walk(root).OfType<JsonObject>())
        {
            foreach (var pair in node)
            {
                if (pair.Key.StartsWith("enemy.", StringComparison.Ordinal) && pair.Value is JsonValue value)
                {
                    facts[pair.Key] = JsonValue.Create(value.ToString());
                }
            }
        }

        return facts;
    }
}
#else
namespace Differential;

internal static class ReferenceInitDiagnosis
{
    public const string Version = "env-t04-init-diagnosis@1";

    public static int Run(string? gameSeed, string outputDirectory, string? recordsDirectory)
    {
        Console.Error.WriteLine("本机构建未包含参考侧适配器（未找到 reference/sts2-sim），无法运行 init-upfront 诊断。");
        return 3;
    }
}
#endif
