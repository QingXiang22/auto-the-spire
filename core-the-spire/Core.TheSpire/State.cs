namespace CoreTheSpire;

using System.Collections;

internal sealed class EntityState
{
    private readonly Dictionary<ContentId, int> _powers = [];
    private readonly Dictionary<(ContentId Power, ContentId Variable), int> _powerVariables = [];
    private readonly Dictionary<ContentId, HashSet<string>> _powerAppliers = [];
    private readonly Dictionary<(ContentId Power, string? ScopeId), int> _powerEventAmounts = [];
    private readonly IReadOnlyDictionary<ContentId, int> _powerView;

    public EntityState(string id, bool isEnemy, int hp, int maxHp)
    {
        Id = id;
        IsEnemy = isEnemy;
        Hp = hp;
        MaxHp = maxHp;
        _powerView = new System.Collections.ObjectModel.ReadOnlyDictionary<ContentId, int>(_powers);
    }

    public string Id { get; }
    public bool IsEnemy { get; }
    public int Hp { get; internal set; }
    public int MaxHp { get; internal set; }
    public int Block { get; internal set; }
    public int Strength { get; internal set; }
    public IReadOnlyDictionary<ContentId, int> Powers => _powerView;
    internal Dictionary<ContentId, int> MutablePowers => _powers;
    internal Dictionary<ContentId, HashSet<string>> MutablePowerAppliers => _powerAppliers;
    internal HashSet<ContentId> SkipNextPowerTriggers { get; } = [];
    public int PowerAmount(ContentId id) => _powers.GetValueOrDefault(id);
    internal int PowerVariable(ContentId power, ContentId variable) =>
        _powerVariables.GetValueOrDefault((power, variable));
    internal IReadOnlyDictionary<ContentId, IReadOnlyDictionary<ContentId, int>> PowerVariableSnapshot() =>
        _powerVariables.GroupBy(pair => pair.Key.Power).ToDictionary(
            group => group.Key,
            group => (IReadOnlyDictionary<ContentId, int>)new System.Collections.ObjectModel.ReadOnlyDictionary<ContentId, int>(
                group.ToDictionary(pair => pair.Key.Variable, pair => pair.Value)));
    internal void InitializePowerVariables(PowerDefinition definition)
    {
        foreach (var variable in definition.Variables)
            _powerVariables.TryAdd((definition.Id, variable.Id), variable.InitialValue);
    }
    internal void ModifyPowerVariable(ContentId power, ContentId variable, int amount)
    {
        var key = (power, variable);
        if (!_powerVariables.ContainsKey(key))
            throw new InvalidOperationException($"Power variable '{power}.{variable}' is not active.");
        _powerVariables[key] = checked(_powerVariables[key] + amount);
    }
    internal void ClearPowerVariables(ContentId power)
    {
        foreach (var key in _powerVariables.Keys.Where(key => key.Power == power).ToArray())
            _powerVariables.Remove(key);
    }
    internal void AccumulatePowerEventAmount(ContentId power, string? scopeId, int amount)
    {
        var key = (power, scopeId);
        _powerEventAmounts[key] = checked(_powerEventAmounts.GetValueOrDefault(key) + amount);
    }
    internal int TakePowerEventAmount(ContentId power, string? scopeId)
    {
        var key = (power, scopeId);
        return _powerEventAmounts.Remove(key, out var amount) ? amount : 0;
    }
    internal void ClearPowerEventAmounts(ContentId power)
    {
        foreach (var key in _powerEventAmounts.Keys.Where(key => key.Power == power).ToArray())
            _powerEventAmounts.Remove(key);
    }
}

internal sealed class PlayerCombatState
{
    private readonly List<CardInstance> _hand;
    private readonly List<CardInstance> _drawPile;
    private readonly List<CardInstance> _discardPile = [];
    private readonly List<CardInstance> _exhaustPile = [];
    private readonly List<CardInstance> _playPile = [];
    private readonly CardInstanceIdView _handView;
    private readonly CardInstanceIdView _drawPileView;
    private readonly CardInstanceIdView _discardPileView;
    private readonly CardInstanceIdView _exhaustPileView;
    private readonly CardInstanceIdView _playPileView;
    private readonly IReadOnlyList<CardInstance> _handInstanceView;
    private readonly IReadOnlyList<CardInstance> _drawPileInstanceView;
    private readonly IReadOnlyList<CardInstance> _discardPileInstanceView;
    private readonly IReadOnlyList<CardInstance> _exhaustPileInstanceView;
    private readonly IReadOnlyList<CardInstance> _playPileInstanceView;
    private int _nextGeneratedCardId;

    public PlayerCombatState(string id, int hp, int maxHp, IEnumerable<CardInstance> hand,
        IEnumerable<CardInstance>? drawPile = null, ContentId? characterId = null)
    {
        Creature = new EntityState(id, false, hp, maxHp);
        CharacterId = characterId;
        _hand = [.. hand];
        _drawPile = drawPile is null ? [] : [.. drawPile];
        _handView = new CardInstanceIdView(_hand);
        _drawPileView = new CardInstanceIdView(_drawPile);
        _discardPileView = new CardInstanceIdView(_discardPile);
        _exhaustPileView = new CardInstanceIdView(_exhaustPile);
        _playPileView = new CardInstanceIdView(_playPile);
        _handInstanceView = _hand.AsReadOnly();
        _drawPileInstanceView = _drawPile.AsReadOnly();
        _discardPileInstanceView = _discardPile.AsReadOnly();
        _exhaustPileInstanceView = _exhaustPile.AsReadOnly();
        _playPileInstanceView = _playPile.AsReadOnly();
    }

