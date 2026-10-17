using System;
using System.Collections.Generic;

namespace BaseSpire.Slice.SourceMap;

/// <summary>
/// Thrown when the word supply is exhausted: a random draw was requested past the end of the supplied
/// payload list. The algorithm must never silently continue with a zero word.
/// </summary>
public sealed class SourceMapWordsExhaustedException : Exception
{
    public SourceMapWordsExhaustedException()
        : base("The map word supply is exhausted (a draw was requested past the end of the payload list).")
    {
    }

    public SourceMapWordsExhaustedException(string message)
        : base(message)
    {
    }

    public SourceMapWordsExhaustedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The ONLY randomness source for the ported act-map generator. It replays a caller-supplied list of
/// 53-bit payloads instead of running the game's xoshiro256** generator.
///
/// <para><b>Word contract (bit-exact with the game's <c>MegaRandom</c>):</b></para>
/// <list type="bullet">
/// <item>Each element of <c>payloads</c> is <c>sourceRaw &gt;&gt; 11</c> of one 64-bit generator word,
/// i.e. a value in <c>[0, 2^53)</c>. Callers obtain it with the source-equivalent draw
/// <c>SampleInt(stream, 0, 2^53)</c>.</item>
/// <item><see cref="NextDouble"/> is exactly <c>payload * 2^-53</c> (<c>1.1102230246251565E-16</c>),
/// which is the game's <c>(double)(raw &gt;&gt; 11) * 1.1102230246251565E-16</c>. The payload must NOT be
/// shifted right by 11 again.</item>
/// <item><see cref="NextFloat"/> is <c>(float)(payload &gt;&gt; 29) * 5.9604645E-08f</c>, which recovers the
/// game's <c>(float)(raw &gt;&gt; 40) * 5.9604645E-08f</c>.</item>
/// <item>The Gaussian methods are the game's Box-Muller loops and consume two payloads per iteration.</item>
/// </list>
/// <para>No full 64-bit word is available or needed: the ported map algorithm only uses
/// <c>NextInt</c>/<c>NextDouble</c>/<c>NextFloat</c>/<c>NextGaussianInt</c>/<c>NextGaussianDouble</c>/<c>NextItem</c>.</para>
///
/// <para>One payload is consumed per <see cref="NextDouble"/>/<see cref="NextFloat"/>/<c>NextInt</c> draw,
/// in exactly the order the source algorithm requests them (see README-port.md).</para>
/// </summary>
public sealed class SourceMapRandom
{
    /// <summary>2^-53, the exact scale factor of the game's <c>NextDouble</c>.</summary>
    private const double IncrementDouble = 1.1102230246251565E-16;

    /// <summary>2^-24, the exact scale factor of the game's <c>NextFloat</c>.</summary>
    private const float IncrementFloat = 5.9604645E-08f;

    private readonly IReadOnlyList<ulong> _payloads;
    private readonly long _startIndex;
    private long _nextIndex;
    private int _drawsUsed;

    /// <param name="payloads">The 53-bit payload supply, each element in <c>[0, 2^53)</c>.</param>
    /// <param name="startIndex">Index of the first payload this instance may consume (default 0).</param>
    public SourceMapRandom(IReadOnlyList<ulong> payloads, long startIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(payloads);
        if (startIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, "startIndex must not be negative.");
        }

        _payloads = payloads;
        _startIndex = startIndex;
        _nextIndex = startIndex;
    }

    /// <summary>Number of payloads consumed by this instance since construction (excludes <c>startIndex</c>).</summary>
    public int DrawsUsed => _drawsUsed;

    /// <summary>True when no payload is left; the next draw will throw <see cref="SourceMapWordsExhaustedException"/>.</summary>
    public bool Exhausted => _nextIndex >= _payloads.Count;

    /// <summary>Number of payloads available from the start index onwards.</summary>
    public int PayloadsAvailable => (int)Math.Max(0L, _payloads.Count - _startIndex);

    /// <summary>
    /// Consumes exactly one payload. Throws <see cref="SourceMapWordsExhaustedException"/> instead of
    /// returning zero when the supply is empty.
    /// </summary>
    private ulong NextPayload()
    {
        if (_nextIndex >= _payloads.Count)
        {
            throw new SourceMapWordsExhaustedException(
                $"Word supply exhausted: requested payload #{_drawsUsed} at supply index {_nextIndex}, " +
                $"but the supply holds {_payloads.Count} payload(s) (startIndex = {_startIndex}).");
        }

        ulong payload = _payloads[(int)_nextIndex];
        _nextIndex++;
        _drawsUsed++;
        return payload;
    }

