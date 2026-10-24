using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunExhaustCauseChecks(DslPackage package, Action<string, Action> check)
    {
        check("EXHAUST-CAUSE ethereal DarkEmbrace draws after flush and leaves both cards in hand", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("exhaust-cause-regression.spire", """
                procedure Ethereal { pure = true parameters = { value = Bool card = Ref(Card) keyword = String(length = 16) } result = Bool body = {
                  return = { value = or(value, and(equal(keyword, "ethereal"), or(equal(field(card, Card.DefinitionId), "base-spire:strike-ironclad"), equal(field(card, Card.DefinitionId), "base-spire:bash")))) }
                } }
                subscription Ethereal { event = ModifyCardKeyword reaction = Ethereal }
                action CauseSetup { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:dark-embrace-power" stacks = 1 } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "CauseSetup");
            IcAdd(s, "StrikeIronclad"); IcAdd(s, "Bash"); var first = IcAdd(s, "DefendIronclad"); IcMove(s, first, "Hand", "DrawPile"); var second = IcAdd(s, "Anger"); IcMove(s, second, "Hand", "DrawPile");
            Submit(s, "EndDevelopmentTurn"); Equal(2, Hand(s).Count, "source deferred draws survive the same-turn hand flush"); True(Hand(s).SequenceEqual(new[] { first, second }), "deferred draws preserve source top order");
        });
        check("EXHAUST-CAUSE ordinary exhaustion of an ethereal card draws immediately and does not queue", () =>
        {
            using var copy = CauseCopy(package); var s = CauseCombat(copy.Load()); Submit(s, "CauseDark");
            var top = IcAdd(s, "DefendIronclad"); IcMove(s, top, "Hand", "DrawPile"); var victim = IcAdd(s, "StrikeIronclad"); Submit(s, "CauseOrdinary", victim);
            True(Hand(s).SequenceEqual(new[] { top }), "ordinary cause stays immediate despite the card keyword"); Equal(0L, CauseDeferred(s), "ordinary does not increment the ethereal counter");
        });
        foreach (var noDrawFirst in new[] { false, true })
            check("EXHAUST-CAUSE NoDraw " + (noDrawFirst ? "expires before deferred draw" : "blocks deferred draw then expires without carrying debt"), () =>
            {
                using var copy = CauseCopy(package); var s = CauseCombat(copy.Load()); if (noDrawFirst) Submit(s, "CauseNoDraw"); Submit(s, "CauseDark"); if (!noDrawFirst) Submit(s, "CauseNoDraw");
                var top = IcAdd(s, "DefendIronclad"); IcMove(s, top, "Hand", "DrawPile"); Submit(s, "CauseEthereal", IcAdd(s, "StrikeIronclad"));
                Equal(1L, CauseDeferred(s), "NoDraw does not suppress counting"); Submit(s, "CauseSideEnd"); Equal(noDrawFirst ? 1 : 0, Hand(s).Count, "actual Power acquisition order controls expiry and Draw preflight");
                Equal(0L, CauseDeferred(s), "blocked draw still consumes the deferred count"); Submit(s, "CauseSideEnd"); Equal(noDrawFirst ? 1 : 0, Hand(s).Count, "no later side event replays old debt");
            });
        check("EXHAUST-CAUSE deferred count uses current amount and stops at full hand for a product above 999", () =>
        {
            using var copy = CauseCopy(package); var s = CauseCombat(copy.Load()); Submit(s, "CauseDark");
            Submit(s, "CauseEthereal", IcAdd(s, "StrikeIronclad")); Submit(s, "CauseEthereal", IcAdd(s, "Bash"));
            var power = Members(s, "Powers").Single(); Set(s, power, "Power", "Stacks", Value.Of(999L));
            for (var i = 0; i < 12; i++) IcMove(s, IcAdd(s, "DefendIronclad"), "Hand", "DrawPile");
            Submit(s, "CauseSideEnd"); Equal(10, Hand(s).Count, "1998 requested draws stop at the real hand cap"); Equal(2, Members(s, "DrawPile").Count, "capacity does not overdraw"); Equal(0L, CauseDeferred(s), "debt consumed once");
        });
        check("EXHAUST-CAUSE removed state and new combat cannot retain deferred counts and queries hide it", () =>
        {
            using var copy = CauseCopy(package); var s = CauseCombat(copy.Load()); Submit(s, "CauseDark"); Submit(s, "CauseEthereal", IcAdd(s, "StrikeIronclad"));
            var state = CauseStates(s).Single(); var hash = s.State.StateHash(); var draws = s.Random.TotalDraws;
            for (var i = 0; i < 3; i++) { True(s.Observe(s.Participant(0), "base-spire:PlayerView").Instances.All(x => x.Id != state), "deferred state is hidden"); s.LegalActions(s.Participant(0)); }
            Equal(hash, s.State.StateHash(), "queries preserve hidden count"); Equal(draws, s.Random.TotalDraws, "queries preserve random streams");
            Submit(s, "CauseRemoveDark"); Equal(0, CauseStates(s).Count, "Power removal destroys its state"); Submit(s, "CauseDark"); Equal(0L, CauseDeferred(s), "replacement starts at zero");
            Submit(s, "CauseEthereal", IcAdd(s, "Bash")); Submit(s, "EndDevelopmentCombat"); Equal(0, CauseStates(s).Count, "combat cleanup destroys deferred state");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "CauseDark"); Equal(0L, CauseDeferred(s), "new combat starts fresh");
        });
        check("EXHAUST-CAUSE generated exhaust placement is silent and both causes keep Charon and FeelNoPain immediate", () =>
        {
            using var copy = CauseCopy(package); var s = CauseCombat(copy.Load()); Submit(s, "CharonsAshes.Obtain"); Submit(s, "CauseDark"); Submit(s, "CauseFeel");
            Submit(s, "CauseGenerateExhaust"); Equal(0L, CauseDeferred(s), "generation directly into exhaust is not an exhaustion"); Equal(55L, EnemyHp(s), "no Charon for mere generation");
            Submit(s, "CauseEthereal", IcAdd(s, "StrikeIronclad")); Equal(52L, EnemyHp(s), "Charon ignores the cause and deals damage now"); Equal(3L, Block(s), "FeelNoPain ignores cause and grants block now"); Equal(1L, CauseDeferred(s), "DarkEmbrace alone defers");
        });
        check("EXHAUST-CAUSE a paused deferred draw has already cleared its counter and resumes exactly once", () =>
        {
            using var copy = CauseCopy(package); copy.Include("cause-pause.spire", """
                procedure PauseDraw { parameters = { owner = Ref(Player) card = Ref(Card) } locals = { answer = Bool } body = {
                  if = { condition = equal(field(owner, Player.Gold), 99) then = {
                    set = { target = owner field = Player.Gold value = 100 }
                    decide = { participant = participant(1) kind = Bool prompt = "resume deferred draw" into = answer }
                  } }
                } }
                subscription PauseDraw { event = AfterCardDrawn reaction = PauseDraw priority = -1 }
                """);
            var s = CauseCombat(copy.Load()); Submit(s, "CauseDark"); Submit(s, "CauseEthereal", IcAdd(s, "StrikeIronclad")); Submit(s, "CauseEthereal", IcAdd(s, "Bash"));
            for (var i = 0; i < 3; i++) IcMove(s, IcAdd(s, "DefendIronclad"), "Hand", "DrawPile");
            Submit(s, "CauseSideEnd"); Equal(SessionStatus.AwaitingDecision, s.Status, "draw notification pauses"); Equal(0L, CauseDeferred(s), "counter is zero before the first draw callback"); Equal(1, Hand(s).Count, "first card moved once before pause");
            var hash = s.State.StateHash(); var draws = s.Random.TotalDraws; s.Observe(s.Participant(0), "base-spire:PlayerView"); Equal(hash, s.State.StateHash(), "paused observation cannot replay the reset"); Equal(draws, s.Random.TotalDraws, "no random samples during observation");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true)); Healthy(s); Equal(2, Hand(s).Count, "outer command continues remaining draws once"); Submit(s, "CauseSideEnd"); Equal(2, Hand(s).Count, "later notification cannot replay the old count");
        });
    }

    private static Copy CauseCopy(DslPackage package)
    {
        var copy = new Copy(package.Root);
        using var stream = typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.exhaust-cause-probe.spire")!;
        using var reader = new StreamReader(stream); copy.Include("cause-probe.spire", reader.ReadToEnd()); return copy;
    }
    private static Session CauseCombat(DslPackage package) { var s = Start(package); Submit(s, "FuzzyWurmCrawler.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); return s; }
    private static IReadOnlyList<InstanceId> CauseStates(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:DarkEmbracePower.State").Id);
    private static long CauseDeferred(Session s) => IntField(s, CauseStates(s).Single(), "DarkEmbracePower.State", "EtherealExhausts");
}
