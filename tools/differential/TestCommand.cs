using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Differential;

/// <summary>
/// ENV-T04 的**严格 regression 入口**：`test --suite regression`。
///
/// 它和探索性入口 <see cref="CompareCommand"/> 的区别是**判定口径**，不是比较算法：
/// <list type="bullet">
/// <item>只接受**严格 suite**（必须声明 <c>expected_case_ids</c> 分母、<c>target_scope</c>、
///       <c>build_identity</c> 与每个 case 的预登记字段）；缺 case、未注册 case、缺维度、
///       错误终点、unsupported、参考缺失、异常、超时、首差**都返回非零**；</item>
/// <item>两侧执行与首差比较复用同一套 <see cref="BaseBackend"/> / <see cref="ReferenceBackends"/> /
///       <see cref="Comparator"/> / <see cref="ComparatorSelfTest"/>，不另写第二份“我以为会比较”的断言；</item>
/// <item>参考侧独立执行（或读**外部参考记录**），绝不复制 base 结果充当参考。</item>
/// </list>
///
/// <para>
/// **退出码**：<c>0</c> = 完整 suite 全部 case 为双侧 <c>passed</c> 或单侧 <c>self_checked</c>（且至少有一个双侧
/// <c>passed</c>、比较器自检通过、每个可计成功结果都通过了声明的必需维度/预期终点校验）；<c>1</c> = 跑完但有 case 不计通过
/// （diverged / unsupported_* / blocked_compare / missing_reference_record / reference_fault / fault /
/// invalid_fixture / invalid_action / wiring_failed / missing_dimension / wrong_terminal / timeout）或比较器自检失败，
/// 或 <c>--only</c> 只跑了子集；<c>2</c> = 用法或 suite **定义**错误（不可载入、缺 case、未注册 case、
/// 注册字段不合法、零执行）；<c>3</c> = **参考适配器不可用**（本机构建未包含 `reference/sts2-sim`），
/// 此时所有 <c>compared</c> case 都判 <c>unsupported_reference_adapter</c>，不可能是通过。
/// </para>
///
/// <para>
/// <c>self_checked</c> 只用于 <c>kind=wiring</c> 的工具/生产集成自检：它的验收标准是自身实测断言，
/// 不是双侧比较，因此**不**计入双侧 <c>passed</c>，报告中单列。它同样**不**免除预登记断言：
/// 声明了 <c>required_dimensions</c> / <c>expected_terminal</c> 就必须真的成立，否则改判
/// <c>missing_dimension</c> / <c>wrong_terminal</c> 并不计成功（否则工厂自检可绕过维度与终点门槛）。
/// </para>
///
/// <para>
/// **`--only` 语义**：定向子集只报告该子集，报告里 <c>complete=false</c> 且**永远非零**，
/// 因此它不能替代完整阶段；默认（不给 <c>--only</c>）执行 suite 全部 case。
/// </para>
/// </summary>
internal static class TestCommand
{
    public const string Version = "env-t04-test@1";
    public const int DefaultCaseTimeoutMs = 120_000;

