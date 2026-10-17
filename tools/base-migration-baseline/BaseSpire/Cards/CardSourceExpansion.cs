using CoreTheSpire;

namespace BaseSpire.Slice;

/// <summary>Input-only base/0.0.2 expansion. Variants replace whole fields, never merge effect blocks.</summary>
internal static class CardSourceExpansion
{
    public static DslDeclarationNode Expand(DslDeclarationNode card, string version)
    {
        var properties = card.Members.OfType<DslPropertyNode>().ToArray();
        foreach (var group in properties.GroupBy(p => p.Name))
            if (group.Count() > 1)
                throw new DslSourceException(group.Skip(1).First().Location, $"duplicate card field '{group.Key}'");
        var common = properties.SingleOrDefault(p => p.Name == "common");
        var values = properties.SingleOrDefault(p => p.Name == "values");
        if (common is null && values is null) return card;
        if (version != "base/0.0.2")
            throw new DslSourceException(card.Location, "common/values require dsl_version = base/0.0.2");
        var shared = common is null ? new DslBlockNode([], card.Location) : Block(common.Value);
        ValidateFields(shared);
        var constants = values is null ? new DslBlockNode([], card.Location) : Block(values.Value);
        ValidateFields(constants);
        var members = card.Members.Where(m => m is not DslPropertyNode { Name: "common" or "values" or "normal" or "upgraded" }).ToList();
        foreach (var variant in new[] { "normal", "upgraded" })
        {
            var replacement = properties.SingleOrDefault(p => p.Name == variant);
            var overrides = replacement is null ? new DslBlockNode([], card.Location) : Block(replacement.Value);
            ValidateFields(overrides);
            var replaced = overrides.Members.OfType<DslPropertyNode>().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            var fields = shared.Members.Where(m => m is not DslPropertyNode p || !replaced.Contains(p.Name))
                .Concat(overrides.Members).ToArray();
            var bindings = new Dictionary<string, DslValueNode>(StringComparer.Ordinal);
            foreach (var value in constants.Members.OfType<DslPropertyNode>())
            {
                var definition = Block(value.Value);
                ValidateFields(definition);
                var choice = definition.Members.OfType<DslPropertyNode>().SingleOrDefault(p => p.Name == variant)
                    ?? throw new DslSourceException(value.Location, $"value '{value.Name}' is missing {variant}");
                if (definition.Members.OfType<DslPropertyNode>().Any(p => p.Name is not ("normal" or "upgraded")))
                    throw new DslSourceException(value.Location, $"value '{value.Name}' accepts only normal/upgraded");
                if (choice.Value is not DslNumberNode number || !number.TryGetInt32(out _))
                    throw new DslSourceException(choice.Location, "compile-time values must be integer literals");
                bindings.Add(value.Name, choice.Value);
            }
            DslValueNode Substitute(DslValueNode node) => node switch
            {
                DslNameNode name when name.Name.StartsWith("values.", StringComparison.Ordinal) =>
                    bindings.TryGetValue(name.Name[7..], out var value) ? value with { Location = name.Location }
                    : throw new DslSourceException(name.Location, $"unknown compile-time value '{name.Name}'"),
                DslBlockNode block => block with { Members = block.Members.Select(Replace).ToArray() },
                DslListNode list => list with { Items = list.Items.Select(Substitute).ToArray() },
                DslCallNode call => call with { Arguments = call.Arguments.Select(a => a with { Value = Substitute(a.Value) }).ToArray() },
                _ => node,
            };
            DslMemberNode Replace(DslMemberNode member) => member switch
            {
                DslPropertyNode property => property with { Value = Substitute(property.Value) },
                DslDeclarationNode declaration => declaration with { Members = declaration.Members.Select(Replace).ToArray() },
                _ => member,
            };
            members.Add(new DslPropertyNode(variant, new DslBlockNode(fields.Select(Replace).ToArray(), overrides.Location), overrides.Location));
        }
        return card with { Members = members };
    }

    private static DslBlockNode Block(DslValueNode value) => value as DslBlockNode
        ?? throw new DslSourceException(value.Location, "expected a block");

    private static void ValidateFields(DslBlockNode block)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in block.Members)
            if (member is not DslPropertyNode property || !seen.Add(property.Name))
                throw new DslSourceException(member.Location, "expected unique property names");
    }
}
