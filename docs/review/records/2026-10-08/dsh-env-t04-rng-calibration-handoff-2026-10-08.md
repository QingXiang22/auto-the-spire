---
task_id: ENV-T04
dispatch_id: ENV-T04-rng-calibration-recovery-20261008
session_id: session-73793e67-0c91-44e8-a838-2854886572ad
delivery_status: ready_for_review
updated_at: 2026-10-08T18:04:07+08:00
dispatch_record: .audit/dsh-env-t04-rng-calibration-2026-10-08/task-record.json
---

# ENV-T04 窄源版本派生校准包（B 线接续）：恢复工具构建并交付源派生校准证据 — 2026-10-08

本包接续被 `INVALID_REQUEST` 中断的原包
[ENV-T04-source-rng-20261008](../../../../.audit/dsh-env-t04-rng-2026-10-08/task-record.json)
（原初稿交接 [records/2026-10-08/dsh-env-t04-rng-handoff-2026-10-08.md](dsh-env-t04-rng-handoff-2026-10-08.md)）。
中断点由负责人 [17:39 复核](../../../../.audit/dsh-supervisor/review-2026-10-08-1739/build.log) 定位为**工具 2 个编译错误**。

本包只做**最小修复 + 独立校准证据**：不重写原包已交付的 Profile/神谕/适配代码，不新增假的参考 API，
不改 core/base/net/reference/solution 与主清单。主清单入口：[ENV-T04](../../tofix/README.md#env-t04)；
工作包入口：[B 完整交付合同](../../../design/post-core-agent-assignments.md#4-bdiff-0102日常严格对拍)、
[ENV 实现约束的初始洗牌派生对齐边界](../../../design/environment-implementation-constraints.md#env-t04初始洗牌的派生对齐边界2026-10-08)、
[工具 README](../../../../tools/differential/README.md)。

## 接手后的第一步

1. 读[主清单 ENV-T04](../../tofix/README.md#env-t04) 与 [17:39 负责人复核](dsh-supervisor-review-2026-10-08.md#1739复核与剩余目标接续)，确认本包已交付项与剩余目标。
2. 直接复跑本节「验证与证据」的 5 条命令；前置条件是仓库根、已安装 .NET 9 SDK（实测 `dotnet 9.0.10`）与 Python，参考检出 `reference/sts2-sim` 必须在位（否则工具走无条件引用分支，`rng` 与 `rng-initial-shuffle` 都不可用）。
3. 预期结果：`rng` rc=0（检查 46、不通过 0）、篡改反例 rc=1（不通过 1）、严格 suite rc=1（8 passed / 1 self_checked / 6 不计通过）、根 build rc=0。任何一项偏离都说明本包证据不再成立，先查 `tools/differential` 是否被改动。

## 原完成条件与逐项回填

| # | 原完成条件 | 状态 | 依据 |
| --- | --- | --- | --- |
| 1 | 按实际类型/Serializable 状态修好 2 个编译错误，不加假参考 API | 已交付 | `RngSourceCompat.cs:156` 返回类型改 `uint`；`RngCrossCheck.cs` 判别段改用 `ToSerializable().state0..state3`，不读不存在的 `Rng.Seed` |
| 2 | 根 build 成功 | 已交付 | `dotnet build AutoTheSpire.sln` rc=0，0 错误（1 个既有警告，见下） |
| 3 | 神谕与实现独立，校验所有已声明 run/player 流、首次洗牌计数/状态 | 已交付 | 12 条 run 级 + 3 条玩家级流逐条比对；神谕重新生成与落盘产物**逐字节相同**；首次洗牌 `draws=9`、`counter=9`、洗后牌序与神谕一致 |
| 4 | 原生默认与 Profile 不同；keyed 路径拒绝/判别 | 已交付 | 指纹 `0x9a0e07e8c860a3a8` ≠ `0x6905ad2e367e85e2`；counter=0 首抽原生 67 ≠ Profile 289；`Apply` 对 keyed 与已抽过的集合都抛 `InvalidOperationException`（两项检查均为真） |
| 5 | 至少一个真实篡改向量非零证据 | 已交付 | 篡改 `streams["Niche"]` +1 → rc=1，`不通过 1`，逐字首差见下 |
| 6 | 严格 15 项 suite 在原源码上跑一次，保留 5 unsupported / 原 `rng-initial-shuffle` ID 与原 required dimensions | 已交付 | 15/15 执行；`unsupported_decision_model` 1 + `unsupported_rng_profile` 4 = 5 个原 ID；`rng-initial-shuffle` ID 与 `actions\|facts\|pile-order\|streams` 未改 |
| 7 | 不把校准通过标成 `rng-initial-shuffle passed`；参考完整运行初始化未接通时保持 `blocked_compare`、rc1 | **已交付但判定与派单预期不同** | 校准 46/0 通过，`rng-initial-shuffle` 实际判定为 **`diverged`**（不是 passed，也不是 `blocked_compare`），rc=1。原因与依据见「与派单预期的偏差」 |
| 8 | 交接指出剩余参考初始化入口的具体位置/最小缺口 | 已交付 | 见「剩余缺口」 |

## 验证与证据

全部在 2026-10-08 18:00–18:03（+08:00）于仓库根执行，日志留在本包 `.audit` 目录。

| 命令 | 返回码 | 关键读数 | 日志 |
| --- | --- | --- | --- |
| `dotnet build AutoTheSpire.sln -v m` | 0 | 0 错误、1 警告（`net-the-spire/Net.TheSpire/Projection/PublicSnapshotProjector.cs(667,52)` CS8604，非本包文件） | `.audit/dsh-env-t04-rng-calibration-2026-10-08/root-build.log` |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 0 | `All 95 engine checks passed.` | `engine-tests.log` |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0 | `All 332 slice checks passed.` | `slice-tests.log` |
| `dotnet tools/differential/bin/Debug/net9.0/Differential.dll rng` | 0 | 读数向量 5/5 一致；布局不一致 8（全部已登记参考偏离，未登记 0）；**源版本 Profile 向量：检查 46，不通过 0** | `rng-calibration.log` |
| `python tools/rng-oracle/vanilla_rng_oracle.py --source-compat <regen>` | 0 | 重新生成的神谕与落盘 `artifacts/environment/rng-source-compat.json` **SHA256 逐字节相同**（`5941bf5e…c7314`） | 命令输出 |
| `python .audit/…/tamper-source-compat.py <向量> <篡改副本>` + `rng --source-compat <篡改副本> --output .audit/…/tamper-out` | 1（预期红） | `不通过 1`，逐字：`derivation: 流 Niche 的种子 期望 52833260，实际 52833259` | `tamper-rng.log`、`tamper-rng-crosscheck.json` |
| `dotnet tools/differential/bin/Debug/net9.0/Differential.dll test --suite regression --output artifacts/environment/tests/regression` | 1（预期红） | 15/15 执行；8 passed、1 self_checked、6 不计通过、比较器自检 6/6、`reference_ran=13` | `suite-regression.log` |

严格 suite 本次 `run_id=regression@env-t04-regression@1+20261008T100245Z`，`complete=true`，
`counts={"expected":15,"selected":15,"executed":15,"passed":8,"self_checked":1,"failed":6,"reference_ran":13,"self_test_failures":0}`，
`verdicts={"passed":8,"unsupported_decision_model":1,"unsupported_rng_profile":4,"diverged":1,"self_checked":1}`，
`suite_sha256=sha256:f601d1cf30a542aa1720b28d229b3680f56601302a01935616faec60301f0ee1`（本包未改 suite 文件）。
8 个双侧 `passed` 仍是原集合（`strike-normal`、`strike-upgraded`、`defend-normal`、`defend-upgraded`、
`bash-then-strike-vulnerable`、`bash-upgraded`、`candidate-scope-partly-playable`、`last-kill-before-terminal`），
比较器与门槛未做任何放宽。

### 校准读数（`artifacts/environment/rng-crosscheck.json` 的 `source_compat`）

- 标记 `profile_id=source-compat-sts2-frozen@1`、`string_seed=ENV-T04-RNG-SHUFFLE-001`、
  `source_run_seed=3850333834`、Shuffle 流种子 `1863647213`、引擎流 `base-spire:combat-draw`。
- 首次洗牌：`draws=9`，洗后牌序与神谕 `deck_after` 一致（`strike,strike,defend,bash,strike,defend,defend,defend,strike,strike`），
  参考侧 `Rng.Shuffle` 后的 `Counter=9`、指纹 `0x6905ad2e367e85e2`。
- 判别证据：原生默认指纹 `0x9a0e07e8c860a3a8`（≠ Profile），counter=0 首抽原生 `67` ≠ Profile `289`；
  `keyed_apply_rejected=true`、`consumed_stream_apply_rejected=true`。
- base 记录核对：`base_shuffle_deck` 与神谕一致、`base_shuffle_draws=9`，来自
  `artifacts/environment/tests/regression/records/base/rng-initial-shuffle.json`。
- 该 JSON 的 `note` 明确本核对**是四源一致性核对（神谕↔工具 Profile↔装上 Profile 的参考↔base 记录），不是双侧比较**。

### 本包改动的源码与摘要

| 文件 | SHA256 | 改动 |
| --- | --- | --- |
| `tools/differential/RngSourceCompat.cs` | `3068be5b38143bc7f8ca868bf5e4fb69222912887c1a55743c7d5872a185d977` | 仅 `PlayerStreams` 返回类型 `(string Type, string Seed)` → `(string Type, uint Seed)` |
| `tools/differential/RngCrossCheck.cs` | `86a5fc70ef18718cb24fc38f5f202268c5358043b5f5633fea47c7601b5eb53a` | ① 新增玩家级 3 条流逐条核对（原只核 run 级）；② 判别段删掉 `Rng.Seed` 改按 `ToSerializable` 状态 + 首抽判别；③ 新增 keyed / 已消费流的拒绝检查；④ 把上述判别读数写进 `areas[0]` |
| `tools/differential/bin/Debug/net9.0/Differential.dll` | `27c4c26f8da7c23a70c5872edceeb4801685417ece88187bae5b051a53696a88` | 上述源码构建产物（net9.0，Debug，2026-10-08T18:02+08:00） |

未改动：`suites/regression.json`、`ReferenceRunEntry.cs`、比较器、core/base/net/reference/solution、主清单。

## 与派单预期的偏差（请负责人裁定）

派单写「现阶段尚未接参考完整运行初始化时仍 `blocked_compare`、rc1」，但**实际判定是 `diverged`**：

- suite 里 `rng-initial-shuffle` 的 `reference_scope` 在上一包就已改为 `compared`（不是本包改的），
  上一包因构建失败从未真正执行；本包修好构建后该声明第一次被真实执行，两侧都跑出了记录
  （`reference_backend=reference:sts2-sim`、`reference_status=ok`、`base_status=ok`），所以它不再落在「参考侧缺入口」的 `blocked_compare` 分支。
- 结果 rc=1、`passed` 数不变（8），`rng-initial-shuffle` 计入 6 个「不计通过」之一。它**没有**被标成 passed。
- 判定为 `diverged` 比改回 `blocked_compare` 信息更多：它保留了 12 条首差，精确指出参考侧初始化与 base 初始化不是同一个场景。
  若负责人认为必须保持 `blocked_compare`，需明确说明把 `reference_scope` 改回的授权，本包未做该改动。

### `rng-initial-shuffle` 的 12 条首差（全部在 `boundary[0]`，`last_consistent_boundary`=「无：第 0 个边界之前就不同」）

| 差异路径 | base 侧 | 参考侧 |
| --- | --- | --- |
| `enemy.base-spire:fuzzy-wurm-crawler.{alive,block,hp,max_hp}` | 缺失 | 存在（hp 56） |
| `enemy.base-spire:shrinker-beetle.{alive,block,hp,max_hp}` | 存在（hp 39） | 缺失 |
| `power.enemy.base-spire:{shrinker-beetle,fuzzy-wurm-crawler}.count` | shrinker-beetle 存在、fuzzy 缺失 | fuzzy 存在、shrinker-beetle 缺失 |
| `streams[base-spire:run:up_front].draws` | 18 | 295 |
| `streams[base-spire:run:up_front].fingerprint` | `0x5a9ffd4dde6a1100` | `0x9873f9e6d1062c55` |

两侧 `base-spire:combat-draw`（Shuffle）`draws=9`、`base-spire:run:niche`（遭遇）`draws=1` 一致。

## 剩余缺口（下一包的最小接入点）

**位置**：`tools/differential/ReferenceRunEntry.cs` 的 `Build(string gameSeed)`（`ReferenceRunEntry.cs:101-122`）：

- `ReferenceRunEntry.cs:107` `var run = new RunState(gameSeed, new Overgrowth());`（参考自己的整局开局）
- `ReferenceRunEntry.cs:110` `RngSourceCompat.Apply(run.Rng, gameSeed);`（唯一替换步，位置正确）
- `ReferenceRunEntry.cs:113-114` `Player.CreateForNewRun(Ironclad, run)` + `run.AddPlayer(player)`（遗物袋等建局前置消费 UpFront）
- `ReferenceRunEntry.cs:117-119` `RoomFactory.CreateRoom(run, RoomType.Monster)` → `run.PushRoom` → `room.Enter(run)`（遭遇抽取走 Niche、怪物出生与 StartCombat 洗牌都在这里）

**最小缺口**：两侧的**初始化不是同一个场景**，因此首次边界就对不上：

1. **遭遇不同**：base 的 `base_source=formal-dev-factory` 开局产出 `shrinker-beetle`（hp 39），
   参考侧 `RoomFactory.CreateRoom(run, RoomType.Monster)` 产出 `fuzzy-wurm-crawler`（hp 56）。
   case 目前没有声明遭遇（`contents` 只列 3 张牌），两侧各自从自己的池子选。
   待确认的推论（**未验证**）：两侧 Niche 抽取次数同为 1、且 Niche 种子已由校准证明与神谕一致，
   却选中不同敌人，指向两侧**遭遇候选池内容或映射**不同，而不是种子派生不同。
2. **UpFront 前置消费量级差 277 次**（参考 295 vs base 18），来自参考侧 `Player.CreateForNewRun` 的建局前置消费；
   base 的 formal-dev-factory 开局不消费同样的序列。下一包需要定位这 277 次抽取的具体来源并决定是否属于已裁定边界
   （若不属，则说明 base 与参考的初始化路径本就不同构）。

**二选一的对齐方案**（下一包裁定，本包不做）：

- (a) 让参考侧按 case 声明的遭遇建局（给 suite/fixture 增加遭遇声明，`Build` 里按声明放置怪物）；或
- (b) 让 base 侧改用与参考相同的「字符串 seed 新局 + 首个普通战斗」初始化路径（`formal-dev-factory` 目前不是这个序列）。

两条路都要同时解决 UpFront 前置消费差，否则 `streams` 维度仍会首差。

## 未交付 / 未验证

- `rng-initial-shuffle` **不是**双侧通过；不得把它算作 ENV-T04 的随机双侧验收。
- 参考侧完整运行初始化的场景对齐（上节）未做，属下一包范围。
- 本包未把校准结论回写到主清单（主清单由 Codex 维护），也未改其他线的交接与生产文件。
- `tools/differential/*.cs` 里的中文注释在 Windows PowerShell 5.1 下显示为乱码（既有编码显示问题，未处理）。
- `python tools/doc-check/check_docs.py` 本次 rc=7，7 条 FAIL **全部**在 `docs/review/tofix/dsh-env-t03-base-sequences-handoff-2026-10-08.md` 与
  `docs/review/tofix/dsh-env-t03-host-sequences-handoff-2026-10-08.md`（C 线文件，本包只读范围），本包交接文件无 FAIL。
  `git diff --check` rc=0（仅有 LF→CRLF 提示）。

## 维护位置

本包状态只在[主清单 ENV-T04](../../tofix/README.md#env-t04) 维护；本文件是唯一交接，`.audit/dsh-env-t04-rng-calibration-2026-10-08/` 存放派单记录与日志，
独有校准产物为 `artifacts/environment/rng-crosscheck.json`（失败数 0）与
`artifacts/environment/tests/regression/`（15 份 base 记录、13 份参考记录、`regression.json`/`regression.md`）。
accepted 不代表验收，父项不关闭。

## 18:09负责人核验补充

完整结果取齐至478、hasMore=false，turn/end completed；task/dispatch/session与派单一致。负责人实际直接DLL执行源派生46项rc0，Niche+1篡改46项1首差rc1；两源码摘要与报告一致。DLL因其他线solution重建已变，按当前DLL摘要记录重新验证，不将原DLL摘要套到新输入。未在A/C运行时构建共享obj/bin。
核验范围＝两编译错误已修、独立向量与参考自身Shuffle/计数、counter=0首抽67对289判别及keyed/已消费流拒绝；整体初始化未验收。报告中“原生与Profile初始指纹”实际拿原生counter0与Profile洗牌后counter9比较，不能支持初始状态不同的结论；下一B包修正这一窄证据问题。其余真实首抽判别仍成立。
原rng-initial-shuffle参考运行入口确实存在并独立执行，严格15项保留、rc1，判定diverged比旧blocked_compare更准确，负责人允许保留真实首差。遭遇与UpFront初始化不同不得通过补抽/换敌人/减维度/Profile扩展抹平。详见[当日复核](dsh-supervisor-review-2026-10-08.md#1809复核与初始化首差定位)。移入records保存已接收证据，不关闭T04或候选。
