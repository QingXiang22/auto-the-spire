using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseSpire.Slice.SourceMap;

/// <summary>
/// Verbatim port of <c>MegaCrit.Sts2.Core.Map.StandardActMap</c> (path generation, point-type
/// assignment, legality rules, pruning and post-processing). Method names, statement order, evaluation
/// order and every RNG call site match the source; see README-port.md §B for the deviation list.
///
/// <para>Deviations (structural only, no algorithmic change):
/// <list type="number">
/// <item>the constructor takes <c>int numberOfRooms</c> + <c>MapPointTypeCounts</c> instead of the game's
/// <c>ActModel</c> (source: <c>_mapLength = actModel.GetNumberOfRooms(isMultiplayer) + 1</c> and
/// <c>_pointTypeCounts = mapPointTypeCountsOverride ?? actModel.GetMapPointTypes(mapRng)</c>); the
/// caller is therefore responsible for drawing the act's rest/unknown Gaussians from the same supply
/// BEFORE constructing this class;</item>
/// <item><c>isMultiplayer</c> is dropped (source only subtracts one room from the act's base count);</item>
/// <item><c>shouldReplaceTreasureWithElites</c>/<c>ShouldReplaceTreasureWithElites</c> is dropped, so
/// <c>AssignPointTypes</c> always writes <c>Treasure</c> to row <c>GetRowCount() - 7</c> (the source's
/// else-branch). A caller that models the Warden ascension must pass a modified
/// <see cref="MapPointTypeCounts"/> / post-process instead.</item>
/// </list></para>
/// </summary>
public sealed class StandardActMap : ActMap
{
    public const int maxElites = 15;

    /// <summary>Declared in the source but unused there too (the loop bound is the literal 7).</summary>
    private const int _iterations = 7;

    /// <summary>Declared in the source but unused there too (the draw bound is the literal 7).</summary>
    private const int _mapWidth = 7;

    private readonly MapPointTypeCounts _pointTypeCounts;

    private readonly int _mapLength;

    private readonly SourceMapRandom _rng;

    private static readonly HashSet<MapPointType> _lowerMapPointRestrictions = new HashSet<MapPointType>
    {
        MapPointType.RestSite,
        MapPointType.Elite,
    };

    private static readonly HashSet<MapPointType> _upperMapPointRestrictions = new HashSet<MapPointType> { MapPointType.RestSite };

    /// These cannot be 2 times in a row.
    private static readonly HashSet<MapPointType> _parentMapPointRestrictions = new HashSet<MapPointType>
    {
        MapPointType.Elite,
        MapPointType.RestSite,
        MapPointType.Treasure,
        MapPointType.Shop,
    };

    /// These cannot be 2 times in a row.
    private static readonly HashSet<MapPointType> _childMapPointRestrictions = new HashSet<MapPointType>
    {
        MapPointType.Elite,
        MapPointType.RestSite,
        MapPointType.Treasure,
        MapPointType.Shop,
    };

    /// Only these point types are allowed to be siblings
    private static readonly HashSet<MapPointType> _siblingPointTypeRestrictions = new HashSet<MapPointType>
    {
        MapPointType.RestSite,
        MapPointType.Monster,
        MapPointType.Unknown,
        MapPointType.Elite,
        MapPointType.Shop,
    };

    public override MapPoint BossMapPoint { get; }

    public override MapPoint StartingMapPoint { get; }

    public override MapPoint? SecondBossMapPoint { get; }

    protected override MapPoint?[,] Grid { get; }

    /// <param name="mapRng">The rng for the map (source: <c>Rng mapRng</c>).</param>
    /// <param name="numberOfRooms">
    /// Source: <c>actModel.GetNumberOfRooms(isMultiplayer)</c> — for act 1 (Overgrowth) single-player this
    /// is 15, and <c>_mapLength</c> becomes 16.
    /// </param>
    /// <param name="pointTypeCounts">Source: <c>mapPointTypeCountsOverride ?? actModel.GetMapPointTypes(mapRng)</c>.</param>
    /// <param name="hasSecondBoss">Whether this act has a second boss (Double Boss ascension mode).</param>
    /// <param name="enablePruning">Used for tests, should always be true if used for gameplay logic.</param>
    public StandardActMap(
        SourceMapRandom mapRng,
        int numberOfRooms,
        MapPointTypeCounts pointTypeCounts,
        bool hasSecondBoss = false,
        bool enablePruning = true)
    {
        _mapLength = numberOfRooms + 1;
        Grid = new MapPoint[7, _mapLength];
        _rng = mapRng;
        _pointTypeCounts = pointTypeCounts;
        BossMapPoint = new MapPoint(GetColumnCount() / 2, GetRowCount());
        StartingMapPoint = new MapPoint(GetColumnCount() / 2, 0);
        if (hasSecondBoss)
        {
            SecondBossMapPoint = new MapPoint(GetColumnCount() / 2, GetRowCount() + 1);
        }

        GenerateMap();
        AssignPointTypes();
        if (enablePruning)
        {
            MapPathPruning.PruneAndRepair(Grid, startMapPoints, this, _pointTypeCounts, _rng, IsValidPointType);
        }

        Grid = MapPostProcessing.CenterGrid(Grid);
        Grid = MapPostProcessing.SpreadAdjacentMapPoints(Grid);
        Grid = MapPostProcessing.StraightenPaths(Grid);
    }

