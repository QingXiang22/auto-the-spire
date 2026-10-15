using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace Differential;

/// <summary>
/// **base（本项目）后端适配器**：把声明式夹具翻译成受控 Engine 会话。
///
/// 边界约定（两个后端必须逐条对齐，见 <c>fixtures/*.json</c> 的 <c>note</c>）：
/// <list type="number">
/// <item>边界 0 = <c>start</c>：受控初始状态建好、脚本执行之前；</item>
/// <item>每个脚本步骤 = 一个边界 <c>step:&lt;n&gt;:&lt;action&gt;</c>；</item>
/// <item>步骤内出现的逻辑决策点（选目标、选牌…）作为该边界的 <c>Decisions</c> 记录，
///       候选取**回答之前**的集合，并记下最终选择——这样"候选数量/参数/约束"本身就参与比较。</item>
/// </list>
/// 两个后端的调度微步不同（我们这边会挂起会话、参考那边把目标当参数、选牌走决策源），
/// 因此比较的是"一次外部动作结算完"这个**共同逻辑边界**，不是各自的函数调用序列（ENV §6.2）。
///
/// <para>
/// **ENV-T04：会话来源。** 每个 case 声明 <see cref="FixtureCase.BaseSource"/>，两种来源都必须
/// 满足"开局前配置随机/数值/Compute 后端、只 Start 一次"：
/// <list type="bullet">
/// <item><see cref="BaseSources.FormalFactory"/>：<c>BaseSpireRuntimeFactory</c> +
///       <c>RunConfiguration</c>（dev 范围）→ <c>CreateSession(gameSeed)</c> → <c>Start()</c> 一次，
///       不做任何受控初始化。C 的公开 Host 走的就是这一条，因此它就是"同构建/配置/seed"的证据；</item>
/// <item><see cref="BaseSources.ControlledSlice"/>：受控机制夹具。参考侧是**单场战斗**适配器
///       （<c>RunState</c> + <c>CombatState</c>），没有整局/房间/遭遇队列驱动，只有受控单场状态
///       才能与它逐边界对应，因此这里用受控切片包；但 Session 用<strong>与工厂相同的公开配置资产</strong>配置
///       （<c>VanillaRandomLayout</c> 的算法/映射/Profile + <c>SeedAllStreams</c> +
///       <c>SourceWeightedPickBackend</c>），不再是 <c>new</c> 出来的默认随机 Session。
///       受控状态只用于机制对拍，**不能**当正常开局胜负证据（ENV §5/§6.1）。</item>
/// </list>
/// </para>
/// </summary>
internal static class BaseBackend
{
    public const string Name = "base-spire:controlled-slice-v1";
    public const string FormalName = "base-spire:singleplayer-dev-v1";
    public const string Version = "env-t04-base@1";

    private const string PlayerType = "base-spire:Player";
    private const string MonsterType = "base-spire:Monster";
    private const string CardType = "base-spire:Card";
    private const string PowerType = "base-spire:Power";
    private const string PlayCardAction = "base-spire:PlayCard";
    private const string EndTurnAction = "base-spire:EndTurn";
    private const string HandPile = "base-spire:Hand";
    private const string DrawPile = "base-spire:DrawPile";
    private const string DiscardPile = "base-spire:DiscardPile";
    private const string PowersRelation = "base-spire:Powers";

    public static CaseRecord Run(FixtureCase fixture) => fixture.Kind == CaseKinds.NormalReward
        ? NormalRewardEntry.RunBase(fixture)
        : fixture.Kind == CaseKinds.Wiring
        ? RunWiring(fixture)
        : fixture.Kind == CaseKinds.Map
            ? RunMap(fixture)
            : RunScripted(fixture);

    // Self-check only: alter an already-held Power before recording start, then execute the real script.
    internal static CaseRecord RunPowerInitializationFault(FixtureCase fixture, string field, bool value) =>
        RunScripted(fixture, session =>
        {
            var type = session.RuleSet.TypeByQualifiedId(PowerType);
            foreach (var declared in fixture.Setup.Powers ?? [])
            {
                var owner = Resolve(declared.Owner, session);
                var relation = session.RuleSet.Relations.Single(entry => entry.QualifiedId == PowersRelation);
                foreach (var instance in session.State.RelationTargets(relation.Id, owner))
                    if (session.State.GetField(instance, type.FieldIndex("DefinitionId")).AsString() == declared.Power)
                        session.State.RunAtomic(() => session.State.SetField(instance, type.FieldIndex(field), Value.Of(value)));
            }
        });

