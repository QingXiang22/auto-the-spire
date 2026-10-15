using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class CompilationCallbacks
{
    internal delegate SliceCard CardOfCallback(string contentId);
    internal delegate SliceEnemy EnemyOfCallback(string contentId);
    internal delegate List<Inst> DamageChainCallback(ProcedureBuilder scope, int amountSlot, int fixedSlot, int stacksSlot, int damageSlot,
            Expr attacker, Expr target, Expr powered);
    internal delegate Inst[] AdditiveStageCallback(ProcedureBuilder scope, int amountSlot, int fixedSlot, int stacksSlot, Expr attacker,
            Expr target);
    internal delegate Expr EnergyGainBlockedCallback(IrScope scope);
    internal delegate Expr BlockClearPreventedCallback(IrScope scope, Expr owner);
    internal delegate Expr DrawPreventedCallback(IrScope scope, Expr owner);
    internal delegate Expr MaxEnergyBonusCallback(IrScope scope, Expr owner);
    internal delegate Inst PotionEffectChainCallback(IrScope scope, Expr potionRef, Expr owner, int targetSlot, string targetKind);
    internal delegate Inst[] CapStageCallback(IrScope scope, int fixedSlot, int stacksSlot, Expr target);
    internal delegate Inst[] MultiplicativeStageCallback(ProcedureBuilder scope, int amountSlot, int fixedSlot, int stacksSlot, Expr attacker,
            Expr target);
    internal delegate Inst[] SettleTailCallback(IrScope scope);
    internal delegate Inst[] RelicVictoryStepsCallback(IrScope scope);
    internal delegate Inst[] VictoryTailCallback(IrScope scope, Expr turn);
    internal delegate Inst BattleEndTailCallback(IrScope scope);
    internal delegate Inst ExpireAtOpponentTurnEndCallback(IrScope scope, Expr power);
    internal delegate Inst[] TriggerChainCallback(IrScope scope, string trigger, Expr? playedCard = null);
    internal delegate Inst[] GrantPowerChainCallback(IrScope scope);
    internal delegate Inst[] RandomAttackChainCallback(IrScope scope);
    internal delegate Inst[] RetaliateChainCallback(IrScope scope, Expr attacker);
    internal delegate Inst HardcodedEnemyMoveCallback(Expr monster);
    internal delegate Inst DeclaredEnemyMoveCallback(Expr monster);
    internal delegate Inst MachinePerformCallback(Expr monster, AuthorEnemy enemy);
    internal delegate int EffectiveMoveCallback(AuthorEnemy enemy, int moveIndex);
    internal delegate Inst BuildCycleCallback(Expr monster, AuthorEnemy enemy);
    internal delegate Inst[] MoveInstructionsCallback(Expr monster, AuthorMove move);
    internal delegate Inst ApplyMovePowerCallback(Expr monster, string argument, bool self);
    internal delegate Inst AddCardsInstructionCallback(string argument);
    internal delegate Expr MoveValueCallback(Expr monster, AuthorMoveStep step);
    internal delegate Inst[] SelectMachineCallback(IrScope scope, Expr monster, AuthorEnemy enemy);
    internal delegate Inst[] ClearCostModifierCallback(IrScope scope, Expr held);
    internal delegate Inst HardcodedPowerTurnEndCallback(Expr power);
    internal delegate Inst DeclaredPowerTurnEndCallback(Expr power);
    internal delegate IEnumerable<Inst> EndTurnHandStepsCallback(Expr cardInHand);
    internal delegate Inst KeepOrDiscardCallback(Expr cardInHand);
    internal delegate Inst TurnEndHookChainCallback(Expr cardInHand);
    internal delegate Inst[] SpawnStartingPowersCallback(IrScope scope, SliceEnemy definition, int monsterSlot);
    internal delegate (FieldSymbol Field, Expr Value)[] MonsterFieldsCallback(IrScope scope, SliceEnemy definition,
            int? spawnHpOverride = null, Expr? hpValue = null);
    internal delegate IEnumerable<(FieldSymbol Field, Expr Value)> MachineFieldsCallback(IrScope scope, SliceEnemy definition);
    internal delegate string ShortMoveNameCallback(string declared);
    internal delegate (FieldSymbol Field, Expr Value)[] CardFieldsCallback(IrScope scope, SliceCard definition, bool upgraded,
            bool unplayable = false, bool ethereal = false);
    internal delegate IReadOnlyList<(FieldSymbol Field, Expr Value)> RewardCardFieldsCallback(IrScope scope, CardDefinition definition,
            bool upgraded);
    internal delegate IEnumerable<Inst> BlockGainStepsCallback(IrScope scope, Expr rawAmount, bool unpowered = false);
    internal delegate IEnumerable<Inst> EnemyBlockStepsCallback(Expr monster, Expr rawAmount);
    internal delegate CardEffectStateBindings EffectStateCallback(IrScope scope);
    internal delegate Inst[] CompileEffectsCallback(IrScope scope, Expr cardRef, Expr owner, Expr selectedTarget, int targetSlot,
            int discardSlot, int discardSlotsSlot, int createdSlot, CardEffectProgram program, bool unpowered = false,
            Expr? paidX = null, Expr? paidStarX = null);
    internal delegate CollectionSymbol PileCollectionCallback(CardEffectPile pile);
    internal delegate CardPileProcedures CardPilesCallback();
    internal delegate Inst[] DispatchCardHookCallback(IrScope scope, Expr cardRef, string hook);
    internal delegate ProcedureSymbol? EnqueueProcedureCallback(string kind, CollectionSymbol queue, EntityTypeSymbol queueEntryType,
            FieldSymbol queueIndexField, FieldSymbol queueEncounterIdField);
    internal delegate Expr HoldsPowerCallback(IrScope scope, Expr owner, string id);
    internal delegate Inst[] RemovePowerCallback(IrScope scope, Expr owner, string id);
    internal delegate Expr InCombatCallback(IrScope scope);
    internal delegate Inst[] ManifestEffectsCallback(CardPlayContext c);
    internal delegate Expr InnateCallback(IrScope s, Expr c);
    internal delegate Inst[] CloneToHandCallback(IrScope s, Expr c);
    internal delegate Inst[] ExhaustAttackAndGrowCallback(IrScope s, Expr c, string variable);
    internal delegate Expr HasTagCallback(IrScope scope, Expr instance, string tag);
}
