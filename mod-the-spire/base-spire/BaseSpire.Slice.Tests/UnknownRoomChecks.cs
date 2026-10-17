using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using BaseSpire.Slice.Rooms;
using BaseSpire.Slice.SourceMap;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-T09-unknown-room-modules-20261012：**真实问号房 + 事件模块**的局部判别面。
///
/// 判别目标是"行为不同会失败"，不是把生产算法抄一遍：
/// <list type="bullet">
/// <item>固定 seed `ENV-T03-SEED-L` 的**金标准**来自独立复算（Python 按源重写 hash/xoshiro/float32 赔率，
///       见 `.artifacts/parallel/dsh-unknown-room-modules-20261012/source-vectors-seed-L.txt`）：
///       第一笔 `unknown_map_point` payload 判定为 Event、`SunkenStatue` 事件级掷点 = -5 ⇒ 金币 111-5=106。</item>
/// <item>赔率状态按源**逐位 float32** 断言（0.1f+0.1f=0.2f、命中复位到 base 而不是累加）。</item>
/// <item>黑名单、非事件房型复用（战斗/精英/宝箱/商店的守卫必须认"实际房型"）用受控赔率做判别。</item>
/// <item>未实现源身份、已访问跳过与访问计数：错一个就会落到另一个身份或另一个计数。</item>
/// </list>
/// 所有会改变读取顺序的期望值都写成字面量，不从被测实现现算。
/// </summary>
internal static class UnknownRoomChecks
{
    private const string RunTypeId = "base-spire:Run";
    private const string NodeTypeId = "base-spire:MapNode";
    private const string NodeCollection = "base-spire:MapNodes";
    private const string ChildrenRelation = "base-spire:MapChildren";
    private const string RunStateCollection = "base-spire:RunStates";
    private const string ChooseAction = "base-spire:ChooseMapPoint";
    private const string EncounterQueue = "base-spire:EncounterQueue";
    private const string QueueEntryType = "base-spire:EncounterQueueEntry";
    private const string EliteQueue = "base-spire:EliteEncounterQueue";
    private const string EliteQueueEntryType = "base-spire:EliteEncounterQueueEntry";
    private const string MonstersCollection = "base-spire:Monsters";
    private const string VisitedCollection = "base-spire:RunVisitedEvents";
    private const string IdentityTypeId = "base-spire:RunRoomIdentity";

    /// <summary>独立复算用的固定 seed（与主清单本轮目标一致；不换 seed、不缩池）。</summary>
    private const string GoldenSeed = "ENV-T03-SEED-L";

    /// <summary>独立来源复算出的 `SunkenStatue` 本局金币量（`GoldVar(111) + Rng.NextInt(-10, 11)` = 106）。</summary>
    private const long GoldenSunkenStatueGold = 106;

    /// <summary>源 `SunkenStatue` 的 `HpLoss` = 7。</summary>
    private const long SunkenStatueHpLoss = 7;

    public static void Run()
    {
        SourceIdentityConversion();
        SourceOddsAndSunkenStatueGolden();
        BlacklistAndShopReuse();
        BattleAndTreasureReuse();
        UnregisteredEventIdentityFails();
        VisitedEventIsSkipped();
        EligibilityAndCounting();
        SourceEligibilityWithoutModules();
    }

    /// <summary>源身份 ↔ 注册 ID 的转换点是唯一且带校验的：源池成员可用，越界名字建包期失败。</summary>
    private static void SourceIdentityConversion()
    {
        Check.Run("ENV-T09 事件：源身份与注册模块 ID 的转换（逐字相等、越界名字建包期失败）", () =>
        {
            var module = new SunkenStatueEvent(ProbeState());
            Check.Equal(SunkenStatueEvent.SourceName, module.Id, "注册 ID 逐字等于源事件身份");
            Check.Equal(SunkenStatueEvent.SourceName, EventSourceIdentity.SourceName(module.Id), "逆方向转换一致");
            Check.True(EventSourceIdentity.IsKnownSource(module.Id), "模块 ID 属于源事件池");
            Check.False(EventSourceIdentity.IsKnownSource("SunkenStatueRenamed"), "改写过的名字不算源身份");
            try
            {
                EventSourceIdentity.ModuleId("SunkenStatueRenamed");
                throw new CheckFailedException("不在源池里的名字被接受了");
            }
            catch (ArgumentException)
            {
                // 预期的建包期失败：不重命名、不缩池。
            }
        });
    }

