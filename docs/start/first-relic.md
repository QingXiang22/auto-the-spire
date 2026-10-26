# 配置第一个遗物并观察效果

文档类型：教程（Tutorial）。你将从正式包的隔离副本开始，使用已有 DSL 模板把重洗后的格挡从 6 改为 8，并通过真实公开接口观察结果。你不需要修改 C#。

前置条件：仓库根目录、PowerShell、Python 3.12 和 `global.json` 指定的 .NET SDK 9.0.306。本练习使用开发牌堆，不是原版开局。正式 TheAbacus 仍应为 6，练习修改不提交到正式包。

## 1. 创建隔离副本

在 PowerShell 中执行以下命令。若学习目录已存在，保留原成果，换一个目录名并继续使用同一组变量。

```powershell
$baseLesson = '.artifacts/tutorials/base-spire-authoring-lesson'
if (Test-Path -LiteralPath $baseLesson) { throw '学习目录已存在，请保留并选择新的目录名' }
New-Item -ItemType Directory -Path $baseLesson | Out-Null
Copy-Item -LiteralPath 'mod-the-spire/base-spire' -Destination $baseLesson -Recurse
$basePackage = Join-Path $baseLesson 'base-spire'
$baseManifest = Join-Path $basePackage 'package.json'
dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj -v quiet
```

构建应成功。这会同时构建教程需要的通用 Host。正式包目录没有改变；后续命令始终指向副本 `$baseManifest`。

## 2. 加载并运行原配置

先验证纯包，再通过公开合法动作获得 TheAbacus 并抽一张牌。

```powershell
dotnet run --no-build --project tools/base-spire-dev -- validate $baseManifest
python -X utf8 docs/examples/base-dsl/run_relic_lesson.py --manifest $baseManifest --obtain TheAbacus.Obtain --expect-block 6 --report .artifacts/base-spire-docs/lesson-original.json
```

两条命令退出码应为 0。第二条输出 `block_trace: [6]` 和 `passed: true`：开发抽牌堆原为空、弃牌堆有三张牌，实际重洗后触发 6 点格挡，然后抽出一张。

驱动脚本只通过现有 Python 客户端执行 describe→catalog→start→step→observe，不实现重洗或格挡。报告记录实际规则标识、scope 和固定游戏 seed `BASE-DSL-LESSON-00`，并标记 `independent_verified: false`。

## 3. 只修改内容参数

打开副本 `relics/shared/the-abacus.spire`。它的核心配置如下：

```text
use TheAbacus {
  template = AfterShuffleBlockRelic
  args = { definition = "base-spire:the-abacus" amount = 6 }
}
```

保留文件头、namespace、模板名和 definition，只把 amount 改成 8。以下命令完成同一编辑：

```powershell
$baseLeaf = (Resolve-Path (Join-Path $basePackage 'relics/shared/the-abacus.spire')).Path
[IO.File]::WriteAllText($baseLeaf, [IO.File]::ReadAllText($baseLeaf).Replace('amount = 6', 'amount = 8'))
dotnet run --no-build --project tools/base-spire-dev -- validate $baseManifest
python -X utf8 docs/examples/base-dsl/run_relic_lesson.py --manifest $baseManifest --obtain TheAbacus.Obtain --expect-block 8 --report .artifacts/base-spire-docs/lesson-configured.json
```

你应看到 `block_trace: [8]`、`passed: true`，规则标识与上一阶段不同。该改变只来自 DSL 数值，没有更改 Host、效果枚举或 C# 规则。

正式工具的 `check` 固定验证原版 TheAbacus 为 6，不适合作为这个 8 点练习副本的成功标准。这里使用明确的教学预期；正式规则不能跟着练习改成 8 或放宽检查。

## 4. 观察模板的边界

尝试把 8 改成 1000，验证加载拒绝超出模板范围的参数，然后恢复 8。

```powershell
[IO.File]::WriteAllText($baseLeaf, [IO.File]::ReadAllText($baseLeaf).Replace('amount = 8', 'amount = 1000'))
dotnet run --no-build --project tools/base-spire-dev -- validate $baseManifest
if ($LASTEXITCODE -ne 2) { throw '超范围模板参数应被拒绝，退出码应为 2' }
[IO.File]::WriteAllText($baseLeaf, [IO.File]::ReadAllText($baseLeaf).Replace('amount = 1000', 'amount = 8'))
```

错误应包含真实内容文件位置与参数不符合模板形状的说明。`amount` 的边界来自共享 DSL 模板，不来自 C# 遗物分支。其他模板参数错误见[排障指南](../areas/base/how-to/verify-and-debug.md)。

你已经完成了加载、修改内容和观察效果。下一篇[共享触发规则教程](shared-rule.md)沿用此副本，创建另一种触发机制。完整模板原理见[开发模型](../development/base-spire/explanation/development-model.md)。
