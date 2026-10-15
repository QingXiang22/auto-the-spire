# P-01 附属审计：42 个"文档规定但 C-01 参考检查器不发码"规则 ID 的逐条归属

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-08、W-02，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 日期 | 2026-10-01 |
| 范围 | `docs/contracts/net/0.1/invariants.json` 的 `documented_but_not_emitted` 全部 **42** 个 ID |
| 输入资产 | `docs/contracts/net/0.1/invariants.json`（49 条发码规则 + 42 条待裁定）、`invariants.md`、`wire.schema.json`、`coverage.schema.json`；`tools/contract-check/`（`check_contracts.py`/`wire_layers.py`/`wire_json.py`/`jsonschema_min.py`/`schema_policy.py`/`wire_canonical.py`/`fixtures/**`） |
| 交叉泳道 | 只读检索 `net-the-spire/Net.TheSpire.Contracts/`（N-01 C# contracts）、`ai-the-spire/`（A-02/A-03 AI 侧） |
| 产出 | 本文 + [`invariants-disposition.json`](../../../contracts/net/0.1/invariants-disposition.json)（42 条一条不漏，机器可消费） |
| 未做 | **未改任何检查器行为**、未改 `invariants.json`、未运行任何 `dotnet` 命令、未 git add/commit/clean |

## 1. 结论

> **监督者补记（2026-10-02）——本报告四点的处置**
> - 第 1 点（**I-66 假阳性**）**已修**：`emit_invariants_json.py` 现同时扫描 `add("I-xx", "Ln"`（`check_contracts.py` 自有助手），`invariants.json` 重生成后 **`rule_count=50`**、`documented_but_not_emitted=41`；`invariants-disposition.json` 同批移除 I-66 条目（**42→41**），其"ID 集合等于 `documented_but_not_emitted`"的断言**重新成立**（schema 16 / covered 11 / missing 14）。复核：`check_contracts.py` 仍 **210/210、退出码 0**，生成器 `--check` 同步。
> - 第 2 点：**监督者先前的说法"这 42 条都不引用 SCHEMA-nn"不成立**（I-69/I-71/I-73 正文写着 SCHEMA-8/8/11-12，我只看命中行首行而误判）——本报告的说法采信。
> - 第 3 点（I-03 的强制者在 AI 侧 `GymManifest.assert_catalog`；wire/C-01 无 catalog_id↔内容摘要绑定、C# `CatalogDto.Digest()` 无调用方）**已登记**：待与 A-02 对齐口径，避免在 wire 侧重复实现。
> - 第 4 点（**L4 账本路径为死代码**：`ledger`/`requests`/`responses` 0 命中 → I-44/I-50/I-81/I-82 从未触发，故 I-42/I-49/I-53 的 `covered` 仅代码级证据；以及**跨泳道 L0 ID 不一致**：C# `WireParser.cs:130/131/184` 抛 I-87、`WireReader.cs:66` 抛 I-23，Python 已用 I-91/I-21）**已交当前 net 泳道任务（N-02）修正并复核**（.NET 泳道由其占用，监督者不在活动期改 C# 文件）。

| 归属 | 计数 | ID |
| --- | --- | --- |
| **A = `schema`**（L1 机器 schema / `schema_policy.py` 的 SCHEMA-nn 断言承担） | **17** | I-02、I-04、I-12、I-14、I-16、I-17、I-19、I-43、I-47、I-51、I-54、I-55、I-58、I-63、I-66、I-71、I-73 |
| **B = `covered`**（由另一个实际发码的规则/泳道覆盖） | **11** | I-10、I-30、I-33、I-38、I-42、I-46、I-49、I-53、I-67、I-69、I-03 |
| **C = `missing`**（无任何强制手段，真实缺口） | **14** | I-06、I-15、I-32、I-41、I-45、I-52、I-56、I-57、I-59、I-60、I-61、I-62、I-64、I-65 |

层分布：L1 **17**、L2 **4**、L3 **5**、L4 **16**。

**判定口径**（为可复核而显式写出）：

