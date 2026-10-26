# 会话状态、执行帧与游戏生命周期

文档类型：解释（Explanation）。本页帮助内核与规则作者区分持久实例、执行上下文、随机和公开观察。当前实现为通用 Session；游戏对象和生命周期由 DSL 包声明。准确行为要求见[核心接口要求](../specs/core-engine-baseline.md)，调用方式见[开发者参考](../areas/core/reference/developer-guide.md)。

## 同一局中有哪些数据

| 对象 | 生命周期与职责 | 实现依据 |
| --- | --- | --- |
| RuleSet | 编译、链接后固定，可由多个会话共享；保存类型、规则、查询、动作和投影定义 | [规则与宿主](rules-and-host.md) |
| SessionState | 每局独立保存作用域、实体实例、字段、集合成员与关系；根作用域由会话创建 | [StateStore.cs](../../core-the-spire/Core.TheSpire/Engine/StateStore.cs) |
| 执行帧 | 保存本次过程的参数、局部值、作用域、指令游标及调用者；过程返回后不作为持久游戏字段保存 | [Session.Frames.cs](../../core-the-spire/Core.TheSpire/Engine/Session.Frames.cs) |
| 待回答决策 | 发布本次请求的候选、范围及版本，暂停原调度栈；合法回答后从原位置继续 | [Session API](../areas/core/reference/session-api.md) |
| RandomService | 保存本会话的具名随机流、消耗与算法相关状态；重放取决于同一输入和调用顺序 | [随机服务](../../core-the-spire/Core.TheSpire/Engine/RandomService.cs) |
| 公开观察 | 由投影和权限从当前状态生成；隐藏实例、字段和内部诊断分别受可见性约束 | [核心观察要求](../specs/core-engine-baseline.md#3-状态组合与可见性约定) |

实体玩法字段使用 `Persistent` 生命周期。中间算术、选择结果和循环变量属于执行帧；`ExecutionOnly` 适用于帧、事件、终局及 compute 载荷，不能把临时计算伪装成隐藏的持久实体字段。

## 作用域控制存活，集合保存成员

Scope 决定实体及子作用域何时销毁。卡牌离开手牌而进入弃牌堆，通常只改变集合成员，不销毁实例；具体游戏清理时机由规则包定义。

当前集合按 Session 中的集合定义保存一份成员表，创建新 Scope 不会自动复制一套集合。多个玩家、房间或战斗需通过字段、关系、所有者与查询筛选区分。关系按 owner 实例保存连接；实例和作用域标识在会话内不复用。

必需引用可在同一构造过程暂缺，但命令结束、决策、终局、观察或状态哈希等稳定边界前必须指向存活的合法实例。销毁也不能留下非法引用。失败原子单位与故障语义按[核心接口要求](../specs/core-engine-baseline.md#2-执行选择和故障约定)处理，不能假设整个玩家动作自动回滚。

## 游戏中的 Run 和战斗由包建模

core 不内置 `RunState`、`GameState`、卡牌、Power 或怪物调度器。base 用声明的实体、字段、集合、关系和作用域表达长期玩家、地图访问、永久牌组、战斗副本及出牌上下文，均由同一执行器推进。

长期数据与战斗副本的对应、提交时点及实例关系属于游戏要求，不能由 Host 维护第二份权威状态。当前归属与边界见[base 规格](../areas/base/spec/overview.md)、[永久牌组与战斗副本](../areas/base/spec/permanent-deck-smith.md)、[Run 行为要求](../areas/base/spec/run-domain.md)及[控制流程](base-control-flow.md)。这些要求不等于完整 Run 已实现。

## 暂停与复现

暂停保留原调用栈、监听派发位置和局部值；回答后不重新执行已完成的初始化或规则。影响后续玩法的历史必须由包显式保存为状态；调试日志不能替代运行规则读取的事实。

从开局复现需要相同规则与算法版本、游戏 seed、语义动作及随机调用顺序。只保存种子和部分字段不代表已经保存执行栈与各随机流进度。开发者快照或诊断能力也不产生外部协议的保存、恢复、rollback 或 fork 承诺；公开接口要求见[协议](../areas/net/reference/protocol.md)。
