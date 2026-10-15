using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Domain.Run;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// ENV-T05：**正规战斗奖励**子包——源 `RewardsSet.WithRewardsFromRoom → GenerateRewardsFor →
/// GenerateWithoutOffering`、`GoldReward`、`CardReward`/`CardFactory`、`PotionRewardOdds`、
/// `CardRarityOdds` 的机制移植。
///
/// 只在地图 scope（`actMap is not null`，即 `ScopeId == DevMap`/正式地图）声明：dev 与受控切片的
/// 包形状逐字不变，非地图 scope 仍用旧的 `UpgradeLongTermCard` 占位。
///
/// <para>
/// **抽取时序**（源 `RewardsSet.cs:224-255` 收集、`:125-147` 逐条 `Populate`；显示顺序是排序后的，不是抽取顺序）：
/// </para>
/// <list type="number">
/// <item>`RollForPotionAndAddTo` → `PotionRewardOdds.Roll`：**1 抽**（float32 赔率判定，命中 −0.1、未命中 +0.1，无夹取）。</item>
/// <item>`GoldReward.Populate` → `NextInt(min, max + 1)`：**1 抽**（`GoldReward.defaultMinGoldAmount/MaxGoldAmount`）。</item>
/// <item>若掉落：`PotionReward.Populate` → `PotionFactory.CreateRandomPotion`：**2 抽**，
///       先 float32 稀有度，再从完整角色/共享池的同稀有度成员按源顺序 NextItem。
///       生成与使用支持独立；未实现使用效果的药水仍有真实实例及具名原因。</item>
/// <item>`CardReward.Populate` → `CardFactory.CreateForReward(player, 3, options)`：每候选 **3 抽**
///       （`RollForRarity` 稀有度 float、`NextItem` 取牌 int、`RollForUpgrade` 升级 float）＝**9 抽**。</item>
/// </list>
/// <para>
/// ⇒ 无药水 11 抽、有药水（源）13 抽。`Act 1` 的 `CurrentActIndex == 0` 且 `baseChance = 0m`，
/// 升级判定保留 `num &lt;= originalOdds`（`NextFloat()` 含 0，只有恰好抽到 0 才升级）。
/// </para>
///
/// <para>
/// **相位**：`MapPhase` 的第 3 相位（<see cref="RewardPhase"/>）＝"奖励开放"。`MapRoomWiring.
/// RegisterChooseMapPoint` 的守卫是 `{0, 2}`，因此奖励期天然不能旅行；奖励判定完成后写回 2。
/// </para>
/// </summary>
public sealed class RewardWiring
{
    /// <summary>奖励候选卡的集合名（卡实体类型，owner = Run 所在 scope，即 battle scope）。</summary>
    public const string RewardCardsCollectionName = "RewardCards";

    /// <summary>内容侧声明进单机 Ironclad 池的 `pools` 名（与 `IroncladCardPool` 同名）。</summary>
    public const string IroncladCardPoolName = "IroncladCardPool";

    public const string CardParameterName = "Card";
    public const string PoolIndexParameterName = "PoolIndex";
    public const string UpgradedParameterName = "Upgraded";

    public const string OpenRewardsProcedureName = "OpenRewards";
    public const string CreateCardProcedureName = "CreateRewardCard";
    public const string CloseRewardsProcedureName = "CloseRewards";

    public const string TakeGoldActionName = "TakeRewardGold";
    public const string TakeCardActionName = "TakeRewardCard";
    public const string SkipCardActionName = "SkipRewardCard";
    public const string TakePotionActionName = "TakeRewardPotion";
    public const string SkipPotionActionName = "SkipRewardPotion";

    public const string GoldFieldName = "RewardGold";
    public const string GoldTakenFieldName = "RewardGoldTaken";
    public const string PotionDroppedFieldName = "RewardPotionDropped";
    public const string PotionResolvedFieldName = "RewardPotionResolved";
    public const string PotionOddsFieldName = "RewardPotionOdds";
    public const string CardOddsFieldName = "RewardCardOdds";
    public const string CardRarityFieldName = "RewardCardRarity";
    public const string CardIndexFieldName = "RewardCardIndex";
    public const string CardUpgradedFieldName = "RewardCardUpgraded";

    /// <summary>`MapPhase` 的"奖励开放"相位：`ChooseMapPoint` 的 `{0, 2}` 守卫在此相位不成立。</summary>
    public const int RewardPhase = 3;

    /// <summary>源 `CardReward` 的 `OptionCount`（`RewardsSet` 传 3）。</summary>
    public const int CardOptionCount = 3;

    /// <summary>int 域的位型上界（float32 位型原样存进 Int 字段）。</summary>
    public const long BitsMax = 4294967295L;

    /// <summary>池下标域上界（含 <see cref="RewardCompute.NoPoolIndex"/> 哨兵）。</summary>
    private const int PoolIndexMax = 999;

    /// <summary>池规格串的最大长度（85 项 × 约 5 字符）。</summary>
    private const int PoolSpecLength = 4096;

