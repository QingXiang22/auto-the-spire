using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-T06-source-stable-shuffle-20261012：源 `StableShuffle` 预排序的判别检查。
///
/// 预期值来源（都不复用被测实现）：
/// <list type="bullet">
/// <item>排序键口径：只读 <c>sts2/src/Core/Helpers/StringHelper.cs:23-28</c>（`Slugify`）、
///       <c>ModelDb.GetEntry/GetCategoryType</c>、<c>ModelId.CompareTo</c>、
///       <c>CardModel.CompareTo</c>（<c>Id</c> → <c>CurrentUpgradeLevel</c>）；</item>
/// <item>源排序**置换**：`List&lt;T&gt;.Sort()`（内省排序）在源比较器下的结果，由本包 C# 探针独立取得
///       （<c>.audit/dsh-source-stable-shuffle-20261012/probe-output.txt</c>），不是从生产代码推的；</item>
/// <item>洗牌后的牌序：只读 <c>ListExtensions.UnstableShuffle</c> + <c>Rng.NextInt</c> 的独立 Python 复算
///       （<c>.audit/dsh-source-stable-shuffle-20261012/verify-shuffle-vectors.txt</c>），种子/游标取 ENV-T04
///       受控重洗 case（字符串 seed <c>ENV-T04-CTRL-random-reshuffle-unsupported</c> ⇒ `Shuffle` 流
///       <c>3072570454</c>、游标 0）。</item>
/// </list>
/// </summary>
internal static class SourceStableShuffleChecks
{
    /// <summary>ENV-T04 受控重洗 case 的字符串 seed（与正式路径同一句 `SeedAllStreams`）。</summary>
    private const string EnvT04GameSeed = "ENV-T04-CTRL-random-reshuffle-unsupported";

    private const string StrikeId = "base-spire:strike-ironclad";
    private const string DefendId = "base-spire:defend-ironclad";
    private const string BashId = "base-spire:bash";
    private const string ShrugId = "base-spire:shrug-it-off";
    private const string ArmamentsId = "base-spire:armaments";
    private const string PlayCardId = "base-spire:PlayCard";

    /// <summary>10 张起始牌堆的**源排序**（弃牌堆顺序下标）：BASH → 4×DEFEND → 5×STRIKE。</summary>
    private static readonly int[] Sorted10 = [9, 5, 6, 7, 8, 0, 1, 2, 3, 4];

    /// <summary>上述输入经 `Shuffle` 流（`3072570454`，游标 0）降序 Fisher–Yates 后的顺序。</summary>
    private static readonly int[] Shuffled10 = [4, 5, 6, 8, 1, 0, 9, 3, 7, 2];

    /// <summary>17 张**同键**实例的源排序置换（.NET 内省排序；与稳定排序不同）。</summary>
    private static readonly int[] Sorted17 = [0, 14, 13, 12, 11, 10, 9, 15, 8, 6, 5, 4, 3, 2, 1, 7, 16];

    /// <summary>上述输入经同一条流（游标 0）洗牌后的顺序。</summary>
    private static readonly int[] Shuffled17 = [15, 5, 12, 1, 16, 6, 0, 11, 10, 8, 3, 13, 4, 14, 7, 9, 2];

