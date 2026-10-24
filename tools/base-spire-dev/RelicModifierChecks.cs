using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunRelicModifierChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Own(string id, string name, Action body) { check(name, body); witnesses[id] = witnesses.GetValueOrDefault(id, []).Append(name).ToArray(); }
        Session Combat(params string[] relics)
        {
            var s = Start(package); foreach (var r in relics) Submit(s, r + ".Obtain");
            Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L)); Set(s, FirstTarget(s), "Enemy", "MaxHp", Value.Of(1000L)); return s;
        }
        void Play(Session s, string card) { Set(s, Player(s), "Player", "Energy", Value.Of(30L)); IcPlay(s, IcAdd(s, card)); }
        InstanceId Relic(Session s, string id) => Members(s, "Relics").Single(r => s.State.GetField(r, s.RuleSet.TypeByQualifiedId("base-spire:Relic").FieldIndex("DefinitionId")).AsString() == id);
        foreach (var row in new[] { ("StrikeDummy", "base-spire:strike-dummy", 3L), ("FakeStrikeDummy", "base-spire:fake-strike-dummy", 1L) })
            Own(row.Item2, "RM " + row.Item1 + ": source bonus on each Strike hit, upgraded and computed attacks, no bonus on other attacks", () =>
            {
                var s = Combat(row.Item1); Play(s, "TwinStrike"); Equal(1000L - 2 * (5 + row.Item3), EnemyHp(s), "each hit receives source additive");
                Play(s, "Anger"); Equal(1000L - 2 * (5 + row.Item3) - 6, EnemyHp(s), "non-Strike is unaffected");
                Set(s, Player(s), "Player", "Energy", Value.Of(30L)); IcPlay(s, IcAdd(s, "StrikeIronclad", true));
                Equal(1000L - 2 * (5 + row.Item3) - 6 - 9 - row.Item3, EnemyHp(s), "upgraded base plus source bonus");
                var computed = Combat(row.Item1); Play(computed, "PerfectedStrike"); Equal(1000L - 8 - row.Item3, EnemyHp(computed), "computed Strike-count base also participates");
            });
        check("RM damage order: strength and both dummies precede weak, vulnerable, final floor and block", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("modifier-order.spire", """
                action SetModifiers { role = player body = {
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" stacks = 1 } }
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:weak-power" stacks = 1 } }
                  call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:vulnerable-power" stacks = 1 } }
                } }
                action Unpowered { role = player body = { call = { procedure = DamageEnemy args = { target = first(type(Enemy)) amount = 6 } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "StrikeDummy.Obtain"); Submit(s, "FakeStrikeDummy.Obtain"); Submit(s, "AddDevelopmentTarget");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "SetModifiers"); Set(s, FirstTarget(s), "Enemy", "Block", Value.Of(2L));
            IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(30L, EnemyHp(s), "floor((6+1+3+1)*.75*1.5)-2 = 10 HP loss");
            Submit(s, "Unpowered"); Equal(24L, EnemyHp(s), "unpowered damage bypasses all attack modifiers");
        });
        check("RM Strike modifiers: removed and melted instances stop adding, nested automatic and repeated plays retain their own source", () =>
        {
            foreach (var melt in new[] { false, true })
            {
                var s = Combat("StrikeDummy"); var r = Relic(s, "base-spire:strike-dummy");
                if (melt) Set(s, r, "Relic", "IsMelted", Value.Of(true)); else Submit(s, "RemoveOwnedRelic", r);
                Play(s, "TwinStrike"); Equal(990L, EnemyHp(s), "inactive modifier");
            }
            var nested = Combat("StrikeDummy"); var top = IcAdd(nested, "TwinStrike"); IcMove(nested, top, "Hand", "DrawPile");
            Play(nested, "Havoc"); Equal(984L, EnemyHp(nested), "skill autoplays Strike-tagged child"); Play(nested, "OneTwoPunch"); Play(nested, "StrikeIronclad");
            Equal(966L, EnemyHp(nested), "both repeated hits receive bonus");
        });
        Own("base-spire:ice-cream", "RM IceCream: first turn resets, later turns add maximum to leftovers, new combat clears leftovers", () =>
        {
            var s = Start(package); Submit(s, "IceCream.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat");
            Set(s, Player(s), "Player", "Energy", Value.Of(17L)); Submit(s, "BeginDevelopmentTurn"); Equal(3L, Energy(s), "first turn resets");
            IcPlay(s, IcAdd(s, "DefendIronclad")); IcNextTurn(s); Equal(5L, Energy(s), "two leftovers plus three");
            Set(s, Player(s), "Player", "MaxEnergy", Value.Of(4L)); IcNextTurn(s); Equal(9L, Energy(s), "dynamic maximum");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(4L, Energy(s), "no cross-combat carry");
        });
        Own("base-spire:art-of-war", "RM ArtOfWar: no first-turn bonus, consecutive skill-only turns reward, attacks suppress only the next turn", () =>
        {
            var s = Combat("ArtOfWar"); Equal(3L, Energy(s), "first turn no bonus"); IcPlay(s, IcAdd(s, "DefendIronclad"));
            IcNextTurn(s); Equal(4L, Energy(s), "skill-only turn earns one"); IcNextTurn(s); Equal(4L, Energy(s), "consecutive no-attack turn earns again");
            IcPlay(s, IcAdd(s, "TwinStrike")); IcNextTurn(s); Equal(3L, Energy(s), "multi-hit attack suppresses reward");
            IcNextTurn(s); Equal(4L, Energy(s), "empty turn restores reward");
            Play(s, "StrikeIronclad"); Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal(3L, Energy(s), "fresh combat first turn"); IcNextTurn(s); Equal(4L, Energy(s), "previous combat attack is cleared");
        });
        check("RM ArtOfWar: nested and end-turn automatic attacks prevent next-turn rewards", () =>
        {
            var nested = Combat("ArtOfWar"); var top = IcAdd(nested, "StrikeIronclad"); IcMove(nested, top, "Hand", "DrawPile");
            Play(nested, "Havoc"); IcNextTurn(nested); Equal(3L, Energy(nested), "nested child attack recorded");
            var end = Combat("ArtOfWar"); Play(end, "Stampede"); IcAdd(end, "StrikeIronclad"); IcNextTurn(end);
            Equal(994L, EnemyHp(end), "actual end-turn automatic attack occurred"); Equal(3L, Energy(end), "record transferred after end-turn attack");
        });
        check("RM energy reset: IceCream adds before ArtOfWar, no-energy-gain suppresses award but not reset, inactive relics stop", () =>
        {
            var both = Combat("IceCream", "ArtOfWar"); IcNextTurn(both); Equal(7L, Energy(both), "three carried plus three maximum plus one award");
            using var copy = new Copy(package.Root); copy.Include("no-energy.spire", """
                action BlockEnergy { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:no-energy-gain" stacks = 1 } } } }
                """);
            var blocked = Start(copy.Load()); Submit(blocked, "IceCream.Obtain"); Submit(blocked, "ArtOfWar.Obtain"); Submit(blocked, "AddDevelopmentTarget");
            Submit(blocked, "BeginDevelopmentCombat"); Submit(blocked, "BeginDevelopmentTurn"); Submit(blocked, "EndDevelopmentTurn");
            Submit(blocked, "BlockEnergy"); Submit(blocked, "BeginDevelopmentTurn");
            Equal(6L, Energy(blocked), "refill bypasses gain restriction, award obeys it");
            foreach (var id in new[] { "base-spire:ice-cream", "base-spire:art-of-war" }) foreach (var melt in new[] { false, true })
            {
                var s = Combat(id.EndsWith("ice-cream", StringComparison.Ordinal) ? "IceCream" : "ArtOfWar"); var r = Relic(s, id);
                if (melt) Set(s, r, "Relic", "IsMelted", Value.Of(true)); else Submit(s, "RemoveOwnedRelic", r);
                IcNextTurn(s); Equal(3L, Energy(s), "inactive relic does not modify next reset");
            }
        });
        check("RM energy site precedes side-start automatic attack and owner-specific modifiers isolate holders", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("early-attack.spire", """
                procedure EarlyAttack { parameters = { owner = Ref(Player) } body = {
                  if = { condition = and(equal(field(owner, Player.TurnNumber), 2), exists(where(collection(Hand), c, field(c, Card.StrikeTag)))) then = {
                    call = { procedure = AutoPlayCard args = { owner = owner card = first(where(collection(Hand), c, field(c, Card.StrikeTag))) forceExhaust = false } }
                  } }
                } }
                subscription EarlyAttack { event = AfterPlayerSideTurnStart reaction = EarlyAttack }
                action OtherHolder { role = player locals = { other = Binding } body = {
                  create = { type = Player scope = frameScope() into = other fields = { Owner = participant(2) } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:strike-dummy" } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:ice-cream" } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:art-of-war" } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "ArtOfWar.Obtain"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn"); IcAdd(s, "StrikeIronclad"); Submit(s, "BeginDevelopmentTurn"); Equal(4L, Energy(s), "previous empty turn earns before automatic attack");
            IcNextTurn(s); Equal(3L, Energy(s), "early attack recorded for the new turn");
            var isolated = Start(copy.Load()); Submit(isolated, "OtherHolder"); Submit(isolated, "AddDevelopmentTarget"); Submit(isolated, "BeginDevelopmentCombat"); Submit(isolated, "BeginDevelopmentTurn");
            IcPlay(isolated, IcAdd(isolated, "StrikeIronclad")); Equal(34L, EnemyHp(isolated), "other holder damage bonus ignored");
            IcNextTurn(isolated); Equal(3L, Energy(isolated), "other holder neither retains nor awards energy");
        });
        return witnesses;
    }
}