    private readonly CollectionSymbol runStates;
    private readonly RunDeck deck;
    private readonly EntityTypeSymbol cardEntity;
    private readonly CollectionSymbol rewardCards;
    private readonly RunResources resources;
    private readonly FieldSymbol runCurrentRoom;
    private readonly FieldSymbol runRewardTaken;
    private readonly FieldSymbol runMapPhase;
    private readonly FieldSymbol rewardGold;
    private readonly FieldSymbol goldTaken;
    private readonly FieldSymbol potionDropped;
    private readonly FieldSymbol potionResolved;
    private readonly FieldSymbol potionOdds;
    private readonly FieldSymbol cardOdds;
    private readonly FieldSymbol[] rarityFields;
    private readonly FieldSymbol[] indexFields;
    private readonly FieldSymbol[] upgradedFields;
    private readonly ProcedureBuilder openRewards;
    private readonly ProcedureBuilder createRewardCard;
    private readonly ProcedureBuilder closeRewards;
    private readonly Assets assets;
    private readonly string poolSpec;
    private readonly string rewardsStream;
    private PotionInventoryRules? potions;

    private RewardWiring(
        CollectionSymbol runStates,
        RunDeck deck,
        EntityTypeSymbol cardEntity,
        CollectionSymbol rewardCards,
        RunResources resources,
        FieldSymbol runCurrentRoom,
        FieldSymbol runRewardTaken,
        FieldSymbol runMapPhase,
        FieldSymbol rewardGold,
        FieldSymbol goldTaken,
        FieldSymbol potionDropped,
        FieldSymbol potionResolved,
        FieldSymbol potionOdds,
        FieldSymbol cardOdds,
        FieldSymbol[] rarityFields,
        FieldSymbol[] indexFields,
        FieldSymbol[] upgradedFields,
        ProcedureBuilder openRewards,
        ProcedureBuilder createRewardCard,
        ProcedureBuilder closeRewards,
        Assets assets,
        string poolSpec,
        string rewardsStream,
        IReadOnlyList<(CardDefinition Card, RewardPoolEntry Entry)> pool)
    {
        this.runStates = runStates;
        this.deck = deck;
        this.cardEntity = cardEntity;
        this.rewardCards = rewardCards;
        this.resources = resources;
        this.runCurrentRoom = runCurrentRoom;
        this.runRewardTaken = runRewardTaken;
        this.runMapPhase = runMapPhase;
        this.rewardGold = rewardGold;
        this.goldTaken = goldTaken;
        this.potionDropped = potionDropped;
        this.potionResolved = potionResolved;
        this.potionOdds = potionOdds;
        this.cardOdds = cardOdds;
        this.rarityFields = rarityFields;
        this.indexFields = indexFields;
        this.upgradedFields = upgradedFields;
        this.openRewards = openRewards;
        this.createRewardCard = createRewardCard;
        this.closeRewards = closeRewards;
        this.assets = assets;
        this.poolSpec = poolSpec;
        this.rewardsStream = rewardsStream;
        Pool = pool;
    }

    /// <summary>有效奖励池：源声明顺序（`IroncladCardPool.GenerateAllCards()`）∩ 内容 `pools` 声明。</summary>
    public IReadOnlyList<(CardDefinition Card, RewardPoolEntry Entry)> Pool { get; }

    /// <summary>池规格串（IR 侧把它当常量传给纯计算资产：后端看不到内容模块的状态）。</summary>
    public string PoolSpec => poolSpec;

    /// <summary>奖励候选卡集合（供检查读取）。</summary>
    public CollectionSymbol RewardCards => rewardCards;

    /// <summary>开奖过程符号（`SettleBattle` 调用）。</summary>
    public ProcedureBuilder OpenRewards => openRewards;

    /// <summary>
    /// 声明奖励子包：Run 字段、`RewardCards` 集合、5 个纯计算资产、3 个过程（开奖/建候选卡/关闭奖励）。
    /// 调用点必须在 `Run` 实体类型与 `MapRoomWiring` 之后（奖励相位字段来自地图子包）。
    /// </summary>
    public static RewardWiring Declare(
        EnginePackageBuilder package,
        EntityTypeSymbol runEntity,
        EntityTypeSymbol cardEntity,
        CollectionSymbol runStates,
        RunDeck deck,
        RunResources resources,
        FieldSymbol runCurrentRoom,
        FieldSymbol runRewardTaken,
        FieldSymbol runMapPhase,
        IReadOnlyList<CardDefinition> cards,
        Func<IrScope, CardDefinition, bool, IReadOnlyList<(FieldSymbol Field, Expr Value)>> cardFields,
        Func<IrScope, IReadOnlyList<Inst>>? afterClose = null,
        Func<IrScope, Expr>? isBoss = null,
        PotionInventoryRules? potions = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(cardFields);

        var pool = BuildPool(cards);
        var spec = RewardPoolSpec.Encode(pool.Select(entry => entry.Entry));
        var stream = VanillaRandomLayout.EnginePlayerStreamName("Rewards");

        var rewardCards = package.Collection(RewardCardsCollectionName, cardEntity);

        // -- Run 字段（都在"奖励开放"相位里被写；公开投影自动带上，供公开轨迹对账） --------------------
        var rewardGold = runEntity.AddField(GoldFieldName, FieldTypeSchema.Int(0, 99999));
        var goldTaken = runEntity.AddField(GoldTakenFieldName, FieldTypeSchema.Int(0, 999));
        var potionDropped = runEntity.AddField(PotionDroppedFieldName, FieldTypeSchema.Bool());
        var potionResolved = runEntity.AddField(PotionResolvedFieldName, FieldTypeSchema.Int(0, 999));
        var potionOdds = runEntity.AddField(PotionOddsFieldName, FieldTypeSchema.Int(0, BitsMax));
        var cardOdds = runEntity.AddField(CardOddsFieldName, FieldTypeSchema.Int(0, BitsMax));

        var rarityFields = new FieldSymbol[CardOptionCount];
        var indexFields = new FieldSymbol[CardOptionCount];
        var upgradedFields = new FieldSymbol[CardOptionCount];
        for (var option = 0; option < CardOptionCount; option++)
        {
            rarityFields[option] = runEntity.AddField($"{CardRarityFieldName}{option}",
                FieldTypeSchema.Int(0, RewardRarity.Quest));
            indexFields[option] = runEntity.AddField($"{CardIndexFieldName}{option}",
                FieldTypeSchema.Int(0, PoolIndexMax));
            upgradedFields[option] = runEntity.AddField($"{CardUpgradedFieldName}{option}", FieldTypeSchema.Bool());
        }

        var assets = DeclareAssets(package);

        var createRewardCard = BuildCreateRewardCard(package, runStates, cardEntity, rewardCards, pool, cardFields);
        var openRewards = BuildOpenRewards(package, runStates, rewardCards, runMapPhase, rewardGold, potionDropped,
            potionOdds, cardOdds, rarityFields, indexFields, upgradedFields, assets, spec, stream, createRewardCard, isBoss, potions);
        var closeRewards = BuildCloseRewards(package, runStates, rewardCards, runCurrentRoom, runRewardTaken, runMapPhase,
            goldTaken, potionDropped, potionResolved, afterClose, potions);

        return new RewardWiring(runStates, deck, cardEntity, rewardCards, resources, runCurrentRoom, runRewardTaken,
            runMapPhase, rewardGold, goldTaken, potionDropped, potionResolved, potionOdds, cardOdds, rarityFields,
            indexFields, upgradedFields, openRewards, createRewardCard, closeRewards, assets, spec, stream, pool) { potions = potions };
    }

