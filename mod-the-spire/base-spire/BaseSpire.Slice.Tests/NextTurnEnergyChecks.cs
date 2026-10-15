using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class NextTurnEnergyChecks
{
    private const string Power = "base-spire:energy-next-turn-power";
    public static void Run()
    {
        foreach (var mode in new[] { "normal", "created-upgraded", "armaments-upgraded" })
            Check.Run($"Next-turn energy: Outmaneuver/{mode} pays now, gains after reset, removes once", () =>
            {
                var model = FormalSpirePackage.Load().Model;
                var definition = model.Cards.Single(c => c.Id == "base-spire:outmaneuver");
                Check.Sequence(new[] { "EventCardPool" }, definition.Pools, "Silent visual pool is not acquisition pool");
                Check.Equal("event", definition.Rarity, "native Event rarity");
                var deck = new List<AuthorDeckEntry> { new(definition.Id, 1, mode == "created-upgraded"), new("base-spire:wound", 12) };
                if (mode == "armaments-upgraded") deck.Add(new("base-spire:armaments", 1));
                var s = Start(model with { Deck = deck });
                var card = FormalSliceFixture.MoveToHand(s, definition.Id);
                if (mode == "armaments-upgraded")
                    FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "base-spire:armaments"), card);
                SetEnergy(s, 3);
                FormalSliceFixture.PlayAt(s, card, FormalSliceFixture.Player(s)); Healthy(s);
                var amount = mode == "normal" ? 2L : 3L;
                Check.Equal(2L, Energy(s), "cost 1; no immediate grant");
                Check.Equal(amount, Stacks(s, Power), "captures actual variant into registered Power");
                Check.True(Members(s, FormalSliceFixture.DiscardPile).Contains(card), "ordinary Skill result");
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(3 + amount, Energy(s), "bonus occurs after max-energy reset, before hand draw");
                Check.False(PowerExists(s, FormalSliceFixture.Player(s), Power), "instance removed, not left at zero");
                FormalSliceFixture.EndTurn(s); Healthy(s);
                Check.Equal(3L, Energy(s), "no repeated bonus or modified maximum");
            });
        Check.Run("Next-turn energy: repeated plays stack into one deferred grant", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var s = Start(model with { Deck = [new("base-spire:outmaneuver", 2), new("base-spire:wound", 12)] });
            for (var i = 0; i < 2; i++) FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "base-spire:outmaneuver"), FormalSliceFixture.Player(s));
            Healthy(s); Check.Equal(1L, Energy(s), "two payments, no early gain");
            Check.Equal(4L, Stacks(s, Power), "source stack-counter semantics");
            Check.Equal(1, Owned(s, FormalSliceFixture.Player(s), Power).Count(), "same definition stacks");
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(7L, Energy(s), "3 reset plus four stacks");
            Check.False(PowerExists(s, FormalSliceFixture.Player(s), Power), "all accumulated stacks consumed once");
        });
        Check.Run("Next-turn energy: energy-gain prevention still consumes the once-only power", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var blocked = SpirePowerSource.Parse("package mymod { power ClosedGate { preventEnergyGain = true } }", "gate.spire").Powers.Single();
            var s = Start(model with { Powers = model.Powers.Append(blocked).ToArray(), Deck = [new("base-spire:wound", 12)] });
            var player = FormalSliceFixture.Player(s);
            FormalSliceFixture.GivePower(s, player, blocked.Id, 1);
            FormalSliceFixture.GivePower(s, player, Power, 2);
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(3L, Energy(s), "reset itself is unaffected; gained energy blocked");
            Check.False(PowerExists(s, player, Power), "native remove executes even when gain is zero");
        });
        Check.Run("Next-turn energy: maximum-energy modifiers apply before temporary bonus", () =>
        {
            var s = Start(FormalSpirePackage.Load().Model with { Deck = [new("base-spire:wound", 12)] });
            var player = FormalSliceFixture.Player(s);
            FormalSliceFixture.GivePower(s, player, "base-spire:pyre-power", 2);
            FormalSliceFixture.GivePower(s, player, Power, 2);
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(7L, Energy(s), "3 base + 2 Pyre + 2 temporary");
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(5L, Energy(s), "only permanent maximum modifier remains");
        });
        Check.Run("Next-turn energy: a mod registers the same mechanic under its own namespace", () =>
        {
            var model = FormalSpirePackage.Load().Model;
            var power = SpirePowerSource.Parse("package mymod { power Recovery { rules = { afterEnergyReset = GrantDeferredEnergy(owner=context.owner, power=context.power, amount=context.power.stacks) } } }", "mod-powers.spire").Powers.Single();
            Check.Equal("mymod:recovery", power.Id, "no concrete Power-name whitelist");
            var body = "type = skill rarity = common cost = 1 target = { selectSelf = {} } keywords = {} play = { applyPower = { power = Recovery stacks = 4 target = context.owner } } hooks = {}";
            var cards = SpireCardSource.Parse($"using namespace mymod; card Battery {{ normal = {{ {body} }} upgraded = {{ {body} }} }}", "mod-cards.spire", new Dictionary<string,string> { [power.Name] = power.Id });
            Check.Equal(1, cards.LoweredCount, "mod card actually lowers");
            var s = Start(model with { Powers = model.Powers.Append(power).ToArray(), Cards = model.Cards.Concat(cards.Cards).ToArray(), Deck = [new("mymod:battery", 1), new("base-spire:wound", 12)] });
            FormalSliceFixture.PlayAt(s, FormalSliceFixture.MoveToHand(s, "mymod:battery"), FormalSliceFixture.Player(s));
            Check.Equal(2L, Energy(s), "mod effect also defers");
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(7L, Energy(s), "registered behavior runs for a custom ID");
            Check.False(PowerExists(s, FormalSliceFixture.Player(s), power.Id), "custom instance also removed");
        });
        Check.Run("Next-turn energy: an enemy-owned power cannot grant player energy", () =>
        {
            var s = Start(FormalSpirePackage.Load().Model with { Deck = [new("base-spire:wound", 12)] });
            var enemy = FormalSliceFixture.Cultist(s);
            FormalSliceFixture.GivePower(s, enemy, Power, 4);
            FormalSliceFixture.EndTurn(s); Healthy(s);
            Check.Equal(3L, Energy(s), "only actual owning player eligible");
            Check.True(PowerExists(s, enemy, Power), "other owner's power not consumed");
        });
        Check.Run("Next-turn energy: lifecycle calls reject missing or unavailable context", () =>
        {
            foreach (var arguments in new[] {
                "owner=context.owner, amount=context.power.stacks",
                "owner=context.target, power=context.power, amount=context.power.stacks",
                "owner=context.owner, power=context.card, amount=2" })
            {
                var power = SpirePowerSource.Parse($"package mymod {{ power Bad {{ rules = {{ afterEnergyReset = GrantDeferredEnergy({arguments}) }} }} }}", "bad-context.spire").Powers.Single();
                var model = FormalSpirePackage.Load().Model;
                var rejected = false;
                try { Start(model with { Powers = model.Powers.Append(power).ToArray() }); }
                catch (EngineFaultException ex) { rejected = ex.Message.Contains("bad-context.spire", StringComparison.Ordinal); }
                Check.True(rejected, "link rejects invalid context with original source: " + arguments);
            }
        });
        Check.Run("Next-turn energy: invalid rule shapes do not register", () =>
        {
            foreach (var rule in new[] { "energyAfterResetOnce = 2", "energyAfterResetOnce = false", "energyAfterResetOnce = true energyAfterResetOnce = true" })
                Check.Equal(0, SpirePowerSource.Parse($"package mymod {{ power Bad {{ rules = {{ {rule} }} }} }}", "bad-power.spire").LoweredCount, rule);
        });
    }
    private static Session Start(SliceAuthorModel model)
    {
        var builder = new RuleSetBuilder(); builder.Add(CombatSlicePackage.Build(authorModel: model).Build());
        var s = new Session(builder.Build(), 20261001UL, [SliceManifest.PlayerRole], computeBackend: SourceWeightedPickBackend.Instance);
        s.Start(); Healthy(s); return s;
    }
    private static IEnumerable<InstanceId> Owned(Session s, InstanceId owner, string id)
    {
        var type = s.RuleSet.TypeByQualifiedId(FormalSliceFixture.PowerTypeId);
        var relation = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:Powers");
        return s.State.RelationTargets(relation.Id, owner).Where(p => s.State.GetField(p, type.FieldIndex("DefinitionId")).AsString() == id);
    }
    private static bool PowerExists(Session s, InstanceId owner, string id) => Owned(s, owner, id).Any();
    private static long Stacks(Session s, string id) => FormalSliceFixture.PowerStacks(s, FormalSliceFixture.Player(s), id);
    private static long Energy(Session s) => FormalSliceFixture.Number(s, FormalSliceFixture.Player(s), FormalSliceFixture.PlayerTypeId, "Energy");
    private static void SetEnergy(Session s, long amount) => FormalSliceFixture.SetNumber(s, FormalSliceFixture.Player(s), FormalSliceFixture.PlayerTypeId, "Energy", amount);
    private static IReadOnlyList<InstanceId> Members(Session s, string pile) => s.State.CollectionMembers(FormalSliceFixture.Collection(s, pile));
    private static void Healthy(Session s) => Check.True(s.Status != SessionStatus.Faulted, $"{s.Fault}");
}
