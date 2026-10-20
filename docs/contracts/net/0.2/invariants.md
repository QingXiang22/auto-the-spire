# net-the-spire 协议 v0.2 跨结构不变量

本文维护协议 v0.2 的跨结构要求；机器检查规则见 [invariants.json](invariants.json)，协议语义见[公开协议](../../../design/net-the-spire/protocol.md)。准确字段与范围以现行协议及规则为准。历史审阅结论与执行结果查 Git 和 CI。

## 1. 适用范围

只维护当前协议 0.2；旧版本输入明确拒绝。目录、快照、控制与账本的规则 ID 保持稳定，schema 和 Python／C# 消费者共同检查。

## 2. 跨结构不变量

### 2.1 标识、目录与能力（§1）

- **I-01 exact identity**：v0.2 采用 exact identity 匹配，`identity` 四键（`protocol_version`/`ruleset_id`/`catalog_id`/`backend_profile_id`）必须与后端当前标识完全相等，不自动降级、不热扩模型、不自动迁移权重。
- **I-02 protocol_version**：本版版本维护期要求 `protocol_version` 完全相等（即 `"0.2"`），不接受前缀/区间匹配。
- **I-03 标识变更触发**：新增声明，或改变字段语义/可见性/动作规则/字段可用性，必须变更对应标识；消费者同步重建。
- **I-04 PublicCatalog 形状**：`PublicCatalog` 恰为 `{protocol_version, ruleset_id, catalog_id, declarations}`，`declarations` 恰含九张表，空表为 `[]`（`pile_types` 是 STS 公开投影结构，不是 core 内置牌区类型）。
- **I-05 声明引用完整性（目录级）**：所有被引用的声明必须存在——`kind_ids`/`member_kind_ids`/`owner_kind_ids`/`element_kind_ids`/`source_kind_ids`/`target_kind_ids`/`target_kind_ids`(entity_ref 字段)/`applies_to.*`/`required_fields`/`optional_fields`/`membership_scope_field_id`/`exclusive_group_id` 指向的 ID 均须在同目录内声明。
- **I-06 命名空间**：声明 ID 带命名空间（示例命名空间为 `demo:`），类别 ID 使用命名空间；同一 `owner` 下不允许重复同名字段；不同实例允许相同 `content_id`。
- **I-07 pile_types.kind_id 独占**：每个 `pile_type` 必须声明 `kind_id`，且同一 `kind_id` 不能被两个 `pile_type` 共用（即实体 `kind_id` 唯一匹配一个已声明 `pile_type`）。
- **I-08 动作/终端字段清单一致**：`action_types`/`terminal_reasons` 的 `required_fields`/`optional_fields` 必须与相关 `fields.applies_to` 及 `fields.required` 一致。
- **I-09 required / conditional 语义**：`required: true` 表示适用的声明 owner 必须带该字段；`visibility: conditional` 在值尚未公开时表示为 `unknown`，不得以省略或内部值代替；`visibility_check_ids` 指向场景覆盖表中的提供方机制检查（不是 AI 侧另一套条件 DSL）。
- **I-10 省略边界**：只有协议结构明确不适用或声明为 `optional` 才能省略；"目录静态检查""当前值可知性""真实机制核对"是三个不同责任。
- **I-11 describe 与 capability 真实性**：`describe` 返回的 `operations`/`scenario_profiles`/`seed_control`/`field_availability`/`supported_structures`/`limits`/`execution_guarantees` 必须是实际能力，不得虚假声明任意中途恢复、真实重置或跨崩溃恰好一次执行；`seed_action_replay=public_trace` 仅承诺固定 identity/场景/seed/动作语义序列后公开事实与候选语义可对齐重放，不保证不同策略消费相同随机数；`decision_pause`、`internal_snapshot_restore` 与公开 `restore`/`fork` 分开，首版后两项公开能力为 `false`。
- **I-12 unknown 与 unavailable 区分**：暂未获知是 `unknown`，后端无法提供是 `unavailable`，两者不可混用；字段可用性由 `field_availability` 声明。
- **I-13 训练绑定**：Gym 绑定 exact identity、场景配置及所需字段能力；不同后端必须核对语义和能力才能共享训练配置，名字相同不证明行为等价；`start.scenario_profile` 必须来自该后端声明的 `scenario_profiles`。
- **I-14 目录不含实现**：目录只声明结构和语义，不含效果代码或隐藏资料；net 不分配模型索引，AI 侧据目录建立 GymManifest；指纹使用确定排序、明确规范化方式及内容/声明摘要，不能用时间戳代替内容标识。
- **I-15 description 非语义**：`description` 仅供人核对，不作为效果文字特征，首次实现不得为它增加自然语言编码。

