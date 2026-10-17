# 冲突：已知牌位置与 net 协议 V-06 不变量不兼容

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01发现，2026-10-02实现；2026-10-03负责人按H/N/J报告与检查代码关闭已知位置冲突并归档；附带公开ID风险转W-02保持开放 |
| 发现途径 | 监督 N-02 前置时按 B-04 交付物与 `protocol.md` 逐条对照；不是运行期故障 |
| 影响范围 | N-02（PublicSnapshot 映射）、base-spire 切片（B-04 公开事实）、可能的 K-06 契约扩展 |
| 责任方 | 协议/net 侧：`protocol.md` 与 `contract-artifacts.md` 的所有者；引擎侧：K-06 契约（属评审批准范围） |
| 复现方式 | 读下列三处即可复现，无需运行任何代码 |

## 1. 现象

协议要求"被标记位置的牌必须同时是该牌堆的可见成员"：

- `docs/design/net-the-spire/protocol.md:89`：**`known_positions` 的 `card_ref` 是 `visible_members` 的子集**；每张牌至多一个位置，每个位置至多一张牌。
- 同文件 `:88`：`visible_members` 唯一、**引用闭合**、成员 kind 属于该 pile_type。
- 同文件 `:75`：`entity_ref` 是"**当前快照 instance_id 字符串**"。
- 同文件 `:83`：`Pile` 的成员是"无隐藏顺序的**卡牌实例引用**"。

而当前实现两侧正好相反：

- 引擎侧：`core-the-spire/Core.TheSpire/Engine/Projection.cs` 的 `IsIdentityHidden` —— 只要某实例是**任一** `IdentityVisible=false` 集合的成员，就**整体不进投影**（这条规则有回归检查 `K-06 projections hide private, hidden, unordered and identity-hidden data` 保护"不透传内部牌序"，不能直接删）。
- 切片侧：`mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs` 把 `DrawPile.IdentityVisible=false`、`OrderVisible=false`；已知顶牌改用**独立公开事实实体** `KnownCard{CardId, Pile, Position}` 表达（放在公开集合 `Known` 里，位置用 `CollectionIndex(drawPile, card)` 现算）。

**后果**：N-02 拿不到既能作 `card_ref`（要实例引用）又满足 `⊆ visible_members`（要属于该牌堆的可见成员）的对象。切片给的是**定义 ID**（`base-spire:strike-ironclad`）加位置，不是实例引用；而那张实例被引擎隐藏，无法出现在快照的 `entities` 里。于是 B-04 的验收项"第一份输入就能表示置顶后位置 0"**无法经协议原生结构表达**。

## 2. 为什么现有的"包侧绕过"行不通（关键细节）

看似可行的两种包侧做法都被挡住：

1. **把已知牌放进第二个公开集合**（如新增无成员组的 `Revealed`）：`Hand/DrawPile/DiscardPile/ExhaustPile/InPlay` 共享成员组 `"pile"`，而 `PlayedThisTurn`、`Known` 无成员组——所以"同一实例同时在 DrawPile 与一个无组集合"在存储层**是允许的**（K-02 的互斥只作用于同组）。**但**引擎的 `IsIdentityHidden` 是"只要在任一身份隐藏集合里就隐藏"，该实例仍会被整体排除，公开集合的成员身份救不了它。
2. **给切片加 `Revealed` 布尔字段让投影放行**：引擎目前**没有**"逐实例可见性覆盖"能力——投影只有集合级的 `IdentityVisible` 开关，包声明不了"哪个字段意味着这张牌已对玩家揭示"。

## 3. 可选方案与代价

| 方案 | 内容 | 代价 / 前置 |
| --- | --- | --- |
| **A. 引擎加"逐成员可见性"**（我建议） | 投影在集合 `IdentityVisible=false` 时不再一律隐藏，而是暴露**该观看者可见的成员子集**；可见性判据由包声明（例如 `TypeProjection` 增加一个 `RevealedFieldName`，指向包维护的 Bool 字段）。切片把 `KnownCard` 换成/补上该标记，N-02 即可同时填 `visible_members` 与该位置 | 属 **K-06 契约扩展**，需评审批准；引擎改动小（投影一处 + Schema 一个字段 + 检查器/测试）；需保留"未标记成员仍不外泄"的回归检查 |
| **B. 协议放宽不变量 3** | 允许 `known_positions.card_ref` 指向不在 `visible_members` 中的实例（或改为按"已知事实"另设结构） | 与 `protocol.md:89` 的既有明文冲突，需协议所有者改文档 + N-01 两端 codec 同步；且会削弱"位置不增加卡牌数"的一致性论证（`:90`） |
| **C. N-02 自造映射** | 由 N-02 把 `KnownCard` 映射成某种快照内实体再借位表达 | **不推荐**：等于单方发明协议形状，违反"共同规范只有一份"与"没有领域事实就向 base-spire 提具体请求" |

## 4. 建议与判据

- **建议选 A**：它把"玩家确实知道这张牌"建模为**领域事实**（包声明），既不违背"不透传内部牌序"（未揭示成员仍隐藏），也让 V-06 不变量 3 天然成立（被标记的牌就是可见成员）。
- 若选 A，需明确两件事：① 可见性判据是"包声明的字段"还是"包把它放进某个公开集合"（前者更显式，后者复用现有集合语义）；② 未揭示成员必须继续不进 `visible_members`，且 `members_complete=false`。
- 若选 B，需同时给出"位置记录不增加卡牌数"（`:90`）在新形状下的等价论证。
- 无论选哪个，**N-02 不得自行发明形状**；本报告是它的前置裁定输入。

