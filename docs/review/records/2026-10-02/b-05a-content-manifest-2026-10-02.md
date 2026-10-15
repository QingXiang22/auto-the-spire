# B-05a：内容与原语清单（冻结迁移 manifest，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05a**（从当前实际注册结果冻结迁移 manifest：声明 ID/源文件与摘要/normal-upgraded/依赖原语-hook/来源/已有检查/未支持项；明确首个单人 Ironclad 范围） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移) |
| 负责范围 | `tools/content-manifest/`（新增机器产物工具 + 冻结清单）、本报告 |
| 状态 | 机器产物已生成（354 条注册声明 / 278 条源文件声明），三项计数分别列出 |
| 前置 | M1（已完成） |

## 1. 机器产物（不是手抄的清单）

```powershell
dotnet run --project tools/content-manifest -- tools/content-manifest/manifest-2026-10-02.json
```

- 生成方式：走**真实注册路径**——`new BaseSpirePackage()` → `ContentRegistry.Build()` → 遍历
  `GameDefinition` 的各类别字典；源文件侧则逐个嵌入资源解析并编译，取声明的 ID。
- 冻结产物：[manifest-2026-10-02.json](../../../../tools/content-manifest/manifest-2026-10-02.json)（约 127 KB）
  含 `sources`（逐文件路径/sha256/字节数/声明 ID）、`categories`（逐声明属性）、`totals`、`differences`、`scope`。

**三条分母分别列出（互不替代）**：

| 口径 | 数值 | 说明 |
| --- | --- | --- |
| 源文件声明数 | **278** | 逐个 `.spire` 编译后取到的 ID（含卡池声明） |
| 实际注册数 | **354** | 走注册路径后 `GameDefinition` 里的全部声明（含 C# 声明的类别） |
| Ironclad 卡池数 | **85** | `base-spire:ironclad-card-pool` 的成员数 |

分类注册数：cards **96**、powers **81**、enemies **89**、encounters **63**、relics **11**、potions **6**、
state variables **4**、afflictions **2**、card pools **1**、characters **1**、runs **1**、run events **0**。

## 2. 源文件与摘要（逐文件）

| 源文件 | 字节 | 声明数 |
| --- | --- | --- |
| `base-spire/ironclad-cards.spire` | 99041 | 95 |
| `base-spire/enemies.spire` | 47320 | 97 |
| `base-spire/ironclad-powers.spire` | 5934 | 27 |
| `base-spire/enemies-glory.spire` | 5472 | 11 |
| `base-spire/enemies-gremlins.spire` | 4405 | 10 |
| `base-spire/enemies-knights.spire` | 2061 | 3 |
| `base-spire/enemies-souls.spire` | 1945 | 3 |
| `base-spire/enemies-forgotten.spire` | 1695 | 4 |
| `base-spire/enemies-overgrowth.spire` | 1475 | 4 |
| `base-spire/status-cards.spire` | 1347 | 2 |
| `base-spire/enemies-amalgams.spire` | 1035 | 1 |
| `base-spire/enemies-colonies.spire` | 950 | 2 |
| `base-spire/enemies-gardens.spire` | 939 | 2 |
| `base-spire/shared-powers.spire` | 895 | 7 |
| `base-spire/potions.spire` | 832 | 6 |
| `base-spire/state.spire` | 383 | 4 |
| `base-spire/potion-powers.spire` | 222 | 1 |

另有 3 个 C# 源文件登记了路径与 sha256（`BaseSpirePackage.cs`、`IroncladCardPool.cs`、
`StandardActMapGenerator.cs`）——**遗物、遭遇与角色/流程由代码声明**，其 ID 不在 `.spire` 里。

## 3. 差异（不可达声明必须显式处理，不靠删源文件缩小分母）

| 差异 | 数量 | 明细 |
| --- | --- | --- |
| `.spire` 声明但未注册（**不可达声明**） | **0** | 无 |
| 已注册但没有 `.spire` 来源 | **76** | 恰为 C# 声明的类别：encounters 63 + relics 11 + characters 1 + runs 1 = 76（可对账，无零头） |

> 这 76 条**不是**"找不到来源"，而是"来源是 C# 文件"。工具把它们与 `.spire` 来源分开标注，避免把
> "代码声明"误报成"不可达"。

## 4. 首个单人 Ironclad 范围

| 分组 | 数量 | 说明 |
| --- | --- | --- |
| Ironclad 卡池内卡牌 | **85** | `scope.ironclad_single_player_cards`，逐 ID 列在清单里 |
| 无卡池归属的卡牌 | **11** | `beckon`、`dazed`、`demonic-shield`、`entrench`、`giant-rock`、`infection`、`slimed`、`tank`、`toxic`、`well-laid-plans`、`wound`——状态牌/衍生牌与**未进卡池**的卡 |
| 多人专属卡 | **2** | `demonic-shield`、`tank`（`MultiplayerOnly == true`）；**显式分组，不计入单人范围** |

