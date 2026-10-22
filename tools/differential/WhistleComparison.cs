using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class WhistleComparison
{
    private sealed record Monster(long Hp, long Strength, string Move, string Intent, string[] History);
    private sealed record Boundary(string At, Monster[] Enemies, long Hp, long Energy, long Plays,
        long[] HandCosts, long[] ExhaustCosts, bool Active, long AiDraws, ulong[] AiState, long ShuffleDraws);

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output"))
            throw new FormatException("Use whistle-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        var package = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        var results = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" })
        foreach (var mode in new[] { "first", "upgraded", "pending-buff", "pending-attack", "random", "two", "lethal", "repeat" })
        {
            var left = Dsl(package, seed, mode); var right = Native(seed, mode);
            var same = NormalPreflight.Json(left) == NormalPreflight.Json(right); passed &= same;
            if (left.Length != (mode == "repeat" ? 7 : 6) || left[^1].Plays != (mode == "repeat" ? 2 : 1)
                || left[^1].ExhaustCosts.Length != (mode == "repeat" ? 2 : 1))
                throw new InvalidOperationException("Whistle comparison omitted real card or turn boundaries.");
            results.Add(new { id = seed + ":" + mode, status = same ? "passed" : "diverged", left, right });
        }
        var report = new { schema = "dsl-migration/whistle@1", scope = "controlled-component", reference,
            package_digest = package.Digest, status = passed ? "passed" : "diverged", independent_verified = passed,
            normal_run_verified = false,
            controlled_setup = "Both sides use empty permanent decks and real combat start, turns, paid Whistle and stun. Enemies are FuzzyWurmCrawler or TwigSlimeM. After birth HP is explicitly normalized to 1000 (33 for lethal target), player HP to 1000. HP generation and Niche stream are outside this component comparison; no RNG is reset or compensated. Costs, damage, exhaust, state log, selected moves, Strength, player resources, complete monster_ai state/cursor and shuffle cursor are compared. No normal event/Run certification.",
            cases = results };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }

    private static Boundary[] Dsl(DslPackage package, string seed, string mode)
    {
        var s = package.CreateSession(1, ["player"], seed); s.Start();
        InstanceId[] All(string name) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + name).Id).ToArray();
        Value Field(InstanceId id, string name, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + name).FieldIndex(field));
        void Set(InstanceId id, string name, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + name).FieldIndex(field), value));
        InstanceId[] Pile(string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id).ToArray();
        void Submit(string name, params InstanceId[] parameters)
        {
            var action = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
            s.Submit(action.Definition, s.Participant(0), parameters.Select(Value.OfRef).ToArray());
            if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Whistle DSL stopped: " + s.Fault);
        }
        var player = All("Player").Single();
        Set(player, "Player", "UsesRunDeck", Value.Of(true)); Set(player, "Player", "AutoDrawPerTurn", Value.Of(5L));
        Set(player, "Player", "MaxHp", Value.Of(1000L)); Set(player, "Player", "Hp", Value.Of(1000L));
        Submit(mode == "random" ? "TwigSlimeM.Spawn" : "FuzzyWurmCrawler.Spawn");
        if (mode is "two" or "lethal") Submit("FuzzyWurmCrawler.Spawn");
        var enemies = All("Enemy");
        foreach (var e in enemies) { Set(e, "Enemy", "MaxHp", Value.Of(1000L)); Set(e, "Enemy", "Hp", Value.Of(1000L)); }
        if (mode == "lethal") Set(enemies[0], "Enemy", "Hp", Value.Of(33L));
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        void Turn() { Submit("EndDevelopmentTurn"); Submit("BeginDevelopmentTurn"); }
        for (var turn = 0; turn < (mode == "pending-attack" ? 2 : mode is "pending-buff" or "random" ? 1 : 0); turn++) Turn();
        Submit("Whistle.AddToHand"); var whistle = Pile("Hand").Single(c => Field(c, "Card", "DefinitionId").AsString() == "base-spire:whistle");
        if (mode == "upgraded") Submit("UpgradeCardInHand", whistle);
        var states = new List<Boundary> { Snapshot("before") };
        Submit("PlayAttackFromHand", whistle); states.Add(Snapshot("played"));
        if (mode == "repeat")
        {
            Submit("Whistle.AddToHand"); Set(player, "Player", "Energy", Value.Of(3L));
            Submit("PlayAttackFromHand", Pile("Hand").Single()); states.Add(Snapshot("repeated"));
        }
        for (var turn = 1; turn <= 4; turn++) { Turn(); states.Add(Snapshot("turn:" + turn)); }
        return states.ToArray();

        Boundary Snapshot(string at)
        {
            var random = s.Random.Capture();
            var rng = random.Streams.SingleOrDefault(r => r.Name == "base-spire:run:monster_ai");
            // The DSL initializes named streams lazily; project untouched state
            // from its actual configured seed without creating or drawing a stream.
            var words = rng?.BackendState ?? new Xoshiro256StarStar(random.ExplicitSeeds["base-spire:run:monster_ai"], false).Capture();
            return new(at, enemies.Select(e =>
            {
                var state = All("EnemyMoveState").Single(st => Field(st, "EnemyMoveState", "Enemy").AsRef() == e);
                return new Monster(Field(e, "Enemy", "Hp").AsInt(), All("Power").Where(p => Field(p, "Power", "HolderEnemy").Kind == ValueKind.Ref && Field(p, "Power", "HolderEnemy").AsRef() == e
                        && Field(p, "Power", "DefinitionId").AsString() == "base-spire:strength-power").Sum(p => Field(p, "Power", "Stacks").AsInt()),
                    Field(e, "Enemy", "MoveId").AsString(), Field(e, "Enemy", "Intent").AsString(),
                    All("EnemyMoveHistory").Where(h => Field(h, "EnemyMoveHistory", "State").AsRef() == state)
                        .OrderBy(h => Field(h, "EnemyMoveHistory", "Sequence").AsInt()).Select(h => Field(Field(h, "EnemyMoveHistory", "Node").AsRef(), "EnemyMoveNode", "Id").AsString()).ToArray());
            }).ToArray(), Field(player, "Player", "Hp").AsInt(), Field(player, "Player", "Energy").AsInt(), Field(player, "Player", "PlaySequence").AsInt(),
                Pile("Hand").Where(c => Field(c, "Card", "DefinitionId").AsString() == "base-spire:whistle").Select(c => Field(c, "Card", "Cost").AsInt()).ToArray(),
                Pile("ExhaustPile").Select(c => Field(c, "Card", "Cost").AsInt()).ToArray(), Field(player, "Player", "CombatActive").AsBool(),
                s.Random.DrawCount("base-spire:run:monster_ai"), words.ToArray(), s.Random.DrawCount("base-spire:run:shuffle"));
        }
    }

    private static Boundary[] Native(string seed, string mode)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered();
        var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var c in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(c);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000);
        var combat = new CombatState(run);
        combat.AddMonster((MonsterModel)(mode == "random" ? ModelDb.Monster<TwigSlimeM>().MutableClone() : ModelDb.Monster<FuzzyWurmCrawler>().MutableClone()), CombatSide.Enemy);
        if (mode is "two" or "lethal") combat.AddMonster((MonsterModel)ModelDb.Monster<FuzzyWurmCrawler>().MutableClone(), CombatSide.Enemy);
        var enemies = combat.SpawnedEnemies.ToArray();
        foreach (var enemy in enemies) { enemy.SetMaxHpInternal(1000); enemy.SetCurrentHpInternal(1000); }
        if (mode == "lethal") enemies[0].SetCurrentHpInternal(33);
        var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult();
        void Turn() => engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
        for (var turn = 0; turn < (mode == "pending-attack" ? 2 : mode is "pending-buff" or "random" ? 1 : 0); turn++) Turn();
        var whistle = Add(); if (mode == "upgraded") whistle.Upgrade();
        var states = new List<Boundary> { Snapshot("before") };
        engine.PlayCardAsync(player, whistle, enemies[0]).GetAwaiter().GetResult(); states.Add(Snapshot("played"));
        if (mode == "repeat")
        {
            whistle = Add(); player.PlayerCombatState!.Energy = 3;
            engine.PlayCardAsync(player, whistle, enemies[0]).GetAwaiter().GetResult(); states.Add(Snapshot("repeated"));
        }
        for (var turn = 1; turn <= 4; turn++) { Turn(); states.Add(Snapshot("turn:" + turn)); }
        return states.ToArray();
        CardModel Add()
        {
            var card = (CardModel)ModelDb.Card<Whistle>().MutableClone(); card.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(card); return card;
        }
        Boundary Snapshot(string at)
        {
            var rng = run.Rng.MonsterAi.ToSerializable(); var p = player.PlayerCombatState!;
            return new(at, enemies.Select(e => new Monster(e.CurrentHp, (long)(e.GetPower<StrengthPower>()?.Amount ?? 0), e.Monster!.NextMove!.Id,
                string.Join(",", e.Monster.NextMove.Intents.Select(i => i.IntentType.ToString().ToLowerInvariant() is var kind && kind == "statuscard" ? "status" : kind)),
                e.Monster.MoveStateMachine!.StateLog.Select(st => st.Id).ToArray())).ToArray(), player.Creature.CurrentHp, p.Energy, p.CardsPlayedThisCombat,
                p.Hand.Cards.Where(c => c is Whistle).Select(c => (long)c.EnergyCost).ToArray(), p.ExhaustPile.Cards.Select(c => (long)c.EnergyCost).ToArray(), engine.IsInProgress,
                rng.counter, [rng.state0, rng.state1, rng.state2, rng.state3], run.Rng.Shuffle.Counter);
        }
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }
}
