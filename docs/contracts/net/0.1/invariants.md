# net-the-spire 协议 v0.1 跨结构不变量

> 2026-10-01 评审更新：以下是原 P-01 第1步的人工清单与历史 U 项，不能当作现行机器 schema 的完成证据。protocol/contract-artifacts 后续已明确能力层级、成功包装、终局facts、位置与seed类型；schema已同步结构，但尚未独立复验。U-01/U-02/U-03/U-04/U-05/U-06/U-07不再一概视为协议未定；U-08/U-12需按已有上位设计实现，U-10始终需要目录参与的语义检查。规范化、机器不变量、独立正反例及跨端验证仍归[R-P01](../../../review/records/2026-10-01/review-followup-2026-10-01.md)。原行号/“本次未读取”的文字保留原交付时点。
>
> 2026-10-01 收尾更新（R-P01 最小剩余单元）：已读上位设计 `contract-artifacts.md`，为 `ScenarioProfile`、`SupportedStructure`、`ExecutionGuarantees`、`SessionSeed`、`AttachResult`、`CloseResult`（以及同批 0 提及的 `Limits`）补跨结构条目，见 §2.6；U-01/U-02/U-03/U-05/U-06 与 §0 的兜底说明已按明文来源改写。**§2.6 与 §3 新增/改写的条目使用 protocol.md 当前 221 行版本的行号；§2.1–§2.5 旧条目仍按 211 行版本，行号已系统性漂移（例：§4 现为 L168–L189，旧条目写 L164–L179），未逐条重编号，见 §4。**

| 项目 | 内容 |
| --- | --- |
| 依据 | 仅 `docs/design/net-the-spire/protocol.md`（v0.1 修订 2，2026-10-01，IN_REVIEW，尚未冻结，无协议实现） |
| 配套机器 schema | [`wire.schema.json`](./wire.schema.json)（JSON Schema draft 2020-12） |
| 范围 | 只收录 protocol.md 已写明的规则；单对象内部结构（键/类型/枚举/条件键）在 schema，跨结构规则（引用完整性、牌堆计数、目录闭合、去重、排序、会话/请求状态机）在本文件 |
| 出处写法 | 行号对应 protocol.md 当前 211 行版本；小节标题同时给出，便于文档改行后定位 |

## 0. 已覆盖对象清单（schema `$defs`）

顶层 wire 消息（schema 根 `oneOf`）：`Request`、`Response`、`PublicCatalog`、`PublicSnapshot`。

| `$defs` | protocol.md 出处 |
| --- | --- |
| `Request`、`DescribePayload`、`GetCatalogPayload`、`StartPayload`、`AttachPayload`、`ObservePayload`、`StepPayload`、`RequestStatusPayload`、`ClosePayload` | §4 控制接口 L164、L166–L175 |
| `Response`、`Error` | §4 L164；§6 L195–L205 |
| `DescribeResult`、`Capabilities` | §1 L50、L52、L54；§4 L168 |
| `ScenarioProfile`、`SupportedStructure`、`ExecutionGuarantees`、`Limits` | §1 L50、L52；`contract-artifacts.md#4` |
| `SessionSeed`（`StartPayload.seed`） | §4 L185 |
| `AttachResult`、`CloseResult` | §4 L183 |
| `Identity` | §1 L19–L28 |
| `PublicCatalog`、`Declarations` | §1 L28、L30；§3 L106–L134 |
| `EntityKindDeclaration`、`ContentTypeDeclaration`、`FieldDeclaration`、`AppliesTo`、`RelationTypeDeclaration`、`PositionSchema`、`PileTypeDeclaration`、`SequenceTypeDeclaration`、`GroupTypeDeclaration`、`ActionTypeDeclaration`、`TerminalReasonDeclaration` | §1 声明表 L36–L46 |
| `PublicSnapshot` | §2 L56–L58；§3 L136–L160 |
| `Entity`、`Field`、`TerminalResult` | §2 L58–L73 |
| `Relation` | §2 L75 |
| `Pile`、`CardPosition` | §2 L77–L88 |
| `Sequence`、`Group`、`GroupMember` | §2 L88–L90 |
| `LegalAction` | §2 L92–L94 |
| `StartResult`、`ObserveResult`、`RequestStatusResult` | §4 L170、L172、L174 |
| 枚举 `Operation`、`ValueType`、`NumberFormat`、`Visibility`、`ContentState`、`FieldState`、`SnapshotStatus`、`Outcome`、`SessionStatus`、`AvailabilityState`、`ExecutionState`、`RetryMode`、`ErrorCode`、`RequestStatus` | §1 L19、L30、L40、L42、L46、L50；§2 L58、L62、L64；§4 L174；§5 L183；§6 L195、L197–L205 |
| 标量 `NonEmptyString`、`DecimalString`、`SafeInteger`、`PositiveInteger`、`NullableNonEmptyString`、`IdList`、`FieldIdList`、`EntityRefList` | §1 L25、L26、L42；§2 L68、L73、L82、L90 |

覆盖口径说明：schema 覆盖 protocol.md 出现的**每一个键名、类型与枚举值**；收尾复核确认**所有对象定义（含 `Capabilities`）均为 `additionalProperties: false`**（机器检查 SCHEMA-2，通过）；原文"`Capabilities` 是唯一 `additionalProperties: true` 的对象（见 §3-未定 U-01）"已随 U-01 关闭而失效。其余对象均为 `additionalProperties: false`，对应 §6 L207 的"顶层未知属性、未知类型、重复键和缺失必需值拒绝"。

## 1. 未覆盖/未定清单（概览）

