using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Differential;

/// <summary>
/// ENV-01 的随机**交叉核对**：用参考实现（<c>reference/sts2-sim</c>，同一份原版源码的独立移植）
/// 重算 <c>tools/rng-oracle</c> 生成的神谕向量。
///
/// 为什么需要它：core 的检查是"我们的实现 vs 神谕"，而神谕本身就是手写的 Python。
/// 如果神谕读错了原版，检查会**一起错**。参考实现是第三份独立实现，用来证明神谕没有读偏；
/// 但它仍然不是权威——真正的裁决依据是只读的 <c>sts2/</c> 源码（ENV §6.3）。
/// </summary>
internal static class ReferenceRngCrossCheck
{
    /// <summary>
    /// 已登记的**参考偏离**（ENV §6.3：参考错误/版本不同 → 保留最小复现与源码判别检查，不照抄）。
    /// 只在两侧都有源码定位、且我们以 <c>sts2/</c> 为准时才能登记；未登记的偏离会被判成失败。
    /// </summary>
    private static readonly (string Id, string Vanilla, string Reference, string Note)[] DeclaredDeviations =
    [
        ("run-seed-derivation",
            "sts2/src/Core/Runs/RunRngSet.cs:104-119 —— `Seed` 是 **uint**，"
            + "`Seed = (uint)StringHelper.GetDeterministicHashCode(seed)`（int 的 352654597 循环）",
            "reference/sts2-sim/src/Sts2Sim.Core/Runs/RunRngSet.cs:36,80 —— `Seed` 是 **ulong**，"
            + "`Seed = StringHelper.GetDeterministicHashCode(seed)` 且该函数在本检出里返回 **XxHash64**"
            + "（旧实现被改名为 GetDeterministicHashCodeOld）",
            "参考检出已同步到比冻结基线更新的上游。run/流种子派生不是同一版本，"
            + "**不能**用它对齐我们的实现，也不能据此修改 VanillaRandomLayout；"
            + "ENV-00 需要把参考冻结到与 sts2/ 同一版本，或逐处登记此类差异。"),
        ("stream-seed-derivation",
            "sts2/src/Core/Random/Rng.cs:52-55 —— `seed + (uint)GetDeterministicHashCode(name)`（uint 回绕）",
            "reference/sts2-sim/src/Sts2Sim.Core/Random/Rng.cs:48-53 —— `seed + GetDeterministicHashCode(name)`，"
            + "两侧都是 **ulong**；`RunRngSet.cs:176-182` 的 `CreateRng` 在默认构造下走 `new Rng(Seed, name)`"
            + "（**顺序**路径），只有显式 `CreateKeyed` 才走 `Rng.CreateSemanticKeyRequired`（重键）",
            "差异是**哈希宽度/算法与相加宽度**（uint ↔ ulong / int 循环 ↔ XxHash64），"
            + "**不是**重键：`RunRngSet(string)` 的默认路径仍是 `usesSemanticKeys=false` 的顺序保真路径"
            + "（该文件注释与 `UsesSemanticKeys` 属性都写明；ENV-T04 源随机包已实测核对）。"
            + "仍需冻结到同一版本或登记窄源版本 Profile 才可用于对齐；重键路径明确禁止用于保真。"),
    ];

#if REFERENCE_SIM
    public static bool Available => true;

