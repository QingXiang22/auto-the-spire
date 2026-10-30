using System.Globalization;
using System.IO.Hashing;
using System.Text;
using CoreTheSpire.Engine;

namespace Differential;

internal sealed record NormalRandomSeed(string Stream, string Seed, string[] InitialWords);
internal sealed record NormalRandomRoot(string Input, string Root, NormalRandomSeed[] Seeds);

internal static class NormalRandomRoots
{
    internal static ulong Hash(string value) => XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(value));
    internal static string Word(ulong value) => value.ToString("x16", CultureInfo.InvariantCulture);
    internal static NormalRandomRoot Dsl(string input, RandomSnapshot snapshot, NormalRandomStateStep[] states, string? encounter, long floor)
    {
        if (!snapshot.ExplicitSeeds.TryGetValue("base-spire:run:up_front", out var upFront)) throw Missing("missing actual explicit stream seeds");
        var root = unchecked(upFront - Hash("up_front"));
        var expectedRoot = input.StartsWith("old", StringComparison.Ordinal) ? (ulong)Binary32.Hash32(input[3..]) : Hash(input);
        if (root != expectedRoot) throw Missing("actual source root disagrees with input seed");
        var seeds = snapshot.ExplicitSeeds.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
        {
            var expected = Derive(root, p.Key, encounter, floor);
            if (p.Value != expected) throw Missing("actual explicit seed differs from its source derivation");
            var initial = Initial(p.Value); var first = states.FirstOrDefault(s => s.Stream == p.Key);
            if (first is not null && !first.BeforeWords.SequenceEqual(initial)) throw Missing("derived seed differs from actual initial stream state");
            return new NormalRandomSeed(p.Key, Word(p.Value), initial);
        }).ToArray();
        return Validate(new(input, Word(root), seeds), states);
    }
    internal static ulong Derive(ulong root, string stream, string? encounter, long floor)
    {
        if (stream.StartsWith("base-spire:run:", StringComparison.Ordinal) || stream.StartsWith("base-spire:player:", StringComparison.Ordinal))
            return unchecked(root + Hash(stream[(stream.LastIndexOf(':') + 1)..]));
        if (stream == "base-spire:event:neow") return unchecked(root + Hash("NEOW"));
        if (stream == "base-spire:encounter:monsters" && encounter is not null)
            return unchecked(root + (ulong)floor + Hash(encounter));
        throw Missing("unclassified source seed derivation: " + stream);
    }
    internal static NormalRandomRoot Validate(NormalRandomRoot value, NormalRandomStateStep[] states)
    {
        if (value.Seeds.Select(s => s.Stream).Distinct(StringComparer.Ordinal).Count() != value.Seeds.Length
            || !value.Seeds.Select(s => s.Stream).SequenceEqual(value.Seeds.Select(s => s.Stream).Order(StringComparer.Ordinal)))
            throw Missing("duplicate or unordered derived stream identity");
        var consumed = states.Select(s => s.Stream).Distinct(StringComparer.Ordinal).ToArray();
        if (consumed.Except(value.Seeds.Select(s => s.Stream), StringComparer.Ordinal).Any()) throw Missing("consumed generator has no actual seed evidence");
        foreach (var seed in value.Seeds)
        {
            if (!ulong.TryParse(seed.Seed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number) || seed.Seed.Length != 16
                || !Initial(number).SequenceEqual(seed.InitialWords)) throw Missing("invalid derived seed or initial words");
            var first = states.FirstOrDefault(s => s.Stream == seed.Stream);
            if (first is not null && (first.Before != 0 || !seed.InitialWords.SequenceEqual(first.BeforeWords)))
                throw Missing("seed evidence disagrees with actual first state");
        }
        return value;
    }
    // Only expand the constructor's seed, without calling or advancing a generator.
    internal static string[] Initial(ulong seed)
    {
        ulong Next()
        {
            var z = unchecked(seed += 0x9e3779b97f4a7c15UL);
            z = unchecked((z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94d049bb133111ebUL); return z ^ (z >> 31);
        }
        return [Word(Next()), Word(Next()), Word(Next()), Word(Next())];
    }
    internal static NotSupportedException Missing(string reason) => new("missing_dimension: " + reason);
}
