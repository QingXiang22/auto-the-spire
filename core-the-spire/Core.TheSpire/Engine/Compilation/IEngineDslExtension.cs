namespace CoreTheSpire.Engine;

/// <summary>
/// A game-owned authoring extension. Both stages emit ordinary SDK declarations/IR; no alternate runtime is allowed.
/// Declare introduces names; Bind runs after core field/parameter shapes are available and before core programs bind.
/// </summary>
public interface IEngineDslExtension
{
    string Identity { get; }
    IReadOnlyList<string> DeclarationKinds { get; }
    void Declare(DslDeclarationNode declaration, EnginePackageBuilder package);
    void Bind(DslDeclarationNode declaration, EnginePackageBuilder package);
}
