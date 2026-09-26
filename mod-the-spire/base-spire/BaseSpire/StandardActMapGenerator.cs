using CoreTheSpire;

namespace BaseSpire;

/// <summary>Source-faithful port of the single-player StandardActMap pipeline.</summary>
public static class BaseSpireMapGenerator
{
    private sealed record PointTypeCounts(int Unknowns, int Rests, int Elites = 5, int Shops = 3);

    public static RunDefinition CreateStandardRun() => CreateStandardRun(null);

    public static RunDefinition CreateStandardRun(Func<SeededRandom, IReadOnlySet<RunRoomType>, RunRoomType>? resolveUnknownRoom)
    {
        return new RunDefinition(BaseSpireIds.StandardRun,
        [
            new ActDefinition(BaseSpireIds.OvergrowthAct, GenerateOvergrowth, resolveUnknownRoom),
            new ActDefinition(BaseSpireIds.HiveAct, GenerateHive, resolveUnknownRoom),
            new ActDefinition(BaseSpireIds.GloryAct, GenerateGlory, resolveUnknownRoom)
        ], resolveUnknownRoom is null ? new UnknownRoomOddsDefinition(
        [
            new UnknownRoomOddsEntry(RunRoomType.Monster, 0.1f),
            new UnknownRoomOddsEntry(RunRoomType.Elite, -1f),
            new UnknownRoomOddsEntry(RunRoomType.Treasure, 0.02f),
            new UnknownRoomOddsEntry(RunRoomType.Shop, 0.03f)
        ]) : null);
    }

    public static ActDefinition CreateUnderdocksAct(
        Func<SeededRandom, IReadOnlySet<RunRoomType>, RunRoomType>? resolveUnknownRoom = null)
    {
        return new ActDefinition(BaseSpireIds.UnderdocksAct, GenerateUnderdocks, resolveUnknownRoom);
    }

    public static ActMapDefinition GenerateOvergrowth(SeededRandom random)
    {
        var rests = random.NextGaussianInt(7, 1, 6, 7);
        var unknowns = StandardRandomUnknownCount(random);
        return Generate(random, 15, rests, unknowns);
    }

    public static ActMapDefinition GenerateHive(SeededRandom random)
    {
        var rests = random.NextGaussianInt(6, 1, 6, 7);
        var unknowns = StandardRandomUnknownCount(random) - 1;
        return Generate(random, 14, rests, unknowns);
    }

    public static ActMapDefinition GenerateGlory(SeededRandom random)
    {
        var rests = random.NextInt(5, 7);
        var unknowns = StandardRandomUnknownCount(random) - 1;
        return Generate(random, 13, rests, unknowns);
    }

    public static ActMapDefinition GenerateUnderdocks(SeededRandom random)
    {
        var rests = random.NextGaussianInt(7, 1, 6, 7);
        var unknowns = StandardRandomUnknownCount(random);
        return Generate(random, 15, rests, unknowns);
    }

    private static int StandardRandomUnknownCount(SeededRandom random) => random.NextGaussianInt(12, 1, 10, 14);

    private static ActMapDefinition Generate(SeededRandom random, int numberOfRooms, int restCount, int unknownCount)
    {
        var generator = new Generator(random, numberOfRooms, new PointTypeCounts(unknownCount, restCount));
        return generator.Generate();
    }

    private sealed class Generator(SeededRandom rng, int mapLength, PointTypeCounts counts)
    {
        private const int Width = 7;
        private readonly Node?[,] _grid = new Node[Width, mapLength + 1];
        private readonly HashSet<Node> _startPoints = [];
        private readonly Node _start = new(3, 0, MapPointType.Ancient);
        private readonly Node _boss = new(3, mapLength + 1, MapPointType.Boss);

