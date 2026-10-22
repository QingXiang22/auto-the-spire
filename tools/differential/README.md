# DSL 与独立参考比较

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

## 重洗诊断

```powershell
dotnet run --project tools/differential -- test --suite regression --case random-reshuffle-unsupported --output .artifacts/differential/reshuffle
```

原用例使用同一字符串 seed、完整有序初始牌区和三次 `end_turn`，原 `actions／facts／pile-order／streams` 全部保留。工具只在声明的受控初始化阶段放置牌和边界资源；随后由双方真实回合与抽牌路径重洗。DSL 两个受控敌人没有完整的对应内容消费者，参考仍执行自己的敌人规则；这些玩法差异不在工具层补偿，本例不能计通过或代表正常 Run。

每个边界的 `random` 保存从脚本起点累计的调用、全部具名流的起始／当前游标与状态字，以及丢失／截断标志。调用包含规范流名、操作、上下界、结果、有序候选槽位与摘要、可用的源码位置。槽位由初始声明与各自的长期来源建立，同名牌保持独立，不按最终牌序重新匹配。首差区分流状态、候选错序、调用参数、游标和结果；任何丢失、未知参数面或容量溢出均报告 `missing_dimension`。事件顺序没有在本例独立验收。

参考适配沿锁定上游已有的实例结果观察字段附加并释放本次诊断，不新增全局回调、不修改参考源码。洗牌候选从参考自己的实际牌区及 `CardModel.CompareTo` 构造，按实测交换更新诊断副本，再与真实手牌／抽牌堆顺序核对；核对失败便拒绝认证该轨迹。

