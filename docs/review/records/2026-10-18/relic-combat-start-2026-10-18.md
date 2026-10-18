# 战斗开始格挡遗物（Anchor／FakeAnchor）DSL 交付与验证记录

日期：2026-10-18。任务：**CORE-AUTHOR-05／DSL-RELIC-START-01**；当前认领、状态与恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录对应[本包规格](../../../design/mod-the-spire/relic-combat-start/spec.md)的 RC01～RC08，是实施与自查记录，不是独立评审，也没有独立原版对拍。

## 交付与范围

在纯 DSL base 包内新增开发态战斗周期：`Player.CombatActive`、`BeforeCombatStart(owner)`、`BeginDevelopmentCombat`／`EndDevelopmentCombat` 过程与同名公开动作，以及共享模板 `BeforeCombatStartBlockRelic`。以该模板迁移两个原版遗物配置：`use Anchor`（10）与 `use FakeAnchor`（4）。包版本升到 `0.5.0-dsl.2`，公开目录新增两个真实 ID。

开始动作只在 Idle→Active 时允许：先验证 inactive，再把本场 `Block` 清 0、置 `CombatActive=true`，最后 emit `BeforeCombatStart`，因此监听器观察到的是已活跃且本场初始格挡为 0 的状态，监听中获得的格挡不会被清掉。结束动作只在活跃时允许，置 `CombatActive=false`，不发开始事件也不发胜利；再次开始重新清 0 并重新派发。该周期是**机制开发边界**，不是正常战斗初始化、回合、胜负或 Run 结算，也不写回跨房永久状态。

## 来源依据

按已安装的项目 skill 入口定位并核对原始字节 SHA256（`source_hash_status: matches`）：

| 内容 | 源 | 资格／池 | 行为 |
| --- | --- | --- | --- |
| `base-spire:anchor` | [Anchor.cs](../../../../sts2/src/Core/Models/Relics/Anchor.cs)，`98ff3fac934c05c8e555fc4c4a445d0b738ade5b6981b96905c002dcdc0b6a3f` | singleplayer／SharedRelicPool | `CanonicalVars` 为 `BlockVar(10m, ValueProp.Unpowered)`，`BeforeCombatStart` 调 `CreatureCmd.GainBlock` |
| `base-spire:fake-anchor` | [FakeAnchor.cs](../../../../sts2/src/Core/Models/Relics/FakeAnchor.cs)，`cebcd73581a376c8554f9f48d8a8ccd11691a9d4420a53e1b4e1b759f9ead478` | singleplayer／EventRelicPool | `BlockVar(4m, ValueProp.Unpowered)`，同一 `BeforeCombatStart` |

两者都使用无加成格挡，因此复用既有 `GainUnpoweredBlock`；没有新增 C# 效果分支。源的 `RelicRarity`、`MerchantCost` 与卡面文案只作元信息，尚未进入包内数据。

## RC08：开发 skill 的实际安装入口

安装前 `%USERPROFILE%\.codex\skills\base-spire-development` 不存在。本次创建 Junction 指向 `F:\Scylier-Project\active\auto-the-spire\.agents\skills\base-spire-development`（真实目标、`SKILL.md`、`agents/openai.yaml`、`scripts/locate.py` 均可从安装路径读出）。随后从**安装路径**执行定位脚本：

```powershell
python -X utf8 "$env:USERPROFILE\.codex\skills\base-spire-development\scripts\locate.py" relics --id base-spire:anchor --verify-source
python -X utf8 "$env:USERPROFILE\.codex\skills\base-spire-development\scripts\locate.py" relics --id base-spire:fake-anchor --verify-source
```

两条均返回 0，`definition_status` 为 `declared-dsl-not-behavior-proof`、`source_hash_status` 为 `matches`。定位脚本只查静态源目录，不被计为玩法执行或独立验证。安装是在本机用户目录进行的链接操作，不修改仓库内容；本机此前已存在的其他 skill 链接未改动。

## 实际验证

Windows／PowerShell，.NET SDK 9.0.306；Debug 配置。包摘要 `7f4fd51e00542da0a67502117dd96f8b7e17d83c1c56dcda88666262ee9b1867`，规则身份 `81e56731d7309c69`（这些是本次最终工作树的实际值；提交后应通过 PR／Actions 关联代码身份）。

| 命令 | 实际结果 |
| --- | --- |
| `dotnet build AutoTheSpire.sln -v minimal` | 通过，0 警告／0 错误 |
| `dotnet run --project tools/base-spire-dev -- validate` | 退出 0；包 `0.5.0-dsl.2`；relic 目录 297 源／3 个 DSL definition |
| `dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior` | 18 项全部通过 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/base-dsl/behavior/package-behavior.json` | 退出 0：3 件行为见证；源 297；未实现 293；`independent_verified=false`；未请求 `--require-complete` |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 351 项通过／0 失败，含新增 RC07 真实 stdio 轨迹 |

行为见证按 RC 逐条覆盖：两个遗物分别在开始获得 10／4、获得与结束都不发奖励、同一持有实例在下一场再次生效；无遗物／已移除／熔化／其他持有者不触发，活跃中才获得要到下一次开始才生效；组合为 14 而不是 28，且另一持有者只得到自己的一份；监听器观察到活跃且本场格挡已清 0，监听获得的格挡保留；重复开始／空闲结束被动作守卫和过程守卫分别拒绝且无状态变化；与 TheAbacus 组合时开始 10、真实重洗再 +6，普通抽牌不触发。三个内容见证各自独立生成，没有用 TheAbacus 场景冒充新遗物。真实 stdio 轨迹用同一 Host 走 begin→observe，核对公开 `Player.Block`／`Player.CombatActive` 与合法候选随相位变化。

## 未验证与未支持

- 没有运行独立原版对拍、完整 Engine 套件（留给 Actions）、正常 Run、完整游戏矩阵或性能套件；`independent_verified` 保持 false。
- 未实现：正常战斗初始化与回合流程、卡牌与 Power、敌人与遭遇、房间／事件、地图与奖励、跨房写回、原版随机布局。旧场景与旧 scope 继续在建会话前拒绝，无 C# 回退。
- 未迁移：遗物正常来源（战斗奖励／事件／商店／角色初始遗物）与替换、熔化、跨战重建；模板的 `Obtain` 只是库存层开发动作。
- 源 297 分母不变；`--require-complete` 仍不成立。

## 接续入口

下一包按[迁移任务](../../../design/mod-the-spire/engine-dsl-migration/tasks.md#后续恢复范围)的依赖顺序从共享战斗与资源机制继续（生命值、能量、伤害与格挡吸收、回合），再交付有源依据的内容消费者。开始前先读主清单确认认领与依赖，不改 Engine 语义，不用 C# 效果绕过 DSL 表达缺口。
