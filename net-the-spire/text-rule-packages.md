# 文本规则包场景（`text-rules`）：通用规则提供方接缝

本文件说明 Host 的第三个场景 `text-rules`：用**同一份通用提供方代码**加载任意文本规则包（core/0.2），
并由公开声明派生目录、观察、候选与标识。它服务于
[公开语义透传的实施边界](../docs/design/net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续)
第 4 项：**新增一个公开字段或一条合法 Action 只改规则包**，net 生产源码不加逐项注册行。

## 1. 组成与责任

| 文件 | 责任 |
| --- | --- |
| `Net.TheSpire.Host/TextRuleProviderSpec.cs` | 读 `--public-metadata` JSON（`fixture_format = net-the-spire/declaration-extension-metadata/1`）。未知顶层键、未知格式、未知集合用途、形状错误一律抛 `NotSupportedException`，不静默丢弃。 |
| `Net.TheSpire.Host/TextRuleSource.cs` | `ISessionSource` 的通用文本规则实现：`Rules` / `Profile` / `ScenarioProfile` / `RngProfileId = rejection-uniform@1`，`CreateSession` 每次返回**已配置、未 Start** 的独立 `Session`（开局程序只在 `Session.Start()` 执行一次）。 |
| `Net.TheSpire.Host/TextRuleScenario.cs` | `public const string Scenario = "text-rules"`；`Create(rulePaths, spec, ledgerCapacity, defaultGameSeed)`：`EngineDslCompiler.LoadFiles` → `DerivedPublicProfile.Build(rules, projectionId, role, metadata, NetProtocol.V02)` → `ControlHost(source, ledgerCapacity)`。 |
| `Net.TheSpire.Host/Program.cs` | 只多一个 `else if (scenario == TextRuleScenario.Scenario)` 组合分支与 `--rules` / `--public-metadata` 两个开关；没有按字段/动作分支的注册表。 |
| `fixtures/declaration-extension/` | 规则包测试资源：`baseline.spire` / `extended.spire` / 共享 `public-metadata.json`（用途与边界见该目录 `README.md`）。 |

`DerivedPublicProfile` 是既有的通用派生器（`Net.TheSpire/Projection/DerivedPublicProfile.cs`）：
kind / 字段 / 枚举 / 引用 / 动作 / 终局原因全部从 `RuleSet` 声明按限定名派生，
动作只来自 `rules.Actions`（procedure/内部块不进 `tables.Actions`，因此不会发布成 Action）。

**不逐项注册的可核对验证记录**（生产程序集与客户端里没有本 fixture 的任何名字）：

```
grep -rn 'Bump|Retally|ReachedLimit|declaration:Marker|declaration:Mark\b' net-the-spire/Net.TheSpire net-the-spire/Net.TheSpire.Contracts net-the-spire/Net.TheSpire.Host  →  0 命中
```

命中的只有两处**检查脚本**（`Net.TheSpire.Tests/DeclarationExtensionChecks.cs`、
`client/test_declaration_extension.py`），它们断言的是派生结果，不是注册来源。

## 2. 用法

```powershell
dotnet net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll `
  --scenario text-rules `
  --rules net-the-spire/fixtures/declaration-extension/extended.spire `
  --public-metadata net-the-spire/fixtures/declaration-extension/public-metadata.json `
  --game-seed DECL-EXT-00
```

- `--rules` 可重复（多文件按序交给 `EngineDslCompiler.LoadFiles`）；`--public-metadata` 只属于本场景，两者必须同时给。
- `--game-seed` 缺省时用元数据的 `default_game_seed`；本场景不接受 `--test-headbutt` / `--author-content`。
- seed 折算：`TextRuleSource.SessionSeed` 用 FNV-1a 64 折叠整串 UTF-8（不 trim、不改大小写、不解析为原版随机布局）；
  成功响应回显的是**游戏 seed 字符串本身**（`ProvidedSession.AcceptedSeed`）。
- 元数据里四类**不可从声明推导**的信息由提供方固定：`content`（内容 ID 目录）、`collections`（集合用途）、
  `terminal_outcomes`（胜负解释，只能 `win`/`loss`/`other`）、`known_positions`（已知位置角色）。
  缺集合/终局条目会被 `DerivedPublicProfile` 按规则号拒绝（`N-02-UNDECLARED-COLLECTION` /
  `N-02-TERMINAL-OUTCOME`），多余键不参与派生。
- 标识：`ruleset_id`/`catalog_id`/`backend_profile_id` 全部由声明表 + 投影 + `static_build_id` 派生；
  **每局 seed 不进任何标识**（`DerivedPublicProfile.Build` 不接收 seed）。只改 `.spire` 文本即换来新的
  catalog/ruleset 标识，`scenario_profile` 保持同一范围 id。
- 只读游标：`RuleSet` 在场景构造时加载一次，`TextRuleSource` 不重算玩法、不另造默认会话。

## 3. 检查与验证记录

| 入口 | 结果 | 覆盖 |
| --- | --- | --- |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 410 executed / 410 passed / 0 failed | E-01 目录 delta；E-02 真实轨迹（`start→observe→step→observe`）；E-03 规则变化换标识、seed 不进标识；E-04 已配置未 Start 的独立会话与 seed 回显；E-05 候选闭合（`choices` 值域决定候选、内部 procedure 不出现）；E-06 过期候选/错 expected_identity 推进前拒绝 + 重发幂等 + 换载荷冲突；E-07 未支持结构显式抛错；E-08 每类真实响应帧通过 0.2 wire schema |
| `python net-the-spire/client/test_declaration_extension.py` | 157 passed / 0 failed | 真实宿主进程 NDJSON：目录 delta、隐藏字段不透出、轨迹与候选、标识、拒绝与幂等；每帧用同一份 `docs/contracts/net/0.2/wire.schema.json` 校验（含按 `$defs` 名字校验目录/快照/结果文档） |
| `python net-the-spire/client/test_client.py` | 103 passed / 0 failed | 本包修改 `net_client.py` 后的既有 0.1/0.2 客户端套件回归 |
| `dotnet build net-the-spire/Net.TheSpire.Tests/Net.TheSpire.Tests.csproj` | 0 警告 0 错误 | 依赖链 Core.TheSpire → Contracts → Net.TheSpire → BaseSpire → Host → Tests |

验证记录日志（真实宿主进程的 ready 横幅 + 每帧请求/响应/规范字节）：
声明扩展与当前纯 DSL 公开行为由 `python tools/ci/check.py net` 检查，结果在 PR CI 查看。

## 4. 边界（本场景不作声称）

- 本场景**不是**完整 base 原版，也不代表全游戏范围；它是「声明扩展只改规则包」的最小可读验证记录。
- 多选/整数区间决策形状、集合成员发布（pile/group/sequence）、地图/奖励选择、训练不在本场景范围；
  遇到当前协议不可编码的结构必须显式 `unsupported`，不得静默删公开事实。
- 本场景不修改 wire 协议、不新增值类型，只用既有 0.2 载荷形状。
- 客户端 `net_client.py` 的两处修复（错误帧不再往 `error` 对象注入 `request_id`；`stderr_text()` 处理二进制 `stderr`）
  只恢复 wire 形状与读取能力，不改变请求语义。
