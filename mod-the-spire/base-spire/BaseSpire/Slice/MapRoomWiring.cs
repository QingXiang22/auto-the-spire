using CoreTheSpire.Engine;
using BaseSpire.Slice.Rooms;

namespace BaseSpire.Slice;

/// <summary>
/// T05：地图/房间子包的引擎接线。默认关闭（`CombatSlicePackage.Build(actMapEnabled: false)`），
/// 由 `BaseSpireScope.DevMap` 打开。
///
/// 设计边界（照简报）：
/// <list type="bullet">
/// <item>状态是**引擎实体/字段/关系/集合**：`MapNode` 实体 + `MapNodes` 集合 + `MapChildren` 关系，
///       地图游标是 `Run` 上的字段（`CurrentNode`/`MapPhase`/`MapFloor`/`MapNodeCount`/`MapDraws`）。</item>
/// <item>地图随机只走新流 `base-spire:run:act_1_map`（`SampleInt(stream, 0, 2^53)`，每笔恰一个 53 位 payload），
///       由本文件按源顺序逐笔抽、逐笔重放；不碰 `System.Random`、不复用别的流游标。</item>
/// <item>合法性只经正常 Action 的 `Candidates`/`Requires`；未实现房型显式具名 `Fail(...)`，节点保留、状态不变。</item>
/// </list>
/// </summary>
public sealed class MapRoomWiring
{
    /// <summary>源 `Overgrowth` 的地图流类型名（引擎名 `base-spire:run:act_1_map`）。</summary>
    public const string ActMapStreamType = "act_1_map";

    public const string PointTypeEnumName = "MapPointType";

    /// <summary>
    /// ENV-T09-unknown-room-modules-20261012：源 `MegaCrit.Sts2.Core.Rooms.RoomType`
    /// （`Rooms/RoomType.cs` 的成员顺序即 ordinal）。"地图点类型"与"实际房型"是两件事：
    /// 问号节点的 `MapPointType` 判出一个 `RoomType`，后者才是房间处理器与守卫的依据。
    /// </summary>
    public const string RoomTypeEnumName = "RoomType";

    public const string NodeTypeName = "MapNode";
    public const string NodesCollectionName = "MapNodes";
    public const string ChildrenRelationName = "MapChildren";
    public const string ChooseActionName = "ChooseMapPoint";
    public const string ChooseParameterName = "Node";
    public const int TravelReadyPhase = 2;
    public const int RestSitePhase = 4;
    public const int TreasurePhase = 5;
    public const int ActCompletedPhase = 6;

    /// <summary>
    /// ENV-T09.2：商店访问相位。取 7 而不是复用 6（`ActCompletedPhase` = 幕完成，语义与"正在逛商店"不同，
    /// 复用会让幕完成与未离店同时成立）。`MapPhase` 的字段域随本声明抬到 0..7，其他相位不变。
    /// </summary>
    public const int ShopPhase = 7;

    /// <summary>
    /// ENV-T09-unknown-room-modules-20261012：事件房访问相位。取 8 而不是复用别的相位——事件是独立房间，
    /// 有自己的当前事件身份与选项；`MapPhase` 的字段域随之抬到 0..<see cref="EventPhase"/>。
    /// </summary>
    public const int EventPhase = 8;

    public FieldSymbol RunActCompleted { get; private set; } = null!;
    public const string RestActionName = "RestAtCampfire";
    public const string SmithActionName = "SmithAtCampfire";

    /// <summary>物化预算（超出即具名失败，绝不静默截断）。</summary>
    public const int MaxCells = 120;

    public const int MaxEdges = 640;

    /// <summary>`Row` 上界：起始点 0、网格 1..15、BOSS 16（源 `BossMapPoint = (3, numberOfRooms + 1)`）。</summary>
    public const int MaxRow = 16;

    /// <summary>
    /// `MapFloor = Row + 1`，因此理论上最大 17（Row 16 的 BOSS 格）；旅行上限由地图决定，不能使用普通遭遇队列长度。
    /// </summary>
    public const int MaxFloor = 17;

