using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

/// <summary>
/// ENV-T02 的判别检查：**战斗 → 整局的永久状态写回边界**。
///
/// 复现的两条缺陷都出在"结算时机"，不是"字段少拷了几个"：
///   ① `SettleBattle` 以前只写 `Run.Hp`：战斗里对**最大生命**的永久改动（Feed 的 `on_fatal_gain_max_hp`、
///      将来的最大生命牌）在换房时被 `StartNextRoom`（`player.MaxHp = Run.MaxHp`）反向抹掉；
///   ② 最后一次击杀发生在效果链**中途**（`DamageMonster` 内部就调 `VictoryTail` → 结算），
///      于是同一张牌里"结算之后"才跑的父效果余效（Feed 的 +3 最大生命/当前生命）根本没有机会进整局，
///      而战斗副本上看得见 +3 —— 这就是"最后击杀余效丢失"。
///
/// 修法是把"结算/胜利"从伤害步骤里挪出来，并让永久状态只有一个写回点：
///   * `CommitPermanent`（`Run.Hp` + `Run.MaxHp`）是唯一写回过程，`SettleBattle` 在同一间房的幂等守卫里调用它；
///   * 胜利判定移到**动作体末尾**的 `BattleEndTail`（`PlayCard`/`EndTurn`/`UsePotion` 各挂一次；
///     守卫用既有 `BattleSettled`，每个房间至多一次），最后击杀之后的余效因此一定先跑完；
///   * 效果步骤表里**不**再判胜利、也不再补提交——`BattleEndTail` 是动作的最后一条指令，
///     所以写回的一定是"全部效果 + 全部收尾"之后的完整战斗副本；
///   * 出牌收尾（`after_card_played` 钩子、`InPlay` 移除、`consumeOnPlay` 消耗、结果区放置）
///     同样因此在终局之前跑完（复核复现：两房夹具最后由 Feed 击杀时牌必须已离开 `InPlay`，
///     追加回血钩子应得 HP 60 而不是停在 53）。
/// 检查分成三组：
///   * 余效**进**整局（最后击杀、跨房、最后一间房）；
///   * 余效**不**提前进整局（战斗未结束就是战斗副本的事，不做实时镜像）；
///   * 写回集合**只有** Hp/MaxHp，长期牌/战斗副本的来源与矩阵不变。
///
/// 夹具用**正式源**（<see cref="FormalSliceFixture.NewFormal" />）：Feed 的 `on_fatal_gain_max_hp` 步骤
/// 只有正式卡源会产出（作者文本词表里没有这个字面步骤），所以这不是测试专用场景。
/// </summary>
internal static class SliceFrontendSettlementChecks
{
    private const string RunTypeId = "base-spire:Run";
    private const string RunStatesCollection = "base-spire:RunStates";
    private const string LongTermCollection = "base-spire:LongTermDeck";
    private const string MonstersCollection = "base-spire:Monsters";
    private const string FeedId = "base-spire:feed";

    /// <summary>正式内容里的声明值（`slice-content.author` 的 `run { hp = 80 max-hp = 80 gold = 99 }`）。</summary>
    private const long DeclaredHp = 80L;
    private const long DeclaredGold = 99L;

    /// <summary>Feed 的 `onFatal = { gainMaxHp = { amount = 3 } }`（`ironclad-cards.spire:4023`）。</summary>
    private const long FeedGain = 3L;

    public static void Run()
    {
        LastKillResidualEntersRun();
        ResidualDoesNotEnterRunBeforeSettlement();
        ResidualInLastRoomArrivesBeforeTerminal();
        WriteBackIsNotLiveMirroring();
        LongTermDeckIsAuthorityAndBattleCopiesAreFresh();
        PlayTailCompletesBeforeTerminal();
        BattleCopiesRecordTheirLongTermSource();
        ConditionalBranchCompletesBeforeTerminal();
        ExhaustReactionCompletesBeforeTerminal();
    }