    public static void Run()
    {
        Check.Run("SourceStableShuffle: 排序键口径（Slugify / Entry@等级 / 序数比较）", () =>
        {
            Check.Equal("STRIKE_IRONCLAD", SourceStableShuffle.Slugify("StrikeIronclad"), "源类名 → ModelId.Entry");
            Check.Equal("DEFEND_IRONCLAD", SourceStableShuffle.Slugify("DefendIronclad"), "源类名 → ModelId.Entry");
            Check.Equal("EXPECT_A_FIGHT", SourceStableShuffle.Slugify("ExpectAFight"), "连续大写逐段断开");
            Check.Equal("I_AM_INVINCIBLE", SourceStableShuffle.Slugify("IAmInvincible"), "连续大写逐段断开");

            // 内容 id 的逆向转写**不是**源 Entry 的恒等还原（连续大写结尾会丢词边界）：
            // 因此实例上的键取自**声明名**，内容 id 只在没有声明名时作为回退。
            Check.Equal("A_B", SourceStableShuffle.Slugify("AB"), "源 Slugify 在词尾连续大写处仍断开");
            Check.Equal("AB", SourceStableShuffle.FallbackEntry("base-spire:ab"), "内容 id 逆向只剩一个词");

            Check.Equal("BASH@0", SourceStableShuffle.Key("BASH", upgraded: false), "普通形态键");
            Check.Equal("BASH@1", SourceStableShuffle.Key("BASH", upgraded: true), "升级形态键");
            Check.Equal(("BASH", 1), SourceStableShuffle.Parse("BASH@1"), "键可解析回 (Entry, 等级)");
            Check.True(SourceStableShuffle.Compare(("BASH", 0), ("DEFEND_IRONCLAD", 0)) < 0, "先比 Entry");
            Check.True(SourceStableShuffle.Compare(("STRIKE_IRONCLAD", 0), ("STRIKE_IRONCLAD", 1)) < 0,
                "同 Entry 时普通实例在升级实例之前");
        });

        Check.Run("SourceStableShuffle: 源 List<T>.Sort 置换（含 17 张同键与 IR 逐步下标契约）", () =>
        {
            var starter = new List<string>();
            for (var index = 0; index < 5; index++) starter.Add(SourceStableShuffle.Key("STRIKE_IRONCLAD", false));
            for (var index = 0; index < 4; index++) starter.Add(SourceStableShuffle.Key("DEFEND_IRONCLAD", false));
            starter.Add(SourceStableShuffle.Key("BASH", false));
            Check.Sequence(Sorted10, SourceStableShuffle.Order(starter),
                "10 张起始牌堆：BASH → DEFEND → STRIKE，同级保持原相对次序");

            Check.Sequence([2, 1, 0], SourceStableShuffle.Order(
                [
                    SourceStableShuffle.Key("STRIKE_IRONCLAD", true),
                    SourceStableShuffle.Key("STRIKE_IRONCLAD", false),
                    SourceStableShuffle.Key("BASH", false),
                ]), "同 Entry 的普通实例排在升级实例之前");

            var identical = Enumerable.Repeat(SourceStableShuffle.Key("STRIKE_IRONCLAD", false), 17).ToList();
            var order = SourceStableShuffle.Order(identical);
            Check.Sequence(Sorted17, order, "17 张同键：源内省排序置换");
            Check.False(order.SequenceEqual(Enumerable.Range(0, 17)), "该输入能判别'拿稳定排序冒名'的实现");

            // IR 契约：每一步的"当前下标" + 逐个移到末尾必须复刻同一个置换。
            var blob = SourceStableShuffle.Encode(identical);
            var remaining = Enumerable.Range(0, 17).ToList();
            var realized = new List<int>();
            for (var step = 0; step < 17; step++)
            {
                var index = SourceStableShuffle.NextIndex(blob, step);
                Check.True(index >= 0 && index < remaining.Count,
                    $"第 {step} 步下标 {index} 应落在 [0,{remaining.Count})");
                realized.Add(remaining[index]);
                remaining.RemoveAt(index);
            }

            Check.Sequence(order, realized, "逐步下标 + 移到末尾 = 源置换");
        });

        Check.Run("SourceStableShuffle: 卡实例排序键随创建/复制/升级路径一致（隐藏元数据）", () =>
        {
            var model = Model with
            {
                Deck =
                [
                    new AuthorDeckEntry(StrikeId, 1, Upgraded: true),
                    new AuthorDeckEntry(DefendId, 1),
                ],
            };
            var session = Start(model, seedStreams: false);
            var longTerm = Collection(session, "base-spire:LongTermDeck");
            Check.Equal("STRIKE_IRONCLAD@1", KeyOf(session, Member(session, longTerm, StrikeId)), "升级的长期实例键");
            Check.Equal("DEFEND_IRONCLAD@0", KeyOf(session, Member(session, longTerm, DefendId)), "普通的长期实例键");

            // 战斗实例由 `StartNextRoom` 的 `Copy` 铺出来（首回合会把它们抽进手牌）：键必须跟着复制。
            var combatKeys = new[] { "base-spire:DrawPile", "base-spire:Hand" }
                .SelectMany(name => session.State.CollectionMembers(Collection(session, name)))
                .Select(card => KeyOf(session, card))
                .Order(StringComparer.Ordinal);
            Check.Sequence(new[] { "DEFEND_IRONCLAD@0", "STRIKE_IRONCLAD@1" }, combatKeys,
                "战斗实例（复制路径）的键与长期实例的升级形态一致");

            // 运行时升级（Armaments 的 `upgradeChooseFromHand` 走 `CardDefinitions.Upgrade`）：键必须一起换。
            var upgradeModel = Model with
            {
                Deck = [new AuthorDeckEntry(StrikeId, 1), new AuthorDeckEntry(ArmamentsId, 1)],
            };
            var upgradeSession = Start(upgradeModel, seedStreams: false);
            var strike = FormalSliceFixture.MoveToHand(upgradeSession, StrikeId);
            Check.Equal("STRIKE_IRONCLAD@0", KeyOf(upgradeSession, strike), "升级前是普通键");
            SetEnergy(upgradeSession, 3);
            FormalSliceFixture.PlayAt(upgradeSession, FormalSliceFixture.MoveToHand(upgradeSession, ArmamentsId), strike);
            Healthy(upgradeSession);
            Check.True(Bool(upgradeSession, strike, "Upgraded"), "Armaments 真的升级了这张手牌");
            Check.Equal("STRIKE_IRONCLAD@1", KeyOf(upgradeSession, strike), "运行时升级同步源排序键");
        });

        Check.Run("SourceStableShuffle: 原 ENV-T04 10 张重洗按源预排序（首差消除，流抽数不变）", () =>
            ReshuffleCase(
                deck: [new AuthorDeckEntry(StrikeId, 5), new AuthorDeckEntry(DefendId, 4), new AuthorDeckEntry(BashId, 1)],
                cards: 10, sorted: Sorted10, shuffled: Shuffled10, label: "10 张起始牌堆"));

        Check.Run("SourceStableShuffle: 17 张同键实例重洗按源算法（>16 内省排序并列次序）", () =>
            ReshuffleCase(
                deck: [new AuthorDeckEntry(StrikeId, 17)],
                cards: 17, sorted: Sorted17, shuffled: Shuffled17, label: "17 张同键实例"));
    }

