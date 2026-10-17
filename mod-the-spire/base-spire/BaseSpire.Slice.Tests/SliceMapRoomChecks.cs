using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using BaseSpire.Slice.SourceMap;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// T05 地图/房间接线检查。
///
/// 只用**生产入口**（<see cref="BaseSpireRuntimeFactory"/> + <see cref="BaseSpireScope.DevMap"/>）建会话，
/// 再从公开状态读地图事实。对照值是移植件 <see cref="SourceActMapCompute"/> 在同一批抽取上的独立重放
/// （同一批 payload 走 `scratch/SourceMap/` 的逐字移植），不是把被测实现的输出再抄一遍。
///
/// 覆盖简报 §6 的 7 项：身份、两 seed 轨迹、合法性、未实现房型、结算出口、抽数与流、回归
/// （回归由既有 333 条 + 本文件的身份检查共同承担）。
/// </summary>
internal static class SliceMapRoomChecks
{
    private const string RunTypeId = "base-spire:Run";
    private const string RunStatesCollection = "base-spire:RunStates";
    private const string NodeTypeId = "base-spire:MapNode";
    private const string NodesCollection = "base-spire:MapNodes";
    private const string ChildrenRelation = "base-spire:MapChildren";
    private const string ChooseAction = "base-spire:ChooseMapPoint";
    private const string MapStream = "base-spire:run:act_1_map";
    private const string MonsterTypeId = "base-spire:Monster";
    private const string MonstersCollection = "base-spire:Monsters";
    private const string QueueCollection = "base-spire:EncounterQueue";
    private const string QueueEntryType = "base-spire:EncounterQueueEntry";
    private const string EliteQueueCollection = "base-spire:EliteEncounterQueue";
    private const string EliteQueueEntryType = "base-spire:EliteEncounterQueueEntry";
    private const string StrikeId = "base-spire:strike-ironclad";
    private const string DevSeedA = "t05-map-alpha";
    private const string DevSeedB = "t05-map-beta";

    // CORE-AUTHOR-04 engine/ir@0.4; evidence: .audit/dsh-core-author-04-2026-10-11/
    // integration-build-description.json, public-host-descriptions.json and act1-public/summary.json.
    // ENV-T08（2026-10-11）合法迁移：包新增 `EliteEncounterQueue`/`EliteEncounterQueueEntry` 与两个
    // per-type 访问计数字段（普通/精英独立库存与独立消费者），因此 dev 规则身份与公开构建身份**必须**变化。
    // 迁移前：ruleset=75a9d9345def3426 build=2f0fd07325835101；迁移后见下方常量（旧值留档供负责人核对）。
    // ENV-T09.2（2026-10-11）合法迁移：dev-map 包新增商店房间（`ShopOffer`/`ShopOffers`、
    // `Run.ShopCardRemovalsUsed`、`MapPhase` 上界抬到 `ShopPhase`=7、5 个商店动作与 9 个纯计算资产、
    // 商店的 `ChooseMapPoint` 分支），因此 dev-map 身份**必须**变化；`dev` 范围没有商店，身份不变。
    // 迁移前：dev-map ruleset=c46704e1392e253e build=fd9e605af013e515（当前见下方常量）。
    // ENV-T06-source-stable-shuffle-20261012（2026-10-12）合法迁移：卡实例新增**隐藏** `SourceOrderKey`
    // （源 `ModelId.Entry@升级等级`，重洗预排序的键），且 `DrawCards` 的重洗增加了源 `StableShuffle` 预排序
    // （新纯计算资产 `base-spire:SourceStableShuffleOrder` + 逐张移动指令），因此 dev 与 dev-map 的
    // 规则身份与公开构建身份**都**变化。
    // 迁移前：dev ruleset=3224ed9fcee52136 build=35a160593789e880；
    //         dev-map ruleset=c46704e1392e253e build=fd9e605af013e515。
    // 迁移后的实际值来自本包 `--describe-build` 实测：
    // .audit/dsh-source-stable-shuffle-20261012/describe-build.txt（旧值留档供负责人核对）。
    //
    // ENV-T09-unknown-room-modules-20261012（2026-10-12）合法迁移：dev-map 包新增**问号房与事件**
    // （`RoomType` 枚举与 `Run.ActualRoomType`、`Run.CurrentEventId`/`EventsVisited`/`EventPageGold`、
    // `RunVisitedEvents` 集合、`MapPhase` 上界抬到 `EventPhase`=8、两条纯计算资产
    // `base-spire:UnknownMapPointOddsRoll`/`base-spire:SourceEventNextInt`、两个事件公开动作与
    // `ChooseMapPoint` 的 Unknown 分支），因此 dev-map 身份**必须**变化；`dev` 范围没有地图/事件，
    // 身份保持不变（2dab0c5b3c3a4034 / 4a08d5bbaf4270be）。
    // 迁移前：dev-map ruleset=6c1e1be04af1492b build=9d9f43c0d1b58809。
    // 迁移后（本包实测，见 .artifacts/parallel/dsh-unknown-room-modules-20261012/describe-build.txt）：
    // 2026-10-12 shared operations/card primitives: generic selection continuations and Feed composition
    // change card IR in both scopes; resource/deck/relic commands additionally change dev-map IR.
    // Previous dev 2dab0c5b3c3a4034 / 4a08d5bbaf4270be; current strict17 remains 16 dual + 1 self.
    // Actual descriptors and source/contract witnesses: .audit/owner-shared-run-20261012/.
    // Byrdonis package: Territorial registration/owner-end grant, normal HP correction and
    // selected MachineState persistence change both scopes. Actual descriptors are recorded
    // in .audit/dsh-byrdonis-elite-20261013/evidence/describe-build.jsonl.
    // 2026-10-15 checkout baseline: canonical LF resource bytes change ContentDigest/PublicBuildId;
    // both RulesetId and SemanticsDigest remain equal to the Byrdonis baseline. This is a source-byte
    // identity migration, not a rule change. Evidence: docs/review/records/2026-10-15/workspace-baseline-review-2026-10-15.md.
    // Previous public builds: dev=859e3c9a561bd7ff; dev-map=e8a6ccf78f186dda.
    // 2026-10-15: enemy block multipliers require a held power; expiring duration powers are removed.
    // Normal first-combat differential witnesses: .audit/env-t04-combat-continuation/normal-4.
    // Unique-group formation selection now preserves its first hit (normal-two-combats witness).
    // AttackPotion offer generation and consumption-before-effect decision; independent selection/cancel
    // trajectories and source shuffle witnesses: .audit/env-t06-offer/.
    // Shrink declares negative infinite duration; damage/tick IR now preserves its -1 sentinel.
    // Third normal combat after AromaOfChaos independently compared: .audit/post-event-20261016/.
    // Previous dev b6472f08ed1c4f87 / 4b14a623a7f07d57; dev-map 31802bab1575fbaf / 3f2336d912772e4f.
    // 2026-10-16 capacity: Elite rewards, visited-floor encounter seeds, Inklet HP/starting move,
    // spent hp cap removal and ending/singleton Headbutt selection. Strict/public evidence: .audit/capacity-20261016/.
    // Previous dev 2df4b453b15febbf / b8d3a04a74271921; dev-map 1f864e09b839c59b / 23b8f78062613886.
    // 2026-10-17 normal Run: own-turn enemy block clear, confirmed-death power removal,
    // and SlipperyBridge permanent preview/removal. Source/native prefix: .audit/act1-run-20261016/.
    // Previous dev aad01bb0852d0d7d / 48e4a0d6e545db41; dev-map 59937585291f40b1 / b91254a8b3d86d80.
    // 2026-10-17: card attachment fields/copy policy and post-body Sown trigger; controlled/native/public witnesses.
    // 2026-10-17: 12 Silent poison cards, owner-origin application hooks, Sly discard queue and public counter.
    // Focused evidence: SilentPoisonChecks; previous dev 475abd581a69694c / 161d18afa369a767.
    // 2026-10-17: complete solo Sly family, dynamic keyword lifetimes, isolated discard batches and before-hit Thorns.
    // Previous dev db7ab18e687e380d / feb3f5074952e499; focused witnesses: SilentSlyChecks.
    // 2026-10-17 relic foundation: instance Counter/IsWax/IsMelted schema, shared construction and
    // source-order victory listeners excluding melted relics. Controlled lifecycle/factory checks:
    // .audit/relic-foundation-20261017/. Previous dev 87b1e83c4318a488 / d3484e3e0486354a.
    // 2026-10-17 relic content batch: 69 declared relic effects, FocusPower role, PlatingPower first-turn
    // rule and orb consumers move the content/semantics digests again (same build/check entry:
    // `--relic-content-only`, `--describe-build`). Previous dev 2ee95d1e50b93b0a / 6ccdb4ee30acd725.
    // 2026-10-17 relic card-play batch: four more executable relics (Nunchaku/Kunai/Shuriken/OrnamentalFan)
    // plus the `after-card-played` trigger and card-kind filter. Previous dev 2b67d3cf8a4731f4 / e3f4afd5fb459d40.
    // 2026-10-17 relic exhaust batch: LetterOpener/CharonsAshes, `after-card-exhausted` and unpowered area
    // damage reuse the shared monster damage command. Previous dev daa13b3bc7f1567c / cee2fcf4a4a63f5a.
    // 2026-10-17 MercuryHourglass continues the same area damage at the player-turn-start trigger.
    // Previous dev 28ac6d84bc73f96d / 9b1e393422bebc75.
    // 2026-10-17 FestivePopper/DivineRight reuse first-turn area damage and combat-entry stars.
    // Previous dev ea4d576da58dae95 / 5e89ed3d07e0b338.
    private const string DevGoldenRuleset = "775b4fee84aa7800";
    private const string DevGoldenBuild = "b103e81fede9d804";
    // Event-domain contract: shared choice guards, gold snapshots and noncombat defeat tail change event IR.
    // Previous dev-map ruleset=e19e4e5b63d0dd01 build=110a5b7867698e0c; dev has no event module and is unchanged.
    // Previous dev-map 639ca07b5a486fbd / 6127d4d3959c048c.
    // 2026-10-16: independent source eligibility rejects Act 1-ineligible events without shrinking the pool.
    // Original-seed normal prefix selects AromaOfChaos on both sides; source threshold/unsupported witnesses
    // and actual descriptors: .audit/env-progress-20261016/. Previous: 9e6fdbefe8520a40 / 536dc2bc2b1c3e58.
    // AromaOfChaos adds both source options and persistent selection/transform IR. Both branches
    // independently compared and publicly replayed: .audit/aroma-20261016/. Dev scope remains unchanged.
    // 2026-10-17: TabletOfTruth page/resource IR, uniform noncombat rewards and WoodCarvings eligibility.
    // Native event execution and normal five-page Run comparison: .audit/act1-events-20261017/.
    // 2026-10-17: complete JungleMazeAdventure/MorphicGrove modules, simultaneous deck selection.
    // Isolated real-catalogue checks: .audit/event-layer-20261017/; native Jungle solo RNG gap remains explicit.
    // Sown/SapphireSeed and actual card-copy semantics: .audit/sown-20261017/.
    // Previous dev-map 34b51c466486b0f9 / d96534dd8df06257.
    // Previous dev-map 2958d9e779c64116 / 46130439a68eedb3.
    // Same relic schema/lifecycle migration; previous dev-map 6ed7062e03b9867d / 74ef43415f47d401.
    // Same relic content batch as dev; previous dev-map 466c1e5dbb31bf7d / 8c82420f95a29547.
    // Same relic card-play batch; previous dev-map 84b4dbd80cb769ea / 42889d7374f76501.
    // Same relic exhaust batch; previous dev-map bf88d25972f155d9 / 9a5285f99cadfca6.
    // Same MercuryHourglass addition; previous dev-map 83238c8da492af09 / 70132d1bb1df4034.
    // Same FestivePopper/DivineRight batch; previous dev-map e95e223845ed0846 / e7c9eb9bb55a084b.
    private const string DevMapGoldenRuleset = "443d0d6515615aa3";
    private const string DevMapGoldenBuild = "4e39836a700af26d";

