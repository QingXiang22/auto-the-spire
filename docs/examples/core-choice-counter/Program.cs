using CoreTheSpire.Engine;

// 普通规则作者先读 game.spire：导入规则库、实例化模板并设置参数。
// rules/score-race.spire 展示如何用 DSL 编写库；counter.spire 保留底层事件/暂停续接示例。
// 这个 C# 文件是演示宿主：负责读文件、创建一局游戏、提交操作和回答选择。
// 运行：dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj
// 可选参数：<规则文件路径> <按逗号分隔的候选下标>。默认 "1,0" 表示先选第二项，再选第一项。
// 配置文件改动后，可用 --no-build 加文件路径运行同一宿主；无需修改这里的游戏逻辑。
var path = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "game.spire");
var answers = (args.Length > 1 ? args[1] : "1,0").Split(',').Select(int.Parse).ToArray();
// 解析配置、编译并链接规则。Session 保存这一局的状态和执行位置。
var rules = EngineDslCompiler.LoadFiles(path);
var session = new Session(rules, seed: 42, participantRoles: ["player"]);
// 这里按下标取第一个玩家；DSL 中 participant(1) 使用的是从 1 开始的参与者 ID。
var player = session.Participant(0);
// 本例只声明一个玩家视图。观察只输出该视图允许公开的信息。
var projection = session.ProjectionsFor(player).Single();
var answerIndex = 0;
// 开局执行规则文件登记的 Setup，然后打印初始状态。
session.Start();
Print("start");
while (session.Status is SessionStatus.Running or SessionStatus.AwaitingDecision)
{
    if (session.Status == SessionStatus.Running)
    {
        // Running 表示目前未暂停或结束。本例自动提交首个合法动作，而不是等待人工输入。
        var action = session.LegalActions(player).FirstOrDefault()
            ?? throw new InvalidOperationException("Running session has no legal action.");
        // 多个参数绑定就是不同合法选择。加分游戏直接按下标提交 +2/+1，无需中途 decide。
        var bindingIndex = action.Bindings.Count > 1 ? NextChoice() : 0;
        var arguments = action.Bindings[bindingIndex].Take(action.Definition.Parameters.Count).ToArray();
        // 演示宿主只处理已枚举好的参数；完整游戏界面还需要让玩家填写自由参数。
        if (arguments.Any(value => value.Kind == ValueKind.None))
            throw new InvalidOperationException("This demo host needs fully enumerated action parameters.");
        session.Submit(action.Definition, player, arguments);
        Print("submit");
    }
    else
    {
        // AwaitingDecision 表示规则执行到了 decide。按预设下标挑候选，再 Answer 续接。
        // 默认第一次取下标 1（加 2），第二次取下标 0（加 1）。
        var request = session.PendingDecision!;
        var index = NextChoice();
        var instances = request.Kind switch
        {
            DecisionResponseKind.EntityRef => new[] { request.Candidates[index] },
            DecisionResponseKind.EntityRefList => request.Candidates.Take(checked((int)request.Minimum)).ToArray(),
            _ => null,
        };
        // RequestId 必须对应当前请求。这个小游戏的 EntityRef 回答只使用 Instances。
        // 其余值是演示宿主给其他回答种类提供的简单默认值，不代表通用游戏策略。
        session.Answer(new DecisionAnswer(request.RequestId, Instances: instances,
            Number: request.Minimum, Flag: true, EnumOrdinal: 0, Participant: request.Participant));
        Print("answer");
    }
}
// Terminal 表示包设置了终局；Faulted 表示执行故障。两者都会退出上面的循环。
Console.WriteLine($"result: {session.Terminal?.Definition.QualifiedId ?? session.Fault?.ToString()}");

int NextChoice() => answerIndex < answers.Length ? answers[answerIndex++] : 0;

void Print(string command)
{
    // Observe 读取当前公开观察，不推进游戏；Print 仅展示字段，没有加分或判定胜负的规则。
    var view = session.Observe(player, projection);
    Console.WriteLine($"{command}: {session.Status}");
    foreach (var instance in view.Instances)
        Console.WriteLine($"  {instance.TypeId}: " + string.Join(", ",
            instance.Fields.Select(field => $"{field.Name}={field.Value}")));
}
