using System.Collections.Generic;
using System.Linq;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.ShopFlowChecks;

/// <summary>
/// 组合计算后端：本包的商店条目优先，其余逐字转发冻结的 `BaseSpireComputeBackend`
/// （药水/宝箱/地图资产都在它那里）。正式集成时这一行委派会落到 `BaseSpireComputeBackend.Execute` 里。
/// </summary>
internal sealed class ShopFlowComputeBackend : IComputeBackend
{
    public string Id => BaseSpireComputeBackend.BackendId;

    public void Execute(ComputeAssetDefinition asset, IReadOnlyList<Value> inputs, Value[] outputs)
    {
        if (ShopCompute.TryExecute(asset.Entry, inputs, outputs))
        {
            return;
        }

        BaseSpireComputeBackend.Instance.Execute(asset, inputs, outputs);
    }
}

/// <summary>
/// 商店模块的**最小宿主**：只声明模块真正消费的接缝（Run/RunStates/金币/血量/长期牌组/遗物袋/药水栏/地图相位与节点）。
///
/// 这是**验证脚手架**，不是生产入口：正式组包要把同样的接缝接在 `CombatSlicePackage.Build` 里（见交接文档）。
/// 相位用 <see cref="ShopPhase"/> = 7（`MapRoomWiring` 目前把 `MapPhase` 限在 0..6，接缝要求抬到 7）。
/// </summary>
internal sealed class ShopHarness
{
    /// <summary>商店相位（提案值；等于 `MapRoomWiring.ActCompletedPhase` 会与"本幕结束"冲突，因此取 7）。</summary>
    public const int ShopPhase = 7;

    /// <summary>`MapRoomWiring.TravelReadyPhase`：离店回到地图。</summary>
    public const int TravelPhase = 2;

    private static SliceAuthorModel? cachedModel;

    private ShopHarness(Session session, ShopRoomWiring shop, CardDefinitions cards, PotionInventoryRules potions,
        EnumTypeSymbol pointType, string seed)
    {
        Session = session;
        Shop = shop;
        Cards = cards;
        Potions = potions;
        PointType = pointType;
        Seed = seed;
    }

    public Session Session { get; }

    public ShopRoomWiring Shop { get; }

    public CardDefinitions Cards { get; }

    public PotionInventoryRules Potions { get; }

    public EnumTypeSymbol PointType { get; }

    public string Seed { get; }

    /// <summary>内容模型只加载一次（`FormalSpirePackage` 走冻结 DLL 里内嵌的正式内容）。</summary>
    public static SliceAuthorModel Model => cachedModel ??= FormalSpirePackage.Load().Model;

