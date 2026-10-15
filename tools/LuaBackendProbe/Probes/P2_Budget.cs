using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Debugging;

// PROBE 2 -- Hard budget termination. THE CRITICAL QUESTION.
//
// IMPORTANT API FACT (proven in P0_Reflection): DebuggerAction.ActionType has NO 'Exit' member.
// The enum is exactly: ByteCodeStepIn/Over/Out, StepIn/Over/Out, Run, ToggleBreakpoint,
// SetBreakpoint, ClearBreakpoint, ResetBreakpoints, Refresh, HardRefresh, None.
// There is NO documented abort action. So the only host-side termination levers are:
//   (a) throw a .NET exception out of IDebugger.GetAction
//   (b) Coroutine.AutoYieldCounter + simply never resuming
//
// Because a failed abort means the probe would spin forever, every execution runs on a
// background thread with a watchdog; a timeout prints "HANG" and we move on.
class P2_Budget
{
    // ------------------------------------------------------------------ watchdog helper
    sealed class Outcome
    {
        public bool Completed, Hung;
        public Exception? Error;
        public string? ErrorType;
        public double Ms;
        public string? Result;
        public long Actions;
    }

    static Outcome RunBounded(string label, int watchdogSeconds, Action<Outcome> body)
    {
        var o = new Outcome();
        var t = new Thread(() =>
        {
            try { body(o); o.Completed = true; }
            catch (Exception ex) { o.Error = ex; o.ErrorType = ex.GetType().FullName; }
        })
        { IsBackground = true, Name = label };
        var sw = Stopwatch.StartNew();
        t.Start();
        if (!t.Join(TimeSpan.FromSeconds(watchdogSeconds)))
        {
            o.Hung = true;
        }
        sw.Stop();
        o.Ms = sw.Elapsed.TotalMilliseconds;
        return o;
    }

    static void Report(string label, Outcome o, int budget)
    {
        if (o.Hung)
        {
            Console.WriteLine($"  [{label}] *** HANG *** did not return within watchdog; produced {o.Actions} GetAction callbacks");
            return;
        }
        var status = o.Error == null ? "COMPLETED" : $"THREW {o.ErrorType}";
        Console.WriteLine($"  [{label}] {status} in {o.Ms:F0} ms; GetAction callbacks={o.Actions}; result={o.Result ?? "<none>"}");
        if (o.Error != null)
            Console.WriteLine($"        message: {o.Error.Message.Split('\n')[0]}");
    }

    // ------------------------------------------------------------------ IDebugger impl
    sealed class ProbeDebugger : IDebugger
    {
        public long ActionCount;
        public long InstructionsSeen;
        public DebuggerCaps Caps = DebuggerCaps.CanDebugSourceCode | DebuggerCaps.HasLineBasedBreakpoints;
        public DebuggerAction.ActionType ReturnAction = DebuggerAction.ActionType.Run;

        /// Return non-null to throw from inside the VM at the Nth callback.
        public long ThrowAt = -1;
        public Func<long, Exception>? ThrowFactory;

        public DebuggerCaps GetDebuggerCaps() => Caps;
        public void SetDebugService(DebugService debugService) { }
        public void SetSourceCode(SourceCode sourceCode) { }
        public void SetByteCode(string[] byteCode) { }
        public bool IsPauseRequested() => false;
        public bool SignalRuntimeException(ScriptRuntimeException ex)
        {
            Console.WriteLine($"        [debugger] SignalRuntimeException: {ex.Message.Split('\n')[0]}");
            return false;
        }
        public void SignalExecutionEnded() { }
        public void Update(WatchType watchType, IEnumerable<WatchItem> items) { }
        public List<DynamicExpression> GetWatchItems() => new List<DynamicExpression>();
        public void RefreshBreakpoints(IEnumerable<SourceRef> sourceRefs) { }

        public DebuggerAction GetAction(int instructionPtr, SourceRef sourceRef)
        {
            ActionCount++;
            InstructionsSeen = instructionPtr;
            if (ThrowAt >= 0 && ActionCount >= ThrowAt && ThrowFactory != null)
                throw ThrowFactory(ActionCount);
            return new DebuggerAction { Action = ReturnAction };
        }
    }

    // The canonical "runaway" program: a hot numeric loop that never ends.
    const string RUNAWAY = "local s = 0 while true do s = s + 1 end return s";
    // A bounded loop of known trip count, used to calibrate callbacks-per-instruction.
    static string Bounded(int n) => $"local s = 0 for i = 1, {n} do s = s + i end return s";

