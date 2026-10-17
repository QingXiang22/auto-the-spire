using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Run;

public sealed record RelicSchema(EntityTypeSymbol Type, FieldSymbol Definition, FieldSymbol Rarity,
    CollectionSymbol Owned, FieldSymbol Counter, FieldSymbol IsWax, FieldSymbol IsMelted,
    FieldSymbol ShowCounter, FieldSymbol DisplayAmount, FieldSymbol StackCount)
{
    public static RelicSchema Declare(EnginePackageBuilder package, string collectionName = "Relics")
    {
        var type = package.EntityType("Relic");
        return new(type, type.AddField("DefinitionId", FieldTypeSchema.String(64)),
            type.AddField("Rarity", FieldTypeSchema.String(16)), package.Collection(collectionName, type),
            type.AddField("Counter", FieldTypeSchema.Int(0, 999999), visibility: FieldVisibility.Hidden),
            type.AddField("IsWax", FieldTypeSchema.Bool()), type.AddField("IsMelted", FieldTypeSchema.Bool()),
            type.AddField("ShowCounter", FieldTypeSchema.Bool()), type.AddField("DisplayAmount", FieldTypeSchema.Int(0, 999999)),
            type.AddField("StackCount", FieldTypeSchema.Int(1, 999999), Value.Of(1L)));
    }

    public (FieldSymbol Field, Expr Value)[] New(IrScope s, SliceRelic definition, bool isWax = false) =>
    [
        (Definition, s.Const(definition.ContentId)), (Rarity, s.Const(definition.Rarity)),
        (Counter, s.Const(0L)), (IsWax, s.Const(isWax)), (IsMelted, s.Const(false)),
        (ShowCounter, s.Const(definition.ShowCounter)), (DisplayAmount, s.Const(0L)), (StackCount, s.Const(1L)),
    ];
}

