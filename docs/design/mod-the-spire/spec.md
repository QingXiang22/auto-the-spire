# mod-the-spire specification

2026-10-18用户确认的目标：base是使用Engine完整DSL的规则库与Spire内容包；Spire是该DSL的受限子集。具体效果、生命周期和整局流程不长期依赖C#游戏规则生成器。旧 C# 领域模块已退出生产，只在 tools 迁移对照保存。当前包仅支持遗物抽牌开发 scope；正常 Run 与未迁移内容明确未支持。目录拆分不构成完整玩法验收。目标、现状及迁移次序见[当前开发资料](base-architecture.md)。

完整环境恢复的细则见[ENV实现约束](../environment-implementation-constraints.md)：同一manifest／Session、DSL单一内容源、权威状态／生命周期、完整房间与两侧case。旧SinglePlayer／ContentSource及本页后文C#编译模块属于历史实现依据，不是活动落点；当前结构查[base组织说明](base-architecture.md)，准确操作查[开发手册](../../development/base-spire/README.md)。禁止把旧Legacy／作者原型成果计入当前Engine或另造C#状态机。

> 2026-10-04当前优先级：用户要求先完成[ENV完整单人环境与源规则对拍](../environment-completion-and-differential-v1.md)。core提供当前原版基线所需通用执行能力，base用正式DSL/规则库实现全部单人可达玩法，net提供完整公开出口；新增AI实验待环境候选验收。目标契约不因旧切片验收而缩小。

本规范定义 `mod-the-spire` 的规则包职责、扩展方式与组合边界，遵循[项目整体规范](../spec.md)。接手时，先在[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)确认任务，再查相关章节。

## 1. 目标边界（2026-10-01）

`core-the-spire` 的目标是通用桌游引擎；`mod-the-spire` 承载游戏包及其扩展。`base-spire` 优先实现 STS，但不是 core 的必需依赖，也不拥有所有游戏必须使用的内置类型。具体提案见 [通用桌游引擎设计稿](../core-the-spire/generic-tabletop-engine-design.md)。

游戏包负责：

- 实体类型、字段、集合/关系、作用域和内容模板。
- 动作、选择、事件、数值修正、完整流程、胜负与观察政策。
- 游戏特有语法/规则库，将作者术语编译为通用 IR。
- 原版参数、奖励/地图/敌人行为算法和可选 Lua 计算资产。

对 base-spire，这包括 Card/Monster/Power、牌区、生命/格挡/能量、伤害/回合、地图/房间、奖励与终局；不仅是给 core 内置 STS 规则补卡牌名单。

## 2. 注册、扩展与组合

规则包声明稳定命名空间、版本/哈希、引擎兼容范围、依赖、Schema/程序与计算资产。依赖和包顺序固定后链接；同 ID 冲突必须显式覆盖，不从一个声明内部的后写覆盖规则推导跨包行为。

STS mod 可显式依赖 base-spire，扩展其开放的 Schema、内容、事件和修正阶段；其他游戏包不需要依赖 STS。修改公共字段类型/实例布局属于契约迁移，不能静默合并。运行期状态归会话，不能保存到共享内容定义或脚本全局。

## 3. Lua 与复杂计算

指定脚本使用通用 ComputeAsset 契约；包声明输入/输出、数值配置、预算和脚本身份。首版建议为纯计算，不直接改玩法状态或推进引擎；返回结果由规则 IR 使用。复杂算法的局部变量属于临时计算空间。

普通规则应能通过游戏包 DSL/程序组合表达；是否使用 Lua 由表达复杂度和总成本决定，不强制将所有玩法写成脚本。Lua 版本、绑定与具体执行 API 尚未选择或实现。

## 4. 当前实现与迁移

以下是 2026-10-01 设计时点的迁移背景，不能据此选择当前生产后端：当时 base-spire 已注册较多 STS 内容，并使用 core 内的 STS 类型；`.spire` 0.0.1 作者约定保持兼容。迁移计划是先迁通用存储/调度，再迁 STS 编译扩展和规则切片，逐步把专用语义从 core 移到游戏包。当前纯 DSL 加载按[迁移规格](engine-dsl-migration/spec.md)执行，完整环境合同继续保留。

当前 base-spire 完整性目标仍为恢复源码快照的单人玩法闭环；其他游戏包用于证明通用性，不把现有内容注册数量当作完整规则等价证据。迁移验收区分解析器接受、实际加载、行为检查和源规则差分。

每批迁移交付来源版本/定位、基础与升级效果、目标域、触发顺序、随机算法/候选池顺序与消费位置的覆盖表。`reference/sts2-sim` 是辅助参考，不替代 `sts2/`；无版本回放、源码恢复缺口和有意偏离单独标注。修正 golden 期望必须有独立规则依据，保留差异记录，不能把参考后端互相一致当作原版保真。

游戏包同时声明观察政策与可见信息更新；net 的机制覆盖表引用这些来源与行为证据，AI 不自行重放规则。公共 Schema/动作/事件/随机契约变化同时记录旧/新语义、编译结果、迁移文件和验收，不因旧 DSL 能解析就宣称已迁到通用引擎。

