using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Host;
using NetTheSpire.Projection;

namespace NetTheSpire.Tests;

/// <summary>
/// ENV-T03 声明扩展实证子包：用同一通用文本规则提供方跑 baseline / extended 两个规则包，
/// 证明「只在规则包新增公开字段与合法 Action」时 net 生产源码无需新增登记行，目录/observe 出现
/// 对应内容、step 执行正确，新增内部块不成为外部操作，隐藏字段不泄露（design.md §7.3 第 4 项）。
/// </summary>
internal static class DeclarationExtensionChecks
{
    private const string BaselineSeed = "DECL-EXT-00";
    private const string SecondSeed = "DECL-EXT-01";

    private const string MarkerKindId = "net:kind:declaration:Marker";
    private const string ValueFieldId = "net:field:declaration:Marker.Value";
    private const string BumpFieldId = "net:field:declaration:Marker.Bump";
    private const string SecretFieldId = "net:field:declaration:Marker.Secret";
    private const string AddActionId = "net:action:declaration:Add";
    private const string AddDeltaFieldId = "net:field:declaration:Add.delta";
    private const string MarkActionId = "net:action:declaration:Mark";
    private const string MarkAmountFieldId = "net:field:declaration:Mark.amount";
    private const string RetallyActionId = "net:action:declaration:Retally";
    private const string TerminalReasonId = "net:reason:declaration:ReachedLimit";
    private const string TerminalValueFieldId = "net:field:declaration:ReachedLimit.Value";

    public static IEnumerable<(string Name, Action Body)> Build()
    {
        yield return ("E-01 [真实] 同一提供方跑 baseline/extended：只多出规则包新增的公开字段与合法动作", CatalogDelta);
        yield return ("E-02 [真实] text-rules 轨迹：start→observe→step→observe 观察到新增字段真实变化", Trajectory);
        yield return ("E-03 [真实] 只改规则包即换公开身份；每局 seed 不进任何身份", IdentityMovesWithRules);
        yield return ("E-04 [真实] 通用接缝：已配置未 Start 的独立会话与游戏 seed 原样回显", ProvidedSessions);
        yield return ("E-05 [真实] 候选闭合：choices 值域决定候选集合，内部 procedure 不出现", CandidateClosure);
        yield return ("E-06 [真实] 过期候选/错 expected_identity 在推进前拒绝；重发幂等；换载荷冲突", RejectionsAndIdempotence);
        yield return ("E-07 [真实] 描述文件遇到未支持结构显式抛错，不静默丢公开事实", UnsupportedMetadataIsExplicit);
        yield return ("E-08 [真实] text-rules 的每类真实响应帧都通过 0.2 wire schema（与 Python 侧同一文件）", SchemaConformance);
        yield return ("E-09 [真实] 无状态写入的连续决策与空动作推进观察版本，重发和拒绝不推进", ObservationBoundaries);
        yield return ("E-10 [真实] 双实例候选保留全部有序答案，一次续接且请求重放无副作用", OrderedPairs);
    }

    // ---- E-01 -----------------------------------------------------------------

