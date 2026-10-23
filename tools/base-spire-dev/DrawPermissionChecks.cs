using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Copy DrawPermissionCopy(DslPackage package)
    {
        var copy = new Copy(package.Root);
        copy.Include("draw-permission-check.spire", """
            action BlockEffectDraw { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:no-draw-power" stacks = 1 } } } }
            action UnblockEffectDraw { role = player body = { forEach = { query = where(collection(Powers), p, equal(field(p, Power.DefinitionId), "base-spire:no-draw-power")) item = power body = { call = { procedure = RemovePower args = { power = power } } } } } }
            action DrawTurnCards { role = player body = { call = { procedure = DrawCardsWithSource args = { owner = first(type(Player)) count = 2 fromHandDraw = true } } } }
            action DrawEffectCards { role = player body = { call = { procedure = DrawCards args = { owner = first(type(Player)) count = 3 } } } }
            """);
        return copy;
    }

    private static void RunDrawPermissionChecks(DslPackage package, Action<string, Action> check)
    {
        check("DRAW-PERMISSION native NoDraw blocks effects without RNG, allows turn hand, publishes removal and expires", () =>
        {
            using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.draw-permission-vector.json")!);
            using var copy = DrawPermissionCopy(package);
            var s = Start(copy.Load()); Submit(s, "Ironclad.Initialize"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var steps = data.RootElement.GetProperty("steps").EnumerateArray().ToArray(); var index = 0;
            void State()
            {
                var expected = steps[index++];
                Equal(expected.GetProperty("hand").GetInt32(), Hand(s).Count, "native actual hand count");
                Equal(expected.GetProperty("blocked").GetBoolean(), s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("DrawBlocked")).AsBool(), "native effect draw permission");
            }
            State(); Submit(s, "BlockEffectDraw"); State();
            var before = JsonSerializer.Serialize(s.Random.Capture()); var piles = Members(s, "DrawPile").ToArray();
            Submit(s, "DrawEffectCards"); State();
            Equal(before, JsonSerializer.Serialize(s.Random.Capture()), "veto consumes no random state");
            True(piles.SequenceEqual(Members(s, "DrawPile")), "veto leaves draw order intact");
            Submit(s, "DrawTurnCards"); State(); Submit(s, "UnblockEffectDraw"); State(); Submit(s, "Draw"); State();
            Submit(s, "BlockEffectDraw"); Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); State();
            Equal(steps.Length, index, "all native steps consumed");
            Submit(s, "BlockEffectDraw"); Submit(s, "EndDevelopmentCombat");
            True(!s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("DrawBlocked")).AsBool(), "combat cleanup clears published veto");
        });
    }
}
