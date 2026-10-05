# auto-the-spire 协作约定

本文件适用于 `docs/` 及其子目录。子目录若有自己的 `AGENTS.md`，在其范围内同时遵守更具体的约定。

## 目录说明
本目录被用来存放`auto-the-spire`项目的所有文档内容。
`review/tofix` 存放当前问题清单及待整理的阶段交接，`review/records` 存放历史证据，`review/archived` 存放有关闭依据的缺陷报告，`review/deprecated` 存放因范围变更不再适用的报告。
2026-10-03整理约定：`review/tofix/README.md` 是唯一当前整改/待决清单；新交接仍按根约定先提交tofix，按阶段合并小批次，不每个原语单独新增报告。负责人审阅后把开放项并入清单。`review/records/YYYY-MM-DD/` 保存交付、部分交付、来源审计和历史规划证据，`review/records/README.md` 登记原报告与当前事项；移入records不表示已修复。只有有关闭依据的缺陷转archived。实现阶段按清单认领并补证据，不把旧报告里的“未开始/下一步”直接当新派单。
执行者更新主清单时仅补对应项的认领、进度和证据；不得清空、删减W项、移走主清单或用最小指针版替换完整任务定义。范围、优先级和关闭结论由用户/规划者维护；发现内容丢失先保留当前版本和证据，再交负责人恢复。阶段报告的RESUME HERE只定位该执行线，不取代全项目待办入口。
`dsl/` 里存放关于dsl脚本如何编写与如何工作等行为的文档介绍，并且对dsl版本进行区分，游戏内容语言为 `dsl/0.0.1/`，通用 core 基础语言为 `dsl/core/0.1/`，DSL 规则库与作者配置层为 `dsl/core/0.2/`，默认作者入口是 `dsl/core/README.md`。
`design/` 里存放关于项目设计相关的报告，分为`design/net-the-spire`、`design/core-the-spire`、`design/mod-the-spire`、`design/ai-the-spire`这四个目录。
`design/spec.md` 描述项目整体上的specification，各模块的specification放在各自的目录里；跨模块的项目级报告（源码审计、阶段评审等）直接放在 `design/` 下。
