# base-spire完成度与工作流审阅 — 2026-10-19

本次回答“纯DSL base还缺什么、开发为什么慢、先改哪里”。负责人Codex `/root`，归属CORE-AUTHOR-05／CI-01。基线`978a0b6`、PR #23、base `0.6.0-dsl.38`；Windows／PowerShell、.NET SDK 9.0.306、Python 3.12。当前任务仍只维护[主清单](../../tofix/README.md#当前交付)。本次修改检查工具、CI触发和工作流文档，不扩张玩法或解除暂停项。

结论：铁甲战士卡牌的本地机制覆盖已较完整，全游戏环境仍处于内容与流程恢复阶段。主要缺口是遗物长尾、其他内容、正常来源／Run，以及纯DSL独立适配器。不能用一个整体百分比合并这些不同层级。

## 完成度证据

下表为本次基线的静态快照，不作为第二份当前状态表。分母来自实际catalog，定义只计`definition`为字符串，运行见证来自本次实际执行的完整check报告。

| 领域 | 来源条目／DSL定义 | 已取得的证据及限制 |
| --- | --- | --- |
| 卡牌 | 578／87 | 87个定义均有本地见证；铁甲源池87项中的85张单人牌完整，两项多人排除。不是五角色全卡完成 |
| 遗物 | 297／55 | 55件均有本地见证；1件源Deprecated排除，241件尚未实现，即55／296件未排除来源。正常商店／奖励等来源及独立比较仍缺 |
| 敌人 | 121／9 | 9个本地见证；开发遭遇及固定血量夹具不证明完整原生出生分布、精英／Boss及正常Run |
| 遭遇 | 88／2 | 两个本地见证，未接全池正常流程 |
| 药水 | 64／6 | 六个本地见证；完整掉落、容量和来源参与者未全恢复 |
| 角色 | 8／1 | Ironclad显式初态已接；8项源目录包含开发／特殊条目，不能当8个正式角色分母 |
| 事件 | 68／0 | 当前DSL事件目录没有迁移定义；切换前C#事件不计入当前支持 |
| Power | 260／0 | catalog尚无逐源定义映射；Strength／Weak／Vulnerable等共享规则与卡牌所需Power已有行为，不能据此说Power完全未做 |
| Act／Run | 5／0 | 源目录和静态act-plan不等于正常运行；5项也不等于五个正式幕 |

包manifest仍只支持`base-spire-dsl-relic-draw-dev-v1`，五个原正常游戏scope显式未支持；根入口运行`SetupSourceRandom`、`SetupDevelopmentDraw`。正常Neow、地图／房间往返、跨幕和最终胜负没有迁移完成。原严格适配器保留17个regression case，当前设计为全部unsupported、executed=0、退出3。`migration`检查的是保留与失败合同，不能替代独立执行。

以上可从[manifest](../../../../mod-the-spire/base-spire/package.json)、[入口](../../../../mod-the-spire/base-spire/base.spire)、[包检查](../../../../tools/base-spire-dev/PackageChecks.cs)和[严格适配器](../../../../tools/differential/Program.cs)复核。逐件重新核对全部源语义不属于本次审阅，因此本地见证不升级为独立原版准确性证明。

## 发现及处理

