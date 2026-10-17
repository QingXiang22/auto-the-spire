# core、base、net和对拍检查

本页说明 GitHub Actions 的回归检查入口。本地开发默认在整组内容完成后，只跑受影响单元测试和少量预先固定种子的对拍；组内按需编译／加载，不逐文件或逐怪运行行为套件。完整 core、base、net 和严格 regression 交给 push／PR 的 CI，失败后按日志在本地定向复现、修复。纯文档只跑文档与差异检查。

以下命令用于显式复现对应 CI 组，不是每批交付的本地必跑清单。在仓库根目录执行，要求 .NET SDK 由根 global.json 指定，Python 3.12，Git；原版 sts2 源码不进入 Actions。

```powershell
python tools/ci/check.py core
python tools/ci/check.py base
python tools/ci/check.py net
python tools/ci/check.py differential
```

默认Release；需要时传 `--configuration Debug`。结果写入 `.artifacts/ci/<组名>/`，包括各命令原始日志、退出码和result.json。第一个非零命令使当前组失败，后续步骤不冒充执行。GitHub为每组保存独立artifact和step summary；四个job使用独立工作区，不共享obj/bin。

| 组 | 实际执行 | 依赖与结果边界 |
| --- | --- | --- |
| core | 构建Engine检查项目，执行全部通用Engine检查 | 不依赖base/net/独立参考/原版源码 |
| base | 构建Slice检查项目，执行全部runtime组；构建作者工具并执行现有例子 | 包含实际DSL加载、链接、运行及地图等既有判别；不包含读取原版源码的核对 |
| net | 构建公开层检查项目并执行其完整入口 | 包含 C# wire、快照、控制会话及正式 Host 接线；不包含 Python 客户端和 AI 离线检查 |
| differential | 准备锁定的纯上游参考，构建对拍工具，执行严格regression | 从GitHub按SHA获取；缺参考、缺case、零执行、首差等保持非零；双侧与接线自检分列 |

base没有删除源码判别，也不依据文件是否存在自动跳过。分组命令如下：

```powershell
# 无原版源码：只运行运行时行为组
 dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --suite runtime
# 本地提供同版本sts2后，单独核对源码元数据
 dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --suite source
# 默认完整入口保持原有全部判别
 dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests
```

源码组当前为药水完整源池顺序和48项元数据核对。runtime报告显式记suite与source_checks_executed；不能把runtime结果当作执行过完整源码核对。未知/缺少suite值返回2。原有局部反馈开关保留。

参考获取和更新方法见[参考依赖](../../reference/README.md)。工作流为[core-base.yml](../../.github/workflows/core-base.yml)，push/PR/手动触发，首次采用Windows及固定SDK；官方Action锁定到提交SHA。结果和日志与当前提交绑定，不自动发布版本、不启动训练。

长程公开回放、全 seed／120 对矩阵和性能检查仍留在对应里程碑或发布验收；当前工作流尚未自动执行这些检查。需要原版源码的检查在具备同版本源码的环境执行。代码和本地检查完成后可以交付待验证的内容组并继续下一组；合并前核对 CI 结果，新回归修复后再合入，不用跳过或吞掉失败换取通过。本轮只检查配置接线，远端四组结果须在提交后确认。

## contracts-python的其余待接入检查

| 检查 | 现有入口 | 要判别的错误 |
| --- | --- | --- |
| 协议/schema与规范编码 | `python tools/contract-check/check_contracts.py` | 非法数字/十进制编码、非规范JSON、错误身份或动作键、版本/字段不匹配及正负例 |
| Python真实stdio客户端 | `python net-the-spire/client/test_client.py` | 帧编码、断开/超时与结果未知、重连和公开正式场景；须先构建Host并显式定位DLL |
| AI公开输入离线检查 | `python ai-the-spire/tests/run_checks.py` | 字典/编码、引用、候选完整性、张量/策略输入、兼容身份与既有离线组件判别；不启动训练实验 |
| 契约需求映射 | `python tools/doc-check/check_trace.py .` | AI/NET需求、共同验证矩阵和模块规格不同步 |

这些检查与已接入的 net 组共同保护 core/base 产物到公开协议、Python 消费方的交界；不能代替 base 玩法行为或独立对拍。跨端清单重算工具当前另有94条仅前缀记录不可重算，不能把它单独当完整双端一致性证明。

接入前需要明确版本覆盖：当前 `check_contracts.py` 固定读取 `docs/contracts/net/0.1`，其既有210项只能按0.1契约报告，不能冒充0.2完整验证。正式Host 0.2的现有判别位于net检查及客户端formal组；后续应逐项核对0.2矩阵，补实际缺口。先构建Release Host，再通过 `NET_THE_SPIRE_HOST_DLL` 显式传入客户端，避免客户端隐式构建或使用旧DLL。AI离线入口包含有限合成样本的梯度、更新、恢复检查，仍不采样真实游戏或启动训练实验。

完整文档源链接仍在本地。依赖检查器的Legacy同名报警和旧商店默认重放首差继续保留，不通过吞错或continue-on-error混入已通过门槛。当前环境与后续任务只看[主清单](../../docs/review/tofix/README.md)。
