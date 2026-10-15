> 记录整理（2026-10-06）：原交付、复核及逐批证据保留；开放项已并入[主清单](../../tofix/README.md)，执行合同见[对应提示词](../../../design/agent-goals.md)。本文按原日期阅读，不作为当前待办入口；移入records不表示问题解决或父项验收。

# A 线：本会话的阻塞点与需要决策的事项（2026-10-05）

> 2026-10-06负责人处置：本文件的blocked是执行会话历史状态，当前A线继续进行；技术建议与顺序按[扩大复核与决定](../2026-10-06/base-spire-owner-decision-2026-10-06.md)及[主清单](../../tofix/README.md)接续。Thorns须before站点，加权随机已有冻结接口组合可行证据，不直接派core新原语；注释损坏保留随改随修债。以下执行者原文保留，不代表新派单。

> 提交人：本会话的 A 线执行 agent。**状态：blocked，等待决策**。
> 本文件只写"现在卡在哪、需要谁决定什么、以及怎么继续"；**交付细节与依据不在这里重复**，见
> [A 线合并审计报告](a-base-line-audit-2026-10-05.md)（尤其 §8.6/§8.9/§8.11/§8.12/§8.13）。
> 主清单入口仍是 [docs/review/tofix/README.md](../../tofix/README.md)。

## 1. 本会话结束时的已验证基线（任何人可一条条复跑）

```
dotnet build AutoTheSpire.sln                                        # 0 错误 0 警告
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests       # All 95 engine checks passed.
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests  # All 310 slice checks passed.
dotnet run --project net-the-spire/Net.TheSpire.Tests                # 393 executed, 393 passed, 0 failed
python tools/doc-check/check_docs.py                                 # 0 problem(s)
python tools/dependency-check/check_dependencies.py                  # 10 passed, 0 failed
git diff --check                                                     # 干净
```

构建描述行（`BaseSpire.Slice.Tests` 输出）：

```
BASE-01 构建：ruleset=264c1758c0be75fc 卡=68 能力=29 敌人=35 起始牌组=10 弱遭遇=4 普通遭遇=8 房间=15 未支持=134 排除遭遇=8
```

本会话交付的批次、每批的判别性检查与真 bug 修复，逐条写在
[A 线合并审计报告](a-base-line-audit-2026-10-05.md) 的 §8.1–§8.13；检查数从 294 → **310**，
敌人 16 → **35**。

## 2. 阻塞条件（具体、可核对，连续 4 轮未变）

1. **本会话上下文预算耗尽。** 剩余项（见 §4）都是中等规模改动（前端新形态 + 运行时站点 + 判别性检查 + 文档 + 五套验证）。
   预算不足时强做，最可能留下**半成品状态**；我选择保住"工作区可验证的全绿"而不是冒险开工。
2. **两次委派子 agent 都停在"只读侦察、零文件写入"**（这是可核对的事实，不是猜测）：
   - 委派 A（`Thorns`，范围 = 前端形态 + 两侧运行时站点 + 检查 + 文档）：3 个完整回合，
     `thorns` 在 `CombatSlicePackage.cs`/`SpirePowerSource.cs` 中出现次数始终为 **0**；
   - 委派 B（`removePower`，范围 = 前端 step + 运行时 + 检查）：2 个完整回合，
     `remove_power`/`RemovePower` 计数始终为 **0**，检查文件未创建；
   - 两次提示里都已写明"**先写代码，不要先长篇探索**"、并给出同类写法的文件与行号 —— 仍然无效。
   我已把这条教训写进 A 线报告 **§8.12 ⑤**（含"把任务拆成单步 + 在提示里给出要新增片段的位置与形状"的对策）。

**这不是技术性阻塞**：工作区全绿，且剩余项的精确改动点、顺序与验收方式都已写在 §8.12。

## 3. 需要决策的三件事（请负责人/其他 agent 定）

### 3.1 剩余机制由谁、以什么粒度做？（最关键）

