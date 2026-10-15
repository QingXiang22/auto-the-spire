# ENV-T04 正常开局首消费归因交接 — 2026-10-11

- task_id: ENV-T04
- dispatch_id: ENV-T04-startup-alignment-20261011
- session_id: session-f68b943d-7ca1-4a97-b2d2-979be7bcc654
- dispatch_record: [.audit 派发记录](../../../.audit/dsh-startup-alignment-2026-10-11/task-record.json)
- delivery_status: ready_for_review
- updated_at: 2026-10-11T12:47:29+08:00

本包交付正常开局的逐次随机消费证据，让 Codex 能拆分真实 base 初始化机制包；**没有消除原开局首差，也不申请关闭 ENV-T04 / CORE-AUTHOR-04 / M01**。当前认领与依赖只维护在[主清单 ENV-T04](README.md#env-t04)。验收者为 Codex 负责人；此处是执行者交付，不是负责人验收。

## 接手后先做什么

1. 核对[主清单](README.md#env-t04)及共享 Git 状态，复用本包[逐次消费轨迹](../../../.audit/dsh-startup-alignment-2026-10-11/final-diagnosis/startup-consumption-trace.json)和[阶段诊断](../../../.audit/dsh-startup-alignment-2026-10-11/final-diagnosis/init-upfront-diagnosis.json)，不要重做 CORE-AUTHOR-04、地图移植或受控初始化。
2. 负责人按 ENV-T08 / base 主责协调一个“真实新 Run 的 UpFront 初始化”机制包：先实现源共享/玩家遗物袋生命周期，再事件/遭遇全池及精英/Boss/古代人预生成；不能只把随机游标前移 260 次。具体落点与分段完成条件见下文。未获派单前本会话不写 base/core/reference。
3. 用下文最小命令重放同 seed，预期先消除第 1 次调用用途差异，再检查原 case 的敌人、全部牌区、actions、streams。最终仍按[原工作包约束](../../design/environment-implementation-constraints.md#env-t04初始洗牌的派生对齐边界2026-10-08)与原 17 分母验收，不能把此诊断 rc0 当双侧通过。

## 结论：首差发生在消费用途，不是初始 Shuffle 样本

原输入 `ENV-T04-RNG-SHUFFLE-001`，`RunConfiguration.Ironclad(0, dev)`，源版本 Profile `source-compat-sts2-frozen@1`。run seed `3850333834`；UpFront `2615586789`、Shuffle `1863647213`、Niche `52833259`。没有改 seed、suite、Profile、默认参考构造或玩法。

| 真实涉及的 run 流 | base / reference 累计 | 第一次消费及差异 |
| --- | --- | --- |
| UpFront | 17 / 295 | 两侧第 1 次前状态指纹都为 `0x422dd5ee644eb169`。base 遭遇队列抽取 `[0,4) → 2`；reference 共享遗物袋首桶洗牌 `[0,30) → 21`。**相同初始流状态、不同调用用途/输入界限**，不能把样本值不同归为源随机算法错误。 |
| Niche | 1 / 1 | seed `52833259`，前状态 `0xccc58273a333ad53`，双方 `[0,3) → 1`，未发现本流样本首差。用于怪物出生 HP 偏移；敌人身份已经因 UpFront 不同，所以不能据此宣称敌人 HP 全等。 |
| Shuffle | 9 / 9 | seed `1863647213`，首前状态 `0xaf4310fd7c212fde`，双方 `[0,10) → 2`；9 次样本与每次前状态逐项一致，无本次初始化洗牌样本首差。 |

### 实际调用序列

base 正常入口为[BaseBackend:280–287](../../../tools/differential/BaseBackend.cs#L280-L287)的工厂 `CreateSession → Start` 一次，无 setup 手牌/清场。生产调用落在[CombatSlicePackage:3512–3554](../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs#L3512-L3554)：`SetupBattle → EnqueueEncounter_weak(0) → SampleInt(PoolStream,0,total)`。逐次轨迹的调度记录首个 RandomCall 是 `0→1`，前面没有共享/玩家遗物袋或事件列表消费；[抽取语句:3327–3337](../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs#L3327-L3337)。15 普通槽位加拒绝重抽合计 17。

reference 独立正常入口为[ReferenceRunEntry:101–122](../../../tools/differential/ReferenceRunEntry.cs#L101-L122)：`new RunState → Apply(12 run streams) → Player.CreateForNewRun → AddPlayer → RoomFactory.CreateRoom → PushRoom → Enter`。实测第 1 次：`Player.CreateForNewRun → RunState.CreatePlayerRelicGrabBag:249 → RelicGrabBag..ctor:40 → Rng.Shuffle:497 → NextInt:178`。共享袋112次，玩家袋118次，创建玩家后累计230；共享古代人洗牌0，事件31项洗牌30次，累计260。第一个普通遭遇抽取是第 **261** 次，前状态 `0x9e38e4fd548739b3`、`NextDouble() = 0.24789939941295447`，路径 `Room.Enter → EnsureRoomsGenerated → GenerateAllActRooms → GenerateActRooms:308 → PickEncounter → PickMonsterEncounter → PickWithoutRepeating → GrabBag.GrabAndRemove → GrabIndex`。普通队列16次→276，精英17次→293，Boss1次→294，古代人1次→295；[阶段证据](../../../.audit/dsh-startup-alignment-2026-10-11/final-diagnosis/init-upfront-diagnosis.json)独立副本重放与实际总数一致。

源依据：[RunManager.InitializeNewRun:445–461](../../../sts2/src/Core/Runs/RunManager.cs#L445-L461)明确先 `State.SharedRelicGrabBag.Populate(...,State.Rng.UpFront)`，再逐玩家 `PopulateRelicGrabBagIfNecessary`；[参考已有生命周期:239–257](../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs#L239-L257)已经接入这两袋。故没有遗漏一个可由工具接入的现成参考入口。源在完整初始化前置袋消费这一点支持 reference，不支持 base dev 的直接队列起点；**这不是裁决 reference 全内容/全版本均正确**。参考有效内容池/解锁过滤与源完整池仍须后续独立核对。

### 洗牌前输入和调用路径

各侧首次 Shuffle 之前独立读取的起始牌序均为 `StrikeIronclad×5, DefendIronclad×4, Bash×1`，顺序相同，没有设牌。参考[Player:277–286](../../../reference/sts2-sim/src/Sts2Sim.Core/Entities/Players/Player.cs#L277-L286)复制 Deck 后调用 DrawPile.RandomizeOrderInternal；[CardPile:60–65](../../../reference/sts2-sim/src/Sts2Sim.Core/Entities/Cards/CardPile.cs#L60-L65)交给 Rng.Shuffle。base [CombatSlicePackage:3227–3235](../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs#L3227-L3235)复制长期牌组再 ShuffleCollection。bounds 依次10、9、8、7、6、5、4、3、2，双方结果依次 `2,0,0,6,5,3,2,0,1`；每次前状态见逐次轨迹。该 seed 的起始 Shuffle 接线/采样未见差异，不扩展为真实重洗一致性结论。

## 最小可实施机制包建议（交负责人拆包，不是本会话新认领）

受阻 case 为 `rng-initial-shuffle`，依赖 ENV-T08/base 真正新 Run 初始化；并非整个工具工作都停止。

1. **第一段真实遗物袋生命周期**：在[正式工厂 CreateSession 配置:111–179](../../../mod-the-spire/base-spire/BaseSpire/SinglePlayer/BaseSpireRuntimeFactory.cs#L111-L179)及[SetupBattle:3512–3554](../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs#L3512-L3554)之间，由 base 主责确定新 Run 的源等价阶段。源共享袋按解锁池/稀有度桶顺序洗牌，然后每玩家袋；保留真正可取用的袋状态与幂等性，不做弃用随机占位。dev-map 已有共享袋能力可评估复用，但不能修改原 case scope 来绕开 dev 缺口。针对该 seed 首次调用应变为共享袋洗牌用途，后续玩家袋和计数须来自真实池，而不是硬编码112/118。
2. **第二段完整幕预生成**：按[源 RunManager:668–691](../../../sts2/src/Core/Runs/RunManager.cs#L668-L691)与[参考 RunState:271–353](../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs#L271-L353)核对共享古代人、事件、普通、精英、Boss、古代人的顺序、池内容/过滤、拒绝重抽。base dev 普通池目前是支持子集，不能在工具替换为参考池或按 seed 特判。前置阶段实装以后，普通队列输入池差异仍可能产生新首差；本包不承诺只补两袋即可完整通过。
3. **门槛**：先同原 seed 原 case 的阶段/首样本对齐，再保留敌人/牌区/streams全比较；共享生产改动由负责人按根测试取舍执行受影响 Slice/消费者与最终集成，最后完整17。保留已过两seed地图输入。重洗包独立调查，不扩大全局 Profile。

可继续事项：工具能对后续新构建运行本包诊断；原14双侧case和受控Power/实例槽位检查均可复用。`random-reshuffle-unsupported`仍为原受控初始化的 boundary3 牌槽顺序首差；本包**没有证明与正常开局同因**，不以 UpFront 初始阶段缺失替它定案。

## 本包修改与诊断边界

- 新增[StartupTrace](../../../tools/differential/StartupTrace.cs)：工具出口记录 actual reference run 流的 counter/bounds/样本/前状态/调用栈，首次Shuffle输入；副本预测期间暂停全局observer并恢复，不消费原对象。
- [InitUpFrontDiagnosis:309](../../../tools/differential/InitUpFrontDiagnosis.cs#L309)在原阶段重放匹配后调用新轨迹诊断；[工具说明](../../../tools/differential/README.md#命令状态)记录出口与覆盖边界。
- 正常 base 工厂没有公开debug注入参数；因此**另外**用相同不可变 Rules、公开seed列表、角色、compute/backend/Profile创建 instrumented replay，不代替正常Run。透明Profile转发并开既有SessionDebugTrace；实测 facts与全部RNG diagnostics匹配真实工厂，DroppedRecords=0。这里不是受控setup注入，但仍是单列重放证据。
- 逐次参考采样覆盖12 run流中实际触及的UpFront/Niche/Shuffle；玩家/内容/临时/怪物RNG及构造期间独立地图流未做本包逐次全覆盖。Profile保持run-only，不改变参考玩家原生派生。正常suite与原受控case仍分列。
- 没有运行行为修复，所以不添加没有判别增益的case。复用严格case、比较器负例及独立48源Profile向量；新诊断保留“重放facts/RNG必须等真实工厂且轨迹不截断”的失败门槛。
- 既有身份出口 SourceFiles 的 `sha256:` 前缀与登记裸hex比较会误标“不一致”；本轮看到hex相同但显示该文字，未据此推断源改动。另存真实文件裸hex摘要，交负责人处理身份展示缺口；本包未扩展该既有接口修复。

## 输入身份与验证

活动 `engine/ir@0.4`、core/0.3、base/0.0.2，176卡/44能力/38敌人；base dev Ruleset `75a9d9345def3426`、PublicBuild `2f0fd07325835101`、content `17b137ee8e3f1726`。算法 `mega-random/xoshiro256ss-splitmix64@1` / draw `vanilla-uniform@1`。参考HEAD `2cdaea3b6dbc20cfa340662fa73640f20bb6fb00`，工作树已有共享修改，**不能用HEAD宣称干净冻结**；相关源/reference/最终DLL摘要与完整参考dirty列表见[交付身份](../../../.audit/dsh-startup-alignment-2026-10-11/delivery-identity.json)。源baseline是 `sts2-frozen-2026-10-05`，窄Profile沿既有三类派生授权；未认证整个参考与源同版本。

原suite SHA256 `5cca423f8e0d298f8229d5b5ad14cbe0fb104515645c796db4a858d31af223a2` 保持；[输入前摘要](../../../.audit/dsh-startup-alignment-2026-10-11/inputs-before.json)中的工具已有文件仅诊断实现变动（交付前README另补方法）。suite17 ID/seed/动作/维度/Profile/来源未改。

工作目录均为 `F:\Scylier-Project\active\auto-the-spire`。以下命令中的 `$dll` 为 `.artifacts/parallel/dsh-startup-alignment/bin/Differential/debug/Differential.dll`，`$audit` 为 `.audit/dsh-startup-alignment-2026-10-11`，实际执行展开为这两个路径；没有使用另一构建冒充最终产物。

| 检查 | 实际命令（参数展开上述路径） | rc / 结果 | 证据 |
| --- | --- | --- | --- |
| 最终隔离构建 | `dotnet build tools/differential --artifacts-path .artifacts/parallel/dsh-startup-alignment` | 0；既有ComparatorSelfTest:67 CS8602警告 | [最终构建日志](../../../.audit/dsh-startup-alignment-2026-10-11/build-final-fixed.txt) |
| 逐次诊断 | `dotnet $dll init-upfront --seed ENV-T04-RNG-SHUFFLE-001 --records .audit/dsh-core-author-04-2026-10-11/regression/records --output $audit/final-diagnosis` | 0；阶段总数匹配、重放匹配工厂、无截断；**不是开局通过** | [诊断日志](../../../.audit/dsh-startup-alignment-2026-10-11/final-diagnosis.txt)及前述两个JSON |
| 原定向case | `dotnet $dll test --suite regression --only rng-initial-shuffle --output $audit/directed` | 1；1执行/complete=false，敌人首差；子集无decision mutant目标自检失败1，不宣称门槛全过 | [定向日志](../../../.audit/dsh-startup-alignment-2026-10-11/directed.txt) |
| 最终完整regression（一次） | `dotnet $dll test --suite regression --output $audit/regression` | **1；17/17执行，base17/reference16；14双侧passed/1单侧self_checked/2未过，自检22项0失败** | [报告](../../../.audit/dsh-startup-alignment-2026-10-11/regression/regression.json)、[日志](../../../.audit/dsh-startup-alignment-2026-10-11/regression.txt) |
| 既有独立源向量 | `dotnet $dll rng --output $audit/rng` | 0；5读数seed一致，8布局差异已登记、未登记0，48源Profile向量失败0 | [日志](../../../.audit/dsh-startup-alignment-2026-10-11/rng.txt)、[实际报告](../../../.audit/dsh-startup-alignment-2026-10-11/rng-crosscheck.json) |

完整回归run_id `regression@env-t04-regression@1+20261011T044604Z`。报告的command字段是既有逻辑入口展示 `dotnet run`，本包真实命令为上表隔离DLL，不把展示字段当实际构建命令。正常开局仍 boundary0 `enemy.base-spire:fuzzy-wurm-crawler.alive: 缺失 ≠ 存在`（base ShrinkerBeetle/reference FuzzyWurmCrawler）；重洗仍 boundary3 `pile.draw.initial-slots: start:05,start:00,start:08,start:03,start:07 ≠ start:03,start:06,start:04,start:09,start:01`，unsupported非零。

开发中出现两次工具编译错误：缺RunRngType命名空间，及误用FactSnapshot.Key（实际Path）；已修并最终重建。失败日志保留，本包不把失败记录替换为通过。没有重跑整solution/Engine/Slice/net/训练/归档，因为没有改玩法、公共编排或DSL；本包验证范围为受影响工具构建、诊断和原严格suite。文档检查 `python tools/doc-check/check_docs.py` 实际 rc2，扫描388篇，只有其他共享交接 [Power交接:21](dsh-power-fixture-handoff-2026-10-11.md#改变文件与共同初值契约) 的 `README.md#L7` 和 [Power交接:25](dsh-power-fixture-handoff-2026-10-11.md#改变文件与共同初值契约) 的约束文档 `#L181` 两个既有锚点问题；本包文档没有被报告问题，不越界修改该交接。见[doc-check日志](../../../.audit/dsh-startup-alignment-2026-10-11/doc-check.txt)。`git diff --check` rc0，只有共享文件LF/CRLF提示，见[diff-check日志](../../../.audit/dsh-startup-alignment-2026-10-11/diff-check.txt)。不将全仓文档检查写成通过。

## 接手自查与维护位置

本交接直接定位目标/有效状态、复用成果、第一步、原完成条件/验收者、阻塞case/依赖及维护位置；属于作者自查，不是独立审阅。长日志保存在本包.audit，状态只回写ENV-T04本包行。本包请求 Codex 复核逐次归因与真实base机制拆包，尚未执行任何跨模块修复、不重启旧DS/AI、不提交/推送/tag。
