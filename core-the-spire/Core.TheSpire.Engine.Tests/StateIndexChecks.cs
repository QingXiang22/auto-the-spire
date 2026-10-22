using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class StateIndexChecks
{
    public static void Run() => Check.Run("state type queries retain creation order across rollback, destruction and clone isolation", () =>
    {
        var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile("""
            using namespace index-test; dsl_version = "core/0.3"
            entity A { fields = {} } entity B { fields = {} }
            """)).Build();
        var state = new SessionState(rules);
        var a = rules.TypeByQualifiedId("index-test:A").Id;
        var b = rules.TypeByQualifiedId("index-test:B").Id;
        var first = state.CreateInstance(a, state.RootScope);
        var other = state.CreateInstance(b, state.RootScope);
        var second = state.CreateInstance(a, state.RootScope);
        Check.Sequence(new[] { first, second }, state.AllInstances(a), "interleaved types stay ordered");
        Check.Throws<InvalidOperationException>(() => state.RunAtomic(() =>
        {
            state.DestroyInstance(first);
            state.CreateInstance(a, state.RootScope);
            throw new InvalidOperationException("rollback");
        }), "failed mutation rolls back both directions");
        Check.Sequence(new[] { first, second }, state.AllInstances(a), "restored instance regains its original position");
        Check.Sequence(new[] { first, other, second }, state.AllInstances(), "all-type ordering agrees");
        var clone = state.Clone();
        clone.DestroyInstance(first);
        var third = clone.CreateInstance(a, clone.RootScope);
        Check.Sequence(new[] { second, third }, clone.AllInstances(a), "clone has its own type storage");
        Check.Sequence(new[] { first, second }, state.AllInstances(a), "clone mutations leave the original intact");
        state.DestroyInstance(other);
        Check.Equal(0, state.AllInstances(b).Count, "destroying the last instance empties the type");
    });
}
