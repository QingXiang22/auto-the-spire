---
task_id: ENV-T04
dispatch_id: ENV-T04-source-initialization-manifest-20261008
session_id: session-e17943ba-7805-421d-884d-9bffc2574240
dispatch_record: .audit/dsh-env-t04-source-init-manifest-2026-10-08/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-08T20:08:00+08:00
---

# ENV-T04 全解锁源建局输入 manifest 与参考逐项差异（B 线子包）：ready_for_review — 2026-10-08

一句话（读者只需这一句）：**冻结源 `sts2-frozen-2026-10-05`（core 锚点 `92ccd34`）在 `UnlockPolicy.All`/Ironclad/单人/dev 下的建局输入已被静态取成 manifest，并逐项对照参考**：共享遗物袋 118 项/6 桶→洗牌 **112** 次、角色袋 122 项/4 桶→**118** 次、Act0 有效事件池 31→**30** 次、普通遭遇候选 **16**（弱 4 + 常规 12）、精英 3、BOSS 3、Act0 古代人 **1**（`Neow`）；10 类清单中 9 类与参考**逐项一致**，唯一实质源/参考内容差是 **2 条 `EncounterTag`**（参考枚举独有 `Jaxfruit`）。这不改变原 case 仍 `diverged` 的事实，也不等于初始化已实现。

