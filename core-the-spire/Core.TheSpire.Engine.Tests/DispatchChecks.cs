using CoreTheSpire;
using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class DispatchChecks
{
    private static RuleSet Rules(string source) => new RuleSetBuilder().Add(EngineDslCompiler.Compile(
        "using namespace dispatches; dsl_version = \"core/0.3\" " + source, "dispatch-check.spire")).Build();

    public static void Run()
    {
        Check.Run("PROJECTION dependent components inherit hidden anchor identity and do not mutate observation", () =>
        {
            var rules = Rules("""
                entity Card { fields = { Name = { type = String(length = 16) default = "same" } } }
                entity Component { fields = { Card = { type = Ref(Card) } Value = { type = Int default = 7 } } }
                collection Hidden { element = Card identity_visible = false }
                projection View { role = player types = { Component = { identity_from = Card } Card = {} } }
                procedure Setup { locals = { card = Binding component = Binding } body = {
                  create = { type = Card scope = frameScope() into = card collection = Hidden }
                  create = { type = Component scope = frameScope() into = component fields = { Card = card } }
                } }
                start = [Setup]
                action Show { role = player body = { removeFromCollection = { collection = Hidden item = first(collection(Hidden)) } } }
                """);
            var s = new Session(rules, 1, ["player"]); s.Start(); var before = s.State.StateHash();
            Check.Equal(0, s.Observe(s.Participant(0), "dispatches:View").Instances.Count, "the attachment cannot disclose its hidden card");
            Check.Equal(before, s.State.StateHash(), "observing the dependency policy is read only");
            s.Submit(rules.Actions.Single(), s.Participant(0), []);
            Check.Equal(2, s.Observe(s.Participant(0), "dispatches:View").Instances.Count, "identifying the anchor exposes its component regardless of declaration order");
            Check.Throws<EngineFaultException>(() => Rules("""
                entity Component { fields = { Number = { type = Int } } }
                projection View { role = player types = { Component = { identity_from = Number } } }
                """), "identity anchors must be references");
        });
        Check.Run("BINDING equal absent optional references can participate in pure dispatch policies", () =>
        {
            var rules = Rules("""
                entity Result { fields = { Same = { type = Bool default = false } } }
                procedure Same { pure = true parameters = { left = Binding right = Binding } result = Bool body = { return = { value = equal(left, right) } } }
                procedure Setup { locals = { result = Binding } body = {
                  create = { type = Result scope = frameScope() into = result fields = { Same = pureCall(Same, none, none) } }
                } }
                start = [Setup]
                """);
            var s = new Session(rules, 1, ["player"]); s.Start(); var type = rules.TypeByQualifiedId("dispatches:Result");
            Check.True(s.State.GetField(s.State.AllInstances(type.Id).Single(), type.FieldIndex("Same")).AsBool(), "two absent references compare equal");
        });
        Check.Run("DISPATCH template cases bind lexical handlers and pure results without query mutations", () =>
        {
            var rules = Rules("""
                dispatch Value { pure = true parameters = { amount = Int } result = Int }
                template Entry { parameters = { key = { type = String(length = 16) } add = { type = Int } } body = {
                  procedure Body { pure = true parameters = { amount = Int } result = Int body = { return = { value = add(amount, arg(add)) } } }
                  dispatchCase Case { dispatch = Value key = arg(key) procedure = Body }
                } }
                use First { template = Entry args = { key = "a" add = 2 } }
                use Second { template = Entry args = { key = "b" add = 5 } }
                entity Result { fields = { Value = { type = Int } } }
                procedure Setup { locals = { result = Binding } body = {
                  create = { type = Result scope = frameScope() into = result fields = { Value = add(pureCall(Value, "a", 10), pureCall(Value, "b", 10)) } }
                } }
                start = [Setup]
                """);
            var s = new Session(rules, 1, ["player"]); s.Start();
            var type = rules.TypeByQualifiedId("dispatches:Result");
            Check.Equal(27L, s.State.GetField(s.State.AllInstances(type.Id).Single(), type.FieldIndex("Value")).AsInt(), "each case uses its own template constant");
        });
        Check.Run("DISPATCH duplicate keys, missing tables, signatures and impure value handlers fail at compile time", () =>
        {
            const string table = "dispatch Value { pure = true parameters = { amount = Int } result = Int }";
            const string handler = "procedure Body { pure = true parameters = { amount = Int } result = Int body = { return = { value = amount } } }";
            const string entry = "dispatchCase Entry { dispatch = Value key = \"a\" procedure = Body }";
            foreach (var source in new[] {
                table + handler + entry + entry.Replace("Entry", "Other"),
                handler + entry,
                table + handler.Replace("amount = Int", "amount = Bool") + entry,
                table + handler.Replace("pure = true", "pure = false") + entry,
                table + handler.Replace("result = Int", "result = Bool").Replace("value = amount", "value = true") + entry })
                Check.Throws<DslSourceException>(() => Rules(source), "invalid closed dispatch must fail before runtime");
        });
        Check.Run("DISPATCH unknown key fails before handler effects and suspended handlers resume once", () =>
        {
            var rules = Rules("""
                dispatch Execute { parameters = { target = Ref(Result) } }
                entity Result { fields = { Value = { type = Int default = 0 } } }
                procedure Body { parameters = { target = Ref(Result) } locals = { answer = Bool } body = {
                  set = { target = target field = Result.Value value = add(field(target, Result.Value), 1) }
                  decide = { participant = participant(1) kind = Bool prompt = "Continue" into = answer }
                  set = { target = target field = Result.Value value = add(field(target, Result.Value), 10) }
                } }
                dispatchCase Entry { dispatch = Execute key = "known" procedure = Body }
                procedure Setup { locals = { target = Binding } body = { create = { type = Result scope = frameScope() into = target } } }
                start = [Setup]
                action Known { role = player body = { call = { procedure = Execute args = { key = "known" target = first(type(Result)) } } } }
                action Missing { role = player body = { call = { procedure = Execute args = { key = "missing" target = first(type(Result)) } } } }
                """);
            foreach (var missing in new[] { false, true })
            {
                var s = new Session(rules, 1, ["player"]); s.Start();
                var type = rules.TypeByQualifiedId("dispatches:Result");
                long Read() => s.State.GetField(s.State.AllInstances(type.Id).Single(), type.FieldIndex("Value")).AsInt();
                var action = rules.Actions.Single(a => a.QualifiedId == (missing ? "dispatches:Missing" : "dispatches:Known"));
                if (missing)
                {
                    Check.Throws<EngineFaultException>(() => s.Submit(action, s.Participant(0), []), "missing key must fail");
                    Check.Equal(0L, Read(), "no handler effects for unknown keys");
                }
                else
                {
                    s.Submit(action, s.Participant(0), []); Check.Equal(1L, Read(), "before pause");
                    var pending = s.PendingDecision!; s.Answer(new DecisionAnswer(pending.RequestId, Flag: true));
                    Check.Equal(11L, Read(), "continuation does not replay the first instruction");
                }
            }
        });
    }
}
