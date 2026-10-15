using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseSpire.Slice.SourceMap;

/// <summary>
/// Detects and removes duplicate path segments from act maps.
/// Verbatim port of <c>MegaCrit.Sts2.Core.Map.MapPathPruning</c>; only the <c>Rng</c> parameter type is
/// replaced by <see cref="SourceMapRandom"/> and the source's <c>CollectionsMarshal.SetCount</c> +
/// span fills are expressed as equivalent list constructions. Draw order and every RNG call site match
/// the source.
///
/// <para><b>Determinism:</b> the segment dictionary is a <c>SortedDictionary</c> with
/// <c>StringComparer.Ordinal</c> (order-independent of enumeration), but <c>FindAllPaths</c> enumerates
/// <c>MapPoint.Children</c> (<see cref="HashSet{T}"/>), which the port preserves verbatim; see
/// README-port.md §C.</para>
/// </summary>
public static class MapPathPruning
{
    /// <summary>
    /// Prunes duplicate segments, then repairs any point types that fell below their target
    /// count. Since repair can create new duplicates, loops until stable (up to 3 iterations).
    /// </summary>
    public static void PruneAndRepair(
        MapPoint?[,] grid,
        HashSet<MapPoint> startMapPoints,
        ActMap map,
        MapPointTypeCounts pointTypeCounts,
        SourceMapRandom rng,
        Func<MapPointType, MapPoint, bool> isValidPointType)
    {
        for (int i = 0; i < 3; i++)
        {
            PruneDuplicateSegments(grid, startMapPoints, map.StartingMapPoint, rng);
            if (!RepairPrunedPointTypes(map, pointTypeCounts, rng, isValidPointType))
            {
                break;
            }
        }
    }

    /// <summary>
    /// After pruning removes nodes, some point types may fall below their target count.
    /// This repairs the map by replacing monsters with the missing types.
    /// </summary>
    /// <param name="map">The map to repair.</param>
    /// <param name="pointTypeCounts">Target counts for each point type.</param>
    /// <param name="rng">Random number generator used for replacement selection.</param>
    /// <param name="isValidPointType">Validates whether a type can be placed at a given point.</param>
    /// <returns>true if any repairs were made (indicating re-pruning may be needed)</returns>
    public static bool RepairPrunedPointTypes(
        ActMap map,
        MapPointTypeCounts pointTypeCounts,
        SourceMapRandom rng,
        Func<MapPointType, MapPoint, bool> isValidPointType)
    {
        bool flag = false;
        flag |= RepairPointType(map, MapPointType.Shop, pointTypeCounts.NumOfShops, rng, isValidPointType);
        flag |= RepairPointType(map, MapPointType.Elite, pointTypeCounts.NumOfElites, rng, isValidPointType);
        flag |= RepairPointType(map, MapPointType.RestSite, pointTypeCounts.NumOfRests, rng, isValidPointType);

        // Source uses `flag | RepairPointType(...)` (non-short-circuit), so the Unknown repair always runs.
        return flag | RepairPointType(map, MapPointType.Unknown, pointTypeCounts.NumOfUnknowns, rng, isValidPointType);
    }

    private static bool RepairPointType(
        ActMap map,
        MapPointType type,
        int targetCount,
        SourceMapRandom rng,
        Func<MapPointType, MapPoint, bool> isValidPointType)
    {
        int num = map.GetAllMapPoints().Count((MapPoint p) => p.PointType == type);
        int num2 = targetCount - num;
        if (num2 <= 0)
        {
            return false;
        }

        bool result = false;
        List<MapPoint> list = (from p in map.GetAllMapPoints()
            where p.PointType == MapPointType.Monster && p.CanBeModified
            select p).ToList();
        list.StableShuffle(rng);
        foreach (MapPoint item in list)
        {
            if (num2 == 0)
            {
                break;
            }

            if (isValidPointType(type, item))
            {
                item.PointType = type;
                num2--;
                result = true;
            }
        }

        return result;
    }

    /// <summary>
    /// Runs the full prune loop (up to 50 iterations) to remove duplicate segments.
    /// </summary>
    public static void PruneDuplicateSegments(
        MapPoint?[,] grid,
        HashSet<MapPoint> startMapPoints,
        MapPoint startingMapPoint,
        SourceMapRandom rng)
    {
        int num = 0;
        List<List<MapPoint[]>> matchingSegments = FindMatchingSegments(startingMapPoint);
        while (PrunePaths(grid, startMapPoints, matchingSegments, rng))
        {
            num++;
            if (num > 50)
            {
                throw new InvalidOperationException($"Unable to prune matching segments in {num} iterations");
            }

            matchingSegments = FindMatchingSegments(startingMapPoint);
        }
    }

