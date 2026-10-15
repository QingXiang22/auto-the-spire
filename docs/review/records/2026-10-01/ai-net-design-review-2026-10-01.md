# ai-the-spire 与 net-the-spire 设计评审

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-02、W-04、W-06、W-08，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

> 后续处置见[设计评审采纳记录](design-review-disposition-2026-10-01.md)：已修改相关设计，F-01…F-23 分别记录采纳、修正或保留；本报告原审阅时点与判断不覆盖后续版本，未因文档修订整体归档。

日期：2026-10-01。状态：待用户评审；本次只新增本报告与只读检查工具，未修改任何设计文档。决策负责人：项目维护者（用户）。

评审对象：`ai-the-spire` 模块设计包与 `net-the-spire` 模块设计包（均为 IN_REVIEW，无实现）。评审范围：契约完整性与可实现性、模块间职责边界、需求到验收的可追踪性、评估与可复现性假设。**不在评审范围内**：`core-the-spire` 与 `mod-the-spire` 的实现现状、规则保真度与内容覆盖；用户已说明这两个模块后续可能重新讨论，因此本文只在"设计向提供方提出了什么要求"这一层面引用它们，不把当前源码能否满足作为结论依据。评审方式为静态阅读加只读脚本检查，未运行构建、规则检查或任何 AI/net 代码。

## 1. 评审对象清单

| 模块 | 文档 | 头部版本 |
| --- | --- | --- |
| ai-the-spire | [spec.md](../../../design/ai-the-spire/spec.md) | v0.2 |
| | [design-v0.md](../../../design/ai-the-spire/design-v0.md) | v1.3 |
| | [encoder-design.md](../../../design/ai-the-spire/encoder-design.md) | v0.4 |
| | [policy-contract.md](../../../design/ai-the-spire/policy-contract.md) | v0.1 |
| | [training-design.md](../../../design/ai-the-spire/training-design.md) | v0.1 |
| | [arc42.md](../../../design/ai-the-spire/arc42.md) | 无状态表 |
| | [literature.md](../../../design/ai-the-spire/literature.md) | 无状态表 |
| net-the-spire | [spec.md](../../../design/net-the-spire/spec.md) | v0.1 |
| | [design.md](../../../design/net-the-spire/design.md) | v0.1 |
| | [protocol.md](../../../design/net-the-spire/protocol.md) | v0.1 |
| 跨模块 | [ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md) | v0.1 |
| | [ai-net-verification.md](../../../design/ai-net-verification.md) | v0.1 |
| | [ai-net-operations.md](../../../design/ai-net-operations.md) | v0.1 |
| 导航 | [docs/README.md](../../../README.md) | 2026-10-01 |

`ai-the-spire/` 与 `net-the-spire/` 两个目录当前为空（各 0 个条目），且未在 `AutoTheSpire.sln` 中注册。本评审的"设计"即上表文档，不含代码。

本文严格区分四种状态：**设计目标**（文档写了）、**已批准**（负责人接受）、**已实现**（代码存在）、**已验证**（有可复现执行证据）。上表全部处于"设计目标 + 待评审"，没有任何一项进入后三种状态；下文所有判断都按此前提表述。

## 2. 评审方法与本轮实际执行的检查

精读上表 13 份文档全文，并核对了 §5 的追踪关系。为可复核，新增两个只读脚本：

