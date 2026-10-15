using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class CombatantCompilation
{
    internal static Inst[] SpawnStartingPowers(
        ContentBindings contentBindings,
        PowerCommandsBindings powerCommandsBindings,
        IrScope scope,
        SliceEnemy definition,
        int monsterSlot)
    {
        if (definition.StartingPowers.Count == 0)
        {
            return [];
        }

        var startingSteps = new List<Inst>();
        foreach (var (powerId, stacks) in definition.StartingPowers)
        {
            var declared = contentBindings.Author?.Powers.FirstOrDefault(entry => entry.Id == powerId);
            var ticks = declared is not null
                && string.Equals(declared.OwnerTurnEnd, PowerTurnEnd.Tick, StringComparison.Ordinal);
            startingSteps.Add(scope.Call(powerCommandsBindings.ApplyPower.Symbol, scope.LocalRef(monsterSlot), scope.Const(powerId),
                scope.Const((long)stacks), scope.Const(ticks), scope.Const(false)));
        }

        return startingSteps.ToArray();
    }

    internal static (FieldSymbol Field, Expr Value)[] MonsterFields(
        EnemiesBindings enemiesBindings,
        ShortMoveNameCallback ShortMoveName,
        MachineFieldsCallback MachineFields,
        IrScope scope,
        SliceEnemy definition,
        int? spawnHpOverride = null,
        Expr? hpValue = null)
    {
        var fields = new List<(FieldSymbol Field, Expr Value)>
            {
                (enemiesBindings.MonsterDefinition, scope.Const(definition.ContentId)),
                (enemiesBindings.MonsterHp, hpValue ?? scope.Const((long)(spawnHpOverride ?? definition.SliceHp))),
                (enemiesBindings.MonsterMaxHp, hpValue ?? scope.Const((long)definition.NormalMaxHp)),
                // BASE-01：`startingBlock`（来源声明）——出生格挡。原版获得格挡还要过 Dexterity/Frail 修正�?
                // 本批�?*未修�?*加；该差异列在构建描述里，不静默当成等价�?
                (enemiesBindings.MonsterBlock, scope.Const((long)definition.StartingBlock)),
                (enemiesBindings.MonsterMoveIndex, scope.Const(0L)),
                (enemiesBindings.MonsterFirstRitual, scope.Const((long)definition.FirstMoveRitual)),
                (enemiesBindings.MonsterStrikeDamage, scope.Const((long)definition.StrikeDamageNormal)),
                // 意图：原版敌人的开场意图就是它行为序列的第一个行动。`StartEnemyTurn` 每次行动前把�?
                // 更新�?*下一�?*行动（见 `BuildCycle`），因此该字段始终是"本轮会做什�?�?
                // `NextMove` �?32 字符的短名字段；冻结清单里的 `FirstMove` �?*描述文本**�?
                // 这时回退到切片的短名常量，程序化路径的字段值与形状因此逐字不变�?
                (enemiesBindings.MonsterNextMove, scope.Const(definition.Behavior is { InitialIsMove: false }
                    ? string.Empty
                    : ShortMoveName(definition.FirstMove))),
            };
        if (enemiesBindings.HasMachine)
        {
            fields.AddRange(MachineFields(scope, definition));
        }

        return [.. fields];
    }

    internal static IEnumerable<(FieldSymbol Field, Expr Value)> MachineFields(
        EnemiesBindings enemiesBindings,
        IrScope scope,
        SliceEnemy definition)
    {
        var behavior = definition.Behavior;
        var initialMove = behavior?.InitialMove ?? string.Empty;
        yield return (enemiesBindings.MachineState!, scope.Const(behavior?.InitialState ?? string.Empty));
        yield return (enemiesBindings.PerformedFirstMove!, scope.Const(false));
        yield return (enemiesBindings.LastMove!, scope.Const(initialMove));
        yield return (enemiesBindings.MoveRunLength!, scope.Const(initialMove.Length > 0 ? 1L : 0L));
        for (var index = 0; index < enemiesBindings.MoveHistory.Length; index++)
        {
            yield return (enemiesBindings.MoveHistory[index], scope.Const(index == 0 ? initialMove : string.Empty));
        }

        foreach (var (move, field) in enemiesBindings.OnceOnlyFields)
        {
            yield return (field, scope.Const(string.Equals(move, initialMove, StringComparison.Ordinal)));
        }
    }

}
