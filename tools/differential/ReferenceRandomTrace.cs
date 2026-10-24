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
    private readonly Func<CardModel, string> cardSlot;
    private readonly Func<string, int, string[]?>? indexCandidates;
    private List<CardModel>? shuffle;
    private int expectedShuffleCount;
    private long dropped;
    private bool truncated;
    private bool initialShuffle;
    private readonly FieldInfo observer = typeof(Rng).GetField("_drawObserver", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new NotSupportedException("Locked reference has no per-instance result hook.");

    public ReferenceRandomTrace(RunRngSet run, Player player, IReadOnlyDictionary<CardModel, string> slots, bool initialShuffle = false)
        : this(run, player, card => slots[card], initialShuffle) { }

    public ReferenceRandomTrace(RunRngSet run, Player player, Func<CardModel, string> cardSlot, bool initialShuffle = false,
        Func<string, int, string[]?>? indexCandidates = null)
    {
        this.player = player; this.cardSlot = cardSlot; this.initialShuffle = initialShuffle; this.indexCandidates = indexCandidates;
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
                    // PopulateCombatState/RandomizeOrderInternal uses the current deck order;
                    // later CardPileCmd.Shuffle uses StableShuffle's sorted discard order.
                    shuffle = state.DiscardPile.Cards.Concat(state.DrawPile.Cards).ToList();
                    if (!initialShuffle) shuffle.Sort();
                    initialShuffle = false;
                    expectedShuffleCount = shuffle.Count;
                }
                if (count > shuffle.Count || count < 2 || count != expectedShuffleCount) { truncated = true; }
                else
                {
                    candidates = shuffle.Take(count).Select(cardSlot).ToArray();
                    var selected = int.Parse(result, CultureInfo.InvariantCulture);
                    (shuffle[count - 1], shuffle[selected]) = (shuffle[selected], shuffle[count - 1]);
                }
                expectedShuffleCount--;
                operation = "shuffle"; minimum = "0"; maximum = count.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                var range = Regex.Match(operation, @"^NextInt\(minInclusive=(-?\d+),maxExclusive=(-?\d+)\)$");
                if (range.Success)
                {
                    operation = "int"; minimum = range.Groups[1].Value; maximum = range.Groups[2].Value;
                    if (minimum == "0")
                    {
                        var count = int.Parse(maximum, CultureInfo.InvariantCulture);
                        candidates = indexCandidates?.Invoke(name, count);
                        if (candidates is not null)
                        {
                            if (candidates.Length != count) truncated = true;
                            operation = "pick";
                        }
                    }
                }
                else if (match.Success)
                {
                    operation = "index"; minimum = "0"; maximum = match.Groups[1].Value;
                    var count = int.Parse(maximum, CultureInfo.InvariantCulture);
                    candidates = indexCandidates?.Invoke(name, count);
                    if (candidates is not null)
                    {
                        if (candidates.Length != count) truncated = true;
                        operation = "pick";
                    }
                }
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

    // Native seed identity for the original ordered controlled fixture only.
    // No run/player compatibility replacement or semantic rekeying is applied.
    public static ulong SourceSeed(string gameSeed, string name) =>
        unchecked(new RunRngSet(gameSeed).Seed + StringHelper.GetDeterministicHashCode(name));
}
#endif
