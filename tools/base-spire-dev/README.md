# 验证与查看纯 DSL base 包

本工具在仓库根目录加载 Engine 的纯文本包，不调用旧 C# 玩法。需要根 `global.json` 指定的 .NET SDK。省略 manifest 时加载 `mod-the-spire/base-spire/package.json`。

```powershell
dotnet run --project tools/base-spire-dev -- validate
dotnet run --project tools/base-spire-dev -- describe
dotnet run --project tools/base-spire-dev -- sync-content
dotnet run --project tools/base-spire-dev -- check --report .artifacts/base-dsl/behavior
```

`validate` 检查文件边界、引用、严格静态数据与公开元数据，并实际启动 DSL Session。`describe` 输出包摘要、规则标识、scope 和源／DSL 定义计数。`check` 执行当前 base 的代表性行为、边界与非法输入检查；它是包外的 C# 检查脚手架，不提供玩法实现。`--report` 写入实际通过场景及卡牌／遗物等行为执行结果，供 [DSL 遗物覆盖检查](../ci/check_relic_coverage.py)与[卡牌完整覆盖检查](../ci/check_card_coverage.py)消费。内容模型定向验证可用 `check --content-model-only`；铁甲战士定向调试可用 `check --ironclad-only`，卡牌奖励可用 `check --card-rewards-only`，原版随机源向量可用 `check --rng-only`，它不生成整包覆盖报告；最终仍运行完整 `check --report`。

`sync-content`从实际CardLibrary／PowerLibrary及显式启动实例确定性派生这两类公开ID，只写public/metadata.json相应content_ids；公开可见性政策、来源catalog和池顺序继续显式维护。validate／check同时核对可执行ID、来源文件映射及公开列表，缺登记、错误映射和重复分别失败。该审计针对当前base的定义库形状，不是通用包的内容推断器。

行为迁移定向检查使用`check --card-behavior-only`，覆盖正文／执行、攻击／结果、费用／关键词、复制／附魔、Power来源、资源及开发事件。可加`--filter "LIFECYCLE "`或`--filter "VISIT "`按不区分大小写的名称前缀筛选；空筛选／零匹配返回2，不能与其他定向参数或report混用。它不生成整包覆盖报告。接口与未支持范围见[行为操作参考](../../docs/development/base-spire/reference/card-behavior.md)。

可在命令后传入其他 manifest 路径，再使用 `--scope <id>`。本版 `check` 用于当前 base 开发范围；`validate`同样核对当前base定义库，不把加载成功记作行为通过。退出码 0 为当前命令完成，2 为格式／加载失败，3 为未支持。旧 `play`、`probe` 和直接传卡牌 `.spire` 的入口明确返回 3。

遗物内容接续组可用 `check --relic-content-only` 定向验证拾取资源、动态抽牌／营火治疗、出牌计数和首次受损抽牌。该命令不生成覆盖报告；形成逐件验证记录时仍运行完整 `check --report`，再由 `tools/ci/check_relic_coverage.py` 核对实际定义与执行结果。源目录保持297项，不能把局部通过写成全遗物完成。

调试单个遗物机制时，可按检查名称筛选：先运行一次`dotnet build tools/base-spire-dev`，随后用`dotnet run --no-build --project tools/base-spire-dev -- check --relic-content-only --filter "AM "`只执行攻击修正组。`--filter`按不区分大小写的名称前缀匹配，不能与其他定向参数或`--report`混用；空白筛选、零匹配返回2。每项输出实际耗时，末尾列执行数、跳过数和检查总耗时；总耗时不包含入口加载，进程总耗时应另计。完整覆盖报告由批次最终CI生成，本地已有相同包摘要的报告时复用，不因补文档重跑。

简单拾取资源的数值差异复用逐件表驱动；同组统一验证本次实例／持有者隔离、直接获得、拾取前失效、移除不撤回、重新获得与跨战不重放。真实stdio还执行五件资源拾取及逐件移除，公开最大生命／生命／金币保持永久结果。商店价格、Ancient来源和资源修正者仍按各领域合同恢复，不把显式开发获得当作正常来源。

遗物定向入口也执行CombatTurnRelicChecks：指定回合能量、每3／5次跨战计数和Sai每回合无加成格挡；复用已有回合门、移除／熔化与持有者隔离判别，补NoEnergyGain、资源上限、不同实例进度及Anchor／HornCleat组合。公开stdio的turn-start-resources轨迹实际获得五件遗物，执行第1至4回合，再跨战验证花朵第5次与Cocoa叠加；不注入资源结果。

