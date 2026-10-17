# B-05b：切片前端（2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05b**（将 Strike/Defend/Bash/Headbutt、切片 Power/敌人的**作者表示**编译为通用 IR，与已有程序化包使用同一运行时） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移) |
| 负责范围 | `mod-the-spire/base-spire/BaseSpire/Authoring/Examples/slice-content.author`（作者文本）、`Authoring/SliceAuthorFrontend.cs`（前端）、`CombatSlicePackage.cs`（step 降级路径）、`BaseSpire.Slice.Tests/Authoring/SliceFrontendChecks.cs` + `SliceFixtures.cs`（检查） |
| 状态 | 实现已验证（切片检查 **29/29**，其中 4 项为 B-05b） |
| 前置 | B-05a（已完成，见 [b-05a](b-05a-content-manifest-2026-10-02.md)） |

## 1. 交付内容

**作者文本** `Authoring/Examples/slice-content.author`（嵌入资源）：声明 package/version、卡组、3 个 Power、2 个敌人与
4 张卡的 normal/upgraded 变体，每张卡带**显式的效果步骤列表**，并逐条镜像 B-05a 冻结清单的数值与出处
（同一 `source` 路径与 `line`）。

**前端** `SliceAuthorFrontend`：

- 词法/语法：`{}` 块、`k = v`、`steps = [a, b]`、字符串、`//`/`#` 注释；**裸名字按 `package { id = ... }`
  限定**到包命名空间（`strike-ironclad` → `base-spire:strike-ironclad`）。
- 声明校验：未知块/未知键/重复键/缺必需键/非法整数/悬空引用/重复身份/**未支持的 step**/
  缺少前置 step（`damage` 前必须有 `target`）/`apply_power` 缺 power 或 stacks/kind 与 step 冲突
  ——全部带**错误码 + 路径**在编译期拒绝。
- 降级：产出程序化包本来就消费的 `SliceCard`/`SlicePower`/`SliceEnemy` 记录 **加上**每张卡的 step 列表。

**包构建** `CombatSlicePackage.Build(includeTestHeadbutt, fromAuthorText)`：作者路径下卡牌/敌人/卡组数据来自
作者模型，且 `playBody` 从"按 kind 分派的通用分支"换成**按 `cardDefinition` 分派、逐卡展开 step 的指令链**：
`target → Decide(monsters)`、`damage → damageMonster(amount)`、`block → gainPlayerBlock(amount)`、
`apply_power → applyPower(powerId, stacks)`、`top_from_discard → Decide(discard) + 移到抽牌堆顶 + RecordKnownTop`。
**默认路径（`fromAuthorText = false`）行为逐字不变**。

## 2. 退出证据逐条对照

| 退出证据要求 | 检查 |
| --- | --- |
| 解析 → IR | `B-05b 作者文本解析为声明模型`：4 卡 / 3 能力 / 2 敌人 / 卡组，与冻结清单**逐条**比对 kind、cost、normal/upgraded amount、power、stacks、source line；步骤序列逐卡核对 |
| 链接 → 实际加载 | `B-05b 作者前端包与程序化包产生同一份声明与行为`：作者包真的 `RuleSetBuilder.Add(...).Build()` 成功，区域与类型都在（通用 `CoreTheSpire.Engine`，不经过 Legacy） |
| 基础/升级行为分别有证据 | 同一检查里对**两条路径**分别断言：Strike 基础 6 / 升级 9；Defend 基础 5 / 升级 8；Bash 基础 8+易伤 2 / 升级 10+易伤 3 |
| Headbutt 续接不回归 | `B-05b 作者前端包的 Headbutt 续接与已知位置事实不回归`：出牌→目标选择→弃牌选择逐段暂停并续接；置顶到位置 0；`RevealedMembers` 授权实例；隐藏牌堆不泄漏其余成员；已知牌是快照内实体 |
| 身份与牌序事实不回归 | 同上（`RevealedMembers` / `Members` 计数 / 实体存在性），与 B-04 的既有 25 项检查并存且全绿 |
| **不能只是包住旧 `EffectExecutor`** | 作者前端只引用 `CoreTheSpire.Engine`（`EnginePackageBuilder`/`RuleSet`/`Session`）；效果由**通用 IR 原语**展开（`Decide`/`Call`/`AddToCollection`/`SetField` 等），没有任何 Legacy 类型的构造或调用。检查里"链接→加载"断言的是通用 `RuleSet` |

