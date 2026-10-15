# DSH易伤包复核与后续并行拆分（2026-10-11）

最新DSH包交付了准确的阻塞证据和持续时间比较器负例，没有完成原易伤case的严格升级。负责人独立执行完整17项，得到11项双侧通过、1项工厂自检、4项unsupported、1项正常开局diverged，比较器7项自检通过。当前状态、下一包及认领只查[主清单T04](../../tofix/README.md#env-t04)、[T05](../../tofix/README.md#env-t05)、[T03](../../tofix/README.md#env-t03)。本文是本轮审阅与规划证据，不是新派单。

## 复核结果与实际证据

通过[dsh-remote-operator](C:/Users/26877/.codex/skills/dsh-remote-operator/SKILL.md)查询原记录：`ENV-T04-vulnerable-decay-20261011` / `session-fb139ad7-8c0c-4fbe-ab7a-f4ab685b00ee` 停止运行，seq568，最终turn/end completed。停止和completed不等于规则验收。[原交接](dsh-vulnerable-decay-handoff-2026-10-11.md)及[最终定向产物](../../../../.audit/dsh-vulnerable-decay-2026-10-11/final-strict/regression.json)与源码相符。

交付范围为tools/differential的suite/fixture说明、ComparatorSelfTest定向负例及README。负责人另纠正工具README的随机测量口径：base脚本期有实测，reference的false只有适配器声明，不能说两侧都已测量无新消费。最终Comparator没有专用易伤profile或过滤牌区逻辑。两侧分别真实执行Bash及end_turn，目标HP38→30，Vulnerable无→2→1；末边界抽牌堆base剩4张Strike，reference剩2张Defend和2张Strike。不能用这些牌区差异判定易伤生产规则错误，也不能把局部一致记为整个case passed。

负责人在仓库根执行：

```powershell
dotnet build tools/differential --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/owner-dsh-review-20261011 -v quiet
dotnet .artifacts/parallel/owner-dsh-review-20261011/bin/Differential/debug/Differential.dll test --suite regression --output .audit/dsh-review-2026-10-11/full-regression
```

构建rc0，0错误/3项既有nullable警告；完整严格执行rc1，17/17执行，独立参考实际16项（工厂wiring不需参考）。[完整报告](../../../../.audit/dsh-review-2026-10-11/full-regression/regression.json)的run_id为 `regression@env-t04-regression@1+20261010T171426Z`，这是执行器UTC标识，文档日期使用用户Asia/Shanghai的2026-10-11。

| 结果 | 实际数量与范围 |
| --- | --- |
| 双侧passed | 11，包括既有卡牌、Headbutt选择、末杀收尾和两项地图生成 |
| self_checked | 1，唯一工厂接线，不是双侧玩法 |
| unsupported_rng_profile | 4：enemy-death-aftermath、power-turn-end-decay、vulnerable-decay-via-bash、random-reshuffle-unsupported |
| diverged | 1：rng-initial-shuffle；第0边界敌人集合不同，reference有FuzzyWurmCrawler |
| 比较器自检 | 7/7，包括stream与新增vulnerable-duration |

DSH单case缺stream自检目标是所选输入不覆盖该维度，完整suite自检实际通过；不应把它诊断成比较器stream代码已失效。Vulnerable负例仅修改真实base记录层数1→2，准确捕获power字段差异；它证明比较器能报错，没有证明参考真实执行一个错误衰减，也没有验证第二次衰减、移除或衰减后伤害。

[输入完整性核对](../../../../.audit/dsh-review-2026-10-11/suite-integrity.json)使用Headbutt派发前原始suite：17项分母不变，除两轮目标Headbutt/Vulnerable以外15项定义完全一致；Vulnerable的ID、profile、setup、script、game_seed、必需维度、终点和内容/变体保持原样。Headbutt本轮实际passed，未回退。suite SHA256 `b783c9942d93f62a2632f6ec10633eb7ef1fcc82d1c905851e976a87e89830ed` 与DSH最终交接一致。

## 为什么这一包没有闭合

不是新增易伤DSL很复杂，而是受控初始化尚未提供完整的共同状态：

- `BaseBackend.OpenControlledSession` 按game_seed配置源随机流，但 `CombatSlicePackage.Build` 默认 `shuffleBattleDeck=false`，受控牌堆按声明顺序。
- `ReferenceBackend.Build` 使用setup.Seed（默认整数20261001转字符串），不使用fixture.GameSeed，也不应用正常Run入口已有的RngSourceCompat；随后真实StartCombat先洗牌。
- 两侧Apply只把起手移回抽牌堆、摆放声明的手牌/弃牌。FixtureSetup没有完整抽牌堆有序状态，所以牌的总集合一致并不代表下一回合抽牌一致。

上述是源码可直接确认的初始化不一致；末边界牌区首差也实际复现。下一包应交付通用受控初始化与适用随机契约，而不是继续给每个回合Power特设过滤。受控机制可以预登记完整初始有序状态，但只能在Start后的受控初始化阶段按同一声明分别构造，不读取另一侧结果、不在脚本执行期间改牌，也不把它算正常开局证据。新的profile/版本必须由负责人根据实际测量裁定；没有证明两侧随机兼容时原case仍保持unsupported。

正常Run的整幕预生成/UpFront消费差异是另一条生产依赖，涉及T08。不能认为修了受控夹具就自动解决rng-initial-shuffle的遭遇首差。

DSH曾尝试专用profile及过滤draw/hand事实，最终均撤销；这些分支耗费工作而没有增加可验成果。改进方法是开包先核对共同初始状态，确认首差属于初始化后交出最小复现与通用方案，避免在机制case里反复改比较口径。此次已产生的有效成果保留，不让下一执行者重做Bash/易伤实现。

## 并行拆分的可行边界

以下是本轮建议；优先级、就绪状态和派发只维护主清单。最多先开三条互不重叠的完整机制线，内容线按已有机制就绪程度补充。

| 工作包 | 主责与主要修改范围 | 完整可验成果 | 依赖/并行条件 |
| --- | --- | --- | --- |
| 药水获得与容量闭环 | Codex/A；base的正式药水加载、共享获得/使用过程、RewardWiring、独立检查与指南 | 源药水池生成真实实例；领取、满栏替换/取消、跳过、目标使用、消耗及永久栏位；阶段和重复操作正确；正常公开路线领取/使用 | T05当前TakeRewardPotion仍直接Fail；关键共享机制由Codex亲自实现。首包范围先Ironclad/共享必要池，完整池差距明示，禁止占位缩池。C消费已交接口后接收尾 |
| 受控初始化/随机对拍闭环 | B；tools/differential的FixtureSetup、两侧适配器、suite、必要rng-oracle及独立audit | 同一预登记完整初始状态/seed与来源；两侧牌序和适用流读数；原回合case原地复测；保留17分母及所有事实；最小负例 | 可与药水包同时做。先由负责人明确受控初始化契约，tools不改变生产或参考玩法。正常Run缺口回T08/A，不用补抽绕过 |
| 公开可见性与诊断出口 | C；net投影/契约/客户端检查；需base声明修正时由A单独协调对应MapRoomWiring字段 | 普通观察只含地图事实，MapDraws随机重放材料留诊断；C#/Python/schema一致；现有合法选择与重复提交语义不变 | 当前真实药水尚未交付只阻塞药水轨迹，不阻塞这项。公开可见性从规则声明派生，net不新增按游戏字段名硬过滤；base声明变更与A协调后再实施 |
| 已就绪卡牌内容（条件就绪） | 单独内容作者；预分配卡DSL与该族独立检查文件 | 完整机制族的普通/直接升级/实际升级行为，加载与源判别；无需修改公共C# | 不重新全扫577。使用现有分层记录和实际注册结果，仅处理已解阻族；新Power/历史/生成语义仍交Codex。不能为了多开一条线凑牌或让作者自行扩IR |

药水包和受控对拍包最能直接推进M01收口；公开可见性是独立可推进事项，不必等全量药水完成。第四条内容线若没有完整就绪机制族则不启动。每线一个机制包、独立.NET artifacts-path；主清单/公共Program/构建身份/公开golden由负责人单写，里程碑集成验证在批次末串行执行。

## 后续推进顺序

先补上述两条M01关键路径及独立公开边界，再接真实奖励/永久写回双侧和公开药水轨迹。M01仍需T01/T02/T03/T04/T05原合同证据，不以第一幕单路线成功关闭。

随后A按T08整幕初始化与源前置随机→T09商店/古代人/事件及事件战斗返回→T10幕转换/完整Ironclad推进；B/C随每个真实机制接线，不等全卡齐备。T06新共享机制依据实际被卡住的内容族选取，旧grantPower名字链接是维护性缺口，可在Power下一包一并处理。五角色特殊资源、完整577卡与最终120对仍属后继范围。AI继续暂缓，core/参考冻结不变。

本轮没有修改生产代码、派发新会话、运行训练、重跑Engine/Slice/net全量或正常Boss轨迹。本轮维护完成后，接续按主清单认领，不恢复已结束DSH会话。

文档检查扫描366份Markdown、0问题；git diff --check退出0，新文档空白检查0问题。
