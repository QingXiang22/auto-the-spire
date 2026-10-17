using System;
using System.Collections.Generic;

namespace BaseSpire.Slice.SourceMap;

/// <summary>
/// One node of the final (post-pruning, post-processing) act map, in the generator's compact output form.
/// </summary>
/// <param name="Col">Column of the node <b>after</b> CenterGrid/SpreadAdjacentMapPoints/StraightenPaths.</param>
/// <param name="Row">
/// Row of the node. Grid rows are <c>0..RowCount-1</c>; row 0 only ever holds
/// <c>StartingMapPoint</c> (3, 0), the boss is at (3, RowCount) and the second boss at (3, RowCount+1).
/// </param>
/// <param name="Type"><see cref="MapPointType"/> as <c>int</c> (Unassigned=0 … Ancient=8).</param>
/// <param name="ParentColsMask">
/// Bit c set ⇔ a parent node sits at column c in row <c>Row - 1</c>. Every edge in a generated map
/// connects adjacent rows, so a column mask is a complete description of the in-edges.
/// </param>
/// <param name="ChildColsMask">Bit c set ⇔ a child node sits at column c in row <c>Row + 1</c>.</param>
public readonly record struct SourceActMapCell(int Col, int Row, int Type, int ParentColsMask, int ChildColsMask);

/// <summary>
/// Final act-map grid plus the word-supply accounting for one generation attempt.
/// </summary>
/// <param name="Completed">True when the full map was generated; false only from <c>TryGenerate</c>.</param>
/// <param name="WordsConsumed">
/// Number of payloads actually consumed (<c>SourceMapRandom.DrawsUsed</c>). On failure this is the
/// prefix consumed before exhaustion/abort, so a caller can resume exactly where generation stopped.
/// </param>
/// <param name="RowCount">Grid row count = <c>numberOfRooms + 1</c> (16 for act 1 single-player).</param>
/// <param name="ColumnCount">Grid column count (always 7).</param>
/// <param name="Cells">
/// Emitted column-major: for col 0..6 (outer), for row 0..<c>RowCount-1</c> (inner), null grid cells
/// skipped — plus <c>StartingMapPoint</c> at its real coordinate (3, 0) and, appended after the grid
/// cells, <c>BossMapPoint</c> (3, RowCount) and <c>SecondBossMapPoint</c> (3, RowCount+1) when present.
/// Empty on failure.
/// </param>
public sealed record SourceActMapResult(
    bool Completed,
    int WordsConsumed,
    int RowCount,
    int ColumnCount,
    IReadOnlyList<SourceActMapCell> Cells);

/// <summary>
/// Public entry point: runs the ported <see cref="StandardActMap"/> generation over a caller-supplied
/// word supply and flattens the final grid.
///
/// <para><b>Word-supply contract.</b> <c>words</c> starts at the first payload the MAP phase may consume.
/// The game draws the act's point-type counts from the map rng before the map phase
/// (<c>Overgrowth.GetMapPointTypes</c>: rest Gaussian, then unknown Gaussian — see README-port.md §A),
/// so a caller replaying a real run must either (a) advance past those draws first and pass the
/// remaining list, or (b) keep one list and use the <c>startIndex</c> overloads below with
/// <c>startIndex = rng.DrawsUsed</c> after computing the counts from the same supply.</para>
///
/// <para><b>Draw ledger.</b> <see cref="SourceActMapResult.WordsConsumed"/> counts only the map phase
/// (start-index based, not absolute).</para>
/// </summary>
public static class SourceActMapGenerator
{
    /// <summary>
    /// Generates the final map. Throws <see cref="SourceMapWordsExhaustedException"/> if the supply runs
    /// out, and <see cref="InvalidOperationException"/> if the source algorithm's own guard trips
    /// ("Cannot find next node" / "Unable to prune matching segments in N iterations"). Use
    /// <see cref="TryGenerate(IReadOnlyList{ulong}, int, MapPointTypeCounts, bool, bool, out SourceActMapResult, out int)"/>
    /// to observe those as <c>false</c> instead.
    /// </summary>
    /// <param name="words">Word supply; see the word-supply contract on this class.</param>
    /// <param name="numberOfRooms">
    /// Source: <c>actModel.GetNumberOfRooms(isMultiplayer)</c>; <c>_mapLength</c>/RowCount becomes
    /// <c>numberOfRooms + 1</c>. Act 1 (Overgrowth) single-player = 15.
    /// </param>
    /// <param name="pointTypeCounts">Source: <c>mapPointTypeCountsOverride ?? actModel.GetMapPointTypes(mapRng)</c>.</param>
    /// <param name="hasSecondBoss">Double Boss (A10+) adds a second boss after the first.</param>
    /// <param name="enablePruning">Source: must be true for gameplay logic; false is a test aid.</param>
    public static SourceActMapResult Generate(
        IReadOnlyList<ulong> words,
        int numberOfRooms,
        MapPointTypeCounts pointTypeCounts,
        bool hasSecondBoss = false,
        bool enablePruning = true)
    {
        return Generate(words, 0L, numberOfRooms, pointTypeCounts, hasSecondBoss, enablePruning);
    }

