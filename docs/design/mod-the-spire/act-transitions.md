# 默认幕计划与跨幕生命周期

文档类型：参考（Reference）。本页定义`dev-run`开发配置的幕状态、开局预生成和切幕行为。当前工程目标和验收状态只查[主清单ENV-T10](../../review/tofix/README.md#env-t10)。

## 配置与源范围

`base-spire-singleplayer-dev-run-v1`使用显式默认计划Overgrowth→Hive→Glory，随后是尚未实现的TheArchitect最终事件。原“完整四幕胜负”目标保留，不能把三个地图幕完成当作全局Victory。

配置只接受Ironclad、A0、全解锁、单人及`sts2-v0.109-xxhash64@1`和对应内容元数据。`dev`、`dev-map`继续保留单幕初始化和既有身份；它们不隐式切换为多幕计划。`Ironclad`和`Full`完整身份仍不开放。

[RunActPlan](../../../tools/base-migration-baseline/BaseSpire/SinglePlayer/RunActPlan.cs)读取唯一[版本化幕数据](../../../tools/base-migration-baseline/BaseSpire/Run/Content/base-spire.run.acts.json)。数据包含每幕完整事件/遭遇/Boss/古代人身份与源顺序、标签、地图长度和已有编队声明；不能执行的编队/敌人保留原身份，实际进入时明确失败。该数据摘要加入构建身份。Overgrowth三个遭遇的v0.109标签迁移与既有版本配置一致；冻结源码和v0.109元数据的差异显式记录，不改参考玩法。

依据为`ActModel.GetDefaultList/GenerateRooms`、`RunManager.GenerateRooms/EnterNextAct/EnterAct/SetActInternal`、`Models/Acts/Overgrowth/Hive/Glory`，以及已锁定v0.109参考的原生默认幕计划。

## 开局预生成

所有局内可变状态均在Engine实体、集合和字段中。[RunActWiring](../../../tools/base-migration-baseline/BaseSpire/Run/RunActWiring.cs)只持有编译符号和静态计划，不创建另一套C# RunState或随机调度器。

1. 复用Run初始化，生成并洗共享/玩家遗物袋；整局只做一次。
2. 洗共享古代人，先为所有后续幕分配共享子集，再生成任何幕的房间。空子集的有界随机调用也按源保留。
3. 按幕顺序：事件队列洗牌→弱/普通队列→独立精英队列→Boss→古代人。全部消费同一UpFront流；非空袋、回避与补袋复用既有队列编译器。
4. 保存各幕预生成结果，启用第一幕的活动队列。进入后续幕只复制相应库存到活动集合，不补抽、不重洗遗物袋。

预生成集合`RunActMetadata`、`RunActNormalQueue`、`RunActEliteQueue`、`RunActEventQueue`和`RunActSharedAncients`具名不发布。公开内容只包括正在访问的幕/房间和实际候选，不泄露未来队列、Boss或古代人。

## Engine状态与切幕

| 状态 | 生命周期 |
| --- | --- |
| `Run.CurrentActIndex` / `ActId` / `ActCount` | 0基幕索引、实际幕身份、默认计划的地图幕数；公开 |
| `Run.CurrentRoom` / `SettledRoom` | 整局战斗计数和提交幂等键，跨幕保留，不能重置后与旧房间相撞 |
| `Run.TotalFloor` | 以前各幕已访问节点数＋本幕已访问节点数；地图换幕后继续累计，遭遇派生流按此取楼层 |
| `Run.MapFloor` / `CurrentNode` / `MapPhase` | 当前幕本地游标；切幕先销毁旧节点，生成新地图，回到新起点 |
| 普通/精英/事件访问计数、问号赔率 | 随新幕重置；全局已访问事件身份继续保留 |
| HP/MaxHp/Gold、永久牌组、遗物、药水 | 同一权威资源与真实库存实例；药水槽位和空隙保留 |

`ContinueToNextAct`只在`ActCompleted=true`且`MapPhase=6`时合法。实际Boss奖励全部解决后才设置这两个条件；奖励相位不得切幕。重发请求由公开账本返回原结果，不再次切幕。

切幕时清理旧战斗能力、存活及死亡怪物、临时战斗牌、出牌历史、充能球等已有战斗级对象；战斗格挡/能量/星星/计数复位。长期卡实例和库存不重建，HP/MaxHp以Run权威值恢复玩家副本。旧地图节点/边被销毁，新幕使用独立地图流物化，因此旧公开句柄不再有效。

## 地图、奖励与后续边界

每幕地图分别使用`act_1_map`、`act_2_map`、`act_3_map`。普通房间数为15/14/13；Rest计数分别为源码的Gaussian(7)、Gaussian(6)、NextInt(5,7)，后两幕Unknown计数在标准值上减1。复用同一地图生成器和物化预算，不为每幕复制算法。

后续幕的第一个合法选点是实际古代人起点，完成它后才应开放普通后继。古代人效果未实现时按实际身份明确失败，不把它跳过为空事件。第一幕沿用既有开发开局，不据此宣称已实现Neow完整流程。

验收据此区分开发开局首幕与包含Neow的正常开局首幕：前者可以验证地图／战斗／Boss奖励组合，后者必须先实现Neow选项、效果及其后续状态延续，再运行首幕全程。本段只明确现有开发范围的证明边界，未新增或改变运行行为；实际排期见[当前计划](../../review/tofix/README.md#推进顺序)。

奖励升级概率读取实际`CurrentActIndex`，不继续固定第一幕的零概率。后续幕事件资格尚未接齐时明确失败，不能套用第一幕资格。第三幕结束后的`ContinueToNextAct`明确停在TheArchitect缺口，保留非全局胜利边界。

## 验证入口

```powershell
dotnet run --project tools/differential -- act-transition --output .audit/act-transition-check
python -X utf8 net-the-spire/client/run_game.py --host-dll PATH_TO_HOST_DLL --scope dev-run --seed ENV-T04-RNG-SHUFFLE-001 --output .audit/dev-run --use-potions --step-unsupported-room
```

第一条是原生默认三幕预生成、九张地图及受控切幕的独立核对，验证队列、计数、RNG指纹、状态/实例保留、旧对象清理、拒绝和最终边界；受控`ActCompleted`不是正常通关证据。第二条从正常公开开局推进，记录真实死亡或具体内容缺口；返回非零不能算通过。历史结果见[证据索引](../../review/records/README.md)。
