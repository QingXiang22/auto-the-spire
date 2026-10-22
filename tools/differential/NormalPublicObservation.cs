using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;

namespace Differential;

internal sealed record NormalMapNode(string Coordinate, string Type, bool Visited, bool Selectable, string[] Children);
internal sealed record NormalRunView(string Phase, long Act, long Floor, long TotalFloor, string CurrentNode, long NodeCount, bool NeowPending);
internal sealed record NormalNeowOption(string Relic, long Position);
internal sealed record NormalNeowView(string Status, string SelectedRelic, NormalNeowOption[] Options);
internal sealed record NormalCardOffer(string Id, bool Upgraded, long Cost, string Rarity);
internal sealed record NormalPotionView(string Origin, long Slot, string Usage, string Target);
internal sealed record NormalRelicView(string Id, bool Melted);
internal sealed record NormalPlayerView(long Hp, long MaxHp, long Gold, string Character, long MaxEnergy, bool CombatActive, string CombatOutcome,
    string Room, bool TurnOpen, long Energy, long Turn, long Block, bool CurrentRun);
internal sealed record NormalEnemyView(string Origin, string Id, long Hp, long MaxHp, long Block, string Move, long MoveIndex, string Intent);
internal sealed record NormalPlayerHistory(long Plays, long HpLosses, bool LostHpThisTurn, bool ExhaustedThisTurn, bool EnemyPhase, bool UsesRunDeck);
internal sealed record NormalPublicPower(string Id, string Holder, long Amount, bool SkipTick);
internal sealed record NormalVisitView(long Id, string Node, string MapType, string Room, string Encounter, string Phase, string Status, bool Committed);
internal sealed record NormalCardView(string Scope, string Origin, string Id, string Label, string SourceEntry, string Kind, string Rarity,
    long Cost, long BaseCost, long Damage, bool Upgraded, bool CanUpgrade, bool Targeted, bool Exhausts, bool Innate, bool LeavesPlay, bool Strike, bool IsX, long PaidX);
internal sealed record NormalRewardView(bool Open, bool GoldPending, long GoldAmount, bool CardPending, bool PotionPending,
    NormalCardOffer[] Cards, string[] Potions);
internal sealed record NormalRewardSetView(long Visit, string Source, string Character, long Act, long Ascension, long CardCount,
    string RarityPolicy, bool Generated, bool Closed, string? Potion, NormalRewardPolicyView Policies);
internal sealed record NormalRewardPolicyView(string PoolEligibility, string UpgradePolicy, string ModifiersPolicy, string RandomStream);

