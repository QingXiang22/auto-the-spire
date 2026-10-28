# DSL 与独立参考比较

## 伤害数值与反应边界

`dotnet run --project tools/differential -c Release -- damage-compare --output .artifacts/differential/damage`执行两个seed的38项组件：负力量与Vigor、分数全挡／破挡／零伤／超杀、大值BodySlam、真实PenNib第十次乘法链、Hemokinesis自伤与Inferno／Rupture反应、致死自伤、Whirlwind同击双杀和Thorns反击致死后中止后续击及双方嵌套死亡。PenNib通过九次真实付费Strike准备；场景初值由工具设置，不能当作正常Run。

报告保留每条结果的实际承受者／dealer、Move／Powered／Unblockable、格挡、实际失血、超杀和致死事实，以及伤害提交前、破挡、HP损失、给予、接收与死亡前的顺序；分数金额以10000为单位，群体回调同时观察另一目标HP。双方均实际执行同一场景，诊断开关核对各自完整状态及随机状态不变；只读probe不执行游戏效果。缺源、缺用例、截断和异常非零，差异返回1。成功仅证明所列组件维度与锁定实现一致，normal_run_verified始终为false，不替代11份原suite或完整M01；任意HP修正／转移／防死仍未支持。

## 战斗动作边界诊断

运行 `dotnet run --project tools/differential -c Release -- combat-boundary-compare --output .artifacts/differential/combat-boundaries`，从两个固定 seed 分别执行开场及完整`EndTurn`（含下一普通玩家开始）、付费 Strike、两张 Infection、Feed 末杀；另覆盖末杀同步／异步移牌、普通／升级及末杀Anger、免费自动Whirlwind、OneTwoPunch重复Strike、Corruption和Juggling普通／满手／Stomp克隆，共28项。第一张移牌时暂停的两张 Infection 已从日常清单移入明确命名的暂停调度专项：`combat-boundary-compare --paused-schedule` 只执行这两项已知 #147 差异，`check.py paused` 是本地入口，不进入 migration 默认组或 CI。`combat-boundary-self-test` 检查比较器拒绝缺通知、错阶段、错牌区、错支付、错招式、错 ending、零执行和重复动作返回。

这些是人工设置的组件：空永久牌组、真实 FuzzyWurmCrawler、双方生命设为1000，Feed 的敌人设为10HP。只读通知记录实际回合号、能量、当前通知实付量spent、生命、格挡、招式、ending、生成数、Stomp手牌费用、Anger克隆升级状态和五个有序牌区；暂停例分别使用 DSL 的行政选择与参考既有异步通知的未完成 Task，观察后各续接一次，不加入游戏效果或动画等待。暂停例只在专项运行，默认报告不包含或过滤其已知差异。Feed 停在房间结算前。报告同时保存双方原始事件／监听信息，以及锁定 Hook 的准确源码、行号、摘要、迭代入口、名单固定和资格检查位置，供逐通知核查；这些源码片段和局部观察不证明全部监听已对齐。

`first_difference` 保留从开场起的首差；`action_difference` 从已经配置好的动作初态比较，`return_difference` 单独比较真实返回值。这样开场的选招差异不会掩盖后续付款或回合末差异。配置牌区期间的 DSL 通知另保留在 raw 中；初始 HP 抽样与 niche、正常房间、重复定义牌的实例标识和完整事件比较不属于本探针。适配器不补执行额外的开始回合动作。

每例另跑关闭诊断的相同初态：DSL 核对完整 StateHash 和随机状态，参考核对自身精确战斗描述和随机状态；观察与候选查询也不得改变 DSL 状态。缺场景／字段、截断、缺参考和异常返回3；实际差异返回1，所比较边界全部一致才返回0。报告的 complete、independent_verified 与 normal_run_verified 始终为 false，不代替原 suites 或 M01 验收。

手动出牌的源入口使用同步 `CardPileCmd.Add` 移入 Play，它不发异步 `AfterCardChangedPiles`；扣费后才在正支付量时发 `AfterEnergySpent`。普通 `AddAsync` 则在整批移牌后通知。Anger和Juggling记录实际入场与两个生成通知；暂停批次的调度差异由 #147 处理，不过滤该事件以制造全轨迹通过。诊断分别记录这些实际站点，不能为了统一事件名给参考添加不存在的回调。

[锁定 sts2-sim](../../reference/README.md) 同时作为当前规则依据和独立执行的比较后端。生产随机派生与开局向量使用同一锁定基线，旧source-catalogs仅保留原迁移清单；一致只表示与该模拟器实现一致，不能单独证明客户端保真。

`regression` 的原 `strike-normal` 和 `random-reshuffle-unsupported` 使用当前生产 DSL 包和锁定的 `sts2-sim` 分别执行。Strike 通过，重洗保留真实首差；其余 15 个原用例及其他 10 份 suite 仍明确报告 `unsupported`。11 份原定义、必测 ID 和比较维度保持不变；完整 regression 返回 3，测试集的 `independent_verified` 为 false。

在仓库根目录准备只读参考，再运行比较：

```powershell
python tools/differential/prepare_reference.py
dotnet run --project tools/differential -- test --suite regression --output .artifacts/differential/regression
```

准备脚本按[参考锁定清单](../../reference/baselines/sts2-sim.json)获取公开上游的提交；已有检出必须符合 commit／tree 且无改动，否则拒绝覆盖。可用 `--source <已有纯上游克隆路径>` 离线准备。缺参考时仍可构建工具，执行比较明确非零；`-p:UseReference=false` 可验证未编入适配器时的失败。

定向检查使用 `--case strike-normal`。报告保留完整 `expected_case_ids` 并列出 `selected_case_ids`，单例 `passed` 与测试集完成分开：`complete: false`，退出码 1，不能代替完整测试集。`self-test` 执行真实双侧并验证比较器的缺数据、错误终点、随机消费及首差失败路径。`--suite-file <JSON路径>` 用于校验测试数据和复现定义错误，要求保留原必测 ID、注册一致和本例全部原维度；自定义输入的 `complete` 为 false，报告保存其 SHA256，不能宣称原测试集完成。

Strike 保持原受控语义：10 张起始牌组、手牌仅一张普通 Strike、两个敌人 HP 为 38／51、玩家 HP 为 80、能量 3，打向 `enemy:0` 后保持 running。原 `game_seed` 保留；DSL 使用该字符串配置源流，参考沿用原受控初始化的数值 seed 20261001。双方初始化消费分别记录，脚本消费必须实测为 0；不对初始化随机流作一致结论，不验证正常开局、敌人行动或整局。

