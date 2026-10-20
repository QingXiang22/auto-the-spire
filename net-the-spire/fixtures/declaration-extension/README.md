# 规则声明扩展例 fixture（declaration-extension）

本目录是 ENV-T03 声明扩展子包（派单 `ENV-T03-declaration-extension-20261008`）的规则包测试资源。
它不是原版 base-spire 的替代品，也不声称代表完整游戏范围：它只是一个**最小可读的规则声明扩展例**。

## 文件

- `baseline.spire` —— 基线规则包（core/0.2）：`scope Board` / `entity Marker{Value}` /
  `collection Slots` / `procedure Setup` / `terminal ReachedLimit` / `action Add{delta∈[1,2]}` /
  `projection PlayerView`。
- `extended.spire` —— 扩展规则包：相对 `baseline.spire` **只**新增
  `Marker.Bump`（公开字段）、`Marker.Secret`（`visibility = Hidden`）、
  `procedure Retally`（内部块，无任何动作或订阅引用）、`action Mark{amount∈[1,3]}`（修改 `Bump` 与隐藏的 `Secret`）。
- `public-metadata.json` —— 两个规则包**共享**的提供方元数据：不可从声明推导的四类信息
  （内容 ID 目录、集合用途、终局胜负解释、已知位置角色）以及提供方配置
  （静态构建标识、场景 profile、投影 ID、参与者角色、默认游戏 seed）。

两个规则包由**同一个通用规则提供方**加载（core 的 `EngineDslCompiler.LoadFiles`，
见 `core-the-spire/Core.TheSpire/Engine/Compilation/EngineDslCompiler.cs`），
并共享同一份元数据文件；因此 baseline 与 extended 之间的差别只有 `.spire` 文本本身。
net 生产源码里没有针对 `Bump` / `Secret` / `Retally` / `Mark` 的逐项注册行。

## 提供方约定（Host 侧通用文本规则场景）

- 规则来源：`--rules <entry.spire>`（可重复，交给 `EngineDslCompiler.LoadFiles`）。
- 元数据来源：`--public-metadata <file.json>`（本目录的 JSON 格式）。
- 投影：`projection_id` 必须与规则包里声明的 projection 限定名相等，并由
  `DerivedPublicProfile.Build` 从 `RuleSet` + 投影 + 元数据派生公开目录与快照。
- 会话：每次 `start` 用 `new Session(rules, seed, [participant_role])` 创建**已配置、未 Start**
  的独立会话；开局程序只在 `Session.Start()` 时执行一次。
- seed：`start.seed` 是 0.2 的游戏 seed 字符串，由本提供方按自己的文档化映射折算为会话
  `ulong` 种子（FNV-1a 64，见 Host 侧 `TextRuleSource.SessionSeed`）；不做 trim、
  不改大小写、不解析为原版随机布局。成功响应回显的是**游戏 seed 字符串**本身，不是折算值。
  本 fixture 不声明随机绘制，因此 seed 只影响会话标识指纹与回显，不改变玩法快照。
- 场景 profile：来自元数据的 `scenario_profile`，不按规则包名字生成，保证 baseline 与
  extended 报告同一个场景范围，只有规则标识随声明变化。

## 边界

- 本 fixture 不覆盖：多选/整数区间决策、集合成员发布（pile/group/sequence）、地图与奖励选择、
  完整原版内容、训练。遇到当前协议不可编码的结构应当明确 `unsupported`，不得静默删公开事实。
- 本 fixture 不修改 wire 协议，不新增值类型；只用既有 0.2 载荷形状。
