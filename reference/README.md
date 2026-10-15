# 对拍参考依赖

生产core/base/net不引用独立参考。严格对拍工具按[锁定清单](baselines/sts2-sim.json)从GitHub拉取sts2-sim；克隆目录被忽略，不作为submodule或vendored源码提交。

本次2026-10-15拉取上游main最新提交 `2089659bd07920088b93226136c72b6b6ee590ad`，使用纯上游代码，不应用本地补丁。锁定SHA和Git tree用于重复运行；后续更新先拉取最新、实际编译/对拍，再修改锁定清单，避免一次代码提交在不同日期使用不同参考。

在仓库根目录运行：

```powershell
python tools/differential/prepare_reference.py
python tools/ci/check.py differential
```

准备脚本只在目标缺失时创建克隆并按固定SHA获取；已有克隆必须是对应提交/tree且无本地改动，否则拒绝覆盖。离线恢复可传入 `--source <已有纯上游克隆路径>`。实际构建和严格suite的非零结果原样保留。

旧的 `2cdaea3` 加本地修改副本已完整保留在 `F:/Scylier-Project/backups/sts2-sim-pre-upstream-20261015/`。当时的锁定清单、补丁及许可证保存在[历史提交](https://github.com/QingXiang22/auto-the-spire/tree/dcc7d609c1fa88f5abadebddd9ea6bf8e1efee5b/reference/baselines)，不用于当前CI。旧轨迹仍对应其原参考身份，不能冒用新版身份。

`sts2-ai-mcp`和orimod仍仅作本地研究资料。已退出的CombatSolver登记只保留在历史中。

原版 `sts2/` 源码保持本地：不上传GitHub、不作为Actions输入。需要它的源码核对和完整文档检查在本地执行；base检查的 `--suite runtime` 与独立参考严格regression均不要求该源码。当前regression的有限通过不代表完整原版/四幕/五角色验收。
