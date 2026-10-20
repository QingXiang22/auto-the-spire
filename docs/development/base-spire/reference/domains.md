# 领域目录与开发范围参考

文档类型：参考（Reference）。下表按游戏职责覆盖 base 的开发范围。落点为长期[目录设计](../../../design/mod-the-spire/base-architecture.md)；实际存在文件以[包 manifest](../../../../mod-the-spire/base-spire/package.json)为准，未迁移目录不建立空效果来占位。

| 领域 | 定义／共享规则的落点 | 关键依赖与阅读 |
| --- | --- | --- |
| 卡牌本身、普通／升级、颜色、状态／诅咒／衍生 | cards/schema、rules 与角色／来源组下单内容文件 | 费用、目标、牌区、共享效果；[卡牌指南](../how-to/cards-and-combat.md) |
| 卡牌附魔／负面附着／动态关键词 | cards/rules 的实例扩展与独立内容配置 | 复制／升级／变换／跨战写回，不混进永久定义 |
| 战斗资源、格挡、伤害、Fatal、死亡 | combat/schema、block、damage、turns；跨域最小类型才放 shared | 修改者、来源、余效与结算边界 |
| Power | 共享实例在combat/schema.spire、定义／生命周期在powers/rules；通知、修正和私有状态在powers/shared所属模块 | 持有者、施加／叠加／衰减／移除；与遗物共用实例订阅；[物品与 Power 指南](../how-to/powers-relics-potions.md) |
| 遗物 | relics/schema、rules、shared／角色配置 | 获得／移除／替换／熔化、计数与生命周期 |
| 药水 | potions/schema、rules 与单内容配置 | 完整源池、动态容量、使用／自动触发、消费与清理 |
| 敌人 | enemies/schema、rules、来源幕／shared 配置 | HP、意图、行动状态、死亡、召唤；[敌人指南](../how-to/enemies-and-encounters.md) |
| 遭遇／编队 | encounters 静态来源与对应 DSL 规则 | 完整资格、源顺序、编队抽样和槽位初始化 |
| 角色、进阶与开局 | characters、run 的初始化与规则 | 起始状态、牌组、资源、原版随机初始化；[Run 指南](../how-to/run-and-rooms.md) |
| 地图、问号房型、队列与整局推进 | maps、run | 地图图结构、访问、候选、房间进入退出、持久状态 |
| 战斗房、商店、营火、宝箱、未知房 | rooms 下各房型的独立文件／索引 | 通用访问生命周期加各自局部状态与选择 |
| 问号事件与先古之民 | events、characters／run 的 Ancient 依赖 | 资格、页面／选项、效果、子战斗与返回 |
| 战斗／事件奖励与候选生成 | rewards、源数据所属领域 | 抽样／显示／领取／跳过、满容量保留、未选清理 |
| 永久牌组、长期资源与跨房写回 | run 的权威持久状态；领域操作消费 | 战斗实例与永久实例映射，原子结算与只写回一次 |
| 幕转换、Boss 与终局 | run 的幕生命周期与显式终局规则 | 剩余效果、奖励关闭、最终胜负与完整观察 |
| 公开目录与隐藏信息 | public/projection.spire、metadata.json | 同一 RuleSet 派生观察／候选；不承载玩法 |
| 随机与池顺序 | 对应领域 DSL 的抽样操作，静态源池记录 | seed 派生、独立流、调用顺序、原版布局仍需迁移 |

`cards/catalog.json` 等静态源目录保存 id、name、source_path、source_sha256、pools、eligibility、evidence、definition。`run/catalog.json` 记录 Act 源对象；它不是地图／房间的可执行注册表。`shared/source-pools.json` 保留源池及帮助成员，`encounters/source.json` 和 run 的静态快照不执行算法。

实际可复用的过程仅以[操作参考](operations.md)列出的定义为准。跨多个领域开发时，先选拥有状态／生命周期的领域为主要归属，再记录调用关系，避免在每个消费者复制机制。
