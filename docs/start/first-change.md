# 完成第一个改动

文档类型：操作指南（How-to）。适用于已能运行[首次检查](environment.md)的贡献者。分支、工单认领、提交和合并规则统一遵循 [CONTRIBUTING](../../CONTRIBUTING.md#开始与交付)。

## 选择任务并定位文件

从[任务入口](../roadmap.md#近期队列)打开已就绪工单，核对目标、范围、输入提交和验收条件，再按[开发流程](../../CONTRIBUTING.md#从工单到交付)认领。先查看工作区，保护已有改动：

```powershell
git status --short
```

| 本次修改 | 先读哪里 |
| --- | --- |
| 已有卡牌数值、Power 或遗物 | [base 修改指南](../development/base-spire/how-to/edit-content.md)；先用精确 ID 找到规则所属文件 |
| 编译、通用状态或 Session 调用 | [core 开发入口](../areas/core/README.md)与[核心接口要求](../contracts/core-engine-baseline.md) |
| 公开字段、Host 或客户端 | [net 开发入口](../areas/net/README.md)与协议参考 |
| 公开观察编码或概率输出 | [AI 开发入口](../areas/ai/README.md)及对应约定 |
| 文档或示例 | 当前权威页面与[文档维护规则](../../CONTRIBUTING.md#哪些内容进入正式文档) |

## 修改并验证

第一次修改原版数值时，在隔离副本按[第一个遗物教程](first-relic.md)观察修改前后结果；卡牌修改的准确字段与普通／升级判别见[单个内容指南](../development/base-spire/how-to/edit-content.md)。练习结果不覆盖正式原版预期。

只改文档运行 `python tools/ci/check.py docs`；运行时按[CI 说明](../../tools/ci/README.md)选择受影响组。失败先定位具体差异，保留原用例、来源范围和断言，不能删检查使其通过。

## 提交可审阅的结果

核对差异只包含本工单改动，更新受影响的用法与接口文档，再按[交付流程](../../CONTRIBUTING.md#从工单到交付)提交 PR。PR 关联工单并说明最终行为、实际命令和未验证范围；已有检查无相关变化时不用重复运行。