比较在初态和动作完成边界进行：

- `actions`：可提交牌的语义槽位和绑定数、结束回合。DSL 的 `PlayTargetedCard`／`PlayCardFromHand` 与 `EndDevelopmentTurn` 对应原 `PlayCard`／`EndTurn`；开发设置动作不属于原动作投影。
- `decisions`：DSL 实际暂停的目标候选、数量及回答；参考由自己的合法目标 API 记录同一选择。
- `facts`：玩家 HP／最大 HP／格挡／能量、两敌人的 HP／最大 HP／格挡／存活与能力。
- `pile-order`：四个牌区的成员数量和构成，以及手牌／耗尽区顺序。原无随机用例不比较随机初始化的抽牌堆／弃牌堆顺序，后续随机用例须按自身原维度恢复。

报告保存双方完整边界、包摘要／规则标识、参考 commit／tree／程序集及适配器摘要和受控输入；发生差异时给出首差维度、路径、双方值、最后一致边界及复现命令。缺参考、缺用例、零执行、缺维度、错误终点、违反随机 Profile 的消费、异常和差异均非零。

## 问号房型受控组件比较

首幕事件选择另用 `dotnet run --project tools/differential -c Release -- event-selection-compare --output .artifacts/differential/event-selection`。两seed各21种受控资源／牌组／宠物／药水／遗物袋／楼层输入，逐项比较31种资格及40次选择的标识、扫描游标和最终访问集合；两侧查询只读且全部Run／玩家随机状态不变。真实Neow进入推进同一游标一次。migration严格要求42种场景及41个边界全部存在，`normal_run_verified`保持false；这不证明事件效果或父子战斗已接通。

新增正常seed `event-normal-11` 由base和真实stdio走BoomingConch、起始牌组首战胜利、奖励关闭，再从(2,1)进入(1,2)问号。锁定原生向量选中AromaOfChaos、游标3、生命75、金币99；base比较选择标识、历史、进入前楼层和问号流，效果未接入时明确失败且不创建空访问。stdio核对真实失败及其重复请求缓存，故障会话不公开隐藏选择状态。该路径与原777及Unknown→Monster同时保留。

正常SapphireSeed由独立原生生成器使用新增seed `sapphire-normal-79` 执行真实Neow、首战胜利和奖励跳过，再从(0,1)进入(0,2)问号。两个分支分别导出：EAT回复9后选升级（生成器沿参考默认策略选最后一张合资格牌），PLANT自动附魔首张合资格牌。base正常组比较完整永久牌实例顺序／定义／升级／附魔、资源、事件游标和问号随机状态，再通过正常关闭恢复源后继；net真实stdio沿公开候选策略验证同一事件生命周期与请求重放，不把不同出牌策略的生命结果混为同一轨迹。该固定正常路径不认证完整Run或M01；原四路径和完整public.snapshot／random.trace／events.trace要求保留。

正常DenseVegetation使用独立来源seed `dense-normal-30`，从(1,1)真实首战胜利、奖励跳过后到(0,2)问号，运行TRUDGE_ON及REST后的四Wriggler子战斗。固定向量覆盖普通奖励跳过／领取、父子访问关闭及(0,3)真实后继，保存各边界完整Run／玩家／事件RNG和资源；base正常组逐项比较，net真实stdio另验公开路径。来源生成器与摘要入口见[rng-oracle](../rng-oracle/README.md)。该结果只覆盖DenseVegetation结束父事件，不证明BattlewornDummy正常返回或完整Run比较完成。

运行`dotnet run --project tools/differential -c Release -- unknown-room-compare --output .artifacts/differential/unknown-rooms`。两个seed各执行普通、禁Shop、双倍未选增长、仅Event和禁Event五种受控输入；每种实际抽取18次，第12次后恢复base赔率并保留随机游标。逐边界比较实际房型、四类赔率的binary32位型、实际NextFloat(0,1)结果、游标与四字状态。负Elite不进轮盘，黑名单不重新归一化；资格和增长hook只用于组件输入，不代表对应遗物已迁移。

migration要求十种场景全部成功，`normal_run_verified`保持false。另由base和真实stdio检查新增正常seed `unknown-normal-354`：选择GoldenPearl，用起始牌组赢下首战、关闭奖励，然后从(3,1)进入真实后继(3,2)。首次问号自然判为Monster并消费下一场FuzzyWurmCrawlerWeak，原地图与访问MapType保留Unknown；固定原生向量核对生命、编队、赔率和完整问号流状态。向量由[锁定来源生成器](../rng-oracle/README.md)独立导出；原seed777和全部必测集保留。组件比较与正常前缀均不替代事件、后续房型或完整Run验收。

BattlewornDummy开发升级档由独立原生 `battleworn-continuation-vector.json` 对比两个seed的受控击杀／超时场景，保存同一父事件恢复、资源、永久升级索引及四条完整随机流；base行为检查消费该向量，migration验证来源摘要。击杀场景将真实敌人设为5HP并生成Feed，不能作为正常游戏路径；真实stdio另验证开发入口的自然超时返回。药水／遗物档、第三幕正常可达及完整Run仍未完成。

## Whistle受控组件比较

运行`dotnet run --project tools/differential -c Release -- whistle-compare --output .artifacts/differential/whistle`。两侧使用空永久牌组和真实CombatEngine，实际支付2费、造成33／44伤害并耗尽Whistle。场景覆盖首招前、待执行增益／攻击、随机招式、同定义双实例、目标死亡和重复击晕；逐边界比较生命、力量、招式／意图、已选历史、能量、出牌计数、Whistle手牌与耗尽费用，以及完整monster_ai状态／游标和shuffle消费。源StateLog不加入瞬态STUNNED，空招执行后恢复已选真实节点本身；恢复不重新抽分支。

组件初态在出生后将双方敌人HP设为1000、玩家HP设为1000；致死场景只把目标设为33。出生HP生成与niche不属于此项比较，没有重键、重设流或补抽。未消费的DSL流按实际配置seed只读投影初始状态，不创建或推进流。migration要求两seed共16个场景全部通过；normal_run_verified保持false，不能替代正常Run或完整行动控制验收。

## 固定轮转敌人组件比较

