# B-05c：完整前端覆盖（第十八批：伤害上限阶段，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十八批）——**伤害上限阶段（cap）**，补齐 `SliceManifest` 与 R-DAMAGE 证据标注的缺口 |
| 权威 | 来源次序见 [r-damage-source-evidence-2026-10-01.md](../2026-10-01/r-damage-source-evidence-2026-10-01.md) §1.3/§2.5/§6（`Hook.cs:2511-2557` + `:1565`）；[开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移) |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（`damage-cap` 解析）、`Slice/CombatSlicePackage.cs`（`CapStage`）、`BaseSpire.Slice.Tests/SliceFrontendDamageCapChecks.cs`（新增 4 项） |
| 状态 | **部分完成（十八批）**：cap 阶段已交付；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批17](b-05c-frontend-batch17-2026-10-02.md) |

## 1. 作者语法与降级

```
power ward { name = Ward  role = Ward  effect = "caps incoming damage"
             owner-turn-end = tick  damage-cap = 2.5 }
```

- `damage-cap = <非负两位小数>`：**承受者**持有该能力时，对即将造成的伤害施加一个上限。
- 降级：在**乘法阶段之后、下限 0 与唯一一次截断之前**插入
  `SetLocal(stacks, PowerStacksOf(target, id))` + `If(stacks > 0, SetLocal(fixed, Minimum(fixed, cap)))`，
  即来源的 `Hook.cs:2539-2555`（取最小上限、全程 decimal、阶段之间不取整）。
- 只有**声明了** `damage-cap` 的能力会生成指令；程序化路径与冻结作者文本都没声明 → 零指令、行为不变。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | 上限值逐条读出（未声明为空）；负数、三位小数、非数字分别以 `invalid_value` 拒绝且路径含 `damage-cap` |
| **位置：乘法之后** | 基础 5 无上限 → **5**；有上限 2.5 → **2**（5 → 2.5 → 截断）；易伤无上限 → **7**（7.5 截断）；**易伤 + 上限 → 2**（7.5 → 2.5 → 2，说明上限在乘法之后） |
| **上限作用于乘法结果** | 上限 7.2 + 易伤：乘法结果 7.5 → **7**；若上限作用于原始基础值 5，结果会是 5 —— 因此这条断言能把两种理解区分开 |
| 诚实边界 | 检查里明确记下：**"先夹再截断"与"先截断再夹"在整数包络内不可区分**（例如 3.75 配上限 3.6/3.4 两种次序都得到 3），因此本批只断言"上限作用于乘法结果"这一可判别性质，不假装能区分不可区分的两种次序 |
| 默认路径 | 冻结文本没有上限声明；程序化路径受控开局仍成立 |

## 3. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 102 slice checks passed**（25 既有 + 4 B-05b + 73 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 4. 本轮修正的缺陷（检查侧）

**检查名与内容不符**：我最初把这条检查命名为"可区分先截断再夹与先夹再截断"，但正文推导出
**两者在整数包络内不可区分**。已把检查名改成它真正断言的性质（"上限作用在乘法结果上"），
并把"不可区分"的推导留在检查注释与报告里——不假装做出了做不到的区分。

## 5. B-05c 剩余子项（仍未完成）

**药水/run**（事件/房间/奖励/地图，整块内容域；开工前需要先出范围说明）；
**生成时的随机选择**（`generate-random`：需声明候选集与随机流）；**变量的跨战斗/跨实例复制**语义；
R-DAMAGE 证据里其余"未覆盖区"（Weak/Shrink 出手倍率、`Unpowered` 属性门控、玩家侧易伤、
非整数比组合）——它们属伤害语义的**其余**缺口，本批只补齐了 cap 一项。

## 6. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`AuthorPower.DamageCap`、键表、`ParseDamageCap`）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`CapStage` + 插入到乘法与下限之间）
- `BaseSpire.Slice.Tests/SliceFrontendDamageCapChecks.cs`（新增 4 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第十九批：药水/run 的范围说明**（先出范围再动手）。药水与 run 是**整块内容域**，
开工前需要明确：本轮的药水是哪几张（B-05a 记的 6 张）、run 流程覆盖到哪一步（战斗外还是含房间/奖励/地图）、
以及它们各自需要哪些新引擎能力（例如药水使用是否要新的动作、run 是否要新的会话类型）。
这份范围说明本身就是交付物，随后再按其分批判实现。