    /// <summary>
    /// ① 最后击杀的父效果余效进整局，并且**跨房**（换房的反向覆盖不再抹掉最大生命）。
    /// 修复前：战斗副本 83/83，整局停在 80/80，第二间房被重置回 80/80。
    /// </summary>
    private static void LastKillResidualEntersRun()
    {
        Check.Run("ENV-T02 结算：最后击杀的父效果余效（Feed +3 最大生命）进整局并被下一间房继承 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var run = RunInstance(session);

            Check.Equal(1L, RunField(session, run, "CurrentRoom"), "开局即第一间房");
            Check.Equal(0L, RunField(session, run, "SettledRoom"), "开局尚未结算");
            Check.Equal(DeclaredHp, RunField(session, run, "MaxHp"), "整局最大生命来自声明（80）");

            // 先打死前排（后排还活着 ⇒ 战斗继续、不结算），再把 Feed 打在后排上：Feed 的伤害杀掉**最后一个**敌人，
            // 余效 `on_fatal_gain_max_hp` 是同一张牌里伤害之后的一步，胜利判定在步骤表末尾才跑 ⇒ 余效先落地。
            KillWith(session, FormalSliceFixture.Cultist(session, SliceManifest.CalcifiedCultistId),
                SliceManifest.StrikeId);
            Check.Equal(SessionStatus.Running, session.Status, "只死一个敌人 ⇒ 战斗继续");
            Check.Equal(0L, RunField(session, run, "SettledRoom"), "死一个不算结算");

            KillWith(session, FormalSliceFixture.Cultist(session, SliceManifest.DampCultistId), FeedId);

            var player = FormalSliceFixture.Player(session);
            Check.Equal(DeclaredHp + FeedGain,
                FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "MaxHp"),
                "战斗副本：Feed 杀了目标 ⇒ 最大生命 +3");
            Check.Equal(DeclaredHp + FeedGain,
                FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp"),
                "战斗副本：当前生命同时 +3");
            Check.Equal(1L, RunField(session, run, "SettledRoom"), "清房已结算（房间键置位）");
            Check.Equal(DeclaredHp + FeedGain, RunField(session, run, "MaxHp"),
                "**最后击杀的父效果余效进了整局**（修复前停在 80）");
            Check.Equal(DeclaredHp + FeedGain, RunField(session, run, "Hp"),
                "整局当前生命 = 余效之后的战斗值");
            Check.Equal(DeclaredGold, RunField(session, run, "Gold"), "战斗内没有金币来源 ⇒ 金币不动");

            // 跨房：`StartNextRoom` 用 Run 的 Hp/MaxHp 反向覆盖战斗副本 ⇒ 漏写回的话这里会是 80/80。
            EnterNextRoom(session);
            Check.Equal(2L, RunField(session, run, "CurrentRoom"), "已进入第二间房");
            var second = FormalSliceFixture.Player(session);
            Check.Equal(DeclaredHp + FeedGain,
                FormalSliceFixture.Number(session, second, FormalSliceFixture.PlayerTypeId, "MaxHp"),
                "第二间房的最大生命来自整局（83），不是声明初值 80");
            Check.Equal(DeclaredHp + FeedGain,
                FormalSliceFixture.Number(session, second, FormalSliceFixture.PlayerTypeId, "Hp"),
                "第二间房的当前生命来自整局（83）");
            Check.False(session.State.GetField(second,
                session.RuleSet.TypeByQualifiedId(FormalSliceFixture.PlayerTypeId).FieldIndex("BattleSettled")).AsBool(),
                "第二间房的结算守卫已重置");
            Check.Equal(DeclaredHp + FeedGain, RunField(session, run, "MaxHp"), "换房不改整局最大生命");
        });
    }

    /// <summary>
    /// ② 余效**不**提前进整局：战斗还没结束时，Feed 的永久改动只属于战斗副本（不做实时镜像）。
    /// 这条是上面那条的判别对照——写成"每次 Feed 都写回"会在这里失败。
    /// </summary>
    private static void ResidualDoesNotEnterRunBeforeSettlement()
    {
        Check.Run("ENV-T02 结算：战斗未结束时的 Feed 只改战斗副本，整局最大生命不动 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var run = RunInstance(session);

            // 用 Feed 打死**前排**（后排活着）⇒ 余效发生，但战斗远未结束。
            KillWith(session, FormalSliceFixture.Cultist(session, SliceManifest.CalcifiedCultistId), FeedId);

            var player = FormalSliceFixture.Player(session);
            Check.Equal(DeclaredHp + FeedGain,
                FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "MaxHp"),
                "战斗副本的最大生命已 +3");
            Check.Equal(SessionStatus.Running, session.Status, "还有一个敌人存活");
            Check.Equal(0L, RunField(session, run, "SettledRoom"), "尚未结算");
            Check.Equal(DeclaredHp, RunField(session, run, "MaxHp"),
                "整局最大生命**不动**（战斗未结束不写回）");
            Check.Equal(DeclaredHp, RunField(session, run, "Hp"), "整局当前生命同样不动");
        });
    }

    /// <summary>
    /// ③ 最后一间房的最后击杀：余效必须在**整局胜利终局之前**安排进整局
    /// （`docs/contracts/core-engine-baseline.md:21`：终局前应执行的游戏余效由游戏包安排）。
    /// 这里同时也是"配置房间数用尽 ⇒ 设 Victory"这条既有语义的回归。
    /// </summary>
    private static void ResidualInLastRoomArrivesBeforeTerminal()
    {
        Check.Run("ENV-T02 结算：最后一间房的最后击杀余效仍进整局（在胜利终局之前）[真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var run = RunInstance(session);

            ClearRoom(session);
            Check.Equal(1L, RunField(session, run, "SettledRoom"), "第一间房已结算");
            Check.Equal(DeclaredHp, RunField(session, run, "MaxHp"), "第一间房没用 Feed ⇒ 整局最大生命仍是 80");
            EnterNextRoom(session);

            // 第二间房就是配置里的最后一间（`SliceManifest.RunRoomCount = 2`）⇒ 清房同时会设整局胜利。
            KillWith(session, FormalSliceFixture.Cultist(session, SliceManifest.CalcifiedCultistId),
                SliceManifest.StrikeId);
            KillWith(session, FormalSliceFixture.Cultist(session, SliceManifest.DampCultistId), FeedId);

            Check.Equal(SessionStatus.Terminal, session.Status, "最后一间房清完 ⇒ 会话进入终局");
            Check.Equal("base-spire:Victory", session.Terminal!.Definition.QualifiedId, "终局是整局胜利");
            Check.Equal(DeclaredHp + FeedGain, RunField(session, run, "MaxHp"),
                "终局之前余效已经进整局（修复前会停在 80）");
            Check.Equal(FormalSliceFixture.Number(session, FormalSliceFixture.Player(session),
                    FormalSliceFixture.PlayerTypeId, "Hp"), RunField(session, run, "Hp"),
                "整局当前生命 = 余效之后的战斗值");
        });
    }

    /// <summary>
    /// ④ 写回不是实时镜像：战斗中途直接改战斗副本的永久字段，整局不动；结算那一刻才整体写回。
    /// （同时证明写回集合里**没有**金币等其它永久资源。）
    /// </summary>
    private static void WriteBackIsNotLiveMirroring()
    {
        Check.Run("ENV-T02 结算：整局只在结算时整体写回，不做实时镜像；写回集合只有 Hp/MaxHp [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var run = RunInstance(session);
            var player = FormalSliceFixture.Player(session);
            var playerType = session.RuleSet.TypeByQualifiedId(FormalSliceFixture.PlayerTypeId);

            // 战斗中直接抬高战斗副本的最大生命（模拟"战斗里得到的永久最大生命"）。
            session.State.RunAtomic(() => session.State.SetField(player, playerType.FieldIndex("MaxHp"),
                Value.Of(DeclaredHp + 7L)));
            Check.Equal(DeclaredHp, RunField(session, run, "MaxHp"), "战斗中整局不动（不实时镜像）");
            Check.Equal(DeclaredGold, RunField(session, run, "Gold"), "战斗不写金币");
            Check.Equal(0L, RunField(session, run, "RewardTaken"), "战斗不写奖励簿记");

            ClearRoom(session);
            Check.Equal(DeclaredHp + 7L, RunField(session, run, "MaxHp"),
                "结算那一刻把战斗副本的永久字段整体写回（87）");
            Check.Equal(FormalSliceFixture.Number(session, FormalSliceFixture.Player(session),
                    FormalSliceFixture.PlayerTypeId, "Hp"), RunField(session, run, "Hp"),
                "当前生命同样在结算那一刻整体写回");
            Check.Equal(DeclaredGold, RunField(session, run, "Gold"), "金币仍然不动");
            Check.Equal(1L, RunField(session, run, "CurrentRoom"), "房间号只由 EnterNextRoom 推进");
            Check.Equal(1L, RunField(session, run, "SettledRoom"), "结算守卫按房间置位");
        });
    }

    /// <summary>
    /// ⑤ 长期牌组是权威、战斗副本是每次换房重建的克隆（`CopyCardFields` 矩阵）：
    ///   * 战斗牌区与长期牌组的实例**不重叠**；
    ///   * 战斗内的临时费用修改不污染长期实例，也不进下一间房（矩阵里 `CostModifier` 归零）；
    ///   * 换房后战斗副本全部是新实例，长期牌组的成员身份跨房稳定。
    /// </summary>
    private static void LongTermDeckIsAuthorityAndBattleCopiesAreFresh()
    {
        Check.Run("ENV-T02 写回矩阵：长期牌组是权威，战斗副本换房重建且临时费用清零 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var cardType = session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
            var longTerm = FormalSliceFixture.Collection(session, LongTermCollection);
            var longMembers = session.State.CollectionMembers(longTerm).ToHashSet();
            Check.True(longMembers.Count > 0, "正式源每张卡各一张都在长期牌组里");

            var battle = BattleCards(session);
            Check.True(battle.Count > 0, "战斗牌区里有牌");
            Check.False(battle.Overlaps(longMembers), "战斗实例与长期实例不重叠（长期实例不直接进场）");

            // ENV-T02（复核修正）：每个战斗副本都记下自己的长期来源实例，且是**按实例**的一一对应。
            var origins = SourceMap(session);
            Check.Equal(longMembers.Count, origins.Count, "正式源：副本数与长期牌组基数一致");
            Check.True(origins.Values.All(longMembers.Contains), "每个副本的来源都是长期牌组成员");
            Check.Equal(origins.Count, origins.Values.Distinct().Count(), "不同副本的来源互不相同");
            Check.True(origins.Values.ToHashSet().SetEquals(longMembers), "来源集合恰好覆盖长期牌组");
            Check.Equal(Definition(session, origins.First(entry => Definition(session, entry.Key) == SliceManifest.StrikeId).Key),
                Definition(session, origins.First(entry => Definition(session, entry.Key) == SliceManifest.StrikeId).Value),
                "副本与来源是同一定义（拷贝不改定义，只建来源关系）");

            // 挑一张**非打击**的战斗牌（打击会在清房时被消耗掉），在战斗侧改临时费用。
            var mutated = session.State.CollectionMembers(FormalSliceFixture.Collection(session, FormalSliceFixture.DrawPile))
                .First(card => Definition(session, card) != SliceManifest.StrikeId);
            var definition = Definition(session, mutated);
            session.State.RunAtomic(() => session.State.SetField(mutated, cardType.FieldIndex("CostModifier"),
                Value.Of(3L)));
            var longTwin = longMembers.First(card => Definition(session, card) == definition);
            Check.Equal(0L, FormalSliceFixture.Number(session, longTwin, FormalSliceFixture.CardTypeId, "CostModifier"),
                "战斗内的临时费用修改**不**污染长期实例");
            Check.Equal(3L, FormalSliceFixture.Number(session, mutated, FormalSliceFixture.CardTypeId, "CostModifier"),
                "战斗副本自己确实带着临时费用修改");

            ClearRoom(session);
            EnterNextRoom(session);

            var secondBattle = BattleCards(session);
            Check.False(secondBattle.Contains(mutated), "第一间房那个被改过的战斗实例没有进入第二间房");
            Check.False(secondBattle.Overlaps(longMembers), "第二间房的副本仍然不是长期实例本身");
            Check.True(longMembers.SetEquals(session.State.CollectionMembers(longTerm)),
                "长期牌组的成员身份跨房稳定");

            var copy = session.State.CollectionMembers(FormalSliceFixture.Collection(session, FormalSliceFixture.DrawPile))
                .Concat(session.State.CollectionMembers(FormalSliceFixture.Collection(session, FormalSliceFixture.HandPile)))
                .FirstOrDefault(card => Definition(session, card) == definition);
            Check.True(copy != default, $"第二间房里应当有 '{definition}' 的副本");
            Check.Equal(0L, FormalSliceFixture.Number(session, copy, FormalSliceFixture.CardTypeId, "CostModifier"),
                "临时费用修正在换房时清零（`CopyCardFields` 矩阵）");
        });
    }

    /// <summary>
    /// ⑥ 复核复现：最后击杀发生在**出牌链中途**时，出牌收尾必须在终局之前跑完。
    /// 旧写法把胜利判定塞在效果步骤表尾部（`StepInstructions`），于是 `SetTerminal` 发生在
    /// `after_card_played` 钩子、`InPlay` 移除、消耗与结果区放置之前——复核探针里牌仍卡在 `InPlay`、
    /// 追加的回血钩子应得 60 却停在 53。这里用三个互不相关的观测量判别收尾是否跑完：
    ///   * 打出的 Feed 离开 `InPlay` 并进弃牌堆（收尾与结果区放置跑完）；
    ///   * `RagePower`（`trigger = afterCardPlayed`、`whenCardType = attack`、每层 +1 格挡）生效——钩子跑完；
    ///   * Feed 的 `on_fatal_gain_max_hp` +3 进整局——父效果余效跑完。
    /// </summary>
    private static void PlayTailCompletesBeforeTerminal()
    {
        Check.Run("ENV-T02 结算：最后击杀的出牌收尾（after_card_played 钩子、离开 InPlay）在终局之前跑完 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var run = RunInstance(session);
            var player = FormalSliceFixture.Player(session);

            // 第一间房全部用打击清掉，Feed 留给最后一间房（= 第二间房，`SliceManifest.RunRoomCount = 2`）。
            ClearRoom(session);
            EnterNextRoom(session);
            Check.Equal(2L, RunField(session, run, "CurrentRoom"), "已进入最后一间房");

            // 最后一间房有两只敌人：先用打击打死一只，留下的一只交给 Feed 收尾。
            KillWith(session, Monsters(session)[0], SliceManifest.StrikeId);
            Check.Equal(1, Monsters(session).Count, "最后一间房只剩一个敌人");

            // 出牌前才挂 RagePower：它的触发点就在出牌收尾里（`after_card_played`），
            // 因此它是否生效可以直接判别"收尾有没有被终局截断"。
            FormalSliceFixture.GivePower(session, player, "base-spire:rage-power", 1L);
            Check.Equal(0L, FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Block"),
                "出牌前没有格挡（受控前提）");

            var maxHpBefore = RunField(session, run, "MaxHp");
            var last = Monsters(session)[0];
            FormalSliceFixture.SetNumber(session, last, FormalSliceFixture.MonsterTypeId, "Hp", 5L);
            var feed = FormalSliceFixture.MoveToHand(session, FeedId);
            FormalSliceFixture.PlayAt(session, feed, last);

            Check.Equal(SessionStatus.Terminal, session.Status, "最后一间房的最后击杀 ⇒ 会话进入终局");
            Check.Equal("base-spire:Victory", session.Terminal!.Definition.QualifiedId, "终局是整局胜利");

            var inPlay = FormalSliceFixture.Collection(session, "base-spire:InPlay");
            Check.False(session.State.CollectionMembers(inPlay).Contains(feed),
                "打出的 Feed 已离开 `InPlay`（修复前会卡在 `InPlay`）");
            Check.True(session.State.CollectionMembers(
                    FormalSliceFixture.Collection(session, FormalSliceFixture.DiscardPile)).Contains(feed),
                "打出的 Feed 进了弃牌堆");
            Check.Equal(1L, FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Block"),
                "`after_card_played` 钩子在终局之前生效（RagePower ⇒ 1 格挡）");
            Check.Equal(maxHpBefore + FeedGain, RunField(session, run, "MaxHp"),
                "Feed 的父效果余效同样在终局之前进整局");
        });
    }

    /// <summary>
    /// ⑦ 复核复现（缺陷 B）：战斗副本必须记下自己是**哪一张长期实例**的副本，且"同定义不同实例的永久变化"互不影响。
    /// 正式源每张定义各只有一张牌，测不出"同定义不同实例"，因此本检查用**同一生产路径**的作者 `run` 场景
    /// （`deck { strike-ironclad x3 … }`：同定义 3 个实例）。
    /// 判别点：
    ///   * 副本 → 来源是**双射**（来源都是长期牌组成员、互不相同、恰好覆盖整个长期牌组）；
    ///   * 同定义的 3 张 Strike 的副本来源**互不相同**（按 `DefinitionId` 合并实例的实现会在这里失败）；
    ///   * 只升级其中**一张**长期实例 ⇒ 换房后恰好那一个来源的副本是升级形态，同定义的其它副本仍是普通形态；
    ///   * 第二间房的来源集合与第一间房相同（关系跨房稳定，不是按结果重匹配）。
    /// </summary>
    private static void BattleCopiesRecordTheirLongTermSource()
    {
        Check.Run("ENV-T02 写回矩阵：战斗副本按实例记录长期来源；同定义不同实例的永久变化互不影响 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFromAuthorText(SourceRelationScenarioText);
            var cardType = session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
            var longTerm = FormalSliceFixture.Collection(session, LongTermCollection);
            var longMembers = session.State.CollectionMembers(longTerm).ToHashSet();
            Check.Equal(3 + 1, longMembers.Count, "长期牌组 = 3 张 Strike + 1 张 test-selfharm");

            // ① 第一间房：副本 → 来源必须是长期牌组上的双射。
            var first = SourceMap(session);
            Check.Equal(longMembers.Count, first.Count, "战斗副本数与长期牌组基数一致");
            Check.True(first.Values.All(longMembers.Contains), "每个副本的来源都是长期牌组成员（不是另一个副本）");
            Check.Equal(first.Count, first.Values.Distinct().Count(), "不同副本的来源互不相同");
            Check.True(first.Values.ToHashSet().SetEquals(longMembers), "来源集合恰好覆盖长期牌组（双射）");

            // ② 同定义不同实例：3 张 Strike 的副本必须指向 3 个**不同**的长期实例。
            var strikeSources = first
                .Where(entry => Definition(session, entry.Key) == SliceManifest.StrikeId)
                .Select(entry => entry.Value)
                .ToList();
            Check.Equal(3, strikeSources.Count, "第一间房有 3 张 Strike 的副本");
            Check.Equal(3, strikeSources.Distinct().Count(),
                "3 张 Strike 副本的来源是 3 个不同实例（不能按 DefinitionId 合并）");

            // ③ 只升级其中一张长期 Strike：换房后只有它的副本是升级形态。
            var target = strikeSources[0];
            var targetCopyBefore = first.Single(entry => entry.Value == target).Key;
            Check.False(session.State.GetField(targetCopyBefore, cardType.FieldIndex("Upgraded")).AsBool(),
                "升级之前，来源实例的副本是普通形态");

            ClearRoom(session);
            var upgrade = session.LegalActions(SliceManifest.Player)
                .Single(candidate => candidate.Definition.QualifiedId == "base-spire:UpgradeLongTermCard");
            session.Submit(upgrade.Definition, SliceManifest.Player, [Value.OfRef(target)]);
            Check.True(session.State.GetField(target, cardType.FieldIndex("Upgraded")).AsBool(),
                "被选中的那一张长期实例升级（Upgraded=true）");

            EnterNextRoom(session);
            var second = SourceMap(session);
            Check.True(second.Values.ToHashSet().SetEquals(longMembers),
                "第二间房的来源集合仍是同一批长期实例（关系跨房稳定）");

            var upgradedCopies = second
                .Where(entry => session.State.GetField(entry.Key, cardType.FieldIndex("Upgraded")).AsBool())
                .ToList();
            Check.Equal(1, upgradedCopies.Count, "换房后恰好一个副本是升级形态");
            Check.True(upgradedCopies[0].Value == target, "升级形态的副本来源就是被升级的那个长期实例");
            Check.Equal(SliceManifest.StrikeId, Definition(session, upgradedCopies[0].Key),
                "被升级的副本仍是 Strike 定义（同定义）");

            var twinCopies = second
                .Where(entry => Definition(session, entry.Key) == SliceManifest.StrikeId && entry.Value != target)
                .ToList();
            Check.Equal(2, twinCopies.Count, "同定义还有两张副本指向别的长期实例");
            foreach (var twin in twinCopies)
            {
                Check.False(session.State.GetField(twin.Key, cardType.FieldIndex("Upgraded")).AsBool(),
                    "同定义的其它实例的副本仍是普通形态");
            }
        });
    }

    /// <summary>
    /// 作者 `run` 场景：`strike-ironclad x3` 给出**同定义三个实例**（正式源每张定义各一张），
    /// 其余部分与既有受控切片场景同形（两个 Cultist、两间房、奖励升级动作）。
    /// </summary>
    private const string SourceRelationScenarioText = """
        package base-spire { version = combat-slice-v1 }
        deck { strike-ironclad x3  test-selfharm }
        run {
          hp = 73
          max-hp = 91
          gold = 123
        }
        power strength { name = Strength  role = Strength  effect = "x"  owner-turn-end = none  damage = add-per-stack:1 }
        power ritual { name = Ritual  role = Ritual  effect = "x"  owner-turn-end = convert-to-strength }
        enemy calcified-cultist {
          name = CalcifiedCultist  hp = 120  tough-hp = 121  first-move-ritual = 4  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        enemy damp-cultist {
          name = DampCultist  hp = 120  tough-hp = 121  first-move-ritual = 5  strike-damage = 0
          strike-damage-deadly = 0  first-move = Incantation  repeat-move = DarkStrike
          move incantation { ritual = first-ritual }
          move dark-strike { damage = strike }
          cycle = [incantation, dark-strike xforever]
        }
        card strike-ironclad {
          name = StrikeIronclad  kind = Attack  cost = 0
          normal   { amount = 6  steps = [target, damage] }
          upgraded { amount = 6  steps = [target, damage] }
        }
        card test-selfharm {
          name = TestSelfHarm  kind = Skill  cost = 0
          normal   { amount = 1  steps = [lose_hp:1] }
          upgraded { amount = 3  steps = [lose_hp:1] }
        }
        """;

    /// <summary>
    /// ⑧ 复核要求（§2 修复方向）：末杀发生在**条件分支内部**时，分支之后的收尾仍要在终局之前跑完。
    /// `PactsEnd` 的伤害整个装在 `if pileCountAtLeast(exhaust,3) then attack(allEnemies,17)` 里，
    /// 因此"最后一个敌人死在条件分支里"正是要判别的形状：结果区摆放（进弃牌堆）不能丢。
    /// </summary>
    private static void ConditionalBranchCompletesBeforeTerminal()
    {
        Check.Run("ENV-T02 结算：末杀发生在条件分支里时，分支后的收尾仍在终局之前跑完 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();

            ClearRoom(session);
            EnterNextRoom(session);

            // 满足 `then` 条件：消耗堆 ≥ 3 张（受控地把三张牌从抽牌堆挪进消耗堆）。
            var drawPile = FormalSliceFixture.Collection(session, FormalSliceFixture.DrawPile);
            var exhaustPile = FormalSliceFixture.Collection(session, FormalSliceFixture.ExhaustPile);
            var moved = session.State.CollectionMembers(drawPile).Take(3).ToList();
            session.State.RunAtomic(() =>
            {
                foreach (var card in moved)
                {
                    session.State.RemoveFromCollection(drawPile, card);
                    session.State.AddToCollection(exhaustPile, card);
                }
            });
            Check.Equal(3, session.State.CollectionMembers(exhaustPile).Count, "消耗堆满足 `pileCountAtLeast` 条件");

            foreach (var monster in Monsters(session))
            {
                FormalSliceFixture.SetNumber(session, monster, FormalSliceFixture.MonsterTypeId, "Hp", 5L);
            }

            var pacts = FormalSliceFixture.MoveToHand(session, "base-spire:pacts-end");
            FormalSliceFixture.PlayAt(session, pacts, Monsters(session)[0]);

            Check.Equal(SessionStatus.Terminal, session.Status, "条件分支里的 AoE 清完最后一个敌人 ⇒ 整局终局");
            Check.Equal("base-spire:Victory", session.Terminal!.Definition.QualifiedId, "终局是整局胜利");
            Check.Equal(0, Monsters(session).Count, "条件分支里的攻击把两个敌人都清掉");
            Check.False(session.State.CollectionMembers(
                    FormalSliceFixture.Collection(session, "base-spire:InPlay")).Contains(pacts),
                "打出的 PactsEnd 没有留在 `InPlay`");
            Check.True(session.State.CollectionMembers(
                    FormalSliceFixture.Collection(session, FormalSliceFixture.DiscardPile)).Contains(pacts),
                "打出的 PactsEnd 进了弃牌堆（结果区在终局之前跑完）");
        });
    }

    /// <summary>
    /// ⑨ 复核要求（§2 修复方向）：末杀来自**消耗牌**时，消耗摆放与 `after_card_exhausted` 反应
    /// 都必须在终局之前跑完。`MoltenFist` = 攻击 10 + `keywords = { exhaust }` + 末尾 `if targetAlive`
    /// （末杀之后那条条件段仍会被求值，不能因目标已被移除而出错）；
    /// `FeelNoPain` = `afterCardExhausted` 每层 +1 格挡，是"消耗反应"的可观测量。
    /// </summary>
    private static void ExhaustReactionCompletesBeforeTerminal()
    {
        Check.Run("ENV-T02 结算：消耗牌末杀时，消耗摆放与 after_card_exhausted 反应在终局之前跑完 [真实]", () =>
        {
            var session = FormalSliceFixture.NewFormal();
            var player = FormalSliceFixture.Player(session);

            ClearRoom(session);
            EnterNextRoom(session);

            // 最后一间房有两只敌人：先用打击打死一只（打击不消耗，不触发 `FeelNoPain`）。
            KillWith(session, Monsters(session)[0], SliceManifest.StrikeId);
            Check.Equal(1, Monsters(session).Count, "只剩一个敌人交给消耗牌收尾");

            FormalSliceFixture.GivePower(session, player, "base-spire:feel-no-pain", 1L);
            Check.Equal(0L, FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Block"),
                "受控前提：出牌前没有格挡");

            var last = Monsters(session)[0];
            FormalSliceFixture.SetNumber(session, last, FormalSliceFixture.MonsterTypeId, "Hp", 5L);
            var fist = FormalSliceFixture.MoveToHand(session, "base-spire:molten-fist");
            FormalSliceFixture.PlayAt(session, fist, last);

            Check.Equal(SessionStatus.Terminal, session.Status, "消耗牌末杀 ⇒ 整局终局");
            Check.True(session.State.CollectionMembers(
                    FormalSliceFixture.Collection(session, FormalSliceFixture.ExhaustPile)).Contains(fist),
                "打出的 MoltenFist 进了消耗堆（消耗摆放先于终局）");
            Check.Equal(1L, FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Block"),
                "`after_card_exhausted` 反应在终局之前生效（FeelNoPain ⇒ 1 格挡）");
        });
    }

    /// <summary>副本 → 长期来源（读 `Card.LongTermSource`，覆盖全部战斗牌区）。</summary>
    private static Dictionary<InstanceId, InstanceId> SourceMap(Session session)
    {
        var index = session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId).FieldIndex("LongTermSource");
        var map = new Dictionary<InstanceId, InstanceId>();
        foreach (var pile in new[]
                 {
                     FormalSliceFixture.HandPile, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile,
                     FormalSliceFixture.ExhaustPile,
                 })
        {
            foreach (var copy in session.State.CollectionMembers(FormalSliceFixture.Collection(session, pile)))
            {
                map[copy] = session.State.GetField(copy, index).AsRef();
            }
        }

        return map;
    }

    private static InstanceId RunInstance(Session session) =>
        session.State.CollectionMembers(FormalSliceFixture.Collection(session, RunStatesCollection)).Single();

    private static long RunField(Session session, InstanceId run, string field) =>
        FormalSliceFixture.Number(session, run, RunTypeId, field);

    private static string Definition(Session session, InstanceId card) =>
        FormalSliceFixture.Definition(session, card);

    private static HashSet<InstanceId> BattleCards(Session session) =>
        new[] { FormalSliceFixture.HandPile, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile }
            .SelectMany(pile => session.State.CollectionMembers(FormalSliceFixture.Collection(session, pile)))
            .ToHashSet();

    /// <summary>把目标压到 5 点（任何攻击都能打死），再用指定牌打死它。</summary>
    private static void KillWith(Session session, InstanceId monster, string cardId)
    {
        FormalSliceFixture.SetNumber(session, monster, FormalSliceFixture.MonsterTypeId, "Hp", 5L);
        var card = FormalSliceFixture.MoveToHand(session, cardId);
        FormalSliceFixture.PlayAt(session, card, monster);
    }

    /// <summary>
    /// 清掉当前房间的所有敌人。
    /// 正式源里每张卡各只有**一个物理实例**，而同一回合不能重复打出同一个实例（`PlayedThisTurn` 成员冲突）
    /// ⇒ 两个敌人之间必须过一个回合（此时敌人会行动、玩家掉血，所以整局 HP 只能按"等于战斗副本"比较）。
    /// </summary>
    private static void ClearRoom(Session session)
    {
        foreach (var monster in Monsters(session))
        {
            KillWith(session, monster, SliceManifest.StrikeId);
            if (Monsters(session).Count > 0)
            {
                FormalSliceFixture.EndTurn(session);
            }
        }
    }

    private static List<InstanceId> Monsters(Session session) =>
        [.. session.State.CollectionMembers(FormalSliceFixture.Collection(session, MonstersCollection))];

    /// <summary>执行 `EnterNextRoom`（只有结算完成的房间才合法）。</summary>
    private static void EnterNextRoom(Session session)
    {
        var action = session.LegalActions(SliceManifest.Player)
            .SingleOrDefault(candidate => candidate.Definition.QualifiedId == "base-spire:EnterNextRoom")
            ?? throw new CheckFailedException("EnterNextRoom 当前不合法（房间尚未结算？）");
        session.Submit(action.Definition, SliceManifest.Player, []);
    }
}
