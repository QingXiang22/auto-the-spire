using CoreTheSpire.Engine;
using BaseSpire.Slice.Effects;
using BaseSpire.Slice.Orchestration;
using static BaseSpire.Slice.Compilation.CompilationCallbacks;
using static BaseSpire.Slice.CombatSlicePackage;

namespace BaseSpire.Slice.Compilation;

internal static class DamageCompilation
{
    internal static MonsterDamageBindings MonsterDamage(
        ContentBindings contentBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings)
    {

        var damageMonster = contentBindings.Package.Procedure("DamageMonster");

        var hitAttacker = damageMonster.Param("Attacker", FieldTypeSchema.RefTo(playerBindings.Player));

        var hitTarget = damageMonster.Param("Target", FieldTypeSchema.RefTo(enemiesBindings.Monster));

        var hitAmount = damageMonster.Param("Amount", FieldTypeSchema.Int(0, 999));

        // B-05c：出手方属性门控——`Powered = false` 表示这次攻击**不受**出手方修正（Strength/Weak�?
        // 与受击方易伤影响（来源：`IsPoweredAttack()` = �?Move 且非 Unpowered）�?
        var hitPowered = damageMonster.Param("Powered", FieldTypeSchema.Bool());

        var hitFixed = damageMonster.Local("fixedDamage", FieldTypeSchema.Fixed());

        var hitStacks = damageMonster.Local("powerStacks", FieldTypeSchema.Int(-99, 99));

        var hitDamage = damageMonster.Local("damage", FieldTypeSchema.Int(0, 9999));

        var hitBlock = damageMonster.Local("block", FieldTypeSchema.Int(0, 999));

        var hitAbsorbed = damageMonster.Local("absorbed", FieldTypeSchema.Int(0, 999));

        var hitTargetRef = damageMonster.Param(hitTarget);

        return new MonsterDamageBindings(damageMonster, hitAttacker, hitAmount, hitPowered, hitFixed, hitStacks, hitDamage, hitBlock, hitAbsorbed, hitTargetRef);
    }

