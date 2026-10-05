# core-the-spire specification

2026-10-05 冻结基线为 engine/ir@0.3，保留 core/0.1 与 core/0.2 语言。启动顺序和枚举域已收口，兼容边界、集合/生命周期与维护规则见 [冻结契约](../../contracts/core-engine-baseline.md)及 [关闭报告](../../review/archived/core-contract-freeze-2026-10-05.md)，优先于下文历史限制。

2026-10-04 当前实现：活动 core 只使用 Engine；core/0.1 ANTLR 基础编译与 core/0.2 DSL 规则库/参数实例化、深冻结、职责拆分、版本化随机注入及有界对拍调试模式已交付，开发者 Session API 保留。当前[架构决定](core-architecture-decisions.md)、[作者入门](../../dsl/core/README.md)和[交付报告](../../review/archived/core-engine-followup-2026-10-04.md)优先于下文历史状态；完整原版与 IDE 插件仍需独立验收。下文 Legacy 兼容章节保留历史背景。

2026-10-05 边界澄清：core 的验收和冻结以已承诺的通用原语、DSL 组合与确定执行契约为准；具体死亡余效、胜负时机、原版内容/整局和对拍属于游戏包及 ENV，不能作为核心独立冻结前置。详见[架构决定第10节](core-architecture-decisions.md#10-2026-10-05通用核心边界与独立冻结条件)。

当前实施细则见[ENV实现约束](../environment-implementation-constraints.md)：目录/依赖、唯一调度器、启动配置、数值/随机和诊断接口均需落实到实际入口。下文历史支持数与迁移前类型不作为当前完成门槛。

> 2026-10-04当前优先级：用户要求先完成[ENV完整单人环境与源规则对拍](../environment-completion-and-differential-v1.md)。core提供当前原版基线所需通用执行能力，base用正式DSL/规则库实现全部单人可达玩法，net提供完整公开出口；新增AI实验待环境候选验收。目标契约不因旧切片验收而缩小。

本文件是 `core-the-spire` 模块的 specification，遵循项目整体 [specification](../spec.md)。2026-10-01 用户已澄清：目标是能描述多类桌游的通用引擎，STS 是首个游戏包。具体架构、DSL/IR、Lua 计算和迁移提案见 [通用桌游引擎设计稿](generic-tabletop-engine-design.md)。目标边界已确认；下文目标与兼容实现说明保留设计时点，当前已有独立Engine及base-spire受控内容消费者，迁移范围尚未闭合。

2026-10-03实现提示：Engine当前受控检查64项通过；正式0.0.1卡/能力前端位于base-spire游戏侧，复用通用AST并lower为Engine IR，不把STS语法语义放回core。Legacy仍编译/运行兼容范围。新路径部分完成与完整迁移分别看[全项目复核](../../review/records/2026-10-03/project-completion-review-2026-10-03.md)。

## 目标架构：通用桌游运行时

- core 提供类型化 Schema/实例、集合与关系、作用域、纯查询、动作验证、决策暂停/续接、规则程序调度、类型化事件、随机数、观察与版本契约。
- 卡牌、怪物、Power、牌区、生命、格挡、能量、地图、房间、回合、伤害与胜负条件由游戏包声明。core 的公共类型和执行分支不包含这些领域语义。
- 一个会话只有一个调度器推进状态；脚本和 native 扩展不重入 Step/Drain。只读查询和修正上下文不提供玩法状态写接口或 RNG 消费。
- 复杂计算允许指定 Lua 资产。首版建议通过通用计算后端接口实现显式输入到类型化输出，结果再由规则程序结算；不把 Lua 局部变量注册为持久字段。
- DSL 局部槽、计算临时空间与玩法状态分开。Lua 是否节省开销通过包含数据传递的实际测量决定。
- 通用性先由小型非 STS 游戏包验证；首批顺序决策与 STS 单人实现不能当作全部桌游/同时决策已支持的证据。
- 引擎内部状态恢复契约与外部协议能力分开，保持 AI/net 的首阶段 NoSL 与无公开 rollback/load/fork 约束。
- 纯程序能力沿调用图校验；有限循环按迭代帧推进，事件级联和实例/候选创建计入结算预算，超限显式故障。公开投影按公开键排序并接受不泄漏/公开事实覆盖两方向的审计。
- PRNG 与种子派生算法、包/内容/脚本及配置摘要进入规则集身份。STS 保真以 `sts2/` 版本化来源为准，旧 core/参考模拟器仅作差分对照；不能仅重录 golden 期望来消除失败。

## 当前实现与目标的区分

现有 `Content.cs`、`State.cs`、`Environment.cs` 和 `Runs.cs` 包含 STS 专用定义与流程；这些是兼容期实现事实，不是允许继续固定在 core 中的目标边界。[现有运行态模型](runtime-state-model.md) 描述它们的归属，后续按设计稿迁到游戏包规则与通用执行模型。

## 1. 现有 STS 兼容实现：聚焦单人的行为逻辑

迁移前的现有实现以及首个 STS 游戏包以**单人玩法**为目标建模并以此校验：一个玩家角色参与的战斗与整局流转。判定标准是单人视角下的行为逻辑正确、可复现、可观察。下列多人相关约定仅描述这个兼容实现及 STS 包范围。

### 1.1 保留部分多人相关的接口

为与原版数据结构保持一致，并避免以后为多人重做骨架，下列结构与内容保留：

| 保留项 | 现状 |
| --- | --- |
| 玩家集合 | 运行态持有玩家集合，而不是把玩家身份铺成全局字段；当前实现只要求单人战斗（[Runs.cs](../../../archive/legacy-sts/core-the-spire/Core.TheSpire.Legacy/Legacy/Runs.cs)） |
| `AnyPlayer` 类来源目标 | 保留在原版目标语义中，单人会话语境下投影为当前玩家（药水 `AnyPlayer` → `self`，见 [content-dsl.md](../../dsl/0.0.1/content-dsl.md)） |
| `multiplayerOnly` 内容 | 仍然注册，但不进入单人卡池、也不是单人合法动作（Demonic Shield、Tank，见 [ironclad-cards.spire:4](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/ironclad-cards.spire#L4)） |
| 多人专属机制的数据位 | 保留定义与可观察字段，但不实现其交互行为（例：Tank 的 Guarded 与队伍伤害分摊，见 [ironclad-dsl-coverage.md](../mod-the-spire/ironclad-dsl-coverage.md)） |

### 1.2 多人交互行为可以从略

下列行为不在本模块的建模范围内。遇到相关分支时按单人投影处理并记录，而不是为多人交互补齐状态机：

- 队友选择、同时选择与多人决策同步
- 玩家之间的伤害分摊、格挡与治疗效果分配
- 跨玩家的共享资源、共享牌区与跨玩家随机流
- 依玩家数量变化的数值缩放（例：Skittish 的多人格挡层数缩放，当前未覆盖，见 [sts2-core-source-audit.md](../sts2-core-source-audit.md)）

### 1.3 对多人相关内容的底线要求

多人相关内容只要求做到三点：注册不失败；单人视角下行为可判定（可打出 / 不可打出 / 不出现在池中）；观察投影不出现队友字段或空洞字段。

## 2. 不属于当前 STS 包模拟范围

账号进度、解锁、成就、多人游戏、界面、音频与平台服务不属于当前 STS 模拟目标（见 [项目 spec](../spec.md)）。core 的通用状态/动作契约仍允许其他游戏包表达自己的参与者和玩法数据；渲染、音频与平台服务由宿主承担。

## 3. STS 兼容期待确认

- 多人专属内容长期保留注册（而不是直接删除）是否作为固定约定。
- 是否需要显式的单人／多人模式开关，还是默认只有单人一种规则集。
- 观察层是否需要显式声明"无队友字段"，以便以后接多人时不破坏既有观察契约。
