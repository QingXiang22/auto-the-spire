namespace CoreTheSpire.Engine.Tests;

internal sealed class CheckFailedException(string message) : Exception(message);

/// <summary>Minimal executable-check harness: prints one line per check and counts real executions.</summary>
internal static class Check
{
    private static int _passed;

    public static int Passed => _passed;

    public static void Run(string name, Action check)
    {
        try
        {
            check();
        }
        catch (CheckFailedException failure)
        {
            throw new CheckFailedException($"{name}: {failure.Message}");
        }
        catch (Exception exception)
        {
            throw new CheckFailedException(
                $"{name}: unexpected {exception.GetType().Name}: {exception.Message}{Environment.NewLine}" +
                exception.StackTrace);
        }

        _passed++;
        Console.WriteLine($"ok   {name}");
    }

    public static void True(bool value, string message)
    {
        if (!value) throw new CheckFailedException(message);
    }

    public static void False(bool value, string message) => True(!value, message);

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new CheckFailedException($"{message}: expected '{expected}', got '{actual}'");
    }

    /// <summary>Element-wise comparison. Named distinctly so it can never be confused with the scalar overload.</summary>
    public static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual)) throw new CheckFailedException($"{message}: sequences differ");
    }

    public static void Contains<T>(IEnumerable<T> values, Predicate<T> predicate, string message)
    {
        if (!values.Any(value => predicate(value))) throw new CheckFailedException(message);
    }

    public static void DoesNotContain<T>(IEnumerable<T> values, Predicate<T> predicate, string message)
    {
        if (values.Any(value => predicate(value))) throw new CheckFailedException(message);
    }

    public static T Throws<T>(Action action, string message) where T : Exception
    {
        try
        {
            action();
        }
        catch (T expected)
        {
            return expected;
        }
        catch (Exception other)
        {
            throw new CheckFailedException($"{message}: expected {typeof(T).Name}, got {other.GetType().Name}");
        }

        throw new CheckFailedException($"{message}: expected {typeof(T).Name}, nothing was thrown");
    }

    public static EngineFaultException Fault(EngineFaultCode code, Action action, string message)
    {
        var fault = Throws<EngineFaultException>(action, message);
        if (fault.Code != code)
            throw new CheckFailedException($"{message}: expected {code}, got {fault.Code} ({fault.Message})");
        return fault;
    }
}