以下内容 protocol.md 未逐字给出形状或取值，**未发明字段**，只在 schema 中留 `$comment: UNSPECIFIED-BY-PROTOCOL` 或许可任意值。收尾复核后逐项标注状态：**已关闭**的按明文来源落地条目，**仍开放**的保留待定。

1. ~~`Capabilities` 内部结构~~ **已关闭（收尾）**：精确键、类型与枚举已由 `contract-artifacts.md#4` 固定并落入 schema（`Capabilities` 八键全必填且拒绝其他键；`Limits`/`ExecutionGuarantees`/`ScenarioProfile`/`SupportedStructure` 逐键固定）。见 §2.6 的 I-69、I-70、I-71 与机器检查 SCHEMA-3～SCHEMA-8。
2. ~~`TerminalResult.facts` 的容器形状~~ **已关闭（收尾）**：protocol.md L62 已固定为 `Field` 数组，与实体字段同构，按 `field_id` 排序且无重复；机器检查 SCHEMA-13，语义检查 I-18/I-90。
3. `Relation.position` 的具体类型（旧 L75，现 L87 附近）：**仍开放**。协议只说 position 语义由该 `relation_type` 的 `position_schema` 声明，未给 wire 类型；schema 侧仍为可空整数（推断）。
4. ~~`StartPayload.seed` 的类型与编码~~ **已关闭（收尾）**：protocol.md L185 固定为规范无符号 64 位十进制字符串（`0..18446744073709551615`），schema 用 `$defs/SessionSeed`（`^(0|[1-9][0-9]{0,19})$`），数值上界由跨结构检查承担（I-76；上界用例见 §2.6）。
5. ~~`attach`/`close` 的成功结果键~~ **已关闭（收尾）**：protocol.md L183 固定 `attach = {session_id, session_status, snapshot?}`、`close = {session_id, session_status:"closed"}`；schema 的 `$defs/UnspecifiedResult` 兜底**已删除**，`Response.result` 仅由具名分支组成（SCHEMA-11），见 I-73、I-81。
6. `PositionSchema` 的 `unit`/`origin` 取值域、`min`/`max` 数值域（L41 只列键名）：**仍开放**（schema 按全部 required 处理，属推断；仅 `min <= max` 有机器检查 I-77）。
7. 命名空间 ID 语法、`semantic_version` 格式、摘要/规范化算法、数组排序规则、`visibility_check_ids` 所指机制表：**部分关闭（收尾）**。`contract-artifacts.md#2/#3` 已固定规范字节、规范十进制、身份白名单与公开数组排序，`#5` 固定覆盖表，并已落入 `coverage.schema.json` 与 I-74/I-75/I-80；命名空间 ID 与 `semantic_version` 的词法**仍开放**（schema 只用非空字符串）。
8. `scenario_profile`、`external_session_ref`、`seed` 的标识符编码：`seed` 已关闭（第 4 项）；`scenario_profile` 与 `external_session_ref` **仍开放**，schema 标为"推断"的非空字符串。
9. `Error.path`、`Error.message` 的内部结构（L195 只给键名）：**仍开放**（U-11）。
10. `Field.value` 的字符串是否合法取决于该 `field_id` 目录声明的 `value_type`：**非"未定"**，而是结构上必须由目录参与校验的部分，见 I-22、U-10。

## 2. 跨结构不变量

### 2.1 身份、目录与能力（§1）

- **I-01 exact identity**：v0.1 采用 exact identity 匹配，`identity` 四键（`protocol_version`/`ruleset_id`/`catalog_id`/`backend_profile_id`）必须与后端当前身份完全相等，不自动降级、不热扩模型、不自动迁移权重。出处：§1 L28、§6 L209。
- **I-02 protocol_version**：本版冻结期要求 `protocol_version` 完全相等（即 `"0.1"`），不接受前缀/区间匹配。出处：§1 L19；§6 L209。
- **I-03 身份变更触发**：新增声明，或改变字段语义/可见性/动作规则/字段可用性，必须变更对应身份；旧目录只对旧 Gym 有效。出处：§6 L209。
- **I-04 PublicCatalog 形状**：`PublicCatalog` 恰为 `{protocol_version, ruleset_id, catalog_id, declarations}`，`declarations` 恰含九张表，空表为 `[]`（`pile_types` 是 STS 公开投影结构，不是 core 内置牌区类型）。出处：§1 L28；§3 L106–L134。
- **I-05 声明引用闭合（目录级）**：所有被引用的声明必须存在——`kind_ids`/`member_kind_ids`/`owner_kind_ids`/`element_kind_ids`/`source_kind_ids`/`target_kind_ids`/`target_kind_ids`(entity_ref 字段)/`applies_to.*`/`required_fields`/`optional_fields`/`membership_scope_field_id`/`exclusive_group_id` 指向的 ID 均须在同目录内声明。出处：§1 L48。
- **I-06 命名空间**：声明 ID 带命名空间（示例命名空间为 `demo:`），类别 ID 使用命名空间；同一 `owner` 下不允许重复同名字段；不同实例允许相同 `content_id`。出处：§1 L11、L30、L48。
- **I-07 pile_types.kind_id 独占**：每个 `pile_type` 必须声明 `kind_id`，且同一 `kind_id` 不能被两个 `pile_type` 共用（即实体 `kind_id` 唯一匹配一个已声明 `pile_type`）。出处：§1 L42；§2 L77。
- **I-08 动作/终端字段清单一致**：`action_types`/`terminal_reasons` 的 `required_fields`/`optional_fields` 必须与相关 `fields.applies_to` 及 `fields.required` 一致。出处：§1 L45、L46、L48。
- **I-09 required / conditional 语义**：`required: true` 表示适用的声明 owner 必须带该字段；`visibility: conditional` 在值尚未公开时表示为 `unknown`，不得以省略或内部值代替；`visibility_check_ids` 指向场景覆盖表中的提供方机制检查（不是 AI 侧另一套条件 DSL）。出处：§1 L40、L48。
- **I-10 省略边界**：只有协议结构明确不适用或声明为 `optional` 才能省略；"目录静态检查""当前值可知性""真实机制核对"是三个不同责任。出处：§1 L48。
- **I-11 describe 与 capability 真实性**：`describe` 返回的 `operations`/`scenario_profiles`/`seed_control`/`field_availability`/`supported_structures`/`limits`/`execution_guarantees` 必须是实际能力，不得虚假声明任意中途恢复、真实重置或跨崩溃恰好一次执行；`seed_action_replay=public_trace` 仅承诺固定 identity/场景/seed/动作语义序列后公开事实与候选语义可对齐重放，不保证不同策略消费相同随机数；`decision_pause`、`internal_snapshot_restore` 与公开 `restore`/`fork` 分开，首版后两项公开能力为 `false`。出处：§1 L50、L52。
- **I-12 unknown 与 unavailable 区分**：暂未获知是 `unknown`，后端无法提供是 `unavailable`，两者不可混用；字段可用性由 `field_availability` 声明。出处：§1 L50、L54。
- **I-13 训练绑定**：Gym 绑定 exact identity、场景配置及所需字段能力；不同后端必须核对语义和能力才能共享训练配置，名字相同不证明行为等价；`start.scenario_profile` 必须来自该后端声明的 `scenario_profiles`。出处：§1 L54；§4 L170。
- **I-14 目录不含实现**：目录只声明结构和语义，不含效果代码或隐藏资料；net 不分配模型索引，AI 侧据目录建立 GymManifest；指纹使用确定排序、明确规范化方式及内容/声明摘要，不能用时间戳代替内容身份。出处：§1 L32。
- **I-15 description 非语义**：`description` 仅供人核对，不作为效果文字特征，首次实现不得为它增加自然语言编码。出处：§1 L34。

