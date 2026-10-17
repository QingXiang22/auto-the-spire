using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class CombatStarsChecks
{
    public static void Run()
    {
        foreach (var upgraded in new[] { false, true })
            Check.Run($"Stars: GatherLight/{upgraded} source block and gain persist across turns", () =>
            {
                var session = Start("gainStars = { amount = 1 }", upgraded);
                Play(session);
                Check.Equal(upgraded ? 11L : 8L, Number(session, "Block"), "source GatherLight block");
                Check.Equal(1L, Number(session, "Stars"), "source one star");
                var end = session.LegalActions(SliceManifest.Player).Single(action => action.Definition.QualifiedId == FormalSliceFixture.EndTurnAction);
                session.Submit(end.Definition, SliceManifest.Player, []);
                Healthy(session);
                Check.Equal(1L, Number(session, "Stars"), "stars do not reset on player turn");
            });
        Check.Run("Stars: composed gain, set and overdraw preserve order and floor at zero", () =>
        {
            var session = Start("gainStars = { amount = 3 } setStars = { amount = 5 } loseStars = { amount = 9 } gainStars = { amount = 2 }", false);
            Play(session);
            Check.Equal(2L, Number(session, "Stars"), "independent ordered resource vector");
            Check.Equal(2L, Number(session, "Energy"), "stars do not replace energy payment");
        });
        Check.Run("Stars: unsupported target and duplicate amount fail author loading", () =>
        {
            foreach (var effect in new[] { "gainStars = { amount = 1 target = context.target }", "gainStars = { amount = 1 amount = 2 }" })
                Check.True(SpireCardSource.Parse(Source(effect), "stars-invalid.spire").Unsupported.Count > 0, "invalid owner resource contract");
        });
        foreach (var upgraded in new[] { false, true })
            Check.Run($"Stars: fixed cost/{upgraded} guards shortage and charges both resources once", () =>
            {
                var session = Start("gainStars = { amount = 1 }", upgraded, starCost: 2);
                var card = FormalSliceFixture.MoveToHand(session, "mymod:gather-light");
                var action = session.LegalActions(SliceManifest.Player).SingleOrDefault(item => item.Definition.QualifiedId == FormalSliceFixture.PlayCardAction);
                Check.True(action is null || !action.Candidates.Contains(card), "cannot play with insufficient stars");
                FormalSliceFixture.SetNumber(session, FormalSliceFixture.Player(session), FormalSliceFixture.PlayerTypeId, "Stars", 3);
                Play(session);
                Check.Equal(2L, Number(session, "Stars"), "pay two before gain one");
                Check.Equal(2L, Number(session, "Energy"), "energy paid independently once");
            });
        Check.Run("Stars: X payment captured before effects and independent of energy X", () =>
        {
            var session = Start("gainStars = { amount = 2 } gainStars = { amount = PaidStarX }", false, starX: true);
            FormalSliceFixture.SetNumber(session, FormalSliceFixture.Player(session), FormalSliceFixture.PlayerTypeId, "Stars", 4);
            Play(session);
            Check.Equal(6L, Number(session, "Stars"), "spent four before effects; later gain uses original four");
            Check.Equal(2L, Number(session, "Energy"), "independent fixed energy cost");
            var zero = Start("gainStars = { amount = PaidStarX }", true, starX: true);
            Play(zero);
            Check.Equal(0L, Number(zero, "Stars"), "zero-star X remains playable");
        });
        Check.Run("Stars: payment snapshot rejected without star X context", () =>
        {
            Check.True(SpireCardSource.Parse(Source("gainStars = { amount = PaidStarX }"), "stars-no-x.spire").Unsupported.Count > 0,
                "non-X star reference cannot load");
        });
    }

    private static string Source(string effects, int starCost = 0, bool starX = false) => $$"""
        using namespace mymod;
        dsl_version = "base/0.0.2";
        card GatherLight {
          common = { type = skill rarity = common cost = 1 starCost = {{(starX ? "X" : starCost.ToString(System.Globalization.CultureInfo.InvariantCulture))}} target = { selectSelf = {} } keywords = {} }
          normal = { play = { gainBlock = { amount = 8 } {{effects}} } }
          upgraded = { play = { gainBlock = { amount = 11 } {{effects}} } }
        }
        """;

    private static Session Start(string effects, bool upgraded, int starCost = 0, bool starX = false)
    {
        var source = SpireCardSource.Parse(Source(effects, starCost, starX), "stars-gather-light.spire");
        Check.Equal(0, source.Unsupported.Count, string.Join(";", source.Unsupported.Select(item => item.Reason)));
        var model = FormalSpirePackage.Load().Model;
        model = model with { Cards = model.Cards.Concat(source.Cards).ToArray(), Deck = [new("mymod:gather-light", 1, upgraded), new("base-spire:wound", 14)] };
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var session = new Session(builder.Build(), 20261012UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        session.Start(); Healthy(session); return session;
    }
    private static void Play(Session session)
    {
        var card = FormalSliceFixture.MoveToHand(session, "mymod:gather-light");
        var action = session.LegalActions(SliceManifest.Player).Single(item => item.Definition.QualifiedId == FormalSliceFixture.PlayCardAction);
        session.Submit(action.Definition, SliceManifest.Player, [Value.OfRef(card)]); Healthy(session);
    }
    private static long Number(Session session, string field) => FormalSliceFixture.Number(session, FormalSliceFixture.Player(session), FormalSliceFixture.PlayerTypeId, field);
    private static void Healthy(Session session) => Check.True(session.Status != SessionStatus.Faulted, $"{session.Fault}");
}
