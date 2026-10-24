# core、纯 DSL base、net 与迁移检查

规则目标见[锁定 sts2-sim](../../reference/README.md)。当前随机与正常开局向量使用锁定来源；旧全内容清单仍表示原迁移范围。各组通过不代表全内容已迁移或客户端保真。CI 报告分别保留目标锁定清单与实际检查范围。

本页给出 [GitHub Actions](../../.github/workflows/core-base.yml) 的检查入口。在仓库根目录执行，需要 Python 3.12 和 Git；运行时检查另需根 `global.json` 指定的 .NET SDK，文档组另需 [Vale 3.24.0](../doc-check/README.md)。默认 Release，可用 `--configuration Debug`。

功能分支由PR的创建／更新触发五组检查，main在push后验证，另保留手动触发；同一功能分支提交不再同时运行push和PR两套矩阵。尚未创建PR时，分支push不会自动运行本工作流。

```powershell
python tools/ci/check.py core
python tools/ci/check.py base
python tools/ci/check.py net
python tools/ci/check.py migration
python tools/ci/check.py docs
```

批量内容修改完成后，可以一次选择多个组：

```powershell
python tools/ci/check.py base net migration docs
```

各组按给定顺序自动运行并在最后统一汇总；组内失败停止依赖它的后续步骤，其他组继续执行。任一组失败则整批返回非零，保留第一个失败退出码。默认各组仍写入`.artifacts/ci/<组名>/`，汇总写入`.artifacts/ci/batch-result.json`；多组命令的`--output`指定共同父目录，单组命令仍指定该组目录。一次批次只列实际受影响的组，不在每张卡完成后重复调用各内部步骤。定向排障命令见[base验证指南](../../docs/development/base-spire/how-to/verify-and-debug.md)。

| 组 | 实际执行 | 结论边界 |
| --- | --- | --- |
| core | 构建并执行通用 Engine 检查，包括同包文件组合 | 不等于原版玩法验收 |
| base | 通用加载并启动纯 DSL 包、执行当前机制行为、正常开局及固定源地图／队列比较、核对 DSL 遗物及卡牌执行结果 | 机制 scope及部分正常开局；铁甲战士卡牌检查要求源池 85 张单人铁甲战士完整且有实际执行结果，保留90项新版来源及五项多人排除 |
| net | 通用 wire／text-rules 检查和纯 DSL 公开路径，包含真实 stdio 子进程 | 含seed 777的真实Neow至首战奖励领取／跳过及地图返回；完整正常Run的snapshot／control／formal检查尚未迁移 |
| docs | 检查 Git 文档（含共享 AGENTS.md）的链接、锚点、JSON 示例、需求引用、Vale 项目术语和差异格式 | CI 安装固定版本 Vale 并校验 SHA256；检查不读取本地原版源码或历史产物 |
| migration | 准备锁定参考，校验构建依赖隔离、153 个固定版本的源代码组与 11 份原 suite；执行 Strike／重洗双侧、逐次随机诊断、正常开局预检、共同动作四路径及严格失败检查 | BattleFriend的13个及Wriggler／Infection的24个受控组件场景须通过；Strike 通过，重洗保留真实首差；正常领取／跳过及目标选择共四路径返回地图，已比较状态、公开牌堆与逐边界协议检查通过；完整公开投影、逐次随机与事件等维度仍缺，因此非零；其余原用例未支持，不代表完整差异测试验收 |

首次非零使当前组失败。原始日志、退出码及 `result.json` 写入 `.artifacts/ci/<组名>/`，Actions 上传各组 artifact，保留 14 天。base 的行为报告标记 `independent_verified: false`；迁移清单中的原检查均标记未执行。固定版本的文本 SHA256 采用 UTF-8 与 LF，跨检出换行保持一致。

