using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Rngs;
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
using Sts2Sim.Core.ValueProps;
#endif

namespace Differential;

internal static class DamageComparison
{
    private sealed record Plan(string Name, int Strength = 0, int Vigor = 0, bool Weak = false,
        bool Vulnerable = false, int Cruelty = 0, long Block = 0, long PlayerBlock = 0, int Hp = 1000, bool SelfHarm = false, bool Reactions = false, int PlayerHp = 80, bool PenNib = false, bool AllTargets = false, bool Incoming = false);
    private static readonly Plan[] Plans = [
        new("plain"), new("retaliation-kills-attacker", Incoming: true, Hp: 3, PlayerBlock: 4),
        new("whirlwind-double-kill", AllTargets: true, Hp: 5), new("self-harm", SelfHarm: true),
        new("self-harm-reactions", SelfHarm: true, Reactions: true),
        new("lethal-self-harm", SelfHarm: true, Reactions: true, PlayerHp: 1),
        new("negative-strength-vigor", Strength: -10, Vigor: 5),
        new("negative-total", Strength: -10), new("negative-strength-vigor-weak-vulnerable", Strength: -10, Vigor: 5, Weak: true, Vulnerable: true),
        new("fraction", Strength: 1, Weak: true, Vulnerable: true),
        new("pen-nib-fraction-chain", Strength: 1, Weak: true, Vulnerable: true, Cruelty: 1, PenNib: true),
        new("fraction-fully-blocked", Strength: 1, Weak: true, Vulnerable: true, Block: 7),
        new("fraction-block-remaining", Strength: 1, Weak: true, Vulnerable: true, Block: 9),
        new("fraction-broken", Strength: 1, Weak: true, Vulnerable: true, Block: 3),
        new("zero-blocked", Strength: -6, Block: 3), new("overkill", Hp: 2),
        new("body-slam-large", PlayerBlock: 999999999, Vulnerable: true),
        new("body-slam-large-chain", PlayerBlock: 999999999, Strength: 999, Vigor: 999, Weak: true, Vulnerable: true, Cruelty: 999),
    ];
    private sealed record Row(string Actor, string Dealer, bool Move, bool Powered, bool Unblockable, long Blocked, long Unblocked, long Overkill, bool BlockBroken, bool FullyBlocked, bool Killed);
    private sealed record Notification(string Actor, string Stage, long AmountScaled, long Hp, long Block, string Card, string Dealer, long OtherHp);
    private sealed record Result(long Hp, long Block, long Energy, long PlayerHp, long Strength, long[] EnemyHps, Row[] Results, Notification[] Notifications);