    private const int MaxBlobLength = 32768;

    private static readonly (string Member, string Label)[] UnimplementedTypes =
    [
        ("Unknown", "未知事件"),
        ("Ancient", "远古/起始点"),
        ("Unassigned", "未分配"),
    ];

    private MapRoomWiring(
        EnumTypeSymbol pointType,
        EntityTypeSymbol node,
        FieldSymbol nodeCol,
        FieldSymbol nodeRow,
        FieldSymbol nodeType,
        FieldSymbol nodeIndex,
        FieldSymbol nodeVisited,
        CollectionSymbol nodes,
        RelationSymbol children,
        FieldSymbol runCurrentNode,
        FieldSymbol runMapPhase,
        FieldSymbol runMapFloor,
        FieldSymbol runMapNodeCount,
        FieldSymbol runMapDraws,
        ComputeAssetSymbol generate,
        FieldSymbol genWords,
        FieldSymbol genRooms,
        FieldSymbol genDone,
        FieldSymbol genRest,
        FieldSymbol genUnknown,
        FieldSymbol genCells,
        FieldSymbol genEdges,
        FieldSymbol genWordsUsed,
        FieldSymbol genStart,
        ComputeAssetSymbol cell,
        FieldSymbol cellWords,
        FieldSymbol cellIndexIn,
        FieldSymbol cellExists,
        FieldSymbol cellCol,
        FieldSymbol cellRow,
        FieldSymbol cellType,
        ComputeAssetSymbol edge,
        FieldSymbol edgeWords,
        FieldSymbol edgeIndexIn,
        FieldSymbol edgeExists,
        FieldSymbol edgeParent,
        FieldSymbol edgeChild,
        EnumTypeSymbol roomType,
        FieldSymbol runActualRoomType)
    {
        PointType = pointType;
        Node = node;
        NodeCol = nodeCol;
        NodeRow = nodeRow;
        NodeType = nodeType;
        NodeIndex = nodeIndex;
        NodeVisited = nodeVisited;
        Nodes = nodes;
        Children = children;
        RunCurrentNode = runCurrentNode;
        RunMapPhase = runMapPhase;
        RunMapFloor = runMapFloor;
        RunMapNodeCount = runMapNodeCount;
        RunMapDraws = runMapDraws;
        Generate = generate;
        GenWords = genWords;
        GenRooms = genRooms;
        GenDone = genDone;
        GenRest = genRest;
        GenUnknown = genUnknown;
        GenCells = genCells;
        GenEdges = genEdges;
        GenWordsUsed = genWordsUsed;
        GenStart = genStart;
        Cell = cell;
        CellWords = cellWords;
        CellIndexIn = cellIndexIn;
        CellExists = cellExists;
        CellCol = cellCol;
        CellRow = cellRow;
        CellType = cellType;
        Edge = edge;
        EdgeWords = edgeWords;
        EdgeIndexIn = edgeIndexIn;
        EdgeExists = edgeExists;
        EdgeParent = edgeParent;
        EdgeChild = edgeChild;
        RoomType = roomType;
        RunActualRoomType = runActualRoomType;
    }

    public EnumTypeSymbol PointType { get; }

    /// <summary>源 `RoomType` 枚举符号（实际房型字段与问号房判定的取值域）。</summary>
    public EnumTypeSymbol RoomType { get; }

    public EntityTypeSymbol Node { get; }

    public FieldSymbol NodeCol { get; }

    public FieldSymbol NodeRow { get; }

    public FieldSymbol NodeType { get; }

    public FieldSymbol NodeIndex { get; }

    public FieldSymbol NodeVisited { get; }

    public CollectionSymbol Nodes { get; }

    public RelationSymbol Children { get; }

    /// <summary>观看者投影（房间子包与其它房间共享同一个投影符号，例如商店货架要进投影）。</summary>
    public ProjectionSymbol Projection { get; private set; } = null!;

    public FieldSymbol RunCurrentNode { get; }

