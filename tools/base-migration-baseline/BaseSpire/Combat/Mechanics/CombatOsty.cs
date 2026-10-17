using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Combat;

public sealed class CombatOsty
{
    public EntityTypeSymbol Entity { get; }
    public CollectionSymbol Pets { get; }
    public FieldSymbol Owner { get; }
    public FieldSymbol Hp { get; }
    public FieldSymbol MaxHp { get; }
    private readonly EventSymbol summoned;
    private readonly EventSymbol revived;
    private readonly FieldSymbol summonedPet;
    private readonly FieldSymbol summonedAmount;
    private readonly FieldSymbol revivedPet;
    private readonly EventSymbol damaged;
    private readonly FieldSymbol damagedPet;
    private readonly FieldSymbol damagedAmount;

    public CombatOsty(EnginePackageBuilder package, EntityTypeSymbol player)
    {
        Entity = package.EntityType("Osty");
        Owner = Entity.AddField("Owner", FieldTypeSchema.RefTo(player));
        Hp = Entity.AddField("Hp", FieldTypeSchema.Int(0, 999999));
        MaxHp = Entity.AddField("MaxHp", FieldTypeSchema.Int(0, 999999));
        Pets = package.Collection("OstyPets", Entity);
        summoned = package.Event("OstySummoned");
        summonedPet = summoned.AddPayload("Pet", FieldTypeSchema.RefTo(Entity));
        summonedAmount = summoned.AddPayload("Amount", FieldTypeSchema.Int(0, 999999));
        revived = package.Event("OstyRevived");
        revivedPet = revived.AddPayload("Pet", FieldTypeSchema.RefTo(Entity));
        damaged = package.Event("OstyDamaged");
        damagedPet = damaged.AddPayload("Pet", FieldTypeSchema.RefTo(Entity));
        damagedAmount = damaged.AddPayload("Amount", FieldTypeSchema.Int(0, 999999));
    }

    public Inst[] Change(IrScope scope, Expr owner, string operation, Expr amount)
    {
        var captured = scope.Local("ostyAmount", FieldTypeSchema.Int(-999999, 999999));
        var pet = scope.Local("ostyPet", FieldTypeSchema.RefTo(Entity));
        var target = scope.LocalRef(pet);
        var found = scope.First(scope.Where(scope.Collection(Pets), entry => scope.Equal(scope.Field(entry, Owner), owner)));
        Inst[] existing = operation switch
        {
            "summonOsty" => [scope.IfElse(scope.Greater(scope.Field(target, Hp), scope.Const(0L)),
                [scope.SetField(target, MaxHp, scope.Add(scope.Field(target, MaxHp), scope.LocalRef(captured))),
                 scope.SetField(target, Hp, scope.Add(scope.Field(target, Hp), scope.LocalRef(captured)))],
                [scope.SetField(target, MaxHp, scope.LocalRef(captured)), scope.SetField(target, Hp, scope.LocalRef(captured)),
                 scope.Emit(revived, (revivedPet, target))])],
            "healOsty" => [scope.If(scope.Greater(scope.Field(target, Hp), scope.Const(0L)),
                scope.SetField(target, Hp, scope.Minimum(scope.Field(target, MaxHp), scope.Add(scope.Field(target, Hp), scope.LocalRef(captured)))))],
            "loseOstyHp" => [scope.SetField(target, Hp, scope.Maximum(scope.Const(0L), scope.Subtract(scope.Field(target, Hp), scope.LocalRef(captured))))],
            _ => throw new InvalidOperationException($"Unknown Osty operation {operation}"),
        };
        return [scope.SetLocal(captured, amount), scope.If(scope.Less(scope.LocalRef(captured), scope.Const(0L)), scope.Fail("Osty amount must not be negative")),
            scope.If(scope.Greater(scope.LocalRef(captured), scope.Const(0L)), [scope.SetLocal(pet, found),
                scope.IfElse(scope.IsNone(target), operation == "summonOsty"
                    ? [scope.CreateEntity(Entity, scope.EntityScope(owner), pet, [(Owner, owner), (Hp, scope.LocalRef(captured)), (MaxHp, scope.LocalRef(captured))], Pets)] : [], existing),
                .. operation == "summonOsty" ? new[] { scope.Emit(summoned, (summonedPet, target), (summonedAmount, scope.LocalRef(captured))) } : Array.Empty<Inst>()])];
    }

    public Inst[] Attack(IrScope scope, Expr owner, Expr target, Expr amount, ProcedureSymbol damage)
    {
        var pet = scope.First(scope.Where(scope.Collection(Pets), entry => scope.Equal(scope.Field(entry, Owner), owner)));
        return [scope.If(scope.Less(amount, scope.Const(0L)), scope.Fail("Osty attack must not be negative")),
            scope.If(scope.Not(scope.IsNone(pet)), scope.If(scope.Greater(scope.Field(pet, Hp), scope.Const(0L)),
                scope.Call(damage, owner, target, amount, scope.Const(false))))];
    }
    public Inst[] Reset(IrScope scope) => [scope.ForEach(scope.Collection(Pets), pet => [scope.Destroy(pet)])];
    public Expr Living(IrScope scope, Expr owner) => scope.First(scope.Where(scope.Collection(Pets),
        pet => scope.And(scope.Equal(scope.Field(pet, Owner), owner), scope.Greater(scope.Field(pet, Hp), scope.Const(0L)))));
    public Inst[] Receive(IrScope scope, Expr owner, Expr amount)
    {
        var slot = scope.Local("ostyDamageTarget", FieldTypeSchema.RefTo(Entity));
        var pet = scope.LocalRef(slot);
        return [scope.SetLocal(slot, Living(scope, owner)), scope.SetField(pet, Hp, scope.Maximum(scope.Const(0L), scope.Subtract(scope.Field(pet, Hp), amount))),
            scope.Emit(damaged, (damagedPet, pet), (damagedAmount, amount))];
    }
}
