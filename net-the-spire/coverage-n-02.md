# N-02 / H-01 / H-02 覆盖表：机制 → 字段 → 来源 → 检查 → 证据

> 本表最初由监督者补写（N-02 执行 agent 三次中断），**H-01/H-02 收口后由本轮执行者按当前实现重写**。
> 只声明**已由可执行检查覆盖**的部分；未覆盖与能力边界在 §2/§3 显式列出。
> 检查入口：`dotnet run --project net-the-spire/Net.TheSpire.Tests` → **410 executed / 410 passed / 0 failed**
> （2026-10-08 复测；其中 18 条为 N-02/H-01/H-02/W-01 映射检查，另 373 条为 N-01 跨端 wire 对照，
> 8 条为本轮新增的规则声明扩展例检查 E-01…E-08，见 §4 与 [text-rule-packages.md](text-rule-packages.md)）。
> W-02 修复后的复核：2026-10-03（公开引用改为会话/观看者内的持久绑定，三个跨状态检查 + 故障注入证据）。
> W-01 作者变体复核：2026-10-03（药水 kind/枚举、药水栏 group + 容量、UsePotion、Run 与长期牌组、
> describe 限额一致性；真实 stdio 见 `python net-the-spire/client/test_author_content.py` → rc=0，
> 终局 + 离线重编码见 `python ai-the-spire/tests/j01_episode.py --only author`）。
> V 编号含义见 [ai-net-verification.md](../docs/design/ai-net-verification.md)；格式遵循
> [contract-artifacts.md](../docs/design/net-the-spire/contract-artifacts.md) 第 5 节。

## 1. 逐机制覆盖

