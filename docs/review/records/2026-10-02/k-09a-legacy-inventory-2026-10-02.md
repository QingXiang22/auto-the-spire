# K-09a：领域面清点与首批迁移清单（2026-10-02）

> 历史证据（2026-10-03整理）：正文保留原时点结果，历史“下一步”不再作为当前派单指令。本报告归属W-06，未结项转入[当前清单](../../tofix/README.md)；移入records不表示修复或验收通过。逐份归属见[记录索引](../README.md)。

| 项目 | 内容 |
| --- | --- |
| 任务 ID | **K-09a（第二步：把旧后端的领域面清点成可分批迁移的清单）** |
| 权威 | [开工清单 §14](../../../design/implementation-kickoff.md)（K-09a 行："先画项目依赖，再迁文件/引用/测试与工具；先迁切片需要的职责、确保新运行时真实使用，再分批抽离其余"） |
| 负责范围 | `tools/dependency-check/legacy_inventory.py`（新增）、`tools/dependency-check/legacy-inventory-2026-10-02.json`（产物）；并把 K-09a 第一步的边界检查**扩到 net/ai 生产模块** |
| 状态 | **清点完成**；实体迁移（文件/引用/测试）与 K-09b 未开始 |
| 前置 | [K-09a 第一步](k-09a-dependency-map-2026-10-02.md)（依赖图 + 边界检查） |

## 1. 旧后端到底有多大（机械清点，不是印象）

```powershell
python tools/dependency-check/legacy_inventory.py --json tools/dependency-check/legacy-inventory-2026-10-02.json
```

| 指标 | 值 |
| --- | --- |
| `Legacy/**` 文件 | **26** |
| 行数 | **12138** |
| 声明的类型 | **363** |
| 反向扫描的仓库文件 | **125** |

**分类（完整性由工具强制：出现未分类即退出码 1）**：

| 类别 | 文件数 | 含义 |
| --- | --- | --- |
| `sts-combat` | **8** | STS 战斗流程/结算（State/Effects/Environment/执行上下文） |
| `sts-content-schema` | **7** | STS 内容模式（Card/Power/Enemy/Potion/Relic/CardCost/EncounterSlot…） |
| `sts-dsl` | **6** | STS 作者前端（DSL 源模型/解析/编译/效果 DSL） |
| `sts-run` | **3** | STS run 流程（地图/房间/奖励/事件） |
| `generic-candidate` | **2** | 疑似通用（随机/数值/作用域扩展状态）；位于冻结后端，迁移需单独立项 |
| **未分类** | **0** | 无（工具强制） |

引用面最大的几个文件（括号里是"被哪些区域引用"）：

| 被引用文件数 | 类别 | 文件 | 引用区域 |
| --- | --- | --- | --- |
| 403 | `sts-combat` | `Legacy/Effects.cs` | base-spire 11 / core-other 99 / legacy 289 / tools 4 |
| 282 | `sts-content-schema` | `Legacy/Content.cs` | base-spire 34 / core-other 80 / legacy 152 / net 3 / slice 2 / tools 11 |
| 90 | `sts-content-schema` | `Legacy/State.cs` | base-spire 2 / core-other 32 / legacy 53 / tools 3 |
| 79 | `sts-content-schema` | `Legacy/Runs.cs` | base-spire 15 / core-other 44 / legacy 20 |
| 48 | `sts-dsl` | `Legacy/DslSource.cs` | base-spire 3 / core-other 6 / legacy 35 / slice 2 / tools 2 |

> 这些计数是**宽匹配**（只看标识符出现），因此会包含"同名成员"这类非依赖出现——
> 这正是第一步的边界检查必须**按类型使用位置**匹配的原因（见 §3）。

## 2. 首批迁移清单（结论：切片需要的职责**已经迁完**，剩下的是内容面与物理隔离）

开工清单要求"**先迁切片需要的职责、确保新运行时真实使用**，再分批抽离其余"。按清点结果，
切片所需的领域职责**已经在 B-05b/B-05c 迁入新路径**，并且有真实使用证据：

