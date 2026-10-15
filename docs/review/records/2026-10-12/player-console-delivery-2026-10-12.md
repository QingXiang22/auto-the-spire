# 玩家终端试玩入口交付 — 2026-10-12

用户要求查看进度并亲自试玩。Codex `/root`只新增薄终端客户端与使用说明，不修改游戏、协议或三项DSH成果；当前状态见[主清单T03](../../tofix/README.md#env-t03)。

入口：[play_game.py](../../../../tools/base-spire-dev/play_game.py)，[操作指南](../../../design/net-the-spire/how-to-play-base-game.md)。数字选择当前公开候选；d编号查看公开字段，p查看公开牌区，r刷新，q退出。状态/合法性/选择/随机数/结算由原Host和Engine提供，抽牌堆只显示公开数量。默认使用上次共同99/770/416验证Host；新A/B/C交付未因此升级为验收。

负责人以实际Host驱动菜单：数字选地图、结束回合、Bash出牌、选择目标，敌人55→47并显示vulnerable×2；查看/刷新/无效编号不推进观察。另复用当前构建语义前缀驱动数字菜单，购买、离店、后续奖励正常；第46次选择未知房间返回backend_fault并明确停止，未写整局通过。操作输入在本次验证中由脚本替代键盘，Host与规则实际执行；没有脚本化假后端或注入游戏状态。

[实际日志与验证摘要](../../../../.audit/player-console-20261012/)包含manual-battle.log、manual-controls.log、manual-shop-boundary.log和verification.json。只验证本工具及固定Host交互，未重跑Engine/Slice/net全套，也没有验证新三项DSH生产行为。纯文档检查扫描433篇，仍有其它两份既有交接4处#L锚点问题，本次新增链接无报错；diff-check rc0。

本次工具维护完成。新A1273/B935/C764均已交付ready_for_review，主清单回写待验证；C报告的Host supported_structures声明ID不一致另待负责人裁决。本次未派发新会话、未继续其生产或开放完整原版身份。