    private MapPoint GetOrCreateMapPoint(MapCoord coord)
    {
        return GetOrCreatePoint(coord.col, coord.row);
    }

    private MapPoint GetOrCreatePoint(int col, int row)
    {
        MapPoint? point = GetPoint(col, row);
        if (point != null)
        {
            return point;
        }

        point = new MapPoint(col, row);
        Grid[col, row] = point;
        return point;
    }

    private void PathGenerate(MapPoint startingPoint)
    {
        MapPoint mapPoint = startingPoint;
        while (mapPoint.coord.row < _mapLength - 1)
        {
            MapCoord coord = GenerateNextCoord(mapPoint);
            MapPoint orCreateMapPoint = GetOrCreateMapPoint(coord);
            mapPoint.AddChildPoint(orCreateMapPoint);
            mapPoint = orCreateMapPoint;
        }
    }

    private MapCoord GenerateNextCoord(MapPoint current)
    {
        int col = current.coord.col;
        int num = Math.Max(0, col - 1);
        int num2 = Math.Min(col + 1, 6);

        // Source fills this list through CollectionsMarshal.SetCount + a span with the same three values
        // in the same order: -1, 0, 1.
        List<int> list = new List<int>(3) { -1, 0, 1 };
        List<int> list2 = list;
        list2.StableShuffle(_rng);
        foreach (int item in list2)
        {
            int row = current.coord.row + 1;
            int num5 = item switch
            {
                -1 => num,
                0 => col,
                1 => num2,
                _ => throw new InvalidOperationException("This isn't possible"),
            };
            if (!HasInvalidCrossover(current, num5))
            {
                return new MapCoord
                {
                    col = num5,
                    row = row,
                };
            }
        }

        // Source: $"Cannot find next node: seed={_rng.Seed}". The word supply has no seed, so the rng
        // position is reported instead; the exception type and control flow are unchanged.
        throw new InvalidOperationException($"Cannot find next node: drawsUsed={_rng.DrawsUsed}");
    }

    private bool HasInvalidCrossover(MapPoint current, int targetX)
    {
        int num = targetX - current.coord.col;
        if (num == 0 || num == 7)
        {
            return false;
        }

        MapPoint? mapPoint = Grid[targetX, current.coord.row];
        if (mapPoint == null)
        {
            return false;
        }

        foreach (MapPoint child in mapPoint.Children)
        {
            int num2 = child.coord.col - mapPoint.coord.col;
            if (num2 == -num)
            {
                return true;
            }
        }

        return false;
    }

    private void GenerateMap()
    {
        for (int i = 0; i < 7; i++)
        {
            MapPoint orCreatePoint = GetOrCreatePoint(_rng.NextInt(0, 7), 1);
            if (i == 1)
            {
                while (startMapPoints.Contains(orCreatePoint))
                {
                    orCreatePoint = GetOrCreatePoint(_rng.NextInt(0, 7), 1);
                }
            }

            startMapPoints.Add(orCreatePoint);
            PathGenerate(orCreatePoint);
        }

        ForEachInRow(Grid, GetRowCount() - 1, delegate(MapPoint x)
        {
            x.AddChildPoint(BossMapPoint);
        });
        if (SecondBossMapPoint != null)
        {
            BossMapPoint.AddChildPoint(SecondBossMapPoint);
        }

        ForEachInRow(Grid, 1, delegate(MapPoint x)
        {
            StartingMapPoint.AddChildPoint(x);
        });
    }

