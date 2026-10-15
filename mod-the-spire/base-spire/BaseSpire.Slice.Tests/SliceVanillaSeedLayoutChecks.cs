using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-01（base 侧）：**字符串 seed → run/player/内容流种子**的布局照抄原版。
///
/// 向量由 <c>tools/rng-oracle/vanilla_rng_oracle.py</c> 生成——那是只读 `sts2/` 之后的独立重写，
/// 不参考任何 C# 实现。检查分三层：命名层（SnakeCase）、哈希层（seed 数值）、**端到端**（按布局算出的
/// `Shuffle` 流种子真的抽出来的读数/洗牌排列），任何一层读错都会在这里露出来。
///
/// 注意：**不要**拿 `reference/sts2-sim` 来"对齐"这里的数值——它的 `RunRngSet` 已经换成了
/// ulong + XxHash64（`Runs/RunRngSet.cs:36,80`），与冻结基线不是同一版本。
/// </summary>
internal static class SliceVanillaSeedLayoutChecks
{
    private const string Stream = "check";

    public static void Run()
    {
        RunSnakeCaseMatchesOracle();
        RunSeedLayoutMatchesOracle();
        RunShuffleStreamEndToEnd();
        RunSeedStreamRequiresVanillaProfile();
    }

    private static void RunSnakeCaseMatchesOracle()
    {
        Check.Run("ENV-01 布局：流名字的 SnakeCase 与神谕一致（命名层）", () =>
        {
            foreach (var (input, expected) in VanillaSeedLayoutVectors.SnakeCases)
            {
                Check.Equal(expected, VanillaRandomLayout.SnakeCase(input), $"SnakeCase('{input}')");
            }

            // 枚举成员集合本身也要对上：少一个流就是少一条随机序列。
            Check.Sequence(VanillaRandomLayout.RunStreamTypes.Order(),
                VanillaSeedLayoutVectors.Layout[0].RunStreams.Select(item => item.Type).Order(),
                "RunRngType 成员集合");
            Check.Sequence(VanillaRandomLayout.PlayerStreamTypes.Order(),
                VanillaSeedLayoutVectors.Layout[0].PlayerStreams.Select(item => item.Type).Order(),
                "PlayerRngType 成员集合");
        });
    }

    private static void RunSeedLayoutMatchesOracle()
    {
        Check.Run("ENV-01 布局：run/player/内容流种子与神谕逐值一致（哈希层）", () =>
        {
            foreach (var vector in VanillaSeedLayoutVectors.Layout)
            {
                Check.Equal(vector.RunSeed, VanillaRandomLayout.RunSeed(vector.StringSeed),
                    $"RunSeed('{vector.StringSeed}')");
                Check.Equal(vector.PlayerSeed, VanillaRandomLayout.PlayerSeed(vector.StringSeed, 0),
                    $"PlayerSeed('{vector.StringSeed}', 0)");

                var streams = VanillaRandomLayout.RunStreams(vector.StringSeed)
                    .ToDictionary(item => item.Type, item => item.Seed);
                foreach (var (type, seed) in vector.RunStreams)
                {
                    Check.Equal(seed, streams[type], $"'{vector.StringSeed}' 的 run 流 {type}");
                }

                var playerStreams = VanillaRandomLayout.PlayerStreams(vector.StringSeed, 0)
                    .ToDictionary(item => item.Type, item => item.Seed);
                foreach (var (type, seed) in vector.PlayerStreams)
                {
                    Check.Equal(seed, playerStreams[type], $"'{vector.StringSeed}' 的玩家流 {type}");
                }
            }

            var runSeed = VanillaRandomLayout.RunSeed("ENV-IRONCLAD-001");
            Check.Equal(VanillaSeedLayoutVectors.RunSeedOfEnvIronclad001, runSeed,
                "ENV-IRONCLAD-001 的 run seed");
            foreach (var content in VanillaSeedLayoutVectors.Content)
            {
                Check.Equal(content.Seed,
                    VanillaRandomLayout.ContentSeed(runSeed, content.Slot, content.Entry),
                    $"内容流 {content.Entry}");
            }
        });
    }

    private static void RunShuffleStreamEndToEnd()
    {
        Check.Run("ENV-01 布局：按布局种出来的 Shuffle 流，读数与洗牌与神谕一致（端到端）", () =>
        {
            foreach (var vector in VanillaSeedLayoutVectors.Layout)
            {
                var service = new RandomService(vector.RunSeed, VanillaRandomLayout.AlgorithmId,
                    VanillaRandomLayout.DrawProfileId, VanillaRandomLayout.Profile);
                VanillaRandomLayout.SeedStream(service, Stream, vector.StringSeed, "Shuffle");

                foreach (var expected in vector.ShuffleStreamFirst4)
                {
                    Check.Equal((int)expected, service.DrawIndex(Stream, 100),
                        $"'{vector.StringSeed}' 的 Shuffle 流读数");
                }

                var deck = Enumerable.Range(0, vector.ShuffleStreamDeck.Length)
                    .Select(index => $"card-{index:00}").ToList();
                service.Shuffle(Stream, deck);
                Check.Sequence(vector.ShuffleStreamDeck, deck, $"'{vector.StringSeed}' 的 Shuffle 洗牌排列");
                // 4 次读数 + 10 张牌的 Fisher-Yates(9 次) = 13：抽取代价也要与原版 Counter 对上。
                Check.Equal(vector.ShuffleStreamCounter, (int)service.DrawCount(Stream),
                    $"'{vector.StringSeed}' 的 Shuffle 流抽取计数");
            }
        });
    }

    private static void RunSeedStreamRequiresVanillaProfile()
    {
        Check.Run("ENV-01 布局：非原版 Profile 上套用原版布局被拒绝（不静默分歧）", () =>
        {
            var standard = new RandomService(1UL);
            Check.False(standard.IsVanillaCompatible, "默认服务不应自称原版兼容");
            try
            {
                VanillaRandomLayout.SeedStream(standard, Stream, "ENV-IRONCLAD-001", "Shuffle");
                throw new CheckFailedException("在默认 Profile 上种原版流必须被拒绝，但它没有");
            }
            catch (InvalidOperationException)
            {
                // 期望：拒绝而不是静默按默认派生规则种下去。
            }
        });
    }
}