    /// <summary>
    /// Additive convenience overload (not in the task book's required API): same as
    /// <see cref="Generate(IReadOnlyList{ulong}, int, MapPointTypeCounts, bool, bool)"/> but starts the
    /// supply at <paramref name="startIndex"/>, so the caller can consume the act's count Gaussians from
    /// the same list first and pass <c>rng.DrawsUsed</c> here.
    /// </summary>
    public static SourceActMapResult Generate(
        IReadOnlyList<ulong> words,
        long startIndex,
        int numberOfRooms,
        MapPointTypeCounts pointTypeCounts,
        bool hasSecondBoss = false,
        bool enablePruning = true)
    {
        SourceMapRandom rng = new SourceMapRandom(words, startIndex);
        StandardActMap map = new StandardActMap(rng, numberOfRooms, pointTypeCounts, hasSecondBoss, enablePruning);
        return BuildResult(map, rng);
    }

    /// <summary>
    /// Exhaustion-tolerant entry point. Returns <c>false</c> (with <paramref name="wordsConsumed"/> =
    /// payloads actually consumed) when the supply ran out. It also returns <c>false</c> when the source
    /// algorithm itself throws <see cref="InvalidOperationException"/>: that is only reachable once the
    /// draw stream cannot produce a valid continuation ("Cannot find next node") or the prune loop fails
    /// to converge, both of which the task book classifies as "exhaustion prevented completion".
    /// </summary>
    public static bool TryGenerate(
        IReadOnlyList<ulong> words,
        int numberOfRooms,
        MapPointTypeCounts pointTypeCounts,
        bool hasSecondBoss,
        bool enablePruning,
        out SourceActMapResult result,
        out int wordsConsumed)
    {
        return TryGenerate(words, 0L, numberOfRooms, pointTypeCounts, hasSecondBoss, enablePruning, out result, out wordsConsumed);
    }

    /// <summary>
    /// Additive convenience overload (not in the task book's required API): <see cref="TryGenerate(IReadOnlyList{ulong}, int, MapPointTypeCounts, bool, bool, out SourceActMapResult, out int)"/>
    /// with an explicit supply offset.
    /// </summary>
    public static bool TryGenerate(
        IReadOnlyList<ulong> words,
        long startIndex,
        int numberOfRooms,
        MapPointTypeCounts pointTypeCounts,
        bool hasSecondBoss,
        bool enablePruning,
        out SourceActMapResult result,
        out int wordsConsumed)
    {
        SourceMapRandom rng = new SourceMapRandom(words, startIndex);
        try
        {
            StandardActMap map = new StandardActMap(rng, numberOfRooms, pointTypeCounts, hasSecondBoss, enablePruning);
            result = BuildResult(map, rng);
            wordsConsumed = result.WordsConsumed;
            return true;
        }
        catch (SourceMapWordsExhaustedException)
        {
            // fall through to the failure result below
        }
        catch (InvalidOperationException)
        {
            // fall through to the failure result below
        }

        wordsConsumed = rng.DrawsUsed;
        result = new SourceActMapResult(
            Completed: false,
            WordsConsumed: wordsConsumed,
            RowCount: numberOfRooms + 1,
            ColumnCount: 7,
            Cells: Array.Empty<SourceActMapCell>());
        return false;
    }

    private static SourceActMapResult BuildResult(StandardActMap map, SourceMapRandom rng)
    {
        List<SourceActMapCell> cells = new List<SourceActMapCell>();

        // Column-major over the real grid. GetPoint also resolves StartingMapPoint at (3, 0), which lives
        // outside the grid array; the boss/second-boss rows sit past RowCount and are appended below.
        for (int col = 0; col < map.GetColumnCount(); col++)
        {
            for (int row = 0; row < map.GetRowCount(); row++)
            {
                MapPoint? point = map.GetPoint(col, row);
                if (point == null)
                {
                    continue;
                }

                cells.Add(MakeCell(point));
            }
        }

        cells.Add(MakeCell(map.BossMapPoint));
        if (map.SecondBossMapPoint != null)
        {
            cells.Add(MakeCell(map.SecondBossMapPoint));
        }

        return new SourceActMapResult(
            Completed: true,
            WordsConsumed: rng.DrawsUsed,
            RowCount: map.GetRowCount(),
            ColumnCount: map.GetColumnCount(),
            Cells: cells);
    }

    private static SourceActMapCell MakeCell(MapPoint point)
    {
        // Bit masks over columns. Every generated edge joins adjacent rows, so OR-ing the columns of all
        // parents/children is exactly "a node at (c, Row-1) is a parent"/"at (c, Row+1) is a child".
        // HashSet enumeration order cannot affect an OR, so this projection is order-independent.
        int parentColsMask = 0;
        foreach (MapPoint parent in point.parents)
        {
            parentColsMask |= 1 << parent.coord.col;
        }

        int childColsMask = 0;
        foreach (MapPoint child in point.Children)
        {
            childColsMask |= 1 << child.coord.col;
        }

        return new SourceActMapCell(
            Col: point.coord.col,
            Row: point.coord.row,
            Type: (int)point.PointType,
            ParentColsMask: parentColsMask,
            ChildColsMask: childColsMask);
    }
}
