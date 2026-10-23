using CoreTheSpire.Engine;

internal static partial class PackageChecks
{
    private static void RunUnknownRoomChecks(DslPackage package, Action<string, Action> check)
    {
        using var copy = new Copy(package.Root);
        copy.Include("unknown-probes.spire", """
            entity UnknownAudit { fields = { Last = { type = String(length = 16) default = "none" } Mode = { type = String(length = 16) default = "none" } } }
            action InitUnknown { role = player locals = { audit = Binding } body = {
              create = { type = UnknownAudit scope = frameScope() into = audit }
              call = { procedure = InitializeUnknownRoomOdds args = { run = first(type(Run)) } }
            } }
            action RollUnknown { role = player locals = { room = String(length = 16) } body = {
              call = { procedure = RollUnknownRoom args = { run = first(type(Run)) excludeShop = equal(field(first(type(UnknownAudit)), UnknownAudit.Mode), "exclude-shop") } into = room }
              set = { target = first(type(UnknownAudit)) field = UnknownAudit.Last value = room }
            } }
            action ResetUnknown { role = player body = { call = { procedure = ResetUnknownRoomOdds args = { run = first(type(Run)) } } } }
            procedure TestAllowed { pure = true parameters = { value = Bool run = Ref(Run) kind = String(length = 16) } result = Bool body = {
              if = { condition = equal(field(first(type(UnknownAudit)), UnknownAudit.Mode), "no-types") then = { return = { value = false } } }
              if = { condition = equal(field(first(type(UnknownAudit)), UnknownAudit.Mode), "event-only") then = { return = { value = equal(kind, "event") } } }
              if = { condition = equal(field(first(type(UnknownAudit)), UnknownAudit.Mode), "no-event") then = { return = { value = and(value, not(equal(kind, "event"))) } } }
              return = { value = value }
            } }
            subscription TestAllowed { event = ModifyUnknownRoomAllowed reaction = TestAllowed }
            procedure TestIncrease { pure = true parameters = { value = Int run = Ref(Run) kind = String(length = 16) } result = Int body = {
              if = { condition = equal(field(first(type(UnknownAudit)), UnknownAudit.Mode), "double") then = { return = { value = float32Add(value, value) } } }
              return = { value = value }
            } }
            subscription TestIncrease { event = ModifyUnknownRoomOddsIncrease reaction = TestIncrease }
            action Boundaries { role = player locals = { run = Binding threshold = Int } body = {
              let = { into = run value = first(type(Run)) }
              if = { condition = not(equal(pureCall(SelectUnknownRoomForRoll, run, false, float32(0.1)), "monster")) then = { fail = { message = "inclusive source first boundary" } } }
              let = { into = threshold value = float32Add(float32(0.1), float32(0.02)) }
              if = { condition = not(equal(pureCall(SelectUnknownRoomForRoll, run, false, threshold), "treasure")) then = { fail = { message = "negative elite is skipped" } } }
              let = { into = threshold value = float32Add(threshold, float32(0.03)) }
              if = { condition = not(equal(pureCall(SelectUnknownRoomForRoll, run, false, threshold), "shop")) then = { fail = { message = "inclusive source shop boundary" } } }
              if = { condition = not(equal(pureCall(SelectUnknownRoomForRoll, run, false, float32(0.9)), "event")) then = { fail = { message = "default event remainder" } } }
              if = { condition = not(equal(pureCall(SelectUnknownRoomForRoll, run, true, threshold), "event")) then = { fail = { message = "blacklist is not renormalized" } } }
            } }
            action CheckShopBlacklist { role = player locals = { run = Binding node = Binding child = Binding } body = {
              let = { into = run value = first(type(Run)) }
              create = { type = MapNode scope = entityScope(run) into = node fields = { Run = run Index = 0 Col = 0 Row = 0 Type = enum(MapPointType, Unknown) } }
              if = { condition = pureCall(UnknownShopIsExcluded, run, node) then = { fail = { message = "empty children are not all shops" } } }
              create = { type = MapNode scope = entityScope(run) into = child fields = { Run = run Index = 1 Col = 0 Row = 1 Type = enum(MapPointType, Shop) } }
              addRelation = { relation = MapChildren owner = node target = child }
              if = { condition = not(pureCall(UnknownShopIsExcluded, run, node)) then = { fail = { message = "all nonempty children shop" } } }
              set = { target = child field = MapNode.Type value = enum(MapPointType, Monster) }
              if = { condition = pureCall(UnknownShopIsExcluded, run, node) then = { fail = { message = "a non-shop successor permits shop" } } }
              set = { target = run field = Run.CurrentPointHasShop value = true }
              if = { condition = not(pureCall(UnknownShopIsExcluded, run, node)) then = { fail = { message = "previous point contained a real shop" } } }
            } }
            action SetupForeignChild { role = player locals = { run = Binding foreign = Binding current = Binding node = Binding } body = {
              let = { into = run value = first(type(Run)) }
              create = { type = Run scope = frameScope() into = foreign fields = { Player = first(type(Player)) } }
              create = { type = MapNode scope = entityScope(run) into = current fields = { Run = run Index = 0 Col = 0 Row = 0 Type = enum(MapPointType, Monster) } }
              create = { type = MapNode scope = entityScope(foreign) into = node collection = MapNodes fields = { Run = foreign Index = 1 Col = 0 Row = 1 Type = enum(MapPointType, Unknown) Selectable = true } }
              addRelation = { relation = MapChildren owner = current target = node }
              set = { target = run field = Run.CurrentNode value = current }
              set = { target = run field = Run.Phase value = "map" }
              set = { target = first(type(Player)) field = Player.CurrentRun value = run }
            } }
            action EnterForeignChild { role = player body = {
              call = { procedure = EnterNormalMapPoint args = { owner = first(type(Player)) run = field(first(type(Player)), Player.CurrentRun) node = first(collection(MapNodes)) } }
            } }
            action MakeOwnChild { role = player body = {
              set = { target = first(collection(MapNodes)) field = MapNode.Run value = field(first(type(Player)), Player.CurrentRun) }
            } }
            action SetupUnknownEventQueue { role = player locals = { definition = Binding } body = {
              call = { procedure = AddOpeningDefinition args = { run = first(type(Run)) category = "event" act = 0 definition = "base-spire:dense-vegetation" room = "none" rarity = "none" weak = false } into = definition }
              call = { procedure = AddOpeningRoomPlan args = { run = first(type(Run)) act = 0 room = "event" definition = definition } }
            } }
            action SetupUnsupportedUnknownEventQueue { role = player locals = { definition = Binding } body = {
              call = { procedure = AddOpeningDefinition args = { run = first(type(Run)) category = "event" act = 0 definition = "base-spire:aroma-of-chaos" room = "none" rarity = "none" weak = false } into = definition }
              call = { procedure = AddOpeningRoomPlan args = { run = first(type(Run)) act = 0 room = "event" definition = definition } }
            } }
            """);
        var rules = copy.Load();
        Session New(string mode = "none")
        {
            var s = Start(rules); Submit(s, "InitUnknown"); Set(s, Audit(s), "UnknownAudit", "Mode", Value.Of(mode)); return s;
        }
        InstanceId Audit(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:UnknownAudit").Id).Single();
        InstanceId[] Odds(Session s) => Members(s, "UnknownRoomOddsOrder").ToArray();
        const string stream = "base-spire:run:unknown_map_point";
        check("UNKNOWN exact binary32 thresholds, negative odds and non-renormalized blacklist", () =>
        {
            var s = New(); Submit(s, "Boundaries"); Equal(0L, s.Random.DrawCount(stream), "pure source classification does not draw");
            Submit(s, "CheckShopBlacklist"); Equal(0L, s.Random.DrawCount(stream), "pure map filters do not draw");
        });
        check("UNKNOWN event-only still consumes once and leaves all excluded odds unchanged", () =>
        {
            var s = New("event-only"); var before = Odds(s).Select(o => IntField(s, o, "UnknownRoomOdds", "Current")).ToArray();
            Submit(s, "RollUnknown"); Equal("event", MoveText(s, Audit(s), "UnknownAudit", "Last"), "real forced-event selection");
            True(before.SequenceEqual(Odds(s).Select(o => IntField(s, o, "UnknownRoomOdds", "Current"))), "excluded odds are untouched");
            Equal(1L, s.Random.DrawCount(stream), "even only event consumes a real source draw");
        });
        check("UNKNOWN selected resets to base; allowed unselected odds use the actual increase and excluded shop freezes", () =>
        {
            foreach (var mode in new[] { "none", "exclude-shop", "double" })
            {
                var s = New(mode); var current = Odds(s).Select(o => IntField(s, o, "UnknownRoomOdds", "Current")).ToArray();
                for (var n = 1; n <= 12; n++)
                {
                    Submit(s, "RollUnknown"); var result = MoveText(s, Audit(s), "UnknownAudit", "Last");
                    for (var i = 0; i < 4; i++)
                    {
                        var kind = MoveText(s, Odds(s)[i], "UnknownRoomOdds", "Kind");
                        var baseBits = IntField(s, Odds(s)[i], "UnknownRoomOdds", "Base");
                        var baseValue = BitConverter.UInt32BitsToSingle((uint)baseBits);
                        var expected = result == kind ? baseBits : mode == "exclude-shop" && kind == "shop" ? current[i]
                            : BitConverter.SingleToUInt32Bits(BitConverter.UInt32BitsToSingle((uint)current[i]) + (mode == "double" ? baseValue + baseValue : baseValue));
                        Equal(expected, IntField(s, Odds(s)[i], "UnknownRoomOdds", "Current"), "source update after real roll"); current[i] = expected;
                    }
                    Equal((long)n, s.Random.DrawCount(stream), "one draw per selection");
                }
                Submit(s, "ResetUnknown");
                foreach (var o in Odds(s)) Equal(IntField(s, o, "UnknownRoomOdds", "Base"), IntField(s, o, "UnknownRoomOdds", "Current"), "act reset restores configured base");
                Equal(12L, s.Random.DrawCount(stream), "reset does not reseed or draw");
                var hash = s.State.StateHash(); var draws = s.Random.TotalDraws;
                s.Observe(s.Participant(0), "base-spire:PlayerView"); s.LegalActions(s.Participant(0));
                Equal(hash, s.State.StateHash(), "observation keeps odds unchanged"); Equal(draws, s.Random.TotalDraws, "observation does not draw");
            }
        });
        check("UNKNOWN no-event fallback follows source type order; empty allowed set fails before random", () =>
        {
            var s = New("no-event"); foreach (var o in Odds(s)) Set(s, o, "UnknownRoomOdds", "Current", Value.Of((long)BitConverter.SingleToUInt32Bits(-1f)));
            Submit(s, "RollUnknown"); Equal("monster", MoveText(s, Audit(s), "UnknownAudit", "Last"), "source lowest enum fallback");
            var empty = New("no-types"); var hash = empty.State.StateHash();
            Throws<EngineFaultException>(() => Submit(empty, "RollUnknown")); Equal(hash, empty.State.StateHash(), "empty set fails before odds mutation");
            Equal(0L, empty.Random.DrawCount(stream), "empty set does not consume");
        });
        check("UNKNOWN foreign Run child is rejected before classification even with a forged graph edge", () =>
        {
            var s = New(); Submit(s, "SetupForeignChild"); var hash = s.State.StateHash();
            var random = System.Text.Json.JsonSerializer.Serialize(s.Random.Capture());
            Throws<EngineFaultException>(() => Submit(s, "EnterForeignChild"));
            True(s.Fault!.Message.Contains("another Run", StringComparison.Ordinal), "ownership guard precedes unknown roll");
            Equal(hash, s.State.StateHash(), "foreign node cannot create a visit or change odds");
            Equal(random, System.Text.Json.JsonSerializer.Serialize(s.Random.Capture()), "foreign node consumes no random stream");
        });
        check("UNKNOWN unsupported actual Event fails by name after one real classification draw", () =>
        {
            var s = New("event-only"); Submit(s, "SetupForeignChild"); Submit(s, "MakeOwnChild"); Submit(s, "SetupUnsupportedUnknownEventQueue");
            var odds = Odds(s).Select(o => IntField(s, o, "UnknownRoomOdds", "Current")).ToArray();
            Throws<EngineFaultException>(() => Submit(s, "EnterForeignChild"));
            True(s.Fault!.Message.Contains("source normal Event effect", StringComparison.Ordinal), "unsupported actual effect is explicit");
            Equal(1L, s.Random.DrawCount(stream), "unsupported actual room does not compensate or retry its draw");
            True(odds.SequenceEqual(Odds(s).Select(o => IntField(s, o, "UnknownRoomOdds", "Current"))), "event-only leaves excluded odds unchanged");
            Equal(0, Members(s, "RunVisits").Count, "unsupported room creates no empty visit");
        });
        check("UNKNOWN supported DenseVegetation creates its actual event visit after one classification draw", () =>
        {
            var s = New("event-only"); Submit(s, "SetupForeignChild"); Submit(s, "MakeOwnChild"); Submit(s, "SetupUnknownEventQueue");
            Submit(s, "EnterForeignChild"); Healthy(s);
            Equal(1L, s.Random.DrawCount(stream), "supported event keeps one classification draw");
            Equal(1, Members(s, "RunVisits").Count, "supported event creates one actual visit");
            Equal("base-spire:dense-vegetation", TextField(s, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventVisit").Id).Single(), "EventVisit", "DefinitionId"), "selected effect is entered");
            True(s.PendingDecision is not null, "real event suspends at its branch decision");
        });
    }
}
