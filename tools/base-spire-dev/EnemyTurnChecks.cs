using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunEnemyTurnChecks(DslPackage package, Action<string, Action> check)
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

        Case("CP57: the enemy phase runs at the end of the player turn and its move rotation advances", () =>
        {
            var s = Start(package); Submit(s, "FuzzyWurmCrawler.Spawn");
            Equal(55L, EnemyHp(s), "the development spawn uses the source minimum HP");
            Equal("none", EnemyIntent(s), "creation does not select a move");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(76L, Hp(s), "the acid goop hit for 4"); Equal(1L, EnemyMove(s), "the next move is inhale"); Equal("buff", EnemyIntent(s), "and the intent is public");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(76L, Hp(s), "inhale deals no damage"); Equal(7L, PowerStacksFor(s, "base-spire:strength-power", FirstTarget(s)), "the enemy gained 7 strength");
            Equal(2L, EnemyMove(s), "the next move is the second goop");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(65L, Hp(s), "the goop now deals 4 + 7 strength"); Equal(0L, EnemyMove(s), "the rotation wraps to the first goop state");
        });
        Case("CP58: enemy attacks honour player block, enemy weak and player vulnerable", () =>
        {
            var s = Start(package); Submit(s, "FuzzyWurmCrawler.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Block", Value.Of(10L));
            Submit(s, "EndDevelopmentTurn");
            Equal(80L, Hp(s), "the whole 4 damage was blocked"); Equal(6L, Block(s), "the remaining block is kept");
            Set(s, Player(s), "Player", "Block", Value.Of(0L));
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Set(s, FirstTarget(s), "Enemy", "Block", Value.Of(0L));
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(69L, Hp(s), "the strength-boosted goop hit for 11");
            using var copy = new Copy(package.Root);
            copy.Include("enemy-weaken.spire", """
                action WeakenTarget { role = player body = { call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:weak-power" stacks = 1 } } } }
                """);
            var weak = Start(copy.Load()); Submit(weak, "FuzzyWurmCrawler.Spawn");
            Submit(weak, "BeginDevelopmentCombat"); Submit(weak, "WeakenTarget"); Submit(weak, "BeginDevelopmentTurn"); Submit(weak, "EndDevelopmentTurn");
            Equal(77L, Hp(weak), "a weakened goop deals 4 x 0.75 rounded down to 3");
        });
        Case("CP59: debuff durations tick down after the enemy phase, not at the player's end of turn", () =>
        {
            var s = Start(package); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "Bash.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(2L, PowerStacksFor(s, "base-spire:vulnerable-power", FirstTarget(s)), "two vulnerable stacks on the enemy");
            Submit(s, "EndDevelopmentTurn");
            Equal(1L, PowerStacksFor(s, "base-spire:vulnerable-power", FirstTarget(s)), "the enemy phase ticks one stack down");
        });

        Own("base-spire:fuzzy-wurm-crawler", "CP60 Fuzzy Wurm Crawler: source HP and the goop / inhale rotation", () =>
        {
            var s = Start(package); Submit(s, "FuzzyWurmCrawler.Spawn");
            Equal(55L, s.State.GetField(FirstTarget(s), EnemyField(s, "MaxHp")).AsInt(), "HP matches the Ascension 0 minimum");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            for (var turn = 0; turn < 4; turn++)
            {
                Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            }
            Equal(54L, Hp(s), "two goop hits (4 and 11) plus a second inhale and goop for 15 land over four turns");
        });

        Case("CP62: the shared attack operation drives two different enemies with their own damage", () =>
        {
            var slime = Start(package); Submit(slime, "TwigSlimeS.Spawn");
            Equal(7L, EnemyHp(slime), "Twig Slime S spawns with the source minimum HP");
            Submit(slime, "BeginDevelopmentCombat"); Submit(slime, "BeginDevelopmentTurn"); Submit(slime, "EndDevelopmentTurn");
            Equal(76L, Hp(slime), "the slime tackle hit for 4");
            var raider = Start(package); Submit(raider, "AssassinRubyRaider.Spawn");
            Equal(18L, EnemyHp(raider), "Ruby Raider spawns with its own HP");
            Submit(raider, "BeginDevelopmentCombat"); Submit(raider, "BeginDevelopmentTurn"); Submit(raider, "EndDevelopmentTurn");
            Equal(70L, Hp(raider), "the raider killshot hit for 10");
        });
        Case("CP63: every hittable enemy acts once in the same enemy phase and dead enemies are skipped", () =>
        {
            var s = Start(package); Submit(s, "TwigSlimeS.Spawn"); Submit(s, "AssassinRubyRaider.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(66L, Hp(s), "both attacks land in one phase (4 + 10)");
            Set(s, Targets(s)[0], "Enemy", "Hp", Value.Of(0L));
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(56L, Hp(s), "only the surviving raider attacks");
        });

        Own("base-spire:twig-slime-s", "CP64 Twig Slime S: source HP and a one-move 4 damage loop", () =>
        {
            var s = Start(package); Submit(s, "TwigSlimeS.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            for (var turn = 0; turn < 2; turn++) { Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); }
            Equal(72L, Hp(s), "two tackles of 4 each"); Equal("attack", EnemyIntent(s), "the intent never changes");
        });
        Own("base-spire:assassin-ruby-raider", "CP65 Assassin Ruby Raider: source HP and a one-move 10 damage loop", () =>
        {
            var s = Start(package); Submit(s, "AssassinRubyRaider.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Block", Value.Of(4L));
            Submit(s, "EndDevelopmentTurn");
            Equal(74L, Hp(s), "four of the ten damage were blocked"); Equal(0L, Block(s), "the block was consumed");
        });

        Case("CP73: an attack-and-defend enemy gains block that absorbs the player's next attack", () =>
        {
            var s = Start(package); Submit(s, "BowlbugEgg.Spawn");
            Equal(21L, EnemyHp(s), "the egg spawns with the source minimum HP");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(73L, Hp(s), "the bite hit for 7"); Equal(7L, EnemyBlock(s), "and the egg gained 7 block");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "Strike.AddToHand"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(1L, EnemyBlock(s), "the strike chewed through most of the block"); Equal(21L, EnemyHp(s), "no HP was lost");
        });
        Case("CP74: an attack-and-buff enemy alternates its damage and its strength buff", () =>
        {
            var s = Start(package); Submit(s, "BruteRubyRaider.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(73L, Hp(s), "the beat hit for 7"); Equal(1L, EnemyMove(s), "the next move is the roar");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(73L, Hp(s), "the roar deals no damage"); Equal(3L, PowerStacksFor(s, "base-spire:strength-power", FirstTarget(s)), "the brute gained 3 strength");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(63L, Hp(s), "the next beat deals 7 + 3");
        });
        Case("CP75: an attack-and-debuff enemy applies Frail, which weakens the next card block", () =>
        {
            var s = Start(package); Submit(s, "Stabbot.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(69L, Hp(s), "the stab hit for 11"); Equal(1L, PowerStacks(s, "base-spire:frail-power"), "the stab applied one frail");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "Defend.AddToHand"); Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(3L, Block(s), "Defend's 5 block becomes 3 under frail (5 x 0.75 rounded down)");
        });

        Own("base-spire:bowlbug-egg", "CP76 Bowlbug Egg: source HP and a bite that also blocks", () =>
        {
            var s = Start(package); Submit(s, "BowlbugEgg.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            for (var turn = 0; turn < 2; turn++) { Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); }
            Equal(66L, Hp(s), "two bites of 7 each"); Equal(7L, EnemyBlock(s), "each enemy turn starts by clearing its block, then the bite adds seven");
        });
        Own("base-spire:brute-ruby-raider", "CP77 Brute Ruby Raider: source HP and the beat / roar alternation", () =>
        {
            var s = Start(package); Submit(s, "BruteRubyRaider.Spawn");
            Equal(30L, EnemyHp(s), "spawn HP matches the Ascension 0 minimum");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal("buff", EnemyIntent(s), "the roar is announced next");
        });
        Own("base-spire:stabbot", "CP78 Stabbot: source HP and a stab that applies Frail", () =>
        {
            var s = Start(package); Submit(s, "Stabbot.Spawn");
            Equal(18L, EnemyHp(s), "spawn HP matches the Ascension 0 minimum");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal("attack,debuff", EnemyIntent(s), "both source intents remain ordered"); Equal(1L, PowerStacks(s, "base-spire:frail-power"), "one frail applied");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(58L, Hp(s), "the second stab hit for 11 again");
        });

        Own("base-spire:axe-ruby-raider", "CP81 Axe Ruby Raider: two 5/5 swings then a 12 big swing", () =>
        {
            var s = Start(package); Submit(s, "AxeRubyRaider.Spawn");
            Equal(20L, EnemyHp(s), "spawn HP matches the Ascension 0 minimum");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(75L, Hp(s), "the first swing hit for 5"); Equal(5L, EnemyBlock(s), "and gained 5 block"); Equal(1L, EnemyMove(s), "next is the second swing");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(70L, Hp(s), "the second swing hit for 5 again"); Equal(2L, EnemyMove(s), "next is the big swing");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(58L, Hp(s), "the big swing hit for 12"); Equal(0L, EnemyMove(s), "the chain wraps to the first swing");
        });
        Own("base-spire:crossbow-ruby-raider", "CP82 Crossbow Ruby Raider: reload for 3 block then fire for 14", () =>
        {
            var s = Start(package); Submit(s, "CrossbowRubyRaider.Spawn");
            Equal(18L, EnemyHp(s), "spawn HP matches the Ascension 0 minimum"); Equal("none", EnemyIntent(s), "creation has no prepared reload");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(80L, Hp(s), "the reload deals no damage"); Equal(3L, EnemyBlock(s), "and gains 3 block");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(66L, Hp(s), "the fire hit for 14"); Equal(0L, EnemyBlock(s), "the reload block was cleared at the start of its turn");
        });
        Own("base-spire:tracker-ruby-raider", "CP83 Tracker Ruby Raider: track applies Frail 2 then the hounds hit eight times", () =>
        {
            var s = Start(package); Submit(s, "TrackerRubyRaider.Spawn");
            Equal(21L, EnemyHp(s), "spawn HP matches the Ascension 0 minimum"); Equal("none", EnemyIntent(s), "creation has no prepared track");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(80L, Hp(s), "the track deals no damage"); Equal(2L, PowerStacks(s, "base-spire:frail-power"), "two frail applied");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(72L, Hp(s), "the hounds hit eight times for one");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(64L, Hp(s), "the hounds repeat on the following turn");
        });

        Own("base-spire:tracker-ruby-raider", "CP126 Tracker Vigor applies to all eight hits of one attack", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("tracker-vigor.spire", """
                entity AttackCounters { fields = { Before = { type = Int default = 0 } After = { type = Int default = 0 } } }
                action GrantVigor { role = player locals = { counters = Binding } body = {
                  create = { type = AttackCounters scope = frameScope() into = counters }
                  call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:vigor-power" stacks = 3 } }
                } }
                procedure CountBefore { parameters = { attack = Ref(Attack) } body = {
                  set = { target = first(type(AttackCounters)) field = AttackCounters.Before value = add(field(first(type(AttackCounters)), AttackCounters.Before), 1) }
                } }
                procedure CountAfter { parameters = { attack = Ref(Attack) } body = {
                  set = { target = first(type(AttackCounters)) field = AttackCounters.After value = add(field(first(type(AttackCounters)), AttackCounters.After), 1) }
                } }
                subscription CountBefore { event = BeforeAttack reaction = CountBefore }
                subscription CountAfter { event = AfterAttack reaction = CountAfter }
                """);
            var session = Start(copy.Load()); Submit(session, "TrackerRubyRaider.Spawn");
            Submit(session, "BeginDevelopmentCombat"); Submit(session, "BeginDevelopmentTurn"); Submit(session, "EndDevelopmentTurn");
            Submit(session, "BeginDevelopmentTurn"); Submit(session, "GrantVigor");
            Set(session, Player(session), "Player", "Hp", Value.Of(100L));
            Set(session, Player(session), "Player", "MaxHp", Value.Of(100L));
            Submit(session, "EndDevelopmentTurn");
            Equal(68L, Hp(session), "eight source hits each gain the original three Vigor");
            var attack = Members(session, "Attacks").Single();
            Equal(8L, IntField(session, attack, "Attack", "Hit"), "all eight hits belong to one command");
            Equal(0L, PowerStacksFor(session, "base-spire:vigor-power", FirstTarget(session)), "Vigor consumed once after the attack");
            var results = Members(session, "DamageResults");
            Equal(8, results.Count, "eight individual damage results");
            var counters = session.State.AllInstances(session.RuleSet.TypeByQualifiedId("base-spire:AttackCounters").Id).Single();
            Equal(1L, IntField(session, counters, "AttackCounters", "Before"), "one BeforeAttack notification");
            Equal(1L, IntField(session, counters, "AttackCounters", "After"), "one AfterAttack notification");
            Equal("0,1,2,3,4,5,6,7", string.Join(',', results.Select(result => IntField(session, result, "DamageResult", "Hit"))), "hit indices stay within one attack");
            True(results.All(result => IntField(session, result, "DamageResult", "Unblocked") == 4), "per-hit damage is four");
        });

        using var graphCopy = new Copy(package.Root);
        graphCopy.Include("rotation-control.spire", """
            action RotationStun { role = player body = { call = { procedure = StunEnemy args = { target = first(type(Enemy)) } } } }
            """);
        var graphPackage = graphCopy.Load();
        foreach (var (name, initial, next) in new[]
        {
            ("AssassinRubyRaider", "KILLSHOT_MOVE", "KILLSHOT_MOVE"), ("AxeRubyRaider", "SWING_1", "SWING_2"),
            ("BowlbugEgg", "BITE_MOVE", "BITE_MOVE"), ("BruteRubyRaider", "BEAT_MOVE", "ROAR_MOVE"),
            ("CrossbowRubyRaider", "RELOAD_MOVE", "FIRE_MOVE"), ("Stabbot", "STAB_MOVE", "STAB_MOVE"),
            ("TrackerRubyRaider", "TRACK_MOVE", "HOUNDS_MOVE")
        })
        {
            Case("CP126 graph isolation, stun, query purity and combat reset: " + name, () =>
            {
                var session = Start(graphPackage); Submit(session, name + ".Spawn"); Submit(session, name + ".Spawn");
                var enemies = Targets(session).ToArray();
                Submit(session, "BeginDevelopmentCombat"); Submit(session, "BeginDevelopmentTurn");
                Submit(session, "RotationStun");
                Equal("STUNNED", MoveText(session, enemies[0], "Enemy", "MoveId"), "only first instance is stunned");
                Equal(initial, MoveText(session, enemies[1], "Enemy", "MoveId"), "other instance retains source first move");
                var hash = session.State.StateHash(); var version = session.StateVersion; var draws = session.Random.TotalDraws;
                for (var read = 0; read < 3; read++) { session.Observe(session.Participant(0), "base-spire:PlayerView"); session.LegalActions(session.Participant(0)); }
                Equal(hash, session.State.StateHash(), "observations preserve state"); Equal(version, session.StateVersion, "observations preserve version");
                Equal(draws, session.Random.TotalDraws, "observations preserve RNG");
                Submit(session, "EndDevelopmentTurn");
                Submit(session, "BeginDevelopmentTurn");
                Equal(initial, MoveText(session, enemies[0], "Enemy", "MoveId"), "stun restores selected source move itself");
                Equal(next, MoveText(session, enemies[1], "Enemy", "MoveId"), "other instance advances exactly once");
                if (name == "BruteRubyRaider") { Submit(session, "EndDevelopmentTurn"); }
                if (name is "Stabbot" or "TrackerRubyRaider" or "BruteRubyRaider")
                {
                    var power = Members(session, "Powers").Single();
                    var powerType = session.RuleSet.TypeByQualifiedId("base-spire:Power");
                    Equal(Value.OfRef(enemies[1]), session.State.GetField(power, powerType.FieldIndex("ApplierEnemy")), "real monster is the Power applier");
                    True(session.State.GetField(power, powerType.FieldIndex("SourceCard")).IsNone, "monster Power has no card source");
                    True(session.State.GetField(power, powerType.FieldIndex("SourcePlay")).IsNone, "monster Power has no play source");
                }
                Submit(session, "EndDevelopmentCombat"); Submit(session, "BeginDevelopmentCombat"); Submit(session, "BeginDevelopmentTurn");
                foreach (var enemy in enemies)
                {
                    Equal(initial, MoveText(session, enemy, "Enemy", "MoveId"), "combat reset restores first move");
                    Equal(1L, IntField(session, MoveState(session, enemy), "EnemyMoveState", "Sequence"), "combat history resets per instance");
                }
                Equal(0L, session.Random.DrawCount(MonsterAi), "fixed graphs and stun do not sample AI");
                var other = Start(graphPackage); Submit(other, name + ".Spawn"); Submit(other, "BeginDevelopmentCombat"); Submit(other, "BeginDevelopmentTurn");
                Equal(initial, MoveText(other, FirstTarget(other), "Enemy", "MoveId"), "separate session retains fresh first move");
            });
        }

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }

    private static long EnemyMove(Session s) => s.State.GetField(FirstTarget(s), EnemyField(s, "MoveIndex")).AsInt();
    private static string EnemyIntent(Session s) => s.State.GetField(FirstTarget(s), EnemyField(s, "Intent")).AsString();
}
