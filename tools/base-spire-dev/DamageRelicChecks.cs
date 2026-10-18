using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunDamageChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new List<string>();
        void Case(string name, Action body) { check(name, body); witnesses.Add(name); }

        Case("DD01: development targets exist only after the explicit action and carry their declared identity", () =>
        {
            var s = Start(package);
            Equal(0, Targets(s).Count, "no enemy without the development action");
            Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget");
            Equal(2, Targets(s).Count, "each call creates one target");
            Equal("base-spire:development-target", TargetId(s), "declared development identity");
            Equal(40L, EnemyHp(s), "declared development HP"); Equal(0L, EnemyBlock(s), "targets start without block");
            True(Legal(s, "AddDevelopmentTarget"), "the development action stays a real public candidate");
        });
        Case("DD02: enemy damage absorbs block first, then HP, and never drops below zero", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("damage-probes.spire", """
                action DamageFirstTarget { role = player body = { call = { procedure = DamageEnemy args = { target = first(type(Enemy)) amount = 5 } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget");
            Submit(s, "DamageFirstTarget"); Equal(35L, EnemyHp(s), "unblocked damage reduces HP");
            Set(s, FirstTarget(s), "Enemy", "Block", Value.Of(10L));
            Submit(s, "DamageFirstTarget"); Equal(5L, EnemyBlock(s), "block absorbs the whole hit"); Equal(35L, EnemyHp(s), "HP untouched while block holds");
            Set(s, FirstTarget(s), "Enemy", "Block", Value.Of(2L));
            Submit(s, "DamageFirstTarget"); Equal(0L, EnemyBlock(s), "block is consumed exactly"); Equal(32L, EnemyHp(s), "only the remainder reaches HP");
            for (var hit = 0; hit < 10; hit++) Submit(s, "DamageFirstTarget");
            Equal(0L, EnemyHp(s), "HP floors at zero"); Equal(0L, EnemyBlock(s), "no block remains");
            Submit(s, "DamageFirstTarget"); Equal(0L, EnemyHp(s), "a dead target stays at zero");
        });
        Case("DD03: player damage uses the same block-then-HP order and floors at zero", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("player-damage.spire", """
                action HurtSelf { role = player body = { call = { procedure = DamagePlayer args = { owner = first(type(Player)) amount = 7 } } } }
                """);
            var s = Start(copy.Load());
            Submit(s, "HurtSelf"); Equal(73L, Hp(s), "unblocked damage reduces player HP");
            Set(s, Player(s), "Player", "Block", Value.Of(5L));
            Submit(s, "HurtSelf"); Equal(0L, Block(s), "player block absorbs first"); Equal(71L, Hp(s), "only the remainder reaches HP");
            for (var hit = 0; hit < 15; hit++) Submit(s, "HurtSelf");
            Equal(0L, Hp(s), "player HP floors at zero"); Equal(80L, Field(s, "MaxHp"), "MaxHp is not touched");
        });
        Case("DD04: the player turn-start time point runs before the late one", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("turn-order.spire", """
                procedure CheckLateDamage { parameters = { owner = Ref(Player) } body = {
                  if = { condition = not(equal(field(first(type(Enemy)), Enemy.Hp), 37)) then = { fail = { message = "late turn start ran before the player turn start damage" } } }
                } }
                subscription CheckLateDamage { event = AfterPlayerTurnStartLate reaction = CheckLateDamage }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "MercuryHourglass.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal(37L, EnemyHp(s), "the player turn start listener already dealt its damage");
        });
        Case("DD05: the damage template ignores absent, removed, melted and other-owner relics, and empty enemy sets", () =>
        {
            var empty = Start(package); Submit(empty, "MercuryHourglass.Obtain");
            Submit(empty, "BeginDevelopmentCombat"); Submit(empty, "BeginDevelopmentTurn");
            Equal(3L, Energy(empty), "no targets means no fault and no side effect");
            foreach (var variant in new[] { "removed", "melted" })
            {
                var s = Start(package); Submit(s, "AddDevelopmentTarget"); Submit(s, "MercuryHourglass.Obtain");
                if (variant == "removed") Submit(s, "RemoveOwnedRelic", Members(s, "Relics").Single());
                else Set(s, Members(s, "Relics").Single(), "Relic", "IsMelted", Value.Of(true));
                Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                Equal(40L, EnemyHp(s), variant + " instance does not damage targets");
            }
            using var copy = new Copy(package.Root);
            copy.Include("damage-owners.spire", """
                procedure MakeDamageOwner { locals = { other = Binding } body = {
                  create = { type = Player scope = frameScope() into = other }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:mercury-hourglass" } }
                } }
                action MakeDamageOwner { role = player body = { call = { procedure = MakeDamageOwner } } }
                action BeginDamageOwnerTurn { role = player body = {
                  call = { procedure = BeginDevelopmentCombat args = { owner = first(skip(type(Player), 1)) } }
                  call = { procedure = BeginDevelopmentTurn args = { owner = first(skip(type(Player), 1)) } }
                } }
                """);
            var owners = Start(copy.Load()); Submit(owners, "AddDevelopmentTarget"); Submit(owners, "MakeDamageOwner");
            Submit(owners, "BeginDevelopmentCombat"); Submit(owners, "BeginDevelopmentTurn");
            Equal(40L, EnemyHp(owners), "another owner's relic does not fire on this turn");
            Submit(owners, "EndDevelopmentTurn"); Submit(owners, "BeginDamageOwnerTurn");
            Equal(37L, EnemyHp(owners), "the owning player's turn start deals the damage");
        });

        var own = "DD05 MercuryHourglass: three unpowered damage to every hittable enemy on each own turn start";
        check(own, () =>
        {
            var s = Start(package); Submit(s, "AddDevelopmentTarget"); Submit(s, "AddDevelopmentTarget"); Submit(s, "MercuryHourglass.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Equal(40L, EnemyHp(s), "no damage at combat start");
            Submit(s, "BeginDevelopmentTurn");
            var targets = Targets(s);
            Equal(37L, s.State.GetField(targets[0], EnemyField(s, "Hp")).AsInt(), "first target damaged");
            Equal(37L, s.State.GetField(targets[1], EnemyField(s, "Hp")).AsInt(), "second target damaged");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(34L, s.State.GetField(targets[0], EnemyField(s, "Hp")).AsInt(), "damage repeats on the next turn");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal(31L, s.State.GetField(targets[0], EnemyField(s, "Hp")).AsInt(), "the held relic damages again in a new combat");
        });

        var all = new List<string>(witnesses) { own };
        return new Dictionary<string, string[]>(StringComparer.Ordinal) { ["base-spire:mercury-hourglass"] = all.ToArray() };
    }

    private static List<InstanceId> Targets(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Enemy").Id).ToList();
    private static InstanceId FirstTarget(Session s) => Targets(s).First();
    private static int EnemyField(Session s, string name) => s.RuleSet.TypeByQualifiedId("base-spire:Enemy").FieldIndex(name);
    private static string TargetId(Session s) => s.State.GetField(FirstTarget(s), EnemyField(s, "DefinitionId")).AsString();
    private static long EnemyHp(Session s) => s.State.GetField(FirstTarget(s), EnemyField(s, "Hp")).AsInt();
    private static long EnemyBlock(Session s) => s.State.GetField(FirstTarget(s), EnemyField(s, "Block")).AsInt();
}
