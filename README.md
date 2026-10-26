# auto-the-spire

auto-the-spire 的目标是为 AI 提供可复现、可观察、可控且可扩展的《杀戮尖塔 2》环境。当前 base 使用纯 DSL，已有 Ironclad 正常开局至首战奖励返回的有限路径；完整 Run 和新版规则基线对齐仍未完成，不能用现有开发场景训练完整游戏。

通用 Engine 编译并执行 DSL，`mod-the-spire/base-spire/` 仅保存 `.spire` 和静态 `.json`；net Host 加载同一规则包，发布观察和合法动作。简单内容使用模板，复杂内容在所属 DSL 模块拥有规则和私有状态。开发接手从[文档总入口](docs/README.md)和[任务入口](docs/roadmap.md)开始。

## 定位内容

唯一包入口为 [package.json](mod-the-spire/base-spire/package.json) 和 [base.spire](mod-the-spire/base-spire/base.spire)。卡牌、Power、遗物分别放在 `cards/`、`powers/`、`relics/`；共享规则位于对应领域的 `rules/`，具体内容按来源组分文件。例如 [TheAbacus](mod-the-spire/base-spire/relics/shared/the-abacus.spire) 只配置模板与数值，共享重洗规则在 [draw.spire](mod-the-spire/base-spire/cards/rules/draw.spire)。完整目录约定见[迁移设计](docs/design/mod-the-spire/base-architecture.md)。

规则依据为[锁定的 sts2-sim](reference/baselines/sts2-sim.json)，恢复方式见[来源说明](reference/README.md)。本地 `sts2/` 不再作为后续裁决依据。各领域 `catalog.json` 只登记已加载的 DSL 定义；[工具固定数据](tools/base-spire-dev/fixtures/source-catalogs.json)记录现有实现的旧来源，尚不代表新版覆盖，不进入生产包。旧 C# 和检查保留在[迁移对照](tools/base-migration-baseline/README.md)，不进入生产 solution，不提供运行回退。

## 构建与验证

在仓库根目录使用 [global.json](global.json) 指定的 .NET SDK：

```powershell
dotnet build AutoTheSpire.sln
dotnet run --project tools/base-spire-dev -- validate
dotnet run --project tools/base-spire-dev -- check
dotnet run --project net-the-spire/Net.TheSpire.Tests
```

当前包支持范围以[manifest](mod-the-spire/base-spire/package.json)为准。开发scope已覆盖出牌／攻击与伤害结果、费用／牌区、Power、部分遗物／药水／敌人、永久牌组、营火和开发事件；它的名字沿用首批遗物抽牌范围，不表示目前只有抽牌能力。当前支持与来源条目总数以实际定义、来源测试数据及 CI 为准；剩余工作从[任务入口](docs/roadmap.md#近期队列)进入 GitHub Issues，实际交付与验证查看关联 PR。

显式 Ironclad 初态、旧来源的随机流和包含 Neow 的首战奖励返回已接入；它们尚未与最新锁定 sts2-sim 对齐，完整 Run、跨幕与正常逐步比较仍未完成。未支持scope明确拒绝，生产不回退旧C#规则。

构建无需本地 `sts2/` 源码；规则核对恢复锁定的 `reference/sts2-sim/`。当前 [CI 入口](tools/ci/README.md) 分别验证 core、纯 DSL base、net 与迁移边界。原完整差异测试未迁移，执行仍非零；迁移检查通过不代表独立参考比较通过。

## 模块与文档

- [core-the-spire](core-the-spire/README.md)：通用类型、规则编译、状态、事件、随机与 Session。
- [base 开发手册](docs/areas/base/README.md)：配置与共享规则教程、全部领域开发方法、准确操作与排障；[agent skill](.agents/skills/base-spire-development/SKILL.md)按任务读取同一资料。
- [net 协议](docs/design/net-the-spire/protocol.md)：stdio 公开交互；默认 Host 加载纯 DSL 包。
- [工程工具](tools/README.md)：开发验证、迁移验证记录和独立参考入口。
- [AI](ai-the-spire/README.md)：既有客户端与离线组件，训练仍暂缓。

完整单人环境的要求和验收范围继续见 [ENV 合同](docs/design/environment-completion-and-differential-v1.md)，本次功能回退没有删除这些目标。
