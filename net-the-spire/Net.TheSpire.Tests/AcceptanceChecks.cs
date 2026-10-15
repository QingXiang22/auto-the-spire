using BaseSpire.Slice;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Tests;

/// <summary>
/// N-05 接口验收：<c>ai-net-verification.md</c> 的 V-01 至 V-10 与 V-15 在本切片上的**适用项**，逐条独立判据。
///
/// 证据类别写在检查名里：
/// <list type="bullet">
/// <item><b>[真实]</b>＝真实引擎 + 真实投影 + 真实控制宿主（本切片 <c>base-spire:combat-slice-v1</c>）的模拟运行；</item>
/// <item><b>[夹具/另计]</b>＝该子项由 fixture 层（A-03/A-05、N-01 跨端）覆盖，本套件只登记指针，不冒充真实证据；</item>
/// <item><b>[故障注入]</b>＝客户端侧由 <c>client/test_client.py</c> 的 faults 组覆盖（见 h-03 报告）。</item>
/// </list>
///
/// 判定工具可以读取内部状态（<see cref="ControlSession.EngineForTests"/>）来证明"公开面没有变化"，
/// 但内部值不进入任何公开快照或训练输入。
/// </summary>
internal static class AcceptanceChecks
{
    private const string PlayerViewId = "base-spire:PlayerView";
    private const string PlayCardId = "base-spire:PlayCard";
    private const string EndTurnId = "base-spire:EndTurn";
    private const string RunToken = "acc";

    /// <summary>对照时被归一化的控制元数据键（contract-artifacts §3：排除 session_id/observation_id/不透明句柄）。</summary>
    private static readonly HashSet<string> VolatileKeys =
        new(["session_id", "observation_id", "candidate_id"], StringComparer.Ordinal);

    public static IEnumerable<(string Name, Action Body)> Build()
    {
        yield return ("V-01 同一目录不同注册顺序 → 规范字节与摘要一致；错身份在推进前拒绝 [真实]",
            CatalogOrderIndependence);
        yield return ("V-02 隐藏内部牌序与 RNG 游标不进公开事实；纯读不消费 RNG [真实]",
            HiddenOrderAndRngStayOutOfPublicFacts);
        yield return ("V-03 已知位置在真实链路上保留/失效，且不跨会话残留 [真实]", KnownPositionLifecycle);
        yield return ("V-04 过期观察不推进、无半结算快照、能力声明显式 [真实]", StaleAndSingleWriter);
        yield return ("V-05 同名牌多实例：候选引用准确、不被合一、语义键稳定 [真实]", SameNameInstancesStayDistinct);
        yield return ("V-06 真实链路失败路径带错误码与路径且不推进；知识状态子项另计 [真实/夹具另计]",
            FailurePathsAndKnowledgeStates);
        yield return ("V-07 原样重发只执行一次、换载荷 conflict、无公开 rollback/fork [真实]", DedupAndReplayCapability);
        yield return ("V-08 目录身份绑定：错目录在推进前拒绝、内容变化改变摘要 [真实]", CatalogIdentityBinding);
        yield return ("V-09 相同种子+动作语义序列重放：公开事实与候选语义对齐 [真实]", SeedActionReplay);
        yield return ("V-10 终止 ≠ 控制关闭 ≠ 技术中断，不伪造胜负 [真实]", TerminalVersusInterruption);
        yield return ("V-15 成对状态保留决策相关公开事实差异 + 目录引用闭合 [真实]", PairedStatesAndReferenceClosure);
        yield return ("N-05 公开动作到通用决策响应的适配（选择续接与实例引用） [真实]",
            DecisionResponseAdaptation);
    }

    // -- V-01 ------------------------------------------------------------------------------------------------