| 协议机制/块 | 引擎来源（唯一事实来源） | 检查名（`Net.TheSpire.Tests`） | 证据 | V 标注 |
| --- | --- | --- | --- | --- |
| `session_status` | `SessionStatus` → `PublicSnapshotProjector.MapSessionStatus` | `N-02 引擎会话状态映射到协议 session_status，并给出是否发布快照` | 四态映射断言：Running/AwaitingDecision→`active`（发布快照）、Terminal→`terminal`、Faulted→`faulted`（**不发布快照**） | V-04 |
| `observation_id` | `ParticipantView.StateVersion` | `N-02 活跃会话投影为 active + status=decision，且 observation_id 绑定稳定版本`；`N-02 observation_id 在同一稳定版本上可重复（纯读，不改状态）` | `ObservationId == session.StateVersion`；连续两次 `Observe`+`Project` 得同一 id 且状态版本不变（observe 纯读） | V-04 |
| `entities` | `ParticipantView.Instances` + `PublicVocabulary` 的 kind/字段映射 | 同上（`Entities.Count > 1`）；W-02：`W-02 跨状态持久绑定：同定义实例的公开字段变化后句柄不换绑`、`W-02 生成/销毁：新实例取新句柄、销毁句柄不复用、候选引用同一批持久句柄`、`W-02 撤销再授权：同一实例重新揭示后仍是同一公开句柄` | 玩家、敌人、手牌、**被授权揭示的卡牌**、**牌区实体**都被发布；实例 id 来自 `ProjectedSnapshot.InstanceRefs`（**会话/观看者内的持久绑定**：首次公开分配、跨状态保留、销毁不复用，见 §2 末两行） | V-15 / **W-02** |
| `relations` | `ParticipantView.Relations`（`Owner`/`Targets`，**已按可识别实例过滤**） | `N-02 切片不声明 sequences/groups，输出空数组且成员引用闭合` | 成员/引用闭包断言：`visible_members ⊆ entities`、无重复。引擎侧过滤规则另由 `H-01 references and relation edges only name identifiable instances` 覆盖 | V-15 |
| `piles`（**H-01/H-02 收口**） | `ParticipantView.Collections` + `PublicSnapshotProfile.Collections` 处置 + 牌区实体 | `H-02 目录声明 pile_types 且 pile_ref 唯一匹配牌堆种类`；`N-02 隐藏牌堆只暴露真实总数、不暴露成员（V-02）` | 五个牌区各发布一个 pile；`pile_ref` 解析到**实体**且其 `kind_id` 唯一匹配一个已声明 `pile_type`；隐藏集合的未授权成员**不得**出现在任何 `visible_members`；未发布集合逐条登记（`SkippedPublicFact` 带规则号 + 理由） | **V-02** / V-15 |
| `known_positions`（**H-01 收口**） | 包声明的 `KnownCard{Viewer,Card,Pile,Position}` 事实 + 引擎观看者授权投影 | `H-01/H-02 已知位置经真实实例引用进入 wire，并满足 I-27/I-29` | 头槌置顶后：`card_ref` 是**真实卡牌实例**（非内容 ID）、`position=0`、`card_ref ∈ visible_members`、`0 ≤ position < total_count`、`members_complete=false`、被引用卡牌是快照内实体、`visible_members.Count < total_count`（授权揭示 ≠ 公开整个牌堆） | **V-02** / V-03 / V-15 |
| `sequences` / `groups` | 同上（`CollectionDisposition.Sequence`/`Group`） | `N-02 切片不声明 sequences/groups，输出空数组且成员引用闭合` | 切片未声明 → **必须输出空数组**（非 `null`、不省略键） | V-15 |
| 候选（活跃态） | `Session.LegalActions` → `BuildLegalActions` | `N-02 候选严格按公开语义键的 UTF-8 字节序排列`；`N-02 candidate_id 只作 step 引用、不进入任何特征` | ① 逐对断言 `CompareByUtf8Bytes(prev, next) < 0`（严格递增＝相同键会被拒绝）；② 候选与绑定 1:1；③ 任何字段名不得是 `candidate_id`，任何 `WireString` 字段值不得等于候选句柄 | **V-05** |
| 候选（暂停态） | `Session.PendingDecision.Candidates` → `BuildDecisionAnswers` | `N-02 暂停期投影为 active + status=decision，候选恰为 PendingDecision.Candidates` | 出牌触发目标选择后：`SessionStatus.AwaitingDecision` → 协议 **`active`** + 快照 `status=decision`；候选引用的公开实例 id **恰为** `PendingDecision.Candidates` | V-04 |
| `terminal_result` | `Session.Terminal` → `BuildTerminal` | `N-02 终局/故障/空候选的边界组合被显式拒绝` | `terminal` 会话缺终局结果 → `N-02-SESSION-STATUS`；`AwaitingDecision` 缺 `DecisionRequest` → `N-02-DECISION-STATE`；`decision` 快照空候选 → `TODO-N02-EMPTY-CANDIDATES`（不得发布空候选快照） | V-04 |
| 目录 `pile_types` / `entity_ref` 目标 / 必需性（**H-02 收口**） | `PublicSnapshotProfile.PileTypes`、`PublishedField.TargetKindId`、`PublishedField.Required`、`PublicActionField.Required` | `H-02 entity_ref 目标种类与必需字段来自显式声明`；`N-03 get_catalog 返回可解析的 exact 目录，身份与会话一致` | `PlayCard.Card` 的 `target_kind_ids` 恰为 `[net:kind:base-spire:Card]`（来自 profile 声明，非"全集过近似"）；该字段进 `required_fields`（protocol.md:98）；每个终局原因的必需事实字段来自声明而不是一律可选；目录含 5 个 `pile_type` 且成员种类非空 | V-08 |
| 不发布的公开事实 | `SkippedPublicFact(EngineQualifiedId, RuleId, Reason)` | `N-02 隐藏牌堆只暴露真实总数、不暴露成员（V-02）` | "没有静默丢弃"可被检查：跳过项必须带规则号与理由。当前显式跳过项：`PlayedThisTurn`（无公开序语义）、`Monsters`（非牌堆/无 owner 实体）、`Known`（事实已逐个作为实体发布）、`KnownReveal`（授权边，公开表达为 `known_positions`） | **V-15** |
| 观看者授权揭示（**H-01 新增**） | `ProjectionSymbol.Reveal` + 包声明的 viewer 字段 | 引擎侧：`H-01 the revealed subset of a hidden area is per viewer`、`H-01 a reveal never widens field permissions`、`H-01 duplicate same-definition instances stay distinct`、`H-01 a hidden-area permutation leaves the revealed subset and its order unchanged`、`H-01 revoking a fact revokes the identity`、`H-01 a fact states the known position and the engine never recomputes it`、`H-01 a projection without a reveal keeps hiding the area`、`H-01 references and relation edges only name identifiable instances`、`H-01 the reveal declaration is part of the rule set identity` + 4 条链接期拒绝 | 13 条检查（引擎 63/63 的 H-01 部分）：按观看者授权、字段权限不放宽、同定义重复实例无混淆、隐藏置换不改公开结果、撤销即失效、位置由包维护且引擎不重算、无授权时默认隐藏不变、引用/关系边只指向可识别实例、声明进入规则身份；4 条链接期拒绝（授权类型不在投影内 / 无观看者字段 / 观看者字段非 participant / 目标类型不在投影内） | **V-02** / V-05 / V-15 |
| **W-01 作者变体的药水与整局事实**（`--author-content`） | 作者文本 `potion`/`potions{}`/`run{}`/`deck{}` → `CombatSlicePackage` 的 `Potion` 实体 + `PotionBelt` 集合（`Capacity = SliceManifest.PotionSlotCount`）+ `UsePotion` 动作 + `Run` 实体 + `LongTermDeck` 集合；公开侧 `SlicePublicProfile.CreateAuthorContent(rules)` | `W-01 作者变体发布药水 kind/枚举、药水栏（group+容量）与 UsePotion`（net）；`W-01 药水栏槽位：容量 = SliceManifest.PotionSlotCount，越界由引擎拒绝 [真实]`（切片）；`python net-the-spire/client/test_author_content.py`（真实 stdio，rc=0） | 药水实体（content_id + Rarity/Target 的 `net:cat:*` + Amount/PowerStacks）、药水栏**发布为 group**（owner = 玩家的唯一实例、成员 = 药水、`max_count` = 引擎声明的容量 3、`min_count` = 0）、`UsePotion` 候选恰为药水栏成员且绑定指向真实实例；整局 `Run` 实体发布 Hp/MaxHp/Gold（作者文本 `run{}`），长期牌组发布为 group（owner = Run、成员 = 长期那批卡牌实例、与战斗牌区**互不相交**）；并断言**药水真的能被操作**：提交 → 目标提问 → 离开药水栏（消耗）→ 敌人获得声明的 Shrink(4)。容量越界由引擎按 `CapacityExceeded` 拒绝（不静默丢弃） | **W-01** |

