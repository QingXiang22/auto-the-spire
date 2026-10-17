# base-spire 受控战斗切片 B-01 交接

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-01、W-06，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01；**B-01 已交付**（声明 + 冻结来源清单 + 可控初始状态 + 受控流程）；B-02 至 B-04 亦已交付，见 [base-spire-combat-slice-b02-b04-2026-10-01.md](base-spire-combat-slice-b02-b04-2026-10-01.md) |
| 负责范围 | `mod-the-spire/base-spire/BaseSpire/Slice/`（新增切片包）、`mod-the-spire/base-spire/BaseSpire.Slice.Tests/`（新增检查项目）；`Core.TheSpire.Engine.Tests` 的夹具与检查；`Engine/StateStore.cs` 的快照缺陷修复 |
| 未触碰 | 旧 DSL 内容（`ContentSource/*.spire`）、旧后端 C#、`net-the-spire/`、`ai-the-spire/`、他人未提交改动；未 git commit |
| 前置 | K-07 已满足（同一运行时装载两个非 STS 包）；K-00/K-01 至 K-07 见 [core-engine-k01-k07-2026-10-01.md](core-engine-k01-k07-2026-10-01.md) |

## 1. 执行命令与结果

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | `0 个警告 / 0 个错误`（新项目已注册进 solution） |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | `All 9 slice checks passed.` |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | `All 32 engine checks passed.`（新增 1 项：循环快照回归） |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | -532462766 | 本批时点记录：与 K-00 基线**相同的首个失败**（Bludgeon 效果形状，属旧 DSL 工作流）；本批未触碰旧后端。**后续状态（2026-10-01 R-LEGACY 复核）**：该失败已修复，现为 `190 passed / 0 failed / 190 executed`（返回码 0），见 [r-legacy-2026-10-01.md](../../archived/r-legacy-2026-10-01.md) |

## 2. 交付内容

**新增**

| 文件 | 内容 |
| --- | --- |
| `BaseSpire/Composition/SliceManifest.cs` | 冻结来源清单：4 张卡、2 个 Power、1 个敌人，每项带 `文件:行`；战斗常量（80 HP、3 能量、10 手牌上限、5/4/1 起始牌组）；未取到原版规则的值显式标为 **TestScenario** |
| `BaseSpire/Composition/CombatSlicePackage.cs` | 用通用 `EnginePackageBuilder` 声明的 `base-spire:combat-slice-v1`：Battle 作用域、Player/Monster/Card/Power 类型、CardKind 枚举、5 个区域（手牌/抽牌堆/弃牌堆/消耗堆/本回合历史）+ Monsters 集合 + Powers 关系、6 个事件、2 个终结结果、PlayerView 投影、5 个过程（SetupBattle / DrawCards / StartPlayerTurn / StartEnemyTurn / EndPlayerTurn）、2 个动作（PlayCard / EndTurn） |
| `BaseSpire.Slice.Tests/`（4 文件） | 9 项 B-01 可执行检查 |

**修改**

| 文件 | 改动 |
| --- | --- |
| `Engine/StateStore.cs` | `CollectionMembers` / `RelationTargets` 改为返回**快照**（见第 3 节缺陷 1） |
| `Engine/IrTypeChecker.cs` | 删除"投影拥有者字段不得为 Public"这条我此前自造过严规则（合法设计：Owner 公开且同时作为私有字段的归属判据） |
| `Engine.Tests/FixturePackages.cs`、`EngineChecks.cs` | 新增 `ClearHand` 动作与循环快照回归检查 |
| `AutoTheSpire.sln` | 注册 `BaseSpire.Slice.Tests` |

## 3. 本批发现的引擎缺陷（契约影响）

1. **查询不是快照（已修）**：`SessionState.CollectionMembers` 对有序集合直接返回内部 `List`，而 `ForEachInst` 的语义是"进入循环时固定候选表"（`Ir.cs` 的注释明确如此）。结果：切片 `EndPlayerTurn` 把手牌逐张移入弃牌堆时只处理了 5 张中的 3 张（索引随列表收缩而跳跃）。现 `CollectionMembers`/`RelationTargets` 一律返回快照，并新增引擎回归检查 `K-03 a loop over a collection iterates a snapshot, not the live list`（微包手牌 3 张 → 全部移出）。
   **对 B/N 批的影响**：任何"遍历集合同时改写它"的内容此前都会**静默漏项**，现在行为正确；若有人在快照语义修复前记录了行为基线，需要重测。
2. **投影拥有者字段过严（已修）**：原检查拒绝"拥有者字段为 Public"，会挡住 STS 这类"Owner 公开 + 私有字段按 Owner 判定"的合法设计。

