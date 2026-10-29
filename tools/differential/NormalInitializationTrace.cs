using System.Globalization;
using CoreTheSpire.Engine;

namespace Differential;

internal sealed record NormalOpeningSource(int Reference, string Category, long Act, string Id, string Room, string Rarity, long AssignedAct);
internal sealed record NormalOpeningList(string Key, string[] Ids);
internal sealed record NormalOpeningShuffle(string Key, long Before, long After, int Selected, string[] Candidates);
internal sealed record NormalAncientAllocation(long Act, long Before, long After, int Take, string[] Remaining);
internal sealed record NormalOpeningPick(long Act, string Room, long Before, long After, int Selected, string[] Candidates);
internal sealed record NormalInitializationDraw(string Operation, long Before, long After, double Minimum, double Maximum, int Arity, string Result);
internal sealed record NormalInitializationCandidates(NormalOpeningShuffle[] Shuffles, NormalAncientAllocation[] Allocations, NormalOpeningPick[] Picks);
internal sealed record NormalInitializationSources(NormalOpeningSource[] Definitions, NormalOpeningList[] Bags, NormalOpeningQueueEntry[] Plans);

internal static class NormalInitializationTrace
{
    internal const string Stream = "base-spire:run:up_front";
    private static readonly string[] Procedures = ["InitializeSourceOpeningPools", "PopulateOpeningRelicBag", "AddOpeningItem", "ShuffleOpeningList"];
    internal static bool Relevant(SessionDebugRecord record) => record.Kind is SessionDebugKind.ProcedureEnter or SessionDebugKind.ProcedureExit
        && Procedures.Any(p => record.Subject == "base-spire:" + p);

