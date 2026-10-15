using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class CombatOrbChecks
{
    public static void Run()
    {
        foreach (var baseSlots in new[] { 0, 3 })
            Check.Run($"Orbs: zero capacity respects character base slots/{baseSlots}", () =>
            {
                var session = Start("removeOrbSlots = { amount = 10 } channelOrb = { kind = Frost }", baseSlots);
                Play(session);
                Check.Equal(baseSlots == 0 ? 1L : 0L, Number(session, "OrbSlots"), "only characters without base slots receive a fallback");
                Check.Equal(baseSlots == 0 ? 1 : 0, Queue(session).Count, "no enqueue into zero capacity");
            });
        Check.Run("Orbs: overflow evokes front; removing slots discards back without evoke", () =>
        {
            var session = Start("channelOrb = { kind = Frost } channelOrb = { kind = Plasma } channelOrb = { kind = Dark } removeOrbSlots = { amount = 1 }", 2);
            Play(session);
            Check.Equal(5L, Number(session, "Block"), "Frost overflow evoke");
            Check.Equal(3L, Number(session, "Energy"), "removed Dark did not evoke Plasma");
            Check.Equal("Plasma", Kind(session, Queue(session).Single()), "front retained after back removal");
        });
        Check.Run("Orbs: non-dequeue repeats preserve identity and plasma is independent of focus", () =>
        {
            var session = Start("channelOrb = { kind = Plasma } evokeOrb = { amount = 2 dequeue = false }", 3);
            Play(session);
            Check.Equal(7L, Number(session, "Energy"), "two evokes gain four energy");
            var retained = Queue(session).Single();
            FormalSliceFixture.EndTurn(session);
            Healthy(session);
            Check.Equal(4L, Number(session, "Energy"), "passive runs after next turn energy reset");
            Check.Equal(retained, Queue(session).Single(), "passive preserves identity");
        });
        Check.Run("Orbs: glass captures passive before decay and evokes twice remaining value", () =>
        {
            var session = Start("channelOrb = { kind = Glass }", 3);
            Play(session);
            var enemies = Enemies(session);
            var before = enemies.Select(enemy => Hp(session, enemy)).ToArray();
            FormalSliceFixture.EndTurn(session); Healthy(session);
            for (var index = 0; index < enemies.Count; index++)
                Check.Equal(before[index] - 4, Hp(session, enemies[index]), "unpowered passive hits each enemy for pre-decay four");
            Check.Equal(3L, OrbNumber(session, Queue(session).Single()), "underlying value decays once");
            before = enemies.Select(enemy => Hp(session, enemy)).ToArray();
            Play(session, "mymod:evoke-probe");
            for (var index = 0; index < enemies.Count; index++)
                Check.Equal(before[index] - 6, Hp(session, enemies[index]), "evoke uses twice the decayed three");
        });
        foreach (var focus in new[] { -10L, 2L })
            Check.Run($"Orbs: Focus/{focus} clamps passive growth but does not modify stored Dark evoke", () =>
            {
                var session = Start("channelOrb = { kind = Dark }", 3);
                FormalSliceFixture.GivePower(session, FormalSliceFixture.Player(session), "mymod:focus", focus);
                Play(session);
                FormalSliceFixture.EndTurn(session); Healthy(session);
                Check.Equal(focus < 0 ? 6L : 14L, OrbNumber(session, Queue(session).Single()), "Dark stores focus-modified passive growth");
                var enemies = Enemies(session);
                FormalSliceFixture.SetNumber(session, enemies[0], FormalSliceFixture.MonsterTypeId, "Hp", 40);
                FormalSliceFixture.SetNumber(session, enemies[1], FormalSliceFixture.MonsterTypeId, "Hp", 30);
                Play(session, "mymod:evoke-probe");
                Check.Equal(40L, Hp(session, enemies[0]), "Dark ignores stronger enemy");
                Check.Equal(focus < 0 ? 24L : 16L, Hp(session, enemies[1]), "stored evoke is not focus-modified again");
            });
        foreach (var kind in new[] { "Lightning", "Dark", "Glass", "Frost", "Plasma" })
            Check.Run($"Orbs: {kind} actual DSL channel and evoke vector", () =>
            {
                var session = Start($"channelOrb = {{ kind = {kind} }} evokeOrb = {{}}", 3);
                var enemies = Enemies(session);
                var before = enemies.Sum(enemy => Hp(session, enemy));
                Play(session);
                var damage = before - enemies.Sum(enemy => Hp(session, enemy));
                Check.Equal(kind switch { "Lightning" => 8L, "Dark" => 6L, "Glass" => 8L * enemies.Count, _ => 0L }, damage, "source evoke damage");
                Check.Equal(kind == "Frost" ? 5L : 0L, Number(session, "Block"), "source evoke block");
                Check.Equal(kind == "Plasma" ? 5L : 3L, Number(session, "Energy"), "source evoke energy");
                Check.Equal(0, Queue(session).Count, "evoked orb removed");
            });
        Check.Run("Orbs: malformed author effects fail loading", () =>
        {
            foreach (var effect in new[] { "channelOrb = { kind = Unknown }", "evokeOrb = { dequeue = maybe }", "addOrbSlots = { amount = 1 amount = 2 }" })
                Check.True(SpireCardSource.Parse(Source(effect), "orb-invalid.spire").Unsupported.Count > 0, "invalid orb contract rejected");
        });
    }

    private static string Source(string effects) => $$"""
        using namespace mymod;
        dsl_version = "base/0.0.2";
        card OrbProbe {
          common = { type = skill rarity = common cost = 0 target = { selectSelf = {} } keywords = {} play = { {{effects}} } }
          normal = {}
          upgraded = {}
        }
        card EvokeProbe {
          common = { type = skill rarity = common cost = 0 target = { selectSelf = {} } keywords = {} play = { evokeOrb = {} } }
          normal = {}
          upgraded = {}
        }
        """;

    private static Session Start(string effects, int baseSlots)
    {
        var source = SpireCardSource.Parse(Source(effects), "orb-probe.spire");
        Check.Equal(0, source.Unsupported.Count, string.Join(";", source.Unsupported.Select(item => item.Reason)));
        var model = FormalSpirePackage.Load().Model;
        var focus = model.Powers.First(power => power.Role == "Strength") with { Id = "mymod:focus", Name = "Focus", Role = "Focus" };
        model = model with { BaseOrbSlots = baseSlots, Powers = model.Powers.Append(focus).ToArray(), Cards = model.Cards.Concat(source.Cards).ToArray(), Deck = [new("mymod:orb-probe", 1), new("mymod:evoke-probe", 1), new("base-spire:wound", 14)] };
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var session = new Session(builder.Build(), 20261012UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start(); Healthy(session); return session;
    }
    private static void Play(Session session, string cardId = "mymod:orb-probe")
    {
        var card = FormalSliceFixture.MoveToHand(session, cardId);
        var action = session.LegalActions(SliceManifest.Player).Single(item => item.Definition.QualifiedId == FormalSliceFixture.PlayCardAction);
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]); Healthy(session);
    }
    private static IReadOnlyList<InstanceId> Queue(Session session) => session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:OrbQueue"));
    private static IReadOnlyList<InstanceId> Enemies(Session session) => session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:Monsters"));
    private static string Kind(Session session, InstanceId orb) => session.State.GetField(orb, session.RuleSet.TypeByQualifiedId("base-spire:Orb").FieldIndex("Kind")).AsString();
    private static long OrbNumber(Session session, InstanceId orb) => FormalSliceFixture.Number(session, orb, "base-spire:Orb", "StoredValue");
    private static long Hp(Session session, InstanceId enemy) => FormalSliceFixture.Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp");
    private static long Number(Session session, string field) => FormalSliceFixture.Number(session, FormalSliceFixture.Player(session), FormalSliceFixture.PlayerTypeId, field);
    private static void Healthy(Session session) => Check.True(session.Status != SessionStatus.Faulted, $"{session.Fault}");
}
