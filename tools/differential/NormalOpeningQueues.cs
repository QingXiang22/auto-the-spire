using System.Globalization;
using CoreTheSpire.Engine;
#if REFERENCE_SIM
using System.Reflection;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Runs;
#endif

namespace Differential;

internal sealed record NormalOpeningQueueEntry(long Act, string Room, long Position, string Id);

internal static class NormalOpeningQueues
{
    internal static NormalOpeningQueueEntry[] Canonical(IEnumerable<NormalOpeningQueueEntry> entries) => entries
        .OrderBy(e => e.Act).ThenBy(e => e.Room, StringComparer.Ordinal).ThenBy(e => e.Position).ToArray();

    internal static void Validate(NormalWeightedCall[] calls, NormalOpeningAttempt[] attempts, NormalOpeningQueueEntry[] queues)
    {
        if (calls.Length != attempts.Length) throw Missing("opening attempt/queue coverage");
        var positions = new Dictionary<(long Act, string Room), long>();
        var accepted = calls.Zip(attempts).Where(pair => pair.Second.Accepted).Select(pair =>
        {
            if (pair.First.Before != pair.Second.Before) throw Missing("opening attempt/queue cursor");
            var selected = pair.First.Candidates.Entries[pair.First.Selected];
            var key = (selected.Act, selected.Room); var position = positions.GetValueOrDefault(key);
            positions[key] = position + 1;
            return new NormalOpeningQueueEntry(selected.Act, selected.Room, position, selected.Id);
        });
        if (!Canonical(accepted).SequenceEqual(Canonical(queues))) throw Missing("actual opening queues differ from accepted draws or commit order");
    }

    internal static void ValidateCleanup(IReadOnlyList<SessionDebugRecord> records, Func<int, bool> exists)
    {
        var bags = records.Where(r => r.Kind == SessionDebugKind.ProcedureEnter && r.Subject == "base-spire:PickOpeningEncounter").Select(r =>
        {
            if (r.TextTruncated || r.Detail is not { } detail) throw Missing("opening bag identity");
            var start = detail.IndexOf("; arguments=[#", StringComparison.Ordinal);
            var end = start < 0 ? -1 : detail.IndexOf(',', start);
            return start >= 0 && end > start && int.TryParse(detail.AsSpan(start + 14, end - start - 14), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? id : throw Missing("opening bag argument format");
        }).Distinct().ToArray();
        if (bags.Length != 9 || bags.Any(exists)) throw Missing("nine temporary DSL opening bags must be destroyed after queue generation");
    }

#if REFERENCE_SIM
    internal static NormalOpeningQueueEntry[] Reference(RunState run)
    {
        var rooms = typeof(RunState).GetField("_generatedRooms", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(run) as Array
            ?? throw Missing("native generated room queues");
        var result = new List<NormalOpeningQueueEntry>();
        for (var act = 0; act < rooms.Length; act++)
        {
            var room = rooms.GetValue(act)!;
            foreach (var (property, kind) in new[] { ("NormalEncounters", "Monster"), ("EliteEncounters", "Elite") })
            {
                var entries = (IEnumerable<EncounterDefinition>)room.GetType().GetProperty(property)!.GetValue(room)!;
                result.AddRange(entries.Select((e, index) => new NormalOpeningQueueEntry(act, kind, index,
                    "base-spire:" + e.IdEntry.ToLowerInvariant().Replace('_', '-'))));
            }
        }
        return Canonical(result);
    }
#endif
    private static NotSupportedException Missing(string message) => new("missing_dimension: " + message);
}