    /// <summary>
    /// 受控端到端重洗：牌组 = <paramref name="deck"/> + 1 张 ShrugItOff。起始发牌（每回合起手 5 张）之后，
    /// 把**全部实例按牌组顺序**放进弃牌堆（手牌里的先、抽牌堆里的后，两者的相对次序都等于牌组顺序）、
    /// ShrugItOff 留手里，然后打出它触发一次"抽牌堆空 → 重洗 + 抽 1 张"。
    /// 断言：实例排序键 → 源排序置换、源预排序 + 同一条 `Shuffle` 流给出的牌序（独立模型）、流消费条数不变。
    /// </summary>
    private static void ReshuffleCase(AuthorDeckEntry[] deck, int cards, int[] sorted, int[] shuffled, string label)
    {
        var model = Model with { Deck = [.. deck, new AuthorDeckEntry(ShrugId, 1)] };
        var session = Start(model, seedStreams: true);
        var hand = Collection(session, "base-spire:Hand");
        var draw = Collection(session, "base-spire:DrawPile");
        var discard = Collection(session, "base-spire:DiscardPile");

        var shrug = FormalSliceFixture.MoveToHand(session, ShrugId);
        Check.Equal(0L, session.Random.DrawCount(SliceManifest.DrawStream), $"{label}: 重洗前 `Shuffle` 流未消费");

        var order = new List<InstanceId>();
        session.State.RunAtomic(() =>
        {
            foreach (var pile in new[] { hand, draw })
            {
                foreach (var card in session.State.CollectionMembers(pile).ToList())
                {
                    if (card == shrug)
                    {
                        continue;
                    }

                    session.State.RemoveFromCollection(pile, card);
                    session.State.AddToCollection(discard, card);
                    order.Add(card);
                }
            }
        });
        Check.Equal(cards, order.Count, $"{label}: 弃牌堆就是全部实例（按牌组顺序）");

        // 实例上的键（弃牌堆顺序）经源算法必须给出模型里的源排序置换。
        Check.Sequence(sorted, SourceStableShuffle.Order([.. order.Select(card => KeyOf(session, card))]),
            $"{label}: 实例排序键 → 源排序置换");

        Play(session, shrug);
        Healthy(session);

        Check.Equal((long)(order.Count - 1), session.Random.DrawCount(SliceManifest.DrawStream),
            $"{label}: 重洗仍是 n-1 次抽取（同一条流、条数不变）");
        Check.Sequence([order[shuffled[0]]], session.State.CollectionMembers(hand),
            $"{label}: 抽到的实例 = 源预排序 + 同一条流的第 1 张");
        Check.Sequence(shuffled.Skip(1).Select(index => order[index]), session.State.CollectionMembers(draw),
            $"{label}: 剩余抽牌堆顺序 = 源预排序 + 同一条流");
    }

