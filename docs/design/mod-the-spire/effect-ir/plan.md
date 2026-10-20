# 逐效果语义实施设计

依据[规格](spec.md)保留逐效果迁移的设计与接口背景。当前派单、认领和状态只在[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)关联的工单维护；[行为要求](../../environment-completion-and-differential-v1.md#env-t06)定义验收，不承担派单。

## 技术上下文与约束检查

以下描述纯 DSL 切换前的 C# 领域前端向同一 Engine IR 迁移时的设计依据，不是当前生产目录或新任务的修改清单。当前实现入口见[base 架构](../base-architecture.md)，具体改动边界、基线和独立 worktree 按执行工单确定。

## 设计决定

正式领域程序保存不可变效果树，每条叶节点持有自己的数值表达式、目标和引用；条件/重复与选择包含子程序。旧AuthorStep仅在诊断输入端翻译成同一类型化程序，不进入运行时步骤分派。卡级元数据/实例字段继续存在，效果常量和表达式独立携带。

效果编译器接受小而明确的 IR 依赖接口，生成同一个 Engine 的指令，不接触 Session 或运行期状态。所选目标在外层动作绑定；条件、资源查询及变量读取在正确的效果执行位置编译成表达式/指令。复杂机制可调用现有领域过程，不能在效果编译器里判终局或提交房间。

动作外层保留统一收尾：效果→适用hook→结果牌区/资源消费→胜负→永久提交。规则包构建器负责声明和链接，在明确接口上连接效果程序，不再解释新效果字符串。既有开发场景兼容部分逐步收敛，避免一次修改全部卡/敌人/药水作者接口。

## 修改入口与兼容边界

| 职责 | 实现入口 | 修改条件 |
| --- | --- | --- |
| 读取已有 DSL 叶参数、解析顺序/条件/重复及链接 | `SpireCardSource` / `Effects/SourceEffectPrograms.cs` | 新增语法或领域表达式；组合已有机制不需要改这里 |
| 不可变领域程序、逐效果数值/目标/条件 | `Effects/CardEffectProgram.cs` | 新增领域语义；不保存 Session 或玩法状态 |
| 程序到固定 Engine IR | `Effects/CardEffectCompiler.cs` | 新增效果的 IR 编译；不持有胜负、房间提交或动作注册接口 |
| 动态数值/条件到状态表达式 | `Effects/CardEffectExpressions.cs` | 新增已绑定状态的查询/条件；不缓存实时值 |
| 实际字段/过程/集合与编译器绑定 | `Cards/Compilation/CardCompilation.EffectState` / `CompileEffects` | 新增或尚未绑定的状态、共享过程；保持同一个 Engine 权威状态 |
| 付款、目标选择、hook、结果区及胜负收尾 | `Orchestration/CardPlayPipeline`与`Cards/Compilation/CardCompilation.CardPlay`；结算尾由`SettlementCompilation.BattleEndTail`生成，其他公开动作在`ActionCompilation`绑定 | 变更动作生命周期；不能由叶效果决定 |

`NormalProgram` / `UpgradedProgram` 非 null 的正式卡以领域程序为执行依据；`AuthorHooks.Programs` 同样保存逐钩子程序。已有 `NormalAmount`、`PowerId`、`Steps` 和 `Conditional` 保留显示与诊断摘要，修改这些摘要不改写正式程序。程序为空表示源块确实为空；未支持或引用缺失仍进入 `Unsupported`，不能注册空牌代替。

旧.author入口的程序为null，由LegacyCardAdapter在CardDefinition.FromSource中一次翻译为相同原语。正式程序的选择、移动、升级、复制均已使用类型化操作；选择及随机选择绑定实例后执行子程序，不再使用LegacyCardEffect/CardCommandEffect或专用步骤执行器。原语组合与特殊机制边界见[约定](../card-primitives/spec.md)。本设计不表示已迁移药水、敌人和Power的全部表示，也不代表ENV-T07文本规则库全部交付。攻击玩家的原语仍未支持，不能拿敌人伤害过程解释玩家引用。hook不允许所选敌人或暂停选择。

动态数值在每条效果处编译为当前状态表达式。X 的数值来源明确为 `PaidX`，读取外层付款捕获的局部值，能量改变后保持不变；正式效果不依赖显示 Amount 槽。所选目标和随机目标使用不同绑定，重复块内的随机效果每次执行各抽一次。进入战斗的 hook 根据复制后实例的升级标记选择变体。

公共组装由`Composition/CombatPackageComposer`安排，维护时按[编译模块参考](../package-compilation-reference.md)进入目标领域；新增共享字段按[现行 DSL 开发手册](../../../development/base-spire/README.md)接通初值、生命周期与消费者。

## 最小开发反馈

从外部文件验证卡牌组合，使用[卡牌modding指南](../modding-guide.md)中的 `tools/base-spire-dev`；修改DSL后无需重编译内容程序集。按职责定位文件和完成最终验收条件见[当前开发资料](../../../development/base-spire/README.md)。提取表达式与开发工具的设计约束见[维护成本改良](maintainability.md)。

仓库根目录运行 `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --effects-only`，只执行本包的语义反例、对照与边界检查。`--combat-only` 执行全部 Slice 检查中除地图检查外的项目，输出明确限定范围；默认仍执行完整验收条件，不自动跳过失败。

新增已有效果组合时，改 `各领域Content/*.spire` 和行为案例即可；新增原语时再依次修改领域前端、效果模型/编译器及必要的绑定。公开字段不变时，不要求 net 为效果组合新增注册特判。IR 改变会影响规则和公开构建摘要，消费者仍应从正式工厂取得标识。实际验证记录及尚未通过的验收条件见主清单链接的交付记录。

## 风险与验证

保留源顺序：条件前的目标选择与随机目标每次抽取不同；X费数值、卡实例变量、升级的字段复制和效果后的成长都有现有消费者。默认路径的 RuleSet 标识可能因 IR 形状变化而改变，golden 更新必须有完整行为检查和构建依据。

先将已复现反例写成可运行检查，再实现模型/前端/编译边界；只对受影响形状扩大模型支持。串行运行 solution build、Engine、Slice、net；用实际加载统计区分新增组合表达能力与原版覆盖。地图开局耗时本轮不处理。
