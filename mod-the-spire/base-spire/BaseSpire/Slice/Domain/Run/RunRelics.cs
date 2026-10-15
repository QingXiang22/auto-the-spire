using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Run;

public sealed record RelicSchema(EntityTypeSymbol Type, FieldSymbol Definition, FieldSymbol Rarity,
    CollectionSymbol Owned);

/// <summary>Supported nonstackable relic acquisition; sources retain selection, price and visit lifetime.</summary>
public sealed class RunRelics(RelicSchema schema, RunInitializationWiring inventory, RunResources resources,
    IReadOnlyList<SliceRelic> library)
{
    public RelicSchema Schema { get; } = schema;
    public IReadOnlyList<SliceRelic> Supported { get; } = [.. library.Where(CanObtain)];
    private int nextLocal;

    public static bool CanObtain(SliceRelic relic) =>
        string.IsNullOrEmpty(SpireRelics.UnsupportedReason(relic))
        && relic.Effects.Where(effect => effect.Trigger == RelicTriggers.AfterObtained)
            .All(effect => effect.Kind is RelicEffectKinds.GainMaxHp or RelicEffectKinds.GainGold)
        && relic.Effects.All(effect => effect.Trigger is RelicTriggers.AfterObtained or RelicTriggers.AfterCombatVictory);

    public Inst[] Obtain(IrScope scope, Expr run, Expr definition)
    {
        var operation = $"obtainRelic{nextLocal++}";
        var identity = scope.Local(operation + "Id", FieldTypeSchema.String(64));
        var supported = scope.Local(operation + "Supported", FieldTypeSchema.Bool());
        var created = scope.Local(operation + "Created", FieldTypeSchema.RefTo(Schema.Type));
        return [scope.SetLocal(identity, definition), scope.SetLocal(supported, scope.Const(false)),
            .. Supported.Select(relic => scope.If(scope.Equal(scope.LocalRef(identity), scope.Const(relic.ContentId)),
                [scope.CreateEntity(Schema.Type, scope.EntityScope(run), created,
                    [(Schema.Definition, scope.Const(relic.ContentId)), (Schema.Rarity, scope.Const(relic.Rarity))], Schema.Owned),
                 .. inventory.RemoveFromPlayer(scope, scope.LocalRef(identity)),
                 .. inventory.RemoveFromShared(scope, scope.LocalRef(identity)),
                 .. relic.Effects.Where(effect => effect.Trigger == RelicTriggers.AfterObtained)
                     .SelectMany(effect => effect.Kind == RelicEffectKinds.GainMaxHp
                         ? resources.GainMaxHp(scope, run, scope.Const((long)effect.Amount))
                         : resources.GainGold(scope, run, scope.Const((long)effect.Amount))),
                 scope.SetLocal(supported, scope.Const(true))])),
            scope.If(scope.Not(scope.LocalRef(supported)),
                scope.Fail("base-spire: obtained relic identity or effects are not implemented"))];
    }
}
