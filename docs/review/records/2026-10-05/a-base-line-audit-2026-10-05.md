> 记录整理（2026-10-06）：原交付、复核及逐批证据保留；开放项已并入[主清单](../../tofix/README.md)，执行合同见[对应提示词](../../../design/agent-goals.md)。本文按原日期阅读，不作为当前待办入口；移入records不表示问题解决或父项验收。

# A 线（BASE-01/02）合并审计报告

| 项目 | 内容 |
| --- | --- |
| 报告 | **A 线唯一自包含审计报告**（本线全部批次合并在此，不再另建碎片报告） |
| 日期 | 2026-10-05（批次：BASE-01 工厂/开局 → R1/R2/R3 修复 → BASE-02 起始遗物 + 出生生命 → 批 1 敌人行动状态机 → 批 2 整幕遭遇队列 → 批 3 敌人 `addCards` + 解析健壮性 → 批 4 `Frail` + 回合末站点校准 → **批 5 遭遇编队（模型/派生流已交付，出生程序未启用）**；**§8.4 记录一次自伤式文件编码事故及恢复**） |
| 工作区 | `F:\Scylier-Project\active\auto-the-spire`，`main`，**未提交**；core 锚定 `92ccd34` / `engine/ir@0.3` |
| 认领 | A：BASE-01 / BASE-02 / BASE-03（关联 ENV-01/02/03/05、W-05/06） |
| 审计结论 | BASE-01 的工厂/正式源/真实开局成立；负责人复核的 **R1/R2/R3 已修复并有判别性检查**；BASE-02 已交付**起始遗物效果**、**敌人出生生命掷点**、**敌人行动状态机**、**整幕遭遇队列（GrabBag + 标签回避）**与**敌人 `addCards`**。**BASE-01 仍未验收**（B/C 未接同一工厂、公开路径未跑通、真实奖励/地图/幕转换缺失），Ironclad **未完成**，本线**不关闭任何父项**。**附带一项自伤事故（§8.4）：`CombatSlicePackage.cs` 编码往返损坏，已恢复代码与全部消息，572 行注释仍待补** |
| 最新验证总表 | solution 构建 **0 错误 0 警告**；Engine **95/95**；Slice **310/310**（新增 28 条：状态机 10 + 队列 2 + `addCards` 3 + Frail 3 + 编队 6 + Constrict 2 + startingPowers 1 + Dexterity 1）；net **393/393**；doc-check **0 问题**；dependency-check **10/10**；`git diff --check` 干净；构建描述 `ruleset=84233027fcde39e1 卡=68 能力=28 敌人=35 弱遭遇=4 普通遭遇=8 房间=15 未支持=135 排除遭遇=8` |

---

## 1. 本报告回答的四个问题

1. **公开入口能做什么** —— §3（工厂接口 + 范围/基线验证）。
2. **正常 Run 走到哪里** —— §5（开发范围下的实际轨迹；**不是**原版保真证据）。
3. **独立对拍证明了哪些新增行为** —— **尚无**：本线证据仍是 A 侧行为检查 + 独立复算（含批 1 的状态机
   独立重放，见 §8.1.1），无双侧比较（B 未接线，见 §9）。
4. **剩余阻塞/未验证范围** —— §8、§9。

---

# 第一部分：BASE-01 正式单人工厂

## 2. 交付的公开接口（B/C 共用，一次登记）

登记位置：[`mod-the-spire/base-spire/README.md`](../../../../tools/base-migration-baseline/README.md) 的「正式单人入口」一节
（签名 + 创建示例 + **当前 dev 限制表** + 身份字段 + 流名映射表）。

| 类型 | 文件 | 关键行 |
| --- | --- | --- |
| `RunConfiguration`（不可变整局配置） | `mod-the-spire/base-spire/BaseSpire/SinglePlayer/RunConfiguration.cs` | :41 record、:63 `Ironclad(...)`、:67 `Resolve()` |
| `BaseSpireScope`（范围身份 + **可实现集合**） | 同上 | :20 起（`Dev`/`Ironclad`/`Full`、`Known`、`Implemented`、`NotImplementedReason`） |
| `BaseSpireSourceBaselines`（**唯一**支持的源基线） | `.../SinglePlayer/BaseSpireSourceBaseline.cs` | :23 `Frozen`、:27 `Supported`、:36 `Require()` |
| `BaseSpireCharacter(s)`（五角色起始配置注册表） | `.../SinglePlayer/BaseSpireCharacters.cs` | :35 record、:63 `All`、:76 `ById`、:82 `ByName` |
| `BaseSpireRuntimeFactory`（唯一组包/会话工厂） | `.../SinglePlayer/BaseSpireRuntimeFactory.cs` | :31 构造、:63 `Describe()`、:78 `CreateSession(seed)`、:116 `ProjectionId` |
| `BaseSpireBuildDescription`（只读构建描述） | `.../SinglePlayer/BaseSpireBuildDescription.cs` | :13 record（含 `ContentDigest`/`SourceDigests`/`SourceEvidencePaths`/`StreamLayoutDigest`/`PublicBuildId`） |
| `BaseSpireRunSession`（已配置、**尚未 Start**） | 同上 | :59 record、:84 `Start()` |

```csharp
var factory = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad()); // ScopeId 只接受 dev
var build   = factory.Describe();                                       // 目录/身份/范围/unsupported
var run     = factory.CreateSession("ENV-IRONCLAD-00");                 // RNG 已种、未消费、未 Start
run.Start();                                                            // 每个新 Session 恰好一次
```

契约（可核对）：返回时 `Session.Random.TotalDraws == 0`、状态里没有玩家实例、`Status == Running`；
`SeededStreams` 给出 **15 条** (原版流类型, 引擎流名, 种子)；`RulesetId` 不含 seed，
`SessionIdentity` 含 seed 指纹（只进证据）。

## 3. 范围 / 基线 / 牌序：负责人复核项 R1/R2/R3 的关闭证据

### R1（P1）开发包不得冒用完整范围身份

- 修复：`BaseSpireScope.Implemented = [Dev]` 是**唯一事实来源**；`RunConfiguration.Resolve()` 在
  **构建任何 Session 之前**拒绝 `Ironclad`/`full`（`NotSupportedException`，附未实现原因），未知 id 是输入错误
  （`ArgumentException`）。范围靠能力/内容闭合开启，不靠改标签。
- 判别性检查（`SinglePlayerStartChecks.cs`）：
  - `BASE-01-R1 范围身份：只有 dev 可实现，Ironclad/full 在建局前拒绝且不生成 Session` —— 三个 id 各传一遍，
    断言 dev 可运行、另两个抛 `NotSupportedException`、拒绝信息点名范围 id 并说明当前只有 dev、
    未知 id 抛 `ArgumentException`；并断言 `Implemented` 恰好是 `[Dev]`。
- 关闭证据：该检查通过；`Describe()` 的 `ScopeId` 只可能是 dev。

### R2（P1）起始牌序在洗牌前被改变

- 修复：`SinglePlayerContentSource` 不再 `OrderBy(CardId)`；改为 GroupBy（保留**首次出现**顺序）+ 计数压缩，
  并**当场断言**"展开后逐位等于来源声明顺序"（不满足就抛，不靠注释声称）。
- 判别性检查两条：
  - `BASE-01-R2 起始牌序` —— 从会话状态读**实际长期牌组顺序**，断言逐位等于
    `Strike×5 → Defend×4 → Bash`（`sts2/src/Core/Models/Characters/Ironclad.cs:43-55`），且不等于排序后的结果。
  - `BASE-01-R2 洗牌输入` —— 取本局 `combat-draw` 流种子，用**独立** `RandomService` 对**声明顺序**复算引擎定义的
    Fisher-Yates，断言其尾段（开局抽 5 张后剩余抽牌堆）与观察值一致；并给反例：若洗牌前按 id 排序，
    同 seed 会得到**不同**结果 ⇒ 检查有判别力。
- 关闭证据：两条检查通过；规则集摘要由 `b343aaf1…` 变为 `65663373…`（证明牌序在 IR 里可见，不是文档承诺）。

### R3（P2）源基线只是可任填的标签

- 修复：`BaseSpireSourceBaselines` 是唯一支持集合（`sts2-frozen-2026-10-05`），`Resolve()` 调 `Require()`，
  未知基线抛 `ArgumentException`（不按名字继续跑、不退回默认）。
- 描述绑定**实际加载物**（不只是名字）：`SourceDigests`（每个源资源一条 16 位内容摘要）、`ContentDigest`（聚合）、
  `SourceEvidencePaths`（file:line 证据清单，来自注册表）、`StreamLayoutDigest`（流名映射 + 算法/映射 id）、
  `PublicBuildId`（范围 + 源基线 + 规则集 + 内容摘要 + 布局）。
- 判别性检查：`BASE-01-R3 源基线` —— 未知基线被拒；`SourceDigests.Count >= 20` 且每条是摘要；
  当场复算遭遇源摘要与 `EncountersDigest` 相等；证据含 `Ironclad.cs`；`PublicBuildId != RulesetId`；同配置两次一致。
- 身份职责分离（回应复核 §2）：**静态规则身份** = `RulesetId`；**随机/布局身份** = `StreamLayoutDigest`；
  **公开构建身份** = `PublicBuildId`；**每局证据** = `SessionIdentity`/`Session.Seed`。

## 4. 正式内容来源（"没有回退原型"的核对表）

| 内容 | 旧路径（正式构建已不用） | 现在的正式来源 |
| --- | --- | --- |
| 起始牌组 | 诊断牌组（每张降级卡各一张）/ `.author` 的 `deck {}` | `BaseSpireCharacters`（`Ironclad.cs:43-55`） |
| 起始资源 | `.author` 的 `run {}` | 同上（80/80、99 金；`Ironclad.cs:33,35`） |
| 起始遗物 | 无 | `SpireRelics` 声明表（`BurningBlood.cs:14-22`） |
| 敌人 | `.author` 两个 Cultist + 固定遭遇 | `ContentSource/enemies*.spire`（`SpireEnemySource`） |
| 遭遇与幕池 | 固定遭遇 | `Run/Content/base-spire.acts.json` + `base-spire.encounters.json` |
| 随机 | 引擎默认 Profile | `mega-random/xoshiro256ss-splitmix64@1` + `vanilla-uniform@1`，15 条流按原版公式显式种 |
| 胜利 | 按房间计数发 Victory | **不发**（正式构建 `terminalOnConfiguredRoomCount: false`） |

---

# 第二部分：BASE-02 起始遗物 + 出生生命（上一批）

