# tools 协作约定

本文件适用于 `tools/` 及其子目录，补充根 [AGENTS.md](../AGENTS.md)。

## 任务与交接

必须执行根[任务与文档管理强制规则](../AGENTS.md#任务与文档管理全仓库强制)。先读[总入口](../docs/README.md)、[主清单](../docs/review/tofix/README.md)本线任务/P优先级及依赖；对拍工作读[B提示词](../docs/design/agent-goals.md#4-b严格对拍与结算-agent)，其他工具按用户明确任务执行。生产实现前登记任务ID/会话/目录/下一可验成果，每线同时一个完整包，不把临时工具变成第二套任务管理平台。

交接记录命令/返回码、构建与源/reference/配置/seed身份、实际执行数/首差/未验证范围，回写本线任务和证据链接；长日志放artifacts或.audit，当前状态不写多份README。历史报告里的命令/下一步不直接决定本轮任务。

## 工具与对拍边界

Git仅维护[工程工具清单](README.md)登记的可复用入口。一次性修复、提取、实验与生成产物保留本地；新目录默认忽略，确认维护用途后更新根.gitignore白名单。artifacts/.artifacts/.audit均为本地产物，不提交。原版源码恢复器和退休Legacy工具不再纳管。

ENV工具遵守[范围](../docs/design/environment-completion-and-differential-v1.md)和[实现约束](../docs/design/environment-implementation-constraints.md)。比较器消费共同base工厂，参考独立执行且默认只读，参考依赖只在工具层；不得将参考答案或工具补偿写入生产玩法。

严格suite缺case/零执行/缺参考/unsupported/异常/超时/首差/缺维度均非零，旧compare返回0不代表验收；不删seed/换弱case/补抽随机/注入清场制造通过。正常Run与受控探针分列，源分母/排除与覆盖须有有效证据。共享构建按B/负责人协调串行；父项、来源窄裁决与候选关闭由负责人决定，不自行改A/C、core或参考玩法。
