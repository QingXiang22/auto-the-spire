# 奖励

查找或修改奖励时从本目录开始。[全部领域](../README.md)。

| 位置 | 负责什么 |
| --- | --- |
| [RewardWiring.cs](RewardWiring.cs) | 奖励状态、领取与关闭 |
| [RewardCompute.cs](RewardCompute.cs) | 奖励候选与纯计算 |
| [RunCardRewards.cs](RunCardRewards.cs) | 整局卡牌奖励操作 |
| [Compilation/RewardCompilation.cs](Compilation/RewardCompilation.cs) | 奖励声明与程序接入 |

行为检查随对应领域的实际消费者维护。

组合已有能力时先修改内容或所属领域模块；新增跨领域依赖或改变声明顺序时，再修改 [Composition](../Composition/README.md)。
