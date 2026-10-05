using System.Globalization;

namespace CoreTheSpire.Engine.Tests;

/// <summary>
/// ENV-01：**原版兼容随机 Profile** 的已知向量对照。
///
/// 向量由 <c>tools/rng-oracle/vanilla_rng_oracle.py</c> 生成——那是只读 `sts2/src/Core/Random/MegaRandom.cs`
/// 与 `Rng.cs` 之后的**独立重写**（Python），不参考 C# 侧任何实现，因此这条检查是"实现 vs 独立神谕"，
/// 不是"同一份代码自己撞自己"。参考模拟器的同算法实现只作交叉核对，不作为权威。
///
/// 这里同时钉住三件容易悄悄漂移的事：
/// <list type="number">
/// <item>位生成器（xoshiro256** + SplitMix64 初始化，含 **uint 种子的零扩展**）；</item>
/// <item>有界映射（原版是 `(int)(NextDouble() * span) + min` 的**截断**，不是拒绝采样）；</item>
/// <item>抽取代价（原版 <c>Rng.Counter</c> 每次调用 +1；10 张牌的 Fisher-Yates 恰好 9 次）。</item>
/// </list>
/// </summary>
internal static class VanillaRandomChecks
{
    private const string Stream = "vanilla";

    public static void Run()
    {
        RunVanillaProfileMatchesOracle();
        RunVanillaProfileRejectsMixedIdentity();
        RunDefaultProfileIsNotTheVanillaMapping();
        RunStreamSeedMustPrecedeFirstDraw();
        RunObservationDoesNotAdvanceVanillaStreams();
    }

    private static RandomService Vanilla(ulong seed)
    {
        var service = new RandomService(seed, RandomService.VanillaAlgorithmId, RandomService.VanillaDrawProfileId);
        service.SeedStream(Stream, seed);
        return service;
    }

    private static void RunVanillaProfileMatchesOracle()
    {
        Check.Run("ENV-01 原版 Profile：原始字/有界映射/双精度/洗牌与独立神谕逐值一致 [真实]", () =>
        {
            foreach (var vector in VanillaRandomVectors.All)
            {
                var raw = Vanilla(vector.Seed);
                for (var index = 0; index < vector.RawFirst8.Length; index++)
                {
                    Check.Equal(vector.RawFirst8[index], raw.DrawUInt64(Stream),
                        $"seed {vector.Seed} 第 {index} 个原始字");
                }

                var bounded = Vanilla(vector.Seed);
                for (var index = 0; index < vector.NextInt100First8.Length; index++)
                {
                    Check.Equal(vector.NextInt100First8[index], bounded.DrawIndex(Stream, 100),
                        $"seed {vector.Seed} 第 {index} 次 NextInt(100)");
                }

                var ranged = Vanilla(vector.Seed);
                for (var index = 0; index < vector.NextInt1To7First8.Length; index++)
                {
                    Check.Equal(vector.NextInt1To7First8[index], ranged.DrawInt(Stream, 1, 7),
                        $"seed {vector.Seed} 第 {index} 次 NextInt(1, 7)");
                }

                var doubles = Vanilla(vector.Seed);
                foreach (var expected in vector.NextDoubleFirst4)
                {
                    Check.Equal(double.Parse(expected, CultureInfo.InvariantCulture), doubles.DrawDouble(Stream),
                        $"seed {vector.Seed} 的 NextDouble");
                }

                // 洗牌：排列与消耗计数都要对（原版 Counter 每次调用 +1，10 张牌 9 次）。
                var shuffler = Vanilla(vector.Seed);
                var deck = Enumerable.Range(0, vector.Shuffle10.Length)
                    .Select(index => $"card-{index:00}").ToList();
                shuffler.Shuffle(Stream, deck);
                Check.Sequence(vector.Shuffle10, deck, $"seed {vector.Seed} 的洗牌排列");
                Check.Equal(vector.Shuffle10Counter, (int)shuffler.DrawCount(Stream),
                    $"seed {vector.Seed} 的洗牌消耗计数");
            }
        });
    }

