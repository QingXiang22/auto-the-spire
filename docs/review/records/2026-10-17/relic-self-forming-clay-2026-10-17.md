# 遗物受击触发的第二件消费者：SelfFormingClay

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批复用上一批新增的 `after-damage-received` 触发点，纳入 SelfFormingClay。归属：DSH。

## 消费者（源依据）

`SelfFormingClay`（Uncommon，Ironclad 池，`SelfFormingClay.cs:15,26-32`）：玩家真正吃到伤害时施加 `SelfFormingClayPower`，层数为 3；该能力在格挡清除（下个回合开始）时按自身层数给格挡并自毁。

声明：`after-damage-received` → `apply-power` `base-spire:self-forming-clay` 3；该触发点同时放开 `apply-power`。

## 与来源的差异已收敛（同轮修复）

上一版能力声明是 `gainBlock(3, target=self)`（固定 3 点），与来源“格挡 = 层数”不符：同一次敌人回合内吃到两次未格挡伤害时，源给 6 点、旧实现给 3 点。

本轮的修复分三处：`SpirePowerSource.ReadSequenceEffect` 的 `gainBlock` 分支接受 `perPowerStack=自己` 并产出 `gain-block-per-stack:N`；`HasReusableBehavior` 的 `AfterPlayerTurnStart` 白名单加入该前缀；`PowerCompilation` 的触发效果链新增 `gain-block-per-stack:N` → `N × 层数` 的格挡。能力声明改为 `gainBlock(1, perPowerStack=SelfFormingClayPower)`。

修复后 `PowerCount` 由 55 变 56（该能力从“未支持内容”变为正式加载），受控场景里第一次掉血回合恰好两击，落下 6 层能力即结算 **6 点格挡**；场景改为断言“格挡 = 3 × 本场掉血事件次数（`HpLossCount`）”，能区分“固定 3”与“按层数”。

## 命令与结果

```powershell
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
```

- 遗物内容组 **82/82 通过**：场景跑到第一次掉血后的下个回合开始，断言格挡 = 3 × 本场掉血事件次数（`HpLossCount`，该受控遭遇为 6 点）且能力已被消耗，并断言战斗继续造成伤害。
- 覆盖报告：**83/83 可执行**、source=297、**unimplemented 由 214 降到 213**，校验器 exit 0；构建描述 `unsupported` 由 333 降到 332，`PowerCount` 由 55 升到 56。
- 身份迁移：dev `58b3cef636e06488`／`250a1283c1556641`、dev-map `2e69c1a8e092394a`／`257f9b76174e59c7`（上一版 `c3e80e123a592ecd`／`e720657f9744aff7`、`a2e76db7002d51c1`／`c1bfa1c760a80690` 记在注释里）。
- 公开语义 10/10、遗物生命周期与 Run 14/14、文档与差异检查通过。

## 未验证与下一步

只有受控内容场景证据：没有独立双侧、正常整局获得或公开 Host 轨迹，完整回归仍交 GitHub Actions。下一已就绪成果是 `ModifyDamageAdditive` 组（StrikeDummy／FakeStrikeDummy 按 Strike 标签、MiniatureCannon 按升级、MysticLighter 按附魔），需要把伤害来源牌接进加性阶段。
