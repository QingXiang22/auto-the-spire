using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// BASE-02 批 2 / ENV-T08：**整幕遭遇队列**（原版 `ActModel.GenerateRooms` 的 GrabBag + 标签回避）。
///
/// 四层证据：
/// <list type="number">
/// <item><b>普通队列 = 独立复算</b>：测试侧按冻结源码语义重放"弱池 3 槽 + 普通池补齐到 15"，与
///       **实际状态**逐槽比较。复算用的池是生产实际使用的**源全池**（`RunSourcePoolManifest.Complete`
///       覆盖后的身份池），不是按可执行性缩过的子集；</item>
/// <item><b>精英队列是独立库存</b>：源在普通两段之后单起 `grabBag3` 生成 `_rooms.eliteEncounters`
///       （固定 `eliteQueueCount` 槽，Overgrowth = 15）。普通队列里不得出现精英遭遇，精英队列里不得
///       出现普通遭遇，两者成员零交集且各有自己的槽位编号空间；</item>
/// <item><b>源前置消费逐段对账</b>：`up_front` 的总消费 = 遗物袋洗牌 + 事件洗牌 + 普通段 + 精英段 +
///       BOSS + 古代人。前置消费按**观察到的袋大小**独立算（每次 Fisher-Yates 为 n-1 抽）；</item>
/// <item><b>判别性性质</b>：相邻两个遭遇不共享标签且不是它自己；"谓词预扫失败 ⇒ 一次也不抽、
///       再无约束抓一次"（合成池：所有遭遇共享同一个标签）。</item>
/// </list>
/// </summary>
internal static class SliceEncounterQueueChecks
{
    private const string QueueCollection = "base-spire:EncounterQueue";
    private const string QueueEntryType = "base-spire:EncounterQueueEntry";
    private const string EliteQueueCollection = "base-spire:EliteEncounterQueue";
    private const string EliteQueueEntryType = "base-spire:EliteEncounterQueueEntry";
    private const string UpFrontStream = "base-spire:run:up_front";
    private const string RunStatesCollection = "base-spire:RunStates";
    private const string RunTypeId = "base-spire:Run";
    private const ulong FixtureSeed = 20261001UL;
    private const ulong FixtureStreamSeed = 0x0FF_1234UL;
    private const string MilestoneSeed = "ENV-IRONCLAD-00";

