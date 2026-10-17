using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

// Expectations transcribed from frozen sts2/src/Core/Models/Cards/<Name>.cs.
// Controlled formal-source execution; no independent client/sim claim.
internal static class RemainingCardFamiliesChecks
{
    private sealed record Case(string Id, long Cost, long Damage, long UpDamage,
        long Block = 0, long UpBlock = 0, int Draw = 0, long Energy = 0, long UpEnergy = 0,
        bool All = false, bool Exhaust = false, int UpDraw = -1, string Family = "family01",
        string Power = "", long Stacks = 0, long UpStacks = 0, string SecondPower = "",
        bool SelfPower = false, bool RemoveExhaustOnUpgrade = false, long OwnerStacks = 0, bool ResultNone = false, bool Random = false, bool XCost = false);

    private static readonly Case[] Basic =
    [
        new("survivor", 1, 0, 0, 8, 11, Family: "ready03"),
        new("suppress", 0, 11, 17, Power: "weak", Stacks: 3, UpStacks: 5, Family: "ready03"),
        new("lunar-blast", 0, 0, 0, Family: "ready02"),
        new("restlessness", 0, 0, 0, Family: "ready02"),
        new("dagger-throw", 1, 9, 12, Draw: 1, Family: "ready02"),
        new("glimmer", 1, 0, 0, Draw: 3, UpDraw: 4, Family: "ready02"),
        new("graveblast", 1, 4, 6, Exhaust: true, RemoveExhaustOnUpgrade: true, Family: "ready02"),
        new("cosmic-indifference", 1, 0, 0, 6, 9, Family: "ready02"),
        new("photon-cut", 1, 10, 13, Draw: 1, UpDraw: 2, Family: "ready02"),
        new("thinking-ahead", 0, 0, 0, Draw: 2, Exhaust: true, RemoveExhaustOnUpgrade: true, Family: "ready02"),
        new("supercritical", 0, 0, 0, Energy: 4, UpEnergy: 6, Exhaust: true, Family: "ready02"),
        new("reap", 3, 27, 33, Family: "ready02"),
        new("celestial-might", 2, 18, 24, Family: "ready02"),
        new("know-thy-place", 0, 0, 0, Exhaust: true, Family: "ready02", Power: "weak", Stacks: 1, UpStacks: 1, SecondPower: "vulnerable", RemoveExhaustOnUpgrade: true),
        new("minion-sacrifice", 0, 0, 0, 8, 11, Exhaust: true, Family: "ready02"),
        new("squash", 1, 10, 12, Family: "ready02", Power: "vulnerable", Stacks: 2, UpStacks: 3),
        new("exterminate", 1, 12, 16, All: true, Family: "ready02"),
        new("finisher", 1, 0, 0, Family: "ready01"),
        new("malaise", 0, 0, 0, Exhaust: true, Family: "ready01", XCost: true),
        new("leap", 1, 0, 0, 9, 12, Family: "ready01"),
        new("fear", 1, 7, 8, Family: "ready01", Power: "vulnerable", Stacks: 1, UpStacks: 2),
        new("eradicate", 0, 11, 14, Family: "ready01", XCost: true),
        new("volley", 0, 10, 14, Family: "ready01", Random: true, XCost: true),
        new("minion-strike", 0, 6, 9, Draw: 1, Exhaust: true, Family: "ready01"),
        new("minion-dive-bomb", 0, 13, 16, Exhaust: true, Family: "ready01"),
        new("byrd-swoop", 0, 14, 18, Family: "ready01"),
        new("peck", 1, 6, 8, Family: "ready01"),
        new("rip-and-tear", 1, 14, 18, Family: "ready01", Random: true),
        new("leg-sweep", 2, 0, 0, 11, 14, Family: "ready01", Power: "weak", Stacks: 2, UpStacks: 3),
        new("footwork", 1, 0, 0, Family: "unblocked01", Power: "dexterity", Stacks: 2, UpStacks: 3, SelfPower: true, ResultNone: true),
        new("prowess", 1, 0, 0, Family: "unblocked01", Power: "strength", Stacks: 1, UpStacks: 2, SecondPower: "dexterity", SelfPower: true, ResultNone: true),
        new("eternal-armor", 3, 0, 0, Family: "unblocked01", Power: "plating-power", Stacks: 9, UpStacks: 12, SelfPower: true, ResultNone: true),
        new("debris", 1, 0, 0, Exhaust: true, Family: "unblocked01"),
        new("strike-silent", 1, 6, 9),
        new("defend-silent", 1, 0, 0, 5, 8),
        new("slice", 0, 6, 9),
        new("deflect", 0, 0, 0, 4, 7),
        new("backflip", 1, 0, 0, 5, 8, Draw: 2),
        new("dash", 2, 10, 13, 10, 13),
        new("dagger-spray", 1, 8, 12, All: true),
        new("adrenaline", 0, 0, 0, Draw: 2, Energy: 1, UpEnergy: 2, Exhaust: true),
        new("strike-defect", 1, 6, 9),
        new("defend-defect", 1, 0, 0, 5, 8),
        new("strike-necrobinder", 1, 6, 9, Family: "family02"),
        new("defend-necrobinder", 1, 0, 0, 5, 8, Family: "family02"),
        new("strike-regent", 1, 6, 9, Family: "family02"),
        new("defend-regent", 1, 0, 0, 5, 8, Family: "family02"),
        new("sow", 1, 8, 11, All: true, Family: "family02"),
        new("parse", 1, 0, 0, Draw: 3, UpDraw: 4, Family: "family02"),
        new("wisp", 0, 0, 0, Energy: 1, UpEnergy: 1, Exhaust: true, Family: "family02"),
        new("backstab", 0, 11, 15, Exhaust: true, Family: "family02"),
        new("boot-sequence", 0, 0, 0, 10, 13, Exhaust: true, Family: "family02"),
        new("dramatic-entrance", 0, 11, 15, All: true, Exhaust: true, Family: "family03"),
        new("finesse", 0, 0, 0, 4, 7, Draw: 1, Family: "family03"),
        new("flash-of-steel", 0, 5, 8, Draw: 1, Family: "family03"),
        new("master-of-strategy", 0, 0, 0, Draw: 3, UpDraw: 4, Exhaust: true, Family: "family03"),
        new("production", 0, 0, 0, Energy: 2, UpEnergy: 3, Exhaust: true, Family: "family03"),
        new("prophesize", 2, 0, 0, Draw: 6, UpDraw: 9, Family: "family03"),
        new("fuel", 0, 0, 0, Draw: 1, UpDraw: 2, Energy: 1, UpEnergy: 1, Exhaust: true, Family: "family03"),
        new("luminesce", 0, 0, 0, Energy: 2, UpEnergy: 3, Exhaust: true, Family: "family03"),
        new("neutralize", 0, 3, 4, Family: "family04", Power: "weak", Stacks: 1, UpStacks: 2),
        new("sucker-punch", 1, 8, 10, Family: "family04", Power: "weak", Stacks: 1, UpStacks: 2),
        new("beam-cell", 0, 3, 4, Family: "family04", Power: "vulnerable", Stacks: 1, UpStacks: 2),
        new("assassinate", 0, 10, 13, Exhaust: true, Family: "family04", Power: "vulnerable", Stacks: 1, UpStacks: 2),
        new("scare", 0, 0, 0, All: true, Exhaust: true, Family: "family04", Power: "weak", Stacks: 1, UpStacks: 1, RemoveExhaustOnUpgrade: true),
        new("defy", 1, 0, 0, 6, 9, Family: "family04", Power: "weak", Stacks: 1, UpStacks: 1),
        new("putrefy", 1, 0, 0, Exhaust: true, Family: "family04", Power: "weak", Stacks: 2, UpStacks: 3, SecondPower: "vulnerable"),
        new("shockwave", 2, 0, 0, All: true, Exhaust: true, Family: "family04", Power: "weak", Stacks: 3, UpStacks: 5, SecondPower: "vulnerable"),
        new("defile", 1, 13, 17, Exhaust: false, Family: "family05"),
        new("sweeping-beam", 1, 6, 9, Draw: 1, UpDraw: 1, All: true, Family: "family05"),
        new("skim", 1, 0, 0, Draw: 3, UpDraw: 4, Family: "family05"),
        new("ultimate-strike", 1, 14, 20, Family: "family05"),
        new("ultimate-defend", 1, 0, 0, 11, 15, Family: "family05"),
        new("bury", 4, 52, 63, Family: "family05"),
        new("shared-fate", 0, 0, 0, Exhaust: true, Family: "family05", Power: "strength", Stacks: -2, UpStacks: -3, OwnerStacks: -2),
        new("soul", 0, 0, 0, Draw: 2, UpDraw: 3, Exhaust: true, Family: "family05"),
    ];