    internal static void MonsterDamageBody(
        EnemiesBindings enemiesBindings,
        PilesBindings pilesBindings,
        EventsBindings eventsBindings,
        PowerRulesBindings powerRulesBindings,
        MonsterDamageBindings monsterDamageBindings,
        DamageChainCallback DamageChain,
        Func<ProcedureBuilder, Inst[]>? relicAfterMonsterDied = null)
    {
        monsterDamageBindings.DamageMonster.Body(monsterDamageBindings.DamageMonster.If(monsterDamageBindings.DamageMonster.Greater(
            monsterDamageBindings.DamageMonster.Field(monsterDamageBindings.HitTargetRef, enemiesBindings.MonsterHp), monsterDamageBindings.DamageMonster.Const(0L)),
            DamageChain(monsterDamageBindings.DamageMonster, monsterDamageBindings.HitAmount, monsterDamageBindings.HitFixed, monsterDamageBindings.HitStacks, monsterDamageBindings.HitDamage, monsterDamageBindings.DamageMonster.Param(monsterDamageBindings.HitAttacker),
                monsterDamageBindings.HitTargetRef, monsterDamageBindings.DamageMonster.Param(monsterDamageBindings.HitPowered)).Concat(new Inst[]
            {
                // Block absorbs the already-truncated damage, then health.
                monsterDamageBindings.DamageMonster.SetLocal(monsterDamageBindings.HitBlock, monsterDamageBindings.DamageMonster.Field(monsterDamageBindings.HitTargetRef, enemiesBindings.MonsterBlock)),
                monsterDamageBindings.DamageMonster.SetLocal(monsterDamageBindings.HitAbsorbed, monsterDamageBindings.DamageMonster.Minimum(monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitBlock),
                    monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitDamage))),
                monsterDamageBindings.DamageMonster.If(monsterDamageBindings.DamageMonster.Const(true),powerRulesBindings.PowerRules.CapHpLoss(monsterDamageBindings.DamageMonster,monsterDamageBindings.HitTargetRef,monsterDamageBindings.HitDamage,monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitAbsorbed))),
                monsterDamageBindings.DamageMonster.SetField(monsterDamageBindings.HitTargetRef, enemiesBindings.MonsterBlock,
                    monsterDamageBindings.DamageMonster.Subtract(monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitBlock), monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitAbsorbed))),
                monsterDamageBindings.DamageMonster.SetField(monsterDamageBindings.HitTargetRef, enemiesBindings.MonsterHp, monsterDamageBindings.DamageMonster.Maximum(monsterDamageBindings.DamageMonster.Const(0L),
                    monsterDamageBindings.DamageMonster.Subtract(monsterDamageBindings.DamageMonster.Field(monsterDamageBindings.HitTargetRef, enemiesBindings.MonsterHp),
                        monsterDamageBindings.DamageMonster.Subtract(monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitDamage), monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitAbsorbed))))),
                monsterDamageBindings.DamageMonster.Emit(eventsBindings.DamageDealt, (eventsBindings.DamageDealtTarget, monsterDamageBindings.HitTargetRef),
                    (eventsBindings.DamageDealtAmount, monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitDamage))),
                monsterDamageBindings.DamageMonster.If(monsterDamageBindings.DamageMonster.Equal(monsterDamageBindings.DamageMonster.Field(monsterDamageBindings.HitTargetRef, enemiesBindings.MonsterHp),
                        monsterDamageBindings.DamageMonster.Const(0L)),
                    [
                    monsterDamageBindings.DamageMonster.Emit(eventsBindings.MonsterDied, (eventsBindings.MonsterDiedMonster, monsterDamageBindings.HitTargetRef)),
                    // A dead monster leaves the living set immediately, so it can never be a later target.
                    // ENV-T02：**刻意不在这里**判胜利——最后一次击杀只是伤害结算里的一步，父效果余效
                    // （Feed 的 `on_fatal_gain_max_hp`、遗物回血、实例变量写回）与出牌收尾还要继续跑；
                    // 胜利判定统一挂在**动作体末尾**的 `BattleEndTail`（`PlayCard`/`EndTurn`/`UsePotion`）。
                    monsterDamageBindings.DamageMonster.RemoveFromCollection(pilesBindings.Monsters, monsterDamageBindings.HitTargetRef),
                    .. relicAfterMonsterDied?.Invoke(monsterDamageBindings.DamageMonster) ?? [],
                    ]),
                monsterDamageBindings.DamageMonster.If(monsterDamageBindings.DamageMonster.And(
                    monsterDamageBindings.DamageMonster.Param(monsterDamageBindings.HitPowered),
                    monsterDamageBindings.DamageMonster.And(monsterDamageBindings.DamageMonster.Greater(
                        monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitDamage),
                        monsterDamageBindings.DamageMonster.LocalRef(monsterDamageBindings.HitAbsorbed)),
                        monsterDamageBindings.DamageMonster.Greater(monsterDamageBindings.DamageMonster.Field(
                            monsterDamageBindings.HitTargetRef,enemiesBindings.MonsterHp),monsterDamageBindings.DamageMonster.Const(0L)))),
                    powerRulesBindings.PowerRules.Call(monsterDamageBindings.DamageMonster,"AfterUnblockedAttack",monsterDamageBindings.HitTargetRef)),
            }).ToArray()));

    }

    internal static PlayerHpLossBindings PlayerHpLoss(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings)
    {

        // B-06a�?*HP 损失**（不是伤害）——来源里 `loseHp` �?`unblockable`（不经格挡）�?`unpowered`
        // （不经修正链），因此这里**不走 DamageChain**，只�?�?HP �?归零即失�?，死亡判定与 damagePlayer 同源�?
        var losePlayerHp = contentBindings.Package.Procedure("LosePlayerHp");

        var lostAmount = losePlayerHp.Param("Amount", FieldTypeSchema.Int(0, 999));

        var lostPlayer = losePlayerHp.First(losePlayerHp.Type(playerBindings.Player, battleBindings.BattleScope));

        return new PlayerHpLossBindings(losePlayerHp, lostAmount, lostPlayer);
    }

    internal static void PlayerHpLossBody(
        PlayerBindings playerBindings,
        EventsBindings eventsBindings,
        PowerRulesBindings powerRulesBindings,
        PlayerHpLossBindings playerHpLossBindings,
        SettleTailCallback SettleTail)
    {
        playerHpLossBindings.LosePlayerHp.Body(
            new Inst[]
            {
                // HP 损失同样�?失去过生�?（来源把 loseHp 与伤害都算作 lost HP）�?
                playerHpLossBindings.LosePlayerHp.If(playerHpLossBindings.LosePlayerHp.Greater(playerHpLossBindings.LosePlayerHp.Param(playerHpLossBindings.LostAmount), playerHpLossBindings.LosePlayerHp.Const(0L)),
                    new Inst[]
                    {
                        powerRulesBindings.PowerRules.Call(playerHpLossBindings.LosePlayerHp,"LostHp"),
                        playerHpLossBindings.LosePlayerHp.SetField(playerHpLossBindings.LostPlayer, playerBindings.PlayerLostHp, playerHpLossBindings.LosePlayerHp.Const(true)),
                        // ENV-T05：TearAsunder 的战斗级计数——直接失去生命也是一次"掉血事件"。
                        playerHpLossBindings.LosePlayerHp.SetField(playerHpLossBindings.LostPlayer, playerBindings.PlayerHpLossCount,
                            playerHpLossBindings.LosePlayerHp.Add(playerHpLossBindings.LosePlayerHp.Field(playerHpLossBindings.LostPlayer, playerBindings.PlayerHpLossCount),
                                playerHpLossBindings.LosePlayerHp.Const(1L))),
                    }),
                playerHpLossBindings.LosePlayerHp.SetField(playerHpLossBindings.LostPlayer, playerBindings.PlayerHp, playerHpLossBindings.LosePlayerHp.Maximum(playerHpLossBindings.LosePlayerHp.Const(0L),
                    playerHpLossBindings.LosePlayerHp.Subtract(playerHpLossBindings.LosePlayerHp.Field(playerHpLossBindings.LostPlayer, playerBindings.PlayerHp),
                        playerHpLossBindings.LosePlayerHp.Param(playerHpLossBindings.LostAmount)))),
                playerHpLossBindings.LosePlayerHp.If(playerHpLossBindings.LosePlayerHp.Equal(playerHpLossBindings.LosePlayerHp.Field(playerHpLossBindings.LostPlayer, playerBindings.PlayerHp), playerHpLossBindings.LosePlayerHp.Const(0L)),
                    [
                        .. SettleTail(playerHpLossBindings.LosePlayerHp),
                        playerHpLossBindings.LosePlayerHp.SetTerminal(eventsBindings.Defeat, (eventsBindings.DefeatTurn, playerHpLossBindings.LosePlayerHp.Field(playerHpLossBindings.LostPlayer, playerBindings.PlayerTurn))),
                    ]),
            }.ToArray());

    }

    internal static AfterDamageBindings AfterDamage(ContentBindings contentBindings, EnemiesBindings enemiesBindings)
    {

        // W-06：`trigger = after_damage_taken`——玩家受�?powered 伤害之后，按声明�?*伤害来源**反击�?
        // 声明位置必须�?`DamagePlayer` **之前**（它的过程体要用到这个符号），反击伤害是**无修�?*�?
        // （来�?`properties = [unpowered]`），因此�?`Powered = false` 的既有伤害路径�?
        var triggerAfterDamage = contentBindings.Package.Procedure("TriggerAfterDamageTaken");

        var damageSource = triggerAfterDamage.Param("Attacker", FieldTypeSchema.RefTo(enemiesBindings.Monster));

        return new AfterDamageBindings(triggerAfterDamage, damageSource);
    }

    internal static void AfterDamageBody(AfterDamageBindings afterDamageBindings, RetaliateChainCallback RetaliateChain)
    {
        afterDamageBindings.TriggerAfterDamage.Body(RetaliateChain(afterDamageBindings.TriggerAfterDamage, afterDamageBindings.TriggerAfterDamage.Param(afterDamageBindings.DamageSource)));

    }

    internal static PlayerDamageBindings PlayerDamage(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings)
    {

        // W-06：`trigger = afterPlayerTurnStart` 的过程声明放�?`GainPlayerBlock` **之后**（见下）�?
        // 它的链里有一种效果是"固定数值格�?，需�?`GainPlayerBlock` 的符号�?

        var damagePlayer = contentBindings.Package.Procedure("DamagePlayer");

        var struckAttacker = damagePlayer.Param("Attacker", FieldTypeSchema.RefTo(enemiesBindings.Monster));

        var struckAmount = damagePlayer.Param("Amount", FieldTypeSchema.Int(0, 999));

        var struckPowered = damagePlayer.Param("Powered", FieldTypeSchema.Bool());

        var struckPlayer = damagePlayer.First(damagePlayer.Type(playerBindings.Player, battleBindings.BattleScope));

        var struckFixed = damagePlayer.Local("fixedDamage", FieldTypeSchema.Fixed());

        var struckStacks = damagePlayer.Local("powerStacks", FieldTypeSchema.Int(-99, 99));

        var struckDamage = damagePlayer.Local("damage", FieldTypeSchema.Int(0, 9999));

        var struckBlock = damagePlayer.Local("block", FieldTypeSchema.Int(0, 999));

        var struckAbsorbed = damagePlayer.Local("absorbed", FieldTypeSchema.Int(0, 999));

        return new PlayerDamageBindings(damagePlayer, struckAttacker, struckAmount, struckPowered, struckPlayer, struckFixed, struckStacks, struckDamage, struckBlock, struckAbsorbed);
    }

    internal static void PlayerDamageBody(
        PlayerBindings playerBindings,
        EnemiesBindings enemiesBindings,
        EventsBindings eventsBindings,
        PowerRulesBindings powerRulesBindings,
        AfterDamageBindings afterDamageBindings,
        PlayerDamageBindings playerDamageBindings,
        DamageChainCallback DamageChain,
        SettleTailCallback SettleTail, BaseSpire.Slice.Domain.Combat.CombatOsty? osty,
        Func<ProcedureBuilder, Inst[]>? relicAfterDamage = null)
    {
        var tail = new Inst[]
            {
                playerDamageBindings.DamagePlayer.SetLocal(playerDamageBindings.StruckBlock, playerDamageBindings.DamagePlayer.Field(playerDamageBindings.StruckPlayer, playerBindings.PlayerBlock)),
                playerDamageBindings.DamagePlayer.SetLocal(playerDamageBindings.StruckAbsorbed, playerDamageBindings.DamagePlayer.Minimum(playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckBlock),
                    playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckDamage))),
                playerDamageBindings.DamagePlayer.SetField(playerDamageBindings.StruckPlayer, playerBindings.PlayerBlock,
                    playerDamageBindings.DamagePlayer.Subtract(playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckBlock), playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckAbsorbed))),
                playerDamageBindings.DamagePlayer.SetField(playerDamageBindings.StruckPlayer, playerBindings.PlayerHp, playerDamageBindings.DamagePlayer.Maximum(playerDamageBindings.DamagePlayer.Const(0L),
                    playerDamageBindings.DamagePlayer.Subtract(playerDamageBindings.DamagePlayer.Field(playerDamageBindings.StruckPlayer, playerBindings.PlayerHp),
                        playerDamageBindings.DamagePlayer.Subtract(playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckDamage),
                            playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckAbsorbed))))),
                // 只有**真的掉了 HP**（扣掉格挡吸收之�?> 0）才�?失去过生�?�?
                playerDamageBindings.DamagePlayer.If(playerDamageBindings.DamagePlayer.Greater(
                        playerDamageBindings.DamagePlayer.Subtract(playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckDamage),
                            playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckAbsorbed)), playerDamageBindings.DamagePlayer.Const(0L)),
                    new Inst[]
                    {
                        powerRulesBindings.PowerRules.Call(playerDamageBindings.DamagePlayer,"LostHp"),powerRulesBindings.PowerRules.Call(playerDamageBindings.DamagePlayer,"ReceivedDamage"),
                        playerDamageBindings.DamagePlayer.SetField(playerDamageBindings.StruckPlayer, playerBindings.PlayerLostHp, playerDamageBindings.DamagePlayer.Const(true)),
                        // ENV-T05：同一次"掉血事件"在战斗级计数里也记一次（完全被挡下不计）。
                        playerDamageBindings.DamagePlayer.SetField(playerDamageBindings.StruckPlayer, playerBindings.PlayerHpLossCount,
                            playerDamageBindings.DamagePlayer.Add(playerDamageBindings.DamagePlayer.Field(playerDamageBindings.StruckPlayer, playerBindings.PlayerHpLossCount),
                                playerDamageBindings.DamagePlayer.Const(1L))),
                    }.Concat(relicAfterDamage?.Invoke(playerDamageBindings.DamagePlayer) ?? []).ToArray()),
                playerDamageBindings.DamagePlayer.Emit(eventsBindings.PlayerDamaged, (eventsBindings.PlayerDamagedAttacker, playerDamageBindings.DamagePlayer.Param(playerDamageBindings.StruckAttacker)),
                    (eventsBindings.PlayerDamagedAmount, playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckDamage))),
                // W-06：受�?**powered 且正�?*伤害之后的能力触发（来源事件�?`require=[move]`/
                // `exclude=[unpowered]`/`positiveAmount=false`）——例�?FlameBarrier 的反击�?
                playerDamageBindings.DamagePlayer.If(playerDamageBindings.DamagePlayer.And(playerDamageBindings.DamagePlayer.Param(playerDamageBindings.StruckPowered),
                        playerDamageBindings.DamagePlayer.Greater(playerDamageBindings.DamagePlayer.LocalRef(playerDamageBindings.StruckDamage), playerDamageBindings.DamagePlayer.Const(0L))),
                    playerDamageBindings.DamagePlayer.Call(afterDamageBindings.TriggerAfterDamage.Symbol, playerDamageBindings.DamagePlayer.Param(playerDamageBindings.StruckAttacker))),
                playerDamageBindings.DamagePlayer.If(playerDamageBindings.DamagePlayer.Equal(playerDamageBindings.DamagePlayer.Field(playerDamageBindings.StruckPlayer, playerBindings.PlayerHp), playerDamageBindings.DamagePlayer.Const(0L)),
                    [
                        .. SettleTail(playerDamageBindings.DamagePlayer),
                        playerDamageBindings.DamagePlayer.SetTerminal(eventsBindings.Defeat,
                            (eventsBindings.DefeatTurn, playerDamageBindings.DamagePlayer.Field(playerDamageBindings.StruckPlayer, playerBindings.PlayerTurn))),
                    ]),
            };
        var scope = playerDamageBindings.DamagePlayer;
        var unblocked = scope.Subtract(scope.LocalRef(playerDamageBindings.StruckDamage), scope.LocalRef(playerDamageBindings.StruckAbsorbed));
        var afterBlock = osty is null ? tail.Skip(3).ToArray() : new[] { scope.IfElse(
            scope.And(scope.Param(playerDamageBindings.StruckPowered), scope.And(scope.Greater(unblocked, scope.Const(0L)),
                scope.Not(scope.IsNone(osty.Living(scope, playerDamageBindings.StruckPlayer))))),
            osty.Receive(scope, playerDamageBindings.StruckPlayer, unblocked), tail.Skip(3).ToArray()) };
        scope.Body(scope.If(scope.Greater(scope.Field(scope.Param(playerDamageBindings.StruckAttacker),enemiesBindings.MonsterHp),scope.Const(0L)),
            DamageChain(scope, playerDamageBindings.StruckAmount, playerDamageBindings.StruckFixed,
            playerDamageBindings.StruckStacks, playerDamageBindings.StruckDamage, scope.Param(playerDamageBindings.StruckAttacker),
            playerDamageBindings.StruckPlayer, scope.Param(playerDamageBindings.StruckPowered)).Concat(new[] {
                scope.If(scope.Param(playerDamageBindings.StruckPowered),powerRulesBindings.PowerRules.Call(scope,"BeforeIncomingAttack",scope.Param(playerDamageBindings.StruckAttacker)))
            }).Concat(tail.Take(3)).Concat(afterBlock).ToArray()));

    }

    internal static PlayerBlockBindings PlayerBlock(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings)
    {

        var gainPlayerBlock = contentBindings.Package.Procedure("GainPlayerBlock");

        var blockAmount = gainPlayerBlock.Param("Amount", FieldTypeSchema.Int(0, 999));

        var blockPlayer = gainPlayerBlock.First(gainPlayerBlock.Type(playerBindings.Player, battleBindings.BattleScope));

        // W-06：`trigger = after_block_gained`——获得格�?*之后**触发（JuggernautPower：随机打一下）�?
        var triggerAfterBlock = contentBindings.Package.Procedure("TriggerAfterBlockGained");

        return new PlayerBlockBindings(gainPlayerBlock, blockAmount, blockPlayer, triggerAfterBlock);
    }

    internal static void PlayerBlockBody(
        ContentBindings contentBindings,
        PlayerBindings playerBindings,
        PlayerBlockBindings playerBlockBindings,
        RandomAttackChainCallback RandomAttackChain)
    {
        playerBlockBindings.TriggerAfterBlock.Body(RandomAttackChain(playerBlockBindings.TriggerAfterBlock));

        playerBlockBindings.GainPlayerBlock.Body(
            playerBlockBindings.GainPlayerBlock.SetField(playerBlockBindings.BlockPlayer, playerBindings.PlayerBlock,
                playerBlockBindings.GainPlayerBlock.Add(playerBlockBindings.GainPlayerBlock.Field(playerBlockBindings.BlockPlayer, playerBindings.PlayerBlock),
                    playerBlockBindings.GainPlayerBlock.Param(playerBlockBindings.BlockAmount))),
            contentBindings.Author is null
                ? playerBlockBindings.GainPlayerBlock.If(playerBlockBindings.GainPlayerBlock.Const(false))
                : playerBlockBindings.GainPlayerBlock.Call(playerBlockBindings.TriggerAfterBlock.Symbol));

    }

    internal static TurnStartTriggerBindings TurnStartTrigger(ContentBindings contentBindings)
    {

        // W-06：`trigger = afterPlayerTurnStart`——玩家回合开始时按声明生效（DemonFormPower：按层数加能力；
        // SelfFormingClayPower：固�?3 点格�?+ 移除自己）。声明在这里是因为链里要用到 `GainPlayerBlock`�?
        var triggerTurnStart = contentBindings.Package.Procedure("TriggerAfterPlayerTurnStart");

        return new TurnStartTriggerBindings(triggerTurnStart);
    }

    internal static void TurnStartTriggerBody(
        TurnStartTriggerBindings turnStartTriggerBindings,
        GrantPowerChainCallback GrantPowerChain)
    {
        turnStartTriggerBindings.TriggerTurnStart.Body(GrantPowerChain(turnStartTriggerBindings.TriggerTurnStart));

    }

    internal static ExhaustTriggerBindings ExhaustTrigger(ContentBindings contentBindings)
    {

        // B-06a�?*能力的声明式触发�?*——`trigger = after_card_exhausted` 时，一张牌被消耗后按层数给格挡�?
        // 这里没有新的原语：用既有的纯过程 `PowerStacksOf` 读层数、`GainPlayerBlock` 给格挡�?
        var triggerAfterExhaust = contentBindings.Package.Procedure("TriggerAfterCardExhausted");

        return new ExhaustTriggerBindings(triggerAfterExhaust);
    }

    internal static void ExhaustTriggerBody(
        PowerRulesBindings powerRulesBindings,
        ExhaustTriggerBindings exhaustTriggerBindings,
        TriggerChainCallback TriggerChain,
        Func<ProcedureBuilder, Inst[]>? relicAfterExhaust = null)
    {
        exhaustTriggerBindings.TriggerAfterExhaust.Body([
            .. TriggerChain(exhaustTriggerBindings.TriggerAfterExhaust, AuthorPowerTrigger.AfterCardExhausted),
            powerRulesBindings.PowerRules.Call(exhaustTriggerBindings.TriggerAfterExhaust, "Exhaust"),
            .. relicAfterExhaust?.Invoke(exhaustTriggerBindings.TriggerAfterExhaust) ?? []]);

    }

    internal static PlayedTriggerBindings PlayedTrigger(ContentBindings contentBindings, CardsBindings cardsBindings)
    {

        // B-06a：`trigger = after_card_played`（可�?`when-card-type`）——一张牌被打�?*之后**触发�?
        var triggerAfterPlayed = contentBindings.Package.Procedure("TriggerAfterCardPlayed");

        var playedForTrigger = triggerAfterPlayed.Param("Card", FieldTypeSchema.RefTo(cardsBindings.Card));

        return new PlayedTriggerBindings(triggerAfterPlayed, playedForTrigger);
    }

    internal static void PlayedTriggerBody(PlayedTriggerBindings playedTriggerBindings, TriggerChainCallback TriggerChain,
        Func<ProcedureBuilder, Expr, Inst[]>? relicAfterPlayed = null)
    {
        var played = playedTriggerBindings.TriggerAfterPlayed.Param(playedTriggerBindings.PlayedForTrigger);
        playedTriggerBindings.TriggerAfterPlayed.Body([
            .. TriggerChain(playedTriggerBindings.TriggerAfterPlayed, AuthorPowerTrigger.AfterCardPlayed, played),
            .. relicAfterPlayed?.Invoke(playedTriggerBindings.TriggerAfterPlayed, played) ?? []]);

    }

    internal static List<Inst> DamageChain(
        AdditiveStageCallback AdditiveStage,
        MultiplicativeStageCallback MultiplicativeStage,
        CapStageCallback CapStage,
        ProcedureBuilder scope,
        int amountSlot,
        int fixedSlot,
        int stacksSlot,
        int damageSlot,
        Expr attacker,
        Expr target,
        Expr powered)
    {
        return
        [
            scope.SetLocal(fixedSlot, scope.IntToFixed(scope.Param(amountSlot))),
                // 属性门控（来源 `IsPoweredAttack()` = �?Move 且非 Unpowered）：**�?powered 的攻�?*同时跳过
                // 出手方的 Strength/Weak 与受击方�?Vulnerable —�?三个修正都在下面这两个阶段里�?
                scope.If(powered, AdditiveStage(scope, amountSlot, fixedSlot, stacksSlot, attacker, target)),
                scope.If(powered,
                    MultiplicativeStage(scope, amountSlot, fixedSlot, stacksSlot, attacker, target)),
                .. CapStage(scope, fixedSlot, stacksSlot, target),
                // The cap stage (Hook.cs:2539-2555) has no slice equivalent yet. The lower bound of zero and the one
                // conversion are the last two steps; the rule is Truncate because `(int)` on a non-negative decimal
                // truncates, which the �? clamp makes equivalent to Floor.
                scope.SetLocal(fixedSlot, scope.Maximum(scope.LocalRef(fixedSlot), scope.Const(0m))),
                scope.SetLocal(damageSlot, scope.FixedToInt(scope.LocalRef(fixedSlot), FixedRounding.Truncate)),
            ];
    }

    internal static Inst[] AdditiveStage(
        ContentBindings contentBindings,
        PowerQueryBindings powerQueryBindings,
        ProcedureBuilder scope,
        int amountSlot,
        int fixedSlot,
        int stacksSlot,
        Expr attacker,
        Expr target)
    {
        if (contentBindings.Author is null)
        {
            return
            [
                // Additive stage: the attacker's own Strength, one point per stack (StrengthPower.cs:15-26).
                scope.SetLocal(stacksSlot,
                        scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, attacker, scope.Const(SliceManifest.StrengthId))),
                    scope.SetLocal(fixedSlot,
                        scope.Add(scope.LocalRef(fixedSlot), scope.IntToFixed(scope.LocalRef(stacksSlot)))),
                ];
        }

        var steps = new List<Inst>();
        foreach (var power in contentBindings.Author.Powers.Where(entry =>
                     string.Equals(entry.DamageModifier, PowerDamage.AddPerStack, StringComparison.Ordinal)))
        {
            var perStack = long.Parse(power.DamageModifierValue);
            if (perStack == 0)
            {
                continue;
            }

            // 层数 × 每层数值：按声明的 per-stack 系数一次算清，避免多次截断�?
            steps.Add(scope.SetLocal(stacksSlot,
                scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, attacker, scope.Const(power.Id))));
            steps.Add(scope.SetLocal(fixedSlot,
                scope.Add(scope.LocalRef(fixedSlot),
                    scope.Multiply(scope.IntToFixed(scope.LocalRef(stacksSlot)), scope.Const((decimal)perStack)))));
        }

        _ = amountSlot;
        _ = target;
        return steps.ToArray();
    }

    internal static Inst[] CapStage(
        ContentBindings contentBindings,
        PowerQueryBindings powerQueryBindings,
        IrScope scope,
        int fixedSlot,
        int stacksSlot,
        Expr target)
    {
        if (contentBindings.Author is null)
        {
            return [];
        }

        var steps = new List<Inst>();
        foreach (var power in contentBindings.Author.Powers.Where(entry => entry.DamageCap.Length > 0))
        {
            steps.Add(scope.SetLocal(stacksSlot,
                scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, target, scope.Const(power.Id))));
            steps.Add(scope.If(scope.Greater(scope.LocalRef(stacksSlot), scope.Const(0L)),
                scope.SetLocal(fixedSlot, scope.Minimum(scope.LocalRef(fixedSlot),
                    scope.Const(decimal.Parse(power.DamageCap,
                        System.Globalization.CultureInfo.InvariantCulture))))));
        }

        return steps.ToArray();
    }

    internal static Inst[] MultiplicativeStage(
        ContentBindings contentBindings,
        PowerQueryBindings powerQueryBindings,
        ProcedureBuilder scope,
        int amountSlot,
        int fixedSlot,
        int stacksSlot,
        Expr attacker,
        Expr target)
    {
        if (contentBindings.Author is null)
        {
            return
            [
                // Multiplicative stage, dealer side first: Weak x0.75 (WeakPower.cs:22-33).
                scope.SetLocal(stacksSlot,
                        scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, attacker, scope.Const(SliceManifest.WeakId))),
                    scope.If(scope.Greater(scope.LocalRef(stacksSlot), scope.Const(0L)),
                        scope.SetLocal(fixedSlot, scope.Multiply(scope.LocalRef(fixedSlot),
                            scope.Const(SliceManifest.WeakDamageMultiplier)))),
                    // Then the target side: Vulnerable x1.5 (VulnerablePower.cs:22,27-36). Both stages only test that the
                    // power is present: its stacks are a duration that ticks down, never a magnitude.
                    scope.SetLocal(stacksSlot,
                        scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, target, scope.Const(SliceManifest.VulnerableId))),
                    scope.If(scope.Greater(scope.LocalRef(stacksSlot), scope.Const(0L)),
                        scope.SetLocal(fixedSlot, scope.Multiply(scope.LocalRef(fixedSlot),
                            scope.Const(SliceManifest.VulnerableDamageMultiplier)))),
                ];
        }

        var steps = new List<Inst>();
        foreach (var side in new[] { PowerDamage.MultiplyOutgoing, PowerDamage.MultiplyIncoming })
        {
            foreach (var power in contentBindings.Author.Powers.Where(entry =>
                         string.Equals(entry.DamageModifier, side, StringComparison.Ordinal)))
            {
                var owner = string.Equals(side, PowerDamage.MultiplyOutgoing, StringComparison.Ordinal)
                    ? attacker
                    : target;
                steps.Add(scope.SetLocal(stacksSlot,
                    scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, scope.Const(power.Id))));
                // ENV-02：受击修正可以带**来源条件**——只有当攻击者身上有该能力时修正才生�?
                //（来�?`ColossusPower.ModifyDamageMultiplicative` �?`dealer.HasPower<VulnerablePower>()`）�?
                var held = power.NegativeStacksAreInfinite
                    ? scope.NotEqual(scope.LocalRef(stacksSlot), scope.Const(0L))
                    : scope.Greater(scope.LocalRef(stacksSlot), scope.Const(0L));
                var applies = power.DamageModifierSourcePower.Length == 0
                    ? held
                    : scope.And(
                        held,
                        scope.Greater(
                            scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, attacker,
                                scope.Const(power.DamageModifierSourcePower)),
                            scope.Const(0L)));
                // ENV-02：易伤倍率可以�?*攻击�?*身上的能力按层数抬高（来�?
                // `CrueltyPower.ModifyVulnerableMultiplier` �?`amount + Amount / 100m`）�?
                // 只在"这条修正属于易伤"且确有这类能力时生效：把倍率写成**运行时表达式**
                //（`base + Σ IntToFixed(攻击者层�? × 百分比`），而不是常量�?
                var donors = string.Equals(side, PowerDamage.MultiplyIncoming, StringComparison.Ordinal)
                             && string.Equals(power.Id, SliceManifest.VulnerableId, StringComparison.Ordinal)
                    ? contentBindings.Author.Powers.Where(entry => entry.VulnerableMultiplierPercentPerStack != 0).ToList()
                    : [];
                if (donors.Count > 0)
                {
                    var baseMultiplier = scope.Const(decimal.Parse(power.DamageModifierValue,
                        System.Globalization.CultureInfo.InvariantCulture));
                    var terms = donors.Select(donor => scope.Multiply(
                        scope.IntToFixed(scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, attacker,
                            scope.Const(donor.Id))),
                        scope.Const(donor.VulnerableMultiplierPercentPerStack / 100m)));
                    var multiplier = terms.Aggregate(baseMultiplier, (left, right) => scope.Add(left, right));
                    steps.Add(scope.If(applies,
                        scope.SetLocal(fixedSlot, scope.Multiply(scope.LocalRef(fixedSlot), multiplier))));
                    continue;
                }

                steps.Add(scope.If(applies,
                    scope.SetLocal(fixedSlot, scope.Multiply(scope.LocalRef(fixedSlot),
                        scope.Const(decimal.Parse(power.DamageModifierValue,
                            System.Globalization.CultureInfo.InvariantCulture))))));
            }
        }

        _ = amountSlot;
        return steps.ToArray();
    }

    internal static Inst[] RandomAttackChain(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PilesBindings pilesBindings,
        PowerQueryBindings powerQueryBindings,
        MonsterDamageBindings monsterDamageBindings,
        IrScope scope)
    {
        var declared = contentBindings.Author is null
            ? []
            : contentBindings.Author.Powers.Where(power => string.Equals(power.Trigger,
                AuthorPowerTrigger.AfterBlockGained, StringComparison.Ordinal)).ToList();
        if (declared.Count == 0)
        {
            return [scope.If(scope.Const(false))];
        }

        var instances = new List<Inst>();
        var index = 0;
        foreach (var power in declared)
        {
            var stacks = scope.Local($"blockTriggerStacks{index}", FieldTypeSchema.Int(-99, 99));
            var picked = scope.Local($"blockTriggerTarget{index}", FieldTypeSchema.Binding());
            var amount = long.Parse(
                power.TriggerEffect[PowerTriggerEffect.RandomAttackPerStackPrefix.Length..]);
            instances.Add(scope.SetLocal(stacks,
                scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                    scope.Const(power.Id))));
            instances.Add(scope.If(scope.Greater(scope.LocalRef(stacks), scope.Const(0L)), new[]
            {
                    scope.SamplePick(SliceManifest.RandomTargetStream,
                        scope.Sort(scope.Collection(pilesBindings.Monsters), item => item), picked),
                    scope.Call(monsterDamageBindings.DamageMonster.Symbol, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                        scope.LocalRef(picked),
                        scope.Multiply(scope.LocalRef(stacks), scope.Const(amount)),
                        scope.Const(false)),
                }));
            index++;
        }

        return [.. instances];
    }

    internal static Inst[] RetaliateChain(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PowerQueryBindings powerQueryBindings,
        MonsterDamageBindings monsterDamageBindings,
        IrScope scope,
        Expr attacker)
    {
        var declared = contentBindings.Author is null
            ? []
            : contentBindings.Author.Powers.Where(power => string.Equals(power.Trigger, AuthorPowerTrigger.AfterDamageTaken,
                StringComparison.Ordinal)).ToList();
        if (declared.Count == 0)
        {
            return [scope.If(scope.Const(false))];
        }

        var instances = new List<Inst>();
        var index = 0;
        foreach (var power in declared)
        {
            var stacks = scope.Local($"retaliateStacks{index}", FieldTypeSchema.Int(-99, 99));
            var amount = long.Parse(
                power.TriggerEffect[PowerTriggerEffect.RetaliatePerStackPrefix.Length..]);
            instances.Add(scope.SetLocal(stacks,
                scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                    scope.Const(power.Id))));
            var hit = scope.Call(monsterDamageBindings.DamageMonster.Symbol,
                scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope)),
                attacker,
                scope.Multiply(scope.LocalRef(stacks), scope.Const(amount)),
                scope.Const(false));
            instances.Add(scope.If(scope.Greater(scope.LocalRef(stacks), scope.Const(0L)), hit));
            index++;
        }

        return [.. instances];
    }

    internal static IEnumerable<Inst> BlockGainSteps(
        ContentBindings contentBindings,
        BattleBindings battleBindings,
        PlayerBindings playerBindings,
        PowerQueryBindings powerQueryBindings,
        PlayerBlockBindings playerBlockBindings,
        IrScope scope,
        Expr rawAmount,
        bool unpowered = false)
    {
        var donors = contentBindings.Author?.Powers.Where(entry => entry.BlockGainMultiplier.Length > 0).ToList() ?? [];
        // 加性阶段（Dexterity）：在乘性阶段**之前**加 `层数 × 每层点数`（`Hook.ModifyBlock` 的顺序）。
        var additive = contentBindings.Author?.Powers.Where(entry => entry.BlockGainAdditivePerStack != 0).ToList() ?? [];
        if (unpowered || (donors.Count == 0 && additive.Count == 0))
        {
            yield return scope.Call(playerBlockBindings.GainPlayerBlock.Symbol, rawAmount);
            yield break;
        }

        var owner = scope.First(scope.Type(playerBindings.Player, battleBindings.BattleScope));
        var fixedSlot = scope.Local($"blockGain{playerBindings.NextBlockGainSlot.Next()}",
            FieldTypeSchema.Fixed(2, 0m, 9999m));
        yield return scope.SetLocal(fixedSlot, scope.IntToFixed(rawAmount));
        foreach (var power in additive)
        {
            yield return scope.SetLocal(fixedSlot, scope.Add(scope.LocalRef(fixedSlot),
                scope.IntToFixed(scope.Multiply(
                    scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, scope.Const(power.Id)),
                    scope.Const((long)power.BlockGainAdditivePerStack)))));
        }
        foreach (var power in donors)
        {
            var multiplier = scope.Const(decimal.Parse(power.BlockGainMultiplier,
                System.Globalization.CultureInfo.InvariantCulture));
            if (power.BlockGainPriorEntriesPerStack <= 0)
            {
                // 无次数上限（Frail）：**拥有者身上真的有这个能力时**才乘 —— 源里的
                // `ModifyBlockMultiplicative` 只在 `Owner == target` 且该能力存在时被调用，
                // 0 层不存在的能力不能凭空改数值。
                yield return scope.If(
                    scope.Greater(scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, scope.Const(power.Id)),
                        scope.Const(0L)),
                    scope.SetLocal(fixedSlot, scope.Multiply(scope.LocalRef(fixedSlot), multiplier)));
                continue;
            }

            var allowance = scope.Multiply(
                scope.CallPure(powerQueryBindings.PowerStacksOf.Symbol, owner, scope.Const(power.Id)),
                scope.Const((long)power.BlockGainPriorEntriesPerStack));
            yield return scope.If(
                scope.Less(scope.Field(owner, playerBindings.PlayerBlockGains), allowance),
                scope.SetLocal(fixedSlot, scope.Multiply(scope.LocalRef(fixedSlot), multiplier)));
        }

        // 原版在这里做 `Math.Max(0m)`，随后 `GainBlockInternal` 截断一次。
        yield return scope.SetLocal(fixedSlot, scope.Maximum(scope.LocalRef(fixedSlot), scope.Const(0m)));
        yield return scope.Call(playerBlockBindings.GainPlayerBlock.Symbol,
            scope.FixedToInt(scope.LocalRef(fixedSlot), FixedRounding.Truncate));
        yield return scope.SetField(owner, playerBindings.PlayerBlockGains,
            scope.Add(scope.Field(owner, playerBindings.PlayerBlockGains), scope.Const(1L)));
    }

}
