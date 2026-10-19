using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunSelfFormingClayChecks(DslPackage package, Action<string, Action> check)
    {
        const string id = "base-spire:self-forming-clay";
        var names = new List<string>();
        void Case(string name, Action body) { check(name, body); names.Add(name); }
        using var copy = new Copy(package.Root);
        copy.Include("clay-probes.spire", """
            action Hit { role = player body = { call = { procedure = DamagePlayer args = { owner = first(type(Player)) amount = 2 } } } }
            action ZeroHit { role = player body = { call = { procedure = DamagePlayer args = { owner = first(type(Player)) amount = 0 } } } }
            action LoseHp { role = player body = { call = { procedure = LosePlayerHp args = { owner = first(type(Player)) amount = 1 } } } }
            action Modifiers { role = player body = {
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:dexterity-power" stacks = 20 } }
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:frail-power" stacks = 20 } }
            } }
            action OtherHolder { role = player locals = { p = Binding } body = {
              create = { type = Player scope = frameScope() into = p }
              call = { procedure = ObtainRelic args = { owner = p definition = "base-spire:self-forming-clay" } }
            } }
            """);
        var variant = copy.Load();
        Session Combat(bool obtain = true)
        {
            var s = Start(variant); if (obtain) Submit(s, "SelfFormingClay.Obtain");
            Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); return s;
        }
        Case("CLAY positive damage accumulates one deferred Power and resolves after block clear once", () =>
        {
            var s = Combat(); Submit(s, "Hit"); Submit(s, "Hit");
            Equal(76L, Hp(s), "two positive damage results"); Equal(0L, Block(s), "no immediate block");
            Equal(6L, IcPower(s, id), "three per result"); Equal(1, Members(s, "Powers").Count, "merge same definition and holder");
            Submit(s, "EndDevelopmentTurn"); Equal(0L, Block(s), "not at turn end"); Submit(s, "BeginDevelopmentTurn");
            Equal(6L, Block(s), "after clearing previous block"); Equal(0L, IcPower(s, id), "self removed");
            IcNextTurn(s); Equal(0L, Block(s), "no repeated reward");
        });
        Case("CLAY blocked and zero hits do not trigger; partial damage and unblockable HP loss do", () =>
        {
            var s = Combat(); Set(s, Player(s), "Player", "Block", Value.Of(2L)); Submit(s, "Hit"); Submit(s, "ZeroHit");
            Equal(80L, Hp(s), "fully blocked and zero"); Equal(0L, IcPower(s, id), "no false damage result");
            Set(s, Player(s), "Player", "Block", Value.Of(1L)); Submit(s, "Hit"); Equal(79L, Hp(s), "partial unblocked damage");
            Submit(s, "LoseHp"); Equal(6L, IcPower(s, id), "HP loss shares source unblockable damage path");
        });
        Case("CLAY absent, melted, removed, other-holder and out-of-combat instances are inactive", () =>
        {
            var bare = Combat(false); Submit(bare, "Hit"); Equal(0L, IcPower(bare, id), "absent");
            Submit(bare, "OtherHolder"); Submit(bare, "Hit"); Equal(0L, IcPower(bare, id), "other holder");
            foreach (var melt in new[] { false, true })
            {
                var s = Combat(); var relic = Members(s, "Relics").Single();
                if (melt) Set(s, relic, "Relic", "IsMelted", Value.Of(true)); else Submit(s, "RemoveOwnedRelic", relic);
                Submit(s, "Hit"); Equal(0L, IcPower(s, id), "inactive inventory instance");
            }
            var outside = Start(variant); Submit(outside, "SelfFormingClay.Obtain"); Submit(outside, "Hit");
            Equal(0L, IcPower(outside, id), "combat guard");
        });
        Case("CLAY unpowered block ignores Dexterity and Frail; committed Power survives relic removal", () =>
        {
            var s = Combat(); Submit(s, "Hit"); Submit(s, "Modifiers"); Submit(s, "RemoveOwnedRelic", Members(s, "Relics").Single());
            IcNextTurn(s); Equal(3L, Block(s), "three unpowered block, existing Power owns its lifetime");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal(0L, IcPower(s, id), "no cross-combat deferred state");
        });
        Case("CLAY source hook fires with retained block and on turn one; combat clear discards pending benefit", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "Barricade")); Submit(s, "Hit"); IcNextTurn(s);
            Equal(3L, Block(s), "CombatManager publishes hook even when Barricade prevents clear"); Equal(0L, IcPower(s, id), "Power resolves normally");
            Submit(s, "LoseHp"); Equal(3L, IcPower(s, id), "new pending benefit");
            Submit(s, "EndDevelopmentCombat"); Equal(0L, IcPower(s, id), "combat clear removes pending Power");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); IcNextTurn(s); Equal(0L, Block(s), "old benefit cannot cross combats");
            var first = Start(variant); Submit(first, "SelfFormingClay.Obtain"); Submit(first, "BeginDevelopmentCombat"); Submit(first, "Hit");
            Submit(first, "BeginDevelopmentTurn"); Equal(3L, Block(first), "first turn still publishes hook");
        });
        Case("CLAY a one-file amount edit changes actual behavior without shared rules", () =>
        {
            using var changed = new Copy(copy.Path("."));
            var path = changed.Path("relics/shared/self-forming-clay.spire");
            File.WriteAllText(path, File.ReadAllText(path).Replace("stacks = 3", "stacks = 4", StringComparison.Ordinal));
            var changedPackage = changed.Load(); True(changedPackage.Digest != variant.Digest, "numeric edit changes package identity");
            var s = Start(changedPackage); Submit(s, "SelfFormingClay.Obtain"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "Hit");
            Equal(4L, IcPower(s, id), "actual new amount"); IcNextTurn(s); Equal(4L, Block(s), "actual deferred reward");
        });
        Case("CLAY block-triggered victory still completes its owned Power removal", () =>
        {
            var s = Combat(); IcPlay(s, IcAdd(s, "Juggernaut")); Submit(s, "Hit");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1L)); IcNextTurn(s);
            Equal("victory", Outcome(s), "block gain triggers Juggernaut and real combat close");
            Equal(0L, IcPower(s, id), "Clay removes itself after the nested victory notification");
            Equal(1, Members(s, "Powers").Count, "other powers keep the existing development cleanup boundary");
        });
        return new(StringComparer.Ordinal) { [id] = names.ToArray() };
    }
}
