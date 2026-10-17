using BaseSpire.SinglePlayer;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Host;
using NetTheSpire.Projection;

namespace NetTheSpire.Tests;

/// <summary>
/// ENV-T03 正常正式 Host 子包的检查：**真实 base dev 工厂 + 派生公开 Profile + 正式控制宿主**。
///
/// 与 <c>AcceptanceChecks</c>（旧 0.1 切片场景）的区别：这里的规则集来自唯一的正式工厂
/// （<see cref="BaseSpireRuntimeFactory"/>），会话由提供方建、控制入口只启动一次，公开目录由引擎声明派生
/// （不是 <c>SlicePublicProfile</c> 的逐项声明表），协议是 0.2 的**每局游戏字符串 seed**。
///
/// 这些检查只声明**能在本轮输入上核对**的事实：正式工厂的 dev 范围、0.2 身份/seed 规则、会话生命周期、
/// 幂等与拒绝、以及"公开候选真的驱动了一局"。它们**不**代表「只改规则包新增字段/Action」的扩展证据
/// （那仍是 ENV-T03 原合同的后续子包），也不代表完整胜负或双侧对拍。
/// </summary>
internal static class FormalPathChecks
{
    private const string MilestoneSeed = "ENV-IRONCLAD-00";

    /// <summary>第二局的每局 seed：与里程碑 seed 不同，用来核对"同 Host 两 seed"与身份不含 seed。</summary>
    private const string SecondSeed = "ENV-T03-C-02";

    public static IEnumerable<(string Name, Action Body)> Build()
    {
        yield return ("F-01 正式 dev 场景：0.2、范围 id、实际 RNG Profile，且静态身份不含每局 seed [真实]",
            FormalIdentity);
        yield return ("F-02 接管未 Start 会话：0 随机数消费、Start 恰好一次、两局互不污染 [真实]",
            ProvidedSessionLifecycle);
        yield return ("F-03 start 结果恰为 {session_id,seed,snapshot}：每局字符串 seed 与实际接受值 [真实]",
            SeedRoundTrip);
        yield return ("F-04 重发幂等、换载荷 conflict、错误身份/场景/目录在创建之前拒绝 [真实]",
            DedupAndPreStartRejection);
        yield return ("F-05 公开链路真跑：observe → step 出牌/结束回合；跨会话与过期绑定被拒；无注入 [真实]",
            PublicTrajectory);
        yield return ("F-06 真实 0.2 载荷通过 docs/contracts/net/0.2/wire.schema.json（C# 读同一份 schema） [真实]",
            SchemaConformance);
        yield return ("F-07 公开 sequence 如实声明：目录/describe 与实际发布一致，owner 按公开种类跨 seed 唯一解析 [真实]",
            PublicSequencesDeclaredAndOwned);
        yield return ("F-08 PlayedThisTurn 真实成员与顺序与 ParticipantView 逐位一致（反转/丢成员即失败） [真实]",
            PublicSequenceOrderAndMembers);
        yield return ("F-09 owner 缺失/歧义显式拒绝，旧固定 owner_ref 路径保留 [真实]",
            SequenceOwnerRejectionAndLegacyRef);
        yield return ("G-01 dev-map 范围选择入口：dev 缺省身份逐字不变、dev-map 进身份、未知范围显式拒绝 [真实]",
            MapScopeSelection);
        yield return ("G-02 dev-map 公开目录：MapNode/MapNodes/MapChildren/ChooseMapPoint 全部由引擎声明派生 [真实]",
            MapDeclarationsDerivedFromEngine);
        yield return ("G-03 dev-map 开局：MapPhase/MapNodes/CurrentNode 公开，ChooseMapPoint 候选恰为公开后继 [真实]",
            MapInitialCandidates);
        yield return ("G-06 dev-run 受控幕结束：公开切幕保留长期资源，旧句柄退出、重发幂等，古代人缺口明确拒绝",
            MultiActPublicBoundary);
        yield return ("Sown public: actual Plant choice, attachment fields, replay and subsequent combat copy [controlled visit]",
            SownPublicVisit);
        yield return ("G-04 两 seed 合法选 Monster 进既有战斗；幂等重发、非法/过期拒绝且抽数与状态不变 [真实]",
            MapChooseAndRejections);
        yield return ("G-05 dev-map 载荷同批通过 0.2 wire schema；出牌要么正常结算要么显式 base 故障 [真实]",
            MapPayloadSchemaAndBattleStep);
        yield return ("G-06 dev-map describe 的 supported_structures 声明 ID 与同一 get_catalog 闭合（I-71/I-84） [真实]",
            MapDescriptionsCloseAgainstCatalog);
    }

    // -- F-01 ------------------------------------------------------------------------------------------------

    private static void FormalIdentity()
    {
        var factory = FormalDevScenario.CreateFactory();
        var build = factory.Describe();
        var host = NewHost(factory, out var metadata);

        Check.Equal(NetProtocol.V02, host.ProtocolVersion, "正式路径的协议版本是 0.2");
        Check.Equal(build.ScopeId, host.SupportedScenarioProfile,
            "场景 id 是所选构建的**范围身份**，不是旧切片 id");
        Check.Equal(BaseSpireScope.Dev, host.SupportedScenarioProfile,
            "当前唯一可实现的正式范围是 dev（未闭合范围不能声称完整）");

        var described = host.Dispatch(Req("f01-describe", Operation.Describe));
        Check.True(described.Ok, $"describe 应当成功：{described.Error?.Code}");
        var capabilities = CapabilitiesDto.FromWire(Field(described, "capabilities"));
        Check.True(capabilities.SeedControl, "正式路径声明 seed_control=true（它真的使用请求给的每局 seed）");
        Check.Equal(1, capabilities.ScenarioProfiles.Count, "只声明实际支持的场景");
        Check.Equal(build.ScopeId, capabilities.ScenarioProfiles[0].Id, "describe 的场景 id 与 start 用同一个");
        Check.Equal(build.RandomDrawProfileId, capabilities.ExecutionGuarantees.RngProfileId,
            "execution_guarantees.rng_profile_id 必须报告**实际**随机映射 Profile（不是 net 的默认常量）");
        Check.False(capabilities.Operations.Contains("attach", StringComparer.Ordinal),
            "模拟后端不声明 attach");

        // 静态身份不含每局 seed：同配置两次构造、以及不同 seed 的两局，公开身份逐字相同。
        var again = NewHost(FormalDevScenario.CreateFactory(), out _);
        Check.Equal(host.Profile.RulesetId, again.Profile.RulesetId, "同一 config 的 ruleset_id 必须相同");
        Check.Equal(host.Profile.CatalogId, again.Profile.CatalogId, "同一 config 的 catalog_id 必须相同");
        Check.Equal(host.Profile.BackendProfileId, again.Profile.BackendProfileId,
            "同一 config 的 backend_profile_id 必须相同");

        var first = Start(host, "f01-a", MilestoneSeed);
        var second = Start(host, "f01-b", SecondSeed);
        Check.True(first.Ok && second.Ok, "两局都应成功");
        Check.Equal(MilestoneSeed, StringField(first, "seed"), "第一局回报实际接受的 seed");
        Check.Equal(SecondSeed, StringField(second, "seed"), "第二局 seed（每局不同，静态身份不变）");
        Check.True(StringField(first, "session_id") != StringField(second, "session_id"),
            "两局必须是不同会话（各自的 seed 不互相污染）");
        Check.Equal(host.Profile.RulesetId, again.Profile.RulesetId, "两局之后静态身份仍然不变");

        // 身份文本里不能出现任何一局的 seed（它们只是控制元数据 / 会话证据）。
        foreach (var identityPart in new[]
                 {
                     host.Profile.RulesetId, host.Profile.CatalogId, host.Profile.BackendProfileId,
                     host.SupportedScenarioProfile, build.RulesetId, build.PublicBuildId,
                     metadata.StaticBuildIdentity,
                 })
        {
            Check.False(identityPart.Contains(MilestoneSeed, StringComparison.Ordinal),
                $"静态身份不得含每局 seed：{identityPart}");
            Check.False(identityPart.Contains(SecondSeed, StringComparison.Ordinal),
                $"静态身份不得含每局 seed：{identityPart}");
        }
    }

    // -- F-02 ------------------------------------------------------------------------------------------------

    private static void ProvidedSessionLifecycle()
    {
        var factory = FormalDevScenario.CreateFactory();
        var host = NewHost(factory, out _);
        var source = host.SourceForTests;
        Check.True(source is not null, "正式路径必须由规则提供方交出会话来源（不是自建默认会话）");

        // 提供方建出来的会话必须是「已配置、尚未 Start」：状态在跑、随机流种好但一个随机数都没消费。
        var provided = source!.CreateSession(MilestoneSeed);
        Check.Equal(SessionStatus.Running, provided.Session.Status, "尚未 Start");
        Check.Equal(0L, provided.Session.Random.TotalDraws, "配置阶段不消费随机数");
        var playerType = provided.Session.RuleSet.TypeByQualifiedId("base-spire:Player");
        Check.Equal(0, provided.Session.State.AllInstances(playerType.Id).Count,
            "尚未 Start 时没有任何玩家实例：不存在建了一半的开局");
        Check.Equal(MilestoneSeed, provided.AcceptedSeed, "实际接受值原样回报（不 trim/不改大小写）");
        Check.Equal(BaseSpireRuntimeFactory.ProjectionId, provided.ProjectionId, "所选公开投影来自工厂");

        // 控制入口启动**恰好一次**：start 之后 RNG 才真的被消费。
        var started = Start(host, "f02-a", MilestoneSeed);
        Check.True(started.Ok, $"start 应当成功：{started.Error?.Code}");
        var sessionId = StringField(started, "session_id")!;
        var session = host.GetSessionForTests(sessionId);
        Check.True(session.EngineForTests.Random.TotalDraws > 0,
            "start 之后引擎才真的开始抽数（开局程序执行过）");
        Check.Equal(1, host.SessionCount, "一次 start 只建一个会话");

        // 同一会话不得启动两次：这是契约的显式失败，不是"再跑一遍开局"。
        RejectsInvalidOperation(() => session.Start(StartRequest(host, "f02-again", MilestoneSeed)),
            "同一会话第二次 Start 必须显式失败");

        // 第二局与第一局互不污染：不同 seed → 不同会话、不同引擎实例。
        var other = Start(host, "f02-b", SecondSeed);
        Check.True(other.Ok, "第二局应当成功");
        Check.True(StringField(other, "session_id") != sessionId, "两局必须是不同的会话句柄");
        Check.Equal(2, host.SessionCount, "同 Host 可以开第二个 seed");
        Check.Equal(MilestoneSeed, session.AcceptedSeed, "第一局的接受值不被第二局改写");
        Check.Equal(SecondSeed, host.GetSessionForTests(StringField(other, "session_id")!).AcceptedSeed,
            "第二局有它自己的接受值");
    }

    // -- F-03 ------------------------------------------------------------------------------------------------

