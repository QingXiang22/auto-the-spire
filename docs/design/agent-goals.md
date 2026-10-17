# 当前 agent goal：core 冻结后的下游分工

本页提供可复制的角色接手提示。先按[主清单领域总览](../review/tofix/README.md#当前任务)定位工作，按[归属与依赖](../review/tofix/README.md#归属与依赖)及[推进顺序](../review/tofix/README.md#推进顺序)接续；原ENV编号保留为认领、完成条件与证据索引，旧P值不单独驱动派单。本页不另指定当前首包。范围与完整验收见[管理约定](post-core-agent-assignments.md)、[ENV范围](environment-completion-and-differential-v1.md)及[实现约束](environment-implementation-constraints.md)。导航更新于2026-10-17。

## 1. 派给集成执行者的一段goal

在当前共享工作区接续用户指定的 ENV 任务。先在项目根目录运行 `git status --short`，读根/目标 `AGENTS.md` 和[接手路径](../README.md#接手路径)。从主清单选择本线已授权、已就绪的一包，核对已有交付，登记会话/目录/小包/下一成果后实施；不从历史报告恢复旧派单。

按[当前交付](../review/tofix/README.md#当前交付)完成整组内容，实际基线遵循[核心契约](../contracts/core-engine-baseline.md)。组内只按需编译／加载，整组写完后本地默认跑受影响单元测试和少量固定种子对拍，广覆盖回归交给 CI；CI 失败后定向修复，只复跑相关本地检查。长程公开／矩阵／性能留对应里程碑，不搜索seed。准确取舍见[检查节奏](../review/tofix/README.md#每批集成与重排条件)。缺参考保留对应待验证，结果回写唯一当前交付，历史证据放records。不改core／参考玩法，不恢复训练或自动派agent。

## 2. 完成与阻塞

每线同时最多一个完整机制包进行中，公共文件与构建串行协调。待验证不等于已验收；阻塞注明依赖 ID、最小复现、受阻 case 和可继续事项。父项关闭、优先级/范围改变、core 维护、参考窄裁决和候选验收由负责人决定。

交接前核对：读者是否能从正文及直接链接找到目标/状态、可复用成果、第一步、完成条件/验收者、阻塞/未验证和维护位置。格式见[交接约定](post-core-agent-assignments.md#04-交接与变更格式)。

## 3. A：base-spire 编码 agent

交付原版规则与真实 Run，让公开 Host 和独立对拍使用同一工厂。

房间机制包先用[房间操作指南](mod-the-spire/how-to-add-a-room.md)和[房间接口参考](mod-the-spire/room-compile-reference.md)，按派单提供的准确接口开工。公共机制按[编译模块参考](mod-the-spire/package-compilation-reference.md)进入`Slice/Compilation`的目标模块；房间服务/注册查`RoomCompilation`，新依赖组或顺序变化才查`CombatPackageComposer`。`CombatSlicePackage.Build`保留调用兼容性，不再存放具体机制实现。

按[base开发指南](mod-the-spire/base-development-guide.md)选择本包修改入口和局部反馈；组合已有效果先从内容和案例入手，扩展机制再进入领域前端/程序/编译器。公开或完整环境门槛仍按下列合同执行。

- **边界**：只写 `mod-the-spire/base-spire/`、必要 mod 文档及 solution 登记；遵守 mod AGENTS 和[冻结契约](../contracts/core-engine-baseline.md)。不改 B/C 生产文件。
- **先读**：[主清单 A 队列](../review/tofix/README.md#当前任务)、[base 工作包](mod-the-spire/base-spire-implementation-plan.md#3-当前可直接派给-a-的接续包2026-10-06)、[A 完整合同](post-core-agent-assignments.md#3-abase-010203正式规则包与完整原版)。
- **第一步**：核对本包现有工厂、前端/IR 和已交付检查，按任务详情的“接手先做”实施；复用状态机/队列/编队/出生能力/R1～R3，避免重做或复制另一份 RunState。
- **交付判断**：按原完成条件给真实可达能力、普通/升级/适用分支、源依据、实际命令和同构建证据。文本规则库要实际链接执行；正常 Run 不注入状态，尚未完整实现的范围保持 dev。B/C 随包接入，不等全量内容完成。
- **维护**：只回写本任务详情和综合交接；源时序/写回方法更新相应工作包/spec，随改随修 UTF-8 注释债。P2 按真实路径依赖接入，不无限清长尾而推迟 Run。

## 4. B：严格对拍与结算 agent

让每次规则交付都有实际独立双侧执行和可复现的首个差异，而非只有绿色本地检查。

- **边界**：只写 `tools/differential/`、必要 rng-oracle、suite/参考适配及证据；统筹串行集成构建，不改 A/C、core、参考玩法或 AI。
- **先读**：[主清单 B 队列](../review/tofix/README.md#当前任务)、[B 完整合同](post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)、[工具 README](../../tools/base-migration-baseline/differential/README.md)和涉及的实现约束。
- **第一步**：定位既有 Comparator/FixtureSuite/两侧适配，核对本包与公开 Host 的构建/配置/seed，再接 A 已有工厂；参考独立执行，不能复制 base 结果。
- **交付判断**：保留必测/unsupported 原 ID 与完整比较维度；缺 case、零执行、缺参考、异常、超时或首个差异都不记通过。源随机差异按原始 seed 和独立依据裁决，不补抽、换 seed 或改参考效果。
- **维护**：本包状态更新主清单；case、源分母、首个差异和 manifest 在工具资产/证据中维护。阶段与候选按合同绑定同一 run_id，覆盖不扫描历史字符串计算，早死或旧 compare 返回 0 不抵完整验收。

## 5. C：net-the-spire 编码 agent

编写客户端先读[公开运行教程](net-the-spire/how-to-run-public-game.md)和[客户端接口参考](net-the-spire/public-client-reference.md)，复用公开快照访问模块；只有本包确有公开契约缺口时再查Host对应实现。

让公开客户端从同一正式工厂正常开局、操作实际合法选择并取得当前可见事实。

- **边界**：只写 `net-the-spire/`、`docs/contracts/net/0.2/`及 net 文档；不重算玩法，不改 A/B、core 或训练。
- **先读**：[主清单 C 队列](../review/tofix/README.md#当前任务)、[C 完整合同](post-core-agent-assignments.md#5-cnet-0102完整公开出口)、[协议第 7 节](net-the-spire/protocol.md#7-env正式环境的02扩展)及[声明派生实施边界](net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续)。
- **第一步**：核对当前认领及已交付接入，接 A 已配置但未 Start 的独立 Session，仅启动一次；不重新建立默认随机 Session。声明派生先使用现有公开结构，不增加逐项游戏注册。
- **交付判断**：C#/Python/schema、身份和载荷证据同批更新，保留旧 0.1 数字 seed。每局实际字符串 seed、重发幂等、同 Host 多 seed 与完整候选按合同验证；未支持形状明确处理，不截断或静默改义。未知/RNG/debug 不进入公开输入，已知牌实例及绝对位置按投影提供。
- **维护**：本包状态与证据更新主清单；协议/schema/codec 变化更新契约及必要迁移。阶段验收交 Python 同正式 Host 的全程轨迹，与 B 同构建/配置/seed 互证，局部检查不抵完整胜负。

## 6. D：真实客户端核对 agent，后排

仅在用户/负责人明确派发 CLIENT-01 / ENV-T15 后执行。先查[任务条件](../review/tofix/README.md#env-t15)，确认真实版本、构建、解锁、seed 和语义选择；只写本线取证工具/记录。

采集或导入正常胜负及重点机制记录，按 B 规范逐步比较；可用录制，不先建大型 UI 平台。缺客户端或匹配记录时写明阻塞/未验证，不用源码审计代替实测，不为对齐改 base/core。

## 7. E：指定 mod 编码 agent，后排

仅在用户指定 mod 名称/版本/范围并派发 MOD-01 / ENV-T16 后执行。先查[任务条件](../review/tofix/README.md#env-t16)，确认目录、依赖、加载顺序及游戏/mod 身份。

使用共同接口和 DSL 交付从加载、真实决策到终局的完整包，与真实游戏加载同 mod 的记录比较并保留原版回归；参考仅在明确支持时辅助。core 通用缺口附最小复现交负责人，不自动扩展为任意 mod 或启动 AI。