AttackModifierRelicChecks随同一定向入口执行MiniatureCannon／PaperPhrog／PaperKrane：升级资格、显式攻击者或牌拥有者、有无状态与正确敌人、力量／虚弱／易伤／Cruelty／格挡顺序、移除／熔化、自动／重复和只读预览。公开cannon-phrog轨迹从永久升级到实际Bash易伤与升级Strike伤害；paper-krane轨迹实际施加Shockwave，再观察敌人的攻击／增益／攻击三回合，未注入伤害结果。

同一遗物定向入口还执行ChemicalX的捕获／支付与效果X分离、零能量、普通／升级、嵌套自动与重复出牌、实例／持有者隔离、移除／熔化、纯查询和选牌暂停续接判别。原版星星X、正常商店获得与独立参考比较仍未验证；约定见[效果X修正](../../docs/design/mod-the-spire/relic-combat-start/content-lifecycle.md#效果-x-修正dsl-relic-content-03)。

拾取随机升级组也使用`check --relic-content-only`，验证Whetstone／WarPaint／SandCastle的类型／张数、完整Niche洗牌、固定源选中实例／事件顺序／游标、重复获得、嵌套调用、无临时状态残留及两战副本。向量只来自独立源码神谕，生成方式见[rng-oracle](../rng-oracle/README.md)；公开stdio逐件观察永久升级和实际攻击／格挡，不构成正常Run。

`base-spire-dsl-relic-draw-dev-v1` 是单玩家、全部解锁的机制开发范围，覆盖实际出牌／选敌／选牌、耗尽、X 费、费用修改、复制／生成／变换、自动与重复出牌、所需 Power 反应、已有敌人／遗物／药水、营火与金币奖励。铁甲战士以完整源池 87 项核对 85 张单人牌，保留两项多人排除；准确共享入口和未支持边界见[铁甲战士操作参考](../../docs/design/mod-the-spire/ironclad-dsl/shared-operations.md)。显式 Ironclad 基础初态、原版随机算法／流布局和自动起手已接入；包含 Neow 的正常开局、完整随机前置消费和正常 Run 不属于此 scope；不使用旧 C# 运行回退。当前数量与交付验证记录从[路线图](../../docs/roadmap.md#近期队列)定位。

敌人招式图可定向运行 `dotnet run --project tools/base-spire-dev -c Release -- check --enemy-moves-only`，包含FuzzyWurmCrawler固定轮转、TwigSlimeM首招及随机限制、Slimed实际出牌和共享图失败检查。该选项不能与其他定向参数或报告输出组合；完整报告仍使用普通check。正常遭遇和完整独立比较不由该定向检查验收。

分阶段学习与实际公开结果见[base 开发教程](../../docs/development/base-spire/README.md)。教学驱动复用现有 Python 客户端，不修改正式包或原版行为执行结果。

金币奖励在胜利后先显示待领金额；通过 `ClaimDevelopmentGoldReward` 领取，再用 `CloseDevelopmentRewards` 关闭，也可直接关闭跳过。未关闭时不能开始下一战或进入营火。详见[奖励约定](../../docs/design/mod-the-spire/combat-rewards/spec.md)。公开路径的定向检查使用 `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only`；默认不带参数的 net 入口仍运行完整迁移检查组。

永久牌组与营火锻造使用已迁移定义的开发获得入口，例如 `Strike.Obtain`。营火从公开 RunDeck 选择可升级永久实例，选择／取消走通用 `net.answer_decision`；取消不消耗选项，成功后显式离场。后续战斗重建副本，临时升级不反写永久牌；显式 Ironclad 初态使用原版 Shuffle 流洗牌并自动抽牌；完整前置消费与正常开局仍未迁移。见[永久牌组与锻造约定](../../docs/design/mod-the-spire/permanent-deck-smith/spec.md)。

显式 Ironclad 的首幕开发战斗胜利还提供三张不同的卡牌奖励。通过 `ClaimDevelopmentCardReward` 候选选择一张，所选同一实例加入 RunDeck；未选实例销毁。`SkipDevelopmentCardReward` 仅跳过卡牌，金币仍可领取；`CloseDevelopmentRewards` 丢弃全部未领奖励。奖励使用完整80张可奖励源牌、稀有度偏移与去重，已使用原版随机映射和共享 Rewards 游标，但药水掉落等前置流程尚缺，不代表正常 Run 或完整原版独立参考比较通过。
