using System.Text;

namespace CoreTheSpire;

public enum StateVariableScope { Run, Combat }
public enum StateVariableType { Int }
public readonly record struct StateVariableKey(ContentId Id, StateVariableScope Scope);

public sealed record StateVariableDefinition(ContentId Id, StateVariableScope Scope,
    StateVariableType Type, int DefaultInt = 0)
{
    public StateVariableKey Key => new(Id, Scope);
    public void Validate()
    {
        Id.Validate();
        if (!Enum.IsDefined(Scope) || !Enum.IsDefined(Type))
            throw new ArgumentException($"State variable '{Id}' has an unsupported scope or type.");
    }
}

/// <summary>Compiles package state declarations into immutable runtime schemas.</summary>
public static class SpireStateDslCompiler
{
    public static IReadOnlyList<StateVariableDefinition> Compile(DslPackageNode package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var result = new List<StateVariableDefinition>();
        var ids = new HashSet<StateVariableKey>();
        foreach (var member in package.Members)
        {
            if (member is not DslDeclarationNode declaration || declaration.Kind != "state")
                throw new DslSourceException(member.Location, "A state package can contain state declarations only.");
            var properties = new Dictionary<string, DslValueNode>(StringComparer.Ordinal);
            foreach (var child in declaration.Members)
            {
                if (child is not DslPropertyNode property)
                    throw new DslSourceException(child.Location, "State declarations can contain properties only.");
                if (!properties.TryAdd(property.Name, property.Value))
                    throw new DslSourceException(property.Location, $"Duplicate property '{property.Name}'.");
                if (property.Name is not ("scope" or "type" or "default"))
                    throw new DslSourceException(property.Location, $"Unknown state property '{property.Name}'.");
            }
            var scopeNode = Required(properties, "scope", declaration.Location);
            var typeNode = Required(properties, "type", declaration.Location);
            var scope = Name(scopeNode) switch
            {
                "run" => StateVariableScope.Run,
                "combat" => StateVariableScope.Combat,
                var value => throw new DslSourceException(scopeNode.Location, $"Unknown state scope '{value}'.")
            };
            if (Name(typeNode) != "int")
                throw new DslSourceException(typeNode.Location, "The first state schema supports type = int only.");
            var initial = 0;
            if (properties.TryGetValue("default", out var defaultNode))
            {
                if (defaultNode is not DslNumberNode number || !number.TryGetInt32(out initial))
                    throw new DslSourceException(defaultNode.Location, "An int state default must be a 32-bit integer.");
            }
            var id = ContentId.Parse($"{package.Name}:{Slug(declaration.Name)}");
            var definition = new StateVariableDefinition(id, scope, StateVariableType.Int, initial);
            if (!ids.Add(definition.Key))
                throw new DslSourceException(declaration.Location, $"Duplicate state variable '{id}' in scope '{scope}'.");
            result.Add(definition);
        }
        return result.AsReadOnly();
    }

    private static DslValueNode Required(IReadOnlyDictionary<string, DslValueNode> properties,
        string key, DslSourceLocation location) => properties.TryGetValue(key, out var value)
        ? value : throw new DslSourceException(location, $"State declaration requires '{key}'.");

    private static string Name(DslValueNode node) => node is DslNameNode name
        ? name.Name : throw new DslSourceException(node.Location, "Expected a name.");

    private static string Slug(string name)
    {
        var slug = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current) && i > 0
                && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
                    || (char.IsUpper(name[i - 1]) && i + 1 < name.Length && char.IsLower(name[i + 1]))))
                slug.Append('-');
            slug.Append(char.ToLowerInvariant(current));
        }
        return slug.ToString();
    }
}
