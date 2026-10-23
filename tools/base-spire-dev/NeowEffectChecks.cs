using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static (Dictionary<string, string[]> Relics, Dictionary<string, string[]> Cards, Dictionary<string, string[]> Potions)
        RunNeowEffectChecks(DslPackage package, Action<string, Action> check)
    {
        const string opening = "NEOW-EFFECT real seed777 WingedBoots and CursedPearl preserve native deck, resources, first hand and RNG";
        const string travel = "NEOW-EFFECT WingedBoots source map travel consumes only three nonedge entries and keeps queries pure";
        const string greed = "NEOW-EFFECT CursedPearl preserves Greed eternal unplayable and acquisition-before-gold ordering";
        const string flex = "NEOW-EFFECT FlexPotion source temporary Strength stacks, changes, attack and expiry preserve original signed Strength";
        void RandomState(Session s, JsonElement expected, string stream)
        {
            var snapshot = s.Random.Capture();
            var state = snapshot.Streams.SingleOrDefault(x => x.Name == stream)?.BackendState
                ?? new Xoshiro256StarStar(snapshot.ExplicitSeeds[stream], protectZeroState: false).Capture();
            Equal(expected.GetProperty("counter").GetInt64(), s.Random.DrawCount(stream), "source RNG cursor: " + stream);
            True(state!.SequenceEqual(Enumerable.Range(0, 4).Select(i => expected.GetProperty("state" + i).GetUInt64())), "source RNG words: " + stream);
        }
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.neow-effects-vector.json")!);
        var vectors = data.RootElement;
        check(opening, () =>
        {
            foreach (var row in vectors.GetProperty("openings").EnumerateArray())
            {
                var s = package.CreateSession(1, ["player"], row.GetProperty("seed").GetString()); s.Start(); Submit(s, "StartNormalRun");
                var original = Members(s, "RunDeck").ToArray();
                var choice = row.GetProperty("choice").GetString() == "WingedBoots" ? "base-spire:winged-boots" : "base-spire:cursed-pearl";
                var option = s.PendingDecision!.Candidates.Single(o => TextField(s, o, "NeowOption", "RelicId") == choice);
                True(s.State.GetField(option, s.RuleSet.TypeByQualifiedId("base-spire:NeowOption").FieldIndex("EffectSupported")).AsBool(), "source option now has a real effect");
                s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, [option])); Healthy(s);
                void State(JsonElement expected)
                {
                    Equal(expected.GetProperty("hp").GetInt64(), Hp(s), "native HP"); Equal(expected.GetProperty("gold").GetInt64(), Field(s, "Gold"), "native gold");
                    var deck = Members(s, "RunDeck"); var source = expected.GetProperty("deck").EnumerateArray().ToArray();
                    Equal(source.Length, deck.Count, "complete permanent deck");
                    True(original.SequenceEqual(deck.Take(10)), "original permanent instances remain in order");
                    for (var i = 0; i < source.Length; i++)
                    {
                        Equal(source[i].GetProperty("id").GetString(), CardDefinition(s, deck[i]), "source permanent definition order");
                        Equal(source[i].GetProperty("upgradable").GetBoolean(), s.State.GetField(deck[i], s.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("CanUpgrade")).AsBool(), "source upgrade eligibility");
                        Equal(source[i].GetProperty("cost").GetInt64(), IntField(s, deck[i], "Card", "BaseCost"), "source permanent cost");
                    }
                    if (expected.GetProperty("hand").ValueKind == JsonValueKind.Array)
                        True(Hand(s).Select(c => CardDefinition(s, c)).SequenceEqual(expected.GetProperty("hand").EnumerateArray().Select(c => c.GetString())), "native first hand, including curse sorting and shuffle");
                    foreach (var pair in new[] { ("up_front", "base-spire:run:up_front"), ("niche", "base-spire:run:niche"), ("shuffle", "base-spire:run:shuffle"), ("rewards", "base-spire:player:rewards"), ("neow", "base-spire:event:neow") })
                        RandomState(s, expected.GetProperty("rng").GetProperty(pair.Item1), pair.Item2);
                }
                State(row.GetProperty("obtained")); s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true)); Healthy(s);
                string Coord(InstanceId n) => IntField(s, n, "MapNode", "Col") + "," + IntField(s, n, "MapNode", "Row");
                var legal = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
                True(legal.Bindings.Select(b => Coord(b[0].AsRef())).Order().SequenceEqual(row.GetProperty("legal").EnumerateArray().Select(c => c[0] + "," + c[1]).Order()), "native map candidates after source acquisition");
                var first = row.GetProperty("first"); var node = Members(s, "MapNodes").Single(n => Coord(n) == first[0] + "," + first[1]);
                Submit(s, "ChooseMapPoint", node); State(row.GetProperty("started"));
                Equal(0L, IntField(s, Members(s, "Relics").Single(r => TextField(s, r, "Relic", "DefinitionId") == choice), "Relic", "Counter"), "ordinary first edge consumes no boot use");
            }
        });
        check(travel, () =>
        {
            using var copy = new Copy(package.Root); copy.Include("travel-probe.spire", """
                action ControlledTravelSetup { role = player body = {
                  set = { target = first(type(Player)) field = Player.CurrentRun value = first(type(Run)) }
                  set = { target = first(type(Run)) field = Run.TotalFloor value = 1 }
                  call = { procedure = BuildNormalActOneMap args = { owner = first(type(Player)) run = first(type(Run)) } }
                } }
                action ControlledTravel { role = player parameters = { node = Ref(MapNode) } candidates = { parameter = node query = where(collection(MapNodes), n, pureCall(CanTravelToNormalMapPoint, first(type(Player)), first(type(Run)), n)) } locals = { visit = Binding } requires = pureCall(CanTravelToNormalMapPoint, first(type(Player)), first(type(Run)), node) body = {
                  call = { procedure = BeginNormalMapVisit args = { run = first(type(Run)) node = node } into = visit }
                  call = { procedure = NotifyNormalRoomEntered args = { owner = first(type(Player)) visit = visit } }
                  call = { procedure = RefreshNormalMapChoices args = { owner = first(type(Player)) run = first(type(Run)) } }
                } }
                action RepeatEntry { role = player body = { call = { procedure = NotifyNormalRoomEntered args = { owner = first(type(Player)) visit = field(first(type(Run)), Run.CurrentVisit) } } } }
                action MeltTravelProbe { role = player body = {
                  forEach = { query = where(collection(Relics), r, equal(field(r, Relic.DefinitionId), "base-spire:winged-boots")) item = boots body = {
                    set = { target = boots field = Relic.Counter value = 0 }
                    set = { target = boots field = Relic.IsMelted value = true }
                  } }
                  call = { procedure = RefreshNormalMapChoices args = { owner = first(type(Player)) run = first(type(Run)) } }
                } }
                action RemoveTravelProbe { role = player body = {
                  call = { procedure = RemoveRelic args = { instance = first(where(collection(Relics), r, equal(field(r, Relic.DefinitionId), "base-spire:winged-boots"))) } }
                } }
                action RestoreTravelProbe { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:winged-boots" } } } }
                """);
            var s = copy.Load().CreateSession(1, ["player"], "777"); s.Start(); Submit(s, "Ironclad.Initialize"); Submit(s, "WingedBoots.Obtain"); Submit(s, "ControlledTravelSetup");
            var boots = Members(s, "Relics").Single(r => TextField(s, r, "Relic", "DefinitionId") == "base-spire:winged-boots");
            string Coord(InstanceId n) => IntField(s, n, "MapNode", "Col") + "," + IntField(s, n, "MapNode", "Row");
            void RejectTravel(InstanceId node)
            {
                var hash = s.State.StateHash(); var random = JsonSerializer.Serialize(s.Random.Capture());
                Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ControlledTravel"), s.Participant(0), [Value.OfRef(node)]));
                Equal(hash, s.State.StateHash(), "illegal travel is atomic"); Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "illegal travel consumes no RNG");
            }
            RejectTravel(Members(s, "MapNodes").First(n => IntField(s, n, "MapNode", "Row") == 3));
            void Choices(JsonElement expected)
            {
                var hash = s.State.StateHash(); var random = JsonSerializer.Serialize(s.Random.Capture());
                var actual = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint").Bindings.Select(b => Coord(b[0].AsRef())).Order().ToArray();
                True(actual.SequenceEqual(expected.EnumerateArray().Select(c => c[0] + "," + c[1]).Order()), "native travel candidates");
                True(actual.SequenceEqual(Members(s, "MapNodes").Where(n => s.State.GetField(n, s.RuleSet.TypeByQualifiedId("base-spire:MapNode").FieldIndex("Selectable")).AsBool()).Select(Coord).Order()), "public Selectable agrees with actual candidates");
                Equal(hash, s.State.StateHash(), "travel query is pure"); Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "travel query never consumes RNG");
            }
            foreach (var row in vectors.GetProperty("travel").EnumerateArray())
            {
                Choices(row.GetProperty("legal")); var next = row.GetProperty("next");
                Submit(s, "ControlledTravel", Members(s, "MapNodes").Single(n => Coord(n) == next[0] + "," + next[1]));
                Equal(row.GetProperty("used").GetInt64(), IntField(s, boots, "Relic", "Counter"), "source three-use lifecycle");
                var hash = s.State.StateHash(); Submit(s, "RepeatEntry"); Equal(hash, s.State.StateHash(), "same visit cannot count twice");
                Choices(row.GetProperty("after"));
            }
            var final = vectors.GetProperty("travel").EnumerateArray().Last().GetProperty("after");
            var run = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
            var current = s.State.GetField(run, s.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex("CurrentNode")).AsRef();
            var children = s.State.RelationTargets(s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren").Id, current);
            RejectTravel(Members(s, "MapNodes").First(n => IntField(s, n, "MapNode", "Row") == IntField(s, current, "MapNode", "Row") + 1 && !children.Contains(n)));
            Submit(s, "MeltTravelProbe"); Choices(final); Submit(s, "RemoveTravelProbe"); Choices(final);
            // Controlled state isolates the out-of-grid boss fallback and parent/child entry guard.
            Submit(s, "RestoreTravelProbe"); boots = Members(s, "Relics").Single(r => TextField(s, r, "Relic", "DefinitionId") == "base-spire:winged-boots");
            var top = Members(s, "MapNodes").First(n => IntField(s, n, "MapNode", "Row") == 15);
            Set(s, run, "Run", "CurrentNode", Value.OfRef(top));
            var bossChildren = s.State.RelationTargets(s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren").Id, top);
            var actual = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint").Bindings.Select(b => b[0].AsRef());
            True(actual.SequenceEqual(bossChildren), "free travel outside grid falls back to ordinary boss connections");
            var visit = Members(s, "RunVisits").Last();
            Set(s, visit, "RunVisit", "RoomEntered", Value.Of(false)); Set(s, visit, "RunVisit", "Parent", Value.OfRef(Members(s, "RunVisits").First()));
            var childHash = s.State.StateHash(); Submit(s, "RepeatEntry"); Equal(childHash, s.State.StateHash(), "child room entry never consumes travel");
        });
        check(greed, () =>
        {
            using var copy = new Copy(package.Root); copy.Include("greed-probe.spire", """
                procedure Acquired { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = equal(field(card, Card.DefinitionId), "base-spire:greed") then = {
                    if = { condition = not(and(equal(field(owner, Player.Gold), 99), and(greaterOrEqual(collectionIndex(RunDeck, card), 0), and(pureCall(CardHasKeyword, card, "eternal"), pureCall(CardHasKeyword, card, "unplayable"))))) then = { fail = { message = "Greed must enter the deck before CursedPearl gold" } } }
                  } }
                } }
                subscription Acquired { event = CardObtained reaction = Acquired }
                action RemoveGreedProbe { role = player parameters = { card = Ref(Card) } body = { call = { procedure = RemovePermanentCard args = { owner = first(type(Player)) card = card } } } }
                action UpgradeGreedProbe { role = player parameters = { card = Ref(Card) } body = { call = { procedure = UpgradeCard args = { card = card } } } }
                action CheckGreedProbe { role = player body = {
                  forEach = { query = where(type(Card), c, equal(field(c, Card.DefinitionId), "base-spire:greed")) item = card body = {
                    if = { condition = or(field(card, Card.CanUpgrade), or(not(pureCall(CardHasKeyword, card, "eternal")), or(not(pureCall(CardHasKeyword, card, "unplayable")), pureCall(CardPlayAllowed, first(type(Player)), card, false)))) then = { fail = { message = "Every Greed instance must retain source restrictions" } } }
                  } }
                  if = { condition = exists(where(collection(CardLibrary), b, and(equal(field(b, CardBlueprint.DefinitionId), "base-spire:greed"), field(b, CardBlueprint.Generatable)))) then = { fail = { message = "Greed cannot enter the combat generation pool" } } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "Ironclad.Initialize"); Submit(s, "CursedPearl.Obtain");
            var card = Members(s, "RunDeck").Single(c => CardDefinition(s, c) == "base-spire:greed");
            Equal(432L, Field(s, "Gold"), "source pickup gold"); var hash = s.State.StateHash(); var random = s.Random.TotalDraws;
            RejectNormalAction(s, "CursedPearl.Obtain");
            Throws<EngineFaultException>(() => Submit(s, "RemoveGreedProbe", card));
            Equal(hash, s.State.StateHash(), "eternal removal rejects before mutation"); Equal(random, s.Random.TotalDraws, "curse acquisition and rejection draw nothing");
            s = Start(copy.Load()); Submit(s, "Ironclad.Initialize"); Submit(s, "CursedPearl.Obtain");
            card = Members(s, "RunDeck").Single(c => CardDefinition(s, c) == "base-spire:greed");
            Submit(s, "BattleFriendV2.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "CheckGreedProbe");
            var copyCard = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Single(c => c != card && CardDefinition(s, c) == "base-spire:greed");
            hash = s.State.StateHash(); random = s.Random.TotalDraws;
            Throws<EngineFaultException>(() => Submit(s, "UpgradeGreedProbe", copyCard));
            Equal(hash, s.State.StateHash(), "combat curse upgrade rejects atomically"); Equal(random, s.Random.TotalDraws, "rejected combat curse upgrade consumes no RNG");
        });
        check(flex, () =>
        {
            using var copy = new Copy(package.Root); copy.Include("flex-probe.spire", """
                action InitialStrength { role = player parameters = { amount = Int } body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = amount } } } }
                action ChangeFlex { role = player body = { call = { procedure = ModifyPowerAmount args = { power = first(where(collection(Powers), p, equal(field(p, Power.DefinitionId), "base-spire:flex-potion-power"))) delta = -3 } } } }
                action ZeroFlex { role = player body = { call = { procedure = ModifyPowerAmount args = { power = first(where(collection(Powers), p, equal(field(p, Power.DefinitionId), "base-spire:flex-potion-power"))) delta = -5 } } } }
                """);
            var rules = copy.Load();
            foreach (var row in vectors.GetProperty("flex").EnumerateArray())
            {
                var s = rules.CreateSession(1, ["player"], row.GetProperty("seed").GetString()); s.Start(); Submit(s, "Ironclad.Initialize"); Submit(s, "BattleFriendV2.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                var grant = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:InitialStrength"); s.Submit(grant, s.Participant(0), [Value.Of(row.GetProperty("baseline").GetInt64())]); Healthy(s);
                var step = 0;
                void State()
                {
                    var expected = row.GetProperty("steps")[step++];
                    var holder = s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("HolderPlayer");
                    var actual = Members(s, "Powers").Where(p => s.State.GetField(p, holder).Kind == ValueKind.Ref && s.State.GetField(p, holder).AsRef() == Player(s)).Select(p => TextField(s, p, "Power", "DefinitionId") + ":" + IntField(s, p, "Power", "Stacks"));
                    True(actual.SequenceEqual(expected.GetProperty("powers").EnumerateArray().Select(p => p.GetProperty("id").GetString() + ":" + p.GetProperty("amount"))), "native Power order and signed amounts");
                    Equal(expected.GetProperty("target_hp").GetInt64(), EnemyHp(s), "real source attack uses temporary Strength");
                    Equal(expected.GetProperty("potion_count").GetInt32(), Members(s, "Potions").Count, "source potion consumption");
                    RandomState(s, expected.GetProperty("shuffle"), "base-spire:run:shuffle"); RandomState(s, expected.GetProperty("rewards"), "base-spire:player:rewards");
                }
                for (var i = 0; i < 2; i++)
                {
                    Submit(s, "FlexPotion.Obtain"); var potion = Members(s, "Potions").Single(); Submit(s, "UsePotion", potion); State();
                    var hash = s.State.StateHash(); var random = JsonSerializer.Serialize(s.Random.Capture());
                    Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:UsePotion"), s.Participant(0), [Value.OfRef(potion)]));
                    Equal(hash, s.State.StateHash(), "consumed potion cannot be reused"); Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "duplicate consumption draws no RNG");
                }
                Submit(s, "ChangeFlex"); State();
                Submit(s, "PlayAttackFromHand", Hand(s).First(c => CardDefinition(s, c) == row.GetProperty("attack").GetString())); State();
                Submit(s, "EndDevelopmentTurn"); State();
                Submit(s, "BeginDevelopmentTurn"); Submit(s, "FlexPotion.Obtain"); Submit(s, "UsePotion", Members(s, "Potions").Single()); Submit(s, "ZeroFlex");
                True(!Members(s, "Powers").Any(p => TextField(s, p, "Power", "DefinitionId") == "base-spire:flex-potion-power"), "zero marker removes after withdrawing all its Strength");
                long Strength() => Members(s, "Powers").Where(p => TextField(s, p, "Power", "DefinitionId") == "base-spire:strength-power").Sum(p => IntField(s, p, "Power", "Stacks"));
                Equal(row.GetProperty("baseline").GetInt64(), Strength(), "zeroing marker retains signed original Strength");
                Submit(s, "EndDevelopmentTurn"); Equal(row.GetProperty("baseline").GetInt64(), Strength(), "removed marker cannot withdraw twice");
                Submit(s, "BeginDevelopmentTurn"); Submit(s, "FlexPotion.Obtain"); Submit(s, "UsePotion", Members(s, "Potions").Single()); Submit(s, "EndDevelopmentCombat");
                Submit(s, "BeginDevelopmentCombat");
                True(!Members(s, "Powers").Any(p => TextField(s, p, "Power", "DefinitionId") is "base-spire:strength-power" or "base-spire:flex-potion-power"), "new combat carries neither Strength nor temporary marker");
            }
            var outside = Start(package); Submit(outside, "FlexPotion.Obtain");
            var potionOutside = Members(outside, "Potions").Single(); var before = outside.State.StateHash();
            Throws<EngineFaultException>(() => outside.Submit(outside.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:UsePotion"), outside.Participant(0), [Value.OfRef(potionOutside)]));
            Equal(before, outside.State.StateHash(), "combat-only use cannot consume a potion outside combat");
        });
        return (new() { ["base-spire:winged-boots"] = [opening, travel], ["base-spire:cursed-pearl"] = [opening, greed] },
            new() { ["base-spire:greed"] = [opening, greed] }, new() { ["base-spire:flex-potion"] = [flex] });
    }
}
