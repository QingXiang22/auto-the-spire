# B-05c：完整前端覆盖（第十二批：伤害修正，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十二批）——**能力的伤害修正**作者化（Vulnerable/Weak/Strength 由声明驱动，不再按 id 比较） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)；伤害次序的冻结证据见 [r-damage-source-evidence](../2026-10-01/r-damage-source-evidence-2026-10-01.md) |
| 负责范围 | `Authoring/Examples/slice-content.author`（三个修正声明）、`Authoring/SliceAuthorFrontend.cs`（`damage` 解析与校验）、`Composition/CombatSlicePackage.cs`（加性/乘性阶段按声明生成）、`BaseSpire.Slice.Tests/Powers/SliceFrontendDamageModifierChecks.cs`（新增 3 项） |
| 状态 | **部分完成（十二批）**：伤害修正已作者化；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批11](b-05c-frontend-batch11-2026-10-02.md) |

## 1. 作者语法与降级

```
power strength  { ... damage = add-per-stack:1 }        // 加性阶段：攻击者每层加 1
power weak      { ... damage = multiply-outgoing:0.75 }  // 乘性阶段（攻击者侧）
power vulnerable{ ... damage = multiply-incoming:1.5 }   // 乘性阶段（承受者侧）
```

- 取值：`none`（默认，不参与修正）/ `add-per-stack:<非负整数>` / `multiply-outgoing|incoming:<两位小数>`。
  非法写法（负数、三位小数、未知种类）→ `invalid_value`（带路径）。**乘数必须能被固定点精确表示**，
  否则会在引擎里悄悄失精度——这条在编译期拒绝。
- 降级：伤害链的**加性阶段**遍历声明了 `add-per-stack` 的能力（层数 × 系数一次算清，避免多次截断）；
  **乘性阶段**先攻击者侧（`multiply-outgoing`）再承受者侧（`multiply-incoming`），两侧都只看"能力是否存在"
  （层数是时长，不是倍率）；**下界与唯一一次截断**保持原样（`Maximum(0)` + `FixedToInt(Truncate)`）。
- 程序化路径保持原来的按 id 表达式（逐字不变）。

## 2. 退出证据（对已冻结伤害语义的复现）

| 判据 | 检查 |
| --- | --- |
| 语法 | 四种形态逐条读出（含默认 `none`）；`add-per-stack:-1`、`0.333`、`multiplier:1.5` 分别以 `invalid_value` 拒绝且路径含 `damage` |
| **R-DAMAGE 反例** | `作者路径复现 R-DAMAGE 反例（5 × 0.75 × 1.5 = 5.625 → 5）[真实]`：基础 5；Weak 单乘 → 3（3.75 **截断**）；Vulnerable 单乘 → 7（7.5 截断）；**两者同时 → 5**（5.625 只截断一次，中间不取整）；力量 +3 → 8；力量 3 + 两侧乘 → 9 |
| 与程序化路径一致 | `冻结作者文本声明了三个修正，且与程序化路径一致 [真实]`：Weak+Vulnerable 与 Strength 两组场景下，作者路径（按声明）与程序化路径（按 id）给出**相同伤害** |
| 冻结文本 | 三个修正的类型与数值逐条核对（力量加性、虚弱 0.75、易伤 1.5） |

## 3. 本轮修正的缺陷

1. **检查的期望算错（非实现错）**：我把 3.75 写成"截断成 4"（把截断当四舍五入），实际是 **3**；
   另一处把 9 写成 8。两处都改为实际值并在断言文本里写明依据（截断 vs 恰好整数）。
2. **过早收紧的规则已回退**：我曾加过"声明了 convert-to-strength 就必须声明 add-per-stack"的校验，
   但它把"这个场景不关心力量加伤"当成错误。正确做法是**默认值 `none` 就是声明词表的一部分**
   （未声明＝不参与修正），因此该规则已删除；依赖力量加伤的测试场景自己显式声明。
3. **fixture 补丁重复插入**：按行插入声明时对同一文本插了多条，导致 `Single()` 抛
   "more than one matching element"；改为幂等（先检测是否已存在）并清理重复。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 80 slice checks passed**（25 既有 + 4 B-05b + 51 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 5. B-05c 剩余子项（仍未完成）

实例变量（卡牌成长值）；生成/升级/变形/附魔；药水/run（事件/房间/奖励/地图）；
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；0.0.1 兼容入口。
另外：**伤害上限阶段**（cap）在切片里仍无等价机制（`SliceManifest` 已注明），它属于能力声明后续可扩展的方向。

## 6. 修改文件

- `BaseSpire/Authoring/Examples/slice-content.author`（三个 `damage = ...` 声明）
- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`AuthorPower.DamageModifier/Value`、`PowerDamage` 词表、`ParsePowerDamage`）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`AdditiveStage` / `MultiplicativeStage` 按声明生成；尾部保持原样）
- `BaseSpire.Slice.Tests/Powers/SliceFrontendDamageModifierChecks.cs`（新增 3 项）、测试场景补力量声明、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第十三批：实例变量（卡牌成长值）** 或 **0.0.1 兼容入口**（两者都不牵涉新的引擎能力，成本可控）：
前者把 `card variables`（B-05a 记录的卡牌变量面）带入作者文本；后者补一条"新旧前端共存"的显式验证
（旧 `.spire` 内容与切片作者文本在同一进程内都能加载并注册）。