    /// <summary>
    /// 开局步骤：把两条**跨房间持久**的赔率状态初始化成源的构造值
    /// （`PotionRewardOdds` ctor `base(0.4f, rng)`、`CardRarityOdds` ctor `base(-0.05f, rng)`）。
    /// 之后每次 roll 就地更新（命中/未命中的步长与偏移增长都在纯计算资产里），不重置。
    /// </summary>
    public IReadOnlyList<Inst> SetupSteps(IrScope scope, CollectionSymbol runStates)
    {
        var run = scope.First(scope.Collection(runStates));
        return
        [
            scope.SetField(run, potionOdds, scope.Const((long)RewardCompute.BitsOf(RewardCompute.PotionBaseOdds))),
            scope.SetField(run, cardOdds, scope.Const((long)RewardCompute.BitsOf(RewardCompute.CardRarityBaseOffset))),
        ];
    }

    /// <summary>战斗结算边界里开奖（`MapPhase` 由 2 推到 <see cref="RewardPhase"/>）。</summary>
    public IReadOnlyList<Inst> SettleSteps(IrScope scope) => [scope.Call(openRewards.Symbol)];

    /// <summary>
    /// 注册 5 个奖励动作：领取金币、领取候选卡、跳过候选卡、取得药水（满栏时奖励保留）、跳过药水。
    /// 守卫都含"相位 3 + 本间房未解决"，因此重复提交同一奖励不重复增益（幂等）。
    /// </summary>
    public void RegisterActions(EnginePackageBuilder package)
    {
        RegisterTakeGold(package);
        RegisterTakeCard(package);
        RegisterSkipCard(package);
        RegisterTakePotion(package);
        RegisterSkipPotion(package);
    }

    private void RegisterTakeGold(EnginePackageBuilder package)
    {
        var action = package.Action(TakeGoldActionName);
        var body = package.ActionBody(action);
        var run = body.First(body.Collection(runStates));
        body.Role(SliceManifest.PlayerRole);
        body.Requires(body.And(
            body.Equal(body.Field(run, runMapPhase), body.Const((long)RewardPhase)),
            body.NotEqual(body.Field(run, goldTaken), body.Field(run, runCurrentRoom))));
        body.Body([
            // 源 `GoldReward.OnSelect` → `PlayerCmd.GainGold(Amount, …)`：直接进**整局**的金币（战斗外，
            // 不存在战斗副本写回问题；`CommitPermanent` 只负责 Hp/MaxHp）。
            .. resources.GainGold(body, run, body.Field(run, rewardGold)),
            body.SetField(run, goldTaken, body.Field(run, runCurrentRoom)),
            body.Call(closeRewards.Symbol)]);
    }

    private void RegisterTakeCard(EnginePackageBuilder package)
    {
        var action = package.Action(TakeCardActionName);
        var parameter = action.Parameter(CardParameterName, FieldTypeSchema.RefTo(cardEntity));
        var body = package.ActionBody(action);
        var run = body.First(body.Collection(runStates));
        var cardRef = body.Param(parameter.DeclarationIndex);
        body.Role(SliceManifest.PlayerRole);
        body.Candidates(CardParameterName, body.Collection(rewardCards));
        body.Requires(body.And(
            body.Equal(body.Field(run, runMapPhase), body.Const((long)RewardPhase)),
            body.NotEqual(body.Field(run, runRewardTaken), body.Field(run, runCurrentRoom))));
        body.Body([
            // 源 `CardReward.OnSelect` → `CardPileCmd.Add(obtainedCard, PileType.Deck)`：进长期牌组，
            // 后续房间由 `StartNextRoom` 逐张拷进战斗牌堆；未选中的候选按源记 `wasPicked: false` 后丢弃。
            body.RemoveFromCollection(rewardCards, cardRef),
            body.ForEach(body.Collection(rewardCards), other => new Inst[] { body.Destroy(other) }),
            .. deck.Acquire(body, cardRef),
            body.SetField(run, runRewardTaken, body.Field(run, runCurrentRoom)),
            body.Call(closeRewards.Symbol)]);
    }

