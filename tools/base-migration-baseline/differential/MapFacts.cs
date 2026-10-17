using System.Globalization;

namespace Differential;

/// <summary>地图节点事实（两侧各自从自己的公开输出读出，交给本类型只做格式化）。</summary>
internal readonly record struct MapPointFact(int Col, int Row, int Type);

/// <summary>地图父子边事实。</summary>
internal readonly record struct MapEdgeFact(int ParentCol, int ParentRow, int ChildCol, int ChildRow);

/// <summary>
/// 地图机制事实的规范化词表（ENV-T04 地图独立对拍子包）。
/// 两侧共用同一份路径格式，值一律由各侧自己的引擎读出——本类型**不做任何推导**，
/// 不从词表反推网格、不代算边、不排序成"权威形状"。
/// </summary>
internal static class MapFacts
{
    public static IReadOnlyList<FactSnapshot> Build(
        IReadOnlyList<MapPointFact> points,
        IReadOnlyList<MapEdgeFact> edges,
        long wordsConsumed,
        MapPointFact? secondBoss)
    {
        var maxRow = points.Count == 0 ? 0 : points.Max(point => point.Row);
        var maxCol = points.Count == 0 ? 0 : points.Max(point => point.Col);

        var facts = new List<FactSnapshot>
        {
            new("map.count", Text(points.Count)),
            new("map.rows", Text(points.Count == 0 ? 0 : maxRow + 1)),
            new("map.cols", Text(points.Count == 0 ? 0 : maxCol + 1)),
            new("map.words_consumed", Text(wordsConsumed)),
            new("map.start", Pick(points, point => point.Row == 0, "起点（第 0 行）")),
            new("map.boss", Pick(points, point => point.Row == maxRow, "BOSS 点（最大行）")),
            new("map.second_boss", secondBoss is { } second ? $"[{second.Col},{second.Row}]" : "none"),
        };

        facts.AddRange(points
            .OrderBy(point => point.Row)
            .ThenBy(point => point.Col)
            .Select(point => new FactSnapshot($"map.node.[{point.Col},{point.Row}].type", Text(point.Type))));

        facts.AddRange(edges
            .OrderBy(edge => edge.ParentRow)
            .ThenBy(edge => edge.ParentCol)
            .ThenBy(edge => edge.ChildRow)
            .ThenBy(edge => edge.ChildCol)
            .Select(edge => new FactSnapshot(
                $"map.edge.[{edge.ParentCol},{edge.ParentRow}]->[{edge.ChildCol},{edge.ChildRow}]", "1")));

        return facts;
    }

    private static string Text(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Pick(
        IReadOnlyList<MapPointFact> points,
        Func<MapPointFact, bool> predicate,
        string what)
    {
        var matches = points.Where(predicate).ToList();
        return matches.Count switch
        {
            1 => $"[{matches[0].Col},{matches[0].Row}]",
            0 => $"none({what})",
            _ => $"multiple({matches.Count},{what})",
        };
    }
}