    public EntityState Creature { get; }
    public ContentId? CharacterId { get; }
    public string Id => Creature.Id;
    public int Energy { get; internal set; } = 3;
    public IReadOnlyList<ContentId> Hand => _handView;
    public IReadOnlyList<ContentId> DrawPile => _drawPileView;
    public IReadOnlyList<ContentId> DiscardPile => _discardPileView;
    public IReadOnlyList<ContentId> ExhaustPile => _exhaustPileView;
    public IReadOnlyList<ContentId> PlayPile => _playPileView;
    public IReadOnlyList<CardInstance> HandInstances => _handInstanceView;
    public IReadOnlyList<CardInstance> DrawPileInstances => _drawPileInstanceView;
    public IReadOnlyList<CardInstance> DiscardPileInstances => _discardPileInstanceView;
    public IReadOnlyList<CardInstance> ExhaustPileInstances => _exhaustPileInstanceView;
    public IReadOnlyList<CardInstance> PlayPileInstances => _playPileInstanceView;
    internal List<CardInstance> MutableHand => _hand;
    internal List<CardInstance> MutableDrawPile => _drawPile;
    internal List<CardInstance> MutableDiscardPile => _discardPile;
    internal List<CardInstance> MutableExhaustPile => _exhaustPile;
    internal List<CardInstance> MutablePlayPile => _playPile;

    public CardInstance CreateGeneratedCard(ContentId cardId, int upgradeLevel = 0) =>
        new($"{Id}/generated-card-{_nextGeneratedCardId++}", cardId, upgradeLevel);
}

internal sealed class CardInstanceIdView(List<CardInstance> cards) : IReadOnlyList<ContentId>
{
    public int Count => cards.Count;
    public ContentId this[int index] => cards[index].CardId;
    public IEnumerator<ContentId> GetEnumerator() => cards.Select(card => card.CardId).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class EnemyCombatState(EntityState creature, EnemyDefinition definition)
{
    public EntityState Creature { get; } = creature;
    public EnemyDefinition Definition { get; } = definition;
    public int ActionIndex { get; set; }
    public ContentId? LastActionId { get; private set; }
    public int ConsecutiveActionCount { get; private set; }
    private readonly Dictionary<ContentId, int> _actionUseCounts = [];
    public IReadOnlyDictionary<ContentId, int> ActionUseCounts => _actionUseCounts;

    public int ActionUseCount(ContentId actionId) => _actionUseCounts.GetValueOrDefault(actionId);

    public void SelectAction(ContentId actionId)
    {
        ConsecutiveActionCount = LastActionId == actionId ? ConsecutiveActionCount + 1 : 1;
        LastActionId = actionId;
        if (Definition.LimitedActionIds.Contains(actionId))
            _actionUseCounts[actionId] = checked(ActionUseCount(actionId) + 1);
    }
}

internal readonly record struct EnemyActionEvaluationContext(GameState state, EnemyCombatState enemy, int enemyIndex)
{
    public int GetMetric(EnemyActionMetric metric)
    {
        var player = state.Player.Creature;
        var creature = enemy.Creature;
        return metric switch
        {
            EnemyActionMetric.Turn => state.Turn,
            EnemyActionMetric.EnemyIndex => enemyIndex,
            EnemyActionMetric.EnemyHp => creature.Hp,
            EnemyActionMetric.EnemyMaxHp => creature.MaxHp,
            EnemyActionMetric.EnemyHpPercent => creature.MaxHp == 0 ? 0 : (int)((long)creature.Hp * 100 / creature.MaxHp),
            EnemyActionMetric.EnemyBlock => creature.Block,
            EnemyActionMetric.PlayerHp => player.Hp,
            EnemyActionMetric.PlayerMaxHp => player.MaxHp,
            EnemyActionMetric.PlayerHpPercent => player.MaxHp == 0 ? 0 : (int)((long)player.Hp * 100 / player.MaxHp),
            EnemyActionMetric.PlayerBlock => player.Block,
            EnemyActionMetric.LivingEnemies => state.Enemies.Count(candidate => candidate.Creature.Hp > 0),
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };
    }

    public int GetPower(EnemyActionSide side, ContentId powerId) => side switch
    {
        EnemyActionSide.Enemy => enemy.Creature.PowerAmount(powerId),
        EnemyActionSide.Player => state.Player.Creature.PowerAmount(powerId),
        _ => throw new ArgumentOutOfRangeException(nameof(side))
    };

    public int GetActionUseCount(ContentId actionId) => enemy.ActionUseCount(actionId);
}

internal sealed class GameState
{
    internal static readonly CardPile[] CombatPiles = Enum.GetValues<CardPile>();
    private readonly List<GameEvent> _events = [];
    private readonly IReadOnlyList<GameEvent> _eventView;
    private readonly ScopedIntState _modInts;
    private long _nextCardPlayExecutionId;
    private int _playerTurnEventStartSequence;