1. `schema`：该条的规范内容可由 `wire.schema.json` 的结构（`required`/`additionalProperties:false`/`enum`/`const`/`if-then-else`/`allOf`）表达，或由 `schema_policy.py` 的 SCHEMA-nn 断言固定；给 `文件:行`。
2. `covered`：该条的主规范内容由**另一个实际发码/实际抛错**的机制承担（同检查器的其他 `I-nn`，或其他泳道的等价强制）；给发码 ID + `case_id`（cross-lane 用例注明泳道）。
3. `missing`：主规范内容**在该仓库任一泳道都找不到强制手段**；必须写出搜索范围与具体反证（"某关键词 0 命中"）。
4. 一条规则含多个子句时，按 **invariants.md 标题所述的主内容**定归属；只被覆盖的次要子句写进备注，并集中列在 §4。

## 2. 42 行判定表

| id | 文档要求摘要（`invariants.md`） | 归属 | 证据（`文件:行` / `case_id`） | 备注 |
| --- | --- | --- | --- | --- |
| I-02 | `protocol_version` 完全相等（`"0.1"`），不接受前缀/区间 | schema | `wire.schema.json:219-221`（const）、`:820-821`、`:1234-1235`；`jsonschema_min.py:102-103`；`schema_policy.py:113-118` SCHEMA-2；case `neg-identity-version` | 跨泳道 C# 同断言（`CatalogDto.cs:19,478`、`SnapshotDto.cs:296`） |
| I-03 | 新增声明/改语义必须变更身份；旧目录只对旧 Gym 有效 | covered | `ai-the-spire/ai_the_spire/encoding/manifest.py:644-657`（`assert_catalog`：目录摘要不符即 `version_mismatch`）；case `combat/new-content-new-version`、`combat/new-content-vs-old-dictionary`（`ai-the-spire/tests/encoding_suite.py:359-389`）；`wire_canonical.py:324-343` | **wire 侧不发码**；C# `CatalogDto.Digest()`（`CatalogDto.cs:493`）无调用方。详见 §5 |
| I-04 | `PublicCatalog` 恰 4 键；`declarations` 恰九表、空表 `[]` | schema | `wire.schema.json:810-833`、`:738-808`；`schema_policy.py:219` SCHEMA-17、`:113-118` SCHEMA-2；case `pos-demo-catalog` | 表存在性由 required 强制，可空由无 minItems 表达 |
| I-06 | 声明 ID 带命名空间；同 owner 不重复字段名 | **missing** | 仅子条款覆盖：I-86（`wire_layers.py:145`，case `neg-duplicate-declaration-id`）、I-88（`wire_layers.py:462-463`，case `neg-entity-duplicate-field-id`） | 命名空间语法未冻结（`invariants.md:186` U-09），无词法检查 |
| I-10 | 只有明确不适用或 `optional` 才能省略 | covered | I-20 `wire_layers.py:478-485`，case `neg-required-entity-field-omitted`；`jsonschema_min.py:139-142`；`wire.schema.json:966-991` | 残留：省略不得编码为 known 零/`not_applicable` 无检查 |
| I-12 | `unknown` 与 `unavailable` 不可混用；由 `field_availability` 声明 | schema | `wire.schema.json:114-121`、`:123-129`、`:158-163`（三组枚举互斥）；`schema_policy.py:113-118`；case `neg-field-availability-unknown-state`；I-83 `wire_layers.py:947-953` + case `neg-capability-field-availability-unknown-field` | 状态语义真实性属提供方责任 |
| I-14 | 目录不含效果代码/隐藏资料；摘要确定且不用时间戳 | schema | `schema_policy.py:113-118` SCHEMA-2（声明对象拒绝未知键）；`wire_canonical.py:300-343`；case `identity:catalog-id`、`canonical:canonical-v1..v5`；`CatalogDto.cs:465-490`、`WireIdentity.cs:62-82` | catalog 摘要未像 ruleset/backend 那样显式拒 `timestamp`（`wire_canonical.py:346-365` 只作用于后两者） |
| I-15 | `description` 非语义、不得增加自然语言编码 | **missing** | 仅有"不进身份"的半机制：`wire_canonical.py:305-306` + case `identity:catalog-id` | 无任何检查阻止把 `description` 当效果文字特征；AI 侧 `manifest.py:8,12-13` 只是文档性声明 |
| I-16 | 快照必须含全部 14 键 | schema | `schema_policy.py:200` SCHEMA-14（"恰为"）；`wire.schema.json:1217-1231`；`jsonschema_min.py:139-142` | 跨泳道独立实现：`ai-the-spire/.../validate.py:51-67,237-306` |
| I-17 | decision 候选非空且 `terminal_result=null`；terminal 反之 | schema | `wire.schema.json:1304-1349`；cases `neg-decision-with-terminal-result`、`neg-decision-without-candidates`、`neg-terminal-without-result`、`neg-terminal-with-legal-actions` | AI 侧亦有自有检查（`validate.py:264,284-306`） |
| I-19 | 仅 `content_state=known` 带 `content_id` | schema | `wire.schema.json:1023-1048`、`:114-121`；cases `neg-content-state-known-without-id`、`neg-content-id-with-non-known-state` | AI/C# 侧同断言（`validate.py:544-560`、`SnapshotDto.cs:72-77`） |
| I-30 | V-06.4 不虚构卡牌；位置记录不增卡数 | covered | I-29 `wire_layers.py:636-643`，case `neg-pile-position-not-in-visible-members`；I-28 `wire_layers.py:621-626`，case `neg-pile-members-over-total`；AI 侧直接以 `related=I-30` 发码（`validate.py:894-902`） | 顺序完整性无检查（也不存在隐藏顺序通道） |
| I-32 | 牌堆成员以 `piles` 为唯一事实来源 | **missing** | 仅 I-79 `wire_layers.py:555-558`（禁止保留关系 `net:pile_member`），case `neg-reserved-derived-relation-in-wire`；AI 侧只预留映射（`validate.py:100`），无检测 | 目录也没有"成员型关系"标记可用于通用检测 |
| I-33 | 使用 `piles` 必须声明三个保留声明，wire 不得回填 | covered | I-79 `wire_layers.py:291-302`（目录侧）+ `:672-693`（wire 侧）；cases `neg-missing-reserved-relation`、`neg-missing-reserved-field`、`neg-reserved-derived-field-in-wire`、`neg-reserved-derived-relation-in-wire` | 同一 ID 横跨 L2/L3 |
| I-38 | 无序数组与 `legal_actions` 按规范键排序；`candidate_id` 不参与 | covered | I-74 `wire_layers.py:452/464/541/592/612/628/700/734/745/789`，cases `neg-entities-unsorted`、`neg-entity-fields-unsorted`；I-80 `wire_layers.py:780-781`，case `neg-legal-actions-unsorted`；`wire_canonical.py:202-209` + case `action-key:candidate-id-and-field-order-irrelevant` | 残句"排序不代替 ID 来源审计/数组序号不作位置"不可检 |
| I-41 | 单客户端串行请求、一个会话只有一个控制者 | **missing** | 无会话/并发建模；`session_busy` 仅枚举（`wire.schema.json:181-198`、`EnvelopeDto.cs:174`）。harness 已支持 `requests/responses/ledger`（`check_contracts.py:242-257`）但无 case 使用 | 建议见 §3 |
| I-42 | `request_id` 唯一非空、不得换 payload 重用 | covered | I-82 `wire_layers.py:1003-1019`；跨泳道同一指纹算法 `EnvelopeDto.cs:146` | **I-82 无用例**，账本路径未被触发（证据偏弱） |
| I-43 | `ok=true` 仅有 `result`，`false` 仅有 `error` | schema | `wire.schema.json:1707-1740`；cases `neg-response-ok-with-error`、`neg-response-not-ok-without-error`、`neg-response-not-ok-with-result` | `wire_layers.py:888` 的 docstring 提到 I-43 但该函数不发码 |
| I-45 | `catalog_id` 不存在则失败，不得返回近似目录 | **missing** | `wire_layers.py:862-884` 的 `_result_matches("get_catalog")` 只比键集合，不比对 `catalog_id`；C#/AI 两侧均无比对 | 建议见 §3 |
| I-46 | start 前置检查：不恢复旧局、不支持则拒绝、身份不匹配拒绝 | covered | I-11 `wire_layers.py:1025-1033`，case `neg-seed-when-unsupported`；I-13 `wire_layers.py:1034-1044`（无用例） | **残留缺口**：`start.payload.expected_identity` 与目录身份不匹配无检查（见 §4） |
| I-47 | `attach` 只控制现有会话，不得默默重开 | schema | `wire.schema.json:1953-2003`；`schema_policy.py:181-189` SCHEMA-12；cases `pos-attach-settling`、`neg-attach-settling-with-snapshot`、`neg-attach-closed-status` | 行为不可检；settling 显式性由分支强制 |
| I-49 | `step` 返回下一 snapshot；不确定则 `outcome_unknown` | covered | I-81 `wire_layers.py:878-879`（step 结果必须恰为快照 14 键）；`wire.schema.json:1425-1439` | **I-81 无用例**；stale/`outcome_unknown` 无强制 |
| I-51 | `close` 停止外部控制、不等同游戏终止 | schema | `wire.schema.json:2005-2019`（`const closed`）；`schema_policy.py:181-189`；cases `pos-close-result`、`neg-close-result-not-closed` | "等当前 step 完成"不可检 |
| I-52 | 去重范围：`start` 进程级、`step`/`close` 会话级、成败都缓存 | **missing** | 无去重/缓存检查；schema 只固定 `Limits.ledger_entries` 存在（`wire.schema.json:1867-1872`、`schema_policy.py:127` SCHEMA-4） | 建议见 §3 |
| I-53 | 先核 `request_id`+指纹再做版本检查；`stale_observation`；start 重发不启两局 | covered | I-82 `wire_layers.py:1003-1019`；I-50 `wire_layers.py:915-927` | 两者**均无用例**；版本/重发语义无强制 |
| I-54 | 首版无 `reset-in-place`/`rollback`/`load`/`fork` | schema | `wire.schema.json:1918-1923`（`const false`）；`schema_policy.py:130-140` SCHEMA-6；cases `neg-public-state-restore-true`、`neg-public-fork-true`；I-70 `wire_layers.py:986-989` | 无对应键者由 `additionalProperties:false` 一并拒绝 |
| I-55 | 会话七状态；`active`→快照 decision、`terminal`→终止快照 | schema | `wire.schema.json:146-156`、`:1304-1349`、`:1570-1598`、`:1975-2003`；I-48 `wire_layers.py:1050-1062` | 迁移合法性（合法路径、deadline）无检查，I-48 无用例 |
| I-56 | 锁内验证→pending→settling→执行一次→先更新后应答；并发返回 `session_busy` | **missing** | `wire_layers.py:930-934` 为空分支（注释 + `pass`）；两泳道均无会话锁/账本 | 建议见 §3 |
| I-57 | 失败归因 `not_started`/`unknown`，转 uncertain/faulted，禁止继续决策 | **missing** | `wire_layers.py:930-934`（唯一涉及 `execution_state` 的代码，空分支）；C# 只解析枚举（`EnvelopeDto.cs`） | 建议见 §3 |
| I-58 | `observe` 不得返回 settling 半成品；换 `observation_id`；`stable_snapshot` 真实性 | schema | `wire.schema.json:1570-1598`；case `neg-observe-settling-with-snapshot`；I-48 `wire_layers.py:1059-1062` | 残留：`stable_snapshot` 声明真实性、`observation_id` 更换无检查 |
| I-59 | 超时不证明未执行；先查原 `request_id`；`not_found` 不是未执行证明 | **missing** | 无超时/重发关联检查；`wire_json.py` 只有 `check_frame_bytes`→I-68 | 建议见 §3 |
| I-60 | 去重记录保留到关闭/退出，容量上限显式配置，超限拒绝新请求 | **missing** | 无保留/容量检查；`ledger_entries` 仅 capabilities required 键（`wire.schema.json:1867-1872`）；`wire_layers.py:1001-1019` 只有 ID 复用检查 | 建议见 §3 |
| I-61 | 进程丢失：模拟可丢弃并记录截断；真实须重新识别，未确定不恢复 | **missing** | 无进程丢失/截断记录检查；`session_not_found` 只是枚举值 | 建议见 §3 |
| I-62 | 取消不撤销已开始的 step；关闭后旧句柄不执行、ID 不复用；deadline→uncertain/faulted | **missing** | 关键词 `deadline` 在两泳道 **0 命中**；schema 只固定 `CloseResult`（`wire.schema.json:2005-2019`） | 建议见 §3 |
| I-63 | `error` 恰 7 键、不附隐藏状态；两个枚举 | schema | `wire.schema.json:1520-1553`；`schema_policy.py:113-118`；cases `neg-error-unknown-code`、`neg-error-execution-state-omitted` | `path`/`message` 内部结构仍开放（U-11） |
| I-64 | 错误码行为表（拒绝/修正输入/重新 observe/查询或停止） | **missing** | 无 `code`→`retry_mode`/`execution_state` 组合检查；`wire_layers.py:930-934` 为空分支；两泳道只解析枚举 | 建议见 §3（性价比最高） |
| I-65 | 传输断开没有 error 响应；保留 `request_id`；重连不得绕过 NoSL | **missing** | 两泳道均无 NDJSON/stdio/传输断开处理；`frame_bytes` 只作 limits 字段 | 建议见 §3 |
| I-66 | 顶层未知属性/未知类型/重复键/缺失必需值拒绝；可选仅在已声明时省略 | schema | `check_contracts.py:218,225`（L1 统一发码 I-66）；`wire.schema.json` 全文；`schema_policy.py:113-118`；44 个 case 期望 I-66 | **口径修正**：I-66 实际发码，被生成器漏扫（见 §6） |
| I-67 | exact identity；变更声明须变更身份；旧目录仍可用于旧 Gym | covered | I-01 `wire_layers.py:438-447`，cases `neg-snapshot-catalog-id-mismatch`、`neg-snapshot-ruleset-mismatch`；`wire.schema.json:219-221` | 与 I-03 同源（protocol.md L219）：identity 部分已强制，变更部分见 I-03 |
| I-69 | `ScenarioProfile` 六键；`id` 集合=合法 `start` 取值；声明闭合；`coverage_ref` 接机制表 | covered | `schema_policy.py:146-151` SCHEMA-8；I-84 `wire_layers.py:974-984` + case `neg-scenario-profile-declaration-unknown`；I-75 `wire_layers.py:347-364` + case `neg-visibility-check-not-in-coverage`；I-13（无用例） | 残留：`coverage_ref` 指向不存在覆盖表无检查（`invariants.md:203` 自认） |
| I-71 | `SupportedStructure` 三键；`structure` 七值；`declaration_ids` 闭合 | schema | `wire.schema.json:1786-1820`；`schema_policy.py:146-151`；case `neg-supported-structure-unknown`；I-84 `wire_layers.py:954-969` + case `neg-supported-structure-declaration-unknown` | "空数组冒充已审计"不可检 |
| I-73 | `attach` 结果包装与快照存在性；`settling` 省略 snapshot | schema | `wire.schema.json:1953-2003`；`schema_policy.py:181-189` SCHEMA-12；cases `pos-attach-settling`、`neg-attach-settling-with-snapshot`、`neg-attach-closed-status`；I-81 `wire_layers.py:872-875` + I-48 `:1050-1062` | "不含 operation"由拒绝未知键覆盖 |

