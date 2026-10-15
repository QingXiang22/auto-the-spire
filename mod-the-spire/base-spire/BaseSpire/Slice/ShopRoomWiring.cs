using System;
using System.Collections.Generic;
using System.Linq;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using BaseSpire.Slice.Domain.Run;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// ENV-T09.2：商店（`sts2/src/Core/Rooms/MerchantRoom.cs` + `Core/Entities/Merchant/*`）的正式机制模块。
///
/// 这是**正式组包路径**里的生产模块（不是隔离原型）：<see cref="Compilation.RoomCompilation"/> 在
/// `RoomServices` 里构造它，`Rooms` 把它的 <see cref="Enter"/> 接进地图 `ChooseMapPoint` 的 Shop 分支，
/// 并注册 5 个动作。宿主接缝全部走已有符号：
/// <see cref="RoomCompileContext"/>（Run/Runs/Hp/MaxHp/Gold/长期牌组/地图相位与当前节点）、
/// 奖励子包的 <c>RewardCardOdds</c> 字段（商店稀有度偏移与奖励**同一个**持久字段）、
/// <see cref="RunInitializationWiring"/> 的玩家/共享遗物袋、<see cref="PotionInventoryRules"/> 的药水栏。
///
/// 所有状态（库存/已售/不可买原因/累计删牌价计数）都在 Engine 类型化字段里：纯计算后端不存运行状态，
/// 也没有第二个 RunState 或调度器。
///
/// 机制与源一一对应（逐条依据见 `.audit/dsh-shop-flow-2026-10-11/source-facts.md`）：
/// <list type="bullet">
/// <item>`MerchantInventory.CreateForNormalMerchant` 的商品顺序与抽数顺序：5 张彩卡
///       （先 `Shops.NextInt(5)` 抽折扣格）、2 张无色、3 件遗物（**先**抽两次稀有度）、3 瓶药水（3×2 抽后 3 次价格抽）；</item>
/// <item>彩卡：`Rewards` 稀有度 roll → `Shops` 选卡 → `Rewards` 升级 roll（源恒不升级）→ `Shops` 价格 →（在售格再抽一次价格并整除 2）；</item>
/// <item>无色卡：按 `ColorlessCardPool.GenerateAllCards()` 的**源顺序**做一次均匀抽取，固定 uncommon/rare，无稀有度 roll；</item>
/// <item>遗物：袋尾取货 + 空袋按 `Shop→Common→Uncommon→Rare→None` 回退，全空回退 `base-spire:circlet`（价格 1）；</item>
/// <item>药水：`Shops` 稀有度 roll（`&lt;=0.1` Rare、`&lt;=0.35` Uncommon）+ 同池不重复的 pick + `Shops` 价格；</item>
/// <item>删牌：`BaseCost + PriceIncrease * CardShopRemovalsUsed`，一次一间只能删一张。</item>
/// </list>
///
/// **普通拒绝不 Fault 整局**：买不起、满栏、已售、不可买、不可删都是**候选域 + 守卫**（`Requires`）拒绝，
/// 由引擎在提交前抛 `Faults.Illegal`（`Scheduler.Submit` 的宿主输入拒绝路径），会话保持 Running、
/// 状态逐字段不变、仍然可以离店。规则 IR 里的 `Fail` 只留给源侧也会抛异常的**内容不完整**边界。
///
/// **没有正式效果声明的商品不伪装可买，也不从清单里消失**：`Unsupported = true` +
/// `UnsupportedReason` 写明原因（源成员尚无内容定义 / 该稀有度在内容里没有成员 / 遗物效果未登记），
/// 商品保留完整的源身份与数量，购买被守卫拒绝。
/// </summary>
public sealed class ShopRoomWiring
{
    // -- 公开面（动作/过程/实体名字：正式集成与验证程序都按这些常量对接） --------------------------------------

    public const string OfferEntityName = "ShopOffer";
    public const string OffersCollectionName = "ShopOffers";
    public const string RemovalsUsedFieldName = "ShopCardRemovalsUsed";

    public const string BuyCardActionName = "BuyShopCard";
    public const string BuyRelicActionName = "BuyShopRelic";
    public const string BuyPotionActionName = "BuyShopPotion";
    public const string RemoveCardActionName = "RemoveShopCard";
    public const string LeaveActionName = "LeaveShop";

    public const string CreateCardProcedureName = "CreateShopCard";
    public const string ResolveCardProcedureName = "ResolveShopCard";
    public const string CreatePotionProcedureName = "CreateShopPotion";
    public const string ResolvePotionProcedureName = "ResolveShopPotion";

    public const string OfferParameterName = "Offer";
    public const string DeckCardParameterName = "Card";

    /// <summary>货架容量：14 个真实商品位 + 余量（源 `MerchantInventory` 生成 5+2+3+3+1）。</summary>
    public const int OfferCapacity = 16;

    public const int ColoredCardEntryCount = 5;
    public const int ColorlessCardEntryCount = 2;
    public const int RelicEntryCount = 3;
    public const int PotionEntryCount = 3;
    public const int RemovalEntryCount = 1;
    public const int TotalEntryCount = ColoredCardEntryCount + ColorlessCardEntryCount + RelicEntryCount + PotionEntryCount + RemovalEntryCount;

    /// <summary>源 `_coloredCardTypes = [Attack, Attack, Skill, Skill, Power]`。</summary>
    public static readonly IReadOnlyList<string> ColoredCardTypes = ["Attack", "Attack", "Skill", "Skill", "Power"];

    public const string CardOfferKind = "card";    public const string RelicOfferKind = "relic";
    public const string PotionOfferKind = "potion";
    public const string RemovalOfferKind = "removal";

    /// <summary>`ShopOffer.PoolKind`：彩卡 1..3（Attack/Skill/Power 各一条池），无色 4。</summary>
    public const int ColoredPoolKindBase = 1;
    public const int ColorlessPoolKind = 4;

    public const string CircletId = "base-spire:circlet";
    public const string ColorlessCardPoolName = "ColorlessCardPool";
    public const string IroncladCardPoolName = "IroncladCardPool";
    public const string ShopPointTypeMember = "Shop";

    /// <summary>正常路径（各稀有度桶非空）每间商店的抽数：`Shops` 27、`Rewards` 14。
    /// 源 `MerchantInventory.CreateForNormalMerchant` 的逐笔来源：折扣格 1；每张彩卡 `NextItem` + 价 各 1；
    /// 每张无色卡 `NextItem` 1（无色无稀有度 roll、无 `GetNextAllowedRarity`）；每件遗物价 1
    /// （遗物稀有度走 Rewards）；每瓶药水 pick 1 + 价 1。在售格**不重复**抽价（源 `SetOnSale` 只把价格整除 2）。
    /// 退化格（该稀有度桶为空）不抽 `NextItem`，因此实测抽数会少一笔，且该格仍作为完整商品位标不可买。</summary>
    public const int ExpectedShopsDraws = 27;
    public const int ExpectedRewardsDraws = 14;

    /// <summary>普通拒绝的守卫语义串（提交时由引擎抛 `Illegal`，不改状态、不结束会话）。</summary>
    public const string NotEnoughGoldMessage = "base-spire: 金币不足，商店购买被拒绝（状态不变）";
    public const string FullBeltMessage = "base-spire: 药水栏已满（3/3），购买被拒绝（状态不变）";

    /// <summary>不可买商品的 `UnsupportedReason`（条目自己写明；购买由守卫拒绝，不 Fault）。</summary>
    public const string ColorlessMissingContentReasonPrefix = "base-spire: 源无色池成员 '";
    public const string ColorlessMissingContentReasonSuffix = "' 尚无内容定义，不可购买（保留源身份与数量，不用别的卡顶替）";
    public const string ColoredBucketReason = "base-spire: 彩卡池在该稀有度下没有可用成员（源侧此时会抛异常），该商品不可购买";
    public const string ColorlessBucketReason = "base-spire: 无色池抽到的源成员尚无内容定义或稀有度不符（不可购买；保留源身份与数量）";
    public const string PotionBucketReason = "base-spire: 药水池在该稀有度下没有内容成员，该商品不可购买";
    public const string UnregisteredRelicReasonPrefix = "base-spire: 商店遗物 '";
    public const string UnregisteredRelicReasonSuffix = "' 未在正式登记表（SpireRelics）里声明可实现效果，不可购买——交 owner 裁决";

