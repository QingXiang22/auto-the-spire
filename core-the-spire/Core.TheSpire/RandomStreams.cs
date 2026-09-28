using System.Numerics;

namespace CoreTheSpire;

/// <summary>Xoshiro256** seeded with Splitmix64, matching the PRNG family used by the recovered game source.</summary>
public sealed class SeededRandom
{
    private ulong _s0, _s1, _s2, _s3;
    public int Counter { get; private set; }

    public SeededRandom(ulong seed)
    {
        _s0 = Splitmix64(ref seed); _s1 = Splitmix64(ref seed); _s2 = Splitmix64(ref seed); _s3 = Splitmix64(ref seed);
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        Counter++;
        return (int)(NextDoubleCore() * maxExclusive);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        Counter++;
        return (int)(NextDoubleCore() * ((long)maxExclusive - minInclusive)) + minInclusive;
    }

    public double NextDouble()
    {
        Counter++;
        return NextDoubleCore();
    }

    public float NextFloat() => (float)NextDouble();

    public float NextFloat(float maxExclusive)
    {
        if (maxExclusive < 0f) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (float)(NextDouble() * maxExclusive);
    }

    public void Shuffle<T>(IList<T> values)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = NextInt(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    public int NextGaussianInt(int mean, int standardDeviation, int minimum, int maximum)
    {
        int value;
        do
        {
            var first = 1.0 - NextDouble();
            var second = 1.0 - NextDouble();
            var normal = Math.Sqrt(-2.0 * Math.Log(first)) * Math.Sin(Math.PI * 2.0 * second);
            value = (int)Math.Round(mean + standardDeviation * normal);
        } while (value < minimum || value > maximum);
        return value;
    }

    public void StableShuffle<T>(IList<T> values) where T : IComparable<T>
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        for (var i = 0; i < sorted.Length; i++) values[i] = sorted[i];
        Shuffle(values);
    }

    private double NextDoubleCore() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    private ulong NextUInt64()
    {
        var result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0; _s3 ^= _s1; _s1 ^= _s2; _s0 ^= _s3; _s2 ^= t; _s3 = BitOperations.RotateLeft(_s3, 45);
        return result;
    }

    private static ulong Splitmix64(ref ulong value)
    {
        var z = value += 11400714819323198485UL;
        z = (z ^ (z >> 30)) * 13787848793156543929UL;
        z = (z ^ (z >> 27)) * 10723151780598845931UL;
        return z ^ (z >> 31);
    }
}

public sealed class RandomStreams(uint seed)
{
    private readonly Dictionary<string, SeededRandom> _streams = new(StringComparer.Ordinal);
    public uint Seed { get; } = seed;

    public static RandomStreams FromStringSeed(string seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        return new RandomStreams(unchecked((uint)DeterministicHash(seed)));
    }

    public SeededRandom Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_streams.TryGetValue(name, out var stream))
        {
            var hash = unchecked((uint)DeterministicHash(name));
            stream = new SeededRandom(unchecked(Seed + hash));
            _streams.Add(name, stream);
        }
        return stream;
    }

    private static int DeterministicHash(string text)
    {
        var a = 352654597;
        var b = a;
        for (var i = 0; i < text.Length; i += 2)
        {
            a = unchecked(((a << 5) + a) ^ text[i]);
            if (i == text.Length - 1) break;
            b = unchecked(((b << 5) + b) ^ text[i + 1]);
        }
        return unchecked(a + b * 1566083941);
    }
}
