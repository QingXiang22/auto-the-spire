# core/0.3 同包文本绑定参考

同包文件组合、严格纯文本 manifest 与静态源目录见[包格式参考](package-reference.md)。


文档类型：参考（Reference）。core/0.3 在 [core/0.2 规则库](../0.2/authoring-guide.md)基础上支持宿主同包绑定与具名调用；基础语法见 [core/0.1](../0.1/language-reference.md)。活动引擎身份为 `engine/ir@0.5`，Core.TheSpire 包版本为 `0.5.0`。旧冻结版本文件保留为历史合同，不表示活动产物仍为旧身份。

## 绑定入口

```csharp
EngineDslCompiler.BindInto(source, builder, sourceName, dependencies, extensions);
var package = builder.Build();
```

BindInto 将文本声明绑定到已有 EnginePackageBuilder 的宿主类型与符号；namespace 必须与 builder 的 PackageId 相同。builder 单次 Build 冻结后不可再绑定。它不生成第二个执行器；过程调用、事件和状态均使用同一 typed IR 和 Session。

LoadFiles 的 extensions/dependencies 重载支持扩展感知加载；`core/0.3` template 实例可使用注册的扩展声明。缺依赖、符号冲突、无效类型或参数仍由编译/链接拒绝。显式 `core/0.1`、`core/0.2` 输入继续接受，前端身份按输入版本记录为 `core-dsl/0.1`、`core-dsl/0.2` 或 `core-dsl/0.3`，并计入模板及扩展身份。依据为 [EngineDslCompiler](../../../../core-the-spire/Core.TheSpire/Engine/Compilation/EngineDslCompiler.cs)；`BindInto` 会把文本前端身份追加到目标构建器身份。

## 具名调用

`call = { procedure = Grant args = { owner = owner amount = 2 } }` 按参数名绑定；pureCall 同样支持具名参数。缺参、未知参数及重复名拒绝，不以对象枚举顺序猜位置。参数仍有明确类型，Ref(Player) 与 Ref(Power) 不可互换。

宿主须显式传入 owner/card/target/power 等上下文；文本名称本身不会自动获得游戏状态。base 的实际应用见[生命周期文本库](../../../../tools/base-migration-baseline/BaseSpire/Authoring/Content/lifecycle-rules.spire)与[base 作者契约](../../0.0.2/authoring-reference.md)。

## 随机流与 binary32

下列能力随 engine/ir@0.5 交付，仍使用 core/0.3 文本版本。gameSeed() 返回 Session 的不可变字符串输入；未指定文本时为数值 seed 的 invariant 十进制文本。seed 不进入静态规则身份；显式文本输入的摘要进入 Session 身份。

| 写法 | 类型与行为 |
| --- | --- |
| gameSeed() | 无参，返回 String；宿主须传每局原始文本，不隐式归一化 |
| hash32(text) | String → Int(0..4294967295)，UTF-16 code unit 双累加器哈希：初始352654597、逐字符乘33异或，最终 left + right ×1566083941，unchecked uint32 |
| seedStream = { stream = name seed = value } | 非空字符串流名、非负 Int seed；首次抽取前设置，重复设置或已消费后设置失败；不消费 RNG，pure 过程禁止 |
| sampleFloat32 = { stream = name into = value } | value 为 Int 槽；一次 DrawDouble 后舍入至 binary32，返回0..4294967295的IEEE754位型；不经Fixed量化，pure过程禁止 |
| float32(value) | Int／Fixed 数值转换成binary32位型；不是把输入Int当位型解读 |
| float32Add(left,right)、float32Minimum(left,right) | 两个Int位型操作数，返回舍入后的binary32位型 |
| float32Less(left,right)、float32LessOrEqual(left,right) | 两个Int位型操作数，返回Bool；位型越界、NaN或无穷拒绝 |
| sort(query,item,key,descending=false,stable=true) | 默认稳定排序保持旧行为；stable=false显式使用 .NET Array.Sort 非稳定排序，同键不以实例ID补序；该选项进入规则身份 |

binary32 位型不能用普通整数加减或大小比较代替浮点运算。所有抽样由同一 RandomService 计数；Capture／Restore 保留尚未消费流的显式种子，不是完整暂停 Session 恢复。sampleInt 继续是半开区间，shuffle 继续是降序 Fisher-Yates；原版预排序由游戏包组合。

本版修正 vanilla profile 的 DrawBool 为源 NextInt(2)==0；engine-default 映射不变。由于该语义修正及新IR原语，旧 engine/ir@0.4 包须重建，旧回放保留原身份；不要只改产物标签。

## 错误定位与身份

生成 IR 可递归附加 IrSource.At；更精确子节点位置保留，源位置不参与语义身份。身份变化需要核对最终规则、构建描述及公开 fixture，再迁移 golden；不能用重新抄 hash 掩盖行为失败。非 Running（包括 Paused）不发布 LegalActions，以避免暂停时 pure guard 干扰待决续接。
