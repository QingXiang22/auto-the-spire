# B-05c：完整前端覆盖（第十五批：可变实例量，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第十五批）——**可变实例量**（效果把值写回这张牌，后续读出；B-05a 记的 `variables` + `instance.VariableOffsets` 的最小等价物） |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行含"实例变量"） |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（`variable`/`grow`/`variable:` 解析与校验）、`Composition/CombatSlicePackage.cs`（每实例字段、读出、写回）、`BaseSpire.Slice.Tests/Cards/SliceFrontendInstanceVariableChecks.cs`（新增 4 项） |
| 状态 | **部分完成（十五批）**：可变实例量已交付；B-05c 其余子项仍未开始（见 §4） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) … [批14](b-05c-frontend-batch14-2026-10-02.md) |

## 1. 作者语法与降级

```
card test-charge {
  name = TestCharge  kind = Attack  cost = 0
  normal   { amount-source = variable:charge  variable { charge = 2 }  grow = charge  steps = [target, damage] }
  upgraded { amount-source = variable:charge  variable { charge = 5 }  grow = charge  steps = [target, damage] }
}
```

- `variable { <名> = <初值> … }`：这张牌的**实例变量**（逐变体声明初值）。
- `amount-source = variable:<名>`：打出时读这张牌自己的该变量。
- `grow = <名>`：效果解析**之后**把该实例的这个变量加一（所以下一次打出读到新值）。
- 降级：为每个声明的变量名分配一个**卡牌实体字段**（`Variable_<名>`），创建实例时写入该变体的初值；
  读出＝读该字段、写回＝`SetField(字段, 加一)`。**不需要新 IR 节点**，只用既有实体字段能力。
- 编译期规则（都带路径）：`grow:`/`variable:` 引用未声明的变量 → `dangling_reference`；
  声明了变量却无人读写 → **`unreachable_variable`**（不许留不可达声明）；初值非整数 → `invalid_value`。

## 2. 退出证据

| 判据 | 检查 |
| --- | --- |
| 语法 | 两个变体的初值（2 / 5）、写回目标、数值来源逐条读出；4 类非法写法分别以 `dangling_reference`/`unreachable_variable`/`invalid_value` 拒绝且路径含变量名 |
| **写回并记住** | `同一实例重复打出读到被写回后的值 [真实]`：实例初值 2 → 第一次打出造成 **2** → 变量变 **3** → 把**同一实例**放回手牌再打 → 造成 **3**（不是又按初值） → 再写回成 **4** |
| 实例独立 | `不同实例的值互不影响 [真实]`：手里两张同定义的牌，只打其中一张 → 被打的那张变 3，另一张**仍是 2** |
| 默认路径 | 冻结作者文本没有实例变量；程序化路径受控开局仍成立 |

## 3. 本轮修正的缺陷（检查侧）

1. **`variable { … }` 被当成未知块**：前端把变体里的嵌套块一律按 `hook …` 处理，于是合法的 `variable` 块被拒。
   改为显式识别数据块（`variable`），其余非 `hook` 前缀的块才报错。
2. **同一回合重复打出被回合簿记挡住**：`PlayedThisTurn` 集合仍是成员，重放时 `MembershipConflict`。
   fixture 里一并清掉本回合出牌记录（那是回合簿记，不是变量语义），让检查聚焦在写回上。
3. **`PutInHand` 又踩一次**：它复用已在手里的实例，所以"两张同定义的牌"其实是同一张；
   第二张改为从抽牌堆**移动**过来。

## 4. B-05c 剩余子项（仍未完成）

生成/升级/变形/附魔（在战斗中生成新牌、升级手牌、变换定义）；药水/run（事件/房间/奖励/地图）；
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）；
伤害上限阶段（cap，切片仍无等价机制）；变量的**跨战斗/跨实例复制**语义（本批只在战斗内、逐实例）。

## 5. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 91 slice checks passed**（25 既有 + 4 B-05b + 62 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 64 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/j01_episode.py` | 0 | 150 passed（不回归） |
| `python tools/contract-check/check_contracts.py` | 0 | 210 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`variable` 数据块、`grow`、`variable:` 来源、`unreachable_variable`）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`Variable_*` 字段分配、初值、`AmountFrom` 的变量分支、`GrowChain`）
- `BaseSpire.Slice.Tests/Cards/SliceFrontendInstanceVariableChecks.cs`（新增 4 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/`、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第十六批：生成/升级/变形**（在战斗中生成新牌、升级手牌中的牌、把一张牌变换成另一张）。
它是剩余子项里唯一还牵涉"实例身份"的一项（生成会产生新实例、升级会改实例字段），
按纪律先确认既有原语（`CreateEntity`/`SetField` 升级标志）够不够，缺则先在 `Engine/` 补。