- 主清单入口：[ENV-T04](../../tofix/README.md#env-t04)
- B 角色/合同：[B 严格对拍与结算](../../../design/agent-goals.md#4-b严格对拍与结算-agent)、[B 完整交付合同](../../../design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)
- 实现约束与裁决条款：[`§10 参考差异与失败处理`](../../../design/environment-implementation-constraints.md#10-参考差异与失败处理)
- 工具入口：[tools/source-manifest/README.md](../../../../tools/source-manifest/README.md)
- 产物：`.audit/dsh-env-t04-source-init-manifest-2026-10-08/source-initialization-manifest.json`（机器可读，含身份绑定）与同目录 `source-initialization-manifest.md`（人读版）
- 前驱：[初始化首差定位与窄校准证据纠正](dsh-env-t04-init-diagnosis-handoff-2026-10-08.md)（原 tofix 报告已并入 records）

## 接手先做什么

1. 复现取证（仓库根目录，只读，不写 obj/bin）：
   `python tools/source-manifest/extract_source_manifest.py --out .audit/dsh-env-t04-source-init-manifest-2026-10-08/source-initialization-manifest.json --markdown .audit/dsh-env-t04-source-init-manifest-2026-10-08/source-initialization-manifest.md` → 本包实测 **rc=0**。
2. 打开 manifest JSON 读三处：`identity`（源/参考版本与文件摘要）、`relic_pools`（两袋分桶与 `Σ(n−1)`）、`source_vs_reference`（10 类差异判定）。
3. 按下面「§10 窄裁决候选」把 4 条交负责人；**不要**直接照参考池数量实现 base 初始化，也不要照抄诊断副本的重放模型。
4. A 的下一包按 [RunManager.cs:447/450](../../../../sts2/src/Core/Runs/RunManager.cs) 的真实调用顺序实现**前置消费**（共享袋 + 每玩家袋 → 共享先古分配 → 各 act 事件洗牌），再谈普通槽抽取；`rng-initial-shuffle` 仍以 `diverged` 结算。

## 已交付能力与文件

| 交付物 | 路径 | 说明 |
| --- | --- | --- |
| 静态取证工具 | [tools/source-manifest/extract_source_manifest.py](../../../../tools/source-manifest/extract_source_manifest.py) | Python 3，只读源与参考；含 `unsupported_parse_gap` 守卫，绝不把"一侧没解析出来"读成相同 |
| 工具文档 | [tools/source-manifest/README.md](../../../../tools/source-manifest/README.md) | 用法、提取内容、已知差异、边界 |
| 机器可读 manifest | `.audit/dsh-env-t04-source-init-manifest-2026-10-08/source-initialization-manifest.json` | 12 类小节 + `identity` 身份绑定 |
| 人读 manifest | `.audit/dsh-env-t04-source-init-manifest-2026-10-08/source-initialization-manifest.md` | 池清单、消费计算、差异渲染、身份绑定 |

本包**只写** `tools/source-manifest/`、本包 `.audit/` 与本交接；未改 `base`/`net`/`core`/`reference`/`sts2`/`solution`/主清单，未写 obj/bin，未提交。

## 关键结论

### 1. 建局前置消费的源侧静态账（All 解锁、单人、单幕模型）

| 阶段 | 源文件·定位 | 池规模 | 洗牌消费 `Σ(桶内项数−1)` |
| --- | --- | --- | --- |
| 共享遗物袋 | [`RunManager.cs:447`](../../../../sts2/src/Core/Runs/RunManager.cs) → [`RelicGrabBag.cs:94-114`](../../../../sts2/src/Core/Runs/RelicGrabBag.cs) | 118 项 / 6 桶（Uncommon 30、Common 25、Rare 35、Shop 25、Event 1、Ancient 2） | **112** |
| 角色遗物袋（Ironclad） | [`RunManager.cs:450`](../../../../sts2/src/Core/Runs/RunManager.cs) → [`Player.cs:363-369`](../../../../sts2/src/Core/Entities/Players/Player.cs) | 122 项 / 4 桶（Uncommon 32、Common 26、Rare 38、Shop 26） | **118** |
| 共享先古分配 | [`RunManager.cs:670-677`](../../../../sts2/src/Core/Runs/RunManager.cs) | `SharedAncients = [Darv]` 1 项；`Acts.Skip(1)` 在单幕模型下循环为空 | 0 |
| Act0 事件池 | [`ActModel.cs:334-347`](../../../../sts2/src/Core/Models/ActModel.cs) | 13（本幕）+ 18（共享）= **31** | **30** |

`112 + 118 + 30 = 260`，与负责人诊断实测**"参考第一个普通槽抽取发生在 `up_front` 计数 260 处"**逐字对齐（前驱交接：`295 − 18 = 277 = 230 + 30 + 17 + 1 + 1 + 0 − 2`，其中 `230 = 112 + 118`）。⇒ 前置消费总量已有**源侧独立证据**，不再只依赖参考分解。

### 2. 池成员、顺序、tag、rarity：9 类与参考逐项一致

`events(act0+shared)`、`shared_events`、`ancients(act0+shared)`、`act0_ancients_only`、`weak_encounters`、`regular_encounters`、`elite_encounters`、`boss_encounters`、`relic_rarity` 全部 `identical`（逐项比对含顺序）。普通遭遇 16 候选 = 源弱 4（`FUZZY_WURM_CRAWLER_WEAK`、`NIBBITS_WEAK`、`SHRINKER_BEETLE_WEAK`、`SLIMES_WEAK`）+ 常规 12；精英 3（`BYGONE_EFFIGY_ELITE`、`BYRDONIS_ELITE`、`PHROG_PARASITE_ELITE`）；BOSS 3（`CEREMONIAL_BEAST_BOSS`、`THE_KIN_BOSS`、`VANTOM_BOSS`）。**"参考 31 = 源 31"已按 ID 与顺序证明，不是凭 count 相同。**

### 3. 唯一实质源/参考内容差：2 条 `EncounterTag`

- 源 `EncounterTag` 枚举**没有 `Jaxfruit`**（[`sts2/src/Core/Entities/Encounters/EncounterTag.cs`](../../../../sts2/src/Core/Entities/Encounters/EncounterTag.cs)：`None, Burrower, Chomper, Nibbit, Shrinker, Slimes, Thieves, Workers, Crawler, Mushroom, Knights, Scrolls, Seapunk, Slugs, Exoskeletons`）；参考枚举新增了 `Jaxfruit`。
- `SLITHERING_STRANGLER_NORMAL`：源 **∅**（[`SlitheringStranglerNormal.cs`](../../../../sts2/src/Core/Models/Encounters/SlitheringStranglerNormal.cs) 无 `Tags` 覆写，基类 [`EncounterModel.cs:114`](../../../../sts2/src/Core/Models/EncounterModel.cs) 默认 `Array.Empty<EncounterTag>()`）vs 参考 `[Jaxfruit, Slimes]`。
- `SNAPPING_JAXFRUIT_NORMAL`：源 `[Mushroom]` vs 参考 `[Mushroom, Jaxfruit]`。
- 影响面：遇袋谓词是 [`EncounterModel.cs:279-286`](../../../../sts2/src/Core/Models/EncounterModel.cs) `SharesTagsWith(other) = Tags.Intersect(other.Tags).Any()`，被 [`ActModel.cs:419-430`](../../../../sts2/src/Core/Models/ActModel.cs) `AddWithoutRepeatingTags` 使用；tag 集合不同会改变**拒绝次数**，进而改变流位置。⇒ 见下 §10 候选 1。

### 4. Act0 古代人候选只有一个，且不参与共享先古

[`RunManager.cs:671`](../../../../sts2/src/Core/Runs/RunManager.cs) 是 `foreach (ActModel item in State.Acts.Skip(1))`，共享先古子集**只分配给 Act1+**，Act0 的 `_sharedAncientSubset` 恒为 `null`，因此 Act0 候选 = 本幕 [`Overgrowth.cs:26`](../../../../sts2/src/Core/Models/Acts/Overgrowth.cs) `AllAncients = [Neow]`（参考 `RunState.cs:276` `subsets[0] = Array.Empty<Type>()` 同义）。**A 实现时不能把 Act0 古代人做成 2 项池**——那会多消费一次 `NextInt(2)` 并改变其后所有流位置。manifest 的 `act0.ancient_pool.act0_effective_candidates` 已显式给出 `["Neow"]`。

### 5. 诊断副本的"角色袋 126 项 / 8 桶"是副本重放模型的假象

源与参考的**角色袋稀有度过滤语义一致**（源 [`RelicGrabBag.cs:16-22/69-92`](../../../../sts2/src/Core/Runs/RelicGrabBag.cs) `_rarities={Common,Uncommon,Rare,Shop}` 的 `RemoveAll`；参考 `RelicGrabBag(..., includeAllRarities: false)` 默认过滤）⇒ 源侧角色袋 = **122 项 / 4 桶 → 118**。诊断里"126 项/8 桶"同好落到 118（`126−8 = 122−4`），所以**流位置相同、295 分解仍成立**，但**逐桶分布不同**（8 桶 vs 4 桶），洗牌结果不可互推。⇒ 以源 4 桶为准；不要用副本重放当源实现依据。

### 6. 身份绑定

- 源：`git_head = 92ccd346b708b0f552ee074dc7ece446a2908a11`，`git status --short -- sts2` **为空（源树干净）**；遗物池目录 9 文件聚合 `E285CF9E…`，遭遇目录 96 文件聚合 `9954FCB6…`；逐文件 sha256 见 JSON `identity.source.files`。
- 参考：`git_head = 2cdaea3b6dbc20cfa340662fa73640f20bb6fb00`，但参考仓库存在共享未提交改动，**本包引用的 8 个参考文件中 [`Runs/RunState.cs`](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs) 是脏文件**（` M src/Sts2Sim.Core/Runs/RunState.cs`，sha256 `F32AFCEE29E5EC0E3079F28E1F30E4F2E27E38848B18D166AF4E31C4FC854059`）；其余关键文件 sha256：`Content/Acts/Overgrowth.cs` `1B8D3384…`、`Content/ActDefinition.cs` `2771474F…`、`Rooms/Act1EventPool.cs` `AD1CDB94…`、`Rooms/SharedEventPool.cs` `46849956…`、`Rooms/SharedAncientPool.cs` `46FB616F…`、`Runs/RelicGrabBag.cs` `63A9E2D2…`。**结论绑定文件摘要，不绑定 HEAD。**

## 验证与证据

- 命令：`python tools/source-manifest/extract_source_manifest.py --out .audit/dsh-env-t04-source-init-manifest-2026-10-08/source-initialization-manifest.json --markdown .audit/dsh-env-t04-source-init-manifest-2026-10-08/source-initialization-manifest.md`，工作目录 `F:/Scylier-Project/active/auto-the-spire`，**rc=0**，两次运行结果一致（含修 `block_after` 解析缺陷之后）。
- stdout 摘要（可复核）：`shared_relic_items 118 / shuffle_draws 112`、`character_bag_items 122 / buckets [Uncommon,Common,Rare,Shop] / shuffle_draws 118`、`act0_events 31 / shuffle_draws 30`、`act0_monster_candidates 16`、diff verdicts 9×`identical` + `encounter_tags: differs`（`compared 22`，2 条实差）。
- 工具自证的两条**不允许静默出错**约束：① `block_after` 曾把 marker 内 `{ get; }` 的 `{` 当块首，导致参考侧全部解析成 0 项——已修（`idx = text.index(marker) + len(marker)`），并为"任一侧 0 项"加 `unsupported_parse_gap` 判定；② 源 tag 曾因 `block_after(body,"Tags")` 落到 `GenerateMonsters()` 方法体而全空——已改为语句正则 `Tags\s*=>(.*?);` + `EncounterTag\.(\w+)`。⇒ **本交接的差异结论只在修复后版本有效。**
- 交叉验证：`112+118+30 = 260` 对齐前驱诊断的 `up_front=260` 首抽位置（前驱交接与 [负责人复核](dsh-supervisor-review-2026-10-08.md) 均记录该值）。
- 文档检查：`git diff --check` rc=0；`python tools/doc-check/check_docs.py` 对本交接文件 **0 问题**（其余 4 条失败全在 A 线的 `docs/review/tofix/dsh-env-t05-map-room-handoff-2026-10-08.md`：`:17/:23/:52/:60` 把同目录主清单写成 `../README.md`，应为 `README.md`；属他线文件，本包不改，供负责人转 A 修正）。

## 源与参考：一致 / 偏离 / 未验证

- **一致**（本包实测逐项，含顺序）：共享遗物池 118 项的稀有度、事件池（本幕 13 + 共享 18，各 31）、弱/常规/精英/BOSS 候选、Act0 古代人 `[Neow]`、共享先古 `[Darv]`、两袋分桶规则与 `Σ(n−1)` 语义、`UnstableShuffle` 与参考 `Shuffle` 逐次抽取等价（均 `n−1` 次 `NextInt`）、共享先古"先分配完再生成房间"的顺序。
- **偏离**（4 条，全部登记为 §10 候选，见下）：① `EncounterTag` 2 条 tag 实差；② 参考 `EffectiveEventPool` 直接取集合 vs 源按 epoch 逐段 `RemoveAll`（`All` 下集合与顺序等价，且该路径不消费随机数，仅池构造路径不同）；③ 参考未移植 `ApplyDiscoveryOrderModifications`（源该路径不消费随机数）；④ `ValidateRoomsAfterLoad` 双 BOSS 排除口径（本局未走到）。
- **未验证**：① 本包**未构建、未参与 solution 编译**（受 A/C 串行约束），因此"工具通"不等于"DLL 通"；② 未重跑严格 suite，也未重跑 `rng-initial-shuffle`（仍 `diverged`）；③ 运行期**遇袋重抽次数**属运行行为，静态取证不能硬断（前驱实测参考普通槽 16 次、base 18 次都只是运行结果）；④ 仅单 seed `ENV-T04-RNG-SHUFFLE-001`；⑤ 仅 Act0/Overgrowth 与 Ironclad；⑥ 参考 `RunState.cs` 为脏文件，其行为可能随后续改动变化。

## §10 窄裁决候选（按 [§10](../../../design/environment-implementation-constraints.md#10-参考差异与失败处理) 六字段登记，交负责人裁定）

1. **`EncounterTag` 的 `Jaxfruit` 差异（新路径，需裁定）**
   - case ID：`rng-initial-shuffle`（ENV-T04 严格 suite 原 ID）
   - source 文件·摘要·定位：`sts2/src/Core/Models/Encounters/SlitheringStranglerNormal.cs`（无 `Tags` 覆写）、`sts2/src/Core/Models/Encounters/SnappingJaxfruitNormal.cs:14`（`Tags => SingleElementList(EncounterTag.Mushroom)`）、枚举 `sts2/src/Core/Entities/Encounters/EncounterTag.cs`（无 `Jaxfruit`）、`sts2/src/Core/Models/EncounterModel.cs:114/279-286`；源树干净 @ `92ccd34`，遭遇目录 96 文件聚合摘要 `9954FCB6959D554CC2A3582D5980746429CB81578565BE7FB7233E897E40E210`（逐文件 sha256 见 manifest `identity.source.files`；`Acts/Overgrowth.cs` 单独为 `61FF9D71…`）
   - 两侧版本：源 `sts2-frozen-2026-10-05` @ `92ccd34`；参考 @ `2cdaea3b…`（`Content/Acts/Overgrowth.cs` sha256 `1B8D338468D04F7E896C76CF63D448F211810669DDF721743A5E8ADFD4F98C9D`）
   - 允许偏离的具体路径·值或条件：参考 `Content/Acts/Overgrowth.cs` 对 `SLITHERING_STRANGLER_NORMAL` 声明 `[Jaxfruit, Slimes]`、对 `SNAPPING_JAXFRUIT_NORMAL` 声明 `[Mushroom, Jaxfruit]`；源分别为 `∅` 与 `[Mushroom]`
   - base 应有结果（按源）：`SlitheringStranglerNormal.Tags = ∅`、`SnappingJaxfruitNormal.Tags = [Mushroom]`，遇袋谓词按此拒绝/接受
   - 参考仍有该偏离的断言：参考侧 2 条 tag 集合与本条完全一致（工具 `encounter_tags` 判定 `differs`，`compared 22`）
   - 影响：谓词拒绝次数 → 流位置；可能解释普通槽抽取次数差异的一部分，**不能**用"参考数量"掩盖
2. **`EffectiveEventPool` 取集合 vs 源 epoch 过滤**
   - source：`sts2/src/Core/Models/ActModel.cs:334-347`（三段 `RemoveAll`，All 下全部保留）、epoch 门 `Event1Epoch`/`Event2Epoch`/`Event3Epoch`
   - 两侧版本：同上；参考 `Content/ActDefinition.cs:53` `EffectiveEventPool => [.. EventPool, .. SharedEventPool.All]`（`2771474F…`）
   - 允许偏离·值或条件：All 解锁下两侧集合均为 31、**顺序也一致**（已实测 `identical`）；差异只在"非 All 解锁"或 epoch 状态不同时显形
   - base 应有结果：按 epoch 过滤实现（All 下等价于取集合）；该路径与随机数消费无关；参考断言：仍不做 epoch 过滤
3. **参考未移植 `ApplyDiscoveryOrderModifications`（已登记 #179）**
   - source：`sts2/src/Core/Models/ActModel.cs:404-417`（遍历 `BossDiscoveryOrder` 替换 Boss，**不消费随机数**）；参考：`Content/ActDefinition.cs:69-72` 注明不移植
   - base 应有结果：该路径不改变流位置、不参与 `UpFront` 消费；参考断言：仍不移植
4. **`ValidateRoomsAfterLoad` 双 BOSS 排除口径**
   - source：`sts2/src/Core/Models/ActModel.cs:391-402`（`:395` 用 `_rooms.SecondBoss?.Id`、`:399` 用 `_rooms.Boss.Id`）；参考：`Content/ActDefinition.cs:104-119` `PickEncounterExcluding` 按传入第一个 BOSS 排除
   - 允许偏离·值或条件：本局为 Ironclad 进阶 0 单 BOSS ⇒ **未走到**，当前不可判定；base 应有结果：留待 A10 双 BOSS 包实测

### source-backed 检查（无需裁定的正面证据，供负责人关闭时核对）

- 共享袋/角色袋 `Σ(n−1)` = 112 / 118，与诊断 `230` 括号一致。
- Act0 事件池 31 的**组成与顺序**（13 本幕 + 18 共享）已逐 ID 证明，非 count 相同。
- Act0 古代人 `[Neow]`、共享 `[Darv]`，与 `Skip(1)` 口径一致。
- Act0 普通遭遇候选 16 = 弱 4 + 常规 12，与参考 16 一致。
- 源 `EncounterModel.Tags` 基类默认空、`SharesTagsWith` 为 `Tags.Intersect(...).Any()`，谓词语义两侧一致（仅 tag 数据有 2 条差）。

## 原完成条件逐项对照（[主清单](../../tofix/README.md#env-t04)「完成条件（保留原合同）」）

1. **既有 7 个 case 实际双侧执行**：本包不执行 suite、不改 suite；沿用已交付的 `test --suite regression`（15 必测，8 `passed` + 1 `self_checked` + 6 不计通过，rc=1）。本包未重跑，属**未验证**。
2. **5 个 unsupported 原 ID 保留**：本包未动 suite 分母，未新增/删减 case；`rng-initial-shuffle` 仍 `diverged`。
3. **缺 case/零执行/缺参考/unsupported/首差/缺维度非零**：本包产物是静态取证，不产生通过判定；`encounter_tags` 以 `differs` 具名报出而非清零，`unsupported_parse_gap` 守卫保证"没解析出来"不会被算成相同。
4. **源/reference 派生与随机 trace 可核对**：新增 `identity`（HEAD + 逐文件 sha256 + 目录聚合摘要 + 脏文件）与 `call_order_and_conditions`（各阶段调用、条件、源行号）绑定源派生；随机 trace 仍由已交付的 `rng`/`init-upfront` 工具负责，本包未重跑。

## 未做的与边界

- 未构建、未跑 suite、未写 obj/bin、未改任何生产文件、未提交。
- 未裁决任何偏离：4 条候选全部留给负责人；不把参考数值当源期望。
- 未实例化召唤族（`Fogmog` 仍在源分母里、未实现内容保留在池清单中但不实例化）；未恢复 AI/完整身份；未开启下一生产包。
- 未修正他线文档缺陷：`docs/review/tofix/dsh-env-t05-map-room-handoff-2026-10-08.md` 的 4 条同目录链接写成 `../README.md`（见上「验证与证据」），属 A 线文件，本包不越界。

## 不变量

- 源/参考只读；`core engine/ir@0.3` 冻结；比较器与 RNG 算法未改。
- 单 seed `ENV-T04-RNG-SHUFFLE-001`、Ironclad、进阶 0、`UnlockPolicy.All`、`SinglePlayer`、`source_baseline_id=sts2-frozen-2026-10-05`、`scope_id=base-spire-singleplayer-dev-v1`（见 manifest `run_configuration`）。
- 原 case 保持真实 `diverged`。

## 21:53负责人接收说明

实际状态停止，完整结果取齐至855，最终turn/end completed。源输入静态证据子包验收：负责人重新提取manifest与交付JSON对象完全一致，并直接核对两条tag源/参考差。260只是源真实前置消费账，不能直接补抽；原初始化case仍diverged。源/参考HEAD不代替实际文件摘要。负责人将文件头误写status字段更正为约定delivery_status；这是管理格式修正，保留执行者交付声明，不表示父项验收。详见[当日复核](dsh-supervisor-review-2026-10-08.md#2153验收与地图公开接续)。移入records只整理日期证据。
