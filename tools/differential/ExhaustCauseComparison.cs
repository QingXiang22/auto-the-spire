using System.Text.Json;
using System.Text.RegularExpressions;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal static class ExhaustCauseComparison
{
    private static readonly string[] Plans = ["ordinary", "deferred", "current-amount", "no-draw-before", "no-draw-after", "full-hand", "reshuffle", "nested", "remove-replace", "generated", "immediate-listeners", "natural", "natural-effect", "natural-lethal", "paused"];
    private sealed record Call(string Stream, string Operation, string Minimum, string Maximum, string Result, long Before, long After, string[]? Candidates);
    private sealed record Power(string Definition, long Amount);
    private sealed record Draw(string Card, long Deferred);
    private sealed record Boundary(string At, long Turn, long Energy, long Hp, long Block, long EnemyHp, bool Ending, string[][] Piles, Power[] Powers, long[] Deferred, Draw[] Draws,
        long AiDraws, ulong[] AiState, long ShuffleDraws, ulong[] ShuffleState, long TargetDraws, ulong[] TargetState, Call[] Calls);

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use exhaust-cause-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository); using var probe = new Probe(repository);
        var package = DslPackage.Load(Path.Combine(probe.Root, "package.json")); var cases = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" })
        foreach (var plan in Plans)
        {
            Boundary[] left, right;
            try { left = Dsl(package, seed, plan); } catch (Exception e) { throw new InvalidOperationException("DSL " + seed + ":" + plan + ": " + e.Message, e); }
            try { right = Native(seed, plan); } catch (Exception e) { throw new InvalidOperationException("Source " + seed + ":" + plan + ": " + e.Message, e); }
            if (left.Length != (plan.StartsWith("natural", StringComparison.Ordinal) ? 2 : 3) || right.Length != left.Length) throw new InvalidOperationException("Exhaust cause comparison omitted command boundaries.");
            var same = JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right); passed &= same;
            cases.Add(new { id = seed + ":" + plan, status = same ? "passed" : "diverged", left, right });
        }
        var report = new { schema = "dsl-migration/exhaust-causes@1", reference,
            package_digest = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json")).Digest, probe_digest = package.Digest,
            status = passed ? "passed" : "diverged", independent_verified = passed, normal_run_verified = false, scope = "controlled-component",
            controlled_setup = "Real FuzzyWurmCrawler combat, empty permanent decks, both HP1000. Controlled card placement has unique creation slots on each side. Strike/Bash/Infection receive the Ethereal keyword on both sides; nested Strike also receives Exhaust. All exhaustion, power lifecycle and draws use actual commands. Natural cases use the real end-turn hand pipeline, comparing the read-only source EnemyTurnStarting boundary with a DSL administrative pause after side-end listeners. Lethal pauses precede reward settlement. Paused deferred draw compares the source first CardDrawn observer, then resumes DSL once. Compare all five ordered piles, current power amounts/private deferred counters, resource/ending state, each draw and its already-cleared counter, full three random streams plus calls/candidates. No reference rewrite, RNG reset, injected rule callback, unsupported-pool reduction or normal Run certification.", cases };
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
        int Collection(string name) => s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id;
        InstanceId[] Pile(string name) => s.State.CollectionMembers(Collection(name)).ToArray();
        void Submit(string name, params InstanceId[] refs) { var a = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name); s.Submit(a.Definition, s.Participant(0), refs.Select(Value.OfRef).ToArray()); if (s.Status is not (SessionStatus.Running or SessionStatus.AwaitingDecision)) throw new InvalidOperationException(name + ": " + s.Fault); }
        var slots = new Dictionary<InstanceId, string>();
        InstanceId Add(string name, string pile = "DrawPile")
        {
            var before = All("Card").ToHashSet(); Submit(name + ".AddToHand"); var card = All("Card").Single(c => !before.Contains(c));
            slots.Add(card, "c" + slots.Count.ToString("D2") + ":" + Field(card, "Card", "SourceEntry").AsString());
            if (pile != "Hand") s.State.RunAtomic(() => { foreach (var from in new[] { "Hand", "DiscardPile" }) if (Pile(from).Contains(card)) s.State.RemoveFromCollection(Collection(from), card); s.State.AddToCollection(Collection(pile), card); });
            return card;
        }
        var player = All("Player").Single(); Set(player, "Player", "UsesRunDeck", Value.Of(true)); Set(player, "Player", "AutoDrawPerTurn", Value.Of(5L));
        Set(player, "Player", "MaxHp", Value.Of(1000L)); Set(player, "Player", "Hp", Value.Of(1000L)); Submit("BurningBlood.Obtain");
        if (plan is "immediate-listeners" or "natural-lethal") Submit("CharonsAshes.Obtain");
        Submit("FuzzyWurmCrawler.Spawn"); var enemy = All("Enemy").Single(); Set(enemy, "Enemy", "MaxHp", Value.Of(1000L)); Set(enemy, "Enemy", "Hp", Value.Of(1000L));
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn"); Submit("CauseInitialize"); var probe = All("CauseProbe").Single();
        if (plan == "natural-lethal") Set(enemy, "Enemy", "Hp", Value.Of(3L));
        if (plan == "no-draw-before") Submit("CauseNoDraw"); Submit("CauseDark"); if (plan == "no-draw-after") Submit("CauseNoDraw");
        if (plan == "nested") Submit("CauseHellraiser"); if (plan == "immediate-listeners") Submit("CauseFeel");
        if (plan == "full-hand") for (var i = 0; i < 9; i++) Add("Defend", "Hand");
        if (plan == "reshuffle") foreach (var name in new[] { "Defend", "Anger", "Offering" }) Add(name, "DiscardPile");
        else if (plan == "nested") { var strike = Add("Strike"); Set(strike, "Card", "Exhausts", Value.Of(true)); Add("Defend"); Add("Anger"); }
        else { Add("Defend"); Add("Anger"); Add("Offering"); }
        var victim = Add(plan == "natural-effect" ? "Infection" : "Strike", "Hand");
        var second = plan is "deferred" or "current-amount" or "natural" or "natural-lethal" or "paused" ? Add("Bash", "Hand") : (InstanceId?)null;
        var start = Snapshot("configured");
        if (plan.StartsWith("natural", StringComparison.Ordinal))
        {
            Set(probe, "CauseProbe", "PauseEnd", Value.Of(true)); Submit("EndDevelopmentTurn");
            if (s.Status != SessionStatus.AwaitingDecision) throw new InvalidOperationException("Natural half-turn must pause before enemy/rewards.");
            var end = Snapshot("half-turn"); return [start, end];
        }
        if (plan == "generated") Add("Defend", "ExhaustPile");
        else
        {
            Submit(plan == "ordinary" ? "CauseOrdinary" : "CauseEthereal", victim); if (second is { } v) Submit("CauseEthereal", v);
            if (plan == "full-hand") Add("Defend", "Hand");
            if (plan == "current-amount") { Set(probe, "CauseProbe", "Amount", Value.Of(2L)); Submit("CauseIncrease"); }
            if (plan == "remove-replace") { Submit("CauseRemoveDark"); Submit("CauseDark"); }
        }
        var middle = Snapshot("exhausted");
        if (plan == "paused") Set(probe, "CauseProbe", "PauseDraw", Value.Of(true));
        Submit("CauseSideEnd");
        if (plan == "paused")
        {
            if (s.Status != SessionStatus.AwaitingDecision) throw new InvalidOperationException("Deferred draw did not pause.");
            middle = Snapshot("first-draw"); var hash = s.State.StateHash(); var draws = s.Random.TotalDraws;
            s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
            if (hash != s.State.StateHash() || draws != s.Random.TotalDraws) throw new InvalidOperationException("Paused queries mutated deferred draw.");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true)); if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Deferred continuation failed.");
        }
        return [start, middle, Snapshot("side-end")];

        Boundary Snapshot(string at)
        {
            if (s.Debug!.DroppedRecords != 0) throw new InvalidOperationException("Random trace truncated.");
            var rng = s.Random.Capture(); ulong[] Words(string name) => (rng.Streams.SingleOrDefault(x => x.Name == name)?.BackendState ?? new Xoshiro256StarStar(rng.ExplicitSeeds[name], false).Capture()).ToArray();
            var calls = s.Debug.Capture().Where(r => r.Kind == SessionDebugKind.RandomCall).Select(r => { var d = r.Random!; if (d.CandidatesTruncated) throw new InvalidOperationException("RNG candidates truncated."); return new Call(r.Subject!, d.Operation, d.Minimum, d.Maximum, d.Result, d.Before, d.After, d.Candidates?.Select(c => r.Subject == "base-spire:run:combat_targets" ? "enemy:" + Array.IndexOf(All("Enemy"), c) : slots[c]).ToArray()); }).ToArray();
            return new(at, Field(player, "Player", "TurnNumber").AsInt(), Field(player, "Player", "Energy").AsInt(), Field(player, "Player", "Hp").AsInt(), Field(player, "Player", "Block").AsInt(), Field(enemy, "Enemy", "Hp").AsInt(), Field(enemy, "Enemy", "Hp").AsInt() == 0,
                new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay" }.Select(p => Pile(p).Select(c => slots[c]).ToArray()).ToArray(),
                Pile("Powers").Where(p => Field(p, "Power", "HolderPlayer") == Value.OfRef(player)).Select(p => new Power(Field(p, "Power", "DefinitionId").AsString()[11..], Field(p, "Power", "Stacks").AsInt())).ToArray(),
                All("DarkEmbracePower.State").Select(p => Field(p, "DarkEmbracePower.State", "EtherealExhausts").AsInt()).ToArray(),
                All("CauseDraw").Select(d => new Draw(slots[Field(d, "CauseDraw", "Card").AsRef()], Field(d, "CauseDraw", "Deferred").AsInt())).ToArray(),
                s.Random.DrawCount("base-spire:run:monster_ai"), Words("base-spire:run:monster_ai"), s.Random.DrawCount("base-spire:run:shuffle"), Words("base-spire:run:shuffle"), s.Random.DrawCount("base-spire:run:combat_targets"), Words("base-spire:run:combat_targets"), calls);
        }
    }

    private static Boundary[] Native(string seed, string plan)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered(); var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000);
        if (plan is "immediate-listeners" or "natural-lethal") RelicCmd.Obtain(ModelDb.Relic<CharonsAshes>(), player).GetAwaiter().GetResult();
        var combat = new CombatState(run); combat.AddMonster((MonsterModel)ModelDb.Monster<FuzzyWurmCrawler>().MutableClone(), CombatSide.Enemy);
        var enemy = combat.SpawnedEnemies.Single(); enemy.SetMaxHpInternal(1000); enemy.SetCurrentHpInternal(1000);
        var slots = new Dictionary<CardModel, string>(); var draws = new List<Draw>(); Boundary? half = null, firstDraw = null;
        long[] Deferred() => player.Creature.Powers.OfType<DarkEmbracePower>().Select(p => (long)(int)typeof(DarkEmbracePower).GetField("_etherealExhausts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(p)!).ToArray();
        using var trace = new ReferenceRandomTrace(run.Rng, player, card => slots[card], false,
            indexCandidates: (stream, count) => stream == "base-spire:run:combat_targets" ? combat.HittableEnemies.Select(e => "enemy:" + combat.Enemies.ToList().IndexOf(e)).ToArray() : null);
        var observerType = typeof(CombatEngine).Assembly.GetType("Sts2Sim.Core.Combat.ICombatObserver")!;
        var observer = DispatchProxy.Create(observerType, typeof(NormalReferenceObserver));
        ((NormalReferenceObserver)observer).Record = (method, args) => {
            if (method == "CardDrawn") { draws.Add(new(slots[(CardModel)args[0]!], Deferred().Sum())); if (plan == "paused" && firstDraw is null) firstDraw = Snapshot("first-draw"); }
            if (method == "EnemyTurnStarting") half = Snapshot("half-turn");
        };
        var engine = (CombatEngine)Activator.CreateInstance(typeof(CombatEngine), BindingFlags.Instance | BindingFlags.NonPublic, null, [combat, observer], null)!;
        engine.StartCombatAsync().GetAwaiter().GetResult();
        if (plan == "natural-lethal") enemy.SetCurrentHpInternal(3);
        void Apply<T>(int n = 1) where T : PowerModel => PowerCmd.Apply<T>(combat, player.Creature, n, null, null).GetAwaiter().GetResult();
        if (plan == "no-draw-before") Apply<NoDrawPower>(); Apply<DarkEmbracePower>(); if (plan == "no-draw-after") Apply<NoDrawPower>();
        if (plan == "nested") Apply<HellraiserPower>(); if (plan == "immediate-listeners") Apply<FeelNoPainPower>(3);
        CardModel Add(CardModel canonical, string pile = "draw")
        {
            var card = (CardModel)canonical.MutableClone(); card.AssignOwner(player); slots.Add(card, "c" + slots.Count.ToString("D2") + ":" + card.Id.Entry);
            if (card is StrikeIronclad or Bash or Infection) typeof(CardModel).GetMethod("AddKeywordInternal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(card, [CardKeyword.Ethereal]);
            var state = player.PlayerCombatState!; (pile switch { "hand" => state.Hand, "discard" => state.DiscardPile, "exhaust" => state.ExhaustPile, _ => state.DrawPile }).AddInternal(card); return card;
        }
        if (plan == "full-hand") for (var i = 0; i < 9; i++) Add(ModelDb.Card<DefendIronclad>(), "hand");
        if (plan == "reshuffle") foreach (var c in new CardModel[] { ModelDb.Card<DefendIronclad>(), ModelDb.Card<Anger>(), ModelDb.Card<Offering>() }) Add(c, "discard");
        else if (plan == "nested") { var strike = Add(ModelDb.Card<StrikeIronclad>()); typeof(CardModel).GetMethod("AddKeywordInternal", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(strike, [CardKeyword.Exhaust]); Add(ModelDb.Card<DefendIronclad>()); Add(ModelDb.Card<Anger>()); }
        else { Add(ModelDb.Card<DefendIronclad>()); Add(ModelDb.Card<Anger>()); Add(ModelDb.Card<Offering>()); }
        var victim = Add(plan == "natural-effect" ? ModelDb.Card<Infection>() : ModelDb.Card<StrikeIronclad>(), "hand");
        var second = plan is "deferred" or "current-amount" or "natural" or "natural-lethal" or "paused" ? Add(ModelDb.Card<Bash>(), "hand") : null;
        var start = Snapshot("configured");
        if (plan.StartsWith("natural", StringComparison.Ordinal))
        {
            engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
            var end = half ?? (plan == "natural-lethal" ? Snapshot("half-turn") : throw new InvalidOperationException("Missing source half-turn observation."));
            return [start, end];
        }
        if (plan == "generated") Add(ModelDb.Card<DefendIronclad>(), "exhaust");
        else
        {
            CardPileCmd.Exhaust(combat, victim, plan != "ordinary").GetAwaiter().GetResult(); if (second is not null) CardPileCmd.Exhaust(combat, second, true).GetAwaiter().GetResult();
            if (plan == "full-hand") Add(ModelDb.Card<DefendIronclad>(), "hand");
            if (plan == "current-amount") PowerCmd.ModifyAmount(combat, player.Creature.Powers.OfType<DarkEmbracePower>().Single(), 2, null, null).GetAwaiter().GetResult();
            if (plan == "remove-replace") { PowerCmd.Remove(player.Creature.Powers.OfType<DarkEmbracePower>().Single()).GetAwaiter().GetResult(); Apply<DarkEmbracePower>(); }
        }
        var middle = Snapshot("exhausted"); Hook.AfterSideTurnEnd(combat, CombatSide.Player, [player.Creature]).GetAwaiter().GetResult(); trace.VerifyShuffle();
        return [start, plan == "paused" ? firstDraw ?? throw new InvalidOperationException("Missing source first draw.") : middle, Snapshot("side-end")];

        Boundary Snapshot(string at)
        {
            var random = trace.Capture(); if (random.Truncated || random.Dropped != 0) throw new InvalidOperationException("Source RNG trace incomplete.");
            var ai = run.Rng.MonsterAi.ToSerializable(); var shuffle = run.Rng.Shuffle.ToSerializable(); var targets = run.Rng.CombatTargets.ToSerializable(); var state = player.PlayerCombatState!;
            return new(at, state.TurnNumber, state.Energy, player.Creature.CurrentHp, player.Creature.Block, enemy.CurrentHp, combat.IsOverOrEnding(),
                new[] { state.Hand, state.DrawPile, state.DiscardPile, state.ExhaustPile, state.PlayPile }.Select(p => p.Cards.Select(c => slots[c]).ToArray()).ToArray(),
                player.Creature.Powers.Select(p => new Power(Regex.Replace(p.GetType().Name, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant(), p.Amount)).ToArray(), Deferred(), draws.ToArray(),
                ai.counter, [ai.state0, ai.state1, ai.state2, ai.state3], shuffle.counter, [shuffle.state0, shuffle.state1, shuffle.state2, shuffle.state3], targets.counter, [targets.state0, targets.state1, targets.state2, targets.state3],
                random.Calls.Select(c => new Call(c.Stream, c.Operation, c.Minimum, c.Maximum, c.Result, c.Before, c.After, c.Candidates?.ToArray())).ToArray());
        }
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }

    private sealed class Probe : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "exhaust-causes-" + Guid.NewGuid());
        internal Probe(string repository)
        {
            var original = Path.Combine(repository, "mod-the-spire/base-spire");
            foreach (var source in Directory.GetFiles(original, "*", SearchOption.AllDirectories)) { var target = Path.Combine(Root, Path.GetRelativePath(original, source)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target); }
            var entry = Path.Combine(Root, "base.spire"); File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"exhaust-cause-probe.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "exhaust-cause-probe.spire"), "using namespace base-spire;\ndsl_version = \"core/0.3\"\n" + File.ReadAllText(Path.Combine(repository, "tools/base-spire-dev/fixtures/exhaust-cause-probe.spire")) + File.ReadAllText(Path.Combine(repository, "tools/differential/exhaust-cause-observer.spire")));
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root);
            if (Path.GetDirectoryName(path) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || !Path.GetFileName(path).StartsWith("exhaust-causes-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected exhaust probe cleanup path.");
            Directory.Delete(path, true);
        }
    }
}

