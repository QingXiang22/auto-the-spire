using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

/// <summary>Source singleplayer JungleMazeAdventure, including its gameplay-stream visual shuffle draws.</summary>
public sealed class JungleMazeAdventureEvent(EnginePackageBuilder package) : ChoiceEventModule
{
    public const string SourceName = "JungleMazeAdventure";
    public const string SoloQuest = "JungleMazeAdventureSoloQuest";
    public const string JoinForces = "JungleMazeAdventureJoinForces";
    public override string Id => EventSourceIdentity.ModuleId(SourceName);
    private readonly EventFloatGoldAsset random = EventFloatGoldAsset.Declare(package);
    private FieldSymbol? solo, joined;

    private void Declare(EventCompileContext context)
    {
        solo ??= context.Room.RunType.AddField("JungleMazeSoloGold", FieldTypeSchema.Int(0, 999999));
        joined ??= context.Room.RunType.AddField("JungleMazeJoinGold", FieldTypeSchema.Int(0, 999999));
    }

    public override Inst[] Enter(IrScope scope, EventCompileContext context, Expr run)
    {
        Declare(context);
        return [.. random.Roll(scope, SourceName, 150, -15, 15, run, solo!),
            .. random.Roll(scope, SourceName, 50, -15, 15, run, joined!)];
    }

    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context)
    {
        Declare(context);
        return [new(JoinForces, e => e.GainGold(e.Read(joined!))), new(SoloQuest, e =>
        {
            var ignored = e.Scope.Local("jungleVisualShufflePayload", FieldTypeSchema.Int(0, EventRandomAsset.Span - 1));
            return [e.Scope.Repeat(e.Scope.Const(2L), [e.Scope.SampleInt(EventRandomAsset.StreamName(SourceName),
                    e.Scope.Const(0L), e.Scope.Const(EventRandomAsset.Span), ignored)]),
                .. e.DamageOutsideCombat(18), .. e.GainGold(e.Read(solo!))];
        })];
    }
}
