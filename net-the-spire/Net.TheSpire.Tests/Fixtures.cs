using System.Text;
using NetTheSpire.Contracts;

namespace NetTheSpire.Tests;

/// <summary>
/// 跨端对照所需资产的**只读**装载器。三类资产、一个来源：
/// <list type="bullet">
/// <item><c>tools/contract-check/fixtures/**</c>：R-P01 参考夹具（正反例、golden 向量），跨端对照的输入；</item>
/// <item><c>docs/specs/net/0.2/*.schema.json</c>：L1 结构契约，C# 端直接读同一份 schema，不另造形状；</item>
/// <item><c>net-the-spire/Net.TheSpire.Tests/fixtures/cross-end-*.json</c>：Python 参考实现产出的对照清单（期望值）。</item>
/// </list>
/// 本装载器不复制、不改写任何参考资产；清单与参考夹具不同源时以参考夹具（Python 侧）为准，
/// 由对照检查报出差异。
/// </summary>
internal sealed class Fixtures
{
    private Fixtures(
        string root,
        WireObject casesManifest,
        WireObject canonicalManifest,
        IReadOnlyDictionary<string, WireValue> baseDocuments,
        IReadOnlyList<WireObject> sourceCases,
        IReadOnlyDictionary<string, WireValue> canonicalById,
        IReadOnlyDictionary<string, WireValue> rawById,
        LiteSchema wireSchema,
        LiteSchema? coverageSchema,
        WireObject golden)
    {
        Root = root;
        CasesManifest = casesManifest;
        CanonicalManifest = canonicalManifest;
        BaseDocuments = baseDocuments;
        SourceCases = sourceCases;
        CanonicalById = canonicalById;
        RawById = rawById;
        WireSchema = wireSchema;
        CoverageSchema = coverageSchema;
        Golden = golden;
    }

    public string Root { get; }

    public WireObject CasesManifest { get; }

    public WireObject CanonicalManifest { get; }

    public IReadOnlyDictionary<string, WireValue> BaseDocuments { get; }

    public IReadOnlyList<WireObject> SourceCases { get; }

    public IReadOnlyDictionary<string, WireValue> CanonicalById { get; }

    public IReadOnlyDictionary<string, WireValue> RawById { get; }

    public LiteSchema WireSchema { get; }

    public LiteSchema? CoverageSchema { get; }

    public WireObject Golden { get; }

    public static Fixtures Load()
    {
        var root = FindRepositoryRoot();
        var checkDir = Path.Combine(root, "tools", "contract-check", "fixtures");
        var contractDir = Path.Combine(root, "docs", "specs", "net", "0.2");
        var testsDir = Path.Combine(root, "net-the-spire", "Net.TheSpire.Tests", "fixtures");

        var bases = new Dictionary<string, WireValue>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(Path.Combine(checkDir, "documents"), "*.json").OrderBy(p => p, StringComparer.Ordinal))
            bases[Path.GetFileNameWithoutExtension(path)] = ReadJson(path);

        var sourceCases = new List<WireObject>();
        foreach (var path in Directory.GetFiles(Path.Combine(checkDir, "cases"), "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            var bundle = (WireObject)ReadJson(path);
            foreach (var item in (bundle.Get("cases") as WireArray ?? new WireArray([])).Items.OfType<WireObject>())
                sourceCases.Add(item);
        }

        var casesManifest = (WireObject)ReadJson(Path.Combine(testsDir, "cross-end-cases.json"));
        var canonicalManifest = (WireObject)ReadJson(Path.Combine(testsDir, "cross-end-canonical.json"));

        return new Fixtures(
            root,
            casesManifest,
            canonicalManifest,
            bases,
            sourceCases,
            IndexById(canonicalManifest, "documents"),
            IndexById(canonicalManifest, "raw_documents"),
            LiteSchema.FromJson(ReadText(Path.Combine(contractDir, "wire.schema.json"))),
            File.Exists(Path.Combine(contractDir, "coverage.schema.json"))
                ? LiteSchema.FromJson(ReadText(Path.Combine(contractDir, "coverage.schema.json")))
                : null,
            (WireObject)ReadJson(Path.Combine(checkDir, "golden", "canonical-vectors.json")));
    }

    private static IReadOnlyDictionary<string, WireValue> IndexById(WireObject manifest, string key)
    {
        var index = new Dictionary<string, WireValue>(StringComparer.Ordinal);
        foreach (var entry in (manifest.Get(key) as WireArray ?? new WireArray([])).Items.OfType<WireObject>())
        {
            if (entry.Get("id") is WireString id) index[id.Value] = entry;
        }

        return index;
    }

    /// <summary>自测试程序集位置向上找仓库根（以 <c>AutoTheSpire.sln</c> 为标记），与调用时的工作目录无关。</summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AutoTheSpire.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new CheckFailedException(
            $"找不到仓库根：自 '{AppContext.BaseDirectory}' 向上未发现 AutoTheSpire.sln");
    }

    /// <summary>按 Python 侧 <c>json.load</c> 的语义读参考夹具（保留成员顺序、允许浮点字面量）。</summary>
    private static WireValue ReadJson(string path) => WireParser.ParseLenient(ReadText(path), Path.GetFileName(path));

    private static string ReadText(string path)
    {
        if (!File.Exists(path)) throw new CheckFailedException($"缺少跨端对照资产: {path}");
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(File.ReadAllBytes(path));
    }
}
