# 铁甲战士完整单人卡牌与共享接口交付

日期：2026-10-18。Codex `/root` 按用户“先铁甲战士全套卡牌，以及相关需要的公共接口”实施 **CORE-AUTHOR-05／DSL-IRONCLAD-ALL-01**。本包补齐开发 scope 的 85 张单人铁甲战士定义及所需共享行为，保留完整来源与未验证边界。当前任务状态只维护在[主清单](../../tofix/README.md#当前交付)，机制要求见[规格](../../../design/mod-the-spire/ironclad-dsl/spec.md)，调用入口见[共享操作参考](../../../design/mod-the-spire/ironclad-dsl/shared-operations.md)。

## 接手入口与完成条件

分支 `codex/ironclad-full-cards-20261018` 从 `62f070c` 接续，依赖未合并的永久牌组／营火 PR #14 及其父分支，不能只从 main 拣本次叶定义后忽略前置。接手先核对主清单和本分支 PR 的 CI，再用 `check --ironclad-only` 定位具体牌族；形成交付证据时运行完整 `check --report` 和卡牌覆盖门槛。

本包完成条件是源池 87 项逐项裁定、85 张单人牌有实际普通／升级行为、生成牌和必要共享反应可用、公开选择与关键结果可见、现有消费者无回归；不是完整原版对拍或正常 Run 验收。后续若恢复正常角色开局，复用本包定义与永久牌组，再补源起始牌组／起始遗物、初始排序洗牌和自动抽牌，不另建 C# 玩法或缩小源池。验收由项目负责人／用户结合本次检查审阅；未进行独立 agent 审阅。

## 来源与交付内容

以 `sts2/src/Core/Models/CardPools/IroncladCardPool.cs` 的 87 项为准；DemonicShield／Tank 的 MultiplayerOnly 覆写使单人适用分母为 85，两个源身份仍在目录，未注册单人空效果。原全卡目录 578 项保留，已有 29 张铁甲战士之外新增 56 张，并实现 PrimalForce 所需 GiantRock。加已有 Shockwave，实际可执行卡定义合计 87。

逐项核对 87 个池成员及 GiantRock 的原始文件 SHA-256，与目录一致。冻结元数据位于 `tools/base-spire-dev/fixtures/ironclad-source-metadata.json`，保存来源 hash、普通／升级费用、类型、Strike 标签、X、原生耗尽和 Innate；行为检查独立消费该快照，不从运行定义反推预期。普通／升级效果和钩子依据相应 CardModel／PowerModel，费用及结算额外核对 CardEnergyCost、CardFactory、CardModel.OnPlayWrapper、CardCmd.AutoPlay、CardSelectCmd。

- 定义模板自动注册可执行 CardLibrary，源池成员与生成资格分开配置。全部解锁单人池保留 85 项，按源 CardFactory 排除 Basic、Ancient、Feed、NotYet 后为 78 个可生成候选；InfernalBlade 再过滤 Attack，Stoke 使用完整可生成池。未实现定义在创建前明确报 unsupported，不返回无效果实例。
- 补齐普通／升级效果和所需选择、手牌快照耗尽、随机目标、计算伤害、最大生命、临时 Strength、回合触发、费用修正及 Power 反应。新内容叶只组合模板，玩法全部在 `.spire`，没有 C#／Host 卡牌分派。
- 共用新建、复制、生成入手、完整耗尽、原生手牌／弃牌选择和升级；复制保留实际类型、标签、成长、升级及临时费用，永久源与本次支付／强制结果不传播。永久牌组与下一场重建继续沿用前包。
- X 在手动支付前捕获，普通免费效果不能覆盖 X；自动出牌捕获但不收费。InfernalBlade 免费在打出或回合结束时失效，Stomp 独立保留本回合折扣，升级不会解除免费修改。
- 每次重复有独立 play 记录，嵌套保存父上下文；AfterCardPlayed 在结果牌区移动之前，全部重复之后才移牌。Feed 致命后仍增加最大生命，耗尽与后置效果完成后才结算胜利；玩家已死时不能判胜。
- 新增显式 PlayTargetedCard，选择任一存活敌人；公开选敌／选牌沿用 net.answer_decision，不扩展 wire。CardPlays／CardLibrary 与内部引用不宣称为完整公开历史。
- CI 增加当前包摘要、578 分母、冻结 87 项源池、两项排除和全部可执行卡见证检查，要求铁甲战士 85/85。C# 仅作包外行为检查。

来源审计同时纠正两处旧实现：HowlFromBeyond 的 Exhaust hover tip 不是原生 Exhaust，它在耗尽堆中于回合末自动打出并按普通结果弃牌；BodySlam 的计算伤害仍应用 powered 修正。旧 CP54 按真实源改正断言，新增普通／升级回合末见证。Strength／Weak／Vulnerable／Cruelty／Colossus 的乘法链保留精度到最后取整，新增能区分逐段错误取整的判别。

## 实际本地检查

仓库根目录，Windows PowerShell，.NET SDK 9.0.306，Debug；受控行为 seed 20261018，随机重放另用 777；真实 stdio seed `BASE-DSL-STDIO-00`。包 `0.5.0-dsl.27`，scope `base-spire-dsl-relic-draw-dev-v1`，random_profile `engine-default`。最终 package digest 为 `b20e2542a15ac9d6601b7d4332a6d3fcba2655272a43df0af7984eca796620ab`，ruleset 为 `e37b32145bf5ac9e`。

| 命令 | 结果与证据 |
| --- | --- |
| `dotnet run --project tools/base-spire-dev -- validate` | rc0，实际加载并启动；共享规则可直接用现有 core/0.3 表达 |
| `dotnet run --project tools/base-spire-dev -- check --ironclad-only` | rc0，139 项定向检查；包含新增普通／升级、完整元数据、选择暂停、重复／嵌套、快照、致命余效、未知定义和跨战寿命 |
| `dotnet run --project tools/base-spire-dev -- check --report .audit/ironclad-check` | rc0，291 项完整包行为通过；87 张可执行牌均有实际见证，保留已有遗物、药水、敌人、遭遇、房间和奖励检查 |
| `python -X utf8 tools/ci/check_card_coverage.py .audit/ironclad-check/package-behavior.json --require-ironclad-complete` | rc0，87 个卡牌见证，源 578，单人铁甲战士 85/85，多人排除 2；independent_verified=false |
| `python -X utf8 tools/ci/check_relic_coverage.py .audit/ironclad-check/package-behavior.json` | rc0，17 个遗物见证，源 297，未实现 279；未改变遗物覆盖结论 |
| `dotnet run --project net-the-spire/Net.TheSpire.Tests -- --base-dsl-only` | rc0，24 项公开路径；新增真实 stdio 的第二个敌人选择、必须单选耗尽、X 支付与公开 PaidX、Rupture 延后触发 |
| `python -X utf8 tools/doc-check/check_docs.py` | rc0，762 份 Markdown，0 问题 |
| `git diff --check` | rc0，无空白错误 |

日志为本地 `.audit/ironclad-focused.log`、`.audit/ironclad-check.log`、`.audit/ironclad-net.log`；完整报告在 `.audit/ironclad-check/package-behavior.json`。不提交临时提取／生成脚本或报告。广覆盖 core／base／net／migration 交由本分支 Actions，结果以对应 PR 检查为准，本记录不预写 CI 成功。

实现中暴露的失败与修复：源元数据检查发现旧 HowlFromBeyond 关键词错误；全包检查发现旧 Weak 探针在开始战斗前施加、被新的跨战清理正确清掉，已把受控探针移到战斗开始之后，保留实际伤害断言；未知定义探针一度丢弃带返回值的过程结果，改为绑定后实际验证失败守卫；公开检查对多个卡实例使用 Single 及手牌动作歧义，改按 ContentId／候选实例选定，实际快照验证通过。暂存检查另发现两个新 DSL 文件的三处空白格式问题，已清理并重新生成最终包身份与完整报告；覆盖报告在包输入冻结后生成，没有手改摘要或通过场景。

## 未验证边界

独立原版双侧实际执行为 0，首差不适用；未恢复独立 DSL 适配，不能把源码审计、固定 seed 重放或本地行为预期当作原版对拍。正常角色初态、起手抽牌／Innate 排序、解锁进度、源 RNG／UnstableShuffle／run 流消耗、正常地图与 Run、无限生命敌人、多人与未迁移附魔／其他 Power 均未验证或未支持。资源上限继承开发 schema。

Power 通用目录仍保留 260 项且没有独立内容定义；本包交付的是铁甲战士所需的实际角色处理器和卡牌消费者，不宣称全 Power 目录完成。其他角色、敌人召唤／复活和 AI／训练暂停不变；不据本包关闭 CORE-AUTHOR-05、M01/M02 或 ENV/W 父项。文档／共享接口和检查足以定位本批交付与后续缺口，尚无独立读者接手验证。
