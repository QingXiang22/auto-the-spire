# 遗物源清单与实例公共接口

本批按用户“先做一些统计和遗物的一些公共性的接口”的指令，为后续遗物内容提供可复现源分母与共享生命周期。当前状态只维护[主清单ENV-T09-PR02](../../tofix/README.md#env-t09)；规则与准确接口见[整局操作契约](../../../design/mod-the-spire/run-domain/spec.md#遗物实例与生命周期)和[调用参考](../../../design/mod-the-spire/room-compile-reference.md#按操作查找已有公共能力)。本记录为2026-10-17实际交付证据，不关闭完整遗物、Ironclad或环境合同。

## 实际统计

逐件清单由[提取工具](../../../../tools/source-manifest/extract_relic_manifest.py)读取本地原版源和当前构建的`--relic-catalog`输出。完整[JSON](../../../../.audit/relic-foundation-20261017/relics.json)含逐文件SHA256、池顺序、稀有度、覆写方法／属性及准确行号、SavedProperty、命令引用导航、base声明；[完整表格](../../../../.audit/relic-foundation-20261017/relics.md)便于逐件查看。

| 口径 | 本次结果 |
| --- | ---: |
| Relics目录C#文件 | 298 |
| 直接继承RelicModel的遗物类 | 297 |
| 各池成员记录／去重身份 | 300／297 |
| 共享池 | 118 |
| 五角色池 | 各8，共40条成员记录 |
| 事件池 | 140 |
| 回退／废弃池 | 各1 |
| base正式声明／可获得声明 | 4／4 |
| 未在base声明的源遗物 | 293 |
| 带SavedProperty的遗物类 | 38 |
| 覆写ShowCounter的遗物类 | 39 |
| 覆写IsAllowed类资格入口的遗物类 | 23 |

目录中的`VakuuCardSelector`是辅助类，不算遗物；LastingCandy、RazorTooth、SparklingRouge分别同时出现在事件和共享池，不能直接相加池数。源稀有度分布为Ancient100、Common30、Event35、None2、Rare50、Shop30、Starter10、Uncommon40。池身份和稀有度分别统计，事件池不等于Event稀有度。

常见覆写入口：AfterObtained82、AfterCombatEnd41、AfterRoomEntered33、AfterSideTurnStart29、AfterCardPlayed28、BeforeSideTurnStart21、BeforeCombatStart19、ModifyMaxEnergy12。每个方法按遗物类去重计数，仅说明源码存在该入口，不证明整个调用树或资格已完成审查。表现层属性与命令引用保留作导航，不将其当作缺失玩法数量。

保留全部源分母，包括废弃和回退。单人可达性仍需逐件裁定；没有用297作为单人可达件数。没有把正式声明4或CanObtain4当作逐件行为／独立原版／公开完成率；机器清单的三种验证字段保持null。

## 公共接口与已有消费者

扩展既有`RunRelics`，共享`RelicSchema.Declare/New`定义与初始化实例。Counter为隐藏字段，IsWax／IsMelted公开；所有值仍在Engine。提供IsOwned／IsActive／HasActive、SetCounter／AddCounter、Obtain／Remove／Replace／Melt及构建期AfterObtained／AfterRemoved续接。

Replace捕获旧实例与列表下标，目标不支持时在移除前失败；正常先移除旧实例及回调，再创建新实例到原位置，计数重置。Melt要求持有、蜡质且未熔毁，保留实例与计数、执行移除回调，后续效果排除熔毁实例。Remove回调可读已离开列表的旧实例，执行后才销毁；获得收益不自动回滚。多次显式Obtain各执行自身效果，动作去重与资格由来源负责。

现有开局使用同一字段配方；房间服务、宝箱／商店／奖励沿既有Obtain消费入口。胜利监听按持有列表顺序逐有效实例执行，熔毁燃烧之血不再回血；既有存活和最大生命封顶保留。拒绝尚未支持的触发点／效果组合，避免将胜利获金误编成回血。

修改base领域／装配／定向检查、tools/source-manifest与文档，没有修改core、net协议、独立参考或源游戏文件。现有四件遗物仍为BurningBlood、Pear、Mango、OldCoin；本批没有批量添加遗物效果。

## 构建与验证

源码依据为本地`RelicCmd`、`Player`和各遗物／池文件，统计JSON记录实际内容摘要，不声称更新至游戏最新版本。受控命令用Engine seed42；正式工厂检查沿用`FrozenIronclad`、A0／all-unlock／singleplayer／dev、固定seed`ENV-IRONCLAD-00`，仍是受控机制场景。

| 验证 | 实际执行与结果 | 日志 |
| --- | --- | --- |
| 受影响测试工程构建 | rc0；首次base重编译4项既有警告，最终增量0警告／0错误 | build.log、build-final.log |
| 遗物生命周期与Run资源／正式工厂 | 13项rc0；包括替换保位、移除顺序、蜡质守卫、计数隔离、原获得hook和实际胜利回血／熔毁停效 | lifecycle.log |
| 后补同定义不同实例判别 | 仅执行新增1项rc0，没有重复上述已通过组 | counters.log |
| 公开语义与默认身份 | 10项rc0，目录仍是实际4件遗物；新字段由规则公开投影派生 | public-semantics.log |
| 既有宝箱获得入口 | 3项rc0，包括取得效果与重复动作拒绝 | treasure.log |
| 静态清单自查 | 297身份闭合、300成员对账、全部方法／属性行号核对、确定性、无base输入不伪装0、错误目录身份拒绝 | inventory-validation.log、statistics.log |
| 文档与补丁 | 688份Markdown、0问题；git diff --check rc0 | doc-check.log、diff-check.log |

日志均在[本批证据目录](../../../../.audit/relic-foundation-20261017/)。目录辅助类与跨池重复已经显式对账，未删除源条目或缩池。

规则身份因实例Schema、初始化配方和胜利监听IR而迁移，原内容资格与随机算法未变。实际`--describe-build`见[身份输出](../../../../.audit/relic-foundation-20261017/build-identities.ndjson)：

| 配置 | 原rules／build | 新rules／build |
| --- | --- | --- |
| dev | 87b1e83c4318a488／d3484e3e0486354a | 2ee95d1e50b93b0a／6ccdb4ee30acd725 |
| dev-map | 6ed7062e03b9867d／74ef43415f47d401 | 466c1e5dbb31bf7d／8c82420f95a29547 |

重现入口（先构建，随后使用同一输出，不并行构建）：

```powershell
dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --no-restore
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --relic-lifecycle-only
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --public-semantics-only
dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --treasure-room-only
```

完整遗物／Run定向组现在合计14项，实际证据为先13项及后补1项分别通过。统计重现命令见[工具说明](../../../../tools/source-manifest/README.md#遗物内容与接口统计)。完整Engine／Slice／net／严格回归留CI，本批没有执行或宣称通过；没有新独立双侧、正常获得／使用或公开Host轨迹。

## 尚未交付的范围

仍缺逐件资格及可达裁定、293件内容声明与实际效果、完整StackCount／多变量／FloorAddedToDeck／历史、全部模型hook与修正排序、可暂停的真实遗物消费者及其来源。Counter不是完整DisplayAmount，蜡质标记不表示WaxChoker等特殊效果已实现。SwordOfStone→SwordOfJade、ChosenCheese、PotionBelt及正常事件／房间取证留后续内容批次；它们不能因接口存在而标为已完成。完整主合同继续开放。
