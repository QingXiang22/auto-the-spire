# 当前进度负责人复核 — 2026-10-13

当前已具备公开战斗、奖励、商店及一个真实事件的连续流程；正常四幕和五角色整局仍未完成。本次按用户要求只复核与汇报，生产源码只读，不派新任务、不恢复旧会话或训练。当前状态和排期以[主清单](../../tofix/README.md)为准。

## 核验到的成果

- 作者模型/规则库重构已有交付：engine/ir@0.4、core/0.3、base/0.0.2、类型化效果、共享与变体替换、数值和快照；完整发布验收仍见CORE-AUTHOR-04。
- Run公共操作、卡牌组合、商店及问号/事件接缝已接入。SunkenStatue的入水选项真实增加金币并造成脱战伤害，取剑仍受SwordOfStone依赖阻塞。普通事件叶复用ChoiceEventModule与RunResources，不将早期交接中已经修复的脱战死亡问题当当前缺口。
- 角色基础原语已有星星费用/X快照、锻造实例成长、五种充能球、Doom、Poison及Osty身份/召唤/成长/治疗/死亡保留/复活和基础代受伤。范围见[角色战斗契约](../../../design/mod-the-spire/character-combat/spec.md)与[阶段记录](../2026-10-12/five-character-primitives-2026-10-12.md)；完整hook、角色初始化及新机制独立双侧尚未完成。
- 内容快照共176张注册牌；五角色源池439张中143张注册（Ironclad87、Silent24、Regent9、Necrobinder14、Defect9），本次目录定位110张本地行为证据。数字来自[日期清单](../2026-10-12/five-character-card-catalog/README.md)，不折算全环境完成率，不替代577项完整源卡分母。

## 实际证据与本次验证

工作目录均为仓库根。审阅前检查git status；保留共享工作区全部改动。最新Osty构建日志记录solution返回0，Engine99、Slice839、net418全部通过。检查core/base/net/differential生产源码的修改时间，没有晚于该构建的.cs/.spire/.csproj；当前Host时间为2026-10-12 22:35:55。本次未重复构建或三套规则检查，旧日志通过与本次实测分列。

[既有严格结果](../../../../.audit/owner-five-character-primitives-20261012/regression/regression.json)是完整17项，16双侧passed、1工厂接线self_checked、0失败、rc0；时间早于最后的Poison/Osty续包。这套case没有新角色原语双侧，不把它当最后新增机制或正常Run/奖励/四幕的原版证据。

本次在当前默认构建上实际运行三条公开轨迹，全部从80HP/99金币正常开局，不注入资源或清场，不换seed、不缩池：

| 路径 | 命令要点（seed均为ENV-T03-SEED-L，scope为dev-map，max-actions为90） | 实际结果 |
| --- | --- | --- |
| 正式客户端 | `python -X utf8 -u net-the-spire/client/run_game.py --host-dll net-the-spire/Net.TheSpire.Host/bin/Debug/net9.0/Net.TheSpire.Host.dll --scope dev-map --seed ENV-T03-SEED-L --output .audit/owner-progress-review-20261013/public-run --max-actions 90 --use-potions` | 46次成功提交，经过战斗/奖励/商店离店；客户端只提供Unknown后停止，status=unsupported_map_room |
| 既有事件包装 | 将脚本换为`.artifacts/parallel/dsh-unknown-room-modules-20261012/drive_unknown.py`，output为`.audit/owner-progress-review-20261013/public-event`，追加`--shop-room auto --shop-policy buy --shop-buy-kind card --shop-remove` | 70次成功提交；46进入SunkenStatue，47金币73→179/HP67→60，48下一商店，之后完成第三场战斗/奖励；客户端在Elite节点停止 |
| 精英边界取证 | 同上一行，output为`.audit/owner-progress-review-20261013/public-elite-boundary`，追加`--step-unsupported-room` | 70次成功提交后，Host拒绝进入精英房：`Source encounter base-spire:byrdonis-elite cannot execute: missing enemy behavior base-spire:byrdonis`；status=fault_host_rejected |

