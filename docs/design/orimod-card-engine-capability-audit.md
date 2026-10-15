# OriMod 1.0.0 卡牌引擎能力清单（源码审计）

范围：`reference/orimod/src/Ori/Cards/**`（131 个 `.cs`）+ `reference/orimod/src/Ori/CardPools/**`（4 个 `.cs`）。
目标：只报告这个 mod **需要什么引擎能力**，不做 DSL 能力对比。

数据来源：全部 131 个文件逐个人工阅读（`read` 工具），配合 `grep`/`pwsh` 统计。
统计口径：131 个文件中，**9 个是基类/基础设施**（`AbstractOriCard`、`AbstractOriVisualCard`、`AbstractOriConceptCard`、
`AbstractOriFieldCard`、`AbstractOriInternalCard`、`AbstractOriSpecialCard`、`OriCardTags`、`OriKeywords`、`OriPlacementHelper`），
**122 个是实际卡牌类**（根目录 95 + `Fields/` 7 + `Special/` 9 + `Special/Concepts/` 11）。

> 关于"卡牌需要非原版能力"的两把尺子（下文分别给出数字，避免歧义）：
> - **A. 卡牌侧能力**：卡牌自身的 `OnPlay`/`OnUpgrade`/`CanonicalVars`/钩子/覆写是否用到普通数据驱动卡牌难以表达的东西。
> - **B. 内容依赖**：卡牌是否引用了 mod 注册的模型（自定义卡池、自定义 Power、自定义 Tag/Keyword）。只要引用了 mod Power 就属于 B。

- 按 A 口径：**94 / 122** 张卡需要至少一项非原版卡牌侧能力。
- 按 B 口径：**122 / 122** 张卡（131 个文件中除 9 个基础设施文件外）都依赖 mod 注册内容；即使"最原版"的
  `StrikeOri` 也属于自定义卡池 `OriCardPool` 并把 `CardTag.Strike` 交给新池注册（`reference/orimod/src/Ori/Cards/StrikeOri.cs:14`、`AbstractOriCard.cs:7`）。

---

## 1. 基础设施 / 基类（9 个文件）

| card file | cost/type/rarity/target | 游戏操作 | 需要的非原版能力 |
|---|---|---|---|
| `Cards/AbstractOriCard.cs` | 抽象，透传 ctor | 无 | `[Pool(typeof(OriCardPool))]` 自定义卡池归属（`:7`）；继承 BaseLib `CustomCardModel` 链 |
| `Cards/AbstractOriVisualCard.cs` | 抽象 | 覆写 `AfterPreventingDeath` → `CardPileCmd.RemoveFromDeck(this)` + `CreatureCmd.Heal(creature, 8m)`（`:72-79`）；覆写 `Tags`/`CanonicalKeywords` 合并子类增量（`:39-59`）；`PortraitPath`/`CustomPortraitPath`/`BetaPortraitPath` 资源路径约定（`:61-65`）；`[SavedProperty] DormantTemperamentEnchantment`（`:21-33`） | 基类级死亡拦截钩子；把自定义 Tag/Keyword 注入基类 `Tags`/`Keywords`；卡牌上持久化 mod 状态（序列化）；自定义卡图路径约定 |
| `Cards/AbstractOriConceptCard.cs` | 抽象，`CardRarity.Event`，资产 Key 固定 `"Unknown"` | `sealed override OnPlay` → `Task.CompletedTask`（`:34-37`）；`CanBeGeneratedInCombat => false`；强制注入 `OriKeywords.ConceptCard` | `OnPlay` 被 `sealed` 成空实现——**卡牌行为完全在卡牌文件之外**，声明式效果列表无法表达；自定义 Keyword |
| `Cards/AbstractOriFieldCard.cs` | 抽象 | `VisualCardPool` 覆写为 `OriFieldCardPool`（`:12`）；`CanBeGeneratedByModifiers => false`；`Tags` 注入 `OriCardTags.Field`（`:21`） | 第二个自定义卡池；基类行为覆写 |
| `Cards/AbstractOriInternalCard.cs` | 抽象，`CardRarity.Token`，不进卡库 | `CanBeGeneratedInCombat => false`；`MaxUpgradeLevel => 0`（`:7-12`） | 隐藏内部卡（仅作 UI 选择代理），需要"不可获得、不可升级、不进卡库"这类基类级开关 |
| `Cards/AbstractOriSpecialCard.cs` | 抽象 | `VisualCardPool` 覆写为 `OriSpecialCardPool`（`:12`）；`Tags` 注入 `OriCardTags.Special`（`:21`） | 第三个自定义卡池 |
| `Cards/OriCardTags.cs` | 静态类 | 10 个 `[CustomEnum(null)]` 自定义 `CardTag`（`:8-36`） | 自定义卡牌 Tag（原版 `CardTag` 仅 `None/Strike/Defend/Minion/OstyAttack/Shiv`，见 `sts2/src/Core/Entities/Cards/CardTag.cs:15-47`）。这些 Tag 的**行为在卡牌之外**被消费：`NoAloftAutoFall`→`AloftSystem.ShouldAutoFall`（`Aloft/AloftSystem.cs:402`）；`MeleeAttack`/`RangedAttack`→各 Power 与遗物（`Powers/AimPower.cs:35`、`Powers/SeraphPower.cs:41`、`Powers/SwordDancePower.cs:20`、`Powers/GlimmeringPower.cs:36`、`Powers/AsterismPower.cs:25`、`Powers/ReflectionPower.cs:34`、`Powers/QuickFillingPower.cs:28`、`Relics/ShardAntiair.cs:38`、`Relics/ShardCatalyst.cs:84`、`Relics/ShardScattering.cs:30`）；`NonDecayWithDistance`→`Aloft/OriDistanceDecayDamageHook.cs:75`；`AttackUp/Down/Auto`→`Powers/AloftMomentumPower.cs:84`；`Placement`→`Cards/OriPlacementHelper.cs:16-18` |
| `Cards/OriKeywords.cs` | 静态类 | 27 个 `[CustomEnum("...")]` + `[KeywordProperties]` 自定义 `CardKeyword`（`:8-111`） | 自定义卡牌 Keyword（原版仅 `None/Exhaust/Ethereal/Innate/Unplayable/Retain/Sly/Eternal`，见 `sts2/src/Core/Entities/Cards/CardKeyword.cs:18-28`）；部分 Keyword（`Return/Reset/Momentum/Bash/Temperament/WeaponSwitch/SpiritEdgeOnly/SpiritArcOnly/*WeaponOnly/Judgment`）是**行为挂点**，语义由 mod 其它系统实现 |
| `Cards/OriPlacementHelper.cs` | 静态类 | `IsDirectPlacementCard`（Tag 判定，`:14-21`）；`ApplyPlacementAsync` 包 `OriPlacementIdentityAllocator.BeginDeterministicCardPlayScope`（`:23-29`） | 放置系统 + 确定性身份分配器；要求"卡牌打出的结算必须在一个确定性 scope 内" |

---

## 2. 根目录卡牌（`Cards/*.cs`，95 张）

