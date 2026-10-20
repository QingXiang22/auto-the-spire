using System.Text.Json;
using System.Text.Json.Nodes;
using CoreTheSpire;
using CoreTheSpire.Engine;

// Build-time inspection of the package's existing definition libraries. No gameplay dispatch or
// runtime directory discovery lives here; source catalogs and pool ordering remain independent.
internal static class ContentRegistration
{
    private sealed record Registry(string Kind, string Type, string Blueprint, string Collection);
    private static readonly Registry[] Registries = [
        new("card", "Card", "CardBlueprint", "CardLibrary"),
        new("power", "Power", "PowerBlueprint", "PowerLibrary")];

    public static void Check(DslPackage package, bool synchronize = false)
    {
        var session = package.CreateSession(0, ["player"]); session.Start();
        if (session.Status == SessionStatus.Faulted) throw new InvalidOperationException(session.Fault?.ToString());
        var metadata = JsonNode.Parse(File.ReadAllText(package.PublicMetadataPath))!.AsObject();
        foreach (var registry in Registries)
        {
            var type = package.Rules.TypeByQualifiedId("base-spire:" + registry.Blueprint);
            var collection = package.Rules.Collections.Single(c => c.QualifiedId == "base-spire:" + registry.Collection);
            var defined = session.State.CollectionMembers(collection.Id)
                .Select(i => session.State.GetField(i, type.FieldIndex("DefinitionId")).AsString()).ToArray();
            if (defined.Distinct(StringComparer.Ordinal).Count() != defined.Length) throw new FormatException($"Duplicate executable {registry.Kind} definition.");
            var rows = package.CatalogPaths.Select(path => (path, document: JsonDocument.Parse(File.ReadAllText(path)))).ToArray();
            try
            {
                var catalog = rows.Single(r => r.document.RootElement.GetProperty("kind").GetString() == registry.Kind).document.RootElement;
                var migrated = catalog.GetProperty("entries").EnumerateArray().Where(r => r.GetProperty("definition").ValueKind == JsonValueKind.String).ToArray();
                RequireSame(defined, migrated.Select(r => r.GetProperty("id").GetString()!), registry.Kind + " executable/catalog");
                foreach (var row in migrated)
                {
                    var id = row.GetProperty("id").GetString()!; var relative = row.GetProperty("definition").GetString()!;
                    var source = EngineDslParser.Parse(File.ReadAllText(Path.Combine(package.Root, relative)), relative);
                    if (!DefinitionStrings(source.Members).Contains(id, StringComparer.Ordinal))
                        throw new FormatException($"{registry.Kind} catalog maps '{id}' to a module without that definition: {relative}");
                }
            }
            finally { foreach (var row in rows) row.document.Dispose(); }
            // Startup development instances are explicit public fixtures, independent of executable cards.
            var instance = package.Rules.TypeByQualifiedId("base-spire:" + registry.Type);
            var publicIds = defined.Concat(session.State.AllInstances(instance.Id)
                .Select(i => session.State.GetField(i, instance.FieldIndex("DefinitionId")).AsString()))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var policy = metadata["content"]!["base-spire:" + registry.Type]!.AsObject();
            if (synchronize) policy["content_ids"] = new JsonArray(publicIds.Select(id => JsonValue.Create(id)).ToArray());
            else RequireSame(publicIds, policy["content_ids"]!.AsArray().Select(n => n!.GetValue<string>()), registry.Kind + " public content_ids (run sync-content to derive)");
        }
        if (synchronize) File.WriteAllText(package.PublicMetadataPath, metadata.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }

    private static void RequireSame(IEnumerable<string> expected, IEnumerable<string> actual, string label)
    {
        var left = expected.ToHashSet(StringComparer.Ordinal); var right = actual.ToArray();
        var missing = left.Except(right, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var extra = right.Except(left, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (missing.Length > 0 || extra.Length > 0 || right.Distinct(StringComparer.Ordinal).Count() != right.Length)
            throw new FormatException($"{label} drift: missing=[{string.Join(", ", missing)}], extra=[{string.Join(", ", extra)}], duplicate={right.Length != right.Distinct(StringComparer.Ordinal).Count()}");
    }

    private static IEnumerable<string> DefinitionStrings(IEnumerable<DslMemberNode> members)
    {
        foreach (var member in members)
        {
            if (member is DslDeclarationNode declaration)
            {
                // A call that merely creates/applies another definition is not ownership. Only
                // definition arguments on a module's template instances contribute its registration.
                if (declaration.Kind == "use" && declaration.Members.OfType<DslPropertyNode>().SingleOrDefault(p => p.Name == "args")?.Value is DslBlockNode arguments
                    && arguments.Members.OfType<DslPropertyNode>().SingleOrDefault(p => p.Name == "definition")?.Value is DslStringNode owned)
                    yield return owned.Value;
                foreach (var value in DefinitionStrings(declaration.Members)) yield return value;
            }
            if (member is not DslPropertyNode property) continue;
            if (property.Value is DslBlockNode block)
                foreach (var value in DefinitionStrings(block.Members)) yield return value;
        }
    }
}