    public static void DefaultIdentity()
    {
        Check.Run("T05 身份：dev 默认规则与公开构建摘要匹配已复核核心编排基线 [真实]", () =>
        {
            var dev = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad(0, BaseSpireScope.Dev));
            Check.Equal(DevGoldenRuleset, dev.Describe().RulesetId, "verified dev ruleset identity");
            Check.Equal(DevGoldenBuild, dev.Describe().PublicBuildId, "verified dev public build identity");
        });
    }

    public static void Run()
    {
        Identity();
        Trajectory();
        Legality();
        UnimplementedRooms();
        EliteRoom();
        SettleThenChoose();
        BossRewardBoundary();
        RestSiteBoundary();
        DrawsAndStream();
    }

    /// <summary>ENV-T08 局部选组入口：只跑精英房消费者检查（不含地图几何/奖励/营火）。</summary>
    public static void EliteRoomBoundary() => EliteRoom();

    /// <summary>简报 §6.1：同工厂、不同范围身份；默认身份由 DefaultIdentity 独立验证。</summary>
    private static void Identity()
    {
        Check.Run("T05 身份：dev-map 与 dev 同工厂但规则和公开构建身份不同 [真实]", () =>
        {
            var dev = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad(0, BaseSpireScope.Dev));
            var map = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad(0, BaseSpireScope.DevMap));

            Check.Equal(BaseSpireScope.DevMap, map.Describe().ScopeId, "dev-map 的范围身份原样进入构建描述");
            Check.Equal(DevMapGoldenRuleset, map.Describe().RulesetId, "verified dev-map ruleset identity");
            Check.Equal(DevMapGoldenBuild, map.Describe().PublicBuildId, "verified dev-map public build identity");
            Check.False(string.Equals(dev.Describe().RulesetId, map.Describe().RulesetId, StringComparison.Ordinal),
                $"dev-map 的规则身份必须不同（{dev.Describe().RulesetId} vs {map.Describe().RulesetId}）");
            Check.False(string.Equals(dev.Describe().PublicBuildId, map.Describe().PublicBuildId, StringComparison.Ordinal),
                $"dev-map 的公开构建身份必须不同（{dev.Describe().PublicBuildId} vs {map.Describe().PublicBuildId}）");

            // 默认范围：包里没有地图声明、没有地图入口、种子流集合不变。
            var devRun = dev.CreateSession(DevSeedA);
            devRun.Start();
            Check.Equal(0, CountIfPresent(devRun.Session, NodesCollection), "dev 范围没有 MapNodes 集合");
            Check.True(Legal(devRun.Session, ChooseAction) is null, "dev 范围没有 ChooseMapPoint（兼容入口不被地图路径替换）");
            Check.Equal(15, devRun.SeededStreams.Count, "dev 的种子流仍是原版 12+3 条（新范围没有偷偷加流）");

            // 地图范围：多一条地图流 + 每个源事件身份一条事件级流（12+3+1+31），且地图流具名登记。
            // ENV-T09-unknown-room-modules-20261012：事件级流按源池**全量**种（不按可执行模块筛），
            // 因此这里断言的是 31 条而不是"已实现事件数"。
            var mapRun = map.CreateSession(DevSeedA);
            mapRun.Start();
            Check.Equal(47, mapRun.SeededStreams.Count, $"dev-map 的种子流是 12+3+1+31（实测 {mapRun.SeededStreams.Count}）");
            Check.Contains(mapRun.SeededStreams, entry => entry.Stream == MapStream,
                $"dev-map 的种子集合里登记了 {MapStream}");
            Check.Equal(RunSourcePoolManifest.Events.Count,
                mapRun.SeededStreams.Count(entry => entry.Type == "Event"),
                "源池里每个事件身份恰好一条事件级流（不缩池）");
            Console.WriteLine($"      T05 身份：dev build={dev.Describe().PublicBuildId} ruleset={dev.Describe().RulesetId} / "
                + $"dev-map build={map.Describe().PublicBuildId} ruleset={map.Describe().RulesetId}");
        });
    }

    /// <summary>简报 §6.2：两个 seed 的物化轨迹逐 cell 等于移植件重放，强制行/配额符合源，且两 seed 几何不同。</summary>
    private static void Trajectory()
    {
        Check.Run("T05 两 seed 轨迹：物化节点逐 cell 等于移植件重放、强制行与配额符合源、两 seed 几何不同 [真实]", () =>
        {
            var geometries = new List<string>();
            foreach (var seed in new[] { DevSeedA, DevSeedB })
            {
                var session = NewRun(seed);
                var run = RunInstance(session);
                var blob = Text(session, run, RunTypeId, "MapDraws");
                var replay = SourceActMapCompute.Replay(blob, SourceActMapCompute.ActRooms);
                Check.True(replay.Done, $"{seed}: 词表足以让源地图生成跑完");
                Check.True(replay.Rest is >= 6 and <= 7, $"{seed}: rest 配额来自 NextGaussianInt(7,1,6,7)（实测 {replay.Rest}）");
                Check.True(replay.Unknown is >= 10 and <= 14, $"{seed}: unknown 配额来自 NextGaussianInt(12,1,10,14)（实测 {replay.Unknown}）");

                var nodes = Nodes(session).OrderBy(node => node.Index).ToList();
                Check.Equal(replay.Cells.Count, nodes.Count,
                    $"{seed}: Count(MapNodes) 等于重放 cell 数（简报的 1 + NodeCount；本包把起点与 BOSS 一起物化）");
                Check.Equal((long)replay.Cells.Count, Int(session, run, RunTypeId, "MapNodeCount"),
                    $"{seed}: Run.MapNodeCount 等于物化节点数");
                for (var i = 0; i < replay.Cells.Count; i++)
                {
                    var cell = replay.Cells[i];
                    Check.Equal((i, cell.Col, cell.Row, cell.Type), (nodes[i].Index, nodes[i].Col, nodes[i].Row, nodes[i].Type),
                        $"{seed}: cell {i} 的 (Index,Col,Row,Type) 与移植件输出逐字一致");
                }

                // 源 StandardActMap.AssignPointTypes:262-280：末行强制营火、GetRowCount()-7 行强制宝箱、第 1 行强制普通战斗。
                var maxRow = nodes.Max(node => node.Row);
                Check.Equal(16, maxRow, $"{seed}: BOSS 格在源 BossMapPoint 行 numberOfRooms + 1 = 16");
                var byRow = nodes.GroupBy(node => node.Row).ToDictionary(group => group.Key, group => group.ToList());
                Check.True(byRow[maxRow].All(node => node.Type == (int)MapPointType.Boss), $"{seed}: 行 {maxRow} 是 BOSS");
                Check.True(byRow[maxRow - 1].All(node => node.Type == (int)MapPointType.RestSite),
                    $"{seed}: 行 {maxRow - 1}（GetRowCount()-1）全营火");
                Check.True(byRow[maxRow - 7].All(node => node.Type == (int)MapPointType.Treasure),
                    $"{seed}: 行 {maxRow - 7}（GetRowCount()-7）全宝箱");
                Check.True(byRow[1].All(node => node.Type == (int)MapPointType.Monster), $"{seed}: 行 1 全普通战斗");
                var start = nodes.Single(node => node.Row == 0);
                Check.Equal(((long)3, (int)MapPointType.Ancient), ((long)start.Col, start.Type), $"{seed}: 起始点 = 列 3 行 0 远古");

                // 配额：Shop/Elite 由 RepairPointType 顶到目标数；Unknown 全量落地；营火 = 末行强制 + 配额净额（摊派受
                // IsValidPointType 限制，可能少摊一个 ⇒ 只断言源能保证的区间）。
                Check.Equal(3, CountOf(nodes, MapPointType.Shop), $"{seed}: 商店 = NumOfShops = 3");
                Check.Equal(5, CountOf(nodes, MapPointType.Elite), $"{seed}: 精英 = NumOfElites = 5");
                Check.Equal(replay.Unknown, CountOf(nodes, MapPointType.Unknown), $"{seed}: 未知 = unknown 配额");
                Check.Equal(1, CountOf(nodes, MapPointType.Boss), $"{seed}: BOSS 一个");
                Check.Equal(1, CountOf(nodes, MapPointType.Ancient), $"{seed}: 远古（起点）一个");
                var rests = CountOf(nodes, MapPointType.RestSite);
                Check.True(rests >= replay.Rest && rests <= replay.Rest + byRow[maxRow - 1].Count,
                    $"{seed}: 营火 = 末行 {byRow[maxRow - 1].Count} 个强制 + rest 配额 {replay.Rest} 的净额（实测 {rests}）");
                Check.Equal(CountEdges(session), replay.Edges.Count, $"{seed}: MapChildren 边数等于移植件边数");

                geometries.Add($"{nodes.Count} 节点/{CountOf(nodes, MapPointType.Monster)} 战斗/{CountOf(nodes, MapPointType.Unknown)} 未知");
                var row1 = string.Join("|", byRow[1].OrderBy(n => n.Col).Select(n => n.Col));
                var histogram = string.Join(",", nodes.GroupBy(n => (MapPointType)n.Type).OrderBy(g => g.Key)
                    .Select(g => $"{g.Key}:{g.Count()}"));
                Console.WriteLine($"      T05 {seed}：nodes={nodes.Count} rest={replay.Rest} unknown={replay.Unknown} "
                    + $"words={replay.WordsUsed} draws={session.Random.DrawCount(MapStream)} 起始点后继={byRow[1].Count} "
                    + $"第一行列=({row1}) 直方图=({histogram})");

                // 简报 §7 要的"首个合法选择进入的房型/遭遇 id"：物化断言都做完了，再真的进去一次。
                var first = Legal(session, ChooseAction)!.Candidates[0];
                var chosen = nodes.Single(node => node.Id == first);
                Submit(session, Legal(session, ChooseAction)!, first);
                Console.WriteLine($"      T05 {seed} 首个合法选择：行 {chosen.Row} 列 {chosen.Col} 房型 {chosen.Type}"
                    + $"（{(MapPointType)chosen.Type}）遭遇={Text(session, RunInstance(session), RunTypeId, "EncounterId")}");
            }

            Check.False(geometries[0] == geometries[1],
                $"两 seed 的几何/配额至少一处不同（{geometries[0]} vs {geometries[1]}）");
        });
    }

    /// <summary>简报 §6.3：候选面、进入真实战斗、非相邻节点与错误阶段都被拒且状态逐字段不变。</summary>
    private static void Legality()
    {
        Check.Run("T05 合法性：起始点候选恰为第 1 行节点；非相邻节点与战斗中选择都被拒、状态逐字段不变 [真实]", () =>
        {
            var session = NewRun(DevSeedA);
            var run = RunInstance(session);
            var nodes = Nodes(session);
            var choose = Legal(session, ChooseAction);
            Check.True(choose is not null, "开局 ChooseMapPoint 是合法动作");
            Check.Sequence(nodes.Where(node => node.Row == 1).OrderBy(node => node.Col).Select(node => node.Id),
                choose!.Candidates, "起始点候选恰为第 1 行节点（= MapChildren(起始点)）");
            var startNode = nodes.Single(node => node.Row == 0);
            Check.Equal(startNode.Id, Ref(session, run, RunTypeId, "CurrentNode"), "CurrentNode 初始落在起始点实体上");
            Check.Equal(0L, Int(session, run, RunTypeId, "MapPhase"), "初始阶段 = 0（MapChoosing）");

            // (a) 非相邻节点：不是 CurrentNode 的子节点 ⇒ 连候选域都进不去。
            var far = nodes.First(node => node.Row == 3).Id;
            var beforeFar = session.State.StateHash();
            var notCandidate = Check.Fault(EngineFaultCode.IllegalAction,
                () => session.Submit(choose.Definition, SliceManifest.Player, [Value.OfRef(far)]),
                "非相邻节点不能被选");
            Check.True(notCandidate.Message.Contains("is not a current candidate", StringComparison.Ordinal),
                $"拒绝原因是候选域（{notCandidate.Message}）");
            Check.Equal(beforeFar, session.State.StateHash(), "被拒之后状态逐字段不变");

            // (b) 合法节点 ⇒ 进入真实战斗。
            var target = choose.Candidates[0];
            var targetFact = nodes.Single(node => node.Id == target);
            Check.Equal((int)MapPointType.Monster, targetFact.Type, "第 1 行节点都是普通战斗房");
            Submit(session, choose, target);
            var inRoom = RunInstance(session);
            Check.Equal(1L, Int(session, inRoom, RunTypeId, "MapPhase"), "选中普通战斗房后 MapPhase = 1（InRoom）");
            Check.Equal(target, Ref(session, inRoom, RunTypeId, "CurrentNode"), "CurrentNode 落在选中的节点");
            Check.True(Flag(session, target, NodeTypeId, "Visited"), "选中节点的 Visited = true");
            Check.Equal((long)(targetFact.Row + 1), Int(session, inRoom, RunTypeId, "MapFloor"), "MapFloor = Row + 1（照简报 §3）");
            var monsters = Monsters(session);
            Check.True(monsters.Count > 0, "进入了真正的战斗（Monsters 非空）");
            var encounter = Text(session, inRoom, RunTypeId, "EncounterId");
            Check.True(QueueIds(session).Contains(encounter, StringComparer.Ordinal),
                $"本场遭遇 '{encounter}' 来自 EncounterQueue（队列 [{string.Join(",", QueueIds(session))}]）");

            // (c) 错误阶段：战斗中 MapFloor 也已推进，守卫应当拒绝；传入的还是一个**真候选**（当前节点的子节点），
            //     因此这条拒绝只可能来自 guard，不会和候选域检查混淆。
            var child = session.State.RelationTargets(Relation(session, ChildrenRelation), target).First();
            Check.True(Legal(session, ChooseAction) is null, "战斗中 ChooseMapPoint 不在合法动作里");
            var beforeGuard = session.State.StateHash();
            var guard = Check.Fault(EngineFaultCode.IllegalAction,
                () => Submit(session, choose, child),
                "战斗中选点被守卫拒绝");
            Check.True(guard.Message.Contains("guard of action", StringComparison.Ordinal),
                $"拒绝原因是守卫（{guard.Message}）");
            Check.Equal(beforeGuard, session.State.StateHash(), "被拒之后状态逐字段不变（牌堆/血量/Visited/阶段都没动）");
        });
    }

    /// <summary>简报 §6.4：未实现房型显式具名失败，节点保留、Visited 仍 false、状态不变。
    /// ENV-T08 起**精英房已接入**（独立精英队列的消费者），因此这里只剩商店。</summary>
    private static void UnimplementedRooms()
    {
        Check.Run("T05 未实现房型：商店已实现，不再是具名 Fail 的房型 [真实]", () =>
        {
            // ENV-T09.2 起商店由 `MapRoomWiring` 的 Shop 分支 + `ShopRoomWiring` 处理，
            // 因此它不再是"具名 Fail 的房型"；其余未实现房型的具名拒绝语义由 `UnimplementedTypes`
            // 与地图生成契约（节点保留、字段零改动）承担，完整入口检查见 ShopFlowChecks。
            var labels = new Dictionary<MapPointType, string>();
            var covered = new List<MapPointType>();
            foreach (var roomType in labels.Keys)
            {
                // 每次换一个**新会话**：一次具名 Fail 会把会话置为 Faulted（core Scheduler.cs:488），
                // 之后 LegalActions 恒为空 —— 那是引擎的既有语义，不是本接线引入的。
                var session = NewRun(DevSeedA);
                var nodes = Nodes(session);
                var byIndex = nodes.ToDictionary(node => node.Index);
                var run = RunInstance(session);
                var edges = EdgeChildren(SourceActMapCompute.Replay(
                    Text(session, run, RunTypeId, "MapDraws"), SourceActMapCompute.ActRooms));
                var roomCount = Int(session, run, RunTypeId, "RoomCount");

                NodeFact? parent = null;
                NodeFact? child = null;
                for (var row = 1; row < 14 && parent is null; row++)
                {
                    foreach (var candidate in nodes.Where(node => node.Row == row))
                    {
                        if (!edges.TryGetValue(candidate.Index, out var children))
                        {
                            continue;
                        }

                        var found = children.Select(index => byIndex[index]).FirstOrDefault(node => node.Type == (int)roomType);
                        if (found.Id.IsNone)
                        {
                            continue;
                        }

                        parent = candidate;
                        child = found;
                        break;
                    }
                }

                var label = labels[roomType];
                Check.True(parent is not null, $"seed {DevSeedA} 的源地图里有 {label} 节点及其前驱");
                var owner = parent!.Value;
                var subject = child!.Value;

                // 把状态安排成"刚在某节点结算完、还没选下一个"：用真实字段（不是绕过 Action 的旁路）。
                // 走不到这么深只是因为没有实现房型/奖励，字段语义与走到的完全一样。
                Arrange(session, owner, roomCount);
                var choose = Legal(session, ChooseAction);
                Check.True(choose is not null, $"{label}：行 {owner.Row} 结算后 ChooseMapPoint 仍合法");
                Check.Contains(choose!.Candidates, id => id == subject.Id, $"{label}节点是合法候选（不被静默过滤）");
                var before = session.State.StateHash();
                var fault = Check.Fault(EngineFaultCode.SessionStateInvalid,
                    () => Submit(session, choose, subject.Id),
                    $"{label}节点应当具名失败");
                Check.True(fault.Message.Contains("尚未实现", StringComparison.Ordinal),
                    $"{label}节点失败消息具名（{fault.Message}）");
                Check.True(fault.Message.Contains(label, StringComparison.Ordinal),
                    $"失败消息里写着房型名（{fault.Message}）");
                Check.Equal(before, session.State.StateHash(), $"{label}被拒之后状态逐字段不变");
                Check.False(Flag(session, subject.Id, NodeTypeId, "Visited"), $"{label}被拒的节点 Visited 仍 false");
                Check.Equal(owner.Id, Ref(session, RunInstance(session), RunTypeId, "CurrentNode"),
                    $"CurrentNode 没有被 {label} 改动");
                Check.Equal(nodes.Count, Nodes(session).Count, $"被拒之后所有节点都还在 MapNodes 里（{label}）");
                covered.Add(roomType);
                Console.WriteLine($"      T05 未实现房型：{label}（行 {subject.Row}）被具名拒绝：{fault.Message}");
            }

            Check.Equal(labels.Count, covered.Count,
                $"未实现房型都被覆盖（覆盖 {covered.Count}：{string.Join(",", covered.Select(type => labels[type]))}）");
        });
    }

    /// <summary>ENV-T08：精英房是**独立库存的消费者**——遭遇必须来自 `EliteEncounterQueue`（普通队列里
    /// 找不到它），只推进精英访问计数、不动普通计数；清场后与普通战斗房走同一条奖励/结算出口。
    /// 精英槽曾被追加进普通队列，因此这条检查在混放状态下必然失败。</summary>
    private static void EliteRoom()
    {
        Check.Run("ENV-T08 精英房消费者：精英节点按访问计数消费**精英队列**（内容未支持时具名失败）[真实]", () =>
        {
            // 找到"某个节点是精英房"及其前驱（真实地图 + 移植件边表）。
            var probe = NewRun(DevSeedA);
            var probeNodes = Nodes(probe);
            var probeRun = RunInstance(probe);
            var probeByIndex = probeNodes.ToDictionary(node => node.Index);
            var probeEdges = EdgeChildren(SourceActMapCompute.Replay(
                Text(probe, probeRun, RunTypeId, "MapDraws"), SourceActMapCompute.ActRooms));
            var slots = QueueSlots(probe, EliteQueueCollection, EliteQueueEntryType);
            var loadedEnemies = SinglePlayerContentSource.Shared.Model.Enemies
                .Select(enemy => enemy.Id).ToHashSet(StringComparer.Ordinal);
            var elitePool = RunSourcePoolManifest.Complete(SinglePlayerContentSource.Shared.EncounterPool).Elites;

            bool Executable(string encounterId)
            {
                var encounter = elitePool.FirstOrDefault(entry => entry.EncounterId == encounterId);
                return encounter is not null && encounter.AllEnemyIds.Count > 0
                    && encounter.AllEnemyIds.All(enemyId => loadedEnemies.Contains(enemyId));
            }

            var playable = slots.FirstOrDefault(slot => Executable(slot.EncounterId));
            foreach (var (item, reason) in SinglePlayerContentSource.Shared.Unsupported.Where(entry =>
                         entry.Item.Contains("Bygone", StringComparison.Ordinal)
                         || entry.Item.Contains("Byrdonis", StringComparison.Ordinal)
                         || entry.Item.Contains("Phrog", StringComparison.Ordinal)))
            {
                Console.WriteLine($"      ENV-T08 精英内容未支持：{item} => {reason}");
            }

            NodeFact? parent = null;
            NodeFact? elite = null;
            for (var row = 1; row < 14 && parent is null; row++)
            {
                foreach (var candidate in probeNodes.Where(node => node.Row == row))
                {
                    if (!probeEdges.TryGetValue(candidate.Index, out var children))
                    {
                        continue;
                    }

                    var found = children.Select(index => probeByIndex[index])
                        .FirstOrDefault(node => node.Type == (int)MapPointType.Elite);
                    if (found.Id.IsNone)
                    {
                        continue;
                    }

                    parent = candidate;
                    elite = found;
                    break;
                }
            }

            Check.True(parent is not null, $"seed {DevSeedA} 的源地图里有精英节点及其前驱");
            var owner = parent!.Value;
            var subject = elite!.Value;

            // ---- 消费者：精英节点按 `EliteEncountersVisited` 取**精英队列**该槽的遭遇 ----
            var session = NewRun(DevSeedA);
            var run = RunInstance(session);
            Arrange(session, owner, Int(session, run, RunTypeId, "RoomCount"));
            var choose = Legal(session, ChooseAction);
            Check.True(choose is not null, $"精英房（行 {owner.Row}）前驱结算后 ChooseMapPoint 合法");
            Check.Contains(choose!.Candidates, id => id == subject.Id, "精英节点是合法候选（不被静默过滤）");
            Check.DoesNotContain(QueueIds(session), id => id == slots[0].EncounterId,
                $"精英队列 0 号槽的遭遇 '{slots[0].EncounterId}' 不得出现在普通队列里");

            if (!string.IsNullOrEmpty(playable.EncounterId))
            {
                // 敌人行为已加载：走完真实精英战（独立消费计数 + 清场 + 奖励出口）。
                FormalSliceFixture.SetNumber(session, run, RunTypeId, "EliteEncountersVisited", playable.Index);
                var draws = session.Random.DrawCount(VanillaRandomLayout.EngineStreamName("UpFront"));
                Submit(session, choose, subject.Id);
                Check.Equal(1L, Int(session, run, RunTypeId, "MapPhase"), "选中精英节点后 MapPhase = 1（InRoom）");
                Check.Equal(subject.Id, Ref(session, run, RunTypeId, "CurrentNode"), "CurrentNode 落在精英节点");
                Check.True(Flag(session, subject.Id, NodeTypeId, "Visited"), "精英节点 Visited = true");
                Check.Equal(draws, session.Random.DrawCount(VanillaRandomLayout.EngineStreamName("UpFront")),
                    "精英入口不重抽遭遇队列；出生HP和洗牌仍按自己的流抽取");

                var encounter = Text(session, run, RunTypeId, "EncounterId");
                Check.Equal(playable.EncounterId, encounter,
                    $"本场遭遇 = 精英队列第 {playable.Index} 槽（源 `eliteEncounters[visited]`）");
                Check.Equal(playable.Index + 1L, Int(session, run, RunTypeId, "EliteEncountersVisited"),
                    "精英访问计数按源 `MarkVisited(Elite)` 推进一格");
                Check.Equal(0L, Int(session, run, RunTypeId, "NormalEncountersVisited"),
                    "精英房不得推进普通访问计数（两个消费者互相独立）");
                Check.True(Monsters(session).Count > 0, "精英房真的出生了敌人");

                // 受控清场验证精英消费者与完整奖励出口；正常原seed证据另在容量suite。
                var player = FormalSliceFixture.Player(session);
                FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Hp", 60);
                foreach (var monster in Monsters(session))
                {
                    FormalSliceFixture.SetNumber(session, monster, MonsterTypeId, "Hp", 1);
                }

                var strikes = 0;
                while (Monsters(session).Count > 0 && strikes++ < 8)
                {
                    FormalSliceFixture.PlayAt(session,
                        FormalSliceFixture.MoveToHand(session, StrikeId), Monsters(session)[0]);
                }

                Check.Equal(0, Monsters(session).Count, "精英敌人已清空");
                Check.True(Flag(session, run, RunTypeId, "RewardRelicPresent"), "精英必须生成真实遗物奖励");
                Check.True(Int(session, run, RunTypeId, "RewardGold") is >= 35 and <= 45, "精英金币范围");
                ResolveFormalReward(session);
                Check.Equal(2L, Int(session, run, RunTypeId, "MapPhase"), "精英房结算后回到选图相位");
                Check.True(Legal(session, ChooseAction) is not null, "精英房结算后可以继续旅行");
                Console.WriteLine($"      ENV-T08 精英房：行 {subject.Row} 精英槽 {playable.Index}→{encounter} 打完并结算；"
                    + $"精英队列 {slots.Count} 槽，普通队列 {QueueIds(session).Count} 槽");
                return;
            }

            // 没有任何可执行精英时保留具名失败判别；Byrdonis 已由 Territorial 的拥有者
            // 回合末 grantPower 接线支持。Slow/Infested 的其他机制仍不属于本包。
            // 消费者不能退回普通队列或生成空房间。
            Check.Equal(0, slots[0].Index, "第一个精英访问取精英队列 0 号槽（源 `eliteEncounters[eliteEncountersVisited]`）");
            var fault = Check.Fault(EngineFaultCode.SessionStateInvalid,
                () => Submit(session, choose, subject.Id),
                $"敌人行为未加载的精英遭遇 '{slots[0].EncounterId}' 必须显式失败");
            Check.True(fault.Message.Contains(slots[0].EncounterId, StringComparison.Ordinal),
                $"失败消息里的遭遇 id 来自精英队列（{fault.Message}）");
            Check.True(fault.Message.Contains("cannot execute", StringComparison.Ordinal),
                $"失败消息具名说明遭遇无法执行（{fault.Message}）");
            Console.WriteLine($"      ENV-T08 精英房：行 {subject.Row} 精英槽 0→{slots[0].EncounterId} 被消费后具名失败"
                + $"（敌人行为未加载）；精英队列 {slots.Count} 槽，普通队列 {QueueIds(session).Count} 槽");
        });
    }

    /// <summary>简报 §6.5：胜利结算后 ChooseMapPoint 重新合法，候选 = 新当前节点的子节点（与移植件边表双向核对）。</summary>
    private static void SettleThenChoose()
    {
        Check.Run("T05 结算出口：清房与奖励动作后 MapPhase=2、候选与当前节点子节点一致 [受控HP，非通关轨迹]", () =>
        {
            var session = NewRun(DevSeedA);
            var choose = Legal(session, ChooseAction);
            var target = choose!.Candidates[0];
            Submit(session, choose, target);

            // 清场：把敌人压到 1 血，用起始牌组的打击牌逐张击杀（既有夹具的清场流程）。
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Hp", 30);
            foreach (var monster in Monsters(session))
            {
                FormalSliceFixture.SetNumber(session, monster, MonsterTypeId, "Hp", 1);
            }

            var strikes = 0;
            while (Monsters(session).Count > 0 && strikes++ < 8)
            {
                FormalSliceFixture.PlayAt(session,
                    FormalSliceFixture.MoveToHand(session, StrikeId), Monsters(session)[0]);
            }

            Check.Equal(0, Monsters(session).Count, "本场敌人已清空");
            ResolveFormalReward(session);
            var run = RunInstance(session);
            Check.Equal(2L, Int(session, run, RunTypeId, "MapPhase"),
                "正式奖励动作完成后 MapPhase = 2（奖励子包的结算出口）");

            var choose2 = Legal(session, ChooseAction);
            Check.True(choose2 is not null, "结算边界之后 ChooseMapPoint 重新合法（有稳定完成出口）");
            Check.Equal(target, Ref(session, run, RunTypeId, "CurrentNode"), "当前节点仍是刚打完的那间房");

            var replay = SourceActMapCompute.Replay(Text(session, run, RunTypeId, "MapDraws"), SourceActMapCompute.ActRooms);
            var byIndex = Nodes(session).ToDictionary(node => node.Index);
            var currentNode = byIndex.Values.Single(node => node.Id == target);
            var expected = EdgeChildren(replay).TryGetValue(currentNode.Index, out var children)
                ? children.OrderBy(index => index).ToList()
                : [];
            var viaRelation = session.State.RelationTargets(Relation(session, ChildrenRelation), target)
                .Select(id => byIndex.Values.Single(node => node.Id == id).Index).OrderBy(index => index).ToList();
            var viaCandidates = choose2!.Candidates
                .Select(id => byIndex.Values.Single(node => node.Id == id).Index).OrderBy(index => index).ToList();

            Check.True(expected.Count > 0, $"当前节点（cell {currentNode.Index}）在源边表里必须有后继");
            Check.Sequence(expected, viaRelation, "MapChildren 关系 = 移植件边表（正向）");
            Check.Sequence(viaRelation, expected, "移植件边表 = MapChildren 关系（反向，双向核对）");
            Check.Sequence(expected, viaCandidates, "合法候选 = 当前节点的子节点");
            Console.WriteLine($"      T05 结算出口：cell {currentNode.Index} → 后继 [{string.Join(",", expected)}] "
                + $"遭遇={Text(session, run, RunTypeId, "EncounterId")} 房间={Int(session, run, RunTypeId, "CurrentRoom")}");
        });
    }

    public static void BossRewardBoundary()
    {
        foreach (var (boss, potionDropped) in new[] { (true, true), (true, false), (false, true) })
        {
            Check.Run($"T05 奖励终局边界：boss={boss} potion={potionDropped} [受控奖励初态，非通关轨迹]", () =>
            {
                var session = NewRun(DevSeedA);
                var run = RunInstance(session);
                var node = Nodes(session).First(item => item.Type == (int)(boss
                    ? MapPointType.Boss : MapPointType.Monster));
                var runType = session.RuleSet.TypeByQualifiedId(RunTypeId);
                session.State.RunAtomic(() =>
                {
                    session.State.SetField(run, runType.FieldIndex("CurrentNode"), Value.OfRef(node.Id));
                    session.State.SetField(run, runType.FieldIndex("CurrentRoom"), Value.Of(1L));
                    session.State.SetField(run, runType.FieldIndex("MapPhase"), Value.Of((long)RewardWiring.RewardPhase));
                    session.State.SetField(run, runType.FieldIndex("RewardTaken"), Value.Of(0L));
                    session.State.SetField(run, runType.FieldIndex(RewardWiring.GoldTakenFieldName), Value.Of(0L));
                    session.State.SetField(run, runType.FieldIndex(RewardWiring.PotionDroppedFieldName), Value.Of(potionDropped));
                    session.State.SetField(run, runType.FieldIndex(RewardWiring.PotionResolvedFieldName), Value.Of(0L));
                    session.State.SetField(run, runType.FieldIndex("RewardRelicPresent"), Value.Of(false));
                    session.State.SetField(run, runType.FieldIndex("RewardRelicResolved"), Value.Of(true));
                });

                Submit(session, Legal(session, RewardWiring.TakeGoldActionName)!);
                Check.True(session.Terminal is null, "金币动作不能绕过未处理的卡牌奖励");
                Check.Equal((long)RewardWiring.RewardPhase, Int(session, run, RunTypeId, "MapPhase"), "卡牌尚未处理仍在奖励相位");
                Submit(session, Legal(session, RewardWiring.SkipCardActionName)!);
                if (potionDropped)
                {
                    Check.True(session.Terminal is null, "卡牌动作不能绕过未处理的药水奖励");
                    Check.Equal((long)RewardWiring.RewardPhase, Int(session, run, RunTypeId, "MapPhase"), "药水尚未处理仍在奖励相位");
                    Submit(session, Legal(session, RewardWiring.SkipPotionActionName)!);
                }

                if (boss)
                {
                    Check.Equal(SessionStatus.Running, session.Status, "幕完成保留整局会话");
                    Check.True(session.Terminal is null, "第一幕不提交整局Victory");
                    Check.True(Flag(session, run, RunTypeId, "ActCompleted"), "奖励全部关闭后才记录幕完成");
                    Check.Equal((long)MapRoomWiring.ActCompletedPhase, Int(session, run, RunTypeId, "MapPhase"), "幕完成独立相位");
                    Check.Equal("base-spire:ContinueToNextAct", session.LegalActions(SliceManifest.Player).Single().Definition.QualifiedId,
                        "幕完成只暴露下一幕入口，未实现时具名失败");
                }
                else
                {
                    Check.True(session.Terminal is null, "普通节点不能提交 Victory");
                    Check.Equal(2L, Int(session, run, RunTypeId, "MapPhase"), "普通节点回到选图相位");
                }
            });
        }
    }

    public static void RestSiteBoundary()
    {
        Session EnterRest()
        {
            var session = NewRun(DevSeedA);
            var nodes = Nodes(session);
            var rest = nodes.First(node => node.Type == (int)MapPointType.RestSite);
            var parent = nodes.First(node => session.State.RelationTargets(Relation(session, ChildrenRelation), node.Id).Contains(rest.Id));
            Arrange(session, parent, Int(session, RunInstance(session), RunTypeId, "RoomCount"));
            var run = RunInstance(session);
            var room = Int(session, run, RunTypeId, "CurrentRoom");
            var draws = session.Random.TotalDraws;
            Submit(session, Legal(session, ChooseAction)!, rest.Id);
            Check.Equal((long)MapRoomWiring.RestSitePhase, Int(session, run, RunTypeId, "MapPhase"), "进入真实营火节点");
            Check.Equal(room, Int(session, run, RunTypeId, "CurrentRoom"), "营火不消耗战斗队列");
            Check.Equal(draws, session.Random.TotalDraws, "营火进入不抽随机");
            Check.True(Legal(session, ChooseAction) is null, "成功选择营火选项前不可旅行");
            Check.True(Legal(session, RewardWiring.TakeGoldActionName) is null, "营火不产生战斗奖励");
            return session;
        }
        Check.Run("T05 Rest：30%截断、上限、满血与重复拒绝 [受控游标/HP，非通关轨迹]", () =>
        {
            foreach (var (hp, expected) in new[] { (10L, 34L), (70L, 81L), (81L, 81L) })
            {
                var session = EnterRest();
                var run = RunInstance(session);
                FormalSliceFixture.SetNumber(session, run, RunTypeId, "MaxHp", 81);
                FormalSliceFixture.SetNumber(session, run, RunTypeId, "Hp", hp);
                var rest = Legal(session, MapRoomWiring.RestActionName)!;
                var draws = session.Random.TotalDraws;
                Submit(session, rest);
                Check.Equal(expected, Int(session, run, RunTypeId, "Hp"), "Rest实际截断且封顶");
                Check.Equal(2L, Int(session, run, RunTypeId, "MapPhase"), "Rest成功才开放旅行");
                Check.Equal(draws, session.Random.TotalDraws, "Rest不抽随机");
                var before = session.State.StateHash();
                Check.Fault(EngineFaultCode.IllegalAction, () => Submit(session, rest), "不能重复治疗");
                Check.Equal(before, session.State.StateHash(), "重复提交零改动");
            }
        });
        Check.Run("T05 Smith：只升级所选实例、保留成长和费用修正 [受控游标/牌面，非通关轨迹]", () =>
        {
            var session = EnterRest();
            var smith = Legal(session, MapRoomWiring.SmithActionName)!;
            var strikes = smith.Candidates.Where(id => FormalSliceFixture.Definition(session, id) == StrikeId).ToArray();
            Check.True(strikes.Length >= 2, "真实初始牌组有多个Strike实例");
            var selected = strikes[0];
            var other = strikes[1];
            FormalSliceFixture.SetNumber(session, selected, FormalSliceFixture.CardTypeId, "Amount", 20);
            FormalSliceFixture.SetNumber(session, selected, FormalSliceFixture.CardTypeId, "CostModifier", -1);
            var draws = session.Random.TotalDraws;
            Submit(session, smith, selected);
            Check.Equal(23L, Int(session, selected, FormalSliceFixture.CardTypeId, "Amount"), "Strike升级只增加3，保留成长");
            Check.Equal(-1L, Int(session, selected, FormalSliceFixture.CardTypeId, "CostModifier"), "费用修正保留");
            Check.True(Flag(session, selected, FormalSliceFixture.CardTypeId, "Upgraded"), "选中实例升级");
            Check.False(Flag(session, other, FormalSliceFixture.CardTypeId, "Upgraded"), "同定义其他实例不升级");
            Check.Equal(draws, session.Random.TotalDraws, "Smith不抽随机");
            Check.Equal(2L, Int(session, RunInstance(session), RunTypeId, "MapPhase"), "Smith消费营火选项");
            Check.True(Legal(session, MapRoomWiring.RestActionName) is null, "Smith后不能再Rest");
        });
    }

    /// <summary>简报 §6.6：地图流消费量等于重放字数、抽数账目闭合、选点不重复生成。</summary>
    private static void DrawsAndStream()
    {
        Check.Run("T05 抽数与流：地图流消费量等于重放字数、账目闭合、选择不重复生成 [真实]", () =>
        {
            var factory = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad(0, BaseSpireScope.DevMap));
            var run = factory.CreateSession(DevSeedA);
            Check.Equal(0L, run.Session.Random.TotalDraws, "建局（工厂）阶段一个随机数都不能消费");
            run.Start();
            var session = run.Session;
            var runInstance = RunInstance(session);
            var blob = Text(session, runInstance, RunTypeId, "MapDraws");
            var words = SourceActMapCompute.ParseWords(blob);
            var replay = SourceActMapCompute.Replay(words, SourceActMapCompute.ActRooms);
            var draws = session.Random.DrawCount(MapStream);
            Check.Equal((long)replay.WordsUsed, draws, "地图流消费字数 = 移植件重放字数（不多抽一个字）");
            Check.Equal((long)words.Count, draws, "词表里的字数 = 地图流消费字数（逐笔记录）");
            // 加载阶段当然不止地图流在抽（首战牌堆洗牌、遭遇队列等）；要证的是**账目闭合**：
            // 地图流之外没有任何游离抽数、地图流一个字都没有溢到别的流上。
            var allStreams = session.Random.Diagnostics()
                .Where(item => item.Draws > 0)
                .OrderByDescending(item => item.Draws)
                .ToList();
            Check.Equal(draws, allStreams.Single(item => item.Name == MapStream).Draws,
                "随机服务诊断里地图流的抽数也是这一笔（流名没有被垫层改写）");
            Check.Equal(allStreams.Sum(item => item.Draws), session.Random.TotalDraws,
                "总抽数 = 所有流的抽数之和（没有游离抽数、没有跨流借用）");
            Check.True(draws > 0 && draws <= SourceActMapCompute.WordBudget, $"消费量在预算内（{draws}）");

            var choose = Legal(session, ChooseAction);
            Submit(session, choose!, choose!.Candidates[0]);
            Check.Equal(draws, session.Random.DrawCount(MapStream), "进入房间不会重新抽地图（不重复生成）");
            Check.Equal(blob, Text(session, RunInstance(session), RunTypeId, "MapDraws"), "MapDraws 词表逐字不变");
            Check.True(Text(session, RunInstance(session), RunTypeId, "MapDraws").StartsWith(';'),
                "词表编码 `;w0;w1;…` 与 IR 侧 Concat 一致");
            Console.WriteLine($"      T05 抽数：地图流 {MapStream} 消费 {draws} 字（加载总抽数 {session.Random.TotalDraws}）；"
                + $"其余流 {string.Join(", ", allStreams.Where(item => item.Name != MapStream).Select(item => $"{item.Name}={item.Draws}"))}");
        });
    }

    // ---- 辅助 -------------------------------------------------------------------------------------

    private readonly record struct NodeFact(InstanceId Id, int Index, int Col, int Row, int Type);

    private static Session NewRun(string seed)
    {
        var run = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad(0, BaseSpireScope.DevMap)).CreateSession(seed);
        run.Start();
        return run.Session;
    }

    private static void Submit(Session session, LegalAction action, params InstanceId[] arguments)
    {
        session.Submit(action.Definition, SliceManifest.Player, [.. arguments.Select(Value.OfRef)]);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            var pending = session.PendingDecision!;
            session.Answer(new DecisionAnswer(pending.RequestId, [pending.Candidates[0]]));
        }
    }

    private static void ResolveFormalReward(Session session)
    {
        var gold = Legal(session, RewardWiring.TakeGoldActionName)
            ?? throw new CheckFailedException("战斗奖励开放后应提供领取金币动作");        Submit(session, gold);

        var card = Legal(session, RewardWiring.SkipCardActionName)
            ?? throw new CheckFailedException("战斗奖励开放后应提供跳过候选卡动作");
        Submit(session, card);

        var potion = Legal(session, RewardWiring.SkipPotionActionName);
        if (potion is not null)
            Submit(session, potion);
        var relic = Legal(session, RewardWiring.SkipRelicActionName);
        if (relic is not null)
        {
            Check.Equal(3L, Int(session, RunInstance(session), RunTypeId, "MapPhase"), "其他奖励解决后仍须处理精英遗物");
            Check.True(Legal(session, ChooseAction) is null, "遗物未解决不能旅行");
            var draws = session.Random.TotalDraws;
            Submit(session, relic);
            Check.Equal(draws, session.Random.TotalDraws, "跳过遗物不补抽");
            Check.True(Legal(session, RewardWiring.SkipRelicActionName) is null, "遗物解决不能重复");
        }
    }

    private static LegalAction? Legal(Session session, string actionId)
    {
        var qualified = actionId.Contains(':', StringComparison.Ordinal)
            ? actionId
            : $"{SliceManifest.PackageId}:{actionId}";
        return session.LegalActions(SliceManifest.Player)
            .FirstOrDefault(candidate => candidate.Definition.QualifiedId == qualified);
    }

    private static InstanceId RunInstance(Session session) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, RunStatesCollection)).Single();

    private static List<NodeFact> Nodes(Session session)
    {
        var type = session.RuleSet.TypeByQualifiedId(NodeTypeId);
        return session.State.CollectionMembers(FormalSliceFixture.Collection(session, NodesCollection))
            .Select(id => new NodeFact(
                id,
                (int)session.State.GetField(id, type.FieldIndex("Index")).AsInt(),
                (int)session.State.GetField(id, type.FieldIndex("Col")).AsInt(),
                (int)session.State.GetField(id, type.FieldIndex("Row")).AsInt(),
                session.State.GetField(id, type.FieldIndex("Type")).AsEnumOrdinal()))
            .ToList();
    }

    private static int CountIfPresent(Session session, string collection) =>
        session.RuleSet.Collections.Any(item => item.QualifiedId == collection)
            ? session.State.CollectionMembers(FormalSliceFixture.Collection(session, collection)).Count
            : 0;

    private static int CountOf(IReadOnlyList<NodeFact> nodes, MapPointType type) =>
        nodes.Count(node => node.Type == (int)type);

    private static int CountEdges(Session session) => Nodes(session)
        .Sum(node => session.State.RelationTargets(Relation(session, ChildrenRelation), node.Id).Count);

    private static IReadOnlyList<InstanceId> Monsters(Session session) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, MonstersCollection));

    private static List<string> QueueIds(Session session) =>
        QueueIds(session, QueueCollection, QueueEntryType);

    private static List<string> QueueIds(Session session, string collectionId, string entryTypeId) =>
        [.. QueueSlots(session, collectionId, entryTypeId).Select(slot => slot.EncounterId)];

    /// <summary>队列槽位（按 `Index` 排序）：`Index` 是隐藏字段，检查侧显式读出来做槽位级断言。</summary>
    private static List<(int Index, string EncounterId)> QueueSlots(Session session, string collectionId,
        string entryTypeId)
    {
        var type = session.RuleSet.TypeByQualifiedId(entryTypeId);
        return
        [
            .. session.State.CollectionMembers(FormalSliceFixture.Collection(session, collectionId))
                .Select(id => (
                    (int)session.State.GetField(id, type.FieldIndex("Index")).AsInt(),
                    session.State.GetField(id, type.FieldIndex("EncounterId")).AsString()))
                .OrderBy(slot => slot.Item1),
        ];
    }

    private static Dictionary<int, List<int>> EdgeChildren(SourceActMapReplay replay)
    {
        var map = new Dictionary<int, List<int>>();
        foreach (var edge in replay.Edges)
        {
            if (!map.TryGetValue(edge.Parent, out var children))
            {
                map[edge.Parent] = children = [];
            }

            children.Add(edge.Child);
        }

        return map;
    }

    /// <summary>把状态安排成"刚在 parent 结算完、还没选下一个"（真实字段语义，不是旁路）。</summary>
    private static void Arrange(Session session, NodeFact parent, long roomCount)
    {
        var run = RunInstance(session);
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "MapPhase", 2);
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "MapFloor", Math.Min(parent.Row + 1, roomCount - 1));
        var index = session.RuleSet.TypeByQualifiedId(RunTypeId).FieldIndex("CurrentNode");
        session.State.RunAtomic(() => session.State.SetField(run, index, Value.OfRef(parent.Id)));
    }

    private static int Relation(Session session, string relation) =>
        session.RuleSet.Relations.Single(item => item.QualifiedId == relation).Id;

    private static long Int(Session session, InstanceId entity, string typeId, string field) =>
        FormalSliceFixture.Number(session, entity, typeId, field);

    private static string Text(Session session, InstanceId entity, string typeId, string field) =>
        session.State.GetField(entity, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsString();

    private static InstanceId Ref(Session session, InstanceId entity, string typeId, string field) =>
        session.State.GetField(entity, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsRef();

    private static bool Flag(Session session, InstanceId entity, string typeId, string field) =>
        session.State.GetField(entity, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsBool();
}
