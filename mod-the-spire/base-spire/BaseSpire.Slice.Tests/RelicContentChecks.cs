using BaseSpire.SinglePlayer;
using BaseSpire.Slice;
using CoreTheSpire.Engine;

namespace BaseSpire.Slice.Tests;

internal static class RelicContentChecks
{
    private static readonly Lazy<RuleSet> Rules = new(Build);
    private static string? selectedRelic;

    public static void Run(string? relic = null)
    {
        selectedRelic = relic;
        if (relic is not null && !SpireRelics.All.Any(r => r.Name == relic)) throw new ArgumentException("Unknown executable relic: " + relic);
        // Expected values are transcribed from the source relic classes, not the production JSON.
        foreach (var (name, hp, maximum, gold) in new[]
        {
            ("Pear", 60L, 93L, 0L), ("Mango", 64L, 97L, 0L), ("OldCoin", 50L, 83L, 300L),
            ("Strawberry", 57L, 90L, 0L), ("FakeMango", 53L, 86L, 0L),
            ("NutritiousOyster", 61L, 94L, 0L), ("LoomingFruit", 81L, 114L, 0L),
            ("GoldenPearl", 50L, 83L, 150L), ("SignetRing", 50L, 83L, 999L),
            ("FakeLeesWaffle", 58L, 83L, 0L), ("LeesWaffle", 90L, 90L, 0L),
        })
            Case([name], $"Relic content: {name} obtained health/gold persist into combat", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name);
                Check.Equal(hp, RunNumber(s, "Hp"), "obtained current HP");
                Check.Equal(maximum, RunNumber(s, "MaxHp"), "obtained maximum HP");
                Check.Equal(gold, RunNumber(s, "Gold"), "obtained gold");
                Submit(s, "BeginMonster");
                Check.Equal(hp, PlayerNumber(s, "Hp"), "next combat inherits obtained HP");
                Check.Equal(maximum, PlayerNumber(s, "MaxHp"), "next combat inherits obtained maximum");
            });
        Case(["FakeMerchantsRug", "WongoCustomerAppreciationBadge", "Circlet"], "Relic content: source identity-only badges and fallback acquisition", () =>
        {
            var s = New();
            foreach (var name in new[] { "FakeMerchantsRug", "WongoCustomerAppreciationBadge", "Circlet", "Circlet" }) Submit(s, "Obtain_" + name);
            Check.Equal(4, Members(s, "Relics").Count, "source explicit obtains each retain an instance");
            Check.Equal(50L, RunNumber(s, "Hp"), "identity-only effects leave HP alone");
            Check.Equal(0L, RunNumber(s, "Gold"), "identity-only effects leave gold alone");
        });
        Case(["PotionBelt"], "Relic content: PotionBelt adds two stable usable slots and persists across combat", () =>
        {
            var s = New(); Check.Equal(3L, PlayerNumber(s, "MaxPotionCount"), "source initial capacity");
            Submit(s, "Obtain_PotionBelt"); Check.Equal(5L, PlayerNumber(s, "MaxPotionCount"), "two slots gained");
            for (var i = 0; i < 5; i++) Submit(s, "GivePotion");
            Check.Sequence(new long[] { 0, 1, 2, 3, 4 }, Members(s, "PotionBelt").Select(p => Number(s, p, "Potion", "SlotIndex")), "new slots can be occupied");
            Submit(s, "GivePotion"); Check.Equal(5, Members(s, "PotionBelt").Count, "sixth bottle cannot exceed actual capacity");
            Submit(s, "BeginMonster");
            Check.Equal(5L, PlayerNumber(s, "MaxPotionCount"), "capacity survives battle setup");
            Check.Equal(5, Members(s, "PotionBelt").Count, "all bottle identities survive");
        });
        foreach (var (name, gain) in new[] { ("BurningBlood", 6L), ("BlackBlood", 12L), ("ChosenCheese", 1L) })
            Case([name], $"Relic content: {name} completed-combat hook commits once", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster"); Finish(s);
                Check.Equal(50L + gain, RunNumber(s, "Hp"), "after-end or victory HP committed");
                Check.Equal(name == "ChosenCheese" ? 84L : 83L, RunNumber(s, "MaxHp"), "only Cheese raises maximum");
                Submit(s, "BeginMonster");
                Check.Equal(50L + gain, PlayerNumber(s, "Hp"), "next battle preserves result");
            });
        foreach (var (name, power, stacks) in new[] { ("Vajra", "base-spire:strength", 1L),
            ("SwordOfJade", "base-spire:strength", 3L), ("OddlySmoothStone", "base-spire:dexterity-power", 1L),
            ("Gorget", "base-spire:plating-power", 4L), ("BronzeScales", "base-spire:thorns-power", 3L) })
            Case([name], $"Relic content: {name} applies its power on each combat entry", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                Check.Equal(stacks, Power(s, power), "first entry power");
                Submit(s, "BeginMonster"); Check.Equal(stacks, Power(s, power), "reset and reapply, no duplicate carryover");
            });
        foreach (var (name, block) in new[] { ("Anchor", 10L), ("FakeAnchor", 4L), ("Sai", 7L) })
            Case([name], $"Relic content: {name} first-turn block and turn lifetime", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                Check.Equal(block, PlayerNumber(s, "Block"), "first-turn block is not cleared");
                Submit(s, "EndTurn"); Check.Equal(name == "Sai" ? block : 0L, PlayerNumber(s, "Block"), "next turn clears old block and applies recurrent block only");
            });
        foreach (var (name, extra, turn) in new[] { ("Lantern", 1L, 1L), ("VeryHotCocoa", 4L, 1L), ("Candelabra", 2L, 2L), ("Chandelier", 3L, 3L) })
            Case([name], $"Relic content: {name} energy triggers on the source turn only", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                for (var t = 1L; t <= 4; t++)
                { Check.Equal(3L + (t == turn ? extra : 0L), PlayerNumber(s, "Energy"), "turn " + t); if (t < 4) Submit(s, "EndTurn"); }
            });
        foreach (var (name, gain) in new[] { ("BloodVial", 2L), ("FakeBloodVial", 1L) })
            Case([name], $"Relic content: {name} heals on first turn only", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                Check.Equal(50L + gain, PlayerNumber(s, "Hp"), "first turn healing");
                Submit(s, "EndTurn"); Check.Equal(50L + gain, PlayerNumber(s, "Hp"), "ritual turn has no damage or second heal");
            });
        foreach (var (name, power) in new[] { ("RedMask", "base-spire:weak"), ("BagOfMarbles", "base-spire:vulnerable") })
            Case([name], $"Relic content: {name} applies one debuff to all enemies", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                Check.True(Members(s, "Monsters").All(m => Power(s, power, m) == 1L), "all actual enemies affected");
            });
        foreach (var (name, amount, turn) in new[] { ("HornCleat", 14L, 2L), ("CaptainsWheel", 18L, 3L) })
            Case([name], $"Relic content: {name} adds block after clearing the specified turn", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                for (var t = 1L; t <= 4; t++)
                { Check.Equal(t == turn ? amount : 0L, PlayerNumber(s, "Block"), "turn " + t); if (t < 4) Submit(s, "EndTurn"); }
            });
        Case(["SlingOfCourage"], "Relic content: SlingOfCourage applies only in an Elite room", () =>
        {
            var s = New(); Submit(s, "Obtain_SlingOfCourage"); Submit(s, "BeginMonster");
            Check.Equal(0L, Power(s, "base-spire:strength"), "ordinary combat excluded");
            Submit(s, "BeginElite"); Check.Equal(2L, Power(s, "base-spire:strength"), "actual Elite enum included");
        });
        Case(["SwordOfStone"], "Relic content: fifth Elite victory replaces SwordOfStone in place", () =>
        {
            var s = New(); foreach (var name in new[] { "OldCoin", "SwordOfStone", "Pear" }) Submit(s, "Obtain_" + name);
            var before = Members(s, "Relics").ToArray(); Submit(s, "BeginMonster"); Finish(s);
            Check.Equal(0L, Number(s, before[1], "Relic", "Counter"), "ordinary victory does not count");
            for (var i = 1; i <= 5; i++)
            {
                Submit(s, "BeginElite"); Finish(s);
                if (i < 5) Check.Equal((long)i, Number(s, before[1], "Relic", "DisplayAmount"), "visible counter");
            }
            var after = Members(s, "Relics");
            Check.Equal(before[0], after[0], "left neighbour"); Check.Equal(before[2], after[2], "right neighbour");
            Check.Equal("base-spire:sword-of-jade", Value(s, after[1], "Relic", "DefinitionId").AsString(), "fifth victory transformed identity");
            Check.True(after[1] != before[1], "new identity");
            Submit(s, "BeginMonster"); Check.Equal(3L, Power(s, "base-spire:strength"), "replacement works next combat");
        });
        RunOrbs();
        RunCardPlay();
        RunTurnEnd();
        RunPassives();
        RunConditions();
        RunRooms();
        if (selectedRelic is null) RelicCoverageChecks.Validate();
    }

    public static void RunOrbs()
    {
        Case(["CrackedCore"], "Relic orbs: CrackedCore channels one Lightning on the source turn and its passive damages an enemy", () =>
        {
            var s = New(); Submit(s, "Obtain_CrackedCore"); Submit(s, "BeginMonster");
            var orb = Members(s, "OrbQueue").Single();
            Check.Equal("Lightning", Value(s, orb, "Orb", "Kind").AsString(), "source channel kind");
            Check.Equal(0L, Number(s, orb, "Orb", "StoredValue"), "Lightning stores no value");
            Check.Equal(1L, PlayerNumber(s, "OrbSlots"), "character without base slots receives the source fallback slot");
            SetMonsterHp(s, 500L);
            var before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - 3L, MonsterHpTotal(s), "unmodified Lightning passive hit");
            Check.Equal(1, Members(s, "OrbQueue").Count, "turn two channels nothing new");
            before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - 3L, MonsterHpTotal(s), "retained orb keeps firing on later turns");
            Submit(s, "BeginMonster");
            Check.Equal(1, Members(s, "OrbQueue").Count, "next battle rebuilds exactly one orb");
            Check.Equal("Lightning", Value(s, Members(s, "OrbQueue")[0], "Orb", "Kind").AsString(), "rebuilt kind");
        });
        Case(["DataDisk"], "Relic orbs: DataDisk Focus raises actual Lightning passive damage and is applied once per room", () =>
        {
            var s = New(); Submit(s, "Obtain_DataDisk"); Submit(s, "Obtain_CrackedCore"); Submit(s, "BeginMonster");
            Check.Equal(1L, Power(s, "base-spire:focus-power"), "one Focus from entering the combat room");
            SetMonsterHp(s, 500L);
            var before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - 4L, MonsterHpTotal(s), "Focus modifies the real orb passive");
            Check.Equal(1L, Power(s, "base-spire:focus-power"), "room entry does not re-apply each turn");
            before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - 4L, MonsterHpTotal(s), "second turn keeps the same stacks");
        });
        Case(["SymbioticVirus"], "Relic orbs: SymbioticVirus channels one Dark on the source turn and only its stored value grows", () =>
        {
            var s = New(); Submit(s, "Obtain_SymbioticVirus"); Submit(s, "BeginMonster");
            var orb = Members(s, "OrbQueue").Single();
            Check.Equal("Dark", Value(s, orb, "Orb", "Kind").AsString(), "source channel kind");
            Check.Equal(6L, Number(s, orb, "Orb", "StoredValue"), "source initial stored value");
            Submit(s, "EndTurn");
            Check.Equal(12L, Number(s, orb, "Orb", "StoredValue"), "passive adds six without evoking");
            Check.Equal(1, Members(s, "OrbQueue").Count, "turn two channels nothing new");
            Submit(s, "EndTurn");
            Check.Equal(18L, Number(s, orb, "Orb", "StoredValue"), "stored value keeps growing");
            Submit(s, "BeginMonster");
            var rebuilt = Members(s, "OrbQueue").Single();
            Check.True(rebuilt != orb, "next battle replaces the identity");
            Check.Equal(6L, Number(s, rebuilt, "Orb", "StoredValue"), "rebuilt orb starts from the source value");
        });
        Case(["RunicCapacitor"], "Relic orbs: RunicCapacitor slots hold three real channels and reset per battle", () =>
        {
            var s = New(); Submit(s, "Obtain_RunicCapacitor"); Submit(s, "Obtain_InfusedCore"); Submit(s, "BeginMonster");
            Check.Equal(3L, PlayerNumber(s, "OrbSlots"), "three slots added on the source turn");
            Check.Sequence(new[] { "Lightning", "Lightning", "Lightning" },
                Members(s, "OrbQueue").Select(orb => Value(s, orb, "Orb", "Kind").AsString()).ToArray(),
                "real capacity holds all three channels without evoking");
            SetMonsterHp(s, 500L);
            var before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - 12L, MonsterHpTotal(s), "three modified passives");
            Check.Equal(3L, PlayerNumber(s, "OrbSlots"), "slots are not added again on turn two");
            Check.Equal(3, Members(s, "OrbQueue").Count, "no evoke consumed the queue");
            Submit(s, "BeginMonster");
            Check.Equal(3L, PlayerNumber(s, "OrbSlots"), "next battle re-applies the source trigger");
            Check.Equal(3, Members(s, "OrbQueue").Count, "next battle channels three fresh orbs");
        });
        Case(["InfusedCore"], "Relic orbs: InfusedCore channels three Lightning and modifies the value it keeps", () =>
        {
            var s = New(); Submit(s, "Obtain_InfusedCore"); Submit(s, "BeginMonster");
            Check.Equal(1L, PlayerNumber(s, "OrbSlots"), "character without base slots receives the source fallback slot");
            Check.Equal(1, Members(s, "OrbQueue").Count, "two of the three channels overflow into evoke");
            Check.Equal("Lightning", Value(s, Members(s, "OrbQueue")[0], "Orb", "Kind").AsString(), "surviving channel kind");
            SetMonsterHp(s, 500L);
            var before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - 4L, MonsterHpTotal(s), "modified passive value");
            Check.Equal(1, Members(s, "OrbQueue").Count, "passive does not consume the orb");
            Submit(s, "BeginMonster");
            Check.Equal(1, Members(s, "OrbQueue").Count, "next battle rebuilds one orb from the source channel count");
        });
    }

    public static void RunCardPlay()
    {
        foreach (var (name, power) in new[] { ("Kunai", "base-spire:dexterity-power"), ("Shuriken", "base-spire:strength") })
            Case([name], $"Relic card play: {name} grants its power on every third Attack of a turn", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
                SetPlayer(s, "Energy", 10L);
                PlayAttack(s); PlayAttack(s);
                Check.Equal(0L, Power(s, power), "two Attacks do not trigger");
                PlayAttack(s); Check.Equal(1L, Power(s, power), "third Attack grants one stack");
                PlayAttack(s); PlayAttack(s);
                Check.Equal(1L, Power(s, power), "next two Attacks do not trigger");
                PlayAttack(s); Check.Equal(2L, Power(s, power), "sixth Attack grants again");
                Submit(s, "EndTurn");
                Check.Equal(2L, Power(s, power), "granted stacks persist into the next turn");
                SetPlayer(s, "Energy", 10L);
                PlayAttack(s); PlayAttack(s);
                Check.Equal(2L, Power(s, power), "the per-turn counter restarts");
                PlayAttack(s); Check.Equal(3L, Power(s, power), "third Attack of the new turn grants again");
            });
        Case(["OrnamentalFan"], "Relic card play: OrnamentalFan grants unpowered block on every third Attack of a turn", () =>
        {
            var s = New(); Submit(s, "Obtain_OrnamentalFan"); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            SetPlayer(s, "Energy", 10L);
            PlayAttack(s); PlayAttack(s);
            Check.Equal(0L, PlayerNumber(s, "Block"), "two Attacks do not grant block");
            PlayAttack(s); Check.Equal(4L, PlayerNumber(s, "Block"), "third Attack grants four block");
            PlayAttack(s); PlayAttack(s); PlayAttack(s);
            Check.Equal(8L, PlayerNumber(s, "Block"), "sixth Attack grants again");
            Submit(s, "EndTurn"); SetPlayer(s, "Energy", 10L);
            PlayAttack(s); PlayAttack(s);
            Check.Equal(0L, PlayerNumber(s, "Block"), "the per-turn counter restarts after the turn clears block");
            PlayAttack(s); Check.Equal(4L, PlayerNumber(s, "Block"), "third Attack of the new turn grants again");
        });
        Case(["Nunchaku"], "Relic card play: Nunchaku keeps its Attack counter across battles and grants energy on the tenth", () =>
        {
            var s = New(); Submit(s, "Obtain_Nunchaku"); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            SetPlayer(s, "Energy", 20L);
            for (var i = 0; i < 4; i++) PlayAttack(s);
            Check.Equal(4L, RelicDisplay(s, "base-spire:nunchaku"), "persistent counter after four Attacks");
            Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            Check.Equal(4L, RelicDisplay(s, "base-spire:nunchaku"), "battle transition does not reset the source counter");
            SetPlayer(s, "Energy", 10L);
            for (var i = 0; i < 4; i++) PlayAttack(s);
            SetPlayer(s, "Energy", 5L);
            PlayAttack(s);
            Check.Equal(4L, PlayerNumber(s, "Energy"), "ninth Attack grants nothing");
            SetPlayer(s, "Energy", 5L);
            PlayAttack(s);
            Check.Equal(5L, PlayerNumber(s, "Energy"), "tenth Attack refunds its cost with one energy");
            Check.Equal(0L, RelicDisplay(s, "base-spire:nunchaku"), "counter wraps at ten");
        });
        Case(["LetterOpener"], "Relic card play: LetterOpener damages every enemy on the third Skill of a turn", () =>
        {
            var s = New(); Submit(s, "Obtain_LetterOpener"); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            SetPlayer(s, "Hp", 95L); SetPlayer(s, "Energy", 20L);
            var perHit = 5L * Members(s, "Monsters").Count;
            var before = MonsterHpTotal(s);
            PlaySkill(s); PlaySkill(s);
            Check.Equal(before, MonsterHpTotal(s), "two Skills do not trigger");
            PlaySkill(s);
            Check.Equal(before - perHit, MonsterHpTotal(s), "third Skill damages every enemy");
            FormalSliceFixture.GivePower(s, FormalSliceFixture.Player(s), "base-spire:strength", 5L);
            before = MonsterHpTotal(s);
            PlaySkill(s); PlaySkill(s); PlaySkill(s);
            Check.Equal(before - perHit, MonsterHpTotal(s), "source area damage stays unpowered");
            Submit(s, "EndTurn"); SetPlayer(s, "Energy", 20L);
            before = MonsterHpTotal(s);
            PlaySkill(s); PlaySkill(s);
            Check.Equal(before, MonsterHpTotal(s), "the per-turn counter restarts");
            PlaySkill(s);
            Check.Equal(before - perHit, MonsterHpTotal(s), "third Skill of the new turn damages again");
        });
        Case(["CharonsAshes"], "Relic exhaust: CharonsAshes damages every enemy whenever a card is exhausted", () =>
        {
            var s = New(); Submit(s, "Obtain_CharonsAshes"); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            SetPlayer(s, "Hp", 95L);
            var perHit = 3L * Members(s, "Monsters").Count;
            var before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before, MonsterHpTotal(s), "a turn without exhaust does not damage");
            Submit(s, "GiveDazed");
            before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - perHit, MonsterHpTotal(s), "the Ethereal card exhausts and damages every enemy");
            FormalSliceFixture.GivePower(s, FormalSliceFixture.Player(s), "base-spire:strength", 5L);
            Submit(s, "GiveDazed");
            before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - perHit, MonsterHpTotal(s), "source area damage stays unpowered");
        });
    }

    public static void RunTurnEnd()
    {
        Case(["CloakClasp"], "Relic turn end: CloakClasp gains one unpowered block per hand card and none on an empty hand", () =>
        {
            // 普通格挡在回合结束时会被清掉，所以同场带上 SturdyClamp（保留格挡上限 10）把这一刻的格挡留下来观察。
            var reference = New(); Submit(reference, "Obtain_SturdyClamp"); Submit(reference, "BeginMonster");
            SetMonsterHp(reference, 500L); SetPlayer(reference, "Hp", 95L);
            Submit(reference, "EndTurn");
            Check.Equal(0L, PlayerNumber(reference, "Block"), "nothing grants block without the relic");

            var s = New(); Submit(s, "Obtain_SturdyClamp"); Submit(s, "Obtain_CloakClasp"); Submit(s, "BeginMonster");
            SetMonsterHp(s, 500L); SetPlayer(s, "Hp", 95L);
            var hand = Members(s, "Hand").Count;
            Check.True(hand > 0, "starting hand is not empty");
            FormalSliceFixture.GivePower(s, FormalSliceFixture.Player(s), "base-spire:dexterity-power", 5L);
            Submit(s, "EndTurn");
            Check.Equal((long)hand, PlayerNumber(s, "Block"), "one block per card, unaffected by Dexterity");

            var empty = New(); Submit(empty, "Obtain_SturdyClamp"); Submit(empty, "Obtain_CloakClasp");
            Submit(empty, "BeginMonster"); SetMonsterHp(empty, 500L); SetPlayer(empty, "Hp", 95L);
            EmptyHand(empty);
            Check.Equal(0, Members(empty, "Hand").Count, "hand emptied before the turn ends");
            Submit(empty, "EndTurn");
            Check.Equal(0L, PlayerNumber(empty, "Block"), "an empty hand gains no block");
        });
        Case(["ScreamingFlagon"], "Relic turn end: ScreamingFlagon damages every enemy only when the hand is empty", () =>
        {
            var s = New(); Submit(s, "Obtain_ScreamingFlagon"); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            SetPlayer(s, "Hp", 95L);
            var perHit = 20L * Members(s, "Monsters").Count;
            var before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before, MonsterHpTotal(s), "a non-empty hand does not damage");
            EmptyHand(s);
            Check.Equal(0, Members(s, "Hand").Count, "hand emptied before the turn ends");
            before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - perHit, MonsterHpTotal(s), "an empty hand damages every enemy for twenty");
        });
    }

    public static void RunPassives()
    {
        foreach (var (name, first, second, third, fourth) in new[]
        { ("BagOfPreparation", 7, 5, 5, 5), ("RingOfTheSnake", 7, 5, 5, 5),
          ("RingOfTheDrake", 7, 7, 7, 5), ("PaelsBlood", 6, 6, 6, 6) })
            Case([name], $"Relic passive: {name} modifies hand draw for exactly the source turns", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                var expected = new[] { first, second, third, fourth };
                for (var t = 0; t < 4; t++)
                { Check.Equal(expected[t], Members(s, "Hand").Count, "hand draw turn " + (t + 1)); if (t < 3) Submit(s, "EndTurn"); }
            });
        Case(["BigMushroom"], "Relic passive: BigMushroom gains max HP and reduces only first hand draw", () =>
        {
            var s = New(); Submit(s, "Obtain_BigMushroom"); Check.Equal(103L, RunNumber(s, "MaxHp"), "twenty maximum HP");
            Check.Equal(70L, RunNumber(s, "Hp"), "gain heals by actual amount");
            Submit(s, "BeginMonster"); Check.Equal(3, Members(s, "Hand").Count, "first hand loses two cards");
            Submit(s, "EndTurn"); Check.Equal(5, Members(s, "Hand").Count, "subsequent hand normal");
        });
        Case(["BoomingConch"], "Relic passive: BoomingConch first-turn Elite draw and energy share qualification", () =>
        {
            var s = New(); Submit(s, "Obtain_BoomingConch"); Submit(s, "BeginMonster");
            Check.Equal(5, Members(s, "Hand").Count, "normal-room draw unchanged"); Check.Equal(3L, PlayerNumber(s, "Energy"), "normal-room energy unchanged");
            Submit(s, "BeginElite"); Check.Equal(7, Members(s, "Hand").Count, "Elite first hand +2");
            Check.Equal(4L, PlayerNumber(s, "Energy"), "Elite first energy +1");
            Submit(s, "EndTurn"); Check.Equal(5, Members(s, "Hand").Count, "only initial hand"); Check.Equal(3L, PlayerNumber(s, "Energy"), "only initial energy");
        });
        Case(["Bread"], "Relic passive: Bread first turn loses energy then gains max energy on later turns", () =>
        {
            var s = New(); Submit(s, "Obtain_Bread"); Submit(s, "BeginMonster");
            Check.Equal(1L, PlayerNumber(s, "Energy"), "first turn 3 minus 2, not 4 minus 2");
            Submit(s, "EndTurn"); Check.Equal(4L, PlayerNumber(s, "Energy"), "second turn bonus");
            Submit(s, "EndTurn"); Check.Equal(4L, PlayerNumber(s, "Energy"), "recurring maximum bonus");
        });
        Case(["IceCream"], "Relic passive: IceCream resets first turn and preserves unspent energy thereafter", () =>
        {
            var s = New(); Submit(s, "Obtain_IceCream"); Submit(s, "BeginMonster");
            Check.Equal(3L, PlayerNumber(s, "Energy"), "first turn resets instead of double credit");
            Submit(s, "EndTurn"); Check.Equal(6L, PlayerNumber(s, "Energy"), "three carried plus three");
            Submit(s, "EndTurn"); Check.Equal(9L, PlayerNumber(s, "Energy"), "six carried plus three");
        });
        Case(["Sozu"], "Relic passive: Sozu raises max energy, prevents procurement, and removal releases both", () =>
        {
            var s = New(); Submit(s, "Obtain_Sozu"); Submit(s, "GivePotion"); Check.Equal(0, Members(s, "PotionBelt").Count, "empty slot does not bypass prevention");
            Submit(s, "BeginMonster"); Check.Equal(4L, PlayerNumber(s, "Energy"), "maximum bonus applies first turn");
            Submit(s, "DropFirstRelic"); Submit(s, "GivePotion"); Check.Equal(1, Members(s, "PotionBelt").Count, "no cached prevention after removal");
            Submit(s, "EndTurn"); Check.Equal(3L, PlayerNumber(s, "Energy"), "removed max-energy modifier no longer participates");
        });
        Case(["RunicPyramid"], "Relic passive: RunicPyramid prevents ordinary flush, preserves hand limit and still exhausts Ethereal", () =>
        {
            var s = New(); Submit(s, "Obtain_RunicPyramid"); Submit(s, "BeginMonster");
            var original = Members(s, "Hand").ToArray(); Submit(s, "GiveDazed"); Submit(s, "EndTurn");
            Check.True(original.All(c => Members(s, "Hand").Contains(c)), "old cards remain through normal flush");
            Check.Equal(10, Members(s, "Hand").Count, "draw fills remaining five slots only");
            Check.True(Members(s, "ExhaustPile").Any(c => Value(s, c, "Card", "DefinitionId").AsString() == "base-spire:dazed"), "Ethereal still exhausts");
        });
    }

    public static void RunConditions()
    {
        Case(["Brimstone"], "Relic conditions: Brimstone increases both sides once per player turn", () =>
        {
            var s = New(); Submit(s, "Obtain_Brimstone"); Submit(s, "BeginMonster");
            Check.Equal(2L, Power(s, "base-spire:strength"), "owner strength");
            Check.True(Members(s, "Monsters").All(m => Power(s, "base-spire:strength", m) == 1L), "all opponents");
            Submit(s, "EndTurn"); Check.Equal(4L, Power(s, "base-spire:strength"), "second player start");
            Check.True(Members(s, "Monsters").All(m => Power(s, "base-spire:strength", m) == 2L), "not a second trigger on enemy start");
        });
        Case(["SparklingRouge"], "Relic conditions: SparklingRouge grants both powers on turn three only", () =>
        {
            var s = New(); Submit(s, "Obtain_SparklingRouge"); Submit(s, "BeginMonster");
            Submit(s, "EndTurn"); Check.Equal(0L, Power(s, "base-spire:strength"), "no early strength");
            Submit(s, "EndTurn"); Check.Equal(1L, Power(s, "base-spire:strength"), "third-turn strength");
            Check.Equal(1L, Power(s, "base-spire:dexterity-power"), "third-turn dexterity");
            Submit(s, "EndTurn"); Check.Equal(1L, Power(s, "base-spire:strength"), "no recurring increase");
        });
        Case(["Pantograph"], "Relic conditions: Pantograph heals only before Boss combat and caps at maximum", () =>
        {
            var s = New(); Submit(s, "Obtain_Pantograph"); Submit(s, "BeginElite");
            Check.Equal(50L, PlayerNumber(s, "Hp"), "Elite excluded"); Submit(s, "BeginBoss");
            Check.Equal(75L, PlayerNumber(s, "Hp"), "Boss heals 25"); Finish(s); Submit(s, "BeginBoss");
            Check.Equal(83L, PlayerNumber(s, "Hp"), "next Boss heal capped");
        });
        Case(["RingingTriangle"], "Relic conditions: RingingTriangle retains first hand but flushes later hands", () =>
        {
            var s = New(); Submit(s, "Obtain_RingingTriangle"); Submit(s, "BeginMonster");
            var first = Members(s, "Hand").ToArray(); Submit(s, "EndTurn");
            Check.Equal(10, Members(s, "Hand").Count, "first turn retain permits capped draw");
            Check.True(first.All(c => Members(s, "Hand").Contains(c)), "first hand retained");
            Submit(s, "EndTurn"); Check.Equal(5, Members(s, "Hand").Count, "second hand flushed normally");
        });
        Case(["SturdyClamp"], "Relic conditions: SturdyClamp retains at most ten block and removal stops retention", () =>
        {
            var s = New(); Submit(s, "Obtain_SturdyClamp"); Submit(s, "BeginMonster"); SetPlayer(s, "Block", 24);
            Submit(s, "EndTurn"); Check.Equal(10L, PlayerNumber(s, "Block"), "ritual turn preserves only ten");
            SetPlayer(s, "Block", 7); Submit(s, "BeginMonster"); SetPlayer(s, "Block", 7);
            Submit(s, "EndTurn"); Check.Equal(7L, PlayerNumber(s, "Block"), "below limit preserved");
            Submit(s, "DropFirstRelic"); Submit(s, "BeginMonster"); SetPlayer(s, "Block", 24);
            Submit(s, "EndTurn"); Check.Equal(0L, PlayerNumber(s, "Block"), "removed relic allows normal clear");
        });
        Case(["MeatOnTheBone", "BurningBlood"], "Relic conditions: MeatOnTheBone inclusive half-HP threshold precedes ordinary victory healing", () =>
        {
            var s = New(); Submit(s, "Obtain_BurningBlood"); Submit(s, "Obtain_MeatOnTheBone"); Submit(s, "BeginMonster");
            SetPlayer(s, "Hp", 41); Finish(s); Check.Equal(59L, RunNumber(s, "Hp"), "41 + early12 + ordinary6 independent of inventory order");
            Submit(s, "BeginMonster"); SetPlayer(s, "Hp", 42); Finish(s); Check.Equal(48L, RunNumber(s, "Hp"), "42 is above floor(83/2)");
        });
        foreach (var (name, period) in new[] { ("HappyFlower", 3), ("FakeHappyFlower", 5) })
            Case([name], $"Relic conditions: {name} counter persists across battles and wraps at its source period", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                for (var t = 1; t < period; t++)
                {
                    Check.Equal(3L, PlayerNumber(s, "Energy"), "no early bonus");
                    Check.Equal((long)t, Number(s, Members(s, "Relics").Single(), "Relic", "DisplayAmount"), "visible persisted progress");
                    if (t + 1 < period) Submit(s, "EndTurn");
                }
                Submit(s, "BeginMonster"); Check.Equal(4L, PlayerNumber(s, "Energy"), "first turn of next fight completes period");
                Check.Equal(0L, Number(s, Members(s, "Relics").Single(), "Relic", "Counter"), "wrapped counter");
                Submit(s, "EndTurn"); Check.Equal(3L, PlayerNumber(s, "Energy"), "bonus lasts one turn");
            });
        foreach (var (name, first, second) in new[] { ("DivineDestiny", 6L, 6L), ("LunarPastry", 0L, 1L) })
            Case([name], $"Relic conditions: {name} changes stars at the source lifecycle point", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                Check.Equal(first, PlayerNumber(s, "Stars"), "initial stars"); Submit(s, "EndTurn");
                Check.Equal(second, PlayerNumber(s, "Stars"), "after one player turn");
                Submit(s, "BeginMonster"); Check.Equal(first, PlayerNumber(s, "Stars"), "battle reset");
            });
        Case(["MercuryHourglass"], "Relic conditions: MercuryHourglass damages every enemy at each player turn start", () =>
        {
            var s = New(); Submit(s, "Obtain_MercuryHourglass"); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            SetPlayer(s, "Hp", 95L);
            var perHit = 3L * Members(s, "Monsters").Count;
            var before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - perHit, MonsterHpTotal(s), "the next player turn start damages every enemy");
            FormalSliceFixture.GivePower(s, FormalSliceFixture.Player(s), "base-spire:strength", 5L);
            before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before - perHit, MonsterHpTotal(s), "source area damage stays unpowered");
        });
        Case(["FestivePopper"], "Relic conditions: FestivePopper damages every enemy on the first turn only", () =>
        {
            // 同 seed 的参照局给出这批敌人未受遗物影响的开局生命；差异即首回合的群体伤害。
            var reference = New(); Submit(reference, "BeginMonster");
            var s = New(); Submit(s, "Obtain_FestivePopper"); Submit(s, "BeginMonster");
            Check.Equal(Members(reference, "Monsters").Count, Members(s, "Monsters").Count, "same encounter for both runs");
            Check.Equal(MonsterHpTotal(reference) - 9L * Members(s, "Monsters").Count, MonsterHpTotal(s),
                "first turn damages every enemy for nine");
            SetMonsterHp(s, 500L); SetPlayer(s, "Hp", 95L);
            var before = MonsterHpTotal(s);
            Submit(s, "EndTurn");
            Check.Equal(before, MonsterHpTotal(s), "later turns do not damage");
        });
        Case(["Pendulum"], "Relic conditions: Pendulum draws on every third player turn and keeps its counter across battles", () =>
        {
            var s = New(); Submit(s, "Obtain_Pendulum"); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            SetPlayer(s, "Hp", 95L);
            Check.Equal(5, Members(s, "Hand").Count, "first turn draws the normal hand");
            Submit(s, "EndTurn");
            Check.Equal(5, Members(s, "Hand").Count, "second turn does not draw");
            Submit(s, "EndTurn");
            Check.Equal(6, Members(s, "Hand").Count, "third turn draws one extra card");

            var carried = New(); Submit(carried, "Obtain_Pendulum"); Submit(carried, "BeginMonster");
            SetMonsterHp(carried, 500L); SetPlayer(carried, "Hp", 95L);
            Submit(carried, "EndTurn");
            Check.Equal(5, Members(carried, "Hand").Count, "second turn still does not draw");
            Submit(carried, "BeginMonster"); SetMonsterHp(carried, 500L);
            Check.Equal(6, Members(carried, "Hand").Count, "the carried counter completes the period in the next battle");
        });
        Case(["CentennialPuzzle"], "Relic damage: CentennialPuzzle draws three cards on the first unblocked hit of a combat", () =>
        {
            var s = New(); Submit(s, "Obtain_CentennialPuzzle"); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            SetPlayer(s, "Hp", 95L);
            var turns = 0;
            while (Members(s, "Hand").Count == 5 && turns < 4) { Submit(s, "EndTurn"); turns++; }
            Check.True(turns < 4, "the combat produces an unblocked hit within four turns");
            Check.Equal(8, Members(s, "Hand").Count, "the first unblocked hit draws three extra cards");
            var before = PlayerNumber(s, "Hp");
            Submit(s, "EndTurn");
            Check.Equal(5, Members(s, "Hand").Count, "later turns in the same combat do not draw again");
            Check.True(PlayerNumber(s, "Hp") <= before, "the combat keeps dealing damage after the draw");
        });
        Case(["SelfFormingClay"], "Relic damage: SelfFormingClay pays three block per unblocked hit through its stacking power", () =>
        {
            var s = New(); Submit(s, "Obtain_SelfFormingClay"); Submit(s, "BeginMonster"); SetMonsterHp(s, 500L);
            SetPlayer(s, "Hp", 95L);
            var turns = 0;
            while (PlayerNumber(s, "Block") == 0L && turns < 4) { Submit(s, "EndTurn"); turns++; }
            var hits = PlayerNumber(s, "HpLossCount");
            Check.True(turns < 4 && hits > 0, "the combat produces unblocked hits within four turns");
            Check.Equal(3L * hits, PlayerNumber(s, "Block"), "each unblocked hit contributes three block");
            Check.Equal(0L, Power(s, "base-spire:self-forming-clay"), "the power is consumed when it pays out");
            var before = PlayerNumber(s, "Hp");
            Submit(s, "EndTurn");
            Check.True(PlayerNumber(s, "Block") % 3L == 0L, "a later payout stays a multiple of the per-hit amount");
            Check.True(PlayerNumber(s, "Hp") <= before, "the combat keeps dealing damage after the payout");
        });
        Case(["DivineRight"], "Relic conditions: DivineRight grants three stars on each combat room entry", () =>
        {
            var s = New(); Submit(s, "Obtain_DivineRight"); Submit(s, "BeginMonster");
            Check.Equal(3L, PlayerNumber(s, "Stars"), "first entry grants three stars");
            Submit(s, "BeginMonster");
            Check.Equal(3L, PlayerNumber(s, "Stars"), "battle reset re-grants without accumulating");
        });
    }

    public static void RunRooms()
    {
        Case(["MealTicket"], "Relic rooms: MealTicket heals on actual Shop entry and next combat inherits it", () =>
        {
            var s = New(); Submit(s, "Obtain_MealTicket"); Visit(s, "RestSite");
            Check.Equal(50L, RunNumber(s, "Hp"), "rest entry excluded"); Visit(s, "Shop");
            Check.Equal(65L, RunNumber(s, "Hp"), "Shop entry heals fifteen"); Submit(s, "BeginMonster");
            Check.Equal(65L, PlayerNumber(s, "Hp"), "battle uses permanent healing");
            Submit(s, "DropFirstRelic"); Visit(s, "Shop"); Check.Equal(65L, RunNumber(s, "Hp"), "removed listener stops healing");
        });
        Case(["EternalFeather"], "Relic rooms: EternalFeather counts complete groups of five permanent cards", () =>
        {
            var s = New(); Submit(s, "Obtain_EternalFeather");
            // Keep nine permanent cards: this distinguishes floor(deck/5)*3 from floor(deck*3/5).
            var deck = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:LongTermDeck");
            s.State.RunAtomic(() => { foreach (var card in Members(s, "LongTermDeck").Skip(9).ToArray()) s.State.RemoveFromCollection(deck.Id, card); });
            Check.Equal(9, Members(s, "LongTermDeck").Count, "controlled nine-card deck");
            Visit(s, "RestSite"); Check.Equal(53L, RunNumber(s, "Hp"), "one complete group heals three at entry");
            Submit(s, "BeginMonster"); Check.Equal(53L, PlayerNumber(s, "Hp"), "persistent result");
        });
        Case(["RegalPillow"], "Relic rooms: RegalPillow modifies resting, not entering, and removal releases the bonus", () =>
        {
            var s = New(); Submit(s, "Obtain_RegalPillow"); SetRun(s, "Hp", CoreTheSpire.Engine.Value.Of(20L)); Visit(s, "RestSite");
            Check.Equal(20L, RunNumber(s, "Hp"), "entering does not rest automatically"); Submit(s, "RestAtCampfire");
            Check.Equal(59L, RunNumber(s, "Hp"), "20 + floor(83*0.3) +15");
            Submit(s, "DropFirstRelic"); SetRun(s, "Hp", CoreTheSpire.Engine.Value.Of(20L)); Visit(s, "RestSite"); Submit(s, "RestAtCampfire");
            Check.Equal(44L, RunNumber(s, "Hp"), "ordinary rest after removal");
        });
        foreach (var (name, extra) in new[] { ("VenerableTeaSet", 2L), ("FakeVenerableTeaSet", 1L) })
            Case([name], $"Relic rooms: {name} charges on rest entry and consumes bonus exactly once", () =>
            {
                var s = New(); Submit(s, "Obtain_" + name); Submit(s, "BeginMonster");
                Check.Equal(3L, PlayerNumber(s, "Energy"), "no initial charge"); Visit(s, "RestSite");
                Visit(s, "Shop"); Submit(s, "BeginMonster"); Check.Equal(3L + extra, PlayerNumber(s, "Energy"), "charge survives intervening Shop");
                Submit(s, "EndTurn"); Check.Equal(3L, PlayerNumber(s, "Energy"), "no next-turn repeat");
                Submit(s, "BeginMonster"); Check.Equal(3L, PlayerNumber(s, "Energy"), "no next-battle repeat");
                Visit(s, "RestSite"); Submit(s, "BeginMonster"); Check.Equal(3L + extra, PlayerNumber(s, "Energy"), "another rest recharges");
            });
        Case(["Planisphere"], "Relic rooms: Planisphere follows Unknown map identity when actual room is combat", () =>
        {
            var s = New(); Submit(s, "Obtain_Planisphere"); Visit(s, "Monster");
            Check.Equal(50L, PlayerNumber(s, "Hp"), "direct Monster node excluded");
            SetRun(s, "UnknownMonsterOdds", CoreTheSpire.Engine.Value.Of((long)BitConverter.SingleToInt32Bits(1f)));
            foreach (var field in new[] { "UnknownEliteOdds", "UnknownTreasureOdds", "UnknownShopOdds" })
                SetRun(s, field, CoreTheSpire.Engine.Value.Of((long)BitConverter.SingleToInt32Bits(-1f)));
            Visit(s, "Unknown"); Check.Equal(55L, PlayerNumber(s, "Hp"), "Unknown resolving to Monster heals before first turn");
            Finish(s); Check.Equal(55L, RunNumber(s, "Hp"), "combat healing persists on settlement");
        });
    }

    private static void Visit(Session s, string pointType)
    {
        var ordinal = pointType switch { "Unknown" => 1, "Shop" => 2, "RestSite" => 4, "Monster" => 5, _ => throw new ArgumentException(pointType) };
        var relation = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:MapChildren");
        var target = Members(s, "MapNodes").First(n => Value(s, n, "MapNode", "Type").AsEnumOrdinal() == ordinal
            && Members(s, "MapNodes").Any(p => s.State.RelationTargets(relation.Id, p).Contains(n)));
        var parent = Members(s, "MapNodes").First(p => s.State.RelationTargets(relation.Id, p).Contains(target));
        SetRun(s, "CurrentNode", CoreTheSpire.Engine.Value.OfRef(parent)); SetRun(s, "MapPhase", CoreTheSpire.Engine.Value.Of(2L));
        SetRun(s, "MapFloor", CoreTheSpire.Engine.Value.Of(Number(s, parent, "MapNode", "Row") + 1));
        s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:ChooseMapPoint"), SliceManifest.Player,
            [CoreTheSpire.Engine.Value.OfRef(target)]);
        Check.False(s.Status == SessionStatus.Faulted, "map entry: " + s.Fault);
    }

    private static void SetRun(Session s, string field, Value value) => s.State.RunAtomic(() =>
        s.State.SetField(Members(s, "RunStates").Single(), s.RuleSet.TypeByQualifiedId("base-spire:Run").FieldIndex(field), value));

    private static void SetPlayer(Session s, string field, long value) => s.State.RunAtomic(() =>
        s.State.SetField(FormalSliceFixture.Player(s), s.RuleSet.TypeByQualifiedId("base-spire:Player").FieldIndex(field), CoreTheSpire.Engine.Value.Of(value)));

    private static void Case(string[] names, string scenario, Action test)
    {
        if (selectedRelic is not null && !names.Contains(selectedRelic, StringComparer.Ordinal)) return;
        Check.Run(scenario, test); foreach (var name in names) RelicCoverageChecks.Passed(name, scenario);
    }

    private static Session New()
    { var s = new Session(Rules.Value, 42, [SliceManifest.PlayerRole], computeBackend: BaseSpireComputeBackend.Instance); s.Start(); return s; }

    private static RuleSet Build()
    {
        var model = SinglePlayerContentSource.Shared.Model with { Run = new(50, 83, 0) };
        var p = CombatSlicePackage.Build(authorModel: model, relicLibrary: SpireRelics.All, startingRelics: [],
            actMapEnabled: true, terminalOnConfiguredRoomCount: false);
        var run = p.Declarations.OfType<EntityTypeSymbol>().Single(t => t.Name == "Run");
        var runs = p.Declarations.OfType<CollectionSymbol>().Single(t => t.Name == "RunStates");
        var obtain = p.Declarations.OfType<ProcedureSymbol>().Single(t => t.Name == "ObtainRelic");
        var start = p.Declarations.OfType<ProcedureSymbol>().Single(t => t.Name == "StartNextRoom");
        var remove = p.Declarations.OfType<ProcedureSymbol>().Single(t => t.Name == "RemoveRelic");
        var relics = p.Declarations.OfType<CollectionSymbol>().Single(t => t.Name == "Relics");
        var drop = p.ActionBody(p.Action("DropFirstRelic")); drop.Role(SliceManifest.PlayerRole);
        drop.Body(drop.Call(remove, drop.First(drop.Collection(runs)), drop.First(drop.Collection(relics))));
        var roomType = p.Declarations.OfType<EnumTypeSymbol>().Single(t => t.Name == MapRoomWiring.RoomTypeEnumName);
        foreach (var definition in SpireRelics.All)
        {
            var body = p.ActionBody(p.Action("Obtain_" + definition.Name)); body.Role(SliceManifest.PlayerRole);
            body.Body(body.Call(obtain, body.First(body.Collection(runs)), body.Const(definition.ContentId)));
        }
        foreach (var type in new[] { "Monster", "Elite", "Boss" })
        {
            var body = p.ActionBody(p.Action("Begin" + type)); body.Role(SliceManifest.PlayerRole);
            body.Body(body.SetField(body.First(body.Collection(runs)), run.Field("ActualRoomType"), body.EnumConst(roomType, type)),
                body.SetField(body.First(body.Collection(runs)), run.Field("MapPhase"), body.Const(1L)), body.Call(start));
        }
        var potion = p.Declarations.OfType<EntityTypeSymbol>().Single(t => t.Name == "Potion");
        var belt = p.Declarations.OfType<CollectionSymbol>().Single(t => t.Name == "PotionBelt");
        var give = p.ActionBody(p.Action("GivePotion")); give.Role(SliceManifest.PlayerRole);
        var created = give.Local("bottle", FieldTypeSchema.RefTo(potion));
        var acquirePotion = p.Declarations.OfType<ProcedureSymbol>().Single(t => t.Name == "AcquirePotion");
        // A fresh bottle is fixture input. The production acquisition procedure allocates its actual slot.
        give.Body(give.CreateEntity(potion, give.EntityScope(give.First(give.Collection(runs))), created,
            [(potion.Field("DefinitionId"), give.Const(model.PotionPool[0].Id)), (potion.Field("SlotIndex"), give.Const(-1L))]),
            give.Call(acquirePotion, give.LocalRef(created)),
            give.If(give.Less(give.CollectionIndex(belt, give.LocalRef(created)), give.Const(0L)), give.Destroy(give.LocalRef(created))));
        var card = p.Declarations.OfType<EntityTypeSymbol>().Single(t => t.Name == "Card");
        var hand = p.Declarations.OfType<CollectionSymbol>().Single(t => t.Name == "Hand");
        var kinds = p.Declarations.OfType<EnumTypeSymbol>().Single(t => t.Name == "CardKind");
        var dazed = p.ActionBody(p.Action("GiveDazed")); dazed.Role(SliceManifest.PlayerRole);
        var token = dazed.Local("token", FieldTypeSchema.RefTo(card));
        dazed.Body(dazed.CreateEntity(card, dazed.EntityScope(dazed.First(dazed.Collection(runs))), token,
            [(card.Field("DefinitionId"), dazed.Const("base-spire:dazed")), (card.Field("Kind"), dazed.EnumConst(kinds, "Status")),
             (card.Field("Unplayable"), dazed.Const(true)), (card.Field("Ethereal"), dazed.Const(true))], hand));
        var builder = new RuleSetBuilder(); builder.Add(p.Build()); return builder.Build();
    }

    private static void Finish(Session s)
    {
        foreach (var monster in Members(s, "Monsters").ToArray()) s.State.RunAtomic(() =>
            s.State.SetField(monster, s.RuleSet.TypeByQualifiedId("base-spire:Monster").FieldIndex("Hp"), CoreTheSpire.Engine.Value.Of(1L)));
        while (Members(s, "Monsters").Count > 0)
        {
            var card = FormalSliceFixture.MoveToHand(s, "base-spire:strike-ironclad");
            FormalSliceFixture.PlayAt(s, card, Members(s, "Monsters")[0]);
        }
    }
    private static void Submit(Session s, string name)
    {
        var action = s.LegalActions(SliceManifest.Player).Single(a => a.Definition.QualifiedId == "base-spire:" + name);
        s.Submit(action.Definition, SliceManifest.Player, action.Bindings.FirstOrDefault() ?? []);
        while (s.Status == SessionStatus.AwaitingDecision)
        { var pending = s.PendingDecision!; s.Answer(new DecisionAnswer(pending.RequestId, [pending.Candidates[0]])); }
        Check.False(s.Status == SessionStatus.Faulted, $"{name}: {s.Fault}");
    }
    private static void PlayAttack(Session s, string cardId = "base-spire:strike-ironclad")
    {
        var card = FormalSliceFixture.MoveToHand(s, cardId);
        FormalSliceFixture.PlayAt(s, card, Members(s, "Monsters")[0]);
        Check.False(s.Status == SessionStatus.Faulted, "card play: " + s.Fault);
    }
    /// <summary>把手牌直接挪到弃牌堆（受控输入，不触发打牌效果），用于观察“空手”时的时点。</summary>
    private static void EmptyHand(Session s)
    {
        var hand = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:Hand").Id;
        var discard = s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:DiscardPile").Id;
        s.State.RunAtomic(() =>
        {
            foreach (var card in s.State.CollectionMembers(hand).ToArray())
            {
                s.State.RemoveFromCollection(hand, card);
                s.State.AddToCollection(discard, card);
            }
        });
    }
    private static void PlayWholeHand(Session s)
    {
        SetPlayer(s, "Energy", 30L);
        foreach (var card in Members(s, "Hand").ToArray())
        {
            FormalSliceFixture.PlayAt(s, card, Members(s, "Monsters")[0]);
            Check.False(s.Status == SessionStatus.Faulted, "hand play: " + s.Fault);
        }
    }
    private static void PlaySkill(Session s, string cardId = "base-spire:defend-ironclad")
    {
        var card = FormalSliceFixture.MoveToHand(s, cardId);
        FormalSliceFixture.PlayAt(s, card, FormalSliceFixture.Player(s));
        Check.False(s.Status == SessionStatus.Faulted, "card play: " + s.Fault);
    }
    private static long RelicDisplay(Session s, string contentId) => Number(s,
        Members(s, "Relics").Single(r => Value(s, r, "Relic", "DefinitionId").AsString() == contentId), "Relic", "DisplayAmount");
    private static void SetMonsterHp(Session s, long hp)
    {
        foreach (var monster in Members(s, "Monsters").ToArray())
            FormalSliceFixture.SetNumber(s, monster, "base-spire:Monster", "Hp", hp);
    }
    private static long MonsterHpTotal(Session s) => Members(s, "Monsters").Sum(m => Number(s, m, "Monster", "Hp"));
    private static long MonsterMaxHpTotal(Session s) => Members(s, "Monsters").Sum(m => Number(s, m, "Monster", "MaxHp"));
    private static IReadOnlyList<InstanceId> Members(Session s, string name) => s.State.CollectionMembers(s.RuleSet.Collections.Single(c => c.QualifiedId == "base-spire:" + name).Id);
    private static Value Value(Session s, InstanceId instance, string type, string field) => s.State.GetField(instance, s.RuleSet.TypeByQualifiedId("base-spire:" + type).FieldIndex(field));
    private static long Number(Session s, InstanceId instance, string type, string field) => Value(s, instance, type, field).AsInt();
    private static long PlayerNumber(Session s, string field) => Number(s, FormalSliceFixture.Player(s), "Player", field);
    private static long RunNumber(Session s, string field) => Number(s, Members(s, "RunStates").Single(), "Run", field);
    private static long Power(Session s, string id, InstanceId? target = null)
    {
        var owner = target ?? FormalSliceFixture.Player(s);
        var relation = s.RuleSet.Relations.Single(r => r.QualifiedId == "base-spire:Powers");
        return s.State.RelationTargets(relation.Id, owner).Where(p => Value(s, p, "Power", "DefinitionId").AsString() == id)
            .Sum(p => Number(s, p, "Power", "Stacks"));
    }
}
