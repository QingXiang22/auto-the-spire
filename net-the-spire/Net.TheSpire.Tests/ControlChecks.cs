using BaseSpire.Slice;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Tests;

/// <summary>
/// N-03 的检查：会话、当前版本/候选绑定、请求账本与 <c>uncertain</c>。
///
/// 依 `protocol.md`：`:187` 去重（start 进程级、step/close 会话级、成败都缓存、先核指纹）、
/// `:193` pending→settling→执行一次→先更新快照/绑定表再应答、并发 `session_busy`、
/// `:201` 关闭后旧句柄不执行、`:214` 不自动创建新会话。审计不变里对应 I-41/I-52/I-56/I-57/I-59/I-60。
/// </summary>
internal static class ControlChecks
{
    private const string PlayerViewId = "base-spire:PlayerView";

    /// <summary>测试用固定运行令牌：会话 id 形如 <c>session-tst-1</c>，让检查可以引用确定句柄。
    /// 生产默认是每进程唯一的随机令牌（见 H-03 的跨进程不复用检查）。</summary>
    private const string TestRunToken = "tst";

    public static IEnumerable<(string Name, Action Body)> Build()
    {
        yield return ("N-03 get_catalog 返回可解析的 exact 目录，身份与会话一致", CatalogShape);
        yield return ("N-03 start 校验 expected_identity 与 scenario_profile（不符即拒）", StartIdentityChecks);
        yield return ("N-03 describe 报 identity+capabilities，且不声明不支持的能力", DescribeShape);
        yield return ("N-03 start 原样重发不启动第二局；换载荷 → request_conflict", StartDeduplication);
        yield return ("N-03 observe 不推进游戏（版本与 observation_id 不变）", ObserveDoesNotAdvance);
        yield return ("N-03 同 request_id 换载荷的 step → request_conflict（不第二次执行）", StepConflict);
        yield return ("N-03 过期的 expected_observation_id → stale_observation 且不推进", StaleObservation);
        yield return ("N-03 非法候选 → illegal_candidate 且不推进", IllegalCandidate);
        yield return ("N-03 面板选择：出牌后 step 决策候选可继续", DecisionStepContinues);
        yield return ("N-03 request_status：完成返回原响应、未知 not_found、可查 start", RequestStatusQuery);
        yield return ("N-03 关闭后旧句柄不执行（session_closed）", ClosedRefuses);
        yield return ("N-03 未知会话 → session_not_found（不自动创建）", UnknownSession);
        yield return ("N-03 账本容量上限 → capacity_exceeded，且既有记录不被截断", LedgerCapacity);
        yield return ("N-03 请求结算后不留半结算条目", NoHalfSettledEntries);
        yield return ("H-03 session_id 在同一进程内不复用（关闭后换句柄）", SessionIdsAreNotReusedInProcess);
        yield return ("H-03 session_id 跨进程不复用，旧句柄不得指向新局", SessionIdsAreNotReusedAcrossProcesses);
        yield return ("H-03 同步后端只报 completed/not_found，不伪造 pending/outcome_unknown", SynchronousProviderStates);
        yield return ("H-03 去重记录保留到关闭，关闭后仍可查原响应", LedgerSurvivesClose);
    }

    // -- 夹具 --------------------------------------------------------------------------------------------------

    private sealed record Host(ControlHost Control, ControlSession Session);

    private static Host NewHost(int ledgerCapacity = 4096)
    {
        var host = NewRawHost(TestRunToken, ledgerCapacity);
        var started = host.Dispatch(StartRequest(host, "start-1"));
        Check.True(started.Ok, "start 应当成功");
        var sessionId = StringField(started, "session_id");
        var session = host.GetSessionForTests(sessionId!);
        return new Host(host, session);
    }

