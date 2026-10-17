# 公共组包解耦交付 — 2026-10-11

任务归属：`ENV-T06-package-decoupling-20261011`。本报告记录用户明确要求拆分`CombatSlicePackage.Build`后的实现与证据；当前状态、排期及接续只在[主清单T06](../../tofix/README.md#env-t06)维护。

## 接手入口

修改共享规则先查[编译模块参考](../../../design/mod-the-spire/package-compilation-reference.md)，按目标进入领域模块。新增房间先查[接入指南](../../../design/mod-the-spire/how-to-add-a-room.md)和`RoomCompilation.RoomServices`/`Rooms`；根组装只有新增依赖组或改变顺序时才需调整。既有源池、初始化与隔离商店成果保留；本包没有集成商店、补卡或改变引擎协议。后续仍按主清单已就绪包执行，本报告不新增派单。

## 实际修改

生产修改仅在`mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs`及新增`Slice/Compilation/`。公共Build的命名参数和默认值保留，通过`PackageBuildOptions`转交组装。原3936行文件成为84行兼容入口，268行`CombatPackageComposer`安排构建顺序和参数转发；具体IR生成由Schema、伤害、Power、敌人、回合、卡牌能力绑定、遭遇队列、换房、开局、奖励、结算及公开动作模块拥有。

`CompilationBindings`分别返回玩家、敌人、卡牌、牌区、Run资源、遗物、遭遇池和房间服务等符号。每个编译方法列明它消费的分组和回调，没有包含全包变量的通用容器。保留必需的互相调用能力，并将程序句柄声明和Body接线分成阶段；短回调适配函数留在根组装，实际规则不再隐式读取Build局部作用域。

`CompilationSlots`只分配编译期间的IR局部名称。格挡名称在玩家与敌人编译间共享，编队名称由换房编译拥有，状态机名称由敌人选择编译拥有。没有添加C#运行态、随机缓存、调度器或另一份RunState。

卡牌变量字段仍在地图服务之后、奖励声明之前补入；奖励仍在SettleBattle生成前就绪。声明、程序体生成和随机调用顺序保留。没有修改DSL语法、公开API、内容源或版本号；同配置的规则身份保持一致。

更新的使用文档包括base组织结构、开发指南、控制流程解释、房间指南/参考，新增编译模块参考。解释和参考分开；已有教程内容及空目录教学路径保留，房间最终例子和作者工具例子重新运行。

## 验证与证据

实际构建输出位于`.artifacts/parallel/package-decoupling`，证据在[本包审计目录](../../../../.audit/base-package-decoupling-2026-10-11/source-manifest.json)。复现命令中的DLL使用独立构建目录，避免与其他会话共享obj/bin。

| 检查 | 实际结果 | 证据 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln --artifacts-path .artifacts/parallel/package-decoupling` | 0错误；首次全量打印4项已有nullable警告，最终增量打印3项；本包没有新增编译警告 | [最终构建](../../../../.audit/base-package-decoupling-2026-10-11/build-final.log)、[首次全量](../../../../.audit/base-package-decoupling-2026-10-11/build-solution.log) |
| `dotnet .../Core.TheSpire.Engine.Tests.dll` | 99/99通过 | [Engine日志](../../../../.audit/base-package-decoupling-2026-10-11/engine.log) |
| `dotnet .../BaseSpire.Slice.Tests.dll` | 759/759通过，未新增按文件名/私有实现断言的检查 | [完整Slice日志](../../../../.audit/base-package-decoupling-2026-10-11/slice.log) |
| 最后将编队命名计数移入换房模块后，`--encounter-queue-only` | 7/7通过；该整理后真实公开轨迹再次与基线相同 | [局部日志](../../../../.audit/base-package-decoupling-2026-10-11/queue-final.log) |
| dev及dev-map的`--describe-build` | 两配置完整描述相同，包括注册数量、内容/语义摘要、规则与公开身份 | [身份比较](../../../../.audit/base-package-decoupling-2026-10-11/build-identity-comparison.json) |
| 正常公开Run，`ACT1-BASIC-10`、dev-map、600动作上限、`--use-potions` | 前后124通信帧、59步骤记录逐项相同；只去掉request_id/session_id | [比较结果](../../../../.audit/base-package-decoupling-2026-10-11/behavior-comparison.json)、[比较脚本](../../../../.audit/base-package-decoupling-2026-10-11/compare_public.py)、[最终公开日志](../../../../.audit/base-package-decoupling-2026-10-11/public-final.log) |
| `dotnet run --project docs/examples/base-room/BaseRoomExample.csproj --artifacts-path .artifacts/parallel/package-room-example` | Gold10→17，MapPhase5→2，remaining_actions=0 | [房间示例](../../../../.audit/base-package-decoupling-2026-10-11/room-example.log) |
| 构建BaseSpire.Dev后指定`BASE_SPIRE_DEV_DLL`运行`verify_examples.py` | 15/15调用通过；包含实际加载、基础/升级、hook及拒绝分支 | [作者例子](../../../../.audit/base-package-decoupling-2026-10-11/author-examples.json) |
| `dotnet .../Net.TheSpire.Tests.dll` | 415通过/1失败/416执行，仍是F-05跨会话候选绑定原失败 | [net日志](../../../../.audit/base-package-decoupling-2026-10-11/net.log) |
| `python tools/doc-check/check_docs.py` | 本包链接有效；全仓仍有4项原有#L锚点错误 | [文档日志](../../../../.audit/base-package-decoupling-2026-10-11/doc-check.log) |
| `git diff --check`及新增文件空白核对 | 通过；Git打印已有文件行尾转换提示 | 交付检查 |

正式公开身份仍为`net:ruleset:4fa58ee1b2f76e3f`、`net:catalog:b400d7f332f590cc`。目录内保留原文件备份和最终源码SHA256清单；源码备份用于比对，不用于覆盖共享工作区。

## 首差与未验证范围

正常Run完成58次提交、三场战斗及奖励后，在第59次尝试进入Fogmog房间时以原有未支持敌人行为故障停止，返回码4。前后失败、生命68/80、金币137及观察4128相同；这证明拆分保持行为，不能计为通关第一幕或完整原版。没有缩小源遭遇池、换seed或更新夹具掩盖故障。

net F-05跨会话绑定仍开放。文档检查的4项原错误来自`dsh-power-fixture-handoff-2026-10-11.md`和`dsh-startup-alignment-handoff-2026-10-11-f68b943d.md`中的#L链接，没有修改其他会话交接。

本包没有重做独立原版双侧、完整五角色环境或性能/上下文用量测量，也没有自动房间发现和扩展安装器。模块内部仍有较长的换房/敌人IR生成，保留了实际规则依赖；此次验收只关闭本解耦小包，不关闭父T06/T08/T09或既有玩法缺口。

## 文档跟进（2026-10-11，用户追加要求）

只同步文档说明：总入口和base README补编译模块导航；A角色提示改为按领域进入`Compilation`；规格、效果IR/编排工作包和卡牌边界修正声明、绑定、队列与永久提交的维护位置。保留旧步骤的初次交付位置，并注明拆分后的维护入口，不重写日期证据。

开发指南新增“新增共享状态字段”：声明→Bindings返回→初值和寿命→消费者→行为核对；房间指南明确`RoomServicesBindings`及奖励回调的归属。声明字段不会自动安排重置，这个前提单独写入指南和参考。新依赖组或构建顺序变化才修改根组装，已有分组新增成员无需在根逐字段补参数。

Diataxis复核：组织结构声明与实际均为Explanation，服务理解依赖和职责；开发指南均为How-to，服务已有语义依据的修改任务；编译模块参考均为Reference，服务符号/API查找。发现1处混用：参考页“扩展已有分组”原含修改步骤，读者需要在查接口时自行拼操作顺序；改为中性的符号/寿命说明，将完整步骤放入开发指南并互链。教程路线未改，没有新增未实跑的教学例子。Diataxis分析完成，NBJ核对术语、前提、源依据与链接。

本轮13份使用/设计/导航文档的改动清单及逐文件差异在[文档审计目录](../../../../.audit/package-doc-sync-2026-10-11/changes.json)。`python tools/doc-check/check_docs.py`扫描408份，仍仅4处既有#L锚点错误；本轮新增链接/锚点有效。`git diff --check`及本轮文件空白核对通过。未改生产代码，未运行构建、规则套件或重复教程执行；本次文档维护完成，不新增生产任务。
