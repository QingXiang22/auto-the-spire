using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class CombatPackageComposer
{
    internal static EnginePackageBuilder Build(PackageBuildOptions options)
    {
        if (options.InitializeNewRun && options.EncounterPool is not null && options.ActPlan is null)
            options = options with { EncounterPool = RunSourcePoolManifest.Complete(options.EncounterPool) };
        // Declaration order is part of the rules identity. Declare handles before binding their bodies.
        // Content and state vocabulary. Each declaration returns only its downstream handles.
        var contentBindings = ContentCompilation.Content(options);
        var battleBindings = CombatSchema.Battle(contentBindings);
        var playerBindings = CombatSchema.Player(contentBindings);
        var enemiesBindings = CombatSchema.Enemies(contentBindings);
        var cardsBindings = CombatSchema.Cards(contentBindings, battleBindings);
        var powerBindings = CombatSchema.Power(contentBindings);
        var knowledgeBindings = CombatSchema.Knowledge(contentBindings, cardsBindings);
        var pilesBindings = CombatSchema.Piles(
            contentBindings, battleBindings, playerBindings, enemiesBindings, cardsBindings, powerBindings);
        var eventsBindings = CombatSchema.Events(
            contentBindings, playerBindings, enemiesBindings, cardsBindings, powerBindings, knowledgeBindings,
            pilesBindings);
        var powerQueryBindings = PowerCompilation.PowerQuery(contentBindings, powerBindings, pilesBindings);
        BaseSpire.Slice.Domain.Combat.CombatOrbs? orbs = null;
        MonsterDamageBindings? monsterDamageBindings = null;
        DrawBindings? drawBindings = null;
        BaseSpire.Slice.Domain.Combat.CombatDoom? doom = null;
        BaseSpire.Slice.Domain.Combat.CombatOsty? osty = null;
        BaseSpire.Slice.Domain.Combat.CombatOsty Osty()
        {
            if (osty is not null) return osty;
            osty = new(contentBindings.Package, playerBindings.Player);
            eventsBindings.Projection.Type(osty.Entity);
            return osty;
        }
        bool UsesOsty(CardEffectProgram? program) => program is not null && program.Effects.Any(effect => effect switch
        {
            OstyCardEffect => true,
            ConditionalCardEffect branch => UsesOsty(branch.Then) || UsesOsty(branch.Else),
            RepeatCardEffect repeat => UsesOsty(repeat.Body),
            DynamicRepeatCardEffect repeat => UsesOsty(repeat.Body),
            WithCountCardEffect count => UsesOsty(count.Body),
            ForEachCardEffect each => UsesOsty(each.Body),
            SelectCardsEffect select => UsesOsty(select.OnSelected),
            RandomCardEffect random => UsesOsty(random.OnSelected),
            _ => false,
        });
        if (contentBindings.Author?.Cards.Any(card => UsesOsty(card.NormalProgram) || UsesOsty(card.UpgradedProgram)
            || (card.NormalHooks.Programs?.Values.Any(UsesOsty) ?? false) || (card.UpgradedHooks.Programs?.Values.Any(UsesOsty) ?? false)) == true
            || contentBindings.Author?.Potions.Any(potion => UsesOsty(potion.Program)) == true) Osty();
        var potionsBindings = CombatSchema.Potions(contentBindings, eventsBindings, playerBindings);
        // Run resources, source pools, rooms and rewards must exist before settlement IR.
        var runStateBindings = RunSchema.RunState(options, contentBindings);
        var relicsBindings = RunSchema.Relics(options, contentBindings, eventsBindings, runStateBindings);
        var encounterPoolBindings = EncounterCompilation.EncounterPool(options, contentBindings);
        var runEncountersBindings = EncounterCompilation.RunEncounters(runStateBindings, encounterPoolBindings);
        var runCollectionsBindings = RunSchema.RunCollections(contentBindings, cardsBindings, eventsBindings, runStateBindings);
        var roomServicesBindings = RoomCompilation.RoomServices(
            options, contentBindings, eventsBindings, cardsBindings, potionsBindings, runStateBindings,
            relicsBindings, runCollectionsBindings);
        var rewardsBindings = RewardCompilation.Rewards(
            contentBindings, cardsBindings, potionsBindings, runStateBindings, runCollectionsBindings,
            roomServicesBindings, RewardCardFields);
        // Shared rule commands. Declare a procedure before compiling bodies that refer to it.
        var settlementBindings = SettlementCompilation.Settlement(
            contentBindings, battleBindings, playerBindings, runStateBindings, runCollectionsBindings,
            roomServicesBindings, rewardsBindings);
        var powerCommandsBindings = PowerCompilation.PowerCommands(contentBindings, powerBindings, pilesBindings);
        var playerBlockBindings = DamageCompilation.PlayerBlock(contentBindings, battleBindings, playerBindings);
        SettlementCompilation.SettlementBinding(
            battleBindings, playerBindings, runStateBindings, runCollectionsBindings, roomServicesBindings,
            rewardsBindings, settlementBindings);
        var powerRulesBindings = PowerCompilation.PowerRules(
            contentBindings, battleBindings, playerBindings, cardsBindings, powerBindings, pilesBindings,
            runCollectionsBindings);
        var relicPrograms = new RelicCompilation(options, contentBindings, battleBindings, playerBindings, pilesBindings,
            relicsBindings, runStateBindings, runCollectionsBindings, roomServicesBindings, eventsBindings,
            powerCommandsBindings, (s, amount) => [.. BlockGainSteps(s, amount, true)], EnergyGainBlocked,
            (s, operation, kind, amount) => orbs!.Emit(s, new OrbCardEffect(operation, kind, CardEffectAmount.Literal(0), true, new("relics.json", 1, 1)), amount),
            (s, card, kind) => s.Equal(s.Field(card, cardsBindings.CardKindField), s.EnumConst(battleBindings.CardKind, kind)),
            // 无加成全体伤害（遗物源 ValueProp.Unpowered）：复用与球/卡牌同一个 DamageMonster 命令。
            (s, amount) => [s.ForEach(s.Collection(pilesBindings.Monsters), enemy => [
                s.If(s.Greater(s.Field(enemy, enemiesBindings.MonsterHp), s.Const(0L)),
                    s.Call(monsterDamageBindings!.DamageMonster.Symbol,
                        s.First(s.Type(playerBindings.Player, battleBindings.BattleScope)), enemy,
                        s.Maximum(s.Const(0L), amount), s.Const(false)))])],
            // 抽牌：与回合开始/卡牌效果共用同一个抽牌过程（含重洗、阻止与 AfterDraw）。
            (s, amount) => [s.Call(drawBindings!.Draw, s.Maximum(s.Const(0L), amount))]);
        if (potionsBindings.Potions is { } potionInventory)
        {
            potionInventory.ShouldAcquire = (s, _) => s.Not(relicPrograms.Has(s, RelicEffectKinds.PreventPotionProcurement));
            potionInventory.DeclareAcquireProcedure(contentBindings.Package);
        }
        monsterDamageBindings = DamageCompilation.MonsterDamage(contentBindings, playerBindings, enemiesBindings);
        DamageCompilation.MonsterDamageBody(
            enemiesBindings, pilesBindings, eventsBindings, powerRulesBindings, monsterDamageBindings,
            DamageChain);
        var playerHpLossBindings = DamageCompilation.PlayerHpLoss(contentBindings, battleBindings, playerBindings);
        DamageCompilation.PlayerHpLossBody(playerBindings, eventsBindings, powerRulesBindings, playerHpLossBindings, SettleTail);
        BaseSpire.Slice.Domain.Combat.CombatPoison? poison = null;
        var poisonPowers = contentBindings.Author?.Powers.Where(power => power.Role == "Poison").ToArray() ?? [];
        if (poisonPowers.Length > 0)
        {
            var damage = new UnblockableDamageCompilation(contentBindings, playerBindings, enemiesBindings, pilesBindings,
                eventsBindings, powerRulesBindings, playerHpLossBindings);
            poison = new(contentBindings.Package, playerBindings.Player, battleBindings.BattleScope, pilesBindings.Monsters,
                pilesBindings.Powers, powerBindings.PowerDefinition, powerBindings.PowerStacks,
                enemiesBindings.MonsterHp, playerBindings.PlayerHp, playerBindings.PlayerBattleSettled,
                poisonPowers.Select(power => power.Id).ToArray(),
                (scope, target) => contentBindings.Author!.Powers.Where(power => power.Role == "Accelerant").Aggregate((Expr)scope.Const(0L),
                    (total, power) => scope.Add(total, scope.Maximum(scope.Const(0L), scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, target, scope.Const(power.Id))))),
                damage.Damage);
        }
        var doomPowers = contentBindings.Author?.Powers.Where(power => power.Role == "Doom").ToArray() ?? [];
        if (doomPowers.Length > 0)
            doom = new(contentBindings.Package, playerBindings.Player, battleBindings.BattleScope, pilesBindings.Monsters,
                enemiesBindings.MonsterHp, playerBindings.PlayerHp, playerBindings.PlayerBattleSettled,
                (scope, target) => doomPowers.Aggregate((Expr)scope.Const(0L), (total, power) => scope.Add(total,
                    scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, target, scope.Const(power.Id)))),
                eventsBindings.MonsterDied, eventsBindings.MonsterDiedMonster,
                (scope, owner) => [.. SettleTail(scope), scope.SetTerminal(eventsBindings.Defeat,
                    (eventsBindings.DefeatTurn, scope.Field(owner, playerBindings.PlayerTurn)))]);
        var afterDamageBindings = DamageCompilation.AfterDamage(contentBindings, enemiesBindings);
        DamageCompilation.AfterDamageBody(afterDamageBindings, RetaliateChain);
        var playerDamageBindings = DamageCompilation.PlayerDamage(contentBindings, battleBindings, playerBindings, enemiesBindings);
        // 受击后的遗物监听先建过程符号，过程体在抽牌等绑定就绪后再补（见下方 relicAfterDamage.Body）。
        var relicAfterDamage = contentBindings.Package.Procedure("RelicAfterDamageReceived");
        DamageCompilation.PlayerDamageBody(
            playerBindings, enemiesBindings, eventsBindings, powerRulesBindings, afterDamageBindings, playerDamageBindings,
            DamageChain, SettleTail, osty, scope => [scope.Call(relicAfterDamage.Symbol)]);
        DamageCompilation.PlayerBlockBody(contentBindings, playerBindings, playerBlockBindings, RandomAttackChain);
        var turnStartTriggerBindings = DamageCompilation.TurnStartTrigger(contentBindings);
        DamageCompilation.TurnStartTriggerBody(turnStartTriggerBindings, GrantPowerChain);
        var exhaustTriggerBindings = DamageCompilation.ExhaustTrigger(contentBindings);
        DamageCompilation.ExhaustTriggerBody(powerRulesBindings, exhaustTriggerBindings, TriggerChain,
            scope => relicPrograms.Dispatch(scope, RelicTriggers.AfterCardExhausted));
        orbs = new BaseSpire.Slice.Domain.Combat.CombatOrbs(contentBindings.Package, playerBindings.PlayerOrbSlots,
            scope => scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
            scope => scope.And(scope.Not(scope.Field(scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)), playerBindings.PlayerBattleSettled)),
                scope.Exists(scope.Collection(pilesBindings.Monsters))),
            (scope, owner) => contentBindings.Author?.Powers.Where(power => power.Role == "Focus").Aggregate(scope.Const(0L),
                (total, power) => scope.Add(total, scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, scope.Const(power.Id)))) ?? scope.Const(0L),
            (scope, amount) => [.. BlockGainSteps(scope, amount, true)],
            (scope, amount) => [scope.If(scope.Not(EnergyGainBlocked(scope)), scope.SetField(scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                playerBindings.PlayerEnergy, scope.Add(scope.Field(scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)), playerBindings.PlayerEnergy), amount)))],
            pilesBindings.Monsters, enemiesBindings.MonsterHp, monsterDamageBindings.DamageMonster.Symbol, contentBindings.Author?.BaseOrbSlots ?? 0)
        { ModifyValue = (s, kind, amount) => kind == "Lightning" ? relicPrograms.Modify(s, RelicEffectKinds.ModifyLightningOrbValue, amount) : amount };
        pilesBindings.Lifecycle.ResetAdditional = scope => [scope.ForEach(scope.Collection(orbs.Queue), item => [scope.Destroy(item)]), .. osty?.Reset(scope) ?? []];
        eventsBindings.Projection.Type(orbs.Entity);
        var playedTriggerBindings = DamageCompilation.PlayedTrigger(contentBindings, cardsBindings);
        DamageCompilation.PlayedTriggerBody(playedTriggerBindings, TriggerChain,
            (scope, card) => relicPrograms.DispatchCard(scope, RelicTriggers.AfterCardPlayed, card));
        // Knowledge, turns and enemy selection.
        var knowledgeCommandsBindings = CardCompilation.KnowledgeCommands(
            contentBindings, playerBindings, cardsBindings, knowledgeBindings, pilesBindings);
        var turnEndBindings = TurnCompilation.TurnEnd(contentBindings);
        var enemyTurnBindings = TurnCompilation.EnemyTurn(
            contentBindings, battleBindings, playerBindings, pilesBindings, turnEndBindings,
            HardcodedPowerTurnEnd, DeclaredPowerTurnEnd, ExpireAtOpponentTurnEnd,
            doom is null ? null : scope => [doom.Resolve(scope, true)]);
        TurnCompilation.EnemyTurnBody(
            contentBindings, enemiesBindings, pilesBindings, enemyTurnBindings, HardcodedEnemyMove,
            DeclaredEnemyMove, poison is null ? null : scope => [poison.Resolve(scope, true)], BlockClearPrevented);
        drawBindings = CardCompilation.Draw(
            contentBindings, battleBindings, playerBindings, cardsBindings, pilesBindings, powerQueryBindings,
            powerCommandsBindings, powerRulesBindings, knowledgeCommandsBindings, DrawPrevented);
        // 抽牌绑定就绪后再补受击遗物监听的过程体（过程体里可能用到抽牌）。
        relicAfterDamage.Body(relicPrograms.Dispatch(relicAfterDamage, RelicTriggers.AfterDamageReceived));
        var enemySelectionBindings = EnemyCompilation.EnemySelection(contentBindings, enemiesBindings);
        EnemyCompilation.EnemySelectionBody(contentBindings, enemiesBindings, enemySelectionBindings, SelectMachine);
        var playerTurnBindings = TurnCompilation.PlayerTurn(
            contentBindings, battleBindings, playerBindings, pilesBindings, eventsBindings, powerRulesBindings,
            turnStartTriggerBindings, drawBindings, enemySelectionBindings, MaxEnergyBonus, BlockClearPrevented,
            Innate, scope => orbs.Passive(scope, true), poison is null ? null : scope => [poison.Resolve(scope, false)], relicPrograms.Dispatch,
            (s, amount) => relicPrograms.Modify(s, RelicEffectKinds.ModifyHandDraw, amount), s => relicPrograms.Has(s, RelicEffectKinds.RetainEnergy),
            s => relicPrograms.Modify(s, RelicEffectKinds.RetainedBlockLimit, s.Const(0L)));
        var playerTurnEndBindings = TurnCompilation.PlayerTurnEnd(contentBindings);
        TurnCompilation.PlayerTurnEndBody(
            battleBindings, playerBindings, pilesBindings, powerRulesBindings, turnEndBindings,
            enemyTurnBindings, playerTurnBindings, playerTurnEndBindings, DispatchCardHook, EndTurnHandSteps,
            ClearCostModifier, scope => orbs.Passive(scope, false),
            doom is null ? null : scope => [doom.Resolve(scope, false)], relicPrograms.Dispatch);
        // Room transitions and real new-Run initialization.
        var setupBindings = NewRunCompilation.Setup(contentBindings);
        var roomTransitionBindings = BattleRoomCompilation.RoomTransition(
            options, contentBindings, battleBindings, playerBindings, enemiesBindings, cardsBindings,
            pilesBindings, runStateBindings, encounterPoolBindings, runEncountersBindings,
            runCollectionsBindings, roomServicesBindings, knowledgeCommandsBindings, enemySelectionBindings,
            playerTurnBindings, EnemyOf, DispatchCardHook, MonsterFields, SpawnStartingPowers, Innate, relicPrograms.Dispatch);
        var encounterQueuesBindings = EncounterCompilation.EncounterQueues(options, encounterPoolBindings, EnqueueProcedure);
        NewRunCompilation.NewRun(
            options, contentBindings, battleBindings, playerBindings, enemiesBindings, cardsBindings,
            pilesBindings, potionsBindings, runStateBindings, relicsBindings, encounterPoolBindings,
            runEncountersBindings, runCollectionsBindings, roomServicesBindings, rewardsBindings,
            playerTurnBindings, setupBindings, roomTransitionBindings, encounterQueuesBindings, EnemyOf,
            MonsterFields, SpawnStartingPowers, CardFields, CardOf);
        // Public actions and room leaves consume the same compiled commands.
        CardCompilation.CardPlay(
            contentBindings, battleBindings, playerBindings, cardsBindings, pilesBindings, eventsBindings,
            powerCommandsBindings, powerRulesBindings, monsterDamageBindings, playerHpLossBindings,
            playerBlockBindings, playedTriggerBindings, drawBindings, HoldsPower, CardPiles, CompileEffects,
            DispatchCardHook, ManifestEffects, RemovePower, HasTag, CloneToHand, EnergyGainBlocked, InCombat,
            BattleEndTail);
        ActionCompilation.PotionAction(
            contentBindings, battleBindings, playerBindings, enemiesBindings, potionsBindings,
            runCollectionsBindings, roomServicesBindings, InCombat, PotionEffectChain, BattleEndTail);
        ActionCompilation.EndTurnAction(contentBindings, pilesBindings, playerTurnEndBindings, InCombat, BattleEndTail);
        ActionCompilation.RunActions(
            contentBindings, cardsBindings, runStateBindings, runCollectionsBindings, roomServicesBindings,
            roomTransitionBindings);
        RoomCompilation.Rooms(
            options, contentBindings, cardsBindings, runStateBindings, relicsBindings, runCollectionsBindings,
            roomServicesBindings, rewardsBindings, roomTransitionBindings, encounterPoolBindings, runEncountersBindings, scope =>
            {
                var player = scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope));
                var run = scope.First(scope.Collection(runCollectionsBindings.RunStates!));
                return [.. BattleRoomCompilation.ClearCombat(scope, player, battleBindings, enemiesBindings, pilesBindings,
                    knowledgeCommandsBindings, runCollectionsBindings, includeDead: true),
                    scope.SetField(player, playerBindings.PlayerHp, scope.Field(run, runStateBindings.RunHp!)),
                    scope.SetField(player, playerBindings.PlayerMaxHp, scope.Field(run, runStateBindings.RunMaxHp!)),
                    scope.SetField(player, playerBindings.PlayerBlock, scope.Const(0L)),
                    scope.SetField(player, playerBindings.PlayerEnergy, scope.Const(0L)),
                    scope.SetField(player, playerBindings.PlayerStars, scope.Const(0L)),
                    scope.SetField(player, playerBindings.PlayerOrbSlots, scope.Const((long)(contentBindings.Author?.BaseOrbSlots ?? 0))),
                    scope.SetField(player, playerBindings.PlayerLostHp, scope.Const(false)),
                    scope.SetField(player, playerBindings.PlayerExhaustedCard, scope.Const(false)),
                    scope.SetField(player, playerBindings.PlayerBlockGains, scope.Const(0L)),
                    scope.SetField(player, playerBindings.PlayerTurn, scope.Const(0L)),
                    scope.SetField(player, playerBindings.PlayerBattleSettled, scope.Const(true))];
            }, relicPrograms.DispatchRun, (s, amount) => relicPrograms.Modify(s, RelicEffectKinds.ModifyRestHeal, amount));
        return contentBindings.Package;

        // These adapters only wire explicit module interfaces; mechanic implementations live in the modules.
        SliceCard CardOf(string contentId) => ContentCompilation.CardOf(contentBindings, contentId);
        SliceEnemy EnemyOf(string contentId) => ContentCompilation.EnemyOf(contentBindings, contentId);
        List<Inst> DamageChain(ProcedureBuilder scope, int amountSlot, int fixedSlot, int stacksSlot, int damageSlot,
                Expr attacker, Expr target, Expr powered) => DamageCompilation.DamageChain(
            AdditiveStage, MultiplicativeStage, CapStage, scope, amountSlot, fixedSlot, stacksSlot, damageSlot,
            attacker, target, powered);
        Inst[] AdditiveStage(ProcedureBuilder scope, int amountSlot, int fixedSlot, int stacksSlot, Expr attacker,
                Expr target) => DamageCompilation.AdditiveStage(
            contentBindings, powerQueryBindings, scope, amountSlot, fixedSlot, stacksSlot, attacker, target);
        Expr EnergyGainBlocked(IrScope scope) => PowerCompilation.EnergyGainBlocked(
            contentBindings, battleBindings, playerBindings, powerQueryBindings, scope);
        Expr BlockClearPrevented(IrScope scope, Expr owner) => PowerCompilation.BlockClearPrevented(
            contentBindings, powerQueryBindings, scope, owner);
        Expr DrawPrevented(IrScope scope, Expr owner) => PowerCompilation.DrawPrevented(
            contentBindings, powerQueryBindings, scope, owner);
        Expr MaxEnergyBonus(IrScope scope, Expr owner) => relicPrograms.Modify(scope, RelicEffectKinds.ModifyMaxEnergy,
            PowerCompilation.MaxEnergyBonus(contentBindings, powerQueryBindings, scope, owner));
        Inst PotionEffectChain(IrScope scope, Expr potionRef, Expr owner, int targetSlot, string targetKind) => PowerCompilation.PotionEffectChain(
            contentBindings, pilesBindings, potionsBindings, CompileEffects, scope, potionRef, owner,
            targetSlot, targetKind);
        Inst[] CapStage(IrScope scope, int fixedSlot, int stacksSlot, Expr target) => DamageCompilation.CapStage(
            contentBindings, powerQueryBindings, scope, fixedSlot, stacksSlot, target);
        Inst[] MultiplicativeStage(ProcedureBuilder scope, int amountSlot, int fixedSlot, int stacksSlot, Expr attacker,
                Expr target) => DamageCompilation.MultiplicativeStage(
            contentBindings, powerQueryBindings, scope, amountSlot, fixedSlot, stacksSlot, attacker, target);
        Inst[] SettleTail(IrScope scope)
        {
            var owner = scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope));
            return [scope.If(scope.Equal(scope.Field(owner, playerBindings.PlayerHp), scope.Const(0L)),
                scope.ForEach(scope.Relation(pilesBindings.Powers, owner), power => [scope.Destroy(power)])),
                .. SettlementCompilation.SettleTail(runStateBindings, settlementBindings, scope)];
        }
        Inst[] RelicVictorySteps(IrScope scope) => [.. relicPrograms.Dispatch(scope, RelicTriggers.AfterCombatEnd),
            .. relicPrograms.Dispatch(scope, RelicTriggers.AfterCombatVictoryEarly),
            .. relicPrograms.Dispatch(scope, RelicTriggers.AfterCombatVictory)];
        Inst[] VictoryTail(IrScope scope, Expr turn) => SettlementCompilation.VictoryTail(
            options, eventsBindings, runStateBindings, runCollectionsBindings, SettleTail, RelicVictorySteps,
            scope, turn);
        Inst BattleEndTail(IrScope scope) => SettlementCompilation.BattleEndTail(
            battleBindings, playerBindings, pilesBindings, VictoryTail, scope);
        Inst ExpireAtOpponentTurnEnd(IrScope scope, Expr power) => TurnCompilation.ExpireAtOpponentTurnEnd(
            contentBindings, powerBindings, scope, power);
        Inst[] TriggerChain(IrScope scope, string trigger, Expr? playedCard = null) => PowerCompilation.TriggerChain(
            contentBindings, battleBindings, playerBindings, cardsBindings, powerQueryBindings,
            playerBlockBindings, scope, trigger, playedCard);
        Inst[] GrantPowerChain(IrScope scope) => PowerCompilation.GrantPowerChain(
            contentBindings, battleBindings, playerBindings, powerBindings, pilesBindings, powerQueryBindings,
            powerCommandsBindings, playerBlockBindings, scope);
        Inst[] RandomAttackChain(IrScope scope) => DamageCompilation.RandomAttackChain(
            contentBindings, battleBindings, playerBindings, pilesBindings, powerQueryBindings,
            monsterDamageBindings, scope);
        Inst[] RetaliateChain(IrScope scope, Expr attacker) => DamageCompilation.RetaliateChain(
            contentBindings, battleBindings, playerBindings, powerQueryBindings, monsterDamageBindings, scope,
            attacker);
        Inst HardcodedEnemyMove(Expr monster) => EnemyCompilation.HardcodedEnemyMove(
            enemiesBindings, powerCommandsBindings, playerDamageBindings, enemyTurnBindings, monster);
        Inst DeclaredEnemyMove(Expr monster) => EnemyCompilation.DeclaredEnemyMove(
            contentBindings, enemiesBindings, enemyTurnBindings, BuildCycle, MachinePerform, monster);
        Inst MachinePerform(Expr monster, AuthorEnemy enemy) => EnemyCompilation.MachinePerform(
            enemiesBindings, enemyTurnBindings, MoveInstructions, monster, enemy);
        int EffectiveMove(AuthorEnemy enemy, int moveIndex) => EnemyCompilation.EffectiveMove(enemy, moveIndex);
        Inst BuildCycle(Expr monster, AuthorEnemy enemy) => EnemyCompilation.BuildCycle(
            enemiesBindings, enemyTurnBindings, EffectiveMove, MoveInstructions, monster, enemy);
        Inst[] MoveInstructions(Expr monster, AuthorMove move) => EnemyCompilation.MoveInstructions(
            powerCommandsBindings, playerDamageBindings, enemyTurnBindings, MoveValue, EnemyBlockSteps,
            ApplyMovePower, AddCardsInstruction, monster, move);
        Inst ApplyMovePower(Expr monster, string argument, bool self) => EnemyCompilation.ApplyMovePower(
            battleBindings, playerBindings, powerCommandsBindings, enemyTurnBindings, monster, argument, self);
        Inst AddCardsInstruction(string argument) => EnemyCompilation.AddCardsInstruction(
            battleBindings, playerBindings, cardsBindings, pilesBindings, enemyTurnBindings, argument);
        Expr MoveValue(Expr monster, AuthorMoveStep step) => EnemyCompilation.MoveValue(
            enemiesBindings, enemyTurnBindings, monster, step);
        Inst[] SelectMachine(IrScope scope, Expr monster, AuthorEnemy enemy) => EnemyCompilation.SelectMachine(
            battleBindings, playerBindings, enemiesBindings, enemySelectionBindings, scope, monster, enemy);
        Inst[] ClearCostModifier(IrScope scope, Expr held) => TurnCompilation.ClearCostModifier(cardsBindings, scope, held);
        Inst HardcodedPowerTurnEnd(Expr power) => TurnCompilation.HardcodedPowerTurnEnd(
            powerBindings, powerCommandsBindings, turnEndBindings, power);
        Inst DeclaredPowerTurnEnd(Expr power) => TurnCompilation.DeclaredPowerTurnEnd(
            contentBindings, battleBindings, enemiesBindings, powerBindings, powerCommandsBindings,
            playerDamageBindings, playerBlockBindings, turnEndBindings, power);
        IEnumerable<Inst> EndTurnHandSteps(Expr cardInHand) => TurnCompilation.EndTurnHandSteps(
            contentBindings, battleBindings, playerBindings, cardsBindings, pilesBindings, powerRulesBindings,
            exhaustTriggerBindings, playerTurnEndBindings, TurnEndHookChain, DispatchCardHook, KeepOrDiscard,
            cardInHand);
        Inst KeepOrDiscard(Expr cardInHand) => TurnCompilation.KeepOrDiscard(
            cardsBindings, pilesBindings, playerTurnEndBindings, cardInHand,
            relicPrograms.Has(playerTurnEndBindings.EndPlayerTurn, RelicEffectKinds.RetainHand),
            (scope, card) => relicPrograms.Dispatch(scope, RelicTriggers.AfterCardDiscarded));
        Inst TurnEndHookChain(Expr cardInHand) => TurnCompilation.TurnEndHookChain(
            playerTurnEndBindings, DispatchCardHook, cardInHand);
        Inst[] SpawnStartingPowers(IrScope scope, SliceEnemy definition, int monsterSlot) => CombatantCompilation.SpawnStartingPowers(
            contentBindings, powerCommandsBindings, scope, definition, monsterSlot);
        (FieldSymbol Field, Expr Value)[] MonsterFields(IrScope scope, SliceEnemy definition,
                int? spawnHpOverride = null, Expr? hpValue = null) => CombatantCompilation.MonsterFields(
            enemiesBindings, ShortMoveName, MachineFields, scope, definition, spawnHpOverride, hpValue);
        IEnumerable<(FieldSymbol Field, Expr Value)> MachineFields(IrScope scope, SliceEnemy definition) => CombatantCompilation.MachineFields(
            enemiesBindings, scope, definition);
        string ShortMoveName(string declared) => EnemyCompilation.ShortMoveName(declared);
        (FieldSymbol Field, Expr Value)[] CardFields(IrScope scope, SliceCard definition, bool upgraded,
                bool unplayable = false, bool ethereal = false) => CardCompilation.CardFields(
            cardsBindings, scope, definition, upgraded, unplayable, ethereal);
        IReadOnlyList<(FieldSymbol Field, Expr Value)> RewardCardFields(IrScope scope, CardDefinition definition,
                bool upgraded) => CardCompilation.RewardCardFields(cardsBindings, scope, definition, upgraded);
        IEnumerable<Inst> BlockGainSteps(IrScope scope, Expr rawAmount, bool unpowered = false) => DamageCompilation.BlockGainSteps(
            contentBindings, battleBindings, playerBindings, powerQueryBindings, playerBlockBindings, scope,
            rawAmount, unpowered);
        IEnumerable<Inst> EnemyBlockSteps(Expr monster, Expr rawAmount) => EnemyCompilation.EnemyBlockSteps(
            contentBindings, playerBindings, enemiesBindings, powerQueryBindings, enemyTurnBindings, monster,
            rawAmount);
        CardEffectStateBindings EffectState(IrScope scope) => CardCompilation.EffectState(
            battleBindings, playerBindings, enemiesBindings, cardsBindings, pilesBindings, powerQueryBindings,
            HasTag, scope);
        Inst[] CompileEffects(IrScope scope, Expr cardRef, Expr owner, Expr selectedTarget, int targetSlot,
                int discardSlot, int discardSlotsSlot, int createdSlot, CardEffectProgram program, bool unpowered = false,
                Expr? paidX = null, Expr? paidStarX = null) => CardCompilation.CompileEffects(
            battleBindings, playerBindings, enemiesBindings, cardsBindings, pilesBindings,
            powerCommandsBindings, powerRulesBindings, monsterDamageBindings, playerHpLossBindings,
            drawBindings, EffectState, BlockGainSteps, EnergyGainBlocked, CardPiles, PileCollection,
            ExhaustAttackAndGrow, DispatchCardHook, scope, cardRef, owner, selectedTarget, targetSlot,
            discardSlot, discardSlotsSlot, createdSlot, program, unpowered, paidX, paidStarX, orbs, doom, Osty);
        CollectionSymbol PileCollection(CardEffectPile pile) => CardCompilation.PileCollection(pilesBindings, pile);
        CardPileProcedures CardPiles() => CardCompilation.CardPiles(
            battleBindings, playerBindings, cardsBindings, pilesBindings, exhaustTriggerBindings,
            knowledgeCommandsBindings, powerRulesBindings, DispatchCardHook);
        Inst[] DispatchCardHook(IrScope scope, Expr cardRef, string hook) => CardCompilation.DispatchCardHook(
            battleBindings, playerBindings, cardsBindings, powerRulesBindings, CompileEffects, scope, cardRef,
            hook);
        ProcedureSymbol? EnqueueProcedure(string kind, CollectionSymbol queue, EntityTypeSymbol queueEntryType,
                FieldSymbol queueIndexField, FieldSymbol queueEncounterIdField) => EncounterCompilation.EnqueueProcedure(
            options, contentBindings, encounterPoolBindings, runEncountersBindings, runCollectionsBindings,
            kind, queue, queueEntryType, queueIndexField, queueEncounterIdField);
        Expr HoldsPower(IrScope scope, Expr owner, string id) => PowerCompilation.HoldsPower(
            powerBindings, pilesBindings, scope, owner, id);
        Inst[] RemovePower(IrScope scope, Expr owner, string id) => PowerCompilation.RemovePower(
            powerBindings, pilesBindings, scope, owner, id);
        Expr InCombat(IrScope scope) => ActionCompilation.InCombat(runCollectionsBindings, roomServicesBindings, scope);
        Inst[] ManifestEffects(CardPlayContext c) => CardCompilation.ManifestEffects(
            battleBindings, cardsBindings, pilesBindings, powerCommandsBindings, monsterDamageBindings,
            playerBlockBindings, knowledgeCommandsBindings, c);
        Expr Innate(IrScope s, Expr c) => CardCompilation.Innate(cardsBindings, s, c);
        Inst[] CloneToHand(IrScope s, Expr c) => CardCompilation.CloneToHand(cardsBindings, pilesBindings, DispatchCardHook, s, c);
        Inst[] ExhaustAttackAndGrow(IrScope s, Expr c, string variable) => CardCompilation.ExhaustAttackAndGrow(
            contentBindings, battleBindings, playerBindings, cardsBindings, pilesBindings, powerQueryBindings,
            EffectState, CardPiles, s, c, variable);
        Expr HasTag(IrScope scope, Expr instance, string tag) => CardCompilation.HasTag(cardsBindings, scope, instance, tag);

    }
}
