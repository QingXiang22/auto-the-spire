using CoreTheSpire.Engine;

namespace BaseSpire.Slice;

/// <summary>
/// Source-backed STS mapping policy, provided through the generic core profile seam.
/// MegaRandom.cs:98-104,222-225,268-287; stream seed layout is owned by VanillaRandomLayout.
/// </summary>
public sealed class VanillaRandomProfile : IRandomProfile
{
    public string AlgorithmId => VanillaRandomLayout.AlgorithmId;
    public string DrawProfileId => VanillaRandomLayout.DrawProfileId;

    public IRandomGenerator Create(ulong seed) => new Xoshiro256StarStar(seed, protectZeroState: false);
    public double DrawDouble(Func<ulong> next) => (next() >> 11) * (1.0 / 9007199254740992.0);
    public bool DrawBool(Func<ulong> next) => (next() & 0x8000000000000000UL) != 0;
    public long DrawBounded(Func<ulong> next, long minInclusive, long maxExclusive) =>
        minInclusive + (long)(DrawDouble(next) * (maxExclusive - minInclusive));
}
