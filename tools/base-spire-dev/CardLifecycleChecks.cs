using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunCardLifecycleChecks(DslPackage package, Action<string, Action> check)
    {
        check("LIFECYCLE runtime catalogs reject source records, null definitions and invalid mappings", () =>
        {
            foreach (var format in new[] { "engine-dsl/source-catalog@1", "engine-dsl/source-data@1" })
                RejectCopy(package.Root, c => c.EditJson("cards/catalog.json", j => j["format"] = format));
            RejectCopy(package.Root, c => c.EditJson("cards/catalog.json", j => j["source_baseline"] = "old-progress"));
            RejectCopy(package.Root, c => c.EditJson("cards/catalog.json", j => j["entries"]![0]!["evidence"] = "claimed complete"));
            RejectCopy(package.Root, c => c.EditJson("cards/catalog.json", j => j["entries"]![0]!["definition"] = null));
            RejectCopy(package.Root, c => c.EditJson("cards/catalog.json", j => j["entries"]!.AsArray().Add(j["entries"]![0]!.DeepClone())));
            RejectCopy(package.Root, c => c.EditJson("cards/catalog.json", j => j["entries"]![0]!["definition"] = "public/metadata.json"));
            using var unknown = new Copy(package.Root);
            unknown.EditJson("cards/catalog.json", j => j["entries"]![0]!["id"] = "base-spire:not-a-source-card");
            Throws<FormatException>(() => ContentRegistration.Check(unknown.Load()));
            using var missing = new Copy(package.Root);
            missing.EditJson("cards/catalog.json", j => j["entries"]!.AsArray().RemoveAt(0));
            Throws<FormatException>(() => ContentRegistration.Check(missing.Load()));
        });
        check("LIFECYCLE registration detects public and source mapping drift and derives only ID lists", () =>
        {
            using var copy = new Copy(package.Root);
            var source = File.ReadAllText(copy.Path("cards/catalog.json"));
            copy.EditJson("public/metadata.json", json => json["content"]!["base-spire:Card"]!["content_ids"]!.AsArray().RemoveAt(1));
            Throws<FormatException>(() => ContentRegistration.Check(copy.Load()));
            ContentRegistration.Check(copy.Load(), synchronize: true); ContentRegistration.Check(copy.Load());
            Equal(source, File.ReadAllText(copy.Path("cards/catalog.json")), "public ID derivation cannot change runtime definitions");
            var once = File.ReadAllText(copy.Path("public/metadata.json")); ContentRegistration.Check(copy.Load(), synchronize: true);
            Equal(once, File.ReadAllText(copy.Path("public/metadata.json")), "derivation is deterministic");
            copy.EditJson("cards/catalog.json", json => json["entries"]!.AsArray().First(r => r!["id"]!.GetValue<string>() == "base-spire:anger")!["definition"] = "cards/ironclad/bash.spire");
            Throws<FormatException>(() => ContentRegistration.Check(copy.Load()));
            copy.EditJson("cards/catalog.json", json =>
            {
                var rows = json["entries"]!.AsArray();
                rows.First(r => r!["id"]!.GetValue<string>() == "base-spire:anger")!["definition"] = "cards/ironclad/anger.spire";
                rows.First(r => r!["id"]!.GetValue<string>() == "base-spire:giant-rock")!["definition"] = "cards/ironclad/primal-force.spire";
            });
            Throws<FormatException>(() => ContentRegistration.Check(copy.Load()));
        });
        check("LIFECYCLE dynamic keywords freeze ethereal and flush batches and gate both play paths", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("keyword-probe.spire", """
                procedure Keywords { pure = true parameters = { value = Bool card = Ref(Card) keyword = String(length = 16) } result = Bool body = {
                  return = { value = or(value, or(and(equal(keyword, "ethereal"), equal(field(card, Card.DefinitionId), "base-spire:strike-ironclad")), or(and(equal(keyword, "retain"), equal(field(card, Card.DefinitionId), "base-spire:defend-ironclad")), and(equal(keyword, "unplayable"), equal(field(card, Card.DefinitionId), "base-spire:bash"))))) }
                } }
                subscription Keywords { event = ModifyCardKeyword reaction = Keywords }
                procedure ExhaustDraw { parameters = { owner = Ref(Player) card = Ref(Card) } body = { call = { procedure = DrawOne args = { owner = owner fromHandDraw = false } } } }
                subscription ExhaustDraw { event = CardExhausted reaction = ExhaustDraw }
                procedure Flush { parameters = { owner = Ref(Player) card = Ref(Card) from = String(length = 16) to = String(length = 16) reason = String(length = 32) } body = {
                  if = { condition = and(equal(reason, "flush"), not(equal(count(collection(Hand)), 1))) then = { fail = { message = "Flush callback ran before its whole batch committed" } } }
                } }
                subscription Flush { event = CardMoved reaction = Flush }
                action Auto { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = {
                  call = { procedure = AutoPlayCard args = { owner = first(type(Player)) card = card forceExhaust = false } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var ethereal = IcAdd(s, "StrikeIronclad"); var retain = IcAdd(s, "DefendIronclad"); var blocked = IcAdd(s, "Bash");
            True(!s.LegalActions(s.Participant(0)).Any(a => a.Definition.QualifiedId == "base-spire:PlayAttackFromHand" && a.Bindings.Any(b => b[0].AsRef() == blocked)), "manual candidates share Unplayable gate");
            Submit(s, "Auto", blocked); Equal(0, Members(s, "CardPlays").Count, "blocked autoplay does not run a body or start history");
            True(Members(s, "DiscardPile").Contains(blocked), "blocked autoplay moves to result pile without payment");
            var drawn = IcAdd(s, "StrikeIronclad"); IcMove(s, drawn, "Hand", "DrawPile");
            Submit(s, "EndDevelopmentTurn"); True(Members(s, "ExhaustPile").Contains(ethereal), "original ethereal exhausted");
            True(!Members(s, "ExhaustPile").Contains(drawn) && Members(s, "DiscardPile").Contains(drawn), "exhaust reaction draw is outside ethereal snapshot, then participates in flush");
            Equal(retain, Hand(s).Single(), "Retain survives the shared hand finish");
            Equal(0, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:HandFlushEntry").Id).Count, "temporary batch cleaned");
        });
        check("LIFECYCLE card cost modifiers compose, clone independently and expire by their own boundary", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("cost-probe.spire", """
                action Costs { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = {
                  call = { procedure = AddCardCostModifier args = { card = card source = "probe:combat" operation = "add" amount = 2 lifetime = "combat" } }
                  call = { procedure = AddCardCostModifier args = { card = card source = "probe:free" operation = "set" amount = 0 lifetime = "play" } }
                } }
                action Copy { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } locals = { result = Binding } body = {
                  call = { procedure = CloneCombatCard args = { owner = first(type(Player)) source = card } into = result }
                  call = { procedure = AddGeneratedToHand args = { card = result } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, Player(s), "Player", "Energy", Value.Of(10L)); var source = IcAdd(s, "StrikeIronclad"); Submit(s, "Costs", source); Submit(s, "Copy", source);
            var clone = Hand(s).Single(c => c != source); Equal(0L, CardCost(s, source), "last set wins in declared order"); Equal(0L, CardCost(s, clone), "cost component deep copied");
            IcPlay(s, source); Equal(3L, CardCost(s, source), "played override expires, combat add remains"); Equal(0L, CardCost(s, clone), "clone retained its independent play override");
            IcNextTurn(s); Equal(0L, CardCost(s, clone), "play-only modifier survives turn end");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat");
            Equal(1L, CardCost(s, source), "combat cost expires even for retained development instances"); Equal(1L, CardCost(s, clone), "all temporary modifiers end at the combat boundary");
        });
        check("LIFECYCLE Sown preserves permanent source and clone status across upgrade, play and two battles", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("sown-probe.spire", """
                action Enchant { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(RunDeck) } body = {
                  call = { procedure = EnchantCard args = { owner = first(type(Player)) card = card definition = "base-spire:sown" amount = 1 } }
                } }
                action Copy { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(DiscardPile) } locals = { result = Binding } body = {
                  call = { procedure = CloneCombatCard args = { owner = first(type(Player)) source = card } into = result }
                  call = { procedure = AddGeneratedToHand args = { card = result } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "Ironclad.Initialize");
            var permanent = Members(s, "RunDeck").First(c => CardDefinition(s, c) == "base-spire:strike-ironclad"); Submit(s, "Enchant", permanent);
            var attachment = s.RuleSet.TypeByQualifiedId("base-spire:CardAttachment");
            InstanceId Attached(InstanceId card) => s.State.AllInstances(attachment.Id).Single(a => s.State.GetField(a, attachment.FieldIndex("Card")).AsRef() == card);
            string Status(InstanceId card) => s.State.GetField(Attached(card), attachment.FieldIndex("Status")).AsString();
            Submit(s, "AddDevelopmentTarget");
            for (var battle = 0; battle < 2; battle++)
            {
                Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L)); Set(s, Player(s), "Player", "Energy", Value.Of(10L));
                var card = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Card").Id).Single(c => s.State.GetField(c, CardField(s, "PermanentSource")).Kind == ValueKind.Ref && s.State.GetField(c, CardField(s, "PermanentSource")).AsRef() == permanent);
                if (Hand(s).Contains(card)) IcMove(s, card, "Hand", "DrawPile");
                var view = s.Observe(s.Participant(0), "base-spire:PlayerView");
                True(view.Instances.All(i => i.Id != Attached(card)), "hidden combat card does not leak through its attachment");
                IcMove(s, card, "DrawPile", "Hand"); view = s.Observe(s.Participant(0), "base-spire:PlayerView");
                True(view.Instances.Any(i => i.Id == Attached(card)), "visible card publishes real attachment identity and status");
                Submit(s, "UpgradeCardInHand", card); Equal("normal", Status(card), "upgrade preserves unspent attachment");
                IcPlay(s, card); Equal(10L, Energy(s), "pay one then Sown restores one"); Equal("disabled", Status(card), "spent combat attachment"); Equal("normal", Status(permanent), "permanent source not mutated");
                Submit(s, "Copy", card); var clone = Hand(s).Single(c => CardDefinition(s, c) == "base-spire:strike-ironclad" && c != card && s.State.GetField(c, CardField(s, "PermanentSource")).Kind == ValueKind.None);
                Equal("disabled", Status(clone), "combat clone preserves spent status"); IcPlay(s, clone); Equal(9L, Energy(s), "spent clone cannot regain energy");
                Submit(s, "EndDevelopmentCombat");
            }
        });
        check("LIFECYCLE Power instance policies and blocked applications use explicit appliers", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("power-instance-probe.spire", """
                use PerApplier { template = PowerDefinition args = { definition = "probe:per-applier" instanceMode = "per-applier" } }
                use Independent { template = PowerDefinition args = { definition = "probe:independent" instanceMode = "independent" } }
                procedure Block { pure = true parameters = { value = Bool player = Binding enemy = Binding definition = String(length = 64) applierPlayer = Binding applierEnemy = Binding card = Binding play = Binding } result = Bool body = {
                  return = { value = not(equal(definition, "base-spire:weak-power")) }
                } }
                subscription Block { event = ShouldApplyPower reaction = Block }
                action Apply { role = player locals = { other = Binding power = Binding } body = {
                  create = { type = Player scope = frameScope() into = other }
                  repeat = { count = 2 body = {
                    call = { procedure = ApplyPowerWithSource args = { player = first(type(Player)) enemy = none definition = "probe:per-applier" stacks = 2 untilTurnEnd = false applierPlayer = first(type(Player)) applierEnemy = none card = none play = none } into = power }
                    call = { procedure = ApplyPowerWithSource args = { player = first(type(Player)) enemy = none definition = "probe:independent" stacks = 1 untilTurnEnd = false applierPlayer = other applierEnemy = none card = none play = none } into = power }
                  } }
                  call = { procedure = ApplyPowerWithSource args = { player = first(type(Player)) enemy = none definition = "probe:per-applier" stacks = 3 untilTurnEnd = false applierPlayer = other applierEnemy = none card = none play = none } into = power }
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:weak-power" stacks = 2 } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "Apply"); Equal(4, Members(s, "Powers").Count, "two per-applier and two independent instances; blocked Weak absent");
            var type = s.RuleSet.TypeByQualifiedId("base-spire:Power");
            var per = Members(s, "Powers").Where(p => s.State.GetField(p, type.FieldIndex("DefinitionId")).AsString() == "probe:per-applier").ToArray();
            Equal("3,4", string.Join(',', per.Select(p => IntField(s, p, "Power", "Stacks")).Order()), "same applier merges; different appliers isolate");
            True(per.Select(p => s.State.GetField(p, type.FieldIndex("ApplierPlayer")).AsRef()).Distinct().Count() == 2, "real applier references retained");
        });
    }
}
