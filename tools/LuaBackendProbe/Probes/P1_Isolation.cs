using System;
using System.Collections.Generic;
using System.Diagnostics;
using MoonSharp.Interpreter;

// PROBE 1 -- Per-call environment isolation.
//
// The chunk mutates BOTH a module-level GLOBAL (reached through _ENV) and an UPVALUE captured
// by a closure it returns. If isolation holds, every call reports the initial values (1, 1).
//
// MoonSharp semantics established in P4_Diag:
//   * Script.LoadFunction(code, env, name) expects a FUNCTION EXPRESSION "function(...) ... end"
//     and returns that function directly.
//   * Script.LoadString(code, env, name)  expects a CHUNK ("local x=1 return x") and returns the
//     chunk as a function; the chunk BODY re-runs on every Call(chunk).
//   * The env table passed at load time becomes _ENV for that compiled prototype. There is NO
//     fallback to Script.Globals (a bare env yields nil for any global name).
//   * Consequence: _ENV is bound at LOAD time, so a fresh environment requires a re-LOAD per call.
class P1_Isolation
{
    // Module-level upvalue + global. Returns a closure, so the host holds a real upvalue.
    const string CHUNK = @"
local upvalue = 0
return function()
    upvalue = upvalue + 1
    globalCounter = (globalCounter or 0) + 1
    return upvalue, globalCounter
end
";

    readonly record struct Obs(double Upvalue, double Global)
    {
        public override string ToString() => $"(upvalue={Upvalue}, global={Global})";
    }

    static Obs CallOnce(Script s, DynValue chunkFn)
    {
        var inner = s.Call(chunkFn);        // run chunk body -> fresh closure + fresh locals
        var r = s.Call(inner);              // invoke the returned closure
        return new Obs(r.Tuple[0].Number, r.Tuple[1].Number);
    }

    static Obs CallClosure(Script s, DynValue closure)
    {
        var r = s.Call(closure);
        return new Obs(r.Tuple[0].Number, r.Tuple[1].Number);
    }

    // ---- the RECOMMENDED pattern, factored out so later probes can reuse it -------------
    public static DynValue LoadFreshChunk(Script script, string code, Table env, string name)
        => script.LoadString(code, env, name);

    public static void Run()
    {
        Console.WriteLine("Chunk under test:");
        Console.WriteLine(CHUNK);

        // ================================================================== A1 (recommended)
        Console.WriteLine("--- A1 RECOMMENDED: ONE long-lived Script + FRESH env table + RE-LOAD per call ---");
        {
            var script = new Script();
            for (int call = 1; call <= 3; call++)
            {
                var env = new Table(script);
                var chunk = script.LoadString(CHUNK, env, $"a1_call{call}");
                Console.WriteLine($"  call {call}: {CallOnce(script, chunk)}   env#{env.GetHashCode():X}");
            }
            Console.WriteLine("  => ISOLATED: every call starts from (1, 1)");
        }

        // ================================================================== A2 (global leak)
        Console.WriteLine("--- A2 LEAK: env table REUSED across calls (re-load, same env) ---");
        {
            var script = new Script();
            var env = new Table(script);
            for (int call = 1; call <= 3; call++)
            {
                var chunk = script.LoadString(CHUNK, env, $"a2_call{call}");
                Console.WriteLine($"  call {call}: {CallOnce(script, chunk)}");
            }
            Console.WriteLine("  => global LEAKS (2nd/3rd call see stale globalCounter); upvalue resets because the closure is new");
        }

        // ================================================================== A3 (upvalue leak)
        Console.WriteLine("--- A3 LEAK: same returned CLOSURE invoked repeatedly (host cached it) ---");
        {
            var script = new Script();
            var env = new Table(script);
            var chunk = script.LoadString(CHUNK, env, "a3");
            var closure = script.Call(chunk);           // hold the closure
            for (int call = 1; call <= 3; call++)
                Console.WriteLine($"  call {call}: {CallClosure(script, closure)}");
            Console.WriteLine("  => UPVALUE LEAKS: the captured local survives across calls of the cached closure");
        }

        // ================================================================== A4 (no re-load at all)
        Console.WriteLine("--- A4 LEAK: LoadString ONCE, then Call(chunk) repeatedly (env + proto shared) ---");
        {
            var script = new Script();
            var env = new Table(script);
            var chunk = script.LoadString(CHUNK, env, "a4");
            for (int call = 1; call <= 3; call++)
                Console.WriteLine($"  call {call}: {CallOnce(script, chunk)}");
            Console.WriteLine("  => global LEAKS; chunk body re-runs so the upvalue resets");
        }

        // ================================================================== B (fresh Script)
        Console.WriteLine("--- B: FRESH Script per call ---");
        for (int call = 1; call <= 3; call++)
        {
            var script = new Script();
            var env = new Table(script);
            var chunk = script.LoadString(CHUNK, env, $"b_call{call}");
            Console.WriteLine($"  call {call}: {CallOnce(script, chunk)}");
        }
        Console.WriteLine("  => ISOLATED, but pays full Lua-state construction per call");

        // ================================================================== C (host callback state)
        Console.WriteLine("--- C: does the SAME long-lived Script share anything else? (host callback identity) ---");
        {
            var script = new Script();
            int hostCalls = 0;
            for (int call = 1; call <= 3; call++)
            {
                var env = new Table(script);
                env.Set("hostTick", DynValue.NewCallback((ctx, a) => { hostCalls++; return DynValue.NewNumber(hostCalls); }));
                var chunk = script.LoadString("return hostTick(), hostTick()", env, $"c_call{call}");
                var r = script.Call(chunk);
                Console.WriteLine($"  call {call}: hostTick()x2 => {r.Tuple[0].Number}, {r.Tuple[1].Number}  (host counter now {hostCalls})");
            }
            Console.WriteLine("  => host-side closures are NOT retained by Lua; nothing survives between calls");
        }

        // ================================================================== cost
        Console.WriteLine("--- COST: re-load per call vs call-only (1000 iterations, trivial script) ---");
        {
            const int N = 1000;
            var script = new Script();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                var env = new Table(script);
                var chunk = script.LoadString("return 1+1", env, "perf_load");
                script.Call(chunk);
            }
            sw.Stop();
            double loadPlusCall = sw.Elapsed.TotalMilliseconds;

            var cachedChunk = script.LoadString("return 1+1", new Table(script), "perf_cached");
            sw.Restart();
            for (int i = 0; i < N; i++) script.Call(cachedChunk);
            sw.Stop();
            double callOnly = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            for (int i = 0; i < N; i++) { var s2 = new Script(); var e2 = new Table(s2); s2.Call(s2.LoadString("return 1+1", e2, "p")); }
            sw.Stop();
            double freshScript = sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"  A1 (long-lived Script, re-load per call) : {loadPlusCall,8:F2} ms / {N}  ({loadPlusCall / N * 1000:F1} us per call)");
            Console.WriteLine($"  cached chunk, Call only                  : {callOnly,8:F2} ms / {N}  ({callOnly / N * 1000:F1} us per call)");
            Console.WriteLine($"  B  (fresh Script per call)               : {freshScript,8:F2} ms / {N}  ({freshScript / N * 1000:F1} us per call)");
            Console.WriteLine($"  => parse overhead per call ~= {(loadPlusCall - callOnly) / N * 1000:F1} us (the price of a fresh _ENV)");
        }
    }
}
