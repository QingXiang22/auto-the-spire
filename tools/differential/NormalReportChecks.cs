using System.IO.Compression;
using System.Text.Json;

namespace Differential;

internal static class NormalReportChecks
{
    internal static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "normal-report-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "evidence.zip");
            var records = Enumerable.Range(0, 192).Select(i => new { sequence = i, text = "source " + i }).ToArray();
            string packed;
            using (var store = new NormalReportStore(path))
                packed = store.Pack(new { early = records[..128], late = records })!.ToJsonString();
            using var document = JsonDocument.Parse(packed);
            var early = document.RootElement.GetProperty("early");
            var late = document.RootElement.GetProperty("late");
            if (early.GetProperty("$count").GetInt32() != 128 || late.GetProperty("$count").GetInt32() != 192)
                throw new InvalidOperationException("History lengths were lost in the report.");
            using var archive = ZipFile.OpenRead(path);
            if (archive.Entries.Count != 3)
                throw new InvalidOperationException("Repeated history prefixes were stored more than once.");
            var restored = new List<int>();
            foreach (var chunk in late.GetProperty("$chunks").EnumerateArray())
            {
                using var stream = archive.GetEntry(chunk.GetProperty("$artifact").GetString()!)!.Open();
                using var values = JsonDocument.Parse(stream);
                restored.AddRange(values.RootElement.EnumerateArray().Select(v => v.GetProperty("sequence").GetInt32()));
            }
            if (!restored.SequenceEqual(Enumerable.Range(0, 192)))
                throw new InvalidOperationException("Report storage changed history content or order.");
        }
        finally { Directory.Delete(directory, true); }
    }
}
