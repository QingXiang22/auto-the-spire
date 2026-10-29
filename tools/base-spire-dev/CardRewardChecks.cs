using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private const string CardRarityStream = "base-spire:player:rewards";
    private const string CardRewardStream = "base-spire:player:rewards";

    public static int RunCardRewardsOnly(DslPackage package)
    {
        var count = 0;
        RunCardRewardChecks(package, (name, body) => { body(); count++; Console.WriteLine("ok   " + name); });
        RunNormalRewardChecks(package, (name, body) => { body(); count++; Console.WriteLine("ok   " + name); });
        RunActOneRewardChecks(package, (name, body) => { body(); count++; Console.WriteLine("ok   " + name); });
        Console.WriteLine($"All {count} focused card-reward checks passed; full check is required for package evidence.");
        return 0;
    }
    private static Dictionary<string, string[]> RunCardRewardChecks(DslPackage package, Action<string, Action> check)
    {
        var cases = new List<string>();
        void Case(string name, Action body) { check(name, body); cases.Add(name); }
        Session Offered(DslPackage? input = null, ulong seed = 20261018)
        {
            var s = (input ?? package).CreateSession(seed, ["player"]); s.Start(); Healthy(s);
            Submit(s, "Ironclad.Initialize"); Submit(s, "TwigSlimeS.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            while (Active(s))
            {
                var attack = Hand(s).FirstOrDefault(c => CardDefinition(s, c) is "base-spire:strike-ironclad" or "base-spire:bash");
                True(attack != default, "ten-card starter hand has a playable attack");
                Submit(s, "PlayAttackFromHand", attack);
            }
            Equal("victory", Outcome(s), "actual seven-HP enemy defeated using starter cards");
            return s;
        }
        long Cursor(Session s) => s.Random.DrawCount(CardRewardStream);

        Case("CR01: reward eligibility follows all 80 source cards and their rarity order, including Feed and NotYet", () =>
        {
            using var stream = typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.ironclad-source-metadata.json")!;
            using var source = JsonDocument.Parse(stream);
            var expected = source.RootElement.GetProperty("entries").EnumerateArray()
                .Where(r => !r.GetProperty("multiplayer_only").GetBoolean() && r.GetProperty("name").GetString() != "GiantRock")
                .Where(r => r.GetProperty("rarity").GetString() is "common" or "uncommon" or "rare")
                .OrderBy(r => Array.IndexOf(IroncladSinglePlayerNames.Split(' '), r.GetProperty("name").GetString())).ToArray();
            Equal(80, expected.Length, "85 minus three Basic and two Ancient");
            var s = Start(package); var blueprint = s.RuleSet.TypeByQualifiedId("base-spire:CardBlueprint");
            var actual = Members(s, "CardLibrary").Where(b => s.State.GetField(b, blueprint.FieldIndex("InIroncladPool")).AsBool())
                .Where(b => s.State.GetField(b, blueprint.FieldIndex("Rarity")).AsString() is "common" or "uncommon" or "rare").ToArray();
            True(actual.Select(b => s.State.GetField(b, blueprint.FieldIndex("DefinitionId")).AsString()).SequenceEqual(expected.Select(r => r.GetProperty("id").GetString())), "source eligibility and order");
            for (var i = 0; i < actual.Length; i++) Equal(expected[i].GetProperty("rarity").GetString(), s.State.GetField(actual[i], blueprint.FieldIndex("Rarity")).AsString(), "independent source rarity");
            Equal(20, expected.Count(r => r.GetProperty("rarity").GetString() == "common"), "source common count");
            Equal(35, expected.Count(r => r.GetProperty("rarity").GetString() == "uncommon"), "source uncommon count");
            Equal(25, expected.Count(r => r.GetProperty("rarity").GetString() == "rare"), "source rare count");
            True(expected.Any(r => r.GetProperty("name").GetString() == "Feed") && expected.Any(r => r.GetProperty("name").GetString() == "NotYet"), "combat-generation restriction is not a reward exclusion");
        });
        Case("CR02: rarity thresholds preserve negative offset, strict boundaries and upper offset", () =>
        {
            using var copy = new Copy(package.Root);
            var vectors = new (long Roll, long Offset, string Rarity)[] {
                (0, 3175926989, "uncommon"), (1051931442, 3175926989, "uncommon"), (1051931443, 3175926989, "common"),
                (1022739086, 0, "rare"), (1022739087, 0, "uncommon"), (1053609164, 0, "uncommon"), (1053609165, 0, "common"),
                (1054615797, 1053609165, "rare"), (1054615798, 1053609165, "uncommon"), (1061997772, 1053609165, "uncommon"), (1061997773, 1053609165, "common") };
            var assertions = string.Join(Environment.NewLine, vectors.Select(v => $"if = {{ condition = not(equal(pureCall(ClassifyCombatRewardRarity, {v.Roll}, {v.Offset}, \"monster\"), \"{v.Rarity}\")) then = {{ fail = {{ message = \"rarity threshold mismatch\" }} }} }}"));
            copy.Include("rarity-boundaries.spire", "action CheckThresholds { role = player body = { " + assertions + " } }");
            Submit(Start(copy.Load()), "CheckThresholds");
        });
        Case("CR03: real victory freezes three distinct options without acquiring cards or firing combat-entry hooks", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("no-combat-reward.spire", """
                procedure InspectRewardEntry { parameters = { owner = Ref(Player) card = Ref(Card) clone = Bool } body = {
                  if = { condition = equal(field(owner, Player.Room), "rewards") then = { fail = { message = "reward incorrectly entered combat" } } }
                } }
                subscription InspectRewardEntry { event = CardEnteredCombat reaction = InspectRewardEntry }
                """);
            var s = Offered(copy.Load()); var options = Members(s, "CardRewardOptions");
            Equal(3, options.Count, "three actual options"); Equal(3, options.Select(c => CardDefinition(s, c)).Distinct().Count(), "no duplicate definitions");
            Equal(10, Members(s, "RunDeck").Count, "offers are not permanent cards yet");
            True(options.All(c => !Hand(s).Contains(c) && !Members(s, "DiscardPile").Contains(c)), "offers are not in combat piles");
            Equal(0, Members(s, "CardRewardPool").Count, "temporary source eligibility cleaned up");
            Equal(3164854026L, IntField(s, Player(s), "Player", "CardRarityOffset"), "initial three nonrare rolls advance offset from minus five");
            var before = Cursor(s); s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
            Equal(before, Cursor(s), "observation and candidates do not reroll");
        });
        Case("CR04: reward claim preserves the chosen upgraded identity, acquisition order and next-combat source", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("reward-obtain-order.spire", """
                procedure InspectRewardObtain { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = equal(field(owner, Player.Room), "rewards") then = {
                    if = { condition = or(less(collectionIndex(RunDeck, card), 0), greaterOrEqual(collectionIndex(CardRewardOptions, card), 0)) then = { fail = { message = "acquisition event before permanent transfer" } } }
                  } }
                } }
                subscription InspectRewardObtain { event = CardObtained reaction = InspectRewardObtain }
                action UpgradeRewardProbe { role = player body = { call = { procedure = UpgradeCard args = { card = first(collection(CardRewardOptions)) } } } }
                """);
            var s = Offered(copy.Load()); var options = Members(s, "CardRewardOptions").ToArray(); var chosen = options[0];
            Submit(s, "UpgradeRewardProbe"); var cost = CardCost(s, chosen); var before = Cursor(s);
            Submit(s, "ClaimDevelopmentCardReward", chosen);
            Equal(11, Members(s, "RunDeck").Count, "one reward acquired"); True(Members(s, "RunDeck").Contains(chosen), "same exact instance becomes permanent");
            True(CardUpgraded(s, chosen), "candidate upgrade preserved"); True(options.Skip(1).All(c => !s.State.Exists(c)), "unselected instances destroyed");
            Equal(0, Members(s, "CardRewardOptions").Count, "options cleared"); True(!FieldBool(s, "CardRewardPending"), "pending cleared");
            True(RewardPending(s), "gold remains independently claimable"); Equal("rewards", Room(s), "claim does not leave rewards");
            Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ClaimDevelopmentCardReward"), s.Participant(0), [Value.OfRef(chosen)]));
            Equal(SessionStatus.Running, s.Status, "stale claim rejected without fault"); Equal(before, Cursor(s), "claim and rejection do not reroll");
            Submit(s, "CloseDevelopmentRewards"); Submit(s, "TwigSlimeS.Spawn"); Submit(s, "BeginDevelopmentCombat");
            var combatCopy = Members(s, "DrawPile").Single(c => PermanentSource(s, c) == chosen);
            True(combatCopy != chosen && CardUpgraded(s, combatCopy), "next battle owns separate upgraded copy"); Equal(cost, CardCost(s, combatCopy), "upgraded cost survives");
            Equal(s.State.GetField(chosen, CardField(s, "Rarity")), s.State.GetField(combatCopy, CardField(s, "Rarity")), "rarity survives combat copy");
        });
        Case("CR05: skip and close destroy all offers while gold remains independent", () =>
        {
            foreach (var explicitSkip in new[] { false, true })
            {
                var s = Offered(); var old = Members(s, "CardRewardOptions").ToArray(); var before = Cursor(s);
                if (explicitSkip) { Submit(s, "SkipDevelopmentCardReward"); True(RewardPending(s), "skip leaves gold pending"); Submit(s, "ClaimDevelopmentGoldReward"); }
                Submit(s, "CloseDevelopmentRewards");
                True(old.All(c => !s.State.Exists(c)), "discarded offers destroyed"); Equal(10, Members(s, "RunDeck").Count, "no reward card acquired");
                True(!FieldBool(s, "CardRewardPending"), "card marker cleared"); Equal(before, Cursor(s), "skipping does not consume card randomness");
                RejectRewardAction(s, "SkipDevelopmentCardReward");
                if (!explicitSkip) Equal(99L, Field(s, "Gold"), "closing skips gold too");
            }
        });
        Case("CR06: fixed seeds repeat options and a later victory creates fresh identities", () =>
        {
            foreach (var seed in new ulong[] { 20261018, 777 })
            {
                var one = Offered(seed: seed); var two = Offered(seed: seed);
                True(Members(one, "CardRewardOptions").Select(c => CardDefinition(one, c)).SequenceEqual(Members(two, "CardRewardOptions").Select(c => CardDefinition(two, c))), "same seed reward definitions");
                Equal(Cursor(one), Cursor(two), "same random cursor");
                var old = Members(one, "CardRewardOptions").ToArray(); Submit(one, "CloseDevelopmentRewards");
                Submit(one, "TwigSlimeS.Spawn"); Submit(one, "BeginDevelopmentCombat"); Submit(one, "BeginDevelopmentTurn");
                while (Active(one)) Submit(one, "PlayAttackFromHand", Hand(one).First(c => CardDefinition(one, c) is "base-spire:strike-ironclad" or "base-spire:bash"));
                Equal(3, Members(one, "CardRewardOptions").Count, "new victory offers three"); True(old.All(c => !one.State.Exists(c)), "old options never reused");
            }
        });
        Case("CR07: rarity reset and cap, rarity fallback and zero upgrade boundary follow source rules", () =>
        {
            using var copy = new Copy(package.Root);
            var path = copy.Path("rewards/rules/combat-cards.spire"); var text = File.ReadAllText(path)
                .Replace("sampleFloat32 = { stream = \"base-spire:player:rewards\" into = roll }", "let = { into = roll value = 0 }")
                .Replace("sampleFloat32 = { stream = \"base-spire:player:rewards\" into = upgradeRoll }", "let = { into = upgradeRoll value = 0 }");
            File.WriteAllText(path, text);
            copy.Include("rarity-probe.spire", """
                action RarityProbe { role = player locals = { rarity = String(length = 16) } body = {
                  call = { procedure = RollCombatRewardRarity args = { owner = first(type(Player)) source = "monster" } into = rarity }
                } }
                action FallbackProbe { role = player body = {
                  addToCollection = { collection = CardRewardPool item = first(where(collection(CardLibrary), b, equal(field(b, CardBlueprint.Rarity), "common"))) }
                  if = { condition = not(equal(pureCall(ResolveCombatRewardRarity, "rare"), "common")) then = { fail = { message = "rare fallback did not wrap to common" } } }
                } }
                """);
            var probe = copy.Load(); var s = Start(probe);
            Set(s, Player(s), "Player", "CardRarityOffset", Value.Of(1053609165L)); Submit(s, "RarityProbe");
            Equal(3175926989L, IntField(s, Player(s), "Player", "CardRarityOffset"), "rare resets offset"); Submit(s, "FallbackProbe");
            var upgraded = Offered(probe); True(Members(upgraded, "CardRewardOptions").All(c => CardUpgraded(upgraded, c)), "source <= zero upgrade boundary");
            File.WriteAllText(path, text.Replace("into = roll value = 0", "into = roll value = 1065353216"));
            var cap = Start(copy.Load()); Set(cap, Player(cap), "Player", "CardRarityOffset", Value.Of(1053609165L)); Submit(cap, "RarityProbe");
            Equal(1053609165L, IntField(cap, Player(cap), "Player", "CardRarityOffset"), "nonrare growth capped");
        });
        Case("CR08: invalid choices and direct acquisition bypass do not gain cards, defeat does not roll", () =>
        {
            var s = Offered(); var before = Cursor(s); var permanent = Members(s, "RunDeck").First();
            Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ClaimDevelopmentCardReward"), s.Participant(0), [Value.OfRef(permanent)]));
            Equal(10, Members(s, "RunDeck").Count, "wrong instance did not acquire"); Equal(3, Members(s, "CardRewardOptions").Count, "options preserved"); Equal(before, Cursor(s), "invalid choice did not reroll");
            using var copy = new Copy(package.Root);
            copy.Include("force-card-reward.spire", """
                action ForceCardReward { role = player body = { call = { procedure = ObtainCardReward args = { owner = first(type(Player)) card = first(collection(RunDeck)) } } } }
                """);
            var direct = Start(copy.Load()); Submit(direct, "Ironclad.Initialize"); ForceRejected(direct, "CardReward"); Equal(10, Members(direct, "RunDeck").Count, "procedure guard before acquisition");
            var dead = Start(package); Submit(dead, "Ironclad.Initialize"); Submit(dead, "BeginDevelopmentCombat"); Submit(dead, "BeginDevelopmentTurn");
            Set(dead, Player(dead), "Player", "Hp", Value.Of(2L)); IcPlay(dead, IcAdd(dead, "Bloodletting"));
            Equal("defeat", Outcome(dead), "actual defeat"); Equal(0L, Cursor(dead), "no card randomness on defeat"); Equal(0, Members(dead, "CardRewardOptions").Count, "no defeat offers");
        });
        Case("CR09: missing source definitions fail before any reward draw or offered state", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("force-reward-offer.spire", "action ForceOffer { role = player body = { call = { procedure = WhenCombatEnded args = { owner = first(type(Player)) victory = true } } } }");
            var s = Start(copy.Load()); Submit(s, "Ironclad.Initialize");
            var missing = Members(s, "CardLibrary").First(c => s.State.GetField(c, s.RuleSet.TypeByQualifiedId("base-spire:CardBlueprint").FieldIndex("InIroncladPool")).AsBool());
            s.State.RunAtomic(() => s.State.DestroyInstance(missing));
            ForceRejected(s, "Offer");
            Equal(0L, Cursor(s), "no card randomness before source validation");
            Equal(0L, s.Random.DrawCount(RewardStream), "no gold randomness before source validation");
            True(!RewardPending(s) && !FieldBool(s, "CardRewardPending"), "no partial reward markers");
            Equal(0, Members(s, "CardRewardOptions").Count, "no replacement options");
            Equal(10, Members(s, "RunDeck").Count, "permanent deck untouched");
        });
        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:combat-cards"] = cases.ToArray() };
    }
}
