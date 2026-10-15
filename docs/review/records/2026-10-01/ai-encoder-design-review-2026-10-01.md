# ai-the-spire 编码器设计评审（encoder-design v0.2）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-02、W-04、W-06、W-08，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

日期：2026-10-01。状态：待用户决策，尚未修改任何设计文档。评审对象：[编码器设计](../../../design/ai-the-spire/encoder-design.md)（IN_REVIEW，2026-10-01）；对照规范：[AI 模块规范](../../../design/ai-the-spire/spec.md)、[技术设计主稿](../../../design/ai-the-spire/design-v0.md)、[项目规范](../../../design/spec.md)、[运行态模型](../../../design/core-the-spire/runtime-state-model.md)、[net 模块规范](../../../design/net-the-spire/spec.md)及 `core-the-spire` 现有源码。

文档整理注：仅修正项目规范的相对链接，原评审意见及其审计时点保留；后续处置见[边界判断](ai-encoder-review-disposition-2026-10-01.md)。

评审范围：契约的可实现性、验收的可判定性、与 `core-the-spire` / `net-the-spire` 现状的一致性。评审方式为静态阅读，未运行构建、规则检查或编码器检查。

## 结论摘要

设计方向成立：当前观察自治（不查内核、不回放事件、不读历史）、确定性编码、字典与权重一起版本化、结构可扩展优先于加宽固定输入、不预设网络结构，这些都与模块规范和项目边界一致，应保留。

问题集中在“可以交给实现者”这一步，分三类：

- **A 类——规范与现状冲突**：契约要求的观察内容当前无法产生，而现有观察反而暴露了不该暴露的数据。需要先决策，否则实现者只能在 fixtures 里自圆其说。
- **B 类——契约缺口**：索引稳定性、掩码语义、错误种类、批处理布局等未定义，不同实现者会得出不同结果。
- **C 类——验收措辞**：多条场景的判定标准不可精确执行。

## A 类：与现状的冲突

### A1. `known_top` 目前没有任何来源，`core` 不存在“玩家已知”模型

`encoder-design.md` 第 4 节把“已知顶部前缀”定义为观察提供方的输出，第 9 节用“首次输入即为头槌结算后的观察，顶部 C”做验收。但：

- `core-the-spire/Core.TheSpire` 中检索 `Known|Reveal|Peek|Scry|FaceUp` 只命中 `DslSource.cs` 的词法分析器 `Peek`，没有任何牌区知识状态。
- 抽牌堆是有序列表：`core-the-spire/Core.TheSpire/Legacy/State.cs:126,166,171,176`（`_drawPile` / `DrawPile` / `DrawPileInstances` / `MutableDrawPile`），索引 0 即顶部（`State.cs:878-879`）。
- 头槌置顶只是把实例插到列表首位：`core-the-spire/Core.TheSpire/Legacy/State.cs:744`（`case CardPilePosition.Top: destination.Insert(0, card);`），测试断言 `DrawPileInstances[0]`（`core-the-spire/Core.TheSpire.Tests/CombatSliceTests.cs:4047-4067`）。恢复源码同样只做重排（`reference/sts2-sim/src/Sts2Sim.Core/Models/Cards/Headbutt.cs:31`），`reference/sts2-sim` 中也没有 known-top / revealed-card 概念。

影响：`known_top` 只能在 fixtures 中成立，无法从当前真实后端投影出来。第 9 节把它作为已实现的验收项、第 10 节又把它放在“真实适配前”才决策，会掩盖一项跨模块前置工作。

建议：在第 10 节把它改为已确认阻塞项，写明“需要 `core` 或 `net` 适配器定义牌区可见性模型（哪张牌、从哪个位置起为玩家已知）”，并给出最小字段建议（例如每张牌区的 `known_prefix_length` 或每实例的 `revealed_to_player`）。这不要求首阶段改 `core`，只要求把依赖写实。

### A2. 现有观察直接暴露完整隐藏牌序，与新契约方向相反

- `core-the-spire/Core.TheSpire/Legacy/State.cs:1015-1025`：`CombatObservation` 直接包含完整的 `DrawPile`（有序 `ContentId` 列表）与 `DrawPileInstances`（构造见 `core-the-spire/Core.TheSpire/Legacy/Environment.cs:279-283`）。
- `core-the-spire/Core.TheSpire/Legacy/Runs.cs:296-302`：`RunObservation` 同样暴露完整牌组与可空药水槽。
- 这与既有规范冲突：[项目规范](../../../design/spec.md):64“不能把隐藏牌序等内部数据无意暴露给 AI”，[运行态模型](../../../design/core-the-spire/runtime-state-model.md):47“隐藏牌序、未揭示地图信息和未来随机值不得泄露到训练观察”。

