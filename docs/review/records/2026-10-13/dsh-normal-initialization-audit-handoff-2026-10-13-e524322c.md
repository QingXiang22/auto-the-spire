# 正常初始化兼容审计交接 — 2026-10-13

> 2026-10-19 整理：切换前历史证据：旧 C# 路径和会话已经退出当前生产；保留当时交付与未验收范围，不按本文继续派单。 当前任务见[主清单](../../tofix/README.md)，后续架构见[内容与行为重构目标](../../../design/mod-the-spire/content-behavior-refactor-goals.md)。


本包确认现有公开参考 API 不足以在首次抽数前接入冻结源兼容初始化，交付最小公开 bootstrap 接口提案及真实受阻证据。没有修改参考或游戏生产源码，没有把正常对拍改为 passed。

- 任务：`ENV-T04-normal-initialization-audit-20261013`（正常初始化兼容审计）。
- 执行会话：`session-e524322c-614d-4142-b393-2ab4a0c80829`；request：`787213fa-bba3-4abe-8dfb-921793b3d22c`。
- 身份及唯一交接路径：[实际派发记录](../../../../.audit/dsh-normal-initialization-audit-20261013/task-record.json)。负责人回填的 handoff_path 与本文件一致；执行者未修改记录。
- 当前认领及完成条件：[主清单 ENV-T04](../../tofix/README.md#env-t04)。开工时清单记旧 B `session-011c4072-7363-48da-bc48-160af934e6ab` 已停止 seq725、文件主责释放，本包认领已登记，派发记录 accepted。停止事实来自负责人的清单核验，未自行操作远端旧会话。
- 范围：[本包派单](../../../../.audit/dsh-normal-initialization-audit-20261013/task.txt)。主清单、base/core/net/reference 全部只读；本包只新增隔离验证工程、本包证据和此唯一交接。工具 C# 与两个 suite 均未修改。

## 负责人接手先做什么

1. 先裁决下文公开 bootstrap 提案；只有另行授权参考主责后才能实施。仅公开现有接收 RunRngSet 的 private 构造仍不够，因为地图及玩家/内容具名派生继续使用参考原生 hash64。
2. 对动态怪物 seed 表达式单独核验冻结源/客户端。冻结源与参考的表达式形状不同，不能把它包装成已确认的 hash32 兼容修复。此项在正常首战中可达，但不是本次 boundary0 地图首个差异的原因。
3. 若批准新参考接口和固定 DLL，另派工具包实现共同动作绑定及 HP 同义投影，先检查首次共同地图边界，再逐步推进真实首战/奖励。禁止在旧分叉后的奖励差异上裁决奖励算法。

本包到此停止，不认领下一包，不提交、发布、清理、派任务或修改参考。父 T04、M01 及完整环境不随本审计关闭。

## 兼容矩阵与首次消费

H32 表示冻结源对 UTF-16 字符的两路有符号 int 循环，最后转 uint；所有源 uint 加法按 32 位回绕。H64 表示参考 UTF-8 XxHash64(seed 0)，ulong 加法不是源 uint 回绕。两者均不能用进程随机化的 string.GetHashCode 替代。

| 域 | 冻结源 / 参考 | 首次消费与现有适配覆盖 |
| --- | --- | --- |
| 根 seed | H32(完整原始字符串) / H64，参考另解释 old 前缀 | 根 hash 不抽数，但支配后续派生。Apply 不改变只读 Seed；原始字符串不许改 |
| 12条 run 具名流 | uint(root + H32(snake_case枚举)) / ulong(root + H64(name)) | 构造流不抽数；Apply 只 MockRng Counter=0 的这12流，保持顺序生成器/抽样，不覆盖根或其他对象 |
| 地图 | 源 `new Rng(root,"act_1_map")` / 参考同名但原生 hash64 | 参考 RunState 构造内 GetMapPointTypes/StandardActMap 即消费，早于 Apply；GenerateMap 再调用仍用错误根/name派生 |
| 玩家 Rewards/Shops/Transformations | 源 uint(H32(string)+slot)，再具名H32 / 参考直接 ulong run.Seed，再具名H64 | Player.CreateForNewRun 创建 PlayerRng 和 Odds，起始牌/遗物及 UpFront 袋随后初始化。当前单人 slot0 只消除槽偏移差异，不能消除hash差异；Apply不覆盖玩家流 |
| UpFront 内容/遭遇 | 共享袋→角色袋→共享古代人分配→逐幕事件/普通/15精英/Boss/古代人（双Boss条件追加） | 参考玩家创建时袋开始消费；遭遇在首次 EnsureRoomsGenerated 延迟生成。Apply对此流播种可生效，但池资格/顺序和抽样仍须独立核验，不凭种子一致宣称全内容兼容 |
| 玩家 Rewards | 源派生的 Rewards / 参考原生玩家流 | 此前无机制消费时，胜利 ResolveOutcomeAsync→RewardsSet.GenerateFor 才消费：药水存在判定→金币→存在药水→卡候选；更早奖励/事件机制可以提前消费，bootstrap必须在玩家创建前绑定 |
| 玩家 Shops | 同上 | 商店库存生成首先选折扣槽位，再生成库存/价格；FakeMerchant也可消费。新2例未执行商店 |
| 玩家 Transformations | 同上 | 变形机制首次执行时消费，如 Claws/LeafyPoultice→CardCmd.Transform；新2例未执行该域 |
| 动态内容/事件 | 源 uint(root+适用slot+H32(id.Entry)+mixin) / 参考事件 root+slot0+H64(id.Entry) | EventModel.Begin→CalculateVars/GenerateInitialOptions 可首次抽数；不是run流，Apply不覆盖。当前首战前缀不走事件 |
| 动态怪物 | 源 nullable/?? seed表达式 / 参考 root+col+row+actIndex+CombatId累加 | 战斗创建怪物后AI首次抽数；Niche HP也在创建中抽数。根修复不能自动证明怪物派生同义，需另裁决 |
| 其他动态域 | act_selection、spoils_map及内容自建Rng | 无幕参数的默认构造还会先进行幕选择；本两例显式Overgrowth不消费act_selection。不得把该窄范围扩展成全Run兼容 |

### 精确源定位

- 根/流：[源 RunRngSet:104–119](../../../../sts2/src/Core/Runs/RunRngSet.cs#L104-L119)、[源 StringHelper:120–134](../../../../sts2/src/Core/Helpers/StringHelper.cs#L120-L134)、[源 Rng:47–55](../../../../sts2/src/Core/Random/Rng.cs#L47-L55)；对照[参考根构造:62–87](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunRngSet.cs#L62-L87)、[参考 hash:36–70](../../../../reference/sts2-sim/src/Sts2Sim.Core/Helpers/StringHelper.cs#L36-L70)、[参考具名Rng:48–53](../../../../reference/sts2-sim/src/Sts2Sim.Core/Random/Rng.cs#L48-L53)。
- 地图：[源 StandardActMap:111–114](../../../../sts2/src/Core/Map/StandardActMap.cs#L111-L114)、[参考构造:153–191](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs#L153-L191)、[参考地图:371–385](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs#L371-L385)。
- 玩家：[源 InitializeSeed:324–328](../../../../sts2/src/Core/Entities/Players/Player.cs#L324-L328)、[源 PlayerRngSet:31–47](../../../../sts2/src/Core/Random/PlayerRngSet.cs#L31-L47)、[参考 Player创建:143–178](../../../../reference/sts2-sim/src/Sts2Sim.Core/Entities/Players/Player.cs#L143-L178)、[参考 PlayerRngSet:28–46](../../../../reference/sts2-sim/src/Sts2Sim.Core/Random/PlayerRngSet.cs#L28-L46)。
- 源顺序：[InitializeNewRun](../../../../sts2/src/Core/Runs/RunManager.cs#L445-L460)、[GenerateRooms](../../../../sts2/src/Core/Runs/RunManager.cs#L668-L690)、[ActModel预抽](../../../../sts2/src/Core/Models/ActModel.cs#L331-L385)、[GenerateMap](../../../../sts2/src/Core/Runs/RunManager.cs#L724-L755)；参考对应[袋与逐幕预抽](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs#L239-L352)。地图与UpFront虽不同流，地图hooks可读玩家状态，因此不得把当前参考“构造先地图”默认当作源生命周期等价。
- Rewards/商店：[参考奖励生成](../../../../reference/sts2-sim/src/Sts2Sim.Core/Rewards/RewardsSet.cs#L39-L100)、[参考商店首次抽取](../../../../reference/sts2-sim/src/Sts2Sim.Core/Entities/Merchant/MerchantInventory.cs#L52-L78)。
- 动态：[源事件](../../../../sts2/src/Core/Models/EventModel.cs#L238)、[参考事件](../../../../reference/sts2-sim/src/Sts2Sim.Core/Models/EventModel.cs#L118-L136)、[源怪物](../../../../sts2/src/Core/Combat/CombatState.cs#L232-L246)、[参考怪物](../../../../reference/sts2-sim/src/Sts2Sim.Core/Combat/CombatState.cs#L264-L286)。冻结源有坐标时第一项非null，会先取root+col；参考累加行/幕/ID。这里仅报告源码差异，不能替负责人裁定恢复源码表达式或客户端实际行为。

### 公开入口排除结果

[现有 Apply](../../../../tools/base-migration-baseline/differential/RngSourceCompat.cs#L328-L353)逐流检查Counter并替换，只覆盖run流；它也不是完整bootstrap的原子校验，后面的流若已消费，前面流可能已替换。新接口应先验证所有输入，再创建/绑定任何对象。

[RunRngSet.FromSave](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunRngSet.cs#L239-L259)仍由原字符串构造根；CloneExact保留根，CloneReseeded只重播指定流且继续原生name hash。PlayerRngSet的[LoadFromSerializable](../../../../reference/sts2-sim/src/Sts2Sim.Core/Random/PlayerRngSet.cs#L203-L212)要求现根Seed一致，没有正常创建玩家前注入的入口。即使序列化能操作部分流，也不能修正已生成地图或冻结源完整初始化。没有使用移植/存档伪装正常新Run、私有反射或old改seed。

## 最小公开参考 bootstrap 提案（未批准、未实施）

目标是只开放“派生与初始化顺序”，仍由参考自身生成地图、牌组、遭遇、奖励及抽样。不能接收base地图或任何已消费运行事实。下面签名为拟新增API，不是现有可调用API。

建议新增参考 `src/Sts2Sim.Core/Random/SequentialSeedDerivation.cs`：

```csharp
public interface ISequentialSeedDerivation
{
    string Identity { get; }
    ulong RootSeed(string originalSeed);
    ulong PlayerSeed(ulong rootSeed, int slotIndex);
    ulong NamedSeed(ulong domainSeed, string exactName);
    ulong ContentSeed(ulong rootSeed, int slotIndex, string entry, uint mixin);
}
```

该接口仅返回播种数，不返回地图、样本、池或Rng状态。冻结源实现由工具独立H32/uint回绕派生，原始string完整保留；参考默认原生H64/old解释行为不变。动态怪物的坐标表达式不混进NamedSeed：需负责人确认后由独立版本化派生契约接入，不能直接照参考累加或猜测源意图。

拟在[RunRngSet](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunRngSet.cs)增加：

```csharp
public static RunRngSet CreateSequential(
    string originalSeed, ISequentialSeedDerivation derivation);
```

拟在[PlayerRngSet](../../../../reference/sts2-sim/src/Sts2Sim.Core/Random/PlayerRngSet.cs)增加：

```csharp
public static PlayerRngSet CreateSequential(
    ulong rootSeed, int slotIndex, ISequentialSeedDerivation derivation);
```

拟在[RunState](../../../../reference/sts2-sim/src/Sts2Sim.Core/Runs/RunState.cs)增加唯一正常创建出口：

```csharp
public static RunState CreateSequentialForNewRun(
    string originalSeed,
    IReadOnlyList<ActDefinition> acts,
    IReadOnlyList<CharacterModel> characters,
    ISequentialSeedDerivation derivation,
    int ascensionLevel = 0,
    HeadlessProgress? progress = null);
```

内部创建未消费run/玩家流并绑定policy；[Player.CreateForNewRun](../../../../reference/sts2-sim/src/Sts2Sim.Core/Entities/Players/Player.cs#L143-L178)通过RunState固定的policy和槽位创建PlayerRng及Odds，而不是创建完再覆盖。新bootstrap不能复用构造先GenerateCurrentMap的顺序，应在未抽数装配后按源顺序：共享/角色袋→进阶效果→全部共享古代人子集分配→逐幕事件/普通/精英/Boss/古代人预抽→当前地图原生生成与hooks→返回可旅行状态。不得重复生成、补抽或提前访问房间触发第二次初始化；现有默认构造保持原语义，新顺序明确登记偏离/兼容身份。

地图 `GenerateCurrentMap`、默认幕选择、事件/其他内容的`new Rng`必须通过已绑定派生数构造参考裸`Rng(ulong)`，不再把源根送入原生具名hash64重算；仅公开RunState构造不能解决这一点。原生MegaRandom和所有抽样/地图算法保留。怪物初始seed待单独裁决，不允许完整兼容身份先于它成立。

### 拒绝与身份合同

- 最小接口不接收现有RunState/Player/Rng/存档，因此从类型入口排除已消费运行输入。不得新增“给现有Run换policy”的setter。
- 若负责人选择接收预建RunRngSet的替代方案，必须原子拒绝任一流Counter非0、keyed模式、已生成地图/房间/袋、已有玩家/访问历史、原字符串不符及缺流；全部校验必须先于修改。Counter为0也不足证明未初始化地图，须以bootstrap生命周期标记保证。
- 不开放地图factory的任意结果注入。若需要factory，输入应仅为参考自行构造的新Rng及参考act，且输出来源限制要成为接口契约；本提案优先只注入seed派生。
- 显式身份建议 `sequential-source-bootstrap-sts2-frozen@1`，不是扩张既有`source-compat-sts2-frozen@1`的12流范围，也不扩大`vanilla-compatible@1`证明范围。原字符串、派生算法/32位溢出版本、源摘要、bootstrap顺序、acts/character/slot、所有域播种、参考DLL及适配源码hash进入参考身份和manifest。
- Clone/存档必须保留policy身份；旧/新policy与keyed互换明确拒绝。重放不得凭同string接受不同派生/顺序身份。
- 批准实现后的最小检查：独立向量覆盖高位/回绕及old完整字符串；检查第一地图样本前policy已绑定、玩家三流种子与首次指纹；任何已消费输入/重复bootstrap拒绝且无副作用。随后两例共同动作前缀实际执行；不能通过改旧17/维度或只比计数证明兼容。

## HP、实例与共同动作的工具语义

[base Facts](../../../../tools/base-migration-baseline/differential/NormalRewardEntry.cs#L119-L143)把长期Run.Hp写成run.hp；[reference Facts](../../../../tools/base-migration-baseline/differential/NormalRewardReference.cs#L66-L85)同路径却是实时Creature.CurrentHp。base战斗实时HP已在player.hp事实中，不应删除HP维度或把长期字段改成实时提交。

本次[逐边界HP证据](../../../../.audit/dsh-normal-initialization-audit-20261013/hp-boundaries.json)中，take的base boundary5（EndTurn后）run.hp=80、player.hp=76；源实现说明来自[永久结算:21–50](../../../../tools/base-migration-baseline/BaseSpire/Combat/Compilation/SettlementCompilation.cs#L21-L50)。这不是跨侧同轨迹差异归因，只是各侧投影定义和实际值核对。

下一个授权工具包应保持：战斗中以player.hp/max_hp比较实时值；run.hp/max_hp表达永久提交值。参考无独立长期提交槽时，不能用实时值冒充它：须记录入战永久快照并仅在参考真实胜利/死亡结算生命周期完成边界刷新，明确标记观察投影。奖励/可旅行边界必须核对永久提交=完成所有胜利余效后的实时值，同时核对base真实Run字段及SettledRoom/BattleSettled；无需改参考游戏状态。战斗最大HP变化也按同样边界处理。该工具修正本包未实施，避免在初始化分叉前制造新的等价结论。

永久牌组位置标识仅在本追加/跳过范围有效，不证明删除、变形、跨战斗复制来源的稳定实例身份。后继范围应使用双方可验证的来源映射，不沿用位置标签假定通用稳定。

[Drive](../../../../tools/base-migration-baseline/differential/NormalRewardEntry.cs#L20-L65)在双方分别选合法首项，故相同策略≠同具体轨迹。后续应由共同语义控制器先比候选集合/边界，再选择一个动作及参数/目标，分别绑定双方当地实例执行；目标/选牌也必须绑定同一语义选择。任一侧候选不存在、不唯一、不可出牌、目标不合法、容量不足或结果故障必须非零，不可退回本侧第一项继续。各侧展开kind/label及选中语义应共同核对，不能用相同行数或相同动作名掩盖不同候选。现有不合法选择会报故障，但不是共同动作绑定已交付。

## 本次真实验证与证据

工作目录均为仓库根。负责人[权威SHA清单](../../../../.audit/owner-continuation-20261013/verified-closure-sha256.json)全项核对一致，[核对结果](../../../../.audit/dsh-normal-initialization-audit-20261013/closure-verification.json)保留expected/actual。工具仅Compile顶层工具C#，定义REFERENCE_SIM，直接引用五个固定DLL，无ProjectReference/NoWarn；[隔离工程](../../../../.audit/dsh-normal-initialization-audit-20261013/verify/DifferentialVerify.csproj)独占输出。本次复制到隔离bin的五个依赖hash也逐一与清单一致。

实际命令如下，不使用机器报告自动填的生产工程复现命令：

```powershell
dotnet build .audit/dsh-normal-initialization-audit-20261013/verify/DifferentialVerify.csproj
$DLL = '.artifacts/parallel/dsh-normal-initialization-audit-20261013/verify/bin/Debug/net9.0/Differential.dll'
dotnet $DLL test --suite regression --output .artifacts/parallel/dsh-normal-initialization-audit-20261013/final/regression
dotnet $DLL test --suite normal-reward-prefix --output .artifacts/parallel/dsh-normal-initialization-audit-20261013/final/normal-reward-prefix
```

| 检查 | 实际返回码 / 结果 | 证据 |
| --- | --- | --- |
| 固定闭包核对 | 0；全部匹配 | 上述closure-verification |
| 隔离工具构建 | 0；0警告/0错误 | [build.log](../../../../.audit/dsh-normal-initialization-audit-20261013/build.log) |
| 完整旧17 | 0；17 executed，16双侧passed+1self_checked，自检无失败 | [机器报告](../../../../.artifacts/parallel/dsh-normal-initialization-audit-20261013/final/regression/regression.json)、[可读结果](../../../../.artifacts/parallel/dsh-normal-initialization-audit-20261013/final/regression/regression.md) |
| 完整新2 | 1；2 executed、2 reference_ran、2diverged，缺维度列表为空，自检无失败 | [机器报告](../../../../.artifacts/parallel/dsh-normal-initialization-audit-20261013/final/normal-reward-prefix/regression.json)、[首个差异表](../../../../.artifacts/parallel/dsh-normal-initialization-audit-20261013/final/normal-reward-prefix/regression.md) |

运行ID分别为`regression@env-t04-regression@1+20261013T110105Z`、`normal-reward-prefix@normal-reward-prefix@1+20261013T110120Z`。新例PublicBuildId=`e8a6ccf78f186dda`。命令只串行跑一次最终状态，没有为绿结果重复全套。

[本次输入/输出SHA](../../../../.audit/dsh-normal-initialization-audit-20261013/input-output-sha256.json)记录工具源码、suite和产物：工具DLL=`9bbeda162ad41afa8dafee233a53a81f662db02a85074d4614eac2b9c5bb7fc1`；参考DLL=`31272354e8fbbfbfab4ce0322b3d45c72e06a81a7fdb26eafd7e9e2b593a8fe3`；旧17 suite=`cc23a6060e3c3ea01c25408d3c34f6e90d6c79a4bfe0390d9e86d027dd34a415`；新2 suite=`23f87a8b2e376113e8111f0304e66d8d0761a3006dce5b65dbb5d0d22f71981a`。旧定义/seed/script/dimensions未改。

注意新suite的静态build_identity.reference仍写旧`BA817...`摘要，机器报告因此也带旧字面值；它不是本次实际加载程序集的身份。以权威清单和上述本次hash为准。报告自动command也仍为dotnet run生产工程形式；本次实际执行是上述隔离DLL，不重编A。后继负责人应协调身份资产/报告命令字段修正，不改旧17规则掩盖错误。

### 首差与最小受阻范围

- take（`ENV-T03-SEED-L`）：boundary0 ChooseMapPoint，base `[map:1,1,map:2,1,map:5,1]`，reference `[map:2,1,map:3,1,map:5,1]`；14/15边界。
- skip（`ENV-T04-RNG-SHUFFLE-001`）：boundary0，base `[map:0,1,map:2,1,map:5,1]`，reference `[map:1,1,map:2,1,map:3,1,map:4,1,map:5,1]`；16/18边界。
- 两侧status均ok，但没有最后一致边界。后续真实胜利和奖励执行仅证明各自正常流程有记录，不证明同动作对拍成立。
- [负责人真实种子向量](../../../../.audit/owner-continuation-20261013/seed-probe.jsonl)：L根源1509098584/参考10828846375177304570，地图源3303851991/参考11980532275380680700，Rewards源1050050546/参考13203298175593260461。本包复用现有真实程序集向量，未拿base生成地图移植给参考。

受阻依赖归属ENV-T04参考bootstrap公开接口裁决；只阻塞正常源兼容初始化及其后的同轨迹奖励比较。可继续范围为固定闭包旧17和已有独立地图机制证据、各线已授权生产任务，以及负责人源/客户端动态seed裁决；不因此阻塞所有生产，也不自派接续。

未验证：新bootstrap未实现/构建；正常初始化一致性、共同动作前缀、HP修正投影、药水领取一致性、后继旅行实际提交、下一战斗复制来源/长期一致性、商店/变形/全幕动态域、真实客户端。缺参考/维度/零执行严格负例本包未另重复全套执行；沿用现有门槛，新2首个差异仍真实非零。

## 文档检查与维护位置

本文件为日期交接，API提案部分按Reference组织，原因分析不当成可直接运行教程；NBJ清晰表达与Diataxis接手自查已完成，不宣称独立读者验证。当前状态仅由负责人维护主清单；接口批准后有效契约由参考/领域主责维护，本报告保留当日证据。

实际执行 `python tools/doc-check/check_docs.py` 与 `git diff --check`，均 rc0；[检查返回码](../../../../.audit/dsh-normal-initialization-audit-20261013/documentation-checks.json)、[文档检查日志](../../../../.audit/dsh-normal-initialization-audit-20261013/doc-check.log)、[差异检查日志](../../../../.audit/dsh-normal-initialization-audit-20261013/diff-check.log)保留原证据。不为纯文档额外运行solution/Engine/Slice/net或参考构建。