    private void RegisterSkipCard(EnginePackageBuilder package)
    {
        var action = package.Action(SkipCardActionName);
        var body = package.ActionBody(action);
        var run = body.First(body.Collection(runStates));
        body.Role(SliceManifest.PlayerRole);
        body.Requires(body.And(
            body.Equal(body.Field(run, runMapPhase), body.Const((long)RewardPhase)),
            body.NotEqual(body.Field(run, runRewardTaken), body.Field(run, runCurrentRoom))));
        body.Body(
            // 源 `CardReward.CanSkip = true`（`CardReward.cs:95`）+ `OnSkipped` 只记 history（`:313-320`）。
            body.ForEach(body.Collection(rewardCards), other => new Inst[] { body.Destroy(other) }),
            body.SetField(run, runRewardTaken, body.Field(run, runCurrentRoom)),
            body.Call(closeRewards.Symbol));
    }

    private void RegisterTakePotion(EnginePackageBuilder package)
    {
        var action = package.Action(TakePotionActionName);
        var body = package.ActionBody(action);
        var run = body.First(body.Collection(runStates));
        body.Role(SliceManifest.PlayerRole);
        var available = body.And(
            body.Equal(body.Field(run, runMapPhase), body.Const((long)RewardPhase)),
            body.And(
                body.Field(run, potionDropped),
                body.NotEqual(body.Field(run, potionResolved), body.Field(run, runCurrentRoom))));
        body.Requires(available);
        if (potions is null)
        {
            body.Body(body.Fail("base-spire: potion inventory and complete source pool are not loaded"));
            return;
        }
        body.Requires(body.And(available,
            body.And(potions.HasSpace(body), body.Exists(body.Collection(potions.Rewards)))));
        body.Body([
            .. potions.Acquire(body),
            body.SetField(run, potionResolved, body.Field(run, runCurrentRoom)),
            body.Call(closeRewards.Symbol),
        ]);
    }

    private void RegisterSkipPotion(EnginePackageBuilder package)
    {
        var action = package.Action(SkipPotionActionName);
        var body = package.ActionBody(action);
        var run = body.First(body.Collection(runStates));
        body.Role(SliceManifest.PlayerRole);
        body.Requires(body.And(
            body.Equal(body.Field(run, runMapPhase), body.Const((long)RewardPhase)),
            body.And(
                body.Field(run, potionDropped),
                body.NotEqual(body.Field(run, potionResolved), body.Field(run, runCurrentRoom)))));
        body.Body([
            .. (potions?.ClearReward(body) ?? []),
            // 源 `Reward.OnSkipped`（`Reward.cs:111-113`）——药水奖励允许跳过，只记 history。
            body.SetField(run, potionResolved, body.Field(run, runCurrentRoom)),
            body.Call(closeRewards.Symbol)]);
    }

    // -- 构建 -----------------------------------------------------------------------------------------

    private static ProcedureBuilder BuildCreateRewardCard(
        EnginePackageBuilder package,
        CollectionSymbol runStates,
        EntityTypeSymbol cardEntity,
        CollectionSymbol rewardCards,
        IReadOnlyList<(CardDefinition Card, RewardPoolEntry Entry)> pool,
        Func<IrScope, CardDefinition, bool, IReadOnlyList<(FieldSymbol Field, Expr Value)>> cardFields)
    {
        var create = package.Procedure(CreateCardProcedureName);
        var poolIndexSlot = create.Param(PoolIndexParameterName, FieldTypeSchema.Int(0, PoolIndexMax));
        var upgradedSlot = create.Param(UpgradedParameterName, FieldTypeSchema.Bool());
        var run = create.First(create.Collection(runStates));
        var owner = create.EntityScope(run);
        var created = create.Local("rewardCardCreated", FieldTypeSchema.Binding());
        var poolIndex = create.Param(poolIndexSlot);
        var upgraded = create.Param(upgradedSlot);

        // 一路 `If` 一个池成员（下标恒有效，越界哨兵 = 不建牌）：分支深度与 `UpgradeLongTermCard` 的
        // DefinitionId 分派同级，形状上是"数据表 → IR"，不是手写 85 段逻辑。
        var cases = new List<Inst>();
        for (var index = 0; index < pool.Count; index++)
        {
            var definition = pool[index].Card;
            cases.Add(create.If(create.Equal(poolIndex, create.Const((long)index)), new Inst[]
            {
                create.IfElse(upgraded,
                    new Inst[]
                    {
                        create.CreateEntity(cardEntity, owner, created, cardFields(create, definition, true), rewardCards),
                    },
                    new Inst[]
                    {
                        create.CreateEntity(cardEntity, owner, created, cardFields(create, definition, false), rewardCards),
                    }),
            }));
        }

        create.Body(cases.ToArray());
        return create;
    }

