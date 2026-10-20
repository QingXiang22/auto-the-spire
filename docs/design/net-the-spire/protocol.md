# net-the-spire 公开协议：v0.1与ENV v0.2扩展

本文供提供方与消费端查询公开字段、操作、错误和兼容规则。第 1～6 节定义 0.1 语义，第 7 节定义 ENV 的 0.2 扩展。设计表中的状态属于 2026-10-01；已有实现和后续扩展的边界见下文状态澄清，当前验收以[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)为准。

| 项目 | 内容 |
| --- | --- |
| 状态与版本 | IN_REVIEW；v0.1 修订 2，2026-10-01；尚未冻结，无协议实现 |
| 负责人与读者 | 项目维护者；net 提供方、AI 消费端和契约检查实现者 |
| 范围 | 语言无关的数据结构、操作、版本、错误；本机传输见[设计](design.md) |
| 本次变化与决策 | 新增唯一公开协议定义；完整快照、具名字段和快照内候选句柄为待评审方案 |
| 依据与下一步 | [net 规范](spec.md)、[AI 规范](../ai-the-spire/spec.md)；实现序列化样例与两端契约检查 |

本文中的“必须”是批准此版本后实现者须遵守的规则，不表示已经实现。`demo:` 是示例命名空间。AI 的索引、数值变换和模型版本不属于游戏后端协议。

