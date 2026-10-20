using System.Text;
using System.Globalization;

namespace CoreTheSpire.Engine;

/// <summary>
/// Named random service (K-05). Randomness is always addressed by <em>name</em>: there is no ambient or default
/// stream, so a rule set cannot consume randomness without the name being visible in the IR and in the identity.
///
/// Determinism contract:
/// <list type="bullet">
/// <item>a stream's seed is derived from the session seed, the algorithm id, the draw profile id and the stream
/// name only — never from creation order or from how many draws other streams made;</item>
/// <item>every draw advances only its own stream, so interleaving independent streams is reproducible;</item>
/// <item>read-only operations (<see cref="DrawCount"/>, <see cref="Diagnostics"/>, <see cref="Capture"/>) never
/// advance any stream, so observations and candidate queries cannot change the RNG position;</item>
/// <item>fixed session seed + fixed algorithm/profile + fixed draw sequence replays exactly.</item>
/// </list>
///
/// Fidelity caveat: <see cref="DrawProfileId"/> defaults to uniform rejection sampling. The recovered game maps
/// random bits to bounded integers through a different mapping. This service therefore does <em>not</em> claim seed
/// equivalence with the original game; a content pack that needs vanilla-equivalent draws must declare its own
/// algorithm and draw profile (and evidence) instead of assuming this default is equivalent.
///
/// ENV-01 adds such a pair explicitly — see <see cref="VanillaAlgorithmId"/> / <see cref="VanillaDrawProfileId"/>:
/// the same xoshiro256** bit generator as the vanilla source, with the vanilla **mapping**
/// (<c>(int)(NextDouble() * span) + min</c>, not rejection) and explicit stream seeding
/// (<see cref="SeedStream"/>) so a content pack can lay out the vanilla stream seeds itself.
/// </summary>
public sealed class RandomService
{
    public const string DefaultAlgorithmId = "xoshiro256ss/splitmix64@1";

    public const string DefaultDrawProfileId = "rejection-uniform@1";

    /// <summary>
    /// 原版兼容算法（`sts2/src/Core/Random/MegaRandom.cs:50-175`：xoshiro256** + SplitMix64 初始化）。
    /// 位生成器与本服务默认实现相同；差别在流种子布局与有界映射，因此两者是**不同的 Profile 身份**。
    /// </summary>
    public const string VanillaAlgorithmId = "mega-random/xoshiro256ss-splitmix64@1";

    /// <summary>
    /// 原版兼容有界映射（`MegaRandom.cs:284-287` + `Rng.cs:89-138`）：
    /// `Next(max) = (int)(NextDouble() * max)`、`Next(min,max) = Next(max-min) + min`、`NextDouble = (raw >> 11) * 2^-53`。
    /// 与默认的拒绝采样**不是同一个分布实现**，不能互换。
    /// </summary>
    public const string VanillaDrawProfileId = "vanilla-uniform@1";

    private readonly Dictionary<string, RandomStream> _streams = new(StringComparer.Ordinal);
    private readonly List<string> _creationOrder = [];
    private readonly Dictionary<string, ulong> _explicitSeeds = new(StringComparer.Ordinal);

    private readonly IRandomProfile _profile;
    internal SessionDebugTrace? Debug { get; set; }

    public RandomService(ulong sessionSeed, string algorithmId = DefaultAlgorithmId,
        string drawProfileId = DefaultDrawProfileId, IRandomProfile? profile = null)
    {
        if (profile is null)
        {
            if (algorithmId == DefaultAlgorithmId && drawProfileId == DefaultDrawProfileId)
                profile = new BuiltInRandomProfile(false);
            else if (algorithmId == VanillaAlgorithmId && drawProfileId == VanillaDrawProfileId)
                profile = new BuiltInRandomProfile(true);
            else throw Faults.Of(EngineFaultCode.Unsupported,
                $"random pair ('{algorithmId}', '{drawProfileId}') needs an explicitly supplied profile");
        }
        else if ((algorithmId != DefaultAlgorithmId || drawProfileId != DefaultDrawProfileId) &&
                 (algorithmId != profile.AlgorithmId || drawProfileId != profile.DrawProfileId))
            throw Faults.Of(EngineFaultCode.Unsupported, "supplied random profile does not match the requested ids");
        if (string.IsNullOrWhiteSpace(profile.AlgorithmId) || string.IsNullOrWhiteSpace(profile.DrawProfileId))
            throw Faults.Schema("random profile ids must be non-empty");
        _profile = profile;
        SessionSeed = sessionSeed;
        AlgorithmId = profile.AlgorithmId;
        DrawProfileId = profile.DrawProfileId;
        SeedFingerprint = Mix(sessionSeed);
    }

