# 静默猎手中毒牌族交付

本批对应主清单ENV-T12-SILENT-POISON，依据用户2026-10-17集中完成较大主题的指令，一次交付静默猎手全部12张中毒相关卡牌。当前状态与剩余验收只维护在[主清单](../../tofix/README.md#当前交付)。

## 内容与机制

新增Accelerant、BouncingFlask、BubbleBubble、CorrosiveWave、DeadlyPoison、Envenom、Haze、Mirage、NoxiousFumes、Outbreak、PoisonedStab、Snakebite的普通／升级定义。分母从原版SilentCardPool及卡牌直接引用的Power独立提取；不从被测DSL倒推范围。正式卡牌总数176→188，Power45→52，后者含Poison、Accelerant、四项触发能力及Artifact。

新增可复用的随机施加Power、存活敌人Power求和、抽牌／回合开始／未格挡攻击触发及每三次实际施加的计数。计数、奇巧待处理条目和全部局内状态属于Engine；内容不包含按卡ID执行的旁路。Haze在整批手动弃牌后免费自动打出，回合末清手不触发；临时授予奇巧未纳入。

Artifact阻止此Poison路径时消耗一层，不挂毒、不推进Outbreak。死亡目标不挂毒，连锁Outbreak不重复发死亡事件；随机反弹清场后不再抽目标，抽牌触发清场后不再继续抽牌。Artifact对其他debuff的完整参与者仍未迁移。

机制契约见[角色战斗能力](../../../design/mod-the-spire/character-combat/spec.md#静默猎手中毒牌族)，作者语法见[语言参考](../../../dsl/0.0.2/authoring-reference.md#中毒内容的触发规则)。

## 本地验证

环境为Windows、根global.json指定的.NET SDK 9.0.306、Python3.12。基线提交e363b1f；本批修改位于codex/complete-character-cards-20261017。先完成整组实现，再执行以下定向检查，未在本地运行完整solution／全量Slice／net／严格regression。

| 命令（仓库根目录） | 结果与边界 |
| --- | --- |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --poison-only` | 68项通过；8项原Poison生命周期及60项本批内容／组合判别，固定受控seed20261017 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --suite source` | 药水源核对及中毒完整源清单核对2项通过；需要本地sts2 |
| `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --card-selection-only` | 14项通过，保护选牌／手动弃牌组合 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --orchestration-only` | 24项通过，保护出牌、自动打出、动态计数及内容组合 |
| `dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --effects-only` | 25项通过，含效果程序与克隆 |
| `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --public-semantics-only` | 10项通过，目录闭合／只读／身份；内部奇巧条目具名不发布 |
| `python tools/doc-check/check_docs.py`、`git diff --check` | 通过；最终提交前再次检查文档与差异 |

68项中覆盖全部12张牌的普通、创建时升级、Armaments升级，以及实际支付、目标、结果牌区、毒层数、触发次数、自然递减、跨回合余数、满手／NoDraw、Dexterity／Frail、Artifact、末杀、随机消耗与弃牌hook先于奇巧的顺序。受控状态设置只用于这些行为用例，不作为正常整局证据。

冻结hash32的dev规则／公开构建身份由475abd581a69694c／161d18afa369a767迁为db7ab18e687e380d／feb3f5074952e499；dev-map由34b51c466486b0f9／d96534dd8df06257迁为2958d9e779c64116／46130439a68eedb3。迁移原因是新增内容、触发流程、公开ApplicationCount和隐藏奇巧队列，以及清场后的抽牌守卫，不把摘要相等当作行为证据。

## GitHub验证与剩余边界

完整core、base、net和独立参考regression在本地轻量检查完成后，使用现有GitHub Actions工作流执行。运行链接和最终结论在PR及主清单维护，不提前把排队状态计为通过。已有事件／附魔PR是本批父分支依赖，按依赖顺序审阅。

本批证明该12张内容的正式加载与受控行为。未执行本族独立原版逐卡对拍、Silent正常开局／多幕胜负、完整角色剩余牌和120对候选矩阵；这些不因本批完成而关闭。

首次Actions运行39023906760的core及differential通过，base因本地CRLF资源与Git LF资源摘要不同而失败，net因旧诊断Profile缺少SlyDiscards及ApplicationCount处置而失败。已按Git字节重建并更新身份快照，补齐兼容映射；本地仅重跑公开语义10项及net快照26项，均通过。完整回归继续交给更新提交后的Actions。
