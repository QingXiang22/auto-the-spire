# 主清单整理前的任务与维护记账 — 2026-10-07

本页保留本次整理前主清单中的逐批实现、依赖取证和维护记录。正文属于原记账时点，不是当前任务表；接手以[唯一当前清单](../../tofix/README.md)的对应任务条目为准。迁入 records 只保存证据，不作生产修复或验收结论。

原维护文字及三条较长的任务记账保留原表述，仅按新位置修复相对链接。完整改动前文件及 SHA-256 在 `.audit/handoff-docs-2026-10-07/` 的 `.snapshot` 和 manifest 中。

## 任务行中的原始记账

## ENV-T01

**进行中，算术与描述收口已交，加载证据仍缺**（沿用A执行会话；边界＝base `Slice/`、`ContentSource/`、`Slice.Tests/`与必要文档；已交＝53位一次抽取、源LINQ Sum/NextFloat/float减法纯Compute、版本化后端及4项判别检查；[负责人复核](../2026-10-06/t01-t02-completion-review-2026-10-06.md)确认319/95本地全绿。**A本轮收口**＝①`Describe` 的 Dexterity/addCards/编队三处误报按已实现范围改写（Dexterity 是 `role=Dexterity` 的加性每层 +1，`addCards` 已实现，编队已接入均匀抽/唯一组/随机槽）；②`Fogmog` 权重从整数比 `2/3` 按源改回 `0.4`/`0.6`；③新增真实内容检查（`SliceEnemyStateMachineChecks`：真实 `enemies.spire` 的候选行经生产前端解析后权重逐位等于源 float32、`0.083333336` 位型等于 `1f/12f`，并逐条点名 Fogmog/TwoTailedRat 的未加载原因与遭遇排除）；实跑 Slice **324/324**。未交＝TwoTailedRat/Fogmog **实际加载**——依赖链已实测并登记在 ENV-T06 行（`summonEnemy` + `EyeWithTeeth`/`IllusionPower` 的 `afterOwnerDeath`+`queueRevival` 死亡复活链 + 槽位模型；TwoTailedRat 另需 `variables`/`conditions`/`modifyEnemyVariable`/`modifyCombatState`/具名条件），用户 2026-10-06 决定本轮**不实现**该族，故本条按"描述收口 + 内容侧权重证据"收尾；B双侧及B/C后端注入/共同工厂接线缺失。下一成果＝B/C按T04/T03接线与实际执行）

## ENV-T02

**进行中，复核两缺陷已修，待B双侧**（沿用A执行会话；边界＝base `Slice/`、`Slice.Tests/`及写回矩阵文档；[负责人复核](../2026-10-06/t01-t02-completion-review-2026-10-06.md)复现的**两条缺陷已修**：①胜利判定从效果步骤表移除，只挂**动作体末尾**（`PlayCard`/`EndTurn`/`UsePotion` 各一处，`CombatSlicePackage.cs` 的 `BattleEndTail`），`CommitIfSettled` 随之删除（`BattleEndTail` 已是动作最后一条指令）⇒ 出牌钩子、`InPlay` 移除、消耗与结果区都在终局之前跑完；②卡实体新增隐藏可选字段 `Card.LongTermSource`，`CopyCardFields` 复制时写入，永久变化按**实例**定位长期牌。检查从5条增至**9条**：出牌收尾（`after_card_played` 钩子 + 离开 `InPlay`）、条件分支末杀（`PactsEnd`）、消耗牌末杀（`MoltenFist` + `FeelNoPain`）、副本→长期来源双射、同定义不同实例永久变化互不影响。判别力已用"临时把旧尾巴插回效果步骤表"实测：新检查立刻以"Feed 卡在 `InPlay`"失败，回退后恢复。实跑 build 0 错误、Slice **324/324**、Engine **95/95**、net **393/393**；写回矩阵与终局时序已同步到 [spec](../../../design/mod-the-spire/spec.md)。未交＝B 执行 `last-kill-before-terminal` 双侧 case 与负责人复核；跨线影响＝`Run.MaxHp` 权威、胜利终局时机后移到动作末尾、卡实体多一个隐藏字段（已改规则身份与 `PublicBuildId`，net 公开映射不受影响，393/393 实测））

## ENV-T06

