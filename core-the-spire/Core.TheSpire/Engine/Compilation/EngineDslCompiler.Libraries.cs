namespace CoreTheSpire.Engine;

public static partial class EngineDslCompiler
{
    /// <summary>Imports are authoring-time dependencies, relative to the importing file, never runtime includes.</summary>
    private sealed class SourceLibraryLoader(IReadOnlyList<IEngineDslExtension>? extensions = null,
        IReadOnlyList<EnginePackage>? dependencies = null)
    {
        private readonly Dictionary<string, EnginePackage> _files = new(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private readonly HashSet<string> _loading = new(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private readonly List<EnginePackage> _packages = [.. dependencies ?? []];

        public RuleSet Load(string[] paths)
        {
            ArgumentNullException.ThrowIfNull(paths);
            foreach (var path in paths) Read(Path.GetFullPath(path), null);
            var linker = new RuleSetBuilder();
            foreach (var package in _packages) linker.Add(package);
            return linker.Build();
        }

        private EnginePackage Read(string path, DslSourceLocation? importLocation)
        {
            if (_files.TryGetValue(path, out var loaded)) return loaded;
            if (!_loading.Add(path))
                throw new DslSourceException(importLocation ?? new(path, 1, 1), $"Cyclic import of '{path}'.");
            try
            {
                if (!File.Exists(path)) throw new DslSourceException(importLocation ?? new(path, 1, 1), $"Rule file not found: '{path}'.");
                var source = ReadFragments(path, null, new(_files.Comparer), new(_files.Comparer));
                var imported = new List<EnginePackage>();
                var importNodes = source.Members.OfType<DslPropertyNode>().Where(node => node.Name == "imports").ToArray();
                if (importNodes.Length > 1) throw new DslSourceException(importNodes[1].Location, "Duplicate property 'imports'.");
                if (importNodes.Length == 1)
                {
                    if (importNodes[0].Value is not DslListNode list)
                        throw new DslSourceException(importNodes[0].Location, "imports must be a list of file paths.");
                    foreach (var node in list.Items)
                    {
                        if (node is not DslStringNode file || string.IsNullOrWhiteSpace(file.Value))
                            throw new DslSourceException(node.Location, "Import paths must be nonempty strings.");
                        var dependency = Read(Path.GetFullPath(file.Value, Path.GetDirectoryName(path)!), node.Location);
                        if (!imported.Contains(dependency)) imported.Add(dependency);
                    }
                }
                var package = new CoreDslBinding(source, _packages, extensions ?? [], imported).Compile();
                if (_packages.Any(existing => existing.PackageId == package.PackageId))
                    throw new DslSourceException(source.Location, $"Package '{package.PackageId}' is already loaded from another file.");
                _packages.Add(package);
                _files.Add(path, package);
                return package;
            }
            finally { _loading.Remove(path); }
        }

        // Includes combine one namespace before name binding. Imports continue to link separate packages.
        private static DslPackageNode ReadFragments(string path, DslPackageNode? root,
            HashSet<string> visited, HashSet<string> loading)
        {
            if (!loading.Add(path)) throw new DslSourceException(new(path, 1, 1), "Cyclic same-package include.");
            try
            {
                if (!File.Exists(path)) throw new DslSourceException(new(path, 1, 1), "Included rule file not found.");
                var source = EngineDslParser.Parse(File.ReadAllText(path), path);
                var language = source.Members.OfType<DslPropertyNode>().Where(p => p.Name == "dsl_version").ToArray();
                if (language.Length != 1 || language[0].Value is not DslStringNode { Value: "core/0.3" })
                    if (root is not null || source.Members.OfType<DslPropertyNode>().Any(p => p.Name == "includes"))
                        throw new DslSourceException(source.Location, "Same-package includes require core/0.3 on every file.");
                if (root is not null && source.Name != root.Name)
                    throw new DslSourceException(source.Location, "Included namespace must match the entry package.");
                if (!visited.Add(path)) return source with { Members = [] };
                var members = new List<DslMemberNode>();
                foreach (var member in source.Members)
                {
                    if (member is DslPropertyNode { Name: "includes" } include)
                    {
                        if (source.Members.OfType<DslPropertyNode>().Count(p => p.Name == "includes") != 1
                            || include.Value is not DslListNode files)
                            throw new DslSourceException(include.Location, "includes must be one list of relative paths.");
                        foreach (var item in files.Items)
                        {
                            if (item is not DslStringNode file || string.IsNullOrWhiteSpace(file.Value) || Path.IsPathRooted(file.Value))
                                throw new DslSourceException(item.Location, "Include paths must be nonempty relative strings.");
                            var fragment = ReadFragments(Path.GetFullPath(file.Value, Path.GetDirectoryName(path)!),
                                root ?? source, visited, loading);
                            members.AddRange(fragment.Members);
                        }
                    }
                    else if (root is not null && member is DslPropertyNode property)
                    {
                        if (property.Name != "dsl_version")
                            throw new DslSourceException(property.Location, "Package properties belong in the entry file; fragments contain declarations only.");
                    }
                    else members.Add(member);
                }
                return source with { Members = members };
            }
            finally { loading.Remove(path); }
        }
    }
}
