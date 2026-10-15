using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

// Source expectations: Footwork.cs:14-31, Prowess.cs:20-40,
// EternalArmor.cs:20-34, Debris.cs:10-21, PlatingPower.cs:61-82.
// All sessions use real formal definitions with an explicit deck.
internal static class UnblockedCardFamiliesChecks
{
    public static void PrintCoverage()
    {
        var model = FormalSpirePackage.Load().Model;
        var links = model.Powers.GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: model with { Deck = [new("base-spire:wound", 12)] }).Build());
        _ = builder.Build(); // Actual IR linking, independent of total source deck size.
        foreach (var resource in SpireCardSource.CardResources())
        {
            var source = SpireCardSource.LoadResource(resource, links);
            foreach (var declaration in source.Declared)
            {
                var parsed = source.Parsed.FirstOrDefault(c => c.Name == declaration.Name);
                var lowered = source.Cards.FirstOrDefault(c => c.Name == declaration.Name);
                Console.WriteLine("COVERAGE " + System.Text.Json.JsonSerializer.Serialize(new
                {
                    Name = declaration.Name, Resource = resource, Line = declaration.Line,
                    Parsed = parsed is not null, Lowered = lowered is not null,
                    Registered = lowered is not null && model.Cards.Any(c => c.Id == lowered.Id),
                    Reason = string.Join("; ", source.Unsupported.Where(x => x.Name == declaration.Name).Select(x => x.Reason)),
                    Id = lowered?.Id, Pools = parsed?.Pools, Upgradeable = parsed?.Upgradeable,
                    MultiplayerOnly = parsed?.MultiplayerOnly, CanGenerateInCombat = parsed?.CanGenerateInCombat
                }));
            }
        }
    }

    public static void Run(string? cardId = null)
    {
        foreach (var id in new[] { "footwork", "prowess", "eternal-armor" }.Where(id => cardId is null || id == cardId))
            foreach (var mode in new[] { "normal", "created-upgraded", "armaments-upgraded" })
                Check.Run($"ENV-T12 unblocked01 {id}/{mode}: subsequent benefit, timing and None result", () => Benefits(id, mode));
        if (cardId is null || cardId == "debris")
        {
            Check.Run("ENV-T12 unblocked01 debris: real empty play pays, records and exhausts", DebrisPlay);
            Check.Run("ENV-T12 unblocked01 debris: real Armaments cannot upgrade and unplayed card discards", DebrisUpgradeAndTurn);
        }
    }

    private static Session Start(string id, string mode)
    {
        var model = FormalSpirePackage.Load().Model;
        var deck = new List<AuthorDeckEntry> { new("base-spire:" + id, 1, mode == "created-upgraded"), new("base-spire:defend-silent", 1), new("base-spire:strike-silent", 1), new("base-spire:wound", 12) };
        if (mode == "armaments-upgraded") deck.Add(new("base-spire:armaments", 1));
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: model with { Deck = deck.ToArray() }).Build());
        var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        s.Start(); Healthy(s);
        var card = FormalSliceFixture.MoveToHand(s, "base-spire:" + id);
        if (mode == "armaments-upgraded")
        {
            FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "base-spire:armaments"), card);
            Healthy(s);
            Check.True(s.State.GetField(card, s.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId).FieldIndex("Upgraded")).AsBool(), "real upgrade");
        }
        var player = FormalSliceFixture.Player(s);
        Set(s, player, "Energy", 10); Set(s, player, "Block", 0); Set(s, player, "Hp", 900);
        foreach (var monster in Members(s, "base-spire:Monsters"))
        {
            FormalSliceFixture.SetNumber(s, monster, FormalSliceFixture.MonsterTypeId, "Hp", 1000);
            FormalSliceFixture.GivePower(s, monster, "base-spire:strength", 30);
        }
        return s;
    }

    private static void Benefits(string id, string mode)
    {
        var s = Start(id, mode); var player = FormalSliceFixture.Player(s);
        var card = FormalSliceFixture.MoveToHand(s, "base-spire:" + id);
        Check.Equal(0, FormalSliceFixture.PlayAtCountingDecisions(s, card, player), "self Power has no enemy decision"); Healthy(s);
        Check.Equal(id == "eternal-armor" ? 7L : 9L, Number(s, player, "Energy"), "native cost");
        Check.Equal(0L, Number(s, player, "Block"), "Power grants no immediate block");
        NoneResult(s, card);
        var upgraded = mode != "normal";
        if (id == "eternal-armor")
        {
            var stacks = upgraded ? 12L : 9L;
            Check.Equal(stacks, FormalSliceFixture.PowerStacks(s, player, "base-spire:plating-power"), "source 9/12");
            var control = Start(id, mode); var controlPlayer = FormalSliceFixture.Player(control);
            FormalSliceFixture.EndTurn(s); FormalSliceFixture.EndTurn(control); Healthy(s); Healthy(control);
            Check.Equal(stacks - 1, FormalSliceFixture.PowerStacks(s, player, "base-spire:plating-power"), "next player start decrements once");
            for (var turn = 2; turn <= 3; turn++)
            {
                var before = Number(s, player, "Hp"); var controlBefore = Number(control, controlPlayer, "Hp");
                FormalSliceFixture.EndTurn(s); FormalSliceFixture.EndTurn(control); Healthy(s); Healthy(control);
                var absorbed = controlBefore - Number(control, controlPlayer, "Hp") - (before - Number(s, player, "Hp"));
                Check.Equal(stacks - turn + 1, absorbed, "end-turn unpowered block absorbs exactly current stacks");
                Check.Equal(stacks - turn, FormalSliceFixture.PowerStacks(s, player, "base-spire:plating-power"), "subsequent start decrements, not damage-triggered");
            }
            NoneResult(s, card);
            return;
        }
        var dexterity = id == "footwork" ? (upgraded ? 3L : 2L) : (upgraded ? 2L : 1L);
        Check.Equal(dexterity, FormalSliceFixture.PowerStacks(s, player, "base-spire:dexterity"), "source Dexterity");
        var strength = id == "prowess" ? dexterity : 0;
        Check.Equal(strength, FormalSliceFixture.PowerStacks(s, player, "base-spire:strength"), "source Strength only for Prowess");
        for (var turn = 1; turn <= 2; turn++)
        {
            Set(s, player, "Energy", 10); Set(s, player, "Block", 0);
            FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "base-spire:defend-silent"), player); Healthy(s);
            Check.Equal(5 + dexterity, Number(s, player, "Block"), "subsequent normal Defend gets Dexterity");
            var enemy = FormalSliceFixture.Cultist(s); var hp = FormalSliceFixture.Number(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp");
            FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "base-spire:strike-silent"), enemy); Healthy(s);
            Check.Equal(6 + strength, hp - FormalSliceFixture.Number(s, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "subsequent normal Strike gets Strength");
            if (turn == 1) { FormalSliceFixture.EndTurn(s); Healthy(s); }
        }
        NoneResult(s, card);
    }

    private static void DebrisPlay()
    {
        var s = Start("debris", "normal"); var player = FormalSliceFixture.Player(s);
        var card = FormalSliceFixture.MoveToHand(s, "base-spire:debris");
        var hp = Number(s, player, "Hp"); var played = Members(s, "base-spire:PlayedThisTurn").Count;
        FormalSliceFixture.GivePower(s, player, "base-spire:feel-no-pain", 3);
        Check.Equal(0, FormalSliceFixture.PlayAtCountingDecisions(s, card, FormalSliceFixture.Cultist(s)), "selectNone never consumes supplied enemy"); Healthy(s);
        Check.Equal(9L, Number(s, player, "Energy"), "cost 1 even with zero effects");
        Check.Equal(hp, Number(s, player, "Hp"), "no native HP effect");
        Check.Equal(3L, Number(s, player, "Block"), "real exhaust triggers existing FeelNoPain");
        Check.Equal(played + 1, Members(s, "base-spire:PlayedThisTurn").Count, "empty play recorded");
        Check.True(Members(s, FormalSliceFixture.ExhaustPile).Contains(card), "real exhaust result");
        FormalSliceFixture.EndTurn(s); Healthy(s);
        Check.True(Members(s, FormalSliceFixture.ExhaustPile).Contains(card), "exhausted card does not redraw");
    }

    private static void DebrisUpgradeAndTurn()
    {
        // Upgraded Armaments applies to all hand cards, so this verifies the runtime eligibility gate.
        // The formal definition also forbids upgrades; no artificial upgraded Debris success is claimed.
        var model = FormalSpirePackage.Load().Model;
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: model with { Deck = [new("base-spire:debris", 1), new("base-spire:armaments", 1, true), new("base-spire:wound", 12)] }).Build());
        var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance); s.Start(); Healthy(s);
        var card = FormalSliceFixture.MoveToHand(s, "base-spire:debris"); var type = s.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId);
        FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "base-spire:armaments"), FormalSliceFixture.Player(s)); Healthy(s);
        Check.False(s.State.GetField(card, type.FieldIndex("Upgraded")).AsBool(), "Debris stays non-upgraded after actual all-hand upgrade");
        var definition = model.Cards.Single(c => c.Id == "base-spire:debris");
        Check.False(definition.Upgradeable, "formal declaration is non-upgradeable");
        Check.True(definition.NormalExhaustsSelf && definition.UpgradedExhaustsSelf, "native Exhaust keyword in both required declaration blocks");
        Check.False(s.State.GetField(card, type.FieldIndex("Ethereal")).AsBool(), "not ethereal");
        FormalSliceFixture.EndTurn(s); Healthy(s);
        Check.True(Members(s, FormalSliceFixture.DiscardPile).Contains(card), "unplayed Debris discards, not exhausts at end turn");
    }

    private static void NoneResult(Session s, InstanceId card)
    {
        foreach (var pile in new[] { FormalSliceFixture.HandPile, FormalSliceFixture.DrawPile, FormalSliceFixture.DiscardPile, FormalSliceFixture.ExhaustPile, "base-spire:InPlay" })
            Check.False(Members(s, pile).Contains(card), "Power result None in " + pile);
    }
    private static IReadOnlyList<InstanceId> Members(Session s, string pile) => s.State.CollectionMembers(FormalSliceFixture.Collection(s, pile));
    private static long Number(Session s, InstanceId player, string field) => FormalSliceFixture.Number(s, player, FormalSliceFixture.PlayerTypeId, field);
    private static void Set(Session s, InstanceId player, string field, long value) => FormalSliceFixture.SetNumber(s, player, FormalSliceFixture.PlayerTypeId, field, value);
    private static void Healthy(Session s) => Check.True(s.Status != SessionStatus.Faulted, $"{s.Fault}");
}
