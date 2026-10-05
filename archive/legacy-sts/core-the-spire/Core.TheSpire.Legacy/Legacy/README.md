# Legacy —— 旧的 STS 特化 DSL 后端（已冻结）

2026-10-04 归档说明：本文件下文是迁移前历史说明，路径/测试数/未修缺陷描述不代表当前状态。旧后端已不进入活动 core；当前归档范围和实测入口以[归档总览](../../../README.md)为准。

> **状态：冻结，不再扩展。** 2026-10-01 由 `core-the-spire/Core.TheSpire/` 根目录整体移动到此子目录。
> 新的机制扩展一律走 [`../Engine/`](../../../../../core-the-spire/Core.TheSpire/Engine)（通用规则引擎）。本目录只做必要修复，不新增能力。

## 这是什么

原 `core-the-spire` 的**第一套后端**：一个围绕《杀戮尖塔》领域写死的规则内核 + `.spire` 内容 DSL。

| 分组 | 文件 |
| --- | --- |
| 语法前端 | `ContentDsl.cs`、`EffectDsl.cs`（通用语法解析器 `DslSource.cs` 于 2026-10-03 **路径级移动**到 [`../Engine/DslSource.cs`](../../../../../core-the-spire/Core.TheSpire/Engine/DslSource.cs)：命名空间仍是 `CoreTheSpire`，消费者无需改动；W-06 的新路径因此可以复用同一个解析器做 AST→通用 IR，而不是再写第二个解析器） |
| 内容编译器 | `SpireCombatDslCompiler.cs`（card / cardPool / potion）、`SpireCombatBehaviorDslCompiler.cs`（power / enemy / affliction） |
| 运行时定义 | `Content.cs`、`StateVariableDefinition.cs`、`EncounterPools.cs`、`EncounterSlots.cs`、`ActRoomContent.cs` |
| 会话与结算 | `State.cs`、`Effects.cs`、`Environment.cs`、`CombatCard*.cs`、`Card*.cs` |
| 整局 | `Runs.cs`、`CardRewards.cs`、`RunEffectExecutionContext.cs` |
| 扩展接缝 | `EffectExecutionContext.cs`（`ICustomEffectHandler` / `ICustomRunEffectHandler`）、`ScopedIntState.cs`、`RandomStreams.cs` |

## 现在还在被谁使用

**这次移动只改了文件路径**：这些文件仍属于 `Core.TheSpire.csproj`（SDK 风格工程按 `**/*.cs` 通配），命名空间仍是 `CoreTheSpire`，程序集输出没有变化。因此下面这些消费者**照常工作**：

| 消费者 | 用法 |
| --- | --- |
| [`mod-the-spire/base-spire`](../../../mod-the-spire/base-spire/BaseSpire.Legacy/BaseSpirePackage.cs) | 加载 `.spire` 内容、注册原版卡牌/Power/敌人/遗物；**当前 122 张卡都跑在这套后端上** |
| [`core-the-spire/Core.TheSpire.Tests`](../../Core.TheSpire.Tests) | 旧后端的可执行规则检查 |
| [`mod-the-spire/test-hook-mod`](../../../mod-the-spire/test-hook-mod/TestHookMod/TrainingModPackage.cs) | `ICustomEffectHandler` / `IRunHook` 示例 |
| [`tools/core-architecture-audit`](../../../tools/core-architecture-audit/Program.cs) | 契约探针与性能基线 |

## 为什么冻结

第二套后端 [`../Engine/`](../../../../../core-the-spire/Core.TheSpire/Engine) 提供的是**包可声明的**类型、字段、动作、事件与订阅：

- 新动作：`EnginePackageBuilder.Action(name)`，`LegalActions` 通用枚举，无逐动作硬编码分支；
- 逐实体自定义状态：实体类型字段 + `ExtensibleFields`（后加载的包用 `Extends` 追加字段）；
- 触发/修正：包声明事件 + 带优先级与重检谓词的订阅；
- 目标查询：`Where` / `Sort` / `Take` / `Skip` / `First` / `Count` / `Exists` / `Aggregate`。

而本目录这套是**封闭**的：`PlayerAction` 是固定 record 层级、`state` 只有 combat/run 两个作用域且仅 `int`、`IRuleHook` 是固定 10 个方法、`TargetSelector` 是 7 个封闭枚举。给 OriMod 这类需要"逐实体状态 + 新动作 + 伤害修正点"的 mod 补能力，等于在这套封闭模型上开洞，方向与 `Engine/` 相反。

## 使用本目录时的约定

1. **不要在这里新增原语、hook、内容声明种类或状态作用域。** 需要新能力时，先在 [`../Engine/`](../../../../../core-the-spire/Core.TheSpire/Engine) 表达；如果 `Engine/` 缺能力，先补 `Engine/`（缺口清单见 [`docs/review/tofix/core-the-spire-completeness-audit-2026-10-01.md`](../../../../../docs/review/records/2026-10-01/core-the-spire-completeness-audit-2026-10-01.md) §3）。
2. **可以做的**：修 bug、补测试、更新与现状不符的注释。
3. **改这里要通知的消费者**：`base-spire`、`Core.TheSpire.Tests`、`test-hook-mod`、`tools/core-architecture-audit`（见上表）。
4. 文档里引用这些文件时，路径要写 `core-the-spire/Core.TheSpire/Legacy/<文件>.cs`。

## 验证

```
dotnet build AutoTheSpire.sln                                          # 0 错误 0 警告
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests         # All 32 engine checks passed.
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests    # All 21 slice checks passed.
dotnet run --project core-the-spire/Core.TheSpire.Tests                # 旧后端规则检查，当前有既有失败
```

> `Core.TheSpire.Tests` 目前**首条失败**在 `CombatSliceTests.cs:498`（期望 `AttackEffect`，实得 `SequenceEffect`）。这是移动之前就存在的失败，已用移动前后的 A/B 运行证实与本次移动无关；该文件当时带有其他 agent 的未提交改动。

## 待决

`base-spire` 的 122 张卡是否、何时迁移到 `Engine/` 尚未决定。在那之前，本目录仍是原版内容的**唯一可运行实现**，"冻结"指的是**不再扩展**，不是"已经不参与运行"。
