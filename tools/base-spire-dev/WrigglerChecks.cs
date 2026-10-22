using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunWrigglerChecks(DslPackage package, Action<string, Action> check)
    {
        using var copy = new Copy(package.Root);
        copy.Include("wriggler-probes.spire", """
            entity InfectionAudit { fields = { Trace = { type = String(length = 256) default = "" } Ends = { type = Int default = 0 } Discards = { type = Int default = 0 } } }
            action Audit { role = player locals = { audit = Binding } body = { create = { type = InfectionAudit scope = frameScope() into = audit } forEach = { query = type(Card) item = c body = { call = { procedure = DestroyCombatCard args = { card = c } } } } } }
            procedure AuditDamage { parameters = { result = Ref(DamageResult) } body = {
              if = { condition = and(not(isNone(field(result, DamageResult.Card))), equal(field(field(result, DamageResult.Card), Card.DefinitionId), "base-spire:infection")) then = {
                if = { condition = or(field(result, DamageResult.Powered), or(field(result, DamageResult.Unblockable), or(not(isNone(field(result, DamageResult.Play))), not(equal(field(result, DamageResult.DealerPlayer), first(type(Player))))))) then = { fail = { message = "Infection damage context" } } }
                set = { target = first(type(InfectionAudit)) field = InfectionAudit.Trace value = concat(field(first(type(InfectionAudit)), InfectionAudit.Trace), field(field(result, DamageResult.Card), Card.Label)) }
              } }
            } }
            subscription AuditDamage { event = DamageResolved reaction = AuditDamage }
            procedure AuditEnd { parameters = { owner = Ref(Player) } body = { set = { target = first(type(InfectionAudit)) field = InfectionAudit.Ends value = add(field(first(type(InfectionAudit)), InfectionAudit.Ends), 1) } } }
            subscription AuditEnd { event = AfterTurnEnded reaction = AuditEnd }
            procedure AuditDiscard { parameters = { owner = Ref(Player) card = Ref(Card) } body = { set = { target = first(type(InfectionAudit)) field = InfectionAudit.Discards value = add(field(first(type(InfectionAudit)), InfectionAudit.Discards), 1) } } }
            subscription AuditDiscard { event = CardDiscarded reaction = AuditDiscard }
            procedure TestKeyword { pure = true parameters = { value = Bool card = Ref(Card) keyword = String(length = 16) } result = Bool body = {
              return = { value = or(value, or(and(equal(field(card, Card.Label), "retained"), equal(keyword, "retain")), and(or(equal(field(card, Card.Label), "ethereal"), equal(field(card, Card.Label), "draw-trigger")), equal(keyword, "ethereal")))) }
            } }
            subscription TestKeyword { event = ModifyCardKeyword reaction = TestKeyword }
            procedure DrawOnExhaust { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
              if = { condition = equal(field(card, Card.Label), "draw-trigger") then = { call = { procedure = DrawCards args = { owner = owner count = 1 } } } }
            } }
            subscription DrawOnExhaust { event = CardExhausted reaction = DrawOnExhaust }
            action Stunned { role = player locals = { enemy = Binding } body = { call = { procedure = Wriggler.Create args = { slot = "wriggler2" startStunned = true } into = enemy } } }
            action InvalidSlot { role = player locals = { enemy = Binding } body = { call = { procedure = Wriggler.Create args = { slot = "unknown" startStunned = false } into = enemy } } }
            action RemoveEnemies { role = player body = { forEach = { query = type(Enemy) item = e body = { call = { procedure = RemoveCombatEnemy args = { target = e } } } } } }
            action CloneFirst { role = player locals = { card = Binding } body = {
              call = { procedure = AddCardCostModifier args = { card = first(collection(Hand)) source = "test" operation = "set" amount = 0 lifetime = "combat" } }
              call = { procedure = AddCardCostModifier args = { card = first(collection(Hand)) source = "test" operation = "add" amount = 2 lifetime = "turn" } }
              call = { procedure = CloneCombatCard args = { owner = first(type(Player)) source = first(collection(Hand)) } into = card }
              call = { procedure = AddGeneratedToHand args = { card = card } }
            } }
            action AutoFirst { role = player body = { call = { procedure = AutoPlayCard args = { owner = first(type(Player)) card = first(collection(Hand)) forceExhaust = false } } } }
            action ExhaustAutoFirst { role = player body = { call = { procedure = AutoPlayCard args = { owner = first(type(Player)) card = first(collection(Hand)) forceExhaust = true } } } }
            action DiscardFirst { role = player body = { call = { procedure = DiscardCombatCard args = { owner = first(type(Player)) card = first(collection(Hand)) } } } }
            action DrawFirst { role = player body = { call = { procedure = MoveCombatCard args = { owner = first(type(Player)) card = first(collection(Hand)) to = "draw-top" reason = "test" } } } }
            action ExhaustFirst { role = player body = { call = { procedure = ExhaustCombatCard args = { owner = first(type(Player)) card = first(collection(Hand)) } } } }
            action DamagePowers { role = player body = {
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = 20 } }
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:weak-power" stacks = 2 } }
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:vulnerable-power" stacks = 2 } }
            } }
            """);
        var rules = copy.Load(); var names = new List<string>();
        void Case(string name, Action body) { check(name, body); names.Add(name); }
        Session New()
        {
            var s = Start(rules); Submit(s, "Audit"); Submit(s, "BattleFriendV1.Spawn");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); return s;
        }
        InstanceId Audit(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:InfectionAudit").Id).Single();
        InstanceId Add(Session s, string label = "I") { Submit(s, "Infection.AddToHand"); var c = Hand(s).Last(); Set(s, c, "Card", "Label", Value.Of(label)); return c; }
        void Turn(Session s) { Submit(s, "EndDevelopmentTurn"); if (Active(s)) Submit(s, "BeginDevelopmentTurn"); }

        Case("WRIGGLER four real slots alternate three turns with distinct HP and actual status/Strength", () =>
        {
            var s = Start(rules); Submit(s, "Audit"); Submit(s, "Wriggler.Spawn");
            var enemies = Targets(s); Equal(4, enemies.Count, "four source slots");
            var hp = enemies.Select(e => IntField(s, e, "Enemy", "MaxHp")).ToArray();
            True(hp.All(x => x is >= 17 and <= 21) && hp.Distinct().Count() == 4, "without-replacement HP candidates");
            Equal(4L, s.Random.DrawCount("base-spire:run:niche"), "one HP word per birth");
            Set(s, Player(s), "Player", "MaxHp", Value.Of(1000L)); Set(s, Player(s), "Player", "Hp", Value.Of(1000L));
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            for (var turn = 0; turn <= 3; turn++)
            {
                for (var i = 0; i < enemies.Count; i++)
                {
                    var bite = (i + turn) % 2 == 0;
                    Equal(bite ? "NASTY_BITE_MOVE" : "WRIGGLE_MOVE", MoveText(s, enemies[i], "Enemy", "MoveId"), "saved slot move");
                    Equal(bite ? "attack" : "buff,status", MoveText(s, enemies[i], "Enemy", "Intent"), "ordered source intents");
                    Equal(turn + 1L, IntField(s, MoveState(s, enemies[i]), "EnemyMoveState", "Sequence"), "only real moves in history");
                }
                if (turn == 3) break;
                Turn(s); Equal(new long[] { 988, 972, 956 }[turn], Hp(s), "real powered bite after Strength");
                Equal((turn + 1) * 2, Members(s, "DiscardPile").Count(c => CardDefinition(s, c) == "base-spire:infection"), "one real status per wriggle");
            }
            var before = s.State.StateHash(); var draws = s.Random.TotalDraws;
            s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
            Equal(before, s.State.StateHash(), "queries do not reselect"); Equal(draws, s.Random.TotalDraws, "queries draw nothing"); Equal(0L, s.Random.DrawCount(MonsterAi), "fixed/conditional graph never draws AI");
        });
        Case("WRIGGLER StartStunned resolves once then the actual slot and removes private state", () =>
        {
            var s = Start(rules); Submit(s, "Audit"); Submit(s, "Stunned"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal("SPAWNED_MOVE", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "real spawned move"); var hp = Hp(s);
            Turn(s); Equal(hp, Hp(s), "first turn is empty"); Equal("WRIGGLE_MOVE", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "slot 2 initialized after stun");
            Turn(s); Equal(1, Members(s, "DiscardPile").Count, "next turn generates infection");
            Turn(s); Equal(hp - 8, Hp(s), "third turn bites with Strength");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "RemoveEnemies");
            foreach (var type in new[] { "Enemy", "EnemyMoveState", "EnemyMoveHistory", "Wriggler.State", "Power" })
                Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).Count(), "full removal: " + type);
            Submit(s, "Wriggler.Spawn"); Submit(s, "BeginDevelopmentCombat"); Equal("NASTY_BITE_MOVE", MoveText(s, FirstTarget(s), "Enemy", "MoveId"), "new instance has fresh slot state");
        });
        Case("WRIGGLER exhausted HP range still consumes once and invalid slot draws nothing", () =>
        {
            var s = Start(rules); Submit(s, "Audit"); Submit(s, "Wriggler.Spawn"); Submit(s, "Wriggler.Spawn");
            Equal(8L, s.Random.DrawCount("base-spire:run:niche"), "fallback also draws once");
            Equal(5, Targets(s).Take(5).Select(e => IntField(s, e, "Enemy", "MaxHp")).Distinct().Count(), "all candidates used before fallback");
            var bad = Start(rules); var before = bad.Random.TotalDraws;
            var action = bad.LegalActions(bad.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:InvalidSlot");
            Throws<EngineFaultException>(() => bad.Submit(action.Definition, bad.Participant(0), []));
            Equal(SessionStatus.Faulted, bad.Status, "unknown slot fails"); Equal(before, bad.Random.TotalDraws, "support check precedes HP");
        });
        Case("WRIGGLER conditional nodes reject missing/no-match dispatch and bound transition cycles", () =>
        {
            foreach (var mode in new[] { "missing", "none", "loop" })
            {
                using var invalid = new Copy(package.Root); var path = invalid.Path("enemies/shared/wriggler.spire"); var text = File.ReadAllText(path);
                text = mode switch
                {
                    "missing" => text.Replace("dispatchCase Condition { dispatch = ResolveEnemyMoveCondition key = \"base-spire:wriggler\" procedure = Condition }", "", StringComparison.Ordinal),
                    "none" => text.Replace("equal(field(own, State.Slot), \"wriggler1\")", "false", StringComparison.Ordinal),
                    _ => text.Replace("field(state, EnemyMoveState.Graph), \"NASTY_BITE_MOVE\")", "field(state, EnemyMoveState.Graph), \"START\")", StringComparison.Ordinal),
                };
                File.WriteAllText(path, text); var s = invalid.Load().CreateSession(1, ["player"]);
                if (mode == "missing")
                {
                    Throws<EngineFaultException>(s.Start); True(s.Fault!.Message.Contains("no condition implementation", StringComparison.Ordinal), "named assembly failure");
                    Equal(0L, s.Random.TotalDraws, "assembly does not draw");
                }
                else
                {
                    s.Start(); Throws<EngineFaultException>(() => Submit(s, "Wriggler.Spawn"));
                    Equal(1L, s.Random.DrawCount("base-spire:run:niche"), "only the actual first HP draw"); Equal(0L, s.Random.DrawCount(MonsterAi), "no conditional fallback draw");
                    if (mode == "loop") Equal(EngineFaultCode.BudgetExceeded, s.Fault!.Code, "conditional recursion uses existing budget");
                    else True(s.Fault!.Message.Contains("no legal condition", StringComparison.Ordinal), "named no-match failure");
                }
            }
        });
        Case("INFECTION -1 survives cost modifiers, cloning and permanent combat copying; manual rejection is atomic", () =>
        {
            var s = New(); var card = Add(s); Submit(s, "CloneFirst");
            foreach (var c in Hand(s))
            {
                Equal(-1L, CardCost(s, c), "source negative cost"); Equal(-1L, IntField(s, c, "Card", "BaseCost"), "negative base cost");
                True(!LegalFor(s, "UpgradeCardInHand", c) && !LegalFor(s, "PlayCardFromHand", c), "no manual/upgrade candidate");
                Equal("status", MoveText(s, c, "Card", "Kind"), "source kind"); Equal("status", MoveText(s, c, "Card", "Rarity"), "source rarity");
            }
            var before = s.State.StateHash(); var rng = s.Random.TotalDraws;
            Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:PlayCardFromHand"), s.Participant(0), [Value.OfRef(card)]));
            Equal(before, s.State.StateHash(), "manual rejection leaves piles/energy/history unchanged"); Equal(rng, s.Random.TotalDraws, "manual rejection never draws");
            var permanent = Start(rules); Submit(permanent, "Audit"); Submit(permanent, "Infection.Obtain"); Submit(permanent, "BattleFriendV1.Spawn"); Submit(permanent, "BeginDevelopmentCombat");
            True(Members(permanent, "DrawPile").All(c => CardCost(permanent, c) == -1), "permanent combat copy keeps sentinel");
            var old = Members(permanent, "DrawPile").Single(); Submit(permanent, "EndDevelopmentCombat"); Submit(permanent, "BeginDevelopmentCombat");
            True(!Members(permanent, "DrawPile").Contains(old) && Members(permanent, "DrawPile").Count == 1, "new combat clears the old status instance and copies the permanent source");
        });
        foreach (var exhaust in new[] { false, true })
            Case("INFECTION autoplay rejection moves to " + (exhaust ? "exhaust" : "discard") + " without body, energy or play history", () =>
            {
                var s = New(); var card = Add(s); var hp = Hp(s); var energy = Energy(s); var draws = s.Random.TotalDraws;
                Submit(s, exhaust ? "ExhaustAutoFirst" : "AutoFirst");
                True(Members(s, exhaust ? "ExhaustPile" : "DiscardPile").Contains(card), "actual result movement");
                Equal(hp, Hp(s), "no body or in-hand damage"); Equal(energy, Energy(s), "no payment"); Equal(0, Members(s, "CardPlays").Count, "no genuine play record"); Equal(draws, s.Random.TotalDraws, "no target draw");
            });
        Case("INFECTION two in-hand instances use ordered, blockable unpowered card-source damage", () =>
        {
            var s = New(); var a = Add(s, "A"); var b = Add(s, "B"); Set(s, Player(s), "Player", "Block", Value.Of(4L));
            Submit(s, "DamagePowers"); var hp = Hp(s);
            Submit(s, "EndDevelopmentTurn"); Equal(hp - 2, Hp(s), "six raw damage consumes four block"); Equal(0L, IntField(s, Player(s), "Player", "Block"), "block consumed");
            Equal("AB", MoveText(s, Audit(s), "InfectionAudit", "Trace"), "original hand order");
            True(Members(s, "DiscardPile").SequenceEqual(new[] { a, b }), "ordered result pile"); Equal(0L, IntField(s, Audit(s), "InfectionAudit", "Discards"), "not active discard"); Equal(0, Members(s, "CardPlays").Count, "not actual play");
        });
        Case("INFECTION retained effect leaves hand; ethereal effect executes before exhausting", () =>
        {
            var s = New(); var a = Add(s, "retained"); var b = Add(s, "ethereal"); var hp = Hp(s);
            Submit(s, "EndDevelopmentTurn"); Equal(hp - 6, Hp(s), "both effect instances run");
            True(Members(s, "DiscardPile").Contains(a) && Members(s, "ExhaustPile").Contains(b) && Hand(s).Count == 0, "effect result ignores Retain, honors Ethereal");
        });
        Case("INFECTION only initial hand effects run; an ethereal exhaust draw does not enter snapshot", () =>
        {
            var s = New(); Add(s, "late"); Submit(s, "DrawFirst"); var initial = Add(s, "early");
            Submit(s, "Slimed.AddToHand"); var trigger = Hand(s).Last(); Set(s, trigger, "Card", "Label", Value.Of("draw-trigger"));
            var hp = Hp(s); Submit(s, "EndDevelopmentTurn");
            Equal(hp - 3, Hp(s), "only initial infection damages"); Equal("early", MoveText(s, Audit(s), "InfectionAudit", "Trace"), "frozen effect list");
            True(Members(s, "DiscardPile").Contains(initial) && Members(s, "ExhaustPile").Contains(trigger), "ethereal precedes initial effects");
            True(Members(s, "DiscardPile").Any(c => MoveText(s, c, "Card", "Label") == "late"), "newly drawn card reaches ordinary flush without effect");
        });
        Case("INFECTION death guards stop result movement, ordinary flush and enemy turn", () =>
        {
            var s = New(); var a = Add(s, "A"); var b = Add(s, "B"); Submit(s, "Defend.AddToHand"); var ordinary = Hand(s).Last();
            Set(s, Player(s), "Player", "Hp", Value.Of(2L)); Submit(s, "EndDevelopmentTurn");
            Equal("defeat", Outcome(s), "real defeat"); True(Members(s, "InPlay").Contains(a) && Hand(s).Contains(b) && Members(s, "DiscardPile").Count == 0, "native death guards keep lethal card in Play and later effect in Hand");
            True(Hand(s).Contains(ordinary), "ordinary flush stops"); Equal(0L, IntField(s, Audit(s), "InfectionAudit", "Ends"), "later end notification stops");
            Equal(1L, IntField(s, MoveState(s, FirstTarget(s)), "EnemyMoveState", "Sequence"), "enemy never acts/selects");
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:TurnEndHandEntry").Id).Count(), "snapshot entries cleaned");
        });
        return new[] { "base-spire:wriggler", "base-spire:infection" }.ToDictionary(id => id, _ => names.ToArray(), StringComparer.Ordinal);
    }
}
