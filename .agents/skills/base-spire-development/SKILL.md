---
name: base-spire-development
description: "开发、迁移或审阅 auto-the-spire 的纯 DSL base 内容与共享玩法时，按领域教程和准确操作定位规则及源依据，选择验证并保留未支持边界。用于卡牌、Power、遗物、药水、敌人、角色、房间、地图、奖励与 Run；不用于旧 C# base 扩张或 AI 训练。"
---

# base-spire 开发

教程中的文件编辑命令以 PowerShell 复现；远端环境可使用等价的文件操作，源码定位助手只需 Python 标准库。不要把未验证的 shell 转写声称为已复现教程。

本 skill 是[人类开发手册](../../../docs/development/base-spire/README.md)的任务路由，供 Codex 和 DSH 使用。假定在该仓库内工作；不依赖用户机器路径、插件或远端服务。玩法与接口以项目文档和 DSL 为准，任务状态在 GitHub Issue 维护，这里不保存第二套规则。

## 开始与约束

读取根／目标目录适用指令和当前任务的认领／范围，再选择下表的一条路线；不要默认加载所有历史记录或整本手册。[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)维护状态，[迁移规格](../../../docs/design/mod-the-spire/base-architecture.md)定义纯包边界。

- base 包仅含 `.spire`／必要静态 `.json`；玩法用 DSL，真正共用的操作放领域 rules；catalog definition 可承载完整内容模块，独有规则／私有状态留在所属文件，以模板作用域隔离。简单内容仍可只用 `use`。Host／Engine 不加入按内容 ID 的游戏效果分支。
- 遗物与Power统一按[监听编写指南](../../../docs/development/base-spire/how-to/write-effect-listeners.md)接入：主流程发布通知或征询修正，效果规则绑定有效实例；不恢复主流程中的遗物专属字段查询或Power效果直接调用。
- scope 从实际 package.json 读取。静态来源条目、DSL 定义、行为测试结果、独立比较与正常 Run 分别报告；来源条目没有已加载 definition 时不计支持。
- 旧 C#／旧作者语法只在 `tools/base-migration-baseline/` 核对源码，不恢复生产依赖。保留原来源池、资格条件与全部必测用例；未迁移显式失败，不删池、补答案或注册空效果。
- 新共享机制先补对应 spec 的状态、输入、顺序、寿命、失败和验证，再写 DSL。通用语言缺口用最小文本复现按现有授权处理；本 skill 不授权解除暂停、擅自改变核心兼容性约定或派出远程 agent。

## 按任务只读需要的文档