    private static ProcedureBuilder BuildOpenRewards(
        EnginePackageBuilder package,
        CollectionSymbol runStates,
        CollectionSymbol rewardCards,
        FieldSymbol runMapPhase,
        FieldSymbol rewardGold,
        FieldSymbol potionDropped,
        FieldSymbol potionOdds,
        FieldSymbol cardOdds,
        FieldSymbol[] rarityFields,
        FieldSymbol[] indexFields,
        FieldSymbol[] upgradedFields,
        Assets assets,
        string poolSpec,
        string rewardsStream,
        ProcedureBuilder createRewardCard, Func<IrScope, Expr>? isBoss, PotionInventoryRules? potions)
    {
        var open = package.Procedure(OpenRewardsProcedureName);
        var run = open.First(open.Collection(runStates));
        var bits = open.Local("rewardBits", FieldTypeSchema.Int(0, RewardCompute.Span - 1));
        var dropped = open.Local("rewardPotionDropped", FieldTypeSchema.Bool());
        var nextOdds = open.Local("rewardNextOdds", FieldTypeSchema.Int(0, BitsMax));
        var gold = open.Local("rewardGoldValue", FieldTypeSchema.Int(0, 99999));
        var rarity = open.Local("rewardRarity", FieldTypeSchema.Int(0, RewardRarity.Quest));
        var count = open.Local("rewardPoolCount", FieldTypeSchema.Int(0, 512));
        var pick = open.Local("rewardPick", FieldTypeSchema.Int(0, 512));
        var index = open.Local("rewardPoolIndex", FieldTypeSchema.Int(0, PoolIndexMax));
        var upgraded = open.Local("rewardUpgraded", FieldTypeSchema.Bool());
        var spec = open.Const(poolSpec);
        var boss = isBoss?.Invoke(open) ?? open.Const(false);

        var steps = new List<Inst>
        {
            // 上一间房若留下候选（理论上已被 CloseRewards 清空），这里先清场，保证候选集合只属本间房。
            open.ForEach(open.Collection(rewardCards), each => new Inst[] { open.Destroy(each) }),

            // ① 药水掉落判定（源 `RewardsSet.RollForPotionAndAddTo` → `PotionRewardOdds.Roll`）：1 抽。
            open.SampleInt(rewardsStream, open.Const(0L), open.Const(RewardCompute.Span), bits),
            open.Compute(assets.PotionDrop, new (FieldSymbol, Expr)[]
            {
                (assets.DropBits, open.LocalRef(bits)),
                (assets.DropCurrent, open.Field(run, potionOdds)),
                (assets.DropIsElite, open.Const(false)),
            }, new (FieldSymbol, int)[]
            {
                (assets.DropDropped, dropped),
                (assets.DropNext, nextOdds),
            }),
            open.SetField(run, potionDropped, open.LocalRef(dropped)),
            open.SetField(run, potionOdds, open.LocalRef(nextOdds)),

            // ② 金币值（源 `GoldReward.Populate` → `NextInt(min, max + 1)`）：1 抽。
            open.IfElse(boss,
                [open.SampleInt(rewardsStream, open.Const(100L), open.Const(101L), gold)],
                [open.SampleInt(rewardsStream, open.Const((long)RewardCompute.DefaultMinGold),
                    open.Const((long)RewardCompute.DefaultMaxGold + 1), gold)]),
            open.SetField(run, rewardGold, open.LocalRef(gold)),
        };

        for (var option = 0; option < CardOptionCount; option++)
        {
            steps.Add(open.SetField(run, indexFields[option], open.Const((long)RewardCompute.NoPoolIndex)));
        }

        // Source potion generation occurs after gold and before the card candidates.
        if (potions is not null)
        {
            steps.InsertRange(0, potions.ClearReward(open));
            steps.Add(open.If(open.LocalRef(dropped),
                potions.Generate(open, run).ToArray()));
        }

        // ④ 三张候选：每张 3 抽（稀有度 float → 取牌 int → 升级 float），黑名单 = 已抽中的候选中枢卡。
        for (var option = 0; option < CardOptionCount; option++)
        {
            var blacklistA = option == 0
                ? open.Const((long)RewardCompute.NoPoolIndex)
                : open.Field(run, indexFields[option - 1]);
            var blacklistB = option <= 1
                ? open.Const((long)RewardCompute.NoPoolIndex)
                : open.Field(run, indexFields[option - 2]);

            steps.Add(open.SampleInt(rewardsStream, open.Const(0L), open.Const(RewardCompute.Span), bits));
            steps.Add(open.Compute(assets.CardRarity, new (FieldSymbol, Expr)[]
            {
                (assets.RarityBits, open.LocalRef(bits)),
                (assets.RaritySpec, spec),
                (assets.RarityCurrent, open.Field(run, cardOdds)),
                (assets.RarityIdxA, blacklistA),
                (assets.RarityIdxB, blacklistB),
                (assets.RarityBoss, boss),
            }, new (FieldSymbol, int)[]
            {
                (assets.RarityOut, rarity),
                (assets.RarityNext, nextOdds),
            }));
            steps.Add(open.SetField(run, cardOdds, open.LocalRef(nextOdds)));
            steps.Add(open.SetField(run, rarityFields[option], open.LocalRef(rarity)));
            steps.Add(open.Compute(assets.CardCount, new (FieldSymbol, Expr)[]
            {
                (assets.CountSpec, spec),
                (assets.CountRarity, open.LocalRef(rarity)),
                (assets.CountIdxA, blacklistA),
                (assets.CountIdxB, blacklistB),
            }, new (FieldSymbol, int)[]
            {
                (assets.CountOut, count),
            }));
            steps.Add(open.IfElse(open.Equal(open.LocalRef(count), open.Const(0L)),
                new Inst[]
                {
                    open.Fail("base-spire: 奖励牌池在该稀有度上已无可选牌（源 `CardFactory.CreateForReward` 在此抛 " +
                        "InvalidOperationException: Tried to create a card for a reward, but we couldn't generate a " +
                        "valid card/rarity）"),
                },
                new Inst[]
                {
                    open.SampleInt(rewardsStream, open.Const(0L), open.LocalRef(count), pick),
                    open.Compute(assets.CardPick, new (FieldSymbol, Expr)[]
                    {
                        (assets.PickSpec, spec),
                        (assets.PickRarity, open.LocalRef(rarity)),
                        (assets.PickIndex, open.LocalRef(pick)),
                        (assets.PickIdxA, blacklistA),
                        (assets.PickIdxB, blacklistB),
                    }, new (FieldSymbol, int)[]
                    {
                        (assets.PickOut, index),
                    }),
                    open.SetField(run, indexFields[option], open.LocalRef(index)),
                    open.SampleInt(rewardsStream, open.Const(0L), open.Const(RewardCompute.Span), bits),
                    open.Compute(assets.CardUpgrade, new (FieldSymbol, Expr)[]
                    {
                        (assets.UpBits, open.LocalRef(bits)),
                        (assets.UpSpec, spec),
                        (assets.UpIndex, open.LocalRef(index)),
                        // 源 `player.RunState.CurrentActIndex`：幕 1 = 0（0 基）。
                        (assets.UpAct, open.Const(0L)),
                    }, new (FieldSymbol, int)[]
                    {
                        (assets.UpOut, upgraded),
                    }),
                    open.SetField(run, upgradedFields[option], open.LocalRef(upgraded)),
                    open.Call(createRewardCard.Symbol, open.LocalRef(index), open.LocalRef(upgraded)),
                }));
        }

        // ⑤ 相位 = 奖励开放（3）：`ChooseMapPoint` 的 {0, 2} 守卫在此不成立 ⇒ 奖励关闭前不能旅行。
        steps.Add(open.SetField(run, runMapPhase, open.Const((long)RewardPhase)));

        open.Body(steps.ToArray());
        return open;
    }