| 优先级 | 发现与影响 | 依据和本次处理 |
| --- | --- | --- |
| P1 | 正常Run和独立比较没有恢复，内容数增长不能证明环境完成 | 实际manifest、入口和适配器；主清单分层保留，新增批次收尾检查集成缺口的规则。全遗物优先不变；适配器需单独完整包，先恢复一个原case |
| P1 | “定向遗物检查”累计执行87项，单次修改三件也跑所有组；完整报告还执行395项 | 原RunRelicContentOnly串行调用七组；本次加入名称前缀筛选、逐项耗时及执行／跳过统计。零匹配和与完整report混用均失败，不允许局部证据冒充全量 |
| P2 | 同提交push／pull_request触发两套四组CI，重复占用runner和审阅注意力 | PR #23的[push运行](https://github.com/QingXiang22/auto-the-spire/actions/runs/39127768861)和[PR运行](https://github.com/QingXiang22/auto-the-spire/actions/runs/39127772229)各含四组成功结果。本次仅main保留push，功能分支由PR触发；必需组和失败传播不变 |
| P2 | 小批交付的固定成本过高，且入口与新优先级冲突 | 最近五个提交仅新增17件遗物，每批触及22～28个文件；合计123次文件触达中36次为docs、28次为tools／检查。原“当前交付”约12304字符，含旧Sown契约和多代迁移进度；“推进顺序”仍先Sown。本次替换过期流水账，保留历史证据链接与Sown未关闭合同，顺序改回全遗物 |
| P2 | 流程规则已有“局部检查／CI广覆盖”，执行收尾仍重复全包／全公开组 | 最近几批记录均含局部检查、完整check、公开组和构建；纯配置也同步改多处手册／工具README。本次明确配置与新机制两条路线，已有模板不重复写规格／接口／协议检查；每件数值见证继续保留 |

文件触达是本地Git统计，不代表工时分布。Git时间戳也不是净工作耗时；没有完整token账单、等待／阅读／编码分段计时，不能推断“慢了几倍”或按本批速度外推全游戏完工日期。局部源码核对、共享机制设计与必要边界验证仍是真实成本，不宜用删验收条件换表面速度。

## 已落地的执行节奏

1. 从唯一当前条目拿到范围、已有模板、源方法和固定判别；按操作局部读取，旧记录按需打开。
2. 按触发、状态寿命与消费者成组交付。仅参数差异时补配置、注册和逐件数值见证；新增共享机制时补对应契约、真实消费者和边界。不给每件遗物重复建项目级任务。
3. 调试只运行当前机制的名称前缀，例如`check --relic-content-only --filter "AM "`；必要时按实际名称进一步缩小。筛选结果不生成覆盖报告。
4. 本地局部通过后交付待验证；CI一次完成完整报告及覆盖门槛。新增公开字段／决策才补相关协议判别，原有表驱动公开覆盖保留；正常来源和独立比较缺失继续单列。
5. 每个可审阅机制批一份简短证据，同一交付分支继续提交；不因达到3～5件机械结束编码窗口。批次结束复核集成层缺口；若连续批次只增长定义，由负责人重排，不默默把正常Run延后到297件全部完成。