### 2.2 PublicSnapshot（§2）

- **I-16 快照完整性**：快照必须包含 `protocol_version`、`ruleset_id`、`catalog_id`、`backend_profile_id`、`session_id`、`observation_id`、`status`、`terminal_result`、`entities`、`relations`、`piles`、`sequences`、`groups`、`legal_actions` 全部 14 键。
- **I-17 decision/terminal 配对**：`status=decision` 时候选非空且 `terminal_result=null`；`status=terminal` 时候选为空且结果非 null；仅表达已确认的游戏结束，技术中断不伪装输赢或奖励。
- **I-18 终局约定一致性**：`reason_id` 必须在 `terminal_reasons` 中声明，`outcome` 与该声明一致；`facts` 只使用已声明的具名类型值并遵守其 `required_fields`/`optional_fields` 及字段 `applies_to`；必需结果事实为 `known`；所有引用均有效；新增原因/事实改变 `catalog_id`，不得临时输出未声明字符串当作终局约定。
- **I-19 content_state 与 content_id**：`content_state` 为 `known/unknown/unavailable/not_applicable`，仅 `known` 带 `content_id`；无内容类型的场景/空槽仍是真实实体。
- **I-20 Field 值存在性**：`state=known` 必须有 `value`，`unknown`/`unavailable` 不带值；`required` 且适用的字段不能省略；省略只表示没有声明，无法区分"不适用"和"可选未提供"，不能编码成 `known` 零或显式 `not_applicable`。
- **I-21 number 规范十进制**：`number` 的 known wire 值必须是规范十进制字符串（如 `"0"`、`"12.5"`），避免先经 JSON 浮点解析丢失源精度；不接受 NaN/Infinity、指数形式、逗号；编码时去掉前导 `+` 和多余零，负零写 `"0"`；wire 解码拒绝非规范字符串，不默默重写；模型的浮点精度另由 ai-the-spire 编码约定声明。
- **I-22 值类型匹配**：字符串值必须按该 `field_id` 声明的 `value_type` 解释——`category` 必须是目录声明的类别 ID，`entity_ref` 必须是**当前快照**的 `instance_id`，`number` 必须满足 I-21，`boolean` 必须是 JSON true/false，`string` 必须是 JSON 字符串（可为空）。（schema 只能表达"字符串或布尔"，逐字段判定需要目录参与。）
- **I-23 结构整数**：版本、数量、序位等结构整数使用 JSON 整数，范围 `0..2^53-1`，超出拒绝配置。
- **I-24 重复 JSON 键**：禁止重复 JSON 键（解析层拒绝）。
- **I-25 关系去重**：同 `type_id`/`source_ref`/`target_ref`/`position` 完全重复的关系拒绝；`position` 语义由目录 `position_schema` 声明。
- **I-26 pile_ref 引用与唯一性**：`pile_ref` 引用实体且引用的实体必须存在，其 `kind_id` 唯一匹配已声明 `pile_type`；`pile_ref` 在快照内唯一；抽牌/弃牌/消耗/长期牌组等由游戏适配器分别声明种类并保留在实体特征中。
- **I-27 V-06.1 位置边界**：`total_count`/`position` 是安全 JSON 整数；每个位置满足 `0 <= position < total_count`，即使成员不完整也用真实总数作边界。
- **I-28 V-06.2 成员完整性与计数**：`visible_members` 唯一、引用均有效、成员 kind 属于该 `pile_type`；其数量不超过 `total_count`，`members_complete=true` 时取等。
- **I-29 V-06.3 位置与成员一致**：`known_positions` 的 `card_ref` 是 `visible_members` 的子集；每张牌至多一个位置，每个位置至多一张牌；`position` 是真实牌堆从顶部 0 起的绝对下标（编码侧 `absolute_position` 同义），允许空洞，绝不是 `visible_members` 数组下标。
- **I-30 V-06.4 不虚构卡牌**：完整成员不意味着顺序完整；位置记录不增加卡牌数；未知标识只体现在总数，不建立带内部序号的虚构卡牌。
- **I-31 互斥组作用域**：有互斥组的 `pile_type` 指定 `membership_scope_field_id`，它必须是牌堆实体上 `required`/`known` 的 `entity_ref` 字段；相同 `exclusive_group_id` 且该字段引用同一作用域实体的牌堆，成员不能重复；战斗牌区与长期牌组使用不同互斥组，可共享卡牌引用；内部完整约束由提供方检查，消费者只检查公开成员，不从未知成员推断违规。
- **I-32 牌堆成员唯一事实来源**：牌堆成员以 `piles` 为唯一事实来源，不在 `relations`/`groups`/`sequences` 重复表达同一成员事实；`Group` 用于其他无序归组（如候选展示分类），`Sequence` 用于公开有序流程；同一实体可参与语义不同的归组，但不能给牌堆另建冗余顺序。
- **I-33 保留声明**：使用 `piles` 的目录必须包含三个保留声明 `net:pile_member`（张量化派生关系）与字段 `net:pile_total_count`/`net:pile_members_complete`，且 wire 的 `relations`/`entities[].fields` 不重复填入这三个声明。
- **I-34 Sequence 顺序**：`elements` 的顺序必须有公开游戏语义，从 0 起连续；同一 `type_id`+`owner_ref` 唯一；`Sequence.elements` 保留公开真实顺序，不参与无序排序。
- **I-35 Group 成员**：`members` 的 `entity_ref` 引用唯一，`multiplicity` 为正整数，具体实例的 `multiplicity=1`；本版不按内容 ID 合并同名卡牌；组内数组顺序没有语义。
- **I-36 动作可执行性**：`LegalAction.fields` 沿用字段结构，所有执行所需字段必须 `known`；必需字段齐全、引用有效且后端当前可执行才可发布；可选字段缺省必须有声明语义，AI 不填默认值；效果中途另有选择时发布新决策点，不枚举整个未来动作序列。
- **I-37 动作句柄唯一性**：同一完整动作语义（`action_type` 与全部具名字段相同）不得以不同 `candidate_id` 重复发布来扭曲概率总量；两张同名牌的不同实例是不同动作。
- **I-38 排序规范**：所有无序数组及 `legal_actions` 遵守 `contract-artifacts.md#3` 的规范排序与动作语义键；`candidate_id` 不参与排序；排序不能代替公开 ID 来源审计，也不得把数组序号当作游戏位置。
- **I-39 快照内引用完整性**：引用的目标均在同一快照内；此前公开且当前有效的信息直接表现为字段、关系和位置；没有历史事件流、独立 `known_top`、模型字典标识、完整隐藏牌序或 RNG；UI 文案与内部诊断置于隔离调试通道，不默认成为训练输入。
- **I-40 标识稳定性与递增**：`observation_id` 在会话内递增，稳定快照和动作绑定共用该版本；`candidate_id` 快照内唯一非空、不透明、不承诺跨快照稳定；`instance_id` 在会话内公开，在可公开跟踪的生命周期内稳定，不回收给其他实体。

