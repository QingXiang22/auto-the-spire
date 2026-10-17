# 在已有规则集上添加和组合内容

> 2026-10-18：本页保留旧 C# base 的教程／接口依据，所列运行路径已退出生产，当前不能照此执行。纯 DSL 开发请从[开发指南](base-development-guide.md)与[包格式参考](../../dsl/core/0.3/package-reference.md)开始；原功能需按迁移任务恢复。


文档类型：操作指南（How-to）。本页面向已有规则集的扩展作者，说明如何选择扩展方式、验证一张独立卡，以及处理依赖和冲突。卡牌语法查[base/0.0.2 作者参考](../../dsl/0.0.2/authoring-reference.md)；规则库设计查[可维护规则集指南](maintainable-rule-sets.md)。

首次新增卡牌从[第一张卡教程](first-base-card.md)开始；本页用于完成已有明确目标的内容组合及安装边界查询。

## 选择已实现的扩展方式

| 需求 | 入口 | 范围 |
| --- | --- | --- |
| 为通用core规则库选择参数或组合模板 | `imports` / `use`，见[配置规则库](../../dsl/core/how-to-configure-game.md) | core/0.2；不是base卡牌声明语言 |
| 在base机制上写新卡 | 独立命名空间 `.spire`，生产 `SpireCardSource` 前端和下方卡牌工具 | 验证已有能力的内容组合；受控战斗 |
| 编写独立Engine规则包 | `EnginePackageBuilder` 和 `RuleSetBuilder`，见[开发者指南](../core-the-spire/developer-guide.md) | 明确Schema、依赖、扩展/覆盖和投影；冻结契约仍适用 |
| 安装完整STS mod并走公开整局 | 对应mod集成工作包 | 卡牌工具未实现自动发现、覆盖加载或完整Run安装；范围见主清单ENV-T16 |

本页的base例子将独立命名空间卡牌定义合入base作者模型，仍由base规则包组装执行。它证明内容可组合，不表示已经建立独立插件包管理器。

## 从文件验证一张新增卡

前置条件：在仓库根目录操作，安装.NET 9；需要验证本文全部例子时另需Python。工具使用实际加载的base能力，不用规范ID表假装能力已注册。

1. 构建卡牌开发工具：

   ```powershell
   dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj
   ```

2. 读取并检查[双击卡示例](../../examples/base-card-mod/two-hits.spire)：

   ```powershell
   dotnet run --no-build --project tools/base-spire-dev -- check docs/examples/base-card-mod/two-hits.spire
   ```

   结果应为 `declared/parsed/lowered/registered` 各1，ID为 `example-mod:two-hits`。`registered` 表示Engine规则包构建成功且受控牌组已创建对应实例；没有行为或原版对拍含义。

3. 验证普通和升级出牌：

   ```powershell
   dotnet run --no-build --project tools/base-spire-dev -- play docs/examples/base-card-mod/two-hits.spire example-mod:two-hits normal
   dotnet run --no-build --project tools/base-spire-dev -- play docs/examples/base-card-mod/two-hits.spire example-mod:two-hits upgraded
   ```

   普通两击3+7使目标HP从38变28，获得格挡2；升级4+9使HP变25，获得格挡3。均付1能量、回答一次目标决策并进入弃牌堆。

4. 创建自己的内容文件，显式声明 `dsl_version = "base/0.0.2"`，用 common 共享字段、normal/upgraded 整字段替换，把命名空间和卡名改为本mod的名称。每个效果写自己的参数。再次执行 `check`，使用返回的规范ID执行 `play`。只改外部 `.spire` 文件时无需重新构建工具或base；修改C#机制时重新构建。

5. 需要验证这一工作流的失败分支时，运行已构建工具的例子检查：

   ```powershell
   python tools/base-spire-dev/verify_examples.py
   ```

   检查外部修改后两击3+11=14、缺能力引用、未支持效果、同ID冲突、已加载能力的限定ID及不支持的内容域；任一错误退出非零。

## 使用稳定引用和显式依赖

新增内容使用自己的命名空间，跨包引用使用规范ID，例如 `power = base-spire:strength`。只有实际加载能力表能解析的引用才能参与执行；能在源清单里找到名字并不够。不要在新mod里复制一份base的伤害或回合实现来绕过缺机制。

已有Power可来自另一个卡文件或敌人文件，作者工具与正式运行时共用注册资源。能力牌打出后的默认结果为None，由共享结果模块移出战斗牌区；无需自写清理或用exhaust替代。真实无目标卡写 `target = { selectNone = {} }`，原生无效果可以保留空 `play = {}`，仍付款、计入历史并执行钩子；未知机制仍须报未支持。无目标卡不可读取 `context.target`，显式owner或全体敌人效果仍可使用。

