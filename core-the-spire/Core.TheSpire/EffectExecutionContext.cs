using System.Text.Json;

namespace CoreTheSpire;

public sealed class RuleHookContext
{
    private readonly GameSession _session;
    private readonly string _modId;

    internal RuleHookContext(GameSession session, ContentId hookId)
    {
        _session = session;
        _modId = hookId.Value[..hookId.Value.IndexOf(':')];
    }

    public int GetInt(string key, int defaultValue = 0) => _session.State.GetModInt(_modId, key, defaultValue);
    public void SetInt(string key, int value) => _session.State.SetModInt(_modId, key, value);
    public int GetPowerAmount(string entityId, ContentId powerId) =>
        _session.State.GetPowerAmount(entityId, powerId);
    public int PlayerHp => _session.State.Player.Creature.Hp;
    public int PlayerMaxHp => _session.State.Player.Creature.MaxHp;
    public int Turn => _session.State.Turn;
    public bool IsPlayerTurn => _session.State.IsPlayerTurn;
    /// <summary>Checks the owning run's relics; returns false for standalone combat sessions.</summary>
    public bool HasRelic(ContentId relicId) =>
        _session.RunOwner?.State.Relics.Contains(relicId) == true;
    public bool IsPlayerCreature(string creatureId) => _session.State.Player.Id == creatureId;
    public bool IsAliveCreature(string creatureId) =>
        _session.State.Player.Id == creatureId
            ? _session.State.Player.Creature.Hp > 0
            : _session.State.Enemies.Any(enemy => enemy.Creature.Id == creatureId && enemy.Creature.Hp > 0);
}

public interface ICustomEffectHandler
{
    ContentId Id { get; }
    void Execute(EffectExecutionContext context, JsonElement payload);
}

/// <summary>Runs a registered C# extension called by a declarative run-event effect.</summary>
public interface ICustomRunEffectHandler
{
    ContentId Id { get; }
    void Execute(RunEffectExecutionContext context, JsonElement payload);
}

/// <summary>Controlled commands available to C# effect extensions; mutable runtime state stays inside the core.</summary>
public sealed class EffectExecutionContext
{
    private readonly GameSession _session;
    private readonly EntityState _owner;
    private readonly EntityState? _target;
    private readonly string _modId;

    internal EffectExecutionContext(GameSession session, EntityState owner, EntityState? target, ContentId handlerId)
    {
        _session = session;
        _owner = owner;
        _target = target;
        _modId = handlerId.Value[..handlerId.Value.IndexOf(':')];
    }

    public string OwnerId => _owner.Id;
    public string? TargetId => _target?.Id;
    public int OwnerHp => _owner.Hp;
    public int? TargetHp => _target?.Hp;
    public int GetInt(string key, int defaultValue = 0) => _session.State.GetModInt(_modId, key, defaultValue);
    public void SetInt(string key, int value) => _session.State.SetModInt(_modId, key, value);

    public void Execute(EffectNode effect) => EffectExecutor.Execute(_session, _owner, effect, _target);
    public void DealDamage(int amount) => Execute(new AttackEffect(amount));
    public void GainBlock(int amount) => Execute(new GainBlockEffect(amount));
    public void ApplyPower(ContentId power, int amount, TargetSelector target = TargetSelector.Self) =>
        Execute(new ApplyPowerEffect(power, amount, target));
}
