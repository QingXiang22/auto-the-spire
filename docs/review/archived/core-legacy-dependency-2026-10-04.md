# core Legacy 编译依赖归档：关闭记录

日期：2026-10-04。用户明确要求当前架构退出 Legacy 并移动到 archive；本报告仅关闭旧后端依赖隔离问题，不关闭通用 DSL、拆分、冻结或完整原版环境。

## 归档前现象

Core.TheSpire SDK 默认编译 Legacy；base 同时编译旧注册器和新 Engine 内容；旧 Core 测试/test-hook-mod/两个工具消费旧类型；differential inventory 调旧注册器；Slice 套件还有 4 项新旧前端兼容检查。仅移动 Legacy 文件夹不能消除这些编译依赖。

## 本次处理

源码与直接消费者移到[archive/legacy-sts](https://github.com/QingXiang22/auto-the-spire/blob/0aca7d1a26f3ad42d5b8657bc794cd0b12f85ba8/archive/legacy-sts/README.md)。旧 core/base 为独立 Legacy 程序集；活动 solution 移除旧测试和 hook 项目，活动 base 只编译新路径。归档内容源为快照，正式 ContentSource 留原处。旧检查保留归档前未提交修改。

Slice 的 4 项兼容检查移为归档可执行工程，活动 Slice 268→264；检查没有删除。通用 DslSource AST/解析器留活动 core，归档可以反向复用。differential inventory 不引用/运行旧注册器，历史注册段标记 archived_not_run、计数为 null；正式匹配直接使用前端按类别产生的 ID，不再需要旧注册器提供连接键。空历史匹配不是实测零。

依赖检查增加活动工程不得引用 archive 的门槛；Legacy 类型扫描从归档读，仍用于发现活动路径误引用。

## 实测

| 命令/范围 | 结果 |
| --- | --- |
| dotnet build AutoTheSpire.sln | 0 错误、0 警告 |
| Core.TheSpire.Engine.Tests | 69/69 |
| BaseSpire.Slice.Tests | 264/264 |
| Net.TheSpire.Tests | 393/393 |
| archive 旧 Core.TheSpire.Tests | 190 passed / 0 failed / 190 executed |
| archive BaseSpire.Compatibility.Tests | 4/4 |
| dependency-check（含不引用 archive） | 10/10；14 个活动项目无环，44 个新路径文件未发现旧类型引用 |
| differential inventory | rc=0；正式卡 68、Power 25；归档注册段未运行 |
| doc-check | 190 份 Markdown，0 问题 |

初次活动构建发现 Slice 兼容检查仍引用 Legacy，已将完整检查及原助手快照归档后复测；初次归档旧测试因 friend assembly 声明留活动 core 而编译失败，已将声明移归档旧程序集后复测。未通过忽略错误或重新引入活动 Legacy 依赖解决。

## 限制与后续

新环境仍未覆盖完整原版。停止当前旧注册器统计不代表旧内容数为零，也不把旧测试通过计为 Engine 内容完成。ANTLR/文本 DSL、Session 拆分、冻结产物和调试模式按[当前清单](../tofix/README.md)实施，本报告不宣称这些已经实现。AI/client 全量学习任务和两个归档审计工具的执行本轮未重跑。