- [check_docs.py](../../../../tools/doc-check/check_docs.py)：Markdown 相对链接目标、标题锚点（GitHub slug 规则）、```json 围栏严格解析（拒绝重复键）、围栏闭合、行尾空白。
- [check_trace.py](../../../../tools/doc-check/check_trace.py)：把 AI spec 第 5 节、net spec 第 2 节、验收文档第 4 节三张追踪表按需求 ID 提取场景集合并逐行比对。

执行记录：

| 检查 | 命令 | 结果 |
| --- | --- | --- |
| 文档链接/锚点/JSON/空白 | `python tools/doc-check/check_docs.py .` | 扫描 40 份 Markdown；AI/net 设计包（`design/ai-the-spire`、`design/net-the-spire`、`design/` 根下跨模块文档及 `docs/README.md`）**0 错误**；剩余 25 条分布在两份无关报告（见 F-22） |
| JSON 样例 | 同上（含在上一行内） | [protocol.md](../../../design/net-the-spire/protocol.md) 第 3 节 2 个样例严格解析通过 |
| 需求声明计数 | 同上 | 20 个：AI-01…AI-10、NET-01…NET-10，全部在验收/计划中被引用 |
| 追踪表比对 | `python tools/doc-check/check_trace.py .` | 20 行中 **7 行不一致**（见 §5） |
| 任务 ID 追踪 | PowerShell 集合比对 | A-01、A-04、A-05、N-05 已定义但未被任何需求引用 |
| 空白 | `git diff --check` | 通过（exit 0，仅既有 LF→CRLF 提示） |
| 构建 / 规则检查 / 内容加载 | 未执行 | 本次为文档评审，未改规则、DSL、注册或代码；两模块无实现可运行 |

改动文件：仅本报告与 `tools/doc-check/` 下两个脚本。未触碰 `core-the-spire`、`mod-the-spire`、任何设计文档，也未移动或整理既有的未提交改动。

## 3. 总体结论

这份设计包作为"待评审方案"的质量高于同类项目的常见水平：它把"用户已确认的约束"和"待批准的提案"分开写，通篇标注 IN_REVIEW 和不存在的实现，主动拒绝在获得批准前编造预算或性能结论。安全边界（只用当前观察、不维护历史缓存、不虚构隐藏实体、隐藏顺序不得进入公开输入）和数值表示（精确十进制 wire、known/unknown/unavailable/padding 四分、mask 与通道分离）都考虑得相当细致，这部分应当保留。

主要缺口不在"该不该这么做"，而在两类工程契约边界：

1. **可机检的契约边界不足。** 规范以自然语言表格承载了本应由机器校验的内容——覆盖度、能力、限制声明、终端原因、条件可见性；且没有单一机器可读的 wire schema，Python 与 C# 两套 codec 各自手写，一致性只能靠人工维护的 fixtures 保证。这直接与设计自己承诺的"exact identity 匹配"和"新增声明须变更身份"冲突：身份要靠规范化摘要，而摘要对象没有被规范化定义。
2. **评估与可复现性假设没有落到协议能力上。** 设计承诺"同种子配对评估""固定种子可复现""按模拟支持范围内重放一致"，但协议的能力声明里没有对应的能力位，候选句柄明确不跨快照稳定，候选列表顺序也未约束。

因此建议：**不要在当前文本下冻结协议 v0.1，也不要让实现者按现文同时手写两套 codec。** 先闭合 §4 的 P1 项（其中 F-01、F-02、F-05、F-06 是纯文档工作，不需要新决策），再进入 E0。E0 本身不必等待 P2 全部定案。

## 4. 设计上值得保留的部分

- **责任边界写得清楚且可执行。** AI 只处理"如何表示已提供的当前观察"，提供方负责"玩家知道什么、如何随游戏变化更新"。编码器不得查内核、不得重放事件、不得读取先前快照（[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):32），这条边界在三份文档中表述一致，没有互相矛盾。
- **反泄漏的默认姿态正确。** 不设 known_top、不建历史缓存、不为未知身份的隐藏牌虚构实体、公开实例 ID 不得由隐藏排列或内部下标派生（[spec.md](../../../design/net-the-spire/spec.md):48）。这是这类项目最容易失守的地方，此处防线完整。
- **数值与知识状态的表示是精细的。** wire 用精确十进制字符串以避免 JSON 浮点丢精度、AI 侧再按 Manifest 变换（[protocol.md](../../../design/net-the-spire/protocol.md):60-65）；known 零、unknown、unavailable、not_applicable、padding 五种情形的区分与 mask 规则明确，并配了对应验收场景 V-06。
- **身份分层与演进规则完整。** protocol/ruleset/catalog/backend_profile/session/observation/candidate/instance 八层身份各有定义（[protocol.md](../../../design/net-the-spire/protocol.md):15-24），Gym/Manifest/ModelConfig/Profile 四层版本各自独立又绑定 exact identity（[design-v0.md](../../../design/ai-the-spire/design-v0.md):136）。
- **执行安全高于便利。** 请求账本、原样重发只执行一次、换载荷冲突、过期观察不推进、结果未知先查询后停止、跨崩溃不假承诺恰好一次——这套生命周期（[protocol.md](../../../design/net-the-spire/protocol.md):160-170）对 NoSL 项目是必要的，且写得比多数同类设计更严谨。
- **拒绝臆定门槛。** "没有批准阈值时允许报告测量，不允许写达标"（[ai-net-verification.md](../../../design/ai-net-verification.md):30）、"零测试不能记为通过"（同文:74）等约束正确地阻止了把计划当结果。
- **事实 / 奖励 / 偏好三层分离。** 允许向量奖励但不以向量化替代目标定义（[training-design.md](../../../design/ai-the-spire/training-design.md):35），并明确"未选候选没有真实收益标签"。

## 5. 必须处理的问题（P1）

以下问题会在协议冻结前造成返工，或在实现后暴露为不可判定的分歧。F-01、F-02、F-05、F-06 属于文档修订，不需要新的产品决策。

### F-01 没有机器可读的规范 schema，双端 codec 将各自手写

- 位置：[protocol.md](../../../design/net-the-spire/protocol.md):11-79（全部 wire 结构均以表格与散文定义）；[ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md):32（A-01 由 Python 侧自行实现"公开目录和 snapshot 数据模型、十进制 codec"）；同文:48（N-01 再实现"C# DTO、PublicCatalog、能力、严格 codec 和双端 fixtures"）。
- 问题：规范只有散文与示例。E0 先在 Python 侧手写一套镜像 DTO，N-01 再手写 C# 一套，然后靠"双端 fixtures"证明一致——**fixtures 由其中一侧编写，因此"一致"是断言而非推导**。需要人肉同步的规则至少包括：精确十进制字符串的规范化（前导 `+`、多余零、负零、指数、逗号）、结构整数的 2^53−1 上限、顶层未知属性/重复键拒绝、声明对象的类型专用键禁混（[protocol.md](../../../design/net-the-spire/protocol.md):32-42）、`position_schema` 的 `null` 语义、以及 §5 的排序与摘要算法。
- 影响：这是整个 E0/E1 的地基。任何一处漂移都会在跨语言联调时才暴露，且因为双方都"自认为符合规范"而难以定位。设计自己要求"实现文档须记录算法，不能用时间戳代替内容身份"（[protocol.md](../../../design/net-the-spire/protocol.md):30），恰恰说明这些算法必须被固定下来。
- 建议：选定一份机器可读的规范作为唯一事实来源（JSON Schema 或等价 IDL + 规范化摘要算法与 decimal codec 的规范性伪代码），由它生成或校验两端实现，并让 fixtures 从规范派生而非手写。E0 的 Python 模型改为"由规范生成/对照"而不是"另行定义"。

### F-02 无序集合在 wire 上的数组顺序没有约束，且示例与规范自相矛盾

- 位置：[protocol.md](../../../design/net-the-spire/protocol.md):69（"成员是无隐藏顺序的卡牌实例引用"）、同文:73（"组成员数组顺序没有语义"）——这两处只声明"顺序无意义"，**没有**要求提供方按规范顺序输出，也没有任何一处规定 snapshot 数组的规范化排序（:30 的"确定排序"只针对目录指纹）。
- 反例证据：[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):86-97 的示例给出 `"visible_members": ["C", "A", "B", "D", "E"]`，配合 `known_positions` 为 C→0、D→3。若把数组下标读作位置，几何恰好是 C@0、A@1、B@2、D@3、E@4——即**该数组正是内部抽牌顺序**。而同一文档:137 要求"无序成员按公开引用或完整描述排序"，按此规则编码前必须重排为 `["A","B","C","D","E"]`，示例本身并不是合法规范化形式。同文:99 又明确"不能把 visible_members 的数组下标当作实际位置"。
- 附带不一致：:90 的示例只给出 C@0 与 D@3，而同文档:225 的验收场景描述同一批牌为"C 位置 0、D 位置 3、E 位置 4"。两处对同一 fixture 的位置集合不同（示例缺 E@4），且 [ai-net-verification.md](../../../design/ai-net-verification.md):38（V-03）用的是 `{C:0, D:3}`。同一验收 fixture 在三个位置有三种写法。
- 影响：V-02 要求"数组顺序、ID 与候选也不能暗带隐藏信息"（[ai-net-verification.md](../../../design/ai-net-verification.md):37），即**验收要求顺序不携带隐藏信息，而契约没有要求**。一个"完全合规"的提供方可以输出内部顺序，从而在 V-02 中失败，却找不到违反了哪条规范。反之，一个读数组下标当位置的编码器也能通过它看到的示例，这正是设计最想防的错误。
- 建议：在协议中明确 `visible_members`、`elements`、`members` 等数组的 wire 顺序规则（二选一：要求规范排序，或声明顺序任意但禁止由内部顺序导出，并规定消费者必须先按引用排序）；把:90 的示例改为规范化顺序，或改为"顺序随意的四张牌 + 已知位置"以消除几何暗示；统一示例、§9 场景与 V-03 的三处 fixture。

### F-03 牌堆位置语义与基数不变量没有闭合

- 位置：[protocol.md](../../../design/net-the-spire/protocol.md):69（`position` "顶部为 0，允许离散位置；…不越界"；"成员完整时数量等于 total_count，否则不大于它"）；[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):99（"位置必须为范围内整数"）；同文:156、170 使用 `absolute_position`（"从 0 起且允许空洞"）。
- 问题：wire 字段名是 `position`，编码侧字段名是 `absolute_position`，两者未显式对齐；"越界/范围内"的基准未写明——是真实牌堆下标（`[0, total_count)`）还是 `visible_members` 的下标？两者在 `members_complete=false` 且存在已知位置时完全不同。此外三条跨结构不变量只在散文里零散出现，未写成可校验的规则集：定位牌集合 ⊆ `visible_members`、`len(visible_members) ≤ total_count`（完整时取等）、所有 `position < total_count`、`total_count` 本身是 JSON 整数还是十进制字符串也未说明。
- 影响：E0 的 `validate_snapshot` 无法据此写出确定性的校验；A-03 的完成判据"离散位置和超限错误完整"（[ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md):34）没有可判定的定义。
- 建议：以"`position` 是绝对下标 `[0, total_count)`"为唯一读法（与 `absolute_position` 一致），把上述四条不变量写成编号列表并各自对应 V-06 的一条断言；统一 `position`/`absolute_position` 的命名或显式声明二者同义。

### F-04 piles 缺少声明表与牌堆语义声明

- 位置：[protocol.md](../../../design/net-the-spire/protocol.md):34-42 的声明表只有 entity_kinds、content_types、fields、relation_types、sequence_types、group_types、action_types——**没有 pile_types**；牌堆只通过 :69"pile_ref 引用牌堆实体"和 :71"使用 piles 的目录必须包含这三个保留声明"被间接表达。
- 问题：relation/sequence/group 三种集合结构都有各自的声明表（来源/目标种类、基数、owner、用途），唯独 piles 没有。消费者的可推导信息因此只有"这个目录里有牌堆"以及"牌堆是一个实体"；无法从目录得知某个牌堆实体是哪一种牌堆（抽牌堆 / 弃牌堆 / 消耗堆 / 出牌堆 / 长期牌组），也无法注册按牌堆种类区分的表示通道。而 [encoder-design.md](../../../design/ai-the-spire/encoder-design.md):74 明确要求"手牌、弃牌、消耗牌及未知顺序的抽牌部分按公开语义处理为多重集合"，这正需要牌堆语义。另外"同一牌不能同时属于同层级的两个战斗牌堆"（:71）中的"同层级"也没有定义。
- 建议：增加 `pile_types` 声明表（id、semantic_version、owner_kind_ids、member_kind_ids、position_schema、语义），并规定牌堆实体必须属于某个已声明 pile_type；把"同层级"替换为可判定的表述。

### F-05 需求→场景追踪表 7/20 行不一致，4 个任务未被需求追踪

两张独立维护的追踪表存在真实矛盾（脚本输出，见 §2）：

| 需求 | AI/net spec 第 2/5 节声明 | 验收文档第 4 节声明 | 性质 |
| --- | --- | --- | --- |
| AI-02 | V-05、V-08 | V-01、V-08 | **矛盾**：各自独有一个对方没有的场景 |
| NET-06 | V-04、V-07 | V-04、V-09 | **矛盾**：同上 |
| AI-01 | V-03 | V-02、V-03 | 子集不对称 |
| AI-03 | V-11 | V-05、V-11 | 子集不对称 |
| AI-04 | V-06 | V-01、V-06 | 子集不对称 |
| AI-10 | V-07、V-14 | V-06、V-07、V-14 | 子集不对称 |
| NET-02 | V-04 | V-04、V-09 | 子集不对称 |

两个"矛盾"行必须判定哪张表正确：AI-02"实体、字段、集合、关系和候选引用可扩展"对应 V-01（目录 round-trip）还是 V-05（同名牌实例与重排）；NET-06"成功响应意味着已取得下一稳定快照"对应 V-07（重发/崩溃）还是 V-09（重放与 NoSL）。

另外，[ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md) 定义了 15 个任务，其中 **A-01、A-04、A-05、N-05 在任何需求行中都没有出现**；A-04（tensorize/collate、mask）和 A-05（契约验收）恰恰是 E0 的核心交付，却没有任何需求 ID 追踪到它们。

- 建议：把追踪关系收敛为单一事实来源（一处维护，另一处只做链接引用），修正两处矛盾行，并为 4 个任务补上所属需求或明确标注为"支撑性任务，不直接对应需求 ID"。

### F-06 覆盖度、能力与限制声明全部是自然语言，无法机检

四处关键声明没有可校验结构：

| 声明 | 位置 | 现状 | 后果 |
| --- | --- | --- | --- |
| `scenario_profiles` | [protocol.md](../../../design/net-the-spire/protocol.md):46（定义）、:149（`start` 载荷）、[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):38、[policy-contract.md](../../../design/ai-the-spire/policy-contract.md):39 | "可启动配置及覆盖说明"——散文 | AI 无法机检某场景所需的公开结构是否被支持；E3 的"所需公开结构可表示"（[ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md):21）无法判定 |
| `supported_structures` | [protocol.md](../../../design/net-the-spire/protocol.md):46 | 全文仅出现一次，无定义 | 与 `scenario_profiles` 职责重叠，且都没有 schema |
| `condition_description` | [protocol.md](../../../design/net-the-spire/protocol.md):38 | 条件是散文，"不要求 AI 执行一套公开条件 DSL"（:44） | `visibility=conditional` 的字段无法被消费端校验；V-06 只能对具体 fixture 断言，不能判定一般情形 |
| terminal `reason_id` / `facts` | [protocol.md](../../../design/net-the-spire/protocol.md):52 | `reason_id` 是未声明的字符串；"facts 使用已声明的具名类型值"但 reason 本身不在任何声明表中 | 演进规则"新增声明…须变更对应身份"（:188）抓不到新增的终端原因；消费端无法从目录枚举可能的结果 |

同一根因还导致能力集不完整：`describe` 缺少"确定性/可重放"、"RNG 流结构"、"会话可暂停/可序列化"这三类能力位，而 V-09 与同种子配对评估（[design-v0.md](../../../design/ai-the-spire/design-v0.md):99）都依赖它们（见 F-10、F-15）。

- 建议：把 `scenario_profiles`、`supported_structures` 定义为结构化对象（含所需的声明 ID 列表与不支持的公开约束清单），把 terminal reason 声明为目录中的 category/entity 类别，把 `condition_description` 升级为"必须同时给出可判定的字段谓词或由提供方自检的场景 ID"。目录静态检查、当前值可知性与真实机制核对本就是三个不同责任（[protocol.md](../../../design/net-the-spire/protocol.md):44），这正需要机器可读的边界而不是散文。

### F-07 机制→字段→来源→场景 映射表没有格式、归属与验收门

- 位置要求：[design.md](../../../design/net-the-spire/design.md):41（"必须先对每个训练配置建立'公开机制 → 当前字段/位置 → 提供方来源 → 验证场景'的映射"）、同文:45（"先使用明确覆盖的切片，列出未支持的公开约束"）。
- 现状：[ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md):49 把 N-02 的完成判据写成"当前位置/变量覆盖有来源和机制测试"；同文:81 把"模拟切片和当前公开事实来源"列为维护者决定项，截止"N-02 前"，但没有选择标准、没有产物格式、没有独立验收门。E1 的进入条件却是"选定已验证的内核切片，能产生所需当前公开事实"（:19）。
- 影响：这是全项目负载最重的一份交付物（E3 的进入条件、AI 侧观察充分性的唯一输入、真实适配的核对清单都依赖它），却只有一句散文要求。它既不在任何任务的产物清单里，也不在验收场景的判定对象里；E1 进入条件所需的东西要等 E1 里的 N-02 才产出，两者的时序关系没有说明。
- 建议：为该映射规定固定格式（每行：公开机制 / 对应字段与关系 / 提供方来源 / 验证场景 ID / 当前状态），把它列为 N-02 的显式产物并配一条验收判据，明确"切片选择标准"由维护者在 N-02 前给出；把该表作为 E3 进入条件的检查对象。

### F-08 观察充分性（非 Markov）风险没有被任何审计产物或验收覆盖

- 位置与承认：[spec.md](../../../design/ai-the-spire/spec.md):83（"当前观察可能不是严格的 Markov 状态"）、[design-v0.md](../../../design/ai-the-spire/design-v0.md):150（D-01 的后果写为"提供方承担充分性"）、同文:106（列为待决项）。
- 现状：V-01…V-14 中，与观察相关的只有 V-02（隐藏状态变化 → 公开输入等价）与 V-03（已知位置保留/失效）。两者检验的都是**不泄漏**方向，即"不同的隐藏状态必须映射到相同的公开表示"。充分性要求的是反方向——"决策相关的状态不得被合并"。这一方向没有任何验收场景，也没有审计产物；只有事后升级路径："发现可决策局面被合并，应提交具体缺失的公开变量/关系/约束和反例"（[spec.md](../../../design/ai-the-spire/spec.md):83）。
- 影响：这是唯一一个"设计正确但可能整体不成立"的风险。没有前置审计时，它只会在 E2 训练不出效果时才被发现，而那时的失败定位成本最高（[training-design.md](../../../design/ai-the-spire/training-design.md):73 的排查顺序把"候选引用、mask、梯度、奖励语义"排在表示容量之前，观察不足排在最后）。
- 建议：不要把它做成单元测试（它本质上不是可断言的泄漏性质），而是明确一个前置审计交付物：对目标切片逐机制列出"影响决策的公开事实 → 当前表示中的载体 → 若缺失则给出具体缺失项与反例"。可与 F-07 的映射表合并为同一份产物，E2 进入前完成。

## 6. 需要决策或修订的问题（P2）

### F-09 `catalog_id` 的身份粒度使任何内容新增作废全部已训模型

- 位置：[protocol.md](../../../design/net-the-spire/protocol.md):19（`catalog_id` = "所有结构声明及其语义版本"的指纹）、:48（"训练 Gym 绑定 exact identity"）、:188（"新增声明或改变字段语义/可见性/动作规则/字段可用性须变更对应身份"）；[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):44（内容集合变化即构建新 Manifest，且"首阶段不实现增量扩表和旧权重迁移"）；[ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md):72（E3"新增内容或字段要同时更新目录、Manifest、样例、训练分布和模型身份"）。
- 问题：身份的最小兼容单位是**整个目录**，而实际兼容需求只是"该 Gym 用到的声明不变"。因此向目录新增一张与当前角色无关的卡，也会让 E3 已经训好的模型无法用于新目录，即使模型所需的字段、动作与内容全部不变。E3 的名称就是"整局与内容扩展"，其迭代方式必然是持续新增内容，于是每次迭代都从零重训。
- 影响：这是全生命周期中成本最高的架构后果，但文档从未把它作为风险登记（只作为已接受的后果）。在"部署成本优先"的驱动下，它可能是决定项目可行性的那一条。
- 建议：引入"所需声明子集"身份（Gym 声明其依赖的声明 ID 集合与语义版本，要求该子集 exact 匹配），或允许 Gym 固定一个目录的冻结子集并显式忽略新增声明；若坚持全目录指纹，请在 [design-v0.md](../../../design/ai-the-spire/design-v0.md) 的风险清单中登记其训练成本，并说明 E3 的迭代预算如何覆盖重复重训。

### F-10 "同种子配对评估"与协议缺失的 RNG 能力

- 位置：[design-v0.md](../../../design/ai-the-spire/design-v0.md):99（"用相同起始种子比较候选和已发布策略"）；[training-design.md](../../../design/ai-the-spire/training-design.md):65（"对同种子配对报告任务指标、方差/置信区间"）；[protocol.md](../../../design/net-the-spire/protocol.md):46 的能力集只有 `seed_control`。
- 问题：不同策略会立刻走出不同轨迹，相同起始种子只在"随机数按决策点分流、与策略无关"时才能形成配对（公共随机数），否则同种子只是共享一组种子、无法降低方差，"配对"一词不成立。协议没有任何要求或能力位声明 RNG 流结构，`backend_profile_id`（:20）的语义只覆盖"观察/动作语义配置"。
- 建议：二选一并写进文档——要么在协议中要求模拟后端按决策点分流的 RNG 结构并在 capability 中声明（例如 `deterministic_streams`），从而让配对评估成立；要么把评估方法表述改为"共享种子集合的独立评估"，并相应下调方差与样本量门槛的期望。

### F-11 `session_id` 的唯一性域没有定义

- 位置：[protocol.md](../../../design/net-the-spire/protocol.md):21（`session_id` 语义为"一次会话；开新局分配新 ID"）、:156（"`start` 的去重记录为进程级"）、:168（去重记录"保留到会话关闭或进程退出"）；[ai-net-operations.md](../../../design/ai-net-operations.md):17（"每个 worker 使用独立会话/RNG/状态"）；[training-design.md](../../../design/ai-the-spire/training-design.md):61（"训练时并行的是独立模拟会话"）。
- 问题：E2 需要多个 worker 进程，而账本与去重是进程级的。若两个 worker 各自分配出 `session-A`，一个同时管理多 worker 的客户端无法区分；`request_status` 的 `session_id` 可选语义也会歧义。
- 建议：规定 `session_id` 在客户端可见范围内唯一（例如 `<worker_id>/<local_id>`，并把 worker 标识纳入能力声明），或在协议中明确"一个客户端进程同一时刻只与一个 worker 建立会话"。

### F-12 候选句柄不跨快照稳定、候选顺序未约束，但评估要求固定种子可复现

- 位置：[protocol.md](../../../design/net-the-spire/protocol.md):23（`candidate_id`"不承诺跨快照稳定"）；[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):137（"候选保留外部顺序并返回映射"）；[policy-contract.md](../../../design/ai-the-spire/policy-contract.md):21（`choices` 顺序"建议与输入一致"）、:27（并列处理"建议等概率并列采样并记录执行 RNG"；"评估时使用固定执行配置及 RNG 种子"）。
- 问题：wire 的 `legal_actions` 顺序由提供方决定且未约束；AI 保留该顺序；执行器从有序列表做 categorical 采样时，同一个 RNG 种子在不同候选顺序下会选出不同动作。设计对"概率按句柄对齐"有要求（AI-08），但对"采样结果可复现"只有建议。
- 建议：二选一并写入契约——在协议中要求 `legal_actions` 按稳定键排序（如 action_type + 具名引用）；或规定执行器采样必须与列表顺序无关（例如按动作语义指纹派生 RNG 子流，或先按稳定键排序再采样）。任选其一都能让"固定种子可复现"成为可检验的性质。

### F-13 轨迹的"可验证内容引用"未定义，且与账本生命周期冲突

- 位置：[training-design.md](../../../design/ai-the-spire/training-design.md):43（transition 记录"完整公开 snapshot **或其可验证内容引用**"）；[protocol.md](../../../design/net-the-spire/protocol.md):168（"去重记录保留到会话关闭或进程退出"）。
- 问题："可验证内容引用"从未定义——是摘要、内容寻址存储、还是指向会话日志的指针？若为后者，账本与快照在进程退出后不再保留，训练重启后该引用不可解析，轨迹便无法自证完整。这与 [training-design.md](../../../design/ai-the-spire/training-design.md):45"append-only 记录…运行轨迹和 checkpoint 放入配置指定的产物目录"存在语义冲突：要么快照必须整份落盘（成本未评估），要么引用机制必须自包含。
- 建议：定义该引用的形式与生命周期（推荐：内容寻址 + 摘要，快照按摘要去重落盘），并在 [ai-net-operations.md](../../../design/ai-net-operations.md):28 的存储指标中显式给出单步快照体积与轨迹总量的量级估计。

### F-14 模块布局缺少 policy/encoding → net DTO 的依赖边

- 位置：[design-v0.md](../../../design/ai-the-spire/design-v0.md):117（"models/policy → encoding；execution → policy + client；…encoding 不依赖 client 的活动会话"）；[policy-contract.md](../../../design/ai-the-spire/policy-contract.md):13（`predict(snapshot, model_bundle)`"只接收 net 当前快照"）；[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):52（编码器消费 `PublicSnapshot` 结构）。
- 问题：按契约，`policy` 与 `encoding` 都必须消费 net 的 wire 类型，但布局只给出 `encoding 不依赖 client 的活动会话` 这一否定式，没有区分"wire DTO 类型"与"活动客户端/传输"。实际后果是 E0 在没有任何 net 实现的情况下必须自行镜像一套 DTO（见 F-01），同一份 schema 被实现两次。
- 建议：在布局中显式区分 `contracts`（纯 DTO，可由规范生成）与 `client`（传输与请求状态），让 `encoding`/`policy` 依赖 `contracts` 而不依赖 `client`；这同时是 F-01 的落地方式。

### F-15 能力集缺少确定性、重放与暂停能力位

- 位置：[protocol.md](../../../design/net-the-spire/protocol.md):46 的能力清单；:30 `execution_guarantees` 仅覆盖"稳定快照、请求状态与活跃会话去重"；V-09 要求"模拟支持范围内重放一致"（[ai-net-verification.md](../../../design/ai-net-verification.md):44），"支持范围"无定义。
- 问题：V-09、design-v0 §6 的同种子比较、以及 E1 的"固定种子随机合法执行可重放"（[ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md):51）都依赖确定性重放，但它既不是能力位，也没有定义"什么算可重放"（同一 seed + 同一动作序列 → 同一公开快照序列？同一事件序列？）。
- 建议：新增 `replay_determinism` 能力位（none / seed+action-sequence / full-state-copy）并定义判定口径；把 V-09 的"支持范围内"替换为对该能力值的断言。

### F-16 字段级 `not_applicable` 用省略表达，与实体侧显式枚举不对称

- 位置：[protocol.md](../../../design/net-the-spire/protocol.md):56（Field 的 `state` 只有 known/unknown/unavailable；"不适用或声明可选的字段可省略，遗漏不等于零"）、:44（"required 表示适用的声明 owner 必须带该字段；conditional 在值尚未公开时为 unknown"）；[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):66、68（实体的 `content_state` 则显式含 `not_applicable`，且固定枚举 `not_applicable=4`）。
- 问题：实体侧"不适用"是显式 wire 值，字段侧"不适用"靠省略；对 `required: false` 的字段，"不适用"与"可选但未提供"无法区分。这本身是自洽的（遗漏不携带信息），但两套编码方式并存且只在散文里说明，消费端极易把"缺失"当作某一具体含义。V-06 的"各知识/适用状态可区分"因此只在实体侧可判定。
- 建议：在协议中以一张状态-编码对照表明确两种表示，并显式写出"对字段而言，缺失 ⇔ 无任何声明"，或统一为显式值以消除不对称。

## 7. 文档一致性与低级问题（P3）

- **F-17 piles 与 groups/sequences 表示重叠。** [protocol.md](../../../design/net-the-spire/protocol.md):71 规定"牌堆成员以 piles 为唯一事实来源，不再在 relations/groups 重复表达"，但 `Group`（:73）本身就是"owner + 成员多重集"，与 pile 语义高度重叠；去重规则只覆盖了 piles↔relations/groups，没有说明 sequences 是否也受同一约束，也没说一个实体能否同时是某 pile 的成员和某 group 的成员。建议明确三者的选择准则（何时用 pile、何时用 group）。
- **F-18 量化/蒸馏后视为新模型需重评的规则没有需求 ID 与场景。** [policy-contract.md](../../../design/ai-the-spire/policy-contract.md):41 与 [ai-net-operations.md](../../../design/ai-net-operations.md):25 都要求量化后重测并视为新模型，但 [ai-net-verification.md](../../../design/ai-net-verification.md):55-68 的追踪表中没有对应行（AI-09 只覆盖量化前的测量）。建议新增需求或在 AI-09 下补场景。
- **F-19 两处声明截止时间冲突。** 晋升指标与置信要求：[ai-net-verification.md](../../../design/ai-net-verification.md):27 要求"E2 前声明"，[ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md):84 写"L-04 实验前"（L-04 在 E2 之内，更宽松）。资源预算：[ai-net-verification.md](../../../design/ai-net-verification.md):28 要求"E2 前批准"，[ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md):82 写"L-01 选维度前"（L-01 在 E2 之内，更严格）。两处都应统一到较早的时点。
- **F-20 把张量概念写成外部可观察要求。** [spec.md](../../../design/ai-the-spire/spec.md):39 与需求 AI-03 写"补齐候选的概率为 0"，但 [policy-contract.md](../../../design/ai-the-spire/policy-contract.md):23 明确"外部 choices 不带 padding"，padding 只存在于 tensor 内。V-11 的"有效/补齐候选混合…padding零"同样混用了两个接口。建议把该要求改写为"内部张量的补齐项为 0"，外部接口的不变量只保留"每个当前候选恰有一项、无额外项、总和为 1"。
- **F-21 用 AI 张量块名描述 net 侧规模。** [ai-net-operations.md](../../../design/ai-net-operations.md):24 用"E/F/R/P/S/G/A/AF"表示每快照规模，这些符号来自 [encoder-design.md](../../../design/ai-the-spire/encoder-design.md):151-160 的 AI 张量块，而 net 侧自己的数组名是 entities/relations/piles/sequences/groups/legal_actions（[protocol.md](../../../design/net-the-spire/protocol.md):52），且 `limits`（:46）从未枚举协议自身的数组。建议 `limits` 按协议数组逐项命名。
- **F-22 文档卫生。** 本轮检查发现 25 条问题全部位于两份无关报告：[agents-md-conformance-2026-09-30.md](../../archived/agents-md-conformance-2026-09-30.md) 含 24 处 `#L..` 形式锚点（按既有约定它们是审计时的源码定位，不是 Markdown 标题锚点，但形式上会被任何 Markdown 链接检查判为断链），[project-progress-review-2026-09.md](../../../design/project-progress-review-2026-09.md):3 有一处行尾空白。同时全仓库没有文档 CI 门，而 [ai-net-verification.md](../../../design/ai-net-verification.md):72 要求"每批执行本地链接/锚点、JSON 样例与空白检查"——该要求目前只能靠人工与临时脚本。建议把 `tools/doc-check/` 接入一个最小检查入口，并约定历史 `#L..` 引用改用 `路径:行号` 的散文写法。
- **F-23 状态表不齐。** [arc42.md](../../../design/ai-the-spire/arc42.md) 与 [literature.md](../../../design/ai-the-spire/literature.md) 没有其他文档都有的"状态与版本"表，版本信息只出现在正文首句或导航页。建议补齐，以便"设计目标 / 待评审 / 已批准"在每份文档内自证。