## 5. 起始遗物效果（Burning Blood）

- 声明式机制（**不为每件遗物写 C# 效果类**）：
  `mod-the-spire/base-spire/BaseSpire/Relics/SliceRelics.cs` —— `SliceRelic` / `SliceRelicEffect` /
  `RelicTriggers` / `RelicEffectKinds` / `SpireRelics`（声明表 + `UnsupportedReason` 构建期校验）。
- 运行时：`CombatSlicePackage.cs` 的 `RelicVictorySteps`（战斗胜利尾，**在结算写回整局之前**）+
  `Relic` 实体与 `Relics` 集合（属整局状态，进观看者投影）。
- 来源依据：`sts2/src/Core/Models/Relics/BurningBlood.cs:14`（`HealVar(6)`）、`:16-22`（拥有者没死才回血）、
  `sts2/src/Core/Commands/CreatureCmd.cs:698`（回血截断到 `MaxHp - CurrentHp`）。
- 判别性检查两条：清场后 `Run.Hp == 40 + 6`（**不是** 40）；78 血清场后是 **80 不是 84**（截断）。

## 6. 敌人出生生命掷点（`Niche` 流）

- 实现：`CombatSlicePackage.cs` 的 `SpawnHpSteps` + `SpawnMonster`（正式构建专用开关 `spawnHpStream`）。
- 来源依据（`sts2/src/Core/Entities/Creatures/Creature.cs:371-383`，调用点 `CombatState.cs:240`）：
  候选 = 声明范围 **双端包含**；排除**同侧**已占用生命；候选非空 ⇒ `NextInt(0, count)` 取升序候选第 k 个；
  候选为空 ⇒ `NextInt(min, max+1)`；**恰好 1 次抽取**；掷出的值**同时**写进 `Hp` 与 `MaxHp`。
- 判别性检查：`Niche` 抽取次数 == 敌人数；`Hp == MaxHp`；每只落声明范围内；
  **逐只独立复算**（同一 `Niche` 种子按来源算法重推，含排除规则）与观察值完全一致。

## 7. 侦察依据（本轮取得，供 B 对拍与后续实现直接引用）

> 两个只读侦察子任务的结论；上一轮同类子任务失败未返回，本轮重新取得。

### 7.1 敌人行动选择（`sts2/src/Core/MonsterMoves/`）

- `chooseWithLimits` / `chooseWithoutRepeat` / `startWithChoice` / `graph` / `candidate(...)`
  **在 `sts2/` 里不存在**——它们是本项目的 DSL 名。引擎真实机制是状态机 + 权重分支：
  `MonsterMoveStateMachine.cs:34,54-80`（`FindNextMoveState`：`do { next = current.GetNextState(...) } while (!current.IsMove)`）、
  `RandomBranchState.cs:115-128`（选取）、`:130-167`（权重/门槛）、`MoveState.cs:67-70`（固定后继）、
  `ConditionalBranchState.cs:44-54`（有序首匹配，无 RNG）、`MoveRepeatType.cs:3`。
- 权重选取表达式：`max = Σ weight_i`；`num = rng.NextFloat(max)`；按**声明顺序**逐个 `num -= weight_i`，
  首个 `num <= 0` 命中。**全部门槛挡住时没有回退**：`max = 0` ⇒ `NextFloat(0) = 0` ⇒ 命中**第一个**分支；
  `throw` 只在候选列表为空时可达。
- 门槛（`GetStateWeight`）：`UseOnlyOnce` = 状态曾出现在 `StateLog` 则 0；`CannotRepeat` / `CanRepeatXTimes(n)`
  = 该行动在日志**尾部连续出现**次数 ≥ 上限则 0（`CannotRepeat` 等价上限 1）；`cooldown = c`
  = 该状态出现在日志最后 `c` 个**行动**里则 0。全部从 `StateLog` 现算，**没有**计数器字段。
- RNG：`RunRngType.MonsterAi`（`RunRngSet.cs:77`），每次选取**恰好 1 次** raw draw
  （`Rng.NextFloat` → `Rng.cs:156-164` → `MegaRandom.NextDouble`）；当前状态是**尚未执行过的 MoveState** 时
  `FindNextMoveState:60-63` 提前返回 ⇒ **0 次抽取**（首动不掷）。
- 状态：每只怪一份 `MonsterMoveStateMachine`（`_currentState`/`_performedFirstMove`/`StateLog`），**按战斗**重建；
  开局状态在构造时进日志（因此 `UseOnlyOnce` 的开场行动在第一回合就已"用过"）。
- 意图 = **已经选好的**那个行动（`NCreature.cs:289`），不是另掷；掷点在每个**玩家回合开始**
  （`CombatManager.cs:480-483`）与出生时（`:860-867`）。
- **缺口**：`maxUsesPerCombat > 1` 在引擎里没有对应物（`UseOnlyOnce` 只等于 n=1，`CanRepeatXTimes` 是连续上限）。
  当前内容只用 `maxUsesPerCombat = 1`。

### 7.2 遭遇队列与 GrabBag

- 一幕的队列在**开局**用同一条 `UpFront` 流预生成（`ActModel.cs:331-386`）：事件洗牌 → 弱遭遇前缀
  （`NumberOfWeakEncounters`，act-1 Overgrowth = 3，`Overgrowth.cs:45`）→ 普通遭遇补齐到
  `BaseNumberOfRooms`（15，`Overgrowth.cs:47`）→ 15 个精英 → BOSS → 古代人。
- 队列抽取 `GrabBag.GrabIndex`（`GrabBag.cs:111-124`）：`num = rng.NextDouble() * totalWeight`，累加权重线性扫描，
  首个 `num < acc` 命中 ⇒ 权重全为 1.0 时等价 `(int)(NextDouble() * count)`，即**引擎 `DrawIndex` 的原版映射**。
  取出即**保序移除**；袋子**自己不会补充**，由调用方在空时按池的枚举顺序以权重 1.0 重新加入（不洗牌、不抽数）。
- 标签回避（`ActModel.cs:419-430`）：谓词 = `!SharesTagsWith(encounters.LastOrDefault())`，即**只与上一次**比较；
  `GrabBag.cs:96-109` 先做一次 0 抽取的预扫（`_entries.Any(predicate)`），命中则**无上限重抽**；
  预扫失败则退化为一次**无约束**抽取。`SharesTagsWith = Tags.Intersect(...).Any()`（`EncounterModel.cs:279-286`）。
- 每次成功抽取消费 `(1 + 拒绝次数)` 次 `NextDouble`；空袋子无约束抽取仍消费 1 次。
- `enemyIds`/`enemySlots`/`formations`/`uniqueChoiceGroup`/`chooseRandomly` **在 `sts2/` 与参考里都不存在**——
  是本项目 DSL 的形状；引擎按遭遇各自的 C# 代码组编队，并使用**派生种子**
  `(uint)(runSeed + TotalFloor + hash(encounter entry))`（`EncounterModel.cs:268-269`），不是共享流派生。

## 8. 实际轨迹（开发范围，**不是**原版保真证据）

朴素策略（优先攻击牌、占位奖励升级未升级的长期牌、清房后进下一间房；无任何状态注入）：

```
seed=ENV-IRONCLAD-00   房间 1→2→…→7   终局 Defeat（自然死亡）
第 1 间遭遇=[base-spire:shrinker-beetle]   第 2 间遭遇=[base-spire:nibbit]
```

注意：房间范围现在是**整幕队列的 15 槽**（原版 `GetNumberOfRooms(false)`），而不是"已支持池的大小"；
同一 seed 的轨迹随批 1（状态机）与批 2（队列）各变一次——两次变化都来自**机制**，不是测试数字：
批 1 让 `mawler-normal` 进池，批 2 让"第几间遇到谁"由 GrabBag 决定（槽位表见 §8.3.1）。

当前 dev 构建实测（`Describe()` 原文）：

```
ruleset=35143f321aa02ef6  卡=68  能力=25  敌人=21  起始牌组=10  弱遭遇=3  普通遭遇=3  房间=15
未支持=152 项   排除遭遇=14 条
池：fuzzy-wurm-crawler-weak, mawler-normal, nibbits-normal, nibbits-weak,
    overgrowth-crawlers, shrinker-beetle-weak
```

---

# 第二部分（续）：BASE-02 批 1 —— 敌人行动状态机（本批新增）

## 8.1 交付：作者 `behavior` → 与源码同构的状态机（同一 Engine IR）

**为什么这是"真实机制"而不是把旧固定序列扩大**：旧降级只支持 `loop`/`cycle`/`startWith` 三种形状，把
`behavior` 压成"序列 + 循环起点"，因此带权重/冷却/图/条件分支的敌人**整条记为未支持**。本批把
`behavior` 解析成**状态图**，与冻结源码 `sts2/src/Core/MonsterMoves/MonsterMoveStateMachine/` 逐条对齐：

| 组件 | 实现文件 | 对齐来源 |
| --- | --- | --- |
| 状态模型（行动 / 随机分支 / 有序条件分支） | `BaseSpire/Enemies/SliceEnemyBehavior.cs`（新） | `MoveState` / `RandomBranchState` / `ConditionalBranchState` |
| `behavior` → 状态图 | `BaseSpire/Enemies/SpireEnemySource.cs`（`BehaviorBuilder`，替换 `FlattenBehavior`） | 每只敌人的 `GenerateMoveStateMachine` |
| 状态图 → Engine IR（选取 + 执行） | `BaseSpire/Composition/CombatSlicePackage.cs`（`SelectEnemyMove` / `MachinePerform`） | `MonsterModel.RollMove` / `PerformMove` / `FindNextMoveState` |

状态**在 Engine 里**：`Monster` 上新增隐藏字段 `MachineState`、`PerformedFirstMove`、`LastMove`、
`MoveRunLength`、`MoveHistory0..K-1`（冷却窗口）、`MoveUsed_<行动>`（`UseOnlyOnce`）。公开事实**没有新增**：
原版公开的仍然只有 `NextMove`（意图），状态 / 日志 / 窗口都是 `FieldVisibility.Hidden`（不进观看者投影）。

调用点与源码相同（这一点决定 RNG 是否对得上）：

- **出生时**：`CombatManager.AfterCreatureAdded` → `RollMove`；初始状态是**行动**时不转换、不抽数
  （`FindNextMoveState:60-63`），是**分支**时当场掷一次（`Flyconid`/`TwoTailedRat` 的形状）。
