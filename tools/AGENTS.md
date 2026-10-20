# tools 协作约定

本文件适用于 `tools/` 及其子目录，遵循 [开发约定](../CONTRIBUTING.md)。

## 开发流程

遵循 [开发约定](../CONTRIBUTING.md)，近期任务见 [路线图](../docs/roadmap.md)。使用任务分支／独立 worktree，通过 PR 和受影响 CI 交付；使用方法或接口变化时更新正式文档，无需逐批交接、会话登记或本地证据链接。

## 工具与对拍边界

Git仅维护[工程工具清单](README.md)登记的可复用入口。一次性修复、提取、实验与生成产物保留本地；新目录默认忽略，确认维护用途后更新根.gitignore白名单。artifacts/.artifacts/.audit均为本地产物，不提交。原版源码恢复器和退休Legacy工具不再纳管。

ENV工具遵守[范围](../docs/design/environment-completion-and-differential-v1.md)和[实现约束](../docs/design/environment-implementation-constraints.md)。比较器消费共同base工厂，参考独立执行且默认只读，参考依赖只在工具层；不得将参考答案或工具补偿写入生产玩法。

严格suite缺case/零执行/缺参考/unsupported/异常/超时/首差/缺维度均非零，旧compare返回0不代表验收；不删seed/换弱case/补抽随机/注入清场制造通过。正常Run与受控探针分列，源分母/排除与覆盖须有有效证据。共享构建按B/负责人协调串行；父项、来源窄裁决与候选关闭由负责人决定，不自行改A/C、core或参考玩法。
