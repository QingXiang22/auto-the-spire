namespace CoreTheSpire;

/// <summary>Integer extension values owned by one runtime scope and isolated by package ID.</summary>
internal sealed class ScopedIntState
{
    private readonly Dictionary<string, Dictionary<string, int>> _values = new(StringComparer.Ordinal);

    public int Get(string modId, string key, int defaultValue = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _values.TryGetValue(modId, out var values)
            ? values.GetValueOrDefault(key, defaultValue)
            : defaultValue;
    }

    public void Set(string modId, string key, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!_values.TryGetValue(modId, out var values))
            _values.Add(modId, values = new Dictionary<string, int>(StringComparer.Ordinal));
        values[key] = value;
    }
}