- **每个玩家回合开始**：`PrepareForNextTurn` → `RollMove`；因为 `PerformedFirstMove` 已置位，这里做真正的
  转换与抽取。两次调用**幂等**（首动不会重复掷）。
- **敌人回合**：按已选的 `NextMove` 执行（不再重掷、也不再把意图改成"下一个"），执行后置
  `PerformedFirstMove`（来源 `OnMovePerformed`）。
- 每次落到随机分支**恰好一次** `monster_ai` 抽取；行动→行动的固定转移与有序条件分支**0 次**；全部门槛
  挡住时原版仍然抽一次并命中第一个分支（`NextFloat(0)` ⇒ `num <= 0`），本实现用 `SampleInt(0,1)` 精确复现。

### 8.1.1 判别性检查（`BaseSpire.Slice.Tests/Enemies/SliceEnemyStateMachineChecks.cs`，新增 10 条）

- **形状 vs 源码**：Mawler（首动 Claw、三候选 `CannotRepeat`/`UseOnlyOnce`/`CannotRepeat`、权重全 1）、
  Flyconid（初始是**分支**、开场池 2 候选、后续池 3 候选、冷却 3/2/0、三个行动都指回后续池）、
  ShrinkerBeetle / Nibbit（`cycle` 从**列表开头**循环）；正式源 21 只敌人全部带完整状态图
  （行动都有后继、引用都在声明里），49 个行动、8 个分支。
- **行为 vs 独立复算**：合成夹具（**显式种** `monster_ai` 流）+ 测试侧独立复算器按源码语义重放
  （门槛从**完整日志**现算，被测实现用计数 + 有界窗口，两种编码互相独立），逐回合比较实际选出的行动；
  并给出计数证据：冷却门槛真的压过候选、真的走到"全挡回落"分支、序列真的换过行动（否则检查无判别力）。
- **首动不掷**：开局 `monster_ai` 抽取为 0，意图 = 初始行动；首动之后该行动立刻被 `CannotRepeat` 挡住。
- **冷却窗口**：`cooldown=2` 的候选在日志含它自己时被压 0（后排只剩另一个候选可选）。
- **有序条件分支**：`metric(EnemyHp, LessThan, 30)` 让同场两只怪选到**不同**行动，且全程**不消耗**
  `monster_ai`。
- **拒绝**：`maxUsesPerCombat=2`（原版没有整场次数上限）、具名 `condition(Name)`、候选指向未声明行动、
  行动没有后继（源码会在转移时抛错）、有序分支带 `weight`、未实现的 `metric` 条件
  —— 全部在**解析期整条拒绝并附原因**。

### 8.1.2 实际解锁的内容（可核对）

- 新进 dev 池：**`base-spire:mawler-normal`**（Mawler：`startWith` + `chooseWithLimits`，用
  `maxConsecutive`/`maxUsesPerCombat`）。
- 构建描述：卡 68 / 能力 25 / **敌人 21（原 16）** / 起始牌组 10 / 弱遭遇 3 / **普通遭遇 3（原 2）** /
  **房间 6（原 5）** / 未支持 **152（原 157）** / 排除遭遇 **14（原 15）**；`ruleset=d1056ff67e84805c`。
- 开发轨迹（seed=`ENV-IRONCLAD-00`，朴素策略，无状态注入）：房间 **1→2→3→4→5→6**，然后停在范围尽头
  （仍是"没有可用推进动作"，不是真实胜负）。

### 8.1.3 内容校准（按源码逐条核对，本批修掉）

`weight=` 在旧内容里有一类**系统性误读**：源码 `AddBranch(state, int)` 的整数参数是 `maxRepeats`（连续上限）
或 `cooldown`，不是权重。已按源码改正：

| 敌人 | 原声明 | 源码 | 现在的声明 |
| --- | --- | --- | --- |
| Flyconid | `weight=3/2/3` + `cooldown` | `AddBranch(…, 3, CannotRepeat)` 等：权重全 1、冷却 3/2/0 | `weight=1` + `maxConsecutive=1` + `cooldown=3/2/0` |
| FlailKnight / MysteriousKnight | `weight=2` | `AddBranch(state, 2)` = `CanRepeatXTimes(2)` | `maxConsecutive=2` |
| SpectralKnight | `weight=2` / `cooldown=1` | `AddBranch(state, 2)` / `CannotRepeat` | `maxConsecutive=2` / `maxConsecutive=1` |
| HunterKiller | `weight=2` | `AddBranch(state, 2)` | `maxConsecutive=2` |
| FakeMerchant | `weight=3` | `AddBranch(state, 3, CannotRepeat)` = cooldown 3 | `cooldown=3, maxConsecutive=1` |
| TwoTailedRat | 缺连续上限 | `AddBranch(state, 3, CannotRepeat)` | 补 `maxConsecutive=1` |

保留为**权重**的（源码确实是浮点权重，按比例转录）：Fogmog（0.4/0.6 → 2:3）、TwoTailedRat
（1/12×3 + 0.75 → 1:1:1:9）。

**仍未解决的内容差异（登记给下一批，不静默）**：

1. **Nibbit 的开场**：源码初始状态是 `ConditionalBranchState`，按 `_isAlone`/`IsFront` 选择
   （`Nibbit.cs:23-36`）——单独出现时 BUTT，两只时前排 SLICE、后排 HISS。本包按 `cycle` 第一项（= 单独时的
   原版行为）开场，因此 `nibbits-normal` 的开场与源码不同。依赖**遭遇位次**，归批 2（编队）。
2. **Axebot**：源码是固定链 + 条件初始状态（`_stockOverrideAmount` 决定 BOOT_UP / HAMMER_UPPERCUT），
   当前 DSL 写成了 `startWithChoice` + 整数权重；该敌人尚未加载（`Frail`/`startingPowers` 未支持），
   但形状需要在能加载前改正。
3. **`startingPowers`、`addCards`、`summonEnemy`、`escape`、`stealGold`、`if(…)` 效果与
   具名 / `LivingAllies` / `power(...)` 条件**仍是未支持项（逐条有原因）——它们是 Inklet、CubexConstruct、
   Chomper、Fogmog、Fabricator 等仍被排除的原因；其中 `Frail` 这类**能力白名单缺口**同时挡掉了
   Flyconid / CorpseSlug / HunterKiller 等一批敌人（属另一机制族，不在批 1 范围）。

### 8.1.4 共享 DSL 机制（本批抽取，含证据）

本批实际被 ≥2 个真实内容复用的**可组合机制**（参数化、变元数、可组合，不是新的 C# 表）：

- `chooseWithoutRepeat(m1,m2,…)`：内容里 **16 处**，分布在 SoulNexus、TheObscura、Exoskeleton、
  FossilStalker、SludgeSpinner、LeafSlimeS 等真实敌人 → 生成的 IR 是"每个候选 `CannotRepeat` + 权重 1 的
  随机分支"。
- `startWithChoice(choice, then=B)`：Flyconid、Axebot（2 个真实内容）→ "初始状态是受限分支、池内行动回到
  `B` 入口"。
- `chooseWithLimits(candidate(…))`（26 处）与 `graph`（29 处）复用同一套候选 / 后继语义。

证据：§8.1.1 的形状检查（Flyconid / Inklet / SlitheringStrangler 的实际形状 = 源码）、运行时独立复算
（权重 / 一次性 / 连续上限 / 冷却 / 全挡回落）、以及每只敌人的拒绝原因（未支持项不被静默近似）。

**仍未成立**：内容语言（0.0.1）没有 `procedure`/`template`/`import` 一类**规则库层**，core/0.2 的模板也
不能直接包含游戏扩展声明。因此这一批的复用是"**语法形态 + 参数**"级，不是库层 procedure 复用。
若要求库层 procedure，需要先扩语言（属通用能力决策），已按冻结契约交负责人，不在 base 自行扩语法或另建运行时。

## 8.2 交给 B / C 的本批交接（内容 ID、身份、语义动作、诊断维度）

- **内容 ID / 来源顺序**：新进池 `base-spire:mawler-normal`（`Enemies/Content/base-spire.encounters.json` 顺序不变）；
  敌人声明顺序 = `enemies*.spire` 文件序（`SpireEnemySource.EnemyResources()` 排序），行动顺序 = `actions(...)`
  声明顺序，分支候选顺序 = `candidate(...)` 声明顺序（**顺序参与行为**，B 不能重排）。
- **规则 / 构建身份**：`Describe().RulesetId=d1056ff67e84805c`（本批内容 + IR 变化后必须换 id）；
  `PublicBuildId`/`StreamLayoutDigest`/`ContentDigest` 同步变化；每局 seed 仍不进静态身份。
- **语义动作**：敌人选取**不产生**玩家侧公开动作；玩家侧动作集合不变（`PlayCard`/`EndTurn`/`EnterNextRoom`/
  `UpgradeLongTermCard`），因此 C 的候选/绑定结构本批**不需要新增字段**。
- **公开事实**：**没有新增**。新增的 6 类状态机字段都是 `Hidden`；唯一公开的敌人事实仍是 `NextMove`
  （意图，值 = 已选行动名 ≤ 32 字符）。C 若已在发布 `NextMove`，本批只需注意它的**更新时点**从"行动前"
  变成"玩家回合开始/出生时"（= 原版）。
- **所需诊断 / 随机维度**：`monster_ai`（引擎流名 `base-spire:run:monster_ai`）——B 的对拍必须把它
  作为**独立维度**比较：每次落到随机分支恰好 1 次抽取、首动 0 次、全挡回落仍然 1 次；行动→行动的固定转移
  0 次。参考侧若用不同流布局，先按窄裁决处理，不要用换 seed/补抽代替。
- **暂停边界**：敌人选取是**非交互**的（不产生 Decision），发生在 `StartPlayerTurn` 开头与出生创建时；
  C 的暂停/续接语义不变。

## 8.3 交付（BASE-02 批 2）：整幕遭遇队列（原版 GrabBag + 标签回避）

**替换掉的近似**：批 1 之前遭遇是"用同一条 `up_front` 流对池洗一次牌，再按池下标取用"，既没有 GrabBag 的
`NextDouble() * 总权重` 下标语义，也没有标签回避与按幕预生成。本批按源码 `ActModel.GenerateRooms`
（`ActModel.cs:331-386`）+ `GrabBag`（`GrabBag.cs:96-124`）+ `AddWithoutRepeatingTags`（`ActModel.cs:419-430`）
重做：