独立Engine包先声明依赖，再按依赖顺序交给 `RuleSetBuilder`。C#入口使用 `Dependencies`、`Overrides`、`Extends` 明确声明意图；链接器校验冲突、覆盖和字段布局。依赖某个符号不等于获得任意修改base内部流程的稳定接口，扩展前需确认对应开放声明及契约。具体语法/API查core参考，不将这些属性套进base卡牌DSL。

卡牌工具只合并新增定义；同ID立即失败。需要覆盖现有内容时，应在独立规则包/正式规则包构建中明示覆盖语义和顺序，并验证所有受影响的内容、身份与消费者。不得靠文件枚举顺序或后写优先偷偷替换。

## 理解受控验证的边界

工具固定seed `20261001`，使用base现有敌人和过程，牌组只含探针卡；目标默认选第一个敌人，其他决策默认选第一个候选。它适合快速检查逐效果语义，复杂选择应写能区分分支的专用行为案例。

`upgraded` 从长期牌组创建完整升级牌，使用升级费用、变量初值、关键词与进战斗hook。已有实例升级另外走共享命令：变量与基础费用增加定义中的升级差额，保留既有成长和临时费用修饰，已升级实例不重复加差额。工具接受卡牌与Power声明；敌人/遗物等内容域仍拒绝。多文件输入先加载Power再链接卡牌，例如 `check cards.spire powers.spire` 和 `play cards.spire powers.spire mymod:card-name upgraded`。新Power使用已实现的行为可直接注册，不需添加C#内容ID白名单；相同名字在不同命名空间保留各自身份。卡牌通过同命名空间名字或跨包限定ID复用已注册Power。正式base发布继续走 `ContentSource` 和 `SinglePlayer` 共同工厂，卡池资格、完整Run、公开载荷、独立源对拍及性能需要对应门槛。

扩展新语义时，按[base开发修改入口](base-development-guide.md)做完整机制包。不要把新卡都塞进规则包构建器的逐牌分支。

## 组合共享牌区与出牌能力

[编排示例](../../examples/base-card-mod/orchestration.spire)定义 Spark 和 GenerateSpark。无需修改C#，普通变体生成一张免费 Spark，升级变体生成两张升级 Spark。先检查整份内容，再执行一个变体：

```powershell
dotnet run --no-build --project tools/base-spire-dev -- check docs/examples/base-card-mod/orchestration.spire
dotnet run --no-build --project tools/base-spire-dev -- play docs/examples/base-card-mod/orchestration.spire example-mod:generate-spark normal
dotnet run --no-build --project tools/base-spire-dev -- play docs/examples/base-card-mod/orchestration.spire example-mod:generate-spark upgraded
```

`check` 的四个数量均为2。`play` 只创建所选牌的受控初始牌组，`registered` 为1；生成牌数从出牌前后的 `handCount` 核对，普通最终1、升级最终2。目标不提问，能量不变。`verify_examples.py` 同时检查本例和双击例子的成功/失败分支。

这些原语复用共享命令，语义如下：

| 写法 | 含义与已实现范围 |
| --- | --- |
| `autoPlay = { from = draw count = 1 forceExhaust = true }` | 抽牌堆顶选牌，先收集整批再免费打出，单敌人目标从命名流选取，结果强制消耗；缺省 forceExhaust=false |
| `autoPlay` 的 `count = { scaled = { base = 0 perCount = 1 count = { energyX = {} } } }` | 次数取已付X快照；也可用其他已支持的类型化计数 |
| `drawUntil = { type = attack }` | 至少尝试抽一张，抽到攻击牌才继续；首张非攻击、空牌堆、NoDraw或满手终止 |
| `generateCards = { count = 1 pool = { card = Spark } cost = 0 }` | 从显式完整候选池生成战斗牌；满手进弃牌，cost=0免费至第一次打出或回合末，省略cost保留基础费 |
| `generateCards` 的 `upgraded = true` / `distinct = true` | 生成升级变体；distinct目前只支持字面 count=1，多张不放回取样明确拒绝 |
| `withCount = { name = n count = { pileCount = { pile = hand } } effect = { ... } }` | 进入块时保存一次计数，savedCount只能在该块及子块读，内层同名绑定不会覆盖外层 |

也接受 `pool = { character = IroncladCardPool }`：先严格解析完整85项单人源池，再按类型与生成资格过滤。其他角色池尚未接入，省略pool仍拒绝；不能用“现在已经降级的卡”代替原角色池。生成/自动出牌暂不允许放在hook里。完整声明/注册及本例不证明 InfernalBlade/Stoke 的独立原版一致性。

自动出牌嵌套仍走同一出牌过程，修改伤害、消耗或付款规则时不用在mod再复制一份调度器。新语义的读取范围和验证入口见[base指南](base-development-guide.md)，运行顺序见[流程说明](control-flow-modules.md)。
