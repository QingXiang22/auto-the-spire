# 在已有机制上扩展内容

文档类型：操作指南（How-to）。适用于对应共享 DSL 模板已经覆盖完整行为的卡牌、Power、遗物、药水、敌人或事件。若操作参考找不到所需完整机制，先走[机制迁移](migrate-mechanism.md)，不要借旧 C# 接口补效果。

## 1. 确定来源和修改落点

使用 [skill 定位助手](../../../../.agents/skills/base-spire-development/scripts/locate.py)按稳定 id／源类名查询。它从工具固定数据读取 source_path、hash 和排除依据，再从生产目录读取实际 definition，只返回有限条目，不执行玩法。已有 definition 时读该文件和所用模板；未迁移时只读对应源类、被调用的共享语义和必要旧检查。

新内容复用原稳定 ID、来源组和单文件名称。教程自创内容使用独立教学命名空间，不能添加到原版来源条目总数。拿不到匹配版本的源时记录待核验，不能把猜测写成已审阅资格。

## 2. 选择已有模板或所属模块

核对模板覆盖的输入、owner、目标、状态寿命、顺序和异常边界。对 TheAbacus 类配置，AfterShuffleBlockRelic 只覆盖重洗后无加成格挡，不能拿它描述普通卡牌格挡、受击触发或永久获得资源。

在来源组新增一个 core/0.3 `.spire`，使用同一 namespace。已有模板足够时只写 `use` 和有界字面量；独有效果可在本文件的模板作用域内声明 procedure／subscription／私有 entity，再展开一次。获得、伤害、抽牌、施加／移除 Power 等操作复用领域入口。如果复用模板必须加 C# 按 ID 分支，先重新审查共享语义。

卡牌复用[正文模板](../reference/card-behavior.md#定义正文与纯值)：普通攻击、格挡和自身Power分别使用AttackCardBody、BlockCardBody、SelfPowerCardBody，模板同时完成正文与静态伤害绑定。组合牌保留效果的显式调用顺序，基础伤害使用StaticCardDamage；动态公式保留自身pure绑定。不能只增加CardDefinition后等待广播。

独有效果的文件与登记步骤见[新增内容模块](add-content-module.md)，已有内容改动见[修改单个内容](edit-content.md)。可运行的共享模板例子见[共享规则教程](../../../start/shared-rule.md)。正式原版内容的定义参数应取自匹配版本源，不能直接保留教程数值。

## 3. 显式注册与保留源范围

将内容路径加入领域 `index.spire`，共享规则尚未组合时同步包含；只有根入口拥有 start、imports 和 version。不要添加文件扫描或隐藏启动回调。

在领域 `catalog.json` 增加 id 与包内相对 `.spire` definition 路径。目录格式为 `engine-dsl/content-catalog@1`，不保留空定义或来源进度。新增目录文件必须在 manifest catalogs 中声明。完整来源、hash、池成员及排除依据保留在包外工具固定数据中，不得将未迁移内容从源池删除，也不得用空效果声明凑覆盖。

需要对外显示的内容 ID 加到 public 元数据对应列表；公开类型／字段／动作由 DSL 声明与投影派生。变更后核对目录和合法候选，不向 net 新增逐项游戏规则注册。

Card／Power的content_ids可运行`dotnet run --project tools/base-spire-dev -- sync-content`从实际定义库及显式启动实例生成。它不替你登记定义或修改公开政策；validate／check会具名检查可执行目录、来源映射及公开列表漂移。

## 4. 验证与交付

按[批次规则](../../../../CONTRIBUTING.md#base-内容按批次交付)完成同机制的一组内容，再按[验证指南](verify-and-debug.md)选择本地定向检查。例如拾取生命上限类遗物使用`python tools/ci/check.py content --focus "relic:RL pickup "`，它构建一次并执行该前缀下的实际用例。新增内容须加入该参数化组，不能用旧内容通过代替本批结果；参数期望来自核对后的来源，不从被测实现反算。独有规则补相应判别，不逐项复制共享机制的端到端检查。

完整base行为与覆盖检查交由批次PR CI，本地不默认再跑base／net／migration全套。公开接口、共享机制或比较适配变化时，按影响补定向检查并说明原因；修改文档才运行docs。允许针对编辑中的具体疑点提前做小检查，不在每项之后重复构建和全套验收。

新增正式遗物时，当前覆盖检查要求 definition 与真实行为报告一一对应；不能把 TheAbacus 的执行结果批量赋给其他遗物。执行结果生成由包外检查维护，开发 helper 观察成功不自动成为原版覆盖报告。[验证指南](verify-and-debug.md)说明命令边界。

通过一个批次PR交付，列出完成的内容和受阻项；单项例外按批次规则说明原因。使用方法或接口变化时才更新对应规格／参考，检查结果在CI查看。完整环境仍按环境规格验收。
