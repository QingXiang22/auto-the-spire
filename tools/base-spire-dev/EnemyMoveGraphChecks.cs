using System.Globalization;
using System.Text.Json;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunEnemyMovesOnly(DslPackage package)
    {
        var count = 0;
        void Case(string name, Action body) { body(); count++; Console.WriteLine("ok   " + name); }
        RunEnemyTurnChecks(package, Case); RunEnemyMoveGraphChecks(package, Case); RunSlimedChecks(package, Case); RunSlimeChecks(package, Case); RunBattleFriendChecks(package, Case); RunWrigglerChecks(package, Case); RunWhistleChecks(package, Case);
        Console.WriteLine($"All {count} enemy move checks passed; controlled development combat, not normal encounters or full simulator comparison.");
        return 0;
    }
    private const string MonsterAi = "base-spire:run:monster_ai";
    private static InstanceId MoveState(Session s, InstanceId enemy) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveState").Id)
        .Single(i => s.State.GetField(i, s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveState").FieldIndex("Enemy")).AsRef() == enemy);
    private static string MoveText(Session s, InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field)).AsString();

    private static Dictionary<string, string[]> RunEnemyMoveGraphChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }
        RunEnemyBirthChecks(package, Case);
        using var vectors = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.enemy-move-vectors.json")!);
        Case("MOVE vectors identify the locked reference and actual source paths", () =>
        {
            using var opening = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-opening-vectors.json")!);
            Equal(opening.RootElement.GetProperty("reference_lock").GetProperty("commit").GetString(), vectors.RootElement.GetProperty("reference_lock").GetProperty("commit").GetString(), "same locked current rule authority");
            foreach (var hash in vectors.RootElement.GetProperty("source_sha256").EnumerateObject())
                True(hash.Name.StartsWith("reference/sts2-sim/src/Sts2Sim.Core/", StringComparison.Ordinal) && hash.Value.GetString()!.Length == 64, "current source locator and digest");
        });
        using var copy = new Copy(package.Root);
        var probes = """
            action PrepareAll { role = player body = { forEach = { query = type(Enemy) item = enemy body = { call = { procedure = SelectNextEnemyMove args = { target = enemy } } } } } }
            action PerformFirst { role = player body = { call = { procedure = PerformEnemyMove args = { owner = first(type(Player)) target = first(type(Enemy)) } } } }
            action SelectFirst { role = player body = { call = { procedure = SelectNextEnemyMove args = { target = first(type(Enemy)) } } } }
            """;
        foreach (var (name, a, b) in new[] { ("StatusWeights", 1, 0), ("BothWeights", 1, 1), ("AttackLimitWeights", 0, 1) })
            probes += $$"""
                action {{name}} { role = player locals = { state = Binding } body = {
                  let = { into = state value = first(type(EnemyMoveState)) }
                  forEach = { query = where(type(EnemyMoveBranch), b, equal(field(field(field(b, EnemyMoveBranch.Node), EnemyMoveNode.Graph), EnemyMoveGraph.DefinitionId), "base-spire:twig-slime-m")) item = branch body = {
                    if = { condition = equal(field(field(branch, EnemyMoveBranch.Target), EnemyMoveNode.Id), "POKEY_POUNCE_MOVE") then = {
                      if = { condition = not(equal(pureCall(EnemyBranchWeight, state, branch), float32({{a}}))) then = { fail = { message = "attack source weight" } } }
                    } else = {
                      if = { condition = not(equal(pureCall(EnemyBranchWeight, state, branch), float32({{b}}))) then = { fail = { message = "status source weight" } } }
                    } }
                  } }
                } }
                """;
        copy.Include("move-probes.spire", probes);
        var observedPackage = copy.Load();
        foreach (var vector in vectors.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var seed = vector.GetProperty("seed").GetString()!;
            Case("MOVE source candidates, weights, raw draws, history and effects: " + seed, () =>
            {
                var s = observedPackage.CreateSession(1, ["player"], seed, new SessionDebugOptions(MaxRecords: 100_000)); s.Start(); Healthy(s);
                Submit(s, "TwigSlimeM.Spawn"); var enemy = FirstTarget(s);
                Equal(26L, EnemyHp(s), "A0 minimum HP, no HP RNG claim");
                Equal("none", MoveText(s, enemy, "Enemy", "MoveId"), "creation has no selected move");
                Equal(0L, s.Random.TotalDraws, "first move does not sample");
                Equal(vector.GetProperty("monster_ai_seed").GetUInt64(), s.Random.Capture().ExplicitSeeds[MonsterAi], "source stream seed");
                var branchType = s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveBranch");
                var nodeType = s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveNode");
                var candidates = s.State.AllInstances(branchType.Id).Where(b => MoveText(s, s.State.GetField(b, branchType.FieldIndex("Node")).AsRef(), "EnemyMoveNode", "Id") == "RAND" && MoveText(s, s.State.GetField(b, branchType.FieldIndex("Target")).AsRef(), "EnemyMoveNode", "Id") is "POKEY_POUNCE_MOVE" or "STICKY_SHOT_MOVE").Select(b => s.State.GetField(b, branchType.FieldIndex("Target")).AsRef())
                    .Select(n => s.State.GetField(n, nodeType.FieldIndex("Id")).AsString()).ToArray();
                Submit(s, "BeginDevelopmentCombat");
                Set(s, Player(s), "Player", "Hp", Value.Of(1000L)); Set(s, Player(s), "Player", "MaxHp", Value.Of(1000L));
                Submit(s, "BeginDevelopmentTurn");
                long expectedHp = 1000; var expectedSlimed = 0;
                var next = vector.GetProperty("initial").GetString();
                foreach (var roll in vector.GetProperty("rolls").EnumerateArray())
                {
                    Equal(string.Join(",", roll.GetProperty("candidates").EnumerateArray().Select(x => x.GetString())), string.Join(",", candidates), "unfiltered source-order candidates");
                    var weights = roll.GetProperty("weight_bits").EnumerateArray().Select(x => x.GetInt64()).ToArray();
                    Submit(s, weights[0] == 0 ? "AttackLimitWeights" : weights[1] == 0 ? "StatusWeights" : "BothWeights");
                    var before = s.State.StateHash(); var version = s.StateVersion; var draws = s.Random.TotalDraws;
                    for (var read = 0; read < 3; read++) { s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0)); _ = s.Debug!.Capture(); }
                    Equal(before, s.State.StateHash(), "observation does not select"); Equal(version, s.StateVersion, "observation version"); Equal(draws, s.Random.TotalDraws, "observation RNG");
                    if (next == "STICKY_SHOT_MOVE") expectedSlimed++; else expectedHp -= 11;
                    Submit(s, "EndDevelopmentTurn");
                    Equal(expectedHp, Hp(s), "saved move executes full attack or status effect");
                    Equal(expectedSlimed, Members(s, "DiscardPile").Count(c => CardDefinition(s, c) == "base-spire:slimed"), "one real status instance per sticky shot");
                    Submit(s, "BeginDevelopmentTurn");
                    next = roll.GetProperty("selected").GetString();
                    Equal(next, MoveText(s, enemy, "Enemy", "MoveId"), "source selected move is saved");
                    Equal(next == "STICKY_SHOT_MOVE" ? "status" : "attack", EnemyIntent(s), "public intent reads saved move");
                    Equal(roll.GetProperty("after").GetInt64(), s.Random.DrawCount(MonsterAi), "even a single eligible branch consumes");
                    Equal(0L, s.Debug!.DroppedRecords, "comparison requires complete diagnostic records");
                    var call = s.Debug!.Records.Last(r => r.Random is not null).Random!;
                    Equal("double", call.Operation, "source raw double operation");
                    Equal(roll.GetProperty("before").GetInt64(), call.Before, "raw cursor before");
                    Equal(roll.GetProperty("after").GetInt64(), call.After, "raw cursor after");
                    Equal(roll.GetProperty("raw_double").GetDouble(), double.Parse(call.Result, CultureInfo.InvariantCulture), "independent raw source value");
                }
                Equal(13L, IntField(s, MoveState(s, enemy), "EnemyMoveState", "Sequence"), "initial state plus twelve selected moves logged");
                Equal(13, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveHistory").Id).Count(), "source history remains instance-owned");
            });
        }
        Case("MOVE fractional source weights keep their binary32 values and source selection sequence", () =>
        {
            using var fractional = new Copy(package.Root);
            var path = fractional.Path("enemies/shared/twig-slime-m.spire");
            File.WriteAllText(path, File.ReadAllText(path).Replace("maxRepeats = 2", "maxRepeats = 2 weight = 0.3", StringComparison.Ordinal).Replace("maxRepeats = 1", "maxRepeats = 1 weight = 0.9", StringComparison.Ordinal));
            var vector = vectors.RootElement.GetProperty("fractional_probe");
            var s = fractional.Load().CreateSession(1, ["player"], vector.GetProperty("seed").GetString()); s.Start(); Healthy(s);
            Submit(s, "TwigSlimeM.Spawn"); Submit(s, "BeginDevelopmentCombat");
            var branches = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveBranch").Id).ToArray();
            Equal(1050253722L, IntField(s, branches[0], "EnemyMoveBranch", "Weight"), "0.3f is preserved without integer conversion");
            Equal(1063675494L, IntField(s, branches[1], "EnemyMoveBranch", "Weight"), "0.9f is preserved");
            Set(s, Player(s), "Player", "Hp", Value.Of(1000L)); Set(s, Player(s), "Player", "MaxHp", Value.Of(1000L));
            Submit(s, "BeginDevelopmentTurn");
            foreach (var roll in vector.GetProperty("rolls").EnumerateArray())
            {
                Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
                Equal(roll.GetProperty("selected").GetString(), MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "independent fractional selection");
                Equal(roll.GetProperty("after").GetInt64(), s.Random.DrawCount(MonsterAi), "fractional branch counter");
            }
        });
        Case("MOVE two same-definition enemies and sessions keep separate histories, and new combat resets", () =>
        {
            var s = Start(observedPackage); Submit(s, "TwigSlimeM.Spawn"); Submit(s, "TwigSlimeM.Spawn");
            var enemies = Targets(s).ToArray(); Submit(s, "BeginDevelopmentCombat"); Submit(s, "PrepareAll");
            var first = MoveState(s, enemies[0]); var second = MoveState(s, enemies[1]);
            Submit(s, "PerformFirst"); Submit(s, "SelectFirst");
            Equal("POKEY_POUNCE_MOVE", MoveText(s, enemies[0], "Enemy", "MoveId"), "first advanced after status");
            Equal("STICKY_SHOT_MOVE", MoveText(s, enemies[1], "Enemy", "MoveId"), "second still has its initial move");
            Equal(2L, IntField(s, first, "EnemyMoveState", "Sequence"), "first log grew"); Equal(1L, IntField(s, second, "EnemyMoveState", "Sequence"), "second log did not");
            var other = Start(observedPackage); Submit(other, "TwigSlimeM.Spawn");
            Equal(0L, other.Random.DrawCount(MonsterAi), "another session has not rolled");
            var before = s.State.StateHash(); var draws = s.Random.TotalDraws;
            Submit(s, "SelectFirst"); Equal(before, s.State.StateHash(), "prepared move is not selected twice"); Equal(draws, s.Random.TotalDraws, "no duplicate random draw");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "PrepareAll");
            Equal("STICKY_SHOT_MOVE", MoveText(s, enemies[0], "Enemy", "MoveId"), "new combat fixed initial move");
            Equal(1L, IntField(s, MoveState(s, enemies[0]), "EnemyMoveState", "Sequence"), "new combat cleared old log");
            Equal(2, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveHistory").Id).Count(), "only each initial history remains");
            Equal(draws, s.Random.TotalDraws, "combat reset never rewinds the shared random stream");
        });
        Case("MOVE cooldown and use-once restrictions inspect the selected instance history", () =>
        {
            var s = Start(observedPackage); Submit(s, "TwigSlimeM.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "PrepareAll");
            var branches = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveBranch").Id).ToArray();
            Set(s, branches[1], "EnemyMoveBranch", "Cooldown", Value.Of(2L));
            Submit(s, "PerformFirst"); Submit(s, "SelectFirst");
            Submit(s, "StatusWeights"); // The initial status remains inside the last two selected moves.
            Submit(s, "PerformFirst"); Submit(s, "SelectFirst");
            Equal("POKEY_POUNCE_MOVE", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "cooldown forces the second attack");
            Submit(s, "AttackLimitWeights"); // Status has now left the cooldown window.
            Submit(s, "PerformFirst"); Submit(s, "SelectFirst");
            Equal("STICKY_SHOT_MOVE", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "status becomes eligible after two attacks");
            s = Start(observedPackage); Submit(s, "TwigSlimeM.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "PrepareAll");
            branches = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveBranch").Id).ToArray();
            Set(s, branches[0], "EnemyMoveBranch", "Once", Value.Of(true));
            Submit(s, "PerformFirst"); Submit(s, "SelectFirst"); Submit(s, "AttackLimitWeights");
            Submit(s, "PerformFirst"); Submit(s, "SelectFirst");
            Equal("STICKY_SHOT_MOVE", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "used attack is no longer eligible");
            Submit(s, "PerformFirst"); var draws = s.Random.TotalDraws;
            Throws<EngineFaultException>(() => Submit(s, "SelectFirst")); Equal(draws, s.Random.TotalDraws, "no branch after once plus CannotRepeat; no fallback draw");
        });
        Case("MOVE dead targets do not act or select, and fixed graphs consume no AI random words", () =>
        {
            var s = Start(observedPackage); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "TwigSlimeM.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "PrepareAll"); var dead = Targets(s)[1]; Set(s, dead, "Enemy", "Hp", Value.Of(0L));
            for (var turn = 0; turn < 3; turn++) { Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn"); }
            Equal(0L, s.Random.DrawCount(MonsterAi), "deterministic cycle and dead slime never sample");
            Equal(0, Members(s, "DiscardPile").Count(c => CardDefinition(s, c) == "base-spire:slimed"), "dead status monster cannot create cards");
            Equal(1L, IntField(s, MoveState(s, dead), "EnemyMoveState", "Sequence"), "dead history stays unchanged");
        });
        Case("MOVE illegal graphs fail at assembly before random use", () =>
        {
            foreach (var (from, to) in new[] {
                ("initial = \"STICKY_SHOT_MOVE\"", "initial = \"MISSING\""),
                ("id = \"POKEY_POUNCE_MOVE\"", "id = \"STICKY_SHOT_MOVE\""),
                ("next = \"RAND\"", "next = \"MISSING\""),
                ("next = \"RAND\"", "next = \"\""),
                ("target = \"POKEY_POUNCE_MOVE\"", "target = \"MISSING\""),
                ("kind = \"random\"", "kind = \"unknown\""),
                ("maxRepeats = 2", "maxRepeats = 2 weight = -0.1") })
            {
                using var invalid = new Copy(package.Root);
                var path = invalid.Path("enemies/shared/twig-slime-m.spire"); File.WriteAllText(path, File.ReadAllText(path).Replace(from, to, StringComparison.Ordinal));
                var s = invalid.Load().CreateSession(1, ["player"]);
                Throws<EngineFaultException>(s.Start); Equal(SessionStatus.Faulted, s.Status, "invalid graph faults"); Equal(0L, s.Random.TotalDraws, "assembly never samples");
                True(s.Fault!.Message.Contains("Enemy move graph", StringComparison.Ordinal), "named graph failure");
            }
            using var noBranches = new Copy(package.Root);
            var noBranchesPath = noBranches.Path("enemies/shared/twig-slime-m.spire");
            File.WriteAllLines(noBranchesPath, File.ReadAllLines(noBranchesPath).Where(line => !line.Contains("template = EnemyMoveBranchDefinition", StringComparison.Ordinal)));
            var missing = noBranches.Load().CreateSession(1, ["player"]); Throws<EngineFaultException>(missing.Start);
            True(missing.Fault!.Message.Contains("no branches", StringComparison.Ordinal), "random nodes require registered successors");
            Equal(0L, missing.Random.TotalDraws, "missing branches fail at assembly");
        });
        Case("MOVE no legal branch faults without a compensating draw, and branch cycles respect execution budget", () =>
        {
            using var empty = new Copy(package.Root);
            var path = empty.Path("enemies/shared/twig-slime-m.spire"); File.WriteAllText(path, File.ReadAllText(path).Replace("maxRepeats = 2", "maxRepeats = 2 weight = 0.0", StringComparison.Ordinal));
            var s = Start(empty.Load()); Submit(s, "TwigSlimeM.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn");
            Throws<EngineFaultException>(() => Submit(s, "BeginDevelopmentTurn")); Equal(0L, s.Random.DrawCount(MonsterAi), "zero total never samples");
            True(s.Fault!.Message.Contains("no legal branch", StringComparison.Ordinal), "no silent fallback");
            using var loop = new Copy(package.Root);
            path = loop.Path("enemies/shared/twig-slime-m.spire"); File.WriteAllText(path, File.ReadAllText(path).Replace("target = \"POKEY_POUNCE_MOVE\" maxRepeats = 2", "target = \"RAND\" maxRepeats = 2", StringComparison.Ordinal));
            s = Start(loop.Load()); Submit(s, "TwigSlimeM.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn");
            Throws<EngineFaultException>(() => Submit(s, "BeginDevelopmentTurn")); Equal(SessionStatus.Faulted, s.Status, "transition loop stops at core budget");
            Equal(EngineFaultCode.BudgetExceeded, s.Fault!.Code, "transition recursion uses the core budget failure");
            True(s.Random.DrawCount(MonsterAi) > 0 && s.Random.DrawCount(MonsterAi) <= s.Budget.MaxCallDepth, "bounded transitions, no infinite loop");
        });
        Case("MOVE status creation suspends and resumes once; stale answers preserve state and RNG", () =>
        {
            using var paused = new Copy(package.Root);
            paused.Include("pause-status.spire", """
                procedure PauseStatus { parameters = { owner = Ref(Player) card = Ref(Card) clone = Bool } locals = { answer = Bool } body = {
                  if = { condition = equal(field(card, Card.DefinitionId), "base-spire:slimed") then = {
                    decide = { participant = field(owner, Player.Owner) kind = Bool prompt = "status probe" into = answer }
                  } }
                } }
                subscription OnPauseStatus { event = CardEnteredCombat reaction = PauseStatus }
                """);
            var s = Start(paused.Load()); Submit(s, "TwigSlimeM.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(SessionStatus.AwaitingDecision, s.Status, "suspended during actual card creation"); var request = s.PendingDecision!;
            var before = s.State.StateHash(); Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request.RequestId + 1, Flag: true)));
            Equal(before, s.State.StateHash(), "stale answer is atomic"); Equal(0L, s.Random.DrawCount(MonsterAi), "no advance while paused");
            s.Answer(new DecisionAnswer(request.RequestId, Flag: true)); Healthy(s);
            Equal(1, Members(s, "DiscardPile").Count(c => CardDefinition(s, c) == "base-spire:slimed"), "one card after continuation");
            Equal(0L, s.Random.DrawCount(MonsterAi), "enemy end does not select after continuation");
            Submit(s, "BeginDevelopmentTurn");
            Equal(1L, s.Random.DrawCount(MonsterAi), "next player start selects once");
            before = s.State.StateHash(); Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request.RequestId, Flag: true)));
            Equal(before, s.State.StateHash(), "duplicate answer is atomic"); Equal(1L, s.Random.DrawCount(MonsterAi), "duplicate does not select again");
        });
        return new Dictionary<string, string[]> { ["base-spire:twig-slime-m"] = witnesses.ToArray() };
    }

    private static Dictionary<string, string[]> RunSlimedChecks(DslPackage package, Action<string, Action> check)
    {
        const string name = "MOVE Slimed: created metadata, no upgrade, actual draw, energy cost and exhaustion";
        check(name, () =>
        {
            var s = Start(package); Submit(s, "AddDevelopmentTarget"); Submit(s, "Slimed.AddToHand");
            var card = Hand(s).Single(); Equal(1L, CardCost(s, card), "source cost");
            Equal("status", MoveText(s, card, "Card", "Kind"), "source type"); Equal("status", MoveText(s, card, "Card", "Rarity"), "source rarity");
            True(!s.State.GetField(card, CardField(s, "CanUpgrade")).AsBool(), "source MaxUpgradeLevel=0");
            True(!LegalFor(s, "UpgradeCardInHand", card), "upgrade not legal");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayCardFromHand", card);
            Equal(2L, Energy(s), "one energy spent"); Equal(1, Hand(s).Count, "one real draw");
            True(Members(s, "ExhaustPile").Contains(card), "actual exhaust movement");
            True(!Members(s, "DiscardPile").Contains(card), "played status is not discarded");
            var before = s.State.StateHash(); var draws = s.Random.TotalDraws;
            Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:PlayCardFromHand"), s.Participant(0), [Value.OfRef(card)]));
            Equal(before, s.State.StateHash(), "exhausted card cannot be played twice"); Equal(draws, s.Random.TotalDraws, "invalid play never draws");
        });
        return new Dictionary<string, string[]> { ["base-spire:slimed"] = [name] };
    }
}
