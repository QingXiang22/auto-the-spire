using CoreTheSpire.Engine;
using BaseSpire.Slice;

namespace BaseSpire.Slice.Tests;

internal sealed class CheckFailedException(string message) : Exception(message);

/// <summary>Minimal executable-check harness: one printed line per check and a real execution count.</summary>
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

    public static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new CheckFailedException(
                $"{message}: expected [{string.Join(",", expected)}], got [{string.Join(",", actual)}]");
        }
    }

    public static void Contains<T>(IEnumerable<T> values, Predicate<T> predicate, string message)
    {
        if (!values.Any(value => predicate(value))) throw new CheckFailedException(message);
    }

    public static void DoesNotContain<T>(IEnumerable<T> values, Predicate<T> predicate, string message)
    {
        if (values.Any(value => predicate(value))) throw new CheckFailedException(message);
    }

    public static EngineFaultException Fault(EngineFaultCode code, Action action, string message)
    {
        EngineFaultException? fault = null;
        try
        {
            action();
        }
        catch (EngineFaultException expected)
        {
            fault = expected;
        }
        catch (Exception other)
        {
            throw new CheckFailedException($"{message}: expected {code}, got {other.GetType().Name}");
        }

        if (fault is null)
            throw new CheckFailedException($"{message}: expected {code}, nothing was thrown");
        if (fault.Code != code)
            throw new CheckFailedException($"{message}: expected {code}, got {fault.Code} ({fault.Message})");
        return fault;
    }
}
