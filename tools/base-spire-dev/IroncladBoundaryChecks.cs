using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunIroncladBoundaryChecks(DslPackage package, Action<string, Action> check)
    {
        Session Combat(long seed = 20261018)
        {
            var s = package.CreateSession(checked((ulong)seed), ["player"]); s.Start(); Healthy(s);
            Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            foreach (var c in Members(s, "DiscardPile").ToArray()) s.State.RunAtomic(() => s.State.DestroyInstance(c));
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L)); Set(s, Player(s), "Player", "Energy", Value.Of(30L)); return s;
        }
        check("IC target decision: choose second enemy, reject stale/foreign answer, pay exactly once", () =>
        {
            var s = Combat(); Submit(s, "AddDevelopmentTarget"); var enemies = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).ToArray();
            var c = IcAdd(s, "StrikeIronclad"); Submit(s, "PlayTargetedCard", c); var decision = s.PendingDecision!;
            Equal(30L, Energy(s), "no payment before target selection"); True(decision.Candidates.SequenceEqual(enemies), "all living enemies in order");
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(decision.RequestId, [c]))); Equal(30L, Energy(s), "bad answer cannot charge");
            s.Answer(new DecisionAnswer(decision.RequestId, [enemies[1]])); Healthy(s);
            Equal(1000L, EnemyHp(s), "first enemy untouched"); Equal(34L, IntField(s, enemies[1], "Enemy", "Hp"), "second enemy damaged"); Equal(29L, Energy(s), "single payment");
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(decision.RequestId, [enemies[0]]))); Equal(29L, Energy(s), "stale answer no extra payment");
        });
        check("IC hand decision: selected non-first card exhausts, pre-choice effects and payment never repeat", () =>
        {
            var s = Combat(); var brand = IcAdd(s, "Brand"); var a = IcAdd(s, "StrikeIronclad"); var b = IcAdd(s, "DefendIronclad");
            IcPlay(s, brand); var decision = s.PendingDecision!; Equal(79L, Hp(s), "one HP lost before choice");
            True(decision.Candidates.SequenceEqual(new[] { a, b }), "currently resolving Brand excluded");
            Throws<EngineFaultException>(() => s.Answer(new DecisionAnswer(decision.RequestId, []))); Equal(79L, Hp(s), "mandatory selection cannot cancel or restart effects");
            s.Answer(new DecisionAnswer(decision.RequestId, [b])); Healthy(s); True(Hand(s).Contains(a) && Members(s, "ExhaustPile").Contains(b), "explicit selected instance"); Equal(79L, Hp(s), "resume did not lose HP twice");
        });
        check("IC exhaust snapshot: DarkEmbrace draws survive FiendFire and only original hand determines hits", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "DarkEmbrace")); var fire = IcAdd(s, "FiendFire"); IcAdd(s, "StrikeIronclad"); IcAdd(s, "DefendIronclad");
            for (var i = 0; i < 3; i++) IcMove(s, IcAdd(s, "Anger"), "Hand", "DrawPile");
            IcPlay(s, fire); Equal(986L, EnemyHp(s), "only original two hand cards produce hits"); Equal(3, Hand(s).Count, "two snapshot exhaust draws and final source exhaust draw survived");
            Equal(3, Members(s, "ExhaustPile").Count, "newly drawn cards not exhausted by snapshot");
        });
        check("IC zero domains: BurningPact still draws, Brand gains strength, zero X does zero damage", () =>
        {
            var s = Combat(); var pact = IcAdd(s, "BurningPact"); IcMove(s, IcAdd(s, "Anger"), "Hand", "DrawPile"); IcPlay(s, pact); Equal(1, Hand(s).Count, "draw occurs with no card to exhaust");
            IcMove(s, Hand(s).Single(), "Hand", "DrawPile"); IcPlay(s, IcAdd(s, "Brand")); Equal(1L, IcPower(s, "damage-additive"), "strength even when hand empty");
            var whirlwind = IcAdd(s, "Whirlwind"); Set(s, Player(s), "Player", "Energy", Value.Of(0L)); IcPlay(s, whirlwind); Equal(1000L, EnemyHp(s), "zero X no hits, even with strength");
        });
        check("IC requirement boundaries: PactsEnd below three, ForgottenRitual before own exhaust, Feed without kill", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "PactsEnd")); Equal(1000L, EnemyHp(s), "PactsEnd below threshold");
            IcPlay(s, IcAdd(s, "ForgottenRitual")); Equal(29L, Energy(s), "own exhaust does not retroactively enable energy");
            IcPlay(s, IcAdd(s, "Feed")); Equal(80L, IntField(s, Player(s), "Player", "MaxHp"), "nonfatal Feed no max HP"); Equal(990L, EnemyHp(s), "nonfatal still damages");
        });
        check("IC autoplay X: captured energy is preserved without payment and no second draw hook fires", () =>
        {
            var s = Combat(); var havoc = IcAdd(s, "Havoc", true); var whirlwind = IcAdd(s, "Whirlwind"); IcMove(s, whirlwind, "Hand", "DrawPile");
            Set(s, Player(s), "Player", "Energy", Value.Of(2L)); IcPlay(s, havoc); Equal(990L, EnemyHp(s), "autoplay X captures two"); Equal(2L, Energy(s), "autoplay does not spend captured energy");
            True(Members(s, "ExhaustPile").Contains(whirlwind), "Havoc forces X attack exhaust"); Equal(2, Members(s, "CardPlays").Count, "parent and child play records");
        });
        check("IC source costs: X ignores ordinary free modifiers and InfernalBlade free expires after playing", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "Unrelenting")); var x = IcAdd(s, "Whirlwind");
            Set(s, Player(s), "Player", "Energy", Value.Of(2L)); IcPlay(s, x); Equal(0L, Energy(s), "X spends energy despite FreeAttack"); Equal(976L, EnemyHp(s), "14 then two five-damage hits");
            var t = Combat(); IcPlay(t, IcAdd(t, "InfernalBlade")); var c = Hand(t).Single();
            // Move the same generated card through a second play to distinguish until-played from whole-turn free.
            var before = Energy(t); IcPlay(t, c); True(Energy(t) <= before, "free play can still have an explicit card energy effect");
            True(!t.State.GetField(c, CardField(t, "FreeUntilPlayed")).AsBool(), "until-played marker cleared");
            Equal(Math.Max(0, IntField(t, c, "Card", "BaseCost") - IntField(t, c, "Card", "TurnCostDiscount")), CardCost(t, c), "remaining local discount restored immediately");
        });
        check("IC powered calculated damage and multipliers round once after the complete chain", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("weak-probe.spire", """
                action Weaken { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:weak-power" kind = "damage-dealt-multiplier" stacks = 1 } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Energy", Value.Of(10L)); IcPlay(s, IcAdd(s, "Tremble")); Submit(s, "Weaken");
            IcPlay(s, IcAdd(s, "TwinStrike")); Equal(30L, EnemyHp(s), "5 x .75 x 1.5 floors to 5 per hit");
            Set(s, Player(s), "Player", "Block", Value.Of(5L)); IcPlay(s, IcAdd(s, "BodySlam")); Equal(25L, EnemyHp(s), "calculated block damage also uses powered modifiers");
        });
        check("IC repeats: one payment and one result transition for two plays of the same instance", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "OneTwoPunch")); var anger = IcAdd(s, "Anger", true); var energy = Energy(s); IcPlay(s, anger);
            Equal(984L, EnemyHp(s), "upgraded Anger hit twice"); Equal(energy, Energy(s), "zero cost paid once");
            var copies = Members(s, "DiscardPile").Where(c => CardDefinition(s, c) == "base-spire:anger").ToArray(); Equal(3, copies.Length, "two effect clones plus one played instance");
            True(copies.All(c => CardUpgraded(s, c) && s.State.GetField(c, CardField(s, "Kind")).AsString() == "attack"), "clones preserve upgrade and type");
            Equal(3, Members(s, "CardPlays").Count, "two distinct repetition records plus skill");
        });
        check("IC nested context: child autoplay finishes before parent and deferred Rupture stays with the right play", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "Rupture")); var havoc = IcAdd(s, "Havoc"); var hemo = IcAdd(s, "Hemokinesis"); IcMove(s, hemo, "Hand", "DrawPile");
            IcPlay(s, havoc); Equal(985L, EnemyHp(s), "child hit before its deferred Strength"); Equal(1L, IcPower(s, "damage-additive"), "child callback applied exactly once");
            Equal(3, Members(s, "CardPlays").Count, "history includes nested play"); Equal(0L, IntField(s, Player(s), "Player", "PlayDepth"), "context stack fully restored");
            True(s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("CurrentPlay")).Kind == ValueKind.None, "parent binding restored to none");
        });
        check("IC multi-block: Unmovable excludes earlier block events from the same SecondWind play", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "Unmovable")); var wind = IcAdd(s, "SecondWind"); IcAdd(s, "DefendIronclad"); IcAdd(s, "DefendIronclad"); IcPlay(s, wind);
            Equal(20L, Block(s), "both blocks doubled within the first block-producing play"); IcPlay(s, IcAdd(s, "DefendIronclad")); Equal(25L, Block(s), "next play is beyond first block quota");
        });
        check("IC calculated absorption: Thrash uses calculated damage and attacker modifiers", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "Inflame")); var thrash = IcAdd(s, "Thrash"); IcAdd(s, "BodySlam"); Set(s, Player(s), "Player", "Block", Value.Of(11L));
            IcPlay(s, thrash); Equal(13L, IntField(s, thrash, "Card", "DamageGrowth"), "block calculated as 11 plus 2 strength when absorbed");
        });
        check("IC random retarget: every SwordBoomerang hit rebuilds alive candidates; empty hand consumes no card sample", () =>
        {
            var s = Combat(); Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(3L)); Submit(s, "AddDevelopmentTarget");
            IcPlay(s, IcAdd(s, "SwordBoomerang", true));
            var totalHp = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).Sum(e => IntField(s, e, "Enemy", "Hp")); Equal(31L, totalHp, "four hits, including retarget after lethal");
            var before = s.Random.DrawCount("base-spire:run:combat_card_selection"); IcPlay(s, IcAdd(s, "Cinder")); Equal(before, s.Random.DrawCount("base-spire:run:combat_card_selection"), "empty random card domain consumes nothing");
        });
        check("IC temporary costs: upgrade cannot remove a this-turn free override; Stomp entering mid-turn uses finished history", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "InfernalBlade")); var generated = Hand(s).Single(); Submit(s, "UpgradeCardInHand", generated); Equal(0L, CardCost(s, generated), "free override survives upgrade");
            IcMove(s, generated, "Hand", "DrawPile"); IcPlay(s, IcAdd(s, "Anger")); var stomp = IcAdd(s, "Stomp"); Equal(2L, CardCost(s, stomp), "new Stomp discounts prior completed attack");
            IcNextTurn(s); Equal(IntField(s, generated, "Card", "BaseCost"), CardCost(s, generated), "upgraded baseline restored"); Equal(3L, CardCost(s, stomp), "Stomp restored");
        });
        check("IC generation replay: full eligible pool, stable seeds and newly generated cards retain actual rules", () =>
        {
            foreach (var seed in new[] { 20261018L, 777L })
            {
                string[] Trace()
                {
                    var s = Combat(seed); var trace = new List<string>();
                    for (var i = 0; i < 12; i++)
                    {
                        Set(s, Player(s), "Player", "Energy", Value.Of(30L)); IcPlay(s, IcAdd(s, "InfernalBlade")); var c = Hand(s).Single(); trace.Add(CardDefinition(s, c)); IcMove(s, c, "Hand", "RemovedFromCombat");
                    }
                    return trace.ToArray();
                }
                True(Trace().SequenceEqual(Trace()), "same source pool and seed replay identically");
            }
        });
        check("IC lethal post-effects: final hit still completes hand choice and resulting copy/exhaust callbacks", () =>
        {
            var s = Combat(); Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(6L)); IcPlay(s, IcAdd(s, "Anger")); Equal("victory", Outcome(s), "victory");
            Equal(2, Members(s, "DiscardPile").Count, "lethal Anger still creates its copy before settlement");
            var t = Combat(); IcPlay(t, IcAdd(t, "DarkEmbrace")); Set(t, FirstTarget(t), "Enemy", "Hp", Value.Of(7L)); var fire = IcAdd(t, "FiendFire"); IcAdd(t, "DefendIronclad"); IcMove(t, IcAdd(t, "Anger"), "Hand", "DrawPile"); IcMove(t, IcAdd(t, "Anger"), "Hand", "DrawPile"); IcPlay(t, fire);
            Equal("victory", Outcome(t), "settlement follows source self-exhaust"); Equal(2, Hand(t).Count, "all exhaust draws completed on final hit");
        });
        check("IC combat lifecycle: temporary growth, play history, powers and energy modifiers never leak to next combat", () =>
        {
            var s = Start(package); Submit(s, "Rampage.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "Draw"); var c = Hand(s).Single(); IcPlay(s, c);
            Set(s, Player(s), "Player", "Energy", Value.Of(10L)); IcPlay(s, IcAdd(s, "Pyre")); IcNextTurn(s); Equal(4L, Energy(s), "Pyre in previous combat");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(3L, Energy(s), "fresh combat has base energy"); Equal(0, Members(s, "CardPlays").Count, "history cleared");
            Equal(0L, IntField(s, Members(s, "DrawPile").Single(), "Card", "DamageGrowth"), "new permanent copy discards combat growth");
        });
        check("IC Colossus incoming modifier and Rupture/Inferno player-side qualification", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("incoming-probe.spire", """
                action Incoming { role = player body = {
                  call = { procedure = DamagePlayerPowered args = { attacker = first(type(Enemy)) owner = first(type(Player)) amount = 11 } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            IcPlay(s, IcAdd(s, "Tremble")); IcPlay(s, IcAdd(s, "Colossus")); Set(s, Player(s), "Player", "Block", Value.Of(0L)); Submit(s, "Incoming"); Equal(75L, Hp(s), "vulnerable attacker's eleven halved to five");
            Set(s, Player(s), "Player", "Energy", Value.Of(5L)); IcPlay(s, IcAdd(s, "Rupture")); IcPlay(s, IcAdd(s, "Inferno")); Set(s, Player(s), "Player", "EnemyPhase", Value.Of(true));
            Submit(s, "Incoming"); Equal(0L, IcPower(s, "damage-additive"), "enemy-side damage does not trigger Rupture"); Equal(40L, EnemyHp(s), "enemy-side damage does not trigger Inferno"); Equal(2L, IntField(s, Player(s), "Player", "HpLossCount"), "both positive damage events counted");
        });
        check("IC lethal self damage: Inferno cannot change a dead player's defeat into victory", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "Inferno")); Set(s, Player(s), "Player", "Hp", Value.Of(1L)); Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(6L));
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); Equal("defeat", Outcome(s), "player death takes priority"); Equal(0L, Hp(s), "no implicit revival");
        });
        check("IC definition boundary: unsupported cards fail before creating any runtime instance", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("unsupported-card.spire", """
                action UnsupportedCard { role = player locals = { card = Binding } body = { call = { procedure = CreateCombatCard args = { owner = first(type(Player)) definition = "base-spire:abrasive" } into = card } } }
                """);
            var s = Start(copy.Load()); var before = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Count;
            Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:UnsupportedCard"), s.Participant(0), []));
            True(s.Fault!.Message.Contains("unsupported_capability", StringComparison.Ordinal), "explicit unsupported diagnostic");
            Equal(before, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Count, "no empty fallback entity");
        });
        check("IC merged power instances: self-damage applications accumulate and single-stack powers stay single", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "CrimsonMantle")); IcPlay(s, IcAdd(s, "CrimsonMantle", true));
            IcPlay(s, IcAdd(s, "Corruption")); IcPlay(s, IcAdd(s, "Corruption")); Equal(1L, IcPower(s, "corruption"), "single source stack");
            IcNextTurn(s); Equal(78L, Hp(s), "two applications cause two self damage"); Equal(18L, Block(s), "normal and upgraded amounts merged");
        });
    }
}
