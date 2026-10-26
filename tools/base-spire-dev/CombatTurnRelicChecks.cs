using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static Dictionary<string, string[]> RunCombatTurnChecks(DslPackage package, Action<string, Action> check)
    {
        var witness = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var shared = new List<string>();
        void Case(string name, Action body) { check(name, body); shared.Add(name); }
        void Own(string id, string name, Action body)
        {
            check(name, body);
            if (!witness.TryGetValue(id, out var list)) witness[id] = list = [];
            list.Add(name);
        }

        Case("TC01: a new combat cycle resets turn, energy and block but never HP", () =>
        {
            var s = Start(package); Submit(s, "Anchor.Obtain");
            Set(s, Player(s), "Player", "Hp", Value.Of(60L));
            Submit(s, "BeginDevelopmentCombat");
            Equal(0L, Turn(s), "turn counter starts at zero"); Equal(0L, Energy(s), "energy starts at zero");
            Submit(s, "BeginDevelopmentTurn"); Equal(3L, Energy(s), "energy reset to MaxEnergy on the first turn");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat");
            Equal(60L, Hp(s), "HP is not reset by combat start"); Equal(0L, Turn(s), "turn counter reset");
            Equal(0L, Energy(s), "energy cleared for the new cycle"); Equal(10L, Block(s), "start listener still rewards the new cycle");
        });
        Case("TC02: turn one keeps pre-combat block and turn two clears it before the block-cleared listener", () =>
        {
            var s = Start(package); Submit(s, "Anchor.Obtain"); Submit(s, "HornCleat.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Equal(10L, Block(s), "Anchor block before turn one");
            Submit(s, "BeginDevelopmentTurn"); Equal(10L, Block(s), "turn one does not clear the pre-combat block"); Equal(1L, Turn(s), "first turn");
            Set(s, Player(s), "Player", "Block", Value.Of(7L));
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn");
            Equal(14L, Block(s), "turn two clears 7 block then HornCleat grants exactly 14"); Equal(2L, Turn(s), "second turn");
        });
        Case("TC03: the turn gate rejects unopened and repeated transitions without side effects", () =>
        {
            var s = Start(package);
            True(!Legal(s, "BeginDevelopmentTurn"), "no turn candidate before combat"); True(!Legal(s, "EndDevelopmentTurn"), "no end candidate before combat");
            Submit(s, "BeginDevelopmentCombat");
            True(Legal(s, "BeginDevelopmentTurn"), "turn candidate inside combat"); True(!Legal(s, "EndDevelopmentTurn"), "no end candidate before the first turn");
            var before = s.StateVersion;
            Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:EndDevelopmentTurn"), s.Participant(0), []));
            Equal(before, s.StateVersion, "rejected end turn has no state changes");
            using var copy = new Copy(package.Root);
            copy.Include("turn-probes.spire", """
                action RepeatTurnProbe { role = player body = { call = { procedure = BeginDevelopmentTurn args = { owner = first(type(Player)) } } } }
                action CloseTurnProbe { role = player body = { call = { procedure = EndDevelopmentTurn args = { owner = first(type(Player)) } } } }
                """);
            var idle = Start(copy.Load()); Submit(idle, "BeginDevelopmentCombat");
            Throws<EngineFaultException>(() => idle.Submit(idle.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:CloseTurnProbe"), idle.Participant(0), []));
            Equal(SessionStatus.Faulted, idle.Status, "procedure guard rejects ending a closed turn"); True(idle.Fault!.Message.Contains("not open", StringComparison.Ordinal), "specific end guard failure");
            Submit(s, "BeginDevelopmentTurn"); Submit(s, "Draw"); Equal(1, Members(s, "Hand").Count, "development card drawn");
            True(!Legal(s, "BeginDevelopmentTurn"), "no second begin candidate while the turn is open");            var open = Start(copy.Load()); Submit(open, "BeginDevelopmentCombat"); Submit(open, "BeginDevelopmentTurn");
            Throws<EngineFaultException>(() => open.Submit(open.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:RepeatTurnProbe"), open.Participant(0), []));
            Equal(SessionStatus.Faulted, open.Status, "procedure guard rejects a second begin"); Equal(1L, Turn(open), "rejection happens before the turn counter advances");
            True(open.Fault!.Message.Contains("already open", StringComparison.Ordinal), "specific begin guard failure");
            Submit(s, "EndDevelopmentTurn");
            Equal(0, Members(s, "Hand").Count, "end turn discards the hand");
            Equal(1, Members(s, "DiscardPile").Count, "the drawn development card returns to the discard pile");
            Equal(2, Members(s, "DrawPile").Count, "the reshuffled cards stay in the draw pile");
            True(!Legal(s, "EndDevelopmentTurn"), "end candidate disappears once the turn is closed"); True(Legal(s, "BeginDevelopmentTurn"), "next turn can start");
        });
        Case("TC04: heal clamps to MaxHp, applies at partial health and leaves full health unchanged", () =>
        {
            var s = Start(package); Submit(s, "BloodVial.Obtain");
            Equal(80L, Hp(s), "development player starts at MaxHp");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(80L, Hp(s), "full health stays capped");
            var partial = Start(package); Submit(partial, "BloodVial.Obtain");
            Set(partial, Player(partial), "Player", "Hp", Value.Of(79L));
            Submit(partial, "BeginDevelopmentCombat"); Submit(partial, "BeginDevelopmentTurn");
            Equal(80L, Hp(partial), "heal of two is clamped at MaxHp");
            var low = Start(package); Submit(low, "BloodVial.Obtain");
            Set(low, Player(low), "Player", "Hp", Value.Of(70L));
            Submit(low, "BeginDevelopmentCombat"); Submit(low, "BeginDevelopmentTurn"); Equal(72L, Hp(low), "heal applies below the cap");
            Submit(low, "EndDevelopmentTurn"); Submit(low, "BeginDevelopmentTurn"); Equal(72L, Hp(low), "the listener only fires on its own turn");
        });
        Case("TC05: gained energy may exceed MaxEnergy and the next turn resets it", () =>
        {
            var s = Start(package); Submit(s, "Lantern.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Equal(0L, Energy(s), "no energy before the first turn");
            Submit(s, "BeginDevelopmentTurn"); Equal(4L, Energy(s), "MaxEnergy 3 plus the source reward");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); Equal(3L, Energy(s), "second turn resets to MaxEnergy only");
        });
        Case("TC06: turn templates ignore absent, removed, melted and other-owner relics", () =>
        {
            var bare = Start(package); Submit(bare, "BeginDevelopmentCombat");
            Submit(bare, "BeginDevelopmentTurn"); Equal(3L, Energy(bare), "no relic means no gain"); Equal(80L, Hp(bare), "no relic means no heal");
            Submit(bare, "EndDevelopmentTurn"); Submit(bare, "BeginDevelopmentTurn"); Equal(0L, Block(bare), "no relic means no block");
            foreach (var symbol in new[] { "Lantern", "BloodVial", "HornCleat", "HappyFlower", "Candelabra", "Chandelier", "VeryHotCocoa", "FakeHappyFlower", "Sai" })
            {
                var removed = Start(package); Submit(removed, symbol + ".Obtain"); Submit(removed, "RemoveOwnedRelic", Members(removed, "Relics").Single());
                Set(removed, Player(removed), "Player", "Hp", Value.Of(70L)); Submit(removed, "BeginDevelopmentCombat");
                for (var turn = 0; turn < 5; turn++) { Submit(removed, "BeginDevelopmentTurn"); Submit(removed, "EndDevelopmentTurn"); }
                Equal(3L, Energy(removed), symbol + ": removed instance grants no energy"); Equal(70L, Hp(removed), symbol + ": removed instance heals nothing"); Equal(0L, Block(removed), symbol + ": removed instance grants no block");
                var melted = Start(package); Submit(melted, symbol + ".Obtain"); Set(melted, Members(melted, "Relics").Single(), "Relic", "IsMelted", Value.Of(true));
                Set(melted, Player(melted), "Player", "Hp", Value.Of(70L)); Submit(melted, "BeginDevelopmentCombat");
                for (var turn = 0; turn < 5; turn++) { Submit(melted, "BeginDevelopmentTurn"); Submit(melted, "EndDevelopmentTurn"); }
                Equal(3L, Energy(melted), symbol + ": melted instance grants no energy"); Equal(70L, Hp(melted), symbol + ": melted instance heals nothing"); Equal(0L, Block(melted), symbol + ": melted instance grants no block");
            }
            using var copy = new Copy(package.Root);
            copy.Include("turn-owners.spire", """
                procedure MakeTurnOwner { locals = { other = Binding } body = {
                  create = { type = Player scope = frameScope() into = other }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:lantern" } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:horn-cleat" } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:candelabra" } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:chandelier" } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:very-hot-cocoa" } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:fake-happy-flower" } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "base-spire:sai" } }
                } }
                action MakeTurnOwner { role = player body = { call = { procedure = MakeTurnOwner } } }
                action BeginOtherTurn { role = player body = { call = { procedure = BeginDevelopmentCombat args = { owner = first(skip(type(Player), 1)) } } call = { procedure = BeginDevelopmentTurn args = { owner = first(skip(type(Player), 1)) } } } }
                """);
            var owners = Start(copy.Load()); Submit(owners, "MakeTurnOwner"); Submit(owners, "BeginDevelopmentCombat"); Submit(owners, "BeginDevelopmentTurn");
            Equal(3L, Energy(owners), "first owner has no relic and gains nothing"); Equal(0L, Block(owners), "first owner gets no block");
            var other = owners.State.AllInstances(owners.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Last();
            Submit(owners, "EndDevelopmentTurn"); Submit(owners, "BeginOtherTurn");
            Equal(8L, PlayerEnergy(owners, other), "second owner gains Lantern and Cocoa energy"); Equal(7L, IntField(owners, other, "Player", "Block"), "second owner gets Sai block"); Equal(0L, Block(owners), "first owner is untouched");
            foreach (var name in new[] { "Candelabra", "Chandelier", "VeryHotCocoa", "FakeHappyFlower", "Sai" })
            {
                True(Legal(owners, name + ".Obtain"), "other holder does not block development acquisition"); Submit(owners, name + ".Obtain");
                True(!Legal(owners, name + ".Obtain"), "same holder cannot obtain a duplicate through the development action");
            }
            Submit(owners, "BeginDevelopmentTurn"); Equal(5L, Energy(owners), "first holder gets its own Candelabra on turn two"); Equal(7L, Block(owners), "first holder gets its own Sai");
            Equal(8L, PlayerEnergy(owners, other), "first holder turn leaves other energy unchanged");
            foreach (var relic in Members(owners, "Relics").Where(r => owners.State.GetField(r, owners.RuleSet.TypeByQualifiedId("base-spire:Relic").FieldIndex("DefinitionId")).AsString() == "base-spire:fake-happy-flower"))
                Equal(1L, IntField(owners, relic, "Relic", "Counter"), "each holder flower counts only its own turn");
        });

        Own("base-spire:lantern", "TC05 Lantern: one energy on the owner's first turn only", () =>
        {
            var s = Start(package); Submit(s, "Lantern.Obtain"); Equal(0L, Energy(s), "obtaining is not a turn start");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(4L, Energy(s), "source reward on turn one");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); Equal(3L, Energy(s), "turn two has no reward");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal(4L, Energy(s), "the same held instance triggers again in a new combat");
        });
        Own("base-spire:blood-vial", "TC04 BloodVial: heals on the owner's first turn of each combat", () =>
        {
            var s = Start(package); Submit(s, "BloodVial.Obtain"); Set(s, Player(s), "Player", "Hp", Value.Of(60L));
            Submit(s, "BeginDevelopmentCombat"); Equal(60L, Hp(s), "combat start does not heal");
            Submit(s, "BeginDevelopmentTurn"); Equal(62L, Hp(s), "first turn heals two");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); Equal(62L, Hp(s), "later turns do not heal");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal(64L, Hp(s), "next combat heals again");
        });
        Own("base-spire:horn-cleat", "TC02 HornCleat: block on the second turn only, after the clear", () =>
        {
            var s = Start(package); Submit(s, "HornCleat.Obtain");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(0L, Block(s), "no block on turn one");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); Equal(14L, Block(s), "turn two grants exactly 14");
            Submit(s, "EndDevelopmentTurn"); Submit(s, "BeginDevelopmentTurn"); Equal(0L, Block(s), "turn three clears the block and grants nothing");
        });
        Own("base-spire:happy-flower", "TC06 HappyFlower: every third turn start gains energy and the counter survives combat", () =>
        {
            var s = Start(package); Submit(s, "HappyFlower.Obtain"); Submit(s, "BeginDevelopmentCombat");
            var expected = new[] { 3L, 3L, 4L, 3L, 3L };
            for (var index = 0; index < expected.Length; index++)
            {
                Submit(s, "BeginDevelopmentTurn"); Equal(expected[index], Energy(s), "turn " + (index + 1) + " energy");
                Submit(s, "EndDevelopmentTurn");
            }
            Equal(2L, Counter(s), "counter advanced modulo three and survives between turns");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Equal(4L, Energy(s), "the persisted counter activates on the next combat's first turn");
        });

        var timedEnergy = new[] {
            (Name: "Candelabra", Id: "base-spire:candelabra", Turn: 2, Amount: 2L),
            (Name: "Chandelier", Id: "base-spire:chandelier", Turn: 3, Amount: 3L),
            (Name: "VeryHotCocoa", Id: "base-spire:very-hot-cocoa", Turn: 1, Amount: 4L)
        };
        foreach (var row in timedEnergy)
            Own(row.Id, "TC timed energy: " + row.Name + " triggers on its source turn in each combat", () =>
            {
                var s = Start(package); Submit(s, row.Name + ".Obtain"); Equal(0L, Energy(s), "pickup grants no energy");
                for (var combat = 0; combat < 2; combat++)
                {
                    Submit(s, "BeginDevelopmentCombat"); Equal(0L, Energy(s), "combat start grants no energy");
                    for (var turn = 1; turn <= 5; turn++)
                    {
                        Submit(s, "BeginDevelopmentTurn"); Equal(turn == row.Turn ? 3L + row.Amount : 3L, Energy(s), "source energy on turn " + turn);
                        Submit(s, "EndDevelopmentTurn");
                    }
                    Submit(s, "EndDevelopmentCombat");
                }
                Equal(0L, s.Random.TotalDraws, "turn rewards consume no random");
            });
        Own("base-spire:fake-happy-flower", "TC FakeHappyFlower: fifth turn, cross-combat persistence and fresh acquisition counter", () =>
        {
            var s = Start(package); Submit(s, "FakeHappyFlower.Obtain"); Submit(s, "BeginDevelopmentCombat");
            for (var turn = 1; turn <= 9; turn++)
            {
                Submit(s, "BeginDevelopmentTurn"); Equal(turn == 5 ? 4L : 3L, Energy(s), "fifth owner turn only");
                Equal((long)(turn % 5), Counter(s), "instance counter modulo five"); Submit(s, "EndDevelopmentTurn");
            }
            Submit(s, "EndDevelopmentCombat"); Equal(4L, Counter(s), "combat end preserves count");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(4L, Energy(s), "tenth observed turn rewards in new combat"); Equal(0L, Counter(s), "reward resets counter");
            Submit(s, "RemoveOwnedRelic", Members(s, "Relics").Single()); Submit(s, "FakeHappyFlower.Obtain"); Equal(0L, Counter(s), "new instance starts fresh");
            IcNextTurn(s); Equal(3L, Energy(s), "fresh instance does not inherit old progress"); Equal(1L, Counter(s), "fresh progress");
        });
        Own("base-spire:sai", "TC Sai: first-turn Anchor and second-turn HornCleat stack after clearing, block stays unpowered", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("sai-powers.spire", """
                action BlockModifiers { role = player body = {
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:dexterity-power" stacks = 20 } }
                  call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:frail-power" stacks = 20 } }
                } }
                action PoweredBlockProbe { role = player body = { call = { procedure = GainBlock args = { owner = first(type(Player)) amount = 5 } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "Anchor.Obtain"); Submit(s, "HornCleat.Obtain"); Submit(s, "Sai.Obtain"); Equal(0L, Block(s), "pickup grants no block");
            Submit(s, "BeginDevelopmentCombat"); Submit(s, "BlockModifiers"); Equal(10L, Block(s), "Anchor at combat start only");
            Submit(s, "BeginDevelopmentTurn"); Equal(17L, Block(s), "turn one keeps Anchor and adds seven unpowered");
            IcNextTurn(s); Equal(21L, Block(s), "turn two clears old block, then fourteen plus seven");
            IcNextTurn(s); Equal(7L, Block(s), "later turn clears and grants seven");
            Submit(s, "PoweredBlockProbe"); Equal(25L, Block(s), "same active modifiers affect powered block: floor((5+20)*0.75)=18");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(17L, Block(s), "new combat includes first-turn Sai again");
        });
        Case("TC energy restrictions: reset bypasses NoEnergyGain, every source reward obeys it, counters still advance; energy caps at 999", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("turn-energy-restriction.spire", """
                action BlockTurnEnergy { role = player body = { call = { procedure = ApplyPlayerPower args = { owner = first(type(Player)) definition = "base-spire:no-energy-gain" stacks = 1 } } } }
                """);
            var variant = copy.Load();
            foreach (var row in timedEnergy.Select(r => (r.Name, r.Turn)).Append((Name: "FakeHappyFlower", Turn: 5)))
            {
                var s = Start(variant); Submit(s, row.Name + ".Obtain"); Submit(s, "BeginDevelopmentCombat");
                for (var turn = 1; turn <= row.Turn; turn++)
                {
                    Submit(s, "BlockTurnEnergy"); Submit(s, "BeginDevelopmentTurn"); Equal(3L, Energy(s), "reset works but award is suppressed: " + row.Name); Submit(s, "EndDevelopmentTurn");
                }
                if (row.Name == "FakeHappyFlower") { Equal(0L, Counter(s), "suppressed fifth-turn reward still resets progress"); Submit(s, "BeginDevelopmentTurn"); Equal(3L, Energy(s), "suppressed award is not deferred"); Equal(1L, Counter(s), "next count"); }
                var capped = Start(package); Submit(capped, row.Name + ".Obtain"); Set(capped, Player(capped), "Player", "MaxEnergy", Value.Of(998L)); Submit(capped, "BeginDevelopmentCombat");
                for (var turn = 1; turn <= row.Turn; turn++) { Submit(capped, "BeginDevelopmentTurn"); if (turn == row.Turn) Equal(999L, Energy(capped), "award clamps to resource cap"); Submit(capped, "EndDevelopmentTurn"); }
            }
        });
        Case("TC instance progress: duplicate flowers count independently, melted instances freeze, removed instances restart; Sai caps block", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("duplicate-turn-relics.spire", """
                action ExtraFlower { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:fake-happy-flower" } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "FakeHappyFlower.Obtain"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            var first = Members(s, "Relics").Single(); Submit(s, "ExtraFlower"); var second = Members(s, "Relics").Last();
            for (var turn = 2; turn <= 4; turn++) IcNextTurn(s);
            Equal(4L, IntField(s, first, "Relic", "Counter"), "first flower sees four"); Equal(3L, IntField(s, second, "Relic", "Counter"), "second sees three");
            var version = s.StateVersion; Throws<EngineFaultException>(() => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:BeginDevelopmentTurn"), s.Participant(0), []));
            Equal(version, s.StateVersion, "rejected repeated turn cannot advance counters");
            Set(s, first, "Relic", "IsMelted", Value.Of(true)); IcNextTurn(s); Equal(3L, Energy(s), "melted flower skips its due reward"); Equal(4L, IntField(s, first, "Relic", "Counter"), "melted count frozen");
            IcNextTurn(s); Equal(4L, Energy(s), "second instance independently reaches five");
            Submit(s, "RemoveOwnedRelic", first); Submit(s, "ExtraFlower"); Equal(0L, IntField(s, Members(s, "Relics").Last(), "Relic", "Counter"), "new counter replaces removed progress");
            var block = Start(package); Submit(block, "Sai.Obtain"); Submit(block, "BeginDevelopmentCombat"); Set(block, Player(block), "Player", "Block", Value.Of(999999997L));
            Submit(block, "BeginDevelopmentTurn"); Equal(999999999L, Block(block), "Sai respects the source Creature.GainBlockInternal cap");
        });

        foreach (var list in witness.Values) list.InsertRange(0, shared);
        return witness.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.Ordinal);
    }

    private static long Turn(Session s) => Field(s, "TurnNumber");
    private static long Energy(Session s) => Field(s, "Energy");
    private static long Hp(Session s) => Field(s, "Hp");
    private static long Counter(Session s) => s.State.GetField(Members(s, "Relics").Single(), s.RuleSet.TypeByQualifiedId("base-spire:Relic").FieldIndex("Counter")).AsInt();
    private static long PlayerEnergy(Session s, InstanceId player) => s.State.GetField(player, s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex("Energy")).AsInt();
    private static long Field(Session s, string name) => s.State.GetField(Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex(name)).AsInt();
}