### 2.2 PublicSnapshot（§2）

- **I-16 快照完整性**：快照必须包含 `protocol_version`、`ruleset_id`、`catalog_id`、`backend_profile_id`、`session_id`、`observation_id`、`status`、`terminal_result`、`entities`、`relations`、`piles`、`sequences`、`groups`、`legal_actions` 全部 14 键。出处：§2 L58。
- **I-17 decision/terminal 配对**：`status=decision` 时候选非空且 `terminal_result=null`；`status=terminal` 时候选为空且结果非 null；仅表达已确认的游戏结束，技术中断不伪装输赢或奖励。出处：§2 L58。
- **I-18 终局契约闭合**：`reason_id` 必须在 `terminal_reasons` 中声明，`outcome` 与该声明一致；`facts` 只使用已声明的具名类型值并遵守其 `required_fields`/`optional_fields` 及字段 `applies_to`；必需结果事实为 `known`；引用闭合；新增原因/事实改变 `catalog_id`，不得临时输出未声明字符串当作终局契约。出处：§2 L58、L60；§1 L46。
- **I-19 content_state 与 content_id**：`content_state` 为 `known/unknown/unavailable/not_applicable`，仅 `known` 带 `content_id`；无内容类型的场景/空槽仍是真实实体。出处：§2 L62。
- **I-20 Field 值存在性**：`state=known` 必须有 `value`，`unknown`/`unavailable` 不带值；`required` 且适用的字段不能省略；省略只表示没有声明，无法区分"不适用"和"可选未提供"，不能编码成 `known` 零或显式 `not_applicable`。出处：§2 L64。
- **I-21 number 规范十进制**：`number` 的 known wire 值必须是规范十进制字符串（如 `"0"`、`"12.5"`），避免先经 JSON 浮点解析丢失源精度；不接受 NaN/Infinity、指数形式、逗号；编码时去掉前导 `+` 和多余零，负零写 `"0"`；wire 解码拒绝非规范字符串，不默默重写；模型的浮点精度另由 ai-the-spire 编码契约声明。出处：§2 表 L66–L71、L73。
- **I-22 值类型匹配**：字符串值必须按该 `field_id` 声明的 `value_type` 解释——`category` 必须是目录声明的类别 ID，`entity_ref` 必须是**当前快照**的 `instance_id`，`number` 必须满足 I-21，`boolean` 必须是 JSON true/false。（schema 只能表达"字符串或布尔"，逐字段判定需要目录参与。）出处：§2 表 L66–L71。
- **I-23 结构整数**：版本、数量、序位等结构整数使用 JSON 整数，范围 `0..2^53-1`，超出拒绝配置。出处：§2 L73。
- **I-24 重复 JSON 键**：禁止重复 JSON 键（解析层拒绝）。出处：§2 L73；§6 L207。
- **I-25 关系去重**：同 `type_id`/`source_ref`/`target_ref`/`position` 完全重复的关系拒绝；`position` 语义由目录 `position_schema` 声明。出处：§2 L75。
- **I-26 pile_ref 引用与唯一性**：`pile_ref` 引用实体且引用的实体必须存在，其 `kind_id` 唯一匹配已声明 `pile_type`；`pile_ref` 在快照内唯一；抽牌/弃牌/消耗/长期牌组等由游戏适配器分别声明种类并保留在实体特征中。出处：§2 L77、L88。
- **I-27 V-06.1 位置边界**：`total_count`/`position` 是安全 JSON 整数；每个位置满足 `0 <= position < total_count`，即使成员不完整也用真实总数作边界。出处：§2 L79、L81（点此并入 V-06）。
- **I-28 V-06.2 成员闭合与计数**：`visible_members` 唯一、引用闭合、成员 kind 属于该 `pile_type`；其数量不超过 `total_count`，`members_complete=true` 时取等。出处：§2 L82。
- **I-29 V-06.3 位置与成员一致**：`known_positions` 的 `card_ref` 是 `visible_members` 的子集；每张牌至多一个位置，每个位置至多一张牌；`position` 是真实牌堆从顶部 0 起的绝对下标（编码侧 `absolute_position` 同义），允许空洞，绝不是 `visible_members` 数组下标。出处：§2 L79、L83。
- **I-30 V-06.4 不虚构卡牌**：完整成员不意味着顺序完整；位置记录不增加卡牌数；未知身份只体现在总数，不建立带内部序号的虚构卡牌。出处：§2 L84。
- **I-31 互斥组作用域**：有互斥组的 `pile_type` 指定 `membership_scope_field_id`，它必须是牌堆实体上 `required`/`known` 的 `entity_ref` 字段；相同 `exclusive_group_id` 且该字段引用同一作用域实体的牌堆，成员不能重复；战斗牌区与长期牌组使用不同互斥组，可共享卡牌引用；内部完整约束由提供方检查，消费者只检查公开成员，不从未知成员推断违规。出处：§2 L86。
- **I-32 牌堆成员唯一事实来源**：牌堆成员以 `piles` 为唯一事实来源，不在 `relations`/`groups`/`sequences` 重复表达同一成员事实；`Group` 用于其他无序归组（如候选展示分类），`Sequence` 用于公开有序流程；同一实体可参与语义不同的归组，但不能给牌堆另建冗余顺序。出处：§2 L88、L90。
- **I-33 保留声明**：使用 `piles` 的目录必须包含三个保留声明 `net:pile_member`（张量化派生关系）与字段 `net:pile_total_count`/`net:pile_members_complete`，且 wire 的 `relations`/`entities[].fields` 不重复填入这三个声明。出处：§2 L88。
- **I-34 Sequence 顺序**：`elements` 的顺序必须有公开游戏语义，从 0 起连续；同一 `type_id`+`owner_ref` 唯一；`Sequence.elements` 保留公开真实顺序，不参与无序排序。出处：§2 L90、L96。
- **I-35 Group 成员**：`members` 的 `entity_ref` 引用唯一，`multiplicity` 为正整数，具体实例的 `multiplicity=1`；本版不按内容 ID 合并同名卡牌；组内数组顺序没有语义。出处：§2 L90。
- **I-36 动作可执行性**：`LegalAction.fields` 沿用字段结构，所有执行所需字段必须 `known`；必需字段齐全、引用有效且后端当前可执行才可发布；可选字段缺省必须有声明语义，AI 不填默认值；效果中途另有选择时发布新决策点，不枚举整个未来动作序列。出处：§2 L92。
- **I-37 动作句柄唯一性**：同一完整动作语义（`action_type` 与全部具名字段相同）不得以不同 `candidate_id` 重复发布来扭曲概率总量；两张同名牌的不同实例是不同动作。出处：§2 L94。
- **I-38 排序规范**：所有无序数组及 `legal_actions` 遵守 `contract-artifacts.md#3` 的规范排序与动作语义键；`candidate_id` 不参与排序；排序不能代替公开 ID 来源审计，也不得把数组序号当作游戏位置。（该外部小节本次未读取，故只记录其存在与约束对象。）出处：§2 L96。
- **I-39 快照内引用闭合**：引用在同一快照内闭合；此前公开且当前有效的信息直接表现为字段、关系和位置；没有历史事件流、独立 `known_top`、模型字典身份、完整隐藏牌序或 RNG；UI 文案与内部诊断置于隔离调试通道，不默认成为训练输入。出处：§2 L98。
- **I-40 身份稳定性与递增**：`observation_id` 在会话内递增，稳定快照和动作绑定共用该版本；`candidate_id` 快照内唯一非空、不透明、不承诺跨快照稳定；`instance_id` 在会话内公开，在可公开跟踪的生命周期内稳定，不回收给其他实体。出处：§1 L24、L25、L26。