## 3. C 类缺口清单与最小可行检查建议

以下 14 条当前**无任何强制手段**，建议按"rule_id / 层 / 路径 / 消息形态"补最小检查（只给建议，本次未实现）。

| rule_id | 层 | 路径（建议） | 消息形态（建议） | 实现要点 |
| --- | --- | --- | --- | --- |
| I-06 | L2 | `catalog.declarations.{table}[i].id`（及各类 `*_kind_ids`/`category_ids`） | `声明 ID 缺少命名空间前缀: {id!r}` | 前置阻塞：先冻结 U-09 的词法（如 `^[a-z][a-z0-9_]*:.+$`），否则会误杀 |
| I-15 | L2 | `catalog.declarations.*[].description` | `description 变化不得影响目录内容身份（description 非语义）` | 成对目录（仅 `description` 不同）要求 `catalog_digest` 相等；反向要求 `description` 不进入任何字典/排序键 |
| I-32 | L3 | `snapshot.relations` / `snapshot.groups` / `snapshot.sequences` | `牌堆成员事实不得在 {块} 中重复表达: {path}` | 目录当前没有"成员型关系"标记；最小版本可限定为"以 pile 实体为 source/target 且成员集合与 `visible_members` 完全重合的关系"，或先在目录加显式标记 |
| I-41 | L4 | `requests`（case 级请求列表，harness 已支持） | `会话同时存在两个未完成控制请求（违反串行/唯一控制者）: {session_id}` | 按 `request_id` 配对 `requests`/`responses`；有第二个控制请求而前一个仍 pending → `session_busy` |
| I-45 | L4 | `response[{rid}].result.catalog_id` | `返回目录与请求 catalog_id 不一致: {got!r} != {asked!r}` | 用同一 `request_id` 关联 `requests`/`responses`；`ok=false` 侧要求 `error.code ∈ {invalid_input, dangling_reference}`，不得返回近似目录 |
| I-52 | L4 | `ledger`（case 级账本） | `request_id 去重范围错误：{operation} 须为 {process\|session} 级` | 账本条目补 `scope`（新 fixture 词汇）；至少要求 `start` 的重复 `request_id` 不产生第二个 `session_id`，且拒绝响应也被缓存 |
| I-56 | L4 | `ledger`、`responses[..].result.snapshot.observation_id` | `并发控制请求未返回 session_busy` / `同 request_id 重发提交了第二次（observation_id 前进两次）` | 需 trace 词汇（`requests`+`responses` 已可用）；顺带可补 `observation_id` 会话内严格递增（I-40 目前只查 `instance_id`/`candidate_id` 重复，**未查递增**） |
| I-57 | L4 | `response[{rid}].error.execution_state` + 后续响应 | `无法证明无游戏效果的失败必须 execution_state=unknown，且之后不得再发布决策快照` | 按 `code`→允许的 `execution_state` 表 + 同一会话后续不得出现新 `step` 结果 |
| I-59 | L4 | `requests`/`ledger` | `超时后未查询原 request_id 就发新请求` / `not_found 不得作为未执行证明` | 账本条目需 `timed_out` 标记与 `request_status` 查询记录（新 fixture 词汇） |
| I-60 | L4 | `ledger` + `capabilities.limits.ledger_entries` | `去重记录超过 limits.ledger_entries 时必须拒绝新控制请求（capacity_exceeded），不得截断` | 纯计数比较，无需新词汇 |
| I-61 | L4 | `ledger`、`response[..].error.code` | `session_not_found 不得自动创建新会话继续旧动作` | 要求 `session_not_found`/`session_closed` 时 `retry_mode != correct_input`，且随后不得出现复用同一 `session_id` 的成功响应 |
| I-62 | L4 | `responses[..].result.session_id` / `session_status` | `关闭后不得再执行旧句柄，且 session_id 不得复用` | 同一 case 内 `session_id` 一经 `closed` 不得再出现在成功响应；deadline→uncertain/faulted 属运行配置，建议留在后端运行时 |
| I-64 | L4 | `response[{rid}].error.code` + `retry_mode` | `错误码 {code} 的 retry_mode 必须为 {expected}` | **性价比最高**：纯查表（`version_mismatch`/`unsupported_capability`→`stop`；`invalid_input`/`unknown_type`/`dangling_reference`→`correct_input`；`stale_observation`/`illegal_candidate`→`query_request`；`capacity_exceeded`/`session_not_found`/`session_closed`→不自动续局；`backend_fault`/`outcome_unknown` 依 `execution_state`），无需新 fixture 词汇 |
| I-65 | L0/L4 | `frame` / `raw_documents` | `传输断开不得以 error 响应表达；请求账本必须保留 request_id` | 在 `raw_documents` 提供截断帧，断言得到 L0 传输发现（当前 `check_frame_bytes` 只发 I-68 上限） |

