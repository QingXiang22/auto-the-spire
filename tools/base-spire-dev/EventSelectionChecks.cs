using CoreTheSpire.Engine;
using System.Text.Json;

internal static partial class PackageChecks
{
    private static void RunEventSelectionChecks(DslPackage package, Action<string, Action> check)
    {
        using var copy = new Copy(package.Root);
        copy.Include("event-selection-probes.spire", """
            entity EventSelectionAudit { fields = { Last = { type = String(length = 64) default = "none" } Override = { type = Bool default = false } } }
            action InitEventSelection { role = player locals = { audit = Binding } body = {
              create = { type = EventSelectionAudit scope = frameScope() into = audit }
            } }
            action AppendEvent { role = player parameters = { id = String(length = 64) } locals = { definition = Binding } body = {
              call = { procedure = AddOpeningDefinition args = { run = first(type(Run)) category = "event" act = 0 definition = id room = "none" rarity = "none" weak = false } into = definition }
              call = { procedure = AddOpeningRoomPlan args = { run = first(type(Run)) act = 0 room = "event" definition = definition } }
            } }
            action PullEvent { role = player locals = { selected = String(length = 64) } body = {
              call = { procedure = PullNextNormalEvent args = { run = first(type(Run)) } into = selected }
              set = { target = first(type(EventSelectionAudit)) field = EventSelectionAudit.Last value = selected }
            } }
            action QueryBridge { role = player requires = pureCall(NormalEventIsAllowed, first(type(Run)), "base-spire:slippery-bridge") body = {} }
            action QueryMorphic { role = player requires = pureCall(NormalEventIsAllowed, first(type(Run)), "base-spire:morphic-grove") body = {} }
            action QueryLegends { role = player requires = pureCall(NormalEventIsAllowed, first(type(Run)), "base-spire:the-legends-were-true") body = {} }
            procedure OverrideEvent { pure = true parameters = { value = String(length = 64) run = Ref(Run) } result = String(length = 64) body = {
              if = { condition = field(first(type(EventSelectionAudit)), EventSelectionAudit.Override) then = { return = { value = "base-spire:dense-vegetation" } } }
              return = { value = value }
            } }
            subscription OverrideEvent { event = ModifyNextNormalEvent reaction = OverrideEvent }
            """);
        var rules = copy.Load();
        Session New(params string[] queue)
        {
            var s = Start(rules); Submit(s, "InitEventSelection");
            foreach (var name in queue)
                s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "base-spire:AppendEvent"), s.Participant(0), [Value.Of("base-spire:" + name)]);
            Healthy(s); return s;
        }
        InstanceId Run(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:Run").Id).Single();
        InstanceId Audit(Session s) => s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:EventSelectionAudit").Id).Single();
        long Cursor(Session s) => IntField(s, Run(s), "Run", "NormalEventIndex");
        string Selected(Session s) => TextField(s, Audit(s), "EventSelectionAudit", "Last");
        check("EVENT source scan skips permanent and temporary gates, prefers unvisited and falls back without RNG", () =>
        {
            var s = New("war-historian-repy", "the-future-of-potions", "aroma-of-chaos", "sapphire-seed");
            var random = JsonSerializer.Serialize(s.Random.Capture());
            Submit(s, "PullEvent"); Equal("base-spire:aroma-of-chaos", Selected(s), "first eligible unvisited source event"); Equal(3L, Cursor(s), "both rejected slots still advance the scan");
            Submit(s, "PullEvent"); Equal("base-spire:sapphire-seed", Selected(s), "unvisited slot wins"); Equal(4L, Cursor(s), "wrap starts at the next source index");
            Submit(s, "PullEvent"); Equal("base-spire:the-future-of-potions", Selected(s), "exhausted source fallback permits temporarily disallowed events"); Equal(10L, Cursor(s), "full rejected pass then permanent-only fallback");
            Equal(3, s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:NormalEventHistory").Id).Count, "unique final accepted IDs");
            Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "event selection consumes no random word");
        });
        check("EVENT pure next-event modification records the final identity and isolates sessions", () =>
        {
            var s = New("aroma-of-chaos"); Set(s, Audit(s), "EventSelectionAudit", "Override", Value.Of(true));
            Submit(s, "PullEvent"); Equal("base-spire:dense-vegetation", Selected(s), "final modified event");
            var history = s.State.AllInstances(s.RuleSet.TypeByQualifiedId("base-spire:NormalEventHistory").Id).Single();
            Equal("base-spire:dense-vegetation", TextField(s, history, "NormalEventHistory", "DefinitionId"), "original candidate is not recorded instead of modified event");
            var other = New("aroma-of-chaos"); Submit(other, "PullEvent"); Equal("base-spire:aroma-of-chaos", Selected(other), "other session retains its own cursor, hook and history");
        });
        check("EVENT empty and permanently forbidden pools retain exact source failures", () =>
        {
            var empty = New(); var hash = empty.State.StateHash();
            Throws<EngineFaultException>(() => Submit(empty, "PullEvent")); Equal(hash, empty.State.StateHash(), "empty source pool fails before mutation");
            var permanent = New("war-historian-repy"); var random = JsonSerializer.Serialize(permanent.Random.Capture());
            Throws<EngineFaultException>(() => Submit(permanent, "PullEvent")); Equal(2L, Cursor(permanent), "both bounded source passes reject permanent gate");
            Equal(random, JsonSerializer.Serialize(permanent.Random.Capture()), "no-event failure does not draw");
        });
        check("EVENT source eligibility uses live resources and pre-entry floor; queries are read-only", () =>
        {
            var s = New(); Submit(s, "Ironclad.Initialize");
            Set(s, Run(s), "Run", "TotalFloor", Value.Of(6)); True(!Legal(s, "QueryBridge"), "bridge excludes floor six");
            Set(s, Run(s), "Run", "TotalFloor", Value.Of(7)); True(Legal(s, "QueryBridge"), "bridge permits floor seven with a removable deck");
            Set(s, Player(s), "Player", "Gold", Value.Of(99)); True(!Legal(s, "QueryMorphic"), "morphic checks the source gold threshold");
            Set(s, Player(s), "Player", "Gold", Value.Of(100)); True(Legal(s, "QueryMorphic"), "morphic reads the real permanent deck");
            Set(s, Player(s), "Player", "Hp", Value.Of(9)); True(!Legal(s, "QueryLegends"), "legends excludes HP nine");
            Set(s, Player(s), "Player", "Hp", Value.Of(10)); True(Legal(s, "QueryLegends"), "legends includes HP ten");
            var hash = s.State.StateHash(); var random = JsonSerializer.Serialize(s.Random.Capture());
            s.LegalActions(s.Participant(0)); s.Observe(s.Participant(0), "base-spire:PlayerView");
            Equal(hash, s.State.StateHash(), "queries do not mutate selection state"); Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "queries do not draw");
        });
        check("EVENT unimplemented act/profile fails before selection or RNG", () =>
        {
            var s = New("aroma-of-chaos"); Set(s, Run(s), "Run", "ActIndex", Value.Of(1)); var hash = s.State.StateHash();
            var random = JsonSerializer.Serialize(s.Random.Capture());
            Throws<EngineFaultException>(() => Submit(s, "PullEvent")); Equal(hash, s.State.StateHash(), "later-act input is explicit, not assumed eligible");
            Equal(random, JsonSerializer.Serialize(s.Random.Capture()), "unsupported act does not draw");
        });
    }
}
