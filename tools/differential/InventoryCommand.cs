using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BaseSpire.Slice;
using CoreTheSpire;
using CoreTheSpire.Engine;

namespace Differential;

/// <summary>
/// ENV-00 的第一份可运行产物：**绑定来源摘要的完整内容分母清单**。
///
/// 三件事分开记账、互不合并分母：
/// <list type="number">
/// <item><b>基线摘要</b>：原版恢复源码、参考模拟器、本项目生产源码各自的实际文件摘要
///       （不拿父 Git HEAD 冒充未提交目录的版本）；</item>
/// <item><b>原版分母</b>：从 <c>sts2/src/Core/Models/**</c> 按原版身份规则逐条抽出内容
///       （类别 + 条目 + 源文件:行），排除项也逐条登记对象与理由；</item>
/// <item><b>本项目侧状态</b>：明确标记已归档的 Legacy 注册段，与正式 <c>.spire</c> 前端的
///       四态（声明/解析/降级/未支持）分别列出。</item>
/// </list>
///
/// 本命令**不做**行为验证与差分：产物里的 <c>columns</c> 会把尚未产生的列显式标成
/// <c>not_run</c>/<c>not_checked</c>，读取方不得把它们当作通过。
/// </summary>
internal static class InventoryCommand
{
    private const string Version = "env-00-inventory@1";

    private static readonly (string Path, string Kind)[] Baselines =
    [
        ("sts2/src/Core/Models", "vanilla-content-source"),
        ("sts2/src/Core/Random", "vanilla-random-source"),
        ("reference/sts2-sim/src", "reference-source"),
        ("reference/sts2-sim/tests", "reference-tests"),
        ("core-the-spire/Core.TheSpire/Engine", "engine-source"),
        ("mod-the-spire/base-spire/BaseSpire/ContentSource", "base-content-source"),
        ("mod-the-spire/base-spire/BaseSpire/Slice", "base-slice-source"),
        ("net-the-spire/Net.TheSpire.Contracts", "net-contracts"),
    ];

    private static readonly string[] KeyFiles =
    [
        "sts2/src/Core/Models/ModelDb.cs",
        "sts2/src/Core/Models/ModelId.cs",
        "sts2/src/Core/Models/AbstractModel.cs",
        "sts2/src/Core/Helpers/StringHelper.cs",
        "reference/sts2-sim/README.md",
    ];

    /// <summary>Legacy 注册路径的类别名 → 原版类别名（用于把两侧放在同一张表的同一行里比较）。</summary>
    private static readonly (string Legacy, string Vanilla)[] LegacyCategories =
    [
        ("cards", "CARD"),
        ("powers", "POWER"),
        ("relics", "RELIC"),
        ("potions", "POTION"),
        ("enemies", "MONSTER"),
        ("encounters", "ENCOUNTER"),
        ("run_events", "EVENT"),
        ("card_pools", "CARD_POOL"),
        ("characters", "CHARACTER"),
        ("afflictions", "AFFLICTION"),
    ];

