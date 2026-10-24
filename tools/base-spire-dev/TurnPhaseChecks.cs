using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunTurnPhaseChecks(DslPackage package, Action<string, Action> check)
    {
        check("TURN-PHASE energy reset sees the previous turn and hand draw precedes player Early normal Late and side start", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("phase-start-probe.spire", """
                entity PhaseTrace { fields = { Text = { type = String(length = 1024) default = "" } } }
                procedure Mark { parameters = { owner = Ref(Player) tag = String(length = 32) } body = {
                  set = { target = first(type(PhaseTrace)) field = PhaseTrace.Text value = concat(field(first(type(PhaseTrace)), PhaseTrace.Text), tag) }
                } }
                procedure Energy { parameters = { owner = Ref(Player) } body = {
                  if = { condition = not(and(equal(field(owner, Player.TurnNumber), 0), equal(field(owner, Player.Energy), 3))) then = { fail = { message = "energy reset must precede turn increment" } } }
                  call = { procedure = Mark args = { owner = owner tag = "energy>" } }
                } }
                procedure Hand { parameters = { owner = Ref(Player) } body = {
                  if = { condition = not(and(equal(field(owner, Player.TurnNumber), 1), equal(count(collection(Hand)), 0))) then = { fail = { message = "BeforeHandDraw must see incremented turn and no drawn hand" } } }
                  call = { procedure = Mark args = { owner = owner tag = "hand>" } }
                } }
                procedure DrawEarly { parameters = { owner = Ref(Player) card = Ref(Card) } body = { call = { procedure = Mark args = { owner = owner tag = "drawEarly>" } } } }
                procedure Draw { parameters = { owner = Ref(Player) card = Ref(Card) } body = { call = { procedure = Mark args = { owner = owner tag = "draw>" } } } }
                procedure Early { parameters = { owner = Ref(Player) } body = {
                  if = { condition = not(and(equal(count(collection(Hand)), 1), equal(field(owner, Player.Energy), 3))) then = { fail = { message = "Early must follow draw and precede Lantern" } } }
                  call = { procedure = Mark args = { owner = owner tag = "early>" } }
                } }
                procedure Normal { parameters = { owner = Ref(Player) } body = { call = { procedure = Mark args = { owner = owner tag = "normal>" } } } }
                procedure Late { parameters = { owner = Ref(Player) } body = {
                  if = { condition = not(equal(field(owner, Player.Hp), 72)) then = { fail = { message = "BloodVial must run in Late after the starting hand" } } }
                  call = { procedure = Mark args = { owner = owner tag = "late>" } }
                } }
                procedure Side { parameters = { owner = Ref(Player) } body = {
                  if = { condition = not(equal(field(owner, Player.Energy), 4)) then = { fail = { message = "Lantern belongs to side start after player start" } } }
                  call = { procedure = Mark args = { owner = owner tag = "side" } }
                } }
                subscription Energy { event = AfterEnergyReset reaction = Energy priority = 10 }
                subscription Hand { event = BeforeHandDraw reaction = Hand }
                subscription DrawEarly { event = AfterCardDrawnEarly reaction = DrawEarly }
                subscription Draw { event = AfterCardDrawn reaction = Draw }
                subscription Early { event = AfterPlayerTurnStartEarly reaction = Early }
                subscription Normal { event = AfterPlayerTurnStart reaction = Normal }
                subscription Late { event = AfterPlayerTurnStartLate reaction = Late priority = 10 }
                subscription Side { event = AfterPlayerSideTurnStart reaction = Side priority = 10 }
                action PhaseTrace { role = player locals = { trace = Binding } body = { create = { type = PhaseTrace scope = frameScope() into = trace } } }
                """);
            var s = Start(copy.Load()); Submit(s, "PhaseTrace"); Submit(s, "Lantern.Obtain"); Submit(s, "BloodVial.Obtain"); Submit(s, "AddDevelopmentTarget");
            Set(s, Player(s), "Player", "Hp", Value.Of(70L)); Set(s, Player(s), "Player", "AutoDrawPerTurn", Value.Of(1L)); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var trace = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:PhaseTrace").Id).Single();
            Equal("energy>hand>drawEarly>draw>early>normal>late>side", TextField(s, trace, "PhaseTrace", "Text"), "all starting phases have separate ordered snapshots");
        });
        check("TURN-PHASE Orichalcum snapshots each duplicate and removed or melted instances cannot redeem captured block", () =>
        {
            foreach (var mode in new[] { "duplicate", "remove", "melt" })
            {
                using var copy = new Copy(package.Root); copy.Include("phase-eligibility.spire", """
                    action ExtraOrichalcum { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:orichalcum" } } } }
                    procedure CancelOrichalcum { parameters = { owner = Ref(Player) } body = { MUTATION } }
                    subscription CancelOrichalcum { event = BeforePlayerSideTurnEnd reaction = CancelOrichalcum priority = -1 }
                    """.Replace("MUTATION", mode switch {
                        "remove" => "call = { procedure = RemoveRelic args = { instance = first(collection(Relics)) } }",
                        "melt" => "set = { target = first(collection(Relics)) field = Relic.IsMelted value = true }",
                        _ => "" }));
                var s = Start(copy.Load()); Submit(s, "Orichalcum.Obtain"); if (mode == "duplicate") Submit(s, "ExtraOrichalcum");
                Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn");
                Equal(mode == "duplicate" ? 12L : 0L, Block(s), "each captured instance is revalidated before redemption");
                if (mode == "remove") Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Orichalcum.State").Id).Count, "removed eligibility state is destroyed");
                if (mode == "duplicate")
                {
                    Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                    Set(s, Player(s), "Player", "Block", Value.Of(2L)); Submit(s, "EndDevelopmentTurn"); Equal(2L, Block(s), "new combat turn cannot reuse old eligibility");
                }
            }
        });
        check("TURN-PHASE Orichalcum captures zero block before Plating and grants eleven before real enemy damage", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("phase-block-probe.spire", """
                action PhasePlating { role = player body = {
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:plating-power" stacks = 5 } }
                  call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:strength-power" stacks = 10 } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "Orichalcum.Obtain"); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "MaxHp", Value.Of(1000L)); Set(s, Player(s), "Player", "Hp", Value.Of(1000L)); Submit(s, "PhasePlating"); Submit(s, "EndDevelopmentTurn");
            Equal(11L, IntField(s, Members(s, "DamageResults").Single(), "DamageResult", "Blocked"), "source Early records Orichalcum before Plating5, normal stage adds six");
            Equal(997L, Hp(s), "real fourteen-damage attack consumes eleven block");
        });
        check("TURN-PHASE Charon last kill precedes DarkEmbrace and ending draw spends no RNG", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("phase-exhaust-probe.spire", """
                action PhaseDarkEmbrace { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:dark-embrace-power" stacks = 1 } } } }
                action PhaseExhaust { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = {
                  set = { target = first(type(Player)) field = Player.EffectDepth value = add(field(first(type(Player)), Player.EffectDepth), 1) }
                  call = { procedure = ExhaustCombatCard args = { owner = first(type(Player)) card = card } }
                  set = { target = first(type(Player)) field = Player.EffectDepth value = subtract(field(first(type(Player)), Player.EffectDepth), 1) }
                } }
                action PhaseSettle { role = player body = { call = { procedure = CheckCombatVictory args = { owner = first(type(Player)) } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "CharonsAshes.Obtain"); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "PhaseDarkEmbrace");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(3L)); var top = IcAdd(s, "StrikeIronclad"); IcMove(s, top, "Hand", "DrawPile"); var victim = IcAdd(s, "DefendIronclad");
            var draws = s.Random.TotalDraws; Submit(s, "PhaseExhaust", victim);
            Equal(0L, IntField(s, FirstTarget(s), "Enemy", "Hp"), "real Charon damage killed the last enemy"); Equal(0, Hand(s).Count, "source inventory listener kills before Power draw");
            True(Members(s, "DrawPile").Contains(top), "ending draw does not move the pending top card"); Equal(draws, s.Random.TotalDraws, "ending draw never shuffles or samples before reward settlement");
            Submit(s, "PhaseSettle"); Equal("victory", Outcome(s), "outer effect settles the real kill");
        });
    }
}
