using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunActOneBossesOnly(DslPackage package)
    {
        var count = 0;
        var witnesses = RunActOneBossChecks(package, (name, body) => { body(); count++; Console.WriteLine("ok   " + name); });
        Console.WriteLine($"All {count} act-one elite/Boss component checks passed for {witnesses.Count} declared content entries.");
        return 0;
    }
    private static Dictionary<string, string[]> RunActOneBossChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new Dictionary<string, List<string>>();
        void Case(string name, Action body, params string[] ids)
        {
            check(name, body);
            foreach (var id in ids)
            {
                if (!witnesses.TryGetValue(id, out var names)) witnesses[id] = names = [];
                names.Add(name);
            }
        }
        Session Combat(string encounter)
        {
            var s = Start(package); Submit(s, encounter + ".Spawn");
            Set(s, Player(s), "Player", "MaxHp", Value.Of(999L)); Set(s, Player(s), "Player", "Hp", Value.Of(999L));
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); return s;
        }
        long Power(Session s, InstanceId enemy, string definition) => Members(s, "Powers")
            .Where(p => { var holder = s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("HolderEnemy")); return !holder.IsNone && holder.AsRef() == enemy && TextField(s, p, "Power", "DefinitionId") == "base-spire:" + definition; })
            .Sum(p => IntField(s, p, "Power", "Stacks"));
        string Move(Session s, InstanceId enemy) => TextField(s, enemy, "Enemy", "MoveId");
        void Hit(Session s, InstanceId enemy)
        {
            Set(s, Player(s), "Player", "Energy", Value.Of(30L)); var card = IcAdd(s, "StrikeIronclad");
            Submit(s, "PlayTargetedCard", card); s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Instances: [enemy])); Healthy(s);
        }
        void Next(Session s) { Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); }

        Case("ACT1 ELITE Effigy sleeps, wakes, slashes and Slow counts cards then resets", () =>
        {
            var s = Combat("BygoneEffigyElite"); var enemy = FirstTarget(s);
            Equal(127L, EnemyHp(s), "fixed source HP"); Equal("SLEEP_MOVE", Move(s, enemy), "initial sleep");
            Set(s, Player(s), "Player", "Energy", Value.Of(30L)); IcPlay(s, IcAdd(s, "DefendIronclad"));
            Hit(s, enemy); Hit(s, enemy); Equal(114L, EnemyHp(s), "6 at 10 percent, then 7 at 20 percent");
            Next(s); Equal("WAKE_MOVE", Move(s, enemy), "sleep then wake");
            Hit(s, enemy); Equal(108L, EnemyHp(s), "Slow counter resets at enemy side start");
            Next(s); Equal(10L, Power(s, enemy, "strength-power"), "wake strength ten");
            Equal("SLASHES_MOVE", Move(s, enemy), "persistent attack"); var hp = Hp(s); Next(s);
            Equal(hp - 23L, Hp(s), "13 attack plus 10 Strength");
        }, "base-spire:bygone-effigy", "base-spire:bygone-effigy-elite");
        Case("ACT1 ELITE Byrdonis alternates attacks and gains Territorial Strength after every side end", () =>
        {
            var s = Combat("ByrdonisElite"); var enemy = FirstTarget(s); True(EnemyHp(s) is >= 81 and <= 84, "A0 HP range");
            var hp = Hp(s); Next(s); Equal(hp - 17L, Hp(s), "first swoop"); Equal(1L, Power(s, enemy, "strength-power"), "first Territorial");
            hp = Hp(s); Next(s); Equal(hp - 12L, Hp(s), "three pecks each gain Strength"); Equal(2L, Power(s, enemy, "strength-power"), "second Territorial");
            Equal("SWOOP_MOVE", Move(s, enemy), "cycle returns");
        }, "base-spire:byrdonis", "base-spire:byrdonis-elite");
        Case("ACT1 ELITE Phrog makes Infection, lashes, then death hatches four stunned Wrigglers before victory", () =>
        {
            var s = Combat("PhrogParasiteElite"); var parent = FirstTarget(s); True(EnemyHp(s) is >= 61 and <= 64, "A0 HP range");
            Next(s); Equal(3, Members(s, "DiscardPile").Count(c => CardDefinition(s, c) == "base-spire:infection"), "three source Infections");
            var hp = Hp(s); Next(s); Equal(hp - 16L, Hp(s), "four hits of four");
            Set(s, parent, "Enemy", "Hp", Value.Of(1L)); Hit(s, parent);
            var children = Targets(s).Where(e => TextField(s, e, "Enemy", "DefinitionId") == "base-spire:wriggler").ToArray();
            Equal(4, children.Length, "four real births"); Equal("none", Outcome(s), "spawn prevents premature victory");
            Equal(4, children.Select(e => IntField(s, e, "Enemy", "MaxHp")).Distinct().Count(), "source HP exclusion");
            hp = Hp(s); Next(s); Equal(hp, Hp(s), "first spawned move is stunned");
            True(children.All(e => Move(s, e) is "NASTY_BITE_MOVE" or "WRIGGLE_MOVE"), "slot-specific next moves");
            foreach (var child in children) { Set(s, child, "Enemy", "Hp", Value.Of(1L)); Hit(s, child); }
            Equal("victory", Outcome(s), "only final hatchling death finishes the combat");
        }, "base-spire:phrog-parasite", "base-spire:phrog-parasite-elite");
        Case("ACT1 BOSS Kin starts distinct follower moves and ends when its primary priest dies", () =>
        {
            var s = Combat("TheKinBoss"); var enemies = Targets(s);
            Equal(3, enemies.Count, "source formation"); Equal("POWER_DANCE_MOVE", Move(s, enemies[0]), "first follower dances");
            Equal("QUICK_SLASH_MOVE", Move(s, enemies[1]), "second follower slashes"); Equal(190L, IntField(s, enemies[2], "Enemy", "Hp"), "priest HP");
            Equal(1L, Power(s, enemies[0], "minion-power"), "real source Minion power");
            var hp = Hp(s); Next(s); Equal(hp - 13L, Hp(s), "quick slash five plus priest eight");
            Equal(2L, Power(s, enemies[0], "strength-power"), "dance grants two Strength");
            Set(s, enemies[2], "Enemy", "Hp", Value.Of(1L)); Hit(s, enemies[2]);
            Equal("victory", Outcome(s), "two surviving Minions do not block victory");
        }, "base-spire:kin-priest", "base-spire:kin-follower", "base-spire:the-kin-boss");
        Case("ACT1 BOSS Vantom has eight Slippery, three attack patterns, Wounds and Prepare Strength", () =>
        {
            var s = Combat("VantomBoss"); var enemy = FirstTarget(s); Equal(173L, EnemyHp(s), "source HP"); Equal(8L, Power(s, enemy, "slippery-power"), "eight caps");
            Hit(s, enemy); Equal(172L, EnemyHp(s), "first capped hit");
            var hp = Hp(s); Next(s); Equal(hp - 7L, Hp(s), "Ink Blot seven");
            hp = Hp(s); Next(s); Equal(hp - 12L, Hp(s), "Inky Lance six twice");
            hp = Hp(s); Next(s); Equal(hp - 26L, Hp(s), "Dismember twenty six");
            Equal(3, Members(s, "DiscardPile").Count(c => CardDefinition(s, c) == "base-spire:wound") + Hand(s).Count(c => CardDefinition(s, c) == "base-spire:wound"), "three real unplayable Wounds");
            Next(s); Equal(2L, Power(s, enemy, "strength-power"), "Prepare Strength"); Equal("INK_BLOT_MOVE", Move(s, enemy), "four-move cycle");
        }, "base-spire:vantom", "base-spire:vantom-boss", "base-spire:wound");
        Case("ACT1 BOSS Beast Plow breaks on unblocked HP threshold, clears Strength, stuns then resumes Cry", () =>
        {
            var s = Combat("CeremonialBeastBoss"); var enemy = FirstTarget(s); Equal(252L, EnemyHp(s), "source HP");
            Next(s); Equal(150L, Power(s, enemy, "plow-power"), "Stamp threshold");
            Next(s); Equal(2L, Power(s, enemy, "strength-power"), "Plow grants Strength");
            Set(s, enemy, "Enemy", "Hp", Value.Of(151L)); Hit(s, enemy);
            Equal(0L, Power(s, enemy, "plow-power"), "break removes threshold power"); Equal(0L, Power(s, enemy, "strength-power"), "break removes Strength");
            Equal("STUNNED", Move(s, enemy), "published immediate stun"); var hp = Hp(s); Next(s);
            Equal(hp, Hp(s), "stunned turn performs no attack"); Equal("BEAST_CRY_MOVE", Move(s, enemy), "resumes second phase at Cry");
            Next(s); var first = IcAdd(s, "DefendIronclad"); var second = IcAdd(s, "StrikeIronclad"); IcPlay(s, first);
            True(!s.LegalActions(s.Participant(0)).Any(a => a.Definition.QualifiedId is "base-spire:PlayTargetedCard" or "base-spire:PlayAttackFromHand" or "base-spire:PlayCardFromHand" && a.Bindings.Any(b => b.Any(v => v.Kind == ValueKind.Ref && v.AsRef() == second))), "Ringing blocks any second card");
            Next(s); Hit(s, enemy); Healthy(s);
        }, "base-spire:ceremonial-beast", "base-spire:ceremonial-beast-boss");
        return witnesses.ToDictionary(row => row.Key, row => row.Value.ToArray());
    }
}
