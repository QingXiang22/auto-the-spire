using System;
using System.Linq;
using System.Reflection;
class Scan {
  public static void Run() {
    var asm = typeof(MoonSharp.Interpreter.Script).Assembly;
    Console.WriteLine("=== types/members mentioning Yield ===");
    foreach (var t in asm.GetTypes().Where(t => t.IsPublic)) {
      foreach (var m in t.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
        if (m.Name.Contains("Yield") || m.Name.Contains("AutoYield")) Console.WriteLine($"  {t.FullName}.{m}");
    }
    Console.WriteLine("=== DebuggerCaps values ===");
    var dc = asm.GetType("MoonSharp.Interpreter.Debugging.DebuggerCaps");
    foreach (var n in Enum.GetNames(dc)) Console.WriteLine($"  {n} = {(int)Enum.Parse(dc,n)}");
    Console.WriteLine("=== Processor / Script fields (all, incl. non-public) mentioning Debug/Yield ===");
    foreach (var t in new[]{"MoonSharp.Interpreter.Script","MoonSharp.Interpreter.Execution.VM.Processor"}) {
      var tt = asm.GetType(t);
      foreach (var f in tt.GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public))
        if (f.Name.Contains("Debug")||f.Name.Contains("Yield")||f.Name.Contains("Attach")) Console.WriteLine($"  {t}.{f.Name} : {f.FieldType.Name}");
      foreach (var p in tt.GetProperties(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public))
        if (p.Name.Contains("Debug")||p.Name.Contains("Yield")) Console.WriteLine($"  {t}.{p.Name} : {p.PropertyType.Name}");
    }
    Console.WriteLine("=== DynValue / Coroutine public API ===");
    var co = asm.GetType("MoonSharp.Interpreter.Coroutine");
    foreach (var m in co.GetMembers(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)) Console.WriteLine($"  {m.MemberType} {m}");
    Console.WriteLine("=== CoroutineState values ===");
    var cs = asm.GetType("MoonSharp.Interpreter.CoroutineState");
    foreach (var n in Enum.GetNames(cs)) Console.WriteLine($"  {n} = {(int)Enum.Parse(cs,n)}");
  }
}