## 8. 需求追踪复查结果

除 §5 的 F-05 外，追踪结构本身是完整的：

- 声明的 20 个需求（AI-01…AI-10、NET-01…NET-10）全部在验收或计划中被引用，无悬空需求、无未声明引用。
- 验收场景 V-01…V-14 全部存在且被引用，无引用不存在的场景 ID。
- 验收文档第 4 节引用的任务 ID（A-02/A-03/N-01…N-04/L-01…L-05/E4）全部在实施计划中定义，无悬空任务引用。
- 反向不完整：A-01、A-04、A-05、N-05 未被任何需求追踪（见 F-05）。

## 9. 待维护者决定的事项

以下问题需要用户判断，评审无法代答：

1. **协议/编码 schema 是否引入机器可读规范**（F-01）。这是唯一会改变 E0 工作方式的选择：引入则 E0 少写一套镜像 DTO，但需要先定义规范格式。
2. **身份粒度是否收窄为"所需声明子集"**（F-09）。影响 E3 的迭代成本模型，越早决定越好。
3. **配对评估的方法学选择**（F-10）：要求后端提供公共随机数，还是放弃配对、改用共享种子集合。
4. **候选顺序与句柄稳定性**（F-12）：固定 wire 排序，还是让采样与顺序无关。
5. **E1 首个切片的范围与选择标准**（F-07）。这是 E1 的进入条件，目前无标准。
6. **观察充分性审计是否作为 E2 的前置门**（F-08），以及由谁在何时完成。