### 2.3 控制接口（§4）

- **I-41 串行与唯一控制者**：单客户端串行请求，一个会话只有一个控制者。出处：§4 L164。
- **I-42 request_id 规则**：`request_id` 由消费端生成的唯一非空字符串，不得换 payload 重用。出处：§4 L164。
- **I-43 ok/result/error 配对**：`ok=true` 仅有 `result`，`ok=false` 仅有 `error`。出处：§4 L164。
- **I-44 operation↔payload/result**：`operation` 必须与 `payload`（§4 表逐行给出）及成功 `result` 对应——`describe`→identity+capabilities、`get_catalog`→exact PublicCatalog、`start`→新 session_id+首个稳定快照、`observe`→`{session_status, snapshot?}`、`step`→下一 snapshot、`request_status`→`{status, response?}`；`attach`/`close` 结果结构协议未给定（见 U-05）。出处：§4 L166–L175。
- **I-45 get_catalog 失败语义**：`catalog_id` 不存在则失败，不得返回近似目录。出处：§4 L169。
- **I-46 start 前置检查**：不恢复旧局；不支持该配置/种子则在启动前拒绝；`expected_identity` 不匹配则拒绝。出处：§4 L170；§1 L54。
- **I-47 attach 不重开**：只允许控制现有会话，返回 snapshot 或明确 settling 状态，不得默默重开。出处：§4 L171。
- **I-48 observe 不推进**：`observe` 不提交玩家动作；`active`/`terminal` 返回稳定快照；`settling`/`uncertain`/`stopped`/`faulted`/`closed` 没有可执行快照。出处：§4 L172；§5 L185。
- **I-49 step 版本与结果**：`step` 完成一个候选及自动结算后返回下一 snapshot；结果不确定则 `outcome_unknown`，不虚构下一观察。出处：§4 L173。
- **I-50 request_status 语义**：`status ∈ {pending, completed, not_found, outcome_unknown}`；`completed` 返回原响应且不再执行；查询 `start` 时省略 `session_id`。出处：§4 L174、L177。
- **I-51 close 语义**：`close` 停止外部控制、释放资源，不等同于游戏已经终止；等当前 step 完成后关闭。出处：§4 L175；§5 L191。
- **I-52 去重范围**：`start` 的去重记录为进程级；`step`/`close` 为会话级；成功与拒绝响应都缓存。出处：§4 L177。
- **I-53 重发与 stale**：先核对 `request_id` 和 payload 指纹，再做候选/版本检查；完成的 `step` 原样重发返回原结果；另一个 `request_id` 使用旧版本报 `stale_observation`；`start` 原样重发不能启动两局。出处：§4 L177；§6 L201。
- **I-54 首版无回滚**：首版无 `reset-in-place`、`rollback`/`load`/`fork`；模拟训练新开局不等于部署可回到之前决策；决策时搜索不作为前提。出处：§4 L179。

