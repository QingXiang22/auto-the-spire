using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class OrchestrationChecks
{
    public static void Run()
    {
        Check.Run("Orchestration: played identity remains known in hidden draw pile, without revealing other cards", () =>
        {
            var s = Probe("gainBlock = { amount = 1 }", copies: 8);
            var played = Hand(s)[0]; var owner = FormalSliceFixture.Player(s);
            Play(s, played, FormalSliceFixture.Cultist(s));
            var discard = FormalSliceFixture.Collection(s, FormalSliceFixture.DiscardPile);
            var draw = FormalSliceFixture.Collection(s, FormalSliceFixture.DrawPile);
            s.State.RunAtomic(() => { s.State.RemoveFromCollection(discard, played); s.State.AddToCollection(draw, played); });
            var view = s.Observe(SliceManifest.Player, "base-spire:PlayerView");
            Check.True(view.Instances.Any(i => i.Id == played), "a played card must close public history references after reshuffle");
            var hidden = Members(s, FormalSliceFixture.DrawPile).First(i => i != played);
            Check.False(view.Instances.Any(i => i.Id == hidden), "other draw identities remain hidden");
            FormalSliceFixture.EndTurn(s);
            var reveal = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:PlayedReveal");
            Check.Equal(0, s.State.RelationTargets(reveal.Id, owner).Count, "turn reset revokes remembered identities");
        });
        Check.Run("Orchestration: lethal first hit skips dead target on later hits, preserving parent effects", () =>
        {
            var s=Probe("repeat = { count = 3 effect = { attack = { damage = 99 } } } gainBlock = { amount = 7 }");
            Play(s,Hand(s)[0],FormalSliceFixture.Cultist(s));
            Check.Equal(1,Members(s,"base-spire:Monsters").Count,"one death, no duplicate removal");
            Check.Equal(7L,Number(s,FormalSliceFixture.Player(s),"Block"),"post-hit effect still executes");
        });
        Check.Run("Orchestration: repeated random hits stop when no targets remain", () =>
        {
            var s=Probe("repeat = { count = 3 effect = { randomAttack = { damage = 99 } } } gainBlock = { amount = 7 }");
            Play(s,Hand(s)[0],FormalSliceFixture.Player(s));
            Check.Equal(0,Members(s,"base-spire:Monsters").Count,"both targets die, third hit has no target");
            Check.Equal(7L,Number(s,FormalSliceFixture.Player(s),"Block"),"remaining effect survives empty target domain");
        });
        Check.Run("Orchestration: nested count restores the outer snapshot after inner shadow and pile mutation", () =>
        {
            var s = Probe("withCount = { name = h count = { pileCount = { pile = hand } } effect = { "
                + "forEachCard = { from = hand effect = { moveSelectedCard = { from = hand to = exhaust } } } "
                + "withCount = { name = h count = { pileCount = { pile = hand } } effect = { gainBlock = { amount = 0 } } } "
                + "scaledAttack = { base = 0 perCount = 1 count = { savedCount = { name = h } } } } }", copies: 2);
            var enemy = FormalSliceFixture.Cultist(s);
            var hp = Hp(s, enemy);
            Play(s, Hand(s)[0], enemy);
            Check.Equal(1L, hp - Hp(s, enemy), "outer snapshot survives inner zero");
        });
        Check.Run("Orchestration: count cannot escape a block or branch, or bind itself", () =>
        {
            const string saved = "scaledAttack = { base = 0 perCount = 1 count = { savedCount = { name = h } } }";
            foreach (var body in new[] {
                "withCount = { name = h count = { pileCount = { pile = hand } } effect = { gainBlock = { amount = 0 } } } " + saved,
                "if = { condition = { targetAlive = {} } then = { withCount = { name = h count = { pileCount = { pile = hand } } effect = {} } } else = { " + saved + " } }",
                "withCount = { name = h count = { savedCount = { name = h } } effect = {} }" })
            {
                var parsed = Parse(body);
                Check.Equal(0, parsed.Cards.Count, "invalid lexical scope rejected before registration");
                Check.True(parsed.Unsupported.Single().Reason.Contains("orchestration-probe.spire", StringComparison.Ordinal), "source diagnostic");
            }
        });
        Check.Run("Orchestration: repeated physical card records separate finished executions", () =>
        {
            var s = Probe("scaledAttack = { base = 1 perCount = 1 count = { cardPlaysThisTurn = { type = attack phase = finished } } }", cost: "0");
            var card = Hand(s)[0]; var target = FormalSliceFixture.Cultist(s); var hp = Hp(s, target);
            for (var i = 0; i < 3; i++)
            {
                if (i > 0) FormalSliceFixture.MoveToHand(s, "base-spire:orchestration-probe");
                Play(s, card, target);
            }
            Check.Equal(6L, hp - Hp(s, target), "1+2+3; earlier plays of this instance are counted");
            Check.Equal(3, Members(s, "base-spire:CardPlayHistory").Count, "one event per execution");
            Check.Equal(1, Members(s, "base-spire:PlayedThisTurn").Count, "public unique-card compatibility view");
            FormalSliceFixture.EndTurn(s);
            Check.Equal(0, Members(s, "base-spire:CardPlayHistory").Count, "turn history reset destroys entries");
        });
        Check.Run("Orchestration: battle HP-loss counter resets at room entry while run HP persists", () =>
        {
            var s = FormalSliceFixture.NewFormal(); var player = FormalSliceFixture.Player(s);
            Play(s, FormalSliceFixture.MoveToHand(s, "base-spire:bloodletting"), player);
            Check.Equal(1L, Number(s, player, "HpLossCount"), "one actual loss event");
            foreach (var (enemyId, cardId) in new[] { (SliceManifest.CalcifiedCultistId, SliceManifest.StrikeId), (SliceManifest.DampCultistId, "base-spire:bash") })
            {
                var enemy = FormalSliceFixture.Cultist(s, enemyId);
                FormalSliceFixture.SetNumber(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 1);
                Play(s, FormalSliceFixture.MoveToHand(s, cardId), enemy);
            }
            var hp = Number(s, player, "Hp");
            NextRoom(s);
            Check.Equal(0L, Number(s, player, "HpLossCount"), "battle reset");
            Check.Equal(hp, Number(s, player, "Hp"), "persistent HP survives room entry");
        });
        Check.Run("Orchestration: a mod caller repeats the shared pipeline with one payment, one target and one result move", () =>
        {
            var parsed = Parse("scaledAttack = { base = 1 perCount = 1 count = { cardPlaysThisTurn = { type = attack phase = finished } } }", "1");
            var model = FormalSpirePackage.Load().Model with { Cards = parsed.Cards, Deck = [new(parsed.Cards.Single().Id, 1)] };
            var vanilla = CombatSlicePackage.Build(authorModel: model);
            var extension = new EnginePackageBuilder("replay-probe", "1"); extension.Dependencies.Add(SliceManifest.PackageId);
            var action = extension.Action("RepeatPlay");
            var cardType = vanilla.Declarations.OfType<EntityTypeSymbol>().Single(t => t.Name == "Card");
            var hand = vanilla.Declarations.OfType<CollectionSymbol>().Single(t => t.Name == "Hand");
            var shared = vanilla.Declarations.OfType<ProcedureSymbol>().Single(t => t.Name == "ExecuteCardPlay");
            var input = action.Parameter("Card", FieldTypeSchema.RefTo(cardType));
            var body = extension.ActionBody(action); body.Role(SliceManifest.PlayerRole); body.Candidates("Card", body.Collection(hand));
            body.Body(body.Call(shared, body.Param(input.DeclarationIndex), body.Const(0L), body.Const(false), body.Const(false), body.Const(3L)));
            var builder = new RuleSetBuilder(); builder.Add(vanilla.Build()); builder.Add(extension.Build());
            var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance); s.Start();
            var card = Hand(s)[0]; var target = FormalSliceFixture.Cultist(s); var hp = Hp(s, target);
            var selected = s.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == "replay-probe:RepeatPlay");
            s.Submit(selected.Definition, SliceManifest.Player, [Value.OfRef(card)]);
            var choices = 0;
            while (s.Status == SessionStatus.AwaitingDecision) { s.Answer(new(s.PendingDecision!.RequestId, [target])); choices++; }
            Healthy(s);
            Check.Equal(1, choices, "target captured for the whole play");
            Check.Equal(6L, hp - Hp(s, target), "three bodies read 0, 1, 2 completed executions");
            Check.Equal(2L, Number(s, FormalSliceFixture.Player(s), "Energy"), "cost paid once");
            Check.Equal(3, Members(s, "base-spire:CardPlayHistory").Count, "replay events are distinct");
            Check.Sequence(new[] { card }, Members(s, FormalSliceFixture.DiscardPile), "one result placement");
        });
        Check.Run("Orchestration: draw-until continues on attack, stops on skill and respects the hand cap", () =>
        {
            var s = FormalSliceFixture.NewFormal(); var player = FormalSliceFixture.Player(s);
            var pillage = FormalSliceFixture.MoveToHand(s, "base-spire:pillage");
            var draw = ArrangeDraw(s, SliceManifest.StrikeId, "base-spire:bash", SliceManifest.DefendId, "base-spire:anger");
            var before = Hand(s).Count;
            Play(s, pillage, FormalSliceFixture.Cultist(s));
            Check.Equal(before + 2, Hand(s).Count, "three drawn and played card removed");
            Check.Sequence(draw.Take(3), Hand(s).TakeLast(3), "continues across two attacks and includes stopping skill");
            Check.True(Members(s, FormalSliceFixture.DrawPile).Contains(draw[3]), "card after stopping skill stays in draw");
            var full = Probe("draw = { amount = 20 }", copies: 16);
            Play(full, Hand(full)[0], FormalSliceFixture.Cultist(full));
            Check.Equal(SliceManifest.MaxHandSize, Hand(full).Count, "oversized draws stop at capacity");
        });
        Check.Run("Orchestration: draw-until stops cleanly under NoDraw and empty piles", () =>
        {
            var s = FormalSliceFixture.NewFormal();
            var card = FormalSliceFixture.MoveToHand(s, "base-spire:pillage");
            ArrangeDraw(s, SliceManifest.StrikeId, SliceManifest.DefendId);
            FormalSliceFixture.GivePower(s, FormalSliceFixture.Player(s), "base-spire:no-draw", 1);
            var count = Hand(s).Count;
            Play(s, card, FormalSliceFixture.Cultist(s));
            Check.Equal(count - 1, Hand(s).Count, "blocked draw terminates without recycling a previous result");
            var empty = Probe("drawUntil = { type = attack }");
            Play(empty, Hand(empty)[0], FormalSliceFixture.Cultist(empty));
            Check.Equal(0, Hand(empty).Count, "empty piles terminate");
        });
        Check.Run("Orchestration: Havoc uses ordinary hooks/history/result with free automatic play and random target", () =>
        {
            var s = FormalSliceFixture.NewFormal(); var player = FormalSliceFixture.Player(s);
            var havoc = FormalSliceFixture.MoveToHand(s, "base-spire:havoc");
            var strike = ArrangeDraw(s, SliceManifest.StrikeId)[0];
            var enemies = Members(s, "base-spire:Monsters");
            var before = enemies.Sum(e => Hp(s, e)); var energy = Number(s, player, "Energy");
            Check.Equal(0, Play(s, havoc, player), "automatic enemy target asks no manual decision");
            Check.Equal(6L, before - enemies.Sum(e => Hp(s, e)), "ordinary Strike effect");
            Check.Equal(1L, s.Random.DrawCount(SliceManifest.RandomTargetStream), "one named target draw for one automatic attack");
            Check.Equal(energy - 1, Number(s, player, "Energy"), "only Havoc pays; Strike is free");
            Check.True(Members(s, FormalSliceFixture.ExhaustPile).Contains(strike), "forced exhaust after body");
            Check.Equal(2, Members(s, "base-spire:CardPlayHistory").Count, "manual and automatic events");
            Check.Equal(0, Members(s, "base-spire:InPlay").Count, "both plays completed");
        });
        Check.Run("Orchestration: autoplay captures a batch before child draws and never reselects drawn cards", () =>
        {
            var source = Definitions(("Parent", "skill", "autoPlay = { from = draw count = 2 }", "0", ""),
                ("Child", "attack", "attack = { damage = 2 } draw = { amount = 1 }", "2", ""),
                ("Second", "attack", "attack = { damage = 3 }", "1", ""),
                ("Third", "attack", "attack = { damage = 9 }", "1", ""));
            var s = FromSource(source);
            var parent = FormalSliceFixture.MoveToHand(s, "base-spire:parent");
            var arranged = ArrangeDraw(s, "base-spire:child", "base-spire:second", "base-spire:third");
            var before = Members(s, "base-spire:Monsters").Sum(e => Hp(s, e));
            Play(s, parent, FormalSliceFixture.Player(s));
            Check.Equal(5L, before - Members(s, "base-spire:Monsters").Sum(e => Hp(s, e)), "2+3; third is only drawn");
            Check.True(Hand(s).Contains(arranged[2]), "child drew the third, not the second preselected card");
            Check.Equal(3, Members(s, "base-spire:CardPlayHistory").Count, "parent and two selected children");
            Check.Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:AutoPlayPending").Id).Count, "batch entries cleaned");
        });
        Check.Run("Orchestration: nested autoplay completes outer later effects and hooks after final kill", () =>
        {
            var s = FromSource(Definitions(("Parent", "skill", "autoPlay = { from = draw count = 1 forceExhaust = true } gainBlock = { amount = 7 }", "1", "afterCardPlayed = { gainBlock = { amount = 3 } }"),
                ("Child", "skill", "autoPlay = { from = draw count = 2 forceExhaust = true }", "2", ""),
                ("Killer", "attack", "attack = { damage = 99 target = allEnemies }", "2", ""),
                ("Surplus", "skill", "gainBlock = { amount = 99 }", "2", "")));
            var parent = FormalSliceFixture.MoveToHand(s, "base-spire:parent");
            var children = ArrangeDraw(s, "base-spire:child", "base-spire:killer", "base-spire:surplus");
            Play(s, parent, FormalSliceFixture.Player(s));
            Check.Equal(10L, Number(s, FormalSliceFixture.Player(s), "Block"), "outer effect and after hook completed");
            Check.Equal(0, Members(s, "base-spire:InPlay").Count, "no interrupted parent or child");
            Check.True(children.All(c => Members(s, FormalSliceFixture.ExhaustPile).Contains(c)), "nested result areas complete");
            Check.Equal(3, Members(s, "base-spire:CardPlayHistory").Count, "nested events complete");
        });
        Check.Run("Orchestration: unplayable autoplay reaches result without effects/payment/history", () =>
        {
            var s = FormalSliceFixture.NewFormal(); var havoc = FormalSliceFixture.MoveToHand(s, "base-spire:havoc");
            var wound = ArrangeDraw(s, "base-spire:wound")[0];
            Play(s, havoc, FormalSliceFixture.Player(s));
            Check.True(Members(s, FormalSliceFixture.ExhaustPile).Contains(wound), "Havoc's forced exhaust applies even when unplayable");
            Check.Equal(1, Members(s, "base-spire:CardPlayHistory").Count, "no fabricated wound play");
        });
        Check.Run("Orchestration: X autoplay pays once, snapshots X before child energy gain and shares manual pipeline", () =>
        {
            var s = FromSource(Definitions(("Cascade", "skill", "autoPlay = { from = draw count = { scaled = { base = 0 perCount = 1 count = { energyX = {} } } } }", "X", ""),
                ("Child", "skill", "modifyEnergy = { amount = 1 }", "2", "")), copies: 4);
            var cascade = FormalSliceFixture.MoveToHand(s, "base-spire:cascade");
            ArrangeDraw(s, "base-spire:child", "base-spire:child", "base-spire:child");
            Play(s, cascade, FormalSliceFixture.Player(s));
            Check.Equal(3L, Number(s, FormalSliceFixture.Player(s), "Energy"), "X=3 paid, three free children each return one energy");
            Check.Equal(4, Members(s, "base-spire:CardPlayHistory").Count, "exactly captured X children");
        });
        Check.Run("Orchestration: explicit generation initializes variant, transient cost and source identity", () =>
        {
            var s = FromSource(Definitions(("Generator", "skill", "generateCards = { count = 1 pool = { card = Child } upgraded = true cost = 0 }", "0", ""),
                ("Child", "attack", "attack = { damage = 4 }", "2", "")));
            var generator = FormalSliceFixture.MoveToHand(s, "base-spire:generator");
            Play(s, generator, FormalSliceFixture.Player(s));
            Check.Equal(1L, s.Random.DrawCount(SliceManifest.CardGenerationStream), "one named generation draw");
            var card = Hand(s).Last(c => FormalSliceFixture.Definition(s, c) == "base-spire:child");
            var type = s.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
            Check.True(s.State.GetField(card, type.FieldIndex("Upgraded")).AsBool(), "generated upgraded variant");
            Check.True(s.State.GetField(card, type.FieldIndex("LongTermSource")).IsNone, "battle-only generation");
            var energy = Number(s, FormalSliceFixture.Player(s), "Energy");
            Play(s, card, FormalSliceFixture.Cultist(s));
            Check.Equal(energy, Number(s, FormalSliceFixture.Player(s), "Energy"), "generated first play is free");
            Check.Equal(0L, s.State.GetField(card, type.FieldIndex("CostModifier")).AsInt(), "free modifier expires after play");
        });
        Check.Run("Orchestration: unsupported command shapes and missing full generation pool fail with source location", () =>
        {
            foreach (var body in new[] { "autoPlay = { from = hand count = 1 }", "drawUntil = { type = typo }", "generateCards = { count = 1 }", "generateCards = { count = 2 distinct = true pool = { card = OrchestrationProbe } }",
                "generateCards = { count = 3 choose = true pool = { card = OrchestrationProbe } }",
                "generateCards = { count = 1 allowCancel = true pool = { card = OrchestrationProbe } }",
                "generateCards = { count = 0 distinct = true choose = true pool = { card = OrchestrationProbe } }",
                "generateCards = { count = 3 distinct = true choose = invalid pool = { card = OrchestrationProbe } }" })
            {
                var parsed = Parse(body);
                Check.Equal(0, parsed.Cards.Count, "no partial capability accepted");
                Check.True(parsed.Unsupported.Single().Reason.Contains("orchestration-probe.spire", StringComparison.Ordinal), "source location retained");
            }
        });
        Check.Run("Orchestration: actual Pillage, Havoc and Cascade source variants execute through the shared procedures", () =>
        {
            foreach (var upgraded in new[] { false, true })
            {
                var pillageRun = FormalSliceFixture.NewFormal();
                var pillage = FormalSliceFixture.MoveToHand(pillageRun, "base-spire:pillage");
                Variant(pillageRun, pillage, upgraded);
                var target = FormalSliceFixture.Cultist(pillageRun); var hp = Hp(pillageRun, target);
                ArrangeDraw(pillageRun, SliceManifest.StrikeId, SliceManifest.DefendId);
                Play(pillageRun, pillage, target);
                Check.Equal(upgraded ? 9L : 6L, hp - Hp(pillageRun, target), "source Pillage damage variant");

                var havocRun = FormalSliceFixture.NewFormal();
                var havoc = FormalSliceFixture.MoveToHand(havocRun, "base-spire:havoc");
                Variant(havocRun, havoc, upgraded);
                ArrangeDraw(havocRun, SliceManifest.StrikeId);
                Play(havocRun, havoc, FormalSliceFixture.Player(havocRun));
                Check.Equal(upgraded ? 3L : 2L, Number(havocRun, FormalSliceFixture.Player(havocRun), "Energy"), "source Havoc payment variant");

                var cascadeRun = FormalSliceFixture.NewFormal();
                var cascade = FormalSliceFixture.MoveToHand(cascadeRun, "base-spire:cascade");
                Variant(cascadeRun, cascade, upgraded);
                ArrangeDraw(cascadeRun, SliceManifest.StrikeId, "base-spire:bash", "base-spire:anger", SliceManifest.DefendId);
                Play(cascadeRun, cascade, FormalSliceFixture.Player(cascadeRun));
                Check.Equal(upgraded ? 5 : 4, Members(cascadeRun, "base-spire:CardPlayHistory").Count, "source Cascade count=X or X+1");
                Check.Equal(0L, Number(cascadeRun, FormalSliceFixture.Player(cascadeRun), "Energy"), "one X payment, free children");
            }
        });
    }

    private static string Definitions(params (string Name, string Kind, string Play, string Cost, string Hooks)[] cards) =>
        "using namespace base-spire; " + string.Join(" ", cards.Select(c => {
            var target = c.Kind == "attack" ? "selectOne = { from = context.opponents where = alive }" : "selectSelf = {}";
            string Variant() => $"type = {c.Kind} rarity = common cost = {c.Cost} target = {{ {target} }} keywords = {{}} play = {{ {c.Play} }} hooks = {{ {c.Hooks} }}";
            return $"card {c.Name} {{ normal = {{ {Variant()} }} upgraded = {{ {Variant()} }} }}";
        }));

    private static SpireCardSource.Source Parse(string body, string cost = "0") => SpireCardSource.Parse(
        Definitions(("OrchestrationProbe", "attack", body, cost, "")), "orchestration-probe.spire", new Dictionary<string, string>());
    private static Session Probe(string body, int copies = 1, string cost = "0") => FromSource(Definitions(("OrchestrationProbe", "attack", body, cost, "")), copies);
    private static Session FromSource(string source, int copies = 1)
    {
        var parsed = SpireCardSource.Parse(source, "orchestration-probe.spire", new Dictionary<string, string>());
        Check.Equal(0, parsed.Unsupported.Count, string.Join("; ", parsed.Unsupported.Select(c => c.Reason)));
        var model = FormalSpirePackage.Load().Model with { Cards = parsed.Cards, Deck = [.. parsed.Cards.Select(c => new AuthorDeckEntry(c.Id, copies))] };
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        s.Start(); Healthy(s); return s;
    }
    private static IReadOnlyList<InstanceId> Members(Session s, string pile) => s.State.CollectionMembers(FormalSliceFixture.Collection(s, pile));
    private static IReadOnlyList<InstanceId> Hand(Session s) => Members(s, FormalSliceFixture.HandPile);
    private static long Hp(Session s, InstanceId enemy) => FormalSliceFixture.Number(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp");
    private static long Number(Session s, InstanceId player, string field) => FormalSliceFixture.Number(s, player, FormalSliceFixture.PlayerTypeId, field);
    private static void Healthy(Session s) => Check.True(s.Status != SessionStatus.Faulted, $"session fault: {s.Fault}");
    private static void Variant(Session s, InstanceId card, bool upgraded)
    {
        // Controlled variant initialization; expected costs below are independent native source constants.
        var recipe = FormalSpirePackage.Load().Model.Cards.Single(c => c.Id == FormalSliceFixture.Definition(s, card));
        var type = s.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
        s.State.RunAtomic(() => {
            s.State.SetField(card, type.FieldIndex("Upgraded"), Value.Of(upgraded));
            s.State.SetField(card, type.FieldIndex("Cost"), Value.Of((long)(upgraded ? recipe.UpgradedCost : recipe.NormalCost)));
        });
    }
    private static int Play(Session s, InstanceId card, InstanceId target) { var n = FormalSliceFixture.PlayAtCountingDecisions(s, card, target); Healthy(s); return n; }
    private static IReadOnlyList<InstanceId> ArrangeDraw(Session s, params string[] ids)
    {
        var selected = new List<InstanceId>();
        foreach (var id in ids)
        {
            var instance = new[] { FormalSliceFixture.HandPile, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile, FormalSliceFixture.ExhaustPile }
                .SelectMany(pile => Members(s, pile)).First(c => !selected.Contains(c) && FormalSliceFixture.Definition(s, c) == id);
            selected.Add(instance);
        }
        var draw = FormalSliceFixture.Collection(s, FormalSliceFixture.DrawPile);
        s.State.RunAtomic(() => {
            foreach (var card in selected.AsEnumerable().Reverse())
            {
                foreach (var pile in new[] { FormalSliceFixture.HandPile, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile, FormalSliceFixture.ExhaustPile })
                {
                    var index = FormalSliceFixture.Collection(s, pile);
                    if (s.State.IsMember(index, card)) s.State.RemoveFromCollection(index, card);
                }
                s.State.AddToCollection(draw, card, InsertPosition.Start);
            }
        });
        return selected;
    }
    private static void NextRoom(Session s)
    {
        var action = s.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == "base-spire:EnterNextRoom");
        s.Submit(action.Definition, SliceManifest.Player, []);
        while (s.Status == SessionStatus.AwaitingDecision) { var p = s.PendingDecision!; s.Answer(new(p.RequestId, [p.Candidates[0]])); }
        Healthy(s);
    }
}
