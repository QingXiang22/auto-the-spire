using System.Text.RegularExpressions;
using CoreTheSpire.Engine;
using BaseSpire.Slice.Domain.Run;

namespace BaseSpire.Slice;

/// <summary>Treasure owns its Engine bag, offer and actions. It never schedules combat.</summary>
public sealed class TreasureRoomWiring
{
    private readonly RoomCompileContext room;
    private readonly CollectionSymbol runs;
    private readonly RunResources resources;
    private readonly RunRelics relicCommands;
    private readonly FieldSymbol entryId;
    private readonly IReadOnlyDictionary<string, CollectionSymbol> bags;
    private readonly ComputeAssetSymbol rarityAsset;
    private readonly FieldSymbol rarityInput;
    private readonly FieldSymbol rarityOutput;
    public FieldSymbol Opened { get; }
    public FieldSymbol Resolved { get; }
    public FieldSymbol Gold { get; }
    public FieldSymbol RelicId { get; }
    public FieldSymbol Rarity { get; }

    public TreasureRoomWiring(EnginePackageBuilder package, RoomCompileContext room,
        RunInitializationWiring inventory, RunResources resources, RunRelics relicCommands)
    {
        this.room = room; runs = room.Runs;
        this.resources = resources;
        this.relicCommands = relicCommands;
        var run = room.RunType;
        Opened = run.AddField("TreasureOpened", FieldTypeSchema.Bool());
        Resolved = run.AddField("TreasureRelicResolved", FieldTypeSchema.Bool());
        Gold = run.AddField("TreasureGold", FieldTypeSchema.Int(0, 52));
        RelicId = run.AddField("TreasureRelicId", FieldTypeSchema.String(64));
        Rarity = run.AddField("TreasureRelicRarity", FieldTypeSchema.String(16));
        entryId = inventory.SharedId;
        bags = inventory.SharedBags;
        rarityAsset = package.ComputeAsset("TreasureRelicRarity");
        rarityAsset.BackendId = BaseSpireComputeBackend.BackendId;
        rarityAsset.SourcePath = "sts2/src/Core/Factories/RelicFactory.cs";
        rarityAsset.SourceHash = BaseSpire.SinglePlayer.BasePublicSemanticsBuilder.Digest("RelicFactory.RollRarity:NextFloat;0.5f;0.83f");
        rarityAsset.Entry = "TreasureRelicRarity";
        rarityInput = rarityAsset.Input("Payload", FieldTypeSchema.Int(0, TreasureRelicRules.RandomSpan - 1));
        rarityOutput = rarityAsset.Output("Rarity", FieldTypeSchema.String(16));
    }

    public static string ContentId(SharedRelicPoolEntry relic) => "base-spire:" +
        Regex.Replace(relic.SourceName, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();

    public Inst[] Enter(IrScope s, Expr run)
    {
        var payload = s.Local("treasureRarityPayload", FieldTypeSchema.Int(0, TreasureRelicRules.RandomSpan - 1));
        var rolled = s.Local("treasureRarity", FieldTypeSchema.String(16));
        var steps = new List<Inst>
        {
            s.SetField(run, Opened, s.Const(false)), s.SetField(run, Resolved, s.Const(false)),
            s.SetField(run, Gold, s.Const(0L)),
            s.SampleInt(VanillaRandomLayout.EngineStreamName("TreasureRoomRelics"), s.Const(0L),
                s.Const(TreasureRelicRules.RandomSpan), payload),
            s.Compute(rarityAsset, [(rarityInput, s.LocalRef(payload))], [(rarityOutput, rolled)]),
            s.SetField(run, Rarity, s.LocalRef(rolled)),
            s.SetField(run, RelicId, s.Const("base-spire:circlet")),
        };
        var found = s.Local("treasureRelicFound", FieldTypeSchema.Bool());
        steps.Add(s.SetLocal(found, s.Const(false)));
        foreach (var rarity in new[] { "Common", "Uncommon", "Rare" })
        {
            var bag = bags[rarity]; var head = s.First(s.Collection(bag));
            steps.Add(s.If(s.And(s.Not(s.LocalRef(found)),
                s.And(s.Equal(s.LocalRef(rolled), s.Const(rarity)), s.Greater(s.Count(s.Collection(bag)), s.Const(0L)))),
                s.SetField(run, RelicId, s.Field(head, entryId)), s.Destroy(head), s.SetLocal(found, s.Const(true))));
            if (rarity != "Rare")
                steps.Add(s.If(s.And(s.Not(s.LocalRef(found)), s.Equal(s.LocalRef(rolled), s.Const(rarity))),
                    s.SetLocal(rolled, s.Const(rarity == "Common" ? "Uncommon" : "Rare"))));
        }
        return [..steps];
    }

    public void RegisterActions(EnginePackageBuilder package)
    {
        Expr AtTreasure(IrScope s, Expr run) =>
            room.At(s, run, MapRoomWiring.TreasurePhase, "Treasure");
        Expr CanResolve(IrScope s, Expr run) => s.And(AtTreasure(s, run),
            s.And(s.Field(run, Opened), s.Not(s.Field(run, Resolved))));
        Inst[] Close(IrScope s, Expr run) => [s.SetField(run, Resolved, s.Const(true)),
            room.CloseVisit(s, run)];

        var open = package.ActionBody(package.Action("OpenTreasureChest"));
        var run = open.First(open.Collection(runs));
        var bits = open.Local("treasureGoldPayload", FieldTypeSchema.Int(0, TreasureRelicRules.RandomSpan - 1));
        open.Role(SliceManifest.PlayerRole);
        open.Requires(open.And(AtTreasure(open, run), open.Not(open.Field(run, Opened))));
        open.Body([open.SampleInt(VanillaRandomLayout.EnginePlayerStreamName("Rewards"), open.Const(0L),
                open.Const(TreasureRelicRules.RandomSpan), bits),
            open.SetField(run, Gold, open.Add(open.Const(42L), open.Divide(open.Multiply(open.LocalRef(bits),
                open.Const(11L)), open.Const(TreasureRelicRules.RandomSpan)))),
            .. resources.GainGold(open, run, open.Field(run, Gold)),
            open.SetField(run, Opened, open.Const(true))]);

        var skip = package.ActionBody(package.Action("SkipTreasureRelic"));
        var skipRun = skip.First(skip.Collection(runs));
        skip.Role(SliceManifest.PlayerRole); skip.Requires(CanResolve(skip, skipRun)); skip.Body(Close(skip, skipRun));

        var take = package.ActionBody(package.Action("TakeTreasureRelic"));
        var takeRun = take.First(take.Collection(runs));
        take.Role(SliceManifest.PlayerRole); take.Requires(CanResolve(take, takeRun));
        take.Body([.. relicCommands.Obtain(take, takeRun, take.Field(takeRun, RelicId)),
            .. Close(take, takeRun)]);
    }
}
