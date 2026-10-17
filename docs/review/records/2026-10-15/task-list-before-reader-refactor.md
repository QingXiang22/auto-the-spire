# 任务清单整理前快照 — 2026-10-15

本文件保留本次重构前的完整清单，用于核对当时的任务、状态、会话、完成条件和验证记录。以下内容是历史文本，其中的“当前”“下一步”不代表新的安排；有效状态与排期只看[现用任务清单](../../tofix/README.md)。

原正文及数字保留，只调整链接的相对路径。旧的 Unknown/Byrdonis 边界、会话“已派发”等描述可能已被较新交接替代，重构后的清单按对应证据标明差别。

---

# 当前任务、优先级与验收清单

更新：2026-10-13。总入口是[项目文档导航](../../../README.md)；本页唯一维护目标状态、任务优先级、认领、阻塞及验收。工程范围仍为 ENV-00～06 / W-01～08，下面的小任务细化这些范围，不取代或缩减父项。规划不表示已经派出 agent。

快速定位：[阶段目标](#0-当前先派env完整单人环境与源规则对拍) · [任务索引与详情](#01-core冻结后的分派与下一步) · [父项](#02-父项状态与范围映射) · [完整W范围](#1-完整w范围与既有进度当前环境优先) · [证据入口](#12-本轮实测复核与接续依据) · [更新规则](#4-认领更新与关闭规则)。

## 0. 当前先派ENV完整单人环境与源规则对拍

当前大目标是同版本原版五角色完整单人环境，玩法在 base；core 0.3为历史冻结锚点，用户2026-10-11授权的版本重构范围见CORE-AUTHOR-04。先完成 M01，再完整 Ironclad，再五角色/进阶与候选；新 AI 实验暂缓。

| 阶段 | 大目标 / 可见成果 | 完成条件 | 当前状态 |
| --- | --- | --- | --- |
| M01 | 第一条真实地图→战斗→奖励→地图流程，可从公开 Host 游玩并独立对拍 | ENV-T01～05验收；同构建/配置/seed，真实合法选择和永久写回；共同入口、严格regression和新增闭环case实际执行 | 当前唯一推进阶段；尚未完成；只是纵向里程碑，不开放完整身份或自动关闭BASE-01 |
| M02 | 完整 Ironclad 单人四幕胜负 | 必需战斗/房间/长期状态与源随机成立，八固定seed及额外完整胜利/真实死亡脚本、严格对拍和公开全程轨迹 | M01之后；必要内容依赖可提前；BASE-02未验收 |
| M03 | 五角色、全部支持进阶、完整单人可达域 | 其余四角色正常多幕胜负，声明/加载/行为/对拍/公开可用分列，所有必要内容与变体有源依据 | 后续；BASE-03未验收 |
| M04 | 原版环境候选结算 | 最终120对、各角色完整胜负、分层coverage/manifest/首个差异裁决/Release成本绑定同一构建与run_id；真实客户端未验证范围明示 | 后续；ENV-06未验收；负责人决定候选及AI下一阶段 |

P0仅用于必须立即中断当前工作的事故；目前未登记P0。当前P1是M01关键路径，P2是M02必需能力，P3是M03/M04或不阻断当前目标的维护，P4是需另行触发的后续范围。优先级表示执行先后，不代表缺陷严重程度、截止日期或可以取消的范围；阶段切换后由负责人重排P值，稳定ID不变。状态用待认领、进行中、待验证、阻塞、已验收、暂缓。

### 0.1 core冻结后的分派与下一步

**接续顺序（2026-10-11整体复排）：真实开局初始化 → 正常奖励与永久状态闭环 → 补齐房间/幕转换并完成Ironclad四幕胜负 → 其余角色/进阶 → 候选结算。** 作者契约重构与公开第一幕Boss流程已有交付，复用CORE-AUTHOR-04/T05证据，不再把同类核心重构或重复Boss回放当作下一生产包。M01仍是当前验收阶段；M02确定需要的房间、跨幕及内容依赖可接续准备，不因一个工具case未过停止全部生产。本地行为、独立双侧与公开证据分别验收，不自动开放完整Ironclad身份。

**本轮复核后接续（2026-10-13）**：三线交接已交并完成本地共同复核：solution/Engine99/Slice842/net418通过，当前严格17仍16双侧passed＋1self_checked、rc0；正常奖励新2例均在初始地图diverged、rc1。正式客户端＋新Host正常seed L105次成功提交，自然击败Byrdonis后继续营火／宝箱，106次尝试因PhrogParasite缺行为停止；源码确认其死亡召唤依T06-PR15暂缓，不扩包或替换池。下一A按T10幕定义／跨幕接续，B收口正常初始化兼容边界，C随正式能力取轨迹。用户重新打开DSH后服务恢复，三旧会话已停止并核对结果；A幕转换／B正常初始化两个新专用会话均已accepted，实际会话及单写边界见认领／task-record；派发不等于交付或验收。具体状态与范围查详情，[本轮共同复核](../2026-10-13/project-continuation-review-2026-10-13.md)保留原命令、身份和首个差异。批量卡牌／AI／敌人召唤复活继续暂缓，M01及完整环境父项开放。

**当前排期决定（用户2026-10-11加速流程指令）**：A复用已交T08.1真实新Run初始化，按已有交付补T09缺失房间、T10幕转换/终局；这些确定需要的M02流程依赖可先生产，不等批量卡牌或M01所有比较case关闭。B复用T04诊断随包核对，C随正式能力补公开交互，T02/04/05收口真实奖励、长期写回及独立闭环证据。T06接确实阻挡当前Run的最小完整机制；用户2026-10-12追加授权的角色基础原语已有交付，不据此恢复批量卡牌。T12卡牌批量补齐暂缓，现有内容先用，恢复条件见T12。已有末杀尾/地图/作者检查不重做；原重洗首个差异已修并完成严格结算，不重派排序或游标对齐，正常Run随机域仍按实际双侧范围收口。CORE-AUTHOR-04剩余验收与版本发布条件保留，T07及CORE-LEGAL-PENDING认领前核对其已有交付。warning、独立注释修复及编辑器不占真实Run主线；AI继续暂缓。

先核对对应详情的认领与已有交付；B/C使用已有dev工厂，不等整个BASE-01关闭。已有状态机/普通队列/编队/出生能力及R1/R2/R3不重做。用户2026-10-10卡牌委派授权保留，但当前接续按2026-10-11暂缓指令执行；公共契约由Codex裁决协调，敌人召唤/复活族继续暂缓；Osty基础交付不开放该敌人族。A/B/C是目录责任，实际认领与状态以任务详情为准，派发仅以对应记录为证，不扩大其他包边界。

| ID / 优先级 | 阶段、父项 / 唯一主责 | 小目标与详情 |
| --- | --- | --- |
| CI-01 / P1 | 用户2026-10-15指定；工程交付 / Codex | [core/base与严格对拍CI，参考上游升级](#ci-01) |
| CORE-AUTHOR-04 / P1 | 用户2026-10-11授权；CORE-DSL-BASE、ENV-T06/07 / Codex | [作者模型与规则库重构；引擎0.4发布](#core-author-04) |
| ENV-T01 / P1 | M01；ENV-01、BASE-02 / A | [源float加权随机集成；纠正Describe/限制文案](#env-t01) |
| ENV-T02 / P1 | M01；ENV-03、W-05 / A | [战斗余效/胜利结算尾与永久状态写回](#env-t02) |
| ENV-T03 / P1 | M01；NET-01、ENV-05 / C | [公开Host接唯一工厂；从规则声明派生公开出口](#env-t03) |
| ENV-T04 / P1 | M01；DIFF-01、ENV-00/01 / B | [严格regression接唯一工厂和独立参考](#env-t04) |
| ENV-T05 / P1 | M01；BASE-01/02、ENV-03 / A | [第一条真实地图战斗奖励闭环](#env-t05) |
| ENV-T06 / P1 | 用户指定关键修正；M02；BASE-02、ENV-02 / A | [必要战斗机制：逐效果表示/职责边界、Thorns、条件/计数、数值/生命周期](#env-t06) |
| ENV-T07 / P2 | M02；CORE-DSL-BASE、W-06 / A | [在一个真实机制中交付文本procedure规则库复用](#env-t07) |
| ENV-T08 / P1（T08.1）/ P2（余项） | M01先行依赖、M02完整范围；ENV-01/03、BASE-02 / A | [整幕初始化/全池与精英BOSS古代人、源前置随机消费](#env-t08) |
| ENV-T09 / P2 | M02；ENV-03、W-05 / A | [完整房间族与持有内容](#env-t09) |
| ENV-T10 / P2 | M02；BASE-02、ENV-03 / A | [各幕推进与Ironclad全可达战斗/内容](#env-t10) |
| ENV-T11 / P2 | M02；DIFF-02、NET-02 / B（C供公开轨迹） | [Ironclad阶段验收证据](#env-t11) |
| ENV-T12 / P3 | M03；BASE-03、ENV-04 / A | [其余四角色资源/机制/可达域](#env-t12) |
| ENV-T13 / P3 | M03；BASE-03、ENV-04 / A | [全进阶、全量单人可达内容及变体收口](#env-t13) |
| ENV-T14 / P3 | M04；DIFF-02、ENV-06 / B | [全量矩阵、候选覆盖与成本结算](#env-t14) |
| ENV-T15 / P4 | CLIENT-01 / D | [匹配真实客户端版本的取证与差分](#env-t15) |
| ENV-T16 / P4 | MOD-01 / E | [一个指定mod端到端内容包及真实对照](#env-t16) |
| MAINT-01 / P3 | W-06 / A | [损坏注释恢复](#maint-01) |
| CORE-LEGAL-PENDING / P3 | core契约维护 / 待派单 | [决策暂停时普通动作查询与提交一致](#core-legal-pending) |
| CORE-DSL-WARN / P2 | 用户2026-10-07明确排期；core维护 / 待派单 | [新语法编译warning](#core-dsl-warn) |

表格用于定位任务；状态、认领、依赖、下一成果及完成条件只在下方对应条目维护。完整ENV/W范围保留，排期按本轮关键依赖调整；后续任务尚未就绪时，不把其验收目标当作当前派单。

#### CI-01

**状态与认领**：待验证（初版三个托管CI组通过，目录整理本地验证完成，待用户PR审阅）；用户2026-10-15明确派单，Codex `/root` / `codex/core-base-ci`，目录整理使用`codex/repository-hygiene-review-20261015`经PR交付。目录边界为`.github/workflows`、`tools/ci`、`tools/differential`及参考版本登记、base检查分组、用户指定目录整理与相关文档，不修改core/base游戏规则，不编辑独立参考玩法。`contracts-python`本次只说明，不接入。

**当前小包与下一成果**：三个CI组及用户2026-10-15追加的目录整理已实施；根archive删除，artifacts与临时/退休工具退出跟踪，reference与tools边界收紧、历史引用迁移，活动规则与有效回归判别保留。[整理证据](repository-hygiene-2026-10-15.md)含无源码/参考/归档的干净副本构建、协议210及AI离线604检查。托管结果按[整理分支Actions](https://github.com/QingXiang22/auto-the-spire/actions?query=branch%3Acodex%2Frepository-hygiene-review-20261015)的具体提交核对；下一步用户PR审阅，未并入主线。

**完成条件**：固定最新已核对参考身份；独立干净检出构建和命令验证；Actions实际运行结果、日志/结算及未验证明确；本地完整检查分组不丢失。网络或上游兼容失败保留最小复现及受影响case，不将旧参考回退包装为新版成功。

**本次交付证据**：[实现与验证](core-base-ci-2026-10-15.md)记录core99、runtime841＋本地source1、作者15及纯上游严格16双侧＋1自检；实现提交fc3c736已推送，[首次Actions](https://github.com/QingXiang22/auto-the-spire/actions/runs/38686046899)三个job均success。下一步用户审阅分支，未并入主线，不关闭环境/版本父项。

#### CORE-AUTHOR-04

**认领**：待验证（实施已交；2026-10-12既有严格17为16双侧+1接口连通性自检、rc0；完整发布验收仍开放）；`CORE-AUTHOR-04-dsh-continue-20261011` / `session-00c5db45-5e96-4b1c-aa57-a6e6771109d5`已停止seq2204，Codex `/root`已完成源码/证据及定向行为复核，无进行中生产小项。[派发记录](../../../../.audit/dsh-core-author-04-2026-10-11/task-record.json)保留原请求/输入及移入records后的交接位置。用户授权仍覆盖本包core破坏性重构与版本迁移；参考和AI不纳入，父项不自动关闭。

**已有成果与原复核（2026-10-11）**：X费PaidX、单一definition/typed program、core/0.3真实文本生命周期、base/0.0.2共享/替换/清空及数值/快照、地图结果复用和0.4元数据/身份迁移已落地。Codex独立当前源码重建后作者契约34/能量10/抽牌10、15次作者例子及数值/词法反例通过，补齐旧工具与最终产物摘要不同的证据缺口。DSH最终solution/Engine99/Slice753/net416及原seed/326动作Act1回放日志核对通过，本轮未重复全跑。当日严格17项14双侧passed/1 self_checked/2未通过，与重构前相同suite及完整差异签名一致；这是历史归因证据。后续初始化、排序与参考流包已经交付，当前严格结算查[T04](#env-t04)，不再把这两个旧失败当当前阻塞；完整版本发布仍按本项完成条件收口。[负责人复核](../2026-10-11/core-author-04-owner-review-2026-10-11.md)、[原交接](../2026-10-11/dsh-core-author-04-handoff-2026-10-11-00c5db45.md)、[原任务](../../../../.audit/dsh-core-author-04-2026-10-11/task.txt)。

**依赖与并行边界**：本包生产会话已停止，复用已交效果IR/统一出牌/牌区过程与正式工厂；严格初始化后继只写tools边界，不改公共core/base或另起同类作者重构。主清单范围/排期/父项关闭由Codex负责。其他已认领内容/工具子包不扩大边界；旧内容仍可迁移加载。按根测试取舍开发选组、最终集成串行验证，不reset/检出标签覆盖共享成果。

**完成条件**：正式执行不依赖旧步骤摘要/空Program回退；第二个同类机制内容只改DSL和案例；普通/升级与清空/替换语义、实时数值/快照、缺上下文拒绝、源位置、暂停续接均有针对性检查。串行完成solution、Engine、Slice、受影响net/tools与作者例子、严格回归/公开第一幕重放；发布版本与迁移说明，按实际证据结算未验证范围，父项不自动关闭。

**降低上下文的范围复核（2026-10-11）**：本包已交主要是卡牌作者模型、类型化效果、文本复用首机制与数值/快照，不能等同于真实Run作者也已低上下文。T08/09/10的Run/房间接口、直接可调用的短工作包与跨包源时序资产仍需随流程实施收敛；文本Power钩子仍只有部分接入。当日拆分前规则包构建器约3850行，该轮三线抽样输入较大，含缓存读取，不当账单或前后效果实验。[日期复核与抽样方法](../../../../.audit/context-burden-review-2026-10-11/assessment.md)区分已交、部分交付及未验证；后续包按[短工作包](../../../../tools/base-migration-baseline/docs/base-development-guide.md#用一个短工作包限定开发上下文)由负责人先给明确接口，不要求执行者反查整套规则包构建器。当前流程生产继续，不另开全面core重构。本轮新增[房间/客户端开发入口与实际调用方](../2026-10-11/developer-onboarding-delivery-2026-10-11.md)：营火/宝箱共用上下文、公开快照访问、可运行例子和小范围关系图已交；商店/跨幕等剩余接口仍随真实流程收敛。按用户要求不再做token统计。

#### ENV-T01

**目标**：源float加权随机集成；纠正Describe/限制文案。

**状态与认领**：待验证。已有源float权重/描述及内容检查复用，不重做；A当前奖励接续包见[T05](#env-t05)。本项仍缺完整正式敌人权重独立双侧证据，T04新地图case不能代替本项，暂缓敌人范围不关闭。

**修改边界**：`mod-the-spire/base-spire/` 下的 `BaseSpire/Slice/`、`BaseSpire/ContentSource/`、`BaseSpire.Slice.Tests/`及必要文档。

**可复用成果**：53 位一次抽取、源 LINQ Sum/NextFloat/float 减法纯 Compute、版本化后端及 4 项针对性检查已交付。清单后续登记 Describe 三处描述收口、Fogmog 权重 `0.4`/`0.6`、生产前端解析真实候选权重的检查，并记录 A 实跑 Slice 324/324；这些后续自报尚不能替代负责人复核与双侧证据。

**接手先做**：A 核对已交付描述与内容侧权重检查，复用现有实现；B 按 [T04](#env-t04) 将相同后端接入独立双侧 case，C 按 [T03](#env-t03) 接共同工厂。下一项可验证的结果是同构建/配置/seed 的实际执行证据，而非重新实现加权算法。算法、文件落点见 [base T01 工作包](../../../design/mod-the-spire/base-spire-implementation-plan.md#env-t01)。

**未交付与范围决定**：共同工厂及B/C入口已接，地图生成双侧已验收；这些不能代替T01正式敌人分数权重/动态门槛的独立双侧行为证据，该范围仍缺。TwoTailedRat/Fogmog 未实际加载，其依赖和受阻遭遇见 [T06](#env-t06)；用户 2026-10-06 决定本轮不实现该族。本次收尾范围是描述收口与内容侧权重证据，不把真实敌人加载记为完成，也不删原验收条件。

**证据**：[2026-10-06 负责人复核](../2026-10-06/t01-t02-completion-review-2026-10-06.md#4-发现t01的限制描述仍误报已支持能力)记录当时 319/95 本地检查；后续修改、精确位型及自报检查保留在[整理前任务记录](../2026-10-07/task-maintenance-history-2026-10-07.md#env-t01)。 [2026-10-08当前输入复核](../2026-10-08/dsh-supervisor-review-2026-10-08.md#1639复核与三线接续)保存本轮build/95/324/393及源码定位；受控检查不升格为正常整局或双侧通过。

**完成条件（保留原合同）**：正式敌人状态机用既有SampleInt+纯Compute，真实分数权重/顺序/动态门槛/多候选/消费计数有针对性检查与B双侧case；构建身份含算法，文案反映实际能力

#### ENV-T02

**目标**：战斗余效/胜利结算尾与永久状态写回。

**状态与认领**：待验证。已有9项结算/长期来源针对性检查复用，last-kill-before-terminal双侧证据已有；A当前奖励接续见[T05](#env-t05)。真实长期写回/正常奖励闭环与完整B写回双侧仍缺，不重做已修末次击杀尾。

**修改边界**：`mod-the-spire/base-spire/` 下的 `BaseSpire/Slice/`、`BaseSpire.Slice.Tests/`及写回矩阵文档。

**可复用成果**：胜利判断移到完整动作末尾的 `BattleEndTail`，删除 `CommitIfSettled`；卡实体新增隐藏可选 `Card.LongTermSource`，复制时保存长期实例来源。A 登记 9 项结算/来源检查，build 0 错误、Slice 324/324、Engine 95/95、net 393/393。此处是清单已有交付自报，本次文档整理未复跑或重新验收。

**接手先做**：复用修复，B 按 [T04](#env-t04) 执行 `last-kill-before-terminal` 双侧 case，并核对长期实例变化；A 提供同构建的输入、源依据及写回矩阵。预期完整动作的钩子、`InPlay` 移除、消耗和结果区处理在终局前完成，同定义不同实例只写回指定来源。方法见 [base T02 工作包](../../../design/mod-the-spire/base-spire-implementation-plan.md#env-t02)。

**跨线变化与未验证**：`Run.MaxHp` 为长期权威，胜利终局后移到动作末尾；卡来源字段隐藏，规则身份及 `PublicBuildId` 已随改动更新，net 映射不因隐藏字段公开新信息。负责人须核对新构建与双侧证据后作结论，旧 319 项复核不能证明后续修复已验收。

**证据**：[原负责人复核](../2026-10-06/t01-t02-completion-review-2026-10-06.md#2-发现t02仍在完整出牌动作结束之前设终局)保存修复前反例；[整理前任务记录](../2026-10-07/task-maintenance-history-2026-10-07.md#env-t02)保存后续自报、旧行为插回的针对性检查和构建结果；有效写回约束见 [mod spec](../../../design/mod-the-spire/spec.md)。 [2026-10-08当前输入复核](../2026-10-08/dsh-supervisor-review-2026-10-08.md#1639复核与三线接续)保存本轮build/95/324/393及源码定位；受控检查不升格为正常整局或双侧通过。

**完成条件（保留原合同）**：Feed MaxHp跨房、最后击杀父效果余效、同定义不同实例永久变化成立；长期牌→战斗副本来源/写回矩阵明确，临时变化不污染长期状态

#### ENV-T03

**负责人共同复核（2026-10-13）**：正式事件策略11项及当前共同Host正常seed L105成功提交／106次提交复算成立；本包消费新Byrdonis能力，首个未支持已到PhrogParasite。使用当前合法候选、事件关闭与跨商店访问重置、Host拒绝不重发已核对；本族限定能力成立，M01／完整公开Run仍待原合同。原固定交接不移动，远端状态恢复查询后释放主责。见[本轮复核](../2026-10-13/project-continuation-review-2026-10-13.md)。

**本轮包认领（2026-10-13）**：待验证（交接已交；通用事件消费者与新Host正常105步已核验；原会话已停止seq586，本包文件主责释放）；`ENV-T03-public-event-policy-20261013` / `session-a762b7b0-79e5-4d34-b331-23a88514f140` / 全新DSH C。唯一生产边界为`net-the-spire/client`的正式运行器/通用事件策略/受影响检查和用法，固定读取`owner-dsh-wave-20261013/host`，不构建或修改Host/base/core/schema。下一成果为正式客户端正常seed L穿过已交问号事件、恢复旅行并准确报告精英内容故障；不搬Sunken专用分支、不伪造整局。边界、准确接口与命令见[短工作包](../../../../.audit/dsh-public-event-policy-20261013/task.txt)，[派发记录](../../../../.audit/dsh-public-event-policy-20261013/task-record.json)；固定交接路径以记录handoff_path为准。

**当前接续与边界（2026-10-13）**：Host声明一致性/旧切片后端补齐已有交付，最新net418通过；原DSH包完整范围仍待验证，不再将已交实现列为待写。正式`run_game.py`未接Unknown/Elite和通用事件策略，当前Host46步停在客户端Unknown边界；既有事件包装可走70步，Host本身的问号事件并未缺失。下一公开消费者范围是复用已交房间/事件能力和实时公开候选；完整整局仍受[T08/09](#env-t09)精英内容及[T10](#env-t10)幕转换约束。[本次实测](../2026-10-13/project-progress-review-2026-10-13.md)。

**横向扩展后新包认领（2026-10-12）**：待验证（会话已停止；当前共同门槛通过，原包完整范围按交接复核）；`ENV-T03-host-declarations-close-20261012` / `session-162ee024-d56e-4165-acf9-9f2350d6409d` / 全新DSH C。允许修改net控制/投影/旧Host组合和必要检查，修I71/I84公开声明一致性与旧切片SourceStableShuffle后端缺失，不改协议/schema或base。输入为负责人不可变`owner-horizontal-extension-20261012`闭包，A源码并行不影响；下一成果为同目录describe中的引用全部有效、当前416旧项通过及一条真实新Host商店契约帧。边界与命令见[短工作包](../../../../.audit/dsh-host-declarations-close-20261012/task.txt)，[派发记录](../../../../.audit/dsh-host-declarations-close-20261012/task-record.json)。

**玩家试玩入口认领（用户2026-10-12要求）**：已验收（终端客户端与固定Host交互范围）；Codex `/root`，只写`tools/base-spire-dev/play_game.py`、本目录README与公开运行指南。复用上次共同验证Host，已交终端公开状态/数字菜单、手动出牌/目标选择/购买离店及具名故障停止；[运行指南](../../../design/net-the-spire/how-to-play-base-game.md)、[独立实测](../2026-10-12/player-console-delivery-2026-10-12.md)。不修改游戏、协议或三项DSH生产成果，不重编当前未验收base，也不派新任务。

**2026-10-12新包认领**：待验证（会话已停止seq764；商店帧/消费者修正已交；Host声明ID缺口已裁定按I71/I84修实现，接本项新包）；`ENV-T03-shop-public-contract-20261012` / `session-6cbdba33-19fc-4a1d-a19d-bb6575710b01` / 全新DSH会话，实际session以[派发记录](../../../../.audit/dsh-shop-public-contract-20261012/task-record.json)为准。边界为net/client真实商店帧契约核验、只修实际消费者缺口；下一成果为同Host schema/公开字段/候选完整性与购买删除离店证据；复用负责人两份轨迹，不重做客户端或额外全套测试。[准确入口、源契约与验收](../../../../.audit/dsh-shop-public-contract-20261012/task.txt)。

**负责人复核后新包认领**：待验证（实现与当前共同Host真实购买/删牌/离店已核验，契约证据接下方新包，完整整局未验收）；`ENV-T03-shop-public-driver-20261011` / `session-a1811025-b9e4-44d0-a2f2-2f7949935ade` / 全新DSH会话，实际session以[派发记录](../../../../.audit/dsh-shop-public-driver-20261011/task-record.json)为准。边界为net/client公开Run商店策略/删牌decision/离店与用法，下一成果为当前公开候选上的客户端支持；商店真实公开轨迹只依赖T09最终Host，其他实现不等待；不改A/B生产。[完整边界与验收](../../../../.audit/dsh-shop-public-driver-20261011/task.txt)。

**新对话接续认领（用户2026-10-11重新派单）**：已验收（跨会话绑定窄包，固定Core/Base上的net416/416负责人复跑；共同构建/公开整局仍待验证）；`ENV-T03-candidate-session-binding-20261011` / `session-2e912a79-fdcd-40cf-9e19-6f08beeba395` / DSH全新candidate会话，实际session及accepted以[派发记录](../../../../.audit/dsh-candidate-binding-20261011/task-record.json)为准。独占net候选绑定实现、受影响F-05/幂等/过期检查及候选契约说明；已交相同观察与动作语义的两局拒绝外局候选且不推进，自局及幂等正常；公开语义键保留。保持语义键独立于会话；固定Base/Core输入，不改A/B生产。[完整文件边界与验收](../../../../.audit/dsh-candidate-binding-20261011/task.txt)。

**开发减负子包认领（2026-10-11）**：已验收（开发入口及当前共同net416通过；完整父项仍开放）；Codex `/root` 按用户明确派单接续，边界为client公开快照访问模块、已停止DSH运行器的共用访问迁移、独立最小客户端例子与net开发指南。保持协议/Host/既有NetClient行为；已交正常公开启动/候选提交例子、315步原轨迹决策对照及新Host有限前缀；当时的net跨会话首个差异已由上方candidate-session-binding包解除；当前消费者接续查本项首段，父项和完整整局不随此子包关闭。

**开发入口原交付证据（2026-10-11）**：[日期交付与门槛范围](../2026-10-11/developer-onboarding-delivery-2026-10-11.md)。共用访问提取后315步候选/观察对照一致；当时Host有限前缀含真实目标选择通过，当时完整net为415通过/1失败（F-05期望拒绝）。后续绑定修复已通过，最新集成net418/418，原失败只保留为修复前证据。当日源码net415/416及独立公开Host复现确认：两局观察版本/语义候选重合时，第一局候选可提交第二局；该修复遵循跨会话绑定合同，不以换seed或放宽断言关闭；响应与源码归因见[本轮复核](../2026-10-11/base-developer-review-2026-10-11.md)。

**公开整局运行器认领**：待验证（会话已停止，固定交接已交；负责人接续共用访问提取）；`ENV-T03-public-run-driver-parallel-20261011` / DSH C `session-7bab7f9a-2ad2-4389-a743-510f7bb77ca4`，实际session见[派发记录](../../../../.audit/dsh-public-run-driver-2026-10-11/task-record.json)。只写net client新Run驱动器/独有helper与使用说明，消费不可变0.4 Host；不构建或修改base/core/Host/协议/旧客户端/共享测试数据，不重复药水检查。下一成果是从实时公开候选推进战斗、奖励、地图及已支持房间并记录轨迹，明确整局胜负/第一幕完成/开发边界与失败，DLL/seed可替换。[完整边界](../../../../.audit/dsh-public-run-driver-2026-10-11/task.txt)，固定交接`docs/review/tofix/dsh-public-run-driver-handoff-2026-10-11.md`；新初始化最终构建上的复放由Codex协调。

**本轮接续认领（2026-10-11）**：待验证（会话已停止，交接待负责人复核）；`ENV-T03-potion-public-20261011` / DSH C `session-9f30503c-6ece-4270-b179-c8cb24534f47`。公开药水交互验证；只写net client新检查/实际客户端缺口/使用文档及本包证据。冻结使用负责人author-final不可变Host，核对SHA与实际describe；A内容并行不影响该DLL。下一项可验证的结果为正常领取/容量/使用/丢弃、重发/过期/非法拒绝及目标决策原始schema帧；正常与受控分列，不重做完整Boss。脚本依运行期声明，后续统一用新构建复放；无base/core/协议/身份/共享测试数据修改权。 独立输出 `.artifacts/parallel/dsh-potion-public`；[派单记录](../../../../.audit/dsh-potion-public-2026-10-11/task-record.json)、[完整边界与验收](../../../../.audit/dsh-potion-public-2026-10-11/task.txt)。共享core保持冻结，公共主清单/IR/身份/Boss回放数据由Codex独自修改；派发accepted不计交付。

**目标**：公开Host接唯一工厂；从规则声明派生公开出口。

**状态与认领**：待验证（父项；药水旧包交接待复核，当前生产包为本项新对话候选会话绑定修复）。旧C会话只交奖励准备、不计验收；后续Codex已完成正常公开奖励及Boss最小流程，不能再以“全部真实奖励未实现”阻塞整条C线。当前准备、实际证据和剩余边界见[2026-10-11复核](../2026-10-11/dsh-review-and-parallel-plan-2026-10-11.md)。

**修改边界**：C只写net/公开契约/net文档及独有audit/交接。药水公开接续等待A实际接口；其他就绪公开包可独立认领。涉及base可见性声明时由A协调该字段，不在net硬编码新的游戏字段过滤，不向旧C会话插单。

**执行依据**：[地图Host派单](../../../../.audit/dsh-env-t03-map-host-2026-10-08/task.txt)、[设计§7.3](../../../design/net-the-spire/design.md#73-env-t03内的实施与完成条件)、[A地图交接](../2026-10-08/dsh-env-t05-map-room-handoff-2026-10-08.md)、[C扩展交接](../2026-10-08/dsh-env-t03-declaration-extension-handoff-2026-10-08.md)。

**下一项可验证的结果**：最终base输入下真实Host正常地图选点→连续攻击及目标回答→胜利→真实奖励领取/选牌或跳过→长期状态→地图返回；身份表重基线、旧0.1/声明扩展兼容与载荷/拒绝/幂等同批。A受阻时交最后正常边界与准确依赖，不能将fault计正常通过。

**接手先做**：T05奖励池门槛已解除，Codex已提供正常公开地图→战斗→奖励→营火/宝箱→Boss轨迹（见T05负责人复核及固定测试数据）；复用该输入继续真实药水领取/容量、拒绝与幂等及独立双侧case。旧等待与“工厂缺卡抛异常”不再是当前阻塞。另须核对MapDraws诊断payload仍公开的边界：普通训练观察应发布地图事实，随机重放材料留诊断出口；本轮没有修改该旧字段。

**此前包负责人复核（2026-10-11）**：已验收（限定公开诊断隔离）；`ENV-T03-public-diagnostics-20261011` / DSH C `session-3b7358f5-d004-4419-a5d7-54ebbab45b49`已停止。MapDraws声明隐藏、公开派生过滤/本地显式诊断/独立Host选择已核对；负责人最终药水构建net416及正常公开轨迹补证，C旧213项Python和旧身份只作为原日期证据，本轮未重复。此前并行药水构建/容量首错由Codex收口，本包无剩余生产小项，完整T03仍开放。 [原交接](../2026-10-11/dsh-public-diagnostics-handoff-2026-10-11.md)、[负责人复核与安排](../2026-10-11/dsh-next-wave-owner-plan-2026-10-11.md)。

**本包证据（2026-10-08）**：范围入口/派生声明/两seed轨迹/拒绝与幂等/首个差异前缀见[地图Host交接](../2026-10-08/dsh-env-t03-map-host-handoff-2026-10-08.md)，使用参考见[正式宿主范围选择](../../../../net-the-spire/formal-dev-scopes.md)；原始证据在[.audit/dsh-env-t03-map-host-2026-10-08/](../../../../.audit/dsh-env-t03-map-host-2026-10-08)（根构建 rc0；C# 415 executed/415 passed（原410+地图5）；Python 地图套件182、旧客户端103、声明扩展157 全通过；两seed NDJSON 与目录形状）。C交付时`dev`身份为ruleset `4206675da0ca12f6`；A守卫修复后现为`6ac4cb3cb2b696c6`，dev-map为`a1d8aa9faa64b143`（完整摘要见本轮复核），`dev-map` 进身份；攻击首个差异 `base-spire:PowerStacksOf` 与未实现房型如实呈现，交A。

**完成条件（保留原合同）**：C#/Python/schema一致；每局字符串seed、未Start会话只启动一次、身份前置拒绝/重发幂等/同Host多seed；正常开局describe/catalog/start/step/observe公开可玩；新增公开字段/Action仅改规则包即可加载、发布、执行，net无新增逐项注册；不可编码结构明确处理，地图/奖励新增选择在T05验收

#### ENV-T04

**本轮正常初始化接续认领（2026-10-13）**：进行中（新专用会话已accepted，尚未交付／验收）；`ENV-T04-normal-initialization-audit-20261013` / `session-e524322c-614d-4142-b393-2ab4a0c80829` / 全新DSH B。只写tools适配／诊断／判别和工具用法，使用负责人99/842/418固定DLL闭包，不重编A源码；交源兼容初始化矩阵、真实公开API适配或最小bootstrap接口提案及严格首个差异，不改reference／base／core／net，不注入或改seed。准确范围、既有向量和固定输入见[本次派单文本](../../../../.audit/dsh-normal-initialization-audit-20261013/task.txt)、[派发记录](../../../../.audit/dsh-normal-initialization-audit-20261013/task-record.json)；API提案不授权参考生产修改。

**负责人共同复核与受阻范围（2026-10-13）**：当前完整源码工具构建及原17为16双侧＋1自检、rc0；新normal-reward-prefix两侧各2真实执行、2diverged、rc1。比较仅正常源兼容初始化受阻：参考构造先用XxHash64根生成地图，现有Apply不覆盖根Seed／地图／玩家流；后续奖励不同不能在已分叉路径直接归因奖励算法。下一窄包[初始化接口核对与适配提案](../../../../.audit/dsh-normal-initialization-audit-20261013/task.txt)已派发至上述新会话；只写tools，不授权修改参考／注入私有字段。药水领取／下一战斗与长期一致性仍未验收，见[实际向量与首个差异](../2026-10-13/project-continuation-review-2026-10-13.md)。

**本轮包认领（2026-10-13）**：待验证（入口／严格失败已核验；新2例初始地图diverged，正常一致性未通过；原会话已停止seq725，本包文件主责释放）；`ENV-T04-normal-reward-diff-20261013` / `session-011c4072-7363-48da-bc48-160af934e6ab` / 全新DSH B。只写`tools/differential`正常首战奖励独立双侧入口/新suite/必要检查和用法，使用`owner-dsh-wave-20261013/differential`固定Core/Base/参考DLL，临时工具工程不重编A源码。下一成果为真实首战胜利→奖励生成/领取或跳过→永久资源提交的两侧执行、完整维度与反例；旧17不改，缺参考/首个差异仍非零，不以受控状态替代正常Run。准确参考API、随机边界和命令见[短工作包](../../../../.audit/dsh-normal-reward-diff-20261013/task.txt)，[派发记录](../../../../.audit/dsh-normal-reward-diff-20261013/task-record.json)；固定交接路径以记录handoff_path为准。

**当前严格结算（2026-10-13核对）**：2026-10-12完整原17实际执行为16双侧passed+1工厂接入self_checked、0失败/自检0失败、rc0；重洗原ID已升级既有源兼容Profile，不变分母/seed/动作/终点。[实际结果](../../../../.audit/owner-five-character-primitives-20261012/regression/regression.json)早于最终Poison/Osty续包，不证明新角色原语。下列旧14/15双侧及重洗未过数字是对应包当时证据；不作为当前首个差异或重新派单依据。DSH完整反例包尚待全部独立复核，父T04仍待验证；下一双侧范围是正常Run奖励/药水/长期写回，不用受控17替代M01闭环。

**横向扩展后新包认领（2026-10-12）**：待验证（会话已停止；当前共同门槛通过，原包完整范围按交接复核）；`ENV-T04-strict-reshuffle-close-20261012` / `session-0eef0705-b034-4a87-8bfb-70880010f1a7` / 全新DSH B。独占tools/differential原case Profile结算与实际必要身份/门槛改动，使用负责人不可变闭包，不构建A源码。负责人已核对完整17及重洗无差异/维度齐备，原ID升级现有源兼容Profile已交，不变分母/seed/动作/终点。执行者完整牌序/缺流反例证据已交，下一验收为负责人完整复核该窄包，不重写Profile；正常Run和父项仍开放。[短工作包](../../../../.audit/dsh-strict-reshuffle-close-20261012/task.txt)、[派发记录](../../../../.audit/dsh-strict-reshuffle-close-20261012/task-record.json)。

**2026-10-12新包认领**：待验证（会话已停止seq935；参考逐边界流包已交，待独立门槛复核）；`ENV-T04-reference-stream-boundaries-20261012` / `session-df65ab58-df64-4989-b62c-fa24eaf92df6` / 全新DSH会话，实际session以[派发记录](../../../../.audit/dsh-reference-stream-boundaries-20261012/task-record.json)为准。边界为tools/differential参考逐边界真实streams及缺维度门槛；完整参考流证据已交；本包当时的base首个差异/严格非零保留为历史证据，后续排序/Profile结算已消除原重洗首个差异，不重做参考流适配。未核对的随机域继续待验证，不改原分母或参考玩法。[准确入口、源契约与验收](../../../../.audit/dsh-reference-stream-boundaries-20261012/task.txt)。

**负责人复核后新包认领**：已验收（语义重放窄包，当前共同Host的45步商店前缀重放通过，不声明整幕）；`ENV-T04-semantic-run-smoke-20261011` / `session-26e42e29-9e84-4751-b131-524816932c9d` / 全新DSH会话，实际session以[派发记录](../../../../.audit/dsh-semantic-smoke-20261011/task-record.json)为准。边界为tools/base-spire-dev语义重放/Host显式选择/必要fixture与用法，下一成果为同seed两新局候选ID不同仍按当前公开语义重放，并保持完整第一幕门槛；不再持久化candidate_id作为控制输入。[完整边界与验收](../../../../.audit/dsh-semantic-smoke-20261011/task.txt)。

**新对话接续认领（用户2026-10-11重新派单）**：已验收（工具侧游标对齐与归因窄包；base排序缺口未修、严格重洗仍非零）；`ENV-T04-reshuffle-followup-20261011` / `session-c4edccc4-e229-48f6-a79b-bfc8ad71c64c` / DSH全新reshuffle会话，实际session及accepted以[派发记录](../../../../.audit/dsh-reshuffle-followup-20261011/task-record.json)为准。仅tools/differential适配/诊断和本包证据，复用拆分后固定DLL闭包与已交T08初始化；已交原17项15双侧/1单侧/1重洗未过、受控游标对齐及源StableShuffle预排序缺口归因；未修base或开放Profile，仍需参考streams维度，不重做开局/地图/Power。A商店不影响固定输入，参考只读，首个差异不假定同因。[完整边界与验收](../../../../.audit/dsh-reshuffle-followup-20261011/task.txt)。

**本轮接续认领（2026-10-11）**：待验证（本工具证据包待Codex复核；仅正常开局比较阻塞于ENV-T08/base真实初始化）；`ENV-T04-startup-alignment-20261011` / `session-f68b943d-7ca1-4a97-b2d2-979be7bcc654`，仅tools/differential及本包证据/方法文档。已定位UpFront第1次同前状态但用途不同：base遭遇`[0,4)→2`，reference共享袋`[0,30)→21`；reference首普通遭遇在第261次。正常初始Shuffle九样本/前状态/牌组输入一致；不补抽/换seed，跨base机制建议交[固定交接](../../tofix/dsh-startup-alignment-handoff-2026-10-11-f68b943d.md)。最终原17/17，14双侧passed/1单侧/2未过、rc1，自检0失败；重洗仍不可判定，地图两seed保留。[完整证据](../../../../.audit/dsh-startup-alignment-2026-10-11/regression/regression.json)、[派发记录](../../../../.audit/dsh-startup-alignment-2026-10-11/task-record.json)。下一成果由负责人复核并协调真实初始化包；不关闭父项/完整环境身份。

**前包交付与剩余范围**：Power初值包`session-a89e1013-7e22-47b7-8ae2-af638525f30a`已停止，原Ticks/SkipNextTick实际对照与完整17项日志已交；其回归与CORE-AUTHOR-04最终回归两失败的语义输入/完整差异签名一致，Power相关双侧通过。原生产认领释放；源码/作者运行时负责人复核及初始随机差异比对见[本轮记录](../2026-10-11/core-author-04-owner-review-2026-10-11.md)。未因此关闭完整T04或独立重洗范围；[前包记录](../../../../.audit/dsh-power-fixture-2026-10-11/task-record.json)保留。

**Vulnerable原接续包与当前结论**：待验证（旧受控初始化/牌序阻塞已由后继包解除，原case当前双侧passed；正常Run双侧仍未交）。以下保留2026-10-11的定位证据：`ENV-T04-vulnerable-decay-20261011` / DSH B `session-fb139ad7-8c0c-4fbe-ab7a-f4ab685b00ee`已停止seq568。工具取证与比较器负例已交，当时原case保持unsupported；两侧真实Bash施加/易伤2→1一致，最小首个差异为end_turn后抽牌堆base四Strike/reference两Defend两Strike。没有缩减牌区比较、删17分母或回退Headbutt；通用受控初始化包已有后继交付，当前接续为独立正常奖励/长期写回，不将当时牌序首个差异判成易伤生产错误。[派单记录](../../../../.audit/dsh-vulnerable-decay-2026-10-11/task-record.json)、[执行交接](../2026-10-11/dsh-vulnerable-decay-handoff-2026-10-11.md)、[负责人复核](../2026-10-11/dsh-review-and-parallel-plan-2026-10-11.md)。

**此前包负责人复核（2026-10-11）**：已验收（限定完整受控初始化及独立零随机门槛）；`ENV-T04-controlled-init-20261011` / DSH B `session-d1e71cd4-9260-48ae-add0-87454e584774`已停止。负责人核对完整历史、未变工具输入及实际构造/计数门槛，独立执行原17项：13双侧passed/1 self_checked/2 diverged/1 unsupported、rc1，自检14/14。enemy-death-aftermath和vulnerable真实双侧成立；Power初值Ticks首个差异交本轮接续包修，真实重洗与正常Run初始化差异仍未通过。 [原交接](../2026-10-11/dsh-controlled-init-handoff-2026-10-11.md)、[负责人复核与安排](../2026-10-11/dsh-next-wave-owner-plan-2026-10-11.md)。

**历史完整suite证据（2026-10-11）**：CORE-AUTHOR-04最终完整17/17执行，14双侧passed/1 self_checked/1 unsupported_rng_profile/1 diverged，比较器自检无失败，严格rc1；负责人已核对原始结果并比对重构前同suite。当时两失败的语义输入与完整差异签名相同；后续初始化/排序/Profile已交，当前结算查本项首段，正常Run双侧和父项/M01仍开放。[最终实际报告](../../../../.audit/dsh-core-author-04-2026-10-11/regression/regression.json)、[负责人归因复核](../2026-10-11/core-author-04-owner-review-2026-10-11.md)。

**Headbutt子包验收（2026-10-11）**：已验收（限定独立选牌/置顶与负例）；`ENV-T04-headbutt-choice-20261011` / `session-85bdb279-671e-4458-a505-a5177525005f`已停止。原case保留ID/语义，增加第二个弃牌候选以真正调用参考选择源；负责人独立构建及正/负例确认两侧两决策、指定牌置顶、改变选择报首个差异，其他16case不变。严格子集rc1/complete=false，不关闭完整套件；本次选中case无streams时mutant/stream自检仍报1失败，作为工具自检覆盖问题保留，不算全部门槛通过。[执行交接](../2026-10-11/dsh-headbutt-choice-handoff-2026-10-11.md)、[负责人证据](../2026-10-11/parallel-main-progress-2026-10-11.md)。

**目标**：严格regression接唯一工厂和独立参考。

**准备与认领**：待验证（父项）。地图生成独立对拍子包已验收：原B会话seq648 completed，负责人工具构建rc0，独立严格17全执行10 passed/1 self_checked/5 unsupported/1 diverged、rc1；两map逐事实和流一致，两项9-case负例各rc1。B当前接续见本项首段；原受控case的未支持/初始化差异已有后继收口，正常奖励/选点多房间对拍仍开放。[本轮复核](../2026-10-09/dsh-supervisor-review-2026-10-09.md#当前内容复核与进度汇报)。

**修改边界与当前包**：已交付tools/differential地图双侧入口、17项suite和负例。B本包生产已停止，A的前置门解除；不重做地图port/manifest。后续奖励能力由A交付后，B再接真实奖励/永久写回比较，参考无模型的部分诚实记录，不扩兼容Profile。

**安装前置与本包成果**：原包已安装并加载 `auto-the-spire-handoff`、`nbj-write-clearly`、`documentation-diataxis`，复用[安装记录](../../../../.audit/dsh-env-t04-implementation-2026-10-07/skill-installation.json)。[2026-10-08 核验](../../tofix/env-t04-verification-2026-10-08.md)指出当时严格入口尚未交付；本包按[接续派单](../../../../.audit/dsh-env-t04-resume-2026-10-08/task.txt)交付 `test --suite regression`、`suites/regression.json`（15 必测）与薄 `test-env.ps1`，并实际执行：双侧 `passed` 8（原 7 个 case + `last-kill-before-terminal`）、`self_checked` 1（工厂接入）、不计通过 6（5 个 unsupported 原 ID + `rng-initial-shuffle`），比较器自检 6/6，严格 suite 诚实红态 rc=1；12 项失败反例全部返回非零。按[自动复核约定](../../../design/deepseek-harness-operations.md#半小时复核交接与接续)由 Codex 核验。

**接手先做**：读[本轮复核](../2026-10-09/dsh-supervisor-review-2026-10-09.md#当前内容复核与进度汇报)与地图独立对拍交接；原B编译门已解除。复用严格17入口、Comparator与两侧记录及A已交奖励/药水能力；比较正常领取/长期写回，不再等已交生产或停止的旧会话。

**下一项可验证的结果**：复用已验收两map初始生成case；下一证据是复用A已交真实奖励后的独立双侧奖励/长期写回；原受控unsupported已随既有17收口，不重派。10 passed是地图包当时结果，当前严格16双侧+1接入仍不关闭T04/M01；选点动作/多房间仍未比较。

**门槛修复原包与负责人复核（2026-10-08）**：门槛修复子包已验收：源码将成功门槛覆盖passed/self_checked；负责人独立运行两套完整9项负例，分别rc1/`missing_dimension`与rc1/`wrong_terminal`，均8项双侧passed、自检6/6、无--only。修复前同输入rc0，suite/比较器/原输入哈希未变，修复后DLL哈希与交接一致；正式15项仍8 passed/1 self_checked/6未通过、rc1。逐项证据见[负责人复核](../2026-10-08/dsh-supervisor-review-2026-10-08.md#门槛修复子包核验)。源随机工具已恢复并实际执行原case；窄校准与原case初始化diverged分别结算，本轮定位源/base/reference初始化首个差异；未知初始化缺口或新的规则差异保持blocked/diverged；本次不验收T02永久写回或Feed余效。

**完成条件（保留原合同）**：既有7个case实际双侧执行，5个unsupported原ID保留；缺case/零执行/缺参考/unsupported/首个差异/缺维度非零；源/reference派生与随机trace可核对

**本包证据（2026-10-08）**：入口与实测见[交接](../2026-10-08/dsh-env-t04-handoff-2026-10-08.md#验证与证据)；产物 `artifacts/environment/tests/regression/`（含 15 份 base 记录、12 份参考记录、`regression.json`）与 [.audit/dsh-env-t04-resume-2026-10-08/](../../../../.audit/dsh-env-t04-resume-2026-10-08)（派单记录、失败契约日志、支撑检查日志）。门槛修复包证据另见[接续交接](../2026-10-08/dsh-env-t04-gates-handoff-2026-10-08.md#验证与证据)：负例 fixture `tools/differential/fixtures/negative/suite-wiring-{missing-dimension,wrong-terminal}.json`、产物 `artifacts/environment/tests/gates-2026-10-08/`（六项，含两项新负例与负责人反例 suite）、[.audit/dsh-env-t04-gates-2026-10-08/](../../../../.audit/dsh-env-t04-gates-2026-10-08)（派单记录、修复前后日志、派生/核对/汇总脚本）。原交接中的5处CS0120已由C修复，负责人本轮solution构建0错误及net393通过；当前证据见[16:39复核](../2026-10-08/dsh-supervisor-review-2026-10-08.md#1639复核与三线接续)。

#### ENV-T05

**本轮接续认领（2026-10-11）**：待验证（会话已停止，交接待负责人复核）；`ENV-T05-potion-reuse-20261011` / DSH A `session-87754af2-f636-4487-9308-1ab439871a28`。复用IR的战斗药水族；仅ExplosiveAmpoule/HeartOfIron/LiquidBronze的DSL及本族针对性检查/Program入口、支持数更新，不改共享IR/Power源/规则包构建器或身份。来源能力缺口只阻塞该瓶，交Codex处理。下一项可验证的结果为正式加载及群体/后续回合/反伤真实效果，而非只看层数。 独立输出 `.artifacts/parallel/dsh-potion-reuse`；[派单记录](../../../../.audit/dsh-potion-reuse-2026-10-11/task-record.json)、[完整边界与验收](../../../../.audit/dsh-potion-reuse-2026-10-11/task.txt)。共享core保持冻结，公共主清单/IR/身份/Boss回放数据由Codex独自修改；派发accepted不计交付。

**本次接续验收（2026-10-10）**：Codex `/root` 完成 `ENV-T05-act1-basic-flow-20261010` 的正常开局至第一幕Boss最小流程，限定已验收。原[DSH交接](../2026-10-10/act1-flow-handoff-2026-10-10.md)只作为输入；已替换三遗物代表池与Boss后整局Victory，接入118项源共享随机袋、独立宝箱稀有度/金币流、营火独立模块和幕完成。固定 `ACT1-BASIC-10` 正常80HP/99金币/10张起始牌，完整85项奖励池，458次公开合法提交打完173HP/Slippery8的Vantom，Boss奖励后32HP/404金币；两次独立Host的459个公开快照去除session_id后全部一致。宝箱真实开箱后合法跳过遗物，药水奖励合法跳过；3项遗物领取另有受控效果/实例检查。并非全池/所有房型/独立原版对拍通过。职责、身份、构建与95/418/416/15门槛证据见[负责人复核](../2026-10-10/act1-flow-owner-review-2026-10-10.md)。本最小流程生产包无剩余小项；下一卡内容派发见T12。

**修改归属**：本包base SinglePlayer/Slice/正式内容/Slice检查、必要net映射与工具/文档由Codex单独顺序完成，core/参考未改。T08/T09/T10中相关改动不关闭其完整父项。以后卡牌内容交DSH，公共IR/编排/Power新机制仍由Codex维护。

**目标**：第一条真实地图战斗奖励闭环。

**状态与认领**：待验证（完整T05合同仍缺真实药水领取/容量及独立双侧正常奖励证据；最小Boss流程已验收）。本包实际卡93/Power41/敌人38，完整85项Ironclad单人奖励池已严格注册。Slippery与新增Power注册不等于所有生命周期已独立对拍。原会话 `session-e6847f17-7dd8-4229-a7b5-017cd626a5fd` 已停止；其目标与历史声明保留在原交接，不作为当前认领。其他房型、完整源前置随机消费、其余Boss及后续幕保持开放。

**接手先做**：复用 `RewardWiring`、`RestSiteWiring`、`TreasureRoomWiring`、共享Card/Power命令及[公开正常流程脚本](../../../../tools/base-spire-dev/act1_smoke.py)。下一共享机制包是源药水生成→真实实例/容量→领取/跳过/使用→长期状态与公开候选；不恢复旧奖励池缺卡阻塞，也不缩池或补抽占位。源随机初始化与房型由T08/T09接续；卡内容由T12按用户加速授权先行。剩余卡/Power族必须交独立预期的行为矩阵，不把声明数视为完成。

**负责人药水闭环交付（2026-10-11）**：待验证；`ENV-T05-potion-flow-20261011` / Codex `/root` 亲自完成。边界为base药水源/模型/前端、SinglePlayer加载、共享库存/生成与RewardWiring、必要规则包构建/检查/指南及公开接口的Boss回放数据；C声明停止后协调补旧net作者Profile兼容，MapRoomWiring.cs仍由C单写。本包正式加载完整48项Ironclad药水池，11项使用效果复用逐效果IR，37项真实元数据明确标记为不支持使用；完成领取、三槽容量、满栏保留/显式丢弃后领取、目标使用/实例消耗、奖励关闭与跨房间保留。实际奖励11/13笔随机调用，不缩池、不补抽。构建、Engine95、药水25、完整Slice738、net416已通过；最终群体作者分支另由专项及相同正式IR身份的公开复放补证。正常公开Host以326次合法提交完成第一幕Boss，8HP/401金币，含领取/使用/丢弃；两次独立Host的327个快照去session_id后一致。规格见[药水契约](../../../design/mod-the-spire/potion-inventory/spec.md)，修改/命令/身份/证据见[负责人交付](../2026-10-11/potion-flow-owner-delivery-2026-10-11.md)。本包无剩余生产小项；下一项可验证的结果是独立原版药水生成/容量/使用/随机时序双侧证据，按T04当前Power初值修复与适用随机契约接续，其余37项效果另按机制排期。完整T05/M01、其他房型/角色/幕转换仍开放。

**整批之后的T05可验证的结果**：源真实普通战斗奖励生成/领取/选牌或跳过/涉及容量、幂等→长期状态提交→地图后继；阶段门控阻止战斗/奖励未完成时旅行。同固定seed正常胜利闭环与受控分支检查分列，必要初始化依赖准确交付或标受阻，不补抽/删池假通过。

**Anger复制子包**：已验收（限定受控正式源复制叶、源核对与长期隔离）。旧会话 `session-48f37248-c0b5-4661-9174-b87dba1e5f3c` 已停止；正式源未改，复制保留创建时升级/变量/费用状态，新实例不进长期牌组、最后击杀后余效和出牌尾成立。负责人补读器的重复目的牌区/嵌套声明拒绝，实际新增10条检查；独立build0、Engine95/95、效果25/25、非地图Slice375/375、刷新工具后的作者工作流9/9。默认身份迁移为ruleset `b4d85a04422074c7` / build `5b86ab40c7f210a3` / semantics `983a2268d49ce4db`；完整Slice首次地图工厂仍报缺24卡，net410 passed/5 failed/415 executed，未做本包独立双侧或正常地图奖励闭环。详见[负责人收口记录](../2026-10-09/anger-review-and-card-batch-2026-10-09.md)与[执行者原交接](../2026-10-09/dsh-env-t05-anger-clone-handoff-2026-10-09-48f37248.md)。

**已发现本路径问题（A目录依赖）**：原PowerStacksOf挂起查询故障已在本处base守卫等价内联修复；[修复交接](../2026-10-08/dsh-env-t05-consecutive-attack-handoff-2026-10-08.md)与本轮独立公开攻击证据成立，通用core问题归CORE-LEGAL-PENDING。开局地图/战斗同时可操作及真实奖励/源初始化缺口继续本包处理；不重做已修攻击包。

**完成条件（保留原合同）**：分步交地图/房间阶段→战斗→真实奖励/选牌/跳过/容量→长期提交→地图合法后继；占位升级退出该路径，奖励幂等、同Host公开可操作与新增双侧case实际执行；正常运行不注入清场/改血

#### ENV-T06

##### 原语补全子任务

2026-10-13按用户要求补录。以下是**待补机制的唯一任务表**，取证与已有能力见[原语完整性审阅](../2026-10-13/base-primitives-review-2026-10-13.md)。P表示排期，非缺陷等级；这些子项不替代父项完成条件。主责A，具体执行会话未认领；公共符号／编译／身份仍单写，当前Byrdonis包不扩范围。每次只认领一个完整机制包，包含前端／类型表示／运行时／真实调用方／必要判别和文档；表中“依赖”只限制所用部分，不要求先实现全表。

| 稳定子任务ID / P / 状态 | 要补的机制及现有基础 | 依赖、下一项可验证的结果与完成判据 |
| --- | --- | --- |
| ENV-T06-PR01 / P2 / 待认领 | **Power状态与生命周期**：已有施加／叠层／部分回合末；补真实施加者、来源卡、实际变化量、类型化触发／修正、阻止施加、修改／移除与独立实例状态；不再用字符串前缀或目标身份猜来源 | 当前A包释放公共文件后接；用Vicious＋Artifact验证来源区分、零变化不误触发、抵消后消耗，Automation类实例计数不串；Expose／Misery所需移除与快照传播后续在同族按完整子包交付 |
| ENV-T06-PR02 / P2 / 待认领 | **生命／格挡／伤害结果与死亡边界**：已有基础伤害、格挡和末尾结算；补LoseBlock、Lose/SetMaxHp、实际伤害／致死结果、Fatal资格、伤害属性／来源、治疗和死亡防止参与者，区分Kill与Damage | 来源接口依PR01；Expose、Feed/HandOfGreed、BrightestFlame及FairyInABottle为源判别，区分格挡／HP损失、死亡被阻止、致死余效；玩家死亡防止可先交，敌人重附／复活仍受PR15暂缓约束 |
| ENV-T06-PR03 / P3 / 待认领 | **可暂停的生命周期选牌**：已有普通选牌和静态Retain；补源允许时点的hook决策、动态数量／实例资格、临时保留与清理 | 依PR01生命周期；WellLaidPlans在BeforeFlushLate按当前层数选0～N张，恢复后继续原顺序且下一回合过期；不全局放宽所有hook选择限制，普通chooseCard不重做 |
| ENV-T06-PR04 / P2 / 待认领 | **实例附魔／负面附着与动态关键词**：已有定义关键字和变量；补Enchant/Afflict的身份、叠加／替换／清除，Apply/RemoveKeyword、单回合Sly及效果寿命 | 依PR01，选择消费PR03的适用部分；Ringing挂载／新牌进入／移除清理，HandTrick只赋予选中Skill临时Sly，升级／复制／换房不串实例；附魔不得伪装Power字段 |
| ENV-T06-PR05 / P2 / 待认领 | **生成候选→选择→获得**：已有显式池及Ironclad生成；补distinct多候选、源资格／池适配、可跳过offer与未选实例清理 | 复用现有Engine决策；Discovery正常生成3个不重复候选并选择／跳过，满手落点正确，候选生成只抽一次且续接不补抽；其他角色完整池在T12内容就绪后接，不以已实现子集替代源池 |
| ENV-T06-PR06 / P2 / 待认领 | **费用修正与重复出牌上下文**：已有固定费／X／StarX／自动出牌；补费用修正寿命、X修正、重复出牌捕获值继承、资源花费hook及目标／出牌限制 | 依PR01；ChemicalX对能量X与星星X生效，EchoForm类重复不二次扣费或错误重读X，零费／自动／临时改费按源结束；复用统一出牌链，不另造重放执行器 |
| ENV-T06-PR07 / P3 / 待认领 | **领域查询与作用域历史**：已有牌区数量、数值运算／let、回合出牌历史；补按定义／实例／类型过滤的牌区查询、HP／资源条件、抽弃牌／伤害／Power来源历史及上一回合／整战斗寿命 | 来源历史依PR01/02；SoulStorm读消耗堆Soul实例数（不是历史），DeathMarch排除回合初抽牌，Bolas辨认上一回合该实例；暂停不重复记录，隐藏历史不进入公开观察 |
| ENV-T06-PR08 / P3 / 待认领 | **永久卡实例成长与降级**：已有战斗变量、LongTermSource和升级；补有来源依据的战斗实例→永久实例写入、Downgrade与修正保留策略 | 依永久牌组身份；GeneticAlgorithm/TheScythe跨两战保留成长，临时克隆不污染原牌或其他同定义实例；直接升级／降级与既有成长分别核对，不把所有战斗变量一律提交 |
| ENV-T06-PR09 / P3 / 待认领 | **星星事件参与者**：保留Gain/Lose/Set、固定费／X快照；补真实ShouldGain/AfterGained/AfterSpent参与者与触发来源 | 依PR01/06；BlackHole等按实际花费触发，拒绝获得与零值不误发，重复／自动出牌正确；正式Regent开局归T12 |
| ENV-T06-PR10 / P3 / 待认领 | **Forge完整实例语义**：保留生成与成长；补CreatedThroughForge、IsDupe过滤、次数修正、AfterForge及SovereignBlade目标／效果参与者 | 依PR01/04适用部分；源ForgeCmd＋SovereignBlade验证消耗区、临时dupe、多个真实剑实例、满手与升级；不能以CloneOf替代IsDupe |
| ENV-T06-PR11 / P3 / 待认领 | **充能球定向操作与hook**：保留五种球、槽位、Focus和基础被动／激发；补末球／指定球、指定被动目标、次数修正与Channel/Evoke参与者 | 依PR01/02；ConsumingShadow末球、TeslaCoil指定目标、Loop/EmotionChip时点可判别；保持队列、满槽激发及随机抽取顺序，Defect正式开局归T12 |
| ENV-T06-PR12 / P3 / 待认领 | **宠物参与者与目标语义**：保留Osty身份／成长／死亡保留／同实例复活／基础代伤；补宠物Power、真实攻击来源与修正、ModifySummonAmount及Summon/Revived参与者 | 依PR01/02及明确生命周期；Osty自身力量与玩家力量隔离、宠物致死与玩家反应隔离、复活先于召唤hook、两战清理；其他友方宠物按实际内容另核对，不冒充敌人召唤或自动恢复暂缓族 |
| ENV-T06-PR13 / P3 / 待认领 | **Poison完整参与者**：保留已有伤害／递减／Accelerant；补统一伤害属性／hook、合法宠物目标和死亡防止路径 | 依PR01/02，宠物范围依PR12；继续验证5/4/3及次数捕获，同时验证伤害被修改／死亡阻止后是否递减；接口存在不算参与者实现 |
| ENV-T06-PR14 / P3 / 待认领 | **Doom完整死亡与触发**：保留阈值、批次和基础阵营结算；补AfterDiedToDoom参与者、UndyingSigil时点及适用宠物／防死交互 | 依PR01/02，宠物依PR12；同批多个目标、死亡被阻止和时点改变均按源，敌人Reattach部分依PR15解除后再交；不重做基础Doom |
| ENV-T06-PR15 / P2 / 暂缓 | **动态敌人召唤／复活**：已有状态机／队列／编队／出生；补动态召唤、死亡保留／重附／复活及相应目标资格 | 延续用户对敌人召唤／复活族的暂缓；源为CreatureCmd、Reattach/Fabricator等。解除前只登记缺口；届时按实际机制拆单，验证不误判胜利、行动队列与随机流、不可击中资格；不阻塞PR16独立范围 |
| ENV-T06-PR16 / P2 / 待认领 | **行动控制与逃离**：已有固定／加权状态机；补Stun、强制下一行动、逃离与目标资格，区分逃离和击杀 | 依PR02结算边界；Whistle及CreatureCmd.Escape代表例，验证跳过／恢复行动、队列位置、逃跑不触发Fatal或伪造死亡收益；不涉及PR15暂缓的召唤／复活实现 |
| ENV-T06-PR17 / P3 / 待认领 | **牌区重排与显式洗牌**：已有移动／置顶／抽空重洗；补作者可组合的主动Shuffle与ShuffleIfNecessary、批量迁移的源顺序及相应已知位置失效 | 复用CardDraw/牌区命令；Reboot先手牌入抽牌堆再主动洗牌／抽牌，与“抽空才重洗”区分；ForegoneConclusion的选择依PR03，空域／非空域随机消费及隐藏牌序不泄漏 |

本表的P2为M02完整流程所需机制，P3为M03或后续扩展；只有正常Run的具体受阻case由负责人确认后，才把最小相关包提到当前关键路径。角色基础已经交付，以上PR09～14只补剩余语义。玩法规则仍在base；发现真实core缺口时按现有授权／冻结契约另裁决，不能凭这张表修改core。各包最终分别提供解析／链接／注册／执行、独立参考及公开证据；没有双侧或正常流程证据时保留待验证。

**架构审阅建议（2026-10-13）**：后续相关Power包优先补齐施加者／来源与实际变化量，再逐族收敛类型化Power表示及机制生命周期组装；具体依据与验证条件见[架构审阅](../2026-10-13/base-architecture-review-2026-10-13.md)。属待负责人规则包构建的建议，不扩展当前DSH认领或新增core授权。

**历史机制包的读法**：下方带日期的局部结果保留原时点，缺14/15/25张奖励牌、net旧415首个差异、旧重洗Profile和旧“下一能量/抽牌”不再是当前生产阻塞或派单；后继交付已记录在本页对应包与T03/04/05。它们仍缺的独立原版双侧不因最新集成通过而自动验收。当前在制状态与剩余公共原语以紧接的T06唯一认领为准。

**五角色与流程公共原语认领（用户2026-10-12派单）**：待验证（基础子包生产与本地集成完成，新机制原版双侧及正常角色流程未验证）；`ENV-T06-five-character-primitives-20261012` / Codex `/root`，当前无在制生产子项，不派DSH。已交任意来源药水获得、星星资源/费用/X快照、锻造生成/实例成长、五种充能球/槽位/Focus、Doom基础阈值/批量死亡、Poison生命周期及Osty身份/归属、召唤成长、治疗、死亡保留/同实例复活、基础攻击与powered格挡后代受伤。最新Osty集成为solution/Engine99/Slice839/net418通过；既有严格17的16双侧+1接入结果早于最后Poison/Osty续包，不替代新机制双侧。公开实体/集合与换战斗清理接入已交，正式Necrobinder公开流程和独立清理定向case未执行。完整宠物Power/攻击修正、召唤修正与hook、死亡防止、Forge/球/星星完整参与者及正式角色开局仍开放；后续须先细化一个实际缺口的源语义和认领，不重做基础Doom/Osty。准确接口与边界见[角色战斗契约](../../../design/mod-the-spire/character-combat/spec.md)，各阶段局部检查/身份/命令见[阶段交付](../2026-10-12/five-character-primitives-2026-10-12.md)及[10月13日复核](../2026-10-13/project-progress-review-2026-10-13.md)。修改边界仍为base公共领域/前端/编译/装配/必要检查与文档，不改core/独立参考，不恢复批量补卡或AI；已交net旧受控Profile兼容迁移保留，不开放额外net生产范围。

**原语组合重构认领（用户2026-10-12派单）**：已验收（本轮共享表示/API重构及既有机制回归，完整原版覆盖仍开放）；`ENV-T06-card-primitives-20261012` / Codex `/root`。已交SelectCardsEffect/RandomCardEffect及选中实例升级/复制/移动子程序，删除CardCommandEffect/Kind和CardCommandCompiler，诊断.author仅输入适配；Feed/Armaments+显式组合，永久RunDeck命令接奖励/商店。当前176加载定义中174通用组合，Thrash/HowlFromBeyond保留特殊机制；这不表示其余原版卡或174张均有独立双侧证据。共同99/794/418及严格原17成立，正常48步动作/记录公开状态与前批相同（会话候选ID/观察编号分列）；[交付/身份迁移与边界](../2026-10-12/shared-operations-and-card-primitives-2026-10-12.md)。本包无未完生产，不补批量卡牌、召唤复活、AI或重开core，父项不关闭。

**排序包本轮负责人结论（2026-10-12）**：已验收（限定base源排序及当前独立重洗行为）；原`ENV-T06-source-stable-shuffle-20261012`已停止。当前共同源码Engine99/Slice775通过，原重洗双侧4边界/完整维度无差异、规则身份同执行者排序交接；严格Profile仍由T04新包结算。net旧切片未装后端的三项消费缺口交T03，不回退排序。父T06/完整环境不关闭。[复核](../2026-10-12/horizontal-content-extension-2026-10-12.md)。

**2026-10-12新包认领**：已验收（会话已停止seq1273；源排序与独立重洗窄包，父项保持开放）；`ENV-T06-source-stable-shuffle-20261012` / `session-a67be89b-932f-4dc3-a4e1-6cd4be860b1b` / 全新DSH会话，实际session以[派发记录](../../../../.audit/dsh-source-stable-shuffle-20261012/task-record.json)为准。边界为base共用抽牌/重洗的源排序预处理、必要Schema/Definitions及窄编译接口/检查；下一成果为原10张首个差异与大牌堆重复实例源排序保真，随机消费/hook不变；不得改商店/core/B/C。[准确入口、源契约与验收](../../../../.audit/dsh-source-stable-shuffle-20261012/task.txt)。

**公共规则包构建解耦小包（用户2026-10-11明确派单）**：已验收；`ENV-T06-package-decoupling-20261011` / Codex `/root`，只改base `Slice`编译期组装和对应文档。`CombatSlicePackage`保留Build签名，规则迁至显式分组依赖的`Compilation`模块，房间声明/注册落在`RoomCompilation`；[模块参考](../../../design/mod-the-spire/package-compilation-reference.md)提供按修改目标的入口。[交付证据](../2026-10-11/package-compilation-decoupling-2026-10-11.md)：Engine99、Slice759、队列7、作者15及房间例子通过；dev/dev-map完整身份不变，正常Run前后124帧/59步骤相同。net仍415/416，Fogmog第59步原故障保留，文档仍4项既有#L错误。保留T08初始化/隔离商店成果，无新派发，不关闭父T06/T08/T09或既有玩法缺口。 文档跟进：已完成；Codex `/root`仅同步总入口、base README、A角色导航及拆分相关规格/指南/参考；[新增共享字段步骤](../../../../tools/base-migration-baseline/docs/base-development-guide.md#新增共享状态字段)明确声明、Bindings、初值/重置和消费者。Diataxis复核及差异见同份交付记录；doc-check仍仅4处既有错误，diff-check通过，未改生产或重跑规则，不新增派单。

**当前共享机制包认领（2026-10-11）**：待验证（生产/本地/公开Boss集成完成；独立原版双侧未执行）；`ENV-T06-next-turn-draw-20261011` / Codex `/root`。已交注册制handDrawNextTurnOnce、Engine有符号回合开始快照/抽牌数量修正/一次消费、Predator普通/升级；10项抽牌、9项既有能量、24项编排通过，最终公开Host457合法提交/26HP/404金币打完第一幕。边界为PowerRules/PowerRuleRuntime、CombatSlicePackage回合接入、next-turn-powers/cards、独立NextTurnDrawChecks/Program、Boss回放数据身份和必要指南；core/参考/AI不改，DSH只写tools/differential。独立输出 `.artifacts/parallel/owner-next-turn-draw`，主清单Codex独自修改，本包无未完成生产小项。[本轮证据](../2026-10-11/parallel-main-progress-2026-10-11.md)。旧grantPower文本钩子仍以静态规范表链接新名字；新增Power注册与卡牌引用可用，不把该旧触发器链接缺口当已修。

**并发下一回合能量包认领（2026-10-10）**：待验证（本地生产完成，公开批次集成/独立原版双侧未执行）；`ENV-T06-next-turn-energy-20261010` / Codex `/root`。已交可注册energyAfterResetOnce规则、PowerRuleRuntime的AfterEnergyReset收益/销毁及根接入，Outmaneuver普通/升级独立DSL、自定义命名空间复用与9项判别；独立构建0错误/3项既有警告，24项编排通过，[证据](../2026-10-10/parallel-next-turn-energy-2026-10-10.md)。公共C#/Program/规格指南与next-turn-powers.spire、next-turn-cards.spire、独立检查由Codex独占；DSH继续已有卡DSL/RemainingCardFamiliesChecks.cs。core/参考/net协议不改，当前小包无未完成生产；下一共享机制按具体原版卡依赖单包认领。双方独立输出为.artifacts/parallel/owner-next-turn与.artifacts/parallel/dsh-ready，主清单Codex独自修改，公开身份/整局在批次集成时统一处理；局部验证优先沿用用户授权。

**通用选牌包认领（2026-10-10）**：待验证（本地生产与公开Boss集成完成；独立原版双侧未齐）；`ENV-T06-card-selection-20261010` / Codex `/root`。边界为base `Slice/Effects` 类型化选择IR与前端、`Orchestration` 选牌/移动共享过程、规则包构建入口接入、独立选择检查和Program入口、必要身份/测试数据与文档。已交类型化选择IR、独立选牌过程、共用移动/手动弃牌hook，Acrobatics/Prepared/Hologram仅以独立DSL接入；12项局部、17项DSH新卡、24项编排回归、solution构建0错误，公开Host第一幕457提交/26HP。相同实际base程序集工厂加载154卡/41Power/38敌人，其中145张是本轮以前已复核内容加本轮8张，另9张为DSH新进行中内容，不升格为已验收。状态断言改必需ID/实际降级覆盖，身份与公开接口回放数据按实际工厂迁移；[证据](../2026-10-10/card-selection-and-ready-content-review-2026-10-10.md)。本包无未完成生产小项；下一共享机制优先下一回合资源与弃牌消费者，按依赖单包认领，不改core、net协议、参考源码。用户本轮要求加快可用成果，验证以本包针对性检查、实际注册和必要构建为门槛，重复全量回归与独立对拍延后，未执行项如实记录。

**负责人复核与公共作者边界包（2026-10-10）**：待验证（本地集成/正常公开Boss流程通过；独立原版双侧未齐）；`ENV-T06-card-author-boundaries-20261010` / Codex `/root`，本包无未完成生产小项。亲自交Power结果None、selectNone/真实空play与目标拒绝、统一Power资源注册、Stampede空候选保护；PerfectedStrike改显式受控牌组，并提供单牌三模式反馈。边界为base源前端/加载器及对应Orchestration、必要检查/身份/公开接口回放数据和指南，core/参考冻结。build0、Engine95、Slice574、net416、作者工具15；137卡/41Power/38敌人，正常公开第一幕457提交/26HP。方法见[base指南](../../../../tools/base-migration-baseline/docs/base-development-guide.md#让新增内容独立于全包大小)，[复核证据](../2026-10-10/dsh-card-families-owner-review-2026-10-10.md)。下一就绪内容族归T12，不恢复旧暂停goal。

**核心编排重构认领（2026-10-10）**：待验证（本包生产与局部检查完成；完整地图/原版双侧未齐）。用户授权Codex `/root` 亲自按sim职责重构base内部规则编排，任务包 `ENV-T06-base-orchestration-20261010`；边界为base schema/效果编译、牌区共享过程、出牌/事件/战斗生命周期、必要测试/工具与指南，core/参考冻结。已交15个职责模块及共用ExecuteCardPlay，词法环境、替代实例、逐执行历史和三处反例修复；Pillage/Havoc/Cascade复用DSL接入，生成仅支持显式完整池。协调net单文件旧测试数据字段/内部集合处置，不改协议或玩法。根构建成功，Engine95/95、编排21/21、非地图Slice396/396、作者12/12；完整Slice及net5项地图检查仍受T05缺15卡阻断（net410/415）。[规格/步骤](../../../design/mod-the-spire/base-orchestration/spec.md)、[范围/身份/实测证据](../2026-10-10/base-orchestration-2026-10-10.md)、[实际模块与读取指南](../../../design/mod-the-spire/control-flow-modules.md)。本包无未完成生产小项，后继按T05缺失机制接续；本轮停止DSH在base重构/卡牌范围的自动生产，完整父项不自动关闭。

**作者契约修复认领（2026-10-10）**：`ENV-T06-author-contracts-20261010` 已验收（限定本地共享语义与作者工作流）。Power注册改语义支持校验、自定义命名空间与跨文件加载/引用、外部消耗hook、保留成长的增量升级、完整升级初始化均有针对性检查；build0、Engine95、非地图Slice401、工具15；完整Slice/net仍受14卡奖励池门槛阻断，独立双侧与正常第一幕未验证。当前无本包生产小项；下一已认领正常流程见T05。[修复证据](../2026-10-10/author-contracts-2026-10-10.md)。

**目标**：必要战斗机制：Thorns、条件/计数、能力移除、数值/生命周期。

**本轮维护成本包**：待验证（完整地图/双侧门槛）；`ENV-T06-rule-maintainability-20261009` 由Codex亲自完成生产实现与局部验证，无未完成生产小项。边界为base `Cards/Effects/` 数值/条件编译及规则包构建接入、`tools/base-spire-dev/` 和指南/证据；保留奖励实现，未改core/net/参考玩法。规则包构建接口58→25行、数值/条件分支15→0，外部卡工作流9/9、Engine95/95、非地图Slice354/354，默认身份不变；完整地图/net仍受T05缺25卡阻断。具体依据与token声明边界见[改良记录](../2026-10-09/rule-maintainability-2026-10-09.md)，作者入口为[base指南](../../../design/mod-the-spire/base-development-guide.md)。

**攻击序列子包**：已验收（限定受控正式源行为与源核对）。`ENV-T06-native-attack-sequences-20261009` / 会话 `session-faa00660-0654-4a7b-bfae-4b852b21f288`；TwinStrike/Bash/Thunderclap新增11项针对性检查，负责人独立build0、Engine95/95、非地图Slice365/365、作者工作流9/9，默认身份未变；没有生产卡块修正。完整Slice/net仍受T05奖励池缺25卡阻断，目标域极端、独立双侧和正常完整Run不在本子包验收范围。[原交接](../2026-10-09/dsh-env-t06-native-attacks-handoff-2026-10-09-faa00660.md)、[负责人验收/工程量依据](../2026-10-09/base-readiness-review-2026-10-09.md)。

**计数模型窄缺陷**：已验收（限定受控事件计数与重复执行）。本轮共享编排以独立开始/完成事件修复同实例二次出牌的MembershipConflict；同牌三次出牌计数1+2+3、公开唯一实例保持1、回合重置0，独立规则包调用三次执行验证一次付款/目标/结果区。证据见本项核心编排记录；完整原版计数消费者/双侧不随窄缺陷关闭，core未改。

**准备与认领**：待验证。用户2026-10-09指定由Codex亲自按规格驱动设计修正，`ENV-T06-effect-ir-boundaries-20261009` 已交逐效果程序、效果编译接口、钩子/付款/目标绑定与动作收尾边界。修改边界为base领域模型/前端/规则包构建接入、必要Slice检查及文档；局部语义与默认公开路径已验证，完整Slice/net地图门槛受ENV-T05奖励对账缺口阻断，原版双侧组合case尚未执行。[规格、设计与实施清单](../../../design/mod-the-spire/effect-ir/spec.md)定义本包方法，[交付记录](../2026-10-09/effect-ir-boundaries-2026-10-09.md)保存实际证据。本包无未完成生产小项；父项其他机制仍开放。卡牌全面修正、地图性能与其他原语按后继工作包接续；架构修正由Codex亲自完成，远程认领只按本项实际登记，不恢复旧会话。本族 `summonEnemy`/死亡复活子包按用户 2026-10-06 决定“不实现、如实记录”，没有认领该子包；该决定不阻塞本族其他机制。

**依赖与受阻范围**：Fogmog/Infested 缺 `summonEnemy`；Fogmog 召援目标 EyeWithTeeth 也因 IllusionPower 未加载。后者缺 `afterOwnerDeath`、`queueRevival` 和死亡/复活生命周期；另需稳定槽位与“召唤者不进本回合队列”。TwoTailedRat 还缺 `variables`/`conditions`/`modifyEnemyVariable`/`modifyCombatState`/具名条件。源位置、槽位语义及完整原因保留在[依赖取证](../2026-10-07/task-maintenance-history-2026-10-07.md#env-t06)。T01 的真实敌人运行及涉及这些遭遇的双侧 case 保持未执行；生产前端候选权重检查不抵实际加载。

**最小复现入口**：在项目根目录按原取证命令打印 Illusion 与敌人/遭遇未支持原因；输出中的未支持是受阻证据，不算规则通过。该命令本次未复跑：

```powershell
$env:W06_DUMP_POWER='Illusion'; dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests
```

**可继续的下一步**：按 [base T06 工作包](../../../design/mod-the-spire/base-spire-implementation-plan.md#env-t06)选一个完整机制包。数值项先复现已加载 Strength/Shrink/Cruelty/Vulnerable 组合源伤害 2、实际 3 的反例，核对 base 数值实现与纯 Compute 路径；原命令和输出见[数值审阅](../2026-10-07/sts2-model-ability-review-2026-10-07.md#41-数值转移有实际反例env-t06)及[探针输出](../../../../.audit/sts2-model-review-2026-10-07/results.txt)。其他可继续项为 Thorns 站点、能力移除与计数/生命周期；不据此解冻 core 或恢复暂缓召唤族。

**逐效果参数证据与修正**：跨层审阅曾复现正式DSL `attack(3); attack(7)` 实际扣血6、预期10，`3+3`及单次10对照正确；原取证见[专项复核](../2026-10-09/base-spire-development-cost-review-2026-10-09.md#6-修改跨层专项复核)。本包已用新领域程序修正该反例，并覆盖独立能力、动态值、条件/重复、钩子和末杀收尾；正式效果不再读共享显示参数，旧诊断步骤显式兼容。受影响原版全量覆盖不据此关闭，行为范围和新构建身份见[交付记录](../2026-10-09/effect-ir-boundaries-2026-10-09.md)。

**完成条件（保留原合同）**：Thorns before站点/零伤害与格挡门控正确，移除含AfterRemoved；出牌计数按执行次数；源decimal/伤害/死亡/召唤/资源归属有对应case

#### ENV-T07

**目标**：在一个真实机制中交付文本procedure规则库复用。

**依赖与准备**：随T06或房间族实施；不先重写全部语言

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：core文本库与领域lowering在同一RuleSet链接执行，普通/升级/适用分支可区分；不能表达时给最小复现由负责人裁定

#### ENV-T08

**负责人共同复核与最小阻塞（2026-10-13）**：Byrdonis/Territorial及共享MachineState修正当前源码集成99/842/418和严格17通过；正式客户端105步含自然击败该精英并领卡／金币，完整精英特殊奖励与新精英双侧未验证。`ENV-T08-phrog-parasite`当前阻塞范围为seed L第106次合法Elite进入；依赖`ENV-T06-PR15`暂缓的死亡后召唤四Wriggler／防止提前战斗结束，不只缺攻击声明。可继续T10幕转换、已有房间与B初始化核对；不暂停全部T08、不缩源池。Byrdonis包主责待DSH状态恢复核对后释放，见[正常证据与源依据](../2026-10-13/project-continuation-review-2026-10-13.md)。

**本轮包认领（2026-10-13）**：待验证（正式加载／机制／正常公开胜利已核验；新精英独立双侧未执行；原会话已停止seq960，本包文件主责释放）；`ENV-T08-byrdonis-elite-20261013` / `session-2dd01a31-b0c3-4128-b182-fbeca7038dbe` / 全新DSH A，必要公共Power机制同时归T06，本轮唯一base生产写入者。边界为Byrdonis源声明/Territorial共享Power前端与运行时/必要base接口、局部检查与规格；不改core/net/tools/参考/其他内容。修正现有普通/进阶HP倒置，交付起始Territorial与正确拥有者阵营回合末增Strength，不消费其层数；下一成果为正式加载/行动循环及原seed L正常精英入口实际可执行。独有ArtifactsPath为`dsh-byrdonis-elite-20261013/build`，B/C不构建其源码。准确源、接口、验证与范围见[短工作包](../../../../.audit/dsh-byrdonis-elite-20261013/task.txt)，[派发记录](../../../../.audit/dsh-byrdonis-elite-20261013/task-record.json)；固定交接路径以记录handoff_path为准。

**目标**：整幕初始化/全池与精英BOSS古代人、源前置随机消费。

**本轮认领**：待验证（会话已停止seq1072，队列分离局部已核验，完整Run/精英结算仍开放）；`ENV-T08-real-run-init-finish-20261011` / DSH A `session-8dd7488f-6281-49ec-aa1a-2a4297db273c`，实际session见[派发记录](../../../../.audit/dsh-real-run-init-finish-2026-10-11/task-record.json)。Codex `/root`负责公共架构协调与复核；接续已有初始化补丁，保持新RoomCompileContext及营火/宝箱接口，先分离普通/精英预生成库存与消费者，再交真实Start初始化、首战及后续库存消费证据。修改边界为base初始化/工厂/公共接口、必要针对性检查及本包交接，不改core/net/tools生产、卡牌源或商店隔离目录。独立构建`.artifacts/parallel/dsh-real-run-init-finish`；[准确接口、源位置与验证命令](../../../../.audit/dsh-real-run-init-finish-2026-10-11/task.txt)，[初始化原交接](../2026-10-11/dsh-real-run-init-finish-handoff-2026-10-11.md)。

**接续依据**：旧接续会话`session-ba05f344-3e5a-4a40-b9b7-1267200c0c60`已核对停止seq769，缺固定交接，仅有未验收部分补丁，不恢复该会话；[旧派发记录](../../../../.audit/dsh-real-run-init-continue-2026-10-11/task-record.json)保留。更早会话`session-4d004f6e-48df-4db2-bfbf-60ecdf4c6328`服务503重试耗尽seq271；[原派发](../../../../.audit/dsh-real-run-init-2026-10-11/task-record.json)保留。两次停止均不计完成。

**先行包与排期**：T08.1 / P1，认领见上；负责人本轮根据T04正常开局受阻路径将此依赖提前，其他整幕范围保持P2。下一项可验证的结果是正式工厂从新Run开始，按源实际执行共享/玩家遗物袋、事件及必要遭遇预生成，再进入首场战斗；长期对象与后续可用池必须成立，不能用补抽、换seed或工具替换池推进随机游标。先核对[T04开局诊断交接](../../tofix/dsh-startup-alignment-handoff-2026-10-11-f68b943d.md)及原源，诊断自报尚待负责人复核，不据此认定两个失败同因。

**负责人本轮门槛复核（2026-10-11）**：普通/精英队列分离及消费者已核对，负责人当前源码独立构建与队列7项通过，旧15/30首个差异已消除。执行者完整Slice759、严格17项15双侧/1单侧/1重洗未过及正常公开三场战斗后Fogmog边界保留；本轮未重复全套或完整Run。新隐藏库存导致身份变化有Schema依据，不撤销修复以保旧常量；精英战斗/战利品、事件、商店与跨幕仍不关闭。net跨会话绑定首个差异已独立复现，按T03接续。[问题复核与base扩展教程](../2026-10-11/base-developer-review-2026-10-11.md)。

**修改边界与完成条件**：A主责base正式初始化/内容池与必要针对性检查；B只在tools比较同构建/config/seed实际开局，C消费正式工厂。保持原敌人、牌区、动作及流位置比较；实现所需的真实生命周期与池顺序后再判首个差异，不把诊断返回0当双侧通过。具体会话、文件边界及本包源范围见本轮认领和派单记录。

**依赖与准备**：复用既有普通15槽GrabBag及编队，但支持子集不等于源完整池；T04正常开局比较依赖本项，T05已交第一幕基本流程不抵真实初始化。精英/Boss/古代人与后续幕全池按原完整合同继续，不因T08.1交付关闭父项。

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：原始池/顺序/回避/补袋/派生/出生与流位置符合源；不以过滤池局部复算宣称整局一致

#### ENV-T09

##### 整局公共原语补全子任务

2026-10-13补录，主责A、具体会话未认领；与T06共用同一个base单写窗口。当前房间包及任务完成条件保留。证据见[原语审阅](../2026-10-13/base-primitives-review-2026-10-13.md)，每行先交一个完整可见机制，不逐事件复制资源算法。

| 稳定子任务ID / P / 状态 | 要补的机制及现有基础 | 依赖、下一项可验证的结果与完成判据 |
| --- | --- | --- |
| ENV-T09-PR01 / P2 / 待认领 | **永久牌组选取／变换**：已有获得／移除和房间升级；补通用永久牌组选取、随机／定向Transform、获得替换／阻止与源hook、附魔选择 | 选择复用Engine，附魔依T06-PR04；AromaOfChaos/Astrolabe/DollysMirror等验证实例身份、替换卡池及随机流、升级／附着迁移、BeforeRemoved/AfterChangedPiles，重复提交不再改牌 |
| ENV-T09-PR02 / P2 / 待认领 | **遗物实例与生命周期**：已有四种效果及Obtain；补计数／局内变量、Remove/Replace/Melt、开局／战斗／回合／房间与修正参与者，按源资格获得 | 依T06-PR01/02的适用hook；SwordOfStone按精英胜利计数并原位替换，获得／移除效果各一次；源遗物袋不按可执行子集缩小，未知效果继续失败 |
| ENV-T09-PR03 / P2 / 待认领 | **药水容量与使用生命周期**：已有生成／任意来源领取／三槽／丢弃／部分战斗使用；补动态容量、指定槽位、非奖励来源生成、多实例、Automatic/非战斗使用及全部适用获得／使用参与者 | 容量先用PotionBelt验证空槽和跨房保留；FairyInABottle依T06-PR02死亡防止，不能注册成手动药水；Alchemize与满栏拒绝保持源随机及实例清理。剩余药水按所缺机制消费其他PR，不另造37个原语任务 |
| ENV-T09-PR04 / P2 / 待认领 | **跨战斗／房间资源命令**：已有基础金币／HP；补Set/LoseMaxHp等源操作、金币来源与Spent/Lost/Stolen历史、真实修正参与者，以及战斗内容调用Run资源的共享入口 | 依T06-PR02与T09-PR02适用参与者；HandOfGreed致死获金、失去上限与跨房提交、购买与被偷的历史区分；每个值唯一权威，不能把Run字段与战斗副本相互覆盖或重复获利 |
| ENV-T09-PR05 / P2 / 待认领 | **事件子流程／奖励容器与资格**：已有普通Continue/Finish；补事件战斗挂起／返回、可组合自定义奖励和多页选项，以及源池身份独立于模块实现的资格判定 | 复用T02结算、T04正常奖励证据；BattlewornDummy返回原事件、DenseVegetation不返回、奖励领取／跳过／失败不重复关闭或写回；未实现事件仍需正确资格，不能恒true掩盖原版池选择差异 |

幕定义、跨幕初始化／收尾与整局终局继续由[T10](#env-t10)承接，不另造“幕转换原语”重复任务；T09各包的公开候选和身份变化由C接同一正式工厂。

**当前正式流程边界（2026-10-13）**：问号判定与SunkenStatue入水路径已有交付；当前Host正常70步公开轨迹已通过事件、下一商店及第三场战斗奖励，进入精英时报错并指出不支持的对象于`byrdonis-elite`缺`byrdonis`行为，依赖[T08](#env-t08)/必要敌人机制。SwordOfStone选项、未实现事件/持有内容及Fogmog/Cubex等原范围继续保留。正式客户端Unknown停止归[T03](#env-t03)消费者策略，不重复实现问号房；完整T09仍待验证，不以本前缀关闭房间族。[当前实测与原始帧](../2026-10-13/project-progress-review-2026-10-13.md)。

**领域命令与规格落地认领（2026-10-12）**：已验收（本批普通选项与当前资源范围）；Codex `/root` 先交[事件领域契约](../../../design/mod-the-spire/event-domain/spec.md)，实施`Domain/Run/RunResources`、`ChoiceEventModule`及能力绑定，迁移Sunken与两个独立例子。内容叶不再手写Role/访问守卫/金币和生命字段/死亡尾。当前共同99/789/418、原17严格及正常seed L问号→事件→下一商店成立，致死/继续/关闭/金币hook顺序与一次求值有4项判别。A/B/C均已停止，交付保留，本包不恢复旧会话；完整金币参与者/伤害修正/遗物/事件战斗与跨幕按实际后继机制继续，不关闭T09/M01。[交付与未验证](../2026-10-12/event-domain-delivery-2026-10-12.md)。

**共享方法审阅与规范维护（2026-10-12）**：已验收（静态审阅与文档规范；生产封装未实施）；Codex `/root` 按用户要求审阅已有公共能力的实际复用和重复领域操作。只改mod协作规范、开发指南/接口参考、本条及[日期审阅](../2026-10-12/shared-domain-method-review-2026-10-12.md)，base与源规则只读。已交可直接查找的公共能力入口；生产接续先在本项统一遗物获得、迁移奖励/宝箱等资源调用，按真实事件需要补永久牌组命令（归T06）及任意来源药水入栏。不另建任务或派发会话。doc-check扫描449份，只报两份旧交接4处无效锚点；diff-check通过，未重跑规则套件或新增Run证据。

**公共操作实施认领（用户2026-10-12派单）**：已验收（本轮公共操作重构与现有支持范围回归，完整房间/遗物一致性未关闭）；Codex `/root` / `ENV-T09-shared-operations-20261012`。商店/宝箱/奖励/营火/事件接同一RunResources，LoseGold/GainMaxHp与RunRelics.Obtain集中语义，抽取、价格和来源关闭保留。3项新增整局判别及原正式房间/药水检查在完整Slice794中通过；共同Engine99/net418、严格17及正常seed L48步前缀成立。金币/生命/遗物全参与者、叠加/替换/移除和新来源原版双侧仍开放；[交付与下一接手方法](../2026-10-12/shared-operations-and-card-primitives-2026-10-12.md)。本包无未完生产，无派发，M01/T09不关闭；后继仍按必要事件/持有内容与跨幕接续。

**sim架构对照维护（2026-10-12）**：已完成（源码组织审阅，非运行验收）；Codex `/root` 对照本地sts2-sim的Run/房间/注册/命令与base已验收接口及A开发中模块，给出普通事件扩展路径、共享命令/生命周期缺口与建议顺序。当前A/B/C派单范围不变，未测开发耗时、未重跑规则或验收A半成品。[源码定位与架构建议](../2026-10-12/sts2-sim-flow-extension-comparison-2026-10-12.md)；建议沿本项/T10后续实际流程收敛，认领实施另按主清单。

**横向扩展接口认领（2026-10-12）**：已验收（编译接口与受控例子）；Codex `/root` 按用户本轮要求核对卡牌/怪物自动资源注册，交 `Slice/Rooms` 地图入口与事件模块注册、`MapRoomWiring` 通用分派及两个独立文件事件例子/对应指南。当前共同Engine99/Slice775、例子选择隔离/永久写回/关闭与反例通过，房间拆分身份不变；未实现真实问号概率/事件池，不抵原版房间验收。[交付与未验证](../2026-10-12/horizontal-content-extension-2026-10-12.md)。

**真实问号流程新包认领（2026-10-12）**：待验证（会话已停止；当前共同门槛通过，原包完整范围按交接复核）；`ENV-T09-unknown-room-modules-20261012` / `session-b4f2d691-688b-494a-83d2-ed06ad4b5ad9` / 全新DSH A。负责base问号概率/真实房型、现有源事件队列消费、事件访问生命周期与逐事件独立模块；公共房间接口只有本包编辑，B/C仅用固定DLL。固定seed `ENV-T03-SEED-L`已真实通过原Unknown中断点、永久写回并恢复旅行；原包完整范围按交接继续复核，正式客户端策略与下一精英内容边界见本项首段，不重派问号判定；不缩池/换seed/伪空事件。修改边界、源入口和验收见[短工作包](../../../../.audit/dsh-unknown-room-modules-20261012/task.txt)，[派发记录](../../../../.audit/dsh-unknown-room-modules-20261012/task-record.json)。T10幕转换后继保留，一线一包，不并发改公共编排。

**本轮共同门槛复核（2026-10-12）**：A已停止seq1944且修正轮completed，固定交接已核验；共同构建0错误/5警告、Engine99/Slice770/net416通过。源无色64项顺序与39/25稀有度逐项核对相符。正常公开seed `ENV-T03-SEED-L`已分别完成买卡（金币111→63）与删牌（111→36、次数0→1、长期牌少1），均离店并继续下一战斗奖励；后续在Unknown节点明确停止，不记整局通过。完整父项与遗物/Eternal/后续房间范围保留。证据见[本轮负责人复核](../2026-10-12/project-owner-review-2026-10-12.md)。

**新对话接续认领（用户2026-10-11重新派单）**：已验收（当前支持范围商店生产与基本公开流；完整房间/遗物/Eternal规则仍开放）；`ENV-T09-shop-integration-20261011` / `session-476a4e3f-6c45-4b2c-974c-91c215c7da85` / DSH全新shop会话，实际session及accepted以[派发记录](../../../../.audit/dsh-shop-integration-20261011/task-record.json)为准。本包独占商店生产模块、RoomCompilation/CompilationBindings/CombatPackageComposer仅商店接口、MapRoomWiring仅Shop入口、SourceActMapCompute仅商店分派、RewardWiring仅已有赔率符号的只读出口及shop检查/用法文档；复用隔离成果与RoomCompileContext，下一成果为正常地图可达、购买/删牌/离店及拒绝后仍可继续的正式商店。T08初始化已有交付不重做；公共模块本轮只由此包编辑，Codex负责接口审阅及最终集成门槛。[完整文件边界与验收](../../../../.audit/dsh-shop-integration-20261011/task.txt)。

**目标**：完整房间族与持有内容。

**开发减负子包认领（2026-10-11）**：已验收（开发入口/例子与当前共同Slice770通过；完整房间族仍开放）；Codex `/root` 按用户明确派单负责编译期Run/房间上下文、营火/宝箱接入迁移、独立可运行房间例子、准确接口参考及小范围调用关系图。T08执行会话已查询停止且未有交接，保留其初始化补丁；不恢复该会话、不改其机制或商店隔离目录。修改边界为base上述接口、docs/examples、两条开发路径和必要导航；串行隔离构建/受影响门槛。已交同一Engine状态上的进入/守卫/关闭例子与局部行为证据；下一验收依赖T08队列契约收口后的完整门槛。不新增token统计，不以文档交付关闭真实房间族。

**开发入口交付证据**：[日期交付与未验证](../2026-10-11/developer-onboarding-delivery-2026-10-11.md)。RoomCompileContext已接入营火/宝箱；[操作指南](../../../design/mod-the-spire/how-to-add-a-room.md)与[接口参考](../../../design/mod-the-spire/room-compile-reference.md)提供独立可运行例子及调用关系。受控局部通过不关闭T09，完整门槛首个差异随T08接续。

**流程接续决定（2026-10-11）**：用户本轮明确授权并发，初始化与商店是不同完整机制，分别一包一个主责。T08.1保有base公共编排/工厂接口；商店隔离生产，Codex在初始化交付后串行接入正式流程。其他精英/事件/古代人按就绪依赖接续，复用营火/宝箱/药水库存，不先补齐卡牌。父项验收合同保持，隔离模块不算正式入口已可达。

**商店并发认领**：待验证（会话已停止seq2309，隔离交接已交，尚未正式集成）；`ENV-T09-shop-flow-parallel-20261011` / DSH商店专项 `session-7fab171b-624d-4018-8a74-2c97e4fc8959`，实际session见[派发记录](../../../../.audit/dsh-shop-flow-2026-10-11/task-record.json)。唯一生产目录`mod-the-spire/base-spire/workpacks/shop-flow/`，交完整Engine库存/价格/购买/删牌/离店模块与独立行为证据；不写正在构建的BaseSpire源码树、公共Program/工厂/MapRoomWiring。下一成果是可直接集成的生产模块与明确接口，正式入口和完整集成由Codex串行收口，未接入前保持待验证。[派单边界](../../../../.audit/dsh-shop-flow-2026-10-11/task.txt)，[商店原交接](../2026-10-11/dsh-shop-flow-handoff-2026-10-11.md)。

**正式集成前接续要求**：隔离13项负责人复跑通过，但尚不能直接验收商店。先修复连续两次Candidates覆盖以及普通操作失败使整局Faulted的问题；复用RoomCompileContext和当前完整48项药水库存，不按旧冻结DLL的两瓶现象重复补T05。无色源顺序及遗物效果边界仍需核对，随后串行接地图/动作/公开语义并交正常轨迹。依据见[问题复核](../2026-10-11/base-developer-review-2026-10-11.md)。

**依赖与准备**：房间结构依赖T05；B/C随族接入

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：子包T09.1营火、T09.2商店、T09.3宝箱/药水/遗物获得使用容量、T09.4古代人、T09.5事件/事件战斗返回；每族从正式入口可达，资源写回/选择/生命周期及双侧case成立

#### ENV-T10

**本轮幕转换接续认领（2026-10-13）**：进行中（新专用会话已accepted，尚未交付／验收）；`ENV-T10-act-transition-20261013` / `session-fa7f87ac-e285-452a-801a-ad4604e6c431` / 全新DSH A。本轮唯一base生产写入者，负责SinglePlayer／Run／Map／Encounter编译与必要幕声明、相关Slice判别及文档；交第一幕参数化→源默认下一幕真实生命周期，长期库存保留、房间幂等键／随机域／队列消费正确。不得实现暂缓召唤／复活或扩其他角色；独有ArtifactsPath与准确接口见[本次派单文本](../../../../.audit/dsh-act-transition-20261013/task.txt)，[派发记录](../../../../.audit/dsh-act-transition-20261013/task-record.json)，accepted不等于交付。

**架构审阅建议（2026-10-13）**：本项开工先让现有第一幕消费显式幕定义，再实现跨幕生命周期；完整源池、随机域与长期状态归属须贯穿，见[架构审阅](../2026-10-13/base-architecture-review-2026-10-13.md)。保留现有排期与单写边界。

**目标**：各幕推进与Ironclad全可达战斗/内容。

**流程接续决定（2026-10-11）**：与T09就绪依赖接续，优先把当前`ContinueToNextAct`显式未实现出口变为真实幕转换，保留长期状态、正确生成下一幕地图/队列并接最终事件/死亡。生产可先接确定需要的M02流程，不等待批量卡牌与全量矩阵；本线仍一包一个主责。阶段资格与双侧验收不因此提前开放，不把第一幕完成伪装整局胜利。

**当前状态与依赖（2026-10-13）**：进行中（本轮新包认领见上）；[幕转换完整短包](../../../../.audit/dsh-act-transition-20261013/task.txt)已accepted至上述新会话。Byrdonis生产交接已通过负责人本地／公开核验，原会话停止seq960，文件主责已释放给本轮专用A；`ContinueToNextAct`仍显式返回未实现。Byrdonis包已复核并确认原会话停止，base公共文件由本轮专用A单写；其他base机制包等待该窗口释放。复用T06/08/09已交公共能力，补长期状态保留、下一幕地图/队列与终局；精英/事件具体内容依赖按对应任务处理，不重做第一幕最小Boss路径。[源码与复核边界](../2026-10-13/project-progress-review-2026-10-13.md)。

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：分幕交付：第一幕闭环→后续幕/幕转换→最终事件；正常四幕胜利/真实死亡，最后击杀余效/提交完整，跨角色可获得内容和变体仍计入域

#### ENV-T11

**目标**：Ironclad阶段验收证据。

**依赖与准备**：随T10准备，验收依赖全部M02能力

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：ENV-IRONCLAD-00～07严格执行，另有完整四幕胜利/死亡脚本与必要机制定向case；同正式构建公开完整轨迹；负责人批准后才开放Ironclad身份

#### ENV-T12

**原语依赖入口（2026-10-13）**：本项消费[T06原语补全表](#原语补全子任务)中的PR03～14及PR17等适用能力；星星／Forge／Orb／Osty／Poison／Doom的剩余实现只在T06登记，不重复认领。基础能力已交与内容未补、正式开局未接、独立证据缺失分开结算；完整审阅及具体源消费者见[原语审阅](../2026-10-13/base-primitives-review-2026-10-13.md)。

**架构审阅建议（2026-10-13）**：正式角色开局前，将共享内容目录与角色配置／开局计划分离，并收敛机制的声明、生命周期与公开投影接入，见[架构审阅](../2026-10-13/base-architecture-review-2026-10-13.md)。本建议不恢复批量卡牌或另行认领。

**公共原语与本项关系**：角色基础能力由[T06唯一认领](#env-t06)维护；星星、锻造、球、Doom、Poison与Osty基础交付和待验证边界查该条及[阶段记录](../2026-10-12/five-character-primitives-2026-10-12.md)，不在本项另列下一生产包或复制测试基线。基础原语存在不等于其余四角色已具备正式开局、全内容或正常整局；T12父项与批量卡牌暂缓决定保留。

**五角色卡牌校对清单维护（用户2026-10-12派单）**：已完成（仅本次参考文档维护）；Codex `/root`交付[五角色源卡与实现证据快照](../2026-10-12/five-character-card-catalog/README.md)，含439张角色卡及14张共享衍生卡的中文普通/升级描述、费用/关键字、源码/base链接与机制线索。实际正式覆盖入口178声明/176注册；按原版池对齐后，五角色144声明/143注册，本次定位110张本地行为证据，33张注册卡保持逐卡行为待核对；不以注册关闭原版验收，577完整分母与父项保留。取证脚本/源摘要/校验在`.audit/owner-five-character-card-catalog-20261012/`，453行及源模板/数值/动态条件校验通过；doc-check扫描461份，仍只有两份既有DSH交接的4处#L锚点失败；git diff --check通过。未修改卡牌/core/公共原语，未重跑全量规则或对拍，未派发会话；该清单为日期快照，不另维护排期。

**目标**：其余四角色资源/机制/可达域。

**当前排期**：用户2026-10-11要求卡牌批量补齐先暂缓，优先开发真实整局流程；不恢复本项旧卡牌会话、不新派独立卡牌族。已有交付与577项完整分母保留，下面记录的待验证能力不作废。流程包只处理阻挡当前正式入口的最小内容依赖；Ironclad正常多幕流程交付后，由负责人按用户本次指令和届时缺口重排卡牌接续。

**用户授权的先行内容包（2026-10-10）**：`ENV-T12-card-content-families-20261010` 待验证（43张本地交付已复核，独立参考/client比较未执行）；原会话 `session-70c7f4c9-c106-4fbc-9fe6-c28ceccd75bf` 已按用户要求暂停，不恢复其goal。[原派发记录](../../../../.audit/dsh-card-families-2026-10-10/task-record.json)、[执行者交接](../2026-10-10/dsh-card-families-handoff-2026-10-10.md)、[负责人复核](../2026-10-10/dsh-card-families-owner-review-2026-10-10.md)。源分母仍577项，角色/Colorless/Status/Curse分类补齐目标不变，卡内容不等同于其他四角色完整环境。按完整机制交付，不凑每族8～12张；共享IR/生命周期仍由Codex亲自负责。


**此前执行者交付**：family01–05累计43张/129项普通、直接升级、真实Armaments升级检查，负责人独立重跑及源核对通过；新增关键词时序和集成证据见上述复核，不复制历史逐族流水账。Power结果区/无目标/Dexterity资源三项已由T06公共包解阻；选牌、下回合资源等共享机制仍开放，只阻塞实际依赖卡。原有Toxic与WellLaidPlans的声明仍未加载，不以声明数冒充实现。

**后续就绪内容包（2026-10-10）**：已验收（限定正式源/本地行为，独立原版双侧未执行）；`ENV-T12-unblocked-card-families-20261010` / 新会话 `session-f9cd8a61-d601-461f-9a07-d845a82c8e54`，沿用Harness默认模型。[派发记录](../../../../.audit/dsh-card-boundaries-content-2026-10-10/task-record.json)。先交Footwork/Prowess/EternalArmor/Debris四张解阻卡及后续收益/结果区判别，再按完整机制继续其他就绪族。负责角色/共用卡DSL、必要同语义Power组合和新族检查；禁止公共C#、core/net/参考、身份与公开接口回放数据。完整范围见[派单文本](../../../../.audit/dsh-card-boundaries-content-2026-10-10/task.txt)，执行者交接已并入records；执行者仅维护本包认领段。执行会话已结束，负责人已独立核对源与17项本包收益/时序/X判别；本地加载与当前构建公开Boss重放见[接续复核](../2026-10-10/card-selection-and-ready-content-review-2026-10-10.md)。完整父项与全577项保持开放。

**此前执行者交付**：已验收（上述限定范围）；`ENV-T12-unblocked-card-families-20261010` / `session-f9cd8a61-d601-461f-9a07-d845a82c8e54`。本地已交两个完整机制包：Footwork/Prowess/EternalArmor/Debris（10项数值+11项后续收益/时序）及Skewer（三升级模式各测X=0/1/3，3项聚合数值+3项逐击检查），见[固定交接](../2026-10-10/dsh-unblocked-cards-handoff-2026-10-10.md)与[577项分层矩阵](../../../../.audit/dsh-card-boundaries-content-2026-10-10/coverage-matrix.md)。最终Engine95/95，默认Slice先实际通过本族156项后停止于旧Status源固定数量断言expected2/got3；旧检查、公共身份和公开接口回放数据未修改，数量/身份迁移交Codex集成。负责文件仅本包卡DSL、新族检查/最小Program入口、`.audit`与本认领/固定交接；负责人已复核两个包（11项后续收益/时序及6项X/逐击检查），集成了Status数量门槛；独立对拍未执行。后继LegSweep归新就绪包；选牌/下回合/实时资源/Curse/Orb/Poison/Osty/Stars最小源卡依赖已登记。完整577项与单人资格保留，不恢复旧goal，T12父项不关闭。

**就绪内容包接续（2026-10-10）**：待验证（会话已完成，完整原版双侧未齐）；`ENV-T12-ready-content-20261010` / `session-d9b9a494-16ca-497a-8507-70c6d66d5109`。29张新卡和普通/升级局部证据已交，负责人独立重跑7张21项代表检查；源依赖与整批未验证范围见[执行者交接](../2026-10-10/dsh-ready-content-handoff-2026-10-10.md)，后续集成/公开Boss见[本轮负责人证据](../2026-10-11/parallel-main-progress-2026-10-11.md)。卡DSL/RemainingCardFamiliesChecks为原边界，公共IR/Power/身份/测试数据由Codex维护；旧会话已停止，不恢复旧goal或重新扫描就绪牌。577完整分母保留，当前176实际注册卡不算全卡语义验收。下一内容包须按具体缺失机制就绪后接续，公共共享机制优先由Codex处理；本轮并行B线认领只见T04。

**依赖与准备**：共享Run和Ironclad稳定后；用户可按内容目录拆分

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：Silent/Regent/Necrobinder/Defect各为一个角色包，逐包交真实开局、多幕胜负及五种覆盖证据；未知来源/unsupported不静默省略

#### ENV-T13

**原语覆盖收口任务 ENV-T13-PR-AUDIT / P3 / 待认领**：由负责人统筹A提供实现清单、B做独立验证；在上述机制包交付后，按完整577项源卡及敌人／遗物／药水／事件／古代人／附魔的单人可达范围逐族建立“源行为→领域操作／hook→DSL／编译接入→实际行为→双侧证据”映射。当前[调用导航清单](../../../../.audit/base-primitives-review-20261013/inventory.json)只覆盖静态命令调用，不是完整性证明；需补直接属性改写、hook覆写、任务牌／奖励／地图修正和源资格等遗漏维度。每个缺口归并现有机制包或登记新完整包，已支持组合只补内容／证据，不新增同义原语；多人专用、表现层及明确不支持域按原ENV合同记录，不能删分母。验收为所有纳入范围的行为有可追溯去向，未知／未验证显式保留，不用调用计数或测试数量声明完整。

**目标**：全进阶、全量单人可达内容及变体收口。

**依赖与准备**：随T12；Ironclad合同先要求的进阶能力提前到M02

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：全部支持进阶实际可配置；源分母/排除理由/待核可达性逐项处理，不继承参考缺内容的缩水范围

#### ENV-T14

**目标**：全量矩阵、候选覆盖与成本结算。

**依赖与准备**：随M03准备；关闭依赖M03及C全出口

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：五角色×进阶0/5/10×八seed共120对，每角色完整胜负/必要房间证据；manifest/首个差异/覆盖/公开轨迹/Release成本完整，交负责人审查

#### ENV-T15

**目标**：匹配真实客户端版本的取证与差分。

**依赖与准备**：候选稳定后另行派单

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：可复现真实记录采集/导入/逐步比较；缺客户端/记录保持未验证，不用源码审计抵实测

#### ENV-T16

**目标**：一个指定mod端到端内容包及真实对照。

**依赖与准备**：候选稳定且用户指定mod/版本/范围后

**接手依据**：按[本线完整交付合同](../../../design/post-core-agent-assignments.md)与 [ENV 验收范围](../../../design/environment-completion-and-differential-v1.md)进入本包；开始前登记执行会话、目录、当前小包和下一成果。

**完成条件（保留原合同）**：同接口加载到终局，与真实游戏加载同mod比较；原版回归保持

#### MAINT-01

**目标**：损坏注释恢复。

**依赖与准备**：随触及机制段修复；独立全文件修复后排

**完成条件（保留原合同）**：严格UTF-8局部补丁，保留事故备份，字面量/行为不受损；不阻断真实Run主线

#### CORE-LEGAL-PENDING

**目标**：决策暂停时普通动作查询与提交一致。

**依赖与准备**：待认领；原暂停期LegalActions/Step不一致证据保留。[2026-10-09复核](../2026-10-09/dsh-supervisor-review-2026-10-09.md)补充挂起期守卫CallPure因DrainTo不推进引发SessionStateInvalid/Faulted的真实Host反例；base本处已等价内联修好。core按冻结契约另行裁定，不阻塞本轮B/A/C接续。原隔离复现与命令见[证据](../../../../.audit/core-model-review-2026-10-07/results.txt)。

**完成条件（保留原合同）**：暂停时对外选择与可接受命令一致；保留有效决策及续接，Running/Terminal/Faulted行为明确；按冻结契约裁定语义版本与迁移，完成针对性检查/API文档

#### CORE-DSL-WARN

**目标**：新语法编译warning。

**依赖与准备**：**待认领**；先接本轮call/scope新语法实施；规范见[语法审阅决定](../../../design/core-the-spire/core-architecture-decisions.md#12-2026-10-07call与scope求值块占位)。修改边界＝core编译诊断及必要检查/参考，不改变现有A/B/C接续顺序

**完成条件（保留原合同）**：空/非空scope求值块均提示内部已忽略、使用调用位置this，诊断包含源位置且仅在编译期输出；未知名称/内部未来语法不被绑定或执行，外层block仍正常绑定；其他warning按实际作者需求接续

任务ID在优先级变化时不改名。每项只有一个主责；协作方提供接口/证据，不并行修改同一机制大文件。认领时在对应条目“准备状态/认领”写执行会话、负责目录、当前小包和下一成果，不能只写“已开工”。每条执行线同时最多一个机制包进行中；真实阻塞记录依赖ID和最小复现，负责人重排队列，不让整条线因一个case停下。

### 0.2 父项状态与范围映射

| 父项 | 当前状态 | 完整范围与关联小任务 |
| --- | --- | --- |
| ENV-00 / DIFF-01/02 | 部分工具交付，未验收 | 源基线/完整分母/严格suite/独立比较与候选结算；T04/11/13/14 |
| ENV-01 | 部分随机Profile交付，未验收 | 游戏seed、独立流、调用顺序、源数值/trace/身份保真；T01/04/06/08 |
| ENV-02 / CORE-DSL-BASE | 正式战斗子集，未验收 | 高级声明→同一Engine IR、真实文本规则库、完整战斗机制；T06/07/10 |
| ENV-03 / BASE-02 | dev连续战斗，未验收 | 真实地图/所有房间/长期实例/余效/四幕Ironclad胜负；T02/05/08/09/10/11 |
| ENV-04 / BASE-03 | 后续，未验收 | 五角色/全部进阶/全量单人可达域与来源；T12/13 |
| ENV-05 / NET-01/02 | 正式工厂及地图公开子包已接，完整出口未验收 | 完整当前公开事实/候选/已知位置及公开全程；T03/05/09/11/12/14 |
| ENV-06 | 旧verify为incomplete，未验收 | 全量矩阵/有效覆盖/源裁决/成本/候选及真实性边界；T14/15 |
| BASE-01 | 工厂部分交付，未验收 | 真实开局/同工厂消费者/正式奖励地图/身份与源证据，满足完整合同后申请；T01/03/04/05 |
| CORE-ENV-INTEGRATION | 开放，由base/tools/net接续 | 工厂、源随机与事件/观察对拍；T01/03/04/08 |
| CORE-EDITOR | P4后续，未交付 | 共享grammar高亮/折叠/诊断/补全/跳转；不阻断文本包运行，需独立派单 |

## 1. 完整W范围与既有进度（当前环境优先）

此表保留全项目任务定义；ENV细化当前执行路线，不撤销学习、公开事实、数据、成本或证据的后续要求。历史批次/详细数字见[整理前完整快照](../2026-10-06/task-list-before-handoff-cleanup.md)。

| 范围与责任模块 | 详情 |
| --- | --- |
| W-01 完整环境公开出口 / net，base/AI配合 | [W-01 完整范围](#w-01) |
| W-02 公开ID与事实可信 / net，base配合 | [W-02 完整范围](#w-02) |
| W-03 场景、数据与成本 / base/AI，B结算ENV证据 | [W-03 完整范围](#w-03) |
| W-04 低成本模型与学习工程 / AI | [W-04 完整范围](#w-04) |
| W-05 完整单人Run / base，net配合 | [W-05 完整范围](#w-05) |
| W-06 正式DSL、完整内容与对拍 / base，B/C配合 | [W-06 完整范围](#w-06) |
| W-07 产品范围与下一阶段决策 / 负责人 | [W-07 完整范围](#w-07) |
| W-08 公共约束与证据结算 / 各线供证，负责人关闭 | [W-08 完整范围](#w-08) |

#### W-01

**已认可范围与当前状态**：受控作者Gym、药水/Run/长期牌组、公开摘要v2及真实stdio部分链路已验收；正式内容/AI专项编码开放

**完整目标、下一交付与关闭条件**：正式源同Host describe→catalog→start→出牌/用药→终局→离线重编码；药水定义/稀有度/目标词表、栏位容量/可用状态及长期组正确。当前LostHpThisTurn/ExhaustedCardThisTurn/成长变量摘要由提供方发布，AI保留差异，不从历史补齐；逐适用Profile核对Manifest，区分原型与正式内容

#### W-02

**已认可范围与当前状态**：持久句柄修复、受伤/生成销毁/撤销授权和故障注入已验收；扩展投影可信度开放

**完整目标、下一交付与关闭条件**：同定义实例在关系/位置可区分时正确表达；隐藏增删/重排不泄露，公开事实完整。跨会话一致换名后核对结构/引用/编码/概率/默认选择，扩展场景执行V-15；相应未验收场景不进入真实学习

#### W-03

**已认可范围与当前状态**：四簇采样设施与D1固定种子划分交付；丰富场景分布/全环境成本开放

**完整目标、下一交付与关闭条件**：固定Gym内资源/目标/牌区知识等真实差异簇，逐模型身份保证适用train/validation/final隔离与非空，不能用跨身份总计代替；绑定scenario源、故障/截断/资源上限/采样成本。ENV当前先完整分母、suite与Release成本；学习分布/预算由W-07另冻结

#### W-04

**已认可范围与当前状态**：默认Gym管线、D1.1可靠性及D2诊断已验收；正式实验暂缓，学习效果开放；用户2026-10-11要求先不动AI，模型工程也暂缓，PyTorch技术选择保留

**完整目标、下一交付与关闭条件**：保留模型/自动微分/Adam/PPO、版本化奖励、真实轨迹学习、同seed配对评价/区间、模型包exact-identity及参数/内存/延迟/吞吐、保存加载/resume证据。Trainer通用调用仍需显式refresh_identity；环境门槛后处理新增路径身份失效、真实效果和晋升。用户2026-10-10已授权优先PyTorch、共享Transformer工程，取代仅NumPy手写的框架限制；社区STS2接口研究已交付；用户2026-10-11要求先不动AI，共享Transformer工程暂缓，后续明确恢复时再登记具体修改边界及验收包。既有NumPy交付保留，不启动真实训练或恢复LLM实验；工程替换不等于学习效果已验收。依据见[社区接口研究与PyTorch决策](../2026-10-10/sts2-community-interfaces-and-pytorch-2026-10-10.md)。

#### W-05

**已认可范围与当前状态**：受控两房间升级和长期/临时边界有交付；真实完整Run开放

**完整目标、下一交付与关闭条件**：战斗终止与整局终止分离，结算幂等及最终余效、HP/MaxHp/金币/药水/遗物等永久写回；同定义不同实例来源，临时牌隔离。正式地图/遭遇/奖励/商店/营火/宝箱/古代人/事件战斗返回/幕转换与终局全部实现；历史两房间Victory不抵完整环境

#### W-06

**已认可范围与当前状态**：前端/共享模型→同Engine IR及正式战斗子集已交付，完整规则/内容/公开/对拍开放；Bully窄缺陷已关闭

**完整目标、下一交付与关闭条件**：ContentSource正式单一来源，高级声明/完整普通升级/条件/X费/目标/hooks语义和文本库组合可核对；运行时支持白名单、来源/解析/加载/行为/对拍/公开可用分列。五角色与所有原版单人可达内容，缺源不删分母，不回退.author/Legacy，不按检查数算覆盖

#### W-07

**已认可范围与当前状态**：ENV范围、基线/保护与源裁决流程已规划；完整环境及AI效果未验收

**完整目标、下一交付与关闭条件**：先ENV-00～06完整环境候选，明示参考偏离/客户端抽样边界；门槛后重新冻结AI任务分布、目标/奖励、预算/晋升及部署收益偏好，旧D3/D4方案/seed不自动沿用

#### W-08

**已认可范围与当前状态**：窄缺陷有关闭记录，发布/全量证据持续开放

**完整目标、下一交付与关闭条件**：N/H/J、P-01与U-11按适用范围对账，绑定同一源/规则/内容/Profile/模型与run_id，复现首个差异/故障、覆盖/确定性及部署成本；历史已交付模型/句柄/UsePotion/两房间不重做，部分父项不能随窄缺陷关闭

### 1.1 本轮已确定的工程决策

优先级与依赖见0.1。长期工程边界：提供方发布当前有效摘要；公开句柄跨状态稳定、销毁不复用；药水栏/长期牌组用通用实体/字段/关系表达；身份变更绑定实际内容和Profile，不要求所有旧golden永远不变。玩法权威状态在Engine类型化存储，base不另建调度器或隐藏RunState。core通用缺口附最小复现交负责人，仅暂停具体case。

### 1.2 本轮实测复核与接续依据

先从对应任务条目的“证据”进入，不按测试总数判断完成。不同日期、验证者和构建的结果分别保留：

| 需要查什么 | 证据入口与适用边界 |
| --- | --- |
| T01/T02 修复前的负责人独立结论、末杀与来源反例 | [2026-10-06 独立复核](../2026-10-06/t01-t02-completion-review-2026-10-06.md)；证明原构建，不能覆盖后续 A 自报或替其验收 |
| T01/T02 后续修复、检查自报与源敌人依赖 | [整理前任务记录](../2026-10-07/task-maintenance-history-2026-10-07.md#任务行中的原始记账)；当前剩余范围见任务详情 |
| 共同工厂、随机/结算源约束与既有 R1～R3 | [负责人基线复核](../2026-10-06/base-spire-owner-decision-2026-10-06.md)；旧 Slice 数字与组合探针是日期证据 |
| 其他交付/旧决定与当时命令 | [records 索引](../README.md)；按需查证，不恢复旧 RESUME HERE |

已交付状态机/GrabBag/编队/出生能力和 R1～R3不重做；父项和五角色范围仍按本页完成条件。D1/D1.1/D2与 Bully 窄缺陷已有验收，模型效果未达标，新 AI 实验继续按清单门槛安排。完整四幕、严格 suite、120 对、正式公开全程、真实客户端与 Release 性能没有本轮通过结论。

## 2. W-07：先冻结完整环境，AI实验待环境验收

- [ ] 完整源分母、实际source/reference摘要和独立可执行比较成立，排除有依据。
- [ ] ENV-01～05的真实规则、全量可达内容/整局和公开出口成立，无静默fallback。
- [ ] ENV-06有效coverage/首个差异裁决/正常胜负/成本和真实性边界结算，由负责人审查候选。
- [ ] 通过环境门槛后另定AI分布、目标/奖励、学习/部署预算与晋升。

Ironclad是阶段门槛；五角色完整环境才是当前环境总目标。每case保护为120秒/20000外部决策/100层，最终120对总14400活动秒；超限单列，不能伪装自然死亡。参考缺功能保持blocked_compare，不降低分母、删seed或用源内注入制造正常胜负。

## 3. 有记录，暂不派单的后续范围

Lua真实Compute后端、IDE/LSP、多人/私密同时选择、任意mod自动迁移、参考项目全量修复、内部恢复/fork/search、分布式及真实部署均需独立触发。共享Transformer工程已由用户2026-10-10触发，但用户2026-10-11要求先不动AI，工程与正式实验暂缓；技术选择和完整范围见W-04。ENV-T15/16按表中条件派单。五角色、原版事件/古代人和单人完整域属于当前总范围，不能移到这里延期。

## 4. 认领、更新与关闭规则

1. 先看git状态和[对应任务提示词](../../../design/agent-goals.md)，按优先级选择本线已就绪项；认领登记会话/目录/下一项可验证的结果。优先级与范围由负责人维护，执行者补进度和证据。
2. 待认领→进行中→待验证→已验收；真实依赖阻塞写ID、最小复现、受阻case和可继续事项；暂缓需明确触发条件。只有负责人可以关闭父项或变更身份。
3. 每次交接只更新对应任务详情、必要的本线阶段报告和证据链接；索引只导航，长日志/逐批历史放records及artifacts，不向本页追加流水账，不为每张卡/原语新建报告。新报告先到tofix，负责人合并开放项后转records；已修窄缺陷有证据才转archived。
4. 每个工作包一体交付实际能力/前端/IR/必要检查/文档。测试新增与开发选组按根[测试取舍](../../../../AGENTS.md#测试取舍全仓库强制)，共享规则完整包集成串行build、Engine、Slice；涉及net跑net；族交付regression、整局阶段ironclad-run、重大共享/候选full-singleplayer。尚未实现的严格命令不记通过，不逐次编辑重复全量验证。
5. 共享工作区不自动提交/reset/清理；公共文件和obj/bin单一编辑/构建顺序由负责人协调。规划不自动启动会话、agent或训练。交接格式/优先级管理见[管理约定](../../../design/post-core-agent-assignments.md)。

## 5. 本轮整理交接与检查

2026-10-15 Codex `/root`按用户要求使用 `humanizer-zh` 完成文档措辞审阅，修改67份现用导航、指南、规格和待处理交接。按上下文区分测试数据、测试场景和回放记录，展开省略过多的简称，并重写开发指南中难以理解的长句；技术含义、任务状态和验收条件保持不变。已核对标题、代码块、行内标识符、命令、路径、链接目标、文档属性和数字，214份历史报告保留原文。文档检查扫描493份、0问题，差异检查通过。本次只修改文案，未构建或运行规则检查，维护已完成。

2026-10-15 Codex `/root`按用户要求使用 `tgdp-dev-docs` 完成开发文档更新。修改边界为[文档总入口](../../../README.md)、[core README](../../../../core-the-spire/README.md)/[开发者指南](../../../design/core-the-spire/developer-guide.md)及 core/[0.2](../../../dsl/core/0.2/authoring-guide.md)～[0.3](../../../dsl/core/0.3/authoring-reference.md)参考、[base README](../../../../tools/base-migration-baseline/README.md)/[开发指南](../../../design/mod-the-spire/base-development-guide.md)及本登记；按源码核对 SDK、活动版本、同包绑定/扩展加载、前端身份、dev/dev-map 范围、公开语义接入与检查分组。构建描述部分限制文字滞后已在 base README 说明，未修改生产描述。`python tools/doc-check/check_docs.py`扫描493份、0问题；`git diff --check`返回0；本机SDK/Python分别为9.0.306/3.12.0。命令和签名已静态核对，本次纯文档未重跑构建、规则套件或教程；保留根 README 既有未提交修改，未改历史证据、排期或验收结论。本次维护完成，无待续生产工作。

2026-10-13 用户重新打开DSH后，Codex `/root`确认服务恢复、三旧会话停止（A960／B725／C586）并分页取齐completed结果；此前本地验证范围不变，释放旧包主责并将A幕转换／B正常初始化两个完整短包登记为进行中且实际accepted。B只读负责人固定DLL，A单写base；C待A契约就绪再随包，不启动AI／批量补卡／暂缓召唤。会话、输入摘要及唯一交接路径见T10/T04和task-record，[恢复记录](../2026-10-13/project-continuation-review-2026-10-13.md#dsh恢复与实际接续)。

2026-10-13 Codex `/root`完成三线交付共同复核与接续材料：隔离solution/99/842/418、完整严格16双侧＋1自检通过；正常奖励2例首个差异非零；正式客户端105成功／106尝试及复算成立。维护本主清单／日期报告／索引和.audit证据，未修改生产。DSH登录接口303，但首页／RPC503，远端停止状态和新派发未确认；两份准确短包已准备，无新accepted。本轮实证、输入摘要、受阻范围与恢复后的第一步见[共同复核](../2026-10-13/project-continuation-review-2026-10-13.md)。

2026-10-13 Codex `/root`完成用户要求的原语完整性审阅与任务补录：新增T06-PR01～17、T09-PR01～05共22个机制子任务，以及T13-PR-AUDIT覆盖收口项，分别注明排期／状态／依赖／可验证的结果；T12只引用公共能力。对照base实现、契约与本地原版代表例；1419份源模型／123份base文件的调用导航及SHA保存在`.audit/base-primitives-review-20261013/`，静态导航不表示行为全覆盖。[综合证据](../2026-10-13/base-primitives-review-2026-10-13.md)已并入对应开放项；原父项完成条件、DSH认领和单写边界保留，不新增派单或运行时验收。

2026-10-13 Codex `/root`按用户要求完成base架构源码审阅：边界为当前base源码、契约、[日期审阅记录](../2026-10-13/base-architecture-review-2026-10-13.md)、索引及本维护登记；Power来源上下文、整局配置、Power类型化与生命周期建议已关联T06/T10/T12，生产／测试数据入口分离作为后续维护建议。没有新增生产认领或改变当前DSH包边界；本次未构建或修改生产代码，未作运行时验收。

2026-10-13 Codex `/root`按用户要求完成本轮DSH分派维护：T08精英必要机制、T04正常奖励双侧、T03公开事件策略三个全新专用会话均已accepted，准确session与固定交接路径写入对应认领/派发记录；T10串行排在A之后。生产分别归A/B/C，负责人不编辑其文件、不在本次派发后等待完成或取回结果；后续按既有复核约定核验实际交付，accepted不计完成或验收。短包/固定DLL摘要和派发汇总见`.audit/dsh-wave-20261013/`及对应条目；不恢复旧DS会话、批量卡牌或AI。

2026-10-13 Codex `/root`按用户要求完成文档校准：边界为本主清单当前摘要/任务接续、records索引、两个角色阶段记录的时点说明、两份旧交接锚点及当日复核追加；生产与原始证据不改，不派单或关闭父项。已校正严格17、Doom/Osty已交付与无在制子项，T12指T06唯一认领；旧绑定/初始化/重洗失败标明历史时点，当前阻塞区分客户端策略、Byrdonis精英内容和幕转换。doc-check扫描465份、0问题/rc0；git diff --check返回0。本次维护无待续工作，未重跑游戏套件；原文快照/差异/检查日志及校准记录见[当日复核追加](../2026-10-13/project-progress-review-2026-10-13.md#文档校准追加)。

2026-10-13 Codex `/root`按用户要求完成当前进度只读复核；维护边界仅本登记、日期报告与records索引，生产源码未改。核对最新solution/99/839/418日志、既有严格16双侧+1接入及三个专用DSH会话停止状态；当前Host正常公开客户端46步在Unknown停止，已有事件包装70步通过SunkenStatue并到精英入口，实地进入确认Byrdonis行为缺失。M01/四幕/五角色、新机制双侧与版本发布保持开放；本次无新派单或生产待续。摘要滞后和证据时点区别见[本次复核](../2026-10-13/project-progress-review-2026-10-13.md)，原始帧见`.audit/owner-progress-review-20261013/`；doc-check扫描465份、rc1，仅两份既有交接4处#L锚点失败，本次文档未报问题；git diff --check返回0。本次不重跑全量规则或严格suite。

2026-10-12 Codex `/root`按用户review并继续推进指令认领三线交付复核与共同构建收口：商店生产、语义冒烟重放、公开商店运行器及实际同构建轨迹；边界为证据核验、主清单/交接维护、必要公开接口协调和后续派单，不自行恢复卡牌/AI。本次负责人维护已完成：三会话交付与共同99/770/416门槛、真实商店两路径及45步语义重放已核验；新A/B/C接续已accepted，认领只见对应条目。[日期证据](../2026-10-12/project-owner-review-2026-10-12.md)及`.audit/owner-project-review-20261012/`保留未验证范围。

2026-10-11 Codex `/root` 按用户要求认领本轮负责人复核与开发者教程维护：核对T08初始化/T09商店固定交接、实际问题及局部证据，按Diataxis补从空目录开始的卡牌/房间教程与扩展入口。只写docs导航/教程/示例宿主及本次审阅证据，生产缺陷给出裁决与明确接续边界；不自动集成商店或另起全面重构。本次维护已完成：[组织结构、两条教程与问题复核](../2026-10-11/base-developer-review-2026-10-11.md)；教程按文中步骤从空目录实跑，卡牌5份结果、房间三阶段检查点一致。solution构建、队列7/当前药水25/隔离商店13通过，net415/416与跨会话首个差异保留；未修改生产或重复全套Engine/Slice/整局。

2026-10-11 Codex `/root` 按用户明确派单使用DSH新专用会话接续T08.1；先核对旧初始化已停止、商店仍运行，公共接口与隔离目录主责保持分离。新短包给出准确Room/初始化接口、源方法和串行验证命令；派发状态仅维护T08认领，accepted不计验收。不统计token，不另派卡牌或AI工作。

2026-10-11 Codex `/root` 按用户要求完成开发减负首包，边界/剩余门槛见T03/T09。交付两条短路径、接口参考/关系图、两个可运行例子及营火/宝箱上下文和公开访问实际调用方；[日期证据](../2026-10-11/developer-onboarding-delivery-2026-10-11.md)已并入开放项并移records。solution/Engine99/局部营火2宝箱3与公开有限前缀通过；完整Slice首个差异15/30、net415/416保留，不新增token统计。doc-check仍仅两份旧交接4处锚点问题，diff检查通过。

2026-10-11 Codex `/root` 按用户要求复核降低心智负担的既有建议、代码交付和三条DSH会话的首尾抽样窗口；证据/解析脚本位于`.audit/context-burden-review-2026-10-11/`。只维护CORE-AUTHOR-04范围界限及开发指南短包契约，未编辑当前三线生产文件、改其认领或扩派任务；负责人派单缺少明确接口的责任与未完成部分已记录。验证：`git diff --check`通过；doc-check扫描389份文档，仍报两份既有交接中的4处`#L`无效锚点，本次新增引用未报错。纯文档复核未运行构建或规则套件。

2026-10-11 Codex `/root` 按用户并发派单要求核对T08初始化仍运行并划定商店/公开Run运行器的独立边界；新认领与响应仅见T09/T03，初始化公共接口不转交。商店隔离模块、client新文件及各自证据由不同会话主责，公开注册/正式集成与最终构建由Codex串行收口。纯文档diff检查返回0；doc-check返回1，仅两份既有交接4个行号锚点问题；未重跑规则套件，accepted不计验收。

2026-10-11 Codex `/root` 按用户加速真实整局流程指令维护根协作约定和本清单：暂缓批量卡牌，登记T08.1生产包及T09/10流程接续，派发文本/占用检查放`.audit/dsh-real-run-init-2026-10-11/`。旧药水A/C查询均已停止，新base机制包已accepted；公共架构与验收仍由Codex负责，派发结果见T08认领，不以accepted关闭父项。纯文档diff检查通过；全仓doc-check仅报告另外两份既有交接共4个行号锚点问题，本次改动未被报告问题；未重跑规则套件。

2026-10-11 Codex `/root` 按用户要求审阅完整M01～M04、ENV小任务及W范围并调整接续顺序；修改边界仅本主清单的排期、T08先行依赖和本维护登记。优先推进真实Run，保留全部验收范围、既有认领与暂缓决定；未修改生产代码、关闭父项或派发新会话。纯文档检查：`git diff --check`返回0；doc-check扫描388篇、返回1，Power和startup两个既有交接共4个行号锚点失败，本次修改的清单未报告问题；未重跑构建或规则套件。

2026-10-11 Codex `/root` 按用户要求完成CORE-AUTHOR-04源码/交接/日志和定向行为复核，补当前作者工具证据、比对重构前后严格差异并整理原交接至records；认领/剩余范围只维护CORE-AUTHOR-04与ENV-T04，接续派发见T04详情。[负责人记录](../2026-10-11/core-author-04-owner-review-2026-10-11.md)注明独立执行和未重复全跑范围，未认领生产/重启训练。

2026-10-11 Codex `/root` 按用户插单完成测试必要性审查与规范维护：根[测试取舍](../../../../AGENTS.md#测试取舍全仓库强制)规定不再新增的七类测试、必须保留的判别能力、开发选组及存量合并方式；core 验证入口和本页管理规则统一指向该节。抽查 Engine/Slice/net/作者工具，未修改测试代码或禁用既有入口；不取消机制/公开/独立对拍验收。`git diff --check` 返回0；全仓doc-check返回2，仅另一份既有交接的两个 `#L` 链接失败，见[原始输出](../../../../.audit/author-runtime-v04-2026-10-11/test-policy-doc-check.log)。CORE-AUTHOR-04 原重构保留认领，规则行为验证不作为此次纯文档维护的门槛。

2026-10-11 Codex `/root` 按用户要求完成B/C固定交接复核与三线下一包规划/派发：维护T03/T04/T05认领、受控Power初值契约及本轮证据/索引，生产由DSH执行，负责人没有改游戏玩法。复核与并行边界见[本轮安排](../2026-10-11/dsh-next-wave-owner-plan-2026-10-11.md)；实际accepted/session以对应task-record为准。共享IR/身份/公共Boss回放数据仍由Codex负责，M01未关闭、AI暂缓。


2026-10-11 Codex `/root` 根据用户确认完成三线认领/派单维护：T05药水由Codex亲自承担，T04受控初始化与T03公开诊断已新建DSH会话并收到accepted。修改边界为对应详情、ENV受控初始化契约、net诊断字段契约及两个提示词/输入摘要/派单记录；生产与验收状态只查T03/T04/T05。卡牌第四线尚无新派单，AI暂缓；固定交接路径已写入各task-record，公共身份/主清单由Codex独自修改。

2026-10-11 Codex `/root` 负责人审阅与后续拆分已完成：核对DSH Vulnerable交接、tools实际输入/独立构建/严格17项，维护T03/T04/T05剩余依赖和并行建议、日期证据及索引；[复核与拆分](../2026-10-11/dsh-review-and-parallel-plan-2026-10-11.md)。没有认领生产包、恢复AI或派发新会话。

2026-10-11 Codex `/root` 本地BMingSY参考登记及并发接续：负责net设计/spec、AI暂缓安排、T04/T06/T12对应状态与本轮证据；生产认领仅见对应详情，参考仓库只读。共享抽牌包及Headbutt子包已交付，接续与验收只看T04/T06/T12详情；证据见[本轮推进](../2026-10-11/parallel-main-progress-2026-10-11.md)。未恢复AI或真实适配器生产。

2026-10-10 Codex `/root` 社区接口研究与AI框架决策维护：修改边界为W-04框架限制、本维护登记及日期研究/索引；读取五个社区仓库和16份固定提交源码/设计文件，随后依用户BMingSY提议追加3份并核对本体出牌/结算及ControlSession模拟耦合，未修改生产代码、运行社区mod或启动训练。[研究记录](../2026-10-10/sts2-community-interfaces-and-pytorch-2026-10-10.md)保存来源、职责判断、最小适配包建议及未验证范围。

2026-10-10 Codex `/root` 卡牌族成果复核与接续：负责T06/T12公共边界和当前任务段、编排规格/base与modding指南、DSL约定及日期证据；完整规则/公开门槛与设计判断见[复核记录](../2026-10-10/dsh-card-families-owner-review-2026-10-10.md)。历史43张交接并入records，当前派发与执行会话只维护T12。
2026-10-09负责人卡牌审阅/整批接续：本会话完成Anger复制叶限定验收、两个读取漏洞修正和默认身份迁移，亲自定义连续卡牌批次的有限IR/职责契约并派发DSH。修改边界为卡读器、两处Slice检查、卡牌工作包及任务/证据文档；实际门槛及未验证范围见[收口记录](../2026-10-09/anger-review-and-card-batch-2026-10-09.md)，当前认领与下一成果只见T05。旧工程量日期估算保留在[此前复核](../2026-10-09/base-readiness-review-2026-10-09.md)，本次不将派单accepted算完成。

2026-10-09负责人跨层专项：先维护成本报告与T06反例取证，随后依用户派单亲自完成领域程序和效果/动作边界包；当前认领/验收只见T06详情。修改范围为base模型/前端/编译接口、Slice检查及配套文档；T05历史认领按用户无DS会话说明解除，保留其工作区改动。原诊断见[专项复核](../2026-10-09/base-spire-development-cost-review-2026-10-09.md#6-修改跨层专项复核)，实施/局部验收及完整门槛首个差异见[交付记录](../2026-10-09/effect-ir-boundaries-2026-10-09.md)。

2026-10-09负责人开发成本审阅：修改边界为诊断 `.audit/base-development-cost-2026-10-09/`、本维护记录与记录索引。已核对四个完整DSH会话的token/调用，并在复制DLL上实测地图重复重放；执行调度、地图结果复用、测试反馈及ENV-T07模块化建议见[成本审阅](../2026-10-09/base-spire-development-cost-review-2026-10-09.md)。本次只完成诊断，生产包仍按对应条目接续。

本次“验收并汇报进度”负责人复核：B地图生成子包验收，A只交设计且有源时序错误，C只交准备并遗留in_progress；旧B等待已解除。按既有授权接续原A奖励包，C待A实际交付再派，不扩生产范围。[复核与独立实测](../2026-10-09/dsh-supervisor-review-2026-10-09.md#当前内容复核与进度汇报)保存完整结果、17严格/负例/构建、当前缺口与源裁定。

2026-10-09负责人验收/接续：本会话只维护T03/T04/T05、相关core缺口证据和本轮记录索引；地图Host与连续攻击修复子包局部验收，B前置已解除，按B地图对拍→A真实奖励→C公开轨迹派发。父项/M01保持开放。证据与验证范围见[本轮复核](../2026-10-09/dsh-supervisor-review-2026-10-09.md)，新会话由对应task-record与任务详情维护。

2026-10-08自动复核及本轮用户要求的验收/接续继续。负责人只维护T03～05详情与日期证据；本轮A地图首包局部接收、C声明扩展/B源输入子包验收，父项/M01保持开放。C地图Host→A正常连续攻击修复→B真实工厂地图独立对拍按编译顺序接续，主清单派发响应与记录为准。[当日复核](../2026-10-08/dsh-supervisor-review-2026-10-08.md#2153验收与地图公开接续)保存证据，半小时循环继续。

2026-10-07 DSH 派单：本会话按用户要求派发 ENV-T04，并设置三项现有文档 skill 安装/加载为实施前置；认领及证据只维护 [T04](#env-t04)。负责人修改边界＝T04详情、本维护记录与 `.audit/dsh-env-t04-implementation-2026-10-07/` 派单/源哈希产物；仅确认 accepted，未查询新任务状态或取回结果，安装和实现完成均未验证，待用户手动唤起复核。

2026-10-07 当前交接文档整理：本会话已交付短任务索引、同页详情、角色接手步骤与验收入口；状态＝已验收（仅文档维护）。修改边界与使用方式见[整理交接](../2026-10-07/handoff-docs-cleanup-2026-10-07.md)。原完成条件、ENV/W范围、既有认领及历史证据核对保留；接手自查、doc-check（229份/0问题）与 git diff --check通过。本次维护无待续工作，不作生产验收声明。

过去维护的日期、结果和修改边界保存在[原维护记录](../2026-10-07/task-maintenance-history-2026-10-07.md#原维护记录)。本节只保留本次维护结论及证据链接；转入 records 不表示 ENV 实现已验收。

2026-10-15 Codex `/root`按用户要求完成共享工作区交付审查与Git清理维护：源码、契约、工具和文档纳管，原始文件及参考改动已仓库外备份；必要历史证据归档、参考固定版本/补丁和LF检出规则已保全。隔离构建/Engine99/本地源码Slice842/net418及严格16双侧+1自检通过；原版源码及源检查按用户要求留本地，不上传GitHub或放入Actions。依赖检查9个同名报警、旧商店轨迹step177首个差异保留，不放宽断言、不关闭ENV/版本父项、不恢复训练或派新会话；详细文件处置、实际命令、身份迁移和未验证见[本次审阅](workspace-baseline-review-2026-10-15.md)。本次保全/清理维护无在制生产子项，新CI配置另行实施。
