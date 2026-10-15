---
task_id: ENV-T03
task_name: C 线声明扩展子包（只改规则包新增公开字段/Action 的真实加载与执行证据）
dispatch_id: ENV-T03-declaration-extension-20261008
session_id: session-185c891a-e2ef-4fd5-a92e-724112e61418
dispatch_record: .audit/dsh-env-t03-declaration-extension-2026-10-08/task-record.json
delivery_status: ready_for_review
updated_at: 2026-10-08T20:47:08+08:00
---

# ENV-T03 C 线声明扩展子包交接 — 2026-10-08

本包**只做一件事**：用一个「仅在规则包里新增公开字段和合法 Action」的扩展例，验证 net 生产源码
不需要新增逐项注册行——目录/observe 出现对应内容、step 执行正确、新增内部块不成为外部操作，
同时旧的过期候选/重发/身份保证保持。本包**不**关闭 ENV-T03 父项，**不**声称两集合公开表达或
连续攻击 `PowerStacksOf` 故障已解决，也不声称这个独立扩展例等于完整 base 原版。

归属：ENV-T03「公开 Host 接唯一工厂；从规则声明派生公开出口」的 C 线**声明扩展实证**子包（主清单 `#### ENV-T03`）。
接手入口：[主清单 ENV-T03](../../tofix/README.md#env-t03)、[net 设计 §7 实施边界](../../../design/net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续)、
[net 协议 §4–§7](../../../design/net-the-spire/protocol.md)、[C 提示词](../../../design/agent-goals.md#5-cnet-the-spire-编码-agent)、
前驱 [C 线通用公开 Sequence 运行时 owner 交接](dsh-env-t03-host-sequences-handoff-2026-10-08.md)。

交付范围：负责会话 `session-185c891a-e2ef-4fd5-a92e-724112e61418`；可写 `net-the-spire/**`（新 fixture、必要泛型接缝、检查、组合与 net 文档）、
`.audit/dsh-env-t03-declaration-extension-2026-10-08/**` 与本交接。`core-the-spire`、`mod-the-spire`、`tools`、`reference`、`ai-the-spire` 只读；主清单不编辑。

前置与共享构建：派单记录 [.audit/…-declaration-extension/task-record.json](../../../../.audit/dsh-env-t03-declaration-extension-2026-10-08/task-record.json)
的 `edit_compiled_sources_after_records` / `build_after_records` 只有 A 线 ENV-T05 地图/房间包
（[.audit/dsh-env-t05-map-room-2026-10-08/task-record.json](../../../../.audit/dsh-env-t05-map-room-2026-10-08/task-record.json)，
会话 `session-3a7026c3-9a61-4e44-9393-b7077d8a0fc1`）。**A 未实际停止前不得修改参与编译的 net 源码、不得跑构建。**

本报告记录 2026-10-08 的交付过程；当前排期、状态与认领以主清单为准。

## 原完成条件逐项对照

| # | 派单要求 | 证据 | 结论 |
| --- | --- | --- | --- |
| 1 | 用**同一通用规则提供方/Host 接缝**跑 baseline 与 extended 两套规则包，扩展 delta 实质仅规则声明 | 同一 `EngineDslCompiler.LoadFiles` + 同一 `ControlHost(ISessionSource, ledgerCapacity)`；两包逐字相同仅差四处声明（见下「规则文本差异」）；两个真实宿主横幅都在 `artifacts/declaration-extension-evidence.ndjson` 的 `host-banner` 条目里，`scenario=text-rules`、`scope=net-text-rules-declaration-extension-v1`，只有 `rules=`/`ruleset=` 不同 | ✅ |
| 2 | 通用加载/配置 + 四类不可推导元数据固定；**不能**为新增字段/Action 手写 Profile/ID 表或特殊 handler | `net-the-spire/Net.TheSpire.Host/TextRuleProviderSpec.cs`（配置/元数据泛型装配）、`TextRuleScenario.cs`、`TextRuleSource.cs`；net 生产源码搜 `Bump|Retally|ReachedLimit|declaration:Marker|declaration:Mark\b` **0 命中** | ✅ |
| 3 | 两套规则各创建「已配置未 Start」的独立 Session，目录/行为严格从 `RuleSet` + 公开投影派生 | C# 检查 E-01…E-04 与 Python 分组 `catalog`/`trajectory`：`describe` → `get_catalog`（未 Start）→ `start` → `observe`/`step` | ✅ |
| 4 | 给出规则文本差异与 net 未新增注册的源码证据 | 规则文本差异见下节；grep 证据见上表第 2 行 | ✅ |
| 5 | 真实 `describe/catalog/start/observe` → 拿到新 Action 的真实合法候选 → `step` → 观察新字段变化 | 真实 ControlHost（子进程 NDJSON）：`net:action:declaration:Mark` 候选参数值域来自 `choices = [1, 3]`；step 后 `net:field:declaration:Marker.Bump` 由 0 变 1/3 | ✅ |
| 6 | 证明新增公开字段/类型与参数、候选闭合 | `get_catalog`：`fields` 4 → 6（新增 `net:field:declaration:Marker.Bump`、`net:field:declaration:Mark.amount`）；`action_types` 2 → 3（新增 `net:action:declaration:Mark`，`required_fields=[net:field:declaration:Mark.amount]`、`optional_fields=[]`） | ✅ |
| 7 | 一项新增隐藏字段不会出现 | `Marker.Secret`（`visibility = Hidden`）在目录九表与 observe 快照 fields 中均**不出现**（C# E-05 与 Python `trajectory/hidden-still-absent` 双重断言） | ✅ |
| 8 | 内部 procedure 不发布成 Action | 内部 `procedure Retally` 不出现在 `action_types`，也无任何合法候选（C# E-05、Python `trajectory`） | ✅ |
| 9 | 过期候选/错 expected_identity 在执行前拒绝；重发幂等 | C# E-06 与 Python 分组 `rejections`（56 条）：`stale_observation` / `illegal_candidate` / `invalid_input`/`version_mismatch` 分类正确；同 `request_id` 重发返回同一结果（幂等）；换载荷同 `request_id` 报 `request_conflict` | ✅ |
| 10 | 身份应反映规则变化而不含每局 seed | baseline `net:ruleset:362f9217df658b91` / `net:catalog:a6063c491bbb2a26`；extended `net:ruleset:d14d0ece0ff8ff3a` / `net:catalog:ae35798d5b89565e`；换 seed（`'DECL-EXT-00'` 与 `'DECL-EXT-00 '`）不改变 ruleset/catalog/backend 三元组 | ✅ |
| 11 | C#/Python/schema 实际载荷一致 | 同一 `docs/contracts/net/0.2/wire.schema.json`：C# 新增 E-08 逐帧校验；Python 检查 `schema` 分组逐帧校验并按 `$defs` 名校验结果对象（describe→`DescribeResult`、catalog→`PublicCatalog`、start→`StartResult`、observe/step→`ObserveResult`、快照→`PublicSnapshot`） | ✅ |
| 12 | 不靠反射列名或编译成功宣称通过 | 全部断言基于真实宿主响应字节（NDJSON 帧）与目录/快照载荷 | ✅ |
| 13 | 泛型接缝缺口可在 net 本线修必要生产代码并检验旧 0.1/0.2 兼容 | `client/net_client.py` 两处修复（见「首差」）；`net-the-spire/client/test_client.py` 旧套件 **103 passed / 0 failed** 回归 | ✅ |
| 14 | 多选/枚举形状不在范围；遇未知结构明确 unsupported，不静默删公开事实 | C# E-07：描述文件遇到未支持结构显式抛错；`describe` 的 `supported_structures` 如实标注未声明公开有序集合 | ✅ |
| 15 | 规则包测试资源只用现有 SDK/冻结语言，不自行扩 DSL | 两包只用 core/0.2 既有语法（`entity`/`procedure`/`action`/`terminal`/`projection`，`dsl_version = "core/0.2"`） | ✅ |
| 16 | 交付最终规则/Host DLL/协议/config/seed/run_id 日志 | 见「交付清单」与「最终实况」；证据日志 44 行 | ✅ |

## 规则文本差异（baseline → extended，逐字其余相同）

`net-the-spire/fixtures/declaration-extension/baseline.spire`（73 行）与 `extended.spire`（101 行）唯一差异：

1. `entity Marker` 字段表新增一行公开字段（`extended.spire:25`）：
   `Bump = { type = Int(min = 0, max = 1000000) default = 0 }`
2. 同表新增一行隐藏字段（`extended.spire:26`）：
   `Secret = { type = Int(min = 0, max = 1000000) default = 7 visibility = Hidden }`
3. 新增内部块（`extended.spire:43-53`）：`procedure Retally { … set = { target = first(type(Marker)) field = Marker.Bump … } }`——**没有任何 action/订阅引用它**。
4. 新增合法动作（`extended.spire:79-96`）：`action Mark { role = player; parameters = { amount = Int(min = 1, max = 3) } choices = { amount = [1, 3] } body = { set Marker.Bump += amount; set Marker.Secret += 1 } }`。

两包都声明 `dsl_version = "core/0.2"`、`start = [Setup]`，共享 `public-metadata.json`。

## 新能力 / 文件 / API / 源版本摘要

- **新场景 `text-rules`**（Host CLI）：`--scenario text-rules --rules <entry.spire> --public-metadata <file.json> [--game-seed <string>]`。组成：`TextRuleProviderSpec`（提供方配置 + 四类不可推导元数据装配）、`TextRuleScenario`（场景/capabilities/limits 与徽标文本）、`TextRuleSource`（`ISessionSource` 实现，把会话 seed 折算为规则源会话标识）。
- **seed 语义**：`--game-seed` 原样发送到 `IGameSeed`，由 `TextRuleSource.SessionSeed`（FNV-1a 64，**不 trim**）折算，只影响每局随机源，**不进身份**。
- **文档**：新增 [net-the-spire/text-rule-packages.md](../../../../net-the-spire/text-rule-packages.md)（用法、责任划分、seed 折算、四类元数据、检查与证据表、边界）；[coverage-n-02.md](../../../../net-the-spire/coverage-n-02.md) 入口行更新为 410/410 并新增第 4 节指向本扩展例。
- **检查入口**：C# `net-the-spire/Net.TheSpire.Tests`（E-01…E-08，总计 410）；Python `net-the-spire/client/test_declaration_extension.py`（157 条，`--only <group>` / `--log <file>`）。
- **源版本**：core `engine/ir@0.3` 冻结（锚定 `core-engine-ir-0.3`），规则语言 core/0.2；net 协议 `0.2`；Host 构建标识 `net-text-rules-declaration-extension-v1`；`rng_profile = rejection-uniform@1`；`projection = declaration:PlayerView`；`frame_limit = 1048576`。

## 交付清单（sha256）

| 文件 | bytes | sha256 |
| --- | --- | --- |
| net-the-spire/fixtures/declaration-extension/baseline.spire | 2447 | `36299AE4C1B42E377C2E3A7EBE6E8880F629821850B89A4F3D85FCDFD1F44533` |
| net-the-spire/fixtures/declaration-extension/extended.spire | 3308 | `6147C1433367028573C07E401E13182440B36191B8CC750D237B9C5538883C03` |
| net-the-spire/fixtures/declaration-extension/public-metadata.json | 768 | `D5F1FC1D4196262CE4103AB1CEE82D7F7BADB9F5B909A1AA767D7704A8D37B8F` |
| net-the-spire/fixtures/declaration-extension/README.md | 3193 | `1BF0E20FB0C802D9959ED1759BC364CE6DD12CCE510490F2BF12C9188E7C9F9C` |
| net-the-spire/Net.TheSpire.Host/TextRuleProviderSpec.cs | 10542 | `32CA13823FCEBC4DA6A636E27C0679D04114109DC4A38096DF7DBD03A02D6AB3` |
| net-the-spire/Net.TheSpire.Host/TextRuleScenario.cs | 2838 | `00CBA5BBCBCABF95B3BEC52B4C11D907C3B102096BDD945E4B1162FCF9208529` |
| net-the-spire/Net.TheSpire.Host/TextRuleSource.cs | 4218 | `BC180ACCE52E5F29FE56989FA3119E872ECCD3BE3AE87DEB02C023370591A5F5` |
| net-the-spire/Net.TheSpire.Host/Program.cs | 16753 | `909E3CB6E356E2141C16DB14B7269747A19EC95AC16D9F186BD8A795CB5747F4` |
| net-the-spire/Net.TheSpire.Tests/DeclarationExtensionChecks.cs | 33420 | `E5930AA5AEAA54DBFB2786A9724C8D58EBB7F24186F9A2F3E2E3A179F9B72D46` |
| net-the-spire/Net.TheSpire.Tests/Program.cs | 2155 | `17144997644D0FEAAEA76601A4BBE1638BEDEB2BA7608D836C5480A21CB7DAB8` |
| net-the-spire/client/net_client.py | 18116 | `721C3E46B8F93B6CE1D74269BFD8240C74D5215A97A053EEF7783F1894C40D1E` |
| net-the-spire/client/test_declaration_extension.py | 34639 | `D43FF922CDED7BC166FC89783E2A53A809A0342F90C52CD500E44B9E37E9AE34` |
| net-the-spire/text-rule-packages.md | 6594 | `AA0DD9D51166B47AC6D4A0F6AD51AD363F9FE2D62CCE2410DFF0F902D44197F3` |
| net-the-spire/coverage-n-02.md | 16336 | `F3E5D647969694CE50F183A84E8182FE0A9401FCEA6A13C5C6E4FF5A3F1CC976` |

**Host DLL**：`net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll`，41472 字节，
sha256 `7980f115be3a47259019e401210f725d2c21d78f66ffeae39bdc9ff5a80cdec7`，LastWriteTime 2026-10-08 20:10:45。

## 最终实况（命令 / 退出码 / 日志）

A 线 `session-3a7026c3-9a61-4e44-9393-b7077d8a0fc1` **实际 running=false**（`asOfSeq 1060`，其交接自身仍 `in_progress`）之后执行，全部串行：

| 步骤 | 命令 | 退出码 | 日志（`.audit/dsh-env-t03-declaration-extension-2026-10-08/`） | 结果 |
| --- | --- | --- | --- | --- |
| 构建 | `dotnet build AutoTheSpire.sln` | 0 | `build-final.log` | 0 警告 0 错误（增量 5.29s） |
| net 检查 | `dotnet run --project net-the-spire/Net.TheSpire.Tests -v minimal --nologo` | 0 | `net-final.log` | **410 executed / 410 passed / 0 failed**（393 既有 + E-01…E-08） |
| 本包 Python | `python -X utf8 -u net-the-spire/client/test_declaration_extension.py --log .audit/dsh-env-t03-declaration-extension-2026-10-08/artifacts/declaration-extension-evidence.ndjson` | 0 | `client-declaration-extension-final.log` | **157 passed / 0 failed / 157 executed**（catalog 36、trajectory 29、identity 18、rejections 56、schema 18） |
| 旧客户端回归 | `python -X utf8 -u net-the-spire/client/test_client.py` | 0 | `client-all-final.log` | **103 passed / 0 failed** |
| 文档 | `python tools/doc-check/check_docs.py` | 4 | `doc-check-final.log` | 4 条失败全在 A 的 `docs/review/tofix/dsh-env-t05-map-room-handoff-2026-10-08.md`（`../README.md[#env-t05]` 链接多一层）；本包文档 0 问题 |

退出码汇总：`exit-codes.txt`；执行记录脚本：`run-final.ps1`。

- **真实横幅**：baseline `… build=net-text-rules-declaration-extension-v1|rules=d589153c7c924811 ruleset=net:ruleset:362f9217df658b91`；extended `… |rules=6ed229e742d8685a ruleset=net:ruleset:d14d0ece0ff8ff3a`（其余字段相同：`default_game_seed=DECL-EXT-00`）。
- **身份三元组**：baseline `net:ruleset:362f9217df658b91` / `net:catalog:a6063c491bbb2a26` / `net:backend:simulation-0.2-1b934efa420e9973`；extended `net:ruleset:d14d0ece0ff8ff3a` / `net:catalog:ae35798d5b89565e` / `net:backend:simulation-0.2-6195cc24ad502061`。
- **run_id / seed**：真实会话 `session-4d06ca43c689-1`，`start` 回显 seed `'DECL-EXT-00'`；另一轮 `'DECL-EXT-00 '`（尾随空格）验证 seed 原样发送且身份不变。
- **终局字段**：`net:reason:declaration:ReachedLimit`（`outcome = win`）的 `Value` 落在 `optional_fields`（声明为裸 `Int`，`DerivedPublicProfile.cs:568,573` 的 `Required: !field.Optional` 如实转述），C# 断言「恰好落在必填或可选之一」。
- **证据日志**：`artifacts/declaration-extension-evidence.ndjson`（44 行 = header + 2 横幅 + 41 帧，每帧含 `fixture/seq/operation/payload/response/canonical`，并按同一 schema 校验）；辅助文件 `artifacts/sessions-at-final.txt`、`artifacts/catalog-extended.json`、`artifacts/probe_catalog.py`、`artifacts/debug_schema.py`。
- 本目录摘要：`summary.md`。

## 首差 / 偏差（都已定位，未越界修）

1. `doc-check` 退出码 4 的 4 条失败**全部**在 A 的交接（链接多一层目录），本包文档 0 问题；未修改他人交接。
2. PowerShell 执行策略拒绝 `& .\run-final.ps1`（`UnauthorizedAccess`／「在此系统上禁止运行脚本」），最终以同内容 inline 执行；`run-final.ps1` 保留为命令记录。
3. 本包内修复了 `net-the-spire/client/net_client.py` 两处缺陷（均属「必要泛型接缝/客户端合规」）：
   - `request()` 原先把 `request_id` 注入错误对象，违反 `$defs.Error` 的 `additionalProperties:false`，使宿主发来的**合规**帧在本客户端手里变得不合规；改为 `response.setdefault("request_id", request_id)`（全仓无消费者依赖注入键）。
   - `stderr_text()` 原先对二进制 stderr 做 `str + bytes`（`TypeError: can only concatenate str (not "bytes") to str`），改为按需 `decode("utf-8", "replace")`。
   修复后旧 0.1/0.2 客户端套件 **103/103** 回归通过。

## 未验证 / 明确不在本包范围

- 本扩展例是**独立最小 fixture**，**不等于**完整 base 原版；未覆盖完整 base 规则库的公开表达。
- 多选/枚举形状不在范围；遇到未知结构按 unsupported 显式报错，不静默删公开事实。
- 父项 ENV-T03 仍开放的两点（两集合公开表达、正常连续攻击 `PowerStacksOf` 故障）待 A；本包不据此宣称全游戏可用。
- 未重跑整个 T04/地图/训练；未开下一生产包；未关闭 ENV-T03。
- `docs/review/tofix/README.md`（主清单）由 Codex 维护，本包未改动。

## 接手第一步

1. 读本交接「原完成条件逐项对照」，以及本包记录 [.audit/…-declaration-extension/task-record.json](../../../../.audit/dsh-env-t03-declaration-extension-2026-10-08/task-record.json) 与 [执行摘要 summary.md](../../../../.audit/dsh-env-t03-declaration-extension-2026-10-08/summary.md)。
2. 独立复跑：`dotnet run --project net-the-spire/Net.TheSpire.Tests -v minimal --nologo`（期望 410/410）与 `python -X utf8 -u net-the-spire/client/test_declaration_extension.py --log <tmp>`（期望 157/157）；如需只看某组加 `--only <catalog|trajectory|identity|rejections|schema>`。
3. 复核「不逐项注册」：在 `net-the-spire/Net.TheSpire`、`Net.TheSpire.Contracts`、`Net.TheSpire.Host` 搜 `Bump|Retally|ReachedLimit|declaration:Marker|declaration:Mark\b`（期望 0 命中）。
4. 评审通过后由主清单维护者决定是否并入 `docs/review/records/2026-10-08/`；**父项 ENV-T03 的收尾仍归 A 线**。

## 21:53负责人接收说明

实际状态停止，完整结果取齐至1529，最终turn/end completed。声明扩展实证子包验收：14项报告源码摘要匹配，真实字段/Action/隐藏/内部与身份保证核验；这不等于base完整环境。父项T03唯一主责仍为C，A提供规则依赖，原文“收尾仍归A线”不覆盖主清单。详见[当日复核](dsh-supervisor-review-2026-10-08.md#2153验收与地图公开接续)。移入records只整理日期证据。