### 2.4 生命周期与恢复（§5）

- **I-55 会话状态机**：会话状态为 `active/settling/terminal/uncertain/stopped/faulted/closed`；`active` 的快照 `status=decision`，`terminal` 有终止快照。出处：§5 L183。
- **I-56 step 执行顺序**：`step` 在会话锁内验证身份/版本/候选，记录 pending 并转 `settling`，再执行一次；到稳定边界后先更新快照/绑定表、记录完成结果，再应答；并发的新控制请求返回 `session_busy`；同 `request_id` 的重发查询原结果，不提交第二次。出处：§5 L183。
- **I-57 失败归因**：执行失败若能证明没有游戏效果，返回 `execution_state=not_started`；否则 `unknown` 并转 `uncertain`/`faulted`，禁止继续决策。出处：§5 L185。
- **I-58 稳定快照前提**：`observe` 读取状态时不返回 `settling` 中的半成品快照；真实游戏会自行推进时适配器必须检测并换 `observation_id`；无法保证观察和执行间原子核对则不能声明 `stable_snapshot` 支持。出处：§5 L185。
- **I-59 超时与重发**：超时只说明消费端未收到完成确认，不证明未执行；先查询原 `request_id`；只有提供方证明 `not_started` 且会话仍为同一 `active` 快照，才可发新请求；进程丢失后的 `not_found` 不是未执行证明；真实适配器不能因界面"似乎没变"就重复点击，可靠核对不足则停止控制。出处：§5 L187。
- **I-60 去重记录保留**：去重记录保留到会话关闭或进程退出，容量上限显式配置；无法保留必需记录时拒绝新控制请求；首版不承诺跨进程崩溃恰好一次执行。出处：§5 L189。
- **I-61 进程丢失处理**：模拟进程丢失可丢弃该局并新开，记录为基础设施截断；真实进程丢失须重新识别当前局及动作结果，未确定则不恢复控制。出处：§5 L189。
- **I-62 取消与关闭后**：客户端取消等待不撤销已开始的 `step`；关闭后旧句柄不执行，ID 不复用；会话长时间无法结算应达到 `configured deadline` 后进入 `uncertain`/`faulted`；执行/观察超时和上限由运行配置给出。出处：§5 L191。

### 2.5 错误、演进与传输（§6）

- **I-63 error 无隐藏状态**：`error` 恰为 `{code, message, path?, session_id?, observation_id?, execution_state, retry_mode}`，不附隐藏状态；`execution_state ∈ {not_started, completed, unknown}`，`retry_mode ∈ {correct_input, query_request, stop}`。出处：§6 L195。
- **I-64 错误码行为**：`version_mismatch`/`unsupported_capability` 拒绝且不自动降级；`invalid_input`/`unknown_type`/`dangling_reference` 修正输入且游戏不推进；`stale_observation`/`illegal_candidate` 重新 `observe` 且不得把旧候选映射到新索引；`request_conflict`/`session_busy` 按 I-53 处理；`capacity_exceeded` 调整资源或停止配置、不截断数据；`session_not_found`/`session_closed` 不自动创建新会话继续旧动作；`backend_fault`/`outcome_unknown` 依 `execution_state` 查询或停止，不盲目重发。出处：§6 L197–L205。
- **I-65 传输断开**：传输断开没有 error 响应；消费端保留 `request_id` 并按恢复规则处理；重连不得绕过 NoSL。出处：§6 L207。
- **I-66 未知内容拒绝**：顶层未知属性、未知类型、重复键和缺失必需值拒绝；可选属性仅在协议/目录已声明时省略。出处：§6 L207。
- **I-67 版本演进**：v0.1 采用 exact identity 匹配；新增声明或改变字段语义/可见性/动作规则/字段可用性须变更对应身份；旧目录仍可用于旧 Gym；后续宽松兼容需另行修订。出处：§6 L209。
- **I-68 传输绑定**：首版为本机 UTF-8 NDJSON stdin/stdout，一行一个完整请求/响应，字符串内换行转义；stdout 仅写协议，日志写 stderr；帧字节上限显式配置，超限失败且不解析部分数据；仅本机子进程，无远程监听与网络鉴权承诺；C# 内存调用复用同一数据/语义契约。出处：§6 L211。

### 2.6 能力、成功包装与 seed 的机器不变量（§1 L50/L52；§4 L183/L185）