    public static int Run(string oracleVectorsPath, string outputDirectory, string? oracleLayoutPath = null,
        string? sourceCompatPath = null)
    {
        if (!File.Exists(oracleVectorsPath))
        {
            Console.Error.WriteLine($"找不到神谕向量：{oracleVectorsPath}");
            return 2;
        }

        var oracle = JsonNode.Parse(File.ReadAllText(oracleVectorsPath))!.AsObject();
        var seeds = oracle["seeds"]!.AsObject();
        var cases = new JsonArray();
        var mismatches = 0;

        foreach (var (key, node) in seeds.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var expected = node!.AsObject();
            var seed = ulong.Parse(key, System.Globalization.CultureInfo.InvariantCulture);
            var differences = new JsonArray();

            void Compare(string field, IEnumerable<string> expectedValues, IEnumerable<string> actualValues)
            {
                var left = expectedValues.ToList();
                var right = actualValues.ToList();
                for (var index = 0; index < Math.Min(left.Count, right.Count); index++)
                {
                    if (left[index] != right[index])
                    {
                        differences.Add(new JsonObject
                        {
                            ["field"] = field,
                            ["index"] = index,
                            ["oracle"] = left[index],
                            ["reference"] = right[index],
                        });
                    }
                }

                if (left.Count != right.Count)
                {
                    differences.Add(new JsonObject
                    {
                        ["field"] = field,
                        ["index"] = -1,
                        ["oracle"] = $"count={left.Count}",
                        ["reference"] = $"count={right.Count}",
                    });
                }
            }

            Compare("next_int_100",
                expected["next_int_100_first8"]!.AsArray().Select(value => value!.GetValue<int>().ToString()),
                Numbers(seed, rng => rng.NextInt(100), 8));

            Compare("next_int_1_7",
                expected["next_int_1_7_first8"]!.AsArray().Select(value => value!.GetValue<int>().ToString()),
                Numbers(seed, rng => rng.NextInt(1, 7), 8));

            Compare("next_double",
                expected["next_double_first4"]!.AsArray().Select(value => value!.GetValue<string>()),
                Doubles(seed, 4));

            var expectedDeck = expected["shuffle_10"]!.AsArray()
                .Select(value => value!.GetValue<string>()).ToList();
            var deck = Enumerable.Range(0, expectedDeck.Count).Select(index => $"card-{index:00}").ToList();
            var shuffler = new Sts2Sim.Core.Random.Rng(seed);
            shuffler.Shuffle(deck);
            Compare("shuffle_10", expectedDeck, deck);
            Compare("shuffle_10_counter",
                [expected["shuffle_10_counter"]!.GetValue<int>().ToString()],
                [shuffler.Counter.ToString()]);

            if (differences.Count > 0)
            {
                mismatches++;
            }

            cases.Add(new JsonObject
            {
                ["seed"] = key,
                ["agree"] = differences.Count == 0,
                ["differences"] = differences,
            });
        }

        var report = new JsonObject
        {
            ["crosscheck_version"] = "env-01-rng-crosscheck@1",
            ["oracle_file"] = oracleVectorsPath,
            ["oracle_layout_file"] = oracleLayoutPath,
            ["reference_root"] = "reference/sts2-sim",
            ["note"] = "原始字（raw_first8）没有参与核对：原版的 Rng 没有暴露整字入口，"
                + "该层只由 core 检查对神谕逐值核对。其余（有界映射/双精度/洗牌/消耗计数）三份实现互证。",
            ["counts"] = new JsonObject
            {
                ["seeds"] = cases.Count,
                ["agree"] = cases.Count - mismatches,
                ["mismatch"] = mismatches,
            },
            ["cases"] = cases,
            ["declared_deviations"] = new JsonArray(DeclaredDeviations.Select(item => (JsonNode)new JsonObject
            {
                ["id"] = item.Id,
                ["vanilla"] = item.Vanilla,
                ["reference"] = item.Reference,
                ["note"] = item.Note,
            }).ToArray()),
        };

        var layoutMismatches = 0;
        var unexplainedLayoutMismatches = 0;
        if (oracleLayoutPath is not null)
        {
            var layoutReport = CrossCheckLayout(oracleLayoutPath);
            layoutMismatches = layoutReport.Mismatches;
            unexplainedLayoutMismatches = layoutReport.Unexplained;
            report["layout"] = layoutReport.Json;
        }

        // ENV-T04：窄源版本兼容 Profile 的**独立源向量**核对（每个涉及流的 seed/32 位回绕/抽样调用）。
        // 它不是双侧比较：这里证的是"源派生 + 参考侧 Profile + 你我两侧的首次洗牌都与独立神谕一致"。
        JsonObject? compatReport = null;
        var compatFailures = 0;
        if (sourceCompatPath is not null)
        {
            compatReport = CrossCheckSourceCompat(sourceCompatPath);
            compatFailures = compatReport["failures"]!.GetValue<int>();
            report["source_compat"] = compatReport;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputDirectory))!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "rng-crosscheck.json"),
            report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

        Console.WriteLine($"rng crosscheck written: {Path.Combine(directory, "rng-crosscheck.json")}");
        Console.WriteLine($"  读数向量：种子 {cases.Count}，一致 {cases.Count - mismatches}，不一致 {mismatches}");
        if (oracleLayoutPath is not null)
        {
            Console.WriteLine($"  布局向量：不一致 {layoutMismatches}"
                + $"（其中已登记为参考偏离 {layoutMismatches - unexplainedLayoutMismatches}，"
                + $"未登记 {unexplainedLayoutMismatches}）");
        }

        if (compatReport is not null)
        {
            Console.WriteLine($"  源版本Profile向量：检查 {compatReport["checks"]!.GetValue<int>()}，"
                + $"不通过 {compatFailures}");
            foreach (var failure in compatReport["failure_detail"]!.AsArray())
            {
                Console.WriteLine($"    {failure!.GetValue<string>()}");
            }
        }

        foreach (var item in cases)
        {
            var caseNode = item!.AsObject();
            if (caseNode["agree"]!.GetValue<bool>())
            {
                continue;
            }

            foreach (var difference in caseNode["differences"]!.AsArray())
            {
                var entry = difference!.AsObject();
                Console.WriteLine($"  seed {caseNode["seed"]} {entry["field"]}[{entry["index"]}]: "
                    + $"oracle={entry["oracle"]} reference={entry["reference"]}");
            }
        }

        // 已登记的参考偏离不算失败；未登记的不一致必须失败（否则"参考又变了"会被静默吞掉）。
        return mismatches == 0 && unexplainedLayoutMismatches == 0 && compatFailures == 0 ? 0 : 1;
    }

    /// <summary>
    /// 布局层交叉核对：字符串 seed → run seed / 流种子的**数值**，以及按该布局抽一次的结果。
    /// 不一致时按已登记的偏离分类；没有登记依据的不一致算失败。
    /// </summary>
    private static (JsonObject Json, int Mismatches, int Unexplained) CrossCheckLayout(string oracleLayoutPath)
    {
        if (!File.Exists(oracleLayoutPath))
        {
            return (new JsonObject { ["error"] = $"找不到布局神谕向量：{oracleLayoutPath}" }, 0, 0);
        }

        var oracle = JsonNode.Parse(File.ReadAllText(oracleLayoutPath))!.AsObject();
        var cases = new JsonArray();
        var mismatches = 0;
        var unexplained = 0;

        foreach (var (key, node) in oracle["seeds"]!.AsObject().OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var expected = node!.AsObject();
            var expectedRunSeed = expected["run_seed"]!.GetValue<uint>();
            var run = new Sts2Sim.Core.Runs.RunRngSet(key);
            var expectedShuffle = expected["shuffle_stream_first4"]!.AsArray()[0]!.GetValue<uint>();
            var actualShuffle = (uint)run.Shuffle.NextInt(100);

            var agreement = new JsonArray();
            var deviations = new JsonArray();
            if (run.Seed == expectedRunSeed)
            {
                agreement.Add("run_seed");
            }
            else
            {
                deviations.Add(new JsonObject
                {
                    ["field"] = "run_seed",
                    ["oracle"] = expectedRunSeed,
                    ["reference"] = run.Seed,
                    ["deviation"] = "run-seed-derivation",
                });
            }

            if (actualShuffle == expectedShuffle)
            {
                agreement.Add("shuffle_first_draw");
            }
            else
            {
                deviations.Add(new JsonObject
                {
                    ["field"] = "shuffle_first_draw",
                    ["oracle"] = expectedShuffle,
                    ["reference"] = actualShuffle,
                    ["deviation"] = "stream-seed-derivation",
                });
            }

            var caseUnexplained = 0;
            if (deviations.Count > 0)
            {
                mismatches++;
                foreach (var deviation in deviations)
                {
                    var id = deviation!.AsObject()["deviation"]!.GetValue<string>();
                    var declared = DeclaredDeviations.Any(item => item.Id == id);
                    deviation.AsObject()["declared"] = declared;
                    if (!declared)
                    {
                        caseUnexplained++;
                    }
                }

                unexplained += caseUnexplained;
            }

            cases.Add(new JsonObject
            {
                ["string_seed"] = key,
                ["agree_on"] = agreement,
                ["deviations"] = deviations,
                ["unexplained"] = caseUnexplained,
            });
        }

        return (new JsonObject
        {
            ["oracle_file"] = oracleLayoutPath,
            ["note"] = "布局层的参考偏离**已登记**（run-seed-derivation / stream-seed-derivation）："
                + "参考检出领先于冻结基线。判据是只读的 sts2/，不是参考。",
            ["strings"] = cases.Count,
            ["mismatch"] = mismatches,
            ["unexplained"] = unexplained,
            ["cases"] = cases,
        }, mismatches, unexplained);
    }

    /// <summary>
    /// **ENV-T04 窄源版本兼容 Profile 的独立源向量核对**。
    ///
    /// 它把三件事接起来，任何一环读错都会失败：
    /// <list type="number">
    /// <item>独立神谕（<c>tools/rng-oracle</c> 的纯 Python 重写，只按只读 `sts2/` 源码）算出的
    ///       run seed / 12 条具名流种子 / uint 回绕 / `old` 前缀解释；</item>
    /// <item>工具层的 <see cref="RngSourceCompat"/>（另一份独立重写，**不调用** base 的 `VanillaRandomLayout`）；</item>
    /// <item>**参考自己的** `RunRngSet` 在装上 Profile 后真的洗一次 10 张起始牌组（`Rng.Shuffle` + `MegaRandom`）。</item>
    /// </list>
    /// <para>
    /// 另外做三条判别性检查，防止"Profile 其实是空操作"或"路径其实是重键"这类假通过：
    /// 参考**原生**（不装 Profile）的流种子必须与源版本不同；`RunRngSet(string)` 必须是顺序路径；
    /// `old` 前缀在源与参考下必须给出不同的 run seed。
    /// </para>
    /// <para>
    /// 它不是双侧比较：base 侧只按**已落盘的 base 记录**核对首次洗牌结果与计数（存在才核）。
    /// </para>
    /// </summary>
    private static JsonObject CrossCheckSourceCompat(string path)
    {
        var checks = 0;
        var failures = new List<string>();
        var failureDetail = new JsonArray();
        var areas = new JsonArray();

        void Expect(string area, string what, string expected, string actual)
        {
            checks++;
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                return;
            }

            failures.Add($"{area}: {what} 期望 {expected}，实际 {actual}");
        }

        if (!File.Exists(path))
        {
            checks++;
            failures.Add($"找不到源版本 Profile 向量：{path}"
                + "（生成：python tools/rng-oracle/vanilla_rng_oracle.py --source-compat <路径>）");
            return new JsonObject
            {
                ["oracle_file"] = path,
                ["checks"] = checks,
                ["failures"] = failures.Count,
                ["failure_detail"] = new JsonArray(failures.Select(item => JsonValue.Create(item)).ToArray()),
                ["areas"] = areas,
            };
        }

        var oracle = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var stringSeed = oracle["string_seed"]!.GetValue<string>();

        // (1) 派生：神谕 ↔ 工具层独立重写。
        var runSeed = RngSourceCompat.RunSeed(stringSeed);
        Expect("derivation", "run_seed", oracle["run_seed"]!.GetValue<uint>().ToString(CultureInfo.InvariantCulture),
            runSeed.ToString(CultureInfo.InvariantCulture));
        Expect("derivation", "profile_id", oracle["profile_id"]!.GetValue<string>(), RngSourceCompat.ProfileId);

        var nameMismatch = 0;
        var oracleStreams = oracle["streams"]!.AsObject();
        var engineNames = oracle["stream_engine_names"]!.AsObject();
        foreach (var (type, seedNode) in oracleStreams.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var expected = seedNode!.GetValue<uint>();
            var actual = RngSourceCompat.StreamSeed(runSeed, type);
            checks++;
            if (expected != actual)
            {
                failures.Add($"derivation: 流 {type} 的种子 期望 {expected}，实际 {actual}");
            }

            checks++;
            if (RngSourceCompat.EngineStreamName(type) != engineNames[type]!.GetValue<string>())
            {
                nameMismatch++;
                failures.Add($"mapping: 流 {type} 的引擎名与神谕不一致");
            }
        }

        var playerSeed = oracle["player_seed"]!.GetValue<uint>();
        Expect("derivation", "player_seed", playerSeed.ToString(CultureInfo.InvariantCulture),
            RngSourceCompat.PlayerSeed(stringSeed, 0).ToString(CultureInfo.InvariantCulture));

        // 玩家槽位的 3 条玩家级流也要逐条核对：ENV 要求"所有已声明流"有向量，不能只核 run 级。
        var oraclePlayerStreams = oracle["player_streams"]!.AsObject();
        var shimPlayerStreams = RngSourceCompat.PlayerStreams(stringSeed, 0)
            .ToDictionary(item => item.Type, item => item.Seed, StringComparer.Ordinal);
        checks++;
        if (shimPlayerStreams.Count != oraclePlayerStreams.Count)
        {
            failures.Add($"derivation: 玩家级流的条数 期望 {oraclePlayerStreams.Count}，实际 {shimPlayerStreams.Count}");
        }

        foreach (var (type, seedNode) in oraclePlayerStreams.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var expected = seedNode!.GetValue<uint>();
            checks++;
            if (!shimPlayerStreams.TryGetValue(type, out var actual))
            {
                failures.Add($"derivation: 玩家级流 {type} 在实现里不存在");
                continue;
            }

            if (expected != actual)
            {
                failures.Add($"derivation: 玩家级流 {type} 的种子 期望 {expected}，实际 {actual}");
            }
        }

        // 工具层自己的抽样：用**布局算出的** Shuffle 流种子洗 10 张起始牌组（独立 xoshiro256** 重写）。
        var firstShuffle = oracle["first_shuffle"]!.AsObject();
        var expectedDeck = firstShuffle["deck_after"]!.AsArray().Select(item => item!.GetValue<string>()).ToList();
        var expectedDraws = firstShuffle["draws"]!.GetValue<int>();
        var shuffleSeed = oracleStreams["Shuffle"]!.GetValue<uint>();
        var (shimDeck, shimDraws) = RngSourceCompat.ShuffleDeck(shuffleSeed, RngSourceCompat.IroncladStartingDeck);
        Expect("shim-shuffle", "deck_after", string.Join(",", expectedDeck), string.Join(",", shimDeck));
        Expect("shim-shuffle", "draws", expectedDraws.ToString(CultureInfo.InvariantCulture),
            shimDraws.ToString(CultureInfo.InvariantCulture));

        // (2) 参考侧：真的装 Profile 并用自己的 Rng.Shuffle 洗同一副牌。
        var rng = new Sts2Sim.Core.Runs.RunRngSet(stringSeed);
        Expect("reference", "usesSemanticKeys（默认构造必须是顺序路径）", "False",
            rng.UsesSemanticKeys.ToString());
        var applied = RngSourceCompat.Apply(rng, stringSeed);
        Expect("reference", "装载的流的条数", RngSourceCompat.RunStreamTypes.Count.ToString(CultureInfo.InvariantCulture),
            applied.Count.ToString(CultureInfo.InvariantCulture));

        var referenceDeck = RngSourceCompat.IroncladStartingDeck.ToList();
        var referenceShuffle = rng.GetRng(Sts2Sim.Core.Entities.Rngs.RunRngType.Shuffle);
        // 初始状态判别必须在**任何抽取之前**取样：装好 Profile、counter 仍为 0 的 `Serializable`
        // 状态才是「参考的初始状态」。洗牌后的指纹属于 9 次抽取之后的路径结果，拿它与原生
        // counter=0 比会得出「两侧初始状态不同」这个错误结论（2026-10-08 负责人指出的窄证据错误）。
        var profileInitial = referenceShuffle.ToSerializable();
        var profileCounter0Fingerprint = RngSourceCompat.FingerprintText(
            profileInitial.state0, profileInitial.state1, profileInitial.state2, profileInitial.state3);
        Expect("reference", "装上 Profile 后 Shuffle 流的起始抽取计数", "0",
            profileInitial.counter.ToString(CultureInfo.InvariantCulture));
        referenceShuffle.Shuffle(referenceDeck);
        Expect("reference", "首次洗牌 deck_after", string.Join(",", expectedDeck), string.Join(",", referenceDeck));
        Expect("reference", "首次洗牌后的抽取计数", expectedDraws.ToString(CultureInfo.InvariantCulture),
            referenceShuffle.Counter.ToString(CultureInfo.InvariantCulture));
        var shuffleState = referenceShuffle.ToSerializable();
        var referenceFingerprint = RngSourceCompat.FingerprintText(
            shuffleState.state0, shuffleState.state1, shuffleState.state2, shuffleState.state3);

        // (3) 判别性检查：Profile 不能是空操作，路径不能是重键，old 前缀必须可区分。
        // 初始状态判别在**同一时点**取样：两边都取 counter=0 的 `Serializable` 状态。
        // `Rng` 不公开 seed（只有 `ToSerializable` 的 state/counter），所以这是唯一可比对的初始状态。
        var native = new Sts2Sim.Core.Runs.RunRngSet(stringSeed);
        var nativeInitial = native.GetRng(Sts2Sim.Core.Entities.Rngs.RunRngType.Shuffle).ToSerializable();
        var nativeCounter0Fingerprint = RngSourceCompat.FingerprintText(
            nativeInitial.state0, nativeInitial.state1, nativeInitial.state2, nativeInitial.state3);
        checks++;
        if (nativeInitial.counter != 0 || profileInitial.counter != 0)
        {
            failures.Add("discriminator: 初始状态取样时点不是 counter=0（原生 "
                + $"{nativeInitial.counter}、Profile {profileInitial.counter}）——该判别无意义");
        }

        checks++;
        if (nativeCounter0Fingerprint == profileCounter0Fingerprint)
        {
            failures.Add("discriminator: 两侧 counter=0 的 Shuffle 流初始状态相同——Profile 是空操作，检查不判别");
        }

        // 抽取结果也判别：另装一份 Profile，两侧各从 counter=0 抽一次，首抽必须不同。
        var freshProfile = new Sts2Sim.Core.Runs.RunRngSet(stringSeed);
        RngSourceCompat.Apply(freshProfile, stringSeed);
        var nativeFirstDraw = native.GetRng(Sts2Sim.Core.Entities.Rngs.RunRngType.Shuffle).NextInt(1000);
        var profileFirstDraw = freshProfile.GetRng(Sts2Sim.Core.Entities.Rngs.RunRngType.Shuffle).NextInt(1000);
        checks++;
        if (nativeFirstDraw == profileFirstDraw)
        {
            failures.Add("discriminator: 参考原生默认与源版本 Profile 的 Shuffle 首次抽取相同——检查不判别");
        }

        var keyed = Sts2Sim.Core.Runs.RunRngSet.CreateKeyed(stringSeed);
        Expect("discriminator", "CreateKeyed 是语义重键", "True", keyed.UsesSemanticKeys.ToString());

        // keyed 路径必须被**拒绝**（不是静默重键）：Apply 对语义重键的集合直接抛错。
        var keyedRejected = false;
        try
        {
            RngSourceCompat.Apply(Sts2Sim.Core.Runs.RunRngSet.CreateKeyed(stringSeed), stringSeed);
        }
        catch (InvalidOperationException)
        {
            keyedRejected = true;
        }

        checks++;
        if (!keyedRejected)
        {
            failures.Add("discriminator: Apply 接受了语义重键（keyed）集合——会静默重键，检查不判别");
        }

        // 已经抽过的流也必须被拒绝：换种子等于掩盖已发生的抽取（静默分歧）。
        var consumedRejected = false;
        try
        {
            var consumed = new Sts2Sim.Core.Runs.RunRngSet(stringSeed);
            consumed.GetRng(Sts2Sim.Core.Entities.Rngs.RunRngType.Niche).NextInt(100);
            RngSourceCompat.Apply(consumed, stringSeed);
        }
        catch (InvalidOperationException)
        {
            consumedRejected = true;
        }

        checks++;
        if (!consumedRejected)
        {
            failures.Add("discriminator: Apply 接受了已经抽过的流——会掩盖已发生的抽取，检查不判别");
        }

        var oldPrefix = oracle["old_prefix"]!.AsObject();
        var oldSeed = oldPrefix["string_seed"]!.GetValue<string>();
        Expect("discriminator", "old 前缀的源 run seed",
            oldPrefix["source_run_seed"]!.GetValue<uint>().ToString(CultureInfo.InvariantCulture),
            RngSourceCompat.RunSeed(oldSeed).ToString(CultureInfo.InvariantCulture));
        Expect("discriminator", "old 前缀的参考原生 run seed",
            oldPrefix["reference_native_run_seed"]!.GetValue<uint>().ToString(CultureInfo.InvariantCulture),
            new Sts2Sim.Core.Runs.RunRngSet(oldSeed).Seed.ToString(CultureInfo.InvariantCulture));

        // (4) base 侧：按**已落盘的 base 记录**核对同一副牌的首次洗牌与计数（存在才核）。
        var baseRecordPath = Path.Combine(Repo.Root(),
            "artifacts", "environment", "tests", "regression", "records", "base", "rng-initial-shuffle.json");
        string? baseShuffle = null;
        var baseDraws = -1;
        if (File.Exists(baseRecordPath))
        {
            var baseRecord = JsonNode.Parse(File.ReadAllText(baseRecordPath))!.AsObject();
            var boundary = baseRecord["boundaries"]!.AsArray()[0]!.AsObject();
            var facts = boundary["facts"]!.AsArray()
                .Select(item => item!.AsObject())
                .ToDictionary(item => item["path"]!.GetValue<string>(), item => item["value"]!.GetValue<string>(),
                    StringComparer.Ordinal);
            string Deck(string key) => string.Join(",", facts[$"pile.{key}.order"].Split(',', StringSplitOptions.None)
                .Select(card => card.Replace("base-spire:", string.Empty, StringComparison.Ordinal)));
            baseShuffle = Deck("hand") + "," + Deck("draw");
            baseDraws = boundary["streams"]!.AsArray()
                .Select(item => item!.AsObject())
                .FirstOrDefault(item => item["stream"]!.GetValue<string>() == "base-spire:combat-draw")!
                ["draws"]!.GetValue<int>();
            Expect("base-record", "首次洗牌 deck_after（hand+draw）", string.Join(",", expectedDeck), baseShuffle);
            Expect("base-record", "base-spire:combat-draw 抽取计数",
                expectedDraws.ToString(CultureInfo.InvariantCulture), baseDraws.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            checks++;
            failures.Add($"base-record: 缺少 base 记录 {baseRecordPath}：本地的 base⇄神谕 首次洗牌互证未做");
        }

        areas.Add(new JsonObject
        {
            ["profile_id"] = RngSourceCompat.ProfileId,
            ["string_seed"] = stringSeed,
            ["source_run_seed"] = runSeed,
            ["shuffle_stream_seed"] = shuffleSeed,
            ["shuffle_engine_stream"] = RngSourceCompat.EngineStreamName("Shuffle"),
            ["shuffle_draws"] = expectedDraws,
            ["shuffle_deck_after"] = string.Join(",", expectedDeck),
            ["shuffle_fingerprint"] = referenceFingerprint,
            ["shuffle_fingerprint_scope"] = $"洗牌 {expectedDraws} 次之后（counter={expectedDraws}）的路径结果指纹，"
                + "不是初始状态",
            ["native_counter0_fingerprint"] = nativeCounter0Fingerprint,
            ["profile_counter0_fingerprint"] = profileCounter0Fingerprint,
            ["counter0_states_equal"] = nativeCounter0Fingerprint == profileCounter0Fingerprint,
            ["native_counter0_first_draw"] = nativeFirstDraw,
            ["profile_counter0_first_draw"] = profileFirstDraw,
            ["keyed_apply_rejected"] = keyedRejected,
            ["consumed_stream_apply_rejected"] = consumedRejected,
            ["stream_names_mismatch"] = nameMismatch,
            ["bases"] = new JsonArray("oracle", "tool-profile", "reference-with-profile", "base-record"),
            ["base_record"] = baseRecordPath is { } recordPath && File.Exists(recordPath)
                ? JsonValue.Create(recordPath)
                : null,
            ["base_shuffle_deck"] = baseShuffle is null ? null : JsonValue.Create(baseShuffle),
            ["base_shuffle_draws"] = baseDraws < 0 ? null : JsonValue.Create(baseDraws),
        });

        return new JsonObject
        {
            ["oracle_file"] = path,
            ["note"] = "独立源向量核对（不是双侧比较）：神谕 ↔ 工具层 Profile ↔ 装上 Profile 的参考 ↔ base 记录。"
                + "base 侧只核首次洗牌结果与计数；不据此宣称 T04 的双侧随机比较通过。"
                + "初始状态判别取两侧 **counter=0** 的 Serializable 指纹与首抽；"
                + "shuffle_fingerprint 是洗牌 9 次之后（counter=9）的路径结果，两者不可互比。",
            ["checks"] = checks,
            ["failures"] = failures.Count,
            ["failure_detail"] = new JsonArray(failures.Select(item => JsonValue.Create(item)).ToArray()),
            ["areas"] = areas,
        };
    }

    private static IEnumerable<string> Numbers(ulong seed, Func<Sts2Sim.Core.Random.Rng, int> draw, int count)
    {
        var rng = new Sts2Sim.Core.Random.Rng(seed);
        for (var index = 0; index < count; index++)
        {
            yield return draw(rng).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static IEnumerable<string> Doubles(ulong seed, int count)
    {
        var rng = new Sts2Sim.Core.Random.Rng(seed);
        for (var index = 0; index < count; index++)
        {
            yield return rng.NextDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
#else
    public static bool Available => false;

    public static int Run(string oracleVectorsPath, string outputDirectory, string? oracleLayoutPath = null,
        string? sourceCompatPath = null)
    {
        Console.Error.WriteLine("本机构建未包含参考侧适配器（未找到 reference/sts2-sim）：无法交叉核对。");
        return 3;
    }
#endif
}
