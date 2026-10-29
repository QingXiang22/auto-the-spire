using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunSourceRandomOnly(DslPackage package)
    {
        var count = 0; RunSourceRandomChecks(package, (name, body) => { body(); count++; Console.WriteLine("ok   " + name); });
        Console.WriteLine($"All {count} source RNG checks passed; vectors are independent recovered-source evidence, not full Run parity.");
        return 0;
    }
    private static void RunSourceRandomChecks(DslPackage package, Action<string, Action> check)
    {
        RunUnknownRoomChecks(package, check);
        RunEventSelectionChecks(package, check);
        using var data = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.source-rng-vectors.json")!);
        foreach (var vector in data.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var seed = vector.GetProperty("seed").GetString()!;
            check("RNG source layout, raw mapping, initial/stable shuffle and rewards: " + JsonSerializer.Serialize(seed), () =>
            {
                using var copy = new Copy(package.Root);
                copy.Include("rng-probes.spire", """
                    entity RngProbe { fields = { Root = { type = Int } Content = { type = Int } } }
                    procedure InspectLayout { locals = { probe = Binding } body = {
                      create = { type = RngProbe scope = frameScope() into = probe fields = { Root = pureCall(SourceRunSeed) Content = pureCall(SourceContentSeed, 2, "SAPPHIRE_SEED", 7) } }
                    } }
                    action InspectLayout { role = player body = { call = { procedure = InspectLayout } } }
                    action FloatProbe { role = player locals = { value = Int } body = {
                      sampleFloat32 = { stream = "base-spire:player:rewards" into = value }
                      set = { target = first(type(RngProbe)) field = RngProbe.Content value = value }
                    } }
                    # Preserve the original component vectors; NormalRewardChecks verifies the full reward set separately.
                    action OfferProbe { role = player body = { call = { procedure = OfferCombatGold args = { owner = first(type(Player)) source = "monster" } } call = { procedure = OfferCombatCards args = { owner = first(type(Player)) source = "monster" } } } }
                    """);
                var p = copy.Load();
                Session New() { var s = p.CreateSession(1, ["player"], seed); s.Start(); Healthy(s); return s; }
                var layout = New();
                Equal(0L, layout.Random.TotalDraws, "layout declarations consume no random words");
                var snapshot = layout.Random.Capture();
                foreach (var pair in vector.GetProperty("streams").EnumerateObject())
                {
                    Equal(pair.Value.GetUInt64(), snapshot.ExplicitSeeds[pair.Name], "exact source stream seed: " + pair.Name);
                    foreach (var expected in vector.GetProperty("int100_first4").GetProperty(pair.Name).EnumerateArray())
                        Equal(expected.GetInt64(), layout.Random.DrawInt(pair.Name, 0, 100), "independent source bounded reads");
                    Equal(4L, layout.Random.DrawCount(pair.Name), "four bounded reads consume four raw words");
                }
                var floats = New(); Submit(floats, "InspectLayout");
                var probe = floats.State.AllInstances(floats.RuleSet.TypeByQualifiedId("base-spire:RngProbe").Id).Single();
                Equal(unchecked((long)vector.GetProperty("run_seed").GetUInt64()), IntField(floats, probe, "RngProbe", "Root"), "UTF8 source run seed");
                Equal(unchecked((long)vector.GetProperty("content_seed_slot2_mixin7").GetUInt64()), IntField(floats, probe, "RngProbe", "Content"), "slot and mixin wrap");
                foreach (var expected in vector.GetProperty("float32_bits_first8").EnumerateArray())
                { Submit(floats, "FloatProbe"); Equal(expected.GetInt64(), IntField(floats, probe, "RngProbe", "Content"), "exact NextFloat binary32 bits"); }
                Equal(8L, floats.Random.DrawCount("base-spire:player:rewards"), "float counter");
                var cards = New(); Submit(cards, "Ironclad.Initialize"); var permanent = Members(cards, "RunDeck").ToArray();
                Submit(cards, "BeginDevelopmentCombat");
                int[] Order(IEnumerable<InstanceId> items) => items.Select(c => Array.IndexOf(permanent, PermanentSource(cards, c))).ToArray();
                True(Order(Members(cards, "DrawPile")).SequenceEqual(vector.GetProperty("initial_order").EnumerateArray().Select(x => x.GetInt32())), "source initial shuffle exact instance order");
                Equal(9L, cards.Random.DrawCount("base-spire:run:shuffle"), "initial shuffle uses n-1 words");
                for (var i = 0; i < 10; i++) Submit(cards, "Draw"); Submit(cards, "DiscardHand"); Submit(cards, "Draw");
                True(Order(Hand(cards).Concat(Members(cards, "DrawPile"))).SequenceEqual(vector.GetProperty("reshuffle_order").EnumerateArray().Select(x => x.GetInt32())), "source pre-sort then same-stream stable reshuffle");
                Equal(vector.GetProperty("shuffle_counter").GetInt64(), cards.Random.DrawCount("base-spire:run:shuffle"), "initial and reshuffle shared cursor");
                var rewards = New(); Submit(rewards, "Ironclad.Initialize");
                foreach (var expected in vector.GetProperty("rewards").EnumerateArray())
                {
                    Submit(rewards, "OfferProbe");
                    Equal(expected.GetProperty("gold").GetInt64(), Field(rewards, "GoldRewardAmount"), "source gold value");
                    var actual = Members(rewards, "CardRewardOptions").ToArray(); var options = expected.GetProperty("cards").EnumerateArray().ToArray();
                    for (var i = 0; i < 3; i++)
                    { Equal(options[i].GetProperty("id").GetString(), CardDefinition(rewards, actual[i]), "source reward identity"); Equal(options[i].GetProperty("upgraded").GetBoolean(), CardUpgraded(rewards, actual[i]), "source upgrade roll"); }
                    Equal(expected.GetProperty("offset_bits").GetInt64(), IntField(rewards, Player(rewards), "Player", "CardRarityOffset"), "source binary32 offset accumulation");
                    Equal(expected.GetProperty("counter").GetInt64(), rewards.Random.DrawCount("base-spire:player:rewards"), "gold, rarity, option and upgrade share source counter");
                    var before = rewards.Random.TotalDraws; rewards.Observe(rewards.Participant(0), "base-spire:PlayerView"); rewards.LegalActions(rewards.Participant(0));
                    Equal(before, rewards.Random.TotalDraws, "observation and action queries do not draw"); Submit(rewards, "CloseDevelopmentRewards");
                }
            });
        }
    }
}
