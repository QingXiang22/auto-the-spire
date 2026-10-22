# DSL 与独立参考比较

[锁定 sts2-sim](../../reference/README.md) 同时作为当前规则依据和独立执行的比较后端。现有 DSL、固定向量及受控 hash32 适配仍保留实际旧来源标识，尚须向新基线迁移；一致只表示与该模拟器实现一致，不能单独证明客户端保真。

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

`source-compat-sts2-frozen@1` 仅按[已授权的受控初始化边界](../../docs/design/environment-implementation-constraints.md#env-t04受控初始化与回合case边界2026-10-11)配置参考 run 流的字符串 hash32、命名流 hash32／uint 回绕及完整 `old` 前缀解释，不调用 base 派生方法。参考自身初始化洗牌的消费在输入阶段单列并归零后才开始脚本，脚本期间禁止补抽、重键或重设游标。报告包含原始 seed、有效流种子、对齐前消费、源文件摘要和适配器摘要；迁移检查用独立 Python 神谕核对每条 run 流向量。参考玩家流仍使用原生派生，初始化前置消费也保留；这些差异如实失败，不扩大兼容范围来制造一致。

[迁移检查](../ci/README.md)执行这两个原用例及严格失败检查，校验原 suite 和生产依赖隔离。`self-test` 另外检查候选错序、相同消费数但参数不同、结果／流位置首差，以及记录缺失／截断／溢出；故障注入不算独立玩法通过。原源与旧适配器见[迁移对照](../base-migration-baseline/README.md)，逐组去向见[CI 迁移数据](../ci/fixtures/legacy-dsl-checks.json)。受控机制比较不替代完整[ENV 验收](../../docs/design/environment-completion-and-differential-v1.md)；参考玩法保持只读。

## 核对正常开局是否可以比较

在仓库根目录运行正常开局预检，输出目录可按任务更换：

```powershell
dotnet run --project tools/differential -c Release -- normal-preflight --output .artifacts/differential/normal-preflight
```

预检读取 `tools/base-spire-dev/fixtures/normal-first-combat.json` 的固定 seed、Neow 选择、公开动作优先级和领取／跳过两条路径。DSL 侧启动真实 stdio Host，从公开候选正常开局、出牌、获得奖励并返回地图；参考侧使用锁定检出的原生 RunState、Player、RoomFactory 和 Enter，到达待选 Neow。双方都保留自己的初始化消费，没有使用受控重洗的种子适配、注入战斗或修改参考。

预检比较待选 Neow 时的生命、最大生命、金币、永久牌／遗物构成、选项顺序和完整地图节点／房型／边。公开 RunDeck 是无序 group，因此只比较牌组构成，不把公开句柄的排序当作永久牌序。地图类别标识按两侧原生枚举名规范化，位置和边不重排为参考答案。

`result.json` 保存包／固定输入／适配器摘要、参考 commit／tree／程序集、双方开局记录、首个可观察差异和公开动作前缀。`claim.public.ndjson` 与 `skip.public.ndjson` 保存真实协议请求／响应；动作前缀使用内容、坐标或当前同类候选下标，不复用其他会话的临时句柄。源码和原始日志分别用于定位规则与复现执行。

固定 seed777 的首个可观察差异是 Neow 选项：DSL 为 NewLeaf／BoomingConch／NeowsBones，锁定参考为 WingedBoots／BoomingConch／CursedPearl；地图也不同。当前 DSL 仍使用旧来源的 hash32 流派生，参考原生 RunRngSet 使用新版根及具名流派生；Neow 理论池也存在版本差异。预检保留这些差异，不能据此将正常流重键来使结果相同。DSL 须向锁定新版迁移，不能反向改变参考；剩余比较范围见[工单 #47](https://github.com/QingXiang22/auto-the-spire/issues/47)。

两侧确有记录且存在开局首差时返回1。缺参考、缺候选、超时、输入策略不完整、无法完成 DSL 返回，或者开局相同但后续双侧适配未完成时返回3。预检不返回0，`complete` 和 `independent_verified` 均为false；`executed` 只统计实际取得双方开局记录的路径，不代表完成该路径的逐步双侧比较。报告另列访问、永久实例映射、牌区、逐次随机和事件等未验证维度。完整 M01 仍须逐步比较，单侧领取／跳过成功不作为验收。

可运行 `normal-preflight-self-test` 定向检查比较器。迁移组会验证两条真实公开返回、实际参考 Neow 首差、缺参考与零执行失败，并保留原11份 suite 的全部必测 ID；故障注入不计作独立玩法通过。
