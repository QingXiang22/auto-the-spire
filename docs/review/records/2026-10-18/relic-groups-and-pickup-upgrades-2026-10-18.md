# 遗物分组与拾取随机永久升级 — 2026-10-18

本批将后续遗物按共享机制排期，并实际交付Whetstone、WarPaint、SandCastle，使拾取随机升级能保留原版选取顺序、Niche消费与永久实例身份。

归属：**CORE-AUTHOR-05／DSL-RELIC-CONTENT-04／RELIC-G01**；负责Codex `/root`。基线`7ff95ca`／现有[PR #23](https://github.com/QingXiang22/auto-the-spire/pull/23)，沿`codex/relic-chemical-x-20261018`接续；前批ChemicalX的四组CI已通过，尚未合并。本文记录本次交付，当前状态只看[主清单](../../tofix/README.md#当前交付)及其[分组表](../../tofix/README.md#遗物分组任务)。

## 接手路线

1. 核对主清单认领与工作区；本批三件遗物和随机永久升级操作已经实现，不重做。分组与重排方法见[开发计划](../../../design/mod-the-spire/relic-combat-start/development-plan.md)，准确行为见[生命周期契约](../../../design/mod-the-spire/relic-combat-start/content-lifecycle.md#拾取随机永久升级dsl-relic-content-04relic-g01)。
2. 仓库根目录运行`dotnet run --project tools/base-spire-dev -- check --relic-content-only`，应包含UP组资格、源向量、嵌套、失效、两战与非法kind判别并返回0。覆盖仍用完整check报告，定向通过不代替独立原版整局。
3. 后续先认领RELIC-G19的完整子包：逐件核对FakeMango／LoomingFruit／NutritiousOyster／SignetRing／GoldenPearl完整源方法，确认仅参数差异后复用已有拾取资源模板，不预先假设全部同义。再按计划接G04→G05→G06；Egg／选牌升级另沿各自依赖交付。

## 规划结果与依据

用户要求按相似实现成组，并自行规划提高可靠性。本次采用workflow-b，把源触发、修改对象、状态寿命、随机／决策与真实消费者作为分组依据。对基线257件未迁移且未排除的源提取public override方法签名及领域命令，全部原始字节SHA256与catalog一致；原始扫描在`.artifacts/relic-group-plan/source-hook-inventory.json`。

主清单增加20组及依赖、优先级和可验成果；候选名字为例子，并不意味着每件完整源语义已审阅或已经支持。无法归类、只有属性覆写或跨域复杂条目进入G20复核，不删除来源、不默认放进角色暂停组。源297项与Deprecated排除保留。

本次规划自查发现并解决三处风险：随机升级与玩家选牌分开；Egg组保留奖励展示／商店创建／入组前修正；随机Take前仍完整消费StableShuffle。详见计划自查段。未派出独立agent，不声称独立规划审阅；没有需用户新决定的业务规则。

## 实现与接口

- cards增加`UpgradeRandomPermanentCards(owner,kind,amount)`，只接受attack／skill／all，amount为1..999；过滤本拥有者可升级RunDeck实例，复用SourceCardSortKey、Niche及UpgradeCard。排序／洗牌不移动永久牌，不发AfterShuffle。
- 每调用建立独立临时选择与候选实体，按候选序号表达源Fisher-Yates；完整洗牌后按新序调用前N张UpgradeCard，随后清理。选取状态不混入Relic／Player字段；升级事件可以嵌套另一获得，父子选择不共享临时集合。当前实现用O(n)临时实体和O(n²)查询避免引入全局共享洗牌集合，无新增性能承诺。
- 遗物模板`PickupRandomUpgradeRelic`以有效实例监听RelicObtained，仅本次relic响应。三份具体内容只配置definition／kind／amount；Whetstone为Attack×2、WarPaint为Skill×2、SandCastle为All×6。移除不撤销升级，战斗不重放拾取。
- base版本`0.6.0-dsl.35`，继承engine/ir@0.6、vanilla-uniform@1及`base-spire-dsl-relic-draw-dev-v1`。修改base的cards／relics／元数据、包外判别／源向量／公开stdio及文档；未改core、Host玩法、独立参考或源池成员。

## 来源、实际验证与证据

本地来源为`local-sts2-source-2026-10-18`，三类AfterObtained、ListExtensions.StableShuffle／UnstableShuffle及CardModel.CompareTo为行为依据；所用卡牌和随机源完整hash冻结在`tools/base-spire-dev/fixtures/pickup-upgrade-vectors.json`。神谕`tools/rng-oracle/pickup_upgrade_vectors.py`只使用独立原版RNG重写，不导入Engine／base；重新生成与冻结文件逐字节一致。

固定源向量seed为`20261018`、`777`、`BASE-DSL-STDIO-00`，连续执行Whetstone两次→WarPaint→SandCastle；比较每步选中实例、升级事件顺序和Niche游标。候选少于16，等键排序只据此验证当前.NET小范围情形，不扩展为大型等键牌组证明。公开stdio固定`BASE-DSL-STDIO-00`，定向包场景默认数值seed`20261018`。环境为Windows／PowerShell、根global.json的.NET SDK 9.0.306与Python 3.12。

| 实际命令（仓库根目录） | 实际结果 | 原始证据与边界 |
| --- | --- | --- |
| `dotnet run --project tools/base-spire-dev -- validate` | 0 | 真实包加载／启动，不代表行为覆盖 |
| `dotnet run --project tools/base-spire-dev -- check --relic-content-only` | 0；53项 | `.artifacts/relic-group-plan/focused.log`；含本批10项机制判别 |
| 神谕生成到`.artifacts/relic-group-plan/rebuilt-vectors.json`并与冻结文件逐字节比较 | 0；一致 | 再生成不读被测DSL；局部独立源向量不是双方正常Run |

| `dotnet run --no-build --project tools/base-spire-dev -- check --report .artifacts/relic-group-plan/behavior` | 0；371项 | `package-check.log`与`behavior/package-behavior.json`，实际执行全部当前包机制 |
| `python -X utf8 tools/ci/check_relic_coverage.py .artifacts/relic-group-plan/behavior/package-behavior.json` | 0；42件见证／297源／254未实现／1源排除 | 校验当前包摘要；`independent_verified=false` |
| `python -X utf8 tools/ci/check_card_coverage.py .artifacts/relic-group-plan/behavior/package-behavior.json --require-ironclad-complete` | 0；85/85铁甲单人，2项多人排除 | 全卡578来源保留，87已声明定义有实际行为 |
| `dotnet build AutoTheSpire.sln --nologo` | 0；0警告／0错误 | `build.log` |

包摘要为`26c838965171f8c90fb022b359f499f396f19cda055de03af5d913ea91e2c433`，ruleset为`b55f2fbf8f4eb351`，见`.artifacts/relic-group-plan/describe.json`。公开路径命令`dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only`返回0，36项通过／0失败，证据为`net-check.log`。三件遗物逐件经真实stdio建立永久牌组、获得并使用战斗副本，得到Strike的9／6伤害和Defend的8／5格挡；内部选择状态不进入公开载荷。文档检查扫描783份Markdown、0问题，`git diff --check`返回0；完整Engine／net／迁移回归交Actions，正常Run与独立原版双侧未运行。

## 修正、未验证与接手自查

开发中纠正两处夹具预期：开发手动结束战斗不重置存活敌人HP，因此跨战断言比较实际前后9点伤害；非法kind的最小反例应捕获EngineFaultException，再检查实体与随机无变化。没有放宽生产守卫或吞掉失败结果。

本批只恢复显式获得后的效果，正常Common／Ancient来源资格、完整Run／Neow／地图、独立参考双侧与大型等键牌组尚未验收。随机向量不补抽、不换seed；余下源条目保持未实现，角色／召唤／复活及训练暂停保持。父项关闭条件仍见[迁移规格](../../../design/mod-the-spire/engine-dsl-migration/spec.md)及主清单ENV/W，由负责人按各层证据验收。

本记录与直接链接可定位当前目标／状态、已交接口、第一步、完成条件／验收者、未验证与维护位置。任务状态唯一回写主清单，设计方法回写开发计划，当前语义回写生命周期／操作参考，长日志留本地artifacts；此为接手自查，非独立读者验证。
