using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class NewRunCompilation
{
    internal static SetupBindings Setup(ContentBindings contentBindings)
    {

        // -- initial state -----------------------------------------------------------------------------------
        var setup = contentBindings.Package.Procedure("SetupBattle");

        var battleLocal = setup.Local("battle", FieldTypeSchema.Binding());

        var characterLocal = setup.Local("player", FieldTypeSchema.Binding());

        var monsterLocal = setup.Local("monster", FieldTypeSchema.Binding());

        var createdLocal = setup.Local("created", FieldTypeSchema.Binding());

        return new SetupBindings(setup, battleLocal, characterLocal, monsterLocal, createdLocal);
    }

    internal static void NewRun(
        PackageBuildOptions options,
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        CardsBindings cardsBindings,
        PilesBindings pilesBindings,
        PotionsBindings potionsBindings,
        RunStateBindings runStateBindings,
        RelicsBindings relicsBindings,
        EncounterPoolBindings encounterPoolBindings,
        RunEncountersBindings runEncountersBindings,
        RunCollectionsBindings runCollectionsBindings,
        RoomServicesBindings roomServicesBindings,
        RewardsBindings rewardsBindings,
        PlayerTurnBindings playerTurnBindings,
        SetupBindings setupBindings,
        RoomTransitionBindings roomTransitionBindings,
        EncounterQueuesBindings encounterQueuesBindings,
        EnemyOfCallback EnemyOf,
        MonsterFieldsCallback MonsterFields,
        SpawnStartingPowersCallback SpawnStartingPowers,
        CardFieldsCallback CardFields,
        CardOfCallback CardOf)
    {
        // The starter deck is laid down in a fixed order: a controlled test scenario, not a vanilla shuffle.
        var setupSteps = new List<Inst>
        {
            setupBindings.Setup.CreateScope(battleBindings.BattleScope, setupBindings.BattleLocal),
            setupBindings.Setup.CreateEntity(playerBindings.Player, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CharacterLocal, new[]
            {
                (playerBindings.PlayerOwner, setupBindings.Setup.Const(Value.OfParticipant(SliceManifest.Player))),
                (playerBindings.PlayerHp, setupBindings.Setup.Const((long)SliceManifest.IroncladMaxHp)),
                (playerBindings.PlayerMaxHp, setupBindings.Setup.Const((long)SliceManifest.IroncladMaxHp)),
                (playerBindings.PlayerBlock, setupBindings.Setup.Const(0L)),
                (playerBindings.PlayerEnergy, setupBindings.Setup.Const((long)SliceManifest.EnergyPerTurn)),
                (playerBindings.PlayerStars, setupBindings.Setup.Const(0L)),
                (playerBindings.PlayerOrbSlots, setupBindings.Setup.Const((long)(contentBindings.Author?.BaseOrbSlots ?? 0))),
                (playerBindings.PlayerMaxEnergy, setupBindings.Setup.Const((long)SliceManifest.EnergyPerTurn)),
                (playerBindings.PlayerTurn, setupBindings.Setup.Const(0L)),
                (playerBindings.PlayerBattleSettled, setupBindings.Setup.Const(false)),
            }),

        };

        // B-07a�?*整局实体必须在战斗开始之前就存在**——`StartNextRoom` 里的 `on_enter_combat` 就可能结束战斗，
        // 而结算过程要能查到它（先前放在末尾，导致"战斗�?setup 内结�?时取不到实例）�?
        if (runStateBindings.HasRun)
        {
            var runInit = new List<(FieldSymbol Field, Expr Value)>
            {
                (runStateBindings.RunHp!, setupBindings.Setup.Const((long)contentBindings.Author!.Run.Hp)),
                (runStateBindings.RunMaxHp!, setupBindings.Setup.Const((long)contentBindings.Author.Run.MaxHp)),
                (runStateBindings.RunGold!, setupBindings.Setup.Const((long)contentBindings.Author.Run.Gold)),
                // 开局�?�?0 间房"，由 `StartNextRoom` 递增�?1�?
                (runStateBindings.RunCurrentRoom!, setupBindings.Setup.Const(0L)),
                (runStateBindings.RunSettledRoom!, setupBindings.Setup.Const(0L)),
                (runStateBindings.RunRewardTaken!, setupBindings.Setup.Const(0L)),
            };
            if (runStateBindings.RunEncounter is not null && runStateBindings.RunRoomCount is not null && options.EncounterPool is not null)
            {
                // BASE-01：当前范围覆盖的战斗房间数（遭遇池长度）。原版这里来自地图，接入地图后由地图取代�?
                runInit.Add((runStateBindings.RunEncounter, setupBindings.Setup.Const(string.Empty)));
                var roomLimit = options.EncounterPool.RoomCount
                    + (roomServicesBindings.ActMap is not null && options.EncounterPool.Bosses.Count > 0 ? 1 : 0);
                runInit.Add((runStateBindings.RunRoomCount, setupBindings.Setup.Const((long)roomLimit)));
                // 两个 per-type 访问计数从 0 开始（源 `RoomSet` 的计数器初值）。
                if (runEncountersBindings.RunNormalVisited is not null)
                {
                    runInit.Add((runEncountersBindings.RunNormalVisited, setupBindings.Setup.Const(0L)));
                }

                if (runEncountersBindings.RunEliteVisited is not null)
                {
                    runInit.Add((runEncountersBindings.RunEliteVisited, setupBindings.Setup.Const(0L)));
                }
            }

            setupSteps.Insert(1, setupBindings.Setup.CreateEntity(runStateBindings.RunEntity!, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CreatedLocal,
                runInit.ToArray(), runCollectionsBindings.RunStates!));

            // T05：地图物化步骤（必须排在 `Run` 实体创建之后，即 index 2）。
            if (roomServicesBindings.ActMap is not null)
            {
                var mapSteps = roomServicesBindings.ActMap.SetupSteps(setupBindings.Setup, runCollectionsBindings.RunStates!);
                setupSteps.InsertRange(2, mapSteps);

                // ENV-T05：奖励赔率状态（源 `PotionRewardOdds`/`CardRarityOdds` 的构造值）也在 Run
                // 实体创建之后初始化；两条赔率是**跨房间持久**的，之后每次 roll 就地更新，不重置。
                if (rewardsBindings.Rewards is not null)
                {
                    setupSteps.InsertRange(2 + mapSteps.Count, rewardsBindings.Rewards.SetupSteps(setupBindings.Setup, runCollectionsBindings.RunStates!));
                }
            }
        }

        // 每个牌区一个可识别实体（公开快照�?kind_id 唯一匹配 pile_ref）；**跨房间存�?*，因此只建一次�?
        foreach (var area in pilesBindings.AreaEntities)
            setupSteps.Add(setupBindings.Setup.CreateEntity(area, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CreatedLocal, []));

        if (contentBindings.Author is null)
        {
            // 程序化路径：形状逐字不变（不�?Run/长期牌组，按冻结卡组直接铺牌）�?
            var frontDefinition = EnemyOf(SliceManifest.CalcifiedCultistId);
            var backDefinition = options.SameDefinitionEnemyPair
                ? frontDefinition
                : EnemyOf(SliceManifest.DampCultistId);
            var defaultFront = setupBindings.Setup.CreateEntity(enemiesBindings.Monster, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.MonsterLocal,
                MonsterFields(setupBindings.Setup, frontDefinition, options.SameDefinitionEnemyPair ? 20 : null),
                pilesBindings.Monsters);
            var defaultBack = setupBindings.Setup.CreateEntity(enemiesBindings.Monster, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.MonsterLocal,
                MonsterFields(setupBindings.Setup, backDefinition, options.SameDefinitionEnemyPair ? 10 : null), pilesBindings.Monsters);
            setupSteps.Add(options.SwapEncounterOrder ? defaultBack : defaultFront);
            setupSteps.Add(options.SwapEncounterOrder ? defaultFront : defaultBack);
            // 出生期挂能力（`startingPowers`）：与后续房间的 `SpawnMonster` 走同一条路径。
            setupSteps.AddRange(SpawnStartingPowers(setupBindings.Setup, frontDefinition, setupBindings.MonsterLocal));
            setupSteps.AddRange(SpawnStartingPowers(setupBindings.Setup, backDefinition, setupBindings.MonsterLocal));
            foreach (var definition in SliceManifest.StarterDeck())
            {
                setupSteps.Add(setupBindings.Setup.CreateEntity(cardsBindings.Card, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CreatedLocal,
                    CardFields(setupBindings.Setup, CardOf(definition), false), pilesBindings.DrawPile));
            }

            if (options.IncludeTestHeadbutt)
            {
                setupSteps.Add(setupBindings.Setup.CreateEntity(cardsBindings.Card, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CreatedLocal,
                    CardFields(setupBindings.Setup, CardOf(SliceManifest.HeadbuttId), false), pilesBindings.DrawPile));
            }

            setupSteps.Add(setupBindings.Setup.Call(playerTurnBindings.StartPlayerTurn.Symbol));
        }
        else
        {
            // 作者路径（W-05）：建立**长期牌组**，然后由 `StartNextRoom` 铺出第一间房的战斗牌�?
            foreach (var entry in contentBindings.Author.Deck)
            {
                var definition = cardsBindings.CardDefinitions.Definition(entry.CardId);
                for (var copy = 0; copy < entry.Count; copy++)
                    setupSteps.Add(setupBindings.Setup.CreateEntity(cardsBindings.Card, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CreatedLocal,
                        cardsBindings.CardDefinitions.New(setupBindings.Setup, definition, entry.Upgraded), runCollectionsBindings.LongTermDeck!));
            }

            if (potionsBindings.HasPotions)
            {
                var slot = 0;
                foreach (var entry in contentBindings.Author.Belt)
                    for (var copy = 0; copy < entry.Count; copy++)
                        setupSteps.Add(setupBindings.Setup.CreateEntity(potionsBindings.Potion!, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CreatedLocal,
                            potionsBindings.Potions!.Fields(setupBindings.Setup, entry.PotionId, setupBindings.Setup.Const((long)(slot < SliceManifest.PotionSlotCount ? slot++ : -1))), potionsBindings.PotionBelt!));
            }

            if (options.IncludeTestHeadbutt)
            {
                // 测试场景的额外牌也进**长期牌组**：这样它会在每一间房里出现（�?这是这一局的牌�?一致）�?
                setupSteps.Add(setupBindings.Setup.CreateEntity(cardsBindings.Card, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CreatedLocal,
                    CardFields(setupBindings.Setup, CardOf(SliceManifest.HeadbuttId), false), runCollectionsBindings.LongTermDeck!));
            }

            if (roomServicesBindings.RunInitialization is not null)
                setupSteps.AddRange(roomServicesBindings.RunInitialization.Setup(setupBindings.Setup, setupBindings.Setup.First(setupBindings.Setup.Collection(runCollectionsBindings.RunStates!))));

            if (options.InitializeNewRun && roomServicesBindings.RunInitialization is not null && options.ActPlan is null)
                setupSteps.AddRange(roomServicesBindings.RunInitialization.GenerateEventQueue(setupBindings.Setup, setupBindings.Setup.First(setupBindings.Setup.Collection(runCollectionsBindings.RunStates!))));

            // BASE-01/02�?*遭遇池与整幕队列**。池成员�?弱池在前、普通池在后"的源顺序各建一个实例，
            // 然后用原�?`UpFront` �?*一次生成整幕队�?*（GrabBag + 标签回避，见 `EnqueueProcedure`）：
            // 池的内容是规则（进规则身份），队列是局内状态（每局一份）—�?第几间遇到谁"永不烘进规则集�?
            if (options.EncounterPool is not null && options.ActPlan is null)
            {
                foreach (var encounter in options.EncounterPool.Encounters.Concat(options.InitializeNewRun ? options.EncounterPool.Elites : []))
                {
                    setupSteps.Add(setupBindings.Setup.CreateEntity(encounterPoolBindings.PoolEntry!, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CreatedLocal,
                        [
                            (encounterPoolBindings.PoolEncounterId!, setupBindings.Setup.Const(encounter.EncounterId)),
                            (encounterPoolBindings.PoolKind!, setupBindings.Setup.Const(encounter.Kind)),
                            (encounterPoolBindings.PoolBagUsed!, setupBindings.Setup.Const(false)),
                            .. encounterPoolBindings.PoolTagIds.Select(tag => (encounterPoolBindings.PoolTags[tag],
                                (Expr)setupBindings.Setup.Const(encounter.Tags.Contains(tag, StringComparer.Ordinal)))),
                        ], encounterPoolBindings.PoolEntries!));
                }

                // 队列顺序：先弱后普通（`for num in 0..NumberOfWeakEncounters` �?
                // `for num2 in NumberOfWeakEncounters..GetNumberOfRooms(false)` 两段）�?
                for (var slot = 0; slot < options.EncounterPool.RoomCount; slot++)
                {
                    var procedure = slot < options.EncounterPool.WeakRoomCount ? encounterQueuesBindings.EnqueueWeak : encounterQueuesBindings.EnqueueRegular;
                    if (procedure is null)
                    {
                        continue;
                    }

                    setupSteps.Add(setupBindings.Setup.Call(procedure, setupBindings.Setup.Const((long)slot)));
                }
                if (options.InitializeNewRun && encounterQueuesBindings.EnqueueElite is not null)
                {
                    // 精英段排在普通段之后、BOSS/古代人之前（源 `GenerateRooms` 的顺序），写进
                    // **独立集合** `EliteEncounterQueue`，槽位号同样从 0 起（源 `for num3 = 0..15`）。
                    // 清空"上一个"对应源里 `_rooms.eliteEncounters` 此时为空：`AddWithoutRepeatingTags`
                    // 取的是**精英列表自己的** `LastOrDefault()`。
                    setupSteps.Add(setupBindings.Setup.SetField(setupBindings.Setup.First(setupBindings.Setup.Collection(runCollectionsBindings.RunStates!)), runEncountersBindings.RunHasLast!, setupBindings.Setup.Const(false)));
                    for (var slot = 0; slot < options.EncounterPool.EliteRoomCount; slot++)
                        setupSteps.Add(setupBindings.Setup.Call(encounterQueuesBindings.EnqueueElite, setupBindings.Setup.Const((long)slot)));
                }

                if (options.InitializeNewRun && roomServicesBindings.RunInitialization is not null)
                {
                    setupSteps.AddRange(roomServicesBindings.RunInitialization.GenerateBossAndAncient(setupBindings.Setup,
                        setupBindings.Setup.First(setupBindings.Setup.Collection(runCollectionsBindings.RunStates!)), options.EncounterPool));
                }
            }

            if (roomServicesBindings.Acts is not null)
                setupSteps.AddRange(roomServicesBindings.Acts.Initialize(setupBindings.Setup,
                    setupBindings.Setup.First(setupBindings.Setup.Collection(runCollectionsBindings.RunStates!)),
                    roomServicesBindings.RunInitialization!, encounterPoolBindings, runEncountersBindings, encounterQueuesBindings));

            // 第一间房走与后续房间**同一�?*流程（HP 来自 Run、战斗牌由长期牌组拷贝而来）�?
            // ENV-T05 修正：**地图 scope 开局不自动开打**。源在幕开始时站在第 0 行起点、只显示可选子节点，
            // 选中节点才进房间（`ChooseMapPoint` → `StartNextRoom`）。原先这里无条件调用 `StartNextRoom`，
            // 会让 `MapPhase == 0`（还在选点）与"已经开打的战斗"并存：玩家既能出牌又能选点，而选点会清场
            // 重开 —— 等于跳过已经开始的战斗。地图 scope 的第一场战斗一律由首个 `ChooseMapPoint` 触发。
            if (roomServicesBindings.ActMap is null)
            {
                setupSteps.Add(setupBindings.Setup.Call(roomTransitionBindings.StartNextRoom!.Symbol));
            }
        }

        // BASE-02：起始遗物实例（来自角色注册表；遗物�?*整局**状态，跨房间保留）�?
        if (relicsBindings.HasRelics && options.StartingRelics is not null)
        {
            foreach (var relicId in options.StartingRelics)
            {
                var declared = options.RelicLibrary!.FirstOrDefault(candidate =>
                    string.Equals(candidate.ContentId, relicId, StringComparison.Ordinal))
                    ?? throw Unsupported($"starting relic '{relicId}'", "没有对应的遗物声明");
                setupSteps.Add(setupBindings.Setup.CreateEntity(relicsBindings.Relic!, setupBindings.Setup.LocalRef(setupBindings.BattleLocal), setupBindings.CreatedLocal,
                    [
                        (relicsBindings.RelicDefinition!, setupBindings.Setup.Const(declared.ContentId)),
                        (relicsBindings.RelicRarity!, setupBindings.Setup.Const(declared.Rarity)),
                    ], relicsBindings.Relics!));
            }
        }

        setupBindings.Setup.Body(setupSteps.ToArray());

        contentBindings.Package.OnSessionStart(setupBindings.Setup.Symbol);

    }
}
