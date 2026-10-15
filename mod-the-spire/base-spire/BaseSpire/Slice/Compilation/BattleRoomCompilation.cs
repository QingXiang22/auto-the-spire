using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class BattleRoomCompilation
{
    internal static RoomTransitionBindings RoomTransition(
        PackageBuildOptions options,
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        RunStateBindings runStateBindings,
        EncounterPoolBindings encounterPoolBindings,
        RunEncountersBindings runEncountersBindings,
        RunCollectionsBindings runCollectionsBindings,
        RoomServicesBindings roomServicesBindings,
        KnowledgeCommandsBindings knowledgeCommandsBindings,
        EnemySelectionBindings enemySelectionBindings,
        PlayerTurnBindings playerTurnBindings,
        EnemyOfCallback EnemyOf,
        DispatchCardHookCallback DispatchCardHook,
        MonsterFieldsCallback MonsterFields,
        SpawnStartingPowersCallback SpawnStartingPowers,
        InnateCallback Innate)
    {
        var formationLocalNames = new CompilationSlots();

        // -- W-05：进入下一间房（领域流程；只走作者路径） ---------------------------------------------------
        // 遭遇顺序按来源：Calcified Cultist 站在 Damp Cultist 前面；测试旋钮只�?*创建顺序**对调�?
        // W-02 同定义敌人对：两个敌人都�?Calcified Cultist 的定义，只有出生生命不同�?0 前排 / 10 后排）�?
        // 固定遭遇只在**没有正式遭遇�?*时使用（受控切片 / `.author` 原型）；正式构建的敌人由池决定，
        // 因此这里不再请求这两个固定定义——正式包里它们可能根本不存在，缺了就该是"内容不在包里"�?
        // 而不是构建期�?First() 崩溃�?
        var fixedFront = options.EncounterPool is null ? EnemyOf(SliceManifest.CalcifiedCultistId) : null;

        var fixedBack = options.EncounterPool is not null
            ? null
            : options.SameDefinitionEnemyPair
                ? fixedFront
                : EnemyOf(SliceManifest.DampCultistId);

        // 一间房的领域生命周期：
        //   StartNextRoom（房间号 +1 �?清掉上一间房的敌�?能力/战斗�?已知事实 �?玩家从整局状态重�?�?
        //   重建敌人 �?**按长期牌组逐张复制**战斗�?�?开回合�?
        //   �?战斗 �?SettleBattle（按房间幂等写回）�?
        //
        // 关键语义（对应验收）�?
        //   * 第二间房�?HP 来自 `Run.Hp`（持久），不来自作者声明的初始值；
        //   * 第二间房的战斗牌�?*长期实例的字段级拷贝**（升�?数�?实例变量随长期实例走），
        //     因此"长期牌组里升级过的牌"会在下一间房生效�?
        //   * 战斗里生�?克隆的临时牌只存在于战斗牌区，开下一间房时被销毁，不污染长期牌组；
        //   * 结算守卫�?`Run.CurrentRoom` 区分，所以第二间房可以再结算一次（重复结算仍是空操作）�?
        ProcedureBuilder? startNextRoom = null;

        if (runStateBindings.HasRun)
        {
            startNextRoom = contentBindings.Package.Procedure("StartNextRoom");
            var roomRun = startNextRoom.First(startNextRoom.Collection(runCollectionsBindings.RunStates!));
            var roomPlayer = startNextRoom.First(startNextRoom.Type(playerBindings.Player, battleBindings.BattleScope));
            var roomScope = startNextRoom.EntityScope(roomPlayer);
            var roomMonster = startNextRoom.Local("monster", FieldTypeSchema.Binding());
            var roomCreated = startNextRoom.Local("created", FieldTypeSchema.Binding());

            // 战斗�?= 长期实例�?*字段级拷�?*。临时费用修正（CostModifier）在战斗之间清零（它�?本回�?的）�?
            (FieldSymbol Field, Expr Value)[] CopyCardFields(Expr longCard)
                => cardsBindings.CardDefinitions.Copy(startNextRoom, longCard, clone: false);

            // `on_enter_combat`：拷贝出来的实例�?*它自己的 DefinitionId** 分派（动态，与敌人行动同一套写法）�?
            Inst EnterCombatDispatch(Expr cardRef) => startNextRoom.If(startNextRoom.Const(true),
                DispatchCardHook(startNextRoom, cardRef, AuthorSteps.HookOnEnterCombat));

            /// <summary>
            /// 出生**一�?*敌人。两种形状：
            /// <list type="bullet">
            /// <item>受控切片�?c>spawnHpStream == null</c>）：字段全是常量，取声明范围下限，形状逐字不变�?
            ///       测试旋钮 <paramref name="spawnHpOverride"/> 仍然生效�?/item>
            /// <item>正式构建：按来源掷出生生命（�?
            ///       <see cref="SpawnHpSteps"/>），Hp �?MaxHp **同为掷出的�?*�?/item>
            /// </list>
            /// </summary>
            Inst[] SpawnMonster(IrScope scope, SliceEnemy definition, Expr scopeExpr, int resultSlot,
                CollectionSymbol collection, int? spawnHpOverride = null)
            {
                if (options.SpawnHpStream is null || spawnHpOverride is not null)
                {
                    return
                    [
                        scope.CreateEntity(enemiesBindings.Monster, scopeExpr, resultSlot,
                            MonsterFields(scope, definition, spawnHpOverride), collection),
                        // BASE-02 批 7：出生时挂上声明的能力（`startingPowers`）。
                        .. SpawnStartingPowers(scope, definition, resultSlot),
                        // BASE-02 批 1：出生后的第一次选取（原版 `CombatManager.AfterCreatureAdded` 的 `RollMove`）：
                        // 初始状态是行动时不掷点（`_performedFirstMove == false && IsMove` 提前返回），
                        // 初始状态是分支时就掷一次（`Flyconid` / `TwoTailedRat` 的形状）。
                        SpawnSelect(scope, resultSlot),
                    ];
                }

                var hpSlot = scope.Local("spawnHp", FieldTypeSchema.Int(0, 9999));
                return
                [
                    .. SpawnHpSteps(scope, definition, hpSlot),
                    scope.CreateEntity(enemiesBindings.Monster, scopeExpr, resultSlot,
                        MonsterFields(scope, definition, hpValue: scope.LocalRef(hpSlot)), collection),
                    .. SpawnStartingPowers(scope, definition, resultSlot),
                    SpawnSelect(scope, resultSlot),
                ];
            }

            /// <summary>
            /// 出生时挂上声明的能力（`startingPowers`）：用 `Build` 层的同名函数（两条出生路径共用）。
            /// </summary>

            /// <summary>出生时按状态机选取一次行动（没有状态机敌人时是空操作）�?/summary>
            Inst SpawnSelect(IrScope scope, int resultSlot) => enemySelectionBindings.SelectEnemyMove is null
                ? scope.If(scope.Const(false))
                : scope.Call(enemySelectionBindings.SelectEnemyMove, scope.LocalRef(resultSlot));

            /// <summary>
            /// 原版**出生生命掷点**（`Creature.SetUniqueMonsterHpValue`，`Creature.cs:371-383`）：
            /// <list type="number">
            /// <item>候�?= 声明范围 <c>[min, max]</c>�?*双端包含**：`Enumerable.Range(min, max+1-min)`）；</item>
            /// <item>移除**同一�?*其他生物已经占用的生命值（`hashSet.ExceptWith(…e.MaxHp)`）—�?
            ///       本包在出生时 Hp == MaxHp，因此读已出生敌人的 Hp 即等价；</item>
            /// <item>恰好�?*一�?* `Niche` 流：候选非�?�?`NextInt(0, count)` 取升序候选里的第 k 个；
            ///       候选为�?�?`NextInt(min, max+1)`（仍然是这范围）�?/item>
            /// </list>
            /// 抽取与判断的顺序按来源；这条掷点�?*创建生物**时消费，与遭遇抽取是两条流�?
            /// </summary>
            Inst[] SpawnHpSteps(IrScope scope, SliceEnemy definition, int hpSlot)
            {
                var min = (long)definition.NormalMinHp;
                var span = (long)(definition.NormalMaxHp - definition.NormalMinHp + 1);
                var cursorValue = scope.Local("spawnHpValue", FieldTypeSchema.Int(0, 9999));
                var available = scope.Local("spawnHpAvailable", FieldTypeSchema.Int(0, 999));
                var index = scope.Local("spawnHpIndex", FieldTypeSchema.Int(0, 999));
                var taken = scope.Local("spawnHpTaken", FieldTypeSchema.Int(0, 999));
                Expr Used(Expr value) => scope.Exists(scope.Where(scope.Collection(pilesBindings.Monsters),
                    existing => (Expr)scope.Equal(scope.Field(existing, enemiesBindings.MonsterHp), value)));
                // 候选计数：升序扫一遍范围，跳过已被占用的值�?
                Inst[] Count(int target) =>
                [
                    scope.SetLocal(target, scope.Const(0L)),
                    scope.SetLocal(cursorValue, scope.Const(min)),
                    scope.Repeat(scope.Const(span),
                        scope.If(scope.Not(Used(scope.LocalRef(cursorValue))),
                            scope.SetLocal(target, scope.Add(scope.LocalRef(target), scope.Const(1L)))),
                        scope.SetLocal(cursorValue, scope.Add(scope.LocalRef(cursorValue), scope.Const(1L)))),
                ];
                if (span <= 0)
                {
                    // 声明�?min > max：这是内容错误，不静默取一个数�?
                    throw Unsupported($"enemy '{definition.ContentId}'",
                        $"出生生命范围无效（{min}..{definition.NormalMaxHp}）");
                }

                return
                [
                    .. Count(available),
                    scope.IfElse(
                        scope.Equal(scope.LocalRef(available), scope.Const(0L)),
                        // 全被占用：源码仍抽一�?`NextInt(min, max+1)`�?
                        [scope.SampleInt(options.SpawnHpStream!, scope.Const(min), scope.Const(min + span), hpSlot)],
                        [
                            // 候选非空：抽第 k 个（升序候选里的下标）�?
                            scope.SampleInt(options.SpawnHpStream!, scope.Const(0L), scope.LocalRef(available), index),
                            scope.SetLocal(taken, scope.Const(0L)),
                            scope.SetLocal(hpSlot, scope.Const(min)),
                            scope.SetLocal(cursorValue, scope.Const(min)),
                            scope.Repeat(scope.Const(span),
                                scope.If(scope.Not(Used(scope.LocalRef(cursorValue))),
                                    scope.If(scope.Equal(scope.LocalRef(taken), scope.LocalRef(index)),
                                        scope.SetLocal(hpSlot, scope.LocalRef(cursorValue))),
                                    scope.SetLocal(taken, scope.Add(scope.LocalRef(taken), scope.Const(1L)))),
                                scope.SetLocal(cursorValue, scope.Add(scope.LocalRef(cursorValue), scope.Const(1L)))),
                        ]),
                ];
            }

            /// <summary>
            /// **编队出生程序**（`formations` / `enemySlots`）。逐条对照各遭遇自己的 `GenerateMonsters()`：
            /// <list type="number">
            /// <item>抽编队：`Rng.NextItem(formations)`（本内容权重全 1 ⇒ 均匀一次抽取；权重不一致时构建期拒绝）；</item>
            /// <item>槽位按**组块**顺序抽：同一个 `uniqueChoiceGroup` 的槽位在第一个成员处连续抽完
            ///       （`SlimesWeak.cs:51-57`：先挑两个小史莱姆、再挑中等史莱姆）；</item>
            /// <item>`chooseRandomly` 槽：在"组内尚未选中"的候选里抽一次（`NextItem`）；
            ///       非随机槽取唯一候选（0 次抽取）；</item>
            /// <item>最后按**名单/槽位顺序**逐名出生（抽取顺序不影响出生顺序，因此阵容位次与源一致）；</item>
            /// <item>抽取用**遭遇派生流**（`(遭遇, 楼层)` 一条，种子来源见
            ///       <see cref="VanillaRandomLayout.EncounterSeed"/>）；楼层取 `Run.CurrentRoom`
            ///       （地图未接入时的显式替代）。</item>
            /// </list>
            /// </summary>
            Inst FormationSpawn(ProcedureBuilder room, SliceEncounter encounter)
            {
                var formations = encounter.EffectiveFormations;
                if (formations.Count > 1 && formations.Select(entry => entry.Weight).Distinct().Count() > 1)
                {
                    throw Unsupported($"encounter '{encounter.EncounterId}'",
                        "编队权重不一致：源码是 `NextItem` 的均匀抽取，加权编队不在本批范围内");
                }

                var steps = new List<Inst>();
                var entry = VanillaRandomLayout.SourceEntry(encounter.EncounterId);
                var rooms = options.EncounterPool!.RoomCount;
                var floor = room.Field(roomRun, runStateBindings.RunCurrentRoom!);

                // ① 本间房的派生流名（楼层是运行期值 ⇒ 在 1..房间数 上分派一次；流已在建局时种好）。
                var streamSlot = room.Local($"encounterStream{formationLocalNames.Next()}", FieldTypeSchema.String(64));
                steps.Add(room.SetLocal(streamSlot,
                    room.Const(VanillaRandomLayout.EncounterStreamName(entry, rooms))));
                for (var candidate = rooms - 1; candidate >= 1; candidate--)
                {
                    steps.Add(room.If(room.Equal(floor, room.Const((long)candidate)),
                        room.SetLocal(streamSlot,
                            room.Const(VanillaRandomLayout.EncounterStreamName(entry, candidate)))));
                }

                // ② 抽编队（只有一条编队时不抽数）。
                var formationSlot = room.Local($"encounterFormation{formationLocalNames.Next()}",
                    FieldTypeSchema.Int(0, 99));
                if (formations.Count == 1)
                {
                    steps.Add(room.SetLocal(formationSlot, room.Const(0L)));
                }
                else
                {
                    steps.Add(room.SampleInt(room.LocalRef(streamSlot), room.Const(0L),
                        room.Const((long)formations.Count), formationSlot));
                }

                steps.Add(FormationBranch(room, 0));
                return room.If(room.Const(true), steps.ToArray());

                Inst FormationBranch(ProcedureBuilder scope, int index)
                {
                    var body = FormationBody(scope, formations[index], streamSlot, encounter.EncounterId);
                    var condition = scope.Equal(scope.LocalRef(formationSlot), scope.Const((long)index));
                    return index == formations.Count - 1
                        ? scope.If(condition, body)
                        : scope.IfElse(condition, body, [FormationBranch(scope, index + 1)]);
                }
            }

            /// <summary>一条编队的出生程序：先定槽位选择（按组块顺序），再按名单顺序出生。</summary>
            Inst[] FormationBody(ProcedureBuilder scope, EncounterFormation formation, int streamSlot,
                string encounterId)
            {
                var steps = new List<Inst>();
                if (formation.EnemyIds.Count > 0)
                {
                    steps.AddRange(formation.EnemyIds.SelectMany(enemyId =>
                        SpawnMonster(scope, EnemyOf(enemyId), roomScope, roomMonster, pilesBindings.Monsters)));
                    return steps.ToArray();
                }

                var picks = formation.Slots
                    .Select(_ => scope.Local($"slotPick{formationLocalNames.Next()}", FieldTypeSchema.String(64)))
                    .ToArray();
                // 组内"已被更早槽位选中"的判据 = 与那些槽位**已经写好的 picks 局部量**逐个比较。
                // 不用可变的 used 标志：一个组的成员槽位共享同一份候选，标志一旦算错就会让后续槽位
                // 的过滤后候选数变成 0（`ruby-raiders-normal` 三个槽正是这种共享）。
                var groupPicks = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                foreach (var index in GroupBlockOrder(formation.Slots))
                {
                    var slot = formation.Slots[index];
                    if (!slot.ChooseRandomly)
                    {
                        if (slot.EnemyChoices.Count != 1)
                        {
                            throw Unsupported($"encounter '{encounterId}'",
                                "非随机槽有多个候选：语义不明确（源里每个槽要么单候选、要么随机）");
                        }

                        steps.Add(scope.SetLocal(picks[index], scope.Const(slot.EnemyChoices[0])));
                        continue;
                    }

                    var grouped = slot.UniqueChoiceGroup.Length > 0;
                    var earlier = grouped && groupPicks.TryGetValue(slot.UniqueChoiceGroup, out var sameGroup)
                        ? sameGroup
                        : [];
                    // 候选 c 是否**仍可选**：不被本组任何更早槽位选中（判据来自那些槽位已写好的 picks）。
                    Expr Eligible(ProcedureBuilder builder, string choice) => earlier.Count == 0
                        ? builder.Const(true)
                        : earlier
                            .Select(pick => (Expr)builder.Not(builder.Equal(builder.LocalRef(pick),
                                builder.Const(choice))))
                            .Aggregate((left, right) => builder.And(left, right));
                    var eligible = scope.Local($"eligible{formationLocalNames.Next()}", FieldTypeSchema.Int(0, 99));
                    var drawn = scope.Local($"drawn{formationLocalNames.Next()}", FieldTypeSchema.Int(0, 99));
                    steps.Add(scope.SetLocal(eligible, scope.Const((long)slot.EnemyChoices.Count)));
                    foreach (var choice in slot.EnemyChoices)
                    {
                        steps.Add(scope.If(scope.Not(Eligible(scope, choice)),
                            scope.SetLocal(eligible,
                                scope.Subtract(scope.LocalRef(eligible), scope.Const(1L)))));
                    }

                    steps.Add(scope.SampleInt(scope.LocalRef(streamSlot), scope.Const(0L),
                        scope.LocalRef(eligible), drawn));
                    steps.Add(scope.SetLocal(picks[index], scope.Const(string.Empty)));
                    if (!grouped)
                    {
                        // 无唯一组 ⇒ 没有过滤，`NextItem(list)` 就是**按抽出的下标直接取候选**
                        //（不需要游标走位：走位只用于"跳过已用候选"的过滤序列）。
                        for (var candidate = 0; candidate < slot.EnemyChoices.Count; candidate++)
                        {
                            steps.Add(scope.If(scope.Equal(scope.LocalRef(drawn), scope.Const((long)candidate)),
                                scope.SetLocal(picks[index], scope.Const(slot.EnemyChoices[candidate]))));
                        }

                        continue;
                    }

                    var cursor = scope.Local($"cursor{formationLocalNames.Next()}", FieldTypeSchema.Int(0, 99));
                    var taken = scope.Local($"taken{formationLocalNames.Next()}", FieldTypeSchema.Bool());
                    steps.Add(scope.SetLocal(cursor, scope.Const(0L)));
                    steps.Add(scope.SetLocal(taken, scope.Const(false)));
                    for (var candidate = 0; candidate < slot.EnemyChoices.Count; candidate++)
                    {
                        var choice = slot.EnemyChoices[candidate];
                        Inst[] take =
                        [
                            scope.If(scope.Equal(scope.LocalRef(cursor), scope.LocalRef(drawn)),
                            [
                                scope.SetLocal(picks[index], scope.Const(choice)),
                                scope.SetLocal(taken, scope.Const(true)),
                            ]),
                            scope.If(scope.Not(scope.LocalRef(taken)),
                                scope.SetLocal(cursor,
                                    scope.Add(scope.LocalRef(cursor), scope.Const(1L)))),
                        ];
                        steps.Add(scope.If(Eligible(scope, choice), take));
                    }

                    if (!groupPicks.TryGetValue(slot.UniqueChoiceGroup, out var groupList))
                    {
                        groupList = [];
                        groupPicks[slot.UniqueChoiceGroup] = groupList;
                    }

                    groupList.Add(picks[index]);
                }

                // 出生：按**名单顺序**（与源的 `list` 顺序一致），位次因此可复现。
                for (var index = 0; index < formation.Slots.Count; index++)
                {
                    var pick = picks[index];
                    var choices = formation.Slots[index].EnemyChoices;
                    steps.Add(SpawnByPick(scope, choices, pick, 0));
                }

                return steps.ToArray();
            }

            /// <summary>按选中的敌人 id 出生：候选 id → 出生程序的 if 链（最后一个分支显式失败）。</summary>
            Inst SpawnByPick(ProcedureBuilder scope, IReadOnlyList<string> choices, int pickSlot, int index)
            {
                var condition = scope.Equal(scope.LocalRef(pickSlot), scope.Const(choices[index]));
                var body = SpawnMonster(scope, EnemyOf(choices[index]), roomScope, roomMonster, pilesBindings.Monsters);
                return index == choices.Count - 1
                    ? scope.IfElse(condition, body,
                        [scope.Fail($"编队槽没有选中任何候选（'{choices[index]}' 是最后一个）")])
                    : scope.IfElse(condition, body, [SpawnByPick(scope, choices, pickSlot, index + 1)]);
            }

            /// <summary>槽位处理顺序：同一个唯一组的槽位在**第一个成员的位置**连续处理，其余保持原顺序。</summary>
            static IReadOnlyList<int> GroupBlockOrder(IReadOnlyList<EncounterSlot> slots)
            {
                var order = new List<int>();
                var done = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < slots.Count; index++)
                {
                    var group = slots[index].UniqueChoiceGroup;
                    if (group.Length == 0)
                    {
                        order.Add(index);
                        continue;
                    }

                    if (!done.Add(group))
                    {
                        continue;
                    }

                    for (var other = index; other < slots.Count; other++)
                    {
                        if (string.Equals(slots[other].UniqueChoiceGroup, group, StringComparison.Ordinal))
                        {
                            order.Add(other);
                        }
                    }
                }

                return order;
            }

            /// <summary>
            /// 本间房的敌人。
            ///
            /// 受控切片（无遭遇池）：固定两个 Cultist，形状逐字不变。
            /// 正式构建（有遭遇池）：按房间号从**幕开始时已经生成好的整幕队列**里取本间房的遭遇
            /// （`QueueIndex == 房间号 - 1`；队列由 `EnqueueProcedure` 按原版 GrabBag + 标签回避生成），
            /// 再按该 id 分派出生哪些敌人。进入房间时**不抽随机数**（编队抽取只消费遭遇派生流）。
            /// </summary>
            Inst SpawnEncounter(ProcedureBuilder room)
            {
                if (options.EncounterPool is null || runStateBindings.RunEncounter is null || encounterPoolBindings.EncounterQueue is null
                    || encounterPoolBindings.QueueIndex is null || encounterPoolBindings.QueueEncounterId is null || runEncountersBindings.RunNormalVisited is null)
                {
                    var fixedSteps = new List<Inst>
                    {
                        room.CreateEntity(enemiesBindings.Monster, roomScope, roomMonster,
                            MonsterFields(room, fixedFront!, options.SameDefinitionEnemyPair ? 20 : null), pilesBindings.Monsters),
                    };
                    fixedSteps.AddRange(SpawnStartingPowers(room, fixedFront!, roomMonster));
                    fixedSteps.Add(room.CreateEntity(enemiesBindings.Monster, roomScope, roomMonster,
                        MonsterFields(room, fixedBack!, options.SameDefinitionEnemyPair ? 10 : null), pilesBindings.Monsters));
                    fixedSteps.AddRange(SpawnStartingPowers(room, fixedBack!, roomMonster));
                    return room.If(room.Const(true), fixedSteps.ToArray());
                }

                var encounters = options.EncounterPool.AllEncounters;
                // 房型来自**实际房型**（源 `RoomType`）：直接战斗/精英节点是自己的类型，问号房节点
                // 保持 `Unknown` 而实际房型可能是 Monster/Elite。只看节点类型会让问号房判出的战斗
                // 拿不到自己的遭遇队列计数（`RunNormalVisited` 不前进）。
                Expr IsMonsterRoom() => roomServicesBindings.ActMap is null
                    ? room.Const(true)
                    : roomServicesBindings.ActMap.AtRoomType(room, roomRun, "Monster");
                Expr IsEliteRoom() => roomServicesBindings.ActMap is null
                    ? room.Const(false)
                    : roomServicesBindings.ActMap.AtRoomType(room, roomRun, "Elite");
                // 源 `RoomSet.NextNormalEncounter` / `NextEliteEncounter`：本相位的队列按
                // **本相位的访问计数**取模索引（不是全局房间号，也不互相推进）。
                Inst PickFrom(CollectionSymbol queue, FieldSymbol index, FieldSymbol encounterId, Expr visited) =>
                    room.ForEach(room.Collection(queue), entry => new[]
                    {
                        room.If(room.Equal(room.Field(entry, index), visited),
                            room.SetField(roomRun, runStateBindings.RunEncounter, room.Field(entry, encounterId))),
                    });
                var bossEncounterId = roomServicesBindings.ActMap is null
                    ? null
                    : options.EncounterPool.Bosses.FirstOrDefault()?.EncounterId;
                var steps = new List<Inst>
                {
                    // 本间房还没取到遭遇时保持空串：分派链的兜底分支会显式失败，而不凭空生成空房间。
                    room.SetField(roomRun, runStateBindings.RunEncounter, room.Const(string.Empty)),
                    room.IfElse(IsEliteRoom(), new Inst[]
                    {
                        encounterPoolBindings.EliteEncounterQueue is not null && encounterPoolBindings.EliteQueueIndex is not null
                            && encounterPoolBindings.EliteQueueEncounterId is not null && runEncountersBindings.RunEliteVisited is not null
                                ? PickFrom(encounterPoolBindings.EliteEncounterQueue, encounterPoolBindings.EliteQueueIndex, encounterPoolBindings.EliteQueueEncounterId,
                                    room.Field(roomRun, runEncountersBindings.RunEliteVisited))
                                : room.Fail("精英房的遭遇队列未接入：Start 初始化与消费端不一致"
                                    + "（不静默退回普通队列）"),
                    }, new Inst[]
                    {
                        PickFrom(encounterPoolBindings.EncounterQueue, encounterPoolBindings.QueueIndex, encounterPoolBindings.QueueEncounterId,
                            room.Field(roomRun, runEncountersBindings.RunNormalVisited)),
                    }),
                    room.If(IsMonsterRoom(), room.SetField(roomRun, runEncountersBindings.RunNormalVisited,
                        room.Add(room.Field(roomRun, runEncountersBindings.RunNormalVisited), room.Const(1L)))),
                    runEncountersBindings.RunEliteVisited is null
                        ? room.If(room.Const(false))
                        : room.If(IsEliteRoom(), room.SetField(roomRun, runEncountersBindings.RunEliteVisited,
                            room.Add(room.Field(roomRun, runEncountersBindings.RunEliteVisited), room.Const(1L)))),
                    bossEncounterId is null
                        ? room.If(room.Const(false))
                        : room.If(room.Equal(room.Field(room.Field(roomRun, roomServicesBindings.ActMap!.RunCurrentNode), roomServicesBindings.ActMap.NodeType),
                                room.EnumConst(roomServicesBindings.ActMap!.PointType, "Boss")),
                            room.SetField(roomRun, runStateBindings.RunEncounter, options.InitializeNewRun
                                ? room.Field(roomRun, roomServicesBindings.RunInitialization!.Boss) : room.Const(bossEncounterId))),
                };

                Inst Dispatch(int index)
                {
                    var encounter = encounters[index];
                    var condition = room.Equal(room.Field(roomRun, runStateBindings.RunEncounter),
                        room.Const(encounter.EncounterId));
                    // 固定阵容：每个敌人走同一条出生路径；带编队的遭遇按编队程序出生。
                    var missing = encounter.AllEnemyIds.Where(id => contentBindings.Author is not null && !contentBindings.Author.Enemies.Any(e => e.Id == id)).ToArray();
                    var body = missing.Length > 0 || encounter.AllEnemyIds.Count == 0
                        ? new[] { room.Fail($"Source encounter {encounter.EncounterId} cannot execute: missing enemy behavior {string.Join(", ", missing)}") }
                        : encounter.HasFormationProgram
                        ? new[] { FormationSpawn(room, encounter) }
                        : encounter.EnemyIds
                            .SelectMany(enemyId => SpawnMonster(room, EnemyOf(enemyId), roomScope, roomMonster,
                                pilesBindings.Monsters))
                            .ToArray();
                    if (index == encounters.Count - 1)
                    {
                        return room.IfElse(condition, body,
                            new[]
                            {
                                room.Fail("本间房的遭遇 id 不在整幕队列里："
                                    + "队列生成与分派链不一致（不静默生成空房间）"),
                            });
                    }

                    return room.IfElse(condition, body, new[] { Dispatch(index + 1) });
                }

                steps.Add(Dispatch(0));
                return room.If(room.Const(true), steps.ToArray());
            }

            var roomSteps = new List<Inst>
            {
                // �?新房间号：本房间的结算守卫因此是新的（一个不重置的全局布尔代表不了所有房间）�?
                startNextRoom.SetField(roomRun, runStateBindings.RunCurrentRoom!,
                    startNextRoom.Add(startNextRoom.Field(roomRun, runStateBindings.RunCurrentRoom!),
                        startNextRoom.Const(1L))),
                // �?清掉上一间房留下的一切：已知事实（它们引用战斗牌，留着会变成悬空引用）�?
                //    敌人及其能力、玩家身上的能力、以及战斗牌区里�?*全部**牌（含临时生成的）�?
                startNextRoom.Call(knowledgeCommandsBindings.ClearKnown.Symbol),
                startNextRoom.ForEach(startNextRoom.Collection(pilesBindings.Monsters), each => new[]
                {
                    startNextRoom.ForEach(startNextRoom.Relation(pilesBindings.Powers, each),
                        power => new[] { startNextRoom.Destroy(power) }),
                    startNextRoom.Destroy(each),
                }),
                startNextRoom.ForEach(startNextRoom.Relation(pilesBindings.Powers, roomPlayer),
                    power => new[] { startNextRoom.Destroy(power) }),
                startNextRoom.If(startNextRoom.Const(true), pilesBindings.Lifecycle.EndBattle(startNextRoom, roomPlayer, battleBindings.BattleScope, runCollectionsBindings.LongTermDeck!)),
                // �?玩家�?*整局**状态重置（HP/最�?HP 延续，格�?能量/回合/守卫复位）�?
                startNextRoom.SetField(roomPlayer, playerBindings.PlayerHp, startNextRoom.Field(roomRun, runStateBindings.RunHp!)),
                startNextRoom.SetField(roomPlayer, playerBindings.PlayerMaxHp, startNextRoom.Field(roomRun, runStateBindings.RunMaxHp!)),
                startNextRoom.SetField(roomPlayer, playerBindings.PlayerBlock, startNextRoom.Const(0L)),
                startNextRoom.SetField(roomPlayer, playerBindings.PlayerEnergy, startNextRoom.Field(roomPlayer, playerBindings.PlayerMaxEnergy)),
                startNextRoom.SetField(roomPlayer, playerBindings.PlayerStars, startNextRoom.Const(0L)),
                startNextRoom.SetField(roomPlayer, playerBindings.PlayerOrbSlots, startNextRoom.Const((long)(contentBindings.Author?.BaseOrbSlots ?? 0))),
                startNextRoom.SetField(roomPlayer, playerBindings.PlayerTurn, startNextRoom.Const(0L)),
                startNextRoom.SetField(roomPlayer, playerBindings.PlayerBattleSettled, startNextRoom.Const(false)),
                startNextRoom.SetField(roomPlayer, playerBindings.PlayerLostHp, startNextRoom.Const(false)),
                // �?重建敌人：正式构建按**本间房的遭遇**分派；受控切片沿用固定遭遇（测试旋钮只对调创建顺序）�?
                SpawnEncounter(startNextRoom),
                // �?战斗牌从**长期牌组**逐张复制（顺序即长期牌组顺序）�?
                startNextRoom.ForEach(startNextRoom.Collection(runCollectionsBindings.LongTermDeck!), longCard => new[]
                {
                    startNextRoom.CreateEntity(cardsBindings.Card, roomScope, roomCreated, CopyCardFields(longCard), pilesBindings.DrawPile),
                    EnterCombatDispatch(startNextRoom.LocalRef(roomCreated)),
                }),
                // ⑤b BASE-01�?*开局洗牌**（原版战斗开始时洗牌，用 run �?`Shuffle` 流）�?
                // 受控切片按声明顺序发牌（它的夹具依赖那个顺序），因此只在正式构建里打开�?
                options.ShuffleBattleDeck
                    ? startNextRoom.ShuffleCollection(pilesBindings.DrawPile, options.EncounterPool!.ShuffleStream)
                    : startNextRoom.If(startNextRoom.Const(false)),                // �?开这一间房的第一回合�?
                startNextRoom.ForEach(startNextRoom.Sort(startNextRoom.Where(startNextRoom.Collection(pilesBindings.DrawPile),c=>Innate(startNextRoom,c)),
                    c=>startNextRoom.CollectionIndex(pilesBindings.DrawPile,c),descending:true),c=>[
                        startNextRoom.RemoveFromCollection(pilesBindings.DrawPile,c),startNextRoom.AddToCollection(pilesBindings.DrawPile,c,InsertPosition.Start)]),
                startNextRoom.Call(playerTurnBindings.StartPlayerTurn.Symbol),
            };
            startNextRoom.Body(roomSteps.ToArray());
        }

        return new RoomTransitionBindings(startNextRoom);
    }
}