    internal GameState(GameDefinition definition, PlayerCombatState player, IEnumerable<EnemyCombatState> enemies)
    {
        Definition = definition;
        _modInts = new ScopedIntState(definition.StateVariables, StateVariableScope.Combat);
        Player = player;
        Enemies = enemies.ToList();
        if (Enemies.Count == 0) throw new ArgumentException("A combat encounter needs at least one enemy.", nameof(enemies));
        _eventView = _events.AsReadOnly();
    }

    public GameDefinition Definition { get; }
    public PlayerCombatState Player { get; }
    public int PlayerMaxEnergy => Math.Max(0, checked(3 + Player.Creature.Powers.Sum(power =>
        checked(Definition.Powers[power.Key].MaxEnergyPerStack * power.Value))));
    public List<EnemyCombatState> Enemies { get; }
    public EntityState Enemy => Enemies[0].Creature;
    public EnemyDefinition EnemyDefinition => Enemies[0].Definition;
    public int Turn { get; internal set; } = 1;
    internal int LastPlayerCardExhaustTurn { get; private set; }
    internal int LastPlayerHpLossTurn { get; private set; }
    internal int PlayerHpLossCount { get; private set; }
    public int EnemyActionIndex
    {
        get => Enemies[0].ActionIndex;
        internal set => Enemies[0].ActionIndex = value;
    }
    public int CurrentEnemyIndex { get; internal set; }
    public EntityState? FirstLivingEnemy => Enemies.FirstOrDefault(enemy => enemy.Creature.Hp > 0)?.Creature;
    public bool IsPlayerTurn { get; internal set; } = true;
    internal long NextCardPlayExecutionId() => checked(++_nextCardPlayExecutionId);
    public bool IsTerminal => (Player.Creature.Hp <= 0 || Enemies.All(enemy => enemy.Creature.Hp <= 0))
        && PendingChoice is null && PendingCardSelection is null && PendingEffects.Count == 0;
    public ChooseEffect? PendingChoice { get; internal set; }
    internal PendingCardSelectionState? PendingCardSelection { get; set; }
    internal EntityState? PendingChoiceOwner { get; set; }
    internal EntityState? PendingChoiceTarget { get; set; }
    internal CardInstance? PendingChoiceSelectedCard { get; set; }
    internal EffectCountScope? PendingChoiceCounts { get; set; }
    internal List<CardPlayFrame> CardPlayFrames { get; } = [];
    internal CardPlayHistory CardPlayHistory { get; } = new();
    internal CardPlayFrame? CurrentCardPlay => CardPlayFrames.Count == 0 ? null : CardPlayFrames[^1];
    internal CardInstance CardInPlay(CardPlayFrame frame) =>
        Player.PlayPileInstances.FirstOrDefault(card => card.InstanceId == frame.Card.InstanceId) ?? frame.Card;
    internal CardInstance? FindCombatCard(string instanceId)
    {
        foreach (var pile in CombatPiles)
            foreach (var card in GetPile(pile))
                if (card.InstanceId == instanceId) return card;
        return null;
    }
    internal List<PendingEffect> PendingEffects { get; } = [];
    internal EndTurnResolutionStage EndTurnStage { get; set; }
    internal int FlushHookIndex { get; set; }
    internal HashSet<string> TemporarilyRetainedCards { get; } = new(StringComparer.Ordinal);
    internal bool PreFlushShouldFlush { get; set; }
    internal IReadOnlyList<CardInstance> FlushedCards { get; set; } = [];
    internal IReadOnlyList<CardInstance> RetainedCards { get; set; } = [];
    internal EntityState? PendingPowerOwner { get; set; }
    internal PowerTrigger PendingPowerTrigger { get; set; }
    internal List<ContentId>? PendingPowerQueue { get; set; }
    internal int PendingPowerIndex { get; set; }
    public IReadOnlyList<GameEvent> Events => _eventView;

