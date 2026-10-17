# 静默猎手单人奇巧牌族交付

本批对应主清单ENV-T12-SILENT-SLY，基线main/f1cd726，开发分支codex/silent-sly-family-20261017。当前状态与后续验收只维护在[主清单](../../tofix/README.md#当前交付)。

## 内容与机制

新增Abrasive、FlickFlack、Reflex、Ricochet、Tactician、Untouchable、HandTrick、MasterPlanner的普通／升级定义。独立读取原版SilentCardPool及CanonicalKeywords，确认单人原生奇巧7张：本批6张加已交付Haze；原版标记MultiplayerOnly的Sneaky不进入单人范围。HandTrick与MasterPlanner是另外两张授予奇巧的消费者。正式加载卡数188→196，Power52→54。

Card.AddedSly保存本场战斗的附加关键词，Card.SingleTurnSly保存本回合状态；它们与定义Sly共同决定实际奇巧。HandTrick先给7／10格挡，再从手牌精确选择当前非奇巧的Skill。MasterPlanner在技能打完后给该实例添加战斗内奇巧，不触及长期牌组。升级保留状态，战斗克隆按创建时状态复制且互相隔离，新实例／变换不继承，回合末清临时状态，下场战斗重建副本。

共享弃牌流程在弃牌hook前捕获奇巧资格，完成整批后按顺序免费自动打出。Engine中的批次号隔离嵌套弃牌，防止内层消费外层待执行条目。Abrasive复用敏捷并新增受击前荆棘：powered攻击算伤后、格挡／生命前反击，全挡和零伤害也触发；反击杀死来源不取消当前命中，后续命中跳过死亡来源。

多能力卡的旧摘要链接现在按本包解析已声明短ID；未知引用仍明确拒绝。正式Profile由base声明派生两个奇巧字段；旧诊断Profile补齐兼容处置。core与参考玩法均未修改。契约见[角色战斗机制](../../../design/mod-the-spire/character-combat/spec.md#静默猎手奇巧牌族)和[作者参考](../../../dsl/0.0.2/authoring-reference.md#奇巧与授予状态)。

## 本地验证

实现整组后再统一执行定向检查。环境Windows、根global.json指定SDK9.0.306、Python3.12；受控战斗固定seed20261017。以下命令均从仓库根目录运行：

| 命令 | 实际结果 |
| --- | --- |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --sly-only` | 52项通过：三种升级路径、免费出牌、精确Skill筛选、临时／战斗内寿命、克隆／变换／升级／下场隔离、嵌套批次、荆棘时点及负例 |
| `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --poison-only` | 68项通过，保护已交付中毒和Haze |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --card-selection-only` | 14项通过 |
| `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --orchestration-only` | 24项通过 |
| `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --public-semantics-only` | 10项通过，目录／只读／构建身份 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --snapshot-only` | 26项通过，公开快照兼容 |
| `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --suite source` | 3项通过，包含独立奇巧源清单核对 |

未在本地执行完整solution／Slice／net／严格regression。完整core、base、net及独立参考regression仅在本地轻量检查完成并推送后交给GitHub Actions；实际运行与结论记录在PR，不提前把排队算通过。源码检查依赖本地sts2，不进入Actions。

冻结hash32的dev规则／公开构建身份由db7ab18e687e380d／feb3f5074952e499迁为8b9877853c09390b／155131ed9ad8a8f7；dev-map由2958d9e779c64116／46130439a68eedb3迁为639897b31bed14b7／ee9f39726c390d25。身份变化对应新卡、动态字段、奇巧批次隔离和受击前触发，资源按Git的LF字节核对。

## 验证边界

本批证明正式加载及受控单人行为，不代表Silent正式开局／多幕胜负、多人Sneaky、全部动态关键词、Omnislice特殊反击条件或独立逐卡原版对拍已经完成。旧中毒牌族及本批源码核对不能替代这些证据。
