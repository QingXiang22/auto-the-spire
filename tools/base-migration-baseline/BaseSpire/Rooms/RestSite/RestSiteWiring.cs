using CoreTheSpire.Engine;
using BaseSpire.Slice.Orchestration;
using BaseSpire.Slice.Domain.Run;
namespace BaseSpire.Slice;

/// <summary>Rest-site options use persistent Run/deck state and shared upgrade recipes.</summary>
public static class RestSiteWiring
{
    public static void Register(EnginePackageBuilder package, RoomCompileContext room, RunResources resources,
        CardDefinitions recipes, Func<IrScope, Expr, Expr>? modifyHeal = null)
    {
        var schema = recipes.Schema;
        var maxHp = room.MaxHp;
        var restAction = package.Action(MapRoomWiring.RestActionName);
        var restBody = package.ActionBody(restAction);
        var restRun = room.CurrentRun(restBody);
        restBody.Role(SliceManifest.PlayerRole);
        Expr AtRestSite(IrScope scope, Expr run) =>
            room.At(scope, run, MapRoomWiring.RestSitePhase, "RestSite");
        restBody.Requires(AtRestSite(restBody, restRun));
        Expr healAmount = restBody.Divide(restBody.Multiply(restBody.Field(restRun, maxHp), restBody.Const(3L)), restBody.Const(10L));
        healAmount = modifyHeal?.Invoke(restBody, healAmount) ?? healAmount;
        restBody.Body([.. resources.HealLivingPlayer(restBody, restRun, healAmount),
            room.CloseVisit(restBody, restRun)]);

        var smithAction = package.Action(MapRoomWiring.SmithActionName);
        var smithParam = smithAction.Parameter("Card", FieldTypeSchema.RefTo(schema.Type));
        var smithBody = package.ActionBody(smithAction);
        var smithRun = room.CurrentRun(smithBody);
        var smithRef = smithBody.Param(smithParam.DeclarationIndex);
        Expr SmithEligible(Expr held) => recipes.CanUpgrade(smithBody, held);
        smithBody.Role(SliceManifest.PlayerRole);
        smithBody.Candidates("Card", smithBody.Where(smithBody.Collection(room.LongTermDeck), SmithEligible));
        smithBody.Requires(smithBody.And(AtRestSite(smithBody, smithRun), SmithEligible(smithRef)));
        // Selecting the card is the confirmation; not submitting Smith leaves the visit open.
        smithBody.Body(recipes.Upgrade(smithBody, smithRef),
            room.CloseVisit(smithBody, smithRun));

    }
}