    /// <summary>True when this service draws like the recovered game (see <see cref="VanillaDrawProfileId"/>).</summary>
    public bool IsVanillaCompatible => string.Equals(DrawProfileId, VanillaDrawProfileId, StringComparison.Ordinal);

    /// <summary>The session seed itself stays internal to the service; the identity carries a fingerprint.</summary>
    public ulong SessionSeed { get; }

    /// <summary>Stable seed fingerprint used in identity and diagnostics.</summary>
    public ulong SeedFingerprint { get; }

    public string AlgorithmId { get; }

    public string DrawProfileId { get; }

    public long TotalDraws { get; private set; }

    /// <summary>Read-only: the number of draws this stream has performed. Does not create the stream.</summary>
    public long DrawCount(string stream) =>
        _streams.TryGetValue(RequireName(stream), out var value) ? value.Draws : 0;

    /// <summary>Read-only diagnostics in stable (ordinal name) order. Does not advance any stream.</summary>
    public IReadOnlyList<RandomStreamDiagnostics> Diagnostics()
    {
        var names = _streams.Keys.ToArray();
        Array.Sort(names, StringComparer.Ordinal);
        var diagnostics = new RandomStreamDiagnostics[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            var stream = _streams[names[i]];
            diagnostics[i] = new RandomStreamDiagnostics(names[i], stream.Draws, stream.Fingerprint());
        }

        return diagnostics;
    }

    public IReadOnlyList<string> Streams => _creationOrder.AsReadOnly();

    public ulong DrawUInt64(string stream)
    {
        var before = DrawCount(stream); var total = TotalDraws;
        var result = Next(Require(stream));
        Record(stream, "uint64", "0", "18446744073709551616", result, before, total);
        return result;
    }

    private ulong Next(RandomStream stream)
    {
        TotalDraws++;
        return stream.Next();
    }

    /// <summary>
    /// 显式指定一条流的种子（原版兼容 Profile 用：原版的流种子是内容/角色/槽位算出来的 **uint**，
    /// 不该由引擎的派生规则代替）。必须在流被创建之前调用——已经抽过的流换种子是静默分歧，直接拒绝。
    /// </summary>
    public void SeedStream(string stream, ulong seed)
    {
        var name = RequireName(stream);
        if (_streams.ContainsKey(name) || _explicitSeeds.ContainsKey(name))
        {
            throw Faults.Of(EngineFaultCode.Unsupported,
                $"stream '{name}' already exists; a stream seed can only be set before its first draw");
        }

        _explicitSeeds[name] = seed;
    }

    /// <summary>
    /// Uniform draw in <c>[0, count)</c>. The **default** profile uses rejection sampling (no modulo bias) and always
    /// consumes at least one raw word; the **vanilla** profile uses the recovered game's double-truncation mapping.
    /// </summary>
    public int DrawIndex(string stream, int count)
    {
        if (count <= 0) throw Faults.Range($"random draw needs a positive count but received {count}");
        var before = DrawCount(stream); var total = TotalDraws;
        var result = checked((int)DrawBounded(stream, 0, count));
        Record(stream, "index", "0", count.ToString(CultureInfo.InvariantCulture), result, before, total);
        return result;
    }

    /// <summary>Uniform draw in <c>[minInclusive, maxExclusive)</c> under the active draw profile.</summary>
    public long DrawInt(string stream, long minInclusive, long maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw Faults.Range(
                $"random range is empty: [{minInclusive}, {maxExclusive})");
        }