    // -- 地图机制：正式工厂正常开局后的地图公开投影 ---------------------------------------------------------

    /// <summary>
    /// `kind=map`：只比幕 1 地图生成机制。base 侧走**唯一正式工厂**的正常开局（`dev-map` 范围），
    /// 事实只从引擎公开投影 `base-spire:MapNodes` / `base-spire:MapChildren` 读，流只投影
    /// `base-spire:run:act_1_map` 一条。**不**用 `SourceActMapCompute` 把抽数词表反推成网格——
    /// 那是拿 base 自己的重放实现自证形状。
    /// </summary>
    private static CaseRecord RunMap(FixtureCase fixture)
    {
        var notes = new List<string>
        {
            "kind=map：只比地图生成机制。事实来自引擎公开投影 base-spire:MapNodes / base-spire:MapChildren，"
                + "流只投影 base-spire:run:act_1_map；正常开局还会消费 UpFront/洗牌等流，那些不属于本 case"
                + "（由 rng-initial-shuffle 等覆盖）。",
            "Actions 传空表：参考侧没有 ChooseMapPoint 的对应物，把 base 的动作列表放进来只会造出与本 case 主张无关的差异；"
                + "base 侧 ChooseMapPoint 的合法性由 base 自身的房间检查覆盖，不在本 case 比较范围内。",
            "本 case 停在 Start 之后的地图相位边界：不声称多房间/奖励/结算跑通。",
        };
        var boundaries = new List<BoundaryRecord>();

        Session session;
        string buildId;
        try
        {
            (session, buildId) = Open(fixture);
        }
        catch (Exception failure)
        {
            return new CaseRecord(fixture.Id, FormalName, Version, fixture.RngProfile, "invalid_fixture",
                $"{failure.GetType().Name}: {failure.Message}", false, notes, boundaries)
            {
                BaseSource = fixture.BaseSource,
            };
        }

        var status = session.Status == SessionStatus.Faulted ? "faulted" : "ok";
        var detail = status == "faulted"
            ? $"正式开局之后引擎报错：{session.Fault?.Message}"
            : null;

        // 不能复用 Add(...)：它固定取 LegalActions/Facts/Streams，而这里要用地图投影并给空动作表。
        boundaries.Add(new BoundaryRecord(
            0,
            "start",
            $"正式开局（工厂 {fixture.BaseScope} 范围，seed={fixture.GameSeed}）",
            [],
            [],
            MapSnapshot.Facts(session),
            MapSnapshot.Streams(session),
            EngineSnapshot.Capture(session)));

        return new CaseRecord(fixture.Id, FormalName, Version, fixture.RngProfile, status, detail, false, notes,
            boundaries)
        {
            EngineTrace = CaptureTrace(session),
            BaseSource = fixture.BaseSource,
            Terminal = session.Terminal?.Definition.QualifiedId,
            SessionStatus = session.Status.ToString(),
            BuildId = buildId,
            SessionIdentity = session.Identity,
        };
    }

    // -- 受控/正式开局会话：跑脚本 ---------------------------------------------------------------------------

