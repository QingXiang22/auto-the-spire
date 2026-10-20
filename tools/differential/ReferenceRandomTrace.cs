#if REFERENCE_SIM
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Differential;

/// <summary>Tool-only observer on the locked reference's existing per-instance post-draw hook.</summary>
internal sealed class ReferenceRandomTrace : IDisposable
{
    private const int Capacity = 20_000;
    private readonly Dictionary<string, Rng> streams = [];
    private readonly Dictionary<string, int> starts = [];
    private readonly List<(Rng Rng, object? Previous)> hooks = [];
    private readonly List<RandomDraw> calls = [];
    private readonly Player player;
    private readonly IReadOnlyDictionary<CardModel, string> slots;
    private List<CardModel>? shuffle;
    private int expectedShuffleCount;
    private long dropped;
    private bool truncated;
    private readonly FieldInfo observer = typeof(Rng).GetField("_drawObserver", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new NotSupportedException("Locked reference has no per-instance result hook.");

    public ReferenceRandomTrace(RunRngSet run, Player player, IReadOnlyDictionary<CardModel, string> slots)
    {
        this.player = player; this.slots = slots;
        if (run.UsesSemanticKeys || player.PlayerRng.UsesSemanticKeys)
            throw new NotSupportedException("Original comparison requires sequential reference streams.");
        try
        {
            foreach (var type in Enum.GetValues<RunRngType>()) Attach("base-spire:run:" + StringHelper.SnakeCase(type.ToString()), run.GetRng(type));
            foreach (var type in Enum.GetValues<PlayerRngType>()) Attach("base-spire:player:" + StringHelper.SnakeCase(type.ToString()), player.PlayerRng.GetRng(type));
        }
        catch { Dispose(); throw; }
    }

    private void Attach(string name, Rng rng)
    {
        streams.Add(name, rng); starts.Add(name, rng.Counter);
        var previous = observer.GetValue(rng);
        if (previous is not null) throw new NotSupportedException("Reference result hook already owned by another observer.");
        Action<int, string, string> hook = (cursor, operation, result) =>
        {
            if (calls.Count >= Capacity) { dropped++; return; }
            var match = Regex.Match(operation, @"^NextInt\(maxExclusive=(\d+)\)$");
            string[]? candidates = null;
            var minimum = ""; var maximum = "";
            if (name == "base-spire:run:shuffle" && match.Success)
            {
                var count = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (shuffle is null)
                {
                    // The upstream callback runs after sampling but before Rng.Shuffle swaps. Its
                    // source piles are still intact. Reconstruct that reference's own StableShuffle
                    // candidate order independently, then verify it against the real resulting piles.
                    var state = player.PlayerCombatState!;
                    shuffle = state.DiscardPile.Cards.Concat(state.DrawPile.Cards).ToList(); shuffle.Sort();
                    expectedShuffleCount = shuffle.Count;
                }
                if (count > shuffle.Count || count < 2 || count != expectedShuffleCount) { truncated = true; }
                else
                {
                    candidates = shuffle.Take(count).Select(c => slots[c]).ToArray();
                    var selected = int.Parse(result, CultureInfo.InvariantCulture);
                    (shuffle[count - 1], shuffle[selected]) = (shuffle[selected], shuffle[count - 1]);
                }
                expectedShuffleCount--;
                operation = "shuffle"; minimum = "0"; maximum = count.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                var range = Regex.Match(operation, @"^NextInt\(minInclusive=(-?\d+),maxExclusive=(-?\d+)\)$");
                if (range.Success) { operation = "int"; minimum = range.Groups[1].Value; maximum = range.Groups[2].Value; }
                else if (match.Success) { operation = "index"; minimum = "0"; maximum = match.Groups[1].Value; }
                else truncated = true; // Never certify an unextracted operation/parameter surface.
            }
            calls.Add(new(name, operation, minimum, maximum, result, cursor - 1L, cursor, candidates,
                "reference/sts2-sim/src/Sts2Sim.Core/Random/Rng.cs"));
        };
        observer.SetValue(rng, hook); hooks.Add((rng, previous));
    }

    public void VerifyShuffle()
    {
        if (shuffle is null) return;
        var state = player.PlayerCombatState!;
        if (expectedShuffleCount != 1 || !state.Hand.Cards.Concat(state.DrawPile.Cards).SequenceEqual(shuffle))
            truncated = true;
        shuffle = null;
    }

    public RandomTrace Capture() => new(calls.ToArray(), dropped, truncated,
        streams.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p =>
        {
            var state = p.Value.ToSerializable();
            return new StreamPosition(p.Key, starts[p.Key], state.counter,
                string.Join(",", new[] { state.state0, state.state1, state.state2, state.state3 }.Select(w => w.ToString("x16", CultureInfo.InvariantCulture))));
        }).ToArray());

    public static long NamedDrawCount(RunRngSet run, Player player) =>
        Enum.GetValues<RunRngType>().Sum(type => (long)run.GetRng(type).Counter)
        + Enum.GetValues<PlayerRngType>().Sum(type => (long)player.PlayerRng.GetRng(type).Counter);

    public void Dispose()
    {
        foreach (var (rng, previous) in hooks) observer.SetValue(rng, previous);
        hooks.Clear();
    }

    // Only the three seed-derivation differences authorized for this original controlled case.
    // The upstream generator, mapping, default constructor and game effects remain unchanged.
    public static void ConfigureSourceStreams(RunRngSet run, string seed)
    {
        foreach (var type in Enum.GetValues<RunRngType>()) run.MockRng(type, SourceSeed(seed, StringHelper.SnakeCase(type.ToString())));
    }

    public static uint SourceSeed(string gameSeed, string name) => unchecked(Hash32(gameSeed) + Hash32(name));

    public static uint Hash32(string text)
    {
        unchecked
        {
            var a = 352654597; var b = a;
            for (var i = 0; i < text.Length; i += 2)
            {
                a = ((a << 5) + a) ^ text[i];
                if (i + 1 < text.Length) b = ((b << 5) + b) ^ text[i + 1];
            }
            return (uint)(a + b * 1566083941);
        }
    }
}
#endif