## 10. 残余风险与本次未执行项

- 本次未运行 `dotnet build AutoTheSpire.sln`、`dotnet run --project core-the-spire/Core.TheSpire.Tests`、运行时内容加载，也未运行任何 AI/net 代码——两模块无实现，且本报告未改规则、DSL 或注册。因此本报告**不能**作为任何"协议已实现""编码器可运行""后端能力已验证"的证据。
- 未执行：Python 编码器契约检查、C#/Python wire round-trip、模拟适配集成、学习实验、CPU 资源基准、真实适配差分（E0–E4 全部未开始）。
- 本文对 `core-the-spire` 与 `mod-the-spire` 未作任何实现层面的判断。§5 F-02、F-03、F-04 所引用的示例与不变量均出自本设计包自身，不依赖当前内核实现；即使内核后续重新设计，这些结论仍然成立。
- 未验证：本报告对文档间一致性的结论基于 2026-10-01 的工作区状态（含未提交改动）。若设计文档在本报告之后被修改，§5 至 §7 的具体行号与引用需重新核对；`tools/doc-check/` 下两个脚本可重跑以复核链接、JSON 与追踪关系。
- 未宣称：本报告没有评估设计的"学习效果可行性"或"部署成本达标"，这些需要 E2–E4 的证据；也没有判定 §6 各 P2 项的最终取向，只给出可选方案与影响。