    internal void Emit(GameSession session, string type, string source, string target, int amount,
        DamageProperties properties = DamageProperties.None, CardInstance? cardSource = null,
        bool causedByEtherealExhaustion = false, ContentId? affectedPower = null,
        decimal? exactAmount = null)
    {
        var gameEvent = new GameEvent(_events.Count, type, source, target, amount, properties,
            cardSource?.InstanceId, causedByEtherealExhaustion, affectedPower, CurrentCardPlay?.ExecutionId, exactAmount);
        _events.Add(gameEvent);
        if (type == "player_turn_start") _playerTurnEventStartSequence = gameEvent.Sequence + 1;
        if (type == "card_exhausted" && source == Player.Id)
            LastPlayerCardExhaustTurn = Turn;
        if ((type is "damage" or "hp_loss") && target == Player.Id && amount > 0)
        {
            LastPlayerHpLossTurn = Turn;
            PlayerHpLossCount = checked(PlayerHpLossCount + 1);
        }
        List<(EntityState Owner, EffectNode Effect, EntityState? Target)>? effects = null;
        foreach (var hook in Definition.Hooks)
        {
            var context = new RuleHookContext(session, hook.Id);
            hook.OnEvent(context, gameEvent);
            if (hook.OnEventEffect(context, gameEvent) is { } eventEffect)
            {
                var owner = eventEffect.Actor switch
                {
                    GameEventActor.Source => FindCreature(source) ?? Player.Creature,
                    GameEventActor.Target => FindCreature(target)
                        ?? throw new InvalidOperationException($"Event '{type}' has no creature target '{target}'."),
                    GameEventActor.Player => Player.Creature,
                    _ => throw new ArgumentOutOfRangeException(nameof(eventEffect.Actor))
                };
                (effects ??= []).Add((owner, eventEffect.Effect, null));
            }
        }
        foreach (var actor in Enemies.Select(enemy => enemy.Creature).Append(Player.Creature))
        foreach (var (powerId, powerAmount) in actor.Powers.ToArray())
        {
            if (powerAmount <= 0) continue;
            foreach (var subscription in Definition.Powers[powerId].Events)
            {
                if (!StringComparer.Ordinal.Equals(subscription.EventType, type)
                    || (subscription.RequiresPositiveAmount && gameEvent.Amount <= 0)
                    || (gameEvent.Properties & subscription.RequiredProperties) != subscription.RequiredProperties
                    || (gameEvent.Properties & subscription.ExcludedProperties) != 0
                    || (subscription.CausedByEtherealExhaustion is { } causedByEthereal
                        && gameEvent.CausedByEtherealExhaustion != causedByEthereal)
                    || (subscription.AffectedPower is { } subscribedPower
                        && gameEvent.AffectedPower != subscribedPower)
                    || (subscription.CardTag is { } cardTag
                        && (gameEvent.CardSourceInstanceId is not { } eventCardId
                            || FindCombatCard(eventCardId) is not { } eventCard
                            || !Definition.Cards[eventCard.CardId].Tags.Contains(cardTag))))
                    continue;
                var ownerId = subscription.Owner == GameEventActor.Source ? source : target;
                if (ownerId != actor.Id) continue;
                if (subscription.OwnerTurnOnly && !IsOwnerTurn(actor)) continue;
                if (subscription.DeferUntilCardPlayed && gameEvent.CardSourceInstanceId is { } cardInstanceId
                    && CardPlayFrames.Any(frame => frame.Card.InstanceId == cardInstanceId))
                {
                    actor.AccumulatePowerEventAmount(powerId, cardInstanceId,
                        subscription.Accumulation == PowerEventAccumulationMode.EventCount ? 1 : powerAmount);
                    continue;
                }
                if (subscription.DeferUntilOwnerTurnEnd)
                {
                    actor.AccumulatePowerEventAmount(powerId, null,
                        subscription.Accumulation == PowerEventAccumulationMode.EventCount ? 1 : powerAmount);
                    continue;
                }
                var otherId = subscription.Owner == GameEventActor.Source ? target : source;
                (effects ??= []).Add((actor,
                    new PowerEventEffect(powerId, subscription.Effect, gameEvent.CardSourceInstanceId),
                    FindCreature(otherId)));
            }
        }
        if (effects is { Count: > 0 })
        {
            for (var i = effects.Count - 1; i >= 0; i--)
                PendingEffects.Add(new(effects[i].Owner, effects[i].Effect, effects[i].Target, null));
        }
    }

    private EntityState? FindCreature(string id)
    {
        if (Player.Id == id) return Player.Creature;
        return Enemies.Select(enemy => enemy.Creature).FirstOrDefault(creature => creature.Id == id);
    }

    private bool IsOwnerTurn(EntityState owner) => owner.IsEnemy ? !IsPlayerTurn : IsPlayerTurn;

    internal void ApplyPower(GameSession session, EntityState owner, ContentId power, int amount, EntityState applier)
    {
        if (!Definition.Powers.TryGetValue(power, out var definition))
            throw new InvalidOperationException($"Unknown power '{power}'.");
        foreach (var hook in Definition.Hooks)
            amount = hook.ModifyPowerAmountReceived(new RuleHookContext(session, hook.Id), power,
                definition.Role, owner.Id, applier.Id, amount);
        if (amount > 0 && definition.IsDebuff)
        {
            foreach (var (powerId, stacks) in owner.MutablePowers)
            {
                if (stacks <= 0 || Definition.Powers[powerId].Role != PowerRole.Artifact) continue;
                ApplyPower(session, owner, powerId, -1, applier);
                return;
            }
        }
        if (amount != 0 && definition.OnStackChange is { } adjustment)
            ApplyPower(session, owner, adjustment.Power, checked(amount * adjustment.AmountPerStack), applier);
        var previousAmount = owner.PowerAmount(power);
        var newAmount = checked(previousAmount + amount);
        if (newAmount == 0)
        {
            owner.MutablePowers.Remove(power);
            owner.MutablePowerAppliers.Remove(power);
            owner.SkipNextPowerTriggers.Remove(power);
            owner.ClearPowerEventAmounts(power);
            owner.ClearPowerVariables(power);
        }
        else
        {
            owner.MutablePowers[power] = newAmount;
            if (previousAmount == 0) owner.InitializePowerVariables(definition);
            if (!owner.MutablePowerAppliers.TryGetValue(power, out var appliers))
                owner.MutablePowerAppliers.Add(power, appliers = []);
            appliers.Add(applier.Id);
        }
        if (definition.Role == PowerRole.Strength) owner.Strength += amount;
        if (amount != 0 && (owner.IsEnemy && definition.SkipFirstTriggerWhenAppliedToEnemy
            || !owner.IsEnemy && definition.SkipFirstTriggerWhenAppliedToPlayer))
            owner.SkipNextPowerTriggers.Add(power);
        Emit(session, "power", applier.Id, owner.Id, amount, affectedPower: power);
    }

