# 调用纯 DSL 包的公开接口

文档类型：操作指南（How-to）。本页示例适用于当前开发 scope；已有有限正常开局与首战奖励返回路径，完整 Run 尚未验收。规则通过通用 Host 加载，Python 客户端从公开观察中读取候选并提交，不实现玩法。

## 准备并运行一个真实效果

需要仓库 `global.json` 指定的 .NET SDK 和 Python 3.12，在仓库根目录执行：

```powershell
dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj -v quiet
python -X utf8 docs/examples/base-dsl/run_relic_lesson.py --manifest mod-the-spire/base-spire/package.json --obtain TheAbacus.Obtain --draw-count 4 --expect-block 6
```

驱动通过现有 stdio 客户端获取 describe／catalog，以 manifest 的开发 scope 启动会话，提交获得遗物和抽牌的当前合法候选。牌堆重洗后，公开 Player.Block 应为 6，报告中 `passed` 为 true，退出码为 0。这是开发机制调用，完整游戏验收见 [环境规格](../../../specs/environment.md)。

修改内容的练习见 [遗物教程](../../../start/first-relic.md)。需要保存调试输出时使用驱动的 `--report .artifacts/public-example/result.json`，完成后删除。

## 客户端调用顺序

```python
client = NetClient(["dotnet", host_dll, "--package", manifest_path])
description = client.describe()["result"]
identity = description["identity"]
client.get_catalog(identity["catalog_id"])
started = client.start(identity, profile, seed="PUBLIC-EXAMPLE")["result"]
snapshot = started["snapshot"]
chosen = snapshot["legal_actions"][0]
client.step(started["session_id"], snapshot["observation_id"], chosen["candidate_id"])
```

`profile` 从 manifest 的受支持 scope 选择。每次提交后重新 observe，使用新 observation_id 和当前候选；先检查响应 `ok`，失败时按 [接口参考](../reference/public-client.md)处理。实际效果、暂停和终局以 Host 输出为准。

## 读取当前牌区

base 包将 Hand、DrawPile、DiscardPile、ExhaustPile、InPlay、RemovedFromCombat 和 RunDeck 发布为 `piles`。先在目录的 `pile_types` 找到对应的 `net:pile_type:base-spire:<集合名>`，再用其 `kind_id` 匹配快照中的牌堆实体，最后按该实体的 `instance_id` 查找 `pile_ref`。牌堆实体的 `Player` 字段指向所属玩家；同一会话的牌堆引用跨回合、战斗与房间保持稳定。

读取 `total_count` 获取真实数量，通过 `visible_members` 读取当前公开成员，并检查 `members_complete`。DrawPile 的成员与内部顺序隐藏，只发布总数；其余牌区发布完整成员。不能把 `visible_members` 的数组顺序当作游戏位置。RunDeck 的成员只从 `piles` 读取，旧 `net:group_type:base-spire:RunDeck` 已移除；重新获取目录和 identity 后启动会话。

当前包没有登记已知牌顶的位置事实，`known_positions` 为空；这项限制仍需后续规则记忆支持。正常开局和局部牌区事实可调用，不代表完整 Run 或完整公开观察已经验收。

## 扩展为正常 Run

正常开局、地图和完整房间生命周期恢复后，再将 [整局驱动](../../../../net-the-spire/client/run_game.md)接到相同正式包。未支持的 scope 应明确拒绝；当前开发动作的成功不能证明整局可运行。验证入口及覆盖范围以 [CI 说明](../../../../tools/ci/README.md)为准。
