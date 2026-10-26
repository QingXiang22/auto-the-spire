using System.Reflection;
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

internal static class DamageComparison
{
    private sealed record Plan(string Name, int Strength = 0, int Vigor = 0, bool Weak = false,
        bool Vulnerable = false, int Cruelty = 0, long Block = 0, long PlayerBlock = 0, int Hp = 1000);
    private static readonly Plan[] Plans = [
        new("plain"), new("negative-strength-vigor", Strength: -10, Vigor: 5),
        new("negative-total", Strength: -10), new("negative-strength-vigor-weak-vulnerable", Strength: -10, Vigor: 5, Weak: true, Vulnerable: true),
        new("fraction", Strength: 1, Weak: true, Vulnerable: true),
        new("fraction-fully-blocked", Strength: 1, Weak: true, Vulnerable: true, Block: 7),
        new("fraction-block-remaining", Strength: 1, Weak: true, Vulnerable: true, Block: 9),
        new("fraction-broken", Strength: 1, Weak: true, Vulnerable: true, Block: 3),
        new("zero-blocked", Strength: -6, Block: 3), new("overkill", Hp: 2),
        new("body-slam-large", PlayerBlock: 999999999, Vulnerable: true),
        new("body-slam-large-chain", PlayerBlock: 999999999, Strength: 999, Vigor: 999, Weak: true, Vulnerable: true, Cruelty: 999),
    ];
    private sealed record Row(long Blocked, long Unblocked, long Overkill, bool BlockBroken, bool FullyBlocked, bool Killed);
    private sealed record Result(long Hp, long Block, long Energy, Row[] Results);

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use damage-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        var original = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        using var fixture = new Fixture(original.Root);
        var package = DslPackage.Load(Path.Combine(fixture.Root, "package.json"));
        var cases = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" }) foreach (var plan in Plans)
        {
            var left = Dsl(package, seed, plan); var right = Native(seed, plan);
            var same = JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right); passed &= same;
            cases.Add(new { id = seed + ":" + plan.Name, status = same ? "passed" : "diverged", left, right });
        }
        var report = new { schema = "dsl-migration/damage@1", reference, package_digest = original.Digest,
            fixture_digest = package.Digest, status = passed ? "passed" : "diverged", independent_verified = passed,
            normal_run_verified = false, scope = "controlled-card-damage-results", executed = cases.Count,
            expected_case_ids = new[] { "777", "A😀B" }.SelectMany(s => Plans.Select(p => s + ":" + p.Name)), cases };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }

    private static Result Dsl(DslPackage package, string seed, Plan plan)
    {
        var s = package.CreateSession(1, ["player"], seed); s.Start();
        InstanceId[] All(string type) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        void Set(InstanceId id, string type, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
        void Submit(string name, params Value[] args)
        {
            s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), s.Participant(0), args);
            if (s.Status != SessionStatus.Running) throw new InvalidOperationException(name + ": " + s.Fault);
        }
        var player = All("Player").Single(); Set(player, "Player", "UsesRunDeck", Value.Of(true));
        Submit("FuzzyWurmCrawler.Spawn"); var enemy = All("Enemy").Single();
        Set(enemy, "Enemy", "MaxHp", Value.Of(1000L)); Set(enemy, "Enemy", "Hp", Value.Of(1000L));
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        void Power(string id, int amount, bool target = false) { if (amount != 0) Submit("DamageProbePower", Value.Of(target), Value.Of("base-spire:" + id), Value.Of((long)amount)); }
        Power("strength-power", plan.Strength); Power("vigor-power", plan.Vigor); Power("weak-power", plan.Weak ? 1 : 0);
        Power("vulnerable-power", plan.Vulnerable ? 1 : 0, true); Power("cruelty-power", plan.Cruelty);
        Set(enemy, "Enemy", "Hp", Value.Of((long)plan.Hp)); Set(enemy, "Enemy", "Block", Value.Of(plan.Block));
        Set(player, "Player", "Block", Value.Of(plan.PlayerBlock)); Set(player, "Player", "EffectDepth", Value.Of(1L));
        Submit(plan.PlayerBlock > 0 ? "BodySlam.AddToHand" : "Strike.AddToHand");
        Submit("PlayAttackFromHand", Value.OfRef(All("Card").Single()));
        var rows = All("DamageResult").Select(r => new Row(Field(r, "DamageResult", "Blocked").AsInt(), Field(r, "DamageResult", "Unblocked").AsInt(),
            Field(r, "DamageResult", "Overkill").AsInt(), Field(r, "DamageResult", "BlockBroken").AsBool(),
            Field(r, "DamageResult", "FullyBlocked").AsBool(), Field(r, "DamageResult", "Killed").AsBool())).ToArray();
        return new(Field(enemy, "Enemy", "Hp").AsInt(), Field(enemy, "Enemy", "Block").AsInt(), Field(player, "Player", "Energy").AsInt(), rows);
    }

    private static Result Native(string seed, Plan plan)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered();
        var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var c in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(c);
        var combat = new CombatState(run); combat.AddMonster((MonsterModel)ModelDb.Monster<FuzzyWurmCrawler>().MutableClone(), CombatSide.Enemy);
        var enemy = combat.SpawnedEnemies.Single(); enemy.SetMaxHpInternal(1000); enemy.SetCurrentHpInternal(1000);
        var rows = new List<Row>();
        var observer = DispatchProxy.Create(typeof(CombatEngine).Assembly.GetType("Sts2Sim.Core.Combat.ICombatObserver")!, typeof(NormalReferenceObserver));
        ((NormalReferenceObserver)observer).Record = (name, args) => {
            if (name == "DamageResolved" && args is [_, Sts2Sim.Core.Entities.Creatures.DamageResult r])
                rows.Add(new(r.BlockedDamage, r.UnblockedDamage, r.OverkillDamage, r.WasBlockBroken, r.WasFullyBlocked, r.WasTargetKilled));
        };
        var engine = (CombatEngine)Activator.CreateInstance(typeof(CombatEngine), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [combat, observer], null)!;
        engine.StartCombatAsync().GetAwaiter().GetResult();
        void Apply<T>(int amount, bool target = false) where T : PowerModel { if (amount != 0) PowerCmd.Apply<T>(combat, target ? enemy : player.Creature, amount, null, null).GetAwaiter().GetResult(); }
        Apply<StrengthPower>(plan.Strength); Apply<VigorPower>(plan.Vigor); Apply<WeakPower>(plan.Weak ? 1 : 0);
        Apply<VulnerablePower>(plan.Vulnerable ? 1 : 0, true); Apply<CrueltyPower>(plan.Cruelty);
        enemy.SetCurrentHpInternal(plan.Hp); enemy.GainBlockInternal(plan.Block); player.Creature.GainBlockInternal(plan.PlayerBlock);
        CardModel card = (CardModel)(plan.PlayerBlock > 0 ? (CardModel)ModelDb.Card<BodySlam>() : ModelDb.Card<StrikeIronclad>()).MutableClone();
        card.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(card);
        engine.PlayCardAsync(player, card, enemy).GetAwaiter().GetResult();
        return new(enemy.CurrentHp, enemy.Block, player.PlayerCombatState.Energy, rows.ToArray());
#else
        throw new NotSupportedException("Independent reference is unavailable.");
#endif
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "damage-compare-" + Guid.NewGuid());
        internal Fixture(string source)
        {
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) { var target = Path.Combine(Root, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); }
            var entry = Path.Combine(Root, "base.spire"); File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"damage-probe.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "damage-probe.spire"), """
                using namespace base-spire;
                dsl_version = "core/0.3"
                action DamageProbePower { role = player parameters = { enemy = Bool definition = String(length = 64) stacks = Int } body = {
                  if = { condition = enemy then = { call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = definition stacks = stacks } } }
                    else = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = definition stacks = stacks } } } }
                } }
                """);
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root);
            if (Path.GetDirectoryName(path) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || !Path.GetFileName(path).StartsWith("damage-compare-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected damage fixture path.");
            Directory.Delete(path, true);
        }
    }
}
