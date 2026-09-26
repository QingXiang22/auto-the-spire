using System.Collections.ObjectModel;

namespace CoreTheSpire;

public readonly record struct ContentId(string Value)
{
    public override string ToString() => Value;

    public static ContentId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1)
            throw new ArgumentException("Content IDs must be namespaced (namespace:name).", nameof(value));
        return new ContentId(value);
    }

    internal void Validate()
    {
        var separator = Value.IndexOf(':');
        if (separator <= 0 || separator == Value.Length - 1)
            throw new ArgumentException($"Content ID '{Value}' must use namespace:name form.");
    }
}

public sealed record PackageInfo(string Id, string Version);
public sealed record PowerDefinition(ContentId Id, PowerTrigger Trigger = PowerTrigger.None, ContentId? TriggerEffect = null,
    bool SkipFirstTriggerWhenAppliedToEnemy = false, PowerRole Role = PowerRole.None, bool DecrementOnTrigger = false,
    bool SkipFirstTriggerWhenAppliedToPlayer = false);
public sealed record CardDefinition(ContentId Id, int Cost, CardType Type, EffectNode Effect,
    bool Exhaust = false, TargetSelector Target = TargetSelector.FirstEnemy);
public sealed record EnemyActionDefinition(ContentId Id, EffectNode Effect, IntentKind Intent, int IntentDamage = 0, ContentId? AppliedPower = null, int AppliedPowerAmount = 0);
public sealed class EncounterDefinition
{
    public EncounterDefinition(ContentId id, RunRoomType roomType, IEnumerable<ContentId> enemyIds)
    {
        id.Validate();
        ArgumentNullException.ThrowIfNull(enemyIds);
        var enemies = enemyIds.ToArray();
        if (roomType is not (RunRoomType.Monster or RunRoomType.Elite or RunRoomType.Boss))
            throw new ArgumentOutOfRangeException(nameof(roomType), "Encounters must be Monster, Elite, or Boss rooms.");
        if (enemies.Length == 0) throw new ArgumentException("An encounter must contain at least one enemy.", nameof(enemyIds));
        foreach (var enemyId in enemies) enemyId.Validate();
        Id = id;
        RoomType = roomType;
        EnemyIds = Array.AsReadOnly(enemies);
    }

    public ContentId Id { get; }
    public RunRoomType RoomType { get; }
    public IReadOnlyList<ContentId> EnemyIds { get; }
}
public sealed record EnemyDefinition
{
    public EnemyDefinition(ContentId id, int minHp, int maxHp, IReadOnlyList<EnemyActionDefinition> actions, bool loopLastAction = true)
    {
        Id = id;
        MinHp = minHp;
        MaxHp = maxHp;
        Actions = Array.AsReadOnly(actions.ToArray());
        LoopLastAction = loopLastAction;
    }
    public ContentId Id { get; }
    public int MinHp { get; }
    public int MaxHp { get; }
    public IReadOnlyList<EnemyActionDefinition> Actions { get; }
    public bool LoopLastAction { get; }
}

public enum CardType { Attack, Skill, Power }
public enum IntentKind { None, Attack, Buff }
public enum PowerTrigger { None, AfterPlayerTurnEnd, AfterEnemyTurnEnd }
public enum PowerRole { None, Strength, Vulnerable }

public interface IRulePackage
{
    PackageInfo Info { get; }
    void Register(ContentRegistry registry);
}

public sealed class ContentRegistry
{
    private readonly Dictionary<ContentId, CardDefinition> _cards = [];
    private readonly Dictionary<ContentId, EnemyDefinition> _enemies = [];
    private readonly Dictionary<ContentId, EncounterDefinition> _encounters = [];
    private readonly Dictionary<ContentId, PowerDefinition> _powers = [];
    private readonly Dictionary<ContentId, CharacterDefinition> _characters = [];
    private readonly Dictionary<ContentId, RunDefinition> _runs = [];
    private readonly Dictionary<ContentId, ICustomEffectHandler> _customEffects = [];
    private readonly List<IRuleHook> _hooks = [];
    private readonly List<IRunHook> _runHooks = [];
    private readonly List<PackageInfo> _packages = [];

