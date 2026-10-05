namespace BaseSpire.Slice.Tests;

internal static class Program
{
    public static void Main()
    {
        SliceFrontendCompatibilityChecks.Run();
        Console.WriteLine($"All {Check.Passed} archived frontend compatibility checks passed.");
    }
}
