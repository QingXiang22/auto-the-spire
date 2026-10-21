using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCardPlayChecks(DslPackage package, Action<string, Action> check)
    {
        var witness = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var shared = new List<string>();
        void Case(string name, Action body) { check(name, body); shared.Add(name); }
        void Own(string id, string name, Action body)
        {
            check(name, body);
            if (!witness.TryGetValue(id, out var list)) witness[id] = list = [];
            list.Add(name);
        }

        Case("CP01: playing a card spends energy, leaves the hand and only then reaches the discard pile", () =>
        {
            var s = Start(package); Submit(s, "Strike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Equal(1, Members(s, "Hand").Count, "granted card is in hand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(3L, Energy(s), "turn start energy");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(2L, Energy(s), "card cost is spent");
            Equal(34L, EnemyHp(s), "attack damage reaches the target");
            Equal(0, Members(s, "Hand").Count, "card left the hand"); Equal(0, Members(s, "InPlay").Count, "card left the resolving pile");
            Equal(4, Members(s, "DiscardPile").Count, "played card joined the three development cards in the discard pile");
        });
        Case("CP02: a card effect observes the card while it is still resolving", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("play-order.spire", """
                procedure CheckResolving { parameters = { owner = Ref(Player) card = Ref(Card) target = Ref(Enemy) } body = {
                  if = { condition = less(collectionIndex(InPlay, card), 0) then = { fail = { message = "card already left the resolving pile" } } }
                } }
                subscription CheckResolving { event = CardPlayedOnTarget reaction = CheckResolving }
                """);
            var s = Start(copy.Load()); Submit(s, "Strike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(34L, EnemyHp(s), "ordered effect completes");
        });
        Case("CP03: illegal plays disappear from the candidates and are rejected by the procedure", () =>
        {
            var s = Start(package); Submit(s, "AddDevelopmentTarget");
            True(!Legal(s, "PlayAttackFromHand"), "no attack candidate without a card in hand");
            Submit(s, "Strike.AddToHand");
            True(!Legal(s, "PlayAttackFromHand"), "no attack candidate outside combat");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            True(Legal(s, "PlayAttackFromHand"), "attack candidate inside a started turn");
            Set(s, Player(s), "Player", "Energy", Value.Of(0L));
            True(!Legal(s, "PlayAttackFromHand"), "no candidate without enough energy");
            using var copy = new Copy(package.Root);
            copy.Include("play-probes.spire", """
                action ForceAttack { role = player body = { call = { procedure = PlayAttackCard args = { owner = first(type(Player)) card = first(collection(Hand)) } } } }
                """);
            var forced = Start(copy.Load()); Submit(forced, "Strike.AddToHand"); Submit(forced, "BeginDevelopmentCombat"); Submit(forced, "BeginDevelopmentTurn");
            Set(forced, Player(forced), "Player", "Energy", Value.Of(0L));
            Throws<EngineFaultException>(() => forced.Submit(forced.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ForceAttack"), forced.Participant(0), []));
            Equal(SessionStatus.Faulted, forced.Status, "procedure guard rejects the play before any state change");
            True(forced.Fault!.Message.Contains("Not enough energy", StringComparison.Ordinal), "specific energy guard failure");
            Equal(1, Members(forced, "Hand").Count, "rejected play keeps the card in hand"); Equal(3, Members(forced, "DiscardPile").Count, "rejected play discards nothing beyond the development setup");
        });
        Case("CP04: playing one card leaves the rest of the hand untouched", () =>
        {
            var s = Start(package); Submit(s, "Strike.AddToHand"); Submit(s, "Defend.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var defend = Hand(s).Single(card => CardDefinition(s, card) == "base-spire:defend-ironclad");
            Submit(s, "PlayCardFromHand", defend);
            Equal(1, Members(s, "Hand").Count, "the other card stays in hand");
            Equal("base-spire:strike-ironclad", CardDefinition(s, Hand(s).Single()), "the untouched card is the strike");
            Equal(5L, Block(s), "skill block applied"); Equal(40L, EnemyHp(s), "no attack happened");
        });
        Case("CP05: the card catalog keeps its source denominator and only the migrated definitions", () =>
        {
            using var rows = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(package.Root, "cards/catalog.json")));
            var cards = rows.RootElement.GetProperty("entries"); Equal(578, SourceCatalog.Entries("cards").Length, "independent frozen card source denominator");
            var migrated = cards.EnumerateArray().Where(row => row.GetProperty("definition").ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(row => row.GetProperty("id").GetString()).Order(StringComparer.Ordinal);
            var expected = IroncladSinglePlayerNames.Split(' ').Select(IroncladId).Concat(["base-spire:giant-rock", "base-spire:shockwave", "base-spire:slimed", "base-spire:whistle"]).Order(StringComparer.Ordinal);
            True(migrated.SequenceEqual(expected), "actual complete singleplayer Ironclad roster plus the token, colorless, status and Whistle consumers");
        });
        Case("CP09: targeted and untargeted cards are offered by different play actions", () =>
        {
            var s = Start(package); Submit(s, "Strike.AddToHand"); Submit(s, "Defend.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var strike = Hand(s).Single(card => CardDefinition(s, card) == "base-spire:strike-ironclad");
            var defend = Hand(s).Single(card => CardDefinition(s, card) == "base-spire:defend-ironclad");
            LegalFor(s, "PlayAttackFromHand", strike); LegalFor(s, "PlayCardFromHand", defend);
            True(!LegalFor(s, "PlayAttackFromHand", defend), "a self-target card is not a legal attack candidate");
            True(!LegalFor(s, "PlayCardFromHand", strike), "a targeted card is not a legal untargeted play candidate");
        });
        Case("CP10: every hit of a multi-hit attack absorbs block separately", () =>
        {
            var s = Start(package); Submit(s, "TwinStrike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, FirstTarget(s), "Enemy", "Block", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(0L, EnemyBlock(s), "first hit consumes the block"); Equal(33L, EnemyHp(s), "5 + 5 damage with 3 absorbed");
            Submit(s, "TwinStrike.AddToHand"); Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(23L, EnemyHp(s), "unblocked twin strike deals both hits");
        });
        Case("CP11: a draw effect resolves before the played card reaches the discard pile", () =>
        {
            var s = Start(package); Submit(s, "PommelStrike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(1, Members(s, "Hand").Count, "the draw effect drew one card");
            Equal(2, Members(s, "DrawPile").Count, "the three development cards were reshuffled and one was drawn");
            Equal(1, Members(s, "DiscardPile").Count, "the played card reached the discard pile after its effect");
        });

        Own("base-spire:strike-ironclad", "CP06 Strike: cost 1 for 6 damage and 9 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Strike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(34L, EnemyHp(s), "normal Strike deals 6");
            Submit(s, "Strike.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(25L, EnemyHp(s), "upgraded Strike deals 9");
            True(!Legal(s, "UpgradeCardInHand"), "an upgraded card leaves the upgrade candidates");
        });
        Own("base-spire:defend-ironclad", "CP07 Defend: cost 1 for 5 block and 8 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Defend.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(5L, Block(s), "normal Defend gains 5 block");
            Submit(s, "Defend.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(13L, Block(s), "upgraded Defend gains 8 more");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(0L, Block(s), "turn two clears the card block like any other block");
        });

        Case("CP19: an exhausting card reaches the exhaust pile instead of the discard pile", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("exhaust-order.spire", """
                procedure CheckExhausted { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = less(collectionIndex(ExhaustPile, card), 0) then = { fail = { message = "exhaust event fired before the card reached the exhaust pile" } } }
                } }
                subscription CheckExhausted { event = CardExhausted reaction = CheckExhausted }
                """);
            var s = Start(copy.Load()); Submit(s, "Impervious.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(30L, Block(s), "exhausting skill still resolves"); Equal(1, Members(s, "ExhaustPile").Count, "the card exhausted");
            Equal(3, Members(s, "DiscardPile").Count, "the discard pile keeps only the development cards");
            var exhausted = Members(s, "ExhaustPile").Single();
            Equal(2L, CardCost(s, exhausted), "the exhausted instance keeps the played card's cost"); Equal("base-spire:impervious", CardDefinition(s, exhausted), "identity survives exhaustion");
        });
        Case("CP20: losing HP ignores block while gaining energy does not clamp to MaxEnergy", () =>
        {
            var s = Start(package); Submit(s, "Bloodletting.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Block", Value.Of(5L));
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(5L, Block(s), "block is untouched by HP loss"); Equal(77L, Hp(s), "three HP lost through the block");
            Equal(5L, Energy(s), "zero cost then two energy gained above MaxEnergy");
        });
        Own("base-spire:twin-strike", "CP14 Twin Strike: cost 1 for two hits of 5 and 7 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "TwinStrike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(30L, EnemyHp(s), "normal Twin Strike deals 5 twice");
            Submit(s, "TwinStrike.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(16L, EnemyHp(s), "upgraded Twin Strike deals 7 twice");
        });
        Own("base-spire:pommel-strike", "CP15 Pommel Strike: cost 1 for 9 damage plus one card, 10 and two when upgraded", () =>
        {
            var s = Start(package); Submit(s, "PommelStrike.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(31L, EnemyHp(s), "normal Pommel Strike deals 9");
            Equal(1, Members(s, "Hand").Count, "normal Pommel Strike draws one");
            Submit(s, "PommelStrike.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:pommel-strike"));
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            var upgraded = Hand(s).Single(card => CardUpgraded(s, card));
            var before = Members(s, "Hand").Count;
            Submit(s, "PlayAttackFromHand", upgraded);
            Equal(21L, EnemyHp(s), "upgraded Pommel Strike deals 10");
            Equal(before - 1 + 2, Members(s, "Hand").Count, "upgraded Pommel Strike draws two");
        });
        Own("base-spire:shrug-it-off", "CP16 Shrug It Off: cost 1 for 8 block plus one card and 11 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "ShrugItOff.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(8L, Block(s), "normal Shrug It Off gains 8 block"); Equal(1, Members(s, "Hand").Count, "and draws one");
            Submit(s, "ShrugItOff.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:shrug-it-off"));
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            var before = Members(s, "Hand").Count;
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(19L, Block(s), "upgraded Shrug It Off gains 11 more block"); Equal(before, Members(s, "Hand").Count, "draw stays at one");
        });
        Own("base-spire:iron-wave", "CP13 Iron Wave: cost 1 for 5 block then 5 damage, 7 and 7 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "IronWave.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(5L, Block(s), "block before damage"); Equal(35L, EnemyHp(s), "damage after block"); Equal(2L, Energy(s), "one energy spent");
            Submit(s, "IronWave.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(12L, Block(s), "upgraded Iron Wave gains 7 more block"); Equal(28L, EnemyHp(s), "and deals 7 more damage");
        });
        Own("base-spire:anger", "CP12 Anger: cost 0 for 6 damage and a copy in the discard pile", () =>
        {
            var s = Start(package); Submit(s, "Anger.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(3L, Energy(s), "zero cost spends nothing"); Equal(34L, EnemyHp(s), "Anger deals 6");
            Equal(5, Members(s, "DiscardPile").Count, "three development cards plus the played card plus its copy");
            var copies = Members(s, "DiscardPile").Count(card => CardDefinition(s, card) == "base-spire:anger");
            Equal(2, copies, "the played Anger and one clone are in the discard pile");
            Submit(s, "Anger.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single(card => CardUpgraded(s, card)));
            Equal(4, Members(s, "DiscardPile").Count(card => CardDefinition(s, card) == "base-spire:anger"), "the second play adds the played card and a clone that keeps the upgraded identity");
        });
        Own("base-spire:body-slam", "CP17 Body Slam: damage equals block and the upgrade lowers the cost", () =>
        {
            var s = Start(package); Submit(s, "BodySlam.AddToHand"); Submit(s, "Defend.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single(card => CardDefinition(s, card) == "base-spire:defend-ironclad"));
            Equal(5L, Block(s), "defend block for the slam");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(35L, EnemyHp(s), "Body Slam deals damage equal to the current block"); Equal(1L, Energy(s), "two cards cost one energy each");
            Submit(s, "BodySlam.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            var slam = Hand(s).Single();
            Equal(0L, CardCost(s, slam), "the upgrade lowers Body Slam to zero cost");
            Set(s, Player(s), "Player", "Energy", Value.Of(1L));
            True(LegalFor(s, "PlayAttackFromHand", slam), "the upgraded slam is still a legal attack candidate");
        });

        Own("base-spire:impervious", "CP21 Impervious: cost 2 for 30 block, 40 when upgraded, and it exhausts", () =>
        {
            var s = Start(package); Submit(s, "Impervious.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(30L, Block(s), "normal Impervious gains 30 block"); Equal(1L, Energy(s), "two energy spent");
            Submit(s, "Impervious.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(2L));
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(70L, Block(s), "upgraded Impervious gains 40 more block");
            Equal(2, Members(s, "ExhaustPile").Count, "both copies exhausted"); Equal(3, Members(s, "DiscardPile").Count, "nothing reached the discard pile");
        });
        Own("base-spire:offering", "CP22 Offering: lose 6 HP, gain 2 energy, draw 3 and exhaust; 5 when upgraded", () =>
        {
            var normal = Start(package); Submit(normal, "Offering.AddToHand");
            for (var index = 0; index < 5; index++) Submit(normal, "Strike.AddToHand");
            Submit(normal, "BeginDevelopmentCombat"); Submit(normal, "BeginDevelopmentTurn"); Submit(normal, "EndDevelopmentTurn"); Submit(normal, "BeginDevelopmentTurn");
            Submit(normal, "Offering.AddToHand");
            Submit(normal, "PlayCardFromHand", Hand(normal).Single(card => CardDefinition(normal, card) == "base-spire:offering"));
            Equal(74L, Hp(normal), "six HP lost"); Equal(5L, Energy(normal), "two energy gained"); Equal(3, Members(normal, "Hand").Count, "normal Offering draws three");
            Equal(1, Members(normal, "ExhaustPile").Count, "Offering exhausts");
            var upgraded = Start(package); Submit(upgraded, "Offering.AddToHand");
            for (var index = 0; index < 5; index++) Submit(upgraded, "Strike.AddToHand");
            Submit(upgraded, "BeginDevelopmentCombat"); Submit(upgraded, "BeginDevelopmentTurn"); Submit(upgraded, "EndDevelopmentTurn"); Submit(upgraded, "BeginDevelopmentTurn");
            Submit(upgraded, "Offering.AddToHand"); Submit(upgraded, "UpgradeCardInHand", Hand(upgraded).Single());
            Submit(upgraded, "PlayCardFromHand", Hand(upgraded).Single(card => CardDefinition(upgraded, card) == "base-spire:offering"));
            Equal(5, Members(upgraded, "Hand").Count, "upgraded Offering draws five");
        });
        Own("base-spire:bloodletting", "CP23 Bloodletting: lose 3 HP for 2 energy, 3 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Bloodletting.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(77L, Hp(s), "normal Bloodletting loses three HP"); Equal(5L, Energy(s), "and gains two energy");
            Submit(s, "Bloodletting.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(74L, Hp(s), "the upgrade does not change the HP loss"); Equal(6L, Energy(s), "the upgrade adds one energy");
            Equal(2, Members(s, "DiscardPile").Count(card => CardDefinition(s, card) == "base-spire:bloodletting"), "no exhaust keyword means both copies stay in the discard pile");
        });
        Own("base-spire:bludgeon", "CP24 Bludgeon: cost 3 for 32 damage and 42 when upgraded", () =>
        {
            var s = Start(package); Submit(s, "Bludgeon.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(8L, EnemyHp(s), "normal Bludgeon deals 32"); Equal(0L, Energy(s), "three energy spent");
            Submit(s, "Bludgeon.AddToHand"); Submit(s, "UpgradeCardInHand", Hand(s).Single());
            Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(0L, EnemyHp(s), "upgraded Bludgeon deals 42 and HP floors at zero");
        });

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }

    private static List<InstanceId> Hand(Session s) => Members(s, "Hand").ToList();
    private static bool LegalFor(Session s, string action, InstanceId card) =>
        s.LegalActions(s.Participant(0)).Any(a => a.Definition.QualifiedId == "base-spire:" + action && a.Candidates.Contains(card));
    private static string CardDefinition(Session s, InstanceId card) => s.State.GetField(card, CardField(s, "DefinitionId")).AsString();
    private static long CardCost(Session s, InstanceId card) => s.State.GetField(card, CardField(s, "Cost")).AsInt();
    private static bool CardUpgraded(Session s, InstanceId card) => s.State.GetField(card, CardField(s, "Upgraded")).AsBool();
    private static int CardField(Session s, string name) => s.RuleSet.TypeByQualifiedId("base-spire:Card").FieldIndex(name);
}
