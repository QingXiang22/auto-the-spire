# 手动试玩 base-spire 的当前边界

文档类型：操作指南（How-to）。终端编号菜单尚未接入当前纯 DSL Host；本页帮助你判断入口是否可用，以及选择已有的公开接口示例。它不提供完整游戏可玩的承诺。

## 确认菜单限制

仓库的 `tools/base-spire-dev/play_game.py` 仍以 `--scenario formal-dev` 启动 Host，并只接受旧 `dev-map`／`dev` scope。当前 Host 仅支持 `text-rules`，明确拒绝这个旧场景；重新构建或更换 DLL 路径不能修复协议与场景不兼容。旧说明中的本机路径和历史已验证构建不适用于当前检出。

不要依赖这份旧菜单判断地图、房间、敌人或整局的支持范围。当前有限正常路径与验证范围查 [CI 说明](../../../../tools/ci/README.md)；完整验收要求查[环境规格](../../../design/environment-completion-and-differential-v1.md)。

## 使用现有公开接口示例

按[公开运行指南](run-public-game.md)构建 Host，并运行真实获得遗物、抽牌和重洗的示例。它通过当前 manifest、公开观察和合法候选执行规则；这是受控机制示例，不是交互菜单或完整游戏。

需要接入自己的界面时，查[客户端参考](../reference/public-client.md)和[协议](../reference/protocol.md)。菜单恢复也应使用相同入口：每次读取最新公开候选、提交所选候选，重新观察；不能沿用旧菜单编号或在客户端补算玩法。

终端菜单的恢复任务、认领与状态从[任务入口](../../../roadmap.md)关联的 Issue 查询，本页不维护第二份进度记录。
