namespace CoreTheSpire;

public sealed record CombatEnemySpawn(string EnemyId, int? Hp = null, string? InstanceId = null,
    ContentId? StartingActionOverride = null, IReadOnlyList<ContentId>? StartingActionChoices = null,
    int? SlotIndex = null, int? TotalSlotCount = null);

public sealed record EnemyCombatObservation(string Id, int Hp, int MaxHp, int Block, int Strength,
    IntentKind Intent, int IntentDamage, IReadOnlyDictionary<ContentId, int> Powers)
{
    public bool IsEscaped { get; init; }
    public int SlotIndex { get; init; }
    public IReadOnlyDictionary<ContentId, int> ActionUseCounts { get; init; }
        = new Dictionary<ContentId, int>();
    public IReadOnlyDictionary<ContentId, IReadOnlyDictionary<ContentId, int>> PowerVariables { get; init; }
        = new Dictionary<ContentId, IReadOnlyDictionary<ContentId, int>>();
    public IReadOnlyDictionary<ContentId, int> Variables { get; init; }
        = new Dictionary<ContentId, int>();
    public IReadOnlyDictionary<ContentId, IReadOnlyList<CardInstance>> PowerCards { get; init; }
        = new Dictionary<ContentId, IReadOnlyList<CardInstance>>();
}

public sealed class CombatEnvironment(GameDefinition definition)
{
    public GameSession Start(uint seed, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, string enemyId, int? enemyHp = null, IEnumerable<ContentId>? drawPile = null,
        ContentId? characterId = null)
        => Start(new RandomStreams(seed), playerId, playerHp, playerMaxHp, hand, enemyId, enemyHp, drawPile, characterId);

    /// <summary>Starts combat from an active run room, using the run deck and its shared RNG streams.</summary>
    public GameSession StartForRun(RunSession runSession, string enemyId, int? enemyHp = null)
        => StartForRun(runSession, [new CombatEnemySpawn(enemyId, enemyHp)]);

    public GameSession StartEncounterForRun(RunSession runSession, ContentId encounterId)
    {
        ArgumentNullException.ThrowIfNull(runSession);
        if (!definition.Encounters.TryGetValue(encounterId, out var encounter))
            throw new KeyNotFoundException($"Unknown encounter '{encounterId}'.");
        if (runSession.State.CurrentRoom != encounter.RoomType)
            throw new InvalidOperationException($"Encounter '{encounterId}' is incompatible with active room '{runSession.State.CurrentRoom}'.");
        if (runSession.State.CurrentEncounterId is { } selectedEncounter && selectedEncounter != encounterId)
            throw new InvalidOperationException($"Encounter '{encounterId}' does not match the encounter selected for this room.");
        ValidateRunCombatStart(runSession);
        runSession.State.BindEncounterToCurrentRoom(encounter);
        var selections = runSession.State.CurrentEncounterSelections;
        return StartForRun(runSession, selections
            .Select(selection => new CombatEnemySpawn(selection.EnemyId.ToString(),
                StartingActionOverride: selection.StartingActionOverride,
                StartingActionChoices: selection.StartingActionChoices,
                SlotIndex: selection.SlotIndex, TotalSlotCount: selection.TotalSlotCount)).ToArray());
    }

    public GameSession StartCurrentEncounterForRun(RunSession runSession)
    {
        ArgumentNullException.ThrowIfNull(runSession);
        var encounterId = runSession.State.CurrentEncounterId
            ?? throw new InvalidOperationException("The active room has no encounter selected by its act definition.");
        return StartEncounterForRun(runSession, encounterId);
    }

    private GameSession StartForRun(RunSession runSession, IReadOnlyList<CombatEnemySpawn> spawns)
    {
        ArgumentNullException.ThrowIfNull(runSession);
        ValidateRunCombatStart(runSession);
        var run = runSession.State;
        foreach (var spawn in spawns)
        {
            var enemyDefinition = FindEnemy(spawn.EnemyId);
            if (enemyDefinition.Actions.Count == 0)
                throw new InvalidOperationException($"Enemy '{spawn.EnemyId}' has no actions.");
            if (spawn.Hp is < 1 || spawn.Hp > enemyDefinition.MaxHp)
                throw new ArgumentOutOfRangeException(nameof(spawns));
        }
        foreach (var card in run.Deck)
            if (!definition.Cards.ContainsKey(card.CardId))
                throw new InvalidOperationException($"Run card '{card.CardId}' is not registered in this combat definition.");

        var shuffledDeck = run.Deck.ToList();
        run.Random.Get("shuffle").Shuffle(shuffledDeck);
        var innate = shuffledDeck.Where(card =>
            (definition.Cards[card.CardId].ResolveKeywords(card) & CardKeywords.Innate) != 0).Take(5).ToArray();
        var openingIds = innate.Select(card => card.InstanceId).ToHashSet(StringComparer.Ordinal);
        var openingHand = innate.Concat(shuffledDeck.Where(card => !openingIds.Contains(card.InstanceId))
            .Take(5 - innate.Length)).ToArray();
        openingIds.UnionWith(openingHand.Select(card => card.InstanceId));
        var drawPile = shuffledDeck.Where(card => !openingIds.Contains(card.InstanceId)).ToArray();
        var session = StartWithInstances(run.Random, run.Character.Id.ToString(), run.Hp, run.MaxHp,
            openingHand, spawns, drawPile, run.Character.Id);
        session.RunOwner = runSession;
        run.ActiveCombat = session;
        foreach (var hook in definition.RunHooks)
            hook.AfterCombatStarted(new RunHookContext(run, hook.Id));
        CombatCardEvents.QueueInitialEntries(session);
        EffectExecutor.Resume(session);
        return session;
    }

    private static void ValidateRunCombatStart(RunSession runSession)
    {
        var run = runSession.State;
        if (run.Status != RunStatus.InProgress || run.Phase != RunPhase.ResolveRoom
            || run.CurrentRoom is not (RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss))
            throw new InvalidOperationException("Combat can only start in an active Monster, Elite, or Boss room.");
        if (run.ActiveCombat is not null)
            throw new InvalidOperationException("This run already has an active combat session.");
    }

    private GameSession Start(RandomStreams rng, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, string enemyId, int? enemyHp, IEnumerable<ContentId>? drawPile,
        ContentId? characterId = null)
    {
        return Start(rng, playerId, playerHp, playerMaxHp, hand,
            [new CombatEnemySpawn(enemyId, enemyHp)], drawPile, characterId);
    }

