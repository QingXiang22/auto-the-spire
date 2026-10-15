using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Combat;

public sealed class CombatOrbs
{
    public FieldSymbol Slots { get; }
    public CollectionSymbol Queue { get; }
    public EntityTypeSymbol Entity => orb;
    private readonly EntityTypeSymbol orb;
    private readonly FieldSymbol kind;
    private readonly FieldSymbol value;
    private readonly Func<IrScope, Expr> owner;
    private readonly Func<IrScope, Expr> active;
    private readonly Func<IrScope, Expr, Expr> focus;
    private readonly Func<IrScope, Expr, Inst[]> block;
    private readonly Func<IrScope, Expr, Inst[]> energy;
    private readonly CollectionSymbol enemies;
    private readonly FieldSymbol hp;
    private readonly ProcedureSymbol damage;
    private readonly int baseSlots;

    public CombatOrbs(EnginePackageBuilder package, FieldSymbol slots, Func<IrScope, Expr> owner,
        Func<IrScope, Expr> active, Func<IrScope, Expr, Expr> focus, Func<IrScope, Expr, Inst[]> block,
        Func<IrScope, Expr, Inst[]> energy, CollectionSymbol enemies, FieldSymbol hp, ProcedureSymbol damage, int baseSlots = 0)
    {
        this.owner = owner; this.active = active; this.focus = focus; this.block = block; this.energy = energy;
        this.enemies = enemies; this.hp = hp; this.damage = damage;
        if (baseSlots is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(baseSlots));
        this.baseSlots = baseSlots;
        Slots = slots;
        orb = package.EntityType("Orb");
        kind = orb.AddField("Kind", FieldTypeSchema.String(16));
        value = orb.AddField("StoredValue", FieldTypeSchema.Int(0, 99999));
        Queue = package.Collection("OrbQueue", orb); Queue.Capacity = 10;
    }

    public Inst[] Emit(IrScope scope, OrbCardEffect effect, Expr amount)
    {
        var count = scope.Local("orbAmount", FieldTypeSchema.Int(-99999, 99999));
        var capacity = scope.Field(owner(scope), Slots);
        Inst[] steps = effect.Operation switch
        {
            "channelOrb" => [scope.Repeat(scope.LocalRef(count), Channel(scope, effect.Kind))],
            "evokeOrb" => [scope.Repeat(scope.LocalRef(count), Evoke(scope, effect.Dequeue))],
            "addOrbSlots" => [scope.SetField(owner(scope), Slots, scope.Minimum(scope.Const(10L), scope.Add(capacity, scope.LocalRef(count))))],
            "removeOrbSlots" => [scope.SetField(owner(scope), Slots, scope.Maximum(scope.Const(0L), scope.Subtract(capacity, scope.LocalRef(count)))),
                scope.ForEach(scope.Skip(scope.Collection(Queue), capacity), item => [scope.Destroy(item)])],
            _ => throw new ArgumentOutOfRangeException(nameof(effect)),
        };
        return [scope.If(active(scope), [scope.SetLocal(count, amount),
            scope.If(scope.Less(scope.LocalRef(count), scope.Const(0L)), scope.Fail("Orb amount must not be negative")), .. steps])];
    }

    private Inst[] Channel(IrScope scope, string orbKind)
    {
        var created = scope.Local("channeledOrb", FieldTypeSchema.RefTo(orb));
        var capacity = scope.Field(owner(scope), Slots);
        return [scope.If(active(scope), [.. baseSlots == 0 ? new[] { scope.If(scope.Equal(capacity, scope.Const(0L)), scope.SetField(owner(scope), Slots, scope.Const(1L))) } : Array.Empty<Inst>(),
            scope.If(scope.GreaterOrEqual(scope.Count(scope.Collection(Queue)), capacity), Evoke(scope, true)),
            scope.If(scope.And(active(scope), scope.Greater(capacity, scope.Const(0L))), scope.CreateEntity(orb, scope.EntityScope(owner(scope)), created,
                [(kind, scope.Const(orbKind)), (value, scope.Const(orbKind == "Dark" ? 6L : orbKind == "Glass" ? 4L : 0L))], Queue))])];
    }

    public Inst[] Evoke(IrScope scope, bool dequeue)
    {
        var selected = scope.Local("evokedOrb", FieldTypeSchema.RefTo(orb));
        var read = scope.LocalRef(selected);
        return [scope.If(scope.And(active(scope), scope.Exists(scope.Collection(Queue))),
            [scope.SetLocal(selected, scope.First(scope.Collection(Queue))),
                .. dequeue ? new[] { scope.RemoveFromCollection(Queue, read) } : Array.Empty<Inst>(),
                .. Trigger(scope, read, true), .. dequeue ? new[] { scope.Destroy(read) } : Array.Empty<Inst>()])];
    }

    public Inst[] Passive(IrScope scope, bool turnStart) => [scope.ForEach(scope.Collection(Queue), item =>
        [scope.If(scope.And(active(scope), turnStart ? scope.Equal(scope.Field(item, kind), scope.Const("Plasma"))
            : scope.Not(scope.Equal(scope.Field(item, kind), scope.Const("Plasma")))), Trigger(scope, item, false))])];

    private Inst[] Trigger(IrScope scope, Expr item, bool evoke)
    {
        var target = scope.Local("orbTarget", FieldTypeSchema.Binding());
        var glassAmount = scope.Local("glassAmount", FieldTypeSchema.Int(0, 99999));
        Expr Modified(long baseValue) => scope.Maximum(scope.Const(0L), scope.Add(scope.Const(baseValue), focus(scope, owner(scope))));
        Expr glass = scope.Maximum(scope.Const(0L), scope.Add(scope.Field(item, value), focus(scope, owner(scope))));
        Inst Hit(Expr enemy, Expr amount) => scope.Call(damage, owner(scope), enemy, amount, scope.Const(false));
        var live = scope.Where(scope.Collection(enemies), enemy => scope.Greater(scope.Field(enemy, hp), scope.Const(0L)));
        return [scope.If(scope.Equal(scope.Field(item, kind), scope.Const("Lightning")),
                [scope.If(scope.Exists(live), [scope.SamplePick(SliceManifest.RandomTargetStream, live, target), Hit(scope.LocalRef(target), Modified(evoke ? 8 : 3))])]),
            scope.If(scope.Equal(scope.Field(item, kind), scope.Const("Frost")), block(scope, Modified(evoke ? 5 : 2))),
            scope.If(scope.Equal(scope.Field(item, kind), scope.Const("Plasma")), energy(scope, scope.Const(evoke ? 2L : 1L))),
            scope.If(scope.Equal(scope.Field(item, kind), scope.Const("Dark")), evoke
                ? [scope.If(scope.Exists(live), Hit(scope.First(scope.Sort(live, enemy => scope.Field(enemy, hp))), scope.Field(item, value)))]
                : [scope.SetField(item, value, scope.Add(scope.Field(item, value), Modified(6)))]),
            scope.If(scope.And(scope.Equal(scope.Field(item, kind), scope.Const("Glass")), scope.Greater(glass, scope.Const(0L))),
                [scope.SetLocal(glassAmount, glass),
                    .. evoke ? Array.Empty<Inst>() : new[] { scope.SetField(item, value, scope.Maximum(scope.Const(0L), scope.Subtract(scope.Field(item, value), scope.Const(1L)))) },
                    scope.ForEach(live, enemy => [Hit(enemy, evoke ? scope.Multiply(scope.LocalRef(glassAmount), scope.Const(2L)) : scope.LocalRef(glassAmount))])])];
    }
}