    private static ProcedureBuilder BuildCloseRewards(
        EnginePackageBuilder package,
        CollectionSymbol runStates,
        CollectionSymbol rewardCards,
        FieldSymbol runCurrentRoom,
        FieldSymbol runRewardTaken,
        FieldSymbol runMapPhase,
        FieldSymbol goldTaken,
        FieldSymbol potionDropped,
        FieldSymbol potionResolved,
        Func<IrScope, IReadOnlyList<Inst>>? afterClose, PotionInventoryRules? potions)
    {
        var close = package.Procedure(CloseRewardsProcedureName);
        var run = close.First(close.Collection(runStates));

        // 三件奖励都"本间房已解决"才收尾：卡牌（领或跳）、金币（领）、药水（无掉落，或已解决）。
        var done = close.And(
            close.Equal(close.Field(run, runRewardTaken), close.Field(run, runCurrentRoom)),
            close.And(
                close.Equal(close.Field(run, goldTaken), close.Field(run, runCurrentRoom)),
                close.Or(
                    close.Not(close.Field(run, potionDropped)),
                    close.Equal(close.Field(run, potionResolved), close.Field(run, runCurrentRoom)))));

        close.Body(close.If(done, [
            close.ForEach(close.Collection(rewardCards), each => new Inst[] { close.Destroy(each) }),
            .. (potions?.ClearReward(close) ?? []),
            close.SetField(run, potionDropped, close.Const(false)),
            close.SetField(run, runMapPhase, close.Const(2L)),
            .. (afterClose?.Invoke(close) ?? []),
        ]));
        return close;
    }

