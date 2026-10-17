namespace BaseSpire.Slice.SourceMap;

/// <summary>
/// Verbatim port of <c>MegaCrit.Sts2.Core.Map.MapPointType</c>. The numeric values are part of the
/// output contract (<c>SourceActMapCell.Type</c>): Unassigned = 0, Unknown = 1, Shop = 2,
/// Treasure = 3, RestSite = 4, Monster = 5, Elite = 6, Boss = 7, Ancient = 8.
/// </summary>
public enum MapPointType
{
    Unassigned,
    Unknown,
    Shop,
    Treasure,
    RestSite,
    Monster,
    Elite,
    Boss,
    Ancient,
}