    private sealed record Outcome(Result Value, string State);

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
            var leftRun = Dsl(package, seed, plan, true); var rightRun = Native(seed, plan, true);
            if (leftRun.State != Dsl(package, seed, plan, false).State || rightRun.State != Native(seed, plan, false).State)
                throw new InvalidOperationException("Damage diagnostics changed gameplay or random state: " + seed + ":" + plan.Name);
            var left = leftRun.Value; var right = rightRun.Value;
            var same = JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right); passed &= same;
            cases.Add(new { id = seed + ":" + plan.Name, status = same ? "passed" : "diverged", diagnostics_read_only = true, left, right });
        }
        var report = new { schema = "dsl-migration/damage@1", reference, package_digest = original.Digest,
            fixture_digest = package.Digest, adapter_sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(DamageComparison).Assembly.Location))), status = passed ? "passed" : "diverged", independent_verified = passed,
            normal_run_verified = false, scope = "controlled-card-damage-results", executed = cases.Count,
            expected_case_ids = new[] { "777", "A😀B" }.SelectMany(s => Plans.Select(p => s + ":" + p.Name)), cases };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }

    private static Outcome Dsl(DslPackage package, string seed, Plan plan, bool diagnostics)
    {
        var s = package.CreateSession(1, ["player"], seed, diagnostics ? new SessionDebugOptions(MaxRecords: 100_000, CaptureValues: true) : null); s.Start();
        InstanceId[] All(string type) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        void Set(InstanceId id, string type, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
        void Submit(string name, params Value[] args)
        {
            s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), s.Participant(0), args);
            if (s.Status != SessionStatus.Running) throw new InvalidOperationException(name + ": " + s.Fault);
        }
        var player = All("Player").Single(); Set(player, "Player", "UsesRunDeck", Value.Of(true));
        Submit("FuzzyWurmCrawler.Spawn"); if (plan.AllTargets) Submit("FuzzyWurmCrawler.Spawn"); var enemy = All("Enemy").First();
        foreach (var e in All("Enemy")) { Set(e, "Enemy", "MaxHp", Value.Of(1000L)); Set(e, "Enemy", "Hp", Value.Of(1000L)); }
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        if (plan.PenNib)
        {
            Submit("PenNib.Obtain"); Set(player, "Player", "Energy", Value.Of(12L));
            for (var i = 0; i < 9; i++) { Submit("Strike.AddToHand"); Submit("PlayAttackFromHand", Value.OfRef(s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id).Last())); }
        }
        void Power(string id, int amount, bool target = false) { if (amount != 0) Submit("DamageProbePower", Value.Of(target), Value.Of("base-spire:" + id), Value.Of((long)amount)); }
        Power("strength-power", plan.Strength); Power("vigor-power", plan.Vigor); Power("weak-power", plan.Weak ? 1 : 0);
        Power("vulnerable-power", plan.Vulnerable ? 1 : 0, true); Power("cruelty-power", plan.Cruelty);
        if (plan.Reactions) { Power("inferno-power", 1); Power("rupture-power", 1); }
        if (plan.Incoming) Power("thorns-power", 4);
        foreach (var e in All("Enemy")) Set(e, "Enemy", "Hp", Value.Of((long)plan.Hp)); Set(enemy, "Enemy", "Block", Value.Of(plan.Block));
        Set(player, "Player", "Hp", Value.Of((long)plan.PlayerHp)); Set(player, "Player", "Block", Value.Of(plan.PlayerBlock)); Set(player, "Player", "EffectDepth", Value.Of(1L));
        if (plan.AllTargets) Set(player, "Player", "Energy", Value.Of(1L));
        if (!plan.Incoming) Submit(plan.AllTargets ? "Whirlwind.AddToHand" : plan.SelfHarm ? "Hemokinesis.AddToHand" : plan.PlayerBlock > 0 ? "BodySlam.AddToHand" : "Strike.AddToHand");
        var cursor = s.Debug?.Capture().LastOrDefault()?.Sequence ?? 0;
        if (plan.Incoming) Submit("DamageProbeIncoming");
        else Submit(plan.AllTargets ? "PlayCardFromHand" : "PlayAttackFromHand", Value.OfRef(s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id).Last()));
        var rows = (s.Debug?.Capture() ?? []).Where(r => r.Sequence > cursor && r.Kind == SessionDebugKind.EventEmit && r.Subject == "base-spire:DamageProbeResolved").Select(r => {
            if (r.Snapshot is not { Complete: true } snapshot || snapshot.Values.Count != 11) throw new InvalidOperationException("Incomplete damage result.");
            string S(int i) => snapshot.Values[i].Scalar!;
            return new Row(S(0), S(1), bool.Parse(S(2)), bool.Parse(S(3)), bool.Parse(S(4)), long.Parse(S(5)), long.Parse(S(6)), long.Parse(S(7)), bool.Parse(S(8)), bool.Parse(S(9)), bool.Parse(S(10)));
        }).ToArray();
        if (s.Debug is { DroppedRecords: > 0 }) throw new InvalidOperationException("Truncated damage diagnostics.");
        var notifications = (s.Debug?.Capture() ?? []).Where(r => r.Sequence > cursor && r.Kind == SessionDebugKind.EventEmit && r.Subject == "base-spire:DamageProbePoint").Select(r => {
            if (r.Snapshot is not { Complete: true } snapshot || snapshot.Values.Count != 8) throw new InvalidOperationException("Incomplete damage notification.");
            string S(int i) => snapshot.Values[i].Scalar!;
            return new Notification(S(0), S(1), long.Parse(S(2)), long.Parse(S(3)), long.Parse(S(4)), S(5), S(6), long.Parse(S(7)));
        }).ToArray();
        var result = new Result(Field(enemy, "Enemy", "Hp").AsInt(), Field(enemy, "Enemy", "Block").AsInt(), Field(player, "Player", "Energy").AsInt(), Field(player, "Player", "Hp").AsInt(),
            All("Power").Where(p => Field(p, "Power", "HolderPlayer") == Value.OfRef(player) && Field(p, "Power", "DefinitionId").AsString() == "base-spire:strength-power").Sum(p => Field(p, "Power", "Stacks").AsInt()), All("Enemy").Select(e => Field(e, "Enemy", "Hp").AsInt()).ToArray(), rows, notifications);
        return new(result, s.State.StateHash() + JsonSerializer.Serialize(s.Random.Capture()));
    }

    private static Outcome Native(string seed, Plan plan, bool diagnostics)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered();
        var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var c in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(c);
        var combat = new CombatState(run); combat.AddMonster((MonsterModel)ModelDb.Monster<FuzzyWurmCrawler>().MutableClone(), CombatSide.Enemy);
        if (plan.AllTargets) combat.AddMonster((MonsterModel)ModelDb.Monster<FuzzyWurmCrawler>().MutableClone(), CombatSide.Enemy);
        var enemies = combat.SpawnedEnemies.ToArray(); var enemy = enemies[0];
        foreach (var e in enemies) { e.SetMaxHpInternal(1000); e.SetCurrentHpInternal(1000); }
        string Actor(Creature c) => ReferenceEquals(c, player.Creature) ? "player" : "enemy:" + Array.IndexOf(enemies, c); enemy.SetMaxHpInternal(1000); enemy.SetCurrentHpInternal(1000);
        var rows = new List<Row>();
        var notifications = new List<Notification>(); var capture = false;
        var probe = (DamageProbeRelic)new DamageProbeRelic().MutableClone(); probe.AssignOwner(player); player.AddRelicInternal(probe);
        probe.Capture = (stage, amount, target, card, dealer) => {
            if (capture) notifications.Add(new(Actor(target), stage, (long)(amount * 10000), target.CurrentHp, target.Block, card?.Id.Entry ?? "", ReferenceEquals(dealer, player.Creature) ? "player" : ReferenceEquals(dealer, enemy) ? "enemy" : "none", ReferenceEquals(target, player.Creature) ? enemy.CurrentHp : enemies.FirstOrDefault(e => !ReferenceEquals(e, target))?.CurrentHp ?? -1));
        };
        var observer = DispatchProxy.Create(typeof(CombatEngine).Assembly.GetType("Sts2Sim.Core.Combat.ICombatObserver")!, typeof(NormalReferenceObserver));
        ((NormalReferenceObserver)observer).Record = (name, args) => {
            if (name == "DamageResolved" && args is [var dealer, Sts2Sim.Core.Entities.Creatures.DamageResult r])
                rows.Add(new(Actor(r.Receiver), ReferenceEquals(dealer, player.Creature) ? "player" : ReferenceEquals(dealer, enemy) ? "enemy" : "none", r.Props.HasFlag(ValueProp.Move), r.Props.IsPoweredAttack(), r.Props.HasFlag(ValueProp.Unblockable), r.BlockedDamage, r.UnblockedDamage, r.OverkillDamage, r.WasBlockBroken, r.WasFullyBlocked, r.WasTargetKilled));
        };
        var engine = (CombatEngine)Activator.CreateInstance(typeof(CombatEngine), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [combat, observer], null)!;
        engine.StartCombatAsync().GetAwaiter().GetResult();
        if (plan.PenNib)
        {
            RelicCmd.Obtain(ModelDb.Relic<Sts2Sim.Core.Models.Relics.PenNib>(), player).GetAwaiter().GetResult(); player.PlayerCombatState!.Energy = 12;
            for (var i = 0; i < 9; i++) { var prefix = (CardModel)ModelDb.Card<StrikeIronclad>().MutableClone(); prefix.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(prefix); engine.PlayCardAsync(player, prefix, enemy).GetAwaiter().GetResult(); }
        }
        void Apply<T>(int amount, bool target = false) where T : PowerModel { if (amount != 0) PowerCmd.Apply<T>(combat, target ? enemy : player.Creature, amount, null, null).GetAwaiter().GetResult(); }
        Apply<StrengthPower>(plan.Strength); Apply<VigorPower>(plan.Vigor); Apply<WeakPower>(plan.Weak ? 1 : 0);
        Apply<VulnerablePower>(plan.Vulnerable ? 1 : 0, true); Apply<CrueltyPower>(plan.Cruelty);
        if (plan.Incoming) Apply<ThornsPower>(4);
        if (plan.Reactions) { var inferno = PowerCmd.Apply<InfernoPower>(combat, player.Creature, 1, null, null).GetAwaiter().GetResult(); inferno.IncrementSelfDamage(); Apply<RupturePower>(1); }
        player.Creature.SetCurrentHpInternal(plan.PlayerHp); foreach (var e in enemies) e.SetCurrentHpInternal(plan.Hp); enemy.GainBlockInternal(plan.Block); player.Creature.GainBlockInternal(plan.PlayerBlock);
        if (plan.AllTargets) player.PlayerCombatState!.Energy = 1;
        rows.Clear(); capture = diagnostics;
        if (plan.Incoming) DamageCmd.Attack(6).FromMonster(enemy.Monster!).WithHitCount(2).Execute().GetAwaiter().GetResult();
        else
        {
            CardModel canonical = plan.AllTargets ? ModelDb.Card<Whirlwind>() : plan.SelfHarm ? ModelDb.Card<Hemokinesis>() : plan.PlayerBlock > 0 ? ModelDb.Card<BodySlam>() : ModelDb.Card<StrikeIronclad>();
            var card = (CardModel)canonical.MutableClone(); card.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(card);
            engine.PlayCardAsync(player, card, plan.AllTargets ? null : enemy).GetAwaiter().GetResult();
        }
        var value = new Result(enemy.CurrentHp, enemy.Block, player.PlayerCombatState.Energy, player.Creature.CurrentHp, (long)player.Creature.Powers.OfType<StrengthPower>().Sum(p => p.Amount), enemies.Select(e => (long)e.CurrentHp).ToArray(), rows.ToArray(), notifications.ToArray());
        var description = new CombatStateDescriptionBuilder(); CombatStateDescription.AppendExactState(ref description, combat);
        var state = JsonSerializer.Serialize(new { exact = description.Build(),
            rng = Enum.GetValues<RunRngType>().Select(t => run.Rng.GetRng(t).ToSerializable()),
            player_rng = Enum.GetValues<PlayerRngType>().Select(t => player.PlayerRng.GetRng(t).ToSerializable()) }, new JsonSerializerOptions { IncludeFields = true });
        return new(value, state);
