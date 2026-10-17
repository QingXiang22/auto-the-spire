# 项目文档总入口

审阅剩余工作时，打开[领域总览](review/tofix/README.md#当前任务)了解各类范围；review最近一批请直接看[当前交付](review/tofix/README.md#当前交付)、[推进顺序](review/tofix/README.md#推进顺序)和[每批集成](review/tofix/README.md#每批集成与重排条件)。[归属与依赖](review/tofix/README.md#归属与依赖)指定主要归属，原ENV编号保留为合同与证据索引。当前状态与排期只在主清单维护。

导航整理：2026-10-17。任务范围、core冻结和AI实验安排以主清单及相关契约为准。

构建与持续集成从[core/base与对拍检查](../tools/ci/README.md)进入；原版源码检查留在本地。

## 本地开发入口

在仓库根目录使用 PowerShell 执行下列文档中的命令。.NET SDK 必须匹配 [global.json](../global.json) 的 `9.0.306`（禁用版本滚动）；Python 检查入口按现有 CI 使用 `3.12`。正常构建不依赖本地原版源码 `sts2/`，读取原版源码的检查另有前置条件。

| 要完成什么 | 入口与成功判断 |
| --- | --- |
| 运行一个不依赖原版内容的小游戏 | [core README](../core-the-spire/README.md#先跑一个小游戏)：分数到 3 后输出 `tutorial:ReachedTarget` |
| 构建和验证 core/base | [CI 检查入口](../tools/ci/README.md)：按修改范围选组，退出码为 0，日志与 `result.json` 写入 `.artifacts/ci/<组名>/` |
| 区分运行时检查与本地源码核对 | [base README](../mod-the-spire/base-spire/README.md)：`--suite runtime` 不读原版源码，`--suite source` 需要同版本 `sts2/`，默认完整组 |
| 准备严格对拍的独立参考 | [参考依赖说明](../reference/README.md)：按锁定提交获取上游，再按 CI 入口执行；完整原版验收仍按主清单 |
| 修改开发文档 | 在根目录运行 `python tools/doc-check/check_docs.py` 和 `git diff --check`；两者均须返回 0 |

## 接手路径

1. 在项目根目录运行 `git status --short`，读根/目标目录的 `AGENTS.md`。继续现有共享工作区；core 标签只锚定冻结版本，不能用检出标签覆盖未提交成果。
2. 打开[任务索引](review/tofix/README.md#当前任务)，进入本次任务的详情。核对认领、依赖、可复用成果、下一项可验证的结果和完成条件；先确认已有交付，再继续一个已授权且就绪的完整机制包。
3. 按下表读取自己的角色入口和必要工作包。生产实现开工前在主清单对应任务详情登记会话、目录、当前小包和下一成果；只整理文档则登记维护记录。
4. 按任务详情的“接手先做”执行，并保留实际命令、构建/配置/seed、结果和证据。交付时只更新对应条目，长日志留证据目录；历史报告通过[records索引](review/records/README.md)按需查证。

| 接手角色 | 先读哪里 | 用来确认什么 |
| --- | --- | --- |
| 负责人 | [管理约定](design/post-core-agent-assignments.md)、清单链接的最近复核 | 当前决策、证据缺口、跨线边界及验收权限 |
| A：base 规则与流程 | [A 提示词](design/agent-goals.md#3-abase-spire-编码-agent)、[base 工作包](design/mod-the-spire/base-spire-implementation-plan.md#3-当前可直接派给-a-的接续包2026-10-06) | 本包文件落点、源语义和行为验收 |
| B：严格对拍 | [B 提示词](design/agent-goals.md#4-b严格对拍与结算-agent)、[B 合同](design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍) | 双侧独立执行、严格失败和证据结算 |
| C：公开出口 | [C 提示词](design/agent-goals.md#5-cnet-the-spire-编码-agent)、[C 合同](design/post-core-agent-assignments.md#5-cnet-0102完整公开出口) | 同工厂接入、协议兼容及公开全程 |
| D/E：客户端或指定 mod | [D 提示词](design/agent-goals.md#6-d真实客户端核对-agent后排)、[E 提示词](design/agent-goals.md#7-e指定-mod-编码-agent后排) | 独立派单的版本、范围与取证条件 |

负责人接手：读当前清单、[管理与分工约定](design/post-core-agent-assignments.md)及清单链接的最近复核证据，承担决策/审查/文档维护；生产编码和派出agent按用户明确安排。

## 使用 core 的文档路径

按 [Diátaxis](https://diataxis.fr/) 区分阅读需要。首次使用从教程开始；完成已有任务、查字段和理解原因各有对应入口。

| 需要 | 文档 |
| --- | --- |
| 跟着做第一次 | [写规则教程](dsl/core/first-rule.md)、[写宿主教程](design/core-the-spire/first-host.md) |
| 完成具体任务 | [配置计分规则库](dsl/core/how-to-configure-game.md)、[在宿主中提交动作参数](design/core-the-spire/how-to-submit-parameters.md) |
| 查准确资料 | [语言参考](dsl/core/0.1/language-reference.md)、[同包绑定与具名调用](dsl/core/0.3/authoring-reference.md)、[交互API参考](design/core-the-spire/session-api-reference.md) |
| 理解概念与原因 | [规则与宿主的关系](design/core-the-spire/rules-and-host.md)、[架构决定](design/core-the-spire/core-architecture-decisions.md) |

## 编写和扩展规则的操作指南

基于base开始修改时，先做[第一张卡](design/mod-the-spire/first-base-card.md)或[第一个房间动作](design/mod-the-spire/first-base-room.md)；两篇教程都从空目录开始，附逐步可见结果。理解目录和编译/运行分工见[base组织结构](design/mod-the-spire/base-architecture.md)。

| 需要完成什么 | 指南 |
| --- | --- |
| 把机制写成可维护的规则集/规则库 | [编写可维护规则集](design/mod-the-spire/maintainable-rule-sets.md) |
| 在已有规则上组合卡牌、验证命名空间与引用 | [modding指南](design/mod-the-spire/modding-guide.md) |
| 接续base任务、定位修改入口并快速验证 | [base开发指南](design/mod-the-spire/base-development-guide.md) |
| 查询公共规则包构建模块、符号分组及声明/接入顺序 | [编译模块参考](design/mod-the-spire/package-compilation-reference.md) |
| 实现地图房间、绑定长期资源并关闭访问 | [房间操作指南与可运行例子](design/mod-the-spire/how-to-add-a-room.md)、[编译接口参考](design/mod-the-spire/room-compile-reference.md) |
| 编写普通事件并复用资源操作/选择生命周期 | [事件领域契约](design/mod-the-spire/event-domain/spec.md)、[事件作者操作步骤](design/mod-the-spire/how-to-add-a-room.md#新增一个具体事件已有能力) |
| 复用金币、生命、遗物和永久牌组操作；组合卡牌原语 | [整局公共操作契约](design/mod-the-spire/run-domain/spec.md)、[公共能力入口](design/mod-the-spire/room-compile-reference.md#按操作查找已有公共能力)、[卡牌原语组合](design/mod-the-spire/card-primitives/spec.md) |
| 逐张校对五角色原版卡名、普通/升级描述与实现证据 | [五角色卡牌清单（2026-10-12快照）](review/records/2026-10-12/five-character-card-catalog/README.md) |
| 从公开接口启动游戏、读取快照并提交候选 | [公开运行操作指南与最小客户端](design/net-the-spire/how-to-run-public-game.md)、[客户端接口参考](design/net-the-spire/public-client-reference.md) |
| 亲自用键盘选择地图、出牌和操作房间 | [手动试玩 base-spire](design/net-the-spire/how-to-play-base-game.md) |

## 文档各管一件事

| 文档 | 唯一职责 | 何时读/更新 |
| --- | --- | --- |
| [当前清单](review/tofix/README.md) | 领域分类、当前顺序、主责／依赖／验收及完整ENV/W范围 | 总览维护领域缺口与排期；原ID详情保留认领、依赖与完整完成条件 |
| [管理与分工](design/post-core-agent-assignments.md) | 优先级规则、目录归属、跨线接口、状态与交接格式 | 负责人必读；跨线协作时查 |
| [角色提示词](design/agent-goals.md) | 如何接单、按本线哪些文档执行 | 只读对应角色；不复制第二套进度 |
| [base实施编排](design/mod-the-spire/base-spire-implementation-plan.md) | 机制工作包、实现依赖、源时序/算法约束 | A执行对应包时读；方法变更时修订 |
| [ENV范围](design/environment-completion-and-differential-v1.md) / [实现约束](design/environment-implementation-constraints.md) | 产品范围、源基线、必测case、身份/状态/证据与完整验收合同 | 执行前按涉及范围读；不因优先级重排缩减合同 |
| `review/records/YYYY-MM-DD/` | 带日期的交付/复核/决策依据、历史规划与原始追加 | 按需查证；正文里的“下一步”不覆盖当前清单 |
| `review/archived/` / `review/deprecated/` | 已有关闭证据的窄缺陷 / 因范围变化失效的报告 | 查关闭范围；未解决项不能当作已修归档 |

## 按任务读取规范

需要把当前工作中的有边界小任务交给远程DS时，使用[DeepSeek Harness委派操作指南](design/deepseek-harness-operations.md)；任务范围和验收仍按主清单，不把插件镜像任务当模型委派。

| 范围 | 主规范/技术入口 |
| --- | --- |
| 项目目标与模块边界 | [项目spec](design/spec.md)、[文档协作约定](AGENTS.md) |
| core宿主API与冻结语义 | 先做[第一次写宿主](design/core-the-spire/first-host.md)；查[开发者指南](design/core-the-spire/developer-guide.md)、[冻结契约](contracts/core-engine-baseline.md)、[Git交接](contracts/core-git-checkpoint.md) |
| base内容与Run | [mod spec](design/mod-the-spire/spec.md)、[base README](../mod-the-spire/base-spire/README.md)、[base实施编排](design/mod-the-spire/base-spire-implementation-plan.md)；历史Ironclad覆盖不是完整分母 |
| 内容DSL | 新卡牌从 [base/0.0.2 作者参考](dsl/0.0.2/authoring-reference.md)进入；已有领域效果查[0.0.1作者约定](dsl/0.0.1/dsl-usage.md)；旧兼容/实现细节查[content-dsl](dsl/0.0.1/content-dsl.md)、[战斗设计](dsl/0.0.1/combat-dsl-design.md) |
| core文本规则库 | 先做[第一次写规则](dsl/core/first-rule.md)；[作者入口与配置入门](dsl/core/README.md)、[0.3同包绑定参考](dsl/core/0.3/authoring-reference.md)、[0.2规则库指南](dsl/core/0.2/authoring-guide.md)、[0.1语言参考](dsl/core/0.1/language-reference.md) |
| B严格对拍 | [工具README](../tools/differential/README.md)、ENV范围/实现约束；参考默认只读，`sts2/`源码及同版本客户端优先裁决 |
| C公开出口 | [net spec](design/net-the-spire/spec.md)、[协议及0.2扩展](design/net-the-spire/protocol.md)、[公开契约资产](design/net-the-spire/contract-artifacts.md) |
| AI兼容/后续学习 | [AI spec](design/ai-the-spire/spec.md)、[策略契约](design/ai-the-spire/policy-contract.md)、[训练设计](design/ai-the-spire/training-design.md)、[共同验收](design/ai-net-verification.md)；是否启动实验只查主清单 W-07，旧阶段证据按需查看 |

## 维护方式

更新文档时，先确认事实应当维护在哪里：当前状态写主清单，验收定义写相应规范，实现步骤写对应工作包，历史过程写 records。其他页面用链接引用。

新增文档前，先判断现有页面能否承载。执行者按机制族或阶段提交一份综合交接；负责人将开放项并入清单后，再把交接转入 records。移动目录或文件后，修复所有引用并运行文档检查。详细规则见[管理约定](design/post-core-agent-assignments.md)。
