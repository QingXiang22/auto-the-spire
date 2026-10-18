# 可组合卡牌效果与八张 Ironclad 普通牌交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-CARD-EFFECTS-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录对应[本包规格](../../../design/mod-the-spire/card-effects/spec.md)的 CE01～CE06，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

把“一张牌一个模板”改成组合式：内容文件用一次 `CardDefinition`（definition、cost、upgradedCost、targeted）加若干效果模板，效果只按 `Card.DefinitionId` 过滤。新增 `Card.Targeted`（原版 TargetType 的开发替身）、`CardUpgraded` 事件与 `DrawCards` 共享操作；`PlayAttackCard` 现在先发 `CardPlayed` 再发 `CardPlayedOnTarget`，与源 `IronWave.cs OnPlay` 的“先格挡后伤害”一致。公开动作名（`Strike.AddToHand`、`PlayAttackFromHand`、`PlayCardFromHand`、`UpgradeCardInHand`）保持不变。

新增六个效果模板：AttackEffect（多段）、BlockEffect、DrawEffect、DamageEqualBlockEffect、AddCopyEffect，以及 CardDefinition。迁移六张新卡：Twin Strike（1 费 5×2／升级 7×2）、Pommel Strike（1 费 9 伤害＋抽 1／升级 10＋抽 2）、Shrug It Off（1 费 8 格挡＋抽 1／升级 11 格挡）、Iron Wave（1 费 5 格挡＋5 伤害／升级 7＋7）、Anger（0 费 6 伤害＋克隆进弃牌堆／升级 8）、Body Slam（1 费伤害等于格挡／升级费用 0）。卡牌目录新增六个 definition 并更新资格与证据，578 源分母不变。包版本升到 `0.5.0-dsl.6`。

上一包的 `AttackCard`／`BlockSkillCard` 被 CardDefinition 与效果模板取代并删除；Strike 与 Defend 改为组合形式，行为与公开动作名不变。

## 来源依据

| 内容 | 源 | 行为 |
| --- | --- | --- |
| `base-spire:twin-strike` | `TwinStrike.cs`，`87131a9f…97f63b29` | cost 1、`DamageVar(5, Move)`、`WithHitCount(2)`、升级每段 +2 |
| `base-spire:pommel-strike` | `PommelStrike.cs`，`3a3330a2…40f56a681` | cost 1、9 伤害＋`CardsVar(1)`；升级 +1 伤害、+1 抽牌 |
| `base-spire:shrug-it-off` | `ShrugItOff.cs`，`4ddebe1b…42a6bb19870` | cost 1、8 格挡＋抽 1；升级只 +3 格挡 |
| `base-spire:iron-wave` | `IronWave.cs`，`842df218…20b0f2ca4` | cost 1、先格挡后伤害各 5；升级各 +2 |
| `base-spire:anger` | `Anger.cs`，`29c2089f…627b541d03` | cost 0、6 伤害，之后把克隆放进弃牌堆；升级 +2 |
| `base-spire:body-slam` | `BodySlam.cs`，`b554a055…d3089b4d3` | cost 1、伤害等于施放时的格挡；升级费用 -1 |

六条 hash 均由已安装 skill 的定位助手从安装路径核对为 `source_hash_status: matches`，definition 指向本批新文件，资格为 `singleplayer`。定位只证明静态源身份一致，不是玩法执行或独立验证。多段伤害在源里是 `WithHitCount(2)`，本包实现为对同一目标连续两次 DamageEnemy，逐段扣格挡。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `b38bdea41207c37c18e5933854d3c15a3dc9bb8283d8718a7a8dd7170bd14e25`，规则身份 `cb6e3f155e61ea34`；行为报告内的 `package_digest` 与当前包一致，报告含 8 件遗物与 8 张卡的见证。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.6`；card 目录 578 源／8 个 DSL definition |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 50 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：8 件遗物行为见证；源 297；未实现 288；`independent_verified=false` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 355 项通过／0 失败，含新增 CP18 Iron Wave 真实 stdio 轨迹 |

新增判别覆盖：有目标与无目标卡由不同公开动作提供候选，互相不出现在对方的候选里；多段伤害逐段扣格挡（目标 3 格挡时先消耗格挡再掉 2，第二段完整掉 5）；抽牌效果在牌进入弃牌堆之前结算（抽牌时弃牌堆尚不含本牌，结算后才移入）；Iron Wave 同一次出牌先得 5 格挡再造成 5 伤害；Body Slam 的伤害等于施放时格挡且升级后费用为 0；Anger 的克隆与原牌同身份、升级后仍带升级状态；八张卡片的普通／升级数值全部按源核对。真实 stdio 轨迹提交 `IronWave.AddToHand` 与 `PlayAttackFromHand`，观察公开格挡 0→5、能量 3→2、目标 40→35。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵与性能套件；`independent_verified` 保持 false。
- 未实现：原版目标选择（公开决策选敌）、X 费与费用修改（定义层升级费用除外）、消耗／虚无／保留／固有、出牌次数限制、卡牌类型与稀有度字段、Power 牌、卡牌获得途径、抽牌到起始手牌、死亡与胜负。
- 同一张牌内多效果的顺序固定为“先无目标效果、后有目标伤害”。这与 Iron Wave 的源顺序一致，但未逐卡核对所有原版 OnPlay 排序；多目标（AllEnemies）与随机目标牌仍未迁移。
- 克隆只进战斗弃牌堆，不写回永久牌组；`AddToHand` 仍只是开发授予。

## 接续入口

下一包按[迁移任务](../../../design/mod-the-spire/engine-dsl-migration/tasks.md#后续恢复范围)补原版目标选择与公开决策应答（net 侧目前没有 Decision／Answer 入口），或先接多目标（AllEnemies）与更多只依赖现有组合模板的 Ironclad 牌。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
