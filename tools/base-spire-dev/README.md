# 验证与查看纯 DSL base 包

本工具在仓库根目录加载 Engine 的纯文本包，不调用旧 C# 玩法。需要根 `global.json` 指定的 .NET SDK。省略 manifest 时加载 `mod-the-spire/base-spire/package.json`。

```powershell
dotnet run --project tools/base-spire-dev -- validate
dotnet run --project tools/base-spire-dev -- describe
dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior
```

`validate` 检查文件边界、引用、严格静态数据与公开元数据，并实际启动 DSL Session。`describe` 输出包摘要、规则身份、scope 和源／DSL 定义计数。`check` 执行当前 base 的代表性行为、边界与非法输入检查；它是包外的 C# 检查脚手架，不提供玩法实现。`--report` 写入实际通过场景及 TheAbacus 的行为见证，供 [DSL 遗物覆盖检查](../ci/check_relic_coverage.py) 消费。

可在命令后传入其他 manifest 路径，再使用 `--scope <id>`。本版 `check` 用于当前 base 开发范围；仅通用加载验证使用 `validate`。退出码 0 为当前命令完成，2 为格式／加载失败，3 为未支持。旧 `play`、`probe` 和直接传卡牌 `.spire` 的入口明确返回 3。

当前 `base-spire-dsl-relic-draw-dev-v1` 只实现遗物实例、抽牌／重洗、TheAbacus／Anchor／FakeAnchor 的格挡触发链、Lantern／BloodVial／HornCleat／HappyFlower 所依赖的开发态战斗与回合资源（生命、能量、回合计数、格挡清理、回合末 debuff 减层、胜利／失败判定、敌方阶段）、MercuryHourglass 所用的无加成伤害与格挡吸收（开发目标由 `AddDevelopmentTarget` 创建）、开发态出牌链（含消耗堆、Power 牌离场与失去生命）、Strength／Vulnerable／Dexterity／Frail／Thorns 修正与反击参与者、三十张正式牌、九只敌人、六瓶药水、营火休息房与战斗金币奖励。未迁移原版目标选择、卡牌获得途径、原版敌方 AI／意图数值与遭遇编队、其余原版怪物和 Run，不能通过旧命令执行；[旧作者工具源码](../base-migration-baseline/base-spire-dev/Program.cs)仅作迁移依据。目录及后续恢复见[规格与设计](../../docs/design/mod-the-spire/engine-dsl-migration.md)。

分阶段学习与实际公开结果见[base 开发教程](../../docs/development/base-spire/README.md)。教学驱动复用现有 Python 客户端，不修改正式包或原版行为见证。

金币奖励在胜利后先显示待领金额；通过 `ClaimDevelopmentGoldReward` 领取，再用 `CloseDevelopmentRewards` 关闭，也可直接关闭跳过。未关闭时不能开始下一战或进入营火。详见[奖励契约](../../docs/design/mod-the-spire/combat-rewards/spec.md)。公开路径的定向检查使用 `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only`；默认不带参数的 net 入口仍运行完整迁移检查组。

永久牌组与营火锻造使用已迁移定义的开发获得入口，例如 `Strike.Obtain`。营火从公开 RunDeck 选择可升级永久实例，选择／取消走通用 `net.answer_decision`；取消不消耗选项，成功后显式离场。后续战斗重建副本，临时升级不反写永久牌；初始洗牌、自动抽牌和正常开局仍未迁移。见[永久牌组与锻造契约](../../docs/design/mod-the-spire/permanent-deck-smith/spec.md)。
