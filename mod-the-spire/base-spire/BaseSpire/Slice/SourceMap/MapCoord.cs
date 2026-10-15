using System;

namespace BaseSpire.Slice.SourceMap;

/// <summary>
/// Verbatim port of <c>MegaCrit.Sts2.Core.Map.MapCoord</c>.
///
/// <para>Deviations: the multiplayer packet serialization surface
/// (<c>IPacketSerializable</c>/<c>Serialize</c>/<c>Deserialize</c>) and the <c>[JsonInclude]</c>
/// attributes are dropped — the map generator never serializes a coordinate. The <c>[Serializable]</c>
/// attribute is kept. The source declares this as a primary-constructor struct with
/// <c>public int col = col;</c> field initializers; the port uses an equivalent explicit constructor and
/// plain fields (same fields, same values, same comparison/hash semantics) so that
/// <c>new MapCoord { col = …, row = … }</c> and <c>default(MapCoord)</c> are unambiguously valid.</para>
/// </summary>
[Serializable]
public struct MapCoord : IEquatable<MapCoord>, IComparable<MapCoord>
{
    public int col;

    public int row;

    public MapCoord(int col, int row)
    {
        this.col = col;
        this.row = row;
    }

    public static bool operator ==(MapCoord first, MapCoord second)
    {
        return first.Equals(second);
    }

    public static bool operator !=(MapCoord first, MapCoord second)
    {
        return !(first == second);
    }

    public bool Equals(MapCoord other)
    {
        if (col == other.col)
        {
            return row == other.row;
        }

        return false;
    }

    public override bool Equals(object? obj)
    {
        if (obj is MapCoord other)
        {
            return Equals(other);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return (col, row).GetHashCode();
    }

    public int CompareTo(MapCoord other)
    {
        return (col, row).CompareTo((other.col, other.row));
    }

    public override string ToString()
    {
        return $"MapCoord ({col}, {row})";
    }
}