影响：保密方向。编码契约假设观察已经过可见性投影，但当前唯一可被 AI 消费的观察记录恰恰是“全量 + 有序”。`core` 只有封装而非投影（`State.cs:123,291` 为 `internal`，`InternalsVisibleTo` 仅开放给测试）。同时 `net-the-spire` 目录尚不存在（`AutoTheSpire.sln:6-18` 未注册该项目），观察/动作也没有对外序列化契约，只有普通 record。若首阶段只写 fixtures，真实接入时会同时面对“缺已知信息”和“多隐藏信息”两个相反的问题。

建议：在 `encoder-design.md` 第 1 或第 4 节增加一小段“现有观察的已知偏差”，列出 `CombatObservation.DrawPile` / `DrawPileInstances` 必须被替换为（已知前缀 + 无序剩余 + 总数）投影，并把它登记为 `net`/`core` 侧前置任务。首阶段边界不变。

### A3. 候选 ID 与“具名实体引用”在现有动作类型上不存在

- 第 3 节要求 `LegalAction` 携带“当前候选 ID、具名实体引用、具名参数”；第 5 节要求“来源、目标等引用角色必须注册具名字段，不能通过位置约定混淆”。
- 现有动作是位置式的：`core-the-spire/Core.TheSpire/Legacy/State.cs:1036-1042`（`PlayCardAction(int HandIndex, string? TargetId)`、`UsePotionAction(int PotionIndex, …)`、`ChooseOptionAction(int OptionIndex)`），且 `core-the-spire/Core.TheSpire/Legacy/Environment.cs:339-356` 用 `Enumerable.Range(...)` 直接生成序号候选。

影响：候选 ID 由谁分配、是否稳定、重排后是否变化，契约没有规定；而第 9 节的“候选重排”验收正依赖这个定义。

建议：明确候选 ID 为不透明句柄、只在对应快照版本内有效；其数值与顺序都不得作为模型特征（与实例 ID 同规则）；适配器负责把位置式动作翻译为具名引用。这一条与第 6 节“返回 candidate_id + probability，绑定输入快照”是配套的，应合并表述。

## B 类：契约缺口

### B1. 索引稳定性与最小调用面不自洽

第 2 节规定“首次构建各字典时按完整名称确定性排序”“恢复训练时读取原映射，不按新注册顺序重建”，但第 8 节的最小调用面只有 `build_manifest(registrations)`，没有保存/加载入口，上述流程在 API 上不可达。

更关键的是：按名称排序意味着插入一个 `base:xxx` 会让其后全部内容索引整体位移；第 9 节场景 1 只覆盖“同一批类型、注册顺序不同”，测不到“内容集合变化后旧字典对新字典”。

建议：(a) 明确新增内容的索引分配规则（追加下一个空闲索引，或全量重排并强制拒绝旧模型）；(b) 补 `save_manifest` / `load_manifest`（或 `build_manifest(registrations, existing=None)`）；(c) 增加一条验收——“已保存字典 + 新增内容后，旧内容索引不变、新内容获得新索引；若实现选择重排，则加载旧模型必须报版本错误”。

### B2. 内容索引缺少有效性掩码，会与补齐索引 0 混淆

第 3 节允许实体“可选内容类型”，第 2/5 节又把索引 0 保留给补齐且禁止与真实值混淆。但第 5 节 entities 块只有“类型索引、内容索引及实体 mask”：实体 mask 只能说明该行有效，不能说明内容索引已知或适用。

真实观察里普遍存在“无内容 / 不适用”的空值：`core-the-spire/Core.TheSpire/Legacy/State.cs:1024`（`IReadOnlyList<ContentId?> Potions`）、`core-the-spire/Core.TheSpire/Legacy/Runs.cs:297`（同）与 `Runs.cs:291-294`（`RunRoomFrameView` 的 `EncounterId` / `EventId` / `EventPageId` 均可空）。

建议：为内容索引增加独立有效性掩码，或规定“无内容类型的实体不产生行”；并明确空药水槽属于“无实体”还是“无内容类型的实体”。

### B3. 错误种类没有枚举，验收却要求返回种类

第 9 节末尾要求“给出字段路径和错误种类”，第 2 节要求“返回显式错误”，但全文没有错误种类清单，也没有错误对象的最小结构。

建议：给出封闭集合，例如 `unknown_type`、`unregistered_field`、`dangling_reference`、`value_type_mismatch`、`duplicate_member`、`count_mismatch`、`version_mismatch`、`capacity_exceeded`、`non_finite_value`、`empty_candidates_non_terminal`，并规定错误同时带输入路径与相关 ID。

### B4. `collate` 的输出布局未定义

第 5 节只写“按本批实际长度补齐，提供独立 mask”，第 8 节给出 `collate(encoded_samples)`，但没有说明各块的形状与归属：实体/字段行可以拼接，而 candidates 与 sequences/groups 每样本长度不同，是拼成一条扁平候选轴加“样本归属”列，还是每样本一块。第 6 节的评分器契约直接依赖这个决定。