        private static readonly HashSet<MapPointType> LowerRestrictions = [MapPointType.RestSite, MapPointType.Elite];
        private static readonly HashSet<MapPointType> UpperRestrictions = [MapPointType.RestSite];
        private static readonly HashSet<MapPointType> ParentRestrictions =
            [MapPointType.Elite, MapPointType.RestSite, MapPointType.Treasure, MapPointType.Shop];
        private static readonly HashSet<MapPointType> ChildRestrictions =
            [MapPointType.Elite, MapPointType.RestSite, MapPointType.Treasure, MapPointType.Shop];
        private static readonly HashSet<MapPointType> SiblingRestrictions =
            [MapPointType.RestSite, MapPointType.Monster, MapPointType.Unknown, MapPointType.Elite, MapPointType.Shop];

        public ActMapDefinition Generate()
        {
            GeneratePaths();
            AssignPointTypes();
            PruneAndRepair();
            CenterGrid();
            SpreadAdjacentMapPoints();
            StraightenPaths();

            var points = new List<Node> { _start };
            points.AddRange(GetAllMapPoints());
            points.Add(_boss);
            return new ActMapDefinition(Id(_start), Id(_boss), points.Select(point => new MapPointDefinition(
                Id(point), point.Row, point.Column, point.Type, point.Children.Select(Id).ToArray())));
        }

        private void GeneratePaths()
        {
            for (var i = 0; i < Width; i++)
            {
                var point = GetOrCreate(rng.NextInt(0, Width), 1);
                if (i == 1)
                    while (_startPoints.Contains(point)) point = GetOrCreate(rng.NextInt(0, Width), 1);
                _startPoints.Add(point);
                PathGenerate(point);
            }
            foreach (var point in GetRow(mapLength)) point.AddChild(_boss);
            foreach (var point in GetRow(1)) _start.AddChild(point);
        }

        private void PathGenerate(Node beginning)
        {
            var point = beginning;
            while (point.Row < mapLength)
            {
                var next = GenerateNextCoord(point);
                var child = GetOrCreate(next.Column, next.Row);
                point.AddChild(child);
                point = child;
            }
        }

        private (int Column, int Row) GenerateNextCoord(Node current)
        {
            var options = new List<int> { -1, 0, 1 };
            rng.StableShuffle(options);
            foreach (var delta in options)
            {
                var targetColumn = delta switch
                {
                    -1 => Math.Max(0, current.Column - 1),
                    0 => current.Column,
                    1 => Math.Min(current.Column + 1, Width - 1),
                    _ => throw new InvalidOperationException()
                };
                if (!HasInvalidCrossover(current, targetColumn)) return (targetColumn, current.Row + 1);
            }
            throw new InvalidOperationException($"Cannot find next node: seed={rng.Counter}");
        }

        private bool HasInvalidCrossover(Node current, int targetColumn)
        {
            var delta = targetColumn - current.Column;
            if (delta == 0 || delta == Width) return false;
            var sameRow = _grid[targetColumn, current.Row];
            return sameRow?.Children.Any(child => child.Column - sameRow.Column == -delta) == true;
        }

        private void AssignPointTypes()
        {
            foreach (var point in GetRow(mapLength))
            {
                point.Type = MapPointType.RestSite;
                point.CanBeModified = false;
            }
            foreach (var point in GetRow(mapLength - 6))
            {
                point.Type = MapPointType.Treasure;
                point.CanBeModified = false;
            }
            foreach (var point in GetRow(1))
            {
                point.Type = MapPointType.Monster;
                point.CanBeModified = false;
            }

            var queue = new Queue<MapPointType>();
            for (var i = 0; i < counts.Rests; i++) queue.Enqueue(MapPointType.RestSite);
            for (var i = 0; i < counts.Shops; i++) queue.Enqueue(MapPointType.Shop);
            for (var i = 0; i < counts.Elites; i++) queue.Enqueue(MapPointType.Elite);
            for (var i = 0; i < counts.Unknowns; i++) queue.Enqueue(MapPointType.Unknown);
            AssignRemainingTypes(queue);
            foreach (var point in GetAllMapPoints().Where(point => point.Type == MapPointType.Unassigned))
                point.Type = MapPointType.Monster;
        }

