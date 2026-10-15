# DSH 自动复核与接续证据 — 2026-10-08

本轮确认 T04 的严格入口已经交付，并复现一个会让完整 suite 假通过的门槛缺口；C 的 T03 旧会话则因 API 错误停止，尚未交付完整公开出口。两个开放范围已回写主清单并分别接续派单；门槛修复在本轮交回后已核验，T04其他范围保留待验证。本记录是日期证据，不维护第二份当前任务表。

接手入口：[主清单 T04](../../tofix/README.md#env-t04)、[T03](../../tofix/README.md#env-t03)和[半小时复核约定](../../../design/deepseek-harness-operations.md#半小时复核交接与接续)。下次先按这两条登记的实际会话查询，再读取其记录指定的交接；运行中不重派，结束后核对完整结果和原完成条件。B 具有本轮优先构建权，C 独立编辑 net，待 B 停止后才构建共享闭包。

## T04 的实际复核

原接续会话 `session-2e21e798-e835-4b05-b899-ac0177e38ada` 的完整结果已取齐至 seq 680，最后一轮 completed；[交接](dsh-env-t04-handoff-2026-10-08.md)的 task/dispatch/session 与派单记录一致。`handoff_path` 在实际派单记录中存在，交接中“缺该字段”的行政说明未获本轮事实支持。

负责人逐项比较既有十二个 case 的 setup/script/provenance 与原 fixtures-v1：全部相同。源码检查确认 test 分支、版本化十五项分母和薄脚本存在，正式工厂路径及受控机制路径明确分列。复用已构建的 Differential.dll，在独立输出目录实跑；本轮未修改或构建生产代码。

| 检查 | 实际命令/结果 | 证据和证明范围 |
| --- | --- | --- |
| 正式严格 suite | `dotnet tools/differential/bin/Debug/net9.0/Differential.dll test --suite regression --output .audit/dsh-supervisor/review-2026-10-08-1606/regression`；rc=1，15/15/15，8 passed、1 self_checked、6 未通过，比较器自检6/6 | [负责人产物](../../../../.audit/dsh-supervisor/review-2026-10-08-1606/regression/regression.json)，与执行者分布一致；原七项及新增末杀战斗边界已实际双侧执行，未支持比较保持红态 |
| 工厂缺维度/错终点反例 | 完整注册隔离 suite 含八项可比较 case 与工厂 case；只令工厂声明 terminal 维度及预期 Defeat。执行 `dotnet tools/differential/bin/Debug/net9.0/Differential.dll test --suite .audit/dsh-supervisor/review-2026-10-08-1606/suite-wiring-invalid.json --output .audit/dsh-supervisor/review-2026-10-08-1606/wiring-invalid`；实际rc=0 | [输入](../../../../.audit/dsh-supervisor/review-2026-10-08-1606/suite-wiring-invalid.json)/[输出](../../../../.audit/dsh-supervisor/review-2026-10-08-1606/wiring-invalid/regression.json)明确 missing_dimensions=[terminal]、terminal_problem 非空，实际 Running，但 verdict=self_checked 且全套返回0；没有 --only 或零执行干扰 |
| 对应源码 | `TestCommand.cs` 的 `if (verdict == "passed")` 只约束双侧通过；返回码将 self_checked 直接计为成功 | 工厂 case 同样声明必需维度和终点，因此这是严格门槛缺口，不能作为正确通过保留 |

修复合同：所有可计成功结果均受声明的维度/终点约束，保留具体失败原因；两个分开负例需在完整 suite 中失败，正常工厂自检继续成立，正式十五项分母与红态保留。[B 接续提示词](../../../../.audit/dsh-env-t04-gates-2026-10-08/task.txt)与[派发记录](../../../../.audit/dsh-env-t04-gates-2026-10-08/task-record.json)保存范围，accepted不代表修复完成。

T04 原合同的源/reference 派生和随机 trace 仍未完全满足，`rng-initial-shuffle` 与原五项未支持比较没有本轮验收结论。新增末杀 case 使用受控 Strike/Bash，只证明该战斗边界及牌区/资源投影，不覆盖 Feed 余效、永久实例写回或跨房，也不关闭 T02/M01。下一轮按主清单安排源随机窄裁决，不以长尾红态阻塞所有独立进展。

## 门槛修复子包核验

会话 `session-7e3c951d-f007-43d7-87c7-27daceb1d452` 的完整结果取齐至seq493，hasMore=false，最后turn/end=completed；交接task/dispatch/session与本包记录一致。源码门槛现为 `verdict is "passed" or "self_checked"`，原判决顺序和更具体失败保留；Program、FixtureSuite、Comparator、正式suite及负责人反例输入哈希均与修复前相同。工具构建日志为0错误，当前源码/DLL哈希与执行者交接一致（DLL `18a2c69b…`）。

负责人复用该DLL独立运行两套完整注册负例：

| 输入 / 独立产物 | 实际结果 | 判别 |
| --- | --- | --- |
| `suite-wiring-missing-dimension.json` / [输出](../../../../.audit/dsh-supervisor/review-2026-10-08-1606/fixed-missing-dimension/regression.json) | rc1；9/9/9；8 passed；比较器自检6/6；run_id末尾 `20261008T083305Z` | 工厂missing_dimension，缺terminal，登记running终点正确 |
| `suite-wiring-wrong-terminal.json` / [输出](../../../../.audit/dsh-supervisor/review-2026-10-08-1606/fixed-wrong-terminal/regression.json) | rc1；9/9/9；8 passed；比较器自检6/6；run_id末尾 `20261008T083306Z` | 工厂wrong_terminal，维度齐全，实际Running与预期Defeat不符 |

两项均complete=true、only=null；修复前同输入rc0的产物及修复后正式suite由执行者证据核对，不重复全量运行。正式suite仍15/15/15，8 passed/1 self_checked/6未通过、rc1（run_id `regression@env-t04-regression@1+20261008T082609Z`），原五项unsupported与随机blocked保留。结论：**只验收本门槛修复子包**，不关闭T04/T02/M01。当前与下一包排期仅维护主清单；已复核交接并入records，移动不构成父项验收。

## T03 的实际复核与恢复

原 C 会话 `session-24834b51-70d6-45e6-b020-3c8c9202f55d` 的完整结果已取齐至 seq 613，最后 turn/end 为 INVALID_REQUEST，未留下完整交接。已有 DTO/版本分支、ProvidedSession/ISessionSource 和 ControlSession/ControlHost 接缝应复用；停止不代表完成。

B 的构建日志自报五处 CS0120。本轮源码核对确认 `ControlHost.SupportedScenarioProfile` 是实例属性，而 AcceptanceChecks 的三处、ControlChecks 和 MeasurementChecks 的各一处仍以静态方式读取。本轮未另跑 root build；不能把静态核对当作新构建结果。

[C 接续提示词](../../../../.audit/dsh-env-t03-resume-2026-10-08/task.txt)保留原 T03 完整行为合同：先修消费者编译缺口，再完成正式工厂 Host、声明派生及真实 NDJSON 轨迹和旧0.1兼容；不退回忽略 source 的全局场景常量。C 可独立编辑自己的 net 文件；构建前查询本轮 B 记录，确认停止后才写共享 core/base 的 obj/bin。无法验证时交接明确待验证/窄阻塞，不用旧 DLL 代替本次验证。

## 证据与未执行

原始结果、完整历史序号和交接摘要保存在[复核游标](../../../../.audit/dsh-supervisor/review-2026-10-08-1606/review-cursor.json)，源码/DLL/suite/实际产物摘要保存在[输入哈希](../../../../.audit/dsh-supervisor/review-2026-10-08-1606/review-inputs.json)。它们只用于输入核对与去重，不维护工程排期。两张接续单实际派发状态和会话只看主清单。

本轮未跑 solution build、Engine/Slice/net 规则套件、公开 Host 轨迹、源随机独立双侧、真实地图奖励闭环、四幕胜负、120 对或成本测量；生产修复和对应验证由本轮 B/C 包交付。文档维护doc-check扫描255份Markdown、0问题，git diff --check通过；实际日志留本复核产物目录。

## 16:39复核与三线接续

本次在前序核验游标基础上接续，不重复运行T04门槛与正式suite。T03前会话 `session-87c45c57-b46a-4a08-863b-7788fa1b1fdc` 查询时running=true；随后只读会话列表显示已停止。完整结果取齐至551、hasMore=false，最后turn/end为API INVALID_REQUEST，未留下指定交接。五处CS0120消费者实际修改存在，其余公开Host仍未接正式工厂；停止不能判完成。

在本项目DS会话均停止、尚未派新包时，负责人串行执行当前输入验证：

| 命令（仓库根目录） | 返回码 / 结果 | 证据与证明范围 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 0；0错误、0警告 | [build.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1639/build.log)；旧5处CS0120已修，不能继续当当前阻塞 |
| `dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests` | 0；95通过 | [engine.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1639/engine.log) |
| `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | 0；324通过 | [slice.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1639/slice.log)；源float/内容候选检查及9项T02结算尾/长期实例来源检查实际执行 |
| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | 0；393执行/393通过/0失败 | [net.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1639/net.log)；这是既有切片/兼容检查，不能证明尚未组装的正式工厂Host |

T01描述/内容权重及T02修复的本地证据已补齐；双侧仍缺。T02中的Feed/跨房、重复牌实例和临时变化检查使用受控状态或作者场景，测试标注“真实”不能升格为正常整局或真实奖励闭环通过。正式工厂仍有占位升级奖励，保持原合同待验证。

本次源码核对纠正T04交接/suite中的一处推断：参考普通`RunRngSet(string)`确实是顺序默认路径，只有`CreateKeyed`使用语义重键。源hash32/uint命名流回绕与参考XxHash64/ulong及old前缀的差异已确认，源/参考八个文件摘要保存于[rng-source-inputs.json](../../../../.audit/dsh-supervisor/review-2026-10-08-1639/rng-source-inputs.json)。参考HEAD `2cdaea3…`有共享未提交修改，未检出、重置或修改参考。[有效窄边界](../../../design/environment-implementation-constraints.md#env-t04初始洗牌的派生对齐边界2026-10-08)允许B在工具层对齐这些派生差异；其他规则、初始化差异仍须另行裁决，原始字符串seed不变。

公开Host的具体剩余依赖是base工厂尚无内容目录、集合用途/归属位置、已知位置角色与终局含义的不可变描述，C现有DerivedPublicProfile不能凭名称猜。负责人将该依赖交A主责；当前生产认领只写主清单。本轮三包均收到accepted，尚未核验实现：

- [A元数据提示词](../../../../.audit/dsh-env-t03-base-metadata-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t03-base-metadata-2026-10-08/task-record.json)：只写base及必要base文档，交领域元数据/稳定身份，禁止反向依赖net；不重做T01/T02。
- [C正常Host提示词](../../../../.audit/dsh-env-t03-host-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t03-host-2026-10-08/task-record.json)：只写net/0.2契约，复用已修接缝，交正常公开开局/候选动作/同Host多seed及0.1兼容；声明扩展实证仍是T03后续子包。
- [B源随机提示词](../../../../.audit/dsh-env-t04-rng-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t04-rng-2026-10-08/task-record.json)：只写tools，原地接通rng-initial-shuffle；保留15分母及5个unsupported，新首差不扩Profile。

构建顺序A→C→B，各自可先编辑不同责任文件；所有新执行者只写固定交接、不并行修改主清单，避免公共文件覆盖。每包第一步先创建in_progress交接，API中断也保留接续位置。完成声明不等于验收；本轮未执行新正式Host轨迹、新源随机双侧、地图/奖励正常闭环、四幕胜负、120对或训练。A的元数据依赖交付后再接T05，不并开A生产包。

本轮状态快照和结果序号保存在[16:39复核游标](../../../../.audit/dsh-supervisor/review-2026-10-08-1639/review-cursor.json)，只作去重/身份核对；当前排期仍只看主清单。文档检查0问题、git diff --check通过，实际扫描数量及日志保存本轮.audit目录；新DS包修改输入后须核对其最终证据，本轮派单前的绿色数字不验收新包。

## 17:39复核与剩余目标接续

本轮按三份原派单记录各查询一次：A元数据停止、完成至581；C正式Host停止871、B源随机停止719，后两者turn/end均为API INVALID_REQUEST。三份result-review-1739.json均hasMore=false，未用停止状态判完成。身份/原交接摘要见[复核游标](../../../../.audit/dsh-supervisor/review-2026-10-08-1739/review-cursor.json)，最终检查输入与Host DLL摘要见[输入哈希](../../../../.audit/dsh-supervisor/review-2026-10-08-1739/review-inputs.json)。

已接收报告：[A元数据](dsh-env-t03-base-metadata-handoff-2026-10-08.md)、[C部分Host](dsh-env-t03-host-handoff-2026-10-08.md)、[B随机初稿](dsh-env-t04-rng-handoff-2026-10-08.md)移入records，开放缺口并入主清单。A/C原dispatch_id误填request_id，负责人按原记录更正并保留原值说明；B初稿updated_at早于实际发生的未来时间不能作完成证据。原delivery_status保留，移动不验收。

| 实际命令（均在新派单前、三线停止后串行执行） | 返回码 / 结果 | 证据与范围 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln` | 1；2错误 | [build.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1739/build.log)：仅tools RngSourceCompat.cs159的uint/string CS0029、RngCrossCheck.cs432不存在Rng.Seed的CS1061；base/net/Host/tests本轮成功生成 |
| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests` | 0；399执行/399通过/0失败 | [net.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1739/net.log)：含正式工厂新6项，不把root失败说成整体绿色 |
| `python -X utf8 net-the-spire/client/test_client.py` | 0；103通过/0失败 | [client.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1739/client.log)：正式53及既有0.1/传输/恢复/错误检查，公开操作已实际存在 |
| `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests -- --dump-declarations` | 0；13集合声明 | [declarations.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1739/declarations.log)：同正式dev工厂的可见性/顺序/成员种类 |

A新增不可变BasePublicSemantics/工厂出口/摘要身份与8检查确实存在；执行者Engine95/Slice332日志保留，未重复全量运行。C接通formal-dev场景、正式ISessionSource、只Start一次及每局字符串seed；负责人独立通过当前net/Python，局部能力可复用。原C完整交接尚缺，旧NDJSON记录之后Program修改，不把旧DLL轨迹身份当最终输入证据。

**具体不验收点**：CombatSlicePackage.cs233的PlayedThisTurn采用CollectionSymbol默认identity/size/order=true，core Projection.cs110–126实际发布；BasePublicSemantics.cs248却以“内部顺序”解释跳过，不能从结果牌区重建本回合有序成员/数量。LongTermDeck/Relics也order=true，而group排序会丢顺序；Monsters具名跳过也未证明已完整表达集合事实。Named Skip只是说明，不能覆盖规则已发布事实。Known/RunStates/EncounterPool/EncounterQueue的实际投影与冗余/不可表示形状须逐项核对，不能以此直接全绿色。运行时owner无需写死句柄：已有BuildGroup按owner种类唯一实例解析，可在通用Sequence里沿用，原wire有sequence形状且保留0.1固定owner路径。未改core、规则公开标记或协议范围来掩盖缺口。

B停在添加源Profile校准途中，未提交完整双侧初始化。Apply/独立神谕/参考自身Shuffle可复用，但源码当前不能构建，自己的卡组洗牌与旧base落盘不能升格为原rng-initial-shuffle双侧。先恢复真实编译与校准，再接原初始化case。严格15分母、5 unsupported、随机blocked_compare保留。

本轮三份更小接续均accepted，尚未验收：

- [B校准恢复](../../../../.audit/dsh-env-t04-rng-calibration-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t04-rng-calibration-2026-10-08/task-record.json)：工具两错、源派生校准及原生/Profile/篡改判别；不冒充原初始化case已通过。
- [A集合元数据修复](../../../../.audit/dsh-env-t03-base-sequences-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t03-base-sequences-2026-10-08/task-record.json)：base真实公开有序成员与owner元数据，未知形状明确待裁定；不重做工厂，不与T05并开。
- [C序列/Host接续](../../../../.audit/dsh-env-t03-host-sequences-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t03-host-sequences-2026-10-08/task-record.json)：通用运行时owner、真实两次出牌顺序/成员证据、同Host多seed、旧0.1兼容及完整最终交接；仅规则包声明扩展实证仍是下一子包。

构建B→A→C串行，生产文件各在线内；主清单由Codex独占维护。新的接续会话accepted后不轮询等待完成。尚未验证最终修复源码、原初始化独立双侧、正常地图/奖励闭环、四幕胜负、五角色/进阶、120对、成本或真实客户端；未启动训练或停止自动循环。文档门槛结果保存在本轮.audit日志，与生产检查分列。

## 18:09复核与初始化首差定位

从主清单登记的三份最新记录各查询一次：A集合元数据会话running=true、seq431；C序列会话running=true、seq512，运行中交接未作验收或重派。B校准会话停止478，完整结果hasMore=false，turn/end completed，交接ready_for_review的task/dispatch/session身份与记录一致。游标与交接原摘要见[本轮去重证据](../../../../.audit/dsh-supervisor/review-2026-10-08-1809/review-cursor.json)。

[B校准交接](dsh-env-t04-rng-calibration-handoff-2026-10-08.md)已归并开放项并移入records。两源码摘要与报告一致；工具DLL被其他线solution重建，当前摘要`8fe6039d…`不同于执行者`27c4c26f…`，负责人直接在当前DLL补必要校准，未并行构建core/base/net obj/bin。输入摘要见[review-inputs.json](../../../../.audit/dsh-supervisor/review-2026-10-08-1809/review-inputs.json)。执行者root build0错误、Engine95/Slice332日志保存其时点输入，不能覆盖在制A/C的最终源码。

| 负责人实际执行 | 结果 | 原始证据 |
| --- | --- | --- |
| `dotnet tools/differential/bin/Debug/net9.0/Differential.dll rng --output .audit/dsh-supervisor/review-2026-10-08-1809/valid/rng` | rc0；源派生46检查/0失败；5读数一致，8布局偏离均登记 | [rng-valid.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1809/rng-valid.log)、[实际校准JSON](../../../../.audit/dsh-supervisor/review-2026-10-08-1809/valid/rng-crosscheck.json) |
| `dotnet tools/differential/bin/Debug/net9.0/Differential.dll rng --source-compat .audit/dsh-env-t04-rng-calibration-2026-10-08/rng-source-compat.tampered.json --output .audit/dsh-supervisor/review-2026-10-08-1809/rng-tamper` | rc1；46检查/1首差，Niche期望52833260、实际52833259 | [rng-tamper.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1809/rng-tamper.log)、[篡改实际JSON](../../../../.audit/dsh-supervisor/review-2026-10-08-1809/rng-tamper-crosscheck.json) |

两次命令遵守无构建模式；工具实际输出在传入output的父目录，为防两次产物覆盖，篡改JSON另存并将有效输入校准放独立valid目录。复跑有效输入只为恢复被覆盖的有效产物，未循环重复全套规则检查。

源码核对确认参考自身Shuffle、12run流装载、3player流向量、counter0首抽原生67/Profile289及keyed/已消费流拒绝成立。校准的一个证据错误保留待修：nativeFingerprint来自counter0，而referenceFingerprint在9次洗牌后取得，不能称“两初始状态不同”。先验收其可复用的编译/派生/真实参考Shuffle与首抽判别能力，下一包修同counter0状态比较；不据46绿色关闭初始化比较或T04。

原ReferenceRunEntry已经实际从原字符串seed建局，不是缺入口；上一包构建失败掩盖了该部分交付。本次严格报告`regression@env-t04-regression@1+20261008T100245Z`：15/15/15、only=null、8passed、1self_checked、5unsupported、1diverged、reference_ran=13、自检6/6、rc1；suite摘要`f601d1cf…`及所有原ID/required_dimensions保留。保存[原报告快照](../../../../.audit/dsh-supervisor/review-2026-10-08-1809/delivered-regression-regression.json)，未在A/C运行时重复整套suite。原case记录确为两侧ok、start边界，玩家/手牌与Shuffle9/Niche1一致，但敌人shrinker-beetle39对fuzzy-wurm-crawler56、UpFront18对295，首差12条。负责人认可真实diverged，不为维持旧描述改回blocked_compare。

这些初始化差异未获派生Profile修补授权；禁止强设敌人、补抽277次、缩池/减维度或受控弱场景冒充正常新局。必须从sts2冻结源码、base dev范围与参考版本分别定位真实池/调用/消费，生产缺口交A目录主责，参考玩法默认只读。既有资料还不足以认定295或18哪一侧符合源，不能自动相信参考数字。

本轮[B初始化诊断/窄判别修复单](../../../../.audit/dsh-env-t04-init-diagnosis-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t04-init-diagnosis-2026-10-08/task-record.json)已accepted，未等待完成；B先读源/独立编辑tools，A/C都停止才构建。唯一交接按记录路径。下一生产包由实际源证据决定，T05仍是A集合修复后的正常地图/房间/奖励闭环，父项不关闭。

本轮没有构建整个solution、重跑Engine/Slice/net/Python、正常地图奖励闭环、四幕胜负、五角色/进阶、120对、Release成本或训练；旧绿色不验收新A/C。文档检查与diff-check日志保存在本轮.audit，执行中的报告链接问题不覆盖其文件强行整理。

## 18:39复核与T05地图房间接续

本轮按主清单最新三份记录各查询一次：A公开集合元数据修复停止475；C序列/Host仍running、seq1008；B初始化诊断仍running、seq505。运行中包不重派、不改其生产文件/交接、不并行构建共享obj/bin。A完整结果hasMore=false、turn/end completed，ready_for_review身份与原派单一致；结果与原交接摘要见[review-cursor.json](../../../../.audit/dsh-supervisor/review-2026-10-08-1839/review-cursor.json)。

[A六类Sequence元数据交接](dsh-env-t03-base-sequences-handoff-2026-10-08.md)已接收、归并开放项后移入records。当前BasePublicSemantics.cs完整摘要3a09d1297a45dfb4ef796a449543a2da8041d9723122193746b258b01907df4d、PublicSemanticsChecks.cs aa6207a02b8a906c2bbdb5782ab7a36087370e581d5a151f9f14fb602f54c676，与报告及333日志一致；[输入哈希](../../../../.audit/dsh-supervisor/review-2026-10-08-1839/review-inputs.json)还保存工厂/config输入。源码核对支持其六类Sequence/owner及canonical摘要改动，base仍无net依赖。不是只按文件或停止状态判完成。

| 复用有效执行者证据（负责人核对当前源码与完成条件，未另运行） | 结果 | 原始日志 / 证明范围 |
| --- | --- | --- |
| `dotnet build mod-the-spire/base-spire/BaseSpire/BaseSpire.csproj -v q` | rc0；0错误 | [01-base-build.txt](../../../../.audit/dsh-env-t03-base-sequences-2026-10-08/artifacts/01-base-build.txt)；本包base元数据可构建 |
| `dotnet build AutoTheSpire.sln -v q` | rc1；C当时在制CS1739 | [02-root-build.txt](../../../../.audit/dsh-env-t03-base-sequences-2026-10-08/artifacts/02-root-build.txt)；不将旧在制错误当最终C失败，也不写整体绿色 |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | rc0；95通过 | [03-engine-tests.txt](../../../../.audit/dsh-env-t03-base-sequences-2026-10-08/artifacts/03-engine-tests.txt) |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests` | rc0；333通过 | [04-slice-tests.txt](../../../../.audit/dsh-env-t03-base-sequences-2026-10-08/artifacts/04-slice-tests.txt)；含规则声明与元数据公开顺序/真实owner/摘要的新增判别 |

核验范围＝PlayedThisTurn/Monsters/Known/RunStates/LongTermDeck/Relics的公开有序集合领域元数据修正、CanonicalText/Digest/PublicBuildId接线；公开runtime owner/真实两次出牌顺序仍待C最终轨迹。A的只读元数据包结束，可以沿主清单接T05，不以整个T03父项关闭为前提。

仍需保留的窄形状缺口：EncounterPool/EncounterQueue成员种类不在实例投影，但core Projection.cs110–126依然发布其集合size与IdentityVisible时成员ID；因此“成员种类未投影”不能证明集合全部事实可跳过。A有BLOCKED命名解释并不等于公开事实已表达。未直接泄漏未来遭遇、改变core/规则可见性或关T03；结合T05源地图可见性给最小建议，由负责人裁定后续。纯数据作者的遗漏不能转成用户在历史里补事实。

本轮已accepted派发[A ENV-T05地图/房间子包](../../../../.audit/dsh-env-t05-map-room-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t05-map-room-2026-10-08/task-record.json)。范围仅base与必要base文档/固定交接；第一个机制＝真实源地图节点/合法后继、房间阶段与父子上下文、正常同工厂配置进入真实战斗与稳定出口。真实奖励/选牌/跳过/容量、长期提交与返回地图是T05下一子包；不能用仅地图节点判M01完成。工厂增加显式地图入口配置应维持dev、纳入静态身份，保留当前B初始洗牌审计配置/初始边界供协调迁移；未开启Ironclad/full范围。

C/B仍在核验当前base，故新A先读源/准备补丁到独有audit，查询C/B停止后才应用base生产改动和构建；记录列edit_after_records/build_after_records，避免读取/构建在制输入和循环依赖。C/B构建前置仍是旧已停A记录，新A不改变他们的依赖。accepted后不等待新包完成，不再查询本轮三会话。

本轮未新增solution/Engine/Slice/net/Python/严格suite运行，未验收C/B运行中报告、正常地图奖励闭环、四幕/五角色/进阶、120对、成本或真实客户端；未启动训练。纯文档检查与diff-check实际输出留本轮audit；C在制报告的链接错误不通过编辑其交接强行清零，结束后再处理。

## 19:09复核与声明扩展源输入接续

三份当前记录各查询一次：A T05地图/房间running=true、seq590；C序列包停止1370；B初始化诊断停止745。后两者实际turn/end completed，task/dispatch/session/frontmatter与记录一致。C最后1370窗口hasMore=true、最早seq364，继续取before364/through1370至最早0、hasMore=false；B745窗口已hasMore=false，均取全才审阅。原交接摘要/去重序号见[复核游标](../../../../.audit/dsh-supervisor/review-2026-10-08-1909/review-cursors.json)，实际关键源码/DLL输入见[输入哈希](../../../../.audit/dsh-supervisor/review-2026-10-08-1909/review-inputs.json)。

[C公开序列交接](dsh-env-t03-host-sequences-handoff-2026-10-08.md)、[B初始化首差交接](dsh-env-t04-init-diagnosis-handoff-2026-10-08.md)已归并开放项并移入records、修复引用；A运行中交接不编辑，A包不重派。交接原ready_for_review保留，归档证据不表示父项关闭。

C关键源码及最终Host DLL完整摘要与报告一致：PublicSnapshotProjector.cs `26c0e7ee…`、PublicVocabulary.cs `03f87b7d…`、FormalPathChecks.cs `dc617816…`、Host.dll `36b85da8…`。按源码核对运行时唯一owner/旧固定owner兼容及元数据转换，复用最终build0错误/0警告、net402/402、Python103/103、同进程两seed/旧0.1 NDJSON，run_id `env-t03-host-sequences-20261008T184609+0800`；已有匹配证据不重复运行。实际[build-final.log](../../../../.audit/dsh-env-t03-host-sequences-2026-10-08/build-final.log)、[net-final.log](../../../../.audit/dsh-env-t03-host-sequences-2026-10-08/net-final.log)、[client-all-final.log](../../../../.audit/dsh-env-t03-host-sequences-2026-10-08/client-all-final.log)与[NDJSON摘要](../../../../.audit/dsh-env-t03-host-sequences-2026-10-08/artifacts/host-formal-summary.json)保存完成条件与载荷身份，未把旧399证据当新结果。

仅核验通用Sequence出口与实际已成功公开路径。F-08经过三次第二张攻击候选backend_fault重开，最后技能路径 `ParticipantView=e8,e7` 与公开元素逐位相同，确能判别丢成员/顺序反转，但这不是全部合法攻击可执行。正式seed `ENV-IRONCLAD-00` 连续Strike→目标决策→再Strike/Bash，在LegalActions触发 `SessionStateInvalid: pure procedure base-spire:PowerStacksOf did not complete; pure procedures cannot suspend`，错误堆栈/探针在C交接保存；Defend第二张和旧0.1连续攻击成功。A文件主责为CombatSlicePackage.cs281–295的Pure PowerStacksOf/关系读取，已登记T05正常路径依赖；A当前包不并开第二生产修复，下一接续必须核对。若确属冻结core缺口需最小复现和负责人兼容维护裁定，禁止静默吞异常或删候选制造绿色。EncounterPool/Queue公开shape及声明扩展仍开放，不能验收完整T03/Profile。

负责人在A运行时只直接执行已有DLL、未构建共享obj/bin：

| 命令 | 实际结果 | 证据 |
| --- | --- | --- |
| `dotnet tools/differential/bin/Debug/net9.0/Differential.dll init-upfront --seed ENV-T04-RNG-SHUFFLE-001 --output .audit/dsh-supervisor/review-2026-10-08-1909/init` | rc0；真实295与CloneExact副本112/118/0/30/16/17/1/1一致 | [init.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1909/init.log)、[实际JSON](../../../../.audit/dsh-supervisor/review-2026-10-08-1909/init/init-upfront-diagnosis.json) |
| `dotnet tools/differential/bin/Debug/net9.0/Differential.dll rng --output .audit/dsh-supervisor/review-2026-10-08-1909/valid/rng` | rc0；48检查/0失败；counter0_states_equal=false，原生/Profile初始状态取样同counter0 | [rng.log](../../../../.audit/dsh-supervisor/review-2026-10-08-1909/rng.log)、[实际JSON](../../../../.audit/dsh-supervisor/review-2026-10-08-1909/valid/rng-crosscheck.json) |

B新增工具纯观察真开局，CloneExact副本抽取不回写实际流；参考230次遗物袋+30事件洗牌发生在普通遭遇槽之前，与base缺前置阶段相符。`295−18=277`分解是**参考当前输入**完整归因，不能当源版本完整期望；源事件/遗物池与tag/顺序尚未逐项证明。B将“记录accepted”当运行/不能构建原因不成立，后续按sessions.running判断，当前无构建复现证明对应DLL可执行而非全solution最终通过。原rng-initial-shuffle仍diverged，15分母/5unsupported/8passed/1self_checked保留，未在A执行时重新suite。

解锁无需新的用户输入：RunConfiguration.cs87/112及ENV实现约束§3已固定UnlockPolicy.All/singleplayer。负责人确认沿用现合同；是否源All事件恰为31、列表内容及源顺序是否和参考同形仍必须取证，不能以count相同替代。未改变Profile边界或允许工具补偿初始化。

本轮两张接续已accepted，未等待完成：

- [C声明扩展实证](../../../../.audit/dsh-env-t03-declaration-extension-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t03-declaration-extension-2026-10-08/task-record.json)：仅在规则包新增公开字段/Action，真实加载/目录/observe/step与隐藏/内部不公开、身份/过期/幂等，net不新增逐项注册；独立fixture不替代完整原版证据。
- [B冻结源初始化输入manifest](../../../../.audit/dsh-env-t04-source-init-manifest-2026-10-08/task.txt)/[记录](../../../../.audit/dsh-env-t04-source-init-manifest-2026-10-08/task-record.json)：源全解锁遗物/事件/弱常规精英BOSS古代人池、rarity/tag/顺序与参考差异，保留未实现内容源分母，供A后续真实初始化。

A继续既有地图/房间包，C/B先在audit/独立文本fixture/Python取证中准备；修改编译项目或共享构建等A停止，构建A→C→B。新包依赖旧A当前记录，旧A只依赖已停前驱C/B，不形成循环。当前生产状态只维护主清单，报告不复制第二表。

本轮未重跑solution/Engine/Slice/net/Python/整套严格suite，未完成正常地图奖励返回、所有合法动作、四幕胜负、五角色/进阶、120对、Release成本、真实客户端或训练。纯文档门槛输出在本轮audit；A运行中交接的字段/相对链接错误待停止后按实际交付修正，不覆盖其工作。

## 21:53验收与地图公开接续

三条会话已停止且最终completed：A地图1529、C声明扩展1529、B源manifest855。A/C最后窗口hasMore=true，分别before551/445继续取回至seq0且hasMore=false；B一次取全。停止与交接声明不当作完成。本轮用户明确要求验收并继续推进。

- A地图/房间首包局部接收：源算法移植、引擎MapNode/MapChildren、合法选择进入既有战斗与稳定结算出口；80配置/5004比较点参考移植证据复用，默认Dev身份及原15严格判决未变。当前范围仅DevMap/Overgrowth/asc0，真实奖励、全房型、公开Host地图、源完整初始化、连续攻击故障仍开放。Fail未实现房型会使会话Faulted；可达第15行营火/BOSS尚不实现，不能称完整地图整局。
- C声明扩展子包验收：报告14项源码摘要全部匹配，生产net源码无新增fixture字段/Action注册；同提供方baseline/extended真实目录/observe/step、隐藏字段/内部块、身份/过期/幂等与schema证据成立。独立fixture不代替base全游戏。
- B源输入证据子包验收：负责人重跑extract_source_manifest rc0，与交付JSON对象完全一致，9类别ID/顺序一致；源共享袋118/6桶112次、角色袋122/4桶118次、事件31/30次。直接源核对SlitheringStrangler无Tags覆盖、SnappingJaxfruit仅Mushroom，参考额外Jaxfruit实差保留。修正诊断副本角色126/8桶与真实122/4桶混淆，不能只凭118计数认初始化正确。

必要独立验证与输入摘要在 [.audit/review-2153](../../../../.audit/dsh-supervisor/review-2026-10-08-2153/)；本轮只做一次受影响根构建及实际检查：build rc0/0错误；Engine95、Slice339、net410、声明扩展Python157、旧客户端103均rc0/零失败。Slice地图检查较慢，串行跑至22:11完成；所有验证在接续派发前完成，输入摘要绑定当时源码，后续DS输入变更需按影响复核。严格原15套件已有A实际证据且默认输入未变，不重复重跑；仍8双侧passed/1self_checked/5unsupported/1rng-diverged、rc1。

源裁决：两条tag按冻结源是base应有值，参考差只接收为精确源事实，不授权参考行为修补或全局忽略RNG后缀，不计双侧passed。All事件31的ID顺序一致无需新偏离授权；非All/discovery/doubleBoss未验证保持开放。EncounterPool/Queue仍发布size/opaque身份，NotPublished没有事实不成立；不能任意删可见性或公开未来遭遇来编码成功。

下一包按P1依赖：C地图Host选择/公开两seed轨迹→A连续攻击修复→B真实工厂地图独立严格对拍。目录分开，构建/编译输入顺序C→A→B；A/B可先各自源审阅/audit准备，前置停止后才改编译生产文件。A修复后再接真实奖励与源前置初始化，不扩召唤族。T03/T04/T05/M01及完整候选全部保持开放。

21:53批次补充：A `SliceMapRoomChecks.cs:297–322` 的SettleThenChoose把敌人压至1血、玩家改至30血并移动Strike到手牌，是受控机制证据；未实现房型也用Arrange置父节点。它们不能证明正常无注入战斗胜利/地图闭环。已列入主清单继续待验证，A首包只局部接收。两个正常seed初始地图与合法进入战斗证据可复用，真实奖励仍未实现。

- C接续accepted：`ENV-T03-map-host-20261008`，会话`session-3baaf675-9539-441a-b208-7ee00280721c`；[记录](../../../../.audit/dsh-env-t03-map-host-2026-10-08/task-record.json)与固定交接路径已保存。accepted只表示已派发。
- A接续accepted：`ENV-T05-consecutive-attack-repair-20261008`，会话`session-20bb6663-e012-4391-bffb-64516cc5a5df`；[记录](../../../../.audit/dsh-env-t05-consecutive-attack-2026-10-08/task-record.json)与固定交接路径已保存。accepted只表示已派发。
- B接续accepted：`ENV-T04-map-differential-20261008`，会话`session-542ab2c1-4079-47c1-9011-2b53fee16ca0`；[记录](../../../../.audit/dsh-env-t04-map-differential-2026-10-08/task-record.json)与固定交接路径已保存。accepted只表示已派发。

地图两case派生边界已按源StandardActMap.CreateFor独立counter-0流追加到[实现约束](../../../design/environment-implementation-constraints.md#env-t04地图两seed的派生对齐边界2026-10-08)，仅hash32/uint，不扩参考地图/float/遍历规则。当前包生产实施等待C→A→B，不重复派发。
