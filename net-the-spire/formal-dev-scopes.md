# 正式宿主（`formal-dev`）的范围选择与地图公开路径

本文件是 [Net.TheSpire.Host](Net.TheSpire.Host/Program.cs) 正式场景的**使用参考**：怎么用一个范围创建会话、范围如何进入公开身份、地图公开事实从哪来。语义来源见 [设计 §7](../docs/design/net-the-spire/design.md#7-2026-10-07公开语义透传的源码核对与接续) 与 [协议](../docs/design/net-the-spire/protocol.md)。

## 命令行

```text
Net.TheSpire.Host --scenario formal-dev [--scope dev|dev-map] [--game-seed <字符串>] [--frame-limit N] [--ledger-capacity N]
```

- `--scenario formal-dev` 走唯一共同工厂 `BaseSpireRuntimeFactory`：Host 只**配置**一个未 Start 的独立 Session，并在第一个请求时启动**一次**；不另造默认随机 Session，也不重算玩法。
- `--scope` 只在 `formal-dev` 下有效；其它场景给了范围会 rc=2 显式拒绝。未知范围名同样 rc=2，stderr 列出可选值（`dev` / `dev-map`）。
- 省略 `--scope` 即 `dev`，与旧调用**身份与行为逐字不变**。
- `--game-seed` 是字符串 seed，**不进身份**；同一范围换 seed 得到同一身份、不同轨迹。

启动横幅（stderr 第一行）自报范围，可作现场核对：

```text
net-the-spire host ready: scenario=formal-dev protocol=0.2 scope=<范围id> scope_name=<名字> build=<公开构建id> ruleset=<规则集id> projection=base-spire:PlayerView rng_profile=vanilla-uniform@1 default_game_seed=ENV-IRONCLAD-00 frame_limit=1048576
```

## 两个范围的公开身份（2026-10-08 实测）

| 名字 | 范围 id | ruleset_id | catalog_id | backend_profile_id | build |
| --- | --- | --- | --- | --- | --- |
| `dev` | `base-spire-singleplayer-dev-v1` | `net:ruleset:4206675da0ca12f6` | `net:catalog:3440d8467b85ae03` | `net:backend:simulation-0.2-ca6767486c903ad4` | `52b285cbc40711cc` |
| `dev-map` | `base-spire-singleplayer-dev-map-v1` | `net:ruleset:508a1c95ddb54d1d` | `net:catalog:6f998eb6e876bea0` | `net:backend:simulation-0.2-99f5c5a6d155077c` | `29abce0b690a3d22` |

范围进入 `ruleset_id`/`catalog_id`/`backend_profile_id`/公开构建 id；seed 不在其中。`dev-map` 多一条地图随机流（`base-spire:run:act_1_map`），所以 backend profile 摘要也不同——**判身份必须看整组，不要只看规则集**。

## 地图公开事实怎么来的

`dev-map` 下 `base` 声明的 `MapNode`（实体）、`MapNodes`（`Run` 拥有的有序集合）、`MapChildren`（`MapNode → MapNode` 关系）、`ChooseMapPoint`（动作 + `Node` 参数）全部由 [DerivedPublicProfile](Net.TheSpire/Projection/DerivedPublicProfile.cs) 从引擎声明 + base 的公开语义元数据**自动派生**：

- 字段形状来自声明（`number`/`category`/`boolean`/实体引用），枚举成员按 `net:cat:<枚举限定名>:<成员名>` 派生；
- 关系默认公开（引擎 `RelationSymbol.Public` 默认真）⇒ 关系条目自动发布；
- 集合的「谁拥有、元素是什么」来自 base 的集合意图（`BasePublicSemantics`），net 侧不逐项登记 mod 字段/动作，也不重判可见性；
- 候选来自引擎稳定边界的公开 `legal_actions`，不含隐藏状态与 RNG。

因此新增地图声明**只需改规则包**；net 侧没有对应的登记表可改。

## 已知边界

- 尚未实现的房型（非 Monster）：base 具名 `Fail`，会话进入 `Faulted`，本路径如实呈现，不伪装可继续。
- 连续攻击的正常攻击故障（`base-spire:PowerStacksOf`）归 base 主责，见交接与 `.audit/` 最小前缀。
- `EncounterPool`/`EncounterQueue` 的公开摘要仍是开放问题，未被本路径降低可见性或补造。

## 怎么复跑

```text
dotnet build AutoTheSpire.sln
dotnet run --project net-the-spire/Net.TheSpire.Tests                 # C# 侧真实检查（含 G 组）
python -X utf8 -u net-the-spire/client/test_map_host.py --log out.ndjson   # Python 侧真实子进程对照
```
