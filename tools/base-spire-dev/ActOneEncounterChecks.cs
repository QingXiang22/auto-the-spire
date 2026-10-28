using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunActOneEncounterChecks(DslPackage package, Action<string, Action> check)
    {
        string Definition(Session s, InstanceId enemy) => s.State.GetField(enemy, EnemyField(s, "DefinitionId")).AsString();
        long HpOf(Session s, InstanceId enemy) => s.State.GetField(enemy, EnemyField(s, "Hp")).AsInt();
        string MoveOf(Session s, InstanceId enemy) => s.State.GetField(enemy, EnemyField(s, "MoveId")).AsString();
        long PowerStacks(Session s, InstanceId enemy, string definition) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Power").Id)
            .Where(p => { var holder = s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("HolderEnemy")); return !holder.IsNone && holder.AsRef() == enemy
                && s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("DefinitionId")).AsString() == definition; })
            .Select(p => s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex("Stacks")).AsInt()).SingleOrDefault();

        check("ACT1 CubexConstructNormal: 65 HP, 13 block and Artifact 1, Charge opens the graph", () =>
        {
            var s = Start(package); Submit(s, "CubexConstructNormal.Spawn");
            Equal(1, Targets(s).Count, "one Cubex"); Equal(65L, EnemyHp(s), "source A0 HP");
            Submit(s, "BeginDevelopmentCombat"); Equal(13L, s.State.GetField(FirstTarget(s), EnemyField(s, "Block")).AsInt(), "room block");
            Equal(1L, PowerStacks(s, FirstTarget(s), "base-spire:artifact-power"), "room Artifact");
            Submit(s, "BeginDevelopmentTurn"); Equal("CHARGE_UP_MOVE", MoveOf(s, FirstTarget(s)), "initial Charge");
        });
        check("ACT1 NibbitsNormal: front starts Slice and back starts Hiss", () =>
        {
            var s = Start(package); Submit(s, "NibbitsNormal.Spawn"); var enemies = Targets(s);
            Equal(2, enemies.Count, "two Nibbits");
            Equal("base-spire:nibbit", Definition(s, enemies[0]), "source identity");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal("SLICE_MOVE", MoveOf(s, enemies[0]), "front initial"); Equal("HISS_MOVE", MoveOf(s, enemies[1]), "back initial");
        });
        check("ACT1 InkletsNormal: three Inklets with only the middle Whirlwind opener", () =>
        {
            var s = Start(package); Submit(s, "InkletsNormal.Spawn"); var enemies = Targets(s);
            Equal(3, enemies.Count, "three Inklets"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal("JAB_MOVE", MoveOf(s, enemies[0]), "left opener"); Equal("WHIRLWIND_MOVE", MoveOf(s, enemies[1]), "middle opener");
            Equal("JAB_MOVE", MoveOf(s, enemies[2]), "right opener");
        });
        check("ACT1 SlimesNormal: TwigM, LeafM and two small slimes with source HP ranges", () =>
        {
            var s = Start(package); Submit(s, "SlimesNormal.Spawn"); var enemies = Targets(s); Equal(4, enemies.Count, "four slimes");
            Equal("base-spire:twig-slime-m", Definition(s, enemies[0]), "first"); Equal("base-spire:leaf-slime-m", Definition(s, enemies[1]), "second");
            True(HpOf(s, enemies[0]) is >= 26 and <= 28, "TwigM HP"); True(HpOf(s, enemies[1]) is >= 32 and <= 35, "LeafM HP");
            True(Definition(s, enemies[2]) is "base-spire:leaf-slime-s" or "base-spire:twig-slime-s", "first small");
        });
        check("ACT1 FlyconidNormal, OvergrowthCrawlers and SnappingJaxfruitNormal keep source order", () =>
        {
            var fly = Start(package); Submit(fly, "FlyconidNormal.Spawn"); var f = Targets(fly);
            Equal(2, f.Count, "flyconid pair"); Equal("base-spire:flyconid", Definition(fly, f[1]), "flyconid second");
            var crawlers = Start(package); Submit(crawlers, "OvergrowthCrawlers.Spawn"); var c = Targets(crawlers);
            Equal(2, c.Count, "crawler pair"); Equal("base-spire:shrinker-beetle", Definition(crawlers, c[0]), "shrinker first");
            var snap = Start(package); Submit(snap, "SnappingJaxfruitNormal.Spawn"); var j = Targets(snap);
            Equal(2, j.Count, "jaxfruit pair"); Equal("base-spire:snapping-jaxfruit", Definition(snap, j[0]), "jaxfruit first");
        });
        check("ACT1 ShrinkerBeetleWeak, VineShamblerNormal, MawlerNormal and NibbitsWeak are reachable", () =>
        {
            var shrinker = Start(package); Submit(shrinker, "ShrinkerBeetleWeak.Spawn"); Equal(1, Targets(shrinker).Count, "shrinker");
            True(HpOf(shrinker, FirstTarget(shrinker)) is >= 38 and <= 40, "shrinker HP");
            var vine = Start(package); Submit(vine, "VineShamblerNormal.Spawn"); Equal(61L, EnemyHp(vine), "vine HP");
            var mawler = Start(package); Submit(mawler, "MawlerNormal.Spawn"); Equal(72L, EnemyHp(mawler), "mawler HP");
            var lone = Start(package); Submit(lone, "NibbitsWeak.Spawn"); Equal(1, Targets(lone).Count, "lone nibbit");
        });
        check("ACT1 normal dispatch reaches every approved act-one Monster encounter", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("normal-encounter-probe.spire", """
                action SpawnNormalEncounterProbe { role = player parameters = { id = String(length = 64) } body = {
                  call = { procedure = PrepareEnemyBirthContext }
                  call = { procedure = SpawnNormalEncounter args = { encounter = id totalFloor = 0 } }
                } }
                """);
            var rules = copy.Load();
            var ids = new[] {
                "base-spire:cubex-construct-normal", "base-spire:flyconid-normal", "base-spire:fogmog-normal",
                "base-spire:fuzzy-wurm-crawler-weak", "base-spire:inklets-normal", "base-spire:mawler-normal",
                "base-spire:nibbits-normal", "base-spire:nibbits-weak", "base-spire:overgrowth-crawlers",
                "base-spire:ruby-raiders-normal", "base-spire:shrinker-beetle-weak", "base-spire:slimes-normal",
                "base-spire:slimes-weak", "base-spire:slithering-strangler-normal", "base-spire:snapping-jaxfruit-normal",
                "base-spire:vine-shambler-normal" };
            foreach (var id in ids)
            {
                var s = Start(rules);
                var action = s.LegalActions(s.Participant(0)).Single(a => a.Definition.QualifiedId == "base-spire:SpawnNormalEncounterProbe");
                s.Submit(action.Definition, s.Participant(0), [Value.Of(id)]); Healthy(s);
                True(Targets(s).Count > 0, id + " spawns at least one source monster");
                Submit(s, "BeginDevelopmentCombat");
            }
        });
        check("ACT1 SlitheringStranglerNormal and FogmogNormal spawn their source slots", () =>
        {
            var strangler = Start(package); Submit(strangler, "SlitheringStranglerNormal.Spawn");
            var members = Targets(strangler); True(members.Count >= 2, "strangler plus secondary");
            Equal("base-spire:slithering-strangler", Definition(strangler, members[^1]), "strangler last");
            var fog = Start(package); Submit(fog, "FogmogNormal.Spawn"); Equal(1, Targets(fog).Count, "fogmog alone before illusion");
            Equal(74L, EnemyHp(fog), "fogmog HP");
        });
    }
}