    public static int Run(string repositoryRoot, string suiteName, string outputDirectory,
        string? only, string? referenceRecords, int caseTimeoutMs)
    {
        var suitePath = FixtureSuites.ResolvePath(repositoryRoot, suiteName);
        FixtureSuite suite;
        try
        {
            suite = FixtureSuites.Load(suitePath);
        }
        catch (Exception failure)
        {
            Console.Error.WriteLine($"无法载入严格 suite：{failure.Message}");
            return 2;
        }

        if (!suite.IsStrict)
        {
            Console.Error.WriteLine($"suite '{suite.Suite}' 不是严格 suite：必须声明 expected_case_ids 分母。"
                + "探索性夹具请用 `compare`。");
            return 2;
        }

        // 定义层自检：缺 case / 未注册 case / 注册字段不合法 / 必测分母重复都在这里失败（ENV 约束 §9）。
        var problems = FixtureSuites.Validate(suite);
        if (problems.Count > 0)
        {
            Console.Error.WriteLine("严格 suite 定义自检失败：");
            foreach (var problem in problems)
            {
                Console.Error.WriteLine("  - " + problem);
            }

            return 2;
        }

        var byId = suite.Cases.ToDictionary(item => item.Id, StringComparer.Ordinal);
        List<FixtureCase> selected;
        var partial = false;
        if (!string.IsNullOrWhiteSpace(only))
        {
            var requested = only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var matched = requested.Where(byId.ContainsKey).ToList();
            var unknown = requested.Where(id => !byId.ContainsKey(id)).ToList();
            if (matched.Count == 0)
            {
                Console.Error.WriteLine($"零执行：--only 没有匹配到任何 case（{string.Join(", ", unknown)}）。"
                    + "严格入口不允许零执行通过。");
                return 2;
            }

            if (unknown.Count > 0)
            {
                Console.Error.WriteLine($"--only 指定了 suite 里不存在的 case：{string.Join(", ", unknown)}");
                return 2;
            }

            selected = matched.Select(id => byId[id]).ToList();
            partial = selected.Count != suite.Cases.Count;
        }
        else
        {
            selected = suite.Cases.ToList();
        }

        var outputPath = Path.IsPathRooted(outputDirectory)
            ? outputDirectory
            : Path.Combine(repositoryRoot, outputDirectory.Replace('/', Path.DirectorySeparatorChar));
        var baseRecordsDirectory = Path.Combine(outputPath, "records", "base");
        var referenceRecordsDirectory = Path.Combine(outputPath, "records", "reference");
        Directory.CreateDirectory(baseRecordsDirectory);

        var externalRecords = referenceRecords is null
            ? null
            : Path.IsPathRooted(referenceRecords)
                ? referenceRecords
                : Path.Combine(repositoryRoot, referenceRecords.Replace('/', Path.DirectorySeparatorChar));

        var command = $"dotnet run --project tools/differential -- test --suite {suiteName}"
            + $" --output {outputDirectory}"
            + (only is null ? string.Empty : $" --only {only}")
            + (referenceRecords is null ? string.Empty : $" --records {referenceRecords}")
            + (caseTimeoutMs == DefaultCaseTimeoutMs ? string.Empty : $" --case-timeout-ms {caseTimeoutMs}");

        var runId = $"{suite.Suite}@{suite.FixturesVersion}+{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}";

        // -- 1. 逐 case 双侧执行（base 侧总是执行；参考侧按声明与可用性） -------------------------------------
        var results = new List<TestCaseResult>();
        var baseRecords = new List<CaseRecord>();
        var referenceRan = 0;
        foreach (var fixture in selected)
        {
            var (record, result) = ExecuteCase(fixture, externalRecords, referenceRecordsDirectory, caseTimeoutMs,
                ref referenceRan, command);
            baseRecords.Add(record);
            File.WriteAllText(Path.Combine(baseRecordsDirectory, fixture.Id + ".json"), Records.ToJson(record),
                new UTF8Encoding(false));
            results.Add(result);
        }

        // -- 2. 比较器自检：受控错误记录必须被抓到（与 compare 共用同一份自检） ------------------------------
        var selfTest = ComparatorSelfTest.Run(baseRecords, command);
        var selfTestFailures = selfTest.Count(item => !item.Detected);

        // -- 3. 判定与退出码 --------------------------------------------------------------------------------
        var referenceUnavailable = results.Any(item => item.Verdict == "unsupported_reference_adapter");
        var passed = results.Count(item => item.Verdict == "passed");
        var selfChecked = results.Count(item => item.Verdict == "self_checked");
        var failed = results.Count - passed - selfChecked;
        var complete = !partial && results.Count == suite.ExpectedCaseIds!.Count;
        int exitCode;
        if (referenceUnavailable)
        {
            exitCode = 3;
        }
        else if (selfTestFailures > 0 || failed > 0 || passed == 0 || !complete)
        {
            exitCode = 1;
        }
        else
        {
            exitCode = 0;
        }

        // -- 4. 落盘：机器报告 + 人读报告 --------------------------------------------------------------------
        var verdicts = results.GroupBy(item => item.Verdict, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var report = new JsonObject
        {
            ["test_version"] = Version,
            ["suite"] = suite.Suite,
            ["fixtures_version"] = suite.FixturesVersion,
            ["suite_path"] = Relative(repositoryRoot, suitePath),
            ["suite_sha256"] = Repo.Sha256(File.ReadAllText(suitePath)),
            ["run_id"] = runId,
            ["command"] = command,
            ["case_timeout_ms"] = caseTimeoutMs,
            ["complete"] = complete,
            ["only"] = only,
            ["target_scope"] = suite.TargetScope,
            ["expected_case_ids"] = new JsonArray(suite.ExpectedCaseIds!
                .Select(id => (JsonNode)JsonValue.Create(id)!).ToArray()),
            ["build_identity"] = suite.BuildIdentity is null ? null : new JsonObject
            {
                ["scope"] = suite.BuildIdentity.Scope,
                ["character"] = suite.BuildIdentity.Character,
                ["ascension"] = suite.BuildIdentity.Ascension,
                ["source_baseline"] = suite.BuildIdentity.SourceBaseline,
                ["reference"] = suite.BuildIdentity.Reference,
                ["note"] = suite.BuildIdentity.Note,
            },
            ["tool"] = new JsonObject
            {
                ["version"] = Version,
                ["assembly"] = typeof(TestCommand).Assembly.GetName().Name,
                ["dotnet"] = Environment.Version.ToString(),
                ["reference_adapter_available"] = ReferenceBackends.Available,
            },
            ["reference_records"] = externalRecords is not null
                ? externalRecords
                : referenceRan > 0
                    ? Relative(repositoryRoot, referenceRecordsDirectory)
                    : null,
            ["counts"] = new JsonObject
            {
                ["expected"] = suite.ExpectedCaseIds!.Count,
                ["selected"] = selected.Count,
                ["executed"] = results.Count,
                ["passed"] = passed,
                ["self_checked"] = selfChecked,
                ["failed"] = failed,
                ["reference_ran"] = referenceRan,
                ["self_test_failures"] = selfTestFailures,
            },
            ["verdicts"] = new JsonObject(verdicts.Select(pair =>
                new KeyValuePair<string, JsonNode?>(pair.Key, JsonValue.Create(pair.Value)))),
            ["exit_code"] = exitCode,
            ["self_test"] = new JsonArray(selfTest.Select(item => (JsonNode)new JsonObject
            {
                ["case"] = item.Name,
                ["expected_kind"] = item.ExpectedKind,
                ["detected"] = item.Detected,
                ["detail"] = item.Detail,
            }).ToArray()),
            ["cases"] = new JsonArray(results.Select(item => (JsonNode)JsonSerializer.SerializeToNode(item, Json)!)
                .ToArray()),
            ["notes"] = new JsonArray(
                JsonValue.Create("只有 passed 与 kind=wiring 的 self_checked 计成功（二者都要过必需维度/预期终点门槛）；"
                    + "unsupported_* / blocked_compare / missing_reference_record / "
                    + "reference_fault / fault / invalid_fixture / invalid_action / missing_dimension / "
                    + "wrong_terminal / timeout 都不计通过。"),
                JsonValue.Create("声明 required_dimensions 的 case 若在记录里没有真的抽取该维度，判定为 missing_dimension。"),
                JsonValue.Create("必需维度与预期终点门槛覆盖**所有可计成功结果**（passed 与 kind=wiring 的 "
                    + "self_checked）：self_checked 只是不要求双侧比较，不是免除预登记断言；更具体的失败"
                    + "（faulted/timeout/invalid_action/invalid_fixture/wiring_failed/unsupported_*/"
                    + "blocked_compare/reference_fault/missing_reference_record/diverged）保持原判定，"
                    + "不被这条门槛覆盖，分母也不因此改变，缺失维度只如实列出、不补占位读数。"),
                JsonValue.Create("参考侧独立执行（或读外部参考记录），不复制 base 结果；受控初始化只用于明示的机制 case。"),
                JsonValue.Create("--only 只报告所选子集，complete=false，永远非零，不能替代完整 suite。")),
        };

        var jsonPath = Path.Combine(outputPath, "regression.json");
        File.WriteAllText(jsonPath, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));
        var markdownPath = Path.Combine(outputPath, "regression.md");
        File.WriteAllText(markdownPath, Markdown(suite, results, selfTest, command, runId, exitCode, complete,
            partial, referenceUnavailable), new UTF8Encoding(false));

        // -- 5. 控制台摘要（任何失败都点名 case 与原因） ------------------------------------------------------
        Console.WriteLine($"test written: {jsonPath}");
        Console.WriteLine($"suite {suite.Suite}@{suite.FixturesVersion}；run_id={runId}；"
            + $"期望 {suite.ExpectedCaseIds!.Count} 个 case，选中 {selected.Count}，执行 {results.Count}"
            + (partial ? "（部分运行：--only，不构成完整通过）" : "（完整 suite）"));
        foreach (var item in results)
        {
            var first = item.Divergences.FirstOrDefault();
            var mark = item.Verdict is "passed" or "self_checked" ? "PASS" : "FAIL";
            Console.WriteLine($"  [{mark,-4}] {item.CaseId,-32}"
                + $" {item.Verdict,-28}"
                + (item.Detail is null ? string.Empty : $" {item.Detail}")
                + (first is null ? string.Empty : $" | 首差 {first.Path}: {first.Left} ≠ {first.Right}"));
        }

        foreach (var item in selfTest)
        {
            Console.WriteLine($"  自检 {(item.Detected ? "OK  " : "FAIL")} {item.Name}（期望 {item.ExpectedKind}）"
                + (item.Detail is null ? string.Empty : $" — {item.Detail}"));
        }

        Console.WriteLine($"退出码 {exitCode}：双侧 passed {passed}，单侧自检 self_checked {selfChecked}，"
            + $"不计通过 {failed}，自检失败 {selfTestFailures}"
            + (referenceUnavailable ? "，参考适配器不可用" : string.Empty)
            + (partial ? "，且本次是子集运行" : string.Empty));
        return exitCode;
    }