    internal static NormalInitializationCandidates Dsl(IReadOnlyList<SessionDebugRecord> records, long dropped,
        NormalOpeningSource[] definitions, NormalOpeningList[] bags, NormalOpeningQueueEntry[] plans)
    {
        if (dropped != 0) throw Missing("dropped initialization records");
        var sources = definitions.ToDictionary(d => d.Reference);
        var lists = new Dictionary<int, (string Key, List<int> Entries)>();
        var shuffles = new List<NormalOpeningShuffle>(); var allocations = new List<NormalAncientAllocation>(); var picks = new List<NormalOpeningPick>();
        var sharedAssigned = definitions.Where(d => d.Category == "shared-ancient").ToDictionary(d => d.Reference, _ => -1L);
        var active = false; var finished = false; string? bagCategory = null;
        int? shuffled = null; var shuffleIndex = 0; int? shared = null; long cursor = 0;
        foreach (var record in records)
        {
            if (Relevant(record))
            {
                if (record.TextTruncated) throw Missing("truncated initialization procedure");
                var entering = record.Kind == SessionDebugKind.ProcedureEnter;
                switch (record.Subject)
                {
                    case "base-spire:InitializeSourceOpeningPools":
                        if (entering)
                        {
                            if (active || finished) throw Missing("repeated initialization");
                            active = true;
                        }
                        else { if (!active || shuffled is not null || bagCategory is not null) throw Missing("unfinished initialization"); active = false; finished = true; }
                        break;
                    case "base-spire:PopulateOpeningRelicBag" when active:
                        if (entering)
                        {
                            var args = Arguments(record, 2); Reference(args[0]);
                            if (bagCategory is not null) throw Missing("nested relic bag initialization");
                            bagCategory = args[1] switch { "true" => "shared-bag", "false" => "player-bag", _ => throw Missing("relic bag source argument") };
                        }
                        else bagCategory = null;
                        break;
                    case "base-spire:AddOpeningItem" when active && entering:
                    {
                        var args = Arguments(record, 2); var list = Reference(args[0]); var definition = Reference(args[1]);
                        if (!sources.TryGetValue(definition, out var source)) throw Missing("unidentified initialization source");
                        if (source.Category == "encounter") break; // The existing weighted trace owns these bags.
                        var key = source.Category switch
                        {
                            "shared-relic" or "ironclad-relic" => (bagCategory ?? throw Missing("relic outside its actual bag")) + "/" + source.Rarity,
                            "event" => "event/" + source.Act,
                            "shared-ancient" => "shared-ancient",
                            _ => throw Missing("unclassified initialization list source")
                        };
                        if (!lists.TryGetValue(list, out var state)) lists.Add(list, state = (key, []));
                        if (state.Key != key || state.Entries.Contains(definition)) throw Missing("mixed or duplicate initialization list entry");
                        state.Entries.Add(definition);
                        if (key == "shared-ancient")
                        {
                            if (shared is not null && shared != list) throw Missing("multiple shared Ancient lists");
                            shared = list;
                        }
                        break;
                    }
                    case "base-spire:ShuffleOpeningList" when active:
                        if (entering)
                        {
                            var list = Reference(Arguments(record, 1)[0]);
                            if (shuffled is not null || !lists.TryGetValue(list, out var state)) throw Missing("unidentified or nested initialization shuffle");
                            shuffled = list; shuffleIndex = state.Entries.Count - 1;
                        }
                        else
                        {
                            if (shuffled is null || shuffleIndex > 0) throw Missing("missing initialization shuffle draw");
                            shuffled = null;
                        }
                        break;
                }
            }
            if (record.Kind != SessionDebugKind.RandomCall || record.Subject != Stream) continue;
            if (!active || record.TextTruncated || record.Random is not { CandidatesTruncated: false } draw || draw.Before != cursor || draw.After != draw.Before + 1)
                throw Missing("initialization draw outside its complete procedure history");
            cursor = draw.After;
            if (draw.Operation == "int")
            {
                var selected = int.Parse(draw.Result, CultureInfo.InvariantCulture);
                if (shuffled is { } list)
                {
                    var state = lists[list];
                    if (shuffleIndex < 1 || draw.Minimum != "0" || draw.Maximum != (shuffleIndex + 1).ToString(CultureInfo.InvariantCulture)
                        || selected < 0 || selected > shuffleIndex) throw Missing("initialization shuffle bounds or index");
                    shuffles.Add(new(state.Key, draw.Before, draw.After, selected, state.Entries.Take(shuffleIndex + 1).Select(id => sources[id].Id).ToArray()));
                    (state.Entries[shuffleIndex], state.Entries[selected]) = (state.Entries[selected], state.Entries[shuffleIndex]);
                    shuffleIndex--;
                }
                else
                {
                    if (shared is null || allocations.Count >= 2) throw Missing("unclassified initialization integer draw");
                    var remaining = lists[shared.Value].Entries; var act = allocations.Count + 1;
                    if (draw.Minimum != "0" || draw.Maximum != (remaining.Count + 1).ToString(CultureInfo.InvariantCulture)
                        || selected < 0 || selected > remaining.Count) throw Missing("shared Ancient allocation bounds or count");
                    allocations.Add(new(act, draw.Before, draw.After, selected, remaining.Select(id => sources[id].Id).ToArray()));
                    foreach (var id in remaining.Take(selected)) sharedAssigned[id] = act;
                    remaining.RemoveRange(0, selected);
                }
            }
            else if (draw.Operation == "pick")
            {
                if (record.Snapshot is not { Complete: true } snapshot || draw.Candidates is not { Count: > 0 } roots || roots.Count != draw.CandidateCount)
                    throw Missing("incomplete direct opening candidates");
                var entities = snapshot.Entities.ToDictionary(e => e.Instance);
                if (!entities.TryGetValue(roots[0], out var first)) throw Missing("missing historical opening candidate root");
                if (first.Type == "base-spire:OpeningListItem") continue; // Independently checked weighted encounters.
                var entries = roots.Select(id => sources.TryGetValue(id.Value, out var source) && entities.TryGetValue(id, out var entity)
                    && entity.Type == "base-spire:OpeningDefinition" && Field(entity, "DefinitionId") == source.Id
                    && Field(entity, "Category") == source.Category && Field(entity, "Room") == source.Room
                    && Field(entity, "Act") == source.Act.ToString(CultureInfo.InvariantCulture)
                    && Field(entity, "AssignedAct") == source.AssignedAct.ToString(CultureInfo.InvariantCulture)
                    ? source : throw Missing("historical direct opening identity or source context")).ToArray();
                var act = picks.Count / 2; var room = picks.Count % 2 == 0 ? "Boss" : "ancient";
                if (entries.Any(d => room == "Boss" ? d.Category != "encounter" || d.Act != act || Field(entities[new InstanceId(d.Reference)], "Room") != "Boss"
                    : !(d.Category == "ancient" && d.Act == act || d.Category == "shared-ancient" && sharedAssigned[d.Reference] == act)))
                    throw Missing("direct opening source context");
                var selected = int.Parse(draw.Result, CultureInfo.InvariantCulture);
                if (draw.Minimum != "0" || draw.Maximum != entries.Length.ToString(CultureInfo.InvariantCulture) || selected < 0 || selected >= entries.Length)
                    throw Missing("direct opening bounds or index");
                picks.Add(new(act, room, draw.Before, draw.After, selected, entries.Select(d => d.Id).ToArray()));
            }
            else throw Missing("unclassified initialization draw");
        }
        if (!finished || active || allocations.Count != 2 || picks.Count != 6
            || sharedAssigned.Any(p => sources[p.Key].AssignedAct != p.Value)) throw Missing("incomplete initialization or uncommitted shared Ancient assignment");
        foreach (var list in lists.Values.Where(l => l.Key != "shared-ancient"))
        {
            var actual = list.Key.StartsWith("event/", StringComparison.Ordinal)
                ? plans.Where(p => p.Room == "event" && p.Act == long.Parse(list.Key[6..], CultureInfo.InvariantCulture)).OrderBy(p => p.Position).Select(p => p.Id).ToArray()
                : bags.Single(b => b.Key == list.Key).Ids;
            if (!actual.SequenceEqual(list.Entries.Select(id => sources[id].Id))) throw Missing("initialization shuffle disagrees with actual bag or event queue");
        }
        ValidatePicks(picks, plans);
        return new(shuffles.ToArray(), allocations.ToArray(), picks.ToArray());
    }