    public static int Run(string outputDirectory)
    {
        var root = Repo.Root();
        var inventory = new JsonObject
        {
            ["inventory_version"] = Version,
            ["repository_root"] = root,
        };

        // -- 1. 版本身份与基线摘要 ----------------------------------------------------------------------
        var projectHead = Repo.GitHead("");
        inventory["project"] = new JsonObject
        {
            ["git_head"] = projectHead.Head,
            ["git_head_source"] = projectHead.Source,
            ["note"] = "父仓库 HEAD 只标识本项目源码；sts2/ 与 reference/ 是本地未提交目录，"
                + "它们的版本只由各自的目录摘要表达（不用父 HEAD 冒充）。",
        };

        var vanillaVersion = ReadVersionEvidence(root);
        inventory["game_version_evidence"] = vanillaVersion;

        var baselines = new JsonArray();
        var missing = new List<string>();
        foreach (var (path, kind) in Baselines)
        {
            var digest = Repo.DigestTree(path, kind);
            if (!digest.Exists)
            {
                missing.Add(path);
            }

            baselines.Add(new JsonObject
            {
                ["path"] = digest.Path,
                ["kind"] = digest.Kind,
                ["exists"] = digest.Exists,
                ["files"] = digest.Files,
                ["bytes"] = digest.Bytes,
                ["sha256"] = digest.Sha256,
                ["skipped_files"] = digest.Skipped,
                ["errors"] = new JsonArray(digest.Errors.Select(error => (JsonNode)JsonValue.Create(error)).ToArray()),
            });
        }

        inventory["baselines"] = baselines;

        var referenceHead = Repo.GitHead("reference/sts2-sim");
        inventory["reference_identity"] = new JsonObject
        {
            ["path"] = "reference/sts2-sim",
            ["git_head"] = referenceHead.Head,
            ["git_head_source"] = referenceHead.Source,
            ["working_tree_is_authoritative"] = true,
        };

        var keyFiles = new JsonArray();
        foreach (var (path, exists, bytes, sha) in Repo.DigestFiles(KeyFiles))
        {
            keyFiles.Add(new JsonObject
            {
                ["path"] = path,
                ["exists"] = exists,
                ["bytes"] = bytes,
                ["sha256"] = sha,
            });
        }

        inventory["key_files"] = keyFiles;

        // -- 2. 原版分母 -------------------------------------------------------------------------------
        var vanilla = VanillaModels.Extract(root);
        var entries = vanilla.Entries;
        var categories = new JsonArray();
        foreach (var group in entries.GroupBy(entry => entry.Category, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var scopeCounts = group.GroupBy(entry => entry.Scope, StringComparer.Ordinal)
                .ToDictionary(item => item.Key, item => item.Count(), StringComparer.Ordinal);
            var ids = new JsonArray();
            foreach (var entry in group.OrderBy(item => item.Entry, StringComparer.Ordinal))
            {
                ids.Add(new JsonObject
                {
                    ["id"] = entry.VanillaId,
                    ["type"] = entry.TypeName,
                    ["source"] = $"{entry.File}:{entry.Line}",
                    ["scope"] = entry.Scope,
                    ["exclusion_reason"] = entry.Reason,
                    ["exclusion_evidence"] = entry.Evidence,
                    ["note"] = entry.Note,
                });
            }

            categories.Add(new JsonObject
            {
                ["category"] = group.Key,
                ["total"] = group.Count(),
                ["in_scope"] = scopeCounts.GetValueOrDefault("in_scope"),
                ["excluded"] = scopeCounts.GetValueOrDefault("excluded"),
                ["pending_source_check"] = scopeCounts.GetValueOrDefault("pending_source_check"),
                ["entries"] = ids,
            });
        }

        var inScope = entries.Where(entry => entry.Scope != "excluded").ToList();
        var excluded = entries.Where(entry => entry.Scope == "excluded").ToList();
        inventory["vanilla"] = new JsonObject
        {
            ["root"] = vanilla.Root,
            ["exists"] = vanilla.Exists,
            ["files_scanned"] = vanilla.FilesScanned,
            ["types_parsed"] = vanilla.TypesParsed,
            ["denominator_total"] = inScope.Count,
            ["excluded_total"] = excluded.Count,
            ["pending_source_check_total"] = entries.Count(entry => entry.Scope == "pending_source_check"),
            ["identity_rule"] = "ModelId = (SlugifyCategory(直接派生 AbstractModel 的基类名去掉 _MODEL), "
                + "Slugify(类型名))，见 sts2/src/Core/Models/ModelDb.cs:GetId 与 ModelId.cs",
            ["categories"] = categories,
            ["warnings"] = new JsonArray(vanilla.Warnings.Select(w => (JsonNode)JsonValue.Create(w)).ToArray()),
        };

        // -- 3. 本项目侧：Legacy 注册 + 正式 .spire 前端四态 ---------------------------------------------
        var legacy = LegacyRegistered();
        var formal = FormalFrontend();

        inventory["base_spire"] = new JsonObject
        {
            ["legacy_registered"] = legacy.Json,
            ["formal_frontend"] = formal.Json,
        };

        // -- 4. 原版分母 × 本项目状态（明确写出连接规则与未核对范围） ------------------------------------
        var matching = BuildMatching(inScope, legacy.LeafIdsByCategory, formal.LoweredByCategory);
        inventory["matching"] = matching;
        inventory["id_exceptions"] = IdExceptions(root, legacy.LeafIdsByCategory);
        inventory["engine_infrastructure"] = EngineInfrastructure();
        inventory["engine_loaded"] = EngineLoaded();

        // -- 5. 机制依赖信号：未支持原因族 -----------------------------------------------------------------
        inventory["mechanisms"] = MechanismHistogram(formal.Unsupported);
        // 名字带 `_mechanisms` 后缀：`next_batch` 是既有的**叙述性**下一批清单（人手维护、不可推导），
        // 这一份是**从原因文本推导**的机制依赖图，两者互补，不能互相覆盖。
        inventory["next_batch_mechanisms"] = NextBatch(formal.Unsupported);
        inventory["single_family_content"] = SingleFamilyContent(formal.Unsupported);

        // -- 6. 尚未产生的列（显式，读取方不得当作通过） -------------------------------------------------
        inventory["columns"] = new JsonObject
        {
            ["source_confirmed"] = "produced_by_this_command",
            ["base_declared_parsed_lowered"] = "produced_by_this_command",
            ["engine_loaded_into_a_ruleset"] = "not_run",
            ["engine_behavior_verified"] = "not_run",
            ["differential"] = "not_run",
            ["host_public"] = "not_checked",
        };

        inventory["commands"] = new JsonObject
        {
            ["inventory"] = "implemented",
            ["compare"] = "not_implemented",
            ["run"] = "not_implemented",
            ["verify"] = "not_implemented",
        };

        // -- 7. 下一批 ------------------------------------------------------------------------------------
        var nextBatch = new JsonArray();
        void AddNext(string item) => nextBatch.Add(JsonValue.Create(item));
        AddNext("compare/run/verify 子命令尚未实现：先做无随机受控夹具的 compare（首差报告），"
            + "随机路径必须报 unsupported_rng_profile（ENV-00/ENV-01）。");
        if (missing.Count > 0)
        {
            AddNext("基线目录缺失，无法冻结对应范围的摘要：" + string.Join(", ", missing));
        }

        if (vanillaVersion["machine_readable"]!.GetValue<bool>() == false)
        {
            AddNext("游戏版本身份只有文档声明、没有机器可读冻结："
                + "需在 ENV-00 落一份带 source/reference 摘要的版本证据文件。");
        }

        var unmatchedFormal = inScope
            .Where(entry => entry.Category is "CARD" or "POWER")
            .Where(entry => !JoinKey(entry).Keys.Any(formal.LoweredLeafIds.Contains))
            .ToList();
        AddNext($"正式前端未降级的分母内容 {unmatchedFormal.Count} 条"
            + $"（最大类别：{TopCategory(unmatchedFormal)}）；按机制族补，不逐张牌修原语。");
        AddNext("mock/deprecated/multiplayer 的范围判定目前只有类名与路径依据，"
            + "需按原版可达性核对；核对前它们分别保留在 pending_source_check 与 excluded 里并逐条登记。");
        AddNext("多类别（非 CARD/POWER）的 join 规则未核对：本产物只保证 CARD/POWER 的连接键，"
            + "其余类别的匹配数仅供参考。");
        inventory["next_batch"] = nextBatch;

        // -- 8. 落盘 --------------------------------------------------------------------------------------
        var outputPath = Path.IsPathRooted(outputDirectory)
            ? outputDirectory
            : Path.Combine(root, outputDirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(outputPath);
        var jsonPath = Path.Combine(outputPath, "inventory.json");
        File.WriteAllText(jsonPath,
            inventory.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        var markdownPath = Path.Combine(outputPath, "inventory.md");
        File.WriteAllText(markdownPath, Markdown(inventory), new UTF8Encoding(false));

        Console.WriteLine($"inventory written: {jsonPath}");
        Console.WriteLine($"markdown summary:   {markdownPath}");
        Console.WriteLine($"vanilla: 文件 {vanilla.FilesScanned} / 类型 {vanilla.TypesParsed} / "
            + $"分母 {inScope.Count} / 排除 {excluded.Count} / 待核对 {entries.Count(e => e.Scope == "pending_source_check")}");
        Console.WriteLine($"base-spire: Legacy 已归档未运行 / 正式降级 卡 {formal.LoweredCards} 能力 {formal.LoweredPowers} / "
            + $"未支持原因族 {formal.Unsupported.Count}");
        foreach (var group in inScope.GroupBy(entry => entry.Category, StringComparer.Ordinal)
                     .OrderByDescending(group => group.Count()))
        {
            if (group.Key is "CARD" or "POWER")
            {
                var matched = group.Count(entry => JoinKey(entry).Keys.Any(formal.LoweredLeafIds.Contains));
                Console.WriteLine($"  {group.Key,-14} 分母 {group.Count(),5}  正式已降级 {matched,5}");
            }
            else
            {
                Console.WriteLine($"  {group.Key,-14} 分母 {group.Count(),5}  正式已降级    n/a（连接键未核对）");
            }
        }

        if (missing.Count > 0)
        {
            Console.Error.WriteLine("缺少基线目录：" + string.Join(", ", missing));
            return 1;
        }

        return 0;
    }

    // -- 版本证据 -------------------------------------------------------------------------------------------

    private static JsonObject ReadVersionEvidence(string root)
    {
        var candidates = new[]
        {
            "sts2/release_info.json",
            "sts2/src/release_info.json",
            "release_info.json",
        };
        foreach (var candidate in candidates)
        {
            var path = Path.Combine(root, candidate.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(path);
            return new JsonObject
            {
                ["machine_readable"] = true,
                ["path"] = candidate,
                ["sha256"] = Repo.Sha256(bytes),
                ["content"] = JsonNode.Parse(Encoding.UTF8.GetString(bytes)),
            };
        }

        return new JsonObject
        {
            ["machine_readable"] = false,
            ["claimed_version"] = "v0.111.0 / commit 41cef1ea / main_assembly_hash 222455745",
            ["evidence"] = new JsonArray(
                JsonValue.Create("docs/design/environment-completion-and-differential-v1.md:22"),
                JsonValue.Create("reference/sts2-sim/README.md:33")),
            ["note"] = "sts2/ 是本地恢复工程（被 .gitignore 排除），目录里没有 release_info.json；"
                + "版本目前只有文档声明，属于 ENV-00 未完成项。",
        };
    }

    // -- Legacy 注册路径 ------------------------------------------------------------------------------------

    private sealed record LegacyResult(JsonObject Json, int Total, HashSet<string> LeafIds,
        Dictionary<string, HashSet<string>> LeafIdsByCategory);

    private static LegacyResult LegacyRegistered()
    {
        // The compatibility implementation is archived and is not a dependency of this executable.
        // Preserve the report section as explicitly unavailable, rather than claiming a measured zero.
        var json = new JsonObject
        {
            ["ok"] = false,
            ["status"] = "archived_not_run",
            ["registered_total"] = null,
            ["archive"] = "archive/legacy-sts",
            ["note"] = "旧注册器已归档，本命令不加载归档程序集；空匹配仅表示没有本轮 Legacy 证据，不表示旧内容数为零。",
        };
        return new LegacyResult(json, 0, new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal));
    }

    // -- 正式 .spire 前端四态 -------------------------------------------------------------------------------

    private sealed record FormalResult(JsonObject Json, int LoweredCards, int LoweredPowers,
        HashSet<string> LoweredLeafIds, IReadOnlyList<(string Name, string Reason)> Unsupported,
        Dictionary<string, HashSet<string>> LoweredByCategory);

    private static FormalResult FormalFrontend()
    {
        var json = new JsonObject();
        var loweredLeafIds = new HashSet<string>(StringComparer.Ordinal);
        var loweredByCategory = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["cards"] = new(StringComparer.Ordinal),
            ["powers"] = new(StringComparer.Ordinal),
        };
        var unsupported = new List<(string, string)>();
        var cardResources = new JsonArray();
        var powerResources = new JsonArray();
        var loweredCards = 0;
        var loweredPowers = 0;

        try
        {
            // 链接表用**已加载的能力源**而不是宽表 `KnownIds`：宽表会给卡牌一个指向"存在但没加载"的能力 id
            // （例如 BattleTrance 的 NoDraw → `base-spire:no-draw`，该能力被能力前端判为运行时未实现），
            // 那样数出来的降级卡是**乐观数**——一旦真的构建就会得到一个悬空引用。
            var links = SpirePowerSource.PowerResources()
                .SelectMany(resource => SpirePowerSource.LoadResource(resource).Powers)
                .GroupBy(power => power.Name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);
            json["link_table"] = "已加载能力源（不是宽表 KnownIds）";
            json["link_orphans"] = new JsonArray(
                (FormalSpirePackage.Load().LinkOrphans)
                .Select(name => (JsonNode)JsonValue.Create(name)).ToArray());
            foreach (var resource in SpirePowerSource.PowerResources())
            {
                var source = SpirePowerSource.LoadResource(resource, links);
                loweredPowers += source.LoweredCount;
                foreach (var power in source.Powers)
                {
                    loweredLeafIds.Add(Unqualified(power.Id));
                    loweredByCategory["powers"].Add(Unqualified(power.Id));
                }

                foreach (var (name, reason) in source.Unsupported)
                {
                    unsupported.Add(($"power:{name}", reason));
                }

                powerResources.Add(new JsonObject
                {
                    ["resource"] = resource,
                    ["declared"] = source.DeclaredCount,
                    ["parsed"] = source.ParsedCount,
                    ["lowered"] = source.LoweredCount,
                    ["unsupported"] = source.Unsupported.Count,
                });
            }

            foreach (var resource in SpireCardSource.CardResources())
            {
                var source = SpireCardSource.LoadResource(resource, links);
                loweredCards += source.LoweredCount;
                foreach (var card in source.Cards)
                {
                    loweredLeafIds.Add(Unqualified(card.Id));
                    loweredByCategory["cards"].Add(Unqualified(card.Id));
                }

                foreach (var (name, reason) in source.Unsupported)
                {
                    unsupported.Add(($"card:{name}", reason));
                }

                cardResources.Add(new JsonObject
                {
                    ["resource"] = resource,
                    ["declared"] = source.DeclaredCount,
                    ["parsed"] = source.ParsedCount,
                    ["lowered"] = source.LoweredCount,
                    ["unsupported"] = source.Unsupported.Count,
                });
            }

            json["ok"] = true;
            json["note"] = "四态：声明/解析/降级/未支持。降级=已产出共享作者模型（AuthorCard/AuthorPower），"
                + "但**不代表**已经进过某个 Engine 规则集或做过行为验证。";
            json["cards"] = new JsonObject
            {
                ["resources"] = cardResources,
                ["lowered_total"] = loweredCards,
            };
            json["powers"] = new JsonObject
            {
                ["resources"] = powerResources,
                ["lowered_total"] = loweredPowers,
            };
            // 每条未支持内容的**具体原因**：挑下一个目标时不用再去猜是哪一层缺东西。
            json["unsupported_detail"] = new JsonArray(unsupported
                .OrderBy(entry => entry.Item1, StringComparer.Ordinal)
                .Select(entry => (JsonNode)new JsonObject
                {
                    ["content"] = entry.Item1,
                    ["reason"] = entry.Item2,
                }).ToArray());
            json["unsupported_total"] = unsupported.Count;
            return new FormalResult(json, loweredCards, loweredPowers, loweredLeafIds, unsupported, loweredByCategory);
        }
        catch (Exception failure)
        {
            json["ok"] = false;
            json["error"] = $"{failure.GetType().Name}: {failure.Message}";
            return new FormalResult(json, loweredCards, loweredPowers, loweredLeafIds, unsupported, loweredByCategory);
        }
    }

    // -- 连接 ----------------------------------------------------------------------------------------------

    /// <summary>
    /// 各类别的连接键规则（原版内容 → 本项目内容 id）。规则本身**可以被测量**：
    /// <list type="bullet">
    /// <item><c>rules</c>：规则文本；</item>
    /// <item><c>suspect</c>：用规则匹配不上、但把名字里所有非字母数字去掉后能对上我们某条 id 的条目——
    ///       说明命名约定不同（例如缩写边界），规则需要修正，不能算"未实现"；</item>
    /// <item><c>collision</c>：两条不同原版内容映射到同一个 id——规则有歧义。</item>
    /// </list>
    /// 只有 <c>suspect=0 且 collision=0</c> 才把该类别标成 <c>join_rule_verified</c>，
    /// 并附上"原版类型名 ↔ 我们的 id"的真实样本作为证据。
    /// </summary>
    private static JsonObject BuildMatching(IReadOnlyList<VanillaEntry> inScope,
        Dictionary<string, HashSet<string>> legacyByCategory, Dictionary<string, HashSet<string>> formalByCategory)
    {
        var perCategory = new JsonArray();
        // 内容 id 的命名空间是**跨类别共享**的：先把每个 leaf id 的类别集合收齐，
        // 才能发现"同一 id 既是卡牌又是能力"这类歧义（并交给登记表核对）。
        var keyToCategories = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var group in inScope.GroupBy(entry => entry.Category, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var keys = group.Select(entry => (Entry: entry, Key: JoinKey(entry))).ToList();
            var joinable = keys.Where(item => item.Key.Keys.Count > 0)
                .Select(item => item.Key.Keys[0]).ToList();

            // 规则歧义：同一条 id 被两条原版内容映射到。
            var collisions = joinable.GroupBy(key => key, StringComparer.Ordinal)
                .Where(cluster => cluster.Count() > 1)
                .Select(cluster => cluster.Key)
                .Order(StringComparer.Ordinal)
                .ToList();

            var registeredIds = legacyByCategory.TryGetValue(LegacyNameOf(group.Key), out var set)
                ? set
                : [];
            var formalLeafIds = formalByCategory.TryGetValue(LegacyNameOf(group.Key), out var lowered)
                ? lowered
                : [];
            // Active matches must come from the actual formal frontend, independently of archived registration.
            var ourIds = registeredIds.Concat(formalLeafIds).ToHashSet(StringComparer.Ordinal);
            var normalizedOurs = ourIds.ToDictionary(Normalize, id => id, StringComparer.Ordinal);
            var suspects = new List<string>();
            var evidence = new List<string>();
            var variantMatched = new List<string>();
            var matchedKeys = new List<string>();
            foreach (var (entry, key) in keys)
            {
                var hit = key.Keys.FirstOrDefault(ourIds.Contains);
                if (hit is not null)
                {
                    matchedKeys.Add(hit);
                    if (hit != key.Keys[0])
                    {
                        variantMatched.Add($"{entry.TypeName} → {hit}（主规则给 {key.Keys[0]}）");
                    }

                    if (evidence.Count < 3)
                    {
                        evidence.Add($"{entry.TypeName} ↔ base-spire:{hit}");
                    }

                    continue;
                }

                // 规则匹配不上，但"去掉非字母数字"能对上我们某条 id ⇒ 是**命名约定**差异，不是未实现。
                var normalized = normalizedOurs.TryGetValue(Normalize(entry.Entry), out var ours);
                if (normalized)
                {
                    suspects.Add($"{entry.TypeName} → 规则给出 {string.Join("/", key.Keys)}，但我们有 {ours}");
                }
            }

            var verified = suspects.Count == 0 && collisions.Count == 0;
            var formal = group.Key is "CARD" or "POWER";
            // 未落地 = **任何**候选键都不在我们注册的 id 里、也不在正式前端降级集里。
            // （曾经只查主键，于是"靠变体命中"的条目被同时算进 matched 和 unmatched，
            //  matched + unmatched > denominator —— 结算数字自相矛盾。）
            var unmatchedFormal = keys
                .Where(item => !item.Key.Keys.Any(ourIds.Contains)
                    && !item.Key.Keys.Any(formalLeafIds.Contains))
                .Select(item => item.Key.Keys.Count > 0 ? item.Key.Keys[0] : null)
                .Where(key => key is not null)
                .Select(key => key!)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();

            // 把这个类别的键并进跨类别命名空间（**原版**命名层面），供"原版同名"统计。
            foreach (var key in matchedKeys.Concat(unmatchedFormal).Distinct(StringComparer.Ordinal))
            {
                if (!keyToCategories.TryGetValue(key, out var owners))
                {
                    keyToCategories[key] = owners = new SortedSet<string>(StringComparer.Ordinal);
                }

                owners.Add(group.Key);
            }
            perCategory.Add(new JsonObject
            {
                ["category"] = group.Key,
                ["our_category"] = LegacyNameOf(group.Key),
                ["denominator"] = group.Count(),
                ["join_rule"] = keys.FirstOrDefault().Key.Rule ?? "unverified",
                ["join_rule_verified"] = verified,
                ["rule_suspect"] = new JsonArray(suspects.Select(s => (JsonNode)JsonValue.Create(s)).ToArray()),
                ["rule_collision"] = new JsonArray(collisions.Select(s => (JsonNode)JsonValue.Create(s)).ToArray()),
                // 主规则没命中、靠"变体规则"命中的条目：对应关系仍在（同名），但**我们的 id 约定不统一**，
                // 因此单列出来当内容命名缺陷，不藏进匹配数里。
                ["rule_variant_matched"] = new JsonArray(
                    variantMatched.Select(s => (JsonNode)JsonValue.Create(s)).ToArray()),
                ["rule_evidence"] = new JsonArray(evidence.Select(s => (JsonNode)JsonValue.Create(s)).ToArray()),
                ["our_registered"] = null,
                ["legacy_registration_status"] = "archived_not_run",
                ["formal_frontend_covers"] = formal,
                // 命中的连接键（leaf 形式）：结算侧要用它把"行为已验证"/"已对拍"的 id 归到类别上。
                ["matched_keys"] = verified
                    ? new JsonArray(matchedKeys.Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .Select(key => (JsonNode)JsonValue.Create(key)).ToArray())
                    : null,
                ["matched_legacy"] = null,
                ["matched_formal_lowered"] = verified ? matchedKeys.Count(formalLeafIds.Contains) : null,
                // 三条互不重叠、可相加核对：formal + legacy_only + uncovered == denominator。
                ["registered_legacy_only"] = null,
                ["identity_holds"] = verified
                    && matchedKeys.Count(formalLeafIds.Contains)
                        + (matchedKeys.Count - matchedKeys.Count(formalLeafIds.Contains))
                        + unmatchedFormal.Count == group.Count(),
                ["settled_check"] = verified
                    ? $"{matchedKeys.Count(formalLeafIds.Contains)} + "
                        + $"{matchedKeys.Count - matchedKeys.Count(formalLeafIds.Contains)} + "
                        + $"{unmatchedFormal.Count} == {group.Count()}"
                    : null,
                ["unmatched_formal_total"] = unmatchedFormal.Count,
                // 完整清单（不是只给样例）：`verify` 要按这份清单结算 coverage，缺项必须能被逐条列出。
                ["unmatched_formal"] = new JsonArray(
                    unmatchedFormal.Select(id => (JsonNode)JsonValue.Create(id)).ToArray()),
            });
        }

        var crossCategory = keyToCategories
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new JsonObject
        {
            ["legacy_evidence_status"] = "archived_not_run",
            ["note"] = "Legacy 注册不再运行；本轮匹配直接使用正式前端按类别产生的 ID，归档注册计数为 null。匹配用**内容 id 末段**做连接（去掉包前缀）：规则是 `kebab(原版类型名)`，能力额外去掉 "
                + "`Power` 后缀。规则本身是**被测量**的——`rule_suspect` 列出'规则匹配不上、但去掉非字母数字后"
                + "能对上我们某条 id'的条目（命名约定差异，不是未实现），`rule_collision` 列出两条原版内容"
                + "映射到同一个 id 的歧义；只有两者都为空才标 `join_rule_verified`。"
                + "匹配只说明'本项目声明了同名内容'，不说明行为一致。",
            // **原版**命名层面的跨类别同名（卡 `X` 与能力 `XPower`）：原版就是这么命名的
            // （很多能力与施加它的卡同名），因此这不是歧义，而是解释"能力为什么靠变体命中"的事实。
            ["cross_category_name_overlap"] = new JsonArray(crossCategory
                .Select(id => (JsonNode)JsonValue.Create(id)).ToArray()),
            ["categories"] = perCategory,
        };
    }

    /// <summary>
    /// **我们自己注册的 id** 的跨类别同名核对 + 登记表一致性。
    ///
    /// 用注册 id 而不是原版名字：原版"卡 `X` / 能力 `XPower`"是惯例，工具把能力后缀去掉之后它们自然同名；
    /// 真正需要治理的是**我们的内容 id 命名空间**里同一个字符串被两个类别注册。
    /// 登记的例外必须仍然歧义（否则报 `stale`），真实歧义必须有登记（否则报 `unexplained`）。
    /// </summary>
    private static JsonObject IdExceptions(string repositoryRoot,
        Dictionary<string, HashSet<string>> leafIdsByCategory)
    {
        var owners = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var (category, ids) in leafIdsByCategory)
        {
            foreach (var id in ids)
            {
                if (!owners.TryGetValue(id, out var set))
                {
                    owners[id] = set = new SortedSet<string>(StringComparer.Ordinal);
                }

                set.Add(LegacyCategoryName(category));
            }
        }

        var collisions = owners.Where(pair => pair.Value.Count > 1)
            .ToDictionary(pair => "base-spire:" + pair.Key, pair => pair.Value, StringComparer.Ordinal);

        var json = new JsonObject();
        var path = Path.Combine(repositoryRoot, "tools", "differential", "registries", "id-exceptions.json");
        json["collisions"] = new JsonObject(collisions
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key,
                JsonValue.Create(string.Join("+", pair.Value)))));
        if (!File.Exists(path))
        {
            json["ok"] = false;
            json["note"] = "没有登记表：所有跨类别同名都会被当成未解释。";
            json["registered"] = new JsonArray();
            json["stale"] = new JsonArray();
            json["unexplained"] = new JsonArray(collisions.Keys
                .Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
            return json;
        }

        var registry = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var registered = new List<(string Id, string Reason)>();
        foreach (var node in registry["exceptions"]!.AsArray())
        {
            var item = node!.AsObject();
            registered.Add((item["id"]!.GetValue<string>(), item["impact"]!.GetValue<string>()));
        }

        var registeredIds = new HashSet<string>(registered.Select(item => item.Id), StringComparer.Ordinal);
        json["ok"] = true;
        json["path"] = "tools/differential/registries/id-exceptions.json";
        json["version"] = registry["version"]?.GetValue<string>();
        json["registered"] = new JsonArray(registered
            .Where(item => collisions.ContainsKey(item.Id))
            .Select(item => (JsonNode)new JsonObject
            {
                ["id"] = item.Id,
                ["categories"] = collisions[item.Id] is { } set
                    ? string.Join("+", set)
                    : string.Empty,
                ["impact"] = item.Reason,
            })
            .ToArray());
        json["stale"] = new JsonArray(registered
            .Where(item => !collisions.ContainsKey(item.Id))
            .Select(item => (JsonNode)JsonValue.Create(item.Id))
            .ToArray());
        json["unexplained"] = new JsonArray(collisions.Keys
            .Where(id => !registeredIds.Contains(id))
            .Order(StringComparer.Ordinal)
            .Select(id => (JsonNode)JsonValue.Create(id))
            .ToArray());
        return json;
    }

