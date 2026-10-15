using System;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        var which = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
        void Run(string name, Action a)
        {
            if (which != "all" && which != name) return;
            Console.WriteLine();
            Console.WriteLine(new string('=', 78));
            Console.WriteLine($"PROBE {name}");
            Console.WriteLine(new string('=', 78));
            try { a(); }
            catch (Exception ex)
            {
                Console.WriteLine($"[UNCAUGHT {ex.GetType().FullName}] {ex.Message}");
            }
        }

        Run("p0", P0_Reflection.Run);
        Run("p0b", Scan.Run); Run("p4", P4_Diag.Run);
        Run("p1", P1_Isolation.Run);
        Run("p0c", Scan2.Run); Run("p2", P2_Budget.Run);
        Run("p3", P3_Numeric.Run);
        Run("p5", P5_Sandbox.Run);
        Run("p6", P6_Marshal.Run);
        Run("p7", P7_Determinism.Run);
    }
}