    /// <summary>转换检查只需要一个 `EventVisitState` 占位（不建会话、不抽随机数）。</summary>
    private static EventVisitState ProbeState()
    {
        var package = new EnginePackageBuilder("base-spire", "0.4.0");
        var runType = package.EntityType("Run");
        var runs = package.Collection("RunStates", runType);
        var initialization = new RunInitializationWiring(package, runType);
        return EventVisitLifecycle.Declare(package, runType, initialization);
    }

    /// <summary>
    /// 固定 seed 的第一次问号房：源判定为事件 → `SunkenStatue` → 源选项 `DIVE_INTO_WATER` 的真实写回与关闭。
    /// 期望值全部来自独立复算（payload 7790481153947240 → roll 0.8649171590805054；事件掷点 -5）。
    /// </summary>
    private static void SourceOddsAndSunkenStatueGolden()
    {
        Check.Run("ENV-T09 问号房：seed L 第一次判定 = 事件/SunkenStatue，赔率按源 float32 累加，选项真实写回并恢复旅行 [真实]", () =>
        {
            var session = NewRun();
            var run = RunInstance(session);
            var target = FindUnknownWithParent(session, out var parent);
            ArrangeAt(session, run, parent, "Monster");
            var hpBefore = FormalSliceFixture.Number(session, run, RunTypeId, "Hp");
            var goldBefore = FormalSliceFixture.Number(session, run, RunTypeId, "Gold");

            Submit(session, Legal(session, ChooseAction)!, target);

            var entered = RunInstance(session);
            Check.Equal("Event", ActualRoomType(session, entered), "实际房型 = 源 RoomType.Event");
            Check.Equal("SunkenStatue", Text(session, entered, RunTypeId, "CurrentEventId"),
                "当前事件 = 源队列第一条（RunEventQueue 的 up_front 洗牌结果）");
            Check.Equal(8L, FormalSliceFixture.Number(session, entered, RunTypeId, "MapPhase"), "事件相位 = 8");
            Check.Equal(1L, FormalSliceFixture.Number(session, entered, RunTypeId, "EventsVisited"),
                "源 RoomSet.MarkVisited(Event)：访问计数 +1");
            Check.Sequence(["SunkenStatue"], VisitedIds(session),
                "已访问集合 = 本次抽中的源身份（source 是同一个 ModelId 集合）");
            Check.Equal((int)MapPointType.Unknown, NodeType(session, target), "地图节点类型仍是 Unknown（不改写节点）");
            Check.Equal(0.2f, Odds(session, entered, "UnknownMonsterOdds"),
                "Monster 赔率 0.1f + 0.1f = 0.2f（命中 Event ⇒ 未选类型增加）");
            Check.Equal(-2f, Odds(session, entered, "UnknownEliteOdds"), "Elite 赔率 -1f + (-1f) = -2f");
            Check.Equal(0.04f, Odds(session, entered, "UnknownTreasureOdds"), "Treasure 赔率 0.02f + 0.02f = 0.04f");
            Check.Equal(0.06f, Odds(session, entered, "UnknownShopOdds"), "Shop 赔率 0.03f + 0.03f = 0.06f");

            var action = Legal(session, SunkenStatueEvent.DiveIntoWaterAction);
            Check.True(action is not null, "事件提供源选项 DIVE_INTO_WATER");
            Submit(session, action!);

            var closed = RunInstance(session);
            Check.Equal(hpBefore - SunkenStatueHpLoss, FormalSliceFixture.Number(session, closed, RunTypeId, "Hp"),
                "源 CreatureCmd.Damage(7)：生命 -7");
            Check.Equal(goldBefore + GoldenSunkenStatueGold, FormalSliceFixture.Number(session, closed, RunTypeId, "Gold"),
                "源 PlayerCmd.GainGold(111 + 掷点)：金币 +106（独立复算）");
            Check.Equal(2L, FormalSliceFixture.Number(session, closed, RunTypeId, "MapPhase"), "关闭后回到可旅行相位");
            Check.Equal(string.Empty, Text(session, closed, RunTypeId, "CurrentEventId"), "关闭后当前事件清空");
            Check.True(Legal(session, SunkenStatueEvent.DiveIntoWaterAction) is null,
                "选项只提交一次（守卫按当前事件身份关闭）");
            Check.True(Legal(session, ChooseAction) is not null, "关闭后可以继续选点旅行");
            Check.Equal("Event", ActualRoomType(session, closed), "事件房完成后实际房型仍是本次判定结果");
        });
    }

