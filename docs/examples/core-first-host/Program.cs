using CoreTheSpire.Engine;

// 从仓库根目录运行；--observe-only 用于 DSL 教程的开局检查。
var observeOnly = args.Contains("--observe-only");
var inputs = args.Where(arg => arg != "--observe-only").ToArray();
var path = inputs.Length > 0 ? inputs[0] : "docs/examples/core-choice-counter/first-counter.spire";
var choices = (inputs.Length > 1 ? inputs[1] : "1,0").Split(',').Select(int.Parse).ToArray();
var choiceIndex = 0;
Session? session = null;

try
{
    var rules = EngineDslCompiler.LoadFiles(path);
    session = new Session(rules, seed: 42, participantRoles: ["player"]);
    var player = session.Participant(0);
    var projection = session.ProjectionsFor(player).Single();

    session.Start();
    Print("start");
    if (observeOnly) return;

    while (session.Status is SessionStatus.Running or SessionStatus.AwaitingDecision)
    {
        if (session.Status == SessionStatus.Running)
        {
            // 教程中的两个规则文件都只有一个无参数动作。
            // 参数化动作的 Bindings 用法见宿主教程，不在这里猜测参数。
            var action = session.LegalActions(player).Single();
            if (action.Definition.Parameters.Count != 0)
                throw new InvalidOperationException("This tutorial host supports parameterless actions only.");
            session.Submit(action.Definition, player, []);
            Print("submit");
        }
        else
        {
            // 动作暂停时只回答当前请求，随后从暂停位置继续。
            var request = session.PendingDecision!;
            if (request.Kind != DecisionResponseKind.EntityRef)
                throw new InvalidOperationException("This tutorial host supports EntityRef decisions only.");
            var index = choiceIndex < choices.Length ? choices[choiceIndex++] : 0;
            if (index < 0 || index >= request.Candidates.Count)
                throw new ArgumentOutOfRangeException(nameof(index), "Choice index is outside the candidate list.");
            session.Answer(new DecisionAnswer(request.RequestId,
                Instances: [request.Candidates[index]]));
            Print("answer");
        }
    }

    if (session.Terminal is { } terminal)
    {
        Console.WriteLine($"result: {terminal.Definition.QualifiedId}");
        for (var i = 0; i < terminal.Definition.Fields.Count; i++)
            Console.WriteLine($"  {terminal.Definition.Fields[i].Name}={terminal.Fields[i]}");
    }
    else
    {
        Console.Error.WriteLine($"fault: {session.Fault}");
        Environment.ExitCode = 1;
    }

    void Print(string command)
    {
        var view = session.Observe(player, projection);
        Console.WriteLine($"{command}: {session.Status}");
        foreach (var instance in view.Instances)
            Console.WriteLine($"  {instance.TypeId}: " + string.Join(", ",
                instance.Fields.Select(field => $"{field.Name}={field.Value}")));
    }
}
catch (EngineFaultException exception)
{
    // 输入被拒绝与规则执行失败都会抛这个异常；Status 区分会话是否还能继续。
    Console.Error.WriteLine($"engine error: {exception.Fault}");
    if (session is not null) Console.Error.WriteLine($"session: {session.Status}");
    Environment.ExitCode = 1;
}
