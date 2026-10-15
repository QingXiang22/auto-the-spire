using System;
using MoonSharp.Interpreter;

// PROBE 4 -- Determine the EXACT parsing contract of Script.LoadFunction vs Script.LoadString vs DoString,
// because the isolation pattern depends on which one produces a callable closure bound to a fresh env.
class P4_Diag
{
    static void Try(string label, Func<DynValue> f)
    {
        try
        {
            var v = f();
            Console.WriteLine($"  OK    {label,-62} -> {Describe(v)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL  {label,-62} -> {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
        }
    }

    static string Describe(DynValue v)
    {
        if (v == null) return "null";
        if (v.Type == DataType.Function) return "function";
        if (v.Type == DataType.Tuple)
        {
            var s = "";
            foreach (var t in v.Tuple) s += $"{t.Type}:{t.ToPrintString()} ";
            return "tuple[" + s.Trim() + "]";
        }
        return $"{v.Type}:{v.ToPrintString()}";
    }

    public static void Run()
    {
        Console.WriteLine("=== LoadFunction: is the argument a chunk body, or an expression? ===");
        Try("LoadFunction(\"1+2\")", () => { var s = new Script(); return s.LoadFunction("1+2", null, "t"); });
        Try("LoadFunction(\"return 1+2\")", () => { var s = new Script(); return s.LoadFunction("return 1+2", null, "t"); });
        Try("LoadFunction(\"local x=1 return x\")", () => { var s = new Script(); return s.LoadFunction("local x=1 return x", null, "t"); });
        Try("LoadFunction(\"x=1 return function() return x end\")", () => { var s = new Script(); return s.LoadFunction("x=1 return function() return x end", null, "t"); });
        Try("LoadFunction(\"return function() return 7 end\")", () => { var s = new Script(); return s.LoadFunction("return function() return 7 end", null, "t"); });

        Console.WriteLine("\n=== HYPOTHESIS: LoadFunction wants a FUNCTION EXPRESSION, i.e. 'function(...) ... end' ===");
        Try("LoadFunction(\"function() return 1+2 end\")", () => { var s = new Script(); return s.LoadFunction("function() return 1+2 end", null, "t"); });
        Try("LoadFunction(\"function(a,b) return a+b end\")", () => { var s = new Script(); return s.LoadFunction("function(a,b) return a+b end", null, "t"); });
        Try("LoadFunction(\"function() local c=0 return function() c=c+1 return c end end\")", () => { var s = new Script(); return s.LoadFunction("function() local c=0 return function() c=c+1 return c end end", null, "t"); });

        Console.WriteLine("\n=== LoadString: chunk semantics ===");
        Try("LoadString(\"return 1+2\")", () => { var s = new Script(); return s.LoadString("return 1+2", null, "t"); });
        Try("LoadString(\"local x=1 return x\")", () => { var s = new Script(); return s.LoadString("local x=1 return x", null, "t"); });
        Try("LoadString(\"local c=0 return function() c=c+1 return c end\")", () => { var s = new Script(); return s.LoadString("local c=0 return function() c=c+1 return c end", null, "t"); });

        Console.WriteLine("\n=== What does LoadFunction actually RETURN? Call it and see. ===");
        {
            var s = new Script();
            var fn = s.LoadFunction("function() return 11 end", null, "t");
            Console.WriteLine($"  LoadFunction(\"function() return 11 end\") type={fn.Type}  Call(fn) => {Describe(s.Call(fn))}");
        }
        {
            var s = new Script();
            var fn = s.LoadFunction("function(a,b) return a+b end", null, "t");
            Console.WriteLine($"  LoadFunction(\"function(a,b) return a+b end\") type={fn.Type}  Call(fn,3,4) => {Describe(s.Call(fn, 3, 4))}");
        }
        {
            // Does LoadFunction bind the pass-through of extra return values?
            var s = new Script();
            var fn = s.LoadFunction("function() return 1, 2, 3 end", null, "t");
            Console.WriteLine($"  LoadFunction multi-return type={fn.Type}  Call => {Describe(s.Call(fn))}");
        }

        Console.WriteLine("\n=== Env binding check: does the envTable passed to LoadString really become _ENV? ===");
        {
            var s = new Script();
            s.Globals.Set("HOSTONLY", DynValue.NewString("from-globals"));
            var env = new Table(s);
            env.Set("ENVONLY", DynValue.NewString("from-env"));

            // NOTE: a BARE env has no 'type', so this errors -- which is exactly the point.
            Try("  bare env: return type(HOSTONLY)", () => { var ss = new Script(); var e = new Table(ss); return ss.Call(ss.LoadString("return type(HOSTONLY)", e, "x")); });

            var fn = s.LoadString("return HOSTONLY, ENVONLY", env, "envtest");
            var r = s.Call(fn);
            Console.WriteLine($"  bare env: HOSTONLY={r.Tuple[0].ToPrintString()} (type={r.Tuple[0].Type})  ENVONLY={r.Tuple[1].ToPrintString()}");
            Console.WriteLine("  -> bare env table does NOT fall back to Script.Globals; HOSTONLY is nil inside the chunk");
        }

        Console.WriteLine("\n=== Does the env table need a __index metatable to reach Globals? ===");
        {
            var s = new Script();
            s.Globals.Set("HOSTONLY", DynValue.NewNumber(5));
            var env = new Table(s);
            env.MetaTable = new Table(s);
            env.MetaTable.Set("__index", DynValue.NewTable(s.Globals));
            var fn = s.LoadString("return HOSTONLY, type(HOSTONLY)", env, "metatest");
            var r = s.Call(fn);
            Console.WriteLine($"  env + __index=Globals => HOSTONLY={r.Tuple[0].ToPrintString()} type={r.Tuple[1].String}");
        }

        Console.WriteLine("\n=== Is a LoadString chunk re-callable (new closure each Call) or one-shot? ===");
        {
            var s = new Script();
            var env = new Table(s);
            var fn = s.LoadString("local c = 0 return function() c = c + 1 return c end", env, "closuretest");
            var inner = s.Call(fn);
            Console.WriteLine($"  Call(chunk) => {Describe(inner)}");
            Console.WriteLine($"  Call(inner) #1 => {Describe(s.Call(inner))}");
            Console.WriteLine($"  Call(inner) #2 => {Describe(s.Call(inner))}");
            Console.WriteLine($"  Call(inner) #3 => {Describe(s.Call(inner))}");
        }
    }
}
