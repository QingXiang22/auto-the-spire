using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunAttackModifierRelicChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Own(string id, string name, Action body) { check(name, body); witnesses[id] = witnesses.GetValueOrDefault(id, []).Append(name).ToArray(); }
        using var copy = new Copy(package.Root);
        copy.Include("attack-modifier-probes.spire", """
            action TargetVulnerable { role = player body = { call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:vulnerable-power" kind = "damage-taken-multiplier" stacks = 3 } } } }
            action AttackerWeak { role = player body = { call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:weak-power" kind = "damage-dealt-multiplier" stacks = 3 } } } }
            action IncomingAttack { role = player body = { call = { procedure = DamagePlayerPowered args = { attacker = first(type(Enemy)) owner = first(type(Player)) amount = 13 } } } }
            action OutgoingModifiers { role = player body = {
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:strength-power" kind = "damage-additive" stacks = 1 } }
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:weak-power" kind = "damage-dealt-multiplier" stacks = 1 } }
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:cruelty-power" kind = "vulnerable-bonus" stacks = 20 } }
            } }
            action IncomingModifiers { role = player body = {
              call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:strength-power" kind = "damage-additive" stacks = 1 } }
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:vulnerable-power" kind = "damage-taken-multiplier" stacks = 3 } }
            } }
            action PlayerStatuses { role = player body = {
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:weak-power" kind = "damage-dealt-multiplier" stacks = 3 } }
              call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:vulnerable-power" kind = "damage-taken-multiplier" stacks = 3 } }
            } }
            action UnpoweredBoth { role = player body = {
              call = { procedure = DamageEnemy args = { target = first(type(Enemy)) amount = 6 } }
              call = { procedure = DamagePlayer args = { owner = first(type(Player)) amount = 13 } }
            } }
            action OtherTargetStatuses { role = player body = {
              call = { procedure = ApplyEnemyPower args = { target = first(skip(type(Enemy), 1)) definition = "base-spire:vulnerable-power" kind = "damage-taken-multiplier" stacks = 3 } }
              call = { procedure = ApplyEnemyPower args = { target = first(skip(type(Enemy), 1)) definition = "base-spire:weak-power" kind = "damage-dealt-multiplier" stacks = 3 } }
            } }
            action OtherHolder { role = player locals = { other = Binding card = Binding } body = {
              create = { type = Player scope = frameScope() into = other fields = { Owner = participant(2) } }
              call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:miniature-cannon" } }
              call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:paper-phrog" } }
              call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:paper-krane" } }
              call = { procedure = CreateCombatCard args = { owner = other definition = "base-spire:strike-ironclad" } into = card }
              call = { procedure = UpgradeCard args = { card = card } }
              call = { procedure = AddGeneratedToHand args = { card = card } }
            } }
            action OtherCardSource { role = player body = { call = { procedure = DamageEnemyPowered args = { owner = first(type(Player)) card = first(where(collection(Hand), c, equal(field(c, Card.Owner), participant(2)))) target = first(type(Enemy)) amount = 9 } } } }
            action OtherDealer { role = player body = { call = { procedure = DamageEnemyPowered args = { owner = first(skip(type(Player), 1)) card = first(where(collection(Hand), c, equal(field(c, Card.Owner), participant(1)))) target = first(type(Enemy)) amount = 9 } } } }
            action OtherDefender { role = player body = { call = { procedure = DamagePlayerPowered args = { attacker = first(type(Enemy)) owner = first(skip(type(Player), 1)) amount = 13 } } } }
            action ExtraKrane { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:paper-krane" } } } }
            action PreviewAttackModifiers { role = player requires = and(equal(foldEvent(ModifyAttackAdditive, 9, first(type(Player)), first(collection(Hand)), first(type(Enemy))), 12), and(equal(foldEvent(ModifyAttackMultiplier, 90000, first(type(Player)), first(collection(Hand)), first(type(Enemy))), 157500), and(equal(foldEvent(ModifyEnemyAttackMultiplier, 130000, first(type(Player)), first(type(Enemy))), 78000), equal(foldEvent(ModifyAttackAdditive, 9, first(type(Player)), none, none), 9)))) body = { } }
            """);
        var variant = copy.Load();
        Session Combat(params string[] relics)
        {
            var s = Start(variant); foreach (var relic in relics) Submit(s, relic + ".Obtain");
            Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L)); Set(s, FirstTarget(s), "Enemy", "MaxHp", Value.Of(1000L)); Set(s, Player(s), "Player", "Energy", Value.Of(30L)); return s;
        }
        Own("base-spire:miniature-cannon", "AM MiniatureCannon: upgraded, non-Strike and per-hit additive damage; ordinary cards stay unchanged", () =>
        {
            foreach (var row in new[] { (Name: "StrikeIronclad", Ordinary: 6L, Upgraded: 12L), (Name: "Anger", Ordinary: 6L, Upgraded: 11L), (Name: "TwinStrike", Ordinary: 10L, Upgraded: 20L) })
            {
                var s = Combat("MiniatureCannon"); IcPlay(s, IcAdd(s, row.Name)); Equal(1000L - row.Ordinary, EnemyHp(s), "ordinary " + row.Name);
                IcPlay(s, IcAdd(s, row.Name, true)); Equal(1000L - row.Ordinary - row.Upgraded, EnemyHp(s), "upgraded " + row.Name);
            }
        });
        Own("base-spire:paper-phrog", "AM PaperPhrog: only active target Vulnerable changes 1.5 to 1.75; no blanket damage boost", () =>
        {
            var s = Combat("PaperPhrog"); IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(994L, EnemyHp(s), "no target Vulnerable means no bonus");
            Submit(s, "TargetVulnerable"); IcPlay(s, IcAdd(s, "StrikeIronclad")); Equal(984L, EnemyHp(s), "floor(6*1.75)=10");
            IcPlay(s, IcAdd(s, "StrikeIronclad", true)); Equal(969L, EnemyHp(s), "floor(9*1.75)=15");
            Equal(3L, IcPower(s, "damage-taken-multiplier"), "damage query never consumes target duration");
        });
        Own("base-spire:paper-krane", "AM PaperKrane: only the attacking enemy's Weak changes .75 to .60; strength, Vulnerable, final floor then block", () =>
        {
            var s = Combat("PaperKrane"); Submit(s, "IncomingAttack"); Equal(67L, Hp(s), "no Weak means full thirteen");
            Submit(s, "AttackerWeak"); Submit(s, "IncomingAttack"); Equal(60L, Hp(s), "floor(13*.60)=7");
            Submit(s, "IncomingModifiers"); Set(s, Player(s), "Player", "Block", Value.Of(2L)); Submit(s, "IncomingAttack");
            Equal(50L, Hp(s), "floor((13+1)*.60*1.5)-2=10"); Equal(0L, Block(s), "block absorbs after floor");
        });
        check("AM phases: upgraded bonus and Strength precede Weak and additive Phrog/Cruelty percentage points; floor once before block", () =>
        {
            var s = Combat("MiniatureCannon", "PaperPhrog"); Submit(s, "TargetVulnerable"); Submit(s, "OutgoingModifiers"); Set(s, FirstTarget(s), "Enemy", "Block", Value.Of(2L));
            IcPlay(s, IcAdd(s, "StrikeIronclad", true)); Equal(983L, EnemyHp(s), "floor((9+3+1)*.75*(1.5+.25+.20))-2=17");
            Submit(s, "AttackerWeak"); Submit(s, "UnpoweredBoth"); Equal(977L, EnemyHp(s), "unpowered outgoing ignores Cannon and Vulnerable"); Equal(67L, Hp(s), "unpowered incoming ignores Weak");
        });
        check("AM eligibility: player-side statuses and another enemy's statuses never activate the wrong Paper modifier", () =>
        {
            var s = Combat("PaperPhrog", "PaperKrane"); Submit(s, "PlayerStatuses"); IcPlay(s, IcAdd(s, "StrikeIronclad", true)); Equal(994L, EnemyHp(s), "player's own Weak still .75, no target Vulnerable bonus");
            Submit(s, "IncomingAttack"); Equal(61L, Hp(s), "player's own Vulnerable remains 1.5, no enemy Weak");
            var other = Combat("PaperPhrog", "PaperKrane"); Submit(other, "AddDevelopmentTarget"); Submit(other, "OtherTargetStatuses");
            IcPlay(other, IcAdd(other, "StrikeIronclad", true)); Equal(991L, EnemyHp(other), "different target Vulnerable ignored"); Submit(other, "IncomingAttack"); Equal(67L, Hp(other), "different attacker Weak ignored");
        });
        check("AM lifetime: removed or melted instances stop modifying damage without removing the statuses", () =>
        {
            foreach (var name in new[] { "MiniatureCannon", "PaperPhrog", "PaperKrane" }) foreach (var melt in new[] { false, true })
            {
                var s = Combat(name); var relic = Members(s, "Relics").Single(); if (melt) Set(s, relic, "Relic", "IsMelted", Value.Of(true)); else Submit(s, "RemoveOwnedRelic", relic);
                Submit(s, "TargetVulnerable"); Submit(s, "AttackerWeak"); IcPlay(s, IcAdd(s, "StrikeIronclad", true)); Equal(987L, EnemyHp(s), "ordinary 1.5 Vulnerable without active relic");
                Submit(s, "IncomingAttack"); Equal(71L, Hp(s), "ordinary .75 Weak without active relic");
            }
        });
        check("AM explicit owners: Cannon accepts dealer OR card owner; Papers follow attacker/defender and other ownership cannot block acquisition", () =>
        {
            var s = Combat(); var ownCard = IcAdd(s, "StrikeIronclad", true); Submit(s, "OtherHolder"); Submit(s, "TargetVulnerable"); Submit(s, "AttackerWeak");
            Submit(s, "IncomingAttack"); Equal(71L, Hp(s), "other holder Krane does not protect first player");
            IcPlay(s, ownCard); Equal(987L, EnemyHp(s), "other holder Cannon and Phrog do not enhance unrelated first-player attack");
            Submit(s, "OtherCardSource"); Equal(969L, EnemyHp(s), "other holder Cannon qualifies by card owner, Phrog does not: floor(12*1.5)=18");
            ownCard = IcAdd(s, "StrikeIronclad", true); Submit(s, "OtherDealer"); Equal(948L, EnemyHp(s), "other holder Cannon qualifies by dealer, Phrog by attacker: floor(12*1.75)=21");
            Submit(s, "OtherDefender"); var other = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Last(); Equal(73L, IntField(s, other, "Player", "Hp"), "own holder Krane protects second player");
            foreach (var name in new[] { "MiniatureCannon", "PaperPhrog", "PaperKrane" }) { True(Legal(s, name + ".Obtain"), "other holder does not block development obtain"); Submit(s, name + ".Obtain"); True(!Legal(s, name + ".Obtain"), "same holder development duplicate excluded"); }
        });
        check("AM automatic and repeated attacks: each upgraded hit keeps its own source and status multiplier", () =>
        {
            var s = Combat("MiniatureCannon", "PaperPhrog"); Submit(s, "TargetVulnerable"); IcPlay(s, IcAdd(s, "OneTwoPunch")); IcPlay(s, IcAdd(s, "TwinStrike", true));
            Equal(932L, EnemyHp(s), "two plays of two hits, floor((7+3)*1.75)=17 each");
            var top = IcAdd(s, "StrikeIronclad", true); IcMove(s, top, "Hand", "DrawPile"); IcPlay(s, IcAdd(s, "Havoc")); Equal(911L, EnemyHp(s), "nested automatic source gives floor((9+3)*1.75)=21");
        });
        check("AM pure queries: repeated preview/observe changes no state or random; null card skips Cannon and Thrash absorbs the upgraded source bonus", () =>
        {
            var s = Combat("MiniatureCannon", "PaperPhrog", "PaperKrane"); Submit(s, "TargetVulnerable"); Submit(s, "AttackerWeak"); IcAdd(s, "StrikeIronclad", true);
            var hash = s.State.StateHash(); var version = s.StateVersion; var random = s.Random.TotalDraws;
            for (var i = 0; i < 3; i++) { True(Legal(s, "PreviewAttackModifiers"), "preview folds return source values"); s.Observe(s.Participant(0), "base-spire:PlayerView"); }
            Equal(hash, s.State.StateHash(), "preview state unchanged"); Equal(version, s.StateVersion, "preview version unchanged"); Equal(random, s.Random.TotalDraws, "preview random unchanged");
            var thrash = IcAdd(s, "Thrash"); IcPlay(s, thrash); Equal(12L, IntField(s, thrash, "Card", "DamageGrowth"), "targetless absorption includes upgraded base nine plus Cannon three, no target Vulnerable");
        });
        check("AM development duplicate instances: Weak percentage cannot turn negative and heal the defender", () =>
        {
            var s = Combat("PaperKrane"); for (var i = 0; i < 5; i++) Submit(s, "ExtraKrane");
            Submit(s, "AttackerWeak"); Submit(s, "IncomingAttack"); Equal(80L, Hp(s), "zero multiplier cannot become negative damage"); Equal(0L, Block(s), "zero damage cannot create block");
        });
        return witnesses;
    }
}
