# 本地参考依赖

生产 core/base/net 不引用参考实现。严格对拍工具的 `sts2-sim` 是条件编译依赖；缺失时构建仍可成功，严格 suite 必须报告参考不可用。

2026-10-15 清理保留了 `sts2-sim` 的实际本地工作树，而非把上游 HEAD 当成完整身份：固定上游 `2cdaea3b6dbc20cfa340662fa73640f20bb6fb00`，本地改动存于 [补丁](baselines/sts2-sim-local.patch)，摘要和完整 Git tree 见[锁定清单](baselines/sts2-sim.json)。补丁是对既有参考改动的保全，未据此宣称完整原版一致或重新验收参考库。

干净检出后，在根目录运行：

```powershell
python tools/differential/prepare_reference.py
dotnet build AutoTheSpire.sln
dotnet run --no-build --project tools/differential -- test --suite regression --output .audit/regression
```

离线复现可向准备脚本传入 `--source <已有sts2-sim克隆路径>`；脚本从该克隆的 Git 对象恢复固定提交，再应用同一补丁。目标目录已存在时拒绝覆盖；当前共享工作区的参考不会被自动修改。

`sts2-ai-mcp` 仅作架构资料；上游及固定提交也登记在锁定清单，不是构建或检查依赖。`orimod` 为本地研究材料，不纳入本仓库。已不存在且未被活动工程引用的 `CombatSolver` 子模块登记在本次清理中移除，其旧指针仍在 Git 历史和仓库外备份中。

`sts2/` 为本地恢复的原版源码，不随本仓库分发。按用户2026-10-15要求，不上传 GitHub、不放入 GitHub Actions；完整 Slice 中的源数据检查、全量文档源链接核对、源清单提取留在本地，并需要同版本源码。普通生产构建不依赖它。
