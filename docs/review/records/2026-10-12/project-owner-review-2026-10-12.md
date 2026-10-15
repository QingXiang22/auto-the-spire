# 项目共同构建与DSH交付复核 — 2026-10-12

商店正式生产、语义重放和客户端能力已交；负责人在同一当前源码构建上完成规则门槛与正常公开购买/删除/离店。完整整局仍未验收。任务/排期/认领只看[主清单](../../tofix/README.md)。

## 交付核验

- A1944：首轮曾INVALID_REQUEST，修正轮turn/end completed；固定交接与源码/构建日志已核验。无色清单64项顺序/每张源稀有度逐项比较，39 uncommon/25 rare、0差异；售价折扣不额外抽样，当前支持范围商店窄包验收。原[商店交接](dsh-shop-handoff-2026-10-11-wave.md)里的“net未接线”是执行者当时未实测判断，当前自动派生公开声明与真实Host已证明能用，不再派手工映射包。
- B755 completed：语义选择按实时候选匹配、句柄只作证据，原完整第一幕门槛保留。负责人从当前Host购买轨迹提取45步前缀，新的会话重放rc0、公开事实一致且句柄不同；fixture明确prefix、ActCompleted/run_complete均false。旧固定字面ID不再作为控制输入。原[语义冒烟交接](dsh-semantic-smoke-handoff-2026-10-11-wave2.md)。
- C894 completed：客户端能力与当前真实Host两条路径已核验，policy21/decision11/map7检查通过；真实商店请求帧已有，schema/适用不变量由新C包收口。原[公开商店交接](dsh-shop-public-driver-handoff-2026-10-11-wave2.md)。未把旧固定Host未含商店等同于当前生产缺失。

## 负责人独立验证

从仓库根执行，固定共同输出`.artifacts/parallel/owner-project-review-20261012`；[日志和原始证据](../../../../.audit/owner-project-review-20261012/)：

| 验证 | 实际结果 | 范围 |
| --- | --- | --- |
| solution build（--artifacts-path上述绝对根） | rc0，0错误/5警告 | 当前源码；含新增CardDefinitions.Schema导致的CS9124与既有nullable警告，不以构建代规则验证 |
| Engine DLL / Slice DLL / net DLL串行执行 | rc0；99/770/416全部通过 | 同一构建，不用旧固定Base替代商店 |
| Differential.dll test --suite regression | rc1；原17全执行，15双侧passed/1 self_checked/1重洗未过、自检0失败 | 原Profile/分母保留；不关闭完整T04/M01 |
| ColorlessShopPool与sts2源清单/构造逐项核对 | rc0；64/64、顺序与39/25元数据一致 | 当前源，非仅实现自测 |
| 正常公开Run购买（seed ENV-T03-SEED-L、dev-map、max90、shop-policy buy/card） | 45成功步；买卡金币111→63，货架售出，长期副本增加，离店phase7→2并继续下一战斗/奖励 | 未注入血量/金币/相位，14格公开货架实际发布；63不足75删牌价，动作不提供、正常离店 |
| 同seed正常公开Run删除（shop-policy leave、shop-remove strike、max80） | 46成功步；正式decision答选中实例，金币111→36、删除次数0→1、长期牌减1，离店并继续下一战斗/奖励 | 不靠买牌后注入金币制造删牌；两条路径各自自然资源 |
| 购买轨迹记录→当前Host新会话语义重放 | 两命令rc0，45步prefix、公开事实一致，记录/当前候选句柄不同 | 只验前缀重放，不声明整幕/整局/独立原版比较 |

两个公开运行器最后均rc4：下一地图只能选Unknown，明确unsupported_map_room，run_complete=false；不是本次商店故障已发生，也不伪装Victory。两次运行策略不同，后续HP/牌组不同属正常删除效果，不相互当同轨迹对拍。完整原版/五角色范围及当前Eternal、遗物效果边界仍开放。

## 接续与未验证

新A仅修StableShuffle排序（原工具游标已经修，不重做初始化）；新B补参考逐边界真实streams，不更改Profile伪装通过；新C复用本次帧补公开契约与最小必要反例。当前源码共同闭包冻结供B/C，不与A共享obj/bin。A之后按真实房间/跨幕依赖推进，卡牌/召唤复活/AI仍暂缓。

未运行完整第一幕/四幕胜负、真实客户端、120对和性能候选；Unknown/Fogmog/Cubex等缺口不靠过滤池或换弱case消除。三份交接移records仅记录已并入开放范围，不关闭父项。当前认领/派发只看主清单与task-record，accepted不算交付。

文档检查扫描424篇，修复本次移动交接的audit反向链接后，仅两份既有交接4处`#L`锚点失败（rc4）；本次新建/移动链接未报错。diff-check rc0。新A/B/C均已accepted，实际session及固定交接只查主清单派发记录。
