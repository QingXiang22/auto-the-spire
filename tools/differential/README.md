# differential：ENV 环境的审计与对拍工具

2026-10-15：正式默认随机布局已采用 `sts2-v0.109-xxhash64@1`。`v109-rng --output <目录>` 独立核对6个原始seed的根、12条run流、3条player流、地图、UpFront计数/指纹及完整单人奖励池元数据；两例首战还核对实际使用的共享MonsterAi流。此专项不代表完整环境验收。参考保持只读，证据记录实际加载DLL摘要。原regression17显式使用历史hash32配置，正常奖励前缀及normal-two-combats使用v0.109原生参考。

ENV-T04 完整受控初始化入口为 `setup.initial_piles`（`ordered-starting-piles@1`），来源固定为 `Ironclad.StartingDeck+controlled-slice@1`。`hand/draw/discard/exhaust` 必须全部显式列出，每项带 `slot/card/upgraded`；校验源起始牌总集合、槽位唯一性与原 hand/discard 一致性。仅四个已授权原 case 使用原 `game_seed` 和 `source-compat-sts2-frozen@1` 的既有三类派生。两侧在 Start 后、脚本前分别从自己的来源牌组构造；脚本入口没有设牌、补抽或重设随机动作。未声明 case 保留旧路径。

`controlled-state-no-random@1` 只有完整声明再次验证、实际 start 边界的全部有序槽位与定义匹配、双方脚本计数实测增量均为 0 才可计入 passed。原 `controlled-no-random@1` 的 end_turn 限制保留。原 facts/actions/decisions/power 与全部牌区顺序仍参与比较，额外记录持久 `pile.*.initial-slots`；期间新造且未登记的实例标记 unknown。

`setup.powers` 是脚本前已经持有的能力（受控 Power 初值契约）。base 从嵌入 `SliceAuthorFrontend.Load().Powers` 的 `OwnerTurnEnd == PowerTurnEnd.Tick` 确定 `Ticks`，初始 `SkipNextTick=false`；参考从自己的源 Power 克隆并调用 `ApplyInternal`，该路径不触发 `PowerCmd.Apply` 对新施加玩家负面能力的首次跳过。Strength 的 `none` 保持不衰减；Vulnerable/Weak 的 `tick` 在本脚本首个敌方 side 回合末衰减（源 `AfterSideTurnEnd` 对玩家/敌方持有者均检查 `CombatSide.Enemy`，不是笼统的拥有者回合末）。此契约不声称运行中施加与初始持有相同。自检保留原 14 项，并实际执行错误 `Ticks=false` / `SkipNextTick=true` 初始化，以及玩家/敌方 Strength、Vulnerable、Weak 的独立双侧对照。