    private static CaseRecord RunScripted(FixtureCase fixture, Action<Session>? initializationFault = null)
    {
        var boundaries = new List<BoundaryRecord>();
        var notes = new List<string>
        {
            "decisions: Kind/Prompt 是各后端自己的表述，只入记录不参与比较；候选与数量约束参与比较。",
            "events: EngineTrace 保存本侧真实执行/事件/决策/RNG 轨迹；参考侧规范化与事件顺序比较仍未完成，不计双侧通过。",
        };

        var backend = fixture.BaseSource == BaseSources.FormalFactory ? FormalName : Name;
        Session session;
        string buildId;
        try
        {
            (session, buildId) = Open(fixture);
            initializationFault?.Invoke(session);
        }
        catch (Exception failure)
        {
            return new CaseRecord(fixture.Id, backend, Version, fixture.RngProfile, "invalid_fixture",
                $"{failure.GetType().Name}: {failure.Message}", false, notes, boundaries)
            {
                BaseSource = fixture.BaseSource,
            };
        }

        var participant = session.Participant(0);
        var exactPileOrder = fixture.RngProfile == RngProfiles.VanillaCompatible || fixture.Setup.InitialPiles is not null;
        var startDraws = EngineSnapshot.TotalDraws(session);
        var startStreams = EngineSnapshot.Streams(session);
        Add(boundaries, "start", StartLabel(fixture), session, participant, [], exactPileOrder);

        var status = "ok";
        string? detail = null;
        for (var index = 0; index < fixture.Script.Count; index++)
        {
            var step = fixture.Script[index];
            var decisions = new List<DecisionSnapshot>();
            try
            {
                Execute(session, participant, step, decisions);
            }
            catch (Exception failure)
            {
                status = failure is CheckFailed ? "invalid_action" : "faulted";
                detail = $"第 {index} 步 {step.Action}: {failure.GetType().Name}: {failure.Message}";
                Add(boundaries, $"abort:{index}", detail, session, participant, decisions, exactPileOrder);
                break;
            }

            if (session.Status == SessionStatus.Faulted)
            {
                status = "faulted";
                detail = $"第 {index} 步 {step.Action} 之后引擎报错：{session.Fault?.Message}";
                Add(boundaries, $"step:{index}:{step.Action}", DescribeStep(step), session, participant, decisions,
                    exactPileOrder);
                break;
            }

            Add(boundaries, $"step:{index}:{step.Action}", DescribeStep(step), session, participant, decisions,
                exactPileOrder, step.ObserveDrawTop);

            if (session.Status == SessionStatus.AwaitingDecision)
            {
                status = "unsupported_decision";
                detail = $"第 {index} 步之后仍有未在脚本里回答的决策点：{session.PendingDecision?.Prompt}";
                break;
            }

            if (session.Status == SessionStatus.Terminal)
            {
                status = "terminal";
                break;
            }
        }

        var consumed = EngineSnapshot.TotalDraws(session) != startDraws;
        if (consumed)
        {
            // 这条 note 只在原版兼容 Profile 下可以说"参与判定"：其余声明实测有消费就不可计入
            // （Comparator.countable 的门槛）。原文本把所有消费都写成"按 unsupported_rng_profile 处理"，
            // 在 random-reshuffle-unsupported 于 2026-10-12 原地升级为 vanilla-compatible@1 之后就是错的。
            notes.Add(fixture.RngProfile == RngProfiles.VanillaCompatible
                ? "本 case 在 start 边界之后消费了新的随机数：声明为原版兼容 Profile，两侧具名流逐边界参与比较。"
                : "本 case 在 start 边界之后消费了新的随机数：不是原版兼容声明，按 unsupported_rng_profile 处理，不计通过。");
        }

        if (fixture.RngProfile != RngProfiles.ControlledNoRandom && fixture.RngProfile != RngProfiles.ControlledStateNoRandom && fixture.RngProfile != RngProfiles.VanillaCompatible)
        {
            notes.Add($"rng_profile='{fixture.RngProfile}' 不是受控无随机也不是原版兼容：本 case 不计通过。");
        }

        return new CaseRecord(fixture.Id, backend, Version, fixture.RngProfile, status, detail, consumed, notes,
            boundaries)
        {
            EngineTrace = CaptureTrace(session),
            InitialState = fixture.Setup.InitialPiles is null ? null : new InitialStateProof(fixture.Setup, true),
            InitializationRandomCount = startDraws,
            ScriptRandom = new RandomMeasurement("raw-generator-words", "Session.Random all streams", startDraws, EngineSnapshot.TotalDraws(session), startStreams, EngineSnapshot.Streams(session)),
            BaseSource = fixture.BaseSource,
            Terminal = session.Terminal?.Definition.QualifiedId,
            SessionStatus = session.Status.ToString(),
            BuildId = buildId,
            SessionIdentity = session.Identity,
        };
    }

    private static string StartLabel(FixtureCase fixture) =>
        fixture.BaseSource == BaseSources.FormalFactory
            ? $"正式开局（工厂 dev 范围，seed={fixture.GameSeed}）"
            : "受控初始状态";