运行`dotnet run --project tools/differential -c Release -- enemy-rotation-compare --output .artifacts/differential/enemy-rotations`。七个既有敌人各自执行完整固定轮转、首招击晕及同定义双实例；Tracker另比较Vigor3、Thorns反击击杀攻击者、玩家致死，以及先移除Frail后的无Power致死。双侧使用真实怪物及普通回合，空永久牌组；敌人生命设置为1000，玩家生命通常为1000，专项显式改为100或3。反击专项只把首个Tracker设为3HP，并增加4层Thorns，保留另一个存活敌人。设置动作调用生产操作，参考源码与随机流不改动。

逐边界核对节点、复合意图、实例历史、生命／格挡、Strength／Vigor／Frail、按承受方分组的逐击结果，以及完整monster_ai状态和游标。初始HP抽样、正常遭遇、全局事件顺序不属于此组件范围，开发行为检查另核对一次Attack及前后通知各一次。

两seed共50个场景保留在报告中。玩家死亡后双方均移除Frail，ending期间拒绝消费敌人Vigor并保留3层；migration要求命令返回0、全部场景所有已比较边界相同。该结果只证明上述受控组件，不代表全部敌人、初始化或正常Run已验收。

## Power改量组件比较

运行`dotnet run --project tools/differential -c Release -- power-amount-compare --output .artifacts/differential/power-amounts`。两seed、玩家／敌人两类持有者与24种场景共96项，分别执行新建、叠加、直接Modify、零量、负Strength、负减益、到零移除、Artifact耗尽和临时力量到期；Given的非交换加法／乘法、通知顺序、嵌套、参与者失效与暂停续接另由base的`check --content-model-only`验证。

双方从真实FuzzyWurmCrawler战斗启动，永久牌组为空，生命显式设为1000。每次命令后比较双方按持有者排列的完整Power库存、层数、PowerType／StackType、持续时间跳过标志、真实施加者，以及生命与monster_ai／shuffle完整状态和游标。侧回合末场景显式调用双方已有通知入口，不伪装成正常整回合；出生HP与niche不属于此组件。测试DSL只调用生产Power操作，参考源码只读。

独立／按施加者两种模式使用明确的probe定义，分别与原生FlankingPower／StranglePower的实例策略比较；没有出牌、伤害或到期调用这些probe，不据此登记两项生产内容支持。来源场景使用真实卡牌引用与怪物施加者。migration要求全部96项、每条已声明命令及所有维度实际执行且一致，缺项或差异非零；normal_run_verified保持false。

## 回合阶段与监听顺序组件比较

运行`dotnet run --project tools/differential -c Release -- turn-phase-compare --output .artifacts/differential/turn-phases`。两seed共28项保留真实Orichalcum＋Plating、同定义双遗物、Charon普通耗尽末杀／非末杀／重洗、BloodVial与Lantern起手、Run与Direct监听顺序，以及Hellraiser嵌套末杀时FeelNoPain先后获得、OrnamentalFan末杀格挡、BagOfMarbles首侧易伤施加者、FiendFire自耗尽、GremlinHorn末杀抽牌及Feed保留最大生命收益但拒绝自身耗尽。双方均使用真实Fuzzy战斗；DSL显式获得参考玩家自带的BurningBlood。永久牌组通常为空，起手场景为Strike／Defend／Bash，生命等受控设置写入报告。

每个声明边界比较回合号、能量、生命／最大生命／格挡、完整有序五牌区、双方Power及实际施加者、实际挡伤、ending，以及monster_ai／shuffle／combat_targets完整状态和游标。逐次随机调用保留操作、边界、结果和候选顺序；起手洗牌沿原牌组顺序，后续重洗沿StableShuffle排序，各自从参考自身牌堆重建并核对结果。监听顺序直接读取锁定参考的实际迭代器，与调用生产排序函数的DSL实例通知比较，覆盖玩家Power、库存、牌区、敌方Power及模型；ShouldClearBlock使用同一Direct路径。缺场景、维度、边界或差异均非零。

末杀组件比较到耗尽／出牌完成，奖励结算不在组件内；base另验证外层胜利与合法余效。移除／熔化与跨战资格、起手阶段由base行为检查验证；新建／失效快照、嵌套及暂停续接保留core与Power改量原测试。敌人出生／槽位与选招时点、虚无耗尽原因／延迟抽牌仍有独立未支持边界。此结果不认证正常Run或M01。

## BattleFriend受控组件比较

运行`dotnet run --project tools/differential -c Release -- battle-friend-compare --output .artifacts/differential/battle-friend`。两侧各自创建真实三档靶人、执行入场与前三回合，比较HP、计时、活动／逃离／死亡、意图、胜利／超时、最大生命，以及niche的实际抽样参数／结果／游标／完整状态和AI消费。真实击杀场景在第三回合出牌前将双方目标HP设置为5，再实际打出Feed；双实例场景核对固定HP的候选耗尽抽样形式。没有重设随机种子或注入结束结果。

这项比较有独立的13个组件场景，migration要求全部通过。报告的independent_verified只适用于该组件范围，normal_run_verified及parent_event_verified保持false；不关闭BattlewornDummy父事件、M01或原11份suite的缺口。

## Wriggler与Infection受控组件比较

运行`dotnet run --project tools/differential -c Release -- wriggler-compare --output .artifacts/differential/wriggler`。两侧使用空永久牌组及真实战斗开始、抽牌／重洗和回合命令，验证四槽三回合、StartStunned、HP候选耗尽，以及Infection格挡、无加成、致死、Retain／Ethereal、非手牌、费用修改与自动尝试。niche比较实际参数、结果、游标及完整状态，并核对run／怪物AI消费、shuffle消费，以及状态牌入场／生成顺序、空creator和玩家生成数。参考侧增加不改玩法的观察遗物。参考的StatusCard意图名称显式映射为DSL的status，保留buff,status顺序。

工具只在受控初态设置HP、格挡和效果牌；测试附加DSL入口调用相同生产过程，参考使用真实CombatEngine及AutoPlayCmd.FromCards。致死场景保留原生牌区守卫结果：致死牌在Play，后一张效果牌在Hand。两个seed共24个组件场景必须全部通过，migration保留原11份suite与严格失败；normal_run_verified和parent_event_verified保持false，不关闭DenseVegetation父事件或完整Run。

## 重洗诊断

