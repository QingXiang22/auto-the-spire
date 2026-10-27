# 新增拥有独有效果的内容模块

文档类型：操作指南（How-to）。适用于已有领域操作能够表达的新卡牌、遗物或Power。简单参数组合继续使用[已有模板配置](extend-content.md)；这里以已实现的SelfFormingClay和它的Power说明独有效果的组织方式，不能在同一包中重复注册这个示例。

前置条件：熟悉core/0.3声明和实例订阅，先确认[当前包scope](../../../../mod-the-spire/base-spire/package.json)以及[操作参考](../reference/operations.md)覆盖所需语义。catalog definition指向已加载的完整DSL内容模块。

## 1. 确认来源和状态寿命

用[定位助手](../../../../.agents/skills/base-spire-development/scripts/locate.py)查询工具固定数据中的source_path、source_sha256与exclusion，以及生产目录中的definition。原版内容复用既有来源ID，不能因为文件名看起来应该带`-power`就另造标识。Relic和Power属于不同目录，SelfFormingClay在两者中的ID均为`base-spire:self-forming-clay`。

把需要的触发、目标、数值、状态和结束条件写清。SelfFormingClay的遗物负责正未阻挡伤害后施加3层Power；Power负责回合开始的AfterBlockCleared阶段获得无加成格挡并移除。该事件包括首回合和保留格挡时，不能用“实际Block变成0”代替发布条件。

若领域事件缺少必要来源或结果，应先补约定与共享发布点。伤害监听使用携带DamageResult的BeforeDamageReceived／AfterDamageReceived等准确站点，读取显式dealer、card、play、属性与实际损失。不要在内容里猜出缺失上下文。

## 2. 在所属文件定义行为

一个内容文件使用同包namespace、core/0.3头部，在模板作用域中定义自身过程／订阅／私有实体，最后展开一次。完整例子是[遗物模块](../../../../mod-the-spire/base-spire/relics/shared/self-forming-clay.spire)与[Power模块](../../../../mod-the-spire/base-spire/powers/shared/self-forming-clay.spire)。

遗物的监听过程调用以下操作；这段应放在过程body内，owner来自事件载荷：

```text
call = {
  procedure = ApplyPlayerPower
  args = { owner = owner definition = "base-spire:self-forming-clay" stacks = 3 }
}
```

实例订阅过滤本次owner的Holder、匹配DefinitionId以及未熔化遗物，并以collectionIndex(Relics,listener)表达库存顺序。获得动作调用ObtainRelic；不要把触发效果仅写进获得动作。

Power在自己的模块内展开PowerDefinition，随后订阅所需通知或pure修正。定义决定基础策略，调用方不传kind。SelfFormingClayPower的Cleared先调用GainUnpoweredBlock，再经RemovePower移除自身；它不修改共享回合过程，也不让遗物负责清理Power。

可单独组合的选择、移动、生成、资源和结果操作进入领域rules，mod也可直接调用；当前消费者数量不决定行为是否进入公共库。模板负责定义绑定和参数配置，公共过程负责动作。只依赖本内容语义的私有状态与生命周期绑定保留在所属模块。准确标志、生命周期及失败语义查[Power参考](../reference/powers.md)。

## 3. 将私有状态绑定到实际实例

层数已经足够表示状态时直接使用Power.Stacks。需要额外计数时，参考[JugglingPower.State](../../../../mod-the-spire/base-spire/powers/shared/juggling-power.spire)：

1. 在模块模板内声明私有entity，其字段包含实际Power引用，字段设为Hidden。
2. PowerCreated订阅只接受本定义的本次power，创建附属实体并写入引用。
3. 修改／重置时按实际Power引用查询，不能只按DefinitionId查询所有同类实例。
4. PowerRemoved通知中清理附属实体。此时Power已离开Powers，清理监听应使用通知载荷与type查询，不能要求它仍在Powers中。

不要把专属计数放进公共Power／Player／CardPlayRecord，也不要使用Engine状态之外的C#字典。复制或战斗副本需要保留哪些私有状态，应另有明确约定；私有entity本身不自动定义复制语义；通过CardCloned／CardCombatCopied／CardRemoving等已支持通知接入，见[行为操作参考](../reference/card-behavior.md)。

## 4. 完成显式登记

以新增一个遗物和一个Power为例，生产登记如下；单个内容只需要对应领域部分。

| 文件 | 要做的事 |
| --- | --- |
| relics/shared/所属文件.spire；powers/shared/所属文件.spire | 各自承载定义与独有玩法 |
| relics/index.spire；powers/index.spire | 各增加一次所属文件路径，不添加运行时目录扫描 |
| relics/catalog.json；powers/catalog.json | 增加id与非空definition；来源hash、排除依据及完整来源条目保留在工具固定数据 |
| public/metadata.json | 在对应实体类型的content_ids中登记ID；Relic与Power分别登记 |

如果引入新集合，还需在公开元数据中明确其用途；隐藏定义集合PowerLibrary已有not_published登记。模板内部私有entity使用type查询时不需要额外集合。实体／字段／动作通过已有投影和Host派生，无需在net写逐内容效果分支。

catalog定义文件不承担start、imports、includes、version等包属性；装配属于入口／索引。生产JSON只存定义映射与必要公开信息，不能写效果程序。

## 5. 验证完整消费者

同一批内容完成后一次运行相关行为组。以修改SelfFormingClay独有规则为例，已有CLAY组检查获得、触发、兑现与生命周期边界：

```powershell
python tools/ci/check.py content --focus relic:CLAY
```

新增内容需要加入相应行为组，复用同机制参数化用例；CLAY或其他旧内容通过不能替代新内容的执行结果。共享边界已有机制检查时不逐项复制，仅为独有状态、触发或清理补必要判别。用[定向入口](verify-and-debug.md)选择本批实际相关组，完整base行为与覆盖报告由批次PR CI生成。本地不默认运行base／net／migration全套；共享接口或比较适配变化才扩大相关验证。修改文档另运行docs。按[批次规则](../../../../CONTRIBUTING.md#base-内容按批次交付)在同一PR交付一组内容，本页单个模块示例不要求逐项开PR。

SelfFormingClay的保留首轮共享时序修正与修正后的7文件接入。修改范围可供后续开发核对，不表示所有新机制都无需修改共享领域操作，也不证明正常Run或独立原版比较完成。