    private static void ForEachInRow(MapPoint?[,] grid, int rowIndex, Action<MapPoint> processor, bool canBeModified = false)
    {
        for (int i = 0; i < grid.GetLength(0); i++)
        {
            MapPoint? mapPoint = grid[i, rowIndex];
            if (mapPoint != null)
            {
                processor(mapPoint);
            }
        }
    }

    private void AssignPointTypes()
    {
        ForEachInRow(Grid, GetRowCount() - 1, delegate(MapPoint p)
        {
            p.PointType = MapPointType.RestSite;
            p.CanBeModified = false;
        });

        // Source: if (ShouldReplaceTreasureWithElites) { Elite } else { Treasure }.
        // This port drops the Warden flag and always takes the Treasure branch.
        ForEachInRow(Grid, GetRowCount() - 7, delegate(MapPoint p)
        {
            p.PointType = MapPointType.Treasure;
            p.CanBeModified = false;
        });

        ForEachInRow(Grid, 1, delegate(MapPoint p)
        {
            p.PointType = MapPointType.Monster;
            p.CanBeModified = false;
        });

        List<MapPointType> list = new List<MapPointType>();
        for (int num = 0; num < _pointTypeCounts.NumOfRests; num++)
        {
            list.Add(MapPointType.RestSite);
        }

        for (int num2 = 0; num2 < _pointTypeCounts.NumOfShops; num2++)
        {
            list.Add(MapPointType.Shop);
        }

        for (int num3 = 0; num3 < _pointTypeCounts.NumOfElites; num3++)
        {
            list.Add(MapPointType.Elite);
        }

        for (int num4 = 0; num4 < _pointTypeCounts.NumOfUnknowns; num4++)
        {
            list.Add(MapPointType.Unknown);
        }

        Queue<MapPointType> pointTypesToBeAssigned = new Queue<MapPointType>(list);
        AssignRemainingTypesToRandomPoints(pointTypesToBeAssigned);
        foreach (MapPoint item in from x in GetAllMapPoints()
            where x.PointType == MapPointType.Unassigned
            select x)
        {
            item.PointType = MapPointType.Monster;
        }

        BossMapPoint.PointType = MapPointType.Boss;
        StartingMapPoint.PointType = MapPointType.Ancient;
        if (SecondBossMapPoint != null)
        {
            SecondBossMapPoint.PointType = MapPointType.Boss;
        }
    }

    /// <summary>
    /// Unreachable from the generator in the source too (nothing calls it); kept verbatim for fidelity.
    /// </summary>
    private void EnsureRowsContainsPointType(MapPointType pointType, List<List<MapPoint>> rows)
    {
        if (!RowsContainPointType(pointType, rows))
        {
            Queue<MapPointType> queue = new Queue<MapPointType>();
            queue.Enqueue(pointType);
            AssignPointTypesToRandomRows(queue, rows);
        }
    }

    /// <summary>
    /// Unreachable from the generator in the source too (nothing calls it); kept verbatim for fidelity.
    /// </summary>
    private static bool RowsContainPointType(MapPointType pointType, IEnumerable<List<MapPoint>> rows)
    {
        return rows.SelectMany((List<MapPoint> row) => row.Where((MapPoint p) => p.PointType == pointType)).Any();
    }

    /// <summary>
    /// Unreachable from the generator in the source too (nothing calls it); kept verbatim for fidelity.
    /// </summary>
    private List<List<MapPoint>> GetRows(int firstRow, int lastRow)
    {
        List<List<MapPoint>> list = new List<List<MapPoint>>();
        for (int i = firstRow; i <= lastRow; i++)
        {
            List<MapPoint> list2 = new List<MapPoint>();
            for (int j = 0; j < 7; j++)
            {
                MapPoint? mapPoint = Grid[j, i];
                if (mapPoint != null)
                {
                    list2.Add(mapPoint);
                }
            }

            list.Add(list2);
        }

        return list;
    }

    /// <summary>
    /// Assigns point types to the map by row to ensure that the point types are more evenly distributed.
    /// </summary>
    /// <param name="pointTypesToBeAssigned">Point types left to be assigned.</param>
    /// <param name="rows">Rows of map points to assign the types to.</param>
    /// <remarks>Unreachable from the generator in the source too (nothing calls it); kept verbatim for fidelity.</remarks>
    private void AssignPointTypesToRandomRows(Queue<MapPointType> pointTypesToBeAssigned, List<List<MapPoint>> rows)
    {
        rows.UnstableShuffle(_rng);
        foreach (List<MapPoint> row in rows)
        {
            row.StableShuffle(_rng);
            using IEnumerator<MapPoint> enumerator2 = row.Where((MapPoint r) => r.PointType == MapPointType.Unassigned).GetEnumerator();
            if (enumerator2.MoveNext())
            {
                MapPoint current2 = enumerator2.Current;
                MapPointType nextValidPointType = GetNextValidPointType(pointTypesToBeAssigned, current2);
                current2.PointType = nextValidPointType;
            }
        }
    }