## 3. 编译期拒绝（带路径）

`B-05b 作者前端在编译期拒绝未支持/不一致的声明` 覆盖 **10** 类非法输入，逐条断言拒绝且带 `Code` 与 `Path`：

| 输入 | 错误码 |
| --- | --- |
| 未知块 | `unknown_block` |
| 未知键（`kindness =`） | `unknown_key` |
| 未支持的 step（`summon_minion`） | `unknown_step` |
| `damage` 缺少前置 `target` | `missing_prerequisite` |
| `apply_power` 缺少 power | `missing_key` |
| 缺少 upgraded 变体 | `missing_key` |
| 卡组引用未声明的卡 | `dangling_reference` |
| `Skill` 声明 `damage` | `unsupported_combination` |
| 步骤表为空 | `invalid_value` |
| `cost = one` | `invalid_value` |

另外前端还拒绝：`normal`/`upgraded` 步骤序列不同（`unsupported_capability`）、同表同时有 `block` 与 `damage`
（`unsupported_combination`）——这两条是**当前运行时的能力边界**，不是"作者随便写"。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 **0 警告**（顺手修掉了我此前 N-05 代码引入的 3 条 CS8604） |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 29 slice checks passed**（25 既有 + 4 B-05b） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 63 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（**不回归**：默认路径行为未变） |

## 5. 未覆盖与边界（B-05c 的输入）

1. **只有 4 张卡**：Bash/Headbutt 之外的效果（消耗、能力牌、X 费、生成、变形、批处理、实例变量）没有作者语法；
   它们是 B-05c 的范围。故 `steps` 词表目前只有 5 个动作。
2. **敌人与能力只作者化了数据**：HP/仪式/打击伤害与 power id/stacks 从作者文本进包，但邪教徒的**行动程序**
   （Incantation/DarkStrike 循环）仍是程序化包里的通用 IR 程序——B-05c 才把行动也表示成作者文本。
3. **没有游戏内升级路径**：升级行为是用"把实例标成升级并写入声明的升级数值"构造出来核对的（切片尚无营火/升级流程，
   属 B-07）；因此它验证的是**运行时按声明数值执行**，不是"升级流程本身正确"。
4. **作者文本与程序化清单是两份数据**：检查逐条比对二者一致，但**没有**让程序化路径也读作者文本
   （那会让冻结清单失去独立对照）。两份数据的偏差由 `B-05b 解析为声明模型` 的逐条断言捕捉。
5. **未做性能/规模验证**：本前端只覆盖 4 张卡，编译期成本未测。
6. **0.0.1 兼容入口未动**：旧 `.spire` 语法与旧后端保持原样；本前端是 **slice-frontend@0** 形态的新语法，
   与 0.0.1 并存而不是替换。

## 6. 修改文件

- `BaseSpire/Authoring/Examples/slice-content.author`（新增；`BaseSpire.csproj` 增加嵌入规则）
- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（新增：模型/解析/校验/降级）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`fromAuthorText` 参数 + step 降级的指令链；默认路径不变）
- `BaseSpire.Slice.Tests/Authoring/SliceFrontendChecks.cs`、`SliceFixtures.cs`（新增；`SliceChecks.RunAll` 注册）
- `net-the-spire/Net.TheSpire.Tests/SnapshotChecks.cs`（修掉 3 条 CS8604 可空警告）

未修改：`Engine/` 语义、Legacy、net 契约与 Python 侧。未提交、未清理共享工作区。

## 7. 下一批依赖

**B-05c（完整前端覆盖）**：分批把 normal/upgraded、目标、X 费、hooks、选择、批处理、实例变量、生成/升级/变形、
Power/敌人/药水及 run 作者表示迁入同一前端，并保留 0.0.1 兼容入口；完成判据是"每项有语法/编译结果/运行语义/检查；
升级空 hooks 不隐式继承；随机候选顺序和消费点有来源；缺能力先补 Engine 通用能力，不加 STS 节点"。
B-05a 的 **354 条注册声明 / 85 张卡池卡** 仍是分母；本批把其中 4 张（含 1 张测试场景卡）带进了作者路径。
