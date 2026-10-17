using System.Diagnostics;
using BaseSpire.Slice;
using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Control;
using NetTheSpire.Projection;

namespace NetTheSpire.Tests;

/// <summary>
/// P-02 的**进程内**分项测量：规则推进、投影、codec 三段分别计时，并按候选规模分档报告分布。
///
/// 纪律（开工清单 §13 P-02）：
/// <list type="bullet">
/// <item>负载必须**可重复**：同一受控切片、同一种子、同一策略；分档只看公开候选规模与动作类型；</item>
/// <item>**只报告测量**，不写"达标"：没有批准的预算就不给结论；</item>
/// <item>诊断读取（RNG 诊断、状态哈希、规则集转储）不得改变状态或 RNG，也不得进入公开数据。</item>
/// </list>
///
/// wire 端到端把这三段与传输/进程边界**捆在一起**，因此那边的数字不能当作这三段的和（见 P-02 报告）。
/// </summary>
internal static class MeasurementChecks
{
    private const string PlayerViewId = "base-spire:PlayerView";
    private const string RunToken = "p02";
    private const int Repetitions = 3;

    private static readonly string[] Buckets = ["minimal", "typical", "large", "long-settlement"];

    public static IEnumerable<(string Name, Action Body)> Build()
    {
        yield return ("P-02 规则推进/投影/codec 分项计时（按候选规模分档，可重复）[真实]", StageTimings);
        yield return ("P-02 诊断读取不改变状态/RNG，也不进入公开数据 [真实]", DiagnosticsArePureAndSeparate);
    }

    // -- 分档计时 -------------------------------------------------------------------------------------------

    private static void StageTimings()
    {
        var samples = new Dictionary<string, List<(double Advance, double Project, double Codec, int Candidates)>>(
            StringComparer.Ordinal);
        foreach (var bucket in Buckets) samples[bucket] = [];

        var classOrder = new List<string>();
        RunEpisode(samples, classOrder);
        var repeatOrder = new List<string>();
        RunEpisode(new Dictionary<string, List<(double, double, double, int)>>(StringComparer.Ordinal)
        {
            ["minimal"] = [], ["typical"] = [], ["large"] = [], ["long-settlement"] = [],
        }, repeatOrder);

        Check.Sequence(classOrder, repeatOrder, "同一受控配置必须给出同一分档序列（负载可重复）");

        var total = 0;
        foreach (var bucket in Buckets)
        {
            var rows = samples[bucket];
            total += rows.Count;
            Console.WriteLine($"       {bucket}: n={rows.Count} " +
                $"advance {Describe(rows.Select(r => r.Advance))} " +
                $"project {Describe(rows.Select(r => r.Project))} " +
                $"codec {Describe(rows.Select(r => r.Codec))} " +
                $"candidates {Describe(rows.Select(r => (double)r.Candidates))}");
        }

        Check.True(total > 0, "至少要有一次可测的转移");
        Check.True(samples["minimal"].Count + samples["typical"].Count + samples["large"].Count > 0,
            "至少要有一次出牌转移用于规模分档");
        Check.True(samples["long-settlement"].Count > 0, "至少要有一个长结算样本（结束回合）");
        Check.True(samples["large"].Count > 0, "在受控切片里应能观察到较大候选规模");
        // 分档必须真的不同（否则"分档"没有判别力）。
        var minimalMax = samples["minimal"].Count == 0 ? -1 : samples["minimal"].Max(r => r.Candidates);
        var largeMin = samples["large"].Count == 0 ? int.MaxValue : samples["large"].Min(r => r.Candidates);
        Check.True(minimalMax < largeMin, $"分档必须真的不同：minimal 最多 {minimalMax}，large 至少 {largeMin}");
    }

    private static void RunEpisode(
        Dictionary<string, List<(double Advance, double Project, double Codec, int Candidates)>> samples,
        List<string> classOrder)
    {
        var host = NewHost();
        var sessionId = StartSession(host, "p02");
        var control = host.GetSessionForTests(sessionId);

        for (var step = 0; step < 30; step++)
        {
            var observed = Observe(host, sessionId);
            var snapshot = (WireObject)Field(observed, "snapshot")!;
            var status = ((WireString)snapshot.Members.Single(m => m.Key == "status").Value).Value;
            if (status == "terminal") break;
            var actions = LegalActions(snapshot);
            if (actions.Count == 0) break;

            var endTurn = actions.FirstOrDefault(a => ActionType(a) == "net:action:base-spire:EndTurn");
            var play = actions.Where(a => ActionType(a) == "net:action:base-spire:PlayCard").ToList();
            // 暂停期的合法动作只有决策应答：它同样是"一次真实结算"，必须继续驱动而不是停止。
            var answer = actions.FirstOrDefault(a => ActionType(a) == "net:action:net.answer_decision");
            var chosen = play.Count > 0 ? play[0] : endTurn ?? answer;
            if (chosen is null) break;
            var isLongSettlement = endTurn is not null && ReferenceEquals(chosen, endTurn);

            // 规则推进 = 引擎执行一次动作（Submit/Answer 到稳定边界）；投影与 codec 分别计时。
            var advance = Stopwatch.StartNew();
            var response = host.Dispatch(Req($"s-{step}", Operation.Step,
                ("session_id", WireValue.Str(sessionId)),
                ("expected_observation_id", WireValue.Integer(ObservationIdOf(observed))),
                ("candidate_id", WireValue.Str(CandidateId(chosen)))));
            advance.Stop();
            if (!response.Ok)
            {
                break; // 决策/终局等稳定边界：本步不计入样本
            }

            var project = Stopwatch.StartNew();
            var view = control.EngineForTests.Observe(SliceManifest.Player, PlayerViewId);
            project.Stop();
            _ = view;

            var codec = Stopwatch.StartNew();
            _ = WireCanonical.Text(snapshot);
            codec.Stop();

            var bucket = isLongSettlement ? "long-settlement" : BucketOf(actions.Count);
            classOrder.Add(bucket);
            samples[bucket].Add((advance.Elapsed.TotalMilliseconds, project.Elapsed.TotalMilliseconds,
                codec.Elapsed.TotalMilliseconds, actions.Count));
        }
    }