## 4. B 类中的残留缺口（已归属但仍有未强制子句）

| id | 残留内容 | 现状 |
| --- | --- | --- |
| I-46 | `start.payload.expected_identity` 与目录身份不匹配必须拒绝 | `expected_identity` 在 `wire_layers.py` 里只出现在 `_PAYLOAD_ALLOWED/_REQUIRED`（`:60`/`:71`）白名单；无任何比对 |
| I-49 / I-53 | `stale_observation` 版本核对、`start` 原样重发不得启动两局、`outcome_unknown` 不虚构下一观察 | I-81/I-82/I-50 的账本分支**当前无任何 case 触发**，属"代码发码但未被运行验证" |
| I-42 | 同上（I-82 无用例） | 账本路径 `case.get("ledger")` 在 4 个 case 文件里 0 命中 |
| I-69 | `coverage_ref` 指向不存在的覆盖表 | `invariants.md:203` 自认的残余风险；无 case |
| I-10 | 省略不得编码为 known 零 / 显式 `not_applicable`；"三种责任" | 散文，不可机检 |
| I-12 | "暂未获知"与"后端无法提供"的语义真实性 | 枚举互斥已强制，语义真实性属提供方责任 |
| I-30 | "完整成员不等于顺序完整" | 设计上不存在隐藏顺序通道，无额外检查 |
| I-38 | "排序不能代替公开 ID 来源审计、不得把数组序号当游戏位置" | 属审计/编码侧要求 |
| I-55 | 状态迁移合法性（合法路径、deadline→uncertain/faulted） | 无状态机检查；I-48 亦无用例 |
| I-58 | `stable_snapshot` 声明真实性；真实游戏推进时必须换 `observation_id` | 无检查 |

