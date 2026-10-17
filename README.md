# auto-the-spire

auto-the-spire 为 AI 提供可复现、可观察、可控且可扩展的《杀戮尖塔 2》训练环境。当前实现包含战斗切片与整局流转骨架，尚未完全完成。开发接手从[文档总入口](./docs/README.md)开始。

各模块按以下方向协作：通用规则内核 `core-the-spire` → 原版内容包 `mod-the-spire/base-spire` 与其他 mod → `net-the-spire` 提供公开观察和合法动作 → `ai-the-spire` 采样、训练及评价。

## 当前构成

- `core-the-spire/Core.TheSpire`：Godot无关的.NET 9/C#规则内核；活动工程只使用通用`Engine/`
- `mod-the-spire/base-spire`：原版内容包、SinglePlayer开发工厂与战斗/Run实现；具体支持范围和未结项只维护当前清单。
- `mod-the-spire/test-hook-mod`：历史示例说明；当前目录仅保留 README，不是活动可编译项目。
- `core-the-spire/Core.TheSpire.Engine.Tests`：通用 Engine 可执行规则检查。
- `net-the-spire`：已有本机stdio Host、公开投影/内容目录/合法候选及控制客户端，支持受控默认/作者场景。
- `ai-the-spire`：已有公开编码器、共享小模型、有限PPO、模型包及demo/train/evaluate/resume/diagnose；学习获胜效果尚未成立。
- `tools/`：仅纳管[维护中的工程工具](tools/README.md)，临时脚本与生成产物留本地。
- `reference/`：仅纳管拉取源、版本身份及说明，外部依赖由脚本获取。
- `artifacts/`、`.artifacts/`、`.audit/`：忽略的本地日志、轨迹和构建/验证产物。
- `sts2/`：源码恢复浏览工程，仅用于核对规则，不是内核编译依赖。

## 构建与验证

安装 .NET 9 SDK 后，在仓库根目录依次运行：

```powershell
dotnet build AutoTheSpire.sln
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests
```

干净检出的生产构建不需要 `sts2/`。完整 Slice 套件包含读取同版本原版源码的检查，须在提供本地 `sts2/` 后运行；源码不上传 GitHub，也不作为 GitHub Actions 输入。源码提取、源规则核对和全量文档源链接检查同样留在本地。

本地和GitHub的core/base/严格对拍共用[CI检查入口](tools/ci/README.md)。base可显式运行 `--suite runtime`（无原版源码），`--suite source`保留本地源码核对，默认仍为完整组。严格对拍按[参考依赖说明](reference/README.md)从GitHub获取固定上游提交，无须在本仓库保存参考源码或本地补丁；历史文档证据按[归档说明](docs/review/evidence/README.md)恢复。

当前实现是战斗切片与通用整局流转骨架，不代表完整原版模拟。源码依据、已读规则域和待扩展清单见 [`docs/design/sts2-core-source-audit.md`](./docs/design/sts2-core-source-audit.md)；整体契约见 [`docs/design/spec.md`](./docs/design/spec.md)。

## 文档与后续实现

开发接手从[文档总入口](./docs/README.md)开始，再查看[当前任务/P优先级清单](./docs/review/tofix/README.md)及对应角色工作包。当前主线为原版完整单人环境，core消费冻结契约；范围和验收见[ENV任务包](./docs/design/environment-completion-and-differential-v1.md)。公开交互遵守[net协议](./docs/design/net-the-spire/protocol.md)，既有工具用法见[AI README](./ai-the-spire/README.md)。