## 2. 能力边界（**不是 M1 阻塞，但必须声明**）

| 边界 | 事实与证据 | 影响 |
| --- | --- | --- |
| ~~引擎无牌堆实体~~ **已收口** | base-spire 为每个牌区声明独立实体种类（`HandPile`/`DrawPileEntity`/`DiscardPileEntity`/`ExhaustPileEntity`/`InPlayPileEntity`），net 按 `kind_id` 唯一解析 | 五个牌区都发布为 pile；`TODO-N02-PILE-ENTITY` / `TODO-N03-CATALOG-PILES` 关闭 |
| ~~已知位置无法表达~~ **已收口** | `KnownCard` 改为引用真实卡牌实例 + 观看者字段；引擎观看者授权投影公开该实例；net 填 `known_positions` | `TODO-N02-KNOWN-POSITION` 关闭；I-29 子集关系成立 |
| ~~**无 `Sequence`/`Group` 声明**~~ **部分收口（W-01）** | 引擎仍无 `SequenceSymbol`/`GroupSymbol`：`PackageBuilder` 只有 `Collection(...)`。**Group 现在可以从集合发布**：`CollectionDisposition.AsGroup(PublishedGroup)` + owner **按种类**唯一解析 + `min_count`/`max_count` 来自引擎声明的集合容量；`CatalogProjector` 发射对应 `group_types`。Sequence 仍不可表达（没有"公开有序流程"的语义来源） | 需要"公开有序流程"的场景**当前无法表达**（本切片 `sequence_types` 为空）；"有 owner 的分组"已可表达（药水栏即首个用例）。引擎集合成员唯一 ⇒ `multiplicity` 恒为 1，**带重数的分组仍无法表达** |
| **`Group.multiplicity`** | 集合成员唯一，协议 `Group` 允许重数 | 一成员多次出现的分组**无法表达**（本切片不适用） |
| **pile 的 `exclusive_group_id` 未声明** | 引擎的战斗牌区共享内部 `MembershipGroup="pile"`，但协议要求互斥组带 `membership_scope_field_id`（必须是牌堆实体上引用作用域实体的字段），而战斗作用域**不是实体** | 目录里五个 `pile_type` 的 `exclusive_group_id`/`membership_scope_field_id` 均为 `null`：**不把内部互斥组冒充协议互斥组**；消费者由此看不到牌区互斥约束 |
| **`observation_id` 是映射而非引擎概念** | 引擎 `Engine/*.cs` 无 `observation_id`；投影器取 `StateVersion` | 语义为"稳定状态版本"，与协议的"乐观并发绑定"一致；**不是**引擎原生身份 |
| ~~**公开实例 ID 直接使用内部创建计数器**~~ **已收口（W-02）** | 公开句柄改由 `PublicSnapshotProjector` 的**会话/观看者内持久绑定表**给出（`ProjectedSnapshot.InstanceRefs`）：首次公开分配、跨状态保留、销毁不复用；同一批新实例之间的编号顺序只由 `PublicIdentityKey` 决定，它只读**已发布**字段、且引用按对端的公开身份表达（既不读 Skip 字段，也不用 `Value.ToString()` 的原始 ref）。`PublicInstanceId` 保留为诊断函数，**不再是公开句柄** | 旧行为（每份快照按公开字段重排编号）会让"受伤/升级/生成销毁/移动牌区"改变同一实例的句柄，并让新增的 KnownCard 事实推动其后所有实例的编号；三条新检查 + 故障注入（恢复逐快照重排 ⇒ 三条全部按预期失败）证明修复有鉴别力 |
| **残余边界：公开身份完全相同的同定义实例** | 这类实例（例如起手牌里的 5 张打击）之间的编号顺序只能由投影顺序（稳定排序）决定 | 任意置换给出同一公开结构，因此不构成可推断泄漏；一旦出现"公开字段完全相同、隐藏字段不同"的内容，需要重新评估绑定顺序的公开可推断性 |

