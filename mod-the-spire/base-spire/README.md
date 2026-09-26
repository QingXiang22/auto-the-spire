# base-spire

`base-spire` 是原版内容规则包，依赖 `core-the-spire`。当前实现覆盖铁甲战士起始状态、基础攻击/格挡/痛击、力量/易伤、仪式、Damp Cultist 与 Calcified Cultist 的行动循环及两者组成的 `CultistsNormal` 多敌 encounter；Burning Blood 通过整局战斗胜利钩子回复 6 点生命，并受最大生命限制。`BaseSpireMapGenerator` 提供恢复源码中的四种 Act 单人标准地图生成器、三幕默认路线定义及 Unknown 房间基础概率配置。

卡牌效果在注册时用 JSON 效果 DSL 编译成强类型节点；环境 step 只执行节点，不在结算热路径解析 JSON。DSL 操作包括顺序、条件、重复、攻击、格挡、施加状态、抽牌/移动牌、资源修改和选择。复杂内容可通过 `ICustomEffectHandler` 扩展，但只能调用 core 提供的效果命令，不能直接改写运行态。

从仓库根目录运行 `dotnet run --project core-the-spire/Core.TheSpire.Tests` 执行无外部测试包依赖的战斗切片检查。源规则审计依据及待读领域见 `docs/sts2-core-source-audit.md`。
