# 战斗开始格挡遗物的 DSL 迁移

关联 CORE-AUTHOR-05／DSL-RELIC-START-01；主责 Codex `/root`。2026-10-18用户要求将已编写 skill 安装到 Codex 并实际迁移部分内容。本包基于 main/b9ce5a3，按[纯 DSL 规格](../engine-dsl-migration/spec.md)和[机制迁移指南](../../../development/base-spire/how-to/migrate-mechanism.md)交付；状态只在[路线图](../../../roadmap.md#近期队列)维护。

## 已核实来源与范围

通过 skill 定位并核验原始字节 SHA256，读取 Anchor.cs、FakeAnchor.cs 的 CanonicalVars 与 BeforeCombatStart。Anchor 每场开始获得 10 无加成格挡；FakeAnchor 为原版 Event 遗物，每场开始获得 4。两者复用现有 GainUnpoweredBlock 与 Relics 持有实例过滤，不需要卡牌出牌、伤害、Power、地图或正常 Run。

本次只迁移这两个原版遗物与共享事件链，并提供显式开发态开始／结束动作以实际执行多个周期。不宣称正常战斗初始化、回合、胜负、跨房写回或原版随机已支持。原抽牌开发 scope 保持，公开包版本升为 0.5.0-dsl.2；未支持的正常 scope 继续拒绝。

## 行为要求与设计

| ID | 契约 | 判别 |
| --- | --- | --- |
| RC01 | Player 增加 CombatActive=false；只有 Idle→Active 的开始操作允许发 BeforeCombatStart(owner) | 活跃期重复开始不发布合法候选；过程直接调用同样拒绝，不能重发事件 |
| RC02 | 开始先验证 inactive，再将 Block 置 0、CombatActive 置 true，然后 emit BeforeCombatStart | 监听器观察到已活跃且本场初始格挡为 0；不得在 emit 之后清掉监听获得的格挡 |
| RC03 | 结束只在 active 时允许，置 CombatActive=false，不发新开始事件；下一次开始重新置本场 Block=0 | 结束后再开始分别获得 10／4，不能累加成 20／8；重复结束拒绝 |
| RC04 | 共享 DSL 模板 BeforeCombatStartBlockRelic 按库存顺序过滤 Holder、IsMelted 和 DefinitionId，调用现有无加成格挡 | Anchor=10，FakeAnchor=4，二者同时=14；无遗物／已移除／熔化／别的 Holder 不触发 |
| RC05 | 内容文件仅 use 模板参数，原源资格、ID、hash、池和 297 分母不改变 | catalog 只新增两个非空 definition，公开目录新增两个真实 ID；不增加 C# 效果分支 |
| RC06 | 旧 TheAbacus 洗牌链与新开始链独立组合 | 两类遗物持有时开始获得 10，再实际重洗获得 6，最终 16；普通获得／结束不错误触发 |
| RC07 | 默认 Host 读同一包，从合法动作执行开始／结束，观察同一 Player.Block／CombatActive | 真 stdio 获得两遗物→开始 14→结束→再次开始 14；身份随新包变化 |
| RC08 | Codex 用户 skill 安装指向当前仓库技能，实际从安装入口读取和定位后执行本任务 | 安装目标原先不存在；校验真实目标、脚本与文档路径，查询 Anchor／FakeAnchor 来源及 hash；不假装独立 agent／DSH 验证 |

Player 的 combat 字段放 combat/schema，开始／结束过程与 BeforeCombatStart 放 combat/lifecycle；模板放 relics/rules，具体配置各放 relics/shared 单文件。开发动作显式命名 BeginDevelopmentCombat／EndDevelopmentCombat，不能命名成正常 Run 开始或伪造战斗胜利。

## 实施与验证顺序

1. 安装并验证用户级目录链接；若安装位置冲突则保留现有内容，不覆盖。先解析 SKILL.md 的真实路径阅读对应手册，再用安装目录脚本定位源。
2. 实现 DSL 生命周期、模板与两份内容配置，登记索引／源目录／公开元数据和新包版本，始终只在 base 中保存 .spire／静态 .json。
3. 在包外追加有判别力的生命周期／隔离／组合检查，按真实执行场景分别生成三个原版遗物的见证，不复用 TheAbacus 场景冒充新遗物。原检查继续执行，源分母和原对拍失败语义不变。
4. 通过真实 stdio 验证公开轨迹，更新准确操作与领域手册及安装方式，记录来源／版本／命令／结果和 skill 实际阅读边界。

最终运行 solution 构建、包行为／覆盖、受影响 net 与迁移边界、文档／skill 校验；本次不改 Engine 语义，通用 Engine 全套由 Actions 验证。不运行独立参考或完整游戏矩阵，不将开发探针当作正常流程证据。若 DSL 表达不足，回到此契约并给最小复现，不用 C# 回调绕过。

## 遗物机制索引

按匹配版本的完整来源确定消费者。分组用于查找共享机制，当前排期见 [路线图](../../../roadmap.md)；实现数由 CI 生成。

| 分组 | 消费者示例 |
| --- | --- |
| RELIC-G01 拾取随机永久升级 | Whetstone、WarPaint、SandCastle；类型／张数参数化 |
| RELIC-G02 永久选牌修改 | Pomander、YummyCookie、EmptyCage、PreciseScissors；升级与移除分别子包 |
| RELIC-G03 卡牌创建修正 | MoltenEgg、ToxicEgg、FrozenEgg；附魔参与者另接依赖 |
| RELIC-G04 回合资源与开场效果 | Candelabra、Chandelier、VeryHotCocoa、FakeHappyFlower、Sai；按事件和资源子包 |
| RELIC-G05 攻击与格挡修正 | Akabeko、PenNib、MiniatureCannon、MysticLighter、PaperPhrog、PaperKrane |
| RELIC-G06 出牌／牌区反应 | GamePiece、JossPaper、CharonsAshes、ToughBandages、Tingsha、UnceasingTop |
| RELIC-G07 抽牌、留牌与行动限制 | Pocketwatch、Fiddle、RunicPyramid、RingingTriangle、VelvetChoker、SneckoEye |
| RELIC-G08 受损、阈值与死亡 | RedSkull、SelfFormingClay、DemonTongue、TungstenRod、TheBoot、LizardTail、FragrantMushroom |
| RELIC-G09 药水库存与触发 | PotionBelt、AlchemicalCoffer、PhialHolster、Sozu、ReptileTrinket |
| RELIC-G10 奖励生成与资格 | WhiteBeastStatue、BlackStar、PrayerWheel、WhiteStar、Orrery、AmethystAubergine |
| RELIC-G11 房间与长期资源 | VenerableTeaSet、FakeVenerableTeaSet、EternalFeather、MealTicket、Pantograph、MawBank |
| RELIC-G12 商店参与者 | MembershipCard、TheCourier、WongoCustomerAppreciationBadge、FakeMerchantsRug |
| RELIC-G13 营火参与者 | Girya、Shovel、MeatCleaver、MiniatureTent、StoneHumidifier、DreamCatcher |
| RELIC-G14 永久生成／复制／变换 | Astrolabe、PandorasBox、NewLeaf、DollysMirror及生成牌拾取者 |
| RELIC-G15 附魔与关键词 | GnarledHammer、Kifuda、PunchDagger、RoyalStamp、FresnelLens、WingCharm |
| RELIC-G16 地图、古代人与跨域长期效果 | GoldenCompass、JuzuBracelet、WingedBoots、FurCoat、LordsParasol |
| RELIC-G17 遗物替换／熔化 | SwordOfStone、TouchOfOrobas、ToyBox及升级遗物 |
| RELIC-G18 其他角色与特殊规则 | CrackedCore、GoldPlatedCables、BigHat、MiniRegent、Byrdpip、PaelsLegion等 |
| RELIC-G19 拾取基础资源 | FakeMango、LoomingFruit、NutritiousOyster、SignetRing、GoldenPearl |
| RELIC-G20 源特例复核与归组 | 尚未确定主要机制、只有属性覆写或有多个跨域阶段的剩余条目 |
