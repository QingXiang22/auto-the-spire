using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunVisitBehaviorChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new Dictionary<string, string[]>();
        check("VISIT reward acquisition shares sourced resource modifiers and cannot credit twice", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("resource-probe.spire", """
                procedure Gold { pure = true parameters = { value = Int owner = Ref(Player) source = String(length = 64) } result = Int body = { return = { value = multiply(value, 2) } } }
                subscription Gold { event = ModifyGoldGained reaction = Gold }
                procedure Gained { parameters = { owner = Ref(Player) requested = Int actual = Int source = String(length = 64) } body = {
                  if = { condition = not(and(equal(actual, multiply(requested, 2)), equal(field(owner, Player.Gold), add(99, actual)))) then = { fail = { message = "Resource notification observed an uncommitted or incorrect gain" } } }
                } }
                subscription Gained { event = GoldGained reaction = Gained }
                """);
            var s = Start(copy.Load()); WinRewardCombat(s); var amount = Field(s, "GoldRewardAmount"); var draws = s.Random.TotalDraws;
            Submit(s, "ClaimDevelopmentGoldReward"); Equal(99L + 2 * amount, Field(s, "Gold"), "reward uses the shared gold modifier and publishes after commit");
            RejectRewardAction(s, "ClaimDevelopmentGoldReward"); Equal(draws, s.Random.TotalDraws, "claim and duplicate rejection do not reroll reward");
        });
        void AnswerBool(Session s, bool value) { s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: value)); Healthy(s); }
        InstanceId Visit(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventVisit").Id).Single();
        string Page(Session s) => s.State.GetField(Visit(s), s.RuleSet.TypeByQualifiedId("base-spire:EventVisit").FieldIndex("Page")).AsString();
        void Close(Session s) { Submit(s, "CloseDevelopmentEvent", Visit(s)); Equal("none", s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("Room")).AsString(), "shared visit restores return room"); }
        const string sapphire = "VISIT SapphireSeed heals once before native selection and shares Sown lifecycle";
        check(sapphire, () =>
        {
            foreach (var eat in new[] { true, false })
            {
                var s = Start(package); Submit(s, "Ironclad.Initialize"); Set(s, Player(s), "Player", "Hp", Value.Of(50L));
                Submit(s, "EnterDevelopmentSapphireSeed"); Equal(50L, Hp(s), "entry pauses at real option choice"); AnswerBool(s, eat);
                Equal(eat ? 59L : 50L, Hp(s), "eat healing happens before selection");
                var decision = s.PendingDecision; var chosen = Members(s, "RunDeck")[0];
                if (eat)
                {
                    True(decision!.Candidates.Count > 1, "real permanent upgrade candidates");
                    var hash = s.State.StateHash(); Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(decision.RequestId, [Player(s)]))); Equal(hash, s.State.StateHash(), "invalid answer cannot repeat healing or advance visit");
                    chosen = decision.Candidates[0]; s.Answer(new DecisionAnswer(decision.RequestId, [chosen])); Healthy(s);
                }
                else True(decision is null, "source PLANT automatically chooses the first eligible permanent card");
                Equal(eat ? "eat" : "plant", Page(s), "leaf owns final page"); Equal(eat ? 59L : 50L, Hp(s), "answer does not replay healing");
                if (eat) True(CardUpgraded(s, chosen), "upgrade is shared command");
                else Equal(1, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:CardAttachment").Id).Count, "one permanent Sown attachment");
                Close(s); if (decision is not null) Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(decision.RequestId, [chosen])));
                Submit(s, "EnterDevelopmentRestSite"); True(s.Status != SessionStatus.Faulted, "closed event releases next visit");
            }
        });
        witnesses["base-spire:sapphire-seed"] = [sapphire];
        check("VISIT SapphireSeed empty upgrade and enchantment domains finish without a card decision", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("empty-event-deck.spire", """
                action EmptyEventDeck { role = player body = { forEach = { query = collection(RunDeck) item = card body = { destroy = { target = card } } } } }
                """);
            var rules = copy.Load();
            foreach (var eat in new[] { true, false })
            {
                var s = Start(rules); Submit(s, "Ironclad.Initialize"); Submit(s, "EmptyEventDeck");
                Set(s, Player(s), "Player", "Hp", Value.Of(50)); var random = s.Random.TotalDraws;
                Submit(s, "EnterDevelopmentSapphireSeed"); AnswerBool(s, eat);
                True(s.PendingDecision is null, "empty domain creates no extra decision");
                Equal(eat ? 59L : 50L, Hp(s), "empty EAT still heals and PLANT changes no HP");
                Equal(eat ? "eat" : "plant", Page(s), "empty branch completes its page");
                Close(s); Equal(random, s.Random.TotalDraws, "empty event does not draw");
            }
            var upgraded = Start(package); Submit(upgraded, "Ironclad.Initialize");
            foreach (var card in Members(upgraded, "RunDeck")) Set(upgraded, card, "Card", "Upgraded", Value.Of(true));
            Submit(upgraded, "EnterDevelopmentSapphireSeed"); AnswerBool(upgraded, true);
            True(upgraded.PendingDecision is null, "fully upgraded deck has no upgrade decision"); Close(upgraded);
        });
        const string doors = "VISIT DoorsOfLightAndDark isolates event random stream and removes the selected permanent instance";
        check(doors, () =>
        {
            foreach (var light in new[] { true, false })
            {
                var s = Start(package); Submit(s, "Ironclad.Initialize"); var deck = Members(s, "RunDeck").ToArray();
                var niche = s.Random.DrawCount("base-spire:run:niche"); Submit(s, "EnterDevelopmentDoorsOfLightAndDark"); AnswerBool(s, light);
                if (light)
                {
                    Equal(2, deck.Count(c => CardUpgraded(s, c)), "stable-shuffled batch upgrades exactly two");
                    Equal((long)deck.Length - 1, s.Random.DrawCount("base-spire:doors-of-light-and-dark:visit:1"), "full Fisher-Yates draw budget, not two random picks");
                }
                else
                {
                    var d = s.PendingDecision!; var chosen = d.Candidates[^1]; s.Answer(new DecisionAnswer(d.RequestId, [chosen])); Healthy(s);
                    True(!Members(s, "RunDeck").Contains(chosen), "chosen last permanent removed"); Equal(deck.Length - 1, Members(s, "RunDeck").Count, "one removal");
                }
                Equal(niche, s.Random.DrawCount("base-spire:run:niche"), "event cannot consume relic niche stream"); Equal(light ? "light" : "dark", Page(s), "correct finished page"); Close(s);
                Submit(s, "EnterDevelopmentDoorsOfLightAndDark"); AnswerBool(s, true); Equal("light", Page(s), "second visit owns a fresh stream and continuation"); Close(s);
            }
        });
        witnesses["base-spire:doors-of-light-and-dark"] = [doors];
        return witnesses;
    }
}
