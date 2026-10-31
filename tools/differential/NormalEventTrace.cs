namespace Differential;

internal sealed record NormalSemanticEvent(string Kind, string[] Data);
internal sealed record NormalEventSource(int Event, int Sequence, string Subject);
internal sealed record NormalEventExclusion(string Subject, string Reason, int Count);
internal sealed record NormalEventProjection(NormalSemanticEvent[] Events, NormalEventSource[] Sources, string[] Unmapped)
{
    public NormalEventExclusion[] Exclusions { get; init; } = [];
}

internal static class NormalEventTrace
{
    internal static NotSupportedException Missing(string reason) => new("missing_dimension: event trace " + reason);

    internal static string Canonical(NormalEventProjection projection)
    {
        if (projection.Unmapped.Length != 0) throw Missing(string.Join("; ", projection.Unmapped.Take(8)));
        if (projection.Sources.Length != projection.Events.Length
            || projection.Sources.Where((s, i) => s.Event != i || i > 0 && s.Sequence < projection.Sources[i - 1].Sequence).Any())
            throw Missing("missing/reordered occurrence sources");
        string? card = null; var stage = 0; string? attack = null; string? preparing = null;
        var living = new HashSet<string>(); var started = false; var ended = false; var playerSideStarting = false;
        foreach (var e in projection.Events)
        {
            var d = e.Data;
            void Require(bool condition) { if (!condition) throw Missing("invalid occurrence order/context at " + e.Kind); }
            switch (e.Kind)
            {
                case "enemy-added": Require(living.Add(d[0])); break;
                case "combat-started": Require(!started); started = true; break;
                case "side-before": Require(started && !ended && card is null); playerSideStarting = d[0] == "player"; break;
                case "side-after": playerSideStarting = false; break;
                case "move-prepare-start": Require(playerSideStarting && preparing is null && card is null && !ended); preparing = d[0]; break;
                case "move-prepare-end": Require(preparing == d[0]); preparing = null; break;
                case "play-request": Require(started && !ended && card is null); card = d[0]; stage = 1; break;
                case "paid": Require(card == d[0] && stage == 1); stage++; break;
                case "result-modifying": Require(card == d[0] && stage == 2); stage++; break;
                case "count-modifying": Require(card == d[0] && stage == 3); stage++; break;
                case "count-modified": Require(card == d[0] && stage == 4); stage++; break;
                case "before-play": Require(card == d[0] && stage == 5); stage++; break;
                case "card-body-start": Require(card == d[0] && stage == 6); stage++; break;
                case "card-body-end": Require(card == d[0] && stage == 7 && attack is null); stage++; break;
                case "after-play": Require(card == d[0] && stage == 8); stage++; break;
                case "play-finished": Require(card == d[0] && stage == 9); card = null; stage = 0; break;
                case "attack-start": Require(attack is null && !ended && d[5] == "1"); attack = d[0]; break;
                case "attack-end": Require(attack == d[0]); attack = null; break;
                case "damage": Require(d[12] == (attack ?? "-1") && d[13] == "0"); break;
                case "death": Require(living.Remove(d[0])); break;
                case "combat-ended": Require(started && !ended && card is null && attack is null && living.Count == 0); ended = true; break;
                case "combat-victory": Require(ended); break;
            }
        }
        if (card is not null || attack is not null || preparing is not null) throw Missing("unfinished semantic occurrence");
        return NormalPreflight.Json(projection.Events.Select(e => new { kind = e.Kind, data = e.Data }).ToArray());
    }
}
