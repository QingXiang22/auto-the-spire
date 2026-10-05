# STS 专用后端归档

2026-10-04，用户决定当前架构不再依赖 Legacy。旧内核及直接消费者移到本目录，保留原命名空间和源码，不进入 `AutoTheSpire.sln`，活动 core/base/net 和 differential 不引用归档工程。

## 保存范围

- `core-the-spire/Core.TheSpire.Legacy`：25 个旧后端源文件及旧测试的 friend assembly 声明。独立程序集名 `Core.TheSpire.Legacy`；只反向依赖活动 core 中的通用 DSL AST/解析器。
- `mod-the-spire/base-spire/BaseSpire.Legacy`：旧注册器、Ironclad 卡池注册和地图生成器；`Content`/`ContentSource` 是归档时的内容快照，活动内容源没有移动。
- `core-the-spire/Core.TheSpire.Tests`：旧规则检查，保留归档前已有修改。
- `mod-the-spire/test-hook-mod/TestHookMod`：旧 hook 示例。
- `mod-the-spire/base-spire/BaseSpire.Compatibility.Tests`：从活动 Slice 套件移出的 4 项新旧前端兼容检查；需要活动 base 的作者前端，但活动 base 不依赖本归档。
- `tools/core-architecture-audit`、`tools/content-manifest`：旧注册器工具。既有冻结 manifest JSON 仍在活动 `tools/content-manifest` 中供历史范围核对。

归档不是将这些内容迁移完成。旧注册量不再计入当前实现；正式原版能力仍需在 Engine/base 新路径实现。不要添加从活动工程到本目录的 ProjectReference、链接编译项或运行时回退。

## 单独验证

从仓库根目录执行：

```powershell
dotnet run --project archive/legacy-sts/core-the-spire/Core.TheSpire.Tests
dotnet run --project archive/legacy-sts/mod-the-spire/base-spire/BaseSpire.Compatibility.Tests
```

2026-10-04 实测：旧规则 190 passed / 0 failed / 190 executed；前端兼容 4/4。活动 solution 构建 0 错误 0 警告；Engine 69/69、Slice 264/264、net 393/393。Slice 原 268 中的 4 项兼容检查只是改变归属，未删除。更多当前决定见[core 架构决定](../../docs/design/core-the-spire/core-architecture-decisions.md)。
