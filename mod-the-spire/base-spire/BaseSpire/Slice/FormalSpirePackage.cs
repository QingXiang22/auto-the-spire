using System.Text.RegularExpressions;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// ENV-02 的第一步：把**正式源降级结果**（而不是切片原型文本）组进一个**可运行的 Engine 包**。
///
/// 与切片原型的关系：非卡部分（敌人/药水/腰带/整局）沿用 <see cref="SliceAuthorFrontend.Load"/>，
/// **卡牌与能力整体换成正式源**——这正是"正式构建"与"夹具"的分界。与测试里的
/// `SliceFormalSourceChecks.BuildModel()` 的差别是：这里**不按行为证据过滤**，把全部降级结果都组进去，
/// 卡组是"每张降级卡各 1 张"，因此会话启动后每张降级卡都会有一个实例——"引擎到底加载了什么"
/// 于是可以从**会话状态**读出来（见 `tools/differential` 的 `engine_loaded`），而不是靠声明自证。
/// </summary>
public static class FormalSpirePackage
{
    /// <summary>这份构建的身份（与切片原型 `combat-slice-v1` 区分开：内容来源不同）。</summary>
    public const string Version = "formal-source-subset-v1";

    public sealed record Loaded(
        SliceAuthorModel Model,
        IReadOnlyList<string> CardIds,
        IReadOnlyList<string> PowerIds,
        IReadOnlyList<string> UnsupportedCards,
        IReadOnlyList<string> UnsupportedPowers,
        /// <summary>
        /// 只在**规范 id 表**（<see cref="SpirePowerSource.KnownIds"/>）下能链接、而在**已加载的能力源**下链接不上的卡。
        /// 这些卡的效果引用了包里根本没有的能力：按规范表链接会得到一个"存在但没加载"的能力 id，
        /// 因此这里宁可**记为未支持**，并把差异摆出来，而不是让它悄悄带一个悬空引用进运行时。
        /// </summary>
        IReadOnlyList<string> LinkOrphans,
        /// <summary>
        /// 能力名 → 引用它、并因此被记为未支持的卡。按引用数降序即"补哪个机制最划算"。
        /// 只统计**原因文本里点名的能力**，不做猜测。
        /// </summary>
        IReadOnlyList<(string Power, IReadOnlyList<string> Cards)> PowerDemand);

    /// <summary>
    /// 组装正式源的作者模型：能力来自共享的全部Power声明资源，卡牌来自全部 `*-cards.spire`，
    /// 链接表用**正式能力源自己的规范 id**（不手写）。
    /// </summary>
    public static Loaded Load()
    {
        var baseModel = SliceAuthorFrontend.Load();

        var powers = new List<AuthorPower>();
        var unsupportedPowers = new List<string>();
        foreach (var resource in SpirePowerSource.PowerDeclarationResources())
        {
            var source = SpirePowerSource.LoadResource(resource);
            powers.AddRange(source.Powers);
            unsupportedPowers.AddRange(source.Unsupported.Select(entry => entry.Name));
        }

        // 链接表：正式能力名 → 正式 id。卡牌里引用的能力名靠它解析成 id。
        var links = powers
            .GroupBy(power => power.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

        var cards = new List<AuthorCard>();
        var unsupportedCards = new List<string>();
        var linkOrphans = new List<string>();
        // 需求表：被卡住的原因里提到的能力名 → 引用它的卡。用来给"下一批补哪个机制"排序，
        // 而不是按卡名一张张猜。
        var powerDemand = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var resource in SpireCardSource.CardResources())
        {
            var source = SpireCardSource.LoadResource(resource, links);
            cards.AddRange(source.Cards);
            unsupportedCards.AddRange(source.Unsupported.Select(entry => entry.Name));
            foreach (var (cardName, reason) in source.Unsupported)
            {
                foreach (Match match in QuotedPower.Matches(reason))
                {
                    if (!powerDemand.TryGetValue(match.Groups[1].Value, out var users))
                    {
                        powerDemand[match.Groups[1].Value] = users = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    users.Add(cardName);
                }
            }

            // 同一份源换成规范 id 表再降级一次：只差在"链接表更宽"。
            var permissive = SpireCardSource.LoadResource(resource, SpirePowerSource.KnownIds);
            var strictIds = new HashSet<string>(source.Cards.Select(card => card.Id), StringComparer.Ordinal);
            foreach (var card in permissive.Cards.Where(card => !strictIds.Contains(card.Id)))
            {
                linkOrphans.Add(card.Name);
            }
        }

        var deck = cards
            .GroupBy(card => card.Id, StringComparer.Ordinal)
            .Select(group => new AuthorDeckEntry(group.Key, 1))
            .ToList();

        var model = baseModel with { Cards = cards, Powers = powers, Deck = deck };
        return new Loaded(
            model,
            [.. cards.Select(card => card.Id).Order(StringComparer.Ordinal)],
            [.. powers.Select(power => power.Id).Order(StringComparer.Ordinal)],
            [.. unsupportedCards.Order(StringComparer.Ordinal)],
            [.. unsupportedPowers.Order(StringComparer.Ordinal)],
            [.. linkOrphans.Order(StringComparer.Ordinal)],
            [.. powerDemand.OrderByDescending(pair => pair.Value.Count)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (pair.Key, (IReadOnlyList<string>)[.. pair.Value]))]);
    }

    /// <summary>原因文本里的 `'能力名'`（解析器自己点名的，不靠猜）。</summary>
    private static readonly Regex QuotedPower = new(@"'([^']+)'", RegexOptions.Compiled);

    /// <summary>用正式源组一个可运行的 Engine 包（与切片原型走同一条 IR/链接/加载路径）。</summary>
    public static EnginePackageBuilder Build(out Loaded loaded) =>
        CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: (loaded = Load()).Model);

    /// <summary>组包并链接成一个规则集（加载失败的构造问题会在这里直接抛出来）。</summary>
    public static RuleSet BuildRuleSet(out Loaded loaded)
    {
        var builder = new RuleSetBuilder();
        builder.Add(Build(out loaded).Build());
        return builder.Build();
    }
}
