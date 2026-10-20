# 当前 base 控制流程与模块

文档类型：解释（Explanation）。本页按纯 DSL base `0.7.0-dsl.40` 描述现行职责。调用签名见[行为操作参考](../../development/base-spire/reference/card-behavior.md)，来源顺序与限制见[迁移约定](content-behavior-migration.md)。旧 C# CardPlayPipeline 等模块不进入生产。

## 当前调用链

```mermaid
flowchart TD
    Manual[手动动作／支付一次] --> Effects[ResolveCardEffects：捕获出牌序列]
    Auto[自动批次先预取／ResolveAutoCard] --> Effects
    Effects --> Record[每次重复独立 CardPlayRecord]
    Record --> Before[BeforeCardPlayed／BeforeCardExecution]
    Before --> Body[ExecuteCardBody 唯一绑定的有序正文]
    Body --> Attack[独立 Attack／逐击 DamageResult 批次]
    Body --> Other[选牌／资源／牌区命令]
    Attack --> Attach[附魔／负面附着阶段]
    Other --> Attach
    Attach --> End[完成记录／AfterCardExecution／AfterCardPlayed]
    End --> Repeat[其余重复执行]
    Repeat --> Result[FinishCardPlay 一次结果区提交]
    Result --> Restore[恢复父上下文／外层胜利检查]
```

图省略具体存活门禁；准确条件以 DSL 为准。付款、原始 X 与结果区属于出牌序列，每次重复另有执行标识；一次执行可以发出多次攻击，一次攻击可以有多段与多目标。直接伤害不因当前正在出牌就自动取得卡来源。

## 职责与入口

| 行为 | 主位置 | 分工与边界 |
| --- | --- | --- |
| 支付、重复、上下文与结果区 | [play](../../../mod-the-spire/base-spire/cards/rules/play.spire) | 唯一正文分派、显式 play、父执行、支付与结果快照；兼容 CurrentPlay 保存／恢复 |
| 正文、基础变量与私有能力 | 所属卡牌文件、[定义模板](../../../mod-the-spire/base-spire/cards/rules/card-definition.spire) | 每定义绑定 ExecuteCardBody 和 ReadCardDamage；外部 Before／After 通知不收集主效果 |
| 攻击与结果 | [attacks](../../../mod-the-spire/base-spire/combat/attacks.spire)、[damage-results](../../../mod-the-spire/base-spire/combat/damage-results.spire) | 每击先提交本批目标，再结果通知、死亡通知；直接群伤有独立DamageBatch，DamageDepth／AttackDepth／PlayDepth共同保护外层结算 |
| 抽牌与自动批次 | [draw](../../../mod-the-spire/base-spire/cards/rules/draw.spire)、[autoplay](../../../mod-the-spire/base-spire/cards/rules/autoplay.spire) | 抽牌复用阻止、容量及重洗；自动批次先收集，嵌套批次不抢父成员 |
| 费用、牌区与回合末手牌 | [costs](../../../mod-the-spire/base-spire/cards/rules/costs.spire)、[piles](../../../mod-the-spire/base-spire/cards/rules/piles.spire) | 独立费用来源／寿命，区分移动、主动弃牌与耗尽；动态关键词与整批 flush |
| 永久牌、复制与附魔 | [permanent-operations](../../../mod-the-spire/base-spire/cards/rules/permanent-operations.spire)、[attachments](../../../mod-the-spire/base-spire/cards/rules/attachments.spire) | 永久／战斗副本分开；复制、升级与移除协议；依附投影保护隐藏牌标识 |
| Power 与其他能力监听 | [Power生命周期](../../../mod-the-spire/base-spire/powers/rules/lifecycle.spire)、[统一监听约定](effect-listeners.md) | 显式来源、定义匹配策略、实例通知与纯修正；完整 StackType 仍未承诺 |
| 资源、访问与奖励 | [resources](../../../mod-the-spire/base-spire/combat/resources.spire)、[visits](../../../mod-the-spire/base-spire/events/rules/visits.spire)、[rewards](../../../mod-the-spire/base-spire/rewards/index.spire) | 共享完整领域调用；访问拥有页面／状态／返回位置，普通事件不直接改 Player.Room |
| 回合、历史及战斗结束 | [lifecycle](../../../mod-the-spire/base-spire/combat/lifecycle.spire) | 开发状态门禁和阶段通知；新战先清理来源历史及临时能力，未恢复正常 Run 结算 |

所有可变状态、局部变量、挂起点和续接均在同一 Engine Session。Host 只负责声明派生、候选应答与公开投影，不编排第二份玩法。

新增内容先复用对应领域命令；存在新源语义时补约定与一个能区分错误的消费者。当前目标与未验证范围只在[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)维护，不把开发范围通过记为完整原版一致。
