using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunNormalRoomsOnly(DslPackage package)
    {
        foreach (var option in new[] { "rest", "smith", "cancel" })
        {
            using var copy = new Copy(package.Root);
            copy.Include("normal-room-probe.spire", """
                action ReachRestSiteProbe { role = player locals = { run = Binding node = Binding } body = {
                  let = { into = run value = field(first(type(Player)), Player.CurrentRun) }
                  let = { into = node value = first(where(collection(MapNodes), n, field(n, MapNode.Selectable))) }
                  set = { target = node field = MapNode.Type value = enum(MapPointType, RestSite) }
                  set = { target = first(type(Player)) field = Player.Hp value = 40 }
                } }
                """);
            var s = copy.Load().CreateSession(777, ["player"], "777"); s.Start();
            Submit(s, "StartNormalRun");
            var conch = s.PendingDecision!.Candidates.Single(c => TextField(s, c, "NeowOption", "RelicId") == "base-spire:booming-conch");
            s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, Instances: [conch]));
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
            Submit(s, "ReachRestSiteProbe");
            var draws = s.Random.TotalDraws;
            var choose = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
            s.Submit(choose.Definition, s.Participant(0), choose.Bindings[0]); Healthy(s);
            Equal("rest-site", TextField(s, Player(s), "Player", "Room"), "normal visit enters campfire");
            var visit = Members(s, "RunVisits").Single();
            True(!Legal(s, "LeaveDevelopmentRoom"), "must finish a rest option");
            var deck = Members(s, "RunDeck").ToArray();
            if (option == "rest")
            {
                Submit(s, "RestAtDevelopmentCampfire");
                Equal(64L, IntField(s, Player(s), "Player", "Hp"), "source A0 rest heals 30 percent of 80");
            }
            else
            {
                Submit(s, "ForgeAtDevelopmentCampfire");
                var request = s.PendingDecision!;
                if (option == "cancel")
                {
                    s.Answer(new DecisionAnswer(request.RequestId, Instances: [])); Healthy(s);
                    True(Legal(s, "RestAtDevelopmentCampfire"), "cancellation preserves the unused choice");
                    Submit(s, "RestAtDevelopmentCampfire");
                }
                else
                {
                    var card = request.Candidates[0];
                    s.Answer(new DecisionAnswer(request.RequestId, Instances: [card])); Healthy(s);
                    True(Members(s, "RunDeck").Contains(card), "smith retains permanent identity");
                    True(s.State.GetField(card, s.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex("Upgraded")).AsBool(), "smith upgrades the selected permanent card");
                }
            }
            True(!Legal(s, "RestAtDevelopmentCampfire"), "one successful option per visit");
            Submit(s, "LeaveDevelopmentRoom");
            Equal("map", TextField(s, Player(s), "Player", "Room"), "normal campfire closes to map");
            Equal("closed", TextField(s, visit, "RunVisit", "Status"), "normal visit closes");
            True(deck.SequenceEqual(Members(s, "RunDeck")), "permanent deck identities survive");
            Equal(draws, s.Random.TotalDraws, "campfire does not consume RNG");
            True(Legal(s, "ChooseMapPoint"), "source successors become available");
            True(!Legal(s, "LeaveDevelopmentRoom"), "cannot close the campfire twice");
            Console.WriteLine("ok   NORMAL ROOM controlled campfire " + option);
        }
        RunNormalItemRoomChecks(package);
        return 0;
    }
    private static void RunNormalItemRoomChecks(DslPackage package)
    {
        using var input = System.Text.Json.JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-room-vectors.json")!);
        foreach (var room in new[] { "Shop", "Treasure" })
        {
            using var copy = new Copy(package.Root);
            copy.Include("room-probe.spire", """
                action SetNormalItemRoomProbe { role = player locals = { node = Binding } body = {
                  let = { into = node value = first(where(collection(MapNodes), n, field(n, MapNode.Selectable))) }
                  set = { target = node field = MapNode.Type value = enum(MapPointType, ROOM) }
                } }
                action FillMerchantPotionsProbe { role = player locals = { potion = Binding } body = {
                  repeat = { count = 2 body = {
                    call = { procedure = CreatePotionInstance args = { owner = first(type(Player)) definition = "base-spire:fire-potion" } into = potion }
                    call = { procedure = ObtainPotion args = { owner = first(type(Player)) potion = potion } }
                  } }
                } }
                """.Replace("ROOM", room));
            var s = copy.Load().CreateSession(777, ["player"], "777"); s.Start(); Submit(s, "StartNormalRun");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Instances: [s.PendingDecision.Candidates.Single(c => TextField(s, c, "NeowOption", "RelicId") == "base-spire:booming-conch")]));
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
            using var catalog = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(package.Root, "relics/catalog.json")));
            var implementedRelics = catalog.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("id").GetString()).ToHashSet();
            var relicMetadata = s.RuleSet.TypeByQualifiedId("base-spire:RoomRelicMetadata");
            foreach (var metadata in s.State.AllInstances(relicMetadata.Id))
                Equal(implementedRelics.Contains(TextField(s, metadata, "RoomRelicMetadata", "DefinitionId")), s.State.GetField(metadata, relicMetadata.FieldIndex("Implemented")).AsBool(), "room support matches the actual executable catalog");
            Submit(s, "SetNormalItemRoomProbe");
            var choice = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
            s.Submit(choice.Definition, s.Participant(0), choice.Bindings[0]); Healthy(s);
            var expected = input.RootElement.GetProperty("vectors").EnumerateArray().Single(e => e.GetProperty("kind").GetString() == room.ToLowerInvariant());
            Equal(expected.GetProperty("rewards").GetInt64(), s.Random.DrawCount("base-spire:player:rewards"), "native reward draws for " + room);
            if (room == "Treasure")
            {
                Equal(expected.GetProperty("gold").GetInt64(), IntField(s, Player(s), "Player", "Gold"), "native chest gold");
                True(Members(s, "Relics").Select(r => TextField(s, r, "Relic", "DefinitionId")).SequenceEqual(expected.GetProperty("relics").EnumerateArray().Select(e => e.GetString())), "native chest relic");
                Equal(1L, s.Random.DrawCount("base-spire:run:treasure_room_relics"), "native chest rarity draws");
            }
            else
            {
                foreach (var (kind, key) in new[] { ("card", "cards"), ("relic", "relics"), ("potion", "potions") })
                {
                    var actual = Members(s, "MerchantInventory").Where(e => TextField(s, e, "MerchantStock", "Kind") == kind).ToArray();
                    var values = expected.GetProperty(key).EnumerateArray().ToArray();
                    Equal(values.Length, actual.Length, "native shelf count " + kind);
                    for (var i = 0; i < values.Length; i++)
                    {
                        Equal(values[i].GetProperty("id").GetString(), TextField(s, actual[i], "MerchantStock", "DefinitionId"), "native shelf ID " + kind + i);
                        Equal(values[i].GetProperty("price").GetInt64(), IntField(s, actual[i], "MerchantStock", "Price"), "native shelf price " + kind + i);
                    }
                }
                Equal(expected.GetProperty("shops").GetInt64(), s.Random.DrawCount("base-spire:player:shops"), "native shop draws");
                var stock = Members(s, "MerchantInventory");
                var unsupported = stock.First(e => !s.State.GetField(e, s.RuleSet.TypeByQualifiedId("base-spire:MerchantStock").FieldIndex("Implemented")).AsBool());
                True(!s.LegalActions(s.Participant(0)).Where(a => a.Definition.QualifiedId == "base-spire:BuyMerchantStock").SelectMany(a => a.Bindings).Any(b => b[0].AsRef() == unsupported), "unimplemented source shelf is visible but not purchasable");
                Set(s, Player(s), "Player", "Gold", Value.Of(0));
                var unchanged = s.State.StateHash(); var cursor = s.Random.TotalDraws;
                True(!Legal(s, "BuyMerchantStock"), "insufficient gold exposes no purchase");
                Equal(unchanged, s.State.StateHash(), "purchase query cannot mutate stock");
                Equal(cursor, s.Random.TotalDraws, "purchase query consumes no RNG");
                Set(s, Player(s), "Player", "Gold", Value.Of(99));
                var card = stock.First(e => TextField(s, e, "MerchantStock", "Kind") == "card");
                var gold = IntField(s, Player(s), "Player", "Gold");
                Submit(s, "BuyMerchantStock", card);
                Equal(gold - IntField(s, card, "MerchantStock", "Price"), IntField(s, Player(s), "Player", "Gold"), "purchase debits exact price");
                Equal(11, Members(s, "RunDeck").Count, "purchase adds permanent card");
                var buy = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:BuyMerchantStock");
                var afterBuy = s.State.StateHash();
                Throws<EngineFaultException>(() => s.Submit(buy, s.Participant(0), [Value.OfRef(card)]));
                Equal(afterBuy, s.State.StateHash(), "a sold entry cannot debit gold or add another card");
                Set(s, Player(s), "Player", "Gold", Value.Of(1000));
                Submit(s, "MerchantRemoveCard", Members(s, "RunDeck")[0]);
                Equal(925L, IntField(s, Player(s), "Player", "Gold"), "A0 first removal costs 75");
                Equal(10, Members(s, "RunDeck").Count, "removal changes persistent deck");
                True(!Legal(s, "MerchantRemoveCard"), "one removal per merchant");
                Submit(s, "BuyMerchantStock", stock.Single(e => TextField(s, e, "MerchantStock", "DefinitionId") == "base-spire:vajra"));
                True(Members(s, "Relics").Any(e => TextField(s, e, "Relic", "DefinitionId") == "base-spire:vajra"), "real relic purchase");
                var potion = stock.First(e => TextField(s, e, "MerchantStock", "Kind") == "potion");
                Submit(s, "BuyMerchantStock", potion); Equal(1, Members(s, "Potions").Count, "real potion procurement");
                Submit(s, "FillMerchantPotionsProbe");
                var unsold = stock.First(e => TextField(s, e, "MerchantStock", "Kind") == "potion" && e != potion);
                var fullState = s.State.StateHash();
                Throws<EngineFaultException>(() => s.Submit(buy, s.Participant(0), [Value.OfRef(unsold)]));
                Equal(fullState, s.State.StateHash(), "full inventory cannot debit gold or consume the unsold offer");
                Submit(s, "DiscardPotion", Members(s, "Potions")[0]);
                Submit(s, "BuyMerchantStock", unsold);
                Equal(3, Members(s, "Potions").Count, "the same unsold offer can be bought after freeing a slot");
            }
            Submit(s, "LeaveNormalItemRoom");
            Equal("map", TextField(s, Player(s), "Player", "Room"), "item room returns to source map");
            True(!Legal(s, "LeaveNormalItemRoom"), "item room closes once");
            Console.WriteLine("ok   NORMAL ROOM native controlled " + room);
        }
    }

}
