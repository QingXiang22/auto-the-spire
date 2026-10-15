# 项目交接 skill 交付 — 2026-10-07

本次创建 `auto-the-spire-handoff`，供作者整理交接、审阅接手路线及接续已授权任务。重点是让读者直接找到下一步、完成条件和证据。

归属：[主清单第 5 节的交接 skill 维护记录](../../tofix/README.md#5-本轮整理交接与检查)。负责会话为本次用户派单会话；修改范围为用户级 skill、docs AGENTS、该维护记录、综合交接及记录索引。本报告保存本日交付，当前状态以主清单为准。

## 如何使用

本次维护无待续工作。整理下一份实际交接时，在项目根目录调用：

```text
使用 $auto-the-spire-handoff 整理本次工作交接，让下一位读者能找到当前任务、第一步操作、完成条件和证据。
```

只审阅时可用“使用 `$auto-the-spire-handoff` 只读检查这份交接能否接手，指出缺失的信息和维护位置”。接续生产任务前仍从[文档总入口](../../../README.md)进入主清单核对当前认领与依赖；该 skill 不产生新派单。

整理的预期结果是一份不依赖历史聊天的交接：能找到目标/当前状态、可复用成果、第一步、完成条件与验收者、阻塞/未验证和维护位置。调用只读审阅时，预期结果为缺口及定位，不修改任务状态。

## 已交付

- 用户级安装目录：`C:\Users\26877\.codex\skills\auto-the-spire-handoff`。`SKILL.md` 提供项目入口、事实来源、交接写法和维护归属；`references/handoff-guide.md` 提供模板、六项接手检查及常见决策场景；`agents/openai.yaml` 提供显示名称与调用提示。
- [文档协作约定](../../../AGENTS.md#写成可以接手的交接)增加接手要求：先给可执行路线，分别说明成果/验证/未验证，只在主清单维护当前任务状态。
- 本报告用该 skill 组织交付说明和使用入口，作为小型文档维护示例。模板按任务规模合并栏目，不能为填表制造信息。

## 验证与维护

本次文档维护的完成条件为 skill 结构/元数据/本地引用有效，六个接手问题可定位，文档检查和差异检查退出 0；由本次负责人核对后登记维护结论。实际结果见[检查证据](../../../../.audit/project-handoff-skill-2026-10-07/validation.json)：

- 项目根目录执行 `python -X utf8 C:\Users\26877\.codex\skills\.system\skill-creator\scripts\quick_validate.py C:\Users\26877\.codex\skills\auto-the-spire-handoff`，退出 0；界面元数据、本地引用与自动发现配置核对通过。
- 项目根目录执行 `python -X utf8 tools/doc-check/check_docs.py`，退出 0，扫描 227 份 Markdown，0 问题；`git diff --check` 退出 0。
- [六项接手自查](../../../../.audit/project-handoff-skill-2026-10-07/reader-selfcheck.json)可定位到正文或直接链接；自查后补充了预期产出、完成条件与登记责任。主清单既有任务定义、状态、认领及其他维护记录核对保持原文。

本次文档交付无已知阻塞；接手效果采用作者自查，尚未进行独立读者验证。

本次没有修改游戏接口或生产代码；构建、Engine/Slice/net、内容加载及双侧/公开链路未执行，不作 ENV 验收声明。

后续修订 skill 修改其安装目录；交接表达原则更新 docs AGENTS。任务状态与认领只更新主清单对应项；带日期证据经负责人整理进入 records，长日志保存在 artifacts/.audit。本轮改动前快照在 `.audit/project-handoff-skill-2026-10-07/`。