    /// <summary>源 `BuildRoomTypeBlacklist`：只有 Shop 会被拉黑，且拉黑期间它不参与"未选赔率增加"。</summary>
    private static void BlacklistAndShopReuse()
    {
        Check.Run("ENV-T09 问号房：判成商店时复用真实商店（相位/离店守卫按实际房型），命中即复位到 base [真实]", () =>
        {
            var session = NewRun();
            var run = RunInstance(session);
            var target = FindUnknownWithParent(session, out var parent);
            ArrangeAt(session, run, parent, "Monster");
            // 受控赔率：Monster/Elite/Treasure 置负（源语义：永不被抽中且不进累加），Shop 置 1f。
            SetOdds(session, run, -1f, -1f, -1f, 1f);
            Submit(session, Legal(session, ChooseAction)!, target);

            var entered = RunInstance(session);
            Check.Equal("Shop", ActualRoomType(session, entered), "Shop 命中");
            Check.Equal(MapRoomWiring.ShopPhase, (int)FormalSliceFixture.Number(session, entered, RunTypeId, "MapPhase"),
                "进入真实商店相位");
            Check.Equal(UnknownMapPointOdds.BaseShopOdds, Odds(session, entered, "UnknownShopOdds"),
                "命中类型复位到 base（0.03f），不是 1.03f");
            Check.True(Legal(session, "LeaveShop") is not null, "商店守卫认实际房型（可离店）");
        });

        Check.Run("ENV-T09 问号房：上一间房是商店时 Shop 被拉黑（同样赔率改判事件），且拉黑类型不累加 [真实]", () =>
        {
            var session = NewRun();
            var run = RunInstance(session);
            var target = FindUnknownWithParent(session, out var parent);
            ArrangeAt(session, run, parent, "Shop");
            SetOdds(session, run, -1f, -1f, -1f, 1f);
            Submit(session, Legal(session, ChooseAction)!, target);

            var entered = RunInstance(session);
            Check.Equal("Event", ActualRoomType(session, entered), "Shop 被拉黑后落到源默认 Event");
            Check.Equal("SunkenStatue", Text(session, entered, RunTypeId, "CurrentEventId"), "事件身份不受黑名单影响");
            Check.Equal(1f, Odds(session, entered, "UnknownShopOdds"),
                "拉黑类型既不命中也不累加（源 `roomTypes.Contains` 判定）");
        });
    }