可选方案，供选择：

- **方案甲（推荐）**：**开新会话**由 A 线执行者按 §8.12 的顺序做，且**每次只做一项、单步提交**：
  跑完 6 条验证命令 + 给出 `BASE-01 构建：…` 的**前后对比**，再进下一项。
- **方案乙**：把 §8.12 的项按"单步"切给不同 agent 并行（前提是**不同文件**：前端 `SpireEnemySource.cs` /
  运行时 `CombatSlicePackage.cs` / 检查文件分开），由负责人协调修改顺序。
- **方案丙**：由负责人本人实现（其余 agent 只做复核）。
- **不再采用的方案**：把整个机制项（含前端+运行时+检查+文档）一次性交给子 agent —— 本会话实测两次都零产出。

### 3.2 `CombatSlicePackage.cs` 的注释损失（1142 个 U+FFFD）要不要继续补？

- 现状：**只影响注释可读性，不影响编译与行为**（事故后所有验证都通过）；清单 `artifacts/text-repair-report.txt`，
  损坏字节留档 `artifacts/combat-slice-package-encoding-damage-2026-10-05.cs.bak`。
- 若继续补：必须**逐批**（每批后 `dotnet build` 0 错 0 警告 + Slice 全过），**严禁** PowerShell 文本
  管道/`Set-Content` 往返（事故根因）；优先文件头与机制段。
- 若暂不补：建议在主清单里保留为**显式技术债**，避免后续会话以为"注释完好"。

### 3.3 遇到核心（`core-the-spire/**`）缺口时的边界

`Thorns`/`if(…)`/`summonRandomEnemy` 等一旦触及核心缺口（例如需要新的 IR 原语或钩子），按仓库约定
**不得自行重开核心架构**。请明确：是**允许**在本任务内提出并等待负责人决策，还是**必须**停在
"记录缺口 + 最小复现"这一步。

## 4. 剩余工作与**已定位的精确改动点**（照 §8.12 做即可）

顺序（按"机制现值 × 内容收益"）：`Thorns` → `if(…)` + `hasPower`/`powerAmountAtLeast` →
`modifyEnemyVariable`/`modifyPowerVariable`/`setEnemyHealth` → `summonRandomEnemy`/`stealGold`/`escape` →
房间类型 / 精英(15)·BOSS 队列 / 真实奖励 / 地图 / 幕转换与四幕胜负。

两项**已经侦察到位**（可直接开工，不必重新推导）：

- **`Thorns`**（§8.12 第 2 条，含源码事实与内容写法）：
  ① 前端 `ReadEvents` 目前只接受 `positiveAmount=false`、事件类型只限 damage/block/power ⇒
  `before_damage` 会被整条拒；② 运行时反击链 `TriggerAfterDamageTaken` **只挂在 `DamagePlayer`**，
  **怪物侧缺对称站点**（要在 `DamageMonster` 之后补同形状钩子）；两处反伤都必须 `powered=false`（不递归）。
- **`removePower(<具名能力>)`**：前端加一个新 step（能力名经"已加载能力表"解析，解析不到即记未支持），
  运行时复用已有的"按 `DefinitionId` 销毁能力"链式写法（`ExpireAtOpponentTurnEnd` 那段形状），
  对不存在的能力是空操作；检查要有 3 条判别性断言（层数归零 / 空操作 / 只影响自己）。

## 5. 明确**不**声称完成的部分

- 房间类型、精英/BOSS 队列、真实奖励、地图、幕转换、四幕真实胜负 —— **未实现**，主清单里保持未关闭。
- 依赖未加载能力的敌人/能力（`Artifact`、`Slow`、`TerritorialPower`、`Stock`、`ImbalancedPower`、`Tender` 等）
  仍逐条列在 `ExcludedEncounters`/`Unsupported`，**不是**静默跳过。
- 本会话的 `blocked` 只表示"本会话无法继续推进"，**不代表** A 线职责已完成。
