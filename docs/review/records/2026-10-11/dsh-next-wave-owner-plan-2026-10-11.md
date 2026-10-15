# 药水闭环后的负责人复核与并行派单 — 2026-10-11

本轮核对B/C交接，复用负责人已完成的药水编排，安排三个目录不重叠的完整包。任务排期/认领/状态只维护[主清单](../../tofix/README.md)，本页保存本次决策及证据，不关闭M01或完整原版范围。

## 接手第一步

从主清单T03/T04/T05打开本轮task-record及task.txt。先核对固定交接、实际文件/输入摘要、结束原因与证据，再验收；accepted只是收到任务。执行者交付后停止，负责人根据实际差异接续，不能自动把后面的建议当新生产认领。

## B/C交付核对

负责人按原记录查询一次：B/C均停止。完整固定through_seq分页结果为B seq851、852 records，C seq598、599 records；任务/session/交接相符，结束消息与固定交接可核对。[完整历史和末消息](../../../../.audit/dsh-plan-2026-10-11/)保存读取证据。

B的工具输入自固定交接未变。负责人以药水最终构建的实际Differential DLL独立执行完整原17case：13 passed / 1 self_checked / 2 diverged / 1 unsupported，reference_ran16，self-test14/14；rc1，完整分母/记录/首差均保留。[实际严格报告](../../../../.audit/dsh-plan-2026-10-11/owner-regression/regression.json)和[命令日志](../../../../.audit/dsh-plan-2026-10-11/owner-regression.log)为本轮实测。初始化声明、独立脚本零随机门槛及enemy-death-aftermath/vulnerable双侧通过可以限定验收；完整T04仍待验证。

`power-turn-end-decay`首差仍为敌人Power层数2对1。已核对BaseBackend.GivePower无条件Ticks=false/SkipNextTick=false、参考ApplyInternal及原case初值；修复归属工具初始化适配，先核对实际能力声明/源时点，不改base玩法或过滤Power比较。原rng-initial-shuffle遭遇首差、random-reshuffle-unsupported仍开放。

C交付的MapDraws Hidden声明、派生可见性、公开错误/候选/重发检查、本地显式读取与独立Host选择已核对。C旧身份和213项Python是原构建证据，不迁移成最终身份；最终构建的net416、完整Slice738及正常公开Boss327快照证据见[负责人药水交付](potion-flow-owner-delivery-2026-10-11.md)。此前并行引起的构建/药水容量首错已在负责人包修正。诊断通道隔离子包限定验收；完整T03仍待验证，本轮未重跑C旧213项Python。

## 并行安排与验收

| 包 | 主责 / 文件边界 | 本轮完整成果 |
| --- | --- | --- |
| 战斗药水复用族 | A；potions.spire、本族新检查、最小Program入口/支持数更新 | ExplosiveAmpoule群体unpowered、HeartOfIron后续Plating、LiquidBronze后续Thorns；仅复用确已成立的IR/Power，需要公共修复的瓶留具名未支持 |
| 受控Power初值 | B；tools/differential | 实际衰减能力2→1，非衰减对照及错误初值负例；完整17保持分母与严格红态分类；正常Run首差只给窄归因方案 |
| 公开药水交互 | C；net/client新检查及必要通用客户端缺口 | 同实例领取、容量与空槽、消耗、未支持瓶、重发一次生效、旧观察/候选拒绝与目标决策；原始帧过schema，正常与受控证据分列 |

提示词分别为[A完整边界](../../../../.audit/dsh-potion-reuse-2026-10-11/task.txt)、[B完整边界](../../../../.audit/dsh-power-fixture-2026-10-11/task.txt)、[C完整边界](../../../../.audit/dsh-potion-public-2026-10-11/task.txt)。派发实际session/request/accepted只在各task-record及主清单相应条目记录。三个包均使用Harness默认模型、新专用会话；不恢复旧会话。

C立即使用负责人已有author-final不可变Host及实际SHA/describe身份，不等待A补内容，也不把当前源码当该DLL身份。其脚本消费运行期声明，负责人最终用合并构建再复放。A/B构建分别使用本线独立artifacts目录；C不写冻结Host目录。主清单、共享IR/生命周期、身份及公共Boss夹具由Codex单写，DS没有跨层修改权。

A只做三个已有效果复用候选，不扫描577卡或扩写37瓶。需要新公共机制的内容先交反例；这样可以验证低上下文的作者路线，同时B/C继续补M01可信度门槛。能力层数、声明数和构建成功分别不足以认定后续效果成立。

## 后续推进判断

三个包回收后，负责人先复核B的初值契约和C的公开分支，再集中集成A内容/身份/公共夹具。下一关键工程是正常Run预消费/遭遇与真实重洗的源一致性；依B实际首差由Codex决定T08最小生产包，然后接战斗奖励/永久写回双侧case。不能因修了受控case把正常Run算通过，也不因第一幕Boss可打完开放完整Ironclad身份。

完整商店/事件/古代人、后续幕/幕转换、剩余药水、角色机制与全部内容仍在原ENV/W范围；AI、本体适配器生产及用户暂缓的召唤/复活族没有启动。本轮负责人只改规划/契约/主清单与证据，不修改生产玩法。

本轮doc-check与git diff --check通过；只独立执行已有最终构建的严格17项回归，没有重复生产构建/Engine/Slice/net完整套件。三个新包均已收到accepted，不等待执行者完成或计为已交付。
