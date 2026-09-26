namespace CoreTheSpire;

public sealed record CombatEnemySpawn(string EnemyId, int? Hp = null, string? InstanceId = null);

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
        return StartForRun(runSession, encounter.EnemyIds
            .Select(enemyId => new CombatEnemySpawn(enemyId.ToString())).ToArray());
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
        var run = runSession.State;
        if (run.Status != RunStatus.InProgress || run.Phase != RunPhase.ResolveRoom
            || run.CurrentRoom is not (RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss))
            throw new InvalidOperationException("Combat can only start in an active Monster, Elite, or Boss room.");
        if (run.ActiveCombat is not null)
            throw new InvalidOperationException("This run already has an active combat session.");
        foreach (var spawn in spawns)
        {
            var enemyDefinition = FindEnemy(spawn.EnemyId);
            if (enemyDefinition.Actions.Count == 0)
                throw new InvalidOperationException($"Enemy '{spawn.EnemyId}' has no actions.");
            if (spawn.Hp is < 1 || spawn.Hp > enemyDefinition.MaxHp)
                throw new ArgumentOutOfRangeException(nameof(spawns));
        }
        foreach (var cardId in run.Deck)
            if (!definition.Cards.ContainsKey(cardId))
                throw new InvalidOperationException($"Run card '{cardId}' is not registered in this combat definition.");

        var shuffledDeck = run.Deck.ToList();
        run.Random.Get("shuffle").Shuffle(shuffledDeck);
        var session = Start(run.Random, run.Character.Id.ToString(), run.Hp, run.MaxHp,
            shuffledDeck.Take(5), spawns, shuffledDeck.Skip(5));
        session.RunOwner = runSession;
        run.ActiveCombat = session;
        return session;
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
        return StartEncounter(seed, playerId, playerHp, playerMaxHp, hand,
            encounter.EnemyIds.Select(enemyId => new CombatEnemySpawn(enemyId.ToString())));
    }

    private GameSession Start(RandomStreams rng, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, IEnumerable<CombatEnemySpawn> enemies, IEnumerable<ContentId>? drawPile)
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
            prepared[i] = (spawn, enemyDefinition, entityId);
        }

        var combatEnemies = new List<EnemyCombatState>(enemySpawns.Length);
        foreach (var (spawn, enemyDefinition, entityId) in prepared)
        {
            var hp = spawn.Hp ?? rng.Get("encounter-hp").NextInt(enemyDefinition.MinHp, enemyDefinition.MaxHp + 1);
            combatEnemies.Add(new EnemyCombatState(new EntityState(entityId, true, hp, hp), enemyDefinition)
            {
                ActionIndex = enemyDefinition.StartingActionIndex
            });
        }
        var state = new GameState(definition,
            new PlayerCombatState(playerId, playerHp, playerMaxHp, hand, drawPile),
            combatEnemies);
        return new GameSession(state, rng);
    }

    private EnemyDefinition FindEnemy(string enemyId) => definition.Enemies.Values
        .SingleOrDefault(e => e.Id.Value == enemyId || e.Id.ToString() == enemyId)
        ?? throw new KeyNotFoundException($"Unknown enemy '{enemyId}'.");

    public CombatObservation Observe(GameSession session) => Observe(session.State);

    private CombatObservation Observe(GameState state)
    {
        var primary = state.FirstLivingEnemy is { } living
            ? state.Enemies.Single(enemy => ReferenceEquals(enemy.Creature, living))
            : state.Enemies[0];
        var next = primary.Definition.Actions[Math.Min(primary.ActionIndex, primary.Definition.Actions.Count - 1)];
        var intentDamage = next.Intent == IntentKind.Attack
            ? state.CalculateDamage(primary.Creature, state.Player.Creature, next.IntentDamage)
            : 0;
        return new CombatObservation(state.Turn, state.Player.Creature.Hp, state.Player.Creature.MaxHp,
            state.Player.Creature.Block, state.Player.Energy, state.Player.Hand.ToArray(), primary.Creature.Hp,
            primary.Creature.MaxHp, primary.Creature.Block, primary.Creature.Strength, next.Intent, intentDamage,
            new Dictionary<ContentId, int>(primary.Creature.Powers), state.IsTerminal,
            state.PendingChoice is { } choice ? new PendingChoiceView(choice.Prompt, choice.Options.Select(option => option.Label).ToArray()) : null,
            Array.AsReadOnly(state.Enemies.Select(enemy => ObserveEnemy(state, enemy)).ToArray()));
    }

    private static EnemyCombatObservation ObserveEnemy(GameState state, EnemyCombatState enemy)
    {
        var action = enemy.Definition.Actions[Math.Min(enemy.ActionIndex, enemy.Definition.Actions.Count - 1)];
        var damage = action.Intent == IntentKind.Attack
            ? state.CalculateDamage(enemy.Creature, state.Player.Creature, action.IntentDamage)
            : 0;
        return new EnemyCombatObservation(enemy.Creature.Id, enemy.Creature.Hp, enemy.Creature.MaxHp,
            enemy.Creature.Block, enemy.Creature.Strength, action.Intent, damage,
            new Dictionary<ContentId, int>(enemy.Creature.Powers));
    }

    public IReadOnlyList<PlayerAction> LegalActions(GameSession session) => LegalActions(session.State);

    private IReadOnlyList<PlayerAction> LegalActions(GameState state)
    {
        if (state.IsTerminal) return [];
        if (state.PendingChoice is { } pending)
            return Enumerable.Range(0, pending.Options.Count).Select(i => (PlayerAction)new ChooseOptionAction(i)).ToArray();
        if (!state.IsPlayerTurn) return [];
        var actions = new List<PlayerAction> { new EndTurnAction() };
        var enemies = state.Enemies.Where(enemy => enemy.Creature.Hp > 0).ToArray();
        for (var i = 0; i < state.Player.Hand.Count; i++)
        {
            var card = definition.Cards[state.Player.Hand[i]];
            if (card.Cost > state.Player.Energy) continue;
            if (card.Target == TargetSelector.Self)
                actions.Add(new PlayCardAction(i, state.Player.Id));
            else if (card.Target == TargetSelector.SelectedTarget)
                actions.AddRange(enemies.Select(enemy => new PlayCardAction(i, enemy.Creature.Id)));
            else
                actions.Add(new PlayCardAction(i, enemies.FirstOrDefault()?.Creature.Id));
        }
        return actions;
    }

    public StepResult Step(GameSession session, PlayerAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var state = session.State;
        var firstEvent = state.Events.Count;
        if (state.PendingChoice is { } pending)
        {
            if (action is not ChooseOptionAction choose || choose.OptionIndex < 0 || choose.OptionIndex >= pending.Options.Count)
                throw new InvalidOperationException("A valid choice option is required to resume the effect.");
            state.PendingChoice = null;
            var choiceOwner = state.PendingChoiceOwner ?? state.Player.Creature;
            var choiceTarget = state.PendingChoiceTarget;
            state.PendingChoiceOwner = null;
            state.PendingChoiceTarget = null;
            state.Emit(session, "choice", state.Player.Id, state.Player.Id, choose.OptionIndex);
            if (pending.Options[choose.OptionIndex].Effect is { } selectedEffect)
                EffectExecutor.Execute(session, choiceOwner, selectedEffect, choiceTarget);
            EffectExecutor.Resume(session);
            if (state.PendingChoice is null)
            {
                if (state.EndTurnStage != EndTurnResolutionStage.None) ContinueEndTurn(session);
                else FinalizePlayedCard(state);
            }
        }
        else if (!LegalActions(state).Any(a => a == action))
        {
            throw new InvalidOperationException("Action is not legal in the current state.");
        }

        if (action is PlayCardAction play)
        {
            var cardId = state.Player.Hand[play.HandIndex];
            var card = definition.Cards[cardId];
            state.Player.MutableHand.RemoveAt(play.HandIndex);
            state.Player.Energy -= card.Cost;
            state.Player.MutablePlayPile.Add(cardId);
            state.PendingPlayedCard = cardId;
            var selectedTarget = play.TargetId == state.Player.Id ? state.Player.Creature
                : state.Enemies.Select(enemy => enemy.Creature).FirstOrDefault(enemy => enemy.Id == play.TargetId);
            state.Emit(session, "play", state.Player.Id, play.TargetId ?? state.FirstLivingEnemy?.Id ?? state.Enemy.Id, 0);
            EffectExecutor.Execute(session, state.Player.Creature, card.Effect, selectedTarget);
            if (state.PendingChoice is null) FinalizePlayedCard(state);
        }
        else if (action is EndTurnAction)
        {
            state.Player.MutableDiscardPile.AddRange(state.Player.Hand);
            state.Player.MutableHand.Clear();
            state.Player.MutableDiscardPile.AddRange(state.Player.PlayPile);
            state.Player.MutablePlayPile.Clear();
            state.IsPlayerTurn = false;
            state.EndTurnStage = EndTurnResolutionStage.PlayerTurnEndPowers;
            ContinueEndTurn(session);
        }
        return new StepResult(Observe(state), state.Events.Skip(firstEvent).ToArray(), state.PendingChoice is not null);
    }

    private static void FinalizePlayedCard(GameState state)
    {
        if (state.PendingPlayedCard is not { } cardId) return;
        var card = state.Definition.Cards[cardId];
        if (card.Exhaust)
        {
            state.Player.MutablePlayPile.Remove(cardId);
            state.Player.MutableExhaustPile.Add(cardId);
        }
        state.PendingPlayedCard = null;
    }

    private static void ContinueEndTurn(GameSession session)
    {
        var state = session.State;
        while (state.PendingChoice is null)
        {
            if (state.PendingPowerQueue is not null && !RunPendingPowerQueue(session)) return;
            switch (state.EndTurnStage)
            {
                case EndTurnResolutionStage.None:
                    return;
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
                        var currentAction = enemy.Definition.Actions[enemy.ActionIndex];
                        enemy.ActionIndex = currentAction.NextActionId is { } nextActionId
                            ? enemy.Definition.GetActionIndex(nextActionId)
                            : currentAction.Id == enemy.Definition.Actions[^1].Id && enemy.Definition.LoopLastAction
                                ? enemy.Definition.Actions.Count - 1
                                : Math.Min(enemy.ActionIndex + 1, enemy.Definition.Actions.Count - 1);
                    }
                    state.CurrentEnemyIndex = 0;
                    state.EndTurnStage = EndTurnResolutionStage.StartPlayerTurn;
                    break;
                case EndTurnResolutionStage.StartPlayerTurn:
                    state.EndTurnStage = EndTurnResolutionStage.None;
                    if (!state.IsTerminal)
                    {
                        state.Turn++;
                        state.Player.Energy = 3;
                        state.Player.Creature.Block = 0;
                        state.IsPlayerTurn = true;
                        state.Emit(session, "player_turn_start", state.Player.Id, state.Player.Id, state.Turn);
                        state.Draw(session, 5);
                    }
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
        while (state.PendingPowerIndex < queue.Count && state.PendingChoice is null)
        {
            var powerId = queue[state.PendingPowerIndex++];
            if (!owner.Powers.TryGetValue(powerId, out var amount)) continue;
            if (owner.SkipNextPowerTriggers.Remove(powerId)) continue;
            var power = state.Definition.Powers[powerId];
            if (power.TriggerEffect is { } triggerEffect)
                EffectExecutor.Execute(session, owner, new ApplyPowerEffect(triggerEffect, amount, TargetSelector.Self));
            if (power.DecrementOnTrigger)
            {
                if (amount <= 1) owner.MutablePowers.Remove(powerId);
                else owner.MutablePowers[powerId] = amount - 1;
            }
        }
        if (state.PendingChoice is not null) return false;
        state.PendingPowerQueue = null;
        state.PendingPowerOwner = null;
        state.PendingPowerIndex = 0;
        return true;
    }
}
