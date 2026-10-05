using System.Collections.ObjectModel;

namespace CoreTheSpire.Engine;

/// <summary>Mutable while authoring; the package freezes this object at Build.</summary>
public abstract class RuleAuthorObject
{
    public bool IsFrozen { get; private set; }

    internal void FreezeObject() => IsFrozen = true;

    protected void EnsureMutable()
    {
        if (IsFrozen) throw Faults.Of(EngineFaultCode.PackageAlreadyBuilt,
            "this rule object belongs to a built package and cannot be changed");
    }
}

internal interface IAuthoringCollection { void Freeze(); }

/// <summary>An SDK collection that rejects writes after its owning package is built.</summary>
public sealed class AuthoringList<T> : Collection<T>, IReadOnlyList<T>, IAuthoringCollection
{
    private bool _frozen;

    public void AddRange(IEnumerable<T> items)
    {
        EnsureMutable();
        foreach (var item in items) Add(item);
    }

    void IAuthoringCollection.Freeze() => _frozen = true;
    private void EnsureMutable()
    {
        if (_frozen) throw Faults.Of(EngineFaultCode.PackageAlreadyBuilt, "this rule collection is frozen");
    }
    protected override void InsertItem(int index, T item) { EnsureMutable(); base.InsertItem(index, item); }
    protected override void SetItem(int index, T item) { EnsureMutable(); base.SetItem(index, item); }
    protected override void RemoveItem(int index) { EnsureMutable(); base.RemoveItem(index); }
    protected override void ClearItems() { EnsureMutable(); base.ClearItems(); }
}

internal static class RuleFreezer
{
    public static void Freeze(EnginePackageBuilder builder)
    {
        foreach (var symbol in builder.Declarations)
        {
            switch (symbol)
            {
                case EnumTypeSymbol type: FreezeList(type.Members); break;
                case EntityTypeSymbol type: FreezeList(type.Fields); break;
                case ProcedureSymbol procedure: FreezeList(procedure.Parameters); FreezeList(procedure.Locals); break;
                case EventSymbol type: FreezeList(type.Payload); break;
                case TerminalResultSymbol result: FreezeList(result.Fields); break;
                case ComputeAssetSymbol asset: FreezeList(asset.Inputs); FreezeList(asset.Outputs); break;
                case ActionSymbol action:
                    FreezeList(action.Parameters);
                    foreach (var choices in action.Choices.Values) FreezeList(choices);
                    break;
                case ProjectionSymbol projection:
                    foreach (var type in projection.Types) type.FreezeObject();
                    FreezeList(projection.Types);
                    FreezeList(projection.Reveals);
                    break;
            }
            symbol.FreezeObject();
        }
        foreach (var subscription in builder.Subscriptions) subscription.FreezeObject();
    }

    private static void FreezeList<T>(IReadOnlyList<T> list)
    {
        foreach (var item in list)
            if (item is RuleAuthorObject child) child.FreezeObject();
        ((IAuthoringCollection)list).Freeze();
    }
}
