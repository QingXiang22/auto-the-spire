# 内容与行为迁移交付证据

BEHAVIOR-01 于2026-10-19启动、2026-10-20完成本包集成。Codex `/root` 单写，分支 `codex/content-behavior-migration-20261019`，起点为目标文档提交 `54bf08a`。本文保存这批实现和验证；后续范围与认领只在[主清单](../../tofix/README.md#当前交付)维护。接口见[实施契约](../../../design/mod-the-spire/content-behavior-migration.md)与[作者参考](../../../development/base-spire/reference/card-behavior.md)。

接手先读取主清单，再从 G05 的数值精度边界建立能区分现有四位整数链与原版 decimal 的最小案例，确定数值合同后迁移实际修正者。不要重做已迁移正文／攻击模型，也不要仅改夹取精度就关闭该目标。正常 Run、完整选择和其他剩余目标仍按原完成条件逐项验收。

## 实际改变

87个已执行卡定义均拥有唯一正文和纯伤害提供者，39个主效果广播订阅移除；正文显式保留调用顺序。一次序列捕获支付、原始X及结果区，每次重复创建独立执行记录并保存父上下文；原生选择在同一Engine栈恢复；历史计数只包含已Started记录，不把Before阶段的预建身份算作已开始。模板use顺序不再承担卡牌主效果的组合规则。

Attack拥有命令边界、段数与目标策略；DamageResult保存来源、逐击结果、实际失血和超杀。攻击群伤与非攻击DamageBatch均先提交本批目标，再发结果及死亡通知；Feed、Rupture、SelfFormingClay消费实际事实。PenNib按源卡引用及预览分支实现；Akabeko给予Vigor，Vigor按攻击开始层数消费。直接伤害保留明确玩家攻击者，不继承当前卡／play；非攻击群伤不创建伪Attack，DamageDepth保护嵌套回调。敌方Vigor按真实敌人持有者隔离；伤害前反应后的最新HP不会被旧快照覆盖。

自动打出先预取整个父批次，嵌套Havoc不会抢走后续成员。费用修正独立保存来源、操作、顺序和到期；Stomp及HowlFromBeyond的专属开关移回所属模块。共享牌区命令区分移动、主动弃牌、耗尽及回合末flush；动态关键词控制资格、留手与消耗，批次外新抽牌不追加消耗。

复制、永久副本和移除通知为费用及附魔提供生命周期接缝。Sown的永久、战斗副本和克隆状态独立，升级不重置消耗；依附身份投影避免隐藏牌通过附魔暴露。Power增加施加者、card／play及实例匹配策略，共享生命周期仍不按具体内容ID执行分支。

带来源资源命令复用于奖励与营火。SapphireSeed和DoorsOfLightAndDark的全部选项使用共享EventVisit及永久选牌／升级／移除／附魔；访问拥有页面、状态、返回位置与序号，随机流逐访问隔离。这是显式开发访问，没有开放正常地图或Run scope。

Card／Power公开ID可从实际定义库及显式启动实例确定性生成；来源分母、池顺序与可见性政策继续显式维护。登记检查区分“模块拥有定义”和“模块只调用另一个定义”，核对来源文件及公开漏登；遗物见证从实际执行登记，没有新建硬编码支持名单。

core只增加通用封闭键分派、pure显式失败、none相等及依附身份投影。core为`0.7.0`／`engine/ir@0.7`，base与公开构建为`0.7.0-dsl.40`。Host派生0.2目录增加可选Boolean Flag，复用现有wire形状及账本完成Bool应答；实体单选和取消保持原行为。旧产物不能只换标签续接。

## 来源与固定身份

原版裁决来自本地 `sts2/src/Core`：CardModel.OnPlayWrapper、AttackCommand.Execute、CreatureCmd.Damage、CardCmd.AutoPlay、CardPileCmd.AutoPlayFromDrawPile、PowerCmd.Apply、CombatManager.DoTurnEnd／FlushPlayerHand，以及Vigor、PenNib、Akabeko、Sown和两个事件。21个直接来源文件的原始SHA-256保存在本地`source-audit.json`；本批新增可执行来源行也核对既有catalog哈希，不分发原版源码。

sts2-sim保持只读：HEAD `2089659bd07920088b93226136c72b6b6ee590ad`，tree `8eb770da4e87677c54799801488b71a8ccba687b`，工作树无修改。其PenNib／Vigor简化不作为源行为裁决。

最终base包摘要为`114587163f8b31ccf67c1f23753049a5178414fcf1af0f2e6b7544471bd77f5e`，RuleSet为`f051bcaf225fe44d`，scope为`base-spire-dsl-relic-draw-dev-v1`，随机配置`vanilla-uniform@1`。行为脚手架默认seed为20261018；公开stdio使用`BASE-DSL-STDIO-00`，原随机固定向量继续使用其既有完整seed列表。

## 实际验证

本地日志位于`.artifacts/behavior-migration-20261019/`，不提交构建产物。

| 命令／证据 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | 最终完整解决方案成功，0警告、0错误 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 123通过；含重复／未知键、签名／pure、模板词法、选择续接、none相等与依附投影 |
| `dotnet run --no-build --project tools/base-spire-dev -- check --report .artifacts/behavior-migration-20261019/final` | 427通过；`base-final-complete.log`及实际`package-behavior.json` |
| `check --card-behavior-only --filter "LIFECYCLE registration"` | 登记审计进一步区分来源调用与定义拥有后，定向通过；`registration-final.log` |
| `python tools/ci/check_relic_coverage.py <上述报告>` | 58有实际行为见证，完整源分母297，未实现238；独立比较未验证 |
| `python tools/ci/check_card_coverage.py <上述报告> --require-ironclad-complete` | 87有实际见证；源分母578，Ironclad单人85/85，保留2项多人排除 |
| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | 完整组388通过；含新增stdio事件→选牌附魔→隐藏副本→抽牌→实际出牌路径 |
| `python tools/doc-check/check_docs.py`、`git diff --check` | 799个Markdown、0问题；差异无空白错误 |
| `python tools/contract-check/check_contracts.py` | 210通过；沿用已有boolean／optional_fields与schema形状 |
| `python tools/ci/check_dsl_migration.py --configuration Debug --output .artifacts/behavior-migration-20261019/migration-contract` | 通过；8活动工程、153退休源码组、11原suite；严格regression仍exit3／0执行、17项unsupported，不是对拍通过 |

首次完整base进程在会话切换时停止，无最终完成记录，因此没有计作通过；完成重跑后才计入完整通过；随后直接伤害批次及Started历史补丁按最终包重新集成。首次公开42项中41通过，新增事件在Bool应答处具名失败；补充通用Flag声明／映射／提交后运行完整net组388通过。没有删减旧用例、降低断言或用空效果替代。

## 本包没有关闭的条件

G01的正文归属、现有单人卡的执行／攻击／结果基础、费用与复制接缝已有上述运行证据。它们不等于G01～G10和A01～A17全部验收：完整decimal与资源边界校准、完整TargetType／自动支付策略、有序多选和独有回合末手牌正文、完整Power StackType及所有来源模型仍需各自源案例。其他附着种类不能从Sown推定支持。

G09仍缺正常地图／事件池、子战斗挂起返回和原M01公开独立比较；G10的其他内容种类派生尚未覆盖。原五个正常scope继续明确unsupported，原ENV／W、完整源分母、召唤／复活暂停和原严格suite合同不变。独立适配未恢复，所有当前行为报告的`independent_verified`保持false。
