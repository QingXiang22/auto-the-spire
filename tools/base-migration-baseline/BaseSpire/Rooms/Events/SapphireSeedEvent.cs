using BaseSpire.Slice.Orchestration;

namespace BaseSpire.Slice.Rooms;

/// <summary>Source SapphireSeed: heal then choose an upgrade, or attach Sown to one chosen permanent instance.</summary>
public sealed class SapphireSeedEvent : ChoiceEventModule
{
    public const string SourceName = "SapphireSeed";
    public const string Eat = "SapphireSeedEat";
    public const string Plant = "SapphireSeedPlant";
    public override string Id => EventSourceIdentity.ModuleId(SourceName);
    protected override IReadOnlyList<EventChoice> Choices(EventCompileContext context)
    {
        var deck = context.Deck ?? throw new InvalidOperationException("SapphireSeed requires RunDeck");
        var attachments = new CardEnchantments(deck.Definitions);
        return [new(Eat, e => [.. e.HealLivingPlayer(9), .. deck.SelectForUpgrade(e.Scope)]),
            new(Plant, e => deck.SelectOne(e.Scope, card => attachments.CanEnchantPermanent(e.Scope, card),
                "Choose a permanent card to enchant with Sown", card => attachments.ApplySown(e.Scope, card)))];
    }
}
