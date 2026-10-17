using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>Source pool eligibility, independent of executable event modules.
/// Current dev-map scope is Overgrowth/Act 1 only; later-act support must supply its actual act index.
/// Source: sts2/src/Core/Models/Events/* IsAllowed. Unimplemented predicates fail rather than allow an event.
/// </summary>
public static class SourceEventEligibility
{
    public static Inst[] Evaluate(IrScope scope, EventCompileContext context, Expr run,
        string sourceName, int result)
    {
        var room = context.Room;
        Expr? allowed = sourceName switch
        {
            // These models inherit EventModel.IsAllowed (true), or their override is true for Act 1 singleplayer.
            "AromaOfChaos" or "DenseVegetation" or "JungleMazeAdventure" or "SapphireSeed"
                or "SunkenStatue" or "TabletOfTruth" or "Wellspring" or "SelfHelpBook"
                or "ThisOrThat" or "BrainLeech" or "RoomFullOfCheese" => scope.Const(true),
            // Source act restrictions reject these before any inventory condition in Act 1.
            "CrystalSphere" or "DollRoom" or "FakeMerchant" or "PotionCourier"
                or "RanwidTheElder" or "RelicTrader" or "StoneOfAllTime" or "Symbiote"
                or "WelcomeToWongos" or "WarHistorianRepy" => scope.Const(false),
            "UnrestSite" => scope.LessOrEqual(
                scope.Multiply(scope.Field(run, room.Hp), scope.Const(10L)),
                scope.Multiply(scope.Field(run, room.MaxHp), scope.Const(7L))),
            "WhisperingHollow" => scope.GreaterOrEqual(scope.Field(run, room.Gold), scope.Const(44L)),
            "TeaMaster" => scope.GreaterOrEqual(scope.Field(run, room.Gold), scope.Const(150L)),
            "TheLegendsWereTrue" => scope.And(
                scope.Greater(scope.Count(scope.Collection(room.LongTermDeck)), scope.Const(0L)),
                scope.GreaterOrEqual(scope.Field(run, room.Hp), scope.Const(10L))),
            "TheFutureOfPotions" => HasTwoPotions(scope),
            "MorphicGrove" when context.Deck is { } morphicDeck => scope.And(
                scope.GreaterOrEqual(scope.Field(run, room.Gold), scope.Const(100L)),
                scope.GreaterOrEqual(scope.Count(scope.Where(scope.Collection(morphicDeck.Cards),
                    card => morphicDeck.CanTransform(scope, card))), scope.Const(2L))),
            "WoodCarvings" when context.Deck is { } permanentDeck => scope.Exists(scope.Where(
                scope.Collection(permanentDeck.Cards), card => scope.And(
                    permanentDeck.HasRarity(scope, card, "basic"), permanentDeck.CanRemove(scope, card)))),
            "SlipperyBridge" when context.Deck is { } deck => scope.And(
                scope.Greater(scope.Count(scope.Where(scope.Collection(room.Map.Nodes), node => scope.Field(node, room.Map.NodeVisited))), scope.Const(6L)),
                scope.Exists(scope.Where(scope.Collection(deck.Cards), card => deck.CanRemove(scope, card)))),
            _ => null,
        };
        Inst[] steps = allowed is null
            ? [scope.Fail($"base-spire: source event eligibility is not implemented for {sourceName}")]
            : [scope.SetLocal(result, allowed)];
        return room.Map.RunActIndex is null ? steps : [scope.IfElse(
            scope.Equal(scope.Field(run, room.Map.RunActIndex), scope.Const(0L)), steps,
            [scope.Fail($"base-spire: source event eligibility for {sourceName} is not implemented after Act 1")])];
    }

    private static Expr? HasTwoPotions(IrScope scope)
    {
        var belt = scope.Package.Declarations.OfType<CollectionSymbol>()
            .SingleOrDefault(c => c.Name == "PotionBelt");
        return belt is null ? null : scope.GreaterOrEqual(scope.Count(scope.Collection(belt)), scope.Const(2L));
    }
}
