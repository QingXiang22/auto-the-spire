# ENV 整改线进度报告（2026-10-04）

**任务**：完成 `docs/review/tofix/README.md` 提到的所有任务——即 ENV 任务包
（`docs/design/environment-completion-and-differential-v1.md`）的 ENV-00 → ENV-06。
本报告是**阶段性进度汇报**，交付物与证据的入口仍在 `tofix/README.md`（唯一任务定义）
与本目录的 `env-handoff.md`（续作交接）。

## 一、一句话结论

**ENV-00（基线/清单/对拍骨架）与 ENV-01 的随机保真主干已完成并逐条有可复现证据；
ENV-02 在 12 个通用机制上落地了对应内容；ENV-03 的整局循环机制已验证完整，
剩余作业面集中在内容/元层（地图、遭遇数据、奖励、事件）。**
`verify` 当前 rc=1（门 8 通过 / 6 开放），这是"结算完整、环境未完成"的**设计语义**，不是工具失败。

## 二、当前实测状态

| 指标 | 值 | 证据 |
| --- | --- | --- |
| 环境分母 | **1567** 条（16 类，每项带源文件:行） | `verify` 的 `denominator_has_sources` **通过** |
| 已降级 / 仅旧后端 / 未落地 | **88** / 约 257 / **1222** | `verify` 汇总行 |
| 无法结算 | **0** | `coverage_settleable` **通过** |
| 门 | **8 通过 / 6 开放**（共 14） | `verify` rc=1 |
| 正式源进入 Engine 构建 | 卡 **68** / 能力 **25**，全部实例化、缺口 0 | `engine_loaded_mechanism` **通过** |
| 行为已验证（自报） | **53** 条 | `artifacts/environment/behavior/slice-behavior-report.json` |
| 对拍 | 9 条内容，7 passed，**0 可计入首差** | `compare` rc=0 |
| 整局循环 | 战斗 → 结算写回 → `EnterNextRoom` → 新遭遇 → 再结算，**机制全通** | `run` rc=1（`flow_verified`） |
| 测试 | Engine **69** / Legacy **190** / Slice **268** / net **393** | 全绿 |
| 其他 | build 0/0、依赖审计 9/9、`check_docs.py` 180 份 0 问题 | 全绿 |

## 三、本次会话落地清单

### ENV-00（基线与对拍骨架）
- `tools/differential`：`inventory` / `compare` / `rng` / `verify` / **`run`** 五个命令；
  基准摘要、分母与来源、四态报告、机制依赖图（`next_batch_mechanisms`）、逐条未支持原因
  （`unsupported_detail`）、单族候选（`single_family_content`）。
- 比较协议：共同逻辑边界＝"一次外部动作结算"，语义槽代替实例 id；`unsupported_*` 一律不计入通过。
- 内容 id 命名空间治理：`registries/id-exceptions.json` + 双向核对（`stale` / `unexplained`）。
- 自检：比较器 6 项受控错误记录；夹具自校验禁止 `end_turn` + `controlled-no-random`。

### ENV-01（随机与数值保真）
- `MegaRandom` 原版算法/有界映射/流布局在 core 与切片两侧实现，并用**独立 Python 神谕**交叉验证
  （`tools/rng-oracle/vanilla_rng_oracle.py`，读向量 5/5 一致、布局 8/8 一致）。
- **观察不消耗随机数**不变量（`SliceFrontendObservationPurityChecks`，带消耗反例）。
- 剩余：base 侧随机流**接线**（切片抽牌流 → 原版 `Shuffle` 的真实调用顺序）、decimal 数值保真。

