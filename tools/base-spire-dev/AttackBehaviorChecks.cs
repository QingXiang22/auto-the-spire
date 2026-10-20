using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunAttackBehaviorChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Case(string name, Action body, params string[] relics)
        {
            check(name, body);
            foreach (var relic in relics)
            {
                if (!witnesses.TryGetValue("base-spire:" + relic, out var rows)) witnesses["base-spire:" + relic] = rows = [];
                rows.Add(name);
            }
        }
        Session Combat(DslPackage? configured = null)
        {
            var s = Start(configured ?? package); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L)); Set(s, Player(s), "Player", "Energy", Value.Of(100L)); return s;
        }
        Case("ATTACK AoE commits each hit to all targets before results and owns one attack boundary", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("observe-hits.spire", """
                procedure Observe { parameters = { result = Ref(DamageResult) } body = {
                  if = { condition = and(equal(field(result, DamageResult.Hit), 0), equal(field(result, DamageResult.Enemy), first(type(Enemy)))) then = {
                    set = { target = first(type(Player)) field = Player.Gold value = field(first(where(type(Enemy), e, not(equal(e, first(type(Enemy)))))), Enemy.Hp) }
                  } }
                } }
                subscription Observe { event = DamageResolved reaction = Observe }
                """);
            var s = Combat(copy.Load()); Submit(s, "AddDevelopmentTarget");
            var targets = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).ToArray();
            foreach (var target in targets) Set(s, target, "Enemy", "Hp", Value.Of(100L));
            Set(s, Player(s), "Player", "Energy", Value.Of(2L)); IcPlay(s, IcAdd(s, "Whirlwind"));
            Equal(95L, IntField(s, Player(s), "Player", "Gold"), "second target already committed at first result hook");
            foreach (var target in targets) Equal(90L, IntField(s, target, "Enemy", "Hp"), "two hits per target");
            var attack = Members(s, "Attacks").Single(); Equal(2L, IntField(s, attack, "Attack", "Hit"), "one attack with two hits");
            var results = Members(s, "DamageResults"); Equal(4, results.Count, "one result per hit and target");
            Equal("0,0,1,1", string.Join(',', results.Select(r => IntField(s, r, "DamageResult", "Hit"))), "hit-major result order");
        });
        Case("ATTACK Akabeko Vigor captures attack-start stacks and preserves layers added between hits", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("add-vigor-mid-attack.spire", """
                procedure Add { parameters = { result = Ref(DamageResult) } body = {
                  if = { condition = and(not(isNone(field(result, DamageResult.Attack))), equal(field(result, DamageResult.Hit), 0)) then = {
                    call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:vigor-power" stacks = 4 } }
                  } }
                } }
                subscription Add { event = DamageResolved reaction = Add }
                """);
            var s = Start(copy.Load()); Submit(s, "Akabeko.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(100L));
            Equal(8L, IcPower(s, "base-spire:vigor-power"), "first side turn grants eight");
            IcPlay(s, IcAdd(s, "TwinStrike")); Equal(70L, EnemyHp(s), "13 then 17 while the original attack stays active");
            Equal(4L, IcPower(s, "base-spire:vigor-power"), "only the original eight stacks consumed at AfterAttack");
            Equal(1, Members(s, "Attacks").Count, "multi-hit is a single command");
        }, "akabeko");
        Case("ATTACK PenNib tenth play doubles all hits and preview is pure before activation", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("pen-preview.spire", """
                action Preview { role = player requires = and(exists(collection(Hand)), equal(pureCall(PreviewPoweredCardDamage, first(type(Player)), first(collection(Hand)), first(type(Enemy))), 12)) body = {} }
                """);
            var s = Combat(copy.Load()); Submit(s, "PenNib.Obtain");
            for (var i = 0; i < 9; i++) IcPlay(s, IcAdd(s, "StrikeIronclad"));
            var next = IcAdd(s, "StrikeIronclad");
            var hash = s.State.StateHash(); var version = s.StateVersion; var draws = s.Random.TotalDraws;
            for (var i = 0; i < 3; i++) True(s.LegalActions(s.Participant(0)).Any(a => a.Definition.QualifiedId == "base-spire:Preview"), "preview includes pending tenth-play multiplier");
            Equal(hash, s.State.StateHash(), "preview cannot create transient entities or change counters"); Equal(version, s.StateVersion, "preview version"); Equal(draws, s.Random.TotalDraws, "preview random");
            var hp = EnemyHp(s); IcPlay(s, next); Equal(12L, hp - EnemyHp(s), "actual tenth play matches preview");
            hp = EnemyHp(s); IcPlay(s, IcAdd(s, "TwinStrike")); Equal(10L, hp - EnemyHp(s), "following two-hit card has no leaked multiplier");
            for (var i = 0; i < 8; i++) IcPlay(s, IcAdd(s, "StrikeIronclad"));
            hp = EnemyHp(s); IcPlay(s, IcAdd(s, "TwinStrike")); Equal(20L, hp - EnemyHp(s), "tenth multi-hit play doubles both hits");
        }, "pen-nib");
        Case("ATTACK PenNib count survives combat and inactive relics clean or suppress their state", () =>
        {
            foreach (var removed in new[] { false, true })
            {
                var s = Combat(); Submit(s, "PenNib.Obtain");
                for (var i = 0; i < 9; i++) IcPlay(s, IcAdd(s, "StrikeIronclad"));
                Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                Set(s, Player(s), "Player", "Energy", Value.Of(10L));
                if (removed) Submit(s, "RemoveOwnedRelic", Members(s, "Relics").Single());
                var hp = EnemyHp(s); IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(removed ? 6L : 12L, hp - EnemyHp(s), "long-term count or removed state");
                var state = s.RuleSet.TypeByQualifiedId("base-spire:PenNib.State");
                Equal(removed ? 0 : 1, s.State.AllInstances(state.Id).Count, "relic-owned state lifecycle");
            }
        }, "pen-nib");
        Case("ATTACK damage result distinguishes block, actual HP loss and overkill", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("damage-result.spire", """
                action Hit { role = player body = { call = { procedure = DamagePlayer args = { owner = first(type(Player)) amount = 10 } } } }
                """);
            var s = Combat(copy.Load()); Set(s, Player(s), "Player", "Hp", Value.Of(3L)); Set(s, Player(s), "Player", "Block", Value.Of(4L)); Submit(s, "Hit");
            var result = Members(s, "DamageResults").Single(); Equal(4L, IntField(s, result, "DamageResult", "Blocked"), "absorbed by block");
            Equal(3L, IntField(s, result, "DamageResult", "Unblocked"), "actual HP lost"); Equal(3L, IntField(s, result, "DamageResult", "Overkill"), "overkill is separate");
            var type = s.RuleSet.TypeByQualifiedId("base-spire:DamageResult"); True(s.State.GetField(result, type.FieldIndex("Card")).IsNone, "direct damage has no inferred current card");
        });
        Case("ATTACK enemy Vigor is holder-specific and pre-damage reactions cannot overwrite newer HP", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("enemy-vigor.spire", """
                procedure HealBefore { parameters = { owner = Ref(Player) attacker = Ref(Enemy) } body = { call = { procedure = Heal args = { owner = owner amount = 5 } } } }
                subscription HealBefore { event = BeforePlayerAttacked reaction = HealBefore }
                action Hit { role = player body = {
                  call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:vigor-power" stacks = 3 } }
                  call = { procedure = ApplyEnemyPower args = { target = first(where(type(Enemy), e, not(equal(e, first(type(Enemy)))))) definition = "base-spire:vigor-power" stacks = 7 } }
                  call = { procedure = DamagePlayerPowered args = { attacker = first(type(Enemy)) owner = first(type(Player)) amount = 8 } }
                } }
                """);
            var s = Combat(copy.Load()); Submit(s, "AddDevelopmentTarget"); Set(s, Player(s), "Player", "Hp", Value.Of(20L)); Submit(s, "Hit");
            Equal(14L, Hp(s), "heal to 25 before receiving eleven actual damage");
            var remaining = Members(s, "Powers").Single(); Equal(7L, IntField(s, remaining, "Power", "Stacks"), "other enemy Vigor is not consumed through two empty player refs");
            Equal(11L, IntField(s, Members(s, "DamageResults").Single(), "DamageResult", "Unblocked"), "authoritative actual HP loss");
        });
        Case("ATTACK unpowered commands retain attack boundaries while powered retaliation stops later hits", () =>
        {
            foreach (var powered in new[] { false, true })
            {
                using var copy = new Copy(package.Root); copy.Include("attack-properties.spire", """
                    entity Boundaries { fields = { Before = { type = Int default = 0 } After = { type = Int default = 0 } } }
                    procedure Before { parameters = { attack = Ref(Attack) } body = { set = { target = first(type(Boundaries)) field = Boundaries.Before value = add(field(first(type(Boundaries)), Boundaries.Before), 1) } } }
                    procedure After { parameters = { attack = Ref(Attack) } body = { set = { target = first(type(Boundaries)) field = Boundaries.After value = add(field(first(type(Boundaries)), Boundaries.After), 1) } } }
                    subscription Before { event = BeforeAttack reaction = Before }
                    subscription After { event = AfterAttack reaction = After }
                    action Hit { role = player locals = { counters = Binding attack = Binding } body = {
                      create = { type = Boundaries scope = frameScope() into = counters }
                      call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:vigor-power" stacks = 3 } }
                      create = { type = Attack scope = frameScope() into = attack collection = Attacks fields = { Enemy = first(type(Enemy)) Defender = first(type(Player)) Amount = 8 Hits = 2 Mode = "single" Powered = POWERED } }
                      call = { procedure = ExecuteAttack args = { attack = attack } }
                    } }
                    """.Replace("POWERED", powered ? "true" : "false", StringComparison.Ordinal));
                var s = Start(copy.Load()); Submit(s, "BronzeScales.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(3L)); Set(s, Player(s), "Player", "Hp", Value.Of(20L)); Submit(s, "Hit");
                Equal(powered ? 9L : 4L, Hp(s), "powered first hit still commits after retaliation; unpowered hits neither gain Vigor nor trigger Thorns");
                Equal(powered ? 0L : 3L, EnemyHp(s), "retaliation has the source powered-attack qualification");
                if (powered)
                {
                    var resultType = s.RuleSet.TypeByQualifiedId("base-spire:DamageResult");
                    var retaliation = Members(s, "DamageResults").Single(r => !s.State.GetField(r, resultType.FieldIndex("Enemy")).IsNone);
                    Equal(Player(s), s.State.GetField(retaliation, resultType.FieldIndex("DealerPlayer")).AsRef(), "Thorns has an explicit dealer even though it has no attack or card source");
                    True(s.State.GetField(retaliation, resultType.FieldIndex("Card")).IsNone, "retaliation cannot inherit a resolving card");
                }
                Equal(powered ? 1L : 2L, IntField(s, Members(s, "Attacks").Single(), "Attack", "Hit"), "dead attacker cannot perform the next hit");
                var boundary = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Boundaries").Id).Single();
                Equal(1L, IntField(s, boundary, "Boundaries", "Before"), "one attack start even when unpowered"); Equal(1L, IntField(s, boundary, "Boundaries", "After"), "one cleanup after the attack");
            }
        });
        Case("ATTACK direct Mercury damage commits one non-attack batch before notifications and victory", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("direct-batch.spire", """
                procedure Observe { parameters = { result = Ref(DamageResult) } body = {
                  if = { condition = exists(where(type(Enemy), e, greater(field(e, Enemy.Hp), 0))) then = { fail = { message = "Direct batch notified before all targets committed" } } }
                } }
                subscription Observe { event = DamageResolved reaction = Observe }
                procedure Died { parameters = { target = Ref(Enemy) } body = {
                  call = { procedure = CheckCombatVictory args = { owner = first(type(Player)) } }
                  if = { condition = not(field(first(type(Player)), Player.CombatActive)) then = { fail = { message = "Nested victory check escaped the direct damage batch" } } }
                } }
                subscription Died { event = EnemyDied reaction = Died }
                procedure NoAttack { parameters = { attack = Ref(Attack) } body = { fail = { message = "Direct damage cannot create an attack boundary" } } }
                subscription NoAttack { event = BeforeAttack reaction = NoAttack }
                """);
            var s = Start(copy.Load()); Submit(s, "MercuryHourglass.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            foreach (var enemy in s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id)) Set(s, enemy, "Enemy", "Hp", Value.Of(3L));
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal("victory", Outcome(s), "victory settles after all result and death callbacks");
            var type = s.RuleSet.TypeByQualifiedId("base-spire:DamageResult"); var results = Members(s, "DamageResults"); Equal(2, results.Count, "one direct result per target");
            Equal(1, results.Select(r => s.State.GetField(r, type.FieldIndex("Batch")).AsRef()).Distinct().Count(), "one real damage batch");
            True(results.All(r => s.State.GetField(r, type.FieldIndex("Attack")).IsNone && s.State.GetField(r, type.FieldIndex("Card")).IsNone), "no inferred attack or card");
            True(results.All(r => s.State.GetField(r, type.FieldIndex("DealerPlayer")).AsRef() == Player(s)), "explicit player source on each result");
            Equal(0L, IntField(s, Player(s), "Player", "DamageDepth"), "batch boundary unwinds");
            Submit(s, "CloseDevelopmentRewards"); Submit(s, "BeginDevelopmentCombat"); Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:DamageBatch").Id).Count, "next combat clears direct batches after their results");
        }, "mercury-hourglass");
        Case("ATTACK autoplay freezes parent batch before nested Havoc selects its own card", () =>
        {
            var s = Combat(); var havoc = IcAdd(s, "Havoc"); var defend = IcAdd(s, "DefendIronclad"); var strike = IcAdd(s, "StrikeIronclad");
            IcMove(s, havoc, "Hand", "DrawPile"); IcMove(s, defend, "Hand", "DrawPile"); IcMove(s, strike, "Hand", "DrawPile");
            var cascade = IcAdd(s, "Cascade"); Set(s, Player(s), "Player", "Energy", Value.Of(2L)); IcPlay(s, cascade);
            True(Members(s, "ExhaustPile").Contains(strike), "nested Havoc picks the card outside the already frozen parent batch");
            True(Members(s, "DiscardPile").Contains(defend), "second parent member resolves normally"); Equal(5L, Block(s), "Defend executed once");
            Equal(994L, EnemyHp(s), "nested Strike executed once");
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:AutoPlayBatch").Id).Count, "batch entities cleaned");
        });
        return witnesses.ToDictionary(p => p.Key, p => p.Value.ToArray(), StringComparer.Ordinal);
    }
}