| 源码事实 | 本批实现 |
| --- | --- |
| 队列长度 = `GetNumberOfRooms(false)` = `BaseNumberOfRooms`（Overgrowth = 15），**前 3 槽**来自弱池 | 队列 15 槽 = 弱 3 + 普通 12（`SliceEncounterPool.RoomCount`/`WeakRoomCount` 取自 acts 声明），不再等于"已支持池大小" |
| 每个池一个 `GrabBag`，权重全 1.0；`num = NextDouble() * 总权重`，按声明顺序累积，首个 `num < acc` 命中 | `EnqueueEncounter_weak/regular`：`SampleInt(up_front, 0, 未用数)`（原版有界映射 = 同一下标），每次尝试**恰好一次**抽取 |
| 谓词 = "不与上一个入队遭遇共享标签 **且** 不是它自己"；`GrabIndex` 先做**零抽取预扫**，没有可用成员就返回 -1 | 预扫为真走重抽循环；为假时**零抽取**转入无约束抓取（一次抽取） |
| 空袋 ⇒ 按池枚举顺序以权重 1.0 重新装袋（不洗牌、不抽数） | 相位内全部成员 `BagUsed` 复位 |
| 队列在**幕开始时**生成；进入房间只 `PullNextEncounter` | 队列 = `EncounterQueue` 集合（`Index` + 遭遇 id），进入房间只按房间号查表，**不抽数** |

**状态归属**：池成员实例带隐藏的 `BagUsed`（袋子状态）与按标签展开的 `Tag_*` 常量字段；"上一个遭遇"
记在 Run 的隐藏字段（`HasLastEncounter`/`LastEncounterId`/`LastTag_*`）。公开事实**没有新增**：
玩家可见的仍然只是"这一间房遇到谁"（`Run.EncounterId`）。

**判别性检查**（`SliceEncounterQueueChecks.cs`，新增 2 条）：

1. **独立复算**：测试侧按源码语义重放整幕队列（`NextDouble()*总权重` 累积、零抽取预扫、重抽、重装袋），
   与状态里的 `EncounterQueue` **逐槽**比较，并核对 `up_front` 的**抽取次数逐次相同**（本 seed = 19 次）；
   同时核对长度 15、槽位编号 0..14、前 3 槽来自弱池、其余来自普通池、相邻两槽不共享标签。
2. **预扫失败路径**（合成池：所有遭遇共享同一个标签）：除第一槽外每次预扫都失败 ⇒ 每次只消费
   **一次**无约束抽取（抽取次数与独立复算相同 = 4），且相邻两槽确实共享标签（证明真的走了这条路径）。
   这条路径在正式池上不会触发（正式池的邻接断言因此附带 `reference.Fallbacks == 0` 的前提）。

### 8.3.1 实际后果（可核对）

- 构建描述：**房间 6 → 15**（= 源的队列长度，不再是池大小）；`ruleset=35143f321aa02ef6`；
  卡 68 / 能力 25 / 敌人 21 / 未支持 152 / 排除遭遇 14 不变。
- 开发轨迹（seed=`ENV-IRONCLAD-00`，朴素策略）：房间 **1→7** 后**自然 Defeat**（房间数从"池尽头"变成了
  真实队列；同一 seed 的具体房间归属由 GrabBag 决定，可逐槽核对 §8.3 的槽位表）。
- 队列实测（15 槽）：
  `fuzzy-wurm-crawler-weak, nibbits-weak, shrinker-beetle-weak, mawler-normal, nibbits-normal,
  overgrowth-crawlers, nibbits-normal, mawler-normal, overgrowth-crawlers, nibbits-normal, mawler-normal,
  overgrowth-crawlers, nibbits-normal, overgrowth-crawlers, mawler-normal`（`up_front` 共 19 次抽取）。

### 8.3.2 仍未成立（登记给下一批，不静默）

1. 输入池是**已支持子集**（6 条）：源池是 4 弱 + 12 普通；未支持遭遇逐条在 `ExcludedEncounters` 里。
2. 原版在队列**之前**还有事件列表洗牌（`list.UnstableShuffle(rng)`）与遗物袋/共享古代人抽取 ⇒ 队列在
   `up_front` 上的**起始位置**与源不同；队列内部算法（含抽取次数）逐字一致。
3. 精英队列（15 条）与 `Boss`/`Ancient` 的 `NextItem` 选择未实现（房间类型未接入）。
4. 重抽循环用 `Repeat(2048)` 表达源码的无上限 `do/while`：预扫保证至少一个可用成员 ⇒ 每轮命中的条件
   概率 ≥ 1/池大小，2048 轮全不命中的概率 < (15/16)^2048 ≈ 0；到上限会**显式失败**。
5. 首次运行（`NumberOfRuns == 0`）的固定房间顺序（`ApplyActDiscoveryOrderModifications`）未建模。

### 8.3.3 交给 B / C 的本批交接

- **语义动作 / 候选**：队列生成发生在**幕开始**（`SetupBattle`，非交互、不产生 Decision），进入房间只有
  按房间号查表；玩家侧动作集合不变 ⇒ C 的候选与绑定结构**不需要改动**。
- **公开事实**：**没有新增**。池成员与队列槽是**未投影**的内部实体（`EncounterPoolEntry`/`EncounterQueueEntry`），
  公开的仍然只有 `Run.EncounterId`（本间房的遭遇 id，值不变、时点不变）。C 若已发布 `RoomCount`，
  注意它的值从"已支持池大小"变成 **15**（= 源的 `GetNumberOfRooms(false)`）。
- **随机维度**：`up_front`（`base-spire:run:up_front`）现在承担**整幕队列**：每次尝试 1 次抽取、
  预扫 0 次、空袋重装 0 次；B 的对拍必须把这条流的**调用序列**与逐槽结果一起比，
  不能只比"队列内容相同"。参考侧若在队列前还有事件洗牌/遗物袋/古代人消费，先按窄裁决处理。
- **构建身份**：`ruleset` 由 `d1056ff67e84805c` 变为 **`35143f321aa02ef6`**（池/队列 IR 变化）；
  `PublicBuildId`/`ContentDigest` 同步变化，每局 seed 仍不进静态身份。

## 8.4 **事故与恢复**：`CombatSlicePackage.cs` 编码往返损坏（2026-10-05）

**性质：A 线自己造成的文件损坏事故，必须记录。** 不是原版/参考差异，也不是测试数字问题。

- **原因**：用 PowerShell 文本管道就地改写该文件
  （`(Get-Content -Raw) -replace … | Set-Content -NoNewline`）。这一次往返把文件按系统代码页解码又编码，
  **每个损坏点丢一个字节**。同类操作（含 `Set-Content` 默认编码）以后一律禁止用于源码；
  源码改写只用 edit/write 工具或显式 UTF-8 的二进制安全脚本。
- **损坏范围**（可核对）：`mod-the-spire/base-spire/BaseSpire/Composition/CombatSlicePackage.cs`
  共 **1235 个字符**被替换/丢失，分布在 **572 行**；其中 **19 处字符串字面量**连同**收尾引号**一起被吃掉，
  导致编译失败（CS1010/CS1002/CS1023 一类）。注释与消息里的中文首当其冲。
- **恢复过程**：
  1. 损坏字节留档：`artifacts/combat-slice-package-encoding-damage-2026-10-05.cs.bak`；
  2. 按 UTF-8（替换非法序列）解码 → 恢复 97% 原文（代码、缩进、英文与大部分中文完好）；
  3. 字符串字面量：用**上一份可编译构建的 DLL** 里的用户字符串表做前缀匹配，
     `tools/repair-combat-slice-strings.py` 精确还原（含 `没有对应的牌区集合`、`没有对应的遗物声明`），
     其余逐条按上下文改写：`tools/repair-combat-slice-text.py`（规则表）+ `artifacts/string-repair-map.txt`；
  4. 编译 + 全量回归证明**行为未受影响**：solution 0 错误 0 警告、Engine 95/95、**Slice 297/297**、
     net 393/393、doc-check 0、dependency-check 10/10。
- **残留（未完成项）**：**572 行注释**里仍有丢失字符的位置（U+FFFD），清单见
  `artifacts/text-repair-report.txt`；**不影响编译与行为**，但文件的可读性低于事故前。
  恢复计划：按清单分批补回（优先本线机制段落），每批只做纯文本补充并重跑 Slice 检查。
  在此之前，任何引用该文件注释的交接都必须以**代码**为准。
- **工具归属说明**：`tools/repair-combat-slice-strings.py` 与 `tools/repair-combat-slice-text.py`
  是**本次事故的一次性恢复脚本**，不属于 B 的对拍平台（`tools/differential/`），也不参与构建/验收；
  注释补完后可删（届时在报告里划掉本行）。

## 8.5 交付（BASE-02 批 3）：敌人 `addCards` + 解析健壮性（本批新增）

**`addCards`（往玩家牌区放状态牌）** —— 逐条对齐 `CardPileCmd.AddToCombatAndPreview<T>`
（`CardPileCmd.cs:997-1035`，例 `Chomper.cs:82` 的 3 张 Dazed）与 `CardPileCmd.Add` 的位置语义
（`CardPileCmd.cs:510-515`）：

| 源码事实 | 本批实现 |
| --- | --- |
| `count` 张**全新实例**（普通形态、实例变量初值来自声明） | `CardFields(...)` + `VariableFields(...)`，每次 `CreateEntity` 一张 |
| `position = Bottom`（默认）= 追加到末尾（`-1` ⇒ `Add`） | `CreateEntity(..., addTo: pile)`（默认 `InsertPosition.End`） |
| `position = Top` = 插到最前（`0` ⇒ `Insert(0)`） | `CreateEntity(..., InsertPosition.Start)` |
| `position = Random` = 每张各抽一次 `Rng.Shuffle.NextInt(当前张数 + 1)` | 每张一次 `SampleInt(combat-draw, 0, Count(pile)+1)` 再 `MoveInCollection(AtIndex)` |
| 牌区 `hand`/`draw`/`discard`（其它牌区不适用战斗生成） | 三个牌区；其它值解析期拒绝 |

**同批修掉的解析健壮性问题**（都是正式源真的出现的写法，之前整只敌人被记为未支持）：

1. `repeat(variants(default=7, deadly=8), attack(3))`：`repeat` 的次数支持 `variants`（进阶 0 取 default）；
2. `loop(<分支>)`：`loop(chooseWithoutRepeat(…))`（`LeafSlimeS`/`TwigSlimeM` 的形状）；
3. `graph` 的 `edge(from, chooseByCondition(…))`：后继支持有序条件分支（`FrogKnight`、`LivingShield` 等）；
4. 敌人的 `addCards` 需要 **卡名 → 内容 id** 链接表（`SinglePlayerContentSource` 在卡牌加载后建表传入）。

