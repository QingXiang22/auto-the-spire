using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class TurnStartComparison
{
    private static readonly string[] Plans = ["clear", "barricade", "clamp", "barricade-clamp", "enemy-barricade", "zero-member", "spawn-skip", "horn-cleat"];
    private sealed record Row(string At, long Turn, long Hp, long Block, long[] EnemyBlock, bool[] Spawned);
    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use turn-start-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        var original = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        using var fixture = new Fixture(original.Root); var package = DslPackage.Load(Path.Combine(fixture.Root, "package.json"));
        var cases = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" }) foreach (var plan in Plans)
        {
            var left = Dsl(package, seed, plan); var right = Native(seed, plan);
            if (left.Length != (plan == "horn-cleat" ? 20 : 12) || right.Length != left.Length) throw new InvalidOperationException("Turn start comparison omitted required stages: " + seed + ":" + plan);
            var same = NormalPreflight.Json(left) == NormalPreflight.Json(right); passed &= same;
            cases.Add(new { id = seed + ":" + plan, status = same ? "passed" : "diverged", left, right });
        }
        var report = new { schema = "dsl-migration/turn-start@1", reference, package_digest = original.Digest, fixture_digest = package.Digest,
            scope = "controlled-component", status = passed ? "passed" : "diverged", independent_verified = passed, normal_run_verified = false,
            setup = "Two real TwigSlimeS with players registered first; both sides explicitly normalize HP1000, empty decks, Block40/player and Block15/enemy after turn one. The real Anchor starts both sides with Block10. Native SturdyClamp is compared with an explicitly tool-only DSL clamp listener attached to Anchor; this certifies the shared veto/callback mechanism, not formal SturdyClamp support. Barricade is production content on both sides. HornCleat is obtained on both sides and runs through turn three: its old TurnNumber1 grants14 on turn two, without repeating on turn three. Zero-member directly sets one HP to zero without death resolution, preserving source side membership. Spawn-skip directly sets the source private SpawnedThisTurn flag and the DSL flag at enemy AfterSideTurnStart; no summon lifecycle is certified. Before/cleared/side/end/configured rows retain both enemy blocks and flags in source order; native EndPlayerTurn automatically starts the next turn and DSL explicitly End+Begin steps. No birth RNG, public inventory equivalence, extra-turn action, summons or normal Run certification.", cases };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }
    private static Row[] Dsl(DslPackage package, string seed, string plan)
    {
        var s = package.CreateSession(1, ["player"], seed); s.Start();
        InstanceId[] All(string type) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        void Set(InstanceId id, string type, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
        void Submit(string name, params Value[] values) { var action = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name); s.Submit(action, s.Participant(0), values); if (s.Status != SessionStatus.Running) throw new InvalidOperationException("DSL start comparison stopped: " + s.Fault); }
        var player = All("Player").Single(); Set(player, "Player", "UsesRunDeck", Value.Of(true)); Set(player, "Player", "MaxHp", Value.Of(1000L)); Set(player, "Player", "Hp", Value.Of(1000L));
        Submit("StartSetPlan", Value.Of(plan)); Submit("Anchor.Obtain"); if (plan == "horn-cleat") Submit("HornCleat.Obtain"); Submit("TwigSlimeS.Spawn"); Submit("TwigSlimeS.Spawn");
        var enemies = All("Enemy"); foreach (var enemy in enemies) { Set(enemy, "Enemy", "MaxHp", Value.Of(1000L)); Set(enemy, "Enemy", "Hp", Value.Of(1000L)); }
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        Set(player, "Player", "Block", Value.Of(40L)); foreach (var e in enemies) Set(e, "Enemy", "Block", Value.Of(15L));
        if (plan == "zero-member") Set(enemies[0], "Enemy", "Hp", Value.Of(0L));
        Submit("StartConfigured"); Submit("EndDevelopmentTurn"); Submit("BeginDevelopmentTurn");
        if (plan == "horn-cleat") { Submit("EndDevelopmentTurn"); Submit("BeginDevelopmentTurn"); }
        return All("StartRow").Select(row => new Row(Field(row, "StartRow", "At").AsString(), Field(row, "StartRow", "Turn").AsInt(), Field(row, "StartRow", "Hp").AsInt(), Field(row, "StartRow", "Block").AsInt(),
            [Field(row, "StartRow", "B0").AsInt(), Field(row, "StartRow", "B1").AsInt()], [Field(row, "StartRow", "S0").AsBool(), Field(row, "StartRow", "S1").AsBool()])).ToArray();
    }
    private static Row[] Native(string seed, string plan)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered(); var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000);
        RelicCmd.Obtain(ModelDb.Relic<Anchor>(), player).GetAwaiter().GetResult();
        if (plan.Contains("clamp", StringComparison.Ordinal)) RelicCmd.Obtain(ModelDb.Relic<SturdyClamp>(), player).GetAwaiter().GetResult();
        var combat = new CombatState(run); combat.AddPlayerCreature(player.Creature);
        for (var i = 0; i < 2; i++) { var e = combat.AddMonster((MonsterModel)ModelDb.Monster<TwigSlimeS>().MutableClone(), CombatSide.Enemy); e.SetMaxHpInternal(1000); e.SetCurrentHpInternal(1000); }
        var enemies = combat.Enemies.ToArray(); var rows = new List<Row>();
        void Capture(string at) => rows.Add(new(at, player.PlayerCombatState!.TurnNumber, player.Creature.CurrentHp, player.Creature.Block, enemies.Select(e => (long)e.Block).ToArray(), enemies.Select(e => e.Monster!.SpawnedThisTurn).ToArray()));
        var probe = (StartReadProbe)new StartReadProbe().MutableClone(); probe.AssignOwner(player); player.AddRelicInternal(probe);
        probe.Start = () => {
            if (plan is "barricade" or "barricade-clamp") PowerCmd.Apply<BarricadePower>(combat, player.Creature, 1, null, null).GetAwaiter().GetResult();
            if (plan == "enemy-barricade") PowerCmd.Apply<BarricadePower>(combat, enemies[0], 1, null, null).GetAwaiter().GetResult();
        };
        probe.Capture = Capture; probe.SideStarted = side => { if (side == CombatSide.Enemy && plan == "spawn-skip") typeof(MonsterModel).GetProperty(nameof(MonsterModel.SpawnedThisTurn))!.SetValue(enemies[0].Monster, true); };
        // Keep the read-only observer before HornCleat, matching the DSL audit priority.
        if (plan == "horn-cleat") RelicCmd.Obtain(ModelDb.Relic<HornCleat>(), player).GetAwaiter().GetResult();
        var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult();
        player.Creature.LoseBlockInternal(player.Creature.Block); player.Creature.GainBlockInternal(40);
        foreach (var e in enemies) e.GainBlockInternal(15);
        if (plan == "zero-member") enemies[0].SetCurrentHpInternal(0);
        Capture("configured"); engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
        if (plan == "horn-cleat") engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
        return rows.ToArray();
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }
#if REFERENCE_SIM
    private sealed class StartReadProbe : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Common;
        internal Action Start { get; set; } = null!;
        internal Action<string> Capture { get; set; } = null!;
        internal Action<CombatSide> SideStarted { get; set; } = null!;
        public override Task BeforeCombatStart() { Start(); return Task.CompletedTask; }
        public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants) { Capture("before:" + (side == CombatSide.Player ? "player" : "enemy")); return Task.CompletedTask; }
        public override Task AfterBlockCleared(Creature creature) { Capture("cleared:" + (creature.IsPlayer ? "player" : "enemy")); return Task.CompletedTask; }
        public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants) { SideStarted(side); Capture("side:" + (side == CombatSide.Player ? "player" : "enemy")); return Task.CompletedTask; }
        public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) { if (side == CombatSide.Enemy) Capture("enemy-end"); return Task.CompletedTask; }
    }