`script_random` 分别记录 Before/After、覆盖与快照，`initialization_random_count` 单列建局消费。base 单位为 Session.Random 全部流的 generator words；参考通过 scoped `RngDiagnostics.DrawObserver` 读取所有原生 Rng 调用（含临时、玩家、怪物），单位为 native-counter-increments，退出即恢复 observer。两单位不可跨侧直接比绝对值；0 增量各自独立验证，null 计数永远不当 0。真实重洗已于 2026-10-12 在源预排序与参考逐边界具名流读数到位后**原地升级为 `vanilla-compatible@1`**（见[重洗严格升级（2026-10-12）](#重洗严格升级2026-10-12)）；受控注入后的重洗随机游标对齐与曾经的 base 缺口见[重洗比较（2026-10-11）](#重洗比较2026-10-11)，参考侧逐边界真实流读数见[参考侧逐边界随机流（2026-10-12）](#参考侧逐边界随机流2026-10-12)。

[ENV 任务包](../../docs/design/environment-completion-and-differential-v1.md) §5 要求的独立 C#/.NET 9 入口。
**它不是生产后端**：只做两后端的适配、标准动作/诊断快照、逐步比较与首差、有限生成与最小复现。

参考实现的 `ProjectReference` 只存在于本工具里（**条件引用**：没有 `reference/sts2-sim` 时整个解决方案
仍能构建，此时 `compare` 明确报 `unsupported_reference_adapter`），不进入 core/base/net 的生产依赖。

## 正常首战奖励前缀

`normal-two-combats` 使用同一入口和 `normal_combat_count=2`，在首场奖励关闭后继续真实合法选点并完成第二场；`normal-potion-reward@2` 保留实际领取并新增领取后丢弃用例。领取例设置 `require_potion_acquisition=true`，两侧均须实际执行TakeRewardPotion且奖励关闭后库存非空；`normal_reward_take_discard` 策略要求真实领取、丢弃和最终空库存，无掉落不能通过。参考通过公开PotionCmd.Discard执行丢弃，战斗中用CanUseManually保留原生UsePotion候选。`normal-held-potion` 已接通AttackPotion持药续战；`normal-potion-use` 通过原生PotionCmd.Use及选择适配实际执行使用/取消，不能省略未支持使用候选制造通过。

`normal-aroma` 在同一原seed的两战领取路线后进入首个合法Unknown节点，要求真实选中AromaOfChaos，分别执行 `normal_aroma_upgrade` 与 `normal_aroma_transform`，完成选牌并回到旅行。参考使用RoomFactory/EventRoom/EventModel.ChooseOption，公开RunDriver构造只安装外部决策源，不推进游戏。候选按本地永久牌组位置对应，另外以 `deck.origins.order` 保留各侧原生实例的首次出现编号，移除后不复用、不按结果牌名重匹配。事件拥有的Rng对象按源事件身份显式映射到比较流；反例验证实例来源与事件随机消费错误会失败。

`normal-post-event` 在两条事件分支后继续第三场正常战斗并关闭真实奖励，策略为 `normal_aroma_upgrade_next_combat` / `normal_aroma_transform_next_combat`，`normal_combat_count=3`。事件后优先公开Power牌，随后沿Bash/Strike/Defend的可见策略；不读取隐藏来源作选择。用例必须实际打出事件改过的牌，并验证每张永久牌的战斗副本、结算后的永久实例/形态不被临时变化覆盖。必需维度 `combat-copy-origins` 要求所有战斗边界具备五个牌区的来源事实；两侧通过LongTermSource/DeckVersion独立读取。调换同定义牌的来源编号也会被比较器拒绝。

`normal-potion-capacity`在原seed继续五场Monster及一场真实Elite，旧字段`normal_combat_count=6`计战斗房间总数。Aroma升级公开Bash；优先FlameBarrier、可从公开弃牌找回FlameBarrier的Headbutt、Power及Bash/Strike/Defend，目标取公开最低HP。三瓶由前三次真实掉落取得，精英再掉落时分别跳过或丢弃中间瓶后领取同一实例；断言槽位0/2不变、奖励来源不变、操作无额外随机，以及真实精英遗物显式跳过后整组奖励关闭。药水语义token按当前非空库存位置编号，实际槽位与实例来源另行比较。参考保留实际Elite房型、原生遗物/额外奖励和原生选牌，未知额外奖励明确失败。Power身份映射只共享定义名/ID对应，不共享任何玩法结果。

使用 [normal-reward-prefix suite](suites/normal-reward-prefix.json) 检查正常首战胜利、真实奖励生成、领取或跳过以及永久资源提交。它独立于原 regression17，不能用受控机制结果替代正常 Run。实现入口为 [base 与共同可见候选策略](NormalRewardEntry.cs) 和 [独立参考生命周期](NormalRewardReference.cs)。

本套件要求 `formal-dev-factory`、`dev-map`、原始字符串 seed、空 setup，以及一个 `normal_reward_take` 或 `normal_reward_skip` 策略动作。base 从可见合法候选生成一条动作计划：选坐标序数最小的合法 Monster 点，优先 Bash、Strike、Defend，同优先级选手牌槽位序数最小者；记录实际目标决策。参考独立执行同一动作、当地牌槽和目标语义，每步提交前核对完整合法候选、可见牌定义和奖励候选。缺失、多义或不合法时停止，不回退到参考侧首项。奖励顺序为首张牌领取或跳过、存在药水时领取或跳过、领取金币。`semantic_actions` 保存共同计划，边界 kind/label 保存实际展开动作；候选集合、牌定义和目标篡改负例必须在提交前被拒绝。

base 仅由正式工厂创建并 Start 一次，所有变化来自合法提交。参考使用正常 RunState、Player、MapTravel、RoomFactory、CombatRoom；实际战胜后调用 `ResolveOutcomeAsync(true)`，再调用真实 `SelectOption`、`Take`、`Skip`。不直接调用奖励生成函数代替战斗。两侧事实包括完整地图点/连线、合法后继旅行候选、战斗牌区/能力、长期金币/Hp/MaxHp、永久牌组来源与升级、药水容量/库存、奖励金额/牌序/稀有度/升级/药水存在及解决状态。牌组位置用于本次候选选择，跨变换身份由独立来源编号保留；当前只覆盖已登记路线，不代表全部移除/变换、附着或下一场复制来源验收。

额外必需维度 `rewards`、`permanent-inventory`、`reward-closure` 分别要求真实奖励事实、每个边界都有长期资源/库存事实，以及最后边界为 travel-ready 且有合法旅行候选。默认一场用例停在奖励关闭后；两场用例会提交后继旅行并比较第二场初始化与结算。原 `run` 命令仍是旧受控探针，不具有本套件的正常奖励语义。

本入口两侧均使用 v0.109 派生。参考使用原生 RunState，不调用 `RngSourceCompat.Apply`，通过公开 CurrentAncientEventType 完成其原生惰性预生成后记录首次边界。边界 Streams 仅记录已消费流的累计状态；地图流按其明确诊断名映射，其他动态流保留证据。战斗中 player.hp 是实时值，run.hp 为永久提交投影，在参考真实胜利结算后更新。

共享固定闭包验证时，不用本工具生产 ProjectReference 重编 base/reference。在仓库根目录运行本包隔离工程和它的 DLL：

```powershell
dotnet build .audit/dsh-normal-reward-diff-20261013/verify/DifferentialVerify.csproj
dotnet .artifacts/parallel/dsh-normal-reward-diff-20261013/verify/bin/Debug/net9.0/Differential.dll test --suite regression --output .artifacts/parallel/dsh-normal-reward-diff-20261013/final/regression
dotnet .artifacts/parallel/dsh-normal-reward-diff-20261013/verify/bin/Debug/net9.0/Differential.dll test --suite normal-reward-prefix --output .artifacts/parallel/dsh-normal-reward-diff-20261013/final/normal-reward-prefix
```

固定闭包缺失时先由负责人提供并核对摘要，不能改用开发中的 base 构建。本包的 [外部 records 判别脚本](../../.audit/dsh-normal-reward-diff-20261013/check-record-negatives.ps1) 从上述实际 base 记录制作明确标注的同侧比较器探针，检查奖励牌、永久写回、缺奖励维度、异常与缺参考均非零；这些探针不是独立参考成功证据。

## 命令状态

`init-upfront` 额外写出 `startup-consumption-trace.json`：参考观察既有原生 draw observer，在实际消费前用独立 `CloneExact()` 预测样本并记录前状态指纹/调用栈；原对象不补抽。base 正常 `dev` 工厂仍 `CreateSession → Start` 一次，工具另以同一不可变 Rules、公开种子与配置建立带观察的 Session，明确标为 instrumented replay，并要求其 facts 和 RNG diagnostics 与真实工厂完全一致、调度轨迹无截断；不一致返回 1。该重放不是受控设牌，也不能代替正常 Run 的 suite 证据。参考逐次轨迹仅覆盖 12 条 run 流（本 seed 实际涉及 UpFront/Niche/Shuffle）；玩家/临时/怪物 RNG 不据此声称全覆盖。洗牌前输入在各自独立入口首次 Shuffle 消费前读取。诊断只在工具文件出口，不进入训练观察。计数相同或该诊断 rc0 都不表示开局双侧通过；仍须原严格 suite 判定。

| 命令 | 状态 | 说明 |
| --- | --- | --- |
| `inventory` | **已实现** | 冻结 source/reference/生产源码摘要，生成原版完整分母与本项目侧状态表 |
| `compare` | **已实现（两侧）** | 夹具 → base 侧记录 + 参考侧记录 → 逐边界比较 → 首差报告；比较器自检 6/6 |
| `test` | **已实现（严格入口）** | ENV-T04：只接受声明 `expected_case_ids` 的**严格 suite**；缺 case/零执行/缺参考/unsupported/异常/超时/首差/错误终点/缺维度都非零；原 `fixtures-v1` 的 7 个可执行 case + 5 个曾判 unsupported 的原 ID 全部保留在 `suites/regression.json`（5 个原 ID 均按原语义**原地升级**，最后一个 `random-reshuffle-unsupported` 于 2026-10-12 升级为 `vanilla-compatible@1`） |
| `rng` | **已实现** | 用参考实现重算 `tools/rng-oracle` 的原版随机向量，交叉核对神谕没有读偏源码 |
| `init-upfront` | **已实现（只读诊断）** | ENV-T04：同 seed 参考开局逐阶段读 `Rng.UpFront.Counter`，并用 `CloneExact()` 独立副本按 `RunState.cs:271-353` 原文重放；另输出逐次 run 流样本、调用路径及初次洗牌前输入，与真实 base 工厂的单列诊断重放核对；不改生产状态/算法 |
| `verify` | **已实现** | 把 `inventory` + `compare` 结算成 `coverage.json` / `environment-manifest.json`，逐条列出未关闭的门 |
| `run` | **部分实现**（退出码 1） | 最小可测整局流程：一场真实战斗 + **战斗结束写回整局**（`Run.Hp`/`SettledRoom`/`BattleSettled` 三项写入），并把整局侧缺口从 `verify` 的 coverage 分类数字带出；地图/房间/奖励尚未实现（退出码 2 = 流程失败） |

退出码（`compare`/`test` 之外的命令沿用旧约定）：`0` 成功且没有**可计入**的首差、`1` 自检失败/可计入的首差/随机向量未登记偏离/环境未完成、`2` 用法错误或套件不可载入、`3` 未实现或参考不可用。

严格入口 `test` 的退出码另有细分（见下节）：`0` 完整 suite 每个 case 都是双侧 `passed` 或（仅 `kind=wiring`）单侧 `self_checked`，且都过了声明的必需维度/预期终点、比较器自检通过；`1` 有 case 不计通过或只跑了子集；`2` suite 定义/用法错误（含缺 case、未注册 case、零执行）；`3` 参考适配器不可用。

```powershell
# 仓库根目录
dotnet run --project tools/differential -- inventory --output artifacts/environment/inventory
dotnet run --project tools/differential -- compare --suite fixtures-v1 --output artifacts/environment/fixtures
dotnet run --project tools/differential -- rng
dotnet run --project tools/differential -- verify --output artifacts/environment/verify
# 用别处的参考记录比较（例如另一台机器的冻结产物）：
dotnet run --project tools/differential -- compare --suite fixtures-v1 --records <参考记录目录> --output artifacts/environment/fixtures
# 严格 regression（ENV-T04；薄脚本一次构建后跑同一入口）：
pwsh -File tools/differential/test-env.ps1 -Suite regression
dotnet run --project tools/differential -- test --suite regression --output artifacts/environment/tests/regression
```

Windows PowerShell 5.1 没有 `pwsh` 时用 `powershell -NoProfile -ExecutionPolicy Bypass -File tools\differential\test-env.ps1 -Suite regression`；
`test-env.ps1` 内容保持 ASCII-only，正是为了在 5.1 的 ANSI 解码下不被破坏。

## 严格入口（`test --suite regression`）

`compare` 是探索性入口（宽松 suite 也能跑，缺参考只记一行）；`test` 是**严格入口**，两者共用同一套
`BaseBackend` / `ReferenceBackends` / `Comparator` / `ComparatorSelfTest`，区别只在判定口径：

- **只接受严格 suite**：必须声明 `expected_case_ids`（必测分母）、`target_scope`、`build_identity`，
  以及每个 case 的 `kind` / `base_source` / `base_scope`（可省略，默认 `dev`）/ `initialization` /
  `contents` / `required_dimensions` / `semantic_actions` / `expected_terminal` / `reference_source`。
  `kind=map` 额外要求 `base_source=formal-dev-factory` 且给出字符串 `game_seed`（地图必须来自真实工厂的
  正常开局，不能是受控夹具）。
- **定义层失败（退出码 2）**：suite 不可载入、缺 case（分母里有 ID 没有可执行夹具）、未注册 case
  （有 case 没进分母）、注册字段不合法、**零执行**（`--only` 没有匹配到任何 case）。
- **执行层失败（退出码 1）**：`diverged`、`unsupported_rng_profile`、`unsupported_decision_model`、
  `blocked_compare`、`missing_reference_record`、`reference_fault`、`fault`、`invalid_fixture`、
  `invalid_action`、`wiring_failed`、`missing_dimension`、`wrong_terminal`、`timeout`，
  以及比较器自检失败、只跑了子集。
- **参考不可用（退出码 3）**：本机构建未包含 `reference/sts2-sim`（条件引用），`compared` case 全部判
  `unsupported_reference_adapter`。
- **必需维度是实测的**：case 声明了 `required_dimensions` 就必须在记录里真的抽取出来（`actions` /
  `decisions` / `facts` / `pile-order` / `power` / `streams` / `trace` / `terminal`），否则判
  `missing_dimension`。**两侧都跑时两侧都查**——因此只声明两侧都真的有的维度（参考侧没有随机流
  `streams` 与 `trace`、也没有终局投影，`terminal` 只在 base-only case 上声明）。缺维度只如实列出，
  不给未抽取的维度补占位读数。
- **预期终点**：`running` / `victory` / `defeat` 按 base 侧实际会话状态核对，不符即 `wrong_terminal`。
  参考侧没有终局（胜负）投影，所以这一项是 base 侧断言，不是双侧比较——报告里写明这条不对称。
- **门槛覆盖所有可计成功结果**：`passed` 与 `self_checked` 都要过上面的必需维度/预期终点校验；
  只有在两者之后仍成立才计成功。更具体的失败（`faulted` / `timeout` / `invalid_action` /
  `invalid_fixture` / `wiring_failed` / `unsupported_*` / `blocked_compare` / `reference_fault` /
  `missing_reference_record` / `diverged`）保持原判定，不被这条门槛覆盖，分母也不因此改变。
- **`self_checked`**：只用于 `kind=wiring` 的工具/生产集成自检（唯一工厂的范围拒绝、未启动会话零随机零玩家、
  配置在 Start 前就位）。它的验收标准是自身实测断言，**不计入双侧 `passed`**，报告与计数单列；
  但它的语义只是"不要求双侧比较"，**不是**免除预登记断言——声明了 `required_dimensions` /
  `expected_terminal` 就必须真的成立，否则改判 `missing_dimension` / `wrong_terminal`，不计成功。
- **`kind=map`**（地图生成机制，2026-10-09 ENV-T04 地图包）：base 侧走唯一正式工厂的**正常开局**
  （`base_scope=dev-map`），事实只从引擎公开投影 `base-spire:MapNodes` / `base-spire:MapChildren` 读，
  流只投影 `base-spire:run:act_1_map`；参考侧走独立地图生成入口（`ReferenceMapEntry`），只生成被比较的
  那一张地图（不建 RunState、不复制 base 结果，也不拿 `SourceActMapCompute` 的重放结果自证形状）。
  比较单位是逐条事实路径：每个 `map.node.[c,r].type` 与每条 `map.edge.[pc,pr]->[cc,cr]`，**不是只比数量**；
  `map.second_boss` 在 base 无二王概念时记 `none`。流维度按**底层 64 位字数**对齐（参考侧的 API 调用数
  不是同一单位：用源兼容模型逐步对齐生成器状态求字数，预算内对不上记 -1 并如实报差异）。
  地图 case 的 `actions` 传空表（参考侧没有 `ChooseMapPoint` 的对应物），所以**不要**给它声明 `actions`
  维度——那是判别负例 `suite-map-missing-dimension.json` 要抓的错。它只覆盖地图生成机制：
  **不证明多房间推进/奖励/结算跑通**。
- **`--only` 语义**：定向子集只报告该子集、报告里 `complete=false`，且**永远非零**，所以不能替代完整阶段；
  默认执行 suite 全部 case。
- **超时保护**：每 case 默认 120 秒（`--case-timeout-ms`），超时判 `timeout` 并继续下一个 case，不伪造终局。
  超时的后台任务不会被打断（引擎当前没有取消接口），进程结束时随进程退出——报告里如实写明，不假装取消了。

产物：`<output>/regression.json`（机器）、`<output>/regression.md`（人读）、`records/base/*.json`、
`records/reference/*.json`；报告带 `run_id`、suite 摘要、构建身份、逐 case 判定/首差/维度/终点与退出码。

### Headbutt 实际选牌定向检查

原 `headbutt-mid-choice` 保留 ID、目标 `enemy:0`、选择 `card:discard:0` 和 `actions/decisions/facts` 必需维度；弃牌增加第二个候选以触发参考实际决策源。参考 `CardSelectCmd` 对不可取消的单候选会自动返回，并非 combat 源不可注入。

脚本步骤可声明 `observe_draw_top: true`：只在该步骤结算边界，两侧分别读取自己的 `pile.draw.top`。不修改牌堆，也不比较受控开局的随机顺序。牌区构成仍逐边界比较。

共享工作区使用隔离构建并运行该目录 DLL：

```powershell
dotnet build tools/differential --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-headbutt
dotnet .artifacts/parallel/dsh-headbutt/bin/Differential/debug/Differential.dll test --suite regression --only headbutt-mid-choice --output .audit/dsh-headbutt-choice-2026-10-11/positive
powershell -NoProfile -ExecutionPolicy Bypass -File tools/differential/check-headbutt-choice.ps1
```

[定向检查脚本](check-headbutt-choice.ps1) 要求审计目录已有原 suite 快照与 positive 记录；核对两侧实际目标/选牌、数量约束、置顶及结算牌区，确认原 17 分母和其他 16 定义不变。再用改变选择的输入独立执行参考，把该参考记录与原输入比较，要求 `diverged` 和返回码 1。负例不编辑执行记录、不复制 base 答案。

定向 `test` 返回 1 是严格契约的一部分：`complete=false`；仅无随机机制子集也可能缺 stream 自检目标。不能把它解释为全 suite 验收，或为此放宽自检。

### Vulnerable 真实施加路径定向审计（2026-10-11）

`vulnerable-decay-via-bash` 保留原 ID、Bash→enemy:0 / end_turn 脚本、actions/decisions/facts/power 必需维度与 17-case 分母。独立 base/reference 各执行一次、各 3 个边界：无能力→Vulnerable 2→1，Bash 后敌人 HP 38→30。源码 `VulnerablePower.AfterSideTurnEnd` 只在 Enemy 侧递减。

前一包结果（受控初始化交付前）为 `unsupported_rng_profile`：当时 base 实测脚本没有新增随机消费，reference 的 false 未经过独立计数；boundary[2].pile.draw.sorted 的首差来自未声明的私有初始牌序。ENV-T04 本包现以完整声明独立构造并读取双方计数，原 Vulnerable 单 case 双侧 passed，真实 0→2→1 和全部牌区事实均保留；完整 suite 证据见 `.audit/dsh-controlled-init-2026-10-11/full-regression`。`mutant/vulnerable-duration` 仍变异最终 stacks 1→2 并捕获 power 字段首差，属于同侧比较器负例。stream 自检原样保留，单 case 因缺 stream 目标失败，由完整 suite 提供目标。

```powershell
dotnet build tools/differential --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-vulnerable
dotnet .artifacts/parallel/dsh-vulnerable/bin/Differential/debug/Differential.dll test --suite regression --only vulnerable-decay-via-bash --output .audit/dsh-vulnerable-decay-2026-10-11/final-strict
```

构建 rc=0；定向执行 rc=1，complete=false，不是完整 suite 通过。原脚本无衰减后攻击，也没有分离的玩家侧/敌方侧中间边界，因此后续伤害效果、侧结束时序的完整动态核对未验证；不扩展其他包。

### 重洗比较（2026-10-11）

`random-reshuffle-unsupported` 保留原 ID/seed/维度/预期终点（`unsupported_rng_profile` 声明已于 2026-10-12 原地升级为 `vanilla-compatible@1`，见下下节；本节记录升级前的诊断）。原证据在 `boundary[3].pile.draw.initial-slots` 首差（base `start:05,00,08,03,07` ≠ reference `start:03,06,04,09,01`）里混了两个原因，本方法把工具侧原因去掉、把玩法侧原因定到具体方法。

**工具侧：受控状态注入后的随机游标对齐。** 受控 case 由夹具声明精确牌区。base 受控切片按声明顺序发牌（`CombatSlicePackage` "受控切片按声明顺序发牌"），`Shuffle` 流消费 0；参考侧战斗开始是它自己的原生流程（`Player.PopulateCombatState → DrawPile.RandomizeOrderInternal → UnstableShuffle`），必然先用 `Shuffle` 流洗一次开局牌堆（10 张 = 9 次），随后适配器的 `ApplyOrdered` 只把**牌序**覆盖成声明顺序，**游标不会回来**。于是脚本开始前参考的 `Shuffle` 游标比 base 多 9，重洗比较在这个纯工具侧偏移上分叉。`RngSourceCompat.RestoreStream` 只把这一条流按 `source-compat-sts2-frozen@1` 重新装回种子（游标 9→0），调用点与前后游标写进参考记录 `notes`，不做静默对齐；不碰其他流、不换算法/seed/维度/Profile/牌区声明。对齐后参考重洗消费的 9 次前缀指纹与 base `base-spire:combat-draw` 的 0→9 指纹相同（`0x268d319817e8d9b3`），除该 case 的重洗牌序外，其余 16 case 判定/首差/维度/终点均不变（差异只有新增的 `notes` 与 `reference_identity.profile_code_sha256`——后者是本文件与 `RngSourceCompat.cs` 的摘要）。

**玩法侧：base 缺源 `StableShuffle` 的预排序。** 源 `CardPileCmd.Shuffle` 先取 `discard + drawPile` 再 `list.StableShuffle(player.RunState.Rng.Shuffle)`，而 `StableShuffle` 按 `CardModel.CompareTo`（`ModelId` 先 `Category` 后 `Entry` 定序，同 Id 再按升级等级）**先排序再 Fisher-Yates**。base 的 `CardDrawProcedures.Reshuffle` → `ShuffleCollection(piles.Draw, DrawStream)` → core `StateStore.ShuffleCollection` 直接在弃牌堆的追加顺序上洗牌，没有这一步排序。同 seed 下两侧洗牌输入顺序因此不同：base 输入 `start:00..09`（弃牌顺序），参考输入 `start:09,05,06,07,08,00,01,02,03,04`（Id 排序 + 同级保持原相对次序）。用源随机模型独立复算（`RngSourceCompat.DeterministicHashCode`/`Xoshiro256StarStar`/descending Fisher–Yates）可以逐值复现两侧输出：base 游标 0 + 弃牌顺序得 `09,01,02,04,06,05,00,08,03,07`（= 实测），参考游标 9 + 排序输入得 `02,08,07,05,00,03,06,04,09,01`（= 实测）；把游标对齐到 0 后参考得 `04,05,06,08,01,00,09,03,07,02`（= 对齐后实测）。游标对齐仍不相等，说明残余首差就是这条排序缺口。

```powershell
# 只构建本包工具专用验证工程（Reference 指向固定闭包，不改生产 csproj）
dotnet build .audit/dsh-reshuffle-followup-20261011/verify/DifferentialVerify.csproj
dotnet .artifacts/parallel/dsh-reshuffle-followup-20261011/bin/Debug/net9.0/Differential.dll test --suite regression --output .audit/dsh-reshuffle-followup-20261011/aligned
```

`RestoreStream` 只服务受控注入 case（`setup.initial_piles` 非空）；其他 run 级流仍保留参考原生消费（本项目受控探针未用到的流不做对齐），也只覆盖该 seed 实际触及的 `Shuffle`。base 侧排序缺口本身不在工具内修：base 已有稳定 `Sort` 查询可用，但排序键必须复刻源 `ModelId` 定序（`Category`+`Entry` 序，base 内容 id 的字典序与之不必然相同），且源 `List.Sort` 在 16 张以上走内省排序、同级次序不保证与稳定排序一致。真实重洗 case 仍未通过，直到 base 补上这一步。

### 参考侧逐边界随机流（2026-10-12）

本方法补齐参考侧**受控机制逐稳定边界的真实随机流快照**：`ReferenceBackend.Add` 过去给
`BoundaryRecord.Streams` 传空表，于是 `random-reshuffle-unsupported` 的参考记录一直是
`missing_dimensions=[streams(reference)]`——重洗缺的不只是"结论"，而是**可比维度的读数本身**。
现在每个边界在**边界时刻**从实际参考 `Rng` 状态读 `Counter` 与状态指纹，并按**两类身份**分开标识：

| 类别 | 键 | 单位 | 参与比较 |
| --- | --- | --- | --- |
| 已映射具名流 | 显式映射后的 base 引擎流名（`RngSourceCompat.EngineStreamName`） | 该生成器自播种以来的原生调用数 | 是（`Comparable=true`，且只在计数器有增量时收录） |
| 动态/临时 RNG | `observed:<槽位>:<原始诊断名>`（保留原始名与独立槽位） | 同上，但对象是运行期构造的 | 否（`Comparable=false`，只作证据） |

- **不按 seed 猜同一流。** 初始化期有一个 seed 恰好等于 `Shuffle` 的临时流（`observed:3`，指纹
  `0x268d319817e8d9b3`），它与具名 `Shuffle` 的后代不同对象；本方法按**对象身份**分列，不合并槽位。
- **逐边界是增量视图，不是累计末态。** 具名流按"自上一个读数以来计数器有增量"收录；动态 RNG 同样按
  "自上一个读数以来又被推进"收录，所以初始化期创建、之后不再动的临时流不会在每个边界被重抄一遍。
- **初始化与受控 Restore 前后单列**（`CaseRecord.ReferenceStreams`：`initialization` / `restore_before` /
  `restore_after` / `boundaries`）。`RestoreStream` 换的是**对象**（`RunRngSet.MockRng` =
  `_rngs[type] = new Rng(seed)`），所以对齐之后必须把基线重挂到新对象；否则新对象会被当成"从未见过"
  而在每个边界重复出现（2026-10-12 调试中实测到的假象）。
- **显式映射必须落在 base 真实使用的键上。** 本包修了 `RngSourceCompat.EngineStreamName`：
  `CombatTargets` / `CombatCardSelection` / `CombatCardGeneration` 原来自写成 `base-spire:random-target`、
  `base-spire:random-card`、`base-spire:run:combat_card_generation`，而 base 在用的常量是
  `SliceManifest.RandomTargetStream`（`base-spire:combat-random-target`）、
  `RandomCardStream`（`base-spire:combat-random-card`）、`CardGenerationStream`（`base-spire:combat-card-generation`）。
  现在这四条直接引用 base 的公开常量，编译期绑死；只在真的消费了这些流时才会显形（今天没有 case 消费，
  属**预防性对齐**）。`RngSourceCompat.UnverifiedKeyNames` 另列 6 条**尚未核对到引擎常量**的合成键
  （`UnknownMapPoint`/`CombatPotionGeneration`/`CombatEnergyCosts`/`MonsterAi`/`CombatOrbs`/`TreasureRoomRelics`）：
  它们同样是"一旦被消费才显形"的隐患，本包只如实登记，不猜 base 的名字、不改 base 常量。
  该历史说明中MonsterAi归属有误：当前参考的MonsterModel.RollMove经CombatState.MonsterAiRng在顺序模式使用RunRngSet.MonsterAi；地图仍为独立派生流。两场正常战斗已验证共享MonsterAi计数/指纹。
- **缺维度门槛收紧到"真有具名流读数"**：`streams` 维度要求某个边界存在 `Draws > 0` 的具名流；
  `TestCommand` 另加流证据自洽检查——声明 `rng_consumed_by_script` 却没有任何具名流边界读数（缺证据而非零消费），
  或脚本期间**实际增长**的具名流在逐边界读数里覆盖不到，都判 `missing_dimension`。初始化期消费的具名流不算脚本消费。
  两条都不补 0、不放宽。

```powershell
# 只构建本包工具专用验证工程（Reference 指向固定闭包，不改生产 csproj）
dotnet build .audit/dsh-reference-stream-boundaries-20261012/verify/DifferentialVerify.csproj
# 定向重洗（本包主 case）+ 完整 17 分母
dotnet .artifacts/parallel/dsh-reference-stream-boundaries-20261012/bin/Debug/net9.0/Differential.dll test --suite regression --only random-reshuffle-unsupported --output .audit/dsh-reference-stream-boundaries-20261012/targeted-reshuffle
dotnet .artifacts/parallel/dsh-reference-stream-boundaries-20261012/bin/Debug/net9.0/Differential.dll test --suite regression --output .audit/dsh-reference-stream-boundaries-20261012/full-regression
# 负例：去掉参考记录的逐边界流读数后必须真报缺维度（rc 1）
dotnet .artifacts/parallel/dsh-reference-stream-boundaries-20261012/bin/Debug/net9.0/Differential.dll test --suite regression --only random-reshuffle-unsupported --records .audit/dsh-reference-stream-boundaries-20261012/negative-missing-streams/records --output .audit/dsh-reference-stream-boundaries-20261012/negative-missing-streams
```

实测（固定闭包 `.artifacts/parallel/owner-project-review-20261012/bin/Differential/debug`，全部三条 rc=1）：

- 完整 17：`expected 17 / executed 17 / passed 15 / self_checked 1 / unsupported_rng_profile 1`，自检 22 项 0 失败
  ——与原严格 17 的判定、首差、维度、终点完全一致（base 记录逐字节未变）。
- `random-reshuffle-unsupported`：`missing_dimensions` 从 `streams(reference)` 变为空；边界 3 两侧同键
  `base-spire:combat-draw` 都是 `draws=9, fp=0x268d319817e8d9b3`；**首差仍是** `boundary[3].pile.draw.initial-slots`
  （base 缺源 `StableShuffle` 预排序），Profile 仍是 `unsupported_rng_profile`，未被工具放宽。
  （**已被下一节取代**：源预排序交付后该 case 于同日原地升级为 `vanilla-compatible@1`，上面这组数字是升级**前**的
  参考流包证据。）
- 负例（逐边界流读数清空、删掉 `reference_streams`）：`missing=[streams(reference)]`，rc=1。

### 重洗严格升级（2026-10-12）

`random-reshuffle-unsupported` **原地升级**到现有 `vanilla-compatible@1`：ID、`game_seed`、3 个 `end_turn`、
预期终点 `running`、内容/变体与 `actions/facts/pile-order/streams` 四个必需维度**全部不变**，分母仍是 17。
升级的前提是源条件全部到位——源 `StableShuffle` 预排序（ENV-T06）与参考侧逐边界具名流读数（上一节）——
并且三条口径按只读源与**实际边界**核对过，不是"同 seed 猜流"：

| 口径 | 依据 | 实际边界核实 |
| --- | --- | --- |
| **Shuffle 映射** | `sts2/src/Core/Commands/CardPileCmd.cs:876` 与 `reference/.../Commands/CardPileCmd.cs:387` 的重洗都取 run 级 `RunState.Rng.Shuffle`；base 把该流具名为 `SliceManifest.DrawStream`（`base-spire:combat-draw`），`RngSourceCompat.EngineStreamName("Shuffle")` 直接引用该常量 | 两侧边界 3 的具名流都只有 `base-spire:combat-draw` |
| **单位** | base `RandomStream.Next()` 每次 `Draws++`（raw generator word）；参考 `Rng.Counter` 每次原生调用 +1；10 张牌降序 Fisher–Yates = 9 次 | 两侧 `draws=9`、指纹 `0x268d319817e8d9b3`，与两份独立模型（本包脚本 + `tools/rng-oracle`）逐值相同 |
| **Restore** | `reference/.../Runs/RunRngSet.cs:265-268` 的 `MockRng` 是**换对象**（`_rngs[type] = new Rng(seed)`，计数器归零）；`ReferenceRandomProbe.Restore` 之后重挂基线 | `restore_before.combat-draw=9` → `restore_after=0`；初始化期那个同 seed 的临时流留在 `observed:3` 独立槽位（`Comparable=false`），**不**与具名流合并 |

实际边界（`boundary[3]`，第 3 次 `end_turn` 触发重洗）两侧逐值一致：手牌 `04,05,06,08,01`、抽牌堆
`00,09,03,07,02`，且都能由「源排序键 `BASH < DEFEND < STRIKE` 预排序 + 9 次 Fisher–Yates」独立复算出来。

完整 17 实测（固定闭包 `.artifacts/parallel/owner-horizontal-extension-20261012/bin/Differential/debug`，
先用该 DLL、再用本包临时工程编出的同一批源码复算，两次结论相同）：

| 运行 | 命令 | 原 rc | 实际执行 | 结果 |
| --- | --- | --- | --- | --- |
| 完整 17（固定 DLL，只改 suite 声明） | `dotnet <fixed>/Differential.dll test --suite regression --output .artifacts/parallel/dsh-strict-reshuffle-close-20261012/full-regression-fixed-dll` | **0** | expected 17 / selected 17 / executed 17 | 双侧 passed **16** / self_checked 1 / 不计通过 0，自检 22 项 0 失败；`random-reshuffle-unsupported` = `passed`（4 边界、`missing_dimensions=[]`） |
| 完整 17（本包临时工程，含一条 note 文案修正） | `dotnet <this package>/Differential.dll test --suite regression --output .artifacts/parallel/dsh-strict-reshuffle-close-20261012/full-regression` | **0** | expected 17 / executed 17 | 同上；base 记录与固定 DLL 版**只差 `random-reshuffle-unsupported` 的一条 notes 文案**，其余 16 base + 16 reference 记录逐字节相同 |

两个**完整 suite**（`complete=true`，不是 `--only`）反例，都复用现有 `--records` 外部参考记录通路与同一个
Comparator，没有新增 suite case、没有改比较器、没有加历史矩阵：

| 反例 | 只改参考记录的这一处 | 原 rc | 结果 |
| --- | --- | --- | --- |
| 牌序变化 | `boundary[3].pile.draw.order` 换一个**纯置换**（前两项对调；多重集/条数/`sorted` 集合都不变） | **1** | `diverged`，唯一首差 `boundary[3].pile.draw.order`（`collection`），最后一致边界 `2:step:1:end_turn`；passed 15 / self_checked 1 / 不计通过 1。若顺序没被真的比较，这次会是 rc 0 |
| 参考 Shuffle 边界流缺失 | `boundaries[*].streams` 清空（参考侧不再有真实具名流读数） | **1** | `verdict=diverged`、首差 `boundary[3].streams[base-spire:combat-draw].present`（`存在 ≠ 缺失`），且 `missing_dimensions=["streams(reference)"]` 如实列出 |

**没有为了绿改其他 case**：另外 16 个 case 的判定、首差、维度、终点与固定闭包基线逐字节一致；`rng-initial-shuffle`、
两个地图 case、`factory-wiring-dev-scope` 与全部受控机制 case 都保持原状。真实重洗之外**未验证**的部分（见下节）
不受本升级影响：本 case 仍只是受控单场机制对拍，不声称正常 Run / 奖励 / 四幕双侧。

### 失败契约反例（工具验收，不算产品覆盖）

`fixtures/negative/` 是有限的一组反例 suite，只用于证明严格入口的失败契约；它们不是产品 case，也不进
`suites/regression.json` 的分母：

| 反例 | 现象 | 期望 |
| --- | --- | --- |
| `suite-missing-case.json` | 分母里的 ID 没有可执行夹具 | rc 2，点名缺失 ID |
| `suite-unregistered-case.json` | suite 里有 case 没进分母 | rc 2，点名未注册 ID |
| （`--only no-such-case`） | 零执行 | rc 2 |
| （`--only` 一个 unsupported 原 case） | unsupported 不计通过 | rc 1 |
| `suite-script-stuck.json` | 脚本打出不在手牌里的牌（异常路径） | rc 1，verdict `invalid_action` |
| `suite-missing-dimension.json` | 声明 `terminal` 但没有终局 | rc 1，verdict `missing_dimension` |
| `suite-wrong-terminal.json` | 登记 victory 实际运行中 | rc 1，verdict `wrong_terminal` |
| `suite-wiring-missing-dimension.json` | **完整**注册 suite（8 个正常双侧 case + 工厂接线 case，全部分母内、不用 `--only`）：工厂声明 `terminal` 维度但实际没有终局 | 修复前 rc 0（`self_checked` 绕过门槛）；修复后 rc 1，工厂 verdict `missing_dimension`，其余 8 个仍 `passed`、自检 6/6 |
| `suite-wiring-wrong-terminal.json` | 同上完整 suite，工厂维度齐备但 `expected_terminal` 错登记为 `defeat`（实际 Running） | 修复前 rc 0；修复后 rc 1，工厂 verdict `wrong_terminal`，其余 8 个仍 `passed`、自检 6/6 |
| `suite-map-missing-dimension.json` | **完整**注册 suite（8 个正常双侧 case + 地图 case，全部分母内、不用 `--only`）：地图 case 声明 `actions` 维度，但地图路径故意不产出动作 | rc 1，地图 verdict `missing_dimension`，其余 8 个仍 `passed`、自检 6/6 |
| `suite-map-wrong-terminal.json` | 同上完整 suite，地图 case 的 `expected_terminal` 错登记为 `victory`（实际 Running、terminal 为空） | rc 1，地图 verdict `wrong_terminal`，其余 8 个仍 `passed`、自检 6/6 |
| （`--case-timeout-ms 1`） | 超时 | rc 1，verdict `timeout` |
| （`--records` 空目录） | 缺参考记录 | rc 1，verdict `missing_reference_record` |
| （`--records` 指向另一个 case 的记录） | 外部参考记录 `case_id` 与当前 case 不一致 | rc 1，verdict `reference_fault`（拒绝按错误记录比较，不外推、不重新匹配） |
| （`--records` 受控变异的参考记录） | 人为首差 | rc 1，verdict `diverged` 并给出首差路径 |
| （`-p:ReferenceSimAvailable=false` 构建） | 参考适配器不可用 | rc 3 |

注意：单 case/退化反例里比较器自检会如实报"整批记录里没有该种类的可改目标"，因此那些运行的自检项会 FAIL；
自检是 suite 级门槛（需要真实 suite 的记录），不代表反例本身没生效——逐 case 的 verdict 行才是该反例的结论。
`suite-wiring-*.json` 两项与 `suite-map-*.json` 两项都是**完整** suite，记录齐备，自检必须 6/6 全过：
它们的非零只来自工厂/地图 case 的必需维度/预期终点门槛，不靠 `--only`、不靠没有双侧 `passed`、也不靠
自检失败。这四项是"门槛覆盖 `self_checked`"与"新维度不能靠声明就算比过"的判别用例
（2026-10-08 实测：wiring 两项修复前 rc 0、修复后 rc 1；2026-10-09 实测：map 两项 rc 1，只有被改的那条 FAIL）。

## 结算（verify）

把 `inventory.json`（完整分母 + 来源摘要）与 `comparison.json`（对拍判定）结算成两份产物：

- `coverage.json`：按类别的分母/已降级/未落地/四项未产生的列，外加**排除项逐条清单**；
- `environment-manifest.json`：绑定清单与产物的摘要、源/参考/生产源码的目录摘要、工具与夹具版本、以及门清单。

三条纪律写死在实现里：

1. **只有 `passed` 计通过**：`unsupported_*` / `not_run` / `not_checked` 一律算未覆盖；
2. **证据链要冻结**：每次重算 8 棵基线目录树的摘要并与清单比对，漂移逐条报出（实测：把某棵树摘要改掉 → `baseline_frozen` 转开放并点名该树）；
3. **不许用"测试很多、全绿"结算**：本命令不看测试数量，只看四态覆盖与对拍判定，且**总是**把未关闭的门列出来。
   未产生的列是 `null` 而不是 `0`；**连接键未按原版核对的类别 `settleable=false`、`uncovered=null`**——
   报 0 会被读成"全缺"，报分母会被读成"已覆盖"，两者都是误导。

**当前结算结果（2026-10-04）**：`overall=incomplete`，门 **5 通过 / 7 开放**（rc=1 = 结算完成但环境未完成，不是工具故障）。
分母 **1567** = **已降级 69 + 仅旧后端注册 276 + 未落地 1222**（16 个类别全部满足这条恒等式，`coverage_consistent` 门守着它）；
**无法结算 0 条**（连接键已推广到全部类别）。开放的门：未落地内容、`engine_loaded`、`behavior_verified`、
`differential`、`host_public`、`vanilla-compatible` 随机 case、事件顺序轨迹。

### 对拍覆盖（`differential_coverage`）

`differential` 这一列不是"未做"一句带过，而是**量化**的：`verify` 结构化扫描 `records/base/*.json`，
只认三种真的承载内容 id 的事实位置——`pile.<牌区>.order/.sorted` 的值、`power.<拥有者>.<i>.id` 的值、
`enemy.<内容 id>.<字段>` 的路径——并排除引擎内置标识符（类型/集合/关系/动作/投影/随机流，
由 `inventory.engine_infrastructure` 从**真实构建的规则集**给出）。

**当前：已对拍内容 9 条 / 分母 1567**（其中 6 条出现在 `passed` case 里；`headbutt` 只在决策模型未对接的 case、
`ritual`/`strength` 只在随机未支持的 case 里出现）。这条数字把"完整环境还很远"说清楚了，
比"`differential=not_run`"有用得多。

> 口径修正（踩过的坑）：最初按文本抓 `base-spire:` 会把事实路径里的 id、槽位名、后端 id 一起抓进来，
> 把 9 条虚报成 56 条。现在是结构化提取，只认上面三种位置。

### 行为证据覆盖（`behavior_coverage`）

规则检查项目**自报**（`dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --report <目录>`
→ `slice-behavior-report.json`），结算工具只读产物，**不解析测试源码**：内容 id 清单是运行期契约，
解析源码会把注释、字符串常量、重构后的写法都当成事实。

自报同时做一致性检查：每个 id 必须真的在正式前端的**降级集**里，否则检查直接失败
（"声称验证了没加载的内容"会在源头暴露）。结算侧再做三层核对：落在分母内多少条、有没有超出分母、
有没有**跨类别同名**。

**当前：行为已验证 42 条**（分母 1567；CARD 41；跨类别同名 1 条、**已登记为例外**，见下）。

## 引擎加载（`engine_loaded`）

ENV-02 的第一步：把**正式源**（不是切片原型文本）组进可运行的 Engine 包——`base-spire` 新增
`FormalSpirePackage`（`Load()` / `Build()` / `BuildRuleSet()`），非卡部分沿用切片作者模型，
**卡牌与能力整体换成正式源**，卡组是"每张降级卡各 1 张"。

`inventory` 随即**从会话状态**读结果，而不是读声明：组包 → 链接成 `RuleSet` → `Session.Start()` →
枚举卡牌实例的 `DefinitionId`。**当前：声明 68 张 → 实例化 68 张，缺口 0**（能力不计入这一列：
能力实例只在被打出时创建，开局没有实例，按声明计入就是过度声称）。

**曾经的乐观数已修正并关闭**：`formal_frontend` 原先用宽表 `KnownIds` 做链接、数出 58 张，
而当时**已加载的能力源**只支持 57 张——差的 `BattleTrance` 引用的 `NoDraw` 被能力前端判为运行时未实现，
宽表却会把它链成 `base-spire:no-draw`（存在但没加载）。补齐 `no-draw` 之后两侧都是 **58 张**、
`link_orphans` 为空，门 `no_dangling_power_links` **通过**。

## 内容 id 命名空间与登记例外

`inventory` 同时做两件容易混淆的事，这里分开写清楚：

| 项 | 含义 | 当前 |
| --- | --- | --- |
| `cross_category_name_overlap` | **原版**命名层面的同名（卡 `X` 与能力 `XPower`）——原版就是这么命名的，**不是歧义**，反而解释了"能力为什么靠变体命中" | 136 条 |
| `id_exceptions.collisions` | **我们自己注册的 id** 里同一个字符串被两个类别注册——这才是要治理的命名空间歧义 | **1 条**：`base-spire:feel-no-pain`（CARD+POWER） |

登记表在 [`registries/id-exceptions.json`](registries/id-exceptions.json)，`inventory` 会与实测**双向核对**：
登记的例外必须仍然歧义（否则报 `stale`，登记表不能腐烂），真实歧义必须有登记（否则报 `unexplained`）。
`verify` 的 `no_unexplained_id_ambiguity` 门守着这两条。**当前：已登记 1、未解释 0、过期 0**（实测：往表里加一条
不存在的例外 → 立刻报 `stale`）。

`feel-no-pain` 的处置是**登记而不是改名**：卡牌与能力在运行时分属不同注册表与实体类型，不构成运行时冲突；
改内容 ID 会动 `.spire` 源、net 目录/Profile 身份、AI 侧编码与既有检查，属公共契约变更，须由规划者裁定。

### 连接键规则（全部类别）

规则是 `kebab(原版类型名)`（能力额外接受"去掉 `Power` 后缀"的变体），**并且是被测量的**：

| 测量项 | 含义 |
| --- | --- |
| `rule_suspect` | 规则匹配不上、但把名字里非字母数字去掉后能对上我们某条 id ⇒ 命名约定差异（不是未实现）；非空则 `join_rule_verified=false` |
| `rule_collision` | 两条不同原版内容映射到同一个 id ⇒ 规则有歧义 |
| `rule_variant_matched` | 主规则没命中、靠变体命中的条目 ⇒ 对应关系仍在，但**我们的 id 约定不统一** |
| `rule_evidence` | "原版类型名 ↔ 我们的 id"真实样本 |
| `identity_holds` | `已降级 + 仅旧后端 + 未落地 == 分母` |

**测量查实的内容缺陷**：POWER 有 **51/81** 条能力靠变体才命中——我们自己的能力 id 有的去 `Power` 后缀
（`vulnerable`、`weak`、`strength`），有的保留（`aggression-power`、`barricade-power`、`corruption-power`）。
对应关系没问题，但**命名约定不统一**，已作为内容缺陷登记（见 tofix 清单）。

## ENV-01 随机对照（rng）

原版随机的**独立神谕**在 [`tools/rng-oracle`](../rng-oracle/README.md)：只按 `sts2/src/Core/Random/MegaRandom.cs`
与 `Rng.cs` 重写（不参考任何 C# 实现），既出 JSON 也**生成** C# 向量表。

`rng` 用参考实现（第三份独立实现）重算同一批向量，核对有界映射、双精度、洗牌排列与消耗计数；
并核对**布局层**（字符串 seed → run/玩家/内容流种子）。它证明的是"神谕没把源码读偏"，
**不是**"我们与原版一致"——裁决依据始终是只读的 `sts2/` 源码（ENV §6.3）。
原始字一层没有参考侧的入口，只由 `Core.TheSpire.Engine.Tests` 对神谕逐值核对。

**已登记的参考偏离（重要）**：`reference/sts2-sim` 的检出**领先于**冻结的 `sts2/` 基线——
它的 `RunRngSet.Seed` 已是 `ulong` + **XxHash64**（`Runs/RunRngSet.cs:36,80`），
流种子还走 `CreateSemanticKeyRequired` 重键模式，而冻结基线是 `uint` + int 循环
（`sts2/src/Core/Runs/RunRngSet.cs:104-119`）。因此布局层 8/8 不一致，全部按**已登记偏离**分类、
返回 0；**不得**用参考的数值对齐我们的实现。未登记的偏离会让 `rng` 返回 1。
ENV-00 的待办因此多一条：把参考冻结到与 `sts2/` 同一版本，或逐处登记此类差异。

## ENV-T04 初始化消费诊断（`init-upfront`）

同一个 `game_seed` 下，两侧开局在 `up_front` 流上的**前置消费量**不同时（实测 base 18 / 参考 295），
只比较首个遭遇只会得到"遭遇不同"，定位不到来源。这条只读诊断把参考的初始化按阶段切开：

```powershell
# 仓库根目录
dotnet tools/differential/bin/Debug/net9.0/Differential.dll init-upfront --seed ENV-T04-RNG-SHUFFLE-001
```

- **阶段读数**：真实开局逐步（`run_constructed` → `profile_applied` → `player_created` → `player_added`
  → `room_created` → `room_pushed` → `room_entered`）读 `run.Rng.UpFront.Counter`；
  `RoomFactory.CreateRoom` 的遭遇闭包是惰性的，所以消费发生在 `room.Enter` 触发的整幕预生成，而不是建房间。
- **分解重放**：在 `profile_applied` 之后取 `run.Rng.UpFront.CloneExact()` 建**独立副本**
  （`Random/Rng.cs:93-99` 深拷贝，副本抽取不回写真实流），按 `Runs/RunState.cs:271-353` 原文重放
  `shared_relic_bag → character_relic_bag → shared_ancients_shuffle → act0_events_shuffle → act0_normal_slots
  → act0_elite_slots → act0_boss → act0_ancient`，逐阶段打印增量；总和与实测不符时**返回 1** 并明说
  「分解尚未覆盖全部消费点，不得据此下结论」。
- **输出**：`artifacts/environment/init-diagnosis/init-upfront-diagnosis.json`（阶段表、池规模、
  遗物袋按稀有度桶的期望抽取数、15 个普通槽的遭遇序列、两侧记录里的 `up_front` 次数）。
- **实测（2026-10-08，seed `ENV-T04-RNG-SHUFFLE-001`）**：观察 295 = 0/0/**230**/0/0/0/**65**；
  重放 112+118+0+30+16+17+1+1 = 295。`295 − 18 = 277` 因此是**参考独有阶段（279）减去普通槽阶段的
  2 次差**：两侧普通槽算法逐字一致，但参考的第一个普通槽抽取已在计数器 **260**（230+30 之后）、
  base 在 **0**，于是同一个弱袋加权抽到不同候选。它是诊断入口，**不改变判定**：
  `rng-initial-shuffle` 仍以 `diverged` 结束。

## 比较协议（compare）

### 边界

两个后端产出**同一条边界序列**，否则按"边界数不同"直接判不可计：

1. 边界 0 = `start`：受控初始状态建好、脚本执行之前；
2. 每个脚本步骤 = 一个边界 `step:<n>:<action>`；
3. 步骤内出现的**逻辑决策点**（选目标、选牌…）记在该边界的 `decisions` 里：候选取**回答之前**的集合，
   并记下最终选择——这样 ENV §6.1 要求的"候选数量/参数/约束本身也比较"才真的发生。

两个后端的调度形状完全不同（我们把目标当决策点、参考把目标当参数；参考的选牌走决策源），
所以比较的是"一次外部动作结算完"这个**共同逻辑边界**，不是各自的函数调用序列（ENV §6.2）。

### 比什么

| 内容 | 载体 | 说明 |
| --- | --- | --- |
| 合法动作 | `actions` | 动作 id + 候选（语义槽位）+ 可绑定组合数 |
| 逻辑决策点 | `decisions` | 候选集合 + 最少/最多可选 + 最终选择；`kind`/`prompt` 只入记录**不比较**（各后端自己的表述） |
| 规范化事实 | `facts` | 同一套路径：`player.*`、`enemy.<内容id>.*`、`pile.<牌区>.{size,sorted,order}`、`power.<拥有者>.<i>.*` |

各后端**私有的完整状态**（我们的引擎实例 id、类型名、集合名）只写进 base 侧记录的 `diagnostic`
字段供人定位首差，**不参与比较**。原生实例 id 是后端分配的不透明句柄，两侧一律先映射成语义槽位
（`player` / `enemy:<i>` / `card:<pile>:<i>` / `power:<owner>:<i>`）。

### 判定（只有 `passed` 计通过）

| 判定 | 含义 |
| --- | --- |
| `passed` | 两侧在**可计入**的边界上逐条一致 |
| `diverged` | 可计入的 case 出现首差（计入退出码） |
| `unsupported_rng_profile` | 该 case 的路径依赖各后端私有随机序列（ENV-01 未完成）：**不计通过**；差异作为**证据**附上，并标明"不是规则结论" |
| `unsupported_decision_model` | 两个后端的决策模型暂时映射不上（见下）：只跑 base 侧，不计通过 |
| `invalid_fixture` / `fault` / `reference_fault` / `missing_reference_record` | 夹具或后端失败，不计通过 |

"受控无随机"是**实测**的：base 侧在 `start` 边界与结束之间比较随机调用总数，一旦变化就降级
`unsupported_rng_profile`，不靠夹具声明自我认证。此外套件自检**拒绝**"脚本含 `end_turn` 却声明为
`controlled-no-random`"的 case：回合切换会把控制权交给**对端自己的**随机序列（选招、洗牌/抽牌顺序），
而我们这一侧"没消费随机"不足以证明可比（切片从固定牌序抽牌时不消费随机，参考侧抽的是它自己洗过的牌序）。
这条曾经靠"记得声明成 unsupported"来把守，现在是被检查的不变量。

### 已知的范围限制（都是显式记录，不是静默省略）

- **抽牌堆/弃牌堆的顺序**由洗牌随机决定，在 ENV-01 提供原版兼容随机之前两侧必然不同：
  因此那时只比**构成**（`pile.<x>.sorted`），比顺序只会把 RNG 差异误报成规则差异。手牌与消耗区始终比顺序。
- **随机流**只在两侧都声明 `vanilla-compatible@1` 时才比较。
- **事件/hook 顺序**尚未参与比较：引擎当前没有事件/hook 顺序的可读轨迹（ENV §6.2 缺口）。
- **投影必须取语义集合，不是"所有该类型实例"**（这条是踩过坑后写下的）：敌人必须取 `base-spire:Monsters`
  参战集合，否则已离场的尸体实例会被算成敌人，把"两侧一致"误报成"一边保留一边移除"。
- **终局边界也要投影**（2026-10-08 ENV-T04 修正）：战斗结束/正在结束时参考侧仍会按手牌列出 `EndTurn`，
  而 base 侧会话已 `Terminal`、不再报任何合法动作。两边说的是同一个逻辑边界（终局后没有外部动作），
  所以参考适配器改用参考**自己的** `CombatState.IsOverOrEnding()`（与 `CheckWinCondition` 同判据、只读）
  返回空动作集，而不是复制 base 结果。修正前 `last-kill-before-terminal` 的首差是
  `boundary[2].actions[base-spire:EndTurn]: 缺失 ≠ 存在`，会把终局后真正要看的牌区/资源事实挡住。
- **`PlayCard` 候选已对齐（曾经不对齐）**：`actions[].candidates` 现在取**可提交绑定里出现过的候选参数取值**，
  而不是候选参数的整个定义域——定义域会包含当前**不可打出**的牌（能量不足、不可打出关键词…），
  参考侧用的是 `CardModel.CanPlay` 过滤后的集合，用定义域会把"两边对合法性判断一致"误报成差异。
  判别性夹具：`candidate-scope-partly-playable`（1 点能量 + 手里 1 费 Strike 与 2 费 Bash）——
  两侧都只报 `['card:hand:0']`、`bindings=1`，2 费那张不进候选。
- **死亡敌人的表示**：参考在死亡后把敌人移出它的敌人列表，我们的引擎保留在集合里。事实路径已改成
  按**内容 id**（`enemy.base-spire:calcified-cultist.*`）而不是存活下标，避免下标错位把"同一只敌人"的
  差异摊成两条无关差异；但"谁对"必须按 `sts2/` 裁决，**本轮没有替任何一侧归一化**。

## 比较器自检（受控错误记录）

`compare` 每次都构造受控错误记录并断言比较器抓得到，否则返回 1。当前 6 项全过：

| 自检 | 期望差种类 | 实测抓到的首差 |
| --- | --- | --- |
| 相同记录 | 不报差异 | 不报差异（但 Profile 不可计入，判定不是 `passed`） |
| 字段值 | `field` | `boundary[3].player.hp: 54 ≠ 53` |
| 缺动作候选 | `action` | `boundary[3].actions[base-spire:PlayCard]: [4 张] ≠ [5 张]` |
| 缺牌区成员 | `collection` | `boundary[3].pile.hand.order: 4 张 ≠ 5 张` |
| 缺决策候选 | `decision` | `boundary[2].decisions[0].candidates: [enemy:0] ≠ [enemy:0,enemy:1]` |
| 随机流调用数 | `stream` | `boundary[3].streams[base-spire:combat-draw].draws: 10 ≠ 9` |

## inventory 做什么

1. **基线摘要**：原版恢复源码（`sts2/src/Core/Models`、`sts2/src/Core/Random`）、参考模拟器
   （`reference/sts2-sim/src`、`tests`）与生产源码（Engine / base 内容源 / base Slice / net 契约）
   逐文件 sha256 再汇总成目录摘要。**未提交目录用自己的摘要**，不用父仓库 Git HEAD 冒充。
2. **原版分母**：按原版自己的身份规则（`ModelDb.GetId` → `SlugifyCategory` + `Slugify`）逐条算出
   `类别.条目`（如 `CARD.ABRASIVE`）并带 `源文件:行`；排除项同样逐条登记理由。
3. **本项目侧状态**：旧后端注册结果与正式 `.spire` 前端四态（声明/解析/降级/未支持）。
4. **连接**：只对连接键已核对的类别（`CARD`/`POWER`）给同名匹配数。
5. **机制直方图**：把真实未支持原因按机制族归类，给出**每个族卡住了哪些内容**（`blocked_content` =
   去重内容数、`reason_fragments` = 原因片段数、`content` = 最多 20 条内容名），直接用来组下一批。
   口径曾错过一次：只报片段数并当成"卡住多少内容"，会把一张卡的 3 条原因算成 3 张卡。

### 下一批该补什么（测量结论，2026-10-04）

`engine_loaded.power_demand` 与机制直方图一起给出的结论是：**剩下的是长尾，没有单个大赢家**。

| 机制族 | 内容数 | 片段数 | 说明 |
| --- | ---: | ---: | --- |
| 运行时未实现该能力的行为 | 19 | 19 | 19 个能力各挡住约 **1** 张引用它的卡 |
| 原语 | 18 | 40 | 18 张卡被原语挡住（其中若干张同时缺多个原语） |
| 能力引用 | 17 | 17 | 与上一行同源：能力没进白名单 ⇒ 引用它的卡也进不来 |
| 能力字段 | 12 | 20 | 能力的字段映射（含药水能力） |
| 关键词 | 2 | 2 | `innate` |

因此批次**按机制族组**（例如把 `if`/`variables`/`withCount` 这类表达式原语放一批、把能力字段族放一批），
而不是"谁的数字大先做谁"——按卡名逐个修原语已经被这条数据否掉了。

`single_family_content`（只命中一个族的内容）是"看起来最便宜"的候选清单，但它**会少报工作量**，
实测两个反例：

| 内容 | 只命中的族 | 实际还需要 |
| --- | --- | --- |
| `Mangle` | 原语（`if`） | `targetAlive` 谓词 + `ManglePower` 能力 |
| `Colossus` | 能力引用（`ColossusPower`） | 一个新机制：**攻击者身上有易伤**时把受击伤害 ×0.5（现有多修饰符模式不支持"按攻击者状态"的条件） |

所以正确的读法是"候选清单"，真正能一次落地的批次要按**机制依赖**（谓词、引用的能力、字段）再拆一层；
按卡的依赖数排序，而不是按族的大小或"只缺一个族"排序。

**挑下一个目标的正确入口是 `unsupported_detail`**（`base_spire.formal_frontend.unsupported_detail`）：
它逐条给出**具体缺哪个字段/原语**，因此能直接找"只缺一个字段"的能力——`CrueltyPower`
（只缺 `vulnerableMultiplierPercentPerStack`）就是这样选出来的。

### 机制依赖图（`next_batch_mechanisms`）

从原因文本**推导**的机制依赖图（71 个机制单元；名字带 `_mechanisms` 后缀，因为既有的 `next_batch`
是**人手维护的叙述清单**，两者互补、不能互相覆盖）。每个单元给出种类（`field`/`primitive`/`keyword`/`hook`/`power`）、
挡住的内容数与内容名。当前前几名：

| 机制 | 种类 | 挡住内容 | 是什么 |
| --- | --- | ---: | --- |
| `on` | field | 6 | `on = [trigger(event=…, effect=…)]` 这个**多触发点**声明形状 |
| `isTemporary` / `onExpire` / `onStackChange` | field | 3 | 临时能力 + 层数变化/到期时的效果（Mangle/SetupStrike/FlexPotion） |
| `if` | primitive | 3 | 条件效果（Mangle/MoltenFist/Stomp） |
| `variables` / `innate` / `autoPlay` / `forEachCard` / `modifyPowerVariable` / `withCount` | field/keyword/primitive | 2 | 各自一族 |

**已勘查、确认不是"小改动"的下一个候选**（避免重复推导）：

| 候选 | 还需要什么 | 成本评估 |
| --- | --- | --- |
| `Whirlwind`（`repeatForX`） | 批次计数要能取**这张牌自己的 `Amount`**（X 费在出牌时把剩余能量写进 Amount） | 不是小改动：`AmountSource` 词表里没有"自身 Amount"，且 `repeat` 的计数在作者模型里是**字面整数**，动态计数要动模型 |
| `Anger`（`cloneCard`） | "生成一张自己的副本进弃牌堆" | 不是小改动：切片运行时**没有任何克隆/复制卡实例**的路径 |
| `Unmovable`（`blockGainModifiers`） | 获得格挡时的乘区 + `priorEntriesPerStack` 的每回合计数 | 中等：修饰符框架可复用（形如 `incomingDamageModifiers`），但要加一个**每回合计数**状态 |
| `Plating`（`on` 两触发点） | `on = [trigger(afterOwnerTurnStart, decrementPower), trigger(afterOwnerTurnEnd, gainBlock(perPowerStack))]` | **已更正为"小改动"**（曾误判为"要改模型"）：两个触发点分别落在**已有**的 `Trigger`（回合开始）与 `OwnerTurnEnd`（回合末）两个槽位；回合末那半只需仿照已有的 `convert-to-strength` 加一个 `gain-block-per-stack:N` 种类，加上一个仿照 `remove-self`/`Tick` 的 `decrement-power` 效果，再加 `on`→两槽位的映射。**不改模型结构** |

产物 `columns` 里显式写出尚未产生的列：`engine_loaded_into_a_ruleset` / `engine_behavior_verified` /
`differential` = `not_run`，`host_public` = `not_checked`。

## 纪律

- **不为了分母好看而删难项**：排除必须给出对象、来源位置与理由；拿不准的留在分母里标 `pending_source_check`。
- **副产物不是覆盖证明**：`matched_formal_lowered` 只说明"本项目声明了同名内容"。
- **未支持不计通过**：`unsupported_*` / `missing_*` / `reference_fault` 都不是 `passed`。
- **受控状态只用于机制对拍**，不能当正常开局胜负证据（ENV §5/§6.1）。
- 源码文件只用编辑工具改，不用 shell 文本往返（会破坏 UTF-8）。

## 一次实测（2026-10-04，ENV-00 第三批：两侧对拍）

`inventory` rc=0；`compare --suite fixtures-v1` **rc=0**。产物：
`artifacts/environment/fixtures/comparison.json` + `records/base/*.json`（10）+ `records/reference/*.json`（9）。

| 判定 | 数量 | case |
| --- | ---: | --- |
| `passed` | **7** | `strike-normal`、`strike-upgraded`、`defend-normal`、`defend-upgraded`、`bash-then-strike-vulnerable`、`bash-upgraded`、`candidate-scope-partly-playable` |
| `unsupported_rng_profile` | 4 | `enemy-death-aftermath`、`power-turn-end-decay`、`vulnerable-decay-via-bash`、`random-reshuffle-unsupported` |
| `unsupported_decision_model` | 1 | `headbutt-mid-choice` |

**6 个 `passed` 的含义**：同一条受控无随机决策序列下，我们的 Engine 与参考实现在伤害（Strike 6/9、
Bash 8/10）、格挡（Defend 5/8）、能量、手牌/弃牌区、能力层数、以及**目标决策的候选集合**
（两个敌人都可选、最终选择 `enemy:0`）上逐条一致。这不代表"原版同种子一致"，也不覆盖含随机或选择的路径。

**本轮查实的三个真实缺口（后续已逐条按 `sts2/` 裁决，见下节）**：

1. **死亡敌人的表示**：`enemy-death-aftermath` 的首差在致命一击结算后的边界——参考把死掉的
   Calcified Cultist **移出了**敌人列表（`enemy.base-spire:calcified-cultist.*` 全部"缺失"），我们的引擎
   把它留在集合里（`hp=0`、`alive=false`）。最后一致边界 = `0:start`。
2. **易伤回合末衰减**：`power-turn-end-decay` 的证据里有一条**与随机无关**的差异
   `power.enemy.base-spire:calcified-cultist.0.stacks`：我方 **2**、参考 **1**。
3. **选牌决策源不可注入**：参考的选牌决策源挂在 **run 层**
   （`RunState.CardSelectionSource`，默认 `RunEngineCardSelectionDecisionSource`），其设置入口
   `ConfigureCardSelectionSource` 是 `internal`；combat 层的 `CardSelectionSource` 不参与这条路径。
   实测：参考侧只记录了 1 个决策点（目标）而不是 2 个（目标+选牌），且没有抛错——说明它静默用了
   run 层默认源。因此 `headbutt-mid-choice` 只跑 base 侧。

## 按 `sts2/` 裁决首差（ENV §6.3）

| 首差 | 裁决 | 依据 |
| --- | --- | --- |
| 死亡敌人仍在敌人列表 | ~~本项目缺陷~~ → **工具投影缺陷，已修；规则两侧本来就一致** | **自我更正**：切片**已经**在怪物死亡时 `RemoveFromCollection(monsters, …)`（`CombatSlicePackage.cs:660-668`，与 `sts2/src/Core/Commands/CreatureCmd.cs:508,523-531` 的"死亡后离场"同源，且同样发生在 `MonsterDied`/`AfterDeath` **之后**）。真正的问题在**工具**：`EngineSnapshot` 按 `AllInstances(Monster 类型)` 投影敌人，把已离场的**尸体实例**也算成敌人；参考侧读的是 `combat.Enemies`（活着的集合）。已改为按 **`base-spire:Monsters` 集合**投影（`LivingEnemies`）。修正后 `enemy-death-aftermath` 在致命一击那个边界上两侧都只剩 `['base-spire:damp-cultist']`，**该路径不再有差异**（只剩 `end_turn` 之后洗牌决定的牌区构成） |
| 易伤回合末 2 vs 1 | **夹具假象，不是规则差异** | `Models/Powers/VulnerablePower.cs:59-65`：只在 `CombatSide.Enemy` 回合末 tick；`Commands/PowerCmd.cs:190` 的 `TickDownDuration` 用**一次性消费**的 `SkipNextDurationTick` 决定是否跳过。原夹具用 `setup.powers` **直接声明**（切片侧 `Ticks=false` ⇒ 明确"不是走时长的能力"），绕过了 `PowerCmd.Apply` 的施加路径，所以两侧本来就不可比。新增 `vulnerable-decay-via-bash` 走真实施加路径后，`power.*.stacks` **不再出现在差异里** |
| 选牌决策源不可注入 | **协议限制（尚未解除）** | 参考侧入口是 `internal`，不改参考就无法注入受控选牌。ENV §6.1 要求"无法唯一映射就显式失败"，因此该 case 只跑 base 侧并判 `unsupported_decision_model`，不计通过 |

**仍然真实、但范围很窄的缺口**（不是上面那条）：切片的离场是**无条件**的，而原版会逐个询问
`Hook.ShouldCreatureBeRemovedFromCombatAfterDeath`，任一能力否决就**留下**（`Hook.cs:2214-2224`；
Adaptable/DieForYou/Illusion/PainfulStabs/Reattach/SteamEruption 六个覆写为 false）。
当前切片实现的能力里**没有**否决者，所以行为与已迁子集一致；等这些能力迁入时必须补"可否决离场"的
声明式机制，不能那时再靠特例。另有原版的"怪物正在行动时不移除"（`CreatureCmd.cs:527-530`）同样待迁。

同批次回归：`dotnet build AutoTheSpire.sln` 0 错误 0 警告；有条件引用与
`-p:ReferenceSimAvailable=false`（无参考目录）两条构建路径都是 0/0；依赖审计 9/9
（18 个项目、0 环、违规 0，`Differential -> ['BaseSpire','Core.TheSpire','Sts2Sim.Core']` 证明参考依赖
只落在工具里）；`check_docs.py` 179 份 0 问题；`git diff --check` 无空白错误；参考仓库**只读**，其既存脏改动未被触碰。

**ENV-01 首批（同日）**：`rng` rc=0；core 新增原版兼容随机 Profile
（`mega-random/xoshiro256ss-splitmix64@1` + `vanilla-uniform@1`），Engine 检查 **69/69**。

**ENV-01 第二批（同日，base 侧布局）**：新增 `BaseSpire/Slice/VanillaRandomLayout.cs`
（字符串 seed → run/player/内容流种子，照抄 `sts2/`）；`BaseSpire.Slice.Tests` 新增 4 项 → **248/248**；
`rng` 增加布局层交叉核对（8 个字符串 seed，包括空串与中文），全部命中**已登记参考偏离**、rc=0。
`Legacy` 190/190、net 393/393 未回归。

**可比性加固（同日，第三次自我更正之后）**：① 敌人投影改按**参战集合**（`base-spire:Monsters`），
修掉"尸体算敌人"的假差异，`enemy-death-aftermath` 在致命一击那个边界上两侧都只剩 `['base-spire:damp-cultist']`；
② `actions[].candidates` 改取**可提交绑定里出现过的取值**（不是候选参数的定义域），
判别性夹具 `candidate-scope-partly-playable` 两侧都只报 1 费那张牌；③ 套件自检新增不变量——
**含 `end_turn` 的 case 不得声明为 `controlled-no-random`**（回合切换走对端私有随机），
用临时错声明夹具实测会 rc=1 并打印原因。`compare` 因此从 6 升到 **7 `passed`**。

**结算（同日，`verify` 落地）**：`verify` rc=1、`overall=incomplete`，门 3 通过 / 8 开放；
分母 1567（连接键已核对 835：已降级 72 / 未落地 763；无法结算 732）；漂移核对 8 棵树无漂移。
这样 ENV-00 §5 的四条稳定命令（`inventory`/`compare`/`run`/`verify`）只剩 `run` 未实现，
而 `run` 依赖 ENV-03 的真实整局流程，现在显式报未实现而不是假装有。

未结项：`run`（ENV-03）、事件/hook 顺序轨迹、**离场否决机制的迁移**（窄口径：切片离场是无条件的，
原版会逐个询问 `ShouldCreatureBeRemovedFromCombatAfterDeath` 且怪物正在行动时不移除；
当前已迁能力里没有否决者，故行为与已迁子集一致）、
选牌决策源的注入路径、游戏版本机器可读冻结、**参考版本冻结**（参考已领先于 `sts2/` 基线）、
非 `CARD/POWER` 连接键核对（732 条因此无法结算）；ENV-01 的 base 侧流**接线**（把切片的 draw 流对到原版 `Shuffle` 流）
与 decimal 数值保真。

## 一次严格 regression 实测（2026-10-08，ENV-T04）

命令与返回码（均为仓库根目录实际执行，全部实名记录在
`.audit/dsh-env-t04-resume-2026-10-08/`）：

| 命令 | 返回码 | 结果 |
| --- | ---: | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File tools\differential\test-env.ps1 -Suite regression` | **1** | 先构建 `Differential.csproj`，再跑严格 suite：15 个必测 case 全部执行，双侧 `passed` **8**、单侧 `self_checked` 1、不计通过 **6**、比较器自检 6/6 |
| `dotnet run --project tools/differential -- test --suite regression --output artifacts/environment/tests/regression` | **1** | 同一入口；run_id `regression@env-t04-regression@1+20261008T080103Z` |
| `dotnet build tools/differential/Differential.csproj` | **0** | 0 错误 0 警告（本机没有 `pwsh`，脚本用 Windows PowerShell 5.1 跑） |
| `dotnet build AutoTheSpire.sln` | **1** | **跨线阻塞**：`net-the-spire/Net.TheSpire.Tests` 5 处 `CS0120` 把 `ControlHost.SupportedScenarioProfile` 当静态成员用，而 `net-the-spire/Net.TheSpire/Control/ControlHost.cs:78` 是实例属性（net 线 2026-10-07 22:15 的在制未提交改动，不是本包改动） |
| `dotnet run --project tools/differential -- rng` | **0** | 读数向量 5/5 一致；布局向量 8 条不一致全部是**已登记参考偏离**、未登记 0 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | **0** | 95/95 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **0** | 324/324 |
| 失败契约反例 11 项（见上表） | 2/2/2/1/1/1/1/1/1/1/3 | 全部命中期望返回码 |

判定分布：7 个原可执行 case 双侧 `passed`（`strike-normal`、`strike-upgraded`、`defend-normal`、
`defend-upgraded`、`bash-then-strike-vulnerable`、`bash-upgraded`、`candidate-scope-partly-playable`）；
新增 `last-kill-before-terminal` 双侧 `passed`；`factory-wiring-dev-scope` 单侧 `self_checked`；
5 个 unsupported 原 ID 保留原判定（`headbutt-mid-choice` = `unsupported_decision_model`，
`enemy-death-aftermath`/`power-turn-end-decay`/`vulnerable-decay-via-bash`/`random-reshuffle-unsupported`
= `unsupported_rng_profile`）；`rng-initial-shuffle` = `blocked_compare`（参考无运行层/字符串 seed 入口，
且参考检出领先冻结基线）。**严格 suite 因此诚实红态**：必需 unsupported/blocked 存在时不允许伪装全通过，
也不删分母。

`last-kill-before-terminal` 的判别点：受控把两只敌人压到 1/5 血，第一张 Strike 击杀前排、第二张 Bash 击杀
**最后一只**敌人（敌人语义槽位按存活集合编号，所以击杀前排后存活的那只重新编号为 `enemy:0`），
最后一个边界同时检查终局（base 侧 `Terminal` = Victory）、牌区与玩家剩余资源——用来抓 ENV-T02 的
"胜利判定挪到动作体末尾（`BattleEndTail`）之前中途 `SetTerminal` 截断父效果余效"那类缺陷。
它只覆盖战斗层终局边界；Run 级长期写回与跨房（Feed MaxHp 等）仍依赖 T05 的真实房间流程。

## 门槛覆盖 `self_checked`（2026-10-08 接续包实测）

上一节是入口交付当日的实测。同日晚些的接续包发现并修掉一个**假通过**：必需维度/预期终点门槛原先只在
`verdict == "passed"` 之后执行，`kind=wiring` 的工厂自检记为 `self_checked` 后直接绕过门槛——把工厂 case 的
`required_dimensions` 加上 `terminal`、或把 `expected_terminal` 写成 `defeat`，整批仍返回 **rc 0**。

修复：门槛改为覆盖**所有可计成功结果**（`passed` 与 `self_checked`）。判决顺序不变（缺维度优先于错终点），
更具体的失败保持原判定，分母不变，缺失维度只如实列出、不补占位读数。

判别证据（完整注册 suite：8 个正常双侧 case + 工厂 case，全部预登记、不用 `--only`、比较器自检 6/6；
修复前用被复核过的旧二进制 `Differential.dll` sha256 `c1ae4d3c…`，修复后 `18a2c69b…`）：

| suite | 修复前 | 修复后 |
| --- | --- | --- |
| `fixtures/negative/suite-wiring-missing-dimension.json`（工厂声明 `terminal` 但无终局，终点登记正确） | rc **0**，`self_checked`，`missing_dimensions=[terminal]` | rc **1**，工厂 `missing_dimension`，其余 8 个 `passed` |
| `fixtures/negative/suite-wiring-wrong-terminal.json`（工厂维度齐备，终点错登记 `defeat`） | rc **0**，`self_checked`，终点问题非空 | rc **1**，工厂 `wrong_terminal`，其余 8 个 `passed` |
| `suites/regression.json`（正式 15 项） | rc 1（8 `passed` / 1 `self_checked` / 6 不计通过） | rc **1**，分布不变；正常工厂 case 仍 `self_checked` |

两项负例的 `setup`/`script`/`provenance` 等字段由 `suites/regression.json` 原样派生（源 sha256
`2dccebe9…`），唯一变量是工厂 case 的 `required_dimensions`/`expected_terminal`；派生与核对脚本在
`.audit/dsh-env-t04-gates-2026-10-08/{build,verify}-negative-suites.py`，实测产物在
`artifacts/environment/tests/gates-2026-10-08/`。

## 地图独立对拍（2026-10-09，ENV-T04 地图续包实测）

本节是 `kind=map` 落盘后的实测。命令与返回码（仓库根目录实际执行；本机没有 `pwsh`，脚本用
Windows PowerShell 5.1 跑；全部实名记录在 `.audit/dsh-env-t04-map-resume-2026-10-09/`）：

| 命令 | 返回码 | 结果 |
| --- | ---: | --- |
| `dotnet build tools/differential/Differential.csproj` | **0** | 0 错误 0 警告（Core + Base + 参考 + 工具闭包） |
| `dotnet run --project tools/differential -- test --suite regression --output artifacts/environment/tests/regression` | **1** | 17 个必测 case 全部执行（17/17，`complete=true`），双侧 `passed` **10**、`self_checked` 1、不计通过 **6**、自检 6/6；run_id `regression@env-t04-regression@1+20261008T170824Z`，suite sha256 `ac502689…` |
| `dotnet run --project tools/differential -- test --suite tools/differential/fixtures/negative/suite-map-missing-dimension.json` | **1** | 地图 case `missing_dimension`，其余 8 个 `passed`、自检 6/6 |
| `dotnet run --project tools/differential -- test --suite tools/differential/fixtures/negative/suite-map-wrong-terminal.json` | **1** | 地图 case `wrong_terminal`，其余 8 个 `passed`、自检 6/6 |

判定分布：`passed` 10（既有 8 条 + 新增 `map-generated-alpha`、`map-generated-beta`）、`self_checked` 1
（`factory-wiring-dev-scope`）、不计通过 6（`headbutt-mid-choice` = `unsupported_decision_model`；
`enemy-death-aftermath`/`power-turn-end-decay`/`vulnerable-decay-via-bash`/`random-reshuffle-unsupported`
= `unsupported_rng_profile`；`rng-initial-shuffle` = `diverged`，首差仍是
`boundary[0].enemy.base-spire:fuzzy-wurm-crawler.alive: 缺失 ≠ 存在`）。**六项既有红态逐字未变**，
分母 15→17 只增不减。

两个地图 case 的实际比较量（`records/{base,reference}/map-generated-*.json`，两侧逐条一致）：

| case | seed | `map.count` | `map.rows`/`map.cols` | 抽取事实 | `act_1_map` 字数 | 状态指纹 |
| --- | --- | ---: | --- | ---: | ---: | --- |
| `map-generated-alpha` | `t05-map-alpha` | 51 | 17 / 7 | 123 条（51 节点类型 + 65 条边 + 7 条头部） | 292 | `0x1d032dd0fc7e5192` |
| `map-generated-beta` | `t05-map-beta` | 61 | 17 / 7 | 140 条（61 节点类型 + 72 条边 + 7 条头部） | 265 | `0xdaeca1274bcfdb41` |

两侧 `map.start`/`map.boss`/`map.second_boss`/每个节点类型/每条边完全一致，`divergences` 为空；两侧记录
绑定同一次构建（`build_id=6b25fa16e5b567f4`）、同一 Profile（`source-compat-sts2-frozen@1`，代码摘要
`952e57a0…`）、同一参考 HEAD（`2cdaea3b…`）与同一批源输入（`reference_identity.source_files` 7 条，摘要
逐条相符）。

两条元数据勘误/修正（草稿审阅时发现，不无脑复制）：

1. `ReferenceMapEntry.SourceFiles()` 原先拿 `Repo.DigestFiles` 的 `sha256:` 前缀去比登记文件的裸十六进制，
   于是一条**完全相符**的摘要也被写成"（登记=…，不一致）"，在源依据上造出假警报。现按十六进制比对；
   修正后 7 条源输入全部相符（例如 `sts2/src/Core/Map/StandardActMap.cs` = `6f927e07…`）。
2. `ReferenceMapEntry` 不建 `RunState`：参考的运行层入口会消费本 case 不比较的流，用它比较地图会把
   "未声明随机消耗"混进证据。地图入口只构造 `Rng(act_1_map)` + `Overgrowth.GetMapPointTypes` +
   `StandardActMap(15, …, hasSecondBoss:false, enablePruning:true)`。

未覆盖范围（不要据此宣称更多）：这两个 case 只覆盖幕 1 地图**生成**机制，两侧各读自己的公开输出，
base 侧只停在 Start 之后的地图相位（`session_status=Running`、无终局）。**不**覆盖 `ChooseMapPoint`
选择语义（参考侧没有对应动作模型，故 base 侧动作表为空）、多房间推进、房间奖励与结算；base 侧选中
尚未实现的房型仍具名 Fail，本包不声称跑通。地图 case 的负数判别只用上述两项反例，未覆盖
"`base_scope=dev`（无地图投影）"这类误配。


### 连续首幕运行

`ironclad-act1`复用正常工厂、共同动作和独立参考，`normal_act1`持续运行至实际死亡、首幕完成或具名失败，2000步上限只作为超时保护；不按战斗场次成功停止。`choose=defensive`是该脚本的公开防守策略选项，低生命时优先Defend，其余规则只读取公开定义、生命、牌区和地图类型。

suite保留原seed的真实死亡控制例和必须到达首幕完成的连续例；后者遇到暂缓的PhrogParasite时仍返回非零。base失败但已有成功动作时，参考继续独立执行该成功前缀，比较所有稳定边界，失败动作不伪造执行；前缀一致仍不能把整局判为通过。normal-run记录的reward-closure维度检查历史上真实发生的奖励→合法旅行，不要求死亡终点仍可旅行；旧前缀suite保持末边界关闭要求。

营火使用原生RestSiteRoom与实际Heal/Smith命令。参考TreasureRoom.Enter会自动取得遗物，无法表示真实跳过，因此连续适配按恢复源码的进入/开箱边界组合原生RNG、共享遗物袋与金币/获得命令；该适配在记录中明确注明，不宣称调用了原生自动领取入口，也未借用base计算结果。商店和额外房间能力未适配时明确失败。

### 默认三幕与受控切幕核对

`act-transition --output <目录>`从正式`dev-run`工厂正常Start，独立创建原生`RunState(Overgrowth,Hive,Glory)`。使用三个已登记seed逐幕核对事件/普通/精英队列、Boss和古代人，以及地图节点/连边/随机消费/指纹；预生成身份全量比较，未执行内容不因缺实现被过滤。

核对切幕边界时显式预置双方的已访问坐标，并在base设定幕结束、资源/槽位及旧战斗残留，随后提交真实ContinueToNextAct。此部分标记`controlledTransition=true, normalVictoryEvidence=false`，不能算正常Boss胜利或四幕对拍。检查奖励未关闭拒绝、库存实例与空槽保留、全局幂等键、地图/旧战斗清理、不得重复UpFront，以及第三幕后TheArchitect仍明确失败。参考私有预生成库存通过只读诊断读取，不修改参考源码、随机数或玩法。缺参考/异常/任一差异均非零。

### 第一幕多页与非战斗卡牌奖励

`event-content --output <目录>`经下述独立事件入口执行TabletOfTruth五页、回复／放弃／致死、唯一及空升级域，以及RoomFullOfCheese八选二的候选定义、顺序、保留实例和清理；参考独立创建原生默认计划和真实事件。逐边界比较HP/MaxHp、永久牌组、实际事件与Rewards流的抽数／指纹；未消费流只核对零抽数。WoodCarvings有／无可移除基础牌资格的两例仍从正式`dev-run`真实问号入口验证，效果未支持仍具名失败。输出明确标记`controlledInitialState=true, normalVictoryEvidence=false`，不是正常通关证据。

`test --suite normal-tablet --output <目录>`独立从默认三幕正常开局，原固定seed实际经过TabletOfTruth五页和后续战斗至真实死亡；要求恰好发生五次解读。支持`base_scope=dev-run`，初始化与单幕`dev-map`分列，原首幕完成失败例保留。此套件只证明正常事件、长期状态和死亡闭环；完整首幕胜利仍按`ironclad-act1`及整局合同验收。RoomFullOfCheese的Engine二选行为与公开协议多选缺口分开报告，不用单选替代二选。

### 独立事件模块验证

`event-modules [--event <源类名>] --output <目录>`使用工具侧`IsolatedEventSession`，不运行地图生成、正常遭遇或敌人。它加载正式ContentSource和版本化元数据，共用生产CardDefinitions、RunDeck、RunResources、EventModules、EventRoomCatalog与Compute后端；工具只提供已声明的受控Run资源／牌组初态和访问动作。通过真实`catalogue.Enter`初始化事件，再执行真实选项，不手工伪造某一事件的页字段。没有第二套玩法或事件ID分派表。

省略`--event`时，对全部已注册模块执行进入检查，再检查JungleMazeAdventure与MorphicGrove的完整行为；指定已注册源类名时只进入对应模块，已有定向行为检查一并运行。MorphicGrove覆盖100金币资格边界、全部金币先扣、一次两张选择／非法重复答案不变、0/1/2候选自动处理、替换实例／顺序／随机指纹及Loner资源。Jungle覆盖两笔float32金币初始化、两分支与独行致死后的金币余效。

原生JungleMazeAdventure明确记录偏离#84：SOLO_QUEST省略两次视觉列表洗牌抽数。工具保留严格参考的2抽与生产4抽差异，单列`known_reference_divergences`并返回1；资源与从源方法组合的独立向量另行核对，不修改参考、补抽或将该分支计为严格通过。`--event MorphicGrove`可独立返回0，不受此差异、敌人缺失或公开多选协议影响。所有结果标明`controlledEventVisits=true, normalVictoryEvidence=false`；正常Run集成仍用严格suite与公开Host。

### Sown与SapphireSeed验证

`event-modules --event SapphireSeed --output <目录>`经真实目录Enter执行三组固定seed的Eat／首张Plant比较，并验证完整永久候选与零随机消费；非首张Plant保留参考自动首选的明确差异，整体返回1，不冒充完整双侧通过。非首张选择、空域／单候选、跨战斗与复制隔离的源行为判别在Slice的`--sown-only`入口。

`sown-combat --output <目录>`独立运行生产规则和原生CombatEngine，对照普通／升级Defend附着Sown后连续两次出牌的能量、格挡和附魔状态，共4个边界；使用受控战斗初态，不比较建局洗牌或宣称正常通关。另只读记录原生CreateClone保留Disabled、通用MutableClone重置的路径差异，参考不修改。异常或可比行为不一致返回非零。公开受控访问由net的正式工厂／ControlHost检查Plant候选、字段、请求重放、过期拒绝及后续战斗副本；正常开局轨迹仍须另列证据。
