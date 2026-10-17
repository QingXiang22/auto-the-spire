using CoreTheSpire.Engine;

namespace BaseSpire.Slice.ShopFlowChecks;

/// <summary>
/// 最小可执行检查框架（与生产测试工程同一套口径：每条检查一行输出、统计真实执行数）。
/// 本工程不引用 `BaseSpire.Slice.Tests`，因此自带一份。
/// </summary>
internal static class Check
{
    private static int passed;
    private static int failed;

    public static int Passed => passed;

    public static int Failed => failed;

    public static void Run(string name, Action check)
    {
        try
        {
            check();
            passed++;
            Console.WriteLine($"ok   {name}");
        }
        catch (Exception exception)
        {
            failed++;
            Console.WriteLine($"FAIL {name}: {exception.Message}");
        }
    }

    public static void True(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void False(bool value, string message) => True(!value, message);

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'");
        }
    }

    public static void Sequence<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException(
                $"{message}: expected [{string.Join(",", expected)}], got [{string.Join(",", actual)}]");
        }
    }

    /// <summary>期望引擎故障（`Fail`/非法动作/不可提交的绑定都走这里），并返回故障码供对账。</summary>
    public static EngineFaultException Fault(Action action, string message)
    {
        try
        {
            action();
        }
        catch (EngineFaultException fault)
        {
            return fault;
        }
        catch (Exception other)
        {
            throw new InvalidOperationException($"{message}: 期望引擎故障，得到 {other.GetType().Name}: {other.Message}");
        }

        throw new InvalidOperationException($"{message}: 期望引擎故障，但提交成功");
    }
}