    private void AssignRemainingTypesToRandomPoints(Queue<MapPointType> pointTypesToBeAssigned)
    {
        for (int i = 0; i < 3; i++)
        {
            if (pointTypesToBeAssigned.Count <= 0)
            {
                break;
            }

            List<MapPoint> list = (from p in GetAllMapPoints()
                where p.PointType == MapPointType.Unassigned
                select p).ToList();
            list.StableShuffle(_rng);
            foreach (MapPoint item in list)
            {
                if (pointTypesToBeAssigned.Count == 0)
                {
                    break;
                }

                item.PointType = GetNextValidPointType(pointTypesToBeAssigned, item);
            }
        }
    }

    private MapPointType GetNextValidPointType(Queue<MapPointType> pointTypesQueue, MapPoint mapPoint)
    {
        // The bound is re-evaluated on every iteration in the source; Queue is mutated inside the loop.
        for (int i = 0; i < pointTypesQueue.Count; i++)
        {
            MapPointType mapPointType = pointTypesQueue.Dequeue();
            if (_pointTypeCounts.ShouldIgnoreMapPointRulesForMapPointType(mapPointType))
            {
                return mapPointType;
            }

            if (IsValidPointType(mapPointType, mapPoint))
            {
                return mapPointType;
            }

            pointTypesQueue.Enqueue(mapPointType);
        }

        return MapPointType.Unassigned;
    }

    public bool IsValidPointType(MapPointType pointType, MapPoint mapPoint)
    {
        if (!IsValidForUpper(pointType, mapPoint))
        {
            return false;
        }

        if (!IsValidForLower(pointType, mapPoint))
        {
            return false;
        }

        if (!IsValidWithParents(pointType, mapPoint))
        {
            return false;
        }

        if (!IsValidWithChildren(pointType, mapPoint))
        {
            return false;
        }

        if (!IsValidWithSiblings(pointType, mapPoint))
        {
            return false;
        }

        return true;
    }

    /// These point types cannot be in the lower part of the map
    private static bool IsValidForLower(MapPointType pointType, MapPoint mapPoint)
    {
        if (mapPoint.coord.row < 6)
        {
            return !_lowerMapPointRestrictions.Contains(pointType);
        }

        return true;
    }

    /// These point types cannot be in the upper part of the map
    private bool IsValidForUpper(MapPointType pointType, MapPoint mapPoint)
    {
        if (mapPoint.coord.row >= _mapLength - 3)
        {
            return !_upperMapPointRestrictions.Contains(pointType);
        }

        return true;
    }

    /// These cannot be 2 times in a row.
    private static bool IsValidWithParents(MapPointType pointType, MapPoint mapPoint)
    {
        if (_parentMapPointRestrictions.Contains(pointType))
        {
            return !mapPoint.parents.Concat(mapPoint.Children).Any((MapPoint p) => pointType == p.PointType);
        }

        return true;
    }

    /// Checks if a point type is valid by ensuring it doesn't appear twice in a row among children
    private static bool IsValidWithChildren(MapPointType pointType, MapPoint mapPoint)
    {
        if (_childMapPointRestrictions.Contains(pointType))
        {
            return !mapPoint.Children.Any((MapPoint p) => pointType == p.PointType);
        }

        return true;
    }

    /// These point types cannot be siblings of each other.
    private static bool IsValidWithSiblings(MapPointType pointType, MapPoint mapPoint)
    {
        if (_siblingPointTypeRestrictions.Contains(pointType))
        {
            return !GetSiblings(mapPoint).Any((MapPoint p) => pointType == p.PointType);
        }

        return true;
    }

    private static IEnumerable<MapPoint> GetSiblings(MapPoint mapPoint)
    {
        // Source spells the lambda parameter `x` (shadowing the query range variable, which is not in
        // scope in the source expression). Renamed for clarity; `object.Equals` is kept verbatim so the
        // comparison stays reference-based (MapPoint only declares a private Equals(MapPoint)).
        return from x in mapPoint.parents.SelectMany((MapPoint parent) => parent.Children)
            where !object.Equals(x, mapPoint)
            select x;
    }
}
