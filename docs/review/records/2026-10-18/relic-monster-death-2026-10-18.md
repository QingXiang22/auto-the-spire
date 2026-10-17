# 遗物敌人死亡触发与 GremlinHorn

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。当前状态：进行中；本轮目标 = 为“敌人死亡”这一来源触发点接入遗物派生，并纳入 GremlinHorn。

## 变更

1. **触发点 `after-monster-died`**（源 `RelicModel.AfterDeath`，敌方死亡分支）。实现沿用本项目已验证的“先建过程符号、抽牌绑定就绪后补过程体”写法：`DamageCompilation.MonsterDamageBody` 增加可选回调，在 `Hp == 0` 分支里发出 `MonsterDied` 事件、移出存活集之后调用 `RelicAfterMonsterDied`；composer 在 `drawBindings` 就绪后设置过程体。
2. **GremlinHorn**（Uncommon，`GremlinHorn.cs:24-32`：任意敌方单位死亡时获得 1 能量并抽 1 张）：声明 `after-monster-died` 的 `gain-energy` 1 与 `draw-cards` 1；该触发点只开放这两种效果。

## 验证（本地受控）

```powershell
dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-restore
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --public-semantics-only
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-lifecycle-only
```

- 构建 0 错误；遗物内容组 **85/85 通过**；场景断言“击杀一个 1 血敌人后，敌方数量减一、能量回到出牌前（-1 费 +1 返还）、手牌回到 5 张（打出 1 张后抽 1 张）”。
- 覆盖报告：**86/86 可执行**、source=297、**unimplemented 211 → 210**，校验器 exit 0；构建描述 `unsupported` 328。
- 公开语义 10/10、遗物生命周期与 Run 14/14。
- 身份迁移：dev `2bbf020a340fc02e`／`ec5c1ba232981b77`、dev-map `df81101e617014db`／`14b7118cfa1af873`（上一版 `d38b36c7be4c7782`／`02ba7ba8bc64ff3a`、`cd59bdfde4209734`／`ae0107d8ff66a60c` 记在注释里）。

## 未验证与风险

- 只有受控内容场景证据：无独立双侧、正常整局获得或公开 Host 轨迹；完整 Engine／Slice／net／严格回归仍交 GitHub Actions。
- `after-monster-died` 只在“伤害致死”分支派发；`CreatureCmd` 的其它移除路径（若未来实现复活／重附／逃逸）需要单独核对，当前不宣称覆盖。
- 本触发点只开放能量与抽牌；以后若要在此处做“随机目标伤害”（`Tingsha`／`ForgottenSoul` 一类），需要新增随机单体伤害效果种类。

## 下一步

`ModifyDamageAdditive` 组（StrikeDummy／FakeStrikeDummy 按 Strike 标签、MiniatureCannon 按升级、MysticLighter 按附魔）：接缝在 `CardEffectCompiler` 的 `CardEffectOperation.Damage` 调用点，需要新增“带牌来源过滤的伤害加性修正”声明条件。
