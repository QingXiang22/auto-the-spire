# NBJ Write Clearly 安装与项目文档修订

日期：2026-10-07。归属：用户派发的负责人文档维护。当前维护状态和检查结果由主清单记录；本文保存本轮范围、方法与证据，不新增生产工作包。

## 结论与适用范围

`nbj-write-clearly` 适合本项目的技术文档。它将 Google 技术文档写作指南的常用原则整理为编写、修订和只读审阅流程，重点是读者目标、执行者与动作、前置条件、可扫描结构及事实保真。它是非官方实现，与项目已有 Diátaxis 类型约定互补。

本轮从 `F:/Something/nbj-write-clearly/.agents/skills/nbj-write-clearly/` 安装到用户级 `C:/Users/26877/.codex/skills/nbj-write-clearly/`。四个 skill 文件保持源文件字节一致，并附带原仓库的 `LICENSE` 和 `NOTICE.md`；可用 `$nbj-write-clearly` 调用。未修改原 skill 或安装其他插件。

共修订 31 份现行入口、指南和规范，另更新主清单维护记录及 records 索引。未对全部历史文档做风格重写，也未把风格检查当作规则或运行时验收。

## 修订范围与读者效果

本轮按以下主题处理现有文档。逐文件差异和机器核对结果保存在证据目录。

| 范围 | 页面 | 修订效果 |
| --- | --- | --- |
| 入口与写作约定 | 根 README、docs README/AGENTS、base README、AI README | 开篇说明用途和接手动作；活动工厂先于历史能力；后续作者有清晰写作检查项 |
| core 使用与维护 | 开发者指南、参数提交指南、规则与宿主解释、core spec、冻结基线 | 按读者目标导航；操作前说明条件；明确规则、宿主和 net 的责任；保持迁移未闭合边界 |
| DSL 作者文档 | 0.0.1 新/旧语法入口、core/0.1 参考、core/0.2 指南、计分配置指南 | 先区分版本和实际解析支持，再展示写法；说明查询范围及初始状态 |
| 项目与 ENV | 项目 spec、ENV 任务包、ENV 实现约束、管理分工、base 实施编排 | 按玩法主题拆分长段落；将交接内容改为顺序清楚的要求；保留范围和冻结条件 |
| net 契约 | net spec、公开协议、契约资产 | 用途先于元数据；在旧“尚无实现”表格前说明日期和实际状态查询入口 |
| AI 与共同流程 | AI spec/模型契约/训练设计、共同实施/运行/验收计划 | 区分历史设计与当前派单；将超时/断线处理整理为明确分支 |
| 远程任务操作 | DSH 操作指南 | 明确流程目标、派发前置条件和 `OWN-TASK-ID` 占位符 |

base README 的旧注册器说明完整移到同页文末，注明历史路径。项目 spec 的主题拆段保留原句。两篇首次教程已有明确步骤和可见结果，本轮保留正文。

## 原文与保真证据

本轮开始时，保存 194 份相关 Markdown 的原文和 SHA-256。快照使用 `.snapshot` 后缀，不进入文档检查语料。

证据目录为 `.audit/nbj-docs-2026-10-07/`：

- `before-manifest.json` 与 `before/`：原文及摘要。
- `changes.json`、`refinements.json`：定点修订依据；最终内容以 `diffs/` 的原文比较为准。
- `diffs/`：本轮逐文件差异，避免共享工作区的已有改动混入本轮判断。
- `verification.json`：原代码块、技术字面量和表格行保留检查；主清单任务定义/状态不变；122 份既有历史证据正文不变；6 个安装文件逐字节核对。
- `doc-check.log`、`diff-check.log`：最终文档检查输出。

机器保留检查用于发现意外删除，不能单独证明自然语言语义等价。本轮另核对了日期、设计与实现边界、迁移未闭合、公开职责目标及 base 历史说明。没有提高任何生产能力或覆盖结论。

## 验证与后续维护

本轮使用的检查入口如下，实际返回码和结果见主清单及上述日志：

```powershell
& 'F:/tool/Git/bin/bash.exe' 'F:/Something/nbj-write-clearly/scripts/check.sh'
python -X utf8 .audit/nbj-docs-2026-10-07/verify.py
python tools/doc-check/check_docs.py
git diff --check
```

纯文档变更未运行 solution 构建、Engine/Slice/net 规则检查、运行时内容加载、严格对拍或公开 Host/Python 游戏链路。未修改生产代码、DSL grammar、wire 载荷、ENV 任务状态或验收合同；未派发 agent、启动训练或提交 Git。

后续新增或修订文档，按 docs AGENTS 的 Diátaxis 类型要求与 NBJ 表达原则执行。原文已清楚时保留原文；涉及技术行为的改动仍按对应模块门槛验证。
