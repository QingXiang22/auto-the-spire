# B-06a：基础战斗内容批（第九小批：能力声明式触发点 + FeelNoPain，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-06a 基础战斗内容批**（第九小批）——**能力的声明式触发点** + `FeelNoPain` |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（B-06a 行）；来源：`Powers/Content/ironclad-powers.spire:28`（能力）+ `Cards/Content/ironclad-cards.spire:1976`（卡） |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（`trigger`/`when-card-type`/`trigger-effect` 三个声明键 + 校验）、`Composition/SliceManifest.cs`（`SlicePower` 三个属性）、`Composition/CombatSlicePackage.cs`（触发链 + 消耗点接线）、`Authoring/Examples/slice-content.author`（能力 + 卡）、`net` 目录、切片检查（新增 5 项） |
| 状态 | **本小批完成**；B-06a 其余子项仍未完成（见 §6） |
| 前置 | 第八小批（`apply_power_self`） |

## 1. 来源是"声明式"的，因此实现也做成声明式

`ironclad-powers.spire` 里能力的写法**本身就是声明**：

```
power FeelNoPain {
  trigger = afterCardExhausted
  effect = gainBlock(1, perPowerStack=FeelNoPain)
}
```

因此本批不是"给 FeelNoPain 写死一段代码"，而是把三个事实做成**通用的声明键**：

| 声明键 | 含义 | 当前支持面 |
| --- | --- | --- |
| `trigger` | **触发时点** | 只支持 `after_card_exhausted`（切片**真的会调用**的时点） |
| `when-card-type` | 只有这一类牌触发（来源 `whenCardType`） | Attack/Skill/Power/Status；**必须**与 `trigger` 同现 |
| `trigger-effect` | 触发效果（来源的 `effect = gainBlock(1, perPowerStack=…)`） | 只支持 `gain-block-per-stack:<n>`；**必须**与 `trigger` 同现 |

未实现的时点（例如 `after_card_drawn`）**被显式拒绝**——不允许"声明了但永远不触发"。
运行时是"声明链"：把声明了该触发点的能力逐个展开成"读层数 → 层数 > 0 就给持有者格挡"，
**没有新原语**（用既有的纯过程 `PowerStacksOf` + `GainPlayerBlock`），且没有声明的能力完全不进链。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法/编译 | 三个键被解析（触发点、空 `when-card-type`、`gain-block-per-stack:1`）；卡的两形态层数 3/4、两处来源行 |
| 拒绝面 | 未实现的触发点→`invalid_value`；只写 `trigger-effect`（无 `trigger`）→`missing_prerequisite`；词表外的效果→`invalid_value`；层数 `1000`→`invalid_value`；只写 `when-card-type`→`missing_prerequisite` |
| **运行语义** | 有 3 层 FeelNoPain 时，**消耗**一张牌 → **+3 格挡**；升级（4 层）→ **+4** |
| **反例（区分真假）** | **没有**该能力时消耗牌**不**加格挡；**有**该能力但打出**不消耗**的牌也**不**加格挡 |
| 端末路径 | 端末因 `ethereal` 被消耗的牌同样走触发点（接线在两条消耗路径上） |
| 冻结文本 | 触发点、触发效果、能力来源行 28、卡来源行 1976、两形态层数逐条一致 |

## 3. 本轮修正的缺陷（两条，都值得记）

1. **局部函数的捕获顺序**：`TriggerChain` 用到了 `gainPlayerBlock`，而后者在 Build 体里声明更晚 ⇒
   `CS0841`（本地变量在声明之前无法使用）。把**过程与局部函数一起**移到 `gainPlayerBlock` 之后解决。
2. **检查里的"变异"因换行符静默失效**（这条更危险）：我用 `Replace("…trigger = …" + "\n", "")`
   制造"缺 key"的负例，但 C# **原始字符串字面量保留文件里的 CRLF**，于是 `"\n"` 匹配不上、
   变异没生效——检查却"看起来在跑"。表现是"必须被拒绝"失败。改为**注释掉 key**（`# trigger removed`）
   这种与换行无关的变异。**教训：检查里用字符串替换做变异时，不能假设换行符。**
