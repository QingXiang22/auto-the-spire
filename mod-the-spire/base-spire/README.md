# base-spire

`base-spire` 是依赖 `core-the-spire` 的原版内容规则包。活动实现使用 `BaseSpire/Slice` 的 Engine 切片、正式卡/Power 迁移子集和 `BaseSpire/SinglePlayer/` 工厂，尚非完整原版。使用共同工厂创建会话时，从下节开始；任务状态与验收看[当前清单](../../docs/review/tofix/README.md)。

编写内容时先看[base开发指南](../../docs/design/mod-the-spire/base-development-guide.md)和[modding指南](../../docs/design/mod-the-spire/modding-guide.md)；维护公共机制时看[可维护规则集指南](../../docs/design/mod-the-spire/maintainable-rule-sets.md)。从外部文件验证卡牌可使用[卡牌开发工具](../../tools/base-spire-dev/README.md)。

公共组包从`CombatSlicePackage.Build`转交`Slice/Compilation/CombatPackageComposer`。状态声明、伤害、Power、敌人、回合、遭遇、换房、开局、奖励与结算分别由领域编译模块拥有；依赖通过类型化符号分组与回调显式传入。理解职责见[base组织结构](../../docs/design/mod-the-spire/base-architecture.md)，查方法见[编译模块参考](../../docs/design/mod-the-spire/package-compilation-reference.md)，新增共享状态按[开发步骤](../../docs/design/mod-the-spire/base-development-guide.md#新增共享状态字段)执行。

活动版本为 Core.TheSpire/BaseSpire `0.4.0`、`engine/ir@0.4`、`core/0.3` 和卡牌作者语言 `base/0.0.2`。新作者语法见[base/0.0.2 参考](../../docs/dsl/0.0.2/authoring-reference.md)，同包文本机制绑定见[core/0.3 参考](../../docs/dsl/core/0.3/authoring-reference.md)。旧冻结文档保留历史合同，不代表当前构建仍使用旧身份。

在仓库根目录运行活动检查：

```powershell
dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests
```

2026-10-04，旧注册器、地图生成器及旧 hooks 已移入[Legacy 归档](../../archive/legacy-sts/README.md)。旧检查在 archive 单独运行。原有能力说明保留在文末的历史段落中。

## 正式单人入口（BASE-01，2026-10-05）

正式构建的**唯一组包/会话工厂**位于 `BaseSpire/SinglePlayer/`。B 的对拍后端与 C 的公开 Host 使用该工厂创建一局。

以下签名定义工厂契约。修改接口时，同步本节和本线的阶段交接：

```csharp
namespace BaseSpire.SinglePlayer;

public sealed record RunConfiguration            // 不可变整局配置；GameSeed 不属于配置
{
    public required string Character { get; init; }          // "Ironclad" 或 "CHARACTER.IRONCLAD"
    public int Ascension { get; init; }                       // 当前只支持 0
    public UnlockPolicy Unlocks { get; init; } = UnlockPolicy.All;
    public RunMode Mode { get; init; } = RunMode.SinglePlayer;
    public string SourceBaselineId { get; init; } = BaseSpireSourceBaselines.Frozen;  // 只接受已登记基线
    public string ScopeId { get; init; } = BaseSpireScope.Dev;                        // 只接受 dev
    public static RunConfiguration Ironclad(int ascension = 0, string scopeId = BaseSpireScope.Dev);
    public BaseSpireCharacter Resolve();     // 角色/进阶/范围/模式/解锁/基线全部在建局前校验
}

public static class BaseSpireSourceBaselines
{
    public const string Frozen = "sts2-frozen-2026-10-05";   // 当前唯一支持的源基线
    public static IReadOnlyList<string> Supported { get; }   // 未知 id 一律拒绝
    public static bool IsSupported(string baselineId);
    public static BaseSpireSourceBaseline Require(string baselineId);   // 未知 → ArgumentException
    public static BaseSpireSourceBaseline Current { get; }   // 含 EvidencePaths（file:line）
}

public static class BaseSpireScope
{
    public const string Dev;        // base-spire-singleplayer-dev-v1（**当前唯一可实现**的范围）
    public const string Ironclad;   // base-spire-ironclad-singleplayer-v1（已登记，尚未实现）
    public const string Full;       // base-spire-singleplayer-v1（已登记，尚未实现）
    public static IReadOnlyList<string> Known { get; }        // 三个 id（区分"未知"与"已知未实现"）
    public static IReadOnlyList<string> Implemented { get; }   // 当前 = [Dev]
    public static bool IsImplemented(string scopeId);
    public static string NotImplementedReason(string scopeId);
}

public sealed class BaseSpireRuntimeFactory
{
    public BaseSpireRuntimeFactory(RunConfiguration configuration);
    public RunConfiguration Configuration { get; }
    public BaseSpireCharacter Character { get; }
    public RuleSet Rules { get; }                             // 只读规则集（会话状态不在这里）
    public BaseSpireBuildDescription Describe();               // 只读规则/构建描述（不含每局 seed）
    public BaseSpireRunSession CreateSession(string gameSeed); // 已配置、**尚未 Start** 的独立会话
    public const string ProjectionId = "base-spire:PlayerView";
}

public sealed record BaseSpireRunSession
{
    public RunConfiguration Configuration { get; }
    public BaseSpireBuildDescription Build { get; }
    public string RequestedSeed { get; }   // 原始请求
    public string AcceptedSeed { get; }    // 实际接受（命令行 seed 路径不做标准化）
    public uint RunSeed { get; }
    public Session Session { get; }        // 调用方 Start() 一次；工厂不代启动
    public ParticipantId Participant { get; }
    public string ParticipantRole { get; }
    public string ProjectionId { get; }
    public IReadOnlyList<(string Type, string Stream, uint Seed)> SeededStreams { get; }
    public string SessionIdentity { get; } // 含 seed 指纹：证据，不作公开 ruleset_id
    public void Start();
}
```

Host、工具和对拍后端使用同一创建流程：

```csharp
var factory = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad());   // ScopeId 只接受 dev（见下）
var build   = factory.Describe();                                          // 目录/身份/范围与 unsupported
var run     = factory.CreateSession("ENV-IRONCLAD-00");                    // 已配置、未 Start、RNG 未消费
run.Start();                                                               // 每个新 Session 恰好一次
// 之后：run.Session.LegalActions(run.Participant) / Submit / Answer / Observe(run.Participant, run.ProjectionId)
```

**dev 范围的接入限制**

B/C 接入时，使用下表中的实际支持范围；不能发布完整范围身份。

| 维度 | 现在 | 拒绝方式 |
| --- | --- | --- |
| 范围 `ScopeId` | 只可实现 `base-spire-singleplayer-dev-v1` | `Ironclad` / `full` → `NotSupportedException`（构造工厂时，**不生成 Session、不发布描述**）；未知 id → `ArgumentException` |
| 角色 `Character` | 只有 `Ironclad` | 其余四角色 → `NotSupportedException`（原因含"卡池/机制未迁移"） |
| 进阶 `Ascension` | 只有 `0` | `>0` → `NotSupportedException`（`tough`/`deadly` 变体未接入） |
| 解锁 / 模式 | `All` / `SinglePlayer` | 其它值 → `NotSupportedException` |
| 源基线 `SourceBaselineId` | 只有 `BaseSpireSourceBaselines.Frozen`（`sts2-frozen-2026-10-05`） | 未知基线 → `ArgumentException`（不按名字继续跑） |
| 内容范围 | 整幕队列 **15 槽**（弱 3 + 普通 12）由已支持的 6 条 act-1 遭遇填充（弱 3 + 普通 3，含 `mawler-normal`）；无地图/房间类型/真实奖励/幕转换 | `Describe().ExcludedEncounters` / `UnsupportedContent` / `Limitations` 逐条列原因 |

范围由**实际能力闭合**开启（`BaseSpireScope.Implemented`），改字符串不算实现。

契约与身份字段：

- **配置固定**：Character / Ascension / Unlocks / Mode / ScopeId / SourceBaselineId 属于构建配置；
  `gameSeed` 每局单独传入。上表的每一项都在**建局前**拒绝，不退回 Ironclad / 进阶 0 / 当前基线。
- **未 Start 的会话**：返回时 `Session.Random.TotalDraws == 0`，且状态里没有任何玩家实例；
  Start 由控制入口调用**一次**（工厂不启动，调用方不得启动第二次）。
- **原版随机**：算法 `mega-random/xoshiro256ss-splitmix64@1`、映射 `vanilla-uniform@1`；
  字符串 seed 按 `uint(hash32(seed))` 得 run seed，12 条 run 流与 3 条 player 流按
  `seed + hash32(snake_case(名))` 显式种好；`SeededStreams` 给出 (原版流, 引擎流名, 种子) 全表。
  流名映射为显式表（`VanillaRandomLayout.EngineStreamName`）：`Shuffle` → `base-spire:combat-draw`、
  `CombatTargets` → `base-spire:combat-random-target`、`CombatCardSelection` → `base-spire:combat-random-card`、
  其余 → `base-spire:run:<snake>`；player 流 → `base-spire:player:<snake>`。
- **身份（三层，职责分开）**：
  `Describe().RulesetId` = 静态规则身份（`RuleSet.Identity` 摘要：内容/链接/编译/数值/Profile）；
  `Describe().StreamLayoutDigest` = 随机布局身份（流名映射 + 算法/映射 id）；
  `Describe().PublicBuildId` = **公开构建身份**（范围 + 源基线 + 规则集 + 内容摘要 + 布局 +
  公开语义元数据摘要，见下文「公开语义元数据」）。
  同一公开构建身份不得对应行为不同的运行配置；`BaseSpireRunSession.SessionIdentity` 含 seed 指纹，
  只进证据与 case，不作公开 ruleset_id。
- **来源可核对（R3）**：`Describe().SourceBaselineId` 来自注册表；`SourceEvidencePaths` 给出 file:line 证据；
  `SourceDigests` / `ContentDigest` 是**实际加载内容**的摘要（可当场复算），而不是来源名称。
- **牌序是规则（R2）**：起始牌组按 `sts2/src/Core/Models/Characters/Ironclad.cs:43-55` 的声明顺序
  （Strike×5 → Defend×4 → Bash）展开，**不按内容 id 排序**；压缩成 `deck { … xN }` 时会当场验证
  "展开后逐位等于声明顺序"。构建期未做任何排序 ⇒ 洗牌输入顺序即声明顺序。
- **正式内容**：卡/能力来自 `ContentSource/*.spire`（能力同时加载 `enemies*.spire` 里的 `power` 声明）；
  敌人来自 `ContentSource/enemies*.spire`（`SpireEnemySource`）；遭遇与幕池来自
  `Content/base-spire.encounters.json` + `Content/base-spire.acts.json`；
  起始牌组/生命/金币/遗物来自 `BaseSpireCharacters`（`sts2/src/Core/Models/Characters/*.cs`）。
  **不再**读取 `Slice/Content/slice-content.author`、不用诊断牌组、不用固定遭遇。
- **敌人行动状态机（BASE-02 批 1）**：`behavior` 的 `loop`/`cycle`/`startWith`/`startWithChoice`/`graph`/
  `chooseWithLimits`/`chooseWithoutRepeat`/`chooseByCondition` 解析成与冻结源码
  `MonsterMoveStateMachine` 同构的状态图（`SliceEnemyBehavior.cs`），状态、行动日志、冷却窗口与已选意图都存在
  **Engine 类型化状态**里（隐藏字段 `MachineState`/`PerformedFirstMove`/`LastMove`/`MoveRunLength`/
  `MoveHistory*`/`MoveUsed_*`），公开的仍然只有 `NextMove`（意图）。选取点与源码相同：**出生时**与
  **每个玩家回合开始**（两次调用幂等，首动不掷）；每次落到随机分支**恰好一次** `monster_ai` 抽取。
  作者语法、生成 IR 与已知差异登记在 [dsl-usage-current 第 6 节](../../docs/dsl/0.0.1/dsl-usage-current.md#6-声明敌人与行动)。
- **整幕遭遇队列（BASE-02 批 2）**：幕开始时用原版 `up_front` 流**一次生成**整幕队列
  （由`Compilation/EncounterCompilation.EnqueueProcedure`生成`EnqueueEncounter_weak/regular`）：前 `NumberOfWeakEncounters`(3) 槽取弱池、
  补齐到 `GetNumberOfRooms(false)`(15) 槽；每个池一个 GrabBag（权重全 1），抓取按"不与上一个入队遭遇
  **共享标签**且不是它自己"重抽，预扫发现没有可用成员时**零抽取**再无约束抓一次，袋子空了按池顺序重装。
  队列落在 `EncounterQueue` 集合（隐藏的 `Index` + 遭遇 id），每间房只按房间号查表、**进入房间不抽数**。
  这段房间数量、按房间号查表及已支持子集说明适用于非地图dev范围。dev-map真实新Run由`NewRunCompilation.NewRun`接完整源池，普通/精英使用独立队列与访问计数；换房消费在`BattleRoomCompilation.RoomTransition`。源池成员存在不表示敌人行为已支持，缺失时具名失败。
- **敌人 `addCards`（BASE-02 批 3）**：`addCards(<卡>, count=n, pile=hand|draw|discard, target=player,
  position=bottom|top|random)` 往玩家牌区放 **n 张全新实例**（普通形态 + 实例变量初值），位置语义对齐
  `CardPileCmd.Add`：`bottom` 追加、`top` 插到最前、`random` **每张**各抽一次
  `Rng.Shuffle.NextInt(当前张数 + 1)`（= 本包 `combat-draw` 流）。卡名经**已加载卡牌**建表解析，
  悬空引用整只敌人记为未支持。已知范围：只支持 `target=player`；不为生成牌展开 `on_enter_combat` 钩子
  （当前会被放入的状态牌都没有声明该钩子，已在代码与描述里记账）。
- **未支持项**：`Describe().ExcludedEncounters` / `UnsupportedContent` / `Limitations` 逐条给出原因；
  dev 范围不得把这些当成"内容不在原版里"。当前 dev 池为 act-1 的弱/普通遭遇中**敌人行动程序已支持**的子集。

## 公开语义元数据（ENV-T03 A 线子包，2026-10-08）

正式工厂提供公开出口所必需的**不可变游戏语义元数据**：通用 `RuleSet` 声明缺的、不能仅凭字段/集合名字
推导的四类含义。它**不是**逐字段/逐动作登记表（那些由声明与所选投影派生），也不承载每局可变态：

```csharp
var factory = new BaseSpireRuntimeFactory(RunConfiguration.Ironclad());
BasePublicSemantics semantics = factory.PublicSemantics;   // 只读、无 seed、同配置稳定
string digest = factory.Describe().SemanticsDigest;        // = semantics.Digest
string identity = factory.Describe().PublicBuildId;        // 已折入 semantics 摘要
```

| 成员 | 内容 | 由谁定义 |
| --- | --- | --- |
| `Content` | `类型限定名 → { FieldName, ContentIds }`：`base-spire:Card/Power/Monster/Relic` 的内容 ID 目录，ids 来自**实际加载内容**（未支持/未降级内容不进目录） | base（Engine 的 `FieldDefinition` 只说"这是 string 字段"，枚举不出取值） |
| `Collections` | `集合限定名 → Pile/Sequence/Group/NotPublished`：牌堆引用哪个 `pile_ref` 实体种类、是否有公开位置坐标、有序流程/归组的 owner **实体种类**、或具名不发布的规则号与理由 | base（集合用途与位置/归属不能从集合名推导） |
| `KnownPositions` | `KnownCard` 事实的卡牌/牌堆/位置字段名 + `标记值 → 引擎集合` 词表（当前只有 `draw` → `base-spire:DrawPile`） | base（只知道字段名不够；标记词表是包自己的词表） |
| `TerminalOutcomes` | `base-spire:Victory → Win`、`base-spire:Defeat → Loss`；`WireName()` 给出 `win`/`loss`/`other` | base（终局名字不同不等于 win/loss） |

契约：

- **构造期闭合**：`BasePublicSemanticsBuilder.Build` 对真实 `RuleSet` + 所选投影 + 实际内容逐条校验。
  规则集里每条集合都必须有用途、每个终局都必须有解释、内容字段必须是真实 string 字段、pile_ref/owner
  （group 与 sequence 的 owner 种类）/已知位置事实种类必须在投影里，否则**显式失败**（不允许静默丢弃公开事实）。
- **顺序不得被载荷丢掉**（2026-10-08 接续）：引擎声明 `identity/order` 公开且成员种类在投影里的集合，
  必须发布为能保留真实顺序的载荷——牌区用 `Pile`，其余用 `Sequence(owner种类)`；**不允许**落成无序
  `Group`，也**不允许**用具名不发布把它说成"没有公开语义"。反过来，`Sequence` 不得用在引擎声明
  隐藏成员/顺序的集合上（大小/顺序隐藏的保证不得泄漏）。`NotPublished` 只允许用在"成员种类不在所选
  投影里"这一种结构原因上，规则号标明受阻（`*-BLOCKED-*`），并在交接里给出最小可表示形状与受影响范围。
- **只读**：`Content`/`Collections`/`TerminalOutcomes`/`KnownPositions` 与目录列表都是只读包装，
  外部改写会抛 `NotSupportedException`。每次构造产出独立实例。
- **身份**：`Digest` = 元数据的稳定摘要（不含 seed）；工厂把它作为 `PublicBuildId` 的一个输入维度，
  内容或语义变化即换身份。`CanonicalText()` 是逐行可核对的规范化文本（证据用）。
- **依赖方向**：base **不引用** `net-the-spire` 的程序集或 DTO。公开出口一侧（C 线）负责把
  `Content`/`Collections`/`KnownPositions`/`TerminalOutcomes` 翻译成协议侧的形状；base 只声明游戏语义。
- **未接通**：本包只提供元数据，不表示 Host/公开出口已经接线；当前正式内容不产生 `KnownCard` 事实
  （`top_from_discard` 类效果未进入 dev 内容），角色是 schema 层声明，运行时事实由包程序维护。

检查：`BaseSpire.Slice.Tests/PublicSemanticsChecks.cs` 覆盖闭合、目录=实际加载内容、集合可见性解释
（牌堆/有序流程/受阻不发布）、公开顺序不落 NotPublished 与丢序 Group 的判别、owner 进入规范化文本、
角色绑定、终局解释、真实会话里 pile_ref/owner/sequence owner 种类唯一解析、同配置稳定/无 seed、
内容变更影响身份与只读防篡改。

## 正式地图房间（`BaseSpireScope.DevMap`）

`dev-map` 由地图 `ChooseMapPoint` 进房，房型与它们的相位（`Run.MapPhase` 字段域 0..8）：

| 房型 | 相位 | 模块 |
| --- | ---: | --- |
| 普通/精英/Boss 战斗 | 1 | `BattleRoomCompilation.RoomTransition` + 普通/精英独立队列 |
| 营火 | 4 | `RestSiteWiring.Register`（休息/打铁，完成才关闭访问） |
| 宝箱 | 5 | `TreasureRoomWiring`（稀有度掷点、袋尾取货、`TakeTreasureRelic`/`SkipTreasureRelic`） |
| 商店 | 7 | `ShopRoomWiring`（见下） |
| 事件 | 8 | `Rooms/Unknown/UnknownRoomEntry` + `Rooms/Events/EventVisitLifecycle` + `Slice/Rooms/Events/<Name>.cs` |

**问号房（`MapPointType.Unknown`）**：按源 `UnknownMapPointOdds` 判定实际房型（初值/float32 累加/阈值/
命中复位/未选赔率增加，赔率以 float32 位型跨房间持久，判定资产 `base-spire:UnknownMapPointOddsRoll`）、
按源 `BuildRoomTypeBlacklist` 拉黑 Shop；判定结果写 `Run.ActualRoomType`，**地图节点类型保持 `Unknown`**。
判成战斗/精英/宝箱/商店时复用同一批 `MapRoomEntry.Standard` 入口与守卫；判成事件时按源
`PullNextEvent` 消费 Start 预生成的源事件队列（资格 + 已访问跳过 + `Run.EventsVisited` 计数）并进入
该事件的独立模块（`Run.CurrentEventId`）。源池选到未实现身份时具名失败，不缩池、不换事件、不伪造离场。
检查：`BaseSpire.Slice.Tests -- --unknown-room-only`（10 项；含固定 seed `ENV-T03-SEED-L` 的独立复算金标准）。

未实现的房型由 `MapRoomWiring` 具名 `Fail`（节点保留、字段零改动）。

**商店**（`ShopRoomWiring.cs` + `ShopCompute.cs` + `ColorlessShopPool.cs`，源 `MerchantInventory`/
`MerchantRoom`）：进入时一次性生成 14 个商品位（5 彩卡 + 2 无色 + 3 遗物 + 3 药水 + 1 删牌），
抽数、价格与稀有度按源逐笔消费（正常路径 `Shops` 27 / `Rewards` 14）；`Shops` 折扣格、彩卡稀有度
只读奖励子包的 `RewardCardOdds` 持久偏移，无色按 `ColorlessCardPool` 的**源顺序 + 源稀有度**分桶抽样。
买卡/买药水/买遗物把结果写进长期牌组、药水栏与持有遗物；删牌用正式 `Decide` 选长期牌实例，
累计次数存在 `Run.ShopCardRemovalsUsed`（下一间涨价）。买不起、满栏、已售、不可买**不在候选域**
且被守卫拒绝（引擎抛 `IllegalAction`，会话保持 Running，仍可离店），未实现效果的商品保留完整源身份
并写明 `UnsupportedReason`。检查：`BaseSpire.Slice.Tests -- --shop-flow-only`（11 项）。

## 历史注册器能力说明

以下为旧注册器的原有说明，保留原文供查证。其中的命令、接口和覆盖数字属于归档路径，不能用作活动 Engine 的使用或验收依据。

`base-spire` 是原版内容规则包，依赖 `core-the-spire`。Ironclad 卡牌行为和 Power 已广泛使用 DSL 描述；当前 DSL fixture 与原版 87 张卡池名单对齐：85 张单人池卡和 2 张多人专用卡。Well-Laid Plans 作为非 Ironclad 内容单独建模，不加入该卡池。详情见 `docs/design/mod-the-spire/ironclad-dsl-coverage.md`。效果覆盖攻击/格挡、抽牌与牌堆移动、选牌与升级、消耗触发、克隆与实例变量、回合攻击历史触发、升级关键字、Power 事件订阅、卡牌实例延迟累积与反应效果、随机目标、AoE、动态计数、回合条件、能量上限、易伤倍率和临时力量生命周期；卡牌规则与池归属仍需持续对照原版源码审计。

已迁入部分 Ironclad 遗物：Burning Blood、Anchor、Vajra、Charon's Ashes、Self-Forming Clay、Red Skull、Demon Tongue、Paper Phrog 和 Ruined Helmet。Fire、Block、Strength、Blessing of the Forge、Beetle Juice 和 Flex Potion 由 `potions.spire` 定义；Flex Potion 与 Setup Strike/Mangle 共用 Power 生命周期。战斗奖励可掉落并领取药水，固定槽位支持消费后保留空位，Potion Belt 扩容 2 格。完整药水池和非战斗/自动药水仍待迁移。

三十七种怪物由 `enemies.spire` 描述，遇敌阵容仍使用 JSON。敌人行为 DSL 支持固定/随机动作图、基于战斗指标的有序条件分支和动态召唤。Burning Blood 在整局战斗胜利后回复 6 点生命，并受最大生命限制。`BaseSpireMapGenerator` 提供恢复源码中的四种 Act 单人标准地图生成器、三幕默认路线定义及 Unknown 房间基础概率配置；完整原版内容和 seed 差分仍待扩展。

卡牌、药水、Power 和怪物行为在注册时从 `.spire` DSL 编译成强类型节点；环境 step 只执行节点，不在结算热路径解析源码。DSL 操作包括顺序、条件、重复、攻击、生命损失、格挡、施加状态、抽牌/移动牌、生成牌、实例级选牌、资源修改、随机生成战斗药水和选择。复杂内容可通过 `ICustomEffectHandler` 扩展，但只能调用 core 提供的效果命令，不能直接改写运行态。

从仓库根目录运行 `dotnet run --project core-the-spire/Core.TheSpire.Tests` 执行无外部测试包依赖的战斗切片检查。源规则审计依据及待读领域见 `docs/design/sts2-core-source-audit.md`。