        var before = DrawCount(stream); var total = TotalDraws;
        var result = DrawBounded(stream, minInclusive, maxExclusive);
        Record(stream, "int", minInclusive.ToString(CultureInfo.InvariantCulture),
            maxExclusive.ToString(CultureInfo.InvariantCulture), result, before, total);
        return result;
    }

    public int DrawInt(string stream, int minInclusive, int maxExclusive) =>
        checked((int)DrawInt(stream, (long)minInclusive, maxExclusive));

    /// <summary>
    /// Uniform <c>uint</c> in <c>[minInclusive, maxExclusive)</c>.
    /// The vanilla profile mirrors `Rng.NextUnsignedInt` (`Rng.cs:127-138`): `min + (uint)(NextDouble() * (max - min))`.
    /// </summary>
    public uint DrawUnsignedInt(string stream, uint minInclusive, uint maxExclusive)
    {
        if (minInclusive >= maxExclusive) throw Faults.Range($"random range is empty: [{minInclusive}, {maxExclusive})");
        var before = DrawCount(stream); var total = TotalDraws;
        var result = checked((uint)DrawBounded(stream, minInclusive, maxExclusive));
        Record(stream, "uint", minInclusive.ToString(CultureInfo.InvariantCulture),
            maxExclusive.ToString(CultureInfo.InvariantCulture), result, before, total);
        return result;
    }

    /// <summary>
    /// Uniform double in <c>[0, 1)</c>. Both profiles use the same 53-bit construction
    /// (`(raw &gt;&gt; 11) * 2^-53`, `MegaRandom.cs:222-225`); it is only reachable through the vanilla profile's
    /// bounded mapping otherwise.
    /// </summary>
    public double DrawDouble(string stream)
    {
        var target = Require(stream);
        var before = target.Draws; var total = TotalDraws;
        var result = _profile.DrawDouble(() => Next(target));
        Record(stream, "double", "0", "1", result, before, total);
        return result;
    }

    public bool DrawBool(string stream)
    {
        var target = Require(stream);
        var before = target.Draws; var total = TotalDraws;
        var result = _profile.DrawBool(() => Next(target));
        Record(stream, "bool", "0", "2", result, before, total);
        return result;
    }

    /// <summary>Picks from the caller's ordered candidates. The engine never silently reorders them.</summary>
    public InstanceId DrawPick(string stream, IReadOnlyList<InstanceId> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0) throw Faults.Range("cannot pick from an empty candidate list");
        var before = DrawCount(stream); var total = TotalDraws;
        var index = checked((int)DrawBounded(stream, 0, candidates.Count));
        Record(stream, "pick", "0", candidates.Count.ToString(CultureInfo.InvariantCulture), index, before, total, candidates);
        return candidates[index];
    }

    /// <summary>In-place Fisher-Yates shuffle (descending index), the only shuffle order this engine defines.</summary>
    public void Shuffle<T>(string stream, IList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        for (var i = items.Count - 1; i > 0; i--)
        {
            var before = DrawCount(stream); var total = TotalDraws;
            var j = checked((int)DrawBounded(stream, 0, i + 1));
            // The snapshot is taken before the swap and includes the active candidate prefix only.
            Record(stream, "shuffle", "0", (i + 1).ToString(CultureInfo.InvariantCulture), j, before, total,
                Debug is not null && items is IList<InstanceId> ids ? new CandidatePrefix(ids, i + 1) : null);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    private void Record<T>(string stream, string operation, string min, string max, T result,
        long before, long totalBefore, IReadOnlyList<InstanceId>? candidates = null) =>
        Debug?.RecordRandom(this, stream, operation, min, max,
            Convert.ToString(result, CultureInfo.InvariantCulture)!, before, totalBefore, candidates);

    private sealed class CandidatePrefix(IList<InstanceId> items, int count) : IReadOnlyList<InstanceId>
    {
        public int Count => count;
        public InstanceId this[int index] => index >= 0 && index < count ? items[index] : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<InstanceId> GetEnumerator() => items.Take(count).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>Deterministic sub-seed derived from a name; used to start independent child sessions.</summary>
    public ulong DeriveSeed(string purpose)
    {
        var name = RequireName(purpose);
        return Mix(SessionSeed ^ Fnv1a64(AlgorithmId + "|derive|" + name));
    }

    public RandomSnapshot Capture()
    {
        var streams = _creationOrder.Select(name => _streams[name].Capture()).ToArray();
        return new RandomSnapshot(AlgorithmId, DrawProfileId, SessionSeed, TotalDraws, Array.AsReadOnly(streams))
        { ExplicitSeeds = new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(new Dictionary<string, ulong>(_explicitSeeds)) };
    }

    /// <summary>Restores a matching position. Validate all streams before replacing current storage.</summary>
    public void Restore(RandomSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.AlgorithmId != AlgorithmId || snapshot.DrawProfileId != DrawProfileId || snapshot.SessionSeed != SessionSeed)
            throw Faults.Of(EngineFaultCode.SnapshotInvalid, "random snapshot belongs to a different profile or seed");
        var restored = new Dictionary<string, RandomStream>(StringComparer.Ordinal);
        foreach (var saved in snapshot.Streams)
        {
            var generator = _profile.Create(0);
            generator.Restore(saved.BackendState ?? new[] { saved.S0, saved.S1, saved.S2, saved.S3 });
            if (!restored.TryAdd(RequireName(saved.Name), new RandomStream(saved.Name, generator, saved.Draws)))
                throw Faults.Of(EngineFaultCode.SnapshotInvalid, $"snapshot repeats stream '{saved.Name}'");
        }
        var seeds = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var seed in snapshot.ExplicitSeeds) seeds.Add(RequireName(seed.Key), seed.Value);
        _streams.Clear(); _creationOrder.Clear(); _explicitSeeds.Clear();
        foreach (var seed in seeds) _explicitSeeds.Add(seed.Key, seed.Value);
        foreach (var (name, stream) in restored) { _streams.Add(name, stream); _creationOrder.Add(name); }
        TotalDraws = snapshot.TotalDraws;
    }

    private long DrawBounded(string stream, long minInclusive, long maxExclusive)
    {
        var span = maxExclusive - minInclusive;
        if (span <= 0) throw Faults.Of(EngineFaultCode.ValueOutOfRange,
            $"random range [{minInclusive}, {maxExclusive}) does not fit in a signed 64-bit span");
        var target = Require(stream);
        var value = _profile.DrawBounded(() => Next(target), minInclusive, maxExclusive);
        if (value < minInclusive || value >= maxExclusive)
            throw Faults.Range("random profile returned a value outside the requested bounds");
        return value;
    }

    private RandomStream Require(string stream)
    {
        var name = RequireName(stream);
        if (_streams.TryGetValue(name, out var existing)) return existing;
        var seed = _explicitSeeds.TryGetValue(name, out var explicitSeed) ? explicitSeed :
            Mix(SessionSeed ^ Fnv1a64(AlgorithmId + "|" + DrawProfileId + "|" + name));
        var created = new RandomStream(name, _profile.Create(seed));
        _streams.Add(name, created); _creationOrder.Add(name);
        return created;
    }

    private static string RequireName(string stream) =>
        string.IsNullOrWhiteSpace(stream) ? throw Faults.Schema("a random stream name is required") : stream;

    internal static ulong Fnv1a64(string text)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }

    internal static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    internal static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));

    private sealed class RandomStream(string name, IRandomGenerator generator, long draws = 0)
    {
        public string Name { get; } = name;
        public long Draws { get; private set; } = draws;
        public ulong Next() { Draws++; return generator.Next(); }
        public RandomStreamSnapshot Capture()
        {
            var state = generator.Capture().ToArray();
            ulong Word(int index) => index < state.Length ? state[index] : 0;
            return new RandomStreamSnapshot(Name, Word(0), Word(1), Word(2), Word(3), Draws)
                { BackendState = Array.AsReadOnly(state) };
        }
        public ulong Fingerprint()
        {
            var state = generator.Capture();
            if (state.Count == 4) return Mix(state[0] ^ RotateLeft(state[1], 13) ^ RotateLeft(state[2], 29) ^ RotateLeft(state[3], 47));
            var hash = Mix((ulong)state.Count);
            foreach (var word in state) hash = Mix(hash ^ word);
            return hash;
        }
    }

}

/// <summary>Read-only per-stream diagnostics. Never advances the stream.</summary>
public sealed record RandomStreamDiagnostics(string Name, long Draws, ulong StateFingerprint);

public sealed record RandomStreamSnapshot(string Name, ulong S0, ulong S1, ulong S2, ulong S3, long Draws)
{
    public IReadOnlyList<ulong>? BackendState { get; init; }
}

public sealed record RandomSnapshot(
    string AlgorithmId,
    string DrawProfileId,
    ulong SessionSeed,
    long TotalDraws,
    IReadOnlyList<RandomStreamSnapshot> Streams)
{
    public IReadOnlyDictionary<string, ulong> ExplicitSeeds { get; init; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, ulong>(new Dictionary<string, ulong>());
}