    public GameSession StartEncounter(uint seed, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, IEnumerable<CombatEnemySpawn> enemies, IEnumerable<ContentId>? drawPile = null,
        ContentId? characterId = null)
        => Start(new RandomStreams(seed), playerId, playerHp, playerMaxHp, hand, enemies, drawPile, characterId);

    public GameSession StartEncounter(uint seed, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, ContentId encounterId, ContentId? characterId = null)
    {
        if (!definition.Encounters.TryGetValue(encounterId, out var encounter))
            throw new KeyNotFoundException($"Unknown encounter '{encounterId}'.");
        var random = new RandomStreams(seed);
        var selections = encounter.SelectEnemies(random.Get("encounter-enemies"));
        return Start(random, playerId, playerHp, playerMaxHp, hand,
            selections.Select(selection => new CombatEnemySpawn(selection.EnemyId.ToString(),
                StartingActionOverride: selection.StartingActionOverride,
                StartingActionChoices: selection.StartingActionChoices,
                SlotIndex: selection.SlotIndex, TotalSlotCount: selection.TotalSlotCount)), drawPile: null, characterId);
    }

    private GameSession Start(RandomStreams rng, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, IEnumerable<CombatEnemySpawn> enemies, IEnumerable<ContentId>? drawPile,
        ContentId? characterId = null)
    {
        var handCards = hand.ToArray();
        var drawCards = drawPile?.ToArray() ?? [];
        var instanceIndex = 0;
        var handInstances = handCards.Select(card => new CardInstance($"fixture-card-{instanceIndex++}", card)).ToArray();
        var drawInstances = drawCards.Select(card => new CardInstance($"fixture-card-{instanceIndex++}", card)).ToArray();
        var session = StartWithInstances(rng, playerId, playerHp, playerMaxHp, handInstances, enemies, drawInstances, characterId);
        CombatCardEvents.QueueInitialEntries(session);
        EffectExecutor.Resume(session);
        return session;
    }