    public static ShopHarness Create(string seed, long startGold = 500, IReadOnlyList<string>? deck = null,
        bool sourceRelicBags = true)
    {
        var model = Model;
        var package = new EnginePackageBuilder(SliceManifest.PackageId, "shop-flow-checks");
        var projection = package.Projection("HarnessView");
        projection.ParticipantRole = SliceManifest.PlayerRole;
        var rootScope = package.ScopeType("HarnessRoot");

        // 卡牌实体：与生产同一套 `CardSchema` / `CardDefinitions`（只留空 variables —— 本工程不跑战斗）。
        var cardKind = package.Enum("CardKind");
        cardKind.Member("Attack").Member("Skill").Member("Power").Member("Status");
        var cardSchema = CardSchema.Declare(package, cardKind);
        var variables = new Dictionary<string, FieldSymbol>(StringComparer.Ordinal);
        var cardDefinitions = new CardDefinitions(cardSchema, cardKind, model, variables);
        var longTermDeck = package.Collection("LongTermDeck", cardSchema.Type);
        longTermDeck.Capacity = 64;
        projection.Type(cardSchema.Type);

        // 地图最小件：节点类型枚举与 `MapRoomWiring` 同成员顺序（Shop = 2），相位字段上界取 7。
        var pointType = package.Enum("MapPointType");
        pointType.Member("Unassigned").Member("Unknown").Member("Shop").Member("Treasure").Member("RestSite")
            .Member("Monster").Member("Elite").Member("Boss").Member("Ancient");
        var nodeEntity = package.EntityType("MapNode");
        var nodeTypeField = nodeEntity.AddField("Type", FieldTypeSchema.EnumOf(pointType));
        var nodeIndexField = nodeEntity.AddField("Index", FieldTypeSchema.Int(0, 512));
        var nodes = package.Collection("MapNodes", nodeEntity);
        projection.Type(nodeEntity);

        var runType = package.EntityType("Run");
        var runs = package.Collection("RunStates", runType);
        var hp = runType.AddField("Hp", FieldTypeSchema.Int(0, 999));
        var maxHp = runType.AddField("MaxHp", FieldTypeSchema.Int(0, 999));
        var gold = runType.AddField("Gold", FieldTypeSchema.Int(0, 99999));
        var cardOdds = runType.AddField("CardRarityOdds", FieldTypeSchema.Int(0, 4294967295L));
        var mapPhase = runType.AddField("MapPhase", FieldTypeSchema.Int(0, ShopPhase));
        var currentNode = runType.AddField("CurrentNode", FieldTypeSchema.RefTo(nodeEntity), optional: true);
        projection.Type(runType);

        // 遗物实体与四袋（稀有度键与 `RunInitializationWiring.PlayerBags` 同口径）。
        var relicEntity = package.EntityType("Relic");
        var relicDefinition = relicEntity.AddField("DefinitionId", FieldTypeSchema.String(64));
        var relicRarity = relicEntity.AddField("Rarity", FieldTypeSchema.String(16));
        var relics = package.Collection("Relics", relicEntity);
        projection.Type(relicEntity);
        var bagEntity = package.EntityType("RelicBagEntry");
        var bagEntryId = bagEntity.AddField("RelicId", FieldTypeSchema.String(64), visibility: FieldVisibility.Hidden);
        var bags = new Dictionary<string, CollectionSymbol>(StringComparer.Ordinal);
        foreach (var rarity in BagRarities)
        {
            bags[rarity] = package.Collection($"PlayerRelicBag{rarity}", bagEntity);
        }

        var potions = new PotionInventoryRules(package, model);

        var shop = new ShopRoomWiring(
            package,
            new ShopRoomWiring.ShopRunSeam(runType, runs, gold, hp, maxHp, cardOdds, projection),
            new ShopRoomWiring.ShopMapSeam(mapPhase, currentNode, nodeTypeField, pointType, ShopPhase, TravelPhase),
            new ShopRoomWiring.ShopCardSeam(cardSchema.Type, longTermDeck, cardDefinitions.Compiled, cardDefinitions.New),
            new ShopRoomWiring.ShopRelicSeam(relicEntity, relicDefinition, relicRarity, relics, bagEntryId, bags, null, SpireRelics.All),
            potions);

        // -- 会话开始：根 scope → 商店节点 → Run → 长期牌组 → 遗物袋 → 进店 ---------------------------------
        var setup = package.Procedure("HarnessSetup");
        var rootLocal = setup.Local("harnessRoot", FieldTypeSchema.Binding());
        var nodeLocal = setup.Local("harnessNode", FieldTypeSchema.Binding());
        var runLocal = setup.Local("harnessRun", FieldTypeSchema.Binding());
        var cardLocal = setup.Local("harnessCard", FieldTypeSchema.Binding());
        var bagLocal = setup.Local("harnessBagEntry", FieldTypeSchema.Binding());
        var steps = new List<Inst> { setup.CreateScope(rootScope, rootLocal) };
        steps.Add(setup.CreateEntity(nodeEntity, setup.LocalRef(rootLocal), nodeLocal,
            [(nodeTypeField, setup.EnumConst(pointType, "Shop")), (nodeIndexField, setup.Const(0L))], nodes));
        steps.Add(setup.CreateEntity(runType, setup.LocalRef(rootLocal), runLocal,
            [
                (hp, setup.Const(80L)),
                (maxHp, setup.Const(80L)),
                (gold, setup.Const(startGold)),
                (cardOdds, setup.Const((long)RewardCompute.BitsOf(RewardCompute.CardRarityBaseOffset))),
                (mapPhase, setup.Const(0L)),
            ], runs));
        var runRef = setup.First(setup.Collection(runs));
        steps.Add(setup.SetField(runRef, currentNode, setup.First(setup.Collection(nodes))));

        foreach (var definitionId in deck ?? DefaultDeck)
        {
            steps.Add(setup.CreateEntity(cardSchema.Type, setup.LocalRef(rootLocal), cardLocal,
                cardDefinitions.New(setup, cardDefinitions.Definition(definitionId), false), longTermDeck));
        }

        foreach (var (rarity, ids) in BagContents(sourceRelicBags))
        {
            var bag = bags[rarity];
            foreach (var id in ids)
            {
                steps.Add(setup.CreateEntity(bagEntity, setup.LocalRef(rootLocal), bagLocal, [(bagEntryId, setup.Const(id))], bag));
            }

            steps.Add(setup.ShuffleCollection(bag, VanillaRandomLayout.EngineStreamName("UpFront")));
        }

        steps.AddRange(shop.Enter(setup, runRef));
        setup.Body(steps.ToArray());
        package.OnSessionStart(setup.Symbol);

        // 验证用入口：重新进店（模块的公开入口就是它；正式整合由地图的 Shop 分支调用）。
        var enter = package.Action("HarnessEnterShop");
        var enterBody = package.ActionBody(enter);
        var enterRun = enterBody.First(enterBody.Collection(runs));
        enterBody.Role(SliceManifest.PlayerRole);
        enterBody.Body(shop.Enter(enterBody, enterRun));
        shop.RegisterActions(package);

        var builder = new RuleSetBuilder();
        builder.Add(package.Build());
        var session = new Session(
            builder.Build(),
            VanillaRandomLayout.RunSeed(seed),
            [SliceManifest.PlayerRole],
            computeBackend: new ShopFlowComputeBackend(),
            randomAlgorithmId: VanillaRandomLayout.AlgorithmId,
            randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
            randomProfile: VanillaRandomLayout.Profile);
        VanillaRandomLayout.SeedAllStreams(session.Random, seed);
        session.Start();
        return new ShopHarness(session, shop, cardDefinitions, potions, pointType, seed);
    }

