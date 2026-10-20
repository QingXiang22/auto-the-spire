# net 规范产物、规范化与覆盖审计

本文规定公开协议资产、规范化、跨结构检查与覆盖要求。字段语义见[协议](protocol.md)，实现与验收结果见对应 PR 和 CI。

## 1. 规范资产与进入条件

游戏语义来自规则提供方。模拟目录从 RuleSet／Projection 与必要只读元数据派生，事实和候选来自内核公开输出；真实后端对齐同一约定。net 不重复登记 mod 的字段或玩法；适配边界见[架构](design.md#7-公开声明与适配边界)。

`docs/contracts/net/0.2/` 保存 `wire.schema.json`（JSON Schema 2020-12）、`invariants.json`（带稳定 ID 的跨结构检查清单）、规范化/decimal 参考实现及正反例。发行前只维护当前 0.2，旧版本输入明确拒绝，消费者同步迁移。wire schema 覆盖目录、能力、快照、请求/响应与错误的完整精确键、枚举和类型分支；所有对象默认拒绝未知键，解析阶段先拒绝重复键。

C# 和 Python contracts 由同一资产生成，或对照它实现并运行同一规范资产的检查；不得各自定义镜像协议。schema 无法表达的引用、适用域、数量、唯一性和状态组合由 invariants 执行。代码生成不能替代源解析器的重复键检查，fixtures 也不能替代通用约束验证。正例、反例和变换例的预期来自规范约束；增加独立手工计算的例子，避免让一侧 codec 生成自己验证的全部期望。

资产变化同步修订文档、正反例、两端版本与标识；规范化、引用和范围校验由共同规则约束，不将实现差异自动视为兼容。

## 2. 数值、规范序列化与标识

wire 数值字符串必须已经规范化：整数匹配 `0` 或 `-?[1-9][0-9]*`；decimal 在规范整数后可加小数部分，最后一位必须非零，绝对值小于 1 允许 `0.xxx`/`-0.xxx`。拒绝 `+1`、`01`、`-0`、`1.0`、`1e2`、空白和非有限值。编码器用源精确值去掉多余零，值为零统一输出 `0`；解码器拒绝非规范输入，不默默改写请求载荷。结构整数使用 JSON 整数，范围 `[0, 2^53-1]`，不是 number 字段的字符串。原数值的范围、精度与最大字符串长度在字段/运行配置中约束，不能无限构造大数。

规范字节为无 BOM 的 UTF-8 紧凑 JSON：对象键按 UTF-8 字节序排序；数组保留其规范语义顺序；结构整数用最短十进制；布尔/null 用 JSON 字面量。字符串保留原 Unicode，不做归一化，不转义 `/`；只转义引号、反斜线和控制字符，常用控制字符用 `\b/\t/\n/\f/\r`，其余用小写 `\u00xx`；拒绝不合法 Unicode。精确数值仍是规范字符串。参考实现逐项固定这些规则，不能依赖两端默认 serializer 恰好相同。

所有摘要采用 `sha256:<小写十六进制>`，对规范字节计算，不对人写的文件空白或时间戳计算。标识按下列白名单形成输入，排除自身 ID，排除仅供人读的 description、路径与诊断时间：

| 标识/指纹 | 摘要输入 |
| --- | --- |
| catalog_id | protocol_version、各声明表中除 description 外的全部语义键；声明按 id 排序，ID 集合排序，公开有序类别/序列不擅自排序 |
| ruleset_id | 引擎/IR 与包编译版本、包顺序、内容/脚本/依赖摘要、规则配置、数值与 PRNG/计算配置；种子不在其中 |
| backend_profile_id | 后端类型、投影/公开标识/动作映射语义版本、字段可用性与支持机制；进程 ID、具体限额和耗时不在其中 |
| request 载荷指纹 | operation 与完整 payload；request_id 是账本键，不参与载荷摘要 |
| snapshot 存储摘要 | 完整规范公开快照，包含标识与控制元数据；用于存储完整性，不冒充跨会话重放指纹 |

同一进程运行限额变化仍须在能力/RuntimeProfile 保存；未改变规则或观察语义时不强制制造新 catalog_id。规则集指纹由规则提供方产出，net 不从公开目录反推完整规则；资产中的规范算法版本也进入相应标识。示例中的 `demo:...` 标识是 fixture 标签，不能当作实际摘要格式已经通过。

## 3. 公开数组与动作语义键

字段按 field_id、实体按公开 instance_id、relations 按 type_id/source_ref/target_ref/position、piles 按 pile_ref、sequences/groups 按 type_id/owner_ref 排序。visible_members 按公开引用排序，known_positions 按 position/card_ref 排序，groups.members 按 entity_ref 排序。Sequence.elements 保留真实公开顺序；类型声明中有序数组保持声明语义。所有这些排序只看公开值。

动作语义键是规范序列化的 `{action_type, fields}`；fields 按 field_id，值使用规范 wire。legal_actions 按该键的 UTF-8 字节序排列，相同键拒绝重复；candidate_id 不参与排序/特征。执行器先按同一键对齐概率，再按固定 ExecutorProfile/执行 RNG 采样；并列也使用此规范表。候选句柄换名不影响采样，相同语义输入和执行 RNG 得到相同语义动作。

candidate_id 是**会话内**的提交引用：它由本会话的候选命名空间与该动作语义键摘要共同决定，因此同 seed、同观察版本、同动作语义的两局不会发出同一个句柄，把一局的候选提交到另一局会被按 `illegal_candidate` 拒绝且不推进那一局。命名空间只取会话句柄的规范摘要，不进语义键、标识或特征；它与语义键摘要一样与会话内观察无关（推进边界后旧句柄仍属同一会话，但由 observation_id 判定过期）。跨会话对齐与重放仍按第 45 行的公开语义映射，不比较不透明句柄字符串。

公开 ID 规范排序只消除排列这个通道，不能证明 ID 的分配安全。提供方必须审计其公开可跟踪规则；跨会话重放时按公开语义映射对齐实体/候选，排除 session_id、observation_id 和不透明句柄的字符串差异，不排除实际事实和动作差异。隐藏状态置换检查另看原始 wire 是否存在非法关联，不能把泄漏归一化掉。

## 4. 能力与状态约束

capabilities 的键、类型、所有可能枚举由 C-01 资产固定。每个 scenario_profile 包含 id、semantic_version、required_declarations、supported_mechanism_ids、unsupported_constraints、coverage_ref；supported_structures 列出协议结构名、支持的声明 ID 与显式限制，不复制散文覆盖率。field_availability 按 field_id 指定 available/unavailable。

上述 capabilities 八个键全部必填并拒绝其他键；backend_kind 为 simulation/real_game，operations 为无重复操作名数组，seed_control 为 boolean。scenario_profiles 是对象数组，其中 id/semantic_version/coverage_ref 为非空字符串，required_declarations/supported_mechanism_ids/unsupported_constraints 为字符串数组。supported_structures 是 `{structure, declaration_ids, unsupported_constraints}` 对象数组；structure 为 Entity/Relation/Pile/Sequence/Group/LegalAction/TerminalResult，后两项为字符串数组。field_availability 是具名 field_id→available/unavailable 映射，引用在语义检查层验证。

execution_guarantees 分别包含 stable_snapshot、request_status、active_deduplication、seed_action_replay、decision_pause、internal_snapshot_restore、public_state_restore、public_fork。seed_action_replay 是 none/public_trace 枚举，其余是布尔能力；另给 rng_profile_id 或 null。内部恢复能力不改变 operations；首版 public_state_restore/public_fork 必须为 false。真实后端不具备可重放性时明确 none。

limits 使用协议字段命名：entities、entity_fields、relations、piles、visible_members_per_pile、known_positions_per_pile、sequences、elements_per_sequence、groups、members_per_group、legal_actions、action_fields、frame_bytes、decimal_chars、ledger_entries、sessions；各项是非负安全整数。是否允许零由所选场景需求校验。AI 的张量/batch 限额另在 Encoding/CapacityProfile，不借用张量块名替代 net 限额。

conditional 字段仍由提供方判定是否可知。声明带 condition_description 和非空 visibility_check_ids，指向覆盖表中可运行的机制检查；消费端校验声明引用、知识状态与能力，不执行第二套规则。提供方检查未获得验证记录时该场景不能宣称 verified。不添加公开条件 DSL。

字段状态表如下；结构上真实实体和 batch padding 仍由 row_mask 区分：

| wire 表达 | 可推断含义 | 编码要求 |
| --- | --- | --- |
| known + value | 值可知，包括 0 | value_mask=1 |
| unknown，无 value | 适用，暂不可知 | value_mask=0，保留状态 |
| unavailable，无 value | 适用，但该后端不能提供 | value_mask=0，保留状态 |
| 字段省略 | 没有值或适用性声明，不能区分“不适用”和“可选未提供” | 以 absent 编码，不能推断已知零/unknown/not_applicable |
| 实体 content_state=not_applicable | 已明确没有内容类型 | 保留独立 content_state；不等于不存在实体 |

required 且适用的字段省略是错误；conditional 且尚未公开用 unknown，不省略。V-06 仅对协议明确区分的状态要求可区分，不承诺从省略恢复未提供的信息。以后需要字段级显式 not_applicable 时升版，不静默新增枚举。

## 5. 机制覆盖与观察充分性审计

每个所选场景/后端交付一张版本化覆盖表（JSON 或等价结构化表，格式在 C-01 固定），每行包含 mechanism_id、source_version/source_locator、public_fact、declaration_ids、update_and_invalidation、verification_ids、evidence_ref、status、unsupported_constraint、decision_counterexample。状态为 proposed/implemented/verified/unsupported；没有运行结果不能写 verified。

N-02 前先建立所选开发场景的机制清单和可信来源；N-02 补齐实际字段/更新和验证验证记录，N-05 检查覆盖完整。E1 进入要求有可验证来源和规则开发场景验证记录，不要求尚未实现的 net 投影已验证。E2 前完成公开事实充分性审计，E3/E4 对新增机制/后端重做。选择首个开发场景至少包括公开资源、重复实例、一个中途选择、位置更新及一个隐藏信息机制；不虚构缺失规则来凑场景。

V-15 检查来源/覆盖表引用和状态，并构造“决策相关公开事实不同”的成对状态，要求 wire 保留差异、编码不错误合并；V-02 检查隐藏状态变化不泄漏。V-15 的有限反例不能证明所有游戏 Markov；允许未确认项，但不能把涉及目标任务的未支持公开约束忽略后启动训练。

## 6. 暂不放宽的边界

首版继续对整个 catalog/ruleset/backend identity 精确匹配。新增无关内容也可能需要新 Gym/模型评估或重训，这是已登记的成本；E3 以固定的内容批次推进。仅对声明子集做兼容检查无法证明玩法/分布不受新内容影响，暂不采纳自动忽略新增声明或热迁移权重。后续可设计闭包化场景子目录，须同时证明规则与可见语义兼容并更新约定。

session_id 在全部 worker 和进程重启的客户端可见域不复用，包含每次进程启动的新 worker 实例标识和局内编号；客户端连同端点校验标识。此标识仅控制元数据，不参与 seed 重放和网络特征。

轨迹首版保存完整公开快照；允许按完整快照规范字节的 SHA-256 在同一产物根去重存储，引用为 `{digest, relative_path}`，读取时核对内容与摘要。该资产随 episode 保留，不能指向随会话/进程消失的请求账本；淘汰不得留下仍被训练/评估引用的悬空快照。尚未实现此存储时直接保存完整快照，不使用模糊的“可验证引用”。
