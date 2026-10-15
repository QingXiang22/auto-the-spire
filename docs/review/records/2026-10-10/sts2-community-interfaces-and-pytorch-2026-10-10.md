# STS2社区接口研究与PyTorch决策（2026-10-10）

本记录服务于net真实游戏适配器的架构筛选，以及用户授权的Python共享Transformer工程。当前任务状态、排期和实现认领只查[主清单](../../tofix/README.md)；AI范围归W-04，真实接口范围归ENV-T03/客户端核对范围，不另建任务表。

用户本轮明确优先使用PyTorch，并要求先共同寻找社区STS2接口项目。PyTorch工程可以接续，正式训练仍按环境门槛安排。本轮交付源码研究和职责判断，没有实现Transformer、真实游戏后端或HTTP传输。

## 研究范围与固定来源

GitHub API仓库身份、递归文件树以及16份选定源码/设计文件保存于仓库根 `.audit/community-sts2-2026-10-10/`。下载只用于阅读，没有执行社区代码、安装mod或调用社区代理。本表链接绑定实际读取的提交；README功能描述只作为项目声称的范围。

| 项目与固定提交 | 已读实现/设计 | 借鉴用途 |
| --- | --- | --- |
| [BMingSY/sts2-ai-mcp](https://github.com/BMingSY/sts2-ai-mcp/tree/5d1d36b24337d7dd27fe125682e4c51fb9f76aa0) | `docs/ai-mcp-v2-api.md`、`docs/ai-mcp-v2-design.md`、`Game/DecisionWindowService.cs`、`Game/GameThread.cs` | 稳定决策、版本绑定选择、执行后等待新决策、静态资料版本缓存 |
| [netcan/slay-the-spire2-agent](https://github.com/netcan/slay-the-spire2-agent/tree/3ac97a1d7a1eaa73bcacc8b04daaf1181e142b60) | `Contracts/Models.cs`、`Core/BridgeSessionState.cs`、`Providers/IGameStateProvider.cs`、`Providers/RuntimeCardIdentity.cs` | DTO、会话版本与游戏提供方拆分；fixture/runtime结构可作边界参考 |
| [Gennadiyev/STS2MCP](https://github.com/Gennadiyev/STS2MCP/tree/55e064850a68f3b4cde7e5fd525bf9b2dec4e885) | `McpMod.Actions.cs`、`McpMod.StateBuilder.cs`；README | 战斗、选牌、奖励、地图、商店、菜单等真实界面的入口定位 |
| [ndwang/sts2-ai-mod](https://github.com/ndwang/sts2-ai-mod/tree/4e6a9df35d1e9bb3a253eab5cdf286f9f6f6ace9) | `DESIGN.md`、`Contexts/IContextHandler.cs`、`GameStabilityDetector.cs`、`Utilities/GodotMainThread.cs` | 每种界面上下文分别提供状态、命令和执行；Godot主线程调度、选择弹窗稳定边界 |
| [CharTyr/STS2-Agent](https://github.com/CharTyr/STS2-Agent/tree/7b3cb83ff97e05477db7688a37a3e622b12ef074) | `Game/ActionExecutionGate.cs`、`Game/GameThread.cs`、`Server/GameEventSubscriberHub.cs` | 多入口共享动作执行许可、拒绝并发旧动作、事件订阅生命周期 |

## 源码观察及采用判断

**决策必须绑定动作。** BMingSY的[API草案](https://github.com/BMingSY/sts2-ai-mcp/blob/5d1d36b24337d7dd27fe125682e4c51fb9f76aa0/docs/ai-mcp-v2-api.md)区分current、wait、act；act使用当前decision_id/action_id，过期返回stale_decision，结算等待超时后继续wait。读取的实现确有对应版本检查及执行中决策租约。文档仍标draft，源码存在不等于这些性质已在我们的游戏版本验收。

我们的 `net-the-spire/Net.TheSpire/Control/ControlSession.cs` 已有observation版本检查、候选绑定、request账本、settling及查询原请求。应复用这些概念，重点补真实后端的异步稳定判定和主线程执行，不重做协议或把HTTP成功当作游戏动作完成。

**职责拆分比端点数量更重要。** netcan将公开模型、会话版本、提供方接口分开；ndwang的IContextHandler将当前界面的状态、命令和执行放在同一上下文。建议真实适配器按战斗、战斗中选牌、奖励、地图、商店、事件等上下文维护，由协调器处理弹窗优先级和切换。增加一种界面应主要修改该界面适配器；公共传输、账本和Python模型无需增加同名分支。选择暂停仍是正在进行的游戏操作，不能当作原动作已全部结算完。

**卡牌引用需要与位置分开。** netcan的[RuntimeCardIdentity](https://github.com/netcan/slay-the-spire2-agent/blob/3ac97a1d7a1eaa73bcacc8b04daaf1181e142b60/mod/Sts2Mod.StateBridge/Providers/RuntimeCardIdentity.cs)将实例键或模型键与handIndex一起哈希。按此实现，同一对象换位置会换公开ID；模型键回退也不能证明物理实例身份。我们继续使用公开对象引用和当前决策绑定的候选句柄，位置单独编码，跨决策重新验证实际对象，不能沿用旧索引。

**并发许可覆盖结算等待。** CharTyr的[ActionExecutionGate](https://github.com/CharTyr/STS2-Agent/blob/7b3cb83ff97e05477db7688a37a3e622b12ef074/STS2AIAgent/Game/ActionExecutionGate.cs)在已有动作执行时拒绝新请求，不排队旧状态上的操作；许可覆盖执行和等待阶段。真实适配器的所有控制入口应汇入同一许可与版本检查，在游戏主线程重新核对对象和合法性。动作已经被接受后等待超时，客户端查询原请求；不能因超时重复点击。

**借鉴方法，调整实现粒度。** BMingSY的DecisionWindowService在所读版本约5,400行，同时包含选择构造、资料和文本效果解析；STS2MCP的Actions/StateBuilder也各有大量游戏分支。我们可参考其游戏入口，但将界面适配、版本协调、资料缓存和诊断分开。这个判断基于源码结构，没有社区项目维护成本或性能对照测量。

**公开信息继续由提供方裁剪。** 部分桥接实现使用游戏DebugOnlyGetState取得内部对象，这可帮助定位入口，但不能直接作为AI观察。未知牌序、内部RNG和调试字段不进入公开契约；牌堆内容与已知位置分别表达。社区的英文文本正则效果推断、LLM摘要或不完整preview也不能作为模拟侧规则权威。

## 本项目采用的方向

维持四层职责：core提供通用执行；base/mod提供规则；net协调版本、公开编码和控制；AI消费公开状态及完整合法候选。模拟后端继续使用规则包公开输出；真实游戏适配器另包接入同一契约，内部可依赖STS2/Godot，公共net与AI保持通用。

本体适配器先形成“公开快照与候选同版本发布 → 版本核对 → 主线程执行 → 当前选择或下一稳定决策”的闭环。HTTP、长轮询或SSE只是传输选择；MCP面向人工/LLM工具使用，不成为高吞吐训练的每步必经路径。社区GameStabilityDetector可帮助识别等待条件，但固定动画延迟和轮询不能搬进模拟训练步进。

PyTorch工程采用共享的实体/关系编码、合法候选评分及价值输出。PyTorch负责注意力、自动微分、优化器与设备支持；模型输入继续消费公开契约，不按角色/遭遇复制一套模型实现，不把opaque句柄和任意序列化次序当作语义特征。网络规模、关系编码细节、checkpoint身份及工程验收由后续完整机制包明确；本轮不宣称Transformer提高了准确率。

## 已核对与未验证

本轮完成五个仓库身份/文件树查询、16份冻结源码/设计文件读取及本地net账本/候选路径对照。未启动STS2真实客户端、未运行社区测试、未证实它们与本项目游戏版本兼容、未做吞吐或学习效果实验。GitHub版本和公开README也不能替代真实客户端证据。

接续实施前先查主清单W-04或对应真实接口项；登记一个完整机制包及其文件边界。本研究没有重新派发DSH或改变进行中的卡牌包。

本轮纯文档检查：`python tools/doc-check/check_docs.py`扫描350份Markdown，0问题；`git diff --check`通过。因相关文档为未跟踪文件，另逐行检查三份编辑文件的行末空白，通过。没有运行构建/规则套件/运行时内容加载；本轮未修改生产规则或代码。

## BMingSY主参考的进一步核对（同日追加）

用户确认需要自有适配器，并提议以BMingSY为参考。本次继续取回同一冻结提交的 `Game/GameActionService.cs`、`Game/GameStateService.cs`、`Server/HttpServer.cs`，核对出牌提交、结算队列和选择暂停片段；原16份证据不变，合计19份选定源码/设计文件。

采用判断：BMingSY作为本体接入的主要行为与工程参考。它的GameActionService通过 `CardModel.TryManualPlay` 提交出牌，等待时检查正在执行及就绪的player-driven动作，并识别CARD_SELECTION暂停；稳定决策层绑定decision_id/action_id。我们据此定位真实游戏入口，自行实现契约和上下文适配，不让BMingSY的HTTP/MCP接口成为AI的第二套输入格式。

需要明确的本地接缝：ControlSession实际仍直接持有core Session与PublicSnapshotProjector；设计里的通用后端不是已完成的双后端实现。接入本体时，把账本/观察版本/候选绑定等协调职责与模拟执行拆开，模拟侧继续消费core公开结果，本体侧独立读取玩家可见状态、绑定游戏对象及在主线程提交动作。改动限定net及适配器，保持既有wire契约和冻结core；这是实施方向，本轮没有完成代码抽取。

BMingSY的CombatStableDelay为500ms，并基于快照签名静止判断稳定。该条件只能作本体过渡期辅助，不能成为模拟训练每步的等待规则；正式边界应由执行队列、当前玩家输入和界面就绪状态共同确认。选牌弹窗是新的合法回答边界，不能等待父出牌全部完成才发布，否则会互相等待；回答后的余效仍须继续跟踪。

拟定最小完整实施包为：附着一个现有单人战斗，公开状态和完整合法候选同版本发布，提交出牌/目标/结束回合，处理中途选牌并续接，识别战斗胜负。附带过期候选拒绝、同request重发只提交一次及等待超时后查询原请求的证据。随后按上下文增加奖励和地图，以游戏版本绑定的实际客户端证据验收；具体认领/排期只在主清单登记，本追加不自动开启生产包或真实训练。
