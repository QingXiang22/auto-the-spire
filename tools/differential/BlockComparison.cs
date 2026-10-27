using System.Text.Json;
using System.Text.RegularExpressions;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Demo;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;
#endif
namespace Differential;

internal static class BlockComparison
{
    private sealed record PowerSpec(string Definition, int Amount, bool Enemy = false);
    private sealed record Plan(string Name, bool Enemy = false, int Amount = 7, bool Unpowered = false, bool Move = true,
        long InitialBlock = 0, string Before = "none", string Cards = "", PowerSpec[]? Powers = null);
    private static PowerSpec P(string id, int amount = 1, bool enemy = false) => new(id, amount, enemy);
    private static readonly Plan[] Plans = [
        new("plain"), new("enemy", Enemy: true), new("dex", Powers: [P("dexterity-power", 3)]),
        new("enemy-dex", Enemy: true, Powers: [P("dexterity-power", 3, true)]),
        new("negative-dex", Powers: [P("dexterity-power", -10)]), new("enemy-negative-dex", Enemy: true, Powers: [P("dexterity-power", -10, true)]),
        new("frail", Powers: [P("frail-power")]), new("enemy-frail", Enemy: true, Powers: [P("frail-power", 1, true)]),
        new("dex-frail", Powers: [P("dexterity-power", 3), P("frail-power")]),
        new("unpowered", Unpowered: true, Move: false, Powers: [P("dexterity-power", 3), P("frail-power"), P("unmovable-power")]),
        new("enemy-unpowered", Enemy: true, Unpowered: true, Move: false, Powers: [P("dexterity-power", 3, true), P("frail-power", 1, true)]),
        new("no-move", Move: false, Powers: [P("dexterity-power", 3), P("frail-power"), P("unmovable-power")]),
        new("frail-unmovable", Amount: 1, Powers: [P("frail-power"), P("unmovable-power")]),
        new("unmovable-frail", Amount: 1, Powers: [P("unmovable-power"), P("frail-power")]),
        new("enemy-unmovable", Enemy: true, Powers: [P("unmovable-power", 1, true)]),
        new("enemy-owner-unmovable", Powers: [P("unmovable-power", 1, true)]),
        new("zero", Amount: 0), new("zero-frail", Amount: 0, Powers: [P("frail-power")]),
        new("cap", InitialBlock: 999999998, Powers: [P("frail-power")]),
        new("enemy-cap", Enemy: true, InitialBlock: 999999998, Powers: [P("frail-power", 1, true)]),
        new("large-scaled", Amount: 999999999, Powers: [P("unmovable-power"), P("frail-power")]),
        new("fraction-juggernaut", Amount: 1, Powers: [P("frail-power"), P("juggernaut-power", 5)]),
        new("remove-before", Before: "remove", Powers: [P("dexterity-power", 3), P("frail-power")]),
        new("nested-before", Before: "nested", Powers: [P("dexterity-power", 3), P("frail-power")]),
        new("paused-before", Before: "pause", Powers: [P("dexterity-power", 3), P("frail-power")]),
        new("ending-before", Before: "ending", Powers: [P("dexterity-power", 3), P("frail-power")]),
        new("probe-both", Powers: [P("frail-power"), P("dexterity-power", 3), P("probe:block-callback")]),
        new("probe-zero", Amount: 0, Powers: [P("probe:block-callback", 2)]),
        new("probe-neutral", Powers: [P("probe:block-callback", 3)]),
        new("defend-history", Amount: 5, Cards: "defend", Powers: [P("unmovable-power")]),
        new("entrench-history", Amount: 3, InitialBlock: 3, Unpowered: true, Cards: "entrench", Powers: [P("unmovable-power"), P("dexterity-power", 3), P("frail-power")]),
        new("same-play-history", Amount: 8, Cards: "evil-eye", Powers: [P("unmovable-power")]),
        new("bowl-move", Enemy: true, Cards: "bowl", Powers: [P("dexterity-power", 3, true), P("frail-power", 1, true)]),
        new("enemy-plating", Enemy: true, Cards: "plating"),
        new("card-source-player", Cards: "source", Powers: [P("dexterity-power", 5, true), P("dexterity-power", 3)]),
        new("card-source-enemy", Enemy: true, Cards: "source", Powers: [P("dexterity-power", 5, true), P("dexterity-power", 3), P("frail-power", 1, true)]),
        new("fraction-history", Amount: 5, Cards: "defend", Powers: [P("dexterity-power", -4), P("frail-power")]),
        new("cap-history", Amount: 5, InitialBlock: 999999999, Cards: "defend", Powers: [P("unmovable-power")]),
        new("rejected-dead", Enemy: true, Cards: "dead"), new("rejected-ending", Cards: "ending")
    ];
    private sealed record Row(string At, long Amount, long PlayerBlock, long EnemyBlock, string Card, bool HasPlay,
        bool Enemy, bool Unpowered, bool Move, string[] Modifiers);
    private sealed record Power(string Definition, long Amount, long StartAmount, bool Enemy);
    private sealed record Result(long Preview, bool QueryReadOnly, long[] Returned, Row[] Rows, long PlayerHp, long Block,
        long[] EnemyHp, long[] EnemyBlock, Power[] Powers, long[] PlayBlockGains, long Callbacks, long TargetDraws, ulong[] TargetState);
    internal static int Run(string repository, string[] args)
    {
        if (args.Length != 0 && (args.Length != 2 || args[0] != "--output")) throw new FormatException("Use block-compare [--output DIRECTORY].");
        var reference = Repository.VerifyReference(repository); var original = DslPackage.Load(Path.Combine(repository, "mod-the-spire/base-spire/package.json"));
        using var fixture = new Fixture(repository, original.Root); var package = DslPackage.Load(Path.Combine(fixture.Root, "package.json"));
        var cases = new List<object>(); var passed = true;
        foreach (var seed in new[] { "777", "A😀B" }) foreach (var plan in Plans)
            {
                Result left, right;
                try { left = Dsl(package, seed, plan); } catch (Exception e) { throw new InvalidOperationException("DSL block " + seed + ":" + plan.Name + ": " + e.Message, e); }
                try { right = Native(seed, plan); } catch (Exception e) { throw new InvalidOperationException("Native block " + seed + ":" + plan.Name + ": " + e.Message, e); }
                var same = NormalPreflight.Json(left) == NormalPreflight.Json(right); passed &= same;
                cases.Add(new { id = seed + ":" + plan.Name, status = same ? "passed" : "diverged", left, right });
            }
        var report = new
        {
            schema = "dsl-migration/block@1",
            reference,
            package_digest = original.Digest,
            fixture_digest = package.Digest,
            status = passed ? "passed" : "diverged",
            independent_verified = passed,
            normal_run_verified = false,
            scope = "controlled-component",
            setup = "Players first; two real TwigSlimeS (one BowlbugEgg in bowl-move), empty decks and HP1000 explicitly normalized. Direct command inputs and initial block are controlled. Real Dexterity/Frail/Unmovable/Juggernaut/Plating execute on both sides. Before/After rows preserve exact modified micro-units, flags, card/play attribution, committed block and distinct actual modifier identities in source hook order. Native modifier identity comes from a read-only Hook.ModifyBlock call after the controlled Before callback; callback execution is additionally tested by a synthetic Power that modifies both phases or multiplies zero. Preview is independently computed; repeated DSL legal queries/observations verify full state hash, version and RNG unchanged. Native previews compare their own resources/Powers and target RNG. Paused command uses a real native pending Task and one DSL decision. Entrench's real source card is paired with a tool-only DSL body, not formal Entrench support. Real Defend/EvilEye prove per-play history and repeated current-play exclusion. Plating covers its initial and two side-end gains with real duration changes. Ending boundaries compare the entered command before native win settlement, not full lifecycle certification; birth RNG, complete public snapshots, extra turns and normal Run remain separate.",
            cases
        };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (args.Length != 0) { Directory.CreateDirectory(args[1]); File.WriteAllText(Path.Combine(args[1], "result.json"), json); }
        Console.WriteLine(json); return passed ? 0 : 1;
    }
    private static Result Dsl(DslPackage package, string seed, Plan plan)
    {
        var s = package.CreateSession(1, ["player"], seed); s.Start();
        InstanceId[] All(string type) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).ToArray();
        Value F(InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
        void Set(InstanceId id, string type, string field, Value value) => s.State.RunAtomic(() => s.State.SetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field), value));
        void Submit(string name, params Value[] values) { s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name), s.Participant(0), values); if (s.Status == SessionStatus.Faulted) throw new InvalidOperationException(s.Fault!.ToString()); }
        var player = All("Player").Single(); Set(player, "Player", "UsesRunDeck", Value.Of(true)); Set(player, "Player", "Hp", Value.Of(1000L)); Set(player, "Player", "MaxHp", Value.Of(1000L));
        Submit("BlockProbeSetup"); var config = All("BlockProbeState").Single();
        Set(config, "BlockProbeState", "Enemy", Value.Of(plan.Enemy)); Set(config, "BlockProbeState", "Amount", Value.Of((long)plan.Amount));
        Set(config, "BlockProbeState", "Unpowered", Value.Of(plan.Unpowered)); Set(config, "BlockProbeState", "Move", Value.Of(plan.Move));
        Set(config, "BlockProbeState", "Before", Value.Of(plan.Before));
        if (plan.Cards == "plating") Set(config, "BlockProbeState", "Before", Value.Of("plating-init"));
        Submit(plan.Cards == "bowl" ? "BowlbugEgg.Spawn" : "TwigSlimeS.Spawn"); Submit("TwigSlimeS.Spawn"); var enemies = All("Enemy");
        foreach (var e in enemies) { Set(e, "Enemy", "Hp", Value.Of(1000L)); Set(e, "Enemy", "MaxHp", Value.Of(1000L)); }
        Submit("BeginDevelopmentCombat"); Submit("BeginDevelopmentTurn");
        foreach (var power in plan.Powers ?? []) Submit("BlockProbePower", Value.Of(power.Enemy), Value.Of(power.Definition.Contains(':') ? power.Definition : "base-spire:" + power.Definition), Value.Of((long)power.Amount));
        Set(plan.Enemy ? enemies[0] : player, plan.Enemy ? "Enemy" : "Player", "Block", Value.Of(plan.InitialBlock));
        InstanceId Card(string symbol) { Submit(symbol + ".AddToHand"); return s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id).Last(); }
        InstanceId? firstCard = null;
        if (plan.Cards is "defend" or "source") firstCard = Card("Defend");
        if (plan.Cards == "entrench") { Submit("BlockProbeAddEntrench"); firstCard = s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id).Single(); }
        if (plan.Cards == "evil-eye") { var exhausted = Card("Anger"); Submit("BlockProbeExhaust", Value.OfRef(exhausted)); firstCard = Card("EvilEye"); }
        if (firstCard is { } card) Set(config, "BlockProbeState", "Card", Value.OfRef(card));
        if (plan.Cards == "dead") Set(enemies[0], "Enemy", "Hp", Value.Of(0L));
        if (plan.Cards == "ending") foreach (var e in enemies) Set(e, "Enemy", "Hp", Value.Of(0L));
        Submit("BlockProbePreview"); var preview = F(config, "BlockProbeState", "Preview").AsInt();
        var hash = s.State.StateHash(); var version = s.StateVersion; var draws = s.Random.TotalDraws;
        for (var i = 0; i < 3; i++) { _ = s.LegalActions(s.Participant(0)); _ = s.Observe(s.Participant(0), "base-spire:PlayerView"); }
        var readOnly = hash == s.State.StateHash() && version == s.StateVersion && draws == s.Random.TotalDraws;
        long[] returned = [];
        if (firstCard is { } playCard && plan.Cards != "source")
        {
            Submit("PlayCardFromHand", Value.OfRef(playCard));
            if (plan.Cards != "evil-eye") Submit("PlayCardFromHand", Value.OfRef(Card("Defend")));
        }
        else if (plan.Cards is "bowl" or "plating")
        {
            for (var i = 0; i < (plan.Cards == "plating" ? 2 : 1); i++) { Submit("EndDevelopmentTurn"); Submit("BeginDevelopmentTurn"); }
        }
        else
        {
            Submit("BlockProbeGain");
            if (plan.Before == "pause")
            {
                if (s.Status != SessionStatus.AwaitingDecision) throw new InvalidOperationException("Block command did not pause.");
                var request = s.PendingDecision!; s.Answer(new DecisionAnswer(request.RequestId, Flag: true));
            }
            returned = [F(config, "BlockProbeState", "Returned").AsInt()];
        }
        if (s.Status != SessionStatus.Running) throw new InvalidOperationException("Block command did not resume: " + s.Fault);
        if (All("BlockGain").Length != 0 || All("BlockAmountModifier").Length != 0) throw new InvalidOperationException("Block command leaked temporary context.");
        var rows = All("BlockProbeRow").Select(r => new Row(F(r, "BlockProbeRow", "At").AsString(), F(r, "BlockProbeRow", "Amount").AsInt(), F(r, "BlockProbeRow", "PlayerBlock").AsInt(), F(r, "BlockProbeRow", "EnemyBlock").AsInt(), F(r, "BlockProbeRow", "Card").AsString(), F(r, "BlockProbeRow", "HasPlay").AsBool(), F(r, "BlockProbeRow", "Enemy").AsBool(), F(r, "BlockProbeRow", "Unpowered").AsBool(), F(r, "BlockProbeRow", "Move").AsBool(),
            All("BlockProbeChanged").Where(c => F(c, "BlockProbeChanged", "Row") == Value.OfRef(r)).Select(c => F(c, "BlockProbeChanged", "Definition").AsString()).ToArray())).ToArray();
        var rng = s.Random.Capture(); var target = rng.Streams.SingleOrDefault(x => x.Name == "base-spire:run:combat_targets"); var words = target?.BackendState?.ToArray() ?? new Xoshiro256StarStar(rng.ExplicitSeeds["base-spire:run:combat_targets"], false).Capture().ToArray();
        return new(preview, readOnly, returned, rows, F(player, "Player", "Hp").AsInt(), F(player, "Player", "Block").AsInt(), enemies.Select(e => F(e, "Enemy", "Hp").AsInt()).ToArray(), enemies.Select(e => F(e, "Enemy", "Block").AsInt()).ToArray(),
            All("Power").OrderBy(p => F(p, "Power", "HolderEnemy").IsNone ? 0 : 1).Select(p => new Power(F(p, "Power", "DefinitionId").AsString(), F(p, "Power", "Stacks").AsInt(), F(p, "Power", "AmountAtTurnStart").AsInt(), !F(p, "Power", "HolderEnemy").IsNone)).ToArray(),
            s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:CardPlays").Id).Select(p => F(p, "CardPlayRecord", "PoweredBlocks").AsInt()).ToArray(), F(config, "BlockProbeState", "CallbackCount").AsInt(), s.Random.DrawCount("base-spire:run:combat_targets"), words);
    }
    private static Result Native(string seed, Plan plan)
    {
#if REFERENCE_SIM
        CombatReplayDemo.EnsureModelsRegistered(); var run = new RunState(seed, new Overgrowth()); var player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run); run.AddPlayer(player);
        foreach (var card in player.Deck.Cards.ToArray()) player.Deck.RemoveInternal(card);
        player.Creature.SetMaxHpInternal(1000); player.Creature.SetCurrentHpInternal(1000);
        var combat = new CombatState(run); combat.AddPlayerCreature(player.Creature);
        combat.AddMonster((MonsterModel)(plan.Cards == "bowl" ? (MonsterModel)ModelDb.Monster<BowlbugEgg>() : ModelDb.Monster<TwigSlimeS>()).MutableClone(), CombatSide.Enemy);
        combat.AddMonster((MonsterModel)ModelDb.Monster<TwigSlimeS>().MutableClone(), CombatSide.Enemy);
        var enemies = combat.Enemies.ToArray(); foreach (var e in enemies) { e.SetMaxHpInternal(1000); e.SetCurrentHpInternal(1000); }
        var rows = new List<Row>(); var frames = new Stack<(CardModel? Card, CardPlay? Play, string[] Modifiers)>(); long callbacks = 0; var plays = new List<CardPlay>();
        string Name(PowerModel p) => p is CallbackPower ? "probe:block-callback" : "base-spire:" + Regex.Replace(p.GetType().Name, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
        void Apply(PowerSpec spec)
        {
            var owner = spec.Enemy ? enemies[0] : player.Creature;
            switch (spec.Definition)
            {
                case "dexterity-power": PowerCmd.Apply<DexterityPower>(combat, owner, spec.Amount, null, null).GetAwaiter().GetResult(); break;
                case "frail-power": PowerCmd.Apply<FrailPower>(combat, owner, spec.Amount, null, null).GetAwaiter().GetResult(); break;
                case "unmovable-power": PowerCmd.Apply<UnmovablePower>(combat, owner, spec.Amount, null, null).GetAwaiter().GetResult(); break;
                case "juggernaut-power": PowerCmd.Apply<JuggernautPower>(combat, owner, spec.Amount, null, null).GetAwaiter().GetResult(); break;
                case "probe:block-callback": var p = (CallbackPower)new CallbackPower().MutableClone(); p.ApplyInternal(owner, spec.Amount); p.Capture = () => callbacks++; break;
                default: throw new NotSupportedException(spec.Definition);
            }
        }
        void Capture(string at, Creature target, decimal amount, ValueProp props, CardModel? card, CardPlay? play, string[] modifiers) => rows.Add(new(at, checked((long)(amount * 1000000m)), player.Creature.Block, enemies[0].Block, card?.Id.Entry ?? "none", play is not null, target.IsMonster, props.HasFlag(ValueProp.Unpowered), props.HasFlag(ValueProp.Move), modifiers));
        var probe = (BlockAudit)new BlockAudit().MutableClone(); probe.AssignOwner(player); player.AddRelicInternal(probe);
        probe.Start = () => { if (plan.Cards == "plating") { PowerCmd.Apply<PlatingPower>(combat, enemies[0], 5, null, null).GetAwaiter().GetResult(); Apply(P("dexterity-power", 3, true)); Apply(P("frail-power", 3, true)); } };
        CardPlay? activePlay = null; probe.Played = play => activePlay = play;
        var nesting = false; var pending = new TaskCompletionSource();
        probe.Before = async (target, amount, props, card) =>
        {
            var currentPlay = card is not null && activePlay?.Card == card ? activePlay : null;
            frames.Push((card, currentPlay, [])); Capture("before", target, amount, props, card, currentPlay, []);
            if (!nesting)
            {
                if (plan.Before == "remove") foreach (var p in player.Creature.Powers.OfType<DexterityPower>().ToArray()) await PowerCmd.Remove(p);
                if (plan.Before == "nested") { nesting = true; await CreatureCmd.GainBlock(combat, player.Creature, 2, ValueProp.Unpowered, null, null); nesting = false; }
                if (plan.Before == "pause") await pending.Task;
                if (plan.Before == "ending") foreach (var e in enemies) await CreatureCmd.Kill(e);
            }
            var frame = frames.Pop();
            _ = Hook.ModifyBlock(combat, target, amount, props, card, currentPlay, out var changed);
            var set = changed.ToHashSet(); var ordered = run.IterateHookListeners(combat).OfType<PowerModel>().Where(set.Contains).Select(Name).ToArray();
            frames.Push((frame.Card, frame.Play, ordered));
        };
        probe.After = (target, amount, props, card) => { var frame = frames.Pop(); Capture("after", target, amount, props, card, frame.Play, frame.Modifiers); };
        var engine = new CombatEngine(combat); engine.StartCombatAsync().GetAwaiter().GetResult();
        foreach (var p in plan.Powers ?? []) Apply(p);
        var recipient = plan.Enemy ? enemies[0] : player.Creature; recipient.LoseBlockInternal(recipient.Block); recipient.GainBlockInternal(plan.InitialBlock);
        CardModel NewCard(CardModel canonical) { var c = (CardModel)canonical.MutableClone(); c.AssignOwner(player); player.PlayerCombatState!.Hand.AddInternal(c); return c; }
        CardModel? firstCard = null;
        if (plan.Cards is "defend" or "source") firstCard = NewCard(ModelDb.Card<DefendIronclad>());
        if (plan.Cards == "entrench") firstCard = NewCard(ModelDb.Card<Entrench>());
        if (plan.Cards == "evil-eye") { var c = NewCard(ModelDb.Card<Anger>()); CardPileCmd.Exhaust(combat, c).GetAwaiter().GetResult(); firstCard = NewCard(ModelDb.Card<EvilEye>()); }
        if (plan.Cards == "dead") enemies[0].SetCurrentHpInternal(0);
        if (plan.Cards == "ending") foreach (var e in enemies) e.SetCurrentHpInternal(0);
        var flags = (plan.Unpowered ? ValueProp.Unpowered : (ValueProp)0) | (plan.Move ? ValueProp.Move : (ValueProp)0);
        string Facts() => NormalPreflight.Json(new { hp = player.Creature.CurrentHp, block = player.Creature.Block, enemies = enemies.Select(e => new { hp = e.CurrentHp, block = e.Block, powers = e.Powers.Select(p => new { id = Name(p), p.Amount, p.AmountOnTurnStart }) }), powers = player.Creature.Powers.Select(p => new { id = Name(p), p.Amount, p.AmountOnTurnStart }), rng = run.Rng.CombatTargets.ToSerializable() });
        var before = Facts(); decimal preview = 0;
        for (var i = 0; i < 3; i++) preview = Hook.ModifyBlock(combat, recipient, plan.Amount, flags, firstCard, null, out _);
        var readOnly = before == Facts(); long[] returned = [];
        if (firstCard is not null && plan.Cards != "source")
        {
            // Observe the actual play via the native card's BeforeCardPlayed hook context below.
            var play = engine.PlayCardWithResultAsync(player, firstCard, null).GetAwaiter().GetResult(); if (play is not null) plays.Add(play);
            if (plan.Cards != "evil-eye") { play = engine.PlayCardWithResultAsync(player, NewCard(ModelDb.Card<DefendIronclad>()), null).GetAwaiter().GetResult(); if (play is not null) plays.Add(play); }
        }
        else if (plan.Cards is "bowl" or "plating") for (var i = 0; i < (plan.Cards == "plating" ? 2 : 1); i++) engine.EndPlayerTurnAsync().GetAwaiter().GetResult();
        else
        {
            var task = CreatureCmd.GainBlock(combat, recipient, plan.Amount, flags, firstCard, null);
            if (plan.Before == "pause") { if (task.IsCompleted) throw new InvalidOperationException("Native block did not pause."); pending.SetResult(); }
            returned = [checked((long)(task.GetAwaiter().GetResult() * 1000000m))];
        }
        var rng = run.Rng.CombatTargets.ToSerializable();
        return new(checked((long)(Math.Max(0, preview) * 1000000m)), readOnly, returned, rows.ToArray(), player.Creature.CurrentHp, player.Creature.Block, enemies.Select(e => (long)e.CurrentHp).ToArray(), enemies.Select(e => (long)e.Block).ToArray(),
            new[] { player.Creature }.Concat(enemies).SelectMany(e => e.Powers).Select(p => new Power(Name(p), p.Amount, p.AmountOnTurnStart, p.Owner.IsMonster)).ToArray(),
            plays.Select(play => (long)combat.SemanticHistory.CountOtherQualifyingBlockGainsThisTurn(combat, player.Creature, null) - combat.SemanticHistory.CountOtherQualifyingBlockGainsThisTurn(combat, player.Creature, play)).ToArray(), callbacks, rng.counter, [rng.state0, rng.state1, rng.state2, rng.state3]);
#else
        throw new NotSupportedException("Independent reference was not built.");
#endif
    }