    /// <summary>声明 5 个纯计算资产（都走 `BaseSpireComputeBackend`，按 `Entry` 分派）。</summary>
    private static Assets DeclareAssets(EnginePackageBuilder package)
    {
        var drop = package.ComputeAsset(RewardCompute.PotionDropAssetName);
        drop.BackendId = BaseSpireComputeBackend.BackendId;
        drop.SourcePath = RewardCompute.SourcePath;
        drop.SourceHash = RewardCompute.SourceHash;
        drop.Entry = RewardCompute.PotionDropEntry;
        var dropBits = drop.Input("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1));
        var dropCurrent = drop.Input("Current", FieldTypeSchema.Int(0, BitsMax));
        var dropIsElite = drop.Input("IsElite", FieldTypeSchema.Bool());
        var dropDropped = drop.Output("Dropped", FieldTypeSchema.Bool());
        var dropNext = drop.Output("Next", FieldTypeSchema.Int(0, BitsMax));

        var rarity = package.ComputeAsset(RewardCompute.CardRarityAssetName);
        rarity.BackendId = BaseSpireComputeBackend.BackendId;
        rarity.SourcePath = RewardCompute.SourcePath;
        rarity.SourceHash = RewardCompute.SourceHash;
        rarity.Entry = RewardCompute.CardRarityEntry;
        var rarityBits = rarity.Input("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1));
        var raritySpec = rarity.Input("Spec", FieldTypeSchema.String(PoolSpecLength));
        var rarityCurrent = rarity.Input("Current", FieldTypeSchema.Int(0, BitsMax));
        var rarityIdxA = rarity.Input("IdxA", FieldTypeSchema.Int(0, PoolIndexMax));
        var rarityIdxB = rarity.Input("IdxB", FieldTypeSchema.Int(0, PoolIndexMax));
        var rarityBoss = rarity.Input("Boss", FieldTypeSchema.Bool());
        var rarityOut = rarity.Output("Rarity", FieldTypeSchema.Int(0, RewardRarity.Quest));
        var rarityNext = rarity.Output("Next", FieldTypeSchema.Int(0, BitsMax));

        var count = package.ComputeAsset(RewardCompute.CardCountAssetName);
        count.BackendId = BaseSpireComputeBackend.BackendId;
        count.SourcePath = RewardCompute.SourcePath;
        count.SourceHash = RewardCompute.SourceHash;
        count.Entry = RewardCompute.CardCountEntry;
        var countSpec = count.Input("Spec", FieldTypeSchema.String(PoolSpecLength));
        var countRarity = count.Input("Rarity", FieldTypeSchema.Int(0, RewardRarity.Quest));
        var countIdxA = count.Input("IdxA", FieldTypeSchema.Int(0, PoolIndexMax));
        var countIdxB = count.Input("IdxB", FieldTypeSchema.Int(0, PoolIndexMax));
        var countOut = count.Output("Count", FieldTypeSchema.Int(0, 512));

        var pick = package.ComputeAsset(RewardCompute.CardPickAssetName);
        pick.BackendId = BaseSpireComputeBackend.BackendId;
        pick.SourcePath = RewardCompute.SourcePath;
        pick.SourceHash = RewardCompute.SourceHash;
        pick.Entry = RewardCompute.CardPickEntry;
        var pickSpec = pick.Input("Spec", FieldTypeSchema.String(PoolSpecLength));
        var pickRarity = pick.Input("Rarity", FieldTypeSchema.Int(0, RewardRarity.Quest));
        var pickIndex = pick.Input("Index", FieldTypeSchema.Int(0, 512));
        var pickIdxA = pick.Input("IdxA", FieldTypeSchema.Int(0, PoolIndexMax));
        var pickIdxB = pick.Input("IdxB", FieldTypeSchema.Int(0, PoolIndexMax));
        var pickOut = pick.Output("PoolIndex", FieldTypeSchema.Int(0, PoolIndexMax));

        var upgrade = package.ComputeAsset(RewardCompute.CardUpgradeAssetName);
        upgrade.BackendId = BaseSpireComputeBackend.BackendId;
        upgrade.SourcePath = RewardCompute.SourcePath;
        upgrade.SourceHash = RewardCompute.SourceHash;
        upgrade.Entry = RewardCompute.CardUpgradeEntry;
        var upBits = upgrade.Input("Bits", FieldTypeSchema.Int(0, RewardCompute.Span - 1));
        var upSpec = upgrade.Input("Spec", FieldTypeSchema.String(PoolSpecLength));
        var upIndex = upgrade.Input("PoolIndex", FieldTypeSchema.Int(0, PoolIndexMax));
        var upAct = upgrade.Input("ActIndex", FieldTypeSchema.Int(0, 8));
        var upOut = upgrade.Output("Upgraded", FieldTypeSchema.Bool());

        return new Assets(
            drop, dropBits, dropCurrent, dropIsElite, dropDropped, dropNext,
            rarity, rarityBits, raritySpec, rarityCurrent, rarityIdxA, rarityIdxB, rarityBoss, rarityOut, rarityNext,
            count, countSpec, countRarity, countIdxA, countIdxB, countOut,
            pick, pickSpec, pickRarity, pickIndex, pickIdxA, pickIdxB, pickOut,
            upgrade, upBits, upSpec, upIndex, upAct, upOut);
    }

    /// <summary>
    /// 奖励池对账：**源声明顺序**（`IroncladRewardPool.SourceOrder`）∩ 内容 `pools = { IroncladCardPool }`。
    /// 缺项、多项、重复、稀有度不认识、普通/升级稀有度不一致、可抽稀有度成员太少都**具名失败**——
    /// 不静默省略未加载的源卡（简报：源 CardPool 未加载内容不得静默省略）。
    /// </summary>
    private static IReadOnlyList<(CardDefinition Card, RewardPoolEntry Entry)> BuildPool(IReadOnlyList<CardDefinition> cards)
    {
        var byId = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            var id = ShortId(card.Id);
            if (!byId.TryAdd(id, card))
            {
                throw new InvalidOperationException($"base-spire: 奖励牌池对账失败：内容里有重复的卡 id '{id}'");
            }
        }

        var effective = new List<string>();
        foreach (var id in IroncladRewardPool.SourceOrder)
        {
            if (!IroncladRewardPool.MultiplayerOnly.Contains(id, StringComparer.Ordinal))
            {
                effective.Add(id);
            }
        }

        foreach (var id in IroncladRewardPool.MultiplayerOnly)
        {
            if (!byId.TryGetValue(id, out var card) || !card.MultiplayerOnly)
            {
                throw new InvalidOperationException(
                    $"base-spire: 奖励牌池对账失败：源侧 multiplayerOnly 的 '{id}' 在内容里不是 multiplayerOnly " +
                    "（87 项源清单与 85 项单机池的差额必须只有这一种来源）");
            }
        }

        var declared = new List<string>();
        foreach (var card in cards)
        {
            if (card.Pools.Contains(IroncladCardPoolName, StringComparer.Ordinal))
            {
                declared.Add(ShortId(card.Id));
            }
        }

        var missing = effective.Where(id => !declared.Contains(id, StringComparer.Ordinal)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"base-spire: 奖励牌池对账失败：源单机清单有 {missing.Count} 项没有进内容池（缺卡或没声明 " +
                $"pools = {{ {IroncladCardPoolName} }}）：{string.Join(", ", missing)}");
        }

        var extra = declared.Where(id => !effective.Contains(id, StringComparer.Ordinal)).ToList();
        if (extra.Count > 0)
        {
            throw new InvalidOperationException(
                $"base-spire: 奖励牌池对账失败：内容声明进 {IroncladCardPoolName} 但有 {extra.Count} 项不在源单机清单里：" +
                string.Join(", ", extra));
        }

        var entries = new List<(CardDefinition Card, RewardPoolEntry Entry)>();
        var buckets = new int[RewardRarity.Quest + 1];
        for (var index = 0; index < effective.Count; index++)
        {
            var card = byId[effective[index]];
            var rarity = RarityOf(card);
            entries.Add((card, new RewardPoolEntry(index, rarity, card.Upgradeable)));
            buckets[rarity]++;
        }

        foreach (var rarity in new[] { RewardRarity.Common, RewardRarity.Uncommon, RewardRarity.Rare })
        {
            // 源 `CardFactory` 的黑色单最多排除 2 张（3 候选），少于等于 2 项会让 `GetNextAllowedRarity` 上卷/返回 None。
            if (buckets[rarity] <= CardOptionCount)
            {
                throw new InvalidOperationException(
                    $"base-spire: 奖励牌池对账失败：稀有度 {rarity} 只有 {buckets[rarity]} 项 ≤ {CardOptionCount}，" +
                    "三候选的黑名单会抽空该稀有度（源此时会抛 InvalidOperationException）");
            }
        }

        return entries;
    }