| 本次任务 | 首先阅读 | 再读什么 |
| --- | --- | --- |
| 修改已有卡牌数值、Power公式或私有状态 | [修改单个内容](../../../docs/development/base-spire/how-to/edit-content.md) | 所属文件与对应判别；精确Power接口查[Power参考](../../../docs/development/base-spire/reference/powers.md) |
| 新增拥有独有效果的内容模块 | [新增内容模块](../../../docs/development/base-spire/how-to/add-content-module.md) | 已有完整消费者及显式登记步骤 |
| 改已有模板参数／新增同类内容 | [内容扩展](../../../docs/development/base-spire/how-to/extend-content.md) | 对应内容文件与模板；首用时看[配置教程](../../../docs/development/base-spire/tutorials/first-relic.md) |
| 新共享触发／生命周期 | [机制迁移](../../../docs/development/base-spire/how-to/migrate-mechanism.md) | 对应领域 spec 和[操作参考](../../../docs/development/base-spire/reference/operations.md)；语法实践看[共享规则教程](../../../docs/development/base-spire/tutorials/shared-rule.md) |
| 卡牌、附着、费用、伤害、回合 | [卡牌与战斗](../../../docs/development/base-spire/how-to/cards-and-combat.md) | 目标源类／共享约定，不通读全部 Cards |
| Power、遗物、药水 | [Power 与物品](../../../docs/development/base-spire/how-to/powers-relics-potions.md) | 目标生命周期和一个真实调用方 |
| 遗物／Power通知、伤害／费用等修正或迁移旧反应 | [编写通知与修正监听](../../../docs/development/base-spire/how-to/write-effect-listeners.md) | [领域约定](../../../docs/design/mod-the-spire/effect-listeners.md)与[通用签名](../../../docs/dsl/core/0.3/authoring-reference.md#实例订阅与只读修正)，再读一个同类消费者 |
| 敌人、行动、遭遇 | [敌人与遭遇](../../../docs/development/base-spire/how-to/enemies-and-encounters.md) | 对应敌人／遭遇与随机约定 |
| 角色、房间、地图、奖励、跨幕／终局 | [Run 与房间](../../../docs/development/base-spire/how-to/run-and-rooms.md) | 本次拥有状态的领域约定及必要消费者 |
| 加载失败、公开结果或覆盖问题 | [验证与排障](../../../docs/development/base-spire/how-to/verify-and-debug.md) | 报错文件附近；包字段只查[格式参考](../../../docs/dsl/core/0.3/package-reference.md) |

## 有界定位，按需追到源

在仓库根目录运行单个查询，例如：

```powershell
python -X utf8 .agents/skills/base-spire-development/scripts/locate.py relics --id base-spire:the-abacus
python -X utf8 .agents/skills/base-spire-development/scripts/locate.py powers --search Strength --limit 3
```

domain 支持 cards／powers／relics／potions／enemies／events／characters／encounters／run／pools。默认返回最多 5 项，最大 20，必须提供精确 `--id` 或非空 `--search`；从包外工具固定数据读取 source_path／hash、池与 exclusion，从生产目录读取 DSL definition，并返回真实支持 scope。run 查 Act 源条目；房间／地图没有 catalog 时按上表直接进指南，不能把找不到条目当作领域不存在。

已有 definition 时，优先读该文件、所用模板和该操作的文档。未迁移时，先读该领域指南和约定，再按返回的 source_path 局部检索所需方法：

```powershell
rg -n -C 8 'AfterShuffle|Block' sts2/src/Core/Models/Relics/TheAbacus.cs
```

上例仅用于确实需要核对 TheAbacus 来源时。它不要求每次配置修改都重读原版源。需要源码时追加 `--verify-source` 比较所选文件的原始字节 SHA256；缺源／不匹配返回 3，不自动更新验证记录。查询无匹配返回 1、格式／文件错误为 2、成功定位为 0；0 不是玩法已支持。`--package` 可指定练习副本 manifest。

若接口文档与实现不一致，只读声明或错误位置附近，记录差异并修文档／对应约定；若 hash 不符，先确定版本或换行差异。不要为寻找一个符号全文读取组包器、Host、所有源目录或所有旧测试。池成员和大快照仅在本次确实修改资格／顺序时读取。

## 实施与验证

复用准确的完整领域操作，不把字段更新当作获得／复制／死亡等完整行为。按显式 index 组合，JSON 只描述静态来源／元信息；公开内容列表与源目录各按其职责维护。

编写监听时先区分通知产生副作用与pure修正返回计算值。instances筛选资格，reaction末参接收当前listener，instance_order表达实际实例顺序；专属状态按能力／实例保存并随获得、移除和跨战清理。按改动选择能区分顺序、失效、嵌套／暂停和查询只读的验证；完整规则及示例维护在指南，不在skill复制另一套接口。

选择上面验证指南中与修改相符的命令。`validate` 证明加载和启动；当前 `check` 是固定正式行为验证，不能通用证明任意新内容。教学驱动只提供实际公开观测，不能证明原版内容覆盖。新增正式内容必须实际运行行为测试；独立参考比较未运行就保持未验证。

交付记录任务、变更、来源／版本、scope／seed、命令与结果、未支持和未验证范围，更新人类手册中受影响的操作／领域页。当前状态只更新对应 Issue；不复制当前支持件数到 skill，不为每张牌建另一套任务表。

## 在 DSH 中使用

Codex 可调用 `$base-spire-development`。DSH 未验证自动发现 `.agents/skills`；由派单者在已有授权任务中附以下显式入口，并替换真实任务／读写边界／验收条件：

```text
先读取 .agents/skills/base-spire-development/SKILL.md，并按任务只读对应手册。
任务与认领：GitHub 执行工单链接和编号；本次内容／机制。
允许修改：本包明确列出的目录或文件；其他文件只读。
验收：本次明确的状态变化、边界、实际命令和验证结果位置。
缺共享能力／源版本不符时记录具体缺口，不回退旧 C# 或缩小源范围。
```

该入口随仓库携带；不要把它改成这台机器的绝对路径，也不需要远端密码或额外 skill 依赖。使用本 skill 不等于已经派单、已验收或已获得额外修改权限。
