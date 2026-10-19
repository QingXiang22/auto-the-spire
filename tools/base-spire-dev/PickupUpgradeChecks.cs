using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunPickupUpgradeChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var relics = new[] { (Name: "Whetstone", Id: "base-spire:whetstone", Card: "Strike", Amount: 2),
            (Name: "WarPaint", Id: "base-spire:war-paint", Card: "Defend", Amount: 2),
            (Name: "SandCastle", Id: "base-spire:sand-castle", Card: "Inflame", Amount: 6) };
        void Own(string id, string name, Action body) { check(name, body); witnesses[id] = witnesses.GetValueOrDefault(id, []).Append(name).ToArray(); }
        void Clean(Session s)
        {
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:PermanentUpgradeSelection").Id).Count, "no selection state leaked");
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:PermanentUpgradeCandidate").Id).Count, "no candidate state leaked");
        }
        foreach (var row in relics)
            Own(row.Id, "UP " + row.Name + ": empty, single, insufficient and excess eligible permanent cards; upgrade exclusions and no combat-card changes", () =>
            {
                foreach (var count in new[] { 0, 1, row.Amount - 1, row.Amount + 2 }.Distinct())
                {
                    var s = Start(package); for (var i = 0; i < count; i++) Submit(s, row.Card + ".Obtain");
                    var eligible = Members(s, "RunDeck").ToArray();
                    Submit(s, row.Card + ".Obtain"); var unavailable = Members(s, "RunDeck").Last(); Set(s, unavailable, "Card", "CanUpgrade", Value.Of(false));
                    Submit(s, row.Card + ".Obtain"); var upgraded = Members(s, "RunDeck").Last(); Set(s, upgraded, "Card", "Upgraded", Value.Of(true));
                    InstanceId? otherKind = null;
                    if (row.Name != "SandCastle") { Submit(s, row.Name == "Whetstone" ? "Defend.Obtain" : "Strike.Obtain"); otherKind = Members(s, "RunDeck").Last(); }
                    Submit(s, row.Card + ".AddToHand"); var combatCard = Hand(s).Single();
                    var order = Members(s, "RunDeck").ToArray(); var before = s.Random.TotalDraws;
                    Submit(s, "TheAbacus.Obtain"); Submit(s, row.Name + ".Obtain");
                    Equal(Math.Min(count, row.Amount), eligible.Count(c => CardUpgraded(s, c)), "source type/count selection");
                    True(!CardUpgraded(s, unavailable) && CardUpgraded(s, upgraded), "excluded instances unchanged");
                    if (otherKind is { } other) True(!CardUpgraded(s, other), "other card kind excluded");
                    True(!CardUpgraded(s, combatCard), "combat card is outside the permanent candidate domain");
                    True(order.SequenceEqual(Members(s, "RunDeck")), "permanent deck membership and order unchanged");
                    Equal((long)Math.Max(0, count - 1), s.Random.DrawCount("base-spire:run:niche"), "full source shuffle consumes n-1");
                    Equal(before + Math.Max(0, count - 1), s.Random.TotalDraws, "no other stream consumed");
                    Equal(0L, Block(s), "permanent random selection never emits AfterShuffle");
                    True(!Legal(s, row.Name + ".Obtain"), "duplicate development action unavailable"); Clean(s);
                }
            });
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.pickup-upgrade-vectors.json")!);
        foreach (var vector in data.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var name = "UP source stable Niche selection, exact instance upgrade order and shared cursor: " + vector.GetProperty("seed").GetString();
            check(name, () =>
            {
                using var copy = new Copy(package.Root); copy.Include("upgrade-source-trace.spire", """
                    entity UpgradeTrace { fields = { Card = { type = Ref(Card) } } }
                    collection UpgradeTraceOrder { element = UpgradeTrace ordered = true }
                    procedure TraceUpgrade { parameters = { card = Ref(Card) } locals = { entry = Binding } body = {
                      create = { type = UpgradeTrace scope = entityScope(card) into = entry collection = UpgradeTraceOrder fields = { Card = card } }
                    } }
                    subscription TraceUpgrade { event = CardUpgraded reaction = TraceUpgrade priority = 1000 }
                    action AnotherWhetstone { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:whetstone" } } } }
                    """);
                var s = copy.Load().CreateSession(1, ["player"], vector.GetProperty("seed").GetString()); s.Start(); Healthy(s);
                var cards = new List<InstanceId>();
                foreach (var card in data.RootElement.GetProperty("cards").EnumerateArray())
                {
                    var cardName = card.GetProperty("name").GetString()!;
                    Submit(s, (cardName == "StrikeIronclad" ? "Strike" : cardName == "DefendIronclad" ? "Defend" : cardName) + ".Obtain");
                    var id = Members(s, "RunDeck").Last(); cards.Add(id);
                    Set(s, id, "Card", "Upgraded", Value.Of(card.GetProperty("upgraded").GetBoolean()));
                    Set(s, id, "Card", "CanUpgrade", Value.Of(card.GetProperty("can_upgrade").GetBoolean()));
                }
                var stepIndex = 0;
                foreach (var step in vector.GetProperty("steps").EnumerateArray())
                {
                    var relic = step.GetProperty("relic").GetString()!;
                    Submit(s, stepIndex++ == 1 ? "AnotherWhetstone" : relic + ".Obtain");
                    var selected = step.GetProperty("selected_indices").EnumerateArray().Select(i => cards[i.GetInt32()]).ToArray();
                    var actual = Members(s, "UpgradeTraceOrder").Select(t => s.State.GetField(t, s.RuleSet.TypeByQualifiedId("base-spire:UpgradeTrace").FieldIndex("Card")).AsRef()).ToArray();
                    True(selected.SequenceEqual(actual), "source selected instances and upgrade event order");
                    var upgraded = step.GetProperty("upgraded_indices").EnumerateArray().Select(i => cards[i.GetInt32()]).ToHashSet();
                    foreach (var card in cards) Equal(upgraded.Contains(card), CardUpgraded(s, card), "cumulative permanent results");
                    Equal(step.GetProperty("niche_counter").GetInt64(), s.Random.DrawCount("base-spire:run:niche"), "exact shared Niche counter");
                    Equal(s.Random.DrawCount("base-spire:run:niche"), s.Random.TotalDraws, "no unrelated stream consumption");
                    True(cards.SequenceEqual(Members(s, "RunDeck")), "source shuffle never reorders deck");
                    foreach (var trace in Members(s, "UpgradeTraceOrder").ToArray()) s.State.RunAtomic(() => s.State.DestroyInstance(trace));
                    Clean(s);
                }
            });
            foreach (var row in relics) witnesses[row.Id] = witnesses[row.Id].Append(name).ToArray();
        }
        check("UP nested upgrade hooks can acquire another upgrade relic without overwriting the parent selection", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("nested-upgrade.spire", """
                entity NestedUpgradeFlag { fields = { Used = { type = Bool default = false } } }
                procedure NestedUpgrade { parameters = { card = Ref(Card) } body = {
                  if = { condition = not(field(first(type(NestedUpgradeFlag)), NestedUpgradeFlag.Used)) then = {
                    set = { target = first(type(NestedUpgradeFlag)) field = NestedUpgradeFlag.Used value = true }
                    call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:war-paint" } }
                  } }
                } }
                subscription NestedUpgrade { event = CardUpgraded reaction = NestedUpgrade priority = 1000 }
                action EnableNestedUpgrade { role = player locals = { flag = Binding } body = { create = { type = NestedUpgradeFlag scope = frameScope() into = flag } } }
                """);
            var s = Start(copy.Load()); for (var i = 0; i < 3; i++) { Submit(s, "Strike.Obtain"); Submit(s, "Defend.Obtain"); }
            Submit(s, "EnableNestedUpgrade"); Submit(s, "Whetstone.Obtain");
            Equal(2, Members(s, "RunDeck").Count(c => CardDefinition(s, c) == "base-spire:strike-ironclad" && CardUpgraded(s, c)), "parent finishes both attacks");
            Equal(2, Members(s, "RunDeck").Count(c => CardDefinition(s, c) == "base-spire:defend-ironclad" && CardUpgraded(s, c)), "child finishes both skills");
            Equal(4L, s.Random.DrawCount("base-spire:run:niche"), "two independent three-card shuffles"); Clean(s);
        });
        check("UP acquisition listener binds the actual relic holder and skips a pickup melted before its reaction", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("upgrade-holder.spire", """
                action OtherUpgradeOwner { role = player locals = { other = Binding } body = {
                  create = { type = Player scope = frameScope() into = other fields = { Owner = participant(2) } }
                  call = { procedure = Strike.Obtain args = { owner = other } }
                  call = { procedure = Strike.Obtain args = { owner = other } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:whetstone" } }
                } }
                """);
            var s = copy.Load().CreateSession(20261018, ["player", "player"]); s.Start(); Healthy(s); Submit(s, "Strike.Obtain");
            var first = Members(s, "RunDeck").Single(); Submit(s, "OtherUpgradeOwner");
            True(!CardUpgraded(s, first), "other owner's pickup leaves first owner's permanent instance untouched");
            Equal(2, Members(s, "RunDeck").Count(c => CardUpgraded(s, c)), "only actual holder's cards upgraded"); Clean(s);
            using var melted = new Copy(package.Root); melted.Include("melt-pickup.spire", """
                procedure MeltPickup { parameters = { owner = Ref(Player) relic = Ref(Relic) } body = { set = { target = relic field = Relic.IsMelted value = true } } }
                subscription MeltPickup { event = RelicObtained reaction = MeltPickup priority = 99 }
                """);
            var inactive = Start(melted.Load()); Submit(inactive, "Strike.Obtain"); Submit(inactive, "Whetstone.Obtain");
            True(!CardUpgraded(inactive, Members(inactive, "RunDeck").Single()), "melted pickup listener is rechecked"); Equal(0L, inactive.Random.TotalDraws, "no hidden selection after invalidation"); Clean(inactive);
        });
        check("UP permanent upgrades survive relic removal and two rebuilt combats without replaying pickup or writing combat upgrades back", () =>
        {
            var s = Start(package); Submit(s, "Strike.Obtain"); Submit(s, "Defend.Obtain"); var deck = Members(s, "RunDeck").ToArray();
            Submit(s, "Whetstone.Obtain"); Submit(s, "RemoveOwnedRelic", Members(s, "Relics").Single());
            Submit(s, "AddDevelopmentTarget");
            for (var battle = 0; battle < 2; battle++)
            {
                Submit(s, "BeginDevelopmentCombat"); Submit(s, "Draw"); Submit(s, "Draw"); Submit(s, "BeginDevelopmentTurn");
                var attack = Hand(s).Single(c => CardDefinition(s, c) == "base-spire:strike-ironclad"); var skill = Hand(s).Single(c => CardDefinition(s, c) == "base-spire:defend-ironclad");
                True(CardUpgraded(s, attack) && !CardUpgraded(s, skill), "fresh copies reflect only permanent changes");
                var beforeHp = EnemyHp(s); IcPlay(s, attack); Equal(beforeHp - 9, EnemyHp(s), "upgraded strike deals nine");
                Submit(s, "UpgradeCardInHand", skill); Submit(s, "EndDevelopmentCombat");
                True(CardUpgraded(s, deck[0]) && !CardUpgraded(s, deck[1]), "temporary skill upgrade never writes back");
                Equal(0L, s.Random.DrawCount("base-spire:run:niche"), "single eligible pickup had no RNG and combat never repeats it"); Clean(s);
            }
        });
        check("UP invalid kind fails before creating selection state or consuming Niche", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("invalid-upgrade.spire", """
                action InvalidUpgrade { role = player body = { call = { procedure = UpgradeRandomPermanentCards args = { owner = first(type(Player)) kind = "power" amount = 2 } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "Strike.Obtain"); var before = s.State.StateHash();
            var action = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:InvalidUpgrade");
            Throws<EngineFaultException>(() => s.Submit(action.Definition, s.Participant(0), [])); Equal(SessionStatus.Faulted, s.Status, "unsupported selector is explicit");
            Equal(before, s.State.StateHash(), "invalid selector changes no stored game state"); Equal(0L, s.Random.TotalDraws, "invalid selector consumes no random"); Clean(s);
        });
        return witnesses;
    }
}