    /// <summary>商店货架字段符号（宿主投影/验证程序直接按它读条目）。</summary>
    public sealed record ShopOfferFields(
        EntityTypeSymbol Type,
        FieldSymbol Slot,
        FieldSymbol Kind,
        FieldSymbol DefinitionId,
        FieldSymbol SourceName,
        FieldSymbol Rarity,
        FieldSymbol PoolKind,
        FieldSymbol PoolIndex,
        FieldSymbol OnSale,
        FieldSymbol Price,
        FieldSymbol Sold,
        FieldSymbol Unsupported,
        FieldSymbol UnsupportedReason);

    // -- 内部类型 ---------------------------------------------------------------------------------------------

    /// <summary>池成员。`Index` 是**该池自己的下标**（彩卡 0..84 按源单机清单、无色 0..63 按源无色清单）。</summary>
    private sealed record PoolCard(int Index, int PoolKind, CardDefinition? Card, string SourceName, int Rarity, string Kind);

    /// <summary>`Enter` 里一次性分配、逐个商品位复用的局部变量。</summary>
    private sealed class Locals
    {
        public int Bits;
        public int Ordinal;
        public int RarityText;
        public int Count;
        public int Pick;
        public int Index;
        public int Price;
        public int Upgraded;
        public int IdText;
        public int SourceName;
        public int OnSaleSlot;
        public int Reason;
        public int Created;
        public int Available;
        public int[] Exclusions = [];
        public int[] RelicRarities = [];
        public int[] PotionOrdinals = [];
        public int[] PotionRarityTexts = [];
        public int[] PotionIndices = [];
        public int[] PotionExclusions = [];
    }

    // -- 字段 -------------------------------------------------------------------------------------------------

    private readonly RoomCompileContext room;
    private readonly MapRoomWiring map;
    private readonly CardDefinitions cardRecipes;
    private readonly RunDeck deck;
    private readonly RunResources resources;
    private readonly RunRelics relicCommands;
    private FieldSymbol? cardRarityOffset;    private readonly PotionInventoryRules potions;

    private readonly string shopsStream = VanillaRandomLayout.EnginePlayerStreamName("Shops");
    private readonly string rewardsStream = VanillaRandomLayout.EnginePlayerStreamName("Rewards");

    private readonly IReadOnlyList<PoolCard> createTable;
    private readonly IReadOnlyDictionary<string, string> coloredSpecs;
    private readonly IReadOnlyList<PoolCard> colorlessPool;
    private readonly string potionSpec;
    private readonly FieldSymbol playerBagEntryId;
    private readonly FieldSymbol sharedBagEntryId;
    private readonly IReadOnlyList<SliceRelic> buyableRelics;

    private readonly ShopCardRarityAsset cardRarity;
    private readonly ShopPoolAsset cardCount;
    private readonly ShopPoolAsset cardPick;
    private readonly ShopCardPriceAsset cardPrice;
    private readonly ShopCardUpgradeAsset cardUpgrade;
    private readonly ShopRelicRarityAsset relicRarityAsset;
    private readonly ShopRelicPriceAsset relicPrice;
    private readonly ShopPotionRarityAsset potionRarity;
    private readonly ShopPotionPriceAsset potionPrice;

    private readonly ProcedureSymbol resolveCard;
    private readonly ProcedureSymbol createCard;
    private readonly ProcedureSymbol resolvePotion;
    private readonly ProcedureSymbol createPotion;

    /// <summary>货架集合。</summary>
    public CollectionSymbol Offers { get; }

    /// <summary>货架字段。</summary>
    public ShopOfferFields Offer { get; }

    /// <summary>Run 上的删牌次数计数（源 `Player.ExtraFields.CardShopRemovalsUsed`）。</summary>
    public FieldSymbol RemovalsUsed { get; }

    /// <summary>商店相位值（= <see cref="MapRoomWiring.ShopPhase"/>，声明上界的一部分）。</summary>
    public long ShopPhase => MapRoomWiring.ShopPhase;

    public ShopRoomWiring(
        EnginePackageBuilder package,
        RoomCompileContext room,
        RunDeck deck,
        RunInitializationWiring inventory,
        PotionInventoryRules potions,
        RunResources resources,
        RunRelics relicCommands,
        string backendId = BaseSpireComputeBackend.BackendId)
    {
        ArgumentNullException.ThrowIfNull(package);
        this.room = room ?? throw new ArgumentNullException(nameof(room));
        map = room.Map;
        this.deck = deck;
        cardRecipes = deck.Definitions;
        this.potions = potions ?? throw new ArgumentNullException(nameof(potions));
        this.resources = resources;
        this.relicCommands = relicCommands;
        // 奖励子包的持久赔率字段（`RewardWiring.CardOddsFieldName`）在 `RewardCompilation.Rewards` 里声明，
        // 比本模块的构造点晚；这里只记下名字，第一次生成库存/注册动作时再解析符号。
        this.cardRarityOffset = null;

        // 池与下标：彩卡照 `RewardWiring.BuildPool` 的口径对账（源单机清单 = `IroncladRewardPool.SourceOrder`）；
        // 无色按 `ColorlessShopPool.SourceOrder` 的**源顺序**（64 项，含尚无内容的成员）。
        var colored = BuildColoredPool(cardRecipes.Compiled);
        var colorless = BuildColorlessPool(cardRecipes.Compiled);
        var table = new List<PoolCard>();
        var specs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kind in new[] { "Attack", "Skill", "Power" })
        {
            var members = colored.Where(card => string.Equals(card.Kind, kind, StringComparison.Ordinal)).ToList();
            if (members.Count == 0)
            {
                throw Faults.Schema($"base-spire: 商店彩卡池里没有 '{kind}' 成员（内容池不完整）");
            }

            specs[kind] = Encode(members);
            table.AddRange(members);
        }

        table.AddRange(colorless);
        coloredSpecs = specs;
        createTable = table;
        colorlessPool = colorless;
        potionSpec = RewardPoolSpec.Encode(potions.Pool.Select((definition, index) => new RewardPoolEntry(index, PotionRarityOrdinal(definition.Rarity), false)));
        buyableRelics = relicCommands.Supported;

