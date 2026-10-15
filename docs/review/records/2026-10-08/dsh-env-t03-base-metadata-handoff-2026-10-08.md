---
task_id: ENV-T03
task_name: base 正式工厂公开出口所需不可变游戏语义元数据
dispatch_id: ENV-T03-base-public-metadata-20261008
session_id: session-71eea2fb-9dea-477e-89b3-fca5adaa6a44
dispatch_record: .audit/dsh-env-t03-base-metadata-2026-10-08/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-08T17:14:22+08:00
---

# ENV-T03 base 公开语义元数据交接 — 2026-10-08

本包为 ENV-T03 的 A 线依赖子包：正式 base 工厂现在随构建给出**只读、不含每局 seed**的不可变游戏语义元数据
（`BaseSpireRuntimeFactory.PublicSemantics`），把通用 `RuleSet` 声明缺的、不能凭名字推导的四类含义声明一次，
并把它折进公开构建身份。C 线据此把 `DerivedPublicProfile` 的派生结果翻成真实公开目录/身份；
本包**不**接通 Host、**不**关闭 T03、**不**实现 T05。

归属：ENV-T03「公开Host接唯一工厂；从规则声明派生公开出口」的 base 侧依赖子包（A）。
接手入口：主清单 ENV-T03 条目；本文件「C 接入步骤」；[base README 公开语义元数据节](../../../../mod-the-spire/base-spire/README.md#公开语义元数据env-t03-a-线子包2026-10-08)。
交付范围：负责会话 `session-71eea2fb-9dea-477e-89b3-fca5adaa6a44`；只写 `mod-the-spire/base-spire/**`、
本交接与 `.audit/dsh-env-t03-base-metadata-2026-10-08/**`。`core-the-spire`、`net-the-spire`、`tools`、
`reference`、`ai-the-spire`、`solution` 只读；主清单未编辑。
本报告记录 2026-10-08 的交付；当前排期、状态与认领以主清单为准。

## 接手后先做什么

1. 先确认本包**已交付部分**，不要重做：元数据类型与构造期闭合在
   `BaseSpire/SinglePlayer/BasePublicSemantics.cs`，工厂出口是 `PublicSemantics`，身份接线在
   `BaseSpireBuildDescription.SemanticsDigest` / `PublicBuildId`，检查在
   `BaseSpire.Slice.Tests/PublicSemanticsChecks.cs`（8 条，已随 Slice 332/332 通过）。
2. 下一步（负责人/下一轮）：先按「差异、阻塞与未验证」核对根构建失败是否只属于 B/C 在制改动；
   若已消解，重跑串行门槛确认 base 与解决方案共同可构建。C 侧按「C 接入步骤」翻译元数据，
   其 `ready_for_review` 之前本包保持待验证。
3. 完成条件（保留原合同，仅列本包相关）：正式工厂的只读内容/集合/位置/终局含义与实际规则、加载内容闭合，
   同配置稳定、无 seed、内容/语义变更影响身份；T03 父项仍要求同工厂正常 describe/catalog/start/observe/step
   公开轨迹（C 线）与「只改规则包新增公开字段/Action」证据，**不由本包关闭**。

## 已交付与接口变化

新增能力（精确 API，C 线直接可用；base 不引用 net）：

```csharp
var factory  = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad());
BasePublicSemantics s = factory.PublicSemantics;        // 只读、无 seed、同配置稳定
string semanticDigest = s.Digest;                       // = factory.Describe().SemanticsDigest
string buildIdentity  = factory.Describe().PublicBuildId; // 已折入 semantics 摘要
string[] lines        = s.CanonicalText().Split('\n');  // 逐行可核对的证据形态
```

| 成员 | 形状 | 内容（本构建实测） |
| --- | --- | --- |
| `Content` | `IReadOnlyDictionary<string, BasePublicContentCatalog>`；`BasePublicContentCatalog(string FieldName, IReadOnlyList<string> ContentIds)` | `base-spire:Card=DefinitionId`（68）、`base-spire:Power=DefinitionId`（29）、`base-spire:Monster=DefinitionId`（35）、`base-spire:Relic=DefinitionId`（1）。ids 来自**实际加载内容**并按 id 稳定排序 |
| `Collections` | `IReadOnlyDictionary<string, BasePublicCollectionSemantics>`；`Use ∈ {Pile, Group, NotPublished}`；`Pile(...)` 给 `PileKindEngineTypeId`/`Position`，`Group(...)` 给 `OwnerKindEngineTypeId`，`NotPublished(ruleId, reason)` | 5 个牌堆（Hand/DrawPile/DiscardPile/ExhaustPile/InPlay，各有专属 pile_ref 实体种类）；抽/弃牌堆给位置坐标 `card/top/0`；`LongTermDeck`、`Relics` 为 `Group(owner=base-spire:Run)`；`PlayedThisTurn`/`Monsters`/`Known`/`RunStates`/`EncounterPool`/`EncounterQueue` 具名不发布 + 规则号 + 理由 |
| `KnownPositions` | `IReadOnlyDictionary<string, BasePublicKnownPositionRole>`；`(FactTypeId, CardFieldName, PileFieldName, PositionFieldName, PileCollectionByToken)` | `base-spire:KnownCard` → `Card`/`Pile`/`Position`，词表 `draw → base-spire:DrawPile`（值已是引擎集合限定名） |
| `TerminalOutcomes` | `IReadOnlyDictionary<string, BasePublicTerminalOutcome>`；`WireName()` → `win`/`loss`/`other` | `base-spire:Victory=Win`、`base-spire:Defeat=Loss` |
| `Digest` / `CanonicalText()` | `string` | 语义摘要（SHA-256 前 16 位十六进制，与工厂同一算法）；规范化文本按类型/集合/终局键排序 |

修改文件：

- 新增 `mod-the-spire/base-spire/BaseSpire/SinglePlayer/BasePublicSemantics.cs`：四类含义的类型 +
  `BasePublicSemanticsBuilder.Build(RuleSet, projectionId, participantRole, BasePublicContentSource)`。
- `SinglePlayer/BaseSpireRuntimeFactory.cs`：构造时构元数据（`_publicSemantics`）、公开 `PublicSemantics`、
  `PublicBuildId` 多一个 `semantics=` 输入维度，`Digest` 复用同一实现。
- `SinglePlayer/BaseSpireBuildDescription.cs`：新增位置参数 `string SemanticsDigest`（在 `StreamLayoutDigest`
  之后、`PublicBuildId` 之前），`Summary` 追加 `semantics=<digest>`。
- 新增 `BaseSpire.Slice.Tests/PublicSemanticsChecks.cs`（8 条检查）并在 `SliceChecks.RunAll` 注册。
- `mod-the-spire/base-spire/README.md`：新增「公开语义元数据（ENV-T03 A 线子包）」参考节，并修正
  `PublicBuildId` 组成描述。

跨线变化（请 C/B 注意）：

- **`Describe().PublicBuildId` 的值变了**（新增 `semantics=` 维度）。这是公开身份的定义变化，不是回归：
  同一配置内部仍稳定，但任何**已记录**的旧 `PublicBuildId` 字符串不再复用。B 的工具把它当不透明身份记录，
  不需要改代码；C 若把它当 `StaticBuildIdentity` 传入派生层，得到的 `net:ruleset:*` 会随之变化。
- `BaseSpireBuildDescription` 多了一个位置参数。只读该记录或构造它的代码需要同步；全仓库只有
  `BaseSpireRuntimeFactory` 构造它（已改）。
- 未新增/未修改 core、net、tools 任何文件；未改主清单。

## 关键设计决定（供复核）

1. **只补四类**：普通字段、枚举、引用、动作/参数一律不登记（仍由 `RuleSet` 声明与所选投影派生）；
   元数据里没有逐项字段表、没有第二份 RunState、没有任何可变游戏状态。
2. **构造期闭合，不靠约定**：`Build` 对真实 `RuleSet` + 投影 + 实际内容逐条校验——集合必须**全有**用途、
   终局必须**全有**解释、内容字段必须是真实 string 字段、pile_ref/owner/已知位置事实种类必须在投影里、
   已知位置三个角色字段必须存在且形状相符、标记只能指向发布为 pile 的集合。缺一项即显式失败
   （错误信息点名 `base-spire:*` 与原因），不会静默省略公开事实。
3. **集合发布/未发布按实际可见性与用途解释**：抽牌堆在引擎声明里 `IdentityVisible=false`、
   `OrderVisible=false`，但 `SizeVisible=true`，因此仍发布为牌堆（大小公开），绝对位置只由
   `KnownCard` 事实公开；具名不发布的六条都写明"公开事实已由其它块表达"或"成员种类不在投影里"，
   而不是"没实现所以省略"。没有使用 `Sequence`（派生层对它显式抛错：owner_ref 是写死的公开引用）。
4. **已知位置只给角色**：值（当前只有 `draw`）与失效仍由包程序维护；metadata 只给字段角色与标记词表。
5. **身份**：`Digest` 是元数据自身的摘要，进入 `PublicBuildId`；`BaseSpireBuildDescription.SemanticsDigest`
   直接暴露它，`Summary` 也带上，便于证据逐条核对。

## 验证与证据

工作目录一律 `F:\Scylier-Project\active\auto-the-spire`；命令均由本次执行，输出存
`.audit/dsh-env-t03-base-metadata-2026-10-08/artifacts/`。

| 检查 | 实际命令 | 返回码/结果 | 证据与证明范围 |
| --- | --- | --- | --- |
| base 单项目构建 | `dotnet build mod-the-spire/base-spire/BaseSpire/BaseSpire.csproj -v q` | **0 错误**（1 个既有 `SpirePowerSource.cs:1241` CS8604 警告，与本次改动无关） | base 自身可编译 |
| 解决方案串行构建 | `dotnet build AutoTheSpire.sln -v q` | **退出码 1**，2 个错误，**均不在 base**：`tools/differential/RngSourceCompat.cs(159,20) CS0029`、`net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs(127,53) CS7036` | 失败来自 B/C 在制改动；base 未出现在任何错误行。见 `artifacts/01-root-build.txt` |
| Engine 检查 | `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | **95/95 passed**，退出码 0 | `artifacts/02-engine-tests.txt` |
| Slice 检查（含本包 8 条） | `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | **332/332 passed**（原 324 + 新 8），退出码 0 | `artifacts/03-slice-tests.txt` |
| 元数据摘要与身份（实测值） | 同上输出 | `semantics=0813ece35cc41a8b`，`build=5c5cc8d5a1cda363`，`ruleset=04078d3be6c4b66c` | 该轮构建的实际值；身份值随改动变化属预期 |
| 内容目录 vs 实际加载（实测） | 同上输出 | 卡 68 / 能力 29 / 敌人 35 / 遗物 1；源里声明但未支持、**不进目录**的卡 28 张（`aggression`…`whirlwind`） | 目录=实际加载内容，不是源声明清单 |

本包 8 条新检查与它们证明的范围（`PublicSemanticsChecks.cs`）：

1. 闭合：集合键集 = `RuleSet.Collections` 全量、终局键集 = `RuleSet.TerminalResults` 全量、不发布均带规则号与理由、
   内容字段真实存在、投影/角色不匹配或指到不存在的投影显式失败。
2. 目录 = 实际加载内容：与 `SinglePlayerContentSource.Shared` 的 `CardIds/PowerIds/EnemyIds` 及遗物库逐项相等，
   条数与 `Describe()` 一致；判别力：被记为未支持的源声明卡 id **不在**目录里。
3. 集合用途：五个牌区 → 真实声明的 pile_ref 种类；位置只给抽/弃牌堆；抽牌堆 `SizeVisible=true` /
   `IdentityVisible=false` / `OrderVisible=false` 与"仍发布为牌堆"并存的理由；两个归组 owner=Run；
   六条具名不发布理由非空且说明实际用途。
4. 已知位置角色：三个字段真实存在且形状相符（卡牌引用 → 真实 `base-spire:Card`）、词表 = 包程序实际写的标记、
   标记指向已发布且有位置坐标的牌堆、事实类型在投影里。
5. 终局解释：Victory=Win / Defeat=Loss，`WireName()` 只产出协议词表；未定义含义落 `other`。
6. 真实会话唯一解析：启动一次后，每个 pile_ref 种类恰好 1 个实例、每个 group owner 种类恰好 1 个实例
   （Run 单例），长期牌组成员种类是卡牌。
7. 稳定/无 seed/内容敏感：两工厂的 `Digest`、`CanonicalText`、`PublicBuildId` 相同；`Describe().SemanticsDigest`
   等于 `PublicSemantics.Digest`；`Summary` 带 `semantics=`；建两个不同 seed 的会话后摘要与文本不变、
   文本不含 `seed` 与里程碑 seed 值；判别力：同一规则集下换一个内容 id → 摘要必变、规范化文本能指出差在哪条。
8. 只读防篡改：四个字典的新增/替换/删除、内容 ID 列表追加、标记词表改写都抛 `NotSupportedException`；
   两个工厂实例各自独立。

本次**未运行**：net 检查（`Net.TheSpire.Tests` 当前无法编译，见上）、`tools/differential` 任何入口、
任何双侧对拍、Host 的 describe/catalog/start/observe/step 公开轨迹、完整整局。原因是它们不在本包范围
且当前被 B/C 在制改动阻塞（并行构建会写同一 `obj`/`bin`）。

执行者自报与负责人实测的区分：上表全部为**本会话实跑**；主清单的验收结论仍由负责人作。

## 差异、阻塞与未验证

首个差异（不在本线，记录范围与首错）：

- 预期：`dotnet build AutoTheSpire.sln` 串行通过（本轮 A 持有第一构建位）。
- 实际：退出码 1，2 个错误，均来自其它模块在制改动：
  1. `tools/differential/RngSourceCompat.cs(159,20)` CS0029：`(string type, uint)` 不能隐式转成 `(string Type, string Seed)`
     —— B 线在制（`SeededStreams` 形状变化）；
  2. `net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs(127,53)` CS7036：`StartRequest` 缺 `seed` 参数
     —— C 线在制（同工厂正常 Host / 0.2 字符串 seed）。
- 本线处置：未修他人代码、未把失败写成通过；完成本线必要检查（base 单项目构建、Engine 95/95、Slice 332/332）。
- 最小复现：上表第 2 行命令；完整输出 `artifacts/01-root-build.txt`。

未验证/需要别人接续的：

- **公开轨迹未接通**：没有 describe/catalog/start/observe/step 的同工厂轨迹证据，本包**不能**宣称
  「T03 公开出口已可用」；T03 保持待 C 验证。
- **语义变更 → 身份的判别**：内容变更敏感性已实测；"语义本身变化"（集合用途/终局解释）无法在不改代码的
  情况下构造输入，只能保证它是 `PublicBuildId` 的单一输入项（源码单点 + 检查 7 的接线断言）。若复核要求更强
  证据，需要负责人决定是否接受"代码级单点 + 内容敏感性"这一层。
- **已知位置无运行时事实**：当前 dev 内容不产生 `KnownCard` 事实（`top_from_discard` /
  `top_attack_from_discard` 类效果未进入正式卡池），因此角色只有 schema 层闭合证据，没有"事实真的按词表写下"
  的运行时证据（受控切片 `SliceChecks` 已另行覆盖包程序写法）。
- **`PublicBuildId` 值变化**：本包改了公开身份定义；任何记录了旧值的证据/对拍基线需由负责人判断是否重取。
- 未验证范围不含 core/net/tools 的行为结论。

## C 接入步骤（跨包翻译，第一版）

C 线在 net 侧构造 `PublicBuildMetadata`，把 base 的形状翻译过去即可（base 不引用 net，方向不能反）：

1. 取 `var build = factory.Describe(); var semantics = factory.PublicSemantics;`
2. `StaticBuildIdentity` = `build.PublicBuildId`（已含 `semantics=` 维度，无需自行拼接）。
3. `Content`：对每个 `(typeId, catalog)` → `new PublicContentRule(catalog.FieldName, catalog.ContentIds)`，
   键用 `typeId`（引擎限定名，如 `base-spire:Card`）。
4. `Collections`：按 `Use` 映射到 net 的工厂方法——
   `Pile` → `PublicCollectionRule.Pile(sem.PileKindEngineTypeId!, sem.Position is { } p ? new PublicPositionSchema(p.Scope, p.Origin, p.First, p.Max) : null)`；
   `Group` → `PublicCollectionRule.Group(sem.OwnerKindEngineTypeId!)`；
   `NotPublished` → `PublicCollectionRule.NotPublished(sem.SkipRuleId!, sem.SkipReason!)`。
5. `TerminalOutcomes`：`semantics.TerminalOutcomes.ToDictionary(pair => pair.Key, pair => pair.Value.WireName())`
   （net 只接受 `win`/`loss`/`other`）。
6. `KnownPositions`：`new PublicKnownPositionRole(role.FactTypeId, role.CardFieldName, role.PileFieldName,
   role.PositionFieldName, role.PileCollectionByToken)`（值是引擎集合限定名，net 会翻成公开 pile_type）。
7. 然后 `DerivedPublicProfile.Build(rules, build.ProjectionId /* base-spire:PlayerView */,
   SliceManifest.PlayerRole 对应的角色字符串 `player`, metadata, protocolVersion)`。
   注意：`PublicSemantics.ProjectionId` / `ParticipantRole` 就在元数据里，不要另写常量。
8. C 验证时请核对：四个内容目录条数（68/29/35/1）、六条具名不发布理由、`draw` 词表、`Victory=win`/`Defeat=loss`、
   以及 `net:ruleset:*` 随 `build.PublicBuildId` 变化。这些是**本包实际输出**，可直接与上面「验证与证据」对照。

## 维护位置

- 状态与认领：主清单 ENV-T03 条目（负责人维护，本包未编辑）。
- 方法与 API：`mod-the-spire/base-spire/README.md#公开语义元数据env-t03-a-线子包2026-10-08`。
- 派单记录：`.audit/dsh-env-t03-base-metadata-2026-10-08/task-record.json`。
- 本次证据与原始输出：`.audit/dsh-env-t03-base-metadata-2026-10-08/artifacts/`（`01-root-build.txt`、
  `02-engine-tests.txt`、`03-slice-tests.txt` 及各自 exit 文件）。
- 后续证据：负责人并入主清单后转 `docs/review/records/2026-10-08/`。

## 17:39负责人接收说明

实际会话 session-71eea2fb-9dea-477e-89b3-fca5adaa6a44 的完整结果取齐至 seq 581、hasMore=false，结束原因 completed。此说明由负责人追加，原执行者 delivery_status 保留；转入 records 只保存已接收的日期证据，不表示验收。实际核验与接续见[当日负责人复核](dsh-supervisor-review-2026-10-08.md#1739复核与剩余目标接续)。
原 dispatch_id 错填 request_id `dcff9bde-2611-46e1-bed5-29c2e6865169`；按原派单记录的 task_id 更正，task/session 身份未变。
元数据与身份 API 已交付，执行者 Engine95/Slice332 本地证据保留。PlayedThisTurn 的 NotPublished 与实际公开声明冲突，LongTermDeck/Relics 的 group 也不保留公开顺序，本包不能整体验收。接续由 A 修正元数据、C 支持运行时 owner 的 sequence。
