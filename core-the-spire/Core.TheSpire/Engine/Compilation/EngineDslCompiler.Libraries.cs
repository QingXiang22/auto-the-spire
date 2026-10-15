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
                var source = EngineDslParser.Parse(File.ReadAllText(path), path);
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
    }
}