## 11. 开工就绪评估（按当前方案能否开工）

判定依据完全采用 [ai-net-implementation-plan.md](../../../design/ai-net-implementation-plan.md) 自己写明的进入条件（第 1 节）与待决事项（第 6 节），不引入本文额外发明的门槛。该计划第 8 行的决策摘要为"E0 可独立开始；后端、学习和真实部署各有独立进入条件；后续目标/预算不阻塞 E0"，本节检验这一自我声明在当前文本下是否成立。

### 11.1 结论

- **E0：可以开工，但还差它自己唯一的批准门**，且建议先做 4 处最小文档修订。技术内容已足以动手，不需要新的产品决策。
- **E1 及之后：现在不能开工**，各有独立的、当前未满足的进入条件（见 11.2）。
- 最简路径：**4 处文档修订（半天到一天）+ 1 次批准 → 即可开工 E0**。不建议跳过修订直接写代码，理由见 11.3。

### 11.2 阶段级 gate 判定

| 阶段 | 计划中的进入条件 | 现状 | 判定 |
| --- | --- | --- | --- |
| E0 | 用户接受协议 v0.1/编码 v0.4 的**首阶段范围、Python 标准库布局、精度配置**（[plan:18](../../../design/ai-net-implementation-plan.md):18；同文:80 要求"A-01 开始前"） | 文档均为 IN_REVIEW；维护者尚未作出该接受。AI spec 第 4 节另列"首阶段实现语言与包布局、数值精度策略"待评审（[spec.md](../../../design/ai-the-spire/spec.md):58） | **差批准门**；技术上就绪，但 4 处契约空洞需先补（11.3） |
| E1 | E0 契约通过；**选定已验证的内核切片**，能产生所需当前公开事实（[plan:19](../../../design/ai-net-implementation-plan.md):19） | E0 未开始；切片选择只有计划第 81 行的"维护者决定"，**无选择标准**；承载切片覆盖的"机制→字段→来源→场景"映射无格式与验收门（F-07） | **不可开工**。前置是 E0 通过 + F-07 产物 |
| E2 | E1 稳定；批准 Training/Reward/Preference/Model/Executor/CapacityProfile 与种子拆分（[plan:20](../../../design/ai-net-implementation-plan.md):20） | 六个 Profile 均未批准；预算与奖励权重按设计留待 L-01/L-03 前决定；配对评估方法学未定（F-10） | **不可开工**，且部分决策被设计有意推迟 |
| E3 | core/mod 对目标范围有行为证据；所需公开结构可表示（[plan:21](../../../design/ai-net-implementation-plan.md):21） | 两者均无证据；"可表示"依赖 F-06 的机检能力。另注：该条以 core/mod 表述，若这两个模块后续重做，本条门槛文字需同步重写 | **不可开工** |
| E4 | 所需公开事实、候选语义、完成信号、控制权已核对；模型通过目标机器预算（[plan:22](../../../design/ai-net-implementation-plan.md):22） | 依赖 E2/E3 与真实适配核对；能力集缺少确定性/重放与暂停能力位（F-15、F-16） | **不可开工** |

