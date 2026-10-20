# 纯 DSL 原版随机机制

关联 CORE-AUTHOR-05／DSL-RNG-RESTORE-01；状态见[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)。来源为本地固定 sts2 的 MegaRandom、Rng、RunRngSet、PlayerRngSet、StringHelper、ListExtensions、CardModel.CompareTo 与 CardRarityOdds。

## 通用能力与内容职责

core 复用显式选择的 xoshiro256**／SplitMix64 及 vanilla-uniform@1 映射，旧 engine-default 的抽样行为不变。新增 DSL 的 gameSeed()、hash32(text)、seedStream、sampleFloat32 和 binary32 运算表达式；所有随机消费仍由唯一 RandomService 计数／快照。binary32 用 Int 保存 IEEE754 位型，绕开 Fixed 的量化；比较／加法在显式 binary32 操作中完成。该表示不是可直接相加的普通整数。另修正原版布尔抽样为 NextInt(2)==0，对已有兼容 profile 的布尔结果构成语义变化，因此引擎标识迁移到 engine/ir@0.5，core 包版本为0.5.0；旧0.4规则须重建，旧回放不能冒名复用。

gameSeed 为不可变会话输入，公开入口保留请求原文（bootstrap 语义），不隐式 trim、大小写或大厅标准化；默认无字符串输入时使用数字种子的 invariant 十进制文本。hash32 是352654597双累加器对UTF-16 code unit 的 unchecked 32位哈希，返回0..4294967295。流种子必须在首次消费之前设置，重复声明拒绝；快照保留尚未抽取的显式种子。非法范围不得消费随机数。

包 manifest 选择通用 vanilla-uniform@1 profile，包会话工厂在工具与Host共用；Host只传字符串seed和选择包声明的profile，禁止自行实现游戏流公式。默认文本规则和旧engine-default包保留FNV会话seed及默认随机行为。

## base 流布局

runSeed=hash32(gameSeed())；run流种子为(runSeed+hash32(snake_case枚举名)) mod 2^32；全部12条源run流显式声明。playerSeed=(runSeed+零基槽位) mod 2^32，Rewards／Shops／Transformations同公式。当前公开单人槽位为0，内容级种子过程接受entry／slot／mixin，并对总和模2^32；entry应为源大写下划线标识，不能用kebab内容ID代替。源未消费的流不提前抽样。

初始战斗牌组按永久顺序复制后直接UnstableShuffle（降序Fisher-Yates、n-1次NextInt）；不发AfterShuffle。弃牌重洗先按源ModelId.Entry与升级级别排序，再用同一Shuffle流洗牌；发AfterShuffle后才抽牌。初始与重洗共享游标，空／单元素洗牌不消费。源 List.Sort 不承诺等键稳定性，DSL 显式 sort(..., stable=false) 使用同一 .NET 排序族，不以实例ID打破平局；十张起始牌的实际来源实例顺序已有独立向量，其他运行时版本及大型重复牌组未据此验收。

卡牌随机选择／生成／目标使用对应run流；药水掉落、金币、药水稀有度与选取、卡牌稀有度、选奖励牌和升级共用player Rewards，不拆出第二条同源流。源NextFloat先NextDouble再binary32舍入；稀有度偏移按binary32累加，保留负阈值与严格小于。首幕升级抽样保留小于等于零边界。未迁移的Run初始化前置消费不补抽、不伪造结果。

## 验证与边界

复用只依原版源码的Python神谕，扩展UTF-16、binary32、流布局、初始／重洗及奖励向量，固定来源hash。实际DSL执行与向量逐项比较数值、候选、来源实例及游标；验证流隔离、快照恢复含未消费种子、非法输入与观察不抽样、公开同seed相同／不同seed变化且静态标识不含seed。旧默认profile回归不变。

本次恢复随机机制和已迁移消费者，不宣称完整Run前置消费、地图／Neow／全内容权重、Gaussian、完整源hook或整局独立参考差异测试已完成。RewardsSet 的药水前置判定与完整首幕 A0 普通奖励现已接入显式访问上下文；独立源向量逐次核对完整序列及48项药水候选顺序，但正常 Run 前置消费和完整模拟器双侧仍未验收。原金币／卡牌局部向量继续仅验证局部操作；RubyRaiders 的遭遇内容流仍为 development-encounter，需真实楼层上下文后恢复。未支持的参考版本差异仍按原合同保留。
