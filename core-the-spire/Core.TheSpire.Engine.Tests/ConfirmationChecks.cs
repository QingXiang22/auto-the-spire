using CoreTheSpire.Engine;

namespace CoreTheSpire.Engine.Tests;

internal static class ConfirmationChecks
{
    public static void Run()
    {
        Check.Run("confirmation rejects false without consuming the pending request", () =>
        {
            var rules = new RuleSetBuilder().Add(EngineDslCompiler.Compile("""
                using namespace confirm-test; dsl_version = "core/0.3"
                procedure Setup { locals = { value = Bool } body = {
                  decide = { participant = participant(1) kind = Confirm prompt = "Continue" into = value }
                } } start = [Setup]
                """)).Build();
            var session = new Session(rules, 1, ["player"]); session.Start();
            var request = session.PendingDecision!; var state = session.State.StateHash();
            Check.Fault(EngineFaultCode.InvalidDecisionResponse, () => session.Answer(new DecisionAnswer(request.RequestId, Flag: false)), "unconfirmed answer");
            Check.Equal(request.RequestId, session.PendingDecision!.RequestId, "same request remains pending");
            Check.Equal(state, session.State.StateHash(), "invalid answer writes nothing");
            session.Answer(new DecisionAnswer(request.RequestId, Flag: true));
            Check.Equal(SessionStatus.Running, session.Status, "affirmative answer completes");
        });

    }
}
