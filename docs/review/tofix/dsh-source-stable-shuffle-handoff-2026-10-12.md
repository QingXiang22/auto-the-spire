# ENV-T06 源 `StableShuffle` 重洗预排序交接 — 2026-10-12

本包修掉源 `StableShuffle` 缺失的**预排序**：base 里正常抽牌与效果抽牌共用的唯一重洗入口
（[CardDrawProcedures.Reshuffle](../../../tools/base-migration-baseline/BaseSpire/Cards/Runtime/CardDrawProcedures.cs)）
现在按源 `CardPileCmd.Shuffle` 的顺序执行「清已知位置 → 弃牌堆逐张并入抽牌堆 → **按源 `CardModel.CompareTo`
排序** → 一次 Fisher–Yates」。ENV-T04 受控重洗 case 的残余首个差异（两侧洗牌**输入顺序**不同）因此消除；随机流
条数与抽牌 hook 次数不变。没有扩大引擎架构，也没有补卡牌。

- task_id: ENV-T06（父项）／本包 `ENV-T06-source-stable-shuffle-20261012`
- dispatch_id: `ENV-T06-source-stable-shuffle-20261012`
- session_id: `session-a67be89b-932f-4dc3-a4e1-6cd4be860b1b`
- request_id: `4832742e-60ac-4525-b32a-38b91981ec8f`
- dispatch_record: [.audit 派发记录](../../../.audit/dsh-source-stable-shuffle-20261012/task-record.json)
- delivery_status: `ready_for_review`
- updated_at: 2026-10-12T08:21:48+08:00

