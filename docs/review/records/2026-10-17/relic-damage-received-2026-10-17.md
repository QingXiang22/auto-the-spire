# 遗物受击触发与 CentennialPuzzle

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批新增“玩家真正吃到伤害之后”的遗物触发点，并纳入 CentennialPuzzle。归属：DSH。

## 新能力

触发点 `after-damage-received`（源 `RelicModel.AfterDamageReceived`，条件对应 `result.UnblockedDamage > 0`）。实现方式是在共享玩家受伤过程 `DamageCompilation.PlayerDamageBody` 的“真的掉了 HP”分支里追加一个**先声明过程符号、稍后补过程体**的调用（`RelicAfterDamageReceived`）：这样受击时点与既有 `LostHp`／`ReceivedDamage`／`PlayerDamaged` 事件同源，而过程体可以在抽牌等绑定就绪后再编译（首次尝试直接在受伤过程里编译遗物监听会因抽牌绑定尚未建立而空引用）。

当前只对 `after-damage-received` 开放 `draw-cards` 与 `add-counter`／`set-counter` 三种效果，条件用既有的 `CounterEquals`。

## 消费者（源依据）

`CentennialPuzzle`（Common，`CentennialPuzzle.cs:14,36-56`）：`AfterDamageReceived` 且目标是自己、`result.UnblockedDamage > 0`、本场战斗未用过时，抽 3 张并把标记置为已用；`AfterCombatEnd` 复位。

声明：`before-combat-start` 的 `set-counter` 0＋`after-damage-received` 的 `draw-cards` 3（`CounterEquals=0`）＋同一触发点的 `add-counter` 1（顺序保证先抽牌再落标记）。

## 命令与结果

```powershell
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
```

- 遗物内容组 **81/81 通过**：场景跑到第一次真正掉血的那一回合，断言手牌由 5 变 8（正好抽 3 张），之后同一场战斗继续掉血也不再抽（手牌回到 5）。
- 覆盖报告：**82/82 可执行**、source=297、**unimplemented 由 215 降到 214**，校验器 exit 0；构建描述 `unsupported` 由 334 降到 333。
- 身份迁移：dev `8b0dd0f571004fd0`／`1b5d78529aeeb4de`、dev-map `85beb60106ad58a9`／`8262c6c4a0d76aee`（上一版 `8f6d5a74a9f0e49d`／`ac56bbdcf1117d15`、`3b104072e22f7fb1`／`3d9836c424faae` 记在注释里）。
- 公开语义 10/10、遗物生命周期与 Run 14/14、文档与差异检查通过。

## 未验证与下一步

只有受控内容场景证据：没有独立双侧、正常整局获得或公开 Host 轨迹，完整回归仍交 GitHub Actions。该触发点目前只在“玩家掉血”时触发（对宠物代伤等其它受击路径未接线）。下一批候选：`ModifyDamageAdditive` 组（StrikeDummy／FakeStrikeDummy 按 Strike 标签、MiniatureCannon 按升级、MysticLighter 按附魔）需要把伤害来源牌接进加性阶段；或继续用 `after-damage-received` 带出 SelfFormingClay 一类需要“下一次/下回合格挡”的消费者。