    public void AddPackage(IRulePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (_packages.Any(p => p.Id == package.Info.Id))
            throw new InvalidOperationException($"Package '{package.Info.Id}' is already loaded.");
        var cardsBefore = new Dictionary<ContentId, CardDefinition>(_cards);
        var enemiesBefore = new Dictionary<ContentId, EnemyDefinition>(_enemies);
        var encountersBefore = new Dictionary<ContentId, EncounterDefinition>(_encounters);
        var powersBefore = new Dictionary<ContentId, PowerDefinition>(_powers);
        var charactersBefore = new Dictionary<ContentId, CharacterDefinition>(_characters);
        var runsBefore = new Dictionary<ContentId, RunDefinition>(_runs);
        var customEffectsBefore = new Dictionary<ContentId, ICustomEffectHandler>(_customEffects);
        var hookCountBefore = _hooks.Count;
        var runHookCountBefore = _runHooks.Count;
        try
        {
            package.Register(this);
            _packages.Add(package.Info);
        }
        catch
        {
            Restore(_cards, cardsBefore);
            Restore(_enemies, enemiesBefore);
            Restore(_encounters, encountersBefore);
            Restore(_powers, powersBefore);
            Restore(_characters, charactersBefore);
            Restore(_runs, runsBefore);
            Restore(_customEffects, customEffectsBefore);
            _hooks.RemoveRange(hookCountBefore, _hooks.Count - hookCountBefore);
            _runHooks.RemoveRange(runHookCountBefore, _runHooks.Count - runHookCountBefore);
            throw;
        }
    }

    public void Register(CardDefinition definition) => AddUnique(_cards, definition.Id, definition, "card");
    public void Register(EnemyDefinition definition) => AddUnique(_enemies, definition.Id, definition, "enemy");
    public void Register(EncounterDefinition definition) => AddUnique(_encounters, definition.Id, definition, "encounter");
    public void Register(PowerDefinition definition) => AddUnique(_powers, definition.Id, definition, "power");
    public void Register(CharacterDefinition definition) => AddUnique(_characters, definition.Id, definition, "character");
    public void Register(RunDefinition definition) => AddUnique(_runs, definition.Id, definition, "run");
    public void Register(IRuleHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        hook.Id.Validate();
        if (_hooks.Any(existing => existing.Id == hook.Id))
            throw new InvalidOperationException($"Duplicate hook ID '{hook.Id}'.");
        _hooks.Add(hook);
    }
    public void Register(IRunHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        hook.Id.Validate();
        if (_runHooks.Any(existing => existing.Id == hook.Id))
            throw new InvalidOperationException($"Duplicate run hook ID '{hook.Id}'.");
        _runHooks.Add(hook);
    }
    public void Register(ICustomEffectHandler handler) => AddUnique(_customEffects, handler.Id, handler, "custom effect handler");

    public void Override(CardDefinition definition) => Replace(_cards, definition.Id, definition, "card");
    public void Override(EnemyDefinition definition) => Replace(_enemies, definition.Id, definition, "enemy");
    public void Override(EncounterDefinition definition) => Replace(_encounters, definition.Id, definition, "encounter");
    public void Override(PowerDefinition definition) => Replace(_powers, definition.Id, definition, "power");
    public void Override(CharacterDefinition definition) => Replace(_characters, definition.Id, definition, "character");
    public void Override(RunDefinition definition) => Replace(_runs, definition.Id, definition, "run");

