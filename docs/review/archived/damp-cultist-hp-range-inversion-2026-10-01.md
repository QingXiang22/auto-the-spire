# DampCultist HP 范围与来源写反（base-spire 内容缺陷）

> 2026-10-01 归档说明：普通51–53/Tough52–54已同步内容、来源审计、DSL示例及两处来源测试，完整旧规则检查190/190通过。本轮实现改动仍由执行agent接管审阅，见评审处置。 详见[评审处置与剩余任务](../records/2026-10-01/review-followup-2026-10-01.md)。

| 项目 | 内容 |
| --- | --- |
| 状态与日期 | 2026-10-01；**未修**，仅报告（内容与旧测试属 base-spire/DSL 工作流，本任务不擅自改他人文件） |
| 发现途径 | 为 B-01 切片核对数值时，以 `sts2/` 恢复工程为权威逐项对照现有 base-spire 内容 |
| 完整证据 | [受控切片 7 项数值来源核对](../records/2026-10-01/sts2-slice-value-extraction-2026-10-01.md) |
| 复现方式 | 读 `sts2/src/Core/Models/Monsters/DampCultist.cs:28,30` 与 `mod-the-spire/base-spire/BaseSpire/Enemies/Content/enemies.spire:222` 直接对照 |

## 结论

`enemies.spire:222` 把 DampCultist 的普通与 ToughEnemies 生命范围**写反了**。

| 档位 | 权威值（sts2 恢复源码） | 现有 base-spire 值 | 判定 |
| --- | --- | --- | --- |
| 普通 | **51–53** | 52–54 | ❌ 写反 |
| ToughEnemies | **52–54** | 51–53 | ❌ 写反 |

权威依据：`sts2/src/Core/Models/Monsters/DampCultist.cs:28,30`

```
public override int MinInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 52, 51);
public override int MaxInitialHp => AscensionHelper.GetValueIfAscension(AscensionLevel.ToughEnemies, 54, 53);
```

`AscensionHelper.GetValueIfAscension(level, ascensionValue, fallbackValue)` 在**未达到**该 ascension 等级时取第三个参数（fallback），因此普通档是 `51`/`53`，ToughEnemies 档才是 `52`/`54`。（`sts2/src/Core/Entities/Ascension/AscensionHelper.cs:22-29`）

现有内容：`enemies.spire:222` `hp = variants(default=range(52, 54), tough=range(51, 53))` —— 两个档位对调。

## 为什么判断是抄写反转而不是有意设计

同一遭遇的另一个敌人没有写反：`CalcifiedCultist` 的 `enemies.spire:231` 是 `default=range(38, 41), tough=range(39, 42)`，与 `sts2/src/Core/Models/Monsters/CalcifiedCultist.cs:28,30` 一致（Tough 档更高）。DampCultist 是孤立的一处反转，且与"ToughEnemies 应更肉"的常识相反。

## 需要一并修改的位置（都已固化错误区间）

1. `mod-the-spire/base-spire/BaseSpire/Enemies/Content/enemies.spire:222` —— 数值本体，应改为 `default=range(51, 53), tough=range(52, 54)`。
2. `core-the-spire/Core.TheSpire.Tests/CombatSliceTests.cs:722` —— `Assert.True(initial.Enemies[1].Hp is >= 52 and <= 54);`（`BaseCultistsEncounterRunsBothSourceEnemySequences`）锁定了错误区间，改内容后必然失败，需要同步改为 `51..53`。
3. `docs/design/sts2-core-source-audit.md:149` —— 文档写了"Damp 普通 HP 52–54 / ToughEnemies 51–53"，同一错误。
4. `BaseDampCultistNormalValuesMatchSourceRules`（`CombatSliceTests.cs:700`）的测试名声称"match source rules"，但**没有**断言 HP，所以它不会失败；建议在修值时补上 HP 断言，否则这个缺陷仍会被漏过。

## 对当前任务的影响

- 本批新增的 `base-spire:combat-slice-v1`（`BaseSpire/Composition/SliceManifest.cs`）**采用权威值** 51–53 / 52–54，并在注释里注明与旧内容的冲突，因此新引擎侧不会继承该错误。
- 旧后端行为未改动；`Core.TheSpire.Tests` 的首个失败仍是无关的 Bludgeon 效果形状问题。

## 建议处理方式

由 base-spire/DSL 工作流的负责人一次性改上述 4 处并重跑 `Core.TheSpire.Tests`；修改会改变敌人 HP 区间，属于行为变化，需要在该工作流的交接里记录。本报告不包含"是否点击式修复"的决定权。
