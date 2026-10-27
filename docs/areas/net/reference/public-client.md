# Python公开客户端接口

文档类型：参考（Reference）。适用于正式Host的协议0.2。首次使用见[公开运行教程](../how-to/run-public-game.md)。底层载荷以[主协议的版本约定](protocol.md)与[0.2 schema](../../../specs/net/0.2/wire.schema.json)核对。

## 连接和响应

Host通过`--package`加载纯 DSL 包，`--scope`只接受该包 manifest 声明的受支持配置。当前包仅开放开发 scope，正常地图和完整 Run 尚未恢复；调用示例见[公开运行指南](../how-to/run-public-game.md)。不可混用不同构建的catalog、候选或回放。

[NetClient](../../../../net-the-spire/client/net_client.py)返回响应信封`{request_id, ok, result或error, ...}`。先检查`ok`再读取`result`；`error`按原样保留。

| 方法 | 参数 | 成功的`result` |
| --- | --- | --- |
| `NetClient(command, *, frame_limit=None, cwd=None)` | Host进程命令列表，例如`dotnet HOST_DLL --package mod-the-spire/base-spire/package.json` | 创建连接，不启动Run |
| `describe(*, timeout=None)` | 无游戏参数 | `identity`、`capabilities.scenario_profiles`等运行期能力 |
| `get_catalog(catalog_id, *, timeout=None)` | 使用`describe.identity.catalog_id` | 所选构建的类型/字段/动作目录 |
| `start(expected_identity, scenario_profile, *, seed=None, request_id=None, timeout=None)` | 前两项来自本次`describe`；seed为游戏字符串 | `{session_id, seed, snapshot}`，seed是实际接受值 |
| `observe(session_id, *, timeout=None)` | 本次启动返回的会话ID | `{session_status, snapshot?}`；稳定状态读取`result.snapshot`，不推进游戏 |
| `step(session_id, expected_observation_id, candidate_id, *, request_id=None, timeout=None)` | 本会话、当前快照观察ID、该快照发布的候选ID | `{session_status, snapshot}` |
| `request_status(target_request_id, session_id=None, *, timeout=None)` | 原请求ID，可带所属会话 | 请求账本状态及完成响应；`not_found`不是未执行证明 |
| `close_session(session_id, *, timeout=None)` | 本次会话ID | 关闭会话；不是游戏胜利 |
| `shutdown()` | 无 | 关闭进程管道并等待Host退出，不提交游戏动作 |

NetClient负责按请求ID匹配响应并在超时后查询恢复。`OutcomeUnknown`表示结果不确定，保留其`request_id`并停止推进；不要自行给原动作换ID重发。

## 公开快照访问

[public_snapshot.py](../../../../net-the-spire/client/public_snapshot.py)提供共用函数，输入均为公开响应中的字典。它不缓存旧候选、不读取内部状态、不执行游戏规则。

| 函数 | 返回值/含义 |
| --- | --- |
| `find_entity(snapshot, kind_suffix)` | 指定公开种类的首个实体或`None`；Run/Player应为单例，多怪物用`entities_of` |
| `entity_of(snapshot, ref)` | 当前公开引用对应实体或`None` |
| `entities_of(snapshot)` | `instance_id → 实体`映射 |
| `field(entity, name)` | 读取`known`字段；整数文本转为整数；未知/缺失返回`None` |
| `public_fields(entity)` | 公开字段短名到值/状态的映射；仅用于单一实体视图 |
| `sequence(snapshot, type_suffix)` | 公开有序集合的元素引用，保留Host顺序 |
| `pile_visible(snapshot, kind_suffix)` | 指定牌堆的可见成员；不补全隐藏成员 |
| `relation_targets(snapshot, type_suffix)` | 公开关系的`source_ref → 有序target_refs` |
| `candidates(snapshot, action=None)` | 当前合法候选；action可为短动作名或完整`action_type` |
| `action_name(candidate)` | 动作短名，例如`PlayCard`或`net.answer_decision` |
| `candidate_fields(candidate)` | 字段ID、短名、公开状态和值，便于记录动作 |
| `first_field_value(candidate)` | 第一个`known`参数值；只适用于已确认参数位置的策略 |

种类/集合/关系访问函数接受短名，也接受完整公开ID。多mod同名时从目录取完整ID；字段短名需要在已确认的实体种类上使用。`flag`和`all_known`分别用于公开布尔字段和候选字段状态。

正式0.2的可选单选会同时发布带Instance字段的`net.answer_decision`候选，以及fields=[]的取消候选。客户端若选择取消，仍提交该候选当前的candidate_id；不自行构造空实例、虚拟卡牌或应答请求。必须选择的目标不会发布取消候选。GeneratedCardOffers是独立公开候选序列，所选实例随后进入Hand或DiscardPile，未选实例不再出现。

## 下游需要什么

| 需要确认的事实 | 唯一入口 |
| --- | --- |
| 当前可提交操作、目标或选牌答案 | 当前snapshot的`legal_actions` |
| 公开字段、集合和引用结构 | `get_catalog`及0.2 schema |
| 起始标识与实际支持场景 | 本次`describe` |
| 长期牌组/药水/遗物集合用途 | 包声明的[公开元数据](../../../../mod-the-spire/base-spire/public/metadata.json)，通过当前catalog查询 |
| base到net公开映射 | [投影规则](../../../../mod-the-spire/base-spire/public/projection.spire)及[通用派生边界](../../../understand/net-design.md#7-公开声明与适配边界) |
| 公开动作的真实游戏行为 | 对应base机制模块；客户端不重写玩法 |

`Run.ActCompleted`和地图阶段属于base玩法；终局结论读取`terminal_result.outcome`。单幕完成、步数截止、故障和执行不确定均不等于整局胜利。
