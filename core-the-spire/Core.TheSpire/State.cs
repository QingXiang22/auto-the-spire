namespace CoreTheSpire;

using System.Collections;

internal sealed class EntityState
{
    private readonly Dictionary<ContentId, int> _powers = [];
    private readonly Dictionary<ContentId, HashSet<string>> _powerAppliers = [];
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
    public int MaxHp { get; }
    public int Block { get; internal set; }
    public int Strength { get; internal set; }
    public IReadOnlyDictionary<ContentId, int> Powers => _powerView;
    internal Dictionary<ContentId, int> MutablePowers => _powers;
    internal Dictionary<ContentId, HashSet<string>> MutablePowerAppliers => _powerAppliers;
    internal HashSet<ContentId> SkipNextPowerTriggers { get; } = [];
    public int PowerAmount(ContentId id) => _powers.GetValueOrDefault(id);
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
        IEnumerable<CardInstance>? drawPile = null)
    {
        Creature = new EntityState(id, false, hp, maxHp);
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

    public void SelectAction(ContentId actionId)
    {
        ConsecutiveActionCount = LastActionId == actionId ? ConsecutiveActionCount + 1 : 1;
        LastActionId = actionId;
    }
}

internal sealed class GameState
{
    private readonly List<GameEvent> _events = [];
    private readonly IReadOnlyList<GameEvent> _eventView;
    private readonly Dictionary<string, Dictionary<string, int>> _modInts = new(StringComparer.Ordinal);

    internal GameState(GameDefinition definition, PlayerCombatState player, IEnumerable<EnemyCombatState> enemies)
    {
        Definition = definition;
        Player = player;
        Enemies = enemies.ToList();
        if (Enemies.Count == 0) throw new ArgumentException("A combat encounter needs at least one enemy.", nameof(enemies));
        _eventView = _events.AsReadOnly();
    }

    public GameDefinition Definition { get; }
    public PlayerCombatState Player { get; }
    public List<EnemyCombatState> Enemies { get; }
    public EntityState Enemy => Enemies[0].Creature;
    public EnemyDefinition EnemyDefinition => Enemies[0].Definition;
    public int Turn { get; internal set; } = 1;
    public int EnemyActionIndex
    {
        get => Enemies[0].ActionIndex;
        internal set => Enemies[0].ActionIndex = value;
    }
    public int CurrentEnemyIndex { get; internal set; }
    public EntityState? FirstLivingEnemy => Enemies.FirstOrDefault(enemy => enemy.Creature.Hp > 0)?.Creature;
    public bool IsPlayerTurn { get; internal set; } = true;
    public bool IsTerminal => (Player.Creature.Hp <= 0 || Enemies.All(enemy => enemy.Creature.Hp <= 0))
        && PendingChoice is null && PendingCardSelection is null && PendingEffects.Count == 0;
    public ChooseEffect? PendingChoice { get; internal set; }
    internal PendingCardSelectionState? PendingCardSelection { get; set; }
    internal EntityState? PendingChoiceOwner { get; set; }
    internal EntityState? PendingChoiceTarget { get; set; }
    internal CardInstance? PendingChoiceSelectedCard { get; set; }
    internal PendingCardPlay? PendingCardPlay { get; set; }
    internal List<(EntityState Owner, EffectNode Effect, EntityState? Target, CardInstance? SelectedCard)> PendingEffects { get; } = [];
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

    internal void Emit(GameSession session, string type, string source, string target, int amount)
    {
        var gameEvent = new GameEvent(_events.Count, type, source, target, amount);
        _events.Add(gameEvent);
        foreach (var hook in Definition.Hooks) hook.OnEvent(new RuleHookContext(session, hook.Id), gameEvent);
    }

    internal void ApplyPower(GameSession session, EntityState owner, ContentId power, int amount, EntityState applier)
    {
        if (!Definition.Powers.TryGetValue(power, out var definition))
            throw new InvalidOperationException($"Unknown power '{power}'.");
        var newAmount = owner.PowerAmount(power) + amount;
        if (newAmount == 0)
        {
            owner.MutablePowers.Remove(power);
            owner.MutablePowerAppliers.Remove(power);
            owner.SkipNextPowerTriggers.Remove(power);
        }
        else
        {
            owner.MutablePowers[power] = newAmount;
            if (!owner.MutablePowerAppliers.TryGetValue(power, out var appliers))
                owner.MutablePowerAppliers.Add(power, appliers = []);
            appliers.Add(applier.Id);
        }
        if (definition.Role == PowerRole.Strength) owner.Strength += amount;
        if (amount != 0 && (owner.IsEnemy && definition.SkipFirstTriggerWhenAppliedToEnemy
            || !owner.IsEnemy && definition.SkipFirstTriggerWhenAppliedToPlayer))
            owner.SkipNextPowerTriggers.Add(power);
        Emit(session, "power", applier.Id, owner.Id, amount);
    }