    public static List<List<MapPoint[]>> FindMatchingSegments(MapPoint startingMapPoint)
    {
        List<List<MapPoint>> list = FindAllPaths(startingMapPoint);
        SortedDictionary<string, List<MapPoint[]>> segments = new SortedDictionary<string, List<MapPoint[]>>(StringComparer.Ordinal);
        foreach (List<MapPoint> item in list)
        {
            AddSegmentsToDictionary(item, segments);
        }

        return GetDuplicateSegments(segments);
    }

    public static List<List<MapPoint>> FindAllPaths(MapPoint currentMapPoint)
    {
        List<List<MapPoint>> list = new List<List<MapPoint>>();
        if (currentMapPoint.PointType == MapPointType.Boss)
        {
            // Source builds a one-element list through CollectionsMarshal.SetCount + span write.
            List<MapPoint> single = new List<MapPoint>(1) { currentMapPoint };
            list.Add(single);
            return list;
        }

        foreach (MapPoint child in currentMapPoint.Children)
        {
            List<List<MapPoint>> list3 = FindAllPaths(child);
            foreach (List<MapPoint> item in list3)
            {
                // Source: one-element list [currentMapPoint], then AddRange(item).
                List<MapPoint> list5 = new List<MapPoint>(1 + item.Count) { currentMapPoint };
                list5.AddRange(item);
                list.Add(list5);
            }
        }

        return list;
    }

    private static void AddSegmentsToDictionary(IReadOnlyList<MapPoint> path, IDictionary<string, List<MapPoint[]>> segments)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (!IsValidSegmentStartMapPoint(path[i]))
            {
                continue;
            }