2026-10-04状态澄清：0.1已存在受控Host/共享契约实现，下文表格中的“无实现”为最初设计时点。第1～6节保留0.1语义；[第7节](#7-env正式环境的02扩展)确定正式ENV的0.2最小扩展，尚待实现，不能在旧0.1中悄悄更改seed编码。

规范资产、decimal/摘要算法、数组排序、能力结构与覆盖审计的唯一细化来源为 [contract-artifacts.md](contract-artifacts.md)。冻结前 C-01 必须交付机器 schema、跨结构不变量、规范化参考实现和双端正反例；当前散文与 JSON 示例不替代这些资产。此修订影响尚未实现的两端 contracts、目录、Manifest 和 fixtures，不能继续按旧草案单独手写两套 DTO。

## 1. 身份、目录与能力

| 身份 | 所属方与含义 |
| --- | --- |
| protocol_version | net 协议版本，初稿 `0.1`；本版冻结期要求完全相等 |
| ruleset_id | 提供方规则指纹：内核语义版本、内容包/顺序/配置及影响规则的开局配置；不含每局种子 |
| catalog_id | 提供方公开目录指纹：所有结构声明及其语义版本 |
| backend_profile_id | 后端观察/动作语义配置；语义或字段可用性改变须换身份 |
| session_id | 一次会话；全部 worker 及进程重启的客户端可见域不复用，含新 worker 实例标识与局内编号；旧请求不能作用于新局 |
| observation_id | 会话内递增的安全 JSON 整数；稳定快照和动作绑定共用该版本 |
| candidate_id | 快照内唯一非空字符串，不透明；是**该会话**当前稳定边界的提交引用：不承诺跨快照稳定，也不跨会话通用（同 seed/同观察版本/同动作语义的两局各有自己的句柄）。同一动作的跨会话对齐走[动作语义键](contract-artifacts.md#3-公开数组与动作语义键)，不用 candidate_id |
| instance_id | 会话内公开实体身份，在可公开跟踪的生命周期内稳定，不回收给其他实体 |

`identity = {protocol_version, ruleset_id, catalog_id, backend_profile_id}`。`PublicCatalog = {protocol_version, ruleset_id, catalog_id, declarations}`。declarations 有 entity_kinds/content_types/fields/relation_types/pile_types/sequence_types/group_types/action_types/terminal_reasons 数组，空表为 `[]`。pile_types 是 STS 公开投影结构，不是 core 内置牌区类型。

声明有带命名空间的 id、semantic_version 和简明语义。content_types 指定实体种类；fields 指定 value_type、applies_to（实体种类/动作类型集合）、required、公开条件；number 字段指定 number_format=integer/decimal，category 字段列出类别 ID，entity_ref 指定目标种类。动作声明列出必需与可选字段。关系声明指定来源/目标种类及可选位置的单位、起点、范围；序列/组声明指定 owner、element/member 种类、基数和用途。一个 owner 下不允许重复同名字段；不同实例允许相同 content_id。

目录声明结构和语义，不包含效果代码或隐藏资料。net 不分配模型索引；AI 根据目录建立 GymManifest。指纹使用确定排序、明确规范化方式及内容/声明摘要；实现文档须记录算法，不能用时间戳代替内容身份。

声明对象的精确键如下。未列的类型专用键不得混入其他值类型。`description` 供人核对，不作为效果文字特征；首次实现不得为它增加自然语言编码。

| 声明表 | 每项必要键与可选键 |
| --- | --- |
| entity_kinds | id、semantic_version、description |
| content_types | id、semantic_version、description、kind_ids |
| fields | id、semantic_version、description、value_type、applies_to（含 entity_kinds/action_types/terminal_reasons 数组）、required（bool）、visibility（public/conditional）；number 加 number_format，category 加 category_ids，entity_ref 加 target_kind_ids；conditional 加 condition_description、非空 visibility_check_ids |
| relation_types | id、semantic_version、description、source_kind_ids、target_kind_ids、position_schema（null 或 `{unit, origin, min, max}`，max 可 null） |
| pile_types | id、semantic_version、description、kind_id、member_kind_ids、position_schema、exclusive_group_id（可 null）、membership_scope_field_id（无互斥组时可 null）；kind_id 必须声明，且不能被两个 pile_type 共用 |
| sequence_types | id、semantic_version、description、owner_kind_ids、element_kind_ids、min_count、max_count（可 null） |
| group_types | id、semantic_version、description、owner_kind_ids、member_kind_ids、min_count、max_count（可 null） |
| action_types | id、semantic_version、description、required_fields、optional_fields（字段 ID 数组） |
| terminal_reasons | id、semantic_version、description、outcome（win/loss/other）、required_fields、optional_fields；结果 facts 仅用这些字段 |

所有引用的声明须存在，类别 ID 使用命名空间；动作/终端字段清单与 applies_to/required 一致。required 表示适用的声明 owner 必须带该字段；conditional 在值尚未公开时为 unknown，不以省略或内部值代替。只有协议结构明确不适用或 optional 才省略。visibility_check_ids 指向场景覆盖表中的提供方机制检查，条件文字供人核对，不要求 AI 执行另一套公开条件 DSL。目录静态检查、当前值可知性和真实机制核对是三个不同责任。

`describe` 返回 identity 与 capabilities：backend_kind=simulation/real_game；operations=实际支持操作；scenario_profiles=可启动配置及覆盖说明；seed_control=能否指定开局种子；field_availability=字段 available/unavailable；supported_structures；limits=实体、字段、候选、帧字节等配置上限；execution_guarantees=稳定快照、请求状态与活跃会话去重能力。不得虚假声明任意中途恢复、真实重置或跨崩溃恰好一次执行。

上述结构的精确键与能力分离见[能力约束](contract-artifacts.md#4-能力与状态约束)。seed_action_replay=public_trace 仅承诺固定 identity/场景/seed/动作语义序列后公开事实与候选语义可对齐重放；不保证不同策略消费相同随机数。decision_pause、internal_snapshot_restore 与公开 restore/fork 分开，首版后两项公开能力为 false；没有该能力的后端不能获得对应重放验收结论。

`describe` 的成功 result 恰为 `{identity, capabilities}`。所有能力键必填；重放/暂停/恢复键仅在 `execution_guarantees` 内，不能在 capabilities 顶层重复。未实现能力明确 false/none，不能用省略或接受未知键代替。机器资产见 `docs/contracts/net/0.1/wire.schema.json`；当前已有结构校验不等于完整 P-01 或跨端验收通过。

训练 Gym 绑定 exact identity、场景配置及所需字段能力。不同后端需核对语义和能力才能共享训练配置；名字相同不证明行为等价。暂未获知是 unknown，后端无法提供是 unavailable，不可混用。

## 2. PublicSnapshot

必须包含 `{protocol_version, ruleset_id, catalog_id, backend_profile_id, session_id, observation_id, status, terminal_result, entities, relations, piles, sequences, groups, legal_actions}`。status 为 decision/terminal。decision 的候选非空且 terminal_result=null；terminal 候选为空，结果为 `{outcome, reason_id, facts}`，outcome 为 win/loss/other，facts 使用已声明的具名类型值。仅表达已确认的游戏结束；技术中断不伪装输赢或奖励。

`facts` 为 Field 数组，与实体字段使用相同的 `{field_id, state, value?}`，按 field_id 排序且无重复；适用域从 reason_id 对应的 terminal_reason 检查。

reason_id 必须在 terminal_reasons 中声明，outcome 与该声明一致；facts 遵守其 required_fields/optional_fields 及字段 applies_to，必需结果事实为 known，所有引用均有效。新增原因/事实改变 catalog_id；不能临时输出未声明的字符串当作终局契约。

Entity 为 `{instance_id, kind_id, content_state, fields, content_id?}`。content_state=known/unknown/unavailable/not_applicable，仅 known 带 content_id。无内容类型的场景/空槽仍是真实实体。

Field 为 `{field_id, state, value?}`；state=known/unknown/unavailable。known 必须有值，其余不带值；required 且适用的字段不能省略；不适用或声明可选的字段可省略。省略只表示没有声明，无法区分“不适用”和“可选未提供”，不能编码成 known 零或显式 not_applicable；完整状态表见契约资产第 4 节。

Engine声明optional且当前值为None的实体字段按此规则省略，并在映射诊断中记录N-02-OPTIONAL-ABSENT；恢复实际值后正常发布known。可选entity_ref有值时仍须引用闭合，不能为了表示空而创建占位实体或发布无效句柄。required字段不适用此省略规则。

| value_type | known 的 wire 值 |
| --- | --- |
| number | 规范十进制字符串，如 `"0"`、`"12.5"`；避免先经 JSON 浮点解析丢失源精度 |
| boolean | JSON true/false |
| category | 目录声明的类别 ID 字符串 |
| entity_ref | 当前快照 instance_id 字符串 |

number 不接受 NaN/Infinity、指数形式、逗号；编码时去掉前导 `+` 和多余零，负零写 `"0"`；wire 解码拒绝非规范字符串，不默默重写。版本、数量、序位等结构整数使用 JSON 整数，范围为 0 到 2^53−1，超出拒绝配置。模型的浮点精度另由[编码契约](../ai-the-spire/encoder-design.md)声明。禁止重复 JSON 键。

Relation 为 `{type_id, source_ref, target_ref, position?}`，position 语义由目录声明；同类型/来源/目标/位置完全重复的关系拒绝。

Relation.position 为非负安全 JSON 整数；目录 position_schema 为 null 时必须省略，否则按声明 min/max 校验。PositionSchema 四键 unit/origin/min/max 全部必填，unit/origin 是由该声明解释的非空语义字符串；min 为非负安全整数，max 为 null 或不小于 min 的非负安全整数，不额外发明固定词汇表。

Pile 为 `{pile_ref, total_count, visible_members, members_complete, known_positions}`。pile_ref 引用实体，其 kind_id 唯一匹配已声明 pile_type，抽牌/弃牌/消耗/长期牌组等由游戏适配器分别声明种类并保留在实体特征中。成员是无隐藏顺序的卡牌实例引用，按公开引用排序。

位置为 `{card_ref, position}`。position 是真实牌堆从顶部 0 起的绝对下标，编码侧 absolute_position 与它同义，允许空洞，绝不是 visible_members 数组下标。跨结构不变量如下，分别纳入 V-06：

1. total_count/position 是安全 JSON 整数；每个位置满足 `0 <= position < total_count`，即使成员不完整也用真实总数作边界。
2. visible_members 唯一、引用均有效、成员 kind 属于 pile_type；其数量不超过 total_count，members_complete=true 时取等。
3. known_positions 的 card_ref 是 visible_members 的子集；每张牌至多一个位置，每个位置至多一张牌。
4. 完整成员不意味着顺序完整；位置记录不增加卡牌数。未知身份仅体现在总数，不建立带内部序号的虚构卡牌。

有互斥组的 pile_type 指定 membership_scope_field_id，它是牌堆实体上 required/known 的 entity_ref 字段。相同 exclusive_group_id 且此字段引用同一作用域实体的牌堆，成员不能重复；战斗牌区与长期牌组使用不同互斥组，可共享卡牌引用。完整内部成员约束由提供方检查，消费者检查公开成员，不从未知成员推断违规。

牌堆成员以 piles 为唯一事实来源，不在 relations/groups/sequences 重复表达同一成员事实。Group 用于其他无序归组（例如候选展示分类），Sequence 用于公开有序流程；同一实体可以参与语义不同的归组，但不能给牌堆另建冗余顺序。张量化派生关系 net:pile_member 和字段 net:pile_total_count/net:pile_members_complete；使用 piles 的目录必须包含这三个保留声明，但 wire 的 relations/entity.fields 不重复填入。pile_ref 唯一。

Sequence 为 `{type_id, owner_ref, elements}`，elements 为实体引用数组，顺序必须有公开游戏语义，从 0 起连续；同一 type_id/owner_ref 唯一。Group 为 `{type_id, owner_ref, members}`，成员为 `{entity_ref, multiplicity}`；引用唯一，multiplicity 为正整数；具体实例的 multiplicity=1。本版不按内容 ID 合并同名卡牌，避免丢失实例引用。组成员数组顺序没有语义。

LegalAction 为 `{candidate_id, action_type, fields}`。fields 沿用字段结构，所有执行所需字段必须 known；具名 entity_ref 指向来源卡牌、目标、选项、商品或节点。必需字段齐全、引用有效且后端当前可执行才可发布；可选字段缺省必须有声明语义，AI 不填默认值。效果中途另有选择时发布新决策点，不枚举整个未来动作序列。

同一完整动作语义（action_type 与全部具名字段相同）不得以不同句柄重复发布来扭曲概率总量；两张同名牌的不同实例是不同动作。所有发布动作字段为 known，未知收益或未知目标内容留在观察实体而非执行参数。

所有无序数组及 legal_actions 遵守[规范排序与动作语义键](contract-artifacts.md#3-公开数组与动作语义键)；Sequence.elements 仍保留公开真实顺序。candidate_id 不参与排序。排序不能代替公开 ID 来源审计，也不得把数组序号当作游戏位置。

正式0.2的`net:action:net.answer_decision`在目录中将`Instance`声明为optional。必选单选（1..1）仅发布带Instance的候选；可选单选（0..1，Engine EntityRefList）额外发布同action_type且fields=[]的取消候选，提交后向Engine回答空实例列表。只能提交当步实际发布的候选，不能自行省略字段制造取消。多于一项的选择仍明确拒绝。旧0.1应答目录继续声明Instance为required。快照与step的wire形状不变，C#/Python沿通用optional_fields校验；目录及后端身份随该声明改变。

2026-10-20，BEHAVIOR-01在派生0.2应答目录增加optional的`net:field:net.answer_decision.Flag`，值类型为boolean。Engine Bool暂停只发布两个同action_type候选，分别包含Flag=false或Flag=true，不含Instance，也不发布空字段取消候选；实体应答仍只含Instance或允许的空取消。step仍提交当前candidate_id，宿主从已发布绑定恢复RequestId与Flag，不接收客户端自拼的应答字段。重复／过期／跨会话候选继续经过同一账本与观察版本检查。目录摘要和后端身份改变，已有boolean字段及optional_fields的C#/Python/schema形状复用；0.1及未声明Flag的历史Profile不开放Bool应答。Int、Enum和多实体选择仍明确报告未支持。

引用的目标均在同一快照内。此前公开且当前有效的信息直接表现为字段、关系和位置；没有历史事件流、独立 known_top、模型字典身份、完整隐藏牌序或 RNG。UI 文案与内部诊断置于隔离调试通道，不默认成为训练输入。

## 3. 完整快照示例

假定目录声明 demo:player、demo:hp 和无参数 demo:end_turn。此 fixture 仅验证协议，不证明原版规则。

配套最小目录如下；此目录无 piles，因此不需要保留牌堆声明。实际战斗 fixtures 应在这个结构上添加自身使用的类型、字段和关系，不能只给一个快照而没有可验证目录。

```json
{
  "protocol_version": "0.1",
  "ruleset_id": "demo:rules-v1",
  "catalog_id": "demo:catalog-v1",
  "declarations": {
    "entity_kinds": [{"id": "demo:player", "semantic_version": "1", "description": "公开玩家实体"}],
    "content_types": [],
    "fields": [
      {
        "id": "demo:hp",
        "semantic_version": "1",
        "description": "当前公开生命值",
        "value_type": "number",
        "number_format": "integer",
        "applies_to": {"entity_kinds": ["demo:player"], "action_types": [], "terminal_reasons": []},
        "required": true,
        "visibility": "public"
      }
    ],
    "relation_types": [],
    "pile_types": [],
    "sequence_types": [],
    "group_types": [],
    "action_types": [{"id": "demo:end_turn", "semantic_version": "1", "description": "示例结束回合", "required_fields": [], "optional_fields": []}],
    "terminal_reasons": []
  }
}
```

```json
{
  "protocol_version": "0.1",
  "ruleset_id": "demo:rules-v1",
  "catalog_id": "demo:catalog-v1",
  "backend_profile_id": "demo:public-v1",
  "session_id": "session-A",
  "observation_id": 3,
  "status": "decision",
  "terminal_result": null,
  "entities": [
    {
      "instance_id": "player-A",
      "kind_id": "demo:player",
      "content_state": "not_applicable",
      "fields": [{"field_id": "demo:hp", "state": "known", "value": "45"}]
    }
  ],
  "relations": [],
  "piles": [],
  "sequences": [],
  "groups": [],
  "legal_actions": [{"candidate_id": "a7", "action_type": "demo:end_turn", "fields": []}]
}
```

## 4. 控制接口

请求 `{request_id, operation, payload}`，响应 `{request_id, ok, result?, error?}`；ok=true 仅有 result，false 仅有 error。request_id 为消费端生成的唯一非空字符串，不得换 payload 重用。单客户端串行请求，一个会话只有一个控制者。

| operation | payload | 成功结果与效果 |
| --- | --- | --- |
| describe | `{}` | identity、capabilities；不推进游戏 |
| get_catalog | `{catalog_id}` | exact PublicCatalog；不存在则失败 |
| start | `{expected_identity, scenario_profile, seed?}` | 模拟后端新 session_id 和首个稳定 snapshot；不恢复旧局；不支持配置/种子则启动前拒绝 |
| attach | `{expected_identity, external_session_ref}` | 真实后端允许控制的现有会话，以及 snapshot 或明确 settling 状态；不得默默重开 |
| observe | `{session_id}` | `{session_status, snapshot?}`；active/terminal 返回稳定快照；settling/uncertain/stopped/faulted/closed 没有可执行快照；不提交玩家动作 |
| step | `{session_id, expected_observation_id, candidate_id}` | 完成一个候选及自动结算后返回下一 snapshot；结果不确定则 outcome_unknown，不虚构下一观察 |
| request_status | `{session_id?, target_request_id}` | `{status, response?}`；pending/completed/not_found/outcome_unknown；completed 返回原响应，不再执行 |
| close | `{session_id}` | 停止外部控制、释放资源；不等同于游戏已经终止 |

成功包装固定如下：start 为 `{session_id, snapshot}`；attach 为 `{session_id, session_status, snapshot?}`，状态为 active/terminal 时必须有对应稳定快照，settling 时省略 snapshot；close 为 `{session_id, session_status: "closed"}`。响应不额外包含 operation，由请求账本关联验证 operation→result。不得保留能接受任意 result 的兜底分支。

start.seed 可选；提供时为规范无符号 64 位十进制字符串，范围 `0..18446744073709551615`，只允许 `0` 或无前导零的正整数，不接受 JSON number、符号、指数或空白。省略时由提供方选种子；不支持指定 seed 的后端在执行前拒绝。该编码适配当前通用运行时的 ulong，不构成与原版随机流等价的承诺。

start 的去重记录为进程级；step/close 为会话级。成功与拒绝响应都缓存。先核对 request_id 和 payload 指纹，再做候选/版本检查：完成的 step 原样重发返回原结果，另一个 request_id 使用旧版本报 stale_observation。start 原样重发不能启动两局。request_status 查询 start 时省略 session_id。

首版无 reset-in-place、rollback/load/fork。模拟训练新开局不等于部署可回到之前决策；决策时搜索不作为前提。

## 5. 生命周期与恢复

会话状态为 active/settling/terminal/uncertain/stopped/faulted/closed。active 的快照 status=decision，terminal 有终止快照。step 在会话锁内验证身份/版本/候选，记录 pending 并转 settling，再执行一次。到稳定边界后先更新快照/绑定表，记录完成结果，再应答。并发的新控制请求返回 session_busy；同 request_id 的重发查询原结果，不提交第二次。

执行失败若能证明没有游戏效果，返回 execution_state=not_started；否则 unknown 并转 uncertain/faulted，禁止继续决策。observe 读取状态时不返回 settling 中的半成品快照。真实游戏会自行推进时，适配器必须检测并换 observation_id；无法保证观察和执行间原子核对则不能声明 stable_snapshot 支持。

超时仅说明消费端未收到完成确认，不证明未执行。先查询原 request_id；只有提供方证明 not_started 且会话仍为同一 active 快照，才可发新请求。进程丢失后的 not_found 不是未执行证明。真实适配器不能因界面“似乎没变”就重复点击；可靠核对不足则停止控制。

去重记录保留到会话关闭或进程退出，容量上限显式配置；无法保留必需记录时拒绝新控制请求。首版不承诺跨进程崩溃恰好一次执行。模拟进程丢失可丢弃该局并新开，记录为基础设施截断；真实进程丢失须重新识别当前局及动作结果，未确定则不恢复控制。

客户端取消等待不撤销已开始的 step；close 等当前 step 完成后关闭。关闭后旧句柄不执行，ID 不复用。会话长时间无法结算应达到 configured deadline 后进入 uncertain/faulted；执行/观察超时和上限由运行配置给出，阈值由实际部署确定。

## 6. 错误、演进与传输

error 为 `{code, message, path?, session_id?, observation_id?, execution_state, retry_mode}`。execution_state=not_started/completed/unknown；retry_mode=correct_input/query_request/stop。错误不附隐藏状态。

| code | 情况与行为 |
| --- | --- |
| version_mismatch / unsupported_capability | 身份、配置、字段能力不符；拒绝，不自动降级 |
| invalid_input / unknown_type / dangling_reference | 修正输入；游戏不推进 |
| stale_observation / illegal_candidate | 重新 observe；不得把旧候选映射到新索引 |
| request_conflict / session_busy | ID 换载荷则修正；另有执行则查询原请求 |
| capacity_exceeded | 调整资源或停止该配置，不截断数据 |
| session_not_found / session_closed | 不自动创建新会话继续旧动作 |
| backend_fault / outcome_unknown | 依 execution_state 查询或停止，不盲目重发 |

传输断开没有 error 响应；消费端保留 request_id 并按恢复规则处理。重连不得绕过 NoSL。顶层未知属性、未知类型、重复键和缺失必需值拒绝；可选属性仅在协议/目录已声明时省略。

v0.1 采用 exact identity 匹配。新增声明或改变字段语义/可见性/动作规则/字段可用性须变更对应身份；旧目录仍可用于旧 Gym，不热扩模型、不自动迁移权重。后续宽松兼容需另行修订。

首版绑定为本机 UTF-8 NDJSON stdin/stdout，一行一个完整请求/响应，字符串内换行转义；stdout 仅写协议，日志写 stderr。帧字节上限显式配置，超限失败，不解析部分数据。仅本机子进程，无远程监听与网络鉴权承诺；未来远程模式须另定认证和控制所有权。C# 内存调用复用同一数据/语义契约。

## 7. ENV正式环境的0.2扩展

本节为已确定的实施契约，尚未实现；完整开局路径与身份约束见[ENV实现约束](../environment-implementation-constraints.md#4-固定配置方式解决字符串seed冲突)。只对正式单人构建及其显式dev/Ironclad范围开放0.2，旧切片/作者场景仍为0.1。

0.2沿用第1～6节的结构和生命周期，仅有以下版本化差异：

| 项目 | 0.1保留行为 | 0.2正式ENV行为 |
| --- | --- | --- |
| protocol_version | `0.1`，exact identity | `0.2`，exact identity；所有带版本的目录/快照一致 |
| start.seed | 可选规范uint64十进制字符串 | 可选非空游戏seed字符串；具体来源校验/标准化由base执行，不由net解析为ulong |
| start成功result | 恰为`{session_id, snapshot}` | 恰为`{session_id, seed, snapshot}`；seed为本次实际接受/生成的游戏字符串 |
| scenario_profile | 旧场景的实际支持ID | 当前正式构建的dev/Ironclad/完整范围ID，不接受旧切片ID伪装正式构建 |

Host固定character/ascension及规则配置进入ruleset_id；每局seed不进入ruleset_id/catalog_id/backend_profile_id。start.expected_identity先与describe实际身份完全匹配，再验证场景/seed；任何错误在创建/启动前返回，不消耗游戏RNG。请求重发沿账本返回同一seed/session/snapshot，不重生成seed。

start未带seed时，使用Host的显式game-seed默认值，或由base按已声明配置生成新seed；成功响应总是报告实际值。客户端/对拍保存该控制元数据，后续重开同一Host可为每局提供不同seed；不能依赖重启进程换seed。字符串`"0"`在0.2是游戏字符串，不能特殊转换为旧原始ulong种子。既有快照状态/候选/目录数据格式不改变；seed元数据不加入模型特征。

0.2未知键、重复键、空seed、类型错误及非法场景仍严格拒绝；0.1继续拒绝非数字seed。即使请求seed形状在两版本都合法，expected_identity中的版本也必须匹配，不能自动降级或按字符串内容猜版本。describe能力字段形状不扩展；seed_control=true仅在实际创建路径使用指定seed时发布，execution_guarantees.rng_profile_id必须报告实际Profile。

共享机器资产新增`docs/contracts/net/0.2/`版本，目录/快照结构可复用公共定义，但版本常量和start请求/结果分支分别校验。C#/Python codec、schema正反例、请求载荷摘要和start重发/默认seed/实际接受seed检查同批更新；不能只改C#Host放宽字段，再让Python或机器schema继续按0.1读取。已验收0.1资产和检查保留，0.2兼容限于本节明确差异，不新增load/fork或自由配置内部状态的操作。
