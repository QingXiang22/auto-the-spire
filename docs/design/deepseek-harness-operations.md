# 用远程DeepSeek Harness派发一个小任务

文档类型：操作指南（How-to）。

本指南说明如何派发一个小任务，并在用户要求时查询、取回和复核结果。开始前，先明确输入、读写边界和验收条件。用户已授权在当前工作的授权范围内自主委派，无需手工在网页发消息；项目任务认领、核心冻结和共享目录约束仍适用。

## 准备连接

服务入口是 `https://124.221.182.144`。登录凭据使用环境变量`DSH-USERNAME`和`DSH-PASSWORD`；当前Windows用户级变量已经配置。脚本可读取未被当前进程继承的用户级变量，不需要复制密码到配置文件、命令行或任务文本。

用户级skill安装在`C:/Users/26877/.codex/skills/dsh-remote-operator/`，可用`$dsh-remote-operator`调用。其`scripts/dsh_client.py`使用Python标准库，保存请求记录但不保存密码或Cookie。设置脚本位置并检查连接：

```powershell
$dshScript = Join-Path $env:USERPROFILE '.codex/skills/dsh-remote-operator/scripts/dsh_client.py'
python -X utf8 $dshScript probe
python -X utf8 $dshScript sessions
python -X utf8 $dshScript models
```

probe应返回`authenticated: true`；sessions只列工作目录和运行元数据，models列实际配置。请求失败退出非零。脚本使用正常TLS校验，不关闭验证来发送账密。

2026-10-07连接验证已成功，模型实际回复`DSH_REMOTE_SKILL_OK`，没有执行工具或修改项目文件。该站点的`/dsh-remote/status`与`/api/dsh-remote/status`当时都返回404；Harness模型委派可用，插件SSH工作区接口尚未验证可用。这是部署实测，不是插件安装完成声明。

## 准备任务文本和工作目录

在[当前清单](../review/tofix/README.md)对应维护记录或任务行登记本包。确认当前已有交付和文件主责，准备UTF-8提示词文件，包含：

- 任务ID、具体目标、只读审阅还是允许修改。
- 必要上下文、输入源码/规范和对应版本。
- 允许读写的文件、不得触及的范围。
- 可区分的验收条件和返回格式，例如源码位置、补丁、实际命令结果及未验证项。

默认让DS直接读取项目工作区；提示词给出绝对路径、规范入口、任务边界与交付要求，无需预先打包当前源码。用户2026-10-07已确认DS可自行读取本项目工作区，可直接使用`F:\Scylier-Project\active\auto-the-spire`作为会话cwd，由DS开始任务时核对目录、git状态及输入版本。同一个目录若由本地与远端共同访问，应按共享工作区处理，禁止重复实现或同时编辑同一文件。

其他项目使用用户指定或已核对的Harness可访问目录。实测默认目录`C:\Users\26877\Documents\deepseek-harness\default-workspace`仅适合无项目依赖的任务，不用于本项目源码审阅。只有DS实际报告路径不可访问时，再处理工作区连接或按需提供源码片段。

## 派发、查询和取回结果

先将提示词写入 `.audit/dsh-task/task.txt`，确认记录文件尚不存在。把下面的 `OWN-TASK-ID` 替换为本包 ID，再运行派发命令：

```powershell
$dshRemoteCwd = 'F:\Scylier-Project\active\auto-the-spire'
python -X utf8 $dshScript dispatch --task-id OWN-TASK-ID --prompt-file .audit/dsh-task/task.txt --record .audit/dsh-task/task-record.json --cwd $dshRemoteCwd
```

脚本新建专用会话，默认沿用Harness模型配置。用户指定模型时加成对的`--provider`和`--model`，值从models取得，不修改服务器的全局默认。不要把任务插入他人已有会话。

2026-10-08 用户授权本项目每半小时自动检查 DS 工作、复核交接并继续派单，取代本项目此前“只派发、等待用户手动唤起”的默认流程。其他项目仍按各自授权执行。记录中的 `accepted` 只表示派发成功，不能记为完成或验收。自动复核按下节执行。

用户要求查询或复核时，使用原记录：

```powershell
python -X utf8 $dshScript status --record .audit/dsh-task/task-record.json
python -X utf8 $dshScript result --record .audit/dsh-task/task-record.json --output .audit/dsh-task/task-result.json
```

实际完成须核对返回结果与结束原因、受阻/错误及验收证据，不能单靠running=false判断。取回的JSON包含page.records，`assistant/message`内有模型正文；`turn/end`说明该轮结束原因。按实际输出审阅方案或补丁、检查证据与未验证事项，回写对应项目任务状态，不能以远端自报关闭父项。

page.hasMore为true表示历史未取全。保持本次through_seq，用最早record的event.seq作为before-seq继续读取，例如：

```powershell
python -X utf8 $dshScript result --record .audit/dsh-task/task-record.json --through-seq 120 --before-seq 80 --max-messages 100 --output .audit/dsh-task/task-result-earlier.json
```

120/80只是示例，必须换成实际序号。结果默认40条、最多200条消息，不能把窗口片段当完整交付。

## 半小时复核、交接与接续

本节是用户 2026-10-08 授权的项目推进方式。Codex 承担负责人复核、任务安排与文档维护；DS 承担明确边界的生产包。任务目标、排期、认领和状态仍只在主清单维护，不建立第二份任务状态表。沿 M01→完整 Ironclad→五角色/进阶→候选验收推进；core 冻结、已明确暂缓事项及 AI 环境门槛继续有效。

