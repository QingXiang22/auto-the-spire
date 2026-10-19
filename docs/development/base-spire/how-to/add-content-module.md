# 新增拥有独有效果的内容模块

文档类型：操作指南（How-to）。适用于已有领域操作能够表达的新卡牌、遗物或Power。简单参数组合继续使用[已有模板配置](extend-content.md)；这里以已实现的SelfFormingClay和它的Power说明独有效果的组织方式，不能在同一包中重复注册这个示例。

前置条件：熟悉core/0.3声明和实例订阅，先确认[当前包scope](../../../../mod-the-spire/base-spire/package.json)以及[操作参考](../reference/operations.md)覆盖所需语义。Core.TheSpire 0.6.1起，catalog definition可指向完整DSL内容模块。

## 1. 确认来源和状态寿命

按对应catalog查source_path、source_sha256、eligibility与definition。原版内容复用该行既有ID，不能因为文件名看起来应该带`-power`就另造身份。Relic和Power属于不同目录，SelfFormingClay在两者中的ID均为`base-spire:self-forming-clay`。

把需要的触发、目标、数值、状态和结束条件写清。SelfFormingClay的遗物负责正未阻挡伤害后施加3层Power；Power负责回合开始的AfterBlockCleared阶段获得无加成格挡并移除。该事件包括首回合和保留格挡时，不能用“实际Block变成0”代替发布条件。

若领域事件缺少必要来源或结果，应先补契约与共享发布点。现有PlayerHpLost只有owner，无法据此区分任意伤害来源。不要在内容里猜出缺失上下文。

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

多个内容确实共用同一行为时再抽模板进入领域rules。独有效果无需先建立一份只有单个消费者的共享模板文件。准确标志、生命周期及失败语义查[Power参考](../reference/powers.md)。

## 3. 将私有状态绑定到实际实例

层数已经足够表示状态时直接使用Power.Stacks。需要额外计数时，参考[JugglingPower.State](../../../../mod-the-spire/base-spire/powers/shared/juggling-power.spire)：

1. 在模块模板内声明私有entity，其字段包含实际Power引用，字段设为Hidden。
2. PowerCreated订阅只接受本定义的本次power，创建附属实体并写入引用。
3. 修改／重置时按实际Power引用查询，不能只按DefinitionId查询所有同类实例。
4. PowerRemoved通知中清理附属实体。此时Power已离开Powers，清理监听应使用通知载荷与type查询，不能要求它仍在Powers中。

不要把专属计数放进公共Power／Player／CardPlayRecord，也不要使用Engine状态之外的C#字典。复制或战斗副本需要保留哪些私有状态，应另有明确契约；私有entity本身不自动定义复制语义。

## 4. 完成显式登记

以新增一个遗物和一个Power为例，生产登记如下；单个内容只需要对应领域部分。

| 文件 | 要做的事 |
| --- | --- |
| relics/shared/所属文件.spire；powers/shared/所属文件.spire | 各自承载定义与独有玩法 |
| relics/index.spire；powers/index.spire | 各增加一次所属文件路径，不添加运行时目录扫描 |
| relics/catalog.json；powers/catalog.json | 更新已有源行的definition及资格依据，保留来源hash和完整源分母 |
| public/metadata.json | 在对应实体类型的content_ids中登记ID；Relic与Power分别登记 |

如果引入新集合，还需在公开元数据中明确其用途；隐藏定义集合PowerLibrary已有not_published登记。模板内部私有entity使用type查询时不需要额外集合。实体／字段／动作通过已有投影和Host派生，无需在net写逐内容效果分支。

catalog定义文件不承担start、imports、includes、version等包属性；装配属于入口／索引。JSON只存静态来源与公开信息，不能写效果程序。

## 5. 验证完整消费者

先运行validate确认加载、绑定、元数据和启动，再用真实操作检查获得、触发、层数合并、兑现、移除，以及不持有／失效／其他持有者／跨战边界。已经实现的SelfFormingClay可复查：

```powershell
dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj -v quiet
dotnet run --no-build --project tools/base-spire-dev -- validate
dotnet run --no-build --project tools/base-spire-dev -- check --relic-content-only --filter CLAY
dotnet run --no-build --project tools/base-spire-dev -- check --content-model-only
```

其他新内容需要自己的判别；CLAY通过不能替代它的行为证据。新增见证接入包外检查，批次最终使用完整check --report核对实际定义与执行见证。需要公开的新事实还应走真实Host轨迹；纯玩法复用不要求复制整套协议检查。

SelfFormingClay的[实际开发记录](../../../review/records/2026-10-19/content-model-migration-2026-10-19.md)保留首轮共享时序修正与修正后的7文件接入。修改范围可供后续开发核对，不表示所有新机制都无需修改共享领域操作，也不证明正常Run或独立原版比较完成。
