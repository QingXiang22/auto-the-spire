using NetTheSpire.Contracts;
using NetTheSpire.Contracts.Dto;
using NetTheSpire.Host;

namespace NetTheSpire.Tests;

internal static class BaseDslPileChecks
{
    internal static readonly string[] Names = ["Hand", "DrawPile", "DiscardPile", "ExhaustPile", "InPlay", "RemovedFromCombat", "RunDeck"];

    internal static PileDto Pile(PublicSnapshotDto snapshot, string name) => snapshot.Piles.Single(p =>
        snapshot.Entities.Single(e => e.InstanceId == p.PileRef).KindId == "net:kind:base-spire:" + name + "View");

    internal static void CheckSnapshot(PublicSnapshotDto snapshot)
    {
        Check.Equal(7, snapshot.Piles.Count, "all combat piles and the permanent deck are published, including empty piles");
        foreach (var name in Names)
        {
            var pile = Pile(snapshot, name);
            Check.Equal(name != "DrawPile", pile.MembersComplete, "draw identity stays hidden; other pile memberships are complete");
            if (name == "DrawPile") Check.Equal(0, pile.VisibleMembers.Count, "unrevealed draw identities stay absent");
            else Check.Equal(pile.TotalCount, pile.VisibleMembers.Count, "visible membership has the real count");
            Check.Equal(0, pile.KnownPositions.Count, "internal collection order is not a known-position claim");
            Check.True(pile.VisibleMembers.All(id => snapshot.Entities.Single(e => e.InstanceId == id).KindId == "net:kind:base-spire:Card"), "public pile references close over actual card entities");
        }
        Check.False(snapshot.Groups.Any(g => g.TypeId == "net:group_type:base-spire:RunDeck"), "the permanent deck has one membership source");
        var combat = Names.Where(n => n != "RunDeck").SelectMany(n => Pile(snapshot, n).VisibleMembers).ToArray();
        Check.Equal(combat.Length, combat.Distinct().Count(), "public combat memberships do not overlap");
    }

    public static void NormalPiles()
    {
        var setup = TextRuleScenario.CreatePackage(Path.Combine(Root(), "mod-the-spire/base-spire/package.json"));
        var host = setup.Host; var number = 0;
        ResponseDto Send(Operation op, params (string, WireValue)[] fields)
        {
            var response = host.Dispatch(new("pile-" + ++number, op,
                new WireObject(fields.Select(p => new KeyValuePair<string, WireValue>(p.Item1, p.Item2)))));
            Check.True(response.Ok, "real Host: " + response.Error?.Message); return response;
        }
        var describe = DescribeResultDto.FromWire(Send(Operation.Describe).Result!);
        var started = (WireObject)Send(Operation.Start, ("seed", WireValue.Str("777")),
            ("expected_identity", describe.Identity.ToWire()), ("scenario_profile", WireValue.Str(host.SupportedScenarioProfile))).Result!;
        var id = ((WireString)started.Get("session_id")!).Value;
        var snapshot = PublicSnapshotDto.FromWire(started.Get("snapshot")!, NetProtocol.V02);
        var pileRefs = Names.ToDictionary(name => name, name => Pile(snapshot, name).PileRef);
        CheckBoundary();
        Step(snapshot.LegalActions.Single(a => a.ActionType.EndsWith(":StartNormalRun", StringComparison.Ordinal)));
        var option = snapshot.Entities.Single(e => e.KindId.EndsWith(":NeowOption", StringComparison.Ordinal)
            && e.Fields.Any(f => f.FieldId.EndsWith(":NeowOption.RelicId", StringComparison.Ordinal) && f.Value is WireString { Value: "base-spire:booming-conch" }));
        Step(snapshot.LegalActions.Single(a => a.Fields.Any(f => f.Value is WireString s && s.Value == option.InstanceId)));
        Step(snapshot.LegalActions.Single(a => a.Fields.Single().Value is WireBool { Value: true }));
        Step(snapshot.LegalActions.First(a => a.ActionType.EndsWith(":ChooseMapPoint", StringComparison.Ordinal)));
        Check.Equal(5, Pile(snapshot, "Hand").TotalCount, "normal first player turn draws five cards");
        Check.Equal(5, Pile(snapshot, "DrawPile").TotalCount, "five starter combat copies remain hidden");
        Check.Equal(10, Pile(snapshot, "RunDeck").TotalCount, "ten permanent starter instances remain distinct");
        Check.Equal(15, snapshot.Entities.Count(e => e.KindId == "net:kind:base-spire:Card"), "only permanent cards and five revealed combat copies have public identities");
        var attack = snapshot.LegalActions.First(a => a.ActionType.EndsWith(":PlayAttackFromHand", StringComparison.Ordinal));
        var played = ((WireString)attack.Fields.Single().Value!).Value;
        Step(attack);
        Check.Equal(4, Pile(snapshot, "Hand").TotalCount, "playing removes exactly one hand instance");
        Check.True(Pile(snapshot, "DiscardPile").VisibleMembers.Contains(played), "the same played instance becomes public in discard");
        Check.False(Pile(snapshot, "RunDeck").VisibleMembers.Contains(played), "the combat copy is distinct from the permanent source");

        void Step(LegalActionDto action)
        {
            Send(Operation.Step, ("session_id", WireValue.Str(id)), ("expected_observation_id", WireValue.Integer(snapshot.ObservationId)), ("candidate_id", WireValue.Str(action.CandidateId)));
            snapshot = PublicSnapshotDto.FromWire(((WireObject)Send(Operation.Observe, ("session_id", WireValue.Str(id))).Result!).Get("snapshot")!, NetProtocol.V02);
            CheckBoundary();
        }
        void CheckBoundary()
        {
            CheckSnapshot(snapshot);
            var engine = host.GetSessionForTests(id).EngineForTests;
            foreach (var name in Names)
            {
                Check.Equal(pileRefs[name], Pile(snapshot, name).PileRef, "pile references survive normal run initialization and room entry");
                Check.Equal(engine.State.CollectionMembers(engine.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id).Count, Pile(snapshot, name).TotalCount,
                    "public total comes from the real collection at every boundary");
            }
        }
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "mod-the-spire/base-spire/package.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root missing.");
    }
}