### 2.3 控制接口（§4）

- **I-41 串行与唯一控制者**：单客户端串行请求，一个会话只有一个控制者。
- **I-42 request_id 规则**：`request_id` 由消费端生成的唯一非空字符串，不得换 payload 重用。
- **I-43 ok/result/error 配对**：`ok=true` 仅有 `result`，`ok=false` 仅有 `error`。
- **I-44 operation↔payload/result**：`operation` 必须与 `payload`（§4 表逐行给出）及成功 `result` 对应——`describe`→identity+capabilities、`get_catalog`→exact PublicCatalog、`start`→新 session_id+实际 seed+首个稳定快照、`observe`→`{session_status, snapshot?}`、`step`→下一 snapshot、`request_status`→`{status, response?}`；`attach`/`close` 按 I-73／I-81 的精确键集合校验。
- **I-45 get_catalog 失败语义**：`catalog_id` 不存在则失败，不得返回近似目录。
- **I-46 start 前置检查**：不恢复旧局；不支持该配置/种子则在启动前拒绝；`expected_identity` 不匹配则拒绝。
- **I-47 attach 不重开**：只允许控制现有会话，返回 snapshot 或明确 settling 状态，不得默默重开。
- **I-48 observe 不推进**：`observe` 不提交玩家动作；`active`/`terminal` 返回稳定快照；`settling`/`uncertain`/`stopped`/`faulted`/`closed` 没有可执行快照。
- **I-49 step 版本与结果**：`step` 完成一个候选及自动结算后返回下一 snapshot；结果不确定则 `outcome_unknown`，不虚构下一观察。
- **I-50 request_status 语义**：`status ∈ {pending, completed, not_found, outcome_unknown}`；`completed` 返回原响应且不再执行；查询 `start` 时省略 `session_id`。
- **I-51 close 语义**：`close` 停止外部控制、释放资源，不等同于游戏已经终止；等当前 step 完成后关闭。
- **I-52 去重范围**：`start` 的去重记录为进程级；`step`/`close` 为会话级；成功与拒绝响应都缓存。
- **I-53 重发与 stale**：先核对 `request_id` 和 payload 指纹，再做候选/版本检查；完成的 `step` 原样重发返回原结果；另一个 `request_id` 使用旧版本报 `stale_observation`；`start` 原样重发不能启动两局。
- **I-54 首版无回滚**：首版无 `reset-in-place`、`rollback`/`load`/`fork`；模拟训练新开局不等于部署可回到之前决策；决策时搜索不作为前提。

