# 并发开发与下一回合能量交付复核

日期：2026-10-10。关联[主清单ENV-T06](../../tofix/README.md#env-t06)的 `ENV-T06-next-turn-energy-20261010` 与[T12内容包](../../tofix/README.md#env-t12)的 `ENV-T12-ready-content-20261010`。本记录保存此构建与协调证据；当前认领、状态和接续仍只看主清单。

接手第一步：读[并发开发步骤](../../../design/mod-the-spire/base-development-guide.md#与共享机制开发并发补卡)，按自己的输出目录构建并运行局部DLL。组合下一回合能量的卡先看同页[作者写法](../../../design/mod-the-spire/base-development-guide.md#编写下一回合能量收益)，复用已注册Power；Outmaneuver已由负责人交付，不重复编写。

## 文件边界和实际协调

Codex亲自拥有PowerRules、PowerRuleRuntime、CombatSlicePackage公共接线、Program、独立NextTurnEnergyChecks、next-turn-powers/next-turn-cards及规格指南。DSH只写原派单卡DSL、RemainingCardFamiliesChecks和其audit/固定交接，不修改公共文件与负责人示例。没有启动新的Codex子agent或新DSH生产会话；主清单在本并发窗口由Codex单写。

双方过去使用默认bin/obj会共享构建输出；这次设置不同 `--artifacts-path`，引用项目也跟随隔离。Codex路径 `.artifacts/parallel/owner-next-turn` 已实际构建及执行；DSH指定 `.artifacts/parallel/dsh-ready`。源文件仍共享，独立输出不是独立源码快照；不以另一方进行中声明扩大验收范围。

原DSH会话 `session-d9b9a494-16ca-497a-8507-70c6d66d5109` 最初status为running；[并发协调](../../../../.audit/parallel-next-turn-2026-10-10/coordination-record.json)由Harness接受，seq299已插入原会话。随后seq302因 `PI_AI_ERROR` 上游服务异常结束，seq303状态running=false，尚无完整交接。负责人在原会话以queue续发同一未完成任务，带独立输出和新Power/选牌契约，[接续记录](../../../../.audit/parallel-next-turn-2026-10-10/resume-and-contract-record.json)。派发accepted只表示接收，不表示DSH内容或独立构建已验收；不重复派发、不恢复旧goal。

接续后再次实际查询，seq566为running=true、agentAvailable=true，见[运行状态快照](../../../../.audit/parallel-next-turn-2026-10-10/dsh-resumed-status.json)。确认原会话已恢复工作；DSH独立输出的构建结果仍等待其固定交接后复核。

## 源依据与交付行为

源版本沿仓库当前恢复源码；依据为 `sts2/src/Core/Models/Powers/EnergyNextTurnPower.cs` 的AfterEnergyReset、`Cards/Outmaneuver.cs`、`CardPools/EventCardPool.cs` 与 `Combat/CombatManager.cs` 的SetupPlayerTurn。源Power获能后Remove；回合顺序先reset，再AfterEnergyReset，再BeforeHandDraw。Outmaneuver实际取得池为EventCardPool，Silent仅视觉池。

共享规则 `energyAfterResetOnce = true` 按实际拥有者和注册定义选择Power，在重置后以当前层数获得能量并销毁实例。复用现有EnergyGainBlocked门控，被禁止收益仍消费一次性Power；不修改MaxEnergy，不按具体Power ID白名单编写。原ReclaimAttacksBeforeTurn接到命名明确的BeforeHandDraw过程。卡内容只施加EnergyNextTurnPower：普通2/升级3，cost1，普通Skill结果。自定义mymod命名空间实跑同机制。

未扩展的其他资源、能量收益额外修正hook、跨实例弃牌监听、Sly、整套角色能力不能由此宣称完成。核心、参考源码、net协议、默认输出、身份golden与公开整局夹具均未因本小包改写。

## 实际验证

工作目录为仓库根。独立测试Session seed `20261001`，使用正式FormalSpirePackage模型与显式受控Deck；Profile为局部CombatSlice规则集，不是公开Host正常Run。

| 命令 | 结果与证据 |
| --- | --- |
| `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/owner-next-turn` | 退出0，0错误，3项既有nullable警告；[构建日志](../../../../.audit/parallel-next-turn-2026-10-10/build.log) |
| `dotnet .artifacts/parallel/owner-next-turn/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --next-turn-energy-only` | 退出0，9/9；[机制日志](../../../../.audit/parallel-next-turn-2026-10-10/next-turn.log) |
| 同一DLL `--orchestration-only` | 退出0，24/24；[编排日志](../../../../.audit/parallel-next-turn-2026-10-10/orchestration.log) |

9项涵盖正式卡普通、直接创建升级、真实Armaments升级；支付后未立即获能；下回合重置后的准确数值/实例销毁/再下回合无重复；两次施加叠层；禁止收益仍销毁；与Pyre最大能量组合；mymod卡跨注册Power的完整运行；敌人拥有者不误触发；false/整数/重复规则拒绝。24项覆盖现有编排与牌区判别。实际注册与运行由这些正式模型检查证明，不仅凭solution可构建。

按用户本轮加快推进指令，未重复全量Engine/Slice/net、独立参考/client对拍、正常公开Act1重放与身份迁移。上个批次已有154卡快照的正常第一幕Boss457提交证据，见[前次记录](card-selection-and-ready-content-review-2026-10-10.md)；该证据不覆盖本次新IR或DSH未交接内容。集成时统一构建/迁移身份并验证公开轨迹。并发减少构建互相覆盖和等待，尚无耗时/token的对照测量，不给出节省比例。

文档检查347份Markdown/0问题，git diff --check退出0（既有行尾转换提示）；日志保存于本次audit的doc-check.log与diff-check.log。此生产小包没有未完成实现；剩余独立对拍与公开批次集成按主清单接续，完整T06/T12父项仍开放。
