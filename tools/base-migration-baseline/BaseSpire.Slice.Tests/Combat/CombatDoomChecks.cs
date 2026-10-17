using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class CombatDoomChecks
{
    public static void Run()
    {
        foreach (var stacks in new[] { 0L, 9L, 10L })
            Check.Run($"Doom: explicit resolution threshold {stacks} ignores block", () =>
            {
                var session = Start();
                var enemies = Enemies(session).ToArray();
                Set(session, enemies[0], FormalSliceFixture.MonsterTypeId, "Hp", 10);
                Set(session, enemies[0], FormalSliceFixture.MonsterTypeId, "Block", 99);
                FormalSliceFixture.GivePower(session, enemies[0], "mymod:doom", stacks);
                Play(session);
                Check.True(Enemies(session).Contains(enemies[0]) == (stacks < 10), "threshold membership");
                Check.Equal(stacks < 10 ? 10L : 0L, FormalSliceFixture.Number(session, enemies[0], FormalSliceFixture.MonsterTypeId, "Hp"), "not ordinary damage");
                Check.Equal(0, session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:DoomBatch")).Count, "batch cleaned");
            });
        Check.Run("Doom: explicit batch kills all captured enemies and settles victory", () =>
        {
            var session = Start();
            foreach (var enemy in Enemies(session).ToArray())
            {
                Set(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 10);
                FormalSliceFixture.GivePower(session, enemy, "mymod:doom", 10);
            }
            Play(session);
            Check.Equal(0, Enemies(session).Count, "all batch victims removed");
            Check.True(session.State.GetField(FormalSliceFixture.Player(session), session.RuleSet.TypeByQualifiedId(FormalSliceFixture.PlayerTypeId).FieldIndex("BattleSettled")).AsBool(), "battle victory committed");
        });
        Check.Run("Doom: enemy side resolves before owner turn end", () =>
        {
            var session = Start();
            var victim = Enemies(session)[0];
            Set(session, victim, FormalSliceFixture.MonsterTypeId, "Hp", 10);
            FormalSliceFixture.GivePower(session, victim, "mymod:doom", 10);
            FormalSliceFixture.EndTurn(session);
            Healthy(session);
            Check.True(!Enemies(session).Contains(victim), "enemy Doom dispatched");
        });
        Check.Run("Doom: player dies before turn end orb and enemy actions", () =>
        {
            var session = Start();
            var player = FormalSliceFixture.Player(session);
            Set(session, player, FormalSliceFixture.PlayerTypeId, "Hp", 10);
            FormalSliceFixture.GivePower(session, player, "mymod:doom", 10);
            FormalSliceFixture.EndTurn(session);
            Healthy(session);
            Check.Equal(0L, FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp"), "Doom death");
            Check.Equal(SessionStatus.Terminal, session.Status, "defeat settled");
            Check.Equal("base-spire:Defeat", session.Terminal!.Definition.QualifiedId, "Doom defeat event");
            Check.Equal(1L, FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Turn"), "next turn not started");
        });
        Check.Run("Doom: nonempty resolution blocks rejected", () =>
            Check.True(SpireCardSource.Parse(Source("resolveDoom = { amount = 1 }"), "doom-invalid.spire").Unsupported.Count > 0, "unsupported arguments"));
    }

    private static string Source(string effect) => $$"""
        using namespace mymod;
        dsl_version = "base/0.0.2";
        card DoomProbe {
          common = { type = skill rarity = common cost = 0 target = { selectSelf = {} } keywords = {} play = { {{effect}} } }
          normal = {}
          upgraded = {}
        }
        """;

    private static Session Start()
    {
        var cards = SpireCardSource.Parse(Source("resolveDoom = {}"), "doom-probe.spire");
        Check.Equal(0, cards.Unsupported.Count, "Doom author loading");
        var powers = SpirePowerSource.Parse("package mymod { power Doom { role = Doom } }", "doom-power.spire");
        Check.Equal(0, powers.Unsupported.Count, "Doom Power loading");
        var model = FormalSpirePackage.Load().Model;
        model = model with { Powers = model.Powers.Concat(powers.Powers).ToArray(), Cards = model.Cards.Concat(cards.Cards).ToArray(), Deck = [new("mymod:doom-probe", 1), new("base-spire:wound", 14)] };
        var builder = new RuleSetBuilder();
        builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var session = new Session(builder.Build(), 20261012UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start(); Healthy(session); return session;
    }
    private static void Play(Session session)
    {
        var card = FormalSliceFixture.MoveToHand(session, "mymod:doom-probe");
        FormalSliceFixture.PlayAt(session, card, FormalSliceFixture.Player(session));
        Healthy(session);
    }
    private static IReadOnlyList<InstanceId> Enemies(Session session) => session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:Monsters"));
    private static void Set(Session session, InstanceId entity, string type, string field, long value) => FormalSliceFixture.SetNumber(session, entity, type, field, value);
    private static void Healthy(Session session) => Check.True(session.Status != SessionStatus.Faulted, $"{session.Fault}");
}
