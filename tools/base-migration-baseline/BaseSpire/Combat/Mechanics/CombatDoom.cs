using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Combat;

public sealed class CombatDoom
{
    private readonly ProcedureSymbol enemyEnd;
    private readonly ProcedureSymbol playerEnd;
    public CollectionSymbol Batch { get; }
    public FieldSymbol Target { get; }
    public EventSymbol BatchFinished { get; }

    public CombatDoom(EnginePackageBuilder package, EntityTypeSymbol player, ScopeTypeSymbol battle,
        CollectionSymbol enemies, FieldSymbol enemyHp, FieldSymbol playerHp, FieldSymbol settled,
        Func<IrScope, Expr, Expr> stacks, EventSymbol monsterDied, FieldSymbol diedTarget,
        Func<IrScope, Expr, Inst[]> playerDefeat)
    {
        var entry = package.EntityType("DoomBatchEntry");
        Target = entry.AddField("Target", FieldTypeSchema.AnyRef(), visibility: FieldVisibility.Hidden);
        Batch = package.Collection("DoomBatch", entry);
        Batch.IdentityVisible = false; Batch.OrderVisible = false;
        BatchFinished = package.Event("DiedToDoom");
        var count = BatchFinished.AddPayload("Count", FieldTypeSchema.Int(0, 999));
        var side = BatchFinished.AddPayload("Side", FieldTypeSchema.String(16));
        var enemyProcedure = package.Procedure("ResolveEnemyDoom");
        var playerProcedure = package.Procedure("ResolvePlayerDoom");
        enemyEnd = enemyProcedure.Symbol; playerEnd = playerProcedure.Symbol;
        Inst[] Resolve(IrScope scope, bool enemySide)
        {
            var owner = scope.First(scope.Type(player, battle));
            var candidates = enemySide ? scope.Collection(enemies) : scope.Type(player, battle);
            var hp = enemySide ? enemyHp : playerHp;
            var created = scope.Local("doomBatchEntry", FieldTypeSchema.RefTo(entry));
            var alive = scope.And(scope.Not(scope.Field(owner, settled)), scope.Exists(scope.Collection(enemies)));
            return [scope.If(alive, [
                scope.ForEach(scope.Where(candidates, creature => scope.And(scope.Greater(scope.Field(creature, hp), scope.Const(0L)),
                    scope.And(scope.Greater(stacks(scope, creature), scope.Const(0L)), scope.LessOrEqual(scope.Field(creature, hp), stacks(scope, creature))))),
                    creature => [scope.CreateEntity(entry, scope.EntityScope(owner), created, [(Target, creature)], Batch)]),
                scope.If(scope.Exists(scope.Collection(Batch)), [
                    scope.ForEach(scope.Collection(Batch), victim =>
                    {
                        var target = scope.Field(victim, Target);
                        return [scope.SetField(target, hp, scope.Const(0L)),
                            .. enemySide ? new[] { scope.Emit(monsterDied, (diedTarget, target)), scope.RemoveFromCollection(enemies, target) } : Array.Empty<Inst>()];
                    }),
                    scope.Emit(BatchFinished, (count, scope.Count(scope.Collection(Batch))), (side, scope.Const(enemySide ? "enemy" : "player"))),
                    .. enemySide ? Array.Empty<Inst>() : playerDefeat(scope, owner),
                    scope.ForEach(scope.Collection(Batch), victim => [scope.Destroy(victim)])])])];
        }
        enemyProcedure.Body(Resolve(enemyProcedure, true));
        playerProcedure.Body(Resolve(playerProcedure, false));
    }

    public Inst Resolve(IrScope scope, bool enemySide) => scope.Call(enemySide ? enemyEnd : playerEnd);
}