### ENV-02（通用机制 + 内容，12 项，全部带原版依据与判别性检查）
`NoDraw`（BattleTrance）、受击修正的**来源条件**（Colossus）、每层最大能量（Pyre）、
易伤倍率按攻击者层数（Cruelty）、`on=[trigger…]` 两触发点 + `decrementPower`（Plating/StoneArmor）、
**临时力量**（Mangle/SetupStrike/FlexPotion）、**尾部条件段 + `targetAlive`**（Mangle）、
**嵌套条件段 + 目标方向缩放施加**（MoltenFist）、**格挡乘区**（Unmovable）、
**卡牌实例变量**（Rampage）、**`power` 事件 + `draw-per-stack`**（Vicious）。
检查文件：`BaseSpire.Slice.Tests/SliceFrontend*Checks.cs`，每条都是"带对照"的判别性检查。

### ENV-03（整局）
- `run` 命令从 rc=3 空壳变为**可跑的最小整局流程**：一场真实战斗 + 结算写回 + `EnterNextRoom` 推进，
  `status=flow_verified`，并把整局侧缺口从 `verify` 的 coverage 数字带出（14 个类别，不手写）。
- 三条前置/循环检查：整局实例存在且初值合法；敌人全灭触发结算并写回 `Run.Hp`/`SettledRoom`/`BattleSettled`；
  `EnterNextRoom` 是**公开合法动作**（房间 +1、结算标记复位、幂等键留在上一间房、**新房间有 2 个敌人**）。
- **跨房间幂等已验证**：第二间房再清场 ⇒ `SettledRoom` 前进到 2、`Run.Hp` 被第二次写回（30 → 25）。
- 特征化断言（本轮新增）：**当前每间房使用同一套遭遇**（`calcified-cultist` + `damp-cultist`）——
  接入地图/遭遇选择后该断言应先失败，提醒改动者更新。

## 四、本轮的自我纠正（如实记录）

写特征化断言时，我先把"第一间房的敌人集合"取在了**清场之后**（此时已从存活集合移除），
于是断言读到空集并使 Slice 套件**失败 1 条**。发现后改为在清场**之前**取样，复测 **268/268** 通过。
记录在此，而不是把那次失败抹掉——它正是"先证明测量有效"这条纪律的实例。

## 五、未完成项与精确下一步

1. **ENV-03 内容/元层（建议主线）**：地图结构 `ACT 0/5`、遭遇作为数据 `ENCOUNTER 0 降级`（63 仅旧后端）、
   奖励 `RELIC 1/297` + `POTION 0/58`、事件 `EVENT 0/68`，以及"哪间房放哪个遭遇"的选择
   （当前固定：见上一节特征化断言）。**下一批**：先把 act 的地图结构与遭遇数据接进正式路径
   （"3 个房间 + 每房一个真实遭遇"可声明），再做奖励结算。
2. **ENV-01 余项**：随机流接线、decimal 数值保真。
3. **卡牌长尾（并行背景）**：`Thrash`（还需 `attackVariable` + `accumulateCardDamage` + `damageVariable`）、
   `Stomp`（4 个机制）、能力侧变量 `variables`+`powerVariable`（CrimsonMantle/Inferno，4 件事）、
   `HowlFromBeyond`（新时序点 + 自动打出）。**实测：单机制目标已经没有了**，后续都是多机制批。
4. **待裁定**：`feel-no-pain` 改名（公共契约）、`AuthorRulesetV2` 是否按公开字段集升版、
   `after_power_applied` 的"施加对象不是玩家"界限只有构造证据。

## 六、过程教训（详见 `env-handoff.md` 第 5 节）

用 shell 改文本文件曾把 `SliceChecks.cs` 写空（靠 harness 快照逐字节恢复；重建调度表会静默缩水：
48 个类只还原 232/257 条）；位置参数记录里插字段会**静默对调**两个 `int`；`ProcedureBuilder.If(...)`
单独调用会被**静默丢弃**；字符串里嵌 ASCII 引号会破坏编译；同回合复用同一卡实例触发成员冲突；
`.spire` 变量名必须按 kebab 归一化，否则两条来源路径落到不同字段名；新增字段必须显式声明 net 公开映射；
`verify` 前必须重跑 `inventory`（摘要冻结在产物里）。

## 七、复现命令

见 `env-handoff.md` 第 1 节（含"先 `inventory` 再 `verify`"的顺序要求）。