/// <summary>Supported nonstackable relic acquisition; sources retain selection, price and visit lifetime.</summary>
public sealed class RunRelics(RelicSchema schema, RunInitializationWiring inventory, RunResources resources,
    IReadOnlyList<SliceRelic> library, Func<IrScope, Expr, Inst[]>? gainPotionSlots = null)
{
    public RelicSchema Schema { get; } = schema;
    public IReadOnlyList<SliceRelic> Supported { get; } = [.. library.Where(r => CanObtain(r)
        && (gainPotionSlots is not null || !r.Effects.Any(e => e.Kind == RelicEffectKinds.GainPotionSlots)))];
    private int nextLocal;
    private ProcedureBuilder? obtainProcedure;
    private ProcedureBuilder? removeProcedure;

    public ProcedureSymbol DeclareObtainProcedure(EnginePackageBuilder package, EntityTypeSymbol runType)
    {
        if (obtainProcedure is not null) return obtainProcedure.Symbol;
        var body = package.Procedure("ObtainRelic");
        var owner = body.Param("Run", FieldTypeSchema.RefTo(runType));
        var definition = body.Param("Definition", FieldTypeSchema.String(64));
        body.Body(CompileObtain(body, body.Param(owner), body.Param(definition)));
        obtainProcedure = body;
        return body.Symbol;
    }

    public ProcedureSymbol DeclareRemoveProcedure(EnginePackageBuilder package, EntityTypeSymbol runType)
    {
        if (removeProcedure is not null) return removeProcedure.Symbol;
        var body = package.Procedure("RemoveRelic");
        var owner = body.Param("Run", FieldTypeSchema.RefTo(runType));
        var relic = body.Param("Relic", FieldTypeSchema.RefTo(Schema.Type));
        body.Body(Remove(body, body.Param(owner), body.Param(relic)));
        removeProcedure = body;
        return body.Symbol;
    }

    // Compile-time continuations. Runtime state and scheduling remain in Engine.
    public Func<IrScope, Expr, Expr, Inst[]> AfterObtained { get; init; } = (_, _, _) => [];
    public Func<IrScope, Expr, Expr, Inst[]> AfterRemoved { get; init; } = (_, _, _) => [];

    public Expr IsOwned(IrScope s, Expr relic) => s.GreaterOrEqual(s.CollectionIndex(Schema.Owned, relic), s.Const(0L));
    public Expr IsActive(IrScope s, Expr relic) => s.And(IsOwned(s, relic), s.Not(s.Field(relic, Schema.IsMelted)));
    public Expr HasActive(IrScope s, Expr definition) => s.Exists(s.Where(s.Collection(Schema.Owned),
        relic => s.And(s.Equal(s.Field(relic, Schema.Definition), definition), s.Not(s.Field(relic, Schema.IsMelted)))));

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

    public static bool CanObtain(SliceRelic relic) => string.IsNullOrEmpty(SpireRelics.UnsupportedReason(relic));

    private Inst[] ObtainEffect(IrScope s, Expr run, SliceRelicEffect effect) => effect.Kind switch
    {
        RelicEffectKinds.GainMaxHp => resources.GainMaxHp(s, run, s.Const((long)effect.Amount)),
        RelicEffectKinds.GainGold => resources.GainGold(s, run, s.Const((long)effect.Amount)),
        RelicEffectKinds.Heal => resources.HealLivingPlayer(s, run, s.Const((long)effect.Amount)),
        RelicEffectKinds.HealToFull => resources.HealToFull(s, run),
        RelicEffectKinds.HealMaxHpPercent => resources.HealMaxHpPercent(s, run, effect.Amount),
        RelicEffectKinds.GainPotionSlots when gainPotionSlots is not null => gainPotionSlots(s, s.Const((long)effect.Amount)),
        _ => throw new InvalidOperationException($"Unwired obtained relic effect: {effect.Kind}"),
    };

    public Inst[] Obtain(IrScope scope, Expr run, Expr definition, Expr? index = null, bool isWax = false)
        => obtainProcedure is not null && index is null && !isWax
            ? [scope.Call(obtainProcedure.Symbol, run, definition)] : CompileObtain(scope, run, definition, index, isWax);

    private Inst[] CompileObtain(IrScope scope, Expr run, Expr definition, Expr? index = null, bool isWax = false)
    {
        var operation = $"obtainRelic{nextLocal++}";
        var identity = scope.Local(operation + "Id", FieldTypeSchema.String(64));
        var supported = scope.Local(operation + "Supported", FieldTypeSchema.Bool());
        var created = scope.Local(operation + "Created", FieldTypeSchema.RefTo(Schema.Type));
        var position = scope.Local(operation + "Index", FieldTypeSchema.Int(-1, 999999));
        return [scope.SetLocal(identity, definition), scope.SetLocal(position, index ?? scope.Const(-1L)),
            scope.SetLocal(supported, scope.Const(false)),
            .. Supported.Select(relic => scope.If(scope.Equal(scope.LocalRef(identity), scope.Const(relic.ContentId)),
                [scope.CreateEntity(Schema.Type, scope.EntityScope(run), created, Schema.New(scope, relic, isWax)),
                 scope.IfElse(scope.Equal(scope.LocalRef(position), scope.Const(-1L)),
                     [scope.AddToCollection(Schema.Owned, scope.LocalRef(created))],
                     [scope.AddToCollection(Schema.Owned, scope.LocalRef(created), InsertPosition.AtIndex, scope.LocalRef(position))]),
                 .. inventory.RemoveFromPlayer(scope, scope.LocalRef(identity)),
                 .. inventory.RemoveFromShared(scope, scope.LocalRef(identity)),
                 .. relic.Effects.Where(effect => effect.Trigger == RelicTriggers.AfterObtained)
                     .SelectMany(effect => ObtainEffect(scope, run, effect)),
                 .. AfterObtained(scope, run, scope.LocalRef(created)),
                 scope.SetLocal(supported, scope.Const(true))])),
            scope.If(scope.Not(scope.LocalRef(supported)),
                scope.Fail("base-spire: obtained relic identity or effects are not implemented"))];
    }

    public Inst[] Remove(IrScope s, Expr run, Expr relic)
    {
        var selected = s.Local($"removeRelic{nextLocal++}", FieldTypeSchema.RefTo(Schema.Type));
        var captured = s.LocalRef(selected);
        return [s.SetLocal(selected, relic), s.IfElse(IsOwned(s, captured),
            [s.RemoveFromCollection(Schema.Owned, captured), .. AfterRemoved(s, run, captured), s.Destroy(captured)],
            [s.Fail("base-spire: cannot remove an unowned relic")])];
    }

    public Inst[] Replace(IrScope s, Expr run, Expr original, Expr definition)
    {
        var prefix = $"replaceRelic{nextLocal++}";
        var selected = s.Local(prefix, FieldTypeSchema.RefTo(Schema.Type));
        var target = s.Local(prefix + "Definition", FieldTypeSchema.String(64));
        var position = s.Local(prefix + "Index", FieldTypeSchema.Int(-1, 999999));
        var selectedRef = s.LocalRef(selected);
        Expr supported = s.Const(false);
        foreach (var entry in Supported)
            supported = s.Or(supported, s.Equal(s.LocalRef(target), s.Const(entry.ContentId)));
        return [s.SetLocal(selected, original), s.SetLocal(target, definition),
            s.SetLocal(position, s.CollectionIndex(Schema.Owned, selectedRef)),
            s.IfElse(s.And(IsOwned(s, selectedRef), supported),
                [.. Remove(s, run, selectedRef), .. Obtain(s, run, s.LocalRef(target), s.LocalRef(position))],
                [s.Fail("base-spire: replacement requires an owned relic and supported target")])];
    }

    public Inst[] Melt(IrScope s, Expr run, Expr relic)
    {
        var selected = s.Local($"meltRelic{nextLocal++}", FieldTypeSchema.RefTo(Schema.Type));
        var captured = s.LocalRef(selected);
        return [s.SetLocal(selected, relic), s.IfElse(s.And(IsActive(s, captured), s.Field(captured, Schema.IsWax)),
            [s.SetField(captured, Schema.IsMelted, s.Const(true)), .. AfterRemoved(s, run, captured)],
            [s.Fail("base-spire: melt requires an owned, unmelted wax relic")])];
    }

    public Inst[] SetCounter(IrScope s, Expr relic, Expr value) => ChangeCounter(s, relic, value, false);
    public Inst[] AddCounter(IrScope s, Expr relic, Expr amount) => ChangeCounter(s, relic, amount, true);

    private Inst[] ChangeCounter(IrScope s, Expr relic, Expr value, bool add)
    {
        var selected = s.Local($"countRelic{nextLocal++}", FieldTypeSchema.RefTo(Schema.Type));
        var captured = s.LocalRef(selected);
        return [s.SetLocal(selected, relic), s.IfElse(IsActive(s, captured),
            [s.SetField(captured, Schema.Counter, add ? s.Add(s.Field(captured, Schema.Counter), value) : value),
             s.If(s.Field(captured, Schema.ShowCounter), s.SetField(captured, Schema.DisplayAmount, s.Field(captured, Schema.Counter)))],
            [s.Fail("base-spire: relic counter requires an active owned relic")])];
    }
}
