# ENV-T03：DS动作出口派生方案负责人复核

日期：2026-10-07。任务归属：ENV-T03，只读设计审阅及负责人复核。本文保存本轮证据与裁决，不维护另一套任务状态。

## 结论

DS完成了所派发的源码审阅，识别的动作声明阻断成立，复用现有catalog、候选绑定及Submit链路的方向可采纳。具体方案需要修订，不能直接照伪代码实施：正式路径不能让旧手写表覆盖规则声明；旧场景及身份需隔离；过期候选验收写错了；新增公开字段与包补充元数据的职责仍需区分。

这是审阅方案交付，不是公开出口实现或ENV-T03验收。未新增或修改生产代码。

## 输入与执行边界

- Harness任务ID：`ENV-T03-actions-review-20261007`；会话：`session-5a3cb0b3-368a-4fae-a700-0e46e572297b`。
- 工作区：`F:\Scylier-Project\active\auto-the-spire`；DS与负责人读取的HEAD均为`92ccd346b708b0f552ee074dc7ece446a2908a11`，下游未提交文件仍是实际输入，未检出标签。
- [派单文本](../../../../.audit/dsh-env-t03-actions-2026-10-07/task.txt)、[派发记录](../../../../.audit/dsh-env-t03-actions-2026-10-07/task-record.json)、[完整结果](../../../../.audit/dsh-env-t03-actions-2026-10-07/review-result.json)、[提取的报告正文](../../../../.audit/dsh-env-t03-actions-2026-10-07/remote-report.txt)、[工具调用清单](../../../../.audit/dsh-env-t03-actions-2026-10-07/tool-calls.json)。
- 结果through_seq=269、hasMore=false，最终报告seq=265，turn/end seq=268且reason=completed。已读取完整窗口，不以停止运行代替报告。
- 56次模型工具调用均为源码/文档阅读、搜索、目录与Git只读查询；记录中没有写文件、构建、测试或派其他agent的调用。不能仅凭共享Git状态推断所有文件从未变化，此处以该会话工具证据界定执行范围。
- 首次回收时网站登录303并设置Cookie，但Harness根页面及RPC返回503；用户修复后，使用原会话记录回收成功，没有重复派发。

## 可以采纳的部分

1. [Scheduler](../../../../core-the-spire/Core.TheSpire/Engine/Scheduler.cs)已经遍历`RuleSet.Actions`生成合法Bindings，`Submit`复核角色、参数个数、所声明的域与守卫再执行。对已有可编码参数的新增动作，无需新增核心facade或改游戏调度。
2. [PublicSnapshotProjector](../../../../net-the-spire/Net.TheSpire/Projection/PublicSnapshotProjector.cs)第774行的`RequireAction`及782行的参数槽映射是实际阻断；[PublicSnapshotProfile](../../../../net-the-spire/Net.TheSpire/Projection/PublicSnapshotProfile.cs)第116行仅查询手写Actions。
3. [CatalogProjector](../../../../net-the-spire/Net.TheSpire/Projection/CatalogProjector.cs)第51行遍历同一Profile的动作表。把该表在正式路径改为从规则派生，可复用目录组装、候选编码及[ControlSession](../../../../net-the-spire/Net.TheSpire/Control/ControlSession.cs)第204行的Submit，不重算玩法。
4. 保留公开句柄、语义键排序、观察版本校验、请求账本与决策应答路径。普通Action派生不代表多选、枚举决策或所有内核形状已经可公开编码。
5. 新增一个参数影响已公开字段的扩展Action，是合适的判别案例；目录、候选、参数绑定后的状态变化都应验证。不可编码参数必须明确处理，不能静默省略。

## 必须修订的部分

### 1. 正式路径不接受手写结构覆盖

DS的`WithDerivedEngineActions`先派生全部动作，再执行`actions[id] = rule`，让旧手写Actions覆盖派生结果。这仍然使重复声明拥有最终解释权：以后规则修改参数名、类型或目标类型，旧表可以覆盖新声明；仅检查动作ID仍存在不足以发现这种冲突。对当前动作做“新旧目录相等”的检查也会被手写覆盖掩盖，不能证明派生正确。

正式路径必须以ActionDefinition/参数声明为唯一结构来源，不用手写动作或槽定义覆盖它。旧受限Profile保持显式旧路径；确有旧公共ID兼容需求时，在该路径处理，并核对对应结构而非覆盖正式定义。派生与旧声明的等价检查须针对未被覆盖的原始派生表。

依据：[已确认设计](../../../../docs/design/net-the-spire/design.md)第7.1/7.2节，[ActionDefinition](../../../../core-the-spire/Core.TheSpire/Engine/Schema.cs)第790行及[手写动作表](../../../../net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs)第306/618行。

