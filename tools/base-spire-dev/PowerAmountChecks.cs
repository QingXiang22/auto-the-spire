using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunPowerAmountChecks(DslPackage package, Action<string, Action> check)
    {
        check("POWER-AMOUNT ending rejects enemy Vigor consumption after real lethal Tracker damage", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("ending-power.spire", """
                action Vigor { role = player body = { call = { procedure = ApplyEnemyPower args = { target = first(type(Enemy)) definition = "base-spire:vigor-power" stacks = 3 } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "TrackerRubyRaider.Spawn"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "Vigor");
            Set(s, Player(s), "Player", "Hp", Value.Of(3L)); Submit(s, "EndDevelopmentTurn");
            Equal("defeat", Outcome(s), "ordinary HOUNDS_MOVE kills the player");
            Equal(3L, PowerStacksFor(s, "base-spire:vigor-power", FirstTarget(s)), "source IsEnding rejects the AfterAttack amount command");
            Equal(0L, IcPower(s, "base-spire:frail-power"), "player death cleanup remains intact");
        });
        using var probeCopy = new Copy(package.Root);
        using (var stream = typeof(PackageChecks).Assembly.GetManifestResourceStream("BaseSpire.Dev.fixtures.power-amount-probe.spire")!)
        using (var reader = new StreamReader(stream)) probeCopy.Include("power-amount-probe.spire", reader.ReadToEnd());
        var configured = probeCopy.Load();
        Session Setup(bool enemy)
        {
            var s = Start(configured); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "AmountInitialize");
            if (enemy) { ProbeSet(s, "Player", Value.None); ProbeSet(s, "Enemy", Value.OfRef(FirstTarget(s))); }
            return s;
        }
        foreach (var enemy in new[] { false, true })
        foreach (var row in new (string Name, string Definition, long Initial, long Delta, bool Modify, long Expected, long Artifact, string Log)[] {
            ("new debuff", "weak-power", 0, 2, false, 0, 1, "BRCM"),
            ("stack debuff", "weak-power", 2, 3, false, 2, 1, "BRMD"),
            ("modify debuff", "weak-power", 2, 3, true, 2, 1, "BRM"),
            ("negative Strength", "strength-power", 0, -2, false, 0, 1, "BRCM"),
            ("positive Strength", "strength-power", 0, 2, false, 2, 2, "BRCPMAHD"),
            ("new zero", "weak-power", 0, 0, false, 0, 2, ""),
            ("existing zero", "weak-power", 2, 0, true, 2, 1, "BRM"),
            ("debuff removal is buff", "weak-power", 2, -2, true, 0, 2, "BRMHX"),
            ("negative buff offset", "strength-power", 2, -4, true, 2, 1, "BRM"),
            ("stack negative buff", "strength-power", 2, -4, false, 2, 1, "BRMD"),
            ("negative debuff is buff", "weak-power", 0, -2, false, -2, 2, "BRCPMAHD"),
            ("single debuff type", "no-draw-power", 0, 1, false, 0, 1, "BRCM"),
            ("Mangle before Strength", "mangle-power", 0, 3, false, 0, 1, "BRCM")
        })
            check("POWER-AMOUNT " + (enemy ? "enemy " : "player ") + row.Name, () =>
            {
                var s = Setup(enemy); ProbeSet(s, "Definition", Value.Of("base-spire:" + row.Definition));
                if (row.Initial != 0) { ProbeSet(s, "Amount", Value.Of(row.Initial)); Submit(s, "AmountApply"); }
                Submit(s, "AmountArtifact"); ProbeSet(s, "Log", Value.Of("")); ProbeSet(s, "Amount", Value.Of(row.Delta));
                var draws = s.Random.TotalDraws; Submit(s, row.Modify ? "AmountModify" : "AmountApply");
                Equal(row.Expected, HolderAmount(s, row.Definition, enemy), "source amount after shared command");
                Equal(row.Artifact, HolderAmount(s, "artifact-power", enemy), "only actual receiver consumes once");
                Equal(row.Log, TextField(s, Probe(s), "AmountProbe", "Log"), "source Before/Received/BeforeApplied/Create/modifier-after/AfterApplied/Changed ordering");
                if (row.Definition == "mangle-power") Equal(0L, HolderAmount(s, "strength-power", enemy), "blocked temporary debuff cannot change Strength first");
                Equal(draws, s.Random.TotalDraws, "Power amount operations spend no RNG"); AssertNoPowerChangeState(s);
            });
        foreach (var definition in new[] { "flex-potion-power", "setup-strike-power", "mangle-power" })
        foreach (var enemy in new[] { false, true })
            check("POWER-AMOUNT " + (enemy ? "enemy " : "player ") + definition + " synchronizes initial, stack, direct change and expiry", () =>
            {
                var s = Setup(enemy); ProbeSet(s, "Definition", Value.Of("base-spire:" + definition));
                var sign = definition == "mangle-power" ? -1L : 1L;
                ProbeSet(s, "Amount", Value.Of(2L)); Submit(s, "AmountApply"); Equal(sign * 2, HolderAmount(s, "strength-power", enemy), "BeforeApplied gives initial Strength once");
                ProbeSet(s, "Amount", Value.Of(3L)); Submit(s, "AmountApply"); Equal(sign * 5, HolderAmount(s, "strength-power", enemy), "stack delta gives Strength once");
                ProbeSet(s, "Amount", Value.Of(-1L)); Submit(s, "AmountModify"); Equal(sign * 4, HolderAmount(s, "strength-power", enemy), "direct offset shares AfterChanged");
                Submit(s, "AmountExpire"); Equal(0L, HolderAmount(s, definition, enemy), "temporary Power removed"); Equal(0L, HolderAmount(s, "strength-power", enemy), "saved Strength withdrawn"); AssertNoPowerChangeState(s);
            });
        foreach (var mode in new[] { "independent", "per-applier" })
            check("POWER-AMOUNT " + mode + " keeps exact instances and original appliers", () =>
            {
                var s = Setup(false); ProbeSet(s, "Definition", Value.Of("probe:" + mode)); ProbeSet(s, "Amount", Value.Of(2L));
                Submit(s, "AmountApply"); Submit(s, "AmountApply");
                if (mode == "per-applier") { ProbeSet(s, "MonsterApplier", Value.Of(true)); Submit(s, "AmountApply"); }
                ProbeSet(s, "Amount", Value.Of(1L)); Submit(s, "AmountModify");
                var rows = Members(s, "Powers"); Equal(2, rows.Count, "instance policy retained through shared pipeline");
                Equal(mode == "independent" ? "2,3" : "3,4", string.Join(',', rows.Select(p => IntField(s, p, "Power", "Stacks")).Order()), "direct modification targets only selected instance"); AssertNoPowerChangeState(s);
            });
        check("POWER-AMOUNT exact card/play and monster sources survive direct modification without replacing initial applier", () =>
        {
            var s = Setup(false); var card = IcAdd(s, "StrikeIronclad"); IcPlay(s, card);
            var play = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:CardPlayRecord").Id).Single();
            ProbeSet(s, "Card", Value.OfRef(card)); ProbeSet(s, "Play", Value.OfRef(play)); ProbeSet(s, "MonsterApplier", Value.Of(true));
            Submit(s, "AmountApply"); var power = Members(s, "Powers").Single(); var type = s.RuleSet.TypeByQualifiedId("base-spire:Power");
            Equal(FirstTarget(s), s.State.GetField(power, type.FieldIndex("ApplierEnemy")).AsRef(), "initial enemy applier");
            Equal(card, s.State.GetField(power, type.FieldIndex("SourceCard")).AsRef(), "explicit card source"); Equal(play, s.State.GetField(power, type.FieldIndex("SourcePlay")).AsRef(), "explicit play source");
            ProbeSet(s, "MonsterApplier", Value.Of(false)); ProbeSet(s, "Amount", Value.Of(1L)); Submit(s, "AmountModify");
            Equal(3L, IntField(s, power, "Power", "Stacks"), "direct offset accepted with new source");
            Equal(FirstTarget(s), s.State.GetField(power, type.FieldIndex("ApplierEnemy")).AsRef(), "modification does not overwrite first-instance source"); AssertNoPowerChangeState(s);
        });
        check("POWER-AMOUNT Given additive then multiplicative runs once for new, stacked and direct offsets", () =>
        {
            using var copy = new Copy(configured.Root); copy.Include("given-power-amount.spire", """
                procedure Add { pure = true parameters = { value = Int player = Binding enemy = Binding definition = String(length = 64) applierPlayer = Binding applierEnemy = Binding card = Binding play = Binding } result = Int body = { return = { value = add(value, 1) } } }
                procedure Multiply { pure = true parameters = { value = Int player = Binding enemy = Binding definition = String(length = 64) applierPlayer = Binding applierEnemy = Binding card = Binding play = Binding } result = Int body = { return = { value = multiply(value, 2) } } }
                subscription Add { event = ModifyPowerGivenAdditive reaction = Add }
                subscription Multiply { event = ModifyPowerGivenMultiplicative reaction = Multiply }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "AmountInitialize");
            ProbeSet(s, "Definition", Value.Of("base-spire:strength-power")); Submit(s, "AmountApply"); Equal(6L, HolderAmount(s, "strength-power", false), "new (2+1)*2");
            ProbeSet(s, "Amount", Value.Of(3L)); Submit(s, "AmountApply"); Equal(14L, HolderAmount(s, "strength-power", false), "one stacking pass adds (3+1)*2");
            Equal(8L, IntField(s, Probe(s), "AmountProbe", "LastDelta"), "changed event publishes actual delta");
            ProbeSet(s, "Amount", Value.Of(1L)); Submit(s, "AmountModify"); Equal(18L, HolderAmount(s, "strength-power", false), "direct offset also uses (1+1)*2"); AssertNoPowerChangeState(s);
        });
        foreach (var remove in new[] { false, true })
            check("POWER-AMOUNT recorded receiver " + (remove ? "removed before callback is skipped" : "added after calculation does not consume"), () =>
            {
                using var copy = new Copy(configured.Root); copy.Include("modify-receiver-lifetime.spire", """
                    procedure ChangeReceiver { parameters = { player = Binding enemy = Binding definition = String(length = 64) amount = Int applierPlayer = Binding applierEnemy = Binding card = Binding play = Binding } locals = { applied = Binding } body = {
                      if = { condition = equal(definition, "base-spire:weak-power") then = { MUTATION } }
                    } }
                    subscription ChangeReceiver { event = BeforePowerCreated reaction = ChangeReceiver }
                    """.Replace("MUTATION", remove
                        ? "call = { procedure = RemovePower args = { power = first(where(collection(Powers), p, equal(field(p, Power.DefinitionId), \"base-spire:artifact-power\"))) } }"
                        : "call = { procedure = ApplyPower args = { player = player enemy = enemy definition = \"base-spire:artifact-power\" stacks = 2 untilTurnEnd = false } into = applied }", StringComparison.Ordinal));
                var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "AmountInitialize");
                if (remove) Submit(s, "AmountArtifact"); Submit(s, "AmountApply");
                Equal(remove ? 0L : 2L, HolderAmount(s, "weak-power", false), "calculated amount is not retroactively recomputed");
                Equal(remove ? 0L : 2L, HolderAmount(s, "artifact-power", false), "only recorded and still active receivers get callbacks"); AssertNoPowerChangeState(s);
            });
        check("POWER-AMOUNT paused calculation hides context, resumes once and consumes one Artifact", () =>
        {
            using var copy = new Copy(configured.Root); copy.Include("pause-power-amount.spire", """
                procedure PauseAmount { parameters = { change = Ref(PowerAmountChange) } locals = { answer = Bool } body = {
                  if = { condition = equal(field(field(change, PowerAmountChange.Definition), PowerBlueprint.DefinitionId), "base-spire:weak-power") then = {
                    decide = { participant = participant(1) kind = Bool prompt = "continue Power calculation" into = answer }
                  } }
                } }
                subscription PauseAmount { event = PowerAmountReceiving reaction = PauseAmount priority = 10001 }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "AmountInitialize"); Submit(s, "AmountArtifact"); Submit(s, "AmountApply");
            Equal(SessionStatus.AwaitingDecision, s.Status, "normal decision pauses the shared pipeline");
            var context = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:PowerAmountChange").Id).Single();
            Equal(1, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:PowerAmountModifier").Id).Count, "actual Artifact recorded before pause");
            var hash = s.State.StateHash(); var version = s.StateVersion; var draws = s.Random.TotalDraws;
            for (var i = 0; i < 3; i++)
            {
                var view = s.Observe(s.Participant(0), "base-spire:PlayerView"); True(view.Instances.All(i => i.Id != context), "calculation context is not public");
                Equal(0, s.LegalActions(s.Participant(0)).Count, "paused session offers no actions");
            }
            Equal(hash, s.State.StateHash(), "queries cannot replay modifier calculation"); Equal(version, s.StateVersion, "query version"); Equal(draws, s.Random.TotalDraws, "query RNG");
            s.Answer(new DecisionAnswer(s.PendingDecision!.RequestId, Flag: true)); Healthy(s);
            Equal(1L, HolderAmount(s, "artifact-power", false), "one deferred callback after resume"); Equal("BRCM", TextField(s, Probe(s), "AmountProbe", "Log"), "no repeated before or receiving stage"); AssertNoPowerChangeState(s);
        });
    }

    private static InstanceId Probe(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:AmountProbe").Id).Single();
    private static void ProbeSet(Session s, string field, Value value) => Set(s, Probe(s), "AmountProbe", field, value);
    private static long HolderAmount(Session s, string definition, bool enemy) => Members(s, "Powers")
        .Where(p => TextField(s, p, "Power", "DefinitionId") == "base-spire:" + definition
            && s.State.GetField(p, s.RuleSet.TypeByQualifiedId("base-spire:Power").FieldIndex(enemy ? "HolderEnemy" : "HolderPlayer")) == Value.OfRef(enemy ? FirstTarget(s) : Player(s)))
        .Sum(p => IntField(s, p, "Power", "Stacks"));
    private static void AssertNoPowerChangeState(Session s)
    {
        foreach (var type in new[] { "PowerAmountChange", "PowerAmountModifier" }) Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:" + type).Id).Count, "completed Power operation cleans " + type);
    }
}
