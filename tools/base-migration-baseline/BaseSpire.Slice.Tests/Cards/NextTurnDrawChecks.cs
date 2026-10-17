using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

// Expectations come from Predator.cs, DrawCardsNextTurnPower.cs and Creature.BeforeTurnStart.
// These controlled checks do not claim independent sim/client parity.
internal static class NextTurnDrawChecks
{
    private const string Power = "base-spire:draw-cards-next-turn-power";

    public static void Run()
    {
        foreach (var mode in new[] { "normal", "created-upgraded", "armaments-upgraded" })
            Check.Run($"Next-turn draw: Predator/{mode} pays and damages before deferred draw", () =>
            {
                var model = FormalSpirePackage.Load().Model;
                var definition = model.Cards.Single(c => c.Id == "base-spire:predator");
                var deck = new List<AuthorDeckEntry> { new(definition.Id, 1, mode == "created-upgraded"), new("base-spire:wound", 30) };
                if (mode == "armaments-upgraded") deck.Add(new("base-spire:armaments", 1));
                var s = Start(model with { Deck = deck });
                var card = FormalSliceFixture.MoveToHand(s, definition.Id);
                if (mode == "armaments-upgraded")
                    FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "base-spire:armaments"), card);
                SetEnergy(s, 3);
                var enemy = FormalSliceFixture.Cultist(s);
                var hp = Field(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp");
                var hand = Hand(s);
                FormalSliceFixture.PlayAt(s, card, enemy);
                Healthy(s);
                Check.Equal(hp - (mode == "normal" ? 15 : 20), Field(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "source damage variant");
                Check.Equal(1L, Field(s, FormalSliceFixture.Player(s), FormalSliceFixture.PlayerTypeId, "Energy"), "cost 2");
                Check.Equal(hand - 1, Hand(s), "no immediate extra draw");
                Check.Equal(2L, Stacks(s, FormalSliceFixture.Player(s), Power), "pending extra hand draw");
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(7, Hand(s), "five base cards plus two next-turn cards");
                Check.Equal(0, Owned(s, FormalSliceFixture.Player(s), Power).Count(), "power removed after turn start");
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(5, Hand(s), "no repeated bonus");
            });

        Check.Run("Next-turn draw: two Predator plays stack and consume once", () =>
        {
            var s = Start(FormalSpirePackage.Load().Model with { Deck = [new("base-spire:predator", 2), new("base-spire:wound", 30)] });
            SetEnergy(s, 4);
            for (var i = 0; i < 2; i++) FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "base-spire:predator"), FormalSliceFixture.Cultist(s));
            Check.Equal(4L, Stacks(s, FormalSliceFixture.Player(s), Power), "counter stacks");
            Check.Equal(1, Owned(s, FormalSliceFixture.Player(s), Power).Count(), "one registered instance");
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(9, Hand(s), "five plus four");
            Check.Equal(0, Owned(s, FormalSliceFixture.Player(s), Power).Count(), "consumed instance");
        });

        Check.Run("Next-turn draw: hand limit still consumes the bonus", () =>
        {
            var s = Start(FormalSpirePackage.Load().Model with { Deck = [new("base-spire:wound", 40)] });
            FormalSliceFixture.GivePower(s, FormalSliceFixture.Player(s), Power, 20);
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(SliceManifest.MaxHandSize, Hand(s), "normal draw procedure enforces hand limit");
            Check.Equal(0, Owned(s, FormalSliceFixture.Player(s), Power).Count(), "bonus is not deferred again after overflow");
        });

        Check.Run("Next-turn draw: NoDraw blocks cards but not one-time consumption", () =>
        {
            var s = Start(FormalSpirePackage.Load().Model with { Deck = [new("base-spire:wound", 30)] });
            var player = FormalSliceFixture.Player(s);
            FormalSliceFixture.GivePower(s, player, Power, 2);
            FormalSliceFixture.GivePower(s, player, "base-spire:no-draw", 2);
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(0, Hand(s), "NoDraw remains active through this turn's hand draw");
            Check.Equal(0, Owned(s, player, Power).Count(), "AfterSideTurnStart removes even when draw is prevented");
        });

        Check.Run("Next-turn draw: newly applied turn-start power waits; existing stacks use current magnitude", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var recovery = SpirePowerSource.Parse("package mymod { power Recovery { rules = { handDrawNextTurnOnce = true } } }", "mod-next-draw.spire").Powers.Single();
            // The old grantPower text linker only accepts canonical names. Assemble a controlled
            // author-model hook here to exercise application between capture and hand draw.
            var morning = model.Powers.Single(p => p.Id == "base-spire:demon-form") with
            {
                Id = "mymod:morning", Name = "Morning",
                TriggerEffect = "gain-power-per-stack:1:mymod:recovery"
            };
            var s = Start(model with { Powers = model.Powers.Concat([recovery, morning]).ToArray(), Deck = [new("base-spire:wound", 40)] });
            var player = FormalSliceFixture.Player(s);
            FormalSliceFixture.GivePower(s, player, "mymod:morning", 1);
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(5, Hand(s), "newly created Recovery had zero AmountOnTurnStart");
            Check.Equal(1L, Stacks(s, player, "mymod:recovery"), "not prematurely consumed");
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(7, Hand(s), "preexisting power receives one extra stack before ModifyHandDraw");
            Check.Equal(0, Owned(s, player, "mymod:recovery").Count(), "current full stack count consumed");
        });

        Check.Run("Next-turn draw: enemy power does not modify the player's hand draw", () =>
        {
            var s = Start(FormalSpirePackage.Load().Model with { Deck = [new("base-spire:wound", 30)] });
            var enemy = FormalSliceFixture.Cultist(s);
            FormalSliceFixture.GivePower(s, enemy, Power, 3);
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(5, Hand(s), "owner scope");
            Check.Equal(3L, Stacks(s, enemy, Power), "player hook does not remove enemy's instance");
        });

        Check.Run("Next-turn draw: unrelated negative stat stacks survive turn-start capture", () =>
        {
            var s = Start(FormalSpirePackage.Load().Model with { Deck = [new("base-spire:wound", 30)] });
            var player = FormalSliceFixture.Player(s);
            FormalSliceFixture.GivePower(s, player, "base-spire:strength", -1);
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(5, Hand(s), "no extra draw from an unrelated stat");
            Check.Equal(-1L, Stacks(s, player, "base-spire:strength"), "signed source power magnitude");
        });

        Check.Run("Next-turn draw: unsupported rule shapes are rejected", () =>
        {
            foreach (var rule in new[] { "handDrawNextTurnOnce = false", "handDrawNextTurnOnce = 2", "handDrawNextTurnOnce = true handDrawNextTurnOnce = true" })
            {
                var source = SpirePowerSource.Parse($"package mymod {{ power Bad {{ rules = {{ {rule} }} }} }}", "bad-next-draw.spire");
                Check.Equal(0, source.Powers.Count, rule);
                Check.True(source.Unsupported.Count > 0, "rejection is reported");
            }
        });
    }

    private static Session Start(SliceAuthorModel model)
    {
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(includeTestHeadbutt: false, fromAuthorText: false, authorModel: model).Build());
        var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        s.Start(); Healthy(s); return s;
    }
    private static int Hand(Session s) => s.State.CollectionMembers(FormalSliceFixture.Collection(s, FormalSliceFixture.HandPile)).Count;
    private static IEnumerable<InstanceId> Owned(Session s, InstanceId owner, string id) => s.State.RelationTargets(s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:Powers").Id, owner)
        .Where(p => FormalSliceFixture.Definition(s, p, FormalSliceFixture.PowerTypeId) == id);
    private static long Stacks(Session s, InstanceId owner, string id) => Owned(s, owner, id).Sum(p => Field(s, p, FormalSliceFixture.PowerTypeId, "Stacks"));
    private static long Field(Session s, InstanceId instance, string type, string name) => s.State.GetField(instance, s.RuleSet.TypeByQualifiedId(type).FieldIndex(name)).AsInt();
    private static void SetEnergy(Session s, long amount) => s.State.RunAtomic(() => s.State.SetField(FormalSliceFixture.Player(s), s.RuleSet.TypeByQualifiedId(FormalSliceFixture.PlayerTypeId).FieldIndex("Energy"), Value.Of(amount)));
    private static void Healthy(Session s) => Check.True(s.Status != SessionStatus.Faulted, $"{s.Fault}");
}