        private void AssignRemainingTypes(Queue<MapPointType> queue)
        {
            for (var attempt = 0; attempt < 3 && queue.Count > 0; attempt++)
            {
                var unassigned = GetAllMapPoints().Where(point => point.Type == MapPointType.Unassigned).ToList();
                rng.StableShuffle(unassigned);
                foreach (var point in unassigned)
                {
                    if (queue.Count == 0) break;
                    point.Type = GetNextValidPointType(queue, point);
                }
            }
        }

        private MapPointType GetNextValidPointType(Queue<MapPointType> queue, Node point)
        {
            for (var i = 0; i < queue.Count; i++)
            {
                var type = queue.Dequeue();
                if (IsValidPointType(type, point)) return type;
                queue.Enqueue(type);
            }
            return MapPointType.Unassigned;
        }

        private bool IsValidPointType(MapPointType type, Node point)
        {
            if (point.Row < 6 && LowerRestrictions.Contains(type)) return false;
            if (point.Row >= mapLength - 2 && UpperRestrictions.Contains(type)) return false;
            if (ParentRestrictions.Contains(type) && point.Parents.Concat(point.Children).Any(node => node.Type == type)) return false;
            if (ChildRestrictions.Contains(type) && point.Children.Any(node => node.Type == type)) return false;
            if (SiblingRestrictions.Contains(type) && GetSiblings(point).Any(node => node.Type == type)) return false;
            return true;
        }

        private IEnumerable<Node> GetSiblings(Node point) => point.Parents.SelectMany(parent => parent.Children).Where(node => node != point);

        private void PruneAndRepair()
        {
            for (var i = 0; i < 3; i++)
            {
                PruneDuplicateSegments();
                if (!RepairPrunedTypes()) break;
            }
        }

        private bool RepairPrunedTypes()
        {
            var changed = RepairPointType(MapPointType.Shop, counts.Shops);
            changed |= RepairPointType(MapPointType.Elite, counts.Elites);
            changed |= RepairPointType(MapPointType.RestSite, counts.Rests);
            changed |= RepairPointType(MapPointType.Unknown, counts.Unknowns);
            return changed;
        }

        private bool RepairPointType(MapPointType type, int target)
        {
            var missing = target - GetAllMapPoints().Count(point => point.Type == type);
            if (missing <= 0) return false;
            var candidates = GetAllMapPoints().Where(point => point.Type == MapPointType.Monster && point.CanBeModified).ToList();
            rng.StableShuffle(candidates);
            var changed = false;
            foreach (var point in candidates)
            {
                if (missing == 0) break;
                if (!IsValidPointType(type, point)) continue;
                point.Type = type;
                missing--;
                changed = true;
            }
            return changed;
        }

        private void PruneDuplicateSegments()
        {
            var iterations = 0;
            var matching = FindMatchingSegments();
            while (PrunePaths(matching))
            {
                if (++iterations > 50) throw new InvalidOperationException("Unable to prune matching map segments in 51 iterations.");
                matching = FindMatchingSegments();
            }
        }

        private List<List<Node[]>> FindMatchingSegments()
        {
            var segments = new SortedDictionary<string, List<Node[]>>(StringComparer.Ordinal);
            foreach (var path in FindAllPaths(_start))
            for (var i = 0; i < path.Count - 1; i++)
            {
                if (path[i].Children.Count <= 1 && path[i].Row != 0) continue;
                for (var length = 2; length < path.Count - i; length++)
                {
                    var end = path[i + length];
                    if (end.Parents.Count < 2) continue;
                    var segment = path.Skip(i).Take(length + 1).ToArray();
                    var key = SegmentKey(segment);
                    if (!segments.TryGetValue(key, out var matches)) segments.Add(key, matches = []);
                    else if (matches.Any(existing => Overlapping(existing, segment))) continue;
                    matches.Add(segment);
                }
            }
            return segments.Values.Where(matches => matches.Count > 1).ToList();
        }

