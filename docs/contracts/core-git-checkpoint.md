# core Git 冻结交接

2026-10-05：冻结运行版本 `engine/ir@0.3`，作者语言保留 `core/0.1`、`core/0.2`。接口、语义及变更规则见 [冻结契约](core-engine-baseline.md)。冻结允许兼容维护；影响结果、候选、权限、确定顺序或数值的修改必须升语义版本并提供迁移说明。

## 提交范围与验证入口

本次 Git 基线收录通用内核、核心检查、ANTLR grammar/生成代码、DSL 示例、核心规范及旧后端归档。根 solution 在提交中只登记内核与核心检查，保证检出该提交后不需要尚未提交的 base/net 实现即可构建。共享工作区中的完整 solution 和其他模块改动继续保留，后续随对应模块提交。旧 base 注册器及旧 hook 消费者随内核一起退出活动路径，历史实现保存在 archive。

在独立检出的基线中执行：

```powershell
dotnet build AutoTheSpire.sln
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj
dotnet run --project docs/examples/core-choice-counter/CoreChoiceCounter.csproj -- docs/examples/core-choice-counter/counter.spire "1,0"
dotnet run --project archive/legacy-sts/core-the-spire/Core.TheSpire.Tests
```

核心检查应为 95/95；默认 DSL 规则库示例 Score 0→2→3 后终局；进阶示例应验证暂停/续接及事件，最终 Score=3、Attempts=2、Applied=2。归档 190 项只说明历史实现仍可单独运行，不计入当前原版环境完成度。归档的前端兼容检查另依赖活动 base，随下游集成验证。

[源码摘要](core-engine-baseline.json)使用 UTF-8 文本的 LF 规范化 SHA-256，避免 Windows Git 换行转换使同一源码得到不同指纹；不用于替代运行时规则身份。摘要覆盖内核、核心检查、示例及冻结契约。历史全工作区 base/net/对拍结果见 [关闭报告](../review/archived/core-contract-freeze-2026-10-05.md)，这些结果不意味着本次提交已经包含下游实现。

## 下游开发次序与验收约束

1. **原版环境优先。** 在 base-spire 的 DSL/规则库中实现原版玩法，net 提供同一环境的公开观察与操作。其他 agent 默认消费冻结 core；不能把卡牌、死亡余效、回合、牌堆或胜负规则搬进内核。
2. **对拍与功能一起交付。** 每批内容附相同版本、配置、种子和动作序列的参考对拍，比较公开状态、合法操作、决策暂停、终局和可定位的首个差异。对拍进入日常测试；新增机制和组合路径不能只用少量示例房间验收。unsupported 必须单列，不算通过。
3. **参考模拟器辅助核对。** 使用 reference/sts2-sim 核对双方覆盖的原版范围；它不是原版规则的最终裁判。分歧按真实游戏及对应版本源码核对，记录参考端覆盖缺口，不能为了匹配参考端而固化已知错误。原版环境候选验收仍需要真实客户端核对。
4. **原版稳定后扩展 mod。** mod 包复用同一内容接口。最终对照真实尖塔加载对应 mod 的表现，固定游戏与 mod 版本、加载顺序、配置、种子及输入。纯模拟器对拍不能替代 mod 的真实客户端验收；涉及外部输入或不可复现随机时明确比较边界。
5. **环境可信后推进 AI。** 完整环境、公开接口和日常对拍先稳定，再做训练收益、性能与泛化评估。通用核心缺口须附最小复现，由负责人决定修复或新增能力，不自行重开架构。

本次冻结只关闭 core 的交付，不宣布原版、整局、mod 或 AI 完工。当前工作区的完整任务定义继续由负责人维护。

后续可以直接按 [agent 分工与交付约定](../design/post-core-agent-assignments.md)分配 A/base、B/对拍、C/net；[可复制提示词](../design/agent-goals.md)与[唯一认领清单](../review/tofix/README.md#01-core冻结后的分派与下一步)配套。D/真实客户端和E/指定mod为原版候选稳定后的任务。
