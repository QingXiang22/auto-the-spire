# 宿主交互 API 参考

文档类型：参考（Reference）。

范围：`CoreTheSpire.Engine` 的文件加载、会话驱动和参与者观察接口。运行语义为 engine/ir@0.3，版本与迁移约束见[冻结契约](../../contracts/core-engine-baseline.md)。构建器、扩展、随机 profile 和诊断选项另查[开发者指南](developer-guide.md)。

## 文件加载与会话

| 接口 | 输入与结果 | 约束 |
| --- | --- | --- |
| `EngineDslCompiler.LoadFiles(params string[] paths)` | 入口文件路径 → 链接后的 `RuleSet` | 递归加载 `imports`；导入相对于导入方文件；加载或编译失败时抛异常 |
| `EngineDslCompiler.Compile(source, sourceName, dependencies, extensions)` | 文本 → `EnginePackage` | 返回包而不是 `RuleSet`；`imports` 需通过 `LoadFiles` 解析；显式依赖与扩展按参数传入 |
| `new Session(rules, seed, participantRoles, ...)` | 规则集、`ulong`种子、角色列表 → 新会话 | 至少一个非空角色；构造不会执行开局程序 |
| `Participant(int index)` | 从零开始的下标 → `ParticipantId` | 第一位参与者 ID 为1；DSL 的 `participant(1)` 使用 ID |
| `Start()` | 无参数；执行规则登记的开局程序 | 宿主负责每局调用一次；可能在开局时请求决策或结束 |
| `RuleSet.Identity` / `Session.Identity` | 规则身份 / 包含种子指纹、随机配置和角色的会话身份 | 身份不替代具体配置与操作序列的记录 |

## 状态与结果

| `SessionStatus` | 命令与查询行为 |
| --- | --- |
| `Running` | 可提交合法动作；也包括构造后、开局前的状态 |
| `AwaitingDecision` | 只接受当前决策的 `Answer`，不接受新动作 |
| `Terminal` | 终局结果可从 `Terminal` 读取；不再接受推进命令 |
| `Faulted` | 故障从 `Fault` 读取；会话不能继续 |

`Start`、`Submit`、`Answer` 同步执行到当前程序完成、暂停、终局或故障。`Running` 不推断当前应行动的玩家，也不保证指定参与者有合法动作。

`TerminalOutcome` 包含 `Definition` 和 `Fields`。`Definition.QualifiedId` 是结果名称；值与 `Definition.Fields` 按下标对应。

## 合法动作与提交

```csharp
IReadOnlyList<LegalAction> LegalActions(ParticipantId participant)
void Submit(ActionDefinition action, ParticipantId participant, IReadOnlyList<Value> arguments)
```

| `LegalAction` 成员 | 含义 |
| --- | --- |
| `Definition` | 当前规则集中的动作定义，包括名称、参数与角色 |
| `Domains` | 参数下标 → 可枚举候选值列表 |
| `Bindings` | 当前守卫成立的参数组合；按参数声明顺序，排除局部槽位 |
| `Candidates` | 有候选引用参数时，该参数的当前实例候选列表 |

无枚举域的自由参数在组合中可能是 `Value.None`；宿主收集输入并按声明类型填入。常用值构造接口：`Value.Of(long)`、`Value.Of(bool)`、`Value.Of(decimal)`、`Value.Of(string)`、`Value.OfRef(InstanceId)`、`Value.OfEnum(int)`、`Value.OfParticipant(ParticipantId)`。

`Submit` 接收本会话规则集的动作定义及恰好与参数数目一致的值列表。提交时重新检查角色、参数域和守卫；此前合法的组合可能因状态变化而被拒绝。停止会话的 `LegalActions` 返回空列表。

## 决策请求与回答

`PendingDecision` 在等待决策时提供当前 `DecisionRequest`。其字段包括 `RequestId`、`StateVersion`、指定参与者 `Participant`、`Kind`、`Prompt`、有序 `Candidates` 及 `Minimum` / `Maximum`。

```csharp
void Answer(DecisionAnswer answer)
```

| `DecisionResponseKind` | `DecisionAnswer` 中读取的字段 | 有效性要求 |
| --- | --- | --- |
| `EntityRef` | `Instances` | 恰好一个当前候选 |
| `EntityRefList` | `Instances` | 数量在范围内，成员来自候选且不重复 |
| `Int` | `Number` | 整数处于请求范围内 |
| `Bool` / `Confirm` | `Flag` | 布尔值 |
| `Enum` | `EnumOrdinal` | 具体枚举结果类型允许的序号；请求范围可进一步缩小 |
| `Participant` | `Participant` | 当前会话有效的参与者 ID |

所有回答都须携带当前 `RequestId`。请求 ID 与状态版本过期、回答无效时，在恢复执行前拒绝，保留当前请求。有效回答从暂停位置续接，也可能再次暂停、终局或故障。

`DecisionAnswer.Participant` 是 Participant 类型的回答值，不表示提交回答的调用者。直接 Session API 不鉴别调用者身份；接入层负责将请求交给指定参与者并验证实际回答者。

## 参与者观察

```csharp
ParticipantView Observe(ParticipantId viewer, ProjectionDefinition projection)
ParticipantView Observe(ParticipantId viewer, string projectionId)
IEnumerable<ProjectionDefinition> ProjectionsFor(ParticipantId participant)
```

`projectionId` 使用限定名称，例如 `tutorial:PlayerView`。`ProjectionsFor` 返回角色适用的投影，不保证只有一个。

`ParticipantView` 包含观察者、投影名称、`StateVersion`、实例、集合、关系以及终局标记。实例字段按投影和可见性发布。视图不可变，不共享实时存储中的可变值；再次修改会话后需重新观察。

`Observe` 不推进执行，不改变状态版本或随机服务。`Session.State` 是内部权威状态接口，不能当作自动符合参与者可见性的观察。

## 错误与停止

引擎错误通过 `EngineFaultException` 抛出，异常的 `Fault` 包含代码、消息和可选上下文。宿主依据异常后的会话状态判断是否可重试：

| 情况 | 会话后果 |
| --- | --- |
| 执行前的非法动作、错误候选、角色不符或过期回答 | 输入被拒绝；会话保持可用 |
| 执行期间的规则错误、预算耗尽等 | 会话进入 `Faulted`，`Fault` 保存原因，当前命令回滚 |
| 在终局或故障后提交命令 | 拒绝；不会重新启动会话 |

普通 Host 不需要在每次 `Submit` 后循环调用 `Step()`；`Step` 也不是单条微指令执行器。

## 相关文档与源码

- 学习一次完整交互：[第一次写宿主](first-host.md)。
- 完成参数化动作接入：[提交动作参数](how-to-submit-parameters.md)。
- 理解交互为何分为动作与动作内决策：[开发者指南的交互模型](developer-guide.md#35-动作与动作内决策是两层交互)。
- 定义与行为：[编译入口](../../../core-the-spire/Core.TheSpire/Engine/Compilation/EngineDslCompiler.cs)、[Session接口](../../../core-the-spire/Core.TheSpire/Engine/Scheduler.cs)、[回答校验](../../../core-the-spire/Core.TheSpire/Engine/Session.Events.cs)、[观察数据](../../../core-the-spire/Core.TheSpire/Engine/Projection.cs)。
