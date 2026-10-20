# 调用纯 DSL 包的公开接口

文档类型：操作指南（How-to）。适用于当前开发 scope；正常地图与完整 Run 尚待恢复。规则通过通用 Host 加载，Python 客户端从公开观察中读取候选并提交，不实现玩法。

## 准备并运行一个真实效果

需要仓库 `global.json` 指定的 .NET SDK 和 Python 3.12，在仓库根目录执行：

```powershell
dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj -v quiet
python -X utf8 docs/examples/base-dsl/run_relic_lesson.py --manifest mod-the-spire/base-spire/package.json --obtain TheAbacus.Obtain --draw-count 4 --expect-block 6
```

驱动通过现有 stdio 客户端获取 describe／catalog，以 manifest 的开发 scope 启动会话，提交获得遗物和抽牌的当前合法候选。牌堆重洗后，公开 Player.Block 应为 6，报告中 `passed` 为 true，退出码为 0。这是开发机制调用，完整游戏验收见 [环境规格](../environment-completion-and-differential-v1.md)。

修改内容的练习见 [遗物教程](../../development/base-spire/tutorials/first-relic.md)。需要保存调试输出时使用驱动的 `--report .artifacts/public-example/result.json`，完成后删除。

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

`profile` 从 manifest 的受支持 scope 选择。每次提交后重新 observe，使用新 observation_id 和当前候选；先检查响应 `ok`，失败时按 [接口参考](public-client-reference.md)处理。实际效果、暂停和终局以 Host 输出为准。

## 扩展为正常 Run

正常开局、地图和完整房间生命周期恢复后，再将 [整局驱动](../../../net-the-spire/client/run_game.md)接到相同正式包。未支持的 scope 应明确拒绝；当前开发动作的成功不能证明整局可运行。验证入口及覆盖范围以 [CI 说明](../../../tools/ci/README.md)为准。
