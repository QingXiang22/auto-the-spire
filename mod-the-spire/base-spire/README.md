# base-spire

`base-spire` 是原版内容规则包，依赖 `core-the-spire`。当前 Ironclad 池迁入了 28/87 张卡，覆盖基础攻击/格挡、格挡动态伤害、抽牌与牌堆移动、动态选牌与升级、批量手牌处理、消耗触发 Power、随机消耗和攻击目标、AoE、力量/易伤/虚弱、无格挡生命损失和持续型 Power。已迁入部分 Ironclad 遗物：Burning Blood、Anchor、Vajra、Charon's Ashes、Self-Forming Clay、Red Skull、Demon Tongue 和 Paper Phrog；易伤伤害倍率由可扩展 hook 修改，Paper Phrog 已验证真实战斗结算。Damp Cultist 与 Calcified Cultist 的行动循环及 `CultistsNormal` 多敌 encounter 已映射；Burning Blood 通过整局战斗胜利钩子回复 6 点生命，并受最大生命限制。`BaseSpireMapGenerator` 提供恢复源码中的四种 Act 单人标准地图生成器、三幕默认路线定义及 Unknown 房间基础概率配置。

卡牌效果在注册时用 JSON 效果 DSL 编译成强类型节点；环境 step 只执行节点，不在结算热路径解析 JSON。DSL 操作包括顺序、条件、重复、攻击、生命损失、格挡、施加状态、抽牌/移动牌、生成牌、实例级选牌、资源修改和选择。复杂内容可通过 `ICustomEffectHandler` 扩展，但只能调用 core 提供的效果命令，不能直接改写运行态。

从仓库根目录运行 `dotnet run --project core-the-spire/Core.TheSpire.Tests` 执行无外部测试包依赖的战斗切片检查。源规则审计依据及待读领域见 `docs/sts2-core-source-audit.md`。
