using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunTurnStartChecks(DslPackage package, Action<string, Action> check)
    {
        using var copy = new Copy(package.Root);
        copy.Include("turn-start-checks.spire", """
            entity StartProbe { fields = { Mode = { type = String(length = 16) } PlayerChanged = { type = Bool default = false } EnemyChanged = { type = Bool default = false } Clears = { type = Int default = 0 } Callbacks = { type = Int default = 0 } } }
            action StartMode { role = player parameters = { mode = String(length = 16) } locals = { probe = Binding } body = { create = { type = StartProbe scope = frameScope() into = probe fields = { Mode = mode } } } }
            action StartPowers { role = player body = {
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = 3 } }
              call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:strength-power" stacks = 4 } }
            } }
            action StartBarricade { role = player body = {
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:barricade" stacks = 1 } }
              call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:barricade" stacks = 1 } }
            } }
            procedure ChangePlayer { parameters = { owner = Ref(Player) } body = {
              if = { condition = and(exists(type(StartProbe)), and(equal(field(first(type(StartProbe)), StartProbe.Mode), "snapshot"), not(field(first(type(StartProbe)), StartProbe.PlayerChanged)))) then = {
                call = { procedure = ApplyPlayerPower args = { owner = owner definition = "base-spire:strength-power" stacks = 2 } }
                call = { procedure = ApplyPlayerPower args = { owner = owner definition = "base-spire:dexterity-power" stacks = 2 } }
                set = { target = first(type(StartProbe)) field = StartProbe.PlayerChanged value = true }
              } }
            } }
            procedure ChangeEnemy { parameters = { owner = Ref(Player) } body = {
              if = { condition = and(exists(type(StartProbe)), and(equal(field(first(type(StartProbe)), StartProbe.Mode), "snapshot"), not(field(first(type(StartProbe)), StartProbe.EnemyChanged)))) then = {
                call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:strength-power" stacks = 2 } }
                call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:dexterity-power" stacks = 2 } }
                set = { target = first(type(StartProbe)) field = StartProbe.EnemyChanged value = true }
              } }
            } }
            subscription ChangePlayer { event = BeforePlayerSideTurnStart reaction = ChangePlayer }
            subscription ChangeEnemy { event = BeforeEnemySideTurnStart reaction = ChangeEnemy }
            # Tool-only clamp behavior probes first-veto selection; Anchor retains its real production identity/effect.
            procedure ClampQuery { pure = true parameters = { value = Bool player = Binding enemy = Binding listener = Ref(Relic) } result = Bool body = { return = { value = and(value, isNone(player)) } } }
            subscription ClampQuery { event = ShouldClearBlock reaction = ClampQuery instances = where(collection(Relics), r, and(equal(field(r, Relic.DefinitionId), "base-spire:anchor"), and(not(field(r, Relic.IsMelted)), and(exists(type(StartProbe)), equal(field(first(type(StartProbe)), StartProbe.Mode), "clamp"))))) instance_order = pureCall(RelicHookOrder, listener, true) }
            procedure ClampCheck { parameters = { check = Ref(BlockClearCheck) listener = Ref(Relic) } body = {
              if = { condition = and(field(check, BlockClearCheck.ShouldClear), not(isNone(field(check, BlockClearCheck.Player)))) then = {
                call = { procedure = RecordBlockClearPreventer args = { check = check power = none relic = listener } }
              } }
            } }
            subscription ClampCheck { event = BlockClearChecking reaction = ClampCheck instances = where(collection(Relics), r, and(equal(field(r, Relic.DefinitionId), "base-spire:anchor"), and(not(field(r, Relic.IsMelted)), and(exists(type(StartProbe)), equal(field(first(type(StartProbe)), StartProbe.Mode), "clamp"))))) instance_order = pureCall(RelicHookOrder, listener, true) }
            procedure ClampAfter { parameters = { check = Ref(BlockClearCheck) listener = Ref(Relic) } body = {
              set = { target = field(check, BlockClearCheck.Player) field = Player.Block value = minimum(10, field(field(check, BlockClearCheck.Player), Player.Block)) }
              set = { target = first(type(StartProbe)) field = StartProbe.Callbacks value = add(field(first(type(StartProbe)), StartProbe.Callbacks), 1) }
            } }
            subscription ClampAfter { event = AfterPreventingBlockClear reaction = ClampAfter instances = where(collection(Relics), r, and(equal(r, field(check, BlockClearCheck.PreventerRelic)), not(field(r, Relic.IsMelted)))) instance_order = pureCall(RelicHookOrder, listener, true) }
            procedure AuditCleared { parameters = { owner = Ref(Player) target = Ref(Enemy) } body = {
              if = { condition = and(exists(type(StartProbe)), equal(field(first(type(StartProbe)), StartProbe.Mode), "all-clear")) then = {
                if = { condition = exists(where(type(Enemy), e, not(equal(field(e, Enemy.Block), 0)))) then = { fail = { message = "All members must finish clearing before the first AfterBlockCleared" } } }
                set = { target = first(type(StartProbe)) field = StartProbe.Clears value = add(field(first(type(StartProbe)), StartProbe.Clears), 1) }
              } }
            } }
            subscription AuditCleared { event = AfterEnemyBlockCleared reaction = AuditCleared }
            procedure SpawnFlag { parameters = { owner = Ref(Player) } body = {
              if = { condition = and(exists(type(StartProbe)), equal(field(first(type(StartProbe)), StartProbe.Mode), "spawn")) then = { set = { target = first(type(Enemy)) field = Enemy.SpawnedThisTurn value = true } } }
            } }
            subscription SpawnFlag { event = AfterEnemySideTurnStart reaction = SpawnFlag }
            procedure PauseFirst { parameters = { owner = Ref(Player) target = Ref(Enemy) } locals = { answer = Bool } body = {
              if = { condition = and(exists(type(StartProbe)), and(equal(field(first(type(StartProbe)), StartProbe.Mode), "pause"), equal(target, first(type(Enemy))))) then = {
                decide = { participant = field(owner, Player.Owner) kind = Bool prompt = "turn eligibility probe" into = answer }
                set = { target = first(where(type(Enemy), e, not(equal(e, target)))) field = Enemy.SpawnedThisTurn value = true }
              } }
            } }
            subscription PauseFirst { event = EnemyTurnStarted reaction = PauseFirst priority = -10 }
            """);
        var rules = copy.Load();
        Session Setup(string mode)
        {
            var s = Start(rules); Submit(s, "TwigSlimeS.Spawn"); Submit(s, "TwigSlimeS.Spawn");
            var action = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:StartMode"); s.Submit(action, s.Participant(0), [Value.Of(mode)]);
            return s;
        }
        InstanceId Power(Session s, string definition, bool enemy) => Members(s, "Powers").Single(p =>
            TextField(s, p, "Power", "DefinitionId") == definition && s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex(enemy ? "HolderEnemy" : "HolderPlayer")) == Value.OfRef(enemy ? FirstTarget(s) : Player(s)));
        long Probe(Session s, string field) => IntField(s, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:StartProbe").Id).Single(), "StartProbe", field);
        check("TURN-START Power snapshots precede BeforeSide changes and newly applied Powers wait for the next own-side snapshot", () =>
        {
            var s = Setup("snapshot"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "StartPowers"); Submit(s, "BeginDevelopmentTurn");
            Equal(3L, IntField(s, Power(s, "base-spire:strength-power", false), "Power", "AmountAtTurnStart"), "player saved pre-hook amount");
            Equal(5L, PowerStacks(s, "base-spire:strength-power"), "hook changed current player amount");
            Equal(0L, IntField(s, Power(s, "base-spire:dexterity-power", false), "Power", "AmountAtTurnStart"), "new player Power has no retroactive snapshot");
            Submit(s, "EndDevelopmentTurn");
            Equal(4L, IntField(s, Power(s, "base-spire:strength-power", true), "Power", "AmountAtTurnStart"), "enemy pre-hook amount saved on enemy start");
            Equal(6L, PowerStacksFor(s, "base-spire:strength-power", FirstTarget(s)), "enemy hook changes current amount");
            Equal(0L, IntField(s, Power(s, "base-spire:dexterity-power", true), "Power", "AmountAtTurnStart"), "new enemy Power is not backfilled");
            Submit(s, "BeginDevelopmentTurn");
            Equal(5L, IntField(s, Power(s, "base-spire:strength-power", false), "Power", "AmountAtTurnStart"), "next player start snapshots the changed amount");
            Equal(2L, IntField(s, Power(s, "base-spire:dexterity-power", false), "Power", "AmountAtTurnStart"), "next own turn includes the new Power");
        });
        check("TURN-START first player turn skips the veto callback, later first veto owns the clamp callback", () =>
        {
            var s = Setup("clamp"); Submit(s, "Anchor.Obtain"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal(10L, Block(s), "real Anchor retained on first player turn"); Equal(0L, Probe(s, "Callbacks"), "first turn does not ask to clear");
            Set(s, Player(s), "Player", "Block", Value.Of(40L)); Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(10L, Block(s), "tool clamp callback reduces retained block after both attacks"); Equal(1L, Probe(s, "Callbacks"), "exact selected instance notified once");
            var barricade = Setup("clamp"); Submit(barricade, "Anchor.Obtain"); Submit(barricade, "BeginDevelopmentCombat"); Submit(barricade, "StartBarricade"); Submit(barricade, "BeginDevelopmentTurn");
            Set(barricade, Player(barricade), "Player", "Block", Value.Of(40L)); Submit(barricade, "EndDevelopmentTurn"); Submit(barricade, "BeginDevelopmentTurn");
            Equal(32L, Block(barricade), "Direct Power veto precedes earlier-acquired relic veto"); Equal(0L, Probe(barricade, "Callbacks"), "later veto cannot acquire the callback");
            Equal(0, barricade.State.AllInstances(barricade.RuleSet.TypeByQualifiedId("base-spire:BlockClearCheck").Id).Count(), "temporary query records cleaned");
        });
        check("TURN-START all side members clear before notifications, including zero HP not yet detached", () =>
        {
            var s = Setup("all-clear"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            foreach (var e in Targets(s)) Set(s, e, "Enemy", "Block", Value.Of(15L));
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(0L)); Submit(s, "EndDevelopmentTurn");
            Equal(2L, Probe(s, "Clears"), "zero HP member participates in clear and notification"); Equal(76L, Hp(s), "zero HP member does not act");
        });
        check("TURN-START enemy Barricade retains its own block while the other enemy clears", () =>
        {
            var s = Setup("plain"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "StartBarricade"); Submit(s, "BeginDevelopmentTurn");
            foreach (var e in Targets(s)) Set(s, e, "Enemy", "Block", Value.Of(15L)); Submit(s, "EndDevelopmentTurn");
            Equal(15L, IntField(s, Targets(s)[0], "Enemy", "Block"), "enemy owner vetoes clearing"); Equal(0L, IntField(s, Targets(s)[1], "Enemy", "Block"), "other owner does not share veto");
        });
        check("TURN-START side switch clears spawn flags, AfterSide marking skips only that actor", () =>
        {
            var s = Setup("spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(76L, Hp(s), "only second actor performs after the controlled spawn flag");
            True(!s.State.GetField(FirstTarget(s), s.RuleSet.TypeByQualifiedId("base-spire:Enemy").FieldIndex("SpawnedThisTurn")).AsBool(), "return side switch clears flag");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn"); Equal(72L, Hp(s), "saved next phase uses fresh eligibility");
        });
        check("TURN-START paused first actor resumes once and later actor rechecks spawn eligibility", () =>
        {
            var s = Setup("pause"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
            Equal(SessionStatus.AwaitingDecision, s.Status, "first actor suspended before body"); Equal(80L, Hp(s), "no attack before answering");
            var request = s.PendingDecision!; s.Answer(new DecisionAnswer(request.RequestId, Flag: true)); Healthy(s);
            Equal(76L, Hp(s), "one resumed attack and later marked actor skipped");
            var before = s.State.StateHash(); Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request.RequestId, Flag: true))); Equal(before, s.State.StateHash(), "duplicate answer does not resume twice");
        });
    }
}
