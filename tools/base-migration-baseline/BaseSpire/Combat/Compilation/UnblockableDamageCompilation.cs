using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Compilation;

internal sealed class UnblockableDamageCompilation
{
    private readonly ProcedureSymbol playerDamage;
    private readonly ProcedureSymbol enemyDamage;

    internal UnblockableDamageCompilation(ContentBindings content, PlayerBindings player, EnemiesBindings enemies,
        PilesBindings piles, EventsBindings events, PowerRulesBindings powerRules, PlayerHpLossBindings hpLoss)
    {
        var damageEnemy = content.Package.Procedure("DamageEnemyUnblockableUnpowered");
        var targetParam = damageEnemy.Param("Target", FieldTypeSchema.RefTo(enemies.Monster));
        var amountParam = damageEnemy.Param("Amount", FieldTypeSchema.Int(0, 999999));
        var target = damageEnemy.Param(targetParam);
        var actual = damageEnemy.Local("actualDamage", FieldTypeSchema.Int(0, 999999));
        damageEnemy.Body(damageEnemy.If(damageEnemy.Greater(damageEnemy.Field(target, enemies.MonsterHp), damageEnemy.Const(0L)), [
            damageEnemy.SetLocal(actual, damageEnemy.Param(amountParam)),
            .. powerRules.PowerRules.CapHpLoss(damageEnemy, target, actual, damageEnemy.Const(0L)),
            damageEnemy.SetField(target, enemies.MonsterHp, damageEnemy.Maximum(damageEnemy.Const(0L),
                damageEnemy.Subtract(damageEnemy.Field(target, enemies.MonsterHp), damageEnemy.LocalRef(actual)))),
            damageEnemy.Emit(events.DamageDealt, (events.DamageDealtTarget, target), (events.DamageDealtAmount, damageEnemy.LocalRef(actual))),
            damageEnemy.If(damageEnemy.Equal(damageEnemy.Field(target, enemies.MonsterHp), damageEnemy.Const(0L)), [
                damageEnemy.Emit(events.MonsterDied, (events.MonsterDiedMonster, target)), damageEnemy.RemoveFromCollection(piles.Monsters, target)])]));
        enemyDamage = damageEnemy.Symbol;
        var damagePlayer = content.Package.Procedure("DamagePlayerUnblockableUnpowered");
        var playerAmount = damagePlayer.Param("Amount", FieldTypeSchema.Int(0, 999999));
        var actualPlayer = damagePlayer.Local("actualDamage", FieldTypeSchema.Int(0, 999999));
        damagePlayer.Body(damagePlayer.If(damagePlayer.Greater(damagePlayer.Field(hpLoss.LostPlayer, player.PlayerHp), damagePlayer.Const(0L)), [
            damagePlayer.SetLocal(actualPlayer, damagePlayer.Param(playerAmount)),
            .. powerRules.PowerRules.CapHpLoss(damagePlayer, hpLoss.LostPlayer, actualPlayer, damagePlayer.Const(0L)),
            damagePlayer.Call(hpLoss.LosePlayerHp.Symbol, damagePlayer.LocalRef(actualPlayer)),
            damagePlayer.If(damagePlayer.Greater(damagePlayer.LocalRef(actualPlayer), damagePlayer.Const(0L)), powerRules.PowerRules.Call(damagePlayer, "ReceivedDamage"))]));
        playerDamage = damagePlayer.Symbol;
    }

    internal Inst[] Damage(IrScope scope, Expr target, bool enemySide, Expr amount) => enemySide
        ? [scope.Call(enemyDamage, target, amount)] : [scope.Call(playerDamage, amount)];
}