    internal int CalculateDamage(EntityState dealer, EntityState target, int baseDamage)
    {
        var damage = Math.Max(0, baseDamage + dealer.Strength);
        decimal multiplier = 1m;
        foreach (var (powerId, amount) in dealer.Powers)
        {
            if (amount != 0 && Definition.Powers[powerId].OutgoingDamageMultiplier is { } factor)
                multiplier *= factor;
        }
        damage = (int)decimal.Floor(damage * multiplier);
        if (HasPowerRole(target, PowerRole.Vulnerable)) damage = (int)MathF.Floor(damage * 1.5f);
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
            var amount = owner.PowerAmount(powerId);
            owner.MutablePowerAppliers.Remove(powerId);
            owner.MutablePowers.Remove(powerId);
            owner.SkipNextPowerTriggers.Remove(powerId);
            Emit(session, "power_removed", applierId, owner.Id, amount);
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

    internal int GetModInt(string modId, string key, int defaultValue) =>
        _modInts.TryGetValue(modId, out var values) ? values.GetValueOrDefault(key, defaultValue) : defaultValue;

    internal void SetModInt(string modId, string key, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_modInts.TryGetValue(modId, out var values))
            _modInts.Add(modId, values = new Dictionary<string, int>(StringComparer.Ordinal));
        values[key] = value;
    }

    internal void Draw(GameSession session, int count)
    {
        if (HasPowerRole(Player.Creature, PowerRole.NoDraw)) return;
        for (var i = 0; i < count; i++)
        {
            if (Player.MutableDrawPile.Count == 0 && Player.MutableDiscardPile.Count > 0)
            {
                session.Random.Get("shuffle").Shuffle(Player.MutableDiscardPile);
                Player.MutableDrawPile.AddRange(Player.MutableDiscardPile);
                Player.MutableDiscardPile.Clear();
            }
            if (Player.MutableDrawPile.Count == 0) return;
            var card = Player.MutableDrawPile[0];
            Player.MutableDrawPile.RemoveAt(0);
            Player.MutableHand.Add(card);
            Emit(session, "draw", Player.Id, Player.Id, 1);
        }
    }

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
        && (definition.UpgradedEffect is not null || definition.UpgradedCost is not null);

    internal bool MatchesCardSelectionFilter(CardInstance card, CardSelectionFilter filter) => filter switch
    {
        CardSelectionFilter.Any => true,
        CardSelectionFilter.Upgradeable => IsUpgradeable(card),
        CardSelectionFilter.NonAttack => Definition.Cards.TryGetValue(card.CardId, out var definition)
            && definition.Type != CardType.Attack,
        CardSelectionFilter.NotRetained => (Definition.Cards[card.CardId].Keywords & CardKeywords.Retain) == 0
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
            cards[index] = new CardInstance(card.InstanceId, card.CardId, card.UpgradeLevel + 1);
            Emit(session, "card_upgraded", Player.Id, card.InstanceId, 1);
            return true;
        }
        return false;
    }

    internal void DecrementPower(EntityState owner, ContentId powerId)
    {
        if (!owner.MutablePowers.TryGetValue(powerId, out var amount)) return;
        if (amount <= 1)
        {
            owner.MutablePowers.Remove(powerId);
            owner.MutablePowerAppliers.Remove(powerId);
            owner.SkipNextPowerTriggers.Remove(powerId);
        }
        else owner.MutablePowers[powerId] = amount - 1;
        if (Definition.Powers[powerId].Role == PowerRole.Strength) owner.Strength--;
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

public sealed record GameEvent(int Sequence, string Type, string Source, string Target, int Amount);
public sealed record PendingChoiceView(string Prompt, IReadOnlyList<string> Options);
internal abstract class PendingCardSelectionState;
internal sealed record PendingCardPlay(CardInstance Card, int XEnergy);
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
    IReadOnlyList<CardInstance> PlayPileInstances, PendingCardSelectionView? PendingCardSelection);

public abstract record PlayerAction;
public sealed record PlayCardAction(int HandIndex, string? TargetId = null) : PlayerAction;
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
    BeforeFlushHooks,
    BeforeFlushLateHooks,
    FlushHand,
    AfterFlushHooks,
    PlayerTurnEndPowers,
    EnemyAction,
    ApplyEnemyActionPower,
    EnemyTurnEndPowers,
    PlayerEnemySideEndPowers,
    AdvanceEnemyAction,
    StartPlayerTurn,
    PlayerTurnStartPowers
}