    internal void GainMaxHp(GameSession session, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount == 0) return;
        if (session.RunOwner is { } runOwner)
            RunEffectCommands.GainMaxHp(runOwner.State, amount);
        var player = Player.Creature;
        player.MaxHp = checked(player.MaxHp + amount);
        player.Hp = checked(player.Hp + amount);
        Emit(session, "max_hp", player.Id, player.Id, amount);
    }

    internal decimal ModifyCardDamageValue(GameSession session, EntityState dealer, decimal baseDamage,
        DamageProperties properties)
    {
        var isPoweredMove = (properties & DamageProperties.Move) != 0
            && (properties & DamageProperties.Unpowered) == 0;
        var damage = isPoweredMove ? Math.Max(0m, baseDamage + dealer.Strength) : Math.Max(0m, baseDamage);
        if (isPoweredMove)
            foreach (var (powerId, amount) in dealer.Powers)
                if (amount != 0 && Definition.Powers[powerId].OutgoingDamageMultiplier is { } factor)
                    damage *= factor;
        return damage;
    }

    internal int CalculateDamage(GameSession session, EntityState dealer, EntityState target, decimal baseDamage,
        DamageProperties properties = DamageProperties.Move)
    {
        var isPoweredMove = (properties & DamageProperties.Move) != 0
            && (properties & DamageProperties.Unpowered) == 0;
        var damage = (int)decimal.Floor(ModifyCardDamageValue(session, dealer, baseDamage, properties));
        if (isPoweredMove && HasPowerRole(target, PowerRole.Vulnerable))
        {
            decimal vulnerableMultiplier = 1.5m;
            if (dealer != target)
                foreach (var (powerId, amount) in dealer.Powers)
                    vulnerableMultiplier += Definition.Powers[powerId].VulnerableMultiplierPercentPerStack
                        * amount / 100m;
            foreach (var hook in Definition.Hooks)
                vulnerableMultiplier = hook.ModifyVulnerableMultiplier(
                    new RuleHookContext(session, hook.Id), dealer.Id, target.Id, properties, vulnerableMultiplier);
            damage = (int)decimal.Floor(damage * vulnerableMultiplier);
        }
        decimal incomingMultiplier = 1m;
        foreach (var (powerId, amount) in target.Powers)
        {
            if (amount <= 0) continue;
            foreach (var modifier in Definition.Powers[powerId].IncomingDamageModifiers)
            {
                if ((properties & modifier.RequiredProperties) != modifier.RequiredProperties
                    || (properties & modifier.ExcludedProperties) != 0
                    || (modifier.RequiredSourcePower is { } requiredPower
                        && dealer.PowerAmount(requiredPower) <= 0))
                    continue;
                incomingMultiplier *= modifier.Multiplier;
            }
        }
        damage = (int)decimal.Floor(damage * incomingMultiplier);
        return Math.Max(0, damage);
    }

    internal void RemovePowersAppliedBy(GameSession session, string applierId)
    {
        var owners = Enemies.Select(enemy => enemy.Creature).Append(Player.Creature);
        foreach (var owner in owners)
        foreach (var (powerId, sourceIds) in owner.MutablePowerAppliers.ToArray())
        {
            if (!Definition.Powers[powerId].RemoveWhenApplierDies || !sourceIds.Remove(applierId)) continue;
            if (sourceIds.Count > 0) continue;
            var amount = RemovePower(owner, powerId);
            if (amount != 0) Emit(session, "power_removed", applierId, owner.Id, amount);
        }
    }

    internal bool HasPowerRole(EntityState entity, PowerRole role) => entity.Powers.Keys.Any(id => Definition.Powers[id].Role == role);

    internal int GetPowerAmount(string entityId, ContentId powerId)
    {
        if (Player.Id == entityId) return Player.Creature.PowerAmount(powerId);
        foreach (var enemy in Enemies)
            if (enemy.Creature.Id == entityId) return enemy.Creature.PowerAmount(powerId);
        throw new KeyNotFoundException($"Unknown combat entity '{entityId}'.");
    }

    internal int GetModInt(string modId, string key, int defaultValue) => _modInts.Get(modId, key, defaultValue);
    internal int ReadModInt(string modId, string key) => _modInts.GetDeclared(modId, key);

    internal void SetModInt(string modId, string key, int value) => _modInts.Set(modId, key, value);

    internal void EnsureDrawPile(GameSession session)
    {
        if (Player.MutableDrawPile.Count != 0 || Player.MutableDiscardPile.Count == 0) return;
        session.Random.Get("shuffle").Shuffle(Player.MutableDiscardPile);
        Player.MutableDrawPile.AddRange(Player.MutableDiscardPile);
        Player.MutableDiscardPile.Clear();
    }

    internal void Draw(GameSession session, int count) =>
        EffectExecutor.Execute(session, Player.Creature, new DrawCardsEffect(count));

    internal List<CardInstance> MoveCards(CardPile from, CardPile to, int count)
    {
        var source = GetPile(from);
        var destination = GetPile(to);
        var moved = new List<CardInstance>();
        for (var i = 0; i < count && source.Count > 0; i++)
        {
            var card = source[0];
            source.RemoveAt(0);
            destination.Add(card);
            moved.Add(card);
        }
        return moved;
    }