    private static SliceAuthorModel Model => FormalSpirePackage.Load().Model;

    /// <summary>
    /// 受控会话：与正式路径同一句配置（原版算法/映射/Profile）。`seedStreams` 打开时按 ENV-T04 的
    /// 字符串 seed 种全部原版线流（`Shuffle` 流因此是 <c>3072570454</c>、游标 0）。
    /// </summary>
    private static Session Start(SliceAuthorModel model, bool seedStreams)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: model)
            .Build());
        var session = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole],
            computeBackend: SourceWeightedPickBackend.Instance,
            randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
            randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
            randomProfile: VanillaRandomLayout.Profile);
        if (seedStreams)
        {
            VanillaRandomLayout.SeedAllStreams(session.Random, EnvT04GameSeed);
        }

        Check.True(session.Random.TotalDraws == 0, "建局不消费随机数（受控切片按声明顺序发牌）");
        session.Start();
        Healthy(session);
        return session;
    }

    private static int Collection(Session session, string name) => FormalSliceFixture.Collection(session, name);

    private static InstanceId Member(Session session, int collection, string definitionId) =>
        session.State.CollectionMembers(collection)
            .Single(card => FormalSliceFixture.Definition(session, card) == definitionId);

    private static string KeyOf(Session session, InstanceId card) => Text(session, card, "SourceOrderKey");

    private static string Text(Session session, InstanceId instance, string field) =>
        session.State.GetField(instance, session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId)
            .FieldIndex(field)).AsString();

    private static bool Bool(Session session, InstanceId instance, string field) =>
        session.State.GetField(instance, session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId)
            .FieldIndex(field)).AsBool();

    private static void SetEnergy(Session session, long amount) => session.State.RunAtomic(() =>
        session.State.SetField(FormalSliceFixture.Player(session),
            session.RuleSet.TypeByQualifiedId(FormalSliceFixture.PlayerTypeId).FieldIndex("Energy"), Value.Of(amount)));

    private static void Play(Session session, InstanceId card)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .Single(candidate => candidate.Definition.QualifiedId == PlayCardId);
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
        while (session.Status == SessionStatus.AwaitingDecision)
        {
            var pending = session.PendingDecision!;
            session.Answer(new DecisionAnswer(pending.RequestId, [pending.Candidates[0]]));
        }
    }

    private static void Healthy(Session session) => Check.True(session.Status != SessionStatus.Faulted, $"{session.Fault}");
}
