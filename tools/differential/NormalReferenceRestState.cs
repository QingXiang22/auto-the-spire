#if REFERENCE_SIM
using Sts2Sim.Core.Runs;

namespace Differential;

internal sealed record NormalRestContext(int Act, int TotalFloor, int RoomCount, string? CurrentRoom, string[] VisitedTypes);
internal sealed record NormalRestFlags(bool RoomUsed, bool PendingForge);

internal static class NormalReferenceRestState
{
    internal static NormalRestContext Capture(RunState run) => new(run.CurrentActIndex, run.TotalFloor, run.CurrentRoomCount,
        run.CurrentRoom?.RoomType.ToString(), run.VisitedMapCoords.Select(c => run.Map.GetPoint(c)?.PointType.ToString()
            ?? throw Missing()).ToArray());

    // Rest-site decisions belong to a RestSiteRoom. Before any rest-site visit there
    // is neither a consumed rest option nor an outstanding smith target selection.
    // Unknown points could have resolved to a rest site; they cannot prove absence.
    internal static NormalRestFlags Normalize(NormalRestContext context)
    {
        if (context.Act != 0 || context.TotalFloor != context.VisitedTypes.Length
            || context.VisitedTypes.Length is < 1 or > 2 || context.VisitedTypes[0] != "Ancient"
            || context.VisitedTypes.Skip(1).Any(t => t != "Monster")
            || context.RoomCount != (context.CurrentRoom is null ? 0 : 1)
            || context.CurrentRoom is not (null or "Event" or "Monster")
            || context.CurrentRoom == "Event" && context.VisitedTypes.Length != 1
            || context.CurrentRoom == "Monster" && context.VisitedTypes.Length != 2)
            throw Missing();
        return new(false, false);
    }

    private static NotSupportedException Missing() => new("missing_dimension: native rest state outside opening and first Monster visit");
}
#endif
