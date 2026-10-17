# 在已有机制上扩展内容

文档类型：操作指南（How-to）。适用于对应共享 DSL 模板已经覆盖完整行为的卡牌、Power、遗物、药水、敌人或事件。若操作参考找不到所需完整机制，先走[机制迁移](migrate-mechanism.md)，不要借旧 C# 接口补效果。

## 1. 确定来源和修改落点

从领域 `catalog.json` 按稳定 id／源类名定位 source_path 和 hash，检查资格及 definition。首次定位可使用 [skill 定位助手](../../../../.agents/skills/base-spire-development/scripts/locate.py)，它返回有限条目，不执行玩法。已有 definition 时读该文件和所用模板；未迁移时只读对应源类、被调用的共享语义和必要旧检查。

新内容复用原稳定 ID、来源组和单文件名称。教程自创内容使用独立教学命名空间，不能添加到原版源分母。拿不到匹配版本的源时记录待核验，不能把猜测写成已审阅资格。

## 2. 选择模板而不是复制机制

核对模板覆盖的输入、owner、目标、状态寿命、顺序和异常边界。对 TheAbacus 类配置，AfterShuffleBlockRelic 只覆盖重洗后无加成格挡，不能拿它描述普通卡牌格挡、受击触发或永久获得资源。

在来源组新增一个 core/0.3 `.spire`，只写同一 namespace 与 `use`，按模板提供有界字面量。不得在 catalog definition 文件写 procedure／action／subscription。如果复用模板必须加 C# 按 ID 分支，先重新审查共享语义。

可运行新增例子见[共享规则教程](../tutorials/shared-rule.md)。正式原版内容的定义参数应取自匹配版本源，不能直接保留教程数值。

## 3. 显式注册与保留源范围

将内容路径加入领域 `index.spire`，共享规则尚未组合时同步包含；只有根入口拥有 start、imports 和 version。不要添加文件扫描或隐藏启动回调。

将对应源行 definition 从 null 更新为包内相对 `.spire` 路径，保留 source_path、hash、完整源池与资格依据。新的数据文件必须在 manifest catalogs 中声明。不得将未迁移内容从源池删除，也不得用空效果声明凑覆盖。

需要对外显示的内容 ID 加到 public 元数据对应列表；公开类型／字段／动作由 DSL 声明与投影派生。变更后核对目录和合法候选，不向 net 新增逐项游戏规则注册。

## 4. 验证与交付

先用开发工具 validate 加载并启动，再运行该机制已承诺的行为检查。只改已有模板数值的内容不重复复制整个引擎测试，但要核对定义参数、资格、源池与实际调用。新行为的差异则必须有相应判别。

新增正式遗物时，当前覆盖检查要求 definition 与真实行为报告一一对应；不能把 TheAbacus 的见证批量赋给其他遗物。见证生成由包外检查维护，开发 helper 观察成功不自动成为原版覆盖报告。[验证指南](verify-and-debug.md)说明命令边界。

提交时更新对应领域契约／参考、主清单和综合证据，记录构建、scope、seed、来源与未验证项。原 ENV/W 完整验收独立保留。
