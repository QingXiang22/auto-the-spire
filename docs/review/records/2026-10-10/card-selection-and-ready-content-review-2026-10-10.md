# 通用选牌交付与就绪内容接续复核

2026-10-10；Codex `/root`。任务归属为主清单ENV-T06/ENV-T12；本页保存本次构建与复核证据，当前状态与下一包只看[主清单](../../tofix/README.md#env-t06)。用户要求优先可用成果并减少非必要检查，本轮按此收紧验证。

接手先运行 `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests`，再用已构建DLL执行 `--card-selection-only`。新选牌卡写法看[独立源文件](../../../../tools/base-migration-baseline/BaseSpire/Cards/Content/selection-cards.spire)，不重做公共编排或恢复暂停会话。下一共享缺口是下一回合资源与弃牌消费者；内容线只接主清单当前DSH包。

## 实际成果

负责人核对原生Footwork/Prowess/EternalArmor/Debris/Skewer源码与DSL，并独立通过11项后续收益/时序、3项X=0/1/3聚合及3项逐击Strength/Weak/Block判别。限定正式源、本地行为接收5张；独立原版/client执行对拍未做。[执行者交接](dsh-unblocked-cards-handoff-2026-10-10.md)保留其156项检查和旧Status固定数量首差；移入records不关闭T12父项。

本次Codex亲自交付 `ChooseCardsEffect`、`CardSelectionProcedures`、牌区模块的选中牌移动及 `onDiscard` 事件接缝。实际状态/答案仍在冻结Engine内，效果模块只生成决策IR，根组包器只接线。来源/去向/精确数量/筛选/置顶均为类型参数，不为每个组合增加AuthorStep字符串或逐牌分支。

Acrobatics（3/4抽后弃1）、Prepared（1/2抽后弃1/2）、Hologram（3/5格挡后取回；升级去Exhaust）只增加独立DSL文件。数量是0..9999字面量；新原版卡写autoSelect=true，候选恰好够或不足时按原生CardSelectCmd自动全选，空集无决策；旧DSL保留手动确认。多选一次提交并恢复原程序；不重复付款或记录出牌。满手牌取回留在弃牌堆，对齐CardPileCmd手牌溢出路由。

手动弃牌触发被弃实例onDiscard；普通出牌结果/回合末移动不触发。该hook仅新正式.spire前端支持，旧.author语法明确拒绝，不接受后静默丢弃。选择中仍复用公共消耗过程与原卡hook/FeelNoPain链。跨实例Power弃牌监听、弃牌历史、Sly、保留选择和可选数量区间不在本包实现范围。

旧Status固定总数断言改为Dazed/Beckon/Debris必需ID与所有声明实际降级检查，允许未来新增状态牌；未删检查。Dev固定身份与公开Boss夹具根据实际生产工厂/Host迁移，没有改HP/路线或放宽成功条件。

## 实测与范围

证据目录：[本包audit](../../../../.audit/card-selection-2026-10-10)。

| 检查 | 实际结果 | 证据 |
| --- | --- | --- |
| solution构建 | 0错误，3条既有nullable warning | solution-build.log |
| 最终Slice目标构建 | 0错误，最终源码包含legacy拒绝修正 | build-final.log |
| 通用选牌 | 12项：普通/升级真实源、付款/暂停/继续、多选、空/不足、筛选、消耗事件、满手牌、弃牌hook、旧置顶、错误源拒绝 | selection-final.log |
| DSH5张独立复核 | 11+3+3项 | dsh-benefits.log、dsh-skewer-values.log、dsh-skewer-hits.log |
| 既有编排/卡牌完成组 | 24项 | orchestration.log |
| 实际生产/诊断加载 | 在公开重放构建快照中双方154卡/41Power；生产38敌人 | content.json、identity.jsonl |
| 正常公开第一幕 | ACT1-BASIC-10；457合法提交；16节点；Boss173HP/Slippery8；完成后26HP/404Gold；无整局Victory | public-act1/summary.json、public-act1.log |

公开重放前比较RunProbe与Host实际BaseSpire.dll的SHA256一致；探针仅调用生产工厂，公开重放仅提交公开候选，不改状态/RNG。重放保留原457条候选与26HP断言，临时夹具验证通过后再更新仓库夹具。此前第一幕已有双次同种子复现，本轮不重复第二次重放。

154是本次公开构建快照的实际加载数，含并行DSH正在写的9张未验收内容；已核验底线为此前137加本轮5+3=145，不能把154全部记为新验收。新的内容线还在改变工作区；本页身份标识这个证据构建，后继批次须由负责人迁移其自己的身份，不能让卡作者逐卡改golden。

| 身份 | dev | dev-map |
| --- | --- | --- |
| Ruleset | 5e0703e89b0114f4 | 6e84026d6ddafa0c |
| PublicBuild | ca142e33c5d48170 | 92aac13463cc2ad6 |
| ContentDigest | cd9bf51e117e34fd | cd9bf51e117e34fd |

公开profile：ruleset `net:ruleset:18b350f4847a008f`；catalog `net:catalog:f2ceba23ad3440f7`；backend `net:backend:simulation-0.2-d26a33ffa4f90b3c`。三份普通内容例子和指南已跟随实现；结构上缩小了卡作者上下文，没有声称实测token节省比例。

本轮没有重复全量Engine95、完整Slice、完整net416、作者工具15或独立原版双侧；这些历史通过数不作为当前全量验收。Toxic（自体受修正伤害）和WellLaidPlans（Power/保留选择）仍缺共享机制；其余四角色独有资源、完整遗物/药水/事件/遭遇与后续幕尚未齐。第一幕开发路径可运行，不代表完整原版或训练环境完成。

文档检查346份/0问题，`git diff --check`退出0（仅既有CRLF提示）；证据为doc-check.log/diff-check.log。

## 加速接续

后继内容包 `ENV-T12-ready-content-20261010` 已通过[dsh-remote-operator](C:/Users/26877/.codex/skills/dsh-remote-operator/SKILL.md)派发：`session-d9b9a494-16ca-497a-8507-70c6d66d5109`；[派单](../../../../.audit/dsh-ready-content-2026-10-10/task.txt)、[accepted记录](../../../../.audit/dsh-ready-content-2026-10-10/task-record.json)。先LegSweep，再连续补现有能力就绪卡；仅卡DSL、RemainingCardFamiliesChecks.cs和本包交接/认领，禁止公共C#/Program/身份/夹具。卡作者使用单牌筛选反馈，不逐卡跑全量/整局；缺共享能力记录具体源依赖并继续可做部分。新派发只记accepted，未查询结果、不宣称交付。

可维护性改进落在[base指南](../../../../tools/base-migration-baseline/docs/base-development-guide.md#用通用选牌编写卡牌)、[验证节奏](../../../../tools/base-migration-baseline/docs/base-development-guide.md#批量开发时缩短反馈)与[选牌规格](../../../design/mod-the-spire/base-orchestration/spec.md#通用选牌与恢复执行)。后续优先实现能解锁整族内容的共享能力；保持一线一包，公共架构由Codex亲自维护。
