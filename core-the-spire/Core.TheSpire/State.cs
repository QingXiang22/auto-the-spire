namespace CoreTheSpire;

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
    private readonly List<ContentId> _hand;
    private readonly List<ContentId> _drawPile;
    private readonly List<ContentId> _discardPile = [];
    private readonly List<ContentId> _exhaustPile = [];
    private readonly List<ContentId> _playPile = [];
    private readonly IReadOnlyList<ContentId> _handView;
    private readonly IReadOnlyList<ContentId> _drawPileView;
    private readonly IReadOnlyList<ContentId> _discardPileView;
    private readonly IReadOnlyList<ContentId> _exhaustPileView;
    private readonly IReadOnlyList<ContentId> _playPileView;

    public PlayerCombatState(string id, int hp, int maxHp, IEnumerable<ContentId> hand, IEnumerable<ContentId>? drawPile = null)
    {
        Creature = new EntityState(id, false, hp, maxHp);
        _hand = [.. hand];
        _drawPile = drawPile is null ? [] : [.. drawPile];
        _handView = _hand.AsReadOnly();
        _drawPileView = _drawPile.AsReadOnly();
        _discardPileView = _discardPile.AsReadOnly();
        _exhaustPileView = _exhaustPile.AsReadOnly();
        _playPileView = _playPile.AsReadOnly();
    }

    public EntityState Creature { get; }
    public string Id => Creature.Id;
    public int Energy { get; internal set; } = 3;
    public IReadOnlyList<ContentId> Hand => _handView;
    public IReadOnlyList<ContentId> DrawPile => _drawPileView;
    public IReadOnlyList<ContentId> DiscardPile => _discardPileView;
    public IReadOnlyList<ContentId> ExhaustPile => _exhaustPileView;
    public IReadOnlyList<ContentId> PlayPile => _playPileView;
    internal List<ContentId> MutableHand => _hand;
    internal List<ContentId> MutableDrawPile => _drawPile;
    internal List<ContentId> MutableDiscardPile => _discardPile;
    internal List<ContentId> MutableExhaustPile => _exhaustPile;
    internal List<ContentId> MutablePlayPile => _playPile;
}

internal sealed class EnemyCombatState(EntityState creature, EnemyDefinition definition)
{
    public EntityState Creature { get; } = creature;
    public EnemyDefinition Definition { get; } = definition;
    public int ActionIndex { get; set; }
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
    public bool IsTerminal => Player.Creature.Hp <= 0 || Enemies.All(enemy => enemy.Creature.Hp <= 0);
    public ChooseEffect? PendingChoice { get; internal set; }
    internal EntityState? PendingChoiceOwner { get; set; }
    internal EntityState? PendingChoiceTarget { get; set; }
    internal ContentId? PendingPlayedCard { get; set; }
    internal List<(EntityState Owner, EffectNode Effect, EntityState? Target)> PendingEffects { get; } = [];
    internal EndTurnResolutionStage EndTurnStage { get; set; }
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

    internal void MoveCards(CardPile from, CardPile to, int count)
    {
        var source = GetPile(from);
        var destination = GetPile(to);
        for (var i = 0; i < count && source.Count > 0; i++)
        {
            var card = source[0];
            source.RemoveAt(0);
            destination.Add(card);
        }
    }

    internal List<ContentId> GetPile(CardPile pile) => pile switch
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
public sealed record CombatObservation(int Turn, int PlayerHp, int PlayerMaxHp, int PlayerBlock, int Energy,
    IReadOnlyList<ContentId> Hand, int EnemyHp, int EnemyMaxHp, int EnemyBlock, int EnemyStrength,
    IntentKind Intent, int IntentDamage, IReadOnlyDictionary<ContentId, int> EnemyPowers, bool IsTerminal,
    PendingChoiceView? PendingChoice, IReadOnlyList<EnemyCombatObservation> Enemies);

public abstract record PlayerAction;
public sealed record PlayCardAction(int HandIndex, string? TargetId = null) : PlayerAction;
public sealed record EndTurnAction : PlayerAction;
public sealed record ChooseOptionAction(int OptionIndex) : PlayerAction;

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
    PlayerTurnEndPowers,
    EnemyAction,
    ApplyEnemyActionPower,
    EnemyTurnEndPowers,
    PlayerEnemySideEndPowers,
    AdvanceEnemyAction,
    StartPlayerTurn
}
