namespace CoreTheSpire;

public sealed class CombatEnvironment(GameDefinition definition)
{
    public GameSession Start(uint seed, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, string enemyId, int? enemyHp = null, IEnumerable<ContentId>? drawPile = null)
        => Start(new RandomStreams(seed), playerId, playerHp, playerMaxHp, hand, enemyId, enemyHp, drawPile);

    /// <summary>Starts combat from an active run room, using the run deck and its shared RNG streams.</summary>
    public GameSession StartForRun(RunSession runSession, string enemyId, int? enemyHp = null)
    {
        ArgumentNullException.ThrowIfNull(runSession);
        var run = runSession.State;
        if (run.Status != RunStatus.InProgress || run.Phase != RunPhase.ResolveRoom
            || run.CurrentRoom is not (RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss))
            throw new InvalidOperationException("Combat can only start in an active Monster, Elite, or Boss room.");
        if (run.ActiveCombat is not null)
            throw new InvalidOperationException("This run already has an active combat session.");
        var enemyDefinition = definition.Enemies.Values.SingleOrDefault(e => e.Id.Value == enemyId || e.Id.ToString() == enemyId)
            ?? throw new KeyNotFoundException($"Unknown enemy '{enemyId}'.");
        if (enemyDefinition.Actions.Count == 0)
            throw new InvalidOperationException($"Enemy '{enemyId}' has no actions.");
        if (enemyHp is < 1 || enemyHp > enemyDefinition.MaxHp)
            throw new ArgumentOutOfRangeException(nameof(enemyHp));
        foreach (var cardId in run.Deck)
            if (!definition.Cards.ContainsKey(cardId))
                throw new InvalidOperationException($"Run card '{cardId}' is not registered in this combat definition.");

        var shuffledDeck = run.Deck.ToList();
        run.Random.Get("shuffle").Shuffle(shuffledDeck);
        var session = Start(run.Random, run.Character.Id.ToString(), run.Hp, run.MaxHp,
            shuffledDeck.Take(5), enemyId, enemyHp, shuffledDeck.Skip(5));
        session.RunOwner = runSession;
        run.ActiveCombat = session;
        return session;
    }

    private GameSession Start(RandomStreams rng, string playerId, int playerHp, int playerMaxHp,
        IEnumerable<ContentId> hand, string enemyId, int? enemyHp, IEnumerable<ContentId>? drawPile)
    {
        var enemyDefinition = definition.Enemies.Values.Single(e => e.Id.Value == enemyId || e.Id.ToString() == enemyId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playerMaxHp);
        if (playerHp < 0 || playerHp > playerMaxHp) throw new ArgumentOutOfRangeException(nameof(playerHp));
        if (enemyDefinition.Actions.Count == 0) throw new InvalidOperationException($"Enemy '{enemyId}' has no actions.");
        var hp = enemyHp ?? rng.Get("encounter-hp").NextInt(enemyDefinition.MinHp, enemyDefinition.MaxHp + 1);
        if (hp < 1 || hp > enemyDefinition.MaxHp) throw new ArgumentOutOfRangeException(nameof(enemyHp));
        var state = new GameState(definition,
            new PlayerCombatState(playerId, playerHp, playerMaxHp, hand, drawPile),
            new EntityState(enemyDefinition.Id.ToString(), true, hp, hp), enemyDefinition);
        return new GameSession(state, rng);
    }

    public CombatObservation Observe(GameSession session) => Observe(session.State);

    private CombatObservation Observe(GameState state)
    {
        var next = state.EnemyDefinition.Actions[Math.Min(state.EnemyActionIndex, state.EnemyDefinition.Actions.Count - 1)];
        var intentDamage = Math.Max(0, next.IntentDamage + state.Enemy.Strength);
        if (state.HasPowerRole(state.Player.Creature, PowerRole.Vulnerable)) intentDamage = (int)MathF.Floor(intentDamage * 1.5f);
        return new CombatObservation(state.Turn, state.Player.Creature.Hp, state.Player.Creature.MaxHp,
            state.Player.Creature.Block, state.Player.Energy, state.Player.Hand.ToArray(), state.Enemy.Hp,
            state.Enemy.MaxHp, state.Enemy.Block, state.Enemy.Strength, next.Intent, intentDamage,
            new Dictionary<ContentId, int>(state.Enemy.Powers), state.IsTerminal,
            state.PendingChoice is { } choice ? new PendingChoiceView(choice.Prompt, choice.Options.Select(option => option.Label).ToArray()) : null);
    }

    public IReadOnlyList<PlayerAction> LegalActions(GameSession session) => LegalActions(session.State);

    private IReadOnlyList<PlayerAction> LegalActions(GameState state)
    {
        if (state.IsTerminal) return [];
        if (state.PendingChoice is { } pending)
            return Enumerable.Range(0, pending.Options.Count).Select(i => (PlayerAction)new ChooseOptionAction(i)).ToArray();
        if (!state.IsPlayerTurn) return [];
        var actions = new List<PlayerAction> { new EndTurnAction() };
        for (var i = 0; i < state.Player.Hand.Count; i++)
        {
            var card = definition.Cards[state.Player.Hand[i]];
            if (card.Cost <= state.Player.Energy)
                actions.Add(new PlayCardAction(i, card.Target == TargetSelector.Self ? state.Player.Id : state.Enemy.Hp > 0 ? state.Enemy.Id : null));
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
            state.Emit(session, "play", state.Player.Id, play.TargetId ?? state.Enemy.Id, 0);
            var selectedTarget = play.TargetId == state.Player.Id ? state.Player.Creature
                : play.TargetId == state.Enemy.Id ? state.Enemy : null;
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
                    state.Enemy.Block = 0;
                    var action = state.EnemyDefinition.Actions[state.EnemyActionIndex];
                    state.Emit(session, "enemy_action", state.Enemy.Id, state.Player.Id, (int)action.Intent);
                    state.EndTurnStage = EndTurnResolutionStage.ApplyEnemyActionPower;
                    EffectExecutor.Execute(session, state.Enemy, action.Effect, state.Player.Creature);
                    break;
                case EndTurnResolutionStage.ApplyEnemyActionPower:
                    var selectedAction = state.EnemyDefinition.Actions[state.EnemyActionIndex];
                    state.EndTurnStage = EndTurnResolutionStage.EnemyTurnEndPowers;
                    if (selectedAction.AppliedPower is { } appliedPower)
                        state.ApplyPower(session, state.Enemy, appliedPower, selectedAction.AppliedPowerAmount, true);
                    break;
                case EndTurnResolutionStage.EnemyTurnEndPowers:
                    state.EndTurnStage = EndTurnResolutionStage.PlayerEnemySideEndPowers;
                    BeginPowerQueue(state, state.Enemy, PowerTrigger.AfterEnemyTurnEnd);
                    break;
                case EndTurnResolutionStage.PlayerEnemySideEndPowers:
                    state.EndTurnStage = EndTurnResolutionStage.AdvanceEnemyAction;
                    BeginPowerQueue(state, state.Player.Creature, PowerTrigger.AfterEnemyTurnEnd);
                    break;
                case EndTurnResolutionStage.AdvanceEnemyAction:
                    var currentAction = state.EnemyDefinition.Actions[state.EnemyActionIndex];
                    state.EnemyActionIndex = currentAction.Id == state.EnemyDefinition.Actions[^1].Id && state.EnemyDefinition.LoopLastAction
                        ? state.EnemyDefinition.Actions.Count - 1
                        : Math.Min(state.EnemyActionIndex + 1, state.EnemyDefinition.Actions.Count - 1);
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