## 5. 三个需要监督者注意的口径问题

1. **"这 42 条都不引用 SCHEMA-nn"这一前提不成立。** I-69（`invariants.md:145`）、I-71（`:147`）、I-73（`:148`）的条目正文分别写着 **SCHEMA-8**、**SCHEMA-8**、**SCHEMA-11/SCHEMA-12**；`§1` 第 1、5 项也把它们标为**已关闭**。所以"3 条已标已关闭"= I-69/I-71/I-73，它们的机器承担者本来就在文档里写明了（与本审计判定一致）。
2. **I-66 被生成器误报。** `emit_invariants_json.py:31-32` 只扫 `_f("I-xx","Ln"` 与 `ParseError("I-xx"`，而 L1 的 I-66 由 `check_contracts.py:218,225` 的 `add("I-66", …)` 发出、并有 44 个 case 期望它。因此 `documented_but_not_emitted` 里的 I-66 是**假阳性**；"代码发码 49 条"实际应为 **50 条**（含 I-66），`invariants.json` 的 `rule_count` 需要修。建议：生成器把 `add(` 也纳入扫描，或改扫"检查器输出过的 ID 常量集合"。
3. **I-03 的强制者不在 wire 侧。** wire/C-01 参考检查器完全没有"目录身份 = 内容摘要"的绑定；C# 侧有 `WireIdentity.CatalogDigest` 且 `CatalogDto.Digest()`（`CatalogDto.cs:493`）**定义后无调用方**；真正拦住"新目录配旧字典"的是 AI 侧 A-02：`GymManifest.assert_catalog`（`manifest.py:644-657`，case `combat/new-content-vs-old-dictionary`、`combat/new-content-new-version`）。若后续把 I-03 判为"wire 侧缺失"，应与 A-02 的这条用例对齐口径，避免重复实现。

