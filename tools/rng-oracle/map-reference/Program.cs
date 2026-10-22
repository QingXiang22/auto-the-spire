using System.Text.Json;
using BaseSpire.Slice.SourceMap;

if (args.Length != 2) throw new ArgumentException("Expected input and output JSON paths.");
using var input = JsonDocument.Parse(File.ReadAllText(args[0]));
var vectors = new List<object>();
foreach (var vector in input.RootElement.GetProperty("vectors").EnumerateArray())
{
    var words = vector.GetProperty("payloads").EnumerateArray().Select(v => v.GetUInt64()).ToArray();
    var rng = new SourceMapRandom(words);
    var rest = rng.NextGaussianInt(7, 1, 6, 7);
    var unknown = rng.NextGaussianInt(12, 1, 10, 14);
    if (Environment.GetEnvironmentVariable("MAP_REFERENCE_DIAGNOSTICS") == "1")
    {
        var probeRng = new SourceMapRandom(words, rng.DrawsUsed);
        var unpruned = new StandardActMap(probeRng, 15, new MapPointTypeCounts(unknown, rest), enablePruning: false);
        Console.Error.WriteLine($"{vector.GetProperty("seed")}: unpruned paths={MapPathPruning.FindAllPaths(unpruned.StartingMapPoint).Count}, nodes={unpruned.GetAllMapPoints().Count()}, draws={rng.DrawsUsed + probeRng.DrawsUsed}");
    }
    var map = SourceActMapGenerator.Generate(words, rng.DrawsUsed, 15, new MapPointTypeCounts(unknown, rest));
    vectors.Add(new { seed = vector.GetProperty("seed").GetString(), rest_count = rest, unknown_count = unknown,
        draws = rng.DrawsUsed + map.WordsConsumed, cells = map.Cells.OrderBy(c => c.Row).ThenBy(c => c.Col)
            .Select(c => new { col = c.Col, row = c.Row, type = c.Type, parents = c.ParentColsMask, children = c.ChildColsMask }).ToArray() });
}
File.WriteAllText(args[1], JsonSerializer.Serialize(new { format = "source-act-one-map-vectors@1",
    reference_kind = "isolated-source-map-port", source_sha256 = input.RootElement.GetProperty("source_sha256"), vectors },
    new JsonSerializerOptions { WriteIndented = true }));
