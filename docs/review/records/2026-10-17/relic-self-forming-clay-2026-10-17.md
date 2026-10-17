# 遗物受击触发的第二件消费者：SelfFormingClay

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批复用上一批新增的 `after-damage-received` 触发点，纳入 SelfFormingClay。归属：DSH。

## 消费者（源依据）

`SelfFormingClay`（Uncommon，Ironclad 池，`SelfFormingClay.cs:15,26-32`）：玩家真正吃到伤害时施加 `SelfFormingClayPower`，层数为 3；该能力在格挡清除（下个回合开始）时按自身层数给格挡并自毁。

声明：`after-damage-received` → `apply-power` `base-spire:self-forming-clay` 3；该触发点同时放开 `apply-power`。

## 与来源的已知差异（明确保留）

现有能力声明 `SelfFormingClayPower`（`shared-powers.spire`）是 `trigger = afterPlayerTurnStart; effect = sequence(gainBlock(3, target=self), removePower(...))`：**固定给 3 点格挡**，不是源里的“格挡 = 层数”。因此同一次敌人回合内吃到两次未格挡伤害时，源给 6 点格挡，当前实现仍给 3 点。检查场景按当前语义断言“之后每次命中同样给 3 点”，并在记录里具名保留该差异。

尝试把它改成 `gainBlock(1, perPowerStack=SelfFormingClayPower)`（本批实际试过一次）会让该能力变成未支持内容，构建随即报 `Relic references unloaded Power: base-spire:self-forming-clay`，因此本批回退到原文；把该能力改成“按层数结算”属于 `SpirePowerSource` 的 `effect = gainBlock(N, perPowerStack=自己)` 解析缺口，留作下一步，不在本批放宽。

## 命令与结果

```powershell
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
```

- 遗物内容组 **82/82 通过**：场景跑到第一次掉血后的下个回合开始，断言格挡为 3 且能力已被消耗；再次掉血后仍是 3（对应上面具名差异），并断言战斗继续造成伤害。
- 覆盖报告：**83/83 可执行**、source=297、**unimplemented 由 214 降到 213**，校验器 exit 0；构建描述 `unsupported` 由 333 降到 332。
- 身份迁移：dev `c3e80e123a592ecd`／`e720657f9744aff7`、dev-map `a2e76db7002d51c1`／`c1bfa1c760a80690`（上一版 `8b0dd0f571004fd0`／`1b5d78529aeeb4de`、`85beb60106ad58a9`／`8262c6c4a0d76aee` 记在注释里）。
- 公开语义 10/10、遗物生命周期与 Run 14/14、文档与差异检查通过。

## 未验证与下一步

只有受控内容场景证据：没有独立双侧、正常整局获得或公开 Host 轨迹，完整回归仍交 GitHub Actions。下一已就绪成果：修 `SelfFormingClayPower` 的按层数结算（`perPowerStack` 解析），把它从“固定 3”升级到与来源一致；之后继续 `ModifyDamageAdditive` 组。