### 2.4 生命周期与恢复（§5）

- **I-55 会话状态机**：会话状态为 `active/settling/terminal/uncertain/stopped/faulted/closed`；`active` 的快照 `status=decision`，`terminal` 有终止快照。
- **I-56 step 执行顺序**：`step` 在会话锁内验证标识/版本/候选，记录 pending 并转 `settling`，再执行一次；到稳定边界后先更新快照/绑定表、记录完成结果，再应答；并发的新控制请求返回 `session_busy`；同 `request_id` 的重发查询原结果，不提交第二次。
- **I-57 失败归因**：执行失败若能证明没有游戏效果，返回 `execution_state=not_started`；否则 `unknown` 并转 `uncertain`/`faulted`，禁止继续决策。
- **I-58 稳定快照前提**：`observe` 读取状态时不返回 `settling` 中的半成品快照；真实游戏会自行推进时适配器必须检测并换 `observation_id`；无法保证观察和执行间原子核对则不能声明 `stable_snapshot` 支持。
- **I-59 超时与重发**：超时只说明消费端未收到完成确认，不证明未执行；先查询原 `request_id`；只有提供方证明 `not_started` 且会话仍为同一 `active` 快照，才可发新请求；进程丢失后的 `not_found` 不是未执行证明；真实适配器不能因界面"似乎没变"就重复点击，可靠核对不足则停止控制。
- **I-60 去重记录保留**：去重记录保留到会话关闭或进程退出，容量上限显式配置；无法保留必需记录时拒绝新控制请求；首版不承诺跨进程崩溃恰好一次执行。
- **I-61 进程丢失处理**：模拟进程丢失可丢弃该局并新开，记录为基础设施截断；真实进程丢失须重新识别当前局及动作结果，未确定则不恢复控制。
- **I-62 取消与关闭后**：客户端取消等待不撤销已开始的 `step`；关闭后旧句柄不执行，ID 不复用；会话长时间无法结算应达到 `configured deadline` 后进入 `uncertain`/`faulted`；执行/观察超时和上限由运行配置给出。

### 2.5 错误、演进与传输（§6）