**可认领（未开始）；本族的 `summonEnemy`/死亡复活链按用户 2026-10-06 决定"不实现、诚实记账"**（A 线本轮受指派先取证"只做 `summonEnemy` 能否解锁真实内容"，实测结论＝**不能**，故不认领该子包，避免交付一个没有内容消费者的原语。**依赖链（实测，供 T01/T05/T12 引用）**：①`summonEnemy` 是 `Fogmog`（`Enemies/Content/enemies.spire:595`）与 `Infested`（`:99`，Wriggler×4 + `startingAction`）的缺口；②`Fogmog` 的召援目标 `EyeWithTeeth` 因 `startingPowers = [power(IllusionPower, 1)]`（`:540`）**也未加载**；③`IllusionPower` 未支持原因＝`trigger = afterOwnerDeath` 未支持 + `queueRevival` 未支持 + 运行时无该能力行为；④源码语义 `IllusionPower.cs:59-133`＝死亡时**不移出战斗**、保留豁免能力、立即插入 `REVIVE_MOVE`、复活回合满血，属死亡/复活生命周期（会触碰胜负判定、目标过滤与敌方回合顺序）；⑤`slot=LastAvailable`（`TwoTailedRat.cs:180` 取最后一个空闲槽）另需槽位模型与"召唤者不进本回合队列"的稳定顺序；TwoTailedRat 还需敌人 `variables`/`conditions`/`modifyEnemyVariable`/`modifyCombatState`/具名条件。**最小复现**：`$env:W06_DUMP_POWER='Illusion'; dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests`（打印 ③ 的三条原因；同一次运行另打印 `BASE-02 敌人未支持：enemy:Fogmog/EyeWithTeeth/TwoTailedRat` 与 `BASE-01 排除：base-spire:fogmog-normal` 的逐条原因）。**受阻 case**＝T01 的"真实内容分数权重"证据与任何依赖这两条敌人遭遇的双侧 case（保持未执行、不改基线）。**可继续事项**＝本族其余机制（Thorns 站点、能力移除、数值/生命周期）与 B/C 接线互不阻塞。[复核报告](../2026-10-06/t01-t02-completion-review-2026-10-06.md) §4 为触发证据） **2026-10-07模型审阅新增数值证据**：当前默认两位小数量化使已加载Strength/Shrink/Cruelty/Vulnerable组合的源伤害2实际为3；[审阅报告](sts2-model-ability-review-2026-10-07.md#41-数值转移有实际反例env-t06)及[探针输出](../../../../.audit/sts2-model-review-2026-10-07/results.txt)保留复现。此项并入本包既有数值范围，先核对base数值实现/纯Compute路径，不据此解冻core或实现此前暂缓的召唤族。

## 原复核摘要

最近行为复核为2026-10-06的[T01/T02独立证据](../2026-10-06/t01-t02-completion-review-2026-10-06.md)：build 0错误/0警告、Engine95/95、Slice319/319、net393/393；dev build=`999c7555a421328f`，68卡/29Power/35敌人、15槽普通队列、134未支持/8排除。算术与Feed本体跨房已交付，末杀完整动作与长期来源未交付，具体开放项唯一见T01/T02任务行。此前[负责人基线证据](../2026-10-06/base-spire-owner-decision-2026-10-06.md)的Slice310/310、dependency10/10及7/7组合探针保留为历史，不能代替本次生产集成判断。

R1范围拒绝/R2起始牌来源顺序/R3未知源基线拒绝及实际摘要已有修复，本轮现有检查独立通过；仍需B/C同工厂身份/源随机互证。状态机/GrabBag/编队/startingPowers/addCards/Niche/Burning Blood/Frail/Constrict/Dexterity已有局部交付；T01/T02新结论以上述任务行和证据为准。Thorns before/条件与真实Run未验收。1142处注释替换字符是此前词法扫描证据，本次未重扫。A会话预算不等于整线技术阻塞。

此前D1/D1.1/D2与Bully窄缺陷已验收，真实管线已交付但模型效果未达标；不重做工程，也不自动启动D3/D4。完整四幕、严格suite、120对、正式公开全程、真实客户端和Release性能均无本轮通过结论。


## 原维护记录

2026-10-07 交接 skill 维护：本会话已交付用户级 `auto-the-spire-handoff`、模板/接手检查及 docs AGENTS 接手约定；状态＝已验收（仅文档维护）。修改边界＝该 skill、docs AGENTS、本记录、综合交接/records索引及 `.audit/project-handoff-skill-2026-10-07/`；使用方式与证据见[交接记录](project-handoff-skill-2026-10-07.md)。skill校验、六项接手自查、doc-check（227份/0问题）与 git diff --check 通过；本次维护无待续工作，不变更 ENV 任务。

2026-10-07 清晰写作文档维护：本会话按用户派单安装用户级 `nbj-write-clearly`，修订31份现行入口/指南/规范并补 docs AGENTS 写作约定；状态＝已验收（仅本轮文档维护）。修改边界＝这些文档、本维护记录、records 综合证据/索引与 `.audit/nbj-docs-2026-10-07/`；已交成果及范围见[综合记录](nbj-clear-writing-2026-10-07.md)。原代码块、技术字面量、表格行及主清单任务定义/状态核对通过；122份既有历史证据正文保持原文，6个安装文件逐字节一致，原 skill 检查脚本通过。最终 doc-check 扫描226份Markdown/0问题，git diff --check退出0。生产代码只读，未运行构建、Engine/Slice/net、内容加载或游戏链路，不作ENV验收声明。

2026-10-07 ENV-T03远程Action方案负责人复核：本会话按用户手动唤起取回原Harness会话完整结果（through_seq=269、hasMore=false、turn completed），已交付[综合复核](dsh-action-proposal-review-2026-10-07.md)。56次工具调用均为只读，DS完成审阅；定位与复用方向可采纳，具体方案需修订正式结构的手写覆盖、旧场景/身份混用、过期候选验收及公开字段/包元数据混淆，裁决已关联ENV-T03行。修改边界＝本维护记录、综合报告/records索引及`.audit/dsh-env-t03-actions-2026-10-07/`结果产物，生产代码只读。仅复核方案，不认领C生产实现，不改变ENV-T03待认领状态；不重新派发远程任务。doc-check扫描225份Markdown/0问题、git diff --check退出0；未运行构建、Engine/Slice/net、内容加载或公开Host/Python链路，不作运行时验收声明。

2026-10-07 DSH派发流程修订与ENV-T03只读审阅：本会话按用户最新指示维护用户级`dsh-remote-operator`及[操作指南](../../../design/deepseek-harness-operations.md)，DS直接读取项目工作区，默认派发接受后返回，用户手动唤起结果复核。已派发本轮审阅：归属ENV-T03，目标＝新增Action无需net手工登记的最小改造方案，生产源码只读；Harness任务ID＝`ENV-T03-actions-review-20261007`，会话＝`session-5a3cb0b3-368a-4fae-a700-0e46e572297b`，cwd＝本项目工作区，[提示词](../../../../.audit/dsh-env-t03-actions-2026-10-07/task.txt)/[派发记录](../../../../.audit/dsh-env-t03-actions-2026-10-07/task-record.json)已保存，响应为accepted。仅审阅，不认领C生产实现，不改变ENV-T03待认领状态；负责人修改边界＝skill及界面提示、指南、本维护记录与派发产物。skill校验通过、doc-check扫描224份Markdown/0问题、git diff --check退出0；未查询DS运行状态或收回结果，任务完成与方案复核尚未验证，等用户唤起；未运行游戏构建/规则/对拍或修改生产代码。

2026-10-07 DSH远程委派skill维护：本会话已交付用户级 `dsh-remote-operator` skill及[操作指南](../../../design/deepseek-harness-operations.md)，根AGENTS与导航已记录授权/入口；修改边界＝该skill/脚本/参考、指南、根AGENTS/导航和本维护记录。固定地址及凭据变量名已保存，密码/Cookie不落盘；依据本地插件0.8.35与远端实际客户端区分Harness模型任务、SSH工作区及镜像后台任务。实测登录→模型/会话查询→创建独立会话→提示词接受→结果读取，DeepSeek回复 `DSH_REMOTE_SKILL_OK`、turn completed、running=false且无工具执行；原记录重复派发被拒绝且ID/内容未覆盖。两插件状态路由404，不宣称SSH接入；cancel及SSH同步只据源码写明，未实测运行中取消/SSH操作。证据在 `.audit/dsh-remote-skill-2026-10-07/`；skill校验/脚本编译通过，doc-check扫描224份Markdown/0问题、git diff --check退出0。未修改生产core/base/net或ENV任务状态，未运行游戏构建/规则/对拍，未安装远端插件、派发ENV生产包或提交Git。

2026-10-07 公开接口职责维护：本会话已交付[职责核对与实施边界](../../../design/net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续)，ENV-T03增加从规则声明派生出口及新增字段/Action无需net注册的验收；修改边界＝net规范/架构/契约资产与协作约定、ENV实现约束、core规则与宿主解释及本清单。明确内核公开视图是模拟语义来源，保留协议编码/句柄/账本；区分重复声明、不可推导的包元数据、wire形状限制及DecisionRequest枚举域描述缺口。doc-check扫描223份Markdown/0问题、git diff --check退出0；所改未跟踪文档另核行尾空白。仅交付规范与接续范围，生产代码只读；未运行构建/Engine/Slice/net/内容加载/对拍，未修改冻结core、协议载荷或已验收状态，未派agent或提交Git。

2026-10-07 STS2表达能力与模型一致性审阅：本会话已交付 [审阅记录](sts2-model-ability-review-2026-10-07.md)，生产源码只读；修改边界＝本维护记录、records报告/索引及 `.audit/sts2-model-review-2026-10-07/` 隔离探针。模型机制可映射到现有内核，base运行架构符合；实测默认两位小数量化使源伤害2变成3，证据并入ENV-T06，其余流程/生命周期/作者组织仍归既有任务。探针实跑退出0，doc-check扫描223份Markdown/0问题、git diff --check退出0；未改玩法或core、未重排ENV、未重跑全仓/Engine/Slice/net/严格对拍，未提交Git。

2026-10-07 建模能力边界澄清：本会话已按用户最新指示修订 [core spec](../../../design/core-the-spire/spec.md#如何判断内核具有建模能力)；修改边界＝该spec与本维护记录。明确本阶段判断模型→规则包→通用IR的表达能力，三国杀缩为三类轻量映射，不以其内容实现/行为验收作为核心前置；STS2仍是规则集开发重点。复用上轮源码/95项检查/探针证据，未改生产代码或扩展游戏任务；本轮doc-check扫描222份Markdown/0问题、git diff --check退出0、所改两文件行尾空白0项，未重跑规则套件。

2026-10-07 通用游戏模型与三国杀能力审阅：本会话已交付 core [建模与支持判定](../../../design/core-the-spire/spec.md#游戏建模与支持判定)、三国杀机制映射与源码核对；修改边界＝该spec、本维护记录及 `.audit/core-model-review-2026-10-07/` 隔离探针。Engine95/95、探针实跑；动作查询/提交不一致并入CORE-LEGAL-PENDING，实际提交者上下文、单待决策、固定订阅次序及观察边界写入spec，不据此宣称完整三国杀支持。doc-check扫描222份Markdown/0问题，git diff --check退出0，所改文件（含未跟踪清单/探针）行尾空白0项；未改生产内核/语法/玩法或ENV任务状态，未运行全仓构建、Slice/net、三国杀加载/对拍，未提交Git。

2026-10-07 实体与行为分离/具名输入原则维护：本会话已交付[实体属性原则](../../../design/core-the-spire/core-architecture-decisions.md#13-2026-10-07实体只持有属性行为由独立块执行)和[初期具名对象/输入决定](../../../design/core-the-spire/core-architecture-decisions.md#14-2026-10-07初期采用具名对象与具名输入)，撤除规则入口草案的 `blocks` 分区并补载荷说明；修改边界＝该草案、现有架构决定及本维护记录。块参照event声明输入、call按名传值、scope只指定this；Clausewitz机制留后期参考，具体关联及未定拼写继续审阅。仅修订设计，未改生产实现或其他任务状态。

2026-10-07 STS2规则配置草案维护：本会话按用户派单交付[规则入口示例](../../../examples/core-syntax-review/sts2-rules.spire)；修改边界＝`docs/examples/core-syntax-review/`及本维护记录。展示内容属性声明/目录映射和初始化顺序；`content`/`fields`及相关类型明确为待审候选。只写设计示例，未实现引用块或修改生产grammar、编译器、玩法及其他任务状态，不作内容可加载或原版规则已实现声明。

2026-10-07 core DSL语法决定维护：本会话已将用户确定的[call与scope块占位行为](../../../design/core-the-spire/core-architecture-decisions.md#12-2026-10-07call与scope求值块占位)记入现有架构决定，warning登记为CORE-DSL-WARN/P2。明确为拟实现语法，尚未修改冻结grammar/编译器；内部求值语言留待后续，不作新语法可运行声明。

2026-10-06 core DSL语法对比示例：本会话按用户要求交付[namespace文件头草案](../../../examples/core-syntax-review/namespace-counter.spire)，与[现有计分示例](../../../examples/core-choice-counter/first-counter.spire)对照；仅替换文件头并移出版本属性，声明/规则体保留现状。文件明确标注当前编译器不接受该草案；不修改grammar、编译器或冻结契约，不作运行能力声明。

2026-10-06 Diátaxis文档维护：本会话已按原站规则修订两篇首次教程，新增配置/参数提交操作指南、API参考与关系解释；README按四类导航，docs AGENTS登记写作要求，并在仓库安装Canonical分类审阅skill。六项问题已处理，教学宿主构建0错误/0警告，九个分阶段运行检查符合正文预期；doc-check扫描222份Markdown/0问题，diff-check退出0，新正文空白核对通过。范围、分类复核与原日志并入[同阶段记录](../2026-10-06/core-first-tutorials-2026-10-06.md#diátaxis-修订与分类复核)；不改冻结API、生产玩法或ENV任务状态。

2026-10-06 core教程维护：本会话已交付[第一次写规则](../../../dsl/core/first-rule.md)和[第一次写宿主](../../../design/core-the-spire/first-host.md)，更新core README/导航并附可运行示例。正文拼接、五分练习、参数提交、选择续接与空项目接入已验证；Engine95/95，doc-check与diff-check通过。全仓build/Slice被范围外 `SliceEnemyStateMachineChecks.cs:641` 等未定义名称阻断，未将Slice记为通过。具体证据见[交付记录](../2026-10-06/core-first-tutorials-2026-10-06.md)；不改冻结内核/玩法/API或ENV任务状态。

2026-10-06 T01/T02负责人审阅：本审阅会话按用户派单完成实现/声明/证据核对，结论及开放项已并入对应任务行，原证据转入[复核记录](../2026-10-06/t01-t02-completion-review-2026-10-06.md)。只改文档并新增隔离探针/日志，未改生产实现、core或参考，未提交或派agent；构建及319/95/393独立通过，末房截断实际复现。doc-check扫描211份Markdown、0问题，git diff --check返回0；日志保存在`.audit/t01-t02-review-2026-10-06/`。

2026-10-06按用户要求引入阶段目标/稳定任务ID/P0～P4、单一主责/依赖/完成条件与接手入口。六份累积报告移到records，开放范围全部留在本页；[整理前完整快照](../2026-10-06/task-list-before-handoff-cleanup.md)保留原正文与追加。历史证据按需从[records索引](../README.md)查阅，移入records不表示修复。core已关闭通用项的范围和证据见[冻结契约](../../../contracts/core-engine-baseline.md)及[关闭报告](../../archived/core-contract-freeze-2026-10-05.md)，不重开核心架构。

前次文档整理验证：`python tools/doc-check/check_docs.py`扫描208份Markdown、0问题；`git diff --check`返回0。六份迁移报告及完整快照逐正文/链接目标核对一致，原始备份SHA-256核对通过；17个小任务ID唯一，完整W八项/ENV七项保留。备份与核对结果在`.audit/docs-handoff-cleanup-2026-10-06/`。仅整理文档与链接，未运行构建/规则/内容加载/严格对拍或训练，未提交Git；上一轮95/310/393仍标历史行为证据。

2026-10-06 AGENTS规范接入：根AGENTS将任务/P优先级/认领/状态/交接及文档单一维护位置列为全仓库强制规则；docs/core/mod补模块入口，新增net/AI/tools AGENTS并引用根规则。修正core旧描述中默认继续开发/维护的歧义，冻结维护须明确派单。验证：doc-check扫描210份Markdown、0问题，单独检查7份AGENTS（含默认扫描排除的tools）链接/锚点/格式0问题，git diff --check返回0。仅改协作约定及本维护记录，未运行生产构建/规则/内容加载/对拍或训练，未提交Git。
