using System.IO.Hashing;
using System.Text;
using BaseSpire.Slice;

namespace BaseSpire.SinglePlayer;

/// <summary>Versioned seed derivation. Sampling remains VanillaRandomProfile in both versions.</summary>
public sealed class RunRandomLayout
{
    public const string Frozen = "sts2-hash32@1";
    public const string V109 = "sts2-v0.109-xxhash64@1";
    public string Id { get; }
    public RunRandomLayout(string id)
    {
        if (id is not (Frozen or V109)) throw new ArgumentException($"Unknown random layout '{id}'.", nameof(id));
        Id = id;
    }

    public ulong Hash(string text) => Id == Frozen
        ? VanillaRandomLayout.DeterministicHashCode(text)
        : XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(text));

    public ulong RootSeed(string originalSeed)
    {
        ArgumentNullException.ThrowIfNull(originalSeed);
        // v0.109 interprets the prefix only for the root; named streams still use xxHash64.
        return Id == V109 && originalSeed.StartsWith("old", StringComparison.Ordinal)
            ? VanillaRandomLayout.DeterministicHashCode(originalSeed[3..]) : Hash(originalSeed);
    }

    public ulong Add(ulong seed, ulong offset) => Id == Frozen
        ? unchecked((uint)(seed + offset)) : unchecked(seed + offset);
    public ulong NamedSeed(ulong seed, string exactName) => Add(seed, Hash(exactName));
    public ulong ContentSeed(ulong seed, int offset, string entry) => NamedSeed(Add(seed, checked((ulong)offset)), entry);

    public IReadOnlyList<(string Type, string Stream, ulong Seed)> Streams(string originalSeed)
    {
        ulong root = RootSeed(originalSeed);
        return [.. VanillaRandomLayout.RunStreamTypes.Select(type =>
            (type, EngineStreamName(type), NamedSeed(root, VanillaRandomLayout.SnakeCase(type)))),
            .. VanillaRandomLayout.PlayerStreamTypes.Select(type =>
            (type, VanillaRandomLayout.EnginePlayerStreamName(type), NamedSeed(root, VanillaRandomLayout.SnakeCase(type))))];
    }

    public string EngineStreamName(string type) => Id == V109 && type == "CombatCardGeneration"
        ? SliceManifest.CardGenerationStream : VanillaRandomLayout.EngineStreamName(type);
}
