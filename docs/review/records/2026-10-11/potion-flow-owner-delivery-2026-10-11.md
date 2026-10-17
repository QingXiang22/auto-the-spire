# 药水闭环负责人交付 — 2026-10-11

本包让正式单人环境按完整源池生成药水，并完成领取、满栏处理、目标使用、消耗和奖励关闭。归属 `ENV-T05-potion-flow-20261011`，由 Codex `/root` 亲自实现；有效状态只看[主清单T05](../../tofix/README.md#env-t05)，机制和完成条件见[药水契约](../../../design/mod-the-spire/potion-inventory/spec.md)。本报告是日期证据，不关闭完整原版范围。

## 接手后的第一步

1. 核对主清单与当前工作区，不重做本包的库存/奖励编排。规则代码在 `BaseSpire/Potions/PotionInventoryRules.cs`、`SpirePotionSource.cs`、`Effects/PotionEffectSource.cs`；作者入口见[base开发指南](../../../../tools/base-migration-baseline/docs/base-development-guide.md#补充药水时只读本机制)。
2. 在已就绪且负责人派单的前提下，B复核受控初始化交接，再为本包补独立药水生成、容量、使用及随机时序对拍。独立参考所需初始化缺口只阻塞对应case，不能换池或补抽。源码对账、受控检查和正常公开轨迹已经可复用。
3. A后继生产按主清单队列认领完整房型/奖励机制包。其余37项药水使用效果逐机制实现，禁止把可生成元数据当行为覆盖率；本次没有派发新的远程生产包。

## 已交付与职责

- `Potions/Content/potions.spire` 保存角色3项+共享45项的完整生成元数据与源顺序。`SinglePlayerContent` 加载实际资源并将摘要纳入内容身份，11项使用程序进作者模型，其余37项留真实元数据和具名未支持原因，仍参与生成。
- `SpirePotionSource` 只做领域读取/链接；效果复用既有解析器、逐效果程序与编译器。11项为 Fire、Block、Strength、Dexterity、Flex、BlessingOfTheForge、BeetleJuice、Energy、Swift、Weak、Vulnerable。DexterityPower通过正式Power源注册，没有新增白名单。
- `PotionInventoryRules` 拥有药水类型、栏位、奖励候选、生成/取得/丢弃；Run/库存全部在Engine，没有另一份C#状态。三槽容量、SlotIndex与空槽稳定；可见成员按槽号排列。领取保留同一候选实例。
- 满栏时TakeRewardPotion不合法，奖励保留。源没有自动替换对话框：先DiscardPotion，再领取；也可SkipRewardPotion。取消就是不提交丢弃。领取/丢弃/跳过不抽随机。
- 正规奖励顺序为掉落判定、金币、掉落时的真实药水两抽、三张卡各三抽，实际11/13笔Player.Rewards。稀有度采用源float32的 `<=0.1f` / `<=0.1f+0.25f`，按完整同稀有度源序列选具体成员。
- UsePotion排除未支持的瓶，敌人目标先提问；效果、实例销毁、BattleEndTail依次执行，不支付能量。Fire/Block为unpowered；修正了Block受Dexterity/Frail影响的问题。跨房间保持真实瓶实例，幕完成后只暴露下一幕入口。
- 自审发现allEnemies元数据/程序已接受，但旧动作分派仅绑定enemy/self，会消耗却不执行；已补非选目标分支，并以多敌人作者反例证明所有目标受伤、没有选择请求、瓶正常消耗。该接线没有把原版群体药水算作已支持，源池仍为11项使用程序。
- 正式公开目录由base语义派生，新增48项Potion目录、PotionBelt/RewardPotions两个Sequence及字段/动作，无wire类型或RPC扩展。C固定交接声明停止后，负责人补旧作者Profile的兼容处置，旧作者目录升v3；正式路径无需逐瓶登记。

## 验证与源身份

实际命令均从仓库根执行，门槛隔离目录 `.artifacts/parallel/owner-potion-flow/final`；最后追加的群体作者接线使用 `author-final` 独立构建/药水专项及公开复放。两种正式范围的IR/内容/语义身份实际对比不变，见[最终汇总](../../../../.audit/owner-potion-flow-2026-10-11/results-summary.json)。长证据在[药水audit](../../../../.audit/owner-potion-flow-2026-10-11/)。源为 `sts2/src/Core/Factories/PotionFactory.cs`、`Player.cs`、`PotionCmd.cs`、`PotionReward.cs`、两个池来源及各药水/Power原版类。

| 检查 | 实际结果与证据 | 证明范围 |
| --- | --- | --- |
| `dotnet build AutoTheSpire.sln --artifacts-path .artifacts/parallel/owner-potion-flow/author-final` | rc0；[build-author-final.log](../../../../.audit/owner-potion-flow-2026-10-11/build-author-final.log)，4项既有nullable警告；此前[build.log](../../../../.audit/owner-potion-flow-2026-10-11/build.log)同样rc0 | 当前完整solution构建 |
| 隔离Engine测试DLL | rc0，95/95；[engine.log](../../../../.audit/owner-potion-flow-2026-10-11/engine.log) | 冻结通用核心回归，core未改 |
| Slice `--potion-flow-only` | rc0，25/25；[potion-checks-final.log](../../../../.audit/owner-potion-flow-2026-10-11/potion-checks-final.log) | 48项独立源码元数据/顺序对账、源float阈值、真实生成、11/13抽、满栏/第一空槽/有序成员、全部11项效果、群体作者程序、临时力量收回、末次击杀与跨房间保留、旧作者兼容 |
| 完整Slice测试DLL（群体作者反例追加前） | rc0，738/738；[slice.log](../../../../.audit/owner-potion-flow-2026-10-11/slice.log) | 含地图/房间与当前正式IR身份；最后新增的1项作者分支由上述最终专项补证 |
| 隔离net测试DLL | rc0，416/416；[net.log](../../../../.audit/owner-potion-flow-2026-10-11/net.log) | 旧作者Profile、正式派生出口与C的地图诊断隐藏/本地可读检查 |
| 正常公开Boss重录+独立Host复放 | rc0，326次合法提交，8HP/401金币；[最终复放摘要](../../../../.audit/owner-potion-flow-2026-10-11/public-replay-author-final/summary.json)及[最终复放对比](../../../../.audit/owner-potion-flow-2026-10-11/public-repeat-final.json) | 正常80HP/99金币/10张起始牌；173HP/Slippery8的Vantom；真实药水领取、Dexterity使用、满栏显式丢弃；327个快照仅去session_id后全部一致；无状态注入，无独立原版侧 |

最终base身份由[构建描述](../../../../.audit/owner-potion-flow-2026-10-11/build-descriptions-final.jsonl)实际读取：dev ruleset `64b527d8b02f1266` / build `7045b03c100a75ab`；dev-map ruleset `487c685799f8581d` / build `4ffc446f7e8ad527`；共同content `6dddb291c1c24638`。正常公开地图身份：ruleset `net:ruleset:1f29fde3c9a9ab3a`，catalog `net:catalog:b400d7f332f590cc`，backend `net:backend:simulation-0.2-350911d86bad900d`。

## 差异与未验证

真实生成修复了以前缺少的两笔抽取，后续卡牌候选/金币改变，因此旧Boss轨迹和静态身份需要迁移。审阅新路径后重录固定候选，并保留严格身份检查；不是取消校验以保旧轨迹。旧作者出口新增字段原先造成4项显式映射失败，兼容修正后416全过。完整Slice的未知原语案例仍使用已实现的chooseCard，已换成真正未知的原语，保留原检查意图；dev golden按本包独立实际描述更新。

尚未验证独立原版双侧药水轨迹。其余37项使用效果、自动/非战斗用法、增加槽数或禁止获取的遗物hook、完整原版解锁/多人/角色池等没有实现。本包只完成Ironclad全解锁的生成/库存机制及11项效果；第一幕完成不是整局Victory，商店/精英/事件/幕转换与完整五角色环境仍按主清单开放。

负责人已看到B/C固定交接，原交接自报不计独立验收；本轮重点交付A，B受控初始化的独立复核及后继派单未执行。C的公开隐藏与本地诊断检查在本构建416项net及正常公开路径中实际通过。

指南已更新：药水作者只读本机制、已注册Power跨文件引用、程序与库存分工，以及Expr实时求值/Requires覆盖的SDK约定。doc-check扫描372份Markdown、0问题，git diff --check rc0；状态/认领/下一任务只维护主清单。