    private static int RarityOf(CardDefinition card)
    {
        if (!string.Equals(card.Normal.Rarity, card.Upgraded.Rarity, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"base-spire: 奖励牌池对账失败：卡 '{card.Id}' 的普通/升级稀有度不一致（'{card.Normal.Rarity}' vs " +
                $"'{card.Upgraded.Rarity}'）；源的稀有度是卡模型的属性，不随形态变");
        }

        var rarity = ParseRarity(card.Normal.Rarity);
        if (rarity == RewardRarity.None)
        {
            throw new InvalidOperationException(
                $"base-spire: 奖励牌池对账失败：卡 '{card.Id}' 的稀有度 '{card.Normal.Rarity}' 不认识");
        }

        return rarity;
    }

    private static int ParseRarity(string value) => value.Trim().ToLowerInvariant() switch
    {
        "basic" => RewardRarity.Basic,
        "common" => RewardRarity.Common,
        "uncommon" => RewardRarity.Uncommon,
        "rare" => RewardRarity.Rare,
        "ancient" => RewardRarity.Ancient,
        "event" => RewardRarity.Event,
        "token" => RewardRarity.Token,
        "status" => RewardRarity.Status,
        "curse" => RewardRarity.Curse,
        "quest" => RewardRarity.Quest,
        _ => RewardRarity.None,
    };

    /// <summary>`base-spire:strike-ironclad` → `strike-ironclad`（与 `IroncladRewardPool` 的 kebab 口径对齐）。</summary>
    private static string ShortId(string id)
    {
        var separator = id.LastIndexOf(':');
        return separator < 0 ? id : id[(separator + 1)..];
    }

    /// <summary>5 个奖励计算资产的 IR 字段符号（输入/输出）。</summary>
    private sealed record Assets(
        ComputeAssetSymbol PotionDrop,
        FieldSymbol DropBits,
        FieldSymbol DropCurrent,
        FieldSymbol DropIsElite,
        FieldSymbol DropDropped,
        FieldSymbol DropNext,
        ComputeAssetSymbol CardRarity,
        FieldSymbol RarityBits,
        FieldSymbol RaritySpec,
        FieldSymbol RarityCurrent,
        FieldSymbol RarityIdxA,
        FieldSymbol RarityIdxB,
        FieldSymbol RarityBoss,
        FieldSymbol RarityOut,
        FieldSymbol RarityNext,
        ComputeAssetSymbol CardCount,
        FieldSymbol CountSpec,
        FieldSymbol CountRarity,
        FieldSymbol CountIdxA,
        FieldSymbol CountIdxB,
        FieldSymbol CountOut,
        ComputeAssetSymbol CardPick,
        FieldSymbol PickSpec,
        FieldSymbol PickRarity,
        FieldSymbol PickIndex,
        FieldSymbol PickIdxA,
        FieldSymbol PickIdxB,
        FieldSymbol PickOut,
        ComputeAssetSymbol CardUpgrade,
        FieldSymbol UpBits,
        FieldSymbol UpSpec,
        FieldSymbol UpIndex,
        FieldSymbol UpAct,
        FieldSymbol UpOut);
}
