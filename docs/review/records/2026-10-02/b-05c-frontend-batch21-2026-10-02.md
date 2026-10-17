# B-05c：完整前端覆盖（第二十一批：出手方属性门控，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第二十一批）——**出手方属性门控（`unpowered`）** |
| 权威 | 来源见 [r-damage-source-evidence-2026-10-01.md](../2026-10-01/r-damage-source-evidence-2026-10-01.md) §2.5/§6/§8.5（`ValuePropExtensions.cs:5-12` 的 `IsPoweredAttack()`、`StrengthPower.cs:21`、`WeakPower.cs:30`、`VulnerablePower.cs:33`） |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（`properties = [unpowered]`：牌/药水/敌人行动三处声明）、`Composition/CombatSlicePackage.cs`（`DamageMonster`/`DamagePlayer` 新增 `Powered` 参数 + 阶段门控）、`BaseSpire.Slice.Tests/Cards/SliceFrontendUnpoweredChecks.cs`（新增 4 项） |
| 状态 | **部分完成（二十一批）**：属性门控已交付；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批20](b-05c-frontend-batch20-2026-10-02.md)、[药水与 run 范围说明](../../../design/mod-the-spire/potion-run-scope-2026-10-02.md) |

## 1. 交付内容

- **作者语法**：`properties = [unpowered]`，三处都能声明——
  牌的变体、药水的 `use`（两处都在 `properties` 键上）、敌人行动的 `move` 块。
  空/未声明＝正常（powered）；未知属性以 `invalid_value` 拒绝、非 `[...]` 形式以 `syntax` 拒绝（均带路径）。
- **运行时**：`DamageMonster` 与 `DamagePlayer` 各增加一个 `Powered` 参数；
  伤害链在**加性阶段与乘性阶段整体**外面套 `If(powered, …)` —— 因此一次 `unpowered` 攻击
  同时跳过**出手方的 Strength、出手方的 Weak、受击方的 Vulnerable**（与来源一致：三者都以
  `IsPoweredAttack()` 为必要条件，而不是各管各的）。
- 程序化路径与冻结作者文本都没有 `unpowered` 声明 → 全部 `Powered = true`，**行为逐字不变**。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | 牌/药水/敌人行动三处的 `unpowered` 逐条读出；未知属性 `ghostly` 与缺方括号的写法分别以 `invalid_value`/`syntax` 拒绝且路径含 `properties` |
| **powered 照旧** | 打击（未声明）：基础 **5**、+3 力量 → **8**、+虚弱 → **3**（5×0.75）、+易伤 → **7**（7.5 截断） |
| **unpowered 全跳过** | 同一场景下 `test-blunt`（`properties = [unpowered]`）在五种局面（基础／有力量／有虚弱／有易伤／三者全有）**都是 5** |
| 药水同源 | `unpowered` 药水在"玩家 3 力量 + 目标易伤"的局面下仍是 **5**（若生效会是 12） |
| 敌人行动可声明 | `move blunt-strike { damage = strike  properties = [unpowered] }` 被解析并参与循环（不可达行动仍按既有规则拒绝） |
| 默认路径 | 冻结文本的牌与敌人行动**全部是 powered** |

## 3. 本轮修正的缺陷（都在我自己的实现里）

1. **注释把声明吞掉了**：我用 PowerShell 双引号里的 ``` `r`n ``` 拼多行注释时写成了**字面** `\r\n`，
   于是 `var hitPowered = …` 整行落在 `//` 注释里。表现是链接期报 `'DamageMonster' takes 3 arguments but 4 were supplied`
   —— **链接期的参数个数检查正好抓住了这类"代码被注释掉"的错误**。
2. **批量替换误伤同名模式**：`scope.Field(cardRef, amountSymbol)));` 在 `damage` 与 `block` 两处都出现，
   我用无差别替换给 `GainPlayerBlock` 也加了一个参数（链接期 `takes 1 arguments but 2 were supplied` 报出）。
   教训：替换前先数出现次数（这次 `occurrences: 3` 就是提示）。
3. **门控最初没接到链上**：阶段函数的签名加了 `powered` 但调用点没传、链上也没有 `If(powered, …)`，
   于是"声明了 unpowered 却仍然加力量"。检查以 `expected '5', got '8'` 抓到，已补齐调用点与门控。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 110 slice checks passed**（25 既有 + 4 B-05b + 81 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 5. B-05c 剩余子项（仍未完成）

- **R-DAMAGE 其余未覆盖区**：`Shrink` 之类的**出手倍率型**能力（按层数乘，而不是"存在即乘"）、
  玩家侧易伤的组合、非整数比组合；
- **`BeetleJuice`（Shrink）** 与 **`FlexPotion`（临时能力的 `onStackChange`/`onExpire`）**、`BlessingOfTheForge`（可用 `upgrade_hand`，未做）；
- **药水栏是否进公开快照**（net 投影）——需规划者决定；
- run 内容声明（等 §15 B-07）；生成时的随机选择；变量的跨战斗语义。

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`properties` 键、`ParseUnpowered`、三处模型字段）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`Powered` 参数 + `If(powered, …)` 门控 + 全部调用点）
- `BaseSpire.Slice.Tests/Cards/SliceFrontendUnpoweredChecks.cs`（新增 4 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第二十二批：出手倍率型能力（`Shrink` 语义）**——它解锁 `BeetleJuice`，并补上 R-DAMAGE 证据里
"未覆盖的出手倍率"那一项。做法与批 12 的 `multiply-outgoing` 平行，但**按层数**乘
（`damage × (1 - k × stacks)` 之类的来源公式需先按证据确认），因此必须先核对来源再动手。