### 2. 不能直接扩展旧Host并保留旧身份

DS建议直接在现有Host的两个SliceProfile上调用合并入口，身份常量暂不变。该方案把新增动作加入旧目录，却继续报告旧ruleset_id/catalog_id；更不能据“当前两份表看起来相等”宣称任意新增包均字节中性。DS末节承认这一等价性尚未实测，正文的确定性结论应改为待验证。

正式路径按ENV-T03接base工厂提供的已配置、未Start会话，使用对应构建身份及派生声明；旧0.1受限场景维持原行为。规则、所选公开投影或影响出口的补充元数据变化必须进入相应身份/目录摘要，不能把`RuleSet.Identity`一个字符串当作全部公开目录含义。身份接线不是可以等到目录变更后再补的事项。

依据：[现有Host](../../../../net-the-spire/Net.TheSpire.Host/Program.cs)第69～77行、[SliceProfile](../../../../net-the-spire/Net.TheSpire/Projection/SlicePublicProfile.cs)第77/135行、[实施边界](../../../../docs/design/net-the-spire/design.md)第7.3节。本文不指定新的哈希算法或生产接口。

### 3. 过期候选验收混淆了版本与候选身份

DS报告§3第4项要求“旧candidate_id + 新旧两种expected_observation_id → stale_observation”，不符合现有实现。

[BuildEntry](../../../../net-the-spire/Net.TheSpire/Projection/PublicSnapshotProjector.cs)第841～851行仅由动作类型及字段的语义键计算candidate_id，没有把观察版本编码进去。示例Meditate只增加Block、没有移除选中手牌时，相同动作与参数可以在下一边界仍然合法，并拥有相同candidate_id。[Step](../../../../net-the-spire/Net.TheSpire/Control/ControlSession.cs)第175/182行分别检查版本和当前绑定表。

正确的验收分三类：

- 另一个request_id使用旧观察版本：`stale_observation`，不推进。
- 使用当前版本，但该动作/参数已不在当前候选中：`illegal_candidate`，不推进；夹具需要消费所选牌或使守卫失效，确实让候选消失。
- 使用当前版本，且相同语义候选仍合法：允许执行。不能为了满足错误验收而改现有candidate_id或拒绝此请求。

同request_id、同载荷的重发仍先走账本，返回原响应，不重新执行；不要将其混入上述“另一个请求”的过期检查。

### 4. 公开字段结构不需要重复包元数据

DS报告§4第9项将“新增公开字段/内容”一起归为仍需base元数据接口，表述过宽。FieldDefinition的Shape、枚举域、引用目标及所选公开Projection可直接提供字段结构；内容ID目录、集合用途、胜负解释和已知位置角色才需要补游戏语义。实现不能因此再造一份需要作者逐字段/动作登记的元数据表。

依据：[FieldDefinition](../../../../core-the-spire/Core.TheSpire/Engine/Schema.cs)第660行、[实施边界](../../../../docs/design/net-the-spire/design.md)第7.1/7.2节。字段派生仍属于ENV-T03整体范围，本次Action子审阅未实现它。

## 其他准确性修正

- 新动作缺失时，`get_catalog`只遍历旧Profile并漏列动作，不会调用RequireAction；实际异常发生在新增动作合法并进入快照候选时。DS验收§3第1项把两个失败点混在一起。
- DS对CollectionDefinition的一个链接误指RuleSetData.cs；实际定义在Schema.cs第720行。
- 没有参数域不等于Submit强行替换调用者传入值为默认值。LegalActions只枚举已声明域，其余槽来自探针初值；Submit使用调用者传入的arguments，但仅对声明域做成员校验，还校验参数个数与守卫。现有net显式拒绝未绑定参数即可，此审阅不据此新增core维护任务。
- `DeclaredIds.Take(8)`及全种类引用上界可保留为已有设计局限，不为此子审阅顺带改变旧describe字节或收紧作者语法。

## 接续与验证边界

实施方案在ENV-T03内收敛为：正式工厂/身份接线、构建时派生动作声明、复用现有目录/候选/Submit、扩展Action端到端验收。旧Profile独立保留；结构不可编码时明确报告，不改冻结core。上述修订足以形成下一份实现派单，本文没有自动启动生产实现或重新派DS。

本轮验证：完整读取原会话结果与56次工具调用；静态核对核心动作、类型声明、Profile、catalog、候选、Host及step源码。没有运行构建、Engine/Slice/net检查、内容注册加载或公开Host/Python链路，不宣称运行时行为、摘要等价或ENV完成。

文档检查结果由主清单本轮维护记录保存；当前任务状态及验收仍以主清单为准。