三个运行工具报告的进程返回码均为1，业务summary.exit_code均为4；均为非零停止，run_complete=false，未观察到ActCompleted。前两种停止是客户端策略边界，第三种确认真实内容缺口。包装脚本只选择公开候选，不编辑游戏状态；它的SunkenStatue具名策略不等于正式客户端通用事件支持。

当前Host SHA256为`A296C2672305B8C96092D8273464B761D98930CFA93454D2A7BC2D6440483B75`，公开ruleset为`net:ruleset:18347a3ee9e37d37`，catalog为`net:catalog:5e415196b73a8b21`。原始帧、步骤、summary及包装运行日志见[本次取证目录](../../../../.audit/owner-progress-review-20261013/)；不同购买策略的轨迹不相互当作同一轨迹对拍。

通过dsh-remote-operator的status只读查询三个已登记专用会话：A问号包session-b4f2d691-688b-494a-83d2-ed06ad4b5ad9为running=false/asOfSeq1390，B严格重洗包session-0eef0705-b034-4a87-8bfb-70880010f1a7为false/495，C声明闭合包session-162ee024-d56e-4165-acf9-9f2350d6409d为false/750。固定交接存在；停止不替代验收。本次没有向DS发送消息或派单，不推断其他会话状态。

## 缺口与接续判断

1. ENV-T03：正式`run_game.py`的IMPLEMENTED_MAP_TYPES仍不含Unknown/Elite，也没有通用事件选项策略。先消费已交事件/房间能力，不误报Host没有Unknown。
2. ENV-T08/T09：上述固定正常路线已实证缺Byrdonis精英行为；其他未实现事件/遗物和Fogmog/Cubex等依赖仍在原范围。仅补正式流程需要的完整内容机制，不恢复批量补牌。
3. ENV-T10：`Rooms/MapRoomWiring.cs`的ContinueToNextAct仍显式Fail，未生成下一幕地图/队列。先前第一幕Boss证据不能抵四幕胜负。
4. ENV-T01/02/04/05：严格17绿态不能代替源权重完整范围、正常奖励/药水/长期写回的独立双侧闭环；M01父项保持开放。
5. ENV-T06/12：新角色原语为本地基础能力，完整Power/伤害/召唤hook、正式角色初始化、独立双侧和正常五角色流程继续待验证。AI继续暂缓。

主清单存在摘要滞后：CORE-AUTHOR-04的“当前严格17重洗未过”已被10月12日结果取代；T12“下一Doom/Osty未交”已被T06和后续记录取代；T06首段仍进行中却注明本轮无未完子项。它们影响接手判断，本次报告按实际后续证据解释，未借只读审阅重写原认领、关闭父项或启动新包。

本次负责人审阅完成，下一生产仍按主清单既有依赖与用户授权认领。本次没有执行新的Engine/Slice/net/严格17、完整第一幕/四幕、真实客户端、120对候选或性能测试。文档检查结果登记主清单维护记录；本报告移入records只保存复核证据，不表示这些父项验收。

## 文档校准追加

用户在本次只读复核后要求校准对齐文档。Codex仅修正文档：主清单严格17当前口径、角色基础包待验证/无在制子项、T12指向T06唯一认领、正式客户端与精英内容/幕转换接续边界；records索引与角色阶段快照标明证据时点，两份旧交接4处无效Markdown行号锚点改为文件或标题链接。上文发现摘要滞后的过程保留，不再代表校准后的入口。

原文快照和逐文件差异在[校准证据目录](../../../../.audit/doc-alignment-20261013/)；原日志/帧/数值与验收范围未改，未改生产、派单、重跑游戏或关闭父项。本次维护完成：`python -X utf8 tools/doc-check/check_docs.py`扫描465份、0问题、rc0，`git diff --check`返回0。首次链接检查误把原文Markdown备份也纳入扫描，原失败日志保留；备份改为`.md.txt`纯文本证据后复查通过，未修改检查器或排除有效文档。
