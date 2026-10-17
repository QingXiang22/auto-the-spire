using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Run;

/// <summary>Compile-time adapters for the source gold hook phases. Defaults match currently supported content.</summary>
public sealed record GoldGainHooks(
    Func<IrScope, Expr, Expr, Expr> Modify,
    Func<IrScope, Expr, Expr, Inst[]> AfterModifying,
    Func<IrScope, Expr, Expr, Inst[]> AfterGained)
{
    public static GoldGainHooks Unmodified { get; } = new(
        (_, _, amount) => amount, (_, _, _) => [], (_, _, _) => []);
}

/// <summary>
/// Run-owned resource commands compile to Engine IR. They never read a Session or maintain runtime state.
/// Contract: docs/design/mod-the-spire/event-domain/spec.md, RUN-GOLD/DAMAGE/HEAL/DEATH.
/// </summary>
public sealed class RunResources(FieldSymbol hp, FieldSymbol maxHp, FieldSymbol goldField, TerminalResultSymbol defeat,
    FieldSymbol defeatTurn, GoldGainHooks? goldHooks = null)
{
    private readonly GoldGainHooks gold = goldHooks ?? GoldGainHooks.Unmodified;
    private int nextLocal;

    public RunResources(RoomCompileContext room, TerminalResultSymbol defeat, FieldSymbol defeatTurn,
        GoldGainHooks? goldHooks = null) : this(room.Hp, room.MaxHp, room.Gold, defeat, defeatTurn, goldHooks) { }

    public Inst[] GainGold(IrScope scope, Expr run, Expr amount)
    {
        var value = Snapshot(scope, "Gold");
        var read = scope.LocalRef(value);
        return
        [
            scope.SetLocal(value, amount),
            scope.SetLocal(value, gold.Modify(scope, run, read)),
            .. gold.AfterModifying(scope, run, read),
            scope.If(scope.Greater(read, scope.Const(0L)),
            [
                scope.SetField(run, goldField, scope.Add(scope.Field(run, goldField), read)),
                .. gold.AfterGained(scope, run, read),
            ]),
        ];
    }

    /// <summary>Supported noncombat Unblockable|Unpowered damage; other modifier participants remain explicit scope gaps.</summary>
    public Inst[] DamageOutsideCombat(IrScope scope, Expr run, Expr amount)
    {
        var value = Snapshot(scope, "Damage");
        var read = scope.LocalRef(value);
        return
        [
            scope.SetLocal(value, amount),
            scope.If(scope.Less(read, scope.Const(0L)), scope.Fail("Run damage cannot be negative")),
            scope.SetField(run, hp,
                scope.Maximum(scope.Const(0L), scope.Subtract(scope.Field(run, hp), read))),
        ];
    }

    /// <summary>Positive healing of a living player, capped by MaxHp. Revival is a separate, unsupported capability.</summary>
    public Inst[] HealLivingPlayer(IrScope scope, Expr run, Expr amount)
    {
        var value = Snapshot(scope, "Heal");
        var read = scope.LocalRef(value);
        return
        [
            scope.SetLocal(value, amount),
            scope.If(scope.Or(scope.Less(read, scope.Const(0L)), IsDead(scope, run)),
                scope.Fail("HealLivingPlayer requires a living player and nonnegative healing")),
            scope.SetField(run, hp,
                scope.Minimum(scope.Field(run, maxHp), scope.Add(scope.Field(run, hp), read))),
        ];
    }

    public Inst[] HealToFull(IrScope scope, Expr owner) =>
        HealLivingPlayer(scope, owner, scope.Subtract(scope.Field(owner, maxHp), scope.Field(owner, hp)));

    public Inst[] HealMaxHpPercent(IrScope scope, Expr owner, int percent) =>
        HealLivingPlayer(scope, owner, scope.Divide(scope.Multiply(scope.Field(owner, maxHp), scope.Const((long)percent)), scope.Const(100L)));

    public Inst[] LoseGold(IrScope scope, Expr run, Expr amount)
    {
        var value = Snapshot(scope, "GoldLoss");
        var read = scope.LocalRef(value);
        return [scope.SetLocal(value, amount),
            scope.If(scope.Less(read, scope.Const(0L)), scope.Fail("Gold loss cannot be negative")),
            scope.SetField(run, goldField,
                scope.Maximum(scope.Const(0L), scope.Subtract(scope.Field(run, goldField), read)))];
    }

    public Inst[] GainMaxHp(IrScope scope, Expr run, Expr amount)
    {
        var value = Snapshot(scope, "MaxHp");
        var read = scope.LocalRef(value);
        return [scope.SetLocal(value, amount),
            scope.If(scope.Or(scope.Less(read, scope.Const(0L)), IsDead(scope, run)),
                scope.Fail("GainMaxHp requires a living player and nonnegative amount")),
            scope.SetField(run, maxHp, scope.Add(scope.Field(run, maxHp), read)),
            .. HealLivingPlayer(scope, run, read)];
    }

    /// <summary>Source noncard max-HP loss: damage any excess current HP first, then clamp the new maximum to one.</summary>
    public Inst[] LoseMaxHp(IrScope scope, Expr run, Expr amount)
    {
        var value = Snapshot(scope, "MaxHpLoss");
        var maximum = Snapshot(scope, "NewMaxHp");
        var read = scope.LocalRef(value);
        var next = scope.LocalRef(maximum);
        return [scope.SetLocal(value, amount),
            scope.If(scope.Less(read, scope.Const(0L)), scope.Fail("Max HP loss cannot be negative")),
            scope.SetLocal(maximum, scope.Subtract(scope.Field(run, maxHp), read)),
            scope.If(scope.Less(next, scope.Field(run, hp)),
                DamageOutsideCombat(scope, run, scope.Subtract(scope.Field(run, hp), next))),
            scope.SetField(run, maxHp, scope.Maximum(scope.Const(1L), next))];
    }

    public Expr IsDead(IrScope scope, Expr run) => scope.Equal(scope.Field(run, hp), scope.Const(0L));

    /// <summary>Emitted by the action boundary after effects and event cleanup; noncombat has no turn.</summary>
    public Inst DeclareDefeat(IrScope scope) => scope.SetTerminal(defeat, (defeatTurn, scope.Const(0L)));

    private int Snapshot(IrScope scope, string operation) =>
        scope.Local($"run{operation}Amount{nextLocal++}", FieldTypeSchema.Int(-999999999, 999999999));
}