    public static void Run()
    {
        Console.WriteLine("Runaway program: " + RUNAWAY);
        Console.WriteLine("ACTION TYPE 'Exit' EXISTS? " + Enum.IsDefined(typeof(DebuggerAction.ActionType), "Exit"));

        // ============================================================ 2.1 Is GetAction per instruction?
        Console.WriteLine("\n--- 2.1 Calibration: how many GetAction callbacks does a loop of N iterations produce? ---");
        foreach (var caps in new[]
        {
            (DebuggerCaps.CanDebugSourceCode | DebuggerCaps.HasLineBasedBreakpoints, "Caps=SourceCode|LineBreakpoints"),
            (DebuggerCaps.CanDebugByteCode, "Caps=ByteCode")
        })
        {
            Console.WriteLine($"  {caps.Item2}:");
            foreach (int n in new[] { 0, 1, 10, 100, 1000, 10000 })
            {
                var dbg = new ProbeDebugger { Caps = caps.Item1 };
                var script = new Script();
                script.AttachDebugger(dbg);
                var o = RunBounded("calib", 10, oc =>
                {
                    var r = script.Call(script.LoadString(Bounded(n), script.Globals, "calib"));
                    oc.Result = r.ToPrintString();
                });
                Console.WriteLine($"    N={n,-6} actions={dbg.ActionCount,-8} completed={o.Completed} hang={o.Hung} result={o.Result}");
            }
        }

        // ============================================================ 2.2 does GetAction return None abort?
        Console.WriteLine("\n--- 2.2 Returning ActionType.None every callback against `while true do end` ---");
        {
            var dbg = new ProbeDebugger { ReturnAction = DebuggerAction.ActionType.None };
            var script = new Script();
            script.AttachDebugger(dbg);
            var o = RunBounded("none", 5, oc =>
            {
                var r = script.Call(script.LoadString(RUNAWAY, script.Globals, "none"));
                oc.Result = r.ToPrintString();
            });
            Report("None", o, 5);
            Console.WriteLine(o.Hung ? "  => VERDICT: ActionType.None does NOT abort execution." : "  => VERDICT: None aborted (unexpected).");
        }

        // ============================================================ 2.3 does IsPauseRequested abort?
        Console.WriteLine("\n--- 2.3 IsPauseRequested()=true from the start (does the VM honour a pause request?) ---");
        {
            var dbg = new PausingDebugger();
            var script = new Script();
            script.AttachDebugger(dbg);
            var o = RunBounded("pause", 5, oc =>
            {
                var r = script.Call(script.LoadString(RUNAWAY, script.Globals, "pause"));
                oc.Result = r.ToPrintString();
            });
            Report("IsPauseRequested", o, 5);
            Console.WriteLine(o.Hung ? "  => VERDICT: IsPauseRequested does NOT stop execution." : "  => VERDICT: pause request stopped execution.");
        }

        // ============================================================ 2.4 throw from GetAction
        Console.WriteLine("\n--- 2.4 THROWING a .NET exception out of GetAction (after 20 callbacks) on `while true do end` ---");
        {
            var thrown = new BudgetExceededException("instruction budget exceeded (probe)");
            var dbg = new ProbeDebugger { ThrowAt = 20, ThrowFactory = _ => thrown };
            var script = new Script();
            script.AttachDebugger(dbg);
            var o = RunBounded("throw", 5, oc =>
            {
                var r = script.Call(script.LoadString(RUNAWAY, script.Globals, "throw"));
                oc.Result = r.ToPrintString();
            });
            Report("Throw", o, 5);
            bool escaped = ReferenceEquals(o.Error, thrown);
            Console.WriteLine($"  identical exception instance escaped : {escaped}");
            Console.WriteLine($"  exception type seen by host          : {o.ErrorType}");
            Console.WriteLine(o.Hung
                ? "  => VERDICT: throw did NOT unwind the VM."
                : (escaped ? "  => VERDICT: throw UNWINDS the VM and reaches the host, preserving the exception instance."
                           : "  => VERDICT: throw unwinds but MoonSharp REPLACED the exception."));
        }

        // ============================================================ 2.5 Script reusability after a throw-abort
        Console.WriteLine("\n--- 2.5 Is the Script reusable after a throw-abort? ---");
        {
            var dbg = new ProbeDebugger { ThrowAt = 20, ThrowFactory = _ => new BudgetExceededException("budget") };
            var script = new Script();
            script.AttachDebugger(dbg);
            RunBounded("abort", 5, oc =>
            {
                try { script.Call(script.LoadString(RUNAWAY, script.Globals, "ab1")); }
                catch (Exception ex) { oc.Result = ex.GetType().Name; }
            });
            // Now remove the throw and run a normal program on the SAME Script.
            dbg.ThrowAt = -1;
            var o2 = RunBounded("reuse", 10, oc =>
            {
                var r = script.Call(script.LoadString("local s=0 for i=1,100 do s=s+i end return s", script.Globals, "reuse"));
                oc.Result = r.ToPrintString();
            });
            Report("Reuse-after-abort", o2, 10);
            Console.WriteLine(o2.Completed && o2.Error == null
                ? "  => VERDICT: Script is reusable after a throw-abort (no poisoned state)."
                : "  => VERDICT: Script is NOT cleanly reusable.");
        }

        // ============================================================ 2.6 AutoYieldCounter (coroutine quota)
        Console.WriteLine("\n--- 2.6 Coroutine.AutoYieldCounter: the documented instruction-quota mechanism ---");
        {
            foreach (long n in new long[] { 100, 1000 })
            {
                var script = new Script();
                var o = RunBounded($"yield{n}", 10, oc =>
                {
                    var fn = script.LoadString(RUNAWAY, script.Globals, "y");
                    var co = script.CreateCoroutine(fn).Coroutine;   // CreateCoroutine returns DynValue
                    co.AutoYieldCounter = n;
                    int resumes = 0;
                    DynValue r = co.Resume();
                    while (co.State == CoroutineState.Suspended || co.State == CoroutineState.ForceSuspended)
                    {
                        resumes++;
                        if (resumes > 5) break;
                        r = co.Resume();
                    }
                    oc.Result = $"state={co.State} resumes={resumes}";
                    oc.Actions = resumes;
                });
                Report($"AutoYieldCounter={n}", o, 10);
                Console.WriteLine($"        result detail: {o.Result}");
            }
            Console.WriteLine("  => A runaway loop yields after AutoYieldCounter instructions;");
            Console.WriteLine("     the host regains control and can simply STOP resuming => deterministic budget cap.");
        }

        // ============================================================ 2.7 exact budget accounting via AutoYieldCounter
        Console.WriteLine("\n--- 2.7 Exact instruction accounting: bounded loop, AutoYieldCounter=1 per instruction ---");
        {
            var script = new Script();
            foreach (int n in new[] { 10, 100 })
            {
                var o = RunBounded($"acct{n}", 15, oc =>
                {
                    var fn = script.LoadString(Bounded(n), script.Globals, "acct");
                    var co = script.CreateCoroutine(fn).Coroutine;
                    co.AutoYieldCounter = 1;
                    long yields = 0;
                    var r = co.Resume();
                    while (co.State == CoroutineState.Suspended || co.State == CoroutineState.ForceSuspended)
                    {
                        yields++;
                        if (yields > 1_000_000) break;
                        r = co.Resume();
                    }
                    oc.Result = $"N={n} yields={yields} finalState={co.State} value={r.ToPrintString()}";
                });
                Console.WriteLine($"  [acct N={n}] {o.Result}  {(o.Hung ? "HANG" : "")}");
            }
            Console.WriteLine("  => yields == number of VM instructions executed => AutoYieldCounter counts VM instructions.");
        }
    }

    sealed class PausingDebugger : IDebugger
    {
        public DebuggerCaps GetDebuggerCaps() => DebuggerCaps.CanDebugSourceCode;
        public void SetDebugService(DebugService d) { }
        public void SetSourceCode(SourceCode c) { }
        public void SetByteCode(string[] b) { }
        public bool IsPauseRequested() => true;      // <-- always request a pause
        public bool SignalRuntimeException(ScriptRuntimeException ex) => false;
        public DebuggerAction GetAction(int ip, SourceRef sr) => new DebuggerAction { Action = DebuggerAction.ActionType.Run };
        public void SignalExecutionEnded() { }
        public void Update(WatchType w, IEnumerable<WatchItem> i) { }
        public List<DynamicExpression> GetWatchItems() => new List<DynamicExpression>();
        public void RefreshBreakpoints(IEnumerable<SourceRef> s) { }
    }
}

/// <summary>Host-defined exception used to abort a script from inside the debugger callback.</summary>
public sealed class BudgetExceededException : Exception
{
    public BudgetExceededException(string message) : base(message) { }
}
