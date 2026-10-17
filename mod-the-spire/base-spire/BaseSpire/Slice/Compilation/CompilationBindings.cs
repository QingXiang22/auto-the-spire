using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using BaseSpire.Slice.Rooms;
using BaseSpire.Slice.Domain.Run;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;
// Compile-time handles only. Runtime game state remains in Engine storage.
internal sealed record ContentBindings(
    EnginePackageBuilder Package,
    SliceAuthorModel? Author);

internal sealed record BattleBindings(
    ScopeTypeSymbol BattleScope,
    EnumTypeSymbol CardKind);

internal sealed record PlayerBindings(
    EntityTypeSymbol Player,
    FieldSymbol PlayerOwner,
    FieldSymbol PlayerHp,
    FieldSymbol PlayerMaxHp,
    FieldSymbol PlayerBlock,
    FieldSymbol PlayerLostHp,
    FieldSymbol PlayerExhaustedCard,
    FieldSymbol PlayerBlockGains,
    FieldSymbol PlayerHpLossCount,
    CompilationSlots NextBlockGainSlot,
    FieldSymbol PlayerEnergy,
    FieldSymbol PlayerStars,
    FieldSymbol PlayerOrbSlots,
    FieldSymbol PlayerMaxEnergy,
    FieldSymbol PlayerTurn,
    FieldSymbol PlayerBattleSettled);

internal sealed record EnemiesBindings(
    EntityTypeSymbol Monster,
    FieldSymbol MonsterDefinition,
    FieldSymbol MonsterHp,
    FieldSymbol MonsterMaxHp,
    FieldSymbol MonsterBlock,
    FieldSymbol MonsterMoveIndex,
    FieldSymbol MonsterFirstRitual,
    FieldSymbol MonsterStrikeDamage,
    FieldSymbol MonsterNextMove,
    List<AuthorEnemy> MachineEnemies,
    bool HasMachine,
    FieldSymbol? MachineState,
    FieldSymbol? PerformedFirstMove,
    FieldSymbol? LastMove,
    FieldSymbol? MoveRunLength,
    FieldSymbol[] MoveHistory,
    Dictionary<string, FieldSymbol> OnceOnlyFields);

internal sealed record CardsBindings(
    Dictionary<string, FieldSymbol> VariableFields,
    CardSchema CardSchema,
    EntityTypeSymbol Card,
    FieldSymbol CardDefinition,
    FieldSymbol CardKindField,
    FieldSymbol CardUpgraded,
    FieldSymbol CardUnplayable,
    FieldSymbol CardEthereal,
    FieldSymbol CardCostModifier,
    FieldSymbol CardAmount,
    FieldSymbol CardPowerId,
    FieldSymbol CardPowerStacks,
    CardDefinitions CardDefinitions,
    Dictionary<string, IReadOnlyList<string>> TaggedDefinitions);

internal sealed record PowerBindings(
    EntityTypeSymbol Power,
    FieldSymbol PowerDefinition,
    FieldSymbol PowerStacks,
    FieldSymbol PowerTicks,
    FieldSymbol PowerSkip);

internal sealed record KnowledgeBindings(
    EntityTypeSymbol KnownCard,
    FieldSymbol KnownViewer,
    FieldSymbol KnownCardRef,
    FieldSymbol KnownPile,
    FieldSymbol KnownPosition,
    CollectionSymbol Known,
    RelationSymbol KnownReveal);

internal sealed record PilesBindings(
    CollectionSymbol Hand,
    CollectionSymbol DrawPile,
    CollectionSymbol DiscardPile,
    CollectionSymbol ExhaustPile,
    CollectionSymbol InPlay,
    CollectionSymbol PlayedThisTurn,
    RelationSymbol PlayedReveal,
    CardPlayHistory PlayHistory,
    ProcedureBuilder ExecuteCardPlay,
    CombatLifecycle Lifecycle,
    CollectionSymbol Monsters,
    RelationSymbol Powers,
    EntityTypeSymbol[] AreaEntities);