    /// <summary>按 case 声明的来源打开一个**已配置、已 Start 一次**的会话，并给出只读构建身份。</summary>
    private static (Session Session, string BuildId) Open(FixtureCase fixture)
    {
        var gameSeed = fixture.GameSeed;
        if (string.IsNullOrWhiteSpace(gameSeed))
        {
            if (fixture.BaseSource == BaseSources.FormalFactory)
            {
                throw new CheckFailed("base_source=formal-dev-factory 必须给出每局字符串 game_seed");
            }

            gameSeed = "ENV-T04-CTRL-" + fixture.Id;
        }

        if (fixture.BaseSource == BaseSources.FormalFactory)
        {
            var run = OpenFormalSession(gameSeed, fixture.BaseScope);
            return (run.Session, run.Build.PublicBuildId);
        }

        if (fixture.BaseSource != BaseSources.ControlledSlice)
        {
            throw new CheckFailed($"未知 base_source '{fixture.BaseSource}'");
        }

        return (OpenControlledSession(fixture, gameSeed), ControlledBuildId(fixture));
    }

    /// <summary>
    /// 正式开局：唯一工厂 + dev 范围配置。`CreateSession` 返回**已配置、尚未 Start** 的会话
    /// （零随机消费、零玩家实例），这里调用 `Start()` **恰好一次**；不做任何受控初始化。
    /// 不支持的 Ironclad/full 范围在构造工厂时就已拒绝（见 <see cref="RunWiring"/> 的实测）。
    /// </summary>
    private static BaseSpireRunSession OpenFormalSession(string gameSeed, string baseScope)
    {
        var scope = baseScope == BaseScopes.DevMap ? BaseSpireScope.DevMap : BaseSpireScope.Dev;
        var factory = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, scope));
        var run = factory.CreateSession(gameSeed);
        run.Start();
        return run;
    }

    /// <summary>
    /// 受控机制会话：受控切片包 + **与工厂相同的公开配置资产**（算法/映射/Profile、全部原版线流、
    /// 加权选取 Compute 后端）。这里的每一条都对应工厂 <c>CreateSession</c> 里的同一句，差别只在包本身
    /// （受控单场 vs 正式整局），因为参考侧只有单场战斗。
    /// </summary>
    private static Session OpenControlledSession(FixtureCase fixture, string gameSeed)
    {
        if (fixture.Setup.InitialPiles is not null) ControlledInit.Validate(fixture.Setup);
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(fixture.Setup.IncludeHeadbutt).Build());
        var rules = builder.Build();

        var session = new Session(
            rules,
            VanillaRandomLayout.RunSeed(gameSeed),
            [SliceManifest.PlayerRole],
            computeBackend: SourceWeightedPickBackend.Instance,
            randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
            randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
            debug: new SessionDebugOptions(),
            randomProfile: VanillaRandomLayout.Profile);

        // 与工厂同一顺序：先按原版布局种好全部线流，再由控制入口 Start。
        VanillaRandomLayout.SeedAllStreams(session.Random, gameSeed);
        if (session.Random.TotalDraws != 0)
        {
            throw new CheckFailed($"受控会话在建局阶段消费了 {session.Random.TotalDraws} 个随机数");
        }

        session.Start();
        Apply(session, fixture.Setup);
        if (fixture.Setup.InitialPiles is not null) ApplyOrdered(session, fixture.Setup.InitialPiles);
        return session;
    }

    private static string ControlledBuildId(FixtureCase fixture) =>
        $"controlled-slice-v1:headbutt={fixture.Setup.IncludeHeadbutt}";

    // -- 工厂接线检查 ---------------------------------------------------------------------------------------

    /// <summary>
    /// 唯一工厂的接线检查：dev 范围可用、未实现范围在**建局前**被拒绝、工厂不代启动、
    /// 随机/数值/Compute 配置在 Start 之前就位。它是工具/生产集成证据，不是内容覆盖。
    /// </summary>
    private static CaseRecord RunWiring(FixtureCase fixture)
    {
        var notes = new List<string>
        {
            "kind=wiring：本 case 检查唯一工厂的接线与拒绝行为，属于工具/生产集成证据，不是规则对拍。",
            "受控初始化不参与：这一条走的是正式工厂的会话创建路径。",
        };
        var boundaries = new List<BoundaryRecord>();
        var rejections = new List<string>();

        BaseSpireRuntimeFactory factory;
        try
        {
            factory = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.Dev));
        }
        catch (Exception failure)
        {
            return new CaseRecord(fixture.Id, FormalName, Version, fixture.RngProfile, "invalid_fixture",
                $"dev 范围的工厂构造失败：{failure.GetType().Name}: {failure.Message}", false, notes, boundaries)
            {
                BaseSource = fixture.BaseSource,
            };
        }

        ExpectRejected(rejections, "scope=Ironclad",
            () => new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.Ironclad)));
        ExpectRejected(rejections, "scope=Full",
            () => new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.Full)));
        ExpectRejected(rejections, "ascension=1",
            () => new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(1)));
        ExpectRejected(rejections, "character=Silent",
            () => new BaseSpireRuntimeFactory(new RunConfiguration { Character = "Silent" }));
        ExpectRejected(rejections, "scope=unknown",
            () => new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, "base-spire-made-up-v1")));
        ExpectRejected(rejections, "source_baseline=unknown",
            () => new BaseSpireRuntimeFactory(new RunConfiguration
            {
                Character = BaseSpireCharacters.IroncladId,
                SourceBaselineId = "sts2-made-up-baseline",
            }));

        var build = factory.Describe();
        var run = factory.CreateSession(fixture.GameSeed!);
        var playerType = run.Session.RuleSet.TypeByQualifiedId(PlayerType);
        var noDraws = run.Session.Random.TotalDraws == 0;
        var noPlayer = run.Session.State.AllInstances(playerType.Id).Count == 0;
        var algorithm = run.Session.Random.AlgorithmId;
        var drawProfile = run.Session.Random.DrawProfileId;
        var compute = run.Session.ComputeBackend?.Id ?? string.Empty;

        run.Start();
        var startedStatus = run.Session.Status.ToString();
        var startOnce = run.Session.Status is SessionStatus.Running or SessionStatus.AwaitingDecision
            or SessionStatus.Terminal;
        Add(boundaries, "start", $"正式开局（工厂 dev 范围，seed={fixture.GameSeed}）", run.Session,
            run.Participant, [], false);

        var wiring = new FactoryWiringSnapshot(
            build.ScopeId,
            build.CharacterName,
            build.Ascension,
            build.SourceBaselineId,
            build.PublicBuildId,
            noDraws,
            noPlayer,
            algorithm,
            drawProfile,
            compute.Length > 0,
            startOnce,
            startedStatus,
            rejections);

        var ok = noDraws && noPlayer && compute.Length > 0 && startOnce
            && algorithm == VanillaRandomLayout.AlgorithmId
            && drawProfile == VanillaRandomLayout.DrawProfileId
            && rejections.Count == 6;

        return new CaseRecord(fixture.Id, FormalName, Version, fixture.RngProfile, ok ? "ok" : "wiring_failed",
            ok ? null : Summarize(wiring), false, notes, boundaries)
        {
            BaseSource = fixture.BaseSource,
            Terminal = run.Session.Terminal?.Definition.QualifiedId,
            SessionStatus = run.Session.Status.ToString(),
            BuildId = build.PublicBuildId,
            SessionIdentity = run.SessionIdentity,
            FactoryWiring = wiring,
        };
    }

    private static string Summarize(FactoryWiringSnapshot wiring) =>
        $"工厂接线检查未通过：no_draws={wiring.UnstartedSessionHasNoDraws},"
        + $" no_player={wiring.UnstartedSessionHasNoPlayerInstance},"
        + $" compute={wiring.ComputeBackendInstalled}, start_once={wiring.StartOnceSucceeded},"
        + $" algorithm={wiring.RandomAlgorithmId}, draw={wiring.RandomDrawProfileId},"
        + $" rejections={wiring.Rejections.Count}";

    /// <summary>一次"必须被拒绝"的实测：记录拒绝类型，未拒绝则原样记进结果（不静默放过）。</summary>
    private static void ExpectRejected(List<string> rejections, string label, Action action)
    {
        try
        {
            action();
            rejections.Add($"{label}: NOT-REJECTED");
        }
        catch (Exception failure)
        {
            rejections.Add($"{label}: {failure.GetType().Name}");
        }
    }

    // -- 构造与受控初始状态 ---------------------------------------------------------------------------------

    private static EngineTraceSnapshot? CaptureTrace(Session session)
    {
        if (session.Debug is not { } trace) return null;
        static EngineTraceEntry Convert(SessionDebugRecord record) => new(record.Sequence, record.Kind.ToString(),
            record.Subject, record.Detail, record.Source?.ToString(), record.StateVersion, record.Status.ToString(), record.Instructions);
        return new EngineTraceSnapshot(trace.Capture().Select(Convert).ToArray(), trace.DroppedRecords,
            trace.FirstFault is { } fault ? Convert(fault) : null);
    }

    private static void Apply(Session session, FixtureSetup setup)
    {
        var rules = session.RuleSet;
        var state = session.State;
        var player = Player(session);
        var playerType = rules.TypeByQualifiedId(PlayerType);
        var cardType = rules.TypeByQualifiedId(CardType);
        var monsterType = rules.TypeByQualifiedId(MonsterType);
        var hand = Collection(session, HandPile);
        var draw = Collection(session, DrawPile);
        var discard = Collection(session, DiscardPile);

        // 先把起手全部退回抽牌堆：受控状态的手牌只由夹具声明决定，不掺入抽取顺序。
        foreach (var card in state.CollectionMembers(hand).ToList())
        {
            state.RunAtomic(() =>
            {
                state.RemoveFromCollection(hand, card);
                state.AddToCollection(draw, card);
            });
        }

        foreach (var declared in setup.Hand ?? [])
        {
            Place(session, cardType, declared, draw, hand);
        }

        foreach (var declared in setup.Discard ?? [])
        {
            Place(session, cardType, declared, draw, discard);
        }

        if (setup.PlayerHp is { } hp)
        {
            state.RunAtomic(() =>
            {
                state.SetField(player, playerType.FieldIndex("Hp"), Value.Of((long)hp));
                state.SetField(player, playerType.FieldIndex("MaxHp"), Value.Of((long)hp));
            });
        }

        if (setup.Energy is { } energy)
        {
            state.RunAtomic(() => state.SetField(player, playerType.FieldIndex("Energy"), Value.Of((long)energy)));
        }

        if (setup.EnemyHp is { } enemyHp)
        {
            var monsters = state.AllInstances(monsterType.Id);
            if (enemyHp.Count != monsters.Count)
            {
                throw new CheckFailed(
                    $"enemy_hp 给了 {enemyHp.Count} 个数值，但场景里有 {monsters.Count} 个敌人——受控状态必须逐个指定");
            }

            state.RunAtomic(() =>
            {
                for (var index = 0; index < monsters.Count; index++)
                {
                    state.SetField(monsters[index], monsterType.FieldIndex("Hp"), Value.Of((long)enemyHp[index]));
                    state.SetField(monsters[index], monsterType.FieldIndex("MaxHp"), Value.Of((long)enemyHp[index]));
                }
            });
        }

        foreach (var declared in setup.Powers ?? [])
        {
            GivePower(session, Resolve(declared.Owner, session), declared.Power, declared.Stacks);
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Session, Dictionary<InstanceId, string>> InitSlots = new();
    private static void ApplyOrdered(Session session, InitialPiles init)
    {
        var keys = new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile" };
        var declarations = new[] { init.Hand!, init.Draw!, init.Discard!, init.Exhaust! };
        var collections = keys.Select(key => session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + key).Id).ToArray();
        var available = collections.SelectMany(c => session.State.CollectionMembers(c)).ToList();
        var slots = new Dictionary<InstanceId, string>();
        session.State.RunAtomic(() =>
        {
            foreach (var collection in collections)
                foreach (var card in session.State.CollectionMembers(collection).ToArray()) session.State.RemoveFromCollection(collection, card);
            for (var p = 0; p < declarations.Length; p++)
                foreach (var declared in declarations[p])
                {
                    var type = session.RuleSet.TypeByQualifiedId(CardType);
                    var card = available.FirstOrDefault(c => Definition(session, c) == declared.Card
                        && session.State.GetField(c, type.FieldIndex("Upgraded")).AsBool() == declared.Upgraded);
                    if (card == default && declared.Upgraded)
                    {
                        card = available.FirstOrDefault(c => Definition(session, c) == declared.Card);
                        if (card != default)
                        {
                            var definition = SliceManifest.Card(declared.Card);
                            session.State.SetField(card, type.FieldIndex("Upgraded"), Value.Of(true));
                            session.State.SetField(card, type.FieldIndex("Amount"), Value.Of((long)definition.UpgradedAmount));
                            session.State.SetField(card, type.FieldIndex("Cost"), Value.Of((long)definition.EffectiveUpgradedCost));
                            session.State.SetField(card, type.FieldIndex("PowerStacks"), Value.Of((long)definition.UpgradedPowerStacks));
                        }
                    }
                    if (card == default) throw new CheckFailed("独立 base 来源牌缺失: " + declared.Card);
                    available.Remove(card);
                    session.State.AddToCollection(collections[p], card);
                    slots.Add(card, declared.Slot);
                }
            if (available.Count != 0) throw new CheckFailed("独立 base 来源有未声明牌");
        });
        InitSlots.Add(session, slots);
    }

    private static void Place(Session session, EntityTypeDefinition cardType, FixtureCard declared,
        int from, int to)
    {
        var state = session.State;
        var card = state.CollectionMembers(from)
            .FirstOrDefault(candidate => Definition(session, candidate) == declared.Card);
        if (card == default)
        {
            throw new CheckFailed($"来源牌区里没有可用的 '{declared.Card}'");
        }

        state.RunAtomic(() =>
        {
            // 牌区共用一个 membership group：必须先离开来源再加入目标（否则报 MembershipConflict）。
            state.RemoveFromCollection(from, card);
            state.AddToCollection(to, card);
        });

        state.RunAtomic(() =>
        {
            if (declared.Upgraded)
            {
                state.SetField(card, cardType.FieldIndex("Upgraded"), Value.Of(true));
            }

            if (declared.Amount is { } amount)
            {
                state.SetField(card, cardType.FieldIndex("Amount"), Value.Of((long)amount));
            }

            if (declared.PowerStacks is { } stacks)
            {
                state.SetField(card, cardType.FieldIndex("PowerStacks"), Value.Of((long)stacks));
            }

            if (declared.Cost is { } cost)
            {
                state.SetField(card, cardType.FieldIndex("Cost"), Value.Of((long)cost));
            }
        });
    }

    /// <summary>声明一个能力实例（与包内 ApplyPower 造出的形状一致）。</summary>
    private static void GivePower(Session session, InstanceId owner, string powerId, long stacks)
    {
        var rules = session.RuleSet;
        var state = session.State;
        var powerType = rules.TypeByQualifiedId(PowerType);
        var relation = rules.Relations.Single(entry => entry.QualifiedId == PowersRelation);
        state.RunAtomic(() =>
        {
            var instance = state.CreateInstance(powerType.Id, state.ScopeOf(owner));
            state.SetField(instance, powerType.FieldIndex("DefinitionId"), Value.Of(powerId));
            state.SetField(instance, powerType.FieldIndex("Stacks"), Value.Of((long)stacks));
            var declared = SliceAuthorFrontend.Load().Powers.SingleOrDefault(power => power.Id == powerId)
                ?? throw new CheckFailed($"受控 Power '{powerId}' 没有共同作者声明");
            state.SetField(instance, powerType.FieldIndex("Ticks"),
                Value.Of(declared.OwnerTurnEnd == PowerTurnEnd.Tick));
            state.SetField(instance, powerType.FieldIndex("SkipNextTick"), Value.Of(false));
            state.AddRelation(relation.Id, owner, instance);
        });
    }

    // -- 脚本执行 -------------------------------------------------------------------------------------------

    private static void Execute(Session session, ParticipantId participant, FixtureStep step,
        List<DecisionSnapshot> decisions)
    {
        switch (step.Action)
        {
            case "play_card":
            {
                var card = Hand(session, step.Card!);
                var action = Legal(session, participant, PlayCardAction);
                session.Submit(action.Definition, participant, [Value.OfRef(card)]);

                // 一次出牌可能产生多个逻辑决策点（先选目标、再选牌…）：按顺序用夹具给的选择回答，
                // 每个决策点都用**回答之前**的候选集记录下来。
                var answerIndex = 0;
                while (session.Status == SessionStatus.AwaitingDecision)
                {
                    var described = EngineSnapshot.DescribeDecision(session, participant)
                        ?? throw new CheckFailed("处于待决策状态却读不到决策点");
                    var chosen = Resolve(Choice(step, answerIndex), session, session.PendingDecision!.Candidates);
                    decisions.Add(described with { Chosen = SlotFor(session, chosen) });
                    session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, [chosen]));
                    answerIndex++;
                }

                break;
            }

            case "end_turn":
                session.Submit(Legal(session, participant, EndTurnAction).Definition, participant, []);
                break;

            default:
                throw new CheckFailed($"未知动作 '{step.Action}'");
        }
    }

    /// <summary>第 n 个决策点的选择：0 号用 <c>target</c>（没给就取候选首项），之后用 <c>choose</c>（没给就取首项）。</summary>
    private static string Choice(FixtureStep step, int index) => index switch
    {
        0 => step.Target ?? step.Choose ?? "first",
        _ => step.Choose ?? "first",
    };

    private static string SlotFor(Session session, InstanceId instance)
    {
        foreach (var (slot, candidate) in EngineSnapshot.SlotMap(session))
        {
            if (candidate == instance)
            {
                return slot;
            }
        }

        return $"unmapped:{instance.Value}";
    }

    private static InstanceId Resolve(string token, Session session, IReadOnlyList<InstanceId>? candidates = null)
    {
        if (token == "first")
        {
            var list = candidates ?? [];
            if (list.Count == 0)
            {
                throw new CheckFailed("'first' 需要至少一个候选");
            }

            return list[0];
        }

        if (!EngineSnapshot.SlotMap(session).TryGetValue(token, out var instance))
        {
            throw new CheckFailed($"无法解析的候选 '{token}'（槽位名如 player / enemy:0 / card:discard:0，或 first）");
        }

        if (candidates is not null && !candidates.Contains(instance))
        {
            throw new CheckFailed($"候选 '{token}' 不在当前决策的候选集合里");
        }

        return instance;
    }

    // -- 小工具 ---------------------------------------------------------------------------------------------

    internal static InstanceId Player(Session session) =>
        session.State.AllInstances(session.RuleSet.TypeByQualifiedId(PlayerType).Id).Single();

    internal static int Collection(Session session, string name) =>
        session.RuleSet.Collections.Single(item => item.QualifiedId == name).Id;

    private static LegalAction Legal(Session session, ParticipantId participant, string actionId) =>
        session.LegalActions(participant)
            .SingleOrDefault(action => action.Definition.QualifiedId == actionId)
        ?? throw new CheckFailed($"当前没有合法动作 '{actionId}'（对手/阶段不允许？）");

    private static string Definition(Session session, InstanceId instance) =>
        session.State.GetField(instance, session.RuleSet.TypeByQualifiedId(CardType).FieldIndex("DefinitionId"))
            .AsString();

    private static InstanceId Hand(Session session, string cardId)
    {
        var found = session.State.CollectionMembers(Collection(session, HandPile))
            .FirstOrDefault(candidate => Definition(session, candidate) == cardId);
        if (found == default)
        {
            throw new CheckFailed($"手牌里没有 '{cardId}'（受控手牌只由夹具 setup.hand 决定）");
        }

        return found;
    }

    private static string DescribeStep(FixtureStep step) => step.Action switch
    {
        "play_card" => $"打出 {step.Card}" + (step.Target is { } target ? $" → {target}" : string.Empty),
        "end_turn" => "结束回合",
        _ => step.Action,
    };

    private static IReadOnlyList<FactSnapshot> OrderedFacts(Session session, IReadOnlyList<FactSnapshot> facts)
    {
        if (!InitSlots.TryGetValue(session, out var slots)) return facts;
        var additions = new List<FactSnapshot>();
        foreach (var (name, key) in new[] { ("Hand", "hand"), ("DrawPile", "draw"), ("DiscardPile", "discard"), ("ExhaustPile", "exhaust") })
        {
            var collection = session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name);
            additions.Add(new FactSnapshot("pile." + key + ".initial-slots", string.Join(",", session.State.CollectionMembers(collection.Id).Select(c => slots.GetValueOrDefault(c, "unknown")))));
        }
        return Records.Facts(facts.Concat(additions));
    }

    private static void Add(List<BoundaryRecord> boundaries, string kind, string label, Session session,
        ParticipantId participant, IReadOnlyList<DecisionSnapshot> decisions, bool exactPileOrder,
        bool observeDrawTop = false) =>
        boundaries.Add(new BoundaryRecord(
            boundaries.Count,
            kind,
            label,
            EngineSnapshot.LegalActions(session, participant),
            decisions,
            OrderedFacts(session, EngineSnapshot.Facts(session, exactPileOrder, observeDrawTop)),
            EngineSnapshot.Streams(session),
            EngineSnapshot.Capture(session)));
}

/// <summary>夹具/脚本层面的可读失败（区别于引擎内部失败）。</summary>
internal sealed class CheckFailed(string message) : Exception(message);