    /// <summary>
    /// 当前访问的实际房型（源 `MapPointHistoryEntry.RoomType`）。直接房型节点在进入前由
    /// `RegisterChooseMapPoint` 写入自己的房型；问号节点由问号房入口写入判定结果。
    /// </summary>
    public FieldSymbol RunActualRoomType { get; }

    public FieldSymbol RunMapPhase { get; }

    public FieldSymbol RunMapFloor { get; }

    public FieldSymbol RunMapNodeCount { get; }

    public FieldSymbol RunMapDraws { get; }

    public ComputeAssetSymbol Generate { get; }

    public FieldSymbol GenWords { get; }

    public FieldSymbol GenRooms { get; }

    public FieldSymbol GenDone { get; }

    public FieldSymbol GenRest { get; }

    public FieldSymbol GenUnknown { get; }

    public FieldSymbol GenCells { get; }

    public FieldSymbol GenEdges { get; }

    public FieldSymbol GenWordsUsed { get; }

    public FieldSymbol GenStart { get; }

    public ComputeAssetSymbol Cell { get; }

    public FieldSymbol CellWords { get; }

    public FieldSymbol CellIndexIn { get; }

    public FieldSymbol CellExists { get; }

    public FieldSymbol CellCol { get; }

    public FieldSymbol CellRow { get; }

    public FieldSymbol CellType { get; }

    public ComputeAssetSymbol Edge { get; }

    public FieldSymbol EdgeWords { get; }

    public FieldSymbol EdgeIndexIn { get; }

    public FieldSymbol EdgeExists { get; }

    public FieldSymbol EdgeParent { get; }

    public FieldSymbol EdgeChild { get; }

    /// <summary>地图流引擎名（`base-spire:run:act_1_map`，与工厂补种用的名字必须逐字相同）。</summary>
    public static string ActMapStreamName => VanillaRandomLayout.ActMapStreamName(1);

