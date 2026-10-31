using CoreTheSpire;
using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunContentModelOnly(DslPackage package)
    {
        var count = 0;
        void Check(string name, Action body) { body(); count++; Console.WriteLine("ok   " + name); }
        RunContentModelChecks(package, Check);
        RunEventDeckChecks(package, Check);
        Console.WriteLine($"All {count} focused content-model checks passed; run full check for coverage evidence.");
        return 0;
    }

    private static void RunContentModelChecks(DslPackage package, Action<string, Action> check)
    {
        RunPowerAmountChecks(package, check);
        const string commands = """
            action StrengthPlus { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = 2 } } } }
            action StrengthMinus { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = -2 } } } }
            action Unknown { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "probe:missing" stacks = 1 } } } }
            action Clear { role = player body = { call = { procedure = ClearCombatPowers } } }
            action RemoveFirst { role = player body = { call = { procedure = RemovePower args = { power = first(collection(Powers)) } } } }
            action TwoHolders { role = player locals = { p = Binding } body = { call = { procedure = ApplyPower args = { player = first(type(Player)) enemy = first(type(Enemy)) definition = "base-spire:strength-power" stacks = 1 untilTurnEnd = false } into = p } } }
            action AddSecond { role = player locals = { p = Binding } body = { create = { type = Player scope = frameScope() into = p } call = { procedure = ApplyPlayerPower args = { owner = p definition = "base-spire:strength-power" stacks = 7 } } } }
            """;
        check("MODEL definition-owned identity, negative amounts, zero removal and holder isolation", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("model-commands.spire", commands);
            var s = Start(copy.Load()); Submit(s, "StrengthMinus"); Equal(-2L, IcPower(s, "base-spire:strength-power"), "new negative strength");
            Submit(s, "StrengthPlus"); Equal(0, Members(s, "Powers").Count, "zero amount removes instance");
            Submit(s, "StrengthPlus"); Submit(s, "StrengthPlus"); Equal(1, Members(s, "Powers").Count, "same definition and holder merge");
            Equal(4L, IcPower(s, "base-spire:strength-power"), "merged amount"); Submit(s, "AddSecond");
            Equal(2, Members(s, "Powers").Count, "same definition on different holders remains separate");
            Submit(s, "RemoveFirst"); Equal(7L, IcPower(s, "base-spire:strength-power"), "other holder remains");
        });
        check("MODEL unknown definitions and invalid holders fail before creating instances", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("model-commands.spire", commands);
            foreach (var command in new[] { "Unknown", "TwoHolders" })
            {
                var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget");
                Throws<EngineFaultException>(() => Submit(s, command)); Equal(0, Members(s, "Powers").Count, "invalid command created no Power");
            }
            using var role = new Copy(package.Root);
            role.Include("wrong-role.spire", commands.Replace("stacks = 2", "stacks = 2 kind = \"damage-additive\"", StringComparison.Ordinal));
            Throws<DslSourceException>(() => role.Load());
        });
        check("MODEL private state is instance-owned and cleaned on removal and combat clear", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("model-commands.spire", commands);
            var s = Start(copy.Load()); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            IcPlay(s, IcAdd(s, "Juggling"));
            var state = s.RuleSet.TypeByQualifiedId("base-spire:JugglingPower.State");
            Equal(1, s.State.AllInstances(state.Id).Count(), "created private state"); Submit(s, "RemoveFirst");
            Equal(0, s.State.AllInstances(state.Id).Count(), "explicit removal cleans state");
            Set(s, Player(s), "Player", "Energy", Value.Of(10L)); IcPlay(s, IcAdd(s, "Juggling")); Submit(s, "Clear");
            Equal(0, s.State.AllInstances(state.Id).Count(), "combat clear uses same removal lifecycle");
        });
        check("MODEL a new Power owns registration, private state and hooks in one content module", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("owned-model.spire", """
                template LocalModel { body = {
                  use Definition { template = PowerDefinition args = { definition = "probe:local" } }
                  entity State { fields = { Power = { type = Ref(Power) visibility = Hidden } } }
                  procedure Created { parameters = { power = Ref(Power) listener = Ref(Power) } locals = { state = Binding } body = { create = { type = State scope = entityScope(power) into = state fields = { Power = power } } } }
                  subscription Created { event = PowerCreated reaction = Created instances = where(collection(Powers), p, and(equal(p, power), equal(field(p, Power.DefinitionId), "probe:local"))) }
                  procedure Removed { parameters = { power = Ref(Power) } body = { forEach = { query = where(type(State), s, equal(field(s, State.Power), power)) item = state body = { destroy = { target = state } } } } }
                  subscription Removed { event = PowerRemoved reaction = Removed }
                  procedure Cleared { parameters = { owner = Ref(Player) listener = Ref(Power) } body = { call = { procedure = GainUnpoweredBlock args = { owner = owner amount = field(listener, Power.Stacks) } } call = { procedure = RemovePower args = { power = listener } } } }
                  subscription Cleared { event = AfterBlockCleared reaction = Cleared instances = where(collection(Powers), p, and(equal(field(p, Power.HolderPlayer), owner), equal(field(p, Power.DefinitionId), "probe:local"))) }
                } }
                use Local { template = LocalModel }
                action LocalApply { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "probe:local" stacks = 4 } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "LocalApply");
            Equal(0L, Block(s), "deferred behavior"); IcNextTurn(s); Equal(4L, Block(s), "owned hook executed");
            Equal(0, Members(s, "Powers").Count, "owned self removal");
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Local.State").Id).Count(), "private state gone");
        });
        check("MODEL one card damage value drives initialization, upgrade, execution and damage readers", () =>
        {
            using var copy = new Copy(package.Root);
            var path = copy.Path("cards/ironclad/strike-ironclad.spire");
            File.WriteAllText(path, File.ReadAllText(path).Replace("baseDamage = 6", "baseDamage = 12", StringComparison.Ordinal));
            foreach (var upgraded in new[] { false, true })
            {
                var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                var card = IcAdd(s, "StrikeIronclad", upgraded); Equal(upgraded ? 9L : 12L, IntField(s, card, "Card", "BaseDamage"), "definition initializes canonical value");
                var hp = IntField(s, FirstTarget(s), "Enemy", "Hp"); IcPlay(s, card);
                Equal(upgraded ? 9L : 12L, hp - IntField(s, FirstTarget(s), "Enemy", "Hp"), "effect reads canonical value");
            }
        });
    }
}