补齐 `wire.schema.json` 中 `ScenarioProfile`、`SupportedStructure`、`ExecutionGuarantees`、`SessionSeed`、`AttachResult`、`CloseResult` 的跨结构条目。同批 0 提及的 `Limits` 由 SCHEMA-4 固定 16 个键、`SafeInteger` 保证非负安全整数；`contract-artifacts.md#4` L51 另要求"是否允许零由所选场景需求校验"，该判定依赖场景需求、当前无 fixtures 覆盖，故不单列条目而记在 §4 残余风险。键名/类型/枚举的精确集合由 `contract-artifacts.md#4-能力与状态约束` 固定，结构部分由 schema 表达；以下只写 schema 不能表达、或必须另有独立用例的部分。**行号按 protocol.md 当前 221 行版本**（§2.1–§2.5 旧条目仍按 211 行版本）。`schema_policy.py` 的 SCHEMA-3～SCHEMA-12 把这些结构结论固定成可重复断言，`wire_layers.py` 的 I-70/I-75/I-76/I-81/I-84 固定语义部分。

- **I-69 ScenarioProfile 与场景绑定**：`scenario_profiles` 每项恰为 `{id, semantic_version, required_declarations, supported_mechanism_ids, unsupported_constraints, coverage_ref}` 六键（SCHEMA-8）；`id`/`semantic_version`/`coverage_ref` 为非空字符串，其余三项为字符串数组。跨结构：`id` 集合就是 `start.scenario_profile` 的合法取值集合，集合外的 profile 必须在执行前拒绝（I-13/I-46）；`required_declarations` 必须是本 catalog 已声明 ID（I-84）；`coverage_ref` 指向本次交付的版本化覆盖表（`coverage_id`），`supported_mechanism_ids` 与 `coverage_ref` 一起把 §1 L48 的 `visibility_check_ids` 接到可校验的机制表上（I-75）。出处：§1 L50；`contract-artifacts.md#4` L45/L47。
- **I-70 ExecutionGuarantees 首版边界与重放弱承诺**：`execution_guarantees` 恰为九键（`stable_snapshot`/`request_status`/`active_deduplication`/`decision_pause`/`internal_snapshot_restore`/`seed_action_replay`/`public_state_restore`/`public_fork`/`rng_profile_id`）；前五者为布尔，`seed_action_replay ∈ {none, public_trace}`，`rng_profile_id` 为非空字符串或 null；首版 `public_state_restore`/`public_fork` 必须为 `false`（schema `const: false`，语义复核 I-70）；`seed_action_replay=public_trace` 只承诺固定 identity/场景/seed/动作语义序列后公开事实与候选语义可对齐重放，不保证不同策略消费相同随机数；`decision_pause`、`internal_snapshot_restore` 与公开 restore/fork 分开，未实现能力必须显式 `false`/`none`，不得用省略或接受未知键代替；重放/暂停/恢复键只能在 `execution_guarantees` 内，不得在 `capabilities` 顶层重复（SCHEMA-7）。不得虚假声明任意中途恢复、真实重置或跨崩溃恰好一次执行。出处：§1 L50、L52、L54；`contract-artifacts.md#4` L49；机器检查 SCHEMA-5/SCHEMA-6/I-70。
- **I-71 SupportedStructure 结构名与声明闭合**：`supported_structures` 每项恰为 `{structure, declaration_ids, unsupported_constraints}`（SCHEMA-8）；`structure` 取值域恰为 `Entity`/`Relation`/`Pile`/`Sequence`/`Group`/`LegalAction`/`TerminalResult`（对应快照的七个公开结构，不是"所有键"）；`declaration_ids` 与 `required_declarations` 一样必须是本 catalog 已声明 ID（I-84）；`unsupported_constraints` 显式列出该结构本后端未支持的部分，不复制散文覆盖率，也不得用空数组冒充"已审计"。出处：§1 L50；`contract-artifacts.md#4` L45/L47。
- **I-73 AttachResult 的包装与快照存在性**：`attach` 成功结果恰为 `{session_id, session_status}` 或 `{session_id, session_status, snapshot}`；`session_status ∈ {active, terminal, settling}`；`active`/`terminal` 必须带**对应稳定快照**（active→`status=decision`，terminal→`status=terminal`），`settling` 必须省略 `snapshot`（schema 条件分支 + I-48）；`attach` 只允许控制现有会话，返回 settling 是明确状态而非默默重开（I-47）；结果不含 `operation`，由请求账本校验 `operation→result`（I-81）。出处：§4 L183、L177；机器检查 SCHEMA-12/I-48/I-81。
- **I-76 SessionSeed 的编码与数值上界**：`start.seed` 可选；提供时必须是规范无符号 64 位十进制字符串，范围 `0..18446744073709551615`，只允许 `0` 或无前导零的正整数，不接受 JSON number、符号（`+`/`-`）、指数或空白；schema 只用 `^(0|[1-9][0-9]{0,19})$` 表达词法（SCHEMA-10），**数值上界是跨结构检查**：20 位全 9 这类值满足模式但超出 `2^64-1`，必须由 L4 拒绝（机器检查 I-76）；省略时由提供方选种子，不支持指定 seed 的后端必须在执行前拒绝（I-11/I-46）。该编码适配当前通用运行时的 `ulong`，不构成与原版随机流等价的承诺。出处：§4 L185；用例 `pos-seed-max`、`pos-seed-max-minus-one`、`pos-seed-2-pow-63`、`neg-seed-overflow`、`neg-seed-overflow-20-nines`。
- **I-81 CloseResult 与 operation→result 配对**：`close` 成功结果恰为 `{session_id, session_status:"closed"}`，`session_status` 为常量 `closed`（schema `const`，SCHEMA-12）；`close` 只停止外部控制并释放资源，不等同于游戏已终止，语义上等当前 `step` 完成后关闭（I-51）；响应不额外包含 `operation`，由请求账本关联验证 `operation→result`（I-81）；`Response.result` 必须只由具名分支组成，**不得保留能接受任意 result 的兜底分支**（SCHEMA-11，原 `$defs/UnspecifiedResult` 已删除）。出处：§4 L183、L181；机器检查 SCHEMA-11/SCHEMA-12/I-81。