2026-10-03迁移边界：现有`.author`是通用IR作者原型/预设状态的测试场景入口，正式作者约定仍为DSL 0.0.1；两解析器分读不同文件和旧后端继续加载不等于正式作者源已迁移。新机制可复用该原型，但生产路径需将实际0.0.1内容在游戏侧lower到Engine并给出加载/行为证据，避免长期维护两份内容。冻结旧后端用于兼容/对照，切换默认入口须等当前范围全部完成。当前任务见[相关规格](../spec.md)。

参考：[ironclad-dsl-coverage.md](ironclad-dsl-coverage.md)。

## 5. 永久状态写回矩阵（ENV-T02）

要求出处：[ENV实现约束](../environment-implementation-constraints.md) 的"写回矩阵必须标清字段来源/提交点/失效点"。
玩家战斗生命副本的提交点是[SettlementCompilation.Settlement](../../../tools/base-migration-baseline/BaseSpire/Combat/Compilation/SettlementCompilation.cs)声明的 `CommitPermanent` 过程，它把玩家**战斗副本**的 `Hp`、`MaxHp`
整体写回整局 `Run` 实例。`SettleBattle`负责房间幂等提交，`BattleEndTail`生成外层动作的结算尾；下一房的副本重建由`BattleRoomCompilation.RoomTransition`生成。完整模块分工见[编译模块参考](package-compilation-reference.md)。资源的归属与提交如下：

| 状态 | 权威归属（非战斗时） | 战斗副本来源 | 提交点 | 重建/失效点 |
| --- | --- | --- | --- | --- |
| `Run.Hp` / `Run.MaxHp` | 整局 `Run` 实例 | `StartNextRoom` 从 `Run.Hp`/`Run.MaxHp` 复制到战斗玩家 | `CommitPermanent`（由 `SettleBattle` 在同一间房的幂等守卫内调用；`BattleEndTail` 是动作的最后一条指令 ⇒ 写回的是跑完全部效果与收尾之后的完整战斗副本） | 换房时 `StartNextRoom` 用整局值覆盖战斗副本（漏写回会被抹掉） |
| `Run.Gold` | 整局 `Run` 实例 | 战斗内没有来源 | 不写回（矩阵里显式不参与） | — |
| `Run.SettledRoom` | 整局 `Run` 实例 | 无副本 | `SettleBattle` 的幂等键（每间房只写一次） | 换房递增 `CurrentRoom` 后重新武装 |
| `Run.RewardTaken` | 整局 `Run` 实例 | 无副本 | `UpgradeLongTermCard`（与结算分开，按房间至多一次） | 换房后重新武装 |
| 长期牌组 `LongTermDeck` | 整局集合（每张牌一个长期实例） | `StartNextRoom` 逐张 `CreateEntity` + `CopyCardFields` 复制出**新的**战斗实例 | `UpgradeLongTermCard` 按**实例**写长期实例（参数就是所选长期实例；`DefinitionId` 只用于取升级变体的数值） | 战斗牌区在换房时整体销毁；战斗实例的临时字段（如 `CostModifier`）按矩阵清零，不回写长期 |
| 战斗副本 → 长期来源 | 每张副本一个**隐藏引用字段** `Card.LongTermSource` | `CopyCardFields` 在复制时写入，指向被复制的那张长期实例 | 关系本身不改状态（永久变化按它定位长期实例） | 副本销毁时随之消失；同定义的不同实例各有各的来源，**不按 `DefinitionId` 合并** |
| 遗物 / 药水 | 整局 `Run` 作用域实体 | 起始遗物 Burning Blood 的胜利回血在结算**之前**改战斗副本；药水使用后销毁实例 | HP变化随 `CommitPermanent` 生效，药水栏跨房间保留 | 药水完整生成元数据与栏位见[药水契约](potion-inventory/spec.md)，使用支持单独记录 |
| 敌人、能力、牌区、`NextMove` 等 | 无（纯战斗态） | 战斗内创建 | 不写回 | 换房时全部销毁并重新建立 |

**余效与终局时序**：胜利判定只挂在**动作体末尾**（`BattleEndTail`：`PlayCard` 的 `playBody`、`EndTurn` 的
`endTurnBody`、`UsePotion` 的 `useBody` 各一处），既不在伤害步骤里、也不在效果步骤表里，因此"最后一次击杀"
之后的父效果余效（Feed 的 `on_fatal_gain_max_hp`、遗物回血、实例变量写回）、条件分支、`after_card_played`
钩子、`InPlay` 移除、`consumeOnPlay` 消耗、消耗区摆放与结果区处理都先跑完，才 `SettleBattle` → `CommitPermanent`
→ `SetTerminal`。针对性检查见 `BaseSpire.Slice.Tests/Cards/SliceFrontendSettlementChecks.cs`（源关系、条件分支末杀、
消耗牌末杀三条各自钉住一类收尾）。
