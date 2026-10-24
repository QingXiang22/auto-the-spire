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
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using System.Reflection;
#endif

namespace Differential;

internal static class TurnPhaseComparison
{
    private static readonly string[] Plans = ["orichalcum-plating", "orichalcum-duplicate", "charon-lethal", "charon-survives", "charon-reshuffle", "starting-draw", "listener-order", "draw-before-block", "block-before-draw", "fan-ending", "bag-first-side", "fiend-fire-ending", "gremlin-horn-ending", "feed-ending"];
    private sealed record Call(string Stream, string Operation, string Minimum, string Maximum, string Result, long Before, long After, string[]? Candidates);
    private sealed record Power(string Definition, long Amount, string Applier);
    private sealed record Boundary(string At, long Turn, long Energy, long Hp, long MaxHp, long Block, long EnemyHp, bool Ending,
        string[][] Piles, Power[] Powers, Power[] EnemyPowers, long BlockedDamage, long AiDraws, ulong[] AiState, long ShuffleDraws, ulong[] ShuffleState, long TargetDraws, ulong[] TargetState, Call[] Calls, string[] RunOrder, string[] DirectOrder);

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use turn-phase-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository); using var probe = new Probe(repository);
        var package = DslPackage.Load(Path.Combine(probe.Root, "package.json")); var cases = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" })
        foreach (var plan in Plans)
        {
            Boundary[] left, right;
            try { left = Dsl(package, seed, plan); } catch (Exception error) { throw new InvalidOperationException("DSL " + seed + ":" + plan + ": " + error.Message, error); }
            try { right = Native(seed, plan); } catch (Exception error) { throw new InvalidOperationException("Source " + seed + ":" + plan + ": " + error.Message, error); }
            if (left.Length != (plan is "starting-draw" or "listener-order" or "bag-first-side" ? 1 : 2) || right.Length != left.Length) throw new InvalidOperationException("Turn comparison omitted required boundaries.");
            var same = JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right); passed &= same;
            cases.Add(new { id = seed + ":" + plan, status = same ? "passed" : "diverged", left, right });
        }
        var report = new { schema = "dsl-migration/turn-phases@1", reference,
            package_digest = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json")).Digest, probe_digest = package.Digest,
            status = passed ? "passed" : "diverged", independent_verified = passed, normal_run_verified = false, scope = "controlled-component",
            controlled_setup = "Real FuzzyWurmCrawler combat. Empty permanent decks except starting-draw (Strike, Defend, Bash); player/enemy HP explicitly normalized to 1000. Starting-draw uses HP970, BloodVial and Lantern. Orichalcum variants apply Plating5 and enemy Strength10 after turn one, then end the turn and start the next ordinary turn (native does both in EndPlayerTurnAsync). Duplicate relics are obtained by the real command. Charon variants exhaust Defend with DarkEmbrace1 at enemy HP3 or6; the reshuffle case supplies Bash/Strike/Offering in discard. Compare every listed command boundary, ordered piles/both-holder Powers and actual appliers, resources, ending, real blocked damage and complete monster_ai/shuffle/combat_targets states/cursors plus individual RNG calls and candidate order. Listener-order reads the actual locked Run/Direct iterators and compares all current fixture Power, inventory, card and monster instances. The native starting BurningBlood is also obtained in DSL. Hellraiser/FeelNoPain orders start with Block30 and enemy HP1; OrnamentalFan uses three real Strikes at enemy HP18. BagOfMarbles uses its real first-side hook and player applier. FiendFire draws for the hand exhaust before its lethal hit; its later self-exhaust is refused. GremlinHorn gains energy but its final-kill draw is refused. Feed at enemy HP10 still grants three MaxHp and healing, while its self-exhaust is refused; MaxHp is compared at every boundary. All lethal component boundaries precede reward settlement on both sides. No reference mutations, RNG resets or normal Run certification; birth/slots, exhaust causes and deferred ethereal draws remain separate work.", cases };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }

    private static Boundary[] Dsl(DslPackage package, string seed, string plan)
    {
        var s = package.CreateSession(1, ["player"], seed, new SessionDebugOptions(MaxRecords: 100_000)); s.Start();
        InstanceId[] All(string type) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId i, string type, string field) => s.State.GetField(i, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        void Set(InstanceId i, string type, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(i, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
        InstanceId[] Pile(string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id).ToArray();
        void Submit(string name, params InstanceId[] refs) { var a = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name); s.Submit(a.Definition, s.Participant(0), refs.Select(Value.OfRef).ToArray()); if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Turn DSL stopped at " + name + ": " + s.Fault); }
        var player = All("Player").Single(); Set(player, "Player", "UsesRunDeck", Value.Of(true)); Set(player, "Player", "AutoDrawPerTurn", Value.Of(5L));
        Submit("BurningBlood.Obtain");
        Set(player, "Player", "MaxHp", Value.Of(1000L)); Set(player, "Player", "Hp", Value.Of(plan == "starting-draw" ? 970L : 1000L));
        if (plan.StartsWith("orichalcum", StringComparison.Ordinal)) { Submit("Orichalcum.Obtain"); if (plan == "orichalcum-duplicate") Submit("PhaseExtraOrichalcum"); }
        else if (plan.StartsWith("charon", StringComparison.Ordinal)) Submit("CharonsAshes.Obtain");
        else if (plan == "listener-order") { Submit("Orichalcum.Obtain"); Submit("Lantern.Obtain"); }
        else if (plan == "fan-ending") Submit("OrnamentalFan.Obtain");
        else if (plan == "bag-first-side") Submit("BagOfMarbles.Obtain");
        else if (plan == "gremlin-horn-ending") Submit("GremlinHorn.Obtain");
        else if (plan is "draw-before-block" or "block-before-draw" or "fiend-fire-ending" or "feed-ending") { }
        else { Submit("BloodVial.Obtain"); Submit("Lantern.Obtain"); foreach (var name in new[] { "Strike", "Defend", "Bash" }) Submit(name + ".Obtain"); }
        Submit("FuzzyWurmCrawler.Spawn"); var target = All("Enemy").Single(); Set(target, "Enemy", "MaxHp", Value.Of(1000L)); Set(target, "Enemy", "Hp", Value.Of(1000L));
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        if (plan is "starting-draw" or "bag-first-side") return [Snapshot("started")];
        if (plan == "listener-order") { Submit("PhaseOrderSetup"); Submit("Strike.AddToHand"); Submit("PhaseRunOrder"); Submit("PhaseDirectOrder"); return [Snapshot("ordered")]; }
        if (plan == "fan-ending")
        {
            Set(target, "Enemy", "Hp", Value.Of(18L)); var before = Snapshot("configured");
            for (var i = 0; i < 3; i++) { Submit("Strike.AddToHand"); Submit("PhasePlay", Pile("Hand").Single()); }
            return [before, Snapshot("played")];
        }
        if (plan == "feed-ending") { Set(target, "Enemy", "Hp", Value.Of(10L)); Submit("Feed.AddToHand"); var before = Snapshot("configured"); Submit("PhasePlay", Pile("Hand").Single()); return [before, Snapshot("played")]; }
        if (plan == "gremlin-horn-ending")
        {
            Set(target, "Enemy", "Hp", Value.Of(6L)); Submit("Strike.AddToHand"); var drawn = Pile("Hand").Single();
            s.State.RunAtomic(() => { s.State.RemoveFromCollection(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id, drawn); s.State.AddToCollection(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:DrawPile").Id, drawn); });
            Submit("Strike.AddToHand"); var before = Snapshot("configured"); Submit("PhasePlay", Pile("Hand").Single()); return [before, Snapshot("played")];
        }
        if (plan.StartsWith("orichalcum", StringComparison.Ordinal))
        {
            Submit("PhasePlating"); var before = Snapshot("configured"); Submit("EndDevelopmentTurn"); Submit("BeginDevelopmentTurn"); return [before, Snapshot("next-turn")];
        }
        if (plan == "block-before-draw") Submit("PhaseFeelNoPain");
        Submit("PhaseDarkEmbrace");
        if (plan == "fiend-fire-ending")
        {
            Set(target, "Enemy", "Hp", Value.Of(7L)); Submit("Anger.AddToHand"); Submit("Anger.AddToHand");
            s.State.RunAtomic(() => { foreach (var c in Pile("Hand")) { s.State.RemoveFromCollection(s.RuleSet.Collections.Single(x => x.QualifiedId == "base-spire:Hand").Id, c); s.State.AddToCollection(s.RuleSet.Collections.Single(x => x.QualifiedId == "base-spire:DrawPile").Id, c); } });
            Submit("FiendFire.AddToHand"); var fire = Pile("Hand").Single(); Submit("Defend.AddToHand"); var before = Snapshot("configured"); Submit("PhasePlay", fire); return [before, Snapshot("played")];
        }
        if (plan is "draw-before-block" or "block-before-draw")
        {
            if (plan == "draw-before-block") Submit("PhaseFeelNoPain");
            Submit("PhaseHellraiser"); Set(player, "Player", "Block", Value.Of(30L));
        }
        Set(target, "Enemy", "Hp", Value.Of(plan is "draw-before-block" or "block-before-draw" ? 1L : plan == "charon-lethal" ? 3L : 6L));
        foreach (var name in plan == "charon-reshuffle" ? new[] { "Bash", "Strike", "Offering" } : new[] { "Strike" })
        {
            Submit(name + ".AddToHand"); var card = Pile("Hand").Single();
            s.State.RunAtomic(() => { s.State.RemoveFromCollection(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id, card); s.State.AddToCollection(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + (plan == "charon-reshuffle" ? "DiscardPile" : "DrawPile")).Id, card); });
        }
        Submit("Defend.AddToHand"); var victim = Pile("Hand").Single(); var start = Snapshot("configured"); Submit("PhaseExhaust", victim); return [start, Snapshot("exhausted")];

        Boundary Snapshot(string at)
        {
            if (s.Debug!.DroppedRecords != 0) throw new InvalidOperationException("Turn random trace truncated.");
            string Card(InstanceId card) => Field(card, "Card", "SourceEntry").AsString();
            var rng = s.Random.Capture(); ulong[] Words(string name) => (rng.Streams.SingleOrDefault(x => x.Name == name)?.BackendState ?? new Xoshiro256StarStar(rng.ExplicitSeeds[name], false).Capture()).ToArray();
            var calls = s.Debug.Capture().Where(r => r.Kind == SessionDebugKind.RandomCall).Select(r => { var d = r.Random!; if (d.CandidatesTruncated) throw new InvalidOperationException("Turn RNG candidates truncated."); return new Call(r.Subject!, d.Operation, d.Minimum, d.Maximum, d.Result, d.Before, d.After, d.Candidates?.Select(c => r.Subject == "base-spire:run:combat_targets" ? "enemy:" + Array.IndexOf(All("Enemy"), c) : Card(c)).ToArray()); }).ToArray();
            return new(at, Field(player, "Player", "TurnNumber").AsInt(), Field(player, "Player", "Energy").AsInt(), Field(player, "Player", "Hp").AsInt(), Field(player, "Player", "MaxHp").AsInt(), Field(player, "Player", "Block").AsInt(), Field(target, "Enemy", "Hp").AsInt(), Field(target, "Enemy", "Hp").AsInt() == 0,
                new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay" }.Select(p => Pile(p).Select(Card).ToArray()).ToArray(),
                Powers(false), Powers(true),
                Pile("DamageResults").Where(d => Field(d, "DamageResult", "Player") == Value.OfRef(player)).Sum(d => Field(d, "DamageResult", "Blocked").AsInt()),
                s.Random.DrawCount("base-spire:run:monster_ai"), Words("base-spire:run:monster_ai"), s.Random.DrawCount("base-spire:run:shuffle"), Words("base-spire:run:shuffle"), s.Random.DrawCount("base-spire:run:combat_targets"), Words("base-spire:run:combat_targets"), calls,
                All("PhaseOrderEntry").Where(e => !Field(e, "PhaseOrderEntry", "Direct").AsBool()).Select(e => Field(e, "PhaseOrderEntry", "Label").AsString()).ToArray(),
                All("PhaseOrderEntry").Where(e => Field(e, "PhaseOrderEntry", "Direct").AsBool()).Select(e => Field(e, "PhaseOrderEntry", "Label").AsString()).ToArray());
        }
        Power[] Powers(bool enemy) => Pile("Powers").Where(p => Field(p, "Power", enemy ? "HolderEnemy" : "HolderPlayer") == Value.OfRef(enemy ? target : player))
            .Select(p => new Power(Field(p, "Power", "DefinitionId").AsString()[11..], Field(p, "Power", "Stacks").AsInt(), !Field(p, "Power", "ApplierPlayer").IsNone ? "player" : !Field(p, "Power", "ApplierEnemy").IsNone ? "enemy" : "none")).ToArray();
    }

    private static Boundary[] Native(string seed, string plan)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered(); var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(plan == "starting-draw" ? 970 : 1000);
        if (plan.StartsWith("orichalcum", StringComparison.Ordinal)) { RelicCmd.Obtain(ModelDb.Relic<Orichalcum>(), player).GetAwaiter().GetResult(); if (plan == "orichalcum-duplicate") RelicCmd.Obtain(ModelDb.Relic<Orichalcum>(), player).GetAwaiter().GetResult(); }
        else if (plan.StartsWith("charon", StringComparison.Ordinal)) RelicCmd.Obtain(ModelDb.Relic<CharonsAshes>(), player).GetAwaiter().GetResult();
        else if (plan == "listener-order") { RelicCmd.Obtain(ModelDb.Relic<Orichalcum>(), player).GetAwaiter().GetResult(); RelicCmd.Obtain(ModelDb.Relic<Lantern>(), player).GetAwaiter().GetResult(); }
        else if (plan == "fan-ending") RelicCmd.Obtain(ModelDb.Relic<OrnamentalFan>(), player).GetAwaiter().GetResult();
        else if (plan == "bag-first-side") RelicCmd.Obtain(ModelDb.Relic<BagOfMarbles>(), player).GetAwaiter().GetResult();
        else if (plan == "gremlin-horn-ending") RelicCmd.Obtain(ModelDb.Relic<GremlinHorn>(), player).GetAwaiter().GetResult();
        else if (plan is "draw-before-block" or "block-before-draw" or "fiend-fire-ending" or "feed-ending") { }
        else
        {
            RelicCmd.Obtain(ModelDb.Relic<BloodVial>(), player).GetAwaiter().GetResult(); RelicCmd.Obtain(ModelDb.Relic<Lantern>(), player).GetAwaiter().GetResult();
            foreach (var canonical in new CardModel[] { ModelDb.Card<StrikeIronclad>(), ModelDb.Card<DefendIronclad>(), ModelDb.Card<Bash>() }) { var card = (CardModel)canonical.MutableClone(); card.AssignOwner(player); player.Deck.AddInternal(card); }
        }
        var combat = new CombatState(run); combat.AddMonster((MonsterModel)ModelDb.Monster<FuzzyWurmCrawler>().MutableClone(), CombatSide.Enemy);
        var target = combat.SpawnedEnemies.Single(); target.SetMaxHpInternal(1000); target.SetCurrentHpInternal(1000);
        // Each card definition occurs once in the shuffled fixtures, so names identify the
        // source's own combat copies without borrowing an identity mapping from the DSL.
        using var trace = new ReferenceRandomTrace(run.Rng, player, card => card.Id.Entry, initialShuffle: plan == "starting-draw",
            indexCandidates: (stream, count) => stream == "base-spire:run:combat_targets" ? combat.HittableEnemies.Select(e => "enemy:" + combat.Enemies.ToList().IndexOf(e)).ToArray() : null);
        var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult(); trace.VerifyShuffle();
        if (plan is "starting-draw" or "bag-first-side") return [Snapshot("started")];
        if (plan == "listener-order")
        {
            PowerCmd.Apply<StrengthPower>(combat, player.Creature, 3, null, null).GetAwaiter().GetResult(); PowerCmd.Apply<DarkEmbracePower>(combat, player.Creature, 1, null, null).GetAwaiter().GetResult(); PowerCmd.Apply<WeakPower>(combat, target, 2, null, null).GetAwaiter().GetResult();
            var card = (CardModel)ModelDb.Card<StrikeIronclad>().MutableClone(); card.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(card); return [Snapshot("ordered")];
        }
        if (plan == "fan-ending")
        {
            target.SetCurrentHpInternal(18); var before = Snapshot("configured");
            for (var i = 0; i < 3; i++) { var card = (CardModel)ModelDb.Card<StrikeIronclad>().MutableClone(); card.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(card); engine.PlayCardAsync(player, card, target).GetAwaiter().GetResult(); }
            return [before, Snapshot("played")];
        }
        if (plan == "feed-ending")
        {
            target.SetCurrentHpInternal(10); var feed = (CardModel)ModelDb.Card<Feed>().MutableClone(); feed.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(feed);
            var before = Snapshot("configured"); engine.PlayCardAsync(player, feed, target).GetAwaiter().GetResult(); return [before, Snapshot("played")];
        }
        if (plan == "gremlin-horn-ending")
        {
            target.SetCurrentHpInternal(6); var top = (CardModel)ModelDb.Card<StrikeIronclad>().MutableClone(); top.AssignOwner(player); player.PlayerCombatState!.DrawPile.AddInternal(top);
            var strike = (CardModel)ModelDb.Card<StrikeIronclad>().MutableClone(); strike.AssignOwner(player); player.PlayerCombatState.Hand.AddInternal(strike);
            var before = Snapshot("configured"); engine.PlayCardAsync(player, strike, target).GetAwaiter().GetResult(); return [before, Snapshot("played")];
        }
        if (plan.StartsWith("orichalcum", StringComparison.Ordinal))
        {
            PowerCmd.Apply<PlatingPower>(combat, player.Creature, 5, null, null).GetAwaiter().GetResult(); PowerCmd.Apply<StrengthPower>(combat, target, 10, null, null).GetAwaiter().GetResult();
            var before = Snapshot("configured"); engine.EndPlayerTurnAsync().GetAwaiter().GetResult(); trace.VerifyShuffle(); return [before, Snapshot("next-turn")];
        }
        if (plan == "block-before-draw") PowerCmd.Apply<FeelNoPainPower>(combat, player.Creature, 3, null, null).GetAwaiter().GetResult();
        PowerCmd.Apply<DarkEmbracePower>(combat, player.Creature, 1, null, null).GetAwaiter().GetResult();
        if (plan == "fiend-fire-ending")
        {
            target.SetCurrentHpInternal(7); for (var i = 0; i < 2; i++) { var top = (CardModel)ModelDb.Card<Anger>().MutableClone(); top.AssignOwner(player); player.PlayerCombatState!.DrawPile.AddInternal(top); }
            var fire = (CardModel)ModelDb.Card<FiendFire>().MutableClone(); fire.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(fire);
            var defend = (CardModel)ModelDb.Card<DefendIronclad>().MutableClone(); defend.AssignOwner(player); player.PlayerCombatState.Hand.AddInternal(defend);
            var before = Snapshot("configured"); engine.PlayCardAsync(player, fire, target).GetAwaiter().GetResult(); return [before, Snapshot("played")];
        }
        if (plan is "draw-before-block" or "block-before-draw")
        {
            if (plan == "draw-before-block") PowerCmd.Apply<FeelNoPainPower>(combat, player.Creature, 3, null, null).GetAwaiter().GetResult();
            PowerCmd.Apply<HellraiserPower>(combat, player.Creature, 1, null, null).GetAwaiter().GetResult(); player.Creature.GainBlockInternal(30);
        }
        target.SetCurrentHpInternal(plan is "draw-before-block" or "block-before-draw" ? 1 : plan == "charon-lethal" ? 3 : 6);
        foreach (var canonical in plan == "charon-reshuffle" ? new CardModel[] { ModelDb.Card<Bash>(), ModelDb.Card<StrikeIronclad>(), ModelDb.Card<Offering>() } : [ModelDb.Card<StrikeIronclad>()])
        {
            var card = (CardModel)canonical.MutableClone(); card.AssignOwner(player); (plan == "charon-reshuffle" ? player.PlayerCombatState!.DiscardPile : player.PlayerCombatState!.DrawPile).AddInternal(card);
        }
        var victim = (CardModel)ModelDb.Card<DefendIronclad>().MutableClone(); victim.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(victim);
        var start = Snapshot("configured"); CardPileCmd.Exhaust(combat, victim).GetAwaiter().GetResult(); trace.VerifyShuffle(); return [start, Snapshot("exhausted")];

        Boundary Snapshot(string at)
        {
            var random = trace.Capture(); if (random.Truncated || random.Dropped != 0) throw new InvalidOperationException("Source turn RNG trace incomplete: " + seed + ":" + plan + ":" + at + " " + JsonSerializer.Serialize(random));
            var ai = run.Rng.MonsterAi.ToSerializable(); var shuffle = run.Rng.Shuffle.ToSerializable(); var targets = run.Rng.CombatTargets.ToSerializable(); var state = player.PlayerCombatState!;
            return new(at, state.TurnNumber, state.Energy, player.Creature.CurrentHp, player.Creature.MaxHp, player.Creature.Block, target.CurrentHp, combat.IsOverOrEnding(),
                new[] { state.Hand, state.DrawPile, state.DiscardPile, state.ExhaustPile, state.PlayPile }.Select(p => p.Cards.Select(c => c.Id.Entry).ToArray()).ToArray(),
                Powers(player.Creature), Powers(target),
                (long)combat.DamageHistory.Entries.Where(d => d.Receiver == player.Creature).Sum(d => d.Result.BlockedDamage), ai.counter, [ai.state0, ai.state1, ai.state2, ai.state3], shuffle.counter, [shuffle.state0, shuffle.state1, shuffle.state2, shuffle.state3], targets.counter, [targets.state0, targets.state1, targets.state2, targets.state3],
                random.Calls.Select(c => new Call(c.Stream, c.Operation, c.Minimum, c.Maximum, c.Result, c.Before, c.After, c.Candidates?.ToArray())).ToArray(), Order(false), Order(true));
        }
        Power[] Powers(Creature owner) => owner.Powers.Select(p => new Power(Regex.Replace(p.GetType().Name, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant(), p.Amount, ReferenceEquals(p.Applier, player.Creature) ? "player" : ReferenceEquals(p.Applier, target) ? "enemy" : "none")).ToArray();
        string[] Order(bool direct)
        {
            if (plan != "listener-order") return [];
            var method = typeof(Sts2Sim.Core.Hooks.Hook).GetMethod(direct ? "IterateDirectCombatHookListeners" : "IterateCombatHookListeners", BindingFlags.Static | BindingFlags.NonPublic, [typeof(ICombatState)])
                ?? throw new NotSupportedException("Locked source listener composition is unavailable.");
            var models = (IEnumerable<AbstractModel>)method.Invoke(null, [combat])!;
            string Name(AbstractModel model) => Regex.Replace(model.GetType().Name, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
            return models.Select(model => model switch {
                PowerModel p => "power:" + (p.Owner == player.Creature ? "player:" : "enemy:") + Name(p),
                RelicModel r => "relic:" + Name(r), CardModel c => "card:" + c.Id.Entry,
                MonsterModel m => "enemy:" + Name(m), _ => throw new NotSupportedException("Unmapped listener in the controlled order fixture: " + model.GetType().Name)
            }).ToArray();
        }
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }

    private sealed class Probe : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "turn-phases-" + Guid.NewGuid());
        internal Probe(string repository)
        {
            var original = Path.Combine(repository, "mod-the-spire/base-spire");
            foreach (var source in Directory.GetFiles(original, "*", SearchOption.AllDirectories)) { var target = Path.Combine(Root, Path.GetRelativePath(original, source)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target); }
            var entry = Path.Combine(Root, "base.spire"); File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"turn-phase-probe.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "turn-phase-probe.spire"), """
                using namespace base-spire;
                dsl_version = "core/0.3"
                entity PhaseOrderEntry { fields = { Label = { type = String(length = 128) } Direct = { type = Bool } } }
                event PhaseOrder { payload = { owner = Ref(Player) direct = Bool } }
                procedure PhasePowerOrder { parameters = { owner = Ref(Player) direct = Bool listener = Ref(Power) } locals = { holder = String(length = 8) entry = Binding } body = {
                  let = { into = holder value = "player:" }
                  if = { condition = not(isNone(field(listener, Power.HolderEnemy))) then = { let = { into = holder value = "enemy:" } } }
                  create = { type = PhaseOrderEntry scope = entityScope(owner) into = entry fields = { Direct = direct Label = concat("power:", concat(holder, substring(field(listener, Power.DefinitionId), 11))) } }
                } }
                procedure PhaseRelicOrder { parameters = { owner = Ref(Player) direct = Bool listener = Ref(Relic) } locals = { entry = Binding } body = {
                  create = { type = PhaseOrderEntry scope = entityScope(owner) into = entry fields = { Direct = direct Label = concat("relic:", substring(field(listener, Relic.DefinitionId), 11)) } }
                } }
                procedure PhaseCardOrder { parameters = { owner = Ref(Player) direct = Bool listener = Ref(Card) } locals = { entry = Binding } body = {
                  create = { type = PhaseOrderEntry scope = entityScope(owner) into = entry fields = { Direct = direct Label = concat("card:", field(listener, Card.SourceEntry)) } }
                } }
                procedure PhaseEnemyOrder { parameters = { owner = Ref(Player) direct = Bool listener = Ref(Enemy) } locals = { entry = Binding } body = {
                  create = { type = PhaseOrderEntry scope = entityScope(owner) into = entry fields = { Direct = direct Label = concat("enemy:", substring(field(listener, Enemy.DefinitionId), 11)) } }
                } }
                subscription PhasePowers { event = PhaseOrder reaction = PhasePowerOrder instances = collection(Powers) instance_order = pureCall(PowerHookOrder, listener, direct) priority = 0 }
                subscription PhaseRelics { event = PhaseOrder reaction = PhaseRelicOrder instances = where(collection(Relics), r, not(field(r, Relic.IsMelted))) instance_order = pureCall(RelicHookOrder, listener, direct) priority = 0 }
                subscription PhaseCards { event = PhaseOrder reaction = PhaseCardOrder instances = collection(Hand) instance_order = pureCall(CardHookOrder, listener, direct) priority = 0 }
                subscription PhaseEnemies { event = PhaseOrder reaction = PhaseEnemyOrder instances = type(Enemy) instance_order = pureCall(EnemyHookOrder, listener, direct) priority = 0 }
                action PhaseRunOrder { role = player body = { emit = { event = PhaseOrder payload = { owner = first(type(Player)) direct = false } } } }
                action PhaseDirectOrder { role = player body = { emit = { event = PhaseOrder payload = { owner = first(type(Player)) direct = true } } } }
                action PhaseOrderSetup { role = player body = {
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = 3 } }
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:dark-embrace-power" stacks = 1 } }
                  call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:weak-power" stacks = 2 } }
                } }
                action PhaseExtraOrichalcum { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:orichalcum" } } } }
                action PhasePlating { role = player body = {
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:plating-power" stacks = 5 } }
                  call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:strength-power" stacks = 10 } }
                } }
                action PhaseDarkEmbrace { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:dark-embrace-power" stacks = 1 } } } }
                action PhaseFeelNoPain { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:feel-no-pain-power" stacks = 3 } } } }
                action PhaseHellraiser { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:hellraiser-power" stacks = 1 } } } }
                action PhasePlay { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = {
                  set = { target = first(type(Player)) field = Player.EffectDepth value = add(field(first(type(Player)), Player.EffectDepth), 1) }
                  call = { procedure = PlayAttackCard args = { owner = first(type(Player)) card = card } }
                  set = { target = first(type(Player)) field = Player.EffectDepth value = subtract(field(first(type(Player)), Player.EffectDepth), 1) }
                } }
                action PhaseExhaust { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = {
                  set = { target = first(type(Player)) field = Player.EffectDepth value = add(field(first(type(Player)), Player.EffectDepth), 1) }
                  call = { procedure = ExhaustCombatCard args = { owner = first(type(Player)) card = card causedByEthereal = false } }
                  set = { target = first(type(Player)) field = Player.EffectDepth value = subtract(field(first(type(Player)), Player.EffectDepth), 1) }
                } }
                """);
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root);
            if (Path.GetDirectoryName(path) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || !Path.GetFileName(path).StartsWith("turn-phases-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected turn probe cleanup path.");
            Directory.Delete(path, true);
        }
    }
}
