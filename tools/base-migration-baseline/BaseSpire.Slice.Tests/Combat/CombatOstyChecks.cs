using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class CombatOstyChecks
{
    public static void Run()
    {
        Check.Run("Osty: enemy powered attacks redirect after block and dead pet stops protecting", () =>
        {
            var session = Start(false);
            var monsters = FormalSliceFixture.Collection(session, "base-spire:Monsters");
            var remaining = session.State.CollectionMembers(monsters).Skip(1).ToArray();
            session.State.RunAtomic(() => { foreach (var enemy in remaining) session.State.RemoveFromCollection(monsters, enemy); });
            FormalSliceFixture.EndTurn(session);
            Play(session, "summon");
            var player = FormalSliceFixture.Player(session);
            var before = FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
            FormalSliceFixture.SetNumber(session, player, FormalSliceFixture.PlayerTypeId, "Block", 2);
            FormalSliceFixture.EndTurn(session); Healthy(session);
            Check.Equal(0L, Number(session, Pets(session).Single(), "Hp"), "Osty receives unblocked lethal hit");
            Check.Equal(before, FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp"), "lethal pet hit does not spill into player");
            Check.Equal(0L, FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "HpLossCount"), "pet damage is not player hp loss");
            var after = FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp");
            FormalSliceFixture.EndTurn(session); Healthy(session);
            Check.True(FormalSliceFixture.Number(session, player, FormalSliceFixture.PlayerTypeId, "Hp") < after, "dead pet no longer protects");
        });
        foreach (var upgraded in new[] { false, true })
            Check.Run($"Osty: actual DSL summon/{upgraded}, growth, death and same-instance revival", () =>
            {
                var session = Start(upgraded);
                Play(session, "summon");
                var pet = Pets(session).Single();
                Check.Equal(upgraded ? 9L : 6L, Number(session, pet, "Hp"), "summon vector");
                Play(session, "loss");
                Play(session, "summon");
                Check.Equal(pet, Pets(session).Single(), "growth retains identity");
                Check.Equal(upgraded ? 18L : 12L, Number(session, pet, "MaxHp"), "growth maximum");
                Check.Equal(upgraded ? 15L : 9L, Number(session, pet, "Hp"), "growth heals only gained amount");
                Play(session, "heal");
                Check.Equal(Number(session, pet, "MaxHp"), Number(session, pet, "Hp"), "living heal caps at maximum");
                Play(session, "kill");
                Check.Equal(0L, Number(session, pet, "Hp"), "dead identity retained");
                Play(session, "heal");
                Check.Equal(0L, Number(session, pet, "Hp"), "healing dead pet does not revive");
                Play(session, "summon");
                Check.Equal(pet, Pets(session).Single(), "revival same instance");
                Check.Equal(upgraded ? 9L : 6L, Number(session, pet, "MaxHp"), "revival resets maximum");
                Check.Equal(upgraded ? 9L : 6L, Number(session, pet, "Hp"), "revival restores current life");
            });
        Check.Run("Osty: zero summon, empty heal and loss do not create pets", () =>
        {
            var session = Start(false);
            Play(session, "zero"); Play(session, "heal"); Play(session, "loss");
            Check.Equal(0, Pets(session).Count, "no implicit summon");
        });
        Check.Run("Osty: only living pet attacks, without player Strength", () =>
        {
            var session = Start(false);
            var enemy = session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:Monsters"))[0];
            FormalSliceFixture.SetNumber(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp", 50);
            FormalSliceFixture.GivePower(session, FormalSliceFixture.Player(session), SliceManifest.StrengthId, 20);
            Play(session, "hit", enemy);
            Check.Equal(50L, FormalSliceFixture.Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "no pet no damage");
            Play(session, "summon"); Play(session, "hit", enemy);
            Check.Equal(43L, FormalSliceFixture.Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "pet attack ignores player strength");
            Play(session, "kill"); Play(session, "hit", enemy);
            Check.Equal(43L, FormalSliceFixture.Number(session, enemy, FormalSliceFixture.MonsterTypeId, "Hp"), "dead pet no damage");
        });
        Check.Run("Osty: malformed fields and duplicate amounts explicitly rejected", () =>
        {
            foreach (var body in new[] { "summonOsty = {}", "summonOsty = { amount = 2 amount = 3 }", "healOsty = { amount = 2 target = self }" })
                Check.True(SpireCardSource.Parse(Source(body), "osty-invalid.spire").Unsupported.Count > 0, "invalid contract rejected");
        });
    }

    private static string Source(string body) => $$"""
        using namespace mymod;
        dsl_version = "base/0.0.2";
        card Probe {
          common = { type = skill rarity = common cost = 0 target = { selectSelf = {} } keywords = {} play = { {{body}} } }
          normal = {}
          upgraded = {}
        }
        """;
    private static Session Start(bool upgraded)
    {
        var text = "using namespace mymod; dsl_version = \"base/0.0.2\";";
        foreach (var (name, effect, selected) in new[] {
            ("Summon", "summonOsty = { amount = values.amount }", false), ("Loss", "loseOstyHp = { amount = 3 }", false),
            ("Kill", "loseOstyHp = { amount = 99 }", false), ("Heal", "healOsty = { amount = 99 }", false),
            ("Zero", "summonOsty = { amount = 0 }", false), ("Hit", "ostyAttack = { amount = 7 }", true) })
        {
            var target = selected ? "selectOne = { from = context.opponents where = alive }" : "selectSelf = {}";
            text += $"card {name} {{ values = {{ amount = {{ normal = 6 upgraded = 9 }} }} common = {{ type = skill rarity = common cost = 0 target = {{ {target} }} keywords = {{}} play = {{ {effect} }} }} normal = {{}} upgraded = {{}} }}";
        }
        var source = SpireCardSource.Parse(text, "osty-probe.spire");
        Check.Equal(0, source.Unsupported.Count, string.Join(";", source.Unsupported.Select(item => item.Reason)));
        var model = FormalSpirePackage.Load().Model;
        model = model with { Cards = model.Cards.Concat(source.Cards).ToArray(), Deck = source.Cards.Select(card => new AuthorDeckEntry(card.Id, 1, upgraded)).ToArray() };
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var session = new Session(builder.Build(), 20261012UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start(); Healthy(session); return session;
    }
    private static void Play(Session session, string card, InstanceId? target = null)
    {
        FormalSliceFixture.PlayAt(session, FormalSliceFixture.MoveToHand(session, "mymod:" + card), target ?? FormalSliceFixture.Player(session)); Healthy(session);
    }
    private static IReadOnlyList<InstanceId> Pets(Session session) => session.State.CollectionMembers(FormalSliceFixture.Collection(session, "base-spire:OstyPets"));
    private static long Number(Session session, InstanceId pet, string field) => FormalSliceFixture.Number(session, pet, "base-spire:Osty", field);
    private static void Healthy(Session session) => Check.True(session.Status != SessionStatus.Faulted, $"{session.Fault}");
}
