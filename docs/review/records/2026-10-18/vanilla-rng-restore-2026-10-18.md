# 纯 DSL 原版随机机制恢复交付

2026-10-18，Codex `/root` 按用户要求实施 **CORE-AUTHOR-05／DSL-RNG-RESTORE-01**。本包将原版随机算法、字符串种子与流布局接回纯 DSL，恢复初始／重洗和已迁移卡牌／金币奖励的真实抽样。当前状态见[主清单](../../tofix/README.md#当前交付)，准确职责、公式和未支持范围见[随机契约](../../../design/mod-the-spire/run-domain/randomness.md)。

## 接手入口与完成条件

分支 `codex/vanilla-rng-restore-20261018` 基于 `faceab9`／卡牌奖励 PR #17，依赖前置初态、完整铁甲卡牌和永久牌组；不可只拣随机规则忽略其调用方。接手先核对主清单和对应PR检查，再执行 `dotnet run --project tools/base-spire-dev -- check --rng-only`，预期10组独立源向量通过。完整包报告使用 `check --report`，定向命令不产生完整覆盖凭证。

公开Host继续 describe→catalog→start→step→observe；使用任意明确字符串seed，声明的profile应为 `vanilla-uniform@1`。Ironclad.Initialize→BeginDevelopmentCombat→BeginDevelopmentTurn 后，初始Shuffle恰好消费9次，起手由同一源种子决定。相同字符串的独立会话重放相同；不同字符串不共享状态，静态规则身份不包含每局seed。

本包完成条件为原版位生成／有界映射／NextFloat、源run/player/content种子公式、初始与源排序重洗、已迁移随机选择／奖励、无状态泄漏及源向量逐项一致。完整正常Run、药水掉落前置、地图／Neow、内容级遭遇上下文和整局参考双侧比较另行验收；父项与其他暂停项不关闭。下一机制包应恢复真实奖励前的药水掉落／来源，或按Run契约接入地图与楼层上下文，不能补抽固定次数制造随机对齐。

## 实现与版本迁移

- 复用已有xoshiro256**／SplitMix64和原版double截断整数映射，包manifest明确选择 `vanilla-uniform@1`。新增通用gameSeed／hash32／seedStream／sampleFloat32／binary32运算；所有状态、计数与执行仍归唯一Engine，不增加C#游戏分派。Host与开发工具共用包CreateSession工厂，返回已配置但未Start的会话。
- `run/random.spire`在启动时按原版UTF-16哈希声明12条run及3条player流。单人槽位0，内容种子过程显式接entry、slot、mixin并按uint32回绕；大写下划线源entry不能替换为kebab内容ID。种子文本保留bootstrap原文，大小写／空白不自动改写。
- 初始复制保持永久顺序后用UnstableShuffle，不发AfterShuffle；弃牌重洗按源ModelId.Entry和升级排序，再用同一Shuffle游标。新增显式非稳定sort选项匹配源List.Sort语义，原默认稳定排序行为保持。内容SourceEntry通过模板配置并在复制中保留。十张起始牌的具体来源实例顺序有向量，较大重复牌组和不同运行时排序未据此验收。
- 卡牌选择／生成／目标迁移至对应run流，Stampede与洗牌共用Shuffle。金币、稀有度、选牌和升级共用player Rewards，移除24-bit开发抽样。稀有度偏移以隐藏Int保存binary32位型，精确按源负阈值、逐次舍入、偏移增长／重置及升级零边界执行。
- 修正已有vanilla布尔映射为源 `NextInt(2)==0`；默认engine-default抽样保持。随机快照保留未消费的显式种子，重复声明和消费后重设拒绝；非法seed／空范围在消费前失败。
- 因vanilla布尔结果修正和新增IR能力，运行身份迁移至 **engine/ir@0.5／Core.TheSpire 0.5.0**，文本语言仍为core/0.3，base为 **0.5.0-dsl.30**。旧engine/ir@0.4包须重建，旧回放保留旧身份，不只改标签续接。

## 来源与独立验证

依据冻结sts2的MegaRandom、Rng、RunRngSet、PlayerRngSet、StringHelper、ListExtensions、CardModel.CompareTo、CardFactory、CardRarityOdds、PlayerOddsSet与IroncladCardPool。新的 `tools/rng-oracle/dsl_rng_vectors.py` 仅调用独立Python神谕和读取原版源，未引用Engine或生产DSL；冻结JSON保存98份源SHA256。原神谕哈希补齐UTF-16 surrogate处理，ASCII／BMP向量保持。

10组seed包含20261018、777、ENV-IRONCLAD-001、BASE-DSL-STDIO-00、ABC／abc、中文、非BMP字符、带空白文本和空文本。逐项核对15条流的种子与各4次有界抽样、8个精确NextFloat位型、初始／重洗实例排列与18次游标、slot2+mixin7内容种子、两轮金币／卡牌奖励身份／升级／偏移和10／20次共享Rewards游标。空文本仅为内部向量，公开入口继续拒绝空seed。

奖励向量限定当前实际迁移的金币／卡牌操作序列：原版RewardsSet先调用PotionRewardOdds且可能生成药水，该流程当前未迁移，不能将10／20次向量解释为正常战斗整份奖励游标。没有隐藏补偿抽样、换seed或修改参考。

## 实际检查

仓库根目录、Windows PowerShell、.NET SDK9.0.306、Debug。scope `base-spire-dsl-relic-draw-dev-v1`，算法 `mega-random/xoshiro256ss-splitmix64@1`，draw profile `vanilla-uniform@1`。包digest `7f62ae3e79b6fcfeb73ce221b441c63d140e8ccf089a8946baffccee6d5b6e55`，ruleset `934a30bac8f66b06`。

| 命令 | 实际结果与证据 |
| --- | --- |
| `dotnet build AutoTheSpire.sln` | rc0，0警告／0错误；`.audit/rng-build.log` |
| `dotnet run --project core-the-spire/Core.TheSpire.Engine.Tests` | 最终rc0，110项通过；`.audit/rng-core-final.log`，包括默认／原版profile、精确binary32、非法输入、快照和pure限制 |
| `dotnet run --project tools/base-spire-dev -- validate` | rc0，实际启动源布局与vanilla profile |
| `dotnet run --project tools/base-spire-dev -- check --rng-only` | rc0，10组独立源向量；`.audit/rng-source-check.log` |
| `dotnet run --project tools/base-spire-dev -- check --card-rewards-only` | rc0，9项奖励定向；`.audit/rng-rewards.log` |
| `dotnet run --project tools/base-spire-dev -- check --report .audit/rng-package-check` | rc0，318项包行为；`.audit/rng-package-check.log`及同目录package-behavior.json |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests` | 最终rc0，374 passed／0 failed；`.audit/rng-net-final.log`，包括原文本规则兼容、包工厂同seed重放和实际stdio |
| `python -X utf8 tools/ci/check_card_coverage.py .audit/rng-package-check/package-behavior.json --require-ironclad-complete` | rc0，87张可执行牌见证，源578，单人铁甲85/85，保留两项多人排除 |
| `python -X utf8 tools/ci/check_relic_coverage.py .audit/rng-package-check/package-behavior.json` | rc0，18件见证，源297，未实现278；不等于完整遗物验收 |
| `python -X utf8 tools/ci/check_dsl_migration.py --configuration Debug --output .audit/rng-migration` | rc0，8活动项目、153冻结源组、11原suite保留；原regression仍rc3、执行0，不冒充整局对拍 |
| `python -X utf8 tools/doc-check/check_docs.py` | rc0，扫描769份Markdown、0问题；`.audit/rng-doc-check.log` |
| `git diff --check` | rc0，无空白错误 |

开发中修复：Sort构建返回通用Query的类型接线；排序键中的升级Bool改成显式0／1文本；奖励旧整数网格断言迁移为独立binary32边界；随机流不再重复相加计数；原版布尔高位方向修正并升级运行身份。全部相关最终回归实际执行通过。文档与本地脚本的转义失败在写入前拒绝，未通过删分母／降低门槛取得绿色。

## 未验证与维护

整局独立模拟器／真实客户端对拍未执行；独立源向量与这些门槛分列。药水奖励、源地图／Neow／正常Run前置消费、Gaussian、全部权重／hook、后幕奖励与大型重复牌组排序仍未验收。RubyRaiders保留development-encounter流，源实际种子依赖楼层，不能用当前无楼层的开发入口声称恢复。其他角色、召唤／复活和训练暂停保持。

状态只回写主清单；接口写随机契约与core作者参考，历史证据留本记录，长日志留.audit。本次为接手自查，未进行独立agent审阅。