    /// <summary>
    /// 声明地图子包的实体/字段/集合/关系/投影/计算资产。**不动任何既有声明**，只在 `actMapEnabled` 时调用。
    /// </summary>
    public static MapRoomWiring Declare(
        EnginePackageBuilder package,
        ProjectionSymbol projection,
        EntityTypeSymbol runEntity)
    {
        // 枚举成员顺序即 ordinal，必须与移植件 `MapPointType` 一致（Unassigned=0 … Ancient=8）。
        var pointType = package.Enum(PointTypeEnumName);
        pointType.Member("Unassigned")
            .Member("Unknown")
            .Member("Shop")
            .Member("Treasure")
            .Member("RestSite")
            .Member("Monster")
            .Member("Elite")
            .Member("Boss")
            .Member("Ancient");

        var node = package.EntityType(NodeTypeName);
        var nodeCol = node.AddField("Col", FieldTypeSchema.Int(0, 6));
        var nodeRow = node.AddField("Row", FieldTypeSchema.Int(0, MaxRow));
        var nodeType = node.AddField("Type", FieldTypeSchema.EnumOf(pointType));
        var nodeIndex = node.AddField("Index", FieldTypeSchema.Int(0, 512));
        var nodeVisited = node.AddField("Visited", FieldTypeSchema.Bool());
        var nodes = package.Collection(NodesCollectionName, node);
        var children = package.Relation(ChildrenRelationName, node);
        projection.Type(node);

        // 枚举成员顺序即 ordinal，必须与移植件 `MapPointType` 一致。
        var roomType = package.Enum(RoomTypeEnumName);
        roomType.Member("Unassigned")
            .Member("Monster")
            .Member("Elite")
            .Member("Boss")
            .Member("Treasure")
            .Member("Shop")
            .Member("Event")
            .Member("RestSite")
            .Member("Map");

        var runCurrentNode = runEntity.AddField("CurrentNode", FieldTypeSchema.RefTo(node), optional: true);
        // 0/2 select map, 1 battle, 3 rewards, 4 rest site, 5 treasure, 6 act completed, 7 shop, 8 event;
        // only 0/2 allow travel.
        var runMapPhase = runEntity.AddField("MapPhase", FieldTypeSchema.Int(0, EventPhase));
        var runMapFloor = runEntity.AddField("MapFloor", FieldTypeSchema.Int(0, MaxFloor));
        var runMapNodeCount = runEntity.AddField("MapNodeCount", FieldTypeSchema.Int(0, 512));
        // ENV-T09-unknown-room-modules-20261012：**实际房型**（源 `MapPointHistoryEntry.RoomType`）。
        // 地图节点的 `Type` 保持 `Unknown`，问号房判出的房型只写在这里；营火/商店/宝箱/战斗的
        // 适用守卫读它（`AtRoomType`），因此绝不能靠改写节点类型来实现"到了别的房"。
        var runActualRoomType = runEntity.AddField("ActualRoomType", FieldTypeSchema.EnumOf(roomType),
            Value.OfEnum(0));
        // Random replay words remain authoritative local diagnostics, outside the participant projection.
        var runMapDraws = runEntity.AddField("MapDraws", FieldTypeSchema.String(MaxBlobLength),
            visibility: FieldVisibility.Hidden);

        var generate = package.ComputeAsset(SourceActMapCompute.GenerateAssetName);
        generate.BackendId = BaseSpireComputeBackend.BackendId;
        generate.SourcePath = SourceActMapCompute.SourcePath;
        generate.SourceHash = SourceActMapCompute.SourceHash;
        generate.Entry = SourceActMapCompute.GenerateEntry;
        var genWords = generate.Input("Words", FieldTypeSchema.String(MaxBlobLength));
        var genRooms = generate.Input("Rooms", FieldTypeSchema.Int(0, 512));
        var genDone = generate.Output("Done", FieldTypeSchema.Bool());
        var genRest = generate.Output("Rest", FieldTypeSchema.Int(0, 99));
        var genUnknown = generate.Output("Unknown", FieldTypeSchema.Int(0, 99));
        var genCells = generate.Output("Cells", FieldTypeSchema.Int(0, 512));
        var genEdges = generate.Output("Edges", FieldTypeSchema.Int(0, 4096));
        var genWordsUsed = generate.Output("WordsUsed", FieldTypeSchema.Int(0, 4096));
        var genStart = generate.Output("StartCell", FieldTypeSchema.Int(0, 512));

        var cell = package.ComputeAsset(SourceActMapCompute.CellAssetName);
        cell.BackendId = BaseSpireComputeBackend.BackendId;
        cell.SourcePath = SourceActMapCompute.SourcePath;
        cell.SourceHash = SourceActMapCompute.SourceHash;
        cell.Entry = SourceActMapCompute.CellEntry;
        var cellWords = cell.Input("Words", FieldTypeSchema.String(MaxBlobLength));
        var cellIndexIn = cell.Input("Index", FieldTypeSchema.Int(0, 512));
        var cellExists = cell.Output("Exists", FieldTypeSchema.Bool());
        var cellCol = cell.Output("Col", FieldTypeSchema.Int(0, 6));
        var cellRow = cell.Output("Row", FieldTypeSchema.Int(0, MaxRow));
        var cellTypeOut = cell.Output("Type", FieldTypeSchema.EnumOf(pointType));

        var edge = package.ComputeAsset(SourceActMapCompute.EdgeAssetName);
        edge.BackendId = BaseSpireComputeBackend.BackendId;
        edge.SourcePath = SourceActMapCompute.SourcePath;
        edge.SourceHash = SourceActMapCompute.SourceHash;
        edge.Entry = SourceActMapCompute.EdgeEntry;
        var edgeWords = edge.Input("Words", FieldTypeSchema.String(MaxBlobLength));
        var edgeIndexIn = edge.Input("Index", FieldTypeSchema.Int(0, 4096));
        var edgeExists = edge.Output("Exists", FieldTypeSchema.Bool());
        var edgeParent = edge.Output("Parent", FieldTypeSchema.Int(0, 512));
        var edgeChild = edge.Output("Child", FieldTypeSchema.Int(0, 512));

        var wiring = new MapRoomWiring(
            pointType, node, nodeCol, nodeRow, nodeType, nodeIndex, nodeVisited, nodes, children,
            runCurrentNode, runMapPhase, runMapFloor, runMapNodeCount, runMapDraws,
            generate, genWords, genRooms, genDone, genRest, genUnknown, genCells, genEdges, genWordsUsed, genStart,
            cell, cellWords, cellIndexIn, cellExists, cellCol, cellRow, cellTypeOut,
            edge, edgeWords, edgeIndexIn, edgeExists, edgeParent, edgeChild,
            roomType, runActualRoomType);
        wiring.RunActCompleted = runEntity.AddField("ActCompleted", FieldTypeSchema.Bool());
        wiring.Projection = projection;
        return wiring;
    }