- **I-63 error 无隐藏状态**：`error` 恰为 `{code, message, path?, session_id?, observation_id?, execution_state, retry_mode}`，不附隐藏状态；`execution_state ∈ {not_started, completed, unknown}`，`retry_mode ∈ {correct_input, query_request, stop}`。
- **I-64 错误码行为**：`version_mismatch`/`unsupported_capability` 拒绝且不自动降级；`invalid_input`/`unknown_type`/`dangling_reference` 修正输入且游戏不推进；`stale_observation`/`illegal_candidate` 重新 `observe` 且不得把旧候选映射到新索引；`request_conflict`/`session_busy` 按 I-53 处理；`capacity_exceeded` 调整资源或停止配置、不截断数据；`session_not_found`/`session_closed` 不自动创建新会话继续旧动作；`backend_fault`/`outcome_unknown` 依 `execution_state` 查询或停止，不盲目重发。
- **I-65 传输断开**：传输断开没有 error 响应；消费端保留 `request_id` 并按恢复规则处理；重连不得绕过 NoSL。
- **I-66 未知内容拒绝**：顶层未知属性、未知类型、重复键和缺失必需值拒绝；可选属性仅在协议/目录已声明时省略。
- **I-67 版本演进**：v0.2 采用 exact identity 匹配；新增声明或改变字段语义/可见性/动作规则/字段可用性须变更对应标识；目录变化后重建 Gym；后续宽松兼容需另行修订。
- **I-68 传输绑定**：首版为本机 UTF-8 NDJSON stdin/stdout，一行一个完整请求/响应，字符串内换行转义；stdout 仅写协议，日志写 stderr；帧字节上限显式配置，超限失败且不解析部分数据；仅本机子进程，无远程监听与网络鉴权承诺；C# 内存调用复用同一数据/语义约定。

### 2.6 能力、成功包装与游戏 seed 的机器不变量

补齐 `wire.schema.json` 中 `ScenarioProfile`、`SupportedStructure`、`ExecutionGuarantees`、`GameSeedString`、`AttachResult`、`CloseResult` 的跨结构条目。 `Limits` 由 SCHEMA-4 固定 16 个键、`SafeInteger` 保证非负安全整数；`contract-artifacts.md#4` L51 另要求"是否允许零由所选场景需求校验"，该判定由具体场景检查。键名/类型/枚举的精确集合由 `contract-artifacts.md#4-能力与状态约束` 固定，结构部分由 schema 表达；以下只写 schema 不能表达、或必须另有独立用例的部分。`schema_policy.py` 的 SCHEMA-3～SCHEMA-12 把这些结构结论固定成可重复断言，`wire_layers.py` 的 I-70/I-75/I-76/I-81/I-84 固定语义部分。

- **I-69 ScenarioProfile 与场景绑定**：`scenario_profiles` 每项恰为 `{id, semantic_version, required_declarations, supported_mechanism_ids, unsupported_constraints, coverage_ref}` 六键（SCHEMA-8）；`id`/`semantic_version`/`coverage_ref` 为非空字符串，其余三项为字符串数组。跨结构：`id` 集合就是 `start.scenario_profile` 的合法取值集合，集合外的 profile 必须在执行前拒绝（I-13/I-46）；`required_declarations` 必须是本 catalog 已声明 ID（I-84）；`coverage_ref` 指向本次交付的版本化覆盖表（`coverage_id`），`supported_mechanism_ids` 与 `coverage_ref` 一起把 §1 L48 的 `visibility_check_ids` 接到可校验的机制表上（I-75）。
- **I-70 ExecutionGuarantees 首版边界与重放弱承诺**：`execution_guarantees` 恰为九键（`stable_snapshot`/`request_status`/`active_deduplication`/`decision_pause`/`internal_snapshot_restore`/`seed_action_replay`/`public_state_restore`/`public_fork`/`rng_profile_id`）；前五者为布尔，`seed_action_replay ∈ {none, public_trace}`，`rng_profile_id` 为非空字符串或 null；首版 `public_state_restore`/`public_fork` 必须为 `false`（schema `const: false`，语义复核 I-70）；`seed_action_replay=public_trace` 只承诺固定 identity/场景/seed/动作语义序列后公开事实与候选语义可对齐重放，不保证不同策略消费相同随机数；`decision_pause`、`internal_snapshot_restore` 与公开 restore/fork 分开，未实现能力必须显式 `false`/`none`，不得用省略或接受未知键代替；重放/暂停/恢复键只能在 `execution_guarantees` 内，不得在 `capabilities` 顶层重复（SCHEMA-7）。不得虚假声明任意中途恢复、真实重置或跨崩溃恰好一次执行。
- **I-71 SupportedStructure 结构名与声明一致性**：`supported_structures` 每项恰为 `{structure, declaration_ids, unsupported_constraints}`（SCHEMA-8）；`structure` 取值域恰为 `Entity`/`Relation`/`Pile`/`Sequence`/`Group`/`LegalAction`/`TerminalResult`（对应快照的七个公开结构，不是"所有键"）；`declaration_ids` 与 `required_declarations` 一样必须是本 catalog 已声明 ID（I-84）；`unsupported_constraints` 显式列出该结构本后端未支持的部分，不复制散文覆盖率，也不得用空数组冒充"已审计"。
- **I-73 AttachResult 的包装与快照存在性**：`attach` 成功结果恰为 `{session_id, session_status}` 或 `{session_id, session_status, snapshot}`；`session_status ∈ {active, terminal, settling}`；`active`/`terminal` 必须带**对应稳定快照**（active→`status=decision`，terminal→`status=terminal`），`settling` 必须省略 `snapshot`（schema 条件分支 + I-48）；`attach` 只允许控制现有会话，返回 settling 是明确状态而非默默重开（I-47）；结果不含 `operation`，由请求账本校验 `operation→result`（I-81）。
- **I-76 游戏 seed**：start.seed 可选；提供时必须为非空字符串，不接受 JSON number 或 null。原样保留 Unicode、大小写、前导零、符号和空白；来源限制由规则提供方在建局前校验。start 成功结果必须报告实际 seed。

