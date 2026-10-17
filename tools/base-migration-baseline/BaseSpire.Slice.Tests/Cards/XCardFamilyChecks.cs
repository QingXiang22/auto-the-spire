using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

// Native Skewer.cs:16-37 / SilentCardPool.cs:97. Source-derived constants,
// paid X repeated attacks (not damage*X), using explicit formal-source decks.
internal static class XCardFamilyChecks
{
    public static void Run(bool benefitsOnly = false)
    {
        foreach (var mode in new[] { "normal", "created-upgraded", "armaments-upgraded" })
        {
            if (!benefitsOnly)
                Check.Run($"ENV-T12 unblocked02 skewer/{mode}: source X cost, X=0/1/3, targets and piles",
                    () => { foreach (var x in new[] { 0, 1, 3 }) Verify(mode, x, false); });
            else
                Check.Run($"ENV-T12 unblocked02 skewer/{mode}: per-hit Strength, Weak rounding and block consumption",
                    () => Verify(mode, 3, true));
        }
    }

    private static void Verify(string mode, int x, bool modifiers)
    {
        var model = FormalSpirePackage.Load().Model;
        var definition = model.Cards.Single(c => c.Id == "base-spire:skewer");
        Check.True(definition.IsXCost, "native X cost; both actual variants tested below");
        Check.True(definition.Pools.SequenceEqual(new[] { "SilentCardPool" }), "native source pool");
        var deck = new List<AuthorDeckEntry> { new("base-spire:skewer", 1, mode == "created-upgraded"), new("base-spire:wound", 12) };
        if (mode == "armaments-upgraded") deck.Add(new("base-spire:armaments", 1));
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: model with { Deck = deck.ToArray() }).Build());
        var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        s.Start(); Healthy(s);
        var card = FormalSliceFixture.MoveToHand(s, "base-spire:skewer");
        if (mode == "armaments-upgraded")
        {
            FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "base-spire:armaments"), card); Healthy(s);
        }
        var type = s.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
        Check.Equal(mode != "normal", s.State.GetField(card, type.FieldIndex("Upgraded")).AsBool(), "actual created/Armaments upgrade state");
        var player = FormalSliceFixture.Player(s);
        FormalSliceFixture.SetNumber(s, player, FormalSliceFixture.PlayerTypeId, "Energy", x);
        var target = FormalSliceFixture.Cultist(s);
        var other = FormalSliceFixture.Cultist(s, SliceManifest.DampCultistId);
        foreach (var enemy in new[] { target, other })
        {
            FormalSliceFixture.SetNumber(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 1000);
            FormalSliceFixture.SetNumber(s, enemy, FormalSliceFixture.MonsterTypeId, "Block", 0);
        }
        if (modifiers)
        {
            FormalSliceFixture.GivePower(s, player, "base-spire:strength", 2);
            FormalSliceFixture.GivePower(s, player, "base-spire:weak", 1);
            FormalSliceFixture.SetNumber(s, target, FormalSliceFixture.MonsterTypeId, "Block", 5);
        }
        var decisions = FormalSliceFixture.PlayAtCountingDecisions(s, card, target); Healthy(s);
        Check.Equal(1, decisions, "AnyEnemy chooses exactly one target, including X=0");
        Check.Equal(0L, FormalSliceFixture.Number(s, player, FormalSliceFixture.PlayerTypeId, "Energy"), "pays actual X");
        var hit = mode == "normal" ? 8L : 11L;
        if (modifiers) hit = (hit + 2) * 3 / 4; // Native Strength +2, Weak floors each attack.
        var expected = modifiers ? x * hit - 5 : x * hit;
        Check.Equal(expected, 1000 - FormalSliceFixture.Number(s, target, FormalSliceFixture.MonsterTypeId, "Hp"), "source damage per actual hit, not scaled one-hit aggregate");
        Check.Equal(1000L, FormalSliceFixture.Number(s, other, FormalSliceFixture.MonsterTypeId, "Hp"), "unselected enemy untouched");
        Check.Equal(0L, FormalSliceFixture.Number(s, target, FormalSliceFixture.MonsterTypeId, "Block"), "native attacks consume selected target block");
        Check.True(Members(s, FormalSliceFixture.DiscardPile).Contains(card), "Attack result discard even X=0");
        Check.False(Members(s, FormalSliceFixture.ExhaustPile).Contains(card), "no native Exhaust");
        Check.False(Members(s, FormalSliceFixture.HandPile).Contains(card), "played card removed from hand");
        Check.True(Members(s, "base-spire:PlayedThisTurn").Contains(card), "play recorded even X=0");
    }

    private static IReadOnlyList<InstanceId> Members(Session s, string pile) => s.State.CollectionMembers(FormalSliceFixture.Collection(s, pile));
    private static void Healthy(Session s) => Check.True(s.Status != SessionStatus.Faulted, $"{s.Fault}");
}
