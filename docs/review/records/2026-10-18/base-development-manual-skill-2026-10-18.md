# base 开发手册与 agent skill 交付

日期：2026-10-18。关联 CORE-AUTHOR-05 的文档维护，当前状态只维护在[主清单](../../tofix/README.md#本次文档整理)。用户要求先写覆盖全部 base 内容的人类教程，再据此生成 Codex／DSH 使用的 skill，以减少源码阅读负担。交付范围与 BD01～07 验收见[文档约定](../../../design/mod-the-spire/base-development-docs.md)。本记录是负责人试跑与自查，不是独立读者／独立 agent 评审。

## 交付与来源

基线 main/807d17d，分支 codex/base-spire-authoring-guide-20261018。遵循项目 Diataxis 分类与 NBJ 清晰写作约定，结合 tgdp-dev-docs、workflow-b 和 skill-creator 的文档／技能创建流程。未修改正式 base、Engine、net 或独立参考的玩法。

[人类手册](../../../development/base-spire/README.md)包含 13 页：两篇分阶段教程、七篇操作指南、两篇准确参考与一篇解释，加手册导航。范围覆盖卡牌／变体／附着、战斗资源／伤害／回合、Power／遗物／药水、敌人／意图／遭遇、角色／进阶、地图／房间／事件／商店／营火／宝箱／奖励／Ancient／跨幕／永久状态／终局，以及随机、公开投影和验证。尚未迁移领域给出前置依赖、状态／顺序和验收方法，不编造可调用接口或完整游戏可玩性。

两篇教程先写并原样走通，再据其与操作指南生成[项目 skill](../../../../.agents/skills/base-spire-development/SKILL.md)。skill 只保留必要约束、按任务路由、定位命令、验证边界与 DSH 显式入口；不复制整本手册、不保存另一套状态表／支持数字。定位脚本只查静态源目录，需核对源时才对选中文件计算 hash。

教程 DSL 和[真实 stdio 驱动](../../../examples/base-dsl/run_relic_lesson.py)放在包外的 docs/examples。驱动复用现有 Python 客户端，不实现游戏效果；正式 base 仍只有原有文本文件，TheAbacus 正式数值保持 6。练习副本与长日志分别保留在 .audit/base-spire-authoring-lesson 与 .artifacts/base-spire-docs，不提交。

## 实际验证

环境：Windows／PowerShell，.NET SDK 9.0.306，Python 3.12。教程范围 base-spire-dsl-relic-draw-dev-v1，协议 0.2，固定 seed BASE-DSL-LESSON-00。先构建 tools/base-spire-dev，0 警告／0 错误，再从两篇 Markdown 提取 9 个 PowerShell 代码块，按文章顺序在同一会话执行，包含创建学习目录、复制初始包、配置编辑和失败后恢复。

| 教程阶段／检查 | 实际结果 | 证据 |
| --- | --- | --- |
| 原配置、获得 TheAbacus 后实际抽牌 | [6]，成功 | lesson-original.json |
| 仅将模板参数 6 改为 8 | [8]，规则身份改变 | lesson-configured.json |
| 参数 1000 超出模板边界 | validate 退出 2，真实文件／参数位置报错；随后恢复 8 | tutorial-walk.log |
| 新共享 AfterCardDrawn 模板＋PracticeBeads 配置 | [2,4,6,6]，第四次空堆不触发 | lesson-shared-rule.json |
| 不持有遗物 | [0,0,0,0] | tutorial-walk.log |
| 同时获得练习 8 点 TheAbacus 与教学 2 点遗物 | [10,12,14] | lesson-combined.json |
| 驱动错误期望／未知动作／缺 Host | 分别退出 1／2／2，不返回伪成功 | lesson-wrong-expectation.json 与负例日志 |
| skill 标准 quick_validate | 通过，元信息／命名／占位检查有效 | 本地命令输出 |
| 定位助手 9 项演练 | 全部符合期望，见下文 | skill-checks.json |

驱动初稿曾使用未加 net: 前缀的动作 ID，未找到候选；已改为实际公开 action／field ID 后重新原样执行教程。未跳过失败，也未改正式规则使教学预期成立。

定位助手覆盖精确遗物 ID、未迁移 Power、宽泛搜索截断、未知 ID、空查询、过大 limit、源 hash 不符、缺源和逃出仓库路径。成功定位仅代表静态条目存在；源不符／缺失为 3、无匹配为 1、输入／文件错误为 2。TheAbacus 单源原始字节 SHA256 与 catalog 匹配。另显式检查 skill Markdown 的所有相对链接；仓库文档检查默认跳过 .agents，未把跳过视为已验证。

## 文档分类与路由自查

| 页面组 | 声明／实际类别 | 读者任务及复核结果 |
| --- | --- | --- |
| tutorials 两页 | Tutorial／Tutorial | 固定路径、隔离初态、分阶段编辑与可见结果，已逐步执行 |
| how-to 七页 | How-to／How-to | 以目标、依赖、操作和判断点组织；未迁移领域说明开发方法，未伪造运行教程 |
| reference 两页 | Reference／Reference | 当前声明、参数、顺序与局限可按表查；不把旧 C# 方法当作当前 API |
| explanation 一页 | Explanation／Explanation | 解释完整 DSL、受限配置、状态归属、显式组合与取证关系 |
| 手册 README | Reference 导航／Reference 导航 | 按读者任务分流，不嵌入长篇机制或另一套状态表 |

分类走查完成，未发现需要再拆分的类型混杂。原过渡开发指南的操作与接口说明曾重复，已改为导航，新手进入两篇教程，接口只在参考页维护。规则未实现的领域不适合作为已可运行教程，已分别写为操作指南；这一区分是功能可用性要求，不是缩小完整开发范围。

代表任务自查：新增洗牌格挡配置进入内容指南→操作参考→一个模板；迁移 StrengthPower 进入 Power 指南并保留 definition=null；宝箱领取药水进入 Run／物品生命周期依赖，不调用旧 C#；源 hash 不符先定位版本而非重写 catalog。没有运行远程 DSH、没有验证其自动发现／加载，也没有派发额外 agent。DSH 使用显式读取项目 SKILL.md 的方式，遵守原任务读写和授权边界。

最终文档检查扫描 720 份 Markdown，0 问题；skill 标准校验、UI 元信息和显式相对链接检查通过，工作树／索引 diff --check 均为 0。单独确认正式 core／base／net 没有修改。未运行不受影响的完整 Engine／base／net／独立对拍、AI 训练或性能检查；本次文档与教学内容不恢复未迁移玩法，也不关闭原 ENV/W 合同。