    public GameDefinition Build()
    {
        if (_packages.Count == 0)
            throw new InvalidOperationException("At least one rules package must be loaded.");
        foreach (var encounter in _encounters.Values)
        foreach (var enemyId in encounter.EnemyIds)
            if (!_enemies.ContainsKey(enemyId))
                throw new InvalidOperationException($"Encounter '{encounter.Id}' references unknown enemy '{enemyId}'.");
        return new GameDefinition(
            _packages.ToArray(),
            new ReadOnlyDictionary<ContentId, CardDefinition>(new Dictionary<ContentId, CardDefinition>(_cards)),
            new ReadOnlyDictionary<ContentId, EnemyDefinition>(new Dictionary<ContentId, EnemyDefinition>(_enemies)),
            new ReadOnlyDictionary<ContentId, EncounterDefinition>(new Dictionary<ContentId, EncounterDefinition>(_encounters)),
            new ReadOnlyDictionary<ContentId, PowerDefinition>(new Dictionary<ContentId, PowerDefinition>(_powers)),
            new ReadOnlyDictionary<ContentId, CharacterDefinition>(new Dictionary<ContentId, CharacterDefinition>(_characters)),
            new ReadOnlyDictionary<ContentId, RunDefinition>(new Dictionary<ContentId, RunDefinition>(_runs)),
            new ReadOnlyDictionary<ContentId, ICustomEffectHandler>(new Dictionary<ContentId, ICustomEffectHandler>(_customEffects)),
            _hooks.ToArray(), _runHooks.ToArray());
    }

    private static void AddUnique<T>(Dictionary<ContentId, T> items, ContentId id, T definition, string kind)
    {
        id.Validate();
        if (!items.TryAdd(id, definition))
            throw new InvalidOperationException($"Duplicate {kind} ID '{id}'. Use Override explicitly to replace content.");
    }

    private static void Replace<T>(Dictionary<ContentId, T> items, ContentId id, T definition, string kind)
    {
        id.Validate();
        if (!items.ContainsKey(id))
            throw new InvalidOperationException($"Cannot override unknown {kind} ID '{id}'.");
        items[id] = definition;
    }

    private static void Restore<T>(Dictionary<ContentId, T> target, Dictionary<ContentId, T> snapshot)
    {
        target.Clear();
        foreach (var (id, definition) in snapshot) target.Add(id, definition);
    }
}

public sealed class GameDefinition
{
    internal GameDefinition(PackageInfo[] packages, IReadOnlyDictionary<ContentId, CardDefinition> cards,
        IReadOnlyDictionary<ContentId, EnemyDefinition> enemies, IReadOnlyDictionary<ContentId, EncounterDefinition> encounters,
        IReadOnlyDictionary<ContentId, PowerDefinition> powers,
        IReadOnlyDictionary<ContentId, CharacterDefinition> characters, IReadOnlyDictionary<ContentId, RunDefinition> runs,
        IReadOnlyDictionary<ContentId, ICustomEffectHandler> customEffects, IRuleHook[] hooks, IRunHook[] runHooks)
    {
        Packages = Array.AsReadOnly(packages);
        Cards = cards;
        Enemies = enemies;
        Encounters = encounters;
        Powers = powers;
        Characters = characters;
        Runs = runs;
        CustomEffects = customEffects;
        Hooks = Array.AsReadOnly(hooks);
        RunHooks = Array.AsReadOnly(runHooks);
        Version = string.Join("+", packages.Select(p => $"{p.Id}@{p.Version}"));
    }

    public IReadOnlyList<PackageInfo> Packages { get; }
    public IReadOnlyDictionary<ContentId, CardDefinition> Cards { get; }
    public IReadOnlyDictionary<ContentId, EnemyDefinition> Enemies { get; }
    public IReadOnlyDictionary<ContentId, EncounterDefinition> Encounters { get; }
    public IReadOnlyDictionary<ContentId, PowerDefinition> Powers { get; }
    public IReadOnlyDictionary<ContentId, CharacterDefinition> Characters { get; }
    public IReadOnlyDictionary<ContentId, RunDefinition> Runs { get; }
    internal IReadOnlyDictionary<ContentId, ICustomEffectHandler> CustomEffects { get; }
    internal IReadOnlyList<IRuleHook> Hooks { get; }
    internal IReadOnlyList<IRunHook> RunHooks { get; }
    public string Version { get; }
}

public interface IRuleHook
{
    ContentId Id { get; }
    void OnEvent(RuleHookContext context, GameEvent gameEvent);
}
