namespace CoreTheSpire.Engine.Tests;

/// <summary>
/// Executable check entry point for the generic runtime:
/// <c>dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests</c>.
/// A failing check throws, so the process exit code is non-zero and the message names the check.
/// </summary>
internal static class Program
{
    public static void Main()
    {
        var passed = EngineChecks.RunAll();
        Console.WriteLine($"All {passed} engine checks passed.");
    }
}