    internal bool MoveCard(CardPile from, CardPile to, string instanceId, CardPilePosition position)
    {
        var source = GetPile(from);
        var index = source.FindIndex(card => card.InstanceId == instanceId);
        if (index < 0) return false;
        var card = source[index];
        source.RemoveAt(index);
        var destination = GetPile(to);
        if (position == CardPilePosition.Top) destination.Insert(0, card);
        else destination.Add(card);
        return true;
    }

    internal bool IsUpgradeable(CardInstance card) => card.UpgradeLevel == 0
        && Definition.Cards.TryGetValue(card.CardId, out var definition)
        && definition.HasUpgrade;

    internal bool MatchesCardSelectionFilter(CardInstance card, CardSelectionFilter filter) => filter switch
    {
        CardSelectionFilter.Any => true,
        CardSelectionFilter.Upgradeable => IsUpgradeable(card),
        CardSelectionFilter.Attack => Definition.Cards[card.CardId].Type == CardType.Attack,
        CardSelectionFilter.NonAttack => Definition.Cards.TryGetValue(card.CardId, out var definition)
            && definition.Type != CardType.Attack,
        CardSelectionFilter.NotRetained => (Definition.Cards[card.CardId].ResolveKeywords(card) & CardKeywords.Retain) == 0
            && !TemporarilyRetainedCards.Contains(card.InstanceId),
        _ => false
    };

    internal bool UpgradeCard(GameSession session, string instanceId)
    {
        foreach (var pile in Enum.GetValues<CardPile>())
        {
            var cards = GetPile(pile);
            var index = cards.FindIndex(card => card.InstanceId == instanceId);
            if (index < 0 || !IsUpgradeable(cards[index])) continue;
            var card = cards[index];
            var upgraded = card.WithUpgradeLevel(card.UpgradeLevel + 1);
            var definition = Definition.Cards[card.CardId];
            if (definition.ResolveCost(upgraded) < definition.ResolveCost(card))
            {
                var newBase = definition.ResolveCost(upgraded);
                upgraded = upgraded.WithCostModifiers(card.CostModifiers.Select(modifier => modifier.Operation == CardCostOperation.Set
                        && modifier.Amount > newBase ? modifier with { Amount = newBase } : modifier).ToArray());
            }
            cards[index] = upgraded;
            Emit(session, "card_upgraded", Player.Id, card.InstanceId, 1);
            return true;
        }
        return false;
    }

    internal void ModifyCardCost(GameSession session, string instanceId, CardCostModifier modifier)
    {
        modifier.Validate();
        if (modifier.Operation == CardCostOperation.Add && modifier.Amount == 0) return;
        foreach (var pile in Enum.GetValues<CardPile>())
        {
            var cards = GetPile(pile);
            var index = cards.FindIndex(card => card.InstanceId == instanceId);
            if (index < 0) continue;
            var card = cards[index];
            cards[index] = card.WithCostModifiers([.. card.CostModifiers, modifier]);
            Emit(session, "card_cost_modified", Player.Id, instanceId, modifier.Amount);
            return;
        }
        throw new InvalidOperationException("The selected card is no longer in combat.");
    }

    internal void ExpireCardCosts(GameSession session, CardCostExpiration boundary, string? instanceId = null)
    {
        foreach (var pile in Enum.GetValues<CardPile>())
        {
            var cards = GetPile(pile);
            for (var index = 0; index < cards.Count; index++)
            {
                var card = cards[index];
                if (instanceId is not null && card.InstanceId != instanceId) continue;
                if (!card.CostModifiers.Any(modifier => (modifier.Expires & boundary) != 0)) continue;
                var remaining = card.CostModifiers.Where(modifier => (modifier.Expires & boundary) == 0).ToArray();
                cards[index] = card.WithCostModifiers(remaining);
                Emit(session, "card_cost_expired", Player.Id, card.InstanceId, card.CostModifiers.Count - remaining.Length);
            }
        }
    }

    internal int ModifyBlockGain(EntityState owner, int amount, DamageProperties properties)
    {
        if (amount <= 0) return amount;
        var currentPlayId = CurrentCardPlay?.ExecutionId;
        foreach (var (powerId, powerAmount) in owner.Powers.ToArray())
        {
            if (powerAmount <= 0) continue;
            foreach (var modifier in Definition.Powers[powerId].BlockGainModifiers)
            {
                if (modifier.PlayerOnly && owner.IsEnemy) continue;
                var earlierEntries = _events.Count(gameEvent =>
                    gameEvent.Sequence >= _playerTurnEventStartSequence
                    && gameEvent.Type == "block" && gameEvent.Amount > 0 && gameEvent.Target == owner.Id
                    && (gameEvent.Properties & modifier.RequiredProperties) == modifier.RequiredProperties
                    && (gameEvent.Properties & modifier.ExcludedProperties) == 0
                    && gameEvent.CardPlayExecutionId != currentPlayId);
                if (earlierEntries < checked(powerAmount * modifier.PriorEntriesPerPowerStack))
                    amount = checked(amount * modifier.Multiplier);
            }
            if ((properties & DamageProperties.Move) != 0
                && Definition.Powers[powerId].BlockGainMultiplier is { } multiplier)
                amount = decimal.ToInt32(decimal.Floor(amount * multiplier));
        }
        return amount;
    }