`locked-native-sequential@1`使用参考原生根和具名流派生，不再覆盖全部run流为旧hash32。原[有序受控初始化](../../docs/design/environment-implementation-constraints.md#env-t04受控初始化与回合case边界2026-10-11)仍将参考初始牌区恢复为用例声明顺序，并在脚本起点以相同的原生Shuffle种子恢复该流游标0；这项消费单列，不能用于正常Run。脚本期间禁止补抽、重键或重设游标。报告保存原始seed、实际原生种子、来源摘要与初始化消费，迁移检查通过当前独立Python神谕核对15条流派生及抽样。玩家流保留原生状态，未迁移敌人的行动差异继续严格失败。


[迁移检查](../ci/README.md)执行这两个原用例及严格失败检查，校验原 suite 和生产依赖隔离。`self-test` 另外检查候选错序、相同消费数但参数不同、结果／流位置首差，以及记录缺失／截断／溢出；故障注入不算独立玩法通过。原源与旧适配器见[迁移对照](../base-migration-baseline/README.md)，逐组去向见[CI 迁移数据](../ci/fixtures/legacy-dsl-checks.json)。受控机制比较不替代完整[ENV 验收](../../docs/design/environment-completion-and-differential-v1.md)；参考玩法保持只读。

## 核对正常开局是否可以比较

在仓库根目录运行正常开局预检，输出目录可按任务更换：

```powershell
dotnet run --project tools/differential -c Release -- normal-preflight --output .artifacts/differential/normal-preflight
```

预检读取 `tools/base-spire-dev/fixtures/normal-first-combat.json` 的固定 seed、Neow 选择、公开动作优先级和领取／跳过两条路径。DSL 侧启动真实 stdio Host，从公开候选正常开局、出牌、获得奖励并返回地图；参考侧使用锁定检出的原生 RunState、Player、RoomFactory 和 Enter，到达待选 Neow。双方都保留自己的初始化消费，没有使用受控重洗的种子适配、注入战斗或修改参考。

预检比较待选 Neow 时的生命、最大生命、金币、永久牌／遗物构成、选项顺序和完整地图节点／房型／边。公开 RunDeck 通过 pile 发布总数和完整成员，不声明永久牌序，因此只比较牌组构成，不把公开句柄的排序当作永久牌序。地图类别标识按两侧原生枚举名规范化，位置和边不重排为参考答案。

`result.json` 保存包／固定输入／适配器摘要、参考 commit／tree／程序集、双方开局记录、首个可观察差异和公开动作前缀。`claim.public.ndjson` 与 `skip.public.ndjson` 保存真实协议请求／响应；动作前缀使用内容、坐标或当前同类候选下标，不复用其他会话的临时句柄。源码和原始日志分别用于定位规则与复现执行。

固定seed777的Neow选项为WingedBoots／BoomingConch／CursedPearl，地图与生产DSL均按锁定新版来源生成。完成顺序随机与开局来源迁移后，开局事实相同仍只说明前缀符合；后续真实SlimesWeak战斗、奖励和返回虽已由DSL公开路径执行，但逐步双侧适配仍由[工单 #47](https://github.com/QingXiang22/auto-the-spire/issues/47)验收。


两侧确有记录且存在开局首差时返回1。缺参考、缺候选、超时、输入策略不完整、无法完成 DSL 返回，或者开局相同但后续双侧适配未完成时返回3。预检不返回0，`complete` 和 `independent_verified` 均为false；`executed` 只统计实际取得双方开局记录的路径，不代表完成该路径的逐步双侧比较。报告另列访问、永久实例映射、牌区、逐次随机和事件等未验证维度。完整 M01 仍须逐步比较，单侧领取／跳过成功不作为验收。

可运行 `normal-preflight-self-test` 定向检查比较器。迁移组会验证两条真实公开返回、全部开局事实一致、后续比较未完成仍失败、缺参考与零执行失败，并保留原11份 suite 的全部必测 ID；故障注入不计作独立玩法通过。

## 比较正常流程的共同动作前缀

```powershell
dotnet run --project tools/differential -c Release -- normal-compare --output .artifacts/differential/normal-paired
dotnet run --project tools/differential -c Release -- normal-compare-self-test
```

`normal-compare` 保留固定输入下领取／跳过两条奖励路径，并新增分别使用真实目标决策的领取／跳过路径，共四个用例。DSL 侧通过正式 Host 工厂、协议编码和 `ControlHost.Dispatch` 执行公开候选；内部诊断读取该 Host 正在执行的同一会话。参考侧从原生 RunState、Player 和真实房间入口独立执行同一个语义动作。它与启动 stdio 子进程的 `normal-preflight` 分开报告。

每个共同边界保存正常动作候选、Neow 决策约束、玩家资源、全部初始敌人及 Power、访问阶段与坐标、永久资产和六个有序战斗牌区。永久卡按各自正常起始牌组建立槽位，战斗副本沿 `PermanentSource`／`DeckVersion` 对应；死亡敌人沿参考的 `SpawnedEnemies` 保留最初编号，不按剩余敌人重新编号。Neow 的可选实体集合按语义排序比较，选项原位置仍保留在真实公开快照中。

原两条路径的 `PlayAttackFromHand` 对应同一张卡及 `first-legal-target` 指令。新增路径通过 `PlayTargetedCard` 暂停，从真实公开候选选择首个目标；参考侧暂存卡片，用原生目标 API 提供候选，收到回答才实际出牌。一次共同结束回合包含 DSL 的显式下一回合续接，对应参考原生结束回合所包含的敌方行动和下一玩家回合。工具不插入玩法效果，也不修改敌人、血量、奖励、流种子或游标。

每个共同边界分别从真实公开快照和参考原生牌区提取七个牌堆的总数、成员完整性、可见成员及已知位置；这与内部有序牌区诊断分开比较。抽牌堆成员保持隐藏。双方还比较实际消费过的每条随机流的游标与四个状态字；这不能替代逐次随机操作及候选比较。参考遭遇的局部随机流在真实房间初始化后，按其实际初始状态和原生遭遇种子唯一识别。

每个边界重发上一条真实请求，检查原响应缓存；换用新 request_id 和旧观察号时，严格要求 `stale_observation`，并检查拒绝也被缓存。每次查询和拒绝前后，游戏状态、随机状态、待决策及完整公开快照必须不变。报告保存逐边界检查结果；缺结果或任一检查失败均不能认证路径。

`events.observed_order` 比较抽牌、出牌前、伤害、出牌后、Power 变化、玩家回合开始和敌人行动开始的共同顺序。DSL 读取事件发生时的有界类型化快照，参考读取原生观察通知；伤害保留来源、目标、格挡、未格挡、溢出与击杀，Power 保留定义、持有者、当前量和变化量，敌人行动保留实例与招式。事件中的牌定义和升级取自发生时字段，实例来源沿已有对应关系，不用边界时的最新字段重写历史。该序列仍是已映射事件子集，不能代替全部适用事件。

每条公开协议轨迹旁的 `.trace.json` 保存一次完整历史快照，边界诊断不重复复制这些值。记录丢失、文本或快照截断均不能认证。正常 Run 的开发便利入口由生产 DSL 守卫拒绝。工具在 `unmapped_public_actions` 保留无法映射的原候选，并在 `missing_dimensions` 标明数量；迁移检查要求四条路径的每个共同边界都没有未映射候选。

药水按共同动作序号和各侧原生奖励位置登记创建来源，领取后保留同一实例对应，不按持有槽号或名称重新配对。合法丢弃候选对应参考原生 `PotionCmd.Discard`；自检通过正常战斗取得并领取药水，再回到地图实际丢弃，同时核对候选、资产、事件、随机状态和协议重放。持有药水缺少创建来源时明确报告缺维度。

战斗内新生成的单张牌以共同动作序号建立来源槽位，并保留该实例的后续对应，不按最终牌区重新配对。一个共同动作出现多张新牌时，当前观察不能区分其生成来源，明确报告缺维度并返回非零，不使用引擎内部编号或最终牌序猜测。

`lifecycle.settlement` 比较原四路径最后击杀的两处真实状态：外层牌效果结束、奖励生成完成。DSL 从 `CombatEnded` 发生时的快照读取前者，并检查击杀、出牌后通知、战斗结束的顺序及效果深度归零；参考在原生出牌结束通知和驱动的胜利检查后、`ResolveOutcomeAsync` 前读取。双方分别保留生命、最大生命、金币与奖励生成状态，再与真实奖励边界比较。正常 BurningBlood 胜利治疗应位于这两处之间；错序、缺失和重复结算记录均失败。这里覆盖固定正常路径的普通牌末杀；Feed 和药水末杀仍由单独的受控 base 检查验证。

遇到已比较维度的首差便停止共同前缀，保存动作、双方边界和首差。`missing_dimensions` 另列尚缺的完整公开投影、逐次随机候选与规范化和适用事件。原始公开协议、Engine 事件与随机诊断、参考原生战斗观察及初始化以来的原始随机结果保留供定位；这些原始记录不等于完成跨后端比较。报告为非零，`complete`／`independent_verified` 保持 false，不能认证 M01 或推断尚未比较的维度已通过。

自检逐项删除双方同一个必需字段、注入各维度首差、检查零执行、缺动作／终点、缺协议检查结果及目标脚本缺决策，并核对开启与关闭诊断时真实 Host 和原生参考的已比较状态及随机状态不变；Host 另核对完整公开快照。迁移组同时执行四条正常路径和缺参考构建；原11份 suite、必测ID和严格失败语义保留。