base组的 `check --normal-run-only` 保留10组固定字符串seed，比较完整前置遗物袋、三幕房间队列、Neow候选、Gaussian拒绝抽样，以及首幕最终节点／房型／父子边和随机游标；源向量的[生成入口](../rng-oracle/README.md#重新生成)与生产规则隔离。公开正向路径从Neow实际获得遗物到首个真实战斗，不注入敌人、清场或奖励。未支持选项／遭遇保留在源池并验证失败无额外状态／随机消费；同组还验证正常首战领取／跳过返回地图、自然死亡与受控末杀／余效顺序；完整整局及逐步双侧比较仍不在本组验收范围。

migration另运行[七个固定轮转敌人组件比较](../differential/README.md#固定轮转敌人组件比较)，要求原50项全部一致，包括玩家致死时Frail清理与Vigor保留。另执行[Power改量组件](../differential/README.md#power改量组件比较)的96项，保留新建／叠加／直接Modify、Artifact、正负增量／零量、临时力量、实例模式及真实来源。两组均实际返回0并核对完整边界；组件初态与部分时点为受控设置，不表示完整正常Run或全部内容验收。

铁甲战士卡牌检查可单独复查：

```powershell
python tools/ci/check_card_coverage.py .artifacts/ci/base/behavior/package-behavior.json --require-ironclad-complete
```

它校验当前包摘要、578 项全卡来源、锁定新版原生铁甲战士卡池、资格和来源摘要、普通／升级元数据对应的行为报告和全部已声明卡牌执行结果；不将局部 Ironclad 检查解释为全卡或原版独立比较通过。当前池元数据由锁定参考独立执行生成，并与当前锁核对；旧578项清单仅保留原迁移来源条目总数，CI不需要本地 `sts2/`。

完整来源固定数据位于[包外工具 fixture](../base-spire-dev/fixtures/source-catalogs.json)，共10个领域1516项，保留来源路径、原始字节SHA256、池及成员顺序和源排除依据。base工具嵌入该文件，Python覆盖检查与定位助手读取同一份已纳管数据；生产目录只登记已加载定义。来源条目总数和实际执行结果分别校验，干净检出无需本地原版源码或历史报告。

migration还要求[回合阶段与监听顺序组件](../differential/README.md#回合阶段与监听顺序组件比较)的28项全部一致：实际Orichalcum／Plating、Charon／DarkEmbrace、起手阶段、Run与Direct顺序，以及末杀后的抽牌与格挡门禁。保留逐次随机参数、候选、结果、游标及完整流状态；原50／96项和11份suite继续执行，不以组件结果认证M01。

问号房型另有两seed共10种受控组件场景，由migration逐边界核对18次抽样与赔率重置；`normal_run_verified`保持false。base正常组和net真实stdio新增seed `unknown-normal-354`，从Neow、首战胜利及奖励关闭进入真实问号Monster，保留原始地图类型并核对实际遭遇。base还比较源生命、访问、四类赔率与完整问号流状态；完整事件和Run不在该前缀验收范围。

首幕事件选择增加两seed共42种受控双侧场景，migration核对完整31项资格、每种41个边界、最终事件标识／游标／访问历史及无随机消费。base正常组与net真实stdio保留新增seed `event-normal-11` 的真实胜利后问号Event路径，核对源选择与未接效果的明确失败；不会创建空访问。此选择器比较不替代各事件效果、父子战斗或后幕验收。

正常事件访问另保留seed `sapphire-normal-79`：真实Neow、首战胜利及奖励跳过后自然进入SapphireSeed，分别执行EAT与PLANT并返回真实地图后继。base核对独立原生向量的资源、永久实例／升级／附魔、完整问号流、访问双向引用及重复关闭拒绝；net真实stdio验证公开访问关系、请求重放与地图返回。PLANT按锁定来源自动选择首张合资格永久牌。

seed `dense-normal-30` 自然进入DenseVegetation，TRUDGE_ON及REST后四Wriggler真实子战斗分别覆盖奖励领取／跳过和地图返回。base行为报告与正常组比较独立原生向量的资源、永久牌组、父子访问及完整Run／玩家／事件随机流；另检查受控踩踏致死、真实子战斗死亡、旧决策、重复FIGHT和错误关联原子拒绝。net真实stdio验证三条正常公开路径及请求重放；migration核对固定向量来源摘要。BattlewornDummy的开发升级档另用两个seed的独立组件向量核对击杀／超时后的同父恢复、永久升级实例与完整随机流；行为检查保留死亡、空／单张升级域、未支持奖励档与重复访问，真实stdio验证超时返回和重放。第三幕正常可达、药水／遗物档、其他未接事件、后幕和完整Run仍未支持。

## 原完整差异测试与覆盖缺口

Neow效果另用独立原生`neow-effects-vector.json`验证seed777的WingedBoots／CursedPearl正常获得至首战；受控组件分列三次自由旅行及两seed正负初始力量下的FlexPotion叠加、改量、攻击与到期。base完整行为组消费这些向量，net真实stdio验证两条正常公开开局、原首战FlexPotion奖励领取和开发药水生命周期，migration核对来源摘要。完整熔化操作、后幕及全公开比较仍未验收。

```powershell
python tools/ci/check.py differential
python tools/ci/check_relic_coverage.py .artifacts/ci/base/behavior/package-behavior.json --require-complete
```

当前这两条命令分别返回 3 和 1。原 regression 的 strike-normal 通过原 actions／decisions／facts／pile-order 比较；random-reshuffle-unsupported 实际执行双方原三次结束回合，各抽取 9 次洗牌随机调用，并保留流状态与玩法首差。其余 15 个 case 明确未支持；测试集仍非零且 `independent_verified: false`。完整源遗物覆盖仍缺，不能以局部行为测试结果关闭完整验收。Actions 当前矩阵采用 `migration`，校验两个原用例、源流独立向量，以及缺参考、缺维度、零执行、错误终点、候选错序、参数差异、记录丢失和首差等失败路径，没有以 `continue-on-error` 吞掉完整差异测试失败。

旧 C# Slice、作者探针、Net 游戏专用组与独立适配器保留在[迁移对照](../base-migration-baseline/README.md)。逐组源标识、原场景名和恢复去向见[CI 迁移数据](fixtures/legacy-dsl-checks.json)。本次用户授权直接切换及功能回退，不代表原判断意图消失。

## 其他检查

文档组在 PR CI 执行；本地可运行 `python tools/ci/check.py docs`。忽略文件不能作为正式链接目标。本地原版源码核对、Python 客户端完整轨迹、AI 离线检查、完整正常 Run、完整差异测试／120 对矩阵及性能均不包含在上述行为组中。相关要求仍见[任务入口](../../docs/roadmap.md)、[ENV 范围](../../docs/design/environment-completion-and-differential-v1.md)和[参考版本](../../reference/README.md)。不得从开发检查通过推导这些项目已经验收。

migration组另严格执行`exhaust-cause-compare`的30个耗尽原因组件，检查全部边界、延迟计数、抽牌事件和随机候选；缺例、缺字段、差异或无参考均失败，正常Run认证保持独立。