    internal CardInstance? DrawOne(GameSession session)
    {
        if (HasPowerRole(Player.Creature, PowerRole.NoDraw)
            || Player.MutableHand.Count >= CombatLimits.MaxHandSize) return null;
        EnsureDrawPile(session);
        if (Player.MutableDrawPile.Count == 0) return null;
        var card = Player.MutableDrawPile[0];
        Player.MutableDrawPile.RemoveAt(0);
        Player.MutableHand.Add(card);
        Emit(session, "draw", Player.Id, Player.Id, 1, cardSource: card);
        return card;
    }

    internal void TransformCard(GameSession session, string instanceId, TransformSelectedCardEffect effect)
    {
        if (!Enum.IsDefined(effect.From) || effect.From == CardPile.Play)
            throw new InvalidOperationException("Transformation cannot replace an active play-pile card.");
        var pile = GetPile(effect.From);
        var index = pile.FindIndex(card => card.InstanceId == instanceId);
        if (index < 0) throw new InvalidOperationException("The selected card is no longer in its source pile.");
        if (Player.Creature.Hp <= 0 || FirstLivingEnemy is null) return;
        var replacement = Player.CreateGeneratedCard(effect.Card);
        if (effect.Upgraded && IsUpgradeable(replacement))
            replacement = replacement.WithUpgradeLevel(1);
        pile[index] = replacement;
        TemporarilyRetainedCards.Remove(instanceId);
        PendingEffects.Add(new(Player.Creature,
            new EmitGameEventEffect("card_transformed", instanceId, replacement.InstanceId, 1), null, replacement));
        CombatCardEvents.QueueEntry(this, replacement);
    }

    internal int DecrementPower(GameSession session, EntityState owner, ContentId powerId)
    {
        if (!owner.MutablePowers.TryGetValue(powerId, out var amount)) return 0;
        var definition = Definition.Powers[powerId];
        var delta = -1;
        foreach (var hook in Definition.Hooks)
            delta = hook.ModifyPowerAmountReceived(new RuleHookContext(session, hook.Id), powerId,
                definition.Role, owner.Id, owner.Id, delta);
        var newAmount = checked(amount + delta);
        if (newAmount == 0)
        {
            owner.MutablePowers.Remove(powerId);
            owner.MutablePowerAppliers.Remove(powerId);
            owner.SkipNextPowerTriggers.Remove(powerId);
            owner.ClearPowerEventAmounts(powerId);
            owner.ClearPowerVariables(powerId);
        }
        else owner.MutablePowers[powerId] = newAmount;
        if (definition.Role == PowerRole.Strength) owner.Strength += delta;
        if (delta != 0 && definition.OnStackChange is { } adjustment)
            ApplyPower(session, owner, adjustment.Power, checked(delta * adjustment.AmountPerStack), owner);
        return delta;
    }

    internal int RemovePower(EntityState owner, ContentId powerId)
    {
        var amount = owner.PowerAmount(powerId);
        if (amount == 0) return 0;
        owner.MutablePowers.Remove(powerId);
        owner.MutablePowerAppliers.Remove(powerId);
        owner.SkipNextPowerTriggers.Remove(powerId);
        owner.ClearPowerEventAmounts(powerId);
        owner.ClearPowerVariables(powerId);
        if (Definition.Powers[powerId].Role == PowerRole.Strength) owner.Strength -= amount;
        return amount;
    }

    internal void ExpirePower(GameSession session, EntityState owner, ContentId powerId)
    {
        var amount = RemovePower(owner, powerId);
        if (amount == 0) return;
        Emit(session, "power_removed", owner.Id, owner.Id, amount);
        if (Definition.Powers[powerId].OnExpire is { } adjustment)
            ApplyPower(session, owner, adjustment.Power, checked(amount * adjustment.AmountPerStack), owner);
    }

    internal int Heal(GameSession session, EntityState healer, EntityState target, int amount)
    {
        var healed = Math.Min(amount, target.MaxHp - target.Hp);
        if (healed <= 0) return 0;
        target.Hp += healed;
        Emit(session, "heal", healer.Id, target.Id, healed);
        return healed;
    }

    internal List<CardInstance> GetPile(CardPile pile) => pile switch
    {
        CardPile.Draw => Player.MutableDrawPile,
        CardPile.Hand => Player.MutableHand,
        CardPile.Discard => Player.MutableDiscardPile,
        CardPile.Exhaust => Player.MutableExhaustPile,
        CardPile.Play => Player.MutablePlayPile,
        _ => throw new ArgumentOutOfRangeException(nameof(pile))
    };
}

public sealed record GameEvent(int Sequence, string Type, string Source, string Target, int Amount,
    DamageProperties Properties = DamageProperties.None, string? CardSourceInstanceId = null,
    bool CausedByEtherealExhaustion = false, ContentId? AffectedPower = null,
    long? CardPlayExecutionId = null, decimal? ExactAmount = null);
public sealed record PendingChoiceView(string Prompt, IReadOnlyList<string> Options);
internal abstract class PendingCardSelectionState
{
    internal EffectCountScope? Counts { get; init; }
}
internal sealed record CardPlayFrame(CardInstance Card, int XEnergy, EntityState? Target,
    CardPile? ResultPile, bool IsAutoPlay, long ExecutionId = 0);
