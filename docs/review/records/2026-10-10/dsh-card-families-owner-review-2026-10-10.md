# 卡牌族交付复核与公共作者边界

Codex `/root` 对DSH已交43张牌进行了独立源码核对与集成检查，并亲自修正了补卡暴露的公共边界。归属ENV-T12-card-content-families-20261010与ENV-T06-card-author-boundaries-20261010；本报告保存2026-10-10证据，当前状态、认领和下一包只看[主清单](../../tofix/README.md#env-t12)。core/参考保持冻结，未提交、清理或覆盖共享工作区。

## 接手入口与复核裁决

先读主清单T12和[base开发指南](../../../design/mod-the-spire/base-development-guide.md#让新增内容独立于全包大小)，再读本族源类、DSL和检查。DSH原会话 `session-70c7f4c9-c106-4fbc-9fe6-c28ceccd75bf` 已按用户要求暂停，最终投影running=false；本轮不恢复该goal。其固定交接已核对，不能把accepted或129项局部通过当成全环境验收。

43张的普通、直接升级和Armaments真实升级129项已独立重跑，正式定义均实际进入受控Engine牌组。源码核对比较了全部43张的普通费用、类型、稀有度、目标与基础关键词，并逐条审阅效果/升级源行；详见[核对产物](../../../../.audit/dsh-card-review-2026-10-10/source-audit.json)。新增20项真实回合末/开局检查，覆盖Sow/Luminesce/Wisp的保留、Parse/Defy/Defile的虚无和Backstab/BootSequence/DramaticEntrance/Assassinate的固有。此为本地执行与源核对；没有执行独立参考/client对拍，不关闭T12父项。

源分母保留主池533项、Token/Event/Quest44项，共577项。旧矩阵的主池134项声明、衍生5项声明不能作为实际加载数；新正式与诊断均加载137张卡、41个Power，正式敌人38。卡牌未支持仍为Toxic的对自身攻击语义与WellLaidPlansPower/选牌保留机制，未伪装支持。新43张之外增加的1张加载是原有Beckon因selectNone修正而解阻。

## 亲自完成的公共调整

1. `Orchestration/CardResultRules` 按Kind处理Power的None结果，优先于Exhaust/forceExhaust。`CardPileProcedures.RemoveFromCombat`只移除牌区归属，保留仍被历史/长期来源引用的实体；不触发消耗事件。依据 `sts2/src/Core/Models/CardModel.cs:2070` 与 `CardPileCmd.AutoPlayFromDrawPile`，手动与自动共享收尾，不改每张卡DSL。
2. `SpireCardSource`接受真实 `selectNone = {}` 与空play；付款、历史、钩子和结果区照常执行。无目标程序若读取所选目标（包括嵌套）明确拒绝。用空play的费用/消耗反例和Beckon“打出无处罚/留手掉6HP”区分静默空实现。
3. `SpirePowerSource.PowerDeclarationResources()`统一 `*-powers.spire` 和敌人文件中的Power声明枚举。`FormalSpirePackage`、SinglePlayer和正式行为模型使用同一注册集合；Dexterity不再因诊断漏资源而拒绝。实际跨文件施加2Dexterity后获得5基础格挡得到7，且两侧41项Power ID集合相同；没有重复声明或名称白名单。
4. `PowerRuleRuntime.EndTurnAuto`在每次随机选牌前检查战斗活跃和候选非空。Stampede无攻击手牌已复现 `ValueOutOfRange: cannot sample from an empty candidate query`，修复后无候选、上一轮耗尽候选均通过，不重复打牌。
5. PerfectedStrike旧判别依赖全包每定义一份和六张标签牌；新增其他角色Strike使实际伤害18→28。这是夹具耦合，改为明确六张正式标签牌的受控Deck，继续断言18/24与跨牌区计数。新族局部入口增加 `--card strike-silent`，实际执行3项；未知ID明确失败，不返回零检查通过。

规范补充于[编排规格](../../../design/mod-the-spire/base-orchestration/spec.md#跨卡族公共边界)；base/modding/DSL指南同步说明Power结果、无目标与注册入口。新能力仍由内容作者组合，公共生命周期与资源机制由Codex主责，不为卡名添加分支。没有证据要求推翻已有类型化效果程序；本次减少的是补卡接缝与测试范围的耦合，尚未量化token或工时下降。

## 验证与身份

命令均从仓库根目录运行，构建与规则门槛串行，日志位于 `.audit/dsh-card-review-2026-10-10/`。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 退出0，0错误 | build.log |
| Engine `dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests` | 退出0，95执行/通过 | engine.log |
| Slice `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 退出0，574执行/通过 | slice.log |
| net `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | 退出0，416执行/通过 | net.log |
| 新卡族单独入口 | 退出0，129执行/通过 | families-baseline.log |
| 作者边界与跨回合新增检查 | 完整Slice内执行；作者组32项 | slice.log、author-boundaries.log |
| 作者工具构建与 `python -X utf8 tools/base-spire-dev/verify_examples.py` | 退出0，15执行/通过 | author-tool-build.log、author-tool.json |
| 单牌局部入口/不存在ID负例 | 分别退出0/1，3项/明确Unknown ID | single-card.log、single-card-negative.log |

早期本轮首差分别为旧Beckon“不支持selectNone”断言与全包PerfectedStrike计数；均以原生语义/显式夹具修正。一次测试尚未退出时误启动构建遇到Windows apphost文件锁；等其退出后串行重建并完成门槛，没有当作规则失败或成功证据。

dev规则/构建为 `7fb25a6d4d67f735` / `6654d05771d82f3f`；dev-map为 `2eed7aa588ea45ab` / `6f17e4281bc87277`；内容摘要 `f0e3f98b604afbab`。137卡/41Power/38敌人的实际加载与诊断一致性见[content.json](../../../../.audit/dsh-card-review-2026-10-10/content.json)。新增内容及结果/生命周期语义改变构建身份，固定golden由负责人据实际工厂重录，没有取消断言。

## 尚未验证与接续边界

正常Engine与独立公开Host均用 `ACT1-BASIC-10` 重新跑通：正常80HP/99金币/10张起始牌、源85项单人奖励池，16节点、457次合法动作/应答，击败173HP/Slippery8的Vantom，最终26HP/404金币，ActCompleted=true/Running，无整局Victory。宝箱与药水仍合法跳过；此只证明开发范围一条路线。当前源值修正后的结果替代旧32HP轨迹，固定公开身份和候选均由负责人重录，Host每步重新验证公开合法候选；[完整公开证据](../../../../.audit/dsh-card-review-2026-10-10/public-act1/summary.json)及同目录public-trajectory.jsonl不访问隐藏状态或RNG。

两次独立Host均退出0，458个公开快照只除session_id后逐项一致，比较产物为[public-repeat-comparison.json](../../../../.audit/dsh-card-review-2026-10-10/public-repeat-comparison.json)。传输包的随机request_id不属于快照，不参与比较。公开身份为规则 `net:ruleset:73fdb0039085816a`、目录 `net:catalog:c4c7bd9f435b3fd8`、后端 `net:backend:simulation-0.2-3a29bc7a6a6860a7`。文档检查342个Markdown/0问题，git diff --check退出0；LF/CRLF提示与原共享修改一并保留，不整理他人文件。

独立参考/client比较、任意seed/全房型、宝箱全遗物领取/药水完整取得、后续幕、其他四角色开局与特色资源均未因此验收。下一内容包先验证Footwork、Prowess、EternalArmor、Debris，再继续已有原语可完整表达的族；不凑批次牌数。Curse类型/负费用、选牌、下回合资源、毒/球/召唤/星星等公共缺口仍交Codex，不用Status/cost0/即时资源或空效果绕过。

复核后已通过[dsh-remote-operator技能](C:/Users/26877/.codex/skills/dsh-remote-operator/SKILL.md)派发新会话 `session-f9cd8a61-d601-461f-9a07-d845a82c8e54`，任务 `ENV-T12-unblocked-card-families-20261010`，响应accepted；[任务文本与边界](../../../../.audit/dsh-card-boundaries-content-2026-10-10/task.txt)、[记录](../../../../.audit/dsh-card-boundaries-content-2026-10-10/task-record.json)。沿用Harness默认，未取回执行结果、未把接受当作完成。当前进度/认领仍只维护主清单T12。