具体有效规则只维护[批次节奏](../../tofix/README.md#每批集成与重排条件)，本节解释本次决定。工作流第一轮改动不会自动恢复独立适配器、正常Run或实现更多遗物。

## 实际验证

证据目录为`.artifacts/base-review-20261019/`及同名前缀日志；目录内原始产物不提交。

| 命令／检查 | 实际结果 |
| --- | --- |
| 修改前`dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-review-20261019/behavior` | 395项通过，进程含构建共119.712秒；包摘要与当前生产内容相同 |
| 修改后`dotnet build tools/base-spire-dev` | 0错误、0警告 |
| 修改后`dotnet run --no-build --project tools/base-spire-dev -- check --relic-content-only` | 87项通过，跳过0，进程41.280秒／检查段40.118秒 |
| 修改后同命令加`--filter "am "` | 10项AM通过，跳过77，进程4.814秒／检查段2.637秒；验证大小写不敏感 |
| 空白／零匹配／筛选与report混用／筛选与其他定向参数混用 | 四种均返回2，未产生完整报告 |
| 当前报告的遗物／铁甲覆盖检查 | 均返回0；55件遗物、85／85铁甲单人牌；`independent_verified=false` |
| 遗物覆盖加`--require-complete` | 如实返回1，241件未实现，完整门槛未满足 |

这些是同环境下不同检查范围的反馈成本，不能解释成引擎执行提速。新筛选仍会经过部分共享夹具准备，计时输出区分单项与检查段；入口加载／构建另计。包规则、内容与源池未改变，修改后只复验受影响的CLI路径，不为文档和触发配置重跑全部395项。

文档检查扫描787份Markdown、0问题；git diff --check通过。PyYAML实际解析CI配置，核对main的push、PR及手动入口和原四组矩阵均保留；逐字比较确认原任务详情、机制子任务与全部ENV／W合同未改。当前提交对应Actions须独立核对，旧PR的四组通过不覆盖本次工具／CI修改。正常Run、整局对拍、全遗物、五角色／进阶／120对矩阵及性能均未新增验收结论。未派出子agent或远程DSH。


## 内容解耦专项审阅

用户同日追问：单个遗物、卡牌、Power的修改或新增，是否仍需要大量改动base-spire。审阅基线`f4d18c5`，继续由Codex `/root`负责；本节为架构发现和候选重构验收，未修改生产规则、加载器或核心。上一轮检查筛选及CI去重解决了反馈成本，本节补充内容边界本身的问题。

### 直接回答与修改面

| 操作 | 当前实际修改面 | 判断 |
| --- | --- | --- |
| 修改已有遗物模板参数 | 通常一个内容叶；改变承诺数值时同步对应数值判别 | 配置路径已能局部修改，不应要求改共享战斗规则 |
| 新增已有模板覆盖的遗物 | 内容叶、显式index、原catalog的definition、公开content_ids四处；版本变更属于发布处理 | 无需修改Engine／Host／共享战斗算法，但登记和验证仍需手工联动 |
| 修改普通攻击牌数值 | 同一个文件中CardDefinition和AttackEffect分别保存普通／升级伤害；还需检查计算伤害／升级消费者 | 文件少，但数值存在多个事实来源，容易改漏 |
| 新增已有组合覆盖的卡牌 | 内容叶、index、源catalog、公开元数据及真实见证 | 可复用已有DSL；没有必要修改C#游戏分派，但登记和证据入口仍未完全解耦 |
| 修改／新增Power行为或寿命 | 调用方提供kind；共享Power实体、reactions／modifiers中的家族分支共同决定行为 | 缺少独立Power定义边界；这是本次最优先的结构问题 |
| 为单个内容增加私有状态 | 部分遗物已有独立State；卡牌／Power仍有专属字段进入公共schema及复制／结束流程 | 局部解耦方法已存在，但没有一致应用 |

### 五个隔离实验

实际命令：`dotnet run --no-build --project .artifacts/decoupling-review-20261019/probe/Probe.csproj -- .artifacts/decoupling-review-20261019 mod-the-spire/base-spire`，退出0，五项探针断言均通过。探针项目只引用现有Engine，加载各自完整包副本并提交真实合法动作；不注入伤害或Power结果。源码、包副本、`observations.log`和逐文件字节差异`change-surface.json`保存在`.artifacts/decoupling-review-20261019/`，不进入正式来源分母或支持数。

| 隔离改动 | 实际结果 |
| --- | --- |
| 仅把TheAbacus内容叶amount从6改为8 | 重洗后8格挡；生产目录、模板、战斗和Host均未改 |
| 新增教学CouplingProbe，复用AfterShuffleBlockRelic；仅新增叶并改index／公开元数据 | 获得后重洗实际7格挡；未改任何共享玩法。教学ID未加入原catalog，正式迁移还需登记原源行 |
| 仅把Strike的CardDefinition.baseDamage从6改为12，保持AttackEffect不变 | 实例BaseDamage为12，实际出牌仍造成6伤害 |
| 仅把Inflame传入的kind从damage-additive改为attack-block，Power ID仍是strength-power | 实际Strength ID实例变为attack-block；Strike伤害6，额外获得2格挡 |
| 仅把Inflame的Power ID改为从未定义的tutorial:undefined-power，保留kind=damage-additive | 包能加载，施加成功，Strike实际造成8伤害；未注册Power也能凭调用方选择行为 |

教学变体故意改变原版规则，用于验证变化边界，不是原版行为通过证据。Power实验经过Engine包加载与Session执行，没有宣称未知ID的公开Host投影也通过。

### 结构发现

1. **P1：Power调用方拥有过多语义。** [ApplyPlayerPower](https://github.com/QingXiang22/auto-the-spire/blob/a4ac0d5/mod-the-spire/base-spire/combat/powers.spire)接收definition和kind，并把调用方的kind直接写入实例；[Inflame](../../../../mod-the-spire/base-spire/cards/ironclad/inflame.spire)和[Bash](../../../../mod-the-spire/base-spire/cards/ironclad/bash.spire)等调用方同时声明Power身份和行为角色。力量ID在10个DSL文件出现16次，易伤ID在10个文件出现10次；这是静态调用位置，不是运行次数。它们不是统一Power定义的引用。未定义ID能生效的实验进一步表明，Power来源目录与可执行定义尚未形成约束。
2. **P1：Power初始化／合并／寿命仍汇聚在公共分支。** [reactions](https://github.com/QingXiang22/auto-the-spire/blob/a4ac0d5/mod-the-spire/base-spire/powers/reactions.spire)的ConfigureAppliedPower按kind设置SelfDamage、Counter和ExpiresBeforeEnemy；TickDebuff枚举三类kind。[modifiers](https://github.com/QingXiang22/auto-the-spire/blob/a4ac0d5/mod-the-spire/base-spire/powers/modifiers.spire)的SinglePowerStacks枚举corruption和autoplay-drawn-strikes。新Power若多一种策略，仍需进入这些中心文件；把ID分支改成kind字符串并没有形成完整对象边界。
3. **P1：内容私有状态穿过共享卡牌生命周期。** [CardPlayRecord.RuptureGain](../../../../mod-the-spire/base-spire/cards/schema.spire)由Power反应写入，再由[cards/rules/reactions](../../../../mod-the-spire/base-spire/cards/rules/reactions.spire)在AfterCardPlayed消费；Rupture的规则被拆进Power、公共记录和卡牌收尾三处。DiscountsOnAttack／AutoFromExhaustAtTurnEnd又进入CardDefinition、[复制](../../../../mod-the-spire/base-spire/cards/rules/operations.spire)和[永久牌→战斗副本](../../../../mod-the-spire/base-spire/cards/rules/permanent-deck.spire)的手工字段列表。类似新私有字段会继续扩大公共修改面。通用费用、HP等共享状态本身没有被本次判为问题。
4. **P2：卡牌数值与计算能力仍有重复或集中分派。** [Strike](../../../../mod-the-spire/base-spire/cards/ironclad/strike-ironclad.spire)的baseDamage／upgradedBaseDamage和damage／upgradedDamage重复；前者参与CardBaseDamage等消费者，后者用于AttackEffect实际结算，实验已证实可分离。CardBaseDamage还按DamageScale枚举耗尽／Strike／易伤层数／格挡；新增计算方式仍须改公共操作，作者没有独立数值提供方边界。
5. **P2：注册和检查重复维护可执行名单。** 内容需手动同步index、catalog及public/metadata；[PackageChecks](../../../../tools/base-spire-dev/PackageChecks.cs)的DB-A05/A10还硬编码当前55件已迁移遗物的完整字符串名单。增加一个合法定义会额外要求修改这段C#。297项原来源分母及独立源依据应继续冻结，而“当前已实现名单”可以由实际声明与执行见证做结构比对，无需再手抄一份；行为期望仍需独立来源，不能从被测DSL现算答案。

近期提交也不能全部归为无效耦合：ChemicalX确实新增了支付X／效果X的共享契约，Paper两件确实需要易伤／虚弱倍率征询，首次接入真实消费者有必要。问题在于这些能力建立后，同类后续内容是否仍扩散修改，以及私有规则是否继续进入公共schema和中心分支。遗物已有[ArtOfWar附属State](../../../../mod-the-spire/base-spire/relics/rules/combat-modifiers.spire)及实例监听可作局部参考，当前不需要先推翻Engine才能开始改善。

### 候选重构顺序与验收

以下为本次审阅建议，尚未变成生产实现或新语言承诺；原纯DSL、显式装配、单一Engine状态和来源分母继续有效。

1. **先建立Power定义边界。** 每个Power或真正共用的模板拥有身份、叠加、寿命、初始化、移除和私有状态。卡牌／遗物只提交目标、Power ID、层数及必要来源，不传行为kind／清理策略；未知定义明确失败。先迁一个简单Power和一个含私有状态的Power，保持原调用时序，再扩展。
2. **合并卡牌数值事实与数值提供方。** 同一个伤害变量的普通／升级值只定义一次，实际结算、预览和引用它的效果走同一读取入口；独立效果可以有不同变量，不能把多效果牌强行压成一份BaseDamage。新增一种计算伤害应能在其模块声明提供方，避免继续增加CardBaseDamage字符串分支。
3. **将专属状态归回所属内容能力。** 以Rupture为首例，把延迟收益关联当前play的状态放在其规则模块；共享出牌流程仅发布恰当生命周期。复制／入战／离场通过明确的能力生命周期处理，保留实例隔离、永久来源及临时状态区别，不给每个新能力再扩公共Card字段和手工复制列表。
4. **收敛登记和见证接线。** 保留显式且可审阅的装配，允许构建工具确定性生成派生名单并检查漂移；不通过运行时目录扫描隐式加载，不改JSON承载玩法的边界。删除当前已支持名单的重复手工登记，表驱动记录每件真实见证，完整源分母与行为判别不降级。

验收应使用实际变化场景：改一件已支持内容数值只改一个权威值；新增同模板内容只改自己的定义、显式装配／来源和见证；新增私有Power／遗物反应不修改公共战斗流程或其他内容模块；新增能力的私有状态不扩共享实体及复制代码。真正新增跨领域生命周期站点仍可修改对应领域契约和发布点，但下一件复用者不得重复修改该站点。

本次五项探针验证的是修改边界，未完成上述重构，也未执行新的原版独立对拍或正常Run。当前玩法候选仍在主清单，后续生产切片应依据这些发现重排，不能把本节建议或上轮CI通过记为内容隔离已验收。


## sts2-sim对照与方案修订

用户要求参考sts2-sim等实现重新考虑base-spire的内容编写负担。本轮审阅基线`a906476`，使用本地锁定参考`2089659bd07920088b93226136c72b6b6ee590ad`／tree `8eb770da4e87677c54799801488b71a8ccba687b`；已核对与[锁定清单](../../../../reference/baselines/sts2-sim.json)一致且工作树干净。阅读14份针对性参考文件并记录字节SHA256于`.artifacts/power-model-design-20261019/source-identity.json`，另核对原版PowerModel／PowerCmd及Strength、Weak、Rupture、Corruption、CrimsonMantle与两张调用卡。未更新或修改独立参考，未执行参考完整测试。

### 可借鉴的职责

| 参考代码 | 实际结构 | 对base的调整含义 |
| --- | --- | --- |
| [PowerModel](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/PowerModel.cs) | 统一Amount／Owner／Applier、InstanceType／StackType／AllowNegative及施加／移除回调 | 定义、实例和生命周期分开；调用方无需复制这些策略 |
| [PowerCmd](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Commands/PowerCmd.cs) | 从ModelDb找规范定义；按实例匹配新建或ModifyAmount，统一处理修正、回调、duration tick和移除 | base应提供稳定领域命令，以定义ID和来源调用，不由调用方传kind |
| [Strength](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/Powers/StrengthPower.cs)／[Weak](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/Powers/WeakPower.cs) | 各自声明修正、资格和衰减站点 | 具体Power文件负责自身行为，中心reactions不再枚举其角色字符串 |
| [Rupture](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/Powers/RupturePower.cs)／[Vigor](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/Powers/VigorPower.cs) | 待结算收益字典、攻击命令／开始层数保存在内容内部；复杂引用参与克隆／状态描述 | 私有状态回归内容模块，并明确实际play／card／attack身份，不能污染公共Card或另放隐藏C#状态 |
| [Inflame](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/Cards/Inflame.cs)／[Strike](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/Cards/StrikeIronclad.cs) | 卡牌只施加已定义Power；Strike的实际攻击、读取接口和升级使用同一个_damage | 卡牌减少对Power实现的了解，变量形成单一事实来源 |
| [ArtOfWar](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/Relics/ArtOfWar.cs)／[Whetstone](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/Relics/Whetstone.cs) | 遗物自己的生命周期与状态，调用稳定资源／牌组操作 | 简单内容可配模板，独特内容直接拥有自己的DSL规则，不必扩万能模板 |
| [ContentRegistry](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Content/ContentRegistry.cs)／[ModelDb](https://github.com/iRyougi/sts2-sim/blob/2089659bd07920088b93226136c72b6b6ee590ad/src/Sts2Sim.Core/Models/ModelDb.cs) | 实际注册表由具体模型类型派生并检查ID冲突；ModelDb注释里的显式列表描述与ContentRegistry实现有时点差异 | 借鉴单一执行定义源和派生登记，不照搬反射或恢复C#内容模型 |

这是职责与修改面的对照，不声明参考每项行为均与原版一致。参考的Weak仍直接查询PaperKrane／Debilitate，PowerCmd的Doom历史也有具体类型特例，不适合原样搬入我们的公共命令；base已有实例通知与pure修正继续保留。StackType.Single不应凭名字当成“重新施加绝不加层”，实际PowerCmd新建／已有路径及完整源行为仍需核对。

### 本轮改变的设计判断

先前建议集中在补Power定义、移私有状态和减少登记。本轮进一步建议调整内容作者边界：简单参数配置继续用模板；复杂内容允许一个完整DSL主模块拥有变量、状态和监听，并调用共享领域命令。当前强制use-only会把每个新特例推入额外规则模板；仅继续增加模板不能充分降低维护负担。正式候选方案、接口边界、兼容影响与验收场景唯一维护在[目录与迁移设计的修订提案](../../../design/mod-the-spire/engine-dsl-migration/plan.md#2026-10-19内容模型修订提案)。该提案尚未改变正式作者约束。

### 现有DSL的可行性原型

在`.artifacts/power-model-design-20261019/`建立独立包副本、两个内容模块及共享原型命令。C#仅作检查驱动，具体定义、层数规则、监听、私有状态和清理均在DSL。共享Apply只接owner／definition／amount，按已登记定义查找，不按具体Power ID分派；测试动作仅为刺激入口。Strength模块按自身ID参与伤害修正，教学Charge模块自行记录每两次攻击给予格挡。

| 实际命令／阶段 | 实际结果与边界 |
| --- | --- |
| `dotnet run --project .artifacts/power-model-design-20261019/probe/Probe.csproj -- .artifacts/power-model-design-20261019/base-spire`，源catalog尚未指向原型模块 | 退出0。Strength施加2层后Strike打8，降5层保留-3并实际打3；Charge连续攻击得到0／4格挡，移除后私有State数量0；未知定义在创建Power前失败。日志`probe.log` |
| 将副本Strength源行definition指向完整模块后，加参数`catalog-guard`执行探针 | 退出0。完整EngineDslCompiler接受同一模块，DslPackage按现有“catalog definitions allow only DSL template uses”拒绝。日志`catalog-guard.log` |

复现时先将`catalog-before-guard.json`复制到副本`powers/catalog.json`运行行为阶段；再用`catalog-after-guard.json`运行guard阶段。两个保存文件均在证据根目录，不能放入正式包产生未声明数据文件。

原型证明当前DSL能承载内容自有规则和状态，并定位catalog约束；它不代表生产迁移完成。定义索引用局内隐藏实体近似，尚未验证只读定义保证；施加／移除流程仅覆盖原型所需路径，未覆盖敌方、来源修正、阻止施加、所有InstanceType、零层全部边界、暂停续接、复制／状态描述、完整duration、公开元数据或原版独立比较。没有将原型加入正式Power支持分母。

本轮只修改候选设计和审阅文档，生产Engine／base／Host／参考保持。本次文档检查扫描788份Markdown、0问题，git diff --check通过；既有设计原文和原任务／ENV／W合同保留校验通过。完整玩法回归不因本次文档修订重跑。