    // -- 单 case 执行 ---------------------------------------------------------------------------------------

    private static (CaseRecord Record, TestCaseResult Result) ExecuteCase(FixtureCase fixture, string? externalRecords,
        string referenceRecordsDirectory, int caseTimeoutMs, ref int referenceRan, string command)
    {
        var backend = fixture.BaseSource == BaseSources.FormalFactory ? BaseBackend.FormalName : BaseBackend.Name;
        var (record, baseTimedOut, baseFailure) = Guarded(() => BaseBackend.Run(fixture), caseTimeoutMs);
        CaseRecord baseRecord;
        if (baseTimedOut)
        {
            baseRecord = TimeoutRecord(fixture, backend, "base", caseTimeoutMs);
        }
        else if (baseFailure is not null)
        {
            baseRecord = FaultRecord(fixture, backend, baseFailure);
        }
        else
        {
            baseRecord = record!;
        }

        CaseRecord? reference = null;
        var referenceBackend = (string?)null;
        var baseBroken = baseRecord.Status is "timeout" or "faulted" or "invalid_fixture";

        // 判定优先级：base 超时/异常（夹具本身走不通）> 声明的参考范围 > base 脚本卡住 > 双侧比较。
        // 声明为 unsupported/blocked/not_applicable 的 case 不跑参考侧，但 base 侧异常仍然照实失败。
        var verdict = (string?)null;
        var detail = (string?)null;
        IReadOnlyList<Divergence> divergences = [];

        if (baseRecord.Status == "timeout")
        {
            verdict = "timeout";
            detail = baseRecord.Detail;
        }
        else if (baseBroken)
        {
            verdict = baseRecord.Status;
            detail = baseRecord.Detail;
        }
        else if (fixture.Kind == CaseKinds.Wiring)
        {
            // 工具/生产集成自检：它的验收标准是自身的实测断言（工厂拒绝/未启动会话/配置就位），
            // 不是双侧比较。因此单独记 self_checked，**不**计入双侧 passed（ENV 约束 §9）。
            verdict = baseRecord.Status == "ok" ? "self_checked" : "wiring_failed";
            detail = baseRecord.Detail;
        }
        else if (fixture.ReferenceScope != ReferenceScopes.Compared)
        {
            verdict = fixture.ReferenceScope;
            detail = fixture.ReferenceScopeReason;
        }
        else if (baseRecord.Status is "invalid_action" or "unsupported_decision")
        {
            verdict = baseRecord.Status;
            detail = baseRecord.Detail;
        }
        else if (externalRecords is not null)
        {
            var path = Path.Combine(externalRecords, fixture.Id + ".json");
            if (!File.Exists(path))
            {
                verdict = "missing_reference_record";
                detail = $"缺少参考记录 {path}（参考侧必须独立产出或冻结提供，不接受 base 结果当参考）";
            }
            else
            {
                var parsed = Records.FromJson(File.ReadAllText(path));
                if (parsed.CaseId != fixture.Id)
                {
                    verdict = "reference_fault";
                    detail = $"外部参考记录 {path} 的 case_id='{parsed.CaseId}' 与当前 case '{fixture.Id}'"
                        + " 不一致：拒绝按错误记录比较（不外推、不重新匹配）";
                }
                else
                {
                    reference = parsed;
                    referenceBackend = parsed.Backend;
                }
            }
        }
        else if (!ReferenceBackends.Available)
        {
            verdict = "unsupported_reference_adapter";
            detail = "本机构建未包含参考侧适配器（未找到 reference/sts2-sim）："
                + "本 case 无法双侧判定，严格入口返回 3。";
        }
        else
        {
            var (ran, timedOut, failure) = Guarded(() => ReferenceBackends.Run(fixture), caseTimeoutMs);
            if (timedOut)
            {
                verdict = "timeout";
                detail = $"reference 侧在 {caseTimeoutMs} ms 内没有返回";
            }
            else if (failure is not null)
            {
                verdict = "reference_fault";
                detail = $"{failure.GetType().Name}: {failure.Message}";
            }
            else
            {
                reference = ran!;
                referenceBackend = ran!.Backend;
                referenceRan++;
                Directory.CreateDirectory(referenceRecordsDirectory);
                File.WriteAllText(Path.Combine(referenceRecordsDirectory, fixture.Id + ".json"),
                    Records.ToJson(ran!), new UTF8Encoding(false));
            }
        }

        if (verdict is null && reference is not null)
        {
            var comparison = Comparator.Compare(baseRecord, reference, command);
            verdict = comparison.Verdict;
            detail = comparison.Detail;
            divergences = comparison.Divergences;
        }

        // 必需维度：声明了就必须在记录里真的抽取出来（base 侧总是查；双侧已跑时参考侧也查）。
        var missing = MissingDimensions(baseRecord, fixture.RequiredDimensions ?? []);
        if (reference is not null && reference.Status is "ok" or "terminal")
        {
            missing = [.. missing, .. MissingDimensions(reference, fixture.RequiredDimensions ?? [])
                .Select(dimension => dimension + "(reference)")];
        }

        // 流证据自洽：声明消费了随机数就必须有逐边界读数（缺证据 → missing_dimension，不补 0、不放宽）。
        var streamProblems = new List<string>();
        if (StreamSettlementProblem(baseRecord) is { } baseStreamProblem)
        {
            streamProblems.Add($"base：{baseStreamProblem}");
        }

        if (reference is not null && StreamSettlementProblem(reference) is { } referenceStreamProblem)
        {
            streamProblems.Add($"reference：{referenceStreamProblem}");
        }

        var terminalProblem = baseRecord.Status is "ok" or "terminal"
            ? TerminalProblem(baseRecord, fixture.ExpectedTerminal!)
            : null;

        // 门槛适用于**所有可计成功**结果：双侧 `passed` 与 `kind=wiring` 的 `self_checked`。
        // `self_checked` 的语义只是"不要求双侧比较"，不是"免除预登记断言"：case 既然声明了必需维度与预期终点，
        // 就必须在记录里真的成立，否则它和 `passed` 一样不能计成功（否则缺维度/错终点可绕过门槛）。
        // 更具体的失败（faulted / timeout / invalid_action / invalid_fixture / wiring_failed /
        // unsupported_* / blocked_compare / reference_fault / missing_reference_record / diverged）保持原判定，
        // 不被这条门槛覆盖；分母不变，缺失维度只如实列出，不给未抽取的维度补占位读数。
        if (verdict is "passed" or "self_checked")
        {
            if (missing.Count > 0)
            {
                verdict = "missing_dimension";
                detail = $"声明的必需维度没有抽取：{string.Join(" / ", missing)}";
            }
            else if (streamProblems.Count > 0)
            {
                verdict = "missing_dimension";
                detail = $"流维度不自洽：{string.Join("；", streamProblems)}";
            }
            else if (terminalProblem is not null)
            {
                verdict = "wrong_terminal";
                detail = terminalProblem;
            }
        }

        var result = new TestCaseResult
        {
            CaseId = fixture.Id,
            Title = fixture.Title,
            Group = fixture.Group,
            Kind = fixture.Kind,
            BaseSource = fixture.BaseSource,
            RngProfile = fixture.RngProfile,
            ReferenceScope = fixture.ReferenceScope,
            ReferenceSource = fixture.ReferenceSource,
            Initialization = fixture.Initialization,
            GameSeed = fixture.GameSeed,
            Contents = fixture.Contents ?? [],
            Variants = fixture.Variants ?? [],
            SemanticActions = fixture.SemanticActions ?? [],
            RequiredDimensions = fixture.RequiredDimensions ?? [],
            MissingDimensions = missing,
            ExpectedTerminal = fixture.ExpectedTerminal ?? string.Empty,
            TerminalProblem = terminalProblem,
            Verdict = verdict!,
            Detail = detail,
            BaseStatus = baseRecord.Status,
            BaseSessionStatus = baseRecord.SessionStatus,
            BaseTerminal = baseRecord.Terminal,
            BuildId = baseRecord.BuildId,
            SessionIdentity = baseRecord.SessionIdentity,
            BaseRngConsumed = baseRecord.RngConsumedByScript,
            BaseBoundaries = baseRecord.Boundaries.Count,
            ReferenceBackend = referenceBackend,
            ReferenceStatus = reference?.Status,
            ReferenceBoundaries = reference?.Boundaries.Count ?? 0,
            Divergences = divergences,
        };
        return (baseRecord, result);
    }

