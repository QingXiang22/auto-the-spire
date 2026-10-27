using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Entities.Rngs;
#endif

namespace Differential;

internal static class CombatBoundaryComparison
{
    private static readonly string[] Plans = ["turn-return", "paid-card", "two-end-effects", "two-end-effects-paused", "feed-ending", "ending-move", "anger-ending", "zero-card", "free-x", "repeat-card", "corruption"];
    private static readonly string[] Seeds = ["777", "A😀B"];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    internal sealed record Point(string Phase, string Card, long Turn, long Energy, long Hp, long MaxHp, long Block,
        long EnemyHp, string Move, bool Ending, string[] Piles, long Spent = 0);
    internal sealed record Trace(Point[] Points, string FinalState, object[] Raw);
    internal sealed record Difference(int Index, string Field, object? Left, object? Right);
    private sealed record HookSource(string Hook, int Overload, string Path, int Line, string BodySha256, string[] Iterators,
        bool ListenerSnapshot, bool ParticipantSnapshot, bool FreshEligibility, bool ExecutionFinished, string Body);

    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use combat-boundary-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository);
        using var probe = new Probe(repository);
        var package = DslPackage.Load(Path.Combine(probe.Root, "package.json"));
        var cases = new List<object>(); var differences = 0;
        foreach (var seed in Seeds)
        foreach (var plan in Plans)
        {
            var left = Dsl(package, seed, plan, true); var leftQuiet = Dsl(package, seed, plan, false);
            var right = Native(seed, plan, true); var rightQuiet = Native(seed, plan, false);
            if (left.FinalState != leftQuiet.FinalState || right.FinalState != rightQuiet.FinalState)
                throw new InvalidOperationException("Boundary diagnostics changed gameplay state or RNG: " + seed + ":" + plan);
            Validate(left); Validate(right);
            var difference = FirstDifference(left.Points, right.Points);
            if (difference is not null) differences++;
            cases.Add(new { id = seed + ":" + plan, status = difference is null ? "passed" : "diverged", first_difference = difference,
                action_difference = FirstDifference(left.Points.SkipWhile(p => p.Phase != "configured").ToArray(), right.Points.SkipWhile(p => p.Phase != "configured").ToArray()),
                return_difference = FirstDifference([left.Points[^1]], [right.Points[^1]]),
                diagnostics_read_only = true, left, right });
        }
        var report = new { schema = "dsl-migration/combat-boundaries@1", reference,
            package_digest = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json")).Digest, probe_digest = package.Digest,
            adapter_sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(CombatBoundaryComparison).Assembly.Location))),
            status = differences == 0 ? "passed" : "diverged", executed = cases.Count, expected_case_ids = Seeds.SelectMany(s => Plans.Select(p => s + ":" + p)).ToArray(),
            complete = false, independent_verified = false, normal_run_verified = false, scope = "controlled-boundary-diagnostic",
            controlled_setup = "Empty permanent decks, real FuzzyWurmCrawler and actual production commands; both HP set to 1000. Paid-card creates Strike; two-end-effects creates two Infection in hand. Its paused variant holds the first real move notification administratively (DSL decide/native incomplete Task), then resumes once, without game effects. Ending-move uses a last-kill Strike followed by actual AddAsync to hand, distinguishing synchronous result placement from rejected async movement. Feed-ending uses Feed at enemy HP10 and holds DSL room settlement until after the outer card. The named native probe relic only observes real hooks; source declarations and raw notifications are retained, without claiming complete listener parity or full events.trace. Fixture card placement is outside action comparison; its DSL notifications remain in raw diagnostics. No missing normal turn is executed by the adapter. Initial HP sampling/niche and normal rooms are outside this probe. Duplicate labels preserve definition/order but do not certify instance identity. Enabled/disabled diagnostics compare the same-backend DSL full StateHash/RNG and native exact combat description/RNG.",
            hook_sources = HookSources(repository), cases };
        var text = JsonSerializer.Serialize(report, Json);
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), text); }
        Console.WriteLine(text); return differences == 0 ? 0 : 1;
    }

    internal static void Validate(Trace trace)
    {
        if (trace.Points.Length == 0 || string.IsNullOrWhiteSpace(trace.FinalState)) throw new InvalidOperationException("Missing boundary trace/state.");
        foreach (var phase in new[] { "combat-before", "player-before", "begin-return", "configured", "action-return" })
            if (!trace.Points.Any(p => p.Phase == phase)) throw new InvalidOperationException("Missing required boundary " + phase);
        if (trace.Points.Count(p => p.Phase == "action-return") != 1 || trace.Points[^1].Phase != "action-return")
            throw new InvalidOperationException("Missing or repeated real action return.");
        if (trace.Points.Any(p => p.Piles.Length != 5 || p.Piles.Any(v => v is null) || string.IsNullOrWhiteSpace(p.Phase)))
            throw new InvalidOperationException("Incomplete boundary fields.");
    }

    internal static Difference? FirstDifference(Point[] left, Point[] right)
    {
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            if (i >= left.Length || i >= right.Length) return new(i, "point", i < left.Length ? left[i] : null, i < right.Length ? right[i] : null);
            var l = JsonSerializer.SerializeToElement(left[i], Json); var r = JsonSerializer.SerializeToElement(right[i], Json);
            foreach (var field in l.EnumerateObject())
                if (!r.TryGetProperty(field.Name, out var value) || field.Value.GetRawText() != value.GetRawText())
                    return new(i, field.Name, field.Value.Clone(), r.TryGetProperty(field.Name, out value) ? value.Clone() : null);
        }
        return null;
    }

    internal static void SelfTest()
    {
        var point = new Point("combat-before", "", 1, 3, 100, 100, 0, 100, "FIRST", false, ["", "", "", "", ""]);
        var phases = new[] { "combat-before", "player-before", "begin-return", "configured", "action-return" };
        var trace = new Trace(phases.Select(p => point with { Phase = p }).ToArray(), "actual-state", []);
        Validate(trace);
        if (FirstDifference(trace.Points, trace.Points) is not null) throw new InvalidOperationException("Identical boundary trace differs.");
        foreach (var phase in phases)
            Reject(trace with { Points = trace.Points.Where(p => p.Phase != phase).ToArray() });
        Reject(trace with { Points = [] }); Reject(trace with { FinalState = "" });
        Reject(trace with { Points = trace.Points.Append(trace.Points[^1]).ToArray() });
        Reject(trace with { Points = trace.Points.Select(p => p with { Piles = [""] }).ToArray() });
        foreach (var changed in new[] { point with { Phase = "wrong-stage" }, point with { Energy = 2 }, point with { Spent = 2 }, point with { Move = "NEXT" },
            point with { Ending = true }, point with { Piles = ["INFECTION,", "", "", "", ""] } })
            if (FirstDifference([point], [changed]) is null) throw new InvalidOperationException("Missed boundary divergence.");
        if (FirstDifference([point], []) is null || FirstDifference([], [point]) is null) throw new InvalidOperationException("Missed missing notification.");
        Console.WriteLine("Combat boundary comparator rejects missing notifications, fields, wrong phases/piles/payment/moves/ending, zero execution and repeated action returns.");
        static void Reject(Trace value)
        {
            try { Validate(value); } catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("Accepted incomplete boundary evidence.");
        }
    }

    private static Trace Dsl(DslPackage package, string seed, string plan, bool capture)
    {
        var session = package.CreateSession(1, ["player"], seed, capture ? new SessionDebugOptions(MaxRecords: 100_000,
            MaxTextLength: 8192, CaptureValues: true, MaxSnapshotValues: 8192) : null); session.Start();
        InstanceId[] All(string type) => session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value Field(InstanceId i, string type, string field) => session.State.GetField(i, session.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        void Set(InstanceId i, string type, string field, long value) => session.State.RunAtomic(() => session.State.SetField(i, session.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), Value.Of(value)));
        void Submit(string name, params InstanceId[] refs)
        {
            var a = session.LegalActions(session.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
            session.Submit(a.Definition, session.Participant(0), refs.Select(Value.OfRef).ToArray());
            if (session.Status != SessionStatus.Running && !(plan == "two-end-effects-paused" && name == "EndTurn" && session.Status == SessionStatus.AwaitingDecision))
                throw new InvalidOperationException(name + ": " + session.Fault);
        }
        var player = All("Player").Single();
        session.State.RunAtomic(() => session.State.SetField(player, session.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("UsesRunDeck"), Value.Of(true)));
        Set(player, "Player", "MaxHp", 1000); Set(player, "Player", "Hp", 1000);
        Set(player, "Player", "AutoDrawPerTurn", 5);
        Submit("BurningBlood.Obtain"); Submit("FuzzyWurmCrawler.Spawn");
        var enemy = All("Enemy").Single(); Set(enemy, "Enemy", "MaxHp", 1000); Set(enemy, "Enemy", "Hp", 1000);
        var boundaries = new List<Point>();
        void Take(string phase)
        {
            string Pile(string name) => string.Concat(session.State.CollectionMembers(session.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id).Select(c => Field(c, "Card", "SourceEntry").AsString() + ","));
            boundaries.Add(new(phase, "", Field(player, "Player", "TurnNumber").AsInt(), Field(player, "Player", "Energy").AsInt(),
                Field(player, "Player", "Hp").AsInt(), Field(player, "Player", "MaxHp").AsInt(), Field(player, "Player", "Block").AsInt(),
                Field(enemy, "Enemy", "Hp").AsInt(), Field(enemy, "Enemy", "MoveId").AsString(), Field(enemy, "Enemy", "Hp").AsInt() == 0,
                new[] { "Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay" }.Select(Pile).ToArray()));
        }
        var raw = new List<object>(); long cursor = 0;
        void Flush(bool fixture = false)
        {
            if (!capture) return;
            if (session.Debug!.DroppedRecords != 0) throw new InvalidOperationException("Truncated DSL boundary diagnostics.");
            foreach (var record in session.Debug.Capture().Where(r => r.Sequence > cursor))
            {
                if (record.Subject == "base-spire:BoundaryPoint" && record.Kind == SessionDebugKind.EventEmit)
                {
                    var snapshot = record.Snapshot;
                    if (record.TextTruncated || snapshot is null || !snapshot.Complete || snapshot.Values.Count != 16) throw new InvalidOperationException("Incomplete DSL boundary snapshot.");
                    var v = snapshot.Values; string S(int i) => v[i].Scalar ?? throw new InvalidOperationException("Missing scalar.");
                    long N(int i) => long.Parse(S(i), CultureInfo.InvariantCulture);
                    var point = new Point(S(0), S(1), N(3), N(4), N(5), N(6), N(7), N(8), S(9), bool.Parse(S(10)), Enumerable.Range(11, 5).Select(S).ToArray(), N(2));
                    if (fixture) raw.Add(new { fixture = true, point }); else boundaries.Add(point);
                }
                else if (record.Kind is SessionDebugKind.EventEmit or SessionDebugKind.ListenerEnter)
                    raw.Add(new { record.Sequence, record.Kind, record.Subject, record.Detail, record.Source });
                cursor = record.Sequence;
            }
        }
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn"); Flush(); Take("begin-return");
        if (plan is "paid-card" or "feed-ending" or "ending-move") Submit(plan == "feed-ending" ? "Feed.AddToHand" : "Strike.AddToHand");
        else if (plan.StartsWith("two-end-effects", StringComparison.Ordinal)) { Submit("Infection.AddToHand"); Submit("Infection.AddToHand"); }
        else if (plan is "zero-card" or "anger-ending") Submit("Anger.AddToHand");
        else if (plan == "free-x") Submit("Whirlwind.AddToHand");
        else if (plan == "repeat-card") { Submit("Strike.AddToHand"); Submit("BoundaryRepeat"); }
        else if (plan == "corruption") { Submit("Defend.AddToHand"); Submit("BoundaryCorruption"); }
        if (plan == "feed-ending") Set(enemy, "Enemy", "Hp", 10);
        if (plan is "ending-move" or "anger-ending") Set(enemy, "Enemy", "Hp", 6);
        Flush(fixture: true); Take("configured");
        if (plan == "two-end-effects-paused")
            Submit("BoundaryEnablePause");
        if (plan == "turn-return" || plan.StartsWith("two-end-effects", StringComparison.Ordinal)) Submit("EndTurn");
        else Submit(plan == "free-x" ? "BoundaryAuto" : "BoundaryPlay", All("Card").Single());
        if (plan == "ending-move") Submit("BoundaryMove", All("Card").Single());
        if (plan == "two-end-effects-paused")
        {
            if (session.Status != SessionStatus.AwaitingDecision) throw new InvalidOperationException("First effect-card move did not pause.");
            Flush(); Take("paused");
            var pausedState = session.State.StateHash(); var pausedRandom = JsonSerializer.Serialize(session.Random.Capture());
            session.Observe(session.Participant(0), "base-spire:PlayerView"); session.LegalActions(session.Participant(0));
            if (pausedState != session.State.StateHash() || pausedRandom != JsonSerializer.Serialize(session.Random.Capture())) throw new InvalidOperationException("Paused query changed gameplay.");
            session.Answer(new DecisionAnswer(session.PendingDecision!.RequestId, Flag: true));
            if (session.Status != SessionStatus.Running) throw new InvalidOperationException("Effect-card continuation failed.");
        }
        Flush(); Take("action-return");
        var beforeQueries = session.State.StateHash(); var beforeRandom = JsonSerializer.Serialize(session.Random.Capture());
        session.Observe(session.Participant(0), "base-spire:PlayerView"); session.LegalActions(session.Participant(0));
        if (beforeQueries != session.State.StateHash() || beforeRandom != JsonSerializer.Serialize(session.Random.Capture())) throw new InvalidOperationException("Boundary queries mutated gameplay.");
        return new(boundaries.ToArray(), beforeQueries + beforeRandom, raw.ToArray());
    }

    private static Trace Native(string seed, string plan, bool capture)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered();
        var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000);
        var combat = new CombatState(run); combat.AddMonster((MonsterModel)ModelDb.Monster<FuzzyWurmCrawler>().MutableClone(), CombatSide.Enemy);
        var enemy = combat.SpawnedEnemies.Single(); enemy.SetMaxHpInternal(1000); enemy.SetCurrentHpInternal(1000);
        var points = new List<Point>(); var raw = new List<object>();
        Point Take(string phase, string card = "")
        {
            var s = player.PlayerCombatState!;
            return new(phase, card, s.TurnNumber, s.Energy, player.Creature.CurrentHp, player.Creature.MaxHp, player.Creature.Block,
                enemy.CurrentHp, enemy.Monster!.NextMove?.Id ?? "none", combat.IsOverOrEnding(),
                new[] { s.Hand, s.DrawPile, s.DiscardPile, s.ExhaustPile, s.PlayPile }.Select(p => string.Concat(p.Cards.Select(c => c.Id.Entry + ","))).ToArray());
        }
        var probe = (BoundaryProbeRelic)new BoundaryProbeRelic().MutableClone(); probe.AssignOwner(player); player.AddRelicInternal(probe);
        probe.Capture = (phase, card) => { if (capture) points.Add(phase.StartsWith("energy-spent:", StringComparison.Ordinal) ? Take("energy-spent", card) with { Spent = int.Parse(phase.Split(':')[1], CultureInfo.InvariantCulture) } : Take(phase, card)); };
        var observerType = typeof(CombatEngine).Assembly.GetType("Sts2Sim.Core.Combat.ICombatObserver")!;
        var observer = DispatchProxy.Create(observerType, typeof(NormalReferenceObserver));
        ((NormalReferenceObserver)observer).Record = (name, arguments) =>
        {
            if (!capture) return;
            if (name == "SideTurnEndListener" && arguments is [string hook, AbstractModel model, CombatSide side, bool starting])
            {
                var method = model.GetType().GetMethod(hook, [typeof(CombatSide), typeof(IEnumerable<Creature>)])!;
                raw.Add(new { name, hook, listener = model.Id.ToString(), side = side.ToString(), starting,
                    declaring_type = method.DeclaringType!.FullName, default_no_op = method.DeclaringType == typeof(AbstractModel) });
            }
            else raw.Add(new { name });
        };
        var engine = (CombatEngine)Activator.CreateInstance(typeof(CombatEngine), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [combat, observer], null)!;
        engine.StartCombatAsync().GetAwaiter().GetResult(); points.Add(Take("begin-return"));
        CardModel Add(CardModel canonical)
        {
            var card = (CardModel)canonical.MutableClone(); card.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(card); return card;
        }
        CardModel? played = null;
        if (plan == "paid-card") played = Add(ModelDb.Card<StrikeIronclad>());
        else if (plan == "feed-ending") { played = Add(ModelDb.Card<Feed>()); enemy.SetCurrentHpInternal(10); }
        else if (plan == "ending-move") { played = Add(ModelDb.Card<StrikeIronclad>()); enemy.SetCurrentHpInternal(6); }
        else if (plan.StartsWith("two-end-effects", StringComparison.Ordinal)) { Add(ModelDb.Card<Infection>()); Add(ModelDb.Card<Infection>()); }
        else if (plan is "zero-card" or "anger-ending") { played = Add(ModelDb.Card<Anger>()); if (plan == "anger-ending") enemy.SetCurrentHpInternal(6); }
        else if (plan == "free-x") played = Add(ModelDb.Card<Whirlwind>());
        else if (plan == "repeat-card") { played = Add(ModelDb.Card<StrikeIronclad>()); PowerCmd.Apply<OneTwoPunchPower>(combat, player.Creature, 1, null, null).GetAwaiter().GetResult(); }
        else if (plan == "corruption") { played = Add(ModelDb.Card<DefendIronclad>()); PowerCmd.Apply<CorruptionPower>(combat, player.Creature, 1, null, null).GetAwaiter().GetResult(); }
        points.Add(Take("configured"));
        if (plan == "free-x") AutoPlayCmd.FromCards(combat, player, [played!]).GetAwaiter().GetResult();
        else if (played is not null) engine.PlayCardAsync(player, played, played.TargetType == TargetType.AnyEnemy ? enemy : null).GetAwaiter().GetResult();
        else
        {
            probe.PauseMoves = plan == "two-end-effects-paused";
            var turn = engine.EndPlayerTurnAsync();
            if (probe.PauseMoves)
            {
                if (turn.IsCompleted || !probe.Paused) throw new InvalidOperationException("Native effect-card move did not suspend.");
                points.Add(Take("paused")); probe.Resume.SetResult();
            }
            turn.GetAwaiter().GetResult();
        }
        if (plan == "ending-move") CardPileCmd.AddAsync(combat, played!, PileType.Hand).GetAwaiter().GetResult();
        points.Add(Take("action-return"));
        var description = new CombatStateDescriptionBuilder(); CombatStateDescription.AppendExactState(ref description, combat);
        var final = JsonSerializer.Serialize(new { point = Take("state"), side = combat.CurrentSide.ToString(), engine.IsInProgress, engine.Won,
            exact_combat_digest = description.Build(),
            powers = combat.Allies.Concat(combat.Enemies).Select(c => c.Powers.Select(p => new { id = p.Id.ToString(), p.Amount }).ToArray()).ToArray(),
            rng = Enum.GetValues<RunRngType>().Select(t => run.Rng.GetRng(t).ToSerializable()).ToArray(),
            player_rng = Enum.GetValues<PlayerRngType>().Select(t => player.PlayerRng.GetRng(t).ToSerializable()).ToArray() }, new JsonSerializerOptions { IncludeFields = true });
        return new(points.ToArray(), final, raw.ToArray());
