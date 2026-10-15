# P-01 统一协议资产（第 1 步）交接

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-08、W-02，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

> 2026-10-01 规划复核：本文体量、64个defs和U项是原交付快照。能力/排序/摘要等不能因本次未读contract-artifacts就一概称为未定。后续schema已有70个defs的结构修订，但尚未独立运行正反例验证；规范化参考实现、机器不变量及跨端检查仍未完成，P-01保持开放。执行接管要求见[评审处置](review-followup-2026-10-01.md)。

> **2026-10-02 监督收尾（R-P01 已交付并复核通过）**
> - **上一条"规范化参考实现、机器不变量及跨端检查仍未完成"中的前两项现已完成**：规范化/摘要参考实现（`canonical` 7/7、`identity` 6/6，golden 由独立工具产出）与机器不变量（`invariants.md` 现有 I-01…I-90，含本轮新增 §2.6 的 I-69/I-70/I-71/I-73/I-76/I-81）都已就位；**正反例已可独立运行**：`python tools/contract-check/check_contracts.py` → **`TOTAL: 210 passed, 0 failed, 210 executed`（退出码 0）**，分组 `action-key 3/3 · canonical 7/7 · decimal 35/35 · fixtures 141/141 · identity 6/6 · schema-policy 18/18`；schema↔不变量差集 **0**（70/70 个 `$defs`）；入口打印 `coverage schema: present`。**跨端检查仍开放**——那是 N-01 的本职。
> - **更正我此前的两处读数**：① "69/69 已达标"是**不含 fixtures 套件的部分基线**（`69 = 18+7+35+6+3` 恰为五个非夹具套件之和）；fixtures 就位后的完整基线是 206 通过 + 1 崩溃（`neg-reserved-derived-field-in-wire` 的 `$set` 越界），现已修好为 210/210。② `coverage schema: ABSENT` 的观测早于 `coverage.schema.json` 落盘（00:06:29），已过期。
> - **按字面验收仍未闭合的两条**（`contract-artifacts.md:11` 点名的 C-01 资产是 `invariants.json`；`wire_layers.py:8` 亦写"规则 ID 与该文件一一对应"）：① **`invariants.json` 全仓不存在**（`Test-Path`=NO），现形式是 `invariants.md` 散文 + `wire_layers.py` 内的规则 ID；② **`I-87` 被重载**（实读：`wire_json.py:37/41/100` 的 L0 解析拒绝 与 `wire_layers.py:273/283` 的 L2 清单一致性用同一 ID），不满足"带稳定 ID"。
>   **建议裁定（择一，已在台账 §3 登记）**：(a) 认定"`invariants.md` + 代码内稳定 ID"即为清单，把 `contract-artifacts.md:11`、`wire_layers.py:8` 的措辞改准；(b) 派一个小任务由 `wire_layers.py` 的 ID 生成 `invariants.json` 并消解 `I-87` 重载（会牵动 fixtures 的 `rule_ids` 期望，须同批改）。**两种都不阻塞 A-01/N-01**。
> - **进展（监督者，2026-10-02）**：`I-87` 重载**已消解** —— L0 严格文本解析规则改用新 ID **I-91**（`wire_json.py:37/41/100` 三处 `ParseError`、夹具 `fixtures/cases/02-decimal-type-seed.json:96` 的 `rule_ids`、`invariants.md` 新增 I-91 行，同批修改）；`I-87` 仅保留 L2「动作/终端字段清单一致性」语义（`wire_layers.py:273/283` 与其 catalog 夹具未动）。复核：`python tools/contract-check/check_contracts.py` → **210 passed / 0 failed / 210 executed（退出码 0）**。**`invariants.json` 已由监督者生成（2026-10-02）**：生成器 `tools/contract-check/emit_invariants_json.py`（可复现，含 `--check` 同步核对）→ `docs/contracts/net/0.1/invariants.json`（13178 B，**49 条代码发码规则**，逐条带 `source`（文件:行）/`layers`/`documented_at`）；`--check` 通过。于是 `wire_layers.py:8` 原先"规则 ID 与该文件一一对应"的声明**现在成立**（此前是失效声明）。**新暴露的待裁定项**：资产内 `documented_but_not_emitted` 列出 **42 个"文档规定但代码不发码"的 ID**（I-02/03/04/06/10/12/14…I-73）——需裁定这些应由机器 schema/L1 承担，还是应补发码；这项已显式记录，不再隐藏。主套件复核：`check_contracts.py` → **210 passed / 0 failed / 210 executed（退出码 0）**。
> - 其他残余：`invariants.md` §2.1–§2.5 的 protocol.md 出处按旧 211 行版本（现 221 行，按行号检索会偏移约 +2～+10）；`I-69`/`I-71`/`I-73` 无独立反例；fixtures 的 `$set`/`$append` 路径失效时**抛异常而非记一条失败**（会把单用例错误升级为整个入口失败——正是我那次 69/69 假象的成因），建议给 `resolve_document` 加越界诊断。

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01；**第 1 步已交付**（机器 schema + 不变量清单）；规范化/摘要参考实现与正反例尚未交付 |
| 负责范围 | `docs/contracts/net/0.1/`（新建两文件）；未修改任何既有文件，未运行 dotnet，未做 git 写操作 |
| 依据 | `docs/design/net-the-spire/protocol.md`（唯一读取的设计文档，逐步拆读） |

