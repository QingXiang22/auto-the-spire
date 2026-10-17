using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class CombatPoisonChecks
{
    public static void Run()
    {
        Check.Run("Poison: actual DSL application and hp loss cap consume independently", () =>
        {
            var session = Start();
            var enemy = Enemies(session)[0];
            Set(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 50);
            var card = FormalSliceFixture.MoveToHand(session, "mymod:poison-probe");
            FormalSliceFixture.PlayAt(session, card, enemy); Healthy(session);
            Check.Equal(5L, FormalSliceFixture.PowerStacks(session, enemy, "mymod:poison"), "applied by compiled card");
            FormalSliceFixture.GivePower(session, enemy, "mymod:cap", 1);
            FormalSliceFixture.EndTurn(session); Healthy(session);
            Check.Equal(48L, Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "unblockable damage still capped");
            Check.Equal(0L, FormalSliceFixture.PowerStacks(session, enemy, "mymod:cap"), "cap consumed once");
            Check.Equal(4L, FormalSliceFixture.PowerStacks(session, enemy, "mymod:poison"), "Poison decrements independently");
        });
        Check.Run("Poison: dead opposing Accelerant is excluded", () =>
        {
            var session = Start();
            var player = FormalSliceFixture.Player(session);
            var enemies = Enemies(session).ToArray();
            foreach (var enemy in enemies) Set(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 50);
            Set(session, enemies[0], FormalSliceFixture.MonsterTypeId, "Hp", 0);
            FormalSliceFixture.GivePower(session, enemies[0], "mymod:accelerant", 20);
            FormalSliceFixture.GivePower(session, player, "mymod:poison", 5);
            Set(session, player, FormalSliceFixture.PlayerTypeId, "Block", 99);
            var before = Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
            FormalSliceFixture.EndTurn(session); Healthy(session);
            Check.Equal(before - 5L, Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp"), "only living opponents contribute");
        });
        foreach (var extra in new[] { 0L, 2L, 20L })
            Check.Run($"Poison: enemy source vector with Accelerant {extra}", () =>
            {
                var session = Start();
                var enemy = Enemies(session)[0];
                Set(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 50);
                Set(session, enemy, FormalSliceFixture.MonsterTypeId, "Block", 99);
                FormalSliceFixture.GivePower(session, enemy, "mymod:poison", 5);
                FormalSliceFixture.GivePower(session, enemy, SliceManifest.StrengthId, 30);
                FormalSliceFixture.GivePower(session, FormalSliceFixture.Player(session), "mymod:accelerant", extra);
                FormalSliceFixture.EndTurn(session); Healthy(session);
                Check.Equal(extra == 0 ? 45L : extra == 2 ? 38L : 35L, Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "5 then 4 then 3, at most five hits");
                Check.Equal(extra == 0 ? 4L : extra == 2 ? 2L : 0L, FormalSliceFixture.PowerStacks(session, enemy, "mymod:poison"), "decrement after each surviving hit");
                if (extra == 20) Check.True(!FormalSliceFixture.PowerIds(session, enemy).Any(id => id.StartsWith("mymod:poison:", StringComparison.Ordinal)), "exhausted power destroyed");
            });
        Check.Run("Poison: lethal enemy damage removes victim before action and settles", () =>
        {
            var session = Start();
            var victims = Enemies(session).ToArray();
            foreach (var victim in victims)
            {
                Set(session, victim, FormalSliceFixture.MonsterTypeId, "Hp", 3);
                FormalSliceFixture.GivePower(session, victim, "mymod:poison", 5);
            }
            var player = FormalSliceFixture.Player(session);
            FormalSliceFixture.GivePower(session, player, "mymod:poison", 5);
            var before = Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
            FormalSliceFixture.EndTurn(session); Healthy(session);
            Check.Equal(0, Enemies(session).Count, "dead units removed");
            Check.Equal(before, Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp"), "no enemy attacks");
            Check.Equal(5L, FormalSliceFixture.PowerStacks(session, player, "mymod:poison"), "no player poison after enemy side cleared");
            Check.True(session.State.GetField(player, session.RuleSet.TypeByQualifiedId(FormalSliceFixture.PlayerTypeId).FieldIndex("BattleSettled")).AsBool(), "victory committed");
        });
        Check.Run("Poison: player uses living opposing Accelerant sum and counts damage hits", () =>
        {
            var session = Start();
            var player = FormalSliceFixture.Player(session);
            var enemies = Enemies(session).ToArray();
            Check.True(enemies.Length >= 2, "opponent sum fixture");
            Set(session, player, FormalSliceFixture.PlayerTypeId, "Block", 99);
            FormalSliceFixture.GivePower(session, player, "mymod:poison", 5);
            foreach (var enemy in enemies) FormalSliceFixture.GivePower(session, enemy, "mymod:accelerant", 1);
            var before = Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
            FormalSliceFixture.EndTurn(session); Healthy(session);
            Check.Equal(before - 12L, Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp"), "two opponents give three poison hits");
            Check.Equal(2L, FormalSliceFixture.PowerStacks(session, player, "mymod:poison"), "player decrement");
            Check.Equal(3L, Number(session, player, FormalSliceFixture.PlayerTypeId, "HpLossCount"), "one hp loss event per poison hit");
        });
        Check.Run("Poison: lethal player damage removes dead-owner powers before terminal", () =>
        {
            var session = Start();
            var player = FormalSliceFixture.Player(session);
            Set(session, player, FormalSliceFixture.PlayerTypeId, "Hp", 3);
            Set(session, player, FormalSliceFixture.PlayerTypeId, "Block", 99);
            FormalSliceFixture.GivePower(session, player, "mymod:poison", 5);
            FormalSliceFixture.EndTurn(session); Healthy(session);
            Check.Equal(SessionStatus.Terminal, session.Status, "player poison death");
            Check.Equal("base-spire:Defeat", session.Terminal!.Definition.QualifiedId, "defeat not victory");
            Check.Equal(0L, FormalSliceFixture.PowerStacks(session, player, "mymod:poison"), "confirmed death removes the power, not a surviving-owner decrement");
            Check.Equal(0, FormalSliceFixture.PowerIds(session, player).Count, "dead-owner power inventory is empty");
        });
    }

    private static Session Start()
    {
        var source = SpirePowerSource.Parse("package mymod { power Poison { role = Poison } power Accelerant { role = Accelerant } power Cap { rules = { hpLossCap = 2 } } }", "poison-probe.spire");
        Check.Equal(0, source.Unsupported.Count, "actual Power DSL loaded");
        var model = FormalSpirePackage.Load().Model;
        var cards = SpireCardSource.Parse("""
            using namespace mymod;
            dsl_version = "base/0.0.2";
            card PoisonProbe {
              common = { type = skill rarity = common cost = 0 target = { selectOne = { from = context.opponents where = alive } } keywords = {} play = { applyPower = { power = mymod:poison stacks = 5 target = context.target } } }
              normal = {}
              upgraded = {}
            }
        """, "poison-card-probe.spire", new Dictionary<string, string> { ["mymod:poison"] = "mymod:poison", ["Poison"] = "mymod:poison" });
        Check.Equal(0, cards.Unsupported.Count, string.Join(";", cards.Unsupported.Select(item => item.Reason)));
        model = model with { Powers = model.Powers.Concat(source.Powers).ToArray(), Cards = model.Cards.Concat(cards.Cards).ToArray(), Deck = [new("mymod:poison-probe", 1), new("base-spire:wound", 14)] };
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var session = new Session(builder.Build(), 20261012UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start(); Healthy(session); return session;
    }
    private static IReadOnlyList<InstanceId> Enemies(Session session) => session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:Monsters"));
    private static long Number(Session session, InstanceId entity, string type, string field) => FormalSliceFixture.Number(session, entity, type, field);
    private static void Set(Session session, InstanceId entity, string type, string field, long value) => FormalSliceFixture.SetNumber(session, entity, type, field, value);
    private static void Healthy(Session session) => Check.True(session.Status != SessionStatus.Faulted, $"{session.Fault}");
}
