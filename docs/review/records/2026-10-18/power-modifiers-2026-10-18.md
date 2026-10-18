# 能力与伤害修正参与者交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-POWER-MODIFIERS-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录对应[本包规格](../../../design/mod-the-spire/power-modifiers/spec.md)的 PW01～PW06，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

新增开发态能力实体 `Power`（`DefinitionId`、`Kind`、可选 `HolderPlayer`／`HolderEnemy`、`Stacks`）与 `Powers` 集合，共享过程 `ApplyPlayerPower`／`ApplyEnemyPower`：同持有者同定义累加层数，否则创建实例。`Kind` 是规则查询用的修正角色（`damage-additive`／`damage-taken-multiplier`／`damage-dealt-multiplier`／`block-additive`／`none`），因此修正链不按能力 ID 分支。

修正链：卡牌攻击走新的 `DamageEnemyPowered`，按源顺序先加攻击者 Strength、再在目标有 Vulnerable 时乘 1.5 并向下取整，最后格挡→生命；卡牌格挡 `GainBlock` 累加 Dexterity。遗物的无加成伤害与格挡继续走 `DamageEnemy`／`GainUnpoweredBlock`，所以修正只作用于 `ValueProp.Move` 的卡牌数值。Weak 只记录层数。

效果模板新增 `ApplyEnemyPowerEffect`（单体）、`ApplyAllEnemiesPowerEffect`（全体）与 `ApplySelfPowerEffect`（自身，暂无正式内容）。迁移三张新卡：Bash、Uppercut、Shockwave（无色）。包版本升到 `0.5.0-dsl.8`，卡牌定义共 15 张。能力目录 260 条保持 `definition` 为空：本设计把修正角色写在应用它的卡牌内容里，不新增能力定义文件。

## 来源依据

| 内容 | 源 | 行为 |
| --- | --- | --- |
| `base-spire:bash` | `Bash.cs`，`368bb650…0efec9f7` | cost 2、8 伤害后施加 Vulnerable 2；升级 +2 伤害、+1 Vulnerable |
| `base-spire:uppercut` | `Uppercut.cs`，`a9d2d834…c285aa1fd` | cost 2、13 伤害后施加 Weak 1 与 Vulnerable 1；升级两项各 +1 |
| `base-spire:shockwave` | `Shockwave.cs`，`0a64fe72…ee43eb24`（无色卡组） | cost 2、Exhaust、对全部敌人施加 Weak 3 与 Vulnerable 3；升级 +2 |
| Strength | `StrengthPower.cs`，`267b986f…283ffe49` | `ModifyDamageAdditive`：攻击者是持有者且是 powered attack 时加层数 |
| Vulnerable | `VulnerablePower.cs`，`8fd6a82d…a3147126` | `ModifyDamageMultiplicative` 1.5，仅 powered attack；敌方回合结束时减层 |
| Dexterity | `DexterityPower.cs`，`f6b56112…07bac1e9` | `ModifyBlockAdditive`：卡牌格挡加层数 |

三张卡的源 hash 均由已安装 skill 的定位助手从安装路径核对为 `source_hash_status: matches`，资格为 `singleplayer`（Shockwave 池为 ColorlessCardPool，其余为 IroncladCardPool）。能力条目的 hash 来自目录登记值，本批未逐条重新计算。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `80e4f9c5ae3fce25f9543e32cda591013f3b4cf97366791694810c09386ec3f9`，规则身份 `18863ce0095b4ce2`；行为报告内的 `package_digest` 与当前包一致，报告含 8 件遗物与 15 张卡的见证。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.8`；card 目录 578 源／15 个 DSL definition |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 64 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：8 件遗物行为见证；源 297；未实现 288；`independent_verified=false` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 357 项通过／0 失败，含新增 CP34 Bash 真实 stdio 轨迹 |

新增判别覆盖：Bash 先造成 8 再施加 2 层 Vulnerable，随后 6 点攻击对易伤目标造成 9（向下取整），层数不会自行减少；同定义重复施加只累加层数（两次 Uppercut 后 Weak／Vulnerable 各一个实例）；Strength 3 只让卡牌攻击从 6 变 9，MercuryHourglass 的无加成 3 不变；Dexterity 2 只让卡牌格挡从 5 变 7，Anchor 的无加成 10 不变；Shockwave 对两个目标分别施加 3 层并进入消耗堆；升级后的 Bash（10×1.5=15）与 Uppercut（13×1.5=19）数值正确。真实 stdio 轨迹提交 `Bash.AddToHand` 与 `PlayAttackFromHand`，观察公开目标 40→32、能量 3→1 与 `Power.Stacks` 2。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵与性能套件；`independent_verified` 保持 false。
- 未实现：能力的回合末衰减与移除（Vulnerable／Weak 在源里于敌方回合结束减层，本包层数不衰减）、能力类型与施加／移除反应、敌方攻击与 Weak 的实际效果、Player 侧能力的正式内容（Strength／Dexterity 由包外探针验证，`ApplySelfPowerEffect` 暂无内容）、原版目标选择、死亡与胜负。
- 能力目录 260 条的 `definition` 仍为空，因为修正角色写在应用卡牌内容里而不是能力定义文件；这是一处已知架构偏差，后续可改为能力侧的注册表。
- 修正链目前只考虑攻击者固定为开发玩家；多攻击者、按来源的伤害区分与 `ModifyHpLost` 未迁移。

## 接续入口

下一包可在能力侧补正式内容（Flex／Inflame 等自增益与 Power 牌的离场规则）与回合末衰减，或补原版目标选择与公开决策应答、死亡与胜负判定。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
