# 验证与查看纯 DSL base 包

本工具在仓库根目录加载 Engine 的纯文本包，不调用旧 C# 玩法。需要根 `global.json` 指定的 .NET SDK。省略 manifest 时加载 `mod-the-spire/base-spire/package.json`。

```powershell
dotnet run --project tools/base-spire-dev -- validate
dotnet run --project tools/base-spire-dev -- describe
dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior
```

`validate` 检查文件边界、引用、严格静态数据与公开元数据，并实际启动 DSL Session。`describe` 输出包摘要、规则身份、scope 和源／DSL 定义计数。`check` 执行当前 base 的代表性行为、边界与非法输入检查；它是包外的 C# 检查脚手架，不提供玩法实现。`--report` 写入实际通过场景及卡牌／遗物等行为见证，供 [DSL 遗物覆盖检查](../ci/check_relic_coverage.py)与[卡牌完整覆盖检查](../ci/check_card_coverage.py)消费。铁甲战士定向调试可用 `check --ironclad-only`，卡牌奖励可用 `check --card-rewards-only`，原版随机源向量可用 `check --rng-only`，它不生成整包覆盖报告；最终仍运行完整 `check --report`。

可在命令后传入其他 manifest 路径，再使用 `--scope <id>`。本版 `check` 用于当前 base 开发范围；仅通用加载验证使用 `validate`。退出码 0 为当前命令完成，2 为格式／加载失败，3 为未支持。旧 `play`、`probe` 和直接传卡牌 `.spire` 的入口明确返回 3。

遗物内容接续组可用 `check --relic-content-only` 定向验证拾取资源、动态抽牌／营火治疗、出牌计数和首次受损抽牌。该命令不生成覆盖报告；形成逐件证据时仍运行完整 `check --report`，再由 `tools/ci/check_relic_coverage.py` 核对实际定义与见证。源目录保持297项，不能把局部通过写成全遗物完成。

`base-spire-dsl-relic-draw-dev-v1` 是单玩家、全部解锁的机制开发范围，覆盖实际出牌／选敌／选牌、耗尽、X 费、费用修改、复制／生成／变换、自动与重复出牌、所需 Power 反应、已有敌人／遗物／药水、营火与金币奖励。铁甲战士以完整源池 87 项核对 85 张单人牌，保留两项多人排除；准确共享入口和未支持边界见[铁甲战士操作参考](../../docs/design/mod-the-spire/ironclad-dsl/shared-operations.md)。显式 Ironclad 基础初态、原版随机算法／流布局和自动起手已接入；包含 Neow 的正常开局、完整随机前置消费和正常 Run 不属于此 scope；不使用旧 C# 运行回退。当前数量与交付证据从[主清单](../../docs/review/tofix/README.md#当前交付)定位。

分阶段学习与实际公开结果见[base 开发教程](../../docs/development/base-spire/README.md)。教学驱动复用现有 Python 客户端，不修改正式包或原版行为见证。

金币奖励在胜利后先显示待领金额；通过 `ClaimDevelopmentGoldReward` 领取，再用 `CloseDevelopmentRewards` 关闭，也可直接关闭跳过。未关闭时不能开始下一战或进入营火。详见[奖励契约](../../docs/design/mod-the-spire/combat-rewards/spec.md)。公开路径的定向检查使用 `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only`；默认不带参数的 net 入口仍运行完整迁移检查组。

永久牌组与营火锻造使用已迁移定义的开发获得入口，例如 `Strike.Obtain`。营火从公开 RunDeck 选择可升级永久实例，选择／取消走通用 `net.answer_decision`；取消不消耗选项，成功后显式离场。后续战斗重建副本，临时升级不反写永久牌；显式 Ironclad 初态使用原版 Shuffle 流洗牌并自动抽牌；完整前置消费与正常开局仍未迁移。见[永久牌组与锻造契约](../../docs/design/mod-the-spire/permanent-deck-smith/spec.md)。

显式 Ironclad 的首幕开发战斗胜利还提供三张不同的卡牌奖励。通过 `ClaimDevelopmentCardReward` 候选选择一张，所选同一实例加入 RunDeck；未选实例销毁。`SkipDevelopmentCardReward` 仅跳过卡牌，金币仍可领取；`CloseDevelopmentRewards` 丢弃全部未领奖励。奖励使用完整80张可奖励源牌、稀有度偏移与去重，已使用原版随机映射和共享 Rewards 游标，但药水掉落等前置流程尚缺，不代表正常 Run 或完整原版独立对拍通过。
