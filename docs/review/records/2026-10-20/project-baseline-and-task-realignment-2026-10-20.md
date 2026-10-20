# 项目实现基线与任务校准记录

本记录说明2026-10-20文档与派单调整的依据，不维护新的当前任务表。当前认领、优先级和下一包只见[主清单](../../tofix/README.md#当前交付)。用户要求核对实际实现后更新清单和文档；本次仅修改文档，未认领玩法编码、恢复旧会话、解除暂停项或关闭ENV/W。

## 审计基线与范围

本地基线为`bfeb51e81fdb09c184e90dbf569facb19116e671`，分支`codex/content-behavior-migration-20261019`。2026-10-20通过QingXiang22Bot查询[PR #26](https://github.com/QingXiang22/auto-the-spire/pull/26)，仍为OPEN、未合并；其四组CI已通过，见[工作流运行](https://github.com/QingXiang22/auto-the-spire/actions/runs/39373089544)。本次作为该迁移PR的文档校准，不声称main已经具备此基线。

检查覆盖项目／文档入口、唯一任务清单、角色与管理约定、base实施编排、ENV实施落点、core语言入口及base/net规格的现行边界。核对生产manifest、solution项目、领域规则、通用Host、独立适配入口和CI组。不是对全部历史报告或每件内容的重新验收；已明确过时的报告未作为当前能力依据。

## 实际实现依据

在仓库根目录执行当前开发工具的`describe`和`validate`，二者均返回0。加载原始DSL文件得到如下身份：

| 项目 | 实测／代码依据 |
| --- | --- |
| base | `0.7.0-dsl.40`；[manifest](../../../../mod-the-spire/base-spire/package.json) |
| Engine／语言 | Core.TheSpire `0.7.0`、`engine/ir@0.7`；文本语言`core/0.3`；[工程](../../../../core-the-spire/Core.TheSpire/Core.TheSpire.csproj)、[编译入口](../../../../core-the-spire/Core.TheSpire/Engine/Compilation/EngineDslCompiler.cs)及[活动契约](../../../contracts/core-engine-baseline.md) |
| 包摘要 | `114587163f8b31ccf67c1f23753049a5178414fcf1af0f2e6b7544471bd77f5e` |
| RuleSet／随机Profile | `f051bcaf225fe44d`／`vanilla-uniform@1` |
| 唯一支持scope | `base-spire-dsl-relic-draw-dev-v1`；原五个正常／完整scope全部明确未支持 |
| 生产组织 | [solution](../../../../AutoTheSpire.sln)与[base组织说明](../../../design/mod-the-spire/base-architecture.md)：Engine执行DSL，Host通用加载；旧C#base只作迁移对照，不进入生产 |

`describe`输出的迁移计数表示可执行定义登记，不能单独证明所有分支、正常来源或独立比较。卡牌／遗物的实际见证引用同摘要的[行为迁移记录](../2026-10-19/content-behavior-migration-2026-10-19.md)，本次未重跑其全套行为检查。

| 内容域 | 完整源数 | 当前定义数与证据边界 |
| --- | --- | --- |
| 卡牌 | 578 | 87；行为报告有87张见证，其中Ironclad单人85张、原角色池另有2项多人排除；不等于全卡完成 |
| Power | 260 | 34；定义数，不是完整StackType或全Power验收 |
| 遗物 | 297 | 58件有见证；1项源排除、238件未实现；正常获得来源仍未恢复 |
| 药水／敌人／遭遇 | 64／121／88 | 6／9／2；不代表完整生成池与正常队列 |
| 事件 | 68 | 2：SapphireSeed、DoorsOfLightAndDark开发访问；无正常事件池或子战斗返回 |
| 角色 | 8 | 1个显式Ironclad初态；原始源目录与五个标准角色验收分母分别保留 |
| 幕／源池 | 5／27 | 均为0个可执行迁移；静态池／计划数据不是正常Run能力 |

另外实际执行`dotnet run --no-build --project tools/differential -- test --suite regression --output .artifacts/docs-baseline-20261020/regression`，返回3：原17个ID全部unsupported、`executed: 0`、`independent_verified: false`。这与[活动适配器](../../../../tools/differential/Program.cs)一致；[CI migration组](../../../../tools/ci/README.md)验证失败语义和迁移边界，不执行独立比较。不能把切换前通过数搬到当前包。

公开接口核对[协议](../../../design/net-the-spire/protocol.md)与同基线迁移记录：实例单选／取消及Bool透传已交付，有序多选和其他不支持形状仍需真实Engine契约。正常地图／Run／跨幕、完整奖励前置消费、Neow／Ancient、事件子战斗和正常公开全程均未恢复。

## 偏差与派单调整依据

| 原偏差 | 影响 | 本次处理 |
| --- | --- | --- |
| 当前清单混入旧7事件、三幕地图、商店和角色专属机制 | A会接不存在的当前消费者，B/C会沿旧路线续跑 | 当前领域总览改用DSL实际能力；旧合同保留日期证据和完整验收，撤销其活动认领含义 |
| 最近批次仍列0.6.1、56件／240件及未确认发布身份 | 不能判断所审分支与验证结果 | 以同一manifest摘要与PR状态校准；旧阶段数字留records，不跨版本累加 |
| 下一包仍要求PenNib、Akabeko／Vigor、Sown | 重复已交付迁移 | 从下一实现包撤下，保留PR审阅及原目标残项；不据此关闭整组或完整环境 |
| G05精度调查被无条件排在所有内容前 | 未证明每件遗物都依赖该变化，造成整线等待 | 保留源decimal判别调查；只有实际反例及消费者受阻才升级对应数值迁移，不取消源精度要求 |
| 角色提示词要求旧SinglePlayer／ContentSource／Slice和旧工厂 | 接手即走入冻结代码或旧SDK | 改为manifest、领域DSL、包外检查和通用Session；ENV落点表同步修订，旧实现只作迁移依据 |
| C直接承担“多选公开”，未区分Engine选择与wire | 可能造游戏专用协议，或C先做无消费者接口 | 顺序改为A确定选择合同→通用Engine实现→C通用透传→RELIC-G02真实消费者；现有单选／取消／Bool不重做 |
| 任务状态分散在当前交付、RELIC排期、旧会话详情 | 同时出现多个下一步或假在制任务 | 状态／P值／当前认领集中到当前交付；RELIC分组改为消费者与依赖目录，旧会话不恢复 |
| 总入口把旧base/0.0.1／0.0.2及C#房间文档当当前教程 | 作者使用当前工具不接受的语法／命令 | 当前操作导航改到人类手册与core/0.3；旧作者参考明确历史边界 |

下一生产候选选择RELIC-G06的主动弃牌／耗尽反应，依据是现有DSL已经声明`CardDiscarded`、`CardExhausted`，并有共享无加成格挡和直接群体伤害批次。只读核对本地源`ToughBandages.AfterCardDiscarded`及`CharonsAshes.AfterCardExhausted`：前者还需牌拥有者与当前行动方资格，后者对可命中敌人造成直接无加成伤害，不是Attack。源码未复制进仓库。Tingsha另涉及CombatTargets随机目标，UnceasingTop涉及手空稳定边界，不能按同一模板盲目合并。该选包是依赖分析结论，尚未实现或验收这两件遗物。

独立比较恢复可先在工具侧选一个当前DSL已有消费者对应的原case，不等待全部Run；若源执行或输入域不相容，登记具体依赖，不能为了首个绿例改原ID／维度。完整17项、11份suite、最终120对、五角色、进阶和原时间保护均保留。

## 文档职责复核

按Diátaxis区分当前使用文档与管理／证据文档，本轮不为分类搬动历史正文：

| 文档 | 主要用途／发现 | 修订结果 |
| --- | --- | --- |
| 项目与docs入口 | 导航；夹带过时能力统计与派单 | 当前支持与状态指向清单，作者按教程／操作／参考／解释进入 |
| base实施编排 | 实施计划，不强行归四类；旧落点与方法混用 | 方法改为DSL；源算法与完整验收保留，排期不另维护 |
| base组织说明／行为操作参考 | 分别为Explanation／Reference | 继续作为当前结构与准确接口入口；完整重构目标不冒充已实现清单 |
| 旧卡牌作者参考／C#房间指南 | 历史Reference／How-to，不能直接运行 | 明示历史状态并从当前作者路径移除；不删除原验收意图 |
| 主清单／角色／管理约定 | 项目管理，不强行归四类 | 唯一状态表、文件责任与接手方法分开；目录责任不等于执行认领 |
| 本记录及既有records | 带日期证据 | 保留当时结果，不用记录的下一步覆盖当前排期 |

本地私有接手指令仍含旧版本和旧顺序，本轮保留其已有修改，未将该文件提交或复制到公开文档。接续依据是本次用户要求及已更新的公共清单；不能通过检出旧冻结标签恢复当前能力。

## 验证与保留范围

本次实际运行describe／validate、regression未支持检查、文档检查和差异检查；原始输出位于本地`.artifacts/docs-baseline-20261020/`。未重跑Engine／base／net全量套件、正常Run、独立原版比较、120对矩阵或AI兼容性检查。已有四组CI仅证明迁移基线对应范围，不能代替上述未执行项。

主清单ENV-T01～16、CORE维护项、T06／T09原语子任务、完整ENV／BASE合同、W-01～08、里程碑与暂停触发条件继续保留。任务重排不改变完整产品目标；旧source计数有快照差异时保留来源，当前完整卡牌分母以catalog的578项为准，不按历史577项缩减。

交付检查扫描800篇Markdown、0问题，`git diff --check`通过；核对修改前后的稳定ID无缺失，21段完整完成条件全部保留，20个遗物分组全部保留。其中两段完成条件仅校准17项regression分母与旧Slice检查的迁移执行入口，未删除原case或公开／独立比较要求。全文修改均为Markdown，本次不改变包摘要或生产代码。
