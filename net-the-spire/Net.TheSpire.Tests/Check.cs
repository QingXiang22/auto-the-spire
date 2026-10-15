using CoreTheSpire.Engine;
using NetTheSpire.Contracts;
using NetTheSpire.Projection;

namespace NetTheSpire.Tests;

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

    /// <summary>断言失败信息里带 rule id 的契约故障；<paramref name="ruleIds"/> 为空时不约束规则号。</summary>
    public static WireFaultException Fault(Action action, string message, params string[] ruleIds)
    {
        WireFaultException? fault = null;
        try
        {
            action();
        }
        catch (WireFaultException expected)
        {
            fault = expected;
        }
        catch (Exception other)
        {
            throw new CheckFailedException(
                $"{message}: expected WireFaultException, got {other.GetType().Name}: {other.Message}");
        }

        if (fault is null) throw new CheckFailedException($"{message}: 期望拒绝，但被接受");
        if (ruleIds.Length > 0 && !ruleIds.Contains(fault.RuleId, StringComparer.Ordinal))
            throw new CheckFailedException(
                $"{message}: 期望规则 {string.Join('/', ruleIds)}，得到 {fault.RuleId} ({fault.Message})");
        return fault;
    }

    /// <summary>断言映射层按登记的规则号显式拒绝（含 L3 风格路径，可空）。</summary>
    public static PublicMappingException MappingFault(Action action, string message, string expectedRuleId)
    {
        PublicMappingException? fault = null;
        try
        {
            action();
        }
        catch (PublicMappingException expected)
        {
            fault = expected;
        }
        catch (Exception other)
        {
            throw new CheckFailedException(
                $"{message}: 期望 PublicMappingException，得到 {other.GetType().Name}: {other.Message}");
        }

        if (fault is null) throw new CheckFailedException($"{message}: 期望拒绝，但被接受");
        if (!string.Equals(fault.RuleId, expectedRuleId, StringComparison.Ordinal))
            throw new CheckFailedException($"{message}: 期望规则 {expectedRuleId}，得到 {fault.RuleId} ({fault.Message})");
        return fault;
    }

    /// <summary>断言引擎按期待的故障码拒绝。</summary>
    public static EngineFaultException EngineFault(EngineFaultCode expected, Action action, string message)
    {
        EngineFaultException? fault = null;
        try
        {
            action();
        }
        catch (EngineFaultException failure)
        {
            fault = failure;
        }
        catch (Exception other)
        {
            throw new CheckFailedException(
                $"{message}: 期望 EngineFaultException，得到 {other.GetType().Name}: {other.Message}");
        }

        if (fault is null) throw new CheckFailedException($"{message}: 期望拒绝，但被接受");
        if (fault.Code != expected)
            throw new CheckFailedException($"{message}: 期望 {expected}，得到 {fault.Code} ({fault.Message})");
        return fault;
    }

    public static void Accept(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            throw new CheckFailedException($"{message}: 期望接受，得到 {exception.GetType().Name}: {exception.Message}");
        }
    }
}