```powershell
dotnet run --project tools/differential -- test --suite regression --case random-reshuffle-unsupported --output .artifacts/differential/reshuffle
```

原用例使用同一字符串 seed、完整有序初始牌区和三次 `end_turn`，原 `actions／facts／pile-order／streams` 全部保留。工具只在声明的受控初始化阶段放置牌和边界资源；随后由双方真实回合与抽牌路径重洗。DSL 两个受控敌人没有完整的对应内容消费者，参考仍执行自己的敌人规则；这些玩法差异不在工具层补偿，本例不能计通过或代表正常 Run。

每个边界的 `random` 保存从脚本起点累计的调用、全部具名流的起始／当前游标与状态字，以及丢失／截断标志。调用包含规范流名、操作、上下界、结果、有序候选槽位与摘要、可用的源码位置。槽位由初始声明与各自的长期来源建立，同名牌保持独立，不按最终牌序重新匹配。首差区分流状态、候选错序、调用参数、游标和结果；任何丢失、未知参数面或容量溢出均报告 `missing_dimension`。事件顺序没有在本例独立验收。

参考适配沿锁定上游已有的实例结果观察字段附加并释放本次诊断，不新增全局回调、不修改参考源码。洗牌候选从参考自己的实际牌区及 `CardModel.CompareTo` 构造，按实测交换更新诊断副本，再与真实手牌／抽牌堆顺序核对；核对失败便拒绝认证该轨迹。