#if REFERENCE_SIM
    private sealed class BlockAudit : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Common;
        internal Action Start { get; set; } = null!;
        internal Action<CardPlay?> Played { get; set; } = null!;
        internal Func<Creature, decimal, ValueProp, CardModel?, Task> Before { get; set; } = null!;
        internal Action<Creature, decimal, ValueProp, CardModel?> After { get; set; } = null!;
        public override Task BeforeCombatStart() { Start(); return Task.CompletedTask; }
        public override Task BeforeCardPlayed(CardPlay play) { Played(play); return Task.CompletedTask; }
        public override Task AfterCardPlayed(CardPlay play) { Played(null); return Task.CompletedTask; }
        public override Task BeforeBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource) => Before(creature, amount, props, cardSource);
        public override Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource) { After(creature, amount, props, cardSource); return Task.CompletedTask; }
    }
    private sealed class CallbackPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
        internal Action Capture { get; set; } = null!;
        public override decimal ModifyBlockAdditive(Creature target, decimal amount, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) => target == Owner && !props.HasFlag(ValueProp.Unpowered) && Amount == 1 ? 1 : 0;
        public override decimal ModifyBlockMultiplicative(Creature target, decimal amount, ValueProp props, CardModel? cardSource, CardPlay? cardPlay) => target == Owner && !props.HasFlag(ValueProp.Unpowered) && Amount < 3 ? 2 : 1;
        public override Task AfterModifyingBlockAmount(decimal modifiedAmount, CardModel? cardSource, CardPlay? cardPlay) { Capture(); return Task.CompletedTask; }
    }
#endif
    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "block-compare-" + Guid.NewGuid());
        internal Fixture(string repository, string source)
        {
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) { var path = Path.Combine(Root, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.Copy(file, path); }
            var entry = Path.Combine(Root, "base.spire"); File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"combat/index.spire\"", "\"block-probe.spire\", \"combat/index.spire\"", StringComparison.Ordinal));
            File.WriteAllText(Path.Combine(Root, "block-probe.spire"), "using namespace base-spire;\ndsl_version = \"core/0.3\"\n" + File.ReadAllText(Path.Combine(repository, "tools/base-spire-dev/fixtures/block-probe.spire")));
        }
        public void Dispose() { var path = Path.GetFullPath(Root); if (Path.GetDirectoryName(path) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) || !Path.GetFileName(path).StartsWith("block-compare-", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected block fixture cleanup path."); Directory.Delete(path, true); }
    }
}
