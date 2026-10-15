# 逐效果表示与职责边界交付 — 2026-10-09

正式卡牌现在保留每条效果的参数和执行结构，组合已有机制可以只修改内容及行为案例；效果编译与整张卡的动作收尾有明确接缝。此次交付由 Codex 亲自完成，没有派发 DeepSeek 或其他 agent。

归属：ENV-T06 中间表示与职责边界子包 `ENV-T06-effect-ir-boundaries-20261009`。本报告保存当日证据；有效状态、认领及父项完成条件只看[主清单](../../tofix/README.md#env-t06)。方法入口为[规格](../../../design/mod-the-spire/effect-ir/spec.md)、[设计](../../../design/mod-the-spire/effect-ir/plan.md)及[实施清单](../../../design/mod-the-spire/effect-ir/tasks.md)。

## 接手入口

本包没有待实现生产小项。接手先从仓库根目录运行 `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --effects-only`，复用已有程序及编译器，不重新写共享卡级参数分支。新语义按设计中的职责表定位；已有组合只改 `ContentSource/*.spire` 和实际行为检查。

完整门槛补证依赖[ENV-T05](../../tofix/README.md#env-t05)完成奖励池对账；之后重跑默认 Slice 和 net，核对地图/奖励实际公开路径。原版双侧组合比较尚未执行，接续需另按主清单授权认领；本报告不派发任务，也不关闭整个 T06 或 ENV-T07。当前文本规则库复用、全量卡牌与地图性能不在此次交付范围。

## 实现及接口变化

- [领域程序](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/CardEffectProgram.cs)保存不可变的逐效果类型、目标、数值来源、能力引用和源码位置；条件及重复包含有序子程序。重复的单位是整个块，零次为空。
- [领域前端](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/SourceEffectPrograms.cs)复用已有 AST 及叶读取，保留独立参数，递归检查变量并链接能力。缺失引用进入带来源的 Unsupported，不注册空效果替代。
- [效果编译器](../../../../mod-the-spire/base-spire/BaseSpire/Slice/Effects/CardEffectCompiler.cs)仅生成冻结 Engine IR。它没有 Session、胜负判定或房间提交接口；动态来源在对应效果位置读取同一个权威状态。
- [组包接缝](../../../../mod-the-spire/base-spire/BaseSpire/Slice/CombatSlicePackage.cs)提供字段、查询和共享领域过程。出牌外层选择一次目标；随机命中使用独立槽位，每次执行抽取。X 读外层捕获的付款局部值，后续能量变化不改付款值。
- `AuthorCard.NormalProgram` / `UpgradedProgram` 和 `AuthorHooks.Programs` 是正式源的执行依据；显示数值及旧 Steps/Conditional 保留兼容摘要，不能覆盖显式参数。升级与各 hook 独立；进入下一战斗按复制实例的升级标记选择 hook。
- `.author` 诊断路径仍用原步骤编译。正式程序的 `LegacyCardEffect` 明确桥接现有选择/移动/升级机制；没有批量迁移敌人、Power、药水或全部卡牌。hook 不允许暂停选择，攻击玩家的原语仍不冒充敌人伤害。

完整动作仍遵循付款/进入 InPlay→before hook→效果→after hook及反应→结果区→胜负→永久提交。既有最后击杀、Feed、变量长期来源和公开纯读取回归继续执行。保留工作区已写入的奖励实现，没有重置、提交或改动冻结 core。

## 实际验证

工作目录均为 `F:\Scylier-Project\active\auto-the-spire`。完整命令及原始退出码见[门槛清单](../../../../.audit/effect-ir-boundaries-2026-10-09/gates.json)，日志位于[本包证据目录](../../../../.audit/effect-ir-boundaries-2026-10-09/)。

| 检查 | 实际结果 | 证据及范围 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln --no-restore` | 退出0，0错误 | `build-gate.log`；工作区 solution 构建 |
| Engine 默认检查 | 退出0，95/95 | `engine-gate.log`；冻结通用内核 |
| Slice `--effects-only` | 退出0，15组 | `effects-gate.log`；普通/升级、3+7/3+3/10对照、显示槽独立、独立能力、条件/重复、变量/动态值、X、目标绑定、hook与末杀提交 |
| Slice `--combat-only --report .audit/effect-ir-boundaries-2026-10-09/content-report` | 退出0，354/354 | `slice-combat-gate.log`；只排除地图组，包含默认 dev 身份与公开语义/连续攻击检查 |
| Slice 默认完整检查 | 退出 -532462766，首错奖励池对账 | `slice-full-gate.log`；没有记为完整通过 |
| net 默认检查 | 退出1，410通过/5失败/415执行 | `net-gate.log`；5项均为 G-01～G-05 dev-map，首错同一奖励池缺口 |
| `python tools/doc-check/check_docs.py` 与 `git diff --check` | 均退出0；314篇文档，0问题 | `doc-check.log` / `diff-check.log` / `doc-gates.json`；链接及格式门槛 |

正式内容真实加载保持：Ironclad 卡牌声明94、解析94、降级67、未支持27；状态牌声明2、解析2、降级1、未支持1。正式 dev 工厂卡68、Power29、敌人35，起始牌组10、弱遭遇4、普通遭遇8、15房间。这些是既有受支持子集，不能代表五角色或完整原版。[行为自报](../../../../.audit/effect-ir-boundaries-2026-10-09/content-report/slice-behavior-report.json)含53条既有行为已验证内容ID；新增探针不加入原版覆盖数字。

受控新用例使用 seed `20261001UL`、现有 `SourceWeightedPickBackend` 与正式包模型；修改仅用于建立能区分语义的测试前提。正常运行回归另实际跑 `ENV-IRONCLAD-00`，到第9房 Defeat；不把受控清场作为正常胜利证据。原始依据为现有 `.spire` 正式内容、`sts2` 规则和冻结 `engine/ir@0.3`，不改源游戏或 RNG Profile。

IR 形状改变导致 dev ruleset 从 `39246f7e8412b142` 变为 `f87614af13d18e0c`，公开构建从 `f291cc66cf6a9050` 变为 `dc9325c6b9b5cca9`。更新了 Slice 的 dev golden，并将默认身份检查从地图工厂检查拆出，使奖励阻断不能遮住默认基线验证。元数据闭合/身份只读检查通过，公开 semantics 摘要仍为 `9d92f078cdb3ff5f`；消费者从工厂读取身份，不硬编码旧构建。

## 修改前后证据与未验证范围

修改前五个文件及 SHA256 保存在 `before/` 和 [before.json](../../../../.audit/effect-ir-boundaries-2026-10-09/before.json)。隔离项目只替换这些修改前源文件，其他工作区源和内容不变；没有回滚活动目录。同一新增用例在旧实现得到“预期10、实际6”，见 [semantic-before.log](../../../../.audit/effect-ir-boundaries-2026-10-09/semantic-before.log)及退出码记录。新实现同一用例得到10，3+3和单次10对照分别为6和10。

完整 Slice 修改前隔离快照也在地图身份检查的奖励工厂对账处失败，`slice-baseline.log` 保存相同25项缺卡清单（该次 native 退出码未单独保存）。复制 net 运行目录并替换为修改前 BaseSpire DLL 后，仍为410通过/5失败，退出1，见 `net-baseline.log` 和 `baseline-net-exit.json`。因此这些现有奖励阻断不归因于本包；没有放宽池对账或伪造未支持牌。

受阻最小入口：默认 Slice 或 net 门槛在 `BaseSpireScope.DevMap` 构建时进入 `RewardWiring.BuildPool`，源单机清单仍有25项未进入内容池，第一项为 `aggression`。依赖是 ENV-T05 奖励/内容池完整性；受影响仅为地图/奖励相关完整门槛，本包的受控语义、默认 dev 和其余公开检查可以继续运行。

本次未执行原版双侧新增组合对拍、完整单人候选套件、Release 性能或 AI 训练；没有给出地图性能修复或全量卡牌正确性结论。规格 SC-01～04 有上述局部证据；SC-05 的完整门槛仍待 ENV-T05 解除后补证。

## 维护与接手自查

任务认领/状态/排期只写主清单；接口及修改入口写规格/设计和 DSL 作者说明；原始命令、失败与快照保留 `.audit`。本记录先交 tofix，负责人将开放项并入 T05/T06 后转 records，转移不表示父项验收。

接手自查已核对目标、复用成果、第一步、原完成条件、阻断及维护位置均能从本文或直接链接定位；没有宣称独立读者审阅。