| card file | cost/type/rarity/target | 游戏操作 | 需要的非原版能力 |
|---|---|---|---|
| `Ablution.cs` | 1 / Skill / Uncommon / AllEnemies | `OriAbilitySystem.AddCount/GetCount/SetCount(SpiritArc)`（`:27-33`）；循环 `SpiritArcAbilityCard.CreateForPlayer` + `OriAbilityAttackExecutor.ExecuteSpiritArcAgainstAllEnemiesAsync`（`:39-40`）；`OriAbilitySystem.NotifyAbilityUsedAsync`（`:45`） | 能力值系统（充能读写清零）、运行时构造 Ability 卡、mod 专用多段攻击执行器与"能力已使用"通知钩子 |
| `Absorbed.cs` | 0 / Skill / Common / Self | `PowerCmd.Apply<AimPower>(..., DynamicVars["AimPower"].BaseValue, ...)`（`:25`） | mod Power `AimPower` |
| `Adjudication.cs` | 1 / Attack / Uncommon / AnyEnemy | 读 `OriAbilitySystem.GetCount(SpiritEdge)` 并清零（`:48-51`）；**运行时改写** `DynamicVars.Damage.BaseValue += count*Increase`（`:53-54`）；`DamageCmd.Attack(...).Targeting`（`:56`）；覆写 `AfterDowngraded` 回补伤害（`:66-70`） | 能力充能读取；按充能计算的动态伤害；改写 canonical var；`AfterDowngraded` 基类回调 |
| `Aetheris.cs` | 0 / Skill / Rare / Self | `PileType.Hand.GetPile(Owner).Cards.Count`（`:32`）；`CardSelectCmd.FromHand(..., prefs 0..count)`（`:36`）；`CardCmd.Discard`（`:39`）；`OriAbilitySystem.AddCount(SpiritArc, n)`（`:40`）；`PowerCmd.Apply<AimPower>(n*AimGain)`（`:41`）；`EnergyCost.AddThisCombat(1)`（`:43`） | 手牌选择 UI；能力充能写入；**本战斗内动态改费** |
| `Afterimage.cs` | 0 / Skill / Uncommon / Self | `CreatureCmd.TriggerAnim(...,"Cast",CastAnimDelay)`（`:23`）；`PowerCmd.Apply<AfterimagePower>(..., DamageVar)`（`:24`） | mod Power；施法动画 |
| `AncientForce.cs` | 1 / Skill / Basic / Self | `PowerCmd.Apply<TempStrengthPower>`（`:25`）；升级 `AddKeyword(Retain)`（`:30`） | mod Power `TempStrengthPower`（不在 `sts2` 中，见统计命令 4） |
| `Antigravity.cs` | 1 / Skill / Rare / Self | 判定 `Owner.Creature.GetPower<AntigravityPower>() == null` 后 `PowerCmd.Apply` 或 `PowerCmd.Remove<AntigravityPower>`（`:27-34`） | mod Power 的存在性分支（切换语义） |
| `Brilliance.cs` | 4 / Attack / Rare / AllEnemies | 快照手牌→`DamageCmd.Attack(...).TargetingAllOpponents`（`:34`）→逐张 `CardCmd.Exhaust`（`:36-39`）→按 `MaxHp*手牌数*HpPercent/100` 对每个敌人 `CreatureCmd.Damage(..., ValueProp.Unpowered, ...)`（`:44-52`） | 引擎侧 `Creature.MaxHp` 读值 + 自定义百分比公式；手牌快照后逐张放逐 |
| `Burst.cs` | 1 / Attack / Common / AnyEnemy | `DamageCmd.Attack(...).WithHitFx("vfx/vfx_attack_slash")`（`:27-29`）；`OriAbilitySystem.GetState(Owner)`、`OriAbilityFamily.IsMeleeWeapon`、`state.Refresh(activeWeapon)`（`:30-38`） | 近战武器判定 + 武器刷新；命中 VFX 资源名 |
| `CardWheel.cs` | 0 / Skill / Uncommon / Self | `PowerCmd.Apply<CardWheelPower>`（`:22`）；`CardPileCmd.Draw`（`:23`） | mod Power |
| `ChargeJump.cs` | 1 / Skill / Common / Self | `IsPlayable`/`ShouldGlowRedInternal` 读 `AloftSystem.GetDistanceToAdjustedGround`（`:24-26`）；`AloftSystem.ChangeAsync(..., Common)`（`:35`）；`CreatureCmd.GainBlock`（`:36`） | 高度状态读取驱动可打出性；aloft 变更 |
| `Clamber.cs` | 1 / Skill / Common / Self | `CreatureCmd.GainBlock`（`:31`）；`PowerCmd.Apply<ClamberPower>(Turns)`（`:32`） | mod Power |
| `CollectionEnergy.cs` | 0 / Skill / Common / Self | 读 `AloftSystem.GetAloft`（`:28`）；`Owner.RunState.Rng.CombatCardSelection.NextInt`（`:31`）；`OriPlacementHelper.ApplyPlacementAsync` + `OriPlacementStateSystem.ApplyCollectionEnergyAsync(height,energyGain)`（`:33`） | 高度读取 + 专用 RNG 流 + **放置物生成**（不是"获得能量"这种立即效果） |
| `CongealingSoul.cs` | 2 / Attack / Common / AnyEnemy | `DamageCmd.Attack(...).WithHitCount(Hits).Targeting(...)`（`:32-35`） | 仅多段伤害（+ `RangedAttack` Tag，见 `:16`） |
| `DefendOri.cs` | 1 / Skill / Basic / Self | `CreatureCmd.GainBlock`（`:26`） | 仅格挡（+ 原版 `CardTag.Defend`，`:15`） |
| `Deflection.cs` | 1 / Skill / Common / Self | `CanonicalKeywords` 注入 `OriKeywords.MeleeWeaponOnly`（`:23`）；`IsPlayable`/`ShouldGlowRedInternal` 依赖 `OriAbilitySystem.CanUse(SpiritEdge/LightHammer)`（`:75-79`）；满足则 `GainBlock` + `PowerCmd.Apply<ReflectPower>`（`:61-64`） | 自定义 Keyword + 能力可用性判定 + mod Power |
| `Departure.cs` | 1 / Attack / Common / AnyEnemy | 读 `AloftSystem.GetAloft` 与 `EnemyAloftSystem.GetHeight(target)` 求差（`:37-40`）；`AloftSystem.ChangeAsync(..., Oriented)`（`:43`）；`DamageCmd.Attack`（`:45`） | 玩家高度 + **敌人高度**双状态；有向位移 |
| `Dignified.cs` | 2 / Power / Rare / Self | `CreatureCmd.TriggerAnim`（`:33`）；`PowerCmd.Apply<DignifiedPower>(Growth)`（`:34`） | mod Power `DignifiedPower`（它的成长逻辑在 Power 内，卡牌只给初值） |
| `Dive.cs` | 0 / Skill / Common / Self | `AloftSystem.IsSupportedByGround(Owner)` 分支（`:30`）：成立则 `CardPileCmd.Draw`（`:32`），否则 `AloftSystem.ChangeAsync(-Aloft, ActiveFall)`（`:36`） | 高度/支撑状态分支 |
| `ElfForm.cs` | 3 / Power / Rare / Self | `OriAbilitySystem.RefreshAll(Owner)`（`:26`）；`PowerCmd.Apply<ElfFormPower>`（`:27`） | 全体能力刷新 |
| `EmbraceTheLight.cs` | 9 / Power / Rare / Self | `MultiplayerConstraint => MultiplayerOnly`（`:18`）；`IsPlayable` 依赖存活队友（`:66-73`）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplyAncientLightFieldAsync`（`:56`）；`CreatureCmd.LoseMaxHp(...,isFromCard:true)`（`:57`）；`OriDeathSystem.KillUnpreventablyAsync`（`:58`） | 多人限定；放置物；**不可预防的强制死亡**（越过全部死亡拦截/复活类 Power）；最大生命削减 |
| `EnergyCell.cs` | 1 / Skill / Uncommon / Self | 读 `Owner.PlayerCombatState.Energy` 与 `.MaxEnergy`（`:27-28`）；`PlayerCmd.GainEnergy(max-energy)`（`:31`） | 读能量池状态并按其差值补满 |
| `Escape.cs` | 1 / Skill / Common / AnyEnemy | 读 `AloftSystem.GetAloft` + `EnemyAloftSystem.GetHeight(target)` 决策方向（`:35-37`）；`AloftSystem.ChangeAsync(Oriented)`（`:38`）；`CreatureCmd.GainBlock`（`:39`） | 玩家/敌人高度双状态、有向位移 |
| `EurekaMoment.cs` | 0 / Skill / Uncommon / Self | `CardPileCmd.Draw` 返回抽到的卡（`:35`），逐张按 `Type == CardType.Attack` 分支：`PowerCmd.Apply<TempStrengthPower>` 或 `PlayerCmd.GainEnergy`（`:36-45`） | 抽牌**返回值**参与逐张分支（非固定效果序列） |
| `EyeOfTheStorm.cs` | 1 / Skill / Common / Self | 读 `AloftSystem.GetAloft`（`:28`）+ 专用 RNG 取高度（`:29-31`）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplyEyeOfTheStormAsync(height,damage)`（`:33`） | 高度 + 放置物生成（伤害延迟结算） |
| `Farewell.cs` | 0 / Skill / Common / Self | 快照手牌 `CardCmd.Discard`（`:25-29`）；`CardPileCmd.Draw`（`:30`） | 仅弃手牌 + 抽牌 |
| `Feathers.cs` | 3 / Attack / Rare / RandomEnemy | `CalculatedDamageVar.WithMultiplier((card,_) => AloftSystem.GetAloft(card.Owner)/AloftStep)`（`:25-29`）；`DamageCmd.Attack(...).WithHitCount(6).TargetingRandomOpponents`（`:41-44`） | **伤害公式是 C# lambda 且读 aloft 状态**；随机多段目标 |
| `FieldCampfire.cs` | 1 / Power / Uncommon / Self | `OriPlacementHelper.ApplyPlacementAsync` + `ApplyCampfireFieldAsync`（`:34`） | 场地放置物；`MaxUpgradeLevel 0`；自定义 Keyword |
| `FlickerFlicker.cs` | 0 / Attack / Uncommon / AnyEnemy | 读玩家 aloft + 敌人高度取方向（`:40-42`）；算出高度区间后**遍历敌人按高度过滤**逐个 `DamageCmd.Attack`（`:45-56`）；`PowerCmd.Apply<VulnerablePower>`（`:59`）；`AloftSystem.TeleportAsync`（`:63`） | 敌人高度区间筛选 + 传送；目标集合由状态计算得出 |
| `Fluorescence.cs` | 1 / Attack / Common / AllEnemies | `PowerCmd.Apply<FluorescencePower>(2)`（`:37`）；循环 `Hits` 次 `DamageCmd.Attack(...).TargetingAllOpponents`（`:38-42`） | mod Power + 固定次数的全体伤害循环 |
| `ForestEyes.cs` | 1 / Skill / Uncommon / Self | 无（仅 ctor，`:7-9`） | 无（**空卡**：声明了费用/类型/稀有度但没有任何效果与变量，疑似未完成或行为在别处） |
| `ForestHeart.cs` | 3 / Power / Rare / Self | `CreatureCmd.TriggerAnim`（`:24`）；`OriPlacementStateSystem.ApplyOrExtendForestHeartAsync(Turns)`（`:25`） | 直接调用放置系统（未经 `OriPlacementHelper`，即**非卡牌打出的 scope**），放置物"存在则延长"语义 |
| `ForestMemory.cs` | 1 / Skill / Uncommon / Self | `CardSelectCmd.FromHand(prefs, filter: 非 Retain)`（`:34`）；`CardCmd.ApplyKeyword(item, Retain)`（`:36`）；统计手中带 Retain 的卡数（`:38`）；`PowerCmd.Apply<TempStrengthPower>(n)`（`:41`） | 带 filter 的手牌选择 + 运行时给卡加 Keyword + 按手牌状态计数 |
| `ForestStrength.cs` | 1 / Power / Rare / Self | `CreatureCmd.TriggerAnim`（`:22`）；`PowerCmd.Apply<ForestStrengthPower>(35)`（`:23`） | mod Power |
| `ForestVoice.cs` | 1 / Skill / Uncommon / Self | `CardSelectCmd.FromHand(1)`（`:34`）；`ModelDb.Power<ForestVoicePower>().ToMutable()` + `SetSelectedCard`（`:37-38`）；`PowerCmd.Apply(powerInstance, ...)`（`:39`）；`CardPileCmd.Add(..., PileType.Exhaust)`（`:40`） | **运行时构造并注入 Power 实例、把选中的卡塞进 Power 内部状态**；存档语义复杂 |
| `Frozen.cs` | 1 / Attack / Common / AnyEnemy | `DamageCmd.Attack`（`:35`）；`PowerCmd.Apply<FrozenPower>(strengthLoss)` 对**目标和自身各一次**（`:39-40`） | mod Power `FrozenPower`（近战自重减益） |
| `Glimmering.cs` | 1 / Power / Uncommon / Self | `CreatureCmd.TriggerAnim`（`:25`）；`PowerCmd.Apply<GlimmeringPower>(5)`（`:26`） | mod Power |
| `Grapple.cs` | 1 / Skill / Uncommon / Self | 枚举 `AloftSystem.GetPlacements<IOriPanelPlacement>` + `EnemyAloftSystem.GetEnemyHeights`（`:66-77`）；为每个候选 `CombatState.CreateCard<MarkPoint>` 并 `InitializeChoice`（`:46-48`）；`CardSelectCmd.FromSimpleGrid(1)`（`:51`）；`AloftSystem.ChangeAsync(Oriented)`（`:56`） | **放置物/敌人高度枚举 + 动态构造代理卡作选择项 + 网格选择 UI**；自定义内部卡 |
| `HandToHand.cs` | 1 / Skill / Common / AnyAlly | `MultiplayerConstraint => MultiplayerOnly`（`:18`）；`GetResultLocationForCardPlay` 覆写（把弃牌堆改成目标玩家手牌，`:39-48`）；`CombatManager.Instance.CurrentCombatId` / `IsCurrentLiveCombat`（`:61,67`）；`CardSelectCmd.FromHand(filter)`（`:63`）；`OriCardTransferCmd.GiveToHand(card, targetPlayer)`（`:69`）；`DeckVersion = null`（`:59`） | **多人限定 + 覆写"打出后去哪里" + 跨玩家转移卡 + 战斗 ID 校验** |
| `Immerse.cs` | 2 / Skill / Rare / Self | `HasTurnEndInHandEffect => true`（`:22`）；`OnTurnEndInHand` → `PowerCmd.Apply<DoomPower>`（`:29-32`）；`AfterCardExhausted` 钩子内 `CardScope.CreateCard<Immerse>` + `CardPileCmd.AddGeneratedCardToCombat(..., Hand)`（`:34-44`） | **手上回合结束钩子 + 卡被放逐钩子 + 自我复制生成**（三个非 `OnPlay` 的行为入口）；`MaxUpgradeLevel 0` |
| `Intangible.cs` | 1 / Skill / Rare / Self | `IsPlayable => AloftSystem.GetAloft(Owner) >= MinAloft`（`:27`）；`ShouldGlowRedInternal => !IsPlayable`（`:29`）；`PowerCmd.Apply<IntangiblePower>`（`:38`） | 高度阈值门槛驱动可打出性 |
| `Intertwine.cs` | 1 / Skill / Common / Self | `OriKeywords.RangedWeaponOnly`（`:25`）；`CalculatedBlockVar.WithMultiplier((card,_) => 手牌数 - (自己在手牌?1:0))`（`:58-67`）；`CreatureCmd.GainBlock(CalculatedBlock.Calculate(target), Props, cardPlay)`（`:79`） | 远程武器判定；**格挡公式是 lambda 且读手牌/所在牌堆** |
| `Irresistible.cs` | 1 / Skill / Uncommon / Self | `CalculatedVar.WithMultiplier((card,_) => CombatManager.Instance.History.CardPlaysFinished.Count(本回合 && 自己))`（`:25`）；`((CalculatedVar)...).Calculate` → `CardPileCmd.Draw`（`:35-36`） | **读取战斗历史（本回合已打出的牌）驱动抽牌数** |
| `LifeCell.cs` | 1 / Skill / Uncommon / Self | `num = MaxHp * Percent / 100`（`:26`）；`PowerCmd.Apply<TempLifePower>(num)`（`:29`） | mod Power + 按 `Creature.MaxHp` 计算的数值 |
| `Lifepact.cs` | 0 / Skill / Uncommon / Self | 判定 `PlayerCombatState.Energy == 0`（`:33`）：成立则 `CreatureCmd.Damage(self, HpLoss, Unblockable|Unpowered|Move, this, cardPlay)` + `PlayerCmd.GainEnergy`（`:35-36`），否则 `CardPileCmd.Draw`（`:40`） | 读能量状态分支；带 `ValueProp` 组合标记的自伤 |
| `LightCatcher.cs` | 1 / Skill / Common / Self | 读 `AloftSystem.GetAloft`（`:27`）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplyLightCatcherAsync(height,1m)`（`:28`） | 高度 + 放置物 |
| `Lightness.cs` | 0 / Skill / Basic / Self | `IsPlayable`/`ShouldGlowRedInternal` 读 `AloftSystem.GetDistanceToAdjustedGround`（`:20-22`）；`CreatureCmd.GainBlock`（`:31`）；实现 `ITranscendenceCard.GetTranscendenceTransformedCard() => ModelDb.Card<Midair>()`（`:39-42`） | 高度条件；BaseLib **Transcendence 卡转换接口** |
| `MagicBow.cs` | 1 / Skill / Uncommon / Self | `OriKeywords.SpiritArcOnly` + `IsPlayable` 依赖 `OriAbilitySystem.CanUse(SpiritArc)`（`:25,92`）；`CardSelectCmd.FromHand(prefs 0..maxCount)`（`:74`）；逐张 `CardCmd.Exhaust`（`:77`）；`OriAbilitySystem.AddCount(SpiritArc, n)`（`:81`） | 能力判定；手牌选择；能力充能写入 |
| `Massing.cs` | 1 / Skill / Common / Self | `PlayerCmd.GainEnergy(1)`（`:35`）；`PowerCmd.Apply<TempStrengthPower>(2)`（`:36`） | 仅获得能量 + mod Power |
| `MasterOfArms.cs` | 1 / Skill / Uncommon / Self | `CreatureCmd.TriggerAnim`（`:31`）；`OriAbilitySystem.GetState` + `state.EnsureCombatRuntime()`（`:32-37`）；`OriWeaponSequence.ResolveNext(OriAbilityRegistry.All 里的武器, state.ActiveWeapon, state.HasAbility)`（`:40-42`）；`state.TrySwitchWeapon`（`:45`）；`OriAbilityRegistry.GetHandler(...).Refresh(state)`（`:61`） | **武器序列轮转 + 能力注册表 + 运行时状态机改写**；自定义 Keyword `WeaponSwitch` |
| `Meteorite.cs` | 1 / Skill / Uncommon / Self | 枚举可达 `AbstractOriPlacementState`（`:62-67`）；未升级走 `OriRandomHelper.PickStableRandomPlacement`（`:36`）；升级后为每个放置物 `CombatState.CreateCard<MeteoritePoint>` + `SetPlacement` + `CardSelectCmd.FromSimpleGrid`（`:40-48`）；`OriPlacementStateSystem.RemoveAsync` + `CreatureCmd.Damage(伤害=高度*0.2)`（`:69-76`） | **放置物枚举 + 稳定随机（自定义确定性随机）+ 代理卡网格选择 + 按放置物高度算伤害 + 移除放置物**；升级前后走完全不同的两条路径 |
| `Midair.cs` | 0 / Skill / Ancient / Self | `CardPileCmd.Draw(3)`（`:45`）；`OriAbilitySystem.AddCount(Bash, n)`（`:46`）；`AloftSystem.ChangeAsync`（`:47`）；`PowerCmd.Apply<MidairPower>`（`:48`） | 三种 mod 系统同时使用；`CardRarity.Ancient`（原版枚举，见 `sts2/src/Core/Entities/Cards/CardRarity.cs:10`） |
| `MoonCut.cs` | 1 / Attack / Uncommon / AllEnemies | 直接调用引擎 `Hook.ModifyDamage(..., ModifyDamageHookType.All, out _)` 取"已修正伤害"（`:41`）；主目标 `DamageCmd.Attack`（`:42`）；其余敌人 `CreatureCmd.Damage(modified*SplashPercent/100, Unpowered)`（`:45-52`） | **在卡牌里手动调引擎伤害修正钩子以获得溅射基准值**（不是纯效果列表） |
| `Myriablade.cs` | 1 / Attack / Rare / AnyEnemy | `DamageCmd.Attack`（`:32`）；`PowerCmd.Apply<MyriabladePower>`（`:35`）；`OriAbilitySystem.Refresh(SpiritEdge)`（`:36`） | mod Power + 能力刷新 |
| `Platform.cs` | 1 / Skill / Uncommon / Self | 读 `AloftSystem.GetAloft`（`:28`）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplyPlatformAsync(height,1m)`（`:29`） | 放置物（平台） |
| `PureHeart.cs` | 1 / Skill / Uncommon / Self | 遍历 `Owner.Creature.Powers` 里 `TypeForCurrentAmount == PowerType.Debuff` 的项逐个 `PowerCmd.Remove`（`:27-30`） | **枚举并批量移除任意 Power（按 Debuff 类型分类）** |
| `Pursue.cs` | 1 / Skill / Uncommon / Self | `CardPileCmd.Draw(2)`（`:22`）；`PowerCmd.Apply<PursuePower>(1)`（`:23`） | 仅抽牌 + mod Power |
| `QuickFilling.cs` | 2 / Power / Rare / Self | `CreatureCmd.TriggerAnim`（`:25`）；`PowerCmd.Apply<QuickFillingPower>(CardsVar)`（`:26`） | mod Power |
| `Rebirth.cs` | 2 / Skill / Rare / Self | 全卡最复杂：`GetResultLocationForCardPlay` 覆写为不进入任何牌堆（`:42-45`）；`PowerCmd.Apply<RebirthLockPower>(silent:true)`（`:57`）；`CardCmd.Discard(全部手牌)`（`:61`）；`Cmd.CustomScaledWait`（`:62,92,95`）；`RemoveNonMarkPowers` 直接 `power.RemoveInternal()` + `AfterRemoved`（`:143-151`）；`OriAbilitySystem.ReinitializeCombatRuntime`（`:65`）；`Creature.LoseBlockInternal`（`:68`）；直接读写 `PlayerCombatState.DrawPile.AddInternal/RandomizeOrderInternal/InvokeContentsChanged/RecalculateCardValues`（`:74-89`）；`Owner.Creature.SetCurrentHpInternal`（`:114,124`）；`AloftSystem.Reset`（`:115`）；`Hook.AfterCurrentHpChanged` / `Hook.AfterCardChangedPiles`（`:94,116,176`）；`NRun.Instance.GlobalUi` + `NSmokyVignetteVfx.Create` + `PlayerFullscreenHealVfx.Play`（`:131,139`）；`NCard.FindOnTable` + `NCombatRoom.Instance.Ui.Hand.Remove` + `GetParent().RemoveChildSafely` + `QueueFreeSafely`（`:162-173`） | **手写战斗重置流程：直接操作牌堆内部、清 Power、清战斗卡、重建抽牌堆、重算卡值、血量为 0 再复活、手动触发引擎 Hook、直接操作 Godot 节点树与全屏 VFX、按本地玩家与否区分表现**。几乎所有"非声明式"能力都出现在这一张卡上 |
| `Reflection.cs` | 1 / Power / Uncommon / Self | `CreatureCmd.TriggerAnim`（`:29`）；`PowerCmd.Apply<ReflectionPower>(2)`（`:30`） | mod Power |
| `Reflux.cs` | 0 / Skill / Uncommon / Self | 从 `PileType.Discard` 用 `CardSelectCmd.FromSimpleGrid` 选牌 → `CardPileCmd.Add(..., PileType.Hand)`（`:29-34`）；从手牌选择 → `CardPileCmd.Add(..., PileType.Draw, CardPilePosition.Top)`（`:35-40`） | **弃牌堆网格选择 + 手牌选择 + 指定插入位置（牌堆顶部）** |
| `Regenerate.cs` | 1 / Skill / Uncommon / Self | `CreatureCmd.Heal(4)`（`:21`）；`PlayerCmd.EndTurn(Owner, canBackOut:false)`（`:22`） | 治疗 + **打出中强制结束回合** |
| `Renzokuken.cs` | 1 / Skill / Uncommon / Self | `PowerCmd.Apply<RenzokukenPower>(2)`（`:26`）；`OriAbilitySystem.Refresh(SpiritEdge)`（`:27`） | mod Power + 能力刷新 |
| `Roaming.cs` | 1 / Skill / Uncommon / Self | 读 `AloftSystem.GetAloft`（`:21`），用专用 RNG 在 `[0,aloft-50] ∪ [aloft+50,200]` 中采样（`:22-30`）；`AloftSystem.TeleportAsync`（`:31`） | 高度状态 + 自定义随机采样区间 + 传送 |
| `RoundhouseKick.cs` | 1 / Attack / Uncommon / AnyEnemy | `DamageCmd.Attack(...).WithHitFx("vfx/vfx_heavy_blunt")`（`:35-37`）；覆写 `ModifyDamageMultiplicative(target, amount, props, dealer, cardSource, cardPlay)`：读 `AloftSystem.GetMomentum` 与敌人高度符号，返回倍率（`:40-57`） | 自定义 Keyword `Momentum`；**在卡牌里覆写伤害修正钩子**（伤害计算不可用静态数值描述） |
| `SentryMark.cs` | 1 / Skill / Uncommon / Self | 读 `AloftSystem.GetAloft`（`:27`）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplySentryAsync(height,rounds)`（`:29`） | 放置物（哨戒，持续回合） |
| `Seraph.cs` | 3 / Power / Rare / Self | `CreatureCmd.TriggerAnim`（`:27`）；`PowerCmd.Apply<SeraphPower>(Increase)`（`:28`） | mod Power |
| `ShadowCraft.cs` | 1 / Power / Rare / Self | `CreatureCmd.TriggerAnim`（`:32`）；`PowerCmd.Apply<ShadowCraftPower>(DexterityPower 值)`（`:33`） | mod Power（带阈值参数） |
| `ShardOvercharge.cs` | 1 / Power / Rare / Self | `CreatureCmd.TriggerAnim`（`:30`）；`PowerCmd.Apply<OverchargePower>(1)`（`:31`）；升级 `AddKeyword(Innate)`（`:36`） | mod Power；自定义 Keyword `Return`（`:18`，语义在卡牌之外） |
| `ShardResilience.cs` | 1 / Power / Uncommon / Self | `CreatureCmd.TriggerAnim`（`:24`）；`PowerCmd.Apply<ResiliencePower>(Percent)`（`:25`） | mod Power |
| `Sinking.cs` | 0 / Skill / Uncommon / Self | `IsPlayable`/`ShouldGlowRedInternal` 读 `AloftSystem.GetDistanceToAdjustedGround`（`:27-29`）；`GainBlock`（`:38`）；`AloftSystem.ChangeAsync(-Aloft, ActiveFall)`（`:39`）；`PowerCmd.Apply<SinkingPower>(1)`（`:40`） | 高度条件 + 主动下落 + mod Power |
| `Skyrocket.cs` | 1 / Skill / Uncommon / Self | `CardSelectCmd.FromHand(prefs 0..count)`（`:34`）；`CardCmd.Discard`（`:37`）；`AloftSystem.ChangeAsync(discarded*Aloft, Common)`（`:38`） | 手牌选择 + 按弃牌数缩放高度变更 |
| `SmallHouse.cs` | 3 / Skill / Rare / Self | 读 `AloftSystem.GetAloft`（`:30`）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplySmallHouseAsync(height,health)`（`:31`） | 放置物（带耐久） |
| `Spark.cs` | 1 / Skill / Common / Self | `PowerCmd.Apply<AimPower>(25)`（`:29`）；`PowerCmd.Apply<AimRetainPower>(1)`（`:30`） | 两个 mod Power |
| `SpiritLink.cs` | 1 / Skill / Rare / Self | 读 `AloftSystem.GetAloft`（`:24`）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplySpiritLinkAsync(height,1m)`（`:25`） | 放置物 |
| `SpiritWell.cs` | 1 / Skill / Uncommon / Self | 读 `AloftSystem.GetAloft`（`:24`）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplySpiritWellAsync(height,1m)`（`:25`） | 放置物 |
| `SpringPlants.cs` | 1 / Power / Uncommon / Self | 读 `AloftSystem.GetGroundAloft`（`:28`，地面高度而非自身高度）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplySpringPlantsAsync(height,charges)`（`:29`） | 放置物 + 区分"地面高度"与"自身高度" |
| `Stasis.cs` | 1 / Skill / Uncommon / Self | 自定义 Keyword `Momentum`（`:29`）；`CalculatedBlockVar.WithMultiplier(CalculateMomentumBlockCount)`，其中读 `AloftSystem.GetMomentum` 与多个 DynamicVar（`:40,58-62`）；`CreatureCmd.GainBlock(CalculatedBlock.Calculate(target), Props, cardPlay)`（`:50`） | 格挡量由 lambda 读动量状态算出，且有上限截断 |
| `Storm.cs` | 1 / Attack / Common / AllEnemies | `DamageCmd.Attack(...).TargetingAllOpponents`（`:34`）；`AloftSystem.ChangeAsync(+Aloft, Common)`（`:37`）；`CardPileCmd.Draw(1)`（`:38`） | 全体伤害 + 高度变更 + 抽牌组合 |
| `Stove.cs` | 1 / Skill / Uncommon / Self | 读 `AloftSystem.GetAloft`（`:30`）；`OriPlacementHelper.ApplyPlacementAsync` + `ApplyStoveAsync(height,damage)`（`:32`）；`OriPlacementDisplays.Stove.CreateHoverTip()`（`:19`） | 放置物 + **放置物自定义 HoverTip 展示对象** |
| `StrikeOri.cs` | 1 / Attack / Basic / AnyEnemy | `DamageCmd.Attack(...).Targeting(...).WithHitFx(...)`（`:26-28`） | 仅伤害（+ 原版 `CardTag.Strike`，`:14`） |
| `SunCross.cs` | 1 / Attack / Uncommon / AnyEnemy | 读玩家 aloft 与目标 `EnemyAloftSystem.GetHeight` 求方向（`:30-31`）；按方向与高度条件过滤敌人并按 `CombatId` 排序（`:32-41`）；逐个 `DamageCmd.Attack`（`:42-47`） | **敌人高度条件筛选 + 按战斗 ID 稳定排序 + 多次单体攻击** |
| `SunMoonRadiance.cs` | 1 / Skill / Rare / Self | 读 `AloftSystem.GetAloft`（`:29`）；`<=100` 则 `PlayerCmd.LoseEnergy` 否则 `GainEnergy`（`:31-38`）；`AloftSystem.SetAsync(200-currentAloft, Absolute)`（`:39`） | 高度阈值分支 + 能量增减 + 绝对高度设置（镜像翻转） |
| `SwitchPlaces.cs` | 1 / Skill / Uncommon / Self | `MultiplayerConstraint => MultiplayerOnly`（`:11`）；`cardPlay.Target?.Player`（`:20`）；`AloftSystem.SwapPlayersAsync(Owner, player, cardPlay)`（`:23`） | 多人限定 + **跨玩家交换高度状态** |
| `SwordDance.cs` | 2 / Power / Uncommon / Self | `CreatureCmd.TriggerAnim`（`:29`）；`PowerCmd.Apply<SwordDancePower>(1)`（`:30`） | mod Power |
| `SwordWave.cs` | 2 / Attack / Common / AnyEnemy | `Hook.ModifyDamage(..., ModifyDamageHookType.All, out _)` 取修正后伤害（`:32`）；主目标 `DamageCmd.Attack`（`:33`）；其余敌人 `CreatureCmd.Damage(splashDamage, Unpowered)`（`:36-40`） | 手动调引擎伤害修正钩子做溅射 |
| `Torrent.cs` | 2 / Power / Ancient / Self | `OriAbilitySystem.GrantAllTemporaryAndRefreshAll(Owner)`（`:22`）；实现 BaseLib `ITomeCard.TomeCharacter => ModelDb.Character<OriCharacter>()`（`:11-13`） | 全体能力临时授予 + BaseLib **Tome（典籍）卡接口**；`CardRarity.Ancient` |
| `TowardTheNight.cs` | 1 / Skill / Common / Self | `OriAbilitySystem.AddCount(SpiritEdge, n)`（`:24`） | 能力充能写入 |
| `Tread.cs` | 2 / Attack / Uncommon / AnyEnemy | `CalculatedDamageVar.WithMultiplier(card => ((Tread)card).CalculateFallDamage())`（`:28`）；`GetFallDistance()` 读 `AloftSystem.GetAloft/GetDistanceToTreadSurface/IsAntigravity` 并枚举 `IOriPlatformPlacement` 求最近平台（`:56-71`）；`AloftSystem.ChangeAsync(-400, ActiveFall)`（`:40`）；`DamageCmd.Attack(先算好的伤害)`（`:41`） | **伤害公式是强转具体卡类型的 lambda，读高度/踩踏面/反重力并枚举平台放置物** |
| `Vigil.cs` | 1 / Skill / Common / Self | 判定 `cardPlay.IsFirstInSeries && !CombatManager.Instance.History.CardPlaysFinished.Any(entry.CardPlay.Card == this)`（`:28`）；`PowerCmd.Apply<EnergyNextTurnPower>`（`:29`）；条件成立再 `PlayerCmd.GainEnergy(1)`（`:32`） | 读战斗历史 + `cardPlay.IsFirstInSeries` 时序标志决定额外效果 |
| `WindSpirit.cs` | 1 / Skill / Common / AllEnemies | `PowerCmd.Apply<WeakPower>(CombatState.HittableEnemies, 1, Owner.Creature, this)`（`:38`）；`AloftSystem.ChangeAsync(+Aloft, Common)`（`:39`） | 全体施加原版 Power + 高度变更 |
| `WispEverlasting.cs` | 1 / Skill / Rare / Self | `CardPileCmd.Draw(2)`（`:31`）；`CardSelectCmd.FromHand(filter: Reincarnator.CanEnchant)`（`:35`）；`ModelDb.Enchantment<Reincarnator>()` + `CardCmd.Enchant(...)` 对选中卡与其牌库版本各一次（`:61,64`）；`CardModel.FromSerializable(selectedCard.ToSerializable())` + `Owner.RunState.AddCard` + `CardPileCmd.Add(deckCopy, Deck)` + `selectedCard.DeckVersion = deckCopy`（`:67-70`）；`CardCmd.PreviewCardPileAdd`（`:71`） | **附魔（Enchantment）系统 + 卡牌序列化/克隆 + 同步"战斗内副本 ↔ 牌库版本" + 选择网格预览 UI** |
| `WispFlight.cs` | 1 / Skill / Uncommon / AnyAlly | `MultiplayerConstraint => MultiplayerOnly`（`:14`）；读目标玩家 `AloftSystem.GetAloft(player)`（`:26`）；`AloftSystem.SetAsync(Owner, aloft, Absolute)`（`:27`） | 多人限定 + 跨玩家读取高度 |
| `WispsWill.cs` | 1 / Power / Rare / Self | `CreatureCmd.TriggerAnim`（`:30`）；`PowerCmd.Apply<IntangiblePower>(1)`（`:31`）；`PowerCmd.Apply<WispsWillPower>(1)`（`:32`）；`HoverTipFactory.FromCard<Immerse>()`（`:18`） | 原版 + mod Power 组合 |
| `WorldUnfurled.cs` | 1 / Skill / Rare / Self | `[SavedProperty] RecordedPlacements`（`:22-34`）；`OriPlacementRecord.Serialize/Deserialize`（`:32,46,68`）；`OriPlacementTypeRegistry.CreateFromRecord`（`:51`）；动态构造 `LocString` + `HoverTip`（`:52-56`）；`OriPlacementStateSystem.RecordNearestPlacements(3)`（`:67`）；`OriPlacementHelper.ApplyPlacementAsync` + `ReplayPlacementsAsync(previousRecords)`（`:71`）；写 `DeckVersion.RecordedPlacements`（`:74-77`） | **卡牌上持久化 mod 状态并在两个副本间同步；放置物序列化/回放；运行时生成本地化文本与 HoverTip** |

---

## 3. Fields（`Cards/Fields/*.cs`，7 张，全部为场地放置）

| card file | cost/type/rarity/target | 游戏操作 | 需要的非原版能力 |
|---|---|---|---|
| `FieldDarkness.cs` | 0 / Power / Curse / Self | `OriPlacementHelper.ApplyPlacementAsync` + `ApplyDarknessFieldAsync`（`:34`）；`OriKeywords.FieldDarkness`（`:20`） | 放置物；自定义 Keyword；`CardRarity.Curse`（原版枚举）；`MaxUpgradeLevel 0` |
| `FieldDesert.cs` | 1 / Power / Rare / Self | `ApplyDesertFieldAsync(100)`（`:36`）；`OriKeywords.SubstantialField` + `FieldDesert` + `Momentum`（`:20-22`） | 放置物（带顶高参数 100）；三个自定义 Keyword |
| `FieldForest.cs` | 1 / Power / Uncommon / Self | `ApplyForestFieldAsync`（`:34`）；`OriKeywords.FieldForest`（`:20`） | 放置物 + 自定义 Keyword |
| `FieldOcean.cs` | 1 / Power / Uncommon / Self | `ApplyOceanFieldAsync`（`:36`）；`SubstantialField` + `FieldOcean` + `Momentum`（`:20-22`） | 放置物 + 三个自定义 Keyword |
| `FieldSnowslide.cs` | 1 / Power / Uncommon / Self | `ApplySnowslideFieldAsync`（`:34`）；`OriKeywords.FieldSnowslide`（`:20`） | 放置物 + 自定义 Keyword |
| `FieldVoid.cs` | 0 / Power / Curse / Self | `ApplyVoidFieldAsync`（`:34`）；`OriKeywords.FieldVoid`（`:20`） | 放置物 + 自定义 Keyword；Curse 稀有度 |
| `FieldWellspring.cs` | 1 / Power / Uncommon / Self | `ApplyWellspringFieldAsync`（`:34`）；`OriKeywords.FieldWellspring`（`:20`） | 放置物 + 自定义 Keyword |

> 全部 7 张 `Fields/` 卡都是"打出后生成一个驻留场地的放置物"，效果本身写在放置物系统里（`Ori/Placements/`），
> 卡牌侧只有一行 `ApplyPlacementAsync`。另有 `FieldCampfire.cs` 在 `Cards/` 根目录但同属这一族（见上表）。

---

## 4. Special（`Cards/Special/*.cs`，9 张）

| card file | cost/type/rarity/target | 游戏操作 | 需要的非原版能力 |
|---|---|---|---|
| `Asterism.cs` | 2 / Power / Uncommon / Self | `CreatureCmd.TriggerAnim`（`:30`）；`PowerCmd.Apply<AsterismPower>(20)`（`:31`） | mod Power |
| `Binding.cs` | 1 / Skill / Uncommon / AnyAlly | `MultiplayerConstraint => MultiplayerOnly`（`:13`）；`cardPlay.Target?.Player`（`:24`）；`Owner.Creature.GetPower<BindingPower>()` 后 `PowerCmd.Remove`（`:27-31`）；`BindingPower.Create(targetPlayer)` + `PowerCmd.Apply(实例, ...)`（`:32`） | 多人限定 + **运行时构造 Power 实例并绑定到某玩家** |
| `FellTheStars.cs` | 2 / Attack / Rare / AllEnemies | `IsPlayable`/`ShouldGlowRedInternal` 依赖 `OriAbilitySystem.CanUse(SpiritEdge)`（`:78-81`）；`DamageCmd.Attack(...).TargetingAllOpponents`（`:67`）；`OriAbilitySystem.TemporarilyDisable(SpiritEdge)`（`:69`） | 能力可用性门槛 + 临时禁用能力；自定义 Keyword `SpiritEdgeOnly`；资产 Key 设为 `"Unknown"`（`:15`） |
| `LastLight.cs` | -2 / Quest / Token / None | 无 `OnPlay`（`IsPlayable => false`，`:16`）；只有自定义 Keyword `OriKeywords.LastLight`（`:10`）；实际行为在基类 `AbstractOriVisualCard.AfterPreventingDeath` → `CardPileCmd.RemoveFromDeck` + `CreatureCmd.Heal(8m)` | **卡牌本身不执行任何操作，行为完全由基类死亡拦截钩子实现**；负费用（`-2`）表示不可打出的 Quest 卡；自定义 Keyword |
| `LookoutsBlessing.cs` | 1 / Skill / Uncommon / Self | 7 个 `Func<Player,CardModel>` 候选工厂（`CombatState.CreateCard<T>`）（`:17-26`）；循环 `RunState.Rng.CombatCardSelection.NextInt` 取 3 个不重复下标（`:44-52`）；`CardSelectCmd.FromSimpleGridForRewards(cards, 1)`（`:56`）；按手牌上限选 `Hand` 或 `Discard` 后 `CardPileCmd.AddGeneratedCardToCombat`（`:59-60`） | **运行时构造候选卡池 + 专用 RNG 去重抽样 + "奖励式"网格选择 UI + 按手牌上限决定生成去向** |
| `MarkPoint.cs` | -2 / Skill / Token / Self（继承 `AbstractOriInternalCard`） | `OnPlay` → `Task.CompletedTask`（`:35-38`）；`InitializeChoice(targetHeight, moveDelta, title)` 写 `DynamicVars["TargetHeight"/"MoveSummary"/"TargetTitle"]` 与 `TitleLocString`（`:40-49`）；`BuildMoveSummary` 动态构造 `LocString`（`:51-66`） | **纯 UI 代理卡**：`StringVar` + 运行时本地化 + 标题注入；`MaxUpgradeLevel 0`、不进卡库、不可在战斗内生成；资源 Key 复用 `"grapple"` |
| `MeteoritePoint.cs` | -2 / Skill / Token / AllEnemies | 持有 `IOriPlacement` 引用（`:25-27`）；`SetPlacement` 写 name/desc/height/damage 变量（`:56-66`）；`OnPlay` 中 `OriPlacementStateSystem.RemoveAsync(placement)` + `CreatureCmd.Damage(全体, Unpowered)`（`:44-54`） | 纯 UI 代理卡 + 持有放置物引用 + 从放置物反推消耗的 HoverTip 文本 |
| `SunsetDance.cs` | 1 / Skill / Uncommon / Self | `OriKeywords.SpiritEdgeOnly` + `IsPlayable` 依赖 `OriAbilitySystem.CanUse(SpiritEdge)`（`:29,88`）；`CardSelectCmd.FromHand(1)` + `CardCmd.Exhaust`（`:72-76`）；`PowerCmd.Apply<TempStrengthPower>(5)`（`:78`） | 能力判定 + 手牌选择放逐 + mod Power |
| `Temperament.cs` | 1 / Skill / Rare / Self | 自定义 Keyword `Temperament`（`:28`）；`IsPlayable` 依赖 `OriAbilitySystem.GetActiveWeapon`（`:32,57-70`）；`TransformPile(Draw/Hand/Discard)`：逐张按硬编码映射表 `CardCmd.Transform(card, replacement, CardPreviewStyle.None)`（`:44-46,72-88`）；`TransformPowers`：逐 Power 类型做手工换算/替换（Aim↔充能、Asterism↔SwordDance、Reflection↔Glimmering、Seraph↔QuickFilling，`:90-174`）；`CopyCardState` 复制升级等级、`DeckVersion`、附魔快照，不能附魔时暂存到 `DormantTemperamentEnchantment`（`:176-206`）；`CreateMeleeReplacement`/`CreateRangedReplacement` 两张硬编码映射表（`:208-304`）；最后 `CardPileCmd.Draw`（`:48`） | **最重的声明式反例**：约 300 行的手写卡牌/能力双向变换表、跨牌堆批处理、`CardModel` 序列化与附魔状态搬迁、"暂存附魔"这一自定义持久字段；完全无法用效果列表或简单宏描述 |

---

## 5. Special/Concepts（`Cards/Special/Concepts/*.cs`，11 张）

全部 11 张都继承 `AbstractOriConceptCard`：`OnPlay` 被 `sealed` 为空实现（`AbstractOriConceptCard.cs:34-37`），
资产 Key 固定 `"Unknown"`（`:11`），强制带 `OriKeywords.ConceptCard`（`:23`），`CanBeGeneratedInCombat => false`（`:13`），
稀有度固定 `CardRarity.Event`（`:30`）。**卡牌文件里只有费用/类型/关键词/DynamicVars 和 `OnUpgrade`，没有任何效果**——行为在卡牌之外。

| card file | cost/type/rarity/target | 游戏操作 | 需要的非原版能力 |
|---|---|---|---|
| `BizarreAdventure.cs` | 1 / Power / Event / Self | 无（仅费用升级 `EnergyCost.UpgradeBy(-1)`，`:14`） | 空实现卡；行为外部实现 |
| `HoppingIsCute.cs` | 2 / Power / Event / Self | 无（仅费用升级，`:14`） | 同上 |
| `KingFromAbove.cs` | 2 / Attack / Event / AllEnemies | 无（`DamageVar(28, Move)` 仅用于描述，`:12`；`ExtraKeywords` 加 `OriKeywords.Judgment`，`:10`） | 空实现卡 + 自定义 Keyword `Judgment` |
| `MightyOriDescends.cs` | 1 / Power / Event / Self | 无（`CardsVar(2)`，`:9`） | 空实现卡 |
| `Mirage.cs` | 2 / Power / Event / Self | 无（`PowerVar<StrengthPower>(5)`，`:13`） | 空实现卡；引用原版 Power 仅作展示 |
| `NightProwl.cs` | 1 / Power / Event / Self | 无（`EnergyVar(2)`，`:12`） | 空实现卡 |
| `SevenSwordsUnite.cs` | 3 / Power / Event / Self | 无（仅费用升级，`:14`） | 空实现卡 |
| `ShadowCloak.cs` | 2 / Power / Event / Self | 无（仅费用升级，`:14`） | 空实现卡 |
| `SunMoonIsle.cs` | 2 / Power / Event / Self | 无（`EnergyVar(2)`，`:12`） | 空实现卡 |
| `SuperBash.cs` | 1 / Power / Event / Self | 无（`HoverTipFactory.FromKeyword(OriKeywords.Bash)`，`:9`） | 空实现卡 + 自定义 Keyword |
| `Tempering.cs` | 1 / Skill / Event / Self | 无（`IntVar("Charges",1)`，`:11`） | 空实现卡 |

---

## 6. CardPools（4 个文件）

| card file | 内容 | 需要的非原版能力 |
|---|---|---|
| `CardPools/AbstractOriVisualCardPool.cs` | 继承 BaseLib `CustomCardPoolModel`；能量图标路径、HSV、`DeckEntryCardColor`、`IsColorless`；`CustomFrame(card)` 按 `CardType` 用 `ResourceLoader.Load<Texture2D>` 加载攻击/能力/技能框（`:25-34`） | BaseLib 自定义卡池模型 + Godot 纹理加载 + 按类型自定义卡框 |
| `CardPools/OriCardPool.cs` | `Title => "ori"`，`IsShared => false`（`:5-7`） | 自定义（非共享）卡池 |
| `CardPools/OriFieldCardPool.cs` | `Title => "ori_field"`，`IsShared => true`（`:5-7`） | 自定义共享卡池（`AbstractOriFieldCard` 归属） |
| `CardPools/OriSpecialCardPool.cs` | `Title => "ori_special"`，`IsShared => true`（`:5-7`） | 自定义共享卡池（`AbstractOriSpecialCard` 归属） |

三个卡池分别对应三条继承链：`AbstractOriCard`→`OriCardPool`、`AbstractOriFieldCard`→`OriFieldCardPool`、
`AbstractOriSpecialCard`→`OriSpecialCardPool`。

---

## 7. 聚合数字

### 7.1 至少需要一项非原版能力的卡牌数

| 口径 | 数字 | 计算方式 |
|---|---|---|
| 卡牌侧能力（A） | **94 / 122**（占 77.0%） | 人工逐张阅读 122 个卡牌类；"可用普通效果列表表达"的白名单为空以外的 28 张（见 7.2），其余 94 张至少命中下面 7.3 的某一项 |
| 内容依赖（B） | **122 / 122** | 每张卡都至少引用一次 mod 注册内容。最保守的例子：`StrikeOri.cs:14` 用 `CardTag.Strike` 但归属自定义池（`AbstractOriCard.cs:7`）；纯伤害 0 依赖的卡为 0 张 |
| 文件口径（含基础设施） | **131 / 131** 个文件至少依赖一项非原版能力 | 9 个基础设施文件本身即能力声明（自定义 Tag/Keyword/卡池/基类钩子） |

验证命令（在工作区根目录执行）：

```pwsh
# 文件清单与行数
Get-ChildItem -Path "reference\orimod\src\Ori\Cards" -Recurse -File -Filter *.cs |
  ForEach-Object { "{0,5}  {1}" -f (Get-Content $_.FullName | Measure-Object -Line).Lines, $_.Name } | Measure-Object

# 引用 mod 能力的文件数（示例：能力系统 / aloft / 放置 / 自定义 Tag / 自定义 Keyword）
$files = Get-ChildItem -Path "reference\orimod\src\Ori\Cards" -Recurse -File -Filter *.cs
'能力系统: ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'OriAbilitySystem\.' }).Count
'aloft:   ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'AloftSystem\.' }).Count
'放置:     ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'OriPlacementStateSystem\.|OriPlacementHelper\.' }).Count
'自定义Tag: ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'OriCardTags\.' }).Count
'自定义KW:  ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'OriKeywords\.' }).Count
'mod Power: ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'PowerCmd\.Apply<[A-Za-z]+Power>|PowerVar<[A-Za-z]+Power>|GetPower<[A-Za-z]+Power>' }).Count

# 变异/覆写面
'WithMultiplier(lambda): ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'WithMultiplier' }).Count
'IsPlayable override:    ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'override bool IsPlayable' }).Count
'Hook.:                  ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'Hook\.' }).Count
```

### 7.2 可表达为"普通有序效果列表"的卡牌数

判定标准（按用户给定定义严格取）：`OnPlay` + `CanonicalVars` + `OnUpgrade` 只包含
**定量伤害（单体/全体、固定段数）、获得格挡、施加 Power（固定量）、抽 N 张、弃/放逐手牌、获得/失去能量、治疗**
这类固定原语，且**没有**：状态读取、lambda 公式、条件分支、手牌/弃牌堆选择、自定义钩子或基类覆写、UI/代理卡、持久化。

**28 / 122 张（23.0%）** 满足，列举（含依据行）：

| # | card | 仅含的效果 | 依据 |
|---|---|---|---|
| 1 | `Absorbed.cs` | 施加 `AimPower(25)` | `:25` |
| 2 | `Afterimage.cs` | 施加 `AfterimagePower(8)` | `:23-24` |
| 3 | `AncientForce.cs` | 施加 `TempStrengthPower(4)` | `:25` |
| 4 | `CardWheel.cs` | 施加 `CardWheelPower(4)` + 抽 4 | `:22-23` |
| 5 | `Clamber.cs` | 格挡 8 + 施加 `ClamberPower(2)` | `:31-32` |
| 6 | `CongealingSoul.cs` | 6 伤害 ×3 段（单体） | `:32-35` |
| 7 | `DefendOri.cs` | 格挡 5 | `:26` |
| 8 | `Dignified.cs` | 施加 `DignifiedPower(2)` | `:33-34` |
| 9 | `Farewell.cs` | 弃全部手牌 + 抽 2 | `:25-30` |
| 10 | `Fluorescence.cs` | 施加 `FluorescencePower(2)` + 全体 4 伤害 ×2 段 | `:37-42` |
| 11 | `ForestEyes.cs` | 无（空效果列表） | `:7-9` |
| 12 | `ForestStrength.cs` | 施加 `ForestStrengthPower(35)` | `:22-23` |
| 13 | `Frozen.cs` | 5 伤害 + 对目标与自身各施加 `FrozenPower(5)` | `:35-40` |
| 14 | `Glimmering.cs` | 施加 `GlimmeringPower(5)` | `:25-26` |
| 15 | `Massing.cs` | 获得 1 能量 + 施加 `TempStrengthPower(2)` | `:35-36` |
| 16 | `Pursue.cs` | 抽 2 + 施加 `PursuePower(1)` | `:22-23` |
| 17 | `QuickFilling.cs` | 施加 `QuickFillingPower(1)` | `:25-26` |
| 18 | `Reflection.cs` | 施加 `ReflectionPower(2)` | `:29-30` |
| 19 | `Regenerate.cs` | 治疗 4 + 结束回合 | `:21-22` |
| 20 | `Seraph.cs` | 施加 `SeraphPower(2)` | `:27-28` |
| 21 | `ShadowCraft.cs` | 施加 `ShadowCraftPower(1)` | `:32-33` |
| 22 | `ShardOvercharge.cs` | 施加 `OverchargePower(1)` | `:30-31` |
| 23 | `ShardResilience.cs` | 施加 `ResiliencePower(20)` | `:24-25` |
| 24 | `Spark.cs` | 施加 `AimPower(25)` + `AimRetainPower(1)` | `:29-30` |
| 25 | `StrikeOri.cs` | 6 伤害（单体） | `:26-28` |
| 26 | `SwordDance.cs` | 施加 `SwordDancePower(1)` | `:29-30` |
| 27 | `WispsWill.cs` | 施加 `IntangiblePower(1)` + `WispsWillPower(1)` | `:30-32` |
| 28 | `Special/Asterism.cs` | 施加 `AsterismPower(20)` | `:30-31` |

必须同时说明的两个限定（否则这个数字会被误读）：

1. **这 28 张里只有 6 张的效果完全不触碰 mod 定义的内容**：`CongealingSoul`、`DefendOri`、`Farewell`、
   `ForestEyes`、`Regenerate`、`StrikeOri`。另外 22 张的"施加 Power"目标都是 mod 自行定义的 PowerModel
   （`Ori/Powers/` 下 38 个文件），DSL 侧仍须先能声明这些 Power，卡牌才能成立。
2. **这 28 张里有 2 张的效果列表之外还挂了会改变结算的元数据**：`CongealingSoul.cs:16` 的
   `OriCardTags.RangedAttack`（被 `Powers/AimPower.cs:35` 等 8 处读取），`ShardOvercharge.cs:18` 的
   `OriKeywords.Return`。若把"外部系统读取的 Tag/Keyword"也算作能力需求，则纯效果列表卡降为 **26 / 122**。

### 7.3 mod 专有系统清单（含引用卡牌数与 file:line 例子）

| 系统 | 引用卡牌文件数 | 代表 file:line |
|---|---|---|
| `OriCardTags`（10 个自定义 `CardTag`） | 49 | `reference/orimod/src/Ori/Cards/ChargeJump.cs:16`（`NoAloftAutoFall`） |
| `OriKeywords`（27 个自定义 `CardKeyword`） | 32 | `reference/orimod/src/Ori/Cards/Deflection.cs:23`（`MeleeWeaponOnly`） |
| `Ori.Powers` 下的 mod PowerModel | 45 | `reference/orimod/src/Ori/Cards/Absorbed.cs:25`（`AimPower`） |
| `AloftSystem`（高度/距离/动量/反重力；共 45 个 public 成员） | 34 | `reference/orimod/src/Ori/Cards/ChargeJump.cs:35`（`ChangeAsync`） |
| `AloftSystem` 只读取状态 | 27 | `reference/orimod/src/Ori/Cards/Intangible.cs:27`（`GetAloft`） |
| `AloftSystem` 写状态（`ChangeAsync`/`SetAsync`/`TeleportAsync`/`SwapPlayersAsync`/`Reset`） | 17 | `reference/orimod/src/Ori/Cards/SunMoonRadiance.cs:39`（`SetAsync`） |
| `EnemyAloftSystem`（敌人高度层） | 6 | `reference/orimod/src/Ori/Cards/Departure.cs:38`（`GetHeight`） |
| `OriPlacementStateSystem`（放置物生成/移除/记录/回放；40 个 public 成员） | 23 | `reference/orimod/src/Ori/Cards/SentryMark.cs:29`（`ApplySentryAsync`） |
| `OriPlacementHelper`（打出 scope 包装） | 20 | `reference/orimod/src/Ori/Cards/Platform.cs:29` |
| `AloftSystem.GetPlacements` / `IOriPlacement`·`IOriPanelPlacement`·`IOriPlatformPlacement`·`AbstractOriPlacementState` | 3 / 4 | `reference/orimod/src/Ori/Cards/Grapple.cs:68`；`reference/orimod/src/Ori/Cards/Meteorite.cs:71` |
| `OriPlacementRecord` / `OriPlacementOrdering` / `OriPlacementDisplays` / `OriPlacementIdentityAllocator` | 6 | `reference/orimod/src/Ori/Cards/WorldUnfurled.cs:67`；`reference/orimod/src/Ori/Cards/OriPlacementHelper.cs:25` |
| `OriAbilitySystem`（能力/武器状态；34 个 public 成员） | 18 | `reference/orimod/src/Ori/Cards/TowardTheNight.cs:24`（`AddCount`） |
| `OriAbilityId`（`SpiritEdge`/`SpiritArc`/`LightHammer`/`LightSpear`/`Bash`） | 14 | `reference/orimod/src/Ori/Cards/Myriablade.cs:36`（`Refresh`） |
| `OriAbilityFamily` / `OriAbilityRegistry` / `OriWeaponSequence` / `OriAbilityStateModifier` | 2 | `reference/orimod/src/Ori/Cards/MasterOfArms.cs:40-42` |
| `OriAbilityAttackExecutor`（mod 专用攻击执行器） | 1 | `reference/orimod/src/Ori/Cards/Ablution.cs:40` |
| 附魔系统（`Reincarnator` / `EnchantmentModel` / `CardCmd.Enchant` / `DormantTemperamentEnchantment`） | 3 | `reference/orimod/src/Ori/Cards/WispEverlasting.cs:61`；`reference/orimod/src/Ori/Cards/Special/Temperament.cs:184` |
| `OriCardTransferCmd`（跨玩家转移卡） | 1 | `reference/orimod/src/Ori/Cards/HandToHand.cs:69` |
| `OriDeathSystem`（不可预防的死亡） | 1 | `reference/orimod/src/Ori/Cards/EmbraceTheLight.cs:58` |
| `OriRandomHelper`（稳定随机放置物选择） | 1 | `reference/orimod/src/Ori/Cards/Meteorite.cs:36` |
| 卡上持久化 mod 状态（`[SavedProperty]`） | 3 | `reference/orimod/src/Ori/Cards/AbstractOriVisualCard.cs:21`；`reference/orimod/src/Ori/Cards/WorldUnfurled.cs:22` |
| 自定义卡池归属（`[Pool]` / `VisualCardPool`） | 3 | `reference/orimod/src/Ori/Cards/AbstractOriCard.cs:7`；`reference/orimod/src/Ori/Cards/AbstractOriFieldCard.cs:12`；`reference/orimod/src/Ori/Cards/AbstractOriSpecialCard.cs:12` |

> 关于 "wisps"：**没有任何卡牌文件直接引用 wisps 系统**。卡牌里出现的 `Wisp*` 全部是卡牌名
> （`WispEverlasting`、`WispFlight`、`WispsWill`）。与 wisp 相关的唯一运行时依赖是 `WispsWill.cs:32` 施加的
> `WispsWillPower`（`reference/orimod/src/Ori/Powers/WispsWillPower.cs`）。也就是说 wisp 机制的复杂度
> **不在卡牌层**，而在 Power 层；卡牌层只表现为"施加一个 mod Power"。

### 7.4 用到的游戏 API（按领域分组）

说明：`X N` 表示 N 个卡牌文件用到（不含基础设施文件；统计命令见 7.6）。

**伤害（damage）**
- `DamageCmd.Attack(decimal).FromCard(this, cardPlay).Targeting(target)` — 18（例 `reference/orimod/src/Ori/Cards/StrikeOri.cs:26`）
- `DamageCmd.Attack(...).TargetingAllOpponents(CombatState)` — 例 `reference/orimod/src/Ori/Cards/Storm.cs:34`
- `DamageCmd.Attack(...).WithHitCount(n)` — 例 `reference/orimod/src/Ori/Cards/CongealingSoul.cs:32`
- `DamageCmd.Attack(...).TargetingRandomOpponents(CombatState)` — 例 `reference/orimod/src/Ori/Cards/Feathers.cs:42`
- `DamageCmd.Attack(...).WithHitFx("vfx/...")` — 15（例 `reference/orimod/src/Ori/Cards/Burst.cs:28`）
- `CreatureCmd.Damage(choiceContext, target(s), amount, ValueProp, dealer, null, cardPlay)` — 6（例 `reference/orimod/src/Ori/Cards/SwordWave.cs:39`）
- `ValueProp` 组合：`Move`、`Unpowered`、`Unblockable` — 例 `reference/orimod/src/Ori/Cards/Lifepact.cs:35`
- `Hook.ModifyDamage(..., ModifyDamageHookType.All, ..., out _)` 手动取修正值 — 2（`reference/orimod/src/Ori/Cards/MoonCut.cs:41`、`reference/orimod/src/Ori/Cards/SwordWave.cs:32`）
- 覆写 `ModifyDamageMultiplicative(...)` — 1（`reference/orimod/src/Ori/Cards/RoundhouseKick.cs:40`）

**格挡 / 生命（block / HP）**
- `CreatureCmd.GainBlock` 共 9 处调用：其中 7 处传 `DynamicVars.Block`（例 `reference/orimod/src/Ori/Cards/DefendOri.cs:26`），2 处传运行时算出的 `decimal` + `ValueProp`（`reference/orimod/src/Ori/Cards/Intertwine.cs:79`、`reference/orimod/src/Ori/Cards/Stasis.cs:50`）
- `override bool GainsBlock` — 10
- `CreatureCmd.Heal` — 2（`reference/orimod/src/Ori/Cards/Regenerate.cs:21`、`reference/orimod/src/Ori/Cards/AbstractOriVisualCard.cs:77`）
- `CreatureCmd.LoseMaxHp(..., isFromCard: true)` — 1（`reference/orimod/src/Ori/Cards/EmbraceTheLight.cs:57`）
- `Owner.Creature.SetCurrentHpInternal` / `LoseBlockInternal` / `Block` 直接读写 — 1（`reference/orimod/src/Ori/Cards/Rebirth.cs:66-68,114,124`）

**Power（增益/减益/能力）**
- `PowerCmd.Apply<T>(choiceContext, target, amount, applier, cardSource)` — 泛型固定类型，45 个文件涉及 PowerModel
- `PowerCmd.Apply(choiceContext, powerInstance, target, amount, applier, cardSource)`（实例重载）— 2（`reference/orimod/src/Ori/Cards/ForestVoice.cs:39`、`reference/orimod/src/Ori/Cards/Special/Binding.cs:32`）
- `PowerCmd.Apply<T>(..., silent: true)` — 1（`reference/orimod/src/Ori/Cards/Rebirth.cs:57`）
- `PowerCmd.Remove(power)` / `PowerCmd.Remove<T>(creature)` — 5（例 `reference/orimod/src/Ori/Cards/PureHeart.cs:29`、`reference/orimod/src/Ori/Cards/Antigravity.cs:33`）
- `Creature.GetPower<T>()` — 2（`reference/orimod/src/Ori/Cards/Antigravity.cs:27`、`reference/orimod/src/Ori/Cards/Special/Binding.cs:27`）
- 遍历 `Creature.Powers` + `power.RemoveInternal()` + `AfterRemoved` — 2（`reference/orimod/src/Ori/Cards/PureHeart.cs:27`、`reference/orimod/src/Ori/Cards/Rebirth.cs:145-149`）
- `ModelDb.Power<T>().ToMutable()` + 注入状态 — 1（`reference/orimod/src/Ori/Cards/ForestVoice.cs:37-38`）

**卡牌流转（card movement）**
- `CardPileCmd.Draw(choiceContext, n, Owner)`（返回抽到的卡）— 12（例 `reference/orimod/src/Ori/Cards/CardWheel.cs:23`）
- `CardCmd.Discard(choiceContext, cards)` — 4（例 `reference/orimod/src/Ori/Cards/Farewell.cs:28`）
- `CardCmd.Exhaust(choiceContext, card)` — 3（例 `reference/orimod/src/Ori/Cards/MagicBow.cs:77`）
- `CardPileCmd.Add(cards, PileType)` / `Add(cards, PileType.Draw, CardPilePosition.Top)` — 5（例 `reference/orimod/src/Ori/Cards/Reflux.cs:33,39`）
- `CardPileCmd.AddGeneratedCardToCombat(card, pile, owner)` — 2（`reference/orimod/src/Ori/Cards/Immerse.cs:41`、`reference/orimod/src/Ori/Cards/Special/LookoutsBlessing.cs:60`）
- `CardPileCmd.RemoveFromDeck(this)` — 1（`reference/orimod/src/Ori/Cards/AbstractOriVisualCard.cs:76`）
- `CardCmd.ApplyKeyword(card, CardKeyword.Retain)` — 1（`reference/orimod/src/Ori/Cards/ForestMemory.cs:36`）
- `CardCmd.Transform(card, replacement, CardPreviewStyle.None)` — 1（`reference/orimod/src/Ori/Cards/Special/Temperament.cs:85`）
- `CardCmd.Enchant(enchantment, card, amount)` / `CardCmd.PreviewCardPileAdd` — 1（`reference/orimod/src/Ori/Cards/WispEverlasting.cs:61,71`）
- `Owner.RunState.AddCard(deckCopy, Owner)` + `CardModel.FromSerializable/ToSerializable` + `DeckVersion` 同步 — 1（`reference/orimod/src/Ori/Cards/WispEverlasting.cs:67-70`）
- `CardPile` 内部直接操作（`AddInternal` / `RandomizeOrderInternal` / `InvokeContentsChanged` / `RemoveFromCurrentPile` / `RemoveFromState`）— 1（`reference/orimod/src/Ori/Cards/Rebirth.cs:74-89,175-177`）
- `CombatState.CloneCard` / `RecalculateCardValues` — 1（`reference/orimod/src/Ori/Cards/Rebirth.cs:79,89`）
- `override CardLocation GetResultLocationForCardPlay()` — 2（`reference/orimod/src/Ori/Cards/HandToHand.cs:39`、`reference/orimod/src/Ori/Cards/Rebirth.cs:42`）
- `PileType.Hand/Draw/Discard/Exhaust/Deck/None.GetPile(Owner).Cards` 直接读 — 例 `reference/orimod/src/Ori/Cards/Aetheris.cs:32`

**选择 / 交互（selection / choice）**
- `CardSelectCmd.FromHand(choiceContext, player, prefs, filter, source)` — 12（例 `reference/orimod/src/Ori/Cards/MagicBow.cs:74`）
- `CardSelectCmd.FromSimpleGrid(choiceContext, cards, owner, prefs)`（含弃牌堆与代理卡）— 3（`reference/orimod/src/Ori/Cards/Reflux.cs:33`、`reference/orimod/src/Ori/Cards/Grapple.cs:51`、`reference/orimod/src/Ori/Cards/Meteorite.cs:48`）
- `CardSelectCmd.FromSimpleGridForRewards(...)` — 1（`reference/orimod/src/Ori/Cards/Special/LookoutsBlessing.cs:56`）
- `CardSelectorPrefs(prompt, count)` / `(prompt, min, max)` — 例 `reference/orimod/src/Ori/Cards/Aetheris.cs:36`（0..count）
- `LocString("cards", Id.Entry + ".selectionScreenPrompt")` 运行时构造提示文本 — 例 `reference/orimod/src/Ori/Cards/ForestMemory.cs:33`
- `CombatState.CreateCard<T>(Owner)` 运行时构造卡（含 UI 代理卡）— 3（`reference/orimod/src/Ori/Cards/Grapple.cs:46`、`reference/orimod/src/Ori/Cards/Meteorite.cs:43`、`reference/orimod/src/Ori/Cards/Special/LookoutsBlessing.cs:19-25`）
- `CardPlay.Target.Player` / `CardPlay.Player` / `CardPlay.ResultPile` / `CardPlay.IsFirstInSeries` 读 — 例 `reference/orimod/src/Ori/Cards/SwitchPlaces.cs:20`、`reference/orimod/src/Ori/Cards/HandToHand.cs:57`、`reference/orimod/src/Ori/Cards/Vigil.cs:28`

**能量 / 费用（energy / cost）**
- `PlayerCmd.GainEnergy(amount, Owner)` — 6（例 `reference/orimod/src/Ori/Cards/Massing.cs:35`）
- `PlayerCmd.LoseEnergy(amount, Owner)` — 1（`reference/orimod/src/Ori/Cards/SunMoonRadiance.cs:33`）
- `PlayerCmd.EndTurn(Owner, canBackOut: false)` — 1（`reference/orimod/src/Ori/Cards/Regenerate.cs:22`）
- `PlayerCombatState.Energy` / `.MaxEnergy` 读 — 2（`reference/orimod/src/Ori/Cards/EnergyCell.cs:27-28`、`reference/orimod/src/Ori/Cards/Lifepact.cs:33`）
- `EnergyCost.UpgradeBy(-1)` — 29
- `EnergyCost.AddThisCombat(1)`（本战斗动态改费）— 1（`reference/orimod/src/Ori/Cards/Aetheris.cs:43`）

**DynamicVars / 数值声明**
- `CanonicalVars` 覆写 — 91
- 变量类型：`DamageVar`、`BlockVar`、`PowerVar<T>`、`CardsVar`、`EnergyVar`、`IntVar`、`HealVar`、`HpLossVar`、`MaxHpVar`、`StringVar`、`DynamicVar` — 例 `reference/orimod/src/Ori/Cards/Lifepact.cs:17-22`、`reference/orimod/src/Ori/Cards/Special/MarkPoint.cs:23-28`
- 计算变量：`CalculationBaseVar` / `CalculationExtraVar` / `CalculatedDamageVar` / `CalculatedBlockVar` / `CalculatedVar` + `.WithMultiplier(lambda)` — 5 个文件用 lambda（`Feathers.cs:25`、`Intertwine.cs:58`、`Irresistible.cs:25`、`Stasis.cs:40`、`Tread.cs:28`）
- 运行时改写 canonical var：`DynamicVars.Damage.BaseValue += ...` / `DynamicVars["x"].BaseValue = ...` — 3（`reference/orimod/src/Ori/Cards/Adjudication.cs:53`、`reference/orimod/src/Ori/Cards/Special/MarkPoint.cs:44`、`reference/orimod/src/Ori/Cards/Special/MeteoritePoint.cs:64-65`）
- `UpgradeValueBy(...)` — 65；`EnergyCost.UpgradeBy(...)` — 29

**关键词 / 标签（keywords / tags）**
- `AddKeyword(CardKeyword)` / `RemoveKeyword(...)`（升级期改关键词）— 12（例 `reference/orimod/src/Ori/Cards/ElfForm.cs:32`、`reference/orimod/src/Ori/Cards/WindSpirit.cs:44`）
- `CanonicalKeywords` 覆写 — 45
- `ExtraTags` / `CanonicalTags` 覆写 — 46 / 2（`reference/orimod/src/Ori/Cards/StrikeOri.cs:14`、`reference/orimod/src/Ori/Cards/DefendOri.cs:15`）
- 原版 Keyword 使用：`Exhaust`、`Ethereal`、`Innate`、`Retain` — 例 `reference/orimod/src/Ori/Cards/Farewell.cs:14`、`reference/orimod/src/Ori/Cards/Dignified.cs:18`、`reference/orimod/src/Ori/Cards/SpiritWell.cs:30`、`reference/orimod/src/Ori/Cards/AncientForce.cs:30`

**引擎钩子 / 时序（hooks / overrides）**
- `Hook.ModifyDamage` — 2（见上）
- `Hook.AfterCurrentHpChanged` / `Hook.AfterCardChangedPiles` — 1（`reference/orimod/src/Ori/Cards/Rebirth.cs:94,116,176`）
- `CombatManager.Instance.History.CardPlaysFinished` + `CardPlayFinishedEntry.HappenedThisTurn` — 3（`reference/orimod/src/Ori/Cards/Irresistible.cs:25`、`reference/orimod/src/Ori/Cards/Vigil.cs:28`）
- `CombatManager.Instance.CurrentCombatId` / `IsCurrentLiveCombat` — 1（`reference/orimod/src/Ori/Cards/HandToHand.cs:61,67`）
- `override bool IsPlayable` — 13；`override bool ShouldGlowRedInternal` — 10
- `override Task OnTurnEndInHand` + `HasTurnEndInHandEffect` — 1（`reference/orimod/src/Ori/Cards/Immerse.cs:22,29`）
- `override Task AfterCardExhausted(choiceContext, card, causedByEthereal)` — 1（`reference/orimod/src/Ori/Cards/Immerse.cs:34`）
- `override Task AfterPreventingDeath(Creature)` — 1（`reference/orimod/src/Ori/Cards/AbstractOriVisualCard.cs:72`）
- `override void AfterDowngraded()` — 1（`reference/orimod/src/Ori/Cards/Adjudication.cs:66`）
- `override decimal ModifyDamageMultiplicative(...)` — 1（`reference/orimod/src/Ori/Cards/RoundhouseKick.cs:40`）
- `override CardLocation GetResultLocationForCardPlay()` — 2（见上）
- `override IEnumerable<IHoverTip> ExtraHoverTips` — 38（含 `HoverTipFactory.FromPower/FromKeyword/FromCard/FromEnchantment`）
- `[SavedProperty]` 持久化字段 — 3（`reference/orimod/src/Ori/Cards/AbstractOriVisualCard.cs:21`、`reference/orimod/src/Ori/Cards/WorldUnfurled.cs:22`）

**表现层 / 多人（presentation / multiplayer）**
- `CreatureCmd.TriggerAnim(creature, "Cast", Owner.Character.CastAnimDelay)` — 17（例 `reference/orimod/src/Ori/Cards/Seraph.cs:27`）
- Godot 节点树与全屏 VFX：`NRun.Instance.GlobalUi`、`NSmokyVignetteVfx.Create`、`PlayerFullscreenHealVfx.Play`、`NCard.FindOnTable`、`NCombatRoom.Instance.Ui.Hand.Remove`、`GetParent().RemoveChildSafely`、`QueueFreeSafely`、`new Color(...)` — 1（全部集中在 `reference/orimod/src/Ori/Cards/Rebirth.cs:131,139,162-173`）
- 动画/等待：`Cmd.CustomScaledWait(a,b)`、`Cmd.Wait(seconds)` — 1（`reference/orimod/src/Ori/Cards/Rebirth.cs:62,92,95,117`）
- `override CardMultiplayerConstraint => MultiplayerOnly` — 5（`reference/orimod/src/Ori/Cards/SwitchPlaces.cs:11`、`WispFlight.cs:14`、`HandToHand.cs:18`、`EmbraceTheLight.cs:18`、`Special/Binding.cs:13`）
- BaseLib 接口：`ITomeCard`（`reference/orimod/src/Ori/Cards/Torrent.cs:11`）、`ITranscendenceCard`（`reference/orimod/src/Ori/Cards/Lightness.cs:14`）
- 资源/纹理：`CardImagePath()` / `BigCardImagePath()`（`reference/orimod/src/Ori/Cards/AbstractOriVisualCard.cs:61-63`）、`ResourceLoader.Load<Texture2D>`（`reference/orimod/src/Ori/CardPools/AbstractOriVisualCardPool.cs:33`）

### 7.5 需要"覆写基类行为"而非声明效果的卡牌

这是 DSL 最难覆盖的一类。计数（各条互不排斥）：

```pwsh
$files = Get-ChildItem -Path "reference\orimod\src\Ori\Cards" -Recurse -File -Filter *.cs
'IsPlayable:            ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'override bool IsPlayable' }).Count
'ShouldGlowRedInternal: ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'ShouldGlowRedInternal' }).Count
'CanonicalKeywords:     ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'CanonicalKeywords' }).Count
'ExtraTags:             ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'ExtraTags' }).Count
'MaxUpgradeLevel:       ' + ($files | Where-Object { (Get-Content $_.FullName -Raw) -match 'MaxUpgradeLevel' }).Count
```
结果：`IsPlayable` 13、`ShouldGlowRedInternal` 10、`CanonicalKeywords` 45、`ExtraTags` 46、`MaxUpgradeLevel` 11、
含 `Hook.` 直接调用 3、含非 `OnPlay` 行为钩子的文件共 6 个（`Immerse`、`Rebirth`、`Adjudication`、`RoundhouseKick`、
`HandToHand`，以及基类 `AbstractOriVisualCard`；覆写点分别为 `OnTurnEndInHand`+`AfterCardExhausted`、`Hook.*`、
`AfterDowngraded`、`ModifyDamageMultiplicative`、`GetResultLocationForCardPlay`、`AfterPreventingDeath`）。

### 7.6 用了 lambda / 复合公式的卡牌

```pwsh
$files = Get-ChildItem -Path "reference\orimod\src\Ori\Cards" -Recurse -File -Filter *.cs
$files | Where-Object { (Get-Content $_.FullName -Raw) -match 'WithMultiplier|\.Calculate\(' } | ForEach-Object { $_.Name }
```
结果 5 张：`Feathers.cs`、`Intertwine.cs`、`Irresistible.cs`、`Stasis.cs`、`Tread.cs`；
再加手工算式卡：`Adjudication.cs:52`、`Brilliance.cs:47`、`LifeCell.cs:26`、`Tread.cs:53`、`Meteorite.cs:79-82`、
`Special/MeteoritePoint.cs:73-76`、`RoundhouseKick.cs:56`、`Aetheris.cs:41`、`MagicBow.cs:81`、`Skyrocket.cs:38`。

---

## 8. 不确定 / 需要确认的点

以下是读源码时无法从卡牌文件本身确定的事情，明确标出而不是猜：

1. **`ForestEyes.cs` 是空卡**（只有 ctor，无 `CanonicalVars`、无 `OnPlay`、无关键词）。无法判断这是有意为之（纯占位/由外部系统赋予行为）还是未完成的实现。它被算入"可用空效果列表表达"。
2. **11 张 Concepts 卡全部是空 `OnPlay`**。它们带 `OriKeywords.ConceptCard` 且有描述用 `DynamicVars`，但行为不在这 131 个文件里。它们真正的效果位置我没有在本次范围内追踪。
3. **自定义 Keyword 的运行时语义不在卡牌层**。`Return`、`Reset`、`Momentum`、`Bash`、`Temperament`、`WeaponSwitch`、`SpiritEdgeOnly`、`SpiritArcOnly`、`MeleeWeaponOnly`、`RangedWeaponOnly`、`Aloft`、`Judgment`、`LastLight` 的行为实现在 `Ori/Powers/`、`Ori/Aloft/`、`Ori/Abilities/`、`Ori/Hooks/` 等处。我确认了 `LastLight` 由 `AbstractOriVisualCard.AfterPreventingDeath` 实现，其余 Keyword 的具体实现位置未逐一核实。
4. **`wisp` 系统与卡牌层没有直接耦合**。卡牌只施加 `WispsWillPower`。我未追踪 `Ori/Wisps/`（10 个文件）内部，因此不声称它需要哪些引擎能力。
5. **`AloftSystem.GetPlacements` 与 `OriPlacementStateSystem.GetPlacements` 两套枚举入口并存**。卡牌有的用前者（`Grapple.cs:68`、`Tread.cs:61`、`Meteorite.cs:64`），有的直接用后者（没有在卡片中直接出现，`Meteorite.cs` 实际走 `AloftSystem.GetPlacements`）。二者是否需要不同的 DSL 表述，取决于放置物的存储位置，我没有进一步区分。
6. **`CardRarity.Ancient / Event / Token / Curse`、`CardType.Quest`、`TargetType.None / AnyAlly / RandomEnemy`、`CardKeyword.Exhaust / Ethereal / Innate / Retain`、`CardTag.Strike / Defend` 全部是原版枚举**，依据 `sts2/src/Core/Entities/Cards/*.cs`。因此本报告不把它们计为"自定义类型"。
7. **`TempStrengthPower` 不在 `sts2/` 里但在 `Ori/Powers/` 里**，所以它是 mod Power。这一点是通过 `Get-ChildItem sts2 -Filter TempStrengthPower.cs` 无命中、而 `Ori/Powers/` 有 `TempStrengthPower.cs` 判定的。
8. **统计口径为"文件包含该模式字符串"**，不是语义级引用计数（例如 `AbstractOriVisualCard.cs` 计入 `OriKeywords` 是因为它读 `OriKeywords.LastLight`）。数字用于排序和量级判断，个别 ±1 差异不影响结论。