建议：补一张 `collate` 输出表（各块形状、样本归属字段、mask 语义、candidate→sample 映射）。

### B5. 未声明哪些块参与模型输入

第 5 节 `metadata` 含“会话、快照、Manifest 身份与数值变换版本”，`reference_map` 用于关联。会话 ID 若被当作特征即构成泄漏与非平稳输入。

建议：显式标注 `metadata` 与 `reference_map` 为仅用于组装与校验、不参与任何网络输入。

### B6. relations / sequences 的语义位置未定义到可实现程度

第 3 节的 Relation 有“可选语义位置”、KnownSequence 有“公开序位”，第 5 节只写“语义位置及有效 mask”“已知顺位或无序成员计数”。但语义位置是否算字段声明（第 2 节只规定“动作引用角色与参数也按字段声明注册”）、序位从 0 还是 1 起、是否允许空洞，都未规定。

建议：补齐这三项，或把 relations / sequences 的数组布局一并写入第 5 节表格。

## C 类：验收与措辞

### C1. “语义不变”应改为精确相等

第 9 节两条场景（三张相同牌重排、隐藏顺序不同）都写“语义不变 / 模型输入语义相同”，而第 5 节说实体“可以”确定性排序。若排序是规范性的，这两条应当断言两次编码输出逐元素相等；若只是“可以”，则该验收无法判定。建议把第 5 节的“可以确定性排序”改为“必须按 … 规范排序”，并把两条验收改成精确相等断言。同处“实体及无序字段”措辞歧义，宜写“无序集合的成员行”。

### C2. 场景 1“旧版本不被无声重建”不可判定

第 2 节已规定版本不匹配返回显式错误。场景应直接断言“加载旧 `dictionary_version` 时报 `version_mismatch` 且不返回任何字典”，而不是“不被无声重建”这种无法观察的描述。

### C3. 验收场景集中在战斗，缺少整局决策

第 9 节各场景中，待选牌与场景选项只以第 8 节任务 1 的 fixture 形式出现；而规范要求覆盖路线、商店、事件、营火、奖励（[AI 模块规范](../../../design/ai-the-spire/spec.md):28、[技术设计主稿](../../../design/ai-the-spire/design-v0.md):17）。建议至少补一条“路线选择 + 可空/未揭示内容”的编码场景——它正好能暴露 B2 的掩码缺口。

### C4. 候选空间规模未讨论

`PlayCardAction(HandIndex, TargetId)`（`State.cs:1037`）表明“打一张牌并选目标”是一个候选，候选数约为手牌数 × 敌人数。第 7 节只讨论实体/字段规模与首层参数，未提候选轴规模与 softmax 归一化成本。建议在第 7 节登记这一风险（是否需要因子化评分），不必改动首阶段任务。

### C5. 小问题

- 第 4 节示例 `known_top: [C:防御]` 混用了实例标签与内容 ID，fixture 宜统一为 `{instance, content}`。
- 第 5 节建议数值通道用 float64，同时要求 boolean / category / entity_ref 分通道；建议直接写明各通道 dtype，避免 boolean 落入浮点通道后被“非有限值检查”牵连。
- 第 2 节 `encoded = (raw - offset) / scale` 未说明是否保存 `offset`/`scale` 以便复原与排错；建议并入 metadata 的“数值变换版本”。

## 建议的最小修订顺序

1. 第 10 节把“真实后端当前可观测字段”从“真实适配前”提前为已确认阻塞项，写入 A1/A2 两条具体依赖。
2. 第 1 节补“现有观察的已知偏差”（A2），第 3 节补候选 ID 语义（A3）。
3. 第 2/8 节补字典保存/加载入口与新内容索引分配规则（B1）。
4. 第 5 节补内容索引有效掩码、`collate` 布局、模型可见块声明（B2/B4/B5）。
5. 第 9 节把两条“语义不变”改成精确相等，补整局决策场景，并列出错误种类（C1/C2/C3/B3）。

以上均为文档层修订建议，不要求首阶段实现者额外改动 `core`、`base-spire` 或 DSL。

## 本次验证

- 静态阅读：`docs/design/ai-the-spire/{spec,design-v0,encoder-design,literature}.md`、`docs/design/{spec,net-the-spire/spec,core-the-spire/runtime-state-model}.md`，以及 `core-the-spire/Core.TheSpire/{State,Environment,Runs}.cs` 与相关测试。
- 未执行：`dotnet build AutoTheSpire.sln`、`dotnet run --project core-the-spire/Core.TheSpire.Tests`、Python 编码器检查、运行时内容加载、性能基准。本次未改动规则、DSL、内容注册或代码。
- 未执行 `git diff --check`：本次会话的 shell 被沙箱拒绝（`SetNamedSecurityInfoW failed (Win32 5)`），无法运行 `pwsh`。本报告为新增文件，未改动既有文件。
