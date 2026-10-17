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

    /// <summary>RelicReward source draw: one Rewards rarity roll, player bag front, then shared bag removal.</summary>
    public Inst[] DrawReward(IrScope s, Expr run, FieldSymbol identity, FieldSymbol rarity)
    {
        var package = s.Package;
        var asset = package.Declarations.OfType<ComputeAssetSymbol>().SingleOrDefault(a => a.Name == "RewardRelicRarity");
        if (asset is null)
        {
            asset = package.ComputeAsset("RewardRelicRarity"); asset.BackendId = BaseSpireComputeBackend.BackendId;
            asset.SourcePath = "sts2/src/Core/Factories/RelicFactory.cs";
            asset.SourceHash = "RelicFactory.RollRarity:float32:0.5:0.83@1";
            asset.Entry = "TreasureRelicRarity"; // Same source RollRarity pure function as treasure/shop.
            asset.Input("Payload", FieldTypeSchema.Int(0, TreasureRelicRules.RandomSpan - 1));
            asset.Output("Rarity", FieldTypeSchema.String(16));
        }
        var payload = s.Local($"rewardRelicPayload{nextLocal++}", FieldTypeSchema.Int(0, TreasureRelicRules.RandomSpan - 1));
        var rolled = s.Local($"rewardRelicRarity{nextLocal++}", FieldTypeSchema.String(16));
        var found = s.Local($"rewardRelicFound{nextLocal++}", FieldTypeSchema.Bool());
        List<Inst> steps = [s.SampleInt(VanillaRandomLayout.EnginePlayerStreamName("Rewards"), s.Const(0L), s.Const(TreasureRelicRules.RandomSpan), payload),
            s.Compute(asset, [(asset.Inputs.Single(), s.LocalRef(payload))], [(asset.Outputs.Single(), rolled)]),
            s.SetField(run, identity, s.Const("base-spire:circlet")), s.SetField(run, rarity, s.Const("None")),
            s.SetLocal(found, s.Const(false))];
        foreach (var bucket in new[] { "Common", "Uncommon", "Rare" })
        {
            var bag = inventory.PlayerBags[bucket]; var head = s.First(s.Collection(bag));
            steps.Add(s.If(s.And(s.Not(s.LocalRef(found)), s.And(s.Equal(s.LocalRef(rolled), s.Const(bucket)), s.Greater(s.Count(s.Collection(bag)), s.Const(0L)))),
                [s.SetField(run, identity, s.Field(head, inventory.PlayerId)), s.SetField(run, rarity, s.Const(bucket)),
                 s.Destroy(head), s.SetLocal(found, s.Const(true))]));
            if (bucket != "Rare") steps.Add(s.If(s.And(s.Not(s.LocalRef(found)), s.Equal(s.LocalRef(rolled), s.Const(bucket))),
                s.SetLocal(rolled, s.Const(bucket == "Common" ? "Uncommon" : "Rare"))));
        }
        steps.AddRange(inventory.RemoveFromShared(s, s.Field(run, identity)));
        return [.. steps];
    }

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
