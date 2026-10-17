using System;
using System.Collections.Generic;
using System.Linq;

namespace BaseSpire.Slice.SourceMap;

/// <summary>
/// Verbatim port of <c>MegaCrit.Sts2.Core.Map.MapPointTypeCounts</c>, minus the ascension lookup.
///
/// <para><b>Deviation (only one):</b> the source's <c>NumOfElites</c> initializer is
/// <c>(int)Math.Round(5f * (AscensionHelper.HasAscension(AscensionLevel.SwarmingElites) ? 1.6f : 1f))</c>,
/// which needs the live run's ascension level. The port hardcodes the A0 (no SwarmingElites) value
/// <c>5</c> and keeps the property <c>init</c>-settable so a caller that models ascensions can pass
/// <c>8</c> (the SwarmingElites value: <c>Math.Round(5f * 1.6f)</c>).</para>
///
/// <para>Constructor parameter order is the source's: <c>(int unknownCount, int restCount)</c>.</para>
/// </summary>
public class MapPointTypeCounts
{
    public HashSet<MapPointType> PointTypesThatIgnoreRules { get; init; } = new HashSet<MapPointType>();

    /// <summary>
    /// Source: <c>(int)Math.Round(5f * (AscensionHelper.HasAscension(AscensionLevel.SwarmingElites) ? 1.6f : 1f))</c>.
    /// Hardcoded to the ascension-free value 5 (see class remarks).
    /// </summary>
    public int NumOfElites { get; init; } = 5;

    public int NumOfShops { get; } = 3;

    public int NumOfUnknowns { get; }

    public int NumOfRests { get; }

    /// <summary>
    /// Whether the map generation MapPointType assignment ignores the rules or follows the rules.
    /// </summary>
    public bool ShouldIgnoreMapPointRulesForMapPointType(MapPointType pointType)
    {
        return PointTypesThatIgnoreRules.Contains(pointType);
    }

    /// <summary>
    /// Source signature takes <c>Rng rng</c>; retargeted at <see cref="SourceMapRandom"/>.
    /// Consumes a variable number of payloads (two per Box-Muller iteration) — see README-port.md §A.
    /// </summary>
    public static int StandardRandomUnknownCount(SourceMapRandom rng)
    {
        return rng.NextGaussianInt(12, 1, 10, 14);
    }

    public MapPointTypeCounts(int unknownCount, int restCount)
    {
        NumOfUnknowns = unknownCount;
        NumOfRests = restCount;
    }

    public MapPointTypeCounts(ActMap existingMap)
    {
        MapPoint[] source = existingMap.GetAllMapPoints().ToArray();
        NumOfUnknowns = source.Count((MapPoint p) => p.PointType == MapPointType.Unknown);
        NumOfRests = source.Count((MapPoint p) => p.PointType == MapPointType.RestSite);
    }
}
