using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunBattleFriendChecks(DslPackage package, Action<string, Action> check)
    {
        using var copy = new Copy(package.Root);
        copy.Include("dummy-audit.spire", """
            entity DummyAudit { fields = { Deaths = { type = Int default = 0 } Escapes = { type = Int default = 0 } Ends = { type = Int default = 0 } Removed = { type = Int default = 0 } } }
            action DummyAudit { role = player locals = { audit = Binding } body = { create = { type = DummyAudit scope = frameScope() into = audit } } }
            procedure DummyDeath { parameters = { target = Ref(Enemy) } body = {
              set = { target = first(type(DummyAudit)) field = DummyAudit.Deaths value = add(field(first(type(DummyAudit)), DummyAudit.Deaths), 1) }
            } }
            subscription DummyDeath { event = EnemyDied reaction = DummyDeath }
            procedure DummyEscape { parameters = { owner = Ref(Player) target = Ref(Enemy) } body = {
              if = { condition = or(not(field(target, Enemy.Escaped)), lessOrEqual(field(target, Enemy.Hp), 0)) then = { fail = { message = "escape must detach a living enemy" } } }
              if = { condition = exists(where(collection(Powers), p, equal(field(p, Power.HolderEnemy), target))) then = { fail = { message = "escape must remove every Power first" } } }
              # The next creature's Power has not ticked yet: dispatch is Power -> Monster per creature.
              if = { condition = exists(where(collection(Powers), p, and(equal(field(p, Power.DefinitionId), "base-spire:battleworn-dummy-time-limit-power"), equal(field(p, Power.Stacks), 1)))) then = {
                if = { condition = pureCall(BattlewornDummyTimeLimitPower.Expired, field(first(collection(Powers)), Power.HolderEnemy)) then = { fail = { message = "later creature ticked before earlier monster" } } }
              } }
              set = { target = first(type(DummyAudit)) field = DummyAudit.Escapes value = add(field(first(type(DummyAudit)), DummyAudit.Escapes), 1) }
            } }
            subscription DummyEscape { event = EnemyEscaped reaction = DummyEscape }
            procedure DummyEnd { parameters = { owner = Ref(Player) victory = Bool } body = {
              set = { target = first(type(DummyAudit)) field = DummyAudit.Ends value = add(field(first(type(DummyAudit)), DummyAudit.Ends), 1) }
            } }
            subscription DummyEnd { event = CombatEnded reaction = DummyEnd }
            procedure DummyRemoval { parameters = { power = Ref(Power) } body = {
              if = { condition = and(not(isNone(field(power, Power.HolderEnemy))), field(field(power, Power.HolderEnemy), Enemy.Escaped)) then = { fail = { message = "Power removal must precede escape detach" } } }
              set = { target = first(type(DummyAudit)) field = DummyAudit.Removed value = add(field(first(type(DummyAudit)), DummyAudit.Removed), 1) }
            } }
            subscription DummyRemoval { event = PowerRemoved reaction = DummyRemoval }
            action RemoveFirstTimer { role = player body = { call = { procedure = RemovePower args = { power = first(collection(Powers)) } } } }
            action AddFirstTimer { role = player body = { call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:battleworn-dummy-time-limit-power" stacks = 3 } } } }
            action RepeatFirstEscape { role = player body = {
              call = { procedure = EscapeEnemy args = { owner = first(type(Player)) target = first(type(Enemy)) } }
              call = { procedure = CheckCombatVictory args = { owner = first(type(Player)) } }
            } }
            action ProbeEscaped { role = player locals = { result = Binding } body = {
              call = { procedure = DamageEnemyFromPlayer args = { owner = first(type(Player)) target = first(type(Enemy)) amount = 9 } }
              call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:strength-power" stacks = 4 } }
              call = { procedure = SelectNextEnemyMove args = { target = first(type(Enemy)) } }
              call = { procedure = PerformEnemyMove args = { owner = first(type(Player)) target = first(type(Enemy)) } }
            } }
            """);
        var rules = copy.Load(); var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }
        bool Flag(Session s, InstanceId id, string type, string field) => s.State.GetField(id, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field)).AsBool();
        long Audit(Session s, string field) => IntField(s, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:DummyAudit").Id).Single(), "DummyAudit", field);
        Session New(int tier, int count = 1)
        {
            var s = Start(rules); Submit(s, "DummyAudit");
            for (var i = 0; i < count; i++) Submit(s, $"BattleFriendV{tier}.Spawn");
            Submit(s, "BeginDevelopmentCombat"); return s;
        }
        void Turn(Session s) { Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn"); }
        foreach (var tier in Enumerable.Range(1, 3))
        {
            Case($"DUMMY V{tier}: real idle, three enemy turns, living escape and no death rewards", () =>
            {
                var s = New(tier); var enemy = FirstTarget(s); var hp = 75L << (tier - 1);
                Equal(hp, EnemyHp(s), "fixed source HP"); Equal(1L, s.Random.DrawCount("base-spire:run:niche"), "fixed HP still draws once");
                Equal("none", MoveText(s, enemy, "Enemy", "MoveId"), "no move before the first player start"); Equal("none", MoveText(s, enemy, "Enemy", "Intent"), "no intent before the first player start");
                for (var turn = 1; turn <= 3; turn++)
                {
                    var hash = s.State.StateHash(); var draws = s.Random.TotalDraws;
                    s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
                    Equal(hash, s.State.StateHash(), "observations are pure"); Equal(draws, s.Random.TotalDraws, "observations never tick or draw");
                    Equal(4L - turn, IntField(s, Members(s, "Powers").Single(), "Power", "Stacks"), "source timer before side end");
                    Turn(s);
                    if (turn == 1) { Equal("IDLE", MoveText(s, enemy, "Enemy", "MoveId"), "first player start selects actual idle"); Equal("stun", MoveText(s, enemy, "Enemy", "Intent"), "idle source intent"); }
                    Equal(hp, EnemyHp(s), "escape never substitutes zero HP");
                    Equal(0L, s.Random.DrawCount(MonsterAi), "IDLE self loop does not draw AI");
                    Equal(turn == 3, Flag(s, enemy, "Enemy", "Escaped"), "exact escape boundary");
                }
                Equal("victory", Outcome(s), "native engine win preserved"); True(Flag(s, Player(s), "Player", "CombatTimedOut"), "forced outcome timeout preserved");
                Equal(0L, Audit(s, "Deaths"), "no death or fatal chain"); Equal(1L, Audit(s, "Escapes"), "single escape"); Equal(1L, Audit(s, "Ends"), "single settlement");
                Equal(1L, Audit(s, "Removed"), "timer removed before detach"); Equal(0, Members(s, "Powers").Count, "all Powers gone");
                True(!RewardPending(s) && !Legal(s, "BeginDevelopmentTurn"), "timeout closes turn without monster reward");
                var before = s.Random.TotalDraws; var history = IntField(s, MoveState(s, enemy), "EnemyMoveState", "Sequence");
                Submit(s, "RepeatFirstEscape"); Submit(s, "ProbeEscaped");
                Equal(1L, Audit(s, "Ends"), "repeat does not settle twice"); Equal(hp, EnemyHp(s), "escaped enemy cannot be damaged"); Equal(0, Members(s, "Powers").Count, "escaped enemy cannot receive Power");
                Equal(history, IntField(s, MoveState(s, enemy), "EnemyMoveState", "Sequence"), "escaped enemy cannot select or perform"); Equal(before, s.Random.TotalDraws, "repeat/escaped paths spend nothing");
                Submit(s, $"BattleFriendV{tier}.Spawn"); Submit(s, "BeginDevelopmentCombat");
                True(!Flag(s, Player(s), "Player", "CombatTimedOut"), "new combat resets timeout");
                Equal(1, Members(s, "Powers").Count, "only new active creature gets timer");
            });
            Case($"DUMMY V{tier}: actual last kill before third phase wins without timeout", () =>
            {
                var s = New(tier); Turn(s); Turn(s); Submit(s, "BeginDevelopmentTurn");
                Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(5L)); Submit(s, "Feed.AddToHand");
                var max = IntField(s, Player(s), "Player", "MaxHp"); Submit(s, "PlayAttackFromHand", Hand(s).Single());
                Equal("victory", Outcome(s), "real final kill wins"); Equal(1L, Audit(s, "Deaths"), "actual death"); Equal(0L, Audit(s, "Escapes"), "no timeout race after final kill");
                True(!Flag(s, Player(s), "Player", "CombatTimedOut"), "real kill eligible for event tier reward");
                Equal(max + 3, IntField(s, Player(s), "Player", "MaxHp"), "actual Fatal consumer fires only on kill");
            });
        }
        Case("DUMMY same-definition instances dispatch Power then Monster and escape independently", () =>
        {
            var s = New(1, 2); Equal(2L, s.Random.DrawCount("base-spire:run:niche"), "duplicate fixed HP also draws");
            Turn(s); Turn(s); Turn(s);
            Equal(2L, Audit(s, "Escapes"), "both snapshot listeners run despite first removal"); Equal(1L, Audit(s, "Ends"), "last active member settles once");
            True(Targets(s).All(e => Flag(s, e, "Enemy", "Escaped")), "both detach alive");
        });
        Case("DUMMY timer removal and recreation isolate instance state and expiry", () =>
        {
            var s = New(1, 2); Turn(s); Submit(s, "RemoveFirstTimer");
            Turn(s); Turn(s);
            Equal(1L, Audit(s, "Escapes"), "second creature expires independently"); True(Active(s), "untimed first enemy remains active");
            Submit(s, "AddFirstTimer"); Equal(3L, IntField(s, Members(s, "Powers").Single(), "Power", "Stacks"), "new timer starts fresh");
            Turn(s); Turn(s); True(Active(s), "recreated timer does not inherit expiry"); Turn(s);
            Equal(2L, Audit(s, "Escapes"), "recreated timer expires on its own third turn"); Equal(1L, Audit(s, "Ends"), "one final settlement");
        });
        return Enumerable.Range(1, 3).Select(t => $"base-spire:battle-friend-v{t}").Append("base-spire:battleworn-dummy-time-limit-power")
            .ToDictionary(id => id, _ => witnesses.ToArray(), StringComparer.Ordinal);
    }
}
