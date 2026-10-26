# 准备环境并运行首次检查

文档类型：操作指南（How-to）。在仓库根目录按本次改动选择依赖和检查；成功以命令退出码为 0 判断，不能从文档检查通过推断游戏功能通过验收。

## 只修改文档

准备 Git、Python 3.12 和 Vale 3.24.0。Vale 安装与固定版本校验见[文档检查说明](../../tools/doc-check/README.md)，这一组不需要 .NET SDK。

```powershell
python --version
git --version
vale --version
python tools/ci/check.py docs
```

命令检查纳管 Markdown 的链接、锚点、JSON 示例、需求引用、项目术语和差异格式。缺 Vale 或版本不符时，按文档检查说明处理后再运行。

## 修改运行时或原版内容

安装 [global.json](../../global.json) 指定的 .NET SDK 9.0.306；示例使用 PowerShell，Python 入口使用 Python 3.12。正常构建和包加载无需本地原版源码。

```powershell
dotnet --version
dotnet build AutoTheSpire.sln
dotnet run --project tools/base-spire-dev -- validate
```

随后按[第一个改动](first-change.md)选择受影响检查。工具参数、各检查组的实际范围和输出位置统一查 [CI 说明](../../tools/ci/README.md)，不在这里维护第二套命令清单。

独立比较另按[参考依赖说明](../../reference/README.md)准备锁定来源。AI 离线组件的 Python 依赖以 [AI 模块说明](../../ai-the-spire/README.md)为准；准备环境不授权启动新的学习实验。
