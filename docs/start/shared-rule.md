# 编写共享触发规则并新增内容配置

文档类型：教程（Tutorial）。你将创建一个“每实际抽到一张牌，获得 2 格挡”的教学遗物。共享机制用完整 DSL 表达，本例具体遗物使用 `use` 配置。学习目标是分清事件订阅、持有实例过滤、共享操作与内容定义的职责。

前置条件：完成[第一个遗物教程](first-relic.md)，保持同一 PowerShell 会话中的 `$basePackage` 和 `$baseManifest`。副本中的 TheAbacus 数值此时为 8。以下 PracticeBeads 是教学内容，不宣称对应原版遗物，也不加入原版来源条目总数。

## 1. 添加共享规则

将已给出的[完整共享模板](../examples/base-dsl/after-draw-block.spire)复制到副本的规则目录。

```powershell
Copy-Item -LiteralPath 'docs/examples/base-dsl/after-draw-block.spire' -Destination (Join-Path $basePackage 'relics/rules/after-draw-block.spire')
```

打开该文件，沿下面四处查看：

1. `parameters` 限定内容可配置的 definition 和 amount。
2. `WhenDrawn` 接收 `AfterCardDrawn` 的 owner、card，以及订阅绑定的 listener 遗物实例，不依赖全局当前上下文。
3. `subscription OnDraw` 的 instances 筛选同一 Holder、未熔化、匹配 definition 的实例，instance_order 按库存顺序派发；执行前会再次检查有效性。
4. `WhenDrawn` 只响应当前实例并调用 `GainUnpoweredBlock`，不再扫描库存；`action Obtain` 复用 `ObtainRelic`。

`GainUnpoweredBlock`、遗物实例和抽牌事件均来自现有 DSL。这个教学模板的获得动作沿用开发场景的单 Player 守卫，不能当作多人通用获得 API。

## 2. 添加具体配置并显式组合

将[教学配置](../examples/base-dsl/practice-beads.spire)放到来源组目录，再将两个文件加入领域索引。

```powershell
Copy-Item -LiteralPath 'docs/examples/base-dsl/practice-beads.spire' -Destination (Join-Path $basePackage 'relics/shared/practice-beads.spire')
$baseRelicIndex = (Resolve-Path (Join-Path $basePackage 'relics/index.spire')).Path
[IO.File]::WriteAllText($baseRelicIndex, [IO.File]::ReadAllText($baseRelicIndex).Replace('"shared/the-abacus.spire"', '"shared/the-abacus.spire", "rules/after-draw-block.spire", "shared/practice-beads.spire"'))
```

具体配置只包含以下 `use`，完整文件另有同一 namespace 和 core/0.3 头部：

```text
use PracticeBeads {
  template = AfterDrawBlockRelic
  args = { definition = "tutorial:practice-beads" amount = 2 }
}
```

两个文件没有加入索引时，包加载会拒绝未引用规则；不会自动扫描注册。索引只组合声明，不承担玩法。

## 3. 登记公开内容

在副本公开元数据的 Relic 内容列表加入教学 ID。不要将它伪装成原版源目录条目。

```powershell
$basePublic = (Resolve-Path (Join-Path $basePackage 'public/metadata.json')).Path
$baseMetadata = Get-Content -LiteralPath $basePublic -Raw | ConvertFrom-Json
$baseMetadata.content.'base-spire:Relic'.content_ids += 'tutorial:practice-beads'
[IO.File]::WriteAllText($basePublic, ($baseMetadata | ConvertTo-Json -Depth 30))
dotnet run --no-build --project tools/base-spire-dev -- validate $baseManifest
```

加载应成功。动作由 DSL 声明派生，公开内容名称来自元数据；无需在 net 添加逐遗物注册代码。正式原版内容的来源与 definition 登记另见[扩展内容](../areas/base/how-to/extend-content.md)。

## 4. 逐次观察真实抽牌

仅获得新遗物，连续执行四次 Draw。

```powershell
python -X utf8 docs/examples/base-dsl/run_relic_lesson.py --manifest $baseManifest --obtain PracticeBeads.Obtain --draw-count 4 --expect-trace 2,4,6,6 --expect-block 6 --report .artifacts/base-spire-docs/lesson-shared-rule.json
```

你应看到 `block_trace: [2, 4, 6, 6]`。前面三次各实际抽到一张牌，第四次两堆为空，没有 `AfterCardDrawn`，所以不加格挡。

再运行一个不持有遗物的会话，以及同时持有两个遗物的会话。

```powershell
python -X utf8 docs/examples/base-dsl/run_relic_lesson.py --manifest $baseManifest --draw-count 4 --expect-trace 0,0,0,0 --expect-block 0
python -X utf8 docs/examples/base-dsl/run_relic_lesson.py --manifest $baseManifest --obtain TheAbacus.Obtain --obtain PracticeBeads.Obtain --draw-count 3 --expect-trace 10,12,14 --expect-block 14 --report .artifacts/base-spire-docs/lesson-combined.json
```

第二个会话第一次洗牌获得练习 TheAbacus 的 8 点，再由新遗物每张获得 2 点，最终 14。两种机制共用同一状态、抽牌过程和 Engine 执行器。

你已完成共享模板、内容配置、显式组合和公开执行。把这套方法用于正式内容前，还需[迁移完整机制](../areas/base/how-to/migrate-mechanism.md)规定的源依据、生命周期边界和行为执行结果；教学成功不构成原版独立参考比较。
