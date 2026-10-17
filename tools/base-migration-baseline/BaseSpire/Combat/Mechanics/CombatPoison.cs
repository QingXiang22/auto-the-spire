using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Domain.Combat;

public sealed class CombatPoison
{
    private readonly ProcedureSymbol enemyStart;
    private readonly ProcedureSymbol playerStart;

    public CombatPoison(EnginePackageBuilder package, EntityTypeSymbol player, ScopeTypeSymbol battle,
        CollectionSymbol enemies, RelationSymbol powers, FieldSymbol definition, FieldSymbol stacks,
        FieldSymbol enemyHp, FieldSymbol playerHp, FieldSymbol settled, IReadOnlyList<string> poisonIds,
        Func<IrScope, Expr, Expr> accelerant, Func<IrScope, Expr, bool, Expr, Inst[]> damage)
    {
        var enemyProcedure = package.Procedure("ResolveEnemyPoison");
        var playerProcedure = package.Procedure("ResolvePlayerPoison");
        enemyStart = enemyProcedure.Symbol;
        playerStart = playerProcedure.Symbol;
        Inst[] Resolve(IrScope scope, bool enemySide)
        {
            var owner = scope.First(scope.Type(player, battle));
            var hp = enemySide ? enemyHp : playerHp;
            var opponents = enemySide ? scope.Type(player, battle) : scope.Collection(enemies);
            var participants = enemySide ? scope.Collection(enemies) : scope.Type(player, battle);
            var extra = scope.Local("accelerantTotal", FieldTypeSchema.Int(0, 999999));
            var iterations = scope.Local("poisonIterations", FieldTypeSchema.Int(0, 999999));
            return [scope.ForEach(participants, target => poisonIds.SelectMany(id => new[] {
                scope.ForEach(scope.Where(scope.Relation(powers, target), power => scope.And(
                    scope.Equal(scope.Field(power, definition), scope.Const(id)),
                    scope.Greater(scope.Field(power, stacks), scope.Const(0L)))), power => [
                    scope.SetLocal(extra, scope.Const(0L)),
                    scope.ForEach(opponents, opponent => [scope.If(scope.Greater(scope.Field(opponent, enemySide ? playerHp : enemyHp), scope.Const(0L)),
                        scope.SetLocal(extra, scope.Add(scope.LocalRef(extra), accelerant(scope, opponent))))]),
                    scope.SetLocal(iterations, scope.Minimum(scope.Field(power, stacks), scope.Add(scope.Const(1L), scope.LocalRef(extra)))),
                    scope.Repeat(scope.LocalRef(iterations), [scope.If(scope.And(
                        scope.And(scope.Not(scope.Field(owner, settled)), scope.Exists(scope.Collection(enemies))), scope.Greater(scope.Field(target, hp), scope.Const(0L))), [
                        .. damage(scope, target, enemySide, scope.Field(power, stacks)),
                        scope.If(scope.Greater(scope.Field(target, hp), scope.Const(0L)), [
                            scope.SetField(power, stacks, scope.Subtract(scope.Field(power, stacks), scope.Const(1L))),
                            scope.If(scope.Equal(scope.Field(power, stacks), scope.Const(0L)), scope.Destroy(power))])])])])
            }).ToArray())];
        }
        enemyProcedure.Body(Resolve(enemyProcedure, true));
        playerProcedure.Body(Resolve(playerProcedure, false));
    }

    public Inst Resolve(IrScope scope, bool enemySide) => scope.Call(enemySide ? enemyStart : playerStart);
}