internal sealed record EventsBindings(
    EventSymbol TurnStarted,
    FieldSymbol TurnStartedTurn,
    EventSymbol CardPlayed,
    FieldSymbol CardPlayedCard,
    EventSymbol DamageDealt,
    FieldSymbol DamageDealtTarget,
    FieldSymbol DamageDealtAmount,
    EventSymbol PlayerDamaged,
    FieldSymbol PlayerDamagedAttacker,
    FieldSymbol PlayerDamagedAmount,
    EventSymbol MonsterDied,
    FieldSymbol MonsterDiedMonster,
    TerminalResultSymbol Victory,
    FieldSymbol VictoryTurn,
    TerminalResultSymbol Defeat,
    FieldSymbol DefeatTurn,
    ProjectionSymbol Projection);

internal sealed record PowerQueryBindings(
    ProcedureBuilder PowerStacksOf);

internal sealed record PotionsBindings(
    bool HasPotions,
    PotionInventoryRules? Potions,
    EntityTypeSymbol? Potion,
    FieldSymbol? PotionDefinition,
    FieldSymbol? PotionTarget,
    CollectionSymbol? PotionBelt);

internal sealed record RunStateBindings(
    bool HasRun,
    EntityTypeSymbol? RunEntity,
    FieldSymbol? RunHp,
    FieldSymbol? RunMaxHp,
    FieldSymbol? RunGold,
    FieldSymbol? RunCurrentRoom,
    FieldSymbol? RunSettledRoom,
    FieldSymbol? RunRewardTaken,
    FieldSymbol? RunEncounter,
    FieldSymbol? RunRoomCount);

internal sealed record RelicsBindings(
    bool HasRelics,
    EntityTypeSymbol? Relic,
    FieldSymbol? RelicDefinition,
    FieldSymbol? RelicRarity,
    CollectionSymbol? Relics,
    Domain.Run.RelicSchema? Schema);

internal sealed record EncounterPoolBindings(
    EntityTypeSymbol? PoolEntry,
    FieldSymbol? PoolEncounterId,
    FieldSymbol? PoolKind,
    CollectionSymbol? PoolEntries,
    FieldSymbol? PoolBagUsed,
    EntityTypeSymbol? QueueEntry,
    FieldSymbol? QueueIndex,
    FieldSymbol? QueueEncounterId,
    CollectionSymbol? EncounterQueue,
    EntityTypeSymbol? EliteQueueEntry,
    FieldSymbol? EliteQueueIndex,
    FieldSymbol? EliteQueueEncounterId,
    CollectionSymbol? EliteEncounterQueue,
    IReadOnlyList<string> PoolTagIds,
    Dictionary<string, FieldSymbol> PoolTags);

internal sealed record RunEncountersBindings(
    FieldSymbol? RunHasLast,
    FieldSymbol? RunLastEncounter,
    Dictionary<string, FieldSymbol> RunLastTags,
    FieldSymbol? RunNormalVisited,
    FieldSymbol? RunEliteVisited);

internal sealed record RunCollectionsBindings(
    CollectionSymbol? RunStates,
    CollectionSymbol? LongTermDeck);

internal sealed record RoomServicesBindings(
    MapRoomWiring? ActMap,
    RunInitializationWiring? RunInitialization,
    RoomCompileContext? Rooms,
    RunResources? Resources,
    RunRelics? RelicCommands,
    RunDeck? DeckCommands,
    TreasureRoomWiring? Treasure,
    ShopRoomWiring? Shop,
    UnknownRoomsBindings? Unknown,
    RunActWiring? Acts = null);

/// <summary>
/// ENV-T09-unknown-room-modules-20261012：问号房 + 事件访问三件套的编译期绑定。
/// 只有 dev-map scope（有地图、有 Run 初始化库存）才存在；默认 scope 为 null，包形状逐字节不变。
/// </summary>
internal sealed record UnknownRoomsBindings(
    UnknownRoomWiring Odds,
    EventVisitState EventState,
    EventCompileContext EventContext,
    EventVisitLifecycle Events,
    EventRoomCatalog Catalog);

internal sealed record RewardsBindings(
    RewardWiring? Rewards);

