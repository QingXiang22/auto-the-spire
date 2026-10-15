#if REFERENCE_SIM
using System.Diagnostics;
using System.Text.Json;
using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Differential;

/// <summary>Tool-only sample observation. Factory run remains unmodified; instrumented replay is separately identified.</summary>
internal static class StartupTrace
{
    private sealed record Draw(string Stream, int Counter, string Operation, double Min, double Max,
        double Sample, string Before, string CallPath);

    public static bool Run(string seed, string output)
    {
        CombatReplayDemo.EnsureModelsRegistered();
        var reference = new List<Draw>();
        Player? player = null;
        string[]? referenceShuffleInput = null;
        string[]? baseShuffleInput = null;
        RunState? run = null;
        var previous = RngDiagnostics.DrawObserver;
        RngDrawObserver? observe = null;
        observe = (rng, op, min, max, arity) =>
        {
            previous?.Invoke(rng, op, min, max, arity);
            if (run is null) return;
            var type = Enum.GetValues<RunRngType>().FirstOrDefault(t => ReferenceEquals(run.Rng.GetRng(t), rng));
            if (!ReferenceEquals(run.Rng.GetRng(type), rng)) return;
            if (type == RunRngType.Shuffle && referenceShuffleInput is null)
                referenceShuffleInput = player!.PlayerCombatState!.DrawPile.Cards.Select(c => c.Id.Entry).ToArray();
            // Observer is called after Counter++, before generator consumption. Clone has no global observation.
            var copy = rng.CloneExact();
            var state = rng.ToSerializable();
            RngDiagnostics.DrawObserver = null;
            double sample;
            try
            {
                sample = op switch
                {
                    "NextInt" => arity == 1 ? copy.NextInt((int)max) : copy.NextInt((int)min, (int)max),
                    "NextUnsignedInt" => copy.NextUnsignedInt((uint)min, (uint)max),
                    "NextFloat" => copy.NextFloat((float)min, (float)max),
                    "NextDouble" => arity == 0 ? copy.NextDouble() : copy.NextDouble(min, max),
                    "NextBool" => copy.NextBool() ? 1 : 0,
                    _ => double.NaN,
                };
            }
            finally { RngDiagnostics.DrawObserver = observe; }
            reference.Add(new Draw(RngSourceCompat.EngineStreamName(type.ToString()), rng.Counter, op,
                min, max, sample, RngSourceCompat.FingerprintText(state.state0, state.state1, state.state2, state.state3),
                new StackTrace(1, true).ToString()));
        };
        RngDiagnostics.DrawObserver = observe;
        CombatRoom room;
        try
        {
            run = new RunState(seed, new Overgrowth());
            RngSourceCompat.Apply(run.Rng, seed);
            player = Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), run);
            run.AddPlayer(player);
            room = (CombatRoom)RoomFactory.CreateRoom(run, RoomType.Monster);
            run.PushRoom(room);
            room.Enter(run).GetAwaiter().GetResult();
        }
        finally { RngDiagnostics.DrawObserver = previous; }

        var factory = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad(0, BaseSpireScope.Dev));
        var normal = factory.CreateSession(seed);
        normal.Start();
        // Existing factory exposes no debug/profile injection. Replay exactly its immutable Rules and published configuration;
        // verify facts and RNG diagnostics against the actual factory run, never substitute this for normal-run evidence.
        var profile = new ObservedProfile(normal.SeededStreams.Concat(normal.EncounterStreams));
        var replay = new Session(factory.Rules, normal.RunSeed, [SliceManifest.PlayerRole],
            computeBackend: SourceWeightedPickBackend.Instance,
            randomAlgorithmId: VanillaRandomLayout.AlgorithmId, randomDrawProfileId: VanillaRandomLayout.DrawProfileId,
            randomProfile: profile, debug: new SessionDebugOptions(100000));
        foreach (var item in normal.SeededStreams.Concat(normal.EncounterStreams)) replay.Random.SeedStream(item.Stream, item.Seed);
        profile.BeforeDraw = name =>
        {
            if (baseShuffleInput is null && name == RngSourceCompat.EngineStreamName("Shuffle"))
                baseShuffleInput = EngineSnapshot.Facts(replay, true).Where(x => x.Path == "pile.draw.order").Select(x => x.Value).ToArray();
        };
        replay.Start();
        var matches = EngineSnapshot.Facts(normal.Session, true).SequenceEqual(EngineSnapshot.Facts(replay, true))
            && normal.Session.Random.Diagnostics().SequenceEqual(replay.Random.Diagnostics());
        var options = new JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
        File.WriteAllText(Path.Combine(output, "startup-consumption-trace.json"), JsonSerializer.Serialize(new
        {
            version = "env-t04-startup-trace@1", generated_at = DateTimeOffset.Now,
            game_seed = seed, run_seed = normal.RunSeed, profile = RngSourceCompat.ProfileId,
            reference_identity = ReferenceRunEntry.Identity(seed, RngSourceCompat.RunStreams(seed)),
            base_build = normal.Build, base_seeded_streams = normal.SeededStreams.Select(x => new { x.Type, x.Stream, x.Seed }),
            source_digests = Repo.DigestTree("sts2/src/Core"),
            suite_digest = Repo.DigestFiles(["tools/differential/suites/regression.json"]).Select(x => new { x.Path, x.Sha256 }),
            normal_factory = new { facts = EngineSnapshot.Facts(normal.Session, true), streams = normal.Session.Random.Diagnostics() },
            instrumented_replay = new { label = "same immutable factory Rules; observed profile forwarding, not normal factory", matches_normal = matches,
                dropped_debug_records = replay.Debug!.DroppedRecords, draws = profile.Draws, calls = replay.Debug.Capture() },
            reference_shuffle_input = referenceShuffleInput, base_pre_draw_probe = baseShuffleInput,
            reference_draws = reference, reference_facts = ReferenceBackend.Facts(room.Engine.State, player, true),
        }, options));
        Console.WriteLine($"startup trace: reference={reference.Count}, base={profile.Draws.Count}, replay_matches_factory={matches}");
        return matches && replay.Debug!.DroppedRecords == 0;
    }

    private sealed class ObservedProfile : IRandomProfile
    {
        private readonly IRandomProfile inner = VanillaRandomLayout.Profile;
        private readonly Dictionary<ulong, string> streams;
        private string current = "";
        private readonly Dictionary<string, int> counters = new();
        public Action<string>? BeforeDraw { get; set; }
        public List<Draw> Draws { get; } = [];
        public ObservedProfile(IEnumerable<(string Type, string Stream, uint Seed)> seeds) =>
            streams = seeds.GroupBy(x => (ulong)x.Seed).ToDictionary(g => g.Key, g => string.Join("|", g.Select(x => x.Stream)));
        public string AlgorithmId => inner.AlgorithmId;
        public string DrawProfileId => inner.DrawProfileId;
        public IRandomGenerator Create(ulong seed) => new Generator(inner.Create(seed), streams.GetValueOrDefault(seed, $"unmapped:{seed}"), this);
        public long DrawBounded(Func<ulong> next, long min, long max)
        {
            var path = new StackTrace(1, true).ToString();
            var value = inner.DrawBounded(next, min, max);
            Draws.Add(new Draw(current, counters[current], "DrawBounded", min, max, value, before, path));
            return value;
        }
        private string before = "";
        public double DrawDouble(Func<ulong> next) => inner.DrawDouble(next);
        public bool DrawBool(Func<ulong> next) => inner.DrawBool(next);
        private sealed class Generator(IRandomGenerator inner, string name, ObservedProfile owner) : IRandomGenerator
        {
            public ulong Next()
            {
                owner.BeforeDraw?.Invoke(name);
                var s = inner.Capture();
                owner.before = RngSourceCompat.FingerprintText(s[0], s[1], s[2], s[3]);
                owner.current = name;
                owner.counters[name] = owner.counters.GetValueOrDefault(name) + 1;
                return inner.Next();
            }
            public IReadOnlyList<ulong> Capture() => inner.Capture();
            public void Restore(IReadOnlyList<ulong> state) => inner.Restore(state);
        }
    }
}
#endif