    /// <summary>非事件判定必须复用真实房间入口：战斗/精英队列计数、宝箱守卫都按实际房型生效。</summary>
    private static void BattleAndTreasureReuse()
    {
        Check.Run("ENV-T09 问号房 → 普通战斗：消费普通遭遇队列并推进 per-type 访问计数 [真实]", () =>
        {
            var session = NewRun();
            var run = RunInstance(session);
            var target = FindUnknownWithParent(session, out var parent);
            ArrangeAt(session, run, parent, "Monster");
            SetOdds(session, run, 1f, -1f, -1f, -1f);
            Submit(session, Legal(session, ChooseAction)!, target);

            var entered = RunInstance(session);
            Check.Equal("Monster", ActualRoomType(session, entered), "判成普通战斗");
            Check.Equal(1L, FormalSliceFixture.Number(session, entered, RunTypeId, "MapPhase"), "战斗相位");
            Check.True(Monsters(session).Count > 0, "真的进入了战斗");
            Check.Contains(QueueIds(session, EncounterQueue, QueueEntryType),
                id => id == Text(session, entered, RunTypeId, "EncounterId"), "本场遭遇来自普通遭遇队列");
            Check.Equal(1L, FormalSliceFixture.Number(session, entered, RunTypeId, "NormalEncountersVisited"),
                "普通队列 per-type 访问计数 +1（只看节点类型会漏掉这一步）");
        });

        Check.Run("ENV-T09 问号房 → 精英：按实际房型走独立精英队列并出生 Byrdonis，不退回普通队列 [受控赔率]", () =>
        {
            var session = NewRun();
            var run = RunInstance(session);
            var target = FindUnknownWithParent(session, out var parent);
            ArrangeAt(session, run, parent, "Monster");
            SetOdds(session, run, -1f, 1f, -1f, -1f);
            var eliteIds = QueueIds(session, EliteQueue, EliteQueueEntryType);
            var normalIds = QueueIds(session, EncounterQueue, QueueEntryType);

            Submit(session, Legal(session, ChooseAction)!, target);
            var encounter = Text(session, RunInstance(session), RunTypeId, "EncounterId");
            Check.Equal("base-spire:byrdonis-elite", encounter, "seed L 首精英槽正常出生 Byrdonis");
            Check.Contains(eliteIds, id => id == encounter, "遭遇来自精英队列");
            Check.DoesNotContain(normalIds, id => id == encounter, "不得退回普通队列");
            Check.True(Monsters(session).Count > 0, "精英实际出生");
        });

        Check.Run("ENV-T09 问号房 → 宝箱：宝箱守卫按实际房型开放开箱动作 [真实]", () =>
        {
            var session = NewRun();
            var run = RunInstance(session);
            var target = FindUnknownWithParent(session, out var parent);
            ArrangeAt(session, run, parent, "Monster");
            SetOdds(session, run, -1f, -1f, 1f, -1f);
            Submit(session, Legal(session, ChooseAction)!, target);

            var entered = RunInstance(session);
            Check.Equal("Treasure", ActualRoomType(session, entered), "判成宝箱");
            Check.Equal((long)MapRoomWiring.TreasurePhase,
                FormalSliceFixture.Number(session, entered, RunTypeId, "MapPhase"), "宝箱相位");
            Check.True(Legal(session, "OpenTreasureChest") is not null, "宝箱守卫认实际房型（可开箱）");
        });
    }

    /// <summary>源池里尚未实现的源身份：显式失败，不伪造离开选项、不静默替代。</summary>
    private static void UnregisteredEventIdentityFails()
    {
        Check.Run("ENV-T09 事件：抽到未实现的源身份时具名失败（会话 Faulted，不提供伪造离场）[真实]", () =>
        {
            var session = NewRun();
            var run = RunInstance(session);
            var target = FindUnknownWithParent(session, out var parent);
            ArrangeAt(session, run, parent, "Monster");
            // 队列第 2 条（下标 1）= 源池顺序里下一个身份 `UnrestSite`，本构建没有它的模块。
            FormalSliceFixture.SetNumber(session, run, RunTypeId, "EventsVisited", 1);
            FormalSliceFixture.SetNumber(session, run, RunTypeId, "Hp", 56);

            var fault = Check.Fault(EngineFaultCode.SessionStateInvalid,
                () => Submit(session, Legal(session, ChooseAction)!, target),
                "未实现的源事件身份必须显式失败");
            Check.True(fault.Message.Contains("no registered executable module", StringComparison.Ordinal),
                $"失败原因是未注册模块（{fault.Message}）");
            Check.Equal(SessionStatus.Faulted, session.Status, "会话 Faulted：没有伪造的离场动作可提交");
        });
    }