| 旧后端的领域职责 | 新路径归属 | 真实使用证据 |
| --- | --- | --- |
| 伤害次序（加法→乘法→上限→下限→唯一一次截断） | 切片通用 IR（`DamageChain`/阶段函数） | B-05c 批 12/18/21/22（含 5.625→5 反例、cap 位置、`unpowered` 门控、Shrink ×0.70） |
| 能力回合末行为（时长递减 / 转力量） | 作者文本 `owner-turn-end` + 切片程序 | B-05c 批 11（并修掉旧实现"非 Ritual 从不衰减"的潜伏缺陷） |
| 卡牌 hooks（消耗/端末在手/入场/打出前后） | 作者文本 `hook …` + 切片程序 | B-05c 批 2/3（含"升级空 hooks 不隐式继承"） |
| 目标域 / X 费 / 选择 / 批处理 / 生成·升级·变形 / 实例变量 | 作者文本 step 词表 + 切片程序 | B-05c 批 1/4/5/6/7/8/14/15/16 |
| 药水（作者表示 + 使用动作） | 作者文本 `potion`/`potions` + 切片程序 | B-05c 批 20/22/23（4 张药水声明可跑） |

**剩余（不在本批完成范围内）**：

1. **内容面**：把 B-05a 冻结的 96 张卡/89 敌人/11 遗物/63 遭遇等**逐批迁入**（这是 §14 的 **B-06a~c**，
   不是 K-09a）——B-05a 的清单就是分母；
2. **物理隔离**：把旧 STS 实现从通用生产程序集隔离到游戏侧兼容项目、默认入口转新后端
   （这是 **K-09b**，其前置是 B-06c 与所选 run 范围有证据）；
3. **`generic-candidate` 两个文件**（随机/作用域扩展状态）是否属于"通用"需要单独立项判断——
   **我不擅自把它们判成通用**，因为它们在冻结后端里、且新引擎已有自己的随机与状态设施，
   迁移它们不会让新路径多出任何能力。

## 3. 本批顺带做的一处强化：边界检查扩到 net/ai，并再修三类误报

第一步的检查只覆盖 `Engine/**` 与 `Slice/**`。清点显示 `net` 也有 3 处宽匹配命中，
于是把检查扩到 **net 生产模块 + 契约 DTO + ai 包**，并逐条诊断那些命中：

| 误报行 | 为什么不是依赖 | 修法 |
| --- | --- | --- |
| `public const string CardType = "base-spire:Card";` | 同名字段（不是 Legacy 的 `CardType` 类型） | 已由"类型使用位置"匹配排除 |
| `[CardType] = new PublicKindRule(` | 同名字段当字典键；旧模式 `X?/X[]` 误匹配了 `CardType]` | 拆成 `X?`（可空）与 `X[]`（必须成对） |
| `if (ContentId is not null)` | 同名成员 + 关键字 `is`；旧模式 `X ident` 误匹配 | 要求跟随标识符**像声明一样收尾**（`, ) ; = < { }`） |

这三类都**进了检查器的自检**（`checker/self-test`），防止回归。扩面后结果：

```
projects: 56，cycles: 0，Legacy 类型: 363，新路径文件: 39，违规: 0
TOTAL: 8 passed, 0 failed, 8 executed
```

即：**Engine + 切片 + net 生产模块 + net 契约 + ai 包（39 个文件）都没有在类型位置引用任何旧后端类型。**

## 4. 运行结果（本轮实跑）

| 命令 | 返回码 | 结果 |
| --- | --- | --- |
| `python tools/dependency-check/legacy_inventory.py --json …` | 0 | 26 文件 / 12138 行 / 363 类型 / 分类完整（未分类 0） |
| `python tools/dependency-check/check_dependencies.py` | 0 | **8/8**：56 项目 0 环、核心单向依赖、**39 文件 0 违规**、自检通过 |
| `dotnet build AutoTheSpire.sln` | 0 | 0 错误 0 警告 |
| 切片 / Engine / 旧后端 / Net 检查 | 0 | 116 / 64 / 190 / 383（均不回归） |

## 5. 修改文件

- `tools/dependency-check/legacy_inventory.py`（新增；分类完整性强制）
- `tools/dependency-check/legacy-inventory-2026-10-02.json`（产物）
- `tools/dependency-check/check_dependencies.py`（边界扩到 net/ai；修三类误报；自检扩容）
- `tools/dependency-check/depmap-2026-10-02.json`（刷新）
- `docs/design/implementation-kickoff.md` §10（登记清点命令）

未提交、未清理共享工作区。

## 6. 下一批建议

按清单的依赖关系，K-09a 的"切片职责"部分已完结，**下一步应转入 B-06a（基础内容批）**：
把 B-05a 冻结清单里的基础内容（例如状态牌与基础 Ironclad 卡）按"先补原语或流程再声明内容支持"的纪律
分批迁入作者路径，并保持"注册数 ≠ 规则覆盖"的表述。K-09b（物理隔离/默认入口切换）需等 B-06c。