**结果（可核对）**：敌人 **21 → 26**（四只史莱姆 `LeafSlimeS/M`、`TwigSlimeS/M` 全部进包）、
未支持 **152 → 147**、`ruleset=fb3673eb637e6525`；卡/能力/遭遇池/房间数不变。

**判别性检查（新增 3 条，`SliceEnemyAddCardsChecks.cs`）**：

- **解析**：`discard/bottom` 与 `draw/random` 的编码逐字断言；`pile=exhaust`、`target=self`、
  `position=middle`、悬空卡名四类写法在解析期被拒并附原因；
- **discard/bottom（真实）**：每次行动净增 3 张 Dazed、**追加在弃牌堆末尾**、`combat-draw` 抽取数为 0；
- **draw/random（真实）**：每张各 1 次 `combat-draw`；用**同一流种子**做独立复算
  （跳过本回合前已发生的抽取）得到两张的插入下标，再模拟"玩家下一回合抽 5 张"，与观察到的
  抽牌堆剩余下标 + 手牌张数逐项比较（本 seed 复算下标 = 4、1，抽走 1 张）。

**本批仍未支持**：`addCards` 的**非玩家目标**（`Noisebot` 用别的 target）、以及内容里缺失的状态牌
（`Toxic` 未随 `status-cards.spire` 加载）与未加载的 `Beckon` —— 这三项逐条留在未支持清单里。

### 8.5.1 编队（formations/slots）现状与解锁判断（供规划者参考）

五个带编队的 act-1 遭遇仍需 **formations/enemySlots** 机制；本批之后各自的**剩余**缺口是：

| 遭遇 | 编队之外还缺什么 |
| --- | --- |
| `slimes-weak` / `slimes-normal` | 只缺编队（四只史莱姆已可加载） |
| `ruby-raiders-normal` | 编队 + `Frail`（`TrackerRubyRaider`） |
| `flyconid-normal` | 编队 + `Frail`（`Flyconid`） |
| `slithering-strangler-normal` | 编队 + `ConstrictPower`（`SlitheringStrangler`） |

即：**编队本身在当前内容下不解锁任何遭遇**，它只是必要条件；下一批把 `Frail`/`ConstrictPower`
这类**能力白名单**补上后，编队才能一次解锁 3～5 条。这个结论用源码逐条核对过
（`SlimesWeak.cs:48-59`、`SlimesNormal.cs:33-45`、`FlyconidNormal.cs:42-49`、`RubyRaidersNormal.cs:38-50`、
`SlitheringStranglerNormal.cs:54-100`），可作为批次顺序的依据。

## 8.6 交付（BASE-02 批 4）：**Frail** + 回合末递减站点的来源校准

**为什么先做能力白名单而不是编队**：§8.5.1 已用源码逐一核对——编队单独不解锁任何遭遇，它需要
`Frail`/`ConstrictPower` 这类能力先落地。本批先补 `Frail`（挡住 8 只敌人的最大单项）。

### 8.6.1 `Frail`（获得格挡 ×0.75）

| 源码事实 | 本批实现 |
| --- | --- |
| `FrailPower.ModifyBlockMultiplicative`：`Owner == target` 且 `props.IsPoweredCardOrMonsterMoveBlock()` ⇒ `0.75m` | 声明 `blockGainMultiplier = 0.75`（新标量形态）+ 运行时乘性阶段；**牌与怪物行动**两条获得格挡路径都过它 |
| `CreatureCmd.GainBlock`：`decimal` 连乘 → `Math.Max(…, 0m)` → `Creature.GainBlockInternal` 里 `(int)` **一次**截断 | 全程用 **定点**（`FieldTypeSchema.Fixed`）连乘，最后 `FixedToInt(Truncate)` 一次：`Defend 5 → 3.75 → 3`、怪物 `gainBlock(10) → 7.5 → 7` |
| `AfterSideTurnEnd(side == Enemy)` ⇒ 递减发生在**敌人**侧回合末 | 见 §8.6.2 的站点修正；`skipFirstWhenAppliedToPlayer` 保留（施加当回合不递减） |
| `FrailPower` 是 `Counter` 型 debuff，位置不限 | 玩家被怪物施加（正规路径）与敌人自施加（夹具）两条都验证 |

同时把 `RuntimeImplemented`/`KnownIds` 补上 `frail`（此前 `blockGainMultiplier` 这个**标量**字段前端根本
不认，`Frail` 整条记为未支持）。

### 8.6.2 顺带修掉的真 bug：回合末递减的**站点**（本轮 Frail 检查暴露）

前端把 `trigger = afterEnemyTurnEnd` 与 `afterPlayerTurnEnd` **都**归约成 `owner-turn-end = tick`，
于是运行时把两者都放在**拥有者自己**的回合末执行 ✗。而来源是
`VulnerablePower`/`WeakPower`/`FrailPower` 都在 `AfterSideTurnEnd(side == Enemy)` 递减（对手侧），
`NoDrawPower` 一类才在拥有者侧。旧行为让玩家身上的易伤/虚弱/脆弱**整体多留一个回合**。

修正（三层，逐层可核对）：

1. `SpirePowerSource`：保留侧别 —— `turnEndSide = "enemy" | "player"`，进 `Declaration`/`AuthorPower`/`SlicePower`；
2. `CombatSlicePackage.ResolveOwnerTurnEnd` 增加两个参数：站点**侧别**与"是否是拥有者自己那一侧"
   （`OwnSide`）；没有声明侧别的能力（冻结 `.author` 内容的 `owner-turn-end = convert-to-strength`）
   沿用旧语义，在拥有者自己那一侧处理；
3. 三个调用点：怪物（自己侧，`enemy`/own）、**玩家在敌人回合末**（`enemy`/非 own，新增）、
   玩家在自己回合末（`player`/own）。

### 8.6.3 结果与检查（可核对）

- 构建描述：能力 **25 → 26**、敌人 **26 → 30**（`Flyconid`、`TrackerRubyRaider`、`CorpseSlug`、`GlobeHead` 等
  Frail 阻塞解除）、普通遭遇 **3 → 4**（**`snapping-jaxfruit-normal` 进池**）、未支持 **147 → 142**、
  排除遭遇 **14 → 13**、`ruleset=d6bc8338e2693152`；房间数仍 15。
- 新增检查 3 条（`SliceFrailChecks.cs`，Slice **297 → 300**）：
  1. 玩家 Defend 过 Frail：**5 → 3**（不是 5、也不是中间量化的 4）；
  2. 递减站点：施加当回合不递减 → 下一个**敌人**回合末 2→1 → 再一个回合末移除；
  3. 敌人自己的 `gainBlock(10)` 过同一阶段：**10 → 7**（同一 move 内先自施加 Frail 再获得格挡）。
- 回归：solution 0 错误 0 警告、Engine 95/95、Slice **300/300**、net 393/393（含既有的
  Ritual 转力量、Unmovable 整数倍、Vulnerable/Weak 衰减检查，全部仍通过）。
- 下一批的编队阻塞面（源码核对，§8.5.1 更新）：`slimes-weak`/`slimes-normal`/`flyconid-normal`/
  `ruby-raiders-normal` 现在**只差编队**；`slithering-strangler-normal` 只差 `ConstrictPower`。

## 8.7 交付与**未启用**（BASE-02 批 5）：遭遇编队（formations / enemySlots）

本批按 §8.5.1 的顺序做「编队」，目标是解锁 `slimes-weak`/`slimes-normal`/`flyconid-normal`/
`ruby-raiders-normal`。**结论：模型、派生流、出生程序都已写好并各有证据，但出生程序与独立复算仍有一处
不一致 ⇒ 编队遭遇继续留在排除清单里（不发布与原版不同的阵容）。**

### 8.7.1 已交付（可核对）

1. **编队模型**（`SliceEncounterPool.cs`）：`EncounterSlot(enemyChoices, chooseRandomly, uniqueChoiceGroup)`
   与 `EncounterFormation(weight, enemyIds, enemySlots)`；`EncounterContent` 结构化解析
   `formations[]`/`enemySlots[]`（不再只数个数）；`SliceEncounter` 带 **`EnemyIds` = 全部可能敌人**、
   `EffectiveFormations`（顶层 `enemySlots` 视为唯一一条编队）。
2. **形状逐条对齐源码**（新检查 1）：`slimes-weak` 三槽（小/中/小，两个小槽同唯一组）、
   `slimes-normal` 两条**固定**编队（= 两个 `NextBool` 结果）、`flyconid-normal`（随机中等史莱姆 + 固定飞菌）、
   `ruby-raiders-normal`（三槽同组 ⇒ 三个不同 Raider）、`slithering-strangler-normal`
   （三条编队：[蛇果+勒杀者] / [随机中等史莱姆+勒杀者] / [两个**有放回**小史莱姆+勒杀者]，权重全 1）。
3. **遭遇派生流**（`VanillaRandomLayout` + 工厂）：`SourceEntry`（`ModelDb.GetEntry` 大写下划线）、
   `EncounterStreamName(entry, floor)`、`EncounterSeed = ContentSeed(runSeed, floor, entry)`
   （`EncounterModel.cs:268` 的 `runSeed + TotalFloor + hash(entry)`）；建局时对每个**已进池的**编队遭遇 ×
   楼层 1..房间数 显式种流（`BaseSpireRunSession.EncounterStreams`，与 12+3 条原版流分开登记），
   种流不消费随机数（检查 2 覆盖公式与"未 Start 时 0 抽取"）。
4. **出生程序**（`CombatSlicePackage.FormationSpawn`/`FormationBody`）：抽编队 → 槽位按**组块**顺序抽
   （同组在第一个成员处连续抽完，`SlimesWeak.cs:51-57`）→ 按**名单顺序**出生（位次与源一致）；
   随机槽用"组内未用候选"过滤后的范围抽一次；非随机槽 0 次抽取；楼层是运行期值 ⇒ 在 1..房间数 上分派流名。

### 8.7.2 未启用：一处**具体**的不一致（下一轮的第一件事）

固定夹具：`slimes-weak`、seed `ENV-IRONCLAD-10`、房间 1、楼层 1：

| | 阵容（槽位顺序） |
| --- | --- |
| 独立复算（源码语义：小1、小2、中） | `twig-slime-s`, **`leaf-slime-m`**, `leaf-slime-s` |
| 出生程序实际产出 | `twig-slime-s`, **`twig-slime-m`**, `leaf-slime-s` |