    /// <summary>
    /// 开局物化步骤（插在 `Run` 实体创建之后）：
    /// <list type="number">
    /// <item>抽数循环：每次多抽**一个** payload 追加进 blob，重放；拿到 `Done` 即停
    ///       ⇒ 消费字数与源逐字一致（不补齐、不多抽一个字）。</item>
    /// <item>物化循环：按 cell 下标建 `MapNode` 实体并加入 `MapNodes`。</item>
    /// <item>边循环：按边下标 `AddRelation(MapChildren, parent, child)`。</item>
    /// <item>收尾：`CurrentNode` = 起始点、`MapPhase` = 0、`MapNodeCount` = 节点数、`MapDraws` = 词表，
    ///       并自检 `Count(MapNodes) == Cells`。</item>
    /// </list>
    /// </summary>
    public IReadOnlyList<Inst> SetupSteps(IrScope scope, CollectionSymbol runStates)
    {
        var run = scope.First(scope.Collection(runStates));
        var blob = scope.Local("mapBlob", FieldTypeSchema.String(MaxBlobLength));
        var done = scope.Local("mapDone", FieldTypeSchema.Bool());
        var restSlot = scope.Local("mapRest", FieldTypeSchema.Int(0, 99));
        var unknownSlot = scope.Local("mapUnknown", FieldTypeSchema.Int(0, 99));
        var cellsSlot = scope.Local("mapCells", FieldTypeSchema.Int(0, 512));
        var edgesSlot = scope.Local("mapEdges", FieldTypeSchema.Int(0, 4096));
        var wordsSlot = scope.Local("mapWordsUsed", FieldTypeSchema.Int(0, 4096));
        var startSlot = scope.Local("mapStartCell", FieldTypeSchema.Int(0, 512));
        var payload = scope.Local("mapPayload", FieldTypeSchema.Int(0, SourceActMapCompute.Span - 1));
        var cellIndex = scope.Local("mapCellIndex", FieldTypeSchema.Int(0, 512));
        var cellExistsSlot = scope.Local("mapCellExists", FieldTypeSchema.Bool());
        var cellColSlot = scope.Local("mapCellCol", FieldTypeSchema.Int(0, 6));
        var cellRowSlot = scope.Local("mapCellRow", FieldTypeSchema.Int(0, MaxRow));
        var cellTypeSlot = scope.Local("mapCellType", FieldTypeSchema.EnumOf(PointType));
        var created = scope.Local("mapCreatedNode", FieldTypeSchema.Binding());
        var edgeIndex = scope.Local("mapEdgeIndex", FieldTypeSchema.Int(0, 4096));
        var edgeExistsSlot = scope.Local("mapEdgeExists", FieldTypeSchema.Bool());
        var edgeParentSlot = scope.Local("mapEdgeParent", FieldTypeSchema.Int(0, 512));
        var edgeChildSlot = scope.Local("mapEdgeChild", FieldTypeSchema.Int(0, 512));

        var steps = new List<Inst>
        {
            scope.SetLocal(blob, scope.Const(string.Empty)),
            scope.SetLocal(done, scope.Const(false)),
        };

        // -- 抽数 + 重放（每轮恰一个 payload） ---------------------------------------------------------
        steps.Add(scope.Repeat(scope.Const((long)SourceActMapCompute.WordBudget), new Inst[]
        {
            scope.If(scope.Not(scope.LocalRef(done)), new Inst[]
            {
                scope.SampleInt(ActMapStreamName, scope.Const(0L), scope.Const(SourceActMapCompute.Span), payload),
                scope.SetLocal(blob, scope.Concat(
                    scope.LocalRef(blob), scope.Const(";"), scope.LocalRef(payload))),
                scope.Compute(Generate, new (FieldSymbol, Expr)[]
                {
                    (GenWords, scope.LocalRef(blob)),
                    (GenRooms, scope.Const((long)SourceActMapCompute.ActRooms)),
                }, new (FieldSymbol, int)[]
                {
                    (GenDone, done),
                    (GenRest, restSlot),
                    (GenUnknown, unknownSlot),
                    (GenCells, cellsSlot),
                    (GenEdges, edgesSlot),
                    (GenWordsUsed, wordsSlot),
                    (GenStart, startSlot),
                }),
            }),
        }));
        steps.Add(scope.If(scope.Not(scope.LocalRef(done)), new Inst[]
        {
            scope.Fail("base-spire: 幕 1 地图生成超出抽数预算（未完成即停，不补齐随机数）"),
        }));

        // -- 物化节点 ---------------------------------------------------------------------------------
        steps.Add(scope.If(scope.Greater(scope.LocalRef(cellsSlot), scope.Const((long)MaxCells)), new Inst[]
        {
            scope.Fail($"base-spire: 幕 1 地图节点数超出物化预算 {MaxCells}"),
        }));
        steps.Add(scope.SetLocal(cellIndex, scope.Const(0L)));
        steps.Add(scope.Repeat(scope.Const((long)MaxCells), new Inst[]
        {
            scope.If(scope.Less(scope.LocalRef(cellIndex), scope.LocalRef(cellsSlot)), new Inst[]
            {
                scope.Compute(Cell, new (FieldSymbol, Expr)[]
                {
                    (CellWords, scope.LocalRef(blob)),
                    (CellIndexIn, scope.LocalRef(cellIndex)),
                }, new (FieldSymbol, int)[]
                {
                    (CellExists, cellExistsSlot),
                    (CellCol, cellColSlot),
                    (CellRow, cellRowSlot),
                    (CellType, cellTypeSlot),
                }),
                scope.If(scope.LocalRef(cellExistsSlot), new Inst[]
                {
                    scope.CreateEntity(Node, scope.EntityScope(run), created, new (FieldSymbol, Expr)[]
                    {
                        (NodeCol, scope.LocalRef(cellColSlot)),
                        (NodeRow, scope.LocalRef(cellRowSlot)),
                        (NodeType, scope.LocalRef(cellTypeSlot)),
                        (NodeIndex, scope.LocalRef(cellIndex)),
                        (NodeVisited, scope.Const(false)),
                    }, Nodes),
                }),
                scope.SetLocal(cellIndex, scope.Add(scope.LocalRef(cellIndex), scope.Const(1L))),
            }),
        }));

        // -- 物化边（源 `MapPoint.Children`） ----------------------------------------------------------
        steps.Add(scope.If(scope.Greater(scope.LocalRef(edgesSlot), scope.Const((long)MaxEdges)), new Inst[]
        {
            scope.Fail($"base-spire: 幕 1 地图边数超出物化预算 {MaxEdges}"),
        }));
        steps.Add(scope.SetLocal(edgeIndex, scope.Const(0L)));
        steps.Add(scope.Repeat(scope.Const((long)MaxEdges), new Inst[]
        {
            scope.If(scope.Less(scope.LocalRef(edgeIndex), scope.LocalRef(edgesSlot)), new Inst[]
            {
                scope.Compute(Edge, new (FieldSymbol, Expr)[]
                {
                    (EdgeWords, scope.LocalRef(blob)),
                    (EdgeIndexIn, scope.LocalRef(edgeIndex)),
                }, new (FieldSymbol, int)[]
                {
                    (EdgeExists, edgeExistsSlot),
                    (EdgeParent, edgeParentSlot),
                    (EdgeChild, edgeChildSlot),
                }),
                scope.If(scope.LocalRef(edgeExistsSlot), new Inst[]
                {
                    scope.AddRelation(Children, NodeAt(scope, edgeParentSlot), NodeAt(scope, edgeChildSlot)),
                }),
                scope.SetLocal(edgeIndex, scope.Add(scope.LocalRef(edgeIndex), scope.Const(1L))),
            }),
        }));

        // -- 游标与自检 -------------------------------------------------------------------------------
        steps.Add(scope.SetField(run, RunMapNodeCount, scope.LocalRef(cellsSlot)));
        steps.Add(scope.SetField(run, RunMapDraws, scope.LocalRef(blob)));
        steps.Add(scope.SetField(run, RunCurrentNode, NodeAt(scope, startSlot)));
        steps.Add(scope.SetField(run, RunMapPhase, scope.Const(0L)));
        steps.Add(scope.If(scope.NotEqual(scope.Count(scope.Collection(Nodes)), scope.LocalRef(cellsSlot)),
            new Inst[]
            {
                scope.Fail("base-spire: 幕 1 地图物化节点数与重放结果不一致"),
            }));
        return steps;
    }

