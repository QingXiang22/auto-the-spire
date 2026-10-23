using NetTheSpire.Contracts.Dto;

namespace Differential;

internal sealed record NormalVisitLinks(long? CurrentVisit, long VisitSequence, long? CurrentReward, string[] Rewards);

// Compare public references through their semantic owners and actual memberships.
// MapNodes membership and its actual published traversal order are separate comparison paths.
internal static class NormalPublicTopology
{
    internal static readonly string[] Piles = ["Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay", "RemovedFromCombat", "RunDeck"];
    internal static readonly string[] Groups = ["CardRewardOptions", "PotionRewardOptions", "Potions", "Relics"];
    internal static readonly string[] Sequences = ["MapNodes", "RunVisits"];
    internal static string[] NativeOwnership() => Piles.Select(p => "pile:" + p + "View:player")
        .Concat(Groups.Select(g => "group:" + g + ":player")).Concat(Sequences.Select(s => "sequence:" + s + ":run"))
        .Order(StringComparer.Ordinal).ToArray();

    internal static void Add(PublicSnapshotDto snapshot, SortedDictionary<string, string> values)
    {
        var entities = snapshot.Entities.ToDictionary(e => e.InstanceId, StringComparer.Ordinal);
        string Kind(EntityDto e) => e.KindId.Split(':')[^1];
        EntityDto[] Of(string kind) => snapshot.Entities.Where(e => Kind(e) == kind).ToArray();
        var player = Of("Player").Single(); var run = Of("Run").Single();
        string Owner(string id) => id == player.InstanceId ? "player" : id == run.InstanceId ? "run"
            : throw new NotSupportedException("missing_dimension: unexpected public topology owner");
        void Put(string path, object value) => values.Add(path, NormalPreflight.Json(value));
        Put("public.ownership", snapshot.Piles.Select(p => "pile:" + Kind(entities[p.PileRef]) + ":" + Owner(NormalPublicObservation.Text(entities[p.PileRef], "Player")))
            .Concat(snapshot.Groups.Select(g => "group:" + g.TypeId.Split(':')[^1] + ":" + Owner(g.OwnerRef)))
            .Concat(snapshot.Sequences.Select(s => "sequence:" + s.TypeId.Split(':')[^1] + ":" + Owner(s.OwnerRef)))
            .Order(StringComparer.Ordinal).ToArray());
        var map = snapshot.Sequences.Single(s => s.TypeId == "net:sequence_type:base-spire:MapNodes");
        string Coordinate(EntityDto e) => Kind(e) == "MapNode"
            ? NormalPublicObservation.Number(e, "Col") + "," + NormalPublicObservation.Number(e, "Row")
            : throw new NotSupportedException("missing_dimension: non-map entity in public MapNodes");
        Put("public.map_members", map.Elements.Select(id => Coordinate(entities[id])).Order(StringComparer.Ordinal).ToArray());
        Put("public.map_sequence", map.Elements.Select(id => Coordinate(entities[id])).ToArray());
        Put("public.map_parents", snapshot.Relations.Where(r => r.TypeId == "net:relation:base-spire:MapParents")
            .Select(r => Coordinate(entities[r.SourceRef]) + "->" + Coordinate(entities[r.TargetRef])).Order(StringComparer.Ordinal).ToArray());
        var visits = snapshot.Sequences.Single(s => s.TypeId == "net:sequence_type:base-spire:RunVisits");
        var current = Optional(run, "CurrentVisit");
        long Visit(string id) => Kind(entities[id]) == "RunVisit" ? NormalPublicObservation.Number(entities[id], "StableId")
            : throw new NotSupportedException("missing_dimension: public visit reference type");
        long Reward(string id) => Kind(entities[id]) == "CombatRewardSet" ? NormalPublicObservation.Number(entities[id], "VisitId")
            : throw new NotSupportedException("missing_dimension: public visit reward type");
        var currentReward = Optional(player, "CurrentRewardSet");
        Put("public.visit_links", new NormalVisitLinks(current is null ? null : Visit(current), NormalPublicObservation.Number(run, "VisitSequence"),
            currentReward is null ? null : Reward(currentReward),
            visits.Elements.Select(id =>
            {
                var reward = Optional(entities[id], "Rewards");
                if (reward is null) return Visit(id) + ":none";
                return Visit(id) + ":reward:" + Reward(reward);
            }).ToArray()));
        var allowedRelations = new Dictionary<string, (string Source, string Target)>
        {
            ["MapChildren"] = ("MapNode", "MapNode"), ["MapParents"] = ("MapNode", "MapNode"), ["NeowOptions"] = ("NeowVisit", "NeowOption")
        };
        foreach (var relation in snapshot.Relations)
            if (allowedRelations.TryGetValue(relation.TypeId.Split(':')[^1], out var shape)
                && (Kind(entities[relation.SourceRef]) != shape.Source || Kind(entities[relation.TargetRef]) != shape.Target || relation.Position is not null))
                throw new NotSupportedException("missing_dimension: public relation endpoint type or unexpected ordering");
        Put("public.relation_types", snapshot.Relations.Select(r => r.TypeId).Distinct().Order(StringComparer.Ordinal).ToArray());
        Put("public.entity_counts", snapshot.Entities.GroupBy(Kind).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key + ":" + g.Count()).ToArray());
        CheckMembers("MapNode", map.Elements);
        CheckMembers("RunVisit", visits.Elements);
        CheckMembers("CombatRewardSet", visits.Elements.Select(id => Optional(entities[id], "Rewards")).OfType<string>());
        CheckMembers("Potion", Members("Potions").Concat(Members("PotionRewardOptions")));
        CheckMembers("Relic", Members("Relics"));
        foreach (var pile in snapshot.Piles) CheckMembers(Kind(entities[pile.PileRef]), [pile.PileRef]);

        IEnumerable<string> Members(string name) => snapshot.Groups.Single(g => g.TypeId == "net:group_type:base-spire:" + name)
            .Members.SelectMany(m => m.Multiplicity == 1 ? new[] { m.EntityRef }
                : throw new NotSupportedException("missing_dimension: public inventory multiplicity"));
        void CheckMembers(string kind, IEnumerable<string> members)
        {
            if (!Of(kind).Select(e => e.InstanceId).Order(StringComparer.Ordinal).SequenceEqual(members.Order(StringComparer.Ordinal)))
                throw new NotSupportedException("missing_dimension: orphan, missing or duplicate public " + kind + " membership");
        }
    }

    private static string? Optional(EntityDto entity, string name) => entity.Fields.Any(f => f.FieldId.EndsWith("." + name, StringComparison.Ordinal))
        ? NormalPublicObservation.Text(entity, name) : null;
}
