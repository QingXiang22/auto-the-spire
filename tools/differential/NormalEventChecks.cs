#if REFERENCE_SIM
using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalEventChecks
{
    internal static void Run(NormalDslRun left, NormalReferenceRun right)
    {
        var boundary = left.Capture("event-check"); var native = right.Capture("event-check");
        var records = left.DebugRecords; var journal = NormalReferenceHookChecks.Read(native);
        var projection = left.ProjectEvents(records); var expected = NormalEventTrace.Canonical(projection);
        Require(expected == NormalEventTrace.Canonical(right.ProjectEvents(journal)) && projection.Events.Length > 300,
            "actual complete normal path has matching semantic occurrences");
        foreach (var kind in new[] { "paid", "card-body-start", "play-finished", "move-prepare-end", "death", "power-removed", "combat-ended", "victory-reaction-end" })
        {
            var index = Array.FindIndex(projection.Events, e => e.Kind == kind);
            Require(index >= 0, "real path executes " + kind);
            Changed(projection with { Events = projection.Events.Where((_, i) => i != index).ToArray(), Sources = projection.Sources[..^1] }, "missing " + kind);
        }
        var reordered = projection.Events.ToArray();
        var prepare = Array.FindIndex(reordered, e => e.Kind == "move-prepare-start");
        (reordered[prepare], reordered[prepare - 1]) = (reordered[prepare - 1], reordered[prepare]);
        Changed(projection with { Events = reordered }, "move preparation before side-start boundary");
        reordered = projection.Events.ToArray();
        var modifier = Array.FindIndex(reordered, e => e.Kind == "draw-modifier");
        (reordered[modifier], reordered[modifier + 1]) = (reordered[modifier + 1], reordered[modifier]);
        Changed(projection with { Events = reordered }, "listener result after its consumer");
        Reject(() => NormalEventTrace.Canonical(projection with { Unmapped = ["unknown override"] }), "unmapped zero-delta override");

        var damage = records.First(r => r.Kind == SessionDebugKind.EventEmit && r.Subject == "base-spire:DamageResolved");
        var changed = records.Select(r => r != damage ? r : r with { Snapshot = r.Snapshot! with { EntitiesTruncated = true } }).ToArray();
        Reject(() => NormalEventTrace.Canonical(left.ProjectEvents(changed)), "truncated historical payload");
        changed = records.Select(r => r != damage ? r : r with { TextTruncated = true }).ToArray();
        Reject(() => NormalEventTrace.Canonical(left.ProjectEvents(changed)), "truncated occurrence text");
        changed = records.Select(r => r != damage ? r : r with { Snapshot = r.Snapshot! with
        {
            Entities = r.Snapshot.Entities.Select(e => e.Type != "base-spire:DamageResult" ? e : e with
            { Fields = e.Fields.Select(f => f.Name != "Unblocked" ? f : f with { Value = f.Value with { Scalar = "999" } }).ToArray() }).ToArray()
        } }).ToArray();
        Changed(left.ProjectEvents(changed), "historical damage mutation after its entity was destroyed");
        var lostFinish = records.Where(r => r.Kind != SessionDebugKind.ProcedureExit || r.Subject != "base-spire:FinishCardPlay").ToArray();
        Reject(() => NormalEventTrace.Canonical(left.ProjectEvents(lostFinish)), "accepted action without its real completion");

        var nativeRecords = journal.Records;
        var unknown = nativeRecords.First(r => r.Kind == "observer");
        var unknownRecords = nativeRecords.Select(r => r != unknown ? r : r with { Name = "FutureUnknownNotification" }).ToArray();
        Reject(() => NormalEventTrace.Canonical(right.ProjectEvents(journal with { Records = unknownRecords })), "unknown native notification");
        var sidePoint = Array.FindIndex(nativeRecords, r => r.Kind == "observer" && r.Name == "SideTurnEndListener" && r.Arguments[^1].Scalar == "true");
        var call = nativeRecords[sidePoint + 1].Call;
        var removed = nativeRecords.Where(r => r.Call != call).Select((r, i) => r with { Sequence = i + 1 }).ToArray();
        Reject(() => NormalEventTrace.Canonical(right.ProjectEvents(journal with { Records = removed })), "both sides of a listener silently bypassed");
        var beforePlay = nativeRecords.First(r => r.Stage == "enter" && r.Kind == "dispatch" && r.Name == "BeforeCardPlayed");
        var ending = nativeRecords.Select(r => r != beforePlay ? r : r with
        {
            Arguments = r.Arguments.Select(v => v.Fields?.ContainsKey("ending") != true ? v : v with
            { Fields = v.Fields.ToDictionary(p => p.Key, p => p.Key == "ending" ? p.Value with { Scalar = "true" } : p.Value) }).ToArray()
        }).ToArray();
        Reject(() => NormalEventTrace.Canonical(right.ProjectEvents(journal with { Records = ending })), "native play starting while combat is ending");
        var draw = nativeRecords.First(r => r.Stage == "exit" && r.Method?.DeclaringType == "Sts2Sim.Core.Models.Relics.BoomingConch" && r.Name == "ModifyHandDraw");
        var changedResult = nativeRecords.Select(r => r != draw ? r : r with { Result = r.Result! with { Scalar = "999" } }).ToArray();
        Changed(right.ProjectEvents(journal with { Records = changedResult }), "actual listener result, including a zero-delta override");
        var side = nativeRecords.First(r => r.Stage == "enter" && r.Kind == "dispatch" && r.Name == "BeforeSideTurnStart");
        var participants = nativeRecords.Select(r => r != side ? r : r with
        { Arguments = r.Arguments[..^1].Append(r.Arguments[^1] with { Items = [] }).ToArray() }).ToArray();
        Changed(right.ProjectEvents(journal with { Records = participants }), "actual side participants");

        var beforeState = left.StateHash; var beforeRandom = left.RandomState;
        Require(left.CheckRequestReplay().Passed, "actual duplicate/stale protocol requests");
        Require(left.StateHash == beforeState && left.RandomState == beforeRandom && left.Capture("repeat").Values["events.trace"] == expected,
            "replay and repeated historical projection preserve state, RNG and occurrences");
        Require(right.Capture("repeat").Values["events.trace"] == native.Values["events.trace"], "native read-only queries do not fabricate gameplay occurrences");
        Require(left.Capture("restored").Values["events.trace"] == boundary.Values["events.trace"], "mutating diagnostic copies never changes real history");
        Console.WriteLine($"Normal events: {projection.Events.Length} actual occurrences; missing/order/ending/replay/history/truncation/unknown-listener checks passed.");

        void Changed(NormalEventProjection altered, string name)
        {
            try { Require(NormalEventTrace.Canonical(altered) != expected, name); }
            catch (NotSupportedException) { }
        }
    }
    private static void Reject(Action action, string name)
    {
        var rejected = false;
        try { action(); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, name);
    }
    private static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Normal event check failed: " + name); }
}
#endif