### DS 的每包交接

每次派单指定唯一交接文件 `docs/review/tofix/dsh-<任务ID>-handoff-<日期>-<会话短ID>.md`，实际路径写进本包 `task-record.json` 的 `handoff_path`。DS 在结束前写交接；运行中可以更新同一文件，但不能把未完工作标为可复核。文件头使用下列字段，正文沿项目交接 skill 模板；这些字段是日期交付声明，当前工程状态仍以主清单为准：

```yaml
---
task_id: ENV-T04
dispatch_id: 本包派单记录中的task_id
session_id: 本包派单记录中的session_id
delivery_status: in_progress # 结束时为 ready_for_review 或 blocked
updated_at: 带时区的实际时间
dispatch_record: 本包task-record.json的仓库相对路径
---
```

正文必须包含已交付能力、修改文件和语义、输入/构建/源/reference/config/seed/run_id（适用时）、逐项完成条件与证据、实际命令/返回码、首个差异、未执行和阻塞、接手第一步。长日志保存在本包 artifacts/.audit。`ready_for_review` 表示执行者请求核验，不代表负责人验收；真实依赖缺失用 `blocked` 并说明依赖 ID、最小复现、受阻 case 和可继续事项。不能只写“完成”。

### Codex 的每次复核

1. 查看 git 状态，从总入口进入主清单，读取本节及对应工作包。只处理主清单明确登记的项目执行会话；按派单记录查询一次实际状态，不向其他会话插单。
2. 会话仍运行时不重派、不编辑其生产文件、不并行写共享 obj/bin。没有变化时保持安静；可在文件边界和构建顺序已协调时推进其他已就绪线。
3. 会话停止后，读取本包交接与结果；分页未取全时继续取齐。核对 task/dispatch/session 身份及实际文件、日志和输入版本。没有交接或只有 accepted/running=false 时，不能判完成；先查真实结束原因，再给剩余目标重新派一个有边界的接续包。
4. 对 `ready_for_review` 按原完成条件核对实际改动和证据，补跑受影响且缺少的必要检查；规则变化按根 AGENTS，构建串行。已有证据仍匹配最终输入时不重复全量运行。严格 suite 因必需 unsupported 返回非零时分别结算入口能力和未支持比较，不能伪装全通过或删分母。
5. 核验不满足时派修复包，保留旧记录与原证据；满足时更新主清单本条，按项目规则归并交接及修复引用。双侧/公开证据不足保持待验证，只阻塞实际受影响比较；父项及身份仍须原合同成立。
6. 从当前主清单按优先级/依赖安排下一完整小包，一个目录责任线最多一个进行中的包。A 的 T01/T02 已交付部分先复核再接 T05；B 接续 T04，C 接续 T03；后续按阶段推进，不停留在重复文档审计或无限清理长尾。跨模块修复交文件主责。凭据只从环境读取，不进入提示词/日志；不自行提交、推送、reset、清理工作区或启动训练。

每次已处理交接的会话 ID、结果序号和交接摘要可记到 `.audit/dsh-supervisor/` 作为去重证据，不复制任务排期表。派单响应不明先查原 request/session，不重复发送。通知只在交付复核、派出下一包、失败/阻塞变化或需要用户操作时发送；无变化不发半小时状态消息。

## 响应不明与取消

脚本在第一次远端写入前记录session_id、request_id和提示词摘要。已有记录会拒绝覆盖；失败后保留needs-check。超时、连接中断或请求结果不明时，先用原记录status/result检查，不另造会话重复发送。

需要停止本包时：

```powershell
python -X utf8 $dshScript cancel --record .audit/dsh-task/task-record.json
python -X utf8 $dshScript status --record .audit/dsh-task/task-record.json
```

cancel接受后仍需核对任务实际停止。记录和结果保留在本包产物目录，不清理其他会话或正在进行的工作。

## 需要SSH工作区时

`F:/Something/dsh-remote`是Harness的SSH工作区插件，网站登录不是SSH登录。插件README及`lib/index.js`是工具参数依据，本次核对版本0.8.35。当前Codex没有直接加载rw_*工具；在已安装插件的Harness会话中按以下流程使用：

1. rw_info确认实际机器与工作区；必要时rw_connect连接已授权SSH机器，再rw_pick_workspace选择其绝对目录。
2. 会话cwd必须位于返回的镜像目录，形成session级绑定。连接成功或设置当前机器不等于绑定已有会话。
3. 实时远端读写用rw_read_file/rw_edit/rw_exec等；内置文件工具只读取本地镜像。Windows主机可能使用Git Bash，命令按实际shell写。
4. rw_sync/rw_push先dryRun核对计划与冲突；不以force覆盖共享修改，不自动开启autoPush。单个补丁优先定向传输。

插件的`GET /dsh-remote/task?id=...`查询同步/推送等后台任务，它不是DeepSeek模型任务接口。模型委派使用原生`session/create`和`session/prompt`。插件路由缺失时不能靠调用/task派发模型任务，也不因普通委派擅自安装插件或重启远端服务。

协议参数和插件工具细节分别保存在skill的`references/harness-api.md`与`references/remote-workspace.md`。以上工作流保持net/core/base既有职责，不授权远程执行者重开core架构或启动AI训练。
