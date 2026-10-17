# 遗物格挡数量条件与 Orichalcum

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批新增“当前格挡数量”这一声明条件，并纳入 Orichalcum。归属：DSH。

## 新能力

`SliceRelicEffect` 增加 `BlockAtLeast`／`BlockAtMost`（默认 -1 ＝不作为条件），在 `RelicCompilation.Condition` 里与现有的回合、房型、计数、手牌条件同层判断；脱战路径没有格挡，读取时按 0 处理。配合已有的 `before-side-turn-end` 与 `gain-block`，就能表达“回合末没有格挡才补格挡”这类来源。

## 消费者（源依据）

`Orichalcum`（Uncommon，`Orichalcum.cs:44-66`）：`BeforeSideTurnEndVeryEarly` 时若 `Block == 0` 置标记，`BeforeSideTurnEnd` 时按标记获得 6 点格挡；`BeforeSideTurnStart` 清标记。

声明：`before-side-turn-end` → `gain-block` 6、`BlockAtMost=0`。

**顺序假设**：来源专门用“very early”是因为 `PlatingPower` 会在回合末先给格挡。本引擎的回合末遗物监听（`TurnCompilation.PlayerTurnEndBody` 里的 `BeforeSideTurnEnd` 派发）在能力回合末链之前执行，因此这里读到的格挡是加层前的值，与来源意图一致；场景用“无格挡→6”和“已有 5 格挡→不补”两例固定该语义。

## 命令与结果

```powershell
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
```

- 遗物内容组 **83/83 通过**：无格挡回合末补 6 点（同场带 SturdyClamp 保留上限 10 以便观察）；预先有 5 点格挡时不补；不带遗物时为 0。
- 覆盖报告：**84/84 可执行**、source=297、**unimplemented 由 213 降到 212**，校验器 exit 0；构建描述 `unsupported` 330。
- 身份迁移：dev `97704742b08ae2ef`／`082df4fc513d85cf`、dev-map `ac0764f3c9e63ad3`／`023957de61c94c87`（上一版 `58b3cef636e06488`／`250a1283c1556641`、`2e69c1a8e092394a`／`257f9b76174e59c7` 记在注释里）。
- 公开语义 10/10、遗物生命周期与 Run 14/14、文档与差异检查通过。

## 未验证与下一步

只有受控内容场景证据：没有独立双侧、正常整局获得或公开 Host 轨迹，完整回归仍交 GitHub Actions。格挡条件目前只按数值比较，不含“格挡是否被阻止清零”等修正参与者。下一批仍建议先做弃牌钩子的最小复现（记录见[弃牌钩子缺口](../2026-10-17/relic-discard-flush-gap-2026-10-17.md)），再回到 `ModifyDamageAdditive` 组。
