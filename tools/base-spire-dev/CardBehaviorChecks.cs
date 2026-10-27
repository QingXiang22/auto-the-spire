using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunCardBehaviorOnly(DslPackage package, string? filter = null)
    {
        var count = 0;
        void Check(string name, Action body)
        {
            if (filter is not null && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase)) return;
            body(); count++; Console.WriteLine("ok   " + name);
        }
        RunCombatEndChecks(package, Check);
        Check("END normal settlement order: source end before cleanup, victory and rewards", () => RunNormalSettlementOrderChecks(package));
        RunCardBehaviorChecks(package, Check);
        RunAttackBehaviorChecks(package, Check);
        RunCardLifecycleChecks(package, Check);
        RunVisitBehaviorChecks(package, Check);
        RunNeowEffectChecks(package, Check);
        if (count == 0) throw new ArgumentException("No card-behavior checks matched --filter.");
        Console.WriteLine($"All {count} focused card-behavior checks passed; no independent coverage claim.");
        return 0;
    }

    private static void RunCardBehaviorChecks(DslPackage package, Action<string, Action> check)
    {
        RunCardPlayPreparationChecks(package, check);
        RunTurnPhaseChecks(package, check);
        RunExhaustCauseChecks(package, check);
        check("BEHAVIOR owned body order changes actual damage without changing shared dispatch or notifications", () =>
        {
            foreach (var reverse in new[] { false, true })
            {
                using var copy = new Copy(package.Root);
                copy.Include("body-order.spire", """
                    procedure AddBlock { pure = true parameters = { amount = Int owner = Ref(Player) card = Ref(Card) target = Ref(Enemy) } result = Int body = {
                      return = { value = add(amount, field(owner, Player.Block)) }
                    } }
                    subscription AddBlock { event = ModifyAttackAdditive reaction = AddBlock }
                    """);
                if (reverse)
                {
                    var path = copy.Path("cards/ironclad/iron-wave.spire");
                    var source = File.ReadAllText(path);
                    var block = source.Split('\n').Single(line => line.Contains("call = { procedure = IronWaveBlock.WhenPlayed", StringComparison.Ordinal));
                    var attack = source.Split('\n').Single(line => line.Contains("call = { procedure = IronWaveDamage.WhenPlayedOnTarget", StringComparison.Ordinal));
                    File.WriteAllText(path, source.Replace(block, "BODY_SWAP", StringComparison.Ordinal).Replace(attack, block, StringComparison.Ordinal).Replace("BODY_SWAP", attack, StringComparison.Ordinal));
                }
                var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                var hp = IntField(s, FirstTarget(s), "Enemy", "Hp"); IcPlay(s, IcAdd(s, "IronWave"));
                Equal(reverse ? 5L : 10L, hp - IntField(s, FirstTarget(s), "Enemy", "Hp"), "body order controls modifier input");
                Equal(5L, Block(s), "both bodies gain exactly one block effect");
            }
        });
        check("BEHAVIOR repeated and nested plays keep explicit parents, payment and result snapshots", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("nested-context.spire", """
                action GrantRepeat { role = player body = {
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:one-two-punch-power" stacks = 1 } }
                } }
                procedure Nested { parameters = { owner = Ref(Player) card = Ref(Card) play = Ref(CardPlayRecord) } locals = { next = Binding } body = {
                  if = { condition = equal(field(card, Card.DefinitionId), "base-spire:strike-ironclad") then = {
                    if = { condition = not(equal(pureCall(AttacksPlayedThisTurn, owner), field(play, CardPlayRecord.PlayIndex))) then = { fail = { message = "Before execution counted its not-yet-started attack" } } }
                    let = { into = next value = first(where(collection(Hand), c, equal(field(c, Card.DefinitionId), "base-spire:defend-ironclad"))) }
                    if = { condition = not(isNone(next)) then = { call = { procedure = AutoPlayCard args = { owner = owner card = next forceExhaust = false } } } }
                  } }
                } }
                subscription Nested { event = BeforeCardExecution reaction = Nested }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "GrantRepeat");
            Set(s, Player(s), "Player", "Energy", Value.Of(10L));
            var child = IcAdd(s, "DefendIronclad"); var card = IcAdd(s, "StrikeIronclad"); IcPlay(s, card);
            var records = Members(s, "CardPlays"); Equal(3, records.Count, "two parent executions and one nested child");
            var type = s.RuleSet.TypeByQualifiedId("base-spire:CardPlayRecord");
            Value Get(InstanceId id, string name) => s.State.GetField(id, type.FieldIndex(name));
            var parents = records.Where(r => Get(r, "Card").AsRef() == card).ToArray();
            var nested = records.Single(r => Get(r, "Card").AsRef() == child);
            Equal(parents[0], Get(nested, "ParentPlay").AsRef(), "nested call refers to its exact parent execution");
            Equal(Get(parents[0], "ActionNumber"), Get(parents[1], "ActionNumber"), "repetitions share sequence identity");
            foreach (var (parent, index) in parents.Select((r, i) => (r, i)))
            {
                Equal((long)index, Get(parent, "PlayIndex").AsInt(), "repeat index"); Equal(2L, Get(parent, "PlayCount").AsInt(), "repeat count");
                Equal(1L, Get(parent, "PaidEnergy").AsInt(), "actual payment captured once");
                Equal("discard", Get(parent, "ResultPile").AsString(), "sequence result snapshot"); True(Get(parent, "Finished").AsBool(), "parent finished");
            }
            Equal(0L, Get(nested, "PaidEnergy").AsInt(), "free nested play has separate resource snapshot");
            True(Get(nested, "IsAutoPlay").AsBool(), "automatic policy is explicit"); Equal(9L, Energy(s), "only outer payment occurred");
            Equal(0L, IntField(s, Player(s), "Player", "PlayDepth"), "all frames unwound");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Equal(0, Members(s, "CardPlays").Count, "parent references clean with history");
        });
        check("BEHAVIOR native body choices resume the same unfinished execution without replaying paid effects", () =>
        {
            var s = Start(package); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            IcAdd(s, "StrikeIronclad"); var chosen = IcAdd(s, "DefendIronclad"); var brand = IcAdd(s, "Brand");
            var energy = Energy(s); IcPlay(s, brand); var pending = s.PendingDecision!;
            var record = Members(s, "CardPlays").Single(); var type = s.RuleSet.TypeByQualifiedId("base-spire:CardPlayRecord");
            True(s.State.GetField(record, type.FieldIndex("Started")).AsBool(), "body execution started before pause");
            True(!s.State.GetField(record, type.FieldIndex("Finished")).AsBool(), "no premature completion at choice");
            Equal(79L, Hp(s), "first body effect executes once");
            s.Answer(new DecisionAnswer(pending.RequestId, [chosen])); Healthy(s);
            Equal(79L, Hp(s), "resume does not replay HP loss"); Equal(energy, Energy(s), "zero-cost body payment not repeated");
            True(s.State.GetField(record, type.FieldIndex("Finished")).AsBool(), "same execution finished");
        });
    }
}