计划第 24 行"E0 → E1 → E2；E3 另依赖规则/内容覆盖，E4 另依赖真实适配"的依赖声明与上表一致，未发现阶段依赖矛盾。也就是说，**计划自身的阶段切分是自洽的，"现在能不能开工"退化成一个问题：E0 的进入条件是否已经满足。**

### 11.3 E0 逐项就绪判定（A-01 / A-02 能否照文实现）

E0 的交付是"catalog/snapshot fixtures、Manifest 持久化、校验、数组/批处理、契约检查"（[plan:18](../../../design/ai-net-implementation-plan.md):18），第一阶段任务为 A-01…A-05，且明确"此时只实现 AI 端读取/校验，不提前实现 C# net 包"（[plan:38](../../../design/ai-net-implementation-plan.md):38），因此 E0 结构上自足，不依赖 net 实现。逐项核对后，可照文实现与需实现者自行拍板的部分如下。

**已足够明确、可直接实现（无需修订）**

| 项 | 依据 |
| --- | --- |
| Manifest 组成、字典构建与持久化、索引 0 保留补齐、确定性排序 | [encoder-design.md](../../../design/ai-the-spire/encoder-design.md):38-48 |
| 十进制字符串的解析规则（禁 NaN/Inf、禁指数与逗号、去前导 `+` 与多余零、负零写 `"0"`） | [protocol.md](../../../design/net-the-spire/protocol.md):65 |
| 知识状态与内容状态的枚举与 mask 语义（含 not_applicable 与 padding 的区分） | [protocol.md](../../../design/net-the-spire/protocol.md):54-56；[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):66-68 |
| 张量块、列、dtype、每样本 B 轴补齐布局 | [encoder-design.md](../../../design/ai-the-spire/encoder-design.md):151-166 |
| 最小错误码集与错误对象形状 | [encoder-design.md](../../../design/ai-the-spire/encoder-design.md):174 |
| 环境身份校验方式（快照携带 environment_identity，AI 侧存 dictionary_version 并比对） | [encoder-design.md](../../../design/ai-the-spire/encoder-design.md):40 |
| 版本错配与旧字典加载的行为 | AI-02、V-08；[encoder-design.md](../../../design/ai-the-spire/encoder-design.md):44 |

