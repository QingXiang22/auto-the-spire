using System.Security.Cryptography;
using System.Text;

namespace Differential;

/// <summary>一棵目录/一组文件的**内容摘要**：不依赖父 Git HEAD，未提交目录也有自己的版本身份。</summary>
internal sealed record TreeDigest(
    string Path,
    string Kind,
    bool Exists,
    int Files,
    long Bytes,
    string Sha256,
    int Skipped,
    IReadOnlyList<string> Errors);

internal static class Repo
{
    /// <summary>摘要时跳过的目录名（构建产物/编辑器缓存/外部 VCS 内部结构）。</summary>
    private static readonly HashSet<string> SkippedDirectories =
        new(["bin", "obj", ".git", ".godot", "node_modules", "__pycache__"], StringComparer.OrdinalIgnoreCase);

    /// <summary>摘要时跳过的扩展名（Godot 的 <c>.uid</c> 只是编辑器元数据，触碰它会污染源码摘要）。</summary>
    private static readonly HashSet<string> SkippedExtensions =
        new([".uid"], StringComparer.OrdinalIgnoreCase);

    public static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AutoTheSpire.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    public static string Sha256(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));

    public static string Sha256(string text) => Sha256(Encoding.UTF8.GetBytes(text));

    /// <summary>逐文件 sha256 汇总成一条摘要：先按相对路径排序，再对 <c>path\tsha\tbytes</c> 行取摘要。</summary>
    public static TreeDigest DigestTree(string relativePath, string kind = "source")
    {
        var root = Root();
        var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(full))
        {
            return new TreeDigest(relativePath, kind, false, 0, 0, "", 0, ["目录不存在"]);
        }

        var lines = new List<string>();
        var errors = new List<string>();
        var skipped = 0;
        long bytes = 0;

        foreach (var file in Enumerate(full))
        {
            if (SkippedExtensions.Contains(file.Extension))
            {
                skipped++;
                continue;
            }

            try
            {
                var content = File.ReadAllBytes(file.FullName);
                var relative = Path.GetRelativePath(root, file.FullName).Replace('\\', '/');
                lines.Add($"{relative}\t{Sha256(content)}\t{content.Length}");
                bytes += content.Length;
            }
            catch (IOException failure)
            {
                errors.Add($"{file.FullName}: {failure.Message}");
            }
        }

        lines.Sort(StringComparer.Ordinal);
        return new TreeDigest(relativePath, kind, true, lines.Count, bytes,
            Sha256(string.Join('\n', lines)), skipped, errors);
    }

    /// <summary>关键单文件摘要（用于把"身份定义处"钉住：ID 派生、聚合入口、版本证据）。</summary>
    public static IReadOnlyList<(string Path, bool Exists, long Bytes, string Sha256)> DigestFiles(
        IEnumerable<string> relativePaths)
    {
        var root = Root();
        var results = new List<(string, bool, long, string)>();
        foreach (var relative in relativePaths)
        {
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                results.Add((relative, false, 0, ""));
                continue;
            }

            var content = File.ReadAllBytes(full);
            results.Add((relative, true, content.Length, Sha256(content)));
        }

        return results;
    }

    /// <summary>
    /// 读一个 Git 工作区/子模块的 HEAD（直接读 <c>.git</c>，不启动进程）。
    /// <c>.git</c> 既可能是目录（普通仓库），也可能是文件（worktree/submodule，内容是 <c>gitdir: …</c>）。
    /// 读不到时返回 <c>null</c>，调用方必须把"没有版本身份"显式写进产物，而不是拿父仓库 HEAD 冒充。
    /// </summary>
    public static (string? Head, string? Source) GitHead(string relativePath)
    {
        var root = Root();
        var gitPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar), ".git");
        string? gitDirectory = null;
        if (Directory.Exists(gitPath))
        {
            gitDirectory = gitPath;
        }
        else if (File.Exists(gitPath))
        {
            foreach (var line in File.ReadAllLines(gitPath))
            {
                if (!line.StartsWith("gitdir:", StringComparison.Ordinal))
                {
                    continue;
                }

                var target = line["gitdir:".Length..].Trim();
                gitDirectory = Path.IsPathRooted(target)
                    ? target
                    : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(gitPath)!, target));
                break;
            }
        }

        if (gitDirectory is null)
        {
            return (null, null);
        }

        var headPath = Path.Combine(gitDirectory, "HEAD");
        if (!File.Exists(headPath))
        {
            return (null, null);
        }

        var head = File.ReadAllText(headPath).Trim();
        const string prefix = "ref:";
        if (!head.StartsWith(prefix, StringComparison.Ordinal))
        {
            return (head, Describe(relativePath, ".git/HEAD"));
        }

        var reference = head[prefix.Length..].Trim();
        var refPath = Path.Combine(gitDirectory, reference.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(refPath))
        {
            return (File.ReadAllText(refPath).Trim(), Describe(relativePath, $".git/{reference}"));
        }

        // 打包引用：packed-refs 里 "sha ref"。
        var packedPath = Path.Combine(gitDirectory, "packed-refs");
        if (File.Exists(packedPath))
        {
            foreach (var line in File.ReadAllLines(packedPath))
            {
                if (line.StartsWith('#') || line.Length == 0)
                {
                    continue;
                }

                var parts = line.Split(' ', 2);
                if (parts.Length == 2 && parts[1].Trim() == reference)
                {
                    return (parts[0].Trim(), Describe(relativePath, ".git/packed-refs"));
                }
            }
        }

        return (null, null);
    }

    /// <summary>把"仓库根"这种空相对路径拼成 <c>.git/HEAD</c> 而不是 <c>/.git/HEAD</c>。</summary>
    private static string Describe(string relativePath, string suffix) =>
        string.IsNullOrEmpty(relativePath) ? suffix : $"{relativePath}/{suffix}";

    private static IEnumerable<FileInfo> Enumerate(string directory)
    {
        var stack = new Stack<DirectoryInfo>();
        stack.Push(new DirectoryInfo(directory));
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            DirectoryInfo[] children;
            FileInfo[] files;
            try
            {
                children = current.GetDirectories();
                files = current.GetFiles();
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!SkippedDirectories.Contains(child.Name))
                {
                    stack.Push(child);
                }
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }
}