    private static void RunVanillaProfileRejectsMixedIdentity()
    {
        Check.Run("ENV-01 原版 Profile：算法与映射必须成对，混搭被显式拒绝（不静默降级）", () =>
        {
            Check.Fault(EngineFaultCode.Unsupported,
                () => _ = new RandomService(1UL, RandomService.VanillaAlgorithmId,
                    RandomService.DefaultDrawProfileId),
                "原版算法 + 默认映射必须被拒绝");
            Check.Fault(EngineFaultCode.Unsupported,
                () => _ = new RandomService(1UL, RandomService.DefaultAlgorithmId,
                    RandomService.VanillaDrawProfileId),
                "默认算法 + 原版映射必须被拒绝");
            Check.Fault(EngineFaultCode.Unsupported,
                () => _ = new RandomService(1UL, "not-an-algorithm", RandomService.VanillaDrawProfileId),
                "未知算法必须被拒绝");
            Check.True(new RandomService(1UL, RandomService.VanillaAlgorithmId,
                RandomService.VanillaDrawProfileId).IsVanillaCompatible,
                "成对的 Profile 应当被接受并标记为原版兼容");
            Check.False(new RandomService(1UL).IsVanillaCompatible, "默认 Profile 不得自我标称为原版兼容");
        });
    }

    private static void RunDefaultProfileIsNotTheVanillaMapping()
    {
        Check.Run("ENV-01 原版 Profile：与默认 Profile 是两套不同抽样（同种子读数不同，避免被当成等价）", () =>
        {
            var differences = 0;
            foreach (var vector in VanillaRandomVectors.All)
            {
                var vanilla = Vanilla(vector.Seed);
                var standard = new RandomService(vector.Seed);
                standard.SeedStream(Stream, vector.Seed);
                for (var index = 0; index < 16; index++)
                {
                    if (vanilla.DrawIndex(Stream, 100) != standard.DrawIndex(Stream, 100))
                    {
                        differences++;
                    }
                }
            }

            Check.True(differences > 0,
                "两套 Profile 在 80 次抽样里必须至少有一次不同；完全相同意味着其中一套是死代码");
        });
    }

    private static void RunStreamSeedMustPrecedeFirstDraw()
    {
        Check.Run("ENV-01 原版 Profile：流种子只能在首次抽取前显式设置（抽取后再换种子是静默分歧）", () =>
        {
            var service = new RandomService(7UL, RandomService.VanillaAlgorithmId,
                RandomService.VanillaDrawProfileId);
            service.SeedStream(Stream, 12345UL);
            var first = service.DrawUInt64(Stream);
            Check.Fault(EngineFaultCode.Unsupported, () => service.SeedStream(Stream, 999UL),
                "已抽取过的流不得改种子");

            // 与"直接用同一个种子建服务"必须一致：显式种子没有被别的派生规则污染。
            var rebuilt = Vanilla(12345UL);
            Check.Equal(rebuilt.DrawUInt64(Stream), first, "显式种子与种子参数应当等价");
        });
    }

    private static void RunObservationDoesNotAdvanceVanillaStreams()
    {
        Check.Run("ENV-01 原版 Profile：观察（DrawCount/Diagnostics/Capture）不消费随机数", () =>
        {
            var service = Vanilla(42UL);
            service.DrawInt(Stream, 0, 10);
            var before = service.DrawCount(Stream);
            _ = service.Diagnostics();
            _ = service.Capture();
            _ = service.DrawCount(Stream);
            Check.Equal(before, service.DrawCount(Stream), "观察后抽取计数不得变化");

            var expected = Vanilla(42UL);
            expected.DrawInt(Stream, 0, 10);
            Check.Equal(expected.DrawInt(Stream, 0, 10), service.DrawInt(Stream, 0, 10),
                "观察之后的下一次读数必须与从未观察过一致");
        });
    }
}
