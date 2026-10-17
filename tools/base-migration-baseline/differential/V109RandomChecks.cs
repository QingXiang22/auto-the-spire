#if REFERENCE_SIM
using System.Text.Json;
using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Differential;

internal static partial class NormalRewardEntry
{
    public static int CheckV109Random(string output)
    {
        Directory.CreateDirectory(output);
        CombatReplayDemo.EnsureModelsRegistered();
        var configuration = RunConfiguration.Ironclad(0, BaseSpireScope.DevMap);
        var factory = new BaseSpireRuntimeFactory(configuration);
        var historical = new BaseSpireRuntimeFactory(configuration with { RandomLayoutId = RunRandomLayout.Frozen });
        List<string> failures = [];
        List<object> results = [];
        var content = SinglePlayerContentVersions.Apply(SinglePlayerContentSource.Shared, configuration.ContentMetadataVersion);
        var ownPool = IroncladRewardPool.SourceOrder.Where(id => !IroncladRewardPool.MultiplayerOnly.Contains(id, StringComparer.Ordinal))
            .Select(id => content.Model.Cards.Single(card => card.Id == "base-spire:" + id)).ToArray();
        var nativePool = ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>().CardPool
            .GetUnlockedCards(Sts2Sim.Core.Entities.Players.PlayerUnlockState.AllUnlocked(), false).ToArray();
        if (!ownPool.Select(c => c.Id).SequenceEqual(nativePool.Select(c => ReferenceBackend.Project(c.Id))))
            failures.Add("Single-player Ironclad card pool identity/order differs");
        foreach (var card in ownPool)
        {
            var native = nativePool.SingleOrDefault(c => ReferenceBackend.Project(c.Id) == card.Id);
            if (native is null || !string.Equals(card.Rarity, native.Rarity.ToString(), StringComparison.OrdinalIgnoreCase)
                || !string.Equals(card.UpgradedRarity, native.Rarity.ToString(), StringComparison.OrdinalIgnoreCase))
                failures.Add("Single-player card rarity differs: " + card.Id);
        }
        var ownAttacks = ownPool.Where(c => c.CanGenerateInCombat && !c.MultiplayerOnly
            && c.Kind == "Attack" && c.Rarity is not ("basic" or "ancient" or "event")).Select(c => c.Id).ToArray();
        var nativeAttacks = nativePool.Where(c => c.CanBeGeneratedInCombat && c.Type == Sts2Sim.Core.Entities.Cards.CardType.Attack
            && c.Rarity is not (Sts2Sim.Core.Entities.Cards.CardRarity.Basic or Sts2Sim.Core.Entities.Cards.CardRarity.Ancient or Sts2Sim.Core.Entities.Cards.CardRarity.Event))
            .Select(c => ReferenceBackend.Project(c.Id)).ToArray();
        if (!ownAttacks.SequenceEqual(nativeAttacks)) failures.Add("AttackPotion complete eligible pool identity/order differs");
        if (factory.Describe().PublicBuildId == historical.Describe().PublicBuildId)
            failures.Add("Random layouts share a public build identity");
        foreach (string seed in new[] { "ENV-T03-SEED-L", "ENV-T04-RNG-SHUFFLE-001",
            "oldENV-T04-RNG-SHUFFLE-001", "中文🌱", "  Mixed Case  ", "FFFFFFFFFFFFFFFF" })
        {
            var localFailures = new List<string>();
            void Equal<T>(string name, T expected, T actual)
            {
                if (!EqualityComparer<T>.Default.Equals(expected, actual))
                    localFailures.Add($"{name}: expected {expected}, got {actual}");
            }
            var actual = factory.CreateSession(seed);
            var reference = new RunState(seed, new Overgrowth());
            Equal("root", reference.Rng.Seed, actual.RunSeed);
            Equal("raw seed", seed, actual.AcceptedSeed);
            Equal("before Start", 0L, actual.Session.Random.TotalDraws);
            foreach (var item in actual.SeededStreams.Where(s => s.Type is not ("ActMap" or "Event")))
            {
                var native = item.Stream.StartsWith("base-spire:player:", StringComparison.Ordinal)
                    ? new Sts2Sim.Core.Random.PlayerRngSet(reference.Rng.Seed).GetRng(
                        Enum.Parse<Sts2Sim.Core.Entities.Rngs.PlayerRngType>(item.Type))
                    : reference.Rng.GetRng(Enum.Parse<Sts2Sim.Core.Entities.Rngs.RunRngType>(item.Type)).CloneExact();
                var service = new RandomService(actual.RunSeed, VanillaRandomLayout.AlgorithmId,
                    VanillaRandomLayout.DrawProfileId, VanillaRandomLayout.Profile);
                service.SeedStream(item.Stream, item.Seed);
                for (int i = 0; i < 3; i++)
                    Equal(item.Type + " sample " + i, (long)native.NextInt(1000000), service.DrawInt(item.Stream, 0, 1000000));
            }
            // Public native lifecycle only; both sides complete pre-generation before comparison.
            var player = Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Ironclad>(), reference);
            reference.AddPlayer(player);
            _ = reference.CurrentAncientEventType;
            actual.Start();
            Equal("factory status", SessionStatus.Running, actual.Session.Status);
            var actualMap = new BaseSide(actual.Session).Facts().Where(f => f.Path.StartsWith("map.", StringComparison.Ordinal)).ToArray();
            using var probe = new ReferenceRandomProbe();
            var referenceMap = new ReferenceSide(reference, player, probe).Facts().Where(f => f.Path.StartsWith("map.", StringComparison.Ordinal)).ToArray();
            if (!actualMap.SequenceEqual(referenceMap)) localFailures.Add("map topology or point types differ");
            var upfront = EngineSnapshot.Streams(actual.Session).Single(s => s.Stream == RngSourceCompat.EngineStreamName("UpFront"));
            Equal("UpFront count", (long)reference.Rng.UpFront.Counter, upfront.Draws);
            var state = reference.Rng.UpFront.ToSerializable();
            Equal("UpFront fingerprint", RngSourceCompat.FingerprintText(state.state0, state.state1, state.state2, state.state3), upfront.Fingerprint);
            if (seed is "ENV-T03-SEED-L" or "ENV-T04-RNG-SHUFFLE-001")
            {
                var nativeSide = new ReferenceSide(reference, player, probe);
                var ownSide = new BaseSide(actual.Session);
                var token = ownSide.Actions().Single(a => a.Action == "base-spire:ChooseMapPoint").Candidates.Order(StringComparer.Ordinal).First();
                ownSide.Execute("base-spire:ChooseMapPoint", token);
                nativeSide.Execute("base-spire:ChooseMapPoint", token);
                var monsters = actual.Session.State.CollectionMembers(actual.Session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Monsters").Id);
                var nativeRoom = (Sts2Sim.Core.Rooms.CombatRoom)reference.CurrentRoom!;
                Equal("monster count", nativeRoom.Engine.State.Enemies.Count, monsters.Count);
                var nativeAi = reference.Rng.MonsterAi.CloneExact();
                for (int draw = 0; draw < 3; draw++)
                    Equal("state-machine MonsterAi sample " + draw, (long)nativeAi.NextInt(1000000),
                        actual.Session.Random.DrawInt(RngSourceCompat.EngineStreamName("MonsterAi"), 0, 1000000));
            }
            results.Add(new { seed, root = actual.RunSeed, streams = actual.SeededStreams.Select(s => new { s.Type, s.Stream, s.Seed }),
                upfront, map_facts = actualMap.Length, failures = localFailures });
            failures.AddRange(localFailures.Select(f => seed + ": " + f));
        }
        File.WriteAllText(Path.Combine(output, "v109-rng.json"), JsonSerializer.Serialize(new
        { version = RunRandomLayout.V109, build = factory.Describe(), reference = NativeIdentity("ENV-T03-SEED-L"),
            card_pool = ownPool.Select(c => new { c.Id, c.Rarity, c.UpgradedRarity }), attack_generation_pool = ownAttacks, results, failures },
            new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
        Console.WriteLine($"v0.109 random initialization: {results.Count} seeds, {failures.Count} failures");
        foreach (var failure in failures) Console.WriteLine(failure);
        return failures.Count == 0 ? 0 : 1;
    }
}
#else
namespace Differential;
internal static partial class NormalRewardEntry
{
    public static int CheckV109Random(string output)
    {
        Console.Error.WriteLine("v109-rng requires the independent reference adapter");
        return 3;
    }
}
#endif
