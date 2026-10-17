# B-06a：基础战斗内容批（第三小批：基础牌第一组 + 胜利场景，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-06a 基础战斗内容批**（第三小批）——基础牌第一组（来源逐条对照）+ **胜利终局**生命周期场景 |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（B-06a 行："基础/升级牌…典型多敌…死亡/胜负；逐机制更新net profile/coverage"） |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（字面量参数步骤 `lose_hp:<n>`）、`Composition/CombatSlicePackage.cs`（该步骤的降级）、`Authoring/Examples/slice-content.author`（3 张卡）、`net` 公开目录（3 个新内容 ID）、切片检查（新增 4 项 + 4 处检查"限定到冻结卡片集"的重构） |
| 状态 | **本小批完成**；B-06a 其余子项仍未完成（见 §5） |
| 前置 | [第一小批](b-06a-status-cards-2026-10-02.md) / [第二小批](b-06a-losehp-beckon-2026-10-02.md) |

## 1. 选卡方式：先看来源能不能表达，不能表达就补原语

`ironclad-cards.spire` 里能**用既有步骤能力**精确表达的基础牌很少，本批取三张（数值逐条镜像来源）：

| 卡 | 来源行 | 普通 / 升级 | 步骤 | 用到/新增的能力 |
| --- | --- | --- | --- | --- |
| `IronWave` | :2103 | 5/5 → 7/7 | `[target, block, damage]` | 既有：格挡与伤害**共用**同一数值字段（来源恰好相等） |
| `TwinStrike` | :2438 | 5 → 7（每击） | `[target, damage x2]` | 既有：**批次数**表达 `repeat count = 2` |
| `Breakthrough` | :1549 | 9 → 13 | `[lose_hp:1, damage_all]` | **新增**：字面量参数步骤 `lose_hp:<n>`（同卡上"伤害取卡牌数值、自损取固定值"） |

**为什么只有三张**：其余基础牌需要切片还没有的能力（`Cleave` 等并不在来源里；
`Thunderclap` 需要"对全体施加能力"、`Shrug It Off`/`Pommel Strike` 需要抽牌、
`Body Slam` 需要"以格挡为伤害"、`Clash` 需要条件、`Perfected Strike` 需要按名字计数）。
这些是**具体反例**，逐条留待后续批次补原语后再声明，而不是先声明再想办法。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 来源对照 | `三张卡的来源数值与步骤逐条对照（含升级形态）`：来源行 2103/2438/1549、步骤次序（含 `lose_hp:1` 在 `damage_all` 之前）、普通/升级数值；非法字面量 `lose_hp:x` 以 `invalid_value` 拒绝并指向 `steps` |
| 混合效果 | IronWave：**伤害 5 且格挡 +5**（同一个数值字段两处使用） |
| **批次数与力量** | TwinStrike：普通两次 5 = **10**、升级两次 7 = **14**、**3 力量下两次 (5+3) = 16**（证明力量**逐击**生效） |
| **典型多敌** | Breakthrough：前排 −9、**后排也 −9**（`damage_all` 不请求目标选择）；播放者**自损固定 1 点**（不是 9） |
| **胜利终局** | 两个敌人都压到 5 点 → 升级 Breakthrough（13 全体）→ 双方归零 → `Terminal`、`base-spire:Victory`、终局回合 = 玩家回合数 |
| net 目录 | 公开目录新增 3 个内容 ID（`dazed`/`beckon`/`iron-wave`/`twin-strike`/`breakthrough` 一并声明）；383 项 net 检查不回归 |

## 3. 本轮修正/重构

1. **多处"冻结文本属性"检查被新内容反复打破**（卡片数、无 hook、批次全为 1、不使用 `damage_all`）。
   逐次改数字会掩盖问题，因此本轮做了两件事：
   - 新增**单一来源** `SliceFixtures.AuthoredBeyondSlice`（已迁内容的卡 id 列表）+ 逐个存在性核对；
   - 把"冻结切片属性"的断言**限定到 `SliceManifest.Cards` 的那 4 张卡**上。
   以后每加一批内容只改这一处，且旧的"切片属性"断言仍然有效地守着切片本身。
2. **`PutInHand` 对清单外的卡会静默用错数值**：升级路径原本从 `SliceManifest` 取升级数值，
   新卡不在清单里会抛异常。改为**要求显式传入升级数值**并给出明确错误信息（不猜、不回落）。
3. 自检脚本层面的小错：`NormalSteps.Single()` 在 TwinStrike 上有多于一个步骤（目标 + 伤害），
   改为按种类取；`static` 局部函数不能捕获 `model`。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 128 slice checks passed**（25 既有 + 4 B-05b + 87 B-05c + 12 项 B-06a） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（目录新增内容后不回归） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed |
| 依赖检查 / 清点 / J-01 / client / 契约 | 0 | 8 / 分类完整 / 150 / 50 / 210 |

## 5. 覆盖分母（"注册数 ≠ 规则覆盖"）

| 口径 | 值 |
| --- | --- |
| 来源里的 Ironclad 卡声明（`ironclad-cards.spire`） | **94** |
| 来源状态牌（`status-cards.spire`） | **2** |
| **迁移分母（冻结清单口径）** | **96** |
| 已迁入作者路径并可运行 | **9**（切片 4 + Dazed/Beckon + IronWave/TwinStrike/Breakthrough） |
| 其中**有行为证据**的 | 9（本批 3 张各有基础/升级或机制检查） |
| 明确**不支持/未迁** | 其余 87 张（其中若干有具体缺口，见 §1 的逐条反例） |

> 本批**不**把"目录里声明了 9 个内容 ID"当作"支持了 9 张卡"之外的东西：
> 目录声明、作者文本可编译、可加载、行为有证据是四件事，本报告分别列了证据。

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`lose_hp:<n>` 字面量参数）
- `BaseSpire/Composition/CombatSlicePackage.cs`（该步骤的降级分支）
- `BaseSpire/Authoring/Examples/slice-content.author`（IronWave / TwinStrike / Breakthrough）
- `net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs`（目录内容 ID）
- 切片检查：`SliceFrontendBasicCardChecks.cs`（新增 4 项）、`SliceFixtures.cs`（`AuthoredBeyondSlice` + 显式升级数值）、
  `SliceFrontendChecks/DomainChecks/HookChecks/PlayHookChecks/BatchChecks/CompatibilityChecks.cs`（限定到切片卡片集）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 7. 下一批建议

**B-06a 第四小批：抽牌原语（`draw:<n>`）**——它一次解锁多张基础牌（`Shrug It Off`、`Pommel Strike` 等），
且需要与"抽牌堆耗尽时重洗"的既有语义、以及端末/开局的抽牌点一起核对（随机流消费必须有来源）。
随后再按同样节奏补"对全体施加能力"（`Thunderclap`）与"以格挡为伤害"（`Body Slam`）。