    public static void Run()
    {
        Check.Run("[ENV-T08 队列] 普通队列 = 15 槽的独立复算；精英槽必须落在独立集合 [真实]", () =>
        {
            // 生产在 `Build` 里用 `RunSourcePoolManifest.Complete` 把可执行子集覆盖成**源身份全池**，
            // 未支持的遭遇保留真实 ID（只阻塞执行）。复算必须用同一个池，否则比的是两个不同的规则。
            var pool = RunSourcePoolManifest.Complete(SinglePlayerContentSource.Shared.EncounterPool);
            var session = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).CreateSession(MilestoneSeed);
            session.Start();
            var state = session.Session;
            var observed = Queue(state);
            var prefix = PrefixDraws(state);
            var reference = Replay(pool, session.RunSeed,
                session.SeededStreams.Single(entry => entry.Type == "UpFront").Seed, prefix);

            Check.Equal(pool.RoomCount, observed.Count,
                $"普通队列长度 = `GetNumberOfRooms(false)`（实际 {observed.Count}，源池大小 {pool.Encounters.Count}）");
            Check.Sequence(observed.Select(entry => entry.Index), Enumerable.Range(0, pool.RoomCount),
                "普通槽位编号必须是 0..房间数-1（精英槽不得占用 15..29）");
            Check.Sequence(reference.Queue.Select(encounter => encounter.EncounterId),
                observed.Select(entry => entry.EncounterId),
                "逐槽的遭遇必须与独立复算一致（含重抽与装袋顺序）");
            // 分段：前 WeakRoomCount 个槽全部来自弱池，其余来自普通池。
            var weak = pool.WeakEncounters.Select(entry => entry.EncounterId).ToHashSet(StringComparer.Ordinal);
            var regular = pool.RegularEncounters.Select(entry => entry.EncounterId).ToHashSet(StringComparer.Ordinal);
            foreach (var entry in observed)
            {
                var expected = entry.Index < pool.WeakRoomCount ? weak : regular;
                Check.True(expected.Contains(entry.EncounterId),
                    $"槽 {entry.Index} 的遭遇 '{entry.EncounterId}' 必须来自"
                    + $"{(entry.Index < pool.WeakRoomCount ? "弱" : "普通")}池");
            }

            // 首差判别：精英遭遇（`RunSourcePoolManifest.Elites`）**一个都不能**在普通队列里。
            var eliteIds = RunSourcePoolManifest.Elites.Select(entry => entry.EncounterId)
                .ToHashSet(StringComparer.Ordinal);
            Check.DoesNotContain(observed, entry => eliteIds.Contains(entry.EncounterId),
                $"普通队列里不得出现精英遭遇（首差：15 个精英槽曾被追加进同一 EncounterQueue）");

            Check.True(reference.Fallbacks == 0,
                "这组池在整幕里不应出现'预扫失败'（出现说明池形状与预期不同，下面的邻接断言要重新核对）");
            for (var index = 1; index < observed.Count; index++)
            {
                var previous = pool.Encounters.Single(entry =>
                    entry.EncounterId == observed[index - 1].EncounterId);
                var current = pool.Encounters.Single(entry => entry.EncounterId == observed[index].EncounterId);
                Check.False(!string.Equals(previous.EncounterId, current.EncounterId, StringComparison.Ordinal)
                        && previous.Tags.Intersect(current.Tags, StringComparer.Ordinal).Any(),
                    $"槽 {index} 与上一槽不能共享标签（{previous.EncounterId} → {current.EncounterId}）");
            }

            Console.WriteLine($"      ENV-T08 普通队列：{pool.RoomCount} 槽（弱 {pool.WeakRoomCount} + 普通 "
                + $"{pool.RoomCount - pool.WeakRoomCount}），前置消费 {prefix}，普通段抽取 {reference.NormalDraws} 次；"
                + $"槽位=[{string.Join(",", observed.Select(entry => entry.EncounterId.Replace("base-spire:", "")))}]");
        });

