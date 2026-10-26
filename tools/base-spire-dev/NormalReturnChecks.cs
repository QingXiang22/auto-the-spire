using CoreTheSpire.Engine;
using System.Text.Json;

internal static partial class PackageChecks
{
    public static int RunNormalReturnOnly(DslPackage package)
    {
        var development = Start(package);
        Submit(development, "Strike.AddToHand");
        Submit(development, "UpgradeCardInHand", Hand(development).Single());
        Submit(development, "FirePotion.Obtain");
        Submit(development, "TheAbacus.Obtain");
        Submit(development, "RemoveOwnedRelic", Members(development, "Relics").Single());
        Submit(development, "LeafSlimeS.Spawn");
        Submit(development, "Draw"); Submit(development, "DiscardHand");
        Equal(0, Hand(development).Count, "development setup actions still execute outside a normal Run");
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
            AssertNoDevelopmentActions(s, rejectSubmissions: claim);
            var permanent = Members(s, "RunDeck").ToArray();
            for (var step = 0; Active(s) && step < input.RootElement.GetProperty("maximum_combat_actions").GetInt32(); step++)
            {
                var actions = s.LegalActions(s.Participant(0));
                var play = priorities.Select(id => actions.FirstOrDefault(a => a.Definition.QualifiedId == id)).First(a => a is not null)!;
                s.Submit(play.Definition, s.Participant(0), play.Bindings.Count > 0 ? play.Bindings[0] : []);
                Healthy(s);
            }
            Equal("victory", Outcome(s), "starter cards defeat the real source encounter");
            AssertNoDevelopmentActions(s);
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
            AssertNoDevelopmentActions(s, rejectSubmissions: claim);
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
            Submit(defeat, "EndTurn");
        }
        Equal("defeat", Outcome(defeat), "actual enemy attacks cause death");
        Equal("defeated", TextField(defeat, Members(defeat, "RunVisits").Single(), "RunVisit", "Status"), "death closes the real visit without victory");
        Equal(0, defeat.State.AllInstances(defeat.RuleSet.TypeByQualifiedId("base-spire:CombatRewardSet").Id).Count, "death generates no rewards");
        Equal(0L, defeat.Random.DrawCount("base-spire:player:rewards"), "death consumes no reward RNG");
        True(!Legal(defeat, "ChooseMapPoint"), "death opens no successors");
        Console.WriteLine("ok   ENV-T05 normal source enemy death path has no rewards or map return");
        RunNormalSettlementOrderChecks(package);
        RunNormalTurnBoundaryChecks(package);
        RunDeathPowerChecks(package);
        RunNormalUnknownRoom(package);
        RunNormalEventSelection(package);
        RunNormalSapphireSeed(package);
        RunNormalDenseVegetation(package);
        return 0;
    }

    private static void RunNormalTurnBoundaryChecks(DslPackage package)
    {
        using var copy = new Copy(package.Root);
        copy.Include("normal-turn-boundary.spire", """
            entity TurnBoundaryProbe { fields = {
              Mode = { type = String(length = 16) default = "pause" }
              Starts = { type = Int default = 0 }
              Ends = { type = Int default = 0 }
            } }
            action SetupTurnBoundary { role = player locals = { probe = Binding } body = { create = { type = TurnBoundaryProbe scope = frameScope() into = probe } } }
            procedure CheckClosedEnd { parameters = { owner = Ref(Player) } body = {
              if = { condition = exists(type(TurnBoundaryProbe)) then = {
                if = { condition = field(owner, Player.TurnOpen) then = { fail = { message = "Manual play remained open during end effects" } } }
                set = { target = first(type(TurnBoundaryProbe)) field = TurnBoundaryProbe.Ends value = add(field(first(type(TurnBoundaryProbe)), TurnBoundaryProbe.Ends), 1) }
              } }
            } }
            subscription CheckClosedEnd { event = BeforePlayerSideTurnEnd reaction = CheckClosedEnd }
            procedure CheckClosedStart { parameters = { owner = Ref(Player) } locals = { answer = Bool } body = {
              if = { condition = exists(type(TurnBoundaryProbe)) then = {
                if = { condition = field(owner, Player.TurnOpen) then = { fail = { message = "Manual play opened before start effects finished" } } }
                set = { target = first(type(TurnBoundaryProbe)) field = TurnBoundaryProbe.Starts value = add(field(first(type(TurnBoundaryProbe)), TurnBoundaryProbe.Starts), 1) }
                if = { condition = equal(field(first(type(TurnBoundaryProbe)), TurnBoundaryProbe.Mode), "pause") then = {
                  decide = { participant = field(owner, Player.Owner) kind = Bool prompt = "turn start probe" into = answer }
                } }
                if = { condition = equal(field(first(type(TurnBoundaryProbe)), TurnBoundaryProbe.Mode), "kill") then = {
                  call = { procedure = DamageAllEnemiesFromPlayer args = { owner = owner amount = 999 } }
                } }
                if = { condition = equal(field(first(type(TurnBoundaryProbe)), TurnBoundaryProbe.Mode), "death") then = {
                  call = { procedure = LosePlayerHp args = { owner = owner amount = 999 } }
                } }
                if = { condition = equal(field(first(type(TurnBoundaryProbe)), TurnBoundaryProbe.Mode), "auto") then = {
                  call = { procedure = AutoPlayFromDrawPile args = { owner = owner play = none count = 1 forceExhaust = false } }
                } }
              } }
            } }
            subscription CheckClosedStart { event = BeforeHandDraw reaction = CheckClosedStart }
            procedure CheckClosedLate { parameters = { owner = Ref(Player) } body = {
              if = { condition = field(owner, Player.TurnOpen) then = { fail = { message = "Manual play opened during side-start notification" } } }
            } }
            subscription CheckClosedLate { event = AfterPlayerSideTurnStart reaction = CheckClosedLate }
            """);
        var rules = copy.Load();
        foreach (var mode in new[] { "pause", "kill", "death", "auto" })
        {
            var s = NormalFirstCombat(rules); Submit(s, "SetupTurnBoundary");
            var probe = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:TurnBoundaryProbe").Id).Single();
            Set(s, probe, "TurnBoundaryProbe", "Mode", Value.Of(mode));
            Submit(s, "EndTurn");
            if (mode == "pause")
            {
                Equal(SessionStatus.AwaitingDecision, s.Status, "one end action reaches a real start decision");
                True(!s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("TurnOpen")).AsBool(), "manual play stays closed at the start decision");
                Equal(0, Hand(s).Count, "starting draw waits for the real answer");
                var request = s.PendingDecision!; var hash = s.State.StateHash(); var rng = JsonSerializer.Serialize(s.Random.Capture());
                Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request.RequestId + 1, Flag: true)));
                s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
                Equal(hash, s.State.StateHash(), "stale answer and paused queries preserve state");
                Equal(rng, JsonSerializer.Serialize(s.Random.Capture()), "stale answer and paused queries consume no RNG");
                s.Answer(new DecisionAnswer(request.RequestId, Flag: true)); Healthy(s);
                Equal(5, Hand(s).Count, "continuation draws once");
                hash = s.State.StateHash(); rng = JsonSerializer.Serialize(s.Random.Capture());
                Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(request.RequestId, Flag: true)));
                Equal(hash, s.State.StateHash(), "repeated answer cannot replay the turn");
                Equal(rng, JsonSerializer.Serialize(s.Random.Capture()), "repeated answer cannot redraw");
            }
            Equal(1L, s.State.GetField(probe, s.RuleSet.TypeByQualifiedId("base-spire:TurnBoundaryProbe").FieldIndex("Starts")).AsInt(), "next ordinary start runs once");
            Equal(1L, s.State.GetField(probe, s.RuleSet.TypeByQualifiedId("base-spire:TurnBoundaryProbe").FieldIndex("Ends")).AsInt(), "player end runs once");
            Equal(2L, Field(s, "TurnNumber"), "single action advances to the next ordinary turn");
            if (mode is "kill" or "death")
            {
                Equal(mode == "kill" ? "victory" : "defeat", Outcome(s), "start effect returns the real outcome");
                True(!Legal(s, "EndTurn") && !Legal(s, "PlayCardFromHand"), "lethal start never reopens manual actions");
            }
            else True(Legal(s, "EndTurn"), "manual actions open after the entire start finishes");
            if (mode == "auto")
                Equal(1, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:CardPlayRecord").Id).Count, "automatic start play works while manual play is closed");
        }
        Console.WriteLine("ok   TURN complete normal EndTurn, closed manual phases, automatic play, start lethal and single decision continuation");
    }

    private static Session NormalFirstCombat(DslPackage package)
    {
        using var input = NormalCombatInput();
        var s = package.CreateSession(777, ["player"], input.RootElement.GetProperty("seed").GetString()); s.Start(); Submit(s, "StartNormalRun");
        AssertNoDevelopmentActions(s);
        var conch = s.PendingDecision!.Candidates.Single(o => TextField(s, o, "NeowOption", "RelicId") == input.RootElement.GetProperty("neow_relic").GetString());
        s.Answer(new DecisionAnswer(s.PendingDecision.RequestId, Instances: [conch]));
        AssertNoDevelopmentActions(s);
        s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true));
        AssertNoDevelopmentActions(s);
        var choose = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:ChooseMapPoint");
        s.Submit(choose.Definition, s.Participant(0), choose.Bindings[0]); Healthy(s);
        True(!Legal(s, "EndDevelopmentCombat"), "normal combat cannot be forcibly ended");
        return s;
    }

    private static JsonDocument NormalCombatInput() => JsonDocument.Parse(typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.normal-first-combat.json")!);

    private static void AssertNoDevelopmentActions(Session s, bool rejectSubmissions = false)
    {
        bool Development(ActionDefinition a)
        {
            var name = a.QualifiedId.Split(':')[^1];
            return name.EndsWith(".Obtain", StringComparison.Ordinal) || name.EndsWith(".AddToHand", StringComparison.Ordinal)
                || name.EndsWith(".Spawn", StringComparison.Ordinal) || name.EndsWith(".Initialize", StringComparison.Ordinal)
                || name is "Draw" or "DiscardHand" or "UpgradeCardInHand" or "RemoveOwnedRelic" or "AddDevelopmentTarget"
                    or "BeginDevelopmentCombat" or "EndDevelopmentCombat" or "BeginDevelopmentTurn" or "EndDevelopmentTurn" || name.StartsWith("EnterDevelopment", StringComparison.Ordinal);
        }
        True(!s.LegalActions(s.Participant(0)).Any(a => Development(a.Definition)), "normal Run has no external development setup candidates");
        if (!rejectSubmissions) return;
        var state = s.State.StateHash(); var random = JsonSerializer.Serialize(s.Random.Capture());
        foreach (var action in s.RuleSet.Actions.Where(Development))
        {
            if (action.Parameters.Count == 0)
                Throws<EngineFaultException>(() => s.Submit(action, s.Participant(0), []));
            else if (action.QualifiedId == "base-spire:RemoveOwnedRelic")
                Throws<EngineFaultException>(() => s.Submit(action, s.Participant(0), [Value.OfRef(Members(s, "Relics").First())]));
            else if (action.QualifiedId == "base-spire:UpgradeCardInHand" && Hand(s).Count > 0)
                Throws<EngineFaultException>(() => s.Submit(action, s.Participant(0), [Value.OfRef(Hand(s).First())]));
        }
        Equal(state, s.State.StateHash(), "directly submitted development actions preserve normal state");
        Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "directly submitted development actions preserve all random streams");
    }

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
            entity EndOrder { fields = { Phase = { type = Int default = 0 } } }
            procedure CheckEnd { parameters = { owner = Ref(Player) victory = Bool } body = {
              if = { condition = victory then = {
                if = { condition = or(not(equal(field(owner, Player.MaxHp), 83)), or(not(equal(count(collection(ExhaustPile)), 0)), not(equal(field(first(collection(InPlay)), Card.DefinitionId), "base-spire:feed")))) then = { fail = { message = "Feed growth must precede combat end while ending refuses self-exhaust" } } }
                if = { condition = not(isNone(field(owner, Player.CurrentRewardSet))) then = { fail = { message = "Rewards generated before combat-end hooks" } } }
                set = { target = first(type(EndOrder)) field = EndOrder.Phase value = 1 }
              } }
            } }
            subscription End { event = CombatEnded reaction = CheckEnd priority = 200 }
            procedure SourceEnd { parameters = { owner = Ref(Player) } body = {
              if = { condition = or(not(equal(field(first(type(EndOrder)), EndOrder.Phase), 1)), or(not(exists(collection(InPlay))), not(exists(collection(Powers))))) then = { fail = { message = "Source end must follow result commit and precede combat cleanup" } } }
              set = { target = first(type(EndOrder)) field = EndOrder.Phase value = 2 }
              call = { procedure = CheckCombatVictory args = { owner = owner } }
            } }
            subscription SourceEnd { event = AfterCombatEnd reaction = SourceEnd }
            procedure CheckVictory { parameters = { owner = Ref(Player) victory = Bool } body = {
              if = { condition = or(not(equal(field(first(type(EndOrder)), EndOrder.Phase), 2)), exists(collection(Powers))) then = { fail = { message = "Victory must follow source end and complete cleanup" } } }
              set = { target = first(type(EndOrder)) field = EndOrder.Phase value = 3 }
              if = { condition = or(not(equal(field(owner, Player.Hp), 49)), or(not(equal(field(owner, Player.Block), 0)), exists(collection(Hand)))) then = { fail = { message = "Cleanup then BurningBlood must precede settlement" } } }
              if = { condition = not(isNone(field(owner, Player.CurrentRewardSet))) then = { fail = { message = "Rewards must wait for all victory hooks" } } }
            } }
            subscription Victory { event = CombatVictory reaction = CheckVictory priority = 200 }
            action SetupControlledFeed { role = player locals = { card = Binding audit = Binding } body = {
              create = { type = EndOrder scope = frameScope() into = audit }
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:dexterity-power" stacks = 1 } }
              call = { procedure = CreateCombatCard args = { owner = first(type(Player)) definition = "base-spire:feed" } into = card }
              call = { procedure = AddGeneratedToHand args = { card = card } }
            } }
            """);
        var s = NormalFirstCombat(copy.Load());
        // Controlled last-kill fixture: keep the real formation, mark the other enemies already defeated.
        foreach (var enemy in s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).Skip(1))
            Set(s, enemy, "Enemy", "Hp", Value.Of(0));
        Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(5));
        Set(s, Player(s), "Player", "Hp", Value.Of(40));
        Submit(s, "SetupControlledFeed");
        var feed = Hand(s).Single(c => CardDefinition(s, c) == "base-spire:feed");
        Submit(s, "PlayAttackFromHand", feed);
        Equal(3L, IntField(s, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EndOrder").Id).Single(), "EndOrder", "Phase"), "commit, source end, cleanup and victory all completed once");
        Equal(83L, IntField(s, Player(s), "Player", "MaxHp"), "last-kill growth persists");
        Equal(49L, Hp(s), "growth heals three before victory heals six");
        Equal("rewards", TextField(s, Player(s), "Player", "Room"), "rewards publish after the source end order");
        Console.WriteLine("ok   ENV-T05 controlled Feed last kill: parent growth, refused ending exhaust, end hook, cleanup, victory healing, rewards");

        using var effects = new Copy(package.Root);
        effects.Include("outer-effect-order.spire", """
            procedure EndTurnKill { parameters = { owner = Ref(Player) } body = {
              call = { procedure = DamageAllEnemiesFromPlayer args = { owner = owner amount = 999 } }
              if = { condition = or(not(field(owner, Player.CombatActive)), not(isNone(field(owner, Player.CurrentRewardSet)))) then = { fail = { message = "Turn effect settled before its outer callback finished" } } }
            } }
            subscription EndTurnKill { event = AfterEnemySideTurnEnd reaction = EndTurnKill }
            procedure PotionAfterKill { parameters = { owner = Ref(Player) potion = Ref(Potion) target = Ref(Enemy) } body = {
              if = { condition = or(not(field(owner, Player.CombatActive)), not(isNone(field(owner, Player.CurrentRewardSet)))) then = { fail = { message = "Potion effect settled before consumption" } } }
            } }
            subscription PotionAfterKill { event = PotionUsedOnTarget reaction = PotionAfterKill priority = 1000 }
            action SetupControlledFirePotion { role = player locals = { potion = Binding } body = {
              call = { procedure = CreatePotionInstance args = { owner = first(type(Player)) definition = "base-spire:fire-potion" } into = potion }
              call = { procedure = ObtainPotion args = { owner = first(type(Player)) potion = potion } }
            } }
            """);
        var compiled = effects.Load();
        var turnEnd = NormalFirstCombat(compiled);
        Submit(turnEnd, "EndTurn");
        Equal("victory", Outcome(turnEnd), "turn-end effect settles after all callbacks");
        Equal(1L, Field(turnEnd, "TurnNumber"), "end-phase last kill cannot begin another turn");
        True(!Legal(turnEnd, "EndTurn"), "settled turn remains closed");
        var potionKill = NormalFirstCombat(compiled);
        foreach (var enemy in potionKill.State.AllInstances(potionKill.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).Skip(1))
            Set(potionKill, enemy, "Enemy", "Hp", Value.Of(0));
        Set(potionKill, FirstTarget(potionKill), "Enemy", "Hp", Value.Of(10));
        Submit(potionKill, "SetupControlledFirePotion");
        Submit(potionKill, "UsePotionOnTarget", Members(potionKill, "Potions").Single());
        Equal("victory", Outcome(potionKill), "potion last kill settles after consumption");
        Equal(0, Members(potionKill, "Potions").Count, "the used potion is consumed once");
        Console.WriteLine("ok   ENV-T05 controlled outer turn and potion callbacks complete before normal settlement");
    }
}
