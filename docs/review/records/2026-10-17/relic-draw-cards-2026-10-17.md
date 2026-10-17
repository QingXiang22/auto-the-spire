# 遗物共享抽牌效果与 Pendulum

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批新增抽牌效果种类（复用共享抽牌过程）并纳入 Pendulum。归属：DSH。

## 新能力

`RelicEffectKinds.DrawCards`（`draw-cards`）通过 `CombatPackageComposer` 里注入的委托调用**同一个**抽牌过程（`CardCompilation.Draw` 的 `Draw` 过程），因此自动继承抽空重洗、`DrawPrevented` 阻止、抽牌事件与 `AfterDraw` 能力触发，不另写抽牌实现。该效果只在 `after-player-turn-start` 触发点开放。

## 消费者（源依据）

`Pendulum`（Common，`Pendulum.cs:14,47-84`）：`AfterPlayerTurnStart` 把 `TurnsSeen = (TurnsSeen + 1) % 3`，为 0 时抽 1 张；该计数是 `SavedProperty`，跨战斗保留，`AfterCombatEnd` 只重置显示状态。

声明：`after-player-turn-start` 的 `add-counter` 1（模 3）＋ `draw-cards` 1（`CounterEquals=0`），`ShowCounter=true`。

## 命令与结果

```powershell
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
```

- 遗物内容组 **80/80 通过**：场景断言第 1、2 回合各抽正常的 5 张，第 3 回合抽到 6 张；另一局只走 1 个回合后重开战斗，携带的计数在新战斗的第一个回合就完成周期并抽到 6 张（对应源里计数跨战斗保留）。
- 覆盖报告：**81/81 可执行**、source=297、**unimplemented 由 216 降到 215**，校验器 exit 0；构建描述 `unsupported` 由 335 降到 334。
- 身份迁移：dev `8f6d5a74a9f0e49d`／`ac56bbdcf1117d15`、dev-map `3b104072e22f7fb1`／`3d9836c424faefaa`（上一版 `2d6cc019dfe1a98d`／`f66c39dcd9474851`、`ade78c1114859bbf`／`30e79a8acac26095` 记在注释里）。
- 公开语义 10/10、遗物生命周期与 Run 14/14、文档与差异检查通过。

## 未验证与下一步

只有受控内容场景证据：没有独立双侧、正常整局获得或公开 Host 轨迹，完整回归仍交 GitHub Actions。抽牌效果暂不区分“抽到手上限”之类细分（沿用共享过程既有语义）。下一批候选：`ModifyDamageAdditive` 组（StrikeDummy／FakeStrikeDummy 按 Strike 标签、MiniatureCannon 按升级、MysticLighter 按附魔），需要把伤害来源牌接进加性阶段；或先用 `draw-cards` 带出需要抽牌的其它触发点（`AfterDamageReceived`／`AfterHandEmptied`／`AfterDeath`）。
