# 纯 DSL base 首批切换与验证记录

日期：2026-10-18。任务：**CORE-AUTHOR-05**；当前认领和恢复顺序只维护在[主清单](../../tofix/README.md#当前交付)。本记录对应[规格 DB-R01～13](../../../design/mod-the-spire/engine-dsl-migration/spec.md)、[目录设计](../../../design/mod-the-spire/engine-dsl-migration/plan.md)和首批[任务 T00～T06](../../../design/mod-the-spire/engine-dsl-migration/tasks.md)。这是负责人实施与自查记录，不是独立评审。

## 交付与范围

生产默认 Host 和开发工具只加载 `mod-the-spire/base-spire/package.json`。包内只含 DSL 和静态 JSON，无 C#、程序集、项目或脚本；Engine 通用加载器编译同一 RuleSet。旧 C#、作者探针、游戏专用 Net 组与独立适配器保留在 tools 的迁移对照，不进入活动 solution 的 8 个项目依赖，也不作为运行回退。

首批可执行 scope 为 `base-spire-dsl-relic-draw-dev-v1`，包版本 `0.5.0-dsl.1`。它包含 Player／Card／Relic 实例、获得／移除与有效性过滤、开发牌堆实际转移和重洗、AfterShuffle／AfterCardDrawn 时序、无加成格挡和 TheAbacus 模板配置。不是正常 Ironclad 开局，不实现普通卡牌效果、自动出牌、正常战斗／房间／Run 或原版随机布局。其他旧 scope 与 legacy-slice／formal-dev 均在建会话之前明确拒绝。

目录按 cards、powers、relics、potions、enemies、events、characters、encounters、run 等领域定位；唯一入口显式 includes 索引。TheAbacus 定义位于 relics/shared/the-abacus.spire，共享生命周期及模板在 relics/rules，重洗与抽牌在 cards/rules。具体内容只允许原生 DSL 的 use 配置，实体、过程和订阅放到共享规则，越界、未知参数或表达式实参拒绝，不再保留独立 C# 效果解释。

## 基线与身份

工作基线为分支 `codex/relic-content-20261017` 的 `0e333be` 和已授权纳入本次审阅的未提交修改。设计前移动、TheAbacus C# 补全及旧 PotionBelt 容量断言失败均如实保存在[纠偏记录](base-dsl-migration-checkpoint-2026-10-18.md)，不把这些旧结果计作本次 DSL 通过。

本地环境：Windows／PowerShell，.NET SDK 9.0.306，Python 3.12。Engine 源语言 core/0.3；Engine 项目版本 0.4.0。包随机 profile 为 engine-default，实际为 `xoshiro256ss/splitmix64@1` 与 `rejection-uniform@1`，开发洗牌流 `base-spire:development-shuffle`。纯包行为固定 seed 20261018，通用 validate 启动 seed 42，Host 默认字符串 seed BASE-DSL-00；未将这些结果声明为原版随机等价。

最终 DSL 包摘要 `2fff0c90cf80043863ca91437de3123ea5d76273c894d96eab02aefcf6eb8d9c`，规则身份 `5d15e09b3bf25600`。摘要覆盖所有规范文本和静态数据，规则身份由实际编译产物给出；二者不包含局内 seed。记录绑定本次最终工作树，提交后可通过 PR 与 Actions 关联代码身份。

## 源范围与旧检查去向

| 静态源目录 | 源身份数 | 当前有 DSL definition |
| --- | ---: | ---: |
| cards | 578 | 0 |
| powers | 260 | 0 |
| relics | 297 | 1 |
| potions | 64 | 0 |
| enemies | 121 | 0 |
| events | 68 | 0 |
| characters | 8 | 0 |
| encounters | 88 | 0 |
| acts | 5 | 0 |

这些是本地恢复源码快照的身份数，包含尚未完成资格审阅的源对象，不等于单人可玩分母。27 个源池保留原有成员顺序；generic／mock 帮助项保留为 source-helper，不静默省略、不计可玩内容。原遭遇、幕池和幕计划的完整静态数据另以 source-data 快照保存；加载器不执行其中玩法。原 297 件遗物的逐源 SHA256 与旧冻结清单核对；当前 1 件行为见证、295 件未实现、1 件原有排除，原完整范围继续开放。

[冻结检查清单](base-dsl-check-disposition.json)逐个登记 153 个旧 C# 源组、原字面场景名、动态案例标记、原合同 ID、SHA256 和恢复去向；这些源组均标记当前未执行。11 份原 suite 的 case 定义保留；regression 中 7 处来源路径在前次目录整理时已重定位，case ID、seed、动作、期望维度和 17 项分母均未改变。原 regression 的 17 个 case ID 完整输出 unsupported，executed 为 0、independent_verified 为 false。清单 SHA256 用 UTF-8／LF 规范化文本，避免 Git 检出换行造成假差异；不能将源码扫描到的名称数当作已执行数。

通用 Net wire 与 text-rules 检查保留运行，新增纯 DSL 公共路径与真实 stdio 子进程；原 Snapshot／Control／Formal 等游戏专用组保留未迁移去向。旧示例项目显式返回退出码 3，旧作者脚本同样明确未支持。新的 migration CI 检查验证依赖隔离、旧源保留和失败语义，不能替代原完整对拍。

## 实际验证

在仓库根目录对最终生产代码串行执行以下检查：

| 命令 | 实际结果 | 证据与边界 |
| --- | --- | --- |
| dotnet build AutoTheSpire.sln --no-restore -v minimal | 通过，0 警告／0 错误 | .artifacts/base-layout/native-build-final.log |
| python tools/ci/check.py core --configuration Debug | 105 项通过 | 包含跨文件前向引用、循环、重复声明、源定位与拆分身份一致 |
| python tools/ci/check.py base --configuration Debug | 11 项通过；当前遗物见证 1 件 | 实际 DSL 加载、执行与范围拒绝，非独立原版对拍 |
| python tools/ci/check.py net --configuration Debug | 350 项通过 | 通用协议、公开加载及真实 stdio describe→catalog→start→step→observe |
| python tools/ci/check.py migration --configuration Debug | 通过，8 个活动项目／153 个旧源组／11 份原 suite | 内部实际运行原 regression，退出 3、零执行，未声称 parity |

对应原始日志在 `.artifacts/base-layout/*-final.log`，各组报告在 `.artifacts/ci/<组>/`，构建产物与长日志不提交。包行为包括：实际洗牌后加 6、非重洗与空堆不加、再次洗牌重复触发、移除和熔化后不触发、另一持有者隔离、满手牌与禁止抽牌不消耗洗牌随机、AfterCardDrawn 之前完成监听，以及另一份只改 DSL 参数的模板配置获得 8 点格挡。开发检查中的受控状态用于边界判别，不当作正常 Run 证据。

首次补充子集检查时漏引入 DslSourceException 的命名空间导致编译失败，已修复后重新执行集成门槛；未跳过检查。文档检查扫描 705 份 Markdown，0 问题；工作树与索引 diff --check 均返回 0。另实际核对 --require-complete 返回 1、原 regression 返回 3、旧作者示例脚本返回 3，保留功能与覆盖缺口。独立对拍、完整 Run、Python／AI 全套与性能未执行。

## 后续恢复的入口

接续先读主清单并选一个完整共享机制包，按源依据编写 DSL、通过领域索引注册并增加实际行为见证，再恢复依赖的内容与原回归意图。当前可复用加载、模板配置、实例、事件与公开投影；不需要重建 C# 工厂。正常 Run 和完整 297 件遗物等原 ENV/W 验收未关闭，不能用本次迁移门槛通过代替。
