# core/0.3 同包文本绑定参考

文档类型：参考（Reference）。core/0.3 在 [core/0.2 规则库](../0.2/authoring-guide.md)基础上支持宿主同包绑定与具名调用；基础语法见 [core/0.1](../0.1/language-reference.md)。活动引擎身份为 `engine/ir@0.4`，Core.TheSpire 包版本为 `0.4.0`。旧冻结版本文件保留为历史合同，不表示活动产物仍为旧身份。

## 绑定入口

```csharp
EngineDslCompiler.BindInto(source, builder, sourceName, dependencies, extensions);
var package = builder.Build();
```

BindInto 将文本声明绑定到已有 EnginePackageBuilder 的宿主类型与符号；namespace 必须与 builder 的 PackageId 相同。builder 单次 Build 冻结后不可再绑定。它不生成第二个执行器；过程调用、事件和状态均使用同一 typed IR 和 Session。

LoadFiles 的 extensions/dependencies 重载支持扩展感知加载；`core/0.3` template 实例可使用注册的扩展声明。缺依赖、符号冲突、无效类型或参数仍由编译/链接拒绝。显式 `core/0.1`、`core/0.2` 输入继续接受，前端身份按输入版本记录为 `core-dsl/0.1`、`core-dsl/0.2` 或 `core-dsl/0.3`，并计入模板及扩展身份。依据为 [EngineDslCompiler](../../../../core-the-spire/Core.TheSpire/Engine/Compilation/EngineDslCompiler.cs)；`BindInto` 会把文本前端身份追加到目标构建器身份。

## 具名调用

`call = { procedure = Grant args = { owner = owner amount = 2 } }` 按参数名绑定；pureCall 同样支持具名参数。缺参、未知参数及重复名拒绝，不以对象枚举顺序猜位置。参数仍有明确类型，Ref(Player) 与 Ref(Power) 不可互换。

宿主须显式传入 owner/card/target/power 等上下文；文本名称本身不会自动获得游戏状态。base 的实际应用见[生命周期文本库](../../../../mod-the-spire/base-spire/BaseSpire/ContentSource/lifecycle-rules.spire)与[base 作者契约](../../0.0.2/authoring-reference.md)。

## 错误定位与身份

生成 IR 可递归附加 IrSource.At；更精确子节点位置保留，源位置不参与语义身份。身份变化需要核对最终规则、构建描述及公开 fixture，再迁移 golden；不能用重新抄 hash 掩盖行为失败。非 Running（包括 Paused）不发布 LegalActions，以避免暂停时 pure guard 干扰待决续接。
