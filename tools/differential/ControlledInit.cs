namespace Differential;

internal sealed record InitialCard(string Slot, string Card, bool Upgraded);
internal sealed record InitialPiles(string Version, string Source, string RngDerivation,
    IReadOnlyList<InitialCard>? Hand, IReadOnlyList<InitialCard>? Draw,
    IReadOnlyList<InitialCard>? Discard, IReadOnlyList<InitialCard>? Exhaust);
internal sealed record InitialStateProof(FixtureSetup Setup, bool Applied);
internal sealed record RandomMeasurement(string Unit, string Coverage, long? Before, long? After,
    IReadOnlyList<StreamSnapshot> BeforeStreams, IReadOnlyList<StreamSnapshot> AfterStreams)
{
    public bool MeasuredZero => Before is { } before && After is { } after && after == before;
}
internal static class ControlledInit
{
    public const string Version = "ordered-starting-piles@1";
    public const string Source = "Ironclad.StartingDeck+controlled-slice@1";
    public static readonly string[] AllowedCases = ["enemy-death-aftermath", "power-turn-end-decay",
        "vulnerable-decay-via-bash", "random-reshuffle-unsupported"];
    public static void Validate(FixtureSetup setup)
    {
        var init = setup.InitialPiles ?? throw new CheckFailed("完整初始牌区声明缺失");
        if (init.Version != Version || init.Source != Source || init.RngDerivation != RngSourceCompat.ProfileId)
            throw new CheckFailed("初始化版本/来源/随机派生未登记");
        if (init.Hand is null || init.Draw is null || init.Discard is null || init.Exhaust is null)
            throw new CheckFailed("完整声明必须包含 hand/draw/discard/exhaust（空区显式 []）");
        var all = init.Hand.Concat(init.Draw).Concat(init.Discard).Concat(init.Exhaust).ToArray();
        if (all.Any(card => string.IsNullOrWhiteSpace(card.Slot)) || all.Select(card => card.Slot).Distinct().Count() != all.Length)
            throw new CheckFailed("初始实例槽位缺失或重复");
        var expected = RngSourceCompat.IroncladStartingDeck.Select(card => "base-spire:" + card).ToList();
        if (setup.IncludeHeadbutt) expected.Add("base-spire:headbutt");
        if (!all.Select(card => card.Card).Order().SequenceEqual(expected.Order()))
            throw new CheckFailed("初始牌总集合与预登记源起始牌组不一致（缺失或多余牌）");
        CheckLegacy(init.Hand, setup.Hand ?? [], "hand");
        CheckLegacy(init.Discard, setup.Discard ?? [], "discard");
    }
    private static void CheckLegacy(IReadOnlyList<InitialCard> cards, IReadOnlyList<FixtureCard> legacy, string pile)
    {
        if (!cards.Select(card => (card.Card, card.Upgraded)).SequenceEqual(legacy.Select(card => (card.Card, card.Upgraded))))
            throw new CheckFailed($"完整 {pile} 与旧 setup.{pile} 不一致");
    }
    public static bool Valid(CaseRecord record)
    {
        if (!Valid(record.InitialState) || record.Boundaries.Count == 0) return false;
        var init = record.InitialState!.Setup.InitialPiles!;
        var facts = record.Boundaries[0].Facts.ToDictionary(fact => fact.Path, fact => fact.Value);
        foreach (var (name, cards) in new[] { ("hand", init.Hand!), ("draw", init.Draw!), ("discard", init.Discard!), ("exhaust", init.Exhaust!) })
            if (facts.GetValueOrDefault("pile." + name + ".initial-slots") != string.Join(",", cards.Select(card => card.Slot))
                || facts.GetValueOrDefault("pile." + name + ".order") != string.Join(",", cards.Select(card => card.Card))) return false;
        return true;
    }
    public static bool Valid(InitialStateProof? proof)
    {
        if (proof?.Applied != true) return false;
        try { Validate(proof.Setup); return true; } catch (CheckFailed) { return false; }
    }
}