**代码规则 ID 对照**（上述条目之外已实现的机器检查，主题供按 ID 定位，均已有 fixtures 期望用例）：

| ID | 主题 | 层 |
| --- | --- | --- |
| I-72 | `number_format=integer` 字段的 known 值不得带小数部分 | L3 |
| I-74 | 无序数组按规范键排序（fields/entities/relations/piles/sequences/groups/known_positions） | L3 |
| I-75 | `visibility_check_ids` 必须在覆盖表内闭合；`mechanism_id` 不重复 | L2 |
| I-77 | `position_schema.min <= max` | L2 |
| I-78 | 有 `exclusive_group_id` 的 pile_type 必须指定 `membership_scope_field_id` | L2 |
| I-79 | 保留声明 `net:pile_member`/`net:pile_total_count`/`net:pile_members_complete`：目录必须声明、wire 不得回填 | L2/L3 |
| I-80 | `legal_actions` 按动作语义键 UTF-8 字节序排列 | L3 |
| I-82 | 同一 `request_id` 不得复用不同 operation/payload | L4 |
| I-83 | `field_availability` 的 field_id 必须在目录中声明，取值 available/unavailable | L4 |
| I-84 | `scenario_profiles.required_declarations` 与 `supported_structures.declaration_ids` 必须在目录内闭合 | L4 |
| I-85 | 覆盖表 `status=verified` 必须有非空 `verification_ids` 与 `evidence_ref` | L2 |
| I-86 | 声明表内 id 不重复 | L2 |
| I-87 | 动作/终端 `required_fields`/`optional_fields` 与 `applies_to`/`required` 一致 | L2 |
| I-88 | 同一实体/动作内 `field_id` 不重复 | L3 |
| I-89 | `min_count <= max_count` | L2 |
| I-90 | `TerminalResult.facts` 按 `field_id` 排序且不重复 | L3 |
| I-91 | wire 文本严格解析（L0）：非法 JSON 常量、浮点字面量、非法 Unicode 值必须拒绝 | L0 |

## 3. 协议未定（UNSPECIFIED-BY-PROTOCOL）

以下条目在 protocol.md 原文中**没有**逐字键名/类型/取值，因此不得由任一实现单方面定义。**收尾复核后逐项标注状态**：U-01/U-02/U-03/U-05/U-06/U-12 已由 `contract-artifacts.md` 或 protocol.md 明文关闭，U-08/U-09 部分关闭；仍开放的保留待定，其 schema 节点仍留 `$comment`。

