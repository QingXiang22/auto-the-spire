# 能力与伤害修正参与者

关联CORE-AUTHOR-05／**DSL-POWER-MODIFIERS-01**；状态与认领只在[GitHub任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)维护。玩法采用[纯DSL](../base-architecture.md)，规则源见[锁定参考](../../../../reference/README.md)。

## 已核实来源与范围

Power由所属内容模块登记定义并注册实例监听，调用方显式传definition、持有者、层数和可用来源，不传Kind。共享创建／改量／移除、定义类型、实例模式、临时寿命与死亡清理见[Power参考](../../../areas/base/reference/powers.md)。源目录中的条目不等于已加载定义，不用旧迁移数量或空定义表示当前支持范围。

攻击使用当前Attack／DamageResult流程：加法、乘法、格挡吸收、生命结果与通知分别处理，不能把Bash／Uppercut的正文伤害和随后施加Power倒置。现有攻击修正仍使用四位整数中间量；完整decimal精度由伤害领域独立校准，格挡组件通过不代表伤害全量对齐。

双方格挡使用同一命令：Dexterity按牌源所有者或无牌时的实际目标贡献加法；Frail仅乘目标的powered Move；Unmovable排除怪物目标并按其他play的正Move格挡历史判断。加法、乘法完成后才非负截断和取整，不能在Frail之后提前丢掉分数。现有无加成入口的Unpowered且无Move组合绕过这三个修正。实际修正者在计算后只回调一次，前后通知保留零量、分数、来源和旗标，详见[双方格挡约定](../combat-turn-resources/spec.md#双方格挡命令)。

## 行为要求与设计

| ID | 约定 | 判别 |
| --- | --- | --- |
| PW01 | 定义决定负值和实例匹配策略；shared同持有者同定义合并，其他模式分别处理 | 重复施加、正负增量、独立和按施加者实例不串状态 |
| PW02 | 攻击按源来源与加法／乘法顺序生成DamageResult，正文伤害先于后续施加Power | Bash和Uppercut的伤害、存活／致死及后续能力分别核对 |
| PW03 | 计算保留目标、牌源、play和旗标，现有无加成数值不误用powered修正 | MercuryHourglass不受Strength加值，Anchor不受Dexterity，怪物Move受本体Frail |
| PW04 | 群体施加遍历准确的可作用目标，每个实例独立执行完整Power命令 | Shockwave作用于全部存活目标，确认死亡者不被重新施加 |
| PW05 | 公开观察遵循字段与关系可见性；查询不改状态、版本、随机或层数 | 实际公开轨迹、重复Observe／LegalActions及隐藏计算上下文检查 |
| PW06 | 内容模块拥有自身行为，共享流程不按内容ID执行玩法 | 修改真实消费者并保留源池、原用例和独立参考，不恢复旧C#回退 |

## 验证

复用现有Power改量、回合、敌人和正常公开路径。格挡独立比较应区分早取整、零量遗漏、按实际状态差通知、错误旗标、错误持有者、合并多次得挡与修正者重复回调。组件、正常路径与完整环境分别验收，命令与实际结果维护在PR／CI，不以静态登记或旧向量代替行为验证。
