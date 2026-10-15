# 开发和修正base-spire规则

文档类型：操作指南（How-to）。面向接续base生产包的开发者，包括小模型。目标是只读本机制所需代码，得到快速且有区分力的反馈，再完成正式验收。任务认领和排期只在[主清单](../../review/tofix/README.md)，机制与源时序要求查[base实施编排](base-spire-implementation-plan.md)。

第一次基于base新增东西，跟着[新增卡牌教程](first-base-card.md)或[新增房间动作教程](first-base-room.md)从空目录做起；先理解目录分工时读[base组织结构](base-architecture.md)。本指南服务已有明确修改目标的开发者，不要求把所有章节作为开工前置阅读。

房间包直接进入[房间操作指南](how-to-add-a-room.md)和[Run/房间接口参考](room-compile-reference.md)：先运行小例子，再读取目标房间与派单指定的源方法。公开客户端用[公开运行教程](../net-the-spire/how-to-run-public-game.md)。两条路径均附实际源码调用关系，无需先通读公共组包器或Host。

准备写金币/生命更新、卡牌创建/升级、牌堆移动或药水入栏前，先查[公共能力入口](room-compile-reference.md#按操作查找已有公共能力)，确认已有方法是否覆盖本次语义。覆盖就直接组合；缺失则给对应机制补完整操作和契约，迁移实际调用方。内容作者只写本内容独有的选择条件、数值和效果顺序；共享能力的适用边界不能省略。

## 按修改类型定位文件

新增共享行为先核对对应机制规格的输入、状态归属、hook/随机顺序、失败与公开结果，再实现领域操作及可判别案例。组合已有能力直接引用现有契约；不为每张内容建立一份规格或镜像检查。普通事件使用`ChoiceEventModule`和`Domain/Run/RunResources`，当前可用边界见[事件领域契约](event-domain/spec.md)，实际新增步骤见[房间指南](how-to-add-a-room.md#新增一个具体事件已有能力)。

先写出“实际行为→预期行为”及源位置，再按下表定位；不要从头读取整个组包器。

| 需要改什么 | 先读/修改 | 何时继续进入组包器 |
| --- | --- | --- |
| 已有效果的数值、目标、普通/升级、组合顺序 | `BaseSpire/ContentSource/*.spire`；相应行为案例 | 组合本身不需要 |
| 卡牌语法、引用、条件/重复结构 | `Slice/SpireCardSource.cs`、`Slice/Effects/SourceEffectPrograms.cs` | 新状态/过程绑定才需要 |
| 类型化效果及IR生成 | `Slice/Effects/CardEffectProgram.cs`、`CardEffectCompiler.cs` | 需要新的共享结算过程才需要 |
| 动态数值/条件读已绑定状态 | `Slice/Effects/CardEffectExpressions.cs` | 新增或尚未绑定的字段/集合才需要 `CardEffectStateBindings` 接线 |
| 付款/X、一次性牌规则 | `Slice/Orchestration/CardPaymentRules.cs` | 新依赖才进入付款接线 |
| 目标/出牌次数/效果分派/结果区 | `CardPlayContext`、`CardPlayPipeline`、`CardEffectDispatcher`、`CardResultRules` | 只调整对应能力绑定；外层动作保留末尾结算 |
| 移动/消耗/克隆/变形、抽牌/自动出牌/生成 | `Slice/Orchestration/` 的对应命令模块 | 新命令才增加 `Compilation/CardCompilation.CompileEffects` 绑定 |
| 历史/重置/具名hook | `CardPlayHistory`、`CombatLifecycle`、`CardEventDispatch` | 新字段声明进 `Compilation/CombatSchema`，寿命仍由对应生命周期模块拥有 |
| 胜负和长期提交 | `Slice/Compilation/SettlementCompilation.cs` 的 `BattleEndTail`/`Settlement` | 新依赖组或模块顺序变化才修改根组装 |
| 能力、敌人新机制 | 对应 `SpirePowerSource` / `SpireEnemySource`，再进入 `Compilation/PowerCompilation` / `EnemyCompilation` | 新依赖组才进入 `CombatPackageComposer` |
| 药水效果与栏位 | `ContentSource/potions.spire`、`SpirePotionSource`、`Effects/PotionEffectSource`、`PotionInventoryRules` | 正式效果复用逐效果程序；新状态绑定才进根接线 |
| 地图/房间/奖励 | `Rooms/MapRoomEntry`、`Rooms/EventRoomCatalog`、目标房间模块、`RewardWiring` | 房型组合进入处理器；事件分别实现`IEventRoomModule`；`Compilation/RoomCompilation`只组合服务；奖励声明进`RewardCompilation` |

公共编译接线按[模块参考](package-compilation-reference.md)定位：新增玩家字段先改`CombatSchema.Player`及`PlayerBindings`，再修改实际消费者；根入口已经传入同一分组时无需逐字段补参数。

在仓库根目录用 `rg -n '过程名或原语名' mod-the-spire/base-spire` 找入口。确认 `.spire` 接受、领域降级、包注册和行为各是哪一阶段；组包失败可能是奖励池依赖，不能把它归因于正在修改的卡牌。

## 新增共享状态字段

本步骤适用于已有语义依据、确实需要新Engine状态的机制。组合已有卡牌效果无需执行本步骤。开始前按主清单认领，确定字段的初值、范围、可见性和寿命，再改声明与消费者。

1. **选择声明者。** 玩家、敌人、Power或牌实例字段在`Compilation/CombatSchema`的对应方法中声明；Run字段在`Compilation/RunSchema.RunState`声明；本次房间访问字段由房间叶声明。DSL中的卡牌实例变量由`RewardCompilation.Rewards`按原顺序补入，内容作者不重复手写字段。不要用C#对象保存一份对应运行状态。
2. **返回所需符号。** 共享字段加入`CompilationBindings.cs`中对应的记录，并补声明方法的返回值。例如玩家字段加入`PlayerBindings`；消费者已接收该分组时，可直接读取新增成员。
3. **安排初值与重置。** 玩家创建初值查`NewRunCompilation.NewRun`；回合事实复用`CombatLifecycle.BeginTurn`及`TurnCompilation.PlayerTurn`；战斗计数复用`CombatLifecycle.EndBattle`及`BattleRoomCompilation.RoomTransition`。只修改本字段寿命涉及的位置；长期Run字段不随回合或换房清零。
4. **接入实际消费者。** 规则写入进目标领域模块；效果查询通过`CardCompilation.EffectState`接到`CardEffectStateBindings`。若消费方法还没有该符号分组，在方法参数及根组装的对应调用增加它；已有分组的新增成员不需要在根入口逐字段传递。
5. **核对行为。** 用能区分初值、更新和重置时机的现有局部检查获得反馈，按根约定完成受影响门槛。字段或IR变化可能改变规则身份，需核对真实内容加载和相同配置的公开行为，不能把构建成功当作规则验收。

**编译辅助元数据的例外。** 卡实例的隐藏 `SourceOrderKey`（源 `ModelId.Entry@升级等级`）由内容定义推出、不来自运行态，但每个创建/复制/升级路径都必须写出它，否则重洗会读到空值。机制、入口与判别检查见[重洗的源预排序](control-flow-modules.md#重洗的源预排序stableshuffle)。

声明句柄和编译程序体的先后关系由`CombatPackageComposer`维护。新增共享过程时，沿用对应领域的`Xxx`/`XxxBody`阶段，把被调用句柄先声明，再编译引用它的程序体；不要让回调在依赖尚未声明时运行。准确入口见[编译模块参考](package-compilation-reference.md)。

## 补卡时限制读取上下文

先读本指南、DSL作者约定、本机制的一张已验证卡和对应原版源类。组合已有原语只改内容与判别案例；不要为核对一张牌读取整个 `CombatSlicePackage`。Power按注册定义引用，跨文件无需重复声明。`rules` 中的布尔机制写 `true`，幅度来自Power层数；只有 `extraAttackPlays` / `hpLossCap` 接受1～99的固定整数。新机制先交给共享模块主责，不能用内容ID分支或空 `play` 伪装支持。

同一角色池的声明顺序、单人资格和生成资格需要源清单。`generateCards.pool.character = IroncladCardPool` 会严格解析完整85项源池，再过滤类型及 `canGenerateInCombat`；其余角色池尚未接入。解析和注册不能代替生成资格或Power时序的行为验证。

完整公共轨迹用下列命令，必须先构建solution：

```powershell
python -X utf8 tools/base-spire-dev/act1_smoke.py --output .audit/act1-public-smoke
```

夹具固定正常开局seed、公开构建身份和实际发布候选。脚本逐步核对候选仍合法，不注入状态；宝箱开箱后合法跳过遗物，药水行为以夹具的必需动作断言为准。源池不缩减，未实现遗物领取仍明确失败。第一幕完成是 `ActCompleted=true` / `MapPhase=6`，不是整局Victory；下一幕入口在当前开发范围中明确失败。内容或接口改变后，先审阅新行为再重录夹具，不能删身份检查让旧轨迹假通过。

## 补充药水时只读本机制

先查[药水生成与栏位契约](potion-inventory/spec.md)和该瓶的原版类。`ContentSource/potions.spire` 同时保存完整源池顺序、元数据与已支持的 `use`；实例/容量/奖励编排不用逐瓶修改。将 `unsupportedUse` 换成真实程序后，增加本瓶基础效果和时序判别检查，并更新本组已支持数量。原版池数量和源顺序保持不变；构建身份变化由负责人集中迁移公开夹具。元数据可以生成和领取，不代表使用效果已实现。

```text
potion ExamplePotion {
  rarity = common
  usage = combatOnly
  target = enemy
  source = "本mod的规则依据"
  unpowered = true
  use = { attack = { damage = 20 target = context.target } }
}
```

该片段放在内容所属的 `package` 中，ID 按该包命名空间生成；不在原版池里添加示例瓶。当前绑定允许字面量伤害、格挡、能量、抽牌、HP变化和已注册Power，及整手牌升级；不允许依赖“正在打出的牌”实例的变量/费用/复制等机制。Power通过已加载链接表引用，跨文件不重复声明。敌人目标由程序的 `NeedsSelectedTarget` 决定，只提问一次；`target = allEnemies` 的群体效果不用选择目标。`unpowered` 是整瓶效果的修正门控，Fire和Block声明为true。

领取/丢弃/固定空槽由 `PotionInventoryRules` 维护。满栏时先显式丢弃，再领取；取消就是不提交丢弃，奖励保留。编写移动过程时，先把 `First(collection)` 存进局部槽，再移出集合；IR表达式在每条指令执行时重新求值，不能把C#的Expr变量当作已保存实例。`Requires` 每次调用会覆盖守卫，多个条件必须显式 `And`。

快速反馈使用 `dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --potion-flow-only`。完整门槛按根AGENTS执行；局部案例不能代替正常公开轨迹或原版双侧。

## 先用小案例获得反馈

1. **核对范围并认领。** 看git状态、目标目录AGENTS、主清单本项和对应源规则；记录目录、完整机制包、下一可验成果。已有奖励或其他未提交实现保留，不重置工作区。
2. **写独立预期。** 例如分开3/7两击与两次3；同时给正常、升级及受影响顺序的对照。新增原版规则以 `sts2/` 对应版本源码和实际游戏为依据，不能从当前base输出反推期望。
3. **优先用外部卡牌探针。** 首次构建 `dotnet build tools/base-spire-dev/BaseSpire.Dev.csproj`，随后按[modding步骤](modding-guide.md)从独立文件 `check` / `play`。这是受控内容组合入口；生产内容仍写入 `ContentSource`，同ID原版修正先在独立命名空间复现，最终再检验原版实际加载。
4. **修复最小语义链。** 组合已有原语时只改内容和案例；新查询进表达式编译；新叶效果进领域模型/编译器。不要把每条效果的参数写回卡级显示 `Amount`，也不要通过旧 `Steps` 绕过正式 `NormalProgram` / `UpgradedProgram`。
5. **运行局部语义检查。** C#变更后重新构建，卡牌效果机制可运行：

   ```powershell
   dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --effects-only
   ```

   该入口覆盖逐效果顺序、引用、X、随机目标和hook等判别案例；需要新机制时补对应案例。它不检查地图和全部内容。

   当前卡牌补齐族使用独立入口，不受默认身份/地图 gate 挡住；第一次构建后直接执行 DLL：

   ```powershell
   dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests/BaseSpire.Slice.Tests.csproj
   dotnet mod-the-spire/base-spire/BaseSpire.Slice.Tests/bin/Debug/net9.0/BaseSpire.Slice.Tests.dll --card-completion-only
   ```

   此入口实际运行 `SliceFrontendIroncladCompletionChecks`。增加本族判别后必须确认该判别确实执行；日志停在前置 gate 的旧检查数不能算新行为通过。内容变更后先重新构建，不能使用旧 DLL 证明新机制。
   编排/牌区机制使用下列入口，默认完整入口也执行同一组案例：

   ```powershell
   dotnet run --project mod-the-spire/base-spire/BaseSpire.Slice.Tests -- --orchestration-only
   ```

   检查词法隔离、事件计数、同实例重放、一次付款多次执行、自动出牌批次、X、末杀余效、抽牌终止与变形。局部入口排除身份/地图门槛，完整门槛仍需执行。

6. **完成集成门槛。** 每个包按最终改动串行执行下列命令；不要多个会话同时写同一obj/bin：

   ```powershell
   dotnet build AutoTheSpire.sln
   dotnet run --no-build --project core-the-spire/Core.TheSpire.Engine.Tests
   dotnet run --no-build --project mod-the-spire/base-spire/BaseSpire.Slice.Tests
   ```

   涉及公开入口/共享规则身份时再跑 `dotnet run --no-build --project net-the-spire/Net.TheSpire.Tests`，族/整局交付按主清单追加实际独立对拍与正常Run。`--combat-only` 可帮助隔离已有地图失败，但必须报告排除范围，不能替代完整Slice。
7. **交完整证据。** 记录命令/退出码、内容源、构建/Profile/seed、声明/解析/加载/行为、首差和未验证范围；实际加载身份有变化时交消费者迁移依据。长日志放 `.audit` / artifacts，回写任务行并按[交接约定](../post-core-agent-assignments.md#0-任务与文档管理规则)提交一份机制交接。

## 让新增内容独立于全包大小

组合已有机制时，只读本卡源类、同族DSL与族检查。跨角色内容运行 `--remaining-card-families-only`；定位单张已登记案例可追加 `--card strike-silent`，只跑该牌普通/直接升级/真实升级三个案例，不存在的ID会失败，不能以零检查宣称通过。公共作者边界运行 `--author-contracts-only`。按一次完整机制族构建和检查；在程序退出后再构建下一版，避免Windows的DLL/可执行文件锁。新族还须核对保留、虚无、固有等跨回合语义，不能只验证打出一刻。

行为检查从正式模型选定义，但用显式受控 `Deck`；例如PerfectedStrike用明确的六张Strike标签牌验证跨牌区计数。不要依赖“每个已注册定义各一份”的全包牌组，再写死总张数；新增另一角色卡会使这种旧检查失败。全量加载检查和行为夹具各有用途。

Power默认打出结果为None，统一结果模块移除战斗牌区归属；无需在每张能力牌加exhaust，也不会因此触发onExhaust。真实无目标牌用 `target = { selectNone = {} }`；原生无效果允许 `play = {}`，仍执行付款、历史与结果区规则。未实现效果必须报缺口，不能借空块省略。无目标牌的效果、嵌套分支不得读所选目标。

正式作者诊断和SinglePlayer使用同一个 `SpirePowerSource.PowerDeclarationResources()` 注册资源入口，包含与敌人共文件的Power。优先链接已有注册定义；找不到Dexterity等能力时先查资源入口，不重复声明、不加名称白名单。Power结果、选择交互、下回合资源等公共机制交模块主责一次实现，卡作者继续处理其他已就绪族。

## 用通用选牌编写卡牌

先看[正式例子](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/selection-cards.spire)与[选牌局部检查](../../../mod-the-spire/base-spire/BaseSpire.Slice.Tests/CardSelectionChecks.cs)。抽牌后弃一张写 `draw = { amount = 3 } chooseCard = { from = hand to = discard autoSelect = true }`；升级后弃两张加 `count = 2`；从弃牌堆取回写 `chooseCard = { from = discard to = hand autoSelect = true }`。卡作者不修改组包器、Engine或net候选映射。

移动短写展开为 `SelectCardsEffect` 与选中牌移动原语；[选牌过程](../../../mod-the-spire/base-spire/BaseSpire/Slice/Orchestration/CardSelectionProcedures.cs)只负责实例选择和恢复子程序。数量是0..9999的字面量，默认1；筛选为省略、`attack`、`nonAttack`或`upgradeable`。去抽牌堆顶加 `destinationPosition = top`。新原版卡按源CardSelectCmd写 `autoSelect = true`：空集跳过、候选数不超过所需数量时自动选全部，其他情况一次决策后恢复。未写时保留手动确认。

需要在选中后做多个操作时，省略`to`，写`onSelected`。以下片段适用于normal/upgraded的play，使用同一个选中实例依次升级、复制、置顶，再沿原程序抽牌：

```text
chooseCard = {
    from = discard
    filter = upgradeable
    prompt = "Upgrade and recover"
    onSelected = {
        upgradeSelectedCard = {}
        cloneSelectedCard = { pile = discard }
        moveSelectedCard = { from = discard to = draw destinationPosition = top }
    }
}
draw = { amount = 1 }
```

`cloneSelectedCard`当前只支持discard，克隆读取前一步升级后的字段；`cloneCard`克隆正在打出的牌，两者来源不同。`randomMoveCard`的`onSelected`同样可组合子程序，普通随机牌选择保留卡随机流和实例排序；特殊Thrash伤害预览仍有独立源契约。`upgradeCards { pile = hand }`短写展开成遍历与升级；正式Armaments升级版展示显式组合。Feed展示攻击后用`targetAlive`分支与`gainMaxHp`组合；原onFatal短写仍兼容。范围与编译边界见[卡牌原语契约](card-primitives/spec.md)。

手动弃牌调用被弃实例的 `hooks = { onDiscard = { ... } }`，不要求它能正常打出；出牌结果和回合末丢牌不触发这个hook。该hook不能发起新的选牌。跨牌/Power弃牌监听、弃牌历史计数及Sly尚须各自机制；不要因此提前编入需要它们的卡。取回至已满手牌时，牌保留在弃牌堆。

## 批量开发时缩短反馈

用户2026-10-10授权本轮优先可用成果：共享机制先跑能区分错误实现的局部检查，完成一次必要构建和实际加载；简单常量卡复用单牌族案例，不新增镜像测试。重复全量Engine/Slice/net、逐卡完整整局和独立对拍延到内容批次或阶段交付，记录未执行项。公共选择改动仍须检查真实暂停/恢复和现有选择消费者；不降低断言、不缩卡池、不把声明或自报当运行证据。

相同构建连续检查用 `dotnet <已构建的测试DLL> --card-selection-only`，免去重复构建。并发任务应写不同源文件；例如共享选牌示例使用独立 `selection-cards.spire`，其他作者继续角色文件。公共身份固定夹具在负责人集成批次时根据实际工厂迁移，内容作者无需为每张卡更新身份。

## 与共享机制开发并发补卡

先由负责人划分文件：机制主责修改公共C#、Program入口及独立机制示例，内容作者只修改派单中的卡源和族案例。主清单在并发窗口由负责人单写，内容作者把交付与缺依赖写入固定交接。可组合已有能力的卡无需等待公共机制开发；新机制在负责人交付契约后再使用。

每个会话使用自己的构建输出，SDK会将项目及引用项目的bin/obj都放入指定目录。以下命令从仓库根运行，负责人使用 `owner-next-turn`，DSH使用 `dsh-ready`：

```powershell
dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/owner-next-turn
dotnet .artifacts/parallel/owner-next-turn/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --next-turn-energy-only

dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/dsh-ready
dotnet .artifacts/parallel/dsh-ready/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --remaining-card-families-only --card leg-sweep
```

替换内容作者的卡ID即可复用第二条检查；一轮源修改只构建一次，随后连续执行该目录的DLL。不要用默认 `dotnet run` 重建到公共输出。同一会话运行DLL期间先完成检查再构建下一版，仍避免覆盖正在使用的程序集。

输出隔离不隔离共享源码。局部构建可能嵌入另一方未交接的内容，因此用显式牌组判别本机制，记录实际构建与未验证范围；若其他内容有语法错误，报告具体文件交其主责修复。身份golden、公开夹具、默认solution输出及整局验收在批次集成时由负责人统一处理。独立输出通过只证明该构建与局部范围，不能直接更新公开环境身份。

## 编写下一回合能量收益

先看[Outmaneuver正式示例](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/next-turn-cards.spire)与[注册Power](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/next-turn-powers.spire)。原版卡直接写 `applyPower = { power = EnergyNextTurnPower stacks = 2 target = context.owner }`；需要自己的Power时注册 `rules = { energyAfterResetOnce = true }`，引用自己的命名空间，无需新增具体ID分支。

卡牌支付本次费用、施加层数、进入普通结果区；下一回合重置能量后按累积层数获得能量，再移除Power实例，之后才执行抽牌前机制和抽牌。不要把此行为写成即时 `gainEnergy` 或修改MaxEnergy。禁止获得能量仍会消耗该一次性Power。普通与升级幅度各自在完整版本中声明；Outmaneuver普通2/升级3、cost1，其实际来源是EventCardPool，SilentCardPool只是原版视觉池。

局部入口 `--next-turn-energy-only` 实际加载正式卡与Power，并判别普通/直接升级/真实Armaments升级、叠加、禁止收益、最大能量组合及自定义命名空间；编排回归使用 `--orchestration-only`。跨实例弃牌监听和其他角色资源仍按各自依赖包处理。

## 保持这些语义边界

正式卡的领域程序是执行依据；显示元数据和旧诊断摘要不决定正式效果数值。`CardEffectExpressions` 在每条效果位置生成实时查询；付款X与实时能量不同。所选目标由共用出牌上下文绑定，随机目标另有槽位。hook内支持的效果范围受前端校验，不可为了绕过选择限制把暂停偷偷移入C#回调。

共用出牌管线执行效果、适用hook及牌区清理；动作外层执行胜负和永久提交。叶效果只可请求嵌套出牌，不获得收尾/终局能力。长期实例与战斗副本来源必须明确，临时生成牌不能污染长期牌组。新公开动作或字段应通过规则声明/投影产生；不要同时在net增加逐牌注册表。

未迁移的升级选择/部分生成等 `LegacyCardEffect` 是显式适配边界，不是新数字效果的默认入口。药水、Power和敌人表示仍有独立兼容路径。遇到通用core缺口，提供最小复现和受阻case交负责人，不自行解冻。

## 用一个短工作包限定开发上下文

每个机制包采用独立上下文。负责人先确认跨模块接缝，直接给同版本源关键方法、可调用接口签名、状态归属/随机流、最小可执行用例和一个局部命令；只写“复用牌组/药水/遗物接口”而让执行者逐个反查实现，不算接口已经交付。执行者核对主清单本项与相关规范，先搜符号再读涉及片段，不把整张清单、全部设计或历史长日志作为常驻输入。复用已确认的源时序，不在每个机制重推整个调用链。派单按以下模板填写：

```text
任务ID/机制：主清单本项；一个完整机制族
源依据：同版本源码路径和关键方法；预期输入/结果
已交付：复用的原语/过程/案例；不要重做的部分
接缝：准确签名/必要字段/状态归属/随机流；最小可编译调用
允许写：内容文件、指定前端/编译器/测试；公共文件单一主责
先复现：最小案例命令；正常/升级/顺序对照
实现边界：无需改组包器的内容组合；新增能力要交完整语义链
验收：局部案例、实际加载、必要串行门槛；受控与正常Run分列
交接：唯一路径、任务/派单/会话身份、命令/证据/首差/下一成果
```

如果为了确定金币、长期牌组、库存或房间关闭的用法，仍需反查整个组包器，先列缺少的接口交负责人确认；房间分文件不代表接缝已自足。完成包保留最小接手路径和实际执行证据，不要求新增阅读量配额。不要因完整门槛有无关失败反复重跑，也不要通过删卡池、造空效果、改参考或更新golden消除首差。设计提案与已验证能力分开写。

接口设计原则见[可维护规则集指南](maintainable-rule-sets.md)，实际编译结构见[逐效果设计](effect-ir/plan.md)。共享操作与出牌流程的实际模块和未迁移范围见[控制流程与上下文边界](control-flow-modules.md)。当前 Ironclad 卡资源补齐的方法与有限机制扩展契约见[卡牌工作包](ironclad-card-completion.md)；实际认领仍查主清单。


## 编写下一回合抽牌收益

先读取[Predator正式示例](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/next-turn-cards.spire)与[注册Power](../../../mod-the-spire/base-spire/BaseSpire/ContentSource/next-turn-powers.spire)。卡牌写 `applyPower = { power = DrawCardsNextTurnPower stacks = 2 target = context.owner }`；自定义Power注册 `rules = { handDrawNextTurnOnce = true }`，并让卡读器引用实际注册的名字/命名空间。无需在net或公共编排按卡名增加分支。

这个Power修改下一回合的基础手牌抽取数量，按现有Draw过程处理NoDraw、手牌上限与洗牌，随后消费。回合开始期间才施加的Power不参与当次收益；开始时已有的Power若随后叠层，收益使用当前层数。作者无需自行写快照/消费钩子。

局部反馈命令（仓库根目录；与DSH对拍使用不同输出）：

```powershell
dotnet build mod-the-spire/base-spire/BaseSpire.Slice.Tests --artifacts-path F:/Scylier-Project/active/auto-the-spire/.artifacts/parallel/owner-next-turn-draw
dotnet .artifacts/parallel/owner-next-turn-draw/bin/BaseSpire.Slice.Tests/debug/BaseSpire.Slice.Tests.dll --next-turn-draw-only
```

该入口验证Predator三种普通/升级创建路径、叠加、容量、NoDraw、开始期间新建/叠层、自定义命名空间和负层数回归。它不代表其他延迟Power、完整角色或原版独立对拍已支持。旧Power `grantPower` 文本钩子的自定义名字链接仍受静态规范表限制；本包用受控作者模型构造该时序反例，不宣称该旧钩子的跨文件链接已经完成。
