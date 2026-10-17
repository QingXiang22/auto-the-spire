# core 首次规则与宿主教程：交付与验证记录

日期：2026-10-06。用户要求补“带人做第一次”和“公开接口怎样使用”的教程。本记录保留此次文档与示例的验证证据；维护登记见[唯一主清单](../../tofix/README.md#本次文档整理)，不改变 ENV 任务状态或冻结内核契约。

以下先保留首次交付时点的证据，随后追加用户要求的[Diátaxis修订与复核](#diátaxis-修订与分类复核)。链接中的教程正文已更新，旧日志仍只证明原时点运行。

## 交付范围

- [第一次写规则](../../../dsl/core/first-rule.md)：逐段编写计分器、开局程序、动作、终局、投影，运行后将三分目标改成五分。
- [第一次写宿主](../../../design/core-the-spire/first-host.md)：空控制台项目接入、LoadFiles/Session/Start、Observe、LegalActions/Bindings/Submit、PendingDecision/Answer、结果与错误处理。
- [最小规则](../../../examples/core-choice-counter/first-counter.spire)和[教学宿主](../../../examples/core-first-host/Program.cs)。宿主只支持本教程的单参与者、单投影、无参数动作和实体单选；参数化提交另外使用已有配置宿主，并在正文给出代码。
- core README、DSL入口、开发者指南及文档总入口连接两条教程。既有规则库与配置入口保留。没有修改生产内核、游戏规则或公开 API。

## 实际运行

原始文件、探针和日志统一在[本次证据目录](../../../../.audit/core-tutorials-2026-10-06/)。所有命令从仓库根目录执行，以下运行均退出 0：

| 检查 | 实际结果 | 证据 |
| --- | --- | --- |
| 构建并运行 `CoreFirstHost.csproj` | 宿主构建 0错误/0警告；分数0→1→2→3，Terminal，结果Score=3 | [首次运行](../../../../.audit/core-tutorials-2026-10-06/first.log) |
| 从正文第2～5步提取全部六个DSL代码块，顺序拼成文件运行 | 与完整规则同样到三分结束；不是只跑仓库里预写文件 | [正文拼接运行](../../../../.audit/core-tutorials-2026-10-06/assembled.log) |
| 按第6步同时改两处目标为五分，`--no-build`运行 | 0→1→2→3→4→5，Terminal，结果Score=5 | [五分练习](../../../../.audit/core-tutorials-2026-10-06/five.log) |
| 同一教学宿主运行 `counter.spire "1,0"` | 暂停时Score=0/Attempts=1/Applied=0；回答后2/1/1；最终3/2/2 | [选择续接](../../../../.audit/core-tutorials-2026-10-06/choice.log) |
| 提取宿主正文的加载及Bindings提交C#片段，新建探针编译运行 | 第一次提交Score=2/Running，第二次Score=3/Terminal，结果tutorial:Game.ReachedTarget | [参数提交](../../../../.audit/core-tutorials-2026-10-06/parameter.log) |
| 配置宿主运行原始 `game.spire "1,0"` | 0→2→3，Terminal | [配置原例](../../../../.audit/core-tutorials-2026-10-06/config.log) |
| 复制规则库并按既有配置教程改为target=5/increments=[2,4] | 0→4→6，Terminal | [配置修改](../../../../.audit/core-tutorials-2026-10-06/config-five.log) |
| 按宿主教程新建console→添加ProjectReference→复制Program→运行 | 在隔离审计目录完成，得到三分终局 | [空项目接入](../../../../.audit/core-tutorials-2026-10-06/fresh-host.log) |

可重复的基础命令：

```powershell
dotnet run --project docs/examples/core-first-host/CoreFirstHost.csproj
dotnet run --no-build --project docs/examples/core-first-host/CoreFirstHost.csproj -- docs/examples/core-choice-counter/counter.spire "1,0"
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj -- docs/examples/core-choice-counter/game.spire "1,0"
```

## 工程与文档检查

- `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests`：95/95，退出0，[日志](../../../../.audit/core-tutorials-2026-10-06/engine.log)。
- `dotnet build AutoTheSpire.sln`：退出1，0警告/6错误，[日志](../../../../.audit/core-tutorials-2026-10-06/build.log)。首错为 `BaseSpire.Slice.Tests/Enemies/SliceEnemyStateMachineChecks.cs:641` 的CS0103，`CandidateLines` 未定义；同文件还有 `ParseCandidateWeights` / `fogmog` 未定义。这些文件不在本次修改范围。
- `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests`：同样被上述编译错误阻断，检查未执行，不记为通过，[日志](../../../../.audit/core-tutorials-2026-10-06/slice.log)。net套件未重跑，本次不改网络模块。
- `python tools/doc-check/check_docs.py` 与 `git diff --check`：退出0；链接/锚点无问题，差异无空白错误，[文档日志](../../../../.audit/core-tutorials-2026-10-06/doc-check.log)、[差异日志](../../../../.audit/core-tutorials-2026-10-06/diff-check.log)。

教程运行证据只证明这里列出的教学规则和接口调用链，不抵充完整原版环境、T01/T02或跨语言公开出口验收。

## Diátaxis 修订与分类复核

同日用户指定以 [Diátaxis原站](https://diataxis.fr/)为写作依据。本轮使用 defuddle 阅读原站的[教程](https://diataxis.fr/tutorials/)、[操作指南](https://diataxis.fr/how-to-guides/)、[参考](https://diataxis.fr/reference/)与[解释](https://diataxis.fr/explanation/)，通过 skill-installer 将 [Canonical documentation-diataxis](https://github.com/canonical/workshop/tree/main/.github/skills/documentation-diataxis)安装到仓库的 [.agents/skills](../../../../.agents/skills/documentation-diataxis/SKILL.md)。它是第三方分类审阅skill，不是Diátaxis官方skill，也不代替原站或技术验证；没有安装其他候选或修改全局skills。

### 页面用途与实际类型

| 页面 | 目标类型 | 修订后内容与读者需要 |
| --- | --- | --- |
| [第一次写规则](../../../dsl/core/first-rule.md) | Tutorial | 固定步骤编写、观察开局、操作到终局并改目标；用实际输出核对过程 |
| [第一次写宿主](../../../design/core-the-spire/first-host.md) | Tutorial | 创建项目→加载与观察→一次提交→循环到终局→回答选择；逐步编译运行 |
| [配置计分目标](../../../dsl/core/how-to-configure-game.md) | How-to | 在已有规则库上完成参数配置与核对 |
| [提交动作参数](../../../design/core-the-spire/how-to-submit-parameters.md) | How-to | 给已有宿主接上用户/策略选定的参数组合 |
| [交互API](../../../design/core-the-spire/session-api-reference.md) | Reference | 按接口列输入、结果、类型、状态和错误语义，供查阅 |
| [规则与宿主](../../../design/core-the-spire/rules-and-host.md) | Explanation | 解释规则/会话/执行帧、动作/决策、观察及规则库职责的关系 |

上述六篇正文各有一种主要类型。README为阅读入口；AGENTS、管理清单、冻结合同与历史记录保持既有职责。没有把整仓历史文档强行套成四个目录，也未声称旧开发者指南已经完全拆分。

### 发现的问题与修订

| 原位置 | 问题及读者影响 | 具体处理 |
| --- | --- | --- |
| 宿主教程第1节 | 可选空项目路线与复制完整程序并列，读者需先做选择；只解释已完成程序，实践过程不足 | 改为唯一空项目路线，每次追加/替换后运行 |
| 宿主教程第4节参数分支 | 第一次操作未完成就切换另一种规则与宿主，打断学习路径 | 迁至参数提交How-to，仅在课后链接 |
| 宿主教程第6节类型表及第7节错误分类 | 查阅资料混在单选练习里，加大首次学习负担 | 迁至API Reference，教程只处理指定文件的实体单选 |
| 规则教程第2～5节 | 中间片段没有可见运行结果，要等整份规则写完才能发现前面错误 | 先完成开局和投影，增加只观察开局的检查点；教学宿主增加`--observe-only` |
| DSL入口原第2～4节 | 导航、职责解释和范围说明混杂，阅读目的不明确 | README保留导航，配置步骤移How-to，职责关系移Explanation |
| 旧开发者指南第3.6节 | 仍声称Build后不保证深度不可变，与冻结契约和`EnginePackage.Freeze`矛盾 | 对照源码及冻结基线修正文档事实，不改API |

完成目标类型与实际内容分类、使用者需要核对、功能与阅读体验检查，并处理以上六项问题。分类结果不作为产品或ENV验收结论。

### 分阶段技术验证

本轮原站摘取、正文生成的代码、运行日志与skill源哈希保存在[Diátaxis审计目录](../../../../.audit/diataxis-docs-2026-10-06/)。从正文提取代码块，按文中追加/替换关系逐阶段生成程序；复制文件后更新审计程序时间戳，确保MSBuild实际重编译各阶段。

| 检查点 | 实际结果 | 证据 |
| --- | --- | --- |
| 宿主第1步，从空目录new console并添加引用 | Hello, World! | [日志](../../../../.audit/diataxis-docs-2026-10-06/host-stage-1.log) |
| 宿主第2步，加载/Start/Observe | Running / Score=0 | [日志](../../../../.audit/diataxis-docs-2026-10-06/host-stage-2.log) |
| 宿主第3步，追加一次Submit | Running / Score=1 | [日志](../../../../.audit/diataxis-docs-2026-10-06/host-stage-3.log) |
| 宿主第4步，替换为循环与结果读取 | 0→1→2→3，Terminal / Score=3 | [日志](../../../../.audit/diataxis-docs-2026-10-06/host-stage-4.log) |
| 宿主第5步，替换为选择续接 | 0/1/0暂停→2/1/1回答→3/2/2终局 | [日志](../../../../.audit/diataxis-docs-2026-10-06/host-stage-5.log) |
| 规则第3步，正文前四块拼接后observe-only | Running / Score=0，退出0 | [日志](../../../../.audit/diataxis-docs-2026-10-06/dsl-start.log) |
| 规则第4步，追加动作与终局 | Terminal / Score=3 | [日志](../../../../.audit/diataxis-docs-2026-10-06/dsl-full.log) |
| 规则第5步，按正文改两处目标 | Terminal / Score=5 | [日志](../../../../.audit/diataxis-docs-2026-10-06/dsl-five.log) |
| 参数提交How-to正文片段 | tutorial:Game.Counter / Score=2 | [日志](../../../../.audit/diataxis-docs-2026-10-06/howto.log) |

九个阶段均退出0，检查输出确实包含各阶段预期结果，[检查记录](../../../../.audit/diataxis-docs-2026-10-06/checkpoints.log)。配置How-to的0→4→6运行沿用同日[配置修改证据](../../../../.audit/core-tutorials-2026-10-06/config-five.log)，规则和配置命令未改变。

教学宿主重新构建0错误/0警告，[构建日志](../../../../.audit/diataxis-docs-2026-10-06/example-build.log)。本轮不改生产规则或内核，未重跑solution、Engine/Slice/net或原版内容加载；首次交付时点的95项通过与Slice编译阻断保留在上文。doc-check扫描222份Markdown/0问题，[日志](../../../../.audit/diataxis-docs-2026-10-06/doc-check.log)；git diff --check退出0，[日志](../../../../.audit/diataxis-docs-2026-10-06/diff-check.log)；12份作者正文/入口/示例文件另查尾空白0问题，覆盖未跟踪文件。最终维护结果已回写主清单。
