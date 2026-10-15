# core、base和对拍检查

在仓库根目录用同一入口运行本地或GitHub Actions检查。要求.NET SDK由根global.json指定，Python 3.12，Git；原版sts2源码不进入Actions。

```powershell
python tools/ci/check.py core
python tools/ci/check.py base
python tools/ci/check.py differential
```

默认Release；需要时传 `--configuration Debug`。结果写入 `.artifacts/ci/<组名>/`，包括各命令原始日志、退出码和result.json。第一个非零命令使当前组失败，后续步骤不冒充执行。GitHub为每组保存独立artifact和step summary；三个job使用独立工作区，不共享obj/bin。

| 组 | 实际执行 | 依赖与结果边界 |
| --- | --- | --- |
| core | 构建Engine检查项目，执行全部通用Engine检查 | 不依赖base/net/独立参考/原版源码 |
| base | 构建Slice检查项目，执行全部runtime组；构建作者工具并执行现有例子 | 包含实际DSL加载、链接、运行及地图等既有判别；不包含读取原版源码的核对 |
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

## contracts-python提案，本次尚未接入

| 检查 | 现有入口 | 要判别的错误 |
| --- | --- | --- |
| 协议/schema与规范编码 | `python tools/contract-check/check_contracts.py` | 非法数字/十进制编码、非规范JSON、错误身份或动作键、版本/字段不匹配及正负例 |
| C#公开层 | `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 投影、候选闭合、隐藏信息边界、幂等/过期/非法提交及Host接线 |
| Python真实stdio客户端 | `python net-the-spire/client/test_client.py` | 帧编码、断开/超时与结果未知、重连和公开正式场景；须先构建Host并显式定位DLL |
| AI公开输入离线检查 | `python ai-the-spire/tests/run_checks.py` | 字典/编码、引用、候选完整性、张量/策略输入、兼容身份与既有离线组件判别；不启动训练实验 |
| 契约需求映射 | `python tools/doc-check/check_trace.py .` | AI/NET需求、共同验证矩阵和模块规格不同步 |

这一组保护core/base产物到公开协议、Python消费方的交界；不能代替base玩法行为或独立对拍。跨端清单重算工具当前另有94条仅前缀记录不可重算，不能把它单独当完整双端一致性证明。

完整文档源链接仍在本地。依赖检查器的Legacy同名报警和旧商店默认重放首差继续保留，不通过吞错或continue-on-error混入已通过门槛。当前环境与后续任务只看[主清单](../../docs/review/tofix/README.md)。
