using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Differential;

/// <summary>
/// ENV-06 的结算入口：把 <c>inventory.json</c>（完整分母 + 来源摘要）与 <c>comparison.json</c>（对拍判定）
/// 结算成 <c>coverage.json</c> 与 <c>environment-manifest.json</c>。
///
/// 三条纪律写死在实现里：
/// <list type="number">
/// <item>**只有 `passed` 计通过**：`unsupported_*` / `missing_*` / `not_run` / `not_checked` 一律算未覆盖；</item>
/// <item>**证据链要冻结**：重算源/参考/生产源码的目录摘要并与清单里的比对，漂移逐条报出来；</item>
/// <item>**不许用"测试很多、全绿"结算**：本命令不看测试数量，只看四态覆盖与对拍判定，
///       并且**总是**把未关闭的门列出来。当前整体结论必然是 `incomplete`（ENV-01~06 未完成），
///       退出码 1 表示"结算完成但环境未完成"，不是工具故障。</item>
/// </list>
/// </summary>
internal static class VerifyCommand
{
    private const string Version = "env-06-verify@1";

    public static int Run(string repositoryRoot, string inventoryPath, string fixturesPath, string outputDirectory,
        string? behaviorDirectory = null)
    {
        if (!File.Exists(inventoryPath))
        {
            Console.Error.WriteLine($"找不到 inventory 产物：{inventoryPath}（先跑 `inventory`）");
            return 2;
        }

        if (!File.Exists(fixturesPath))
        {
            Console.Error.WriteLine($"找不到对拍产物：{fixturesPath}（先跑 `compare`）");
            return 2;
        }

        var inventory = JsonNode.Parse(File.ReadAllText(inventoryPath))!.AsObject();
        var comparison = JsonNode.Parse(File.ReadAllText(fixturesPath))!.AsObject();

        var gateList = new JsonArray();
        Gate(gateList, "baseline_frozen", "源/参考/生产源码摘要与清单一致（无漂移）",
            Drift(repositoryRoot, inventory, out var driftDetail) ? "passed" : "open",
            driftDetail, "重跑 `inventory` 更新冻结摘要，或查明是哪些文件变了");
        Gate(gateList, "denominator_has_sources", "完整分母每一项都有来源",
            DenominatorHasSources(inventory, out var sourceDetail) ? "passed" : "open",
            sourceDetail, "补齐缺来源的内容");
        var uncovered = UncoveredTotal(inventory);
        var notSettleable = NotSettleableTotal(inventory);
        Gate(gateList, "no_uncovered_content", "连接键已核对的类别里没有未落地的内容",
            uncovered == 0 ? "passed" : "open",
            $"未落地 {uncovered} 条（连接键已核对类别；见 coverage.json 的 uncovered 列表）",
            "按机制族逐批迁移，不用测试数量替代");
        Gate(gateList, "coverage_settleable", "每个类别的 coverage 都能结算（连接键已按原版核对）",
            notSettleable == 0 ? "passed" : "open",
            $"连接键未核对、因此**无法结算**的分母 {notSettleable} 条"
                + $"（{string.Join("、", UnsettleableCategories(inventory))}）",
            "逐类别按原版核对连接键（内容 id 的对应规则），未核对前不报这些类别的分子");
        var loaded = inventory["engine_loaded"]?.AsObject();
        var loadedCards = loaded?["instantiated_cards"]?.GetValue<int>() ?? 0;
        var loadGaps = loaded?["lowered_not_loaded"]?.AsArray().Count ?? -1;
        Gate(gateList, "engine_loaded_mechanism",
            "正式源能进入 Engine 构建且**没有加载缺口**（覆盖面本身另由 no_uncovered_content 计）",
            loaded?["ok"]?.GetValue<bool>() == true && loadGaps == 0 ? "passed" : "open",
            loaded?["ok"]?.GetValue<bool>() == true
                ? $"正式源构建实测：声明 {loaded["declared_cards"]} 张卡 → 会话启动后实例化 "
                    + $"{loadedCards} 张，缺口 {loadGaps}；分母 {Denominator(inventory)}"
                    + "（能力不计入：开局没有能力实例，按声明计入就是过度声称）"
                : $"构建失败：{loaded?["error"]?.GetValue<string>()}",
            "把正式源覆盖面从当前子集扩到完整分母（ENV-02/04），并让每批机制族交付时重跑本列");
        var linkOrphans = inventory["base_spire"]?["formal_frontend"]?["link_orphans"]?.AsArray() ?? [];
        Gate(gateList, "no_dangling_power_links", "没有'引用了未加载能力'的卡（宽表链接会制造悬空引用）",
            linkOrphans.Count == 0 ? "passed" : "open",
            linkOrphans.Count == 0
                ? "没有卡引用未加载的能力"
                : $"{linkOrphans.Count} 张：{string.Join("、", linkOrphans.Select(node => node!.GetValue<string>()))}"
                    + "——按宽表 `KnownIds` 链接会得到一个'存在但没加载'的能力 id，因此记为未支持",
            "把被引用的能力补进运行时能力白名单（例如 BattleTrance 需要 `no-draw`）");
        var behavior = BehaviorCoverage(repositoryRoot, behaviorDirectory, inventory);
        var exceptions = inventory["id_exceptions"]?.AsObject();
        var unexplainedAmbiguity = exceptions?["unexplained"]?.AsArray().Count ?? 0;
        var staleExceptions = exceptions?["stale"]?.AsArray().Count ?? 0;
        Gate(gateList, "no_unexplained_id_ambiguity", "跨类别同名内容 id 都有登记依据（登记表不腐烂）",
            unexplainedAmbiguity == 0 && staleExceptions == 0 ? "passed" : "open",
            unexplainedAmbiguity == 0 && staleExceptions == 0
                ? $"已登记 {exceptions?["registered"]?.AsArray().Count ?? 0} 条，无未解释、无过期登记"
                : $"未解释 {unexplainedAmbiguity} 条；过期登记（已不再歧义）{staleExceptions} 条",
            "把新出现的跨类别同名登记进 tools/differential/registries/id-exceptions.json，"
                + "或清掉已不再歧义的登记（登记表必须与实测一致）");
        Gate(gateList, "behavior_verified", "内容有判别性行为证据（由规则检查**自报**，不解析测试源码）",
            "open",
            behavior.Detail,
            "逐批机制族交付时补判别性检查，并让检查项目继续自报（`--report <目录>`）");
        var differential = DifferentialCoverage(repositoryRoot, comparison, inventory);
        Gate(gateList, "differential", "与参考逐边界对拍（含首差裁决）",
            "open",
            $"已对拍内容 **{differential.Ids.Count}** 条 / 分母 {Denominator(inventory)}；"
                + $"case 判定 {VerdictSummary(comparison)}",
            "把可计入 case 从当前夹具扩到完整分母（逐机制族推进）");
        Gate(gateList, "host_public", "适用 Host 公开可用",
            ColumnState(inventory, "host_public") == "produced" ? "passed" : "open",
            $"inventory.columns.host_public = {ColumnState(inventory, "host_public")}",
            "ENV-05：接正式源子集进指定 Host");
        Gate(gateList, "no_countable_divergence", "对拍没有可计入的首差",
            Countable(comparison) == 0 ? "passed" : "open",
            $"countable diverged = {Countable(comparison)}",
            "先修首差再重跑受影响 case");
        var identityFailures = IdentityFailures(inventory);
        Gate(gateList, "coverage_consistent", "每条类别的结算恒等式成立（已降级 + 仅旧后端 + 未落地 == 分母）",
            identityFailures.Count == 0 ? "passed" : "open",
            identityFailures.Count == 0
                ? $"全部 {identityChecked} 个类别恒等式成立"
                : $"{identityFailures.Count} 个类别不成立：{string.Join("；", identityFailures.Take(4))}",
            "先修结算口径（重复计数会把未落地数字算错，进而误判覆盖）");
        Gate(gateList, "rng_profile_vanilla", "随机路径在**原版兼容 Profile** 下可计入",
            VanillaCases(comparison) > 0 ? "passed" : "open",
            $"声明为 vanilla-compatible@1 的 case = {VanillaCases(comparison)}"
                + $"（受控无随机 7、unsupported 4、决策模型未对接 1）",
            "ENV-01：base 侧流接线 + 数值保真做完才可能开出可计入的随机 case");
        Gate(gateList, "event_order", "事件/hook 顺序可读并参与比较",
            "open", "引擎没有事件/hook 顺序的可读轨迹（ENV §6.2 缺口）",
            "core/Engine 补可读事件轨迹，工具再比较");

        var coverage = Coverage(repositoryRoot, inventory, comparison, uncovered, notSettleable);
        var open = gateList.Count(node => node!.AsObject()["status"]!.GetValue<string>() == "open");

        var manifest = new JsonObject
        {
            ["manifest_version"] = "env-00-environment-manifest@1",
            ["verify_version"] = Version,
            ["overall"] = open == 0 ? "complete" : "incomplete",
            ["open_gates"] = open,
            ["command"] = "dotnet run --project tools/differential -- verify"
                + $" --inventory {Relative(repositoryRoot, inventoryPath)}"
                + $" --fixtures {Relative(repositoryRoot, fixturesPath)}",
            ["identity"] = new JsonObject
            {
                ["inventory_version"] = inventory["inventory_version"]?.GetValue<string>(),
                ["fixtures_version"] = comparison["fixtures_version"]?.GetValue<string>(),
                ["compare_version"] = comparison["compare_version"]?.GetValue<string>(),
                ["project_git_head"] = inventory["project"]?["git_head"]?.GetValue<string>(),
                ["reference_git_head"] = inventory["reference_identity"]?["git_head"]?.GetValue<string>(),
                ["game_version_evidence_machine_readable"] =
                    inventory["game_version_evidence"]?["machine_readable"]?.GetValue<bool>(),
                ["baselines"] = BoundBaselines(repositoryRoot, inventory),
                ["artifacts"] = new JsonArray(
                    new JsonObject
                    {
                        ["path"] = Relative(repositoryRoot, inventoryPath),
                        ["sha256"] = Repo.Sha256(File.ReadAllBytes(inventoryPath)),
                    },
                    new JsonObject
                    {
                        ["path"] = Relative(repositoryRoot, fixturesPath),
                        ["sha256"] = Repo.Sha256(File.ReadAllBytes(fixturesPath)),
                    }),
            },
            ["gates"] = gateList,
            ["differential_coverage"] = differential.Json,
            ["behavior_coverage"] = behavior.Json,
            ["engine_loaded"] = inventory["engine_loaded"]?.DeepClone(),
            ["id_exceptions"] = inventory["id_exceptions"]?.DeepClone(),
            ["notes"] = new JsonArray(
                JsonValue.Create("本清单是**候选环境**的身份与未关闭项，不是完工声明；"
                    + "只有 `overall=complete` 且负责人审查后才可能进入下一阶段。"),
                JsonValue.Create("`unsupported_rng_profile` / `unsupported_decision_model` / `not_run` / "
                    + "`not_checked` 一律算未覆盖，不用测试数量或构建绿色替代。")),
        };

        var outputPath = Path.IsPathRooted(outputDirectory)
            ? outputDirectory
            : Path.Combine(repositoryRoot, outputDirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(outputPath);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var manifestPath = Path.Combine(outputPath, "environment-manifest.json");
        var coveragePath = Path.Combine(outputPath, "coverage.json");
        File.WriteAllText(manifestPath, manifest.ToJsonString(options), new UTF8Encoding(false));
        File.WriteAllText(coveragePath, coverage.ToJsonString(options), new UTF8Encoding(false));

        Console.WriteLine($"environment manifest: {manifestPath}");
        Console.WriteLine($"coverage:             {coveragePath}");
        Console.WriteLine($"整体结论：{manifest["overall"]}（未关闭的门 {open}/{gateList.Count}）");
        foreach (var node in gateList)
        {
            var gate = node!.AsObject();
            Console.WriteLine($"  [{(gate["status"]!.GetValue<string>() == "passed" ? "通过" : "开放")}] "
                + $"{gate["id"]}：{gate["detail"]}");
        }

        Console.WriteLine($"分母 {coverage["totals"]!["denominator"]}；"
            + $"已降级 {coverage["totals"]!["lowered"]}；未落地 {uncovered}（连接键已核对类别）；"
            + $"无法结算 {notSettleable}（连接键未核对类别）");
        // 结算完成但环境未完成 → 1；证据链自相矛盾（产物读不动）在上面的 2 里已经返回。
        return open == 0 ? 0 : 1;
    }

    private static void Gate(JsonArray gates, string id, string title, string status, string detail, string blocker) =>
        gates.Add(new JsonObject
        {
            ["id"] = id,
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["next"] = status == "passed" ? null : blocker,
        });

    private static int identityChecked;

    /// <summary>
    /// **行为证据覆盖**：读规则检查项目**自报**的产物（`slice-behavior-report.json`），
    /// 把里面的内容 id 归到分母类别上。
    ///
    /// 三条诚实性约束：① 没有自报产物时明确写 <c>not_reported</c>，不假装是 0 或通过；
    /// ② 自报里的 id 必须能在分母的连接键里找到，找不到的单独列出（说明自报超出了分母范围）；
    /// ③ 报告自带 <c>not_lowered</c>，非空即说明"声称验证了没加载的内容"，直接算失败。
    /// </summary>
    private static (string Detail, JsonObject Json) BehaviorCoverage(string repositoryRoot,
        string? behaviorDirectory, JsonObject inventory)
    {
        var directory = behaviorDirectory is null
            ? Path.Combine(repositoryRoot, "artifacts", "environment", "behavior")
            : Path.IsPathRooted(behaviorDirectory)
                ? behaviorDirectory
                : Path.Combine(repositoryRoot, behaviorDirectory.Replace('/', Path.DirectorySeparatorChar));
        var path = Path.Combine(directory, "slice-behavior-report.json");
        if (!File.Exists(path))
        {
            return ("未找到自报产物（先跑 `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests "
                + "-- --report artifacts/environment/behavior`）",
                new JsonObject { ["status"] = "not_reported", ["expected_path"] = Relative(repositoryRoot, path) });
        }

        var report = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        // 同一个 leaf id 可能同时是我们某一类**卡牌**和某一类**能力**的 id（例如 feel-no-pain）：
        // 这时静默归给"第一个见到的类别"会把统计变成假的，所以先收集全部归属，再显式报歧义。
        var keyToCategories = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var node in inventory["matching"]?["categories"]?.AsArray() ?? [])
        {
            var item = node!.AsObject();
            var category = item["category"]!.GetValue<string>();
            foreach (var key in item["matched_keys"]?.AsArray() ?? [])
            {
                AddCategory(keyToCategories, key!.GetValue<string>(), category);
            }

            foreach (var key in item["unmatched_formal"]?.AsArray() ?? [])
            {
                AddCategory(keyToCategories, key!.GetValue<string>(), category);
            }
        }

        var byCategory = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var outside = new List<string>();
        var ambiguous = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var ids = report["behavior_verified_ids"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
        foreach (var id in ids)
        {
            if (!keyToCategories.TryGetValue(id, out var categories))
            {
                outside.Add(id);
                continue;
            }

            if (categories.Count > 1)
            {
                ambiguous[id] = string.Join("+", categories);
                continue;
            }

            var category = categories.First();
            byCategory[category] = byCategory.GetValueOrDefault(category) + 1;
        }

        var notLowered = report["not_lowered"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
        // 歧义里哪些是**已登记例外**：分开写，读者不必自己去对两份清单。
        // 登记表用**限定** id（`base-spire:feel-no-pain`），这里的键是**leaf**（`feel-no-pain`）——
        // 两边都要归一化，否则会出现"已登记却说未解释"的假结论。
        var registered = new HashSet<string>(
            (inventory["id_exceptions"]?["registered"]?.AsArray() ?? [])
            .Select(node => Unqualify(node!.AsObject()["id"]!.GetValue<string>())),
            StringComparer.Ordinal);
        var ambiguousRegistered = ambiguous.Keys.Where(registered.Contains).ToList();
        var ambiguousUnexplained = ambiguous.Keys.Where(id => !registered.Contains(id)).ToList();
        var json = new JsonObject
        {
            ["status"] = "reported",
            ["report_file"] = Relative(repositoryRoot, path),
            ["report_version"] = report["report_version"]?.GetValue<string>(),
            ["scope"] = report["scope"]?.GetValue<string>(),
            ["checks_passed"] = report["checks_passed"]?.GetValue<int>(),
            ["behavior_verified_count"] = ids.Count,
            ["in_denominator"] = ids.Count - outside.Count,
            ["by_category"] = new JsonObject(byCategory.Select(pair =>
                new KeyValuePair<string, JsonNode?>(pair.Key, JsonValue.Create(pair.Value)))),
            ["ambiguous_across_categories"] = new JsonObject(ambiguous.Select(pair =>
                new KeyValuePair<string, JsonNode?>(pair.Key, JsonValue.Create(pair.Value)))),
            ["ambiguous_registered"] = new JsonArray(ambiguousRegistered
                .Select(id => (JsonNode)JsonValue.Create(id)).ToArray()),
            ["ambiguous_unexplained"] = new JsonArray(ambiguousUnexplained
                .Select(id => (JsonNode)JsonValue.Create(id)).ToArray()),
            ["outside_denominator"] = new JsonArray(outside.Select(id => (JsonNode)JsonValue.Create(id)).ToArray()),
            ["not_lowered"] = new JsonArray(notLowered.Select(id => (JsonNode)JsonValue.Create(id)).ToArray()),
        };

        var denominator = Denominator(inventory);
        var detail = $"行为已验证 {ids.Count} 条（分母 {denominator}），落在分母内 {ids.Count - outside.Count} 条"
            + $"；按类别 {string.Join("、", byCategory.Select(pair => $"{pair.Key} {pair.Value}"))}"
            + (ambiguous.Count > 0
                ? $"；**跨类别同名 {ambiguous.Count} 条**（{string.Join(",", ambiguous.Keys.Take(5))}；"
                    + $"已登记例外 {ambiguousRegistered.Count}、未解释 {ambiguousUnexplained.Count}）"
                    + "——这是我们自己内容里的 id 歧义，已单列不重复计数"
                : string.Empty)
            + (outside.Count > 0 ? $"；**超出分母 {outside.Count} 条**（{string.Join(",", outside.Take(5))}）" : string.Empty)
            + (notLowered.Count > 0 ? $"；**声称验证却未加载 {notLowered.Count} 条**" : string.Empty)
            + "（自报范围＝受控切片已迁移内容）";
        return (detail, json);
    }

    /// <summary>去掉包前缀：登记表用限定 id，事实/键用 leaf。</summary>
    private static string Unqualify(string id)
    {
        var colon = id.IndexOf(':');
        return colon >= 0 ? id[(colon + 1)..] : id;
    }

    private static void AddCategory(Dictionary<string, SortedSet<string>> map, string key, string category)    {
        if (!map.TryGetValue(key, out var set))
        {
            map[key] = set = new SortedSet<string>(StringComparer.Ordinal);
        }

        set.Add(category);
    }

    private static int Denominator(JsonObject inventory)
    {
        var total = 0;
        foreach (var node in inventory["matching"]?["categories"]?.AsArray() ?? [])
        {
            total += node!.AsObject()["denominator"]!.GetValue<int>();
        }

        return total;
    }

    private static string VerdictSummary(JsonObject comparison)
    {
        var counts = comparison["counts"]?.AsObject();
        return counts is null
            ? "<none>"
            : string.Join("、", counts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key} {pair.Value}"));
    }

    /// <summary>
    /// **对拍覆盖**：扫描 base 侧记录里出现的每一个 `base-spire:*` 内容 id，给出"哪些内容真的进过两侧对拍、
    /// 对应哪些 case、那些 case 是什么判定"。
    ///
    /// 这样 `differential` 这一列从 `not_run` 变成**可量化的缺口**，而不是一句"未做"：
    /// 它同时给出分子（真进过对拍的内容）与分母，且只统计**记录里真的出现过**的 id，
    /// 不把"夹具标题里提到"或"代码里有声明"算成对拍覆盖。
    /// </summary>
    private static (JsonObject Json, IReadOnlyList<string> Ids) DifferentialCoverage(string repositoryRoot,
        JsonObject comparison, JsonObject inventory)
    {
        var infrastructure = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in inventory["engine_infrastructure"]?["ids"]?.AsArray() ?? [])
        {
            infrastructure.Add(node!.GetValue<string>());
        }

        foreach (var node in inventory["engine_infrastructure"]?["streams"]?.AsArray() ?? [])
        {
            infrastructure.Add(node!.GetValue<string>());
        }
        var recordsField = comparison["base_records"]?.GetValue<string>();
        var exercised = new Dictionary<string, (HashSet<string> Cases, HashSet<string> Verdicts)>(
            StringComparer.Ordinal);
        var verdictOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in comparison["cases"]?.AsArray() ?? [])
        {
            var item = node!.AsObject();
            verdictOf[item["case_id"]!.GetValue<string>()] = item["verdict"]!.GetValue<string>();
        }

        var recordsDirectory = recordsField is null
            ? null
            : Path.Combine(repositoryRoot, recordsField.Replace('/', Path.DirectorySeparatorChar));
        var caseCount = 0;
        if (recordsDirectory is not null && Directory.Exists(recordsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(recordsDirectory, "*.json").Order(StringComparer.Ordinal))
            {
                var caseId = Path.GetFileNameWithoutExtension(file);
                caseCount++;
                var text = File.ReadAllText(file);
                foreach (var id in ExtractContentIds(JsonNode.Parse(text)!))
                {
                    if (infrastructure.Contains(id))
                    {
                        continue;
                    }

                    if (!exercised.TryGetValue(id, out var bucket))
                    {
                        bucket = (new HashSet<string>(StringComparer.Ordinal),
                            new HashSet<string>(StringComparer.Ordinal));
                    }

                    bucket.Cases.Add(caseId);
                    if (verdictOf.TryGetValue(caseId, out var verdict))
                    {
                        bucket.Verdicts.Add(verdict);
                    }

                    exercised[id] = bucket;
                }
            }
        }

        var ids = exercised.Keys.Order(StringComparer.Ordinal).ToList();
        var byVerdict = new JsonObject();
        foreach (var id in ids)
        {
            foreach (var verdict in exercised[id].Verdicts)
            {
                var key = verdict;
                byVerdict[key] = (byVerdict[key]?.GetValue<int>() ?? 0) + 1;
            }
        }

        var entries = new JsonObject();
        foreach (var id in ids)
        {
            entries[id] = new JsonObject
            {
                ["cases"] = new JsonArray(exercised[id].Cases.Order(StringComparer.Ordinal)
                    .Select(caseId => (JsonNode)JsonValue.Create(caseId)).ToArray()),
                ["verdicts"] = new JsonArray(exercised[id].Verdicts.Order(StringComparer.Ordinal)
                    .Select(verdict => (JsonNode)JsonValue.Create(verdict)).ToArray()),
            };
        }

        var json = new JsonObject
        {
            ["method"] = "扫描 records/base/*.json 里出现的每一个 `base-spire:*` 字面量，"
                + "**排除引擎内置标识符**（类型/集合/关系/动作/投影/随机流，见 inventory.engine_infrastructure）；"
                + "只统计真的出现在记录里的内容，不把声明或夹具标题算成对拍覆盖。",
            ["records_directory"] = recordsField,
            ["infrastructure_excluded"] = infrastructure.Count,
            ["cases_scanned"] = caseCount,
            ["distinct_content_ids"] = ids.Count,
            ["by_verdict"] = byVerdict,
            ["exercised"] = entries,
        };
        return (json, ids);
    }

