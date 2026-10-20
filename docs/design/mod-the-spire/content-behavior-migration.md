# 内容与行为迁移实施契约

关联 [G01～G10 目标与验收](content-behavior-refactor-goals.md)，任务 BEHAVIOR-01。2026-10-19 用户明确要求开始迁移，Codex `/root` 负责。当前范围、切片及状态只维护在[GitHub 任务清单](https://github.com/QingXiang22/auto-the-spire/issues/28)；本文定义接口与验证，不复制任务状态。

## 出牌正文与上下文

现有 DSL 只能静态 call 或广播 emit，不能在分散内容模块中声明带唯一键的正文绑定。增加通用文本 `dispatch` 与 `dispatchCase`：分派声明参数／可选 pure 与 result，case 提供静态字符串键及目标过程，调用仍使用普通 call（额外传 key）。编译期拒绝重复键、未知分派、缺少／错误的处理签名；运行时未知键在调用处理过程之前失败。编译器将它们降为已有比较、分支、调用和返回 IR，不引入动态反射、游戏 ID 或另一执行器。模板实例中的 case 保持词法作用域和错误源定位；旧文本不改变执行语义。

base 声明正文分派，参数为 owner／card／target／play。每个已执行卡定义有一个 case，正文显式顺序调用自己的效果过程；移除主效果对 CardPlayed／CardPlayedOnTarget 的广播订阅。外部反应仍使用 Before／After 通知，正文流程不靠这些通知收集效果。

一次出牌序列保留 owner、card、目标、自动标记、实际支付／原始 X、结果区及父执行；每次重复创建独立 CardPlayRecord，记录序列、index／count 与父执行。流程依次执行 Before、开始事实、正文、附着站点、完成事实、After，按玩家存活和源结束边界决定继续。序列付款一次、结果区提交一次；嵌套及 decide 使用同一 Engine 栈恢复，不重付费或重放正文。兼容 CurrentPlay 只做保存／恢复，新正文显式传 play。

未定义正文与定义缺失在支付前失败；定义注册、catalog 来源和池顺序继续分别核对。开发占位牌不冒充正式定义或正常 Run。公开动作名称可保留，内部实体／执行关联隐藏，不让新运行态泄漏到公开内容列表。

## 攻击与结果边界

首个攻击切片在 DSL 中增加 Attack 与 DamageResult 隐藏实体。Attack 保存攻击者、card／play、目标策略、段数及当前击；DamageResult 保存 Attack／hit、来源、受击者、格挡／实际失血／超杀与致死事实。命中先提交本批全部目标，再按结果发布通知，最后按本批致死事实发布死亡通知。Before／AfterAttack 每个命令一次，段数修正后逐击重新取得目标；随机目标仅在实际有候选的命中消费目标流。直接伤害可以没有 Attack 和卡来源，不能从 CurrentPlay 自动推断为卡牌伤害。

攻击深度与出牌深度共同保护外层结算；结果历史在下一战重建牌组前先于出牌记录清理。Vigor 的攻击引用仅在 Before 至 After 内有效并清理；Rupture 的卡伤害消费显式结果来源，仍按本轮已支持的玩家／敌人持有者边界。未接死亡防止、召唤／复活或其他角色消费者时明确保留未支持，不把结果模型当作这些玩法已经完成。

直接群伤另有DamageBatch，不创建伪Attack。MercuryHourglass、Inferno、LetterOpener与StoneCalendar一次冻结目标，先提交全部结果，再通知与处理死亡；DamageDepth保护批次内嵌套回调不能提前提交胜利。Thorns、Juggernaut与FirePotion等明确传玩家攻击者，card／play保持none，不因正在出牌就取得卡来源。未提供来源的开发兼容DamageEnemy仍保留未知来源。

普通动态伤害由每张牌的 ReadCardDamage case 提供，CardBaseDamage 不再枚举 DamageScale；预览与实际伤害修正共用纯过程。当前百分比乘法仍沿用四位整数中间值，尚未校准为原版完整 decimal；本包的结果模型不关闭 G05 的数值精度条件。

## 费用、牌区与实例能力

费用修正作为独立 CardCostModifier 保存 card、source、set／add、amount 和 turn／play／turn-or-play／combat 寿命，按插入顺序折叠；临时费用不再依靠三个覆盖开关。公开 Cost 是命令维护的缓存，读取和预览不写缓存。克隆复制独立修正，新战清除临时费用，永久基础费用与临时费用分开。

MoveCombatCard 统一拥有者、永久／战斗资格、原区／目的区、插入位置、满手与移动通知；生成入手、回顶、Anger 复制和 PrimalForce 移除调用此操作。主动弃牌另发 CardDiscarded；回合末整批 flush 和打出结果区不冒充主动弃牌。FinishTurnHand 冻结 Ethereal 批次，再处理 BeforeHandFlush、Retain／ShouldFlushHand 筛选、整批提交与通知。新抽到的牌不混入旧消耗批次。当前未接带独有回合末手牌正文的牌与完整有序多选消费者。

CardCloned、CardCombatCopied、CardRemoving 是附属能力的复制与清理接缝。Stomp 的费用与 HowlFromBeyond 的耗尽堆自动行为归所属模块；通用 Card 不再保存它们的专属开关。CardAttachment 保存附魔身份与状态，Sown 在明确附魔阶段回能后禁用；永久→战斗副本与战斗克隆按各自来源复制，升级不重置状态，销毁前清理。公开附属实体采用通用 identity_from 依附卡身份，隐藏抽牌堆不泄漏附魔或卡引用。

Power 施加接收 applierPlayer／applierEnemy／card／play，并经过 ShouldApplyPower、ModifyPowerGiven、ModifyPowerReceived。定义选择 shared／per-applier／independent 匹配；现有 singleStack 兼容语义保留，不能视为完整原版 StackType。Vigor 支持敌我攻击资格与层数快照，Rupture 消费实际 DamageResult 的来源。

准确类型、调用签名和阶段见[行为操作参考](../../development/base-spire/reference/card-behavior.md)。

## 资源、访问与登记

资源命令保存来源及实际变化；金币先修正、通知修正结果，再提交正收益。治疗不隐含复活。金币领取复用带来源的命令，营火与事件复用永久选牌／升级；永久移除清理附属状态及旧战斗来源引用。资源仍继承开发 schema 上限，未恢复所有来源修正者。

EventVisit 独立拥有 DefinitionId、Page、Status、ReturnRoom 与访问序号。共享入口验证访问互斥，共享 finish／close 转换，叶内容只表达自身选项与领域调用。SapphireSeed 和 DoorsOfLightAndDark 的全部选项接入原生决策；后者每次访问持有独立、按源内容种子初始化的随机流，完整稳定洗牌不消费遗物 Niche。这里只开放显式开发访问，不代表地图池、子战斗返回或原 M01 已恢复。

构建工具 validate／check 从启动后的 CardLibrary、PowerLibrary 核对可执行 ID、完整来源 catalog 和公开 content_ids；错误文件映射与缺登记分别报错。sync-content 仅从这两个定义库及显式启动实例派生相应公开 ID 列表，不改公开政策、源分母或池顺序。遗物行为见证仍由实际执行登记；其他种类的目录派生继续按真实消费者扩展，不引入运行时目录扫描。

Host 的派生 0.2 应答目录增加可选 Boolean Flag，Bool 暂停发布 false／true 两个完整候选；实体单选与取消保持原形状。提交仅回填 Engine 的对应 RequestId／Flag，不增加业务专用命令；目录与后端身份随声明变化，准确合同见[协议](../net-the-spire/protocol.md)。

## 验证与迁移

首包复用现有 Ironclad／内容模型与公开检查，补足能区分下列错误的判别：重复／缺正文、未知键、模板词法绑定、错误签名、pure 拒绝、正文顺序、付款次数、嵌套执行身份及原生选择暂停。局部调试按失败定向运行；集成时固定最终包执行受影响 Engine、base 与公开组，不反复跑未变化状态。

core 文本分派进入编译身份 dispatch/1；依附身份投影、pure 显式失败与 none 相等比较使执行语义迁至 engine/ir@0.7、Core.TheSpire 0.7.0。base 同批迁至 0.7.0-dsl.40，公开构建身份同步。旧规则产物不能冒用新身份。原正常 scope、源分母、独立比较与 ENV／W 完整条件不变，未执行独立对拍不记通过。
