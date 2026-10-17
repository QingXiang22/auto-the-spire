# B-05c：完整前端覆盖（第四批：X 费与捕获时点，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（第四批）——`cost = X` 与**捕获时点** |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移)（B-05c 行明文要求"X 费捕获/目标域/保留时点有独立检查"） |
| 负责范围 | `Authoring/SliceAuthorFrontend.cs`（`cost = X` 解析与校验）、`Composition/CombatSlicePackage.cs`（合法性 + 捕获降级）、`BaseSpire.Slice.Tests/Cards/SliceFrontendXCostChecks.cs`（新增 4 项检查） |
| 状态 | **部分完成（四批）**：X 费与捕获时点已交付；B-05c 其余子项仍未开始（见 §5） |
| 前置 | [批1](b-05c-frontend-batch1-2026-10-02.md) / [批2](b-05c-frontend-batch2-2026-10-02.md) / [批3](b-05c-frontend-batch3-2026-10-02.md) |

## 1. 本批交付

1. **作者语法 `cost = X`**（与 [AGENTS.md](../../../../AGENTS.md) 的项目 DSL 约定一致）：解析为 `IsXCost`，
   声明费用不是固定值。非 X 费仍要求整数，非法文本（`cost = half`）以 `invalid_value` 拒绝。
2. **通用能力（先补运行时，再开放语法）**：X 费牌打出时**捕获当时的剩余能量**，写进这张牌自己的
   `Amount` 字段（效果读的就是这个字段），随后能量清零。没有新增实体字段、没有改 wire/投影。
3. **合法性**：X 费牌在能量为 0 时也能打出（X = 0 合法）；非 X 费牌仍要求 `能量 ≥ 费用`。
   实现上是把原来的单一 guard 包成 `Or(能量 >= 费用, def == 某个 X 费定义)`。
4. **冲突拒绝**：X 费牌声明固定 `amount != 0` → `unsupported_combination`（带路径），
   因为固定值与捕获值会冲突。
5. **零影响保证**：当作者模型里没有 X 费牌时，能量花费仍是**原来那一条指令**（逐字相同），
   因此程序化路径与冻结作者文本的 IR 不变。

## 2. 退出证据（"X 费捕获"的独立检查）

| 判据 | 检查 |
| --- | --- |
| 语法 | `cost = X` 被识别（`IsXCost`、声明费用为 0）；Strike 不是 X 费；`amount != 0` 与 `cost = half` 分别以 `unsupported_combination`/`invalid_value` 拒绝且路径指向该卡 |
| **捕获时点** | `捕获时点是打出那一刻的剩余能量（3 → 3，2 → 2，0 → 0）[真实]`：先花掉 0/1/3 点能量再打出同一张 X 费牌，伤害分别为 **3 / 2 / 0**（能量来自本回合 3 点），能量被清零，且捕获值确实写进了这张牌的 `Amount` 字段 |
| 合法性 | `0 能量时 X 费牌仍合法，非 X 费牌仍按声明费用扣费 [真实]`：0 能量时 X 费牌出现在 `LegalAction.Bindings` 里、Strike 不在；**直接提交非法的 Strike 被 guard 拒绝**且会话状态不变 |
| 默认路径不受影响 | `冻结作者文本没有 X 费牌`：模型里无 X 费；程序化路径开局能量仍是 3 |

## 3. 本轮修正的两处实现/检查缺陷

1. **`Bindings` 的形状判断错误**：我最初按"长度为 1 的参数元组"过滤 `LegalAction.Bindings`，
   但引擎存的是**整帧槽位的克隆**（`probe.Slots.Clone()`），于是 `legalRefs=0`、检查假失败。
   改为按 `Domains.Keys.Single()` 取参数槽位再读该槽。诊断信息（`bindings=1 domains=0:3 hand=3 legalRefs=0`）
   直接把原因指出来——记在这里，因为 net/ai 侧若也按"长度=参数个数"解读 `bindings` 会犯同样的错。
2. **合法性断言层次错位**：先前用"候选域里有没有这张牌"断言合法性是错的——候选域是**整只手牌**，
   guard 才决定可提交的组合；合法性必须看 `Bindings` 或提交时的拒绝。

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 46 slice checks passed**（25 既有 + 4 B-05b + 16 项 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 63 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python ai-the-spire/tests/d04_longrun.py` | 0 | 50 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 5. B-05c 剩余子项（仍未完成）

选择/批处理与**随机候选顺序来源**、保留时点（retain）、实例变量、生成/升级/变形/附魔、
**Power/敌人的行动程序**（只作者化了数据）、药水/run（事件/房间/奖励/地图）、
hook 里更广的步骤面（`target`/`apply_power`/`top_from_discard`/`exhaust_self`）、0.0.1 兼容入口。
B-05a 的分母（354 条声明 / 85 张卡池卡）不变；旧后端仍持有那 2 张真实 X 费卡（B-05a 记录），
本批只是把"X 费 + 捕获时点"这条**能力**建成并验证。

## 6. 修改文件

- `BaseSpire/Authoring/SliceAuthorFrontend.cs`（`AuthorCard.IsXCost`；`cost = X` 解析；amount 冲突校验）
- `BaseSpire/Composition/CombatSlicePackage.cs`（`EnergyIsEnough()` guard、`SpendEnergy()` 捕获链）
- `BaseSpire.Slice.Tests/Cards/SliceFrontendXCostChecks.cs`（新增 4 项）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态）

未修改：`Engine/` 语义（只用既有原语：`Or`/`SetField`/`Subtract`）、Legacy、net、Python 侧。
未提交、未清理共享工作区。

## 7. 下一批建议

**B-05c 第五批：选择与批处理 + 随机候选顺序来源**。它是 §14 里"随机候选顺序和消费点有来源"
这条明文要求的落点，也是剩余子项里唯一同时牵涉**随机数调用顺序**（公共契约）的一项，
因此建议单独一批做，并在开工前明确"候选顺序由哪个具名随机流消费"。
