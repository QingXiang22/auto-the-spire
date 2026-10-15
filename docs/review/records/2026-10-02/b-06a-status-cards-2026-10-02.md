# B-06a：基础战斗内容批（第一小批：状态牌关键词 + Dazed，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-06a 基础战斗内容批**（第一小批）——状态牌的两个关键词 `unplayable` / `ethereal` 与第一张状态牌 `Dazed` |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（B-06a 行："基础/升级牌、常用Power、典型多敌、药水、能量/费用、牌区域、死亡/胜负；逐机制更新net profile/coverage"）；来源：`ContentSource/status-cards.spire:3-23` |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（`unplayable`/`ethereal`/`kind = Status`/显式空步骤表）、`Slice/CombatSlicePackage.cs`（卡牌字段 + 出牌 guard + 端末消耗）、`Slice/Content/slice-content.author`（Dazed）、`net` 公开映射（两个布尔字段 + Status 类别）、`BaseSpire.Slice.Tests/SliceFrontendStatusCardChecks.cs`（新增 4 项） |
| 状态 | **本小批完成**；B-06a 的其余内容（基础牌批、典型多敌、能量/费用、死亡/胜负、其余药水）未完成（见 §5） |
| 前置 | B-05c（批 1〜23）、R-DAMAGE |

## 1. 先补原语，再声明内容（这是清单的纪律）

状态牌的来源语义只有两个关键词 + 空效果：

```
card Dazed {
    upgradeable = false
    normal = { type = status  rarity = status  cost = 0
               target = { selectSelf = {} }  keywords = { ethereal unplayable }  play = {}  hooks = {} }
    ...
}
```

因此本批先补三个原语：

| 原语 | 语义（来源） | 降级 |
| --- | --- | --- |
| `unplayable = true` | `keywords = { unplayable }`：不能主动打出 | 牌实体新增 `Unplayable` 字段；**出牌 guard** 加上 `Not(Field(played, Unplayable))` |
| `ethereal = true` | `keywords = { ethereal }`：端末仍在手牌时**消耗** | 牌实体新增 `Ethereal` 字段；端末手牌循环里 `If(ethereal) → 进消耗区 else → 走原有取舍` |
| `kind = Status` | `type = status` | 引擎卡牌种类枚举加第四项 `Status`；**net 公开类别**加 `net:cat:Status` |

另外放宽了一处：`steps = []` 现在是**显式的空效果**（状态牌没有效果）；**缺整个 `steps` 键仍然拒绝**——
"没写"不等于"写了空"。这条改动让 B-05b 的一条负例（"步骤表为空必须被拒绝"）失效，已把它换成
"省略 `steps` 键必须被拒绝"，并新增一条正向断言（`steps = []` 被接受）。

## 2. 【net 契约】新字段必须显式处置——投影器拒绝了静默丢弃

加了两个卡牌字段后，`Net.TheSpire.Tests` 立刻报：

```
[N-02-UNDECLARED-FIELD] base-spire:Card.Unplayable 没有公开映射处置（不允许静默丢弃公开字段）
```

这正是协议纪律在起作用：**新引擎字段不能悄悄出现在 wire 上，也不能悄悄消失**。
处置决定（写进 profile 并附理由）：**发布**这两个布尔字段，理由是"可见牌的公开属性，且消费者需要能解释
'为什么这张牌不在可提交组合里'"（wire 已有 boolean 值类型，无需自造）。同时新增 `net:cat:Status` 类别。
H-02 的 `category_ids` 期望值随之更新（这是**有意的契约变化**，不是放宽检查）。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | `kind = Status`、`unplayable`、`ethereal` 被解析；非布尔值（`yes`）以 `invalid_value` 拒绝并指向 `unplayable` |
| **不可打出** | `unplayable 的牌不进合法提交组合，强制提交被 guard 拒绝 [真实]`：Dazed 不在 `LegalAction.Bindings` 里、Strike 在；强制提交 Dazed 被 **guard** 拒绝且会话状态不变 |
| **端末消耗** | `ethereal 端末进消耗区，普通牌照常进弃牌堆 [真实]`：端末后 Dazed 在**消耗区**且**不在弃牌堆**；Strike 在弃牌堆且不在消耗区 |
| 来源对照 | Dazed 的 `kind`/两个关键词/`SourceLine = 3` 与来源逐条一致；它**不在起始卡组**里（受控场景不变） |
| 编译/加载 | 引擎枚举含 `Status`；net 目录声明 `net:cat:Status` 与两个布尔字段 |

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 120 slice checks passed**（25 既有 + 4 B-05b + 87 项 B-05c + 4 项 B-06a） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | **383 passed**（含 H-02 类别期望的**有意更新**） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed |
| `python tools/dependency-check/check_dependencies.py` | 0 | 8 passed（新路径仍不引用 Legacy） |
| 其余（J-01 / client / contract / inventory） | 0 | 150 / 50 / 210 / 分类完整 |

## 5. B-06a 剩余（未完成）

| 子项 | 现状 |
| --- | --- |
| **基础牌批** | 只迁了 Dazed；其余状态牌（`Beckon`）与基础 Ironclad 卡（B-05a 记的 96 张里的基础部分）未迁 |
| `Beckon` 需要的新原语 | `turnEndInHand` 里的 **`loseHp`（自身、无格挡）** —— 切片没有"HP 损失"步骤；且它必须与死亡判定/胜负的次序一致，需单独一批 |
| 典型多敌 | 切片固定 2 敌；"典型多敌"场景（3+ 敌、召唤）未做 |
| 能量/费用 | 已有（`cost`/X 费，批 4），但**费用修正/临时费用**未做 |
| 牌区域 | 已有手牌/抽牌/弃牌/消耗/在玩/本回合打出；**"牌区域"更细的语义（如弃牌堆顺序公开）**未展开 |
| 死亡/胜负 | 切片已有终局（B-02 覆盖失败；胜利场景需补一条显式检查） |
| 药水 | 4 张声明可跑；`FlexPotion` 仍缺引擎触发点（**待裁定**） |

**因此 B-06a 未完成**，本报告只声明"状态牌关键词 + Dazed"这一小批的四态证据。

## 6. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`unplayable`/`ethereal` 键与模型、`kind = Status`、显式空步骤表）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`Unplayable`/`Ethereal` 字段、出牌 guard、端末消耗、`Status` 枚举成员）
- `BaseSpire/Slice/Content/slice-content.author`（Dazed 声明）
- `net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs`（两个布尔字段的公开处置 + `net:cat:Status`）
- `net-the-spire/Net.TheSpire.Tests/SnapshotChecks.cs`（H-02 类别期望更新）
- 切片检查：`SliceFrontendStatusCardChecks.cs`（新增 4 项）、`SliceFrontendChecks.cs`（负例替换 + 正向空步骤 + 冻结卡数）、其余 3 处冻结卡数期望
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 7. 下一批建议

**B-06a 第二小批：`loseHp`（自身、无格挡）原语 + `Beckon`**——它补齐状态牌里唯一还缺的机制，
且必须与死亡判定/胜负次序一起设计（写进"来源对照 + 生命周期场景"证据）；随后按同样节奏推进
基础 Ironclad 卡批与"典型多敌"场景。
