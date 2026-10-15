using CoreTheSpire.Engine;

namespace Differential;

/// <summary>
/// base 侧**地图机制**的规范化投影（ENV-T04 地图独立对拍子包）。
///
/// 只读引擎的公开投影：`base-spire:MapNodes` 集合里 `MapNode` 的 `Col`/`Row`/`Type`，
/// 以及 `base-spire:MapChildren` 关系的父子边，外加随机服务的流诊断。
/// **不**用 `SourceActMapCompute` 把词表反推成网格——那是拿 base 自己的重放实现自证形状。
/// </summary>
internal static class MapSnapshot
{
    /// <summary>幕 1 地图流的引擎流名（`MapRoomWiring.ActMapStreamName`）。</summary>
    public const string StreamName = "base-spire:run:act_1_map";

    private const string NodeTypeId = "base-spire:MapNode";
    private const string NodesCollection = "base-spire:MapNodes";
    private const string ChildrenRelation = "base-spire:MapChildren";

    public static IReadOnlyList<FactSnapshot> Facts(Session session)
    {
        var nodeType = session.RuleSet.TypeByQualifiedId(NodeTypeId);
        var nodes = session.RuleSet.Collections.Single(item => item.QualifiedId == NodesCollection).Id;
        var children = session.RuleSet.Relations.Single(item => item.QualifiedId == ChildrenRelation).Id;
        var colField = nodeType.FieldIndex("Col");
        var rowField = nodeType.FieldIndex("Row");
        var typeField = nodeType.FieldIndex("Type");

        var instances = session.State.CollectionMembers(nodes)
            .Select(id => new
            {
                Id = id,
                Col = (int)session.State.GetField(id, colField).AsInt(),
                Row = (int)session.State.GetField(id, rowField).AsInt(),
                Type = session.State.GetField(id, typeField).AsEnumOrdinal(),
            })
            .ToList();

        var byId = new Dictionary<InstanceId, (int Col, int Row)>();
        foreach (var instance in instances)
        {
            byId[instance.Id] = (instance.Col, instance.Row);
        }

        var points = instances
            .Select(instance => new MapPointFact(instance.Col, instance.Row, instance.Type))
            .ToList();

        var edges = new List<MapEdgeFact>();
        foreach (var instance in instances)
        {
            foreach (var target in session.State.RelationTargets(children, instance.Id))
            {
                if (byId.TryGetValue(target, out var child))
                {
                    edges.Add(new MapEdgeFact(instance.Col, instance.Row, child.Col, child.Row));
                }
            }
        }

        // base 侧没有"二王点"概念（重放参数 hasSecondBoss=false 时不产生），故传 null。
        return MapFacts.Build(points, edges, session.Random.DrawCount(StreamName), secondBoss: null);
    }

    /// <summary>只保留地图流：base 正常开局还会消费 UpFront/洗牌等流，那些不属于本 case。</summary>
    public static IReadOnlyList<StreamSnapshot> Streams(Session session) =>
        [.. EngineSnapshot.Streams(session).Where(item => item.Stream == StreamName)];
}