已排除的可能（本轮实测）：

- 组块顺序**确实**是 `[0,2,1]`（用临时 `Fail` 追踪确认第一个处理的是槽 0）；三次槽的候选与组名与 JSON 一致；
- **抽取次数一致**：该房间在派生流上恰好消费 **3** 次抽取（`DrawCount` 实测 = 3），与源的
  "小1、小2、中" 三次 `NextItem` 相同 ⇒ "次数不对" 被排除；
- 源 `Rng.NextInt(min,max)` 对 1 元素区间**仍然消费一次抽取**（`Rng.cs:101-109` 无退化早退），
  引擎侧 `SampleInt` 也直接落到 `Random.DrawInt(stream,min,max)`（`Session.Instructions.cs:127-135`），
  两边在这点上等价。

⇒ 剩余疑点收窄到**第三次抽取的取值映射**（同一个流、同一位置，独立探针第 3 次 `DrawInt(0,2)` = 0，
而产出的中等史莱姆对应下标 1）或**游标映射**在第二个分组槽之后的错位。
**下一步（最小、决定性）**：让编队程序把每槽的 `drawn` 值写进一个可读的调试字段（或临时 `Fail` 携带该值），
逐槽比对探针的第 k 次抽取；定位后把 §8.7.1(4) 与运行时复算检查一起打开。
实验期间发现的唯一"顺带结论"：编队**未启用**时池里 0 条编队遭遇、派生素子 0 条，房间生成完全不触及派生流
（已被 §8.7.2 的两条检查钉住）。

**当前状态是显式的**：新增两条检查把"(a) 五条编队遭遇逐条带原因进 `ExcludedEncounters`"、
"(b) 池里 0 条编队遭遇、派生素子 0 条"钉成事实 —— 不会出现"既不在池里也不在排除清单里"的静默丢失，
也不会让下一轮误以为编队已经生效。`ConstrictPower`（`slithering-strangler`）仍缺，是编队之外的第二个前置。

### 8.7.3 回归

- solution 0 错误 0 警告；Engine 95/95；**Slice 300 → 304**（+4：编队形状、派生流、显式排除、未启用事实）；
  net 393/393；doc-check 0 问题。
- 构建描述：`ruleset=42d68f0766b845ae 卡=68 能力=26 敌人=30 弱遭遇=3 普通遭遇=4 房间=15 未支持=142 排除遭遇=13`；
  队列仍是 15 槽（弱 3 + 普通 12），`up_front` 抽取 17 次。

### 8.7.4 同时记账：注释损失只补了一部分

上一轮的事故（§8.4）本轮委派了一个子 agent 做注释修复，它在被叫停前只补回约 77 个字符
（`CombatSlicePackage.cs` 仍有 **1158** 个 U+FFFD）。**代码与行为不受影响**（本轮全部验证在事故后重跑通过），
但注释里仍有洞；恢复工作按 `artifacts/text-repair-report.txt` 的行号清单继续，不得用 PowerShell 文本往返。

## 8.8 交付（BASE-02 批 5 收尾）：遭遇编队**已启用**，两处真 bug 定位并修掉

§8.7 的两条假设被逐一排除后，本轮把差异定位到**出生程序自身**，两处都是真 bug（不是原版差异）：

### 8.8.1 根因一：`If(cond, then, other)` 被当成 if-then-else 误用

分组槽的"游标走位"里写成了：

```csharp
scope.If(cursor == drawn, SetLocal(pick, choice), SetLocal(taken, true))
```

第三个参数是**else 分支**：游标**不**匹配时反而把 `taken` 置真，于是
`If(Not(taken), cursor++)` 永不执行 ⇒ 游标恒为 0 ⇒ **永远取第一个候选**。
这就是 §8.7.2 里"中等史莱姆复算 `leaf-slime-m`、程序给 `twig-slime-m`"的直接原因
（`twig-slime-m` 正是该槽的第一个候选）。修法：then 分支写成**块**
`If(cond, [SetLocal(pick, …), SetLocal(taken, true)])`；无唯一组的槽干脆不再走位——
`NextItem(list)` 就是"按抽出的下标直接取候选"。

### 8.8.2 根因二：组内"已用"标志跨槽位、跨房间共享

原先用一个 `used(组, 候选) → bool 局部量` 记录"本组已选过谁"。它有两个问题：
① 同组的多个槽位共享同一份候选与同一批标志；② 局部量**不保证每间房从 false 开始**。
结果是 `ruby-raiders-normal`（三槽共享五个候选）在第 3 个槽上算出 `eligible = 0` ⇒
引擎报 `sampling range [0, 0) is empty`（这一幕在整局推进检查里暴露）。
修法：**不用可变标志**，改成"该候选是否被本组**更早槽位已写好的 picks 局部量**选中"的
**判据表达式**（`And(Not(pick_j == c))`）——它是纯读、天然按房间重算，过滤后的候选数也就不会算错。

### 8.8.3 启用与证据

- 四条第一幕编队遭遇**进池**：`slimes-weak`、`slimes-normal`、`flyconid-normal`、`ruby-raiders-normal`；
  构建描述 **弱遭遇 3 → 4、普通遭遇 4 → 7**（池共 11 条）、**排除遭遇 13 → 9**、`ruleset=f463c5a964bc8f8f`；
  队列仍是 15 槽（弱 3 + 普通 12），`up_front` 抽取 **17 → 15** 次（池更大 ⇒ 重抽更少）。
- 新增/改写检查（Slice **305 → 306**）：
  1. **首间房**带编队时：实际阵容 = 独立复算（`ENV-IRONCLAD-10` 的 `slimes-weak`：
     `[twig-slime-s, leaf-slime-m, leaf-slime-s]`，含组内不重复与位次顺序）；
  2. **跨房间**：seed `ENV-IRONCLAD-00` 第 **3** 间房（`slimes-weak`）的阵容 = 独立复算**按楼层 3** 的派生流
     ⇒ 同时验证了"每 (遭遇, 楼层) 一条流 + 楼层取房间号"的**运行期流名分派**；
  3. 四条编队遭遇进池 + 派生流 4 × 15 = **60** 条且建局不消费随机数；
  4. `slithering-strangler-normal` 仍显式排除，原因是**敌人未加载**（缺 `ConstrictPower`）——不是编队本身。
- 回归：solution 0 错误 0 警告、Engine 95/95、Slice **306/306**、net 393/393、doc-check 0 问题；
  整局推进（公开动作走到真实胜负）在编队启用后仍通过。

## 8.9 交付（BASE-02 批 6）：`ConstrictPower`（缠绕）——解锁蛇形勒杀者

**来源**：`ConstrictPower.AfterSideTurnEnd`（`participants.Contains(Owner)` 时对**拥有者**造成
`Amount` 点 `ValueProp.Unpowered` 伤害）+ `AfterDeath`（施加者死亡且非防移除时移除该能力）；
敌人 `SlitheringStrangler`（`enemies.spire:720-736`）的行动程序：
`constrict(3)` → `graph(start=constrict)` 的二选一 → 回到 `constrict`。

**实现**：

| 环节 | 做法 |
| --- | --- |
| 前端 | `on = [trigger(event=afterOwnerTurnEnd, effect=scaledAttack(0, 1, count=targetPowerAmount(自己, target=self), target=self, properties=[unpowered]))]` → 新的回合末形态 `damage-owner-unpowered:base:perStack` |
| 运行时 | 拥有者回合末对自己造成 `base + perStack × 层数` 点伤害，走既有的 `DamagePlayer(..., Powered=false)` ⇒ 格挡先吸收、**不消化** Strength/Weak/Vulnerable，也不触发"powered 且正数"的反击链（`CombatSlicePackage.cs:1029` 的门控） |
| 站点门控 | 只在**玩家自己**的回合末展开（内容里该能力只由敌人施加给玩家；敌人持有同款未覆盖，已记账） |
| 顺带修掉的守卫 | 前端"没有任何可识别行为就拒绝"的检查漏了 `owner-turn-end` 字段 ⇒ **只声明回合末行为的能力会被整条拒掉**（Constrict 就是这么被挡住的，Pyre 当年同类）。已把 `owner-turn-end` 计入"可识别行为" |

**结果**：`SlitheringStrangler` 进包、`slithering-strangler-normal` 进池 ⇒ 第一幕五条编队遭遇**全部**进池；
构建描述：能力 **26 → 27**、敌人 **30 → 31**、普通遭遇 **7 → 8**、未支持 **142 → 140**、
排除遭遇 **9 → 8**、`ruleset=a0db4c1122c21325`；状态机覆盖：**31 只敌人全部带状态图**（行动 75 个、分支 17 个）。

**检查（Slice 306 → 308）**：

1. **语义**：3 层 Constrict 在**拥有者（玩家）回合末**结算恰好 **3** 点 —— 场上同时挂着 5 层**易伤**也不放大
   （`unpowered` 的判别性断言：powered 的话会是 4 点）；**敌人回合不结算**；层数**不衰减**（时长型）。
2. **进包**：`slithering-strangler` 已加载、它的遭遇在池里、且**不**同时出现在排除清单。

## 8.10 交付（BASE-02 批 7）：`startingPowers`（出生期挂能力）+ 三处出生路径统一

**来源**：敌人的出生期能力与战斗中施加**没有第二条实现**——都是 `PowerCmd.Apply`；因此本批只补"出生点"，
效果仍走既有的 `applyPower` 程序。

| 环节 | 做法 |
| --- | --- |
| 前端 | `startingPowers = [power(<能力>, <层数>), …]` → `[(能力 id, 层数)]`；名字经"已加载能力"解析（**不产生悬空引用**）、层数必须为正整数，别的写法逐条点名 |
| 运行时 | 出生时逐个 `applyPower(怪, id, 层数, ticks=声明是 tick, skip=false)`；`ticks` 按声明（出生自带的易伤/虚弱在回合末递减），`skip=false`（源的"跳过第一次衰减"只发生在**施加给玩家**的负面能力，`PowerCmd.cs:144-147`，以及 `RitualPower.AfterApplied` 的特例） |
| **三处出生路径统一** | 原先只有 `SpawnMonster`（正式池路径）会挂能力；受控切片的 `Setup` 与"固定两 Cultist"的 `SpawnEncounter` 分支是**直接 `CreateEntity`** ⇒ 出生自带能力在**第一间房**会漏挂。本轮把 `SpawnStartingPowers` 提到 `Build` 层，三条路径共用 |

