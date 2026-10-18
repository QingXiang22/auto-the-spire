# 回合末减层与 Power 牌离场交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-POWER-DURATION-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录接在[能力与修正参与者](../../../design/mod-the-spire/power-modifiers/spec.md)之后，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

补齐能力的时长与 Power 类卡牌的离场：

- 新增回合末时点 `AfterTurnEnded`：`EndDevelopmentTurn` 先弃掉手牌，再派发该事件，然后才关闭回合门。共享过程 `TickDownDebuffs` 订阅它，把 debuff 角色（`damage-taken-multiplier`、`damage-dealt-multiplier`）的能力减一层，归零时移出集合并销毁实例，对应源 `PowerCmd.TickDownDuration`。Vulnerable／Weak 在源里于**敌方回合结束时**减层；由于敌方回合尚未迁移，这个开发时点对单玩家是可观察等价的。
- 新增 `Card.LeavesPlay`（对应 `CardType.Power`）与 `RemovedFromCombat` 集合：结算顺序为 `Exhausts`→消耗堆、`LeavesPlay`→离场、否则弃牌堆；只有消耗分支发 `CardExhausted`。源 `CardPileCmd.RemoveFromCombat` 把 Power 牌移出战斗并交回主牌组，本 scope 没有主牌组，因此保留在 `RemovedFromCombat`。
- `CardDefinition` 增加 `leavesPlay` 参数，15 个既有内容文件同步补上 `leavesPlay = false`。
- 迁移 Inflame（1 费 Power 牌，+2／3 Strength），成为 Strength 的正式内容消费者，闭合上一包用开发探针验证的缺口。

包版本升到 `0.5.0-dsl.9`，卡牌定义 16 张。

## 来源依据

| 内容 | 源 | 行为 |
| --- | --- | --- |
| `base-spire:inflame` | `Inflame.cs`，`adacf6ee…f966ad810` | cost 1、`CardType.Power`、`StrengthPower` 2 于自身；升级 +1 Strength |
| 回合末减层 | `VulnerablePower.cs`、`WeakPower.cs` | 敌方回合结束时 `PowerCmd.TickDownDuration` 减层，归零移除 |
| Power 牌离场 | `CardPileCmd.RemoveFromCombat` | CardType.Power 的牌结算后离开战斗、返回主牌组 |

Inflame 的源 hash 由已安装 skill 的定位助手从安装路径核对为 `source_hash_status: matches`，资格 `singleplayer`。Vulnerable／Weak 的 hash 仍取目录登记值，本批未重新计算。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `a68fb0282704a66965b41f085c72d5595e4a7f6db801119a26073050723838e3`，规则身份 `13563877e6493663`；行为报告内的 `package_digest` 与当前包一致，含 8 件遗物与 16 张卡的见证。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.9`；card 目录 578 源／16 个 DSL definition |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 67 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：8 件遗物行为见证；源 297；未实现 288；`independent_verified=false` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 358 项通过／0 失败，含新增 CP38 Inflame 真实 stdio 轨迹 |

新增判别覆盖：Bash 施加 2 层 Vulnerable 后，回合结束减到 1 层，再结束一次则实例被移除且后续回合仍能正常开始；Power 牌打出后施加 2 层 Strength、不进入弃牌堆也不进入消耗堆而是离场，且随后攻击按 6+2 结算；升级 Inflame 合并到 5 层 Strength、两张牌都离场。真实 stdio 轨迹提交 `Inflame.AddToHand` 与 `PlayCardFromHand`，观察公开能量 3→2 与 `Power.Stacks` 2。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵与性能套件；`independent_verified` 保持 false。
- 未实现：敌方回合（因此减层在玩家回合末发生，而不是源里的敌方回合末）、能力的施加／移除反应、能力类型与清零语义、其它消耗／离场来源、原版目标选择与决策应答、死亡与胜负。
- 能力目录 260 条的 `definition` 仍为空（修正角色写在应用卡牌内容里，已知架构偏差未变）。
- Power 牌离场后不回到任何主牌组；跨战斗牌组写回仍未迁移。

## 接续入口

下一包可补敌方行动与胜负判定（同时把 debuff 减层移到敌方回合末），或继续补原版目标选择与公开决策应答，再批量接只依赖现有组合模板的牌。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
