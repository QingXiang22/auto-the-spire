using System.Text.Json;
using System.Text.RegularExpressions;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunIroncladOnly(DslPackage package)
    {
        var count = 0;
        RunIroncladChecks(package, (name, body) => { body(); count++; Console.WriteLine("ok   " + name); });
        Console.WriteLine($"All {count} focused Ironclad checks passed; run full check for package coverage evidence."); return 0;
    }
    // Frozen source roster, independent of the executable definitions and generated-card candidate list.
    private const string IroncladSinglePlayerNames = "Aggression Anger Armaments AshenStrike Barricade Bash BattleTrance BloodWall Bloodletting Bludgeon BodySlam Brand Break Breakthrough Bully BurningPact Cascade Cinder Colossus Conflagration Corruption CrimsonMantle Cruelty DarkEmbrace DefendIronclad DemonForm Dismantle Dominate DrumOfBattle EvilEye ExpectAFight Feed FeelNoPain FiendFire FightMe FlameBarrier ForgottenRitual Havoc Headbutt Hellraiser Hemokinesis HowlFromBeyond Impervious InfernalBlade Inferno Inflame IronWave Juggernaut Juggling Mangle MoltenFist NotYet Offering OneTwoPunch PactsEnd PerfectedStrike Pillage PommelStrike PrimalForce Pyre Rage Rampage Rupture SecondWind SetupStrike ShrugItOff Spite Stampede Stoke Stomp StoneArmor StrikeIronclad SwordBoomerang Taunt TearAsunder Thrash Thunderclap Tremble TrueGrit TwinStrike Unmovable Unrelenting Uppercut Vicious Whirlwind";

    private static string IroncladId(string name) => "base-spire:" + Regex.Replace(name, "(?<!^)(?=[A-Z])", "-").ToLowerInvariant();
    private static long IntField(Session s, InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field)).AsInt();
    private static long IcPower(Session s, string kind) => Members(s, "Powers").Where(p => s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("Kind")).AsString() == kind).Sum(p => IntField(s, p, "Power", "Stacks"));
    private static InstanceId IcAdd(Session s, string name, bool upgraded = false)
    {
        Submit(s, (name == "StrikeIronclad" ? "Strike" : name == "DefendIronclad" ? "Defend" : name) + ".AddToHand");
        var card = Hand(s).Last(c => CardDefinition(s, c) == IroncladId(name));
        if (upgraded) Submit(s, "UpgradeCardInHand", card);
        return card;
    }
    private static void IcMove(Session s, InstanceId card, string from, string to)
    {
        var source = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + from).Id;
        var destination = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + to).Id;
        s.State.RunAtomic(() => { s.State.RemoveFromCollection(source, card); s.State.AddToCollection(destination, card); });
    }
    private static void IcPlay(Session s, InstanceId card)
    {
        Submit(s, s.State.GetField(card, CardField(s, "Targeted")).AsBool() ? "PlayAttackFromHand" : "PlayCardFromHand", card);
    }
    private static void IcNextTurn(Session s) { Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); }

    private static Dictionary<string, string[]> RunIroncladChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Session Combat()
        {
            var s = Start(package); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            foreach (var card in Members(s, "DiscardPile").ToArray()) s.State.RunAtomic(() => s.State.DestroyInstance(card));
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L)); Set(s, FirstTarget(s), "Enemy", "MaxHp", Value.Of(1000L));
            Set(s, Player(s), "Player", "Energy", Value.Of(30L)); return s;
        }
        void Own(string name, Action<Session, InstanceId, bool> body)
        {
            foreach (var up in new[] { false, true })
            {
                var label = "IC " + name + (up ? "+: " : ": ") + "source behavior";
                check(label, () => { var s = Combat(); var card = IcAdd(s, name, up); body(s, card, up); });
                witnesses[IroncladId(name)] = witnesses.GetValueOrDefault(IroncladId(name), []).Append(label).ToArray();
            }
        }
        check("IC roster: 87 source rows, 85 singleplayer implementations and complete generation pool", () =>
        {
            using var catalog = JsonDocument.Parse(File.ReadAllText(Path.Combine(package.Root, "cards/catalog.json")));
            var source = catalog.RootElement.GetProperty("entries").EnumerateArray().Where(r => r.GetProperty("pools").EnumerateArray().Any(p => p.GetString() == "IroncladCardPool")).ToArray();
            Equal(87, source.Length, "frozen source pool denominator");
            var expected = IroncladSinglePlayerNames.Split(' ').Select(IroncladId).ToArray();
            Equal(85, expected.Length, "independent singleplayer roster");
            True(source.Where(r => r.GetProperty("definition").ValueKind == JsonValueKind.String).Select(r => r.GetProperty("id").GetString()).Order().SequenceEqual(expected.Order()), "every applicable card has a definition, both MP exclusions retained");
            var s = Start(package);
            var ids = Members(s, "CardLibrary").Where(b => s.State.GetField(b, s.RuleSet.TypeByQualifiedId("base-spire:CardBlueprint").FieldIndex("InIroncladPool")).AsBool()).Select(b => s.State.GetField(b, s.RuleSet.TypeByQualifiedId("base-spire:CardBlueprint").FieldIndex("DefinitionId")).AsString()).ToArray();
            True(ids.SequenceEqual(expected), "generation source candidates retain the entire source pool order");
            var eligible = Members(s, "CardLibrary").Where(b => s.State.GetField(b, s.RuleSet.TypeByQualifiedId("base-spire:CardBlueprint").FieldIndex("Generatable")).AsBool()).ToArray();
            Equal(78, eligible.Length, "excludes three Basic, two Ancient and Feed/NotYet from 85");
        });
        check("IC frozen source metadata: every applicable card's type, tags, X cost, exhaustion and upgrades", () =>
        {
            using var stream = typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.ironclad-source-metadata.json")!;
            using var source = JsonDocument.Parse(stream);
            var rows = source.RootElement.GetProperty("entries"); Equal(88, rows.GetArrayLength(), "87 source-pool cards plus GiantRock");
            foreach (var row in rows.EnumerateArray())
            {
                if (row.GetProperty("multiplayer_only").GetBoolean()) continue;
                var s = Combat(); var c = IcAdd(s, row.GetProperty("name").GetString()!);
                Equal(row.GetProperty("cost").GetInt64(), CardCost(s, c), "source base cost");
                Equal(row.GetProperty("kind").GetString(), s.State.GetField(c, CardField(s, "Kind")).AsString(), "source type");
                foreach (var (sourceName, field) in new[] { ("targeted", "Targeted"), ("strike_tag", "StrikeTag"), ("is_x", "IsX"), ("exhausts", "Exhausts") })
                    Equal(row.GetProperty(sourceName).GetBoolean(), s.State.GetField(c, CardField(s, field)).AsBool(), row.GetProperty("name").GetString() + ": " + field);
                Submit(s, "UpgradeCardInHand", c);
                Equal(row.GetProperty("upgraded_cost").GetInt64(), CardCost(s, c), "source upgraded cost");
                Equal(row.GetProperty("upgraded_innate").GetBoolean(), s.State.GetField(c, CardField(s, "Innate")).AsBool(), "source Innate upgrade");
            }
        });

        Own("Armaments", (s, c, up) => { var a = IcAdd(s, "StrikeIronclad"); var b = IcAdd(s, "DefendIronclad"); IcPlay(s, c); if (!up) { Equal(SessionStatus.AwaitingDecision, s.Status, "mandatory hand choice"); s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, [b])); Healthy(s); } Equal(5L, Block(s), "block precedes selection"); True(CardUpgraded(s, b) && CardUpgraded(s, a) == up, "selected versus entire hand"); });
        Own("HowlFromBeyond", (s, c, up) => { var brand = IcAdd(s, "Brand"); IcPlay(s, brand); True(Members(s, "ExhaustPile").Contains(c), "another effect exhausted Howl"); Submit(s, "EndDevelopmentTurn"); Equal(up ? 978L : 983L, EnemyHp(s), "end-turn auto damage includes Brand strength"); True(Members(s, "DiscardPile").Contains(c), "auto result is discard, not repeat exhaust"); Equal(0, Members(s, "ExhaustPile").Count, "left exhaust pile"); });
        Own("AshenStrike", (s, c, up) => { var a = IcAdd(s, "DefendIronclad"); IcMove(s, a, "Hand", "ExhaustPile"); IcPlay(s, c); Equal(up ? 990L : 991L, EnemyHp(s), "6 plus exhaust count times upgraded factor"); });
        Own("Bully", (s, c, up) => { IcPlay(s, IcAdd(s, "Tremble")); IcPlay(s, c); Equal(up ? 981L : 985L, EnemyHp(s), "calculated damage uses all three Vulnerable stacks, then 1.5 multiplier"); });
        Own("Brand", (s, c, up) => { var a = IcAdd(s, "StrikeIronclad"); IcPlay(s, c); Equal(79L, Hp(s), "HP loss"); True(Members(s, "ExhaustPile").Contains(a), "selected card exhausted"); Equal(up ? 2L : 1L, IcPower(s, "damage-additive"), "strength after exhaustion"); True(Members(s, "DiscardPile").Contains(c), "Brand itself is not exhausted"); });
        Own("BurningPact", (s, c, up) => { var a = IcAdd(s, "StrikeIronclad"); for (var i = 0; i < 4; i++) IcMove(s, IcAdd(s, "DefendIronclad"), "Hand", "DrawPile"); IcPlay(s, c); True(Members(s, "ExhaustPile").Contains(a), "exhausted selected card"); Equal(up ? 3 : 2, Hand(s).Count, "draw after exhaustion"); });
        Own("BattleTrance", (s, c, up) => { for (var i = 0; i < 5; i++) IcMove(s, IcAdd(s, "DefendIronclad"), "Hand", "DrawPile"); IcPlay(s, c); Equal(up ? 4 : 3, Hand(s).Count, "draw before NoDraw"); Submit(s, "Draw"); Equal(up ? 4 : 3, Hand(s).Count, "later draws blocked"); IcNextTurn(s); Submit(s, "Draw"); Equal(1, Hand(s).Count, "NoDraw expired"); });
        Own("Cinder", (s, c, up) => { var a = IcAdd(s, "DefendIronclad"); IcPlay(s, c); Equal(up ? 976L : 982L, EnemyHp(s), "damage before exhaust"); True(Members(s, "ExhaustPile").Contains(a), "one random hand card exhausted"); });
        Own("Dominate", (s, c, up) => { IcPlay(s, IcAdd(s, "Tremble")); IcPlay(s, c); Equal(up ? 5L : 4L, IcPower(s, "damage-additive"), "strength uses total Vulnerable after application"); True(Members(s, "ExhaustPile").Contains(c), "exhaust result"); });
        Own("DrumOfBattle", (s, c, up) => { for (var i = 0; i < 2; i++) IcMove(s, IcAdd(s, "DefendIronclad"), "Hand", "DrawPile"); IcPlay(s, c); Equal(2, Hand(s).Count, "draw two"); IcMove(s, c, "DiscardPile", "Hand"); foreach (var a in Hand(s).Where(a => a != c).ToArray()) IcMove(s, a, "Hand", "DiscardPile"); var brand = IcAdd(s, "Brand"); var energy = Energy(s); IcPlay(s, brand); Equal(energy + (up ? 3 : 2), Energy(s), "Drum's own exhaust reaction"); });
        Own("ExpectAFight", (s, c, up) => { IcAdd(s, "StrikeIronclad"); IcAdd(s, "Anger"); var blood = IcAdd(s, "Bloodletting"); IcPlay(s, c); Equal(up ? 31L : 30L, Energy(s), "gain remaining attack count after payment"); IcPlay(s, blood); Equal(up ? 31L : 30L, Energy(s), "NoEnergyGain blocks later gain"); IcNextTurn(s); IcPlay(s, IcAdd(s, "Bloodletting")); Equal(5L, Energy(s), "gain restored next turn"); });
        Own("Feed", (s, c, up) => { Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(up ? 12L : 10L)); IcPlay(s, c); Equal(up ? 84L : 83L, IntField(s, Player(s), "Player", "MaxHp"), "fatal max HP gain before settlement"); Equal(up ? 84L : 83L, Hp(s), "also heals"); Equal("victory", Outcome(s), "final target ends combat after Feed"); True(Members(s, "ExhaustPile").Contains(c), "Feed exhausted before victory"); });
        Own("FiendFire", (s, c, up) => { IcAdd(s, "StrikeIronclad"); IcAdd(s, "DefendIronclad"); IcPlay(s, c); Equal(up ? 980L : 986L, EnemyHp(s), "two attacks for original hand snapshot"); Equal(3, Members(s, "ExhaustPile").Count, "two hand cards plus Fiend Fire"); });
        Own("FightMe", (s, c, up) => { IcPlay(s, c); Equal(up ? 988L : 990L, EnemyHp(s), "two hits before buffs"); Equal(up ? 5L : 4L, IcPower(s, "damage-additive"), "owner and enemy gain separate Strength"); });
        Own("ForgottenRitual", (s, c, up) => { var brand = IcAdd(s, "Brand"); var a = IcAdd(s, "DefendIronclad"); IcMove(s, c, "Hand", "DrawPile"); IcPlay(s, brand); Submit(s, "Draw"); var energy = Energy(s); IcPlay(s, c); Equal(energy + (up ? 3 : 2), Energy(s), "earlier exhaust enables gain; own exhaust happens later"); True(Members(s, "ExhaustPile").Contains(a), "real earlier exhaust"); });
        Own("Headbutt", (s, c, up) => { var a = IcAdd(s, "DefendIronclad"); IcMove(s, a, "Hand", "DiscardPile"); IcPlay(s, c); Equal(up ? 988L : 991L, EnemyHp(s), "damage"); Equal(a, Members(s, "DrawPile").First(), "selected discard moved to top"); True(Members(s, "DiscardPile").Contains(c), "Headbutt was ineligible during its own selection"); });
        Own("Mangle", (s, c, up) => { IcPlay(s, c); Equal(up ? 980L : 985L, EnemyHp(s), "damage before loss"); Equal(up ? -15L : -10L, IcPower(s, "damage-additive"), "negative enemy strength"); IcNextTurn(s); Equal(0L, IcPower(s, "damage-additive"), "enemy strength restored after its turn"); });
        Own("NotYet", (s, c, up) => { Set(s, Player(s), "Player", "Hp", Value.Of(50L)); IcPlay(s, c); Equal(up ? 63L : 60L, Hp(s), "heal"); True(Members(s, "ExhaustPile").Contains(c), "exhaust result"); });
        Own("PactsEnd", (s, c, up) => { for (var i = 0; i < 3; i++) IcMove(s, IcAdd(s, "DefendIronclad"), "Hand", "ExhaustPile"); IcPlay(s, c); Equal(up ? 977L : 983L, EnemyHp(s), "three exhausted enables area hit"); });
        Own("PerfectedStrike", (s, c, up) => { IcAdd(s, "StrikeIronclad"); var a = IcAdd(s, "PommelStrike"); IcMove(s, a, "Hand", "ExhaustPile"); IcPlay(s, c); Equal(up ? 985L : 988L, EnemyHp(s), "all combat Strike tags includes self and exhaust"); });
        Own("Pillage", (s, c, up) => { IcMove(s, IcAdd(s, "Anger"), "Hand", "DrawPile"); IcMove(s, IcAdd(s, "DefendIronclad"), "Hand", "DrawPile"); IcMove(s, IcAdd(s, "StrikeIronclad"), "Hand", "DrawPile"); IcPlay(s, c); Equal(up ? 991L : 994L, EnemyHp(s), "damage"); Equal(2, Hand(s).Count, "stops after first nonattack draw"); Equal(1, Members(s, "DrawPile").Count, "does not draw next attack"); });
        Own("PrimalForce", (s, c, up) => { IcAdd(s, "StrikeIronclad"); var defend = IcAdd(s, "DefendIronclad"); IcPlay(s, c); var rock = Hand(s).Single(a => a != defend); Equal("base-spire:giant-rock", CardDefinition(s, rock), "hand attack transformed"); Equal(up, CardUpgraded(s, rock), "upgrade propagates"); IcPlay(s, rock); Equal(up ? 980L : 984L, EnemyHp(s), "generated rock has actual effect"); });
        Own("GiantRock", (s, c, up) => { IcPlay(s, c); Equal(up ? 980L : 984L, EnemyHp(s), "token normal/upgrade"); });
        Own("Rampage", (s, c, up) => { IcPlay(s, c); Equal(991L, EnemyHp(s), "initial nine"); IcMove(s, c, "DiscardPile", "Hand"); IcPlay(s, c); Equal(up ? 973L : 977L, EnemyHp(s), "instance damage grows by nine/five"); });
        Own("SecondWind", (s, c, up) => { IcAdd(s, "DefendIronclad"); var a = IcAdd(s, "StrikeIronclad"); IcPlay(s, c); Equal(up ? 7L : 5L, Block(s), "block for each nonattack"); Equal(a, Hand(s).Single(), "attack remains"); Equal(1, Members(s, "ExhaustPile").Count, "only nonattack exhausted"); });
        Own("SetupStrike", (s, c, up) => { IcPlay(s, c); Equal(up ? 991L : 993L, EnemyHp(s), "damage before own Strength"); Equal(up ? 3L : 2L, IcPower(s, "damage-additive"), "temporary gain"); IcNextTurn(s); Equal(0L, IcPower(s, "damage-additive"), "restored own strength at side end"); });
        Own("Stomp", (s, c, up) => { IcPlay(s, IcAdd(s, "Anger")); Equal(2L, CardCost(s, c), "prior attack discounts"); IcPlay(s, c); Equal(up ? 979L : 982L, EnemyHp(s), "area damage"); Equal(1L, CardCost(s, c), "own before-play also discounts"); IcNextTurn(s); Equal(3L, CardCost(s, c), "temporary discount reset"); });
        Own("SwordBoomerang", (s, c, up) => { IcPlay(s, c); Equal(up ? 988L : 991L, EnemyHp(s), "independent random hits"); Equal(up ? 4L : 3L, s.Random.DrawCount("base-spire:combat-targets"), "one target sample per hit"); });
        Own("TearAsunder", (s, c, up) => { IcPlay(s, IcAdd(s, "Bloodletting")); IcPlay(s, c); Equal(up ? 986L : 990L, EnemyHp(s), "one HP loss event adds one hit"); });
        Own("Thrash", (s, c, up) => { var a = IcAdd(s, "StrikeIronclad"); IcPlay(s, c); Equal(up ? 988L : 992L, EnemyHp(s), "two initial hits"); Equal(6L, IntField(s, c, "Card", "DamageGrowth"), "absorbs selected card damage"); True(Members(s, "ExhaustPile").Contains(a), "selected attack exhausted"); IcMove(s, c, "DiscardPile", "Hand"); IcPlay(s, c); Equal(up ? 964L : 972L, EnemyHp(s), "growth applied to both next hits"); });
        Own("TrueGrit", (s, c, up) => { var a = IcAdd(s, "StrikeIronclad"); IcPlay(s, c); Equal(up ? 9L : 7L, Block(s), "block"); True(Members(s, "ExhaustPile").Contains(a), "selected or random remaining card exhausted"); });
        Own("Unrelenting", (s, c, up) => { IcPlay(s, c); Equal(up ? 980L : 986L, EnemyHp(s), "damage"); var a = IcAdd(s, "StrikeIronclad"); Set(s, Player(s), "Player", "Energy", Value.Of(0L)); True(LegalFor(s, "PlayAttackFromHand", a), "next attack free in candidate query"); IcPlay(s, a); Equal(0L, Energy(s), "free attack spends zero"); Equal(0L, IcPower(s, "free-attack"), "consumed before play"); });
        Own("Whirlwind", (s, c, up) => { Set(s, Player(s), "Player", "Energy", Value.Of(3L)); IcPlay(s, c); Equal(up ? 976L : 985L, EnemyHp(s), "three X hits"); Equal(0L, Energy(s), "all energy paid"); Equal(3L, IntField(s, c, "Card", "PaidX"), "captured before payment"); });

        Own("Aggression", (s, c, up) => { Equal(up, s.State.GetField(c, CardField(s, "Innate")).AsBool(), "upgraded Innate metadata"); IcPlay(s, c); var a = IcAdd(s, "Anger"); IcMove(s, a, "Hand", "DiscardPile"); IcNextTurn(s); True(Hand(s).Contains(a) && CardUpgraded(s, a), "returns and upgrades discard attack on next side start"); });
        Own("Barricade", (s, c, up) => { IcPlay(s, c); Equal(up ? 28L : 27L, Energy(s), "cost upgrade"); Set(s, Player(s), "Player", "Block", Value.Of(17L)); IcNextTurn(s); Equal(17L, Block(s), "retains previous block"); });
        Own("Colossus", (s, c, up) => { IcPlay(s, c); Equal(up ? 8L : 5L, Block(s), "block"); Equal(1L, IcPower(s, "halve-vulnerable-attacks"), "power active"); IcNextTurn(s); Equal(0L, IcPower(s, "halve-vulnerable-attacks"), "decremented after enemy side"); });
        Own("Corruption", (s, c, up) => { IcPlay(s, c); Set(s, Player(s), "Player", "Energy", Value.Of(0L)); var a = IcAdd(s, "DefendIronclad"); True(LegalFor(s, "PlayCardFromHand", a), "skills free in legal candidates"); IcPlay(s, a); Equal(5L, Block(s), "skill resolves"); True(Members(s, "ExhaustPile").Contains(a), "skill exhausts"); });
        Own("CrimsonMantle", (s, c, up) => { IcPlay(s, c); IcNextTurn(s); Equal(79L, Hp(s), "one application adds one self-damage"); Equal(up ? 10L : 8L, Block(s), "unpowered turn block"); });
        Own("Cruelty", (s, c, up) => { IcPlay(s, c); IcPlay(s, IcAdd(s, "Tremble")); IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(up ? 988L : 990L, EnemyHp(s), "Vulnerable becomes 2x or 1.75x"); });
        Own("DarkEmbrace", (s, c, up) => { IcPlay(s, c); IcMove(s, IcAdd(s, "StrikeIronclad"), "Hand", "DrawPile"); IcPlay(s, IcAdd(s, "Impervious")); Equal(1, Hand(s).Count, "exhaust draws a card"); });
        Own("DemonForm", (s, c, up) => { IcPlay(s, c); IcNextTurn(s); Equal(up ? 3L : 2L, IcPower(s, "damage-additive"), "strength every side start"); IcNextTurn(s); Equal(up ? 6L : 4L, IcPower(s, "damage-additive"), "repeated trigger"); });
        Own("FeelNoPain", (s, c, up) => { IcPlay(s, c); IcPlay(s, IcAdd(s, "Impervious")); Equal(up ? 34L : 33L, Block(s), "unpowered block on exhaust"); });
        Own("Hellraiser", (s, c, up) => { IcPlay(s, c); var a = IcAdd(s, "StrikeIronclad"); IcMove(s, a, "Hand", "DrawPile"); var energy = Energy(s); Submit(s, "Draw"); Equal(994L, EnemyHp(s), "Strike tag auto played on draw"); Equal(energy, Energy(s), "automatic card does not pay"); True(Members(s, "DiscardPile").Contains(a), "automatic result pile"); });
        Own("Inferno", (s, c, up) => { IcPlay(s, c); IcNextTurn(s); Equal(79L, Hp(s), "turn self damage"); Equal(up ? 991L : 994L, EnemyHp(s), "HP loss triggers unpowered area damage"); });
        Own("Juggernaut", (s, c, up) => { IcPlay(s, c); IcPlay(s, IcAdd(s, "DefendIronclad")); Equal(up ? 992L : 994L, EnemyHp(s), "positive block triggers unpowered random hit"); });
        Own("Juggling", (s, c, up) => { Equal(up, s.State.GetField(c, CardField(s, "Innate")).AsBool(), "Innate upgrade"); IcPlay(s, c); for (var i = 0; i < 3; i++) IcPlay(s, IcAdd(s, "Anger")); Equal(1, Hand(s).Count, "exactly third attack creates one clone"); Equal("base-spire:anger", CardDefinition(s, Hand(s).Single()), "copies actual third attack"); IcPlay(s, Hand(s).Single()); Equal(0, Hand(s).Count, "fourth attack creates no clone"); });
        Own("OneTwoPunch", (s, c, up) => { IcPlay(s, c); IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(988L, EnemyHp(s), "first attack played twice"); IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(up ? 976L : 982L, EnemyHp(s), "upgrade affects number of doubled attacks, not repeat count"); });
        Own("Pyre", (s, c, up) => { IcPlay(s, c); Equal(28L, Energy(s), "does not immediately gain energy"); IcNextTurn(s); Equal(up ? 5L : 4L, Energy(s), "next energy reset modified"); });
        Own("Rage", (s, c, up) => { IcPlay(s, c); IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(up ? 5L : 3L, Block(s), "unpowered block after attack"); IcNextTurn(s); IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(0L, Block(s), "expired at player side end"); });
        Own("Rupture", (s, c, up) => { IcPlay(s, c); IcPlay(s, IcAdd(s, "Hemokinesis")); Equal(985L, EnemyHp(s), "HP-loss strength is deferred until after the damaging card"); Equal(up ? 2L : 1L, IcPower(s, "damage-additive"), "deferred strength applied after play"); });
        Own("Stampede", (s, c, up) => { IcPlay(s, c); var a = IcAdd(s, "StrikeIronclad"); var energy = Energy(s); Submit(s, "EndDevelopmentTurn"); Equal(994L, EnemyHp(s), "auto attack before flushing hand"); Equal(energy, Energy(s), "auto attack is free"); True(Members(s, "DiscardPile").Contains(a), "correct result pile"); });
        Own("StoneArmor", (s, c, up) => { IcPlay(s, c); Submit(s, "EndDevelopmentTurn"); Equal(up ? 6L : 4L, Block(s), "unpowered block before enemy phase"); Submit(s, "BeginDevelopmentTurn"); Equal(up ? 5L : 3L, IcPower(s, "plating"), "decrements after first owner turn"); });
        Own("Unmovable", (s, c, up) => { IcPlay(s, c); IcPlay(s, IcAdd(s, "DefendIronclad")); Equal(10L, Block(s), "first powered block doubled"); IcPlay(s, IcAdd(s, "DefendIronclad")); Equal(15L, Block(s), "second play normal"); IcNextTurn(s); IcPlay(s, IcAdd(s, "DefendIronclad")); Equal(10L, Block(s), "turn history reset"); });
        Own("Vicious", (s, c, up) => { IcPlay(s, c); for (var i = 0; i < 3; i++) IcMove(s, IcAdd(s, "DefendIronclad"), "Hand", "DrawPile"); IcPlay(s, IcAdd(s, "Tremble")); Equal(up ? 2 : 1, Hand(s).Count, "draw after positive Vulnerable application"); });

        Own("Havoc", (s, c, up) => { var a = IcAdd(s, "StrikeIronclad"); IcMove(s, a, "Hand", "DrawPile"); var before = Energy(s); IcPlay(s, c); Equal(994L, EnemyHp(s), "top draw auto played"); Equal(before - (up ? 0 : 1), Energy(s), "only Havoc paid"); True(Members(s, "ExhaustPile").Contains(a), "forced exhaust result"); });
        Own("Cascade", (s, c, up) => { for (var i = 0; i < 4; i++) IcMove(s, IcAdd(s, "StrikeIronclad"), "Hand", "DrawPile"); Set(s, Player(s), "Player", "Energy", Value.Of(2L)); IcPlay(s, c); Equal(up ? 982L : 988L, EnemyHp(s), "X or X+1 automatic cards"); Equal(0L, Energy(s), "X paid once"); Equal(0, Members(s, "ExhaustPile").Count, "no forced exhaust"); });
        Own("InfernalBlade", (s, c, up) => { IcPlay(s, c); var a = Hand(s).Single(); Equal("attack", s.State.GetField(a, CardField(s, "Kind")).AsString(), "generated from full attack pool"); Equal(0L, CardCost(s, a), "free this turn"); True(Members(s, "ExhaustPile").Contains(c), "source exhausts"); IcNextTurn(s); Equal(IntField(s, a, "Card", "BaseCost"), CardCost(s, a), "cost restores"); });
        Own("Stoke", (s, c, up) => { var a = IcAdd(s, "StrikeIronclad"); var b = IcAdd(s, "DefendIronclad"); IcPlay(s, c); Equal(2, Hand(s).Count, "same count regenerated"); True(Hand(s).All(x => CardUpgraded(s, x) == up), "upgrade every generated card"); True(Members(s, "ExhaustPile").Contains(a) && Members(s, "ExhaustPile").Contains(b), "original hand exhausted"); True(Hand(s).All(x => x != a && x != b), "new instance identities"); });

        RunIroncladBoundaryChecks(package, check);
        return witnesses;
    }
}