    /// <summary>战斗结算边界之后把 `MapPhase` 推到 2（`RoomSettled`）：奖励子包的插入点。</summary>
    public Inst SettleStep(IrScope scope, Expr run) => scope.SetField(run, RunMapPhase, scope.Const(2L));

    /// <summary>
    /// "当前这次访问是不是这个房型"（不含相位）：实际房型字段命中（问号房判出的房型），
    /// 或地图节点类型就是它（直接房型节点）。
    ///
    /// 为什么两选一都要看：地图节点类型是**地图事实**（问号节点永远是 `Unknown`），而房间守卫要问的是
    /// **当前房间事实**。只查节点类型会让问号房判出的宝箱/商店拿不到自己的操作；只查实际房型则要求
    /// 每个进入路径都记得先写它。`pointType` 使用源枚举名，且必须在源 `RoomType` 里有同名成员
    /// （`Monster`/`Elite`/`Boss`/`Treasure`/`Shop`/`RestSite`）——`Unknown`/`Ancient`/`Unassigned`
    /// 不是房型，不能传进来。
    /// </summary>
    public Expr AtRoomType(IrScope scope, Expr run, string pointType) => scope.Or(
        scope.Equal(scope.Field(run, RunActualRoomType), scope.EnumConst(RoomType, pointType)),
        scope.Equal(scope.Field(scope.Field(run, RunCurrentNode), NodeType),
            scope.EnumConst(PointType, pointType)));