## 5. 相关检查与证据位置

- 引擎隐藏规则的回归检查：`core-the-spire/Core.TheSpire.Engine.Tests/EngineChecks.cs` 的 `K-06 projections hide private, hidden, unordered and identity-hidden data`。
- 切片的已知事实实现与检查：`CombatSlicePackage.cs` 的 `RecordKnownTop` / `RevokeKnown` / `ClearKnownCards`；`BaseSpire.Slice.Tests` 的 `B-04 the first snapshot already states a known absolute position`、`B-04 a hidden pile leaks neither its members nor its order`、`B-04 drawing a known card revokes its position`、`B-04 a reshuffle voids every known position`。
- 监督侧台账：[supervision-ledger-2026-10-01.md](../records/2026-10-01/supervision-ledger-2026-10-01.md) §6。

## 6. 2026-10-02架构处置与下一步

采用A的成员子集方向，保留协议实例引用与 `known_positions.card_ref ⊆ visible_members ⊆ entities`。具体契约已写入[通用引擎观察设计](../../design/core-the-spire/generic-tabletop-engine-design.md#8-观察恢复与训练入口)，执行任务为[开工清单H-01/H-02](../../design/implementation-kickoff.md#122-首批必须收口的任务)。A中举例的全局Revealed布尔字段不能单独作为通用授权设计：授权须针对观看者，并保持字段权限。

base-spire维护真实实例引用、牌堆和当前可知位置，抽牌/移动/洗牌时更新或撤销；net映射已授权事实，不造牌、不放宽协议。core提供通用授权投影能力，不内置头槌/牌堆规则；AI消费当前快照，不另记历史。

关闭需要Engine、切片、net与实际stdio编码链路的证据，覆盖重复同定义牌、两观看者、部分可见成员、多个离散位置、知识失效和隐藏部分置换。补设计并未解决运行时缺口；本报告保留tofix，完成H-01/H-02及相应联调后再按关闭证据归档。上文描述的是发现时实现，不以历史行号或检查数量代替当前复验。

## 7. 2026-10-02 实现与验证状态（H-01/H-02）

**已实现**（详见 [h-01-h-02-2026-10-02.md](../records/2026-10-02/h-01-h-02-2026-10-02.md)）：

- **引擎**：`ProjectionSymbol.Reveal(authorityType, relation)`；授权判据是包声明的 participant 观看者字段（非全局布尔）；`ProjectedCollection.RevealedMembers` 与完整成员分离（`IdentityVisible=false` 时 `Members` 仍为空，原 V-02 防泄漏断言不变）；只放开身份隐藏不放宽字段权限；授权子集按实例身份升序；引用型字段与关系边在目标不可识别时不发布；声明进入规则身份；4 条链接期拒绝。
- **切片**：`KnownCard{Viewer, Card(ref→真实实例), Pile, Position}` + 关系 `KnownReveal`；清除/撤销事实时同时清除授权边；每个牌区声明独立实体种类供 `pile_ref` 唯一匹配。
- **net**：`known_positions` 由事实实体填入并逐条校验 I-27/I-29；`visible_members` = 完整成员 ∪ 授权揭示成员；`pile_types` 导出且 `kind_id` 唯一；`KnownReveal` 显式不发布（授权边不是游戏事实）。

**本轮实跑**：build 0/0；Engine **63/63**（+13）；切片 **25/25**；旧后端 **190/190**（不回归）；net **365/365**（+3）；client **38/38**（+2）；契约 210/210；AI 503/503；doc-check 0。

**仍未关闭**：① J-01 的**实际 stdio 链路**尚未断言 `known_positions`（本轮 stdio 客户端 38 项通过，但未覆盖该字段）；② 公开实例 ID 仍直接使用内部创建计数器（`design.md:61` 的映射要求，已登记待裁定）。因此本报告**保留 tofix**，不归档。

## 8. 2026-10-03负责人关闭记录

第7节描述H-01/H-02执行当时的缺口。后续[N-05](../records/2026-10-02/n-05-2026-10-02.md)处理知识撤销与引用闭合，[J-01](../records/2026-10-02/j-01-m1-2026-10-02.md)真实stdio headbutt组的15项检查包含：结算后第一份快照位置0指向实际所选实例，该实例属于entities和visible_members，并直接编码出引用/位置及真实mask。对应检查代码位于 `ai-the-spire/tests/j01_episode.py`，与报告有限范围一致。

负责人据H-01的观看者/撤销等检查报告及上述实际链路证据关闭“已知牌位置无法通过现协议表达”的缺陷，保持既有协议不变量；本轮未重新运行实现检查。附带的“公开实例ID透传内部序号”是另一个未解决问题，明确转入[整体评审第2节](../records/2026-10-03/project-readiness-review-2026-10-03.md#2-需要负责人关注的全局缺口)和[W-02](../../design/implementation-kickoff.md#193-可连续执行的工作包)。归档不表示投影整体无泄漏或全部接口已通过。