3. 顺带：**依赖检查抓到了我新引入的类型名遮蔽**（见 §4）。

## 4. 【K-09a 边界】依赖检查抓到新引入的**名称遮蔽**，并因此增强

我在本批新增了一个静态类 `PowerTrigger`，而**旧后端（`Legacy/Content.cs`）本来就声明了同名类型**。
`check_dependencies.py` 立刻报出 `CombatSlicePackage.cs` 在类型位置引用了 `PowerTrigger`（Legacy 类型）。
虽然它实际解析到的是切片自己的类型，但这是真实的**歧义风险**（一旦某个文件同时 `using` 两个命名空间就危险）。

处理（两边都做，而不是把检查放宽）：
1. **改名**：`PowerTrigger` → `AuthorPowerTrigger`；顺手把新路径里**既有的**两处遮蔽也清掉
   （`Parser` → `AuthorParser`、`Token` → `AuthorToken`，都在切片前端内部）。
2. **增强检查**：新路径自己声明的同名类型**不再是 Legacy 依赖**，但"两处同名"作为
   **`new-path/no-shadowed-legacy-type-names`** 显式报出（不静默放过）。改名后该检查为 0 违规。
   检查总数 8 → **9**。

## 5. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 153 slice checks passed**（25 既有 + 4 B-05b + 87 B-05c + 37 项 B-06a） |
| `python tools/dependency-check/check_dependencies.py` | 0 | **9/9**（新增遮蔽检查；违规 0） |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| AI / J-01 / client / 契约 / 清点 / doc-check | 0 | 503 / 150 / 50 / 210 / 分类完整 / 0 problems |

## 6. 覆盖分母与剩余

| 口径 | 值 |
| --- | --- |
| 迁移分母（94 张 Ironclad 卡 + 2 张状态牌） | **96** |
| 已迁入并可运行 | **16**（切片 4 + Dazed/Beckon + IronWave/TwinStrike/Breakthrough + PommelStrike/ShrugItOff + Thunderclap + Rampage + Entrench + Inflame + FeelNoPain） |
| 明确未迁 | 其余 80 张 |

- 来源里还有**另一种触发点**（`trigger = afterCardPlayed` + `whenCardType = attack`，即 `RagePower`）——
  它额外需要"回合末**整体移除**能力"（来源 `removeAfterPlayerTurnEnd = true`），属下一批；
- **多能力同牌**、**能量获取**、**条件**、**按名字计数**、**交互式抽牌**、**`BodySlam`**（来源缺失）等仍待补。

## 7. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`AuthorPowerTrigger`、`PowerTriggerEffect`、三个键与校验、遮蔽改名）
- `BaseSpire/Composition/SliceManifest.cs`（`SlicePower.Trigger/WhenCardType/TriggerEffect`）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`TriggerAfterCardExhausted` + 声明链 + 两条消耗路径接线）
- `BaseSpire/Authoring/Examples/slice-content.author`（`power feel-no-pain` + `card feel-no-pain`）
- `net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs`（目录内容 ID）
- `tools/dependency-check/check_dependencies.py`（遮蔽检查 + 新路径同名类型集合）
- 切片检查：`SliceFrontendPowerTriggerChecks.cs`（新增 5 项）、`SliceFixtures.cs`、`SliceChecks.cs`、两处能力计数
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 8. 下一批建议

**B-06a 第十小批：`after_card_played` 触发点 + `when-card-type` 过滤 + 回合末整体移除（`owner-turn-end = remove`）**
——一次解锁 `RagePower`/`Rage`，并让"本回合有效的触发型能力"这条通用形态成立；
`remove` 需要先确认切片有没有"销毁实例"的原语，没有就按"缺能力先补 Engine"的次序先补它。