    /// <summary>必需维度实测：声明了却没有抽取出来就是失败（ENV 约束 §9）。</summary>
    private static IReadOnlyList<string> MissingDimensions(CaseRecord record, IReadOnlyList<string> required)
    {
        var missing = new List<string>();
        foreach (var dimension in required.Distinct(StringComparer.Ordinal))
        {
            var extracted = dimension switch
            {
                Dimensions.Actions => record.Boundaries.Any(item => item.Actions.Count > 0),
                Dimensions.Decisions => record.Boundaries.Any(item => item.Decisions.Count > 0),
                Dimensions.Facts => record.Boundaries.Any(item => item.Facts.Count > 0),
                Dimensions.PileOrder => record.Boundaries.Any(item =>
                    item.Facts.Any(fact => fact.Path.EndsWith(".order", StringComparison.Ordinal))),
                Dimensions.Power => record.Boundaries.Any(item =>
                    item.Facts.Any(fact => fact.Path.StartsWith("power.", StringComparison.Ordinal))),
                Dimensions.Streams => record.Boundaries.Any(item =>
                    item.Streams.Any(stream => Records.IsNamedStream(stream) && stream.Draws > 0)),
                Dimensions.Rewards => record.Boundaries.Any(item =>
                    new[] { "reward.gold", "reward.cards.order", "reward.potion.present", "reward.potion.resolved" }
                        .All(path => item.Facts.Any(f => f.Path == path))),
                Dimensions.Permanent => record.Boundaries.Count > 0 && record.Boundaries.All(item =>
                    new[] { "run.gold", "run.hp", "run.max_hp", "deck.order", "deck.instance_count", "potion.capacity", "potion.inventory" }
                        .All(path => item.Facts.Any(f => f.Path == path))),
                Dimensions.RewardClosure => record.Boundaries.LastOrDefault() is { } last
                    && last.Facts.Any(f => f.Path == "run.phase" && f.Value == "travel-ready")
                    && last.Actions.Any(a => a.Action == "base-spire:ChooseMapPoint" && a.Candidates.Count > 0),
                Dimensions.Trace => record.EngineTrace is { Records.Count: > 0 },
                Dimensions.Terminal => !string.IsNullOrWhiteSpace(record.Terminal),
                _ => false,
            };
            if (!extracted)
            {
                missing.Add(dimension);
            }
        }

        return missing;
    }

