# 开始贡献

本页帮助第一次接触仓库的贡献者找到环境、练习和交付入口。所有命令从仓库根目录执行；只读文档无需安装游戏或准备参考源码。

1. 按[环境与首次检查](environment.md)准备所需工具。只改文档可直接运行 `python tools/ci/check.py docs`；修改运行时再准备 .NET SDK。
2. 选择一个受控练习：[第一份规则](first-rule.md)学习 DSL，[第一个宿主](first-host.md)学习 Session；内容作者从[配置第一个遗物](first-relic.md)开始，再做[共享触发规则](shared-rule.md)。练习修改留在隔离副本。
3. 按[第一个改动](first-change.md)选择工单、定位文件并运行受影响检查。已有基础的内容作者可以直接进入[修改单个内容](../areas/base/how-to/edit-content.md)。
4. 按 [CONTRIBUTING](../../CONTRIBUTING.md#开始与交付)提交 PR，写明实际验证及限制；任务认领、优先级和完成条件以[任务入口](../roadmap.md)连接的 Issue 为准。

需要先判断应改哪个模块时，读[架构总览](../understand/architecture.md)；查询当前语法和字段时，用[参考索引](../reference/README.md)。