**需实现者自行拍板、否则会变成事实规范（这就是建议先修订的 4 项）**

1. **`Pile.position` 的基准与基数不变量（F-03）。** A-01 要写 snapshot 数据模型与"严格校验"，而"范围内整数"的基准未定义：真实牌堆下标 `[0, total_count)` 还是 `visible_members` 下标？`total_count`、`position`、`count` 等结构整数是 JSON 整数还是十进制字符串也未写明（[protocol.md](../../../design/net-the-spire/protocol.md):65 只覆盖字段值）。这两点直接决定模型字段类型与 `validate_snapshot` 的断言集合（[plan:34](../../../design/ai-net-implementation-plan.md):34 的完成判据是"离散位置和超限错误完整"）。
2. **无序集合在 wire 上的数组顺序（F-02）。** [encoder-design.md](../../../design/ai-the-spire/encoder-design.md):90 的示例数组恰好等于内部抽牌顺序，而 :137 又要求编码前按引用排序、:99 禁止把下标当位置。A-01 要交付的 fixtures 必须给出确定的样例，实现者只能自己选一种读法；一旦选定并被 A-05 与 V-02/V-03 的"样例侧"证据固化，后续修订要同时改 fixtures、校验器与验收文案。
3. **piles 与声明表的关系（F-04）。** 声明表有 relation/sequence/group 而无 `pile_types`，牌堆仅靠保留声明间接表达（[protocol.md](../../../design/net-the-spire/protocol.md):71）。A-01 的"声明/类型严格校验"因此无法判定"这个目录是否合法地声明了牌堆"，也建不出按牌堆种类区分的字典项。
4. **`visibility=conditional` 的可判定程度（F-06）。** 条件是散文且明确"不要求 AI 执行公开条件 DSL"（[protocol.md](../../../design/net-the-spire/protocol.md):38、:44）。A-01 的声明校验要么只能做结构校验（需显式写出这一限制，并同步收窄 V-06 的断言范围），要么就得引入一套条件判定——后者超出 E0 范围。

