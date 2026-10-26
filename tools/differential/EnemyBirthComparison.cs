using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class EnemyBirthComparison
{
    private const string Niche = "base-spire:run:niche";
    private static readonly string[] Modes = ["reverse-slots", "located-slots", "new-combat", "fixed-hp", "zero-hp-member", "detached", "duplicate-max", "entry-condition", "before-side-condition"];
    private sealed record Birth(long Id, long Hp, ulong[] MonsterRng);
    private sealed record HpCall(long Min, long Max, long Value, long Before, long After);
    private sealed record Phase(string At, long Id, string Move, long History);
    private sealed record Trace(Birth[] Births, long[] ActionOrder, HpCall[] HpCalls, long NicheDraws, ulong[] NicheState, Phase[] Phases, long[] RandomTargets);

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use enemy-birth-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        var original = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        using var fixture = new Fixture(original.Root);
        var package = DslPackage.Load(Path.Combine(fixture.Root, "package.json"));
        var results = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" }) foreach (var mode in Modes)
        {
            var left = Dsl(package, seed, mode); var right = Native(seed, mode);
            var same = NormalPreflight.Json(left) == NormalPreflight.Json(right); passed &= same;
            var expectedBirths = mode.EndsWith("-slots", StringComparison.Ordinal) ? 4 : mode is "fixed-hp" or "new-combat" ? 2 : mode.EndsWith("condition", StringComparison.Ordinal) ? 1 : mode == "duplicate-max" ? 3 : 6;
            if (left.Births.Length != expectedBirths || left.HpCalls.Length != expectedBirths || right.Births.Length != expectedBirths
                || (mode is "reverse-slots" or "located-slots" or "entry-condition" or "before-side-condition" && left.Phases.Length != 6 * expectedBirths))
                throw new InvalidOperationException("Incomplete enemy birth case: " + seed + ":" + mode);
            results.Add(new { id = seed + ":" + mode, status = same ? "passed" : "diverged", left, right });
        }
        var report = new { schema = "dsl-migration/enemy-birth@1", scope = "controlled-component", reference,
            package_digest = original.Digest, fixture_digest = package.Digest, status = passed ? "passed" : "diverged",
            independent_verified = passed, normal_run_verified = false,
            setup = "Factory/slot cases use actual CombatRoom.Prepare with players first and real Wrigglers in factory order 4,1,3,2. HP cases preserve actual source draws; zero HP, removal and duplicate MaxHp are explicit controlled mutations. Beetle source runs its real entry Powers and conditional graph; DSL uses a tool-only condition probe, not registered SlumberingBeetle content or Slumber support. Located case sets explicit third-act map coordinates on both sides; it does not prove normal access. New-combat explicitly creates a fresh empty context. The slot cases also compare eight real random-target selections from source-ordered living candidates. Both sides have empty decks. DSL End+Begin is explicit component stepping, not normal EndTurn certification. MonsterSeed is checked against the source's actual fresh monster RNG state, without reseeding it.", cases = results };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }

    private static Trace Dsl(DslPackage package, string seed, string mode)
    {
        var s = package.CreateSession(1, ["player"], seed, new SessionDebugOptions(MaxRecords: 100_000)); s.Start();
        InstanceId[] All(string type) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        void Set(InstanceId id, string type, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
        void Submit(string name)
        {
            var action = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
            s.Submit(action.Definition, s.Participant(0), []);
            if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Enemy birth DSL stopped: " + s.Fault);
        }
        var player = All("Player").Single(); Set(player, "Player", "UsesRunDeck", Value.Of(true));
        Set(player, "Player", "Hp", Value.Of(1000L)); Set(player, "Player", "MaxHp", Value.Of(1000L));
        if (mode == "located-slots") Submit("BirthLocation");
        var births = new List<Birth>();
        void Born(string action)
        {
            Submit(action);
            var enemy = All("Enemy").Last();
            births.Add(new(Field(enemy, "Enemy", "CombatId").AsInt(), Field(enemy, "Enemy", "MaxHp").AsInt(), MonsterState(unchecked((ulong)Field(enemy, "Enemy", "MonsterSeed").AsInt()))));
        }
        if (mode.EndsWith("-slots", StringComparison.Ordinal)) { foreach (var slot in new[] { 4, 1, 3, 2 }) Born("BirthSlot" + slot); }
        else if (mode.EndsWith("condition", StringComparison.Ordinal)) Born("BirthBeetle");
        else if (mode == "new-combat") { Born("BirthSlot1"); Submit("BirthRemoveFirst"); Submit("BirthNewContext"); Born("BirthSlot1"); }
        else if (mode == "fixed-hp") { Born("BirthFixed"); Born("BirthFixed"); }
        else
        {
            Born("BirthSlot1");
            var first = All("Enemy").Single();
            if (mode == "zero-hp-member") Set(first, "Enemy", "Hp", Value.Of(0L));
            if (mode == "detached") Submit("BirthRemoveFirst");
            if (mode == "duplicate-max")
            {
                Set(first, "Enemy", "MaxHp", Value.Of(17L)); Born("BirthSlot1");
                Set(All("Enemy").Last(), "Enemy", "MaxHp", Value.Of(17L)); Born("BirthSlot1");
            }
            else for (var i = 0; i < 5; i++) Born("BirthSlot1");
        }
        Submit("BirthSort");
        var actionOrder = All("Enemy").OrderBy(e => Field(e, "Enemy", "SpawnOrder").AsInt()).Select(e => Field(e, "Enemy", "CombatId").AsInt()).ToArray();
        if (mode is "reverse-slots" or "located-slots" or "entry-condition" or "before-side-condition")
        {
            if (mode == "before-side-condition") Submit("BirthWake");
            Submit("BirthStart"); Submit("BeginDevelopmentTurn"); Submit("EndDevelopmentTurn"); Submit("BeginDevelopmentTurn");
        }
        if (mode.EndsWith("-slots", StringComparison.Ordinal)) for (var i = 0; i < 8; i++) Submit("BirthPickTarget");
        if (s.Debug!.DroppedRecords != 0) throw new InvalidOperationException("Enemy birth diagnostics truncated.");
        var calls = s.Debug.Capture().Where(r => r.Kind == SessionDebugKind.RandomCall && r.Subject == Niche).Select(r =>
        {
            var c = r.Random ?? throw new InvalidOperationException("Missing HP call.");
            if (c.Operation != "int") throw new InvalidOperationException("Unexpected HP operation.");
            return new HpCall(long.Parse(c.Minimum, CultureInfo.InvariantCulture), long.Parse(c.Maximum, CultureInfo.InvariantCulture), long.Parse(c.Result, CultureInfo.InvariantCulture), c.Before, c.After);
        }).ToArray();
        var rng = s.Random.Capture().Streams.Single(r => r.Name == Niche);
        var phases = All("BirthPhase").Select(p => new Phase(Field(p, "BirthPhase", "At").AsString(), Field(p, "BirthPhase", "Id").AsInt(), Field(p, "BirthPhase", "Move").AsString(), Field(p, "BirthPhase", "History").AsInt())).ToArray();
        return new(births.ToArray(), actionOrder, calls, rng.Draws, [rng.S0, rng.S1, rng.S2, rng.S3], phases, All("BirthPick").Select(p => Field(p, "BirthPick", "Id").AsInt()).ToArray());
    }

    private static ulong[] MonsterState(ulong seed)
    {
#if REFERENCE_SIM
        var state = new Rng(seed).ToSerializable(); return [state.state0, state.state1, state.state2, state.state3];
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }

    private static Trace Native(string seed, string mode)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered();
        var run = new RunState(seed); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000);
        if (mode == "located-slots")
        {
            ((List<MapCoord>)typeof(RunState).GetField("_visitedMapCoords", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!).Add(new MapCoord(3, 5));
            typeof(RunState).GetProperty(nameof(RunState.CurrentActIndex))!.SetValue(run, 2);
        }
        var rng = run.Rng.Niche; var calls = new List<HpCall>(); (long Min, long Max) bounds = default;
        var hook = typeof(Rng).GetField("_drawObserver", BindingFlags.Instance | BindingFlags.NonPublic)!;
        if (RngDiagnostics.DrawObserver is not null || hook.GetValue(rng) is not null) throw new InvalidOperationException("HP observer already owned.");
        RngDiagnostics.DrawObserver = (source, op, min, max, arity) => { if (ReferenceEquals(source, rng)) { if (op != "NextInt" || arity != 2) throw new InvalidOperationException("Unexpected HP operation."); bounds = ((long)min, (long)max); } };
        hook.SetValue(rng, (Action<int, string, string>)((cursor, _, result) => calls.Add(new(bounds.Min, bounds.Max, long.Parse(result, CultureInfo.InvariantCulture), cursor - 1, cursor))));
        try
        {
            var births = new List<Birth>(); CombatState combat;
            Birth Take(Creature e) { var state = e.Monster!.Rng.ToSerializable(); return new(e.CombatId!.Value, e.MaxHp, [state.state0, state.state1, state.state2, state.state3]); }
            if (mode.EndsWith("-slots", StringComparison.Ordinal))
            {
                var encounter = new EncounterDefinition(() => new[] { 4, 1, 3, 2 }.Select(i => ((MonsterModel)ModelDb.Monster<Wriggler>().MutableClone(), (string?)("wriggler" + i))).ToArray()) { Slots = ["wriggler1", "wriggler2", "wriggler3", "wriggler4"] };
                Func<(EncounterDefinition, IReadOnlyList<(MonsterModel, string?)>)> factory = () => (encounter, encounter.CreateMonsters());
                var room = (CombatRoom)Activator.CreateInstance(typeof(CombatRoom), BindingFlags.Instance | BindingFlags.NonPublic, null, [factory, RoomType.Monster], null)!;
                typeof(CombatRoom).GetMethod("Prepare", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(room, [run]);
                combat = (CombatState)typeof(CombatRoom).GetField("_preparedCombatState", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(room)!;
                births.AddRange(combat.SpawnedEnemies.Select(Take));
            }
            else
            {
                combat = new CombatState(run); combat.AddPlayerCreature(player.Creature);
                Creature Born(bool beetle = false) { var e = combat.AddMonster((MonsterModel)(beetle ? ModelDb.Monster<SlumberingBeetle>().MutableClone() : ModelDb.Monster<Wriggler>().MutableClone()), CombatSide.Enemy, beetle ? null : "wriggler1"); births.Add(Take(e)); return e; }
                if (mode.EndsWith("condition", StringComparison.Ordinal)) Born(true);
                else if (mode == "new-combat") { Born(); combat.RemoveCreature(combat.Enemies.Single()); combat = new CombatState(run); combat.AddPlayerCreature(player.Creature); Born(); }
                else if (mode == "fixed-hp") { Born(true); Born(true); }
                else
                {
                    var first = Born();
                    if (mode == "zero-hp-member") first.SetCurrentHpInternal(0);
                    if (mode == "detached") combat.RemoveCreature(first);
                    if (mode == "duplicate-max") { first.SetMaxHpInternal(17); var second = Born(); second.SetMaxHpInternal(17); Born(); }
                    else for (var i = 0; i < 5; i++) Born();
                }
            }
            var actionOrder = combat.Enemies.Select(e => (long)e.CombatId!.Value).ToArray();
            var phases = new List<Phase>();
            if (mode is "reverse-slots" or "located-slots" or "entry-condition" or "before-side-condition")
            {
                var probe = (BirthPhaseProbeRelic)new BirthPhaseProbeRelic().MutableClone(); probe.AssignOwner(player); player.AddRelicInternal(probe);
                probe.BeforePlayer = () => { if (mode == "before-side-condition" && combat.Enemies[0].GetPower<SlumberPower>() is { } slumber) PowerCmd.Remove(slumber).GetAwaiter().GetResult(); };
                probe.Capture = at => { foreach (var e in combat.Enemies) phases.Add(new(at, e.CombatId!.Value, e.Monster!.NextMove?.Id ?? "none", e.Monster.MoveStateMachine!.StateLog.Count)); };
                var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult(); engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
            }
            var picks = mode.EndsWith("-slots", StringComparison.Ordinal) ? Enumerable.Range(0, 8).Select(_ => (long)(run.Rng.CombatTargets.NextItem(combat.HittableEnemies) ?? throw new InvalidOperationException("Native random target set is empty.")).CombatId!.Value).ToArray() : Array.Empty<long>();
            var state = rng.ToSerializable();
            return new(births.ToArray(), actionOrder, calls.ToArray(), state.counter, [state.state0, state.state1, state.state2, state.state3], phases.ToArray(), picks);
        }
        finally { hook.SetValue(rng, null); RngDiagnostics.DrawObserver = null; }
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "enemy-birth-" + Guid.NewGuid());
        internal Fixture(string original)
        {
            foreach (var file in Directory.GetFiles(original, "*", SearchOption.AllDirectories)) { var target = Path.Combine(Root, Path.GetRelativePath(original, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); }
            var entry = Path.Combine(Root, "base.spire"); File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"birth-probe.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            var script = """
                using namespace base-spire;
                dsl_version = "core/0.3"
                entity BirthPick { fields = { Id = { type = Int } } }
                action BirthPickTarget { role = player locals = { target = Binding row = Binding } body = {
                  call = { procedure = RandomLivingEnemy into = target }
                  create = { type = BirthPick scope = frameScope() into = row fields = { Id = field(target, Enemy.CombatId) } }
                } }
                entity BirthFlag { fields = { Asleep = { type = Bool default = false } Wake = { type = Bool default = false } } }
                entity BirthPhase { fields = { At = { type = String(length = 32) } Id = { type = Int } Move = { type = String(length = 64) } History = { type = Int } } }
                action BirthBeetle { role = player locals = { target = Binding flag = Binding } body = {
                  call = { procedure = CreateSourceEncounterEnemy args = { definition = "base-spire:birth-beetle-probe" min = 86 max = 86 } into = target }
                  create = { type = BirthFlag scope = frameScope() into = flag }
                } }
                action BirthFixed { role = player locals = { target = Binding } body = { call = { procedure = CreateSourceEncounterEnemy args = { definition = "base-spire:birth-fixed-probe" min = 86 max = 86 } into = target } } }
                action BirthStart { role = player body = { call = { procedure = BeginDevelopmentCombat args = { owner = first(type(Player)) } } } }
                action BirthNewContext { role = player body = { call = { procedure = PrepareEnemyBirthContext } } }
                action BirthLocation { role = player locals = { run = Binding node = Binding } body = {
                  create = { type = Run scope = frameScope() into = run fields = { Player = first(type(Player)) ActIndex = 2 } }
                  create = { type = MapNode scope = frameScope() into = node fields = { Run = run Col = 3 Row = 5 } }
                  set = { target = run field = Run.CurrentNode value = node }
                  set = { target = first(type(Player)) field = Player.CurrentRun value = run }
                } }
                action BirthWake { role = player body = { set = { target = first(type(BirthFlag)) field = BirthFlag.Wake value = true } } }
                action BirthRemoveFirst { role = player body = { call = { procedure = RemoveCombatEnemy args = { target = first(type(Enemy)) } } } }
                action BirthSort { role = player body = { call = { procedure = SortEncounterEnemies } } }
                use BirthGraph { template = EnemyMoveGraphDefinition args = { definition = "base-spire:birth-beetle-probe" initial = "SLEEP_CHECK" } }
                use BirthConditional { template = EnemyMoveNodeDefinition args = { definition = "base-spire:birth-beetle-probe" id = "SLEEP_CHECK" kind = "conditional" } }
                use BirthSnore { template = EnemyMoveNodeDefinition args = { definition = "base-spire:birth-beetle-probe" id = "SNORE_MOVE" next = "SLEEP_CHECK" intent = "sleep" } }
                use BirthRoll { template = EnemyMoveNodeDefinition args = { definition = "base-spire:birth-beetle-probe" id = "ROLL_OUT_MOVE" next = "ROLL_OUT_MOVE" intent = "attack,buff" } }
                procedure BirthCondition { pure = true parameters = { state = Ref(EnemyMoveState) node = Ref(EnemyMoveNode) } result = Binding body = {
                  if = { condition = field(first(type(BirthFlag)), BirthFlag.Asleep) then = { return = { value = pureCall(FindEnemyMoveNode, field(state, EnemyMoveState.Graph), "SNORE_MOVE") } } }
                  return = { value = pureCall(FindEnemyMoveNode, field(state, EnemyMoveState.Graph), "ROLL_OUT_MOVE") }
                } }
                dispatchCase BirthConditionCase { dispatch = ResolveEnemyMoveCondition key = "base-spire:birth-beetle-probe" procedure = BirthCondition }
                procedure BirthExecute { parameters = { owner = Ref(Player) target = Ref(Enemy) node = Ref(EnemyMoveNode) } body = {} }
                dispatchCase BirthExecuteCase { dispatch = ExecuteEnemyMove key = "base-spire:birth-beetle-probe" procedure = BirthExecute }
                procedure BirthEntry { parameters = { owner = Ref(Player) target = Ref(Enemy) } body = {
                  if = { condition = equal(field(target, Enemy.DefinitionId), "base-spire:birth-beetle-probe") then = { set = { target = first(type(BirthFlag)) field = BirthFlag.Asleep value = true } } }
                } }
                subscription BirthEntry { event = EnemyAddedToCombat reaction = BirthEntry }
                procedure BirthCapture { parameters = { at = String(length = 32) } locals = { row = Binding state = Binding } body = {
                  forEach = { query = sort(type(Enemy), e, field(e, Enemy.SpawnOrder)) item = enemy body = {
                    let = { into = state value = first(where(type(EnemyMoveState), m, equal(field(m, EnemyMoveState.Enemy), enemy))) }
                    create = { type = BirthPhase scope = frameScope() into = row fields = { At = at Id = field(enemy, Enemy.CombatId) Move = field(enemy, Enemy.MoveId) History = field(state, EnemyMoveState.Sequence) } }
                  } }
                } }
                procedure BirthCombat { parameters = { owner = Ref(Player) } body = { call = { procedure = BirthCapture args = { at = "combat-before" } } } }
                procedure BirthBefore { parameters = { owner = Ref(Player) } body = {
                  if = { condition = exists(where(type(BirthFlag), f, field(f, BirthFlag.Wake))) then = { set = { target = first(type(BirthFlag)) field = BirthFlag.Asleep value = false } } }
                  call = { procedure = BirthCapture args = { at = "player-before" } }
                } }
                procedure BirthEnergy { parameters = { owner = Ref(Player) } body = { call = { procedure = BirthCapture args = { at = "energy-reset" } } } }
                procedure BirthEnd { parameters = { owner = Ref(Player) } body = { call = { procedure = BirthCapture args = { at = "enemy-ended" } } } }
                subscription BirthCombat { event = BeforeCombatStart reaction = BirthCombat }
                subscription BirthBefore { event = BeforePlayerSideTurnStart reaction = BirthBefore }
                subscription BirthEnergy { event = AfterEnergyReset reaction = BirthEnergy }
                subscription BirthEnd { event = AfterEnemySideTurnEnd reaction = BirthEnd }
                """;
            foreach (var i in Enumerable.Range(1, 4)) script += $$"""
                action BirthSlot{{i}} { role = player locals = { target = Binding } body = { call = { procedure = Wriggler.Create args = { slot = "wriggler{{i}}" startStunned = false } into = target } } }
                """;
            File.WriteAllText(Path.Combine(Root, "birth-probe.spire"), script);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }
}

#if REFERENCE_SIM
internal sealed class BirthPhaseProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;
    internal Action<string> Capture { get; set; } = _ => { };
    internal Action BeforePlayer { get; set; } = () => { };
    public override Task BeforeCombatStart() { Capture("combat-before"); return Task.CompletedTask; }
    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants) { if (side == CombatSide.Player) { BeforePlayer(); Capture("player-before"); } return Task.CompletedTask; }
    public override Task AfterEnergyReset(Player player) { Capture("energy-reset"); return Task.CompletedTask; }
    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) { if (side == CombatSide.Enemy) Capture("enemy-ended"); return Task.CompletedTask; }
}
#endif
