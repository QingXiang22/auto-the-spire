> 记录整理（2026-10-06）：原交付、复核及逐批证据保留；开放项已并入[主清单](../../tofix/README.md)，执行合同见[对应提示词](../../../design/agent-goals.md)。本文按原日期阅读，不作为当前待办入口；移入records不表示问题解决或父项验收。

# base-spire 后续任务编排交接

日期：2026-10-05。范围：负责人阅读现状、源码核对与文档编排；没有生产编码、启动agent、构建、提交、清理或AI训练。本报告保持tofix作为规划接续依据，不表示缺陷关闭，状态只在[唯一清单](../../tofix/README.md)维护。

## 1. 决策与落点

新增[base实施编排](../../../design/mod-the-spire/base-spire-implementation-plan.md)，细化原BASE-01/02/03，不调整ENV范围、固定seed、时间保护或模块职责。接续唯一工厂与正式源子集，执行为：入口收口与B/C接线→敌人状态机/共享DSL→整幕初始化/遭遇→地图与真实奖励/长期写回→各房间及遗物药水→各幕/完整Ironclad→阶段申请→其余角色/进阶→候选结算。公开出口与严格对拍随每批交付。

主清单新增规划入口并校准顶部时点；分工文档与agent goals同步阅读入口及“现有修复待复核”措辞。A原报告与旧负责人复核保留原文，不将旧发现或执行者自报冒充新的独立验收。

## 2. 源码核对发现

- R1/R2/R3修复及检查已存在：当前范围仅dev；起始牌组压缩后验证展开顺序；未知源基线拒绝并绑定内容/流布局摘要。未重新运行，仍待独立运行复核和消费者核对。
- 工厂已接Burning Blood与Niche，但 `BaseSpireRuntimeFactory.cs:131,136` 的限制描述仍称出生生命取下限、遗物无效果，`RunConfiguration.NotImplementedReason`也有过期项目。已并入BASE-01首批生产描述收口，未修改代码。
- 旧A交接的弱遭遇数加总会把队列误算成18；源 `ActModel.cs:361` 从弱遭遇数补到总数15。回避谓词 `ActModel.cs:421` 同时排除同标签及同一遭遇，不只是标签。
- 源状态机首动零随机仅适用于初始状态已经是Move且尚未执行的路径，随机分支首动不能一律不抽数。见 `MonsterMoveStateMachine.cs:60-78`。
- `tools/differential/Program.cs` 尚无test子命令；`BaseBackend.cs`仍用受控Session；net ControlSession仍直接创建Session。共同正式入口接线仍未交付。

## 3. 验证与风险

改动文件：本文、base实施编排、主清单、core冻结后分工约定、agent goals。运行 `python tools/doc-check/check_docs.py` 和 `git diff --check` 核对文档链接/格式，结果在主清单本轮规划记录登记。

未运行solution构建、Engine/Slice/net行为检查、运行时内容加载、严格对拍、正常四幕胜负、真实客户端或Release性能。A最新95/282/393仅引用其原报告，旧独立复核95/275/393保留历史；本轮不新增测试通过数字或关闭父项。当前参考hash/派生冲突仍需B和负责人按源向量核对，数值精度和整局调用顺序未验收；这些不阻止base继续不受影响的规则实现，但相应对拍不能记passed。
