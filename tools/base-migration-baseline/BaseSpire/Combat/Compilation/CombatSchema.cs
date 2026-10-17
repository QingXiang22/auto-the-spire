using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class CombatSchema
{
    internal static BattleBindings Battle(ContentBindings contentBindings)
    {

        var battleScope = contentBindings.Package.ScopeType("Battle");

        var cardKind = contentBindings.Package.Enum("CardKind");

        // B-06a：状态牌是第四种卡牌种类（来�?`type = status`）�?
        cardKind.Member("Attack").Member("Skill").Member("Power").Member("Status");

        return new BattleBindings(battleScope, cardKind);
    }

    internal static PlayerBindings Player(ContentBindings contentBindings)
    {

        // -- player and monsters ----------------------------------------------------------------------------
        var player = contentBindings.Package.EntityType("Player");

        player.OwnerFieldName = "Owner";

        player.ExtensibleFields = true;

        var playerOwner = player.AddField("Owner", FieldTypeSchema.Participant());

        var playerHp = player.AddField("Hp", FieldTypeSchema.Int(0, 999));

        var playerMaxHp = player.AddField("MaxHp", FieldTypeSchema.Int(1, 999));

        var playerBlock = player.AddField("Block", FieldTypeSchema.Int(0, 999));

        // B-06a：条件效果用�?本回合失去过生命"（来�?playerLostHpThisTurn）；回合开始清零�?
        var playerLostHp = player.AddField("LostHpThisTurn", FieldTypeSchema.Bool());

        // W-06：本回合是否**消耗过�?*（来�?`cardExhaustedThisTurn`；回合开始清零，进消耗区时置位）�?
        var playerExhaustedCard = player.AddField("ExhaustedCardThisTurn", FieldTypeSchema.Bool());

        // ENV-02：本回合**由牌获得格挡**的次数（UnmovablePower �?�?N 次翻�?要看它）�?
        var playerBlockGains = player.AddField("BlockGainsThisTurn", FieldTypeSchema.Int(0, 99));

        // ENV-T05：本次**战斗**里玩家"实际失去生命"的**事件次数**（TearAsunder 的 `playerHpLossCount`）。
        // 与 `LostHpThisTurn` 不同：这是战斗级计数，**不随回合清零**；每次真的掉 HP 记一次
        // （直接失去生命，或未被完全挡下的攻击）。由 CombatLifecycle 在换房时重置。
        var playerHpLossCount = player.AddField("HpLossCount", FieldTypeSchema.Int(0, 999));

        // 每次组装格挡步骤都要一个独立局部量（同一次出牌里可能有多处获得格挡）�?
        // 声明�?*所有调用点之前**：本地函数会捕获它，C# 的确定赋值按文本顺序检查�?
        var nextBlockGainSlot = new CompilationSlots();


        var playerEnergy = player.AddField("Energy", FieldTypeSchema.Int(0, 99));
        var playerStars = player.AddField("Stars", FieldTypeSchema.Int(0, 99999));
        var playerOrbSlots = player.AddField("OrbSlots", FieldTypeSchema.Int(0, 10));

        var playerMaxEnergy = player.AddField("MaxEnergy", FieldTypeSchema.Int(0, 99));

        var playerTurn = player.AddField("Turn", FieldTypeSchema.Int(0, 9999));

        // B-07a/W-05�?*这场战斗**是否已结算。放在战斗作用域的玩家上 �?按战斗实例幂�?
        // （新战斗 = 新玩家实�?= false），符合 §19.2"不能用一个不重置的全局布尔代表所有房�?�?
        var playerBattleSettled = player.AddField("BattleSettled", FieldTypeSchema.Bool());

        return new PlayerBindings(player, playerOwner, playerHp, playerMaxHp, playerBlock, playerLostHp, playerExhaustedCard, playerBlockGains, playerHpLossCount, nextBlockGainSlot, playerEnergy, playerStars, playerOrbSlots, playerMaxEnergy, playerTurn, playerBattleSettled);
    }

    internal static EnemiesBindings Enemies(ContentBindings contentBindings)
    {

        var monster = contentBindings.Package.EntityType("Monster");

        monster.ExtensibleFields = true;

        var monsterDefinition = monster.AddField("DefinitionId", FieldTypeSchema.String(64));

        var monsterHp = monster.AddField("Hp", FieldTypeSchema.Int(0, 9999));

        var monsterMaxHp = monster.AddField("MaxHp", FieldTypeSchema.Int(1, 9999));

        var monsterBlock = monster.AddField("Block", FieldTypeSchema.Int(0, 999));

        var monsterMoveIndex = monster.AddField("MoveIndex", FieldTypeSchema.Int(0, 255));

        // The two move parameters the slice's cultists differ in; a full move machine is B-06 work.
        var monsterFirstRitual = monster.AddField("FirstMoveRitual", FieldTypeSchema.Int(0, 99));

        var monsterStrikeDamage = monster.AddField("StrikeDamage", FieldTypeSchema.Int(0, 99));

        /// <summary>The move the monster will perform on its next turn; an intent the player may observe.</summary>
        var monsterNextMove = monster.AddField("NextMove", FieldTypeSchema.String(32));

        // -- BASE-02 �?1：敌人行�?*状态机**�?Engine 里的状�?-------------------------------------------------
        // 每只怪一份，等价于原版每场战斗一�?`MonsterMoveStateMachine`（`MonsterModel.SetUpForCombat`）�?
        // 这些字段�?*隐藏**的：原版公开的只�?已选意�?（NextMove），状态机的当前状态、日志与冷却窗口
        // 属于内部状态，不该变成公开事实（`FieldVisibility.Hidden` 不进观看者投影）�?
        var machineEnemies = contentBindings.Author?.Enemies.Where(enemy => enemy.Behavior is not null).ToList() ?? [];

        var hasMachine = machineEnemies.Count > 0;

        var machineState = hasMachine
            ? monster.AddField("MachineState", FieldTypeSchema.String(32), visibility: FieldVisibility.Hidden)
            : null;

        var performedFirstMove = hasMachine
            ? monster.AddField("PerformedFirstMove", FieldTypeSchema.Bool(), visibility: FieldVisibility.Hidden)
            : null;

        var lastMove = hasMachine
            ? monster.AddField("LastMove", FieldTypeSchema.String(32), visibility: FieldVisibility.Hidden)
            : null;

        var moveRunLength = hasMachine
            ? monster.AddField("MoveRunLength", FieldTypeSchema.Int(0, 99), visibility: FieldVisibility.Hidden)
            : null;

        // 冷却窗口：原版按"日志里最�?c �?*行动**"看某个行动是否出现过（`RandomBranchState.cs:158-165`），
        // 因此只需要最�?max(cooldown) 条行动历史�?
        var cooldownWindow = hasMachine ? machineEnemies.Max(enemy => enemy.Behavior!.MaxCooldown) : 0;

        var moveHistory = hasMachine
            ? Enumerable.Range(0, cooldownWindow)
                .Select(index => monster.AddField($"MoveHistory{index}", FieldTypeSchema.String(32),
                    visibility: FieldVisibility.Hidden))
                .ToArray()
            : [];

        // `UseOnlyOnce`（含 `maxUsesPerCombat=1`）：该行�?*出现�?*就权�?0（`RandomBranchState.cs:134-141`）�?
        var onceOnlyFields = hasMachine
            ? machineEnemies.SelectMany(enemy => enemy.Behavior!.OnceOnlyMoves)
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(move => move,
                    move => monster.AddField($"MoveUsed_{move}", FieldTypeSchema.Bool(),
                        visibility: FieldVisibility.Hidden),
                    StringComparer.Ordinal)
            : new Dictionary<string, FieldSymbol>(StringComparer.Ordinal);

        return new EnemiesBindings(monster, monsterDefinition, monsterHp, monsterMaxHp, monsterBlock, monsterMoveIndex, monsterFirstRitual, monsterStrikeDamage, monsterNextMove, machineEnemies, hasMachine, machineState, performedFirstMove, lastMove, moveRunLength, moveHistory, onceOnlyFields);
    }

    internal static CardsBindings Cards(ContentBindings contentBindings, BattleBindings battleBindings)
    {

        var variableFields = new Dictionary<string, FieldSymbol>(StringComparer.Ordinal);

        var cardSchema = CardSchema.Declare(contentBindings.Package, battleBindings.CardKind);

        var card = cardSchema.Type;

        var cardDefinition = cardSchema.Definition;

        var cardKindField = cardSchema.Kind;

        var cardUpgraded = cardSchema.Upgraded;

        var cardUnplayable = cardSchema.Unplayable;

        var cardEthereal = cardSchema.Ethereal;

        var cardCost = cardSchema.Cost;

        var cardCostModifier = cardSchema.CostModifier;

        var cardAmount = cardSchema.Amount;

        var cardPowerId = cardSchema.PowerId;

        var cardPowerStacks = cardSchema.PowerStacks;

        var cardLongTermSource = cardSchema.LongTermSource;

        var cardCloneOf = cardSchema.CloneOf;

        var cardDefinitions = new CardDefinitions(cardSchema, battleBindings.CardKind, contentBindings.Author, variableFields);

        // ENV-T05（PerfectedStrike）：**标签 → 成员定义 id**。`count = { cardsWithTag = { tag = Strike } }` 按
        // 原版是"玩家战斗内全部牌区里带该标签的牌数"（`PlayerCombatState.AllCards.Count(c => c.Tags.Contains(...))`）。
        // 标签是**卡牌定义**的属性（作者在卡级 `tags = { Strike }` 声明），因此这里编译成"实例的定义 id 落在该
        // 标签的成员集合里"。这样变换/生成出来的实例自动按新定义判定，不必给卡实例加一个可变的标签字段，
        // 也就不需要处理"复制/变换/降级时标签要不要跟着走"。集合与顺序都取自作者卡表（与 `RewardWiring`
        // 的池对账同源），不另外维护第二份名单。
        var taggedDefinitions = cardDefinitions.Compiled
            .SelectMany(entry => entry.Tags.Select(tag => (Tag: tag, entry.Id)))
            .GroupBy(entry => entry.Tag, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(entry => entry.Id)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)],
                StringComparer.Ordinal);

        return new CardsBindings(variableFields, cardSchema, card, cardDefinition, cardKindField, cardUpgraded, cardUnplayable, cardEthereal, cardCostModifier, cardAmount, cardPowerId, cardPowerStacks, cardDefinitions, taggedDefinitions);
    }

    internal static PowerBindings Power(ContentBindings contentBindings)
    {

        // -- power instance ---------------------------------------------------------------------------------
        var power = contentBindings.Package.EntityType("Power");

        power.ExtensibleFields = true;

        var powerDefinition = power.AddField("DefinitionId", FieldTypeSchema.String(64));

        var powerStacks = power.AddField("Stacks", FieldTypeSchema.Int(-99, 99));

        /// <summary>True when the power's turn-end behaviour is "lose one stack".</summary>
        var powerTicks = power.AddField("Ticks", FieldTypeSchema.Bool());

        /// <summary>True when the owner's next turn end skips this power's turn-end behaviour once.</summary>
        var powerSkip = power.AddField("SkipNextTick", FieldTypeSchema.Bool());

        return new PowerBindings(power, powerDefinition, powerStacks, powerTicks, powerSkip);
    }

    internal static KnowledgeBindings Knowledge(ContentBindings contentBindings, CardsBindings cardsBindings)
    {

        // -- what the player legitimately knows --------------------------------------------------------------
        // "Known" facts are pack-declared, never engine-inferred. A fact names the viewing participant and references
        // the real card instance the rules just identified, plus the area and the absolute position they stated. The
        // projection authorizes exactly those instances to that viewer, so no definition id ever stands in for an
        // instance and the hidden pile still exposes no member the player was not shown.
        var knownCard = contentBindings.Package.EntityType("KnownCard");

        knownCard.OwnerFieldName = "Viewer";

        var knownViewer = knownCard.AddField("Viewer", FieldTypeSchema.Participant(),
            visibility: FieldVisibility.OwnerPrivate);

        var knownCardRef = knownCard.AddField("Card", FieldTypeSchema.RefTo(cardsBindings.Card),
            visibility: FieldVisibility.OwnerPrivate);

        var knownPile = knownCard.AddField("Pile", FieldTypeSchema.String(16),
            visibility: FieldVisibility.OwnerPrivate);

        var knownPosition = knownCard.AddField("Position", FieldTypeSchema.Int(-1, 99),
            visibility: FieldVisibility.OwnerPrivate);

        var known = contentBindings.Package.Collection("Known", knownCard);

        var knownReveal = contentBindings.Package.Relation("KnownReveal", cardsBindings.Card);

        return new KnowledgeBindings(knownCard, knownViewer, knownCardRef, knownPile, knownPosition, known, knownReveal);
    }

    internal static PilesBindings Piles(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        CardsBindings cardsBindings,
        PowerBindings powerBindings)
    {

        // -- areas ------------------------------------------------------------------------------------------
        var hand = contentBindings.Package.Collection("Hand", cardsBindings.Card);

        hand.MembershipGroup = "pile";

        hand.Capacity = SliceManifest.MaxHandSize;

        var drawPile = contentBindings.Package.Collection("DrawPile", cardsBindings.Card);

        drawPile.MembershipGroup = "pile";

        // The draw pile is a hidden area: its size is public, but neither its members nor their order are. Known
        // positions are published separately through the pack's own facts.
        drawPile.IdentityVisible = false;

        drawPile.OrderVisible = false;

        var discardPile = contentBindings.Package.Collection("DiscardPile", cardsBindings.Card);

        discardPile.MembershipGroup = "pile";

        var exhaustPile = contentBindings.Package.Collection("ExhaustPile", cardsBindings.Card);

        exhaustPile.MembershipGroup = "pile";

        // A played card waits here while its own effect resolves, so it cannot select itself and the piles it reads
        // are the ones as they were before the play. It moves to its result area when the play finishes.
        var inPlay = contentBindings.Package.Collection("InPlay", cardsBindings.Card);

        inPlay.MembershipGroup = "pile";

        // A card played this turn is also in its result pile, so this area deliberately has no membership group.
        var playedThisTurn = contentBindings.Package.Collection("PlayedThisTurn", cardsBindings.Card);

        // A reshuffle may move a played card into the hidden draw pile. Its identity
        // remains known for the public turn history; this reveals no pile position.
        var playedReveal = contentBindings.Package.Relation("PlayedReveal", cardsBindings.Card);

        var playHistory = CardPlayHistory.Declare(contentBindings.Package, cardsBindings.CardSchema, battleBindings.CardKind);

        var executeCardPlay = contentBindings.Package.Procedure("ExecuteCardPlay");

        var lifecycle = new CombatLifecycle(cardsBindings.CardSchema, playHistory, playedThisTurn,
            playerBindings.PlayerLostHp, playerBindings.PlayerExhaustedCard, playerBindings.PlayerBlockGains, playerBindings.PlayerHpLossCount, playedReveal);

        var monsters = contentBindings.Package.Collection("Monsters", enemiesBindings.Monster);

        var powers = contentBindings.Package.Relation("Powers", powerBindings.Power);

        // -- areas as publicly identifiable entities ---------------------------------------------------------
        // protocol.md:83 requires a pile_ref to reference an entity whose kind_id uniquely matches a declared
        // pile_type, so every area sort is declared as its own entity kind. These entities carry no gameplay state:
        // the pack keeps which cards are where, and the engine only provides the entity and collection primitives.
        var handPile = contentBindings.Package.EntityType("HandPile");

        var drawPileEntity = contentBindings.Package.EntityType("DrawPileEntity");

        var discardPileEntity = contentBindings.Package.EntityType("DiscardPileEntity");

        var exhaustPileEntity = contentBindings.Package.EntityType("ExhaustPileEntity");

        var inPlayPileEntity = contentBindings.Package.EntityType("InPlayPileEntity");

        var areaEntities = new[] { handPile, drawPileEntity, discardPileEntity, exhaustPileEntity, inPlayPileEntity };

        return new PilesBindings(hand, drawPile, discardPile, exhaustPile, inPlay, playedThisTurn, playedReveal, playHistory, executeCardPlay, lifecycle, monsters, powers, areaEntities);
    }

    internal static EventsBindings Events(
        ContentBindings contentBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        CardsBindings cardsBindings,
        PowerBindings powerBindings,
        KnowledgeBindings knowledgeBindings,
        PilesBindings pilesBindings)
    {

        // -- events -----------------------------------------------------------------------------------------
        var turnStarted = contentBindings.Package.Event("TurnStarted");

        var turnStartedTurn = turnStarted.AddPayload("Turn", FieldTypeSchema.Int(0, 9999));

        var cardPlayed = contentBindings.Package.Event("CardPlayed");

        var cardPlayedCard = cardPlayed.AddPayload("Card", FieldTypeSchema.RefTo(cardsBindings.Card));

        var damageDealt = contentBindings.Package.Event("DamageDealt");

        var damageDealtTarget = damageDealt.AddPayload("Target", FieldTypeSchema.RefTo(enemiesBindings.Monster));

        var damageDealtAmount = damageDealt.AddPayload("Amount", FieldTypeSchema.Int(0, 999));

        var playerDamaged = contentBindings.Package.Event("PlayerDamaged");

        var playerDamagedAttacker = playerDamaged.AddPayload("Attacker", FieldTypeSchema.RefTo(enemiesBindings.Monster));

        var playerDamagedAmount = playerDamaged.AddPayload("Amount", FieldTypeSchema.Int(0, 999));

        var monsterDied = contentBindings.Package.Event("MonsterDied");

        var monsterDiedMonster = monsterDied.AddPayload("Monster", FieldTypeSchema.RefTo(enemiesBindings.Monster));

        var victory = contentBindings.Package.TerminalResult("Victory");

        var victoryTurn = victory.AddField("Turn", FieldTypeSchema.Int(0, 9999));

        var defeat = contentBindings.Package.TerminalResult("Defeat");

        var defeatTurn = defeat.AddField("Turn", FieldTypeSchema.Int(0, 9999));

        var projection = contentBindings.Package.Projection("PlayerView");

        projection.ParticipantRole = SliceManifest.PlayerRole;

        projection.Type(playerBindings.Player);

        projection.Type(enemiesBindings.Monster);

        projection.Type(cardsBindings.Card);

        projection.Type(powerBindings.Power);

        projection.Type(knowledgeBindings.KnownCard);

        foreach (var area in pilesBindings.AreaEntities) projection.Type(area);

        // The player may identify exactly the card instances its own facts reference (H-01).
        projection.Reveal(knowledgeBindings.KnownCard, knowledgeBindings.KnownReveal);

        projection.Reveal(playerBindings.Player, pilesBindings.PlayedReveal);

        return new EventsBindings(turnStarted, turnStartedTurn, cardPlayed, cardPlayedCard, damageDealt, damageDealtTarget, damageDealtAmount, playerDamaged, playerDamagedAttacker, playerDamagedAmount, monsterDied, monsterDiedMonster, victory, victoryTurn, defeat, defeatTurn, projection);
    }

    internal static PotionsBindings Potions(ContentBindings contentBindings, EventsBindings eventsBindings, PlayerBindings playerBindings)
    {

        // PotionInventoryRules owns potion schema, slots and reward candidates.
        var hasPotions = contentBindings.Author is not null && (contentBindings.Author.Potions.Count > 0 || contentBindings.Author.Belt.Count > 0 || contentBindings.Author.PotionPool.Count > 0);

        var potions = hasPotions ? new PotionInventoryRules(contentBindings.Package, contentBindings.Author!, playerBindings.Player) : null;

        var potion = potions?.Entity;

        var potionDefinition = potions?.Definition;

        var potionTarget = potions?.Target;

        var potionAmount = potions?.Amount;

        var potionPowerId = potions?.PowerId;

        var potionPowerStacks = potions?.PowerStacks;

        var potionBelt = potions?.Belt;

        if (hasPotions) eventsBindings.Projection.Type(potion!);

        return new PotionsBindings(hasPotions, potions, potion, potionDefinition, potionTarget, potionBelt);
    }
}