#else
        throw new NotSupportedException("Independent reference is unavailable.");
#endif
    }

#if REFERENCE_SIM
    private sealed class DamageProbeRelic : RelicModel
    {
        public override Sts2Sim.Core.Entities.Relics.RelicRarity Rarity => Sts2Sim.Core.Entities.Relics.RelicRarity.Event;
        internal Action<string, decimal, Creature, CardModel?, Creature?> Capture { get; set; } = (_, _, _, _, _) => { };
        public override Task BeforeDamageReceived(Creature target, decimal amount, Sts2Sim.Core.ValueProps.ValueProp props, Creature? dealer, CardModel? cardSource) { Capture("before", amount, target, cardSource, dealer); return Task.CompletedTask; }
        public override Task BeforeDeath(Creature target) { Capture("death", 0, target, null, null); return Task.CompletedTask; }
        public override Task AfterBlockBroken(Creature target, Creature? breaker) { Capture("block", 0, target, null, breaker); return Task.CompletedTask; }
        public override Task AfterCurrentHpChanged(Creature target, decimal delta) { Capture("hp", -delta, target, null, null); return Task.CompletedTask; }
        public override Task AfterDamageGiven(Creature? dealer, Sts2Sim.Core.Entities.Creatures.DamageResult result, Sts2Sim.Core.ValueProps.ValueProp props, Creature target, CardModel? cardSource) { Capture("given", result.UnblockedDamage, target, cardSource, dealer); return Task.CompletedTask; }
        public override Task AfterDamageReceived(Creature target, Sts2Sim.Core.Entities.Creatures.DamageResult result, Sts2Sim.Core.ValueProps.ValueProp props, Creature? dealer, CardModel? cardSource) { Capture("received", result.UnblockedDamage, target, cardSource, dealer); return Task.CompletedTask; }
    }
