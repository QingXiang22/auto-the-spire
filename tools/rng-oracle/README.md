# 锁定来源的随机向量

本工具供随机机制与base内容维护者重新生成独立期望。当前规则依据为[参考锁](../../reference/baselines/sts2-sim.json)指定的sts2-sim顺序随机路径；参考checkout保持只读。生产base仍通过DSL执行。

## 重新生成

先按[参考准备说明](../../reference/README.md)准备干净checkout，再从仓库根运行：

```powershell
python -X utf8 tools/rng-oracle/refresh_locked_vectors.py --output .artifacts/rng-vectors
```

生成器校验commit、tree和未提交改动。它先运行只依赖锁定Sts2Sim.Core的[原生导出程序](reference-vectors/Program.cs)，再生成固定测试数据、开局池DSL和核心哈希向量。原生程序不引用本项目Engine或base，不读取生产运行结果；Python消费者神谕使用独立XxHash64与既有xoshiro映射。来源SHA256统一使用UTF-8字节并将CRLF规范为LF，向量以`source_hash_encoding="utf8-lf"`声明，避免Git检出换行方式改变摘要。重新运行应得到相同纳管文件。

| 产物 | 独立来源与检查范围 |
| --- | --- |
| normal-opening-vectors、reference-pool-metadata | 原生RunState、Player、Neow，完整遗物袋、三幕来源队列、候选资格与顺序；固定Overgrowth／Hive／Glory，不声称随机替代幕选择 |
| act-one-map-vectors、map-gaussian-vectors | 原生Overgrowth与StandardActMap，最终节点／类型／边／游标、真实GetAllMapPoints后接独立起点／Boss的公开序列与位置，以及Gaussian拒绝抽样 |
| normal-combat-vectors | seed777正常Neow选择BoomingConch，首个合法后继真实遭遇、HP、首招、手牌与抽牌堆 |
| neow-effects-vector | seed777真实Neow选择WingedBoots／CursedPearl后首战；另列WingedBoots三次非连接入房与两seed正负初始Strength的FlexPotion受控组件，保存资源、实例顺序和随机四字状态／游标 |
| slimes-vectors | 十组原seed，原生SlimesWeak累计楼层2的受控编队、HP、三回合行动、Slimed与游标 |
| ironclad-source-metadata | 原生90项完整池及GiantRock；五项多人资格、基本／升级属性与来源摘要 |
| source-rng-vectors | Python独立派生，15条流、初始／重洗、局部金币／卡牌；保留原seed并增加old前缀边界 |
| normal-reward-vectors | Python独立完整首幕A0普通奖励，三次访问、48项药水池和每次抽样的候选／结果／游标 |
| enemy-move-vectors、pickup-upgrade-vectors | Python独立招式图与永久升级随机；分数权重为明确的算法探针 |
| Word64Vectors.cs | 原生StringHelper的UTF-8哈希输出；生成时同时核对Python哈希及根seed分支 |

局部生成器可使用各自`--output`参数；其来源池读取先由原生导出得到的reference-pool-metadata，要求锁一致。`normal_opening_vectors.py`只渲染已导出的来源定义，不再维护第二套开局算法。

## 验证与限制

```powershell
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests -c Release
dotnet run --project tools/base-spire-dev -c Release -- check --rng-only
dotnet run --project tools/base-spire-dev -c Release -- check --enemy-moves-only
dotnet run --project tools/base-spire-dev -c Release -- check --card-rewards-only
dotnet run --project tools/base-spire-dev -c Release -- check --normal-run-only
```

所有向量保留实际来源标识与摘要，不能将旧来源SHA改名后当新版证明。`vanilla_rng_oracle.py`保留旧32位布局与原始数值算法的历史回归；当前消费者改用`locked_rng_oracle.py`，只复用未变的原始xoshiro／有界抽样算法。旧source-catalogs清单不能证明当前版本全内容行为符合。

`normal-unknown-room-vector.json`由原生生成器独立走完新增seed `unknown-normal-354`的GoldenPearl选择、首战自然胜利与奖励跳过，再通过RoomFactory进入首次问号Monster；保存实际遭遇、生命、编队、四类赔率位型和完整问号流状态。它与原有十组seed及seed777路径同时保留，不作为完整Run比较的替代。

`normal-event-selection-vector.json`由同一独立生成器执行seed `event-normal-11` 的BoomingConch、首战自然胜利及奖励跳过，通过真实RoomFactory选择问号Event后停止，不进入事件效果。它保存最终事件标识、游标／已访问集合、资格使用的进入前楼层、生命／金币、四类赔率及完整问号流状态。migration校验其来源摘要；base正常组消费向量，事件效果未连接时必须明确失败。

`normal-sapphire-seed-vector.json`独立执行seed `sapphire-normal-79` 的真实Neow、首战自然胜利、奖励跳过和SapphireSeed两个分支，保存完整永久牌顺序／定义／升级／附魔、生命／金币、进入楼层、选择游标、地图后继与完整问号流。EAT使用参考默认选牌策略的最后一个合资格实例，PLANT按来源自动附魔首个实例；两侧效果均不消耗额外随机数。base正常回归消费该文件，migration核对锁定来源摘要。

`normal-dense-vegetation-vector.json`独立执行seed `dense-normal-30` 的真实Neow、首战胜利及奖励跳过，自然进入DenseVegetation，再走TRUDGE_ON、REST后子战斗奖励跳过／领取三条路径。向量保存进入、选择、四Wriggler出生、胜利奖励和关闭边界的资源及完整Run／玩家／事件RNG四字状态与游标，同时保留永久牌组和真实后继。TRUDGE_ON范围为61～99，源NextInt上界100不含；战后生命包含BurningBlood的胜利治疗。base正常组及行为报告消费该文件，migration核对来源摘要；stdio沿公开策略验证访问生命周期，出牌策略差异不用于断言同一战后生命。

`battleworn-continuation-vector.json`独立执行seed `777`／`dummy-continuation` 的真实BattlewornDummy升级档。受控初态为Ironclad起始牌组、50HP及直接EventRoom；击杀路径在第三回合将真实目标设为5HP并生成Feed，超时路径真实结束三个敌方回合。向量保存原父事件恢复前后资源、永久升级索引及niche／shuffle／rewards／事件流完整四字状态与游标；base行为检查消费，migration核对来源摘要。该组件场景不证明第三幕正常可达，stdio另以开发公开入口验证真实超时和父子关系。

这里验证组件与指定正常路径；完整M01公开状态、实例、事件与逐次随机比较仍由[差异工具](../differential/README.md)验收。不得换seed、缩池、补抽、重键参考或把缺消费者当作通过。

`draw-permission-vector.json` 从锁定原生实现导出受控 NoDraw 的实际查询、阻止者与手牌变化：效果抽牌被拒且不耗洗牌随机，回合手牌允许，移除与到期恢复。base 行为组消费此向量，migration 校验锁定来源摘要；它不代替正常 Run 的逐边界比较。
