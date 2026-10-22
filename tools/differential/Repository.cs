using System.Diagnostics;
using System.Text.Json;

namespace Differential;

internal static class Repository
{
    public static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "mod-the-spire/base-spire/package.json"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot locate the production DSL package.");
    }

    public static object VerifyReference(string repository)
    {
        var directory = Path.Combine(repository, "reference/sts2-sim");
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Independent reference checkout is missing.");
        string Git(params string[] args)
        {
            var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add("-C"); start.ArgumentList.Add(directory);
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException(error);
            return output.Trim();
        }
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository, "reference/baselines/sts2-sim.json")));
        var root = document.RootElement;
        var head = Git("rev-parse", "HEAD"); var tree = Git("rev-parse", "HEAD^{tree}");
        if (!Path.GetFullPath(Git("rev-parse", "--show-toplevel")).Equals(Path.GetFullPath(directory),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            || head != root.GetProperty("commit").GetString() || tree != root.GetProperty("tree").GetString()
            || Git("status", "--porcelain", "--untracked-files=all").Length != 0)
            throw new InvalidOperationException("Independent reference does not match the clean locked commit/tree.");
        return new { repository = root.GetProperty("repository").GetString(), commit = head, tree, clean = true,
            declared_game_baseline = root.GetProperty("declared_game_baseline").GetString(),
            rule_source = root.GetProperty("rule_source").GetString() };
    }
}