## 6. 附带发现（不属于 42 条，供 N-01 泳道参考）

- **L0 规则 ID 跨泳道不一致**：`invariants.md:202` 记录 L0 严格文本解析已改用 **I-91**（Python：`wire_json.py:37/41/100`），但 C# 侧 `WireParser.cs:130`、`:131`、`:184` 仍对 NaN/Infinity/浮点字面量抛 **I-87**；`WireReader.cs:66` 对非规范 decimal 抛 **I-23**（Python 侧对应 I-21/I-22）。跨端比对 `rule_ids` 时会出现同一违规两个 ID。
- **`invariants.json` 与生成器无跨泳道视角**：`rule_count`/`documented_but_not_emitted` 只描述 `tools/contract-check` 自身，不含 C#/AI 侧发码，容易把"其他泳道已强制"的条误判为缺口（I-03 即例）。

## 7. 未确认项

1. I-42/I-49/I-53 的"covered"证据只到代码层（I-82/I-81/I-50 无用例），**未经运行验证**；建议后续补 `ledger` 用例。
2. I-46 的 I-13 分支无 fixtures 用例（只有代码），`scenario_profile` 集合外拒绝未验证。
3. I-69/I-71/I-73 的跨结构反例（`coverage_ref` 悬空、`structure` 与实际快照结构不符）仍缺（与 `invariants.md:203` 一致）。
4. `invariants.md` §2.1–§2.5 的 `protocol.md` 行号按旧 211 行版本，与新条目（221 行版本）不一致；本审计只引用 `invariants.md` 的标题与出处，**未逐条重核 `protocol.md` 行号**。
5. 协议仍 IN_REVIEW 未冻结（`protocol.md` 头部/`invariants.md:9`）；协议变更会使本映射失效。
6. 按边界要求**未运行任何 `dotnet` 命令**，因此 C# 侧（N-01）的检查**未执行验证**，只做了源码级只读检索；`ai-the-spire` 的测试套件本次也未运行。
7. 本审计的"missing"结论限定为"该仓库现有 Python/C#/AI 三处源码 + fixtures 中找不到强制手段"（搜索范围见 JSON 的 `evidence`），不排除外部工具或运行时（真实后端）另有保障。