#else
        throw new NotSupportedException("Independent reference is unavailable.");
#endif
    }

    private static HookSource[] HookSources(string repository)
    {
        const string path = "reference/sts2-sim/src/Sts2Sim.Core/Hooks/Hook.cs";
        var text = File.ReadAllText(Path.Combine(repository, path)).Replace("\r\n", "\n");
        var names = new[] { "BeforeCombatStart", "BeforeSideTurnStart", "AfterEnergyReset", "AfterSideTurnStart", "AfterEnergySpent",
            "BeforeCardPlayed", "AfterCardPlayed", "AfterCardChangedPiles", "ModifyCardPlayResultLocation", "AfterModifyingCardPlayResultLocation",
            "ModifyCardPlayCount", "AfterModifyingCardPlayCount", "BeforeSideTurnEndVeryEarly", "BeforeSideTurnEndEarly", "BeforeSideTurnEnd", "AfterSideTurnEnd",
            "ShouldClearBlock", "AfterPreventingBlockClear", "AfterBlockCleared", "IterateCombatHookListeners",
            "IterateDirectCombatHookListeners", "IterateBlockClearListeners", "IsEligibleHookListener" };
        var declarations = System.Text.RegularExpressions.Regex.Matches(text, @"^    (?:public|private|internal) static [^\n]*?\b(\w+)\s*\(", System.Text.RegularExpressions.RegexOptions.Multiline);
        var result = new List<HookSource>();
        foreach (var name in names)
        {
            var matches = declarations.Where(m => m.Groups[1].Value == name).ToArray();
            if (matches.Length == 0) throw new InvalidOperationException("Missing locked Hook " + name);
            for (var i = 0; i < matches.Length; i++)
            {
                var start = matches[i].Index; var next = declarations.FirstOrDefault(m => m.Index > start)?.Index ?? text.Length;
                var body = text[start..next].TrimEnd();
                var iterators = new[] { "IterateCombatHookListeners", "IterateDirectCombatHookListeners", "IterateBlockClearListeners" }.Where(body.Contains).ToArray();
                result.Add(new(name, i, path, 1 + text[..start].Count(c => c == '\n'), Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(body))), iterators,
                    body.Contains("listeners = ") && body.Contains(".ToArray()"), body.Contains("participantSnapshot"), body.Contains("IsEligibleHookListener("), body.Contains("InvokeExecutionFinished()"), body));
            }
        }
        return result.ToArray();
    }

    private sealed class Probe : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "combat-boundaries-" + Guid.NewGuid());
        internal Probe(string repository)
        {
            var original = Path.Combine(repository, "mod-the-spire/base-spire");
            foreach (var source in Directory.GetFiles(original, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(Root, Path.GetRelativePath(original, source)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            File.Copy(Path.Combine(repository, "tools/differential/combat-boundary-observer.spire"), Path.Combine(Root, "combat-boundary-observer.spire"));
            var entry = Path.Combine(Root, "base.spire");
            File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"combat-boundary-observer.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Root);
            if (Path.GetDirectoryName(path) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || !Path.GetFileName(path).StartsWith("combat-boundaries-", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected boundary probe cleanup path.");
            Directory.Delete(path, true);
        }
    }
}

#if REFERENCE_SIM
// This named tool probe records actual dispatch. It neither modifies game rules nor represents supported content.
internal sealed class BoundaryProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;
    internal Action<string, string> Capture { get; set; } = (_, _) => { };
    internal bool PauseMoves { get; set; }
    internal bool Paused { get; private set; }
    internal TaskCompletionSource Resume { get; } = new();
    public override Task BeforeCombatStart() { Capture("combat-before", ""); return Task.CompletedTask; }
    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants) { Capture(side == CombatSide.Player ? "player-before" : "enemy-before", ""); return Task.CompletedTask; }
    public override Task AfterEnergyReset(Player player) { Capture("energy-reset", ""); return Task.CompletedTask; }
    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants) { if (side == CombatSide.Player) Capture("player-started", ""); return Task.CompletedTask; }
    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants) { if (side == CombatSide.Enemy) Capture("enemy-ended", ""); return Task.CompletedTask; }
    public override Task AfterEnergySpent(CardModel card, int amount) { Capture("energy-spent:" + amount, card.Id.Entry); return Task.CompletedTask; }
    public override Task BeforeCardPlayed(CardPlay play) { Capture("card-before", play.Card.Id.Entry); return Task.CompletedTask; }
    public override Task AfterCardPlayed(CardPlay play) { Capture("card-after", play.Card.Id.Entry); return Task.CompletedTask; }
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        Capture("pile:" + Pile(oldPileType) + ":" + Pile(card.Pile?.Type ?? PileType.None), card.Id.Entry);
        if (PauseMoves && !Paused && oldPileType == PileType.Hand && card.Pile?.Type == PileType.Play)
        { Paused = true; return Resume.Task; }
        return Task.CompletedTask;
    }
    private static string Pile(PileType type) => type == PileType.Draw ? "draw" : type.ToString().ToLowerInvariant();
}
#endif
