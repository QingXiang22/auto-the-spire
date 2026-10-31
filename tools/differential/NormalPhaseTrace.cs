using CoreTheSpire.Engine;

namespace Differential;

internal static class NormalPhaseTrace
{
    internal static bool Relevant(SessionDebugRecord record) => record.Kind == SessionDebugKind.ProcedureEnter && record.Subject == "base-spire:RunEnemyTurn"
        || record.Kind == SessionDebugKind.ProcedureExit && record.Subject is "base-spire:BeginDevelopmentCombat" or "base-spire:DrawDevelopmentTurnHand"
            or "base-spire:RunEnemyTurn" or "base-spire:PerformEnemyMove" or "base-spire:BeginDevelopmentTurn";

    internal static NormalObservedEvent[] Merge(IReadOnlyList<SessionDebugRecord> records, IReadOnlyDictionary<long, NormalObservedEvent> emitted)
    {
        var result = new List<NormalObservedEvent>();
        NormalObservedEvent? move = null; var enemyPhase = false;
        foreach (var record in records)
        {
            if (emitted.TryGetValue(record.Sequence, out var observed))
            {
                if (observed.Kind == "enemy-move-started")
                {
                    if (!enemyPhase || move is not null) throw Missing("unmatched enemy move start");
                    move = observed;
                }
                result.Add(observed);
            }
            if (!Relevant(record)) continue;
            if (record.TextTruncated) throw Missing("truncated phase procedure");
            switch (record.Subject)
            {
                case "base-spire:BeginDevelopmentCombat": result.Add(new("combat-started")); break;
                case "base-spire:DrawDevelopmentTurnHand": result.Add(new("player-turn-prepared")); break;
                case "base-spire:BeginDevelopmentTurn": result.Add(new("player-turn-started")); break;
                case "base-spire:RunEnemyTurn" when record.Kind == SessionDebugKind.ProcedureEnter:
                    if (enemyPhase) throw Missing("nested enemy phase");
                    enemyPhase = true; result.Add(new("enemy-turn-starting")); break;
                case "base-spire:RunEnemyTurn":
                    if (!enemyPhase || move is not null) throw Missing("unmatched enemy phase finish");
                    enemyPhase = false; result.Add(new("player-turn-ended")); break;
                case "base-spire:PerformEnemyMove":
                    if (move is null) throw Missing("move finish without its actual start");
                    result.Add(move with { Kind = "enemy-move-finished" }); move = null; break;
            }
        }
        if (enemyPhase || move is not null) throw Missing("unfinished enemy phase");
        return result.ToArray();
    }

    private static NotSupportedException Missing(string detail) => new("missing_dimension: " + detail);
}