    /// <summary>源 `EnsureNextEventIsValid`：已访问身份被跳过（访问计数前进），不会重复进同一个事件。</summary>
    private static void VisitedEventIsSkipped()
    {
        Check.Run("ENV-T09 事件：已访问身份被跳过、计数前进，选中的是下一个身份 [真实]", () =>
        {
            var session = NewRun();
            var run = RunInstance(session);
            var target = FindUnknownWithParent(session, out var parent);
            ArrangeAt(session, run, parent, "Monster");
            AddVisited(session, run, "SunkenStatue");
            FormalSliceFixture.SetNumber(session, run, RunTypeId, "Hp", 56);

            // 下标 0 已访问 ⇒ 跳过；下标 1 = `UnrestSite`（未实现）⇒ 具名失败。
            Check.Fault(EngineFaultCode.SessionStateInvalid,
                () => Submit(session, Legal(session, ChooseAction)!, target),
                "跳过后落到未实现身份");
            Check.Equal(2L, FormalSliceFixture.Number(session, run, RunTypeId, "EventsVisited"),
                "跳过已访问身份 +1（源 EnsureNextEventIsValid），进入事件房再 +1（源 MarkVisited）= 2");
            Check.Sequence(["SunkenStatue", "UnrestSite"], VisitedIds(session),
                "抽中的是队列里下一个身份（不是重复的 SunkenStatue）");
        });
    }

    /// <summary>
    /// 资格判定与"全部走完允许重复"：用一个**受控包**（不是正式内容）挂三个探针模块，
    /// 只有第二个可进入 ⇒ 第一次抽中它；三个都不可进入/已访问 ⇒ 源允许重复，落回本轮第一条。
    /// </summary>
    private static void EligibilityAndCounting()
    {
        Check.Run("ENV-T09 事件生命周期：资格不满足会跳过，全部走完按源允许重复 [受控初态]", () =>
        {
            var session = BuildLifecycleProbe();
            var run = RunInstance(session);

            Submit(session, Legal(session, "PullEvent")!, []);
            Check.Equal("SunkenStatue", Text(session, run, RunTypeId, "PulledId"),
                "资格不满足的 UnrestSite 被跳过，选中第一个可进入的身份");
            Check.Equal(1L, FormalSliceFixture.Number(session, run, RunTypeId, "EventsVisited"),
                "跳过 1 个身份：计数 +1");

            Submit(session, Legal(session, "PullEvent")!, []);
            Check.Equal("SunkenStatue", Text(session, run, RunTypeId, "PulledId"),
                "一轮走完（全部不允许或已访问）⇒ 源允许重复：回到本轮起点身份（eventsVisited 前进一个队列长度）");
            Check.Equal(4L, FormalSliceFixture.Number(session, run, RunTypeId, "EventsVisited"),
                "第二轮恰好前进队列长度 3（1 → 4）");
            Check.Sequence(["SunkenStatue"], VisitedIds(session),
                "重复身份不新增集合成员（源是 HashSet，去重语义一致）");
        });
    }

    // -- 受控包 -----------------------------------------------------------------------------------------

    /// <summary>源池里不但有身份、还带一个"资格恒 false"的探针；第二个身份可进入。</summary>
    private sealed class ProbeEvent(string sourceName, bool eligible) : IEventRoomModule, IEventEligibility
    {
        public string Id => EventSourceIdentity.ModuleId(sourceName);

        public Inst[] Enter(IrScope scope, EventCompileContext context, Expr run) => [];

        public void RegisterActions(EnginePackageBuilder package, EventCompileContext context)
        {
        }

