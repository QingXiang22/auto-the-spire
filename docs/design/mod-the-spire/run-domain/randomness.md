# 纯 DSL 原版随机机制

关联 CORE-AUTHOR-05／DSL-RNG-RESTORE-01；状态见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。目标来源为[锁定 sts2-sim](../../../../reference/baselines/sts2-sim.json) 的顺序随机实现。根与具名流使用新版 XxHash64／ulong 布局；历史 hash32 向量仅证明其原来源回归，不能作为新版期望。

## 通用能力与内容职责

core复用显式选择的xoshiro256**／SplitMix64及vanilla-uniform@1映射，默认engine-default抽样行为不变。`engine/ir@0.9`／Core.TheSpire 0.9.0提供gameSeed、hash32／hash64、uint64Add、ordinal文本前缀与UTF-16字符串截取、显式流种子和binary32／binary64操作；所有随机消费仍由唯一RandomService计数与快照。浮点和无符号64位均用Int保存位型，只通过对应操作解释。具体操作见[作者参考](../../../dsl/core/0.3/authoring-reference.md#随机流与-binary32)。

gameSeed 为不可变会话输入，公开入口保留请求原文（bootstrap 语义），不隐式 trim、大小写或大厅标准化；默认无字符串输入时使用数字种子的 invariant 十进制文本。hash32 是352654597双累加器对UTF-16 code unit 的 unchecked 32位哈希，返回0..4294967295。流种子必须在首次消费之前设置，重复声明拒绝；快照保留尚未抽取的显式种子。非法范围不得消费随机数。

包 manifest 选择通用 vanilla-uniform@1 profile，包会话工厂在工具与Host共用；Host只传字符串seed和选择包声明的profile，禁止自行实现游戏流公式。默认文本规则和旧engine-default包保留FNV会话seed及默认随机行为。

## base 流布局

普通根种子为 UTF-8 XxHash64(gameSeed, seed=0)。原文以小写 `old` 开头时，仅根种子改为后缀的旧 UTF-16 hash32；`old` 本身保留空后缀语义，大写 `Old` 不走此分支。run 流为 `(runSeed + XxHash64(snake_case枚举名)) mod 2^64`，全部12条显式声明；单人 PlayerRngSet 直接使用根种子，Rewards／Shops／Transformations 同公式。内容级种子为 `(runSeed + slot + XxHash64(entry) + mixin) mod 2^64`；entry 为源大写下划线标识，不能用 kebab 内容 ID 代替。DSL 以 Int 位型保存完整64位字，通过 `uint64Add` 与 `seedStream64` 显式解释；不对位型使用普通有符号加法。未消费的流不提前抽样。

初始战斗牌组按永久顺序复制后直接UnstableShuffle（降序Fisher-Yates、n-1次NextInt）；不发AfterShuffle。弃牌重洗先按源ModelId.Entry与升级级别排序，再用同一Shuffle流洗牌；发AfterShuffle后才抽牌。初始与重洗共享游标，空／单元素洗牌不消费。源 List.Sort 不承诺等键稳定性，DSL 显式 sort(..., stable=false) 使用同一 .NET 排序族，不以实例ID打破平局；十张起始牌的实际来源实例顺序已有独立向量，其他运行时版本及大型重复牌组未据此验收。

卡牌随机选择／生成／目标使用对应run流；药水掉落、金币、药水稀有度与选取、卡牌稀有度、选奖励牌和升级共用player Rewards，不拆出第二条同源流。源NextFloat先NextDouble再binary32舍入；稀有度偏移按binary32累加，保留负阈值与严格小于。首幕升级抽样保留小于等于零边界。未迁移的Run初始化前置消费不补抽、不伪造结果。

## 验证与边界

[向量生成工具](../../../../tools/rng-oracle/README.md)先验证参考checkout与commit／tree锁一致且干净，再独立执行原生参考。原生向量覆盖共享与玩家遗物袋、三幕队列、Neow、首幕地图和Gaussian、seed777真实开战，以及十组SlimesWeak编队／HP／三回合。Python独立重写64位派生与消费者随机算法，保留金币／卡牌局部向量、完整48项药水奖励逐次抽样、拾取升级与招式图向量；根哈希与old前缀同时对照原生输出。所有当前向量记录锁定来源，旧source-catalogs仍只表示原迁移清单。

实际DSL逐项核对数值、候选顺序、实例来源及流游标。原十组seed、原20261021显式未支持回归保留；额外覆盖old／old777／oldold777／Old777。seed777经BoomingConch、真实SlimesWeak、起始牌组战斗、领取或跳过奖励返回原地图后继，仍属于指定正常路径。SlimesWeak累计楼层和初始化顺序见[Run规格](spec.md#战斗副本来源与结算写回)。

Ironclad新版原生池为90项，五项按源IsMultiplayerOnly排除，完整单人池85项、奖励池80项。Bloodletting／Cruelty／Dominate／Taunt的稀有度随来源迁移；ExpectAFight为3费，按正力量获得15+5×力量格挡，升级为16+8×力量，使用共享有加成格挡。Cascade的源X费用哨兵-1在DSL以IsX=true和非负Cost保存，支付仍取当前能量。

完整公共观察、事件轨迹、每步动作及每个随机调用的双侧M01比较由独立比较工单验收；这些组件与单路径检查不能代替完整整局验证。其他正常房间、未迁移Neow效果及后续遭遇继续显式失败，不替换池或补抽。RubyRaiders仍是development-encounter流的受控编队，不冒充正常累计楼层消费者。