## 3. 未覆盖（明确不属于本层或尚缺证据）

1. **并发/去重/账本**（V-04 的 busy/stale 部分、**V-07**）：属 **N-03**（已交付；`session_busy` 在同步引擎下不可达，见 N-03 报告）。
2. **端到端 stdio 与超时/保留**（**V-14** 相关）：属 **N-04 / H-03**（已交付）。N-04 覆盖帧上限、畸形帧与
   `request_status` 三态；**H-03 补齐**：延迟管道注入（响应晚于客户端超时到达 → 按 `request_id` 归位，
   见 [h-03-2026-10-02.md](../docs/review/records/2026-10-02/h-03-2026-10-02.md)）、宿主断开（`OutcomeUnknown`，不编造结果）、
   跨进程会话句柄不复用、以及 `pending`/`outcome_unknown` 的客户端处理（脚本化测试后端，标为故障注入）。
   仍未覆盖：**真实异步提供方的在飞超时**（本同步后端不产生可观察的 `pending`）。
3. **真实模拟证据与逐场景证据类别标注**（V-01~V-10 + V-15 的"真实"档）：属 **N-05**；本表只提供**夹具/单元层**证据。**本表的 V 标注不等于 V-15 已成立**：V-15 还要求"决策相关事实不同的成对状态"在真实模拟中逐对保留差异。
4. **隐藏顺序/RNG 游标不变的完整 V-02**：本表覆盖"隐藏成员不泄漏"与"隐藏置换不改公开结果"两个可判别部分；完整 V-02（改内部牌序/RNG 游标/未揭示内容后公开特征逐字段相同）需在 N-05 用真实模拟场景证明。
5. **能力项 `internal_snapshot_restore`**：首版可为 `false`（评审已裁定不需要 core 快照），未在本层声明。
6. ~~**公开实例 ID 的创建顺序泄漏风险**~~ **已收口（W-02）**：见 §2 末两行。检查证据：`W-02 只对调不可见创建顺序的成对场景：公开快照逐字节相同`（成对场景）、`W-02 重复同定义实例各有互不相同的公开引用，且重复投影保持同一绑定`、`W-02 跨状态持久绑定…`、`W-02 生成/销毁…`、`W-02 撤销再授权…`；故障注入（把句柄退回"每份快照按公开字段重排"）让后三条**按预期失败**，注入已还原。仍然开放：§3.4 的完整 V-02（隐藏顺序/RNG 游标的真实模拟对照）与 §3.3 的逐场景证据档。

## 4. 规则声明扩展例（`--scenario text-rules`，ENV-T03 声明扩展子包）

第 4 项的落点在这里：**只改规则包文本**（新增一个公开字段与一条合法 Action）即可让目录/观察/候选/身份随之变化，
net 生产源码不加逐项注册行。实现与用法见 [text-rule-packages.md](text-rule-packages.md)，
规则包测试资源见 [fixtures/declaration-extension/](fixtures/declaration-extension/README.md)。

- 检查：`E-01`…`E-08`（同一提供方跑 baseline/extended、真实轨迹、身份随声明变化且 seed 不进身份、
  已配置未 Start 的独立会话、候选闭合与内部 procedure 不发布、过期候选/错身份拒绝与重发幂等、
  未支持结构显式报错、每类真实响应帧通过 0.2 schema）。
- 真实 stdio：`python net-the-spire/client/test_declaration_extension.py` → **157 passed / 0 failed**（每帧同一份 schema 校验）。
- 本场景**不**声称代表完整 base 原版；多选/枚举决策形状、集合成员发布仍在 §3 的边界内。