        public Expr IsAllowed(IrScope scope, EventCompileContext context, Expr run) => scope.Const(eligible);
    }

    private static void SourceEligibilityWithoutModules()
    {
        Check.Run("ENV-T09 源资格独立于效果：跳过第一幕禁用事件和70%生命以上的UnrestSite [受控队列]", () =>
        {
            var session = BuildLifecycleProbe(["PotionCourier", "StoneOfAllTime", "UnrestSite", "SunkenStatue"], []);
            var run = RunInstance(session);
            FormalSliceFixture.SetNumber(session, run, RunTypeId, "Hp", 57);
            Submit(session, Legal(session, "PullEvent")!);
            Check.Equal("SunkenStatue", Text(session, run, RunTypeId, "PulledId"), "不因缺少效果模块而默认允许");
            Check.Equal(3L, FormalSliceFixture.Number(session, run, RunTypeId, "EventsVisited"), "只前进三个不合格身份");
            Check.Sequence(["SunkenStatue"], VisitedIds(session), "被跳过的身份不计已访问");

            var boundary = BuildLifecycleProbe(["UnrestSite", "SunkenStatue"], []);
            var boundaryRun = RunInstance(boundary);
            FormalSliceFixture.SetNumber(boundary, boundaryRun, RunTypeId, "Hp", 56);
            Submit(boundary, Legal(boundary, "PullEvent")!);
            Check.Equal("UnrestSite", Text(boundary, boundaryRun, RunTypeId, "PulledId"), "56/80恰好70%允许");
        });
        Check.Run("ENV-T09 未支持的源资格明确失败，不作为恒true或静默缩池 [受控队列]", () =>
        {
            var session = BuildLifecycleProbe(["LuminousChoir", "SunkenStatue"], []);
            var fault = Check.Fault(EngineFaultCode.SessionStateInvalid,
                () => Submit(session, Legal(session, "PullEvent")!), "资格未支持必须报错");
            Check.True(fault.Message.Contains("source event eligibility is not implemented for LuminousChoir", StringComparison.Ordinal),
                "失败定位到资格及源身份");
            Check.Equal(0, VisitedIds(session).Count, "资格未确定不记录事件访问");
        });
    }

