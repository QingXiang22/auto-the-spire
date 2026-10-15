# B-05c：完整前端覆盖（第二批：hooks，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第二批）——hook 的作者语法与运行时 |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移) |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（hook 词表/模型/校验）、`Slice/CombatSlicePackage.cs`（共享步骤降级 + 三个触发点）、`BaseSpire.Slice.Tests/SliceFrontendHookChecks.cs`（新增 5 项检查） |
| 状态 | **部分完成（第二批）**：本批交付 `on_exhaust` 与 `turn_end_in_hand` 两个 hook；B-05c 其余子项仍未开始（见 §4） |
| 前置 | B-05c 第一批（[batch1](b-05c-frontend-batch1-2026-10-02.md)）、B-05b |

## 1. 本批交付

1. **新步骤 `exhaust_self`**：打出后进**消耗区**（已有 `base-spire:ExhaustPile` 区域）而不是弃牌堆，
   并在进入时触发 `on_exhaust`。
2. **两个 hook 的作者语法**（逐变体声明）：

   ```
   card test-ember {
     normal {
       amount = 3
       steps = [target, damage, exhaust_self]
       hook on_exhaust { steps = [block] }
     }
     upgraded { amount = 5  steps = [target, damage, exhaust_self] }
   }
   ```

   模型里 hooks 是**逐变体**的：`AuthorHooks{OnExhaust, TurnEndInHand}`，升级变体没声明就是没有。
3. **`turn_end_in_hand`**：回合结束时仍在手牌的牌按定义触发（在丢弃之前）。
4. **三个触发点共用同一套步骤降级**：`StepInstructions(scope, cardRef, attacker, target, targetSlot, discardSlot, steps)`
   被出牌（含目标）、`on_exhaust`、`turn_end_in_hand` 三处复用，因此"hook 里能做什么"与"出牌时能做什么"不会漂移。
5. **编译期规则**（都带路径）：
   - `on_exhaust` 却没有 `exhaust_self` → `unreachable_hook`（当前运行时没有别的路径会消耗这张牌）；
   - 未知 hook 名 → `unknown_hook`；
   - hook 里超出运行时能力的步骤（如 `top_from_discard`）→ `unsupported_capability`（当前 hook 只支持
     `damage_all` / `block`：它们不需要目标选择、也不需要弃牌槽位）；
   - hook 块里只允许 `steps` 键 → 其它键 `unknown_key`。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法/编译 | `B-05c hooks: 语法接受两个 hook，并拒绝不可达/超能力的 hook 声明（带路径）`：模型里 `on_exhaust`/`turn_end_in_hand` 的步骤表被正确读出；4 类非法声明分别以 `unreachable_hook`/`unknown_hook`/`unsupported_capability` 拒绝 |
| 运行语义（消耗 + on_exhaust） | `普通变体打出即消耗并触发 on_exhaust [真实]`：牌进**消耗区**、不进弃牌堆；`damage` 与 `on_exhaust` 的数值都来自该卡声明 |
| **升级空 hooks 不隐式继承** | `升级变体未声明 hook 时不触发（**不隐式继承**）[真实]`：同一张卡升级后仍然消耗自己、伤害变为升级值，但格挡**没有增加**——普通变体的 hook 没有被继承；另有模型级断言（只给普通变体声明 hook 时升级变体的 hooks 为空） |
| 运行语义（端末在手） | `turn_end_in_hand 在回合结束按声明数值触发... [真实]`：普通(2)/升级(4) 两种形态分别对**两名**敌人各造成声明数值；牌离开手牌；没有消耗步骤则**不进**消耗区 |
| 冻结文本不受影响 | `冻结作者文本没有被本批改动`：4 张卡仍无 hook、不消耗自己；程序化路径的受控开局仍成立 |

## 3. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 38 slice checks passed**（25 既有 + 4 B-05b + 4 B-05c批1 + 5 B-05c批2） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 63 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 4. 本轮修正的两处真实缺陷（都在我自己的实现里）

1. **hook 分支取反**：结果区与端末两处都写成 `IfElse(upgraded, 普通hook, 升级hook)`（条件为真=已升级，却取了普通
   变体）。检查 `普通变体打出即触发 on_exhaust` 立刻以 `expected '3', got '0'` 抓到——**这正是"不隐式继承"
   这类规则需要真实运行时断言的原因**：只做语法检查会漏掉它。
2. **观测量选错**：`turn_end_in_hand` 最初用 `block` 当观测量，但格挡会在**下回合开始被清掉**（且被敌人攻击消耗），
   断言因此不可判别。改为用 `damage_all`（伤害耐久）并分别断言两名敌人；同时把"进弃牌堆"的断言放宽为
   "**不进消耗区**"——因为结束回合的抽牌会把弃牌堆洗回抽牌堆，位置不是稳定事实。

## 5. B-05c 剩余子项（仍未完成）

X 费（捕获时点）、选择/批处理与随机候选顺序来源、实例变量、生成/升级/变形/附魔、**Power/敌人的行动程序**、
药水/run（事件/房间/奖励/地图）、`before|after_card_played` 与 `on_enter_combat` 两个 hook、
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`）、0.0.1 兼容入口。
B-05a 的分母（354 条声明 / 85 张卡池卡）不变：本批带进作者路径的仍只有 4 张冻结卡 + 检查内的测试场景卡。

## 6. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`exhaust_self`、`AuthorHooks`、两词块头解析、hook 校验）
- `BaseSpire/Slice/CombatSlicePackage.cs`（共享 `StepInstructions`；结果区分派（消耗区 + on_exhaust）；
  端末 `TurnEndHookChain`；`exhaustPile` 使用）
- `BaseSpire.Slice.Tests/SliceFrontendHookChecks.cs`（新增 5 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/` 语义、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第三批：X 费（捕获时点）+ `on_enter_combat`/`before|after_card_played` 三个 hook**。
X 费需要先补"捕获时点"的通用能力（当前引擎有 `CostKind`/`HasEnergyXCost` 的概念但没有消费点程序），
因此建议先做 hook（可复用本批的共享降级与触发点机制），再做 X 费。