- **U-01 `Capabilities` 的精确键与内部结构**：~~**未定**~~ **已关闭（收尾）**。原文依据：L50 只给出八键名与一句话语义，L52 把精确键交给 `contract-artifacts.md#4`。收尾复核实况：`contract-artifacts.md#4`（L45/L47/L49/L51）已固定全部键、类型与枚举，schema 的 `Capabilities`/`Limits`/`ExecutionGuarantees`/`ScenarioProfile`/`SupportedStructure` 均为 `additionalProperties: false` 且逐键固定，机器检查 SCHEMA-2～SCHEMA-8 通过。跨结构条目见 §2.6 I-69/I-70/I-71。**原文"该对象显式 `additionalProperties: true`"与"只声明为 object/array"均已失效。**
- **U-02 `seed_action_replay` / `decision_pause` / `internal_snapshot_restore` / 公开 `restore`、`fork` 的键位与取值**：**已关闭（收尾）**。`contract-artifacts.md#4` L49 固定为 `execution_guarantees` 内的九键；`seed_action_replay` 是 `none`/`public_trace` 枚举，`decision_pause`/`internal_snapshot_restore` 为布尔，公开 `public_state_restore`/`public_fork` 为 `const: false`；顶层不复述（SCHEMA-6/SCHEMA-7），见 §2.6 I-70。
- **U-03 `TerminalResult.facts` 容器形状**：**已关闭（收尾）**。protocol.md L62 已固定为 `Field` 数组（与实体字段同构，按 `field_id` 排序且无重复），机器检查 SCHEMA-13 + I-90；原文"schema 留任意值 + `$comment`"已失效。
- **U-04 `Relation.position` 类型**：**已关闭（监督者收尾复核，2026-10-02）**。现行 `protocol.md:81` 已给出 wire 类型与语义："`Relation.position` 为**非负安全 JSON 整数**；目录 `position_schema` 为 null 时必须省略，否则按声明 `min`/`max` 校验"。原条目引用的 "L75" 与"未给 wire 类型"是**旧版本行号/旧状态**，已过期。该键与 `CardPosition`（`{card_ref, position}`）确实不是同一结构：前者是关系的位置、后者是牌堆中卡牌的绝对位置。
- **U-05 成功结果的未定键**：**已关闭（收尾）**。`attach`/`close` 的结果键由 protocol.md L183 固定为 `AttachResult`/`CloseResult`（见 §2.6 I-73/I-81）；`$defs/UnspecifiedResult` 兜底**已删除**，改为具名分支枚举（SCHEMA-11）；`DescribeResult`/`StartResult` 的外层键名仍属 schema 推断，但已被 SCHEMA-12 固定为断言。
- **U-06 `StartPayload.seed`**：**已关闭（收尾）**。protocol.md L185 固定为规范无符号 64 位十进制字符串，schema 用 `$defs/SessionSeed`；数值上界 `2^64-1` 由跨结构检查 I-76 承担（模式本身只到 20 位），见 §2.6 I-76。
- **U-07 `PositionSchema` 细节**：**已关闭（监督者收尾复核，2026-10-02）**。`protocol.md:81` 已逐项规定：四键 `unit`/`origin`/`min`/`max` **全部必填**；`unit`/`origin` 是**由该声明解释的非空语义字符串**——即**不额外发明固定词汇表**，故原"取值域（如 `card`/`top` 之类词汇表）"不再是未定项；`min` 为**非负安全整数**；`max` 为 `null` 或**不小于 `min` 的非负安全整数**。语义解释器（如 `contract-artifacts.md#2` 的牌堆位置用法）只消费这些声明，不得自创词汇。
- **U-08 规范化与摘要算法**：**部分关闭（收尾）**。`contract-artifacts.md` 已读取：`#2` 固定规范字节、规范十进制与身份白名单，`#3` 固定公开数组排序与动作语义键，`#4` 固定能力与状态约束。机器侧由 `wire_canonical.py` 参考实现 + `canonical`/`decimal`/`identity`/`action-key` 向量（51 项）承担。**仍开放**：命名空间 ID 与 `semantic_version` 的词法、`candidate_id` 生成规则的封闭定义。
- **U-09 标识符编码**：**部分仍开放**。命名空间 ID 语法（示例仅见 `demo:`、`net:pile_member`）、`semantic_version` 格式与比较规则、`candidate_id` 生成规则（仅"快照内唯一非空、不透明"）、`external_session_ref` 编码均未逐字给出；schema 只用非空字符串并加 `$comment`。`seed` 已由 §2.6 I-76 关闭。
- **U-10 `Field.value` 与声明 `value_type` 的对应**：schema 只能表达"字符串或布尔"。逐字段判定（number→I-21 规范十进制、category→目录类别 ID、entity_ref→当前快照 `instance_id`）需要 `field_id` 的目录声明参与，属跨结构校验（I-22），schema 不做判定。
- **U-11 `Error.path` / `Error.message` 结构**：**仍开放**。L195 只给键名；schema 将 `path`/`message` 定为字符串（推断），未定义路径语法（如 JSON Pointer）。
- **U-12 `visibility_check_ids` 指向的机制表**：**已关闭（收尾）**。`contract-artifacts.md#5` 已固定版本化覆盖表格式，机器资产为 `docs/contracts/net/0.1/coverage.schema.json`（本目录内），`check_contracts.py` 在 L1 用它对 `coverage` 文档做结构校验、在 L2 由 I-75/I-85 做引用与证据校验；5 个用例携带覆盖表文档，其中 2 个专门针对该 schema 的拒绝行为。原判定"该表的形状未给出"已失效。

## 4. 本次交付未做的事

**首轮交付时点（211 行版本）的记录：**

- ~~未读取 `contract-artifacts.md`、`spec.md`、`design.md`、`implementation-kickoff.md`；因此 U-01、U-08、U-12 只能登记，不能补齐。~~ **收尾已读 `contract-artifacts.md`**（U-01/U-02/U-03/U-05/U-06/U-08/U-12 据此关闭或部分关闭）；`spec.md`/`design.md`/`implementation-kickoff.md` 仍未读，但不影响本文件条目。
- 未定义任何 protocol.md 未出现的键、枚举值或类型；所有"推断"均在 schema 的 `$comment` 与本文 U-05/U-07/U-09/U-11 中显式标注。
- ~~未写实现、未改任何既有文件~~：契约参考检查（`tools/contract-check/`）与 `coverage.schema.json` 已在后续轮次落地；schema 的语义级校验（引用闭合、牌堆计数、去重、排序）由 `wire_layers.py` 与 2.1–2.6 的条目共同承担。

**收尾轮次的残余风险（未关闭，交后续处理）：**

- **行号漂移**：§2.1–§2.5 的出处行号对应 protocol.md 旧 211 行版本，现文件为 221 行，`§4` 实际在 L168–L189（旧条目写 L164–L179），`§5`/`§6` 同样后移。本次只保证新增条目（§2.6、§3）用当前行号，未逐条重编号。
- ~~**规则 ID 语义重载**~~ **已于 2026-10-02 消解（监督者执行）**：L0 严格文本解析规则改用新 ID **I-91**（`wire_json.py` 三处 `ParseError`、夹具 `02-decimal-type-seed.json` 的 `rule_ids`、本文件新增 I-91 行同批修改）；`I-87` 仅保留 L2「动作/终端字段清单一致性」语义。复核：`python tools/contract-check/check_contracts.py` → **210 passed / 0 failed / 210 executed（退出码 0）**。仍待处理的是另一条：`invariants.json`（`contract-artifacts.md:11` 点名的 C-01 资产）尚未生成。
- **未覆盖的边界**：`I-69`/`I-71`/`I-73` 为文档级条目，其结构部分由 SCHEMA-8/SCHEMA-11/SCHEMA-12 断言承担，尚无独立反例（如 `coverage_ref` 指向不存在覆盖表、`supported_structures` 的 `structure` 名与快照实际结构不符）。`Limits` 的"是否允许零由所选场景需求校验"（`contract-artifacts.md#4` L51）没有 fixtures，属提供方责任。
- **未做跨端验证**：C# contracts（N-01）未实现，本文件与 fixtures 只是 Python 侧参考检查；协议仍 IN_REVIEW，未冻结。
