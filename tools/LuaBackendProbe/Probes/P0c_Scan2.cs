using System; using System.Linq; using System.Reflection;
class Scan2 { public static void Run() {
  var asm = typeof(MoonSharp.Interpreter.Script).Assembly;
  var dv = typeof(MoonSharp.Interpreter.DynValue);
  Console.WriteLine("=== DynValue members mentioning Coroutine ===");
  foreach (var m in dv.GetMembers(BindingFlags.Public|BindingFlags.Instance)) if (m.Name.Contains("Coroutine")) Console.WriteLine("  "+m);
  var sc = typeof(MoonSharp.Interpreter.Script);
  Console.WriteLine("=== Script members mentioning Coroutine ===");
  foreach (var m in sc.GetMembers(BindingFlags.Public|BindingFlags.Instance)) if (m.Name.Contains("Coroutine")) Console.WriteLine("  "+m);
}}
