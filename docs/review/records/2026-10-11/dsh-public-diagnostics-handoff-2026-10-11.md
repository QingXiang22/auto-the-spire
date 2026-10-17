# ENV-T03-public-diagnostics-20261011

task_id: ENV-T03-public-diagnostics-20261011

dispatch_record: `.audit/dsh-public-diagnostics-2026-10-11/task-record.json`

session_id: session-3b7358f5-d004-4419-a5d7-54ebbab45b49

request_id: ef85a725-fad4-4ac5-ac15-41ac836b9955

delivery_status: ready_for_review

timezone: Asia/Shanghai

delivered_at: 2026-10-11T01:43:32+08:00

## 交付

C 包将 `base-spire:Run.MapDraws` 声明为 `FieldVisibility.Hidden`，并保留原有地图生成与抽样顺序。公开目录、快照和错误响应继续由 `DerivedPublicProfile` 的通用可见性投影决定，公开地图节点、边、类型、游标、阶段和合法选择保持可用。没有新增 RPC、wire 类型、全 State 序列化或状态缓存。

本地诊断沿现有显式测试/SDK 入口读取 `ControlSession.EngineForTests` 的 `Session.State.GetField`；公开响应不含 `MapDraws` 或词表。Python 客户端新增 `NET_THE_SPIRE_HOST_DLL` 显式路径，缺失时硬失败，证据使用本包独立 Host DLL。0.1 兼容形状与 0.2 schema 无需新增字段；身份和目录由真实声明重新派生。

## 修改范围

- `mod-the-spire/base-spire/BaseSpire/Rooms/MapRoomWiring.cs`：唯一获准的 base 字段接缝。
- `net-the-spire/Net.TheSpire.Tests/FormalPathChecks.cs`：目录不泄漏、两 seed 本地词表非空、重复 observe/RNG/状态纯度、非法/幂等/过期路径不泄漏的检查。
- `net-the-spire/client/net_client.py`：显式独立 Host 选择。
- `net-the-spire/client/test_map_host.py`：移除公开词表断言，增加所有普通响应诊断泄漏检查与重复 observe 快照一致性。
- `.audit/dsh-public-diagnostics-2026-10-11/`：命令日志、公开 NDJSON、构建与检查证据；未改 `task.txt`、`task-record.json`、`inputs-before.json`。

## 实际证据

独立构建命令：

`dotnet build net-the-spire/Net.TheSpire.Tests --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-public-diagnostics`

该构建返回 0，并产出独立 `Net.TheSpire.Host.dll`、`Net.TheSpire.Tests.dll`。C# 直接运行该测试 DLL：`.audit/dsh-public-diagnostics-2026-10-11/check-net.log`，G-01 至 G-04 通过；本地诊断词表计数为 `t05-map-alpha=292`、`t05-map-beta=265`，对应状态哈希 `1eb0f5194799369b`、`4c94cfbc47581e78`，RNG 总抽数 `419`、`393`。

Python 实际使用环境变量 `NET_THE_SPIRE_HOST_DLL=F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-public-diagnostics/bin/Net.TheSpire.Host/debug/Net.TheSpire.Host.dll`，结果：host 21/21、catalog 49/49、trajectory 74/74、rejections 46/46；schema 分组亦执行并记录于公开 NDJSON。日志为 `.audit/dsh-public-diagnostics-2026-10-11/public-map.ndjson`。

根构建命令 `dotnet build AutoTheSpire.sln --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-public-diagnostics` 返回 1。首个错误是并行工作区既有的 `mod-the-spire/base-spire/BaseSpire/Potions/SpirePotionSource.cs(15,15): error CS0103: 当前上下文中不存在名称“EncounterContent”`；本包未修改或修复该药水内容错误。根 Engine/Net/Slice 的最终命令与返回码见对应 `.audit` 日志。

## 最终门槛与身份

- 最终直接运行 Net DLL：返回 0，416 passed / 0 failed / 416 executed；通用 Secret 隐藏字段检查同时通过，证明过滤来自声明可见性。
- Engine DLL：返回 0，95/95。
- Python：返回 0，213/213，包含 schema 23/23；两 seed 的公开选择、出牌与目标回答均留在 NDJSON。
- 随后 Slice 独立构建返回 0（并行源码已改变）；Slice DLL 返回 -532462766，首错 W-01 药水栏槽位：`expected 'CapacityExceeded', got 'ValueOutOfRange'`，位于 `SliceFrontendPotionChecks.cs:157`。未修改 A 的断言或实现。
- doc-check 首轮仅本交接六处行尾空白，已修正；最终返回 0。`git diff --check` 返回 0。

实际公开构建身份（见 `results-summary.json` 和 NDJSON describe/get_catalog）：

| scope | ruleset_id | catalog_id | backend_profile_id |
| --- | --- | --- | --- |
| dev-map | net:ruleset:5df8931221750da0 | net:catalog:05b29d976458bcc6 | net:backend:simulation-0.2-19173f6a9ecbb68f |
| dev | net:ruleset:ab937ce67b363bee | net:catalog:fccc8a2f5818a186 | net:backend:simulation-0.2-aea94737aa498ae5 |

公开 Host 的 BaseSpire DLL SHA256 为 `250bb50a220227c31e6a35ab613f60db5d33fd37a6ca5f118947ed273ce3014d`；随后 Slice 构建的为 `ae9034d1f79eae41025263317291fd7eab2995eebc927ad337ea4f035dd4d025`，不能将后者说成同一公开构建。输入摘要在 `inputs-c-observed.json`，源码/产物实际摘要在 `results-summary.json`。schema SHA256 仍为 `6299696398b5aa14688e432a8097dff3f177de68ac28f87128bc01637dea46ae`；只是字段从派生目录中退出，不改变 wire 的通用结构。受影响规则身份需消费者重新 describe/get_catalog 并使用新身份 start；公开 Boss 夹具由 Codex 后续统一整合。

## ENV-T05 并行协调补充

A 增加正式 Potion 类型、来源于 `SinglePlayerContent.Model.PotionPool` 的48项真实生成目录，以及 PotionBelt/RewardPotions 公开 Sequence；BasePublicSemantics 与正式工厂仍由 A 维护。C 的正式检查没有固定药水数量或旧 ruleset/catalog/build/content/semantics 摘要；目录和 Sequence 计数来自当前声明，身份来自当前 describe。去除 G-01 冗余旧入口比较，保留同一当前构建缺省/dev 一致性，并明确允许内容版本摘要迁移。作者切片的“两瓶药水”断言仅对应作者文本夹具，不是正式药水池合同，保持原兼容检查。

上表与本包公开轨迹是当时构建的历史证据，不是 ENV-T05 最终身份 golden。最终身份与公开路线由 A 统一集成；本包不扩写药水规则或 base 工厂。

## 未验证与下一步

本机制公开与本地边界已验证；根构建首错和后续 Slice 药水首错保留，不能宣称全仓库通过。负责人下一步复核本交接、核对实际日志与 diff，再将本包并入 ENV-T03 总清单；药水包收敛后由主责统一重跑根构建及受影响 Slice。本包至此停止，未运行训练、AI 或真实客户端。