        private static List<List<Node>> FindAllPaths(Node point)
        {
            if (point.Type == MapPointType.Boss) return [[point]];
            var paths = new List<List<Node>>();
            foreach (var child in point.Children)
            foreach (var suffix in FindAllPaths(child))
                paths.Add([point, .. suffix]);
            return paths;
        }

        private static string SegmentKey(IReadOnlyList<Node> segment)
        {
            var first = segment[0];
            var last = segment[^1];
            var prefix = first.Row == 0 ? $"{first.Row}-{last.Column},{last.Row}-" :
                $"{first.Column},{first.Row}-{last.Column},{last.Row}-";
            return prefix + string.Join(',', segment.Select(node => (int)node.Type));
        }

        private static bool Overlapping(IReadOnlyList<Node> left, IReadOnlyList<Node> right)
        {
            if (left.Count < 3 || right.Count < 3) return false;
            for (var i = 1; i <= left.Count - 2; i++)
                if (left[i] == right[i]) return true;
            return false;
        }

        private bool PrunePaths(List<List<Node[]>> matching)
        {
            foreach (var matches in matching)
            {
                rng.Shuffle(matches);
                var pruned = 0;
                foreach (var segment in matches)
                {
                    if (pruned == matches.Count - 1) break;
                    if (PruneSegment(segment)) pruned++;
                }
                if (pruned > 0) return true;
                foreach (var segment in matches)
                for (var i = 0; i < segment.Length - 1; i++)
                {
                    var parent = segment[i];
                    var child = segment[i + 1];
                    if (parent.Children.Count >= 2 && child.Parents.Count != 1)
                    {
                        parent.RemoveChild(child);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool PruneSegment(Node[] segment)
        {
            var changed = false;
            for (var i = 0; i < segment.Length - 1; i++)
            {
                var point = segment[i];
                if (!IsInGrid(point)) return true;
                if (point.Children.Count > 1 || point.Parents.Count > 1 || point.Parents.Any(parent => parent.Children.Count == 1 && IsInGrid(parent)))
                    continue;
                if (segment.Skip(i).Any(node => node.Children.Count > 1 && node.Parents.Count == 1)) continue;
                if (segment[^1].Parents.Count == 1) return false;
                if (point.Children.Where(child => !segment.Contains(child)).Any(child => child.Parents.Count == 1)) continue;
                RemovePoint(point);
                changed = true;
            }
            return changed;
        }

        private void RemovePoint(Node point)
        {
            _grid[point.Column, point.Row] = null;
            _startPoints.Remove(point);
            foreach (var child in point.Children.ToArray()) point.RemoveChild(child);
            foreach (var parent in point.Parents.ToArray()) parent.RemoveChild(point);
        }

        private bool IsInGrid(Node point) => _grid[point.Column, point.Row] != null || point.Type is MapPointType.Ancient or MapPointType.Boss;

        private void CenterGrid()
        {
            var leftEmpty = ColumnEmpty(0) && ColumnEmpty(1);
            var rightEmpty = ColumnEmpty(Width - 1) && ColumnEmpty(Width - 2);
            var shift = leftEmpty && !rightEmpty ? -1 : !leftEmpty && rightEmpty ? 1 : 0;
            if (shift == 0) return;
            for (var row = 0; row < _grid.GetLength(1); row++)
            {
                var columns = shift > 0 ? Enumerable.Range(0, Width).Reverse() : Enumerable.Range(0, Width);
                foreach (var column in columns)
                {
                    var point = _grid[column, row];
                    _grid[column, row] = null;
                    var target = column + shift;
                    if (target < 0 || target >= Width || point == null) continue;
                    _grid[target, row] = point;
                    point.Column = target;
                }
            }
        }

        private bool ColumnEmpty(int column)
        {
            for (var row = 0; row < _grid.GetLength(1); row++)
                if (_grid[column, row] != null) return false;
            return true;
        }

        private void SpreadAdjacentMapPoints()
        {
            for (var row = 0; row < _grid.GetLength(1); row++)
            {
                var rowNodes = Enumerable.Range(0, Width).Select(column => _grid[column, row]).Where(point => point != null).Cast<Node>().ToList();
                bool moved;
                do
                {
                    moved = false;
                    foreach (var point in rowNodes)
                    {
                        var bestColumn = point.Column;
                        var bestGap = ComputeGap(point.Column, rowNodes, point);
                        foreach (var allowed in AllowedPositions(point))
                        {
                            if (allowed == point.Column || (_grid[allowed, row] != null && _grid[allowed, row] != point)) continue;
                            var gap = ComputeGap(allowed, rowNodes, point);
                            if (gap <= bestGap) continue;
                            bestColumn = allowed;
                            bestGap = gap;
                        }
                        if (bestColumn == point.Column) continue;
                        _grid[point.Column, row] = null;
                        _grid[bestColumn, row] = point;
                        point.Column = bestColumn;
                        moved = true;
                    }
                } while (moved);
            }
        }

        private IEnumerable<int> AllowedPositions(Node point)
        {
            var allowed = new HashSet<int>(Enumerable.Range(0, Width));
            foreach (var neighbor in point.Parents.Concat(point.Children))
                allowed.IntersectWith(Enumerable.Range(Math.Max(0, neighbor.Column - 1),
                    Math.Min(Width - 1, neighbor.Column + 1) - Math.Max(0, neighbor.Column - 1) + 1));
            return allowed;
        }

        private static int ComputeGap(int column, IReadOnlyList<Node> rowNodes, Node current)
        {
            var gap = int.MaxValue;
            foreach (var node in rowNodes)
                if (node != current) gap = Math.Min(gap, Math.Abs(column - node.Column));
            return gap;
        }

        private void StraightenPaths()
        {
            for (var row = 0; row < _grid.GetLength(1); row++)
            for (var column = 0; column < Width; column++)
            {
                var point = _grid[column, row];
                if (point == null || point.Parents.Count != 1 || point.Children.Count != 1) continue;
                var parent = point.Parents.First();
                var child = point.Children.First();
                var target = point.Column < child.Column && point.Column < parent.Column ? point.Column + 1 :
                    point.Column > child.Column && point.Column > parent.Column ? point.Column - 1 : point.Column;
                if (target < 0 || target >= Width || target == point.Column || _grid[target, row] != null) continue;
                _grid[point.Column, row] = null;
                _grid[target, row] = point;
                point.Column = target;
            }
        }

        private List<Node> GetAllMapPoints()
        {
            var points = new List<Node>();
            for (var column = 0; column < Width; column++)
            for (var row = 0; row < _grid.GetLength(1); row++)
                if (_grid[column, row] is { } point) points.Add(point);
            return points;
        }

        private List<Node> GetRow(int row)
        {
            var points = new List<Node>();
            for (var column = 0; column < Width; column++)
                if (_grid[column, row] is { } point) points.Add(point);
            return points;
        }

        private Node GetOrCreate(int column, int row) => _grid[column, row] ??= new Node(column, row, MapPointType.Unassigned);
        private static string Id(Node point) => $"map:{point.Column}:{point.Row}";

        private sealed class Node(int column, int row, MapPointType type) : IComparable<Node>
        {
            public int Column { get; set; } = column;
            public int Row { get; } = row;
            public MapPointType Type { get; set; } = type;
            public bool CanBeModified { get; set; } = true;
            public HashSet<Node> Parents { get; } = [];
            public HashSet<Node> Children { get; } = [];
            public int CompareTo(Node? other) => other is null ? 1 : (Column, Row).CompareTo((other.Column, other.Row));
            public void AddChild(Node child)
            {
                if (!Children.Add(child)) return;
                child.Parents.Add(this);
            }
            public void RemoveChild(Node child)
            {
                Children.Remove(child);
                child.Parents.Remove(this);
            }
        }
    }
}
