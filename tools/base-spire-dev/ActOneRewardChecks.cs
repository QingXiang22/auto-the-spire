using System.Globalization;
using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunActOneRewardChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-reward-vectors.json")!);
        using var opening = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-opening-vectors.json")!);
        using var copy = new Copy(package.Root);
        copy.Include("act-one-reward-probes.spire", """
            procedure ResetRewardsProbe { parameters = { owner = Ref(Player) } locals = { rewards = Binding } body = {
              let = { into = rewards value = field(owner, Player.CurrentRewardSet) }
              call = { procedure = ClearCombatCardReward args = { owner = owner } }
              call = { procedure = ClearCombatPotionReward args = { owner = owner } }
              if = { condition = not(isNone(rewards)) then = {
                set = { target = rewards field = CombatRewardSet.Closed value = true }
                set = { target = rewards field = CombatRewardSet.RelicPending value = false }
                set = { target = owner field = Player.CurrentRewardSet value = none }
              } }
              set = { target = owner field = Player.GoldRewardPending value = false }
              set = { target = owner field = Player.GoldRewardAmount value = 0 }
              set = { target = owner field = Player.Room value = "none" }
            } }
            action PrepareActOneRewardRun { role = player body = {
              call = { procedure = InitializeNormalIronclad args = { owner = first(type(Player)) } }
              call = { procedure = InitializeSourceOpeningPools args = { run = first(type(Run)) } }
              call = { procedure = InitializeMerchantSources }
              set = { target = first(type(Player)) field = Player.CurrentRun value = first(type(Run)) }
            } }
            action ResetActOneRewards { role = player body = { call = { procedure = ResetRewardsProbe args = { owner = first(type(Player)) } } } }
            action OfferActOneEliteRewardOne { role = player body = { call = { procedure = OfferNormalCombatRewards args = { owner = first(type(Player)) visitId = 1 source = "elite" actIndex = 0 ascension = 0 } } } }
            action OfferActOneEliteRewardTwo { role = player body = { call = { procedure = OfferNormalCombatRewards args = { owner = first(type(Player)) visitId = 2 source = "elite" actIndex = 0 ascension = 0 } } } }
            action OfferActOneBossRewardOne { role = player body = { call = { procedure = OfferNormalCombatRewards args = { owner = first(type(Player)) visitId = 1 source = "boss" actIndex = 0 ascension = 0 } } } }
            action OfferActOneBossRewardTwo { role = player body = { call = { procedure = OfferNormalCombatRewards args = { owner = first(type(Player)) visitId = 2 source = "boss" actIndex = 0 ascension = 0 } } } }
            """);
        var rules = copy.Load();
        var metadata = rules.Rules.TypeByQualifiedId("base-spire:RoomRelicMetadata");
        InstanceId Current(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("CurrentRewardSet")).AsRef();
        string PotionId(Session s, InstanceId p) => TextField(s, p, "Potion", "DefinitionId");
        bool Claiming(Session s) => s.LegalActions(s.Participant(0)).Any(a => a.Definition.QualifiedId == "base-spire:ClaimCombatRelicReward");
        bool SetBool(Session s, InstanceId set, string name) => s.State.GetField(set, s.RuleSet.TypeByQualifiedId("base-spire:CombatRewardSet").FieldIndex(name)).AsBool();
        bool Implemented(Session s, string id) => s.State.AllInstances(metadata.Id)
            .Where(m => TextField(s, m, "RoomRelicMetadata", "DefinitionId") == id)
            .Select(m => s.State.GetField(m, metadata.FieldIndex("Implemented")).AsBool()).Single();
        string[] Bag(JsonElement vector, string category, string rarity) => vector.GetProperty("bags").EnumerateArray()
            .Where(b => b.GetProperty("category").GetString() == category && b.GetProperty("rarity").GetString() == rarity)
            .SelectMany(b => b.GetProperty("ids").EnumerateArray().Select(i => i.GetString()!)).ToArray();
        bool InBag(Session s, string category, string rarity, string id)
        {
            var list = s.RuleSet.TypeByQualifiedId("base-spire:OpeningList");
            var bag = s.State.AllInstances(list.Id).Single(l => TextField(s, l, "OpeningList", "Category") == category && TextField(s, l, "OpeningList", "Rarity") == rarity);
            var relation = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:OpeningItems").Id;
            return s.State.RelationTargets(relation, bag)
                .Select(i => s.State.GetField(i, s.RuleSet.TypeByQualifiedId("base-spire:OpeningListItem").FieldIndex("Definition")).AsRef())
                .Any(d => TextField(s, d, "OpeningDefinition", "DefinitionId") == id);
        }
        void CompareCalls(Session s, int start, JsonElement offer, string label)
        {
            var expected = offer.GetProperty("calls").EnumerateArray().ToArray();
            var calls = s.Debug!.Records.Skip(start).Where(r => r.Random is not null).ToArray();
            Equal(expected.Length, calls.Length, label + " no missing or extra draws");
            for (var i = 0; i < calls.Length; i++)
            {
                var call = calls[i].Random!; var e = expected[i]; var operation = e.GetProperty("operation").GetString();
                Equal(RewardStream, calls[i].Subject, label + " source stream");
                Equal(e.GetProperty("before").GetInt64(), call.Before, label + " cursor before " + e.GetProperty("label"));
                Equal(e.GetProperty("after").GetInt64(), call.After, label + " cursor after " + e.GetProperty("label"));
                Equal(operation == "sampleFloat32" ? "double" : operation == "samplePick" ? "pick" : "int", call.Operation, label + " draw operation " + e.GetProperty("label"));
                if (operation == "sampleFloat32")
                    Equal(e.GetProperty("result").GetInt64(), (long)BitConverter.SingleToUInt32Bits((float)double.Parse(call.Result!, CultureInfo.InvariantCulture)), label + " independent float result");
                else
                {
                    Equal(e.GetProperty("min").ToString(), call.Minimum, label + " integer minimum");
                    Equal(e.GetProperty("max").ToString(), call.Maximum, label + " integer maximum");
                    if (operation == "samplePick")
                    {
                        True(!call.CandidatesTruncated, label + " complete candidates");
                        var type = e.GetProperty("label").GetString() == "potion-pick" ? "PotionBlueprint" : "CardBlueprint";
                        var field = s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex("DefinitionId");
                        var ids = call.Candidates!.Select(c => s.State.GetField(c, field).AsString()).ToArray();
                        True(ids.SequenceEqual(e.GetProperty("candidates").EnumerateArray().Select(x => x.GetString())), label + " source ordered candidate domain");
                        Equal(e.GetProperty("result").GetString(), ids[int.Parse(call.Result!, CultureInfo.InvariantCulture)], label + " selected identity");
                    }
                    else Equal(e.GetProperty("result").ToString(), call.Result, label + " integer result");
                }
            }
        }
        void CompareOffer(Session s, JsonElement offer, string label)
        {
            Equal(offer.GetProperty("gold").GetInt64(), Field(s, "GoldRewardAmount"), label + " source gold range");
            var potion = offer.GetProperty("potion"); var offered = Members(s, "PotionRewardOptions");
            Equal(potion.ValueKind == JsonValueKind.Null ? 0 : 1, offered.Count, label + " actual potion drop");
            if (offered.Count == 1) Equal(potion.GetString(), PotionId(s, offered[0]), label + " full source potion result");
            var cards = Members(s, "CardRewardOptions"); var expectedCards = offer.GetProperty("cards").EnumerateArray().ToArray();
            for (var i = 0; i < 3; i++)
            {
                Equal(expectedCards[i].GetProperty("id").GetString(), CardDefinition(s, cards[i]), label + " card rarity-pool result");
                Equal(expectedCards[i].GetProperty("upgraded").GetBoolean(), CardUpgraded(s, cards[i]), label + " upgrade roll");
            }
            Equal(offer.GetProperty("odds_bits").GetInt64(), Field(s, "PotionRewardOdds"), label + " binary32 pity odds");
            Equal(offer.GetProperty("offset_bits").GetInt64(), Field(s, "CardRarityOffset"), label + " binary32 card offset");
        }
        Session Prepared(string seed)
        {
            var s = rules.CreateSession(1, ["player"], seed, new SessionDebugOptions(MaxRecords: 100_000));
            s.Start(); Healthy(s); Submit(s, "PrepareActOneRewardRun"); return s;
        }
        JsonElement Opening(string seed) => opening.RootElement.GetProperty("vectors").EnumerateArray().Single(v => v.GetProperty("seed").GetString() == seed);

        Case("REWARD elite: source gold, potion bonus, elite rarity odds, ordered draws and player-bag relic", () =>
        {
            foreach (var vector in data.RootElement.GetProperty("elite_vectors").EnumerateArray())
            {
                var seed = vector.GetProperty("seed").GetString()!;
                var bags = Opening(seed);
                var s = Prepared(seed);
                var removed = new HashSet<string>();
                var offers = vector.GetProperty("offers").EnumerateArray().ToArray();
                for (var visit = 0; visit < offers.Length; visit++)
                {
                    var label = "elite seed " + seed + " visit " + (visit + 1);
                    var start = s.Debug!.Records.Count;
                    Submit(s, visit == 0 ? "OfferActOneEliteRewardOne" : "OfferActOneEliteRewardTwo");
                    CompareOffer(s, offers[visit], label);
                    CompareCalls(s, start, offers[visit], label);
                    Equal(offers[visit].GetProperty("counter").GetInt64(), s.Random.DrawCount(RewardStream), label + " complete shared cursor");
                    var rarity = offers[visit].GetProperty("relic_rarity").GetString()!;
                    var relic = Bag(bags, "player-bag", rarity).First(id => !removed.Contains(id));
                    var set = Current(s);
                    var supported = Implemented(s, relic);
                    Equal(relic, TextField(s, set, "CombatRewardSet", "RelicId"), label + " source player-bag front by rolled rarity");
                    True(SetBool(s, set, "RelicPending"), label + " relic reward pending");
                    Equal(supported, SetBool(s, set, "RelicImplemented"), label + " public implementation flag");
                    True(!InBag(s, "player-bag", rarity, relic), label + " drawn relic leaves the player bag");
                    True(!InBag(s, "shared-bag", rarity, relic), label + " drawn relic leaves the shared bag");
                    removed.Add(relic);
                    Equal(supported, Claiming(s), label + " only implemented relics are claimable");
                    if (supported)
                    {
                        var owned = Members(s, "Relics").Count;
                        Submit(s, "ClaimCombatRelicReward");
                        Equal(owned + 1, Members(s, "Relics").Count, label + " claim obtains exactly one relic");
                        True(Members(s, "Relics").Any(r => TextField(s, r, "Relic", "DefinitionId") == relic), label + " claimed definition");
                        True(!Claiming(s), label + " pending clears after the claim");
                    }
                    Submit(s, "ResetActOneRewards");
                    True(!SetBool(s, set, "RelicPending"), label + " skipping clears the relic offer");
                    if (!supported)
                        True(Members(s, "Relics").All(r => TextField(s, r, "Relic", "DefinitionId") != relic), label + " unsupported relic is never granted");
                }
                var state = s.State.StateHash(); var draws = s.Random.TotalDraws;
                Throws<EngineFaultException>(() => Submit(s, "OfferActOneEliteRewardOne"));
                True(s.Fault!.Message.Contains("already generated", StringComparison.Ordinal), "elite visit settles once");
                Equal(state, s.State.StateHash(), "a settled elite visit cannot mutate state");
                Equal(draws, s.Random.TotalDraws, "a settled elite visit cannot draw again");
            }
        });
        Case("REWARD boss: source 100 gold, all-rare cards, no relic and every visit settles once", () =>
        {
            foreach (var vector in data.RootElement.GetProperty("boss_vectors").EnumerateArray())
            {
                var seed = vector.GetProperty("seed").GetString()!;
                var s = Prepared(seed);
                var offers = vector.GetProperty("offers").EnumerateArray().ToArray();
                for (var visit = 0; visit < offers.Length; visit++)
                {
                    var label = "boss seed " + seed + " visit " + (visit + 1);
                    var start = s.Debug!.Records.Count;
                    Submit(s, visit == 0 ? "OfferActOneBossRewardOne" : "OfferActOneBossRewardTwo");
                    CompareOffer(s, offers[visit], label);
                    CompareCalls(s, start, offers[visit], label);
                    Equal(offers[visit].GetProperty("counter").GetInt64(), s.Random.DrawCount(RewardStream), label + " complete shared cursor");
                    var set = Current(s);
                    Equal("none", TextField(s, set, "CombatRewardSet", "RelicId"), label + " source reward set has no boss relic");
                    True(!SetBool(s, set, "RelicPending"), label + " no relic offer");
                    True(!Claiming(s), label + " nowhere to claim a boss relic");
                    Submit(s, "ResetActOneRewards");
                }
                var state = s.State.StateHash(); var draws = s.Random.TotalDraws;
                Throws<EngineFaultException>(() => Submit(s, "OfferActOneBossRewardOne"));
                True(s.Fault!.Message.Contains("already generated", StringComparison.Ordinal), "boss visit settles once");
                Equal(state, s.State.StateHash(), "a settled boss visit cannot mutate state");
                Equal(draws, s.Random.TotalDraws, "a settled boss visit cannot draw again");
            }
        });
        return new() { ["base-spire:normal-combat-rewards"] = witnesses.ToArray() };
    }
}