internal sealed record SettlementBindings(
    ProcedureBuilder? CommitPermanent,
    ProcedureBuilder? SettleBattle);

internal sealed record PowerCommandsBindings(
    ProcedureBuilder ApplyPower,
    ProcedureBuilder ResolvePowerApplied,
    int AppliedPowerIdParam,
    int AppliedOwnerParam,
    ProcedureBuilder GainStrength);

internal sealed record PowerRulesBindings(
    PowerRuleRuntime PowerRules);

internal sealed record MonsterDamageBindings(
    ProcedureBuilder DamageMonster,
    int HitAttacker,
    int HitAmount,
    int HitPowered,
    int HitFixed,
    int HitStacks,
    int HitDamage,
    int HitBlock,
    int HitAbsorbed,
    Expr HitTargetRef);

internal sealed record PlayerHpLossBindings(
    ProcedureBuilder LosePlayerHp,
    int LostAmount,
    Expr LostPlayer);

internal sealed record AfterDamageBindings(
    ProcedureBuilder TriggerAfterDamage,
    int DamageSource);

internal sealed record PlayerDamageBindings(
    ProcedureBuilder DamagePlayer,
    int StruckAttacker,
    int StruckAmount,
    int StruckPowered,
    Expr StruckPlayer,
    int StruckFixed,
    int StruckStacks,
    int StruckDamage,
    int StruckBlock,
    int StruckAbsorbed);

internal sealed record PlayerBlockBindings(
    ProcedureBuilder GainPlayerBlock,
    int BlockAmount,
    Expr BlockPlayer,
    ProcedureBuilder TriggerAfterBlock);

internal sealed record TurnStartTriggerBindings(
    ProcedureBuilder TriggerTurnStart);

internal sealed record ExhaustTriggerBindings(
    ProcedureBuilder TriggerAfterExhaust);

internal sealed record PlayedTriggerBindings(
    ProcedureBuilder TriggerAfterPlayed,
    int PlayedForTrigger);

internal sealed record KnowledgeCommandsBindings(
    ProcedureBuilder ClearKnown,
    ProcedureBuilder RevokeKnown,
    ProcedureBuilder RecordKnownTop);

internal sealed record TurnEndBindings(
    ProcedureBuilder ResolveTurnEnd,
    int TurnEndOwner,
    int TurnEndEvent,
    int TurnEndOwnSide);

internal sealed record EnemyTurnBindings(
    ProcedureBuilder EndEnemyTurn,
    ProcedureBuilder StartEnemyTurn);

internal sealed record DrawBindings(
    CardDrawProcedures CardDraw,
    ProcedureSymbol Draw,
    AutoPlayProcedures AutoPlayCommands);

internal sealed record EnemySelectionBindings(
    ProcedureSymbol? SelectEnemyMove,
    CompilationSlots MachineSlot,
    string MonsterAiStream,
    int SourceWeightedPickArity,
    ComputeAssetSymbol? SourceWeightedPick,
    FieldSymbol? SourceWeightedPickBits,
    FieldSymbol? SourceWeightedPickCount,
    FieldSymbol? SourceWeightedPickPick,
    FieldSymbol[] SourceWeightedPickWeights)
{
    // Bound after declaring weighted-pick inputs; selection compilation shares the name allocator.
    public ProcedureSymbol? SelectEnemyMove { get; set; } = SelectEnemyMove;
}

internal sealed record PlayerTurnBindings(
    ProcedureBuilder StartPlayerTurn);

internal sealed record PlayerTurnEndBindings(
    ProcedureBuilder EndPlayerTurn);

internal sealed record SetupBindings(
    ProcedureBuilder Setup,
    int BattleLocal,
    int CharacterLocal,
    int MonsterLocal,
    int CreatedLocal);

internal sealed record RoomTransitionBindings(
    ProcedureBuilder? StartNextRoom);

internal sealed record EncounterQueuesBindings(
    ProcedureSymbol? EnqueueWeak,
    ProcedureSymbol? EnqueueRegular,
    ProcedureSymbol? EnqueueElite);
