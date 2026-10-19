using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    public static int RunRelicContentOnly(DslPackage package, string? filter = null)
    {
        var count = 0; var skipped = 0;
        var total = System.Diagnostics.Stopwatch.StartNew();
        void Check(string name, Action body)
        {
            if (filter is not null && !name.StartsWith(filter, StringComparison.OrdinalIgnoreCase)) { skipped++; return; }
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try { body(); }
            catch { Console.Error.WriteLine($"FAIL {name} ({timer.Elapsed.TotalSeconds:F3}s)"); throw; }
            count++; Console.WriteLine($"ok   {name} ({timer.Elapsed.TotalSeconds:F3}s)");
        }
        RunRelicContentChecks(package, Check);
        RunCombatTurnChecks(package, Check);
        RunRelicModifierChecks(package, Check);
        RunAttackModifierRelicChecks(package, Check);
        RunXValueRelicChecks(package, Check);
        RunPickupUpgradeChecks(package, Check);
        RunEffectListenerChecks(package, Check);
        RunSelfFormingClayChecks(package, Check);
        if (count == 0) throw new ArgumentException("No relic-content checks match --filter: " + filter);
        Console.WriteLine($"All {count} selected relic-content checks passed; skipped={skipped}; elapsed={total.Elapsed.TotalSeconds:F3}s. Full check is required for coverage evidence."); return 0;
    }
    private static Dictionary<string, string[]> RunRelicContentChecks(DslPackage package, Action<string, Action> check)
    {
        var witnesses = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Own(string id, string name, Action body) { check(name, body); witnesses[id] = witnesses.GetValueOrDefault(id, []).Append(name).ToArray(); }
        Session Combat(params string[] relics)
        {
            var s = Start(package); foreach (var r in relics) Submit(s, r + ".Obtain");
            Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L)); Set(s, FirstTarget(s), "Enemy", "MaxHp", Value.Of(1000L));
            Set(s, Player(s), "Player", "Energy", Value.Of(100L)); return s;
        }
        void Plays(Session s, string card, int count) { for (var i = 0; i < count; i++) IcPlay(s, IcAdd(s, card)); }
        InstanceId Relic(Session s, string id) => Members(s, "Relics").Single(r => s.State.GetField(r, s.RuleSet.TypeByQualifiedId("base-spire:Relic").FieldIndex("DefinitionId")).AsString() == id);

        foreach (var row in new[] { ("Pear", "base-spire:pear", 10L), ("Mango", "base-spire:mango", 14L), ("LeesWaffle", "base-spire:lees-waffle", 7L),
            ("FakeMango", "base-spire:fake-mango", 3L), ("LoomingFruit", "base-spire:looming-fruit", 31L), ("NutritiousOyster", "base-spire:nutritious-oyster", 11L) })
            Own(row.Item2, "RL pickup " + row.Item1 + ": source max HP and healing, without repeating on combat entry", () =>
            {
                var s = Start(package); Set(s, Player(s), "Player", "Hp", Value.Of(20L)); Submit(s, row.Item1 + ".Obtain");
                Equal(80L + row.Item3, Field(s, "MaxHp"), "source max HP pickup"); Equal(row.Item1 == "LeesWaffle" ? 87L : 20L + row.Item3, Hp(s), "source pickup heal");
                True(!Legal(s, row.Item1 + ".Obtain"), "duplicate pickup unavailable");
                Submit(s, "RemoveOwnedRelic", Members(s, "Relics").Single()); Equal(80L + row.Item3, Field(s, "MaxHp"), "permanent max HP not revoked");
                Submit(s, "BeginDevelopmentCombat"); Equal(80L + row.Item3, Field(s, "MaxHp"), "combat does not replay pickup");
                Equal(row.Item1 == "LeesWaffle" ? 87L : 20L + row.Item3, Hp(s), "removal and combat do not revoke or repeat pickup healing");
                Equal(0L, s.Random.TotalDraws, "pickup resources consume no random");
            });
        Own("base-spire:fake-lees-waffle", "RL FakeLeesWaffle: ten percent max HP truncates and caps without raising max HP", () =>
        {
            var s = Start(package); Set(s, Player(s), "Player", "MaxHp", Value.Of(89L)); Set(s, Player(s), "Player", "Hp", Value.Of(20L));
            Submit(s, "FakeLeesWaffle.Obtain"); Equal(28L, Hp(s), "ten percent floors"); Equal(89L, Field(s, "MaxHp"), "no max HP increase");
        });
        foreach (var row in new[] { ("OldCoin", "base-spire:old-coin", 300L), ("SignetRing", "base-spire:signet-ring", 999L), ("GoldenPearl", "base-spire:golden-pearl", 150L) })
            Own(row.Item2, "RL pickup " + row.Item1 + ": source gold once, retained after removal and combat", () =>
            {
                var s = Start(package); Submit(s, row.Item1 + ".Obtain"); Equal(99L + row.Item3, Field(s, "Gold"), "source gold pickup");
                True(!Legal(s, row.Item1 + ".Obtain"), "duplicate pickup unavailable");
                Submit(s, "RemoveOwnedRelic", Members(s, "Relics").Single()); Equal(99L + row.Item3, Field(s, "Gold"), "no refund reversal");
                Submit(s, "BeginDevelopmentCombat"); Submit(s, "EndDevelopmentCombat");
                Equal(99L + row.Item3, Field(s, "Gold"), "combat does not replay gold pickup"); Equal(80L, Hp(s), "gold-only pickup never changes HP");
                Equal(0L, s.Random.TotalDraws, "gold pickups and manual end consume no random");
            });
        var simplePickups = new[] { (Name: "FakeMango", Id: "base-spire:fake-mango", Hp: 3L, Gold: 0L),
            (Name: "LoomingFruit", Id: "base-spire:looming-fruit", Hp: 31L, Gold: 0L), (Name: "NutritiousOyster", Id: "base-spire:nutritious-oyster", Hp: 11L, Gold: 0L),
            (Name: "SignetRing", Id: "base-spire:signet-ring", Hp: 0L, Gold: 999L), (Name: "GoldenPearl", Id: "base-spire:golden-pearl", Hp: 0L, Gold: 150L) };
        check("RL simple pickups: early removal or melting cancels the snapshotted max-HP and gold reward", () =>
        {
            foreach (var melt in new[] { false, true })
            {
                using var copy = new Copy(package.Root); copy.Include("cancel-resource-pickup.spire", """
                    procedure CancelPickup { parameters = { owner = Ref(Player) relic = Ref(Relic) } body = {
                      MUTATION
                    } }
                    subscription CancelPickup { event = RelicObtained reaction = CancelPickup priority = 99 }
                    """.Replace("MUTATION", melt ? "set = { target = relic field = Relic.IsMelted value = true }" : "call = { procedure = RemoveRelic args = { instance = relic } }"));
                var variant = copy.Load();
                foreach (var row in simplePickups)
                {
                    var s = Start(variant); Set(s, Player(s), "Player", "Hp", Value.Of(20L)); Submit(s, row.Name + ".Obtain");
                    Equal(80L, Field(s, "MaxHp"), "invalidated pickup cannot raise max HP"); Equal(20L, Hp(s), "invalidated pickup cannot heal"); Equal(99L, Field(s, "Gold"), "invalidated pickup cannot gain gold");
                    Equal(melt ? 1 : 0, Members(s, "Relics").Count, "ownership follows the actual early mutation");
                    Equal(!melt, Legal(s, row.Name + ".Obtain"), "melted ownership still blocks duplicate development acquisition");
                    Equal(0L, s.Random.TotalDraws, "invalidated pickup consumes no random");
                }
            }
        });
        check("RL simple pickups: direct acquisitions reward only the actual new instance and holder; other holders do not block development acquisition", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("resource-pickup-owners.spire", string.Join("\n", simplePickups.Select(row => $$"""
                action Other{{row.Name}} { role = player locals = { other = Binding } body = {
                  create = { type = Player scope = frameScope() into = other fields = { Owner = participant(2) } }
                  call = { procedure = ObtainRelic args = { owner = other definition = "{{row.Id}}" } }
                } }
                action Direct{{row.Name}} { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "{{row.Id}}" } } } }
                """)));
            var variant = copy.Load();
            foreach (var row in simplePickups)
            {
                var s = variant.CreateSession(20261018, ["player", "player"]); s.Start(); Healthy(s); Submit(s, "Other" + row.Name);
                var other = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Player").Id).Last();
                Equal(80L, Field(s, "MaxHp"), "other holder cannot affect first player"); Equal(99L, Field(s, "Gold"), "other holder's gold isolated");
                True(Legal(s, row.Name + ".Obtain"), "other holder's matching definition cannot block own action");
                Submit(s, row.Name + ".Obtain"); Submit(s, "Direct" + row.Name);
                Equal(80L + 2 * row.Hp, Field(s, "MaxHp"), "only the two new own instances reward"); Equal(80L + 2 * row.Hp, Hp(s), "each max HP pickup heals only its amount");
                Equal(99L + 2 * row.Gold, Field(s, "Gold"), "old matching relic cannot replay its pickup");
                Equal(80L + row.Hp, IntField(s, other, "Player", "MaxHp"), "other max HP unchanged after own pickups"); Equal(99L + row.Gold, IntField(s, other, "Player", "Gold"), "other gold unchanged after own pickups");
                foreach (var relic in Members(s, "Relics").Where(r => s.State.GetField(r, s.RuleSet.TypeByQualifiedId("base-spire:Relic").FieldIndex("Holder")).AsRef() == Player(s)).ToArray()) Submit(s, "RemoveOwnedRelic", relic);
                Submit(s, row.Name + ".Obtain"); Equal(80L + 3 * row.Hp, Field(s, "MaxHp"), "new acquisition after removal rewards once"); Equal(99L + 3 * row.Gold, Field(s, "Gold"), "permanent gold retained and granted once again");
                Equal(0L, s.Random.TotalDraws, "no cosmetic or gameplay random consumption");
            }
        });
        check("RL acquisition lifecycle: direct shared ObtainRelic runs pickup once for Strawberry and Pear", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("direct-pickup.spire", """
                action DirectPickup { role = player body = {
                  call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:strawberry" } }
                  call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:pear" } }
                } }
                """);
            var s = Start(copy.Load()); Submit(s, "DirectPickup"); Equal(97L, Field(s, "MaxHp"), "both pickups subscribe to real acquisition"); Equal(97L, Hp(s), "no extra action-side gain");
        });
        Own("base-spire:black-blood", "RL BlackBlood: twelve healing after victory, no manual-end healing or defeat revival", () =>
        {
            var s = Combat("BlackBlood"); Set(s, Player(s), "Player", "Hp", Value.Of(20L)); Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1L));
            Plays(s, "StrikeIronclad", 1); Equal("victory", Outcome(s), "actual final hit"); Equal(32L, Hp(s), "source twelve healing");
            var manual = Combat("BlackBlood"); Set(manual, Player(manual), "Player", "Hp", Value.Of(20L)); Submit(manual, "EndDevelopmentCombat"); Equal(20L, Hp(manual), "manual end is not victory");
            var dead = Combat("BlackBlood"); Set(dead, Player(dead), "Player", "Hp", Value.Of(2L)); Plays(dead, "Bloodletting", 1); Equal(0L, Hp(dead), "no defeat revival");
        });
        Own("base-spire:captains-wheel", "RL CaptainsWheel: eighteen block only after the third-turn clear, resets next combat", () =>
        {
            var s = Combat("CaptainsWheel"); Equal(0L, Block(s), "no turn one block"); IcNextTurn(s); Equal(0L, Block(s), "no turn two block");
            Set(s, Player(s), "Player", "Block", Value.Of(5L)); IcNextTurn(s); Equal(18L, Block(s), "clear before source eighteen"); IcNextTurn(s); Equal(0L, Block(s), "not repeated on four");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(0L, Block(s), "new combat starts at one");
        });
        Own("base-spire:bag-of-preparation", "RL BagOfPreparation: seven-card first hand, Innate max rather than additive draw, removed and melted inactive", () =>
        {
            foreach (var mode in new[] { "normal", "innate", "removed", "melted", "blocked" })
            {
                var s = Start(package); Submit(s, "Ironclad.Initialize"); Submit(s, "BagOfPreparation.Obtain"); var relic = Relic(s, "base-spire:bag-of-preparation");
                if (mode == "removed") Submit(s, "RemoveOwnedRelic", relic);
                if (mode == "melted") Set(s, relic, "Relic", "IsMelted", Value.Of(true));
                if (mode == "innate") foreach (var c in Members(s, "RunDeck").Take(8)) Set(s, c, "Card", "Innate", Value.Of(true));
                Submit(s, "BeginDevelopmentCombat"); if (mode == "blocked") Set(s, Player(s), "Player", "DrawBlocked", Value.Of(true));
                Submit(s, "BeginDevelopmentTurn"); Equal(mode == "normal" ? 7 : mode == "innate" ? 8 : mode == "blocked" ? 0 : 5, Hand(s).Count, mode + " initial hand");
                if (mode == "normal") { IcNextTurn(s); Equal(5, Hand(s).Count, "turn two uses base five"); }
            }
        });
        Own("base-spire:regal-pillow", "RL RegalPillow: rest adds fifteen to normal heal, caps and ignores removed or melted relic", () =>
        {
            foreach (var mode in new[] { "normal", "cap", "removed", "melted" })
            {
                var s = Start(package); Submit(s, "RegalPillow.Obtain"); var relic = Members(s, "Relics").Single();
                if (mode == "removed") Submit(s, "RemoveOwnedRelic", relic); if (mode == "melted") Set(s, relic, "Relic", "IsMelted", Value.Of(true));
                Set(s, Player(s), "Player", "Hp", Value.Of(mode == "cap" ? 70L : 20L)); Submit(s, "EnterDevelopmentRestSite"); Submit(s, "RestAtDevelopmentCampfire");
                Equal(mode == "cap" ? 80L : mode == "normal" ? 59L : 44L, Hp(s), mode + " rest result"); True(!Legal(s, "RestAtDevelopmentCampfire"), "one successful visit option");
            }
        });
        foreach (var row in new[] { ("Kunai", "base-spire:kunai", "base-spire:dexterity-power"), ("Shuriken", "base-spire:shuriken", "base-spire:strength-power"), ("OrnamentalFan", "base-spire:ornamental-fan", "block") })
            Own(row.Item2, "RL " + row.Item1 + ": every three completed attacks, multi-hit once, six twice and early turn reset", () =>
            {
                var s = Combat(row.Item1); Plays(s, "TwinStrike", 1); Equal(1L, Counter(s), "two hits are one play"); Plays(s, "StrikeIronclad", 1);
                Equal(0L, row.Item3 == "block" ? Block(s) : IcPower(s, row.Item3), "no reward after two"); Plays(s, "StrikeIronclad", 1);
                Equal(row.Item3 == "block" ? 4L : 1L, row.Item3 == "block" ? Block(s) : IcPower(s, row.Item3), "third play source reward");
                Plays(s, "StrikeIronclad", 3); Equal(row.Item3 == "block" ? 8L : 2L, row.Item3 == "block" ? Block(s) : IcPower(s, row.Item3), "six plays reward twice");
                Plays(s, "StrikeIronclad", 2); Equal(2L, Counter(s), "remainder before next turn"); IcNextTurn(s); Equal(0L, Counter(s), "counter resets before next turn starts");
                Set(s, Player(s), "Player", "Energy", Value.Of(100L)); Plays(s, "StrikeIronclad", 1); Equal(1L, Counter(s), "new turn starts from zero");
            });
        Own("base-spire:nunchaku", "RL Nunchaku: tenth attack gains one energy and counter persists across turns and combats", () =>
        {
            var s = Combat("Nunchaku"); Plays(s, "StrikeIronclad", 9); Equal(9L, Counter(s), "nine attacks saved"); IcNextTurn(s); Equal(9L, Counter(s), "survives next turn");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Equal(9L, Counter(s), "survives next combat");
            Plays(s, "StrikeIronclad", 1); Equal(3L, Energy(s), "spent one then gained one"); Equal(0L, Counter(s), "counter wraps");
        });
        Own("base-spire:letter-opener", "RL LetterOpener: third skill hits every enemy unpowered and resets per turn", () =>
        {
            var s = Combat("LetterOpener"); Submit(s, "AddDevelopmentTarget"); Plays(s, "DefendIronclad", 2); Equal(1000L, EnemyHp(s), "two skills do not trigger");
            Plays(s, "DefendIronclad", 1); Equal(995L, EnemyHp(s), "source five unpowered damage");
            Equal(35L, IntField(s, Targets(s).Last(), "Enemy", "Hp"), "every enemy hit"); Plays(s, "DefendIronclad", 2); IcNextTurn(s);
            Set(s, Player(s), "Player", "Energy", Value.Of(100L)); Plays(s, "DefendIronclad", 1); Equal(995L, EnemyHp(s), "new turn does not reuse previous remainder");
        });
        Own("base-spire:stone-calendar", "RL StoneCalendar: seventh turn end deals fifty-two to all and lethal skips enemy phase", () =>
        {
            var s = Combat("StoneCalendar"); for (var i = 1; i < 7; i++) { Submit(s, "EndDevelopmentTurn"); Equal(1000L, EnemyHp(s), "not before seventh"); Submit(s, "BeginDevelopmentTurn"); }
            Submit(s, "EndDevelopmentTurn"); Equal(948L, EnemyHp(s), "source 52 damage on turn seven"); Submit(s, "BeginDevelopmentTurn"); Submit(s, "EndDevelopmentTurn"); Equal(948L, EnemyHp(s), "not repeated on eight");
            var lethal = Start(package); Submit(lethal, "StoneCalendar.Obtain"); Submit(lethal, "TwigSlimeS.Spawn"); Submit(lethal, "TwigSlimeS.Spawn"); Submit(lethal, "BeginDevelopmentCombat");
            Set(lethal, Player(lethal), "Player", "TurnNumber", Value.Of(6L)); Submit(lethal, "BeginDevelopmentTurn"); Submit(lethal, "EndDevelopmentTurn");
            True(Targets(lethal).All(e => IntField(lethal, e, "Enemy", "Hp") == 0), "all enemy instances killed"); Equal("victory", Outcome(lethal), "victory after source group damage"); Equal(80L, Hp(lethal), "no enemy attack after final kill");
        });
        Own("base-spire:centennial-puzzle", "RL CentennialPuzzle: first real HP loss draws three once, all-blocked ignored and next combat resets", () =>
        {
            var s = Combat("CentennialPuzzle"); Set(s, Player(s), "Player", "Block", Value.Of(4L));
            using var copy = new Copy(package.Root); copy.Include("damage-probes.spire", """
                action BlockedHit { role = player body = { call = { procedure = DamagePlayer args = { owner = first(type(Player)) amount = 4 } } } }
                """);
            var blocked = Start(copy.Load()); Submit(blocked, "CentennialPuzzle.Obtain"); Submit(blocked, "BeginDevelopmentCombat"); Set(blocked, Player(blocked), "Player", "Block", Value.Of(4L)); Submit(blocked, "BlockedHit");
            Equal(0, Hand(blocked).Count, "fully blocked hit does not draw"); True(!blocked.State.GetField(Members(blocked, "Relics").Single(), blocked.RuleSet.TypeByQualifiedId("base-spire:Relic").FieldIndex("UsedThisCombat")).AsBool(), "blocked hit does not spend trigger");
            Plays(s, "Bloodletting", 1); Equal(3, Hand(s).Count, "draws three on positive card HP loss");
            Plays(s, "Bloodletting", 1); Equal(3, Hand(s).Count, "only first loss triggers"); Submit(s, "DiscardHand");
            Submit(s, "EndDevelopmentCombat"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn"); Plays(s, "Bloodletting", 1); Equal(3, Hand(s).Count, "next combat reactivates");
        });
        check("RL counter consumers: melted or removed relics stop triggers and repeated plays count separately", () =>
        {
            foreach (var melted in new[] { false, true })
            {
                var s = Combat("Shuriken"); Plays(s, "StrikeIronclad", 2); var r = Members(s, "Relics").Single();
                if (melted) Set(s, r, "Relic", "IsMelted", Value.Of(true)); else Submit(s, "RemoveOwnedRelic", r);
                Plays(s, "StrikeIronclad", 1); Equal(0L, IcPower(s, "base-spire:strength-power"), "inactive third-play trigger");
            }
            var repeat = Combat("Kunai"); Plays(repeat, "StrikeIronclad", 1); Plays(repeat, "OneTwoPunch", 1); Plays(repeat, "StrikeIronclad", 1);
            Equal(1L, IcPower(repeat, "base-spire:dexterity-power"), "repeat contributes two real completed plays");
        });
        check("RL automatic attacks and final kills complete relic callbacks before victory", () =>
        {
            var s = Combat("Shuriken"); Plays(s, "StrikeIronclad", 2);
            var top = IcAdd(s, "StrikeIronclad"); IcMove(s, top, "Hand", "DrawPile");
            IcPlay(s, IcAdd(s, "Havoc")); Equal(1L, IcPower(s, "base-spire:strength-power"), "automatic child attack counts");
            var lethal = Combat("OrnamentalFan"); Plays(lethal, "StrikeIronclad", 2); Set(lethal, FirstTarget(lethal), "Enemy", "Hp", Value.Of(1L));
            Plays(lethal, "StrikeIronclad", 1); Equal("victory", Outcome(lethal), "third attack really killed"); Equal(4L, Block(lethal), "post-play block completed before victory");
        });
        check("RL counter instances isolate holders and newly acquired copies, while melted ownership still blocks duplicates", () =>
        {
            using var copy = new Copy(package.Root);
            copy.Include("relic-owner-probe.spire", """
                action OtherHolder { role = player locals = { owner = Binding } body = {
                  create = { type = Player scope = frameScope() into = owner fields = { Owner = participant(2) } }
                  call = { procedure = ObtainRelic args = { owner = owner definition = "base-spire:shuriken" } }
                } }
                action ExtraCopy { role = player body = { call = { procedure = ObtainRelic args = { owner = first(type(Player)) definition = "base-spire:shuriken" } } } }
                """);
            var s = Start(copy.Load()); Submit(s, "Shuriken.Obtain"); Submit(s, "OtherHolder"); Submit(s, "AddDevelopmentTarget"); Submit(s, "BeginDevelopmentCombat"); Submit(s, "BeginDevelopmentTurn");
            Set(s, FirstTarget(s), "Enemy", "Hp", Value.Of(1000L)); Set(s, Player(s), "Player", "Energy", Value.Of(100L));
            Plays(s, "StrikeIronclad", 2); Submit(s, "ExtraCopy"); Plays(s, "StrikeIronclad", 1);
            var rs = Members(s, "Relics").ToArray();
            Equal(0L, IntField(s, rs[0], "Relic", "Counter"), "original instance reached three");
            Equal(0L, IntField(s, rs[1], "Relic", "Counter"), "other holder did not advance");
            Equal(1L, IntField(s, rs[2], "Relic", "Counter"), "new same-definition instance starts independently");
            Equal(1L, IcPower(s, "base-spire:strength-power"), "only original holder instance activated");
            var melted = Start(package); Submit(melted, "OldCoin.Obtain"); Set(melted, Members(melted, "Relics").Single(), "Relic", "IsMelted", Value.Of(true));
            True(!Legal(melted, "OldCoin.Obtain"), "melting cannot reopen pickup to duplicate gold");
        });
        return witnesses;
    }
}
