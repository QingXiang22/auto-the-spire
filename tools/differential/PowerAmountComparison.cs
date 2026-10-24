using System.Text.Json;
using System.Text.RegularExpressions;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class PowerAmountComparison
{
    private sealed record Step(string Command, int Amount = 0);
    private sealed record Plan(string Name, string Definition, Step[] Steps);
    private sealed record Power(string Definition, long Amount, string Type, string StackType, bool SkipTick, string Applier);
    private sealed record Boundary(string At, Power[] Player, Power[] Enemy, long PlayerHp, long EnemyHp, long AiDraws, ulong[] AiState, long ShuffleDraws, ulong[] ShuffleState);
    private static Step A(int amount) => new("apply", amount);
    private static Step M(int amount) => new("modify", amount);
    private static Step Artifact(int amount = 2) => new("artifact", amount);
    private static readonly Plan[] Plans = [
        new("new-debuff", "weak-power", [Artifact(), A(2)]),
        new("stack-debuff", "weak-power", [A(2), Artifact(), A(3)]),
        new("modify-debuff", "weak-power", [A(2), Artifact(), M(3)]),
        new("negative-strength", "strength-power", [Artifact(), A(-2)]),
        new("positive-strength", "strength-power", [Artifact(), A(2)]),
        new("new-zero", "weak-power", [Artifact(), A(0)]),
        new("existing-zero", "weak-power", [A(2), Artifact(), M(0)]),
        new("remove-debuff", "weak-power", [A(2), Artifact(), M(-2)]),
        new("negative-offset", "strength-power", [A(2), Artifact(), M(-4)]),
        new("negative-stack", "strength-power", [A(2), Artifact(), A(-4)]),
        new("negative-debuff", "weak-power", [Artifact(), A(-2)]),
        new("single-debuff", "no-draw-power", [Artifact(), A(1)]),
        new("single-buff-stack", "corruption-power", [A(1), A(1), M(1)]),
        new("blocked-mangle", "mangle-power", [Artifact(), A(3)]),
        new("artifact-exhaust", "weak-power", [Artifact(1), A(2), A(2)]),
        new("flex", "flex-potion-power", [A(2), A(3), M(-1), new("expire")]),
        new("setup-strike", "setup-strike-power", [A(2), A(3), M(-1), new("expire")]),
        new("mangle", "mangle-power", [A(2), A(3), M(-1), new("expire")]),
        new("flex-artifact-expiry", "flex-potion-power", [A(3), Artifact(), new("expire")]),
        new("setup-artifact-expiry", "setup-strike-power", [A(3), Artifact(), new("expire")]),
        new("mangle-artifact-expiry", "mangle-power", [A(3), Artifact(), new("expire")]),
        new("independent", "probe:independent", [A(2), A(2), M(1)]),
        new("per-applier", "probe:per-applier", [A(2), A(2), new("monster"), A(2), M(1)]),
        new("source", "weak-power", [new("card"), new("monster"), A(2), A(1), M(-1)])
    ];

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use power-amount-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        using var probe = new Probe(repository);
        var package = DslPackage.Load(Path.Combine(probe.Root, "package.json"));
        var cases = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" })
        foreach (var enemy in new[] { false, true })
        foreach (var plan in Plans)
        {
            var left = Dsl(package, seed, enemy, plan); var right = Native(seed, enemy, plan);
            if (left.Length != plan.Steps.Length + 1 || right.Length != left.Length) throw new InvalidOperationException("Power comparison omitted command boundaries.");
            var same = JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right); passed &= same;
            cases.Add(new { id = seed + ":" + (enemy ? "enemy:" : "player:") + plan.Name, status = same ? "passed" : "diverged", left, right });
        }
        var report = new { schema = "dsl-migration/power-amounts@1", reference,
            package_digest = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json")).Digest, probe_digest = package.Digest,
            status = passed ? "passed" : "diverged", independent_verified = passed, normal_run_verified = false, scope = "controlled-component",
            controlled_setup = "Both sides start real FuzzyWurmCrawler combat with empty permanent decks and normalize HP to 1000. Test-only callers invoke production Power operations; source remains unchanged. Compare ordered per-holder inventories, amounts, source type/stack metadata, duration grace, actual appliers and complete monster_ai/shuffle states after every command. Birth HP/niche and full hook order are outside scope. Expiry explicitly invokes each side's existing side-end notification. Probe instance definitions compare only instance policy against real FlankingPower/StranglePower, not their card/damage behaviors. No normal Run certification.", cases };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }

    private static Boundary[] Dsl(DslPackage package, string seed, bool enemy, Plan plan)
    {
        var s = package.CreateSession(1, ["player"], seed); s.Start();
        InstanceId[] All(string type) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId i, string type, string field) => s.State.GetField(i, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        void Set(InstanceId i, string type, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(i, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
        void Submit(string name) { var a = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name); s.Submit(a.Definition, s.Participant(0), []); if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Power DSL stopped: " + s.Fault); }
        var player = All("Player").Single(); Set(player, "Player", "UsesRunDeck", Value.Of(true)); Set(player, "Player", "AutoDrawPerTurn", Value.Of(5L));
        Set(player, "Player", "MaxHp", Value.Of(1000L)); Set(player, "Player", "Hp", Value.Of(1000L));
        Submit("FuzzyWurmCrawler.Spawn"); var target = All("Enemy").Single(); Set(target, "Enemy", "MaxHp", Value.Of(1000L)); Set(target, "Enemy", "Hp", Value.Of(1000L));
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn"); Submit("AmountInitialize");
        var probe = All("AmountProbe").Single();
        Set(probe, "AmountProbe", "Definition", Value.Of(plan.Definition.StartsWith("probe:", StringComparison.Ordinal) ? plan.Definition : "base-spire:" + plan.Definition));
        if (enemy) { Set(probe, "AmountProbe", "Player", Value.None); Set(probe, "AmountProbe", "Enemy", Value.OfRef(target)); }
        var snapshots = new List<Boundary> { Snapshot("start") };
        for (var index = 0; index < plan.Steps.Length; index++)
        {
            var step = plan.Steps[index]; Set(probe, "AmountProbe", "Amount", Value.Of((long)step.Amount));
            switch (step.Command)
            {
                case "apply": Submit("AmountApply"); break;
                case "modify": Submit("AmountModify"); break;
                case "artifact": Set(probe, "AmountProbe", "Artifact", Value.Of((long)step.Amount)); Submit("AmountArtifact"); break;
                case "expire": Submit("AmountExpire"); break;
                case "monster": Set(probe, "AmountProbe", "MonsterApplier", Value.Of(true)); break;
                case "card": Submit("Strike.AddToHand"); Set(probe, "AmountProbe", "Card", Value.OfRef(s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id).Single())); break;
                default: throw new InvalidOperationException("Unknown Power comparison step.");
            }
            snapshots.Add(Snapshot(index + ":" + step.Command));
        }
        return snapshots.ToArray();
        Power[] Powers(bool foe) => All("Power").Where(p => Field(p, "Power", foe ? "HolderEnemy" : "HolderPlayer") == Value.OfRef(foe ? target : player)).Select(p =>
        {
            var entry = Field(p, "Power", "Definition").AsRef(); var id = Field(p, "Power", "DefinitionId").AsString();
            return new Power(id.StartsWith("base-spire:", StringComparison.Ordinal) ? id[11..] : id, Field(p, "Power", "Stacks").AsInt(), Field(entry, "PowerBlueprint", "PowerType").AsString(), Field(entry, "PowerBlueprint", "StackType").AsString(), Field(p, "Power", "SkipTick").AsBool(),
                !Field(p, "Power", "ApplierPlayer").IsNone ? "player" : !Field(p, "Power", "ApplierEnemy").IsNone ? "enemy" : "none");
        }).ToArray();
        Boundary Snapshot(string at)
        {
            foreach (var type in new[] { "PowerAmountChange", "PowerAmountModifier" }) if (All(type).Length != 0) throw new InvalidOperationException("Power command leaked " + type);
            var hash = s.State.StateHash(); var version = s.StateVersion; var draws = s.Random.TotalDraws;
            s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
            if (hash != s.State.StateHash() || version != s.StateVersion || draws != s.Random.TotalDraws) throw new InvalidOperationException("Power public query changed state.");
            ulong[] Words(string name) { var rng = s.Random.Capture(); return (rng.Streams.SingleOrDefault(x => x.Name == name)?.BackendState ?? new Xoshiro256StarStar(rng.ExplicitSeeds[name], false).Capture()).ToArray(); }
            return new(at, Powers(false), Powers(true), Field(player, "Player", "Hp").AsInt(), Field(target, "Enemy", "Hp").AsInt(), s.Random.DrawCount("base-spire:run:monster_ai"), Words("base-spire:run:monster_ai"), s.Random.DrawCount("base-spire:run:shuffle"), Words("base-spire:run:shuffle"));
        }
    }

    private static Boundary[] Native(string seed, bool enemy, Plan plan)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered(); var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000);
        var combat = new CombatState(run); combat.AddMonster((MonsterModel)ModelDb.Monster<FuzzyWurmCrawler>().MutableClone(), CombatSide.Enemy);
        var target = combat.SpawnedEnemies.Single(); target.SetMaxHpInternal(1000); target.SetCurrentHpInternal(1000); var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult();
        var holder = enemy ? target : player.Creature; Creature applier = player.Creature; PowerModel? current = null; CardModel? cardSource = null;
        var snapshots = new List<Boundary> { Snapshot("start") };
        for (var index = 0; index < plan.Steps.Length; index++)
        {
            var step = plan.Steps[index];
            switch (step.Command)
            {
                case "apply": current = PowerCmd.Apply(combat, PowerClass(plan.Definition), holder, step.Amount, applier, cardSource).GetAwaiter().GetResult(); break;
                case "modify": PowerCmd.ModifyAmount(combat, current!, step.Amount, applier, cardSource).GetAwaiter().GetResult(); break;
                case "artifact": PowerCmd.Apply<ArtifactPower>(combat, holder, step.Amount, player.Creature, null).GetAwaiter().GetResult(); break;
                case "expire": Hook.AfterSideTurnEnd(combat, enemy ? CombatSide.Enemy : CombatSide.Player, [holder]).GetAwaiter().GetResult(); break;
                case "monster": applier = target; break;
                case "card": cardSource = (CardModel)ModelDb.Card<StrikeIronclad>().MutableClone(); cardSource.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(cardSource); break;
                default: throw new InvalidOperationException("Unknown Power reference step.");
            }
            snapshots.Add(Snapshot(index + ":" + step.Command));
        }
        return snapshots.ToArray();
        static Type PowerClass(string name) => name switch {
            "weak-power" => typeof(WeakPower), "strength-power" => typeof(StrengthPower), "no-draw-power" => typeof(NoDrawPower), "corruption-power" => typeof(CorruptionPower),
            "flex-potion-power" => typeof(FlexPotionPower), "setup-strike-power" => typeof(SetupStrikePower), "mangle-power" => typeof(ManglePower),
            "probe:independent" => typeof(FlankingPower), "probe:per-applier" => typeof(StranglePower), _ => throw new InvalidOperationException("Unknown source Power type.") };
        Power[] Powers(Creature owner) => owner.Powers.Select(p => new Power(p is FlankingPower ? "probe:independent" : p is StranglePower ? "probe:per-applier" : Regex.Replace(p.GetType().Name, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant(), p.Amount, p.Type.ToString().ToLowerInvariant(), p.StackType.ToString().ToLowerInvariant(), p.SkipNextDurationTick,
            ReferenceEquals(p.Applier, player.Creature) ? "player" : ReferenceEquals(p.Applier, target) ? "enemy" : "none")).ToArray();
        Boundary Snapshot(string at)
        {
            var ai = run.Rng.MonsterAi.ToSerializable(); var shuffle = run.Rng.Shuffle.ToSerializable();
            return new(at, Powers(player.Creature), Powers(target), player.Creature.CurrentHp, target.CurrentHp, ai.counter, [ai.state0, ai.state1, ai.state2, ai.state3], shuffle.counter, [shuffle.state0, shuffle.state1, shuffle.state2, shuffle.state3]);
        }
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }

    private sealed class Probe : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "power-amounts-" + Guid.NewGuid());
        internal Probe(string repository)
        {
            var original = Path.Combine(repository, "mod-the-spire/base-spire");
            foreach (var source in Directory.GetFiles(original, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(Root, Path.GetRelativePath(original, source)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            var entry = Path.Combine(Root, "base.spire"); File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"power-amount-probe.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "power-amount-probe.spire"), "using namespace base-spire;\ndsl_version = \"core/0.3\"\n" + File.ReadAllText(Path.Combine(repository, "tools/base-spire-dev/fixtures/power-amount-probe.spire")));
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root);
            if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(path).StartsWith("power-amounts-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected Power probe cleanup path.");
            Directory.Delete(path, true);
        }
    }
}
