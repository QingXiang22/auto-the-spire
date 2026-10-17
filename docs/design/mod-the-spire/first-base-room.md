# 在base房间接口上写出第一个动作

> 2026-10-18：本页保留旧 C# base 的教程／接口依据，所列运行路径已退出生产，当前不能照此执行。纯 DSL 开发请从[开发指南](base-development-guide.md)与[包格式参考](../../dsl/core/0.3/package-reference.md)开始；原功能需按迁移任务恢复。


文档类型：教程（Tutorial）。你将从空目录创建房间叶，依次观察没有动作、领取金币、领取后关闭访问。你只编写一个`Register`方法，固定宿主负责预置场景。

前置条件：.NET 9 SDK和PowerShell；在仓库根目录操作。教程使用受控宝箱节点、金币10、阶段5，没有真实地图生成、开局库存或随机抽取。正式接入另见[房间操作指南](how-to-add-a-room.md)。

## 1. 创建项目并运行空房间叶

```powershell
$roomLesson = '.audit/tutorial-base-room'
if (Test-Path $roomLesson) { throw '学习目录已存在，请保留旧成果并选择一个新的目录名' }
New-Item -ItemType Directory -Path $roomLesson | Out-Null
$baseProject = (Resolve-Path 'mod-the-spire/base-spire/BaseSpire/BaseSpire.csproj').Path
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="$baseProject" /></ItemGroup>
</Project>
"@ | Set-Content -Encoding utf8 "$roomLesson/LearnRoom.csproj"
Copy-Item docs/examples/base-room/TutorialHost.cs.txt "$roomLesson/Program.cs"
@'
using BaseSpire.Slice;
using CoreTheSpire.Engine;

static class ExampleRoom
{
    public static void Register(EnginePackageBuilder package, RoomCompileContext room)
    {
    }
}
'@ | Set-Content -Encoding utf8 "$roomLesson/ExampleRoom.cs"
dotnet run --project "$roomLesson/LearnRoom.csproj" --artifacts-path .artifacts/parallel/tutorial-base-room -v quiet
```

应看到：

```text
start: Gold=10, MapPhase=5
available_actions=0
```

项目已引用base，宿主已创建同一Engine内的Run和节点；空房间叶没有注册动作。宿主是提供给你的脚手架，无需先学习其状态预置代码。

## 2. 注册领取金币动作

用下面的完整文件替换空房间叶：

```powershell
@'
using BaseSpire.Slice;
using CoreTheSpire.Engine;

static class ExampleRoom
{
    public static void Register(EnginePackageBuilder package, RoomCompileContext room)
    {
        var body = package.ActionBody(package.Action("TakeExampleGold"));
        var run = room.CurrentRun(body);
        body.Role("player");
        body.Requires(room.At(body, run, MapRoomWiring.TreasurePhase, "Treasure"));
        body.Body(
            body.SetField(run, room.Gold, body.Add(body.Field(run, room.Gold), body.Const(7L))));
    }
}
'@ | Set-Content -Encoding utf8 "$roomLesson/ExampleRoom.cs"
dotnet run --project "$roomLesson/LearnRoom.csproj" --artifacts-path .artifacts/parallel/tutorial-base-room -v quiet
```

应看到：

```text
start: Gold=10, MapPhase=5
available_actions=1
close: Gold=17, MapPhase=5
remaining_actions=1
```

动作让长期金币增加7。阶段仍为5，所以这个动作还可以再次执行。宿主的`close`只是输出标签，此时房间并未关闭。

`CurrentRun`生成读取Run的表达式，`At`同时检查房型和阶段，`SetField`生成Engine写指令。金币不会存在`ExampleRoom`的C#变量里。

## 3. 领取后关闭访问

将动作体的最后一行换成金币变化加关闭指令：

```powershell
$roomText = Get-Content -Raw "$roomLesson/ExampleRoom.cs"
$roomText.Replace('body.Const(7L))));', "body.Const(7L))),`n            room.CloseVisit(body, run));") | Set-Content -Encoding utf8 "$roomLesson/ExampleRoom.cs"
dotnet run --project "$roomLesson/LearnRoom.csproj" --artifacts-path .artifacts/parallel/tutorial-base-room -v quiet
```

应看到：

```text
start: Gold=10, MapPhase=5
available_actions=1
close: Gold=17, MapPhase=2
remaining_actions=0
```

阶段2表示恢复旅行。动作的房间守卫不再成立，因此不会再次出现在合法动作列表中。关闭发生在金币提交之后。

你已经新增了一个真实Engine房间动作，作者文件只依赖base的公开编译接口。正式游戏尚未安装这个示例：下一步按[正式接入三处](how-to-add-a-room.md#3-由负责人接入正式入口)处理规则包构建入口、地图进入和动作注册，再核对公开语义。自动房间插件发现尚未提供，不能省略这一步。

继续修改你自己的动作时，查[接口参考](room-compile-reference.md)即可。普通不可执行条件应放在守卫/候选中；规则`Fail`会使Session进入故障，不能用它表达“金币不够但仍可离店”。
