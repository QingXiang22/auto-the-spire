---
task_id: ENV-T08
dispatch_id: ENV-T08-byrdonis-elite-20261013
session_id: session-2dd01a31-b0c3-4128-b182-fbeca7038dbe
delivery_status: ready_for_review
updated_at: "2026-10-13T11:38:00+08:00"
timezone: Asia/Shanghai
dispatch_record: ../../../../.audit/dsh-byrdonis-elite-20261013/task-record.json
---

# Byrdonis 精英机制包交接 — 2026-10-13

本包已使正式 Byrdonis 精英正常加载，并在原 seed L 公开路线自然击败、领取奖励和进入后继房间。最终 Engine 99项、Slice 842项通过；完整 solution 构建受到 B 线 differential 编译错误影响，保留失败，提交负责人复核而非全项目验收。

归属：ENV-T08 精英遭遇/消费者；共享拥有者回合末 Power 能力同时归 ENV-T06。有效认领、排期与父项完成条件只看[主清单](../../tofix/README.md)，具体授权与验证要求见[派单](../../../../.audit/dsh-byrdonis-elite-20261013/task.txt)。本会话不修改主清单，不关闭父项，不实施 PhrogParasite、另一怪物或幕转换。

## 接手后先做什么

负责人先查看[命令返回码](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/final-return-codes.txt)和[公开摘要](../../../../.audit/dsh-byrdonis-elite-20261013/runs/seed-L/summary.json)，不要将 rc4 当成 Byrdonis 仍未加载。B 修复自身 differential 的访问级别错误后，由负责人串行复核 solution 与受影响消费者。下一怪物需要新派单；本会话没有自行接续权限。独立参考对拍及 net 最终集成仍由对应主责/负责人安排。

## 实现与源依据