**另有 1 项虽属 E1 但会影响 E0 的断言口径：** `scenario_profile` 与 `supported_structures` 无结构（[protocol.md](../../../design/net-the-spire/protocol.md):46）。E0 可以只按字符串透传，但那样 A-05 的"声明真实提供方未验证"就必须写成"能力声明未经结构化校验"。

### 11.4 最小开工清单

按"改动面最小、不需要新决策"排序：

1. 修订 [protocol.md](../../../design/net-the-spire/protocol.md):69 与 [encoder-design.md](../../../design/ai-the-spire/encoder-design.md):99：固定 `position` 为绝对下标 `[0, total_count)`，写出四条基数不变量，并声明结构整数使用 JSON 整数（F-03）。
2. 修订 [encoder-design.md](../../../design/ai-the-spire/encoder-design.md):86-97：把示例数组改为规范排序，并统一示例、:225 与 V-03 三处 fixture（F-02）。
3. 修订 [protocol.md](../../../design/net-the-spire/protocol.md):34-42：增加 `pile_types` 声明表并规定牌堆实体归属（F-04）。
4. 修订 [protocol.md](../../../design/net-the-spire/protocol.md):38、:44：显式写明 E0 阶段 `conditional` 只做结构校验，或给出可判定谓词（F-06）。
5. **维护者批准**：协议 v0.1／编码 v0.4 的首阶段范围、Python 3 标准库与包布局 `ai_the_spire/encoding/`、数值通道精度与 `normalization_tolerance` 的确定方式（[plan:18](../../../design/ai-net-implementation-plan.md):18、:80；[spec.md](../../../design/ai-the-spire/spec.md):58）。批准动作同时应把相关文档状态从 IN_REVIEW 提升为可实施，否则 [docs/README.md](../../../README.md):44 的"DRAFT/IN_REVIEW 不是实现承诺"会让 E0 的证据层级悬空。

完成这 5 项后，A-01→A-05 可以按现文施工；F-09（身份粒度）、F-10（配对评估）、F-11（session_id 域）、F-12（候选顺序）等 P2 项**都不阻塞 E0**，它们分别落在 E2 与 E4 之前，与计划第 6 节的时点一致。

### 11.5 现在可以立即并行、且不受任何未决项阻塞的工作

- 4 项协议/编码修订本身，以及随之更新第 5 节的追踪表（F-05 的两处矛盾行可顺手修正）。
- 把 `tools/doc-check/` 接入一个最小检查入口，兑现 [ai-net-verification.md](../../../design/ai-net-verification.md):72 要求的"每批执行本地链接/锚点、JSON 样例与空白检查"（F-22）。
- F-01 的规范 schema 草案。若采纳机器可读规范，应在 A-01 之前定，因为 A-01 会产出第一份实现；若暂不采纳，应显式写下"A-01 的 Python 模型即当前事实标准"，以免后续出现权威倒置。

### 11.6 不建议现在开工的部分及理由

- **不要现在写双端 codec 的第二端（N-01/C#）。** 规范唯一事实来源未定（F-01），两端各自手写会在联调时才暴露漂移；待 E0 通过、规范形态确定后再开工。
- **不要现在做快照公开投影与可见性过滤（N-02）。** 它是 F-07 映射表的产物而非输入，且 E1 的切片尚未选定，此时实现等于替维护者决定"哪些字段可见"。
- **不要现在选网络结构或填奖励权重（L-01/L-03 前置）。** 预算与奖励按设计属于维护者决定项，计划第 6 节明确其截止时点，提前决定会违反 [ai-net-verification.md](../../../design/ai-net-verification.md):30 的"不允许写达标"约束。

### 11.7 本节证据边界

本节是**文档就绪度判定**，不是构建或运行证据：本次仍未执行 `dotnet build`、规则检查、内容加载或任何 AI/net 代码（两模块无实现）。"E0 可在 11.4 完成后开工"的依据是计划自身的进入条件与本文第 5 至 7 节的静态核对，**不能**读作任何功能已实现或已验证。若维护者选择直接进入 A-01 而不做 11.4 的修订，则 11.3 的 4 项将事实上由实现者定标；本报告建议在该情形下于交接中显式记录这 4 项"由实现决定"，以便 A-05 与 V-02/V-03 的样例侧证据有据可查。
