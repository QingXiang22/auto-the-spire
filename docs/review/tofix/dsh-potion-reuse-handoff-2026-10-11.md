---
task_id: ENV-T05-potion-reuse-20261011
dispatch_record: .audit/dsh-potion-reuse-2026-10-11/task-record.json
session_id: session-87754af2-f636-4487-9308-1ab439871a28
request_id: 8aebaa1d-52ec-41de-a94b-286c6214c4a2
delivery_status: ready_for_review
updated_at: 2026-10-11T03:01:14+08:00
---

# ENV-T05 已有效果复用药水包交接 — 2026-10-11

本包让负责人复用 ExplosiveAmpoule 与 HeartOfIron 的正式使用程序，并获得 LiquidBronze 的可执行公共缺口反例。48项源池保持完整，支持数从11增至13，具名未支持从37减至35；LiquidBronze仍未支持。此处交付可审阅的两瓶及缺口，不代表 ENV-T05 父项或公开轨迹已验收。

归属为[主清单 ENV-T05](README.md#env-t05)，派单与本包验收范围见[原任务](../../../.audit/dsh-potion-reuse-2026-10-11/task.txt)及[派单记录](../../../.audit/dsh-potion-reuse-2026-10-11/task-record.json)。正文记录本次交付，排期、认领和关闭结论由负责人维护主清单；本会话未修改主清单或受保护派单输入。

## 负责人接手第一步

1. 在工作目录 `F:/Scylier-Project/active/auto-the-spire` 核对主清单与共享工作区，复用下列两瓶程序和检查，不重做既有库存、奖励或原11瓶。
2. 运行 `dotnet .artifacts/parallel/dsh-potion-reuse/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --potion-reuse-only`。前置条件是仍使用本包 Debug 产物；预期11项通过，记录正式 `PlatingPower=base-spire:plating-power`，并输出两个 `unregistered power` 反例。若工作区已变化，先比较[构建清单](../../../.audit/dsh-potion-reuse-2026-10-11/build-manifest.json)和[正式源摘要](../../../.audit/dsh-potion-reuse-2026-10-11/final-potion-reuse.log)，再由负责人按集成策略重建。
3. Codex决定玩家 ThornsPower 的公共实现：先处理正式降级器不接受 `before_damage` 的问题，再核对玩家/敌人方向、零伤害powered攻击以及原版 Omnislice 特例。此前保留 LiquidBronze 的 unsupportedUse。修复后须补真实后续反伤3、零伤害反伤3与非攻击对照，不能仅测获得3层。
4. 按原派单完成条件，由负责人统一运行完整Slice/net/公开Boss及必要正常获得轨迹。A线在本次交付后停止，不自行修公共机制或认领下一包。

## 已交付能力和实际修改

| 内容 | 正式实现 | 实际判别 |
| --- | --- | --- |
| ExplosiveAmpoule | [正式源:122](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/potions.spire#L122)：`unpowered = true`，对 `allEnemies` 伤害10，既有IR和动作尾 | 双敌各31→21；分别及组合安排 Strength8、Weak3、Vulnerable3，均仍10；无目标提问、不付能量、不新增随机抽取、旧瓶重复提交拒绝；双敌各10同时击杀后瓶消耗、SettledRoom结算和下一房保留另一瓶 |
| HeartOfIron | [正式源:187](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/potions.spire#L187)：普通链接施加 `PlatingPower` 7给owner；加载的通用ID为 `base-spire:plating-power` | 施加不立即给格挡，免费且仅消费一次；连续三回合7→6→5→4；第2、3回合相对无药对照少掉血6、5，实际验证后续格挡；另加Dexterity8/Frail4对照仍6、5，符合unpowered格挡 |
| LiquidBronze | [正式源:194](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/potions.spire#L194)：保留具名unsupportedUse，未启用程序 | 不出现UsePotion空动作，仍可丢弃；实际加载表中 `ThornsPower` 和敌人 `Thorns` 都缺失；最小链接反例加载0项程序，见下一节 |

新增[复用检查](../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/PotionReuseChecks.cs)，[Program入口:26](../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/Program.cs#L26)添加 `--potion-reuse-only`，[默认调用:133](../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/Program.cs#L133)接入同一检查；[PotionFlow数量:37](../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/PotionFlowChecks.cs#L37)仅调整13/35及对应文本。未新增公共C#机制、Power白名单或随机补抽；48项的元数据/顺序/稀有度和原11瓶程序保留，既有25项检查通过。

来源、语义及编辑详情见[源/构建摘要](../../../.audit/dsh-potion-reuse-2026-10-11/source-build-summary.md)。药水规则依据[开发指南药水章节](../../design/mod-the-spire/base-development-guide.md)和[库存契约](../../design/mod-the-spire/potion-inventory/spec.md)。

## LiquidBronze 的公共缺口与最小复现

[原版药水](../../../sts2/src/Core/Models/Potions/LiquidBronze.cs)施加ThornsPower 3；[原版能力:17](../../../sts2/src/Core/Models/Powers/ThornsPower.cs#L17)在 BeforeDamageReceived 检查powered攻击或Omnislice，不要求amount>0。普通powered攻击9或0均应反伤3，普通unpowered/非攻击对照不反伤；Omnislice例外需另外核对。

正式组装仅链接[实际加载的能力](../../../mod-the-spire/base-spire/BaseSpire/SinglePlayer/SinglePlayerContent.cs#L87)。[现有敌人Thorns声明:114](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/enemies.spire#L114)未降级成功，实际原因如下：

```text
Thorns.events 的事件类型 'before_damage' 未支持（只支持 damage / block / power）；运行时未实现声明的角色/触发器/效果组合：不注册部分生效的能力
```

[最小反例:126](../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/PotionReuseChecks.cs#L126)用正式加载表，分别编译单瓶 `applyPower = { power = ThornsPower stacks = 3 target = context.owner }` 以及 `power = Thorns`。实际程序数均为0，保留未支持元数据；[最终输出:18](../../../.audit/dsh-potion-reuse-2026-10-11/final-potion-reuse.log#L18)为：

```text
potion-reuse/minimal-liquid-bronze.spire(7,13): unregistered power 'ThornsPower'
potion-reuse/minimal-liquid-bronze.spire(7,13): unregistered power 'Thorns'
```

诊断名称来自检查内DSL字符串。现有敌人源还有 `positiveAmount=true`，即使完成事件降级也会漏掉源要求的零伤害反伤，因此不能直接替代玩家通用能力。本包没有声称LiquidBronze后续反伤已运行通过，也未把具名缺口检查计成新增支持。

## 构建、配置和证据

所有命令从工作目录执行；SDK9.0.306、Debug；仅使用 `.artifacts/parallel/dsh-potion-reuse` 的构建和DLL。seed=20261011UL，BaseSpireComputeBackend.Instance。新增检查读取 SinglePlayerContentSource 的正式药水/Power内容，再用作者双敌战斗、受控初始库存和状态判别语义；未建立正常获得轨迹，没有公开整幕run_id或正常Profile验收记录。

实际嵌入源摘要：potions `121e32c818cd784b`、ironclad-powers `c60f206794170e26`、enemies `80ee84a0fabc92a1`；聚合内容摘要 `7aabc15c408b2f79`。完整SHA256与工具版本在[构建清单](../../../.audit/dsh-potion-reuse-2026-10-11/build-manifest.json)。

先完成[局部反馈](../../../.audit/dsh-potion-reuse-2026-10-11/source-build-summary.md#局部反馈与更正)，再按下表顺序串行收口一次；最终源码未再修改。

| 实际命令 | rc/结果 | 证据 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln --artifacts-path .artifacts/parallel/dsh-potion-reuse -c Debug` | 0；0错误、1项既有Differential空引用警告 | [solution build](../../../.audit/dsh-potion-reuse-2026-10-11/final-solution-build.log) |
| `dotnet .artifacts/parallel/dsh-potion-reuse/bin/Core.TheSpire.Engine.Tests/debug/Core.TheSpire.Engine.Tests.dll` | 0；95项通过 | [Engine](../../../.audit/dsh-potion-reuse-2026-10-11/final-engine.log) |
| `dotnet .artifacts/parallel/dsh-potion-reuse/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --potion-flow-only` | 0；25项通过 | [药水流程](../../../.audit/dsh-potion-reuse-2026-10-11/final-potion-flow.log) |
| `dotnet .artifacts/parallel/dsh-potion-reuse/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --potion-reuse-only` | 0；11项通过，包含未支持/公共缺口检查 | [药水复用](../../../.audit/dsh-potion-reuse-2026-10-11/final-potion-reuse.log) |

solution按要求构建了本包独立目录内Host及依赖；未使用或覆盖负责人不可变Host、其他线obj/bin。未改 Power源、IR、生命周期、前端、工厂、库存/奖励/地图规则、静态身份、Boss夹具等公共拥有文件。未提交、reset、清理、派子agent或训练。

完整Slice运行、net运行、公开第一幕Boss、本包新增瓶正常获得轨迹均未执行，交Codex集成；既有公开Boss通过只是输入基线，不是本次复验。LiquidBronze的玩家Thorns加载及后续反伤仍待公共修复和负责人验收。

文档/格式检查：`python tools/doc-check/check_docs.py` rc=2，扫描379页；仅另一线的 Power 夹具交接中两个 Markdown `#L` 锚点失败，见[完整文档检查](../../../.audit/dsh-potion-reuse-2026-10-11/doc-check.log)。未越权修订该交接。`python -B .audit/dsh-potion-reuse-2026-10-11/handoff-check.py` rc=0，本包两页链接/锚点/围栏及四个源文件行尾检查无问题，见[定向检查](../../../.audit/dsh-potion-reuse-2026-10-11/handoff-check.log)；`git diff --check` rc=0，见[格式日志](../../../.audit/dsh-potion-reuse-2026-10-11/full-diff-check.log)。git diff不覆盖未跟踪新文件，定向检查补充本包范围。

本报告按 auto-the-spire-handoff 方法完成执行者接手自查：目标、当前入口、可复用成果、第一步、验收范围、公共缺口和证据均可由正文直接定位；没有独立读者验证。派单文件、原始输入摘要仍受保护，本包交付状态只维护本页头部，父任务状态及认领只由负责人维护[主清单](README.md#env-t05)。
