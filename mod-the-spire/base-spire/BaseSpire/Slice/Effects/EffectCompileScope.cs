using System.Collections.Immutable;
using CoreTheSpire;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Effects;

/// <summary>Immutable lexical bindings used only while generating IR, never gameplay state.</summary>
public sealed record EffectCompileScope(Expr? SelectedCard, ImmutableDictionary<string, Expr> Counts)
{
    public bool DeferDiscardFlush { get; init; }
    public static EffectCompileScope Empty { get; } = new(null,
        ImmutableDictionary.Create<string, Expr>(StringComparer.Ordinal));
    public EffectCompileScope BindCount(string name, Expr value) => this with { Counts = Counts.SetItem(name, value) };
    public EffectCompileScope BindCard(Expr card) => this with { SelectedCard = card };
    public Expr Count(string name, DslSourceLocation source) => Counts.TryGetValue(name, out var value) ? value
        : throw new InvalidOperationException($"{source}: savedCount '{name}' has no enclosing withCount");
}