    public static void Run(string? cardId = null)
    {
        var entries = cardId is null ? Basic : Basic.Where(e => e.Id == cardId).ToArray();
        if (entries.Length == 0) throw new ArgumentException($"Unknown remaining-card check id '{cardId}'.");
        foreach (var entry in entries)
        {
            foreach (var mode in entry.Id == "debris" ? new[] { "normal" } : new[] { "normal", "created-upgraded", "armaments-upgraded" })
            {
                Check.Run($"ENV-T12 {entry.Family} {entry.Id}/{mode}: formal registration, cost, targets, piles and source values", () => {
                    if (entry.XCost) foreach (var x in new[] { 0, 1, 3 }) Verify(entry, mode, x);
                    else if (entry.Id is "finisher" or "lunar-blast" or "restlessness") foreach (var finished in new[] { 0, 2 }) Verify(entry, mode, finished: finished);
                    else Verify(entry, mode);
                });
            }
        }
    }

    private static void Verify(Case entry, string mode, int x = 3, int finished = 0)
    {
        var id = "base-spire:" + entry.Id;
        var model = FormalSpirePackage.Load().Model;
        // Formal definitions, controlled deck. Four Wounds distinguish draw count without reshuffle.
        var supply = entry.Id == "prophesize" ? 9 : 4;
        var deck = mode == "armaments-upgraded"
            ? new AuthorDeckEntry[] { new(id, 1), new("base-spire:armaments", 1), new("base-spire:wound", supply) }
            : new AuthorDeckEntry[] { new(id, 1, mode == "created-upgraded"), new("base-spire:wound", supply) };
        if (entry.Id == "finisher") deck = [.. deck, new("base-spire:strike-silent", 2)];
        if (entry.Id == "lunar-blast") deck = [.. deck, new("base-spire:defend-silent", 2)];
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: model with { Deck = deck }).Build());
        var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        s.Start();
        Check.True(s.Status != SessionStatus.Faulted, $"formal startup: {s.Fault}");
        var card = FormalSliceFixture.MoveToHand(s, id);
        var hand = FormalSliceFixture.Collection(s, FormalSliceFixture.HandPile);
        var draw = FormalSliceFixture.Collection(s, FormalSliceFixture.DrawPile);
        var wounds = Members(s, FormalSliceFixture.HandPile).Where(c => c != card && FormalSliceFixture.Definition(s, c) == "base-spire:wound").ToArray();
        s.State.RunAtomic(() => { foreach (var wound in wounds) { s.State.RemoveFromCollection(hand, wound); s.State.AddToCollection(draw, wound); } });
        if (mode == "armaments-upgraded")
        {
            var armaments = FormalSliceFixture.MoveToHand(s, "base-spire:armaments");
            FormalSliceFixture.PlayAt(s, armaments, card);
            var type = s.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
            Check.True(s.State.GetField(card, type.FieldIndex("Upgraded")).AsBool(), "real upgrade action applied");
        }
        var player = FormalSliceFixture.Player(s);
        var startingEnergy = entry.XCost ? x : Math.Max(3, entry.Cost);
        FormalSliceFixture.SetNumber(s, player, FormalSliceFixture.PlayerTypeId, "Energy", startingEnergy);
        FormalSliceFixture.SetNumber(s, player, FormalSliceFixture.PlayerTypeId, "Block", 0);
        var front = FormalSliceFixture.Cultist(s);
        var back = FormalSliceFixture.Cultist(s, SliceManifest.DampCultistId);
        FormalSliceFixture.SetNumber(s, front, FormalSliceFixture.MonsterTypeId, "Hp", 1000);
        FormalSliceFixture.SetNumber(s, back, FormalSliceFixture.MonsterTypeId, "Hp", 1000);
        for (var i = 0; i < (entry.Id == "restlessness" ? 0 : finished); i++)
        {
            FormalSliceFixture.SetNumber(s, player, FormalSliceFixture.PlayerTypeId, "Energy", 3);
            FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, entry.Id == "lunar-blast" ? "base-spire:defend-silent" : "base-spire:strike-silent"), front);
            Check.True(s.Status != SessionStatus.Faulted, $"history setup: {s.Fault}");
        }
        FormalSliceFixture.SetNumber(s, player, FormalSliceFixture.PlayerTypeId, "Energy", startingEnergy);
        FormalSliceFixture.SetNumber(s, front, FormalSliceFixture.MonsterTypeId, "Hp", 1000);
        FormalSliceFixture.SetNumber(s, player, FormalSliceFixture.PlayerTypeId, "Block", 0);
        var discardChoice = entry.Id is "graveblast" or "cosmic-indifference";
        var handChoice = entry.Id is "dagger-throw" or "survivor" or "glimmer" or "photon-cut" or "thinking-ahead";
        InstanceId selected = default;
        if (discardChoice || entry.Id == "restlessness")
        {
            s.State.RunAtomic(() => {
                foreach (var other in Members(s, FormalSliceFixture.HandPile).Where(c => c != card).ToArray())
                { s.State.RemoveFromCollection(hand, other); s.State.AddToCollection(draw, other); }
                if (discardChoice)
                {
                    var discard = FormalSliceFixture.Collection(s, FormalSliceFixture.DiscardPile);
                    foreach (var other in Members(s, FormalSliceFixture.DiscardPile).ToArray())
                    { s.State.RemoveFromCollection(discard, other); s.State.AddToCollection(draw, other); }
                    selected = Members(s, FormalSliceFixture.DrawPile).First(c => FormalSliceFixture.Definition(s, c) == "base-spire:wound");
                    s.State.RemoveFromCollection(draw, selected); s.State.AddToCollection(discard, selected);
                }
            });
            if (entry.Id == "restlessness" && finished > 0) FormalSliceFixture.MoveToHand(s, "base-spire:wound");
        }
        if (entry.Id == "survivor") FormalSliceFixture.MoveToHand(s, "base-spire:wound");
        var frontHp = Number(s, front, FormalSliceFixture.MonsterTypeId, "Hp");
        var backHp = Number(s, back, FormalSliceFixture.MonsterTypeId, "Hp");
        var held = Members(s, FormalSliceFixture.HandPile).Count;
        Check.Equal(entry.Cost, Number(s, card, FormalSliceFixture.CardTypeId, "Cost"), "source cost");
        var decisions = 0;
        var action = s.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == FormalSliceFixture.PlayCardAction);
        s.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]);
        while (s.Status == SessionStatus.AwaitingDecision)
        {
            var pending = s.PendingDecision!;
            var chosen = pending.Candidates.Contains(front) ? front : pending.Candidates[0];
            if (handChoice && chosen != front) selected = chosen;
            s.Answer(new DecisionAnswer(pending.RequestId, [chosen]));
            decisions++;
        }
        if (handChoice && selected == default)
            selected = Members(s, entry.Id is "dagger-throw" or "survivor" ? FormalSliceFixture.DiscardPile : FormalSliceFixture.DrawPile)
                .First(c => c != card && FormalSliceFixture.Definition(s, c) == "base-spire:wound");
        Check.True(s.Status != SessionStatus.Faulted, $"play: {s.Fault}");
        var upgraded = mode != "normal";
        var cardType = s.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
        if (entry.Id is "parse" or "defile")
            Check.True(s.State.GetField(card, cardType.FieldIndex("Ethereal")).AsBool(), "source ethereal keyword");
        var damage = upgraded ? entry.UpDamage : entry.Damage;
        if (entry.Id == "finisher") damage = finished * (upgraded ? 8 : 6);
        if (entry.Id == "lunar-blast") damage = (finished + (mode == "armaments-upgraded" ? 1 : 0)) * (upgraded ? 5 : 4);
        if (entry.XCost && entry.Id != "malaise") damage *= x;
        if (entry.Random)
            Check.Equal(damage, frontHp + backHp - Number(s, front, FormalSliceFixture.MonsterTypeId, "Hp") - Number(s, back, FormalSliceFixture.MonsterTypeId, "Hp"), "random hits total across living enemies");
        else
        {
            Check.Equal(damage, frontHp - Number(s, front, FormalSliceFixture.MonsterTypeId, "Hp"), "selected enemy damage");
            Check.Equal(entry.All ? damage : 0L, backHp - Number(s, back, FormalSliceFixture.MonsterTypeId, "Hp"), "all versus single target");
        }
        if (entry.Id == "malaise")
        {
            var amount = x + (upgraded ? 1 : 0);
            Check.Equal(-(long)amount, FormalSliceFixture.PowerStacks(s, front, "base-spire:strength"), "single negative Strength application from paid X");
            Check.Equal((long)amount, FormalSliceFixture.PowerStacks(s, front, "base-spire:weak"), "single Weak application from paid X");
        }
        Check.Equal(upgraded ? entry.UpBlock : entry.Block, Number(s, player, FormalSliceFixture.PlayerTypeId, "Block"), "owner block even with enemy target");
        var expectedDraw = upgraded && entry.UpDraw >= 0 ? entry.UpDraw : entry.Draw;
        var expectedEnergy = upgraded ? entry.UpEnergy : entry.Energy;
        if (entry.Id == "restlessness") { expectedDraw = finished == 0 ? upgraded ? 3 : 2 : 0; expectedEnergy = expectedDraw; }
        Check.Equal(startingEnergy - (entry.XCost ? x : entry.Cost) + expectedEnergy, Number(s, player, FormalSliceFixture.PlayerTypeId, "Energy"), "fee and energy order");
        Check.Equal(held - 1 + expectedDraw - (handChoice ? 1 : 0) + (entry.Id == "graveblast" ? 1 : 0), Members(s, FormalSliceFixture.HandPile).Count, "draw/selection count after leaving hand");
        var enemyChoice = !entry.Random && !entry.All && (damage > 0 || entry.Power.Length > 0 && !entry.SelfPower || entry.Id is "finisher" or "lunar-blast" or "malaise" or "eradicate");
        Check.Equal((enemyChoice ? 1 : 0) + (handChoice && held - 1 + expectedDraw > 1 ? 1 : 0), decisions, "target and card selection decisions");
        if (discardChoice || handChoice)
        {
            var destination = entry.Id == "graveblast" ? FormalSliceFixture.HandPile : entry.Id is "dagger-throw" or "survivor" ? FormalSliceFixture.DiscardPile : FormalSliceFixture.DrawPile;
            Check.True(Members(s, destination).Contains(selected), "chosen card arrives at source destination");
            if (destination == FormalSliceFixture.DrawPile)
                Check.Equal(selected, Members(s, destination)[0], "chosen card is next draw (top)");
        }
        if (entry.Power.Length > 0)
        {
            foreach (var power in new[] { entry.Power, entry.SecondPower }.Where(p => p.Length > 0))
            {
                var expected = upgraded ? entry.UpStacks : entry.Stacks;
                Check.Equal(entry.SelfPower ? expected : entry.OwnerStacks, FormalSliceFixture.PowerStacks(s, player, "base-spire:" + power), "owner power target");
                Check.Equal(entry.SelfPower ? 0L : expected, FormalSliceFixture.PowerStacks(s, front, "base-spire:" + power), "selected enemy power target");
                Check.Equal(entry.All ? expected : 0L, FormalSliceFixture.PowerStacks(s, back, "base-spire:" + power), "rear enemy power target");
            }
        }
        if (entry.ResultNone)
        {
            foreach (var pile in new[] { FormalSliceFixture.HandPile, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile, FormalSliceFixture.ExhaustPile, "base-spire:InPlay" })
                Check.False(Members(s, pile).Contains(card), "power card leaves combat piles");
        }
        else
            Check.True(Members(s, entry.Exhaust && !(upgraded && entry.RemoveExhaustOnUpgrade) ? FormalSliceFixture.ExhaustPile : FormalSliceFixture.DiscardPile).Contains(card), "source postplay pile");
    }

    private static IReadOnlyList<InstanceId> Members(Session s, string pile) => s.State.CollectionMembers(FormalSliceFixture.Collection(s, pile));
    private static long Number(Session s, InstanceId instance, string type, string field) => FormalSliceFixture.Number(s, instance, type, field);
}
