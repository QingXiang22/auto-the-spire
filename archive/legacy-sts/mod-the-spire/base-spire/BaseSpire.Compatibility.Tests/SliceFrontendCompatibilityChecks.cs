using BaseSpire.Slice;
using CoreTheSpire;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// B-05c 第十三批：**保留 0.0.1 兼容入口**（开工清单 §14 B-05c 行的明文要求）的显式验证。
///
/// 两套作者前端在同一进程/同一构建里共存，各读各的文件：
/// <list type="bullet">
/// <item>旧 <c>.spire</c>（0.0.1，`Core.TheSpire/Legacy`）：`BaseSpireContent` 加载 17 个内容源，注册出原版内容；</item>
/// <item>切片作者文本（`Slice/Content/*.author`，编译到通用 IR）：`SliceAuthorFrontend` 加载 4 卡/4 能力/2 敌人。</item>
/// </list>
/// 判据：两者都能加载；**互不静默吞掉对方的语法**（旧语法片段被新前端带路径拒绝，反之亦然）；
/// 同进程内先跑旧后端再跑新前端也不互相污染。
/// </summary>
internal static class SliceFrontendCompatibilityChecks
{
    /// <summary>
    /// 旧 0.0.1 语法的代表片段：**逐字取自冻结内容源** `ContentSource/status-cards.spire`
    /// （具名卡、`normal`/`upgraded` 块、`target = { selectSelf = {} }`、`keywords`、`hooks`）。
    /// 不用自造语法：兼容性结论必须基于真实存在的旧语法。
    /// </summary>
    private const string LegacySnippet = """
        using namespace base-spire;

        card Dazed {
            upgradeable = false
            normal = {
                type = status
                rarity = status
                cost = 0
                target = { selectSelf = {} }
                keywords = { ethereal unplayable }
                play = {}
                hooks = {}
            }
            upgraded = {
                type = status
                rarity = status
                cost = 0
                target = { selectSelf = {} }
                keywords = { ethereal unplayable }
                play = {}
                hooks = {}
            }
        }
        """;
    public static void Run()
    {
        Check.Run("B-05c 兼容入口：旧 0.0.1 内容仍能加载并注册出原版内容", () =>
        {
            var cards = BaseSpireContent.LoadCards();
            Check.Equal(96, cards.Count, "旧内容源的卡牌数（与 B-05a 冻结清单一致）");
            Check.Equal(1, BaseSpireContent.LoadCardPools().Count, "卡池");

            var registry = new ContentRegistry();
            registry.AddPackage(new BaseSpirePackage());
            var game = registry.Build();
            Check.Equal(96, game.Cards.Count, "注册后的卡牌数");
            Check.Equal(89, game.Enemies.Count, "注册后的敌人数");

            // 代表性核验：一张旧卡的升级形态来自旧语法（不是切片作者文本）。
            var strike = game.Cards[BaseSpireIds.StrikeIronclad];
            Check.Equal(1, strike.Cost, "打击费用 1（来自旧 .spire 语法）");
            Check.Equal(CardType.Attack, strike.Type, "打击是攻击牌");
            Check.True(strike.HasUpgrade, "旧卡仍有升级形态");
        });

        Check.Run("B-05c 兼容入口：切片作者文本仍能加载（两套前端同进程共存）", () =>
        {
            // 先跑一遍旧后端（上一条检查的副作用留在进程里），再加载新前端。
            _ = BaseSpireContent.LoadCards();
            var model = SliceAuthorFrontend.Load();
            Check.Equal(SliceManifest.Cards.Count + SliceFixtures.AuthoredBeyondSlice.Count, model.Cards.Count,
                "切片作者文本的卡牌数（切片 + B-06a 已迁内容）");
            Check.Equal(2, model.Enemies.Count, "敌人");
            Check.Equal("combat-slice-v1", model.Version, "切片版本");

            var builder = new RuleSetBuilder();
            builder.Add(CombatSlicePackage.Build(fromAuthorText: true).Build());
            var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole]);
            session.Start();
            Check.Equal(SessionStatus.Running, session.Status, "新前端构建的切片能开局");
        });

        Check.Run("B-05c 兼容入口：新前端不吞旧语法（带路径拒绝），旧解析器读得懂旧语法", () =>
        {
            // 旧解析器：接受旧语法片段。
            var parsed = DslSourceParser.Parse(LegacySnippet, "compatibility.spire");
            var cards = SpireCombatDslCompiler.CompileCards(parsed);
            Check.Equal(1, cards.Count, "旧解析器编译出 1 张卡");

            // 新前端：**必须拒绝**旧语法（否则两套语法会被混读，内容会静默错位）。
            try
            {
                SliceAuthorFrontend.Parse(LegacySnippet, "compatibility.spire");
                Check.True(false, "切片作者前端必须拒绝旧 .spire 语法");
            }
            catch (SliceAuthorError failure)
            {
                Check.True(failure.Code.Length > 0 && failure.Path.Length > 0,
                    $"拒绝必须带错误码与路径：{failure.Message}");
            }

            // 反向：旧解析器读不懂切片作者文本（两套语法是分开的，不是互相兼容的方言）。
            var authorText = ReadAuthorText();
            Check.True(Rejects(authorText), "旧解析器必须拒绝切片作者文本");
        });

        Check.Run("B-05c 兼容入口：两套前端读的是不同文件，路径与扩展名不重叠", () =>
        {
            var contentSources = typeof(BaseSpireContent).Assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith("BaseSpire.ContentSource.", StringComparison.Ordinal)).ToList();
            var authorSources = typeof(SliceAuthorFrontend).Assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith("BaseSpire.SliceAuthor.", StringComparison.Ordinal)).ToList();

            Check.True(contentSources.Count > 0, $"旧内容源：{contentSources.Count} 个");
            Check.Sequence([SliceAuthorFrontend.ResourceName], authorSources, "新前端只有一个作者文本资源");
            Check.False(contentSources.Intersect(authorSources, StringComparer.Ordinal).Any(),
                "两套资源名不重叠");
            Check.True(contentSources.All(name => name.EndsWith(".spire", StringComparison.Ordinal)),
                "旧内容源都是 .spire");
            Check.True(authorSources.All(name => name.EndsWith(".author", StringComparison.Ordinal)),
                "切片作者文本是 .author（扩展名不同，落点也不同）");
        });
    }

    private static string ReadAuthorText()
    {
        using var stream = typeof(SliceAuthorFrontend).Assembly
            .GetManifestResourceStream(SliceAuthorFrontend.ResourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static bool Rejects(string text)
    {
        try
        {
            var parsed = DslSourceParser.Parse(text, "slice-content.author");
            _ = SpireCombatDslCompiler.CompileCards(parsed);
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