- [enemies.spire](../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/enemies.spire)：普通 HP 从错误90改为81..84，tough从81..84改为90。Territorial 改为 `on=[trigger(event=afterOwnerTurnEnd,effect=grantPower(Strength,sourceStacks))]`；原有 SWOOP17/deadly19、PECK3×3/deadly4×3声明不改。依据：[Byrdonis源码](../../../../sts2/src/Core/Models/Monsters/Byrdonis.cs#L20-L50)。Ascension>0仍不开放。
- [SpirePowerSource](../../../../mod-the-spire/base-spire/BaseSpire/Slice/SpirePowerSource.cs)：回合末槽位复用 grantPower 读取器与 `gain-power-per-stack:1:<id>`，注册守卫认可该行为；没有怪物ID分支。Power资源发现与敌人起始Power链接继续走既有正式路径。
- [TurnCompilation](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/TurnCompilation.cs)：复用 OwnSide 门控和 ApplyPower，按当前Stacks向Owner施加，源Power不递减、不移除、不首次跳过、不在施加当时额外触发。依据：[TerritorialPower源码](../../../../sts2/src/Core/Models/Powers/TerritorialPower.cs#L21-L27)的 participants.Contains(Owner)。状态全部归Engine，无新增可变C#游戏状态，无新增随机抽取。
- [EnemyCompilation](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Compilation/EnemyCompilation.cs#L398-L405)：必要共享接缝修正——SelectMachine原来只发布NextMove而未写回MachineState，导致第三轮又执行PECK，局部伤害实测17→12→15。现在同时写回MachineState，恢复SWOOP→PECK→SWOOP，实测17→12→19；固定循环MonsterAi零抽取。该接缝影响所有已加载状态机，不只是Byrdonis，最终Slice保留现有状态机检查。
- [Byrdonis检查](../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceByrdonisChecks.cs)、[Program](../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/Program.cs)、[全套入口](../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceChecks.cs)：三个最小判别组及 `--byrdonis-only`。受控机制夹具使用正式定义和测试专属单敌遭遇池；这不是生产缩池，也不作为正常Run证据。正式公开重放保持源池与seed，无HP修改或注入清场。
- [UnknownRoomChecks](../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/UnknownRoomChecks.cs)：旧“Byrdonis缺失故障来自精英队列”断言迁移为实际出生、精英队列来源且不退回普通队列。[SliceMapRoomChecks](../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceMapRoomChecks.cs)：保留既有受控清场/奖励检查，但不将它算正常胜利；入口随机断言改为UpFront队列不重抽，允许实际HP/洗牌各用自身流；迁移已核对语义后的身份基线。
- [Frail时长检查](../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceFrailChecks.cs)：最终Slice首差暴露旧夹具的第三轮重复施加。仅在时长用例将交替施加改为 `startWith(machineFrail, then=loop(machineClaw))`，保持2→1→0断言，不改Frail生产机制。旧夹具曾依赖MachineState未写回的错误行为；该迁移是共享接缝修正的必要消费者更新。
- [Constrict检查](../../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/SliceConstrictChecks.cs)：第二次全套在不衰减见证失败 expected3/got6，原因也是第三轮循环重新施加。仅将该见证改为一次施加后loop闲置，保留3点unpowered伤害和不衰减断言；生产Constrict不改。原失败日志保留。
- [共享编译参考](../../../design/mod-the-spire/package-compilation-reference.md#拥有者回合末按层数施加-power)：记录语法、IR、OwnSide时序、状态归属、随机及不支持范围。Diataxis接手自查：目标Reference，正文仍是Reference，未混入新任务状态表；技术验证见下文，不以分类替代规则检查。

## 实际身份

[完整构建描述](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/describe-build.jsonl)：dev ruleset `d8685ae01adaad51` / build `859e3c9a561bd7ff`；dev-map ruleset `a50a3463cb78a08d` / build `e8a6ccf78f186dda`。内容摘要 `b92b52fc1cf33816`，加载Power45、敌人39。[实现文件SHA256](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/implementation-hashes.json)保存关键文件的实际内容身份。

公开协议0.2，net ruleset `net:ruleset:a64a83e3f92610d7`，catalog `net:catalog:adcb9da61a30b527`，backend `net:backend:simulation-0.2-716ace0f01a46472`；session `session-dbf7366fcf91-1`。Host SHA256 `28074D53A897E4A19A2D90786AAFF83EEE8203F219C99E9861ED9CE8F4BFF867`。客户端版本 `run_game.py/2026-10-13-event`；重放前后SHA256一致 `A1EF7C6B3DF54E339C075EF4247914BAC0DFB693E32ADD2568047BE146FBDE4B`，只读包装 `F28AAAD4E8E8CC5EA0D7D0E73B5C27806DCA90099DD1198440C5E93C9BAE3981`。更早取证时C仍在改客户端，哈希曾为CBC178…；因此本次正常运行不是旧70步逐字重放，实际变为第68步入精英，原地图e22→e23连接不变。证据：[前哈希](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/public-driver-hashes-before.json)、[后哈希](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/public-driver-hashes-after.json)。未覆盖C文件。

## 验证与命令

所有命令工作目录为仓库根；所有构建均使用绝对 `ArtifactsPath=F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-byrdonis-elite-20261013/build`，不使用共享obj/bin。下面 `$A` 代表该绝对路径。

| 检查 | 命令 | 实际结果与证据 |
| --- | --- | --- |
| 本族局部 | `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj -p:ArtifactsPath=$A`；`dotnet $A/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --byrdonis-only` | rc0，三个判别组通过；[局部日志](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/byrdonis-local3.log)。此前夹具方法名编译错误与循环17/12/15失败已修，日志保留 |
| 精英/问号消费者 | 同上DLL分别 `--elite-room-only`、`--unknown-room-only` | rc0；[精英](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/elite-local.log)、[问号](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/unknown-local.log)；均有受控初态，不冒充正常Run |
| 最终solution | `dotnet build AutoTheSpire.sln -p:ArtifactsPath=$A --nologo -v:minimal` | rc1；[构建日志](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/final-solution.log)：首错tools/differential/NormalRewardReference.cs:46 CS0122 ReferenceBackend.Project(ModelId?)不可访问；另有SlotOf不可访问。Engine/Slice/Host已生成，未修改tools |
| 最终Engine | `dotnet $A/bin/Core.TheSpire.Engine.Tests/debug/Core.TheSpire.Engine.Tests.dll` | rc0，99项通过；[Engine日志](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/final-engine.log) |
| 最终Slice | `dotnet $A/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll` | 首次最终Slice检查在Frail时长夹具失败 expected0/got2；迁移后第二次在Constrict单次施加见证失败 expected3/got6。两次返回码-532462766（Windows工具报告3762504530），原[首次日志](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/final-slice.log)和[第二次日志](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/final-slice2.log)保留。两个见证均改为只施加一次，保留原断言；第三次全套 rc0，842项通过，见[最终Slice日志](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/final-slice3.log)。后两次只修改受控测试前置，没有生产语义变更，因此不重复已通过的Engine或公开Run。 |
| 文档 | `python tools/doc-check/check_docs.py`；`git diff --check` | doc-check rc0/0问题；diff-check rc0，仅共享工作区LF/CRLF提示；[文档日志](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/doc-check.log) |

公开命令如下，实际 rc4 是下一只精英边界，不是 Byrdonis 缺失。

```powershell
python -X utf8 .artifacts/parallel/dsh-unknown-room-modules-20261012/drive_unknown.py --host-dll F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-byrdonis-elite-20261013/build/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll --scope dev-map --seed ENV-T03-SEED-L --max-actions 140 --use-potions --shop-room auto --shop-policy buy --shop-buy-kind card --shop-remove --step-unsupported-room --output .audit/dsh-byrdonis-elite-20261013/runs/seed-L
```

## 正常公开结果及限定

[原始steps](../../../../.audit/dsh-byrdonis-elite-20261013/runs/seed-L/steps.ndjson)、[原始frames](../../../../.audit/dsh-byrdonis-elite-20261013/runs/seed-L/frames.ndjson)、[精英提取](../../../../.audit/dsh-byrdonis-elite-20261013/evidence/public-elite-extract.json)可核对：

- 第68步 ChooseMapPoint 原精英节点e23（上游e22），floor8、room4、实际Elite、遭遇Byrdonis，怪物HP84/84，初始SWOOP，Territorial实例e184为1层且不tick。
- 第73/80/87/92步 EndTurn 后意图分别PECK/SWOOP/PECK/SWOOP；力量实例e192为1/2/3/4，Territorial始终1。玩家实际战斗HP65/58/44/26；不能误用战斗中保持77的Run.Hp判断未受伤。公开原始数据包含格挡和已存在Power，因此净失血不是裸攻击17/12/19；裸伤害判别由局部检查承担。
- 第98步自然末次击杀、结算回6血，Run.Hp32，MapPhase3；99步领10金币（85→95），100步领卡并关闭奖励回MapPhase2。精英特有战利品仍未建模，不能宣称原版精英奖励全量正确。
- 101步进入floor9营火，102步Rest后Run.Hp56；103步进入floor10宝箱，104步开箱、105步跳过遗物。106次尝试进入下一精英报 `base-spire:phrog-parasite-elite` missing enemy behavior；105成功提交、rc4、run_complete=false。没有自然死亡，也没有全Run/M01/全精英/四幕结论。

公开投影还保留了一些旧战斗Power实体和tick负层数；本包仅用当前怪物关系及e184/e192判别Territorial，未对该既有生命周期问题作越界修改。独立参考对拍未运行；源码核对和受控Engine检查不等同双侧通过。net套件最终集成归负责人，本会话未运行。新怪物与幕转换均未实施。

## 接手自查与维护位置

目标和权限在派单/主清单；可复用改动在“实现”；首个动作是B修复后负责人复核solution；完成条件与裁决者是派单/主清单负责人；未验证在“验证”和“限定”；唯一状态维护仍是Codex主清单。上述为作者接手自查，不是独立读者验证。长日志留本包audit，交接固定在本文件，不另造当前状态表。