    private static Session BuildLifecycleProbe(string[]? sourceNames = null, IEventRoomModule[]? moduleOverrides = null)
    {
        var package = new EnginePackageBuilder("base-spire", "0.4.0");
        var runType = package.EntityType("Run");
        var hp = runType.AddField("Hp", FieldTypeSchema.Int(0, 100));
        var maxHp = runType.AddField("MaxHp", FieldTypeSchema.Int(0, 100));
        var gold = runType.AddField("Gold", FieldTypeSchema.Int(0, 999));
        var pulled = runType.AddField("PulledId", FieldTypeSchema.String(64));
        var card = package.EntityType("Card");
        var deck = package.Collection("LongTermDeck", card);
        var runs = package.Collection("RunStates", runType);
        var view = package.Projection("PlayerView");
        view.ParticipantRole = "player";
        view.Type(runType);
        var map = MapRoomWiring.Declare(package, view, runType);
        var rooms = new RoomCompileContext(runType, runs, hp, maxHp, gold, deck, map);

        var initialization = new RunInitializationWiring(package, runType);
        var state = EventVisitLifecycle.Declare(package, runType, initialization);
        var context = new EventCompileContext(rooms, state.CurrentEventId, MapRoomWiring.EventPhase);
        IEventRoomModule[] modules = moduleOverrides ??
        [
            new ProbeEvent("UnrestSite", eligible: false),
            new ProbeEvent("SunkenStatue", eligible: true),
            new ProbeEvent("WarHistorianRepy", eligible: false),
        ];
        var lifecycle = new EventVisitLifecycle(state, context, modules);
        var catalog = new EventRoomCatalog(modules);
        catalog.RegisterActions(package, context);

        var pull = package.ActionBody(package.Action("PullEvent"));
        var pullRun = context.Room.CurrentRun(pull);
        var result = lifecycle.Pull(pull, pullRun);
        pull.Role(SliceManifest.PlayerRole);
        pull.Body([.. result.Steps, pull.SetField(pullRun, pulled, pull.LocalRef(result.IdSlot))]);

        var setup = package.Procedure("Setup");
        var area = package.ScopeType("ProbeArea");
        var areaSlot = setup.Local("probeArea", FieldTypeSchema.Binding());
        var runSlot = setup.Local("probeRun", FieldTypeSchema.RefTo(runType));
        var created = setup.Local("probeCreated", FieldTypeSchema.Binding());
        var steps = new List<Inst>
        {
            setup.CreateScope(area, areaSlot),
            setup.CreateEntity(runType, setup.LocalRef(areaSlot), runSlot,
                [(hp, setup.Const(50L)), (maxHp, setup.Const(80L)), (gold, setup.Const(0L)),
                 (pulled, setup.Const(string.Empty))], runs),
        };
        // 源池身份顺序（与 RunSourcePoolManifest 一致）：本包只放三个，探针用。
        foreach (var sourceName in sourceNames ?? ["UnrestSite", "SunkenStatue", "WarHistorianRepy"])
        {
            steps.Add(setup.CreateEntity(initialization.RoomIdentity, setup.EntityScope(setup.LocalRef(runSlot)), created,
                [(initialization.RoomId, setup.Const(sourceName))], initialization.Events));
        }

        setup.Body(steps.ToArray());
        package.OnSessionStart(setup.Symbol);

        var builder = new RuleSetBuilder();
        builder.Add(package.Build());
        var session = new Session(builder.Build(), 20261012UL, [SliceManifest.PlayerRole]);
        session.Start();
        return session;
    }

    // -- 夹具 -------------------------------------------------------------------------------------------

    private static Session NewRun()
    {
        var run = new BaseSpireRuntimeFactory(RunConfiguration.FrozenIronclad(0, BaseSpireScope.DevMap))
            .CreateSession(GoldenSeed);
        run.Start();
        return run.Session;
    }

    /// <summary>在 seed 的公开地图上找一个问号节点及其父节点（只用公开地图事实）。</summary>
    private static InstanceId FindUnknownWithParent(Session session, out InstanceId parent)
    {
        var nodes = Nodes(session);
        foreach (var candidate in nodes.Where(node => node.Row >= 1 && node.Row < 14))
        {
            var unknown = session.State.RelationTargets(Relation(session, ChildrenRelation), candidate.Id)
                .FirstOrDefault(child => NodeType(session, child) == (int)MapPointType.Unknown);
            if (unknown != default)
            {
                parent = candidate.Id;
                return unknown;
            }
        }

        throw new CheckFailedException($"seed {GoldenSeed} 的公开地图里找不到带前驱的问号节点");
    }