    /// <summary>
    /// 流证据与流读数必须自洽（ENV-T04 参考流边界包）。
    ///
    /// <para>
    /// 两层，都只针对**具名流**（两侧同一生成器、同一单位）：
    /// (1) 记录声明"脚本期间消费了随机数"（<c>rng_consumed_by_script</c>），却没有任何一个边界留下具名流
    /// 读数——这就是"缺维度"而不是"零消费"（动态/临时 RNG 槽位不是该声明的证据）；
    /// (2) 脚本期间**真的增长了**的具名流（<c>script_random</c> before/after 逐流求差，含只在 after 出现的新流）
    /// 必须在逐边界读数里出现。初始化阶段消费的具名流不算脚本消费：逐边界读数是"该边界区间内有推进"的视图，
    /// 初始化消费由 <c>initialization_random_count</c> 与参考记录的 <c>reference_streams.initialization</c> 承担。
    /// </para>
    /// <para>
    /// 两条都不放宽：宁可如实报缺证据，也不拿累计末态或补 0 填平。
    /// </para>
    /// </summary>
    private static string? StreamSettlementProblem(CaseRecord record)
    {
        if (record.Status is not ("ok" or "terminal"))
        {
            return null;
        }

        if (record.RngConsumedByScript && record.Boundaries.Count > 0
            && record.Boundaries.All(boundary => !boundary.Streams.Any(Records.IsNamedStream)))
        {
            return $"声明脚本期间消费了随机数，但全部 {record.Boundaries.Count} 个边界都没有**具名流**读数"
                + "（动态/临时 RNG 槽位不是该声明的证据；这正是缺维度，不是零消费）";
        }

        var consumedByScript = ScriptStreamDeltas(record);
        if (consumedByScript.Count == 0)
        {
            return null;
        }

        var covered = record.Boundaries
            .SelectMany(boundary => boundary.Streams)
            .Where(Records.IsNamedStream)
            .Select(stream => stream.Stream)
            .ToHashSet(StringComparer.Ordinal);
        var uncovered = consumedByScript.Where(stream => !covered.Contains(stream))
            .Order(StringComparer.Ordinal).ToList();
        return uncovered.Count == 0
            ? null
            : $"脚本期间实际增长的具名流（{string.Join("+", consumedByScript.Order(StringComparer.Ordinal))}）"
                + $"在逐边界读数里覆盖不到：{string.Join(" / ", uncovered)}";
    }