    /// <summary>
    /// 源 `RoomType` 成员名 → 直接房型节点的写入值；问号/远古/未分配节点**没有**直接房型，
    /// 返回 null 表示"这一条进入路径不写实际房型"（问号房入口自己写判定结果）。
    /// </summary>
    public static string? DirectRoomType(string pointType) => pointType switch
    {
        "Monster" or "Elite" or "Boss" or "Treasure" or "Shop" or "RestSite" => pointType,
        _ => null,
    };

    /// <summary>
    /// 注册 `ChooseMapPoint`：候选 = `MapChildren(CurrentNode)`（源 `MapTravel.GetTravelablePointsFrom`），
    /// 守卫 = 阶段（选点 0 或已结算 2）且未到地图末尾 <see cref="MaxFloor"/>。按房型分派：
    /// 普通战斗与**精英**进 `StartNextRoom`（遭遇分别来自普通/精英队列），营火/宝箱/商店各自处理，
    /// `Unknown` 由问号房模块判定后复用上述入口，其余房型具名 `Fail(...)`（节点保留、字段零改动）。
    ///
    /// 每条入口在进入处理器**之前**写 `Run.ActualRoomType`（<see cref="DirectRoomType"/> 为 null 的
    /// `Unknown`/`Ancient`/`Unassigned` 不写——问号房入口自己写源判定结果），因此房间守卫与
    /// `BattleRoomCompilation` 的房型判定读的都是"本次访问的实际房型"。
    /// </summary>
    public void RegisterChooseMapPoint(
        EnginePackageBuilder package,
        CollectionSymbol runStates,
        ProcedureBuilder startNextRoom,
        TreasureRoomWiring treasure,
        ShopRoomWiring shop) =>
        RegisterChooseMapPoint(package, runStates, MapRoomEntry.Standard(this, startNextRoom, treasure, shop));