`locked-native-sequential@1`使用参考原生根和具名流派生，不再覆盖全部run流为旧hash32。原[有序受控初始化](../../docs/specs/environment-constraints.md#env-t04受控初始化与回合case边界2026-10-11)仍将参考初始牌区恢复为用例声明顺序，并在脚本起点以相同的原生Shuffle种子恢复该流游标0；这项消费单列，不能用于正常Run。脚本期间禁止补抽、重键或重设游标。报告保存原始seed、实际原生种子、来源摘要与初始化消费，迁移检查通过当前独立Python神谕核对15条流派生及抽样。玩家流保留原生状态，未迁移敌人的行动差异继续严格失败。


[迁移检查](../ci/README.md)执行这两个原用例及严格失败检查，校验原 suite 和生产依赖隔离。`self-test` 另外检查候选错序、相同消费数但参数不同、结果／流位置首差，以及记录缺失／截断／溢出；故障注入不算独立玩法通过。原源与旧适配器见[迁移对照](../base-migration-baseline/README.md)，逐组去向见[CI 迁移数据](../ci/fixtures/legacy-dsl-checks.json)。受控机制比较不替代完整[ENV 验收](../../docs/specs/environment.md)；参考玩法保持只读。

## 核对正常开局是否可以比较

在仓库根目录运行正常开局预检，输出目录可按任务更换：

```powershell
dotnet run --project tools/differential -c Release -- normal-preflight --output .artifacts/differential/normal-preflight
```

预检读取 `tools/base-spire-dev/fixtures/normal-first-combat.json` 的固定 seed、Neow 选择、公开动作优先级和领取／跳过两条路径。DSL 侧启动真实 stdio Host，从公开候选正常开局、出牌、获得奖励并返回地图；参考侧使用锁定检出的原生 RunState、Player、RoomFactory 和 Enter，到达待选 Neow。双方都保留自己的初始化消费，没有使用受控重洗的种子适配、注入战斗或修改参考。

预检比较待选 Neow 时的生命、最大生命、金币、永久牌／遗物构成、选项顺序和完整地图节点／房型／边。公开 RunDeck 通过 pile 发布总数和完整成员，不声明永久牌序，因此只比较牌组构成，不把公开句柄的排序当作永久牌序。地图类别标识按两侧原生枚举名规范化，位置和边不重排为参考答案。

`result.json` 保存包／固定输入／适配器摘要、参考 commit／tree／程序集、双方开局记录、首个可观察差异和公开动作前缀。`claim.public.ndjson` 与 `skip.public.ndjson` 保存真实协议请求／响应；动作前缀使用内容、坐标或当前同类候选下标，不复用其他会话的临时句柄。源码和原始日志分别用于定位规则与复现执行。

固定seed777的Neow选项为WingedBoots／BoomingConch／CursedPearl，地图与生产DSL均按锁定新版来源生成。完成顺序随机与开局来源迁移后，开局事实相同仍只说明前缀符合；后续真实SlimesWeak战斗、奖励和返回虽已由DSL公开路径执行，但逐步双侧适配仍由[工单 #47](https://github.com/QingXiang22/auto-the-spire/issues/47)验收。

三项Neow现均有生产效果，FlexPotion也可从原正常奖励池领取；WingedBoots／CursedPearl正常获得后首战与FlexPotion受控组件的独立期望由`neow-effects-vector.json`提供，base行为组消费，migration核对来源摘要。该向量保留正常与受控范围，不替代此处正常策略逐边界的全公开比较。MapNode.Index／MapNodes发布顺序按下述实际原生遍历比较；其余未映射公开字段仍须取得参考对应状态，不能只检查DSL不变量或生成参考值。


两侧确有记录且存在开局首差时返回1。缺参考、缺候选、超时、输入策略不完整、无法完成 DSL 返回，或者开局相同但后续双侧适配未完成时返回3。预检不返回0，`complete` 和 `independent_verified` 均为false；`executed` 只统计实际取得双方开局记录的路径，不代表完成该路径的逐步双侧比较。报告另列访问、永久实例映射、牌区、逐次随机和事件等未验证维度。完整 M01 仍须逐步比较，单侧领取／跳过成功不作为验收。

可运行 `normal-preflight-self-test` 定向检查比较器。迁移组会验证两条真实公开返回、全部开局事实一致、后续比较未完成仍失败、缺参考与零执行失败，并保留原11份 suite 的全部必测 ID；故障注入不计作独立玩法通过。

## 比较正常流程的共同动作前缀

```powershell
dotnet run --project tools/differential -c Release -- normal-compare --output .artifacts/differential/normal-paired
dotnet run --project tools/differential -c Release -- normal-compare-self-test
```

`normal-compare` 保留固定输入下领取／跳过两条奖励路径，并新增分别使用真实目标决策的领取／跳过路径，共四个用例。DSL 侧通过正式 Host 工厂、协议编码和 `ControlHost.Dispatch` 执行公开候选；结束回合只提交一次生产`EndTurn`，不补发开始动作；内部诊断读取该 Host 正在执行的同一会话。参考侧从原生 RunState、Player 和真实房间入口独立执行同一个语义动作。它与启动 stdio 子进程的 `normal-preflight` 分开报告。

当前报告格式为 `normal-run/paired@2`：`result.json` 保存状态、动作前缀、比较边界、首差和诊断引用，`normal-evidence.zip` 保存按内容摘要去重的压缩数据。累计历史按 64 条组成共享片段，边界仍保留原历史长度；长 JSON 字符串按原字面值保存。完整比较发生在存储转换之前，不删事件、候选或必需维度。两个文件须一起保留，不能只拷贝摘要。

Python 消费者使用 [load_report](../ci/normal_report.py) 按需读取引用；会校验内容摘要及历史长度，缺条目、损坏或计数不符均失败。公开请求／响应仍分别写入原 `.public.ndjson`。同一任务复用一个输出目录，不再为每轮核对保存完整报告副本。

每个共同边界保存正常动作候选、Neow 决策约束、玩家资源、全部初始敌人及 Power、访问阶段与坐标、永久资产和六个有序战斗牌区。永久卡按各自正常起始牌组建立槽位，战斗副本沿 `PermanentSource`／`DeckVersion` 对应；死亡敌人沿参考的 `SpawnedEnemies` 保留最初编号，不按剩余敌人重新编号。Neow 的可选实体集合按语义排序比较，选项原位置仍保留在真实公开快照中。

原两条路径的 `PlayAttackFromHand` 对应同一张卡及 `first-legal-target` 指令。新增路径通过 `PlayTargetedCard` 暂停，从真实公开候选选择首个目标；参考侧暂存卡片，用原生目标 API 提供候选，收到回答才实际出牌。一次共同结束回合包含 DSL 的显式下一回合续接，对应参考原生结束回合所包含的敌方行动和下一玩家回合。工具不插入玩法效果，也不修改敌人、血量、奖励、流种子或游标。

每个共同边界分别从真实公开快照和参考原生牌区提取七个牌堆的总数、成员完整性、可见成员及已知位置；这与内部有序牌区诊断分开比较。抽牌堆成员保持隐藏。双方还比较实际消费过的每条随机流的游标与四个状态字；这不能替代逐次随机操作及候选比较。参考遭遇的局部随机流在真实房间初始化后，按其实际初始状态和原生遭遇种子唯一识别。

`random.step_states` 比较每次随机调用前后的四个状态字和流内游标。参考侧记录真实调用状态；DSL 侧从实际边界状态逆推，并逐次核对实际记录的原始随机字。缺失、截断、多字消费或不支持的随机算法会使该维度失败。独立流按各自顺序比较；完整候选与事件仍是未满足的必需维度，不能据此将正常路径判为通过。

`random.float32_calls` 逐条比较正常路径中的浮点取样：具名流、调用前后游标、已求值的上下界及最终 binary32 位型。DSL 读取真实 `RandomCallDetails.Float32`，参考读取自身 `NextFloat` 参数和实际结果；原始 double 记录保留在诊断中。比较按流名与流内游标规范化，不要求独立流有相同的全局初始化顺序。缺附加数据、截断、丢失、重复或流内错序均失败。固定首战有敌人三次与奖励八次浮点调用；这部分一致不代表离散候选、权重及拒绝过程已经比较，完整 `random.trace` 仍为必需维度。

`random.scalar_calls` 比较实际整数上下界、结果及流内游标。原生无参 `NextBool` 的 true／false 对应 DSL `[0,2)` 的 0／1；地图 Gaussian 的四次输入将 DSL `[0,2^53)` 整数除以 `2^53`，与原生单位区间 `NextDouble` 的 binary64 位型比较。94 次开局加权抽样读取 core 0.9.5 在实际抽样时保留的 `SingleRawWord`，按 vanilla 公式 `(raw >> 11) * 2^-53` 规范化，同时校验实际离散结果确为该单位值乘候选数后的截断。缺字或多字消费明确失败，不从选中索引反推或重跑生成器。

固定首战累计比较 725 次数值调用，其中 627 次整数、四次 Gaussian 输入及 94 次加权单位值，双方 `unmapped_scalar_calls` 为空。独立流按流名分组，流内原顺序仍须完整保留；其离散候选另见下文。数值结果相同不证明全部候选或拒绝过程相同，完整 `random.trace` 继续作为必需维度。

正常出生的 HP 抽样直接比较两侧真实 niche 调用：有可用值时保留 `NextInt(0, availableCount)` 的偏移；候选耗尽时保留原HP区间调用，不再把源偏移转换为最终HP参数。参考通过只读 before-setup 诊断，在真实创建完成、任何 Setup 效果之前保存 `SpawnedEnemies` 原创建顺序、同一怪物的初始区间及 MaxHp，并关联同一创建窗口的真实 niche 调用。初始化验证按前序不同MaxHp排除候选，检查实际参数和所选生命与原生实例一致，随后保留原始调用；缺记录、错序、参数或生命不符均失败。该窗口不推断中途召唤／复活的成员变化；其他随机来源仍按原边界严格报告未分类。

`random.weighted_candidates` 另行比较 94 次开局加权抽样的实际有序候选、幕／房型／弱怪标记、兼容性标签、权重、总权重、选中索引及流内游标。DSL 从抽样时保存的候选、一层定义引用与出关系目标字段取值，均匀离散抽样对应各条目权重 1；不回查已销毁候选或后来的定义状态。参考在真实 RNG 回调中复制同一幕对象上存活的 GrabBag 条目、标签与总权重，将实际单位区间结果按原生累积权重规范化为索引。标签按名称排序比较成员，原生不参与兼容性判断的 None 不计入；候选顺序仍按抽样输入保留。该诊断限定于锁定来源的同步开局生成：每个新池建立一个新 bag，后续抽样及补池沿用同一对象；缺少或同时发现多个新池时失败。

`random.weighted_attempts` 比较每次抽样时上一次接受的遭遇、兼容性结果列表、是否启用谓词及实际接受／拒绝结果。DSL 读取实际纯过程参数／返回和选择过程返回，要求预检覆盖完整候选，并核对抽中项的实际兼容性结果；诊断格式改变、截断或缺记录时失败。参考从同一次回调的真实候选与上次接受对象计算纯兼容性条件，在下一个 RNG 回调或边界核对实际 bag、总权重与 LastPicked／LastMonster：拒绝保持原袋，接受删除选中项；若下一次抽样前已补池，要求与同一幕的完整源池一致。原生实际后状态保留在 `opening_commits`，不追加随机调用。

固定输入有 87 次接受和七次拒绝。`random.opening_queues` 从实际保留的 RunRoomPlan 与原生 GeneratedActRooms 读取三幕的 87 项普通／精英队列，比较幕、房型、顺序和定义；各侧还要求队列逐项等于实际接受的抽样，防止正确抽中却错误提交。DSL 的九个临时遭遇袋在开局完成后必须已销毁；原生保留 bag 属于不同存储方式，以相同的实际队列和已核对的提交变化比较。`unmapped_weighted_semantics` 因此为空；其他候选语义和完整 `random.trace` 要求仍保留，不能据此认证全部随机维度。

公开语义投影直接消费真实 Host 快照，比较整张地图的坐标／房型／连接／访问和可选状态、Run 阶段与当前位置、Neow 选项原位置和选择结果、访问顺序及提交状态、金币／卡牌／药水奖励，以及持有药水槽位和遗物熔化状态。玩家、敌人和 Power 的已映射公开资源也单独比较，不用内部事实填补未知或缺失的公开字段。数组按各自声明的成员／顺序语义规范化；单人 Profile 中参考药水的 AnyPlayer 目标对应自己。

Run.Profile 依据参考的实际角色、进阶、人数、epoch／无色卡解锁状态及完整角色池规范化；部分解锁或其他配置明确失败，原值保存在 `native_configuration`。Player.AutoDrawPerTurn 对应原生 CombatEngine 的 BaseHandDrawCount，表示修改钩子之前的基础抽牌配置。首战出现的 Vulnerable 按原生持续时间计数，不在玩家回合结束时整体失效，因此其 ExpiresAtTurnEnd 为 false；其他 Power 的此项语义未映射时明确失败。

Player.RoomUsed 和 PendingForge 表示休息点选项已使用及锻造目标待选状态。参考端读取当前房间、房间栈数量和真实访问历史，确认首幕只经过 Ancient 与首个 Monster 后，将尚不存在的休息点选择状态规范化为两个 false；原始依据保存在 `native_rest_context`。休息点、可能解析为休息点的 Unknown、多层房间或后续幕不适用此映射，明确报告缺失维度。

`public.player_history` 比较当前活动战斗的出牌次数、正数 HP 损失次数、本回合损血／耗尽标记、敌方阶段及永久牌组使用状态。DSL 读取公开字段；参考读取原生玩家计数与伤害／耗尽历史、当前阵营及永久牌组类型。原生房间在奖励阶段保留已完成战斗的历史，DSL 在奖励发布前清理；共同表示将已结束战斗的计数与回合标记归零，并在 `native_player_history` 保留原生归档计数。这些字段的比较只覆盖实际执行的正常路径，尚未映射的公开字段继续单列。

`public.reward_sets` 比较全部保留奖励集合的访问标识、房型来源、角色、生成幕／进阶、卡牌数量、稀有度策略、生成／关闭状态及待领药水引用。参考在真实奖励生成完成后保存来源上下文，读取 CardReward 的实际数量、策略和填充状态，并在各边界读取房间退出完成标记及 PotionReward 的处理状态。领取或跳过后待领药水引用归空；集合及生成上下文在返回地图后继续比较。

集合中的池资格、升级／修改策略和随机流也纳入比较。参考只在实际 Ironclad 单人首幕、解锁池与完整池逐项一致、CardReward 使用默认遭遇路径、未启用语义随机键且活跃监听者没有奖励修改钩子时，规范化为 all-unlocked-ironclad／act-one／none；随机流取实际 PlayerRng.Rewards 的已登记名称。原始池、生成上下文和钩子清单保存在 `native_reward_policies`；自定义生成参数、部分解锁、其他幕或修改钩子明确失败。

`public.enemies` 同时比较当前招式的索引与意图。参考索引为状态机注册的实际 MoveState 顺序中的位置，意图读取同一 NextMove 的 Intents；首战映射 Attack／StatusCard 为 attack／status。未注册招式、多项意图及尚未映射的意图种类明确失败，不从招式名称猜测或填入默认值。

`public.cards` 比较全部当前可见的永久牌、战斗牌与奖励牌的实例来源、定义、标签、原生条目、种类、稀有度、费用、伤害变量、升级及关键字元数据；不展开隐藏抽牌堆。DSL 的 BaseDamage 与 DamageGrowth 相加对应参考的原生伤害变量。奖励牌从两侧实际创建位置登记来源，领取后保留：参考原生 SelectOption 会复制选中对象，因此检查实际 SelectedOption 及唯一新增永久实例后建立这条获得来源，不按领取后的名字或位置重新配对。多实例获得或未提取的能量 X 明确非零。

`public.snapshot` 汇集上述由双方分别读取的全部公开语义面，以及真实候选和当前决策。只有每个必需语义面都有结果、没有未映射字段时才生成；新字段、错误命名空间、字段未知或缺失、内容可见性错误都不能用内部状态补齐。自检逐类扰动实际快照中的每个字段，要求结果变化或明确拒绝；保留奖励集合的 PotionPending 也单独读取，不能只在当前奖励窗口比较。

Host 的协议、规则包、目录及后端标识与真实 describe 应答核对，session_id 与实际 start 应答核对；观察号和候选句柄继续由请求重放、过期拒绝与实例映射验证。原生参考没有这些 Host 专属标识，不能伪造相同字符串当作游戏事实。原始 Host 快照保留在诊断，跨后端比较使用共同语义和已经声明的规范化，例如伤害变量总值和战后活动计数归零。

Neow 的 EffectSupported 对照真实 EventOption.IsEnabled：锁定的 AncientEventModel.RelicOption 必须有已注册遗物目标和实际获得回调，不使用 DSL 支持名单或参考常量；禁用回调及未知目标另有负向检查。这项可执行资格不能单独证明遗物全部后续机制。药水对照覆盖奖励中和库存中的全部公开实例、来源、槽位、用法、目标及 EffectSupported。当前四路径的 FlexPotion 能力来自另一个原生组件的实际获得、使用、5层临时力量和回合末移除；组件先于正常随机观察执行，结果和随机不注入被比较的 Run，也不把它写成正常路径已经用药。其他尚无独立执行依据的药水明确报缺维度，不默认支持或缩减来源池。

每个边界重发上一条真实请求，检查原响应缓存；换用新 request_id 和旧观察号时，严格要求 `stale_observation`，并检查拒绝也被缓存。每次查询和拒绝前后，游戏状态、随机状态、待决策及完整公开快照必须不变。报告保存逐边界检查结果；缺结果或任一检查失败均不能认证路径。

`events.observed_order` 比较抽牌、出牌前、伤害、出牌后、Power 变化、玩家回合开始和敌人行动开始的共同顺序。DSL 读取事件发生时的有界类型化快照，参考读取原生观察通知；伤害保留来源、目标、格挡、未格挡、溢出与击杀，Power 保留定义、持有者、当前量和变化量，敌人行动保留实例与招式。事件中的牌定义和升级取自发生时字段，实例来源沿已有对应关系，不用边界时的最新字段重写历史。该序列仍是已映射事件子集，不能代替全部适用事件。

共同顺序也比较战斗开始、玩家回合抽牌准备完成、敌方阶段开始、单个敌人招式完成和整轮结束。DSL 分别取 `BeginDevelopmentCombat` 返回、`DrawDevelopmentTurnHand` 返回、`RunEnemyTurn` 进入、`PerformEnemyMove` 返回及 `RunEnemyTurn` 返回的真实诊断点，与参考原生观察通知对应。完成记录保留同一次敌人招式开始时的实例和招式，不用已经准备的下一招重写它；未配对、嵌套或缺失的招式／敌方阶段完成记录明确失败。这些相关过程记录随历史轨迹保存，不要求打开逐指令诊断。

该映射覆盖固定正常首战的阶段顺序，不把原生对全部模型调用的空 hook 与 DSL 稀疏订阅逐条视为相同，也不声明所有 hook 效果已比较。双方原始事件与监听器诊断继续保留；完整 `events.trace` 仍为必需维度。

参考的 `native_event_coverage` 逐条登记观察通知是否已加入共同事件序列。回合末监听通知另保留实际实例标识、定义、四个钩子阶段、Player／Enemy 阵营、进入／退出及虚方法声明类型；只有实际继承锁定 AbstractModel 默认方法时才标为默认空操作，覆盖方法即使本次没有状态变化也保持未映射。进入／退出必须按同一对象、阶段与阵营配对；边界仍有未完成调用时失败。未知观察通知保留在 Unmapped，不因未命中现有映射分支而消失。该清单说明参考通知的处理范围，尚不认证 DSL 全部事件或 InvokeExecutionFinished 的完整语义。

每条公开协议轨迹旁的 `.trace.json` 保存一次完整历史快照，边界诊断不重复复制这些值。记录丢失、文本或快照截断均不能认证。正常 Run 的开发便利入口由生产 DSL 守卫拒绝。工具在 `unmapped_public_actions` 保留无法映射的原候选，并在 `missing_dimensions` 标明数量；迁移检查要求四条路径的每个共同边界都没有未映射候选。

公开结构另比较七个牌堆视图、库存组和 Run 序列的所有者，地图节点序列的成员、地图双向连接、全部公开实体的分类数量，以及当前访问／当前奖励／访问奖励之间的引用。实体句柄按玩家、Run、坐标与原生访问 ID 解释，不能用最终位置修复错误引用。缺失、重复或游离的公开地图节点、访问、奖励、药水、遗物和牌堆视图均拒绝；未知关系类型也进入比较。原生牌区没有独立视图实体，其视图名称和所有者规范化为单人玩家的对应牌区。

地图节点以`public.map_sequence`比较实际发布顺序，以`public.map`逐节点比较公开Index、坐标及原有字段。base `0.7.0-dsl.64`将旧创建次序改为公开遍历语义：参考端直接读取`ActMap.GetAllMapPoints()`，随后接独立StartingMapPoint、BossMapPoint；Index为这份实际序列的位置。DSL在最终坐标整理后发布同一语义，不修改来源抽样、实例或边，也不按DSL输出重排参考。独立十seed向量保存该原生顺序，正常四路径在每个共同边界继续比较；倒序、错索引、缺字段和缺／重复成员均严格失败。`unmapped_public_fields`继续按真实快照检查，非空时不能生成完整`public.snapshot`。

药水按共同动作序号和各侧原生奖励位置登记创建来源，领取后保留同一实例对应，不按持有槽号或名称重新配对。合法丢弃候选对应参考原生 `PotionCmd.Discard`；自检通过正常战斗取得并领取药水，再回到地图实际丢弃，同时核对候选、资产、事件、随机状态和协议重放。持有药水缺少创建来源时明确报告缺维度。

战斗内新生成的单张牌以共同动作序号建立来源槽位，并保留该实例的后续对应，不按最终牌区重新配对。一个共同动作出现多张新牌时，当前观察不能区分其生成来源，明确报告缺维度并返回非零，不使用引擎内部编号或最终牌序猜测。

`lifecycle.settlement` 比较原四路径最后击杀的两处真实状态：外层牌效果结束、奖励生成完成。DSL 从 `CombatEnded` 发生时的快照读取前者，并检查击杀、出牌后通知、战斗结束的顺序及效果深度归零；参考在原生出牌结束通知和驱动的胜利检查后、`ResolveOutcomeAsync` 前读取。双方分别保留生命、最大生命、金币与奖励生成状态，再与真实奖励边界比较。正常 BurningBlood 胜利治疗应位于这两处之间；错序、缺失和重复结算记录均失败。这里覆盖固定正常路径的普通牌末杀；Feed 和药水末杀仍由单独的受控 base 检查验证。

遇到已比较维度的首差便停止共同前缀，保存动作、双方边界和首差。`missing_dimensions` 继续列出逐次随机候选与规范化和适用事件；公开快照有缺项时仍单列 public 缺维度。原始公开协议、Engine 事件与随机诊断、参考原生战斗观察及初始化以来的原始随机结果保留供定位；这些原始记录不等于完成跨后端比较。报告为非零，`complete`／`independent_verified` 保持 false，不能认证 M01 或推断尚未比较的维度已通过。

自检逐项删除双方同一个必需字段、注入各维度首差、检查零执行、缺动作／终点、缺协议检查结果及目标脚本缺决策，并核对开启与关闭诊断时真实 Host 和原生参考的已比较状态及随机状态不变；Host 另核对完整公开快照。迁移组同时执行四条正常路径和缺参考构建；原11份 suite、必测ID和严格失败语义保留。

公开 `Player.DrawBlocked` 比较实际效果抽牌资格：DSL 发布同一 `CanDrawCards(owner,false)` 查询的否决结果，参考战斗使用真实 `Hook.ShouldDraw`。首战创建前没有原生战斗态，参考直接枚举 `RunState.IterateHookListeners(null)` 并调用各原生 `ShouldDraw(player,false)`，不构造战斗态或填常量。正常四路径逐边界比较 `public.draw_blocked`；受控 `draw-permission-vector.json` 另用真实 NoDraw、效果抽牌、回合手牌及到期验证正负状态，不冒充正常路径已覆盖该 Power。

## 耗尽原因与延迟抽牌组件

`dotnet run --project tools/differential -c Release -- exhaust-cause-compare --output .artifacts/differential/exhaust-causes`运行两seed各15种场景。受控命令例比较设置、耗尽后与侧结束三个边界，自然回合末例比较设置与敌方开始前两个边界；均比较五个有序牌区、实际Power与隐藏虚无次数、每次抽牌时已清零的次数、资源／ending及monster_ai、shuffle、combat_targets的完整状态、游标和逐次调用候选。普通耗尽、改量、NoDraw先后获得、满手、重洗、Hellraiser嵌套耗尽、移除重建、无事件生成、即时Charon／FeelNoPain、真实回合末虚无及Infection正文、末杀和暂停续接均执行实际命令。

两侧明确使用空永久牌组、HP1000和受控牌区；Strike／Bash／Infection显式取得虚无，嵌套Strike另取得耗尽关键词。自然回合末比较参考原生EnemyTurnStarting观察点与DSL清手／侧结束后、敌方行动前的行政暂停；末杀停在奖励前。暂停抽牌先与参考只读CardDrawn通知比较首张，再续接完成。该报告仅认证受控组件，不认证正常Run或M01；原11份suite与正常比较的缺维度失败保持。

## 敌人出生与选招边界组件

`dotnet run --project tools/differential -c Release -- enemy-birth-compare --output .artifacts/differential/enemy-birth`运行两seed共20项受控判别。反向槽位编队使用真实CombatRoom.Prepare，先登记玩家，再按4、1、3、2工厂顺序创建Wriggler并排序；比较源CombatId、逐只HP、个体RNG初始状态、实际行动顺序、niche每次参数／结果／游标及完整终态。另保留第三幕位置种子、零HP但仍在侧列表、完整移除后编号不复用、显式新战斗重置、重复MaxHp去重与固定HP仍消费的场景。位置／HP／移除设置明确为工具状态，不证明正常可达。

入场条件使用原生SlumberingBeetle的真实入场Power及SLEEP_CHECK图；DSL仅加载工具专用条件探针，比较BeforeCombatStart、玩家BeforeSideTurnStart、AfterEnergyReset和敌方末的节点／历史，再验证下一回合。回合前移除原生Slumber与改变探针条件用于判别提前选招。该场景不登记SlumberingBeetle或Slumber为正式支持，不认证其伤害／Power全效果。组件显式执行DSL End+Begin，正常EndTurn自动进入下一可行动回合仍独立验收。新组件不替代原11份suite或正常Run比较。

固定首招的补充判别使用真实TwigSlimeM，比较建图后的初始历史与尚未发布的NextMove、首次Roll，以及普通玩家回合后的额外玩家入口。原生调用真实额外回合StartTurn入口；DSL显式关闭开发回合标志后再次Begin，只认证保存招式／历史及AI四字状态不变，不声明正式额外回合动作或完整生命周期。`--package MANIFEST`可对指定生产包复现差异；migration要求包含原18项和新增两seed的extra-ready，未知／重复参数被拒绝。

## 侧开始、清格挡与行动资格组件

`dotnet run --project tools/differential -c Release -- turn-start-compare --output .artifacts/differential/turn-start`运行两个seed共16项判别。两只真实TwigSlimeS使用空牌组、归一化HP1000与显式格挡初态；逐项比较12个侧开始／清挡／侧结束边界（HornCleat为20个，运行至第三回合）、玩家资源及两个敌人的格挡和出生资格标志。实际Anchor证明首回合保留格挡，HornCleat读取清挡后的旧编号1并只在第二回合给14格挡，Barricade验证双方持有者与Direct首否决顺序。参考SturdyClamp与DSL工具专用钳制监听对照，只认证阻止者回调接缝，不登记SturdyClamp正式支持。

零HP场景明确不执行死亡命令，验证仍在侧列表的成员先清挡、再全体通知而不行动。出生资格场景直接设置双方标志，只证明当轮跳过和换边清标，不宣称召唤／复活。DSL显式End+Begin与参考自动推进到下一普通玩家回合比较；正常EndTurn动作另验。已有28项回合组件同时新增实际Power.AmountOnTurnStart维度，保留全部旧维度、用例和严格失败。

## 双方格挡命令组件

`dotnet run --project tools/differential -c Release -- block-compare --output .artifacts/differential/blocks`运行两个seed共80项受控判别。真实Dexterity／Frail／Unmovable／Juggernaut／Plating分别验证双方、来源所有者、负加值、无加成、非Move、分数、零量及999999999上限；BowlbugEgg执行真实招式，Plating执行初始与两个本侧末得挡。Before／After逐条比较精确修正量、已提交格挡、card／play／旗标和实际修正者实例标识，原生实例标识来自同输入的只读Hook.ModifyBlock输出。专用修正Power另验证双阶段只回调一次、乘零仍参与、无修正不回调；这不登记为正式内容。

真实Defend与EvilEye比较每个play的Move得挡历史；源EvilEye的两次调用不合并。Entrench真实源卡只与工具专用DSL正文比较Unpowered|Move，不登记正式支持。分数未增加整数格挡、已到上限和Unpowered|Move仍按源记录正的Move历史。暂停使用真实原生未完成Task及DSL决策，各续接一次；嵌套Before保留独立上下文，临时记录在完成后清理。只读预览另验状态hash、版本和随机不变。

初态为玩家先登记、两敌人、空牌组和归一化HP1000；给定格挡与Power是明确组件输入。出生HP随机、完整公开库存和正常Run不由本组件认证；ending场景只比较已进入格挡命令的余效，不替代完整胜负／奖励结算验收。原80项、16项侧开始、28项回合及原11份suite继续由migration严格保留。
