using BaseSpire.Slice.Effects;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Orchestration;

/// <summary>Single play authority for manual and automatic play. Effects do not own settlement.</summary>
public sealed class CardPlayPipeline(CardSchema schema, CardPileSymbols piles, CardPlayHistory history,
    CollectionSymbol played, CardPaymentRules payment, CardResultRules result,
    Func<IrScope, Expr> owner, Func<CardPlayContext, Expr> eligible, Func<CardPlayContext, Inst[]> prepareTarget,
    Func<CardPlayContext, string, Inst[]> hook,
    Func<CardPlayContext, Inst[]> beforeValues, Func<CardPlayContext, Inst[]> body,
    Func<CardPlayContext, Inst[]> afterValues, Func<CardPlayContext, Inst[]> powerAfter,
    Func<IrScope, Expr, string, Inst[]> removePower, EventSymbol playedEvent, FieldSymbol playedCard, PowerRuleRuntime? powerRules = null,
    RelationSymbol? playedReveal = null)
{
    public void Build(ProcedureBuilder play)
    {
        var card = play.Param("Card", FieldTypeSchema.RefTo(schema.Type));
        var source = play.Param("SourcePile", FieldTypeSchema.Int(0, 4));
        var auto = play.Param("Auto", FieldTypeSchema.Bool());
        var exhaust = play.Param("ForceExhaust", FieldTypeSchema.Bool());
        var executions = play.Param("Executions", FieldTypeSchema.Int(1, 9999));
        var c = CardPlayContext.Create(play, play.Param(card), owner(play), play.Param(auto), play.Param(exhaust), schema.Type);
        var entry = play.Local("playEntry", FieldTypeSchema.RefTo(history.Entry));
        var oneShot = payment.OneShot(c, removePower);
        var previous = play.Local("previousActiveCard", FieldTypeSchema.RefTo(schema.Type));
        var replays = powerRules?.Replay(play, c.Card, play.Param(executions)) ?? (Array.Empty<Inst>(), play.Param(executions));
        var enter = Enum.GetValues<CardEffectPile>().Where(p => p != CardEffectPile.InPlay).Select(p =>
            play.If(play.Equal(play.Param(source), play.Const((long)p)), pilesMove(p))).ToArray();
        Inst[] pilesMove(CardEffectPile from) => [play.RemoveFromCollection(piles[from], c.Card), play.AddToCollection(piles.InPlay, c.Card)];
        play.Body([
            .. enter,
            play.IfElse(play.Or(play.Field(c.Card, schema.Unplayable), play.Not(eligible(c))), result.Emit(c), [
                .. payment.Pay(c), .. oneShot.Capture, .. replays.Item1,
                .. powerRules is null ? [] : new Inst[] { play.SetLocal(previous, play.Field(c.Owner, powerRules.ActiveCard)), play.SetField(c.Owner,powerRules.ActiveCard,c.Card) },
                play.If(play.Less(play.CollectionIndex(played, c.Card), play.Const(0L)), [
                    play.AddToCollection(played, c.Card),
                    .. playedReveal is null ? [] : new Inst[] { play.AddRelation(playedReveal, c.Owner, c.Card) }]),
                .. prepareTarget(c),
                play.Repeat(replays.Item2, [play.If(eligible(c), [
                    play.Emit(playedEvent, (playedCard, c.Card)),
                    history.Start(play, c.Card, schema.Kind, c.Auto, entry),
                    .. powerRules is null ? [] : new Inst[] { powerRules.Call(play,"BeforePlay",c.Card) },
                    .. hook(c, AuthorSteps.HookBeforeCardPlayed),
                    .. beforeValues(c), .. body(c),
                    history.Finish(play, entry),
                    .. hook(c, AuthorSteps.HookAfterCardPlayed), .. powerAfter(c),
                    .. powerRules is null ? [] : new Inst[] { powerRules.Call(play,"AfterPlay",c.Card) }, .. afterValues(c)])]),
                .. powerRules is null ? [] : new Inst[] { play.SetField(c.Owner,powerRules.ActiveCard,play.LocalRef(previous)) },
                .. oneShot.Consume, .. result.Emit(c),
                play.If(play.Field(c.Card, schema.FreeUntilPlayed),
                    play.SetField(c.Card, schema.CostModifier, play.Const(0L)),
                    play.SetField(c.Card, schema.FreeUntilPlayed, play.Const(false)))])]);
    }
}
