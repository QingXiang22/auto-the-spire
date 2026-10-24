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
        check("LIFECYCLE clone placement preserves lifecycle order, instance values and full-hand fallback", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("clone-placement.spire", """
                entity ClonePlacementProbe { fields = { Card = { type = Ref(Card) } Stage = { type = Int } } }
                procedure Cloned { parameters = { owner = Ref(Player) source = Ref(Card) card = Ref(Card) } locals = { probe = Binding } body = {
                  create = { type = ClonePlacementProbe scope = frameScope() into = probe fields = { Card = card Stage = 1 } }
                } }
                subscription Cloned { event = CardCloned reaction = Cloned }
                procedure Entered { parameters = { owner = Ref(Player) card = Ref(Card) clone = Bool } locals = { probe = Binding } body = {
                  if = { condition = clone then = {
                    let = { into = probe value = first(where(type(ClonePlacementProbe), p, equal(field(p, ClonePlacementProbe.Card), card))) }
                    if = { condition = isNone(probe) then = { fail = { message = "clone entered before clone notification" } } }
                    if = { condition = not(equal(field(probe, ClonePlacementProbe.Stage), 1)) then = { fail = { message = "duplicate clone entry" } } }
                    set = { target = probe field = ClonePlacementProbe.Stage value = 2 }
                  } }
                } }
                subscription Entered { event = CardEnteredCombat reaction = Entered }
                procedure Moved { parameters = { owner = Ref(Player) card = Ref(Card) from = String(length = 16) to = String(length = 16) reason = String(length = 32) } locals = { probe = Binding } body = {
                  let = { into = probe value = first(where(type(ClonePlacementProbe), p, equal(field(p, ClonePlacementProbe.Card), card))) }
                  if = { condition = not(isNone(probe)) then = {
                    if = { condition = not(and(equal(field(probe, ClonePlacementProbe.Stage), 2), and(equal(from, "none"), equal(reason, "clone-probe")))) then = { fail = { message = "clone placement order or reason changed" } } }
                    set = { target = probe field = ClonePlacementProbe.Stage value = 3 }
                  } }
                } }
                subscription Moved { event = CardMoved reaction = Moved }
                action CopyToHand { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } locals = { result = Binding } body = {
                  call = { procedure = CloneCombatCardToPile args = { owner = first(type(Player)) source = card to = "hand" reason = "clone-probe" } into = result }
                } }
                action CopyToDiscard { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } locals = { result = Binding } body = {
                  call = { procedure = CloneCombatCardToPile args = { owner = first(type(Player)) source = card to = "discard" reason = "clone-probe" } into = result }
                } }
                """);
            var variant = copy.Load();
            foreach (var (action, full, pile) in new[] { ("CopyToHand", false, "Hand"), ("CopyToHand", true, "DiscardPile"), ("CopyToDiscard", false, "DiscardPile") })
            {
                var s = Start(variant); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
                var source = IcAdd(s, "StrikeIronclad", upgraded: true);
                Set(s, source, "Card", "DamageGrowth", Value.Of(4L));
                if (full) for (var index = 0; index < 9; index++) IcAdd(s, "DefendIronclad");
                var draws = s.Random.TotalDraws; Submit(s, action, source);
                var type = s.RuleSet.TypeByQualifiedId("base-spire:ClonePlacementProbe"); var probe = s.State.AllInstances(type.Id).Single();
                var clone = s.State.GetField(probe, type.FieldIndex("Card")).AsRef();
                Equal(3L, IntField(s, probe, "ClonePlacementProbe", "Stage"), "clone notification, combat entry, then movement");
                True(clone != source && Hand(s).Contains(source) && Members(s, pile).Contains(clone), "new instance reaches the resolved pile without moving its source");
                True(CardUpgraded(s, clone), "clone retains the source upgrade"); Equal(4L, IntField(s, clone, "Card", "DamageGrowth"), "clone retains instance growth");
                Equal(draws, s.Random.TotalDraws, "cloning and placement consume no random draws");
            }
        });
        check("LIFECYCLE card cost modifiers compose, clone independently and expire by their own boundary", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("cost-probe.spire", """
                action Costs { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } body = {
                  call = { procedure = AddCardCostModifier args = { card = card source = "probe:combat" operation = "add" amount = 2 lifetime = "combat" } }
                  call = { procedure = AddCardCostModifier args = { card = card source = "probe:free" operation = "set" amount = 0 lifetime = "play" } }
                } }
                action Copy { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } locals = { result = Binding } body = {
                  call = { procedure = CloneCombatCardToPile args = { owner = first(type(Player)) source = card to = "hand" reason = "generate" } into = result }
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
                  call = { procedure = CloneCombatCardToPile args = { owner = first(type(Player)) source = card to = "hand" reason = "generate" } into = result }
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
        check("LIFECYCLE public hand operations compose across exhaust reactions without extending the batch", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("hand-operation-probe.spire", """
                procedure ReplaceExhausted { parameters = { owner = Ref(Player) card = Ref(Card) } locals = { replacement = Binding } body = {
                  call = { procedure = CreateCombatCard args = { owner = owner definition = "base-spire:anger" } into = replacement }
                  call = { procedure = AddGeneratedToHand args = { card = replacement } }
                } }
                subscription ReplaceExhausted { event = CardExhausted reaction = ReplaceExhausted }
                action Rebuild { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(InPlay) } locals = { size = Int } body = {
                  call = { procedure = ExhaustHandCards args = { owner = first(type(Player)) } into = size }
                  call = { procedure = GrowCardDamage args = { card = card amount = size } }
                  call = { procedure = GenerateCardsToHand args = { owner = first(type(Player)) count = size attacksOnly = true upgraded = true } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var receiver = IcAdd(s, "StrikeIronclad"); IcMove(s, receiver, "Hand", "InPlay");
            var originals = new[] { IcAdd(s, "DefendIronclad"), IcAdd(s, "Bash") };
            Submit(s, "Rebuild", receiver);
            Equal(2L, IntField(s, receiver, "Card", "DamageGrowth"), "captured original count, independent of callback additions");
            True(originals.All(c => Members(s, "ExhaustPile").Contains(c)), "both original cards exhausted");
            Equal(4, Hand(s).Count, "two callback cards survive alongside two generated cards");
            Equal(2, Hand(s).Count(c => s.State.GetField(c, CardField(s, "Upgraded")).AsBool()), "only the generated batch upgraded");
            True(Hand(s).All(c => s.State.GetField(c, CardField(s, "Kind")).AsString() == "attack"), "generation respects the caller's kind policy");
        });
        check("LIFECYCLE public transformation accepts a different kind and replacement without changing nonmatching cards", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("transform-operation-probe.spire", """
                action TransformSkills { role = player body = {
                  call = { procedure = TransformHandCards args = { owner = first(type(Player)) kind = "skill" replacementDefinition = "base-spire:strike-ironclad" upgraded = true } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var kept = IcAdd(s, "Anger"); var originals = new[] { IcAdd(s, "DefendIronclad"), IcAdd(s, "Impervious") };
            var draws = s.Random.TotalDraws; Submit(s, "TransformSkills");
            Equal(3, Hand(s).Count, "two matching cards replaced once"); True(Hand(s).Contains(kept), "nonmatching original instance kept");
            True(originals.All(c => !Hand(s).Contains(c) && !Members(s, "ExhaustPile").Contains(c)), "transformation removes without exhausting");
            foreach (var replacement in Hand(s).Where(c => c != kept))
            {
                Equal("base-spire:strike-ironclad", CardDefinition(s, replacement), "caller-selected definition");
                True(s.State.GetField(replacement, CardField(s, "Upgraded")).AsBool(), "replacement upgraded");
                Equal(9L, IntField(s, replacement, "Card", "BaseDamage"), "upgrade applied to replacement values");
            }
            Equal(draws, s.Random.TotalDraws, "fixed transformation consumes no random draws");
        });
        check("LIFECYCLE public generation applies caller cost policy before placement notifications", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("generation-operation-probe.spire", """
                procedure CheckCost { parameters = { owner = Ref(Player) card = Ref(Card) from = String(length = 16) to = String(length = 16) reason = String(length = 32) } body = {
                  if = { condition = equal(reason, "generate") then = {
                    if = { condition = not(equal(field(card, Card.Cost), 2)) then = { fail = { message = "cost applied after placement" } } }
                  } }
                } }
                subscription CheckCost { event = CardMoved reaction = CheckCost }
                action Generate { role = player locals = { generated = Binding } body = {
                  call = { procedure = GenerateCombatCardWithCost args = { owner = first(type(Player)) attacksOnly = true cost = 2 source = "probe:generator" lifetime = "combat" } into = generated }
                  if = { condition = not(isNone(generated)) then = { call = { procedure = AddGeneratedToHand args = { card = generated } } } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Submit(s, "Generate"); var card = Hand(s).Single(); Equal(2L, CardCost(s, card), "custom set value");
            IcNextTurn(s); Equal(2L, CardCost(s, card), "custom combat lifetime survives turn boundary");
            var modifier = Members(s, "CardCostModifiers").Single();
            Equal("probe:generator", s.State.GetField(modifier, s.RuleSet.TypeByQualifiedId("base-spire:CardCostModifier").FieldIndex("Source")).AsString(), "caller source retained");
        });
        check("LIFECYCLE public attack result and Power operations compose outside their original card definitions", () =>
        {
            using var copy = new Copy(package.Root); copy.Include("attack-operation-probe.spire", """
                action RewardFatal { role = player parameters = { card = Ref(Card) } candidates = { parameter = card query = collection(Hand) } locals = { attack = Binding } body = {
                  call = { procedure = ApplyEnemyPowerFromCard args = { owner = first(type(Player)) card = card play = none target = first(type(Enemy)) definition = "base-spire:weak-power" stacks = 2 } }
                  call = { procedure = AmplifyEnemyPowerFromCard args = { owner = first(type(Player)) card = card play = none target = first(type(Enemy)) definition = "base-spire:weak-power" multiplier = 3 } }
                  if = { condition = not(equal(pureCall(EnemyPowerAmount, first(type(Enemy)), "base-spire:weak-power"), 6)) then = { fail = { message = "caller Power or multiplier ignored" } } }
                  repeat = { count = 2 body = {
                    call = { procedure = CardAttackWithResult args = { owner = first(type(Player)) card = card play = none target = first(type(Enemy)) amount = 20 hits = 1 mode = "single" dynamic = false } into = attack }
                    if = { condition = pureCall(AttackKilledEnemy, attack, first(type(Enemy))) then = {
                      call = { procedure = IncreaseMaxHp args = { owner = first(type(Player)) amount = 2 } }
                    } }
                  } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var card = IcAdd(s, "StrikeIronclad"); Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(10L));
            var maxHp = IntField(s, Player(s), "Player", "MaxHp"); Submit(s, "RewardFatal", card);
            Equal(maxHp + 2, IntField(s, Player(s), "Player", "MaxHp"), "only the killing attack qualifies; stale results do not reward the second attack");
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