    private GameSession StartWithInstances(RandomStreams rng, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<CardInstance> hand, IEnumerable<CombatEnemySpawn> enemies, IEnumerable<CardInstance>? drawPile,
        ContentId? characterId = null)
    {
        if (characterId is { } character && !definition.Characters.ContainsKey(character))
            throw new KeyNotFoundException($"Unknown combat character '{character}'.");
        ArgumentNullException.ThrowIfNull(enemies);
        var enemySpawns = enemies.ToArray();
        if (enemySpawns.Length == 0) throw new ArgumentException("A combat encounter needs at least one enemy.", nameof(enemies));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playerMaxHp);
        if (playerHp < 0 || playerHp > playerMaxHp) throw new ArgumentOutOfRangeException(nameof(playerHp));
        var declaredSlotCounts = enemySpawns.Where(spawn => spawn.TotalSlotCount is not null)
            .Select(spawn => spawn.TotalSlotCount!.Value).Distinct().ToArray();
        if (declaredSlotCounts.Length > 1)
            throw new ArgumentException("Combat enemy spawns must agree on the total slot count.", nameof(enemies));
        var totalSlotCount = declaredSlotCounts.Length == 0 ? (int?)null : declaredSlotCounts[0];
        if (totalSlotCount is <= 0) throw new ArgumentOutOfRangeException(nameof(enemies));
        var slotIndexes = new HashSet<int>();
        var prepared = new (CombatEnemySpawn Spawn, EnemyDefinition Definition, string InstanceId, int SlotIndex)[enemySpawns.Length];
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < enemySpawns.Length; i++)
        {
            var spawn = enemySpawns[i];
            var slotIndex = spawn.SlotIndex ?? i;
            if (slotIndex < 0 || (totalSlotCount is { } capacity && slotIndex >= capacity)
                || !slotIndexes.Add(slotIndex))
                throw new ArgumentException("Combat enemy spawn slots must be unique indexes within the encounter capacity.", nameof(enemies));
            var enemyDefinition = FindEnemy(spawn.EnemyId);
            if (enemyDefinition.Actions.Count == 0)
                throw new InvalidOperationException($"Enemy '{spawn.EnemyId}' has no actions.");
            var entityId = spawn.InstanceId ?? (enemySpawns.Length == 1
                ? enemyDefinition.Id.ToString()
                : $"{enemyDefinition.Id}#{i + 1}");
            ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
            if (!instanceIds.Add(entityId)) throw new ArgumentException("Enemy instance IDs must be unique.", nameof(enemies));
            if (spawn.Hp is < 1 || spawn.Hp > enemyDefinition.MaxHp)
                throw new ArgumentOutOfRangeException(nameof(enemies));
            if (spawn.StartingActionOverride is { } startingAction
                && !enemyDefinition.Actions.Any(action => action.Id == startingAction))
                throw new ArgumentException($"Enemy '{spawn.EnemyId}' has no action '{startingAction}'.", nameof(enemies));
            if (spawn.StartingActionChoices is { } startChoices
                && (startChoices.Count < 2 || startChoices.Distinct().Count() != startChoices.Count
                    || startChoices.Any(actionId => !enemyDefinition.Actions.Any(action => action.Id == actionId))))
                throw new ArgumentException($"Enemy '{spawn.EnemyId}' has invalid starting action choices.", nameof(enemies));
            if (spawn.StartingActionOverride is not null && spawn.StartingActionChoices is not null)
                throw new ArgumentException("A combat enemy cannot have both a fixed starting action and starting action choices.", nameof(enemies));
            prepared[i] = (spawn, enemyDefinition, entityId, slotIndex);
        }

        var combatEnemies = new List<EnemyCombatState>(enemySpawns.Length);
        var chosenEnemyHp = new HashSet<int>();
        foreach (var (spawn, enemyDefinition, entityId, slotIndex) in prepared)
        {
            var hp = spawn.Hp ?? ChooseUniqueEnemyHp(enemyDefinition, chosenEnemyHp, rng.Get("niche"));
            chosenEnemyHp.Add(hp);
            var enemy = new EnemyCombatState(new EntityState(entityId, true, hp, hp), enemyDefinition)
            {
                ActionIndex = enemyDefinition.StartingActionIndex,
                SlotIndex = slotIndex
            };
            enemy.Creature.Block = enemyDefinition.StartingBlock;
            combatEnemies.Add(enemy);
        }
        var handInstances = hand.ToArray();
        var drawInstances = drawPile?.ToArray() ?? [];
        var allCardInstanceIds = handInstances.Concat(drawInstances).Select(card => card.InstanceId).ToArray();
        if (allCardInstanceIds.Distinct(StringComparer.Ordinal).Count() != allCardInstanceIds.Length)
            throw new ArgumentException("Combat card instance IDs must be unique.", nameof(hand));
        foreach (var card in handInstances.Concat(drawInstances))
        {
            if (!definition.Cards.ContainsKey(card.CardId))
                throw new KeyNotFoundException($"Unknown combat card '{card.CardId}'.");
            foreach (var offset in card.VariableOffsets)
                definition.Cards[card.CardId].ResolveDecimalVariable(card, offset.Variable);
        }

        var state = new GameState(definition,
            new PlayerCombatState(playerId, playerHp, playerMaxHp, handInstances, drawInstances, characterId),
            combatEnemies, totalSlotCount);
        var session = new GameSession(state, rng);
        foreach (var enemy in combatEnemies)
            foreach (var startingPower in enemy.Definition.StartingPowers)
                state.ApplyPower(session, enemy.Creature, startingPower.Power, startingPower.Amount, enemy.Creature);
        EffectExecutor.Resume(session);
        for (var enemyIndex = 0; enemyIndex < combatEnemies.Count; enemyIndex++)
        {
            var enemy = combatEnemies[enemyIndex];
            enemy.ActionIndex = prepared[enemyIndex].Spawn.StartingActionOverride is { } actionId
                ? enemy.Definition.GetActionIndex(actionId)
                : prepared[enemyIndex].Spawn.StartingActionChoices is { } choices
                    ? enemy.Definition.GetActionIndex(choices[rng.Get("enemy-ai").NextInt(choices.Count)])
                : enemy.Definition.ResolveStartingActionIndex(
                    new EnemyActionEvaluationContext(state, enemy, enemyIndex), rng.Get("enemy-ai"));
            enemy.SelectAction(enemy.Definition.Actions[enemy.ActionIndex].Id);
        }
        return session;
    }

    private static int ChooseUniqueEnemyHp(EnemyDefinition enemy, IReadOnlySet<int> previouslyChosenHp,
        SeededRandom random)
    {
        var candidates = Enumerable.Range(enemy.MinHp, enemy.MaxHp - enemy.MinHp + 1)
            .Where(hp => !previouslyChosenHp.Contains(hp)).ToArray();
        if (candidates.Length == 0)
            candidates = Enumerable.Range(enemy.MinHp, enemy.MaxHp - enemy.MinHp + 1).ToArray();
        return candidates[random.NextInt(candidates.Length)];
    }

    private EnemyDefinition FindEnemy(string enemyId) => definition.Enemies.Values
        .SingleOrDefault(e => e.Id.Value == enemyId || e.Id.ToString() == enemyId)
        ?? throw new KeyNotFoundException($"Unknown enemy '{enemyId}'.");

    public CombatObservation Observe(GameSession session) => ObserveSession(session);

    private CombatObservation ObserveSession(GameSession session)
    {
        var state = session.State;
        var primary = state.FirstLivingEnemy is { } living
            ? state.Enemies.Single(enemy => ReferenceEquals(enemy.Creature, living))
            : state.Enemies[0];
        var next = primary.Definition.Actions[Math.Min(primary.ActionIndex, primary.Definition.Actions.Count - 1)];
        var intentDamage = CalculateIntentDamage(session, primary, next);
        return new CombatObservation(state.Turn, state.Player.Creature.Hp, state.Player.Creature.MaxHp,
            state.Player.Creature.Block, state.Player.Energy, state.Player.Hand.ToArray(),
            new Dictionary<ContentId, int>(state.Player.Creature.Powers), primary.Creature.Hp,
            primary.Creature.MaxHp, primary.Creature.Block, primary.Creature.Strength, next.Intent, intentDamage,
            new Dictionary<ContentId, int>(primary.Creature.Powers), state.IsTerminal,
            state.PendingChoice is { } choice ? new PendingChoiceView(choice.Prompt, choice.Options.Select(option => option.Label).ToArray()) : null,
            Array.AsReadOnly(state.Enemies.Select(enemy => ObserveEnemy(session, enemy)).ToArray()),
            state.Player.DrawPile.ToArray(), state.Player.DiscardPile.ToArray(),
            state.Player.ExhaustPile.ToArray(), state.Player.PlayPile.ToArray(),
            state.Player.HandInstances.ToArray(), state.Player.DrawPileInstances.ToArray(),
            state.Player.DiscardPileInstances.ToArray(), state.Player.ExhaustPileInstances.ToArray(),
            state.Player.PlayPileInstances.ToArray(),
            Array.AsReadOnly(session.RunOwner?.State.Potions.ToArray() ?? []),
            state.PendingCardSelection switch
            {
                PendingCardChoice cardChoice => new PendingCardChoiceView(cardChoice.Effect.Prompt,
                    cardChoice.Options.ToArray()),
                PendingCardSetChoice cardSetChoice => new PendingCardSetChoiceView(cardSetChoice.Effect.Prompt,
                    cardSetChoice.Options.ToArray(), cardSetChoice.Selected.ToArray(),
                    cardSetChoice.Effect.MinChoices, cardSetChoice.MaxChoices),
                _ => null
            })
        {
            MaxEnergy = state.PlayerMaxEnergy,
            CharacterId = state.Player.CharacterId,
            HandPlayPreviews = Array.AsReadOnly(state.Player.HandInstances
                .Select(card => CombatCardRules.Preview(state, card)).ToArray()),
            PlayerPowerVariables = state.Player.Creature.PowerVariableSnapshot(),
            CombatVariables = state.CombatStateVariables
        };
    }

    private static EnemyCombatObservation ObserveEnemy(GameSession session, EnemyCombatState enemy)
    {
        var state = session.State;
        var action = enemy.Definition.Actions[Math.Min(enemy.ActionIndex, enemy.Definition.Actions.Count - 1)];
        var intent = enemy.SkipNextAction ? IntentKind.Stun : action.Intent;
        var damage = intent is IntentKind.Attack or IntentKind.DeathBlow
            ? CalculateIntentDamage(session, enemy, action) : 0;
        return new EnemyCombatObservation(enemy.Creature.Id, enemy.Creature.Hp, enemy.Creature.MaxHp,
            enemy.Creature.Block, enemy.Creature.Strength, intent, damage,
            new Dictionary<ContentId, int>(enemy.Creature.Powers))
        {
            IsEscaped = enemy.IsEscaped,
            ActionUseCounts = new Dictionary<ContentId, int>(enemy.ActionUseCounts),
            PowerVariables = enemy.Creature.PowerVariableSnapshot(),
            PowerCards = enemy.Creature.PowerCardSnapshot(),
            Variables = new Dictionary<ContentId, int>(enemy.Variables),
            SlotIndex = enemy.SlotIndex
        };
    }

    private static int CalculateIntentDamage(GameSession session, EnemyCombatState enemy,
        EnemyActionDefinition action)
    {
        if (action.Intent is not (IntentKind.Attack or IntentKind.DeathBlow)
            || action.IntentHitCount <= 0) return 0;
        var state = session.State;
        var perHit = action.IntentDamage / action.IntentHitCount;
        var damage = 0;
        for (var hit = 0; hit < action.IntentHitCount; hit++)
            damage = checked(damage + state.CalculateDamage(session, enemy.Creature,
                state.Player.Creature, perHit));
        return damage;
    }

    public IReadOnlyList<PlayerAction> LegalActions(GameSession session) => LegalActionsForSession(session);

    private IReadOnlyList<PlayerAction> LegalActionsForSession(GameSession session)
    {
        var state = session.State;
        if (state.PendingCardSelection is PendingCardChoice cardChoice)
            return cardChoice.Options.Select(card => (PlayerAction)new ChooseCardAction(card.InstanceId)).ToArray();
        if (state.PendingCardSelection is PendingCardSetChoice cardSetChoice)
        {
            var choices = cardSetChoice.Selected.Count < cardSetChoice.MaxChoices
                ? cardSetChoice.Options.Select(card => (PlayerAction)new ChooseCardAction(card.InstanceId)).ToList()
                : [];
            if (cardSetChoice.Selected.Count >= cardSetChoice.Effect.MinChoices)
                choices.Add(new ConfirmCardSelectionAction());
            return choices;
        }
        if (state.PendingChoice is { } pending)
            return Enumerable.Range(0, pending.Options.Count).Select(i => (PlayerAction)new ChooseOptionAction(i)).ToArray();
        if (state.IsTerminal) return [];
        if (!state.IsPlayerTurn) return [];
        var actions = new List<PlayerAction> { new EndTurnAction() };
        var enemies = state.Enemies.Where(enemy => enemy.IsActive).ToArray();
        for (var i = 0; i < state.Player.Hand.Count; i++)
        {
            var instance = state.Player.HandInstances[i];
            var card = definition.Cards[instance.CardId];
            if (card.MultiplayerOnly) continue;
            if ((card.ResolveKeywords(instance) & CardKeywords.Unplayable) != 0) continue;
            if (CombatCardRules.Preview(state, instance).EnergyToSpend > state.Player.Energy) continue;
            if (card.Target == TargetSelector.None)
                actions.Add(new PlayCardAction(i));
            else if (card.Target == TargetSelector.Self)
                actions.Add(new PlayCardAction(i, state.Player.Id));
            else if (card.Target == TargetSelector.SelectedTarget)
                actions.AddRange(enemies.Select(enemy => new PlayCardAction(i, enemy.Creature.Id)));
            else if (card.Target == TargetSelector.AllOpponents)
                actions.Add(new PlayCardAction(i));
            else if (card.Target == TargetSelector.RandomEnemy)
                actions.Add(new PlayCardAction(i));
            else
                actions.Add(new PlayCardAction(i, enemies.FirstOrDefault()?.Creature.Id));
        }
        if (session.RunOwner is { } runOwner)
        {
            for (var potionIndex = 0; potionIndex < runOwner.State.Potions.Count; potionIndex++)
            {
                if (runOwner.State.Potions[potionIndex] is not { } potionId) continue;
                var potion = definition.Potions[potionId];
                switch (potion.Target)
                {
                    case TargetSelector.Self:
                        actions.Add(new UsePotionAction(potionIndex, state.Player.Id));
                        break;
                    case TargetSelector.SelectedTarget:
                        actions.AddRange(enemies.Select(enemy => new UsePotionAction(potionIndex, enemy.Creature.Id)));
                        break;
                    case TargetSelector.FirstEnemy:
                        if (enemies.FirstOrDefault() is { } firstEnemy)
                            actions.Add(new UsePotionAction(potionIndex, firstEnemy.Creature.Id));
                        break;
                    case TargetSelector.AllOpponents:
                    case TargetSelector.RandomEnemy:
                        actions.Add(new UsePotionAction(potionIndex));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(potion.Target));
                }
            }
        }
        return actions;
    }

    public StepResult Step(GameSession session, PlayerAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var state = session.State;
        var firstEvent = state.Events.Count;
        if (state.PendingCardSelection is PendingCardChoice cardChoice)
        {
            if (action is not ChooseCardAction chooseCard
                || !cardChoice.Options.Any(card => card.InstanceId == chooseCard.CardInstanceId))
                throw new InvalidOperationException("A card from the pending pile selection must be chosen.");
            state.PendingCardSelection = null;
            var chosenCard = cardChoice.Options.Single(card => card.InstanceId == chooseCard.CardInstanceId);
            if (cardChoice.Effect.To is { } destination
                && !state.MoveCard(cardChoice.Effect.From, destination,
                    chooseCard.CardInstanceId, cardChoice.Effect.DestinationPosition, session.Random.Get("shuffle")))
                throw new InvalidOperationException("The selected card is no longer in its source pile.");
            state.Emit(session, "card_choice", state.Player.Id, chooseCard.CardInstanceId, 1);
            if (cardChoice.Effect.OnSelected is { } onSelected)
                state.PendingEffects.Add(new(cardChoice.Owner, onSelected, cardChoice.Target, chosenCard, cardChoice.Counts));
            if (cardChoice.Effect.To == CardPile.Exhaust && cardChoice.Effect.From != CardPile.Exhaust)
                EffectExecutor.QueueCardExhaustion(session, state.Player.Creature, chosenCard);
            EffectExecutor.Resume(session);
            ResumeChoiceContinuation(session);
        }
        else if (state.PendingCardSelection is PendingCardSetChoice cardSetChoice)
        {
            if (action is ChooseCardAction chooseCard
                && cardSetChoice.Options.Any(card => card.InstanceId == chooseCard.CardInstanceId)
                && cardSetChoice.Selected.Count < cardSetChoice.MaxChoices)
            {
                var chosen = cardSetChoice.Options.Single(card => card.InstanceId == chooseCard.CardInstanceId);
                cardSetChoice.Options.Remove(chosen);
                cardSetChoice.Selected.Add(chosen);
                state.Emit(session, "card_selected", cardSetChoice.Owner.Id, chosen.InstanceId, cardSetChoice.Selected.Count);
            }
            else if (action is ConfirmCardSelectionAction
                && cardSetChoice.Selected.Count >= cardSetChoice.Effect.MinChoices)
            {
                state.PendingCardSelection = null;
                state.Emit(session, "card_selection_confirmed", cardSetChoice.Owner.Id,
                    cardSetChoice.Owner.Id, cardSetChoice.Selected.Count);
                if (cardSetChoice.Effect.OnSelected is { } onSelected)
                {
                    for (var i = cardSetChoice.Selected.Count - 1; i >= 0; i--)
                        state.PendingEffects.Add(new(cardSetChoice.Owner, onSelected,
                            cardSetChoice.Target, cardSetChoice.Selected[i], cardSetChoice.Counts));
                }
                EffectExecutor.Resume(session);
                ResumeChoiceContinuation(session);
            }
            else throw new InvalidOperationException("A legal card selection or confirmation action is required.");
        }
        else if (state.PendingChoice is { } pending)
        {
            if (action is not ChooseOptionAction choose || choose.OptionIndex < 0 || choose.OptionIndex >= pending.Options.Count)
                throw new InvalidOperationException("A valid choice option is required to resume the effect.");
            state.PendingChoice = null;
            var choiceOwner = state.PendingChoiceOwner ?? state.Player.Creature;
            var choiceTarget = state.PendingChoiceTarget;
            var choiceSelectedCard = state.PendingChoiceSelectedCard;
            var choiceCounts = state.PendingChoiceCounts;
            state.PendingChoiceOwner = null;
            state.PendingChoiceTarget = null;
            state.PendingChoiceSelectedCard = null;
            state.PendingChoiceCounts = null;
            state.Emit(session, "choice", state.Player.Id, state.Player.Id, choose.OptionIndex);
            if (pending.Options[choose.OptionIndex].Effect is { } selectedEffect)
                EffectExecutor.Execute(session, choiceOwner, selectedEffect, choiceTarget, choiceSelectedCard, choiceCounts);
            EffectExecutor.Resume(session);
            ResumeChoiceContinuation(session);
        }
        else if (!LegalActionsForSession(session).Any(a => a == action))
        {
            throw new InvalidOperationException("Action is not legal in the current state.");
        }

        if (action is UsePotionAction usePotion)
        {
            var run = session.RunOwner?.State
                ?? throw new InvalidOperationException("Potions can only be used in a run combat.");
            var potionId = run.Potions[usePotion.PotionIndex]
                ?? throw new InvalidOperationException("The selected potion slot is empty.");
            var potion = definition.Potions[potionId];
            var target = usePotion.TargetId == state.Player.Id ? state.Player.Creature
                : state.Enemies.Select(enemy => enemy.Creature).FirstOrDefault(enemy => enemy.Id == usePotion.TargetId);
            if (!run.TryUsePotion(usePotion.PotionIndex, out _))
                throw new InvalidOperationException("The selected potion is no longer available.");
            state.Emit(session, "potion_used", state.Player.Id, state.Player.Id, usePotion.PotionIndex);
            EffectExecutor.Execute(session, state.Player.Creature, potion.Effect, target);
        }
        else if (action is PlayCardAction play)
        {
            var cardInstance = state.Player.HandInstances[play.HandIndex];
            var selectedTarget = play.TargetId == state.Player.Id ? state.Player.Creature
                : state.Enemies.Select(enemy => enemy.Creature).FirstOrDefault(enemy => enemy.Id == play.TargetId);
            CombatCardPlay.QueueManual(session, cardInstance, selectedTarget);
            EffectExecutor.Resume(session);
        }
        else if (action is EndTurnAction)
        {
            state.IsPlayerTurn = false;
            state.ProcessedTurnEndHandCardIds.Clear();
            state.TurnEndHandCardIds.Clear();
            state.EndTurnStage = EndTurnResolutionStage.AfterAutoPostPlayPowers;
            BeginPowerQueue(state, state.Player.Creature, PowerTrigger.AfterAutoPostPlayPhaseEntered);
            ContinueEndTurn(session);
        }
        return new StepResult(ObserveSession(session), state.Events.Skip(firstEvent).ToArray(),
            state.PendingChoice is not null || state.PendingCardSelection is not null);
    }

    private static void ResumeChoiceContinuation(GameSession session)
    {
        var state = session.State;
        if (state.PendingChoice is not null || state.PendingCardSelection is not null) return;
        if (state.EndTurnStage != EndTurnResolutionStage.None) ContinueEndTurn(session);
    }

    private static void ContinueEndTurn(GameSession session)
    {
        var state = session.State;
        while (state.PendingChoice is null && state.PendingCardSelection is null)
        {
            if (state.PendingPowerQueue is not null && !RunPendingPowerQueue(session)) return;
            switch (state.EndTurnStage)
            {
                case EndTurnResolutionStage.None:
                    return;
                case EndTurnResolutionStage.PlayerHandCleanup:
                    var etherealCard = state.Player.MutableHand.FirstOrDefault(card =>
                        (state.Definition.Cards[card.CardId].ResolveKeywords(card) & CardKeywords.Ethereal) != 0
                        && state.Definition.Hooks.All(hook => hook.ShouldEtherealTrigger(
                            new RuleHookContext(session, hook.Id), card)));
                    if (etherealCard is not null)
                    {
                        state.Player.MutableHand.Remove(etherealCard);
                        state.Player.MutableExhaustPile.Add(etherealCard);
                        EffectExecutor.QueueCardExhaustion(session, state.Player.Creature, etherealCard,
                            causedByEtherealExhaustion: true);
                        EffectExecutor.Resume(session);
                        break;
                    }
                    state.EndTurnStage = EndTurnResolutionStage.PlayerHandTurnEndEffects;
                    break;
                case EndTurnResolutionStage.PlayerHandTurnEndEffects:
                    var turnEndInstanceId = state.TurnEndHandCardIds.FirstOrDefault(instanceId =>
                        !state.ProcessedTurnEndHandCardIds.Contains(instanceId));
                    var turnEndCard = turnEndInstanceId is null ? null
                        : state.Player.MutableHand.FirstOrDefault(card => card.InstanceId == turnEndInstanceId);
                    if (turnEndCard is null)
                    {
                        if (turnEndInstanceId is not null)
                            state.ProcessedTurnEndHandCardIds.Add(turnEndInstanceId);
                        else
                        {
                            state.EndTurnStage = EndTurnResolutionStage.BeforeFlushHooks;
                            state.FlushHookIndex = 0;
                        }
                        break;
                    }
                    state.ProcessedTurnEndHandCardIds.Add(turnEndCard.InstanceId);
                    state.PendingEffects.Add(new(state.Player.Creature,
                        state.Definition.Cards[turnEndCard.CardId].TurnEndInHand!,
                        state.Player.Creature, turnEndCard));
                    EffectExecutor.Resume(session);
                    break;
                case EndTurnResolutionStage.AfterAutoPostPlayPowers:
                    state.EndTurnStage = EndTurnResolutionStage.AfterAutoPostPlayCards;
                    break;
                case EndTurnResolutionStage.AfterAutoPostPlayCards:
                    state.EndTurnStage = EndTurnResolutionStage.CapturePlayerTurnEndHandCards;
                    CombatCardEvents.QueueAfterAutoPostPlay(state);
                    EffectExecutor.Resume(session);
                    break;
                case EndTurnResolutionStage.CapturePlayerTurnEndHandCards:
                    state.TurnEndHandCardIds.AddRange(state.Player.MutableHand
                        .Where(card => state.Definition.Cards[card.CardId].TurnEndInHand is not null)
                        .Select(card => card.InstanceId));
                    state.EndTurnStage = EndTurnResolutionStage.PlayerHandCleanup;
                    break;
                case EndTurnResolutionStage.BeforeFlushHooks:
                case EndTurnResolutionStage.BeforeFlushLateHooks:
                case EndTurnResolutionStage.AfterFlushHooks:
                    if (!RunFlushHooks(session)) return;
                    break;
                case EndTurnResolutionStage.FlushHand:
                    FlushPlayerHand(session);
                    break;
                case EndTurnResolutionStage.PlayerTurnEndPowers:
                    state.EndTurnStage = EndTurnResolutionStage.PlayerOwnerTurnEndPowers;
                    state.CurrentEnemyIndex = 0;
                    BeginPowerQueue(state, state.Player.Creature, PowerTrigger.AfterPlayerTurnEnd);
                    state.ExpireCardCosts(session, CardCostExpiration.TurnEnd);
                    EffectExecutor.Resume(session);
                    break;
                case EndTurnResolutionStage.PlayerOwnerTurnEndPowers:
                    state.EndTurnStage = EndTurnResolutionStage.EnemyPlayerSideEndPowers;
                    BeginPowerQueue(state, state.Player.Creature, PowerTrigger.AfterOwnerTurnEnd);
                    break;
                case EndTurnResolutionStage.EnemyPlayerSideEndPowers:
                    while (state.CurrentEnemyIndex < state.Enemies.Count
                        && !state.Enemies[state.CurrentEnemyIndex].IsActive)
                        state.CurrentEnemyIndex++;
                    if (state.CurrentEnemyIndex >= state.Enemies.Count)
                    {
                        state.CurrentEnemyIndex = 0;
                        state.EndTurnStage = EndTurnResolutionStage.ExpirePlayerPowers;
                        break;
                    }
                    var enemyWithEndPower = state.Enemies[state.CurrentEnemyIndex++];
                    BeginPowerQueue(state, enemyWithEndPower.Creature, PowerTrigger.AfterPlayerTurnEnd);
                    break;
                case EndTurnResolutionStage.ExpirePlayerPowers:
                    foreach (var powerId in state.Player.Creature.Powers.Keys
                        .Where(id => state.Definition.Powers[id].RemoveAfterPlayerTurnEnd).ToArray())
                    {
                        var removed = state.RemovePower(state.Player.Creature, powerId);
                        if (removed != 0)
                            state.Emit(session, "power_removed", state.Player.Id, state.Player.Id, removed,
                                affectedPower: powerId);
                    }
                    state.EndTurnStage = EndTurnResolutionStage.EnemyOwnerTurnStartPowers;
                    state.CaptureEnemyTurnParticipants();
                    state.CurrentEnemyIndex = 0;
                    state.CardPlayHistory.Reset();
                    break;
                case EndTurnResolutionStage.EnemyOwnerTurnStartPowers:
                    while (state.CurrentEnemyIndex < state.EnemyTurnParticipants.Count
                        && !state.EnemyTurnParticipants[state.CurrentEnemyIndex].IsActive)
                        state.CurrentEnemyIndex++;
                    if (state.CurrentEnemyIndex >= state.EnemyTurnParticipants.Count)
                    {
                        state.CurrentEnemyIndex = 0;
                        state.EndTurnStage = EndTurnResolutionStage.EnemyAction;
                        break;
                    }
                    var startingEnemy = state.EnemyTurnParticipants[state.CurrentEnemyIndex++];
                    BeginPowerQueue(state, startingEnemy.Creature, PowerTrigger.AfterOwnerTurnStart);
                    break;
                case EndTurnResolutionStage.EnemyAction:
                    if (state.Player.Creature.Hp <= 0)
                    {
                        state.EndTurnStage = EndTurnResolutionStage.StartPlayerTurn;
                        break;
                    }
                    while (state.CurrentEnemyIndex < state.EnemyTurnParticipants.Count
                        && !state.EnemyTurnParticipants[state.CurrentEnemyIndex].IsActive)
                        state.CurrentEnemyIndex++;
                    if (state.CurrentEnemyIndex >= state.EnemyTurnParticipants.Count)
                    {
                        state.CurrentEnemyIndex = 0;
                        state.EndTurnStage = EndTurnResolutionStage.EnemyTurnEndPowers;
                        break;
                    }
                    var actingEnemy = state.EnemyTurnParticipants[state.CurrentEnemyIndex];
                    if (ShouldClearBlock(session, actingEnemy.Creature))
                        actingEnemy.Creature.Block = 0;
                    if (actingEnemy.SkipNextAction)
                    {
                        actingEnemy.SkipNextAction = false;
                        state.Emit(session, "enemy_action_skipped", state.Player.Id,
                            actingEnemy.Creature.Id, 1);
                        EffectExecutor.Resume(session);
                        state.Emit(session, "enemy_action", actingEnemy.Creature.Id, state.Player.Id,
                            (int)IntentKind.Stun);
                        state.CurrentEnemyIndex++;
                        break;
                    }
                    var action = actingEnemy.Definition.Actions[actingEnemy.ActionIndex];
                    state.Emit(session, "enemy_action", actingEnemy.Creature.Id, state.Player.Id, (int)action.Intent);
                    state.EndTurnStage = EndTurnResolutionStage.ApplyEnemyActionPower;
                    EffectExecutor.Execute(session, actingEnemy.Creature, action.Effect, state.Player.Creature);
                    break;
                case EndTurnResolutionStage.ApplyEnemyActionPower:
                    var currentEnemy = state.EnemyTurnParticipants[state.CurrentEnemyIndex];
                    var selectedAction = currentEnemy.Definition.Actions[currentEnemy.ActionIndex];
                    state.EndTurnStage = EndTurnResolutionStage.EnemyAction;
                    if (selectedAction.AppliedPower is { } appliedPower)
                    {
                        var powerOwner = selectedAction.AppliedPowerTarget == TargetSelector.Self
                            ? currentEnemy.Creature
                            : state.Player.Creature;
                        state.ApplyPower(session, powerOwner, appliedPower,
                            selectedAction.AppliedPowerAmount, currentEnemy.Creature);
                    }
                    state.CurrentEnemyIndex++;
                    break;
                case EndTurnResolutionStage.EnemyTurnEndPowers:
                    while (state.CurrentEnemyIndex < state.EnemyTurnParticipants.Count
                        && !state.EnemyTurnParticipants[state.CurrentEnemyIndex].IsActive)
                        state.CurrentEnemyIndex++;
                    if (state.CurrentEnemyIndex >= state.EnemyTurnParticipants.Count)
                    {
                        state.CurrentEnemyIndex = 0;
                        state.EndTurnStage = EndTurnResolutionStage.PlayerEnemySideEndPowers;
                        break;
                    }
                    var endedEnemy = state.EnemyTurnParticipants[state.CurrentEnemyIndex];
                    state.EndTurnStage = EndTurnResolutionStage.EnemyOwnerTurnEndPowers;
                    BeginPowerQueue(state, endedEnemy.Creature, PowerTrigger.AfterEnemyTurnEnd);
                    break;
                case EndTurnResolutionStage.EnemyOwnerTurnEndPowers:
                    var ownerEndedEnemy = state.EnemyTurnParticipants[state.CurrentEnemyIndex++];
                    state.EndTurnStage = EndTurnResolutionStage.EnemyTurnEndPowers;
                    BeginPowerQueue(state, ownerEndedEnemy.Creature, PowerTrigger.AfterOwnerTurnEnd);
                    break;
                case EndTurnResolutionStage.PlayerEnemySideEndPowers:
                    state.EndTurnStage = EndTurnResolutionStage.AdvanceEnemyAction;
                    BeginPowerQueue(state, state.Player.Creature, PowerTrigger.AfterEnemyTurnEnd);
                    break;
                case EndTurnResolutionStage.AdvanceEnemyAction:
                    for (var enemyIndex = 0; enemyIndex < state.EnemyTurnParticipants.Count; enemyIndex++)
                    {
                        var enemy = state.EnemyTurnParticipants[enemyIndex];
                        if (!enemy.IsActive) continue;
                        var currentAction = enemy.Definition.Actions[enemy.ActionIndex];
                        enemy.ActionIndex = enemy.Definition.ResolveNextActionIndex(currentAction.Id,
                            enemy.LastActionId, enemy.ConsecutiveActionCount,
                            new EnemyActionEvaluationContext(state, enemy, state.Enemies.IndexOf(enemy)),
                            session.Random.Get("enemy-ai"));
                        enemy.SelectAction(enemy.Definition.Actions[enemy.ActionIndex].Id);
                    }
                    state.CurrentEnemyIndex = 0;
                    state.EndTurnStage = EndTurnResolutionStage.StartPlayerTurn;
                    break;
                case EndTurnResolutionStage.StartPlayerTurn:
                    if (!state.IsTerminal)
                    {
                        state.Turn++;
                        state.CardPlayHistory.Reset();
                        state.Player.Energy = state.PlayerMaxEnergy;
                        if (ShouldClearBlock(session, state.Player.Creature))
                            state.Player.Creature.Block = 0;
                        state.IsPlayerTurn = true;
                        state.Emit(session, "player_turn_start", state.Player.Id, state.Player.Id, state.Turn);
                        state.EndTurnStage = EndTurnResolutionStage.PlayerTurnStartPowers;
                        BeginPowerQueue(state, state.Player.Creature, PowerTrigger.AfterPlayerTurnStart);
                        break;
                    }
                    state.EndTurnStage = EndTurnResolutionStage.None;
                    return;
                case EndTurnResolutionStage.PlayerTurnStartPowers:
                    state.EndTurnStage = EndTurnResolutionStage.EnemyPlayerTurnStartPowers;
                    state.CurrentEnemyIndex = 0;
                    break;
                case EndTurnResolutionStage.EnemyPlayerTurnStartPowers:
                    while (state.CurrentEnemyIndex < state.Enemies.Count
                        && !state.Enemies[state.CurrentEnemyIndex].IsActive)
                        state.CurrentEnemyIndex++;
                    if (state.CurrentEnemyIndex >= state.Enemies.Count)
                    {
                        state.CurrentEnemyIndex = 0;
                        state.EndTurnStage = EndTurnResolutionStage.PlayerOwnerTurnStartPowers;
                        break;
                    }
                    var enemyAtPlayerTurnStart = state.Enemies[state.CurrentEnemyIndex++];
                    BeginPowerQueue(state, enemyAtPlayerTurnStart.Creature, PowerTrigger.AfterPlayerTurnStart);
                    break;
                case EndTurnResolutionStage.PlayerOwnerTurnStartPowers:
                    state.EndTurnStage = EndTurnResolutionStage.None;
                    BeginPowerQueue(state, state.Player.Creature, PowerTrigger.AfterOwnerTurnStart);
                    if (!state.IsTerminal) EffectExecutor.Execute(session, state.Player.Creature, new DrawCardsEffect(5));
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static void BeginPowerQueue(GameState state, EntityState owner, PowerTrigger trigger)
    {
        state.PendingPowerOwner = owner;
        state.PendingPowerTrigger = trigger;
        state.PendingPowerQueue = owner.Powers
            .Where(pair => state.Definition.Powers[pair.Key].Trigger == trigger
                || state.Definition.Powers[pair.Key].Lifecycle.Any(subscription => subscription.Trigger == trigger)
                || (state.Definition.Powers[pair.Key].RemoveAfterOwnerTurnEnd && IsOwnerTurnEnd(owner, trigger))
                || (state.Definition.Powers[pair.Key].RemoveAfterOpponentTurnEnd && IsOpponentTurnEnd(owner, trigger)))
            .Select(pair => pair.Key)
            .ToList();
        state.PendingPowerIndex = 0;
    }

    private static bool RunPendingPowerQueue(GameSession session)
    {
        var state = session.State;
        var owner = state.PendingPowerOwner ?? throw new InvalidOperationException("Missing power trigger owner.");
        var queue = state.PendingPowerQueue!;
        while (state.PendingPowerIndex < queue.Count && state.PendingChoice is null
            && state.PendingCardSelection is null)
        {
            var powerId = queue[state.PendingPowerIndex++];
            if (!owner.Powers.TryGetValue(powerId, out var amount)) continue;
            var power = state.Definition.Powers[powerId];
            var triggerEffects = new List<EffectNode>();
            var hasTrigger = power.Trigger == state.PendingPowerTrigger;
            var lifecycleEffects = power.Lifecycle
                .Where(subscription => subscription.Trigger == state.PendingPowerTrigger)
                .Select(subscription => subscription.Effect).ToArray();
            var skipTrigger = (hasTrigger || lifecycleEffects.Length > 0)
                && owner.SkipNextPowerTriggers.Remove(powerId);
            if (hasTrigger && !skipTrigger)
            {
                if (power.TriggerEffect is { } triggerPower)
                    triggerEffects.Add(new ApplyPowerEffect(triggerPower, amount, TargetSelector.Self));
                if (power.TriggerRuleEffect is { } triggerRule) triggerEffects.Add(triggerRule);
                if (power.DecrementOnTrigger) triggerEffects.Add(new DecrementPowerEffect(powerId));
            }
            if (!skipTrigger)
                triggerEffects.AddRange(lifecycleEffects);
            if (power.RemoveAfterOwnerTurnEnd && IsOwnerTurnEnd(owner, state.PendingPowerTrigger))
                triggerEffects.Add(new ExpirePowerEffect(powerId));
            if (power.RemoveAfterOpponentTurnEnd && IsOpponentTurnEnd(owner, state.PendingPowerTrigger))
                triggerEffects.Add(new ExpirePowerEffect(powerId));
            if (triggerEffects.Count > 0)
                EffectExecutor.Execute(session, owner, new SequenceEffect(triggerEffects));
        }
        if (state.PendingChoice is not null || state.PendingCardSelection is not null) return false;
        state.PendingPowerQueue = null;
        state.PendingPowerOwner = null;
        state.PendingPowerIndex = 0;
        return true;
    }

    private static bool IsOwnerTurnEnd(EntityState owner, PowerTrigger trigger) => trigger ==
        (owner.IsEnemy ? PowerTrigger.AfterEnemyTurnEnd : PowerTrigger.AfterPlayerTurnEnd);

    private static bool IsOpponentTurnEnd(EntityState owner, PowerTrigger trigger) => trigger ==
        (owner.IsEnemy ? PowerTrigger.AfterPlayerTurnEnd : PowerTrigger.AfterEnemyTurnEnd);

    private static bool RunFlushHooks(GameSession session)
    {
        var state = session.State;
        var stage = state.EndTurnStage;
        while (state.FlushHookIndex < state.Definition.Hooks.Count
            && state.PendingChoice is null && state.PendingCardSelection is null)
        {
            var hook = state.Definition.Hooks[state.FlushHookIndex++];
            var context = new RuleHookContext(session, hook.Id);
            var effect = stage switch
            {
                EndTurnResolutionStage.BeforeFlushHooks => hook.BeforeFlush(context, state.Player.Id),
                EndTurnResolutionStage.BeforeFlushLateHooks => hook.BeforeFlushLate(context, state.Player.Id,
                    state.PreFlushShouldFlush),
                EndTurnResolutionStage.AfterFlushHooks => hook.AfterFlush(context, state.Player.Id,
                    state.FlushedCards, state.RetainedCards),
                _ => throw new InvalidOperationException($"'{stage}' is not a flush hook stage.")
            };
            if (effect is not null) EffectExecutor.Execute(session, state.Player.Creature, effect);
        }
        if (state.PendingChoice is not null || state.PendingCardSelection is not null) return false;

        state.FlushHookIndex = 0;
        if (stage == EndTurnResolutionStage.BeforeFlushHooks)
            state.PreFlushShouldFlush = ShouldFlush(session);
        state.EndTurnStage = stage switch
        {
            EndTurnResolutionStage.BeforeFlushHooks => EndTurnResolutionStage.BeforeFlushLateHooks,
            EndTurnResolutionStage.BeforeFlushLateHooks => EndTurnResolutionStage.FlushHand,
            EndTurnResolutionStage.AfterFlushHooks => EndTurnResolutionStage.PlayerTurnEndPowers,
            _ => throw new InvalidOperationException($"'{stage}' is not a flush hook stage.")
        };
        return true;
    }

    private static void FlushPlayerHand(GameSession session)
    {
        var state = session.State;
        var shouldFlush = state.PreFlushShouldFlush;
        var flushed = new List<CardInstance>();
        var retained = new List<CardInstance>();
        foreach (var card in state.Player.MutableHand)
        {
            var hasRetainKeyword = (state.Definition.Cards[card.CardId].ResolveKeywords(card) & CardKeywords.Retain) != 0;
            if (!shouldFlush || hasRetainKeyword || state.TemporarilyRetainedCards.Contains(card.InstanceId))
                retained.Add(card);
            else
                flushed.Add(card);
        }

        state.Player.MutableHand.Clear();
        state.Player.MutableHand.AddRange(retained);
        state.Player.MutableDiscardPile.AddRange(flushed);
        state.Player.MutableDiscardPile.AddRange(state.Player.MutablePlayPile);
        state.Player.MutablePlayPile.Clear();
        state.FlushedCards = Array.AsReadOnly(flushed.ToArray());
        state.RetainedCards = Array.AsReadOnly(retained.ToArray());
        state.TemporarilyRetainedCards.Clear();
        foreach (var card in flushed) state.Emit(session, "card_moved", state.Player.Id, card.InstanceId, 1);
        state.EndTurnStage = EndTurnResolutionStage.AfterFlushHooks;
        state.FlushHookIndex = 0;
    }

    private static bool ShouldClearBlock(GameSession session, EntityState creature)
    {
        var state = session.State;
        if (creature.Powers.Any(power => power.Value > 0
            && state.Definition.Powers[power.Key].PreventsBlockClear))
            return false;
        return state.Definition.Hooks.All(hook => hook.ShouldClearBlock(
            new RuleHookContext(session, hook.Id), creature.Id));
    }

    private static bool ShouldFlush(GameSession session) => session.State.Definition.Hooks.All(hook =>
        hook.ShouldFlush(new RuleHookContext(session, hook.Id), session.State.Player.Id));
}