    /// <summary>脚本期间**逐流增量**（&gt; 0 且是具名流）的流名：初始化消费不算。</summary>
    private static List<string> ScriptStreamDeltas(CaseRecord record)
    {
        if (record.ScriptRandom is not { } measurement)
        {
            return [];
        }

        var before = measurement.BeforeStreams
            .GroupBy(stream => stream.Stream, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Max(stream => stream.Draws), StringComparer.Ordinal);
        return
        [
            .. measurement.AfterStreams
                .GroupBy(stream => stream.Stream, StringComparer.Ordinal)
                .Where(group => Records.IsNamedStream(group.First())
                    && group.Max(stream => stream.Draws) - before.GetValueOrDefault(group.Key, 0) > 0)
                .Select(group => group.Key),
        ];
    }

    /// <summary>预期终点：严格入口按 base 侧实际会话状态核对（错误终点直接非零）。</summary>
    private static string? TerminalProblem(CaseRecord record, string expected)
    {
        var status = record.SessionStatus ?? string.Empty;
        var terminal = record.Terminal;
        switch (expected)
        {
            case ExpectedTerminals.Running:
                return status is "Running" or "AwaitingDecision"
                    ? null
                    : $"期望会话停在可继续边界，实际 session_status={status}，terminal={terminal ?? "(无)"}";
            case ExpectedTerminals.Victory:
                return status == "Terminal" && !string.IsNullOrWhiteSpace(terminal)
                    && !terminal.Contains("Defeat", StringComparison.Ordinal)
                    ? null
                    : $"期望胜利终局，实际 session_status={status}，terminal={terminal ?? "(无)"}";
            case ExpectedTerminals.Defeat:
                return status == "Terminal" && terminal is not null
                    && terminal.Contains("Defeat", StringComparison.Ordinal)
                    ? null
                    : $"期望失败终局，实际 session_status={status}，terminal={terminal ?? "(无)"}";
            default:
                return $"未知 expected_terminal '{expected}'";
        }
    }

