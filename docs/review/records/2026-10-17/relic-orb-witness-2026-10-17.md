# 遗物球类消费者行为见证

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批为续批小包，只补五件球类遗物的运行时行为证据，不新增内容声明。归属：DSH（2026-10-17用户指令从Codex `/root`接手本线，见主清单[当前交付](../../tofix/README.md#当前交付)）。

## 本批要解决的问题

`relics.json`已声明的69件遗物中，`DataDisk`、`CrackedCore`、`SymbioticVirus`、`RunicCapacitor`、`InfusedCore`五件只有声明与静态对账，没有行为见证。CI门槛 `tools/ci/check_relic_coverage.py`要求每件可执行定义至少一条实际通过的场景，因此这五件是[覆盖报告](../../../../tools/README.md)里唯一的失败项，报出的原文为 `unverified executable relics: DataDisk, CrackedCore, SymbioticVirus, RunicCapacitor, InfusedCore`。本批消除该失败项，并按要求实际观察球的被动／激发伤害与存值、首回合限制和下一场战斗重建，而不是只检查Power或球实例存在。

## 源依据

行为取自`sts2/src/Core/Models/Relics/`对应源类：DataDisk（Common，`AfterRoomEntered`仅在战斗房施加Focus 1）、CrackedCore（Starter，`BeforeSideTurnStart`在`TurnNumber <= 1`时生成1个Lightning）、SymbioticVirus（Uncommon，`AfterSideTurnStart`同首回合限制生成1个Dark）、RunicCapacitor（Shop，`AfterSideTurnStart`同限制`OrbCmd.AddSlots(Owner, 3)`）、InfusedCore（Starter，`AfterSideTurnStart`同限制生成3个Lightning，且`ModifyOrbValue`只对Lightning +1）。

## 改动边界

只改`mod-the-spire/base-spire/BaseSpire.Slice.Tests/RelicContentChecks.cs`：新增`RunOrbs()`（第135行起，由`Run()`在`RunPassives()`前调用）与三个局部断言辅助`SetMonsterHp`／`MonsterHpTotal`／`MonsterMaxHpTotal`。生产内容、领域、编译和公开协议均未改；五件遗物的`relics.json`声明沿用上一批，因此[FOUNDATION记录](relic-foundation-2026-10-17.md)里的rules／build身份继续有效。

## 实际场景与断言

- CrackedCore：开局获得后进入战斗，队列只有一个Lightning、`StoredValue`为0，无基础槽角色得到源回退的1个槽；把怪物生命设为500后连续两次结束回合，各减少3点真实被动伤害，第二回合不再新增球；再次进入战斗重建恰好一个Lightning实例。
- DataDisk：与CrackedCore同场，进入战斗房得到`base-spire:focus-power` 1层，Lightning被动实际为4点且第二回合仍是同一1层（不按回合重复施加）。
- SymbioticVirus：Dark存值从6起，非激发回合只增长为12、18，不产生伤害；下一场战斗重建新实例并从6重新开始。
- RunicCapacitor：与InfusedCore同场，得到3个真实槽位，三个Lightning同时留在队列而不被激发溢出，一回合三次修正后被动共12点；第二回合不重复加槽，下一场战斗重新触发并获得三个新球。
- InfusedCore：无基础槽角色只留1个槽，三次生成中的两次溢出为激发且表现为真实伤害，存活球被动为4点（源值3加自身+1），下一场战斗按源生成数重建1个球。

## 命令与结果

```powershell
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
```

- 遗物内容组：**68项全部通过**（含新增5项球遗物场景），末尾门槛行`Relics CI: every executable definition has a passed behavior witness; full source denominator retained`通过。
- 覆盖报告：`relic runtime coverage: 69/69 executable; source=297, unimplemented=227, complete=False`，校验器exit 0。报告写在`artifacts/`本地目录，不提交。

## 未验证与下一步

本批只取得受控内容场景的本地证据：没有新的独立双侧、正常整局获得／使用或公开Host轨迹，也没有执行完整Engine／Slice／net／严格回归（按用户2026-10-17分工留给GitHub Actions）。`--require-complete`仍不成立，因为297件源遗物中还有227件是`unreviewed`，需要逐件裁定单人适用性并对适用者补声明与行为；一件废弃项已有排除依据，五件多人专用项留原合同。本批不关闭ENV-T09-PR02-CONTENT。下一已就绪成果为下一组源遗物的资格裁定与内容实现，仍沿用“一组一包、包内包含声明＋行为见证＋覆盖门槛”的方式。
