namespace CoreTheSpire;

// A linked immutable scope stays attached to its queued continuation, including across choices.
internal sealed record EffectCountScope(string Name, int Value, EffectCountScope? Parent)
{
    internal int Resolve(string name) => Name == name ? Value
        : Parent?.Resolve(name) ?? throw new InvalidOperationException($"Count '{name}' is not bound in this effect scope.");
}

internal readonly record struct PendingEffect(EntityState Owner, EffectNode Effect, EntityState? Target,
    CardInstance? SelectedCard, EffectCountScope? Counts = null);