## 1. 交付物与自检

| 文件 | 体量 | 内容 |
| --- | --- | --- |
| `docs/contracts/net/0.1/wire.schema.json` | 40462 字节 | JSON Schema draft 2020-12；64 个 `$defs`；根 `oneOf: Request / Response / PublicCatalog / PublicSnapshot`；每个对象都有 `properties`/`required`/`additionalProperties: false`（唯一例外是 `Capabilities`，其内部键由尚未读取的 `contract-artifacts.md#4` 定义，已在文件内 `$comment` 与不变量 U-01 标注） |
| `docs/contracts/net/0.1/invariants.md` | 27261 字节 | 已覆盖对象清单（`$defs` ↔ protocol.md 行号）、68 条跨结构不变量（I-01…I-68，含 protocol.md L79 自命名的 V-06.1…V-06.4 牌堆不变量）、协议未定项 U-01…U-12 |

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `python -c "import json;json.load(open('docs/contracts/net/0.1/wire.schema.json',encoding='utf-8'))"` | 0 | 可解析 |
| 临时脚本：`jsonschema` 4.25.1 `Draft202012Validator.check_schema` | 0 | 通过 meta-schema 校验（临时脚本已删除，未遗留文件） |
| 临时脚本：protocol.md §3 的 catalog/snapshot fixture + Request/Response 正反例 | — | 按预期通过/拒绝（脚本已删除） |

枚举值逐字取自 protocol.md：`value_type`、`number_format`、`visibility`、`content_state`、`state`、`status`、`outcome`、会话状态（active/settling/terminal/uncertain/stopped/faulted/closed）、`field_availability`、`execution_state`、`retry_mode`、14 个 error code、`request_status`、8 个 operation。

## 2. 已覆盖对象

Request/Response/Error 与 8 个 payload（describe、get_catalog、start、attach、observe、step、request_status、close）；DescribeResult/Capabilities/StartResult/ObserveResult/RequestStatusResult；Identity；PublicCatalog/Declarations 与 9 张声明表项（entity_kinds、content_types、fields、relation_types、pile_types、sequence_types、group_types、action_types、terminal_reasons）+ AppliesTo + PositionSchema；PublicSnapshot；Entity/Field/TerminalResult；Relation；Pile/CardPosition；Sequence/Group/GroupMember；LegalAction；全部枚举与标量 defs。

## 3. 未覆盖 / 协议未定（U-01…U-12 要点）

`Capabilities` 内部结构（operations、scenario_profiles、supported_structures、limits、execution_guarantees）与
`seed_action_replay`/`decision_pause`/`internal_snapshot_restore`/公开 restore·fork 的键位；`TerminalResult.facts` 容器形状；
`Relation.position` 类型；`StartPayload.seed` 类型；attach/close 成功结果键；`PositionSchema` 的 unit/origin 取值域与必填性；
`DescribeResult`/`StartResult` 外层键名（推断，已 `$comment` 标注）；命名空间 ID 语法、`semantic_version` 格式、摘要/规范化算法、
数组排序规则、`visibility_check_ids` 机制表。

以上一律**未发明字段**；schema 内留 `$comment: UNSPECIFIED-BY-PROTOCOL`，不变量清单里逐条登记。
`Field.value` 的字符串在缺少目录参与时无法判定是否为规范十进制（`"01"`/`"-0"` 作为 category/entity_ref 合法），
已登记为 I-21/I-22 与 U-10，未在 schema 里伪造语义。

## 4. 下一批依赖

1. **P-01 第 2 步**：规范化（canonical bytes）+ 摘要参考实现 + 正反例 fixtures + 可运行检查入口
   （未知键、重复 JSON 键、非规范 decimal、类型/身份错配、悬空引用、候选重复、牌堆越界/计数/定位冲突）。第 2 步应先补读
   `docs/design/net-the-spire/contract-artifacts.md` 以闭合 U-01/U-08/U-12。
2. **N-01**：C# 侧 DTO/目录/能力/严格 codec 必须以本 schema 与不变量为唯一事实来源，并比较两端规范字节与摘要；
   在此之前协议不得宣布冻结。
3. **A-01**：编码侧读取同一份规范；本 schema 可直接作为 Python contracts 的结构来源，避免第二套手写协议模型。