#endif
    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "turn-start-" + Guid.NewGuid());
        internal Fixture(string source)
        {
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) { var target = Path.Combine(Root, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); }
            var entry = Path.Combine(Root, "base.spire"); File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"turn-start-probe.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "turn-start-probe.spire"), """
                using namespace base-spire;
                dsl_version = "core/0.3"
                entity StartPlan { fields = { Name = { type = String(length = 32) } } }
                entity StartRow { fields = { At = { type = String(length = 32) } Turn = { type = Int } Hp = { type = Int } Block = { type = Int } B0 = { type = Int } B1 = { type = Int } S0 = { type = Bool } S1 = { type = Bool } } }
                action StartSetPlan { role = player parameters = { plan = String(length = 32) } locals = { p = Binding } body = { create = { type = StartPlan scope = frameScope() into = p fields = { Name = plan } } } }
                procedure StartCapture { parameters = { at = String(length = 32) } locals = { row = Binding e0 = Binding e1 = Binding player = Binding } body = {
                  let = { into = player value = first(type(Player)) }
                  let = { into = e0 value = first(type(Enemy)) }
                  let = { into = e1 value = first(where(type(Enemy), e, not(equal(e, e0)))) }
                  create = { type = StartRow scope = frameScope() into = row fields = { At = at Turn = field(player, Player.TurnNumber) Hp = field(player, Player.Hp) Block = field(player, Player.Block) B0 = field(e0, Enemy.Block) B1 = field(e1, Enemy.Block) S0 = field(e0, Enemy.SpawnedThisTurn) S1 = field(e1, Enemy.SpawnedThisTurn) } }
                } }
                procedure StartSetup { parameters = { owner = Ref(Player) } body = {
                  if = { condition = or(equal(field(first(type(StartPlan)), StartPlan.Name), "barricade"), equal(field(first(type(StartPlan)), StartPlan.Name), "barricade-clamp")) then = { call = { procedure = ApplyPlayerPower args = { owner = owner definition = "base-spire:barricade" stacks = 1 } } } }
                  if = { condition = equal(field(first(type(StartPlan)), StartPlan.Name), "enemy-barricade") then = { call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:barricade" stacks = 1 } } } }
                } }
                subscription StartSetup { event = BeforeCombatStart reaction = StartSetup }
                procedure StartBeforePlayer { parameters = { owner = Ref(Player) } body = { call = { procedure = StartCapture args = { at = "before:player" } } } }
                procedure StartBeforeEnemy { parameters = { owner = Ref(Player) } body = { call = { procedure = StartCapture args = { at = "before:enemy" } } } }
                procedure StartClearPlayer { parameters = { owner = Ref(Player) } body = { call = { procedure = StartCapture args = { at = "cleared:player" } } } }
                procedure StartClearEnemy { parameters = { owner = Ref(Player) target = Ref(Enemy) } body = { call = { procedure = StartCapture args = { at = "cleared:enemy" } } } }
                procedure StartSidePlayer { parameters = { owner = Ref(Player) } body = { call = { procedure = StartCapture args = { at = "side:player" } } } }
                procedure StartSideEnemy { parameters = { owner = Ref(Player) } body = {
                  if = { condition = equal(field(first(type(StartPlan)), StartPlan.Name), "spawn-skip") then = { set = { target = first(type(Enemy)) field = Enemy.SpawnedThisTurn value = true } } }
                  call = { procedure = StartCapture args = { at = "side:enemy" } }
                } }
                procedure StartEndEnemy { parameters = { owner = Ref(Player) } body = { call = { procedure = StartCapture args = { at = "enemy-end" } } } }
                subscription StartBeforePlayer { event = BeforePlayerSideTurnStart reaction = StartBeforePlayer priority = -100 }
                subscription StartBeforeEnemy { event = BeforeEnemySideTurnStart reaction = StartBeforeEnemy priority = -100 }
                subscription StartClearPlayer { event = AfterBlockCleared reaction = StartClearPlayer priority = -100 }
                subscription StartClearEnemy { event = AfterEnemyBlockCleared reaction = StartClearEnemy priority = -100 }
                subscription StartSidePlayer { event = AfterPlayerSideTurnStart reaction = StartSidePlayer priority = -100 }
                subscription StartSideEnemy { event = AfterEnemySideTurnStart reaction = StartSideEnemy priority = -100 }
                subscription StartEndEnemy { event = AfterEnemySideTurnEnd reaction = StartEndEnemy priority = -100 }
                action StartConfigured { role = player body = { call = { procedure = StartCapture args = { at = "configured" } } } }
                procedure StartClamp { parameters = { check = Ref(BlockClearCheck) listener = Ref(Relic) } body = {
                  if = { condition = and(field(check, BlockClearCheck.ShouldClear), not(isNone(field(check, BlockClearCheck.Player)))) then = { call = { procedure = RecordBlockClearPreventer args = { check = check power = none relic = listener } } } }
                } }
                subscription StartClamp { event = BlockClearChecking reaction = StartClamp instances = where(collection(Relics), r, and(equal(field(r, Relic.DefinitionId), "base-spire:anchor"), or(equal(field(first(type(StartPlan)), StartPlan.Name), "clamp"), equal(field(first(type(StartPlan)), StartPlan.Name), "barricade-clamp")))) instance_order = pureCall(RelicHookOrder, listener, true) }
                procedure StartClamped { parameters = { check = Ref(BlockClearCheck) listener = Ref(Relic) } body = { set = { target = field(check, BlockClearCheck.Player) field = Player.Block value = minimum(10, field(field(check, BlockClearCheck.Player), Player.Block)) } } }
                subscription StartClamped { event = AfterPreventingBlockClear reaction = StartClamped instances = where(collection(Relics), r, equal(r, field(check, BlockClearCheck.PreventerRelic))) instance_order = pureCall(RelicHookOrder, listener, true) }
                """);
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root);
            if (Path.GetDirectoryName(path) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || !Path.GetFileName(path).StartsWith("turn-start-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected turn start fixture cleanup path.");
            Directory.Delete(path, true);
        }
    }
}
