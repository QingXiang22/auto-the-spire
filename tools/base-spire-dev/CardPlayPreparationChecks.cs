using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunCardPlayPreparationChecks(DslPackage package, Action<string, Action> check)
    {
        check("PLAY-PREP positive payment pauses after silent movement and resumes exactly once", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("play-probe.spire", PlayPreparationProbe);
            var s = PlayPreparationCombat(copy.Load()); var card = IcAdd(s, "StrikeIronclad"); Submit(s, "PlayProbeStart");
            var probe = PlayProbe(s); Set(s, probe, "PlayProbe", "Pause", Value.Of(true)); IcPlay(s, card);
            Equal(SessionStatus.AwaitingDecision, s.Status, "payment hook really paused");
            Equal(2L, Energy(s), "payment already spent"); True(Members(s, "InPlay").Contains(card), "paid card already in play");
            Equal(0, Members(s, "CardPlays").Count, "no premature body record");
            Equal(1L, IntField(s, probe, "PlayProbe", "Paid"), "positive payment notification");
            Equal(0L, IntField(s, probe, "PlayProbe", "Moved"), "synchronous play movement is silent");
            var hash = s.State.StateHash(); var draws = s.Random.TotalDraws;
            s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
            Equal(hash, s.State.StateHash(), "paused queries are read-only"); Equal(draws, s.Random.TotalDraws, "paused queries use no random");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true)); Healthy(s);
            Equal(2L, Energy(s), "resume never pays again"); Equal(994L, EnemyHp(s), "body runs once after resume");
            Equal(1L, IntField(s, probe, "PlayProbe", "Paid"), "notification not repeated");
            Equal(0L, IntField(s, probe, "PlayProbe", "Moved"), "ordinary result movement is also silent");
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:CardPlayPreparation").Id).Count(), "preparation cleaned");
        });
        check("PLAY-PREP zero and free plays omit payment; empty hand observes final pile before cost expiry", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("play-probe.spire", PlayPreparationProbe);
            foreach (var free in new[] { false, true })
            {
                var s = PlayPreparationCombat(copy.Load()); var card = IcAdd(s, "DefendIronclad");
                Submit(s, "PlayProbeStart"); Submit(s, "PlayProbeFree", card);
                if (free) Submit(s, "PlayProbeAuto", card); else IcPlay(s, card);
                var probe = PlayProbe(s);
                Equal(0L, IntField(s, probe, "PlayProbe", "Paid"), "zero or autoplay has no positive payment hook");
                Equal(1L, IntField(s, probe, "PlayProbe", "Empty"), "one final empty-hand notification");
                Equal(0L, IntField(s, probe, "PlayProbe", "Cost"), "until-played free cost survives through empty-hand hook");
                Equal(1L, IntField(s, card, "Card", "Cost"), "cost expires after empty-hand hook");
                True(Members(s, "DiscardPile").Contains(card), "empty-hand hook saw final discard");
            }
            var dead = PlayPreparationCombat(copy.Load()); var hemo = IcAdd(dead, "Hemokinesis"); Submit(dead, "PlayProbeStart");
            Set(dead, Player(dead), "Player", "Hp", Value.Of(1L)); IcPlay(dead, hemo);
            Equal(0L, IntField(dead, PlayProbe(dead), "PlayProbe", "Empty"), "dead owner has no empty-hand notification");
        });
        check("PLAY-PREP real modifiers preserve participant identity, order and read-only previews", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("play-probe.spire", PlayPreparationProbe);
            var s = PlayPreparationCombat(copy.Load()); var card = IcAdd(s, "DefendIronclad");
            Submit(s, "PlayProbeStart"); Submit(s, "PlayProbeCorruptions");
            var hash = s.State.StateHash(); var draws = s.Random.TotalDraws;
            for (var i = 0; i < 3; i++) True(Legal(s, "PlayProbePreview"), "pure result/count preview stays eligible");
            Equal(hash, s.State.StateHash(), "preview does not create preparations or consume powers"); Equal(draws, s.Random.TotalDraws, "preview uses no random");
            IcPlay(s, card); var probe = PlayProbe(s);
            Equal(1L, IntField(s, probe, "PlayProbe", "Participants"), "second Corruption did not change the already-exhaust result");
            Equal("RrCc", s.State.GetField(probe, s.RuleSet.TypeByQualifiedId("base-spire:PlayProbe").FieldIndex("Order")).AsString(), "result callbacks finish before count modifiers");
            True(Members(s, "ExhaustPile").Contains(card), "actual Corruption result");
            var repeated = PlayPreparationCombat(copy.Load()); var strike = IcAdd(repeated, "StrikeIronclad");
            Submit(repeated, "PlayProbeStart"); Submit(repeated, "PlayProbeRepeats"); IcPlay(repeated, strike);
            Equal(982L, EnemyHp(repeated), "each real repeat instance adds one rather than clamping to two");
            Equal(0L, IcPower(repeated, "base-spire:one-two-punch-power"), "both actual participants consumed once");
            Equal(2L, Energy(repeated), "three bodies share one payment");
            foreach (var before in new[] { false, true })
            {
                using var ordered = new Copy(package.Root); ordered.Include("play-probe.spire", PlayPreparationProbe);
                var expected = before ? 4 : 6; var priority = before ? -100 : 100;
                // Controlled noncommutative modifier surrounds the two real +1 consumers.
                ordered.Include("double-count.spire", $$"""
                    procedure DoublePreview { pure = true parameters = { value = Int owner = Ref(Player) card = Ref(Card) target = Binding } result = Int body = { return = { value = multiply(value, 2) } } }
                    subscription DoublePreview { event = ModifyCardPlayCount reaction = DoublePreview priority = {{priority}} }
                    procedure DoubleCount { parameters = { preparation = Ref(CardPlayPreparation) } body = { call = { procedure = SetCardPlayCount args = {
                      preparation = preparation value = multiply(field(preparation, CardPlayPreparation.Count), 2) power = first(collection(Powers)) relic = none
                    } } } }
                    subscription DoubleCount { event = CardPlayCountModifying reaction = DoubleCount priority = {{priority}} }
                    action OrderedPreview { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) }
                      requires = equal(pureCall(CalculateCardPlayCount, first(type(Player)), card, none), {{expected}}) body = { } }
                    """);
                var order = PlayPreparationCombat(ordered.Load()); var strikeOrder = IcAdd(order, "StrikeIronclad");
                Submit(order, "PlayProbeStart"); Submit(order, "PlayProbeRepeats"); True(Legal(order, "OrderedPreview"), "pure preview preserves noncommutative ordering"); IcPlay(order, strikeOrder);
                Equal(1000L - 6 * expected, EnemyHp(order), "stateful preparation uses the same order");
                Equal(0L, IcPower(order, "base-spire:one-two-punch-power"), "a model recorded twice still gets one duration tick");
            }
        });
        check("PLAY-PREP Power result precedes forced exhaust; semantic block history distinguishes sides", () =>
        {
            var s = PlayPreparationCombat(package); var card = IcAdd(s, "Inflame"); Set(s, card, "Card", "ForceExhaust", Value.Of(true)); IcPlay(s, card);
            True(Members(s, "RemovedFromCombat").Contains(card), "Power leaves combat before exhaust default");
            True(s.State.GetField(card, CardField(s, "ForceExhaust")).AsBool(), "unreached default-exhaust branch does not consume its flag");
            using var copy = new Copy(package.Root); copy.Include("side-block.spire", """
                action SideBlockPower { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:unmovable-power" stacks = 1 } } } }
                action OtherSideDefend { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = {
                  set = { target = first(type(Player)) field = Player.EnemyPhase value = true }
                  call = { procedure = AutoPlayCard args = { owner = first(type(Player)) card = card forceExhaust = false } }
                } }
                """);
            var history = PlayPreparationCombat(copy.Load()); Submit(history, "SideBlockPower"); IcPlay(history, IcAdd(history, "DefendIronclad"));
            Equal(10L, Block(history), "first player-side gain doubled"); var other = IcAdd(history, "DefendIronclad"); Submit(history, "OtherSideDefend", other);
            Equal(20L, Block(history), "enemy-side gain is first in its own semantic turn despite identical owner and turn number");
        });
        check("PLAY-PREP refused autoplay retains costs and flags; ending preserves the remaining preloaded batch", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("blocked-auto.spire", """
                procedure RefuseAuto { pure = true parameters = { value = Bool owner = Ref(Player) card = Ref(Card) auto = Bool } result = Bool body = { return = { value = and(value, not(auto)) } } }
                subscription RefuseAuto { event = ShouldPlayCard reaction = RefuseAuto }
                action RefusedAuto { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = {
                  call = { procedure = AddCardCostModifier args = { card = card source = "refused" operation = "set" amount = 0 lifetime = "play" } }
                  call = { procedure = AutoPlayCard args = { owner = first(type(Player)) card = card forceExhaust = false } }
                } }
                """);
            foreach (var exhaust in new[] { false, true })
            {
                var s = PlayPreparationCombat(copy.Load()); var power = IcAdd(s, "Inflame");
                Set(s, power, "Card", "ForceExhaust", Value.Of(exhaust)); Submit(s, "RefusedAuto", power);
                True(Members(s, exhaust ? "ExhaustPile" : "DiscardPile").Contains(power), "refused Power uses non-play fallback, not successful Power removal");
                Equal(0, Members(s, "CardPlays").Count, "refusal creates no play"); Equal(0L, IntField(s, power, "Card", "Cost"), "until-played cost remains");
                Equal(exhaust, s.State.GetField(power, CardField(s, "ForceExhaust")).AsBool(), "non-play fallback retains one-shot flag");
            }
            var ending = PlayPreparationCombat(package); var attack = IcAdd(ending, "StrikeIronclad"); var unplayed = IcAdd(ending, "DefendIronclad");
            IcMove(ending, attack, "Hand", "DrawPile"); IcMove(ending, unplayed, "Hand", "DrawPile");
            Set(ending, unplayed, "Card", "ForceExhaust", Value.Of(true)); Set(ending, FirstTarget(ending), "Enemy", "Hp", Value.Of(6L));
            var cascade = IcAdd(ending, "Cascade"); Set(ending, Player(ending), "Player", "Energy", Value.Of(2L)); IcPlay(ending, cascade);
            True(Members(ending, "InPlay").Contains(unplayed), "ending does not resolve the second preloaded card");
            Equal(0L, Block(ending), "unplayed card has no body");
            True(!ending.State.GetField(unplayed, CardField(ending, "ForceExhaust")).AsBool(), "batch still assigns false flag before terminal skip");
        });
    }

    private static Session PlayPreparationCombat(DslPackage package)
    {
        var s = Start(package); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
        Set(s, FirstTarget(s), "Enemy", "MaxHp", Value.Of(1000L)); Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L));
        return s;
    }
    private static InstanceId PlayProbe(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:PlayProbe").Id).Single();
    private const string PlayPreparationProbe = """
        entity PlayProbe { fields = { Paid = { type = Int default = 0 } Moved = { type = Int default = 0 } Empty = { type = Int default = 0 } Cost = { type = Int default = -1 } Participants = { type = Int default = 0 } Pause = { type = Bool default = false } Order = { type = String(length = 64) default = "" } } }
        action PlayProbeStart { role = player locals = { probe = Binding } body = { create = { type = PlayProbe scope = entityScope(first(type(Player))) into = probe } } }
        procedure ProbePaid { parameters = { owner = Ref(Player) card = Ref(Card) amount = Int } locals = { answer = Bool } body = {
          set = { target = first(type(PlayProbe)) field = PlayProbe.Paid value = add(field(first(type(PlayProbe)), PlayProbe.Paid), amount) }
          if = { condition = field(first(type(PlayProbe)), PlayProbe.Pause) then = { decide = { participant = field(owner, Player.Owner) kind = Bool prompt = "observe paid card" into = answer } } }
        } }
        subscription ProbePaid { event = AfterEnergySpent reaction = ProbePaid }
        procedure ProbeMoved { parameters = { owner = Ref(Player) card = Ref(Card) from = String(length = 16) to = String(length = 16) reason = String(length = 32) } body = {
          if = { condition = exists(type(PlayProbe)) then = { set = { target = first(type(PlayProbe)) field = PlayProbe.Moved value = add(field(first(type(PlayProbe)), PlayProbe.Moved), 1) } } }
        } }
        subscription ProbeMoved { event = CardMoved reaction = ProbeMoved }
        procedure ProbeEmpty { parameters = { owner = Ref(Player) } body = {
          if = { condition = not(equal(count(collection(InPlay)), 0)) then = { fail = { message = "empty-hand hook preceded result movement" } } }
          set = { target = first(type(PlayProbe)) field = PlayProbe.Empty value = add(field(first(type(PlayProbe)), PlayProbe.Empty), 1) }
          set = { target = first(type(PlayProbe)) field = PlayProbe.Cost value = field(field(owner, Player.CurrentCard), Card.Cost) }
        } }
        subscription ProbeEmpty { event = AfterHandEmptied reaction = ProbeEmpty }
        action PlayProbeFree { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = { call = { procedure = AddCardCostModifier args = { card = card source = "probe" operation = "set" amount = 0 lifetime = "play" } } } }
        action PlayProbeAuto { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = { call = { procedure = AutoPlayCard args = { owner = first(type(Player)) card = card forceExhaust = false } } } }
        procedure ProbePowerPair { parameters = { definition = String(length = 64) } locals = { power = Binding } body = {
          repeat = { count = 2 body = { create = { type = Power scope = entityScope(first(type(Player))) into = power collection = Powers fields = {
            DefinitionId = definition Definition = first(where(collection(PowerLibrary), b, equal(field(b, PowerBlueprint.DefinitionId), definition))) HolderPlayer = first(type(Player)) Stacks = 1
          } } } }
        } }
        action PlayProbeCorruptions { role = player body = { call = { procedure = ProbePowerPair args = { definition = "base-spire:corruption-power" } } } }
        action PlayProbeRepeats { role = player body = { call = { procedure = ProbePowerPair args = { definition = "base-spire:one-two-punch-power" } } } }
        action PlayProbePreview { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) }
          requires = and(equal(pureCall(CalculateCardPlayResultPile, first(type(Player)), card, false, 0, 0, 0, false), "exhaust"), equal(pureCall(CalculateCardPlayCount, first(type(Player)), card, none), 1)) body = { } }
        procedure ProbeResult { parameters = { preparation = Ref(CardPlayPreparation) } body = { set = { target = first(type(PlayProbe)) field = PlayProbe.Order value = concat(field(first(type(PlayProbe)), PlayProbe.Order), "R") } } }
        subscription ProbeResult { event = CardPlayResultModifying reaction = ProbeResult priority = -2000000 }
        procedure ProbeAfterResult { parameters = { preparation = Ref(CardPlayPreparation) modifier = Ref(CardPlayModifier) } body = {
          set = { target = first(type(PlayProbe)) field = PlayProbe.Order value = concat(field(first(type(PlayProbe)), PlayProbe.Order), "r") }
          set = { target = first(type(PlayProbe)) field = PlayProbe.Participants value = add(field(first(type(PlayProbe)), PlayProbe.Participants), 1) }
        } }
        subscription ProbeAfterResult { event = AfterModifyingCardPlayResultPile reaction = ProbeAfterResult }
        procedure ProbeCount { parameters = { preparation = Ref(CardPlayPreparation) } body = { set = { target = first(type(PlayProbe)) field = PlayProbe.Order value = concat(field(first(type(PlayProbe)), PlayProbe.Order), "C") } } }
        subscription ProbeCount { event = CardPlayCountModifying reaction = ProbeCount priority = -2000000 }
        procedure ProbeAfterCount { parameters = { preparation = Ref(CardPlayPreparation) } body = { set = { target = first(type(PlayProbe)) field = PlayProbe.Order value = concat(field(first(type(PlayProbe)), PlayProbe.Order), "c") } } }
        subscription ProbeAfterCount { event = AfterModifyingCardPlayCount reaction = ProbeAfterCount priority = 2000000 }
        """;
}