    private static string BucketOf(int candidates) => candidates switch
    {
        <= 2 => "minimal",
        <= 5 => "typical",
        _ => "large",
    };

    private static string Describe(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(value => value).ToList();
        if (ordered.Count == 0) return "n/a";
        return $"p50={ordered[ordered.Count / 2]:F3}ms p90={ordered[Math.Min(ordered.Count - 1,
            (int)(ordered.Count * 0.9))]:F3}ms max={ordered[^1]:F3}ms";
    }

    // -- 诊断纯度 -------------------------------------------------------------------------------------------

    private static void DiagnosticsArePureAndSeparate()
    {
        var host = NewHost();
        var sessionId = StartSession(host, "p02-diag");
        var control = host.GetSessionForTests(sessionId);
        var engine = control.EngineForTests;

        var hashBefore = engine.State.StateHash();
        var drawsBefore = engine.Random.TotalDraws;
        for (var stream = 0; stream < 3; stream++)
        {
            // 诊断读取：RNG 诊断、状态哈希、完整观察。都必须是纯读。
            _ = engine.Random.Diagnostics();
            _ = engine.State.StateHash();
            _ = engine.Observe(SliceManifest.Player, PlayerViewId);
            _ = engine.LegalActions(SliceManifest.Player);
        }

        Check.Equal(hashBefore, engine.State.StateHash(), "诊断读取不得改变状态");
        Check.Equal(drawsBefore, engine.Random.TotalDraws, "诊断读取不得消费 RNG");

        // 诊断不得进入公开数据：wire 与目录里都不该出现诊断来源的键。
        var observed = Observe(host, sessionId);
        var snapshotText = WireCanonical.Text(Field(observed, "snapshot"));
        foreach (var forbidden in new[] { "diagnostic", "trace", "rng_state", "internal", "state_hash" })
        {
            Check.False(snapshotText.Contains(forbidden, StringComparison.Ordinal),
                $"公开快照不得包含诊断内容（{forbidden}）");
        }

        var catalogText = WireCanonical.Text(CatalogProjector.Project(SlicePublicProfile.Create()).ToWire());
        Check.False(catalogText.Contains("diagnostic", StringComparison.Ordinal),
            "目录不得包含诊断声明");
    }

    // -- 夹具 -----------------------------------------------------------------------------------------------

    private static ControlHost NewHost() => new(
        Rules(), SlicePublicProfile.Create(), SliceManifest.Player, SliceManifest.PlayerRole, PlayerViewId,
        20261001UL, 4096, RunToken,
        // 切片包声明的计算资产要求宿主装后端（与 AcceptanceChecks/ControlChecks 同一组合层合同）。
        computeBackend: BaseSpireComputeBackend.Instance);

    private static RuleSet Rules()
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build().Build());
        return builder.Build();
    }

    private static string StartSession(ControlHost host, string requestId)
    {
        var profile = host.Profile;
        var started = host.Dispatch(Req(requestId, Operation.Start,
            ("expected_identity", new IdentityDto(profile.ProtocolVersion, profile.RulesetId,
                profile.CatalogId, profile.BackendProfileId).ToWire()),
            ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile))));
        Check.True(started.Ok, $"start 应当成功：{started.Error?.Code}");
        return StringField(started, "session_id")!;
    }

    private static ResponseDto Observe(ControlHost host, string sessionId) =>
        host.Dispatch(Req("o-" + Guid.NewGuid().ToString("N")[..8], Operation.Observe,
            ("session_id", WireValue.Str(sessionId))));

    private static RequestDto Req(string id, Operation operation, params (string Key, WireValue Value)[] members) =>
        new(id, operation, new WireObject(members.Select(member =>
            new KeyValuePair<string, WireValue>(member.Key, member.Value))));

    private static WireValue Field(ResponseDto response, string key)
    {
        Check.True(response.Result is WireObject, $"响应结果必须是对象（{key}）");
        foreach (var member in ((WireObject)response.Result!).Members)
        {
            if (member.Key == key) return member.Value;
        }

        throw new CheckFailedException($"响应结果缺少字段 {key}");
    }

    private static string? StringField(ResponseDto response, string key) =>
        Field(response, key) is WireString text ? text.Value : null;

    private static long ObservationIdOf(ResponseDto observeResponse) =>
        long.Parse(((WireInteger)((WireObject)Field(observeResponse, "snapshot")!)
            .Members.Single(m => m.Key == "observation_id").Value).Digits);

    private static IReadOnlyList<WireObject> LegalActions(WireObject snapshot) =>
        ((WireArray)snapshot.Members.Single(m => m.Key == "legal_actions").Value).Items.OfType<WireObject>().ToList();

    private static string ActionType(WireObject action) =>
        ((WireString)action.Members.Single(m => m.Key == "action_type").Value).Value;

    private static string CandidateId(WireObject action) =>
        ((WireString)action.Members.Single(m => m.Key == "candidate_id").Value).Value;
}