    // -- 超时/异常兜底 ---------------------------------------------------------------------------------------

    /// <summary>
    /// 带超时保护地跑一个 case（ENV 约束 §13：每 case 120 秒保护）。超时后**不等待**那个后台任务
    /// （引擎当前没有可中断的取消接口），进程结束时它随进程退出；这一点在报告里明写，不假装取消了。
    /// </summary>
    private static (T? Value, bool TimedOut, Exception? Failure) Guarded<T>(Func<T> action, int timeoutMs)
        where T : class
    {
        var task = Task.Run(action);
        try
        {
            if (!task.Wait(timeoutMs))
            {
                return (null, true, null);
            }
        }
        catch (AggregateException aggregate)
        {
            return (null, false, aggregate.InnerException ?? aggregate);
        }

        return (task.Result, false, null);
    }

    private static CaseRecord TimeoutRecord(FixtureCase fixture, string backend, string side, int timeoutMs) =>
        new(fixture.Id, backend, Version, fixture.RngProfile, "timeout",
            $"{side} 侧在 {timeoutMs} ms 内没有返回：按超时失败处理，不伪造终局、不跳过断言", false,
            ["超时的 case 不计通过；保护预算按 ENV 约束 §13。"], [])
        {
            BaseSource = fixture.BaseSource,
        };

    private static CaseRecord FaultRecord(FixtureCase fixture, string backend, Exception failure) =>
        new(fixture.Id, backend, Version, fixture.RngProfile, "faulted",
            $"{failure.GetType().Name}: {failure.Message}", false,
            ["执行器抛出未预期异常：按异常失败处理。"], [])
        {
            BaseSource = fixture.BaseSource,
        };

