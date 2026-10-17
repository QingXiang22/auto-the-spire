using CoreTheSpire;

namespace BaseSpire.Slice.Effects;

/// <summary>Convenience syntax expands to the same reusable effect program as explicit composition.</summary>
public static class CardEffectPrimitives
{
    public static SelectCardsEffect ChooseMove(CardEffectPile from, CardEffectPile to, int count,
        CardEffectCardFilter filter, bool top, bool autoSelect, DslSourceLocation source) =>
        new(from, filter, count, new([new MoveSelectedCardEffect(from, to, source) { Manual = true, Top = top }]), source)
        {
            AutoSelect = autoSelect,
            Prompt = to switch
            {
                CardEffectPile.Exhaust => SliceManifest.ExhaustChoicePrompt,
                CardEffectPile.Draw when top => SliceManifest.HeadbuttPrompt,
                CardEffectPile.Discard => "Choose cards to discard.",
                CardEffectPile.Hand => "Choose cards to return to your Hand.",
                _ => "Choose cards to move.",
            },
        };

    public static ConditionalCardEffect OnFatal(CardEffectAmount amount, DslSourceLocation source) =>
        new(new(CardConditionKind.TargetAlive), CardEffectProgram.Empty,
            new([new PrimitiveCardEffect(CardEffectOperation.GainMaxHp, CardEffectTarget.Owner, amount, "", source)]), source);
}
