using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Differential;

/// <summary>
/// ENV-00 的 `compare`：跑夹具、逐边界比较、报首差，并用**受控错误记录**自证比较器真的抓得到差异。
///
/// 比较的是"一次外部动作结算完"这个共同逻辑边界上的**规范化事实**（牌区顺序、槽位字段、能力层数）
/// 与**逻辑决策点**（候选集合/数量约束/最终选择）；各后端私有的完整状态只入记录、不参与比较（ENV §6.2）。
/// </summary>
internal static class CompareCommand
{
    private const string Version = "env-00-compare@1";

    public static int Run(string repositoryRoot, string suiteName, string outputDirectory, string? referenceRecords)
    {
        var suitePath = FixtureSuites.ResolvePath(repositoryRoot, suiteName);
        FixtureSuite suite;
        try
        {
            suite = FixtureSuites.Load(suitePath);
        }
        catch (Exception failure)
        {
            Console.Error.WriteLine($"无法载入夹具套件：{failure.Message}");
            return 2;
        }

        var problems = FixtureSuites.Validate(suite);
        if (problems.Count > 0)
        {
            Console.Error.WriteLine("夹具套件自检失败：");
            foreach (var problem in problems)
            {
                Console.Error.WriteLine("  - " + problem);
            }

            return 1;
        }

        var outputPath = Path.IsPathRooted(outputDirectory)
            ? outputDirectory
            : Path.Combine(repositoryRoot, outputDirectory.Replace('/', Path.DirectorySeparatorChar));
        var baseRecordsDirectory = Path.Combine(outputPath, "records", "base");
        var referenceRecordsDirectory = Path.Combine(outputPath, "records", "reference");
        Directory.CreateDirectory(baseRecordsDirectory);

        var reproduction = $"dotnet run --project tools/differential -- compare --suite {suiteName}"
            + $" --output {outputDirectory}"
            + (referenceRecords is null ? string.Empty : $" --records {referenceRecords}");

        // -- 1. 跑 base 侧，落盘记录 -------------------------------------------------------------------------
        var baseRecords = new List<CaseRecord>();
        foreach (var fixture in suite.Cases)
        {
            var record = BaseBackend.Run(fixture);
            baseRecords.Add(record);
            File.WriteAllText(Path.Combine(baseRecordsDirectory, fixture.Id + ".json"),
                Records.ToJson(record), new UTF8Encoding(false));
        }

        // -- 2. 比较器自检（受控错误记录必须被抓到） ----------------------------------------------------------
        var selfTest = ComparatorSelfTest.Run(baseRecords, reproduction, suite.Cases);
        var selfTestFailures = selfTest.Count(item => !item.Detected);

        // -- 3. 与参考侧比较 --------------------------------------------------------------------------------
        string? externalRecords = null;
        if (referenceRecords is not null)
        {
            externalRecords = Path.IsPathRooted(referenceRecords)
                ? referenceRecords
                : Path.Combine(repositoryRoot, referenceRecords.Replace('/', Path.DirectorySeparatorChar));
        }

        var comparisons = new List<ComparisonResult>();
        var referenceRan = 0;
        foreach (var (fixture, record) in suite.Cases.Zip(baseRecords))
        {
            if (fixture.ReferenceScope != "compared")
            {
                comparisons.Add(new ComparisonResult(record.CaseId, fixture.ReferenceScope,
                    fixture.ReferenceScopeReason, []));
                continue;
            }

            CaseRecord? reference = null;
            if (externalRecords is not null)
            {
                var path = Path.Combine(externalRecords, record.CaseId + ".json");
                if (!File.Exists(path))
                {
                    comparisons.Add(new ComparisonResult(record.CaseId, "missing_reference_record",
                        $"缺少参考记录 {path}", []));
                    continue;
                }

                reference = Records.FromJson(File.ReadAllText(path));
            }
            else if (ReferenceBackends.Available)
            {
                try
                {
                    reference = fixture.Kind == CaseKinds.NormalReward
                        ? NormalRewardEntry.RunReference(fixture, record) : ReferenceBackends.Run(fixture);
                    referenceRan++;
                    Directory.CreateDirectory(referenceRecordsDirectory);
                    File.WriteAllText(Path.Combine(referenceRecordsDirectory, record.CaseId + ".json"),
                        Records.ToJson(reference), new UTF8Encoding(false));
                }
                catch (Exception failure)
                {
                    comparisons.Add(new ComparisonResult(record.CaseId, "reference_fault",
                        $"{failure.GetType().Name}: {failure.Message}", []));
                    continue;
                }
            }
            else
            {
                comparisons.Add(new ComparisonResult(record.CaseId, "unsupported_reference_adapter",
                    "本机构建未包含参考侧适配器（未找到 reference/sts2-sim）：不计通过（ENV-00 未完成项）", []));
                continue;
            }

            comparisons.Add(Comparator.Compare(record, reference, reproduction));
        }

        // -- 4. 落盘 ----------------------------------------------------------------------------------------
        var verdicts = comparisons.GroupBy(item => item.Verdict, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var report = new JsonObject
        {
            ["compare_version"] = Version,
            ["suite"] = suite.Suite,
            ["fixtures_version"] = suite.FixturesVersion,
            ["suite_path"] = Path.GetRelativePath(repositoryRoot, suitePath).Replace('\\', '/'),
            ["command"] = reproduction,
            ["base_records"] = Relative(repositoryRoot, baseRecordsDirectory),
            ["reference_records"] = referenceRan > 0 ? Relative(repositoryRoot, referenceRecordsDirectory)
                : externalRecords,
            ["reference_adapter_available"] = ReferenceBackends.Available,
            ["self_test"] = new JsonArray(selfTest.Select(item => (JsonNode)new JsonObject
            {
                ["case"] = item.Name,
                ["expected_kind"] = item.ExpectedKind,
                ["detected"] = item.Detected,
                ["detail"] = item.Detail,
            }).ToArray()),
            ["counts"] = new JsonObject(verdicts.Select(pair =>
                new KeyValuePair<string, JsonNode?>(pair.Key, JsonValue.Create(pair.Value)))),
            ["cases"] = new JsonArray(comparisons.Select(item => (JsonNode)new JsonObject
            {
                ["case_id"] = item.CaseId,
                ["verdict"] = item.Verdict,
                ["detail"] = item.Detail,
                ["divergences"] = new JsonArray(item.Divergences.Select(divergence => (JsonNode)new JsonObject
                {
                    ["boundary"] = divergence.BoundaryIndex,
                    ["boundary_kind"] = divergence.BoundaryKind,
                    ["kind"] = divergence.Kind,
                    ["path"] = divergence.Path,
                    ["left"] = divergence.Left,
                    ["right"] = divergence.Right,
                    ["last_consistent_boundary"] = divergence.LastConsistentBoundary,
                    ["reproduction"] = divergence.Reproduction,
                }).ToArray()),
            }).ToArray()),
            ["rng_profiles"] = new JsonArray(baseRecords
                .GroupBy(item => item.RngProfile, StringComparer.Ordinal)
                .Select(group => (JsonNode)new JsonObject
                {
                    ["profile"] = group.Key,
                    ["cases"] = group.Count(),
                    ["script_consumed_randomness"] = group.Count(item => item.RngConsumedByScript),
                    ["counted_as_passable"] = group.Key is RngProfiles.VanillaCompatible
                        or RngProfiles.ControlledNoRandom,
                }).ToArray()),
            ["notes"] = new JsonArray(
                JsonValue.Create("只有 passed 计通过；unsupported_*/missing_*/reference_fault 都不计通过。"),
                JsonValue.Create("事件/hook 顺序轨迹尚未参与比较：引擎没有可读轨迹（ENV §6.2 缺口）。"),
                JsonValue.Create("受控无随机 case 的'无随机'是**实测**的：脚本执行期间随机调用总数变化即降级为 unsupported_rng_profile。")),
        };

        var jsonPath = Path.Combine(outputPath, "comparison.json");
        File.WriteAllText(jsonPath, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        var markdownPath = Path.Combine(outputPath, "comparison.md");
        File.WriteAllText(markdownPath, Markdown(suite, baseRecords, comparisons, selfTest, reproduction),
            new UTF8Encoding(false));

        // -- 5. 控制台摘要与退出码 ---------------------------------------------------------------------------
        Console.WriteLine($"compare written: {jsonPath}");
        Console.WriteLine($"base 记录：{baseRecords.Count} 个 case → {Relative(repositoryRoot, baseRecordsDirectory)}"
            + (referenceRan > 0 ? $"；参考记录 {referenceRan} 个 → {Relative(repositoryRoot, referenceRecordsDirectory)}" : string.Empty));
        Console.WriteLine($"参考侧适配器：{(ReferenceBackends.Available ? "已接入" : "不可用（未找到 reference/sts2-sim）")}");
        foreach (var item in selfTest)
        {
            Console.WriteLine($"  自检 {(item.Detected ? "OK  " : "FAIL")} {item.Name}（期望 {item.ExpectedKind}）"
                + (item.Detail is null ? string.Empty : $" — {item.Detail}"));
        }

        foreach (var group in comparisons.GroupBy(item => item.Verdict, StringComparer.Ordinal)
                     .OrderByDescending(group => group.Count()))
        {
            Console.WriteLine($"  判定 {group.Key,-34} {group.Count()}");
        }

        foreach (var record in baseRecords.Where(item => item.Status != "ok" && item.Status != "terminal"))
        {
            Console.WriteLine($"  [{record.Status}] {record.CaseId}: {record.Detail}");
        }

        var diverged = comparisons.Count(item => item.Verdict == "diverged");
        Console.WriteLine($"自检失败 {selfTestFailures}；首差 {diverged}；"
            + $"通过 {comparisons.Count(item => item.Verdict == "passed")}；"
            + $"不计通过 {comparisons.Count(item => item.Verdict != "passed")}");
        return selfTestFailures == 0 && diverged == 0 ? 0 : 1;
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    // -- Markdown 摘要 -------------------------------------------------------------------------------------

    private static string Markdown(FixtureSuite suite, IReadOnlyList<CaseRecord> baseRecords,
        IReadOnlyList<ComparisonResult> comparisons, IReadOnlyList<ComparatorSelfTest.Item> selfTest,
        string reproduction)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# ENV-00 compare（自动生成）");
        builder.AppendLine();
        builder.AppendLine($"- 产物版本：`{Version}`");
        builder.AppendLine($"- 夹具套件：`{suite.Suite}`（`{suite.FixturesVersion}`），case {suite.Cases.Count} 个");
        builder.AppendLine($"- 参考侧适配器：{(ReferenceBackends.Available ? "已接入" : "**不可用**（未找到 reference/sts2-sim）")}");
        builder.AppendLine($"- 复现命令：`{reproduction}`");
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

        builder.AppendLine();
        builder.AppendLine("## base 侧记录（真实跑出）");
        builder.AppendLine();
        builder.AppendLine("| case | 状态 | 边界数 | 脚本消费随机 | RNG Profile | 标题 |");
        builder.AppendLine("| --- | --- | ---: | --- | --- | --- |");
        foreach (var record in baseRecords)
        {
            var fixture = suite.Cases.First(item => item.Id == record.CaseId);
            builder.AppendLine($"| {record.CaseId} | {record.Status} | {record.Boundaries.Count} | "
                + $"{(record.RngConsumedByScript ? "是" : "否")} | {record.RngProfile} | {fixture.Title} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 判定（只有 passed 计通过）");
        builder.AppendLine();
        builder.AppendLine("| case | 判定 | 首差 |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var result in comparisons)
        {
            var first = result.Divergences.FirstOrDefault();
            builder.AppendLine($"| {result.CaseId} | {result.Verdict} | "
                + (first is null
                    ? (result.Detail ?? string.Empty).Replace("|", "\\|")
                    : $"`{first.Path}`：{first.Left} ≠ {first.Right}".Replace("|", "\\|"))
                + " |");
        }

        return builder.ToString();
    }
}
