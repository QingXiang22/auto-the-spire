using System;
using System.Linq;
using System.Reflection;

// Probe 0: reflect the MoonSharp assembly to establish the exact API surface available.
class P0_Reflection
{
    static Assembly Asm => typeof(MoonSharp.Interpreter.Script).Assembly;

    public static void Run()
    {
        var asm = Asm;
        Console.WriteLine($"Assembly      : {asm.GetName().Name} {asm.GetName().Version}");
        Console.WriteLine($"Location      : {asm.Location}");
        Console.WriteLine($"ImageRuntime  : {asm.ImageRuntimeVersion}");
        Console.WriteLine($"TargetFramework (attr): {asm.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName}");

        Console.WriteLine("\n=== DebuggerAction.ActionType enum values ===");
        var at = typeof(MoonSharp.Interpreter.Debugging.DebuggerAction.ActionType);
        Console.WriteLine($"IsEnum={at.IsEnum}  UnderlyingType={Enum.GetUnderlyingType(at).Name}");
        foreach (var n in Enum.GetNames(at))
            Console.WriteLine($"  {n} = {(int)Enum.Parse(at, n)}");

        Console.WriteLine("\n=== Task asked to try ActionType.Exit -- does it exist? ===");
        Console.WriteLine($"  Enum.IsDefined(ActionType, \"Exit\") = {Enum.IsDefined(at, "Exit")}");
        Console.WriteLine($"  Enum.IsDefined(ActionType, \"None\") = {Enum.IsDefined(at, "None")}");

        Console.WriteLine("\n=== IDebugger members (this is the ONLY debugger interface) ===");
        foreach (var m in typeof(MoonSharp.Interpreter.Debugging.IDebugger).GetMembers())
            Console.WriteLine($"  {m.MemberType,-8} {m}");

        Console.WriteLine("\n=== Look for 'ScriptDebugger' type anywhere in the assembly ===");
        var sd = asm.GetTypes().FirstOrDefault(t => t.Name == "ScriptDebugger");
        Console.WriteLine(sd == null
            ? "  NOT FOUND -- ScriptDebugger does not exist in MoonSharp 2.0.0"
            : $"  found {sd.FullName} abstract={sd.IsAbstract}");

        Console.WriteLine("\n=== ScriptDebugger-like base classes / any IDebugger implementations shipped ===");
        foreach (var t in asm.GetTypes().Where(t => typeof(MoonSharp.Interpreter.Debugging.IDebugger).IsAssignableFrom(t)))
            Console.WriteLine($"  {t.FullName} abstract={t.IsAbstract} public={t.IsPublic}");

        Console.WriteLine("\n=== Script number/load/options API ===");
        foreach (var name in new[] { "LoadFunction", "LoadString", "LoadFile", "AttachDebugger", "DoString", "Call", "Globals", "Options", "DebuggerEnabled" })
        {
            var found = typeof(MoonSharp.Interpreter.Script).GetMember(name, BindingFlags.Public | BindingFlags.Instance);
            if (found.Length == 0) Console.WriteLine($"  (missing) {name}");
            foreach (var m in found) Console.WriteLine($"  {m.MemberType,-8} {m}");
        }

        Console.WriteLine("\n=== ScriptOptions public props ===");
        foreach (var p in typeof(MoonSharp.Interpreter.ScriptOptions).GetProperties())
            Console.WriteLine($"  {p.PropertyType.Name,-30} {p.Name}");

        Console.WriteLine("\n=== Search whole assembly for quota/budget/instruction/cancel/abort/limit/timeout API ===");
        var hits = asm.GetTypes()
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => $"{t.FullName}.{m.Name}"))
            .Where(n => n.Contains("quota", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("budget", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("instruction", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Cancel", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Abort", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Limit", StringComparison.OrdinalIgnoreCase)
                     || n.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            .Distinct().OrderBy(x => x).ToList();
        Console.WriteLine(hits.Count == 0 ? "  (none found)" : string.Join("\n", hits.Select(h => "  " + h)));

        Console.WriteLine("\n=== ScriptRuntimeException public surface (host-side error type) ===");
        foreach (var m in typeof(MoonSharp.Interpreter.ScriptRuntimeException).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  {m.MemberType,-8} {m}");

        Console.WriteLine("\n=== InterpreterException hierarchy ===");
        foreach (var t in asm.GetTypes().Where(t => typeof(Exception).IsAssignableFrom(t) && t.IsPublic).OrderBy(t => t.FullName))
            Console.WriteLine($"  {t.FullName} : {t.BaseType?.Name}");
    }
}