    /// <summary>我们注册路径的类别名 → 原版类别名（登记表与报告都用原版类别名说话）。</summary>
    private static string LegacyCategoryName(string legacyCategory) =>
        LegacyCategories.FirstOrDefault(pair => pair.Legacy == legacyCategory).Vanilla ?? legacyCategory;

    /// <summary>原版类别名 → 我们注册路径里的类别名（用于取该类别自己的 id 集合）。</summary>
    private static string LegacyNameOf(string vanillaCategory) =>
        LegacyCategories.FirstOrDefault(pair => pair.Vanilla == vanillaCategory).Legacy ?? vanillaCategory;

    /// <summary>名字规范化：只留字母数字并转小写，用来发现"同一名字的不同命名约定"。</summary>
    private static string Normalize(string text) =>
        new([.. text.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    /// <summary>
    /// 连接键规则：**主规则 +（必要时）变体**。
    ///
    /// 主规则是 `kebab(原版类型名)`；能力额外接受"去掉 `Power` 后缀"。
    /// 变体的存在本身是一条**内容命名缺陷**：我们自己的能力 id 有的去后缀（`vulnerable`）、
    /// 有的保后缀（`barricade-power`），所以两条都试；靠变体命中的条目会被单列进
    /// <c>rule_variant_matched</c>，而不是混进匹配数里。
    /// </summary>
    private static (IReadOnlyList<string> Keys, string Rule) JoinKey(VanillaEntry entry)
    {
        if (entry.Category == "POWER")
        {
            var stripped = entry.Entry.EndsWith("_POWER", StringComparison.Ordinal)
                ? VanillaModels.Kebab(entry.Entry[..^"_POWER".Length])
                : null;
            var full = entry.Kebab;
            return stripped is null
                ? ([full], "kebab(原版类型名)")
                : ([stripped, full], "kebab(原版类型名去掉 Power 后缀)，变体：保留后缀");
        }

        return ([entry.Kebab], "kebab(原版类型名)");
    }

    // -- 引擎内置标识符 -------------------------------------------------------------------------------------

    /// <summary>
    /// 受控切片规则集里**不是内容**的限定标识符（类型/集合/关系/作用域/过程/事件/终局/投影/动作）与随机流名。
    ///
    /// 为什么要单独导出：`verify` 要从对拍记录里数"哪些**内容**进过对拍"，而记录里同时出现
    /// `base-spire:Hand`、`base-spire:PlayCard`、`base-spire:combat-draw` 这类内置标识符；
    /// 不把它们排除掉，覆盖数字会虚高。这份清单由**真实构建的规则集**给出，不是手抄。
    /// </summary>
    private static JsonObject EngineInfrastructure()
    {
        var json = new JsonObject();
        try
        {
            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build(false).Build());
            var rules = builder.Build();
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var type in rules.Types) ids.Add(type.QualifiedId);
            foreach (var collection in rules.Collections) ids.Add(collection.QualifiedId);
            foreach (var relation in rules.Relations) ids.Add(relation.QualifiedId);
            foreach (var scope in rules.ScopeTypes) ids.Add(scope.QualifiedId);
            foreach (var procedure in rules.Procedures) ids.Add(procedure.QualifiedId);
            foreach (var @event in rules.Events) ids.Add(@event.QualifiedId);
            foreach (var terminal in rules.TerminalResults) ids.Add(terminal.QualifiedId);
            foreach (var projection in rules.Projections) ids.Add(projection.QualifiedId);
            foreach (var action in rules.Actions) ids.Add(action.QualifiedId);
            json["ok"] = true;
            json["note"] = "由真实构建的受控切片规则集给出；随机流名另列（引擎按名字寻址，不是内容）。";
            json["ids"] = new JsonArray(ids.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
            json["streams"] = new JsonArray(
                JsonValue.Create(SliceManifest.DrawStream),
                JsonValue.Create(SliceManifest.RandomTargetStream),
                JsonValue.Create(SliceManifest.RandomCardStream));
            return json;
        }
        catch (Exception failure)
        {
            json["ok"] = false;
            json["error"] = $"{failure.GetType().Name}: {failure.Message}";
            json["ids"] = new JsonArray();
            json["streams"] = new JsonArray();
            return json;
        }
    }

    // -- 引擎加载（ENV-02 第一步） -------------------------------------------------------------------------

    /// <summary>
    /// **引擎到底加载了什么**：把正式源组进可运行的 Engine 包 → 链接成规则集 → 开会话 → 枚举**卡牌实例**的
    /// `DefinitionId`。
    ///
    /// 为什么从会话状态读而不是从声明读：声明只说明"我打算加载"，链接/加载/实例化失败都不体现在声明里。
    /// 卡组是"每张降级卡各 1 张"，所以每个降级卡都应当在启动后有实例；差集就是真实的加载缺口。
    ///
    /// 能力**不计入这一列**：能力实例只在被打出时创建，开局没有实例，把它们按声明计入就是过度声称；
    /// 这里只单独报"包内声明的能力数"。
    /// </summary>
    private static JsonObject EngineLoaded()
    {
        var json = new JsonObject();
        try
        {
            var rules = FormalSpirePackage.BuildRuleSet(out var loaded);
            var session = new Session(rules, 20261001UL, [SliceManifest.PlayerRole]);
            session.Start();

            var cardType = rules.TypeByQualifiedId("base-spire:Card");
            var instantiated = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var instance in session.State.AllInstances(cardType.Id))
            {
                var definition = session.State.GetField(instance, cardType.FieldIndex("DefinitionId")).AsString();
                instantiated.Add(Unqualified(definition));
            }

            var declared = new HashSet<string>(
                loaded.CardIds.Select(Unqualified), StringComparer.Ordinal);
            var notLoaded = declared.Where(id => !instantiated.Contains(id))
                .Order(StringComparer.Ordinal).ToList();
            var unexpected = instantiated.Where(id => !declared.Contains(id))
                .Order(StringComparer.Ordinal).ToList();

            json["ok"] = true;
            json["package_version"] = FormalSpirePackage.Version;
            json["method"] = "正式源 → EnginePackageBuilder → RuleSetBuilder → Session.Start() → 枚举卡牌实例的 "
                + "DefinitionId；卡组为每张降级卡各 1 张，因此每个降级卡都应有实例。";
            json["declared_cards"] = declared.Count;
            json["instantiated_cards"] = instantiated.Count;
            json["declared_powers"] = loaded.PowerIds.Count;
            json["unsupported_cards"] = loaded.UnsupportedCards.Count;
            json["unsupported_powers"] = loaded.UnsupportedPowers.Count;
            json["link_orphans"] = new JsonArray(
                loaded.LinkOrphans.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
            json["power_demand"] = new JsonArray(loaded.PowerDemand.Select(entry => (JsonNode)new JsonObject
            {
                ["power"] = entry.Power,
                ["blocked_cards"] = entry.Cards.Count,
                ["cards"] = new JsonArray(entry.Cards.Select(name => (JsonNode)JsonValue.Create(name)).ToArray()),
            }).ToArray());
            json["lowered_not_loaded"] = new JsonArray(
                notLoaded.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
            json["loaded_not_declared"] = new JsonArray(
                unexpected.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
            json["note"] = "能力不计入本列：能力实例只在被打出时创建，开局没有实例——"
                + "把包内声明的能力算成'引擎已加载'是过度声称。";
            return json;
        }
        catch (Exception failure)
        {
            json["ok"] = false;
            json["error"] = $"{failure.GetType().Name}: {failure.Message}";
            return json;
        }
    }

    // -- 机制直方图 -----------------------------------------------------------------------------------------

    private static readonly Regex DottedPrefix = new(@"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+\.?[ \t]*",
        RegexOptions.Compiled);

    /// <summary>
    /// ENV-00 要求的**机制依赖图 + 简短下一批列表**（机器可读）。
    ///
    /// 从每条未支持原因里抽出**机制单元**并分类，按"挡住多少内容"排序：
    /// <list type="bullet">
    /// <item><c>field</c>：能力字段（`能力字段 'X' 未支持`、`参数 'X' 未支持`）；</item>
    /// <item><c>primitive</c>：原语（`原语 'X' 未支持`）；</item>
    /// <item><c>power</c>：引用的能力名（`的能力 'X' 无法链接`）；</item>
    /// <item><c>hook</c>：钩子（`钩子 'X' 未支持`）。</item>
    /// </list>
    /// **为什么按机制而不是按内容排**：机制是按内容交叠的，同一张卡可能同时缺三样；
    /// 这份图给出"补哪一个机制能解锁哪些内容"，由人来决定把哪些机制放进同一批（例如同一个声明形状）。
    /// </summary>
    private static JsonArray NextBatch(IReadOnlyList<(string Name, string Reason)> unsupported)
    {
        var tokens = new Dictionary<(string Kind, string Token), SortedSet<string>>();
        foreach (var (name, reason) in unsupported)
        {
            foreach (var fragment in reason.Split('；', StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var (kind, token) in MechanismTokens(fragment))
                {
                    var key = (kind, token);
                    if (!tokens.TryGetValue(key, out var users))
                    {
                        tokens[key] = users = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    users.Add(name);
                }
            }
        }

        return new JsonArray(tokens
            .OrderByDescending(pair => pair.Value.Count)
            .ThenBy(pair => pair.Key.Kind, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Token, StringComparer.Ordinal)
            .Select(pair => (JsonNode)new JsonObject
            {
                ["mechanism"] = pair.Key.Token,
                ["kind"] = pair.Key.Kind,
                ["blocked_content"] = pair.Value.Count,
                ["content"] = new JsonArray(pair.Value.Take(20)
                    .Select(name => (JsonNode)JsonValue.Create(name)).ToArray()),
            })
            .ToArray());
    }

    private static readonly Regex QuotedToken = new(@"'([^']+)'", RegexOptions.Compiled);

    /// <summary>把一条原因片段拆成"机制单元"（种类 + 名字）。名字一律取解析器自己点名的 `'X'`。</summary>
    private static IEnumerable<(string Kind, string Token)> MechanismTokens(string fragment)
    {
        var kind = fragment.Contains("能力字段", StringComparison.Ordinal)
            || fragment.Contains("参数", StringComparison.Ordinal)
                ? "field"
                : fragment.Contains("关键词", StringComparison.Ordinal)
                    ? "keyword"
                    : fragment.Contains("原语", StringComparison.Ordinal)
                        ? "primitive"
                        : fragment.Contains("钩子", StringComparison.Ordinal)
                            ? "hook"
                            : fragment.Contains("的能力", StringComparison.Ordinal)
                                ? "power"
                                : "other";
        foreach (Match match in QuotedToken.Matches(fragment))
        {
            yield return (kind, match.Groups[1].Value);
        }
    }

    /// <summary>
    /// **只命中一个机制族**的内容：用来找"看起来最便宜"的批次单位。
    ///
    /// **它会少报工作量，不要直接当批次依据**：族相同不等于机制相同。实测两个反例——
    /// <list type="bullet">
    /// <item>`Mangle` 只命中"原语"族，但它的 `if` 还需要 `targetAlive` 谓词与 `ManglePower` 能力；</item>
    /// <item>`Colossus` 只命中"能力引用"族，但 `ColossusPower` 的语义是
    ///       "**攻击者身上有易伤**时把受击伤害乘 0.5"——需要一个新的条件减伤要求种类。</item>
    /// </list>
    /// 因此正确的读法是"这是候选清单"，真正的批次单位要按机制依赖（谓词、引用的能力、字段）再拆一层。
    /// </summary>
    private static JsonArray SingleFamilyContent(IReadOnlyList<(string Name, string Reason)> unsupported)
    {
        // 先收集成普通元组再建 JSON 节点：JsonNode 不能同时挂在两个父节点下
        //（直接 new JsonArray(已有父节点的节点) 会抛 "The node already has a parent"）。
        var found = new List<(string Family, string Content, string Reason)>();
        foreach (var (name, reason) in unsupported)
        {
            var families = reason.Split('；', StringSplitOptions.RemoveEmptyEntries)
                .Select(Family)
                .Where(family => family.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
            if (families.Count != 1)
            {
                continue;
            }

            found.Add((families[0], name, reason));
        }

        return new JsonArray(found
            .OrderBy(entry => entry.Family, StringComparer.Ordinal)
            .ThenBy(entry => entry.Content, StringComparer.Ordinal)
            .Select(entry => (JsonNode)new JsonObject
            {
                ["content"] = entry.Content,
                ["family"] = entry.Family,
                ["reason"] = entry.Reason,
            })
            .ToArray());
    }

    /// <summary>
    /// 机制直方图：把"未支持原因"按**机制族**归类，给出"哪些机制卡住了哪些内容"。
    ///
    /// 口径（曾经错过一次）：一个内容可能同时命中多个族，因此
    /// <list type="bullet">
    /// <item><c>blocked_content</c> = **去重后的内容条数**——批次要按这个排；</item>
    /// <item><c>reason_fragments</c> = 命中该族的**原因片段数**（同一内容可能贡献多条）；</item>
    /// <item><c>content</c> = 具体卡/能力名（最多列 20 条），用来直接组一个批次。</item>
    /// </list>
    /// 早先只报片段数并把它当"卡住多少内容"，会把一张卡的 3 条原因算成 3 张卡。
    /// </summary>
    private static JsonArray MechanismHistogram(IReadOnlyList<(string Name, string Reason)> unsupported)
    {
        var families = new Dictionary<string, (int Fragments, SortedSet<string> Content)>(StringComparer.Ordinal);
        foreach (var (name, reason) in unsupported)
        {
            foreach (var fragment in reason.Split('；', StringSplitOptions.RemoveEmptyEntries))
            {
                var key = Family(fragment);
                if (key.Length == 0)
                {
                    continue;
                }

                // 值元组是**值类型**：取出来必须显式初始化、改完再写回字典
                // （默认值的 Content 是 null，直接 Add 会抛 ArgumentNullException）。
                if (!families.TryGetValue(key, out var bucket))
                {
                    bucket = (0, new SortedSet<string>(StringComparer.Ordinal));
                }

                bucket.Fragments++;
                bucket.Content.Add(name);
                families[key] = bucket;
            }
        }

        var array = new JsonArray();
        foreach (var (family, bucket) in families
                     .OrderByDescending(item => item.Value.Content.Count)
                     .ThenBy(item => item.Key, StringComparer.Ordinal))
        {
            array.Add(new JsonObject
            {
                ["family"] = family,
                ["blocked_content"] = bucket.Content.Count,
                ["reason_fragments"] = bucket.Fragments,
                ["content"] = new JsonArray(bucket.Content.Take(20)
                    .Select(name => (JsonNode)JsonValue.Create(name)).ToArray()),
            });
        }

        return array;
    }

    /// <summary>把一条原因片段归成一个机制族键：去掉成员路径前缀、去掉具体取值与结尾的"未支持"。</summary>
    private static string Family(string fragment)
    {
        var text = DottedPrefix.Replace(fragment.Trim(), string.Empty);
        var cut = text.IndexOfAny(['\'', '"', '（', '(', '：']);
        if (cut >= 0)
        {
            text = text[..cut];
        }

        text = text.Trim().TrimEnd('的').Trim();
        if (text.EndsWith("未支持", StringComparison.Ordinal))
        {
            text = text[..^"未支持".Length].Trim();
        }

        return text;
    }

    private static string TopCategory(IReadOnlyList<VanillaEntry> entries)
    {
        var top = entries.GroupBy(entry => entry.Category, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault();
        return top is null ? "-" : $"{top.Key} {top.Count()}";
    }

    private static string Unqualified(string id)
    {
        var colon = id.IndexOf(':');
        return colon >= 0 ? id[(colon + 1)..] : id;
    }

    // -- Markdown 摘要 -------------------------------------------------------------------------------------

    private static string Markdown(JsonObject inventory)
    {
        var builder = new StringBuilder();
        var vanilla = inventory["vanilla"]!.AsObject();
        var matching = inventory["matching"]!.AsObject();
        builder.AppendLine("# ENV-00 inventory（自动生成）");
        builder.AppendLine();
        builder.AppendLine($"- 产物版本：`{inventory["inventory_version"]}`");
        builder.AppendLine($"- 生成命令：`dotnet run --project tools/differential -- inventory --output <dir>`");
        builder.AppendLine($"- 原版分母根目录：`{vanilla["root"]}`");
        builder.AppendLine($"- 分母（未排除）：**{vanilla["denominator_total"]}**；"
            + $"排除：{vanilla["excluded_total"]}；待核对：{vanilla["pending_source_check_total"]}");
        builder.AppendLine();
        builder.AppendLine("## 基线摘要（未提交目录用自己的摘要，不用父 Git HEAD）");
        builder.AppendLine();
        builder.AppendLine("| 路径 | 种类 | 存在 | 文件 | 字节 | sha256 |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | --- |");
        foreach (var node in inventory["baselines"]!.AsArray())
        {
            var item = node!.AsObject();
            var sha = item["sha256"]!.GetValue<string>();
            builder.AppendLine($"| `{item["path"]}` | {item["kind"]} | {item["exists"]} | {item["files"]} | "
                + $"{item["bytes"]} | `{(sha.Length > 20 ? sha[..20] + "…" : sha)}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## 原版分母类别");
        builder.AppendLine();
        builder.AppendLine("分母 = `in_scope` + `pending_source_check`（`pending_source_check` 只是"
            + "「可达性尚未核对」，仍计入分母；`excluded` 才不计入，且逐条登记了理由）。");
        builder.AppendLine();
        builder.AppendLine("| 类别 | 合计 | in_scope | 待核对 | 排除 |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var node in vanilla["categories"]!.AsArray())
        {
            var item = node!.AsObject();
            builder.AppendLine($"| {item["category"]} | {item["total"]} | {item["in_scope"]} | "
                + $"{item["pending_source_check"]} | {item["excluded"]} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 与原版分母的连接（只说明同名声明，不说明行为一致）");
        builder.AppendLine();
        builder.AppendLine("| 类别 | 分母 | Legacy 同名 | 正式已降级 | 未降级 | 连接键 |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | --- |");
        foreach (var node in matching["categories"]!.AsArray())
        {
            var item = node!.AsObject();
            builder.AppendLine($"| {item["category"]} | {item["denominator"]} | {Cell(item["matched_legacy"])} | "
                + $"{Cell(item["matched_formal_lowered"])} | {item["unmatched_formal_total"]} | {item["join_rule"]} |");
        }

        builder.AppendLine();
        builder.AppendLine("## 只在产物中、尚未产生的列");
        builder.AppendLine();
        foreach (var (key, value) in inventory["columns"]!.AsObject())
        {
            builder.AppendLine($"- `{key}` = `{value}`");
        }

        builder.AppendLine();
        builder.AppendLine("## 下一批");
        builder.AppendLine();
        foreach (var node in inventory["next_batch"]!.AsArray())
        {
            builder.AppendLine($"- {node!.GetValue<string>()}");
        }

        return builder.ToString();
    }

    /// <summary>表格单元：<c>null</c> 必须显示成 <c>n/a</c>，不能显示成空白（空白会被读成 0）。</summary>
    private static string Cell(JsonNode? node) =>
        node is null || node.GetValueKind() == JsonValueKind.Null ? "n/a" : node.ToString();
}
