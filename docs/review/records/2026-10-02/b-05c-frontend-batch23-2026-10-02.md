# B-05c：完整前端覆盖（第二十三批：整手升级药水，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第二十三批）——**`BlessingOfTheForge`**（升级整手牌的药水，纯内容） |
| 权威 | 冻结药水语法：`ContentSource/potions.spire:19`（`usage = combatOnly`、`target = self`、`use = upgradeCards(pile=hand)`）；[开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移) |
| 负责范围 | `Slice/Content/slice-content.author`（药水声明 + 栏位）、`BaseSpire.Slice.Tests/SliceFrontendBlessingChecks.cs`（新增 2 项） |
| 状态 | **部分完成（二十三批）**：已交付；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批16](b-05c-frontend-batch16-2026-10-02.md)（`upgrade_hand`）、[批20](b-05c-frontend-batch20-2026-10-02.md)（药水机制） |

## 1. 交付内容（纯内容，无新机制）

```
potion blessing-of-the-forge {
  name = BlessingOfTheForge  rarity = uncommon  usage = combat-only  target = self
  source = base-spire/ContentSource/potions.spire  line = 19
  use { amount = 0  steps = [upgrade_hand] }
}
potions { beetle-juice  blessing-of-the-forge }
```

它复用批 16 建立的 `upgrade_hand` 步骤（整手牌置为升级形态并换成各自的升级数值），因此本批**没有新增代码**，
只新增声明与检查——这正是"效果步骤词表复用"设计带来的收益。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 声明 | 步骤＝`upgrade_hand`、`target = self` |
| 运行 | `声明被解析，且用后整手牌变成升级形态 [真实]`：用前手里都不是升级形态 → 用后**全部**是；**没有生成新牌**（还是原来那几个实例）；升级后的打击造成 **9**（而非 6，证明数值也跟着换了） |
| 冻结文本 | 声明为 `self`/`uncommon`/`upgrade_hand`，且**在药水栏里**（可达）；清空栏位的版本被拒绝（`unreachable_potion`） |

## 3. 本轮修正的缺陷（我自己引入的）

**插入位置落在上一个药水块内部**：我用 `potion beetle-juice {` 作为插入锚点，结果新药水被插到该块的**开头**，
于是解析器把新药水当成 beetle-juice 的嵌套内容（表现为 `potions[...blessing-of-the-forge]: 药水栏引用了没有声明的药水`）。
修法是把声明移到前一个块**结束之后**。教训：往块结构文件里追加声明时，锚点要取**块尾**（`}` 之后）而不是块首。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 116 slice checks passed**（25 既有 + 4 B-05b + 87 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 5. B-05c 剩余子项（仍未完成）

- **`FlexPotion`**：需要临时能力的 `onStackChange`/`onExpire` 触发点——**引擎能力缺口**（能力系统目前只有
  "回合末行为"与"伤害修正"两类声明，没有"层数变化/过期"触发点）；
- **药水栏是否进公开快照**（net 投影）——跨模块契约，需规划者决定；
- R-DAMAGE 其余未覆盖区（玩家侧易伤组合、非整数比组合）；
- run 内容声明（等 §15 B-07）；生成时的随机选择；变量的跨战斗语义。

## 6. 修改文件

- `BaseSpire/Slice/Content/slice-content.author`（`potion blessing-of-the-forge` + 栏位）
- `BaseSpire.Slice.Tests/SliceFrontendBlessingChecks.cs`（新增 2 项）、`SliceChecks.cs`（注册）、
  `SliceFrontendPotionChecks.cs`（冻结药水计数 1→2）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第二十四批：药水栏进公开快照**（net 侧投影与公开词典），仍然按本会话的做法做成**可选形状**
（只有内容声明了药水时才投影该堆），因此不会改变现有 net 行为；随后处理 `FlexPotion` 所需的引擎触发点，
或转入 §14 的 K-09a/B-06a（非阻塞项）。