    /// <summary>把状态安排成"刚在 parent 结算完、还没选下一个"（真实字段语义，不是旁路）。</summary>
    private static void ArrangeAt(Session session, InstanceId run, InstanceId parent, string actualRoomType)
    {
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "MapPhase", 2);
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "MapFloor",
            Math.Min(Row(session, parent) + 1, MapRoomWiring.MaxFloor - 1));
        var nodeField = session.RuleSet.TypeByQualifiedId(RunTypeId).FieldIndex("CurrentNode");
        var actualField = session.RuleSet.TypeByQualifiedId(RunTypeId).FieldIndex("ActualRoomType");
        var roomType = session.RuleSet.Enums.Single(item => item.QualifiedId == "base-spire:RoomType");
        session.State.RunAtomic(() =>
        {
            session.State.SetField(run, nodeField, Value.OfRef(parent));
            session.State.SetField(run, actualField, Value.OfEnum(roomType.OrdinalOf(actualRoomType)));
        });
    }

    private static void AddVisited(Session session, InstanceId run, string sourceName)
    {
        var identity = session.RuleSet.TypeByQualifiedId(IdentityTypeId);
        var collection = session.RuleSet.Collections.Single(item => item.QualifiedId == VisitedCollection).Id;
        session.State.RunAtomic(() =>
        {
            var entity = session.State.CreateInstance(identity.Id, session.State.ScopeOf(run));
            session.State.SetField(entity, identity.FieldIndex("SourceId"), Value.Of(sourceName));
            session.State.AddToCollection(collection, entity);
        });
    }

    private static void SetOdds(Session session, InstanceId run, float monster, float elite, float treasure,
        float shop)
    {
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "UnknownMonsterOdds", UnknownMapPointOdds.BitsOf(monster));
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "UnknownEliteOdds", UnknownMapPointOdds.BitsOf(elite));
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "UnknownTreasureOdds", UnknownMapPointOdds.BitsOf(treasure));
        FormalSliceFixture.SetNumber(session, run, RunTypeId, "UnknownShopOdds", UnknownMapPointOdds.BitsOf(shop));
    }

    private static float Odds(Session session, InstanceId run, string field) =>
        UnknownMapPointOdds.FloatOf(FormalSliceFixture.Number(session, run, RunTypeId, field));

    private static string ActualRoomType(Session session, InstanceId run)
    {
        var ordinal = session.State
            .GetField(run, session.RuleSet.TypeByQualifiedId(RunTypeId).FieldIndex("ActualRoomType")).AsEnumOrdinal();
        return session.RuleSet.Enums.Single(item => item.QualifiedId == "base-spire:RoomType").NameOf(ordinal);
    }

    private static IReadOnlyList<string> VisitedIds(Session session)
    {
        var identity = session.RuleSet.TypeByQualifiedId(IdentityTypeId);
        return
        [
            .. session.State.CollectionMembers(FormalSliceFixture.Collection(session, VisitedCollection))
                .Select(item => session.State.GetField(item, identity.FieldIndex("SourceId")).AsString()),
        ];
    }

    private static List<(InstanceId Id, int Row)> Nodes(Session session) =>
    [
        .. session.State.CollectionMembers(FormalSliceFixture.Collection(session, NodeCollection))
            .Select(id => (id, (int)FormalSliceFixture.Number(session, id, NodeTypeId, "Row"))),
    ];

    private static int Row(Session session, InstanceId node) =>
        (int)FormalSliceFixture.Number(session, node, NodeTypeId, "Row");

    private static int NodeType(Session session, InstanceId node) =>
        session.State.GetField(node, session.RuleSet.TypeByQualifiedId(NodeTypeId).FieldIndex("Type"))
            .AsEnumOrdinal();

    private static InstanceId RunInstance(Session session) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, RunStateCollection)).Single();

    private static IReadOnlyList<InstanceId> Monsters(Session session) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, MonstersCollection));

    private static List<string> QueueIds(Session session, string collectionId, string entryTypeId)
    {
        var type = session.RuleSet.TypeByQualifiedId(entryTypeId);
        return
        [
            .. session.State.CollectionMembers(FormalSliceFixture.Collection(session, collectionId))
                .Select(id => session.State.GetField(id, type.FieldIndex("EncounterId")).AsString()),
        ];
    }

    private static LegalAction? Legal(Session session, string actionId)
    {
        var qualified = actionId.Contains(':', StringComparison.Ordinal)
            ? actionId
            : $"{SliceManifest.PackageId}:{actionId}";
        return session.LegalActions(SliceManifest.Player)
            .FirstOrDefault(candidate => candidate.Definition.QualifiedId == qualified);
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

    private static string Text(Session session, InstanceId entity, string typeId, string field) =>
        session.State.GetField(entity, session.RuleSet.TypeByQualifiedId(typeId).FieldIndex(field)).AsString();

    private static int Relation(Session session, string relation) =>
        session.RuleSet.Relations.Single(item => item.QualifiedId == relation).Id;
}
