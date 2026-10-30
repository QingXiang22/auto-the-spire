using System.IO.Compression;
using System.Text.Json;
using CoreTheSpire.Engine;

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
            CheckCachedHistory(directory);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckCachedHistory(string directory)
    {
        var path = Path.Combine(directory, "cached.zip");
        var records = Enumerable.Range(0, 192).Select(i => new SessionDebugRecord(i, SessionDebugKind.EventEmit,
            "history", "source " + i, null, i, SessionStatus.Running, 0)).ToArray();
        var altered = records.ToArray(); altered[17] = altered[17] with { Detail = "changed historical argument" };
        var inputs = new Dictionary<string, SessionDebugRecord[]>
        {
            ["early"] = records[..128], ["partial"] = records[..129], ["late"] = records,
            ["altered"] = altered, ["missing"] = records[..^1], ["reordered"] = records.Reverse().ToArray()
        };
        string packed;
        using (var store = new NormalReportStore(path)) packed = store.Pack(inputs)!.ToJsonString();
        using var document = JsonDocument.Parse(packed);
        using var archive = ZipFile.OpenRead(path);
        foreach (var (key, expected) in inputs)
        {
            var history = document.RootElement.GetProperty(key); var restored = new List<SessionDebugRecord>();
            foreach (var reference in history.GetProperty("$chunks").EnumerateArray())
            {
                using var stream = archive.GetEntry(reference.GetProperty("$artifact").GetString()!)!.Open();
                restored.AddRange(JsonSerializer.Deserialize<SessionDebugRecord[]>(stream, NormalReportStore.Options)!);
            }
            if (history.GetProperty("$count").GetInt32() != expected.Length || !restored.SequenceEqual(expected))
                throw new InvalidOperationException("Cached history changed original, partial, altered or reordered records: " + key);
        }
        var early = document.RootElement.GetProperty("early").GetProperty("$chunks")[0].GetRawText();
        if (early != document.RootElement.GetProperty("late").GetProperty("$chunks")[0].GetRawText()
            || early == document.RootElement.GetProperty("altered").GetProperty("$chunks")[0].GetRawText())
            throw new InvalidOperationException("Only unchanged diagnostic segments may reuse an artifact.");
    }
}
