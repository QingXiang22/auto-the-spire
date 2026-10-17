using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

internal static class RelicSourceChecks
{
    public static void Run()
    {
        Check.Run("Relic source: all 297 classes, hashes and complete pool membership match local sts2", () =>
        {
            var root = new DirectoryInfo(Environment.CurrentDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "AutoTheSpire.sln"))) root = root.Parent;
            Check.True(root is not null && Directory.Exists(Path.Combine(root.FullName, "sts2")), "local source is required");
            var directory = Path.Combine(root!.FullName, "sts2/src/Core/Models/Relics");
            var classes = Directory.GetFiles(directory, "*.cs").Where(path => Regex.IsMatch(File.ReadAllText(path), @"\bclass \w+\s*:\s*RelicModel")).ToArray();
            Check.Equal(297, classes.Length, "full source denominator");
            Check.Sequence(classes.Select(Path.GetFileNameWithoutExtension).Order(), RelicSourceCatalog.All.Select(r => r.Name).Order(), "no missing or helper identities");
            foreach (var row in RelicSourceCatalog.All)
            {
                var path = Path.Combine(root.FullName, row.SourcePath);
                Check.Equal(row.SourceSha256, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))), row.Name + " source hash");
            }
            foreach (var path in Directory.GetFiles(Path.Combine(root.FullName, "sts2/src/Core/Models/RelicPools"), "*.cs"))
            {
                var pool = Path.GetFileNameWithoutExtension(path);
                var expected = Regex.Matches(File.ReadAllText(path), @"ModelDb\.Relic<([^>]+)>").Select(m => m.Groups[1].Value).ToArray();
                var actual = RelicSourceCatalog.All.Where(r => r.Pools.Contains(pool)).Select(r => r.Name).ToArray();
                Check.Sequence(expected.Order(), actual.Order(), pool + " full membership (pool order is retained in the separate pool manifest)");
            }
        });
    }
}