        // 实体与集合。
        var offerType = package.EntityType(OfferEntityName);
        Offer = new ShopOfferFields(
            offerType,
            offerType.AddField("Slot", FieldTypeSchema.Int(0, OfferCapacity - 1)),
            offerType.AddField("Kind", FieldTypeSchema.String(16)),
            offerType.AddField("DefinitionId", FieldTypeSchema.String(64)),
            offerType.AddField("SourceName", FieldTypeSchema.String(64)),
            offerType.AddField("Rarity", FieldTypeSchema.String(16)),
            offerType.AddField("PoolKind", FieldTypeSchema.Int(-1, ColorlessPoolKind)),
            offerType.AddField("PoolIndex", FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex)),
            offerType.AddField("OnSale", FieldTypeSchema.Bool()),
            offerType.AddField("Price", FieldTypeSchema.Int(0, 999999999)),
            offerType.AddField("Sold", FieldTypeSchema.Bool()),
            offerType.AddField("Unsupported", FieldTypeSchema.Bool()),
            offerType.AddField("UnsupportedReason", FieldTypeSchema.String(512)));
        Offers = package.Collection(OffersCollectionName, offerType);
        Offers.Capacity = OfferCapacity;
        // 商品是当前公开事实（能买什么/价格/是否已售），因此货架实体进观看者投影。
        room.Map.Projection.Type(offerType);

        // Run 侧长期状态。
        RemovalsUsed = room.RunType.AddField(RemovalsUsedFieldName, FieldTypeSchema.Int(0, 999));

        // 纯计算资产。
        cardRarity = ShopCompute.DeclareCardRarity(package, backendId);
        cardCount = ShopCompute.DeclareCardCount(package, backendId);
        cardPick = ShopCompute.DeclareCardPick(package, backendId);
        cardPrice = ShopCompute.DeclareCardPrice(package, backendId);
        cardUpgrade = ShopCompute.DeclareCardUpgrade(package, backendId);
        relicRarityAsset = ShopCompute.DeclareRelicRarity(package, backendId);
        relicPrice = ShopCompute.DeclareRelicPrice(package, backendId);
        potionRarity = ShopCompute.DeclarePotionRarity(package, backendId);
        potionPrice = ShopCompute.DeclarePotionPrice(package, backendId);

        // 建牌/建药水/解析 id 的过程。
        resolveCard = DeclareResolveCard(package);
        createCard = DeclareCreateCard(package);
        resolvePotion = DeclareResolvePotion(package);
        createPotion = DeclareCreatePotion(package);

        // 遗物接缝：袋成员 id 字段与玩家/共享袋（源 `RelicFactory.PullNextRelicFromBack`）。
        RelicType = relicCommands.Schema.Type;
        RelicDefinition = relicCommands.Schema.Definition;
        RelicRarity = relicCommands.Schema.Rarity;
        Relics = relicCommands.Schema.Owned;
        PlayerBags = inventory.PlayerBags;
        SharedBags = inventory.SharedBags;
        // Player and shared bags are different entity types in source (and in `RunInitializationWiring`), so each
        // needs its own id field symbol.
        playerBagEntryId = inventory.PlayerId;
        sharedBagEntryId = inventory.SharedId;
    }

    /// <summary>正式遗物实体/字段/集合（买遗物动作建持有实例时复用，不另建实体类型）。</summary>
    public EntityTypeSymbol RelicType { get; }

    public FieldSymbol RelicDefinition { get; }

    public FieldSymbol RelicRarity { get; }

    public CollectionSymbol Relics { get; }

    /// <summary>玩家袋（含 `Shop` 档）与共享袋（`Common/Uncommon/Rare`）。</summary>
    public IReadOnlyDictionary<string, CollectionSymbol> PlayerBags { get; }

    public IReadOnlyDictionary<string, CollectionSymbol> SharedBags { get; }

    // -- 进入商店 ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 生成整间商店（源 `MerchantInventory.CreateForNormalMerchant` 的完整顺序）。
    /// 宿主把 `Run.CurrentNode` 指到商店节点之后调用；相位在这里置成 <see cref="MapRoomWiring.ShopPhase"/>。
    /// 重复进入会先清空上一轮货架（源每次都新建一份 inventory）。
    /// </summary>
    public Inst[] Enter(IrScope s, Expr runExpr)
    {
        ArgumentNullException.ThrowIfNull(s);
        var code = new List<Inst>
        {
            s.ForEach(s.Collection(Offers), offer => new Inst[] { s.Destroy(offer) }),
            s.SetField(runExpr, map.RunMapPhase, s.Const(ShopPhase)),
        };

        var l = new Locals
        {
            Bits = s.Local("shopBits", FieldTypeSchema.Int(0, RewardCompute.Span - 1)),
            Ordinal = s.Local("shopOrdinal", FieldTypeSchema.Int(0, 10)),
            RarityText = s.Local("shopRarityText", FieldTypeSchema.String(16)),
            Count = s.Local("shopCount", FieldTypeSchema.Int(0, Math.Max(ColorlessShopPool.Count, RewardCompute.MaxPoolIndex + 1))),
            Pick = s.Local("shopPick", FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex)),
            Index = s.Local("shopIndex", FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex)),
            Price = s.Local("shopPrice", FieldTypeSchema.Int(0, 999999999)),
            Upgraded = s.Local("shopUpgraded", FieldTypeSchema.Bool()),
            IdText = s.Local("shopDefinitionId", FieldTypeSchema.String(64)),
            SourceName = s.Local("shopSourceName", FieldTypeSchema.String(64)),
            OnSaleSlot = s.Local("shopOnSaleSlot", FieldTypeSchema.Int(0, ColoredCardEntryCount - 1)),
            Reason = s.Local("shopUnsupportedReason", FieldTypeSchema.String(512)),
            Created = s.Local("shopOfferCreated", FieldTypeSchema.Binding()),
            Available = s.Local("shopCardAvailable", FieldTypeSchema.Bool()),
        };
        l.Exclusions = Allocate(s, code, "shopExclusion", ShopCompute.ExclusionCount);
        l.RelicRarities = Allocate(s, code, "shopRelicRarity", RelicEntryCount - 1, FieldTypeSchema.String(16));
        l.PotionOrdinals = Allocate(s, code, "shopPotionOrdinal", PotionEntryCount, FieldTypeSchema.Int(0, 10));
        l.PotionRarityTexts = Allocate(s, code, "shopPotionRarity", PotionEntryCount, FieldTypeSchema.String(16));
        l.PotionIndices = Allocate(s, code, "shopPotionIndex", PotionEntryCount);
        l.PotionExclusions = Allocate(s, code, "shopPotionExclusion", ShopCompute.ExclusionCount);

        // 1) 折扣格：源 `Player.PlayerRng.Shops.NextInt(_coloredCardTypes.Length)`（5 格彩卡之一）。
        code.Add(s.SampleInt(shopsStream, s.Const(0L), s.Const((long)ColoredCardTypes.Count), l.OnSaleSlot));

        // 2) 5 张彩卡（每类一条池；下标是该池自己的源清单下标，跨类排除项天然惰性）。
        var cursor = 0;
        for (var slot = 0; slot < ColoredCardTypes.Count; slot++)
        {
            var kind = ColoredCardTypes[slot];
            EmitColoredCard(s, code, l, slot, kind, ColoredPoolKindBase + IndexOfColoredKind(kind), ref cursor);
        }

        // 3) 2 张无色卡（源顺序均匀抽取，固定 uncommon / rare，没有稀有度 roll）。
        for (var i = 0; i < ColorlessCardEntryCount; i++)
        {
            EmitColorlessCard(s, code, l, ColoredCardTypes.Count + i, i == 0 ? RewardRarity.Uncommon : RewardRarity.Rare, ref cursor);
        }

        // 4) 遗物：源先求值两次 RollRarity（Rewards），第三格固定 Shop（不抽稀有度）。
        for (var i = 0; i < l.RelicRarities.Length; i++)
        {
            code.Add(s.SampleInt(rewardsStream, s.Const(0L), s.Const(RewardCompute.Span), l.Bits));
            code.Add(s.Compute(
                relicRarityAsset.Asset,
                [(relicRarityAsset.Bits, s.LocalRef(l.Bits))],
                [(relicRarityAsset.Rarity, l.RelicRarities[i])]));
        }

        var relicSlot = ColoredCardTypes.Count + ColorlessCardEntryCount;
        for (var i = 0; i < RelicEntryCount; i++)
        {
            EmitRelic(s, code, l, relicSlot + i, i < l.RelicRarities.Length ? s.LocalRef(l.RelicRarities[i]) : s.Const("Shop"));
        }

        // 5) 药水：源先 `CreateRandomPotionsOutOfCombat(Player, 3, Shops)` 生成 3 瓶（稀有度 + pick），
        //    再逐个建条目（每个条目构造时抽一次价格）。
        for (var i = 0; i < PotionEntryCount; i++)
        {
            code.Add(s.SampleInt(shopsStream, s.Const(0L), s.Const(RewardCompute.Span), l.Bits));
            code.Add(s.Compute(
                potionRarity.Asset,
                [(potionRarity.Bits, s.LocalRef(l.Bits))],
                [(potionRarity.Ordinal, l.PotionOrdinals[i]), (potionRarity.Rarity, l.PotionRarityTexts[i])]));
            EmitPotionPick(s, code, l, i);
        }

        var potionSlot = relicSlot + RelicEntryCount;
        for (var i = 0; i < PotionEntryCount; i++)
        {
            code.Add(s.SampleInt(shopsStream, s.Const(0L), s.Const(RewardCompute.Span), l.Bits));
            code.Add(s.Compute(
                potionPrice.Asset,
                [(potionPrice.Ordinal, s.LocalRef(l.PotionOrdinals[i])), (potionPrice.Bits, s.LocalRef(l.Bits))],
                [(potionPrice.Price, l.Price)]));
            code.Add(s.Call(resolvePotion, l.IdText, s.LocalRef(l.PotionIndices[i])));
            code.Add(s.SetLocal(l.Reason, s.Const(string.Empty)));
            code.Add(s.If(
                s.Equal(s.LocalRef(l.PotionIndices[i]), s.Const((long)RewardCompute.NoPoolIndex)),
                s.SetLocal(l.Reason, s.Const(PotionBucketReason))));
            code.Add(CreateOffer(
                s,
                l,
                potionSlot + i,
                PotionOfferKind,
                s.LocalRef(l.IdText),
                s.Const(string.Empty),
                s.LocalRef(l.PotionRarityTexts[i]),
                s.Const(-1L),
                s.LocalRef(l.PotionIndices[i]),
                s.Const(false),
                s.LocalRef(l.Price),
                s.Equal(s.LocalRef(l.PotionIndices[i]), s.Const((long)RewardCompute.NoPoolIndex))));
        }

        // 6) 删牌：源 `MerchantCardRemovalEntry.CalcCost()` = `BaseCost + PriceIncrease * CardShopRemovalsUsed`（无抽数）。
        code.Add(s.SetLocal(l.Reason, s.Const(string.Empty)));
        code.Add(CreateOffer(
            s,
            l,
            potionSlot + PotionEntryCount,
            RemovalOfferKind,
            s.Const(string.Empty),
            s.Const(string.Empty),
            s.Const(string.Empty),
            s.Const(-1L),
            s.Const(-1L),
            s.Const(false),
            s.Add(
                s.Const(ShopCompute.RemovalBasePrice),
                s.Multiply(s.Const(ShopCompute.RemovalPriceIncrease), s.Field(runExpr, RemovalsUsed))),
            s.Const(false)));

        return code.ToArray();
    }

    // -- 动作注册 ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 注册 5 个动作：买卡 / 买遗物 / 买药水 / 删牌 / 离店。
    /// 五个动作的守卫都要求"地图相位 = 商店相位 且 当前节点类型是商店"，因此战斗期（相位 1）无法购买。
    /// 买不起 / 满栏 / 已售 / 不可买都不在候选域里，且守卫再次拒绝；拒绝不改状态、不结束会话。
    /// </summary>
    public void RegisterActions(EnginePackageBuilder package)
    {
        ArgumentNullException.ThrowIfNull(package);

        // 买卡：源 `MerchantCardEntry.OnTryPurchase`（先把牌放进长期牌组，再扣一次金币）。
        {
            var action = package.Action(BuyCardActionName);
            var body = package.ActionBody(action);
            var parameter = body.Param(OfferParameterName, FieldTypeSchema.RefTo(Offer.Type));
            var runExpr = room.CurrentRun(body);
            var offer = body.Param(parameter.DeclarationIndex);
            body.Role(SliceManifest.PlayerRole);
            body.Candidates(OfferParameterName, BuyableOffers(body, CardOfferKind, runExpr, requiresBeltSpace: false));
            body.Requires(BuyGuard(body, offer, runExpr, CardOfferKind, requiresBeltSpace: false));
            body.Body([
                body.Call(createCard, body.Field(offer, Offer.PoolKind), body.Field(offer, Offer.PoolIndex)),
                .. resources.LoseGold(body, runExpr, body.Field(offer, Offer.Price)),
                body.SetField(offer, Offer.Sold, body.Const(true))]);
        }

        // 买遗物：源 `MerchantRelicEntry.OnTryPurchase`（先扣金币，再 `RelicCmd.Obtain` + AfterObtained 效果）。
        {
            var action = package.Action(BuyRelicActionName);
            var body = package.ActionBody(action);
            var parameter = body.Param(OfferParameterName, FieldTypeSchema.RefTo(Offer.Type));
            var runExpr = room.CurrentRun(body);
            var offer = body.Param(parameter.DeclarationIndex);
            body.Role(SliceManifest.PlayerRole);
            body.Candidates(OfferParameterName, BuyableOffers(body, RelicOfferKind, runExpr, requiresBeltSpace: false));
            body.Requires(BuyGuard(body, offer, runExpr, RelicOfferKind, requiresBeltSpace: false));
            body.Body([.. resources.LoseGold(body, runExpr, body.Field(offer, Offer.Price)),
                .. relicCommands.Obtain(body, runExpr, body.Field(offer, Offer.DefinitionId)),
                body.SetField(offer, Offer.Sold, body.Const(true))]);
        }

        // 买药水：源 `MerchantPotionEntry.OnTryPurchase`（`PotionCmd.TryToProcure` → 满栏失败 → 扣金币）。
        {
            var action = package.Action(BuyPotionActionName);
            var body = package.ActionBody(action);
            var parameter = body.Param(OfferParameterName, FieldTypeSchema.RefTo(Offer.Type));
            var runExpr = room.CurrentRun(body);
            var offer = body.Param(parameter.DeclarationIndex);
            var purchased = body.Local("purchasedPotion", FieldTypeSchema.RefTo(potions.Entity));
            var code = new List<Inst>
            {
                body.Call(createPotion, purchased, body.Field(offer, Offer.PoolIndex)),
            };
            code.AddRange(potions.Acquire(body, body.LocalRef(purchased)));
            code.Add(body.IfElse(body.GreaterOrEqual(body.CollectionIndex(potions.Belt, body.LocalRef(purchased)), body.Const(0L)),
                [.. resources.LoseGold(body, runExpr, body.Field(offer, Offer.Price)),
                    body.SetField(offer, Offer.Sold, body.Const(true))],
                [body.Destroy(body.LocalRef(purchased))]));
            body.Role(SliceManifest.PlayerRole);
            body.Candidates(OfferParameterName, BuyableOffers(body, PotionOfferKind, runExpr, requiresBeltSpace: true));
            body.Requires(BuyGuard(body, offer, runExpr, PotionOfferKind, requiresBeltSpace: true));
            body.Body(code.ToArray());
        }

        // 删牌：费用在进入商店时按"已删次数"算好（源 `MerchantCardRemovalEntry`），一次一间只能删一张。
        //
        // 引擎的动作只有**一个**候选实体参数（`ActionBuilder.Candidates` 再次调用会覆盖前一次，
        // 见 `Scheduler.Domains`），所以这里不能用"第二个 Candidates"表达要删哪张牌——旧隔离模块正是
        // 那样写，结果商品候选被牌组候选覆盖、同定义两件时删掉的不是玩家选的那件。正式做法：
        // 商品候选 + 守卫判"买得起/能删"，再用**正式 decision**（`IrScope.Decide`，与营火删/选牌同一套
        // 暂停—恢复机制）问要删的长期牌实例；答案由引擎按候选域校验（必须是长期牌组里的真实实例）。
        // 普通拒绝（已售/买不起/空牌组）都走守卫，不 Fault 整局。
        {
            var action = package.Action(RemoveCardActionName);
            var body = package.ActionBody(action);
            var parameter = body.Param(OfferParameterName, FieldTypeSchema.RefTo(Offer.Type));
            var runExpr = room.CurrentRun(body);
            var offer = body.Param(parameter.DeclarationIndex);
            var chosen = body.Local("removedShopCard", FieldTypeSchema.RefTo(cardRecipes.Schema.Type));
            var eligible = body.Local("removalCandidates", FieldTypeSchema.Int(0, 999));
            body.Role(SliceManifest.PlayerRole);
            body.Candidates(OfferParameterName, BuyableOffers(body, RemovalOfferKind, runExpr, requiresBeltSpace: false));
            body.Requires(body.And(
                BuyGuard(body, offer, runExpr, RemovalOfferKind, requiresBeltSpace: false),
                body.Greater(body.Count(body.Collection(room.LongTermDeck)), body.Const(0L))));
            body.Body([
                // 源 `CardSelectCmd.FromDeckForRemoval`：候选就是长期牌组实例；只有真正选到实例才扣金。
                body.SetLocal(eligible, body.Count(body.Collection(room.LongTermDeck))),
                body.Decide(
                    body.Const(Value.OfParticipant(SliceManifest.Player)),
                    DecisionResponseKind.EntityRef,
                    SliceManifest.RemoveCardChoicePrompt,
                    chosen,
                    body.Collection(room.LongTermDeck),
                    body.Const(1L),
                    body.Const(1L)),
                .. resources.LoseGold(body, runExpr, body.Field(offer, Offer.Price)),
                .. deck.Remove(body, body.LocalRef(chosen)),
                body.SetField(runExpr, RemovalsUsed, body.Add(body.Field(runExpr, RemovalsUsed), body.Const(1L))),
                body.SetField(offer, Offer.Sold, body.Const(true))]);
        }

        // 离店：回到地图（源 `MerchantRoom.Exit`；地图历史写入不在本 Slice 模型里，见交接）。
        {
            var action = package.Action(LeaveActionName);
            var body = package.ActionBody(action);
            var runExpr = room.CurrentRun(body);
            body.Role(SliceManifest.PlayerRole);
            body.Requires(room.At(body, runExpr, (int)MapRoomWiring.ShopPhase, ShopPointTypeMember));
            body.Body(room.CloseVisit(body, runExpr));
        }
    }

    // -- 生成细节 ---------------------------------------------------------------------------------------------

    private void EmitColoredCard(IrScope s, List<Inst> code, Locals l, int slot, string kind, int poolKind, ref int cursor)
    {
        var spec = s.Const(coloredSpecs[kind]);
        // 稀有度 roll 走 Rewards（源 `PlayerOddsSet` 用 `rng.Rewards` 构造 `CardRarityOdds`）。
        code.Add(s.SampleInt(rewardsStream, s.Const(0L), s.Const(RewardCompute.Span), l.Bits));
        code.Add(s.Compute(
            cardRarity.Asset,
            [
                (cardRarity.Bits, s.LocalRef(l.Bits)),
                (cardRarity.Spec, spec),
                (cardRarity.OffsetBits, OffsetExpr(s)),
                .. ExclusionInputs(cardRarity.Exclusions, s, l.Exclusions),
            ],
            [(cardRarity.Ordinal, l.Ordinal), (cardRarity.Rarity, l.RarityText), (cardRarity.Available, l.Available)]));
        // 有成员才抽 pick（源 `NextItem` 只在非空列表上抽一笔）；没有成员时按源该格的语义标不可买。
        code.Add(s.SetLocal(l.Index, s.Const((long)RewardCompute.NoPoolIndex)));
        code.Add(s.SetLocal(l.Reason, s.Const(string.Empty)));
        code.Add(s.IfElse(
            s.LocalRef(l.Available),
            [EmitCardPick(s, code, l, spec, l.Ordinal, l.Exclusions)],
            [s.SetLocal(l.Reason, s.Const(ColoredBucketReason))]));
        EmitUpgradeAndPrice(s, code, l, l.Ordinal, colorless: false, onSaleSlot: l.OnSaleSlot, slot: slot);
        code.Add(s.Call(resolveCard, l.IdText, s.Const((long)poolKind), s.LocalRef(l.Index)));
        code.Add(s.SetLocal(l.Available, s.NotEqual(s.LocalRef(l.Index), s.Const((long)RewardCompute.NoPoolIndex))));
        code.Add(CreateOffer(
            s,
            l,
            slot,
            CardOfferKind,
            s.LocalRef(l.IdText),
            s.Const(string.Empty),
            s.LocalRef(l.RarityText),
            s.Const((long)poolKind),
            s.LocalRef(l.Index),
            s.Equal(s.LocalRef(l.OnSaleSlot), s.Const((long)slot)),
            s.LocalRef(l.Price),
            s.Not(s.LocalRef(l.Available))));
        RecordExclusion(s, code, l.Exclusions, l.Index, ref cursor);
    }

    /// <summary>
    /// 无色卡：源 `CardFactory.CreateForMerchant(player, ColorlessCardPool, rarity)`（`CardFactory.cs:69-79`）
    /// **先按固定稀有度过滤源清单**（`options.Where(c =&gt; c.Rarity == rarity)`），再对过滤后的 items 执行
    /// 一次 `Shops.NextItem`；无色无稀有度 roll、不调 `GetNextAllowedRarity`。
    /// 抽取上界是**该稀有度的源成员数**（保源顺序）。抽到尚未进内容的成员时保留源身份（`SourceName` =
    /// 源 kebab 名）并标不可买，不换成别的卡、也不从清单里删掉；可买/不可买都只由"源成员有没有内容"决定，
    /// 不按效果支持度过滤抽取范围。
    /// </summary>
    private void EmitColorlessCard(IrScope s, List<Inst> code, Locals l, int slot, int targetRarity, ref int cursor)
    {
        // 源清单里该稀有度的成员下标（保序）；抽取上界 = 它的长度。
        var bucket = ColorlessShopPool.IndicesAtRarity(RarityText(targetRarity));
        if (bucket.Count == 0)
        {
            throw Faults.Schema($"base-spire: 无色源清单在稀有度 '{RarityText(targetRarity)}' 下没有任何成员");
        }

        code.Add(s.SetLocal(l.Ordinal, s.Const((long)targetRarity)));
        code.Add(s.SetLocal(l.Index, s.Const((long)RewardCompute.NoPoolIndex)));
        code.Add(s.SetLocal(l.IdText, s.Const(string.Empty)));
        code.Add(s.SetLocal(l.SourceName, s.Const(string.Empty)));
        code.Add(s.SetLocal(l.Reason, s.Const(string.Empty)));
        code.Add(s.SampleInt(shopsStream, s.Const(0L), s.Const((long)bucket.Count), l.Pick));
        for (var position = 0; position < bucket.Count; position++)
        {
            var member = colorlessPool[bucket[position]];
            code.Add(s.If(
                s.Equal(s.LocalRef(l.Pick), s.Const((long)position)),
                [
                    s.SetLocal(l.IdText, s.Const(member.Card?.Id ?? string.Empty)),
                    s.SetLocal(l.SourceName, s.Const(member.SourceName)),
                    s.SetLocal(l.Index, s.Const((long)member.Index)),
                    s.SetLocal(l.Reason, s.Const(ColorlessReason(member))),
                ]));
        }

        code.Add(s.If(
            s.Equal(s.LocalRef(l.Index), s.Const((long)RewardCompute.NoPoolIndex)),
            s.SetLocal(l.Reason, s.Const(ColorlessBucketReason))));

        EmitUpgradeAndPrice(s, code, l, l.Ordinal, colorless: true, onSaleSlot: -1, slot: slot);
        code.Add(s.SetLocal(l.Available, s.And(
            s.NotEqual(s.LocalRef(l.Index), s.Const((long)RewardCompute.NoPoolIndex)),
            s.And(
                s.Equal(s.LocalRef(l.Reason), s.Const(string.Empty)),
                s.NotEqual(s.LocalRef(l.IdText), s.Const(string.Empty))))));
        code.Add(CreateOffer(
            s,
            l,
            slot,
            CardOfferKind,
            s.LocalRef(l.IdText),
            s.LocalRef(l.SourceName),
            s.Const(RarityText(targetRarity)),
            s.Const((long)ColorlessPoolKind),
            s.LocalRef(l.Index),
            s.Const(false),
            s.LocalRef(l.Price),
            s.Not(s.LocalRef(l.Available))));
        RecordExclusion(s, code, l.Exclusions, l.Index, ref cursor);
    }

    /// <summary>
    /// 无色商品的不可买原因：成员在源清单里但还没进内容时写明源身份（可买时返回空串）。
    /// 稀有度不再需要在这里判断——抽取上界已经按源稀有度过滤过，抽到的成员必然同稀有度。
    /// </summary>
    private static string ColorlessReason(PoolCard member) => member.Card is null
        ? ColorlessMissingContentReasonPrefix + member.SourceName + ColorlessMissingContentReasonSuffix
        : string.Empty;

    /// <summary>
    /// 池成员定位：源 `items = options.Where(rarity).ToList(); Shops.NextItem(items)`。
    /// 有成员才抽一笔 `Shops`（源只在非空列表上调 `NextItem`），命中后写回该池自己的 member 下标。
    /// </summary>
    private Inst EmitCardPick(IrScope s, List<Inst> code, Locals l, Expr spec, int ordinal, int[] exclusions)
    {
        code.Add(s.Compute(
            cardCount.Asset,
            [(cardCount.Spec, spec), (cardCount.Ordinal, s.LocalRef(ordinal)), .. ExclusionInputs(cardCount.Exclusions, s, exclusions)],
            [(cardCount.Result, l.Count)]));
        code.Add(s.SetLocal(l.Index, s.Const((long)RewardCompute.NoPoolIndex)));
        return s.If(
            s.Greater(s.LocalRef(l.Count), s.Const(0L)),
            [
                s.SampleInt(shopsStream, s.Const(0L), s.LocalRef(l.Count), l.Pick),
                s.Compute(
                    cardPick.Asset,
                    [
                        (cardPick.Spec, spec),
                        (cardPick.Ordinal, s.LocalRef(ordinal)),
                        (cardPick.Asset.Inputs[2], s.LocalRef(l.Pick)),
                        .. ExclusionInputs(cardPick.Exclusions, s, exclusions),
                    ],
                    [(cardPick.Result, l.Index)]),
            ]);
    }

    /// <summary>升级 roll（源恒不升级，但**无条件**消费一笔 Rewards）+ 价格（在售格再抽一次并整除 2）。</summary>
    private void EmitUpgradeAndPrice(IrScope s, List<Inst> code, Locals l, int ordinal, bool colorless, int onSaleSlot, int slot)
    {
        code.Add(s.SampleInt(rewardsStream, s.Const(0L), s.Const(RewardCompute.Span), l.Bits));
        code.Add(s.Compute(
            cardUpgrade.Asset,
            [(cardUpgrade.Bits, s.LocalRef(l.Bits))],
            [(cardUpgrade.Upgraded, l.Upgraded)]));
        code.Add(s.SampleInt(shopsStream, s.Const(0L), s.Const(RewardCompute.Span), l.Bits));
        // 在售格**只抽一笔价格**：源 `MerchantCardEntry` 构造时 `CalcCost()` 抽一次，`SetOnSale()` 只把
        // `_cost /= 2`（不再抽样）。这里按"这一格是否在售"算同一个价格，但**不额外消费随机数**。
        if (onSaleSlot < 0)
        {
            code.Add(PriceCompute(s, l, ordinal, colorless, onSale: false));
            return;
        }

        code.Add(s.IfElse(
            s.Equal(s.LocalRef(onSaleSlot), s.Const((long)slot)),
            [PriceCompute(s, l, ordinal, colorless, onSale: true)],
            [PriceCompute(s, l, ordinal, colorless, onSale: false)]));
    }

    private Inst PriceCompute(IrScope s, Locals l, int ordinal, bool colorless, bool onSale) =>
        s.Compute(
            cardPrice.Asset,
            [
                (cardPrice.Ordinal, s.LocalRef(ordinal)),
                (cardPrice.Colorless, s.Const(colorless)),
                (cardPrice.Bits, s.LocalRef(l.Bits)),
                (cardPrice.OnSale, s.Const(onSale)),
            ],
            [(cardPrice.Price, l.Price)]);

    /// <summary>
    /// 遗物取货：源 `RelicFactory.PullNextRelicFromBack` + `RelicGrabBag.GetAvailableDeque` 的
    /// `Shop→Common→Uncommon→Rare→None` 回退链（袋尾取货、空袋往下卷、全空回退 Circlet）。
    /// </summary>
    private void EmitRelic(IrScope s, List<Inst> code, Locals l, int slot, Expr rolledRarity)
    {
        var resolvedRarity = s.Local($"shopRelicResolved{slot}", FieldTypeSchema.String(16));
        var startRank = s.Local($"shopRelicStartRank{slot}", FieldTypeSchema.Int(0, 3));
        var found = s.Local($"shopRelicFound{slot}", FieldTypeSchema.Bool());
        var buyable = s.Local($"shopRelicBuyable{slot}", FieldTypeSchema.Bool());

        code.Add(s.SetLocal(startRank, s.Const(0L)));
        code.Add(s.If(s.Equal(rolledRarity, s.Const("Common")), s.SetLocal(startRank, s.Const(1L))));
        code.Add(s.If(s.Equal(rolledRarity, s.Const("Uncommon")), s.SetLocal(startRank, s.Const(2L))));
        code.Add(s.If(s.Equal(rolledRarity, s.Const("Rare")), s.SetLocal(startRank, s.Const(3L))));
        code.Add(s.SetLocal(resolvedRarity, s.Const("None")));
        code.Add(s.SetLocal(l.IdText, s.Const(CircletId)));
        code.Add(s.SetLocal(found, s.Const(false)));

        var rank = 0;
        foreach (var rarity in new[] { "Shop", "Common", "Uncommon", "Rare" })
        {
            if (PlayerBags.TryGetValue(rarity, out var bag))
            {
                var head = s.First(s.Collection(bag));
                code.Add(s.If(
                    s.And(
                        s.Not(s.LocalRef(found)),
                        s.And(s.LessOrEqual(s.LocalRef(startRank), s.Const((long)rank)), s.Greater(s.Count(s.Collection(bag)), s.Const(0L)))),
                    [
                        s.SetLocal(resolvedRarity, s.Const(rarity)),
                        s.SetLocal(l.IdText, s.Field(head, playerBagEntryId)),
                        s.SetLocal(found, s.Const(true)),
                        s.Destroy(head),
                    ]));
            }

            rank++;
        }

        // 镜像 `RunState.SharedRelicGrabBag.Remove(relic)`：共享袋里同 id 的成员一并移除。
        foreach (var bag in SharedBags.Values)
        {
            code.Add(s.ForEach(
                s.Where(s.Collection(bag), entry => s.Equal(s.Field(entry, sharedBagEntryId), s.LocalRef(l.IdText))),
                entry => new Inst[] { s.Destroy(entry) }));
        }

        code.Add(s.SampleInt(shopsStream, s.Const(0L), s.Const(RewardCompute.Span), l.Bits));
        code.Add(s.Compute(
            relicPrice.Asset,
            [(relicPrice.Rarity, s.LocalRef(resolvedRarity)), (relicPrice.Bits, s.LocalRef(l.Bits))],
            [(relicPrice.Price, l.Price)]));

        // 可买判据：正式登记表里、效果已实现的那几件（外加源回退遗物 Circlet；源里它也没有获得效果）。
        code.Add(s.SetLocal(buyable, s.Equal(s.LocalRef(l.IdText), s.Const(CircletId))));
        foreach (var relic in buyableRelics)
        {
            code.Add(s.If(s.Equal(s.LocalRef(l.IdText), s.Const(relic.ContentId)), s.SetLocal(buyable, s.Const(true))));
        }

        code.Add(s.SetLocal(l.Reason, s.Const(string.Empty)));
        code.Add(s.If(
            s.Not(s.LocalRef(buyable)),
            s.SetLocal(
                l.Reason,
                s.Concat(s.Const(UnregisteredRelicReasonPrefix), s.LocalRef(l.IdText), s.Const(UnregisteredRelicReasonSuffix)))));
        code.Add(CreateOffer(
            s,
            l,
            slot,
            RelicOfferKind,
            s.LocalRef(l.IdText),
            s.Const(string.Empty),
            s.LocalRef(resolvedRarity),
            s.Const(-1L),
            s.Const(-1L),
            s.Const(false),
            s.LocalRef(l.Price),
            s.Not(s.LocalRef(buyable))));
    }

    private void EmitPotionPick(IrScope s, List<Inst> code, Locals l, int index)
    {
        var spec = s.Const(potionSpec);
        code.Add(s.SetLocal(l.PotionIndices[index], s.Const((long)RewardCompute.NoPoolIndex)));
        code.Add(s.Compute(
            cardCount.Asset,
            [
                (cardCount.Spec, spec),
                (cardCount.Ordinal, s.LocalRef(l.PotionOrdinals[index])),
                .. ExclusionInputs(cardCount.Exclusions, s, l.PotionExclusions),
            ],
            [(cardCount.Result, l.Count)]));
        code.Add(s.If(
            s.Greater(s.LocalRef(l.Count), s.Const(0L)),
            [
                s.SampleInt(shopsStream, s.Const(0L), s.LocalRef(l.Count), l.Pick),
                s.Compute(
                    cardPick.Asset,
                    [
                        (cardPick.Spec, spec),
                        (cardPick.Ordinal, s.LocalRef(l.PotionOrdinals[index])),
                        (cardPick.Asset.Inputs[2], s.LocalRef(l.Pick)),
                        .. ExclusionInputs(cardPick.Exclusions, s, l.PotionExclusions),
                    ],
                    [(cardPick.Result, l.PotionIndices[index])]),
            ]));
        if (index < PotionEntryCount - 1)
        {
            // 源在同一个 `list` 上 `Remove(item)`，因此同一间商店的药水互不重复。
            code.Add(s.SetLocal(l.PotionExclusions[index], s.LocalRef(l.PotionIndices[index])));
        }
    }

    /// <summary>建货架条目（`Reason` 局部里已经放好不可买原因）。</summary>
    private Inst CreateOffer(
        IrScope s,
        Locals l,
        int slot,
        string kind,
        Expr definitionId,
        Expr sourceName,
        Expr rarity,
        Expr poolKind,
        Expr poolIndex,
        Expr onSale,
        Expr price,
        Expr unsupported)
    {
        return s.CreateEntity(
            Offer.Type,
            s.EntityScope(room.CurrentRun(s)),
            l.Created,
            [
                (Offer.Slot, s.Const((long)slot)),
                (Offer.Kind, s.Const(kind)),
                (Offer.DefinitionId, definitionId),
                (Offer.SourceName, sourceName),
                (Offer.Rarity, rarity),
                (Offer.PoolKind, poolKind),
                (Offer.PoolIndex, poolIndex),
                (Offer.OnSale, onSale),
                (Offer.Price, price),
                (Offer.Sold, s.Const(false)),
                (Offer.Unsupported, unsupported),
                (Offer.UnsupportedReason, s.LocalRef(l.Reason)),
            ],
            Offers);
    }

    private static int[] Allocate(IrScope s, List<Inst> code, string prefix, int count, FieldTypeSchema? schema = null)
    {
        var slots = new int[count];
        for (var i = 0; i < count; i++)
        {
            slots[i] = s.Local($"{prefix}{i}", schema ?? FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex));
            if (schema is null)
            {
                code.Add(s.SetLocal(slots[i], s.Const(-1L)));
            }
        }

        return slots;
    }

    private static IEnumerable<(FieldSymbol Field, Expr Value)> ExclusionInputs(IReadOnlyList<FieldSymbol> fields, IrScope s, int[] exclusions)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            yield return (fields[i], s.LocalRef(exclusions[i]));
        }
    }

    private static void RecordExclusion(IrScope s, List<Inst> code, int[] exclusions, int index, ref int cursor)
    {
        if (cursor >= exclusions.Length)
        {
            return;
        }

        code.Add(s.SetLocal(exclusions[cursor], s.LocalRef(index)));
        cursor++;
    }

    private Expr OffsetExpr(IrScope s) => s.Field(room.CurrentRun(s),
        // The reward subpackage declares its persistent odds fields in `RewardCompilation.Rewards`, which runs
        // after this module is constructed: resolve the symbol by name on first use (the Run type symbol is shared,
        // so this is the same field the reward path reads and writes).
        cardRarityOffset ??= room.RunType.Field(RewardWiring.CardOddsFieldName));

    /// <summary>
    /// 普通拒绝的守卫：相位 + 节点 + 商品种类 + 未售出 + 可买（非 Unsupported）+ 金币足够（药水还要有空槽）。
    /// 任一不成立 ⇒ 动作不合法（引擎抛 `Illegal`，状态不变、会话保持 Running），不 Fault 整局。
    /// </summary>
    private Expr BuyGuard(IrScope s, Expr offer, Expr runExpr, string kind, bool requiresBeltSpace) =>
        s.And(
            s.And(
                room.At(s, runExpr, (int)MapRoomWiring.ShopPhase, ShopPointTypeMember),
                s.Not(s.Field(offer, Offer.Sold))),
            s.And(
                s.Equal(s.Field(offer, Offer.Kind), s.Const(kind)),
                s.And(
                    s.Not(s.Field(offer, Offer.Unsupported)),
                    s.And(
                        s.GreaterOrEqual(s.Field(runExpr, room.Gold), s.Field(offer, Offer.Price)),
                        requiresBeltSpace ? potions.HasSpace(s) : s.Const(true)))));

    /// <summary>买得到的商品才是候选（已售 / 不可买 / 买不起 / 满栏都不在候选域里）。</summary>
    private Query BuyableOffers(IrScope s, string kind, Expr runExpr, bool requiresBeltSpace) =>
        s.Where(
            s.Collection(Offers),
            offer => s.And(
                s.And(
                    s.Equal(s.Field(offer, Offer.Kind), s.Const(kind)),
                    s.Not(s.Field(offer, Offer.Sold))),
                s.And(
                    s.Not(s.Field(offer, Offer.Unsupported)),
                    s.And(
                        s.GreaterOrEqual(s.Field(runExpr, room.Gold), s.Field(offer, Offer.Price)),
                        requiresBeltSpace ? potions.HasSpace(s) : s.Const(true)))));

    /// <summary>删牌候选：牌必须在**长期牌组**里，且不是 `Eternal`（源 `CardModel.IsRemovable`）。
    /// 本 Slice 的 `eternal` 尚未作为卡字段存在，因此可删性判据 = "是长期牌组的真实实例"（由 decision 候选域保证）。</summary>
    private Expr Removable(IrScope s, Expr card) => s.Exists(s.Where(s.Collection(room.LongTermDeck), held => s.Equal(held, card)));
    // -- 过程声明 ---------------------------------------------------------------------------------------------

    private ProcedureSymbol DeclareResolveCard(EnginePackageBuilder package)
    {
        var procedure = package.Procedure(ResolveCardProcedureName);
        var poolKind = procedure.Param("PoolKind", FieldTypeSchema.Int(0, ColorlessPoolKind));
        var poolIndex = procedure.Param("PoolIndex", FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex));
        var resolved = procedure.Local("resolvedShopCardId", FieldTypeSchema.String(64));
        procedure.Result(FieldTypeSchema.String(64));
        var code = new List<Inst> { procedure.SetLocal(resolved, procedure.Const(string.Empty)) };
        foreach (var member in createTable.Where(member => member.Card is not null))
        {
            code.Add(procedure.If(
                procedure.And(
                    procedure.Equal(procedure.Param(poolKind), procedure.Const((long)member.PoolKind)),
                    procedure.Equal(procedure.Param(poolIndex), procedure.Const((long)member.Index))),
                procedure.SetLocal(resolved, procedure.Const(member.Card!.Id))));
        }

        code.Add(procedure.Return(procedure.LocalRef(resolved)));
        return procedure.Body(code.ToArray());
    }

    private ProcedureSymbol DeclareCreateCard(EnginePackageBuilder package)
    {
        var procedure = package.Procedure(CreateCardProcedureName);
        var poolKind = procedure.Param("PoolKind", FieldTypeSchema.Int(0, ColorlessPoolKind));
        var poolIndex = procedure.Param("PoolIndex", FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex));
        var runExpr = room.CurrentRun(procedure);
        var code = new List<Inst>();
        foreach (var member in createTable.Where(member => member.Card is not null))
        {
            code.Add(procedure.If(
                procedure.And(
                    procedure.Equal(procedure.Param(poolKind), procedure.Const((long)member.PoolKind)),
                    procedure.Equal(procedure.Param(poolIndex), procedure.Const((long)member.Index))),
                deck.Create(procedure, runExpr, member.Card!)));
        }

        return procedure.Body(code.ToArray());
    }

    private ProcedureSymbol DeclareResolvePotion(EnginePackageBuilder package)
    {
        var procedure = package.Procedure(ResolvePotionProcedureName);
        var poolIndex = procedure.Param("PoolIndex", FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex));
        var resolved = procedure.Local("resolvedShopPotionId", FieldTypeSchema.String(64));
        procedure.Result(FieldTypeSchema.String(64));
        var code = new List<Inst> { procedure.SetLocal(resolved, procedure.Const(string.Empty)) };
        for (var index = 0; index < potions.Pool.Count; index++)
        {
            code.Add(procedure.If(
                procedure.Equal(procedure.Param(poolIndex), procedure.Const((long)index)),
                procedure.SetLocal(resolved, procedure.Const(potions.Pool[index].Id))));
        }

        code.Add(procedure.Return(procedure.LocalRef(resolved)));
        return procedure.Body(code.ToArray());
    }

    private ProcedureSymbol DeclareCreatePotion(EnginePackageBuilder package)
    {
        var procedure = package.Procedure(CreatePotionProcedureName);
        procedure.Result(FieldTypeSchema.RefTo(potions.Entity));
        var poolIndex = procedure.Param("PoolIndex", FieldTypeSchema.Int(-1, RewardCompute.NoPoolIndex));
        var runExpr = room.CurrentRun(procedure);
        var created = procedure.Local("createdShopPotion", FieldTypeSchema.Binding());
        var code = new List<Inst>();
        for (var index = 0; index < potions.Pool.Count; index++)
        {
            code.Add(procedure.If(
                procedure.Equal(procedure.Param(poolIndex), procedure.Const((long)index)),
                procedure.CreateEntity(
                    potions.Entity,
                    procedure.EntityScope(runExpr),
                    created,
                    potions.Fields(procedure, potions.Pool[index].Id, procedure.Const(-1L)))));
        }
        code.Add(procedure.Return(procedure.LocalRef(created)));
        return procedure.Body(code.ToArray());
    }

    // -- 池构建 -----------------------------------------------------------------------------------------------

    /// <summary>
    /// 彩卡池：与 `RewardWiring.BuildPool` 同一口径（`IroncladRewardPool.SourceOrder` 去掉 multiplayerOnly，
    /// 并用内容里 `pools = { IroncladCardPool }` 的声明做双向对账）；下标 = 源单机清单里的位置。
    /// </summary>
    private static IReadOnlyList<PoolCard> BuildColoredPool(IReadOnlyList<CardDefinition> cards)
    {
        var byId = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            var shortId = ShortId(card.Id);
            if (!byId.TryAdd(shortId, card))
            {
                throw new InvalidOperationException($"base-spire: 商店牌池对账失败：内容里有重复的卡 id '{shortId}'");
            }
        }

        var effective = new List<CardDefinition>();
        foreach (var id in IroncladRewardPool.SourceOrder)
        {
            if (IroncladRewardPool.MultiplayerOnly.Contains(id, StringComparer.Ordinal))
            {
                continue;
            }

            if (!byId.TryGetValue(id, out var card))
            {
                throw new InvalidOperationException($"base-spire: 商店牌池对账失败：源单机清单有 {id} 没有进内容");
            }

            effective.Add(card);
        }

        foreach (var id in IroncladRewardPool.MultiplayerOnly)
        {
            if (byId.TryGetValue(id, out var card) && !card.MultiplayerOnly)
            {
                throw new InvalidOperationException($"base-spire: 商店牌池对账失败：源侧 multiplayerOnly 的 '{id}' 在内容里不是 multiplayerOnly");
            }
        }

        var declared = cards
            .Where(card => card.Pools.Contains(IroncladCardPoolName, StringComparer.Ordinal))
            .Select(card => ShortId(card.Id))
            .ToHashSet(StringComparer.Ordinal);
        var effectiveIds = effective.Select(card => ShortId(card.Id)).ToHashSet(StringComparer.Ordinal);
        var missing = effectiveIds.Where(id => !declared.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"base-spire: 商店牌池对账失败：源单机清单有 {missing.Count} 项没有进内容池：{string.Join(", ", missing)}");
        }

        var extra = declared.Where(id => !effectiveIds.Contains(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (extra.Count > 0)
        {
            throw new InvalidOperationException($"base-spire: 商店牌池对账失败：内容声明进 IroncladCardPool 但有 {extra.Count} 项不在源单机清单里：{string.Join(", ", extra)}");
        }

        return effective
            .Select((card, index) => new PoolCard(index, ColoredPoolKindBase + IndexOfColoredKind(card.Kind), card,
                ShortId(card.Id), RarityOrdinal(CardRarityText(card)), card.Kind))
            .ToList();
    }

    /// <summary>
    /// 无色池：按 `ColorlessShopPool.Entries`（源 `ColorlessCardPool.GenerateAllCards()` 的 64 项 + 各自源稀有度）
    /// 建**整条**清单。尚无内容定义的成员保留条目（`Card = null`）——抽取范围与顺序仍与源一致
    /// （上界按源稀有度过滤，见 `EmitColorlessCard`），商品位保留完整身份并标不可买，不从清单里过滤、
    /// 也不用别的卡顶替。内容声明进 `ColorlessCardPool` 但不在源清单里的项 => 具名失败（对账方向与彩卡一致）。
    /// </summary>
    private static IReadOnlyList<PoolCard> BuildColorlessPool(IReadOnlyList<CardDefinition> cards)
    {
        var byId = new Dictionary<string, CardDefinition>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            byId[ShortId(card.Id)] = card;
        }

        var declared = cards
            .Where(card => !card.MultiplayerOnly && card.Pools.Contains(ColorlessCardPoolName, StringComparer.Ordinal))
            .Select(card => ShortId(card.Id))
            .ToList();
        var extra = declared.Where(id => !ColorlessShopPool.Entries.Any(entry =>
                string.Equals(entry.SourceId, id, StringComparison.Ordinal)))
            .OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (extra.Count > 0)
        {
            throw new InvalidOperationException(
                $"base-spire: 商店无色池对账失败：内容声明进 {ColorlessCardPoolName} 但有 {extra.Count} 项不在源清单里：{string.Join(", ", extra)}");
        }

        var pool = new List<PoolCard>(ColorlessShopPool.Count);
        for (var index = 0; index < ColorlessShopPool.Count; index++)
        {
            var source = ColorlessShopPool.Entries[index];
            var rarity = RarityOrdinal(source.Rarity);
            if (byId.TryGetValue(source.SourceId, out var card))
            {
                var contentRarity = RarityOrdinal(CardRarityText(card));
                if (contentRarity != rarity)
                {
                    throw new InvalidOperationException(
                        $"base-spire: 商店无色池对账失败：卡 '{card.Id}' 的内容稀有度（{card.Normal.Rarity}）"
                        + $"与源稀有度（{source.Rarity}）不一致");
                }

                pool.Add(new PoolCard(index, ColorlessPoolKind, card, source.SourceId, rarity, card.Kind));
            }
            else
            {
                pool.Add(new PoolCard(index, ColorlessPoolKind, null, source.SourceId, rarity, string.Empty));
            }
        }

        return pool;
    }

    private static string Encode(IReadOnlyList<PoolCard> members) =>
        RewardPoolSpec.Encode(members
            .Where(member => member.Card is not null)
            .Select(member => new RewardPoolEntry(member.Index, member.Rarity, member.Card!.Upgradeable)));

    private static int IndexOfColoredKind(string kind) => kind switch
    {
        "Attack" => 0,
        "Skill" => 1,
        _ => 2,
    };

    private static string ShortId(string id)
    {
        var separator = id.LastIndexOf(':');
        return separator < 0 ? id : id[(separator + 1)..];
    }

    private static string CardRarityText(CardDefinition card)
    {
        var normal = card.Normal.Rarity;
        var upgraded = card.Upgraded.Rarity;
        if (!string.Equals(normal, upgraded, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"base-spire: 商店牌池对账失败：卡 '{card.Id}' 的 normal/upgraded 稀有度不一致（'{normal}' vs '{upgraded}'）");
        }

        return normal;
    }

    private static int RarityOrdinal(string rarity) => rarity switch
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
        _ => throw Faults.Schema($"base-spire: 商店池里出现了未登记的稀有度 '{rarity}'"),
    };

    private static int PotionRarityOrdinal(string rarity) => rarity switch
    {
        "rare" => RewardRarity.Rare,
        "uncommon" => RewardRarity.Uncommon,
        _ => RewardRarity.Common,
    };

    private static string RarityText(int rarity)
    {
        // 不用 switch 常量标签：RewardRarity 的成员不保证是 const。
        if (rarity == RewardRarity.Rare)
        {
            return "rare";
        }

        if (rarity == RewardRarity.Uncommon)
        {
            return "uncommon";
        }

        return rarity == RewardRarity.Basic ? "basic" : "common";
    }
}
