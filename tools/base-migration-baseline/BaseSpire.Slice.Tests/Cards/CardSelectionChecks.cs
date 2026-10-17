using BaseSpire.Slice;
using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class CardSelectionChecks
{
    public static void Run()
    {
        foreach (var upgraded in new[] { false, true })
        {
            Check.Run($"Selection: Acrobatics/{upgraded} draws before pause and pays once", () =>
            {
                var s = Start("base-spire:acrobatics", upgraded);
                var card = FormalSliceFixture.MoveToHand(s, "base-spire:acrobatics");
                Normalize(s, card, 1);
                var beforeDraw = Members(s, FormalSliceFixture.DrawPile).Count;
                Submit(s, card);
                Check.Equal(SessionStatus.AwaitingDecision, s.Status, "real hand discard decision");
                Check.Equal(beforeDraw - (upgraded ? 4 : 3), Members(s, FormalSliceFixture.DrawPile).Count, "native draw already resolved");
                Check.Equal(2L, Energy(s), "cost paid before selection");
                Check.False(s.PendingDecision!.Candidates.Contains(card), "in-play card excluded");
                var chosen = s.PendingDecision.Candidates.Last();
                s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, [chosen])); Healthy(s);
                Check.Equal(2L, Energy(s), "resumption does not pay twice");
                Check.True(Members(s, FormalSliceFixture.DiscardPile).Contains(chosen), "actual chosen card discarded");
                Check.True(Members(s, FormalSliceFixture.DiscardPile).Contains(card), "source result after selection");
                Check.Equal(1, Members(s, "base-spire:PlayedThisTurn").Count, "one play only");
            });
            Check.Run($"Selection: Prepared/{upgraded} exact count and automatic insufficient candidates", () =>
            {
                var s = Start("base-spire:prepared", upgraded);
                var card = FormalSliceFixture.MoveToHand(s, "base-spire:prepared");
                Normalize(s, card, 2);
                Submit(s, card);
                var pending = s.PendingDecision!;
                Check.Equal(SessionStatus.AwaitingDecision, s.Status, "choice required among alternatives");
                var count = upgraded ? 2 : 1;
                Check.Equal((long)count, pending.Minimum, "native exact min");
                Check.Equal((long)count, pending.Maximum, "native exact max");
                Check.Equal(upgraded ? DecisionResponseKind.EntityRefList : DecisionResponseKind.EntityRef, pending.Kind, "one multi-select, not repeated single picks");
                var chosen = pending.Candidates.TakeLast(count).ToArray();
                s.Answer(new DecisionAnswer(pending.RequestId, chosen)); Healthy(s);
                Check.True(chosen.All(c => Members(s, FormalSliceFixture.DiscardPile).Contains(c)), "all selected refs move");
                Check.Equal(3L, Energy(s), "native free play");
                // Native selector auto-confirms when fewer candidates exist than requested.
                var sparse = Start("base-spire:prepared", upgraded);
                var source = FormalSliceFixture.MoveToHand(sparse, "base-spire:prepared");
                Normalize(sparse, source, 1, draw: false);
                Submit(sparse, source); Healthy(sparse);
                Check.Equal(SessionStatus.Running, sparse.Status, "available cards automatically selected");
                Check.Equal(0, Members(sparse, FormalSliceFixture.HandPile).Count, "candidate shortage clamps, no impossible decision");
            });
            Check.Run($"Selection: Hologram/{upgraded} block then discard recovery and variant result", () =>
            {
                var s = Start("base-spire:hologram", upgraded);
                var card = FormalSliceFixture.MoveToHand(s, "base-spire:hologram");
                Normalize(s, card, 1);
                var recover = Members(s, FormalSliceFixture.DrawPile).Take(2).ToArray();
                foreach (var victim in recover) Move(s, victim, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile);
                Submit(s, card);
                Check.Equal(upgraded ? 5L : 3L, Number(s, "Block"), "gain block before recovery decision");
                Check.True(s.PendingDecision!.Candidates.SequenceEqual(recover), "discard candidates only");
                s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, [recover[1]])); Healthy(s);
                Check.True(Members(s, FormalSliceFixture.HandPile).Contains(recover[1]), "chosen identity recovered");
                Check.True(Members(s, FormalSliceFixture.DiscardPile).Contains(recover[0]), "unchosen stays");
                Check.True(Members(s, upgraded ? FormalSliceFixture.DiscardPile : FormalSliceFixture.ExhaustPile).Contains(card), "native upgrade removes Exhaust");
            });
        }
        Check.Run("Selection: zero/empty/automatic exact candidates continue remaining effects", () =>
        {
            foreach (var (count, held) in new[] { (0, 2), (1, 0), (2, 2) })
            {
                var (s, card) = Probe($"chooseCard = {{ from = hand to = discard count = {count} autoSelect = true }} gainBlock = {{ amount = 7 }}");
                Normalize(s, card, held, draw: false); Submit(s, card); Healthy(s);
                Check.Equal(SessionStatus.Running, s.Status, "no empty or redundant prompt");
                Check.Equal(7L, Number(s, "Block"), "continues after choice");
                Check.Equal(count == 0 ? held : 0, Members(s, FormalSliceFixture.HandPile).Count, "correct automatic movement");
            }
        });
        Check.Run("Selection: filtered candidates and common exhaust hook", () =>
        {
            var (s, card) = Probe("chooseCard = { from = hand to = exhaust filter = attack }");
            Normalize(s, card, 0);
            var attack = FormalSliceFixture.MoveToHand(s, "base-spire:strike-silent");
            var wound = FormalSliceFixture.MoveToHand(s, "base-spire:wound");
            FormalSliceFixture.GivePower(s, FormalSliceFixture.Player(s), "base-spire:feel-no-pain", 3);
            Submit(s, card);
            Check.True(s.PendingDecision!.Candidates.SequenceEqual(new[] { attack }), "filter does not accept status");
            s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, [attack])); Healthy(s);
            Check.True(Members(s, FormalSliceFixture.ExhaustPile).Contains(attack), "exhaust command used");
            Check.True(Members(s, FormalSliceFixture.HandPile).Contains(wound), "unchosen untouched");
            Check.Equal(3L, Number(s, "Block"), "common exhaustion event");
        });
        Check.Run("Selection: full hand recovery stays in discard", () =>
        {
            var (s, card) = Probe("draw = { amount = 10 } chooseCard = { from = discard to = hand autoSelect = true }");
            Normalize(s, card, 0);
            var recover = Members(s, FormalSliceFixture.DrawPile).Last();
            Move(s, recover, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile);
            Submit(s, card); Healthy(s);
            Check.Equal(SliceManifest.MaxHandSize, Members(s, FormalSliceFixture.HandPile).Count, "never overflows hand");
            Check.True(Members(s, FormalSliceFixture.DiscardPile).Contains(recover), "native hand overflow routes to discard");
        });
        Check.Run("Selection: existing discard-to-draw-top pauses and draws the chosen identity", () =>
        {
            var (s, card) = Probe("chooseCard = { from = discard to = draw destinationPosition = top } draw = { amount = 1 }");
            Normalize(s, card, 0);
            var recover = Members(s, FormalSliceFixture.DrawPile).Take(2).ToArray();
            foreach (var victim in recover) Move(s, victim, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile);
            Submit(s, card);
            Check.Equal(SessionStatus.AwaitingDecision, s.Status, "legacy default retains manual selection");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [recover[1]])); Healthy(s);
            Check.True(Members(s, FormalSliceFixture.HandPile).Contains(recover[1]), "next draw gets selected top card");
            Check.True(Members(s, FormalSliceFixture.DiscardPile).Contains(recover[0]), "other candidate remains");
        });
        Check.Run("Selection: discard hook fires only for the selected manual discard", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var source = SpireCardSource.Parse("using namespace mymod; "
                + Card("Victim", "", "onDiscard = { modifyEnergy = { amount = 2 } }")
                + Card("Chooser", "chooseCard = { from = hand to = discard autoSelect = true }"), "discard-hooks.spire");
            Check.Equal(0, source.Unsupported.Count, "real hook lowering");
            var s = Start(model with { Cards = model.Cards.Concat(source.Cards).ToArray(), Deck = [new("mymod:victim", 1), new("mymod:chooser", 1)] });
            Submit(s, FormalSliceFixture.MoveToHand(s, "mymod:chooser")); Healthy(s);
            Check.Equal(5L, Energy(s), "manual discard hook once");
            FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "mymod:victim"), FormalSliceFixture.Player(s));
            Check.Equal(5L, Energy(s), "normal result is not manual discard");
            var end = Start(model with { Cards = model.Cards.Concat(source.Cards).ToArray(), Deck = [new("mymod:victim", 1)] });
            FormalSliceFixture.EndTurn(end); Healthy(end);
            Check.Equal(3L, Energy(end), "turn-end discard does not trigger manual hook");
        });
        Check.Run("Selection composition: one selected identity upgrades, clones and moves before remaining effects", () =>
        {
            var (s, card) = Probe("""
                chooseCard = {
                    from = discard filter = upgradeable prompt = "Upgrade and recover"
                    onSelected = {
                        upgradeSelectedCard = {}
                        cloneSelectedCard = { pile = discard }
                        moveSelectedCard = { from = discard to = draw destinationPosition = top }
                    }
                }
                draw = { amount = 1 }
                modifyEnergy = { amount = 2 }
                """);
            Normalize(s, card, 0);
            var victim = Members(s, FormalSliceFixture.DrawPile).Single(c => FormalSliceFixture.Definition(s, c) == "base-spire:strike-silent");
            Move(s, victim, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile);
            Submit(s, card);
            Check.Equal(SessionStatus.AwaitingDecision, s.Status, "pause before selected effects");
            Check.Equal("Upgrade and recover", s.PendingDecision!.Prompt, "explicit selection prompt preserved");
            Check.Equal(3L, Energy(s), "remaining effects have not run");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [victim])); Healthy(s);
            Check.True(Members(s, FormalSliceFixture.HandPile).Contains(victim), "same selected instance returned via top draw");
            var type = s.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
            Check.True(s.State.GetField(victim, type.FieldIndex("Upgraded")).AsBool(), "selected instance upgraded");
            var clone = Members(s, FormalSliceFixture.DiscardPile).Single(c => FormalSliceFixture.Definition(s, c) == "base-spire:strike-silent");
            Check.True(clone != victim, "independent clone");
            Check.True(s.State.GetField(clone, type.FieldIndex("Upgraded")).AsBool(), "clone sees preceding upgrade");
            Check.Equal(Value.OfRef(victim), s.State.GetField(clone, type.FieldIndex("CloneOf")), "clone uses selected card, not played chooser");
            Check.Equal(5L, Energy(s), "remaining effect resumes once");
        });
        Check.Run("Selection composition: lexical count, nested power links and current-card restrictions", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var source = SpireCardSource.Parse("using namespace mymod; " + Card("Linked", """
                let = { name = captured value = 2 effect = {
                    chooseCard = { from = hand autoSelect = true onSelected = {
                        applyPower = { power = base-spire:strength stacks = { savedCount = { name = captured } } target = context.owner }
                    } }
                } }
                """), "nested-selection.spire", model.Powers.ToDictionary(p => p.Name, p => p.Id, StringComparer.Ordinal));
            Check.Equal(0, source.Unsupported.Count, string.Join(";", source.Unsupported.Select(e => e.Reason)));
            var s = Start(model with { Cards = model.Cards.Concat(source.Cards).ToArray(), Deck = [new("mymod:linked", 1), new("base-spire:wound", 14)] });
            var card = FormalSliceFixture.MoveToHand(s, "mymod:linked"); Normalize(s, card, 1);
            Submit(s, card); Healthy(s);
            Check.Equal(2L, FormalSliceFixture.PowerStacks(s, FormalSliceFixture.Player(s), "base-spire:strength"), "child inherits count and linked power");
            foreach (var effect in new[] { "upgradeSelectedCard = {}", "cloneSelectedCard = { pile = discard }" })
                Check.True(SpireCardSource.Parse("using namespace mymod; " + Card("Unbound", effect), "unbound.spire").Unsupported.Count > 0, "unbound selected instance rejected");
        });
        Check.Run("Selection: invalid fields and unsupported hook decisions reject at source", () =>
        {
            foreach (var body in new[] { "from = hand to = discard count = -1", "from = hand to = discard count = 1 count = 2", "from = hand to = discard filter = skill", "from = hand to = discard unknown = 1", "from = hand to = discard destinationPosition = top", "from = hand to = discard autoSelect = 1" })
                Check.True(SpireCardSource.Parse("using namespace mymod; " + Card("Bad", "chooseCard = { " + body + " }"), "bad-selection.spire").Unsupported.Count > 0, body);
            var badHook = SpireCardSource.Parse("using namespace mymod; " + Card("Bad", "", "onDiscard = { chooseCard = { from = hand to = discard } }"), "bad-hook.spire");
            Check.True(badHook.Unsupported.Count > 0, "discard hook cannot recursively initiate a choice");
        });
    }

    private static (Session, InstanceId) Probe(string play)
    {
        var model = FormalSpirePackage.Load().Model;
        var source = SpireCardSource.Parse("using namespace mymod; " + Card("Probe", play), "selection-probe.spire");
        Check.Equal(0, source.Unsupported.Count, string.Join(";", source.Unsupported.Select(e => e.Reason)));
        var s = Start(model with { Cards = model.Cards.Concat(source.Cards).ToArray(), Deck = [new("mymod:probe", 1), new("base-spire:wound", 14), new("base-spire:strike-silent", 1)] });
        return (s, FormalSliceFixture.MoveToHand(s, "mymod:probe"));
    }
    private static string Card(string name, string play, string hooks = "")
    {
        var variant = $"type = skill rarity = common cost = 0 target = {{ selectSelf = {{}} }} keywords = {{}} play = {{ {play} }} hooks = {{ {hooks} }}";
        return $"card {name} {{ upgradeable = false normal = {{ {variant} }} upgraded = {{ {variant} }} }}";
    }
    private static Session Start(string id, bool upgraded) => Start(FormalSpirePackage.Load().Model with { Deck = [new(id, 1, upgraded), new("base-spire:wound", 14)] });
    private static Session Start(SliceAuthorModel model)
    {
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance); s.Start(); Healthy(s); return s;
    }
    private static void Normalize(Session s, InstanceId card, int held, bool draw = true)
    {
        foreach (var other in Members(s, FormalSliceFixture.HandPile).Where(c => c != card).Skip(held).ToArray())
            Move(s, other, FormalSliceFixture.HandPile, draw ? FormalSliceFixture.DrawPile : FormalSliceFixture.ExhaustPile);
        if (!draw) foreach (var other in Members(s, FormalSliceFixture.DrawPile).ToArray()) Move(s, other, FormalSliceFixture.DrawPile, FormalSliceFixture.ExhaustPile);
        FormalSliceFixture.SetNumber(s, FormalSliceFixture.Player(s), FormalSliceFixture.PlayerTypeId, "Energy", 3);
        FormalSliceFixture.SetNumber(s, FormalSliceFixture.Player(s), FormalSliceFixture.PlayerTypeId, "Block", 0);
    }
    private static void Move(Session s, InstanceId card, string from, string to) => s.State.RunAtomic(() => { s.State.RemoveFromCollection(FormalSliceFixture.Collection(s, from), card); s.State.AddToCollection(FormalSliceFixture.Collection(s, to), card); });
    private static void Submit(Session s, InstanceId card) { var action = s.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == FormalSliceFixture.PlayCardAction); s.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]); Healthy(s); }
    private static IReadOnlyList<InstanceId> Members(Session s, string pile) => s.State.CollectionMembers(FormalSliceFixture.Collection(s, pile));
    private static long Number(Session s, string field) => FormalSliceFixture.Number(s, FormalSliceFixture.Player(s), FormalSliceFixture.PlayerTypeId, field);
    private static long Energy(Session s) => Number(s, "Energy");
    private static void Healthy(Session s) => Check.True(s.Status != SessionStatus.Faulted, $"{s.Fault}");
}
