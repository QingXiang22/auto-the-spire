using System.Text.Json;
using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;

namespace Differential;

// Transport IDs belong to the real Host handshake; game facts are compared through their
// already independent, complete semantic projections. Neither backend borrows the other's values.
internal static class NormalPublicSnapshot
{
    internal static void ValidateEnvelope(PublicSnapshotDto snapshot, IdentityDto expected, string sessionId)
    {
        if (snapshot.ProtocolVersion != NetProtocol.V02 || snapshot.ProtocolVersion != expected.ProtocolVersion
            || snapshot.RulesetId != expected.RulesetId || snapshot.CatalogId != expected.CatalogId
            || snapshot.BackendProfileId != expected.BackendProfileId || snapshot.SessionId != sessionId
            || string.IsNullOrWhiteSpace(sessionId) || snapshot.ObservationId < 0)
            throw new NotSupportedException("missing_dimension: public snapshot differs from its actual Host handshake/session");
        // These four normal paths stop on the map after one combat, while the Run is still live.
        // Terminal snapshots need their own native Run outcome mapping, rather than a null default.
        if (snapshot.Status != "decision" || snapshot.TerminalResult is not null || snapshot.LegalActions.Count == 0)
            throw new NotSupportedException("missing_dimension: normal prefix public status/outcome");
    }

    internal static bool Add(SortedDictionary<string, string> values, string[] unmappedFields, string status)
    {
        if (unmappedFields.Length != 0) return false;
        var paths = NormalRunComparison.RequiredPaths.Where(p => p.StartsWith("public.", StringComparison.Ordinal) && p != "public.snapshot").ToArray();
        if (paths.Any(p => !values.ContainsKey(p)) || !values.ContainsKey("actions.candidates") || !values.ContainsKey("decisions.current"))
            return false;
        if (status != "decision") throw new NotSupportedException("missing_dimension: unhandled native Run outcome");
        var projections = paths.Order(StringComparer.Ordinal).ToDictionary(p => p, p => Parse(values[p]), StringComparer.Ordinal);
        values.Add("public.snapshot", NormalPreflight.Json(new { status,
            projections, actions = Parse(values["actions.candidates"]), decision = Parse(values["decisions.current"]) }));
        return true;
        static JsonElement Parse(string text) { using var document = JsonDocument.Parse(text); return document.RootElement.Clone(); }
    }
}