    private static void CatalogDelta()
    {
        var narrow = new FixtureHost(nameof(CatalogDelta), "baseline.spire").Catalog();
        var wide = new FixtureHost(nameof(CatalogDelta), "extended.spire").Catalog();

        var narrowFields = narrow.Declarations.Fields;
        var wideFields = wide.Declarations.Fields;

        // 两个规则包都发布的基础事实：同一实体种类、同一公开字段、同一合法动作。
        Check.True(narrow.Declarations.EntityKinds.Any(kind => kind.Id == MarkerKindId), "baseline 必须发布 Marker 实体种类");
        Check.True(wide.Declarations.EntityKinds.Any(kind => kind.Id == MarkerKindId), "extended 必须发布 Marker 实体种类");
        Check.Equal(narrow.Declarations.EntityKinds.Count, wide.Declarations.EntityKinds.Count, "新增字段不得新增实体种类");
        Check.True(narrowFields.Any(field => field.Id == ValueFieldId), "baseline 必须发布 Marker.Value");
        Check.True(narrow.Declarations.ActionTypes.Any(action => action.Id == AddActionId), "baseline 必须发布 Add 动作");

        // extended 的 delta 恰好是规则包里新增的两处公开事实：实体字段 Marker.Bump 与动作参数 Mark.amount。
        var addedFields = wideFields.Select(field => field.Id)
            .Except(narrowFields.Select(field => field.Id), StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        Check.Sequence(new[] { MarkAmountFieldId, BumpFieldId }, addedFields, "公开字段的增量必须恰好是规则包新增的公开字段");
        var removedFields = narrowFields.Select(field => field.Id)
            .Except(wideFields.Select(field => field.Id), StringComparer.Ordinal)
            .ToList();
        Check.Equal(0, removedFields.Count, "扩展包不得删掉 baseline 的公开字段");

        var addedActions = wide.Declarations.ActionTypes.Select(action => action.Id)
            .Except(narrow.Declarations.ActionTypes.Select(action => action.Id), StringComparer.Ordinal)
            .ToList();
        Check.Sequence(new[] { MarkActionId }, addedActions, "合法动作的增量必须恰好是规则包新增的 Action");

        // 新增的不公开事实：Hidden 字段不进目录、内部 procedure 不成为外部操作。
        Check.False(wideFields.Any(field => field.Id == SecretFieldId), "visibility=Hidden 的字段不得出现在公开目录");
        Check.False(wide.Declarations.ActionTypes.Any(action => action.Id == RetallyActionId), "内部 procedure 不得成为公开 Action");

        // 新增公开字段的类型/参数由声明派生。
        var bump = wideFields.Single(field => field.Id == BumpFieldId);
        Check.Equal("number", bump.ValueType, "Marker.Bump 必须按 Int 声明派生成 number");
        Check.Equal("integer", bump.NumberFormat, "Marker.Bump 必须派生成 integer 数字格式");
        Check.True(bump.Required, "无 optional 的公开字段必须是 required");
        var mark = wide.Declarations.ActionTypes.Single(action => action.Id == MarkActionId);
        Check.Sequence(new[] { MarkAmountFieldId }, mark.RequiredFields, "Mark 的参数必须恰好是规则包声明的 amount");
        var addBefore = narrow.Declarations.ActionTypes.Single(action => action.Id == AddActionId);
        var addAfter = wide.Declarations.ActionTypes.Single(action => action.Id == AddActionId);
        Check.Sequence(addBefore.RequiredFields, addAfter.RequiredFields, "扩展包不得改动 baseline 动作的参数");

        // 终局解释来自四类不可推导元数据；终局字段由终局声明派生。
        // 声明写的是裸 `Value = Int`（没有 default/取值约定），引擎把该终局字段标成 optional；
        // net 如实转述声明派生的形状，不自行把可选字段升成必填，所以这里只要求它出现在必填或可选之一。
        var terminal = wide.Declarations.TerminalReasons.Single(reason => reason.Id == TerminalReasonId);
        Check.Equal("win", terminal.Outcome, "terminal_outcomes 元数据必须决定终局胜负解释");
        var terminalFieldSlots = new[] { terminal.RequiredFields, terminal.OptionalFields }
            .Count(list => list.Contains(TerminalValueFieldId, StringComparer.Ordinal));
        Check.Equal(1, terminalFieldSlots, "终局字段必须由终局声明派生，且只能落在必填或可选之一");

        // 集合用途元数据：Slots 声明 not_published，因此目录里没有成员发布表、没有内容目录。
        Check.Equal(0, narrow.Declarations.ContentTypes.Count, "内容目录为空时必须不发 content_types");
        Check.Equal(0, narrow.Declarations.PileTypes.Count, "未发布的集合不得变成 pile 类型");
        Check.Equal(0, narrow.Declarations.SequenceTypes.Count, "未发布的集合不得变成 sequence 类型");
        Check.Equal(0, narrow.Declarations.GroupTypes.Count, "未发布的集合不得变成 group 类型");
    }

    // ---- E-02 -----------------------------------------------------------------

    private static void Trajectory()
    {
        var fixture = new FixtureHost(nameof(Trajectory), "extended.spire");
        var (sessionId, first) = Start(fixture, "e02-start", BaselineSeed);

        Check.Equal("decision", first.Status, "开局后应当是等待决策状态");
        Check.Equal(0L, Field(first, BumpFieldId), "开局时新增字段应当是声明默认值");

        var mark = Candidate(first, MarkActionId, MarkAmountFieldId, "3");
        var stepped = Step(fixture, "e02-mark", sessionId, first.ObservationId, mark);
        Check.True(stepped.Ok, $"执行新增动作必须成功：{stepped.Error?.Code} {stepped.Error?.Message}");

        var second = Observe(fixture, "e02-observe-2", sessionId);
        Check.Equal(3L, Field(second, BumpFieldId), "新增动作必须真的修改规则包新增的公开字段");
        Check.Equal(0L, Field(second, ValueFieldId), "未被执行的动作不得改动其它公开字段");
        Check.False(HasField(second, SecretFieldId), "隐藏字段不得出现在公开观察");
        Check.False(
            WireCanonical.Text(ObservedSnapshotWire(fixture, sessionId)).Contains("Secret", StringComparison.Ordinal),
            "隐藏字段名不得出现在公开载荷里");

        var add = Candidate(second, AddActionId, AddDeltaFieldId, "2");
        var added = Step(fixture, "e02-add", sessionId, second.ObservationId, add);
        Check.True(added.Ok, $"执行 baseline 动作必须继续可用：{added.Error?.Code} {added.Error?.Message}");
        var third = Observe(fixture, "e02-observe-3", sessionId);
        Check.Equal(2L, Field(third, ValueFieldId), "baseline 动作必须仍然生效");
        Check.Equal(3L, Field(third, BumpFieldId), "扩展字段的值必须保持");
    }

    // ---- E-03 -----------------------------------------------------------------

    private static void IdentityMovesWithRules()
    {
        var narrow = new FixtureHost(nameof(IdentityMovesWithRules), "baseline.spire");
        var wide = new FixtureHost(nameof(IdentityMovesWithRules), "extended.spire");
        var again = new FixtureHost(nameof(IdentityMovesWithRules), "extended.spire");

        Check.Equal(NetProtocol.V02, wide.Host.Profile.ProtocolVersion, "文本规则场景必须走 0.2 派生 profile");
        Check.True(
            !string.Equals(narrow.Host.Profile.RulesetId, wide.Host.Profile.RulesetId, StringComparison.Ordinal),
            "只在规则包新增公开字段/合法动作也必须换 ruleset_id");
        Check.True(
            !string.Equals(narrow.Host.Profile.CatalogId, wide.Host.Profile.CatalogId, StringComparison.Ordinal),
            "声明面变化必须换 catalog_id");
        Check.Equal(wide.Host.Profile.RulesetId, again.Host.Profile.RulesetId, "同一规则包重复构建必须得到同一 ruleset_id");
        Check.Equal(wide.Host.Profile.CatalogId, again.Host.Profile.CatalogId, "同一规则包重复构建必须得到同一 catalog_id");

        foreach (var part in new[]
                 {
                     wide.Host.Profile.RulesetId,
                     wide.Host.Profile.CatalogId,
                     wide.Host.Profile.BackendProfileId,
                     wide.Host.SupportedScenarioProfile,
                     wide.PublicBuildIdentity,
                 })
        {
            Check.False(part.Contains(BaselineSeed, StringComparison.Ordinal), $"公开身份不得含每局 seed：{part}");
            Check.False(part.Contains(SecondSeed, StringComparison.Ordinal), $"公开身份不得含每局 seed：{part}");
        }

        var described = wide.Host.Dispatch(Req("e03-describe", Operation.Describe));
        Check.True(described.Ok, $"describe 必须成功：{described.Error?.Code} {described.Error?.Message}");
        var capabilities = CapabilitiesDto.FromWire(Field(described, "capabilities")!);
        Check.Equal(wide.Spec.ScenarioProfile, capabilities.ScenarioProfiles[0].Id, "场景身份必须来自提供方描述文件");
        Check.Equal(wide.Host.SourceForTests!.RngProfileId, capabilities.ExecutionGuarantees.RngProfileId, "describe 必须报告实际使用的 rng profile");
        Check.True(capabilities.SeedControl, "本提供方真的使用请求给的每局 seed");
    }

    // ---- E-04 -----------------------------------------------------------------

    private static void ProvidedSessions()
    {
        var fixture = new FixtureHost(nameof(ProvidedSessions), "extended.spire");
        var source = fixture.Host.SourceForTests;
        Check.True(source is not null, "正式接缝必须暴露 ISessionSource");

        var provided = source!.CreateSession(null);
        Check.Equal(fixture.Spec.DefaultGameSeed, provided.AcceptedSeed, "请求未带 seed 时必须用提供方显式默认值");
        Check.Equal(fixture.Spec.ProjectionId, provided.ProjectionId, "会话必须使用提供方声明的投影");
        Check.Equal(fixture.Spec.ParticipantRole, provided.ParticipantRole, "会话必须使用提供方声明的参与者角色");
        Check.Equal(provided.Session.Participant(0), provided.Participant, "ParticipantId 必须是该会话的参与者");
        Check.Equal(SessionStatus.Running, provided.Session.Status, "返回句柄必须尚未 Start");
        Check.Equal(0L, provided.Session.Random.TotalDraws, "配置阶段不得消费任何随机数");
        Check.Equal(source.RngProfileId, provided.Session.Random.DrawProfileId, "会话随机流必须用 describe 报告的那个 profile");
        Check.Equal(
            TextRuleSource.SessionSeed(fixture.Spec.DefaultGameSeed),
            provided.Session.Random.SessionSeed,
            "游戏 seed 字符串必须映射成该会话的种子");

        var second = source.CreateSession(SecondSeed);
        Check.True(!ReferenceEquals(provided.Session, second.Session), "每次调用必须返回独立会话");
        Check.Equal(SecondSeed, second.AcceptedSeed, "请求带的 seed 必须原样被接受");
        Check.Equal(TextRuleSource.SessionSeed(SecondSeed), second.Session.Random.SessionSeed, "同一 seed 必须得到同一会话种子");
        Check.True(
            !string.Equals(provided.Session.Identity, second.Session.Identity, StringComparison.Ordinal),
            "不同 seed 的会话身份必须不同");

        // 原样使用，不做 trim / 不做大小写归一。
        const string padded = "  DECL-EXT-02  ";
        Check.Equal(padded, source.CreateSession(padded).AcceptedSeed, "游戏 seed 字符串必须原样回显");
        Check.True(
            TextRuleSource.SessionSeed(padded) != TextRuleSource.SessionSeed("DECL-EXT-02"),
            "不做 trim：不同的游戏 seed 串必须映射成不同的会话种子");
    }

    // ---- E-05 -----------------------------------------------------------------

    private static void CandidateClosure()
    {
        var fixture = new FixtureHost(nameof(CandidateClosure), "extended.spire");
        var (_, snapshot) = Start(fixture, "e05-start", BaselineSeed);

        var byAction = snapshot.LegalActions
            .GroupBy(action => action.ActionType, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        Check.False(byAction.ContainsKey(RetallyActionId), "内部 procedure 不得成为公开候选");
        Check.Sequence(
            new[] { AddActionId, MarkActionId },
            byAction.Keys.OrderBy(key => key, StringComparer.Ordinal),
            "候选动作集合必须恰好是规则包声明的两个 Action");
        Check.Sequence(new[] { "1", "2" }, Values(byAction[AddActionId], AddDeltaFieldId), "Add 的候选参数域必须恰好是 choices 声明的 [1,2]");
        Check.Sequence(new[] { "1", "3" }, Values(byAction[MarkActionId], MarkAmountFieldId), "Mark 的候选参数域必须恰好是 choices 声明的 [1,3]");
        Check.Equal(
            snapshot.LegalActions.Count,
            snapshot.LegalActions.Select(action => action.CandidateId).Distinct(StringComparer.Ordinal).Count(),
            "candidate_id 必须唯一");
    }

    // ---- E-06 -----------------------------------------------------------------

    private static void RejectionsAndIdempotence()
    {
        var fixture = new FixtureHost(nameof(RejectionsAndIdempotence), "extended.spire");
        var host = fixture.Host;

        var wrongIdentity = host.Dispatch(Req(
            "e06-bad-identity",
            Operation.Start,
            ("expected_identity", new IdentityDto(NetProtocol.V02, "net:ruleset:not-this", host.Profile.CatalogId, host.Profile.BackendProfileId).ToWire()),
            ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile)),
            ("seed", WireValue.Str(BaselineSeed))));
        Check.Equal("version_mismatch", ErrorCode(wrongIdentity), "错 expected_identity 必须在启动前拒绝");
        Check.Equal(0, host.SessionCount, "启动前拒绝不得创建会话");

        var (sessionId, first) = Start(fixture, "e06-start", BaselineSeed);
        var engine = host.GetSessionForTests(sessionId);
        var versionBefore = engine.EngineForTests.StateVersion;

        var stale = Step(fixture, "e06-stale", sessionId, first.ObservationId + 1, Candidate(first, MarkActionId, MarkAmountFieldId, "3"));
        Check.Equal("stale_observation", ErrorCode(stale), "过期 expected_observation_id 必须拒绝");
        Check.Equal(versionBefore, engine.EngineForTests.StateVersion, "被拒绝的 step 不得推进状态");

        var unknownCandidate = Step(fixture, "e06-unknown", sessionId, first.ObservationId, "c-0000000000000000");
        Check.Equal("illegal_candidate", ErrorCode(unknownCandidate), "未知 candidate_id 必须拒绝");
        Check.Equal(versionBefore, engine.EngineForTests.StateVersion, "被拒绝的 step 不得推进状态");

        var candidate = Candidate(first, MarkActionId, MarkAmountFieldId, "3");
        var accepted = Step(fixture, "e06-mark", sessionId, first.ObservationId, candidate);
        Check.True(accepted.Ok, $"合法候选必须成功：{accepted.Error?.Code} {accepted.Error?.Message}");
        var replay = Step(fixture, "e06-mark", sessionId, first.ObservationId, candidate);
        Check.True(replay.Ok, "原样重发必须成功");
        Check.Equal(WireCanonical.Text(accepted.Result!), WireCanonical.Text(replay.Result!), "原样重发必须得到同一结果");

        var after = Observe(fixture, "e06-observe-2", sessionId);
        Check.Equal(3L, Field(after, BumpFieldId), "原样重发只执行一次：新增字段只增加一次");

        var conflict = Step(fixture, "e06-mark", sessionId, first.ObservationId, Candidate(first, MarkActionId, MarkAmountFieldId, "1"));
        Check.Equal("request_conflict", ErrorCode(conflict), "同 request_id 换载荷必须冲突");
        Check.Equal(3L, Field(Observe(fixture, "e06-observe-3", sessionId), BumpFieldId), "冲突请求不得执行");

        var staleAgain = Step(fixture, "e06-stale-2", sessionId, first.ObservationId, candidate);
        Check.Equal("stale_observation", ErrorCode(staleAgain), "已过期的观察号必须始终拒绝");
    }

