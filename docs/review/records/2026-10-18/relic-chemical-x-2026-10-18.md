# Chemical X 与效果 X 修正交付 — 2026-10-18

本包让纯DSL的ChemicalX实际修正Whirlwind／Cascade效果，同时保留独立的能量支付与原始X捕获；不代表正常商店来源、星星X或原版整局已经恢复。

归属：**CORE-AUTHOR-05／DSL-RELIC-CONTENT-03**，Codex `/root`；当前状态与接续认领见[主清单](../../tofix/README.md#当前交付)。基于main／`3a160c6`（前批PR #21／#22已合并），分支`codex/relic-chemical-x-20261018`。原297件遗物来源、完整ENV/W、原严格suite及暂停项保留。

## 接手后先做什么

1. 核对主清单与工作区；本批已交付X查询和ChemicalX，无需重新实现。签名、前置及源差别见[生命周期契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#效果-x-修正dsl-relic-content-03)，通用调用入口见[操作参考](../../../development/base-spire/reference/operations.md)。
2. 在仓库根目录执行`dotnet run --project tools/base-spire-dev -- check --relic-content-only`，预期包括本批10项X判别并返回0；只有完整`check --report`产物可用于覆盖门槛。
3. 后续仍沿全遗物主线，另行登记拾取永久升级机制包，先用定位助手核对Whetstone／WarPaint的源、选取随机流及`UpgradeCard`完整操作，再迁移真实消费者。当前X监听不承担牌组升级或随机来源。原父项完成条件仍见[纯DSL迁移规格](../../../design/mod-the-spire/engine-dsl-migration/spec.md)与主清单ENV范围，由负责人验收。

## 已交付与接口

- `cards/rules/operations.spire`新增pure `ResolveEnergyXValue(owner,card)`，以`Card.PaidX`为初值征询`ModifyXValue(value,owner,card)`。非X查询返回0、不派发修正；有效能量X的生产效果全部走此入口。
- `relics/rules/x-value.spire`声明`XValueRelic`实例监听模板；ChemicalX内容叶只配置definition与amount=2。按Holder、IsMelted、实际牌拥有者及库存顺序参与，移除后立即失效，无额外私有状态。
- Whirlwind与Cascade在各次效果开始取一次修正X。Cascade升级随后+1；子牌自动打出使用子牌自己的当前能量捕获，父牌次数不会随子效果获得能量而增长。每次重复重新查询有效监听，保留原捕获与一次支付。
- 包版本／公开构建身份为`0.6.0-dsl.34`，继承`engine/ir@0.6`，随机配置`vanilla-uniform@1`，scope为`base-spire-dsl-relic-draw-dev-v1`。公开内容增加真实ChemicalX身份；无需wire/schema变更或Host玩法补偿。
- 修改范围为base的cards／combat修正事件／relics／manifest与公开元数据、tools包外判别、net真实stdio检查及契约／手册。本批未改core、参考玩法、源池成员或暂停域。

## 来源与验证

源基线`local-sts2-source-2026-10-18`。定位助手对ChemicalX、Whirlwind与Cascade的原始字节SHA256均返回matches；ChemicalX为`86d1b7f27cbc9e6de97f1f7f14f616bb6b8dcf3022e993301d6648ee353561c4`。同时局部核对CardModel.ResolveEnergyXValue／SpendEnergy、CardEnergyCost.GetAmountToSpend、CardCmd.AutoPlay；六份文件的完整hash保存于`.artifacts/relic-chemical-x/source-hashes.json`。源码核对不是双侧执行。

固定包行为seed为数值`20261018`，公开stdio为字符串`BASE-DSL-STDIO-00`；受控场景和额外DSL探针只用于判别，不冒充正常Run。使用根global.json的.NET SDK 9.0.306，Windows／PowerShell、Python 3.12。

包摘要为`9d84a555221e647f374be381bf8e2ec3f1f71bd268570684ec67b835e019cb5e`，ruleset为`041c7c78c1856ea6`，见`.artifacts/relic-chemical-x/describe.json`。

以下命令工作目录均为仓库根目录；日志在`.artifacts/relic-chemical-x/`。

| 实际命令 | 返回码／结果 | 证据与证明范围 |
| --- | --- | --- |
| `dotnet run --project tools/base-spire-dev -- check --relic-content-only` | 0；43项 | 包含本批10项X判别；定向入口不生成全包覆盖 |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/relic-chemical-x/behavior` | 0；361项 | `package-check.log`及`behavior/package-behavior.json`，实际DSL执行 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/relic-chemical-x/behavior/package-behavior.json` | 0；39件见证／297来源／257未实现／1项源排除 | 与当前包摘要核对，`independent_verified=false` |
| `python -X utf8 tools/ci/check_card_coverage.py .artifacts/relic-chemical-x/behavior/package-behavior.json --require-ironclad-complete` | 0；85/85单人铁甲，2项多人排除 | 全卡578来源保留；全部已声明87定义有行为 |
| `dotnet build AutoTheSpire.sln --nologo` | 0；0警告／0错误 | `build.log`，不是原版玩法比较 |

| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only` | 0；33通过／0失败 | `net-check.log`，含真实stdio：3能量5段共25伤害，随后0能量2段共10；公开PaidX仍为3／0 |
| `dotnet run --no-build --project tools/base-spire-dev -- describe` | 0 | `describe.json`，包身份与真实定义数量 |

本地未重复完整Engine、完整net及迁移组，广覆盖由本分支Actions执行；独立原版双侧未运行。文档检查扫描781份Markdown、0问题；`git diff --check`返回0。

## 失败修正与未验证

首次加载把源内部非X误调用的异常语义写成pure过程中的`fail`，被引擎以PurityViolation正确拒绝。本包没有放宽pure或修改core，改为非X返回0、不派发修正，并在契约中记录该错误调用语义与源的差别；正常X调用语义不变。随后同步根DSL／manifest／公开构建版本，修复Manifest identity不一致。预览夹具曾在空手牌时查询`first(Hand)`，改为真实参数候选后通过；生产流程不增加空引用补偿。

星星X、正常商店／遗物池来源、完整获得修正、独立原版双侧、Neow／地图／正常Run与完整胜负均未验收。其余遗物仍按源目录显式未实现；不以本批单件或局部覆盖关闭全遗物及父项。原严格suite仍属于未迁移范围，本批不重写或缩减。

## 维护位置

当前状态、负责范围及下一成果只维护主清单；操作与边界回写上述契约和人类手册。交付过程与实际结果保存本记录，长日志留本地artifacts。接手自查可沿正文直接定位目标、已交付、第一步、原完成条件、未验证与唯一状态位置；未运行独立agent评审。