其余类别按同一口径给出：enemies 89 / encounters 63 / relics 11 / potions 6 / afflictions 2 / state variables 4
（逐 ID 与来源在清单里）。

## 5. 迁移所需属性（已逐条落在清单里）

- **normal / upgraded**：`has_upgrade`（96 张卡里 **90** 张有升级形态）、`upgraded_cost`、`upgraded_effect`
  以及每个 hook 的升级对应项；`HasUpgrade` 的判据是"任一升级形态存在"（效果/费用/关键词/伤害值/
  变量/四个 hook），因此升级为空 hooks 的卡不会被误判成"有升级"。
- **依赖原语 / hook**：每张卡列出实际用到的 hook 集合（`on_exhaust`/`turn_end_in_hand`/`on_enter_combat`/
  `before_card_played`/`after_card_played`/`after_auto_post_play`）与变量数；**96 张卡里只有 6 张带 hook**，
  其余是纯效果树——这正是 B-05b/B-05c 需要先映射的效果原语面。Power（81 条）列出 `events`/`lifecycle`/
  `incoming_damage_modifiers`/`block_gain_modifiers`/`variables` 计数。
- **X 费**：`cost_kind == X` 的卡 **2** 张（迁移时必须单独核对捕获时点）。
- **已有检查**：本清单只记声明与属性；"已有检查"以 `Core.TheSpire.Tests`（旧后端 190 项）与
  `BaseSpire.Slice.Tests`（新切片 25 项）为入口，B-05a 不重复统计测试数（避免与"注册数"混淆）。
- **未支持项**：切片路径尚只覆盖 4 张卡（`strike-ironclad`/`defend-ironclad`/`bash`/`headbutt`，B-05b）；
  其余 92 张卡与全部 enemies/relics/potions/encounters 仍只存在于旧后端——**未支持项本来就是分母的一部分**，
  本清单不把它们删掉。

## 6. 本轮修掉的缺陷（发生在我自己的清单工具里）

1. **类别记账不一致导致假"不可达"**：状态变量类别在生成 JSON 时没有把 ID 加进 `registered` 集合，
   于是 4 条状态变量被误报成"声明但未注册"，同时注册总数少算 4（350 而不是 354）。
   修复后 `declared_but_not_registered == 0`、`registered_total == 354`。
   **这正是 B-05a"三个数分别列出、不得让分母被静默缩小/放大"要防的错误**，因此记在报告里。
2. 顺手确认了一件容易误判的事：**76 条"无 `.spire` 来源"的注册项恰好等于 C# 声明的四类之和**
   （63+11+1+1），不是漏登记的零头——工具因此把它们标成 `<csharp-declared>` 而不是"缺来源"。

## 7. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build tools/content-manifest/ContentManifest.csproj` | 0 | 0 错误 |
| `dotnet run --project tools/content-manifest -- <path>` | 0 | 生成 129635 字节清单（354 注册 / 278 声明 / 池 85） |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告（新工具不在 solution 内，不干扰既有构建） |

## 8. 边界与未覆盖

1. **清单覆盖的是旧后端（Legacy）的注册结果**：新通用 `Engine/` 切片只声明 4 张卡与少量敌人，
   其清单属 B-05b/B-05c；两份分母**不能相加**。
2. **"已有检查"没有逐声明映射**：报告给出检查入口与数量，没有为 354 条声明逐条标注"哪个检查覆盖它"；
   逐条映射属 B-05c 的完成判据（"每项有语法/编译结果/运行语义/检查"）。
3. **来源摘要只到文件级**：逐声明的行号/片段未记录。
4. **范围外的角色/内容未清点**：`sts2/` 恢复工程里存在其他角色与多人内容，本清单只覆盖 base-spire
   已注册的部分；其余内容的清点属后续批次（第15节整局范围），本轮不发明数字。
5. **run events = 0**：当前 base-spire 没有注册任何 run event——这是事实，作为缺口保留，不静默省略。

## 9. 下一批依赖

**B-05b（切片前端）**：把 Strike/Defend/Bash/Headbutt 与切片 Power/敌人的**作者表示**编译为通用 IR，
并与已有程序化包使用同一运行时；验收要求"解析→IR→链接→实际加载→基础/升级行为分别有证据；
Headbutt 续接、身份与牌序事实不回归；不能只是包住旧 `EffectExecutor`"。
本清单的 354 条声明与 85 张卡池卡是 B-05b/B-05c 的**冻结分母**。