## 4. 实际覆盖（可核对）

- **声明与链接**：包冻结为 `base-spire@combat-slice-v1`；`EngineCompatibility` 不匹配 → `SchemaInvalid`；缺依赖 → `MissingDependency`；identity 稳定。
- **core 无 STS 分支**：检查用反射断言 `Core.TheSpire` **不引用** `BaseSpire`（`GetReferencedAssemblies`），并且引擎自检项目（无 base-spire 引用）与切片项目跑同一套运行时。
- **同名卡可区分**：开局手牌 5 张 Strike 是 5 个不同 `InstanceId`，定义值相同。
- **可控初始状态**：玩家 80/80、能量 3/3、回合 1；DampCultist 52/54、MoveIndex 0；手牌 5、抽牌堆 5，合计恰好是清单里的 5/4/1 牌组且顺序确定。
- **受控流程**：`PlayCard` 花 1 能量、牌离开手牌、进入弃牌堆与本回合历史；`EndTurn` → 弃掉整手 → 敌方回合推进 MoveIndex → 下一玩家回合抽 5 张（回合数 2、弃牌堆 5、手牌 5）。
- **扩展性**：第二个包通过 `Extends` + 限定 ID 给 `base-spire:Player` 追加字段并链接成功。

## 5. 未实现与残余风险

0. **来源核对已完成**：7 项切片数值已逐项与 `sts2/` 恢复工程对照，证据表见
   [sts2-slice-value-extraction-2026-10-01.md](sts2-slice-value-extraction-2026-10-01.md)。结果：Strike/Defend/Bash/Headbutt
   数值与选择语义、Strength 加法语义、Vulnerable ×1.5、Ritual 与行动序列、遭遇编成**全部一致**；发现 1 个真实内容缺陷
   （DampCultist HP 两档写反，见 [damp-cultist-hp-range-inversion-2026-10-01.md](../../archived/damp-cultist-hp-range-inversion-2026-10-01.md)）
   与 3 处非数值差异（Headbutt 提示文案、易伤"跳过首次"的实现层次、伤害取整点）。切片清单已按**权威值**冻结
   （普通 51–53、Tough 52–54），不继承旧内容的反转值。B-02 的结算顺序与截断点也已由此确定。

1. **B-02 未做**：伤害/格挡结算、Strength 与 Vulnerable 的实际算术（含 +50% 与取整顺序）、弃牌堆洗回、DampCultist 的 Incantation→DarkStrike 循环与意图、DeathBlow/复活、胜负判定、不可打出的牌规则。相关位置在代码里以 `B-02:` 注释显式标出，**当前 `PlayCard` 不结算效果**（只扣费、移动、发事件），`StartEnemyTurn` 只推进 MoveIndex。
2. **B-03 未做**：Headbutt 的"从弃牌堆选一张置顶抽牌堆"选择与暂停续接（`RequestDecision` 语义在引擎里已可用并有测试，但切片未接）。
3. **B-04 未做**：机制→字段→来源→检查→证据覆盖表、公开事实（位置 0、洗牌更新、已知牌序）的投影契约。
4. **测试场景值**：每回合抽 5 张**没有来源**（`Environment.cs:760-772` 的回合开始顺序只设置回合数/能量/格挡），已标为 TestScenario；起始牌组顺序、DampCultist 取范围最小值同理。这些**不能**当作原版行为。
5. **旧 DSL 迁移未动**：`ContentSource/*.spire` 仍是旧语法，B-05 才做作者前端；切片是从旧内容**手工核对数值**后在新引擎里重新声明，不是自动迁移。
6. **P-01 仍未交付**：三次委派均在读完大文档后耗尽上下文且未留下任何文件（`docs/contracts/`、`tools/contract-check/` 仍不存在）。建议拆成两个更小的批次：先 `wire.schema.json` + `invariants.md`，再规范化/摘要参考实现 + 正反例。

## 6. 下一批依赖

- **B-02 → B-03 → B-04**：B-02 需要先确定"伤害修正的结算顺序与取整"（Strength 加成、Vulnerable ×1.5 的取整、格挡吸收、溢出保护），来源应为 `sts2/` 恢复工程 + `Content.cs:53` 的 `VulnerableMultiplierPercentPerStack`；未取到证据前不要猜。
- **N-02**：可基于切片的 `PlayerView` 设计公开快照，但 B-04 的覆盖表未完成前不应宣布公开事实充分。
- **J-01**：需要 B-04 + N-05 + A-05 同时就绪。
- **K-08**：与 B 无依赖，可并行（`IComputeBackend` 缝与重入拒绝已有实测）。
