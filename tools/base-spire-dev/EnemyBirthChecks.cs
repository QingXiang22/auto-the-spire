using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunEnemyBirthChecks(DslPackage package, Action<string, Action> check)
    {
        using var copy = new Copy(package.Root);
        copy.Include("enemy-birth-checks.spire", """
            entity BirthRoomAudit { fields = { Entered = { type = Bool default = false } Added = { type = Int default = 0 } } }
            action BirthEnableRoomAudit { role = player locals = { audit = Binding } body = { create = { type = BirthRoomAudit scope = frameScope() into = audit } } }
            procedure BirthAuditRoom { parameters = { owner = Ref(Player) visit = Ref(RunVisit) } body = {
              if = { condition = exists(type(BirthRoomAudit)) then = {
                forEach = { query = type(Enemy) item = enemy body = {
                  if = { condition = or(not(equal(field(enemy, Enemy.MoveId), "none")), not(exists(where(type(EnemyMoveState), s, equal(field(s, EnemyMoveState.Enemy), enemy))))) then = { fail = { message = "Room entry requires empty initialized move graphs" } } }
                } }
                set = { target = first(type(BirthRoomAudit)) field = BirthRoomAudit.Entered value = true }
              } }
            } }
            procedure BirthAuditAdded { parameters = { owner = Ref(Player) target = Ref(Enemy) } body = {
              if = { condition = exists(type(BirthRoomAudit)) then = {
                if = { condition = not(field(first(type(BirthRoomAudit)), BirthRoomAudit.Entered)) then = { fail = { message = "Enemy entry must follow room entry" } } }
                set = { target = first(type(BirthRoomAudit)) field = BirthRoomAudit.Added value = add(field(first(type(BirthRoomAudit)), BirthRoomAudit.Added), 1) }
              } }
            } }
            subscription BirthAuditRoom { event = NormalRoomEntered reaction = BirthAuditRoom }
            subscription BirthAuditAdded { event = EnemyAddedToCombat reaction = BirthAuditAdded }
            action BirthRemove { role = player body = { call = { procedure = RemoveCombatEnemy args = { target = first(type(Enemy)) } } } }
            action BirthPrepare { role = player body = { call = { procedure = PrepareEnemyBirthContext } } }
            action BirthInvalidHp { role = player locals = { hp = Int } body = { call = { procedure = RollEncounterEnemyHp args = { min = 21 max = 17 } into = hp } } }
            """);
        var rules = copy.Load();
        check("BIRTH normal room notification follows graph setup and precedes all monster entry effects", () =>
        {
            var s = rules.CreateSession(777, ["player"], "777"); s.Start(); Submit(s, "BirthEnableRoomAudit"); Submit(s, "StartNormalRun");
            var request = s.PendingDecision!;
            var option = request.Candidates.Single(o => TextField(s, o, "NeowOption", "RelicId") == "base-spire:booming-conch");
            s.Answer(new DecisionAnswer(request.RequestId, Instances: [option]));
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
            var choose = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
            s.Submit(choose.Definition, s.Participant(0), choose.Bindings[0]); Healthy(s);
            var audit = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:BirthRoomAudit").Id).Single();
            Equal(3L, IntField(s, audit, "BirthRoomAudit", "Added"), "all real SlimesWeak monsters enter after room notification");
            True(Targets(s).All(e => MoveText(s, e, "Enemy", "MoveId") != "none"), "normal first turn prepares all moves");
        });
        check("BIRTH removal does not reuse identity, explicit fresh context resets and sessions isolate", () =>
        {
            var s = Start(rules); Submit(s, "FuzzyWurmCrawler.Spawn");
            Equal(1L, IntField(s, FirstTarget(s), "Enemy", "CombatId"), "player was registered first");
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EnemyMoveState").Id).Count(), "creation does not build a graph before the batch is complete");
            Submit(s, "BirthRemove"); Submit(s, "FuzzyWurmCrawler.Spawn");
            Equal(2L, IntField(s, FirstTarget(s), "Enemy", "CombatId"), "empty side is not a fresh context");
            Equal(1L, IntField(s, FirstTarget(s), "Enemy", "BirthOrder"), "birth is monotonic");
            Submit(s, "BirthRemove"); Submit(s, "BirthPrepare"); Submit(s, "FuzzyWurmCrawler.Spawn");
            Equal(1L, IntField(s, FirstTarget(s), "Enemy", "CombatId"), "explicit new combat starts after player ID");
            var other = Start(rules); Submit(other, "FuzzyWurmCrawler.Spawn");
            Equal(1L, IntField(other, FirstTarget(other), "Enemy", "CombatId"), "another session starts independently");
            Equal("none", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "creation does not resolve initial node");
        });
        check("BIRTH a nonempty context rejects reset without changing the birth counter or RNG", () =>
        {
            var s = Start(rules); Submit(s, "FuzzyWurmCrawler.Spawn");
            var before = IntField(s, Player(s), "Player", "NextEnemyBirthOrder"); var draws = s.Random.TotalDraws;
            Throws<EngineFaultException>(() => Submit(s, "BirthPrepare"));
            Equal(before, IntField(s, Player(s), "Player", "NextEnemyBirthOrder"), "rejected reset keeps identity");
            Equal(draws, s.Random.TotalDraws, "rejected reset consumes nothing");
        });
        check("BIRTH invalid HP bounds fail before a niche draw", () =>
        {
            var s = Start(rules); var draws = s.Random.TotalDraws;
            Throws<EngineFaultException>(() => Submit(s, "BirthInvalidHp"));
            True(s.Fault!.Message.Contains("HP minimum exceeds maximum", StringComparison.Ordinal), "named HP failure");
            Equal(draws, s.Random.TotalDraws, "invalid HP does not sample");
        });
    }
}
