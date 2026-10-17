using System.Text.Json;

// The independent adapter has not been migrated. Preserve every requested case and fail explicitly.
var suiteIndex = Array.IndexOf(args, "--suite");
var outputIndex = Array.IndexOf(args, "--output");
var suite = suiteIndex >= 0 && suiteIndex + 1 < args.Length ? args[suiteIndex + 1] : "regression";
var suitePath = Path.Combine(AppContext.BaseDirectory, "suites", suite + ".json");
if (!File.Exists(suitePath))
{
    Console.Error.WriteLine($"unsupported_capability: suite '{suite}' is not available in the pure DSL adapter.");
    return 3;
}
using var definition = JsonDocument.Parse(File.ReadAllText(suitePath));
var ids = definition.RootElement.GetProperty("expected_case_ids").EnumerateArray().Select(v => v.GetString()!).ToArray();
var report = new
{
    schema = "dsl-migration/differential@1", suite, status = "unsupported", independent_verified = false,
    executed = 0, expected_case_ids = ids,
    cases = ids.Select(id => new { id, status = "unsupported", reason = "The original independent adapter requires retired C# rules; no DSL replacement has been verified." }),
};
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
if (outputIndex >= 0 && outputIndex + 1 < args.Length)
{
    Directory.CreateDirectory(args[outputIndex + 1]);
    File.WriteAllText(Path.Combine(args[outputIndex + 1], "result.json"), json);
}
Console.WriteLine(json);
Console.Error.WriteLine("unsupported_capability: independent pure DSL comparison is not migrated. No legacy rules were executed.");
return 3;