    /// <summary>
    /// Game equivalent: <c>MegaRandom.NextDouble()</c> = <c>(double)(raw &gt;&gt; 11) * 2^-53</c>.
    /// Consumes one payload, which already carries the <c>&gt;&gt; 11</c> shift.
    /// </summary>
    public double NextDouble() => (double)NextPayload() * IncrementDouble;

    /// <summary>
    /// Game equivalent: <c>MegaRandom.NextFloat()</c> = <c>(float)(raw &gt;&gt; 40) * 2^-24</c>.
    /// Consumes one payload; <c>payload &gt;&gt; 29</c> recovers the game's <c>raw &gt;&gt; 40</c>.
    /// </summary>
    public float NextFloat() => (float)(NextPayload() >> 29) * IncrementFloat;

    /// <summary>
    /// Game equivalent: <c>Rng.NextInt(maxExclusive)</c> → <c>MegaRandom.Next(maxExclusive)</c>
    /// → <c>(int)(NextDouble() * maxExclusive)</c>. Consumes one payload.
    /// </summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive < 1)
        {
            // MegaRandom.Next(int) throws for maxValue < 1; mirrored so the port fails identically.
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "maxExclusive must be > 0");
        }

        return (int)(NextDouble() * maxExclusive);
    }

    /// <summary>
    /// Game equivalent: <c>Rng.NextInt(minInclusive, maxExclusive)</c> → <c>MegaRandom.Next(min, max)</c>
    /// → <c>NextInner(max - min) + min</c>. Consumes one payload.
    /// </summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusive), "Minimum must be lower than maximum.");
        }

        // Mirrors MegaRandom.Next(int minValue, int maxValue) exactly, including its long-range branch:
        //   long num = (long)maxValue - (long)minValue;
        //   if (num <= int.MaxValue) return NextInner((int)num) + minValue;
        //   return (int)(NextInner(num) + minValue);
        // where NextInner(n) = (int)(NextDouble() * n) for an int argument and
        // (long)(NextDouble() * n) for a long argument. The map algorithm only ever passes small
        // ranges (NextInt(0, 7), NextInt(i + 1)), so both branches return identical values here.
        long range = (long)maxExclusive - (long)minInclusive;
        if (range <= int.MaxValue)
        {
            return (int)(NextDouble() * (double)(int)range) + minInclusive;
        }

        return (int)((long)(NextDouble() * (double)range) + minInclusive);
    }

    /// <summary>
    /// Game equivalent: <c>Rng.NextGaussianInt(mean, stdDev, min, max)</c> — verbatim Box-Muller loop
    /// (sine branch), two payloads per iteration, retried while the rounded value is out of range.
    /// No mean/range pre-checks exist in the source, so none are added here.
    /// </summary>
    public int NextGaussianInt(int mean, int stdDev, int min, int max)
    {
        int num3;
        do
        {
            double d = 1.0 - NextDouble();
            double num = 1.0 - NextDouble();
            double num2 = Math.Sqrt(-2.0 * Math.Log(d)) * Math.Sin(Math.PI * 2.0 * num);
            double a = (double)mean + (double)stdDev * num2;
            num3 = (int)Math.Round(a);
        }
        while (num3 < min || num3 > max);

        return num3;
    }

    /// <summary>
    /// Game equivalent: <c>Rng.NextGaussianDouble(mean, stdDev, min, max)</c> — verbatim Box-Muller loop
    /// (cosine branch), two payloads per iteration, retried while the raw value is outside [0, 1].
    /// No mean pre-check exists in the source, so none is added here.
    /// </summary>
    public double NextGaussianDouble(double mean = 0.0, double stdDev = 1.0, double min = 0.0, double max = 1.0)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum must not be higher than maximum.");
        }

        double num4;
        do
        {
            double d = 1.0 - NextDouble();
            double num = 1.0 - NextDouble();
            double num2 = Math.Sqrt(-2.0 * Math.Log(d));
            double d2 = Math.PI * 2.0 * num;
            double num3 = num2 * Math.Cos(d2);
            num4 = mean + num3 * stdDev;
        }
        while (num4 < 0.0 || num4 > 1.0);

        return num4 * (max - min) + min;
    }

    /// <summary>
    /// Game equivalent: <c>Rng.NextItem(items)</c> for an already materialized list:
    /// <c>items[NextInt(0, items.Count)]</c>. Consumes one payload.
    /// </summary>
    public T NextItem<T>(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items[NextInt(0, items.Count)];
    }
}
