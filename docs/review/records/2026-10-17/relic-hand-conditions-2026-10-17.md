# 遗物手牌数量条件与两件回合结束消费者

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批新增“手牌数量条件”这一通用声明条件，并用它纳入两件在回合结束时触发的遗物。归属：DSH。

## 新能力

`SliceRelicEffect` 增加两个条件字段：`HandAtLeast`／`HandAtMost`（默认 -1 ＝不作为条件），在 `RelicCompilation.Condition` 里用共享的 `Hand` 集合数量判断。它们与既有的回合、房型、计数条件同属一层，不新建调度器；来源是 `PileType.Hand` 的 `Count != 0`（如 CloakClasp）与 `IsEmpty`（如 ScreamingFlagon）。同时只为 `before-side-turn-end` 开放 `damage-all-enemies` 与 `gain-block` 两种效果。

## 两件消费者（源依据）

| 遗物 | 稀有度 | 源行为 | 本批声明 |
| --- | --- | --- | --- |
| CloakClasp | Rare | `CloakClasp.cs:16-32`：回合结束前，若手牌非空，按“手牌数 × 1”获得 Unpowered 格挡 | `before-side-turn-end` → `gain-block` 1、`AmountSource=hand`、`HandAtLeast=1` |
| ScreamingFlagon | Shop | `ScreamingFlagon.cs:15-25`：回合结束前，若手牌为空，对全体敌人造成 20 点 Unpowered 伤害 | `before-side-turn-end` → `damage-all-enemies` 20、`HandAtMost=0` |

## 检查写法上的两个事实

1. **格挡在回合末会被清掉**，直接读 `Player.Block` 拿不到这一刻的获得量（首次断言两侧都是 0）。改为同场带上 `SturdyClamp`（保留格挡上限 10）把这一格挡留到可读时点；再用 Dexterity 5 对照，确认获得的是 Unpowered 格挡（数量不随敏捷变化）。
2. 参照局与遗物局都**直接把手牌挪进弃牌堆**（受控输入，不走打牌）来构造“空手”，避免打出的 Defend 自带格挡干扰观察。

## 命令与结果

```powershell
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
```

- 遗物内容组 **79/79 通过**：CloakClasp 断言“手牌 5 张 → 保留 5 点格挡、空手 → 0 点”；ScreamingFlagon 断言“有手牌不掉血、空手按 20 × 敌人数掉血”。
- 覆盖报告：**80/80 可执行**、source=297、**unimplemented 由 218 降到 216**，校验器 exit 0；构建描述 `unsupported` 由 337 降到 335。
- 身份迁移：dev `2d6cc019dfe1a98d`／`f66c39dcd9474851`、dev-map `ade78c1114859bbf`／`30e79a8acac26095`（上一版 `775b4fee84aa7800`／`b103e81fede9d804`、`443d0d6515615aa3`／`4e39836a700af26d` 记在注释里）。
- 公开语义 10/10、遗物生命周期与 Run 14/14、文档检查与差异检查通过。

## 未验证与下一步

只有受控内容场景证据：没有独立双侧、正常整局获得或公开 Host 轨迹，完整回归仍交 GitHub Actions。手牌条件目前只支持数量，不区分牌型；`before-side-turn-end` 的 `Unpowered` 格挡复用遗物非加成路径（与来源 `ValueProp.Unpowered` 一致），但不覆盖其它来源。下一批候选：`ModifyDamageAdditive` 组（StrikeDummy／FakeStrikeDummy 按 Strike 标签、MiniatureCannon 按升级、MysticLighter 按附魔），需要带牌来源过滤的伤害加性修正。
