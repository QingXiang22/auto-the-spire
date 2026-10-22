using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

// A controlled component comparison. Neither side enters BattlewornDummy's event;
// no source changes, RNG resets, injected outcome, or normal-Run certification.
internal static class BattleFriendComparison
{
    private const string Niche = "base-spire:run:niche";
    private sealed record HpCall(long Minimum, long Maximum, long Result, long Before, long After);
    private sealed record Enemy(long Hp, long MaxHp, bool Active, bool Escaped, bool Dead, long Timer, string Move, string Intent);
    private sealed record Boundary(string At, Enemy[] Enemies, bool Active, bool Victory, bool TimedOut, long MaxHp,
        long HpDraws, ulong[] HpState, long AiDraws);

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output"))
            throw new FormatException("Use battle-friend-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        var package = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        var results = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" })
        foreach (var tier in Enumerable.Range(1, 3))
        foreach (var mode in new[] { "timeout", "kill" }) Compare(seed, tier, mode, 1);
        Compare("777", 1, "timeout", 2);
        var report = new { schema = "dsl-migration/battle-friend@1", scope = "controlled-component", reference,
            package_digest = package.Digest, status = passed ? "passed" : "diverged", independent_verified = passed,
            normal_run_verified = false, parent_event_verified = false,
            controlled_setup = "Each side creates real BattleFriend models and executes real turns. Kill cases set target HP=5 immediately before a real Feed play on turn 3. Deck/shuffle and unrelated Run generation are outside this component comparison; niche HP calls/state and AI cursor are compared without reseeding.",
            cases = results };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;

        void Compare(string seed, int tier, string mode, int count)
        {
            var left = Dsl(package, seed, tier, mode, count); var right = Native(seed, tier, mode, count);
            var same = NormalPreflight.Json(left) == NormalPreflight.Json(right); passed &= same;
            if (left.States.Length != 4 || left.Calls.Length != count || left.States[^1].Active || !left.States[^1].Victory
                || left.States[^1].TimedOut != (mode == "timeout") || left.States.Any(s => s.AiDraws != 0))
                throw new InvalidOperationException("Missing BattleFriend execution boundaries or real outcome: " + seed + ":" + tier + ":" + mode);
            results.Add(new { id = seed + ":v" + tier + ":" + mode + ":" + count, status = same ? "passed" : "diverged", left, right });
        }
    }

    private sealed record Trace(Boundary[] States, HpCall[] Calls);
    private static Trace Dsl(DslPackage package, string seed, int tier, string mode, int count)
    {
        var s = package.CreateSession(1, ["player"], seed, new SessionDebugOptions(MaxRecords: 100_000)); s.Start();
        InstanceId[] Instances(string type) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId id, string type, string name) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(name));
        var player = Instances("Player").Single();
        void Submit(string name, params InstanceId[] bindings)
        {
            var action = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
            s.Submit(action.Definition, s.Participant(0), bindings.Select(Value.OfRef).ToArray());
            if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Component DSL stopped: " + s.Fault);
        }
        for (var i = 0; i < count; i++) Submit($"BattleFriendV{tier}.Spawn");
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        var states = new List<Boundary> { Snapshot("start") };
        for (var turn = 1; turn <= 3; turn++)
        {
            if (turn == 3 && mode == "kill")
            {
                s.State.RunAtomic(() => s.State.SetField(Instances("Enemy").Single(), s.RuleSet.TypeByQualifiedId("base-spire:Enemy").FieldIndex("Hp"), Value.Of(5L)));
                Submit("Feed.AddToHand");
                var card = Instances("Card").Single(c => Field(c, "Card", "DefinitionId").AsString() == "base-spire:feed");
                Submit("PlayAttackFromHand", card);
            }
            else
            {
                Submit("EndDevelopmentTurn");
                if (Field(player, "Player", "CombatActive").AsBool()) Submit("BeginDevelopmentTurn");
            }
            states.Add(Snapshot("turn:" + turn));
        }
        if (s.Debug!.DroppedRecords != 0) throw new InvalidOperationException("Component RNG diagnostics truncated.");
        var calls = s.Debug.Capture().Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == Niche).Select(r =>
        {
            var c = r.Random ?? throw new InvalidOperationException("Missing HP call details.");
            if (c.Operation != "int" || c.CandidatesTruncated) throw new InvalidOperationException("Unexpected HP operation.");
            return new HpCall(long.Parse(c.Minimum, CultureInfo.InvariantCulture), long.Parse(c.Maximum, CultureInfo.InvariantCulture),
                long.Parse(c.Result, CultureInfo.InvariantCulture), c.Before, c.After);
        }).ToArray();
        return new(states.ToArray(), calls);

        Boundary Snapshot(string at)
        {
            var rng = s.Random.Capture().Streams.Single(r => r.Name == Niche);
            return new(at, Instances("Enemy").Select(e =>
            {
                var escaped = Field(e, "Enemy", "Escaped").AsBool(); var hp = Field(e, "Enemy", "Hp").AsInt();
                var timer = Instances("Power").Where(p => Field(p, "Power", "HolderEnemy").Kind == ValueKind.Ref && Field(p, "Power", "HolderEnemy").AsRef() == e)
                    .Where(p => Field(p, "Power", "DefinitionId").AsString() == "base-spire:battleworn-dummy-time-limit-power").Sum(p => Field(p, "Power", "Stacks").AsInt());
                return new Enemy(hp, Field(e, "Enemy", "MaxHp").AsInt(), hp > 0 && !escaped, escaped, hp == 0, timer,
                    Field(e, "Enemy", "MoveId").AsString(), Field(e, "Enemy", "Intent").AsString());
            }).ToArray(), Field(player, "Player", "CombatActive").AsBool(), Field(player, "Player", "CombatOutcome").AsString() == "victory",
                Field(player, "Player", "CombatTimedOut").AsBool(), Field(player, "Player", "MaxHp").AsInt(), rng.Draws,
                [rng.S0, rng.S1, rng.S2, rng.S3], s.Random.DrawCount("base-spire:run:monster_ai"));
        }
    }

    private static Trace Native(string seed, int tier, string mode, int count)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered();
        var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        var combat = new CombatState(run); var rng = run.Rng.Niche;
        var calls = new List<HpCall>(); (long Min, long Max) bounds = default;
        var hook = typeof(Rng).GetField("_drawObserver", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var previous = RngDiagnostics.DrawObserver;
        if (previous is not null || hook.GetValue(rng) is not null) throw new InvalidOperationException("RNG observer already owned.");
        RngDiagnostics.DrawObserver = (source, op, min, max, arity) =>
        {
            if (!ReferenceEquals(source, rng)) return;
            if (op != "NextInt" || arity is not (1 or 2)) throw new InvalidOperationException("Unexpected native HP draw.");
            bounds = ((long)min, (long)max);
        };
        hook.SetValue(rng, (Action<int, string, string>)((cursor, op, result) => calls.Add(new(bounds.Min, bounds.Max,
            long.Parse(result, CultureInfo.InvariantCulture), cursor - 1, cursor))));
        try
        {
            for (var i = 0; i < count; i++) combat.AddMonster((MonsterModel)(tier switch
            {
                1 => (MonsterModel)ModelDb.Monster<BattleFriendV1>(), 2 => ModelDb.Monster<BattleFriendV2>(), 3 => ModelDb.Monster<BattleFriendV3>(),
                _ => throw new ArgumentOutOfRangeException(nameof(tier)),
            }).MutableClone(), CombatSide.Enemy);
            var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult();
            var timers = combat.SpawnedEnemies.Select(e => e.GetPower<BattlewornDummyTimeLimitPower>() ?? throw new InvalidOperationException("Source room-entry timer missing.")).ToArray();
            var states = new List<Boundary> { Snapshot("start") };
            for (var turn = 1; turn <= 3; turn++)
            {
                if (turn == 3 && mode == "kill")
                {
                    var target = combat.SpawnedEnemies.Single(); target.SetCurrentHpInternal(5);
                    var feed = (CardModel)ModelDb.Card<Feed>().MutableClone(); feed.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(feed);
                    engine.PlayCardAsync(player, feed, target).GetAwaiter().GetResult();
                    // RunDriver checks the real win condition after each card action.
                    engine.CheckWinCondition();
                }
                else engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
                states.Add(Snapshot("turn:" + turn));
            }
            return new(states.ToArray(), calls.ToArray());

            Boundary Snapshot(string at)
            {
                var state = rng.ToSerializable();
                return new(at, combat.SpawnedEnemies.Select(e => new Enemy(e.CurrentHp, e.MaxHp, e.IsAlive && combat.Enemies.Contains(e),
                    combat.EscapedCreatures.Contains(e), e.IsDead, (long)(e.GetPower<BattlewornDummyTimeLimitPower>()?.Amount ?? 0),
                    e.Monster!.NextMove!.Id, e.Monster.NextMove.Intents.Single().IntentType.ToString().ToLowerInvariant())).ToArray(),
                    engine.IsInProgress, engine.Won, combat.EscapedCreatures.Any(combat.SpawnedEnemies.Contains) || timers.Any(p => p.HasExpired),
                    player.Creature.MaxHp, state.counter, [state.state0, state.state1, state.state2, state.state3],
                    run.Rng.MonsterAi.Counter + combat.SpawnedEnemies.Sum(e => (long)e.Monster!.Rng.Counter));
            }
        }
        finally { hook.SetValue(rng, null); RngDiagnostics.DrawObserver = previous; }
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }
}
