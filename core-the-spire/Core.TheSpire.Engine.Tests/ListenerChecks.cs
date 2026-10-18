using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class ListenerChecks
{
    private static RuleSet Rules(string source) => new RuleSetBuilder().Add(EngineDslCompiler.Compile(
        "using namespace listeners; dsl_version = \"core/0.3\" " + source)).Build();
    private static long Field(Session s, string type, string field) { var t = s.RuleSet.TypeByQualifiedId("listeners:" + type); return s.State.GetField(s.State.AllInstances(t.Id).First(), t.FieldIndex(field)).AsInt(); }
    private static void Submit(Session s, string name) => s.Submit(s.RuleSet.Actions.Single(a => a.QualifiedId == "listeners:" + name), s.Participant(0), []);
    private const string Model = """
        entity Item { fields = { Digit = { type = Int default = 0 } Active = { type = Bool default = true } } }
        collection Items { element = Item ordered = true }
        entity Result { fields = { Value = { type = Int default = 0 } } }
        procedure Setup { locals = { item = Binding result = Binding } body = {
          create = { type = Result scope = frameScope() into = result }
          create = { type = Item scope = frameScope() into = item collection = Items fields = { Digit = 2 } }
          create = { type = Item scope = frameScope() into = item collection = Items fields = { Digit = 1 } }
        } }
        start = [Setup]
        """;

    public static void Run()
    {
        Check.Run("LISTEN cross-package instance queries own temporary slots and resolve the subscriber namespace", () =>
        {
            var library = EngineDslCompiler.Compile("""
                using namespace library; dsl_version = "core/0.3"
                entity Item { fields = { Value = { type = Int default = 7 } } }
                collection Items { element = Item ordered = true }
                event Ping { }
                procedure React { parameters = { listener = Ref(Item) } locals = { untouched = Int } body = {
                  set = { target = listener field = Item.Value value = add(field(listener, Item.Value), 1) }
                } }
                procedure Setup { locals = { item = Binding } body = { create = { type = Item scope = frameScope() into = item collection = Items } } }
                start = [Setup]
                """);
            var subscriber = EngineDslCompiler.Compile("""
                using namespace subscriber; dsl_version = "core/0.3" dependencies = [library]
                entity Decoy { fields = { Value = { type = Int default = 99 } } }
                subscription React { event = library:Ping reaction = library:React
                  instances = where(collection(library:Items), i, greater(field(i, library:Item.Value), 0))
                  instance_order = collectionIndex(library:Items, listener)
                  recheck = greater(field(listener, library:Item.Value), 0)
                }
                action Ping { role = player body = { emit = { event = library:Ping } } }
                """, dependencies: [library]);
            var s = new Session(new RuleSetBuilder().Add(library).Add(subscriber).Build(), 1, ["player"]);
            s.Start(); s.Submit(s.RuleSet.Actions.Single(), s.Participant(0), []);
            var type = s.RuleSet.TypeByQualifiedId("library:Item");
            Check.Equal(8L, s.State.GetField(s.State.AllInstances(type.Id).Single(), type.FieldIndex("Value")).AsInt(), "query temporaries and imported reaction resolve independently");
        });
        Check.Run("LISTEN instance notifications follow collection order across subscriptions and recheck validity", () =>
        {
            var s = new Session(Rules(Model + """
                event Ping { }
                procedure One { parameters = { listener = Ref(Item) } body = {
                  set = { target = first(type(Result)) field = Result.Value value = add(multiply(field(first(type(Result)), Result.Value), 10), 1) }
                } }
                procedure Two { parameters = { listener = Ref(Item) } body = {
                  set = { target = first(type(Result)) field = Result.Value value = add(multiply(field(first(type(Result)), Result.Value), 10), 2) }
                } }
                subscription One { event = Ping reaction = One instances = where(collection(Items), i, and(field(i, Item.Active), equal(field(i, Item.Digit), 1))) instance_order = collectionIndex(Items, listener) }
                subscription Two { event = Ping reaction = Two instances = where(collection(Items), i, and(field(i, Item.Active), equal(field(i, Item.Digit), 2))) instance_order = collectionIndex(Items, listener) }
                action Ping { role = player body = { emit = { event = Ping } } }
                action Reverse { role = player body = { moveInCollection = { collection = Items item = first(collection(Items)) position = End } } }
                """), 1, ["player"]);
            s.Start(); Submit(s, "Ping"); Check.Equal(21L, Field(s, "Result", "Value"), "instance order overrides declaration order");
            Submit(s, "Reverse"); Submit(s, "Ping"); Check.Equal(2112L, Field(s, "Result", "Value"), "next event snapshots reordered instances");
        });
        Check.Run("LISTEN snapshot excludes additions and skips destroyed or deactivated instances after a nested event", () =>
        {
            foreach (var mutation in new[] { "destroy = { target = last }", "set = { target = last field = Item.Active value = false }" })
            {
                var s = new Session(Rules(Model + """
                    event Ping { }
                    event Nested { }
                    procedure Nested { body = { set = { target = first(type(Result)) field = Result.Value value = add(field(first(type(Result)), Result.Value), 10) } } }
                    subscription Nested { event = Nested reaction = Nested }
                    procedure React { parameters = { listener = Ref(Item) } locals = { last = Binding added = Binding } body = {
                      set = { target = first(type(Result)) field = Result.Value value = add(field(first(type(Result)), Result.Value), field(listener, Item.Digit)) }
                      if = { condition = equal(field(listener, Item.Digit), 2) then = {
                        let = { into = last value = first(where(collection(Items), i, equal(field(i, Item.Digit), 1))) }
                        MUTATION
                        create = { type = Item scope = frameScope() into = added collection = Items fields = { Digit = 9 } }
                        emit = { event = Nested }
                      } }
                    } }
                    subscription React { event = Ping reaction = React instances = where(collection(Items), i, field(i, Item.Active)) instance_order = collectionIndex(Items, listener) }
                    action Ping { role = player body = { emit = { event = Ping } } }
                    """.Replace("MUTATION", mutation)), 1, ["player"]);
                s.Start(); Submit(s, "Ping"); Check.Equal(12L, Field(s, "Result", "Value"), "original first plus nested effect only");
            }
        });
        Check.Run("LISTEN paused instance notification resumes once and preserves the remaining instance snapshot", () =>
        {
            var s = new Session(Rules(Model + """
                event Ping { }
                procedure React { parameters = { listener = Ref(Item) } locals = { answer = Bool } body = {
                  set = { target = first(type(Result)) field = Result.Value value = add(field(first(type(Result)), Result.Value), field(listener, Item.Digit)) }
                  if = { condition = equal(field(listener, Item.Digit), 2) then = { decide = { participant = participant(1) kind = Bool prompt = "continue" into = answer } } }
                } }
                subscription React { event = Ping reaction = React instances = collection(Items) instance_order = collectionIndex(Items, listener) }
                action Ping { role = player body = { emit = { event = Ping } } }
                """), 1, ["player"]);
            s.Start(); Submit(s, "Ping"); Check.Equal(SessionStatus.AwaitingDecision, s.Status, "listener suspended");
            Check.Equal(2L, Field(s, "Result", "Value"), "first listener ran before suspension");
            var pending = s.PendingDecision!; s.Answer(new DecisionAnswer(pending.RequestId, Flag: true));
            Check.Equal(3L, Field(s, "Result", "Value"), "resume does not replay first listener"); Check.Equal(SessionStatus.Running, s.Status, "all remaining listeners completed");
        });
        Check.Run("LISTEN pure folds share instance ordering, support nested folds and never mutate query state", () =>
        {
            var s = new Session(Rules(Model + """
                event Inner { payload = { value = Int } }
                event Empty { payload = { value = Int } }
                event Modify { payload = { value = Int } }
                procedure Inner { pure = true parameters = { value = Int } result = Int body = { return = { value = add(value, 10) } } }
                subscription Inner { event = Inner reaction = Inner }
                procedure One { pure = true parameters = { value = Int listener = Ref(Item) } result = Int body = { return = { value = add(multiply(value, 10), field(listener, Item.Digit)) } } }
                subscription One { event = Modify reaction = One instances = where(collection(Items), i, equal(field(i, Item.Digit), 1)) instance_order = collectionIndex(Items, listener) }
                subscription Two { event = Modify reaction = One instances = where(collection(Items), i, equal(field(i, Item.Digit), 2)) instance_order = collectionIndex(Items, listener) }
                action Read { role = player requires = equal(foldEvent(Modify, foldEvent(Empty, foldEvent(Inner, 0))), 1021) body = { } }
                """), 1, ["player"]);
            s.Start(); var hash = s.State.StateHash(); var version = s.StateVersion; var draws = s.Random.TotalDraws;
            for (var i = 0; i < 3; i++) Check.Equal(1, s.LegalActions(s.Participant(0)).Count, "folded guard remains available");
            Check.Equal(hash, s.State.StateHash(), "query state hash"); Check.Equal(version, s.StateVersion, "query state version"); Check.Equal(draws, s.Random.TotalDraws, "query random count");
        });
        Check.Run("LISTEN folds reject impure, missing, wrong and incomplete accumulator results", () =>
        {
            foreach (var declaration in new[] {
                "procedure R { parameters = { value = Int } result = Int body = { return = { value = value } } }",
                "procedure R { pure = true parameters = { value = Int } body = { } }",
                "procedure R { pure = true parameters = { value = Int } result = Bool body = { return = { value = true } } }",
                "procedure R { pure = true result = Int body = { return = { value = 1 } } }" })
                Check.Throws<EngineFaultException>(() => Rules("event M { payload = { value = Int } } " + declaration + " subscription S { event = M reaction = R } action A { requires = equal(foldEvent(M, 0), 0) body = { } }"), "invalid fold listener is rejected at link time");
        });
        Check.Run("LISTEN recursive pure folds respect the existing call budget", () =>
        {
            var s = new Session(Rules("""
                event M { payload = { value = Int } }
                procedure R { pure = true parameters = { value = Int } result = Int body = { return = { value = foldEvent(M, value) } } }
                subscription S { event = M reaction = R }
                procedure Setup { locals = { value = Int } body = { let = { into = value value = foldEvent(M, 0) } } }
                start = [Setup]
                """), 1, ["player"], budget: new SessionBudget(MaxCallDepth: 8));
            Check.Fault(EngineFaultCode.BudgetExceeded, s.Start, "existing call-depth guard");
        });
    }
}
