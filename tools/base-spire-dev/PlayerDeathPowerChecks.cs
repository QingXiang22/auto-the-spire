using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunPlayerDeathPowerChecks(DslPackage package)
    {
        foreach (var mode in new[] { "ordered", "nested", "normal", "card" })
        {
            var nested = mode is "nested" or "normal";
            var normal = mode == "normal";
            var fromCard = mode == "card";
            using var copy = new Copy(package.Root);
            copy.Include("player-death-audit.spire", """
                entity PlayerDeathAudit { fields = {
                  Deaths = { type = Int default = 0 } Removed = { type = Int default = 0 } Ended = { type = Int default = 0 }
                } }
                action InitializePlayerDeathAudit { role = player locals = { audit = Binding other = Binding } body = {
                  create = { type = PlayerDeathAudit scope = frameScope() into = audit }
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:frail-power" stacks = 2 } }
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:juggling-power" stacks = 3 } }
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:vigor-power" stacks = 4 } }
                  call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:strength-power" stacks = 5 } }
                  create = { type = Player scope = frameScope() into = other }
                  call = { procedure = ApplyPlayerPower args = { owner = other definition = "base-spire:juggling-power" stacks = 7 } }
                } }
                procedure InspectPlayerDeath { parameters = { owner = Ref(Player) } body = {
                  if = { condition = not(equal(count(where(collection(Powers), p, equal(field(p, Power.HolderPlayer), owner))), 3)) then = { fail = { message = "Player death notification must see all original Powers" } } }
                  if = { condition = or(not(field(owner, Player.CombatActive)), or(field(owner, Player.TurnOpen), not(equal(field(owner, Player.CombatOutcome), "none")))) then = { fail = { message = "Death notification requires the pending defeat context" } } }
                  set = { target = first(type(PlayerDeathAudit)) field = PlayerDeathAudit.Deaths value = add(field(first(type(PlayerDeathAudit)), PlayerDeathAudit.Deaths), 1) }
                  call = { procedure = CheckCombatDefeat args = { owner = owner } }
                } }
                subscription InspectPlayerDeath { event = PlayerDied reaction = InspectPlayerDeath }
                procedure InspectFatalCard { parameters = { owner = Ref(Player) card = Ref(Card) } body = {
                  if = { condition = equal(field(owner, Player.Hp), 0) then = {
                    if = { condition = or(not(field(owner, Player.CombatActive)), or(not(equal(field(owner, Player.CombatOutcome), "none")), isNone(field(owner, Player.CurrentPlay)))) then = { fail = { message = "Fatal card reactions retain the live outer play until its boundary" } } }
                    if = { condition = exists(where(collection(Powers), p, equal(field(p, Power.HolderPlayer), owner))) then = { fail = { message = "Fatal card reactions follow death Power cleanup" } } }
                  } }
                } }
                subscription InspectFatalCard { event = AfterCardPlayed reaction = InspectFatalCard }
                procedure InspectPlayerPowerRemoval { parameters = { power = Ref(Power) } body = {
                  if = { condition = equal(field(power, Power.HolderPlayer), first(type(Player))) then = {
                    if = { condition = greaterOrEqual(collectionIndex(Powers, power), 0) then = { fail = { message = "Player Power removed before leaving its collection" } } }
                    if = { condition = or(not(equal(field(first(type(PlayerDeathAudit)), PlayerDeathAudit.Deaths), 1)), not(equal(field(first(type(PlayerDeathAudit)), PlayerDeathAudit.Ended), 0))) then = { fail = { message = "Player Power removal must follow death and precede combat end" } } }
                    set = { target = first(type(PlayerDeathAudit)) field = PlayerDeathAudit.Removed value = add(field(first(type(PlayerDeathAudit)), PlayerDeathAudit.Removed), 1) }
                    if = { condition = and(NESTED, equal(field(power, Power.DefinitionId), "base-spire:frail-power")) then = {
                      call = { procedure = RemovePower args = { power = first(where(collection(Powers), p, and(equal(field(p, Power.HolderPlayer), first(type(Player))), equal(field(p, Power.DefinitionId), "base-spire:vigor-power")))) } }
                    } }
                    call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = 9 } }
                    call = { procedure = LosePlayerHp args = { owner = first(type(Player)) amount = 5 } }
                    call = { procedure = CheckCombatDefeat args = { owner = first(type(Player)) } }
                  } }
                } }
                subscription InspectPlayerPowerRemoval { event = PowerRemoved reaction = InspectPlayerPowerRemoval }
                procedure InspectPlayerCombatEnd { parameters = { owner = Ref(Player) victory = Bool } body = {
                  if = { condition = exists(where(collection(Powers), p, equal(field(p, Power.HolderPlayer), owner))) then = { fail = { message = "Combat end precedes player Power cleanup" } } }
                  if = { condition = not(equal(field(first(type(PlayerDeathAudit)), PlayerDeathAudit.Removed), 3)) then = { fail = { message = "Every player Power must be fully removed exactly once" } } }
                  if = { condition = or(not(equal(count(type(JugglingPower.State)), 1)), exists(type(VigorPower.State))) then = { fail = { message = "Combat end must see cleaned player module state and retained other-holder state" } } }
                  set = { target = first(type(PlayerDeathAudit)) field = PlayerDeathAudit.Ended value = add(field(first(type(PlayerDeathAudit)), PlayerDeathAudit.Ended), 1) }
                } }
                subscription InspectPlayerCombatEnd { event = CombatEnded reaction = InspectPlayerCombatEnd }
                action LethalPlayerDamage { role = player body = { call = { procedure = DamagePlayer args = { owner = first(type(Player)) amount = 10 } } } }
                action RepeatPlayerDeath { role = player body = {
                  call = { procedure = CheckCombatDefeat args = { owner = first(type(Player)) } }
                  call = { procedure = RemovePowersOnDeath args = { player = first(type(Player)) enemy = none } }
                } }
                """.Replace("NESTED", nested ? "true" : "false", StringComparison.Ordinal));
            var compiled = copy.Load();
            var s = normal ? NormalFirstCombat(compiled) : Start(compiled);
            if (!normal) { Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); }
            Submit(s, "InitializePlayerDeathAudit");
            var removedPowers = Members(s, "Powers").Take(3).ToArray();
            Equal(2, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:JugglingPower.State").Id).Count, "private states exist for both holders before damage");
            Equal(1, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:VigorPower.State").Id).Count, "dead player's second module state exists");
            if (fromCard) Submit(s, "Bloodletting.AddToHand");
            Set(s, Player(s), "Player", "Hp", Value.Of(fromCard ? 2L : 3L)); Set(s, Player(s), "Player", "Block", Value.Of(normal ? 0L : 4L));
            var draws = s.Random.TotalDraws;
            if (normal)
            {
                Submit(s, "EndTurn");
                Equal("defeated", TextField(s, Members(s, "RunVisits").Single(), "RunVisit", "Status"), "real normal visit commits defeat after cleanup");
                Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:CombatRewardSet").Id).Count, "normal defeat creates no rewards");
                True(!Legal(s, "ChooseMapPoint"), "normal defeat opens no map successors");
            }
            else if (fromCard) IcPlay(s, Hand(s).Single());
            else Submit(s, "LethalPlayerDamage");
            Equal("defeat", Outcome(s), "real damage or card HP loss defeats the player");
            var audit = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:PlayerDeathAudit").Id).Single();
            Equal(1L, IntField(s, audit, "PlayerDeathAudit", "Deaths"), "one player death notification");
            Equal(3L, IntField(s, audit, "PlayerDeathAudit", "Removed"), "one removal for each dead-player Power");
            Equal(1L, IntField(s, audit, "PlayerDeathAudit", "Ended"), "one combat end");
            Equal(2, Members(s, "Powers").Count, "live enemy and other player retain their Powers");
            Equal("5,7", string.Join(',', Members(s, "Powers").Select(p => IntField(s, p, "Power", "Stacks"))), "other-holder amounts unchanged");
            if (!normal)
            {
                var result = Members(s, "DamageResults").Single();
                Equal(fromCard ? 0L : 4L, IntField(s, result, "DamageResult", "Blocked"), "card HP loss bypasses block; ordinary damage consumes block");
                Equal(fromCard ? 2L : 3L, IntField(s, result, "DamageResult", "Unblocked"), "actual life lost");
                Equal(fromCard ? 1L : 3L, IntField(s, result, "DamageResult", "Overkill"), "overkill preserved");
            }
            var hash = s.State.StateHash();
            if (normal)
            {
                Equal(SessionStatus.Terminal, s.Status, "normal defeat ends the session at a declared terminal result");
                Equal("base-spire:NormalRunDefeat", s.Terminal!.Definition.QualifiedId, "declared defeat result");
                Equal(0L, s.Terminal!.Fields[0].AsInt(), "terminal reports the resolved act");
                True(s.Terminal!.Fields[1].AsInt() > 0, "terminal reports the total floor");
                var repeat = s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:RepeatPlayerDeath");
                EngineFaultException? terminalFault = null;
                try { s.Submit(repeat, s.Participant(0), []); } catch (EngineFaultException error) { terminalFault = error; }
                True(terminalFault is not null && terminalFault.Message.Contains("terminal", StringComparison.Ordinal), "a terminal session accepts no further command");
                Equal(hash, s.State.StateHash(), "repeat death check and cleanup are state-idempotent");
                Equal(draws, s.Random.TotalDraws, "damage, nested death checks and cleanup consume no RNG");
            }
            else
            {
                Submit(s, "RepeatPlayerDeath"); Equal(hash, s.State.StateHash(), "repeat death check and cleanup are state-idempotent");
                hash = s.State.StateHash(); var version = s.StateVersion;
                for (var i = 0; i < 3; i++)
                {
                    var view = s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
                    True(view.Instances.All(i => !removedPowers.Contains(i.Id)), "removed player Powers are absent from the public view");
                    True(Members(s, "Powers").All(p => view.Instances.Any(i => i.Id == p)), "live holders still publish their Powers");
                }
                Equal(hash, s.State.StateHash(), "defeated public queries are read-only"); Equal(version, s.StateVersion, "query version unchanged");
                Equal(draws, s.Random.TotalDraws, "damage, nested death checks and cleanup consume no RNG");
                True(!Legal(s, "BeginDevelopmentTurn") && !Legal(s, "ClaimDevelopmentGoldReward"), "defeat exposes neither turn nor reward actions");
            }
            var independent = Start(compiled);
            Equal(0, Members(independent, "Powers").Count, "separate session does not inherit dead-player Powers");
            Equal(0, independent.State.AllInstances(independent.RuleSet.TypeByQualifiedId("base-spire:JugglingPower.State").Id).Count, "separate session does not inherit private module state");
            Console.WriteLine("ok   CB09 controlled player death: " + mode + ", module cleanup, other-holder isolation, idempotence and public queries");
            if (mode == "ordered")
            {
                var live = Start(compiled); Submit(live, "AddDevelopmentTarget"); Submit(live, "BeginDevelopmentCombat"); Submit(live, "BeginDevelopmentTurn"); Submit(live, "InitializePlayerDeathAudit");
                var beforeInvalid = live.State.StateHash();
                Throws<EngineFaultException>(() => Submit(live, "RepeatPlayerDeath"));
                Equal(beforeInvalid, live.State.StateHash(), "live-player death cleanup is rejected atomically");
            }
        }
    }
}