#endif

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
                action DamageProbeIncoming { role = player body = { call = { procedure = EnemyAttack args = { attacker = first(type(Enemy)) defender = first(type(Player)) amount = 6 hits = 2 } } } }
                event DamageProbePoint { payload = { actor = String(length = 16) stage = String(length = 16) amount = Int hp = Int block = Int card = String(length = 64) dealer = String(length = 16) otherHp = Int } }
                event DamageProbeResolved { payload = { actor = String(length = 16) dealer = String(length = 16) move = Bool powered = Bool unblockable = Bool blocked = Int unblocked = Int overkill = Int broken = Bool full = Bool killed = Bool } }
                procedure DamageProbeResolved { parameters = { result = Ref(DamageResult) } locals = { actor = String(length = 16) dealer = String(length = 16) } body = {
                  let = { into = dealer value = "none" }
                  if = { condition = not(isNone(field(result, DamageResult.DealerPlayer))) then = { let = { into = dealer value = "player" } } }
                  if = { condition = not(isNone(field(result, DamageResult.DealerEnemy))) then = { let = { into = dealer value = "enemy" } } }
                  let = { into = actor value = "enemy:0" }
                  if = { condition = not(isNone(field(result, DamageResult.Player))) then = { let = { into = actor value = "player" } } else = {
                    if = { condition = not(equal(field(result, DamageResult.Enemy), first(type(Enemy)))) then = { let = { into = actor value = "enemy:1" } } }
                  } }
                  emit = { event = DamageProbeResolved payload = { actor = actor dealer = dealer move = field(result, DamageResult.Move) powered = field(result, DamageResult.Powered) unblockable = field(result, DamageResult.Unblockable) blocked = field(result, DamageResult.Blocked) unblocked = field(result, DamageResult.Unblocked) overkill = field(result, DamageResult.Overkill) broken = field(result, DamageResult.BlockBroken) full = field(result, DamageResult.FullyBlocked) killed = field(result, DamageResult.Killed) } }
                } }
                subscription DamageProbeResolved { event = DamageResolved reaction = DamageProbeResolved }
                procedure DamageProbeCapture { parameters = { result = Ref(DamageResult) stage = String(length = 16) amount = Int } locals = { card = String(length = 64) dealer = String(length = 16) actor = String(length = 16) hp = Int block = Int other = Binding otherHp = Int } body = {
                  let = { into = dealer value = "none" }
                  if = { condition = not(equal(stage, "hp")) then = {
                    if = { condition = not(isNone(field(result, DamageResult.DealerPlayer))) then = { let = { into = dealer value = "player" } } }
                    if = { condition = not(isNone(field(result, DamageResult.DealerEnemy))) then = { let = { into = dealer value = "enemy" } } }
                  } }
                  let = { into = card value = "" }
                  if = { condition = and(not(isNone(field(result, DamageResult.Card))), or(equal(stage, "before"), or(equal(stage, "given"), equal(stage, "received")))) then = { let = { into = card value = field(field(result, DamageResult.Card), Card.SourceEntry) } } }
                  let = { into = actor value = "enemy:0" }
                  if = { condition = isNone(field(result, DamageResult.Player)) then = {
                    if = { condition = not(equal(field(result, DamageResult.Enemy), first(type(Enemy)))) then = { let = { into = actor value = "enemy:1" } } }
                    let = { into = other value = first(where(type(Enemy), e, not(equal(e, field(result, DamageResult.Enemy))))) }
                    let = { into = otherHp value = -1 }
                    if = { condition = not(isNone(other)) then = { let = { into = otherHp value = field(other, Enemy.Hp) } } }
                    let = { into = hp value = field(field(result, DamageResult.Enemy), Enemy.Hp) }
                    let = { into = block value = field(field(result, DamageResult.Enemy), Enemy.Block) }
                  } else = {
                    let = { into = actor value = "player" }
                    let = { into = otherHp value = field(first(type(Enemy)), Enemy.Hp) }
                    let = { into = hp value = field(field(result, DamageResult.Player), Player.Hp) }
                    let = { into = block value = field(field(result, DamageResult.Player), Player.Block) }
                  } }
                  emit = { event = DamageProbePoint payload = { actor = actor stage = stage amount = amount hp = hp block = block card = card dealer = dealer otherHp = otherHp } }
                } }
                procedure DamageProbeEnemyDeath { parameters = { target = Ref(Enemy) } locals = { actor = String(length = 16) other = Binding hp = Int } body = {
                  let = { into = actor value = "enemy:0" }
                  if = { condition = not(equal(target, first(type(Enemy)))) then = { let = { into = actor value = "enemy:1" } } }
                  let = { into = other value = first(where(type(Enemy), e, not(equal(e, target)))) }
                  let = { into = hp value = -1 }
                  if = { condition = not(isNone(other)) then = { let = { into = hp value = field(other, Enemy.Hp) } } }
                  emit = { event = DamageProbePoint payload = { actor = actor stage = "death" amount = 0 hp = field(target, Enemy.Hp) block = field(target, Enemy.Block) card = "" dealer = "none" otherHp = hp } }
                } }
                subscription DamageProbeEnemyDeath { event = EnemyDied reaction = DamageProbeEnemyDeath }
                procedure DamageProbePlayerDeath { parameters = { owner = Ref(Player) } body = {
                  emit = { event = DamageProbePoint payload = { actor = "player" stage = "death" amount = 0 hp = field(owner, Player.Hp) block = field(owner, Player.Block) card = "" dealer = "none" otherHp = field(first(type(Enemy)), Enemy.Hp) } }
                } }
                subscription DamageProbePlayerDeath { event = PlayerDied reaction = DamageProbePlayerDeath }
                procedure DamageProbeBefore { parameters = { result = Ref(DamageResult) } body = { call = { procedure = DamageProbeCapture args = { result = result stage = "before" amount = field(result, DamageResult.ModifiedScaled) } } } }
                subscription DamageProbeBefore { event = BeforeDamageReceived reaction = DamageProbeBefore }
                procedure DamageProbeBlock { parameters = { result = Ref(DamageResult) } body = { call = { procedure = DamageProbeCapture args = { result = result stage = "block" amount = 0 } } } }
                subscription DamageProbeBlock { event = AfterBlockBroken reaction = DamageProbeBlock }
                procedure DamageProbeHp { parameters = { result = Ref(DamageResult) } body = { call = { procedure = DamageProbeCapture args = { result = result stage = "hp" amount = multiply(field(result, DamageResult.Unblocked), 10000) } } } }
                subscription DamageProbeHp { event = AfterDamageHpLost reaction = DamageProbeHp }
                procedure DamageProbeGiven { parameters = { result = Ref(DamageResult) } body = { call = { procedure = DamageProbeCapture args = { result = result stage = "given" amount = multiply(field(result, DamageResult.Unblocked), 10000) } } } }
                subscription DamageProbeGiven { event = AfterDamageGiven reaction = DamageProbeGiven }
                procedure DamageProbeReceived { parameters = { result = Ref(DamageResult) } body = { call = { procedure = DamageProbeCapture args = { result = result stage = "received" amount = multiply(field(result, DamageResult.Unblocked), 10000) } } } }
                subscription DamageProbeReceived { event = AfterDamageReceived reaction = DamageProbeReceived }
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