    public static IReadOnlyList<string> DefaultDeck =>
        [SliceManifest.StrikeId, SliceManifest.StrikeId, SliceManifest.DefendId];

    public static readonly IReadOnlyList<string> BagRarities = ["Shop", "Common", "Uncommon", "Rare"];

    /// <summary>
    /// 袋内容：默认用源共享池清单（与 `RunInitializationWiring` 同口径，含大量"未登记效果"的遗物，
    /// 用来验证"不可买商品被明确标记"）；`sourceRelicBags=false` 时每袋只放能实现的遗物。
    /// </summary>
    public static IReadOnlyList<(string Rarity, IReadOnlyList<string> Ids)> BagContents(bool sourceRelicBags)
    {
        if (!sourceRelicBags)
        {
            return
            [
                ("Uncommon", ["base-spire:pear"]),
                ("Rare", ["base-spire:mango", "base-spire:old-coin"]),
                ("Shop", ["base-spire:pear"]),
                ("Common", ["base-spire:pear"]),
            ];
        }

        var contents = new List<(string, IReadOnlyList<string>)>();
        foreach (var rarity in BagRarities)
        {
            contents.Add((rarity, [.. SharedRelicPoolManifest.Entries
                .Where(entry => entry.Rarity == rarity)
                .Select(TreasureRoomWiring.ContentId)]));
        }

        return contents;
    }

    // -- 读状态 ----------------------------------------------------------------------------------------------

    public InstanceId Run => Members("RunStates").Single();

    public InstanceId ShopNode => Members("MapNodes").Single();

    public IReadOnlyList<InstanceId> Members(string collectionName) => Session.State.CollectionMembers(
        Session.RuleSet.Collections.Single(collection => collection.QualifiedId == "base-spire:" + collectionName).Id);

    public IReadOnlyList<InstanceId> Offers() => [.. Members(ShopRoomWiring.OffersCollectionName).OrderBy(offer => OfferNumber(offer, "Slot"))];

    public long Number(InstanceId instance, string typeName, string field) =>
        Session.State.GetField(instance, FieldIndex(typeName, field)).AsInt();

    public string Text(InstanceId instance, string typeName, string field) =>
        Session.State.GetField(instance, FieldIndex(typeName, field)).AsString();

    public bool Flag(InstanceId instance, string typeName, string field) =>
        Session.State.GetField(instance, FieldIndex(typeName, field)).AsBool();

    public long OfferNumber(InstanceId offer, string field) => Number(offer, ShopRoomWiring.OfferEntityName, field);

    public string OfferText(InstanceId offer, string field) => Text(offer, ShopRoomWiring.OfferEntityName, field);

    public bool OfferBool(InstanceId offer, string field) => Flag(offer, ShopRoomWiring.OfferEntityName, field);

    public long RunNumber(string field) => Number(Run, "Run", field);

    public string RunText(string field) => Text(Run, "Run", field);

    public long DrawCount(string playerStreamType) =>
        Session.Random.DrawCount(VanillaRandomLayout.EnginePlayerStreamName(playerStreamType));

    private int FieldIndex(string typeName, string field) =>
        Session.RuleSet.TypeByQualifiedId("base-spire:" + typeName).FieldIndex(field);

    // -- 受控注入（验证"货架内容随机"之外的分支；与生产测试同一手法） -----------------------------------------

    public void SetRun(string field, long value) => Inject(Run, "Run", field, Value.Of(value));

    public void SetOfferNumber(InstanceId offer, string field, long value) =>
        Inject(offer, ShopRoomWiring.OfferEntityName, field, Value.Of(value));

    public void SetOfferText(InstanceId offer, string field, string value) =>
        Inject(offer, ShopRoomWiring.OfferEntityName, field, Value.Of(value));

    public void SetOfferFlag(InstanceId offer, string field, bool value) =>
        Inject(offer, ShopRoomWiring.OfferEntityName, field, Value.Of(value));

    public void Inject(InstanceId instance, string typeName, string field, Value value) =>
        Session.State.RunAtomic(() => Session.State.SetField(instance, FieldIndex(typeName, field), value));

    /// <summary>把当前节点类型切成别的地图类型（`MapPointType` 的成员序与 `MapRoomWiring` 一致）。</summary>
    public void SetShopNodeType(int ordinal) => Inject(ShopNode, "MapNode", "Type", Value.OfEnum(ordinal));

    public const int ShopPointOrdinal = 2;
    public const int MonsterPointOrdinal = 5;

    // -- 动作 -------------------------------------------------------------------------------------------------

    public LegalAction? Legal(string actionName) => Session.LegalActions(SliceManifest.Player)
        .FirstOrDefault(action => action.Definition.QualifiedId == "base-spire:" + actionName);

    public void Submit(string actionName, params Value[] arguments)
    {
        var action = Legal(actionName) ?? throw new InvalidOperationException($"动作 '{actionName}' 当前不合法");
        Session.Submit(action.Definition, SliceManifest.Player, arguments);
    }

    public void EnterShop() => Submit("HarnessEnterShop");
}
