# 卡牌出牌核心（费用、牌区与起始 Strike／Defend）

关联 CORE-AUTHOR-05／**DSL-CARD-PLAY-01**；实施与认领见[主清单](../../../review/tofix/README.md#当前交付）。本包接在伤害之后，按[纯 DSL 规格](../engine-dsl-migration/spec.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)编写；状态只在 GitHub Issues 维护。

## 已核实来源与范围

从 `sts2/src/Core/Models/Cards/StrikeIronclad.cs` 与 `DefendIronclad.cs` 核对两张 Ironclad 起始牌：Strike 为 cost 1、`DamageVar(6m, ValueProp.Move)`、`CardType.Attack`、`TargetType.AnyEnemy`、`OnUpgrade` 加 3；Defend 为 cost 1、`BlockVar(5m, ValueProp.Move)`、`CardType.Skill`、`TargetType.Self`、`OnUpgrade` 加 3。两者都在 `IroncladCardPool`。

本包实现开发态出牌链：`Card.DefinitionId`／`Cost`／`Upgraded`、`InPlay` 牌区、`CardPlayed` 与 `CardPlayedOnTarget` 事件、共享出牌过程（校验战斗与手牌、按费用扣能量、Hand→InPlay、派发监听、InPlay→DiscardPile）以及值升级过程。内容用两个模板加正式参数迁移 Strike 与 Defend。

不迁移：原版目标选择（公开决策选敌）、X 费、费用修改、消耗／虚无／保留／固有、出牌次数限制、卡牌类型与稀有度字段、Power 牌、卡牌获得途径（奖励／商店／事件／开局牌组）。开发 scope 的单体攻击解析“第一个可命中开发目标”，不是玩家选择；开发授予动作直接把卡放进手牌。

## 行为要求与设计

| ID | 契约 | 判别 |
| --- | --- | --- |
| CP01 | 出牌只在战斗活跃且有合法手牌时允许；先校验费用，再扣能量、把卡移出手牌进入 InPlay，监听完成后进入 DiscardPile | 能量不足时公开候选消失，过程直接调用也在改状态前失败；拒绝后手牌与弃牌堆不变 |
| CP02 | 卡牌效果监听在卡仍位于 InPlay 时运行，最后才移入弃牌堆 | 监听内查询 InPlay 能看到该卡；出牌结束后 InPlay 为空、弃牌堆新增一张 |
| CP03 | 攻击牌通过 `CardPlayedOnTarget` 携带解析出的敌人；无目标时不提供攻击候选 | 没有可命中目标时 `PlayAttackFromHand` 不是合法候选 |
| CP04 | 出牌只影响本次打出的卡，其它手牌与状态不变 | 两张手牌打出一张后，另一张仍在手且身份不变 |
| CP05 | 值升级只改实例的 `Upgraded` 标记，模板据它选择数值；定义级变化不迁移 | 升级后的 Strike 打 9、Defend 得 8；升级后该实例离开升级候选 |
| CP06 | 内容文件仅 use 模板参数，源资格与定义、578 源分母不变，不新增 C# 效果分支 | cards catalog 只新增两个非空 definition，且资格从 unreviewed 更新为审阅结论 |
| CP07 | 公开轨迹通过真实 stdio 授予卡牌、开始战斗与回合、提交攻击候选 | 能量 3→2、目标 40→34、回合号与格挡字段保持 |

不迁移的边界必须在验证与文档中与“已支持”分开：正常牌组、抽牌到起始手牌、原版目标选择与公开决策应答均未交付。

## 实施与验证顺序

1. 先补牌面状态与 InPlay 牌区，再实现共享出牌过程与事件，最后接两个内容模板。
2. 在包外追加出牌顺序、费用拒绝、牌区隔离、升级数值与目录分母检查；为两张卡各自生成见证。
3. 通过真实 stdio 验证公开轨迹，更新共享操作参考与领域资料。
4. 最后运行 solution 构建、包行为／覆盖、受影响 net 与迁移边界、文档检查；不改 Engine 语义，不运行独立参考或完整游戏矩阵。
