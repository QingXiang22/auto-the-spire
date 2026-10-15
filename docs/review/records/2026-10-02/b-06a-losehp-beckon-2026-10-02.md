# B-06a：基础战斗内容批（第二小批：HP 损失原语 + Beckon，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-06a 基础战斗内容批**（第二小批）——**HP 损失（`lose_hp`）原语 + `Beckon` 状态牌** |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（B-06a 行："…**死亡/胜负**…来源对照和生命周期场景成立"）；来源：`ContentSource/status-cards.spire:25-60` |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（`lose_hp` 步骤 + hook 允许面）、`Slice/CombatSlicePackage.cs`（`LosePlayerHp` 过程 + 步骤降级）、`Slice/Content/slice-content.author`（Beckon）、`BaseSpire.Slice.Tests/SliceFrontendBeckonChecks.cs`（新增 4 项） |
| 状态 | **本小批完成**；B-06a 其余子项未完成（见 §6） |
| 前置 | [第一小批](b-06a-status-cards-2026-10-02.md)（`unplayable`/`ethereal`/`Status`） |

## 1. 来源语义（先看来源，再补原语）

```
card Beckon {
    normal = { type = status  cost = 1  target = { selectNone = {} }  keywords = {}   play = {}
               hooks = { turnEndInHand = { loseHp = {
                   amount = 6  target = context.owner
                   properties = { move unpowered unblockable } } } } }
```

三个关键点决定了实现方式：**它是 `loseHp` 而不是伤害**、**`unblockable`**（不经格挡）、
**`unpowered`**（不经修正链）。因此**不能**复用 `DamageChain`——复用会把格挡吸收与 Strength/易伤
乘性修正一起带进来，那与来源不符。

## 2. 交付内容

- 新过程 `LosePlayerHp(Amount)`：`HP = max(0, HP - Amount)`，然后 `HP == 0 → SetTerminal(Defeat, Turn)`。
  死亡/终局判定与 `DamagePlayer` **同一套写法**（同一个 `defeat` 终局与 `playerTurn` 字段），
  因此**胜负次序一致**；不发出伤害事件（它不是伤害）。
- 新步骤 `lose_hp`（可在卡牌步骤与**端末 hook**里声明；已加入 hook 允许面）。
- 冻结作者文本新增 `card beckon`（`kind = Status`、`cost = 1`、`steps = []`、
  `hook turn_end_in_hand { steps = [lose_hp] }`、`amount = 6`、来源行 25）。

## 3. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法/编译 | `lose_hp` 在端末 hook 里被接受；hook 允许面变为 `[damage_all, block, apply_power, lose_hp]`（既有期望随之**有意更新**）；Beckon 数值 6、步骤为空 |
| 加载/行为 | `可声明且它不进消耗区`：端末扣 6 HP；Beckon 不是 ethereal → **进弃牌堆** |
| **绕过格挡** | 先给 10 点格挡再端末：**HP 照扣 6**（若格挡吸收，HP 一点不掉）——反证就在 HP 这一行；格挡在**新玩家回合开始**时按规则清零（与本次损失无关，已在断言里写明） |
| **不走修正链** | 玩家持有 5 力量 + 3 易伤：**仍是 6**（`unpowered`） |
| **胜负次序** | 把 HP 压到 6 再端末：HP **归零** → `SessionStatus.Terminal`、终局 `base-spire:Defeat`、终局回合字段 = 玩家回合数（与伤害同源） |

## 4. 本轮修正的缺陷（都在我自己的实现/检查里）

1. **把格挡断言写错了**：我原本断言"格挡原封不动 = 10"，实际端末后进入新玩家回合，格挡按规则清零。
   —— 我把"未吸收"的证据改为**HP 扣了 6**（这才是反证），并显式写明格挡清零的原因，而不是把断言放宽。
2. **终局 id 少写命名空间**：`session.Terminal!.Definition.QualifiedId` 是 `base-spire:Defeat`。
3. **多处"冻结文本无 hook / 卡片数"检查被新内容打破**：这些检查原本遍历 `model.Cards`，
   断言"全部无 hook""共 4/5 张"。正确做法是把它们**限定在冻结切片的那几张卡**上
   （`SliceManifest.Cards`），而不是放宽断言——B-06a 之后还会有更多内容，逐次放宽会掩盖真问题。

## 5. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 124 slice checks passed**（25 既有 + 4 B-05b + 87 B-05c + 8 项 B-06a） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed |
| 依赖检查 / 清点 / J-01 / client / 契约 | 0 | 8 / 分类完整 / 150 / 50 / 210 |

## 6. B-06a 剩余（未完成）

- **基础牌批**：基础 Ironclad 卡（B-05a 冻结清单里的基础部分）尚未成批迁入；只有 4 张切片卡 + 2 张状态牌；
- **典型多敌**：3+ 敌、召唤/复活场景未做；
- **能量/费用**：`cost` 与 X 费已有，但**费用修正/临时费用**未做；
- **牌区域**：五个牌区已有，更细的顺序语义未展开；
- **胜负**：失败已有显式检查（本批与 B-02），**胜利**的显式生命周期场景仍缺一条；
- **药水**：4 张可跑；`FlexPotion` 仍缺引擎触发点（待裁定）。

## 7. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`AuthorSteps.LoseHp`、hook 允许面）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`LosePlayerHp`、`lose_hp` 步骤降级）
- `BaseSpire/Slice/Content/slice-content.author`（Beckon）
- 检查：`SliceFrontendBeckonChecks.cs`（新增 4 项）、`SliceFrontendStatusCardChecks.cs`（卡数）、
  `SliceFrontendChecks/DomainChecks/HookChecks/PlayHookChecks/BatchChecks/CompatibilityChecks.cs`（限定到冻结卡片集）、
  `SliceFrontendHookStepChecks.cs`（hook 允许面）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 8. 下一批建议

**B-06a 第三小批：胜利终局的显式生命周期场景 + 基础卡批的第一组**（例如把 B-05a 清单里的
基础 Ironclad 攻击/技能按"先补原语再声明"的纪律成批迁入，并给出"注册数 ≠ 规则覆盖"的分母更新）。