    private static void SeedRoundTrip()
    {
        var host = NewHost(FormalDevScenario.CreateFactory(), out _);

        // 带 seed：原样使用（base 不做大厅式标准化：不改大小写、不换字符）。
        var withSeed = Start(host, "f03-a", "MiXeD-Case-0");
        Check.True(withSeed.Ok, $"start 应当成功：{withSeed.Error?.Code}");
        Check.Equal("MiXeD-Case-0", StringField(withSeed, "seed"),
            "请求给的原始字符串就是实际接受值（不做 size/case 标准化）");

        // 不带 seed：用 Host 的显式默认游戏 seed，并回报实际值（请求里没有 seed 成员）。
        var noSeed = host.Dispatch(Req("f03-b", Operation.Start,
            ("expected_identity", Identity(host).ToWire()),
            ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile))));
        Check.True(noSeed.Ok, $"不带 seed 的 start 应当成功：{noSeed.Error?.Code}");
        Check.Equal(MilestoneSeed, StringField(noSeed, "seed"),
            "未带 seed 时使用 Host 的显式默认游戏 seed，并回报实际接受值");
        Check.True(StringField(noSeed, "session_id") != StringField(withSeed, "session_id"),
            "默认 seed 也开独立的一局");

        // 0.2 的字符串 "0" 是游戏字符串，不是旧的数值零种子。
        var zero = Start(host, "f03-c", "0");
        Check.True(zero.Ok, "字符串 \"0\" 在 0.2 是合法游戏 seed");
        Check.Equal("0", StringField(zero, "seed"), "字符串 \"0\" 原样回报，不特殊转换为 ulong");

        // 结果键集合**恰好**是 {session_id, seed, snapshot}（protocol.md §7；键按序比较，键集合固定）。
        var shape = Start(host, "f03-d", MilestoneSeed);
        var result = (WireObject)shape.Result!;
        Check.Sequence(["seed", "session_id", "snapshot"],
            result.Members.Select(member => member.Key).Order(StringComparer.Ordinal),
            "0.2 的 start 成功结果键集合固定");
        Check.True(result.Members.Single(m => m.Key == "snapshot").Value is WireObject,
            "结果里的 snapshot 是对象");
    }

    // -- F-04 ------------------------------------------------------------------------------------------------

    private static void DedupAndPreStartRejection()
    {
        var host = NewHost(FormalDevScenario.CreateFactory(), out _);
        var identity = Identity(host);

        // 错误**协议版本**：请求载荷按版本分支，所以它在解析层就被挡下（`invalid_input` + not_started），
        // 不自动降级、也不按字符串内容猜版本。模型/C# 内存调用与真实 wire 走**同一个**契约（不出现两种错误码）。
        var wrongVersion = host.Dispatch(Req("f04-bad-version", Operation.Start,
            ("expected_identity", new IdentityDto(NetProtocol.V01, identity.RulesetId, identity.CatalogId,
                identity.BackendProfileId).ToWire()),
            ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)),
            ("seed", WireValue.Str(MilestoneSeed))));
        Check.Equal("invalid_input", ErrorCode(wrongVersion), "错协议版本必须拒绝，不按内容猜版本");
        Check.Equal("not_started", wrongVersion.Error!.ExecutionState, "拒绝发生在推进之前");
        Check.Equal(0, host.SessionCount, "版本不符不得创建会话");

        // 同版本下的**身份不符**（错目录）走 identity 比较 → `version_mismatch`（protocol.md §6 第 213 行）。
        var wrongCatalog = host.Dispatch(Req("f04-bad-catalog", Operation.Start,
            ("expected_identity", new IdentityDto(identity.ProtocolVersion, identity.RulesetId,
                "fixture:other-catalog", identity.BackendProfileId).ToWire()),
            ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)),
            ("seed", WireValue.Str(MilestoneSeed))));
        Check.Equal("version_mismatch", ErrorCode(wrongCatalog), "错目录身份必须 version_mismatch");
        Check.Equal("not_started", wrongCatalog.Error!.ExecutionState, "身份不符必须发生在创建之前");
        Check.Equal(0, host.SessionCount, "身份不符不得创建会话");

        // 旧切片场景 id 不能冒充正式构建。
        var wrongScenario = host.Dispatch(Req("f04-bad-scenario", Operation.Start,
            ("expected_identity", identity.ToWire()),
            ("scenario_profile", WireValue.Str("base-spire-combat-slice-v1")),
            ("seed", WireValue.Str(MilestoneSeed))));
        Check.Equal("unsupported_capability", ErrorCode(wrongScenario), "旧切片 id 必须被拒，不自动降级");
        Check.Equal(0, host.SessionCount, "场景不符不得创建会话");

        // seed 形状按**协议版本**在请求层校验（I-76）：0.2 要求非空游戏字符串。
        Check.Fault(
            () => RequestDto.FromWire(StartDocument(identity, host.SupportedScenarioProfile, string.Empty),
                NetProtocol.V02),
            "0.2 的空 seed 必须在请求层被拒", "I-76");
        Check.Fault(
            () => RequestDto.FromWire(
                StartDocumentWithSeedNode(identity, host.SupportedScenarioProfile, WireValue.Integer(5)),
                NetProtocol.V02),
            "0.2 的非字符串 seed 必须在请求层被拒", "I-76");
        Check.Accept(
            () => RequestDto.FromWire(StartDocument(identity, host.SupportedScenarioProfile, MilestoneSeed),
                NetProtocol.V02),
            "0.2 的游戏字符串 seed 必须在请求层被接受");
        // 拿错版本校验时，**身份版本**先被挡下（I-66）：这正是"入口必须按运行时版本解析"的理由。
        Check.Fault(
            () => RequestDto.FromWire(StartDocument(identity, host.SupportedScenarioProfile, MilestoneSeed),
                NetProtocol.V01),
            "0.2 身份在 0.1 解析路径下必须被挡下（版本各自精确相等）", "I-66");
        Check.Equal(0, host.SessionCount, "非法 seed 不得创建会话");

        // 原样重发：返回**同一局**（同一 session/seed/snapshot），不启动第二局。
        var request = StartRequest(host, "f04-resend", MilestoneSeed);
        var firstResponse = host.Dispatch(request);
        Check.True(firstResponse.Ok, $"start 应当成功：{firstResponse.Error?.Code}");
        var replayed = host.Dispatch(StartRequest(host, "f04-resend", MilestoneSeed));
        Check.Equal(WireCanonical.Text(firstResponse.ToWire()), WireCanonical.Text(replayed.ToWire()),
            "同 request_id 同载荷重发必须逐字返回原结果");
        Check.Equal(1, host.SessionCount, "重发不得启动第二局");

        // 同 request_id 换载荷 → request_conflict（不得换载荷重用）。
        var conflict = host.Dispatch(StartRequest(host, "f04-resend", SecondSeed));
        Check.Equal("request_conflict", ErrorCode(conflict), "同 request_id 换载荷必须 conflict");
        Check.Equal(1, host.SessionCount, "冲突请求不得再建会话");
    }

    // -- F-05 ------------------------------------------------------------------------------------------------

    private static void PublicTrajectory()
    {
        var factory = FormalDevScenario.CreateFactory();
        var build = factory.Describe();
        var host = NewHost(factory, out _);
        var first = Start(host, "f05-a", MilestoneSeed);
        var second = Start(host, "f05-b", SecondSeed);
        var sessionId = StringField(first, "session_id")!;
        var otherId = StringField(second, "session_id")!;
        var session = host.GetSessionForTests(sessionId);

        var observed = Observe(host, sessionId);
        Check.True(observed.Ok, $"observe 应当成功：{observed.Error?.Code}");
        var snapshot = (WireObject)Field(observed, "snapshot")!;
        var actions = LegalActions(snapshot);
        Check.True(actions.Count > 0, "开局稳定边界必须有公开候选（否则无法操作）");
        Check.Equal("decision", ((WireString)snapshot.Members.Single(m => m.Key == "status").Value).Value,
            "开局是等待决策的稳定边界");

        // 公开事实不得携带每局 seed（它是控制元数据，不是游戏事实）。
        var text = WireCanonical.Text(snapshot);
        Check.False(text.Contains(MilestoneSeed, StringComparison.Ordinal), "公开快照不得含每局 seed");

        // 「不改血/清场/注入状态」：开局公开生命必须等于构建声明的起始生命。
        Check.Equal(build.StartingHp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            PublicPlayerHp(session, snapshot), "开局公开生命 = 构建声明的起始生命（没有改血/清场）");

        // 过期观察 / 跨会话候选都必须在推进前拒绝。
        var otherObserved = Observe(host, otherId);
        var otherSession = host.GetSessionForTests(otherId);
        var otherVersionBefore = otherSession.EngineForTests.StateVersion;
        var observationId = ObservationId(snapshot);
        var candidateId = CandidateId(actions[0]);
        var versionBefore = session.EngineForTests.StateVersion;

        var crossSession = host.Dispatch(Req("f05-cross", Operation.Step,
            ("session_id", WireValue.Str(otherId)),
            ("expected_observation_id", WireValue.Integer(observationId)),
            ("candidate_id", WireValue.Str(candidateId))));
        Check.True(ErrorCode(crossSession) is "stale_observation" or "illegal_candidate",
            $"另一会话的候选必须被拒：{ErrorCode(crossSession)}");
        Check.Equal(otherVersionBefore, otherSession.EngineForTests.StateVersion,
            "跨会话拒绝不得推进那一局的状态");

        var stale = host.Dispatch(Req("f05-stale", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(observationId + 1000)),
            ("candidate_id", WireValue.Str(candidateId))));
        Check.Equal("stale_observation", ErrorCode(stale), "过期 observation_id 必须拒绝");
        Check.Equal(versionBefore, session.EngineForTests.StateVersion, "被拒的 step 不得推进状态");

        // 真的执行一条公开动作：优先出牌，否则结束回合（都是公开候选）。
        var play = actions.FirstOrDefault(action => ActionType(action).EndsWith("PlayCard", StringComparison.Ordinal));
        var chosen = play ?? actions.First(action =>
            ActionType(action).EndsWith("EndTurn", StringComparison.Ordinal));
        var hpBefore = PublicPlayerHp(session, snapshot);
        var stepResponse = host.Dispatch(Req("f05-step", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(observationId)),
            ("candidate_id", WireValue.Str(CandidateId(chosen)))));
        Check.True(stepResponse.Ok, $"step 应当成功：{stepResponse.Error?.Code} {stepResponse.Error?.Message}");
        Check.True(session.EngineForTests.StateVersion > versionBefore, "公开 step 真的推进了引擎状态");

        var afterSnapshot = (WireObject)Field(Observe(host, sessionId), "snapshot")!;
        Check.True(ObservationId(afterSnapshot) > observationId, "推进后必须给出新的观察版本");
        if (play is not null)
        {
            // 出牌路径不碰玩家自己的生命：公开生命必须原样（证明轨迹没有靠改血/清场制造）。
            Check.Equal(hpBefore, PublicPlayerHp(session, afterSnapshot),
                "出牌本身不改写玩家生命（没有通过工具改血清场）");
        }

        // 出牌后旧观察版本不可再用（候选不跨稳定边界复用）。
        var replayOld = host.Dispatch(Req("f05-replay-old", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(observationId)),
            ("candidate_id", WireValue.Str(CandidateId(chosen)))));
        Check.Equal("stale_observation", ErrorCode(replayOld), "推进后旧观察版本必须过期");

        // 结束回合同样是一条真实公开动作（本轮至少证明一条；这里两条路径都能跑）。
        var endTurn = LegalActions(afterSnapshot)
            .FirstOrDefault(action => ActionType(action).EndsWith("EndTurn", StringComparison.Ordinal));
        if (endTurn is not null)
        {
            var ended = host.Dispatch(Req("f05-end", Operation.Step,
                ("session_id", WireValue.Str(sessionId)),
                ("expected_observation_id", WireValue.Integer(ObservationId(afterSnapshot))),
                ("candidate_id", WireValue.Str(CandidateId(endTurn)))));
            Check.True(ended.Ok, $"结束回合应当成功：{ended.Error?.Code} {ended.Error?.Message}");
        }

        Console.WriteLine($"      F-05 轨迹：seed={MilestoneSeed} 动作={ActionType(chosen)} "
            + $"会话={sessionId} 观察={observationId}→{ObservationId(afterSnapshot)}");

        CrossSessionCandidateBinding();
    }

    /// <summary>
    /// T03 跨会话候选绑定（同 seed 判别用例）：两局**同一 seed** 时开局观察版本与动作语义逐条重合，
    /// 因此 candidate_id 是否绑定实际会话是可判别的——只按动作语义摘要派生句柄的实现会在这里被抓住。
    ///
    /// 判别点：① 两局语义键逐条一致（训练侧仍可跨会话对齐）；② candidate_id 无交集（提交引用绑定会话）；
    /// ③ 外局候选提交必须拒绝且**不推进**目标会话；④ 目标会话自己的候选必须正常成功。
    /// </summary>
    private static void CrossSessionCandidateBinding()
    {
        var host = NewHost(FormalDevScenario.CreateFactory(), out _);
        var first = Start(host, "f05-same-a", MilestoneSeed);
        var second = Start(host, "f05-same-b", MilestoneSeed);
        Check.True(first.Ok && second.Ok, "同 seed 两局都应成功");
        var firstId = StringField(first, "session_id")!;
        var secondId = StringField(second, "session_id")!;
        Check.True(firstId != secondId, "同 seed 也必须是两个不同会话句柄");

        var firstObserve = Observe(host, firstId);
        var secondObserve = Observe(host, secondId);
        var firstSnapshot = (WireObject)Field(firstObserve, "snapshot")!;
        var secondSnapshot = (WireObject)Field(secondObserve, "snapshot")!;
        var firstActions = LegalActions(firstSnapshot);
        var secondActions = LegalActions(secondSnapshot);
        Check.Equal(ObservationId(firstSnapshot), ObservationId(secondSnapshot),
            "同 seed 两局的稳定观察版本必须相同（候选绑定的判别前提）");
        Check.Equal(firstActions.Count, secondActions.Count, "同 seed 两局的候选数必须相同");

        // ① 语义键逐条一致：从公开 {action_type, fields} 算规范摘要（candidate_id 不参与）。
        var firstSemantics = firstActions.Select(SemanticDigest).Order(StringComparer.Ordinal).ToList();
        var secondSemantics = secondActions.Select(SemanticDigest).Order(StringComparer.Ordinal).ToList();
        Check.Sequence(firstSemantics, secondSemantics,
            "同 seed 两局的动作语义键必须逐条一致（跨会话可对齐，不把会话标识混进语义）");

        // ② candidate_id 无交集：提交引用绑定实际会话，不得跨局复用。
        var firstIds = firstActions.Select(CandidateId).ToHashSet(StringComparer.Ordinal);
        var secondIds = secondActions.Select(CandidateId).ToHashSet(StringComparer.Ordinal);
        Check.True(!firstIds.Overlaps(secondIds),
            $"同 seed 两局的 candidate_id 不得有交集（实际 {firstIds.Intersect(secondIds).Count()} 个重合）："
            + "否则第一局的候选会被第二局当成自己的候选接受");

        // ③ 外局候选：拒绝且不推进目标会话（用与 ④ 不同的候选，避免只验证"某个特定候选"）。
        var foreign = firstActions.First(action =>
            !secondIds.Contains(CandidateId(action)) && ActionType(action).EndsWith("EndTurn", StringComparison.Ordinal));
        var foreignTarget = host.GetSessionForTests(secondId);
        var foreignVersionBefore = foreignTarget.EngineForTests.StateVersion;
        var foreignObservation = ObservationId(secondSnapshot);
        var crossStep = host.Dispatch(Req("f05-same-cross", Operation.Step,
            ("session_id", WireValue.Str(secondId)),
            ("expected_observation_id", WireValue.Integer(foreignObservation)),
            ("candidate_id", WireValue.Str(CandidateId(foreign)))));
        Check.Equal("illegal_candidate", ErrorCode(crossStep),
            "同 seed 下外局候选必须报 illegal_candidate（候选不是本会话发布的提交引用）");
        Check.Equal("not_started", crossStep.Error!.ExecutionState, "拒绝必须发生在推进之前");
        Check.Equal(foreignVersionBefore, foreignTarget.EngineForTests.StateVersion,
            "外局候选被拒不得推进目标会话");
        Check.Equal(foreignObservation, ObservationId((WireObject)Field(Observe(host, secondId), "snapshot")!),
            "外局候选被拒不得改变目标会话的观察版本");

        // ④ 目标会话自己的候选必须正常成功（同一边界、同一语义，唯一区别是句柄属于本会话）。
        var own = secondActions.First(action => ActionType(action).EndsWith("EndTurn", StringComparison.Ordinal));
        var ownStep = host.Dispatch(Req("f05-same-own", Operation.Step,
            ("session_id", WireValue.Str(secondId)),
            ("expected_observation_id", WireValue.Integer(foreignObservation)),
            ("candidate_id", WireValue.Str(CandidateId(own)))));
        Check.True(ownStep.Ok, $"本会话自己的候选必须成功：{ownStep.Error?.Code} {ownStep.Error?.Message}");
        Check.True(foreignTarget.EngineForTests.StateVersion > foreignVersionBefore,
            "本会话候选真的推进了引擎状态");

        Console.WriteLine($"      F-05 跨会话绑定（同 seed）：观察={foreignObservation} 语义键={firstSemantics.Count} 条一致；"
            + $"candidate_id 交集 0；外局拒绝 code={ErrorCode(crossStep)}");
    }

    /// <summary>公开候选的动作语义键摘要：规范序列化 {action_type, fields} 的 UTF-8 文本摘要（不读 candidate_id）。</summary>
    private static string SemanticDigest(WireObject action) =>
        WireCanonical.Digest(WireValue.Object(
            ("action_type", Member(action, "action_type")),
            ("fields", Member(action, "fields"))));

    // -- F-06 ------------------------------------------------------------------------------------------------

    /// <summary>
    /// 真实正式路径产出的每一类载荷都必须通过 **0.2** 机器 schema（C# 端直接读同一份文件，
    /// 不另造形状；Python 侧另有 `net-the-spire/tools/emit_wire_schema_0_2.py --check` 保证它由 0.1 正确派生）。
    /// </summary>
    private static void SchemaConformance()
    {
        var schemaPath = Path.Combine(RepositoryRoot(), "docs", "contracts", "net", "0.2", "wire.schema.json");
        Check.True(File.Exists(schemaPath), $"缺少 0.2 wire schema：{schemaPath}");
        var schema = LiteSchema.FromJson(File.ReadAllText(schemaPath));

        var host = NewHost(FormalDevScenario.CreateFactory(), out _);
        var identity = Identity(host);
        var started = Start(host, "f06-seed", MilestoneSeed);
        var sessionId = StringField(started, "session_id")!;
        var snapshot = (WireObject)Field(started, "snapshot")!;
        var chosen = LegalActions(snapshot)
            .First(action => ActionType(action).EndsWith("PlayCard", StringComparison.Ordinal));
        var step = host.Dispatch(Req("f06-step", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(ObservationId(snapshot))),
            ("candidate_id", WireValue.Str(CandidateId(chosen)))));

        var documents = new List<(string Role, WireValue Document)>
        {
            ("describe/result", host.Dispatch(Req("f06-describe", Operation.Describe)).ToWire()),
            ("get_catalog/result", host.Dispatch(Req("f06-catalog", Operation.GetCatalog,
                ("catalog_id", WireValue.Str(host.Profile.CatalogId)))).ToWire()),
            ("start/result", started.ToWire()),
            ("observe/result", Observe(host, sessionId).ToWire()),
            ("step/result", step.ToWire()),
            ("error/not_started", host.Dispatch(Req("f06-bad", Operation.Start,
                ("expected_identity", new IdentityDto(NetProtocol.V01, identity.RulesetId, identity.CatalogId,
                    identity.BackendProfileId).ToWire()),
                ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)),
                ("seed", WireValue.Str(MilestoneSeed)))).ToWire()),
        };

        foreach (var (role, document) in documents)
        {
            var errors = schema.ValidateDefinition("Response", document);
            Check.True(errors.Count == 0,
                $"0.2 载荷违反 schema（{role}）：{string.Join(" | ", errors.Take(3))}");
        }
    }

    /// <summary>从自测试程序集位置向上找仓库根（以 <c>AutoTheSpire.sln</c> 为标记），与工作目录无关。</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AutoTheSpire.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new CheckFailedException($"找不到仓库根：自 '{AppContext.BaseDirectory}' 向上未发现 AutoTheSpire.sln");
    }

    // -- F-07 ------------------------------------------------------------------------------------------------

    /// <summary>
    /// 公开有序集合的**声明面与 owner 解析**：base 声明的每条 sequence 都出现在目录与 describe 里；owner 只按
    /// 公开种类声明，运行时在**该会话自己的绑定表**里唯一解析（不是写死的常量句柄），两个不同字符串 seed 都成立。
    /// </summary>
    private static void PublicSequencesDeclaredAndOwned()
    {
        var factory = FormalDevScenario.CreateFactory();
        var host = NewHost(factory, out var metadata);
        var declared = metadata.Collections
            .Where(pair => pair.Value.Use == PublicCollectionUse.Sequence)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();
        Check.True(declared.Count > 0,
            "本轮 base 元数据必须至少声明一条公开有序集合；一条也没有时本检查没有可核对的事实（不得静默通过）");

        // 目录：每条声明的 sequence 恰好一条 sequence_type，owner 只能是**公开种类**。
        var catalog = host.Dispatch(Req("f07-catalog", Operation.GetCatalog,
            ("catalog_id", WireValue.Str(host.Profile.CatalogId))));
        Check.True(catalog.Ok, $"get_catalog 应当成功：{catalog.Error?.Code}");
        var declarations = (WireObject)Field(catalog, "declarations");
        var sequenceTypes = ((WireArray)Member(declarations, "sequence_types")).Items.OfType<WireObject>().ToList();
        Check.Equal(declared.Count, sequenceTypes.Count,
            "目录 sequence_types 条数必须等于 base 声明的公开有序集合条数");
        foreach (var (collectionId, rule) in declared)
        {
            var typeId = DerivedPublicProfile.SequenceTypePrefix + collectionId;
            var entry = sequenceTypes.SingleOrDefault(item =>
                Member(item, "id") is WireString id && id.Value == typeId);
            Check.True(entry is not null, $"目录缺少 base 声明的 sequence_type '{typeId}'");
            Check.Equal(DerivedPublicProfile.KindPrefix + rule.OwnerKindEngineTypeId,
                Strings(Member(entry!, "owner_kind_ids")).Single(),
                $"sequence_type '{typeId}' 的 owner_kind_ids 必须是唯一的公开种类（不得写死实例句柄）");
            Check.True(Strings(Member(entry!, "element_kind_ids")).Any(),
                $"sequence_type '{typeId}' 必须声明元素种类");
            Check.Equal(0L, long.Parse(((WireInteger)Member(entry!, "min_count")).Digits),
                $"sequence_type '{typeId}' 的 min_count 来自集合容量语义（当前 0）");
        }

        // describe：limits 与 supported_structures 必须与实际发布一致（不再是"引擎无 Sequence 声明"）。
        var describe = host.Dispatch(Req("f07-describe", Operation.Describe));
        Check.True(describe.Ok, $"describe 应当成功：{describe.Error?.Code}");
        var capabilities = (WireObject)Field(describe, "capabilities");
        var limits = (WireObject)Member(capabilities, "limits");
        Check.Equal((long)declared.Count, long.Parse(((WireInteger)Member(limits, "sequences")).Digits),
            "describe.limits.sequences 必须等于实际发布的 sequence_type 数");
        var sequenceStructure = ((WireArray)Member(capabilities, "supported_structures")).Items
            .OfType<WireObject>()
            .Single(item => Member(item, "structure") is WireString text && text.Value == "Sequence");
        Check.Equal(declared.Count, Strings(Member(sequenceStructure, "declaration_ids")).Count(),
            "describe 的 Sequence 结构声明数必须与目录一致（空表＋「不支持」不再成立）");

        // 两个不同字符串 seed：owner 都在**该会话自己的绑定表**里解析到声明的公开种类的唯一可见实例。
        foreach (var (label, seed) in new[] { ("milestone", MilestoneSeed), ("second", SecondSeed) })
        {
            var started = Start(host, "f07-start-" + label, seed);
            Check.True(started.Ok, $"start({label}) 应当成功：{started.Error?.Code}");
            var sessionId = StringField(started, "session_id")!;
            var session = host.GetSessionForTests(sessionId);
            var observed = Observe(host, sessionId);
            Check.True(observed.Ok, $"observe({label}) 应当成功：{observed.Error?.Code}");
            var snapshot = (WireObject)Field(observed, "snapshot");
            var sequences = ((WireArray)Member(snapshot, "sequences")).Items.OfType<WireObject>().ToList();
            Check.Equal(declared.Count, sequences.Count,
                $"快照必须发布 base 声明的全部 {declared.Count} 条 sequence（{label}）");

            var kindByRef = Entities(snapshot).ToDictionary(InstanceId, entity =>
                ((WireString)Member(entity, "kind_id")).Value, StringComparer.Ordinal);
            foreach (var (collectionId, rule) in declared)
            {
                var typeId = DerivedPublicProfile.SequenceTypePrefix + collectionId;
                var sequence = sequences.Single(item =>
                    Member(item, "type_id") is WireString text && text.Value == typeId);
                var ownerRef = ((WireString)Member(sequence, "owner_ref")).Value;
                Check.True(kindByRef.TryGetValue(ownerRef, out var ownerKind),
                    $"sequence '{typeId}' 的 owner_ref '{ownerRef}' 必须是本快照已发布的实体（不能是写死句柄）");
                Check.Equal(DerivedPublicProfile.KindPrefix + rule.OwnerKindEngineTypeId, ownerKind!,
                    $"sequence '{typeId}' 的 owner 必须是声明的公开种类的可见实例（{label}）");
                Check.True(session.Snapshot().InstanceRefs.Values.Contains(ownerRef),
                    $"sequence '{typeId}' 的 owner_ref 必须来自本会话的绑定表（不是常量）");
            }
        }
    }

    // -- F-08 ------------------------------------------------------------------------------------------------

    /// <summary>
    /// `PlayedThisTurn` 的真实公开成员与顺序：一条**完全合法**的两次出牌路径（出牌 → 逐个回答真实公开决策 →
    /// 再出牌）之后，快照 `sequences[].elements` 必须与引擎 `ParticipantView` 的成员**逐位**一致——顺序反转
    /// 或丢成员都直接失败（不是"排序后看起来像"）。
    ///
    /// 第二张牌**逐候选回退**：正式 base 内容里某些牌作为同一回合第二张打出时，Step 结算会在引擎里抛
    /// `backend_fault`（pure procedure 'base-spire:PowerStacksOf' did not complete）。那是 base/core 侧的
    /// 既有缺陷（本包交接已记录复现），net 检查不能把它当"顺序正确"；因此每个候选都在**干净的新会话**里试，
    /// 记录被引擎拒绝的候选，换下一个；**所有候选都失败**时下面统一失败（即不存在合法两次出牌路径）。
    /// </summary>
    private static void PublicSequenceOrderAndMembers()
    {
        var factory = FormalDevScenario.CreateFactory();
        var host = NewHost(factory, out var metadata);
        var played = metadata.Collections.SingleOrDefault(pair =>
            pair.Value.Use == PublicCollectionUse.Sequence
            && pair.Key.EndsWith("PlayedThisTurn", StringComparison.Ordinal));
        Check.True(played.Value is not null,
            "本轮 base 元数据必须把 PlayedThisTurn 声明为公开有序集合（否则没有真实顺序可核对）");
        var collectionId = played.Key;
        var sequenceTypeId = DerivedPublicProfile.SequenceTypePrefix + collectionId;

        List<string> raw = [];
        SequenceDto? published = null;
        List<(string Card, string Error)> rejected = [];
        for (var attempt = 0; attempt < 6 && published is null; attempt++)
        {
            // 同一个每局字符串 seed、每次一个**新会话**：引擎故障后的会话状态未知，不能在同一会话里重试。
            var started = Start(host, "f08-start-" + attempt, MilestoneSeed);
            Check.True(started.Ok, $"start 应当成功：{started.Error?.Code}");
            var sessionId = StringField(started, "session_id")!;
            var session = host.GetSessionForTests(sessionId);

            // 第一张取公开引用**最大**的手牌：第二张的引用必然更小，于是能区分「按公开句柄排序」的实现。
            var first = PickPlay(host, session, sessionId, descending: true, rejected);
            if (first is null) break;
            var (firstResponse, firstFault, firstFaultMessage) = DispatchOrFault(host, sessionId, first.Value.ObservationId, first.Value.Candidate);
            if (firstFault is not null || firstResponse is { Ok: false })
            {
                var code = firstFault ?? firstResponse!.Error!.Code!;
                Check.Equal("backend_fault", code,
                    "第一张合法 PlayCard 的不是引擎故障的失败必须直接失败（不得当成可回退的候选）");
                rejected.Add((first.Value.Card, firstFaultMessage ?? firstResponse!.Error!.Message ?? code));
                Console.WriteLine($"      F-08 第{attempt + 1}次：第一张 {first.Value.Card} 引擎故障（{code}）"
                    + "——本包交接记录为 base/core 既有缺陷，换下一个候选重开一局");
                continue;
            }

            ResolveDecisions(host, sessionId);

            var second = PickPlay(host, session, sessionId, descending: false, rejected);
            if (second is null) break;
            var (secondResponse, secondFault, secondFaultMessage) = DispatchOrFault(host, sessionId, second.Value.ObservationId, second.Value.Candidate);
            if (secondFault is not null || secondResponse is { Ok: false })
            {
                var code = secondFault ?? secondResponse!.Error!.Code!;
                Check.Equal("backend_fault", code,
                    "第二张合法 PlayCard 的不是引擎故障的失败必须直接失败（不得当成可回退的候选）");
                rejected.Add((second.Value.Card, secondFaultMessage ?? secondResponse!.Error!.Message ?? code));
                Console.WriteLine($"      F-08 第{attempt + 1}次：第二张 {second.Value.Card} 打出后引擎故障"
                    + $"（{code}）——本包交接记录为 base/core 既有缺陷，换下一个候选重开一局");
                continue;
            }

            (raw, published) = ReadSequence(session, collectionId, sequenceTypeId);
        }

        Check.True(raw.Count >= 2,
            $"{collectionId} 在至少两次合法出牌后必须有真实成员（实际 {raw.Count}）；"
            + $"被引擎拒绝的第二张候选：[{string.Join(", ", rejected.Select(entry => entry.Card + "=" + entry.Error))}]");
        Check.True(published is not null, $"{collectionId} 必须作为 sequence 发布（不是 NotPublished/Group）");
        Check.Equal(raw.Count, published!.Elements.Count,
            $"{collectionId} 的 elements 条数必须等于 ParticipantView 的成员数（丢成员即失败）");
        for (var index = 0; index < raw.Count; index++)
        {
            Check.Equal(raw[index], published.Elements[index],
                $"{collectionId} elements[{index}] 必须与 ParticipantView 的第 {index} 个成员一致（顺序反转/重排即失败）");
        }

        Check.False(IsAscending(raw),
            "核对用的真实顺序必须不是升序：否则本检查无法区分「按公开句柄排序」的实现");
        Console.WriteLine($"      F-08 {collectionId}：ParticipantView = {string.Join(",", raw)}；"
            + $"发布 elements = {string.Join(",", published.Elements)}；"
            + $"被引擎拒绝的第二张候选 {rejected.Count} 个");
    }

    /// <summary>
    /// 按公开引用挑一张合法的 PlayCard：<paramref name="descending"/> 为真取引用**最大**的手牌（保证核对顺序不是升序），
    /// 否则取引用最小且不在 <paramref name="rejected"/> 里的候选（逐候选回退，见 F-08 的方法注释）。
    /// </summary>
    private static (string Candidate, string Card, long ObservationId)? PickPlay(ControlHost host, ControlSession session,
        string sessionId, bool descending, IReadOnlyCollection<(string Card, string Error)> rejected)
    {
        var observed = Observe(host, sessionId);
        Check.True(observed.Ok, $"observe 应当成功：{observed.Error?.Code}");
        var snapshot = (WireObject)Field(observed, "snapshot");

        // 手牌引用来自**原始投影**（公开引用经本会话绑定表得到），不是从 wire 形状猜。
        var hand = RawRefs(session, collection => collection.QualifiedId.EndsWith("Hand", StringComparison.Ordinal));
        var plays = new List<(string Candidate, string Card)>();
        foreach (var action in LegalActions(snapshot))
        {
            if (!ActionType(action).EndsWith("PlayCard", StringComparison.Ordinal)) continue;
            var cards = CandidateRefs(action).Where(hand.Contains).Distinct(StringComparer.Ordinal).ToList();
            Check.Equal(1L, (long)cards.Count,
                $"PlayCard 候选 {CandidateId(action)} 必须恰好引用一张公开手牌（实际 {cards.Count}）：否则无法核对打的是哪张");
            plays.Add((CandidateId(action), cards[0]));
        }

        var available = plays
            .Where(item => !rejected.Any(entry => entry.Card == item.Card))
            .OrderBy(item => item.Card, StringComparer.Ordinal)
            .ToList();
        if (available.Count == 0) return null;

        var pick = descending ? available[^1] : available[0];
        return (pick.Candidate, pick.Card, ObservationId(snapshot));
    }

    /// <summary>用**该次观测的**观察号提交一个 step（观察号过期时必须由服务端拒绝，而不是我们猜）。</summary>
    private static ResponseDto DispatchStep(ControlHost host, string sessionId, long observationId, string candidateId) =>
        host.Dispatch(Req("f08-step-" + Guid.NewGuid().ToString("N")[..8], Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(observationId)),
            ("candidate_id", WireValue.Str(candidateId))));

    /// <summary>
    /// 提交 step 并**区分**「协议错误响应」与「引擎故障异常」：Host 的 CLI（Program.Handle）会把引擎故障翻成
    /// `backend_fault` 错误响应，但直接调用 <see cref="ControlHost.Dispatch"/> 时它是 <see cref="EngineFaultException"/>。
    /// 检查必须把两者都当作 base/core 的既有故障（不是顺序证据），否则会把引擎故障误报成 net 检查缺陷。
    /// </summary>
    private static (ResponseDto? Response, string? FaultCode, string? FaultMessage) DispatchOrFault(ControlHost host,
        string sessionId, long observationId, string candidateId)
    {
        try
        {
            return (DispatchStep(host, sessionId, observationId, candidateId), null, null);
        }
        catch (EngineFaultException fault)
        {
            return (null, "backend_fault", fault.Code + "：" + fault.Message);
        }
    }

    /// <summary>把出牌后出现的**真实公开决策**逐个回答（例如攻击牌选目标），直到不再有决策候选。</summary>
    private static void ResolveDecisions(ControlHost host, string sessionId)
    {
        for (var round = 0; round < 8; round++)
        {
            var observed = Observe(host, sessionId);
            Check.True(observed.Ok, $"回答决策前 observe 应当成功：{observed.Error?.Code}");
            var snapshot = (WireObject)Field(observed, "snapshot");
            var decision = LegalActions(snapshot).FirstOrDefault(action =>
                ActionType(action).EndsWith("answer_decision", StringComparison.Ordinal));
            if (decision is null) return;

            var response = DispatchStep(host, sessionId, ObservationId(snapshot), CandidateId(decision));
            Check.True(response.Ok, $"回答真实公开决策应当成功：{response.Error?.Code} {response.Error?.Message}");
        }

        Check.True(false, "出牌后的公开决策没有在 8 轮内结束：本检查拿不到稳定的两次出牌边界");
    }

    /// <summary>
    /// 同一稳定边界的**原始投影**与**公开发布**：逐位对照必须以原始成员为基准（公开快照不能自我证明）。
    /// </summary>
    private static (List<string> Raw, SequenceDto Published) ReadSequence(ControlSession session, string collectionId,
        string sequenceTypeId)
    {
        var view = session.EngineForTests.Observe(session.ParticipantForTests, session.ProjectionIdForTests);
        var projected = session.Snapshot();
        Check.Equal(view.StateVersion, projected.ObservationId,
            "同一稳定边界的原始 ParticipantView 与公开观察必须同版本（否则对照不同源）");
        Check.True(projected.Snapshot is not null, "有快照的会话必须发布 snapshot");
        var collection = view.Collections.SingleOrDefault(item => item.QualifiedId == collectionId);
        Check.True(collection is not null, $"ParticipantView 缺少集合 '{collectionId}'");
        Check.True(collection!.IdentityVisible, $"{collectionId} 的成员必须公开可见（IdentityVisible=false）");
        Check.True(collection.OrderVisible, $"{collectionId} 的顺序必须公开可见（OrderVisible=false）");
        var sequence = projected.Snapshot!.Sequences.SingleOrDefault(item => item.TypeId == sequenceTypeId);
        Check.True(sequence is not null, $"快照必须把 '{collectionId}' 发布为 sequence（type_id={sequenceTypeId}）");
        var raw = collection.Members.Select(member => projected.InstanceRefs[member]).ToList();
        return (raw, sequence!);
    }

    /// <summary>原始投影里某个集合的成员在**本会话绑定表**里的公开引用（顺序即真实顺序）。</summary>
    private static List<string> RawRefs(ControlSession session, Func<ProjectedCollection, bool> match)
    {
        var view = session.EngineForTests.Observe(session.ParticipantForTests, session.ProjectionIdForTests);
        var projected = session.Snapshot();
        var collection = view.Collections.SingleOrDefault(match);
        Check.True(collection is not null, "ParticipantView 必须含有所需的集合");
        return collection!.Members.Select(member => projected.InstanceRefs[member]).ToList();
    }

    private static bool IsAscending(IReadOnlyList<string> values)
    {
        for (var index = 1; index < values.Count; index++)
        {
            if (string.CompareOrdinal(values[index - 1], values[index]) > 0) return false;
        }

        return true;
    }

    // -- F-09 ------------------------------------------------------------------------------------------------

    /// <summary>
    /// owner 的两种错误必须**显式拒绝**（不猜、不静默跳过）：owner 种类不在公开投影里在派生层拒绝，
    /// owner 种类在本快照里不唯一在投影层拒绝；同时保留旧的固定 `owner_ref` 路径（旧 0.1 兼容形状）。
    /// </summary>
    private static void SequenceOwnerRejectionAndLegacyRef()
    {
        var factory = FormalDevScenario.CreateFactory();
        var host = NewHost(factory, out var metadata);
        var semantics = factory.PublicSemantics;
        var started = Start(host, "f09-start", MilestoneSeed);
        Check.True(started.Ok, $"start 应当成功：{started.Error?.Code}");
        var sessionId = StringField(started, "session_id")!;
        var session = host.GetSessionForTests(sessionId);
        var view = session.EngineForTests.Observe(session.ParticipantForTests, session.ProjectionIdForTests);
        var rules = session.EngineForTests.RuleSet;
        var profile = host.Profile;

        var collectionId = metadata.Collections.Single(pair =>
            pair.Value.Use == PublicCollectionUse.Sequence
            && pair.Key.EndsWith("PlayedThisTurn", StringComparison.Ordinal)).Key;
        var sequenceTypeId = DerivedPublicProfile.SequenceTypePrefix + collectionId;
        var elementKindId = profile.Collections[collectionId].Sequence!.ElementKindIds.Single();

        // ① 派生层：owner 种类不在所选投影里 ⇒ 明确拒绝（不得猜一个、也不得静默降级成不发布）。
        var badMetadata = metadata with
        {
            Collections = new Dictionary<string, PublicCollectionRule>(metadata.Collections, StringComparer.Ordinal)
            {
                [collectionId] = PublicCollectionRule.Sequence("base-spire:NotProjectedKind"),
            },
        };
        Check.MappingFault(() => DerivedPublicProfile.Build(
                rules, semantics.ProjectionId, semantics.ParticipantRole, badMetadata, NetProtocol.V02),
            "owner 种类不在公开投影里时派生层必须显式失败（不得猜、不得静默不发布）",
            "N-02-UNDECLARED-COLLECTION");

        // ② 投影层：owner 种类在本快照里有多个可见实例 ⇒ 拒绝（谁是 owner 不唯一）。
        var pileKinds = profile.PileTypes.Select(pile => pile.PileKindId).ToHashSet(StringComparer.Ordinal);
        var groupOwnerKinds = profile.GroupTypes.Select(group => group.OwnerKindId)
            .ToHashSet(StringComparer.Ordinal);
        var ambiguous = view.Instances
            .GroupBy(instance => instance.TypeId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(kind => kind, StringComparer.Ordinal)
            .FirstOrDefault(kind =>
                !pileKinds.Contains(DerivedPublicProfile.KindPrefix + kind)
                && !groupOwnerKinds.Contains(DerivedPublicProfile.KindPrefix + kind));
        Check.True(ambiguous is not null, "本快照必须有可见实例 >1 的公开种类，才能核对 owner 歧义拒绝");

        var collections = new Dictionary<string, CollectionDisposition>(profile.Collections, StringComparer.Ordinal);
        collections[collectionId] = CollectionDisposition.AsSequence(
            PublishedSequence.ForOwnerKind(sequenceTypeId, ambiguous!, [elementKindId], 0, null));
        // decision 状态下投影要求候选非空：这一层只换集合处置，候选仍取该会话真实枚举（不注入假候选）。
        var legalActions = session.EngineForTests.LegalActions(session.ParticipantForTests);
        Check.True(legalActions.Count > 0, "核对 owner 拒绝时必须有真实候选，否则投影会先按空候选拒绝");
        Check.MappingFault(() => new PublicSnapshotProjector(rules, Probe(profile, collections), "f09-ambiguous")
                .Project(new SnapshotInput(view, legalActions, session.EngineForTests.PendingDecision,
                    session.EngineForTests.Status, session.EngineForTests.Terminal)),
            "owner 种类在本快照里不唯一时必须显式拒绝（不得猜一个）",
            "N-02-SEQUENCE-OWNER");

        // ③ 旧固定 owner_ref 路径仍可用（0.1 兼容形状）：直接声明一个公开引用，必须原样发布。
        var card = view.Instances.First(instance =>
            DerivedPublicProfile.KindPrefix + instance.TypeId == elementKindId);
        var baseline = new PublicSnapshotProjector(rules, profile, "f09-baseline").Project(
            new SnapshotInput(view, legalActions, session.EngineForTests.PendingDecision,
                session.EngineForTests.Status, session.EngineForTests.Terminal));
        var legacyRef = baseline.InstanceRefs[card.Id];
        collections[collectionId] = CollectionDisposition.AsSequence(
            PublishedSequence.ForPublicRef(sequenceTypeId, legacyRef, [elementKindId], 0, null));
        var legacy = new PublicSnapshotProjector(rules, Probe(profile, collections), "f09-legacy").Project(
            new SnapshotInput(view, legalActions, session.EngineForTests.PendingDecision,
                session.EngineForTests.Status, session.EngineForTests.Terminal));
        var legacySequence = legacy.Snapshot!.Sequences.Single(item => item.TypeId == sequenceTypeId);
        Check.Equal(legacyRef, legacySequence.OwnerRef,
            "旧的固定 owner_ref 路径必须原样发布该引用（0.1 兼容路径保留）");

        Console.WriteLine("      F-09 owner 拒绝：派生层 N-02-UNDECLARED-COLLECTION、投影层 N-02-SEQUENCE-OWNER；"
            + $"旧固定引用 {legacyRef} 仍发布");
    }

    /// <summary>同一 Profile 只换集合处置的探针（kinds/relations/actions/terminals 原样复用，不另造公开面）。</summary>
    private static PublicSnapshotProfile Probe(PublicSnapshotProfile profile,
        IReadOnlyDictionary<string, CollectionDisposition> collections) =>
        new(profile.RulesetId, profile.CatalogId, profile.BackendProfileId, profile.Kinds, collections,
            profile.Relations, profile.Actions, profile.AnswerAction, profile.Terminals, profile.KnownPositions,
            profile.ProtocolVersion);

    // -- 构造与辅助 -------------------------------------------------------------------------------------------

    private static ControlHost NewHost(BaseSpireRuntimeFactory factory, out PublicBuildMetadata metadata)
    {
        metadata = FormalDevMetadata.FromBase(factory);
        return FormalDevScenario.Create(factory, metadata, MilestoneSeed, 4096);
    }

    private static RequestDto StartRequest(ControlHost host, string id, string seed) =>
        Req(id, Operation.Start,
            ("expected_identity", Identity(host).ToWire()),
            ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)),
            ("seed", WireValue.Str(seed)));

    /// <summary>一条**完整的** start 请求文档（含 request_id/operation/payload）：请求层按版本校验 seed 形状。</summary>
    private static WireValue StartDocument(IdentityDto identity, string scenarioProfile, string seed) =>
        StartDocumentWithSeedNode(identity, scenarioProfile, WireValue.Str(seed));

    private static WireValue StartDocumentWithSeedNode(IdentityDto identity, string scenarioProfile, WireValue seed) =>
        WireValue.Object(
            ("request_id", WireValue.Str("f04-wire")),
            ("operation", WireValue.Str("start")),
            ("payload", WireValue.Object(
                ("expected_identity", identity.ToWire()),
                ("scenario_profile", WireValue.Str(scenarioProfile)),
                ("seed", seed))));

    private static ResponseDto Start(ControlHost host, string id, string seed) =>
        host.Dispatch(StartRequest(host, id, seed));

    private static IdentityDto Identity(ControlHost host) => new(
        host.Profile.ProtocolVersion, host.Profile.RulesetId, host.Profile.CatalogId,
        host.Profile.BackendProfileId);

    private static void RejectsInvalidOperation(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new CheckFailedException($"{message}：期望 InvalidOperationException（显式失败），但被接受");
    }

    /// <summary>公开的玩家生命值：从**投影绑定表**取公开引用，再读公开字段（不从引擎序号推导）。</summary>
    private static string PublicPlayerHp(ControlSession session, WireObject snapshot)
    {
        var rules = session.EngineForTests.RuleSet;
        var playerType = rules.TypeByQualifiedId("base-spire:Player");
        var player = session.EngineForTests.State.AllInstances(playerType.Id).First();
        var publicRef = session.Snapshot().InstanceRefs[player];
        var fieldId = DerivedPublicProfile.FieldPrefix + "base-spire:Player.Hp";
        var entity = Entities(snapshot).Single(item => InstanceId(item) == publicRef);
        var field = FieldsOf(entity).Single(item =>
            ((WireString)item.Members.Single(m => m.Key == "field_id").Value).Value == fieldId);
        Check.Equal("known", ((WireString)field.Members.Single(m => m.Key == "state").Value).Value,
            "公开生命必须是已知事实");
        return ((WireString)field.Members.Single(m => m.Key == "value").Value).Value;
    }

    private static IReadOnlyList<WireObject> Entities(WireObject snapshot) =>
        ((WireArray)snapshot.Members.Single(m => m.Key == "entities").Value).Items.OfType<WireObject>().ToList();

    private static IReadOnlyList<WireObject> FieldsOf(WireObject entity) =>
        ((WireArray)entity.Members.Single(m => m.Key == "fields").Value).Items.OfType<WireObject>().ToList();

    private static IReadOnlyList<WireObject> LegalActions(WireObject snapshot) =>
        ((WireArray)snapshot.Members.Single(m => m.Key == "legal_actions").Value).Items
        .OfType<WireObject>().ToList();

    private static string InstanceId(WireObject entity) =>
        ((WireString)entity.Members.Single(m => m.Key == "instance_id").Value).Value;

    private static string ActionType(WireObject action) =>
        ((WireString)action.Members.Single(m => m.Key == "action_type").Value).Value;

    private static string CandidateId(WireObject action) =>
        ((WireString)action.Members.Single(m => m.Key == "candidate_id").Value).Value;

    private static long ObservationId(WireObject snapshot) =>
        long.Parse(((WireInteger)snapshot.Members.Single(m => m.Key == "observation_id").Value).Digits);

    private static ResponseDto Observe(ControlHost host, string sessionId) =>
        host.Dispatch(Req("o-" + Guid.NewGuid().ToString("N")[..8], Operation.Observe,
            ("session_id", WireValue.Str(sessionId))));

    private static RequestDto Req(string id, Operation operation, params (string Key, WireValue Value)[] members) =>
        new(id, operation, new WireObject(members.Select(member =>
            new KeyValuePair<string, WireValue>(member.Key, member.Value))));

    private static WireValue Field(ResponseDto response, string key)
    {
        Check.True(response.Result is WireObject, $"响应结果必须是对象（{key}）：{response.Error?.Code}");
        foreach (var member in ((WireObject)response.Result!).Members)
        {
            if (member.Key == key)
            {
                return member.Value;
            }
        }

        throw new CheckFailedException($"响应结果缺少字段 {key}");
    }

    private static string? StringField(ResponseDto response, string key) =>
        Field(response, key) is WireString text ? text.Value : null;

    private static string ErrorCode(ResponseDto response)
    {
        Check.True(response.Error is not null, "期望错误响应");
        return response.Error!.Code;
    }

    /// <summary>已知必存在的对象成员（缺字段时显式失败，不返回 null 混过去）。</summary>
    private static WireValue Member(WireObject value, string key) =>
        value.Members.Single(member => member.Key == key).Value;

    private static WireValue? TryMember(WireObject value, string key)
    {
        foreach (var member in value.Members)
        {
            if (member.Key == key)
            {
                return member.Value;
            }
        }

        return null;
    }

    /// <summary>把声明列表读成字符串集合（单值或数组都接受：声明面不保证包装形状）。</summary>
    private static IReadOnlyList<string> Strings(WireValue value) => value switch
    {
        WireString text => [text.Value],
        WireArray array => array.Items.SelectMany(Strings).ToList(),
        _ => [],
    };

    /// <summary>候选动作公开载荷里引用的实体（公开引用）：只认已知事实，不从形状猜。</summary>
    private static IReadOnlyList<string> CandidateRefs(WireObject action)
    {
        var found = new List<string>();
        foreach (var field in FieldsOf(action))
        {
            if (TryMember(field, "state") is not WireString state || state.Value != "known") continue;
            if (TryMember(field, "value") is { } value) CollectStrings(value, found);
        }

        return found;
    }

    private static void CollectStrings(WireValue value, List<string> found)
    {
        switch (value)
        {
            case WireString text:
                found.Add(text.Value);
                break;
            case WireArray array:
                foreach (var item in array.Items)
                {
                    CollectStrings(item, found);
                }

                break;
            case WireObject nested:
                foreach (var member in nested.Members)
                {
                    CollectStrings(member.Value, found);
                }

                break;
        }
    }

    // -- G：dev-map 范围（ENV-T03 C 线：地图公开 Host 接线） -------------------------------------------------
    //
    // 这五个检查只核对**能在本轮输入上核实**的事实：范围选择入口、dev-map 的公开目录/快照/候选是否
    // 完全由引擎声明派生、合法选择是否进入既有正常战斗、非法/过期是否在执行前被拒且不消耗抽数。
    // 它们**不**代表地图全版本、遭遇池/候选池公开面或完整胜负已验收（那些缺口在交接里如实列出）。

    private static readonly string MapNodeKindId = DerivedPublicProfile.KindPrefix + "base-spire:MapNode";
    private static readonly string MapRunKindId = DerivedPublicProfile.KindPrefix + "base-spire:Run";

    private static readonly string MapNodesSequenceId =
        DerivedPublicProfile.SequenceTypePrefix + "base-spire:MapNodes";

    private static readonly string MapChildrenRelationId =
        DerivedPublicProfile.RelationPrefix + "base-spire:MapChildren";

    private static readonly string ChooseMapPointActionId =
        DerivedPublicProfile.ActionPrefix + "base-spire:ChooseMapPoint";

    private static readonly string ChooseMapPointNodeFieldId =
        DerivedPublicProfile.FieldPrefix + "base-spire:ChooseMapPoint.Node";

    /// <summary>本包核对的每局 seed（与 A 的地图包交接一致：两 seed 的真实子进程证据已落盘）。</summary>
    private static readonly string[] MapEvidenceSeeds = ["t05-map-alpha", "t05-map-beta"];

    /// <summary>G-01：命令行范围选择入口；同一当前构建的缺省/dev 入口身份一致，内容变化允许摘要迁移。</summary>
    private static void MapScopeSelection()
    {
        Check.Sequence(new[] { "dev", "dev-map", "dev-run" },
            FormalDevScenario.SelectableScopes.Select(scope => scope.Name),
            "命令行显式区分单幕与多幕开发范围（顺序稳定）");
        Check.Equal(BaseSpireScope.Dev, FormalDevScenario.ScopeIdForName("dev")!,
            "dev 名字映射到 base 的 dev 范围常量");
        Check.Equal(BaseSpireScope.DevMap, FormalDevScenario.ScopeIdForName("dev-map")!,
            "dev-map 名字映射到 base 的 dev-map 范围常量");
        Check.True(FormalDevScenario.ScopeIdForName("dev-map-typo") is null,
            "未知范围名返回 null（调用方显式拒绝，不静默回退到 dev）");
        Check.Equal("dev", FormalDevScenario.DefaultScopeName, "缺省范围名仍是 dev（旧命令行逐字不变）");

        var byDefault = FormalDevScenario.CreateFactory().Describe();
        var explicitDev = FormalDevScenario.CreateFactory(BaseSpireScope.Dev).Describe();
        Check.Equal(BaseSpireScope.Dev, byDefault.ScopeId, "无参工厂的范围身份是 dev");
        Check.Equal(byDefault.PublicBuildId, explicitDev.PublicBuildId,
            "显式 dev 与缺省 dev 是同一构建身份");

        var devMap = FormalDevScenario.CreateFactory(BaseSpireScope.DevMap).Describe();
        Check.Equal(BaseSpireScope.DevMap, devMap.ScopeId, "dev-map 工厂的范围身份是 dev-map");
        Check.True(devMap.PublicBuildId != byDefault.PublicBuildId,
            "范围进入身份：dev-map 与 dev 的公开构建 id 必须不同");

        var devHost = NewHost(FormalDevScenario.CreateFactory(), out _);
        var mapHost = NewHost(FormalDevScenario.CreateFactory(BaseSpireScope.DevMap), out _);
        Check.True(devHost.Profile.RulesetId != mapHost.Profile.RulesetId,
            "范围进入身份：dev-map 与 dev 的规则集 id 必须不同");
        Check.True(devHost.Profile.CatalogId != mapHost.Profile.CatalogId,
            "范围进入身份：dev-map 与 dev 的公开目录 id 必须不同");
    }

    /// <summary>
    /// G-02：dev-map 的公开目录必须**从引擎声明派生**（不逐项登记）：地图实体种类/字段、Run 拥有的有序集合、
    /// MapChildren 关系、ChooseMapPoint 动作及其必需参数都要在目录里，且形状是可核对的公开类别/引用。
    /// </summary>
    private static void MapDeclarationsDerivedFromEngine()
    {
        var host = NewHost(FormalDevScenario.CreateFactory(BaseSpireScope.DevMap), out _);
        var catalog = host.Dispatch(Req("g02-catalog", Operation.GetCatalog,
            ("catalog_id", WireValue.Str(host.Profile.CatalogId))));
        Check.True(catalog.Ok, $"dev-map get_catalog 应当成功：{catalog.Error?.Code}");
        var declarations = (WireObject)Field(catalog, "declarations");

        var kinds = DeclarationRows(declarations, "entity_kinds");
        Check.True(DeclarationRow(kinds, MapNodeKindId) is not null,
            $"dev-map 目录必须发布 {MapNodeKindId}（来自 base 的地图实体声明，不逐项登记）");
        Check.True(DeclarationRow(kinds, MapRunKindId) is not null, $"dev-map 目录必须发布 {MapRunKindId}");

        var fields = DeclarationRows(declarations, "fields");
        foreach (var (qualified, valueType) in new[]
                 {
                     ("base-spire:MapNode.Col", "number"),
                     ("base-spire:MapNode.Row", "number"),
                     ("base-spire:MapNode.Index", "number"),
                     ("base-spire:MapNode.Type", "category"),
                     ("base-spire:MapNode.Visited", "boolean"),
                     ("base-spire:Run.CurrentNode", "entity_ref"),
                     ("base-spire:Run.MapPhase", "number"),
                     ("base-spire:Run.MapFloor", "number"),
                     ("base-spire:Run.MapNodeCount", "number"),
                 })
        {
            var row = DeclarationRow(fields, DerivedPublicProfile.FieldPrefix + qualified);
            Check.True(row is not null, $"dev-map 目录缺少地图公开字段 {qualified}");
            Check.Equal(valueType, ((WireString)Member(row!, "value_type")).Value,
                $"地图公开字段 {qualified} 的公开值类型");
        }

        Check.True(DeclarationRow(fields, DerivedPublicProfile.FieldPrefix + "base-spire:Run.MapDraws") is null,
            "Hidden 地图重放词表不得进入公开目录");

        var typeField = DeclarationRow(fields, DerivedPublicProfile.FieldPrefix + "base-spire:MapNode.Type")!;
        Check.Contains(Strings(Member(typeField, "category_ids")),
            id => id == DerivedPublicProfile.CategoryPrefix + "base-spire:MapPointType:Monster",
            "MapNode.Type 的公开类别必须含 Monster（枚举成员按 net:cat:<枚举限定名>:<成员名> 派生）");

        var currentNode = DeclarationRow(fields, DerivedPublicProfile.FieldPrefix + "base-spire:Run.CurrentNode")!;
        Check.Equal(false, ((WireBool)Member(currentNode, "required")).Value,
            "Run.CurrentNode 是可选引用（开局尚未选择时也必须能发布快照）");
        Check.Contains(Strings(Member(currentNode, "target_kind_ids")),
            id => id == MapNodeKindId, "Run.CurrentNode 的目标种类必须是 MapNode");

        var sequences = DeclarationRows(declarations, "sequence_types");
        var mapNodes = DeclarationRow(sequences, MapNodesSequenceId);
        Check.True(mapNodes is not null, $"dev-map 目录必须发布有序集合 {MapNodesSequenceId}（Run 拥有）");
        Check.Equal(MapRunKindId, Strings(Member(mapNodes!, "owner_kind_ids")).Single(),
            "MapNodes 的 owner 是唯一的公开种类 Run（不是写死实例句柄）");
        Check.Contains(Strings(Member(mapNodes!, "element_kind_ids")), id => id == MapNodeKindId,
            "MapNodes 的元素种类是 MapNode");

        var relations = DeclarationRows(declarations, "relation_types");
        var children = DeclarationRow(relations, MapChildrenRelationId);
        Check.True(children is not null,
            $"dev-map 目录必须发布关系 {MapChildrenRelationId}（RelationSymbol.Public 默认可见 ⇒ 自动派生）");
        Check.Contains(Strings(Member(children!, "target_kind_ids")), id => id == MapNodeKindId,
            "MapChildren 的目标种类是 MapNode");

        var actions = DeclarationRows(declarations, "action_types");
        var choose = DeclarationRow(actions, ChooseMapPointActionId);
        Check.True(choose is not null, $"dev-map 目录必须发布动作 {ChooseMapPointActionId}");
        Check.Equal(ChooseMapPointNodeFieldId, Strings(Member(choose!, "required_fields")).Single(),
            "ChooseMapPoint 的唯一必需参数是到 MapNode 的引用");
        var nodeParameter = DeclarationRow(fields, ChooseMapPointNodeFieldId);
        Check.True(nodeParameter is not null, $"dev-map 目录必须发布参数字段 {ChooseMapPointNodeFieldId}");
        Check.Equal("entity_ref", ((WireString)Member(nodeParameter!, "value_type")).Value,
            "ChooseMapPoint.Node 的公开值类型是实体引用");
        Check.Contains(Strings(Member(nodeParameter!, "target_kind_ids")), id => id == MapNodeKindId,
            "ChooseMapPoint.Node 的目标种类是 MapNode");
    }

    /// <summary>
    /// G-06（I-71/I-84）：0.2 dev-map 宿主的 `describe` 与 `get_catalog` 必须**引用闭合**——`describe` 里
    /// 每个 `supported_structures[].declaration_ids` 都要能在同一目录里找到，且必须是**已发布的公开 ID**
    /// （`net:kind:`/`net:relation:`/`net:action:`/`net:reason:`），不得用映射表的引擎键
    /// （`base-spire:Player`）冒充。判别读的是两次真实请求的 wire 结果，不是 Profile 内部清单。
    /// </summary>
    private static void MapDescriptionsCloseAgainstCatalog()
    {
        var host = NewHost(FormalDevScenario.CreateFactory(BaseSpireScope.DevMap), out _);
        var describeResponse = host.Dispatch(Req("g06-describe", Operation.Describe));
        Check.True(describeResponse.Ok, $"dev-map describe 应当成功：{describeResponse.Error?.Code}");
        // 0.2 的身份按**所选 Profile 的协议版本**解析（IdentityDto 的默认分支是 0.1，不能拿它读 0.2 身份）。
        var describeWire = (WireObject)describeResponse.Result!;
        var describe = new DescribeResultDto(
            IdentityDto.FromWire(Member(describeWire, "identity"), host.Profile.ProtocolVersion),
            CapabilitiesDto.FromWire(Member(describeWire, "capabilities")));
        var catalogResponse = host.Dispatch(Req("g06-catalog", Operation.GetCatalog,
            ("catalog_id", WireValue.Str(host.Profile.CatalogId))));
        Check.True(catalogResponse.Ok, $"dev-map get_catalog 应当成功：{catalogResponse.Error?.Code}");
        var catalog = PublicCatalogDto.FromWire(catalogResponse.Result!, host.Profile.ProtocolVersion);

        var unclosed = DeclarationClosure.Unclosed(describe, catalog);
        Check.Equal(0, unclosed.Count,
            $"dev-map describe 的 declaration_ids 必须全部在本目录声明（I-84）：{string.Join(",", unclosed)}");
        var engineKeys = DeclarationClosure.EngineKeysUsedAsDeclarationIds(describe, host.Profile);
        Check.Equal(0, engineKeys.Count,
            $"dev-map describe 不得把引擎映射键当公开声明 ID：{string.Join(",", engineKeys)}");

        // 四类结构各自**真的有内容**（不用空数组掩盖缺失），且是已发布的公开形态。
        foreach (var structure in new[] { "Entity", "Relation", "LegalAction", "TerminalResult" })
        {
            Check.True(DeclarationClosure.StructureIds(describe, structure).Count > 0,
                $"{structure} 必须声明实际条目（空表不得冒充已闭合）");
        }

        Check.True(DeclarationClosure.StructureIds(describe, "Entity")
                .Contains("net:kind:base-spire:Player", StringComparer.Ordinal),
            "Entity 必须声明 net:kind:base-spire:Player（公开 kind id，不是引擎键）");
        Check.True(DeclarationClosure.StructureIds(describe, "LegalAction")
                .Contains("net:action:net.answer_decision", StringComparer.Ordinal),
            "LegalAction 必须登记净自声明的决策应答类型");
        Check.True(DeclarationClosure.StructureIds(describe, "Relation")
                .All(id => id.StartsWith(DerivedPublicProfile.RelationPrefix, StringComparison.Ordinal)),
            "Relation 的声明 ID 必须是已发布的关系类型（net:relation:…）");
        Check.True(DeclarationClosure.StructureIds(describe, "LegalAction")
                .All(id => id.StartsWith(DerivedPublicProfile.ActionPrefix, StringComparison.Ordinal)),
            "LegalAction 的声明 ID 必须是已发布的动作类型（net:action:…）");
    }

    /// <summary>
    /// G-03：dev-map 真实开局。地图事实必须来自公开面：阶段/层数来自 Run 的公开字段，节点集合来自公开有序集合，
    /// 后继来自**公开关系**（MapChildren），并且合法选择候选的 Node 必须与后继逐一相同（不多不少）。
    /// </summary>
    private static void MapInitialCandidates()
    {
        foreach (var seed in MapEvidenceSeeds)
        {
            var host = NewHost(FormalDevScenario.CreateFactory(BaseSpireScope.DevMap), out _);
            var started = Start(host, "g03-start-" + seed, seed);
            Check.True(started.Ok, $"dev-map start({seed}) 应当成功：{started.Error?.Code}");
            var snapshot = (WireObject)Field(started, "snapshot");
            var run = EntityOfKind(snapshot, MapRunKindId);

            Check.Equal(0L, MapInt(run, "base-spire:Run.MapPhase"), $"{seed}：开局地图阶段是 0（可选点）");
            Check.Equal(0L, MapInt(run, "base-spire:Run.MapFloor"), $"{seed}：开局地图层是 0");

            var nodes = MapNodeRefs(snapshot);
            Check.True(nodes.Count > 1, $"{seed}：MapNodes 必须发布整张地图（实际 {nodes.Count} 个节点）");
            Check.Equal(MapInt(run, "base-spire:Run.MapNodeCount"), (long)nodes.Count,
                $"{seed}：Run.MapNodeCount 必须等于公开有序集合的元素个数");
            Check.Equal(nodes.Count, nodes.Distinct(StringComparer.Ordinal).Count(),
                $"{seed}：MapNodes 不得重复发布同一节点引用");
            var engine = host.GetSessionForTests(StringField(started, "session_id")!).EngineForTests;
            var draws = LocalMapDraws(engine);
            Check.True(draws.Length > 0, $"{seed}：Engine 本地诊断仍保留非空地图重放词表");
            var hash = engine.State.StateHash();
            var rng = engine.Random.TotalDraws;
            for (var repeat = 0; repeat < 3; repeat++)
            {
                AssertNoMapDiagnostics(Observe(host, StringField(started, "session_id")!), draws);
                Check.Equal(draws, LocalMapDraws(engine), "本地读取与重复 observe 保留词表");
            }
            Check.Equal(hash, engine.State.StateHash(), "本地读取与重复 observe 不改变状态");
            Check.Equal(rng, engine.Random.TotalDraws, "本地读取与重复 observe 不消费 RNG");
            AssertNoMapDiagnostics(started, draws);
            Console.WriteLine($"      · local diagnostics seed={seed} words={draws.Split(';', StringSplitOptions.RemoveEmptyEntries).Length} rng={rng} state={hash}");

            var current = MapText(run, "base-spire:Run.CurrentNode");
            Check.True(nodes.Contains(current), $"{seed}：Run.CurrentNode {current} 必须是 MapNodes 的公开成员");

            var successors = MapChildrenOf(snapshot, current);
            Check.True(successors.Count > 0, $"{seed}：当前节点的公开 MapChildren 后继不得为空");
            var choices = LegalActions(snapshot)
                .Where(action => ActionType(action) == ChooseMapPointActionId)
                .Select(action => MapText(action, "base-spire:ChooseMapPoint.Node"))
                .OrderBy(reference => reference, StringComparer.Ordinal)
                .ToList();
            Check.True(choices.Count > 0, $"{seed}：地图阶段 0 必须有 ChooseMapPoint 公开候选");
            Check.Sequence(successors, choices,
                $"{seed}：ChooseMapPoint 候选必须恰是当前节点的公开 MapChildren 后继（不多不少）");
            foreach (var choice in choices)
            {
                Check.True(nodes.Contains(choice), $"{seed}：候选 Node {choice} 必须是已发布的 MapNode 实例");
                Check.Equal(MapNodeKindId, ((WireString)Member(EntityOfRef(snapshot, choice), "kind_id")).Value,
                    $"{seed}：候选 Node {choice} 的公开种类必须是 MapNode");
            }

            var monsters = choices.Where(choice => MapNodeTypeOf(snapshot, choice) == "Monster").ToList();
            Check.True(monsters.Count > 0,
                $"{seed}：本包需要至少一个公开的 Monster 后继才能验证「选择 → 进入既有战斗」"
                + $"（实际类型：{string.Join("/", choices.Select(choice => MapNodeTypeOf(snapshot, choice)))}）");
        }
    }

    private static void SownPublicVisit()
    {
        var host = NewHost(FormalDevScenario.CreateFactory(BaseSpireScope.DevRun), out _);
        var started = Start(host, "sown-start", "ENV-T04-RNG-SHUFFLE-001");
        Check.True(started.Ok, "formal start");
        var sid = StringField(started, "session_id")!;
        var control = host.GetSessionForTests(sid); var engine = control.EngineForTests;
        var owner = engine.State.AllInstances(engine.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
        Value Read(CoreTheSpire.Engine.InstanceId id, string name) => engine.State.GetField(id, engine.RuleSet.Type(engine.State.TypeIdOf(id)).FieldIndex(name));
        void Set(CoreTheSpire.Engine.InstanceId id, string name, Value value) => engine.State.SetField(id, engine.RuleSet.Type(engine.State.TypeIdOf(id)).FieldIndex(name), value);
        var deck = engine.State.CollectionMembers(engine.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:LongTermDeck").Id).ToArray();
        // Explicit controlled visit; no assertion of natural event selection or normal victory.
        engine.State.RunAtomic(() => { Set(owner, "CurrentEventId", Value.Of("SapphireSeed")); Set(owner, "MapPhase", Value.Of(8L)); });
        var before = (WireObject)Field(Observe(host, sid), "snapshot");
        var plant = LegalActions(before).Single(a => ActionType(a).EndsWith(":SapphireSeedPlant", StringComparison.Ordinal));
        var pause = DispatchStep(host, sid, ObservationId(before), CandidateId(plant));
        Check.True(pause.Ok, pause.Error?.Message ?? "real event action");
        var pending = (WireObject)Field(pause, "snapshot");
        var chosen = deck[6]; var chosenRef = control.Snapshot().InstanceRefs[chosen];
        var answer = LegalActions(pending).Single(a => ActionType(a).EndsWith("answer_decision", StringComparison.Ordinal) && CandidateRefs(a).Contains(chosenRef));
        Check.Equal(deck.Length, LegalActions(pending).Count, "all actual eligible permanent instances published");
        var request = Req("sown-answer", Operation.Step, ("session_id", WireValue.Str(sid)),
            ("expected_observation_id", WireValue.Integer(ObservationId(pending))), ("candidate_id", WireValue.Str(CandidateId(answer))));
        var applied = host.Dispatch(request); Check.True(applied.Ok, applied.Error?.Message ?? "Plant answer");
        Check.Equal(WireCanonical.Text(applied.ToWire()), WireCanonical.Text(host.Dispatch(request).ToWire()), "answer replay identical");
        var after = (WireObject)Field(applied, "snapshot");
        var permanent = EntityOfRef(after, chosenRef);
        Check.Equal("base-spire:sown", MapText(permanent, "base-spire:Card.EnchantmentId"), "public attachment identity");
        Check.Equal(1L, MapInt(permanent, "base-spire:Card.EnchantmentAmount"), "public magnitude");
        Check.False(MapBool(permanent, "base-spire:Card.EnchantmentDisabled"), "permanent normal state");
        var stale = DispatchStep(host, sid, ObservationId(pending), CandidateId(answer));
        Check.False(stale.Ok, "old observation rejected");
        Check.Equal("stale_observation", stale.Error!.Code, "replay with new request does not apply twice");
        var relation = engine.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren");
        var nodes = engine.State.CollectionMembers(engine.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:MapNodes").Id);
        var parent = nodes.First(p => engine.State.RelationTargets(relation.Id, p).Any(c => Read(c, "Type").AsEnumOrdinal() == 5));
        engine.State.RunAtomic(() => { Set(owner, "CurrentNode", Value.OfRef(parent)); Set(owner, "MapFloor", Value.Of(Read(parent, "Row").AsInt() + 1)); });
        var travel = (WireObject)Field(Observe(host, sid), "snapshot");
        var next = LegalActions(travel).First(a => ActionType(a) == ChooseMapPointActionId && MapNodeTypeOf(travel, MapText(a, "base-spire:ChooseMapPoint.Node")) == "Monster");
        Check.True(DispatchStep(host, sid, ObservationId(travel), CandidateId(next)).Ok, "actual battle entry");
        var copy = engine.State.AllInstances(engine.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Single(c => Read(c, "LongTermSource").Kind == ValueKind.Ref && Read(c, "LongTermSource").AsRef() == chosen);
        engine.State.RunAtomic(() => {
            foreach (var name in new[] { "DrawPile", "DiscardPile", "ExhaustPile" }) { var pile = engine.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name); if (engine.State.IsMember(pile.Id, copy)) engine.State.RemoveFromCollection(pile.Id, copy); }
            var hand = engine.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand"); if (!engine.State.IsMember(hand.Id, copy)) engine.State.AddToCollection(hand.Id, copy);
        });
        var combat = (WireObject)Field(Observe(host, sid), "snapshot"); var copyRef = control.Snapshot().InstanceRefs[copy];
        var play = LegalActions(combat).Single(a => ActionType(a).EndsWith(":PlayCard", StringComparison.Ordinal) && CandidateRefs(a).Contains(copyRef));
        Check.True(DispatchStep(host, sid, ObservationId(combat), CandidateId(play)).Ok, "public play actual attached copy");
        var end = (WireObject)Field(Observe(host, sid), "snapshot");
        Check.True(MapBool(EntityOfRef(end, copyRef), "base-spire:Card.EnchantmentDisabled"), "battle attachment consumed publicly");
        Check.False(MapBool(EntityOfRef(end, chosenRef), "base-spire:Card.EnchantmentDisabled"), "permanent status remains normal");
    }

    private static void MultiActPublicBoundary()
    {
        var host = NewHost(FormalDevScenario.CreateFactory(BaseSpireScope.DevRun), out _);
        var started = Start(host, "multi-start", "ENV-T04-RNG-SHUFFLE-001");
        Check.True(started.Ok, $"multi-act start must succeed: {started.Error?.Message}");
        var sessionId = StringField(started, "session_id")!;
        var initial = (WireObject)Field(started, "snapshot");
        var oldNodes = MapNodeRefs(initial);
        var engine = host.GetSessionForTests(sessionId).EngineForTests;
        var runType = engine.RuleSet.TypeByQualifiedId("base-spire:Run");
        var run = engine.State.AllInstances(runType.Id).Single();
        var originals = new[] { "LongTermDeck", "Relics", "PotionBelt" }.ToDictionary(name => name,
            name => engine.State.CollectionMembers(engine.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id).ToArray());
        engine.State.RunAtomic(() =>
        {
            foreach (var (name, value) in new[] { ("CurrentRoom", 7L), ("SettledRoom", 7L), ("Hp", 37L), ("Gold", 234L), ("MapPhase", 6L) })
                engine.State.SetField(run, runType.FieldIndex(name), Value.Of(value));
            engine.State.SetField(run, runType.FieldIndex("ActCompleted"), Value.Of(true));
        });
        var observed = host.Dispatch(Req("multi-observe", Operation.Observe, ("session_id", WireValue.Str(sessionId))));
        Check.True(observed.Ok, $"controlled completed-act boundary must project: {observed.Error?.Message}");
        var before = (WireObject)Field(observed, "snapshot");
        var candidate = LegalActions(before).Single(a => ActionType(a).EndsWith(":ContinueToNextAct", StringComparison.Ordinal));
        var upFront = engine.Random.DrawCount(BaseSpire.Slice.VanillaRandomLayout.EngineStreamName("UpFront"));
        var request = Req("multi-next", Operation.Step, ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(ObservationId(before))), ("candidate_id", WireValue.Str(CandidateId(candidate))));
        var response = host.Dispatch(request);
        Check.True(response.Ok, $"actual public transition must succeed: {response.Error?.Message}");
        Check.Equal(WireCanonical.Text(response.ToWire()), WireCanonical.Text(host.Dispatch(request).ToWire()), "replayed transition must return its original response");
        var snapshot = (WireObject)Field(response, "snapshot"); var publicRun = EntityOfKind(snapshot, MapRunKindId);
        Check.Equal(1L, MapInt(publicRun, "base-spire:Run.CurrentActIndex"), "one public transition enters Hive");
        Check.Equal("base-spire:hive-act", MapText(publicRun, "base-spire:Run.ActId"), "act identity is publicly visible");
        Check.Equal(37L, MapInt(publicRun, "base-spire:Run.Hp"), "health survives the transition");
        Check.Equal(234L, MapInt(publicRun, "base-spire:Run.Gold"), "gold survives the transition");
        Check.False(MapNodeRefs(snapshot).Any(oldNodes.Contains), "new map cannot reuse destroyed old map handles");
        foreach (var (name, ids) in originals)
            Check.Sequence(ids, engine.State.CollectionMembers(engine.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id), name + " preserves its real instances");
        Check.Equal(upFront, engine.Random.DrawCount(BaseSpire.Slice.VanillaRandomLayout.EngineStreamName("UpFront")), "public transition never regenerates rooms");
        var stale = host.Dispatch(Req("multi-stale", Operation.Step, ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(ObservationId(snapshot))), ("candidate_id", WireValue.Str(CandidateId(candidate)))));
        Check.False(stale.Ok, "old transition candidate is invalid at the next act");
        var ancient = LegalActions(snapshot).Single(a => ActionType(a) == ChooseMapPointActionId);
        Check.Equal("Ancient", MapNodeTypeOf(snapshot, MapText(ancient, "base-spire:ChooseMapPoint.Node")), "next-act ancient cannot be silently skipped");
        var frontier = host.Dispatch(Req("multi-ancient", Operation.Step, ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(ObservationId(snapshot))), ("candidate_id", WireValue.Str(CandidateId(ancient)))));
        Check.False(frontier.Ok, "unimplemented ancient effect is a genuine boundary");
        Check.True(frontier.Error!.Message.Contains("ancient event", StringComparison.Ordinal), "failure names the actual missing ancient behavior");
    }

    /// <summary>
    /// G-04：两 seed 的合法选择 → 进入既有正常战斗 + 写回核对；同 request 重发幂等；
    /// 非法候选（实体引用）与过期观察在执行前被拒，且观察号/当前节点/阶段/地图抽数都不变。
    /// </summary>
    private static void MapChooseAndRejections()
    {
        foreach (var seed in MapEvidenceSeeds)
        {
            var host = NewHost(FormalDevScenario.CreateFactory(BaseSpireScope.DevMap), out _);
            var started = Start(host, "g04-start-" + seed, seed);
            Check.True(started.Ok, $"dev-map start({seed}) 应当成功：{started.Error?.Code}");
            var sessionId = StringField(started, "session_id")!;
            var snapshot = (WireObject)Field(started, "snapshot");
            var observation = ObservationId(snapshot);
            var run = EntityOfKind(snapshot, MapRunKindId);
            var engine = host.GetSessionForTests(sessionId).EngineForTests;
            var draws = LocalMapDraws(engine);
            var rng = engine.Random.TotalDraws;
            var hash = engine.State.StateHash();
            var current = MapText(run, "base-spire:Run.CurrentNode");

            var monster = LegalActions(snapshot)
                .Where(action => ActionType(action) == ChooseMapPointActionId)
                .Where(action => MapNodeTypeOf(snapshot, MapText(action, "base-spire:ChooseMapPoint.Node")) == "Monster")
                .FirstOrDefault();
            Check.True(monster is not null, $"{seed}：必须有公开的 Monster 后继可选");
            var pick = CandidateId(monster!);
            var pickNode = MapText(monster!, "base-spire:ChooseMapPoint.Node");
            var pickRow = MapInt(EntityOfRef(snapshot, pickNode), "base-spire:MapNode.Row");

            // 非法候选：实体引用不是候选，必须在任何状态/随机变更之前被拒。
            var illegal = DispatchStep(host, sessionId, observation, current);
            Check.Equal("illegal_candidate", ErrorCode(illegal),
                $"{seed}：把实体引用当候选必须被拒（候选不属于当前稳定边界）");
            var afterIllegal = (WireObject)Field(Observe(host, sessionId), "snapshot");
            var afterIllegalRun = EntityOfKind(afterIllegal, MapRunKindId);
            Check.Equal(observation, ObservationId(afterIllegal), $"{seed}：被拒候选不得推进观察号");
            Check.Equal(draws, LocalMapDraws(engine),
                $"{seed}：被拒候选不得消耗地图抽数（状态/RNG 不变）");
            Check.Equal(current, MapText(afterIllegalRun, "base-spire:Run.CurrentNode"),
                $"{seed}：被拒候选不得改当前节点");
            Check.Equal(0L, MapInt(afterIllegalRun, "base-spire:Run.MapPhase"), $"{seed}：被拒候选不得改地图阶段");

            Check.Equal(hash, engine.State.StateHash(), "非法候选和观察不改状态");
            Check.Equal(rng, engine.Random.TotalDraws, "非法候选和观察不消费 RNG");
            AssertNoMapDiagnostics(illegal, draws);

            // 合法选择 Monster → 写回 + 进入既有战斗。
            var chooseRequest = Req("g04-choose-" + seed, Operation.Step,
                ("session_id", WireValue.Str(sessionId)),
                ("expected_observation_id", WireValue.Integer(observation)),
                ("candidate_id", WireValue.Str(pick)));
            var choose = host.Dispatch(chooseRequest);
            Check.True(choose.Ok, $"{seed}：合法选择 Monster 节点应当成功：{choose.Error?.Code}");
            var battle = (WireObject)Field(choose, "snapshot");
            var battleRun = EntityOfKind(battle, MapRunKindId);
            Check.Equal(pickNode, MapText(battleRun, "base-spire:Run.CurrentNode"),
                $"{seed}：选择后当前节点必须写回所选公开节点");
            Check.Equal(1L, MapInt(battleRun, "base-spire:Run.MapPhase"), $"{seed}：选择 Monster 后进入战斗阶段 1");
            Check.Equal(pickRow + 1, MapInt(battleRun, "base-spire:Run.MapFloor"),
                $"{seed}：地图层按所选节点行号 +1 推进");
            Check.True(MapBool(EntityOfRef(battle, pickNode), "base-spire:MapNode.Visited"),
                $"{seed}：所选节点必须写回已访问");
            Check.Equal(draws, LocalMapDraws(engine),
                $"{seed}：选择本身只读候选，不得额外消耗地图抽数");
            Check.True(LegalActions(battle).All(action => ActionType(action) != ChooseMapPointActionId),
                $"{seed}：战斗中（阶段 1）不得再出现地图选择候选（阶段守卫来自引擎）");
            Check.True(LegalActions(battle).Any(action => ActionType(action).EndsWith("PlayCard", StringComparison.Ordinal)),
                $"{seed}：进战斗后必须出现既有出牌候选（本包只是进入既有正常战斗）");

            AssertNoMapDiagnostics(choose, draws);
            hash = engine.State.StateHash();
            rng = engine.Random.TotalDraws;
            // 同 request 重发幂等：返回同一稳定边界，不再消耗抽数。
            var replay = host.Dispatch(chooseRequest);
            Check.True(replay.Ok, $"{seed}：同 request 重发必须幂等成功：{replay.Error?.Code}");
            Check.Equal(ObservationId(battle), ObservationId((WireObject)Field(replay, "snapshot")),
                $"{seed}：同 request 重发必须返回同一稳定边界");
            var replayRun = EntityOfKind((WireObject)Field(replay, "snapshot"), MapRunKindId);
            Check.Equal(draws, LocalMapDraws(engine),
                $"{seed}：同 request 重发不得重新消耗地图抽数");

            // 已消费的候选不得映射到新边界。
            var consumed = DispatchStep(host, sessionId, ObservationId(battle), pick);
            Check.Equal("illegal_candidate", ErrorCode(consumed), $"{seed}：已消费的候选必须被拒");

            // 过期观察号：候选合法（当前战斗候选）但观察号是旧的。
            var battleCandidate = LegalActions(battle)
                .First(action => ActionType(action).EndsWith("PlayCard", StringComparison.Ordinal));
            var stale = DispatchStep(host, sessionId, observation, CandidateId(battleCandidate));
            Check.Equal("stale_observation", ErrorCode(stale), $"{seed}：过期观察号必须被拒并提示重新 observe");
            var afterStale = EntityOfKind((WireObject)Field(Observe(host, sessionId), "snapshot"), MapRunKindId);
            Check.Equal(1L, MapInt(afterStale, "base-spire:Run.MapPhase"),
                $"{seed}：过期/已消费的提交都不得改地图阶段");
            Check.Equal(draws, LocalMapDraws(engine),
                $"{seed}：过期/已消费的提交都不得消耗地图抽数");
            Check.Equal(hash, engine.State.StateHash(), "幂等/过期/已消费提交不改状态");
            Check.Equal(rng, engine.Random.TotalDraws, "幂等/过期/已消费提交不消费 RNG");
            foreach (var response in new[] { replay, consumed, stale }) AssertNoMapDiagnostics(response, draws);
        }
    }

    /// <summary>
    /// G-05：dev-map 的**同一批**载荷要过 0.2 wire schema（C# 读同一份 schema 文件）；
    /// 并在进战斗后出一次牌：要么正常结算（战斗继续），要么由 base 显式抛故障——不得静默当作成功。
    /// </summary>
    private static void MapPayloadSchemaAndBattleStep()
    {
        var schemaPath = Path.Combine(RepositoryRoot(), "docs", "contracts", "net", "0.2", "wire.schema.json");
        Check.True(File.Exists(schemaPath), $"缺少 0.2 wire schema：{schemaPath}");
        var schema = LiteSchema.FromJson(File.ReadAllText(schemaPath));

        var host = NewHost(FormalDevScenario.CreateFactory(BaseSpireScope.DevMap), out _);
        var seed = MapEvidenceSeeds[0];
        var started = Start(host, "g05-start", seed);
        Check.True(started.Ok, $"dev-map start({seed}) 应当成功：{started.Error?.Code}");
        var sessionId = StringField(started, "session_id")!;
        var snapshot = (WireObject)Field(started, "snapshot");
        var observation = ObservationId(snapshot);
        var monster = LegalActions(snapshot)
            .First(action => ActionType(action) == ChooseMapPointActionId
                             && MapNodeTypeOf(snapshot, MapText(action, "base-spire:ChooseMapPoint.Node")) == "Monster");
        var choose = host.Dispatch(Req("g05-choose", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(observation)),
            ("candidate_id", WireValue.Str(CandidateId(monster)))));
        Check.True(choose.Ok, $"dev-map 选择 Monster 应当成功：{choose.Error?.Code}");
        var battle = (WireObject)Field(choose, "snapshot");
        var observed = Observe(host, sessionId);
        var error = host.Dispatch(Req("g05-illegal", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(ObservationId(battle))),
            ("candidate_id", WireValue.Str("not-a-candidate"))));

        var documents = new List<(string Role, WireValue Document)>
        {
            ("describe/result", host.Dispatch(Req("g05-describe", Operation.Describe)).ToWire()),
            ("get_catalog/result", host.Dispatch(Req("g05-catalog", Operation.GetCatalog,
                ("catalog_id", WireValue.Str(host.Profile.CatalogId)))).ToWire()),
            ("start/result", started.ToWire()),
            ("observe/result", observed.ToWire()),
            ("step/step(地图)/result", choose.ToWire()),
            ("error/illegal_candidate", error.ToWire()),
        };
        foreach (var (role, document) in documents)
        {
            var errors = schema.ValidateDefinition("Response", document);
            Check.True(errors.Count == 0,
                $"dev-map 0.2 载荷违反 schema（{role}）：{string.Join(" | ", errors.Take(3))}");
        }

        // 进战斗后出一次牌：正常结算，或 base 显式故障（本包不伪造终局、不刷 seed 绕开）。
        var play = LegalActions(battle).First(action =>
            ActionType(action).EndsWith("PlayCard", StringComparison.Ordinal));
        var (response, faultCode, faultMessage) = DispatchOrFault(host, sessionId, ObservationId(battle),
            CandidateId(play));
        if (faultCode is not null)
        {
            Check.True(!string.IsNullOrEmpty(faultMessage), $"base 故障必须以显式 code/message 表达：{faultCode}");
            Console.WriteLine($"      · {seed} 进战斗后出牌遇到 base 故障（如实记录，不伪装可继续）：{faultMessage}");
        }
        else if (response!.Ok)
        {
            var next = (WireObject)Field(response, "snapshot");
            var nextRun = EntityOfKind(next, MapRunKindId);
            Check.Equal(1L, MapInt(nextRun, "base-spire:Run.MapPhase"),
                "战斗中的出牌不得改地图阶段（地图与战斗状态分离）");
            Check.True(ObservationId(next) > ObservationId(battle), "正常结算的出牌必须推进稳定边界");
        }
        else
        {
            Check.True(ErrorCode(response).StartsWith("backend_fault", StringComparison.Ordinal),
                $"出牌被拒时必须显式报 backend_fault（本包不把失败伪装成正常结算）：{response.Error?.Code}");
        }
    }

    // Explicit in-process SDK diagnostics; never publish this value in a response.
    private static string LocalMapDraws(Session engine)
    {
        var type = engine.RuleSet.TypeByQualifiedId("base-spire:Run");
        return engine.State.GetField(engine.State.AllInstances(type.Id).Single(), type.FieldIndex("MapDraws")).AsString();
    }

    private static void AssertNoMapDiagnostics(ResponseDto response, string words)
    {
        var text = WireCanonical.Text(response.ToWire());
        Check.False(text.Contains("MapDraws", StringComparison.Ordinal), "普通响应不得包含地图诊断字段");
        Check.False(text.Contains(words, StringComparison.Ordinal), "普通响应不得包含地图重放词表载荷");
    }

    private static IReadOnlyList<WireObject> DeclarationRows(WireObject declarations, string table) =>
        ((WireArray)Member(declarations, table)).Items.OfType<WireObject>().ToList();

    private static WireObject? DeclarationRow(IReadOnlyList<WireObject> rows, string id) =>
        rows.SingleOrDefault(row => Member(row, "id") is WireString text && text.Value == id);

    private static WireObject EntityOfKind(WireObject snapshot, string kindId)
    {
        var matches = Entities(snapshot)
            .Where(entity => ((WireString)Member(entity, "kind_id")).Value == kindId)
            .ToList();
        Check.True(matches.Count == 1, $"快照必须恰好发布一个 {kindId} 实体（实际 {matches.Count}）");
        return matches[0];
    }

    private static WireObject EntityOfRef(WireObject snapshot, string instanceRef)
    {
        var matches = Entities(snapshot).Where(entity => InstanceId(entity) == instanceRef).ToList();
        Check.True(matches.Count == 1, $"公开引用 {instanceRef} 必须在快照里唯一解析（实际 {matches.Count}）");
        return matches[0];
    }

    /// <summary>读**已知**公开字段；字段存在但不是已知事实时返回 <c>null</c>（调用方必须显式处理，不猜）。</summary>
    private static WireValue? MapKnownField(WireObject entity, string qualifiedFieldId)
    {
        var fieldId = DerivedPublicProfile.FieldPrefix + qualifiedFieldId;
        foreach (var field in FieldsOf(entity))
        {
            if (Member(field, "field_id") is not WireString id || id.Value != fieldId) continue;
            if (TryMember(field, "state") is WireString state && state.Value == "known")
            {
                return TryMember(field, "value");
            }

            return null;
        }

        return null;
    }

    private static long MapInt(WireObject entity, string qualifiedFieldId) =>
        MapKnownField(entity, qualifiedFieldId) switch
        {
            WireInteger number => long.Parse(number.Digits),
            WireString text => long.Parse(text.Value),
            null => throw new CheckFailedException($"公开数值字段 {qualifiedFieldId} 不是已知事实"),
            var other => throw new CheckFailedException(
                $"公开数值字段 {qualifiedFieldId} 的形状不是整数：{other.GetType().Name}"),
        };

    private static string MapText(WireObject entity, string qualifiedFieldId) =>
        MapKnownField(entity, qualifiedFieldId) switch
        {
            WireString text => text.Value,
            WireInteger number => number.Digits,
            WireBool boolean => boolean.Value ? "true" : "false",
            null => throw new CheckFailedException($"公开字段 {qualifiedFieldId} 不是已知事实"),
            var other => throw new CheckFailedException(
                $"公开字段 {qualifiedFieldId} 的形状不是标量：{other.GetType().Name}"),
        };

    private static bool MapBool(WireObject entity, string qualifiedFieldId) =>
        MapKnownField(entity, qualifiedFieldId) switch
        {
            WireBool boolean => boolean.Value,
            WireString text => text.Value == "true",
            null => throw new CheckFailedException($"公开布尔字段 {qualifiedFieldId} 不是已知事实"),
            var other => throw new CheckFailedException(
                $"公开布尔字段 {qualifiedFieldId} 的形状不是布尔：{other.GetType().Name}"),
        };

    private static IReadOnlyList<string> MapNodeRefs(WireObject snapshot)
    {
        var sequences = ((WireArray)Member(snapshot, "sequences")).Items.OfType<WireObject>().ToList();
        var matches = sequences.Where(item =>
            Member(item, "type_id") is WireString id && id.Value == MapNodesSequenceId).ToList();
        Check.True(matches.Count == 1, $"快照必须恰好发布一条 {MapNodesSequenceId}（实际 {matches.Count}）");
        return ((WireArray)Member(matches[0], "elements")).Items.OfType<WireString>()
            .Select(item => item.Value)
            .ToList();
    }

    /// <summary>公开关系上的后继（排序后，便于与候选集合逐一比较）。</summary>
    private static IReadOnlyList<string> MapChildrenOf(WireObject snapshot, string parentRef) =>
        ((WireArray)Member(snapshot, "relations")).Items.OfType<WireObject>()
            .Where(relation => Member(relation, "type_id") is WireString id && id.Value == MapChildrenRelationId)
            .Where(relation => Member(relation, "source_ref") is WireString source && source.Value == parentRef)
            .Select(relation => ((WireString)Member(relation, "target_ref")).Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reference => reference, StringComparer.Ordinal)
            .ToList();

    /// <summary>公开类别 id 的最后一段（枚举成员名）；节点类型只从公开字段读，不从引擎序号推。</summary>
    private static string MapNodeTypeOf(WireObject snapshot, string nodeRef)
    {
        var text = MapText(EntityOfRef(snapshot, nodeRef), "base-spire:MapNode.Type");
        return text[(text.LastIndexOf(':') + 1)..];
    }
}