**结果**：未支持 **140 → 139**、敌人 **31 → 32**（`startingPowers` 不再是拒绝理由；剩余 20 只仍被**它们引用的能力**挡住）。

**检查（Slice 308 → 309）**：合成敌人 `startingPowers = [power(Vulnerable, 2)]` ⇒ 出生即带 **2** 层；
一个敌人回合末之后变 **1** 层（按声明递减 + 怪物出生自带不跳过第一次）。

### 8.10.1 本批发现的**跨引用顺序**问题（下一轮）

`Ritual`（声明在 `enemies.spire`）的效果是 `grantPower(Strength, sourceStacks)`，而 `Strength` 声明在
**字典序更靠后**的 `shared-powers.spire` ⇒ 能力源**单趟**降级时 `Ritual` 找不到 `Strength`，于是整条被判
"引用的能力不在已加载的能力源里"（`CalcifiedCultist.moves.*` 因此整只未支持）。这是**加载顺序**缺陷，
不是内容缺失：修法是在能力源里让 `grantPower(<规范名>)` 走**规范 id 表**（`KnownIds`）而不是"当前已加载集合"，
或在能力源做两趟降级。已记入下一批（它一次会解锁依赖 `Strength`/`Vulnerable` 等基础能力的敌人与能力）。

### 8.10.2 仍未支持的门（按出现频次，供规划）

`startingPowers` 之后，剩余敌人的阻塞集中在**它们引用的能力**：`Artifact`、`Slow`（`modifyPowerVariable`
+ 事件对）、`TerritorialPower`、`Stock`、`ImbalancedPower`、`Tender` 等，以及行动效果 `if(…)`/`summonEnemy`/
`escape`/`stealGold` 与具名条件 `power(…)`。这些都是**逐条**的机制缺口，不再是"整类未接入"。

## 8.11 交付（BASE-02 批 8）：修掉跨文件能力引用被误判——`skipFirstWhenAppliedToEnemy` 未识别

§8.10.1 记的"加载顺序缺陷"经查**不是顺序问题**：真正的原因是前端把
`skipFirstWhenAppliedToEnemy` 当成**未知字段**（只把玩家版 `skipFirstWhenAppliedToPlayer` 放进了
"运行时既有规则覆盖"的分支）⇒ `Ritual`（`trigger = afterEnemyTurnEnd` + `effect = grantPower(Strength, sourceStacks)`
+ `skipFirstWhenAppliedToEnemy = true`）整条被判未支持 ⇒ 引用它的敌人（`CalcifiedCultist` 等）也整只未支持。

**修法**：把敌人版并入同一分支（与玩家版同处理），并在代码与构建描述里**显式记账**其已知范围：
「跳过第一次衰减」的判定落在**施加点**（玩家负面能力与 `Ritual` 步骤各自传 `skip`），因此
`skipFirstWhenAppliedToEnemy` 目前只被**识别**、不改变出生期施加的行为——后果是"出生自带的 tick 型能力
会在敌人第一个回合末就递减一次"，这条行为已被 §8.10 的 `startingPowers` 检查**钉住**（不是静默差异）。

**结果**：能力 **27 → 28**（`Ritual` 进包）、敌人 **32 → 35**、未支持 **139 → 135**、
`ruleset=84233027fcde39e1`；状态机覆盖：**35 只敌人全部带状态图**（行动 84 个、分支 18 个）。
回归：solution 0 错误 0 警告、Engine 95/95、Slice **309/309**、net 393/393、doc-check 0。

**教训（记入下一批的做法）**：报"顺序/链接"类缺陷前，先用 `W06_DUMP_POWER` 这类**逐条诊断**把
"这个声明自己的拒绝理由"打出来——本轮若直接按"两趟降级/规范 id 表"去改，会改到**不是病因**的地方。

## 8.12 A 线剩余缺口的**逐条清单**（从当前构建的诊断里直接抽取，供下一批按频次取用）

抽取方式：对 `artifacts/run13.txt`（当前构建的逐条诊断输出）用 PowerShell 正则分组统计（`[regex]::Matches` + `Group-Object`）；脚本版因本环境 UTF-8 读取不稳定已移除，不作为交付物。
当前描述：`卡=68 能力=28 敌人=35 弱遭遇=4 普通遭遇=8 房间=15 未支持=135 排除遭遇=8`。

**① 缺失的能力引用（`excluded` 的直接原因，按被引用次数）**：

`Artifact×5`、`Thorns×4`、`Minion×3`、`IllusionPower×2`、`Dexterity×2`、
`PossessDexterityPower`、`PaperCutsPower`、`Slumber`、`HardenedShellPower`、`Hexx`、`Intangible`、
`Burrowed`、`TangledPower`、`HighVoltage`。

**② 未支持的敌人效果族（按出现次数）**：

`modifyEnemyVariable×4`、`if×4`、`stealGold×3`、`summonRandomEnemy×3`、`modifyPowerVariable×2`、
`removePower×2`、`escape×2`、`setEnemyHealth×1`。

**③ 建议的下批顺序（按"机制现值 × 内容收益"排）**：

1. **`Dexterity`（加性格挡修正）** —— 本批最划算：格挡修正阶段**已经**建好（§8.6 的定点连乘 + 一次截断），
   只需在链条里加**加性**一段（来源 `DexterityPower.ModifyBlockAdditive`），立刻解锁 2 处引用，
   并与既有 `Frail` 成对（一个加、一个乘，顺序按源的 `Hook.ModifyBlock`）。
2. **`Thorns`**（下一批首选）—— 源 `ThornsPower.BeforeDamageReceived`：`target == Owner && dealer != null && props.IsPoweredAttack()` ⇒ 对**攻击者**造成 `Amount` 点 `Unpowered` 伤害（内容写法见 `enemies.spire:115-120` 的 `events=[event(type=before_damage, owner=target, require=[move], exclude=[unpowered], effect=scaledAttack(0,1,count=targetPowerAmount(Thorns,target=self), target=target, properties=[unpowered]))]`）。**已定位的两处改动点（委派子 agent 的只读诊断，未落代码）**：① 前端 `ReadEvents` 目前只接受 `positiveAmount=false`、事件类型只限 damage/block/power ⇒ Thorns 会以「事件类型 `before_damage` 未支持」**整条被拒**（要新增该形态，并让别的组合继续逐条点名）；② 运行时反击链 `TriggerAfterDamageTaken` **只挂在 `DamagePlayer`** 上 ⇒ **怪物侧缺对称站点**（要补 `DamageMonster` 之后的同形状钩子）；反伤两处都要 `powered=false`（不递归触发）。**落点**：现有站点只在**玩家被打**时触发（`DamagePlayer` 的触发链，
   `CombatSlicePackage.cs:1029`），把它从 FlameBarrier 的专用形状推广成"按层数反伤"即可，收益 4 处引用。
3. **`if(<条件>, …)` + `hasPower`/`powerAmountAtLeast`** —— 内容里出现 4 次的**异质**条件族
   （见 `enemies*.spire`：`hasPower(X, target=…)`、`powerAmountAtLeast(X, n[, target=…])`、
   `cardPlaysThisTurnAtLeast(1, type=Skill, phase=Started)`，且带 `else=`）。
   实作要点：解析 `if` 的 then/else、条件求值复用既有的 `powerStacksOf` 纯函数、`else` 走 `IfElse`；
   `cardPlaysThisTurnAtLeast` 需要本回合出牌计数（`PlayedThisTurn` 集合已有 ⇒ 可判类型）。
4. **`modifyEnemyVariable` / `modifyPowerVariable` / `setEnemyHealth`** —— 数值改写族：前两个是字段写回
   （敌人自带变量、能力变量），第三个是"按声明设敌人血量"（`Chomper` 一类）；
   它们**都是纯状态写**，不需要新钩子。
5. **`summonRandomEnemy` / `stealGold` / `escape`** —— 需要新机制（生成随机敌人的随机源与槽位上限、
   金币窃取与失败结算、逃跑与战斗结束条件），应各自单独一批，不要与数值族混在一起。
6. 再往后才是**房间类型/精英·BOSS 队列/真实奖励/地图/幕转换**（§12 的 RESUME HERE 已列）。

**⑤ 委派教训（2026-10-05，两轮实测）**：把 §8.12 的机制项交给子 agent 时，**两次都停在"只读侦察"**（各 2–3 个完整回合、零文件写入，`remove_power`/`thorns` 计数始终为 0），即使提示里已写"先写代码别探索"。下一会话若继续用委派：把任务再拆成**单步**（例如"只加前端 `remove_power:<id>` step 并让 `dotnet build` 通过，先不管运行时与检查"），并在提示里给出**要新增的代码片段位置与形状**（函数名 + 现有同类写法的行号），否则应由**主 agent 或新会话**直接实现。**④ 方法教训（已入 §8.11）**：先打印"这个声明自己的拒绝理由"再动手；不要按"看起来像"的假设改机制。

## 8.13 交付（BASE-02 批 9）：`Dexterity`（格挡**加性**修正）+ 修掉一个潜在错误行为

**`Dexterity`**（`role = Dexterity`，来源 `DexterityPower.ModifyBlockAdditive`：`block + Amount`）：
在既有的**乘性**格挡阶段（§8.6 的定点链）**之前**加一段**加性**阶段（`层数 × 每层点数`），
牌与怪物行动两条获得格挡路径都过它，全程定点、最后只截断一次（`Hook.ModifyBlock` 的顺序：先加后乘）。

**顺带修掉一个潜在错误行为（本轮新检查逼出来的）**：无次数上限的乘性修正（`Frail` 的 ×0.75）此前是
**无条件**乘上去的 —— 只要该能力被**声明**，即使拥有者身上**没有**这个能力也会改数值（源的
`ModifyBlockMultiplicative` 只在 `Owner == target` 且该能力存在时被调用）。**现象**：夹具里玩家 3 层
`Dexterity` + Defend 5 应得 8，实测 **6 = (5+3)×0.75**，暴露出"没带 Frail 也被乘了 0.75"。
修法：乘性项外面加"拥有者层数 > 0"的门（玩家侧与敌人侧都加）。此前若干检查没抓到它，是因为它们多
只断言"格挡吸收了伤害"，不再单独断言 Defend 的精确值。

**结果**：能力 **28 → 29**、未支持 **135 → 134**、`ruleset=264c1758c0be75fc`；
新检查（Slice **309 → 310**）：3 层 Dexterity + Defend = **8**（加性生效，且不被无关能力污染）。

---

# 第三部分：未验证范围、阻塞与下一批

## 9. 剩余阻塞（分类）

