using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCombatEndChecks(DslPackage package, Action<string, Action> check)
    {
        check("END victory resets source end consumers once; defeat retains their game-over state", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("end-audit.spire", """
                entity EndAudit { fields = { Ends = { type = Int default = 0 } Commits = { type = Int default = 0 } Victories = { type = Int default = 0 } } }
                action EndInitialize { role = player locals = { audit = Binding } body = { create = { type = EndAudit scope = frameScope() into = audit } } }
                procedure SourceEnd { parameters = { owner = Ref(Player) } body = {
                  set = { target = first(type(EndAudit)) field = EndAudit.Ends value = add(field(first(type(EndAudit)), EndAudit.Ends), 1) }
                  if = { condition = not(isNone(field(owner, Player.CurrentRewardSet))) then = { fail = { message = "End hooks precede rewards" } } }
                  call = { procedure = CheckCombatVictory args = { owner = owner } }
                  call = { procedure = CheckCombatDefeat args = { owner = owner } }
                } }
                subscription SourceEnd { event = AfterCombatEnd reaction = SourceEnd priority = 200 }
                procedure Committed { parameters = { owner = Ref(Player) victory = Bool } body = { set = { target = first(type(EndAudit)) field = EndAudit.Commits value = add(field(first(type(EndAudit)), EndAudit.Commits), 1) } } }
                subscription Committed { event = CombatEnded reaction = Committed }
                procedure Victory { parameters = { owner = Ref(Player) victory = Bool } body = {
                  if = { condition = not(equal(field(first(type(EndAudit)), EndAudit.Ends), 1)) then = { fail = { message = "Source end must finish before victory" } } }
                  set = { target = first(type(EndAudit)) field = EndAudit.Victories value = add(field(first(type(EndAudit)), EndAudit.Victories), 1) }
                } }
                subscription Victory { event = CombatVictory reaction = Victory }
                action EndRepeat { role = player body = {
                  if = { condition = or(pureCall(CombatIsEnding), not(pureCall(CombatIsOverOrEnding))) then = { fail = { message = "Committed outcome is over, not pending ending" } } } call = { procedure = CheckCombatVictory args = { owner = first(type(Player)) } } call = { procedure = CheckCombatDefeat args = { owner = first(type(Player)) } } } }
                action EndMutual { role = player body = {
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:thorns-power" stacks = 4 } }
                  call = { procedure = EnemyAttack args = { attacker = first(type(Enemy)) defender = first(type(Player)) amount = 6 hits = 2 } }
                } }
                action EndLose { role = player body = { call = { procedure = LosePlayerHp args = { owner = first(type(Player)) amount = 999 } } } }
                """);
            foreach (var win in new[] { true, false })
            {
                var s = Start(copy.Load()); Submit(s, "EndInitialize");
                foreach (var relic in new[] { "ArtOfWar", "CentennialPuzzle", "Kunai", "Shuriken", "OrnamentalFan", "StoneCalendar", "LetterOpener", "Nunchaku", "PenNib" }) Submit(s, relic + ".Obtain");
                Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat");
                // Controlled seventh-turn setup arms the real StoneCalendar consumer without six unrelated turns.
                Set(s, Player(s), "Player", "TurnNumber", Value.Of(6L)); Submit(s, "BeginDevelopmentTurn");
                Submit(s, "Bloodletting.AddToHand"); IcPlay(s, Hand(s).Last());
                IcPlay(s, IcAdd(s, "StrikeIronclad"));
                InstanceId Relic(string id) => Members(s, "Relics").Single(r => TextField(s, r, "Relic", "DefinitionId") == "base-spire:" + id);
                bool Flag(string type, string field) { var t = s.RuleSet.TypeByQualifiedId("base-spire:" + type); return s.State.GetField(s.State.AllInstances(t.Id).Single(), t.FieldIndex(field)).AsBool(); }
                long Counter(string id) => IntField(s, Relic(id), "Relic", "Counter");
                True(Flag("ArtOfWar.State", "ThisTurn") && Flag("StoneCalendar.State", "Armed"), "real turn and card consumers have pending state");
                True(s.State.GetField(Relic("centennial-puzzle"), s.RuleSet.TypeByQualifiedId("base-spire:Relic").FieldIndex("UsedThisCombat")).AsBool(), "real self-harm spent the puzzle trigger");
                foreach (var id in new[] { "kunai", "shuriken", "ornamental-fan" }) Equal(1L, Counter(id), "one real attack counted before end");
                if (win) { Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(6L)); IcPlay(s, IcAdd(s, "StrikeIronclad")); }
                else Submit(s, "EndLose");
                Equal(win ? "victory" : "defeat", Outcome(s), "real outcome");
                foreach (var id in new[] { "kunai", "shuriken", "ornamental-fan" }) Equal(win ? 0L : 1L, Counter(id), "source victory-only end resets");
                Equal(!win, Flag("ArtOfWar.State", "ThisTurn"), "ArtOfWar end reset"); Equal(!win, Flag("StoneCalendar.State", "Armed"), "StoneCalendar end reset");
                Equal(!win, s.State.GetField(Relic("centennial-puzzle"), s.RuleSet.TypeByQualifiedId("base-spire:Relic").FieldIndex("UsedThisCombat")).AsBool(), "Puzzle resets at victory, not next start");
                Equal(1L, Counter("letter-opener"), "LetterOpener has no end reset"); Equal(win ? 2L : 1L, Counter("nunchaku"), "persistent Nunchaku counter survives end");
                var audit = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EndAudit").Id).Single();
                Equal(win ? 1L : 0L, IntField(s, audit, "EndAudit", "Ends"), "victory-only source end"); Equal(1L, IntField(s, audit, "EndAudit", "Commits"), "one commit"); Equal(win ? 1L : 0L, IntField(s, audit, "EndAudit", "Victories"), "victory-only notification");
                var state = s.State.StateHash(); Submit(s, "EndRepeat"); Equal(state, s.State.StateHash(), "repeat result checks are state-idempotent");
            }
            var mutual = Start(copy.Load()); Submit(mutual, "EndInitialize"); Submit(mutual, "GremlinHorn.Obtain"); Submit(mutual, "AddDevelopmentTarget"); Submit(mutual, "BeginDevelopmentCombat"); Submit(mutual, "BeginDevelopmentTurn");
            Set(mutual, Player(mutual), "Player", "Hp", Value.Of(1L)); Set(mutual, FirstTarget(mutual), "Enemy", "Hp", Value.Of(3L));
            Submit(mutual, "EndMutual");
            Equal(0L, Hp(mutual), "original hit still kills player after retaliation"); Equal(0L, EnemyHp(mutual), "retaliation kills the final dealer"); Equal("defeat", Outcome(mutual), "failure wins after the whole nested action");
            Equal(4L, Energy(mutual), "GremlinHorn energy remains allowed during pending ending"); Equal(0, Hand(mutual).Count, "GremlinHorn draw refuses pending ending");
            var end = mutual.State.AllInstances(mutual.RuleSet.TypeByQualifiedId("base-spire:EndAudit").Id).Single();
            Equal(0L, IntField(mutual, end, "EndAudit", "Ends"), "mutual death cannot enter source victory end"); Equal(0L, IntField(mutual, end, "EndAudit", "Victories"), "mutual death has no victory"); Equal(1L, IntField(mutual, end, "EndAudit", "Commits"), "mutual death commits once");
            True(!Legal(mutual, "BeginDevelopmentTurn") && !Legal(mutual, "ClaimDevelopmentGoldReward"), "mutual death exposes no turn or rewards");
        });
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        Case("CP45: killing the last enemy ends the combat in victory and closes the turn", () =>
        {
            var s = Start(package); Submit(s, "Bludgeon.AddToHand"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(8L, EnemyHp(s), "the surviving target keeps the combat open"); Equal("none", Outcome(s), "the combat is not over");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(5L));
            Submit(s, "Bludgeon.AddToHand"); Set(s, Player(s), "Player", "Energy", Value.Of(3L));
            Submit(s, "PlayAttackFromHand", Hand(s).Single());
            Equal(0L, EnemyHp(s), "the last target died"); Equal("victory", Outcome(s), "the combat is won");
            True(!Active(s), "the combat is closed"); True(!Legal(s, "PlayAttackFromHand"), "no further plays are offered");
            True(!Legal(s, "BeginDevelopmentTurn"), "no further turns are offered");
            True(!Legal(s, "BeginDevelopmentCombat"), "rewards must close before the next combat");
            Submit(s, "CloseDevelopmentRewards");
            True(Legal(s, "BeginDevelopmentCombat"), "a new development combat may start after rewards close");
        });
        Case("CP46: reaching zero HP ends the combat in defeat even without enemies", () =>
        {
            var s = Start(package); Submit(s, "Bloodletting.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Hp", Value.Of(2L));
            Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal(0L, Hp(s), "the HP floor is zero"); Equal("defeat", Outcome(s), "the combat is lost");
            True(!Active(s), "the combat is closed"); True(!Legal(s, "PlayCardFromHand"), "no further plays are offered");
        });
        Case("CP47: a new development combat resets the previous outcome", () =>
        {
            var s = Start(package); Submit(s, "Bloodletting.AddToHand");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Hp", Value.Of(2L)); Submit(s, "PlayCardFromHand", Hand(s).Single());
            Equal("defeat", Outcome(s), "first combat lost");
            Set(s, Player(s), "Player", "Hp", Value.Of(80L));
            Submit(s, "BeginDevelopmentCombat");
            Equal("none", Outcome(s), "the outcome is cleared for the new combat"); True(Active(s), "the new combat runs");
            Submit(s, "BeginDevelopmentTurn"); Equal(1L, Turn(s), "the new combat starts at turn one");
        });

        var all = new List<string>(witnesses);
        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:bludgeon"] = all.ToArray() };
    }

    private static string Outcome(Session s) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("CombatOutcome")).AsString();
}
