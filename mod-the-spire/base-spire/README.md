# base-spire

`base-spire` 是原版内容规则包，依赖 `core-the-spire`。当前 Ironclad 池迁入了 53/87 张卡，另注册 Entrench 和 Slimed。效果覆盖攻击/格挡、抽牌与牌堆移动、选牌与升级、消耗触发、随机目标、AoE、动态计数、回合条件、能量上限、易伤倍率和临时力量生命周期；完整卡池仍待迁入。

已迁入部分 Ironclad 遗物：Burning Blood、Anchor、Vajra、Charon's Ashes、Self-Forming Clay、Red Skull、Demon Tongue、Paper Phrog 和 Ruined Helmet。Fire、Block、Strength、Blessing of the Forge、Beetle Juice 和 Flex Potion 由 `potions.spire` 定义；Flex Potion 与 Setup Strike/Mangle 共用 Power 生命周期。战斗奖励可掉落并领取药水，固定槽位支持消费后保留空位，Potion Belt 扩容 2 格。完整药水池和非战斗/自动药水仍待迁移。

九种怪物由 `enemies.spire` 描述，遇敌阵容仍使用 JSON。Burning Blood 在整局战斗胜利后回复 6 点生命，并受最大生命限制。`BaseSpireMapGenerator` 提供恢复源码中的四种 Act 单人标准地图生成器、三幕默认路线定义及 Unknown 房间基础概率配置；完整原版内容和 seed 差分仍待扩展。

卡牌、药水、Power 和怪物行为在注册时从 `.spire` DSL 编译成强类型节点；环境 step 只执行节点，不在结算热路径解析源码。DSL 操作包括顺序、条件、重复、攻击、生命损失、格挡、施加状态、抽牌/移动牌、生成牌、实例级选牌、资源修改、随机生成战斗药水和选择。复杂内容可通过 `ICustomEffectHandler` 扩展，但只能调用 core 提供的效果命令，不能直接改写运行态。

从仓库根目录运行 `dotnet run --project core-the-spire/Core.TheSpire.Tests` 执行无外部测试包依赖的战斗切片检查。源规则审计依据及待读领域见 `docs/sts2-core-source-audit.md`。
