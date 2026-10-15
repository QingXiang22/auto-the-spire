# 作者契约修复验收

任务ID：ENV-T06-author-contracts-20261010；Codex /root；限定本地语义验收。

Power不再按内容ID白名单拒绝：注册条件改为已实现的角色/触发器/效果组合；base规范名字表仅对base命名空间生效。作者工具先加载全部Power，再链接多文件卡牌，局部优先与限定ID引用，冲突拒绝。onExhaust与旧on_exhaust都接受外部消耗，不要求自带消耗关键词。

已有实例升级采用变量/基础费用的差额，保留成长和CostModifier；升级关键词迁移、重复升级无操作。直接升级牌从完整配方创建，升级费用、变量、关键词、进战斗hook成立。依据reference/sts2-sim/src/Sts2Sim.Core/Models/CardModel.cs:549以及Cards/Thrash.cs:40、Rampage.cs:32的增量OnUpgrade。无多级升级/降级。

修改：base Slice前端/CardDefinitions/Deck初始化；Slice检查；tools/base-spire-dev与指南。core/reference生产文件未改。五项新增判别覆盖跨文件自定义Power、外部消耗、正常弃牌、真实Armaments增量升级与重复升级、直接升级初始化及entry hook。变量成长fixture是受控初态，不能视为正常整局证据。

验证：根build 0 error；Engine 95/95；非地图Slice401/401；作者工具15/15；doc-check334文件0问题、git diff --check通过。完整Slice在首次dev-map建局失败，strict85项奖励池缺14卡；net410 passed/5 failed/415 executed，均受同门槛影响。工厂加载79卡/31能力/35敌人。dev ruleset=6cc014f2ad1c8a90、build=3a7ffe81d13a81fa、semantics=ecf0d4d2e1f05429。证据在.audit/author-contracts-2026-10-10/下combat.log/full.log/net.log/tool.json/identity.log。

下一机制包是第一幕基本正常流程，包括真实奖励依赖、营火/宝箱和Boss。未做独立源双侧、公开第一幕全程或全卡覆盖；不关闭T05/M01。按用户最新授权，正常Boss流程完成之后再开启DSH新会话完成剩余卡牌分类包，不恢复旧会话。
