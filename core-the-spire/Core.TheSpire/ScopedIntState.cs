namespace CoreTheSpire;

/// <summary>Integer extension values owned by one runtime scope and isolated by package ID.</summary>
internal sealed class ScopedIntState
{
    private readonly Dictionary<string, Dictionary<string, int>> _values = new(StringComparer.Ordinal);

    public ScopedIntState(IReadOnlyDictionary<StateVariableKey, StateVariableDefinition>? definitions,
        StateVariableScope scope)
    {
        if (definitions is null) return;
        foreach (var definition in definitions.Values)
        {
            if (definition.Scope != scope) continue;
            var separator = definition.Id.Value.IndexOf(':');
            var modId = definition.Id.Value[..separator];
            var key = definition.Id.Value[(separator + 1)..];
            if (!_values.TryGetValue(modId, out var values))
                _values.Add(modId, values = new Dictionary<string, int>(StringComparer.Ordinal));
            values.Add(key, definition.DefaultInt);
        }
    }

    public int Get(string modId, string key, int defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _values.TryGetValue(modId, out var values)
            ? values.GetValueOrDefault(key, defaultValue) : defaultValue;
    }

    public int GetDeclared(string modId, string key)
    {
        return _values.TryGetValue(modId, out var values) && values.TryGetValue(key, out var value)
            ? value : throw new InvalidOperationException($"State variable '{modId}:{key}' is not declared in this scope.");
    }

    public void Set(string modId, string key, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_values.TryGetValue(modId, out var values) || !values.ContainsKey(key))
            throw new InvalidOperationException($"State variable '{modId}:{key}' is not declared in this scope.");
        values[key] = value;
    }

    public IReadOnlyDictionary<ContentId, int> Snapshot()
    {
        var snapshot = new Dictionary<ContentId, int>();
        foreach (var (modId, values) in _values)
            foreach (var (key, value) in values)
                snapshot.Add(ContentId.Parse($"{modId}:{key}"), value);
        return snapshot;
    }
}