## 8. 行为未变复核

审计前后各执行一次（均为仓库根目录）：

```
python tools/contract-check/check_contracts.py
```

输出（两次一致）：

```
wire schema: docs\contracts\net\0.1\wire.schema.json
coverage schema: present
  action-key       3/3 passed
  canonical        7/7 passed
  decimal          35/35 passed
  fixtures         141/141 passed
  identity         6/6 passed
  schema-policy    18/18 passed
TOTAL: 210 passed, 0 failed, 210 executed
```

**退出码 0**。

`python tools/contract-check/emit_invariants_json.py --check` 未运行（会与"不重写 `invariants.json`"的边界相关），本次只读该产物。

## 9. 声明

- 本审计**未做任何代码改动**：未新增/删除检查、未改 `check_contracts.py` 的用例或期望、未改 `wire_layers.py`/`wire_json.py`/`schema_policy.py`/`jsonschema_min.py`。
- **未重写** `invariants.json`（生成器产物）；对其 `rule_count` / `documented_but_not_emitted` 的修改建议只写在 §5。
- 本审计只新增两个文件：`docs/contracts/net/0.1/invariants-disposition.json` 与本文；`core-the-spire/`、`mod-the-spire/`、`net-the-spire/`、`ai-the-spire/`、`sts2/`、`reference/`、`docs/design/` 全部只读。
- 未运行 `dotnet`、未 `git add/commit/clean`。
- 判定过程由脚本机械生成 JSON 并做断言（42 条齐全、ID 集合等于 `invariants.json` 的 `documented_but_not_emitted`、`disposition ∈ {schema,covered,missing}`、`layer ∈ {L1..L4}`、`schema` 必带 `SCHEMA-nn`/`wire.schema.json` 证据、`missing` 必带"搜索范围"），脚本位于仓库外临时目录，不随交付保留。