    // ---- E-07 -----------------------------------------------------------------

    private static void UnsupportedMetadataIsExplicit()
    {
        var directory = Path.Combine(Path.GetTempPath(), "decl-ext-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var unknownKey = Path.Combine(directory, "unknown-key.json");
            File.WriteAllText(unknownKey, Minimal(extra: "\"silent_drop\": {}"));
            ExpectUnsupported(() => TextRuleProviderSpec.Load(unknownKey), "未知顶层键");

            var unknownUse = Path.Combine(directory, "unknown-use.json");
            File.WriteAllText(unknownUse, Minimal(collections: "\"declaration:Slots\": { \"use\": \"member_list\" }"));
            ExpectUnsupported(() => TextRuleProviderSpec.Load(unknownUse), "未知集合用途");

            var missingIds = Path.Combine(directory, "missing-ids.json");
            File.WriteAllText(missingIds, Minimal(content: "\"declaration:Marker\": { \"field_name\": \"Contents\" }"));
            ExpectUnsupported(() => TextRuleProviderSpec.Load(missingIds), "内容条目缺少 content_ids");

            var minimal = Path.Combine(directory, "minimal.json");
            File.WriteAllText(minimal, Minimal());
            var spec = TextRuleProviderSpec.Load(minimal);
            Check.Equal(TextRuleProviderSpec.SupportedFormat, spec.FixtureFormat, "最小描述文件必须可加载");
            Check.Equal("declaration:PlayerView", spec.ProjectionId, "最小描述文件必须读回投影 id");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string Minimal(string? extra = null, string? collections = null, string? content = null)
    {
        var members = new List<string>
        {
            "\"fixture_format\": \"" + TextRuleProviderSpec.SupportedFormat + "\"",
            "\"static_build_id\": \"net-text-rules-declaration-extension-check\"",
            "\"scenario_profile\": \"net-text-rules-declaration-extension-check\"",
            "\"projection_id\": \"declaration:PlayerView\"",
            "\"participant_role\": \"player\"",
            "\"default_game_seed\": \"DECL-EXT-CHECK\"",
            "\"terminal_outcomes\": { \"declaration:ReachedLimit\": \"win\" }",
            "\"known_positions\": {}",
        };

        if (content is not null)
        {
            members.Add("\"content\": { " + content + " }");
        }

        if (collections is not null)
        {
            members.Add("\"collections\": { " + collections + " }");
        }

        if (extra is not null)
        {
            members.Add(extra);
        }

        return "{\n  " + string.Join(",\n  ", members) + "\n}\n";
    }

    private static void ExpectUnsupported(Action action, string what)
    {
        try
        {
            action();
        }
        catch (NotSupportedException)
        {
            return;
        }

        throw new CheckFailedException($"{what}：期望显式 NotSupportedException，但没有抛错（不得静默丢弃公开事实）");
    }

    // ---- E-08 -----------------------------------------------------------------

    /// <summary>
    /// text-rules 场景的每类真实响应帧都必须通过 **0.2** 机器 schema（C# 端直接读同一份文件，
    /// 不另造形状）。Python 侧 `net-the-spire/tools/declaration_extension_check.py` 在真实宿主进程上
    /// 用同一份 schema 独立复核同一批载荷，两侧互为对照。
    /// </summary>
    private static void SchemaConformance()
    {
        var schemaPath = Path.Combine(RepositoryRoot(), "docs", "specs", "net", "0.2", "wire.schema.json");
        Check.True(File.Exists(schemaPath), $"缺少 0.2 wire schema：{schemaPath}");
        var schema = LiteSchema.FromJson(File.ReadAllText(schemaPath));

        var fixture = new FixtureHost(nameof(SchemaConformance), "extended.spire");
        var described = fixture.Host.Dispatch(Req("e08-describe", Operation.Describe));
        Check.True(described.Ok, "describe 必须成功");
        var catalog = fixture.Host.Dispatch(Req("e08-catalog", Operation.GetCatalog,
            ("catalog_id", WireValue.Str(fixture.Host.Profile.CatalogId))));
        Check.True(catalog.Ok, "get_catalog 必须成功");
        var started = fixture.Host.Dispatch(Req(
            "e08-start",
            Operation.Start,
            ("expected_identity", Identity(fixture.Host)),
            ("scenario_profile", WireValue.Str(fixture.Host.SupportedScenarioProfile)),
            ("seed", WireValue.Str(BaselineSeed))));
        Check.True(started.Ok, $"start 必须成功：{started.Error?.Code} {started.Error?.Message}");
        var sessionId = StringField(started, "session_id")!;
        var first = Snapshot(started);
        var mark = Candidate(first, MarkActionId, MarkAmountFieldId, "3");
        var stepped = Step(fixture, "e08-step", sessionId, first.ObservationId, mark);
        Check.True(stepped.Ok, $"执行新增动作必须成功：{stepped.Error?.Code} {stepped.Error?.Message}");
        var observed = fixture.Host.Dispatch(Req("e08-observe", Operation.Observe, ("session_id", WireValue.Str(sessionId))));
        Check.True(observed.Ok, "observe 必须成功");
        var rejected = fixture.Host.Dispatch(Req(
            "e08-bad",
            Operation.Start,
            ("expected_identity", new IdentityDto("0.1", fixture.Host.Profile.RulesetId,
                fixture.Host.Profile.CatalogId, fixture.Host.Profile.BackendProfileId).ToWire()),
            ("scenario_profile", WireValue.Str(fixture.Host.SupportedScenarioProfile)),
            ("seed", WireValue.Str(BaselineSeed))));
        // 0.2 的 Identity 把 protocol_version 固定为 "0.2"：旧版本号的载荷在解析身份时就非法（invalid_input，
        // 尚未推进），与跨版本身份不同的 version_mismatch（E-06）是两条不同的拒绝路径。
        Check.Equal("invalid_input", ErrorCode(rejected), "非 0.2 的身份载荷必须在推进前被拒");

        var documents = new (string Role, WireValue Document)[]
        {
            ("describe/result", described.ToWire()),
            ("get_catalog/result", catalog.ToWire()),
            ("start/result", started.ToWire()),
            ("step/result", stepped.ToWire()),
            ("observe/result", observed.ToWire()),
            ("error/not_started", rejected.ToWire()),
        };

        foreach (var (role, document) in documents)
        {
            var errors = schema.ValidateDefinition("Response", document);
            Check.True(errors.Count == 0,
                $"text-rules 的 0.2 载荷违反 schema（{role}）：{string.Join(" | ", errors.Take(3))}");
        }
    }

    private static void ObservationBoundaries()
    {
        var fixture = new FixtureHost(nameof(ObservationBoundaries), "boundary-decisions.spire");
        var (sessionId, initial) = Start(fixture, "boundary-start", BaselineSeed);
        var engine = fixture.Host.GetSessionForTests(sessionId).EngineForTests;
        var storageVersion = engine.StateVersion;
        var snapshot = initial;
        var sequence = 0;
        Advance(snapshot.LegalActions.Single(a => a.ActionType == "net:action:boundary:ChooseTwice"));
        Check.Equal(storageVersion, engine.StateVersion, "first pause does not write entity state");
        var answer = snapshot.LegalActions.Single();
        Check.Equal("net:action:net.answer_decision", answer.ActionType, "first native choice is pending");
        Advance(answer);
        Check.Equal(storageVersion, engine.StateVersion, "second pause does not write entity state");
        Check.Equal(answer.CandidateId, snapshot.LegalActions.Single().CandidateId, "consecutive choices deliberately have the same semantic answer handle");
        Advance(snapshot.LegalActions.Single());
        Check.Equal("1", ((WireString)snapshot.Entities.Single().Fields.Single().Value!).Value, "completion writes exactly once");
        var afterCompletion = engine.StateVersion;
        Advance(snapshot.LegalActions.Single(a => a.ActionType == "net:action:boundary:Noop"));
        Check.Equal(afterCompletion, engine.StateVersion, "a successful empty action also leaves entity state untouched");

        // Controlled numeric boundary; this is not a normal-run gameplay initialization.
        typeof(ControlSession).GetField("_observationId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(fixture.Host.GetSessionForTests(sessionId), WireCanonical.SafeIntegerMax);
        snapshot = Observe(fixture, "boundary-limit-observe", sessionId);
        var atLimit = Signature();
        var exhausted = Step(fixture, "boundary-limit", sessionId, snapshot.ObservationId,
            snapshot.LegalActions.Single(a => a.ActionType == "net:action:boundary:Noop").CandidateId);
        Check.Equal("capacity_exceeded", ErrorCode(exhausted), "observation exhaustion rejects before executing");
        Check.Equal(atLimit, Signature(), "observation exhaustion preserves state and pending work");
        Check.Equal(WireCanonical.SafeIntegerMax, Observe(fixture, "boundary-limit-after", sessionId).ObservationId, "exhaustion cannot wrap the version");

        void Advance(LegalActionDto candidate)
        {
            var previous = snapshot;
            var requestId = "boundary-step-" + ++sequence;
            var result = Step(fixture, requestId, sessionId, previous.ObservationId, candidate.CandidateId);
            Check.True(result.Ok, "real control step succeeds");
            snapshot = Snapshot(result);
            Check.True(snapshot.ObservationId > previous.ObservationId, "every settled control boundary advances the public observation version");
            var before = Signature();
            Check.Equal(WireCanonical.Text(snapshot.ToWire()), WireCanonical.Text(Observe(fixture, requestId + "-observe", sessionId).ToWire()), "observe preserves the stable version and full snapshot");
            var replay = Step(fixture, requestId, sessionId, previous.ObservationId, candidate.CandidateId);
            Check.Equal(WireCanonical.Text(result.ToWire()), WireCanonical.Text(replay.ToWire()), "same request returns its original response before checking the old version");
            var stale = Step(fixture, requestId + "-stale", sessionId, previous.ObservationId, candidate.CandidateId);
            Check.Equal("stale_observation", ErrorCode(stale), "a different request with the previous version is stale even when the candidate handle still exists");
            Check.Equal(WireCanonical.Text(stale.ToWire()), WireCanonical.Text(Step(fixture, requestId + "-stale", sessionId, previous.ObservationId, candidate.CandidateId).ToWire()), "rejected requests also retain their cached response");
            Check.Equal(before, Signature(), "observe, cached replay and rejection preserve state, RNG and the pending decision");
            Check.Equal(WireCanonical.Text(snapshot.ToWire()), WireCanonical.Text(Observe(fixture, requestId + "-after", sessionId).ToWire()), "rejections do not advance the public version");
        }
        string Signature() => engine.State.StateHash() + System.Text.Json.JsonSerializer.Serialize(engine.Random.Capture())
            + System.Text.Json.JsonSerializer.Serialize(engine.PendingDecision) + engine.StateVersion + engine.Status;
    }

    private static void OrderedPairs()
    {
        var fixture = new FixtureHost(nameof(OrderedPairs), "ordered-pair.spire");
        var (sessionId, initial) = Start(fixture, "pair-start", BaselineSeed);
        var start = initial.LegalActions.Single(a => a.ActionType == "net:action:pair:ChoosePair");
        var paused = Step(fixture, "pair-open", sessionId, initial.ObservationId, start.CandidateId);
        Check.True(paused.Ok, "generic pair pause publishes a real stable snapshot");
        var snapshot = Snapshot(paused); var engine = fixture.Host.GetSessionForTests(sessionId).EngineForTests;
        Check.Equal(2, engine.PendingDecision!.Minimum, "pair minimum is retained");
        Check.Equal(2, engine.PendingDecision.Maximum, "pair maximum is retained");
        Check.Equal(12, snapshot.LegalActions.Count, "four visible instances have all twelve ordered distinct pairs");
        string Instance(LegalActionDto a, string id) => ((WireString)a.Fields.Single(f => f.FieldId == id).Value!).Value;
        var first = DerivedPublicProfile.AnswerInstanceFieldId; var second = DerivedPublicProfile.AnswerSecondInstanceFieldId;
        var pairs = snapshot.LegalActions.Select(a => (Instance(a, first), Instance(a, second))).ToArray();
        Check.True(pairs.All(p => p.Item1 != p.Item2) && pairs.Distinct().Count() == 12, "no repeated identities or duplicate semantic candidates");
        Check.True(pairs.All(p => pairs.Contains((p.Item2, p.Item1))), "both answer orders are present");
        var chosen = snapshot.LegalActions[0]; var one = Instance(chosen, first); var two = Instance(chosen, second);
        long Value(PublicSnapshotDto s, string id) => long.Parse(((WireString)s.Entities.Single(e => e.InstanceId == id).Fields.Single(f => f.FieldId == ValueFieldId).Value!).Value, System.Globalization.CultureInfo.InvariantCulture);
        var beforeOne = Value(snapshot, one); var beforeTwo = Value(snapshot, two);
        var response = Step(fixture, "pair-answer", sessionId, snapshot.ObservationId, chosen.CandidateId);
        Check.True(response.Ok, "one real candidate resumes the full pair");
        var complete = Snapshot(response);
        Check.Equal(beforeOne + 10, Value(complete, one), "first selected identity executes first");
        Check.Equal(beforeTwo + 20, Value(complete, two), "second selected identity executes second");
        Check.True(engine.PendingDecision is null, "no second single-select pause is invented");
        var signature = engine.State.StateHash() + System.Text.Json.JsonSerializer.Serialize(engine.Random.Capture());
        Check.Equal(WireCanonical.Text(response.ToWire()), WireCanonical.Text(Step(fixture, "pair-answer", sessionId, snapshot.ObservationId, chosen.CandidateId).ToWire()), "same request returns its original pair response");
        var stale = Step(fixture, "pair-stale", sessionId, snapshot.ObservationId, chosen.CandidateId);
        Check.Equal("stale_observation", ErrorCode(stale), "fresh stale request cannot execute a pair again");
        Check.Equal(WireCanonical.Text(stale.ToWire()), WireCanonical.Text(Step(fixture, "pair-stale", sessionId, snapshot.ObservationId, chosen.CandidateId).ToWire()), "stale pair rejection is cached");
        Check.Equal(signature, engine.State.StateHash() + System.Text.Json.JsonSerializer.Serialize(engine.Random.Capture()), "pair replay and rejection preserve state and random streams");
    }

    // ---- 脚手架 ---------------------------------------------------------------

    /// <summary>一个 fixture Host：同一份通用提供方代码 + 同一份描述文件，只有规则包入口文件不同。</summary>
    private sealed class FixtureHost
    {
        public FixtureHost(string check, string ruleFile)
        {
            CheckName = check;
            Spec = TextRuleProviderSpec.Load(FixturePath("public-metadata.json"));
            var setup = TextRuleScenario.Create(new[] { FixturePath(ruleFile) }, Spec, ledgerCapacity: 4096);
            Host = setup.Host;
            PublicBuildIdentity = setup.PublicBuildIdentity;
        }

        private string CheckName { get; }

        public TextRuleProviderSpec Spec { get; }

        public ControlHost Host { get; }

        public string PublicBuildIdentity { get; }

        public PublicCatalogDto Catalog()
        {
            var response = Host.Dispatch(Req(
                CheckName + "-catalog",
                Operation.GetCatalog,
                ("catalog_id", WireValue.Str(Host.Profile.CatalogId))));
            Check.True(response.Ok, $"get_catalog 必须成功：{response.Error?.Code} {response.Error?.Message}");
            return PublicCatalogDto.FromWire(response.Result!, NetProtocol.V02);
        }
    }

    private static (string SessionId, PublicSnapshotDto Snapshot) Start(FixtureHost fixture, string requestId, string seed)
    {
        var response = fixture.Host.Dispatch(Req(
            requestId,
            Operation.Start,
            ("expected_identity", Identity(fixture.Host)),
            ("scenario_profile", WireValue.Str(fixture.Host.SupportedScenarioProfile)),
            ("seed", WireValue.Str(seed))));
        Check.True(response.Ok, $"start 必须成功：{response.Error?.Code} {response.Error?.Message}");
        Check.Equal(seed, StringField(response, "seed"), "0.2 start 结果必须回显被接受的游戏 seed 字符串");
        var sessionId = StringField(response, "session_id");
        Check.True(sessionId is not null, "start 必须给出 session_id");
        return (sessionId!, Snapshot(response));
    }

    private static PublicSnapshotDto Observe(FixtureHost fixture, string requestId, string sessionId)
    {
        var response = fixture.Host.Dispatch(Req(requestId, Operation.Observe, ("session_id", WireValue.Str(sessionId))));
        Check.True(response.Ok, $"observe 必须成功：{response.Error?.Code} {response.Error?.Message}");
        return Snapshot(response);
    }

    private static ResponseDto Step(FixtureHost fixture, string requestId, string sessionId, long observationId, string candidateId) =>
        fixture.Host.Dispatch(Req(
            requestId,
            Operation.Step,
            ("session_id", WireValue.Str(sessionId)),
            ("expected_observation_id", WireValue.Integer(observationId)),
            ("candidate_id", WireValue.Str(candidateId))));

    private static WireValue ObservedSnapshotWire(FixtureHost fixture, string sessionId)
    {
        var response = fixture.Host.Dispatch(Req(sessionId + "-raw", Operation.Observe, ("session_id", WireValue.Str(sessionId))));
        Check.True(response.Ok, "observe 必须成功");
        return Field(response, "snapshot")!;
    }

    private static PublicSnapshotDto Snapshot(ResponseDto response) =>
        PublicSnapshotDto.FromWire(Field(response, "snapshot")!, NetProtocol.V02);

    private static string Candidate(PublicSnapshotDto snapshot, string actionType, string fieldId, string value)
    {
        var matches = snapshot.LegalActions
            .Where(action => string.Equals(action.ActionType, actionType, StringComparison.Ordinal))
            .Where(action => string.Equals(FieldText(action, fieldId), value, StringComparison.Ordinal))
            .ToList();
        Check.Equal(1, matches.Count, $"候选 {actionType} 中 {fieldId}={value} 必须恰好一个");
        return matches[0].CandidateId;
    }

    private static List<string> Values(IEnumerable<LegalActionDto> actions, string fieldId) =>
        actions.Select(action => FieldText(action, fieldId)).OrderBy(text => text, StringComparer.Ordinal).ToList();

    private static string FieldText(LegalActionDto action, string fieldId)
    {
        var field = action.Fields.SingleOrDefault(item => string.Equals(item.FieldId, fieldId, StringComparison.Ordinal))
            ?? throw new CheckFailedException($"候选 {action.ActionType} 缺少参数 {fieldId}");
        return field.Value is WireString text
            ? text.Value
            : throw new CheckFailedException($"候选参数 {fieldId} 必须是规范字符串，实际 {field.Value?.TypeName ?? "无值"}");
    }

    private static long Field(PublicSnapshotDto snapshot, string fieldId)
    {
        var instance = snapshot.Entities.SingleOrDefault(entity => string.Equals(entity.KindId, MarkerKindId, StringComparison.Ordinal))
            ?? throw new CheckFailedException("快照必须包含 Marker 实例");
        var field = instance.Fields.SingleOrDefault(item => string.Equals(item.FieldId, fieldId, StringComparison.Ordinal))
            ?? throw new CheckFailedException($"快照必须包含公开字段 {fieldId}");
        Check.Equal("known", field.State, $"公开字段 {fieldId} 必须是 known");
        return field.Value is WireString text
            ? long.Parse(text.Value, System.Globalization.CultureInfo.InvariantCulture)
            : throw new CheckFailedException($"公开字段 {fieldId} 的值必须是规范十进制字符串");
    }

    private static bool HasField(PublicSnapshotDto snapshot, string fieldId) =>
        snapshot.Entities.SelectMany(entity => entity.Fields)
            .Any(field => string.Equals(field.FieldId, fieldId, StringComparison.Ordinal));

    private static WireValue Identity(ControlHost host) =>
        new IdentityDto(NetProtocol.V02, host.Profile.RulesetId, host.Profile.CatalogId, host.Profile.BackendProfileId).ToWire();

    private static RequestDto Req(string id, Operation operation, params (string Key, WireValue Value)[] members) =>
        new(id, operation, new WireObject(members.Select(member => new KeyValuePair<string, WireValue>(member.Key, member.Value))));

    private static WireValue? Field(ResponseDto response, string key) => response.Result is WireObject obj ? obj.Get(key) : null;

    private static string? StringField(ResponseDto response, string key) => Field(response, key) is WireString text ? text.Value : null;

    private static string ErrorCode(ResponseDto response) => response.Error?.Code ?? "<ok>";

    private static string FixturePath(string name) =>
        Path.Combine(RepositoryRoot(), "net-the-spire", "fixtures", "declaration-extension", name);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AutoTheSpire.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new CheckFailedException($"找不到仓库根：自 '{AppContext.BaseDirectory}' 向上未发现 AutoTheSpire.sln");
    }
}