- **I-81 CloseResult 与 operation→result 配对**：`close` 成功结果恰为 `{session_id, session_status:"closed"}`，`session_status` 为常量 `closed`（schema `const`，SCHEMA-12）；`close` 只停止外部控制并释放资源，不等同于游戏已终止，语义上等当前 `step` 完成后关闭（I-51）；响应不额外包含 `operation`，由请求账本关联验证 `operation→result`（I-81）；`Response.result` 必须只由具名分支组成，**不得保留能接受任意 result 的兜底分支**（SCHEMA-11，原 `$defs/UnspecifiedResult` 已删除）。

**代码规则 ID 对照**（上述条目之外已实现的机器检查，主题供按 ID 定位，均已有 fixtures 期望用例）：

| ID | 主题 | 层 |
| --- | --- | --- |
| I-72 | `number_format=integer` 字段的 known 值不得带小数部分 | L3 |
| I-74 | 无序数组按规范键排序（fields/entities/relations/piles/sequences/groups/known_positions） | L3 |
| I-75 | `visibility_check_ids` 必须在覆盖表中有对应条目；`mechanism_id` 不重复 | L2 |
| I-77 | `position_schema.min <= max` | L2 |
| I-78 | 有 `exclusive_group_id` 的 pile_type 必须指定 `membership_scope_field_id` | L2 |
| I-79 | 保留声明 `net:pile_member`/`net:pile_total_count`/`net:pile_members_complete`：目录必须声明、wire 不得回填 | L2/L3 |
| I-80 | `legal_actions` 按动作语义键 UTF-8 字节序排列 | L3 |
| I-82 | 同一 `request_id` 不得复用不同 operation/payload | L4 |
| I-83 | `field_availability` 的 field_id 必须在目录中声明，取值 available/unavailable | L4 |
| I-84 | `scenario_profiles.required_declarations` 与 `supported_structures.declaration_ids` 必须在目录中有对应条目 | L4 |
| I-85 | 覆盖表 `status=verified` 必须有非空 `verification_ids` 与 `evidence_ref` | L2 |
| I-86 | 声明表内 id 不重复 | L2 |
| I-87 | 动作/终端 `required_fields`/`optional_fields` 与 `applies_to`/`required` 一致 | L2 |
| I-88 | 同一实体/动作内 `field_id` 不重复 | L3 |
| I-89 | `min_count <= max_count` | L2 |
| I-90 | `TerminalResult.facts` 按 `field_id` 排序且不重复 | L3 |
| I-91 | wire 文本严格解析（L0）：非法 JSON 常量、浮点字面量、非法 Unicode 值必须拒绝 | L0 |