    private static void CatalogOrderIndependence()
    {
        var profile = SlicePublicProfile.Create();
        var canonical = CatalogProjector.Project(profile);
        var reordered = CatalogProjector.Project(Reordered(profile));

        Check.Equal(WireCanonical.Text(canonical.ToWire()), WireCanonical.Text(reordered.ToWire()),
            "同一目录以不同注册顺序必须产出同一规范字节（contract-artifacts §3 的 id 排序）");
        Check.Equal(canonical.Digest(), reordered.Digest(), "目录摘要与注册顺序无关");
        Check.Equal(profile.CatalogId, canonical.CatalogId, "目录身份与运行时身份一致");

        // 错身份必须在**推进前**拒绝，且不得留下会话。
        var host = NewHost();
        var wrongIdentity = new IdentityDto(IdentityDto.ExpectedProtocolVersion, profile.RulesetId,
            "fixture:other-catalog", profile.BackendProfileId);
        var rejected = host.Dispatch(Req("s-bad", Operation.Start,
            ("expected_identity", wrongIdentity.ToWire()),
            ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile))));
        Check.Equal("version_mismatch", ErrorCode(rejected), "错目录身份必须 version_mismatch");
        Check.Equal(0, host.SessionCount, "身份不符不得创建会话（推进前拒绝）");
    }

    private static PublicSnapshotProfile Reordered(PublicSnapshotProfile source) => new(
        source.RulesetId, source.CatalogId, source.BackendProfileId,
        Reverse(source.Kinds), Reverse(source.Collections), Reverse(source.Relations),
        Reverse(source.Actions), source.AnswerAction, Reverse(source.Terminals), source.KnownPositions);

    private static IReadOnlyDictionary<string, T> Reverse<T>(IReadOnlyDictionary<string, T> source) =>
        source.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    // -- V-02 ------------------------------------------------------------------------------------------------

    private static void HiddenOrderAndRngStayOutOfPublicFacts()
    {
        var (host, sessionId, session) = StartedHost();
        var before = Text(Snapshot(host, sessionId));
        var drawsBefore = session.EngineForTests.Random.TotalDraws;
        var versionBefore = session.EngineForTests.StateVersion;

        // 纯读：多次观察与合法候选查询不得改变状态版本或 RNG 游标。
        _ = Snapshot(host, sessionId);
        _ = Snapshot(host, sessionId);
        Check.Equal(versionBefore, session.EngineForTests.StateVersion, "观察不得改状态版本");
        Check.Equal(drawsBefore, session.EngineForTests.Random.TotalDraws, "观察/候选查询不得消费 RNG");

        // 隐藏通道：把抽牌堆的内部顺序倒过来（提供方判定工具，不是公开动作）。
        var state = session.EngineForTests.State;
        var draw = session.EngineForTests.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:DrawPile").Id;
        var internalBefore = state.CollectionMembers(draw).Select(member => member.Value).ToList();
        state.RunAtomic(() =>
        {
            var members = state.CollectionMembers(draw).ToList();
            foreach (var member in members) state.RemoveFromCollection(draw, member);
            // 逐个插到最前面 → 内部顺序恰好被倒置。
            foreach (var member in members) state.AddToCollection(draw, member, InsertPosition.Start);
        });
        Check.Sequence(internalBefore.AsEnumerable().Reverse(), state.CollectionMembers(draw).Select(m => m.Value),
            "判定工具确实改变了隐藏内部顺序");

        var after = Text(Snapshot(host, sessionId));
        Check.Equal(before, after,
            "隐藏内部牌序不得进入公开快照（数组顺序/ID/候选都不携带它）");

        // RNG 游标推进同样不进公开事实：多消费一次具名流后公开面仍相同。
        session.EngineForTests.Random.DrawInt("base-spire:probe", 0, 10);
        Check.True(session.EngineForTests.Random.TotalDraws > drawsBefore, "判定工具确实推进了 RNG 游标");
        Check.Equal(before, Text(Snapshot(host, sessionId)), "RNG 游标不得进入公开快照");
    }

    // -- V-03 ------------------------------------------------------------------------------------------------

    private static void KnownPositionLifecycle()
    {
        var first = NewHost();
        var sessionId = StartSession(first, "v03-a");
        var card = PlayHeadbutt(first, sessionId);
        var pile = Snapshot(first, sessionId) is WireObject snapshot
            ? Piles(snapshot)
            : throw new CheckFailedException("快照必须是对象");

        var withPosition = pile.Single(p => KnownPositions(p).Count > 0);
        var position = KnownPositions(withPosition).Single();
        Check.Equal(PublicRef(first.GetSessionForTests(sessionId), card),
            ((WireString)position.Members.Single(m => m.Key == "card_ref").Value).Value,
            "第一份快照即给出真实实例引用");
        Check.Equal("0", ((WireInteger)position.Members.Single(m => m.Key == "position").Value).Digits,
            "置顶后的绝对位置为 0");

        // 抽到那张牌：知识必须失效，而不是从隐藏实时索引重算。
        Submit(first, sessionId, EndTurnId);
        Submit(first, sessionId, EndTurnId);
        var after = Piles((WireObject)Snapshot(first, sessionId)!);
        Check.Equal(0, after.Sum(p => KnownPositions(p).Count), "抽到已知牌后位置必须失效");

        // W-02：**引用跨授权周期稳定**——被揭示过的实例在"撤销 + 换了公开位置"之后，
        // 仍必须以**同一不透明公开引用**出现在公开集合里（编号不随授权状态或所在牌堆改变）。
        var knownRef = ((WireString)position.Members.Single(m => m.Key == "card_ref").Value).Value;
        var stillPublished = after
            .Where(p => VisibleMembers(p).Contains(knownRef, StringComparer.Ordinal))
            .ToList();
        Check.True(stillPublished.Count > 0,
            $"撤销后该实例仍以同一公开引用出现在公开集合里（{knownRef}）");

        // 另一个会话：不得残留上一局的知识。
        var otherSession = StartSession(first, "v03-b");
        var fresh = Piles((WireObject)Snapshot(first, otherSession)!);
        Check.Equal(0, fresh.Sum(p => KnownPositions(p).Count), "新会话不得残留已知位置");

        // 另一个宿主（跨进程）：同样不得残留。
        var secondHost = NewHost();
        var secondSession = StartSession(secondHost, "v03-c");
        var elsewhere = Piles((WireObject)Snapshot(secondHost, secondSession)!);
        Check.Equal(0, elsewhere.Sum(p => KnownPositions(p).Count), "另一宿主不得残留已知位置");
    }

    // -- V-04 ------------------------------------------------------------------------------------------------

    private static void StaleAndSingleWriter()
    {
        var host = NewHost();
        var sessionId = StartSession(host, "v04");
        var observed = Observe(host, sessionId);
        var observation = ObservationIdOf(observed);
        var candidate = FirstCandidateId(observed);

        var step = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(observation)),
            ("candidate_id", WireValue.Str(candidate))));
        Check.True(step.Ok, $"step 应当成功：{step.Error?.Code}");
        var afterStep = ObservationIdOf(Observe(host, sessionId));

        // 同一个旧 observation 用新 request_id 重用 → stale，且不得额外推进。
        var stale = host.Dispatch(Req("s-2", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(observation)),
            ("candidate_id", WireValue.Str(candidate))));
        Check.Equal("stale_observation", ErrorCode(stale), "过期观察必须 stale_observation");
        Check.Equal(afterStep, ObservationIdOf(Observe(host, sessionId)), "过期请求不得推进游戏");

        // 半结算快照：账本里不得留下结算中条目，且当前候选与当前版本一致。
        Check.False(host.GetSessionForTests(sessionId).Settling, "不得留下半结算条目");
        var current = Observe(host, sessionId);
        Check.Equal(ObservationIdOf(current), afterStep, "候选必须与当前版本一致");

        // 能力诚实：并发写者没有被声明为支持（同步单写者）。
        var guarantees = Capabilities(host).ExecutionGuarantees;
        Check.True(guarantees.RequestStatus, "必须声明 request_status");
        Check.True(guarantees.ActiveDeduplication, "必须声明活跃去重");
        Check.False(guarantees.PublicFork, "不得声明公开 fork");
        Check.False(guarantees.PublicStateRestore, "不得声明公开状态恢复");
    }

    // -- V-05 ------------------------------------------------------------------------------------------------

    private static void SameNameInstancesStayDistinct()
    {
        var host = NewHost();
        var sessionId = StartSession(host, "v05");
        var observed = Observe(host, sessionId);
        var snapshot = (WireObject)Field(observed, "snapshot")!;

        // 起始手牌里同名牌多实例：候选必须各自引用一个实例，且不被合一。
        var strikeRefs = Entities(snapshot)
            .Where(entity => ContentId(entity) == "base-spire:strike-ironclad")
            .Select(entity => InstanceId(entity))
            .ToList();
        Check.True(strikeRefs.Count >= 2, $"起始手牌应有多个同名实例（实际 {strikeRefs.Count}）");
        Check.Equal(strikeRefs.Count, strikeRefs.Distinct(StringComparer.Ordinal).Count(), "同名实例不得被合一");

        // 每个手牌实例都是一个独立候选（同名牌的不同实例是不同动作，protocol.md:100）。
        var playActions = LegalActions(snapshot)
            .Where(action => ActionType(action) == "net:action:base-spire:PlayCard")
            .ToList();
        Check.Equal(strikeRefs.Count, playActions.Count, "每个手牌实例都有一个独立候选");
        var keys = LegalActions(snapshot).Select(SemanticKey).ToList();
        Check.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count(), "候选语义键不得重复（否则概率总量会被扭曲）");

        var play = playActions.First(action => CandidateRefersTo(action, strikeRefs[0]));
        var chosen = CandidateId(play);
        var playFields = (WireArray)play.Members.Single(m => m.Key == "fields").Value;
        var chosenRef = ((WireString)((WireObject)playFields.Items[0]).Members
            .Single(m => m.Key == "value").Value).Value;
        Check.True(strikeRefs.Contains(chosenRef), "候选字段必须引用真实卡牌实例");

        var step = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(ObservationIdOf(observed))),
            ("candidate_id", WireValue.Str(chosen))));
        Check.True(step.Ok, $"出牌应当成功：{step.Error?.Code}");

        // 其余同名实例必须仍然存在且身份不变。
        var after = (WireObject)Field(Observe(host, sessionId), "snapshot")!;
        var remaining = Entities(after).Select(InstanceId).ToHashSet(StringComparer.Ordinal);
        Check.True(remaining.Contains(chosenRef), "出牌后该实例仍以同一公开引用存在（进入结果牌区）");
        foreach (var other in strikeRefs.Where(reference => reference != chosenRef))
        {
            Check.True(remaining.Contains(other), $"未被选中的同名实例必须保持身份：{other}");
        }
    }

    // -- V-06 ------------------------------------------------------------------------------------------------

    private static void FailurePathsAndKnowledgeStates()
    {
        var host = NewHost();
        var sessionId = StartSession(host, "v06");
        var observed = Observe(host, sessionId);
        var before = ObservationIdOf(observed);

        // 非法候选：明确的错误码 + 不推进。
        var illegal = host.Dispatch(Req("s-bad", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(before)),
            ("candidate_id", WireValue.Str("candidate-not-issued"))));
        Check.Equal("illegal_candidate", ErrorCode(illegal), "未知候选必须 illegal_candidate");
        Check.Equal(before, ObservationIdOf(Observe(host, sessionId)), "失败请求不得推进游戏");

        // 载荷形状错误必须带路径，且不得创建/推进任何东西。
        var shape = host.Dispatch(Req("o-bad", Operation.Observe));
        Check.Equal("invalid_input", ErrorCode(shape), "缺 session_id 必须 invalid_input");
        Check.True(!string.IsNullOrWhiteSpace(shape.Error!.Path), $"形状错误必须带具体路径（得到 {shape.Error.Path}）");

        // 非规范十进制 seed：wire 边界必须拒绝而不是静默改写。注意 `Dispatch` 是**进程内**调用，
        // 不走 `RequestDto.FromWire`，所以这里直接检 wire 解码层（对应 I-76）。
        var seedRequest = WireValue.Object(
            ("request_id", WireValue.Str("s-seed")),
            ("operation", WireValue.Str("start")),
            ("payload", WireValue.Object(
                ("expected_identity", Identity(host).ToWire()),
                ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)),
                ("seed", WireValue.Str("007")))));
        Check.Fault(() => RequestDto.FromWire(seedRequest), "非规范十进制 seed 必须被 wire 层拒绝", "I-76");

        // 真实数据上牌堆四类不变量的可判定部分：card_ref ⊆ visible_members、位置在 total_count 内、成员唯一。
        var snapshot = (WireObject)Field(observed, "snapshot")!;
        foreach (var pile in Piles(snapshot))
        {
            var members = VisibleMembers(pile);
            Check.Equal(members.Count, members.Distinct(StringComparer.Ordinal).Count(), "visible_members 必须唯一");
            var total = long.Parse(((WireInteger)pile.Members.Single(m => m.Key == "total_count").Value).Digits);
            Check.True(members.Count <= total, "visible_members 不得超过 total_count");
            foreach (var position in KnownPositions(pile))
            {
                var card = ((WireString)position.Members.Single(m => m.Key == "card_ref").Value).Value;
                var index = long.Parse(((WireInteger)position.Members.Single(m => m.Key == "position").Value).Digits);
                Check.True(members.Contains(card, StringComparer.Ordinal), "known_positions 的 card_ref 必须在 visible_members 内");
                Check.True(index >= 0 && index < total, "known_positions 的 position 必须在 total_count 内");
            }
        }

        // 知识状态子项（known/unknown/unavailable/省略/padding 与悬空引用等反例）由 fixture 层覆盖：
        // A-03（ai-the-spire validate 组）与 N-01 跨端负例。本切片不产生 unknown/unavailable，因此
        // 这里只登记能力边界，不冒充真实证据。
    }

    // -- V-07 ------------------------------------------------------------------------------------------------

    private static void DedupAndReplayCapability()
    {
        var host = NewHost();
        var sessionId = StartSession(host, "v07");
        var observed = Observe(host, sessionId);
        var observation = ObservationIdOf(observed);
        var candidate = FirstCandidateId(observed);

        var request = Req("s-1", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(observation)),
            ("candidate_id", WireValue.Str(candidate)));
        var first = host.Dispatch(request);
        Check.True(first.Ok, "首次 step 应当成功");
        var afterFirst = ObservationIdOf(Observe(host, sessionId));

        var replay = host.Dispatch(request);
        Check.Equal(WireCanonical.Text(first.ToWire()), WireCanonical.Text(replay.ToWire()),
            "原样重发必须逐字节返回原响应");
        Check.Equal(afterFirst, ObservationIdOf(Observe(host, sessionId)),
            "原样重发不得第二次执行（观察版本不变）");

        var conflict = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(afterFirst)),
            ("candidate_id", WireValue.Str(candidate))));
        Check.Equal("request_conflict", ErrorCode(conflict), "同 id 换载荷必须 request_conflict");
        Check.Equal(afterFirst, ObservationIdOf(Observe(host, sessionId)), "冲突请求不得推进游戏");

        // NoSL：公开能力里不得出现 rollback/load/fork。
        var capabilities = Capabilities(host);
        Check.False(capabilities.ExecutionGuarantees.PublicFork, "不得提供公开 fork");
        Check.False(capabilities.ExecutionGuarantees.PublicStateRestore, "不得提供公开 load");
        Check.False(capabilities.Operations.Contains("attach", StringComparer.Ordinal),
            "模拟后端必须声明不支持 attach（不得默默重开）");
    }

    // -- V-08 ------------------------------------------------------------------------------------------------

    private static void CatalogIdentityBinding()
    {
        var host = NewHost();
        var profile = SlicePublicProfile.Create();

        var unknown = host.Dispatch(Req("g-bad", Operation.GetCatalog, ("catalog_id", WireValue.Str("fixture:other"))));
        Check.Equal("version_mismatch", ErrorCode(unknown), "未知目录必须 version_mismatch");

        var started = host.Dispatch(StartRequest(host, "s-1"));
        Check.True(started.Ok, "正确身份应当能启动");
        Check.Equal("session-acc-1", StringField(started, "session_id"), "会话句柄");

        // 目录**内容**变化必须改变摘要（catalog_id 字符串本身由提供方声明，需同步变更——见报告 §未覆盖）。
        var baseline = CatalogProjector.Project(profile).Digest();
        var grown = new PublicSnapshotProfile(profile.RulesetId, profile.CatalogId, profile.BackendProfileId,
            Grown(profile.Kinds), profile.Collections, profile.Relations, profile.Actions, profile.AnswerAction,
            profile.Terminals, profile.KnownPositions);
        Check.True(CatalogProjector.Project(grown).Digest() != baseline,
            "新增内容必须改变目录摘要（否则新内容会复用旧身份）");
    }

    /// <summary>在目录里加一个内容类型，模拟"新增内容建新目录"。</summary>
    private static IReadOnlyDictionary<string, PublicKindRule> Grown(IReadOnlyDictionary<string, PublicKindRule> kinds)
    {
        var copy = kinds.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var card = copy["base-spire:Card"];
        copy["base-spire:Card"] = card with { ContentIds = [.. card.ContentIds, "base-spire:new-card"] };
        return copy;
    }

    // -- V-09 ------------------------------------------------------------------------------------------------

    private static void SeedActionReplay()
    {
        var first = NewHost();
        var second = NewHost();
        var firstSession = StartSession(first, "v09-a");
        var secondSession = StartSession(second, "v09-b");

        var firstFacts = new List<string>();
        var secondFacts = new List<string>();
        var firstCandidates = new List<string>();
        var secondCandidates = new List<string>();

        for (var step = 0; step < 4; step++)
        {
            var firstSnapshot = (WireObject)Field(Observe(first, firstSession), "snapshot")!;
            // 第二局插入额外只读查询：它们不得改变任何事实或 RNG。
            _ = Observe(second, secondSession);
            var secondSnapshot = (WireObject)Field(Observe(second, secondSession), "snapshot")!;

            firstFacts.Add(Text(firstSnapshot));
            secondFacts.Add(Text(secondSnapshot));
            firstCandidates.Add(string.Join("|", LegalActions(firstSnapshot).Select(SemanticKey)));
            secondCandidates.Add(string.Join("|", LegalActions(secondSnapshot).Select(SemanticKey)));

            var action = SmallestSemanticKey(firstSnapshot);
            Step(first, firstSession, firstSnapshot, action);
            // 第二局用**语义等价**的动作（不依赖句柄）：按语义键重新选出同一动作。
            var mirrored = SmallestSemanticKey(secondSnapshot);
            Check.Equal(SemanticKey(action), SemanticKey(mirrored),
                $"同一状态下按语义键选出的动作必须一致（第 {step + 1} 步）");
            Step(second, secondSession, secondSnapshot, mirrored);
        }

        Check.Sequence(firstFacts, secondFacts, "相同种子+动作语义序列必须给出相同的公开事实");
        Check.Sequence(firstCandidates, secondCandidates, "候选语义序列必须一致");
    }

    // -- V-10 ------------------------------------------------------------------------------------------------

    private static void TerminalVersusInterruption()
    {
        var host = NewHost();
        var sessionId = StartSession(host, "v10");

        // 控制关闭 ≠ 游戏终止：close 不是胜负结论。
        var closed = host.Dispatch(Req("c-1", Operation.Close, ("session_id", WireValue.Str(sessionId))));
        Check.True(closed.Ok, "close 应当成功");
        Check.Equal("closed", StringField(closed, "session_status"), "close 结果包装固定");
        Check.True(closed.Result is WireObject closedResult && !HasKey(closedResult, "terminal_result"),
            "控制关闭不得携带终局结果（不等于游戏已终止）");

        // 真实到达终止：一直结束回合直到战斗结束。
        var battle = NewHost();
        var battleSession = StartSession(battle, "v10-b");
        var terminal = DriveToTerminal(battle, battleSession, maxSteps: 80);
        Check.True(terminal is not null, "受控切片必须能在有限回合内到达终止");
        var terminalSnapshot = terminal!;
        Check.Equal("terminal", ((WireString)terminalSnapshot.Members.Single(m => m.Key == "status").Value).Value,
            "终止快照 status=terminal");
        Check.Equal(0, LegalActions(terminalSnapshot).Count, "终止快照不得有候选");
        var terminalResult = (WireObject)terminalSnapshot.Members.Single(m => m.Key == "terminal_result").Value;
        var outcome = ((WireString)terminalResult.Members.Single(m => m.Key == "outcome").Value).Value;
        Check.True(outcome is "win" or "loss", $"outcome 必须是 win/loss，实际 {outcome}");
        var reason = ((WireString)terminalResult.Members.Single(m => m.Key == "reason_id").Value).Value;
        Check.Contains(CatalogProjector.Project(SlicePublicProfile.Create()).Declarations.TerminalReasons,
            declaration => declaration.Id == reason, "终局原因必须在目录里声明");

        // 技术中断（backend_fault）不伪装成胜负：错误响应的 retry_mode 是 stop，且没有终局结果。
        Check.Equal("faulted", PublicSnapshotProjector.MapSessionStatus(SessionStatus.Faulted).SessionStatus,
            "faulted 是独立状态");
        Check.False(PublicSnapshotProjector.MapSessionStatus(SessionStatus.Faulted).PublishesSnapshot,
            "faulted 不发布快照（不得伪造终止快照）");

        // 轨迹/奖励/μ 的子项属 AI 侧（A-05 的样例侧与 L-01a/L-02a/L-03a 工程），不在本层冒充。
    }

    // -- V-15 ------------------------------------------------------------------------------------------------

    private static void PairedStatesAndReferenceClosure()
    {
        var host = NewHost();
        var sessionId = StartSession(host, "v15");
        var baseline = (WireObject)Field(Observe(host, sessionId), "snapshot")!;
        var baselineText = Normalize(baseline);

        // 成对状态：只改一个**决策相关**的公开事实（玩家能量），差异必须保留在 wire 里。
        var session = host.GetSessionForTests(sessionId);
        var player = session.EngineForTests.State.AllInstances(
                session.EngineForTests.RuleSet.TypeByQualifiedId("base-spire:Player").Id)
            .Single();
        var energy = session.EngineForTests.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("Energy");
        session.EngineForTests.State.RunAtomic(() => session.EngineForTests.State.SetField(player, energy, Value.Of(1L)));
        var changedSnapshot = (WireObject)Field(Observe(host, sessionId), "snapshot")!;
        var changed = Normalize(changedSnapshot);
        Check.True(baselineText != changed, "决策相关的公开事实差异必须保留在 wire 里（不得被聚合抹掉）");

        // 目录引用闭合：快照里出现的每个 id 都必须在**已发射的目录文档**里声明。
        // 注意必须核对发射物而不是 profile 的内部清单——只查内部清单会放过"清单有、wire 没有"的缺口。
        var catalog = CatalogProjector.Project(SlicePublicProfile.Create()).Declarations;
        var entityKinds = catalog.EntityKinds.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var declaredFields = catalog.Fields.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var declaredContent = catalog.ContentTypes.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        var declaredActions = catalog.ActionTypes.Select(d => d.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var entity in Entities(changedSnapshot))
        {
            Check.True(entityKinds.Contains(KindId(entity)), $"实体 kind_id 必须在目录声明：{KindId(entity)}");
            foreach (var field in FieldsOf(entity))
            {
                var fieldId = ((WireString)field.Members.Single(m => m.Key == "field_id").Value).Value;
                Check.True(declaredFields.Contains(fieldId), $"字段必须在目录声明：{fieldId}");
            }

            var content = ContentId(entity);
            if (content is not null)
            {
                Check.True(declaredContent.Contains(content), $"内容 ID 必须在目录声明：{content}");
            }
        }

        var pileKinds = catalog.PileTypes.Select(declaration => declaration.KindId)
            .ToHashSet(StringComparer.Ordinal);
        var entitiesById = Entities(changedSnapshot).ToDictionary(InstanceId, entity => entity, StringComparer.Ordinal);
        foreach (var pile in Piles(changedSnapshot))
        {
            var reference = ((WireString)pile.Members.Single(m => m.Key == "pile_ref").Value).Value;
            Check.True(entitiesById.TryGetValue(reference, out var entity),
                $"pile_ref 必须引用本快照的实体：{reference}");
            Check.True(pileKinds.Contains(KindId(entity!)),
                $"pile 实体的 kind_id 必须是已声明 pile_type：{KindId(entity!)}");
        }

        foreach (var action in LegalActions(changedSnapshot))
        {
            Check.True(declaredActions.Contains(ActionType(action)), $"动作类型必须在目录声明：{ActionType(action)}");
        }
    }

    // -- N-05 决策适配 ---------------------------------------------------------------------------------------

    private static void DecisionResponseAdaptation()
    {
        var host = NewHost();
        var sessionId = StartSession(host, "n05-decision");
        var card = PlayHeadbutt(host, sessionId, expectDecision: true);

        // 暂停期：候选是 net 自声明的决策应答，其字段必须引用真实实例（被弃牌堆中的牌）。
        var paused = (WireObject)Field(Observe(host, sessionId), "snapshot")!;
        Check.Equal("decision", ((WireString)paused.Members.Single(m => m.Key == "status").Value).Value,
            "选中带选择的牌后必须停在 decision");
        var answers = LegalActions(paused)
            .Where(action => ActionType(action) == "net:action:net.answer_decision")
            .ToList();
        Check.True(answers.Count > 0, "决策期必须发布 net.answer_decision 候选");
        foreach (var answer in answers)
        {
            var value = ((WireString)((WireObject)((WireArray)answer.Members
                .Single(m => m.Key == "fields").Value).Items[0]).Members.Single(m => m.Key == "value").Value).Value;
            Check.True(Entities(paused).Any(entity => InstanceId(entity) == value),
                $"决策候选必须引用快照内的真实实例：{value}");
        }

        // 续接：应答后游戏继续，且同一张牌被放回抽牌堆顶（公开事实可核对）。
        var chosen = CandidateId(answers[0]);
        var step = host.Dispatch(Req("d-1", Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(ObservationIdOf(Observe(host, sessionId)))),
            ("candidate_id", WireValue.Str(chosen))));
        Check.True(step.Ok, $"决策应答应当被接受：{step.Error?.Code}");
        var resumed = (WireObject)Field(Observe(host, sessionId), "snapshot")!;
        Check.True(((WireString)resumed.Members.Single(m => m.Key == "status").Value).Value is "decision" or "terminal",
            "应答后必须回到稳定边界");
        Check.True(Entities(resumed).Any(entity =>
                InstanceId(entity) == PublicRef(host.GetSessionForTests(sessionId), card)),
            "被处理的牌必须仍以同一公开引用存在");
    }

    // -- 夹具与助手 ------------------------------------------------------------------------------------------

    private static ControlHost NewHost() => new(
        Rules(), SlicePublicProfile.Create(), SliceManifest.Player, SliceManifest.PlayerRole, PlayerViewId,
        20261001UL, 4096, RunToken,
        // 切片包声明的计算资产（源预排序 `SourceStableShuffleOrder`、敌人加权选取等）需要宿主装后端；
        // 后端由本组合层（认识 base 包的一侧）提供，控制层只把它转交引擎。缺它就变成执行期 Unsupported。
        computeBackend: BaseSpireComputeBackend.Instance);

    private static RuleSet Rules()
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: true).Build());
        return builder.Build();
    }

    private static (ControlHost Host, string SessionId, ControlSession Session) StartedHost()
    {
        var host = NewHost();
        var sessionId = StartSession(host, "v02");
        return (host, sessionId, host.GetSessionForTests(sessionId));
    }

    private static string StartSession(ControlHost host, string requestId)
    {
        var started = host.Dispatch(StartRequest(host, requestId));
        Check.True(started.Ok, $"start 应当成功：{started.Error?.Code}");
        return StringField(started, "session_id")!;
    }

    /// <summary>
    /// 旧 0.1 切片场景的 start 请求：身份与 scenario_profile 都取**实际传入的宿主**，
    /// 不再读一个忽略所选 source 的静态常量（那会让检查对着另一个身份发请求）。
    /// </summary>
    private static RequestDto StartRequest(ControlHost host, string id)
    {
        var profile = host.Profile;
        return Req(id, Operation.Start,
            ("expected_identity", new IdentityDto(profile.ProtocolVersion, profile.RulesetId,
                profile.CatalogId, profile.BackendProfileId).ToWire()),
            ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)));
    }

    private static IdentityDto Identity(ControlHost host)
    {
        var profile = host.Profile;
        return new IdentityDto(profile.ProtocolVersion, profile.RulesetId, profile.CatalogId,
            profile.BackendProfileId);
    }

    private static WireValue? Snapshot(ControlHost host, string sessionId) => Field(Observe(host, sessionId), "snapshot");

    private static ResponseDto Observe(ControlHost host, string sessionId) =>
        host.Dispatch(Req("o-" + Guid.NewGuid().ToString("N")[..8], Operation.Observe,
            ("session_id", WireValue.Str(sessionId))));

    private static void Submit(ControlHost host, string sessionId, string actionEngineId)
    {
        var observed = Observe(host, sessionId);
        var snapshot = (WireObject)Field(observed, "snapshot")!;
        var action = LegalActions(snapshot).First(a => a.Members.Single(m => m.Key == "action_type").Value is WireString s
            && s.Value.EndsWith(actionEngineId.Split(':')[^1], StringComparison.Ordinal));
        Step(host, sessionId, snapshot, action);
    }

    private static void Step(ControlHost host, string sessionId, WireObject snapshot, WireObject action)
    {
        var response = host.Dispatch(Req("s-" + Guid.NewGuid().ToString("N")[..8], Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(
                long.Parse(((WireInteger)snapshot.Members.Single(m => m.Key == "observation_id").Value).Digits))),
            ("candidate_id", WireValue.Str(CandidateId(action)))));
        Check.True(response.Ok, $"step 应当成功：{response.Error?.Code} {response.Error?.Message}");
    }

    private static InstanceId PlayHeadbutt(ControlHost host, string sessionId, bool expectDecision = false)
    {
        var session = host.GetSessionForTests(sessionId);
        var state = session.EngineForTests.State;
        var rules = session.EngineForTests.RuleSet;
        var cardType = rules.TypeByQualifiedId("base-spire:Card");
        var draw = rules.Collections.Single(c => c.QualifiedId == "base-spire:DrawPile").Id;
        var hand = rules.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id;
        var discard = rules.Collections.Single(c => c.QualifiedId == "base-spire:DiscardPile").Id;

        // 受控测试场景：头槌入手、一张打击进弃牌堆（与切片检查同一手法，不改规则）。
        var headbutt = Relocate(session, "base-spire:headbutt", draw, hand);
        Relocate(session, "base-spire:strike-ironclad", hand, discard);

        var observed = Observe(host, sessionId);
        var snapshot = (WireObject)Field(observed, "snapshot")!;
        var headbuttRef = PublicRef(session, headbutt);
        var action = LegalActions(snapshot).Single(a =>
            ActionType(a) == "net:action:base-spire:PlayCard" && CandidateRefersTo(a, headbuttRef));
        Step(host, sessionId, snapshot, action);

        // 目标选择（敌人），随后才是弃牌选择。
        var target = Observe(host, sessionId);
        var targetSnapshot = (WireObject)Field(target, "snapshot")!;
        var targetAnswer = LegalActions(targetSnapshot)
            .First(a => ActionType(a) == "net:action:net.answer_decision");
        Step(host, sessionId, targetSnapshot, targetAnswer);

        if (expectDecision)
        {
            return headbutt;
        }

        var choice = Observe(host, sessionId);
        var choiceSnapshot = (WireObject)Field(choice, "snapshot")!;
        var discardAnswer = LegalActions(choiceSnapshot)
            .First(a => ActionType(a) == "net:action:net.answer_decision");
        var discardedRef = AnswerRefersTo(discardAnswer);
        Step(host, sessionId, choiceSnapshot, discardAnswer);
        return DiscardedToInstance(session, discardedRef);
    }

    /// <summary>W-02：公开句柄从**投影绑定表**读取（不再由引擎创建序号推导）。</summary>
    private static string PublicRef(ControlSession session, InstanceId instance) =>
        session.Snapshot().InstanceRefs[instance];
    private static InstanceId DiscardedToInstance(ControlSession session, string publicRef)
    {
        foreach (var instance in session.EngineForTests.State.AllInstances())
        {
            if (PublicRef(session, instance) == publicRef)
            {
                return instance;
            }
        }

        throw new CheckFailedException($"找不到公开引用 {publicRef} 对应的实例");
    }

    private static InstanceId Relocate(ControlSession session, string contentId, int from, int to)
    {
        var state = session.EngineForTests.State;
        var cardType = session.EngineForTests.RuleSet.TypeByQualifiedId("base-spire:Card");
        var definition = cardType.FieldIndex("DefinitionId");
        var card = state.CollectionMembers(from).FirstOrDefault(candidate =>
            state.GetField(candidate, definition).AsString() == contentId);
        if (card == default)
        {
            throw new CheckFailedException($"源牌区里没有 '{contentId}'");
        }

        state.RunAtomic(() =>
        {
            state.RemoveFromCollection(from, card);
            state.AddToCollection(to, card);
        });
        return card;
    }

    private static WireObject? DriveToTerminal(ControlHost host, string sessionId, int maxSteps)
    {
        for (var step = 0; step < maxSteps; step++)
        {
            var snapshot = (WireObject)Field(Observe(host, sessionId), "snapshot")!;
            var status = ((WireString)snapshot.Members.Single(m => m.Key == "status").Value).Value;
            if (status == "terminal")
            {
                return snapshot;
            }

            var endTurn = LegalActions(snapshot)
                .FirstOrDefault(a => ActionType(a) == "net:action:base-spire:EndTurn");
            if (endTurn is null)
            {
                return null;
            }

            Step(host, sessionId, snapshot, endTurn);
        }

        return null;
    }

    private static bool CandidateRefersTo(WireObject action, string publicRef) =>
        action.Members.Any(member => member.Key == "fields" && member.Value is WireArray fields &&
            fields.Items.OfType<WireObject>().Any(field =>
                field.Members.Any(entry => entry.Key == "value" && entry.Value is WireString text &&
                    text.Value == publicRef)));

    private static string AnswerRefersTo(WireObject action) =>
        ((WireString)((WireObject)((WireArray)action.Members.Single(m => m.Key == "fields").Value).Items[0])
            .Members.Single(m => m.Key == "value").Value).Value;

    private static WireObject SmallestSemanticKey(WireObject snapshot) =>
        LegalActions(snapshot)
            .OrderBy(SemanticKey, Comparer<string>.Create((left, right) => WireCanonical.CompareByUtf8Bytes(left, right)))
            .First();

    /// <summary>动作语义键＝规范序列化的 <c>{action_type, fields}</c>（contract-artifacts §3）。</summary>
    private static string SemanticKey(WireObject action) => WireCanonical.Text(action);

    private static string Text(WireValue? snapshot) =>
        snapshot is null ? "<missing>" : WireCanonical.Text(Normalize(snapshot));

    private static WireValue Normalize(WireValue value) => value switch
    {
        WireObject obj => new WireObject(obj.Members.Select(member => new KeyValuePair<string, WireValue>(
            member.Key, VolatileKeys.Contains(member.Key) ? WireValue.Str("<control>") : Normalize(member.Value)))),
        WireArray array => new WireArray(array.Items.Select(Normalize)),
        _ => value,
    };

    private static IReadOnlyList<WireObject> Entities(WireValue snapshot) =>
        ((WireArray)((WireObject)snapshot).Members.Single(m => m.Key == "entities").Value).Items
        .OfType<WireObject>().ToList();

    private static IReadOnlyList<WireObject> Piles(WireObject snapshot) =>
        ((WireArray)snapshot.Members.Single(m => m.Key == "piles").Value).Items.OfType<WireObject>().ToList();

    private static IReadOnlyList<WireObject> LegalActions(WireObject snapshot) =>
        ((WireArray)snapshot.Members.Single(m => m.Key == "legal_actions").Value).Items.OfType<WireObject>().ToList();

    private static IReadOnlyList<WireObject> FieldsOf(WireObject entity) =>
        ((WireArray)entity.Members.Single(m => m.Key == "fields").Value).Items.OfType<WireObject>().ToList();

    private static IReadOnlyList<WireObject> KnownPositions(WireObject pile) =>
        ((WireArray)pile.Members.Single(m => m.Key == "known_positions").Value).Items.OfType<WireObject>().ToList();

    private static List<string> VisibleMembers(WireObject pile) =>
        ((WireArray)pile.Members.Single(m => m.Key == "visible_members").Value).Items
        .OfType<WireString>().Select(text => text.Value).ToList();

    private static string InstanceId(WireObject entity) =>
        ((WireString)entity.Members.Single(m => m.Key == "instance_id").Value).Value;

    private static string KindId(WireObject entity) =>
        ((WireString)entity.Members.Single(m => m.Key == "kind_id").Value).Value;

    private static string? ContentId(WireObject entity) =>
        entity.Members.Any(m => m.Key == "content_id") &&
        entity.Members.Single(m => m.Key == "content_id").Value is WireString text
            ? text.Value
            : null;

    private static string ActionType(WireObject action) =>
        ((WireString)action.Members.Single(m => m.Key == "action_type").Value).Value;

    private static string CandidateId(WireObject action) =>
        ((WireString)action.Members.Single(m => m.Key == "candidate_id").Value).Value;

    private static CapabilitiesDto Capabilities(ControlHost host)
    {
        var described = host.Dispatch(Req("d-1", Operation.Describe));
        Check.True(described.Ok, "describe 应当成功");
        return CapabilitiesDto.FromWire(Field(described, "capabilities"));
    }

    private static bool HasKey(WireObject value, string key) => value.Members.Any(m => m.Key == key);

    private static RequestDto Req(string id, Operation operation, params (string Key, WireValue Value)[] members) =>
        new(id, operation, new WireObject(members.Select(member =>
            new KeyValuePair<string, WireValue>(member.Key, member.Value))));

    private static WireValue Field(ResponseDto response, string key)
    {
        var node = response.Result;
        Check.True(node is WireObject, $"响应结果必须是对象（{key}）：{response.Error?.Code}");
        foreach (var member in ((WireObject)node!).Members)
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

    private static long ObservationIdOf(ResponseDto observeResponse) =>
        long.Parse(((WireInteger)((WireObject)Field(observeResponse, "snapshot")!)
            .Members.Single(m => m.Key == "observation_id").Value).Digits);

    private static string FirstCandidateId(ResponseDto observeResponse) =>
        CandidateId(LegalActions((WireObject)Field(observeResponse, "snapshot")!)[0]);
}
