# 消耗堆、失去生命与四张 Ironclad 牌交付记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-CARD-EXHAUST-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录对应[本包规格](../../../design/mod-the-spire/card-exhaust/spec.md)的 CX01～CX06，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

新增**消耗**：`Card.Exhausts`（源 `CardKeyword.Exhaust` 的开发替身）、`ExhaustPile` 集合、`CardExhausted` 事件；`FinishCardPlay` 在结算后按 `Exhausts` 分流到消耗堆或弃牌堆。新增**绕过格挡的失去生命** `LosePlayerHp`（对应 Offering／Bloodletting 的 `Unblockable | Unpowered` 伤害）。效果模板新增 `EnergyEffect` 与 `LoseHpEffect`，`CardDefinition` 增加 `exhausts` 参数，`AddCopyEffect` 的克隆同时复制 `Exhausts`。

迁移四张新卡：Impervious（2 费 30／40 格挡，消耗）、Offering（0 费失去 6 生命 +2 能量抽 3／抽 5，消耗）、Bloodletting（0 费失去 3 生命 +2／+3 能量）、Bludgeon（3 费 32／42 单体伤害）。卡牌目录新增四个 definition 与审阅结论，578 源分母不变。包版本升到 `0.5.0-dsl.7`，卡牌定义共 12 张。

## 来源依据

| 内容 | 源 | 行为 |
| --- | --- | --- |
| `base-spire:impervious` | `Impervious.cs`，`9a97b309…e9bb79552` | cost 2、`BlockVar(30, Move)`、`CardKeyword.Exhaust`、升级 +10 格挡 |
| `base-spire:offering` | `Offering.cs`，`4c46c463…0d0a3147db1` | cost 0、`HpLossVar(6)` 不可格挡无加成伤害、`EnergyVar(2)`、`CardsVar(3)`、Exhaust；升级 +2 抽牌 |
| `base-spire:bloodletting` | `Bloodletting.cs`，`3787518b…66e98c323f` | cost 0、失去 3 生命、+2 能量；升级 +1 能量 |
| `base-spire:bludgeon` | `Bludgeon.cs`，`2770f0c3…21fd9c6ab8` | cost 3、`DamageVar(32, Move)`；升级 +10 |

四条 hash 均由已安装 skill 的定位助手从安装路径核对为 `source_hash_status: matches`，definition 指向本批新文件，资格为 `singleplayer`。定位只证明静态源身份一致，不是玩法执行或独立验证。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `cb0adf81e8fc3e2ddc34c77e53ab3eadfa3d0eefb28b894f3e35b1d0b079de13`，规则身份 `5eb6beea8a8d4e81`；行为报告内的 `package_digest` 与当前包一致，报告含 8 件遗物与 12 张卡的见证。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.7`；card 目录 578 源／12 个 DSL definition |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 56 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：8 件遗物行为见证；源 297；未实现 288；`independent_verified=false` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 356 项通过／0 失败，含新增 CP25 消耗技能真实 stdio 轨迹 |

新增判别覆盖：消耗牌不在弃牌堆、订阅 CardExhausted 时该卡已在消耗堆（顺序判别）、消耗堆实例保留身份与费用；失去生命在 5 格挡下仍掉 3 且格挡不变；0 费 Bloodletting 在 3 能量上得到 5（可超过 MaxEnergy）；Impervious 普通 30／升级 40 且两张都消耗；Offering 普通抽 3、升级抽 5（用 8 张牌的可抽池区分）；Bloodletting 普通 +2／升级 +3 能量且不消耗；Bludgeon 普通 32（40→8）／升级 42（生命下限 0）。真实 stdio 轨迹提交 `Impervious.AddToHand` 与 `PlayCardFromHand`，观察公开格挡 0→30、能量 3→1。

## 未验证与未支持

- 未运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵与性能套件；`independent_verified` 保持 false。
- 未实现：其它消耗来源与消耗反应钩子、虚无／保留／固有、X 费与费用修改（定义层升级费用除外）、出牌次数限制、卡牌类型与稀有度字段、Power 牌、卡牌获得途径、原版目标选择、死亡与胜负。
- 失去生命不触发受击反应、不应用伤害修正；消耗与失去生命的交互（例如“消耗时回复生命”类能力）尚未迁移。
- 同一张牌内多效果的顺序仍是“先无目标效果、后有目标伤害”，未逐卡核对所有原版 OnPlay 排序。

## 接续入口

下一包可继续共享战斗机制（Power／易伤等修正参与者），或补原版目标选择与公开决策应答、多目标伤害与更多只依赖现有组合模板的 Ironclad 牌。开始前先读主清单确认认领与依赖；不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
