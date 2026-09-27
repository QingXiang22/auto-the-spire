namespace CoreTheSpire;

public sealed record CombatEnemySpawn(string EnemyId, int? Hp = null, string? InstanceId = null,
    ContentId? StartingActionOverride = null);

public sealed record EnemyCombatObservation(string Id, int Hp, int MaxHp, int Block, int Strength,
    IntentKind Intent, int IntentDamage, IReadOnlyDictionary<ContentId, int> Powers);

public sealed class CombatEnvironment(GameDefinition definition)
{
    public GameSession Start(uint seed, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, string enemyId, int? enemyHp = null, IEnumerable<ContentId>? drawPile = null)
        => Start(new RandomStreams(seed), playerId, playerHp, playerMaxHp, hand, enemyId, enemyHp, drawPile);

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
                StartingActionOverride: selection.StartingActionOverride)).ToArray());
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
        var session = StartWithInstances(run.Random, run.Character.Id.ToString(), run.Hp, run.MaxHp,
            shuffledDeck.Take(5), spawns, shuffledDeck.Skip(5));
        session.RunOwner = runSession;
        run.ActiveCombat = session;
        foreach (var hook in definition.RunHooks)
            hook.AfterCombatStarted(new RunHookContext(run, hook.Id));
        session.State.Emit(session, "combat_started", session.State.Player.Id, session.State.Player.Id, 0);
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
        IEnumerable<ContentId> hand, string enemyId, int? enemyHp, IEnumerable<ContentId>? drawPile)
    {
        return Start(rng, playerId, playerHp, playerMaxHp, hand,
            [new CombatEnemySpawn(enemyId, enemyHp)], drawPile);
    }

    public GameSession StartEncounter(uint seed, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, IEnumerable<CombatEnemySpawn> enemies, IEnumerable<ContentId>? drawPile = null)
        => Start(new RandomStreams(seed), playerId, playerHp, playerMaxHp, hand, enemies, drawPile);

    public GameSession StartEncounter(uint seed, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, ContentId encounterId)
    {
        if (!definition.Encounters.TryGetValue(encounterId, out var encounter))
            throw new KeyNotFoundException($"Unknown encounter '{encounterId}'.");
        var random = new RandomStreams(seed);
        var selections = encounter.SelectEnemies(random.Get("encounter-enemies"));
        return Start(random, playerId, playerHp, playerMaxHp, hand,
            selections.Select(selection => new CombatEnemySpawn(selection.EnemyId.ToString(),
                StartingActionOverride: selection.StartingActionOverride)), drawPile: null);
    }

    private GameSession Start(RandomStreams rng, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, IEnumerable<CombatEnemySpawn> enemies, IEnumerable<ContentId>? drawPile)
    {
        var handCards = hand.ToArray();
        var drawCards = drawPile?.ToArray() ?? [];
        var instanceIndex = 0;
        var handInstances = handCards.Select(card => new CardInstance($"fixture-card-{instanceIndex++}", card)).ToArray();
        var drawInstances = drawCards.Select(card => new CardInstance($"fixture-card-{instanceIndex++}", card)).ToArray();
        var session = StartWithInstances(rng, playerId, playerHp, playerMaxHp, handInstances, enemies, drawInstances);
        session.State.Emit(session, "combat_started", session.State.Player.Id, session.State.Player.Id, 0);
        EffectExecutor.Resume(session);
        return session;
    }

    private GameSession StartWithInstances(RandomStreams rng, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<CardInstance> hand, IEnumerable<CombatEnemySpawn> enemies, IEnumerable<CardInstance>? drawPile)
    {
        ArgumentNullException.ThrowIfNull(enemies);
        var enemySpawns = enemies.ToArray();
        if (enemySpawns.Length == 0) throw new ArgumentException("A combat encounter needs at least one enemy.", nameof(enemies));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playerMaxHp);
        if (playerHp < 0 || playerHp > playerMaxHp) throw new ArgumentOutOfRangeException(nameof(playerHp));
        var prepared = new (CombatEnemySpawn Spawn, EnemyDefinition Definition, string InstanceId)[enemySpawns.Length];
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < enemySpawns.Length; i++)
        {
            var spawn = enemySpawns[i];
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
            prepared[i] = (spawn, enemyDefinition, entityId);
        }

        var combatEnemies = new List<EnemyCombatState>(enemySpawns.Length);
        var chosenEnemyHp = new HashSet<int>();
        foreach (var (spawn, enemyDefinition, entityId) in prepared)
        {
            var hp = spawn.Hp ?? ChooseUniqueEnemyHp(enemyDefinition, chosenEnemyHp, rng.Get("encounter-hp"));
            chosenEnemyHp.Add(hp);
            var actionIndex = spawn.StartingActionOverride is { } actionId
                ? enemyDefinition.GetActionIndex(actionId)
                : enemyDefinition.ResolveStartingActionIndex(rng.Get("enemy-ai"));
            var enemy = new EnemyCombatState(new EntityState(entityId, true, hp, hp), enemyDefinition)
            {
                ActionIndex = actionIndex
            };
            enemy.SelectAction(enemyDefinition.Actions[actionIndex].Id);
            combatEnemies.Add(enemy);
        }
        var handInstances = hand.ToArray();
        var drawInstances = drawPile?.ToArray() ?? [];
        var allCardInstanceIds = handInstances.Concat(drawInstances).Select(card => card.InstanceId).ToArray();
        if (allCardInstanceIds.Distinct(StringComparer.Ordinal).Count() != allCardInstanceIds.Length)
            throw new ArgumentException("Combat card instance IDs must be unique.", nameof(hand));
        foreach (var card in handInstances.Concat(drawInstances))
            if (!definition.Cards.ContainsKey(card.CardId))
                throw new KeyNotFoundException($"Unknown combat card '{card.CardId}'.");

        var state = new GameState(definition,
            new PlayerCombatState(playerId, playerHp, playerMaxHp, handInstances, drawInstances),
            combatEnemies);
        return new GameSession(state, rng);
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
        var intentDamage = next.Intent == IntentKind.Attack
            ? state.CalculateDamage(session, primary.Creature, state.Player.Creature, next.IntentDamage)
            : 0;
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
            });
    }

    private static EnemyCombatObservation ObserveEnemy(GameSession session, EnemyCombatState enemy)
    {
        var state = session.State;
        var action = enemy.Definition.Actions[Math.Min(enemy.ActionIndex, enemy.Definition.Actions.Count - 1)];
        var damage = action.Intent == IntentKind.Attack
            ? state.CalculateDamage(session, enemy.Creature, state.Player.Creature, action.IntentDamage)
            : 0;
        return new EnemyCombatObservation(enemy.Creature.Id, enemy.Creature.Hp, enemy.Creature.MaxHp,
            enemy.Creature.Block, enemy.Creature.Strength, action.Intent, damage,
            new Dictionary<ContentId, int>(enemy.Creature.Powers));
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
        var enemies = state.Enemies.Where(enemy => enemy.Creature.Hp > 0).ToArray();
        for (var i = 0; i < state.Player.Hand.Count; i++)
        {
            var instance = state.Player.HandInstances[i];
            var card = definition.Cards[instance.CardId];
            if ((card.Keywords & CardKeywords.Unplayable) != 0) continue;
            if (!card.HasEnergyXCost && card.ResolveCost(instance) > state.Player.Energy) continue;
            if (card.Target == TargetSelector.Self)
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
                var potion = definition.Potions[runOwner.State.Potions[potionIndex]];
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
                    chooseCard.CardInstanceId, cardChoice.Effect.DestinationPosition))
                throw new InvalidOperationException("The selected card is no longer in its source pile.");
            state.Emit(session, "card_choice", state.Player.Id, chooseCard.CardInstanceId, 1);
            if (cardChoice.Effect.OnSelected is { } onSelected)
                state.PendingEffects.Add((cardChoice.Owner, onSelected, cardChoice.Target, chosenCard));
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
                        state.PendingEffects.Add((cardSetChoice.Owner, onSelected,
                            cardSetChoice.Target, cardSetChoice.Selected[i]));
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
            state.PendingChoiceOwner = null;
            state.PendingChoiceTarget = null;
            state.PendingChoiceSelectedCard = null;
            state.Emit(session, "choice", state.Player.Id, state.Player.Id, choose.OptionIndex);
            if (pending.Options[choose.OptionIndex].Effect is { } selectedEffect)
                EffectExecutor.Execute(session, choiceOwner, selectedEffect, choiceTarget, choiceSelectedCard);
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
            var potionId = run.Potions[usePotion.PotionIndex];
            var potion = definition.Potions[potionId];
            var target = usePotion.TargetId == state.Player.Id ? state.Player.Creature
                : state.Enemies.Select(enemy => enemy.Creature).FirstOrDefault(enemy => enemy.Id == usePotion.TargetId);
            run.Potions.RemoveAt(usePotion.PotionIndex);
            state.Emit(session, "potion_used", state.Player.Id, state.Player.Id, usePotion.PotionIndex);
            EffectExecutor.Execute(session, state.Player.Creature, potion.Effect, target);
        }
        else if (action is PlayCardAction play)
        {
            var cardInstance = state.Player.HandInstances[play.HandIndex];
            var card = definition.Cards[cardInstance.CardId];
            state.Player.MutableHand.RemoveAt(play.HandIndex);
            var xEnergy = card.HasEnergyXCost ? state.Player.Energy : 0;
            state.Player.Energy = card.HasEnergyXCost ? 0 : state.Player.Energy - card.ResolveCost(cardInstance);
            state.Player.MutablePlayPile.Add(cardInstance);
            state.PendingCardPlay = new PendingCardPlay(cardInstance, xEnergy);
            var selectedTarget = play.TargetId == state.Player.Id ? state.Player.Creature
                : state.Enemies.Select(enemy => enemy.Creature).FirstOrDefault(enemy => enemy.Id == play.TargetId);
            state.Emit(session, "play", state.Player.Id, play.TargetId ?? state.FirstLivingEnemy?.Id ?? state.Enemy.Id, 0);
            EffectExecutor.Execute(session, state.Player.Creature, card.ResolveEffect(cardInstance), selectedTarget);
            if (state.PendingChoice is null && state.PendingCardSelection is null) FinalizePlayedCard(session);
        }
        else if (action is EndTurnAction)
        {
            state.IsPlayerTurn = false;
            state.EndTurnStage = EndTurnResolutionStage.PlayerHandCleanup;
            ContinueEndTurn(session);
        }
        return new StepResult(ObserveSession(session), state.Events.Skip(firstEvent).ToArray(),
            state.PendingChoice is not null || state.PendingCardSelection is not null);
    }

    private static void FinalizePlayedCard(GameSession session)
    {
        var state = session.State;
        if (state.PendingCardPlay is not { } cardPlay) return;
        var cardInstance = cardPlay.Card;
        var card = state.Definition.Cards[cardInstance.CardId];
        if ((card.Keywords & CardKeywords.Exhaust) != 0)
        {
            state.Player.MutablePlayPile.Remove(cardInstance);
            state.Player.MutableExhaustPile.Add(cardInstance);
            state.PendingCardPlay = null;
            EffectExecutor.QueueCardExhaustion(session, state.Player.Creature, cardInstance);
            EffectExecutor.Resume(session);
            return;
        }
        state.PendingCardPlay = null;
    }

    private static void ResumeChoiceContinuation(GameSession session)
    {
        var state = session.State;
        if (state.PendingChoice is not null || state.PendingCardSelection is not null) return;
        if (state.EndTurnStage != EndTurnResolutionStage.None) ContinueEndTurn(session);
        else FinalizePlayedCard(session);
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
                        (state.Definition.Cards[card.CardId].Keywords & CardKeywords.Ethereal) != 0
                        && state.Definition.Hooks.All(hook => hook.ShouldEtherealTrigger(
                            new RuleHookContext(session, hook.Id), card)));
                    if (etherealCard is not null)
                    {
                        state.Player.MutableHand.Remove(etherealCard);
                        state.Player.MutableExhaustPile.Add(etherealCard);
                        EffectExecutor.QueueCardExhaustion(session, state.Player.Creature, etherealCard);
                        EffectExecutor.Resume(session);
                        break;
                    }
                    state.EndTurnStage = EndTurnResolutionStage.BeforeFlushHooks;
                    state.FlushHookIndex = 0;
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
                    state.EndTurnStage = EndTurnResolutionStage.EnemyAction;
                    BeginPowerQueue(state, state.Player.Creature, PowerTrigger.AfterPlayerTurnEnd);
                    break;
                case EndTurnResolutionStage.EnemyAction:
                    if (state.Player.Creature.Hp <= 0)
                    {
                        state.EndTurnStage = EndTurnResolutionStage.StartPlayerTurn;
                        break;
                    }
                    while (state.CurrentEnemyIndex < state.Enemies.Count
                        && state.Enemies[state.CurrentEnemyIndex].Creature.Hp <= 0)
                        state.CurrentEnemyIndex++;
                    if (state.CurrentEnemyIndex >= state.Enemies.Count)
                    {
                        state.CurrentEnemyIndex = 0;
                        state.EndTurnStage = EndTurnResolutionStage.EnemyTurnEndPowers;
                        break;
                    }
                    var actingEnemy = state.Enemies[state.CurrentEnemyIndex];
                    actingEnemy.Creature.Block = 0;
                    var action = actingEnemy.Definition.Actions[actingEnemy.ActionIndex];
                    state.Emit(session, "enemy_action", actingEnemy.Creature.Id, state.Player.Id, (int)action.Intent);
                    state.EndTurnStage = EndTurnResolutionStage.ApplyEnemyActionPower;
                    EffectExecutor.Execute(session, actingEnemy.Creature, action.Effect, state.Player.Creature);
                    break;
                case EndTurnResolutionStage.ApplyEnemyActionPower:
                    var currentEnemy = state.Enemies[state.CurrentEnemyIndex];
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
                    while (state.CurrentEnemyIndex < state.Enemies.Count
                        && state.Enemies[state.CurrentEnemyIndex].Creature.Hp <= 0)
                        state.CurrentEnemyIndex++;
                    if (state.CurrentEnemyIndex >= state.Enemies.Count)
                    {
                        state.CurrentEnemyIndex = 0;
                        state.EndTurnStage = EndTurnResolutionStage.PlayerEnemySideEndPowers;
                        break;
                    }
                    var endedEnemy = state.Enemies[state.CurrentEnemyIndex++];
                    BeginPowerQueue(state, endedEnemy.Creature, PowerTrigger.AfterEnemyTurnEnd);
                    break;
                case EndTurnResolutionStage.PlayerEnemySideEndPowers:
                    state.EndTurnStage = EndTurnResolutionStage.AdvanceEnemyAction;
                    BeginPowerQueue(state, state.Player.Creature, PowerTrigger.AfterEnemyTurnEnd);
                    break;
                case EndTurnResolutionStage.AdvanceEnemyAction:
                    foreach (var enemy in state.Enemies)
                    {
                        if (enemy.Creature.Hp <= 0) continue;
                        var currentAction = enemy.Definition.Actions[enemy.ActionIndex];
                        enemy.ActionIndex = enemy.Definition.ResolveNextActionIndex(currentAction.Id,
                            enemy.LastActionId, enemy.ConsecutiveActionCount, session.Random.Get("enemy-ai"));
                        enemy.SelectAction(enemy.Definition.Actions[enemy.ActionIndex].Id);
                    }
                    state.CurrentEnemyIndex = 0;
                    state.EndTurnStage = EndTurnResolutionStage.StartPlayerTurn;
                    break;
                case EndTurnResolutionStage.StartPlayerTurn:
                    if (!state.IsTerminal)
                    {
                        state.Turn++;
                        state.Player.Energy = 3;
                        if (state.Definition.Hooks.All(hook => hook.ShouldClearBlock(
                            new RuleHookContext(session, hook.Id), state.Player.Id)))
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
                    state.EndTurnStage = EndTurnResolutionStage.None;
                    if (!state.IsTerminal) state.Draw(session, 5);
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
            .Where(pair => state.Definition.Powers[pair.Key].Trigger == trigger)
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
            if (owner.SkipNextPowerTriggers.Remove(powerId)) continue;
            var power = state.Definition.Powers[powerId];
            var triggerEffects = new List<EffectNode>();
            if (power.TriggerEffect is { } triggerPower)
                triggerEffects.Add(new ApplyPowerEffect(triggerPower, amount, TargetSelector.Self));
            if (power.TriggerRuleEffect is { } triggerRule) triggerEffects.Add(triggerRule);
            if (power.DecrementOnTrigger) triggerEffects.Add(new DecrementPowerEffect(powerId));
            if (triggerEffects.Count > 0)
                EffectExecutor.Execute(session, owner, new SequenceEffect(triggerEffects));
        }
        if (state.PendingChoice is not null || state.PendingCardSelection is not null) return false;
        state.PendingPowerQueue = null;
        state.PendingPowerOwner = null;
        state.PendingPowerIndex = 0;
        return true;
    }

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
            var hasRetainKeyword = (state.Definition.Cards[card.CardId].Keywords & CardKeywords.Retain) != 0;
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

    private static bool ShouldFlush(GameSession session) => session.State.Definition.Hooks.All(hook =>
        hook.ShouldFlush(new RuleHookContext(session, hook.Id), session.State.Player.Id));
}