        Check.Run("[ENV-T08 队列] 精英队列 = 独立库存（15 槽、仅源精英池成员、与普通队列零交集、逐槽同复算）[真实]", () =>
        {
            var pool = RunSourcePoolManifest.Complete(SinglePlayerContentSource.Shared.EncounterPool);
            var session = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).CreateSession(MilestoneSeed);
            session.Start();
            var state = session.Session;
            var normal = Queue(state);
            var elite = Queue(state, EliteQueueCollection, EliteQueueEntryType);
            var prefix = PrefixDraws(state);
            var streamSeed = session.SeededStreams.Single(entry => entry.Type == "UpFront").Seed;
            var reference = Replay(pool, session.RunSeed, streamSeed, prefix);

            Check.True(pool.EliteRoomCount > 0, $"源幕声明给出精英队列长度（实际 {pool.EliteRoomCount}）");
            Check.Equal(pool.EliteRoomCount, elite.Count,
                $"精英队列长度 = 源固定循环数（实际 {elite.Count}）——不是把精英塞进普通队列");
            Check.Equal(pool.EliteRoomCount, reference.Elite.Count, "独立复算的精英队列长度也必须等于源循环数");
            Check.Sequence(elite.Select(entry => entry.Index), Enumerable.Range(0, pool.EliteRoomCount),
                "精英槽位编号是**自己的** 0..N-1（与普通队列的编号空间互不重叠）");

            // 成员身份：只来自源精英池，且 15 槽一定覆盖全部身份（3 成员袋按池顺序重装 5 次）。
            var eliteIds = pool.Elites.Select(entry => entry.EncounterId).ToHashSet(StringComparer.Ordinal);
            Check.True(elite.All(entry => eliteIds.Contains(entry.EncounterId)),
                $"精英队列成员必须全部来自源精英池 [{string.Join(",", eliteIds)}]"
                + $"（实际 [{string.Join(",", elite.Select(e => e.EncounterId).Distinct())}]）");
            Check.Equal(eliteIds.Count, elite.Select(entry => entry.EncounterId).Distinct(StringComparer.Ordinal).Count(),
                "精英袋每次空了就按池顺序重装 ⇒ 15 槽覆盖池里全部身份");
            Check.Equal(pool.EliteRoomCount / pool.Elites.Count,
                elite.Count(entry => entry.EncounterId == pool.Elites[0].EncounterId),
                $"池有 {pool.Elites.Count} 个身份、队列 {pool.EliteRoomCount} 槽 ⇒ 每个身份恰好出现 "
                + $"{pool.EliteRoomCount / pool.Elites.Count} 次（= 装袋轮数）");

            // 零交集：两个方向的判别都要有。
            var normalIds = normal.Select(entry => entry.EncounterId).ToHashSet(StringComparer.Ordinal);
            Check.DoesNotContain(elite, entry => normalIds.Contains(entry.EncounterId),
                "精英队列里不得出现普通/弱遭遇");
            Check.DoesNotContain(normal, entry => eliteIds.Contains(entry.EncounterId),
                "普通队列里不得出现精英遭遇");

            Check.Sequence(reference.Elite.Select(encounter => encounter.EncounterId),
                elite.Select(entry => entry.EncounterId),
                "逐槽精英遭遇必须与独立复算一致（独立袋子、独立'上一个'、空袋按池顺序重装）");
            Check.Equal(reference.EliteDraws, EliteDraws(state),
                "精英段每次抓取恰好一次抽取（无预扫回退：3 成员袋里总有一个不是'上一个'）");

            Console.WriteLine($"      ENV-T08 精英队列：{elite.Count} 槽、身份 {eliteIds.Count} 个、"
                + $"精英段抽取 {reference.EliteDraws} 次；槽位=[{string.Join(",", elite.Select(entry => entry.EncounterId.Replace("base-spire:", "")))}]");
        });

        Check.Run("[ENV-T08 初始化] up_front 逐段对账（遗物袋/事件/普通/精英/BOSS/古代人）+ 同 seed 重放一致 + 计数独立 [真实]", () =>
        {
            var pool = RunSourcePoolManifest.Complete(SinglePlayerContentSource.Shared.EncounterPool);
            var first = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).CreateSession(MilestoneSeed);
            Check.Equal(0L, first.Session.Random.TotalDraws, "CreateSession（配置阶段）一个随机数都不能消费");
            first.Start();
            var state = first.Session;
            var prefix = PrefixDraws(state);
            var normal = Queue(state);
            var elite = Queue(state, EliteQueueCollection, EliteQueueEntryType);
            var reference = Replay(pool, first.RunSeed,
                first.SeededStreams.Single(entry => entry.Type == "UpFront").Seed, prefix);

            // 逐段对账：源顺序是 遗物袋 → 事件/共享古代人 → 普通两段 → 精英段 → BOSS → 幕古代人。
            var boss = 1;
            var ancient = 1;
            var parts = PrefixBreakdown(state);
            var total = (int)state.Random.DrawCount(UpFrontStream);
            Console.WriteLine($"      ENV-T08 账目：遗物袋洗牌 {parts.Bags}（袋 "
                + $"[{string.Join(",", parts.BagSizes)}]），共享古代人 {parts.Ancients}，事件 "
                + $"{parts.Events}（{parts.EventMembers} 项），前置 {prefix}，普通 {reference.NormalDraws}，精英 "
                + $"{reference.EliteDraws}，BOSS+古代人 {boss + ancient}，实测 total={total}");
            Console.WriteLine("      ENV-T08 流账目：" + string.Join(", ",
                state.Random.Diagnostics().Where(item => item.Draws > 0)
                    .OrderByDescending(item => item.Draws).Select(item => $"{item.Name}={item.Draws}")));
            Check.Equal(prefix + reference.NormalDraws + reference.EliteDraws + boss + ancient,
                total,
                "`up_front` 总消费 = 前置（遗物袋 + 事件洗牌）+ 普通段 + 精英段 + BOSS + 古代人，逐段闭合");
            Check.True(prefix > 0, $"源前置消费必须发生（遗物袋/事件洗牌实际 {prefix} 抽）");
            Check.True(state.Random.TotalDraws >= state.Random.DrawCount(UpFrontStream),
                "up_front 之外还有别的流在抽，但总抽数不得小于 up_front 的抽数");

            // 同 config/seed 重放一致：队列内容与随机账目逐项相同。
            var second = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).CreateSession(MilestoneSeed);
            second.Start();
            var again = second.Session;
            Check.Sequence(normal.Select(entry => entry.EncounterId),
                Queue(again).Select(entry => entry.EncounterId), "同 config/seed 的普通队列逐槽一致");
            Check.Sequence(elite.Select(entry => entry.EncounterId),
                Queue(again, EliteQueueCollection, EliteQueueEntryType).Select(entry => entry.EncounterId),
                "同 config/seed 的精英队列逐槽一致");
            Check.Equal(state.Random.TotalDraws, again.Random.TotalDraws, "同 config/seed 的总抽数一致");

            // 消费者：dev 范围开局就进第一间普通战斗房 ⇒ 普通计数推进一次、精英计数不动，
            // 本间遭遇 = 普通队列第 0 槽（源 `NextNormalEncounter` / `MarkVisited`）。
            var run = RunInstance(state);
            Check.Equal(1L, Number(state, run, RunTypeId, "NormalEncountersVisited"),
                "进入一间普通战斗房后普通访问计数 = 1");
            Check.Equal(0L, Number(state, run, RunTypeId, "EliteEncountersVisited"),
                "普通战斗房不得推进精英访问计数（两个消费者互相独立）");
            Check.Equal(normal[0].EncounterId, Text(state, run, RunTypeId, "EncounterId"),
                "本间遭遇 = 普通队列按**普通访问计数**取到的槽（不是按全局房间号）");

            Console.WriteLine($"      ENV-T08 初始化：前置 {prefix} 抽，普通 {reference.NormalDraws} 抽，精英 "
                + $"{reference.EliteDraws} 抽，BOSS/古代人 2 抽，up_front 合计 "
                + $"{state.Random.DrawCount(UpFrontStream)} 抽（会话总抽数 {state.Random.TotalDraws}）");
        });

        Check.Run("[ENV-T08 初始化] Start 只初始化一次：第二次 Start 具名失败且不重建库存 [真实]", () =>
        {
            var session = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).CreateSession(MilestoneSeed);
            session.Start();
            var state = session.Session;
            var before = Inventory(state);
            Check.True(before.Length > 0, "开局后遗物袋/队列库存非空（初始化真的跑过）");

            var fault = Check.Fault(EngineFaultCode.SessionStateInvalid, () => session.Start(),
                "整局初始化只能跑一次");
            Check.True(fault.Message.Contains("already populated", StringComparison.Ordinal),
                $"第二次 Start 必须具名说明库存已建立（{fault.Message}）");
            Check.Equal(before, Inventory(state), "失败的第二次 Start 不得重建/重洗任何库存");
        });

        Check.Run("[ENV-T08 队列] ACT1-BASIC-10 的普通队列逐槽 = 跨实现已核对向量（参考侧重放）[真实]", () =>
        {
            // 期望向量来自同根构建的 `Differential.dll init-upfront --seed ACT1-BASIC-10` 的**参考侧重放**
            // （`reference/sts2-sim` 独立执行 + 工具层阶段分解，不是本实现自报的输出）。本包把它当已核对向量
            // 逐槽比对：它同时钉住"源前置消费之后从同一游标开始"和"弱 3 + 普通 12 的实际内容"。
            var expected = new[]
            {
                "base-spire:shrinker-beetle-weak", "base-spire:nibbits-weak", "base-spire:slimes-weak",
                "base-spire:fogmog-normal", "base-spire:slithering-strangler-normal",
                "base-spire:cubex-construct-normal",
                "base-spire:nibbits-normal", "base-spire:overgrowth-crawlers", "base-spire:mawler-normal",
                "base-spire:ruby-raiders-normal", "base-spire:snapping-jaxfruit-normal",
                "base-spire:vine-shambler-normal",
                "base-spire:slimes-normal", "base-spire:inklets-normal", "base-spire:flyconid-normal",
            };
            var session = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad()).CreateSession("ACT1-BASIC-10");
            session.Start();
            var observed = Queue(session.Session);
            Check.Sequence(expected, observed.Select(entry => entry.EncounterId),
                "ACT1-BASIC-10 的普通队列必须逐槽等于参考侧重放");
            Check.Equal(293, (int)session.Session.Random.DrawCount(UpFrontStream),
                "该 seed 的 up_front 总消费 = 参考侧阶段重放（112 + 118 + 0 + 30 + 16 + 15 + 1 + 1）");
            Console.WriteLine($"      ENV-T08 参考向量：ACT1-BASIC-10 普通队列 {observed.Count} 槽与参考侧一致，"
                + $"up_front {session.Session.Random.DrawCount(UpFrontStream)} 抽");
        });

        Check.Run("[BASE-02 队列] 标签回避真的生效：抽到与上一个共享标签的成员必须重抽（回避/不回避队列必须不同）[真实]", () =>
        {
            // 合成池：x1/x2 共享 `fixture:x`，y1 是 `fixture:y`。回避生效时第二个槽**不可能**再取到与
            // 第一个槽共享标签的那个成员；不回避时第二个槽可能正是它。两条路径的队列因此必须不同——
            // 否则"从不预扫、从不回避"的实现能蒙混过关（这正是本检查的判别力来源）。
            var pool = new SliceEncounterPool(
                [],
                [
                    new SliceEncounter("fixture:x1", SliceEncounterPool.NormalKind,
                        ["base-spire:calcified-cultist"], ["fixture:x"], [], []),
                    new SliceEncounter("fixture:x2", SliceEncounterPool.NormalKind,
                        ["base-spire:damp-cultist"], ["fixture:x"], [], []),
                    new SliceEncounter("fixture:y1", SliceEncounterPool.NormalKind,
                        ["base-spire:calcified-cultist"], ["fixture:y"], [], []),
                ],
                WeakRoomCount: 0,
                RoomCount: 3,
                UpFrontStream,
                "base-spire:combat-draw");
            var session = FixtureSession(pool);
            var observed = Queue(session);
            var avoid = Replay(pool, FixtureSeed, FixtureStreamSeed, 0, avoidTags: true);
            var plain = Replay(pool, FixtureSeed, FixtureStreamSeed, 0, avoidTags: false);

            Check.True(avoid.Retries > 0, $"这组合成池必须真的触发回避重抽（实际 {avoid.Retries} 次）");
            Check.False(
                avoid.Queue.Select(entry => entry.EncounterId)
                    .SequenceEqual(plain.Queue.Select(entry => entry.EncounterId), StringComparer.Ordinal),
                $"回避生效与不回避必须给出不同队列（回避=[{string.Join(",", avoid.Queue.Select(e => e.EncounterId))}] "
                + $"不回避=[{string.Join(",", plain.Queue.Select(e => e.EncounterId))}]）");
            Check.Sequence(avoid.Queue.Select(entry => entry.EncounterId),
                observed.Select(entry => entry.EncounterId),
                "生产队列必须等于**回避生效**的独立复算，而不是无约束抓取的结果");
            Check.Equal(avoid.Draws, (int)session.Random.DrawCount(UpFrontStream),
                "回避重抽的抽取次数必须与独立复算逐次相同（每次尝试一次抽取、预扫零次）");
            Console.WriteLine($"      BASE-02 回避：回避重抽 {avoid.Retries} 次，"
                + $"回避队列=[{string.Join(",", avoid.Queue.Select(e => e.EncounterId))}] "
                + $"不回避队列=[{string.Join(",", plain.Queue.Select(e => e.EncounterId))}]");
        });

        Check.Run("[BASE-02 队列] 谓词预扫失败：这次抓取零抽取，再无约束抓一次（标签全相同的合成池）[真实]", () =>
        {
            var pool = new SliceEncounterPool(
                [
                    new SliceEncounter("fixture:one", SliceEncounterPool.WeakKind,
                        ["base-spire:calcified-cultist"], ["fixture:same-tag"], [], []),
                    new SliceEncounter("fixture:two", SliceEncounterPool.WeakKind,
                        ["base-spire:damp-cultist"], ["fixture:same-tag"], [], []),
                ],
                [
                    new SliceEncounter("fixture:one", SliceEncounterPool.NormalKind,
                        ["base-spire:calcified-cultist"], ["fixture:same-tag"], [], []),
                    new SliceEncounter("fixture:two", SliceEncounterPool.NormalKind,
                        ["base-spire:damp-cultist"], ["fixture:same-tag"], [], []),
                ],
                WeakRoomCount: 2,
                RoomCount: 4,
                UpFrontStream,
                "base-spire:combat-draw");
            var session = FixtureSession(pool);
            var observed = Queue(session);
            var reference = Replay(pool, FixtureSeed, FixtureStreamSeed, 0);
            Check.False(session.RuleSet.Collections.Any(item => item.QualifiedId == EliteQueueCollection),
                "受控夹具（initializeNewRun=false）不声明精英队列：包形状与既有夹具一致");

            Check.Sequence(reference.Queue.Select(encounter => encounter.EncounterId),
                observed.Select(entry => entry.EncounterId), "逐槽的遭遇必须与独立复算一致");
            Check.Equal(4, observed.Count, "队列长度 = 配置的 4 间房");
            Check.True(reference.Fallbacks >= 3,
                $"所有遭遇共享同一标签时，除第一槽外每次预扫都应失败（实际 {reference.Fallbacks} 次）");
            Check.Equal(reference.Draws, (int)session.Random.DrawCount(UpFrontStream),
                "预扫失败仍然只消费**一次**无约束抽取（抽取次数必须与独立复算相同）");

            // 判别力：正因为走了"无约束抓取"，相邻两槽会共享标签（这正是原版在这种池下的行为）。
            for (var index = 1; index < observed.Count; index++)
            {
                Check.Equal("fixture:same-tag",
                    pool.Encounters.First(entry => entry.EncounterId == observed[index].EncounterId).Tags[0],
                    $"槽 {index} 仍来自同一个标签池（证明这条路径真的走到了无约束抓取）");
            }

            Console.WriteLine($"      BASE-02 队列（合成池）：4 槽，预扫失败 {reference.Fallbacks} 次，"
                + $"up_front 抽取 {reference.Draws} 次");
        });
    }

    /// <summary>源前置消费的抽取次数：每个遗物袋 / 共享古代人 / 事件列表各一次 Fisher-Yates
    /// （引擎 `RandomService.Shuffle` 为降序 Fisher-Yates ⇒ n 项恰好 n-1 抽）。用**观察到的**袋大小算，
    /// 不复制生产的入选策略。</summary>
    private static int PrefixDraws(Session session) => PrefixBreakdown(session).Total;

    private readonly record struct PrefixParts(int Bags, int Ancients, int Events, int EventMembers,
        IReadOnlyList<int> BagSizes)
    {
        public int Total => Bags + Ancients + Events;
    }

    private static PrefixParts PrefixBreakdown(Session session)
    {
        var sizes = new List<int>();
        foreach (var collection in session.RuleSet.Collections)
        {
            var id = collection.QualifiedId;
            if (!id.StartsWith("base-spire:SharedRelicBag", StringComparison.Ordinal)
                && !id.StartsWith("base-spire:PlayerRelicBag", StringComparison.Ordinal))
            {
                continue;
            }

            sizes.Add(session.State.CollectionMembers(collection.Id).Count);
        }

        var bags = sizes.Sum(size => Math.Max(0, size - 1));
        var ancients = Members(session, "base-spire:RunSharedAncients");
        var events = Members(session, "base-spire:RunEventQueue");
        return new PrefixParts(bags, Math.Max(0, ancients - 1), Math.Max(0, events - 1), events, sizes);
    }

    private static int Members(Session session, string collectionId)
    {
        var collection = session.RuleSet.Collections.SingleOrDefault(item => item.QualifiedId == collectionId);
        return collection is null ? 0 : session.State.CollectionMembers(collection.Id).Count;
    }

    /// <summary>精英段的抽取次数：每次 `EnqueueEncounter_elite` 恰好一次（无预扫回退）。</summary>
    private static int EliteDraws(Session session)
    {
        var collection = session.RuleSet.Collections.Single(item => item.QualifiedId == EliteQueueCollection);
        return session.State.CollectionMembers(collection.Id).Count;
    }

    /// <summary>独立复算：按冻结源码 `ActModel.GenerateRooms` + `GrabBag` 语义重放整幕队列
    /// （普通两段 + 精英段）。`advance` = 源前置消费的抽取次数，让复算从同一游标位置开始。</summary>
    private static QueueReference Replay(SliceEncounterPool pool, ulong sessionSeed, ulong streamSeed, int advance,
        bool avoidTags = true)
    {
        var random = new RandomService(sessionSeed, VanillaRandomLayout.AlgorithmId,
            VanillaRandomLayout.DrawProfileId, VanillaRandomLayout.Profile);
        random.SeedStream(UpFrontStream, streamSeed);
        for (var index = 0; index < advance; index++)
        {
            random.DrawDouble(UpFrontStream);
        }

        var reference = new QueueReference(pool, random, UpFrontStream, avoidTags);
        reference.Build();
        return reference;
    }

    private sealed class QueueReference(
        SliceEncounterPool pool, RandomService random, string stream, bool avoidTags = true)
    {
        private readonly List<SliceEncounter> _queue = [];
        private readonly List<SliceEncounter> _elite = [];
        private readonly List<SliceEncounter> _weakBag = [];
        private readonly List<SliceEncounter> _regularBag = [];
        private readonly List<SliceEncounter> _eliteBag = [];

        /// <summary>`GrabIndex` 的抽取次数（每次尝试一次；预扫 0 次）。**含**精英段。</summary>
        public int Draws { get; private set; }

        /// <summary>普通两段的抽取次数（总抽取减掉精英段）。</summary>
        public int NormalDraws => Draws - EliteDraws;

        /// <summary>精英段的抽取次数（与普通段分开记账，因为它写进**另一个**队列）。</summary>
        public int EliteDraws { get; private set; }

        /// <summary>走了"谓词预扫失败 ⇒ 无约束抓取"的次数。</summary>
        public int Fallbacks { get; private set; }

        /// <summary>因为"与上一个共享标签/就是上一个"而重抽的次数（源 `GrabAndRemove` 的谓词生效证明）。</summary>
        public int Retries { get; private set; }

        public IReadOnlyList<SliceEncounter> Queue => _queue;

        /// <summary>精英队列（源 `_rooms.eliteEncounters`）。</summary>
        public IReadOnlyList<SliceEncounter> Elite => _elite;

        public void Build()
        {
            for (var slot = 0; slot < pool.RoomCount; slot++)
            {
                var weakPhase = slot < pool.WeakRoomCount;
                var source = weakPhase ? pool.WeakEncounters : pool.RegularEncounters;
                var bag = weakPhase ? _weakBag : _regularBag;
                if (bag.Count == 0)
                {
                    bag.AddRange(source);
                }

                // `AddWithoutRepeatingTags`：先带谓词抓（预扫失败 ⇒ null 且零抽取），再无约束抓一次。
                var last = !avoidTags || _queue.Count == 0 ? null : _queue[^1];
                var picked = GrabAndRemove(bag, last);
                if (picked is null)
                {
                    Fallbacks++;
                    picked = GrabAndRemove(bag, null);
                }

                if (picked is not null)
                {
                    _queue.Add(picked);
                }
            }

            // 精英段：源是**独立**的第三段循环（`for num3 = 0; num3 < 15; num3++`），写进自己的列表。
            var beforeElite = Draws;
            for (var slot = 0; slot < pool.EliteRoomCount; slot++)
            {
                if (_eliteBag.Count == 0)
                {
                    _eliteBag.AddRange(pool.Elites);
                }

                var last = _elite.Count == 0 ? null : _elite[^1];
                var picked = GrabAndRemove(_eliteBag, last);
                if (picked is null)
                {
                    Fallbacks++;
                    picked = GrabAndRemove(_eliteBag, null);
                }

                if (picked is not null)
                {
                    _elite.Add(picked);
                }
            }

            EliteDraws = Draws - beforeElite;
        }

        private SliceEncounter? GrabAndRemove(List<SliceEncounter> bag, SliceEncounter? last)
        {
            bool Matches(SliceEncounter candidate) =>
                last is null
                || (!string.Equals(candidate.EncounterId, last.EncounterId, StringComparison.Ordinal)
                    && !candidate.Tags.Intersect(last.Tags, StringComparer.Ordinal).Any());

            if (last is not null && !bag.Any(Matches))
            {
                // `GrabIndex` 的预扫：没有成员满足谓词 ⇒ 返回 -1，**一次也不抽**。
                return null;
            }

            int index;
            var attempts = 0;
            do
            {
                index = GrabIndex(bag);
                attempts++;
            }
            while (last is not null && index >= 0 && !Matches(bag[index]));
            if (attempts > 1)
            {
                Retries += attempts - 1;
            }

            var picked = bag[index];
            bag.RemoveAt(index);
            return picked;
        }

        /// <summary>`GrabBag.GrabIndex`：`num = NextDouble() * 总权重`，按声明顺序累积，首个 `num &lt; acc` 命中。</summary>
        private int GrabIndex(List<SliceEncounter> bag)
        {
            Draws++;
            var target = random.DrawDouble(stream) * bag.Count;
            var accumulated = 0.0;
            for (var index = 0; index < bag.Count; index++)
            {
                accumulated += 1.0;
                if (target < accumulated)
                {
                    return index;
                }
            }

            return -1;
        }
    }

    private static Session FixtureSession(SliceEncounterPool pool)
    {
        var model = SliceAuthorFrontend.Load();
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, authorModel: model,
            encounterPool: pool, packageVersion: "encounter-queue-fixture").Build());
        var session = new Session(builder.Build(), FixtureSeed, [SliceManifest.PlayerRole],
            randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
            randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
            randomProfile: VanillaRandomLayout.Profile,
            computeBackend: SourceWeightedPickBackend.Instance);
        session.Random.SeedStream(UpFrontStream, FixtureStreamSeed);
        session.Start();
        return session;
    }

    private static IReadOnlyList<(int Index, string EncounterId)> Queue(Session session,
        string collectionId = QueueCollection, string entryTypeId = QueueEntryType)
    {
        var collection = session.RuleSet.Collections
            .Single(entry => entry.QualifiedId == collectionId).Id;
        var type = session.RuleSet.TypeByQualifiedId(entryTypeId);
        return
        [
            .. session.State.CollectionMembers(collection)
                .Select(instance => (
                    (int)session.State.GetField(instance, type.FieldIndex("Index")).AsInt(),
                    session.State.GetField(instance, type.FieldIndex("EncounterId")).AsString()))
                .OrderBy(entry => entry.Item1),
        ];
    }

    private static InstanceId RunInstance(Session session) => session.State
        .CollectionMembers(session.RuleSet.Collections.Single(entry => entry.QualifiedId == RunStatesCollection).Id)
        .Single();

    /// <summary>库存指纹：每个遗物袋 / 事件队列 / 共享古代人 / 两条遭遇队列的成员数（顺序稳定）。</summary>
    private static string Inventory(Session session) => string.Join(";", session.RuleSet.Collections
        .Where(collection => collection.QualifiedId.StartsWith("base-spire:SharedRelicBag", StringComparison.Ordinal)
            || collection.QualifiedId.StartsWith("base-spire:PlayerRelicBag", StringComparison.Ordinal)
            || collection.QualifiedId is "base-spire:RunEventQueue" or "base-spire:RunSharedAncients"
                or QueueCollection or EliteQueueCollection)
        .OrderBy(collection => collection.QualifiedId, StringComparer.Ordinal)
        .Select(collection => $"{collection.QualifiedId}={session.State.CollectionMembers(collection.Id).Count}"));

    private static long Number(Session session, InstanceId entity, string typeId, string field) =>
        session.State.GetField(entity, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsInt();

    private static string Text(Session session, InstanceId entity, string typeId, string field) =>
        session.State.GetField(entity, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsString();
}
