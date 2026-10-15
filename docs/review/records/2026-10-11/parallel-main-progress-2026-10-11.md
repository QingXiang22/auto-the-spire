# 本地参考登记与主项目并行推进（2026-10-11）

本轮将BMingSY写入net规范，完成下一回合抽牌共享机制及正常公开Boss集成，并复核DSH就绪卡和Headbutt对拍。接续先查[主清单T04](../../tofix/README.md#env-t04)、[T06](../../tofix/README.md#env-t06)、[T12](../../tofix/README.md#env-t12)，不要重新执行已完成的内容扫描；当前认领/下一包/验收只维护主清单。

## 用户安排与文档落点

用户要求将本地 `reference/sts2-ai-mcp/` 作为主要参考写入相关文档，并先不动AI，继续并行推进主项目。本地HEAD核对为 `5d1d36b24337d7dd27fe125682e4c51fb9f76aa0`；[net规范](../../../design/net-the-spire/spec.md)和[设计§8](../../../design/net-the-spire/design.md#8-bmingsy本体适配器参考)定义参考文件、决策/线程/动作/可见性职责及适配接缝。没有运行社区mod或启动本体适配器生产。

主清单W-04保留PyTorch技术选择及完整模型范围，工程和正式训练依用户安排暂缓；ai目录未修改。core/参考保持原样。采用nbj-write-clearly和auto-the-spire-handoff整理方法；生产按karpathy-guidelines控制范围。DSH通过[dsh-remote-operator](C:/Users/26877/.codex/skills/dsh-remote-operator/SKILL.md)协调。

## Codex共享机制交付

任务 `ENV-T06-next-turn-draw-20261011`：PowerRules注册 `handDrawNextTurnOnce`；PowerRuleRuntime将层数快照、手牌抽取数量修正和一次移除编译为Engine IR；CombatSlicePackage只接回合站点。所有状态在Engine Power实体中，自定义命名空间复用同一规则，没有具体Power ID分支或额外C#状态缓存。

源依据为 `sts2/src/Core/Models/Powers/DrawCardsNextTurnPower.cs`、`Entities/Creatures/Creature.cs` 的BeforeTurnStart、`Combat/CombatManager.cs` 的SetupPlayerTurn/AfterSideTurnStart。开始时已有Power才参与，收益使用修改时当前层数；抽牌仍走正常Draw过程，受NoDraw/手牌上限约束，随后移除。开始期间新施加的Power留到下一回合。Predator普通15/升级20攻击后施加2层，cost2、SilentCardPool；直接DSL接入。

公开重放发现本包初版快照字段不允许负值，负力量造成ValueOutOfRange；已改为与Power.Stacks相同的-99..99，并增加准确反例。最终实际Boss轨迹通过，不以忽略异常替代修复。

编写方法见[下一回合抽牌指南](../../../design/mod-the-spire/base-development-guide.md#编写下一回合抽牌收益)及[编排规格](../../../design/mod-the-spire/base-orchestration/spec.md#下一回合抽牌)。旧grantPower文本钩子的名字仍受静态规范表限制；本包开始期间施加的反例通过受控作者模型构造，不能将其写成该旧钩子的自定义名字链接已完成。新Power规则注册和卡牌引用本身可用。

## DSH交付核对与并发边界

此前就绪内容会话 `session-d9b9a494-16ca-497a-8507-70c6d66d5109` 查询为停止，seq1229，实际结束为completed；[29卡交接](../2026-10-10/dsh-ready-content-handoff-2026-10-10.md)与本地文件/日志存在。负责人在其实际DLL上独立复跑Finisher、Malaise、Eradicate、DaggerThrow、ThinkingAhead、Survivor、Suppress三种模式，21项通过；X费与历史牌各含独立输入分支。没有将29张全部提升为独立原版一致性验收。577原版完整分母不变。

并发包 `ENV-T04-headbutt-choice-20261011` / `session-85bdb279-671e-4458-a505-a5177525005f` 只写tools/differential；[执行交接](dsh-headbutt-choice-handoff-2026-10-11.md)。原case过去仅一个弃牌候选，参考会自动选择而不请求决策；增加第二个候选后真实调用既有选择源，保留原ID、目标、选择及actions/decisions/facts范围。两侧分别读取选择后pile.draw.top，没有改参考玩法或复制另一侧结果。负责人在新独立输出构建并复跑正/负例，确认两侧目标与弃牌两决策、Defend置顶，改变参考选择报decision.chosen首差；其他16case定义与旧审计一致。

Headbutt单case verdict=passed，严格--only命令仍返回1/complete=false。选中case没有streams，比较器mutant/stream自检报1失败；负例准确返回diverged。单机制结果与完整suite/全局自检分别结算，没有删自检、分母或未支持项。严格完整17-case suite没有本轮通过结论。

审核之后已给B派原vulnerable-decay-via-bash接续包 `ENV-T04-vulnerable-decay-20261011`，会话 `session-fb139ad7-8c0c-4fbe-ab7a-f4ab685b00ee`；[提示词](../../../../.audit/dsh-vulnerable-decay-2026-10-11/task.txt)、[accepted记录](../../../../.audit/dsh-vulnerable-decay-2026-10-11/task-record.json)。边界仍仅工具、独有audit和固定交接，使用独立 `.artifacts/parallel/dsh-vulnerable`。accepted不算新包完成，当前状态查T04。

## 实际检查与正常流程

命令工作目录均为仓库根；主证据目录 [.audit/parallel-main-2026-10-11](../../../../.audit/parallel-main-2026-10-11/)。Codex与DSH分别使用独立.NET artifacts-path，默认Host只有负责人构建，没有并发覆盖共享obj/bin。

| 检查 | 实际结果 | 证据 |
| --- | --- | --- |
| 最终Slice构建 | 退出0；0错误/3项既有nullable警告 | build-final.log |
| `--next-turn-draw-only` | 10项通过：三升级路径、叠加、上限、NoDraw、开始期间新建/叠层、自定义规则、敌人范围、负层数及拒绝 | next-turn-draw.log |
| `--next-turn-energy-only`、`--orchestration-only` | 9项与24项通过 | next-turn-energy.log、orchestration.log、targeted-checks.json |
| 旧DSH已构建DLL代表卡复核 | 7张/21项，逐命令退出0；DLL SHA256保存 | dsh-review.json及review-*.log |
| 正式内容/生产工厂读取 | 176卡、43Power、38敌人；实际覆盖178行、176注册，不等于577全覆盖 | content-identity.json、content-coverage.log |
| 最终隔离Host构建与公开第一幕 | 构建0；ACT1-BASIC-10；457合法提交；16节点；Boss173HP/Slippery8；26HP/404金币，幕完成而非整局Victory | host-build.log、public-act1-final/summary.json与public-trajectory.jsonl |
| 默认Host构建与身份核对 | 构建0/3项既有警告；describe身份与成功重放/刷新后的正常夹具一致 | default-host-build.log、default-host-describe.json |
| Headbutt负责人独立构建/正例/负例 | 构建0；正例执行1/reference_ran1/verdict passed，负例verdict diverged；严格子集退出1，自检失败仍明示 | headbutt-build.log、headbutt-positive与headbutt-negative/regression.json |

公开重放只提交公开候选，没有读写State/RNG或改HP/路线。Boss夹具只刷新本次公开身份与base PublicBuildId，457条候选/26HP断言保持原样。dev-map base PublicBuildId `eb51c16bfab3657a`，ContentDigest `c69f5db213a21894`；公开ruleset `net:ruleset:9b1449d9771b286d`、catalog `net:catalog:86d4bb8509b96bb5`、backend `net:backend:simulation-0.2-4721fb5a726be028`。

按用户加速安排，没有重复全量Engine/Slice/net或整个solution/严格suite、第二次同种子整局、真实客户端或训练。局部行为、实际加载、正常公开Boss成立；完整角色、全部房型、后续幕、药水/遗物等父项仍需原合同证据。本包生产无剩余小项，未验证的独立对拍与完整身份断言集成按主清单接续。

文档检查 `python tools/doc-check/check_docs.py` 最终扫描359份Markdown、0问题；`git diff --check` 退出0。检查日志位于本轮主证据目录。
