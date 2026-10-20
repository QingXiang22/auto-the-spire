# 开发敌人、意图与遭遇

文档类型：操作指南（How-to）。纯DSL包已有受控开发战斗；FuzzyWurmCrawler和TwigSlimeM使用共享招式图，其余支持内容以实际definition和行为测试为准。正常遭遇队列与Run尚未恢复，不能直接运行旧 `enemies*.spire` 或C#敌人编译器。

从 enemies/catalog 的 source_path 读目标敌人，从 encounters/catalog 与 source 快照确认编队。旧[行为要求](../../../design/environment-completion-and-differential-v1.md#env-t08)保留完整范围，具体动作机制依赖[战斗指南](cards-and-combat.md)。

## 建立敌人状态与意图

在 enemies/schema 定义敌人实例、参与者／阵营、生命、存活状态、行动状态及目标关系。共享战斗字段由战斗域拥有；敌人域只拥有行动机制和种类差异。固定定义与逐次行动状态分开，多个同定义敌人不能共用可变 history。

为目标敌人记录开场、状态机节点、分支条件、历史回避、意图与动作的源顺序。DSL 中先决定并保存下一行动，再让公开投影读取同一行动，不能显示一个意图却执行另一条逻辑。多击意图需明确每击修正与显示合计的关系。

固定和随机节点复用[共享招式图](../../../design/mod-the-spire/enemy-turn/spec.md#共享图的当前操作与失败方式)：内容文件组合EnemyMoveGraphDefinition、EnemyMoveNodeDefinition与EnemyMoveBranchDefinition，绑定ExecuteEnemyMove正文。TwigSlimeM示范固定首招与随机历史限制，FuzzyWurmCrawler示范源顺序固定轮转。每次生成通过SpawnDevelopmentEnemy初始化；观察读取Enemy.MoveId／MoveIndex／Intent，不能调用SelectNextEnemyMove生成意图。

把敌人动作写成共享操作组合，目标使用真实存活集合与源资格。行动、回合末衰减和切换意图的时点由战斗流程调用，不为每个敌人再写一套轮转调度器。

## 保留原版随机语义

先确定 HP、行动分支、编队与目标各使用哪个源随机流、seed 派生、调用顺序和候选顺序。Engine 能 shuffle 或 sample 不代表已具备原版布局。需要原版兼容时，约定必须给出独立依据及消费计数，不得在工具端补抽使答案对齐。

确定性行动不要额外消耗随机；空候选、死亡目标和分支被跳过时同样记录是否应抽样。用固定输入检查下一行动与流游标，再与真正独立参考比较；只检查两次自运行相同不能证明原版保真。

核对AddBranch的确切重载：TwigSlimeM的整数2是最多连续次数，默认权重1。共享图保留零权重项及源候选顺序，单个有效分支仍消费monster_ai；总权重为0明确失败。静态分数权重转成binary32，按源的double缩放和逐项float扣减顺序执行。条件节点和动态权重仍未支持；Whistle 已通过共享 `StunEnemy` 接入强制迁移，验证覆盖跳过、恢复、公开意图及同定义实例隔离。

## 编排遭遇与编队

静态 JSON 保存源敌人 ID、池和顺序；算法、资格与开场行为用 DSL。恢复每个遭遇时同时确认人数／槽位、随机编队的一次抽样结果、每个槽位出生／开场和对局状态。

将 weak／regular／elite／boss 资格与幕内队列放 encounter／Run 的共享规则。保持相邻重复／tag 回避、源回退与队列轮换，不能把暂未实现的敌人从池里删掉。抽到未支持内容应具名失败，既不替换弱怪，也不返回空战斗。

召唤、复活、逃离与行动控制是独立机制。源敌人需要它们时记录依赖；主清单仍暂停的机制不由本指南自动授权恢复。一个普通敌人可单独实现，不因召唤缺口阻挡所有其他敌人。

## 验证真实消费者

先验证一个敌人的完整行动循环，再验证真实 encounter 初始化和多敌流程。检查出生 HP、开场、意图、目标失效、行动 history、死亡清理与最后一敌终局；同定义两个实例必须独立。

新遭遇接入地图／战斗入口后，通过公开合法动作实际进入并完成一次战斗，再核对奖励及永久状态写回。受控单敌探针和正常多敌流程分列，静态名单存在不算敌人行为已迁移。交付后更新[真实操作参考](../reference/operations.md)与原回归去向。

本地定向运行 `dotnet run --project tools/base-spire-dev -c Release -- check --enemy-moves-only`；完整包检查仍运行全部卡牌、敌人及其他机制。源向量只证明标明的受控选招与随机范围，不能代替正常遭遇或模拟器独立比较。
