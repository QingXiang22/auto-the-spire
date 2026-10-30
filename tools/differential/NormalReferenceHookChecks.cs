#if REFERENCE_SIM
using System.Text.Json;
using Sts2Sim.Core.Models;

namespace Differential;

internal static class NormalReferenceHookChecks
{
    internal static NormalNativeHookJournal Read(NormalBoundary boundary)
    {
        using var document = JsonDocument.Parse(NormalPreflight.Json(boundary.Diagnostic));
        return JsonSerializer.Deserialize<NormalNativeHookJournal>(document.RootElement.GetProperty("native_hook_journal"))!;
    }

    internal static void Run(NormalBoundary boundary)
    {
        var journal = Read(boundary); var records = journal.Records; var targets = journal.Provenance.Targets;
        Require(records.Length > 1000 && journal.Audit.Notifications > 700 && targets.Length > 1000, "real normal run records the complete hook surface and notifications");
        Require(NormalPreflight.Json(NormalReferenceHookAudit.Check(records, targets)) == NormalPreflight.Json(journal.Audit), "published audit describes the actual call journal");
        Require(journal.Audit.DefaultImplementations.All(c => c.Method.DeclaringType == typeof(AbstractModel).FullName)
            && journal.Audit.Overrides.All(c => c.Method.DefaultImplementation is null), "only actual base implementations may be classified as defaults");
        Require(journal.Audit.UnclassifiedBase.Any(c => c.Method.Signature.StartsWith("AfterDeath(", StringComparison.Ordinal)), "virtual forwarding is retained for semantic audit");
        var zero = records.First(r => r.Stage == "enter" && r.Method?.DeclaringType == "Sts2Sim.Core.Models.Relics.BoomingConch"
            && r.Name == "AfterSideTurnStart" && r.Arguments[0].Scalar == "Enemy");
        Require(zero.Method!.DefaultImplementation is null, "a true override on its inactive side is never an empty default");
        var draw = records.First(r => r.Kind == "dispatch" && r.Stage == "exit" && r.Name == "ModifyHandDraw");
        Require(draw.Result?.Scalar == "5" && draw.Arguments[^1] is { Kind: "sequence", Items.Length: 0 },
            "value-only patching preserves the real hand-draw result and its nonnull out collection");
        Require(records.Any(r => r.Context == "query") && records.Any(r => r.Context == "gameplay"), "query hooks retain a distinct actual origin");
        var entryIndex = Array.FindIndex(records, r => r.Call == zero.Call && r.Stage == "enter");
        var exitIndex = Array.FindIndex(records, r => r.Call == zero.Call && r.Stage == "exit");
        Reject(records.Where((_, i) => i != entryIndex).ToArray(), targets, "missing listener entry");
        Reject(records.Where((_, i) => i != exitIndex).ToArray(), targets, "missing listener completion");
        Reject(records[..exitIndex], targets, "truncated pending listener");
        var changed = records.ToArray(); (changed[entryIndex], changed[exitIndex]) = (changed[exitIndex], changed[entryIndex]);
        Reject(changed, targets, "reordered listener calls");
        changed = records.ToArray(); changed[exitIndex] = changed[exitIndex] with { Receiver = zero.Receiver! with { Reference = zero.Receiver.Reference + 1 } };
        Reject(changed, targets, "completion from another instance");
        changed = records.ToArray(); changed[entryIndex] = zero with { Arguments = [] };
        Reject(changed, targets, "missing invocation arguments");
        changed = records.ToArray(); changed[exitIndex] = changed[exitIndex] with { Result = new("task", Scalar: "WaitingForActivation") };
        Reject(changed, targets, "task acceptance presented as execution completion");
        var falseDefault = zero.Method with { DefaultImplementation = "empty" };
        changed = records.Select(r => r.Method == zero.Method ? r with { Method = falseDefault } : r).ToArray();
        Reject(changed, targets.Select(m => m != zero.Method ? m : falseDefault).ToArray(), "zero-delta override relabeled as default");
        changed = records.ToArray(); changed[entryIndex] = zero with { Method = zero.Method with { BodyHash = "changed" } };
        Reject(changed, targets, "method not in the observed manifest");
        Console.WriteLine($"Normal native hook journal: {journal.Audit.Calls} paired calls, {journal.Audit.Notifications} notifications; missing/reordered/foreign calls and false default classifications rejected.");
    }

    private static void Reject(NormalNativeHookRecord[] records, NormalHookMethod[] targets, string name)
    {
        var rejected = false;
        try { NormalReferenceHookAudit.Check(records, targets); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, name);
    }
    private static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Native hook journal check failed: " + name); }
}
#endif
