using BaseSpire.Slice.Domain.Run;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Rooms;

public enum EventCompletion { Finish, Continue }

/// <summary>Immutable content description; shared compilation owns control guards and the action boundary.</summary>
public sealed record EventChoice(string ActionName, Func<EventEffectContext, Inst[]> Effects,
    Func<EventEffectContext, Expr>? Available = null, EventCompletion Completion = EventCompletion.Finish);

public sealed record EventEffectContext(IrScope Scope, Expr Run, RunResources Resources)
{
    public Expr Read(FieldSymbol field) => Scope.Field(Run, field);
    public Inst[] GainGold(long amount) => GainGold(Scope.Const(amount));
    public Inst[] GainGold(Expr amount) => Resources.GainGold(Scope, Run, amount);
    public Inst[] DamageOutsideCombat(long amount) => Resources.DamageOutsideCombat(Scope, Run, Scope.Const(amount));
    public Inst[] HealLivingPlayer(long amount) => Resources.HealLivingPlayer(Scope, Run, Scope.Const(amount));
    public Inst Unsupported(string reason) => Scope.Fail(reason);
}

/// <summary>Normal option-based events provide content only; custom interactions can retain IEventRoomModule.</summary>
public abstract class ChoiceEventModule : IEventRoomModule
{
    public abstract string Id { get; }
    public virtual Inst[] Enter(IrScope scope, EventCompileContext context, Expr run) => [];
    protected abstract IReadOnlyList<EventChoice> Choices(EventCompileContext context);

    public void RegisterActions(EnginePackageBuilder package, EventCompileContext context)
    {
        var resources = context.Resources ?? throw new InvalidOperationException(
            $"Event '{Id}' requires the RunResources compile capability");
        foreach (var choice in Choices(context))
        {
            var body = package.ActionBody(package.Action(choice.ActionName));
            var run = context.Room.CurrentRun(body);
            var effects = new EventEffectContext(body, run, resources);
            body.Role(SliceManifest.PlayerRole);
            var active = body.And(context.Active(body, run, Id), body.Not(resources.IsDead(body, run)));
            body.Requires(choice.Available is null ? active : body.And(active, choice.Available(effects)));
            body.Body(
            [
                .. choice.Effects(effects),
                body.IfElse(resources.IsDead(body, run),
                    [.. context.Close(body, run), resources.DeclareDefeat(body)],
                    choice.Completion == EventCompletion.Finish ? context.Close(body, run) : []),
            ]);
        }
    }
}
