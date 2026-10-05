namespace CoreTheSpire.Engine;

/// <summary>One deterministic raw-word generator. Captured state must be independent of writable storage.</summary>
public interface IRandomGenerator
{
    ulong Next();
    IReadOnlyList<ulong> Capture();
    void Restore(IReadOnlyList<ulong> state);
}

/// <summary>A versioned generator/mapping pair supplied by the host or game package.</summary>
public interface IRandomProfile
{
    string AlgorithmId { get; }
    string DrawProfileId { get; }
    IRandomGenerator Create(ulong seed);
    long DrawBounded(Func<ulong> next, long minInclusive, long maxExclusive);
    double DrawDouble(Func<ulong> next);
    bool DrawBool(Func<ulong> next);
}

/// <summary>Reusable bit generator; game-specific seed layout remains in the content package.</summary>
public sealed class Xoshiro256StarStar : IRandomGenerator
{
    private ulong _s0, _s1, _s2, _s3;

    public Xoshiro256StarStar(ulong seed, bool protectZeroState = true)
    {
        _s0 = SplitMix64(ref seed); _s1 = SplitMix64(ref seed);
        _s2 = SplitMix64(ref seed); _s3 = SplitMix64(ref seed);
        if (protectZeroState && (_s0 | _s1 | _s2 | _s3) == 0UL) _s0 = 0x9E3779B97F4A7C15UL;
    }

    public ulong Next()
    {
        var result = RandomService.RotateLeft(_s1 * 5UL, 7) * 9UL;
        var temporary = _s1 << 17;
        _s2 ^= _s0; _s3 ^= _s1; _s1 ^= _s2; _s0 ^= _s3;
        _s2 ^= temporary; _s3 = RandomService.RotateLeft(_s3, 45);
        return result;
    }

    public IReadOnlyList<ulong> Capture() => Array.AsReadOnly(new[] { _s0, _s1, _s2, _s3 });

    public void Restore(IReadOnlyList<ulong> state)
    {
        if (state.Count != 4) throw Faults.Of(EngineFaultCode.SnapshotInvalid, "xoshiro state needs four words");
        (_s0, _s1, _s2, _s3) = (state[0], state[1], state[2], state[3]);
    }

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var value = state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}

internal sealed class BuiltInRandomProfile(bool doubleTruncation) : IRandomProfile
{
    public string AlgorithmId => doubleTruncation ? RandomService.VanillaAlgorithmId : RandomService.DefaultAlgorithmId;
    public string DrawProfileId => doubleTruncation ? RandomService.VanillaDrawProfileId : RandomService.DefaultDrawProfileId;
    public IRandomGenerator Create(ulong seed) => new Xoshiro256StarStar(seed, protectZeroState: !doubleTruncation);
    public double DrawDouble(Func<ulong> next) => (next() >> 11) * (1.0 / 9007199254740992.0);
    public bool DrawBool(Func<ulong> next) => doubleTruncation ? (next() >> 63) != 0 : (next() & 1UL) != 0;
    public long DrawBounded(Func<ulong> next, long minInclusive, long maxExclusive)
    {
        var span = maxExclusive - minInclusive;
        if (doubleTruncation) return minInclusive + (long)(DrawDouble(next) * span);
        var range = (ulong)span;
        var limit = (ulong.MaxValue / range) * range;
        ulong value;
        do { value = next(); } while (value >= limit);
        return minInclusive + (long)(value % range);
    }
}
