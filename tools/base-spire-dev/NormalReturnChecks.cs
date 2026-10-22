using CoreTheSpire.Engine;
using System.Text.Json;

internal static partial class PackageChecks
{
    public static int RunNormalReturnOnly(DslPackage package)
    {
        var noNewDeath = Start(package);
        Submit(noNewDeath, "AddDevelopmentTarget");
        Set(noNewDeath, FirstTarget(noNewDeath), "Enemy", "Hp", Value.Of(0));
        Submit(noNewDeath, "BeginDevelopmentCombat"); Submit(noNewDeath, "BeginDevelopmentTurn");
        Equal("none", Outcome(noNewDeath), "a turn boundary cannot invent victory from an old dead target");
        using var input = NormalCombatInput();
        using var source = JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-reward-vectors.json")!);
        var expected = source.RootElement.GetProperty("vectors").EnumerateArray().Single(v => v.GetProperty("seed").GetString() == input.RootElement.GetProperty("seed").GetString()).GetProperty("offers")[0];
        var priorities = input.RootElement.GetProperty("combat_action_priority").EnumerateArray().Select(a => "base-spire:" + a.GetString()).ToArray();
        foreach (var path in input.RootElement.GetProperty("reward_paths").EnumerateArray())
        {
            var claim = path.GetString() == "claim";
            var s = NormalFirstCombat(package);
            var permanent = Members(s, "RunDeck").ToArray();
            for (var step = 0; Active(s) && step < input.RootElement.GetProperty("maximum_combat_actions").GetInt32(); step++)
            {
                var actions = s.LegalActions(s.Participant(0));
                var play = priorities.Select(id => actions.FirstOrDefault(a => a.Definition.QualifiedId == id)).First(a => a is not null)!;
                s.Submit(play.Definition, s.Participant(0), play.Bindings.Count > 0 ? play.Bindings[0] : []);
                Healthy(s);
            }
            Equal("victory", Outcome(s), "starter cards defeat the real source encounter");
            var visit = Members(s, "RunVisits").Single();
            var rewards = s.State.GetField(visit, s.RuleSet.TypeByQualifiedId("base-spire:RunVisit").FieldIndex("Rewards")).AsRef();
            Equal(1L, IntField(s, rewards, "CombatRewardSet", "VisitId"), "rewards belong to the actual first visit");
            Equal("rewards", TextField(s, visit, "RunVisit", "Phase"), "visit waits for reward resolution");
            Equal(99L, IntField(s, Player(s), "Player", "Gold"), "victory does not claim gold");
            Equal(0, Hand(s).Count, "source end-combat clears the hand");
            Equal(10, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Count - Members(s, "CardRewardOptions").Count, "only permanent and reward cards survive cleanup");
            True(!Legal(s, "ChooseMapPoint"), "pending rewards block the map");
            var options = Members(s, "CardRewardOptions").ToArray();
            var potionOptions = Members(s, "PotionRewardOptions").ToArray();
            var amount = IntField(s, Player(s), "Player", "GoldRewardAmount");
            Equal(expected.GetProperty("gold").GetInt64(), amount, "normal visit consumes the independent source reward amount");
            True(options.Select(c => CardDefinition(s, c)).SequenceEqual(expected.GetProperty("cards").EnumerateArray().Select(c => c.GetProperty("id").GetString())), "normal visit preserves the independent source card order");
            Equal(expected.GetProperty("counter").GetInt64(), s.Random.DrawCount("base-spire:player:rewards"), "normal battle consumes the exact complete reward cursor");
            var draws = s.Random.TotalDraws;
            if (claim)
            {
                Submit(s, "ClaimDevelopmentGoldReward");
                RejectNormalAction(s, "ClaimDevelopmentGoldReward");
                Submit(s, "ClaimDevelopmentCardReward", options[0]);
                True(Members(s, "RunDeck").Contains(options[0]), "the chosen actual instance becomes permanent");
                if (potionOptions.Length > 0) Submit(s, "ClaimDevelopmentPotionReward");
            }
            else
            {
                Submit(s, "SkipDevelopmentCardReward");
                if (potionOptions.Length > 0) Submit(s, "SkipDevelopmentPotionReward");
            }
            Submit(s, "CloseDevelopmentRewards");
            Equal(draws, s.Random.TotalDraws, "claim, skip and close never reroll");
            Equal(claim ? 99L + amount : 99L, IntField(s, Player(s), "Player", "Gold"), "only claimed gold is permanent");
            True(permanent.All(c => Members(s, "RunDeck").Contains(c)), "starter permanent identities survive");
            Equal(claim ? 11 : 10, Members(s, "RunDeck").Count, "claim and skip preserve the proper deck");
            True(options.Skip(claim ? 1 : 0).All(c => !s.State.Exists(c)), "unselected cards are destroyed");
            Equal(0, Members(s, "PotionRewardOptions").Count, "unselected potion offers are cleared");
            Equal("map", TextField(s, Player(s), "Player", "Room"), "close returns to the map");
            Equal("closed", TextField(s, visit, "RunVisit", "Status"), "visit closes once");
            RejectNormalAction(s, "CloseDevelopmentRewards");
            var run = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
            var current = s.State.GetField(run, s.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex("CurrentNode")).AsRef();
            var children = s.State.RelationTargets(s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren").Id, current);
            var choose = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
            True(choose.Bindings.Select(b => b[0].AsRef()).ToHashSet().SetEquals(children), "exact source successors are legal");
            var state = s.State.StateHash();
            Throws<EngineFaultException>(() => s.Submit(choose.Definition, s.Participant(0), [Value.OfRef(current)]));
            Equal(state, s.State.StateHash(), "the completed node cannot be revisited");
            Equal(draws, s.Random.TotalDraws, "a stale visit cannot consume RNG");
            Console.WriteLine("ok   ENV-T05 normal source combat, " + (claim ? "claim" : "skip") + ", permanent state and legal map successors");
        }

        var defeat = NormalFirstCombat(package);
        for (var turn = 0; Active(defeat) && turn < 30; turn++)
        {
            Submit(defeat, "EndDevelopmentTurn");
            if (Active(defeat)) Submit(defeat, "BeginDevelopmentTurn");
        }
        Equal("defeat", Outcome(defeat), "actual enemy attacks cause death");
        Equal("defeated", TextField(defeat, Members(defeat, "RunVisits").Single(), "RunVisit", "Status"), "death closes the real visit without victory");
        Equal(0, defeat.State.AllInstances(defeat.RuleSet.TypeByQualifiedId("base-spire:CombatRewardSet").Id).Count, "death generates no rewards");
        Equal(0L, defeat.Random.DrawCount("base-spire:player:rewards"), "death consumes no reward RNG");
        True(!Legal(defeat, "ChooseMapPoint"), "death opens no successors");
        Console.WriteLine("ok   ENV-T05 normal source enemy death path has no rewards or map return");
        RunNormalSettlementOrderChecks(package);
        return 0;
    }

    private static Session NormalFirstCombat(DslPackage package)
    {
        using var input = NormalCombatInput();
        var s = package.CreateSession(777, ["player"], input.RootElement.GetProperty("seed").GetString()); s.Start(); Submit(s, "StartNormalRun");
        var conch = s.PendingDecision!.Candidates.Single(o => TextField(s, o, "NeowOption", "RelicId") == input.RootElement.GetProperty("neow_relic").GetString());
        s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, Instances: [conch]));
        s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
        var choose = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
        s.Submit(choose.Definition, s.Participant(0), choose.Bindings[0]); Healthy(s);
        True(!Legal(s, "EndDevelopmentCombat"), "normal combat cannot be forcibly ended");
        return s;
    }

    private static JsonDocument NormalCombatInput() => JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-first-combat.json")!);

    private static void RejectNormalAction(Session s, string name)
    {
        var state = s.State.StateHash(); var draws = s.Random.TotalDraws;
        True(!Legal(s, name), "repeated action is not offered: " + name);
        var action = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:" + name);
        Throws<EngineFaultException>(() => s.Submit(action, s.Participant(0), []));
        Equal(state, s.State.StateHash(), "rejected action changes no state"); Equal(draws, s.Random.TotalDraws, "rejected action draws no RNG");
    }

    private static void RunNormalSettlementOrderChecks(DslPackage package)
    {
        using var copy = new Copy(package.Root);
        copy.Include("settlement-order.spire", """
            procedure CheckEnd { parameters = { owner = Ref(Player) victory = Bool } body = {
              if = { condition = victory then = {
                if = { condition = or(not(equal(field(owner, Player.MaxHp), 83)), not(equal(count(collection(ExhaustPile)), 1))) then = { fail = { message = "Feed growth and exhaustion must precede combat end" } } }
                if = { condition = not(isNone(field(owner, Player.CurrentRewardSet))) then = { fail = { message = "Rewards generated before combat-end hooks" } } }
              } }
            } }
            subscription End { event = CombatEnded reaction = CheckEnd priority = 200 }
            procedure CheckVictory { parameters = { owner = Ref(Player) victory = Bool } body = {
              if = { condition = or(not(equal(field(owner, Player.Hp), 49)), or(not(equal(field(owner, Player.Block), 0)), exists(collection(Hand)))) then = { fail = { message = "Cleanup then BurningBlood must precede settlement" } } }
              if = { condition = not(isNone(field(owner, Player.CurrentRewardSet))) then = { fail = { message = "Rewards must wait for all victory hooks" } } }
            } }
            subscription Victory { event = CombatVictory reaction = CheckVictory priority = 200 }
            """);
        var s = NormalFirstCombat(copy.Load());
        // Controlled last-kill fixture: keep the real formation, mark the other enemies already defeated.
        foreach (var enemy in s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).Skip(1))
            Set(s, enemy, "Enemy", "Hp", Value.Of(0));
        Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(5));
        Set(s, Player(s), "Player", "Hp", Value.Of(40));
        Submit(s, "Feed.AddToHand");
        var feed = Hand(s).Single(c => CardDefinition(s, c) == "base-spire:feed");
        Submit(s, "PlayAttackFromHand", feed);
        Equal(83L, IntField(s, Player(s), "Player", "MaxHp"), "last-kill growth persists");
        Equal(49L, Hp(s), "growth heals three before victory heals six");
        Equal("rewards", TextField(s, Player(s), "Player", "Room"), "rewards publish after the source end order");
        Console.WriteLine("ok   ENV-T05 controlled Feed last kill: parent effect, exhaust, end hook, cleanup, victory healing, rewards");

        using var effects = new Copy(package.Root);
        effects.Include("outer-effect-order.spire", """
            procedure EndTurnKill { parameters = { owner = Ref(Player) } body = {
              call = { procedure = DamageAllEnemiesFromPlayer args = { owner = owner amount = 999 } }
              if = { condition = or(not(field(owner, Player.CombatActive)), not(isNone(field(owner, Player.CurrentRewardSet)))) then = { fail = { message = "Turn effect settled before its outer callback finished" } } }
            } }
            subscription EndTurnKill { event = AfterPlayerTurnFinished reaction = EndTurnKill }
            procedure PotionAfterKill { parameters = { owner = Ref(Player) potion = Ref(Potion) target = Ref(Enemy) } body = {
              if = { condition = or(not(field(owner, Player.CombatActive)), not(isNone(field(owner, Player.CurrentRewardSet)))) then = { fail = { message = "Potion effect settled before consumption" } } }
            } }
            subscription PotionAfterKill { event = PotionUsedOnTarget reaction = PotionAfterKill priority = 1000 }
            """);
        var compiled = effects.Load();
        var turnEnd = NormalFirstCombat(compiled);
        Submit(turnEnd, "EndDevelopmentTurn");
        Equal("victory", Outcome(turnEnd), "turn-end effect settles after all callbacks");
        var potionKill = NormalFirstCombat(compiled);
        foreach (var enemy in potionKill.State.AllInstances(potionKill.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).Skip(1))
            Set(potionKill, enemy, "Enemy", "Hp", Value.Of(0));
        Set(potionKill, FirstTarget(potionKill), "Enemy", "Hp", Value.Of(10));
        Submit(potionKill, "FirePotion.Obtain");
        Submit(potionKill, "UsePotionOnTarget", Members(potionKill, "Potions").Single());
        Equal("victory", Outcome(potionKill), "potion last kill settles after consumption");
        Equal(0, Members(potionKill, "Potions").Count, "the used potion is consumed once");
        Console.WriteLine("ok   ENV-T05 controlled outer turn and potion callbacks complete before normal settlement");
    }
}
