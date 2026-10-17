# 遗物回合开始与进入战斗的既有能力消费者

任务：**ENV-T09-PR02-CONTENT**（全部遗物的适用性／行为／验证去向）。本批不新增触发点或效果种类，只把两件已有能力就能表达的源遗物纳入正式内容。归属：DSH。

## 两件消费者（源依据）

| 遗物 | 稀有度 | 源行为 | 本批声明 |
| --- | --- | --- | --- |
| FestivePopper | Common | `FestivePopper.cs:17-30`：玩家回合开始且 `TurnNumber == 1` 时，对全体可命中敌人造成 9 点 Unpowered 伤害 | `after-player-turn-start` → `damage-all-enemies` 9，`OnTurn=1` |
| DivineRight | Starter（Regent 池） | `DivineRight.cs:18-24`：进入战斗房时获得 3 颗星 | `after-combat-room-entered` → `gain-stars` 3 |

两件资格与源依据写入 `relic-source-catalog.json`（`Eligibility=singleplayer`）。

## 检查中的一个事实

首回合群体伤害不能拿 `MaxHp` 当基线：该受控遭遇里怪物出生生命可能低于 `MaxHp`，用 `MaxHp - Hp` 会把出生差值算进伤害（首次断言 18 期望 vs 23 实测）。改为同 seed 的参照局：先跑一局没有该遗物的 `BeginMonster`，用它的 `Hp` 总和作基线，差值即遗物造成的伤害，并同时断言两局的敌人数量一致。这个写法比读 `MaxHp` 更严格，也不依赖出生掷点实现细节。

## 命令与结果

```powershell
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-content-only --report artifacts/relic-content
python -X utf8 tools/ci/check_relic_coverage.py artifacts/relic-content/relic-coverage.json
```

- 遗物内容组 **77/77 通过**：FestivePopper 断言首回合按“9 × 敌人数”掉血、之后回合不再掉血；DivineRight 断言每次进入战斗房都是 3 星（重开不累加）。
- 覆盖报告：**78/78 可执行**、source=297、**unimplemented 由 220 降到 218**，校验器 exit 0；构建描述 `unsupported` 由 339 降到 337。
- 身份迁移：dev `775b4fee84aa7800`／`b103e81fede9d804`、dev-map `443d0d6515615aa3`／`4e39836a700af26d`（上一版 `ea4d576da58dae95`／`5e89ed3d07e0b338`、`e95e223845ed0846`／`e7c9eb9bb55a084b` 记在注释里）。
- 公开语义 10/10、遗物生命周期与 Run 14/14、文档检查 691 份 0 问题、`git diff --check` rc0。

## 未验证与下一步

只有受控内容场景证据：没有独立双侧、正常整局获得或公开 Host 轨迹，完整回归仍交 GitHub Actions。DivineRight 属 Regent 池，当前只验证效果本身，不代表 Regent 正式开局可用。下一批候选：`ModifyDamageAdditive` 组（StrikeDummy／FakeStrikeDummy 按 Strike 标签、MiniatureCannon 按升级、MysticLighter 按附魔）需要“带牌来源过滤的伤害加性修正”这一新种类；`AfterPlayerTurnStart` 组其余成员需要手牌选择／随机生成能力。