internal sealed class PendingCardChoice(ChooseCardFromPileEffect effect, IReadOnlyList<CardInstance> options,
    EntityState owner, EntityState? target, CardInstance? selectedCard) : PendingCardSelectionState
{
    public ChooseCardFromPileEffect Effect { get; } = effect;
    public IReadOnlyList<CardInstance> Options { get; } = options;
    public EntityState Owner { get; } = owner;
    public EntityState? Target { get; } = target;
    public CardInstance? SelectedCard { get; } = selectedCard;
}
internal sealed class PendingCardSetChoice(ChooseCardsFromPileEffect effect, IEnumerable<CardInstance> options,
    EntityState owner, EntityState? target, CardInstance? selectedCard, int maxChoices)
    : PendingCardSelectionState
{
    public ChooseCardsFromPileEffect Effect { get; } = effect;
    public List<CardInstance> Options { get; } = options.ToList();
    public EntityState Owner { get; } = owner;
    public EntityState? Target { get; } = target;
    public CardInstance? SelectedCard { get; } = selectedCard;
    public int MaxChoices { get; } = maxChoices;
    public List<CardInstance> Selected { get; } = [];
}
[System.Text.Json.Serialization.JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PendingCardChoiceView), "single")]
[System.Text.Json.Serialization.JsonDerivedType(typeof(PendingCardSetChoiceView), "multiple")]
public abstract record PendingCardSelectionView(string Prompt, IReadOnlyList<CardInstance> Options);
public sealed record PendingCardChoiceView(string Prompt, IReadOnlyList<CardInstance> Options)
    : PendingCardSelectionView(Prompt, Options);
public sealed record PendingCardSetChoiceView(string Prompt, IReadOnlyList<CardInstance> Options,
    IReadOnlyList<CardInstance> Selected, int MinChoices, int MaxChoices)
    : PendingCardSelectionView(Prompt, Options);
public sealed record CombatObservation(int Turn, int PlayerHp, int PlayerMaxHp, int PlayerBlock, int Energy,
    IReadOnlyList<ContentId> Hand, IReadOnlyDictionary<ContentId, int> PlayerPowers,
    int EnemyHp, int EnemyMaxHp, int EnemyBlock, int EnemyStrength,
    IntentKind Intent, int IntentDamage, IReadOnlyDictionary<ContentId, int> EnemyPowers, bool IsTerminal,
    PendingChoiceView? PendingChoice, IReadOnlyList<EnemyCombatObservation> Enemies,
    IReadOnlyList<ContentId> DrawPile, IReadOnlyList<ContentId> DiscardPile,
    IReadOnlyList<ContentId> ExhaustPile, IReadOnlyList<ContentId> PlayPile,
    IReadOnlyList<CardInstance> HandInstances, IReadOnlyList<CardInstance> DrawPileInstances,
    IReadOnlyList<CardInstance> DiscardPileInstances, IReadOnlyList<CardInstance> ExhaustPileInstances,
    IReadOnlyList<CardInstance> PlayPileInstances, IReadOnlyList<ContentId?> Potions,
    PendingCardSelectionView? PendingCardSelection)
{
    public int MaxEnergy { get; init; }
    public ContentId? CharacterId { get; init; }
    public IReadOnlyList<CardPlayPreview> HandPlayPreviews { get; init; } = Array.Empty<CardPlayPreview>();
    public IReadOnlyDictionary<ContentId, IReadOnlyDictionary<ContentId, int>> PlayerPowerVariables { get; init; }
        = new Dictionary<ContentId, IReadOnlyDictionary<ContentId, int>>();
}

public abstract record PlayerAction;
public sealed record PlayCardAction(int HandIndex, string? TargetId = null) : PlayerAction;
public sealed record UsePotionAction(int PotionIndex, string? TargetId = null) : PlayerAction;
public sealed record EndTurnAction : PlayerAction;
public sealed record ChooseOptionAction(int OptionIndex) : PlayerAction;
public sealed record ChooseCardAction(string CardInstanceId) : PlayerAction;
public sealed record ConfirmCardSelectionAction : PlayerAction;

public sealed record StepResult(CombatObservation Observation, IReadOnlyList<GameEvent> Events, bool NeedsChoice);

public sealed class GameSession
{
    internal GameSession(GameState state, RandomStreams random)
    {
        State = state;
        Random = random;
    }

    internal GameState State { get; }
    internal RandomStreams Random { get; }
    internal RunSession? RunOwner { get; set; }
}

internal enum EndTurnResolutionStage
{
    None,
    PlayerHandCleanup,
    AfterAutoPostPlayPowers,
    AfterAutoPostPlayCards,
    BeforeFlushHooks,
    BeforeFlushLateHooks,
    FlushHand,
    AfterFlushHooks,
    PlayerTurnEndPowers,
    PlayerOwnerTurnEndPowers,
    EnemyPlayerSideEndPowers,
    ExpirePlayerPowers,
    EnemyOwnerTurnStartPowers,
    EnemyAction,
    ApplyEnemyActionPower,
    EnemyTurnEndPowers,
    EnemyOwnerTurnEndPowers,
    PlayerEnemySideEndPowers,
    AdvanceEnemyAction,
    StartPlayerTurn,
    PlayerTurnStartPowers,
    PlayerOwnerTurnStartPowers
}

internal static class CombatLimits
{
    public const int MaxHandSize = 10;
}