    // -- 报告 -----------------------------------------------------------------------------------------------

    internal sealed record TestCaseResult
    {
        public required string CaseId { get; init; }
        public required string Title { get; init; }
        public required string Group { get; init; }
        public required string Kind { get; init; }
        public required string BaseSource { get; init; }
        public required string RngProfile { get; init; }
        public required string ReferenceScope { get; init; }
        public string? ReferenceSource { get; init; }
        public string? Initialization { get; init; }
        public string? GameSeed { get; init; }
        public required IReadOnlyList<string> Contents { get; init; }
        public required IReadOnlyList<string> Variants { get; init; }
        public required IReadOnlyList<string> SemanticActions { get; init; }
        public required IReadOnlyList<string> RequiredDimensions { get; init; }
        public required IReadOnlyList<string> MissingDimensions { get; init; }
        public required string ExpectedTerminal { get; init; }
        public string? TerminalProblem { get; init; }
        public required string Verdict { get; init; }
        public string? Detail { get; init; }
        public string? BaseStatus { get; init; }
        public string? BaseSessionStatus { get; init; }
        public string? BaseTerminal { get; init; }
        public string? BuildId { get; init; }
        public string? SessionIdentity { get; init; }
        public bool BaseRngConsumed { get; init; }
        public int BaseBoundaries { get; init; }
        public string? ReferenceBackend { get; init; }
        public string? ReferenceStatus { get; init; }
        public int ReferenceBoundaries { get; init; }
        public required IReadOnlyList<Divergence> Divergences { get; init; }
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string Markdown(FixtureSuite suite, IReadOnlyList<TestCaseResult> results,
        IReadOnlyList<ComparatorSelfTest.Item> selfTest, string command, string runId, int exitCode, bool complete,
        bool partial, bool referenceUnavailable)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# ENV-T04 严格 regression（自动生成）");
        builder.AppendLine();
        builder.AppendLine($"- 产物版本：`{Version}`");
        builder.AppendLine($"- suite：`{suite.Suite}`（`{suite.FixturesVersion}`），目标范围 `{suite.TargetScope}`，"
            + $"期望 case {suite.ExpectedCaseIds!.Count} 个");
        builder.AppendLine($"- run_id：`{runId}`");
        builder.AppendLine($"- 复现命令：`{command}`");
        builder.AppendLine($"- 完整 suite：{complete}（{(partial ? "子集运行" : "全量运行")}）；"
            + $"参考适配器：{(referenceUnavailable ? "**不可用**" : ReferenceBackends.Available ? "已接入" : "不可用")}；"
            + $"退出码：**{exitCode}**");
        builder.AppendLine();
        builder.AppendLine("## 逐 case 判定（只有 passed / self_checked 计成功，且都过了必需维度与预期终点门槛）");
        builder.AppendLine();
        builder.AppendLine("| case | 判定 | 内容/机制 | 必需维度 | 终点 | base status | 边界 | 首差/原因 |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | ---: | --- |");
        foreach (var item in results)
        {
            var first = item.Divergences.FirstOrDefault();
            var reason = first is null
                ? (item.Detail ?? string.Empty)
                : $"`{first.Path}`：{first.Left} ≠ {first.Right}（最后一致边界 {first.LastConsistentBoundary}）";
            builder.AppendLine($"| {item.CaseId} | {(item.Verdict is "passed" or "self_checked" ? item.Verdict : $"**{item.Verdict}**")} | "
                + $"{string.Join(", ", item.Contents)} | {string.Join(", ", item.RequiredDimensions)}"
                + (item.MissingDimensions.Count > 0 ? $"（缺 {string.Join(", ", item.MissingDimensions)}）" : string.Empty)
                + $" | {item.ExpectedTerminal} ({(item.TerminalProblem is null ? "ok" : "**不符**")}) | "
                + $"{item.BaseStatus} | {item.BaseBoundaries}/{item.ReferenceBoundaries} | "
                + $"{reason.Replace("|", "\\|")} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 比较器自检（受控错误记录必须被抓到）");
        builder.AppendLine();
        builder.AppendLine("| 自检 | 期望差种类 | 是否抓到 | 说明 |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var item in selfTest)
        {
            builder.AppendLine($"| {item.Name} | {item.ExpectedKind} | {(item.Detected ? "是" : "**否**")} | "
                + $"{(item.Detail ?? string.Empty).Replace("|", "\\|")} |");
        }

        return builder.ToString();
    }
}