| # | 阻塞 | 现状 | 归属 |
| --- | --- | --- | --- |
| 1 | B 未接同一工厂 | `tools/differential/BaseBackend.cs` 仍走受控切片；正式侧应 `new BaseSpireRuntimeFactory(RunConfiguration.Ironclad())` + `CreateSession(seed)`。**R1/R2 未修复前不得计相关 case 通过**——现在已修复，且 `ScopeId` 只可能是 dev | B |
| 2 | C 未接同一工厂 | `Net.TheSpire.Host/Program.cs:62-69`、`Control/ControlSession.cs:55` 仍 `new Session(...)` 默认随机 Profile | C |
| 3 | seed 哈希版本分歧 | 本项目按冻结 `sts2/` 的 `uint(hash32)`；`reference/sts2-sim` 是 v0.111 的 XxHash64。**`old` 前缀只改 run seed 一处，具名流派生仍用参考哈希** ⇒ 不能当整套对拍通过依据 | 负责人 + B |
| 4 | 无双侧对拍 | 本批全部证据是 A 侧行为检查 + 独立复算 | B |
| 5 | 无公开 Host 轨迹 | 公开 `describe→catalog→start→step→observe` 未跑通 | C |
| 6 | ~~敌人行动算法未实现~~ **本批已交付** | `chooseWithLimits`/`graph`/`startWithChoice`/`chooseWithoutRepeat`/`chooseByCondition` 已按源码语义接入（见 §8.1）；剩余的是**条件与行动效果的覆盖**：具名 `condition(Name)`、`metric(LivingAllies/EnemyCount/…)`、`power(...)`、`allyAction(...)`、`startingPowers`、`addCards`、`summonEnemy`、`escape`/`stealGold`/`if(…)`，以及**能力白名单缺口**（`Frail`/`Ritual`/`TangledPower` 等） | A / BASE-02 |
| 7 | ~~遭遇队列仍是洗牌近似~~ **本批已交付** | 队列已按 `ActModel.GenerateRooms` 预生成（15 槽、弱 3 + 普通 12、GrabBag 下标、标签回避重抽、空袋重装、零抽取预扫；见 §8.3）。剩余：输入池是已支持子集、队列前的整幕前置消费（事件洗牌/遗物袋/古代人）未实现 ⇒ 流起点不同、精英/BOSS 队列未接入 | A / BASE-02 |
| 8 | 真实奖励/地图/房间缺失 | 奖励仍是占位升级；无地图（`act_{n}_map`）、营火/商店/宝箱/精英/BOSS/事件、幕转换、四幕终局 | A / BASE-02 |
| 9 | 流位置无法逐字对齐 | 原版在遭遇抽取前还有遗物袋/古代人洗牌与事件列表洗牌（各消耗若干抽取）；这些机制未实现，因此**抽取起点**与源不同（队列内部算法可以做到逐字一致） | A / BASE-02 |
| 10 | 加权选取的数值等价性 | 原版 `NextFloat(max)` + 浮点减法 vs 本实现的整数有界抽取 + 整数累积权重，只在 `d*max` 恰为整数/舍入跨边界时不同。需要 core 的"按流抽定点/浮点"通用原语才能真正逐位一致（最小复现已写入 `SliceEnemyStateMachineChecks` 的夹具形状） | 负责人（core 决策）+ A |
| 11 | 依赖遭遇位次的开场条件 | Nibbit 的 `_isAlone`/`IsFront` 等需要编队/位次事实；本批按单独出现的原版行为开场（见 §8.1.3 第 1 条） | A / BASE-02（批 2） |

## 10. 逐条未支持范围（`Describe()` 原文）

### 10.1 被排除的 14 条 act-1 遭遇

| 遭遇 | 原因（`ExcludedEncounters` 原文） |
| --- | --- |
| `base-spire:cubex-construct-normal` | 敌人 `base-spire:cubex-construct` 未加载（`startingPowers`/`Artifact` 未支持） |
| `base-spire:fogmog-normal` | 敌人 `base-spire:fogmog` 未加载（`summonEnemy`） |
| `base-spire:inklets-normal` | 敌人 `base-spire:inklet` 未加载（`startingPowers` + `SlipperyPower` 白名单缺口） |
| `base-spire:snapping-jaxfruit-normal` | 敌人 `base-spire:flyconid` 未加载（引用 `Frail`，该能力未进运行时白名单；行为已在批 1 支持） |
| `base-spire:vine-shambler-normal` | 敌人 `base-spire:vine-shambler` 未加载（引用 `TangledPower`，该能力未进运行时白名单） |
| `base-spire:flyconid-normal` | 带 `enemySlots(2)`：需要按权重/唯一选择组的随机编队程序 |
| `base-spire:ruby-raiders-normal` | 带 `enemySlots(3)`：同上 |
| `base-spire:slimes-weak` | 带 `enemySlots(3)`：同上 |
| `base-spire:slimes-normal` | 带 `formations(2)`：同上 |
| `base-spire:slithering-strangler-normal` | 带 `formations(3)`：同上（敌人行为已在批 1 支持） |
| `base-spire:bygone-effigy-elite` | 精英房间未接入：需要地图与房间类型流程 |
| `base-spire:byrdonis-elite` | 精英房间未接入：同上 |
| `base-spire:phrog-parasite-elite` | 精英房间未接入：同上 |
| `base-spire:vantom-boss` | BOSS 房间未接入：同上 |

（`base-spire:slimes-weak` / `cubex` / `fogmog` / `inklets` / `vine-shambler` 里，敌人**行为**本身的缺口在批 1
已消除；剩下的是能力白名单、`startingPowers`/`summonEnemy` 一类行动效果与遭遇编队。）

### 10.2 构建描述里的其它限制（`Limitations` 要点）

1. 遭遇队列：已按原版 GrabBag + 标签回避预生成整幕队列（15 槽 = 弱 3 + 普通 12，进入房间不抽数）；
   仍有的差别：输入池是已支持子集、队列前的整幕前置消费（事件洗牌/遗物袋/古代人）未实现 ⇒ 流起点不同、
   精英/BOSS 队列未接入、重抽循环有 2048 次上限、首次运行的固定顺序未建模（详见 §8.3.2）。
2. 房间流程：只有普通战斗房间；营火/商店/宝箱/精英/BOSS/事件与幕转换未接入。
3. 地图：`act_{n}_map` 命名流未使用，范围按遭遇池长度推进。
4. 出生生命：按 `Niche` 流掷点已接入，但**整幕前置消费**未接入 ⇒ 流位置还不与原版对齐。
5. 敌人获得格挡/施加能力时未过 Dexterity/Frail 等修正阶段。
6. 敌人行动已是状态机（权重/一次性/连续上限/冷却/有序条件/图）；已知差异见 §9 的第 10、11 项。
7. 奖励仍是切片占位（每间房一次长期牌升级）。
8. 药水机制未接入（药水栏为空）；遗物**已接入效果**但只覆盖 Burning Blood。
9. 胜利条件：不按房间计数发 Victory；最终 BOSS 后的终局事件未接入。
10. 敌人行动 DSL 未支持：`addCards`、`summonEnemy`、`startingPowers`、具名/位次/`LivingAllies` 条件、
    `escape`/`stealGold`/`if(…)` 效果；`maxUsesPerCombat > 1` 未支持。

## 11. 验证命令与返回码（可直接复跑）

```powershell
dotnet build AutoTheSpire.sln                                       # rc=0  0 错误 0 警告
dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests      # rc=0  95/95
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests # rc=0  297/297
dotnet run --project net-the-spire/Net.TheSpire.Tests               # rc=0  393 passed / 0 failed
python tools/doc-check/check_docs.py                                # 0 问题
python tools/dependency-check/check_dependencies.py                 # 10 passed / 0 failed
git diff --check                                                    # rc=0  干净
```

`BaseSpire.Slice.Tests` 会打印构建摘要、池、逐条排除原因、轨迹、状态机清单与整幕队列槽位（§5/§8/§8.1/§8.3）。
**未执行**：`tools/differential` 的 `test --suite regression|ironclad-run`（B 的入口未实现）、
严格双侧比较、正常四幕胜利、真实客户端取证、Release 性能测量。

## 12. RESUME HERE（下一批，按真实 Run 阻塞顺序）

0. **先补齐 §8.4 的注释损失**（可选但推荐，优先本线机制段落）：按 `artifacts/text-repair-report.txt`
   的 572 行清单分批补回丢失字符，纯文本补充、每批重跑 Slice 检查。
1. **能力白名单（`Frail`、`ConstrictPower` 等）**：它同时是 `snapping-jaxfruit-normal`、
   `ruby-raiders-normal`（+`TrackerRubyRaider`）、`flyconid-normal` 的前置，也是 `Flyconid`/`CorpseSlug`/
   `HunterKiller`/`LouseProgenitor`/`GlobeHead`/`Stabbot` 一类敌人的直接阻塞（§8.5.1）。
2. **遭遇编队**：`formations` / `enemySlots`（`weight`、`uniqueChoiceGroup`、`chooseRandomly`、槽位次序），
   抽取用遭遇派生种子 `(runSeed + floor + hash(entry))`（§7.2）；配 **位次** 事实以支持 Nibbit 一类
   依位次的开场条件（§9 第 11 项）。有了 1 之后它会一次解锁 3～5 条遭遇。
3. **精英/BOSS 队列与房间类型**：`for 15` 的精英队列、`rng.NextItem(AllBossEncounters)`、
   `GetUnlockedAncients` + 共享古代人子集；配合地图与房间类型流程。
4. **真实奖励与房间流程**：战斗奖励/选牌/跳过/容量、营火、商店、宝箱、精英、BOSS、事件（含事件内战斗返回）。
5. **地图与幕转换**：`act_{n}_map` 命名流 + `unknown_map_point` 房间类型、幕转换、最终 BOSS 后的四幕真实胜负。
6. **整幕前置消费**：事件列表洗牌、遗物袋、共享古代人——队列在 `up_front` 上的**起始位置**要与之对齐。
7. **长期状态**：事件战斗返回、Feed 最大生命跨房间、最后击杀余效不被终局截断。
8. **通用缺口（交负责人决策，不在 base 绕开）**：核心若提供"按流抽一个定点/浮点"的原语，加权选取即可与
   原版 `NextFloat` + 浮点减法**逐位一致**（§9 第 10 项）；内容语言的**库层 procedure**（core/0.2 模板目前
   不能包含游戏扩展声明）同属该决策。

以上任一项都不以短房间演示、人工清场、测试数量增长或超时替代验收。
