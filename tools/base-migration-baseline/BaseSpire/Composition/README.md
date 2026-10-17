# 规则包组装

查找或修改规则包组装时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [CombatSlicePackage.cs](CombatSlicePackage.cs) | 保留既有 Build 调用入口 |
| [FormalSpirePackage.cs](FormalSpirePackage.cs) | 正式规则包构建入口 |
| [CombatPackageComposer.cs](CombatPackageComposer.cs) | 声明顺序与领域模块组合 |
| [CompilationBindings.cs](CompilationBindings.cs) | 类型化符号分组 |
| [CompilationCallbacks.cs](CompilationCallbacks.cs) | 跨领域编译回调 |
| [CompilationSlots.cs](CompilationSlots.cs) | IR局部量分配 |
| [PackageBuildOptions.cs](PackageBuildOptions.cs) | 组包选项 |
| [SliceManifest.cs](SliceManifest.cs) | 兼容切片的定义与身份 |

行为检查随对应领域的实际消费者维护。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