归属与接手入口：归属[主清单 ENV-T06](README.md#env-t06)（主清单由负责人独自修改，本会话只写本交接与自有证据）。
交付范围＝base 共享重洗的源预排序、其窄排序元数据/纯计算资产/必要检查与两处指南；**未改** core、net、tools、
`reference/`、商店接口与既有 `CardDefinitions.Schema` 形状。本报告记录 2026-10-12 的交付；当前排期、认领与
状态以主清单为准，`ready_for_review` 不是验收，也不关闭父项或环境。

## 接手后先做什么

1. 先核对[主清单 ENV-T06](README.md#env-t06)与 `git status --short`。本包**已交付**：源排序键
   （隐藏 `SourceOrderKey`）、共享重洗的源预排序、纯计算资产与后端分派、5 项针对性检查、两处指南与一次完整
   Slice 通过。已交的商店/语义重放/客户端**不要重做**。
2. 之后（B 补参考 streams、Codex 串行收口严格门槛）：用下面的新 `BaseSpire.dll` 重跑严格 17。前置条件＝把差分器
   base 侧/参考侧的**身份基线**按本包的新值迁移（见「身份迁移」），预期结果＝`random-reshuffle-unsupported` 的
   `boundary[3].pile.draw.initial-slots` 首个差异消失；该 case 的 `rng_profile` 声明未改，仍判
   `unsupported_rng_profile`（是否升级为可计通过由负责人决定，升级须同时补参考侧 `streams` 投影）。
3. 定向复算命令（本包已验证）：`dotnet <测试 dll> --reshuffle-only`。生产侧单点复算可用
   `python .audit/dsh-source-stable-shuffle-20261012/verify-shuffle-vectors.py`。

## 已交付与接口变化

**新增：源排序键（窄编译辅助元数据）**

- [CardSchema](../../../tools/base-migration-baseline/BaseSpire/Cards/Runtime/CardSchema.cs) 新增**隐藏**字段
  `SourceOrderKey`（`FieldTypeSchema.String(64)`，`FieldVisibility.Hidden`），内容 = `源 Entry@升级等级`
  （例：`STRIKE_IRONCLAD@0` / `BASH@1`）。Entry 由**声明名**经源 `StringHelper.Slugify` 还原（大写下划线），
  不是内容 id 的逆向转写：两者只在「连续大写结尾」这类名字上不等价（源 `AB` → `A_B`，内容 id 逆向只剩 `AB`）。
- 写出点只有 [CardDefinitions](../../../tools/base-migration-baseline/BaseSpire/Cards/Runtime/CardDefinitions.cs)
  的 `Fields`（受控切片：用 `SliceCard.Name`）、`New`（作者定义，含生成/变形/奖励/商店长期卡）、`Copy`（克隆与
  长期→战斗副本）、`Upgrade`（运行时升级，键随升级换成 `@1`）。定义 id → Entry 的表由 `source.Cards` 的声明名
  建成，缺表时回退到内容 id 逆向（当前内容两者逐张一致，见检查）。
- 该字段**不进公开输入**（隐藏字段），也不保存运行态；它是定义不变量 + 当前升级形态。

**新增：源预排序（纯计算 + IR 实现）**

- [SourceStableShuffle](../../../tools/base-migration-baseline/BaseSpire/Randomness/SourceStableShuffle.cs)（新文件，命名空间
  `BaseSpire.Slice`）：`Slugify`、键编解码、源比较 `(Entry 序数, 升级等级)`、`Order`（源 `List<T>.Sort` 置换）、
  `NextIndex`（IR 每一步的当前下标）、资产声明与后端 `TryExecute`。
- 源 `StableShuffle` 的排序是 `List<T>.Sort()`（**内省排序**）：≥16 张同键实例的并列次序**不等于**稳定排序。
  本包 C# 探针实测 4000 组重复实例输入里 **3005 组**与稳定排序不同（[probe-output.txt](../../../.audit/dsh-source-stable-shuffle-20261012/probe-output.txt)）。
  因此 `Order` 复刻的是**置换**：对下标数组用同一个比较器调用 `List<int>.Sort(...)`（比较结果逐对同号 ⇒ 同一
  .NET 运行时下置换逐位一致）；IR 不新增运行态、不消耗随机数，只用「每一步问当前下标 + 把那张牌移到末尾」。
- `CardDrawProcedures.Reshuffle` 的指令序列：`clearKnown` → 弃牌堆逐张移入抽牌堆 → 拼键快照 blob →
  `Repeat` 逐步 `Compute` + `MoveInCollection(..., End)` → 一次 `ShuffleCollection(DrawStream)`。触发条件与源
  `ShuffleIfNecessary` 一致（抽牌堆空且弃牌堆非空），因此合并顺序就是弃牌堆顺序；`One`/`Until` 共用本入口。
- 资产声明在 [CardCompilation.Draw](../../../tools/base-migration-baseline/BaseSpire/Cards/Compilation/CardCompilation.cs)
  （`SourceStableShuffle.Declare(package, cardSchema.SourceOrderKey)`）；执行分派在
  [SourceWeightedPickBackend](../../../tools/base-migration-baseline/BaseSpire/Randomness/SourceWeightedPick.cs)（与加权分桶
  同一个 `BackendId`，引擎每会话只装一个后端）。`BaseSpireComputeBackend` 的 default 分支逐字转发到它，
  因此受控切片/对拍（`SourceWeightedPickBackend`）与 dev-map（`BaseSpireComputeBackend`）两条路径一致；
  `SourceActMapCompute.cs` 未改。
- 作者/维护者指南：[重洗的源预排序](../../design/mod-the-spire/control-flow-modules.md#重洗的源预排序stableshuffle)
  与[新增共享状态字段](../../../tools/base-migration-baseline/docs/base-development-guide.md#新增共享状态字段)的编译辅助元数据例外说明。

**检查（`BaseSpire.Slice.Tests`）**

- 新增 [SourceStableShuffleChecks](../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Randomness/SourceStableShuffleChecks.cs)
  5 项：①键口径（Slugify/键/比较，含「声明名 vs 内容 id 逆向不等价」的判别向量）；②源置换（10 张起始牌堆、
  17 张同键的 `List<T>.Sort` 置换，并与稳定排序可判别）+ IR 逐步下标契约；③真实卡实例上的键随创建/复制/运行时
  升级（Armaments）一致；④**端到端**：ENV-T04 同 seed/同流位置的 10 张重洗牌序 + 流抽数 9 次不变；
  ⑤**端到端**：17 张同键（>16）重洗按源算法（含 16 次抽取）。注册：`--reshuffle-only`、
  [Program](../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Program.cs) 完整入口与
  [SliceChecks.RunAll](../../../tools/base-migration-baseline/BaseSpire.Slice.Tests/Authoring/SliceChecks.cs)。
- **三处既有检查的必要适配**（都因这次语义修正而必须改，均已在代码注释里写明原因）：
  ① `SliceChecks.AttackUntilOutOfAttacks` 改为只从 `LegalAction.Bindings`（guard 已通过）里挑攻击牌——它原先按
  `Candidates`（全部手牌）贪心，源排序把 `BASH` 稳定排到最前时会出现「最前面的攻击牌付不起、但同手牌里有付得起
  的牌」而递交被守卫拒绝；② `SliceEnemyStateMachineChecks` 的「有序条件分支」会话改装**正确**后端并只断言
  `monster_ai` 抽取为 0——该测试场景回合开始会真的重洗，重洗现在会执行**另一个**包内 compute 资产，「装错后端即
  Unsupported」仍由同一 Check.Run 里的加权分桶会话覆盖；③ `SliceMapRoomChecks` 的 dev/dev-map 身份常量按
  `--describe-build` 实测迁移（下节）。除这三处外没有改既有断言。

**身份迁移（需负责人确认）**

卡类型新增隐藏字段、`DrawCards` 新增 compute 资产与移动指令 ⇒ 规则身份与公开构建身份**必然**变化：

| 范围 | ruleset（前 → 后） | 公开构建（前 → 后） |
| --- | --- | --- |
| dev | `3224ed9fcee52136` → `2dab0c5b3c3a4034` | `35a160593789e880` → `4a08d5bbaf4270be` |
| dev-map | `c46704e1392e253e` → `6c1e1be04af1492b` | `fd9e605af013e515` → `9d9f43c0d1b58809` |

实测证据 [describe-build.txt](../../../.audit/dsh-source-stable-shuffle-20261012/describe-build.txt)，迁移注释写在
`SliceMapRoomChecks` 常量上方（含旧值留档）。这属「合法重构造成旧快照失效」的基线迁移；差分器/公开接口回放数据侧
（`tools/differential`、net/Host 测试场景）的同一批身份由 B/负责人统一迁移。

## 源定位

- 预排序本身：`sts2/src/Core/Extensions/ListExtensions.cs:22-31`（`list2.Sort()` 后 `UnstableShuffle`）。
- 触发与合并顺序：`sts2/src/Core/Commands/CardPileCmd.cs:864-876`（`discard + drawPile` → `StableShuffle`）、
  `ShuffleIfNecessary`（抽牌堆空且弃牌堆非空）。
- 排序键：`sts2/src/Core/Models/CardModel.cs:2242-2263`（`base.CompareTo` 后 `CurrentUpgradeLevel`）→
  `AbstractModel.cs:87-97`（`Id.CompareTo`）→ `ModelId.cs:42-50`（`Category`、`Entry` 都是 `Ordinal`）；
  `ModelDb.cs:58-81`（`GetEntry = Slugify(类型名)`、`GetCategoryType` 对卡类归结到 `CardModel` ⇒ 卡间只比 Entry）；
  `StringHelper.cs:23-28`（Slugify）。
- 参考侧同一步：`reference/sts2-sim/src/Sts2Sim.Core/Commands/CardPileCmd.cs:375-407`、
  `Helpers/ListExtensions.cs:16-27`。

## 验证与证据

工作目录 `F:\Scylier-Project\active\auto-the-spire`；构建输出 `.artifacts/parallel/dsh-source-stable-shuffle-20261012`
（本包独立根，未写其他构建根、未并发构建 solution、未覆盖固定闭包）。

| 检查 | 命令（`$dll` = 上述根的 `bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll`） | rc / 结果 | 证据 |
| --- | --- | --- | --- |
| 独立构建（仅 Slice 测试工程） | `dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj -p:UseArtifactsOutput=true -p:ArtifactsPath=… --no-incremental` | rc0；**0 错误 / 4 警告**（4 条与 `.audit/owner-project-review-20261012/build.log` 同名同位置，均为既有） | [build.log](../../../.audit/dsh-source-stable-shuffle-20261012/build.log) |
| 本机制判别（开发组） | `dotnet $dll --reshuffle-only` | rc0；**5/5** | [reshuffle-only.txt](../../../.audit/dsh-source-stable-shuffle-20261012/reshuffle-only.txt) |
| 编排回归（受影响组） | `dotnet $dll --orchestration-only` / `--effects-only` / `--author-contracts-only` | rc0；24 / 全部效果 / 34 | [orchestration-only.txt](../../../.audit/dsh-source-stable-shuffle-20261012/orchestration-only.txt)、[effects-only.txt](../../../.audit/dsh-source-stable-shuffle-20261012/effects-only.txt)、[author-contracts-only.txt](../../../.audit/dsh-source-stable-shuffle-20261012/author-contracts-only.txt) |
| 一次完整 Slice | `dotnet $dll` | rc0；**All 775 slice checks passed**（迁移前 770，本包 +5） | [slice-full-final.txt](../../../.audit/dsh-source-stable-shuffle-20261012/slice-full-final.txt) |
| 身份实测 | `dotnet $dll --describe-build` | rc0；dev/dev-map 新身份（见上表） | [describe-build.txt](../../../.audit/dsh-source-stable-shuffle-20261012/describe-build.txt) |
| 独立随机模型复算 | `python .audit/dsh-source-stable-shuffle-20261012/verify-shuffle-vectors.py` | rc0；10 张 case 复现 T04 参考侧 hand `04,05,06,08,01` / draw `00,09,03,07,02`；17 张同键给出判别向量 | [verify-shuffle-vectors.txt](../../../.audit/dsh-source-stable-shuffle-20261012/verify-shuffle-vectors.txt) |
| 源置换探针（C#，只读源语义） | `dotnet …/probe-bin/Debug/net9.0/StableShuffleProbe.dll` | rc0；下标置换与源 `List<T>.Sort` **0/4000** 不一致；稳定排序 **3005/4000** 不一致；逐步下标实现 0/4000 不一致 | [probe-output.txt](../../../.audit/dsh-source-stable-shuffle-20261012/probe-output.txt)、[探针源码](../../../.audit/dsh-source-stable-shuffle-20261012/probe/Program.cs) |
| 文档检查 | `python tools/doc-check/check_docs.py` | rc4；430 篇、4 个问题**全部**是既有文件（`dsh-power-fixture-handoff-2026-10-11.md` 2 个、`dsh-startup-alignment-handoff-2026-10-11-f68b943d.md` 2 个），本包改动/新增文件 0 个 | [doc-check.txt](../../../.audit/dsh-source-stable-shuffle-20261012/doc-check.txt) |
| 空白检查 | `git diff --check` | rc0；只有既有共享文件的 LF/CRLF 提示（本包多数文件未跟踪，该命令不覆盖它们） | [diff-check.txt](../../../.audit/dsh-source-stable-shuffle-20261012/diff-check.txt) |

**产物 SHA256**（同一次构建，完整清单见 [dll-sha256.txt](../../../.audit/dsh-source-stable-shuffle-20261012/dll-sha256.txt)）：

- `BaseSpire.dll` = `050B92798291840BF5482E3EE8FDB1A02FBA99C7598574CFAE05C386D45519ED`
- `BaseSpire.Slice.Tests.dll` = `80B2C5B70FEC297A761943B7667F44C72A62FBCDCC7803787370E52F603C656A`
- `Core.TheSpire.dll` = `841C20A4F9A26D90151807995BBB5A1ED7145FE5BAC983039E330DD187DDADCD`（core 未改，为共同构建产物）

**Scope / Profile / seed**：`dev`、`dev-map`（`--describe-build`）；随机 Profile `mega-random/xoshiro256ss-splitmix64@1`
+ `vanilla-uniform@1`（与正式工厂/对拍同一句）；端到端检查用 ENV-T04 的字符串 seed
`ENV-T04-CTRL-random-reshuffle-unsupported` ⇒ `Shuffle` 流（`base-spire:combat-draw`）种子 `3072570454`、游标 0。

**执行的用例数**：本包新组 5；受影响开发组 24 + 34 + 效果组；一次完整 Slice 775。未跑 Engine/net/tools/solution。

## 判别点（实际结果）

- **原 10 张首个差异消失**（端到端实测，同 seed/同流位置）：重洗后第 1 张 = 弃牌堆第 4 个实例，剩余抽牌堆 =
  第 `5,6,8,1,0,9,3,7,2` 个实例 —— 与 T04 独立模型的 `04,05,06,08,01 / 00,09,03,07,02` 同序；`Shuffle` 流恰好
  消费 **9** 次（`n-1`，与迁移前同一条流、同条数）。
- **>16 张重复实例按源算法**：17 张同键实例端到端得到源内省排序置换 `[0,14,13,12,11,10,9,15,8,6,5,4,3,2,1,7,16]`
  再洗牌的结果，消费 16 次；同一输入的**稳定排序**会给不同置换（纯函数与端到端两层都能判别）。
- **键精确**：实例上的键由声明名还原（`ExpectAFight` 这类连续大写也逐段断开），普通/升级/复制/运行时升级
  四条路径一致；`STRIKE_IRONCLAD@1` 与 `@0` 的先后关系等于源 `CurrentUpgradeLevel`。
- **知识清除与抽牌 hook**：`clearKnown` 仍在移动/洗牌之前调用一次，`ShuffleCollection` 仍只调用一次，抽牌 hook
  （`PowerRules.AfterDraw`）仍每张一次；本包没有新增/删除任一消费点（既有抽牌组与编排组回归通过）。

## 未验证与缺口

- **严格 17 未重跑**：差分器要换新 `BaseSpire.dll` 并迁移身份基线（B/负责人）。因此本包只证明「源预排序把
  重洗输入顺序对齐」，不证明 `random-reshuffle-unsupported` 已可计通过（它的 Profile 声明未改，预期仍
  `unsupported_rng_profile`）。
- **>16 且键混合**的输入只有纯函数层面的一般性论证（同一比较器 + 同一 .NET 内省排序 ⇒ 置换逐位一致）与
  17 张同键的端到端实测；未与参考侧逐例对拍更大牌堆。
- **升级等级 >1**（源 `CurrentUpgradeLevel` 可多于 1）在 base 由布尔 `Upgraded` 表示，键只区分 `@0/@1`——这是
  既有模型限制，本包未扩。
- **牌触发的非空抽牌堆重洗**（如 Reboot 一类「把弃牌堆洗回抽牌堆」）base 目前没有入口；`Reshuffle` 的触发条件与
  源 `ShuffleIfNecessary` 一致，因此该形状仍未接通（上一包已登记的同一缺口）。
- **未跑**：`dotnet build AutoTheSpire.sln`、Engine、net、tools、训练/归档套件、正常整局公开复放与 Host 轨迹。
  本包没有改 core/net/tools/协议，也没有改商店接口。
- **测试辅助方法的注意事项**：`SliceFixtures.PutInHand(upgraded: true)` 这类**测试注入**升级不经过 `CardDefinitions.Upgrade`，
  不会同步排序键；生产升级路径已同步（检查③覆盖）。若后续检查要用注入升级再触发重洗，需要显式同步该键。

## 依赖与可继续事项

- 依赖：ENV-T04 的受控重洗 case 与独立模型（已交）；[.audit/dsh-reshuffle-followup-20261011](../../../.audit/dsh-reshuffle-followup-20261011/)
  的游标对齐成果（本包复用其 seed/期望值，未重做工具侧）。上游：本包不启动下一包。
- 可继续（B/Codex）：①把差分器 base/参考身份基线迁到本包新值并补参考侧逐边界 `streams`；②用新 DLL 跑严格 17，
  确认 `boundary[3]` 首个差异消失后由负责人裁定是否升级该 case 的比较口径；③如需，把「>16 且键混合」的输入加进
  受控 case 作为独立对拍。
- 可复用：本包构建根、`--reshuffle-only` 开发入口、独立模型脚本、C# 探针、5 项检查与身份实测清单。

## 维护位置与自查

- 任务状态/认领/下一成果只回写[主清单 ENV-T06](../../review/tofix/README.md#env-t06)（本轮由负责人独自修改）；机制用法写
  [base 控制流模块](../../design/mod-the-spire/control-flow-modules.md#重洗的源预排序stableshuffle)与
  [base 开发指南](../../../tools/base-migration-baseline/docs/base-development-guide.md#新增共享状态字段)；证据与长日志在
  [.audit/dsh-source-stable-shuffle-20261012](../../../.audit/dsh-source-stable-shuffle-20261012/)，构建在
  `.artifacts/parallel/dsh-source-stable-shuffle-20261012`。
- 预期读者能定位：目标与有效状态（主清单 ENV-T06）、可复用成果（「已交付与接口变化」）、第一步（「接手后先做什么」2）、
  完成条件与验收者（主清单完成条件＋负责人 Codex）、阻塞/未验证（「未验证与缺口」）、维护位置（本节）。
- 以上属作者自查，不是独立读者验证；`ready_for_review` 表示请负责人复核，不等于验收。本会话未提交、未推送、
  未打 tag，未 reset/清理/覆盖共享未提交成果，未在日志或文件里写入任何凭据。