    public void RegisterChooseMapPoint(EnginePackageBuilder package, CollectionSymbol runStates,
        IReadOnlyList<MapRoomEntry> entries)
    {
        var registeredTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
            if (!registeredTypes.Add(entry.PointType))
                throw new ArgumentException($"Duplicate map room entry '{entry.PointType}'", nameof(entries));
        var action = package.Action(ChooseActionName);
        var parameter = action.Parameter(ChooseParameterName, FieldTypeSchema.RefTo(Node));
        var body = package.ActionBody(action);
        var run = body.First(body.Collection(runStates));
        var nodeRef = body.Param(parameter.DeclarationIndex);
        body.Role(SliceManifest.PlayerRole);
        body.Candidates(ChooseParameterName, body.Relation(Children, body.Field(run, RunCurrentNode)));
        body.Requires(body.And(
            body.Or(
                body.Equal(body.Field(run, RunMapPhase), body.Const(0L)),
                body.Equal(body.Field(run, RunMapPhase), body.Const(2L))),
            body.Less(body.Field(run, RunMapFloor), body.Const((long)MaxFloor))));

        var instructions = new List<Inst>();
        foreach (var entry in entries)
        {
            // 实际房型先于进入处理器写：直接房型节点写自己的房型；问号节点不写（处理器会按源判定结果写）。
            var steps = new List<Inst>();
            if (DirectRoomType(entry.PointType) is { } direct)
            {
                steps.Add(body.SetField(run, RunActualRoomType, body.EnumConst(RoomType, direct)));
            }

            steps.AddRange(entry.Enter(body, run, nodeRef));
            instructions.Add(body.If(
                body.Equal(body.Field(nodeRef, NodeType), body.EnumConst(PointType, entry.PointType)),
                steps.ToArray()));
        }
        foreach (var (member, label) in UnimplementedTypes.Where(item => !registeredTypes.Contains(item.Member)))
        {
            instructions.Add(body.If(
                body.Equal(body.Field(nodeRef, NodeType), body.EnumConst(PointType, member)),
                new Inst[]
                {
                    body.Fail($"base-spire: 房间类型 {label} 尚未实现（地图节点保留、不伪装空事件）"),
                }));
        }
        foreach (var member in PointType.Members.Where(member => !registeredTypes.Contains(member)
            && !UnimplementedTypes.Any(item => item.Member == member)))
            instructions.Add(body.If(body.Equal(body.Field(nodeRef, NodeType), body.EnumConst(PointType, member)),
                [body.Fail($"base-spire: no map room entry registered for {member}")]));

        body.Body(instructions.ToArray());

        // Expose the actual next operation at the development frontier. This preserves the public
        // nonterminal-decision contract without reporting an Act 1 clear as a whole-run victory.
        var advance = package.ActionBody(package.Action("ContinueToNextAct"));
        var completedRun = advance.First(advance.Collection(runStates));
        advance.Role(SliceManifest.PlayerRole);
        advance.Requires(advance.And(advance.Field(completedRun, RunActCompleted),
            advance.Equal(advance.Field(completedRun, RunMapPhase), advance.Const((long)ActCompletedPhase))));
        advance.Body(advance.Fail("base-spire: Act 1 completed; act transition is not implemented in dev-map scope"));
    }

    private Expr NodeAt(IrScope scope, int indexSlot) =>
        scope.First(scope.Where(scope.Collection(Nodes),
            item => scope.Equal(scope.Field(item, NodeIndex), scope.LocalRef(indexSlot))));
}