    /// <summary>只构造宿主，不启动会话；<paramref name="runToken"/> 为 null 时用生产默认（每次唯一）。</summary>
    private static ControlHost NewRawHost(string? runToken = null, int ledgerCapacity = 4096)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build().Build());
        return new ControlHost(builder.Build(), SlicePublicProfile.Create(), SliceManifest.Player,
            SliceManifest.PlayerRole, PlayerViewId, 20261001UL, ledgerCapacity, runToken,
            // 同一组合层合同：切片包声明的计算资产要求宿主装后端（控制层不认识游戏 ID，只转交）。
            computeBackend: BaseSpireComputeBackend.Instance);
    }

    /// <summary>
    /// 旧 0.1 切片场景的 start 请求：身份与 scenario_profile 取自**实际传入的宿主**，不读忽略 source 的静态常量。
    /// </summary>
    private static RequestDto StartRequest(ControlHost host, string id, string? scenarioProfile = null,
        string? catalogId = null)
    {
        var profile = host.Profile;
        var identity = new IdentityDto(profile.ProtocolVersion, profile.RulesetId,
            catalogId ?? profile.CatalogId, profile.BackendProfileId);
        return Req(id, Operation.Start,
            ("expected_identity", identity.ToWire()),
            ("scenario_profile", WireValue.Str(scenarioProfile ?? host.SupportedScenarioProfile)));
    }

    private static RequestDto Req(string id, Operation operation, params (string Key, WireValue Value)[] members) =>
        new(id, operation, new WireObject(members.Select(member =>
            new KeyValuePair<string, WireValue>(member.Key, member.Value))));

    private static WireValue Field(ResponseDto response, string key)
    {
        var node = response.Result;
        Check.True(node is WireObject, $"响应结果必须是对象（{key}）");
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

    private static long ObservationIdOf(ResponseDto observeResponse)
    {
        var snapshot = Field(observeResponse, "snapshot");
        Check.True(snapshot is WireObject, "observe 必须返回快照");
        foreach (var member in ((WireObject)snapshot).Members)
        {
            if (member.Key == "observation_id" && member.Value is WireInteger integer &&
                long.TryParse(integer.Digits, out var parsed))
            {
                return parsed;
            }
        }

        throw new CheckFailedException("快照缺少 observation_id");
    }

    private static string FirstCandidateId(ResponseDto observeResponse)
    {
        var snapshot = Field(observeResponse, "snapshot");
        foreach (var member in ((WireObject)snapshot).Members)
        {
            if (member.Key != "legal_actions" || member.Value is not WireArray array)
            {
                continue;
            }

            foreach (var item in array.Items)
            {
                if (item is not WireObject action)
                {
                    continue;
                }

                foreach (var field in action.Members)
                {
                    if (field.Key == "candidate_id" && field.Value is WireString text)
                    {
                        return text.Value;
                    }
                }
            }
        }

        throw new CheckFailedException("快照里没有候选");
    }

    // -- 检查 --------------------------------------------------------------------------------------------------

    private static void StartIdentityChecks()
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build().Build());
        var profile = SlicePublicProfile.Create();
        var host = new ControlHost(builder.Build(), profile, SliceManifest.Player, SliceManifest.PlayerRole,
            PlayerViewId, 20261001UL);

        var wrongCatalog = host.Dispatch(StartRequest(host, "s-bad-catalog", catalogId: "fixture:other-catalog"));
        Check.Equal("version_mismatch", ErrorCode(wrongCatalog), "身份不符必须 version_mismatch（不自动降级）");
        Check.Equal(0, host.SessionCount, "身份不符不得创建会话");

        var wrongScenario = host.Dispatch(StartRequest(host, "s-bad-scenario", scenarioProfile: "not-supported"));
        Check.Equal("unsupported_capability", ErrorCode(wrongScenario), "不支持的场景必须在启动前拒绝");

        var wrongCatalogId = host.Dispatch(Req("g-bad", Operation.GetCatalog,
            ("catalog_id", WireValue.Str("fixture:other-catalog"))));
        Check.Equal("version_mismatch", ErrorCode(wrongCatalogId), "get_catalog 的 catalog_id 不存在则失败");
    }

    private static void CatalogShape()
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build().Build());
        var host = new ControlHost(builder.Build(), SlicePublicProfile.Create(), SliceManifest.Player,
            SliceManifest.PlayerRole, PlayerViewId, 20261001UL);
        var response = host.Dispatch(Req("g-1", Operation.GetCatalog,
            ("catalog_id", WireValue.Str(SlicePublicProfile.Create().CatalogId))));
        Check.True(response.Ok, $"get_catalog 必须成功：{response.Error?.Code} {response.Error?.Message}");
        var catalog = PublicCatalogDto.FromWire(response.Result!);
        var profile = SlicePublicProfile.Create();
        Check.Equal(profile.CatalogId, catalog.CatalogId, "catalog_id 必须与运行时身份一致");
        Check.Equal(profile.RulesetId, catalog.RulesetId, "ruleset_id 必须与运行时身份一致");
        Check.True(catalog.Declarations.EntityKinds.Count > 0, "必须导出实体种类");
        Check.True(catalog.Declarations.ContentTypes.Count > 0, "必须导出内容类型（含 power/card 内容）");
        Check.True(catalog.Declarations.Fields.Count > 0, "必须导出字段声明");
        Check.True(catalog.Declarations.ActionTypes.Count > 0, "必须导出动作类型");
        Check.True(catalog.Declarations.TerminalReasons.Count > 0, "必须导出终局原因");
        // H-02：牌堆实体已由 base-spire 声明，pile_types 不再是空的（旧的 TODO-N03-CATALOG-PILES 已收口）。
        Check.Equal(5, catalog.Declarations.PileTypes.Count, "五个牌区各导出一个 pile_type");
        Check.True(catalog.Declarations.PileTypes.All(pile => pile.MemberKindIds.Count > 0),
            "每个 pile_type 必须声明成员种类");
        // I-86：content_type 的 id 必须唯一（作者内容里同一内容 ID 可同时属于卡牌与能力：
        // 必须合并成一条、kind_ids 列出全部种类，而不是发两条重复 id 的声明）。
        var contentIds = catalog.Declarations.ContentTypes.Select(item => item.Id).ToList();
        Check.Equal(contentIds.Count, contentIds.Distinct(StringComparer.Ordinal).Count(),
            "content_types 的 id 必须唯一（I-86）");
        Check.True(catalog.Declarations.ContentTypes.All(item => item.KindIds.Count > 0),
            "每条 content_type 必须声明至少一个 kind_id");
        Check.True(catalog.Declarations.ContentTypes.All(item =>
                item.KindIds.SequenceEqual(item.KindIds.OrderBy(id => id, StringComparer.Ordinal))),
            "content_type.kind_ids 必须按公开 id 排序（V-01 规范字节）");
        Check.True(catalog.Digest().StartsWith("sha256:", StringComparison.Ordinal), "目录必须有规范摘要");
    }

    private static void DescribeShape()
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build().Build());
        var host = new ControlHost(builder.Build(), SlicePublicProfile.Create(), SliceManifest.Player,
            SliceManifest.PlayerRole, PlayerViewId, 20261001UL);
        var response = host.Dispatch(Req("d-1", Operation.Describe));
        Check.True(response.Ok, "describe 必须成功");
        var parsed = DescribeResultDto.FromWire(response.Result!);
        Check.Equal(IdentityDto.ExpectedProtocolVersion, parsed.Identity.ProtocolVersion, "协议版本");
        Check.Equal(SlicePublicProfile.Create().RulesetId, parsed.Identity.RulesetId, "ruleset 身份");
        Check.False(parsed.Capabilities.Operations.Contains("attach", StringComparer.Ordinal),
            "模拟后端不得声明 attach");
        Check.True(parsed.Capabilities.Operations.Contains("request_status", StringComparer.Ordinal),
            "必须声明 request_status");
        Check.False(parsed.Capabilities.ExecutionGuarantees.PublicStateRestore, "首版 public_state_restore=false");
        Check.False(parsed.Capabilities.ExecutionGuarantees.PublicFork, "首版 public_fork=false");
        Check.False(parsed.Capabilities.ExecutionGuarantees.InternalSnapshotRestore,
            "首版 internal_snapshot_restore=false（评审已裁定不需要 core 快照）");
        Check.Equal("simulation", parsed.Capabilities.BackendKind, "后端种类");

        // describe 的**结构声明与限额**必须与 Profile 实际发布的事实一致：报得比真实值小就是假声明
        // （消费者按这些数字分配缓冲），报 0 而实际发布 group 会直接误导。
        var expectedPiles = SlicePublicProfile.Create().PileTypes.Select(pile => pile.PileTypeId)
            .OrderBy(id => id, StringComparer.Ordinal).ToList();
        var declaredPiles = parsed.Capabilities.SupportedStructures.Single(structure => structure.Structure == "Pile")
            .DeclarationIds.OrderBy(id => id, StringComparer.Ordinal).ToList();
        Check.Sequence(expectedPiles, declaredPiles, "Pile 结构声明必须列出真实的 pile_type");

        var groups = parsed.Capabilities.SupportedStructures.Single(structure => structure.Structure == "Group");
        Check.Equal(0, groups.DeclarationIds.Count, "默认 Profile 不发布任何 group");
        Check.Equal(0L, parsed.Capabilities.Limits.Groups, "默认 Profile 的 groups 限额为 0（与发布一致）");
        Check.Equal(0L, parsed.Capabilities.Limits.MembersPerGroup, "默认 Profile 的 members_per_group 为 0");
        Check.Equal(0L, parsed.Capabilities.Limits.Sequences, "引擎无 Sequence 声明");

        // 已收口的具名缺口不得继续作为"不支持约束"发布（否则消费者会以为该能力不可用）。
        var constraints = parsed.Capabilities.ScenarioProfiles.Single().UnsupportedConstraints;
        Check.False(constraints.Any(text => text.Contains("TODO-", StringComparison.Ordinal)),
            $"不支持约束不得继续登记已收口的 TODO：{string.Join(" / ", constraints)}");
    }

    private static void StartDeduplication()
    {
        var host = NewHost().Control;
        var before = host.SessionCount;
        var again = host.Dispatch(StartRequest(host, "start-1"));   // 原样重发：载荷必须逐字相同
        Check.True(again.Ok, "原样重发必须返回原结果");
        Check.Equal(before, host.SessionCount, "原样重发不得启动第二局");
        var conflict = host.Dispatch(StartRequest(host, "start-1", scenarioProfile: "another-profile"));
        Check.Equal("request_conflict", ErrorCode(conflict), "同一 start request_id 换载荷");
    }

    private static void ObserveDoesNotAdvance()
    {
        var host = NewHost().Control;
        var first = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var second = host.Dispatch(Req("o-2", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        Check.True(first.Ok && second.Ok, "observe 必须成功");
        Check.Equal(ObservationIdOf(first), ObservationIdOf(second), "observe 不得推进游戏");
    }

    private static void StepConflict()
    {
        var host = NewHost().Control;
        var observe = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var observation = ObservationIdOf(observe);
        var candidate = FirstCandidateId(observe);
        var first = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str("session-tst-1")),
            ("expected_observation_id", WireValue.Integer(observation)),
            ("candidate_id", WireValue.Str(candidate))));
        Check.True(first.Ok, $"首次 step 应当成功：{first.Error?.Code} {first.Error?.Message}");
        var replay = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str("session-tst-1")),
            ("expected_observation_id", WireValue.Integer(observation)),
            ("candidate_id", WireValue.Str(candidate))));
        Check.True(replay.Ok, "完成后的原样重发必须返回原结果");
        Check.Equal(WireCanonical.Text(first.Result!), WireCanonical.Text(replay.Result!),
            "原样重发必须得到同一结果（只执行一次）");
        var conflict = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str("session-tst-1")),
            ("expected_observation_id", WireValue.Integer(observation)),
            ("candidate_id", WireValue.Str("c-not-a-candidate"))));
        Check.Equal("request_conflict", ErrorCode(conflict), "同 request_id 换载荷");
    }

    private static void StaleObservation()
    {
        var host = NewHost().Control;
        var observe = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var candidate = FirstCandidateId(observe);
        var stale = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str("session-tst-1")),
            ("expected_observation_id", WireValue.Integer(0)),
            ("candidate_id", WireValue.Str(candidate))));
        Check.Equal("stale_observation", ErrorCode(stale), "过期版本必须拒绝");
        var after = host.Dispatch(Req("o-2", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        Check.Equal(ObservationIdOf(observe), ObservationIdOf(after), "过期请求不得推进游戏");
    }

    private static void IllegalCandidate()
    {
        var host = NewHost().Control;
        var observe = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var illegal = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str("session-tst-1")),
            ("expected_observation_id", WireValue.Integer(ObservationIdOf(observe))),
            ("candidate_id", WireValue.Str("c-does-not-exist"))));
        Check.Equal("illegal_candidate", ErrorCode(illegal), "不存在的候选必须拒绝");
    }

    private static void DecisionStepContinues()
    {
        var host = NewHost().Control;
        var observe = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var playCandidate = FirstCandidateId(observe);
        var step = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str("session-tst-1")),
            ("expected_observation_id", WireValue.Integer(ObservationIdOf(observe))),
            ("candidate_id", WireValue.Str(playCandidate))));
        Check.True(step.Ok, $"出牌应当成功：{step.Error?.Code}");
        var after = host.Dispatch(Req("o-2", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var status = StringField(after, "session_status");
        Check.True(status is "active" or "terminal", $"出牌后会话状态应为 active/terminal，实际 {status}");
        var snapshot = Field(after, "snapshot");
        Check.True(snapshot is WireObject, "active 会话必须有稳定快照");
    }

    private static void RequestStatusQuery()
    {
        var host = NewHost().Control;
        var observe = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var status = host.Dispatch(Req("q-1", Operation.RequestStatus,
            ("session_id", WireValue.Str("session-tst-1")),
            ("target_request_id", WireValue.Str("o-1"))));
        Check.True(status.Ok, "request_status 必须成功");
        Check.Equal("completed", StringField(status, "status"), "完成的请求应报 completed");
        Check.Equal(WireCanonical.Text(observe.ToWire()), WireCanonical.Text(Field(status, "response")),
            "completed 必须返回原响应（整个响应，不是仅 result）");

        var unknown = host.Dispatch(Req("q-2", Operation.RequestStatus,
            ("session_id", WireValue.Str("session-tst-1")),
            ("target_request_id", WireValue.Str("never-sent"))));
        Check.Equal("not_found", StringField(unknown, "status"), "未知请求报 not_found（不是未执行证明）");

        var startQuery = host.Dispatch(Req("q-3", Operation.RequestStatus,
            ("target_request_id", WireValue.Str("start-1"))));
        Check.Equal("completed", StringField(startQuery, "status"), "查询 start 可省略 session_id");
    }

    private static void ClosedRefuses()
    {
        var host = NewHost().Control;
        var closed = host.Dispatch(Req("c-1", Operation.Close, ("session_id", WireValue.Str("session-tst-1"))));
        Check.True(closed.Ok, "close 必须成功");
        Check.Equal("closed", StringField(closed, "session_status"), "close 结果包装固定");
        var after = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        Check.Equal("session_closed", ErrorCode(after), "关闭后旧句柄不执行");
    }

    private static void UnknownSession()
    {
        var host = NewHost().Control;
        var response = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-404"))));
        Check.Equal("session_not_found", ErrorCode(response), "未知会话必须报 session_not_found");
        Check.Equal(1, host.SessionCount, "不得自动创建新会话");
    }

    private static void LedgerCapacity()
    {
        var host = NewHost(ledgerCapacity: 1).Control;
        var ok = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        Check.True(ok.Ok, "第一条请求应当成功");
        var over = host.Dispatch(Req("o-2", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        Check.Equal("capacity_exceeded", ErrorCode(over), "账本满时必须拒绝新控制请求");
        var still = host.Dispatch(Req("q-1", Operation.RequestStatus,
            ("session_id", WireValue.Str("session-tst-1")),
            ("target_request_id", WireValue.Str("o-1"))));
        Check.Equal("completed", StringField(still, "status"),
            "容量拒绝后既有记录必须仍在账本里且可查（不得丢弃或变成未知）");
    }

    /// <summary>同一个进程内的会话句柄：单调计数器，关闭后不回收给新局。</summary>
    private static void SessionIdsAreNotReusedInProcess()
    {
        var host = NewRawHost(TestRunToken);
        var first = StringField(host.Dispatch(StartRequest(host, "start-1")), "session_id");
        Check.Equal("session-tst-1", first, "本进程第一个会话句柄");
        var closed = host.Dispatch(Req("c-1", Operation.Close, ("session_id", WireValue.Str(first!))));
        Check.True(closed.Ok, "close 应当成功");

        var second = StringField(host.Dispatch(StartRequest(host, "start-2")), "session_id");
        Check.True(second != first, $"关闭后新会话必须换句柄（旧 {first}，新 {second}）");
        var old = host.Dispatch(Req("o-9", Operation.Observe, ("session_id", WireValue.Str(first!))));
        Check.Equal("session_closed", ErrorCode(old), "旧句柄仍指向已关闭的那一局，而不是被回收给新局");
    }

    /// <summary>跨进程（新宿主实例＝宿主重启）：默认令牌每次不同，旧句柄在新进程里必须找不到。</summary>
    private static void SessionIdsAreNotReusedAcrossProcesses()
    {
        var first = NewRawHost();
        var second = NewRawHost();
        var idFirst = StringField(first.Dispatch(StartRequest(first, "start-1")), "session_id");
        var idSecond = StringField(second.Dispatch(StartRequest(second, "start-1")), "session_id");
        Check.True(first.RunToken != second.RunToken, "两个宿主实例的运行令牌必须不同");
        Check.True(idFirst != idSecond, $"重启后会话句柄不得复用：{idFirst} vs {idSecond}");

        // 判别力所在：修复前两个进程都会发出 "session-1"，旧句柄会在新进程里**成功打开另一局**。
        var crossed = second.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str(idFirst!))));
        Check.Equal("session_not_found", ErrorCode(crossed),
            "旧进程发出的句柄在新进程里必须找不到，否则等于把旧句柄接到另一局");
        Check.True(second.GetSessionForTests(idSecond!).SessionId == idSecond, "新进程自己的会话仍可寻址");
    }

    /// <summary>同步后端的状态诚实性：completed/not_found 之外的状态不由本地凭空产生。</summary>
    private static void SynchronousProviderStates()
    {
        var host = NewHost().Control;
        var observe = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var step = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str("session-tst-1")),
            ("expected_observation_id", WireValue.Integer(ObservationIdOf(observe))),
            ("candidate_id", WireValue.Str(FirstCandidateId(observe)))));
        Check.True(observe.Ok && step.Ok, "两个控制请求都必须完成");
        Check.False(host.GetSessionForTests("session-tst-1").Settling,
            "同步后端不残留 settling 条目 → 协议 pending 在本地不可达（不得伪造）");

        foreach (var target in new[] { "o-1", "s-1" })
        {
            var status = host.Dispatch(Req("q-" + target, Operation.RequestStatus,
                ("session_id", WireValue.Str("session-tst-1")),
                ("target_request_id", WireValue.Str(target))));
            Check.Equal("completed", StringField(status, "status"), $"已完成的 {target} 必须报 completed");
        }

        var missing = host.Dispatch(Req("q-nf", Operation.RequestStatus,
            ("session_id", WireValue.Str("session-tst-1")),
            ("target_request_id", WireValue.Str("never-sent"))));
        Check.Equal("not_found", StringField(missing, "status"),
            "无记录只报 not_found；不得用 outcome_unknown 掩盖（它只属于真的无法确认的情形）");
    }

    /// <summary>protocol.md:199：去重记录保留到会话关闭或进程退出；关闭后仍可查到原响应。</summary>
    private static void LedgerSurvivesClose()
    {
        var host = NewHost().Control;
        var observe = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var closed = host.Dispatch(Req("c-1", Operation.Close, ("session_id", WireValue.Str("session-tst-1"))));
        Check.True(closed.Ok, "close 应当成功");

        var status = host.Dispatch(Req("q-1", Operation.RequestStatus,
            ("session_id", WireValue.Str("session-tst-1")),
            ("target_request_id", WireValue.Str("o-1"))));
        Check.Equal("completed", StringField(status, "status"), "关闭后既有记录仍必须可查");
        Check.Equal(WireCanonical.Text(observe.ToWire()), WireCanonical.Text(Field(status, "response")),
            "关闭后查询仍返回原响应");
    }

    private static void NoHalfSettledEntries()
    {
        var host = NewHost().Control;
        var observe = host.Dispatch(Req("o-1", Operation.Observe, ("session_id", WireValue.Str("session-tst-1"))));
        var step = host.Dispatch(Req("s-1", Operation.Step,
            ("session_id", WireValue.Str("session-tst-1")),
            ("expected_observation_id", WireValue.Integer(ObservationIdOf(observe))),
            ("candidate_id", WireValue.Str(FirstCandidateId(observe)))));
        Check.True(step.Ok, "step 应当成功");
        Check.False(host.GetSessionForTests("session-tst-1").Settling, "结算完成后不得残留 settling 条目");
        var status = host.Dispatch(Req("q-1", Operation.RequestStatus,
            ("session_id", WireValue.Str("session-tst-1")),
            ("target_request_id", WireValue.Str("s-1"))));
        Check.Equal("completed", StringField(status, "status"), "step 完成后应能查到原响应");
    }
}
