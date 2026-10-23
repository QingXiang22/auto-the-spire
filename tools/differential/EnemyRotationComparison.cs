using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class EnemyRotationComparison
{
    private static readonly string[] Names = ["AssassinRubyRaider", "AxeRubyRaider", "BowlbugEgg", "BruteRubyRaider", "CrossbowRubyRaider", "Stabbot", "TrackerRubyRaider"];
    private sealed record Monster(long Hp, long Block, long Strength, long Vigor, string Move, string Intent, string[] History);
    private sealed record Hit(bool Player, long Blocked, long Unblocked, long Overkill, bool Killed);
    private sealed record Boundary(Monster[] Enemies, long Hp, long Frail, Hit[] Hits, bool Active, string Outcome, long AiDraws, ulong[] AiState);

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output"))
            throw new FormatException("Use enemy-rotation-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        using var probe = new Probe(Path.Combine(repository, "mod-the-spire/base-spire"));
        var package = DslPackage.Load(Path.Combine(probe.Root, "package.json"));
        var productionDigest = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json")).Digest;
        var cases = new List<object>();
        var passed = true;
        foreach (var seed in new[] { "777", "A😀B" })
        foreach (var name in Names)
        foreach (var mode in new[] { "rotation", "stun", "two" })
            Compare(seed, name, mode);
        foreach (var seed in new[] { "777", "A😀B" })
        foreach (var mode in new[] { "vigor", "retaliation", "lethal", "lethal-clean" })
            Compare(seed, "TrackerRubyRaider", mode);
        var report = new
        {
            schema = "dsl-migration/enemy-rotations@1", reference, package_digest = productionDigest, probe_digest = package.Digest,
            status = passed ? "passed" : "diverged", independent_verified = passed, normal_run_verified = false,
            scope = "controlled-component",
            controlled_setup = "Real source monsters, empty permanent decks and ordinary turns. Enemy HP is explicitly normalized to 1000, player HP to 1000 (100 for Vigor, 3 for lethal); Tracker receives Vigor3, retaliation additionally gives player Thorns4 and Tracker HP3. Stun uses the shared command without a card. HP generation and niche are outside this comparison. Test-only actions call production operations. No reference source changes, RNG resets or compensation. Stable boundaries compare ordered moves/intents/history, HP/block, Strength/Vigor/Frail, per-target hit sequences and full monster_ai state/cursor. DSL attack-count and event-boundary checks run separately.",
            cases
        };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json);
        return passed ? 0 : 1;

        void Compare(string seed, string name, string mode)
        {
            var left = Dsl(package, seed, name, mode);
            var right = Native(seed, name, mode);
            var same = JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
            if (left.Length != (mode is "vigor" or "retaliation" or "lethal" or "lethal-clean" ? 3 : 5))
                throw new InvalidOperationException("Enemy rotation comparison omitted required turn boundaries.");
            passed &= same;
            cases.Add(new { id = seed + ":" + name + ":" + mode, status = same ? "passed" : "diverged", left, right });
        }
    }

    private static Boundary[] Dsl(DslPackage package, string seed, string name, string mode)
    {
        var session = package.CreateSession(1, ["player"], seed); session.Start();
        InstanceId[] All(string type) => session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId instance, string type, string field) => session.State.GetField(instance, session.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        void Set(InstanceId instance, string type, string field, Value value) => session.State.RunAtomic(() => session.State.SetField(instance, session.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
        void Submit(string action)
        {
            var legal = session.LegalActions(session.Participant(0)).Single(candidate => candidate.Definition.QualifiedId == "base-spire:" + action);
            session.Submit(legal.Definition, session.Participant(0), []);
            if (session.Status != SessionStatus.Running) throw new InvalidOperationException("Enemy rotation DSL stopped: " + session.Fault);
        }
        var player = All("Player").Single();
        Set(player, "Player", "UsesRunDeck", Value.Of(true)); Set(player, "Player", "AutoDrawPerTurn", Value.Of(5L));
        Set(player, "Player", "MaxHp", Value.Of(1000L)); Set(player, "Player", "Hp", Value.Of(1000L));
        Submit(name + ".Spawn"); if (mode is "two" or "retaliation") Submit(name + ".Spawn");
        var enemies = All("Enemy");
        foreach (var enemy in enemies) { Set(enemy, "Enemy", "Hp", Value.Of(1000L)); Set(enemy, "Enemy", "MaxHp", Value.Of(1000L)); }
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        var snapshots = new List<Boundary> { Snapshot() };
        var turns = mode is "vigor" or "retaliation" or "lethal" or "lethal-clean" ? 2 : 4;
        for (var turn = 0; turn < turns; turn++)
        {
            if (mode == "stun" && turn == 0) Submit("RotationStun");
            if (turn == 1 && mode is "vigor" or "retaliation" or "lethal")
            {
                Submit("RotationVigor");
                Set(player, "Player", "Hp", Value.Of(mode == "lethal" ? 3L : 100L));
                if (mode == "retaliation")
                {
                    Set(enemies[0], "Enemy", "Hp", Value.Of(3L));
                    Submit("RotationThorns");
                }
            }
            if (turn == 1 && mode == "lethal-clean") { Submit("RotationRemoveFrail"); Set(player, "Player", "Hp", Value.Of(3L)); }
            Submit("EndDevelopmentTurn");
            if (Field(player, "Player", "CombatActive").AsBool()) Submit("BeginDevelopmentTurn");
            snapshots.Add(Snapshot());
        }
        return snapshots.ToArray();

        long Power(string definition, InstanceId? enemy = null) => All("Power").Where(power => Field(power, "Power", "DefinitionId").AsString() == "base-spire:" + definition
            && (enemy is null ? Field(power, "Power", "HolderPlayer") == Value.OfRef(player) : Field(power, "Power", "HolderEnemy") == Value.OfRef(enemy.Value)))
            .Sum(power => Field(power, "Power", "Stacks").AsInt());
        Boundary Snapshot()
        {
            var random = session.Random.Capture();
            var stream = random.Streams.SingleOrDefault(candidate => candidate.Name == "base-spire:run:monster_ai");
            var words = stream?.BackendState ?? new Xoshiro256StarStar(random.ExplicitSeeds["base-spire:run:monster_ai"], false).Capture();
            return new(enemies.Select(enemy =>
            {
                var state = All("EnemyMoveState").Single(candidate => Field(candidate, "EnemyMoveState", "Enemy").AsRef() == enemy);
                return new Monster(Field(enemy, "Enemy", "Hp").AsInt(), Field(enemy, "Enemy", "Block").AsInt(), Power("strength-power", enemy), Power("vigor-power", enemy),
                    Field(enemy, "Enemy", "MoveId").AsString(), Field(enemy, "Enemy", "Intent").AsString(),
                    All("EnemyMoveHistory").Where(history => Field(history, "EnemyMoveHistory", "State").AsRef() == state)
                        .OrderBy(history => Field(history, "EnemyMoveHistory", "Sequence").AsInt())
                        .Select(history => Field(Field(history, "EnemyMoveHistory", "Node").AsRef(), "EnemyMoveNode", "Id").AsString()).ToArray());
            }).ToArray(), Field(player, "Player", "Hp").AsInt(), Power("frail-power"),
                All("DamageResult").Select(result => new Hit(!Field(result, "DamageResult", "Player").IsNone,
                    Field(result, "DamageResult", "Blocked").AsInt(), Field(result, "DamageResult", "Unblocked").AsInt(),
                    Field(result, "DamageResult", "Overkill").AsInt(), Field(result, "DamageResult", "Killed").AsBool())).OrderBy(hit => hit.Player).ToArray(),
                Field(player, "Player", "CombatActive").AsBool(), Field(player, "Player", "CombatOutcome").AsString(),
                session.Random.DrawCount("base-spire:run:monster_ai"), words.ToArray());
        }
    }

    private static Boundary[] Native(string seed, string name, string mode)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered();
        var run = new RunState(seed, new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000);
        var combat = new CombatState(run);
        var model = (MonsterModel)ModelDb.GetById<MonsterModel>(ModelDb.GetId(typeof(TrackerRubyRaider).Assembly.GetType("Sts2Sim.Core.Models.Monsters." + name)!));
        combat.AddMonster((MonsterModel)model.MutableClone(), CombatSide.Enemy);
        if (mode is "two" or "retaliation") combat.AddMonster((MonsterModel)model.MutableClone(), CombatSide.Enemy);
        var enemies = combat.SpawnedEnemies.ToArray();
        foreach (var enemy in enemies) { enemy.SetMaxHpInternal(1000); enemy.SetCurrentHpInternal(1000); }
        var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult();
        var snapshots = new List<Boundary> { Snapshot() };
        var turns = mode is "vigor" or "retaliation" or "lethal" or "lethal-clean" ? 2 : 4;
        for (var turn = 0; turn < turns; turn++)
        {
            if (mode == "stun" && turn == 0) CreatureCmd.Stun(enemies[0]).GetAwaiter().GetResult();
            if (turn == 1 && mode is "vigor" or "retaliation" or "lethal")
            {
                PowerCmd.Apply<VigorPower>(combat, enemies[0], 3, enemies[0], null).GetAwaiter().GetResult();
                player.Creature.SetCurrentHpInternal(mode == "lethal" ? 3 : 100);
                if (mode == "retaliation")
                {
                    enemies[0].SetCurrentHpInternal(3);
                    PowerCmd.Apply<ThornsPower>(combat, player.Creature, 4, player.Creature, null).GetAwaiter().GetResult();
                }
            }
            if (turn == 1 && mode == "lethal-clean")
            {
                PowerCmd.Remove(player.Creature.GetPower<FrailPower>()!).GetAwaiter().GetResult();
                player.Creature.SetCurrentHpInternal(3);
            }
            engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
            snapshots.Add(Snapshot());
        }
        return snapshots.ToArray();
        Boundary Snapshot()
        {
            var random = run.Rng.MonsterAi.ToSerializable();
            return new(enemies.Select(enemy => new Monster(enemy.CurrentHp, enemy.Block, enemy.GetPower<StrengthPower>()?.Amount ?? 0,
                    enemy.GetPower<VigorPower>()?.Amount ?? 0, enemy.Monster!.NextMove!.Id,
                    string.Join(',', enemy.Monster.NextMove.Intents.Select(intent => intent.IntentType.ToString().ToLowerInvariant())),
                    enemy.Monster.MoveStateMachine!.StateLog.Select(state => state.Id).ToArray())).ToArray(),
                player.Creature.CurrentHp, player.Creature.GetPower<FrailPower>()?.Amount ?? 0,
                combat.DamageHistory.Entries.Select(entry => new Hit(entry.Receiver == player.Creature, entry.Result.BlockedDamage,
                    entry.Result.UnblockedDamage, entry.Result.OverkillDamage, entry.Result.WasTargetKilled)).OrderBy(hit => hit.Player).ToArray(),
                engine.IsInProgress, engine.IsInProgress ? "none" : engine.Won ? "victory" : "defeat",
                random.counter, [random.state0, random.state1, random.state2, random.state3]);
        }
#else
        throw new NotSupportedException("Independent locked reference is not available.");
#endif
    }

    private sealed class Probe : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "enemy-rotations-" + Guid.NewGuid());
        internal Probe(string original)
        {
            foreach (var source in Directory.GetFiles(original, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(Root, Path.GetRelativePath(original, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            var entry = Path.Combine(Root, "base.spire");
            File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"rotation-probes.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "rotation-probes.spire"), """
                using namespace base-spire;
                dsl_version = "core/0.3"
                action RotationStun { role = player body = { call = { procedure = StunEnemy args = { target = first(type(Enemy)) } } } }
                action RotationVigor { role = player body = { call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:vigor-power" stacks = 3 } } } }
                action RotationThorns { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:thorns-power" stacks = 4 } } } }
                action RotationRemoveFrail { role = player body = {
                  forEach = { query = where(collection(Powers), power, and(equal(field(power, Power.HolderPlayer), first(type(Player))), equal(field(power, Power.DefinitionId), "base-spire:frail-power"))) item = power body = { call = { procedure = RemovePower args = { power = power } } } }
                } }
                """);
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root);
            if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(path).StartsWith("enemy-rotations-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected probe cleanup path.");
            Directory.Delete(path, true);
        }
    }
}