            for (int j = 2; j < path.Count - i; j++)
            {
                MapPoint endMapPoint = path[i + j];
                if (IsValidSegmentEndMapPoint(endMapPoint))
                {
                    MapPoint[] array = path.Skip(i).Take(j + 1).ToArray();
                    string key = GenerateSegmentKey(array);
                    if (!segments.ContainsKey(key))
                    {
                        segments[key] = new List<MapPoint[]>(1) { array };
                    }
                    else if (!AnyOverlappingSegments(segments[key], array))
                    {
                        segments[key].Add(array);
                    }
                }
            }
        }
    }

    private static bool IsValidSegmentStartMapPoint(MapPoint startMapPoint)
    {
        if (startMapPoint.Children.Count <= 1)
        {
            return startMapPoint.coord.row == 0;
        }

        return true;
    }

    private static bool IsValidSegmentEndMapPoint(MapPoint endMapPoint)
    {
        return endMapPoint.parents.Count >= 2;
    }

    private static string GenerateSegmentKey(IReadOnlyList<MapPoint> segment)
    {
        StringBuilder stringBuilder = new StringBuilder();
        MapPoint mapPoint = segment[0];
        MapPoint mapPoint2 = segment[segment.Count - 1];
        if (mapPoint.coord.row == 0)
        {
            // Source appends the START ROW (not the column) in this branch, then the END
            // column/row. Reproduced verbatim (it only ever sees row 0 here, so every pre-Boss
            // segment key starts with "0-").
            stringBuilder.Append($"{mapPoint.coord.row}-{mapPoint2.coord.col},{mapPoint2.coord.row}-");
        }
        else
        {
            stringBuilder.Append($"{mapPoint.coord.col},{mapPoint.coord.row}-{mapPoint2.coord.col},{mapPoint2.coord.row}-");
        }

        stringBuilder.Append(string.Join(",", segment.Select((MapPoint point) => (int)point.PointType)));
        return stringBuilder.ToString();
    }

    private static bool AnyOverlappingSegments(IEnumerable<MapPoint[]> existingSegments, IReadOnlyList<MapPoint> segment)
    {
        return existingSegments.Any((MapPoint[] existingSegment) => OverlappingSegment(existingSegment, segment));
    }

    private static bool OverlappingSegment(IReadOnlyList<MapPoint> a, IReadOnlyList<MapPoint> b)
    {
        if (a.Count < 3 || b.Count < 3)
        {
            return false;
        }

        for (int i = 1; i <= a.Count - 2; i++)
        {
            if (object.Equals(a[i], b[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static List<List<MapPoint[]>> GetDuplicateSegments(IDictionary<string, List<MapPoint[]>> segments)
    {
        return segments.Values.Where((List<MapPoint[]> segmentList) => segmentList.Count > 1).ToList();
    }

    private static bool PrunePaths(
        MapPoint?[,] grid,
        HashSet<MapPoint> startMapPoints,
        IEnumerable<List<MapPoint[]>> matchingSegments,
        SourceMapRandom rng)
    {
        foreach (List<MapPoint[]> matchingSegment in matchingSegments)
        {
            matchingSegment.UnstableShuffle(rng);
            if (PruneAllButLast(grid, startMapPoints, matchingSegment) != 0)
            {
                return true;
            }

            if (BreakAParentChildRelationshipInAnySegment(matchingSegment))
            {
                return true;
            }
        }

        return false;
    }

    private static int PruneAllButLast(MapPoint?[,] grid, HashSet<MapPoint> startMapPoints, IReadOnlyList<MapPoint[]> matches)
    {
        int num = 0;
        foreach (MapPoint[] match in matches)
        {
            if (num == matches.Count - 1)
            {
                return num;
            }

            if (PruneSegment(grid, startMapPoints, match))
            {
                num++;
            }
        }

        return num;
    }

    private static bool PruneSegment(MapPoint?[,] grid, HashSet<MapPoint> startMapPoints, MapPoint[] segment)
    {
        bool result = false;
        for (int i = 0; i < segment.Length - 1; i++)
        {
            MapPoint mapPoint = segment[i];
            if (!IsInMap(grid, mapPoint))
            {
                return true;
            }

            if (mapPoint.Children.Count > 1 || mapPoint.parents.Count > 1 || mapPoint.parents.Any((MapPoint n) => n.Children.Count == 1 && !IsRemoved(grid, n)))
            {
                continue;
            }

            MapPoint[] source = segment.Skip(i).ToArray();
            if (!source.Any((MapPoint n) => n.Children.Count > 1 && n.parents.Count == 1))
            {
                if (segment[^1].parents.Count == 1)
                {
                    return false;
                }

                if (!mapPoint.Children.Where((MapPoint c) => !segment.Contains(c)).Any((MapPoint c) => c.parents.Count == 1))
                {
                    RemovePoint(grid, startMapPoints, mapPoint);
                    result = true;
                }
            }
        }

        return result;
    }

    private static void RemovePoint(MapPoint?[,] grid, HashSet<MapPoint> startMapPoints, MapPoint mapPoint)
    {
        grid[mapPoint.coord.col, mapPoint.coord.row] = null;
        startMapPoints.Remove(mapPoint);
        foreach (MapPoint item in mapPoint.Children.ToList())
        {
            mapPoint.RemoveChildPoint(item);
        }

        foreach (MapPoint item2 in mapPoint.parents.ToList())
        {
            item2.RemoveChildPoint(mapPoint);
        }
    }

    private static bool IsInMap(MapPoint?[,] grid, MapPoint mapPoint)
    {
        if (grid[mapPoint.coord.col, mapPoint.coord.row] == null && mapPoint.PointType != MapPointType.Ancient)
        {
            return mapPoint.PointType == MapPointType.Boss;
        }

        return true;
    }

    private static bool IsRemoved(MapPoint?[,] grid, MapPoint mapPoint)
    {
        return grid[mapPoint.coord.col, mapPoint.coord.row] == null;
    }

    private static bool BreakAParentChildRelationshipInAnySegment(List<MapPoint[]> matches)
    {
        foreach (MapPoint[] match in matches)
        {
            if (BreakAParentChildRelationshipInSegment(match))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BreakAParentChildRelationshipInSegment(MapPoint[] segment)
    {
        bool result = false;
        for (int i = 0; i < segment.Length - 1; i++)
        {
            MapPoint mapPoint = segment[i];
            if (mapPoint.Children.Count >= 2)
            {
                MapPoint mapPoint2 = segment[i + 1];
                if (mapPoint2.parents.Count != 1)
                {
                    mapPoint.RemoveChildPoint(mapPoint2);
                    result = true;
                }
            }
        }

        return result;
    }
}