    internal static void ValidatePicks(IEnumerable<NormalOpeningPick> source, NormalOpeningQueueEntry[] plans)
    {
        var picks = source.ToArray();
        if (picks.Length != 6 || plans.Count(p => p.Room is "Boss" or "ancient") != 6) throw Missing("missing or repeated direct opening plan");
        for (var i = 0; i < picks.Length; i++)
        {
            var pick = picks[i];
            var committed = plans.Where(p => p.Act == pick.Act && p.Room == pick.Room).ToArray();
            if (pick.Act != i / 2 || pick.Room != (i % 2 == 0 ? "Boss" : "ancient") || pick.After != pick.Before + 1
                || i > 0 && pick.Before <= picks[i - 1].Before
                || pick.Selected < 0 || pick.Selected >= pick.Candidates.Length || pick.Candidates.Distinct(StringComparer.Ordinal).Count() != pick.Candidates.Length
                || committed.Length != 1 || committed[0].Position != 0 || committed[0].Id != pick.Candidates[pick.Selected])
                throw Missing("opening candidate result disagrees with actual committed plan");
        }
    }
    private static string Field(SessionDebugEntitySnapshot entity, string name) => entity.Fields.Single(f => f.Name == name).Value is { Scalar: { } value, Truncated: false }
        ? value : throw Missing("incomplete historical opening field");
    private static string[] Arguments(SessionDebugRecord record, int count)
    {
        var detail = record.Detail ?? throw Missing("initialization procedure arguments");
        var start = detail.IndexOf("; arguments=[", StringComparison.Ordinal);
        if (start < 0 || !detail.EndsWith(']')) throw Missing("initialization argument format");
        var args = detail[(start + 13)..^1].Split(',');
        return args.Length == count ? args : throw Missing("initialization argument arity");
    }
    private static int Reference(string value) => value.StartsWith('#') && int.TryParse(value.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : throw Missing("initialization reference format");
    internal static NotSupportedException Missing(string detail) => new("missing_dimension: " + detail);
}
