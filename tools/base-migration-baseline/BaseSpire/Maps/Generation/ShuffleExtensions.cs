using System.Collections.Generic;
using System.Linq;

namespace BaseSpire.Slice.SourceMap;

/// <summary>
/// Verbatim port of <c>MegaCrit.Sts2.Core.Extensions.ListExtensions.StableShuffle/UnstableShuffle</c>,
/// retargeted at <see cref="SourceMapRandom"/>. Deliberately in the <c>BaseSpire.Slice.SourceMap</c>
/// namespace so the ported map files compile without extra usings.
/// </summary>
public static class ShuffleExtensions
{
    /// <summary>
    /// Source: <c>ListExtensions.StableShuffle(this List&lt;T&gt; list, Rng rng)</c>.
    /// Sorts a copy of the list with <see cref="Comparer{T}.Default"/> (the source calls
    /// <c>list.ToList().Sort()</c>, which is exactly <see cref="Comparer{T}.Default"/>), writes it back
    /// into the caller's list, then runs <see cref="UnstableShuffle{T}"/>. Draw order is unchanged.
    /// </summary>
    public static List<T> StableShuffle<T>(this List<T> list, SourceMapRandom rng) where T : IComparable<T>
    {
        List<T> sorted = list.ToList();
        sorted.Sort(Comparer<T>.Default);
        for (int i = 0; i < list.Count; i++)
        {
            list[i] = sorted[i];
        }

        return list.UnstableShuffle(rng);
    }

    /// <summary>
    /// Source: <c>ListExtensions.UnstableShuffle(this List&lt;T&gt; list, Rng rng)</c> — descending
    /// Fisher-Yates, one <c>NextInt(i + 1)</c> draw per step for <c>i = Count - 1 .. 1</c>.
    /// The source spells the same loop as <c>while (num &gt; 1) { num--; ... }</c>.
    /// </summary>
    public static List<T> UnstableShuffle<T>(this List<T> list, SourceMapRandom rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
