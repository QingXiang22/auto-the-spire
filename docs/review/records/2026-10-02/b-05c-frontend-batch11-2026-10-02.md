# B-05c：完整前端覆盖（第十一批：能力行为程序，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十一批）——**能力的回合末行为程序**作者化（此前按 id 硬编码） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含"Power/敌人/药水及 run"） |
| 负责范围 | `Slice/Content/slice-content.author`（四个能力的回合末行为）、`Slice/SliceAuthorFrontend.cs`（`owner-turn-end` 解析与校验）、`Slice/CombatSlicePackage.cs`（按声明分派 + **修掉一处潜伏缺陷**）、`BaseSpire.Slice.Tests/SliceFrontendPowerProgramChecks.cs`（新增 4 项） |
| 状态 | **部分完成（十一批）**：能力回合末行为已作者化；B-05c 其余子项仍未开始（见 §6） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批10](b-05c-frontend-batch10-2026-10-02.md) |

## 1. 【发现并修掉的潜伏缺陷】`If(cond, a, b)` 没有 else 语义

引擎只有 `If(Expr condition, params Inst[] then)` 与 `IfElse(condition, then, otherwise)` 两个重载。
端末能力处理原本写成：

```csharp
resolveTurnEnd.If(
    Equal(powerDefinition, RitualId),
    IfElse(skip, [resetSkip], [gainStrength]),   // 第 2 个参数
    If(powerTicks, IfElse(...decrement...)))     // 第 3 个参数 —— 被当成 then 块里的**第二条语句**
```

即语义是"**如果是 Ritual**，就依次执行 [转力量] 和 [衰减检查]"，而**非 Ritual 的能力（易伤/虚弱）根本不进入这个 `If`**，
因此它们**永远不会按时长衰减**——与能力文本声明的"decremented after the enemy turn end, skipped on the turn it is applied"
以及 `SliceManifest` 的记录不一致。

本批把两处都改成 `IfElse`：

- **程序化路径**（`HardcodedPowerTurnEnd`）：改为真正的 if/else，非 Ritual 的能力按 `Ticks` 标志衰减。
- **作者路径**（`DeclaredPowerTurnEnd`）：按声明的 `owner-turn-end` 分派，语义与文档一致。

**影响面**：`net-the-spire` 的 383 项检查、`Core.TheSpire.Tests` 的 190 项、AI 侧 503 项全部不回归
（它们没有断言旧的非衰减行为）；切片内部 77 项也全绿。这是**行为修复**，不是放宽校验。

## 2. 作者语法与降级

```
power ritual    { ... owner-turn-end = convert-to-strength }   // 端末按层数转成力量（跳过被施加的回合）
power vulnerable{ ... owner-turn-end = tick }                  // 时长型：端末减 1（跳过被施加的回合）
power strength  { ... owner-turn-end = none }                  // 不随时间变化
```

- 取值：`none`（默认）/ `tick` / `convert-to-strength`；其它写法 → `invalid_value`（带路径）。
- 降级：端末遍历该所有者身上的能力时，**按声明分派**；没有声明的能力**不做任何事**（不留隐式默认）。
- **不留隐式默认的显式检查**：敌人行动里用了 `ritual`，却没有任何能力声明 `convert-to-strength`
  → `missing_key`（否则 Ritual 永远不转力量，而这是"必需事实"）。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | 四个声明的行为逐条读出；默认值（不写键）是 `none`；`owner-turn-end = explode` 以 `invalid_value` 拒绝；缺 `convert-to-strength` 以 `missing_key` 拒绝且路径指出 `powers` |
| **两条路径一致** | `作者路径与程序化路径的时长/Ritual 语义一致 [真实]`：同一实例挂 2 层易伤 → 第一个端末**跳过**（仍 2 层）→ 第二个端末减到 1 层；两步都与程序化路径逐值相同 |
| Ritual 语义 | `Ritual 按声明在端末转成力量... [真实]`：首回合上 4 层 Ritual 且**不转**；第二回合转成 4 层力量；第三回合 Ritual 再加 4 层（力量 8）而 **Ritual 自身层数不变**（力量不衰减、Ritual 累加是既有语义） |
| 冻结文本 | 4 个能力都声明了回合末行为（Ritual = convert-to-strength，Vulnerable/Weak = tick，Strength = none） |

## 4. 本轮修正的其他问题

1. **`convert-to <power>` 值里含空格**导致词法把第二个词当成新键（`syntax` 错误）→ 改成单 token 的
   `convert-to-strength`（切片里唯一的转换方向，也不需要再声明一个被引用的能力）。
2. **检查的期望算错（非实现错）**：我以为 Ritual 转换只发生一次，实际每个端末都按层数再加一次力量
   （4 → 8）。改为断言累加值并在注释里写明"力量不衰减"与"Ritual 累加"是两件事。
3. 10 个测试场景文本补上 `power ritual { ... owner-turn-end = convert-to-strength }`（用幂等的按行插入，
   避免重复插入）。

## 5. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 77 slice checks passed**（25 既有 + 4 B-05b + 48 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 6. B-05c 剩余子项（仍未完成）

实例变量（卡牌成长值）；生成/升级/变形/附魔；药水/run（事件/房间/奖励/地图）；
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；0.0.1 兼容入口。
另外：能力的**伤害修正**（Vulnerable ×1.5 / Weak ×0.75 / Strength +层数）仍按 id 写在伤害链里，
未随本批作者化——它是伤害计算的一部分，与"回合末行为"分开处理，记为后续批次。

## 7. 修改文件

- `BaseSpire/Slice/Content/slice-content.author`（四个能力的 `owner-turn-end`；新增 `power ritual` 声明）
- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`AuthorPower.OwnerTurnEnd`、`PowerTurnEnd` 词表、两条校验）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`DeclaredPowerTurnEnd` 按声明分派；**`HardcodedPowerTurnEnd` 改用 `IfElse` 修掉潜伏缺陷**）
- `BaseSpire.Slice.Tests/SliceFrontendPowerProgramChecks.cs`（新增 4 项）、10 个场景文本补能力声明、`SliceChecks.cs`（注册）；`SliceFrontendChecks.cs`（power 计数 3→4）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 8. 下一批建议

**B-05c 第十二批：能力的伤害修正作者化**（Vulnerable/Weak/Strength 的乘/加由声明驱动，而不是按 id 比较）。
它把伤害链也从"按 id"变成"按声明"，但直接牵涉已冻结的伤害语义（R-DAMAGE 证据），
必须先确认声明形式能表达固定点/单次截断的次序，再动手。
