using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class CombatForgeChecks
{
    public static void Run()
    {
        foreach (var upgraded in new[] { false, true })
            Check.Run($"Forge: RefineBlade/{upgraded} creates once, grows exhausted instances and preserves identity", () =>
            {
                var session = Start(upgraded);
                Play(session);
                var blade = Blades(session).Single();
                Check.Equal(upgraded ? 23L : 19L, Damage(session, blade), "base ten plus source forge nine/thirteen");
                Play(session);
                Check.Equal(blade, Blades(session).Single(), "existing blade reused");
                Check.Equal(upgraded ? 36L : 28L, Damage(session, blade), "second forge additive");
                session.State.RunAtomic(() =>
                {
                    session.State.RemoveFromCollection(FormalSliceFixture.Collection(session, FormalSliceFixture.HandPile), blade);
                    session.State.AddToCollection(FormalSliceFixture.Collection(session, FormalSliceFixture.ExhaustPile), blade);
                });
                Play(session);
                Check.Equal(2, Blades(session).Count, "only exhausted blade does not prevent creation");
                Check.Equal(upgraded ? 49L : 37L, Damage(session, blade), "exhausted blade still grows");
                Check.Equal(upgraded ? 23L : 19L, Damage(session, Blades(session).Single(item => item != blade)), "new blade starts independently");
            });
    }

    private static Session Start(bool upgraded)
    {
        var source = SpireCardSource.Parse("""
            using namespace mymod;
            dsl_version = "base/0.0.2";
            card Blade {
              variables = { variable = { name = Damage default = 10 upgraded = 10 } }
              damageVariable = Damage
              common = { type = attack rarity = token target = { selectOne = { from = context.opponents where = alive } } keywords = { retain } play = { attackVariable = { variable = Damage } } }
              normal = { cost = 2 }
              upgraded = { cost = 1 }
            }
            card RefineBlade {
              values = { forgeAmount = { normal = 9 upgraded = 13 } }
              common = { type = skill rarity = common cost = 1 target = { selectSelf = {} } keywords = {} play = { forge = { card = Blade variable = Damage amount = values.forgeAmount } } }
              normal = {}
              upgraded = {}
            }
            """, "forge-source.spire");
        Check.Equal(0, source.Unsupported.Count, string.Join(";", source.Unsupported.Select(item => item.Reason)));
        var model = FormalSpirePackage.Load().Model;
        model = model with { Cards = model.Cards.Concat(source.Cards).ToArray(), Deck = [new("mymod:refine-blade", 1, upgraded), new("base-spire:wound", 14)] };
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var session = new Session(builder.Build(), 20261012UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start(); Healthy(session); return session;
    }
    private static void Play(Session session)
    {
        FormalSliceFixture.SetNumber(session, FormalSliceFixture.Player(session), FormalSliceFixture.PlayerTypeId, "Energy", 3);
        var card = FormalSliceFixture.MoveToHand(session, "mymod:refine-blade");
        var action = session.LegalActions(SliceManifest.Player).Single(item => item.Definition.QualifiedId == FormalSliceFixture.PlayCardAction);
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]); Healthy(session);
    }
    private static IReadOnlyList<InstanceId> Blades(Session session) => session.State.AllInstances(session.RuleSet.TypeByQualifiedId(FormalSliceFixture.CardTypeId).Id)
        .Where(card => FormalSliceFixture.Definition(session, card) == "mymod:blade").ToArray();
    private static long Damage(Session session, InstanceId card) => FormalSliceFixture.Number(session, card, FormalSliceFixture.CardTypeId, "Variable_damage");
    private static void Healthy(Session session) => Check.True(session.Status != SessionStatus.Faulted, $"{session.Fault}");
}
