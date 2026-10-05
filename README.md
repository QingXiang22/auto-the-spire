# auto-the-spire

2026-10-05：通用内核冻结为 engine/ir@0.3。此提交独立收录核心及必要配套，下游 base/net/AI 工作区改动仍待各自提交。当前入口与验证见 [core 冻结交接](docs/contracts/core-git-checkpoint.md)；下文旧 API 描述保留历史背景，以冻结契约为准。

为 AI 提供可复现、可观察、可控且可扩展的《杀戮尖塔 2》训练环境。整体依赖方向是：通用规则内核 `core-the-spire` → 原版内容包 `mod-the-spire/base-spire` 与其他 mod → 未来由 `net-the-spire` 暴露统一的 Gym 风格接口。

## 当前构成

- `core-the-spire/Core.TheSpire`：Godot 无关的 .NET 9/C# 战斗状态机、整局地图流转骨架、效果树、内容注册、观察/动作 API 和会话随机数服务。
- `mod-the-spire/base-spire`：铁甲战士起始角色数据、基础卡牌、力量/易伤/仪式及咔咔战斗切片。
- `mod-the-spire/test-hook-mod`：用于验证内容覆盖、新增内容、受控 C# 效果和每局 mod 状态的独立示例 mod。
- `core-the-spire/Core.TheSpire.Tests`：不依赖外部测试包的可执行规则检查。
- `sts2/`：源码恢复浏览工程，仅用于核对规则，不是内核编译依赖。

## 构建与验证

```powershell
dotnet build AutoTheSpire.sln
dotnet run --project core-the-spire/Core.TheSpire.Tests
```

当前实现是战斗切片与通用整局流转骨架，不代表完整原版模拟。源码依据、已读规则域和待扩展清单见 [`docs/sts2-core-source-audit.md`](docs/sts2-core-source-audit.md)；整体契约见 [`docs/spec.md`](docs/spec.md)。
