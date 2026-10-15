# B-05c：完整前端覆盖（第一批，2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06、W-01，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **B-05c**（把 normal/upgraded、目标、X 费、hooks、选择、批处理、实例变量、生成/升级/变形、Power/敌人/药水及 run 作者表示迁入同一前端；保留 0.0.1 兼容入口）——**本批只覆盖一小部分** |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md#14-m1后阶段二sts作者前端与内容迁移) |
| 负责范围 | `Slice/SliceAuthorFrontend.cs`（词表与校验）、`Slice/CombatSlicePackage.cs`（降级）、`BaseSpire.Slice.Tests/SliceFrontendDomainChecks.cs`（新增检查） |
| 状态 | **部分完成**：本批交付"效果完全由步骤驱动 + 目标域 `damage_all`"；其余子项仍未开始（见 §4） |
| 前置 | B-05b（已完成，见 [b-05b](b-05b-slice-frontend-2026-10-02.md)） |

## 1. 本批交付

1. **效果完全由步骤驱动**：删掉"kind 与 step 必须匹配"的人为限制（`Skill` 不能声明 `damage`、`Attack` 不能声明
   `block`），也删掉"同一张卡不能既打伤害又加格挡"的限制——效果只看声明的 step 列表，`kind` 退回为元数据
   （仍用于投影/内容目录）。这不是放宽校验换绿灯：`block` 与 `damage` 两个原语本来就是通用的，旧限制只是
   程序化路径按 kind 分派时的副产品。
2. **新目标域 `damage_all`**：对**全部**敌人各造成一次声明数值的伤害，**不需要**目标选择步骤。
   降级只用了既有通用 IR 原语（`ForEach` + `Collection(monsters)` + `Call(damageMonster)`），
   **没有新增 STS 专用节点、没有改 Engine**。
3. **目标域不明确即拒绝**：`damage` 与 `damage_all` 同时出现 → `unsupported_combination`（带路径）。
4. `Build(includeTestHeadbutt, fromAuthorText, authorModel)` 增加**显式作者模型入参**：检查可以传入自定义作者
   文本建包（测试场景卡只在该检查内存在，冻结文本不受影响）。

## 2. 退出证据（B-05c 行要求的分层核对）

| 状态 | 证据 |
| --- | --- |
| **语法** | `B-05c target domain: 语法接受 damage_all，且拒绝目标域不明确的组合（带路径）`：`steps = [damage_all]` 被接受；`damage`+`damage_all` 以 `unsupported_combination` 拒绝且路径指向该卡；`damage_some` 以 `unknown_step` 拒绝 |
| **编译结果** | 同一检查：模型里能看到 `test-cleave` 的步骤就是 `[damage_all]`、卡组包含该卡 |
| **运行语义** | `B-05c target domain: damage_all 对每个敌人各打一次，数值来自声明且不串对象 [真实]`：两名邪教徒各自减少**正好 5**（升级形态各自 **7**）；不产生目标选择决策；单目标路径不波及另一个敌人 |
| **检查** | 上述两项 + `B-05c 效果完全由步骤驱动：同一张卡可以既造成伤害又获得格挡（kind 只作元数据）` |
| **冻结文本不受影响** | `B-05c 冻结作者文本不受影响：卡组与四张卡的步骤逐条不变`：仍是 4 张卡、卡组逐条相同、没有任何卡使用 `damage_all`；单目标 Strike 仍只打所选敌人 |

"目标域有独立检查"（B-05c 行的明文要求）由第 3 条检查满足；"不串对象"由前后排敌人的分别断言满足。

## 3. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | **All 33 slice checks passed**（25 既有 + 4 B-05b + 4 B-05c） |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | 63 passed |
| `dotnet run --project core-the-spire/Core.TheSpire.Tests` | 0 | 190 passed / 0 failed |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 0 | 383 passed（不回归） |
| `python ai-the-spire/tests/run_checks.py` | 0 | 503 passed（不回归） |
| `python net-the-spire/client/test_client.py` | 0 | 50 passed（不回归） |

## 4. B-05c 的剩余子项（本批**未**完成，逐条列出，不并入"已完成"）

| 子项 | 现状 |
| --- | --- |
| 目标域 | 本批只做了 `damage_all`（全部敌人）；`self`/`random-enemy`/`all-allies`/`none` 未做 |
| X 费 | 未做：作者语法没有 `cost = X`，运行时也没有捕获时点程序（B-05a 记的 2 张 X 费卡仍在旧后端） |
| hooks | 未做：`on_exhaust`/`turn_end_in_hand`/`on_enter_combat`/`before|after_card_played` 的作者语法与运行时都没有；**"升级空 hooks 不隐式继承"这条规则因此还没有可断言的载体**（B-05a 记的 6 张带 hook 卡仍未迁移） |
| 选择/批处理 | 未做（只有 Headbutt 的单个弃牌选择；批处理 `count` 语义、多选、随机候选顺序与消费点都还没作者化） |
| 实例变量 | 未做（B-05a 记的卡变量/成长值仍在旧后端） |
| 生成/升级/变形/附魔 | 未做 |
| Power/敌人的**行动程序** | 未做：B-05b 只作者化了敌人与 Power 的**数据**，行动循环（Incantation/DarkStrike）仍是程序化 IR |
| 药水 / run（事件/房间/奖励/地图） | 未做 |
| 0.0.1 兼容入口 | 未动（旧 `.spire` 与旧后端照常，未替换） |
| 随机候选顺序与消费点来源 | 未做（属选择/批处理同批） |

**结论**：B-05c **未完成**，本报告只声明"效果步骤驱动 + 一个目标域"这一批的语法/编译/运行/检查四态都有证据。
按开工清单 §14 的纪律，缺能力时先在通用 Engine/切片运行时补能力、不加 STS 节点——本批遵守了这一点
（`damage_all` 只用 `ForEach`+`Call`）。

## 5. 修改文件

- `BaseSpire/Slice/SliceAuthorFrontend.cs`（`damage_all` 词表；去掉 kind/step 与 block+damage 的人为限制；
  新增 `damage`/`damage_all` 互斥校验）
- `BaseSpire/Slice/CombatSlicePackage.cs`（`damage_all` 降级；`authorModel` 显式入参）
- `BaseSpire.Slice.Tests/SliceFrontendDomainChecks.cs`（新增 4 项检查）、`SliceFrontendChecks.cs`（负例表更新：
  把"Skill 声明 damage"换成"damage 与 damage_all 同时出现"）、`SliceFixtures.cs`（`PutInHand` 支持显式升级值，
  测试场景卡不必存在于冻结清单）、`SliceChecks.cs`（注册）
- `docs/review/tofix/long-task-progress-2026-10-02.md`（状态更新）

未修改：`Engine/` 语义、Legacy、net、Python 侧。未提交、未清理共享工作区。

## 6. 下一批建议

按 B-05c 的依赖顺序，下一批建议做 **hooks 的作者语法 + 运行时（先做 `on_exhaust` 与 `turn_end_in_hand`）**：
它能同时落地"升级空 hooks 不隐式继承"这条明文规则，并覆盖 B-05a 记的 6 张带 hook 卡里的 4 张。
X 费需要先补"捕获时点"的通用能力，建议排在 hooks 之后。