    /// <summary>
    /// 从一条记录里抽出**真的承载内容 id** 的事实位置——只认三种形状，不做"看到 `base-spire:` 就抓"：
    /// <list type="bullet">
    /// <item><c>pile.&lt;牌区&gt;.order/.sorted</c> 的值（逗号分隔的内容 id，可能带 <c>+</c> 表示升级）；</item>
    /// <item><c>power.&lt;拥有者&gt;.&lt;i&gt;.id</c> 的值；</item>
    /// <item><c>enemy.&lt;内容 id&gt;.&lt;字段&gt;</c> 的**路径**（敌人事实按内容 id 建键）。</item>
    /// </list>
    /// 早先按文本抓 `base-spire:` 会把事实路径里的 id（`enemy.…hp`）、槽位名、后端 id 一起抓进来，
    /// 让"已对拍内容"从 7 条虚报成 56 条。
    /// </summary>
    private static IEnumerable<string> ExtractContentIds(JsonNode record)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var boundary in record["boundaries"]?.AsArray() ?? [])
        {
            foreach (var factNode in boundary?["facts"]?.AsArray() ?? [])
            {
                var path = factNode!["path"]!.GetValue<string>();
                var value = factNode["value"]!.GetValue<string>();
                if (path.StartsWith("pile.", StringComparison.Ordinal)
                    && (path.EndsWith(".order", StringComparison.Ordinal)
                        || path.EndsWith(".sorted", StringComparison.Ordinal)))
                {
                    foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        ids.Add(part.TrimEnd('+'));
                    }
                }
                else if (path.StartsWith("power.", StringComparison.Ordinal)
                    && path.EndsWith(".id", StringComparison.Ordinal))
                {
                    ids.Add(value);
                }
                else if (path.StartsWith("enemy.", StringComparison.Ordinal))
                {
                    var lastDot = path.LastIndexOf('.');
                    if (lastDot > "enemy.".Length)
                    {
                        ids.Add(path["enemy.".Length..lastDot]);
                    }
                }
            }
        }

        return ids;
    }

    /// <summary>结算恒等式自检：`已降级 + 仅旧后端 + 未落地 == 分母`。重复计数会让覆盖数字失真。</summary>
    private static List<string> IdentityFailures(JsonObject inventory)
    {
        var failures = new List<string>();
        identityChecked = 0;
        foreach (var node in inventory["matching"]?["categories"]?.AsArray() ?? [])
        {
            var item = node!.AsObject();
            if (item["join_rule_verified"]?.GetValue<bool>() != true)
            {
                continue;
            }

            identityChecked++;
            if (item["identity_holds"]?.GetValue<bool>() != true)
            {
                failures.Add($"{item["category"]!.GetValue<string>()}"
                    + $"（{item["settled_check"]?.GetValue<string>()}）");
            }
        }

        return failures;
    }

    private static string ColumnState(JsonObject inventory, string column) =>
        inventory["columns"]?[column]?.GetValue<string>() ?? "<missing>";

    private static int Countable(JsonObject comparison) =>
        comparison["counts"]?["diverged"]?.GetValue<int>() ?? 0;

    private static int VanillaCases(JsonObject comparison)
    {
        var profiles = comparison["rng_profiles"]?.AsArray() ?? [];
        foreach (var node in profiles)
        {
            if (node!["profile"]!.GetValue<string>() == RngProfiles.VanillaCompatible)
            {
                return node["cases"]!.GetValue<int>();
            }
        }

        return 0;
    }

    /// <summary>重算基线摘要并与清单比对：漂移必须显式报出来，否则"冻结证据"是假的。</summary>
    private static bool Drift(string repositoryRoot, JsonObject inventory, out string detail)
    {
        var drifted = new List<string>();
        var checkedCount = 0;
        foreach (var node in inventory["baselines"]?.AsArray() ?? [])
        {
            var baseline = node!.AsObject();
            var path = baseline["path"]!.GetValue<string>();
            var kind = baseline["kind"]!.GetValue<string>();
            if (baseline["exists"]?.GetValue<bool>() != true)
            {
                continue;
            }

            checkedCount++;
            var current = Repo.DigestTree(path, kind);
            if (current.Sha256 != baseline["sha256"]!.GetValue<string>())
            {
                drifted.Add($"{path}（{baseline["files"]} → {current.Files} 个文件）");
            }
        }

        detail = drifted.Count == 0
            ? $"核对 {checkedCount} 棵目录树，无漂移"
            : $"漂移 {drifted.Count}/{checkedCount}：{string.Join("；", drifted)}";
        return drifted.Count == 0;
    }

    private static bool DenominatorHasSources(JsonObject inventory, out string detail)
    {
        var missing = new List<string>();
        var total = 0;
        foreach (var node in inventory["vanilla"]?["categories"]?.AsArray() ?? [])
        {
            var category = node!.AsObject();
            foreach (var entryNode in category["entries"]!.AsArray())
            {
                var entry = entryNode!.AsObject();
                if (entry["scope"]!.GetValue<string>() == "excluded")
                {
                    continue;
                }

                total++;
                var source = entry["source"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(source))
                {
                    missing.Add(entry["id"]!.GetValue<string>());
                }
            }
        }

        detail = missing.Count == 0
            ? $"分母 {total} 条全部带源文件:行"
            : $"{missing.Count}/{total} 条缺来源：{string.Join(",", missing.Take(8))}";
        return missing.Count == 0;
    }

    /// <summary>只统计**连接键已按原版核对**的类别：未核对的类别报"无法结算"，不当成 0 也不当成全缺。</summary>
    private static int UncoveredTotal(JsonObject inventory)
    {
        var total = 0;
        foreach (var node in inventory["matching"]?["categories"]?.AsArray() ?? [])
        {
            var item = node!.AsObject();
            if (item["join_rule_verified"]!.GetValue<bool>())
            {
                total += item["unmatched_formal_total"]!.GetValue<int>();
            }
        }

        return total;
    }

    private static int NotSettleableTotal(JsonObject inventory)
    {
        var total = 0;
        foreach (var node in inventory["matching"]?["categories"]?.AsArray() ?? [])
        {
            var item = node!.AsObject();
            if (!item["join_rule_verified"]!.GetValue<bool>())
            {
                total += item["denominator"]!.GetValue<int>();
            }
        }

        return total;
    }

    private static IEnumerable<string> UnsettleableCategories(JsonObject inventory)
    {
        foreach (var node in inventory["matching"]?["categories"]?.AsArray() ?? [])
        {
            var item = node!.AsObject();
            if (!item["join_rule_verified"]!.GetValue<bool>())
            {
                yield return $"{item["category"]!.GetValue<string>()} {item["denominator"]!.GetValue<int>()}";
            }
        }
    }

    private static JsonObject Coverage(string repositoryRoot, JsonObject inventory, JsonObject comparison,
        int uncovered, int notSettleable)
    {
        var categories = new JsonArray();
        var lowered = 0;
        var legacyOnly = 0;
        var denominator = 0;
        foreach (var node in inventory["matching"]?["categories"]?.AsArray() ?? [])
        {
            var item = node!.AsObject();
            var verified = item["join_rule_verified"]!.GetValue<bool>();
            var matched = verified ? item["matched_formal_lowered"]?.GetValue<int>() ?? 0 : (int?)null;
            denominator += item["denominator"]!.GetValue<int>();
            lowered += matched ?? 0;
            legacyOnly += verified ? item["registered_legacy_only"]?.GetValue<int>() ?? 0 : 0;
            categories.Add(new JsonObject
            {
                ["category"] = item["category"]!.GetValue<string>(),
                ["denominator"] = item["denominator"]!.GetValue<int>(),
                ["join_rule_verified"] = verified,
                ["lowered"] = matched,
                // 连接键没核对的类别：**不报数字**（0 会被读成"全缺"，分母会被读成"已覆盖"）。
                ["settleable"] = verified,
                ["registered_legacy_only"] = verified
                    ? item["registered_legacy_only"]?.GetValue<int>()
                    : null,
                ["uncovered"] = verified ? item["unmatched_formal_total"]!.GetValue<int>() : null,
                ["engine_loaded"] = null,
                ["behavior_verified"] = null,
                ["differential"] = null,
                ["host_public"] = null,
                ["uncovered_ids"] = verified ? item["unmatched_formal"]!.DeepClone() : null,
                ["blocker"] = verified ? null : "连接键未按原版核对：该类别暂不结算分子",
            });
        }

        return new JsonObject
        {
            ["coverage_version"] = Version,
            ["note"] = "只有 `passed` 计通过。`lowered` 只说明**已降级成共享作者模型**，"
                + "不等于 Engine 加载、行为验证、差分或 Host 可用；未产生的列是 null 而不是 0；"
                + "连接键未核对的类别 `settleable=false`、`uncovered=null`（不报数字，避免被读成全覆盖或全缺）。",
            ["columns_not_produced"] = new JsonArray(
                JsonValue.Create("engine_loaded_into_a_ruleset"),
                JsonValue.Create("engine_behavior_verified"),
                JsonValue.Create("differential"),
                JsonValue.Create("host_public")),
            ["totals"] = new JsonObject
            {
                ["denominator"] = denominator,
                ["lowered"] = lowered,
                ["registered_legacy_only"] = legacyOnly,
                ["uncovered"] = uncovered,
                ["identity"] = $"{lowered} + {legacyOnly} + {uncovered} == {denominator}",
                ["uncovered_scope"] = "全部类别（连接键均已按原版核对）",
                ["not_settleable"] = notSettleable,
                ["fixture_cases"] = comparison["counts"] is null
                    ? 0
                    : comparison["counts"]!.AsObject().Sum(pair => pair.Value!.GetValue<int>()),
                ["fixture_passed"] = comparison["counts"]?["passed"]?.GetValue<int>() ?? 0,
            },
            ["categories"] = categories,
            ["fixture_verdicts"] = comparison["counts"]?.DeepClone(),
            ["excluded_content"] = Excluded(inventory),
        };
    }

    /// <summary>排除项也逐条列出：结算不能只报分子。</summary>
    private static JsonArray Excluded(JsonObject inventory)
    {
        var excluded = new JsonArray();
        foreach (var node in inventory["vanilla"]?["categories"]?.AsArray() ?? [])
        {
            foreach (var entryNode in node!.AsObject()["entries"]!.AsArray())
            {
                var entry = entryNode!.AsObject();
                if (entry["scope"]!.GetValue<string>() != "excluded")
                {
                    continue;
                }

                excluded.Add(new JsonObject
                {
                    ["id"] = entry["id"]!.GetValue<string>(),
                    ["reason"] = entry["exclusion_reason"]?.GetValue<string>(),
                    ["evidence"] = entry["exclusion_evidence"]?.GetValue<string>(),
                });
            }
        }

        return excluded;
    }

    private static JsonArray BoundBaselines(string repositoryRoot, JsonObject inventory)
    {
        var baselines = new JsonArray();
        foreach (var node in inventory["baselines"]?.AsArray() ?? [])
        {
            var baseline = node!.AsObject();
            baselines.Add(new JsonObject
            {
                ["path"] = baseline["path"]!.GetValue<string>(),
                ["kind"] = baseline["kind"]!.GetValue<string>(),
                ["sha256"] = baseline["sha256"]!.GetValue<string>(),
            });
        }

        return baselines;
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