// These projections read the protocol snapshot, never Engine fields or hidden collection members.
// They cover named semantic surfaces; the full public.snapshot gate stays required until every
// applicable entity field and topology surface has its own mapping or justified normalization.
internal static class NormalPublicObservation
{
    private static string FieldName(FieldDto field) => field.FieldId.Split(':')[^1];
    private static readonly HashSet<string> MappedFields = new[]
    {
        ("Run", "Phase ActIndex MapFloor TotalFloor CurrentNode MapNodeCount NeowPending CurrentVisit VisitSequence"),
        ("MapNode", "Col Row Type Visited Selectable"),
        ("NeowVisit", "Status SelectedRelic"), ("NeowOption", "Position RelicId"),
        ("Player", "Hp MaxHp Gold CharacterId MaxEnergy CombatActive CombatOutcome Room TurnOpen Energy TurnNumber Block CurrentRun CurrentRewardSet GoldRewardPending GoldRewardAmount CardRewardPending PlaySequence HpLossCount LostHpThisTurn ExhaustedCardThisTurn EnemyPhase UsesRunDeck"),
        ("Enemy", "Hp MaxHp Block MoveId MoveIndex Intent"), ("Power", "HolderPlayer HolderEnemy Stacks SkipTick"),
        ("RunVisit", "StableId Node MapType RoomType EncounterId Phase Status Committed Rewards"),
        ("Card", "Label SourceEntry Kind Rarity Cost BaseCost BaseDamage DamageGrowth Upgraded CanUpgrade Targeted Exhausts Innate LeavesPlay StrikeTag IsX PaidX"),
        ("CombatRewardSet", "PotionPending VisitId Source CharacterId ActIndex Ascension CardCount RarityPolicy Generated Closed Potion PoolEligibility UpgradePolicy ModifiersPolicy RandomStream"), ("Potion", "Slot Usage Target"), ("Relic", "IsMelted"),
        ("HandView", "Player"), ("DrawPileView", "Player"), ("DiscardPileView", "Player"), ("ExhaustPileView", "Player"),
        ("InPlayView", "Player"), ("RemovedFromCombatView", "Player"), ("RunDeckView", "Player")
    }.SelectMany(p => p.Item2.Split(' ').Select(f => p.Item1 + "." + f)).ToHashSet(StringComparer.Ordinal);
    internal static string[] UnmappedFields(PublicSnapshotDto snapshot) => snapshot.Entities.SelectMany(e => e.Fields)
        .Select(FieldName).Where(f => !MappedFields.Contains(f)).Distinct().Order(StringComparer.Ordinal).ToArray();
    internal static WireValue Field(EntityDto entity, string name)
    {
        var field = entity.Fields.Single(f => FieldName(f) == entity.KindId.Split(':')[^1] + "." + name);
        return field.State == "known" && field.Value is not null ? field.Value
            : throw new NotSupportedException("missing_dimension: public field " + field.FieldId + " is " + field.State);
    }
    internal static string Text(EntityDto entity, string name) => Field(entity, name) is WireString value ? value.Value
        : throw new NotSupportedException("missing_dimension: public string field " + name);
    internal static long Number(EntityDto entity, string name) => long.Parse(Text(entity, name), System.Globalization.CultureInfo.InvariantCulture);
    internal static bool Flag(EntityDto entity, string name) => Field(entity, name) is WireBool value ? value.Value
        : throw new NotSupportedException("missing_dimension: public boolean field " + name);
    private static string Content(EntityDto entity) => entity.ContentState == "known" && entity.ContentId is not null ? entity.ContentId
        : throw new NotSupportedException("missing_dimension: public content identity for " + entity.KindId);
    internal static void Add(PublicSnapshotDto snapshot, SortedDictionary<string, string> values, Func<WireValue, string> binding)
    {
        NormalPublicTopology.Add(snapshot, values);
        EntityDto[] Entities(string kind) => snapshot.Entities.Where(e => e.KindId == "net:kind:base-spire:" + kind).ToArray();
        var byHandle = snapshot.Entities.ToDictionary(e => e.InstanceId, StringComparer.Ordinal);
        EntityDto[] Group(string name)
        {
            var group = snapshot.Groups.Single(g => g.TypeId == "net:group_type:base-spire:" + name);
            if (byHandle[group.OwnerRef].KindId != "net:kind:base-spire:Player") throw new NotSupportedException("missing_dimension: public inventory owner");
            return group.Members.Select(m => m.Multiplicity == 1 ? byHandle[m.EntityRef]
                : throw new NotSupportedException("missing_dimension: repeated public group instance")).ToArray();
        }
        var nodes = Entities("MapNode");
        string Coord(EntityDto node) => Number(node, "Col") + "," + Number(node, "Row");
        var run = Entities("Run").Single(); var player = Entities("Player").Single();
        void Put(string path, object value) => values.Add(path, NormalPreflight.Json(value));
        Put("public.map", nodes.Select(n => new NormalMapNode(Coord(n), Text(n, "Type").Split(':')[^1], Flag(n, "Visited"), Flag(n, "Selectable"),
            snapshot.Relations.Where(r => r.TypeId == "net:relation:base-spire:MapChildren" && r.SourceRef == n.InstanceId)
                .Select(r => Coord(byHandle[r.TargetRef])).Order(StringComparer.Ordinal).ToArray())).OrderBy(n => n.Coordinate, StringComparer.Ordinal).ToArray());
        Put("public.run", new NormalRunView(Text(run, "Phase"), Number(run, "ActIndex"), Number(run, "MapFloor"), Number(run, "TotalFloor"),
            Coord(byHandle[Text(run, "CurrentNode")]), Number(run, "MapNodeCount"), Flag(run, "NeowPending")));
        Put("public.player", new NormalPlayerView(Number(player, "Hp"), Number(player, "MaxHp"), Number(player, "Gold"), Text(player, "CharacterId"),
            Number(player, "MaxEnergy"), Flag(player, "CombatActive"), Text(player, "CombatOutcome"), Text(player, "Room"), Flag(player, "TurnOpen"),
            Number(player, "Energy"), Number(player, "TurnNumber"), Number(player, "Block"), Text(player, "CurrentRun") == run.InstanceId));
        Put("public.player_history", new NormalPlayerHistory(Number(player, "PlaySequence"), Number(player, "HpLossCount"),
            Flag(player, "LostHpThisTurn"), Flag(player, "ExhaustedCardThisTurn"), Flag(player, "EnemyPhase"), Flag(player, "UsesRunDeck")));
        Put("public.enemies", Entities("Enemy").Select(e => new NormalEnemyView(binding(WireValue.Str(e.InstanceId)), Content(e), Number(e, "Hp"),
            Number(e, "MaxHp"), Number(e, "Block"), Text(e, "MoveId"), Number(e, "MoveIndex"), Text(e, "Intent"))).OrderBy(e => e.Origin, StringComparer.Ordinal).ToArray());
        Put("public.powers", Entities("Power").Select(p => new NormalPublicPower(Content(p), binding(Field(p,
            p.Fields.Any(f => FieldName(f) == "Power.HolderPlayer") ? "HolderPlayer" : "HolderEnemy")), Number(p, "Stacks"), Flag(p, "SkipTick")))
                .OrderBy(p => p.Holder, StringComparer.Ordinal).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray());
        Put("public.visits", snapshot.Sequences.Single(s => s.TypeId == "net:sequence_type:base-spire:RunVisits").Elements
            .Select(id => byHandle[id]).Select(v => new NormalVisitView(Number(v, "StableId"), Coord(byHandle[Text(v, "Node")]),
                Text(v, "MapType").Split(':')[^1], Text(v, "RoomType"), Text(v, "EncounterId"), Text(v, "Phase"), Text(v, "Status"), Flag(v, "Committed"))).ToArray());
        var neow = Entities("NeowVisit").Single();
        var options = snapshot.Relations.Where(r => r.TypeId == "net:relation:base-spire:NeowOptions" && r.SourceRef == neow.InstanceId)
            .Select(r => byHandle[r.TargetRef]).ToArray();
        if (options.Length != Entities("NeowOption").Length) throw new NotSupportedException("missing_dimension: orphan public Neow option");
        Put("public.neow", new NormalNeowView(Text(neow, "Status"), Text(neow, "SelectedRelic"), options
            .OrderBy(o => Number(o, "Position")).Select(o => new NormalNeowOption(Text(o, "RelicId"), Number(o, "Position"))).ToArray()));
        var current = player.Fields.SingleOrDefault(f => FieldName(f) == "Player.CurrentRewardSet");
        var reward = current is null ? null : byHandle[Text(player, "CurrentRewardSet")];
        Put("public.reward_offer", new NormalRewardView(reward is not null, Flag(player, "GoldRewardPending"), Number(player, "GoldRewardAmount"),
            Flag(player, "CardRewardPending"), reward is not null && Flag(reward, "PotionPending"),
            Group("CardRewardOptions").Select(c => new NormalCardOffer(Content(c), Flag(c, "Upgraded"), Number(c, "Cost"), Text(c, "Rarity")))
                .OrderBy(c => c.Id, StringComparer.Ordinal).ThenBy(c => c.Upgraded).ToArray(),
            Group("PotionRewardOptions").Select(p => binding(WireValue.Str(p.InstanceId))).Order(StringComparer.Ordinal).ToArray()));
        Put("public.reward_sets", Entities("CombatRewardSet").Select(r => new NormalRewardSetView(Number(r, "VisitId"), Text(r, "Source"),
            Text(r, "CharacterId"), Number(r, "ActIndex"), Number(r, "Ascension"), Number(r, "CardCount"), Text(r, "RarityPolicy"),
            Flag(r, "Generated"), Flag(r, "Closed"), r.Fields.Any(f => FieldName(f) == "CombatRewardSet.Potion") ? binding(Field(r, "Potion")) : null,
            new(Text(r, "PoolEligibility"), Text(r, "UpgradePolicy"), Text(r, "ModifiersPolicy"), Text(r, "RandomStream"))))
                .OrderBy(r => r.Visit).ToArray());
        Put("public.potion_inventory", Group("Potions").Select(p => new NormalPotionView(binding(WireValue.Str(p.InstanceId)), Number(p, "Slot"), Text(p, "Usage"), Text(p, "Target")))
            .OrderBy(p => p.Origin, StringComparer.Ordinal).ToArray());
        Put("public.relic_inventory", Group("Relics").Select(r => new NormalRelicView(Content(r), Flag(r, "IsMelted"))).OrderBy(r => r.Id, StringComparer.Ordinal).ToArray());
        var deck = snapshot.Piles.Single(p => byHandle[p.PileRef].KindId == "net:kind:base-spire:RunDeckView").VisibleMembers.ToHashSet(StringComparer.Ordinal);
        var combatMembers = snapshot.Piles.Where(p => byHandle[p.PileRef].KindId != "net:kind:base-spire:RunDeckView")
            .SelectMany(p => p.VisibleMembers).ToHashSet(StringComparer.Ordinal);
        var rewardCards = Group("CardRewardOptions").Select(c => c.InstanceId).ToHashSet(StringComparer.Ordinal);
        Put("public.cards", Entities("Card").Select(c => new NormalCardView(deck.Contains(c.InstanceId) ? "deck" : rewardCards.Contains(c.InstanceId) ? "reward"
            : combatMembers.Contains(c.InstanceId) ? "combat" : throw new NotSupportedException("missing_dimension: visible card outside a public pile or reward group"),
            binding(WireValue.Str(c.InstanceId)), Content(c), Text(c, "Label"), Text(c, "SourceEntry"), Text(c, "Kind"), Text(c, "Rarity"),
            Number(c, "Cost"), Number(c, "BaseCost"), checked(Number(c, "BaseDamage") + Number(c, "DamageGrowth")), Flag(c, "Upgraded"), Flag(c, "CanUpgrade"),
            Flag(c, "Targeted"), Flag(c, "Exhausts"), Flag(c, "Innate"), Flag(c, "LeavesPlay"), Flag(c, "StrikeTag"), Flag(c, "IsX"), Number(c, "PaidX")))
                .OrderBy(c => c.Scope, StringComparer.Ordinal).ThenBy(c => c.Origin, StringComparer.Ordinal).ToArray());
    }
}
